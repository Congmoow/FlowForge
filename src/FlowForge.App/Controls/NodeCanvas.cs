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
    private const double PortRadius = 5;
    private const double PortHitRadius = 10;

    private static readonly IBrush CanvasBackground = new SolidColorBrush(Color.Parse("#F8FAFC"));
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.Parse("#E2E8F0")), 1);
    private static readonly IBrush NodeFill = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly Pen NodeStroke = new(new SolidColorBrush(Color.Parse("#2563EB")), 1.5);
    private static readonly Pen SelectedNodeStroke = new(new SolidColorBrush(Color.Parse("#16A34A")), 2.5);
    private static readonly IBrush PortFill = new SolidColorBrush(Color.Parse("#2563EB"));
    private static readonly Pen PortStroke = new(new SolidColorBrush(Color.Parse("#FFFFFF")), 1.5);
    private static readonly Pen EdgePen = new(new SolidColorBrush(Color.Parse("#64748B")), 2);
    private static readonly IBrush IncompatibleFill = new SolidColorBrush(Color.Parse("#DC2626"));
    private static readonly Pen IncompatibleStroke = new(new SolidColorBrush(Color.Parse("#FFFFFF")), 1.5);
    private static readonly IBrush HeaderFill = new SolidColorBrush(Color.Parse("#EFF6FF"));
    private static readonly IBrush TextFill = new SolidColorBrush(Color.Parse("#1E293B"));

    /// <summary>
    /// Stage 1 中用于验证画布渲染链路的静态占位节点。
    /// </summary>
    public static Rect PlaceholderNode { get; } = new(96, 80, 220, 96);

    private Guid? draggingNodeId;
    private bool isDraggingSelection;
    private Point lastPointerPosition;
    private bool isDraggingEdge;

    /// <summary>
    /// 画布 ViewModel 属性。
    /// </summary>
    public static readonly StyledProperty<CanvasViewModel?> CanvasProperty =
        AvaloniaProperty.Register<NodeCanvas, CanvasViewModel?>(nameof(Canvas));

    /// <summary>
    /// 节点库 ViewModel 属性。
    /// </summary>
    public static readonly StyledProperty<ToolboxViewModel?> ToolboxProperty =
        AvaloniaProperty.Register<NodeCanvas, ToolboxViewModel?>(nameof(Toolbox));

    /// <summary>
    /// 初始化节点画布控件。
    /// </summary>
    public NodeCanvas()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    /// <summary>
    /// 画布 ViewModel。
    /// </summary>
    public CanvasViewModel? Canvas
    {
        get => GetValue(CanvasProperty);
        set => SetValue(CanvasProperty, value);
    }

    /// <summary>
    /// 节点库 ViewModel。
    /// </summary>
    public ToolboxViewModel? Toolbox
    {
        get => GetValue(ToolboxProperty);
        set => SetValue(ToolboxProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(CanvasBackground, bounds);
        DrawGrid(context, bounds);

        if (Canvas is { Nodes.Count: > 0 } canvas)
        {
            foreach (var edge in canvas.Edges)
            {
                DrawEdge(context, edge);
            }

            if (canvas.DraftEdge is not null)
            {
                DrawEdge(context, canvas.DraftEdge);
            }

            foreach (var node in canvas.Nodes)
            {
                DrawNode(context, node);
            }

            DrawConnectionPreview(context, canvas);

            return;
        }

        DrawPlaceholderNode(context);
    }

    private static void DrawEdge(DrawingContext context, EdgeViewModel edge)
    {
        var geometry = BezierEdgeShape.CreateGeometry(edge.StartPoint, edge.EndPoint);
        context.DrawGeometry(null, EdgePen, geometry);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Canvas is not { } canvas)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        var port = FindPortAt(canvas, position);
        var node = FindNodeAt(canvas, position);
        if (ShouldUseAddNodeShortcut(e.KeyModifiers, node is not null, port is not null, Toolbox is not null))
        {
            canvas.AddNodeFromTemplateCommand.Execute(new AddNodeFromTemplateRequest(Toolbox!.SelectedTemplate, position));
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (port is { Direction: PortDirection.Output })
        {
            isDraggingEdge = true;
            canvas.BeginEdgeDragCommand.Execute(new BeginEdgeDragRequest(port, position));
            e.Pointer.Capture(this);
            e.Handled = true;
            InvalidateVisual();
            return;
        }

        if (node is null)
        {
            canvas.ClearSelectionCommand.Execute(null);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        canvas.SelectNodeCommand.Execute(new SelectNodeRequest(node.Id, ResolveSelectionGesture(e.KeyModifiers, node.IsSelected)));
        draggingNodeId = node.Id;
        isDraggingSelection = node.IsSelected && canvas.Nodes.Count(candidate => candidate.IsSelected) > 1;
        lastPointerPosition = position;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (isDraggingEdge && Canvas is { } edgeCanvas)
        {
            var position = e.GetPosition(this);
            edgeCanvas.UpdateEdgeDragCommand.Execute(position);
            edgeCanvas.PreviewEdgeTargetCommand.Execute(FindPortAt(edgeCanvas, position));
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (draggingNodeId is not { } nodeId || Canvas is not { } canvas)
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

        if (isDraggingSelection)
        {
            canvas.MoveSelectedNodesCommand.Execute(delta);
        }
        else
        {
            canvas.MoveNodeCommand.Execute(new MoveNodeRequest(nodeId, delta));
        }
        lastPointerPosition = currentPosition;
        InvalidateVisual();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (isDraggingEdge && Canvas is { } canvas)
        {
            var targetPort = FindPortAt(canvas, e.GetPosition(this));
            if (targetPort is { Direction: PortDirection.Input })
            {
                canvas.CompleteEdgeDragCommand.Execute(targetPort);
            }
            else
            {
                canvas.CancelEdgeDragCommand.Execute(null);
            }

            isDraggingEdge = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

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
        DrawNode(context, bounds, node.Title, node.TypeId, node.IsSelected);
        DrawPorts(context, node);
    }

    private static void DrawNode(DrawingContext context, Rect node, string titleText, string bodyText, bool isSelected = false)
    {
        var header = new Rect(node.X, node.Y, node.Width, NodeHeaderHeight);

        context.FillRectangle(NodeFill, node, 8);
        context.DrawRectangle(isSelected ? SelectedNodeStroke : NodeStroke, node, 8);
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

    private static void DrawPorts(DrawingContext context, NodeViewModel node)
    {
        foreach (var port in node.Inputs.Concat(node.Outputs))
        {
            context.DrawEllipse(PortFill, PortStroke, port.AnchorPoint, PortRadius, PortRadius);
        }
    }

    private static void DrawConnectionPreview(DrawingContext context, CanvasViewModel canvas)
    {
        if (canvas.ConnectionPreviewState != ConnectionPreviewState.Incompatible || canvas.PreviewTargetPort is null)
        {
            return;
        }

        var center = canvas.PreviewTargetPort.AnchorPoint + new Vector(-18, -18);
        context.DrawEllipse(IncompatibleFill, IncompatibleStroke, center, 8, 8);
        context.DrawLine(IncompatibleStroke, center + new Vector(-4, -4), center + new Vector(4, 4));
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

    private static PortViewModel? FindPortAt(CanvasViewModel canvas, Point position)
    {
        foreach (var node in canvas.Nodes.Reverse())
        {
            foreach (var port in node.Inputs.Concat(node.Outputs))
            {
                if (Distance(port.AnchorPoint, position) <= PortHitRadius)
                {
                    return port;
                }
            }
        }

        return null;
    }

    private static double Distance(Point first, Point second)
    {
        var delta = first - second;
        return Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
    }

    /// <summary>
    /// 根据修饰键和节点当前选择状态解析选择手势。
    /// </summary>
    /// <param name="modifiers">当前键盘修饰键。</param>
    /// <param name="isNodeAlreadySelected">节点按下前是否已经选中。</param>
    /// <returns>应发送给画布 ViewModel 的选择手势。</returns>
    public static SelectionGesture ResolveSelectionGesture(KeyModifiers modifiers, bool isNodeAlreadySelected)
    {
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            return SelectionGesture.Toggle;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift) || isNodeAlreadySelected)
        {
            return SelectionGesture.Add;
        }

        return SelectionGesture.Replace;
    }

    /// <summary>
    /// 判断当前点击是否应该使用从节点库快速添加节点的快捷入口。
    /// </summary>
    /// <param name="modifiers">当前键盘修饰键。</param>
    /// <param name="hasHitNode">是否命中了已有节点。</param>
    /// <param name="hasHitPort">是否命中了已有端口。</param>
    /// <param name="hasToolbox">是否绑定了节点库。</param>
    /// <returns>如果应在当前位置添加节点则返回 true。</returns>
    public static bool ShouldUseAddNodeShortcut(KeyModifiers modifiers, bool hasHitNode, bool hasHitPort, bool hasToolbox)
    {
        return hasToolbox
            && modifiers.HasFlag(KeyModifiers.Control)
            && !hasHitNode
            && !hasHitPort;
    }

    private void StopDragging(IPointer pointer)
    {
        if (draggingNodeId is null)
        {
            return;
        }

        draggingNodeId = null;
        isDraggingSelection = false;
        pointer.Capture(null);
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (Canvas is not { } canvas || Toolbox is not { } toolbox)
        {
            return;
        }

        var template = e.Data.Get(ToolboxListBox.DragNodeTemplateFormat) as NodeTemplateViewModel ?? toolbox.SelectedTemplate;
        canvas.AddNodeFromTemplateCommand.Execute(new AddNodeFromTemplateRequest(template, e.GetPosition(this)));
        InvalidateVisual();
        e.Handled = true;
    }
}
