"""Create a unique, uninstalled font from a local Windows font, for local QA only."""
from pathlib import Path
import struct

root=Path(__file__).resolve().parent.parent
qa=root/'qa'
fonts=qa/'字体目录 测试'/'子目录'
fonts.mkdir(parents=True,exist_ok=True)
source=Path('C:/Windows/Fonts/impact.ttf')
if not source.exists(): source=Path('C:/Windows/Fonts/arialbd.ttf')
b=bytearray(source.read_bytes())
n=struct.unpack_from('>H',b,4)[0]
records={b[12+i*16:16+i*16].decode('ascii'):(12+i*16,*struct.unpack_from('>III',b,16+i*16)) for i in range(n)}
entry,_,offset,length=records['name']
old=b[offset:offset+length]
_,count,storage=struct.unpack_from('>HHH',old)
data=bytearray(); entries=[]
for i in range(count):
    platform,enc,lang,ident,size,pos=struct.unpack_from('>HHHHHH',old,6+i*12)
    value=old[storage+pos:storage+pos+size]
    if ident in (1,4,6,16,21):
        value='SubForgeFixture'.encode('utf-16-be' if platform in (0,3) else 'ascii')
    entries.append(struct.pack('>HHHHHH',platform,enc,lang,ident,len(value),len(data)))
    data.extend(value)
table=struct.pack('>HHH',0,count,6+12*count)+b''.join(entries)+data
def checksum(data):
    data=bytes(data)+b'\0'*((-len(data))%4)
    return sum(struct.unpack('>'+str(len(data)//4)+'I',data))&0xffffffff
b.extend(b'\0'*((-len(b))%4));new_offset=len(b);b.extend(table);b.extend(b'\0'*((-len(b))%4))
struct.pack_into('>III',b,entry+4,checksum(table),new_offset,len(table))
head=records['head'][2]
struct.pack_into('>I',b,head+8,0)
struct.pack_into('>I',b,head+8,(0xb1b0afba-checksum(b))&0xffffffff)
(fonts/'专用字体.ttf').write_bytes(b)
ass='''[Script Info]
ScriptType: v4.00+
PlayResX: 1280
PlayResY: 720
[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Default,SubForgeFixture,58,&H00FFFFFF,&H000000FF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,2,0,2,30,30,40,1
[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
Dialogue: 0,0:00:00.00,0:00:00.00,Default,,0,0,0,,
Dialogue: 0,0:00:00.50,0:00:01.60,Default,,0,0,0,,Subtitle converter
Dialogue: 0,0:00:01.80,0:00:02.80,Default,,0,0,0,,SubForge font test
'''
(qa/'fixture.ass').write_text(ass,encoding='utf-8-sig')
print('Prepared a local-only font fixture at',fonts)
