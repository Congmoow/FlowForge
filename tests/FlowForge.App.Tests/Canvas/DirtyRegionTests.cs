using Avalonia;
using Avalonia.Media;
using FlowForge.App.Canvas;
using FlowForge.App.Controls;
using FlowForge.App.ViewModels;
using FluentAssertions;

namespace FlowForge.App.Tests.Canvas;

public sealed class DirtyRegionTests
{
    [Fact]
    public void NodeDrawOperation_EqualSnapshots_HaveStableEqualityAndCachedResources()
    {
        var snapshot = CreateNodeSnapshot(new Rect(10, 20, 220, 96));
        using var first = new NodeDrawOperation(snapshot);
        using var second = new NodeDrawOperation(snapshot);

        first.Equals(second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
        first.TitleText.Should().NotBeNull();
        first.Bounds.Should().Be(snapshot.Bounds);
    }

    [Fact]
    public void DrawOperations_Dispose_IsIdempotentAndMarksOperationDisposed()
    {
        using var node = new NodeDrawOperation(CreateNodeSnapshot(new Rect(0, 0, 220, 96)));
        using var edge = new EdgeDrawOperation(new EdgeDrawSnapshot(
            Guid.NewGuid(),
            new Point(0, 10),
            new Point(100, 10),
            2));

        node.Dispose();
        node.Dispose();
        edge.Dispose();
        edge.Dispose();

        node.IsDisposed.Should().BeTrue();
        edge.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void RetainedCanvasScene_ReplaceNode_ReturnsOldAndNewBoundsUnion()
    {
        var scene = new RetainedCanvasScene();
        var nodeId = Guid.NewGuid();
        scene.ReplaceNode(new NodeDrawOperation(CreateNodeSnapshot(new Rect(10, 20, 220, 96), nodeId)));

        var dirty = scene.ReplaceNode(new NodeDrawOperation(CreateNodeSnapshot(new Rect(30, 20, 220, 96), nodeId)));

        dirty.Should().Be(new Rect(10, 20, 240, 96));
        scene.NodeOperations.Should().ContainSingle();
        scene.LastDirtyRect.Should().Be(dirty);
    }

    [Fact]
    public void EdgeDrawOperation_BoundsAndHitTest_UseCachedBezierGeometry()
    {
        using var operation = new EdgeDrawOperation(new EdgeDrawSnapshot(
            Guid.NewGuid(),
            new Point(-80, 32),
            new Point(80, 32),
            2));

        operation.Geometry.Should().NotBeNull();
        operation.Bounds.Should().Be(BezierBounds.GetBounds(
            operation.Snapshot.StartPoint,
            operation.Snapshot.EndPoint,
            operation.Snapshot.StrokeWidth));
        operation.HitTest(new Point(0, 32)).Should().BeTrue();
    }

    [Fact]
    public void NodeCanvas_NodeMove_ReplacesOnlyTheMovedRetainedOperation()
    {
        var canvasViewModel = new CanvasViewModel();
        var node = new NodeViewModel(Guid.NewGuid(), "core.test.node", "节点", 10, 20);
        canvasViewModel.Nodes.Add(node);
        var canvas = new NodeCanvas { Canvas = canvasViewModel };
        var previous = canvas.RetainedScene.NodeOperations.Should().ContainSingle().Subject;

        node.Position = new Point(80, 120);

        var current = canvas.RetainedScene.NodeOperations.Should().ContainSingle().Subject;
        current.Should().NotBeSameAs(previous);
        previous.IsDisposed.Should().BeTrue();
        current.Bounds.Should().Be(CanvasCulling.NodeBounds(node.Position));
        canvas.RetainedScene.LastDirtyRect.Should().Be(new Rect(10, 20, 290, 196));
    }

    [Fact]
    public void NodeCanvas_NodeMove_UpdatesOnlyTheChangedChildVisual()
    {
        var canvasViewModel = new CanvasViewModel();
        var movedNode = new NodeViewModel(Guid.NewGuid(), "core.test.moved", "移动节点", 10, 20);
        var unchangedNode = new NodeViewModel(Guid.NewGuid(), "core.test.unchanged", "未移动节点", 400, 20);
        canvasViewModel.Nodes.Add(movedNode);
        canvasViewModel.Nodes.Add(unchangedNode);
        using var canvas = new NodeCanvas { Canvas = canvasViewModel };
        canvas.Measure(new Size(800, 600));
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var before = canvas.RetainedVisuals
            .ToDictionary(visual => visual.OperationId);

        movedNode.Position = new Point(80, 120);

        var after = canvas.RetainedVisuals
            .ToDictionary(visual => visual.OperationId);
        canvas.RetainedScene.NodeOperations
            .Single(operation => operation.Snapshot.Id == movedNode.Id)
            .Bounds.Should().Be(new Rect(80, 120, 220, 96));
        after[movedNode.Id].Operation.Bounds.Should().Be(new Rect(80, 120, 220, 96));
        after[movedNode.Id].Should().BeSameAs(before[movedNode.Id]);
        after[movedNode.Id].ViewBounds.Should().Be(new Rect(80, 120, 220, 96));
        after[unchangedNode.Id].Should().BeSameAs(before[unchangedNode.Id]);
        after[unchangedNode.Id].ViewBounds.Should().Be(before[unchangedNode.Id].ViewBounds);
    }

    [Fact]
    public void NodeCanvas_RetainedVisuals_AreArrangedAtTheirWorldBounds()
    {
        var canvasViewModel = new CanvasViewModel();
        var firstNode = new NodeViewModel(Guid.NewGuid(), "core.test.first", "第一个节点", 10, 20);
        var secondNode = new NodeViewModel(Guid.NewGuid(), "core.test.second", "第二个节点", 400, 120);
        canvasViewModel.Nodes.Add(firstNode);
        canvasViewModel.Nodes.Add(secondNode);
        using var canvas = new NodeCanvas { Canvas = canvasViewModel };

        canvas.Measure(new Size(800, 600));
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var firstVisual = canvas.RetainedVisuals.Single(visual => visual.OperationId == firstNode.Id);
        var secondVisual = canvas.RetainedVisuals.Single(visual => visual.OperationId == secondNode.Id);
        Avalonia.Controls.Canvas.GetLeft(firstVisual).Should().Be(10);
        Avalonia.Controls.Canvas.GetTop(firstVisual).Should().Be(20);
        Avalonia.Controls.Canvas.GetLeft(secondVisual).Should().Be(400);
        Avalonia.Controls.Canvas.GetTop(secondVisual).Should().Be(120);

        firstNode.Position = new Point(80, 160);

        Avalonia.Controls.Canvas.GetLeft(firstVisual).Should().Be(80);
        Avalonia.Controls.Canvas.GetTop(firstVisual).Should().Be(160);
    }

    [Fact]
    public void RetainedOperationVisual_ViewportTransform_UpdatesBoundsWithoutReplacingOperation()
    {
        using var operation = new NodeDrawOperation(CreateNodeSnapshot(new Rect(0, 0, 220, 96)));
        using var visual = new RetainedOperationVisual(operation, new ViewportTransform());

        visual.ApplyViewportTransform(new ViewportTransform(2, new Vector(5, 7)));

        visual.Operation.Should().BeSameAs(operation);
        visual.ViewBounds.Should().Be(new Rect(-10, -14, 440, 192));
    }

    private static NodeDrawSnapshot CreateNodeSnapshot(Rect bounds, Guid? id = null)
    {
        return new NodeDrawSnapshot(
            id ?? Guid.NewGuid(),
            bounds,
            "节点",
            "core.test.node",
            Color.Parse("#2563EB"),
            false,
            [new Point(bounds.Left, bounds.Top + 32), new Point(bounds.Right, bounds.Top + 32)]);
    }
}
