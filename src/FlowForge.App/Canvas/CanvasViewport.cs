using Avalonia;

namespace FlowForge.App.Canvas;

/// <summary>
/// 保存画布视口状态并提供导航操作。
/// </summary>
public sealed class CanvasViewport
{
    private Size viewSize;

    /// <summary>初始化默认 1x、零平移的视口。</summary>
    public CanvasViewport()
    {
        Transform = new ViewportTransform();
    }

    /// <summary>当前 world/view 变换。</summary>
    public ViewportTransform Transform { get; private set; }

    /// <summary>视口控件的 view 尺寸。</summary>
    public Size ViewSize
    {
        get => viewSize;
        set
        {
            if (viewSize == value)
            {
                return;
            }

            viewSize = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>当前视口可见的 world 矩形。</summary>
    public Rect WorldBounds => Transform.ViewToWorld(new Rect(ViewSize));

    /// <summary>视口状态变化事件。</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// 按 view 像素平移视口。
    /// </summary>
    /// <param name="viewDelta">指针在 view 坐标中的位移。</param>
    public void PanBy(Vector viewDelta)
    {
        if (viewDelta == default)
        {
            return;
        }

        Transform = new ViewportTransform(
            Transform.Zoom,
            Transform.Pan - viewDelta / Transform.Zoom);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>以鼠标位置为中心设置目标缩放比例。</summary>
    /// <param name="viewPoint">缩放中心的 view 坐标。</param>
    /// <param name="zoom">目标缩放比例。</param>
    public void ZoomAt(Point viewPoint, double zoom)
    {
        var next = Transform.ZoomAt(viewPoint, zoom);
        if (next == Transform)
        {
            return;
        }

        Transform = next;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>将视口恢复到默认变换。</summary>
    public void Reset()
    {
        Transform = new ViewportTransform();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 根据缩放比例返回保持约 24 view 像素间距的 world 网格步长。
    /// </summary>
    /// <param name="zoom">缩放比例。</param>
    /// <returns>world 坐标中的网格步长。</returns>
    public static double GridStepForZoom(double zoom)
    {
        var clampedZoom = new ViewportTransform(zoom).Zoom;
        return 24 / clampedZoom;
    }
}
