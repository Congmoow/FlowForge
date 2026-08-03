using Avalonia;

namespace FlowForge.App.Canvas;

/// <summary>
/// 描述画布 world 坐标与控件 view 坐标之间的二维变换。
/// </summary>
public readonly record struct ViewportTransform
{
    /// <summary>允许的最小缩放比例。</summary>
    public const double MinimumZoom = 0.25;

    /// <summary>允许的最大缩放比例。</summary>
    public const double MaximumZoom = 4;

    /// <summary>初始化默认 1x、零平移的变换。</summary>
    public ViewportTransform()
    {
        Zoom = 1;
        Pan = default;
    }

    /// <summary>初始化视口变换。</summary>
    /// <param name="zoom">缩放比例。</param>
    /// <param name="pan">view 左上角对应的 world 坐标。</param>
    public ViewportTransform(double zoom = 1, Vector pan = default)
    {
        Zoom = ClampZoom(zoom);
        Pan = pan;
    }

    /// <summary>当前缩放比例。</summary>
    public double Zoom { get; }

    /// <summary>view 左上角对应的 world 坐标。</summary>
    public Vector Pan { get; }

    /// <summary>将 world 点转换为 view 点。</summary>
    /// <param name="worldPoint">world 坐标点。</param>
    /// <returns>view 坐标点。</returns>
    public Point WorldToView(Point worldPoint)
    {
        return new Point(
            (worldPoint.X - Pan.X) * Zoom,
            (worldPoint.Y - Pan.Y) * Zoom);
    }

    /// <summary>将 view 点逆变换为 world 点。</summary>
    /// <param name="viewPoint">view 坐标点。</param>
    /// <returns>world 坐标点。</returns>
    public Point ViewToWorld(Point viewPoint)
    {
        return new Point(
            viewPoint.X / Zoom + Pan.X,
            viewPoint.Y / Zoom + Pan.Y);
    }

    /// <summary>将 world 矩形转换为 view 矩形。</summary>
    /// <param name="worldBounds">world 矩形。</param>
    /// <returns>view 矩形。</returns>
    public Rect WorldToView(Rect worldBounds)
    {
        var topLeft = WorldToView(worldBounds.TopLeft);
        var bottomRight = WorldToView(worldBounds.BottomRight);
        return new Rect(topLeft, bottomRight);
    }

    /// <summary>将 view 矩形逆变换为 world 矩形。</summary>
    /// <param name="viewBounds">view 矩形。</param>
    /// <returns>world 矩形。</returns>
    public Rect ViewToWorld(Rect viewBounds)
    {
        var topLeft = ViewToWorld(viewBounds.TopLeft);
        var bottomRight = ViewToWorld(viewBounds.BottomRight);
        return new Rect(topLeft, bottomRight);
    }

    /// <summary>返回限制在支持范围内的缩放变换。</summary>
    /// <param name="zoom">请求的缩放比例。</param>
    /// <returns>保留当前平移的变换。</returns>
    public ViewportTransform WithZoom(double zoom)
    {
        return new ViewportTransform(zoom, Pan);
    }

    /// <summary>
    /// 以 view 中的指定点为中心设置缩放比例。
    /// </summary>
    /// <param name="viewPoint">缩放中心所在的 view 点。</param>
    /// <param name="zoom">目标缩放比例。</param>
    /// <returns>保持中心 world 点不动的新变换。</returns>
    public ViewportTransform ZoomAt(Point viewPoint, double zoom)
    {
        var nextZoom = ClampZoom(zoom);
        var worldPoint = ViewToWorld(viewPoint);
        var nextPan = new Vector(
            worldPoint.X - viewPoint.X / nextZoom,
            worldPoint.Y - viewPoint.Y / nextZoom);
        return new ViewportTransform(nextZoom, nextPan);
    }

    /// <summary>获取供 Avalonia 绘制上下文使用的 world 到 view 矩阵。</summary>
    public Matrix WorldToViewMatrix =>
        new(Zoom, 0, 0, Zoom, -Pan.X * Zoom, -Pan.Y * Zoom);

    private static double ClampZoom(double zoom)
    {
        if (double.IsNaN(zoom) || double.IsInfinity(zoom) || zoom <= 0)
        {
            return 1;
        }

        return Math.Clamp(zoom, MinimumZoom, MaximumZoom);
    }
}
