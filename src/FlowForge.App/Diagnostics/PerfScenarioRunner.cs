using System.Diagnostics;
using System.Text;
using FlowForge.App.Controls;

namespace FlowForge.App.Diagnostics;

/// <summary>
/// 在真实 Avalonia 画布上执行自动平移和性能采样。
/// </summary>
public static class PerfScenarioRunner
{
    /// <summary>
    /// 使用 A5 采样参数运行一次性能场景，保留原有调用契约。
    /// </summary>
    /// <param name="canvas">已挂载到 Avalonia 窗口的画布控件。</param>
    /// <param name="scenario">要绑定的确定性压力场景。</param>
    /// <param name="options">采样参数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含每秒样本和聚合指标的结果。</returns>
    public static Task<PerfRunResult> RunAsync(
        NodeCanvas canvas,
        StressScenario scenario,
        PerfSamplingOptions options,
        CancellationToken ct = default)
    {
        return RunAsync(
            canvas,
            scenario,
            PerfRunOptions.From(options),
            sampleProgress: null,
            ct: ct);
    }

    /// <summary>
    /// 运行一次性能场景。FPS 来自画布实际 Render 调用次数，不使用模拟计数。
    /// </summary>
    /// <param name="canvas">已挂载到 Avalonia 窗口的画布控件。</param>
    /// <param name="scenario">要绑定的确定性压力场景。</param>
    /// <param name="options">HUD 和采样器共享的运行参数。</param>
    /// <param name="sampleProgress">每个正式采样窗口完成时接收一次样本。</param>
    /// <param name="ct">取消令牌。</param>
    /// <param name="windowSize">实际宿主窗口尺寸；未提供时兼容性回退到画布尺寸。</param>
    /// <returns>包含每秒样本和聚合指标的结果。</returns>
    public static async Task<PerfRunResult> RunAsync(
        NodeCanvas canvas,
        StressScenario scenario,
        PerfRunOptions options,
        IProgress<PerfSample>? sampleProgress = null,
        Avalonia.Size? windowSize = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        if (scenario.Nodes.Count != options.NodeCount || scenario.Seed != options.Seed)
        {
            throw new ArgumentException("压力场景与采样参数不一致。", nameof(scenario));
        }

        canvas.Canvas = scenario.Canvas;
        canvas.ResetRenderFrameCount();
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var panState = new PanState();

        await RunPanLoopAsync(
            canvas,
            scenario.WorldBounds,
            options,
            options.Warmup,
            stopwatch,
            panState,
            null,
            ct).ConfigureAwait(true);

        var samples = new List<PerfSample>();
        var lastSampleTimestamp = TimeSpan.Zero;
        var lastFrameCount = canvas.RenderFrameCount;
        await RunPanLoopAsync(
            canvas,
            scenario.WorldBounds,
            options,
            options.Duration,
            stopwatch,
            panState,
            (elapsed, frameCount) =>
            {
                var interval = elapsed - lastSampleTimestamp;
                var renderedFrames = frameCount - lastFrameCount;
                var seconds = Math.Max(interval.TotalSeconds, double.Epsilon);
                var perfSample = new PerfSample(
                    options.NodeCount,
                    options.Seed,
                    DateTimeOffset.UtcNow,
                    interval,
                    renderedFrames,
                    renderedFrames / seconds,
                    Process.GetCurrentProcess().WorkingSet64);
                samples.Add(perfSample);
                sampleProgress?.Report(perfSample);
                lastSampleTimestamp = elapsed;
                lastFrameCount = frameCount;
            },
            ct).ConfigureAwait(true);

        var result = new PerfRunResult(
            options.NodeCount,
            options.Seed,
            startedAt,
            options.Warmup,
            options.Duration,
            samples)
        {
            Metadata = PerfRunMetadata.Capture(
                options.Commit,
                windowSize ?? canvas.Bounds.Size),
        };
        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            await WriteJsonAsync(options.OutputPath, result, ct).ConfigureAwait(false);
        }

        return result;
    }

    private static async Task RunPanLoopAsync(
        NodeCanvas canvas,
        Avalonia.Rect sceneBounds,
        PerfRunOptions options,
        TimeSpan duration,
        Stopwatch stopwatch,
        PanState panState,
        Action<TimeSpan, long>? sample,
        CancellationToken ct)
    {
        var phaseStart = stopwatch.Elapsed;
        var nextSample = options.SampleInterval;
        while (stopwatch.Elapsed - phaseStart < duration)
        {
            ct.ThrowIfCancellationRequested();
            var elapsed = stopwatch.Elapsed;
            var stepSeconds = options.PanInterval.TotalSeconds;
            panState.Direction = PerfPanController.ResolveDirection(
                canvas.Viewport.WorldBounds,
                sceneBounds,
                panState.Direction);
            canvas.Viewport.PanBy(PerfPanController.GetViewDelta(
                panState.Direction,
                options.PanSpeed,
                options.PanInterval));
            await Task.Delay(options.PanInterval, ct).ConfigureAwait(true);

            if (sample is null)
            {
                continue;
            }

            elapsed = stopwatch.Elapsed - phaseStart;
            if (elapsed >= nextSample || elapsed >= duration)
            {
                sample(elapsed, canvas.RenderFrameCount);
                nextSample += options.SampleInterval;
            }
        }
    }

    private sealed class PanState
    {
        public int Direction { get; set; } = 1;
    }

    private static async Task WriteJsonAsync(
        string? outputPath,
        PerfRunResult result,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            fullPath,
            result.ToJson(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            ct).ConfigureAwait(false);
    }
}
