using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StandaloneAssRocket {
// Keep offsets into the original text: replacing a font never reserializes styles,
// event fields, line endings or other override tags.
sealed class AssFontReference {
    public string Name,Style;public int Offset,Length,Line;public bool Inline,Vertical;
}
sealed class AssFontDocument {
    public readonly string Path,Text;public readonly byte[] Original;
    public readonly List<AssFontReference> References=new List<AssFontReference>();
    readonly Encoding encoding;readonly byte[] preamble;
    AssFontDocument(string path,byte[] bytes,string text,Encoding decoder,byte[] bom){Path=path;Original=bytes;Text=text;encoding=decoder;preamble=bom;Parse();}
    public static string Key(string name){return (name??"").Trim().TrimStart('@').Normalize(NormalizationForm.FormC).ToLowerInvariant();}
    public static AssFontDocument Read(string path){
        byte[] bytes=File.ReadAllBytes(path),bom=new byte[0];Encoding decoder;int skip=0;
        if(bytes.Length>=2&&bytes[0]==0xff&&bytes[1]==0xfe){decoder=new UnicodeEncoding(false,false,true);skip=2;}
        else if(bytes.Length>=2&&bytes[0]==0xfe&&bytes[1]==0xff){decoder=new UnicodeEncoding(true,false,true);skip=2;}
        else {decoder=new UTF8Encoding(false,true);if(bytes.Length>=3&&bytes[0]==0xef&&bytes[1]==0xbb&&bytes[2]==0xbf)skip=3;}
        if(skip>0)bom=bytes.Take(skip).ToArray();string text;
        try{text=decoder.GetString(bytes,skip,bytes.Length-skip);}
        catch(DecoderFallbackException){if(skip>0)throw;decoder=Encoding.GetEncoding(936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);text=decoder.GetString(bytes);}
        return new AssFontDocument(System.IO.Path.GetFullPath(path),bytes,text,decoder,bom);
    }
    void Add(string raw,int offset,int line,string style,bool inline){
        int left=raw.Length-raw.TrimStart().Length;string value=raw.Trim();if(value.Length==0||value=="@")return;
        References.Add(new AssFontReference{Name=value.TrimStart('@'),Vertical=value.StartsWith("@"),Offset=offset+left,Length=value.Length,Line=line,Style=style,Inline=inline});
    }
    void Parse(){
        string section="";string[] format=null;int lineNumber=1,scanOffset=0;
        foreach(Match line in Regex.Matches(Text,@"[^\r\n]+|(?<=\n)(?=\n)")){
            // Count from source offsets, including blank lines and mixed CRLF/LF.
            for(;scanOffset<line.Index;scanOffset++)if(Text[scanOffset]=='\n'||Text[scanOffset]=='\r'&&(scanOffset+1==Text.Length||Text[scanOffset+1]!='\n'))lineNumber++;
            string trimmed=line.Value.Trim();if(trimmed.StartsWith("[")&&trimmed.EndsWith("]")){section=trimmed;format=null;continue;}
            bool styles=section.Equals("[V4+ Styles]",StringComparison.OrdinalIgnoreCase)||section.Equals("[V4 Styles]",StringComparison.OrdinalIgnoreCase);
            bool events=section.Equals("[Events]",StringComparison.OrdinalIgnoreCase);if(!styles&&!events)continue;
            var header=Regex.Match(line.Value,@"^\s*Format\s*:\s*(.*)$",RegexOptions.IgnoreCase);
            if(header.Success){format=header.Groups[1].Value.Split(',').Select(x=>x.Trim()).ToArray();continue;}
            var data=Regex.Match(line.Value,styles?@"^\s*Style\s*:":@"^\s*(?:Dialogue|Comment)\s*:",RegexOptions.IgnoreCase);if(!data.Success)continue;
            if(format==null)throw new InvalidDataException("第 "+lineNumber+" 行前缺少 Format，无法安全定位字体字段。");
            int index=Array.FindIndex(format,x=>x.Equals(styles?"Fontname":"Text",StringComparison.OrdinalIgnoreCase));
            if(index<0||!styles&&index!=format.Length-1)throw new InvalidDataException("第 "+lineNumber+" 行的 Format 不受支持（缺少 Fontname 或 Text 不在末尾）。");
            string body=line.Value.Substring(data.Length);string[] fields=body.Split(new[]{','},format.Length);
            if(fields.Length!=format.Length)throw new InvalidDataException("第 "+lineNumber+" 行字段数与 Format 不一致。");
            int offset=line.Index+data.Length;for(int i=0;i<index;i++)offset+=fields[i].Length+1;
            int styleIndex=Array.FindIndex(format,x=>x.Equals(styles?"Name":"Style",StringComparison.OrdinalIgnoreCase));string style=styleIndex<0?"":fields[styleIndex].Trim();
            if(styles)Add(fields[index],offset,lineNumber,style,false);
            else foreach(Match block in Regex.Matches(fields[index],@"\{[^{}]*\}"))foreach(Match font in Regex.Matches(block.Value,@"\\fn([^\\}]*)(?=\\|\})")){
                var value=font.Groups[1];string raw=value.Value;
                // A font tag inside \t(...) can end immediately before the
                // transform's closing parenthesis. It is not part of the name.
                int depth=0;for(int k=0;k<font.Index;k++){if(block.Value[k]=='(')depth++;else if(block.Value[k]==')')depth=Math.Max(0,depth-1);}
                while(depth>0&&raw.TrimEnd().EndsWith(")")){raw=raw.TrimEnd();raw=raw.Substring(0,raw.Length-1);depth--;}
                Add(raw,offset+block.Index+value.Index,lineNumber,style,true);
            }
        }
    }
    public byte[] Replace(IDictionary<string,string> replacements,out int changes){
        var edits=new List<Tuple<AssFontReference,string>>();
        foreach(var reference in References){string target;if(!replacements.TryGetValue(Key(reference.Name),out target)||Key(target)==Key(reference.Name))continue;
            if(String.IsNullOrWhiteSpace(target)||target.IndexOfAny(new[]{',','\\','{','}','\r','\n','\0'})>=0)throw new ArgumentException("替换字体名称包含无效字符。");
            edits.Add(Tuple.Create(reference,(reference.Vertical?"@":"")+target.Trim().TrimStart('@')));
        }
        changes=edits.Count;if(changes==0)return Original;
        var result=new StringBuilder(Text);foreach(var edit in edits.OrderByDescending(x=>x.Item1.Offset))result.Remove(edit.Item1.Offset,edit.Item1.Length).Insert(edit.Item1.Offset,edit.Item2);
        byte[] content;
        try{content=encoding.GetBytes(result.ToString());}
        catch(EncoderFallbackException){throw new InvalidDataException("替换字体名称无法用原文件编码保存。请先将此 ASS 另存为 UTF-8 后重试，原文件未修改。");}
        return preamble.Concat(content).ToArray();
    }
    public string Save(IDictionary<string,string> replacements,out int changes){
        byte[] updated=Replace(replacements,out changes);if(changes==0)return null;
        // Keep a readable ASS backup before atomically replacing the source.
        // Never overwrite an earlier backup or truncate the source in place.
        if(!File.ReadAllBytes(Path).SequenceEqual(Original))throw new IOException("文件在检查后已发生变化，请重新检查后再替换。");
        string directory=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path),"backup");Directory.CreateDirectory(directory);
        string backup=System.IO.Path.Combine(directory,System.IO.Path.GetFileName(Path));
        if(File.Exists(backup))backup=System.IO.Path.Combine(directory,System.IO.Path.GetFileNameWithoutExtension(Path)+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N").Substring(0,8)+".ass");
        string temp=Path+".font-edit-"+Guid.NewGuid().ToString("N")+".tmp";
        try{
            File.WriteAllBytes(temp,updated);File.Copy(Path,backup,false);
            if(!File.ReadAllBytes(backup).SequenceEqual(Original)||!File.ReadAllBytes(Path).SequenceEqual(Original))throw new IOException("文件在保存前已发生变化，请重新检查。");
            File.Replace(temp,Path,null);return backup;
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
}
