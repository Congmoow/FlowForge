namespace FlowForge.Core.Abstractions;

/// <summary>
/// 表示工作流中可执行的节点。
/// </summary>
public interface INode
{
    /// <summary>
    /// 获取跨会话保持稳定的节点标识。
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// 获取用于定位节点实现类型的稳定标识。
    /// </summary>
    string TypeId { get; }

    /// <summary>
    /// 获取节点声明的只读输入端口集合。
    /// </summary>
    IReadOnlyList<IPort> Inputs { get; }

    /// <summary>
    /// 获取节点声明的只读输出端口集合。
    /// </summary>
    IReadOnlyList<IPort> Outputs { get; }

    /// <summary>
    /// 获取或设置节点的可序列化配置。
    /// </summary>
    INodeConfig Config { get; set; }

    /// <summary>
    /// 异步执行节点，并通过执行上下文读取输入和写入输出。
    /// </summary>
    /// <param name="ctx">当前节点的执行上下文。</param>
    /// <param name="ct">用于协作式取消执行的令牌。</param>
    /// <returns>表示节点执行过程的异步结果。</returns>
    ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct);
}
