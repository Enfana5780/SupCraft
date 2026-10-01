#!/usr/bin/env python3
"""Build a self-contained SupCraft macOS app on the target architecture."""
import argparse
import hashlib
import json
from pathlib import Path
import platform
import plistlib
import re
import shutil
import subprocess
import sys
import tarfile
import urllib.request

from bundle_dylibs import audit, macho, relocate
from collect_legal import collect as collect_legal

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parent
VERSION = (PROJECT / "VERSION").read_text(encoding="utf8").strip()
if not re.fullmatch(r"\d+\.\d+\.\d+", VERSION):
    raise ValueError("VERSION must contain major.minor.patch")


def run(*args, cwd=None):
    print("+", " ".join(str(arg) for arg in args), flush=True)
    subprocess.run([str(arg) for arg in args], cwd=cwd, check=True)


def upstream(name, sha, vendor):
    target = vendor / (name + "-" + sha)
    if target.exists():
        return target
    archive = vendor / (name + "-" + sha + ".tar.gz")
    urllib.request.urlretrieve(f"https://codeload.github.com/cubicibo/{name}/tar.gz/{sha}", archive)
    with tarfile.open(archive) as source:
        source.extractall(vendor, filter="data")
    return target


def build(output):
    if sys.platform != "darwin":
        raise RuntimeError("Native dependency builds and signing require macOS.")
    if platform.machine() != "arm64":
        raise RuntimeError("SupCraft for macOS supports only native Apple Silicon (arm64) builds.")
    arch = "arm64"
    output = output.resolve(); output.mkdir(parents=True, exist_ok=True)
    stage = output / ("stage-" + arch)
    if stage.exists():
        if not stage.is_relative_to(output) or stage.is_symlink():
            raise RuntimeError("Unsafe build stage")
        shutil.rmtree(stage)
    stage.mkdir()
    vendor = ROOT / "vendor" / "upstream"; vendor.mkdir(parents=True, exist_ok=True)
    pins = json.loads((ROOT / "upstream-pins.json").read_text())
    sources = {name: upstream(name, sha, vendor) for name, sha in pins.items()}
    venv = ROOT / "vendor" / ("venv-" + arch)
    if not venv.exists():
        run(sys.executable, "-m", "venv", venv)
    python = venv / "bin/python"
    run(python, "-m", "pip", "install", "--upgrade", "pip")
    run(python, "-m", "pip", "install", "-r", ROOT / "requirements-macos.txt")
    # Install immutable local sources, avoiding the upstream unpinned git requirements.
    for name in ("ScenariStream", "piliq", "brule", "SUPer"):
        run(python, "-m", "pip", "install", "--no-deps", "--no-build-isolation", sources[name])
    # Make the fallback available in macOS spawn workers, which import SUPer afresh.
    site = Path(subprocess.check_output([str(python), "-c", "import sysconfig; print(sysconfig.get_path('purelib'))"], text=True).strip())
    patched_utils = site / "SUPer/utils.py"
    utils_source = patched_utils.read_text()
    original_ssim = "from SSIM_PIL import compare_ssim"
    if utils_source.count(original_ssim) != 1:
        raise RuntimeError("The pinned SUPer SSIM integration hook changed unexpectedly")
    patched_utils.write_text(utils_source.replace(original_ssim, "from gpu_fallback import compare_ssim"))

    builddir = stage / "renderer-build"
    run("meson", "setup", builddir, sources["ass2bdnxml"], "--buildtype=release")
    run("ninja", "-C", builddir)
    patched = stage / "supercli.py"
    source = (sources["SUPer"] / "supercli.py").read_text()
    original = "config_file = application_path.joinpath('config.ini')"
    if source.count(original) != 1:
        raise RuntimeError("The pinned SUPer config hook changed unexpectedly")
    patched.write_text(source.replace(original, "config_file = Path(os.environ['SUPCRAFT_JOB_CONFIG']) if 'SUPCRAFT_JOB_CONFIG' in os.environ else application_path.joinpath('config.ini')"))
    run(python, "-m", "PyInstaller", "--noconfirm", "--clean", "--onedir", "--name", "super-cli", "--distpath", stage / "frozen", "--workpath", stage / "pyinstaller-work", "--specpath", stage,
        "--paths", ROOT / "runtime", "--add-data", str(patched) + ":.", "--collect-all", "SUPer", "--collect-all", "SSIM_PIL", "--collect-all", "pyopencl", "--collect-all", "brule", "--collect-all", "piliq", "--collect-all", "scenaristream", ROOT / "runtime/super_entry.py")

    app = stage / "SupCraft.app"; contents = app / "Contents"; resources = contents / "Resources"; tools = resources / "tools"
    tools.mkdir(parents=True)
    run("dotnet", "publish", ROOT / "SupCraft.Mac/SupCraft.Mac.csproj", "-c", "Release", "-r", "osx-" + arch, "--self-contained", "true", "-p:UseAppHost=true", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=false", "-p:DebugType=embedded", "-o", contents / "MacOS")
    shutil.copy2(builddir / "ass2bdnxml", tools / "ass2bdnxml")
    shutil.copytree(stage / "frozen/super-cli", tools / "super", symlinks=True)
    quant_prefix = Path(subprocess.check_output(["brew", "--prefix", "libimagequant"], text=True).strip())
    quant_source = (quant_prefix / "lib/libimagequant.dylib").resolve()
    lib = tools / "lib"; lib.mkdir()
    quant_target = lib / quant_source.name; shutil.copy2(quant_source, quant_target)
    run("install_name_tool", "-id", "@rpath/" + quant_target.name, quant_target)
    copied = {quant_source: quant_target}
    relocate(tools / "ass2bdnxml", lib, copied)
    relocate(quant_target, lib, copied)
    # PyInstaller can collect dependencies with absolute brew paths; relocate them too.
    for binary in list(app.rglob("*")):
        if not binary.is_symlink() and macho(binary):
            relocate(binary, lib, copied)
    (tools / "toolchain.json").write_text(json.dumps({"renderer": "ass2bdnxml", "encoder": "super/super-cli", "quantizer": "lib/" + quant_target.name}, indent=2))
    iconset = stage / "SupCraft.iconset"; iconset.mkdir()
    for logical, actual in ((16, 16), (16, 32), (32, 32), (32, 64), (128, 128), (128, 256), (256, 256), (256, 512), (512, 512), (512, 1024)):
        name = f"icon_{logical}x{logical}" + ("@2x" if actual != logical else "") + ".png"
        run("sips", "-z", actual, actual, PROJECT / "assets/subforge.png", "--out", iconset / name)
    run("iconutil", "-c", "icns", iconset, "-o", resources / "SupCraft.icns")
    plist = {"CFBundleExecutable": "SupCraft", "CFBundleIdentifier": "com.enfana5780.supcraft", "CFBundleName": "SupCraft", "CFBundleDisplayName": "SupCraft", "CFBundleIconFile": "SupCraft.icns", "CFBundlePackageType": "APPL", "CFBundleInfoDictionaryVersion": "6.0", "CFBundleVersion": VERSION, "CFBundleShortVersionString": VERSION, "LSMinimumSystemVersion": "15.0", "NSHighResolutionCapable": True}
    (contents / "Info.plist").write_bytes(plistlib.dumps(plist))
    (resources / "upstream-pins.json").write_text(json.dumps(pins, indent=2))
    licenses = resources / "licenses"; licenses.mkdir()
    for name, directory in sources.items():
        matches = list(directory.glob("LICENSE*")) + list(directory.glob("COPYING*"))
        for index, file in enumerate(matches):
            if file.is_file():
                shutil.copy2(file, licenses / (name + "-" + str(index) + ".txt"))
    # Keep the exact adapted CLI source and integration wrapper available with the binary.
    adapted = resources / "source"; adapted.mkdir()
    shutil.copy2(patched, adapted / "supercli.py")
    shutil.copy2(patched_utils, adapted / "utils.py")
    for file in (ROOT / "runtime").glob("*.py"):
        shutil.copy2(file, adapted / file.name)
    (resources / "python-packages.txt").write_text(subprocess.check_output([str(python), "-m", "pip", "freeze"], text=True))
    collect_legal(resources, python, copied.keys())
    audit(app)
    for binary in app.rglob("*"):
        if binary != contents / "MacOS/SupCraft" and not binary.is_symlink() and macho(binary):
            run("codesign", "--force", "--sign", "-", "--entitlements", ROOT / "entitlements.plist", binary)
    for framework in sorted(app.rglob("*.framework"), key=lambda path: len(path.parts), reverse=True):
        if framework.is_dir() and not framework.is_symlink():
            run("codesign", "--force", "--sign", "-", framework)
    run("codesign", "--force", "--sign", "-", "--entitlements", ROOT / "entitlements.plist", app)
    run("codesign", "--verify", "--deep", "--strict", app)
    run(python, ROOT / "verify_bundle.py", app)
    run(python, ROOT / "prepare_fixture.py", ROOT / "vendor/native-fixture")
    run("dotnet", "run", "--project", ROOT / "SupCraft.Checks/SupCraft.Checks.csproj", "-c", "Release", "--", "--native", app)
    archive = output / f"SupCraft-macOS-{VERSION}-{arch}.zip"
    run("ditto", "-c", "-k", "--sequesterRsrc", "--keepParent", app, archive)
    dmg_stage = stage / "dmg"; dmg_stage.mkdir(); shutil.copytree(app, dmg_stage / app.name, symlinks=True)
    (dmg_stage / "Applications").symlink_to("/Applications")
    dmg = output / f"SupCraft-macOS-{VERSION}-{arch}.dmg"
    if dmg.exists():
        dmg.unlink()
    run("hdiutil", "create", "-volname", "SupCraft", "-srcfolder", dmg_stage, "-ov", "-format", "UDZO", dmg)
    sums = "".join(hashlib.file_digest(file.open("rb"), "sha256").hexdigest() + "  " + file.name + "\n" for file in (archive, dmg))
    (output / f"SHA256SUMS-macOS-{arch}.txt").write_text(sums)
    print("Built and conversion-tested:", archive, dmg)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=ROOT / "dist")
    build(parser.parse_args().output)
