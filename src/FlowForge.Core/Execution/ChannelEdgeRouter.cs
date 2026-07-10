using System.Runtime.CompilerServices;
using System.Threading.Channels;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Graph;

namespace FlowForge.Core.Execution;

/// <summary>
/// 使用每条边一个 Channel 的方式路由工作流数据。
/// </summary>
public sealed class ChannelEdgeRouter
{
    private readonly Dictionary<Guid, INode> _nodes;
    private readonly IReadOnlyList<WorkflowEdge> _edges;
    private readonly IReadOnlyDictionary<Guid, Channel<object?>> _channels;

    /// <summary>
    /// 根据工作流当前节点和边创建独立路由快照。
    /// </summary>
    /// <param name="workflow">要路由的工作流。</param>
    public ChannelEdgeRouter(Workflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        _nodes = workflow.Nodes.ToDictionary(node => node.Id);
        _edges = workflow.Edges.ToArray();
        _channels = _edges.ToDictionary(
            edge => edge.Id,
            _ => Channel.CreateUnbounded<object?>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = true,
            }));
    }

    /// <summary>
    /// 为指定节点创建受端口范围约束的执行上下文。
    /// </summary>
    /// <param name="nodeId">节点标识。</param>
    /// <returns>节点执行上下文。</returns>
    public IExecutionContext CreateExecutionContext(Guid nodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out var node))
        {
            throw new WorkflowReferenceException($"执行节点 {nodeId} 不存在。");
        }

        return new NodeExecutionContext(this, node);
    }

    /// <summary>
    /// 完成指定节点的全部输出边，可选择向下游传播失败原因。
    /// </summary>
    /// <param name="nodeId">节点标识。</param>
    /// <param name="error">要传播的失败原因；正常完成时为 <see langword="null"/>。</param>
    public void CompleteOutputs(Guid nodeId, Exception? error = null)
    {
        EnsureNodeExists(nodeId);

        foreach (var edge in _edges.Where(edge => edge.SourceNodeId == nodeId))
        {
            _channels[edge.Id].Writer.TryComplete(error);
        }
    }

    /// <summary>
    /// 完成路由快照中的全部边，可选择向所有等待者传播失败原因。
    /// </summary>
    /// <param name="error">要传播的失败原因；正常完成时为 <see langword="null"/>。</param>
    public void CompleteAll(Exception? error = null)
    {
        foreach (var channel in _channels.Values)
        {
            channel.Writer.TryComplete(error);
        }
    }

    private void EnsureNodeExists(Guid nodeId)
    {
        if (!_nodes.ContainsKey(nodeId))
        {
            throw new WorkflowReferenceException($"执行节点 {nodeId} 不存在。");
        }
    }

    private sealed class NodeExecutionContext(ChannelEdgeRouter router, INode node) : IExecutionContext
    {
        public async ValueTask<T?> ReadAsync<T>(IPort<T> port, CancellationToken ct)
        {
            EnsureOwnedInput(port);
            var edge = router._edges.SingleOrDefault(edge =>
                edge.TargetNodeId == node.Id
                && string.Equals(edge.TargetPortId, port.Id, StringComparison.Ordinal));

            if (edge is null)
            {
                throw new UnconnectedInputPortException(node.Id, port.Id);
            }

            var value = await router._channels[edge.Id].Reader.ReadAsync(ct).ConfigureAwait(false);
            return ConvertValue(value, port);
        }

        public async IAsyncEnumerable<T?> ReadAllAsync<T>(
            IPort<T> port,
            [EnumeratorCancellation] CancellationToken ct)
        {
            EnsureOwnedInput(port);
            var edge = router._edges.SingleOrDefault(edge =>
                edge.TargetNodeId == node.Id
                && string.Equals(edge.TargetPortId, port.Id, StringComparison.Ordinal));

            if (edge is null)
            {
                throw new UnconnectedInputPortException(node.Id, port.Id);
            }

            await foreach (var value in router._channels[edge.Id].Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return ConvertValue(value, port);
            }
        }

        public async ValueTask WriteAsync<T>(IPort<T> port, T? value, CancellationToken ct)
        {
            EnsureOwnedOutput(port);
            var outgoingEdges = router._edges.Where(edge =>
                edge.SourceNodeId == node.Id
                && string.Equals(edge.SourcePortId, port.Id, StringComparison.Ordinal));

            foreach (var edge in outgoingEdges)
            {
                await router._channels[edge.Id].Writer.WriteAsync(value, ct).ConfigureAwait(false);
            }
        }

        private void EnsureOwnedInput<T>(IPort<T> port)
        {
            ArgumentNullException.ThrowIfNull(port);
            if (!node.Inputs.Any(input => ReferenceEquals(input, port)))
            {
                throw new ExecutionPortException($"端口 {port.Id} 不是节点 {node.Id} 声明的输入端口。");
            }
        }

        private void EnsureOwnedOutput<T>(IPort<T> port)
        {
            ArgumentNullException.ThrowIfNull(port);
            if (!node.Outputs.Any(output => ReferenceEquals(output, port)))
            {
                throw new ExecutionPortException($"端口 {port.Id} 不是节点 {node.Id} 声明的输出端口。");
            }
        }

        private T? ConvertValue<T>(object? value, IPort<T> port)
        {
            if (value is null)
            {
                return default;
            }

            if (value is T typedValue)
            {
                return typedValue;
            }

            throw new PortValueTypeMismatchException(node.Id, port.Id, typeof(T), value.GetType());
        }
    }
}
