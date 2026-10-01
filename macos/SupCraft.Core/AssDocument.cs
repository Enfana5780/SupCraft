using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SupCraft.Core;

public sealed class ConversionException(string stage, string message, string? details = null) : Exception(message)
{
    public string Stage { get; } = stage;
    public string Details { get; } = details ?? message;
}

public sealed class AssDocument
{
    public int EventCount { get; private set; }
    public double LastEnd { get; private set; }
    public HashSet<string> Fonts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<int> SkippedLines { get; } = [];
    private readonly string[] lines;

    static AssDocument() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    private AssDocument(string[] text) => lines = text;

    public static AssDocument Read(string path)
    {
        if (!File.Exists(path) || !path.EndsWith(".ass", StringComparison.OrdinalIgnoreCase))
            throw new ConversionException("输入", "请选择存在的 ASS 字幕文件。", path);
        var bytes = File.ReadAllBytes(path);
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { text = Encoding.GetEncoding(936).GetString(bytes); }
        var doc = new AssDocument(text.TrimStart('\ufeff').Replace("\r\n", "\n").Split('\n'));
        string section = "";
        string[]? eventFormat = null, styleFormat = null;
        for (int i = 0; i < doc.lines.Length; i++)
        {
            string line = doc.lines[i].Trim();
            if (line.StartsWith('[') && line.EndsWith(']')) { section = line; continue; }
            if (line.StartsWith("Format:", StringComparison.OrdinalIgnoreCase))
            {
                var fields = line[7..].Split(',').Select(s => s.Trim()).ToArray();
                if (section.Equals("[Events]", StringComparison.OrdinalIgnoreCase)) eventFormat = fields;
                else if (section.Contains("Styles", StringComparison.OrdinalIgnoreCase)) styleFormat = fields;
            }
            else if (line.StartsWith("Style:", StringComparison.OrdinalIgnoreCase) && styleFormat != null)
            {
                var values = line[6..].Trim().Split(',');
                int font = Array.FindIndex(styleFormat, s => s.Equals("Fontname", StringComparison.OrdinalIgnoreCase));
                if (font >= 0 && font < values.Length) doc.Fonts.Add(values[font].Trim().TrimStart('@'));
            }
            else if (section.Equals("[Events]", StringComparison.OrdinalIgnoreCase) && line.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
            {
                if (eventFormat == null) throw Error(i, "Dialogue 前没有 Events Format。");
                if (eventFormat.Distinct(StringComparer.OrdinalIgnoreCase).Count() != eventFormat.Length)
                    throw Error(i, "Events Format 包含重复字段。");
                var values = line[9..].Trim().Split(',', eventFormat.Length);
                if (values.Length != eventFormat.Length) throw Error(i, "字段数量不符合 Events Format。");
                int start = Index(eventFormat, "Start"), end = Index(eventFormat, "End"), content = Index(eventFormat, "Text");
                if (start < 0 || end < 0 || content < 0) throw Error(i, "Events Format 缺少 Start、End 或 Text。");
                if (string.IsNullOrWhiteSpace(values[content])) { doc.SkippedLines.Add(i + 1); continue; }
                double a = Time(values[start], i), b = Time(values[end], i);
                if (b <= a) throw Error(i, "结束时间不晚于开始时间。");
                doc.EventCount++;
                doc.LastEnd = Math.Max(doc.LastEnd, b);
                foreach (Match font in Regex.Matches(values[content], @"\\fn([^\\}]+)")) doc.Fonts.Add(font.Groups[1].Value.Trim().TrimStart('@'));
            }
        }
        if (doc.EventCount == 0) throw new ConversionException("字幕预检", "字幕没有可转换的非空 Dialogue 事件。");
        return doc;
    }

    static ConversionException Error(int line, string message) => new("字幕预检", $"第 {line + 1} 行：{message}");
    static int Index(string[] fields, string name) => Array.FindIndex(fields, s => s.Equals(name, StringComparison.OrdinalIgnoreCase));
    static double Time(string value, int line)
    {
        var match = Regex.Match(value.Trim(), @"^(\d+):([0-5]\d):([0-5]\d)\.(\d{1,3})$");
        if (!match.Success) throw Error(line, $"时间格式无效：{value}");
        return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 3600
            + int.Parse(match.Groups[2].Value) * 60 + int.Parse(match.Groups[3].Value)
            + int.Parse(match.Groups[4].Value.PadRight(3, '0')) / 1000.0;
    }

    // Always write UTF-8 for libass; source bytes are never modified.
    public void WriteConversionCopy(string path, bool removeTailFade = false)
    {
        string section = "";
        string[]? format = null;
        var skip = SkippedLines.ToHashSet();
        var output = new List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (skip.Contains(i + 1)) continue;
            string line = lines[i], trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']')) section = trimmed;
            if (section.Equals("[Events]", StringComparison.OrdinalIgnoreCase))
            {
                if (trimmed.StartsWith("Format:", StringComparison.OrdinalIgnoreCase)) format = trimmed[7..].Split(',').Select(s => s.Trim()).ToArray();
                else if (removeTailFade && format != null && trimmed.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase))
                {
                    var values = trimmed[9..].Trim().Split(',', format.Length);
                    int end = Index(format, "End"), content = Index(format, "Text");
                    if (values.Length == format.Length && end >= 0 && content >= 0 && Math.Abs(Time(values[end], i) - LastEnd) < 0.01)
                    {
                        values[content] = Regex.Replace(values[content], @"\\fad\(\s*\d+\s*,\s*\d+\s*\)", "", RegexOptions.IgnoreCase);
                        line = "Dialogue: " + string.Join(',', values);
                    }
                }
            }
            output.Add(line);
        }
        File.WriteAllText(path, string.Join('\n', output), new UTF8Encoding(false));
    }
}
