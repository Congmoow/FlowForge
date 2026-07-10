using FlowForge.Core.Execution;

namespace FlowForge.App.Services;

/// <summary>
/// 执行 Stage 3 预置示例工作流。
/// </summary>
public interface IStage3WorkflowRunner
{
    /// <summary>
    /// 异步执行预置工作流并上报节点状态。
    /// </summary>
    /// <param name="progress">节点执行状态接收器。</param>
    /// <param name="cancellationToken">用于停止工作流的取消令牌。</param>
    /// <returns>表示工作流执行过程的任务。</returns>
    Task RunAsync(IProgress<NodeExecutionEvent> progress, CancellationToken cancellationToken);
}
