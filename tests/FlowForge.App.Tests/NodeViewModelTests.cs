using FlowForge.App.ViewModels;
using FluentAssertions;
using Avalonia;

namespace FlowForge.App.Tests;

public sealed class NodeViewModelTests
{
    [Fact]
    public void Constructor_RequiredValues_SetsIdentityAndPosition()
    {
        var id = Guid.NewGuid();

        var viewModel = new NodeViewModel(id, "core.datasource.csv", "CSV 读取", 120, 240);

        viewModel.Id.Should().Be(id);
        viewModel.TypeId.Should().Be("core.datasource.csv");
        viewModel.Title.Should().Be("CSV 读取");
        viewModel.Position.Should().Be(new Point(120, 240));
        viewModel.IsSelected.Should().BeFalse();
        viewModel.ExecutionState.Should().Be(NodeExecutionVisualState.Idle);
    }

    [Fact]
    public void Position_WhenChanged_RaisesPropertyNotifications()
    {
        var viewModel = new NodeViewModel(Guid.NewGuid(), "core.datasource.csv", "CSV 读取", 0, 0);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.Position = new Point(32, 64);

        viewModel.Position.Should().Be(new Point(32, 64));
        changedProperties.Should().ContainSingle().Which.Should().Be(nameof(NodeViewModel.Position));
    }

    [Fact]
    public void Position_WhenAssignedSameValue_DoesNotRaisePropertyNotification()
    {
        var viewModel = new NodeViewModel(Guid.NewGuid(), "core.datasource.csv", "CSV 读取", 32, 64);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.Position = new Point(32, 64);

        changedProperties.Should().BeEmpty();
    }

    [Fact]
    public void IsSelected_WhenChanged_RaisesPropertyNotification()
    {
        var viewModel = new NodeViewModel(Guid.NewGuid(), "core.datasource.csv", "CSV 读取", 0, 0);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.IsSelected = true;

        viewModel.IsSelected.Should().BeTrue();
        changedProperties.Should().ContainSingle().Which.Should().Be(nameof(NodeViewModel.IsSelected));
    }

    [Fact]
    public void ExecutionState_WhenChanged_RaisesPropertyNotification()
    {
        var viewModel = new NodeViewModel(Guid.NewGuid(), "core.datasource.csv", "CSV 读取", 0, 0);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.ExecutionState = NodeExecutionVisualState.Running;

        viewModel.ExecutionState.Should().Be(NodeExecutionVisualState.Running);
        changedProperties.Should().ContainSingle().Which.Should().Be(nameof(NodeViewModel.ExecutionState));
    }
}
