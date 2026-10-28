using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;

namespace SupCraft.Mac;

internal static class Appearance
{
    public static bool Dark { get; private set; }
    public static void Apply(bool dark)
    {
        Dark = dark;
        var app = Application.Current!;
        app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        string[] keys = ["Surface", "Card", "Field", "Line", "Ink", "Muted", "Accent", "AccentSoft", "Danger", "Success"];
        string[] colors = dark
            ? ["#151B29", "#20283A", "#263147", "#35425A", "#E7EDFA", "#A3B2CB", "#7094FF", "#2C3D64", "#FF929E", "#71D7B0"]
            : ["#F3F6FC", "#FFFFFF", "#F7F9FD", "#E0E7F5", "#26374E", "#7D8FA9", "#4169EF", "#EDF2FF", "#CC435A", "#218768"];
        for (int i = 0; i < keys.Length; i++) app.Resources[keys[i]] = Brush.Parse(colors[i]);
    }
    public static void Bind(AvaloniaObject control, AvaloniaProperty property, string key) => control.Bind(property, new DynamicResourceExtension(key));
    public static void Style(Window window)
    {
        Bind(window, Window.BackgroundProperty, "Surface");
        Bind(window, Window.ForegroundProperty, "Ink");
        window.FontSize = 13;
        window.Styles.Add(new Style(s => s.OfType<Button>()) { Setters = {
            new Setter(Button.PaddingProperty, new Thickness(15, 9)), new Setter(Button.CornerRadiusProperty, new CornerRadius(10)),
            new Setter(Button.MinHeightProperty, 36d), new Setter(Button.BorderThicknessProperty, new Thickness(1)),
            new Setter(Button.BackgroundProperty, new DynamicResourceExtension("Card")), new Setter(Button.BorderBrushProperty, new DynamicResourceExtension("Line")),
            new Setter(Button.ForegroundProperty, new DynamicResourceExtension("Ink")) } });
        window.Styles.Add(new Style(s => s.OfType<Button>().Class("primary")) { Setters = {
            new Setter(Button.BackgroundProperty, new DynamicResourceExtension("Accent")), new Setter(Button.ForegroundProperty, Brushes.White),
            new Setter(Button.BorderBrushProperty, new DynamicResourceExtension("Accent")) } });
        window.Styles.Add(new Style(s => s.OfType<TextBox>()) { Setters = {
            new Setter(TextBox.CornerRadiusProperty, new CornerRadius(8)), new Setter(TextBox.MinHeightProperty, 34d),
            new Setter(TextBox.BackgroundProperty, new DynamicResourceExtension("Field")), new Setter(TextBox.BorderBrushProperty, new DynamicResourceExtension("Line")) } });
        window.Styles.Add(new Style(s => s.OfType<ComboBox>()) { Setters = {
            new Setter(ComboBox.CornerRadiusProperty, new CornerRadius(8)), new Setter(ComboBox.MinHeightProperty, 34d),
            new Setter(ComboBox.BackgroundProperty, new DynamicResourceExtension("Field")), new Setter(ComboBox.BorderBrushProperty, new DynamicResourceExtension("Line")) } });
        window.Styles.Add(new Style(s => s.OfType<NumericUpDown>()) { Setters = {
            new Setter(NumericUpDown.CornerRadiusProperty, new CornerRadius(8)), new Setter(NumericUpDown.MinHeightProperty, 34d),
            new Setter(NumericUpDown.BackgroundProperty, new DynamicResourceExtension("Field")), new Setter(NumericUpDown.BorderBrushProperty, new DynamicResourceExtension("Line")) } });
    }
    public static TextBlock Text(string text, double size = 13, string color = "Ink")
    {
        var block = new TextBlock { Text = text, FontSize = size, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        Bind(block, TextBlock.ForegroundProperty, color); return block;
    }
    public static Border Card(Control child, double padding = 16)
    {
        var card = new Border { CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1), Padding = new Thickness(padding), Child = child };
        Bind(card, Border.BackgroundProperty, "Card"); Bind(card, Border.BorderBrushProperty, "Line"); return card;
    }
}
