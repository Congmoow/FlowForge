using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using FlowForge.App.Commands;
using FlowForge.App.Diagnostics;
using FlowForge.App.Services;
using FlowForge.Core.Execution;
using FlowForge.Core.Serialization;
using System.Text.Json;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace FlowForge.App.ViewModels;

/// <summary>
/// 主窗口的根 ViewModel。
/// </summary>
public sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
    private readonly IStage3WorkflowRunner workflowRunner;
    private readonly IWorkflowFileService workflowFileService;
    private readonly CommandHistory commandHistory = new();
    private WorkflowDocument? currentDocument;
    private CancellationTokenSource? runCancellation;
    private bool isDisposed;

    /// <summary>
    /// 初始化主窗口 ViewModel。
    /// </summary>
    public MainWindowViewModel()
        : this(new Stage3SampleWorkflowRunner(new WorkflowScheduler()), new AvaloniaWorkflowFileService())
    {
    }

    /// <summary>
    /// 使用指定示例工作流运行器初始化主窗口 ViewModel。
    /// </summary>
    /// <param name="workflowRunner">Stage 3 示例工作流运行器。</param>
    public MainWindowViewModel(IStage3WorkflowRunner workflowRunner)
        : this(workflowRunner, new AvaloniaWorkflowFileService())
    {
    }

    /// <summary>
    /// 使用指定运行器和文件服务初始化主窗口 ViewModel。
    /// </summary>
    /// <param name="workflowRunner">Stage 3 示例工作流运行器。</param>
    /// <param name="workflowFileService">工作流文件服务。</param>
    public MainWindowViewModel(IStage3WorkflowRunner workflowRunner, IWorkflowFileService workflowFileService)
    {
        ArgumentNullException.ThrowIfNull(workflowRunner);
        ArgumentNullException.ThrowIfNull(workflowFileService);
        this.workflowRunner = workflowRunner;
        this.workflowFileService = workflowFileService;
        Canvas = new CanvasViewModel(commandHistory);
        Toolbox = new ToolboxViewModel();
        var csvNode = new NodeViewModel(Guid.Parse("11111111-1111-1111-1111-111111111111"), "core.datasource.csv", "CSV 读取", 96, 80);
        csvNode.Outputs.Add(new PortViewModel(
            csvNode,
            "rows",
            "rows",
            PortDirection.Output,
            typeof(IEnumerable<Dictionary<string, string>>),
            0));
        Canvas.Nodes.Add(csvNode);

        var consoleNode = new NodeViewModel(Guid.Parse("22222222-2222-2222-2222-222222222222"), "core.sink.console", "控制台输出", 420, 120);
        consoleNode.Inputs.Add(new PortViewModel(consoleNode, "value", "value", PortDirection.Input, typeof(object), 0));
        Canvas.Nodes.Add(consoleNode);

        var canRun = this.WhenAnyValue(viewModel => viewModel.IsRunning).Select(isRunning => !isRunning);
        var canStop = this.WhenAnyValue(viewModel => viewModel.IsRunning);
        RunCommand = ReactiveCommand.CreateFromTask(RunAsync, canRun);
        StopCommand = ReactiveCommand.Create(Stop, canStop);
        NewCommand = ReactiveCommand.CreateFromTask(NewAsync);
        OpenCommand = ReactiveCommand.CreateFromTask(OpenAsync);
        SaveCommand = ReactiveCommand.CreateFromTask(() => SaveAsync(saveAs: false));
        SaveAsCommand = ReactiveCommand.CreateFromTask(() => SaveAsync(saveAs: true));
        var historyChanges = Observable.FromEventPattern(
                handler => commandHistory.Changed += handler,
                handler => commandHistory.Changed -= handler)
            .Select(_ => Unit.Default)
            .StartWith(Unit.Default);
        UndoCommand = ReactiveCommand.Create(() => { commandHistory.Undo(); }, historyChanges.Select(_ => commandHistory.CanUndo));
        RedoCommand = ReactiveCommand.Create(() => { commandHistory.Redo(); }, historyChanges.Select(_ => commandHistory.CanRedo));
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
    /// 开发模式性能 HUD 状态；普通模式下保持隐藏。
    /// </summary>
    public PerfHudViewModel PerfHud { get; } = new();

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

    /// <summary>获取新建工作流命令。</summary>
    public ReactiveCommand<Unit, Unit> NewCommand { get; }

    /// <summary>获取打开工作流命令。</summary>
    public ReactiveCommand<Unit, Unit> OpenCommand { get; }

    /// <summary>获取保存工作流命令。</summary>
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    /// <summary>获取另存为工作流命令。</summary>
    public ReactiveCommand<Unit, Unit> SaveAsCommand { get; }

    /// <summary>获取撤销最近编辑的命令。</summary>
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }

    /// <summary>获取重做最近撤销编辑的命令。</summary>
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }

    /// <summary>获取当前工作流文件路径。</summary>
    [Reactive]
    public string? CurrentFilePath { get; private set; }

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
        NewCommand.Dispose();
        OpenCommand.Dispose();
        SaveCommand.Dispose();
        SaveAsCommand.Dispose();
        UndoCommand.Dispose();
        RedoCommand.Dispose();
        PerfHud.Dispose();
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

    private Task NewAsync()
    {
        Canvas.Nodes.Clear();
        Canvas.Edges.Clear();
        currentDocument = null;
        CurrentFilePath = null;
        StatusMessage = "已新建工作流。";
        return Task.CompletedTask;
    }

    private async Task OpenAsync()
    {
        var result = await workflowFileService.OpenAsync();
        if (result is null)
        {
            return;
        }

        ApplyDocument(result.Document);
        currentDocument = result.Document;
        CurrentFilePath = result.Path;
        StatusMessage = $"已打开 {Path.GetFileName(result.Path)}。";
    }

    private async Task SaveAsync(bool saveAs)
    {
        var document = BuildDocument();
        var path = await workflowFileService.SaveAsync(document, CurrentFilePath, saveAs);
        if (path is null)
        {
            return;
        }

        currentDocument = document;
        CurrentFilePath = path;
        StatusMessage = $"已保存 {Path.GetFileName(path)}。";
    }

    private void ApplyDocument(WorkflowDocument document)
    {
        Canvas.Nodes.Clear();
        Canvas.Edges.Clear();

        foreach (var nodeDocument in document.Nodes)
        {
            var template = Toolbox.Templates.FirstOrDefault(candidate => candidate.TypeId == nodeDocument.TypeId);
            var node = new NodeViewModel(
                nodeDocument.Id,
                nodeDocument.TypeId,
                template?.Title ?? nodeDocument.TypeId,
                nodeDocument.Position.X,
                nodeDocument.Position.Y);
            if (template is not null)
            {
                foreach (var input in template.Inputs.Select((value, index) => (value, index)))
                {
                    node.Inputs.Add(new PortViewModel(node, input.value.Id, input.value.DisplayName, input.value.Direction, input.value.DataType, input.index));
                }

                foreach (var output in template.Outputs.Select((value, index) => (value, index)))
                {
                    node.Outputs.Add(new PortViewModel(node, output.value.Id, output.value.DisplayName, output.value.Direction, output.value.DataType, output.index));
                }
            }

            Canvas.Nodes.Add(node);
        }

        foreach (var edge in document.Edges)
        {
            var source = Canvas.Nodes.Single(node => node.Id == edge.SourceNodeId).Outputs.Single(port => port.Id == edge.SourcePortId);
            var target = Canvas.Nodes.Single(node => node.Id == edge.TargetNodeId).Inputs.Single(port => port.Id == edge.TargetPortId);
            Canvas.Edges.Add(new EdgeViewModel(edge.Id, source, target));
        }
    }

    private WorkflowDocument BuildDocument()
    {
        var previousConfigs = currentDocument?.Nodes.ToDictionary(node => node.Id, node => node.Config)
            ?? new Dictionary<Guid, JsonElement>();
        var nodes = Canvas.Nodes.Select(node => new WorkflowNodeDocument(
            node.Id,
            node.TypeId,
            new WorkflowNodePosition(node.Position.X, node.Position.Y),
            previousConfigs.TryGetValue(node.Id, out var config) ? config : JsonSerializer.SerializeToElement(new { }))).ToArray();
        var edges = Canvas.Edges.Where(edge => edge.Target is not null).Select(edge => new WorkflowEdgeDocument(
            edge.Id,
            edge.Source.Node.Id,
            edge.Source.Id,
            edge.Target!.Node.Id,
            edge.Target.Id)).ToArray();
        var metadata = currentDocument?.Metadata
            ?? new WorkflowMetadata("未命名工作流", DateTimeOffset.UtcNow, "0.1.0");
        return new WorkflowDocument(metadata, nodes, edges);
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
