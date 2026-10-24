using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using StandaloneAssRocket;

static class FontManagerTests {
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static IEnumerable<Control> All(Control root){yield return root;foreach(Control child in root.Controls)foreach(var control in All(child))yield return control;}
    static Dictionary<string,string> Map(params string[] names){var result=new Dictionary<string,string>();for(int i=0;i<names.Length;i+=2)result.Add(AssFontDocument.Key(names[i]),names[i+1]);return result;}
    static string Fixture(string old){return "[Script Info]\r\nTitle: preserve 字幕\\fnFake\r\n\r\n[V4+ Styles]\nFormat: Fontsize, Name, Fontname, Bold, PrimaryColour, Outline\nStyle: 65,Main, "+old+" ,0,&H00FFFFFF,2.4\nStyle: 42,Other, @"+old+" ,-1,&H00CCFFFF,1\nStyle: 22,Keep,"+old+" Extra,0,&H00FFFFFF,0\n[Events]\r\nFormat: Layer, Start, End, Style, Text\r\nDialogue: 0,0:00:01.00,0:00:03.00,Main,{\\fn"+old+"\\b1}hello, 字幕{\\fn}reset \\fnLiteral\r\nComment: 0,0:00:01.00,0:00:03.00,Other,{\\fn@"+old+"}comment\r\nDialogue: 0,0:00:01.00,0:00:03.00,Main,{\\t(0,100,\\fn"+old+")}transform\r\n";}
    [STAThread]static int Main(string[] args){try{Run(Path.GetFullPath(args[0]));return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{UiTheme.UseDark(false);}}
    static void Run(string root){
        Application.EnableVisualStyles();string folder=Path.Combine(root,"qa","font-manager-v9");Directory.CreateDirectory(folder);
        string original=Fixture("MissingFont"),file=Path.Combine(folder,"episode_01.ass");File.WriteAllText(file,original,new UTF8Encoding(true));var document=AssFontDocument.Read(file);
        Check(document.References.Count==6,"scan styles, vertical faces, dialogue, comments and transformed inline fonts");
        Check(!document.References.Any(x=>x.Name=="Literal"||x.Name=="Fake"),"literal font-looking text outside override blocks is ignored");
        Check(document.References.First().Line==6&&document.References.First().Style=="Main","usage keeps exact source lines and style names across mixed line endings");
        int changes;byte[] bytes=document.Replace(Map("MissingFont","Arial","Arial","Tahoma"),out changes);string changed=Encoding.UTF8.GetString(bytes.Skip(3).ToArray());
        Check(changes==5&&changed==original.Replace(" MissingFont "," Arial ").Replace(" @MissingFont "," @Arial ").Replace("\\fnMissingFont\\","\\fnArial\\").Replace("\\fn@MissingFont}","\\fn@Arial}").Replace("\\fnMissingFont)","\\fnArial)"),"font-only replacements preserve every other byte-level text field, whitespace, line ending and transform parenthesis");
        Check(bytes.Take(3).SequenceEqual(new byte[]{0xef,0xbb,0xbf})&&!changed.Contains("Tahoma"),"UTF-8 BOM retained and mappings do not cascade");
        string backup=document.Save(Map("MissingFont","Arial"),out changes);Check(File.ReadAllBytes(backup).SequenceEqual(document.Original)&&File.ReadAllBytes(file).SequenceEqual(bytes),"atomic save keeps exact original backup and writes replacements");
        Check(Path.GetDirectoryName(backup)==Path.Combine(folder,"backup")&&Path.GetExtension(backup)==".ass","backup is a readable ASS in the source folder backup directory");
        byte[] firstBackup=File.ReadAllBytes(backup);var beforeSecond=AssFontDocument.Read(file);string secondBackup=beforeSecond.Save(Map("Arial","Tahoma"),out changes);
        Check(secondBackup!=backup&&File.ReadAllBytes(backup).SequenceEqual(firstBackup)&&File.ReadAllBytes(secondBackup).SequenceEqual(beforeSecond.Original),"repeated replacements retain earlier backups and back up each source version");
        Check(!Directory.GetFiles(folder,"*.bak",SearchOption.AllDirectories).Any(),"font replacement never creates .bak files");
        var saved=AssFontDocument.Read(file);Check(!saved.References.Any(x=>x.Name=="MissingFont"),"saved font usage refreshes without old references");
        try{document.Save(Map("MissingFont","Arial"),out changes);throw new Exception("stale scan accepted");}catch(IOException){Check(true,"externally changed ASS refuses stale replacement");}
        Check(!Directory.GetFiles(folder,"*.tmp").Any(),"no temporary edit files remain");
        string same=Path.Combine(folder,"same.ass");File.WriteAllText(same,original,new UTF8Encoding(false));var noBom=AssFontDocument.Read(same);Check(noBom.Save(Map("OtherFont","Arial"),out changes)==null&&changes==0&&File.ReadAllBytes(same).SequenceEqual(noBom.Original),"unmatched fonts leave file untouched and create no backup");
        string isolated=Path.Combine(folder,"no-changes-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(isolated);string untouched=Path.Combine(isolated,"untouched.ass");File.WriteAllText(untouched,original,new UTF8Encoding(false));
        AssFontDocument.Read(untouched).Save(Map("OtherFont","Arial"),out changes);Check(!Directory.Exists(Path.Combine(isolated,"backup")),"no replacement creates no backup directory");
        string initialBackup=AssFontDocument.Read(untouched).Save(Map("MissingFont","Arial"),out changes);Check(initialBackup==Path.Combine(isolated,"backup","untouched.ass")&&File.ReadAllText(initialBackup)==original,"first backup keeps the original ASS filename and contents");
        string blocked=Path.Combine(folder,"blocked-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(blocked);string blockedSource=Path.Combine(blocked,"source.ass");File.WriteAllText(blockedSource,original,new UTF8Encoding(false));File.WriteAllText(Path.Combine(blocked,"backup"),"occupied");
        try{AssFontDocument.Read(blockedSource).Save(Map("MissingFont","Arial"),out changes);throw new Exception("invalid backup destination accepted");}catch(IOException){Check(File.ReadAllText(blockedSource)==original&&!Directory.GetFiles(blocked,"*.tmp").Any(),"backup destination failure leaves ASS untouched and no temporary file");}
        var gbk=Encoding.GetEncoding(936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);string gbkPath=Path.Combine(folder,"gbk.ass");File.WriteAllBytes(gbkPath,gbk.GetBytes(Fixture("缺少字体")));var gbkDocument=AssFontDocument.Read(gbkPath);
        byte[] gbkResult=gbkDocument.Replace(Map("缺少字体","微软雅黑"),out changes);Check(changes==5&&gbk.GetString(gbkResult).Contains(" 微软雅黑 ")&&!gbkResult.Take(3).SequenceEqual(new byte[]{0xef,0xbb,0xbf}),"GBK remains GBK with Chinese font replacement");
        try{gbkDocument.Save(Map("缺少字体","Font😀"),out changes);throw new Exception("lossy encoding accepted");}catch(InvalidDataException){Check(File.ReadAllBytes(gbkPath).SequenceEqual(gbkDocument.Original),"unencodable replacement is rejected without corrupting original");}
        foreach(var encoding in new Encoding[]{new UnicodeEncoding(false,true,true),new UnicodeEncoding(true,true,true)}){string utf16=Path.Combine(folder,"utf16-"+encoding.CodePage+".ass");File.WriteAllText(utf16,original,encoding);var doc=AssFontDocument.Read(utf16);byte[] data=doc.Replace(Map("MissingFont","Arial"),out changes);Check(data.Take(2).SequenceEqual(encoding.GetPreamble())&&changes==5,"UTF-16 encoding and BOM retained: "+encoding.CodePage);}
        string invalid=Path.Combine(folder,"broken.ass");File.WriteAllText(invalid,"[V4+ Styles]\nStyle: Default,Arial\n");try{AssFontDocument.Read(invalid);throw new Exception("unsafe format accepted");}catch(InvalidDataException){Check(true,"malformed style format is excluded from edits");}
        string styleOnly=Path.Combine(folder,"styles-only.ass");File.WriteAllText(styleOnly,"[V4+ Styles]\nFormat: Name, Fontname\nStyle: Default,Arial\n");Check(AssFontDocument.Read(styleOnly).References.Count==1,"font management accepts style-only documents independently of conversion validation");
        try{noBom.Replace(Map("MissingFont","Bad,Font"),out changes);throw new Exception("comma accepted");}catch(ArgumentException){Check(true,"invalid font names cannot alter ASS field structure");}
        Check(AssFontDocument.Key(" @MiSsIngFont ")==AssFontDocument.Key("MissingFont"),"matching ignores case and vertical prefix without substring matching");
        using(var main=new MainForm()){
            main.Show();Application.DoEvents();Application.DoEvents();
            var directory=(TextBox)typeof(MainForm).GetField("fontDirectory",Flags).GetValue(main);directory.Text=Path.Combine(root,"qa","字体目录 测试");
            var catalog=main.BuildFontCatalog();Check(catalog.Choices.Any(x=>x.Name=="SubForgeFixture"&&x.Directory)&&catalog.Choices.Any(x=>x.System),"replacement catalog includes installed and recursively discovered directory font families");
            Check(!catalog.Choices.Any(x=>x.Name=="专用字体")&&catalog.Contains("SubForgeFixture"),"directory selection uses internal family names instead of font filenames");
            string second=Path.Combine(folder,"episode_02.ass");File.WriteAllText(second,Fixture("MissingFont"),new UTF8Encoding(false));
            var queue=(ListView)typeof(MainForm).GetField("queue",Flags).GetValue(main);typeof(MainForm).GetMethod("AddFiles",Flags).Invoke(main,new object[]{new[]{same,second}});Application.DoEvents();
            var item=queue.Items[0];Point point=new Point(item.SubItems[1].Bounds.Left+10,item.Bounds.Top+item.Bounds.Height/2);
            Check(main.QueuePathAt(point)==same&&main.QueuePathAt(new Point(5,point.Y))==null,"full-path hover resolves ASS cell only");
            typeof(Control).GetMethod("OnMouseMove",Flags).Invoke(queue,new object[]{new MouseEventArgs(MouseButtons.None,0,point.X,point.Y,0)});
            var tip=(ToolTip)typeof(MainForm).GetField("pathTip",Flags).GetValue(main);Check(tip.GetToolTip(queue)==same,"hover tooltip contains complete original path");
            typeof(Control).GetMethod("OnMouseLeave",Flags).Invoke(queue,new object[]{EventArgs.Empty});Check(tip.GetToolTip(queue)=="","path tooltip clears when leaving list");
            foreach(bool dark in new[]{false,true}){
                main.SetDarkMode(dark);foreach(int dpi in new[]{96,144,192,96}){main.ApplyUiScale(dpi,null,false);using(var manager=new FontManagerForm(new[]{same,second,invalid},catalog,main)){
                    manager.Show(main);Application.DoEvents();Application.DoEvents();
                    Check(Math.Abs(manager.Font.Size-main.Font.Size)<.01F&&manager.Font.Unit==main.Font.Unit,"font manager body matches main font at DPI "+dpi);
                    Check(Math.Abs(manager.replacement.Font.Size-main.Font.Size)<.01F&&Math.Abs(manager.grid.Font.Size-main.Font.Size)<.01F,"font input and list match the main text size at DPI "+dpi);
                    foreach(var button in All(manager).OfType<ActionButton>()){
                        Check(Math.Abs(button.Font.Size-main.Font.Size)<.01F,"dialog button matches main font: "+button.Text);
                        using(var bitmap=new Bitmap(button.Width,button.Height)){button.DrawToBitmap(bitmap,button.ClientRectangle);Check(bitmap.GetPixel(0,0).ToArgb()==button.Parent.BackColor.ToArgb()&&bitmap.GetPixel(bitmap.Width-1,bitmap.Height-1).ToArgb()==button.Parent.BackColor.ToArgb(),"rounded button corners match parent background: "+button.Text);}
                    }
                    using(var details=manager.CreateTextDialog("Usage test","ASS path and style usage")){
                        details.Show(manager);Application.DoEvents();Check(Math.Abs(details.Font.Size-manager.Font.Size)<.01F&&All(details).OfType<TextBox>().All(x=>Math.Abs(x.Font.Size-manager.Font.Size)<.01F),"usage/result dialog follows the same DPI font size");details.Close();
                    }
                    if(dpi==96){
                        Size originalSize=manager.ClientSize;float rowHeight=((TableLayoutPanel)manager.Controls[0]).RowStyles[0].Height;
                        foreach(float scale in new[]{1.5F,2F,1F}){manager.ApplyDialogScale(scale,null);Application.DoEvents();Check(Math.Abs(manager.Font.Size-12*scale)<.01F&&manager.grid.RowTemplate.Height==(int)Math.Round(34*scale),"repeated dialog DPI change preserves fonts and row density: "+scale);}
                        Check(manager.ClientSize==originalSize&&((TableLayoutPanel)manager.Controls[0]).RowStyles[0].Height==rowHeight,"dialog returns to exact logical layout after repeated DPI changes");
                    }
                    Check(manager.grid.Rows.Count==2&&manager.summary.Text.Contains("2 / 3"),"manager merges shared font usage and reports unreadable files");
                    var row=manager.grid.Rows.Cast<DataGridViewRow>().Single(x=>((FontUsage)x.Tag).Name=="MissingFont");var usage=(FontUsage)row.Tag;
                    Check(usage.Missing&&usage.Files.Count==2&&row.Cells[0].Style.ForeColor==UiTheme.Danger,"missing font is red and exposes both ASS usage files");
                    manager.grid.ClearSelection();row.Selected=true;manager.replacement.SelectedItem=catalog.Choices.Single(x=>x.Name=="SubForgeFixture");manager.StageSelected();
                    Check(usage.Replacement=="SubForgeFixture"&&File.ReadAllBytes(same).SequenceEqual(noBom.Original),"staging replacement leaves source unchanged until Save");
                    Check(manager.BackColor==UiTheme.Background&&manager.grid.DefaultCellStyle.ForeColor==UiTheme.Ink,"font manager follows "+(dark?"dark":"light")+" appearance");
                    Check(manager.grid.Height>100&&manager.replacement.Bottom<=manager.replacement.Parent.ClientSize.Height,"font management controls fit scaled dialog");
                    using(var image=new Bitmap(manager.Width,manager.Height)){manager.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(folder,dark?"font-manager-dark.png":"font-manager-light.png"));}
                    usage.Replacement="";manager.Close();
                }}
            }
            main.SetDarkMode(false);
            using(var manager=new FontManagerForm(new[]{same,second},catalog,main)){
                manager.Show(main);Application.DoEvents();var row=manager.grid.Rows.Cast<DataGridViewRow>().Single(x=>((FontUsage)x.Tag).Name=="MissingFont");manager.grid.ClearSelection();row.Selected=true;
                manager.replacement.SelectedItem=catalog.Choices.Single(x=>x.Name=="SubForgeFixture");manager.StageSelected();
                using(var timer=new System.Windows.Forms.Timer{Interval=100}){
                    timer.Tick+=(s,e)=>{var result=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x.Text=="字体替换结果");if(result!=null){timer.Stop();result.Close();}};timer.Start();manager.SaveChanges();
                }
                Check(new[]{same,second}.All(x=>AssFontDocument.Read(x).References.Any(r=>r.Name=="SubForgeFixture")&&!AssFontDocument.Read(x).References.Any(r=>r.Name=="MissingFont")),"Save action writes the selected replacement to both ASS files");
                Check(manager.grid.Rows.Cast<DataGridViewRow>().Any(x=>((FontUsage)x.Tag).Name=="SubForgeFixture"&&!((FontUsage)x.Tag).Missing),"saving refreshes available font status from directory catalog");
                Check(queue.Items.Cast<ListViewItem>().All(x=>x.SubItems[0].Text=="等待"&&x.SubItems[2].Text.Contains("字体已更新")),"saving returns modified queue tasks to waiting state");
                manager.Close();
            }
            main.Close();
        }
        Console.WriteLine("All font manager checks passed.");
    }
}
