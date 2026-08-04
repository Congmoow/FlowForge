namespace FlowForge.App.Diagnostics;

/// <summary>
/// 描述一次开发模式性能运行及 HUD 展示所需的固定参数。
/// </summary>
public sealed record PerfRunOptions
{
    /// <summary>要生成的节点数量。</summary>
    public int NodeCount { get; init; } = 1000;

    /// <summary>生成器使用的确定性种子。</summary>
    public int Seed { get; init; } = PerfSamplingOptions.DefaultSeed;

    /// <summary>预热时长；预热样本不写入结果。</summary>
    public TimeSpan Warmup { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>正式采样时长。</summary>
    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>样本采样间隔。</summary>
    public TimeSpan SampleInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>自动平移循环间隔。</summary>
    public TimeSpan PanInterval { get; init; } = TimeSpan.FromMilliseconds(16);

    /// <summary>自动平移速度，单位为 view 像素/秒。</summary>
    public double PanSpeed { get; init; } = 120;

    /// <summary>性能 JSON 输出路径；为空时不写文件。</summary>
    public string? OutputPath { get; init; }

    /// <summary>是否显示性能 HUD。</summary>
    public bool ShowHud { get; init; } = true;

    /// <summary>
    /// 从兼容的性能采样参数创建一次运行配置。
    /// </summary>
    /// <param name="options">已有性能采样参数。</param>
    /// <returns>供 HUD 和采样器共享的运行配置。</returns>
    public static PerfRunOptions From(PerfSamplingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return new PerfRunOptions
        {
            NodeCount = options.NodeCount,
            Seed = options.Seed,
            Warmup = options.Warmup,
            Duration = options.Duration,
            SampleInterval = options.SampleInterval,
            PanInterval = options.PanInterval,
            PanSpeed = options.PanSpeed,
            OutputPath = options.OutputPath,
        };
    }

    /// <summary>
    /// 转换回 A5 保留的采样参数类型。
    /// </summary>
    /// <returns>可供旧 API 使用的性能采样参数。</returns>
    public PerfSamplingOptions ToSamplingOptions()
    {
        return new PerfSamplingOptions
        {
            NodeCount = NodeCount,
            Seed = Seed,
            Warmup = Warmup,
            Duration = Duration,
            SampleInterval = SampleInterval,
            PanInterval = PanInterval,
            PanSpeed = PanSpeed,
            OutputPath = OutputPath,
        };
    }

    /// <summary>
    /// 验证当前运行配置。
    /// </summary>
    public void Validate()
    {
        ToSamplingOptions().Validate();
    }
}
