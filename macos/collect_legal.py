"""Collect the actual build's notices, package records and upstream source.

SPDX-License-Identifier: LicenseRef-SupCraft-Free-Use-No-Resale-1.0
This inventory records unresolved native provenance; it does not certify it.
"""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import urllib.request

PROJECT = Path(__file__).resolve().parent.parent
DOCS = ("VERSION",)


def notice(path):
    return path.is_file() and any(word in path.name.upper() for word in ("LICENSE", "LICENCE", "COPYING", "COPYRIGHT", "NOTICE", "AUTHORS"))


def collect(resources, python, native_paths):
    legal = resources / 'legal'
    shutil.copytree(PROJECT / 'legal/licenses', legal / 'licenses', dirs_exist_ok=True)
    shutil.copy2(PROJECT / 'legal/license-index.json', legal / 'license-index.json')
    shutil.copy2(PROJECT / 'LICENSE', legal / 'SupCraft-LICENSE.txt')
    shutil.copy2(PROJECT / 'legal/SUPCRAFT-0BSD.txt', legal / 'SupCraft-runtime-0BSD.txt')
    pins = json.loads((PROJECT / 'macos/upstream-pins.json').read_text(encoding='utf8'))
    sources = json.loads((PROJECT / 'legal/source-index.json').read_text(encoding='utf8'))
    (legal / 'source-index.json').write_text(json.dumps([entry for entry in sources if entry['component'] in pins], indent=2) + '\n', encoding='utf8')
    (legal / 'SOURCE-LOCATION.txt').write_text('Third-party source and build materials: https://github.com/Enfana5780/SupCraft/tree/main/legal\n', encoding='utf8')
    for name in DOCS:
        shutil.copy2(PROJECT / name, resources / name)
    records = {'schema': 1, 'generated_utc': datetime.now(timezone.utc).isoformat(), 'python': [], 'nuget': [], 'native': [], 'unresolved': []}
    query = r'''
import importlib.metadata as m,json,sys
result=[]
for d in m.distributions():
 result.append({'name':d.metadata['Name'],'version':d.version,'license':d.metadata.get('License-Expression') or d.metadata.get('License'),'project_urls':d.metadata.get_all('Project-URL') or [],'root':str(d.locate_file('')),'files':[str(f) for f in d.files or []]})
print(json.dumps({'version':sys.version.split()[0],'prefix':sys.prefix,'distributions':result}))
'''
    metadata = json.loads(subprocess.check_output([str(python), '-c', query], text=True))
    prefix = Path(metadata['prefix']).resolve()
    for entry in metadata['distributions']:
        name, version = entry['name'], entry['version']
        target = legal / 'licenses/python' / (re.sub(r'[^A-Za-z0-9_.-]', '_', name) + '-' + version)
        collected = []
        root = Path(entry.pop('root'))
        for filename in entry.pop('files'):
            source = (root / filename).resolve()
            if notice(source) and source.is_relative_to(prefix):
                dest = target / filename.replace('../', '__parent__/')
                dest.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(source, dest)
                collected.append(str(dest.relative_to(legal)))
        entry['notices'] = collected
        entry['pypi_metadata'] = f'https://pypi.org/pypi/{name}/{version}/json'
        records['python'].append(entry)
        if not collected: records['unresolved'].append(f'No license file collected from installed Python distribution: {name} {version}')
    version = metadata['version']
    python_url = f'https://raw.githubusercontent.com/python/cpython/v{version}/LICENSE'
    with urllib.request.urlopen(python_url, timeout=45) as response:
        (legal / 'licenses/Python-runtime-PSF.txt').write_bytes(response.read())
    records['python_runtime'] = {'version': version, 'license_url': python_url, 'source_url': f'https://github.com/python/cpython/tree/v{version}'}

    assets = json.loads((PROJECT / 'macos/SupCraft.Mac/obj/project.assets.json').read_text())
    package_roots = [Path(root) for root in assets['packageFolders']]
    for identifier, entry in assets['libraries'].items():
        if entry['type'] != 'package': continue
        folder = next((root / entry['path'] for root in package_roots if (root / entry['path']).is_dir()), None)
        collected = []
        if folder:
            for source in folder.rglob('*'):
                if notice(source):
                    dest = legal / 'licenses/nuget' / entry['path'] / source.relative_to(folder)
                    dest.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(source, dest)
                    collected.append(str(dest.relative_to(legal)))
        records['nuget'].append({'package': identifier, 'notices': collected, 'package_url': 'https://www.nuget.org/packages/' + identifier})
        if not collected: records['unresolved'].append('No embedded notice file in NuGet package: ' + identifier + '; consult package license metadata and retained references')

    # INSTALL_RECEIPT and the bottle's own formula are closer to the actual binary
    # than today's mutable Homebrew API. Preserve both without claiming to have
    # rebuilt an embedded wheel library with that formula.
    cellars = set()
    for native in native_paths:
        path = Path(native)
        match = re.search(r'/Cellar/([^/]+)/([^/]+)/', str(path))
        if match:
            cellars.add(Path(str(path).split('/Cellar/', 1)[0] + '/Cellar/' + match[1] + '/' + match[2]))
    for cellar in sorted(cellars):
        target = legal / 'native-build-records' / cellar.parent.name / cellar.name
        target.mkdir(parents=True, exist_ok=True)
        files = []
        for source in [cellar / 'INSTALL_RECEIPT.json', *cellar.glob('.brew/*.rb')]:
            if source.is_file():
                shutil.copy2(source, target / source.name); files.append(source.name)
        for source in cellar.rglob('*'):
            if notice(source):
                dest = target / 'notices' / source.relative_to(cellar)
                dest.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(source, dest)
        records['native'].append({'formula': cellar.parent.name, 'version': cellar.name, 'build_records': files})

    # Wheel-embedded FFmpeg can differ from the installed Homebrew formulas.
    ffmpeg = []
    for binary in resources.glob('tools/super/_internal/cv2/.dylibs/libavcodec*.dylib'):
        data = binary.read_bytes(); pos = data.find(b'--enable-gpl')
        if pos >= 0:
            start = data.rfind(b'\0', max(0, pos - 4000), pos) + 1; end = data.find(b'\0', pos)
            ffmpeg.append({'binary': str(binary.relative_to(resources)), 'sha256': hashlib.sha256(data).hexdigest(), 'configuration': data[start:end].decode('utf8', errors='replace')})
    records['wheel_ffmpeg'] = ffmpeg
    if ffmpeg: records['unresolved'].append('Wheel embeds GPL FFmpeg and codec libraries; exact historical dependency source/build records must be verified separately (see SOURCES.md)')
    records['unresolved'].append('This inventory is not a complete corresponding-source certification for all native transitive libraries.')
    (legal / 'runtime-materials.json').write_text(json.dumps(records, indent=2) + '\n', encoding='utf8')
    # Record every material without including machine-specific caches or fonts.
    sums = []
    for file in sorted(legal.rglob('*')):
        if file.is_file() and file.name != 'SHA256SUMS.txt':
            sums.append(hashlib.sha256(file.read_bytes()).hexdigest() + '  ' + str(file.relative_to(legal)))
    (legal / 'SHA256SUMS.txt').write_text('\n'.join(sums) + '\n', encoding='utf8')
    print(f'Collected legal materials: {len(records["python"])} Python and {len(records["nuget"])} NuGet distributions, {len(records["native"])} native formula records.')
