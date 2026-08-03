using Avalonia;
using FlowForge.App.Controls;

namespace FlowForge.App.Canvas;

/// <summary>
/// 计算三次贝塞尔连线的保守 world 包围盒。
/// </summary>
public static class BezierBounds
{
    /// <summary>
    /// 返回包含起点、终点和两个控制点并按描边膨胀的矩形。
    /// </summary>
    /// <param name="startPoint">贝塞尔起点。</param>
    /// <param name="endPoint">贝塞尔终点。</param>
    /// <param name="strokeWidth">描边宽度。</param>
    /// <returns>包含整条连线和描边的 world bounds。</returns>
    public static Rect GetBounds(Point startPoint, Point endPoint, double strokeWidth)
    {
        var controls = BezierEdgeShape.CalculateControlPoints(startPoint, endPoint);
        var minimumX = Math.Min(Math.Min(startPoint.X, endPoint.X), Math.Min(controls.First.X, controls.Second.X));
        var maximumX = Math.Max(Math.Max(startPoint.X, endPoint.X), Math.Max(controls.First.X, controls.Second.X));
        var minimumY = Math.Min(Math.Min(startPoint.Y, endPoint.Y), Math.Min(controls.First.Y, controls.Second.Y));
        var maximumY = Math.Max(Math.Max(startPoint.Y, endPoint.Y), Math.Max(controls.First.Y, controls.Second.Y));
        var margin = Math.Max(0, strokeWidth) / 2;

        return new Rect(
            minimumX - margin,
            minimumY - margin,
            maximumX - minimumX + margin * 2,
            maximumY - minimumY + margin * 2);
    }
}
