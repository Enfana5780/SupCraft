using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SkiaSharp;

namespace SupCraft.Core;

public enum OutputFormat { Sup, Bdn, Both }
public sealed record ConversionOptions(string OutputDirectory, string? FontDirectory, int Height = 1080, double FrameRate = 23.976, OutputFormat Format = OutputFormat.Sup, string Language = "zho", bool Hdr = false, decimal DimPercent = 33, bool PalettePng = true);
public sealed record Toolchain(string Renderer, string Encoder, string Quantizer)
{
    public static Toolchain Load(string path)
    {
        var value = JsonSerializer.Deserialize<Toolchain>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new ConversionException("环境", "转换组件清单无效。");
        string root = Path.GetDirectoryName(Path.GetFullPath(path))!;
        string Resolve(string name) => Path.GetFullPath(Path.Combine(root, name));
        return new(Resolve(value.Renderer), Resolve(value.Encoder), Resolve(value.Quantizer));
    }
    public static string BundledManifest => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../Resources/tools/toolchain.json"));
}

public sealed class ConversionService(Toolchain tools, IToolRunner? runner = null)
{
    readonly IToolRunner runner = runner ?? new ToolRunner();
    public static string SafeName(string name)
    {
        string safe = Regex.Replace(name, "[^A-Za-z0-9._-]+", "_").Trim('_');
        return safe.Length == 0 ? "subtitle" : safe;
    }
    public static string UniqueName(string input, string directory, HashSet<string> used)
    {
        string stem = SafeName(Path.GetFileNameWithoutExtension(input)), name = stem;
        for (int i = 2; used.Contains(name) || File.Exists(Path.Combine(directory, name + ".sup")) || File.Exists(Path.Combine(directory, name + ".xml")); i++) name = stem + "_" + i;
        used.Add(name); return name;
    }
    public static string Config(string quantizer) => $"[PILIQ]\nquality=95\nspeed=4\ndither=95\nquantizer={quantizer}\n\n[SUPer]\nuse_gpu=1\nlayout_mode=2\nabort_on_error=0\n";

    public async Task<IReadOnlyList<string>> ConvertAsync(string input, ConversionOptions options, HashSet<string> used, Action<string> log, CancellationToken token)
    {
        if (options.Height is not (720 or 1080 or 1440 or 2160)) throw new ConversionException("参数", "分辨率无效。");
        if (!new[] { 23.976, 24, 25, 29.97, 50, 59.94 }.Contains(options.FrameRate) || options.DimPercent is < 0 or > 100)
            throw new ConversionException("参数", "帧率或亮度缩减值无效。");
        if (options.Height > 1080 && options.Format != OutputFormat.Bdn) throw new ConversionException("参数", "1440p / 2160p 仅支持 BDN。");
        if (!File.Exists(tools.Renderer) || options.Format != OutputFormat.Bdn && (!File.Exists(tools.Encoder) || !File.Exists(tools.Quantizer)))
            throw new ConversionException("环境", "转换组件不完整，请使用完整 Mac 程序包。");
        var document = AssDocument.Read(input);
        var fonts = FontCatalog.ReadDirectory(options.FontDirectory);
        if (!string.IsNullOrWhiteSpace(options.FontDirectory) && fonts.Length == 0) throw new ConversionException("字体目录", "所选目录没有 TTF、OTF 或 TTC 字体。");
        var catalog = FontCatalog.Create(options.FontDirectory);
        var missing = document.Fonts.Where(f => !catalog.Available(f)).ToArray();
        if (missing.Length > 0) log("[警告] 缺失字体：" + string.Join("、", missing) + "；替代字体可能改变排版。");
        if (document.SkippedLines.Count > 0) log($"跳过 {document.SkippedLines.Count} 条空字幕；原始行号：{string.Join('、', document.SkippedLines.Take(20))}。原 ASS 不变。");
        Directory.CreateDirectory(options.OutputDirectory);
        string name = UniqueName(input, options.OutputDirectory, used);
        string work = Path.Combine(Path.GetTempPath(), "SupCraft", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work); bool completed = false;
        log($"开始：{Path.GetFileName(input)}；{document.EventCount} 条事件；临时目录：{work}");
        try
        {
            string ass = Path.Combine(work, "input.ass"), xml = Path.Combine(work, "bdn.xml");
            document.WriteConversionCopy(ass);
            var args = new List<string> { "-v", options.Height == 720 ? "720p" : "1080p", "-f", options.FrameRate.ToString(CultureInfo.InvariantCulture), "--floor-ms", "-t", name, "-l", SafeName(options.Language) };
            if (fonts.Length > 0)
            {
                string directory = Path.Combine(work, "fonts"); Directory.CreateDirectory(directory);
                for (int i = 0; i < fonts.Length; i++) { token.ThrowIfCancellationRequested(); File.Copy(fonts[i], Path.Combine(directory, $"{i:D5}" + Path.GetExtension(fonts[i]))); }
                args.AddRange(["--fontdir", directory]);
            }
            if (options.Hdr) args.AddRange(["--dim", options.DimPercent.ToString(CultureInfo.InvariantCulture)]);
            if (options.Format == OutputFormat.Bdn && options.Height <= 1080 && options.PalettePng) args.AddRange(["-q", "255", "-r"]);
            args.Add(ass);
            await runner.RunAsync(tools.Renderer, args, work, null, log, token);
            ValidateBdn(xml);
            if (BdnEnd(xml, options.FrameRate) + 0.5 < document.LastEnd)
            {
                log("[警告] BDN 尾部不足；仅从临时副本尾部移除 \\fad 后重试。");
                document.WriteConversionCopy(ass, removeTailFade: true);
                await runner.RunAsync(tools.Renderer, args, work, null, log, token);
                ValidateBdn(xml);
                if (BdnEnd(xml, options.FrameRate) + 0.5 < document.LastEnd) throw new ConversionException("BDN 验证", "重试后仍缺少末尾字幕。", work);
            }
            if (options.Height > 1080) ScaleBdn(xml, options.Height);
            var outputs = new List<string>();
            if (options.Format != OutputFormat.Bdn)
            {
                string config = Path.Combine(work, "super.ini"), sup = Path.Combine(work, name + ".sup");
                File.WriteAllText(config, Config(tools.Quantizer));
                var env = new Dictionary<string, string> { ["SUPCRAFT_JOB_CONFIG"] = config, ["PYTHONUNBUFFERED"] = "1" };
                log("SUPer：默认请求 OpenCL 加速；设备不可用时由编码器回退 CPU。");
                await runner.RunAsync(tools.Encoder, ["-i", xml, "-b", options.Hdr ? "2020" : "709", "-q", "3", "-t", "4", "-y", sup], work, env, log, token);
                ValidateSup(sup, document.LastEnd);
                token.ThrowIfCancellationRequested();
                string target = Path.Combine(options.OutputDirectory, name + ".sup"); File.Copy(sup, target, overwrite: false); outputs.Add(target);
            }
            if (options.Format != OutputFormat.Sup)
            {
                token.ThrowIfCancellationRequested();
                var bdn = XDocument.Load(xml);
                // Publish each image with a task prefix; never overwrite another task's PNGs.
                int i = 0;
                var renamed = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var graphic in bdn.Descendants("Graphic"))
                {
                    string relative = graphic.Value.Trim();
                    if (!renamed.TryGetValue(relative, out string? targetName))
                    {
                        string desired = $"{name}_{i++:D6}.png"; targetName = desired;
                        for (int suffix = 2; File.Exists(Path.Combine(options.OutputDirectory, targetName)); suffix++) targetName = Path.GetFileNameWithoutExtension(desired) + "_" + suffix + ".png";
                        File.Copy(AssetPath(work, relative), Path.Combine(options.OutputDirectory, targetName), overwrite: false); renamed.Add(relative, targetName);
                    }
                    graphic.Value = targetName;
                }
                string target = Path.Combine(options.OutputDirectory, name + ".xml");
                using (var stream = new FileStream(target, FileMode.CreateNew)) bdn.Save(stream);
                ValidateBdn(target); outputs.Add(target);
            }
            log("完成：" + string.Join("；", outputs)); completed = true; return outputs;
        }
        finally
        {
            if (completed) { try { Directory.Delete(work, true); } catch (IOException ex) { log("临时目录清理失败：" + ex.Message); } }
            else log("失败或取消任务的临时目录已保留：" + work);
        }
    }

    public static string AssetPath(string directory, string name)
    {
        string root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar, path = Path.GetFullPath(Path.Combine(directory, name));
        if (Path.IsPathRooted(name) || !path.StartsWith(root, StringComparison.Ordinal)) throw new ConversionException("BDN 验证", "图像引用超出任务目录。", name);
        return path;
    }
    public static void ValidateBdn(string path)
    {
        if (!File.Exists(path)) throw new ConversionException("BDN 验证", "未生成 BDN XML。", path);
        try
        {
            var graphics = XDocument.Load(path).Descendants("Graphic").ToArray();
            if (graphics.Length == 0) throw new ConversionException("BDN 验证", "BDN 没有图像事件。");
            foreach (var graphic in graphics) if (!File.Exists(AssetPath(Path.GetDirectoryName(path)!, graphic.Value.Trim()))) throw new ConversionException("BDN 验证", "BDN 引用的 PNG 不存在。", graphic.Value);
        }
        catch (System.Xml.XmlException ex) { throw new ConversionException("BDN 验证", "XML 结构无效。", ex.Message); }
    }
    public static double BdnEnd(string path, double rate)
    {
        int nominal = (int)Math.Round(rate); double end = 0;
        foreach (var entry in XDocument.Load(path).Descendants("Event"))
        {
            var parts = ((string?)entry.Attribute("OutTC") ?? "").Split(':');
            if (parts.Length != 4 || parts.Any(p => !int.TryParse(p, out _))) throw new ConversionException("BDN 验证", "BDN 结束时间无效。");
            var values = parts.Select(int.Parse).ToArray();
            end = Math.Max(end, ((values[0] * 3600 + values[1] * 60 + values[2]) * nominal + values[3]) / rate);
        }
        return end;
    }
    public static void ValidateSup(string path, double expectedEnd)
    {
        if (!File.Exists(path)) throw new ConversionException("SUP 验证", "未生成 SUP。", path);
        using var stream = File.OpenRead(path); Span<byte> header = stackalloc byte[13]; uint last = 0; int segments = 0;
        while (stream.Position < stream.Length)
        {
            if (stream.Read(header) != 13 || header[0] != 'P' || header[1] != 'G') throw new ConversionException("SUP 验证", "SUP 段标记或文件边界损坏。");
            last = Math.Max(last, BinaryPrimitives.ReadUInt32BigEndian(header[2..6]));
            int length = BinaryPrimitives.ReadUInt16BigEndian(header[11..13]);
            if (stream.Position + length > stream.Length) throw new ConversionException("SUP 验证", "SUP 段数据不完整。");
            stream.Position += length; segments++;
        }
        if (segments == 0 || last / 90000.0 + 1 < expectedEnd) throw new ConversionException("SUP 验证", "SUP 为空或尾部时间不足。");
    }
    public static void ScaleBdn(string path, int height)
    {
        var document = XDocument.Load(path); var format = document.Descendants("Format").FirstOrDefault() ?? throw new ConversionException("BDN 验证", "BDN 缺少 Format。");
        double scale = height / 1080.0; format.SetAttributeValue("VideoFormat", height + "p");
        var scaled = new HashSet<string>(StringComparer.Ordinal);
        foreach (var graphic in document.Descendants("Graphic"))
        {
            string file = AssetPath(Path.GetDirectoryName(path)!, graphic.Value.Trim());
            int width = (int)Math.Round(int.Parse(graphic.Attribute("Width")!.Value) * scale), h = (int)Math.Round(int.Parse(graphic.Attribute("Height")!.Value) * scale);
            if (scaled.Add(file))
            {
                using var original = SKBitmap.Decode(file) ?? throw new ConversionException("图像", "无法读取 PNG。", file);
                using var surface = SKSurface.Create(new SKImageInfo(width, h, SKColorType.Bgra8888, SKAlphaType.Premul));
                using var image = SKImage.FromBitmap(original);
                surface.Canvas.Clear(SKColors.Transparent);
                surface.Canvas.DrawImage(image, new SKRect(0, 0, width, h), new SKSamplingOptions(SKFilterMode.Linear));
                using var output = surface.Snapshot(); using var data = output.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(file, data.ToArray());
            }
            graphic.SetAttributeValue("Width", width); graphic.SetAttributeValue("Height", h);
            foreach (string coordinate in new[] { "X", "Y" }) graphic.SetAttributeValue(coordinate, (int)Math.Round(int.Parse(graphic.Attribute(coordinate)!.Value) * scale));
        }
        document.Save(path);
    }
}
