using Avalonia;
using FlowForge.App.Canvas;
using FlowForge.App.ViewModels;
using FluentAssertions;

namespace FlowForge.App.Tests.Canvas;

public sealed class CanvasCullingTests
{
    [Fact]
    public void NodeBounds_PositionAndDefaultSize_ReturnsWorldRectangle()
    {
        var bounds = CanvasCulling.NodeBounds(new Point(-40, 18));

        bounds.Should().Be(new Rect(-40, 18, 220, 96));
    }

    [Fact]
    public void CullNodes_OffscreenNode_IsExcludedAndOrderIsPreserved()
    {
        var first = new NodeViewModel(Guid.NewGuid(), "first", "第一", -400, 0);
        var second = new NodeViewModel(Guid.NewGuid(), "second", "第二", 20, 20);
        var third = new NodeViewModel(Guid.NewGuid(), "third", "第三", 260, 20);

        var visible = CanvasCulling.CullNodes([first, second, third], new Rect(0, 0, 240, 160));

        visible.Should().Equal(second);
    }

    [Theory]
    [InlineData(-220, 0, true)]
    [InlineData(240, 0, true)]
    [InlineData(-221, 0, false)]
    [InlineData(241, 0, false)]
    public void NodeBounds_TouchingViewportBoundary_IsIncluded(
        double x,
        double y,
        bool expectedVisible)
    {
        var bounds = CanvasCulling.NodeBounds(new Point(x, y));

        CanvasCulling.IntersectsIncludingBoundary(bounds, new Rect(0, 0, 240, 160))
            .Should().Be(expectedVisible);
    }

    [Fact]
    public void ViewportTransform_WithPanAndZoom_CullsAgainstWorldBounds()
    {
        var viewport = new CanvasViewport
        {
            ViewSize = new Size(200, 100),
        };
        viewport.PanBy(new Vector(100, 50));
        viewport.ZoomAt(new Point(100, 50), 2);

        var visibleWorld = viewport.WorldBounds;
        CanvasCulling.IntersectsIncludingBoundary(
                CanvasCulling.NodeBounds(new Point(-50, -25)),
                visibleWorld)
            .Should().BeTrue();
    }
}
