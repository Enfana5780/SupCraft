"""Check architecture, relocatability and that the Mac bundle has no Windows renderer."""
import json
import hashlib
from pathlib import Path
import plistlib
import subprocess
import sys

from bundle_dylibs import audit


def verify(app):
    contents = app / "Contents"
    plist = plistlib.loads((contents / "Info.plist").read_bytes())
    assert plist["CFBundleExecutable"] == "SupCraft"
    assert (contents / "Resources/SupCraft.icns").is_file()
    legal = contents / 'Resources/legal'
    assert (legal / 'licenses/GPL-3.0.txt').is_file()
    assert (legal / 'license-index.json').is_file()
    assert (legal / 'SupCraft-LICENSE.txt').is_file()
    assert (legal / 'SupCraft-runtime-0BSD.txt').is_file()
    assert (legal / 'SOURCE-LOCATION.txt').is_file()
    assert not (contents / 'Resources/README.md').exists()
    assert not (contents / 'Resources/THIRD-PARTY-NOTICES.md').exists()
    assert not (legal / 'upstream-sources').exists()
    assert (contents / 'Resources/VERSION').read_text().strip() == plist['CFBundleShortVersionString']
    assert (legal / 'runtime-materials.json').is_file()
    for line in (legal / 'SHA256SUMS.txt').read_text().splitlines():
        expected, relative = line.split('  ', 1)
        file = (legal / relative).resolve()
        assert file.is_relative_to(legal.resolve()) and file.is_file()
        assert hashlib.sha256(file.read_bytes()).hexdigest() == expected, relative
    sources = json.loads((legal / 'source-index.json').read_text())
    assert len(sources) >= 5 and all(entry['url'] and entry['sha256'] for entry in sources)
    tools = contents / "Resources/tools"
    manifest = json.loads((tools / "toolchain.json").read_text())
    for key in ("renderer", "encoder", "quantizer"):
        path = (tools / manifest[key]).resolve()
        assert path.is_relative_to(app.resolve()) and path.is_file(), path
    for path in app.rglob("*"):
        if path.is_symlink():
            assert path.resolve().is_relative_to(app.resolve()), "Escaping bundle symlink: " + str(path)
        assert path.suffix.lower() not in (".exe", ".avs"), path
        if path.is_relative_to(tools):
            assert not any(name in path.name.lower() for name in ("avisynth", "vsfilter", "avs2bdnxml")), path
    audit(app)
    subprocess.run([str(contents / "MacOS/SupCraft"), "--self-test-ui"], check=True, timeout=45)
    print("PASS: native UI launch, Mac-only toolchain, no external absolute dependencies.")


if __name__ == "__main__":
    verify(Path(sys.argv[1]).resolve())
