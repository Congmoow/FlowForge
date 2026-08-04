using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace FlowForge.App.Diagnostics;

/// <summary>
/// 从真实 Avalonia 窗口视觉树导出性能场景截图。
/// </summary>
public static class PerfScreenshotExporter
{
    /// <summary>
    /// 将当前窗口渲染到 PNG 文件，不依赖桌面截图或屏幕遮挡状态。
    /// </summary>
    /// <param name="window">已显示且包含性能画布的窗口。</param>
    /// <param name="outputPath">PNG 输出路径。</param>
    public static void Save(Window window, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var size = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(window.Bounds.Width)),
            Math.Max(1, (int)Math.Ceiling(window.Bounds.Height)));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        bitmap.Render(window);
        bitmap.Save(fullPath);
    }
}
