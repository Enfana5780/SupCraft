using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

[assembly: System.Reflection.AssemblyTitle("SupCraft")]
[assembly: System.Reflection.AssemblyProduct("SupCraft")]
[assembly: System.Reflection.AssemblyDescription("ASS to BDN/PGS subtitle batch converter")]

namespace StandaloneAssRocket {
sealed class AssInfo {
    public int Events; public double LastEnd; public int Width=1920,Height=1080;
    public HashSet<string> Fonts=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public readonly List<int> SkippedEmptyLines=new List<int>();
}
sealed class QueueItem {
    public string Input,Status="等待",Output="",Error="";
    public QueueItem(string input){Input=input;}
}
sealed class ConversionException:Exception {
    public readonly string Stage,Advice,Details;
    public ConversionException(string stage,string message,string advice,string details=null):base(message){Stage=stage;Advice=advice;Details=details??message;}
}
static class AssParser {
    // Only structurally valid events with an empty/whitespace Text field are
    // omitted. Override tags and drawing commands are deliberately preserved.
    public static void WriteConversionCopy(string source,string target,AssInfo info){
        if(info.SkippedEmptyLines.Count==0){File.Copy(source,target,true);return;}
        byte[] bytes=File.ReadAllBytes(source);bool bom=bytes.Length>=3&&bytes[0]==0xef&&bytes[1]==0xbb&&bytes[2]==0xbf;
        Encoding encoding;string text;
        try{encoding=new UTF8Encoding(false,true);text=encoding.GetString(bytes,bom?3:0,bytes.Length-(bom?3:0));}
        catch(DecoderFallbackException){encoding=Encoding.GetEncoding(936);text=encoding.GetString(bytes);bom=false;}
        string newline=text.Contains("\r\n")?"\r\n":"\n";
        var skipped=new HashSet<int>(info.SkippedEmptyLines);
        string cleaned=String.Join(newline,text.Replace("\r\n","\n").Split('\n').Where((line,index)=>!skipped.Contains(index+1)));
        byte[] output=encoding.GetBytes(cleaned);
        if(bom){byte[] withBom=new byte[output.Length+3];withBom[0]=0xef;withBom[1]=0xbb;withBom[2]=0xbf;Buffer.BlockCopy(output,0,withBom,3,output.Length);output=withBom;}
        File.WriteAllBytes(target,output);
    }
    static double ParseTime(string value,int line){
        var m=Regex.Match(value.Trim(),@"^(\d+):([0-5]\d):([0-5]\d)\.(\d{1,3})$");
        if(!m.Success)throw new ConversionException("字幕预检","第 "+line+" 行时间格式无效："+value,"时间应为 H:MM:SS.cc，例如 0:01:23.45。");
        return int.Parse(m.Groups[1].Value)*3600+int.Parse(m.Groups[2].Value)*60+int.Parse(m.Groups[3].Value)+int.Parse(m.Groups[4].Value.PadRight(3,'0'))/1000.0;
    }
    // The bundled renderer can omit a short final event that contains \fad.
    // Change only a working copy, and only events ending at the known ASS tail.
    public static int RemoveFadeAtEnd(string path,double lastEnd){
        byte[] bytes=File.ReadAllBytes(path);bool bom=bytes.Length>=3&&bytes[0]==0xef&&bytes[1]==0xbb&&bytes[2]==0xbf;
        Encoding encoding;string text;
        try{encoding=new UTF8Encoding(false,true);text=encoding.GetString(bytes,bom?3:0,bytes.Length-(bom?3:0));}
        catch(DecoderFallbackException){encoding=Encoding.GetEncoding(936);text=encoding.GetString(bytes);bom=false;}
        string[] lines=text.Replace("\r\n","\n").Split('\n');string section="";string[] format=null;int changed=0;
        for(int i=0;i<lines.Length;i++){
            string line=lines[i].Trim();if(line.StartsWith("[")&&line.EndsWith("]")){section=line;continue;}
            if(!section.Equals("[Events]",StringComparison.OrdinalIgnoreCase))continue;
            if(line.StartsWith("Format:",StringComparison.OrdinalIgnoreCase)){format=line.Substring(7).Split(',').Select(x=>x.Trim()).ToArray();continue;}
            if(!line.StartsWith("Dialogue:",StringComparison.OrdinalIgnoreCase)||format==null)continue;
            int endIndex=Array.FindIndex(format,x=>x.Equals("End",StringComparison.OrdinalIgnoreCase));
            int textIndex=Array.FindIndex(format,x=>x.Equals("Text",StringComparison.OrdinalIgnoreCase));
            if(endIndex<0||textIndex<0)continue;
            string[] fields=line.Substring(9).Trim().Split(new[]{','},format.Length);
            if(fields.Length!=format.Length||Math.Abs(ParseTime(fields[endIndex],i+1)-lastEnd)>0.01)continue;
            string updated=Regex.Replace(fields[textIndex],@"\\fad\(\s*\d+\s*,\s*\d+\s*\)","",RegexOptions.IgnoreCase);
            if(updated==fields[textIndex])continue;
            fields[textIndex]=updated;lines[i]="Dialogue: "+String.Join(",",fields);changed++;
        }
        if(changed>0){byte[] output=encoding.GetBytes(String.Join("\r\n",lines));if(bom){var withBom=new byte[output.Length+3];withBom[0]=0xef;withBom[1]=0xbb;withBom[2]=0xbf;Buffer.BlockCopy(output,0,withBom,3,output.Length);output=withBom;}File.WriteAllBytes(path,output);}
        return changed;
    }
    public static AssInfo Read(string path){
        if(!File.Exists(path))throw new ConversionException("输入","ASS 文件不存在。","重新选择输入文件。",path);
        if(!path.EndsWith(".ass",StringComparison.OrdinalIgnoreCase))throw new ConversionException("输入","输入文件不是 .ass。","选择 Advanced SubStation Alpha 字幕文件。",path);
        byte[] bytes=File.ReadAllBytes(path); string text;
        try { text=new UTF8Encoding(false,true).GetString(bytes.Length>=3&&bytes[0]==0xef&&bytes[1]==0xbb&&bytes[2]==0xbf?bytes.Skip(3).ToArray():bytes); }
        catch(DecoderFallbackException){try{text=Encoding.GetEncoding(936).GetString(bytes);}catch(Exception e){throw new ConversionException("字幕预检","无法识别字幕编码。","将字幕另存为 UTF-8 BOM。",e.ToString());}}
        var info=new AssInfo();string section="";string[] eventFormat=null,styleFormat=null;
        foreach(var pair in text.Replace("\r\n","\n").Split('\n').Select((s,i)=>new{Text=s.Trim().TrimStart('\ufeff'),Line=i+1})){
            string s=pair.Text;
            if(s.StartsWith("[")&&s.EndsWith("]")){section=s;continue;}
            if(section.Equals("[Script Info]",StringComparison.OrdinalIgnoreCase)){
                var mx=Regex.Match(s,@"^PlayResX\s*:\s*(\d+)",RegexOptions.IgnoreCase);if(mx.Success)info.Width=int.Parse(mx.Groups[1].Value);
                var my=Regex.Match(s,@"^PlayResY\s*:\s*(\d+)",RegexOptions.IgnoreCase);if(my.Success)info.Height=int.Parse(my.Groups[1].Value);
            }
            if(s.StartsWith("Format:",StringComparison.OrdinalIgnoreCase)){
                var f=s.Substring(7).Split(',').Select(x=>x.Trim()).ToArray();
                if(section.Equals("[Events]",StringComparison.OrdinalIgnoreCase))eventFormat=f;
                else if(section.IndexOf("Styles",StringComparison.OrdinalIgnoreCase)>=0)styleFormat=f;
            } else if(s.StartsWith("Style:",StringComparison.OrdinalIgnoreCase)&&styleFormat!=null){
                var v=s.Substring(6).Trim().Split(',');var d=styleFormat.Zip(v,(k,x)=>new{k,x}).ToDictionary(x=>x.k,x=>x.x,StringComparer.OrdinalIgnoreCase);
                if(d.ContainsKey("Fontname"))info.Fonts.Add(d["Fontname"].Trim().TrimStart('@'));
            } else if(section.Equals("[Events]",StringComparison.OrdinalIgnoreCase)&&s.StartsWith("Dialogue:",StringComparison.OrdinalIgnoreCase)){
                if(eventFormat==null)throw new ConversionException("字幕预检","第 "+pair.Line+" 行 Dialogue 前没有 Events Format。","修复 [Events] 区段的 Format 行。");
                var v=s.Substring(9).Trim().Split(new[]{','},eventFormat.Length);if(v.Length!=eventFormat.Length)throw new ConversionException("字幕预检","第 "+pair.Line+" 行字段数不符合 Format。","在字幕编辑器中检查该行逗号和字段。",s);
                var d=eventFormat.Zip(v,(k,x)=>new{k,x}).ToDictionary(x=>x.k,x=>x.x,StringComparer.OrdinalIgnoreCase);
                if(!d.ContainsKey("Start")||!d.ContainsKey("End"))throw new ConversionException("字幕预检","Events Format 缺少 Start 或 End。","修复 Format 字段。");
                if(!d.ContainsKey("Text"))throw new ConversionException("字幕预检","Events Format 缺少 Text。","修复 Format 字段。");
                if(String.IsNullOrWhiteSpace(d["Text"])){info.SkippedEmptyLines.Add(pair.Line);continue;}
                double a=ParseTime(d["Start"],pair.Line),b=ParseTime(d["End"],pair.Line);if(b<=a)throw new ConversionException("字幕预检","第 "+pair.Line+" 行结束时间不晚于开始时间。","修正该字幕事件时间。",s);
                info.LastEnd=Math.Max(info.LastEnd,b);info.Events++;
                foreach(Match fm in Regex.Matches(d.ContainsKey("Text")?d["Text"]:"",@"\\fn([^\\}]+)"))info.Fonts.Add(fm.Groups[1].Value.Trim().TrimStart('@'));
            }
        }
        if(info.Events==0)throw new ConversionException("字幕预检","字幕没有可转换的非空 Dialogue 事件。","确认 [Events] 区段包含有效字幕内容。");
        return info;
    }
}
sealed partial class MainForm:Form {
    readonly string tools=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tools");
    readonly TextBox input=new TextBox(),output=new TextBox(),language=new TextBox(),log=new UiLogBox(),fontDirectory=new TextBox();
    readonly CheckBox gpu=new UiCheckBox();readonly ToolTip tips=new ToolTip();
    Control fileActionsPanel;Button fontBrowse,outputBrowse;string[] batchFontFiles=new string[0];bool closeAfterBatch;
    readonly UiListView queue=new UiListView();readonly List<QueueItem> jobs=new List<QueueItem>();
    readonly UiSelect engine=new UiSelect(),resolution=new UiSelect(),fps=new UiSelect(),format=new UiSelect();
    readonly CheckBox hdr=new UiCheckBox();readonly NumericUpDown dim=new CleanNumeric();
    readonly NumericUpDown customWidth=new CanvasNumeric(),customHeight=new CanvasNumeric();Control customSizeGroup;
    readonly CheckBox autocrop=new UiCheckBox(),palette=new UiCheckBox(),buffer=new UiCheckBox(),strict=new UiCheckBox();
    readonly Button start=new ActionButton{Primary=true},cancel=new ActionButton(),remove=new ActionButton(),clear=new ActionButton(),fontCheck=new ActionButton();readonly UiProgress progress=new UiProgress();readonly Label status=new Label();
    CancellationTokenSource cts;Process active;AssInfo info;
    public MainForm(){BuildInterface();}
    void UpdateEngineMode(){
        string selected=resolution.SelectedItem==null?"1080p":resolution.SelectedItem.ToString().Split(' ')[0];
        resolution.Items.Clear();resolution.Items.AddRange(engine.SelectedIndex==0?new object[]{"720p","1080p","1440p","2160p","自定义 Beta"}:new object[]{"720p","1080p","1440p（BDN）","2160p（BDN）"});
        int found=-1;for(int i=0;i<resolution.Items.Count;i++)if(resolution.Items[i].ToString().StartsWith(selected))found=i;resolution.SelectedIndex=found>=0?found:1;hdr.Enabled=engine.SelectedIndex==1;dim.Enabled=hdr.Enabled&&hdr.Checked;UpdateResolutionMode();
    }
    void UpdateResolutionMode(){
        if(customSizeGroup!=null){customSizeGroup.Visible=IsCustomResolution();customWidth.Enabled=customHeight.Enabled=cts==null&&IsCustomResolution();}
        bool high=engine.SelectedIndex==1&&(ResolutionName()=="1440p"||ResolutionName()=="2160p");if(high){format.SelectedIndex=1;format.Enabled=false;}else format.Enabled=cts==null;
        UpdateOutputOptions();UpdateCanvasNotice();
    }
    void UpdateOutputOptions(){
        bool compatible=engine.SelectedIndex==0;bool high=ResolutionName()=="1440p"||ResolutionName()=="2160p";
        autocrop.Enabled=buffer.Enabled=compatible;strict.Enabled=compatible&&format.SelectedIndex!=1;palette.Enabled=compatible&&format.SelectedIndex!=0||!compatible&&!high&&format.SelectedIndex==1;gpu.Enabled=!compatible&&format.SelectedIndex!=1;
        if(optionsPanel!=null&&modernPanel!=null){optionsPanel.Visible=compatible;modernPanel.Visible=!compatible;LayoutSettings();}
    }

    void PickFontDirectory(){using(var d=new FolderBrowserDialog{Description="选择字体文件夹（包括子文件夹，无需安装到系统）"})if(d.ShowDialog()==DialogResult.OK)fontDirectory.Text=d.SelectedPath;}
    Dictionary<string,string> AvailableFontFiles(){var map=FontFileMap();foreach(string file in FontDirectory.ReadFiles(fontDirectory.Text)){map[NormalizeFont(Path.GetFileNameWithoutExtension(file))]=file;AddFontNames(map,file);}return map;}
    void LogSkippedEmptyEvents(string source,AssInfo ass){if(ass.SkippedEmptyLines.Count==0)return;Log("INFO",Path.GetFileName(source)+" · 跳过空字幕事件","忽略 "+ass.SkippedEmptyLines.Count+" 条内容为空的 Dialogue；行号："+String.Join("、",ass.SkippedEmptyLines.Take(20))+(ass.SkippedEmptyLines.Count>20?" …":"")+"。仅清理临时转换副本，原 ASS 保持不变。",null);}
    void WarnMissingFonts(string source,AssInfo ass){LogSkippedEmptyEvents(source,ass);using(var fonts=new InstalledFontCollection()){var installed=new HashSet<string>(fonts.Families.Select(x=>x.Name),StringComparer.OrdinalIgnoreCase);var files=AvailableFontFiles();var missing=ass.Fonts.Where(x=>!FontAvailable(x,installed,files)).OrderBy(x=>x).ToArray();if(missing.Length>0)Log("WARN",Path.GetFileName(source)+" · 缺失字体",String.Join("、",missing),"安装字体或选择包含所需字体的文件夹后重试；替代字体可能改变字宽、换行和位置。");}}
    void SetBatchControls(bool enabled){fileActionsPanel.Enabled=enabled;fontDirectory.Enabled=fontBrowse.Enabled=output.Enabled=outputBrowse.Enabled=enabled;foreach(var combo in new[]{engine,resolution,fps,format})combo.Enabled=enabled;language.Enabled=enabled;hdr.Enabled=enabled&&engine.SelectedIndex==1;dim.Enabled=enabled&&hdr.Enabled&&hdr.Checked;customWidth.Enabled=customHeight.Enabled=enabled&&IsCustomResolution();if(enabled)UpdateResolutionMode();else foreach(var c in new[]{autocrop,palette,buffer,strict,gpu})c.Enabled=false;}
    static void Row(TableLayoutPanel p,int row,string label,Control field,Control button){field.Dock=DockStyle.Fill;p.Controls.Add(new Label{Text=label,AutoSize=true,Anchor=AnchorStyles.Left},0,row);p.Controls.Add(field,1,row);p.Controls.Add(button,2,row);}
    void PickInput(){using(var d=new OpenFileDialog{Filter="ASS 字幕|*.ass",Multiselect=true})if(d.ShowDialog()==DialogResult.OK)AddFiles(d.FileNames);}
    void AddFiles(IEnumerable<string> paths){
        if(cts!=null)return;int added=0;foreach(string path in paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase)){
            if(!path.EndsWith(".ass",StringComparison.OrdinalIgnoreCase)||queue.Items.Cast<ListViewItem>().Any(x=>String.Equals((string)x.Tag,path,StringComparison.OrdinalIgnoreCase)))continue;
            var item=new ListViewItem(new[]{"等待",path,""}){Tag=path};queue.Items.Add(item);added++;
        }
        taskCount.Text=queue.Items.Count+" 个任务";status.Text="队列中 "+queue.Items.Count+" 个文件";Log("INFO","已添加任务","新增 "+added+" 个，队列共 "+queue.Items.Count+" 个。",null);
    }
    void RemoveSelected(){foreach(ListViewItem item in queue.SelectedItems.Cast<ListViewItem>().ToArray())queue.Items.Remove(item);taskCount.Text=queue.Items.Count+" 个任务";status.Text="队列中 "+queue.Items.Count+" 个文件";}
    void ClearQueue(){if(cts==null){queue.Items.Clear();taskCount.Text="0 个任务";status.Text="等待添加 ASS 文件";}}
    void ShowSelectedDetails(){if(queue.SelectedItems.Count==0)return;var i=queue.SelectedItems[0];MessageBox.Show(this,(string)i.Tag+Environment.NewLine+Environment.NewLine+i.SubItems[2].Text,"任务详情 · "+i.SubItems[0].Text,MessageBoxButtons.OK,i.SubItems[0].Text=="失败"?MessageBoxIcon.Error:MessageBoxIcon.Information);}
    void CheckFonts(){
        if(queue.Items.Count==0){MessageBox.Show(this,"请先添加 ASS 文件。","字体管理",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        try{using(var manager=new FontManagerForm(queue.Items.Cast<ListViewItem>().Select(x=>(string)x.Tag).ToArray(),BuildFontCatalog(),this))manager.ShowDialog(this);}
        catch(Exception e){ShowError(new ConversionException("字体管理",e.Message,"检查 ASS 文件及字体目录访问权限。",e.ToString()));}
    }
    void CollectFonts(string[] families,IWin32Window owner){
        using(var d=new FolderBrowserDialog{Description="选择字体复制目录"})if(d.ShowDialog(owner)==DialogResult.OK){var map=AvailableFontFiles();int copied=0,unresolved=0;foreach(string family in families){string file;var key=NormalizeFont(family);map.TryGetValue(key,out file);if(String.IsNullOrWhiteSpace(file)||!File.Exists(file)){unresolved++;continue;}string target=Path.Combine(d.SelectedPath,Path.GetFileName(file));if(!File.Exists(target))File.Copy(file,target);copied++;}MessageBox.Show(owner,"已复制 "+copied+" 个字体文件。"+(unresolved>0?"\r\n有 "+unresolved+" 个字体无法从注册表定位，可能属于字体集合或用户字体。":""),"字体收集",MessageBoxButtons.OK,unresolved==0?MessageBoxIcon.Information:MessageBoxIcon.Warning);}
    }
    static bool FontAvailable(string name,HashSet<string> families,Dictionary<string,string> files){return families.Contains((name??"").TrimStart('@'))||files.ContainsKey(NormalizeFont(name));}
    static Dictionary<string,string> FontFileMap(){var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);AddFontDirectory(result,Environment.GetFolderPath(Environment.SpecialFolder.Fonts));AddFontDirectory(result,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Microsoft\Windows\Fonts"));AddRegistryFonts(result,Registry.LocalMachine,@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");AddRegistryFonts(result,Registry.LocalMachine,@"SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\Fonts");AddRegistryFonts(result,Registry.CurrentUser,@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");foreach(string path in result.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray())AddFontNames(result,path);return result;}
    static void AddFontDirectory(Dictionary<string,string> result,string directory){if(!Directory.Exists(directory))return;foreach(string path in Directory.GetFiles(directory)){string extension=Path.GetExtension(path);if(!extension.Equals(".ttf",StringComparison.OrdinalIgnoreCase)&&!extension.Equals(".otf",StringComparison.OrdinalIgnoreCase)&&!extension.Equals(".ttc",StringComparison.OrdinalIgnoreCase))continue;string stem=Path.GetFileNameWithoutExtension(path);result[NormalizeFont(stem)]=path;foreach(string alias in stem.Split(new[]{" & "},StringSplitOptions.RemoveEmptyEntries))result[NormalizeFont(alias)]=path;}}
    static void AddRegistryFonts(Dictionary<string,string> result,RegistryKey root,string key){using(var r=root.OpenSubKey(key)){if(r==null)return;foreach(string name in r.GetValueNames()){var value=r.GetValue(name) as string;if(String.IsNullOrWhiteSpace(value))continue;string path=Path.IsPathRooted(value)?value:Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),value);if(!File.Exists(path)&&!Path.IsPathRooted(value)&&root==Registry.CurrentUser)path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Microsoft\Windows\Fonts",value);if(!File.Exists(path))continue;string display=Regex.Replace(name,@"\s*\([^\)]*\)\s*$","").Trim();result[NormalizeFont(display)]=path;result[NormalizeFont(Path.GetFileNameWithoutExtension(value))]=path;}}}
    // Read every localized family, full and PostScript name, including all TTC faces.
    static ushort FontU16(BinaryReader r){return (ushort)((r.ReadByte()<<8)|r.ReadByte());}
    static uint FontU32(BinaryReader r){return ((uint)FontU16(r)<<16)|FontU16(r);}
    static void AddFontNames(Dictionary<string,string> result,string path,ISet<string> familyNames=null){
        try{
            using(var stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            using(var r=new BinaryReader(stream)){
                uint signature=FontU32(r);var faces=new List<uint>();
                if(signature==0x74746366){FontU32(r);uint count=FontU32(r);if(count>4096||12L+count*4L>stream.Length)return;for(uint i=0;i<count;i++)faces.Add(FontU32(r));}
                else faces.Add(0);
                foreach(uint face in faces){
                    if((long)face+12>stream.Length)continue;stream.Position=face;uint type=FontU32(r);
                    if(type!=0x00010000&&type!=0x4f54544f&&type!=0x74727565)continue;
                    int tables=FontU16(r);stream.Position+=6;if(stream.Position+tables*16L>stream.Length)continue;
                    long nameOffset=0,nameLength=0;
                    for(int t=0;t<tables;t++){uint tag=FontU32(r);FontU32(r);uint offset=FontU32(r),length=FontU32(r);if(tag==0x6e616d65){nameOffset=offset;nameLength=length;}}
                    if(nameOffset==0||nameLength<6||nameOffset+nameLength>stream.Length)continue;
                    stream.Position=nameOffset;FontU16(r);int records=FontU16(r),storage=FontU16(r);
                    if(6L+records*12L>nameLength)continue;
                    for(int n=0;n<records;n++){
                        stream.Position=nameOffset+6+n*12L;
                        int platform=FontU16(r),encoding=FontU16(r);FontU16(r);int id=FontU16(r),length=FontU16(r),offset=FontU16(r);
                        if(id!=1&&id!=4&&id!=6&&id!=16&&id!=21)continue;
                        if(storage+offset+(long)length>nameLength)continue;
                        Encoding decoder=null;
                        if(platform==0||(platform==3&&(encoding==0||encoding==1||encoding==10)))decoder=Encoding.BigEndianUnicode;
                        else if(platform==1&&encoding==0)decoder=Encoding.GetEncoding(10000);
                        if(decoder==null)continue;stream.Position=nameOffset+storage+offset;
                        string alias=decoder.GetString(r.ReadBytes(length)).Trim('\0',' ','\t','\r','\n');
                        if(alias.Length>0){result[NormalizeFont(alias)]=path;if(familyNames!=null&&id==1)familyNames.Add(alias);}
                    }
                }
            }
        }catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}
    }
    static string NormalizeFont(string value){return (value??"").Trim().TrimStart('@').Normalize(NormalizationForm.FormC).ToLowerInvariant();}
    internal string ResolveOutputDirectory(string source){return String.IsNullOrWhiteSpace(output.Text)?Path.GetDirectoryName(Path.GetFullPath(source)):Path.GetFullPath(output.Text.Trim());}
    internal string ReserveOutputName(string source,string directory,HashSet<string> usedNames){string name=SafeName(Path.GetFileNameWithoutExtension(source)),candidate=name;int suffix=2;while(!usedNames.Add(Path.Combine(directory,candidate)))candidate=name+"_"+(suffix++);return candidate;}
    void PickOutput(){using(var d=new FolderBrowserDialog())if(d.ShowDialog()==DialogResult.OK)output.Text=d.SelectedPath;}
    void TryPreflight(){try{info=AssParser.Read(input.Text);LogSkippedEmptyEvents(input.Text,info);Log("INFO","字幕预检通过",info.Events+" 条事件；最后时间 "+TimeSpan.FromSeconds(info.LastEnd)+"；脚本分辨率 "+info.Width+"×"+info.Height,null);var installed=new HashSet<string>(new InstalledFontCollection().Families.Select(x=>x.Name),StringComparer.OrdinalIgnoreCase);var fontFiles=FontFileMap();var missing=info.Fonts.Where(x=>!FontAvailable(x,installed,fontFiles)).OrderBy(x=>x).ToArray();if(missing.Length>0)Log("WARN","发现缺失字体",String.Join("、",missing),"安装字体后再转换，否则渲染器会使用替代字体。字宽、换行和位置可能改变。");}catch(ConversionException e){ShowError(e);}}
    async Task ConvertAsync(){
        if(cts!=null)return;TemporaryFonts temporaryFonts=null;try{
            if(queue.Items.Count==0)throw new ConversionException("输入","任务队列为空。","点击“添加 ASS”或将 ASS 文件拖入窗口。");if(IsCustomResolution()){int width,height;string error;if(!TryReadCustomCanvas(out width,out height,out error)){UpdateCanvasNotice();return;}customWidth.Value=width;customHeight.Value=height;CustomCanvas.ValidateSize(width,height);}if(!String.IsNullOrWhiteSpace(output.Text)){Directory.CreateDirectory(output.Text.Trim());CleanLegacyWorkDirectories(output.Text.Trim());}
            string avs2=Path.Combine(tools,"avs2bdnxml.exe"),filter=Path.Combine(tools,"S-VSFilterMod.dll");if(!File.Exists(avs2)||!File.Exists(filter))throw new ConversionException("环境","转换组件不完整。","重新解压完整程序包。",avs2+Environment.NewLine+filter);
            batchFontFiles=FontDirectory.ReadFiles(fontDirectory.Text);if(!String.IsNullOrWhiteSpace(fontDirectory.Text)&&batchFontFiles.Length==0)throw new ConversionException("字体目录","所选目录没有 TTF、OTF 或 TTC 字体。","选择包含字体文件的目录，或清空路径以使用系统字体。");if(engine.SelectedIndex==0&&batchFontFiles.Length>0){temporaryFonts=new TemporaryFonts(batchFontFiles);Log("INFO","临时字体加载","已加载 "+temporaryFonts.LoadedCount+" 个字体文件；批次结束后卸载。",null);if(temporaryFonts.Failed.Count>0)throw new ConversionException("字体加载","部分字体无法临时加载。","检查字体文件是否损坏或被安全软件拦截。",String.Join(Environment.NewLine,temporaryFonts.Failed));}
            cts=new CancellationTokenSource();StartElapsed();SetBatchControls(false);start.Enabled=false;cancel.Enabled=true;remove.Enabled=false;clear.Enabled=false;int passed=0,failed=0,index=0;var usedNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(ListViewItem item in queue.Items){
                cts.Token.ThrowIfCancellationRequested();index++;item.SubItems[0].Text="预检";item.EnsureVisible();progress.Style=ProgressBarStyle.Blocks;progress.Value=(index-1)*100/queue.Items.Count;
                try{item.SubItems[0].Text="转换中";var made=await ConvertOne((string)item.Tag,avs2,filter,index,usedNames,cts.Token);item.SubItems[0].Text="完成";item.SubItems[2].Text=String.Join("；",made);item.ForeColor=Color.DarkGreen;passed++;}
                catch(OperationCanceledException){item.SubItems[0].Text="已取消";throw;}
                catch(ConversionException e){item.SubItems[0].Text="失败";item.SubItems[2].Text=e.Stage+"："+e.Message;item.ForeColor=Color.Firebrick;failed++;Log("ERROR",Path.GetFileName((string)item.Tag)+" · "+e.Stage,e.Details,e.Advice);}
                catch(Exception e){item.SubItems[0].Text="失败";item.SubItems[2].Text=e.Message;item.ForeColor=Color.Firebrick;failed++;Log("ERROR",Path.GetFileName((string)item.Tag)+" · 未处理异常",e.ToString(),"保留日志并报告此异常。");}
            }
            status.Text="批量完成：成功 "+passed+"，失败 "+failed;progress.Value=100;Log(failed==0?"INFO":"WARN","批量转换完成","成功 "+passed+"，失败 "+failed+"。双击失败任务可查看原因。",null);
        }catch(OperationCanceledException){status.Text="已取消";Log("WARN","任务已取消","外部转换进程已停止。未完成的文件保留用于诊断。",null);}catch(ConversionException e){status.Text="失败："+e.Stage;ShowError(e);}catch(Exception e){status.Text="未处理异常";ShowError(new ConversionException("程序",e.Message,"保存日志并报告此异常。",e.ToString()));}finally{StopElapsed();if(active!=null){active.Dispose();active=null;}if(cts!=null){cts.Dispose();cts=null;}if(temporaryFonts!=null)temporaryFonts.Dispose();batchFontFiles=new string[0];SetBatchControls(true);start.Enabled=!customCanvasInvalid;cancel.Enabled=false;remove.Enabled=true;clear.Enabled=true;progress.Style=ProgressBarStyle.Blocks;if(closeAfterBatch)BeginInvoke((Action)Close);}
    }
    async Task<List<string>> ConvertOne(string source,string avs2,string filter,int index,HashSet<string> usedNames,CancellationToken token){
        if(engine.SelectedIndex==1)return await ConvertModern(source,index,usedNames,token);
               info=AssParser.Read(source);WarnMissingFonts(source,info);double rate=double.Parse(fps.Text,CultureInfo.InvariantCulture);int frames=(int)Math.Ceiling((info.LastEnd+2.0)*rate);int[] wh=Resolution();string destination=ResolveOutputDirectory(source),baseName=ReserveOutputName(source,destination,usedNames);
        string workRoot=Path.Combine(Path.GetTempPath(),"StandaloneAssRocket");Directory.CreateDirectory(workRoot);string work=Path.Combine(workRoot,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+index.ToString("D3")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(work);try{string safeAss=Path.Combine(work,"input.ass"),safeFilter=Path.Combine(work,"subtitle-filter.dll");AssParser.WriteConversionCopy(source,safeAss,info);File.Copy(filter,safeFilter,true);string script=Path.Combine(work,"render.avs");File.WriteAllText(script,"LoadPlugin(\""+Avs(safeFilter)+"\")\r\nMaskSubMod(\""+Avs(safeAss)+"\", "+wh[0]+", "+wh[1]+", "+rate.ToString("0.##############",CultureInfo.InvariantCulture)+", "+frames+")\r\n",new UTF8Encoding(false));
        status.Text="["+index+"/"+queue.Items.Count+"] "+Path.GetFileName(source);Log("INFO","开始任务 "+index+"/"+queue.Items.Count,source+Environment.NewLine+"事件 "+info.Events+"，画布 "+wh[0]+"×"+wh[1]+"，帧数 "+frames+"，工作目录 "+work,null);
        var outputs=new List<string>();if(format.SelectedIndex==0||format.SelectedIndex==2)outputs.Add(Path.Combine(destination,baseName+".sup"));if(format.SelectedIndex==1||format.SelectedIndex==2)outputs.Add(Path.Combine(destination,baseName+".xml"));
        foreach(string target in outputs){string staged=Path.Combine(work,Path.GetFileName(target));await RunTool(avs2,Args(staged,script,baseName,rate),work,token);Validate(staged,rate,info.LastEnd);if(IsNonStandardCanvas())CustomCanvas.ValidateOutput(staged,wh[0],wh[1]);Publish(staged,target);Validate(target,rate,info.LastEnd);Log("INFO","输出验证通过",target+"（"+new FileInfo(target).Length.ToString("N0")+" 字节）",null);}return outputs;}finally{DeleteWorkDirectory(work);}
    }
    async Task<List<string>> ConvertModern(string source,int index,HashSet<string> usedNames,CancellationToken token){
        info=AssParser.Read(source);WarnMissingFonts(source,info);double rate=double.Parse(fps.Text,CultureInfo.InvariantCulture);string destination=ResolveOutputDirectory(source),baseName=ReserveOutputName(source,destination,usedNames);
        string workRoot=Path.Combine(Path.GetTempPath(),"StandaloneAssRocket"),work=Path.Combine(workRoot,"modern-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+index.ToString("D3")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(work);bool completed=false;try{
            string modern=Path.Combine(tools,"modern"),super=Path.Combine(tools,"super","SUPer_CLI.exe");if(!File.Exists(Path.Combine(modern,"ass2bdnxml.exe")))throw new ConversionException("现代核心","缺少 ass2bdnxml v0.7h。","重新解压完整运行包。");if(!File.Exists(Path.Combine(modern,"libunibreak-7.dll")))throw new ConversionException("现代核心","缺少 libunibreak-7.dll，ass2bdnxml 无法启动。","请使用完整发布包；不要单独移动 EXE。");if((format.SelectedIndex==0||format.SelectedIndex==2)&&!File.Exists(super))throw new ConversionException("现代核心","缺少 SUPer CLI。","重新解压完整运行包。");
            CopyDirectory(modern,work);string safeAss=Path.Combine(work,"input.ass");AssParser.WriteConversionCopy(source,safeAss,info);string customFonts=batchFontFiles.Length>0?FontDirectory.Stage(batchFontFiles,work):null;string args="-v "+RenderResolutionName()+" -f "+rate.ToString(CultureInfo.InvariantCulture)+" --floor-ms -t \""+baseName+"\" -l \""+SafeName(language.Text)+"\"";if(customFonts!=null)args+=" --fontdir \""+customFonts+"\"";if(hdr.Checked)args+=" --dim "+dim.Value.ToString(CultureInfo.InvariantCulture);if(format.SelectedIndex==1&&ResolutionName()!="1440p"&&ResolutionName()!="2160p"&&palette.Checked)args+=" -q 255 -r";args+=" \""+safeAss+"\"";
            status.Text="["+index+"/"+queue.Items.Count+"] 现代核心："+Path.GetFileName(source);Log("INFO","现代核心渲染",source+Environment.NewLine+(hdr.Checked?"HDR BT.2020，亮度缩减 "+dim.Value+"%":"SDR BT.709")+"，"+ResolutionName(),null);await RunTool(Path.Combine(work,"ass2bdnxml.exe"),args,work,token);
            string xml=Path.Combine(work,"bdn.xml");if(ResolutionName()=="1440p"||ResolutionName()=="2160p")ScaleBdn(xml,Resolution()[0],Resolution()[1]);Validate(xml,rate,info.LastEnd);
            if(BdnLastEnd(xml,rate)+0.5<info.LastEnd){
                int changed=AssParser.RemoveFadeAtEnd(safeAss,info.LastEnd);
                if(changed==0)throw new ConversionException("BDN 验证","BDN 缺少 ASS 尾部字幕，且没有可重试的尾部淡入淡出。","查看保留的 BDN XML 和工作目录。",work);
                Log("WARN","尾部字幕重试","BDN 未包含最后字幕；在临时 ASS 中移除最后 "+changed+" 条字幕的淡入淡出后重新渲染。原 ASS 不变。",null);
                foreach(string png in Directory.GetFiles(work,"*.png"))File.Delete(png);File.Delete(xml);
                await RunTool(Path.Combine(work,"ass2bdnxml.exe"),args,work,token);
                if(ResolutionName()=="1440p"||ResolutionName()=="2160p")ScaleBdn(xml,Resolution()[0],Resolution()[1]);Validate(xml,rate,info.LastEnd);
                if(BdnLastEnd(xml,rate)+0.5<info.LastEnd)throw new ConversionException("BDN 验证","重试后 BDN 仍缺少 ASS 尾部字幕。","查看保留的 BDN XML 和工作目录。",work);
            }
            var outputs=new List<string>();
            if(format.SelectedIndex==0||format.SelectedIndex==2){string stagedSup=Path.Combine(work,baseName+".sup"),finalSup=Path.Combine(destination,baseName+".sup");string superArgs="-i \""+xml+"\" -b "+(hdr.Checked?"2020":"709")+" -q 3 -t 4 -y \""+stagedSup+"\"";using(var config=await SuperConfigLease.Acquire(Path.GetDirectoryName(super),gpu.Checked,token)){Log("INFO","SUPer 计算模式",gpu.Checked?"OpenCL 加速已启用（设备支持由上游检测）":"CPU 模式；已关闭 OpenCL 图像计算。",null);await RunTool(super,superArgs,work,token);}Validate(stagedSup,rate,info.LastEnd);Publish(stagedSup,finalSup);Validate(finalSup,rate,info.LastEnd);outputs.Add(finalSup);}
            if(format.SelectedIndex==1||format.SelectedIndex==2){string finalXml=Path.Combine(destination,baseName+".xml");Publish(xml,finalXml);Validate(finalXml,rate,info.LastEnd);outputs.Add(finalXml);}completed=true;return outputs;
        }finally{if(completed)DeleteWorkDirectory(work);else Log("WARN","已保留失败任务的临时文件",work,"可检查 BDN XML、PNG 和 SUP。");}
    }
    static double BdnLastEnd(string xml,double rate){
        var d=new XmlDocument();d.Load(xml);int nominal=(int)Math.Round(rate);double last=0;
        foreach(XmlNode e in d.SelectNodes("//Event")){
            var attr=e.Attributes["OutTC"];if(attr==null)continue;
            var parts=attr.Value.Split(':');if(parts.Length!=4)continue;
            int h,m,s,f;if(!int.TryParse(parts[0],out h)||!int.TryParse(parts[1],out m)||!int.TryParse(parts[2],out s)||!int.TryParse(parts[3],out f))continue;
            last=Math.Max(last,((h*3600+m*60+s)*nominal+f)/rate);
        }
        return last;
    }
    static void CopyDirectory(string source,string destination){foreach(string dir in Directory.GetDirectories(source,"*",SearchOption.AllDirectories))Directory.CreateDirectory(Path.Combine(destination,dir.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar)));foreach(string file in Directory.GetFiles(source,"*",SearchOption.AllDirectories)){string relative=file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);string target=Path.Combine(destination,relative);Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(file,target,true);}}
    static void ScaleBdn(string xmlPath,int targetWidth,int targetHeight){var d=new XmlDocument();d.Load(xmlPath);var format=d.SelectSingleNode("//Format");if(format==null)throw new ConversionException("高分辨率 BDN","BDN XML 缺少 Format 节点。","保留日志和输入文件用于诊断。");int sourceWidth=1920,sourceHeight=1080;int.TryParse(format.Attributes["VideoFormat"]==null?"":Regex.Match(format.Attributes["VideoFormat"].Value,@"\d+").Value,out sourceHeight);if(sourceHeight==720)sourceWidth=1280;double sx=targetWidth/(double)sourceWidth,sy=targetHeight/(double)sourceHeight;format.Attributes["VideoFormat"].Value=targetHeight+"p";foreach(XmlNode g in d.SelectNodes("//Graphic")){string path=Path.Combine(Path.GetDirectoryName(xmlPath),g.InnerText.Trim());int w=int.Parse(g.Attributes["Width"].Value),h=int.Parse(g.Attributes["Height"].Value),x=int.Parse(g.Attributes["X"].Value),y=int.Parse(g.Attributes["Y"].Value);int nw=(int)Math.Round(w*sx),nh=(int)Math.Round(h*sy);using(var source=Image.FromFile(path))using(var bitmap=new Bitmap(nw,nh,System.Drawing.Imaging.PixelFormat.Format32bppArgb))using(var graphics=Graphics.FromImage(bitmap)){graphics.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;graphics.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;graphics.DrawImage(source,0,0,nw,nh);string temp=path+".scaled.png";bitmap.Save(temp,System.Drawing.Imaging.ImageFormat.Png);source.Dispose();File.Delete(path);File.Move(temp,path);}g.Attributes["Width"].Value=nw.ToString();g.Attributes["Height"].Value=nh.ToString();g.Attributes["X"].Value=((int)Math.Round(x*sx)).ToString();g.Attributes["Y"].Value=((int)Math.Round(y*sy)).ToString();}d.Save(xmlPath);}
    void CleanLegacyWorkDirectories(string directory){try{string parent=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;foreach(string path in Directory.GetDirectories(directory,".work-*",SearchOption.TopDirectoryOnly)){string full=Path.GetFullPath(path);if(full.StartsWith(parent,StringComparison.OrdinalIgnoreCase))Directory.Delete(full,true);}}catch(Exception e){Log("WARN","旧缓存清理失败",e.Message,"可关闭占用文件的程序后手动删除输出目录中的 .work-* 文件夹。");}}
    void DeleteWorkDirectory(string work){try{string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"StandaloneAssRocket")).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;string full=Path.GetFullPath(work);if(full.StartsWith(root,StringComparison.OrdinalIgnoreCase)&&Directory.Exists(full))Directory.Delete(full,true);}catch(Exception e){Log("WARN","临时文件清理失败",work+Environment.NewLine+e.Message,"文件可能仍被转换组件占用，退出程序后可手动删除。");}}
    void Publish(string staged,string target){try{Directory.CreateDirectory(Path.GetDirectoryName(target));if(staged.EndsWith(".xml",StringComparison.OrdinalIgnoreCase)){var d=new XmlDocument();d.Load(staged);foreach(XmlNode g in d.SelectNodes("//Graphic")){string name=g.InnerText.Trim();string source=Path.Combine(Path.GetDirectoryName(staged),name),destination=Path.Combine(Path.GetDirectoryName(target),name);if(!File.Exists(source))throw new ConversionException("输出发布","找不到 BDN 引用的 PNG。","保留临时目录和日志用于诊断。",source);File.Copy(source,destination,true);}File.Copy(staged,target,true);}else File.Copy(staged,target,true);}catch(ConversionException){throw;}catch(Exception e){throw new ConversionException("输出发布","转换已完成，但无法复制到输出目录。","检查输出目录权限、同名文件占用和磁盘空间。",target+Environment.NewLine+e);}}
    string Args(string target,string script,string title,double rate){return "-t \""+title+"\" -l \""+SafeName(language.Text)+"\" -v "+CompatibilityFormatName()+" -f "+rate.ToString(CultureInfo.InvariantCulture)+" -a"+(autocrop.Checked?1:0)+" -p"+(palette.Checked?1:0)+" -b"+(buffer.Checked?1:0)+" -z"+(strict.Checked?1:0)+" -e0 -n0 -u0 -m3 -o \""+target+"\" \""+script+"\"";}
    async Task RunTool(string file,string args,string work,CancellationToken token){
        Log("INFO","启动 "+Path.GetFileNameWithoutExtension(file),file+" "+args,null);var tail=new List<string>();active=new Process{StartInfo=new ProcessStartInfo(file,args){WorkingDirectory=work,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.Default,StandardErrorEncoding=Encoding.Default},EnableRaisingEvents=true};
        DataReceivedEventHandler read=(s,e)=>{if(e.Data==null)return;BeginInvoke((Action)(()=>{LogRaw(e.Data);status.Text=e.Data.Length>70?e.Data.Substring(0,70):e.Data;}));lock(tail){tail.Add(e.Data);if(tail.Count>80)tail.RemoveAt(0);}};active.OutputDataReceived+=read;active.ErrorDataReceived+=read;
        try{if(!active.Start())throw new ConversionException("启动","无法启动 "+Path.GetFileName(file)+"。","检查安全软件和文件权限。");active.BeginOutputReadLine();active.BeginErrorReadLine();using(token.Register(()=>{try{if(active!=null&&!active.HasExited)active.Kill();}catch{}})){await Task.Run(()=>active.WaitForExit());}token.ThrowIfCancellationRequested();int code=active.ExitCode;if(code!=0){string details;lock(tail)details=String.Join(Environment.NewLine,tail);throw Classify(Path.GetFileName(file),code,details);}}
        catch(System.ComponentModel.Win32Exception e){throw new ConversionException("启动","无法运行转换组件："+e.Message,"确认 Windows 允许执行 tools 目录中的程序。",e.ToString());}
        finally{if(active!=null){active.Dispose();active=null;}}
    }
    ConversionException Classify(string tool,int code,string details){string l=details.ToLowerInvariant();if(l.Contains("avisynth")||l.Contains("avis"))return new ConversionException("AviSynth","AviSynth 无法加载或读取渲染脚本。","确认安装 32 位 AviSynth+；检查滤镜架构与 ASS 路径。",details);if(l.Contains("png")||l.Contains("output"))return new ConversionException("输出写入","无法写入 PNG、XML 或 SUP。","检查输出目录权限、磁盘空间和路径长度。",details);return new ConversionException("转换核心",tool+" 退出码 "+code,"查看下方原始输出；检查字幕、滤镜、字体和参数。",details);}
    void Validate(string target,double rate,double expectedEnd){
        if(!File.Exists(target)||new FileInfo(target).Length==0)throw new ConversionException("输出验证","转换进程没有生成有效文件。","查看转换日志和输出目录。",target);
        if(target.EndsWith(".xml",StringComparison.OrdinalIgnoreCase)){try{var d=new XmlDocument();d.Load(target);var graphics=d.SelectNodes("//Graphic");if(graphics==null||graphics.Count==0)throw new Exception("XML 中没有 Graphic");foreach(XmlNode g in graphics){string p=Path.Combine(Path.GetDirectoryName(target),g.InnerText.Trim());if(!File.Exists(p))throw new Exception("缺少 PNG："+p);}}catch(Exception e){throw new ConversionException("BDN 验证","BDN XML 或 PNG 引用无效。","检查特殊文件名、磁盘空间及写出错误。",e.ToString());}}
        else {byte[] b=File.ReadAllBytes(target);long pos=0;uint max=0;while(pos+13<=b.Length){if(b[pos]!='P'||b[pos+1]!='G')throw new ConversionException("SUP 验证","SUP 段同步标记损坏。","保留文件和日志用于诊断。","偏移 "+pos);uint pts=(uint)(b[pos+2]<<24|b[pos+3]<<16|b[pos+4]<<8|b[pos+5]);if(pts>max)max=pts;int n=b[pos+11]<<8|b[pos+12];pos+=13+n;}if(pos!=b.Length)throw new ConversionException("SUP 验证","SUP 文件末尾不完整。","检查磁盘空间或任务是否被中断。","已解析 "+pos+" / "+b.Length);double end=max/90000.0;if(end+1.0<expectedEnd)throw new ConversionException("SUP 验证","SUP 最终时间早于 ASS 最后一条字幕。","尾部字幕可能丢失；请保留日志。","SUP "+end+" 秒，ASS "+expectedEnd+" 秒");}
    }
    bool IsCustomResolution(){return engine.SelectedIndex==0&&resolution.SelectedIndex==4;}
    bool IsNonStandardCanvas(){return engine.SelectedIndex==0&&resolution.SelectedIndex>=2;}
    string CompatibilityFormatName(){if(IsNonStandardCanvas()){var wh=Resolution();return wh[0]+"x"+wh[1];}return ResolutionName();}
    int[] Resolution(){if(IsCustomResolution())return new[]{(int)customWidth.Value,(int)customHeight.Value};string value=ResolutionName();return value=="720p"?new[]{1280,720}:value=="1440p"?new[]{2560,1440}:value=="2160p"?new[]{3840,2160}:new[]{1920,1080};}
    string ResolutionName(){if(IsCustomResolution())return ((int)customWidth.Value).ToString(CultureInfo.InvariantCulture)+"x"+((int)customHeight.Value).ToString(CultureInfo.InvariantCulture);string value=resolution.SelectedItem==null?"1080p":resolution.SelectedItem.ToString();return value.StartsWith("720p")?"720p":value.StartsWith("1440p")?"1440p":value.StartsWith("2160p")?"2160p":"1080p";}
    string RenderResolutionName(){return ResolutionName()=="720p"?"720p":"1080p";}
    static string Avs(string p){return p.Replace("\\","/").Replace("\"","\\\"");}
    static string SafeName(string s){string v=Regex.Replace(s??"","[^A-Za-z0-9._-]+","_").Trim('_');return String.IsNullOrWhiteSpace(v)?"subtitle":v;}
    void Cancel(){if(cts!=null){cancel.Enabled=false;cts.Cancel();}}
    void ShowError(ConversionException e){Log("ERROR",e.Stage+"："+e.Message,e.Details,e.Advice);MessageBox.Show(this,e.Message+"\r\n\r\n处理建议："+e.Advice,"转换失败 · "+e.Stage,MessageBoxButtons.OK,MessageBoxIcon.Error);}
    void Log(string level,string title,string details,string advice){LogRaw(DateTime.Now.ToString("HH:mm:ss")+" ["+level+"] "+title);if(!String.IsNullOrWhiteSpace(details))LogRaw("  "+details.Replace("\r\n","\r\n  "));if(!String.IsNullOrWhiteSpace(advice))LogRaw("  处理建议："+advice);}
    void LogRaw(string s){if(InvokeRequired){BeginInvoke((Action)(()=>LogRaw(s)));return;}log.AppendText(s+Environment.NewLine);log.SelectionStart=log.TextLength;log.ScrollToCaret();try{Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"logs"));File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"logs",DateTime.Now.ToString("yyyyMMdd")+".log"),s+Environment.NewLine,Encoding.UTF8);}catch{}}
}
static class Program {[STAThread]static void Main(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);if(args.Any(x=>x=="--self-test-ui")||args.Any(x=>x=="--render-ui")){using(var form=new MainForm()){form.CreateControl();if(args.Length>1&&args[0]=="--render-ui"){form.Show();Application.DoEvents();Application.DoEvents();if(args.Contains("--custom")||args.Contains("--2160p"))form.SelectPreviewResolution(args.Contains("--custom"));if(args.Contains("--invalid"))form.SelectInvalidPreview();if(args.Contains("--dark"))form.SetDarkMode(true);Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(args[1]);}form.Hide();}}return;}Application.Run(new MainForm());}}
}
