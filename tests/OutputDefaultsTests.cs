using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using StandaloneAssRocket;

static class OutputDefaultsTests {
    const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static T Field<T>(MainForm form,string name){return (T)typeof(MainForm).GetField(name,Flags).GetValue(form);}
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
    static void Convert(MainForm form){
        var task=(Task)typeof(MainForm).GetMethod("ConvertAsync",Flags).Invoke(form,null);var deadline=DateTime.UtcNow.AddSeconds(120);
        while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(10);if(DateTime.UtcNow>deadline)throw new Exception("Default-output conversion timeout");}
        task.GetAwaiter().GetResult();Application.DoEvents();
        foreach(ListViewItem item in Field<ListView>(form,"queue").Items)Check(item.SubItems[0].Text=="完成","batch output completed: "+item.SubItems[2].Text);
    }
    static void VerifyBdn(string path){var xml=new XmlDocument();xml.Load(path);Check(xml.SelectNodes("//Graphic").Count>0&&xml.SelectNodes("//Graphic").Cast<XmlNode>().All(x=>File.Exists(Path.Combine(Path.GetDirectoryName(path),x.InnerText.Trim()))),"BDN PNG references resolve beside source: "+path);}
    [STAThread]static int Main(string[] args){try{Run(Path.GetFullPath(args[0]));return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
    static void Run(string root){
        Application.EnableVisualStyles();string work=Path.Combine(root,"qa","output-defaults-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
        byte[] original=File.ReadAllBytes(Path.Combine(root,"qa","fixture.ass"));
        foreach(int engine in new[]{1,0}){
            string sourceA=Path.Combine(work,"engine-"+engine,"a"),sourceB=Path.Combine(work,"engine-"+engine,"b");Directory.CreateDirectory(sourceA);Directory.CreateDirectory(sourceB);
            string a=Path.Combine(sourceA,"shared.ass"),b=Path.Combine(sourceB,"shared.ass");File.WriteAllBytes(a,original);File.WriteAllBytes(b,original);
            string unrelated=Path.Combine(sourceA,".work-keep");Directory.CreateDirectory(unrelated);File.WriteAllText(Path.Combine(unrelated,"notes.txt"),"keep source folder contents");
            using(var form=new MainForm()){
                typeof(MainForm).GetField("tools",Flags).SetValue(form,Path.Combine(root,"app","tools"));form.Show();Application.DoEvents();
                Check(Field<UiSelect>(form,"engine").SelectedIndex==1,"default engine is libass");Field<UiSelect>(form,"engine").SelectedIndex=engine;
                Field<UiSelect>(form,"resolution").SelectedIndex=0;Field<UiSelect>(form,"format").SelectedIndex=2;Field<TextBox>(form,"fontDirectory").Text=Path.Combine(root,"qa","字体目录 测试");
                Check(String.IsNullOrWhiteSpace(Field<TextBox>(form,"output").Text),"default output field uses source-directory mode");
                typeof(MainForm).GetMethod("AddFiles",Flags).Invoke(form,new object[]{new[]{a,b}});
                Check(String.IsNullOrWhiteSpace(Field<TextBox>(form,"output").Text)&&form.ResolveOutputDirectory(a)==sourceA&&form.ResolveOutputDirectory(b)==sourceB,"adding files from different folders retains per-file output directories");
                Convert(form);
                foreach(string source in new[]{sourceA,sourceB}){
                    Check(File.Exists(Path.Combine(source,"shared.sup"))&&File.Exists(Path.Combine(source,"shared.xml")),"SUP and BDN use original basename in each source directory: engine "+engine);
                    VerifyBdn(Path.Combine(source,"shared.xml"));Check(!Directory.Exists(Path.Combine(source,"subtitle-output"))&&!File.Exists(Path.Combine(source,"shared_2.sup")),"no subtitle-output folder or unnecessary suffix is created");
                    Check(File.ReadAllBytes(Path.Combine(source,"shared.ass")).SequenceEqual(original),"conversion preserves original ASS");
                }
                Check(File.Exists(Path.Combine(unrelated,"notes.txt")),"source-directory mode does not clean unrelated .work folders");
                form.Close();
            }
            if(engine!=0)continue;
            using(var form=new MainForm()){
                typeof(MainForm).GetField("tools",Flags).SetValue(form,Path.Combine(root,"app","tools"));form.Show();Application.DoEvents();
                Field<UiSelect>(form,"engine").SelectedIndex=engine;Field<UiSelect>(form,"resolution").SelectedIndex=0;Field<UiSelect>(form,"format").SelectedIndex=1;Field<TextBox>(form,"fontDirectory").Text=Path.Combine(root,"qa","字体目录 测试");
                string common=Path.Combine(work,"common-output");Field<TextBox>(form,"output").Text=common;
                typeof(MainForm).GetMethod("AddFiles",Flags).Invoke(form,new object[]{new[]{a,b}});Check(Field<TextBox>(form,"output").Text==common&&form.ResolveOutputDirectory(a)==common,"explicit common output directory remains selected after adding files");
                Convert(form);Check(File.Exists(Path.Combine(common,"shared.xml"))&&File.Exists(Path.Combine(common,"shared_2.xml")),"common output directory reserves distinct names for colliding source basenames");VerifyBdn(Path.Combine(common,"shared.xml"));VerifyBdn(Path.Combine(common,"shared_2.xml"));
                Field<TextBox>(form,"output").Clear();Check(form.ResolveOutputDirectory(a)==sourceA&&form.ResolveOutputDirectory(b)==sourceB,"clearing output directory restores source-directory mode");form.Close();
            }
        }
        Console.WriteLine("All default output checks passed.");
    }
}
