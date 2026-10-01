"""Verify retained source and notice integrity, not legal provenance certification.

SPDX-License-Identifier: 0BSD
"""
import hashlib
import json
from pathlib import Path
import tarfile
import zipfile

ROOT = Path(__file__).resolve().parent.parent
LEGAL = ROOT / 'legal'


def check():
    for entry in json.loads((LEGAL / 'license-index.json').read_text()):
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
    assert 'Permission to use, copy, modify' in (ROOT / 'LICENSE').read_text()
    assert '0BSD' in (ROOT / 'LICENSING.md').read_text(encoding='utf8')
    assert 'GNU GENERAL PUBLIC LICENSE' in (LEGAL / 'licenses/SUPer-GPL-3.0.txt').read_text()
    print(f'PASS: {len(sources)} source archives, complete retained reference notices and independent 0BSD / GPL texts.')


if __name__ == '__main__': check()
