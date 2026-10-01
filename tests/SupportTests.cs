using System;
using System.IO;
using System.Linq;
using System.Threading;
using StandaloneAssRocket;

static class SupportTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static int Main(string[] args){
        string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
        string ini=Path.Combine(root,"config.ini");
        const string original="[PILIQ]\r\nquality=95\r\n[SUPer]\r\nuse_gpu=1\r\nlayout_mode=2\r\n[Other]\r\nuse_gpu=7\r\n";
        File.WriteAllText(ini,original);byte[] bytes=File.ReadAllBytes(ini);
        using(var cpu=SuperConfigLease.Acquire(root,false,CancellationToken.None).GetAwaiter().GetResult()){
            string config=File.ReadAllText(ini);
            Check(config.Contains("use_gpu=0"),"CPU selection writes use_gpu=0");
            Check(config.Contains("[Other]\r\nuse_gpu=7"),"other INI sections unchanged");
            using(var cancel=new CancellationTokenSource(200)){
                try{SuperConfigLease.Acquire(root,true,cancel.Token).GetAwaiter().GetResult();throw new Exception("lease not exclusive");}
                catch(OperationCanceledException){Console.WriteLine("PASS concurrent lease waits and can be cancelled");}
            }
        }
        Check(File.ReadAllBytes(ini).SequenceEqual(bytes),"original config restored byte for byte");
        using(var gpu=SuperConfigLease.Acquire(root,true,CancellationToken.None).GetAwaiter().GetResult())Check(File.ReadAllText(ini).Contains("use_gpu=1"),"GPU selection writes use_gpu=1");
        Check(SuperConfigLease.Configure("[SUPer]\nlayout_mode=2\n[Other]\nvalue=1\n",false).Contains("[SUPer]\nuse_gpu=0"),"missing GPU key inserted in correct section");
        Check(SuperConfigLease.Configure("[Other]\nvalue=1\n",false).Contains("[SUPer]\r\nuse_gpu=0"),"missing SUPer section added");
        var fonts=Path.Combine(root,"fonts-input");Directory.CreateDirectory(Path.Combine(fonts,"nested"));
        File.WriteAllText(Path.Combine(fonts,"one.TTF"),"a");File.WriteAllText(Path.Combine(fonts,"nested","two.otf"),"b");File.WriteAllText(Path.Combine(fonts,"ignore.txt"),"c");
        string[] found=FontDirectory.ReadFiles(fonts);Check(found.Length==2,"font search includes subfolders and filters extensions");
        string staged=FontDirectory.Stage(found,root);Check(Directory.GetFiles(staged).Length==2,"staging preserves distinct fonts with safe filenames");
        try{FontDirectory.ReadFiles(Path.Combine(root,"nonexistent"));throw new Exception("missing directory accepted");}catch(ConversionException){Console.WriteLine("PASS missing font directory rejected");}
        Console.WriteLine("All support checks passed.");return 0;
    }
}
