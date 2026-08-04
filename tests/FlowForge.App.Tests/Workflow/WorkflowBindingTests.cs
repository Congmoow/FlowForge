using System.Reactive.Threading.Tasks;
using System.Text.Json;
using Avalonia;
using FlowForge.App.Services;
using FlowForge.App.ViewModels;
using FlowForge.Core.Execution;
using FlowForge.Core.Nodes.DataSource;
using FlowForge.Core.Serialization;
using FluentAssertions;
using CoreWorkflow = FlowForge.Core.Graph.Workflow;

namespace FlowForge.App.Tests.Workflow;

public sealed class WorkflowBindingTests
{
    [Fact]
    public void NodeViewModel_RealNodeConstructor_PreservesModelAndCreatesPorts()
    {
        var registry = NodeRegistry.CreateDefault();
        var definition = registry.GetDefinition("core.datasource.text");
        var node = definition.Create(Guid.NewGuid(), new TextDataSourceConfig("input.txt"));

        var viewModel = new NodeViewModel(node, definition, 12, 34);

        viewModel.Node.Should().BeSameAs(node);
        viewModel.Config.Should().Be(new TextDataSourceConfig("input.txt"));
        viewModel.Inputs.Should().BeEmpty();
        viewModel.Outputs.Should().ContainSingle(port =>
            port.Id == "content" && port.DataType == typeof(string));
    }

    [Fact]
    public void ToolboxViewModel_RegistryDefinitions_ProvidesEveryDefinition()
    {
        var registry = NodeRegistry.CreateDefault();

        var toolbox = new ToolboxViewModel(registry);

        toolbox.Templates.Select(template => template.TypeId)
            .Should().Equal(registry.Definitions.Select(definition => definition.TypeId));
    }

    [Fact]
    public void CanvasViewModel_TemplateDrop_CreatesRegistryBackedNode()
    {
        var registry = NodeRegistry.CreateDefault();
        var template = new ToolboxViewModel(registry).Templates
            .Single(item => item.TypeId == "core.datasource.text");
        var canvas = new CanvasViewModel(registry);

        canvas.AddNodeFromTemplateCommand.Execute(new AddNodeFromTemplateRequest(template, new Point(20, 30)));

        canvas.Nodes.Should().ContainSingle();
        canvas.Nodes[0].Node.TypeId.Should().Be("core.datasource.text");
        canvas.Nodes[0].Position.Should().Be(new Point(20, 30));
    }

    [Fact]
    public async Task RunCommand_ExecutesCurrentCanvasWorkflow()
    {
        var registry = NodeRegistry.CreateDefault();
        var node = registry.GetDefinition("core.transform.text-concat")
            .Create(Guid.NewGuid(), new FlowForge.Core.Nodes.Transform.TextConcatNodeConfig("|"));
        var runner = new CapturingWorkflowRunner();
        var viewModel = new MainWindowViewModel(runner, new FakeWorkflowFileService(), registry);
        viewModel.Canvas.Nodes.Add(new NodeViewModel(node, registry.GetDefinition(node.TypeId), 0, 0));

        await viewModel.RunCommand.Execute().ToTask();

        runner.Workflow.Should().NotBeNull();
        runner.Workflow!.Nodes.Should().ContainSingle().Which.Should().BeSameAs(node);
        viewModel.StatusMessage.Should().Be("工作流运行完成。");
    }

    [Fact]
    public async Task OpenCommand_InvalidDocument_PreservesExistingCanvasAndReportsError()
    {
        var registry = NodeRegistry.CreateDefault();
        var files = new FakeWorkflowFileService
        {
            OpenResult = new WorkflowOpenResult(
                "broken.ffw",
                new WorkflowDocument(
                    new WorkflowMetadata("坏文件", DateTimeOffset.UnixEpoch, "0.1.0"),
                    [new WorkflowNodeDocument(
                        Guid.NewGuid(),
                        "plugin.missing",
                        new WorkflowNodePosition(0, 0),
                        JsonDocument.Parse("{}").RootElement.Clone())],
                    [])),
        };
        var viewModel = new MainWindowViewModel(
            new CapturingWorkflowRunner(),
            files,
            registry);
        var existing = registry.GetDefinition("core.datasource.text")
            .Create(Guid.NewGuid(), new TextDataSourceConfig("keep.txt"));
        viewModel.Canvas.Nodes.Add(new NodeViewModel(existing, registry.GetDefinition(existing.TypeId), 1, 2));

        await viewModel.OpenCommand.Execute().ToTask();

        viewModel.Canvas.Nodes.Should().ContainSingle().Which.Node.Should().BeSameAs(existing);
        viewModel.CurrentFilePath.Should().BeNull();
        viewModel.StatusMessage.Should().Contain("打开失败");
    }

    [Fact]
    public async Task OpenCommand_ValidDocument_RebuildsRealNodesAndEdges()
    {
        var textId = Guid.NewGuid();
        var consoleId = Guid.NewGuid();
        var edgeId = Guid.NewGuid();
        var files = new FakeWorkflowFileService
        {
            OpenResult = new WorkflowOpenResult(
                "valid.ffw",
                new WorkflowDocument(
                    new WorkflowMetadata("有效", DateTimeOffset.UnixEpoch, "0.1.0"),
                    [
                        new WorkflowNodeDocument(
                            textId,
                            "core.datasource.text",
                            new WorkflowNodePosition(10, 20),
                            JsonDocument.Parse("{\"filePath\":\"input.txt\"}").RootElement.Clone()),
                        new WorkflowNodeDocument(
                            consoleId,
                            "core.sink.console",
                            new WorkflowNodePosition(300, 20),
                            JsonDocument.Parse("{}").RootElement.Clone()),
                    ],
                    [new WorkflowEdgeDocument(edgeId, textId, "content", consoleId, "value")])),
        };
        var viewModel = new MainWindowViewModel(
            new CapturingWorkflowRunner(),
            files,
            NodeRegistry.CreateDefault());

        await viewModel.OpenCommand.Execute().ToTask();

        viewModel.Canvas.Nodes.Should().HaveCount(2);
        viewModel.Canvas.Nodes.Single(node => node.Id == textId).Node
            .Should().BeOfType<TextDataSourceNode>();
        viewModel.Canvas.Edges.Should().ContainSingle(edge => edge.Id == edgeId);
        viewModel.StatusMessage.Should().Be("已打开 valid.ffw。");
    }

    [Fact]
    public async Task SaveCommand_SerializesCurrentNodeTypedConfig()
    {
        var registry = NodeRegistry.CreateDefault();
        var files = new FakeWorkflowFileService { SavedPath = "typed.ffw" };
        var viewModel = new MainWindowViewModel(new CapturingWorkflowRunner(), files, registry);
        var node = registry.GetDefinition("core.datasource.text")
            .Create(Guid.NewGuid(), new TextDataSourceConfig("typed.txt", "utf-16"));
        viewModel.Canvas.Nodes.Add(new NodeViewModel(node, registry.GetDefinition(node.TypeId), 10, 20));

        await viewModel.SaveCommand.Execute().ToTask();

        files.LastDocument.Should().NotBeNull();
        var config = files.LastDocument!.Nodes.Single().Config
            .Deserialize<TextDataSourceConfig>(WorkflowJsonSerializerOptions.Default);
        config.Should().Be(new TextDataSourceConfig("typed.txt", "utf-16"));
    }

    private sealed class CapturingWorkflowRunner : IWorkflowRunner
    {
        public CoreWorkflow? Workflow { get; private set; }

        public Task RunAsync(
            CoreWorkflow workflow,
            IProgress<NodeExecutionEvent> progress,
            CancellationToken cancellationToken)
        {
            Workflow = workflow;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWorkflowFileService : IWorkflowFileService
    {
        public WorkflowOpenResult? OpenResult { get; init; }

        public string? SavedPath { get; init; }

        public WorkflowDocument? LastDocument { get; private set; }

        public Task<WorkflowOpenResult?> OpenAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OpenResult);
        }

        public Task<string?> SaveAsync(
            WorkflowDocument document,
            string? currentPath,
            bool saveAs,
            CancellationToken cancellationToken = default)
        {
            LastDocument = document;
            return Task.FromResult(SavedPath ?? currentPath);
        }
    }
}
