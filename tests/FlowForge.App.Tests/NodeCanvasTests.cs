using FlowForge.App.Controls;
using FlowForge.App.ViewModels;
using FluentAssertions;

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
}
