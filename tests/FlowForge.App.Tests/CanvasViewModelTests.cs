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
    public void AddNodeFromTemplateCommand_TemplateAndPosition_AddsNodeWithPorts()
    {
        var template = new NodeTemplateViewModel(
            "core.datasource.text",
            "文本读取",
            [],
            [new PortTemplateViewModel("content", "content", PortDirection.Output, typeof(string))]);
        var viewModel = new CanvasViewModel();

        viewModel.AddNodeFromTemplateCommand.Execute(new AddNodeFromTemplateRequest(template, new Point(120, 240)));

        viewModel.Nodes.Should().ContainSingle();
        var node = viewModel.Nodes[0];
        node.TypeId.Should().Be("core.datasource.text");
        node.Title.Should().Be("文本读取");
        node.Position.Should().Be(new Point(120, 240));
        node.Outputs.Should().ContainSingle();
        node.Outputs[0].DataType.Should().Be<string>();
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
    public void SelectNodeCommand_NormalClick_SelectsOnlyRequestedNode()
    {
        var first = new NodeViewModel(Guid.NewGuid(), "core.datasource.csv", "CSV 读取", 10, 20);
        var second = new NodeViewModel(Guid.NewGuid(), "core.sink.console", "控制台输出", 300, 20)
        {
            IsSelected = true,
        };
        var viewModel = new CanvasViewModel();
        viewModel.Nodes.Add(first);
        viewModel.Nodes.Add(second);

        viewModel.SelectNodeCommand.Execute(new SelectNodeRequest(first.Id, SelectionGesture.Replace));

        first.IsSelected.Should().BeTrue();
        second.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void SelectNodeCommand_CtrlClick_TogglesRequestedNode()
    {
        var node = new NodeViewModel(Guid.NewGuid(), "core.datasource.csv", "CSV 读取", 10, 20);
        var viewModel = new CanvasViewModel();
        viewModel.Nodes.Add(node);

        viewModel.SelectNodeCommand.Execute(new SelectNodeRequest(node.Id, SelectionGesture.Toggle));
        viewModel.SelectNodeCommand.Execute(new SelectNodeRequest(node.Id, SelectionGesture.Toggle));

        node.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void SelectNodeCommand_ShiftClick_AddsRequestedNode()
    {
        var first = new NodeViewModel(Guid.NewGuid(), "core.datasource.csv", "CSV 读取", 10, 20)
        {
            IsSelected = true,
        };
        var second = new NodeViewModel(Guid.NewGuid(), "core.sink.console", "控制台输出", 300, 20);
        var viewModel = new CanvasViewModel();
        viewModel.Nodes.Add(first);
        viewModel.Nodes.Add(second);

        viewModel.SelectNodeCommand.Execute(new SelectNodeRequest(second.Id, SelectionGesture.Add));

        first.IsSelected.Should().BeTrue();
        second.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void ClearSelectionCommand_SelectedNodes_ClearsSelection()
    {
        var node = new NodeViewModel(Guid.NewGuid(), "core.datasource.csv", "CSV 读取", 10, 20)
        {
            IsSelected = true,
        };
        var viewModel = new CanvasViewModel();
        viewModel.Nodes.Add(node);

        viewModel.ClearSelectionCommand.Execute(null);

        node.IsSelected.Should().BeFalse();
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
    public void MoveSelectedNodesCommand_SelectedNodes_AddsDeltaToAllSelectedNodes()
    {
        var first = new NodeViewModel(Guid.NewGuid(), "core.datasource.csv", "CSV 读取", 10, 20)
        {
            IsSelected = true,
        };
        var second = new NodeViewModel(Guid.NewGuid(), "core.sink.console", "控制台输出", 300, 20)
        {
            IsSelected = true,
        };
        var third = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 500, 20);
        var viewModel = new CanvasViewModel();
        viewModel.Nodes.Add(first);
        viewModel.Nodes.Add(second);
        viewModel.Nodes.Add(third);

        viewModel.MoveSelectedNodesCommand.Execute(new Vector(8, 12));

        first.Position.Should().Be(new Point(18, 32));
        second.Position.Should().Be(new Point(308, 32));
        third.Position.Should().Be(new Point(500, 20));
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
    public void CompleteEdgeDragCommand_InputAlreadyConnected_DoesNotAddSecondEdge()
    {
        var firstSourceNode = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);
        var secondSourceNode = new NodeViewModel(Guid.NewGuid(), "core.datasource.csv", "CSV 读取", 100, 360);
        var targetNode = new NodeViewModel(Guid.NewGuid(), "core.sink.console", "控制台输出", 400, 200);
        var firstSourcePort = new PortViewModel(firstSourceNode, "content", "内容", PortDirection.Output, typeof(string), 0);
        var secondSourcePort = new PortViewModel(secondSourceNode, "rows", "rows", PortDirection.Output, typeof(object), 0);
        var targetPort = new PortViewModel(targetNode, "value", "值", PortDirection.Input, typeof(object), 0);
        var viewModel = new CanvasViewModel();
        viewModel.BeginEdgeDragCommand.Execute(new BeginEdgeDragRequest(firstSourcePort, new Point(480, 260)));
        viewModel.CompleteEdgeDragCommand.Execute(targetPort);

        viewModel.BeginEdgeDragCommand.Execute(new BeginEdgeDragRequest(secondSourcePort, new Point(480, 360)));
        viewModel.CompleteEdgeDragCommand.Execute(targetPort);

        viewModel.DraftEdge.Should().BeNull();
        viewModel.Edges.Should().ContainSingle();
        viewModel.Edges[0].Source.Should().BeSameAs(firstSourcePort);
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
