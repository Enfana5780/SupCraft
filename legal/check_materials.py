"""Verify retained source and notice integrity, not legal provenance certification.

SPDX-License-Identifier: LicenseRef-SupCraft-Free-Use-No-Resale-1.0
"""
import hashlib
import json
from pathlib import Path
import tarfile
import zipfile

ROOT = Path(__file__).resolve().parent.parent
LEGAL = ROOT / 'legal'


def check():
    catalog = json.loads((LEGAL / 'license-index.json').read_text(encoding='utf8'))
    for entry in catalog:
        assert 'error' not in entry, entry
        file = LEGAL / 'licenses' / entry['file']
        assert hashlib.sha256(file.read_bytes()).hexdigest() == entry['sha256'], file
        assert len(file.read_bytes()) > 150, 'Notice is a pointer, not its full text: ' + str(file)
    sources = json.loads((LEGAL / 'source-index.json').read_text())
    for entry in sources:
        archive = LEGAL / 'upstream-sources' / entry['archive']
        assert hashlib.sha256(archive.read_bytes()).hexdigest() == entry['sha256'], archive
        if archive.name.endswith('.tar.gz'):
            with tarfile.open(archive) as source:
                names = source.getnames()
                assert all(name.split('/')[0] == entry['component'] + '-' + entry['commit'] for name in names)
                assert len(names) > 2
        else:
            with zipfile.ZipFile(archive) as source:
                assert source.testzip() is None
                assert 'avs2bdnxml-2.08/avs2bdnxml.c' in source.namelist()
                assert 'avs2bdnxml-2.08/Makefile' in source.namelist()
    project_license = (ROOT / 'LICENSE').read_text(encoding='utf8')
    assert 'LicenseRef-SupCraft-Free-Use-No-Resale-1.0' in project_license
    assert '商业项目' in project_license and '禁止售卖' in project_license
    assert 'Permission to use, copy, modify' in (LEGAL / 'SUPCRAFT-0BSD.txt').read_text(encoding='utf8')
    assert '禁止售卖或收费分发' in (ROOT / 'README.md').read_text(encoding='utf8')
    assert '0BSD' in (ROOT / 'THIRD-PARTY-NOTICES.md').read_text(encoding='utf8')
    for file in (ROOT / 'macos/runtime').glob('*.py'):
        assert 'SPDX-License-Identifier: 0BSD' in file.read_text(encoding='utf8'), file
    assert 'GNU GENERAL PUBLIC LICENSE' in (LEGAL / 'licenses/GPL-3.0.txt').read_text()
    names = [entry['original_file'] for entry in catalog]
    assert len(names) == len(set(names)), 'Duplicate logical notice entries'
    retained = {file.relative_to(LEGAL / 'licenses').as_posix() for file in (LEGAL / 'licenses').rglob('*') if file.is_file()}
    assert retained == {entry['file'] for entry in catalog}, 'Notice catalog does not cover retained files'
    assert len(retained) == len({entry['sha256'] for entry in catalog}), 'Identical notice bytes stored more than once'
    print(f'PASS: {len(sources)} source archives, {len(catalog)} component notices in {len(retained)} files, project no-resale license and preserved 0BSD / GPL exceptions.')


if __name__ == '__main__': check()
