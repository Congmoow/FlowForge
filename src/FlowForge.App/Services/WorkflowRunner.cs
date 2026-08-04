using FlowForge.Core.Execution;
using FlowForge.Core.Graph;

namespace FlowForge.App.Services;

/// <summary>
/// 使用 Core 调度器执行当前画布工作流。
/// </summary>
public sealed class WorkflowRunner : IWorkflowRunner
{
    private readonly WorkflowScheduler scheduler;

    /// <summary>
    /// 使用指定调度器初始化工作流运行器。
    /// </summary>
    /// <param name="scheduler">负责执行节点的调度器。</param>
    public WorkflowRunner(WorkflowScheduler scheduler)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        this.scheduler = scheduler;
    }

    /// <inheritdoc />
    public Task RunAsync(
        Workflow workflow,
        IProgress<NodeExecutionEvent> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(progress);
        return scheduler.ExecuteAsync(workflow, progress, cancellationToken);
    }
}
