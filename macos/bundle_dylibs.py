"""Relocate external Mach-O dependencies into the app; never require Homebrew at runtime."""
import os
from pathlib import Path
import shutil
import subprocess


def run(*args):
    return subprocess.check_output([str(arg) for arg in args], text=True)


def macho(path):
    if not path.is_file():
        return False
    with path.open("rb") as stream:
        return stream.read(4) in (b"\xcf\xfa\xed\xfe", b"\xce\xfa\xed\xfe", b"\xca\xfe\xba\xbe", b"\xbe\xba\xfe\xca")


def dependencies(path):
    identities = {line.strip() for line in run("otool", "-D", path).splitlines()[1:] if line.startswith("\t") or line.startswith(("/", "@"))}
    return [line.strip().split(" (", 1)[0] for line in run("otool", "-L", path).splitlines()[1:] if line.startswith("\t") and line.strip().split(" (", 1)[0] not in identities]


def relocate(binary, library_directory, copied=None):
    copied = {} if copied is None else copied
    library_directory.mkdir(parents=True, exist_ok=True)
    for name in dependencies(binary):
        if not name.startswith("/") or name.startswith(("/System/Library/", "/usr/lib/")):
            continue
        source = Path(name).resolve()
        if source == binary.resolve():
            continue
        if source not in copied:
            target = library_directory / source.name
            if target.exists() and target.resolve() != source:
                if target.read_bytes() != source.read_bytes():
                    raise RuntimeError("Different libraries share a filename: " + str(source))
            else:
                shutil.copy2(source, target)
            copied[source] = target
            run("install_name_tool", "-id", "@rpath/" + target.name, target)
            relocate(target, library_directory, copied)
        target = copied[source]
        relative = os.path.relpath(target, binary.parent)
        run("install_name_tool", "-change", name, "@loader_path/" + relative, binary)
    return copied


def audit(app):
    for path in app.rglob("*"):
        if path.is_symlink() or not macho(path):
            continue
        for name in dependencies(path):
            if name.startswith("/") and not name.startswith(("/System/Library/", "/usr/lib/")):
                raise RuntimeError("External runtime dependency: " + str(path) + " -> " + name)
