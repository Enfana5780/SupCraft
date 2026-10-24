using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StandaloneAssRocket {
    // Custom canvases are deliberately confined to the Windows AviSynth path.
    static class CustomCanvas {
        public const int Minimum=64,Maximum=4096;
        public static void ValidateSize(int width,int height){
            if(width<Minimum||width>Maximum||height<Minimum||height>Maximum)throw new ConversionException("自定义画布","宽高必须为 "+Minimum+"–"+Maximum+" 的整数。","调整自定义宽高。");
            // The bundled SSE2 loops advance 16 bytes and stop only on equality.
            // A frame must contain a multiple of four RGBA pixels; the UI's
            // stricter per-dimension alignment also keeps sizes easy to validate.
            if(width%4!=0||height%4!=0)throw new ConversionException("自定义画布","宽度和高度必须分别为 4 的倍数。","调整宽高，例如 1440×3200。");
        }
        static void Invalid(string details){throw new ConversionException("自定义画布验证","输出尺寸或坐标与所选画布不一致。","保留日志并检查转换核心输出。",details);}
        static int Number(System.Xml.XmlNode node,string name){int value;var a=node.Attributes[name];if(a==null||!int.TryParse(a.Value,out value)){Invalid("缺少或无效的 "+name);return 0;}return value;}
        static int Word(byte[] data,int offset){return data[offset]*256+data[offset+1];}
        static void Rectangle(int x,int y,int w,int h,int width,int height){if(x<0||y<0||w<=0||h<=0||(long)x+w>width||(long)y+h>height)Invalid("越界图形："+x+","+y+" "+w+"×"+h+"；画布 "+width+"×"+height);}
        public static void ValidateOutput(string path,int width,int height){
            ValidateSize(width,height);
            if(path.EndsWith(".xml",StringComparison.OrdinalIgnoreCase)){
                var d=new System.Xml.XmlDocument();d.Load(path);var format=d.SelectSingleNode("//Format");
                if(format==null||format.Attributes["VideoFormat"]==null||format.Attributes["VideoFormat"].Value!=width+"x"+height)Invalid("BDN VideoFormat 不匹配。");
                var graphics=d.SelectNodes("//Graphic");if(graphics.Count==0)Invalid("没有字幕图形。");
                foreach(System.Xml.XmlNode g in graphics){int w=Number(g,"Width"),h=Number(g,"Height");Rectangle(Number(g,"X"),Number(g,"Y"),w,h,width,height);using(var image=System.Drawing.Image.FromFile(Path.Combine(Path.GetDirectoryName(path),g.InnerText.Trim())))if(image.Width!=w||image.Height!=h)Invalid("PNG 与 XML 图形尺寸不匹配。");}
            }else{
                byte[] data=File.ReadAllBytes(path);int offset=0,compositions=0;
                while(offset<data.Length){
                    if(data.Length-offset<13||data[offset]!='P'||data[offset+1]!='G')Invalid("SUP 段头损坏。");
                    int length=Word(data,offset+11),start=offset+13;byte kind=data[offset+10];
                    if(length>data.Length-start)Invalid("SUP 段不完整。");
                    if(kind==0x16){if(length<11||Word(data,start)!=width||Word(data,start+2)!=height)Invalid("SUP PCS 画布不匹配。");compositions++;}
                    if(kind==0x17){if(length<1||length!=1+data[start]*9)Invalid("SUP WDS 不完整。");for(int i=0;i<data[start];i++){int p=start+1+i*9;Rectangle(Word(data,p+1),Word(data,p+3),Word(data,p+5),Word(data,p+7),width,height);}}
                    if(kind==0x15){if(length<4)Invalid("SUP ODS 不完整。");if((data[start+3]&0x80)!=0){if(length<11)Invalid("SUP ODS 缺少尺寸。");Rectangle(0,0,Word(data,start+7),Word(data,start+9),width,height);}}
                    offset=start+length;
                }
                if(compositions==0)Invalid("SUP 缺少画布信息。");
            }
        }
    }

    // Public session fonts are required: FR_PRIVATE fonts cannot reach avs2bdnxml.
    sealed class TemporaryFonts : IDisposable {
        [DllImport("gdi32.dll", CharSet=CharSet.Unicode, ExactSpelling=true)]
        static extern int AddFontResourceW(string path);
        [DllImport("gdi32.dll", CharSet=CharSet.Unicode, ExactSpelling=true)]
        static extern bool RemoveFontResourceW(string path);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)]
        static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wparam, IntPtr lparam, uint flags, uint timeout, out UIntPtr result);
        readonly List<string> loaded = new List<string>();
        public readonly List<string> Failed = new List<string>();
        public int LoadedCount { get { return loaded.Count; } }
        public TemporaryFonts(IEnumerable<string> files) {
            try {
                foreach(string path in files) {
                    if(AddFontResourceW(path)>0) loaded.Add(path); else Failed.Add(path);
                }
                if(loaded.Count>0) Notify();
            } catch { Dispose(); throw; }
        }
        static void Notify() {
            UIntPtr result;
            SendMessageTimeout(new IntPtr(0xffff),0x001d,UIntPtr.Zero,IntPtr.Zero,2,100,out result);
        }
        public void Dispose() {
            bool changed=loaded.Count>0;
            foreach(string path in loaded) RemoveFontResourceW(path);
            loaded.Clear();
            if(changed) Notify();
        }
    }

    static class FontDirectory {
        public static string[] ReadFiles(string directory) {
            if(String.IsNullOrWhiteSpace(directory)) return new string[0];
            if(!Directory.Exists(directory)) throw new ConversionException("字体目录","所选字体目录不存在。","重新选择字体目录。",directory);
            try {
                return Directory.GetFiles(directory,"*",SearchOption.AllDirectories)
                    .Where(p=>new[]{".ttf",".otf",".ttc"}.Contains(Path.GetExtension(p).ToLowerInvariant()))
                    .OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray();
            } catch(Exception e) {
                throw new ConversionException("字体目录","无法读取字体目录。","检查目录和子目录的读取权限。",e.ToString());
            }
        }
        public static string Stage(IEnumerable<string> files,string work) {
            string directory=Path.Combine(work,"fonts");Directory.CreateDirectory(directory);
            int index=0;
            foreach(string file in files) File.Copy(file,Path.Combine(directory,(++index).ToString("D5")+Path.GetExtension(file)),true);
            return directory;
        }
    }

    // SUPer v0.2.8 reads config.ini next to its executable, NOT from cwd.
    // An exclusive lease serializes different SubForge instances and restores
    // the exact original config bytes after the encoder has exited.
    sealed class SuperConfigLease : IDisposable {
        FileStream lease;
        readonly string path;
        readonly byte[] original;
        SuperConfigLease(FileStream stream,string file,bool gpu) {
            lease=stream;path=file;original=File.ReadAllBytes(file);
            try {
                File.WriteAllText(path,Configure(Encoding.UTF8.GetString(original).TrimStart('\ufeff'),gpu),new UTF8Encoding(false));
            } catch { try { File.WriteAllBytes(path,original); } finally { lease.Dispose();lease=null; } throw; }
        }
        public static string Configure(string text,bool gpu) {
            var section=Regex.Match(text,@"(?ims)^\[SUPer\][ \t]*\r?\n(?<body>.*?)(?=^\[|\z)");
            string value="use_gpu="+(gpu?"1":"0");
            if(!section.Success) return text.TrimEnd()+"\r\n\r\n[SUPer]\r\n"+value+"\r\n";
            string body=section.Groups["body"].Value;
            if(Regex.IsMatch(body,@"(?im)^\s*use_gpu\s*=")) body=Regex.Replace(body,@"(?im)^[ \t]*use_gpu[ \t]*=[^\r\n]*",value);
            else body=value+"\r\n"+body;
            var group=section.Groups["body"];
            return text.Substring(0,group.Index)+body+text.Substring(group.Index+group.Length);
        }
        public static async Task<SuperConfigLease> Acquire(string directory,bool gpu,CancellationToken token) {
            string config=Path.Combine(directory,"config.ini");
            if(!File.Exists(config)) throw new ConversionException("SUPer 配置","缺少 config.ini。","重新解压完整运行包。",config);
            FileStream stream;
            while(true) {
                token.ThrowIfCancellationRequested();
                try { stream=new FileStream(Path.Combine(directory,"subforge-config.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);break; }
                catch(IOException e) { int code=e.HResult&0xffff;if(code!=32&&code!=33)throw; }
                await Task.Delay(150,token);
            }
            try { token.ThrowIfCancellationRequested();return new SuperConfigLease(stream,config,gpu); } catch { stream.Dispose();throw; }
        }
        public void Dispose() {
            if(lease==null)return;
            try { File.WriteAllBytes(path,original); } finally { lease.Dispose();lease=null; }
        }
    }
}
