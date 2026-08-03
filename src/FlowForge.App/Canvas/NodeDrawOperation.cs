using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.SceneGraph;

namespace FlowForge.App.Canvas;

/// <summary>
/// 节点 retained 绘制操作，持有不可变快照和缓存资源。
/// </summary>
public sealed class NodeDrawOperation : ICustomDrawOperation
{
    private static readonly IImmutableBrush Fill = new ImmutableSolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IImmutableBrush HeaderFill = new ImmutableSolidColorBrush(Color.Parse("#EFF6FF"));
    private static readonly IImmutableBrush PortFill = new ImmutableSolidColorBrush(Color.Parse("#2563EB"));
    private static readonly ImmutablePen PortPen = CreatePen(Color.Parse("#FFFFFF"), 1.5);
    private readonly ImmutablePen borderPen;
    private readonly ImmutablePen selectedBorderPen;
    private bool disposed;

    /// <summary>初始化节点绘制操作。</summary>
    /// <param name="snapshot">不可变绘制快照。</param>
    public NodeDrawOperation(NodeDrawSnapshot snapshot)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        TitleText = new FormattedText(
            snapshot.Title,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            14,
            new ImmutableSolidColorBrush(Color.Parse("#1E293B")));
        BodyText = new FormattedText(
            snapshot.Body,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            12,
            new ImmutableSolidColorBrush(Color.Parse("#1E293B")));
        borderPen = CreatePen(snapshot.BorderColor, 1.5);
        selectedBorderPen = CreatePen(snapshot.BorderColor, 2.5);
    }

    /// <summary>不可变节点绘制快照。</summary>
    public NodeDrawSnapshot Snapshot { get; }

    /// <summary>缓存的节点标题文本。</summary>
    public FormattedText TitleText { get; }

    /// <summary>缓存的节点正文文本。</summary>
    public FormattedText BodyText { get; }

    /// <summary>操作是否已经释放。</summary>
    public bool IsDisposed => disposed;

    /// <inheritdoc />
    public Rect Bounds => Snapshot.Bounds;

    /// <inheritdoc />
    public bool HitTest(Point p)
    {
        return !disposed && Bounds.Contains(p);
    }

    /// <inheritdoc />
    public void Render(ImmediateDrawingContext context)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var header = new Rect(Bounds.X, Bounds.Y, Bounds.Width, 32);
        context.DrawRectangle(Fill, Snapshot.IsSelected ? selectedBorderPen : borderPen, Bounds, 8, 8, new BoxShadows());
        context.DrawRectangle(HeaderFill, null, header, 8, 8, new BoxShadows());
        foreach (var point in Snapshot.PortPoints)
        {
            context.DrawEllipse(PortFill, PortPen, point, 5, 5);
        }
    }

    /// <inheritdoc />
    public bool Equals(ICustomDrawOperation? other)
    {
        return other is NodeDrawOperation operation
            && Snapshot.Id == operation.Snapshot.Id
            && Snapshot.Bounds == operation.Snapshot.Bounds
            && string.Equals(Snapshot.Title, operation.Snapshot.Title, StringComparison.Ordinal)
            && string.Equals(Snapshot.Body, operation.Snapshot.Body, StringComparison.Ordinal)
            && Snapshot.BorderColor == operation.Snapshot.BorderColor
            && Snapshot.IsSelected == operation.Snapshot.IsSelected
            && Snapshot.PortPoints.SequenceEqual(operation.Snapshot.PortPoints);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as ICustomDrawOperation);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Snapshot.Id);
        hash.Add(Snapshot.Bounds);
        hash.Add(Snapshot.Title, StringComparer.Ordinal);
        hash.Add(Snapshot.Body, StringComparer.Ordinal);
        hash.Add(Snapshot.BorderColor);
        hash.Add(Snapshot.IsSelected);
        foreach (var point in Snapshot.PortPoints)
        {
            hash.Add(point);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        disposed = true;
    }

    private static ImmutablePen CreatePen(Color color, double thickness)
    {
        return new ImmutablePen(
            new ImmutableSolidColorBrush(color),
            thickness,
            new ImmutableDashStyle([], 0),
            PenLineCap.Round,
            PenLineJoin.Round,
            10);
    }
}
