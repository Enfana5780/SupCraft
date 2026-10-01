using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using SupCraft.Core;

namespace SupCraft.Mac;

public sealed class Job(string input) : INotifyPropertyChanged
{
    public string Input { get; } = input;
    public string Name => Path.GetFileName(Input);
    string status = "等待", details = "";
    public string Status { get => status; set { status = value; PropertyChanged?.Invoke(this, new(nameof(Status))); } }
    public string Details { get => details; set { details = value; PropertyChanged?.Invoke(this, new(nameof(Details))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    public override string ToString() => $"{Status}  ·  {Name}";
}

public sealed class MainWindow : Window
{
    readonly ObservableCollection<Job> jobs = [];
    readonly ListBox queue = new() { SelectionMode = SelectionMode.Multiple, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0) };
    readonly TextBox output = new(), fontDirectory = new(), language = new() { Text = "zho", Width = 90 };
    readonly ComboBox resolution = Combo(["720p", "1080p", "1440p（BDN）", "2160p（BDN）"], 1, 175), fps = Combo(["23.976", "24", "25", "29.97", "50", "59.94"], 0, 105), format = Combo(["SUP", "BDN XML + PNG", "SUP + BDN"], 0, 175);
    readonly CheckBox hdr = new() { Content = "HDR / BT.2020" }, palette = new() { Content = "调色板 PNG", IsChecked = true };
    readonly NumericUpDown dim = new() { Minimum = 0, Maximum = 100, Value = 33, Increment = 1, FormatString = "0.0", Width = 100, IsEnabled = false };
    readonly TextBox log = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, Background = Brush.Parse("#F3F6FA"), BorderThickness = new Thickness(0), FontFamily = new FontFamily("Menlo,Consolas,monospace"), FontSize = 11, Padding = new Thickness(12, 8) };
    readonly TextBlock status = new() { Text = "等待添加 ASS 文件", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    readonly ProgressBar progress = new() { Minimum = 0, Maximum = 100, Height = 5, Foreground = Brush.Parse("#169D9D"), Background = Brush.Parse("#E0E8EF") };
    readonly Button start = new() { Content = "开始批量转换", MinWidth = 150, Background = Brush.Parse("#17344F"), Foreground = Brushes.White, BorderBrush = Brush.Parse("#17344F") }, cancel = new() { Content = "取消当前批次", MinWidth = 125, IsEnabled = false };
    readonly StackPanel parameters = new() { Spacing = 12 };
    readonly StackPanel fileActions = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    readonly object logLock = new();
    CancellationTokenSource? batch;
    bool closeAfterBatch;

    public MainWindow()
    {
        Title = "SupCraft · macOS · ASS → BDN / SUP v" + typeof(MainWindow).Assembly.GetName().Version!.ToString(3);
        Width = 1180; Height = 860; MinWidth = 900; MinHeight = 680; FontSize = 13;
        using (var icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupCraft.Icon.png")) Icon = new WindowIcon(icon!);
        Background = Brush.Parse("#EAF0F6");
        Styles.Add(new Style(s => s.OfType<Button>()) { Setters = { new Setter(Button.PaddingProperty, new Thickness(12, 7)), new Setter(Button.CornerRadiusProperty, new CornerRadius(5)), new Setter(Button.BorderThicknessProperty, new Thickness(1)), new Setter(Button.BorderBrushProperty, Brush.Parse("#C8D4E1")), new Setter(Button.BackgroundProperty, Brush.Parse("#F1F5F9")), new Setter(Button.FontSizeProperty, 12d) } });
        var body = new Grid { Margin = new Thickness(20, 18), RowDefinitions = new RowDefinitions("Auto,*,Auto,145"), RowSpacing = 12 };
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 14 };
        using (var icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupCraft.Icon.png"))
            heading.Children.Add(new Image { Source = new Bitmap(icon!), Width = 48, Height = 48 });
        var title = new StackPanel { Spacing = 3 };
        title.Children.Add(new TextBlock { Text = "SupCraft", FontSize = 25, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White });
        title.Children.Add(new TextBlock { Text = "ASS 图形字幕转换  /  BDN · SUP", FontSize = 12, Foreground = Brush.Parse("#BDD0E2") });
        Grid.SetColumn(title, 1); heading.Children.Add(title);
        var platform = new Border { Background = Brush.Parse("#203F57"), BorderBrush = Brush.Parse("#476278"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(12, 7), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = "macOS · Apple Silicon", FontSize = 12, Foreground = Brush.Parse("#90DFDA") } };
        Grid.SetColumn(platform, 2); heading.Children.Add(platform);
        body.Children.Add(new Border { Background = Brush.Parse("#142C43"), CornerRadius = new CornerRadius(8), Padding = new Thickness(18, 12), Child = heading });
        var workspace = new Grid { ColumnDefinitions = new ColumnDefinitions("*,370"), ColumnSpacing = 14 };
        Grid.SetRow(workspace, 1); body.Children.Add(workspace);
        var queuePanel = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*") };
        var queueHeading = SectionHeading("任务队列", "拖入 ASS 文件 · 双击查看详情");
        queuePanel.Children.Add(queueHeading);
        fileActions.Children.Add(Action("添加 ASS…", PickFiles));
        fileActions.Children.Add(Action("移除选中", () => { foreach (var job in queue.SelectedItems!.Cast<Job>().ToArray()) jobs.Remove(job); return Task.CompletedTask; }));
        fileActions.Children.Add(Action("清空队列", () => { jobs.Clear(); return Task.CompletedTask; }));
        fileActions.Children.Add(Action("检查字体…", CheckFonts));
        fileActions.Margin = new Thickness(14, 10, 14, 12); Grid.SetRow(fileActions, 1); queuePanel.Children.Add(fileActions);
        var columns = TableRow(["状态", "ASS 文件", "输出 / 错误"], true); Grid.SetRow(columns, 2); queuePanel.Children.Add(columns);
        queue.ItemsSource = jobs;
        queue.Styles.Add(new Style(s => s.OfType<ListBoxItem>()) { Setters = { new Setter(ListBoxItem.PaddingProperty, new Thickness(0)), new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch) } });
        queue.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Job>((job, _) =>
        {
            var row = TableRow(["", job!.Name, ""], false);
            ((TextBlock)((Border)row.Children[0]).Child!).Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(Job.Status)));
            ((TextBlock)((Border)row.Children[2]).Child!).Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(Job.Details)));
            ToolTip.SetTip(row, job.Input); return row;
        });
        queue.DoubleTapped += async (_, _) => { if (queue.SelectedItem is Job job) await ShowText("任务详情", job.Input + "\n\n" + job.Status + "\n" + job.Details); };
        var table = new Grid { ClipToBounds = true }; table.Children.Add(new GridLines(true)); table.Children.Add(queue);
        var empty = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 6, IsHitTestVisible = false };
        empty.Children.Add(new TextBlock { Text = "将 ASS 字幕拖到这里", FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#546D84"), HorizontalAlignment = HorizontalAlignment.Center });
        empty.Children.Add(new TextBlock { Text = "或点击“添加 ASS…”开始批量转换", FontSize = 12, Foreground = Brush.Parse("#788C9F") });
        table.Children.Add(empty); jobs.CollectionChanged += (_, _) => { empty.IsVisible = jobs.Count == 0; if (batch == null) status.Text = $"队列中 {jobs.Count} 个文件"; };
        Grid.SetRow(table, 3); queuePanel.Children.Add(table); workspace.Children.Add(Card(queuePanel));
        var settings = new Grid { RowDefinitions = new RowDefinitions("Auto,*") }; settings.Children.Add(SectionHeading("转换设置", "libass 渲染路线"));
        parameters.Margin = new Thickness(16, 12);
        parameters.Children.Add(PathRow("输出目录", output));
        parameters.Children.Add(PathRow("字体目录（可选，无需安装）", fontDirectory));
        output.PlaceholderText = "选择转换结果的保存目录"; fontDirectory.PlaceholderText = "留空使用系统字体";
        ToolTip.SetTip(fontDirectory, "递归读取 TTF / OTF / TTC；无需安装字体。留空使用系统字体。");
        var fields = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var field in new Control[] { Field("分辨率", resolution), Field("帧率", fps), Field("输出格式", format), Field("语言代码", language) }) { field.Margin = new Thickness(0, 0, 14, 12); fields.Children.Add(field); }
        parameters.Children.Add(new Border { Height = 1, Background = Brush.Parse("#D6E0EA") }); parameters.Children.Add(fields);
        var color = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") }; hdr.VerticalAlignment = VerticalAlignment.Center; color.Children.Add(hdr); var brightness = Field("亮度缩减 %", dim); Grid.SetColumn(brightness, 1); color.Children.Add(brightness); parameters.Children.Add(color); parameters.Children.Add(palette);
        var settingsScroll = new ScrollViewer { Content = parameters, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(settingsScroll, 1); settings.Children.Add(settingsScroll); var settingsCard = Card(settings); Grid.SetColumn(settingsCard, 1); workspace.Children.Add(settingsCard);
        resolution.SelectionChanged += (_, _) => UpdateOptions(); format.SelectionChanged += (_, _) => UpdateOptions(); hdr.IsCheckedChanged += (_, _) => dim.IsEnabled = hdr.IsChecked == true;
        ToolTip.SetTip(palette, "仅 720p/1080p 的 BDN 输出可用。索引色 PNG 可能影响渐变；SUP 量化由 SUPer 处理。");
        start.Click += async (_, _) => await ConvertBatch(); cancel.Click += (_, _) => { cancel.IsEnabled = false; batch?.Cancel(); };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 }; actions.Children.Add(start); actions.Children.Add(cancel);
        var run = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 22, Margin = new Thickness(14, 10) }; run.Children.Add(actions);
        var runState = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center }; status.FontSize = 12; status.Foreground = Brush.Parse("#526B82"); runState.Children.Add(status); runState.Children.Add(progress); Grid.SetColumn(runState, 1); run.Children.Add(runState);
        var runCard = Card(run); Grid.SetRow(runCard, 2); body.Children.Add(runCard);
        var logs = new Grid { RowDefinitions = new RowDefinitions("Auto,*") }; logs.Children.Add(SectionHeading("详细日志", "转换进度与错误记录")); Grid.SetRow(log, 1); logs.Children.Add(log); var logCard = Card(logs); Grid.SetRow(logCard, 3); body.Children.Add(logCard);
        var surface = new Grid(); surface.Children.Add(new GridLines(false)); surface.Children.Add(body); Content = surface;
        Closing += (_, args) => { if (batch != null) { args.Cancel = true; closeAfterBatch = true; batch.Cancel(); status.Text = "正在取消任务并清理子进程…"; } };
        UpdateOptions();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, (_, args) => args.DragEffects = batch == null && args.DataTransfer.TryGetFiles() != null ? DragDropEffects.Copy : DragDropEffects.None);
        AddHandler(DragDrop.DropEvent, (_, args) => AddFiles((args.DataTransfer.TryGetFiles() ?? []).Select(file => file.TryGetLocalPath()).OfType<string>()));
    }
    static ComboBox Combo(string[] items, int selected, double width) => new() { ItemsSource = items, SelectedIndex = selected, Width = width };
    static Control Field(string label, Control control) { var panel = new StackPanel { Spacing = 5 }; panel.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Brush.Parse("#526B82") }); panel.Children.Add(control); return panel; }
    static Border Card(Control child) => new() { Background = Brush.Parse("#FCFDFE"), BorderBrush = Brush.Parse("#BFCEDC"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), ClipToBounds = true, Child = child };
    static Border SectionHeading(string title, string hint)
    {
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 14 };
        heading.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Brush.Parse("#24425D") });
        var text = new TextBlock { Text = hint, FontSize = 11, Foreground = Brush.Parse("#6D8397"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }; Grid.SetColumn(text, 1); heading.Children.Add(text);
        return new Border { Background = Brush.Parse("#EAF0F6"), BorderBrush = Brush.Parse("#CEDAE6"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(14, 10), Child = heading };
    }
    static Grid TableRow(string[] values, bool heading)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("78,1.7*,*"), Height = 36, Background = heading ? Brush.Parse("#E0E9F2") : Brushes.Transparent };
        for (int i = 0; i < values.Length; i++)
        {
            var cell = new Border { BorderBrush = Brush.Parse("#CEDAE6"), BorderThickness = new Thickness(0, 0, i < 2 ? 1 : 0, 1), Padding = new Thickness(10, 0), Child = new TextBlock { Text = values[i], FontSize = 12, FontWeight = heading ? FontWeight.SemiBold : FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush.Parse("#36536D"), TextTrimming = TextTrimming.CharacterEllipsis } };
            Grid.SetColumn(cell, i); row.Children.Add(cell);
        }
        return row;
    }
    Button Action(string text, Func<Task> action)
    {
        var button = new Button { Content = text };
        button.Click += async (_, _) => { try { await action(); } catch (Exception ex) { await ShowText("操作失败", ex.Message); } };
        return button;
    }
    Control PathRow(string label, TextBox box)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,70"), ColumnSpacing = 8 };
        grid.Children.Add(box);
        var browse = Action("浏览…", async () => { var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = label }); if (folders.Count > 0) box.Text = folders[0].TryGetLocalPath(); });
        Grid.SetColumn(browse, 1); grid.Children.Add(browse); return Field(label, grid);
    }
    public void AddFiles(IEnumerable<string> files)
    {
        if (batch != null) return;
        foreach (string file in files.Where(p => p.EndsWith(".ass", StringComparison.OrdinalIgnoreCase) && File.Exists(p)))
            if (!jobs.Any(j => j.Input == file)) jobs.Add(new Job(Path.GetFullPath(file)));
        if (jobs.Count > 0 && string.IsNullOrWhiteSpace(output.Text)) output.Text = Path.Combine(Path.GetDirectoryName(jobs[0].Input)!, "subtitle-output");
        status.Text = $"队列中 {jobs.Count} 个文件";
    }
    async Task PickFiles()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "添加 ASS 字幕", AllowMultiple = true, FileTypeFilter = [new FilePickerFileType("ASS 字幕") { Patterns = ["*.ass"] }] });
        AddFiles(files.Select(f => f.TryGetLocalPath()).OfType<string>());
    }
    void UpdateOptions()
    {
        bool high = resolution.SelectedIndex > 1;
        if (high) format.SelectedIndex = 1;
        format.IsEnabled = !high;
        palette.IsEnabled = !high && format.SelectedIndex == 1;
    }
    void Log(string text)
    {
        string line = DateTime.Now.ToString("HH:mm:ss") + " " + text;
        lock (logLock)
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Logs/SupCraft");
            try { Directory.CreateDirectory(directory); File.AppendAllText(Path.Combine(directory, DateTime.Now.ToString("yyyyMMdd") + ".log"), line + "\n"); } catch (IOException) { }
        }
        Dispatcher.UIThread.Post(() => { log.Text = (log.Text + line + "\n"); if (log.Text.Length > 250000) log.Text = log.Text[^200000..]; log.CaretIndex = log.Text.Length; });
    }
    async Task ConvertBatch()
    {
        if (batch != null) return;
        if (jobs.Count == 0 || string.IsNullOrWhiteSpace(output.Text)) { await ShowText("转换参数", "请添加 ASS 并设置输出目录。"); return; }
        var options = new ConversionOptions(output.Text!, fontDirectory.Text, new[] { 720, 1080, 1440, 2160 }[resolution.SelectedIndex], double.Parse(fps.SelectedItem!.ToString()!, CultureInfo.InvariantCulture), (OutputFormat)format.SelectedIndex, language.Text ?? "zho", hdr.IsChecked == true, dim.Value ?? 33, palette.IsChecked == true);
        batch = new CancellationTokenSource(); var token = batch.Token;
        start.IsEnabled = parameters.IsEnabled = fileActions.IsEnabled = false; cancel.IsEnabled = true;
        progress.Value = 0;
        int complete = 0, failed = 0; var snapshot = jobs.ToArray();
        foreach (var job in snapshot) { job.Status = "等待"; job.Details = ""; }
        try
        {
            var service = new ConversionService(Toolchain.Load(Toolchain.BundledManifest));
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var job in snapshot)
            {
                token.ThrowIfCancellationRequested(); job.Status = "转换中"; status.Text = "正在转换：" + job.Name;
                try { var paths = await Task.Run(() => service.ConvertAsync(job.Input, options, used, Log, token), token); job.Status = "完成"; job.Details = string.Join("；", paths); complete++; }
                catch (OperationCanceledException) { job.Status = "已取消"; throw; }
                catch (Exception ex) { job.Status = "失败"; job.Details = ex is ConversionException error ? error.Stage + "：" + error.Message : ex.Message; Log(job.Details + "\n" + ex); failed++; }
                progress.Value = (complete + failed) * 100.0 / snapshot.Length;
            }
            status.Text = $"批次完成：成功 {complete}，失败 {failed}";
        }
        catch (OperationCanceledException) { status.Text = "批次已取消，已完成文件保留。"; }
        catch (Exception ex) { status.Text = "无法开始转换：" + ex.Message; Log(ex.ToString()); await ShowText("转换失败", ex.Message); }
        finally { batch.Dispose(); batch = null; start.IsEnabled = parameters.IsEnabled = fileActions.IsEnabled = true; cancel.IsEnabled = false; UpdateOptions(); if (closeAfterBatch) Close(); }
    }
    async Task CheckFonts()
    {
        if (jobs.Count == 0) { await ShowText("字体检查", "请先添加 ASS。"); return; }
        var catalog = FontCatalog.Create(fontDirectory.Text); var referenced = new SortedSet<string>(StringComparer.OrdinalIgnoreCase); var lines = new List<string>();
        foreach (var job in jobs)
        {
            try { var doc = AssDocument.Read(job.Input); referenced.UnionWith(doc.Fonts); var missing = doc.Fonts.Where(f => !catalog.Available(f)).ToArray(); lines.Add(job.Name + "：" + (missing.Length == 0 ? "字体齐全" : "缺少 " + string.Join("、", missing))); }
            catch (Exception ex) { lines.Add(job.Name + "：" + ex.Message); }
        }
        string report = $"SupCraft 字体检查\n字体目录：{fontDirectory.Text}\n\n缺失字体：\n" + string.Join('\n', referenced.Where(f => !catalog.Available(f))) + "\n\n可用字体：\n" + string.Join('\n', referenced.Where(catalog.Available)) + "\n\n逐文件结果：\n" + string.Join('\n', lines);
        await ShowText("字体检查", report, async () =>
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "导出字体报告", SuggestedFileName = "SupCraft-font-report.txt" });
            if (file != null) { await using var stream = await file.OpenWriteAsync(); stream.SetLength(0); await stream.WriteAsync(Encoding.UTF8.GetBytes(report)); }
        }, async () =>
        {
            var directories = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "收集可用字体" });
            string? directory = directories.FirstOrDefault()?.TryGetLocalPath(); if (directory == null) return;
            var paths = referenced.Select(catalog.FileFor).OfType<string>().Distinct(StringComparer.Ordinal).ToArray(); int copied = 0;
            foreach (string source in paths) { string target = Path.Combine(directory, Path.GetFileName(source)); if (!File.Exists(target)) { File.Copy(source, target); copied++; } }
            Log($"已收集 {copied} 个字体文件；部分系统字体可能只能识别字体族，无法定位可复制文件。");
        });
    }
    async Task ShowText(string title, string text, Func<Task>? save = null, Func<Task>? collect = null)
    {
        var window = new Window { Title = title, Width = 780, Height = 560, MinWidth = 500, MinHeight = 360, Background = Brush.Parse("#EAF0F6"), WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var layout = new DockPanel { Margin = new Thickness(18) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 12, 0, 0) };
        if (save != null) buttons.Children.Add(Action("导出报告…", save)); if (collect != null) buttons.Children.Add(Action("收集可用字体…", collect));
        var close = new Button { Content = "关闭" }; close.Click += (_, _) => window.Close(); buttons.Children.Add(close); DockPanel.SetDock(buttons, Dock.Bottom); layout.Children.Add(buttons);
        layout.Children.Add(new TextBox { Text = text, IsReadOnly = true, AcceptsReturn = true }); window.Content = layout; await window.ShowDialog(this);
    }
}
