using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace StandaloneAssRocket {
sealed class FontChoice {
    public string Name;public bool System,Directory;
    public override string ToString(){return Name+"  ·  "+(System&&Directory?"系统 / 字体目录":System?"系统":"字体目录");}
}
sealed class FontCatalog {
    public readonly List<FontChoice> Choices=new List<FontChoice>();
    public readonly HashSet<string> Available=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public bool Contains(string font){return Available.Contains(AssFontDocument.Key(font));}
}
sealed class FontUsage {
    public string Name,Replacement="";public bool Missing;
    public readonly List<AssFontDocument> Files=new List<AssFontDocument>();
}
sealed partial class MainForm {
    readonly ToolTip pathTip=new ToolTip{InitialDelay=450,ReshowDelay=100,AutoPopDelay=30000,ShowAlways=true};string pathTipValue="";
    internal string QueuePathAt(Point point){var hit=queue.HitTest(point);return hit.Item!=null&&hit.SubItem!=null&&hit.Item.SubItems.IndexOf(hit.SubItem)==1?(string)hit.Item.Tag:null;}
    void UpdateQueuePathTip(Point point){string value=QueuePathAt(point)??"";if(value==pathTipValue)return;pathTipValue=value;pathTip.SetToolTip(queue,value);}
    void ClearQueuePathTip(){pathTipValue="";pathTip.SetToolTip(queue,"");pathTip.Hide(queue);}
    internal FontCatalog BuildFontCatalog(){
        var result=new FontCatalog();var choices=new Dictionary<string,FontChoice>(StringComparer.OrdinalIgnoreCase);
        using(var installed=new InstalledFontCollection())foreach(var family in installed.Families){string key=AssFontDocument.Key(family.Name);result.Available.Add(key);choices[key]=new FontChoice{Name=family.Name,System=true};}
        foreach(string alias in FontFileMap().Keys)result.Available.Add(alias);
        var directoryMap=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(string file in FontDirectory.ReadFiles(fontDirectory.Text)){
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);AddFontNames(directoryMap,file,names);
            foreach(string name in names){string key=AssFontDocument.Key(name);FontChoice choice;if(!choices.TryGetValue(key,out choice)){choice=new FontChoice{Name=name};choices.Add(key,choice);}choice.Directory=true;}
        }
        foreach(string alias in directoryMap.Keys)result.Available.Add(alias);
        result.Choices.AddRange(choices.Values.OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase));return result;
    }
    internal void FontFilesChanged(IEnumerable<string> paths){
        var changed=new HashSet<string>(paths,StringComparer.OrdinalIgnoreCase);
        foreach(ListViewItem item in queue.Items)if(changed.Contains((string)item.Tag)){item.SubItems[0].Text="等待";item.SubItems[2].Text="字体已更新，等待转换";}
        queue.Invalidate();Log("INFO","字体替换完成","已更新 "+changed.Count+" 个 ASS 文件；原 ASS 备份位于各文件同目录的 backup 文件夹。",null);
    }
}
// Use the same logical pixel sizes and explicit per-monitor font scaling as the
// main window. Framework DPI autoscaling alone leaves pixel fonts too small.
class FontDialog:Form {
    readonly Dictionary<Control,Font> designFonts=new Dictionary<Control,Font>();
    readonly Dictionary<RowStyle,float> rowSizes=new Dictionary<RowStyle,float>();
    readonly Dictionary<ColumnStyle,float> columnSizes=new Dictionary<ColumnStyle,float>();
    readonly List<Font> ownedFonts=new List<Font>();
    Size designMinimum;bool ready,scaling;float uiScale=1F;
    internal float UiScale{get{return uiScale;}}
    [StructLayout(LayoutKind.Sequential)]struct NativeRect {public int Left,Top,Right,Bottom;}
    static IEnumerable<Control> All(Control root){yield return root;foreach(Control child in root.Controls)foreach(var control in All(child))yield return control;}
    internal FontDialog(){AutoScaleMode=AutoScaleMode.None;Font=UiTheme.BaseFont;BackColor=UiTheme.Background;ForeColor=UiTheme.Ink;}
    internal void FinishLayout(float scale){
        foreach(var control in All(this)){
            designFonts.Add(control,control.Font);var table=control as TableLayoutPanel;if(table==null)continue;
            foreach(RowStyle row in table.RowStyles)if(row.SizeType==SizeType.Absolute)rowSizes.Add(row,row.Height);
            foreach(ColumnStyle column in table.ColumnStyles)if(column.SizeType==SizeType.Absolute)columnSizes.Add(column,column.Width);
        }
        designMinimum=MinimumSize;ready=true;ApplyDialogScale(scale,null);
    }
    internal void ApplyDialogScale(float scale,Rectangle? suggested){
        if(!ready||scaling)return;scale=Math.Max(1F,Math.Min(4F,scale));scaling=true;SuspendLayout();var previous=ownedFonts.ToArray();ownedFonts.Clear();
        try{
            float ratio=scale/uiScale;uiScale=scale;Scale(new SizeF(ratio,ratio));
            var fonts=new Dictionary<Font,Font>();
            foreach(var pair in designFonts){Font font;if(!fonts.TryGetValue(pair.Value,out font)){var source=pair.Value;float pixels=source.Unit==GraphicsUnit.Pixel?source.Size:source.SizeInPoints*96F/72F;font=new Font(source.FontFamily,pixels*scale,source.Style,GraphicsUnit.Pixel);fonts.Add(source,font);ownedFonts.Add(font);}pair.Key.Font=font;}
            foreach(var pair in rowSizes)pair.Key.Height=(float)Math.Round(pair.Value*scale);
            foreach(var pair in columnSizes)pair.Key.Width=(float)Math.Round(pair.Value*scale);
            foreach(var table in All(this).OfType<DataGridView>()){
                table.DefaultCellStyle.Font=table.Font;var headerFont=new Font(UiTheme.TableHeaderFont.FontFamily,UiTheme.TableHeaderFont.Size*scale,UiTheme.TableHeaderFont.Style,GraphicsUnit.Pixel);ownedFonts.Add(headerFont);table.ColumnHeadersDefaultCellStyle.Font=headerFont;
                table.RowTemplate.Height=(int)Math.Round(34*scale);table.ColumnHeadersHeight=(int)Math.Round(36*scale);
                foreach(DataGridViewRow row in table.Rows)row.Height=table.RowTemplate.Height;
            }
            var work=Screen.FromControl(Owner??this).WorkingArea;MinimumSize=new Size(Math.Min(work.Width,(int)Math.Round(designMinimum.Width*scale)),Math.Min(work.Height,(int)Math.Round(designMinimum.Height*scale)));
            if(suggested.HasValue)Bounds=suggested.Value;
            else Size=new Size(Math.Min(Width,work.Width),Math.Min(Height,work.Height));
        }finally{ResumeLayout(true);scaling=false;foreach(var font in previous)font.Dispose();Invalidate(true);}
    }
    protected override void WndProc(ref Message m){
        if(m.Msg==0x02E0&&ready){var bounds=(NativeRect)Marshal.PtrToStructure(m.LParam,typeof(NativeRect));ApplyDialogScale((m.WParam.ToInt64()&0xffff)/96F,Rectangle.FromLTRB(bounds.Left,bounds.Top,bounds.Right,bounds.Bottom));m.Result=IntPtr.Zero;return;}
        base.WndProc(ref m);
    }
    protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing){foreach(var font in ownedFonts)font.Dispose();ownedFonts.Clear();}}
}
sealed class FontManagerForm:FontDialog {
    readonly string[] paths;FontCatalog catalog;readonly MainForm owner;
    readonly List<AssFontDocument> documents=new List<AssFontDocument>();readonly List<FontUsage> usages=new List<FontUsage>();readonly List<string> errors=new List<string>();
    internal readonly DataGridView grid=new DataGridView();internal readonly ComboBox replacement=new ComboBox();
    internal readonly Label summary=new Label(),message=new Label();readonly CheckBox missingOnly=new UiCheckBox{BackColor=UiTheme.Background};
    readonly ActionButton apply=new ActionButton{Text="保存替换（自动备份）",Primary=true,BackColor=UiTheme.Background};readonly Button set=new ActionButton{Text="设置替换",BackColor=UiTheme.Background},reset=new ActionButton{Text="撤销所选",BackColor=UiTheme.Background};
    internal FontManagerForm(string[] files,FontCatalog fonts,MainForm parent){
        paths=files;catalog=fonts;owner=parent;Owner=parent;Text="字体管理";StartPosition=FormStartPosition.CenterParent;
        Icon=parent.Icon;Font=UiTheme.BaseFont;BackColor=UiTheme.Background;ForeColor=UiTheme.Ink;ClientSize=new Size(960,610);MinimumSize=new Size(780,540);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=1,RowCount=6,BackColor=UiTheme.Background};
        foreach(var height in new[]{36,32,0,66,64,42})layout.RowStyles.Add(height==0?new RowStyle(SizeType.Percent,100):new RowStyle(SizeType.Absolute,height));Controls.Add(layout);
        summary.Dock=DockStyle.Fill;summary.TextAlign=ContentAlignment.MiddleLeft;layout.Controls.Add(summary,0,0);
        var filters=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,BackColor=UiTheme.Background};
        missingOnly.Text="仅显示缺失字体";missingOnly.Size=new Size(142,26);missingOnly.Margin=new Padding(0,5,16,0);missingOnly.CheckedChanged+=(s,e)=>RefreshRows();filters.Controls.Add(missingOnly);
        var rescan=Button("重新检查",()=>{if(usages.Any(x=>x.Replacement.Length>0)){message.Text="有待保存的替换，请先保存或撤销后再重新检查。";message.ForeColor=UiTheme.Danger;return;}try{catalog=owner.BuildFontCatalog();replacement.Items.Clear();replacement.Items.AddRange(catalog.Choices.Cast<object>().ToArray());Reload();}catch(Exception ex){message.Text=ex.Message;message.ForeColor=UiTheme.Danger;}});rescan.Size=new Size(104,28);filters.Controls.Add(rescan);layout.Controls.Add(filters,0,1);
        ConfigureGrid();layout.Controls.Add(grid,0,2);
        var edit=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=2,BackColor=UiTheme.Background,Padding=new Padding(0,8,0,0)};
        edit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,82));edit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));edit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,108));edit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,108));edit.RowStyles.Add(new RowStyle(SizeType.Absolute,32));edit.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        edit.Controls.Add(new Label{Text="替换为",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
        replacement.DropDownStyle=ComboBoxStyle.DropDown;replacement.AutoCompleteMode=AutoCompleteMode.SuggestAppend;replacement.AutoCompleteSource=AutoCompleteSource.ListItems;replacement.MaxDropDownItems=15;replacement.Dock=DockStyle.Fill;replacement.FlatStyle=FlatStyle.Flat;replacement.BackColor=UiTheme.Input;replacement.ForeColor=UiTheme.Ink;
        replacement.Items.AddRange(catalog.Choices.Cast<object>().ToArray());edit.Controls.Add(replacement,1,0);set.Dock=reset.Dock=DockStyle.Fill;set.Margin=reset.Margin=new Padding(8,0,0,0);set.Click+=(s,e)=>StageSelected();reset.Click+=(s,e)=>{foreach(var row in Selected())row.Replacement="";RefreshRows();};edit.Controls.Add(set,2,0);edit.Controls.Add(reset,3,0);
        var help=new Label{Text="可多选字体批量设置；下拉框包含系统及所选字体目录中的字体，可输入名称定位。",Dock=DockStyle.Fill,ForeColor=UiTheme.Muted,TextAlign=ContentAlignment.MiddleLeft};edit.Controls.Add(help,0,1);edit.SetColumnSpan(help,4);layout.Controls.Add(edit,0,3);
        message.Dock=DockStyle.Fill;message.TextAlign=ContentAlignment.MiddleLeft;message.ForeColor=UiTheme.Muted;layout.Controls.Add(message,0,4);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,BackColor=UiTheme.Background};
        actions.Controls.Add(Button("关闭",Close));apply.Size=new Size(190,36);apply.Click+=(s,e)=>SaveChanges();actions.Controls.Add(apply);
        actions.Controls.Add(Button("导出报告…",Export));actions.Controls.Add(Button("收集可用字体…",()=>owner.CollectManagedFonts(usages.Where(x=>!x.Missing).Select(x=>x.Name).ToArray(),this)));layout.Controls.Add(actions,0,5);
        FormClosing+=(s,e)=>{if(usages.Any(x=>x.Replacement.Length>0)&&MessageBox.Show(this,"有尚未保存的字体替换，关闭并放弃这些替换？","字体管理",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)e.Cancel=true;};
        Reload();FinishLayout(parent.UiScale);
    }
    ActionButton Button(string text,Action action){var button=new ActionButton{Text=text,Size=new Size(122,36),Margin=new Padding(8,0,0,0),BackColor=UiTheme.Background};button.Click+=(s,e)=>action();return button;}
    void ConfigureGrid(){
        grid.Dock=DockStyle.Fill;grid.BackgroundColor=UiTheme.Surface;grid.BorderStyle=BorderStyle.None;grid.AllowUserToAddRows=grid.AllowUserToDeleteRows=grid.AllowUserToResizeRows=false;grid.ReadOnly=true;grid.RowHeadersVisible=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=true;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.EnableHeadersVisualStyles=false;grid.GridColor=UiTheme.Line;grid.RowTemplate.Height=34;grid.ShowCellToolTips=true;
        grid.DefaultCellStyle=new DataGridViewCellStyle{BackColor=UiTheme.Surface,ForeColor=UiTheme.Ink,SelectionBackColor=UiTheme.RowSelected,SelectionForeColor=UiTheme.Ink,Padding=new Padding(6,0,6,0)};
        grid.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=UiTheme.Input,ForeColor=UiTheme.Muted,Padding=new Padding(6),SelectionBackColor=UiTheme.Input};grid.ColumnHeadersHeight=36;grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.Single;
        grid.Columns.Add(new DataGridViewTextBoxColumn{Name="font",HeaderText="ASS 使用的字体",FillWeight=31});grid.Columns.Add(new DataGridViewTextBoxColumn{Name="status",HeaderText="状态",FillWeight=12});grid.Columns.Add(new DataGridViewTextBoxColumn{Name="files",HeaderText="文件数",FillWeight=10});grid.Columns.Add(new DataGridViewTextBoxColumn{Name="target",HeaderText="待替换为",FillWeight=32});grid.Columns.Add(new DataGridViewLinkColumn{Name="view",HeaderText="使用位置",Text="查看",UseColumnTextForLinkValue=true,FillWeight=15,LinkColor=UiTheme.Accent,ActiveLinkColor=UiTheme.AccentDeep,VisitedLinkColor=UiTheme.Accent,TrackVisitedState=false});
        foreach(DataGridViewColumn column in grid.Columns)column.SortMode=DataGridViewColumnSortMode.NotSortable;
        grid.CellContentClick+=(s,e)=>{if(e.RowIndex>=0&&e.ColumnIndex==4)ShowUsage((FontUsage)grid.Rows[e.RowIndex].Tag);};
        grid.SelectionChanged+=(s,e)=>UpdateButtons();
    }
    internal void Reload(){
        documents.Clear();errors.Clear();usages.Clear();var map=new Dictionary<string,FontUsage>(StringComparer.OrdinalIgnoreCase);
        foreach(string path in paths)try{
            var document=AssFontDocument.Read(path);documents.Add(document);
            foreach(var group in document.References.GroupBy(x=>AssFontDocument.Key(x.Name))){FontUsage usage;if(!map.TryGetValue(group.Key,out usage)){usage=new FontUsage{Name=group.First().Name,Missing=!catalog.Contains(group.First().Name)};map.Add(group.Key,usage);}usage.Files.Add(document);}
        }catch(Exception e){errors.Add(path+"\r\n"+e.Message);}
        usages.AddRange(map.Values.OrderByDescending(x=>x.Missing).ThenBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase));RefreshRows();
        message.ForeColor=errors.Count>0?UiTheme.Danger:UiTheme.Muted;
        message.Text=errors.Count>0?"有 "+errors.Count+" 个文件读取失败，未纳入替换。导出报告可查看详情。":"仅替换样式及行内 \\fn 的字体名称；原 ASS 自动备份到同目录的 backup 文件夹。";
    }
    internal void RefreshRows(){
        var selected=new HashSet<FontUsage>(Selected());grid.Rows.Clear();foreach(var usage in usages.Where(x=>!missingOnly.Checked||x.Missing)){
            int index=grid.Rows.Add(usage.Name,usage.Missing?"缺失":"可用",usage.Files.Count,usage.Replacement.Length==0?"—":usage.Replacement);var row=grid.Rows[index];row.Tag=usage;row.Height=(int)Math.Round(34*UiScale);
            if(usage.Missing){row.Cells[0].Style.ForeColor=row.Cells[1].Style.ForeColor=UiTheme.Danger;row.Cells[0].Style.SelectionForeColor=row.Cells[1].Style.SelectionForeColor=UiTheme.Danger;}
            row.Cells[0].ToolTipText=usage.Name;row.Cells[3].ToolTipText=usage.Replacement;row.Cells[2].ToolTipText=String.Join("\r\n",usage.Files.Select(x=>x.Path));
        }
        grid.ClearSelection();foreach(DataGridViewRow row in grid.Rows)row.Selected=selected.Contains((FontUsage)row.Tag);if(grid.SelectedRows.Count==0&&grid.Rows.Count>0)grid.Rows[0].Selected=true;
        summary.Text="已检查 "+documents.Count+" / "+paths.Length+" 个 ASS  ·  "+usages.Count+" 种字体  ·  缺失 "+usages.Count(x=>x.Missing)+" 种  ·  待替换 "+usages.Count(x=>x.Replacement.Length>0)+" 种";UpdateButtons();
    }
    IEnumerable<FontUsage> Selected(){return grid.SelectedRows.Cast<DataGridViewRow>().Select(x=>x.Tag as FontUsage).Where(x=>x!=null).ToArray();}
    void UpdateButtons(){set.Enabled=reset.Enabled=Selected().Any();apply.Enabled=usages.Any(x=>x.Replacement.Length>0);}
    internal void StageSelected(){
        var choice=replacement.SelectedItem as FontChoice;
        if(choice==null||replacement.Text!=choice.ToString())choice=catalog.Choices.FirstOrDefault(x=>AssFontDocument.Key(x.Name)==AssFontDocument.Key(replacement.Text));
        if(choice==null){message.ForeColor=UiTheme.Danger;message.Text="请从列表选择系统或字体目录中的字体，或输入完整字体名称。";return;}
        foreach(var usage in Selected())usage.Replacement=AssFontDocument.Key(usage.Name)==AssFontDocument.Key(choice.Name)?"":choice.Name;
        RefreshRows();message.ForeColor=UiTheme.Muted;message.Text="替换已暂存。点击“保存替换（自动备份）”后写入对应 ASS 文件。";
    }
    internal void SaveChanges(){
        var mappings=usages.Where(x=>x.Replacement.Length>0).ToDictionary(x=>AssFontDocument.Key(x.Name),x=>x.Replacement);if(mappings.Count==0)return;
        var changed=new List<string>();var failures=new List<string>();var backups=new List<string>();int total=0;
        foreach(var document in documents)try{int count;string backup=document.Save(mappings,out count);if(backup!=null){changed.Add(document.Path);backups.Add(backup);total+=count;}}catch(Exception e){failures.Add(document.Path+"\r\n"+e.Message);}
        if(changed.Count>0)owner.FontFilesChanged(changed);Reload();
        message.ForeColor=failures.Count>0?UiTheme.Danger:UiTheme.Success;message.Text="已保存 "+changed.Count+" 个文件，替换 "+total+" 处字体引用。"+(failures.Count>0?"另有 "+failures.Count+" 个文件未保存，详见结果。":"已自动备份，字体状态已重新检查。");
        ShowText("字体替换结果",message.Text+"\r\n\r\n【原文件备份】\r\n"+String.Join("\r\n",backups)+(failures.Count==0?"":"\r\n\r\n【未保存的文件】\r\n"+String.Join("\r\n\r\n",failures)));
    }
    void ShowUsage(FontUsage usage){ShowText("字体使用位置 · "+usage.Name,String.Join("\r\n\r\n",usage.Files.Select(file=>file.Path+"\r\n"+String.Join("\r\n",file.References.Where(x=>AssFontDocument.Key(x.Name)==AssFontDocument.Key(usage.Name)).Select(x=>"  第 "+x.Line+" 行 · "+(x.Inline?"行内 \\fn":"样式")+(x.Style.Length==0?"":" · "+x.Style))))));}
    string Report(){return summary.Text+"\r\n\r\n"+String.Join("\r\n\r\n",usages.Select(x=>x.Name+" · "+(x.Missing?"缺失":"可用")+(x.Replacement.Length>0?" → "+x.Replacement:"")+"\r\n"+String.Join("\r\n",x.Files.Select(f=>f.Path))))+(errors.Count==0?"":"\r\n\r\n【读取失败】\r\n"+String.Join("\r\n\r\n",errors));}
    void Export(){using(var dialog=new SaveFileDialog{Filter="文本报告|*.txt",FileName="ASS-font-report.txt"})if(dialog.ShowDialog(this)==DialogResult.OK)try{File.WriteAllText(dialog.FileName,Report(),new UTF8Encoding(true));}catch(Exception e){MessageBox.Show(this,e.Message,"导出失败",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
    void ShowText(string title,string text){using(var dialog=CreateTextDialog(title,text))dialog.ShowDialog(this);}
    internal FontDialog CreateTextDialog(string title,string text){
        var dialog=new FontDialog{Text=title,Owner=this,Icon=Icon,StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(800,440),MinimumSize=new Size(560,320)};
        var box=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,Text=text,BackColor=UiTheme.Input,ForeColor=UiTheme.Ink,BorderStyle=BorderStyle.None};
        var panel=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=52,Padding=new Padding(8),FlowDirection=FlowDirection.RightToLeft,BackColor=UiTheme.Background};panel.Controls.Add(Button("关闭",dialog.Close));dialog.Controls.Add(box);dialog.Controls.Add(panel);dialog.FinishLayout(UiScale);return dialog;
    }

}
sealed partial class MainForm {internal void CollectManagedFonts(string[] fonts,IWin32Window window){CollectFonts(fonts,window);}}
}
