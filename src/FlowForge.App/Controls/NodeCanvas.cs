using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using FlowForge.App.ViewModels;

namespace FlowForge.App.Controls;

/// <summary>
/// 节点画布控件，负责自绘网格和节点。
/// </summary>
public sealed class NodeCanvas : Control
{
    private const double NodeWidth = 220;
    private const double NodeHeight = 96;
    private const double NodeHeaderHeight = 32;

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

    private Guid? draggingNodeId;
    private Point lastPointerPosition;

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(CanvasBackground, bounds);
        DrawGrid(context, bounds);

        if (DataContext is CanvasViewModel canvas && canvas.Nodes.Count > 0)
        {
            foreach (var node in canvas.Nodes)
            {
                DrawNode(context, node);
            }

            return;
        }

        DrawPlaceholderNode(context);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (DataContext is not CanvasViewModel canvas)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        var node = FindNodeAt(canvas, position);
        if (node is null)
        {
            return;
        }

        draggingNodeId = node.Id;
        lastPointerPosition = position;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (draggingNodeId is not { } nodeId || DataContext is not CanvasViewModel canvas)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            StopDragging(e.Pointer);
            return;
        }

        var currentPosition = e.GetPosition(this);
        var delta = currentPosition - lastPointerPosition;
        if (delta == default)
        {
            return;
        }

        canvas.MoveNodeCommand.Execute(new MoveNodeRequest(nodeId, delta));
        lastPointerPosition = currentPosition;
        InvalidateVisual();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        StopDragging(e.Pointer);
        e.Handled = true;
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
        DrawNode(context, PlaceholderNode, "CSV 读取", "静态占位节点");
    }

    private static void DrawNode(DrawingContext context, NodeViewModel node)
    {
        var bounds = new Rect(node.Position, new Size(NodeWidth, NodeHeight));
        DrawNode(context, bounds, node.Title, node.TypeId);
    }

    private static void DrawNode(DrawingContext context, Rect node, string titleText, string bodyText)
    {
        var header = new Rect(node.X, node.Y, node.Width, NodeHeaderHeight);

        context.FillRectangle(NodeFill, node, 8);
        context.DrawRectangle(NodeStroke, node, 8);
        context.FillRectangle(HeaderFill, header, 8);

        var title = new FormattedText(
            titleText,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            14,
            TextFill);

        context.DrawText(title, new Point(node.X + 16, node.Y + 9));

        var body = new FormattedText(
            bodyText,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            12,
            TextFill);

        context.DrawText(body, new Point(node.X + 16, node.Y + 52));
    }

    private static NodeViewModel? FindNodeAt(CanvasViewModel canvas, Point position)
    {
        foreach (var node in canvas.Nodes.Reverse())
        {
            var bounds = new Rect(node.Position, new Size(NodeWidth, NodeHeight));
            if (bounds.Contains(position))
            {
                return node;
            }
        }

        return null;
    }

    private void StopDragging(IPointer pointer)
    {
        if (draggingNodeId is null)
        {
            return;
        }

        draggingNodeId = null;
        pointer.Capture(null);
    }
}
