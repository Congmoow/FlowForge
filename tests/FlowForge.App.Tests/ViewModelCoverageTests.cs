using System.Reactive.Threading.Tasks;
using Avalonia;
using FlowForge.App.Services;
using FlowForge.App.ViewModels;
using FlowForge.Core.Execution;
using FlowForge.Core.Graph;
using FlowForge.Core.Nodes.DataSource;
using FlowForge.Core.Serialization;
using FluentAssertions;
using CoreWorkflow = FlowForge.Core.Graph.Workflow;

namespace FlowForge.App.Tests;

public sealed class ViewModelCoverageTests
{
    [Fact]
    public void Canvas_CommitNodeMove_MultipleNodes_CreatesCompositeHistoryEntry()
    {
        var first = new NodeViewModel(Guid.NewGuid(), "first", "第一", 10, 20);
        var second = new NodeViewModel(Guid.NewGuid(), "second", "第二", 30, 40);
        var canvas = new CanvasViewModel();
        canvas.Nodes.Add(first);
        canvas.Nodes.Add(second);
        var original = new Dictionary<NodeViewModel, Point>
        {
            [first] = first.Position,
            [second] = second.Position,
        };

        first.Position = new Point(100, 200);
        second.Position = new Point(300, 400);
        canvas.CommitNodeMove(original);

        canvas.CommandHistory.Undo().Should().BeTrue();
        first.Position.Should().Be(new Point(10, 20));
        second.Position.Should().Be(new Point(30, 40));
        canvas.CommandHistory.Redo().Should().BeTrue();
        first.Position.Should().Be(new Point(100, 200));
        second.Position.Should().Be(new Point(300, 400));
    }

    [Fact]
    public void Canvas_UnknownSelectionAndInputDrag_DoNothing()
    {
        var node = new NodeViewModel(Guid.NewGuid(), "node", "节点", 0, 0);
        var input = new PortViewModel(node, "input", "输入", PortDirection.Input, typeof(string), 0);
        node.Inputs.Add(input);
        var canvas = new CanvasViewModel();
        canvas.Nodes.Add(node);

        canvas.SelectNodeCommand.Execute(new SelectNodeRequest(Guid.NewGuid(), SelectionGesture.Replace));
        canvas.BeginEdgeDragCommand.Execute(new BeginEdgeDragRequest(input, new Point(10, 20)));
        canvas.UpdateEdgeDragCommand.Execute(new Point(30, 40));

        canvas.SelectedNode.Should().BeNull();
        canvas.DraftEdge.Should().BeNull();
    }

    [Fact]
    public void Canvas_UnknownTemplate_CreatesCompatibilityNodeWithDeclaredPorts()
    {
        var template = new NodeTemplateViewModel(
            "plugin.custom",
            "自定义节点",
            [new PortTemplateViewModel("input", "输入", PortDirection.Input, typeof(string))],
            [new PortTemplateViewModel("output", "输出", PortDirection.Output, typeof(int))]);
        var canvas = new CanvasViewModel();

        canvas.AddNodeFromTemplateCommand.Execute(new AddNodeFromTemplateRequest(template, new Point(12, 34)));

        var node = canvas.Nodes.Should().ContainSingle().Subject;
        node.Title.Should().Be("自定义节点");
        node.Inputs.Should().ContainSingle().Which.DataType.Should().Be<string>();
        node.Outputs.Should().ContainSingle().Which.DataType.Should().Be<int>();
        template.Title.Should().Be("自定义节点");
        template.Inputs.Should().ContainSingle();
        template.Outputs.Should().ContainSingle();
    }

    [Fact]
    public void Canvas_PreviewWithoutDraft_ClearsPreviewState()
    {
        var node = new NodeViewModel(Guid.NewGuid(), "node", "节点", 0, 0);
        var output = new PortViewModel(node, "output", "输出", PortDirection.Output, typeof(string), 0);
        node.Outputs.Add(output);
        var canvas = new CanvasViewModel();

        canvas.PreviewEdgeTargetCommand.Execute(output);
        canvas.PreviewEdgeTargetCommand.Execute(null);

        canvas.ConnectionPreviewState.Should().Be(ConnectionPreviewState.None);
        canvas.PreviewTargetPort.Should().BeNull();
    }

    [Fact]
    public async Task CompatibilityNode_ExecuteAsync_CompletesAndExposesNoDefinition()
    {
        var viewModel = new NodeViewModel(Guid.NewGuid(), "legacy", "兼容节点", 0, 0);

        viewModel.Definition.Should().BeNull();
        await viewModel.Node.ExecuteAsync(null!, CancellationToken.None);
    }

    [Fact]
    public void Toolbox_EmptyRegistry_ReportsMeaningfulError()
    {
        Action create = () => _ = new ToolboxViewModel(new NodeRegistry());

        create.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("节点目录不能为空");
    }

    [Fact]
    public void MainWindow_DefaultConstructor_InitializesCurrentWorkflowShell()
    {
        using var viewModel = new MainWindowViewModel();

        viewModel.ApplicationName.Should().Be("FlowForge");
        viewModel.Toolbox.Templates.Should().NotBeEmpty();
        viewModel.CreateCurrentWorkflow().Nodes.Should().BeEmpty();
    }

    [Fact]
    public void MainWindow_CurrentWorkflowTwoArgumentConstructor_UsesDefaultRegistry()
    {
        using var viewModel = new MainWindowViewModel(
            new FakeWorkflowRunner(),
            new FakeWorkflowFileService());

        viewModel.Toolbox.Templates.Should().NotBeEmpty();
    }

    [Fact]
    public void MainWindow_NullRunnerArguments_AreRejectedByCompatibilityConstructors()
    {
        var files = new FakeWorkflowFileService();

        Action legacy = () => _ = new MainWindowViewModel((IStage3WorkflowRunner)null!, files);
        Action current = () => _ = new MainWindowViewModel(
            (IWorkflowRunner)null!,
            files,
            NodeRegistry.CreateDefault());

        legacy.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("legacyWorkflowRunner");
        current.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("workflowRunner");
    }

    [Fact]
    public async Task MainWindow_RunGeneralFailure_MarksRunningNodesFailedAndCanDisposeTwice()
    {
        var runner = new FakeStage3WorkflowRunner
        {
            Handler = (progress, _) =>
            {
                progress.Report(new NodeExecutionEvent(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    NodeExecutionStatus.Running));
                return Task.FromException(new InvalidOperationException("一般错误"));
            },
        };
        using var viewModel = new MainWindowViewModel(runner);

        await viewModel.RunCommand.Execute().ToTask();

        viewModel.StatusMessage.Should().Contain("一般错误");
        viewModel.Canvas.Nodes.Single(node => node.TypeId == "core.datasource.csv")
            .ExecutionState.Should().Be(NodeExecutionVisualState.Failed);
        viewModel.Canvas.Nodes.Single(node => node.TypeId == "core.sink.console")
            .ExecutionState.Should().Be(NodeExecutionVisualState.Idle);
        viewModel.Dispose();
    }

    [Fact]
    public async Task MainWindow_OpenAndSaveCancellation_KeepCurrentDocumentState()
    {
        var files = new FakeWorkflowFileService();
        using var viewModel = new MainWindowViewModel(new FakeStage3WorkflowRunner(), files);

        await viewModel.OpenCommand.Execute().ToTask();
        await viewModel.SaveCommand.Execute().ToTask();

        viewModel.CurrentFilePath.Should().BeNull();
        viewModel.StatusMessage.Should().Be("就绪。");
    }

    [Fact]
    public async Task MainWindow_UnknownExecutionEvent_LeavesCanvasStable()
    {
        var runner = new FakeStage3WorkflowRunner
        {
            Handler = (progress, _) =>
            {
                progress.Report(new NodeExecutionEvent(Guid.NewGuid(), NodeExecutionStatus.Succeeded));
                progress.Report(new NodeExecutionEvent(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    (NodeExecutionStatus)999));
                return Task.CompletedTask;
            },
        };
        using var viewModel = new MainWindowViewModel(runner);

        await viewModel.RunCommand.Execute().ToTask();

        viewModel.StatusMessage.Should().Be("示例工作流运行完成。");
        viewModel.Canvas.Nodes.Should().OnlyContain(node => node.ExecutionState == NodeExecutionVisualState.Idle);
    }

    [Fact]
    public void MainWindow_CreateCurrentWorkflow_IncludesConnectedEdge()
    {
        var registry = NodeRegistry.CreateDefault();
        var text = registry.GetDefinition("core.datasource.text").Create(
            Guid.NewGuid(),
            new TextDataSourceConfig("input.txt"));
        var console = registry.GetDefinition("core.sink.console").Create(Guid.NewGuid());
        var textViewModel = new NodeViewModel(text, registry.GetDefinition(text.TypeId), new Point(0, 0));
        var consoleViewModel = new NodeViewModel(console, registry.GetDefinition(console.TypeId), new Point(100, 0));
        var edge = new EdgeViewModel(
            Guid.NewGuid(),
            textViewModel.Outputs.Single(port => port.Id == "content"),
            consoleViewModel.Inputs.Single(port => port.Id == "value"));
        using var viewModel = new MainWindowViewModel(
            new FakeWorkflowRunner(),
            new FakeWorkflowFileService(),
            registry);
        viewModel.Canvas.Nodes.Add(textViewModel);
        viewModel.Canvas.Nodes.Add(consoleViewModel);
        viewModel.Canvas.Edges.Add(edge);

        var workflow = viewModel.CreateCurrentWorkflow();

        workflow.Edges.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new WorkflowEdge(
                edge.Id,
                text.Id,
                "content",
                console.Id,
                "value"));
    }

    private sealed class FakeStage3WorkflowRunner : IStage3WorkflowRunner
    {
        public Func<IProgress<NodeExecutionEvent>, CancellationToken, Task> Handler { get; init; }
            = static (_, _) => Task.CompletedTask;

        public Task RunAsync(IProgress<NodeExecutionEvent> progress, CancellationToken cancellationToken)
        {
            return Handler(progress, cancellationToken);
        }
    }

    private sealed class FakeWorkflowRunner : IWorkflowRunner
    {
        public Task RunAsync(
            CoreWorkflow workflow,
            IProgress<NodeExecutionEvent> progress,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWorkflowFileService : IWorkflowFileService
    {
        public Task<WorkflowOpenResult?> OpenAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WorkflowOpenResult?>(null);
        }

        public Task<string?> SaveAsync(
            WorkflowDocument document,
            string? currentPath,
            bool saveAs,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
