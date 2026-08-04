using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;

namespace FlowForge.App.Canvas;

/// <summary>
/// 承载单个 retained operation 的子 visual。
/// </summary>
public sealed class RetainedOperationVisual : Control, IDisposable
{
    private ICustomDrawOperation operation;
    private ViewportTransform transform;
    private LocalOperation localOperation;
    private bool disposed;

    /// <summary>
    /// 初始化 retained operation 子 visual。
    /// </summary>
    /// <param name="operation">要承载的不可变绘制操作。</param>
    /// <param name="transform">当前 world/view 变换。</param>
    public RetainedOperationVisual(ICustomDrawOperation operation, ViewportTransform transform)
    {
        this.operation = operation ?? throw new ArgumentNullException(nameof(operation));
        this.transform = transform;
        OperationId = ResolveOperationId(operation);
        WorldBounds = operation.Bounds;
        ViewBounds = transform.WorldToView(WorldBounds);
        localOperation = new LocalOperation(operation, 1);
        IsHitTestVisible = false;
    }

    /// <summary>操作的稳定 ID。</summary>
    public Guid OperationId { get; }

    /// <summary>当前承载的 retained operation。</summary>
    public ICustomDrawOperation Operation => operation;

    /// <summary>操作在 world 坐标中的紧边界。</summary>
    public Rect WorldBounds { get; private set; }

    /// <summary>操作在画布 view 坐标中的紧边界。</summary>
    public Rect ViewBounds { get; private set; }

    /// <summary>
    /// 只更新视口变换，不替换 operation 或触发子 visual 重绘。
    /// </summary>
    /// <param name="nextTransform">新的 world/view 变换。</param>
    public void ApplyViewportTransform(ViewportTransform nextTransform)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var nextViewBounds = nextTransform.WorldToView(WorldBounds);
        if (transform == nextTransform && ViewBounds == nextViewBounds)
        {
            return;
        }

        transform = nextTransform;
        ViewBounds = nextViewBounds;
    }

    /// <summary>
    /// 替换 operation 或更新坐标变换，并只使当前子 visual 失效。
    /// </summary>
    /// <param name="nextOperation">新的不可变绘制操作。</param>
    /// <param name="nextTransform">新的 world/view 变换。</param>
    public void Update(ICustomDrawOperation nextOperation, ViewportTransform nextTransform)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(nextOperation);

        var operationChanged = !ReferenceEquals(operation, nextOperation);
        var nextWorldBounds = nextOperation.Bounds;
        if (!operationChanged && WorldBounds == nextWorldBounds)
        {
            ApplyViewportTransform(nextTransform);
            return;
        }

        operation = nextOperation;
        WorldBounds = nextWorldBounds;
        ApplyViewportTransform(nextTransform);
        if (operationChanged)
        {
            localOperation = new LocalOperation(nextOperation, 1);
        }

        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        return WorldBounds.Size;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        context.Custom(localOperation);

        if (operation is not NodeDrawOperation node)
        {
            return;
        }

        context.DrawText(node.TitleText, new Point(16, 9));
        context.DrawText(node.BodyText, new Point(16, 52));
    }

    /// <summary>释放子 visual 持有的局部 operation 包装。</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        localOperation.Dispose();
    }

    private static Guid ResolveOperationId(ICustomDrawOperation operation)
    {
        return operation switch
        {
            NodeDrawOperation node => node.Snapshot.Id,
            EdgeDrawOperation edge => edge.Snapshot.Id,
            _ => throw new ArgumentException("仅支持 FlowForge retained operation。", nameof(operation)),
        };
    }

    private sealed class LocalOperation : ICustomDrawOperation
    {
        private readonly ICustomDrawOperation source;
        private readonly double zoom;
        private readonly Rect sourceBounds;

        public LocalOperation(ICustomDrawOperation source, double zoom)
        {
            this.source = source;
            this.zoom = zoom;
            sourceBounds = source.Bounds;
        }

        public Rect Bounds => new(0, 0, sourceBounds.Width * zoom, sourceBounds.Height * zoom);

        public bool HitTest(Point p)
        {
            return source.HitTest(new Point(
                p.X / zoom + sourceBounds.X,
                p.Y / zoom + sourceBounds.Y));
        }

        public void Render(ImmediateDrawingContext context)
        {
            using (context.PushSetTransform(new Matrix(
                zoom,
                0,
                0,
                zoom,
                -sourceBounds.X * zoom,
                -sourceBounds.Y * zoom)))
            {
                source.Render(context);
            }
        }

        public bool Equals(ICustomDrawOperation? other)
        {
            return other is LocalOperation candidate
                && Math.Abs(zoom - candidate.zoom) < double.Epsilon
                && source.Equals(candidate.source);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ICustomDrawOperation);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(source, zoom);
        }

        public void Dispose()
        {
            // The retained scene owns and disposes the source operation.
        }
    }
}
