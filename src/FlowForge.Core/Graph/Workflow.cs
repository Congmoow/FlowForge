using System.Collections;
using FlowForge.Core.Abstractions;

namespace FlowForge.Core.Graph;

/// <summary>
/// 表示由节点和有向边组成的可编辑工作流聚合。
/// </summary>
public sealed class Workflow
{
    private readonly List<INode> _nodes = [];
    private readonly List<WorkflowEdge> _edges = [];
    private readonly IReadOnlyList<INode> _nodeView;
    private readonly IReadOnlyList<WorkflowEdge> _edgeView;

    /// <summary>
    /// 初始化一个空工作流。
    /// </summary>
    public Workflow()
    {
        _nodeView = new ReadOnlyListView<INode>(_nodes);
        _edgeView = new ReadOnlyListView<WorkflowEdge>(_edges);
    }

    /// <summary>
    /// 获取工作流中的只读节点集合。
    /// </summary>
    public IReadOnlyList<INode> Nodes => _nodeView;

    /// <summary>
    /// 获取工作流中的只读边集合。
    /// </summary>
    public IReadOnlyList<WorkflowEdge> Edges => _edgeView;

    /// <summary>
    /// 向工作流添加节点。
    /// </summary>
    /// <param name="node">要添加的节点。</param>
    /// <exception cref="DuplicateWorkflowElementException">节点标识已存在。</exception>
    public void AddNode(INode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (_nodes.Any(existing => existing.Id == node.Id))
        {
            throw new DuplicateWorkflowElementException($"节点标识 {node.Id} 已存在。");
        }

        _nodes.Add(node);
    }

    /// <summary>
    /// 从工作流删除节点以及与其关联的所有边。
    /// </summary>
    /// <param name="nodeId">要删除的节点标识。</param>
    /// <returns>找到并删除节点时为 <see langword="true"/>，否则为 <see langword="false"/>。</returns>
    public bool RemoveNode(Guid nodeId)
    {
        var nodeIndex = _nodes.FindIndex(node => node.Id == nodeId);
        if (nodeIndex < 0)
        {
            return false;
        }

        _nodes.RemoveAt(nodeIndex);
        _edges.RemoveAll(edge => edge.SourceNodeId == nodeId || edge.TargetNodeId == nodeId);
        return true;
    }

    /// <summary>
    /// 校验并向工作流添加一条边。
    /// </summary>
    /// <param name="edge">要添加的边。</param>
    public void AddEdge(WorkflowEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);

        if (_edges.Any(existing => existing.Id == edge.Id))
        {
            throw new DuplicateWorkflowElementException($"边标识 {edge.Id} 已存在。");
        }

        var sourceNode = FindNode(edge.SourceNodeId, "源");
        var targetNode = FindNode(edge.TargetNodeId, "目标");
        var sourcePort = FindSourcePort(sourceNode, edge.SourcePortId);
        var targetPort = FindTargetPort(targetNode, edge.TargetPortId);

        if (!targetPort.DataType.IsAssignableFrom(sourcePort.DataType))
        {
            throw new PortTypeMismatchException(
                $"端口类型不兼容：{sourcePort.DataType} 无法赋值给 {targetPort.DataType}。");
        }

        if (_edges.Any(existing =>
                existing.TargetNodeId == edge.TargetNodeId
                && string.Equals(existing.TargetPortId, edge.TargetPortId, StringComparison.Ordinal)))
        {
            throw new InputPortAlreadyConnectedException(
                $"输入端口 {edge.TargetNodeId}/{edge.TargetPortId} 已存在连接。");
        }

        _edges.Add(edge);
    }

    /// <summary>
    /// 从工作流删除指定边。
    /// </summary>
    /// <param name="edgeId">要删除的边标识。</param>
    /// <returns>找到并删除边时为 <see langword="true"/>，否则为 <see langword="false"/>。</returns>
    public bool RemoveEdge(Guid edgeId)
    {
        var edgeIndex = _edges.FindIndex(edge => edge.Id == edgeId);
        if (edgeIndex < 0)
        {
            return false;
        }

        _edges.RemoveAt(edgeIndex);
        return true;
    }

    private INode FindNode(Guid nodeId, string role)
    {
        return _nodes.FirstOrDefault(node => node.Id == nodeId)
            ?? throw new WorkflowReferenceException($"{role}节点 {nodeId} 不存在。");
    }

    private static IPort FindSourcePort(INode node, string portId)
    {
        var output = FindPort(node.Outputs, portId);
        if (output is not null)
        {
            return output;
        }

        if (FindPort(node.Inputs, portId) is not null)
        {
            throw new PortDirectionException($"端口 {node.Id}/{portId} 是输入端口，不能作为边的源端口。");
        }

        throw new WorkflowReferenceException($"源端口 {node.Id}/{portId} 不存在。");
    }

    private static IPort FindTargetPort(INode node, string portId)
    {
        var input = FindPort(node.Inputs, portId);
        if (input is not null)
        {
            return input;
        }

        if (FindPort(node.Outputs, portId) is not null)
        {
            throw new PortDirectionException($"端口 {node.Id}/{portId} 是输出端口，不能作为边的目标端口。");
        }

        throw new WorkflowReferenceException($"目标端口 {node.Id}/{portId} 不存在。");
    }

    private static IPort? FindPort(IReadOnlyList<IPort> ports, string portId)
    {
        return ports.FirstOrDefault(port => string.Equals(port.Id, portId, StringComparison.Ordinal));
    }

    private sealed class ReadOnlyListView<T>(IReadOnlyList<T> source) : IReadOnlyList<T>
    {
        public T this[int index] => source[index];

        public int Count => source.Count;

        public IEnumerator<T> GetEnumerator()
        {
            return source.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
