using FlowForge.Core.Abstractions;

namespace FlowForge.Plugins.Sample;

/// <summary>
/// 提供示例节点的显式插件目录。
/// </summary>
public sealed class SamplePlugin : INodePlugin
{
    /// <summary>
    /// 获取示例插件提供的节点定义。
    /// </summary>
    public IReadOnlyList<NodeDefinition> Definitions { get; } =
    [
        NodeDefinition.Create<UppercaseNodeConfig>(
            "core.transform.uppercase",
            "转大写",
            static (id, config) => new UppercaseNode(id, config),
            static () => new UppercaseNodeConfig(),
            inputs: [new PortDefinition("text", "文本", typeof(string))],
            outputs: [new PortDefinition("result", "大写文本", typeof(string))]),
    ];
}

/// <summary>
/// 转大写节点的不可变配置。
/// </summary>
public sealed record UppercaseNodeConfig : INodeConfig;

/// <summary>
/// 将输入文本转换为不变文化大写文本的示例节点。
/// </summary>
public sealed class UppercaseNode : INode
{
    private UppercaseNodeConfig config;

    /// <summary>
    /// 使用新的节点标识和默认配置创建示例节点。
    /// </summary>
    public UppercaseNode()
        : this(Guid.NewGuid(), new UppercaseNodeConfig())
    {
    }

    /// <summary>
    /// 使用指定节点标识和配置创建示例节点。
    /// </summary>
    /// <param name="id">跨会话保持稳定的节点标识。</param>
    /// <param name="config">节点配置。</param>
    public UppercaseNode(Guid id, UppercaseNodeConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Id = id;
        this.config = config;
        TextInput = PortFactory.Create<string>("text", "文本");
        ResultOutput = PortFactory.Create<string>("result", "大写文本");
        Inputs = Array.AsReadOnly<IPort>([TextInput]);
        Outputs = Array.AsReadOnly<IPort>([ResultOutput]);
    }

    /// <summary>
    /// 获取节点稳定标识。
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// 获取节点类型标识。
    /// </summary>
    public string TypeId => "core.transform.uppercase";

    /// <summary>
    /// 获取文本输入端口集合。
    /// </summary>
    public IReadOnlyList<IPort> Inputs { get; }

    /// <summary>
    /// 获取大写文本输出端口集合。
    /// </summary>
    public IReadOnlyList<IPort> Outputs { get; }

    /// <summary>
    /// 获取文本输入端口。
    /// </summary>
    public IPort<string> TextInput { get; }

    /// <summary>
    /// 获取大写文本输出端口。
    /// </summary>
    public IPort<string> ResultOutput { get; }

    /// <summary>
    /// 获取或设置节点配置。
    /// </summary>
    public INodeConfig Config
    {
        get => config;
        set => config = value as UppercaseNodeConfig
            ?? throw new ArgumentException($"配置必须是 {nameof(UppercaseNodeConfig)}。", nameof(value));
    }

    /// <summary>
    /// 读取输入文本并写入不变文化大写结果。
    /// </summary>
    /// <param name="ctx">当前节点的执行上下文。</param>
    /// <param name="ct">用于协作式取消的令牌。</param>
    /// <returns>表示节点执行过程的异步结果。</returns>
    public async ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var text = await ctx.ReadAsync(TextInput, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        await ctx.WriteAsync(ResultOutput, (text ?? string.Empty).ToUpperInvariant(), ct)
            .ConfigureAwait(false);
    }
}
