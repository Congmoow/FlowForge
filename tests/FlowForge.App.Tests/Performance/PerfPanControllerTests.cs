using Avalonia;
using FlowForge.App.Diagnostics;
using FluentAssertions;

namespace FlowForge.App.Tests.Performance;

public sealed class PerfPanControllerTests
{
    [Fact]
    public void ResolveDirection_ReversesAtSceneEdgesAndKeepsNodesInSweep()
    {
        var sceneBounds = new Rect(0, 0, 3000, 600);

        PerfPanController.ResolveDirection(
                new Rect(1800, 0, 1200, 600),
                sceneBounds,
                direction: 1)
            .Should().Be(-1);
        PerfPanController.ResolveDirection(
                new Rect(0, 0, 1200, 600),
                sceneBounds,
                direction: -1)
            .Should().Be(1);
        PerfPanController.ResolveDirection(
                new Rect(400, 0, 1200, 600),
                sceneBounds,
                direction: 1)
            .Should().Be(1);
    }

    [Fact]
    public void ResolveDirection_WhenSceneFitsViewport_DisablesPan()
    {
        PerfPanController.ResolveDirection(
                new Rect(0, 0, 1200, 760),
                new Rect(40, 40, 800, 600),
                direction: 1)
            .Should().Be(0);
    }

    [Fact]
    public void GetViewDelta_UsesViewportPanSignAndFixedStep()
    {
        PerfPanController.GetViewDelta(direction: 1, speed: 120, interval: TimeSpan.FromSeconds(1))
            .Should().Be(new Vector(-120, 0));
        PerfPanController.GetViewDelta(direction: -1, speed: 120, interval: TimeSpan.FromSeconds(0.5))
            .Should().Be(new Vector(60, 0));
        PerfPanController.GetViewDelta(direction: 0, speed: 120, interval: TimeSpan.FromSeconds(1))
            .Should().Be(default(Vector));
    }
}
