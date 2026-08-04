using System.Globalization;

namespace FlowForge.App.Diagnostics;

/// <summary>
/// 描述开发模式性能采样的固定参数。
/// </summary>
public sealed record PerfSamplingOptions
{
    /// <summary>默认的可重复场景种子。</summary>
    public const int DefaultSeed = 20260803;

    /// <summary>要生成的节点数量。</summary>
    public int NodeCount { get; init; } = 1000;

    /// <summary>生成器使用的确定性种子。</summary>
    public int Seed { get; init; } = DefaultSeed;

    /// <summary>被测代码提交标识；为空时读取 FLOWFORGE_PERF_COMMIT。</summary>
    public string? Commit { get; init; }

    /// <summary>预热时长；预热样本不写入结果。</summary>
    public TimeSpan Warmup { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>正式采样时长。</summary>
    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>样本采样间隔。</summary>
    public TimeSpan SampleInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>自动平移循环间隔；8ms 为 Windows UI 合成器保留 60fps 采样余量。</summary>
    public TimeSpan PanInterval { get; init; } = TimeSpan.FromMilliseconds(8);

    /// <summary>自动平移速度，单位为 view 像素/秒。</summary>
    public double PanSpeed { get; init; } = 120;

    /// <summary>性能 JSON 输出路径；为空时不写文件。</summary>
    public string? OutputPath { get; init; }

    /// <summary>真实窗口渲染截图输出路径；为空时不保存截图。</summary>
    public string? ScreenshotPath { get; init; }

    /// <summary>
    /// 验证采样参数。
    /// </summary>
    public void Validate()
    {
        if (!StressScenarioGenerator.SupportedNodeCounts.Contains(NodeCount))
        {
            throw new ArgumentOutOfRangeException(nameof(NodeCount), "节点数量必须是 100、250、500 或 1000。");
        }

        if (Warmup < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Warmup), "预热时长不能为负数。");
        }

        if (Duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Duration), "采样时长必须为正数。");
        }

        if (SampleInterval <= TimeSpan.Zero || SampleInterval > Duration)
        {
            throw new ArgumentOutOfRangeException(nameof(SampleInterval), "采样间隔必须为正数且不大于采样时长。");
        }

        if (PanInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(PanInterval), "平移间隔必须为正数。");
        }

        if (double.IsNaN(PanSpeed) || double.IsInfinity(PanSpeed) || PanSpeed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PanSpeed), "平移速度必须是非负有限数。");
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            return;
        }

        if (Path.IsPathRooted(OutputPath) && string.IsNullOrWhiteSpace(Path.GetDirectoryName(OutputPath)))
        {
            throw new ArgumentException("性能输出路径无效。", nameof(OutputPath));
        }
    }

    /// <summary>
    /// 从命令行解析性能模式参数。
    /// </summary>
    /// <param name="args">应用程序命令行参数。</param>
    /// <param name="options">解析后的采样参数。</param>
    /// <returns>包含 <c>--perf</c> 时返回 true。</returns>
    public static bool TryParse(IReadOnlyList<string> args, out PerfSamplingOptions options)
    {
        ArgumentNullException.ThrowIfNull(args);

        var perf = false;
        var nodeCount = 1000;
        var seed = DefaultSeed;
        string? commit = null;
        string? outputPath = null;
        string? screenshotPath = null;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--perf", StringComparison.OrdinalIgnoreCase))
            {
                perf = true;
                continue;
            }

            if (TryReadOption(argument, "--nodes", args, ref index, out var nodesValue))
            {
                nodeCount = ParseInt(nodesValue, "nodes");
                continue;
            }

            if (TryReadOption(argument, "--seed", args, ref index, out var seedValue))
            {
                seed = ParseInt(seedValue, "seed");
                continue;
            }

            if (TryReadOption(argument, "--commit", args, ref index, out var commitValue))
            {
                commit = commitValue;
                continue;
            }

            if (TryReadOption(argument, "--output", args, ref index, out var pathValue))
            {
                outputPath = pathValue;
                continue;
            }

            if (TryReadOption(argument, "--screenshot", args, ref index, out var screenshotValue))
            {
                screenshotPath = screenshotValue;
            }
        }

        options = new PerfSamplingOptions
        {
            NodeCount = nodeCount,
            Seed = seed,
            Commit = commit,
            OutputPath = outputPath,
            ScreenshotPath = screenshotPath,
        };

        if (perf)
        {
            options.Validate();
        }

        return perf;
    }

    private static bool TryReadOption(
        string argument,
        string optionName,
        IReadOnlyList<string> args,
        ref int index,
        out string value)
    {
        if (argument.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase))
        {
            value = argument[(optionName.Length + 1)..];
            return true;
        }

        if (!string.Equals(argument, optionName, StringComparison.OrdinalIgnoreCase))
        {
            value = string.Empty;
            return false;
        }

        if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
        {
            throw new FormatException($"命令行参数 {optionName} 缺少值。");
        }

        value = args[index];
        return true;
    }

    private static int ParseInt(string value, string optionName)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw new FormatException($"命令行参数 --{optionName} 不是有效整数：{value}。");
        }

        return result;
    }
}
