using System.Collections.Concurrent;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace FlowForge.App.Canvas;

/// <summary>
/// 缓存画布中重复使用的 immutable 描边资源。
/// </summary>
internal static class CanvasPenCache
{
    private static readonly ImmutableDashStyle SolidDashStyle = new([], 0);
    private static readonly ConcurrentDictionary<PenKey, ImmutablePen> Pens = new();

    /// <summary>
    /// 获取指定颜色和线宽的共享描边。
    /// </summary>
    /// <param name="color">描边颜色。</param>
    /// <param name="thickness">描边线宽。</param>
    /// <returns>可安全共享的 immutable 描边。</returns>
    public static ImmutablePen Get(Color color, double thickness)
    {
        return Pens.GetOrAdd(
            new PenKey(color, thickness),
            static key => new ImmutablePen(
                new ImmutableSolidColorBrush(key.Color),
                key.Thickness,
                SolidDashStyle,
                PenLineCap.Round,
                PenLineJoin.Round,
                10));
    }

    private readonly record struct PenKey(Color Color, double Thickness);
}
