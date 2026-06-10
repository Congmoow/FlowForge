using FlowForge.App.Controls;
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
}
