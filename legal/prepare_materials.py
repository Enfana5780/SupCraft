"""Refresh retained upstream licenses and immutable source archives.

SPDX-License-Identifier: LicenseRef-SupCraft-Free-Use-No-Resale-1.0
Existing files are reused; --refresh deliberately re-fetches reference notices.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
from pathlib import Path
import shutil
import tarfile
import urllib.request
import zipfile
from notice_catalog import compact_notices, restore_notices

ROOT = Path(__file__).resolve().parent.parent
LEGAL = ROOT / "legal"
REFERENCES = {
    "libass-ISC.txt": "https://raw.githubusercontent.com/libass/libass/master/COPYING",
    "libimagequant-COPYRIGHT.txt": "https://raw.githubusercontent.com/ImageOptim/libimagequant/main/COPYRIGHT",
    "VSFilterMod-upstream-GPL-3.0.txt": "https://raw.githubusercontent.com/sorayuki/VSFilterMod/master/LICENSE",
    "Avalonia-MIT.txt": "https://raw.githubusercontent.com/AvaloniaUI/Avalonia/master/licence.md",
    "PyInstaller-COPYING.txt": "https://raw.githubusercontent.com/pyinstaller/pyinstaller/v6.16.0/COPYING.txt",
    "Python-PSF.txt": "https://raw.githubusercontent.com/python/cpython/3.11/LICENSE",
    "GLib-COPYING.txt": "https://raw.githubusercontent.com/GNOME/glib/main/LICENSES/LGPL-2.1-or-later.txt",
    "FriBidi-COPYING.txt": "https://raw.githubusercontent.com/fribidi/fribidi/master/COPYING",
    "FreeType-FTL.txt": "https://raw.githubusercontent.com/freetype/freetype/master/docs/FTL.TXT",
    "HarfBuzz-COPYING.txt": "https://raw.githubusercontent.com/harfbuzz/harfbuzz/main/COPYING",
    "Fontconfig-COPYING.txt": "https://raw.githubusercontent.com/fontconfig/fontconfig/main/COPYING",
    "Graphite2-COPYING.txt": "https://raw.githubusercontent.com/silnrsi/graphite/master/COPYING",
    "PCRE2-LICENCE.txt": "https://raw.githubusercontent.com/PCRE2Project/pcre2/main/LICENCE.md",
    "libunibreak-LICENCE.txt": "https://raw.githubusercontent.com/adah1972/libunibreak/master/LICENCE",
    "libpng-LICENSE.txt": "https://raw.githubusercontent.com/pnggroup/libpng/libpng16/LICENSE",
    "zlib-LICENSE.txt": "https://raw.githubusercontent.com/madler/zlib/master/LICENSE",
    "Brotli-LICENSE.txt": "https://raw.githubusercontent.com/google/brotli/master/LICENSE",
    "Expat-COPYING.txt": "https://raw.githubusercontent.com/libexpat/libexpat/master/COPYING",
    "libiconv-COPYING-LIB.txt": "https://raw.githubusercontent.com/arthenica/libiconv/master/COPYING.LIB",
    "gettext-COPYING-LIB.txt": "https://raw.githubusercontent.com/autotools-mirror/gettext/master/gettext-runtime/intl/COPYING.LIB",
    "FFmpeg-COPYING-GPLv3.txt": "https://raw.githubusercontent.com/FFmpeg/FFmpeg/n7.1.1/COPYING.GPLv3",
    "FFmpeg-COPYING-LGPLv2.1.txt": "https://raw.githubusercontent.com/FFmpeg/FFmpeg/n7.1.1/COPYING.LGPLv2.1",
    "Skia-LICENSE.txt": "https://raw.githubusercontent.com/google/skia/main/LICENSE",
    "Numba-LICENSE.txt": "https://raw.githubusercontent.com/numba/numba/0.61.2/LICENSE",
    "llvmlite-LICENSE.txt": "https://raw.githubusercontent.com/numba/llvmlite/v0.44.0/LICENSE",
    "Pillow-LICENSE.txt": "https://raw.githubusercontent.com/python-pillow/Pillow/11.3.0/LICENSE",
    "OpenCV-LICENSE.txt": "https://raw.githubusercontent.com/opencv/opencv/4.12.0/LICENSE",
    "opencv-python-LICENSE.txt": "https://raw.githubusercontent.com/opencv/opencv-python/4.x/LICENSE.txt",
    "opencv-python-THIRD-PARTY.txt": "https://raw.githubusercontent.com/opencv/opencv-python/4.x/LICENSE-3RD-PARTY.txt",
    "tqdm-LICENCE.txt": "https://raw.githubusercontent.com/tqdm/tqdm/v4.67.1/LICENCE",
}


def download(url, target):
    request = urllib.request.Request(url, headers={"User-Agent": "SupCraft-license-materials"})
    with urllib.request.urlopen(request, timeout=45) as response:
        data = response.read()
    if not data or data.lstrip().lower().startswith(b"<!doctype html"):
        raise ValueError("Expected a license or source file: " + url)
    target.write_bytes(data)


def prepare(refresh=False):
    licenses = LEGAL / "licenses"; licenses.mkdir(parents=True, exist_ok=True)
    catalog_path = LEGAL / 'license-index.json'
    previous = json.loads(catalog_path.read_text(encoding='utf8')) if catalog_path.exists() else []
    restore_notices(licenses, previous)
    source_dir = LEGAL / "upstream-sources"; source_dir.mkdir(exist_ok=True)
    pins = json.loads((ROOT / "macos/upstream-pins.json").read_text())
    index = []
    for name, sha in pins.items():
        url = f"https://codeload.github.com/cubicibo/{name}/tar.gz/{sha}"
        archive = source_dir / f"{name}-{sha}.tar.gz"
        if not archive.exists():
            cached = ROOT / "macos/vendor" / (name + ".tar.gz")
            if cached.exists():
                with tarfile.open(cached) as source:
                    assert all(item.name.split('/')[0] == name + '-' + sha for item in source.getmembers())
                shutil.copy2(cached, archive)
            else:
                download(url, archive)
        with tarfile.open(archive) as source:
            members = source.getmembers()
            assert all(item.name.split('/')[0] == name + '-' + sha for item in members)
            for member in members:
                if not member.isfile():
                    continue
                relative = member.name.split('/', 1)[-1]
                if relative == 'LICENSE':
                    target = licenses / (name + ('-GPL-3.0.txt' if name == 'SUPer' else '-MIT.txt'))
                    target.write_bytes(source.extractfile(member).read())
                if name == 'ass2bdnxml' and relative == 'ass2bdnxml.c':
                    text = source.extractfile(member).read().decode('utf8')
                    (licenses / 'ass2bdnxml-ISC.txt').write_text(text.split('#include', 1)[0], encoding='utf8')
        index.append({'component': name, 'commit': sha, 'url': url, 'archive': archive.name, 'sha256': hashlib.sha256(archive.read_bytes()).hexdigest()})
    shutil.copy2(ROOT / 'third_party/avs2bdnxml/COPYING', licenses / 'avs2bdnxml-GPL-3.0.txt')
    shutil.copy2(ROOT / 'third_party/SupMover/LICENSE', licenses / 'SupMover-AGPL-3.0.txt')
    shutil.copy2(licenses / 'SUPer-GPL-3.0.txt', licenses / 'GPL-3.0.txt')
    # Retain the source actually supplied with the Windows renderer; do not
    # substitute a same-version download or imply its .a dependencies are source.
    bundled = source_dir / 'avs2bdnxml-2.08-bundled-src.zip'
    base = ROOT / 'third_party/avs2bdnxml'
    with zipfile.ZipFile(bundled, 'w', zipfile.ZIP_DEFLATED) as archive:
        for file in sorted(base.rglob('*')):
            if file.is_file() and (file.suffix.lower() in ('.c', '.h', '.asm', '.md') or file.name.startswith('Makefile') or file.name == 'COPYING'):
                archive.write(file, 'avs2bdnxml-2.08/' + file.relative_to(base).as_posix())
    index.append({'component': 'avs2bdnxml', 'version': '2.08', 'provenance': 'Source supplied with the existing third_party directory; exact static library build still unverified', 'archive': bundled.name, 'sha256': hashlib.sha256(bundled.read_bytes()).hexdigest()})

    def fetch(item):
        name, url = item; target = licenses / name
        try:
            if refresh or not target.exists(): download(url, target)
            return {'file': name, 'url': url, 'sha256': hashlib.sha256(target.read_bytes()).hexdigest()}
        except Exception as error:
            return {'file': name, 'url': url, 'error': str(error)}
    with ThreadPoolExecutor(max_workers=8) as pool:
        references = list(pool.map(fetch, REFERENCES.items()))
    (LEGAL / 'source-index.json').write_text(json.dumps(index, indent=2) + '\n', encoding='utf8')
    failures = [item for item in references if 'error' in item]
    for item in failures: print('MISSING:', item['file'], item['error'])
    print(f"Prepared {len(pins)} complete pinned upstream archives, bundled avs2bdnxml source and {len(references) - len(failures)} reference notices.")
    if failures: raise SystemExit(1)
    catalog = compact_notices(licenses, previous + references)
    catalog_path.write_text(json.dumps(catalog, indent=2) + '\n', encoding='utf8')
    print(f"Catalogued {len(catalog)} component notices in {len({entry['file'] for entry in catalog})} distinct files.")


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('--refresh', action='store_true')
    prepare(parser.parse_args().refresh)
