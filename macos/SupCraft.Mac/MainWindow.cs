using System.Collections.ObjectModel;
using System.Diagnostics;
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
    readonly StackPanel directories = new() { Spacing = 8 };
    readonly CheckBox gpu = new() { Content = "GPU 加速（OpenCL）" };
    readonly TextBlock elapsed = Appearance.Text("已运行 00:00:00", 11, "Muted"), dimensions = Appearance.Text("1920 × 1080 像素", 12, "Accent");
    readonly Stopwatch stopwatch = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly StackPanel fileActions = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    readonly object logLock = new();
    CancellationTokenSource? batch;
    bool closeAfterBatch;

    public MainWindow()
    {
        Title = "SupCraft · macOS · v" + typeof(MainWindow).Assembly.GetName().Version!.ToString(3);
        Width = 1280; Height = 840; MinWidth = 960; MinHeight = 720;
        using (var icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupCraft.Icon.png")) Icon = new WindowIcon(icon!);
        Appearance.Style(this);
        start.Classes.Add("primary"); start.ClearValue(Button.BackgroundProperty); start.ClearValue(Button.ForegroundProperty); start.ClearValue(Button.BorderBrushProperty);
        start.HorizontalAlignment = HorizontalAlignment.Stretch; cancel.Content = "取消"; cancel.MinWidth = 78;
        Appearance.Bind(log, TextBox.BackgroundProperty, "Field"); Appearance.Bind(log, TextBox.ForegroundProperty, "Ink");
        Appearance.Bind(progress, ProgressBar.ForegroundProperty, "Accent"); Appearance.Bind(progress, ProgressBar.BackgroundProperty, "Line");
        var body = new Grid { Margin = new Thickness(18, 12, 18, 18), RowDefinitions = new RowDefinitions("Auto,552,*"), RowSpacing = 12 };
        SizeChanged += (_, _) => body.RowDefinitions[1].Height = new GridLength(Math.Clamp(Bounds.Height - 288, 420, 552));
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 18, Margin = new Thickness(0, 0, 0, 4) };
        using (var icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("SupCraft.Icon.png"))
            heading.Children.Add(new Image { Source = new Bitmap(icon!), Width = 40, Height = 40 });
        var title = Appearance.Text("SupCraft", 26); title.FontWeight = FontWeight.Bold; Grid.SetColumn(title, 1); heading.Children.Add(title);
        var subtitle = Appearance.Text("ASS → SUP / BDN 字幕转换器 · macOS", 13, "Muted"); Grid.SetColumn(subtitle, 2); heading.Children.Add(subtitle);
        var theme = new Button { Content = Appearance.Dark ? "浅色模式" : "深色模式" };
        theme.Click += (_, _) => { Appearance.Apply(!Appearance.Dark); theme.Content = Appearance.Dark ? "浅色模式" : "深色模式"; };
        Grid.SetColumn(theme, 3); heading.Children.Add(theme); body.Children.Add(heading);
        var workspace = new Grid { ColumnDefinitions = new ColumnDefinitions("*,460"), ColumnSpacing = 12 };
        Grid.SetRow(workspace, 1); body.Children.Add(workspace);
        var queuePanel = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto,Auto"), RowSpacing = 10 };
        var queueTitle = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        queueTitle.Children.Add(SectionHeading("任务与文件", ""));
        var count = Appearance.Text("0 个任务", 12, "Muted"); Grid.SetColumn(count, 1); queueTitle.Children.Add(count); queuePanel.Children.Add(queueTitle);
        var add = Action("＋ 添加 ASS", PickFiles); add.Classes.Add("primary");
        fileActions.Children.Add(add);
        fileActions.Children.Add(Action("移除", () => { foreach (var job in queue.SelectedItems!.Cast<Job>().ToArray()) jobs.Remove(job); return Task.CompletedTask; }));
        fileActions.Children.Add(Action("清空", () => { jobs.Clear(); return Task.CompletedTask; }));
        fileActions.Children.Add(Action("字体管理", CheckFonts));
        Grid.SetRow(fileActions, 1); queuePanel.Children.Add(fileActions);
        var columns = TableRow(["状态", "ASS 文件", "输出 / 错误"], true); Grid.SetRow(columns, 2); queuePanel.Children.Add(columns);
        queue.ItemsSource = jobs;
        queue.Styles.Add(new Style(s => s.OfType<ListBoxItem>()) { Setters = { new Setter(ListBoxItem.PaddingProperty, new Thickness(0)), new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch) } });
        queue.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Job>((job, _) =>
        {
            var row = TableRow(["", job!.Name, ""], false);
            var state = (TextBlock)((Border)row.Children[0]).Child!;
            state.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(Job.Status)));
            void ColorStatus() => Appearance.Bind(state, TextBlock.ForegroundProperty, job.Status == "失败" ? "Danger" : job.Status == "完成" ? "Success" : job.Status == "转换中" ? "Accent" : "Muted");
            job.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Job.Status)) ColorStatus(); }; ColorStatus();
            ((TextBlock)((Border)row.Children[2]).Child!).Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(Job.Details)));
            ToolTip.SetTip(row, job.Input); return row;
        });
        queue.DoubleTapped += async (_, _) => { if (queue.SelectedItem is Job job) await ShowText("任务详情", job.Input + "\n\n" + job.Status + "\n" + job.Details); };
        var table = new Grid { ClipToBounds = true }; table.Children.Add(new GridLines(true)); table.Children.Add(queue);
        Grid.SetRow(table, 3); queuePanel.Children.Add(table);
        var hint = Appearance.Text("支持拖入多个 ASS 文件 · 双击任务查看详情", 11, "Muted"); Grid.SetRow(hint, 4); queuePanel.Children.Add(hint);
        directories.Children.Add(PathRow("输出目录", output)); directories.Children.Add(PathRow("字体目录（可选）", fontDirectory));
        Grid.SetRow(directories, 5); queuePanel.Children.Add(directories);
        output.PlaceholderText = "留空：各 ASS 所在目录"; fontDirectory.PlaceholderText = "留空使用系统字体";
        ToolTip.SetTip(output, "留空时，各任务输出到各自 ASS 所在目录；选择目录可集中输出。");
        ToolTip.SetTip(fontDirectory, "递归读取 TTF / OTF / TTC，无需安装字体。");
        jobs.CollectionChanged += (_, _) => { count.Text = $"{jobs.Count} 个任务"; if (batch == null) status.Text = jobs.Count == 0 ? "就绪 · 等待添加 ASS 文件" : $"就绪 · {jobs.Count} 个任务"; };
        workspace.Children.Add(Card(queuePanel));
        var settings = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 14 }; settings.Children.Add(SectionHeading("转换设置", ""));
        parameters.Children.Add(Field("转换核心", Appearance.Text("libass / ass2bdnxml / SUPer", 13)));
        var fields = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 16 };
        resolution.Width = double.NaN; fps.Width = double.NaN; format.Width = double.NaN; language.Width = double.NaN;
        foreach (var c in new Control[] { resolution, fps, format, language }) c.HorizontalAlignment = HorizontalAlignment.Stretch;
        fields.Children.Add(Field("分辨率", resolution)); var frameField = Field("帧率 (fps)", fps); Grid.SetColumn(frameField, 1); fields.Children.Add(frameField); parameters.Children.Add(fields);
        dimensions.TextWrapping = TextWrapping.Wrap; parameters.Children.Add(dimensions);
        var formats = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 16 }; formats.Children.Add(Field("输出格式", format)); var langField = Field("语言代码", language); Grid.SetColumn(langField, 1); formats.Children.Add(langField); parameters.Children.Add(formats);
        var color = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") }; hdr.VerticalAlignment = VerticalAlignment.Center; color.Children.Add(hdr); var brightness = Field("亮度缩减 %", dim); Grid.SetColumn(brightness, 1); color.Children.Add(brightness); parameters.Children.Add(color);
        parameters.Children.Add(gpu); parameters.Children.Add(palette);
        ToolTip.SetTip(gpu, "用于 SUPer 图像相似度计算；不可用时自动回退 CPU。仅 SUP 输出可用。");
        ToolTip.SetTip(palette, "仅 720p / 1080p 的 BDN 输出可用。SUP 的量化由 SUPer 处理。");
        var settingsScroll = new ScrollViewer { Content = parameters, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(settingsScroll, 1); settings.Children.Add(settingsScroll);
        var run = new StackPanel { Spacing = 12 };
        var runState = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
        status.FontSize = 11; Appearance.Bind(status, TextBlock.ForegroundProperty, "Muted"); runState.Children.Add(status); Grid.SetColumn(elapsed, 1); runState.Children.Add(elapsed); run.Children.Add(runState); run.Children.Add(progress);
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 }; actions.Children.Add(start); Grid.SetColumn(cancel, 1); actions.Children.Add(cancel); run.Children.Add(actions);
        Grid.SetRow(run, 2); settings.Children.Add(run); var settingsCard = Card(settings); Grid.SetColumn(settingsCard, 1); workspace.Children.Add(settingsCard);
        resolution.SelectionChanged += (_, _) => UpdateOptions(); format.SelectionChanged += (_, _) => UpdateOptions(); hdr.IsCheckedChanged += (_, _) => dim.IsEnabled = hdr.IsChecked == true;
        start.Click += async (_, _) => await ConvertBatch(); cancel.Click += (_, _) => { cancel.IsEnabled = false; batch?.Cancel(); status.Text = "正在取消并清理子进程…"; };
        timer.Tick += (_, _) => UpdateElapsed();
        var logs = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 10 };
        var logHeading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") }; logHeading.Children.Add(SectionHeading("运行日志", ""));
        var clear = Action("清空日志", () => { log.Text = ""; return Task.CompletedTask; }); clear.Padding = new Thickness(12, 4); clear.MinHeight = 28; Grid.SetColumn(clear, 1); logHeading.Children.Add(clear); logs.Children.Add(logHeading);
        Grid.SetRow(log, 1); logs.Children.Add(log); var logCard = Card(logs); Grid.SetRow(logCard, 2); body.Children.Add(logCard); Content = body;
        Closing += (_, args) => { if (batch != null) { args.Cancel = true; closeAfterBatch = true; batch.Cancel(); status.Text = "正在取消任务并清理子进程…"; } };
        UpdateOptions();
        Log("程序已启动。添加 ASS 后即可转换；输出目录留空时保存到各 ASS 所在目录。");
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, (_, args) => args.DragEffects = batch == null && args.DataTransfer.TryGetFiles() != null ? DragDropEffects.Copy : DragDropEffects.None);
        AddHandler(DragDrop.DropEvent, (_, args) => AddFiles((args.DataTransfer.TryGetFiles() ?? []).Select(file => file.TryGetLocalPath()).OfType<string>()));
    }
    internal void CheckUiInteractions()
    {
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException("UI check: " + message); }
        Require(string.IsNullOrEmpty(output.Text), "default source-directory output");
        resolution.SelectedIndex = 3;
        Require(format.SelectedIndex == 1 && !format.IsEnabled && !gpu.IsEnabled && !palette.IsEnabled, "high BDN option locks");
        resolution.SelectedIndex = 1;
        Require(format.IsEnabled && palette.IsEnabled, "standard BDN option unlocks");
        format.SelectedIndex = 0;
        Require(gpu.IsEnabled && !palette.IsVisible, "SUP options");
        hdr.IsChecked = true; Require(dim.IsEnabled, "HDR brightness enabled"); hdr.IsChecked = false; Require(!dim.IsEnabled, "HDR brightness disabled");
        bool original = Appearance.Dark; Appearance.Apply(!original); Require(Appearance.Dark != original, "theme toggle"); Appearance.Apply(original);
        Require(start.IsEnabled && !cancel.IsEnabled, "idle controls");
        Console.WriteLine("PASS: UI source-output default, resolution/format/GPU locks, HDR controls and theme switching.");
    }
    void UpdateElapsed() => elapsed.Text = "已运行 " + $"{(long)stopwatch.Elapsed.TotalHours:00}:{stopwatch.Elapsed.Minutes:00}:{stopwatch.Elapsed.Seconds:00}";
    static ComboBox Combo(string[] items, int selected, double width) => new() { ItemsSource = items, SelectedIndex = selected, Width = width };
    static Control Field(string label, Control control) { var panel = new StackPanel { Spacing = 6 }; panel.Children.Add(Appearance.Text(label, 12, "Muted")); panel.Children.Add(control); return panel; }
    static Border Card(Control child) => Appearance.Card(child);
    static Control SectionHeading(string title, string hint)
    {
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var marker = new Border { Width = 3, Height = 18, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center }; Appearance.Bind(marker, Border.BackgroundProperty, "Accent"); heading.Children.Add(marker);
        var text = Appearance.Text(title, 17); text.FontWeight = FontWeight.SemiBold; heading.Children.Add(text); return heading;
    }
    static Grid TableRow(string[] values, bool heading)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("70,1.5*,*"), Height = heading ? 26 : 36 };
        for (int i = 0; i < values.Length; i++)
        {
            var text = Appearance.Text(values[i], 12, heading ? "Muted" : "Ink"); text.FontWeight = heading ? FontWeight.SemiBold : FontWeight.Normal; text.TextTrimming = TextTrimming.CharacterEllipsis;
            var cell = new Border { BorderThickness = new Thickness(0, 0, i < 2 ? 1 : 0, 1), Padding = new Thickness(9, 0), Child = text }; Appearance.Bind(cell, Border.BorderBrushProperty, "Line");
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
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*,70"), ColumnSpacing = 8 };
        grid.Children.Add(Appearance.Text(label, 12)); Grid.SetColumn(box, 1); grid.Children.Add(box);
        var browse = Action("浏览…", async () => { var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = label }); if (folders.Count > 0) box.Text = folders[0].TryGetLocalPath(); });
        browse.Padding = new Thickness(8, 6); Grid.SetColumn(browse, 2); grid.Children.Add(browse); return grid;
    }
    public void AddFiles(IEnumerable<string> files)
    {
        if (batch != null) return;
        foreach (string file in files.Where(p => p.EndsWith(".ass", StringComparison.OrdinalIgnoreCase) && File.Exists(p)))
            if (!jobs.Any(j => j.Input == Path.GetFullPath(file))) jobs.Add(new Job(Path.GetFullPath(file)));
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
        palette.IsVisible = format.SelectedIndex == 1;
        palette.IsEnabled = !high && format.SelectedIndex == 1;
        gpu.IsEnabled = format.SelectedIndex != 1;
        dimensions.Text = resolution.SelectedIndex switch { 0 => "1280 × 720 像素", 1 => "1920 × 1080 像素", 2 => "2560 × 1440 · 仅 BDN，由 1080p 缩放", _ => "3840 × 2160 · 仅 BDN，由 1080p 缩放" };
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
        if (jobs.Count == 0) { await ShowText("转换参数", "请先添加 ASS 字幕。"); return; }
        var options = new ConversionOptions(output.Text ?? "", fontDirectory.Text, new[] { 720, 1080, 1440, 2160 }[resolution.SelectedIndex], double.Parse(fps.SelectedItem!.ToString()!, CultureInfo.InvariantCulture), (OutputFormat)format.SelectedIndex, language.Text ?? "zho", hdr.IsChecked == true, dim.Value ?? 33, palette.IsChecked == true, gpu.IsChecked == true);
        batch = new CancellationTokenSource(); var token = batch.Token;
        start.IsEnabled = parameters.IsEnabled = directories.IsEnabled = fileActions.IsEnabled = false;
        stopwatch.Restart(); UpdateElapsed(); timer.Start(); cancel.IsEnabled = true;
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
        finally { stopwatch.Stop(); timer.Stop(); UpdateElapsed(); batch.Dispose(); batch = null; start.IsEnabled = parameters.IsEnabled = directories.IsEnabled = fileActions.IsEnabled = true; cancel.IsEnabled = false; UpdateOptions(); if (closeAfterBatch) Close(); }
    }
    async Task CheckFonts()
    {
        if (jobs.Count == 0) { await ShowText("字体管理", "请先添加 ASS。"); return; }
        var window = new FontManagerWindow(jobs.Select(j => j.Input).ToArray(), fontDirectory.Text, changed =>
        {
            foreach (var job in jobs.Where(j => changed.Contains(j.Input))) { job.Status = "等待"; job.Details = "字体已更新"; }
        }, Log);
        await window.ShowDialog(this);
    }
    async Task ShowText(string title, string text, Func<Task>? save = null, Func<Task>? collect = null)
    {
        var window = new Window { Title = title, Width = 780, Height = 560, MinWidth = 500, MinHeight = 360, Background = Brush.Parse("#EAF0F6"), WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Appearance.Style(window);
        var layout = new DockPanel { Margin = new Thickness(18) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 12, 0, 0) };
        if (save != null) buttons.Children.Add(Action("导出报告…", save)); if (collect != null) buttons.Children.Add(Action("收集可用字体…", collect));
        var close = new Button { Content = "关闭" }; close.Click += (_, _) => window.Close(); buttons.Children.Add(close); DockPanel.SetDock(buttons, Dock.Bottom); layout.Children.Add(buttons);
        layout.Children.Add(new TextBox { Text = text, IsReadOnly = true, AcceptsReturn = true }); window.Content = layout; await window.ShowDialog(this);
    }
}
