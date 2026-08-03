using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.SceneGraph;
using FlowForge.App.Controls;

namespace FlowForge.App.Canvas;

/// <summary>
/// 连线 retained 绘制操作，缓存贝塞尔几何和描边。
/// </summary>
public sealed class EdgeDrawOperation : ICustomDrawOperation
{
    private readonly ImmutablePen pen;
    private bool disposed;

    /// <summary>初始化连线绘制操作。</summary>
    /// <param name="snapshot">不可变绘制快照。</param>
    public EdgeDrawOperation(EdgeDrawSnapshot snapshot)
    {
        Snapshot = snapshot;
        Geometry = BezierEdgeShape.CreateGeometry(snapshot.StartPoint, snapshot.EndPoint);
        pen = new ImmutablePen(
            new ImmutableSolidColorBrush(Color.Parse("#64748B")),
            snapshot.StrokeWidth,
            new ImmutableDashStyle([], 0),
            PenLineCap.Round,
            PenLineJoin.Round,
            10);
    }

    /// <summary>不可变连线绘制快照。</summary>
    public EdgeDrawSnapshot Snapshot { get; }

    /// <summary>缓存的贝塞尔几何。</summary>
    public PathGeometry Geometry { get; }

    /// <summary>操作是否已经释放。</summary>
    public bool IsDisposed => disposed;

    /// <inheritdoc />
    public Rect Bounds => BezierBounds.GetBounds(
        Snapshot.StartPoint,
        Snapshot.EndPoint,
        Snapshot.StrokeWidth);

    /// <inheritdoc />
    public bool HitTest(Point p)
    {
        if (disposed)
        {
            return false;
        }

        var controls = BezierEdgeShape.CalculateControlPoints(Snapshot.StartPoint, Snapshot.EndPoint);
        var previous = Snapshot.StartPoint;
        var tolerance = Math.Max(4, Snapshot.StrokeWidth / 2 + 2);
        for (var index = 1; index <= 24; index++)
        {
            var t = index / 24d;
            var inverse = 1 - t;
            var current = new Point(
                inverse * inverse * inverse * Snapshot.StartPoint.X
                    + 3 * inverse * inverse * t * controls.First.X
                    + 3 * inverse * t * t * controls.Second.X
                    + t * t * t * Snapshot.EndPoint.X,
                inverse * inverse * inverse * Snapshot.StartPoint.Y
                    + 3 * inverse * inverse * t * controls.First.Y
                    + 3 * inverse * t * t * controls.Second.Y
                    + t * t * t * Snapshot.EndPoint.Y);
            if (DistanceToSegment(p, previous, current) <= tolerance)
            {
                return true;
            }

            previous = current;
        }

        return false;
    }

    /// <inheritdoc />
    public void Render(ImmediateDrawingContext context)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var controls = BezierEdgeShape.CalculateControlPoints(Snapshot.StartPoint, Snapshot.EndPoint);
        var previous = Snapshot.StartPoint;
        for (var index = 1; index <= 24; index++)
        {
            var t = index / 24d;
            var inverse = 1 - t;
            var current = new Point(
                inverse * inverse * inverse * Snapshot.StartPoint.X
                    + 3 * inverse * inverse * t * controls.First.X
                    + 3 * inverse * t * t * controls.Second.X
                    + t * t * t * Snapshot.EndPoint.X,
                inverse * inverse * inverse * Snapshot.StartPoint.Y
                    + 3 * inverse * inverse * t * controls.First.Y
                    + 3 * inverse * t * t * controls.Second.Y
                    + t * t * t * Snapshot.EndPoint.Y);
            context.DrawLine(pen, previous, current);
            previous = current;
        }
    }

    /// <inheritdoc />
    public bool Equals(ICustomDrawOperation? other)
    {
        return other is EdgeDrawOperation operation
            && Snapshot.Equals(operation.Snapshot);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as ICustomDrawOperation);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return Snapshot.GetHashCode();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        disposed = true;
    }

    private static double DistanceToSegment(Point point, Point start, Point end)
    {
        var segment = end - start;
        var lengthSquared = segment.X * segment.X + segment.Y * segment.Y;
        if (lengthSquared <= double.Epsilon)
        {
            var distance = point - start;
            return Math.Sqrt(distance.X * distance.X + distance.Y * distance.Y);
        }

        var offset = point - start;
        var projection = Math.Clamp(
            (offset.X * segment.X + offset.Y * segment.Y) / lengthSquared,
            0,
            1);
        var closest = start + segment * projection;
        var difference = point - closest;
        return Math.Sqrt(difference.X * difference.X + difference.Y * difference.Y);
    }
}
