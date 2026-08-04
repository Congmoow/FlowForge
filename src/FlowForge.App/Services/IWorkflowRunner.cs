using FlowForge.Core.Execution;
using FlowForge.Core.Graph;

namespace FlowForge.App.Services;

/// <summary>
/// 执行调用方提供的当前工作流。
/// </summary>
public interface IWorkflowRunner
{
    /// <summary>
    /// 异步执行指定工作流并上报节点状态。
    /// </summary>
    /// <param name="workflow">当前画布构建出的工作流。</param>
    /// <param name="progress">节点执行状态接收器。</param>
    /// <param name="cancellationToken">用于停止工作流的取消令牌。</param>
    /// <returns>表示工作流执行过程的任务。</returns>
    Task RunAsync(
        Workflow workflow,
        IProgress<NodeExecutionEvent> progress,
        CancellationToken cancellationToken);
}
