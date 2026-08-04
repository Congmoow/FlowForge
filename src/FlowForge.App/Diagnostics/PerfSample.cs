using System.Text.Json;

namespace FlowForge.App.Diagnostics;

/// <summary>
/// 表示一个实际渲染采样窗口的性能数据。
/// </summary>
/// <param name="NodeCount">场景节点数量。</param>
/// <param name="Seed">场景确定性种子。</param>
/// <param name="Timestamp">采样窗口结束时间。</param>
/// <param name="Interval">采样窗口时长。</param>
/// <param name="RenderedFrames">窗口内实际调用画布 Render 的次数。</param>
/// <param name="FramesPerSecond">按窗口时长折算的实际 FPS。</param>
/// <param name="WorkingSetBytes">采样时进程 WorkingSet64。</param>
public sealed record PerfSample(
    int NodeCount,
    int Seed,
    DateTimeOffset Timestamp,
    TimeSpan Interval,
    long RenderedFrames,
    double FramesPerSecond,
    long WorkingSetBytes);

/// <summary>
/// 表示一次完整性能采样运行及其聚合指标。
/// </summary>
public sealed record PerfRunResult(
    int NodeCount,
    int Seed,
    DateTimeOffset StartedAt,
    TimeSpan Warmup,
    TimeSpan Duration,
    IReadOnlyList<PerfSample> Samples)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>获取性能数据的来源和运行环境。</summary>
    public PerfRunMetadata Metadata { get; init; } =
        PerfRunMetadata.Capture(commit: null, windowSize: default);

    /// <summary>所有采样窗口 FPS 的算术平均值。</summary>
    public double AverageFramesPerSecond => Samples.Count == 0
        ? 0
        : Samples.Average(sample => sample.FramesPerSecond);

    /// <summary>所有采样窗口中的最高 WorkingSet64。</summary>
    public long PeakWorkingSetBytes => Samples.Count == 0
        ? 0
        : Samples.Max(sample => sample.WorkingSetBytes);

    /// <summary>
    /// 序列化为供无人值守采样使用的 JSON 文档。
    /// </summary>
    /// <returns>格式化的 UTF-8 兼容 JSON 文本。</returns>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonOptions);
    }
}
