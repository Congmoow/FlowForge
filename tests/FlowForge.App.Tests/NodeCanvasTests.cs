using FlowForge.App.Controls;
using FlowForge.App.ViewModels;
using FluentAssertions;
using Avalonia.Input;

namespace FlowForge.App.Tests;

public sealed class NodeCanvasTests
{
    [Fact]
    public void PlaceholderNode_DefaultValue_UsesExpectedBounds()
    {
        var node = NodeCanvas.PlaceholderNode;

        node.X.Should().Be(96);
        node.Y.Should().Be(80);
        node.Width.Should().Be(220);
        node.Height.Should().Be(96);
    }

    [Fact]
    public void CanvasAndToolbox_WhenAssigned_AreStoredAsControlProperties()
    {
        var canvasViewModel = new CanvasViewModel();
        var toolboxViewModel = new ToolboxViewModel();

        var canvas = new NodeCanvas
        {
            Canvas = canvasViewModel,
            Toolbox = toolboxViewModel,
        };

        canvas.Canvas.Should().BeSameAs(canvasViewModel);
        canvas.Toolbox.Should().BeSameAs(toolboxViewModel);
    }

    [Fact]
    public void ShouldUseAddNodeShortcut_CtrlClickOnNode_ReturnsFalse()
    {
        var shouldAddNode = NodeCanvas.ShouldUseAddNodeShortcut(
            KeyModifiers.Control,
            hasHitNode: true,
            hasHitPort: false,
            hasToolbox: true);

        shouldAddNode.Should().BeFalse();
    }

    [Fact]
    public void ShouldUseAddNodeShortcut_CtrlClickOnEmptyCanvas_ReturnsTrue()
    {
        var shouldAddNode = NodeCanvas.ShouldUseAddNodeShortcut(
            KeyModifiers.Control,
            hasHitNode: false,
            hasHitPort: false,
            hasToolbox: true);

        shouldAddNode.Should().BeTrue();
    }

    [Fact]
    public void ResolveSelectionGesture_PressSelectedNodeWithoutModifiers_PreservesCurrentSelection()
    {
        var gesture = NodeCanvas.ResolveSelectionGesture(KeyModifiers.None, isNodeAlreadySelected: true);

        gesture.Should().Be(SelectionGesture.Add);
    }

    [Fact]
    public void ResolveSelectionGesture_PressUnselectedNodeWithoutModifiers_ReplacesCurrentSelection()
    {
        var gesture = NodeCanvas.ResolveSelectionGesture(KeyModifiers.None, isNodeAlreadySelected: false);

        gesture.Should().Be(SelectionGesture.Replace);
    }
}
