using Avalonia;
using FlowForge.App.ViewModels;
using FluentAssertions;

namespace FlowForge.App.Tests;

public sealed class EdgeViewModelTests
{
    [Fact]
    public void Constructor_ConnectedPorts_SetsIdentityAndEndpoints()
    {
        var sourceNode = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);
        var targetNode = new NodeViewModel(Guid.NewGuid(), "core.sink.console", "控制台输出", 400, 200);
        var sourcePort = new PortViewModel(sourceNode, "content", "内容", PortDirection.Output, typeof(string), 0);
        var targetPort = new PortViewModel(targetNode, "value", "值", PortDirection.Input, typeof(object), 0);
        var edgeId = Guid.NewGuid();

        var edge = new EdgeViewModel(edgeId, sourcePort, targetPort);

        edge.Id.Should().Be(edgeId);
        edge.Source.Should().BeSameAs(sourcePort);
        edge.Target.Should().BeSameAs(targetPort);
        edge.IsDraft.Should().BeFalse();
        edge.StartPoint.Should().Be(new Point(320, 232));
        edge.EndPoint.Should().Be(new Point(400, 232));
    }

    [Fact]
    public void Endpoints_WhenNodesMove_TrackPortAnchors()
    {
        var sourceNode = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);
        var targetNode = new NodeViewModel(Guid.NewGuid(), "core.sink.console", "控制台输出", 400, 200);
        var sourcePort = new PortViewModel(sourceNode, "content", "内容", PortDirection.Output, typeof(string), 0);
        var targetPort = new PortViewModel(targetNode, "value", "值", PortDirection.Input, typeof(object), 0);
        var edge = new EdgeViewModel(Guid.NewGuid(), sourcePort, targetPort);

        sourceNode.Position = new Point(140, 260);
        targetNode.Position = new Point(500, 320);

        edge.StartPoint.Should().Be(new Point(360, 292));
        edge.EndPoint.Should().Be(new Point(500, 352));
    }

    [Fact]
    public void CreateDraft_WithCurrentPoint_CreatesDraftEdge()
    {
        var sourceNode = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);
        var sourcePort = new PortViewModel(sourceNode, "content", "内容", PortDirection.Output, typeof(string), 0);

        var edge = EdgeViewModel.CreateDraft(sourcePort, new Point(480, 260));

        edge.Source.Should().BeSameAs(sourcePort);
        edge.Target.Should().BeNull();
        edge.IsDraft.Should().BeTrue();
        edge.StartPoint.Should().Be(new Point(320, 232));
        edge.EndPoint.Should().Be(new Point(480, 260));
    }

    [Fact]
    public void DraftEndPoint_WhenChanged_RaisesPropertyNotification()
    {
        var sourceNode = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);
        var sourcePort = new PortViewModel(sourceNode, "content", "内容", PortDirection.Output, typeof(string), 0);
        var edge = EdgeViewModel.CreateDraft(sourcePort, new Point(480, 260));
        var changedProperties = new List<string?>();
        edge.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        edge.DraftEndPoint = new Point(520, 300);

        edge.EndPoint.Should().Be(new Point(520, 300));
        changedProperties.Should().Contain(nameof(EdgeViewModel.DraftEndPoint));
        changedProperties.Should().Contain(nameof(EdgeViewModel.EndPoint));
    }

    [Fact]
    public void Constructor_IncompatiblePorts_ThrowsArgumentException()
    {
        var sourceNode = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);
        var targetNode = new NodeViewModel(Guid.NewGuid(), "core.transform.json-parse", "JSON 解析", 400, 200);
        var sourcePort = new PortViewModel(sourceNode, "content", "内容", PortDirection.Output, typeof(string), 0);
        var targetPort = new PortViewModel(targetNode, "json", "JSON", PortDirection.Input, typeof(int), 0);

        var act = () => new EdgeViewModel(Guid.NewGuid(), sourcePort, targetPort);

        act.Should().Throw<ArgumentException>().WithMessage("源端口和目标端口不兼容。*");
    }
}
