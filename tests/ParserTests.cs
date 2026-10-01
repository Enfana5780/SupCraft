using System;
using System.IO;
using System.Linq;
using System.Text;
using StandaloneAssRocket;

static class ParserTests {
    const string Header="[Script Info]\r\nPlayResX: 1280\r\nPlayResY: 720\r\n[Events]\r\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\r\n";
    static string root;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static string Write(string name,string events,Encoding encoding){string path=Path.Combine(root,name+".ass");File.WriteAllText(path,Header+events,encoding);return path;}
    static void Reject(string name,string events){string path=Write(name,events,new UTF8Encoding(true));try{AssParser.Read(path);throw new Exception("Accepted invalid event: "+name);}catch(ConversionException){Console.WriteLine("PASS rejects "+name);}}
    static int Main(string[] args){try{return Run(args);}catch(Exception e){Console.Error.WriteLine("FAIL "+e.Message);return 1;}}
    static int Run(string[] args){
        root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
        const string visible="Dialogue: 0,0:00:01.00,0:00:02.00,Default,,0,0,0,,Visible{\\fnKeepFont}\r\n";
        const string emptyZero="Dialogue: 0,0:00:00.00,0:00:00.00,Default,,0,0,0,,\r\n";
        const string emptyInvalid="Dialogue: 0,bad,bad,Default,,0,0,0,,   \r\n";
        const string emptyTail="Dialogue: 0,0:40:00.00,0:50:00.00,Default,,0,0,0,,\r\n";
        const string drawing="Dialogue: 0,0:00:02.00,0:00:03.00,Default,,0,0,0,,{\\p1}m 0 0 l 40 0 40 40 0 40\r\n";
        foreach(Encoding encoding in new Encoding[]{new UTF8Encoding(true),new UTF8Encoding(false),Encoding.GetEncoding(936)}){
            string source=Write("mixed-"+encoding.CodePage+"-"+encoding.GetPreamble().Length,emptyZero+visible+emptyInvalid+emptyTail+drawing+"Comment: 0,bad,bad,Default,,0,0,0,,注释\r\n",encoding);
            byte[] original=File.ReadAllBytes(source);var info=AssParser.Read(source);
            Check(info.Events==2&&info.SkippedEmptyLines.Count==3&&info.LastEnd==3,"empty rows excluded from count and tail ("+encoding.CodePage+", BOM "+encoding.GetPreamble().Length+")");
            Check(info.Fonts.Contains("KeepFont"),"visible inline font retained");
            string copy=Path.ChangeExtension(source,".clean.ass");AssParser.WriteConversionCopy(source,copy,info);var cleaned=AssParser.Read(copy);
            Check(cleaned.Events==2&&cleaned.SkippedEmptyLines.Count==0&&cleaned.LastEnd==3,"temporary copy removes only empty rows");
            Check(File.ReadAllBytes(copy).Take(encoding.GetPreamble().Length).SequenceEqual(encoding.GetPreamble()),"encoding preamble preserved");
            Check(File.ReadAllBytes(source).SequenceEqual(original),"original ASS unchanged");
        }
        Reject("nonempty-zero-duration","Dialogue: 0,0:00:00.00,0:00:00.00,Default,,0,0,0,,Visible\r\n");
        Reject("nonempty-reversed-time","Dialogue: 0,0:00:02.00,0:00:01.00,Default,,0,0,0,,Visible\r\n");
        Reject("nonempty-invalid-time","Dialogue: 0,bad,0:00:02.00,Default,,0,0,0,,Visible\r\n");
        Reject("malformed-fields","Dialogue: 0,0:00:00.00\r\n"+visible);
        Reject("all-empty",emptyZero+emptyTail);
        string tagged=Write("tagged",visible+"Dialogue: 0,0:00:02.00,0:00:03.00,Default,,0,0,0,,{\\alpha&HFF&}Content\r\n",new UTF8Encoding(false));
        Check(AssParser.Read(tagged).Events==2,"tagged/transparent content not inferred as empty");
        if(args.Length>1){
            string path=args[1];byte[] original=File.ReadAllBytes(path);var info=AssParser.Read(path);
            string copy=Path.Combine(root,"user-regression.clean.ass");AssParser.WriteConversionCopy(path,copy,info);var cleaned=AssParser.Read(copy);
            Check(info.SkippedEmptyLines.Count==12,"Komi-san sample skips 12 empty events");
            Check(info.Events==cleaned.Events&&info.LastEnd==cleaned.LastEnd&&cleaned.SkippedEmptyLines.Count==0,"Komi-san sample preflight and temporary copy agree");
            Check(File.ReadAllBytes(path).SequenceEqual(original),"Komi-san original unchanged");
            Console.WriteLine("Sample: "+info.Events+" retained events; tail "+info.LastEnd+" seconds; skipped lines "+String.Join(",",info.SkippedEmptyLines));
        }
        return 0;
    }
}
