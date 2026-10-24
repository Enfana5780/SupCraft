using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Drawing;
using StandaloneAssRocket;

static class CustomCanvasTests {
    static readonly BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static T Field<T>(MainForm form,string name){return (T)typeof(MainForm).GetField(name,Flags).GetValue(form);}
    static object Call(MainForm form,string name,params object[] args){return typeof(MainForm).GetMethod(name,Flags).Invoke(form,args);}
    static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static void Rejected(string path){try{CustomCanvas.ValidateOutput(path,1440,3200);throw new Exception("Bad canvas accepted: "+path);}catch(ConversionException){Console.WriteLine("PASS rejects "+Path.GetFileName(path));}}
    static void Pump(Task task){DateTime deadline=DateTime.UtcNow.AddSeconds(90);while(!task.IsCompleted){Application.DoEvents();System.Threading.Thread.Sleep(10);if(DateTime.UtcNow>deadline)throw new Exception("Conversion timeout");}task.GetAwaiter().GetResult();Application.DoEvents();}
    [STAThread]static int Main(string[] args){try{Run(Path.GetFullPath(args[0]));return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
    static void Run(string root){
        Application.EnableVisualStyles();
        using(var form=new MainForm()){
            form.Show();Application.DoEvents();
            var engine=Field<UiSelect>(form,"engine");var resolution=Field<UiSelect>(form,"resolution");var format=Field<UiSelect>(form,"format");var width=Field<NumericUpDown>(form,"customWidth");var height=Field<NumericUpDown>(form,"customHeight");var group=Field<Control>(form,"customSizeGroup");
            engine.SelectedIndex=0;
            Check(resolution.Items.Count==5&&!group.Visible&&!width.Enabled,"preset hides custom size fields");
            resolution.SelectedIndex=4;Application.DoEvents();
            Check(group.Visible&&width.Enabled&&height.Enabled,"AviSynth exposes custom size fields");
            Check(width.Value==1440&&height.Value==3200&&width.Minimum==64&&width.Maximum==4096,"custom defaults and dimension limits");
            try{CustomCanvas.ValidateSize(1001,1601);throw new Exception("Unaligned frame accepted");}catch(ConversionException){Console.WriteLine("PASS rejects SSE2-incompatible frame size before conversion");}
            try{CustomCanvas.ValidateSize(1001,1600);throw new Exception("Non-multiple-of-four width accepted");}catch(ConversionException){}
            Check((string)Call(form,"ResolutionName")=="1440x3200","custom format carries both dimensions");
            engine.SelectedIndex=1;Application.DoEvents();
            Check(resolution.Items.Count==4&&resolution.SelectedIndex==1&&!group.Visible&&!width.Enabled,"libass restores its original presets");
            Check((string)Call(form,"ResolutionName")=="1080p"&&((int[])Call(form,"Resolution"))[0]==1920,"custom values cannot leak into libass");
            resolution.SelectedIndex=3;Check(format.SelectedIndex==1&&!format.Enabled,"libass high resolution remains BDN only");
            engine.SelectedIndex=0;resolution.SelectedIndex=4;Application.DoEvents();
            Check(format.Enabled&&width.Value==1440&&height.Value==3200,"switching back preserves custom dimensions");
            Check(Field<Label>(form,"resolutionWarning").Visible,"custom displays a persistent non-Blu-ray warning");
            var clock=Field<System.Diagnostics.Stopwatch>(form,"batchClock");
            Call(form,"StartElapsed");System.Threading.Thread.Sleep(1100);Application.DoEvents();
            Check(clock.IsRunning&&Field<Label>(form,"elapsedLabel").Text=="已运行 00:00:01","elapsed timer updates while running");
            Call(form,"StopElapsed");var stopped=clock.Elapsed;System.Threading.Thread.Sleep(50);Check(!clock.IsRunning&&clock.Elapsed==stopped,"elapsed timer stops and retains final duration");
            Call(form,"StartElapsed");Check(Field<Label>(form,"elapsedLabel").Text=="已运行 00:00:00","new batch resets elapsed duration");Call(form,"StopElapsed");
            var elapsed=typeof(MainForm).GetMethod("FormatElapsed",BindingFlags.NonPublic|BindingFlags.Static);
            Check((string)elapsed.Invoke(null,new object[]{TimeSpan.FromSeconds(3723)})=="01:02:03","elapsed format is hours:minutes:seconds");
            Check((string)elapsed.Invoke(null,new object[]{TimeSpan.FromHours(25)})=="25:00:00","elapsed hours do not wrap after one day");
            form.Close();
        }
        foreach(int outputFormat in new[]{2,0,1})using(var form=new MainForm()){
            typeof(MainForm).GetField("tools",Flags).SetValue(form,Path.Combine(root,"app","tools"));form.Show();Application.DoEvents();
            Field<UiSelect>(form,"engine").SelectedIndex=0;Field<UiSelect>(form,"resolution").SelectedIndex=4;Field<UiSelect>(form,"fps").SelectedIndex=1;Field<UiSelect>(form,"format").SelectedIndex=outputFormat;Field<CheckBox>(form,"strict").Checked=true;
            string destination=Path.Combine(root,"qa","compat-custom-"+outputFormat);Field<TextBox>(form,"output").Text=destination;
            // Exercise non-even dimensions as well as the requested portrait canvas.
            int w=outputFormat==0?1000:1440,h=outputFormat==0?1600:3200;
            Field<NumericUpDown>(form,"customWidth").Value=w;Field<NumericUpDown>(form,"customHeight").Value=h;
            string input=Path.Combine(root,"qa","canvas-probe","portrait.ass");byte[] original=File.ReadAllBytes(input);
            Call(form,"AddFiles",new object[]{new[]{input}});
            var conversion=(Task)Call(form,"ConvertAsync");
            Check(!Field<NumericUpDown>(form,"customWidth").Enabled&&!Field<NumericUpDown>(form,"customHeight").Enabled,"batch locks custom dimensions");
            Check(Field<System.Diagnostics.Stopwatch>(form,"batchClock").IsRunning,"conversion starts elapsed clock");
            Pump(conversion);
            Check(!Field<System.Diagnostics.Stopwatch>(form,"batchClock").IsRunning,"completion stops elapsed clock");
            File.WriteAllText(Path.Combine(root,"qa","compat-custom-"+outputFormat+".log"),Field<TextBox>(form,"log").Text);
            var item=Field<ListView>(form,"queue").Items[0];Check(item.SubItems[0].Text=="完成","custom conversion format "+outputFormat+": "+item.SubItems[2].Text);
            if(outputFormat!=1)CustomCanvas.ValidateOutput(Path.Combine(destination,"portrait.sup"),w,h);
            if(outputFormat!=0)CustomCanvas.ValidateOutput(Path.Combine(destination,"portrait.xml"),w,h);
            Check(original.SequenceEqual(File.ReadAllBytes(input)),"original ASS unchanged");
            Check(Field<NumericUpDown>(form,"customWidth").Enabled,"custom fields restored after batch");
            form.Close();
        }
        foreach(int preset in new[]{2,3})using(var form=new MainForm()){
            typeof(MainForm).GetField("tools",Flags).SetValue(form,Path.Combine(root,"app","tools"));form.Show();Application.DoEvents();
            Field<UiSelect>(form,"engine").SelectedIndex=0;Field<UiSelect>(form,"resolution").SelectedIndex=preset;Field<UiSelect>(form,"format").SelectedIndex=2;
            Check(Field<Label>(form,"resolutionWarning").Visible,"high preset shows non-Blu-ray warning");
            Check(!Field<Control>(form,"customSizeGroup").Visible&&Field<UiSelect>(form,"format").Enabled,"high preset supports all outputs without custom inputs");
            string destination=Path.Combine(root,"qa","compat-preset-"+preset);Field<TextBox>(form,"output").Text=destination;
            Call(form,"AddFiles",new object[]{new[]{Path.Combine(root,"qa","fixture.ass")}});Pump((Task)Call(form,"ConvertAsync"));
            var item=Field<ListView>(form,"queue").Items[0];Check(item.SubItems[0].Text=="完成","high preset conversion: "+item.SubItems[2].Text);
            int w=preset==2?2560:3840,h=preset==2?1440:2160;CustomCanvas.ValidateOutput(Path.Combine(destination,"fixture.sup"),w,h);CustomCanvas.ValidateOutput(Path.Combine(destination,"fixture.xml"),w,h);
            Field<UiSelect>(form,"resolution").SelectedIndex=1;Check(!Field<Label>(form,"resolutionWarning").Visible,"standard preset clears warning");form.Close();
        }
        string source=Path.Combine(root,"qa","compat-custom-2"),badXml=Path.Combine(source,"bad.xml");
        foreach(string attribute in new[]{"VideoFormat","X","Width"}){
            var d=new XmlDocument();d.Load(Path.Combine(source,"portrait.xml"));
            if(attribute=="VideoFormat")d.SelectSingleNode("//Format").Attributes[attribute].Value="1080p";
            else d.SelectSingleNode("//Graphic").Attributes[attribute].Value=attribute=="X"?"1440":"1";
            d.Save(badXml);Rejected(badXml);
        }
        byte[] bytes=File.ReadAllBytes(Path.Combine(source,"portrait.sup"));int offset=0,lastPcs=-1;
        while(offset<bytes.Length){if(bytes[offset+10]==0x16)lastPcs=offset+13;offset+=13+bytes[offset+11]*256+bytes[offset+12];}
        Check(lastPcs>=0,"SUP has composition segments");bytes[lastPcs]=0;bytes[lastPcs+1]=1;
        string badSup=Path.Combine(source,"bad-clear.sup");File.WriteAllBytes(badSup,bytes);Rejected(badSup);
        File.Delete(badXml);File.Delete(badSup);
        Console.WriteLine("All custom canvas checks passed.");
    }
}
