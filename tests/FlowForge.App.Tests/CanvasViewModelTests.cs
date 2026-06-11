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
}
