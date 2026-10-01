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
