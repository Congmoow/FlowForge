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
    /// 运行一次性能场景。FPS 来自画布实际 Render 调用次数，不使用模拟计数。
    /// </summary>
    /// <param name="canvas">已挂载到 Avalonia 窗口的画布控件。</param>
    /// <param name="scenario">要绑定的确定性压力场景。</param>
    /// <param name="options">采样参数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>包含每秒样本和聚合指标的结果。</returns>
    public static async Task<PerfRunResult> RunAsync(
        NodeCanvas canvas,
        StressScenario scenario,
        PerfSamplingOptions options,
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

        await RunPanLoopAsync(canvas, options, options.Warmup, stopwatch, null, ct).ConfigureAwait(true);

        var samples = new List<PerfSample>();
        var lastSampleTimestamp = TimeSpan.Zero;
        var lastFrameCount = canvas.RenderFrameCount;
        await RunPanLoopAsync(
            canvas,
            options,
            options.Duration,
            stopwatch,
            (elapsed, frameCount) =>
            {
                var interval = elapsed - lastSampleTimestamp;
                if (interval < options.SampleInterval && elapsed < options.Duration)
                {
                    return;
                }

                var renderedFrames = frameCount - lastFrameCount;
                var seconds = Math.Max(interval.TotalSeconds, double.Epsilon);
                samples.Add(new PerfSample(
                    options.NodeCount,
                    options.Seed,
                    DateTimeOffset.UtcNow,
                    interval,
                    renderedFrames,
                    renderedFrames / seconds,
                    Process.GetCurrentProcess().WorkingSet64));
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
            samples);
        if (!string.IsNullOrWhiteSpace(options.OutputPath))
        {
            await WriteJsonAsync(options.OutputPath, result, ct).ConfigureAwait(false);
        }

        return result;
    }

    private static async Task RunPanLoopAsync(
        NodeCanvas canvas,
        PerfSamplingOptions options,
        TimeSpan duration,
        Stopwatch stopwatch,
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
            canvas.Viewport.PanBy(new Avalonia.Vector(options.PanSpeed * stepSeconds, 0));
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
