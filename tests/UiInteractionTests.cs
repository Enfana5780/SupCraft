using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using StandaloneAssRocket;

static class UiInteractionTests {
    static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static T Field<T>(MainForm form,string name){return (T)typeof(MainForm).GetField(name,Flags).GetValue(form);}
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wp,IntPtr lp);
    [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr window,int index);
    static int Hit(MainForm form,Point point){var p=form.PointToScreen(point);return SendMessage(form.Handle,0x84,IntPtr.Zero,new IntPtr((p.X&0xffff)|((p.Y&0xffff)<<16))).ToInt32();}
    static void Key(Control control,Keys key){typeof(Control).GetMethod("OnKeyDown",Flags).Invoke(control,new object[]{new KeyEventArgs(key)});Application.DoEvents();}
    static void CheckResize(MainForm form,int dpi){
        var files=Field<Panel>(form,"filesCard");var settings=Field<Panel>(form,"settingsCard");var logs=Field<Panel>(form,"logsCard");
        Size window=form.Size,filesSize=files.Size,settingsSize=settings.Size,logsSize=logs.Size;int logTop=logs.Top;
        int dx=(int)(180*dpi/96F),dy=(int)(120*dpi/96F);
        form.Size=new Size(window.Width+dx,window.Height);Application.DoEvents();
        if(form.Width==window.Width){form.Width=window.Width-dx;Application.DoEvents();}dx=form.Width-window.Width;
        Check(dx!=0&&settings.Size==settingsSize&&files.Width==filesSize.Width+dx&&files.Height==filesSize.Height&&logs.Height==logsSize.Height,"horizontal resize changes files width and preserves settings size at DPI "+dpi);
        form.Size=new Size(window.Width,window.Height+dy);Application.DoEvents();
        if(form.Height==window.Height){form.Height=window.Height-dy;Application.DoEvents();}dy=form.Height-window.Height;
        Check(dy!=0&&settings.Size==settingsSize&&files.Size==filesSize&&logs.Top==logTop&&logs.Height==logsSize.Height+dy,"vertical resize changes only log height at DPI "+dpi);
        form.Size=window;Application.DoEvents();
    }
    static void CheckProgressLayout(MainForm form,int dpi){
        var settings=Field<Panel>(form,"settingsCard");var panel=Field<Panel>(form,"statusPanel");var actions=Field<Panel>(form,"settingsActions");var viewport=Field<ScrollHost>(form,"settingsViewport");var logs=Field<Panel>(form,"logsCard");
        Check(panel.Parent==settings&&panel.Left==actions.Left&&panel.Right==actions.Right&&panel.Bottom<actions.Top&&viewport.Bottom<panel.Top,"status and progress sit above Start inside settings at DPI "+dpi);
        foreach(Control child in panel.Controls)Check(panel.ClientRectangle.Contains(child.Bounds),"progress content stays inside the settings card: "+child.GetType().Name);
        var status=Field<Label>(form,"status");var elapsed=Field<Label>(form,"elapsedLabel");var progress=Field<UiProgress>(form,"progress");
        Check(status.Right<elapsed.Left&&progress.Top>status.Bottom&&progress.Width==panel.Width,"status, elapsed time and progress bar do not overlap at DPI "+dpi);
        Check(logs.Top==settings.Bottom+(int)Math.Round(10*dpi/96F),"logs use the space vacated by the old status strip at DPI "+dpi);
    }
    static void CheckGrid(UiListView queue,string state){
        using(var bitmap=new Bitmap(queue.Width,queue.Height)){
            queue.DrawToBitmap(bitmap,queue.ClientRectangle);
            int boundary=queue.Columns[0].Width-1;
            Check(bitmap.GetPixel(boundary,queue.Height-8).ToArgb()==UiTheme.Line.ToArgb(),"column grid remains visible in "+state+" list");
            if(queue.Items.Count==0){int rows=0;for(int y=queue.Font.Height*2;y<queue.Height-1;y++)if(bitmap.GetPixel(boundary/2,y).ToArgb()==UiTheme.Line.ToArgb())rows++;Check(rows>=3,"empty list background contains horizontal grid rows");}
        }
    }
    [STAThread]static int Main(){try{Run();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
    static void Run(){
        Application.EnableVisualStyles();
        using(var form=new MainForm()){
            Check(form.Size==new Size(1280,840),"default window retains 1280 width and increases height to 840");
            form.Show();Application.DoEvents();
            form.ApplyUiScale(96,null,false);form.Size=new Size(1280,840);Application.DoEvents();
            var engine=Field<UiSelect>(form,"engine");float baseFont=engine.Font.Size;
            Check(engine.SelectedIndex==1&&Field<Panel>(form,"modernPanel").Visible&&!Field<Panel>(form,"optionsPanel").Visible,"startup selects libass and exposes only modern options");
            Check(String.IsNullOrWhiteSpace(Field<TextBox>(form,"output").Text),"startup defaults to each ASS source directory");
            Check(!Field<Panel>(form,"settingsContent").Controls.Cast<Control>().Any(x=>x.Text=="输出选项"||x.Text=="现代核心选项"),"option group titles are removed");
            Check(Math.Abs(baseFont-12F)<.01F,"base setting text uses the requested 12 pixel size");
            foreach(int dpi in new[]{144,192,96,192,144,96}){
                form.ApplyUiScale(dpi,null,false);form.Size=new Size((int)(1280*dpi/96F),(int)(840*dpi/96F));Application.DoEvents();
                Check(Math.Abs(engine.Font.Size-baseFont*dpi/96F)<.05F,"font follows DPI without cumulative scaling: "+dpi);
                Check(engine.Height==(int)Math.Round(32*dpi/96F),"input height follows DPI: "+dpi);
                var select=Field<UiSelect>(form,"resolution");select.Open();Application.DoEvents();Check(select.Popup.Font.Size==select.Font.Size,"dropdown preserves scaled font: "+dpi);select.Popup.Close();Application.DoEvents();
                CheckResize(form,dpi);CheckProgressLayout(form,dpi);
                var list=Field<UiListView>(form,"queue");list.Items.Clear();for(int i=0;i<10;i++)list.Items.Add(new ListViewItem(new[]{"等待","episode_"+i+".ass",""}));list.EnsureVisible(0);Application.DoEvents();
                Check(list.Items[7].Bounds.Bottom<=list.ClientSize.Height&&list.Items[0].Bounds.Top>=0,"file list shows eight complete rows at DPI "+dpi);list.Items.Clear();
                engine.SelectedIndex=0;Check(Field<Panel>(form,"optionsPanel").Visible&&!Field<Panel>(form,"modernPanel").Visible,"AviSynth shows only output options at DPI "+dpi);engine.SelectedIndex=1;
            }
            var status=Field<Label>(form,"status");var progress=Field<UiProgress>(form,"progress");
            string initialStatus=status.Text;status.Text="[1/8] 正在转换 · "+new String('a',160)+".ass";progress.Value=47;
            typeof(MainForm).GetMethod("StartElapsed",Flags).Invoke(form,null);System.Threading.Thread.Sleep(1100);Application.DoEvents();
            Check(progress.Value==47&&Field<Label>(form,"elapsedLabel").Text=="已运行 00:00:01","relocated progress and elapsed time keep updating");
            Check(Field<ToolTip>(form,"tips").GetToolTip(status)==status.Text,"long conversion status remains available as a tooltip");
            typeof(MainForm).GetMethod("StopElapsed",Flags).Invoke(form,null);status.Text=initialStatus;progress.Value=0;
            var queue=Field<UiListView>(form,"queue");CheckGrid(queue,"empty");
            for(int i=0;i<40;i++)queue.Items.Add(new ListViewItem(new[]{"等待","episode_"+i.ToString("00")+".ass",""}));
            Application.DoEvents();queue.EnsureVisible(30);Application.DoEvents();Check(queue.TopItem.Index>0,"populated grid preserves native vertical scrolling");CheckGrid(queue,"scrolled");queue.Items.Clear();Application.DoEvents();CheckGrid(queue,"cleared");
            for(int cycle=0;cycle<4;cycle++){
                using(var other=new Form()){other.Show();other.Activate();Application.DoEvents();form.Activate();Application.DoEvents();other.Close();}
                SendMessage(form.Handle,0x86,IntPtr.Zero,IntPtr.Zero);SendMessage(form.Handle,0x86,new IntPtr(1),IntPtr.Zero);SendMessage(form.Handle,0x85,new IntPtr(1),IntPtr.Zero);Application.DoEvents();
                Check((GetWindowLong(form.Handle,-16)&0x00C00000)==0,"reactivation keeps native caption removed: "+cycle);
                Check(form.PointToScreen(Point.Empty)==form.Location,"reactivation keeps custom header at client top: "+cycle);
            }
            Check(form.FormBorderStyle==FormBorderStyle.None,"title bar is part of the application");
            Check(Hit(form,new Point(3,3))==13&&Hit(form,new Point(form.ClientSize.Width-3,form.ClientSize.Height-3))==17,"window corners preserve native resizing");
            Check(Hit(form,new Point(700,30))==2,"integrated header preserves native dragging");
            var maximize=Field<Button>(form,"maximizeButton");var minimize=Field<Button>(form,"minimizeButton");var close=Field<Button>(form,"closeButton");
            Check(Hit(form,new Point(Field<Panel>(form,"headerPanel").Left+close.Left+close.Width/2,32))!=2,"caption buttons are excluded from header dragging");
            Rectangle normal=form.Bounds;maximize.PerformClick();Application.DoEvents();Check(form.WindowState==FormWindowState.Maximized,"maximize button works");
            var work=Screen.FromControl(form).WorkingArea;Check(form.Bounds.Left>=work.Left-1&&form.Bounds.Top>=work.Top-1&&form.Bounds.Right<=work.Right+1&&form.Bounds.Bottom<=work.Bottom+1,"maximized window respects taskbar work area");
            maximize.PerformClick();Application.DoEvents();Check(form.WindowState==FormWindowState.Normal&&form.Bounds==normal,"restore button restores window bounds: "+normal+" -> "+form.Bounds+" state="+form.WindowState);
            minimize.PerformClick();Application.DoEvents();Check(form.WindowState==FormWindowState.Minimized,"minimize button works");form.WindowState=FormWindowState.Normal;Application.DoEvents();
            var resolution=Field<UiSelect>(form,"resolution");engine.SelectedIndex=0;resolution.SelectedIndex=4;
            var width=Field<NumericUpDown>(form,"customWidth");var height=Field<NumericUpDown>(form,"customHeight");var notice=Field<NoticeLabel>(form,"customValidation");var start=Field<Button>(form,"start");
            width.Text="1441";Application.DoEvents();Check(notice.Visible&&notice.IsError&&notice.Text.Contains("宽度")&&!start.Enabled,"non-multiple-of-four width shows an immediate red warning");
            Check(Field<InputShell>(form,"customWidthShell").Invalid,"invalid width input receives red border");
            using(var image=new Bitmap(notice.Width,notice.Height)){notice.DrawToBitmap(image,notice.ClientRectangle);Color pixel=image.GetPixel(notice.Width/2,4);Check(pixel.R>pixel.G+6&&pixel.R>pixel.B+6,"warning actually renders a red background");}
            width.Text="1440";height.Text="3201";Application.DoEvents();Check(notice.Visible&&notice.Text.Contains("高度")&&!start.Enabled,"height is checked independently of width");
            height.Text="3200";Application.DoEvents();Check(!notice.Visible&&start.Enabled,"correcting dimensions clears warning and reenables conversion");
            width.Text="";Application.DoEvents();Check(notice.Visible&&!start.Enabled,"incomplete input is reported while editing");
            width.Text="9999";Application.DoEvents();Check(notice.Visible&&notice.Text.Contains("64–4096")&&!start.Enabled,"out-of-range input is reported before focus leaves field");
            width.Text="1441";Field<UiSelect>(form,"engine").SelectedIndex=1;Application.DoEvents();Check(!notice.Visible&&start.Enabled,"invalid custom input cannot block modern mode");
            Field<UiSelect>(form,"engine").SelectedIndex=0;resolution.SelectedIndex=4;Application.DoEvents();Check(notice.Visible&&!start.Enabled,"returning to custom mode revalidates previous raw input");width.Text="1440";height.Text="3200";
            resolution.SelectedIndex=2;Key(resolution,Keys.Down);Check(resolution.SelectedIndex==3,"custom select supports keyboard navigation");Key(resolution,Keys.Alt|Keys.Down);Check(resolution.Popup!=null&&resolution.Popup.Visible,"Alt+Down opens custom dropdown");
            var popup=resolution.Popup;Key(popup,Keys.Down);Key(popup,Keys.Enter);Check(resolution.SelectedIndex==4&&resolution.Popup==null,"dropdown keyboard selection commits the option");
            Key(resolution,Keys.F4);Check(resolution.Popup!=null,"F4 opens custom dropdown");Key(resolution.Popup,Keys.Escape);Check(resolution.Popup==null,"Escape dismisses dropdown");
            var scroll=Field<ScrollHost>(form,"settingsViewport");form.Size=new Size(1040,800);Application.DoEvents();Check(scroll.MaxOffset==0,"conditional options fit the taller settings card without scrolling");scroll.Configure(scroll.Content.Width,scroll.Height+120);scroll.ScrollBy(80);Check(scroll.Offset>0&&scroll.Content.Top<0,"custom scrollbar scrolls settings");scroll.ScrollBy(-10000);Check(scroll.Offset==0,"custom scrollbar returns to the top");
            close.PerformClick();Application.DoEvents();Check(!form.Visible,"integrated close button closes the application");
        }
    }
}
