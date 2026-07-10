namespace FlowForge.Core.Execution;

/// <summary>
/// 表示工作流因单个节点失败而终止。
/// </summary>
public sealed class WorkflowExecutionException : InvalidOperationException
{
    /// <summary>
    /// 使用失败节点和根异常初始化工作流执行异常。
    /// </summary>
    /// <param name="nodeId">失败节点标识。</param>
    /// <param name="innerException">节点抛出的根异常。</param>
    public WorkflowExecutionException(Guid nodeId, Exception innerException)
        : base($"节点 {nodeId} 执行失败。", innerException)
    {
        NodeId = nodeId;
    }

    /// <summary>
    /// 获取导致工作流失败的节点标识。
    /// </summary>
    public Guid NodeId { get; }
}
