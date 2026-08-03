using Avalonia;
using FlowForge.App.ViewModels;

namespace FlowForge.App.Canvas;

/// <summary>
/// 提供画布节点视口剔除所需的纯几何函数。
/// </summary>
public static class CanvasCulling
{
    /// <summary>画布节点的标准尺寸。</summary>
    public static readonly Size DefaultNodeSize = new(220, 96);

    /// <summary>根据节点左上角计算 world bounds。</summary>
    /// <param name="position">节点 world 坐标。</param>
    /// <returns>节点 world bounds。</returns>
    public static Rect NodeBounds(Point position)
    {
        return NodeBounds(position, DefaultNodeSize);
    }

    /// <summary>根据节点左上角和尺寸计算 world bounds。</summary>
    /// <param name="position">节点 world 坐标。</param>
    /// <param name="size">节点 world 尺寸。</param>
    /// <returns>节点 world bounds。</returns>
    public static Rect NodeBounds(Point position, Size size)
    {
        return new Rect(position, size);
    }

    /// <summary>
    /// 按原顺序返回与视口相交的节点，边界相切也视为可见。
    /// </summary>
    /// <param name="nodes">待剔除的节点集合。</param>
    /// <param name="viewport">world 视口矩形。</param>
    /// <returns>可见节点的只读列表。</returns>
    public static IReadOnlyList<NodeViewModel> CullNodes(
        IEnumerable<NodeViewModel> nodes,
        Rect viewport)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        return nodes
            .Where(node => IntersectsIncludingBoundary(NodeBounds(node.Position), viewport))
            .ToArray();
    }

    /// <summary>
    /// 按贝塞尔 world bounds 返回与视口相交的连线，边界相切也视为可见。
    /// </summary>
    /// <param name="edges">待剔除的连线集合。</param>
    /// <param name="viewport">world 视口矩形。</param>
    /// <param name="strokeWidth">连线描边宽度。</param>
    /// <returns>可见连线的只读列表。</returns>
    public static IReadOnlyList<EdgeViewModel> CullEdges(
        IEnumerable<EdgeViewModel> edges,
        Rect viewport,
        double strokeWidth)
    {
        ArgumentNullException.ThrowIfNull(edges);

        return edges
            .Where(edge => IntersectsIncludingBoundary(
                BezierBounds.GetBounds(edge.StartPoint, edge.EndPoint, strokeWidth),
                viewport))
            .ToArray();
    }

    /// <summary>判断两个矩形是否相交或边界相切。</summary>
    /// <param name="first">第一个矩形。</param>
    /// <param name="second">第二个矩形。</param>
    /// <returns>相交或相切时返回 true。</returns>
    public static bool IntersectsIncludingBoundary(Rect first, Rect second)
    {
        return first.Right >= second.Left
            && first.Left <= second.Right
            && first.Bottom >= second.Top
            && first.Top <= second.Bottom;
    }
}
