using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Internal;

namespace FlowForge.Core.Nodes.Sink;

/// <summary>
/// 表示控制台输出节点的配置。
/// </summary>
public sealed record ConsoleSinkNodeConfig : INodeConfig;

/// <summary>
/// 将输入值异步写入文本输出器并追加换行。
/// </summary>
public sealed class ConsoleSinkNode : INode
{
    private readonly TextWriter _writer;
    private ConsoleSinkNodeConfig _config;

    /// <summary>
    /// 初始化使用新节点标识、默认配置和标准输出器的控制台输出节点。
    /// </summary>
    public ConsoleSinkNode()
        : this(Guid.NewGuid(), new ConsoleSinkNodeConfig(), Console.Out)
    {
    }

    /// <summary>
    /// 初始化使用新节点标识、默认配置和指定输出器的控制台输出节点。
    /// </summary>
    /// <param name="writer">接收节点输出的文本输出器。</param>
    public ConsoleSinkNode(TextWriter writer)
        : this(Guid.NewGuid(), new ConsoleSinkNodeConfig(), writer)
    {
    }

    /// <summary>
    /// 初始化使用新节点标识、指定配置和标准输出器的控制台输出节点。
    /// </summary>
    /// <param name="config">控制台输出配置。</param>
    public ConsoleSinkNode(ConsoleSinkNodeConfig config)
        : this(Guid.NewGuid(), config, Console.Out)
    {
    }

    /// <summary>
    /// 初始化使用指定节点标识、配置和标准输出器的控制台输出节点。
    /// </summary>
    /// <param name="id">跨会话保持稳定的节点标识。</param>
    /// <param name="config">控制台输出配置。</param>
    public ConsoleSinkNode(Guid id, ConsoleSinkNodeConfig config)
        : this(id, config, Console.Out)
    {
    }

    /// <summary>
    /// 初始化使用指定节点标识、配置和输出器的控制台输出节点。
    /// </summary>
    /// <param name="id">跨会话保持稳定的节点标识。</param>
    /// <param name="config">控制台输出配置。</param>
    /// <param name="writer">接收节点输出的文本输出器。</param>
    public ConsoleSinkNode(Guid id, ConsoleSinkNodeConfig config, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(writer);

        Id = id;
        _config = config;
        _writer = writer;
        ValueInput = new NodePort<object>("value", "值");
        Inputs = NodePortList.Create(ValueInput);
        Outputs = NodePortList.Empty;
    }

    /// <summary>
    /// 获取跨会话保持稳定的节点标识。
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// 获取控制台输出节点的稳定类型标识。
    /// </summary>
    public string TypeId => "core.sink.console";

    /// <summary>
    /// 获取仅包含值输入端口的不可变集合。
    /// </summary>
    public IReadOnlyList<IPort> Inputs { get; }

    /// <summary>
    /// 获取空的输出端口集合。
    /// </summary>
    public IReadOnlyList<IPort> Outputs { get; }

    /// <summary>
    /// 获取待输出值的输入端口。
    /// </summary>
    public IPort<object> ValueInput { get; }

    /// <summary>
    /// 获取或设置控制台输出配置。
    /// </summary>
    public INodeConfig Config
    {
        get => _config;
        set => _config = value as ConsoleSinkNodeConfig
            ?? throw new ArgumentException($"配置必须是 {nameof(ConsoleSinkNodeConfig)}。", nameof(value));
    }

    /// <summary>
    /// 异步读取输入值并将其写为一行文本。
    /// </summary>
    /// <param name="ctx">当前节点的执行上下文。</param>
    /// <param name="ct">用于取消输入读取和文本写入的令牌。</param>
    /// <returns>表示节点执行过程的异步结果。</returns>
    public async ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var value = await ctx.ReadAsync(ValueInput, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        var text = value?.ToString() ?? string.Empty;
        await _writer.WriteLineAsync(text.AsMemory(), ct).ConfigureAwait(false);
    }
}
