using Avalonia;
using FlowForge.App.Canvas;
using FlowForge.App.Controls;
using FlowForge.App.ViewModels;
using FluentAssertions;

namespace FlowForge.App.Tests.Canvas;

public sealed class BezierBoundsTests
{
    [Fact]
    public void GetBounds_IncludesEndpointsAndBothControlPoints()
    {
        var start = new Point(100, 100);
        var end = new Point(200, 120);
        var controls = BezierEdgeShape.CalculateControlPoints(start, end);

        var bounds = BezierBounds.GetBounds(start, end, strokeWidth: 4);

        bounds.Contains(start).Should().BeTrue();
        bounds.Contains(end).Should().BeTrue();
        bounds.Contains(controls.First).Should().BeTrue();
        bounds.Contains(controls.Second).Should().BeTrue();
        bounds.Should().Be(new Rect(98, 98, 104, 24));
    }

    [Fact]
    public void GetBounds_InflatesByHalfStrokeWidth()
    {
        var bounds = BezierBounds.GetBounds(new Point(0, 0), new Point(100, 0), strokeWidth: 10);

        bounds.Should().Be(new Rect(-5, -5, 110, 10));
    }

    [Fact]
    public void CullEdges_CurveControlHullCrossesViewport_KeepsEdge()
    {
        var sourceNode = new NodeViewModel(Guid.NewGuid(), "source", "源", -300, 0);
        var targetNode = new NodeViewModel(Guid.NewGuid(), "target", "目标", 80, 0);
        var source = new PortViewModel(sourceNode, "out", "输出", PortDirection.Output, typeof(string), 0);
        var target = new PortViewModel(targetNode, "in", "输入", PortDirection.Input, typeof(string), 0);
        var edge = new EdgeViewModel(Guid.NewGuid(), source, target);

        var visible = CanvasCulling.CullEdges([edge], new Rect(-5, 20, 10, 20), strokeWidth: 2);

        visible.Should().ContainSingle().Which.Should().BeSameAs(edge);
    }
}
