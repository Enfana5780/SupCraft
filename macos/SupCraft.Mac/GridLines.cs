using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SupCraft.Mac;

// Queue guides use the same columns and row height as the task table.
internal sealed class GridLines : Control
{
    public static readonly StyledProperty<IBrush?> LineBrushProperty = AvaloniaProperty.Register<GridLines, IBrush?>(nameof(LineBrush));
    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    static GridLines() => AffectsRender<GridLines>(LineBrushProperty);
    public GridLines(bool table) => Appearance.Bind(this, LineBrushProperty, "Line");
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        double width = Bounds.Width, height = Bounds.Height;
        var pen = new Pen(LineBrush, 1);
        for (double y = 36; y < height; y += 36) context.DrawLine(pen, new Point(0, y), new Point(width, y));
        foreach (double x in new[] { 70d, 70 + Math.Max(0, width - 70) * 1.5 / 2.5 })
            context.DrawLine(pen, new Point(x, 0), new Point(x, height));
    }
}
