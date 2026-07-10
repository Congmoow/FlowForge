using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using FlowForge.App.Services;
using FlowForge.Core.Execution;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace FlowForge.App.ViewModels;

/// <summary>
/// 主窗口的根 ViewModel。
/// </summary>
public sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
    private readonly IStage3WorkflowRunner workflowRunner;
    private CancellationTokenSource? runCancellation;
    private bool isDisposed;

    /// <summary>
    /// 初始化主窗口 ViewModel。
    /// </summary>
    public MainWindowViewModel()
        : this(new Stage3SampleWorkflowRunner(new WorkflowScheduler()))
    {
    }

    /// <summary>
    /// 使用指定示例工作流运行器初始化主窗口 ViewModel。
    /// </summary>
    /// <param name="workflowRunner">Stage 3 示例工作流运行器。</param>
    public MainWindowViewModel(IStage3WorkflowRunner workflowRunner)
    {
        ArgumentNullException.ThrowIfNull(workflowRunner);
        this.workflowRunner = workflowRunner;
        Canvas = new CanvasViewModel();
        Toolbox = new ToolboxViewModel();
        var csvNode = new NodeViewModel(Guid.Parse("11111111-1111-1111-1111-111111111111"), "core.datasource.csv", "CSV 读取", 96, 80);
        csvNode.Outputs.Add(new PortViewModel(csvNode, "rows", "rows", PortDirection.Output, typeof(object), 0));
        Canvas.Nodes.Add(csvNode);

        var consoleNode = new NodeViewModel(Guid.Parse("22222222-2222-2222-2222-222222222222"), "core.sink.console", "控制台输出", 420, 120);
        consoleNode.Inputs.Add(new PortViewModel(consoleNode, "value", "value", PortDirection.Input, typeof(object), 0));
        Canvas.Nodes.Add(consoleNode);

        var canRun = this.WhenAnyValue(viewModel => viewModel.IsRunning).Select(isRunning => !isRunning);
        var canStop = this.WhenAnyValue(viewModel => viewModel.IsRunning);
        RunCommand = ReactiveCommand.CreateFromTask(RunAsync, canRun);
        StopCommand = ReactiveCommand.Create(Stop, canStop);
    }

    /// <summary>
    /// 应用显示名称。
    /// </summary>
    public string ApplicationName { get; } = "FlowForge";

    /// <summary>
    /// 左侧节点库标题。
    /// </summary>
    public string ToolboxTitle { get; } = "节点库";

    /// <summary>
    /// 中央画布标题。
    /// </summary>
    public string CanvasTitle { get; } = "工作流画布";

    /// <summary>
    /// 右侧属性面板标题。
    /// </summary>
    public string PropertyPanelTitle { get; } = "属性面板";

    /// <summary>
    /// 中央工作流画布。
    /// </summary>
    public CanvasViewModel Canvas { get; }

    /// <summary>
    /// 左侧节点库。
    /// </summary>
    public ToolboxViewModel Toolbox { get; }

    /// <summary>
    /// 获取异步运行预置工作流的命令。
    /// </summary>
    public ReactiveCommand<Unit, Unit> RunCommand { get; }

    /// <summary>
    /// 获取停止当前工作流的命令。
    /// </summary>
    public ReactiveCommand<Unit, Unit> StopCommand { get; }

    /// <summary>
    /// 当前是否正在执行工作流。
    /// </summary>
    [Reactive]
    public bool IsRunning { get; private set; }

    /// <summary>
    /// 当前工作流运行状态说明。
    /// </summary>
    [Reactive]
    public string StatusMessage { get; private set; } = "就绪。";

    /// <summary>
    /// 取消进行中的工作流并释放命令资源。
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        runCancellation?.Cancel();
        runCancellation?.Dispose();
        RunCommand.Dispose();
        StopCommand.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task RunAsync()
    {
        ResetNodeStates();
        IsRunning = true;
        StatusMessage = "正在运行示例工作流…";
        runCancellation = new CancellationTokenSource();
        var synchronizationContext = SynchronizationContext.Current;
        var progress = new CallbackProgress<NodeExecutionEvent>(executionEvent =>
            DispatchProgress(synchronizationContext, executionEvent));

        try
        {
            await workflowRunner.RunAsync(progress, runCancellation.Token);
            StatusMessage = "示例工作流运行完成。";
        }
        catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
        {
            ResetRunningNodes();
            StatusMessage = "工作流运行已取消。";
        }
        catch (WorkflowExecutionException error)
        {
            ApplyNodeEvent(new NodeExecutionEvent(error.NodeId, NodeExecutionStatus.Failed, error.InnerException));
            StatusMessage = $"运行失败：{error.InnerException?.Message ?? error.Message}";
            Trace.TraceError("Stage 3 示例工作流运行失败：{0}", error);
        }
        catch (Exception error)
        {
            ResetRunningNodes(NodeExecutionVisualState.Failed);
            StatusMessage = $"运行失败：{error.Message}";
            Trace.TraceError("Stage 3 示例工作流运行失败：{0}", error);
        }
        finally
        {
            runCancellation.Dispose();
            runCancellation = null;
            IsRunning = false;
        }
    }

    private void Stop()
    {
        runCancellation?.Cancel();
    }

    private void DispatchProgress(SynchronizationContext? synchronizationContext, NodeExecutionEvent executionEvent)
    {
        if (synchronizationContext is null || ReferenceEquals(SynchronizationContext.Current, synchronizationContext))
        {
            ApplyNodeEvent(executionEvent);
            return;
        }

        synchronizationContext.Post(static state =>
        {
            var payload = (ProgressPayload)state!;
            payload.ViewModel.ApplyNodeEvent(payload.ExecutionEvent);
        }, new ProgressPayload(this, executionEvent));
    }

    private void ApplyNodeEvent(NodeExecutionEvent executionEvent)
    {
        var node = Canvas.Nodes.FirstOrDefault(candidate => candidate.Id == executionEvent.NodeId);
        if (node is null)
        {
            return;
        }

        node.ExecutionState = executionEvent.Status switch
        {
            NodeExecutionStatus.Running => NodeExecutionVisualState.Running,
            NodeExecutionStatus.Succeeded => NodeExecutionVisualState.Success,
            NodeExecutionStatus.Failed => NodeExecutionVisualState.Failed,
            _ => NodeExecutionVisualState.Idle,
        };
    }

    private void ResetNodeStates()
    {
        foreach (var node in Canvas.Nodes)
        {
            node.ExecutionState = NodeExecutionVisualState.Idle;
        }
    }

    private void ResetRunningNodes(NodeExecutionVisualState state = NodeExecutionVisualState.Idle)
    {
        foreach (var node in Canvas.Nodes.Where(node => node.ExecutionState == NodeExecutionVisualState.Running))
        {
            node.ExecutionState = state;
        }
    }

    private sealed record ProgressPayload(MainWindowViewModel ViewModel, NodeExecutionEvent ExecutionEvent);

    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value)
        {
            callback(value);
        }
    }
}
