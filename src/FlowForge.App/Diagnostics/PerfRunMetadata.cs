using System.Runtime.InteropServices;
using Avalonia;

namespace FlowForge.App.Diagnostics;

/// <summary>
/// 记录性能 JSON 的来源、窗口和运行环境，避免测量结果只能依赖文件名解释。
/// </summary>
/// <param name="Commit">被测代码提交标识。</param>
/// <param name="WindowWidth">实际宿主窗口宽度，单位为 logical px。</param>
/// <param name="WindowHeight">实际宿主窗口高度，单位为 logical px。</param>
/// <param name="Cpu">测量机器的处理器描述。</param>
/// <param name="Gpu">测量机器的图形处理器描述。</param>
/// <param name="OperatingSystem">操作系统描述。</param>
/// <param name="DotnetSdk">生成或运行测量时使用的 .NET SDK 版本。</param>
/// <param name="DotnetRuntime">运行测量时使用的 .NET runtime 描述。</param>
/// <param name="RuntimeIdentifier">测量运行时的 RID。</param>
/// <param name="AvaloniaVersion">测量运行时的 Avalonia 程序集版本。</param>
public sealed record PerfRunMetadata(
    string Commit,
    double WindowWidth,
    double WindowHeight,
    string Cpu,
    string Gpu,
    string OperatingSystem,
    string DotnetSdk,
    string DotnetRuntime,
    string RuntimeIdentifier,
    string AvaloniaVersion)
{
    /// <summary>
    /// 从当前进程和性能运行参数采集可追溯元数据。
    /// </summary>
    /// <param name="commit">命令行或调用方提供的提交标识。</param>
    /// <param name="windowSize">实际宿主窗口尺寸。</param>
    /// <returns>不可变的运行元数据。</returns>
    public static PerfRunMetadata Capture(string? commit, Size windowSize)
    {
        return new(
            FirstNonEmpty(commit, Environment.GetEnvironmentVariable("FLOWFORGE_PERF_COMMIT")),
            NormalizeDimension(windowSize.Width),
            NormalizeDimension(windowSize.Height),
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("FLOWFORGE_PERF_CPU"),
                "unknown"),
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("FLOWFORGE_PERF_GPU"),
                "unknown"),
            RuntimeInformation.OSDescription,
            FirstNonEmpty(
                Environment.GetEnvironmentVariable("DOTNET_SDK_VERSION"),
                "unknown"),
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.RuntimeIdentifier,
            typeof(Application).Assembly.GetName().Version?.ToString() ?? "unknown");
    }

    private static string FirstNonEmpty(string? value, string? fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.IsNullOrWhiteSpace(fallback) ? "unknown" : fallback
            : value;
    }

    private static double NormalizeDimension(double value)
    {
        return double.IsFinite(value) && value >= 0 ? value : 0;
    }
}
