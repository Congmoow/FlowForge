using System.Diagnostics;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Graph;

namespace FlowForge.Core.Execution;

/// <summary>
/// 并发调度工作流节点，并通过 Channel 路由流式数据。
/// </summary>
public sealed class WorkflowScheduler
{
    private readonly Func<Workflow, ChannelEdgeRouter> _routerFactory;

    /// <summary>
    /// 初始化工作流调度器。
    /// </summary>
    public WorkflowScheduler()
    {
        _routerFactory = static workflow => new ChannelEdgeRouter(workflow);
    }

    /// <summary>
    /// 异步执行完整工作流。
    /// </summary>
    /// <param name="workflow">要执行的工作流。</param>
    /// <param name="progress">可选的节点状态接收器。</param>
    /// <param name="cancellationToken">用于停止全部节点的取消令牌。</param>
    /// <returns>表示工作流执行过程的任务。</returns>
    /// <exception cref="CyclicGraphException">工作流包含环。</exception>
    /// <exception cref="WorkflowExecutionException">任一节点执行失败。</exception>
    /// <exception cref="OperationCanceledException">调用方取消工作流。</exception>
    public async Task ExecuteAsync(
        Workflow workflow,
        IProgress<NodeExecutionEvent>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var orderedNodes = TopologicalSorter.Sort(workflow);
        cancellationToken.ThrowIfCancellationRequested();

        if (orderedNodes.Count == 0)
        {
            return;
        }

        var router = _routerFactory(workflow);
        var failureTracker = new FailureTracker();
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var nodeTasks = new Task[orderedNodes.Count];

        for (var index = 0; index < orderedNodes.Count; index++)
        {
            var node = orderedNodes[index];
            nodeTasks[index] = Task.Run(
                () => ExecuteNodeAsync(
                    node,
                    router,
                    progress,
                    failureTracker,
                    linkedCancellation),
                CancellationToken.None);
        }

        try
        {
            await Task.WhenAll(nodeTasks).ConfigureAwait(false);
        }
        catch
        {
            var failure = failureTracker.Failure;
            if (failure is not null)
            {
                throw new WorkflowExecutionException(failure.NodeId, failure.Error);
            }

            cancellationToken.ThrowIfCancellationRequested();
            linkedCancellation.Token.ThrowIfCancellationRequested();
            throw;
        }
    }

    private static async Task ExecuteNodeAsync(
        INode node,
        ChannelEdgeRouter router,
        IProgress<NodeExecutionEvent>? progress,
        FailureTracker failureTracker,
        CancellationTokenSource linkedCancellation)
    {
        Report(progress, new NodeExecutionEvent(node.Id, NodeExecutionStatus.Running));

        try
        {
            var context = router.CreateExecutionContext(node.Id);
            await node.ExecuteAsync(context, linkedCancellation.Token).ConfigureAwait(false);
            router.CompleteOutputs(node.Id);
            Report(progress, new NodeExecutionEvent(node.Id, NodeExecutionStatus.Succeeded));
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            router.CompleteOutputs(node.Id, new OperationCanceledException(linkedCancellation.Token));
            throw;
        }
        catch (Exception error)
        {
            if (failureTracker.TrySet(node.Id, error))
            {
                Report(progress, new NodeExecutionEvent(node.Id, NodeExecutionStatus.Failed, error));
                router.CompleteAll(error);
                await linkedCancellation.CancelAsync().ConfigureAwait(false);
                throw;
            }

            throw new OperationCanceledException(linkedCancellation.Token);
        }
    }

    private static void Report(IProgress<NodeExecutionEvent>? progress, NodeExecutionEvent executionEvent)
    {
        if (progress is null)
        {
            return;
        }

        try
        {
            progress.Report(executionEvent);
        }
        catch (Exception error)
        {
            Trace.TraceError("节点执行进度接收器失败：{0}", error);
        }
    }

    private sealed record FailureInfo(Guid NodeId, Exception Error);

    private sealed class FailureTracker
    {
        private FailureInfo? _failure;

        public FailureInfo? Failure => Volatile.Read(ref _failure);

        public bool TrySet(Guid nodeId, Exception error)
        {
            var failure = new FailureInfo(nodeId, error);
            return Interlocked.CompareExchange(ref _failure, failure, null) is null;
        }
    }
}
