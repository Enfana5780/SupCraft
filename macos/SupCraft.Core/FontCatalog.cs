using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace SupCraft.Core;

public sealed class FontCatalog
{
    static FontCatalog() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    private readonly Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);
    private readonly SortedSet<string> displayNames = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> FamilyNames => displayNames.Where(name => !name.StartsWith('.')).ToArray();
    private readonly HashSet<string> families = new(StringComparer.OrdinalIgnoreCase);
    public static string Normalize(string name) => Regex.Replace(name.Trim().TrimStart('@'), @"\s+", "").ToLowerInvariant();
    public bool Available(string name) => families.Contains(Normalize(name)) || files.ContainsKey(Normalize(name));
    public string? FileFor(string name) => files.GetValueOrDefault(Normalize(name));

    public static string[] ReadDirectory(string? path, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(path)) return [];
        if (!Directory.Exists(path)) { if (required) throw new ConversionException("字体目录", "字体目录不存在。", path); return []; }
        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = !required, AttributesToSkip = FileAttributes.ReparsePoint };
            return Directory.EnumerateFiles(path, "*", options)
                .Where(p => new[] { ".ttf", ".otf", ".ttc" }.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .Order(StringComparer.Ordinal).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new ConversionException("字体目录", "无法读取字体目录或子目录。", ex.Message); }
    }

    public static FontCatalog Create(string? customDirectory)
    {
        var result = new FontCatalog();
        if (OperatingSystem.IsMacOS())
        {
            foreach (string path in new[] { "/System/Library/Fonts", "/Library/Fonts", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Fonts") })
                foreach (string file in ReadDirectory(path, false)) result.AddFile(file);
            result.AddMacFamilies();
        }
        else if (OperatingSystem.IsWindows())
            foreach (string file in ReadDirectory(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), false)) result.AddFile(file);
        foreach (string file in ReadDirectory(customDirectory)) result.AddFile(file);
        return result;
    }

    public void AddFile(string path)
    {
        files[Normalize(Path.GetFileNameWithoutExtension(path))] = path;
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            static ushort U16(BinaryReader r) => (ushort)((r.ReadByte() << 8) | r.ReadByte());
            static uint U32(BinaryReader r) => ((uint)U16(r) << 16) | U16(r);
            uint signature = U32(reader);
            List<uint> offsets = [0];
            if (signature == 0x74746366)
            {
                U32(reader); uint count = U32(reader);
                if (count > 4096 || 12L + count * 4L > stream.Length) return;
                offsets.Clear(); for (uint i = 0; i < count; i++) offsets.Add(U32(reader));
            }
            foreach (uint face in offsets)
            {
                stream.Position = face + 4; int tables = U16(reader); stream.Position += 6;
                uint nameOffset = 0, nameLength = 0;
                for (int i = 0; i < tables; i++)
                {
                    uint tag = U32(reader); U32(reader); uint offset = U32(reader), length = U32(reader);
                    if (tag == 0x6e616d65) { nameOffset = offset; nameLength = length; }
                }
                if (nameOffset == 0 || nameOffset + (long)nameLength > stream.Length) continue;
                stream.Position = nameOffset; U16(reader); int records = U16(reader), storage = U16(reader);
                for (int i = 0; i < records; i++)
                {
                    stream.Position = nameOffset + 6 + i * 12;
                    int platform = U16(reader); U16(reader); U16(reader); int id = U16(reader), length = U16(reader), offset = U16(reader);
                    if (id is not (1 or 4 or 6 or 16 or 17)) continue;
                    long position = nameOffset + storage + offset;
                    if (position + length > stream.Length) continue;
                    stream.Position = position; byte[] bytes = reader.ReadBytes(length);
                    string alias = (platform is 0 or 3 ? Encoding.BigEndianUnicode : Encoding.GetEncoding(10000)).GetString(bytes).Trim('\0', ' ');
                    if (alias.Length > 0) { files[Normalize(alias)] = path; if (id is 1 or 16) displayNames.Add(alias); }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException) { /* Unreadable fonts still retain the filename alias. */ }
    }

    [DllImport("/System/Library/Frameworks/CoreText.framework/CoreText")]
    static extern IntPtr CTFontManagerCopyAvailableFontFamilyNames();
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    static extern nint CFArrayGetCount(IntPtr array);
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    static extern nint CFStringGetLength(IntPtr value);
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.I1)]
    static extern bool CFStringGetCString(IntPtr value, byte[] buffer, nint size, uint encoding);
    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    static extern void CFRelease(IntPtr value);

    void AddMacFamilies()
    {
        IntPtr array = CTFontManagerCopyAvailableFontFamilyNames();
        if (array == IntPtr.Zero) return;
        try
        {
            for (nint i = 0; i < CFArrayGetCount(array); i++)
            {
                IntPtr value = CFArrayGetValueAtIndex(array, i);
                int size = checked((int)CFStringGetLength(value) * 4 + 1); byte[] buffer = new byte[size];
                if (CFStringGetCString(value, buffer, size, 0x08000100))
                    {
                    string name = Encoding.UTF8.GetString(buffer, 0, Array.IndexOf(buffer, (byte)0));
                    families.Add(Normalize(name)); displayNames.Add(name);
                }
            }
        }
        finally { CFRelease(array); }
    }
}
