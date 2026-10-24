"""Portrait fixture with graphics above and below the former 1080p boundary."""
from pathlib import Path

root = Path(__file__).resolve().parent.parent
work = root / 'qa' / 'canvas-probe'
work.mkdir(parents=True, exist_ok=True)
ass = r'''[Script Info]
ScriptType: v4.00+
PlayResX: 1440
PlayResY: 3200
[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Default,Arial,64,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,0,0,7,0,0,0,1
[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
Dialogue: 0,0:00:00.25,0:00:01.25,Default,,0,0,0,,{\an7\pos(100,100)\p1}m 0 0 l 120 0 120 60 0 60
Dialogue: 0,0:00:00.25,0:00:01.25,Default,,0,0,0,,{\an7\pos(100,3000)\p1}m 0 0 l 120 0 120 60 0 60
Dialogue: 0,0:00:00.25,0:00:01.25,Default,,0,0,0,,{\an5\pos(720,1600)}Portrait canvas
'''
(work / 'portrait.ass').write_text(ass, encoding='utf-8-sig')
script = 'LoadPlugin("' + (root / 'third_party/VSFilterMod/S-VSFilterMod.dll').as_posix() + '")\n'
script += 'MaskSubMod("' + (work / 'portrait.ass').as_posix() + '", 1440, 3200, 24, 78)\n'
(work / 'render.avs').write_text(script, encoding='utf-8')
print(work)
