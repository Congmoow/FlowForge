using System.Collections;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Internal;

namespace FlowForge.Core.Nodes.Transform;

/// <summary>
/// 表示遍历节点的配置。
/// </summary>
public sealed record ForeachNodeConfig : INodeConfig;

/// <summary>
/// 将输入集合中的项目逐项流式写出。
/// </summary>
public sealed class ForeachNode : INode
{
    private ForeachNodeConfig _config;

    /// <summary>
    /// 初始化使用新节点标识和默认配置的遍历节点。
    /// </summary>
    public ForeachNode()
        : this(Guid.NewGuid(), new ForeachNodeConfig())
    {
    }

    /// <summary>
    /// 初始化使用新节点标识和指定配置的遍历节点。
    /// </summary>
    /// <param name="config">遍历节点配置。</param>
    public ForeachNode(ForeachNodeConfig config)
        : this(Guid.NewGuid(), config)
    {
    }

    /// <summary>
    /// 初始化使用指定节点标识和配置的遍历节点。
    /// </summary>
    /// <param name="id">跨会话保持稳定的节点标识。</param>
    /// <param name="config">遍历节点配置。</param>
    public ForeachNode(Guid id, ForeachNodeConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Id = id;
        _config = config;
        ItemsInput = new NodePort<IEnumerable>("items", "集合");
        ItemOutput = new NodePort<object>("item", "项目");
        Inputs = NodePortList.Create(ItemsInput);
        Outputs = NodePortList.Create(ItemOutput);
    }

    /// <summary>
    /// 获取跨会话保持稳定的节点标识。
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// 获取遍历节点的稳定类型标识。
    /// </summary>
    public string TypeId => "core.transform.foreach";

    /// <summary>
    /// 获取仅包含集合输入端口的不可变集合。
    /// </summary>
    public IReadOnlyList<IPort> Inputs { get; }

    /// <summary>
    /// 获取仅包含项目输出端口的不可变集合。
    /// </summary>
    public IReadOnlyList<IPort> Outputs { get; }

    /// <summary>
    /// 获取待遍历集合的输入端口。
    /// </summary>
    public IPort<IEnumerable> ItemsInput { get; }

    /// <summary>
    /// 获取逐项输出的项目端口。
    /// </summary>
    public IPort<object> ItemOutput { get; }

    /// <summary>
    /// 获取或设置遍历节点配置。
    /// </summary>
    public INodeConfig Config
    {
        get => _config;
        set => _config = value as ForeachNodeConfig
            ?? throw new ArgumentException($"配置必须是 {nameof(ForeachNodeConfig)}。", nameof(value));
    }

    /// <summary>
    /// 异步读取集合并逐项写入输出端口。
    /// </summary>
    /// <param name="ctx">当前节点的执行上下文。</param>
    /// <param name="ct">用于取消输入读取和输出写入的令牌。</param>
    /// <returns>表示节点执行过程的异步结果。</returns>
    public async ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var items = await ctx.ReadAsync(ItemsInput, ct).ConfigureAwait(false);
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            await ctx.WriteAsync(ItemOutput, item, ct).ConfigureAwait(false);
        }
    }
}
