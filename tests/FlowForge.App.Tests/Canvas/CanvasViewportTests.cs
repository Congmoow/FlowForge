using Avalonia;
using FlowForge.App.Canvas;
using FluentAssertions;

namespace FlowForge.App.Tests.Canvas;

public sealed class CanvasViewportTests
{
    [Fact]
    public void PanBy_ViewDelta_MovesWorldOriginOppositeToScreenDelta()
    {
        var viewport = new CanvasViewport
        {
            ViewSize = new Size(800, 600),
        };

        viewport.PanBy(new Vector(80, -40));

        viewport.Transform.Pan.Should().Be(new Vector(-80, 40));
    }

    [Fact]
    public void ZoomAt_MousePoint_UsesCursorAsStableAnchor()
    {
        var viewport = new CanvasViewport
        {
            ViewSize = new Size(800, 600),
        };
        var cursor = new Point(320, 240);
        var worldBefore = viewport.Transform.ViewToWorld(cursor);

        viewport.ZoomAt(cursor, 2);

        var worldAfter = viewport.Transform.ViewToWorld(cursor);
        worldAfter.X.Should().BeApproximately(worldBefore.X, 0.0001);
        worldAfter.Y.Should().BeApproximately(worldBefore.Y, 0.0001);
    }

    [Fact]
    public void WorldBounds_ViewSizeAndTransform_ReturnsVisibleWorldRectangle()
    {
        var viewport = new CanvasViewport
        {
            ViewSize = new Size(400, 200),
        };
        viewport.PanBy(new Vector(100, 50));
        viewport.ZoomAt(new Point(0, 0), 2);

        viewport.WorldBounds.Should().Be(new Rect(-100, -50, 200, 100));
    }

    [Theory]
    [InlineData(0.25, 96)]
    [InlineData(1, 24)]
    [InlineData(4, 6)]
    public void GridStepForZoom_AdaptsWorldGridDensity(double zoom, double expected)
    {
        CanvasViewport.GridStepForZoom(zoom).Should().Be(expected);
    }
}
