using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Text;
using System.Linq;
using System.Xml;
using System.Drawing;
using System.Drawing.Imaging;
using StandaloneAssRocket;

static class ConversionTests {
    static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static T Field<T>(MainForm form,string name){return (T)typeof(MainForm).GetField(name,Flags).GetValue(form);}
    static bool HasFont(){using(var fonts=new InstalledFontCollection())return fonts.Families.Any(f=>f.Name=="SubForgeFixture");}
    [STAThread]static int Main(string[] args){try{return RunTests(args);}catch(Exception e){Console.Error.WriteLine("FAIL "+e.GetType().Name+": "+e.Message);return 1;}}
    static int RunTests(string[] args){
        Application.EnableVisualStyles();string root=Path.GetFullPath(args[0]);
        string executable=Path.Combine(root,"app","tools");
        if(HasFont())throw new Exception("Fixture unexpectedly already installed");
        foreach(int engine in new[]{1,0})foreach(bool folder in new[]{false,true}){
            string label=(engine==1?"modern":"compat")+(folder?"-folder":"-fallback");
            using(var form=new MainForm()){
                typeof(MainForm).GetField("tools",Flags).SetValue(form,executable);
                form.Show();Application.DoEvents();
                Field<ComboBox>(form,"engine").SelectedIndex=engine;
                Field<ComboBox>(form,"resolution").SelectedIndex=0;
                Field<ComboBox>(form,"format").SelectedIndex=2;
                Field<TextBox>(form,"output").Text=Path.Combine(root,"qa",label);
                if(folder)Field<TextBox>(form,"fontDirectory").Text=Path.Combine(root,"qa","字体目录 测试");
                typeof(MainForm).GetMethod("AddFiles",Flags).Invoke(form,new object[]{new[]{Path.Combine(root,"qa","fixture.ass")}});
                Task conversion=(Task)typeof(MainForm).GetMethod("ConvertAsync",Flags).Invoke(form,null);
                DateTime deadline=DateTime.UtcNow.AddSeconds(90);
                while(!conversion.IsCompleted){Application.DoEvents();System.Threading.Thread.Sleep(10);if(DateTime.UtcNow>deadline)throw new Exception("Timed out "+label);}
                conversion.GetAwaiter().GetResult();Application.DoEvents();
                var item=Field<ListView>(form,"queue").Items[0];
                string log=Field<TextBox>(form,"log").Text;File.WriteAllText(Path.Combine(root,"qa",label+".log"),log);
                if(item.SubItems[0].Text!="完成")throw new Exception(label+": "+item.SubItems[2].Text);
                if(!File.Exists(Path.Combine(root,"qa",label,"fixture.sup")))throw new Exception("No SUP");
                if(folder&&log.Contains("缺失字体"))throw new Exception("Folder font not recognized");
                if(!log.Contains("跳过空字幕事件"))throw new Exception("Empty event not reported");
                if(engine==1&&folder&&!log.Contains("--fontdir"))throw new Exception("No fontdir argument");
                if(engine==1&&!log.Contains("CPU 模式"))throw new Exception("No CPU configuration");
                Console.WriteLine("PASS "+label+" SUP + BDN conversion");
                form.Close();
            }
            if(HasFont())throw new Exception("Temporary font not unloaded: "+label);
        }
        Console.WriteLine("PASS session font removed after compatibility conversion");
        foreach(int resolution in new[]{0,3})using(var form=new MainForm()){
            typeof(MainForm).GetField("tools",Flags).SetValue(form,executable);
            form.Show();Application.DoEvents();
            Field<ComboBox>(form,"engine").SelectedIndex=1;
            Field<ComboBox>(form,"resolution").SelectedIndex=resolution;
            Field<ComboBox>(form,"format").SelectedIndex=1;
            string destination=Path.Combine(root,"qa",resolution==0?"modern-bdn-palette":"modern-bdn-2160");
            Field<TextBox>(form,"output").Text=destination;
            Field<TextBox>(form,"fontDirectory").Text=Path.Combine(root,"qa","字体目录 测试");
            typeof(MainForm).GetMethod("AddFiles",Flags).Invoke(form,new object[]{new[]{Path.Combine(root,"qa","fixture.ass")}});
            Task conversion=(Task)typeof(MainForm).GetMethod("ConvertAsync",Flags).Invoke(form,null);
            while(!conversion.IsCompleted){Application.DoEvents();System.Threading.Thread.Sleep(10);}
            conversion.GetAwaiter().GetResult();Application.DoEvents();
            if(Field<ListView>(form,"queue").Items[0].SubItems[0].Text!="完成")throw new Exception("BDN conversion failed");
            var xml=new XmlDocument();xml.Load(Path.Combine(destination,"fixture.xml"));
            foreach(XmlNode graphic in xml.SelectNodes("//Graphic"))using(var png=Image.FromFile(Path.Combine(destination,graphic.InnerText.Trim()))){
                if(png.Width!=int.Parse(graphic.Attributes["Width"].Value)||png.Height!=int.Parse(graphic.Attributes["Height"].Value))throw new Exception("BDN dimensions mismatch");
                // GDI+ can expand transparent indexed PNG to ARGB on load.
                // Inspect the actual PNG IHDR color type instead.
                bool indexed=File.ReadAllBytes(Path.Combine(destination,graphic.InnerText.Trim()))[25]==3;
                if(indexed!=(resolution==0))throw new Exception("Wrong PNG palette mode");
            }
            if(resolution==3&&xml.SelectSingleNode("//Format").Attributes["VideoFormat"].Value!="2160p")throw new Exception("High-resolution canvas mismatch");
            Console.WriteLine("PASS "+(resolution==0?"modern 720p BDN indexed PNG":"modern 2160p BDN scaled RGBA dimensions"));form.Close();
        }
        using(var form=new MainForm()){
            typeof(MainForm).GetField("tools",Flags).SetValue(form,executable);
            form.Show();Application.DoEvents();
            Field<TextBox>(form,"fontDirectory").Text=Path.Combine(root,"qa","字体目录 测试");
            Field<TextBox>(form,"output").Text=Path.Combine(root,"qa","cancel-close");
            typeof(MainForm).GetMethod("AddFiles",Flags).Invoke(form,new object[]{new[]{Path.Combine(root,"qa","fixture.ass")}});
            Task conversion=(Task)typeof(MainForm).GetMethod("ConvertAsync",Flags).Invoke(form,null);
            form.Close();
            DateTime deadline=DateTime.UtcNow.AddSeconds(30);
            while(!conversion.IsCompleted){Application.DoEvents();System.Threading.Thread.Sleep(10);if(DateTime.UtcNow>deadline)throw new Exception("Close/cancel timeout");}
            conversion.GetAwaiter().GetResult();Application.DoEvents();
        }
        if(HasFont())throw new Exception("Font retained after cancel/close");
        Console.WriteLine("PASS closing during conversion cancels and unloads temporary font");
        return 0;
    }
}
