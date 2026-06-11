using Avalonia;
using FlowForge.App.ViewModels;
using FluentAssertions;

namespace FlowForge.App.Tests;

public sealed class CanvasViewModelTests
{
    [Fact]
    public void Constructor_DefaultState_StartsWithEmptyNodeCollection()
    {
        var viewModel = new CanvasViewModel();

        viewModel.Nodes.Should().BeEmpty();
    }

    [Fact]
    public void MoveNodeCommand_ExistingNode_AddsDeltaToPosition()
    {
        var node = new NodeViewModel(Guid.NewGuid(), "core.datasource.csv", "CSV 读取", 10, 20);
        var viewModel = new CanvasViewModel();
        viewModel.Nodes.Add(node);

        viewModel.MoveNodeCommand.Execute(new MoveNodeRequest(node.Id, new Vector(12, 8)));

        node.Position.Should().Be(new Point(22, 28));
    }

    [Fact]
    public void MoveNodeCommand_ExistingNode_AppliesMultipleDeltas()
    {
        var node = new NodeViewModel(Guid.NewGuid(), "core.datasource.csv", "CSV 读取", 10, 20);
        var viewModel = new CanvasViewModel();
        viewModel.Nodes.Add(node);

        viewModel.MoveNodeCommand.Execute(new MoveNodeRequest(node.Id, new Vector(12, 8)));
        viewModel.MoveNodeCommand.Execute(new MoveNodeRequest(node.Id, new Vector(-2, 6)));

        node.Position.Should().Be(new Point(20, 34));
    }

    [Fact]
    public void MoveNodeCommand_ZeroDelta_DoesNotRaiseNodeNotification()
    {
        var node = new NodeViewModel(Guid.NewGuid(), "core.datasource.csv", "CSV 读取", 10, 20);
        var changedProperties = new List<string?>();
        var viewModel = new CanvasViewModel();
        viewModel.Nodes.Add(node);
        node.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.MoveNodeCommand.Execute(new MoveNodeRequest(node.Id, default));

        node.Position.Should().Be(new Point(10, 20));
        changedProperties.Should().BeEmpty();
    }

    [Fact]
    public void MoveNodeCommand_UnknownNode_DoesNotChangeExistingNodes()
    {
        var node = new NodeViewModel(Guid.NewGuid(), "core.datasource.csv", "CSV 读取", 10, 20);
        var viewModel = new CanvasViewModel();
        viewModel.Nodes.Add(node);

        viewModel.MoveNodeCommand.Execute(new MoveNodeRequest(Guid.NewGuid(), new Vector(12, 8)));

        node.Position.Should().Be(new Point(10, 20));
    }

    [Fact]
    public void BeginEdgeDragCommand_OutputPort_CreatesDraftEdge()
    {
        var sourceNode = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);
        var sourcePort = new PortViewModel(sourceNode, "content", "内容", PortDirection.Output, typeof(string), 0);
        sourceNode.Outputs.Add(sourcePort);
        var viewModel = new CanvasViewModel();
        viewModel.Nodes.Add(sourceNode);

        viewModel.BeginEdgeDragCommand.Execute(new BeginEdgeDragRequest(sourcePort, new Point(480, 260)));

        viewModel.DraftEdge.Should().NotBeNull();
        viewModel.DraftEdge!.Source.Should().BeSameAs(sourcePort);
        viewModel.DraftEdge.EndPoint.Should().Be(new Point(480, 260));
    }

    [Fact]
    public void UpdateEdgeDragCommand_ActiveDraft_UpdatesDraftEndPoint()
    {
        var sourceNode = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);
        var sourcePort = new PortViewModel(sourceNode, "content", "内容", PortDirection.Output, typeof(string), 0);
        var viewModel = new CanvasViewModel();
        viewModel.BeginEdgeDragCommand.Execute(new BeginEdgeDragRequest(sourcePort, new Point(480, 260)));

        viewModel.UpdateEdgeDragCommand.Execute(new Point(520, 300));

        viewModel.DraftEdge!.EndPoint.Should().Be(new Point(520, 300));
    }

    [Fact]
    public void CompleteEdgeDragCommand_CompatibleInput_AddsEdgeAndClearsDraft()
    {
        var sourceNode = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);
        var targetNode = new NodeViewModel(Guid.NewGuid(), "core.sink.console", "控制台输出", 400, 200);
        var sourcePort = new PortViewModel(sourceNode, "content", "内容", PortDirection.Output, typeof(string), 0);
        var targetPort = new PortViewModel(targetNode, "value", "值", PortDirection.Input, typeof(object), 0);
        var viewModel = new CanvasViewModel();
        viewModel.BeginEdgeDragCommand.Execute(new BeginEdgeDragRequest(sourcePort, new Point(480, 260)));

        viewModel.CompleteEdgeDragCommand.Execute(targetPort);

        viewModel.DraftEdge.Should().BeNull();
        viewModel.Edges.Should().ContainSingle();
        viewModel.Edges[0].Source.Should().BeSameAs(sourcePort);
        viewModel.Edges[0].Target.Should().BeSameAs(targetPort);
    }

    [Fact]
    public void CompleteEdgeDragCommand_IncompatibleInput_DoesNotAddEdgeAndClearsDraft()
    {
        var sourceNode = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);
        var targetNode = new NodeViewModel(Guid.NewGuid(), "core.transform.json-parse", "JSON 解析", 400, 200);
        var sourcePort = new PortViewModel(sourceNode, "content", "内容", PortDirection.Output, typeof(string), 0);
        var targetPort = new PortViewModel(targetNode, "json", "JSON", PortDirection.Input, typeof(int), 0);
        var viewModel = new CanvasViewModel();
        viewModel.BeginEdgeDragCommand.Execute(new BeginEdgeDragRequest(sourcePort, new Point(480, 260)));

        viewModel.CompleteEdgeDragCommand.Execute(targetPort);

        viewModel.DraftEdge.Should().BeNull();
        viewModel.Edges.Should().BeEmpty();
    }

    [Fact]
    public void PreviewEdgeTargetCommand_CompatibleInput_SetsCompatibleState()
    {
        var sourceNode = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);
        var targetNode = new NodeViewModel(Guid.NewGuid(), "core.sink.console", "控制台输出", 400, 200);
        var sourcePort = new PortViewModel(sourceNode, "content", "内容", PortDirection.Output, typeof(string), 0);
        var targetPort = new PortViewModel(targetNode, "value", "值", PortDirection.Input, typeof(object), 0);
        var viewModel = new CanvasViewModel();
        viewModel.BeginEdgeDragCommand.Execute(new BeginEdgeDragRequest(sourcePort, new Point(480, 260)));

        viewModel.PreviewEdgeTargetCommand.Execute(targetPort);

        viewModel.ConnectionPreviewState.Should().Be(ConnectionPreviewState.Compatible);
        viewModel.PreviewTargetPort.Should().BeSameAs(targetPort);
    }

    [Fact]
    public void PreviewEdgeTargetCommand_IncompatibleInput_SetsIncompatibleState()
    {
        var sourceNode = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);
        var targetNode = new NodeViewModel(Guid.NewGuid(), "core.transform.json-parse", "JSON 解析", 400, 200);
        var sourcePort = new PortViewModel(sourceNode, "content", "内容", PortDirection.Output, typeof(string), 0);
        var targetPort = new PortViewModel(targetNode, "json", "JSON", PortDirection.Input, typeof(int), 0);
        var viewModel = new CanvasViewModel();
        viewModel.BeginEdgeDragCommand.Execute(new BeginEdgeDragRequest(sourcePort, new Point(480, 260)));

        viewModel.PreviewEdgeTargetCommand.Execute(targetPort);

        viewModel.ConnectionPreviewState.Should().Be(ConnectionPreviewState.Incompatible);
        viewModel.PreviewTargetPort.Should().BeSameAs(targetPort);
    }

    [Fact]
    public void CancelEdgeDragCommand_ActiveDraft_ClearsDraft()
    {
        var sourceNode = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);
        var sourcePort = new PortViewModel(sourceNode, "content", "内容", PortDirection.Output, typeof(string), 0);
        var viewModel = new CanvasViewModel();
        viewModel.BeginEdgeDragCommand.Execute(new BeginEdgeDragRequest(sourcePort, new Point(480, 260)));

        viewModel.CancelEdgeDragCommand.Execute(null);

        viewModel.DraftEdge.Should().BeNull();
    }
}
