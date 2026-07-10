using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Internal;

namespace FlowForge.Core.Nodes.Transform;

/// <summary>
/// 表示文本拼接节点的配置。
/// </summary>
/// <param name="Separator">相邻文本片段之间使用的分隔符。</param>
public sealed record TextConcatNodeConfig(string Separator = "") : INodeConfig;

/// <summary>
/// 使用配置的分隔符拼接文本片段数组。
/// </summary>
public sealed class TextConcatNode : INode
{
    private TextConcatNodeConfig _config;

    /// <summary>
    /// 初始化使用新节点标识和默认配置的文本拼接节点。
    /// </summary>
    public TextConcatNode()
        : this(Guid.NewGuid(), new TextConcatNodeConfig())
    {
    }

    /// <summary>
    /// 初始化使用新节点标识和指定配置的文本拼接节点。
    /// </summary>
    /// <param name="config">文本拼接配置。</param>
    public TextConcatNode(TextConcatNodeConfig config)
        : this(Guid.NewGuid(), config)
    {
    }

    /// <summary>
    /// 初始化使用指定节点标识和配置的文本拼接节点。
    /// </summary>
    /// <param name="id">跨会话保持稳定的节点标识。</param>
    /// <param name="config">文本拼接配置。</param>
    public TextConcatNode(Guid id, TextConcatNodeConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Id = id;
        _config = config;
        PartsInput = new NodePort<string[]>("parts", "文本片段");
        ResultOutput = new NodePort<string>("result", "结果");
        Inputs = NodePortList.Create(PartsInput);
        Outputs = NodePortList.Create(ResultOutput);
    }

    /// <summary>
    /// 获取跨会话保持稳定的节点标识。
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// 获取文本拼接节点的稳定类型标识。
    /// </summary>
    public string TypeId => "core.transform.text-concat";

    /// <summary>
    /// 获取仅包含文本片段输入端口的不可变集合。
    /// </summary>
    public IReadOnlyList<IPort> Inputs { get; }

    /// <summary>
    /// 获取仅包含拼接结果输出端口的不可变集合。
    /// </summary>
    public IReadOnlyList<IPort> Outputs { get; }

    /// <summary>
    /// 获取文本片段数组输入端口。
    /// </summary>
    public IPort<string[]> PartsInput { get; }

    /// <summary>
    /// 获取拼接结果输出端口。
    /// </summary>
    public IPort<string> ResultOutput { get; }

    /// <summary>
    /// 获取或设置文本拼接配置。
    /// </summary>
    public INodeConfig Config
    {
        get => _config;
        set => _config = value as TextConcatNodeConfig
            ?? throw new ArgumentException($"配置必须是 {nameof(TextConcatNodeConfig)}。", nameof(value));
    }

    /// <summary>
    /// 异步读取文本片段，完成拼接并写入结果端口。
    /// </summary>
    /// <param name="ctx">当前节点的执行上下文。</param>
    /// <param name="ct">用于取消输入读取和输出写入的令牌。</param>
    /// <returns>表示节点执行过程的异步结果。</returns>
    public async ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var parts = await ctx.ReadAsync(PartsInput, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        var result = string.Join(_config.Separator, parts ?? Array.Empty<string>());
        await ctx.WriteAsync(ResultOutput, result, ct).ConfigureAwait(false);
    }
}
