using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using SkiaSharp;
using SupCraft.Core;

static class Program
{
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Reject(Action action) { try { action(); } catch (ConversionException) { return; } throw new Exception("Invalid input was accepted"); }
    const string Script = "[Script Info]\n[V4+ Styles]\nFormat: Name, Fontname\nStyle: Default,Arial\n[Events]\nFormat: Layer, Start, End, Style, Text\nDialogue: 0,0:00:00.00,0:00:00.00,Default,  \nDialogue: 0,0:00:00.50,0:00:03.20,Default,{\\fad(50,50)}字幕 WMWM\n";
    static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--child-wait") { File.WriteAllText(args[1], Environment.ProcessId.ToString()); await Task.Delay(Timeout.Infinite); return 0; }
        if (args.Length > 0 && args[0] == "--child-echo") { File.WriteAllText(args[1], args[2] + "\n" + Environment.GetEnvironmentVariable("SUPCRAFT_TEST_VALUE")); return 0; }
        string root = Path.Combine(Path.GetTempPath(), "SupCraft-checks-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            if (args.Length > 0 && args[0] == "--native") await Native(Path.GetFullPath(args[1]), root);
            else if (args.Length > 0 && args[0] == "--renderer") await RenderOnly(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), root);
            else await Checks(root);
            Console.WriteLine("PASS: SupCraft checks."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Console.Error.WriteLine("Kept checks at " + root); return 1; }
    }
    static async Task Checks(string root)
    {
        string source = Path.Combine(root, "字幕.ass"), clean = Path.Combine(root, "clean.ass");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        foreach (var encoding in new Encoding[] { new UTF8Encoding(false), new UTF8Encoding(true), Encoding.GetEncoding(936) })
        {
            File.WriteAllText(source, Script, encoding); byte[] original = File.ReadAllBytes(source); var doc = AssDocument.Read(source);
            Require(doc.EventCount == 1 && doc.SkippedLines.Count == 1 && doc.LastEnd == 3.2, "Empty-row preflight/tail regression");
            doc.WriteConversionCopy(clean); Require(File.ReadAllText(clean).Contains("字幕") && !File.ReadAllText(clean).Contains("0:00:00.00"), "Working copy encoding/empty row");
            doc.WriteConversionCopy(clean, true); Require(!File.ReadAllText(clean).Contains("\\fad"), "Tail fade retry");
            Require(File.ReadAllBytes(source).SequenceEqual(original), "Source bytes changed");
        }
        File.WriteAllText(source, Script.Replace("0:00:03.20", "0:00:00.20")); Reject(() => AssDocument.Read(source));
        File.WriteAllText(source, Script); Require(AssDocument.Read(source).Fonts.Contains("Arial"), "Font reference parser");
        Require(ConversionService.Config("/app/quant.dylib").Contains("use_gpu=0\n"), "GPU default must match Windows CPU default");
        Require(ConversionService.Config("/app/quant.dylib", true).Contains("use_gpu=1\n"), "GPU opt-in");
        FontEditingChecks(root);
        Reject(() => ConversionService.AssetPath(root, "../outside.png"));
        Reject(() => ConversionService.AssetPath(root, "/outside.png"));
        var runner = new FakeRunner(); var tools = new Toolchain(source, source, source); var service = new ConversionService(tools, runner);
        string output = Path.Combine(root, "输出目录"); Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "subtitle.sup"), "Existing user output");
        var options = new ConversionOptions(output, null, Format: OutputFormat.Both, UseGpu: true);
        var made = await service.ConvertAsync(source, options, new HashSet<string>(), Console.WriteLine, CancellationToken.None);
        Require(made.Count == 2 && runner.Renders == 2 && runner.ConfiguredGpu, "Both outputs, tail retry and GPU configuration");
        Require(File.ReadAllText(Path.Combine(output, "subtitle.sup")) == "Existing user output", "Previous output overwritten");
        ConversionService.ValidateBdn(made.Single(p => p.EndsWith(".xml")));
        ConversionService.ValidateSup(made.Single(p => p.EndsWith(".sup")), 3.2);
        string highDir = Path.Combine(root, "high");
        var high = await service.ConvertAsync(source, options with { OutputDirectory = highDir, Height = 2160, Format = OutputFormat.Bdn }, new HashSet<string>(), Console.WriteLine, CancellationToken.None);
        var xml = XDocument.Load(high[0]); Require(xml.Descendants("Format").Single().Attribute("VideoFormat")!.Value == "2160p", "High BDN metadata");
        var graphic = xml.Descendants("Graphic").First(); using (var image = SKBitmap.Decode(Path.Combine(highDir, graphic.Value))) Require(image.Width == 80 && image.Height == 40, "High BDN actual PNG size");
        byte[] broken = new byte[14]; broken[0] = (byte)'P'; broken[1] = (byte)'G'; broken[12] = 4;
        string badSup = Path.Combine(root, "bad.sup"); File.WriteAllBytes(badSup, broken); Reject(() => ConversionService.ValidateSup(badSup, 0));
        string firstFolder = Path.Combine(root, "source-one"), secondFolder = Path.Combine(root, "source-two");
        Directory.CreateDirectory(firstFolder); Directory.CreateDirectory(secondFolder);
        var reservations = new HashSet<string>();
        foreach (string folder in new[] { firstFolder, secondFolder })
        {
            string input = Path.Combine(folder, "same.ass"); File.WriteAllText(input, Script);
            var result = await service.ConvertAsync(input, options with { OutputDirectory = "", Format = OutputFormat.Bdn }, reservations, Console.WriteLine, CancellationToken.None);
            Require(result.Single() == Path.Combine(folder, "same.xml"), "Default source-directory output or cross-directory name reservation");
            Require(!Directory.Exists(Path.Combine(folder, "subtitle-output")), "Unexpected output subdirectory");
        }
        Require(ConversionService.ResolveOutputDirectory(source, "  ") == root, "Whitespace means source directory");
        await service.ConvertAsync(source, options with { OutputDirectory = Path.Combine(root, "cpu"), UseGpu = false }, new HashSet<string>(), Console.WriteLine, CancellationToken.None);
        Require(!runner.ConfiguredGpu, "CPU selection must reach encoder configuration");
        await ProcessChecks(root);
    }
    static void FontEditingChecks(string root)
    {
        const string text = "[Script Info]\r\nTitle: keep me\r\n\r\n[V4+ Styles]\r\nFormat: Name, Fontname, Fontsize, Bold\r\nStyle: Default, @Arial ,62,-1\r\n[Events]\r\nFormat: Layer, Start, End, Style, Text\r\nDialogue: 0,0:00:00.50,0:00:03.20,Default,{\\fnArial\\b1}字幕{\\t(0,300,\\fnArial)}{\\fn}plain \\fnNotATag\r\n";
        var map = new Dictionary<string, string> { [AssFontDocument.Key("Arial")] = "Helvetica" };
        int number = 0;
        foreach (var encoding in new Encoding[] { new UTF8Encoding(false, true), new UTF8Encoding(true, true), Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback), new UnicodeEncoding(false, true, true), new UnicodeEncoding(true, true, true) })
        {
            string file = Path.Combine(root, $"font-{number++}.ass"); File.WriteAllText(file, text, encoding);
            byte[] original = File.ReadAllBytes(file); var document = AssFontDocument.Read(file);
            Require(document.References.Count == 3 && document.References[0].Line == 6 && document.References[1].Line == 9, "Font parser: style, inline, transform, line numbers, plain text");
            var replaced = document.Replace(map, out int count); Require(count == 3, "Replacement count");
            string expected = text.Replace("@Arial", "@Helvetica").Replace("\\fnArial", "\\fnHelvetica");
            Require(replaced.SequenceEqual(encoding.GetPreamble().Concat(encoding.GetBytes(expected))), "Encoding/BOM/line endings/unrelated fields changed");
            string backup = document.Save(map, out count); Require(File.ReadAllBytes(backup).SequenceEqual(original), "Original backup bytes");
            Require(File.ReadAllBytes(file).SequenceEqual(replaced), "Saved font replacement");
            var current = AssFontDocument.Read(file); string next = current.Save(new Dictionary<string, string> { ["helvetica"] = "Arial" }, out count);
            Require(next != backup && File.Exists(backup) && File.ReadAllBytes(next).SequenceEqual(replaced), "Previous backup overwritten");
            Require(AssDocument.Read(file).Fonts.Contains("Arial"), "Conversion preflight must support font-editor encodings");
        }
        string gbkFile = Path.Combine(root, "unencodable.ass"); File.WriteAllText(gbkFile, text, Encoding.GetEncoding(936)); var gbk = AssFontDocument.Read(gbkFile);
        try { gbk.Save(new Dictionary<string, string> { ["arial"] = "Font😀" }, out _); throw new Exception("Unencodable name accepted"); } catch (InvalidDataException) { }
        Require(File.ReadAllBytes(gbkFile).SequenceEqual(gbk.Original), "Unencodable replacement changed original");
        File.AppendAllText(gbkFile, "; external edit"); var external = File.ReadAllBytes(gbkFile);
        try { gbk.Save(map, out _); throw new Exception("Stale source accepted"); } catch (IOException) { }
        Require(File.ReadAllBytes(gbkFile).SequenceEqual(external), "Stale replacement overwrote external edit");
        try { gbk.Replace(new Dictionary<string, string> { ["arial"] = "invalid,font" }, out _); throw new Exception("Invalid font accepted"); } catch (ArgumentException) { }
        Console.WriteLine("PASS: font references, batch replacements, encodings, backups, concurrent edit protection.");
    }
    static (string Executable, List<string> Prefix) Self()
    {
        string executable = Environment.ProcessPath!;
        return (executable, Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? [typeof(Program).Assembly.Location] : []);
    }
    static async Task ProcessChecks(string root)
    {
        var self = Self(); var runner = new ToolRunner(); string echo = Path.Combine(root, "argument echo.txt");
        string literal = "space 中文 ; $(must-not-run) ' \"";
        await runner.RunAsync(self.Executable, self.Prefix.Concat(["--child-echo", echo, literal]).ToArray(), root, new Dictionary<string, string> { ["SUPCRAFT_TEST_VALUE"] = "per-job value" }, Console.WriteLine, CancellationToken.None);
        Require(File.ReadAllText(echo) == literal + "\nper-job value", "ArgumentList/environment isolation");
        using var cancel = new CancellationTokenSource(); string pidFile = Path.Combine(root, "pid.txt");
        var waiting = runner.RunAsync(self.Executable, self.Prefix.Concat(["--child-wait", pidFile]).ToArray(), root, null, Console.WriteLine, cancel.Token);
        for (int i = 0; i < 100 && !File.Exists(pidFile); i++) await Task.Delay(50);
        Require(File.Exists(pidFile), "Cancellation helper did not start"); int pid = int.Parse(File.ReadAllText(pidFile)); cancel.Cancel();
        try { await waiting.WaitAsync(TimeSpan.FromSeconds(15)); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { }
        try { using var process = Process.GetProcessById(pid); Require(process.HasExited, "Cancelled child still running"); } catch (ArgumentException) { }
    }
    static async Task Native(string app, string root)
    {
        var project = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        string fixture = Path.Combine(project, "vendor/native-fixture");
        var tools = Toolchain.Load(Path.Combine(app, "Contents/Resources/tools/toolchain.json"));
        await RenderOnly(tools.Renderer, fixture, root);
        string input = Path.Combine(fixture, "fixture.ass"), fonts = Path.Combine(fixture, "字体目录 测试");
        var service = new ConversionService(tools);
        var outputs = await service.ConvertAsync(input, new ConversionOptions(Path.Combine(root, "both"), fonts, Format: OutputFormat.Both), new HashSet<string>(), Console.WriteLine, CancellationToken.None);
        Require(outputs.Count == 2, "Native both output missing");
        string executable = Path.Combine(app, "Contents/MacOS/SupCraft");
        await new ToolRunner().RunAsync(executable, ["--convert", "--input", input, "--output", Path.Combine(root, "bundle-cli"), "--fonts", fonts, "--format", "both", "--fps", "23.976", "--gpu"], root, null, Console.WriteLine, CancellationToken.None);
        string localInput = Path.Combine(root, "source output.ass"); File.Copy(input, localInput);
        await new ToolRunner().RunAsync(executable, ["--convert", "--input", localInput, "--fonts", fonts, "--format", "bdn"], root, null, Console.WriteLine, CancellationToken.None);
        ConversionService.ValidateBdn(Path.Combine(root, "source_output.xml"));
        Console.WriteLine("PASS: native packaged CLI, SUP + BDN, directory fonts, CPU default, GPU opt-in, source-directory output.");
    }
    static async Task RenderOnly(string renderer, string fixture, string root)
    {
        string input = Path.Combine(fixture, "fixture.ass"), fonts = Path.Combine(fixture, "字体目录 测试");
        Require(!FontCatalog.Create(null).Available("SupCraftMacFixture"), "Fixture was already installed");
        Require(FontCatalog.Create(fonts).Available("SupCraftMacFixture"), "Directory font alias not recognized");
        var service = new ConversionService(new Toolchain(renderer, renderer, renderer));
        var fallback = await service.ConvertAsync(input, new ConversionOptions(Path.Combine(root, "fallback"), null, Format: OutputFormat.Bdn, PalettePng: false), new HashSet<string>(), Console.WriteLine, CancellationToken.None);
        var selected = await service.ConvertAsync(input, new ConversionOptions(Path.Combine(root, "selected"), fonts, Format: OutputFormat.Bdn, PalettePng: false), new HashSet<string>(), Console.WriteLine, CancellationToken.None);
        string FirstImage(string xml) => Path.Combine(Path.GetDirectoryName(xml)!, XDocument.Load(xml).Descendants("Graphic").First().Value);
        Require(!File.ReadAllBytes(FirstImage(fallback[0])).SequenceEqual(File.ReadAllBytes(FirstImage(selected[0]))), "Directory font did not affect real rendering");
        var palette = await service.ConvertAsync(input, new ConversionOptions(Path.Combine(root, "palette"), fonts, Height: 720, Format: OutputFormat.Bdn), new HashSet<string>(), Console.WriteLine, CancellationToken.None);
        Require(File.ReadAllBytes(FirstImage(palette[0]))[25] == 3, "Palette BDN was not indexed PNG");
        var high = await service.ConvertAsync(input, new ConversionOptions(Path.Combine(root, "high-native"), fonts, Height: 2160, Format: OutputFormat.Bdn), new HashSet<string>(), Console.WriteLine, CancellationToken.None);
        Require(XDocument.Load(high[0]).Descendants("Format").Single().Attribute("VideoFormat")!.Value == "2160p", "Native high BDN missing");
        Require(!FontCatalog.Create(null).Available("SupCraftMacFixture"), "Font was permanently registered");
        Console.WriteLine("PASS: actual libass font-directory render, palette PNG, 2160p BDN and no font installation.");
    }
    sealed class FakeRunner : IToolRunner
    {
        public int Renders; public bool ConfiguredGpu;
        public Task RunAsync(string executable, IReadOnlyList<string> arguments, string work, IReadOnlyDictionary<string, string>? environment, Action<string> log, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (environment == null)
            {
                Renders++; string image = Path.Combine(work, "image.png"); using var bitmap = new SKBitmap(40, 20); bitmap.Erase(SKColors.White); using var encoded = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100); File.WriteAllBytes(image, encoded.ToArray());
                string tail = Renders == 1 ? "00:00:00:12" : "00:00:03:05";
                new XDocument(new XElement("BDN", new XElement("Description", new XElement("Format", new XAttribute("VideoFormat", "1080p"))), new XElement("Events", new XElement("Event", new XAttribute("OutTC", tail), new XElement("Graphic", new XAttribute("Width", 40), new XAttribute("Height", 20), new XAttribute("X", 10), new XAttribute("Y", 10), "image.png"))))).Save(Path.Combine(work, "bdn.xml"));
            }
            else
            {
                string config = File.ReadAllText(environment["SUPCRAFT_JOB_CONFIG"]); ConfiguredGpu = config.Contains("use_gpu=1");
                byte[] data = new byte[13]; data[0] = (byte)'P'; data[1] = (byte)'G'; BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(2, 4), 4 * 90000); File.WriteAllBytes(arguments[^1], data);
            }
            return Task.CompletedTask;
        }
    }
}
