using Avalonia;

namespace FlowForge.App.Diagnostics;

/// <summary>
/// 计算性能场景的有界水平自动平移，确保采样期间仍有场景内容可见。
/// </summary>
public static class PerfPanController
{
    /// <summary>
    /// 根据当前视口和场景边界决定是否反向平移。
    /// </summary>
    /// <param name="viewportBounds">当前视口的 world 边界。</param>
    /// <param name="sceneBounds">压力场景的 world 边界。</param>
    /// <param name="direction">当前方向，1 表示向右，-1 表示向左，0 表示停止。</param>
    /// <returns>下一步方向。</returns>
    public static int ResolveDirection(Rect viewportBounds, Rect sceneBounds, int direction)
    {
        if (direction is < -1 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        if (sceneBounds.Width <= viewportBounds.Width)
        {
            return 0;
        }

        if (direction == 0)
        {
            return 1;
        }

        if (direction > 0 && viewportBounds.Right >= sceneBounds.Right)
        {
            return -1;
        }

        if (direction < 0 && viewportBounds.Left <= sceneBounds.Left)
        {
            return 1;
        }

        return direction;
    }

    /// <summary>
    /// 将 world 中的移动方向转换为 <see cref="Canvas.CanvasViewport.PanBy"/> 所需的 view 位移。
    /// </summary>
    /// <param name="direction">当前方向，1 表示向右，-1 表示向左，0 表示停止。</param>
    /// <param name="speed">view 像素每秒的速度。</param>
    /// <param name="interval">本次平移的时间步长。</param>
    /// <returns>传给视口平移方法的 view 位移。</returns>
    public static Vector GetViewDelta(int direction, double speed, TimeSpan interval)
    {
        if (direction is < -1 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        if (double.IsNaN(speed) || double.IsInfinity(speed) || speed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speed));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(interval, TimeSpan.Zero);

        return new Vector(-direction * speed * interval.TotalSeconds, 0);
    }
}
