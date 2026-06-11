using Avalonia;
using FlowForge.App.Controls;
using FluentAssertions;
using Avalonia.Media;

namespace FlowForge.App.Tests;

public sealed class BezierEdgeShapeTests
{
    [Fact]
    public void CalculateControlPoints_LeftToRight_UsesHorizontalCurve()
    {
        var start = new Point(100, 120);
        var end = new Point(340, 180);

        var (first, second) = BezierEdgeShape.CalculateControlPoints(start, end);

        first.Should().Be(new Point(220, 120));
        second.Should().Be(new Point(220, 180));
    }

    [Fact]
    public void CalculateControlPoints_ShortDistance_UsesMinimumHandleLength()
    {
        var start = new Point(100, 120);
        var end = new Point(124, 180);

        var (first, second) = BezierEdgeShape.CalculateControlPoints(start, end);

        first.Should().Be(new Point(148, 120));
        second.Should().Be(new Point(76, 180));
    }

    [Fact]
    public void CreateGeometry_WithStartAndEnd_ReturnsBezierFigure()
    {
        var start = new Point(100, 120);
        var end = new Point(340, 180);

        var geometry = BezierEdgeShape.CreateGeometry(start, end);

        geometry.Figures.Should().ContainSingle();
        var figure = geometry.Figures.Single();
        figure.StartPoint.Should().Be(start);
        figure.Segments.Should().ContainSingle();
        var segment = figure.Segments.Single().Should().BeOfType<BezierSegment>().Subject;
        segment.Point1.Should().Be(new Point(220, 120));
        segment.Point2.Should().Be(new Point(220, 180));
        segment.Point3.Should().Be(end);
    }
}
