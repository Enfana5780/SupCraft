"""Generate a uniquely named, uninstalled font from a local font for conversion tests."""
from pathlib import Path
import struct
import sys


def fixture(root):
    root.mkdir(parents=True, exist_ok=True)
    candidates = [Path("/System/Library/Fonts/Supplemental/Arial Black.ttf"), Path("/System/Library/Fonts/Supplemental/Arial.ttf"), Path("C:/Windows/Fonts/impact.ttf")]
    source = next((font for font in candidates if font.is_file()), None)
    if source is None:
        raise RuntimeError("No local TTF test font found")
    data = source.read_bytes()
    count = struct.unpack_from(">H", data, 4)[0]
    tables = {}
    for i in range(count):
        tag, _, offset, size = struct.unpack_from(">4sIII", data, 12 + 16 * i)
        tables[tag] = bytearray(data[offset:offset + size])
    name = tables[b"name"]
    _, names, start = struct.unpack_from(">HHH", name)
    records, strings = [], bytearray()
    for i in range(names):
        platform, encoding, language, ident, length, offset = struct.unpack_from(">HHHHHH", name, 6 + 12 * i)
        value = name[start + offset:start + offset + length]
        if ident in (1, 3, 4, 6, 16):
            value = "SupCraftMacFixture".encode("utf-16-be" if platform in (0, 3) else "ascii")
        records.append(struct.pack(">HHHHHH", platform, encoding, language, ident, len(value), len(strings)))
        strings.extend(value)
    tables[b"name"] = bytearray(struct.pack(">HHH", 0, names, 6 + names * 12) + b"".join(records) + strings)
    struct.pack_into(">I", tables[b"head"], 8, 0)
    output = bytearray(data[:12]); output.extend(b"\0" * count * 16)
    def checksum(blob):
        padded = bytes(blob) + b"\0" * (-len(blob) % 4)
        return sum(struct.unpack(">" + "I" * (len(padded) // 4), padded)) & 0xffffffff
    head_offset = 0
    for i, (tag, blob) in enumerate(tables.items()):
        offset = len(output); output.extend(blob); output.extend(b"\0" * (-len(output) % 4))
        struct.pack_into(">4sIII", output, 12 + 16 * i, tag, checksum(blob), offset, len(blob))
        if tag == b"head":
            head_offset = offset
    struct.pack_into(">I", output, head_offset + 8, (0xb1b0afba - checksum(output)) & 0xffffffff)
    fonts = root / "字体目录 测试" / "nested"
    fonts.mkdir(parents=True, exist_ok=True)
    (fonts / "测试字体.TTF").write_bytes(output)
    (root / "fixture.ass").write_text("""[Script Info]
ScriptType: v4.00+
PlayResX: 1920
PlayResY: 1080
[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Default,SupCraftMacFixture,62,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,30,30,40,1
[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
Dialogue: 0,0:00:00.00,0:00:00.00,Default,,0,0,0,,
Dialogue: 0,0:00:00.50,0:00:02.00,Default,,0,0,0,,SupCraft WMWM 012345
Dialogue: 0,0:00:02.20,0:00:03.20,Default,,0,0,0,,{\\fad(50,50)}Tail preserved
""", encoding="utf-8")
    print("Created local-only test font:", fonts)


if __name__ == "__main__":
    fixture(Path(sys.argv[1]).resolve())
