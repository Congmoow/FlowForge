using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using FlowForge.App.Commands;
using FlowForge.App.Diagnostics;
using FlowForge.App.Services;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Execution;
using FlowForge.Core.Graph;
using FlowForge.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace FlowForge.App.ViewModels;

/// <summary>
/// 主窗口的根 ViewModel。
/// </summary>
public sealed class MainWindowViewModel : ReactiveObject, IDisposable
{
    private readonly IWorkflowRunner? workflowRunner;
    private readonly IStage3WorkflowRunner? legacyWorkflowRunner;
    private readonly IWorkflowFileService workflowFileService;
    private readonly NodeRegistry nodeRegistry;
    private readonly CommandHistory commandHistory = new();
    private WorkflowDocument? currentDocument;
    private CancellationTokenSource? runCancellation;
    private bool isDisposed;

    /// <summary>
    /// 初始化主窗口 ViewModel。
    /// </summary>
    public MainWindowViewModel()
        : this(
            new WorkflowRunner(new WorkflowScheduler()),
            new AvaloniaWorkflowFileService(),
            NodeRegistry.CreateDefault())
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
        : this(
            null,
            workflowRunner,
            workflowFileService,
            NodeRegistry.CreateDefault(),
            legacyMode: true)
    {
    }

    /// <summary>
    /// 使用当前画布工作流运行器、文件服务和节点目录初始化主窗口 ViewModel。
    /// </summary>
    /// <param name="workflowRunner">执行当前画布工作流的运行器。</param>
    /// <param name="workflowFileService">工作流文件服务。</param>
    /// <param name="nodeRegistry">统一节点目录。</param>
    /// <param name="secretStore">可选的密钥存储。</param>
    /// <param name="filePickerService">可选的文件选择服务。</param>
    [ActivatorUtilitiesConstructor]
    public MainWindowViewModel(
        IWorkflowRunner workflowRunner,
        IWorkflowFileService workflowFileService,
        NodeRegistry nodeRegistry,
        ISecretStore? secretStore = null,
        IFilePickerService? filePickerService = null)
        : this(
            workflowRunner,
            null,
            workflowFileService,
            nodeRegistry,
            legacyMode: false,
            secretStore,
            filePickerService)
    {
    }

    /// <summary>
    /// 使用当前画布工作流运行器和文件服务初始化主窗口 ViewModel。
    /// </summary>
    /// <param name="workflowRunner">执行当前画布工作流的运行器。</param>
    /// <param name="workflowFileService">工作流文件服务。</param>
    public MainWindowViewModel(IWorkflowRunner workflowRunner, IWorkflowFileService workflowFileService)
        : this(workflowRunner, workflowFileService, NodeRegistry.CreateDefault())
    {
    }

    private MainWindowViewModel(
        IWorkflowRunner? workflowRunner,
        IStage3WorkflowRunner? legacyWorkflowRunner,
        IWorkflowFileService workflowFileService,
        NodeRegistry nodeRegistry,
        bool legacyMode,
        ISecretStore? secretStore = null,
        IFilePickerService? filePickerService = null)
    {
        ArgumentNullException.ThrowIfNull(workflowFileService);
        ArgumentNullException.ThrowIfNull(nodeRegistry);
        if (legacyMode && legacyWorkflowRunner is null)
        {
            throw new ArgumentNullException(nameof(legacyWorkflowRunner));
        }

        if (!legacyMode && workflowRunner is null)
        {
            throw new ArgumentNullException(nameof(workflowRunner));
        }

        this.workflowRunner = workflowRunner;
        this.legacyWorkflowRunner = legacyWorkflowRunner;
        this.workflowFileService = workflowFileService;
        this.nodeRegistry = nodeRegistry;
        Canvas = new CanvasViewModel(commandHistory, nodeRegistry);
        Toolbox = new ToolboxViewModel(nodeRegistry);
        PropertyPanel = new PropertyPanelViewModel(
            Canvas,
            secretStore: secretStore,
            filePickerService: filePickerService);
        if (legacyMode)
        {
            SeedLegacyCanvas();
        }

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
    /// 右侧节点属性面板。
    /// </summary>
    public PropertyPanelViewModel PropertyPanel { get; }

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
        PropertyPanel.Dispose();
        PerfHud.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task RunAsync()
    {
        ResetNodeStates();
        IsRunning = true;
        StatusMessage = legacyWorkflowRunner is null
            ? "正在运行工作流…"
            : "正在运行示例工作流…";
        runCancellation = new CancellationTokenSource();
        var synchronizationContext = SynchronizationContext.Current;
        var progress = new CallbackProgress<NodeExecutionEvent>(executionEvent =>
            DispatchProgress(synchronizationContext, executionEvent));

        try
        {
            if (legacyWorkflowRunner is not null)
            {
                await legacyWorkflowRunner.RunAsync(progress, runCancellation.Token);
            }
            else
            {
                await workflowRunner!.RunAsync(CreateCurrentWorkflow(), progress, runCancellation.Token);
            }

            StatusMessage = legacyWorkflowRunner is null
                ? "工作流运行完成。"
                : "示例工作流运行完成。";
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
            Trace.TraceError("工作流运行失败：{0}", error);
        }
        catch (Exception error)
        {
            ResetRunningNodes(NodeExecutionVisualState.Failed);
            StatusMessage = $"运行失败：{error.Message}";
            Trace.TraceError("工作流运行失败：{0}", error);
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
        try
        {
            var result = await workflowFileService.OpenAsync();
            if (result is null)
            {
                return;
            }

            var candidate = CreateCanvasState(result.Document);
            Canvas.ReplaceContents(candidate.Nodes, candidate.Edges);
            currentDocument = result.Document;
            CurrentFilePath = result.Path;
            StatusMessage = $"已打开 {Path.GetFileName(result.Path)}。";
        }
        catch (Exception error)
        {
            StatusMessage = $"打开失败：{error.Message}";
            Trace.TraceError("工作流打开失败：{0}", error);
        }
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

    /// <summary>
    /// 将当前画布的真实节点和连线构建为 Core 工作流。
    /// </summary>
    /// <returns>当前画布工作流。</returns>
    public Workflow CreateCurrentWorkflow()
    {
        var workflow = new Workflow();
        foreach (var node in Canvas.Nodes)
        {
            workflow.AddNode(node.Node);
        }

        foreach (var edge in Canvas.Edges.Where(edge => edge.Target is not null))
        {
            workflow.AddEdge(new WorkflowEdge(
                edge.Id,
                edge.Source.Node.Id,
                edge.Source.Id,
                edge.Target!.Node.Id,
                edge.Target.Id));
        }

        return workflow;
    }

    private (IReadOnlyList<NodeViewModel> Nodes, IReadOnlyList<EdgeViewModel> Edges) CreateCanvasState(
        WorkflowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var workflow = WorkflowSerializer.CreateWorkflow(document, nodeRegistry);
        var modelsById = workflow.Nodes.ToDictionary(node => node.Id);
        var viewModelsById = new Dictionary<Guid, NodeViewModel>();

        foreach (var nodeDocument in document.Nodes)
        {
            var model = modelsById[nodeDocument.Id];
            var definition = nodeRegistry.GetDefinition(nodeDocument.TypeId);
            viewModelsById.Add(
                nodeDocument.Id,
                new NodeViewModel(model, definition, nodeDocument.Position.X, nodeDocument.Position.Y));
        }

        var edges = new List<EdgeViewModel>(document.Edges.Count);
        foreach (var edge in document.Edges)
        {
            var source = viewModelsById[edge.SourceNodeId].Outputs.Single(port => port.Id == edge.SourcePortId);
            var target = viewModelsById[edge.TargetNodeId].Inputs.Single(port => port.Id == edge.TargetPortId);
            edges.Add(new EdgeViewModel(edge.Id, source, target));
        }

        return (viewModelsById.Values.ToArray(), edges);
    }

    private WorkflowDocument BuildDocument()
    {
        var nodes = Canvas.Nodes.Select(node => new WorkflowNodeDocument(
            node.Id,
            node.TypeId,
            new WorkflowNodePosition(node.Position.X, node.Position.Y),
            JsonSerializer.SerializeToElement(
                node.Config,
                node.Config.GetType(),
                WorkflowJsonSerializerOptions.Default))).ToArray();
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

    private void SeedLegacyCanvas()
    {
        var csvNode = new NodeViewModel(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "core.datasource.csv",
            "CSV 读取",
            96,
            80);
        csvNode.Outputs.Add(new PortViewModel(
            csvNode,
            "rows",
            "rows",
            PortDirection.Output,
            typeof(IEnumerable<Dictionary<string, string>>),
            0));
        Canvas.Nodes.Add(csvNode);

        var consoleNode = new NodeViewModel(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "core.sink.console",
            "控制台输出",
            420,
            120);
        consoleNode.Inputs.Add(new PortViewModel(
            consoleNode,
            "value",
            "value",
            PortDirection.Input,
            typeof(object),
            0));
        Canvas.Nodes.Add(consoleNode);
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
