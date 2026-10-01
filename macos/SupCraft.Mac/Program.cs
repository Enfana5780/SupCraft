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
            if (input.Length == 0 || output.Length == 0) throw new ArgumentException("--convert requires --input and --output.");
            string format = Value("--format", "sup");
            var options = new ConversionOptions(output, Value("--fonts"), int.Parse(Value("--height", "1080")), double.Parse(Value("--fps", "23.976"), System.Globalization.CultureInfo.InvariantCulture), format switch { "bdn" => OutputFormat.Bdn, "both" => OutputFormat.Both, "sup" => OutputFormat.Sup, _ => throw new ArgumentException("Invalid --format") }, Hdr: args.Contains("--hdr"));
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
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light; }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow(); desktop.MainWindow = window;
            if (desktop.Args?.Contains("--self-test-ui") == true)
            {
                if (desktop.Args.Contains("--compact")) { window.Width = window.MinWidth; window.Height = window.MinHeight; }
                int preview = Array.IndexOf(desktop.Args, "--preview-ass");
                if (preview >= 0 && preview + 1 < desktop.Args.Length) window.AddFiles([desktop.Args[preview + 1]]);
                DispatcherTimer.RunOnce(() =>
                {
                    int index = Array.IndexOf(desktop.Args, "--screenshot");
                    if (index >= 0 && index + 1 < desktop.Args.Length)
                    {
                        using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height), new Vector(96, 96));
                        bitmap.Render(window); using var stream = File.Create(desktop.Args[index + 1]); bitmap.Save(stream, PngBitmapEncoderOptions.Default);
                    }
                    window.Close();
                }, TimeSpan.FromSeconds(2));
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
