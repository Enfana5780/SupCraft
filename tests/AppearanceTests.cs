using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using StandaloneAssRocket;

static class AppearanceTests {
    static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static T Field<T>(MainForm form,string name){return (T)typeof(MainForm).GetField(name,Flags).GetValue(form);}
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wp,IntPtr lp);
    static bool Bright(Color color){return color.R>210&&color.G>210&&color.B>210;}
    static void CheckNumeric(CleanNumeric numeric,InputShell shell,bool disabled,string state){
        numeric.ArrangeEditor();shell.Refresh();Application.DoEvents();
        foreach(Control child in numeric.Controls){
            if(child is TextBoxBase)Check(child.Visible!=disabled,"native editor visibility in "+state);
            else using(var graphics=Graphics.FromHwnd(numeric.Handle))Check(!child.Visible&&child.Region!=null&&child.Region.IsEmpty(graphics),"native arrows cannot paint into "+state);
        }
        using(var bitmap=new Bitmap(shell.Width,shell.Height)){
            shell.DrawToBitmap(bitmap,shell.ClientRectangle);int black=0;
            for(int y=2;y<bitmap.Height-2;y++)for(int x=numeric.Right-3;x<numeric.Right+6&&x<bitmap.Width-2;x++){var color=bitmap.GetPixel(x,y);if(color.R<8&&color.G<8&&color.B<8)black++;}
            Check(black==0,"numeric trailing area has no black block in "+state);
            bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"numeric-"+state+".png"));
        }
    }
    [STAThread]static int Main(){try{Run();return 0;}catch(Exception exception){Console.Error.WriteLine(exception);return 1;}finally{UiTheme.UseDark(false);}}
    static void Run(){
        Application.EnableVisualStyles();
        using(var form=new MainForm()){
            form.Show();Application.DoEvents();Application.DoEvents();
            var toggle=Field<Button>(form,"themeButton");var settings=Field<Panel>(form,"settingsCard");var files=Field<Panel>(form,"filesCard");
            var engine=Field<UiSelect>(form,"engine");var hdr=Field<CheckBox>(form,"hdr");var dim=Field<CleanNumeric>(form,"dim");var shell=Field<InputShell>(form,"dimShell");
            var add=(Button)Field<Control>(form,"fileActionsPanel").Controls[0];
            engine.SelectedIndex=0;
            Size fixedSize=settings.Size;int fileHeight=files.Height;string addText=add.Text;
            for(int iteration=0;iteration<4;iteration++){
                toggle.PerformClick();Application.DoEvents();bool dark=iteration%2==0;
                Check(form.IsDarkMode==dark&&toggle.Text.Contains(dark?"浅色":"深色"),"top-right switch updates theme and target label");
                Check(settings.Size==fixedSize&&files.Height==fileHeight&&engine.SelectedIndex==0,"theme switch preserves layout and conversion settings");
                Check(Field<Panel>(form,"statusPanel").BackColor==UiTheme.Surface&&Field<Label>(form,"status").BackColor==UiTheme.Surface&&Field<Label>(form,"elapsedLabel").BackColor==UiTheme.Surface&&Field<UiProgress>(form,"progress").BackColor==UiTheme.Surface,"progress content matches settings surface after theme change");
                Check(add.Text==addText&&add.Enabled,"theme switch preserves add action text");
                using(var bitmap=new Bitmap(add.Width,add.Height)){
                    add.DrawToBitmap(bitmap,add.ClientRectangle);int white=0;
                    for(int y=8;y<bitmap.Height-8;y++)for(int x=8;x<bitmap.Width-8;x++)if(Bright(bitmap.GetPixel(x,y)))white++;
                    bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"add-"+iteration+".png"));
                    Check(white>20,"primary action label remains visible after theme switch: "+white);
                }
                if(dark){Check(settings.BackColor.GetBrightness()<.3F&&Field<TextBox>(form,"log").BackColor.GetBrightness()<.3F,"cards and logs use dark backgrounds");Check(Field<TextBox>(form,"language").ForeColor.GetBrightness()>.65F,"dark input text retains contrast");}
                CheckNumeric(dim,shell,true,(dark?"dark":"light")+"-disabled");
                engine.SelectedIndex=1;hdr.Checked=true;dim.Focus();Application.DoEvents();
                dim.Text="42.5";Field<Button>(form,"start").Focus();Application.DoEvents();Check(dim.Value==42.5M,"brightness remains editable and commits decimal values");
                CheckNumeric(dim,shell,false,(dark?"dark":"light")+"-enabled");
                dim.Focus();hdr.Checked=false;engine.SelectedIndex=0;Application.DoEvents();
                CheckNumeric(dim,shell,true,(dark?"dark":"light")+"-after-disable");
                var point=form.PointToScreen(new Point(Field<Panel>(form,"headerPanel").Left+toggle.Left+toggle.Width/2,toggle.Top+toggle.Height/2));
                var hit=SendMessage(form.Handle,0x84,IntPtr.Zero,new IntPtr((point.X&0xffff)|((point.Y&0xffff)<<16)));
                Check(hit.ToInt32()!=2,"theme switch is excluded from title-bar dragging");
            }
            engine.SelectedIndex=0;Field<UiSelect>(form,"resolution").SelectedIndex=4;Field<NumericUpDown>(form,"customWidth").Text="1441";toggle.PerformClick();Application.DoEvents();
            Check(Field<NoticeLabel>(form,"customValidation").Visible&&!Field<Button>(form,"start").Enabled,"invalid custom canvas stays blocked in dark mode");
            Field<NumericUpDown>(form,"customWidth").Text="1440";engine.Open();Application.DoEvents();Check(engine.Popup!=null&&engine.Popup.BackColor.GetBrightness()<.3F,"dropdown uses the dark palette");engine.Popup.Close();form.SetDarkMode(false);
        }
    }
}
