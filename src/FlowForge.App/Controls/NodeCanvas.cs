using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace FlowForge.App.Controls;

/// <summary>
/// 节点画布控件，负责自绘网格和节点。
/// </summary>
public sealed class NodeCanvas : Control
{
    private static readonly IBrush CanvasBackground = new SolidColorBrush(Color.Parse("#F8FAFC"));
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.Parse("#E2E8F0")), 1);
    private static readonly IBrush NodeFill = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly Pen NodeStroke = new(new SolidColorBrush(Color.Parse("#2563EB")), 1.5);
    private static readonly IBrush HeaderFill = new SolidColorBrush(Color.Parse("#EFF6FF"));
    private static readonly IBrush TextFill = new SolidColorBrush(Color.Parse("#1E293B"));

    /// <summary>
    /// Stage 1 中用于验证画布渲染链路的静态占位节点。
    /// </summary>
    public static Rect PlaceholderNode { get; } = new(96, 80, 220, 96);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(CanvasBackground, bounds);
        DrawGrid(context, bounds);
        DrawPlaceholderNode(context);
    }

    private static void DrawGrid(DrawingContext context, Rect bounds)
    {
        const double gridSize = 24;

        for (var x = 0d; x <= bounds.Width; x += gridSize)
        {
            context.DrawLine(GridPen, new Point(x, 0), new Point(x, bounds.Height));
        }

        for (var y = 0d; y <= bounds.Height; y += gridSize)
        {
            context.DrawLine(GridPen, new Point(0, y), new Point(bounds.Width, y));
        }
    }

    private static void DrawPlaceholderNode(DrawingContext context)
    {
        var node = PlaceholderNode;
        var header = new Rect(node.X, node.Y, node.Width, 32);

        context.FillRectangle(NodeFill, node, 8);
        context.DrawRectangle(NodeStroke, node, 8);
        context.FillRectangle(HeaderFill, header, 8);

        var title = new FormattedText(
            "CSV 读取",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            14,
            TextFill);

        context.DrawText(title, new Point(node.X + 16, node.Y + 9));

        var body = new FormattedText(
            "静态占位节点",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            12,
            TextFill);

        context.DrawText(body, new Point(node.X + 16, node.Y + 52));
    }
}
