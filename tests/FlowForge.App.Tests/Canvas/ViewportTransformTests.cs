using Avalonia;
using FlowForge.App.Canvas;
using FluentAssertions;

namespace FlowForge.App.Tests.Canvas;

public sealed class ViewportTransformTests
{
    [Fact]
    public void WorldToViewAndViewToWorld_SameTransform_RoundTripsPoint()
    {
        var transform = new ViewportTransform(2, new Vector(10, -20));
        var world = new Point(42, 18);

        var view = transform.WorldToView(world);
        var roundTrip = transform.ViewToWorld(view);

        view.Should().Be(new Point(64, 76));
        roundTrip.X.Should().BeApproximately(world.X, 0.0001);
        roundTrip.Y.Should().BeApproximately(world.Y, 0.0001);
    }

    [Fact]
    public void WorldToViewMatrix_MatchesPointConversion()
    {
        var transform = new ViewportTransform(2, new Vector(10, -20));
        var world = new Point(42, 18);

        var matrixPoint = transform.WorldToViewMatrix.Transform(world);

        matrixPoint.Should().Be(new Point(64, 76));
    }

    [Fact]
    public void ZoomAt_ViewPoint_PreservesWorldPointUnderCursor()
    {
        var transform = new ViewportTransform(1, new Vector(100, 50));
        var cursor = new Point(240, 180);
        var worldBefore = transform.ViewToWorld(cursor);

        var zoomed = transform.ZoomAt(cursor, 2);

        zoomed.Zoom.Should().Be(2);
        var worldAfter = zoomed.ViewToWorld(cursor);
        worldAfter.X.Should().BeApproximately(worldBefore.X, 0.0001);
        worldAfter.Y.Should().BeApproximately(worldBefore.Y, 0.0001);
    }

    [Theory]
    [InlineData(0.01, 0.25)]
    [InlineData(0.25, 0.25)]
    [InlineData(4, 4)]
    [InlineData(20, 4)]
    public void WithZoom_ClampsToSupportedRange(double requested, double expected)
    {
        var transform = new ViewportTransform();

        transform.WithZoom(requested).Zoom.Should().Be(expected);
    }
}
