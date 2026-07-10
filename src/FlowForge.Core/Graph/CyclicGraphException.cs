namespace FlowForge.Core.Graph;

/// <summary>
/// 表示工作流包含环，因而无法完成拓扑排序。
/// </summary>
public sealed class CyclicGraphException : InvalidOperationException
{
    /// <summary>
    /// 使用尚未完成排序的节点标识初始化异常。
    /// </summary>
    /// <param name="remainingNodeIds">拓扑排序终止时尚未完成排序的节点标识。</param>
    /// <exception cref="ArgumentNullException"><paramref name="remainingNodeIds"/> 为 <see langword="null"/>。</exception>
    public CyclicGraphException(IEnumerable<Guid> remainingNodeIds)
        : base("工作流包含环，无法完成拓扑排序。")
    {
        ArgumentNullException.ThrowIfNull(remainingNodeIds);
        RemainingNodeIds = Array.AsReadOnly(remainingNodeIds.ToArray());
    }

    /// <summary>
    /// 获取拓扑排序终止时尚未完成排序的节点标识。
    /// </summary>
    public IReadOnlyList<Guid> RemainingNodeIds { get; }
}
