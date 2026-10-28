using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using SupCraft.Core;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace SupCraft.Mac;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--convert") return Convert(args).GetAwaiter().GetResult();
        Build().StartWithClassicDesktopLifetime(args); return 0;
    }
    public static AppBuilder Build() => AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
    static async Task<int> Convert(string[] args)
    {
        try
        {
            string Value(string key, string fallback = "") { int index = Array.IndexOf(args, key); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }
            string input = Value("--input"), output = Value("--output");
            if (input.Length == 0) throw new ArgumentException("--convert requires --input.");
            string format = Value("--format", "sup");
            var options = new ConversionOptions(output, Value("--fonts"), int.Parse(Value("--height", "1080")), double.Parse(Value("--fps", "23.976"), System.Globalization.CultureInfo.InvariantCulture), format switch { "bdn" => OutputFormat.Bdn, "both" => OutputFormat.Both, "sup" => OutputFormat.Sup, _ => throw new ArgumentException("Invalid --format") }, Hdr: args.Contains("--hdr"), UseGpu: args.Contains("--gpu"));
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
            var service = new ConversionService(Toolchain.Load(Value("--toolchain", Toolchain.BundledManifest)));
            await service.ConvertAsync(input, options, new HashSet<string>(StringComparer.OrdinalIgnoreCase), Console.WriteLine, cts.Token);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

public sealed class App : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); Appearance.Apply(false); }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.Args?.Contains("--dark") == true) Appearance.Apply(true);
            var window = new MainWindow(); desktop.MainWindow = window;
            if (desktop.Args?.Contains("--self-test-ui") == true)
            {
                if (desktop.Args.Contains("--compact")) { window.Width = window.MinWidth; window.Height = window.MinHeight; }
                int preview = Array.IndexOf(desktop.Args, "--preview-ass");
                if (preview >= 0 && preview + 1 < desktop.Args.Length) window.AddFiles([desktop.Args[preview + 1]]);
                Window capture = window;
                if (desktop.Args.Contains("--font-manager"))
                {
                    string[] inputs = preview >= 0 && preview + 1 < desktop.Args.Length ? [desktop.Args[preview + 1]] : [];
                    var manager = new FontManagerWindow(inputs, null, _ => { }, Console.WriteLine);
                    window.Opened += (_, _) => manager.Show(window); capture = manager;
                }
                DispatcherTimer.RunOnce(() =>
                {
                    if (desktop.Args.Contains("--check-ui")) window.CheckUiInteractions();
                    int index = Array.IndexOf(desktop.Args, "--screenshot");
                    if (index >= 0 && index + 1 < desktop.Args.Length)
                    {
                        using var bitmap = new RenderTargetBitmap(new PixelSize((int)capture.Bounds.Width, (int)capture.Bounds.Height), new Vector(96, 96));
                        bitmap.Render(capture); using var stream = File.Create(desktop.Args[index + 1]); bitmap.Save(stream, PngBitmapEncoderOptions.Default);
                    }
                    if (capture != window) capture.Close();
                    window.Close();
                }, TimeSpan.FromSeconds(3));
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
