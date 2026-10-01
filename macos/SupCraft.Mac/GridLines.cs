using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SupCraft.Mac;

// Queue guides line up with the header and task cells, including the empty queue.
// The desktop grid stays subtle so controls and subtitle paths remain easy to read.
internal sealed class GridLines(bool table) : Control
{
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        double width = Bounds.Width, height = Bounds.Height;
        var pen = new Pen(Brush.Parse(table ? "#DFE7EF" : "#DCE5EE"), 1);
        double step = table ? 36 : 28;
        for (double y = step; y < height; y += step)
            context.DrawLine(pen, new Point(0, y), new Point(width, y));
        if (table)
        {
            foreach (double x in new[] { 78d, 78 + Math.Max(0, width - 78) * 1.7 / 2.7 })
                context.DrawLine(pen, new Point(x, 0), new Point(x, height));
        }
        else
            for (double x = step; x < width; x += step)
                context.DrawLine(pen, new Point(x, 0), new Point(x, height));
    }
}
