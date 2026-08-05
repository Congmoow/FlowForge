using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.SceneGraph;
using FlowForge.App.Controls;
using System.Collections.Immutable;

namespace FlowForge.App.Canvas;

/// <summary>
/// 连线 retained 绘制操作，缓存贝塞尔几何和描边。
/// </summary>
public sealed class EdgeDrawOperation : ICustomDrawOperation
{
    private readonly ImmutablePen pen;
    private readonly (Point First, Point Second) controlPoints;
    private readonly ImmutableArray<Point> renderPoints;
    private PathGeometry? geometry;
    private bool disposed;

    /// <summary>初始化连线绘制操作。</summary>
    /// <param name="snapshot">不可变绘制快照。</param>
    public EdgeDrawOperation(EdgeDrawSnapshot snapshot)
    {
        Snapshot = snapshot;
        controlPoints = BezierEdgeShape.CalculateControlPoints(snapshot.StartPoint, snapshot.EndPoint);
        Bounds = BezierBounds.GetBounds(
            snapshot.StartPoint,
            snapshot.EndPoint,
            snapshot.StrokeWidth);
        renderPoints = CreateRenderPoints(snapshot.StartPoint, snapshot.EndPoint, controlPoints);
        pen = CanvasPenCache.Get(Color.Parse("#64748B"), snapshot.StrokeWidth);
    }

    /// <summary>不可变连线绘制快照。</summary>
    public EdgeDrawSnapshot Snapshot { get; }

    /// <summary>按需创建并缓存的贝塞尔几何。</summary>
    public PathGeometry Geometry => geometry ??= BezierEdgeShape.CreateGeometry(
        Snapshot.StartPoint,
        Snapshot.EndPoint);

    /// <summary>缓存的连线 world bounds。</summary>
    public Rect Bounds { get; }

    /// <summary>操作是否已经释放。</summary>
    public bool IsDisposed => disposed;

    /// <inheritdoc />
    public bool HitTest(Point p)
    {
        if (disposed)
        {
            return false;
        }

        var controls = controlPoints;
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

        for (var index = 1; index < renderPoints.Length; index++)
        {
            context.DrawLine(pen, renderPoints[index - 1], renderPoints[index]);
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

    private static ImmutableArray<Point> CreateRenderPoints(
        Point startPoint,
        Point endPoint,
        (Point First, Point Second) controls)
    {
        var points = ImmutableArray.CreateBuilder<Point>(25);
        points.Add(startPoint);
        for (var index = 1; index <= 24; index++)
        {
            var t = index / 24d;
            var inverse = 1 - t;
            points.Add(new Point(
                inverse * inverse * inverse * startPoint.X
                    + 3 * inverse * inverse * t * controls.First.X
                    + 3 * inverse * t * t * controls.Second.X
                    + t * t * t * endPoint.X,
                inverse * inverse * inverse * startPoint.Y
                    + 3 * inverse * inverse * t * controls.First.Y
                    + 3 * inverse * t * t * controls.Second.Y
                    + t * t * t * endPoint.Y));
        }

        return points.MoveToImmutable();
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
