namespace FlowForge.Core.Execution;

/// <summary>
/// 表示节点执行过程中可观察的状态。
/// </summary>
public enum NodeExecutionStatus
{
    /// <summary>节点正在执行。</summary>
    Running,

    /// <summary>节点执行成功。</summary>
    Succeeded,

    /// <summary>节点执行失败。</summary>
    Failed,
}

/// <summary>
/// 描述单个节点的一次执行状态变化。
/// </summary>
/// <param name="NodeId">节点标识。</param>
/// <param name="Status">节点执行状态。</param>
/// <param name="Error">失败原因；非失败状态时为 <see langword="null"/>。</param>
public sealed record NodeExecutionEvent(
    Guid NodeId,
    NodeExecutionStatus Status,
    Exception? Error = null);
