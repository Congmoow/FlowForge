namespace FlowForge.Core.Graph;

/// <summary>
/// 表示工作流中连接两个节点端口的有向边。
/// </summary>
/// <param name="Id">边的稳定标识。</param>
/// <param name="SourceNodeId">源节点标识。</param>
/// <param name="SourcePortId">源输出端口标识。</param>
/// <param name="TargetNodeId">目标节点标识。</param>
/// <param name="TargetPortId">目标输入端口标识。</param>
public sealed record WorkflowEdge(
    Guid Id,
    Guid SourceNodeId,
    string SourcePortId,
    Guid TargetNodeId,
    string TargetPortId);
