"""Store identical notice bytes once, retaining every original component path.

SPDX-License-Identifier: LicenseRef-SupCraft-Free-Use-No-Resale-1.0
"""
import hashlib
from pathlib import Path
import shutil


PREFERRED = (
    'GPL-3.0.txt', 'avs2bdnxml-GPL-3.0.txt', 'FriBidi-COPYING.txt',
    'ScenariStream-MIT.txt',
    'nuget/skiasharp.nativeassets.macos/3.119.4/LICENSE.txt',
    'nuget/skiasharp.nativeassets.macos/3.119.4/THIRD-PARTY-NOTICES.txt',
)


def within(directory, name):
    path = directory / name
    if not path.resolve().is_relative_to(directory.resolve()):
        raise ValueError('Notice path leaves its directory: ' + name)
    return path


def restore_notices(directory, entries):
    """Recreate aliases for an upstream refresh, without downloading old bytes."""
    for entry in entries:
        name = entry.get('original_file', entry['file'])
        target = within(directory, name)
        source = within(directory, entry['file'])
        if target.exists() or not source.is_file():
            continue
        if hashlib.sha256(source.read_bytes()).hexdigest() != entry['sha256']:
            raise ValueError('Retained notice hash mismatch: ' + entry['file'])
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)


def compact_notices(directory, entries):
    """Return an index for all logical notices; remove only byte-identical files."""
    restore_notices(directory, entries)
    metadata = {entry.get('original_file', entry['file']): dict(entry) for entry in entries}
    groups = {}
    for file in sorted(directory.rglob('*')):
        if file.is_file():
            digest = hashlib.sha256(file.read_bytes()).hexdigest()
            groups.setdefault(digest, []).append(file.relative_to(directory).as_posix())
    result = []
    for digest, names in groups.items():
        canonical = next((name for name in PREFERRED if name in names), names[0])
        for name in names:
            entry = metadata.get(name, {})
            entry.update(original_file=name, file=canonical, sha256=digest)
            result.append(entry)
            if name != canonical:
                duplicate = within(directory, name)
                # Check the original again immediately before removing its alias.
                if duplicate.read_bytes() != within(directory, canonical).read_bytes():
                    raise ValueError('Notice changed while compacting: ' + name)
                duplicate.unlink()
    for path in sorted(directory.rglob('*'), key=lambda p: len(p.parts), reverse=True):
        if path.is_dir() and not any(path.iterdir()):
            within(directory, path.relative_to(directory).as_posix()).rmdir()
    return sorted(result, key=lambda entry: entry['original_file'])
