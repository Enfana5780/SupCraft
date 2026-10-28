using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Platform.Storage;
using SupCraft.Core;

namespace SupCraft.Mac;

public sealed class FontRow(string name, bool available, AssFontDocument[] documents) : INotifyPropertyChanged
{
    public string Name { get; } = name;
    public string Key => AssFontDocument.Key(Name);
    public bool Available { get; } = available;
    public string State => Available ? "可用" : "缺失";
    public AssFontDocument[] Documents { get; } = documents;
    public string Usage => $"{Documents.Length} 个文件";
    string replacement = "";
    public string Replacement { get => replacement; set { replacement = value; PropertyChanged?.Invoke(this, new(nameof(Replacement))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class FontManagerWindow : Window
{
    readonly string[] paths;
    readonly string? directory;
    readonly Action<HashSet<string>> changed;
    readonly Action<string> log;
    readonly ObservableCollection<FontRow> rows = [];
    readonly ListBox list = new() { SelectionMode = SelectionMode.Multiple, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
    readonly ComboBox target = new() { HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 240 };
    readonly TextBox search = new() { PlaceholderText = "搜索替换字体名称…" };
    readonly TextBlock summary = Appearance.Text("正在检查字体…", 12, "Muted");
    readonly TextBox details = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap };
    readonly StackPanel commands = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    readonly TextBlock notice = Appearance.Text("选择一项或多项字体，再设置替换字体。保存时自动备份原 ASS。", 12, "Muted");
    FontCatalog? catalog;
    List<string> errors = [];
    bool saving;

    public FontManagerWindow(string[] paths, string? directory, Action<HashSet<string>> changed, Action<string> log)
    {
        this.paths = paths; this.directory = directory; this.changed = changed; this.log = log;
        Title = "SupCraft · 字体管理"; Width = 1000; Height = 740; MinWidth = 860; MinHeight = 650; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Appearance.Style(this);
        var layout = new Grid { Margin = new Thickness(18), RowDefinitions = new RowDefinitions("Auto,*,Auto,150,Auto"), RowSpacing = 12 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 18 };
        var title = Appearance.Text("字体管理", 22); title.FontWeight = FontWeight.SemiBold; header.Children.Add(title); Grid.SetColumn(summary, 1); header.Children.Add(summary);
        var refresh = Button("重新检查", Refresh); Grid.SetColumn(refresh, 2); header.Children.Add(refresh); layout.Children.Add(header);
        var table = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 6 };
        table.Children.Add(Row(["状态", "字体名称", "使用范围", "替换为"], true));
        list.ItemsSource = rows;
        list.Styles.Add(new Avalonia.Styling.Style(s => s.OfType<ListBoxItem>()) { Setters = { new Avalonia.Styling.Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch), new Avalonia.Styling.Setter(ListBoxItem.PaddingProperty, new Thickness(0)) } });
        list.ItemTemplate = new FuncDataTemplate<FontRow>((font, _) =>
        {
            var row = Row([font!.State, font.Name, font.Usage, ""], false);
            Appearance.Bind((TextBlock)row.Children[0], TextBlock.ForegroundProperty, font.Available ? "Success" : "Danger");
            ((TextBlock)row.Children[3]).Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(FontRow.Replacement)));
            ToolTip.SetTip(row, font.Name); return row;
        });
        list.SelectionChanged += (_, _) => ShowSelection();
        Grid.SetRow(list, 1); table.Children.Add(list); var tableCard = Appearance.Card(table); Grid.SetRow(tableCard, 1); layout.Children.Add(tableCard);
        var replace = new Grid { ColumnDefinitions = new ColumnDefinitions("180,*,Auto,Auto"), ColumnSpacing = 8 }; replace.Children.Add(search); Grid.SetColumn(target, 1); replace.Children.Add(target);
        search.TextChanged += (_, _) => FilterTargets();
        var set = Button("设置替换", () => { if (target.SelectedItem is not string name) { notice.Text = "请从列表选择可用的字体家族。"; return Task.CompletedTask; } foreach (var row in Selected()) row.Replacement = name; notice.Text = "替换已暂存，点击“保存替换”写入 ASS。"; return Task.CompletedTask; }); Grid.SetColumn(set, 2); replace.Children.Add(set);
        var reset = Button("清除替换", () => { foreach (var row in Selected()) row.Replacement = ""; return Task.CompletedTask; }); Grid.SetColumn(reset, 3); replace.Children.Add(reset);
        Grid.SetRow(replace, 2); layout.Children.Add(replace);
        Grid.SetRow(details, 3); layout.Children.Add(details);
        var footer = new StackPanel { Spacing = 10 }; notice.TextWrapping = TextWrapping.Wrap; footer.Children.Add(notice);
        var save = Button("保存替换（自动备份）", Save); save.Classes.Add("primary"); commands.Children.Add(save); commands.Children.Add(Button("导出报告…", Export)); commands.Children.Add(Button("收集可用字体…", Collect));
        commands.Children.Add(Button("关闭", () => { Close(); return Task.CompletedTask; })); footer.Children.Add(commands); Grid.SetRow(footer, 4); layout.Children.Add(footer); Content = layout;
        Opened += async (_, _) => await Run(Refresh);
        Closing += (_, args) => { if (saving) args.Cancel = true; };
    }
    IEnumerable<FontRow> Selected() => list.SelectedItems?.Cast<FontRow>() ?? [];
    static Grid Row(string[] values, bool heading)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("65,*,90,*"), ColumnSpacing = 10, Height = 36, Margin = new Thickness(6, 0) };
        for (int i = 0; i < values.Length; i++) { var text = Appearance.Text(values[i], 12, heading ? "Muted" : "Ink"); text.TextTrimming = TextTrimming.CharacterEllipsis; text.FontWeight = heading ? FontWeight.SemiBold : FontWeight.Normal; Grid.SetColumn(text, i); row.Children.Add(text); }
        return row;
    }
    Button Button(string text, Func<Task> action)
    {
        var button = new Button { Content = text }; button.Click += async (_, _) => await Run(action); return button;
    }
    async Task Run(Func<Task> action)
    {
        try { await action(); } catch (Exception ex) { notice.Text = "操作失败：" + ex.Message; log(notice.Text); }
    }
    async Task Refresh()
    {
        if (saving) return;
        IsEnabled = false;
        try
        {
            var loaded = await Task.Run(() =>
            {
                var fonts = FontCatalog.Create(directory); var docs = new List<AssFontDocument>(); var failures = new List<string>();
                foreach (string path in paths) try { docs.Add(AssFontDocument.Read(path)); } catch (Exception ex) { failures.Add(path + "：" + ex.Message); }
                return (fonts, docs, failures);
            });
            catalog = loaded.fonts; errors = loaded.failures;
            rows.Clear();
            foreach (var group in loaded.docs.SelectMany(d => d.References.Select(r => (Document: d, Reference: r))).GroupBy(x => AssFontDocument.Key(x.Reference.Name)).OrderBy(g => g.Key))
                rows.Add(new FontRow(group.First().Reference.Name, catalog.Available(group.First().Reference.Name), group.Select(x => x.Document).Distinct().ToArray()));
            summary.Text = $"{paths.Length} 个文件 · {rows.Count} 种字体 · {rows.Count(r => !r.Available)} 项缺失" + (errors.Count > 0 ? $" · {errors.Count} 个读取失败" : "");
            FilterTargets(); details.Text = errors.Count > 0 ? string.Join("\n", errors) : "选择字体查看 ASS 完整路径、样式和行号。";
        }
        finally { IsEnabled = true; }
    }
    void FilterTargets()
    {
        string? selected = target.SelectedItem as string;
        var names = catalog?.FamilyNames.Where(n => n.Contains(search.Text?.Trim() ?? "", StringComparison.OrdinalIgnoreCase)).ToArray() ?? [];
        target.ItemsSource = names; target.SelectedItem = selected != null && names.Contains(selected) ? selected : names.FirstOrDefault();
    }
    void ShowSelection()
    {
        details.Text = string.Join("\n\n", Selected().Select(row => row.Name + " · " + row.State + "\n" + string.Join("\n", row.Documents.Select(doc => doc.Path + "\n" + string.Join("\n", doc.References.Where(r => AssFontDocument.Key(r.Name) == row.Key).Select(r => $"  第 {r.Line} 行 · 样式 {r.Style} · {(r.Inline ? "行内 \\fn" : "样式字体")}"))))));
    }
    string Report() => "SupCraft 字体报告\n字体目录：" + (directory ?? "系统字体") + "\n\n" + string.Join("\n\n", rows.Select(row => row.Name + " · " + row.State + (row.Replacement.Length > 0 ? " → " + row.Replacement : "") + "\n" + string.Join("\n", row.Documents.Select(d => d.Path)))) + "\n\n" + string.Join("\n", errors);
    async Task Save()
    {
        var replacements = rows.Where(r => r.Replacement.Length > 0).ToDictionary(r => r.Key, r => r.Replacement);
        if (replacements.Count == 0) { notice.Text = "请先选择字体并设置替换。"; return; }
        var docs = rows.Where(r => replacements.ContainsKey(r.Key)).SelectMany(r => r.Documents).Distinct().ToArray();
        saving = true; IsEnabled = false;
        var updated = new HashSet<string>(StringComparer.Ordinal); var failures = new List<string>(); int references = 0;
        try
        {
            await Task.Run(() =>
            {
                foreach (var doc in docs)
                    try { string? backup = doc.Save(replacements, out int count); if (backup != null) { updated.Add(doc.Path); references += count; log($"字体替换：{doc.Path}；{count} 处；备份：{backup}"); } }
                    catch (Exception ex) { failures.Add(doc.Path + "：" + ex.Message); }
            });
            changed(updated);
        }
        finally { saving = false; IsEnabled = true; }
        await Refresh();
        // Retain unsuccessful mappings so the user can correct/retry without rebuilding them.
        foreach (var row in rows) if (row.Documents.Any(d => docs.Any(old => old.Path == d.Path) && !updated.Contains(d.Path)) && replacements.TryGetValue(row.Key, out var value)) row.Replacement = value;
        notice.Text = $"已保存 {updated.Count} 个文件、替换 {references} 处；原文件已备份到同目录 backup。" + (failures.Count > 0 ? $" {failures.Count} 个文件保存失败，请查看详情。" : "");
        if (failures.Count > 0) { details.Text = string.Join("\n\n", failures); foreach (var error in failures) log(error); }
    }
    async Task Export()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "导出字体报告", SuggestedFileName = "SupCraft-font-report.txt" });
        if (file == null) return; await using var stream = await file.OpenWriteAsync(); stream.SetLength(0); await stream.WriteAsync(Encoding.UTF8.GetBytes(Report())); notice.Text = "字体报告已导出。";
    }
    async Task Collect()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "收集可用字体" });
        string? folder = folders.FirstOrDefault()?.TryGetLocalPath(); if (folder == null || catalog == null) return;
        int copied = 0;
        foreach (string source in rows.Select(r => catalog.FileFor(r.Name)).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            string target = Path.Combine(folder, Path.GetFileName(source));
            if (Path.GetFullPath(source) == Path.GetFullPath(target)) continue;
            for (int i = 2; File.Exists(target); i++) target = Path.Combine(folder, Path.GetFileNameWithoutExtension(source) + "_" + i + Path.GetExtension(source));
            File.Copy(source, target, false); copied++;
        }
        notice.Text = $"已收集 {copied} 个字体文件；部分系统字体只能识别字体族，无法定位可复制文件。"; log(notice.Text);
    }
}
