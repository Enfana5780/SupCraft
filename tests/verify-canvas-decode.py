"""Optional pixel-level portrait SUP check; requires FFmpeg and Pillow."""
from pathlib import Path
import json
import subprocess
import xml.etree.ElementTree as ET
from PIL import Image, ImageChops

root = Path(__file__).resolve().parent.parent
work = root / 'qa' / 'compat-custom-2'
sup = work / 'portrait.sup'
probe = json.loads(subprocess.check_output([
    'ffprobe', '-v', 'error', '-show_entries', 'stream=codec_name,width,height',
    '-of', 'json', str(sup),
], text=True))
assert probe['streams'][0] == {'codec_name': 'hdmv_pgs_subtitle', 'width': 1440, 'height': 3200}
reference = Image.new('RGBA', (1440, 3200), (0, 0, 0, 255))
for graphic in ET.parse(work / 'portrait.xml').findall('.//Event/Graphic'):
    with Image.open(work / graphic.text.strip()) as image:
        reference.alpha_composite(image.convert('RGBA'), (int(graphic.get('X')), int(graphic.get('Y'))))
checks = {}
for timestamp, name, visible in [('0.2', 'decoded.png', True), ('1.5', 'cleared.png', False)]:
    output = work / name
    # FFmpeg normalizes this subtitle input's initial PTS to zero.
    subprocess.run([
        'ffmpeg', '-hide_banner', '-loglevel', 'error', '-f', 'lavfi', '-i',
        'color=c=black:s=1440x3200:r=24:d=3', '-i', str(sup),
        '-filter_complex', '[0:v][1:s]overlay', '-ss', timestamp,
        '-frames:v', '1', '-y', str(output),
    ], check=True)
    with Image.open(output) as image:
        image = image.convert('RGB')
        assert image.size == (1440, 3200)
        for point in [(110, 110), (110, 3010)]:
            pixel = image.getpixel(point)
            assert (min(pixel) > 200) if visible else (max(pixel) < 10), (name, point, pixel)
        if visible:
            # Palette/YCbCr rounding may differ slightly from the RGBA BDN.
            diff = ImageChops.difference(image, reference.convert('RGB'))
            error = max(channel[1] for channel in diff.getextrema())
            assert error <= 3, error
            checks['max_rgb_error_vs_bdn'] = error
        else:
            assert image.getbbox() is None, 'Subtitle did not clear'
        checks[name] = 'passed'
result = {'probe': probe, 'checks': checks,
          'ffmpeg': subprocess.check_output(['ffmpeg', '-version'], text=True).splitlines()[0]}
(work / 'decode-verification.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(result, ensure_ascii=False, indent=2))
