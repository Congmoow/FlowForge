using Avalonia;
using FlowForge.App.ViewModels;
using FluentAssertions;

namespace FlowForge.App.Tests;

public sealed class PortViewModelTests
{
    [Fact]
    public void Constructor_RequiredValues_SetsPortMetadata()
    {
        var node = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);

        var port = new PortViewModel(node, "content", "内容", PortDirection.Output, typeof(string), 0);

        port.Node.Should().BeSameAs(node);
        port.Id.Should().Be("content");
        port.DisplayName.Should().Be("内容");
        port.Direction.Should().Be(PortDirection.Output);
        port.DataType.Should().Be<string>();
        port.Index.Should().Be(0);
    }

    [Fact]
    public void AnchorPoint_InputPort_UsesLeftNodeEdge()
    {
        var node = new NodeViewModel(Guid.NewGuid(), "core.transform.text-concat", "文本拼接", 100, 200);
        var port = new PortViewModel(node, "parts", "片段", PortDirection.Input, typeof(string[]), 1);

        port.AnchorPoint.Should().Be(new Point(100, 256));
    }

    [Fact]
    public void AnchorPoint_OutputPort_UsesRightNodeEdge()
    {
        var node = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);
        var port = new PortViewModel(node, "content", "内容", PortDirection.Output, typeof(string), 0);

        port.AnchorPoint.Should().Be(new Point(320, 232));
    }

    [Fact]
    public void AnchorPoint_WhenNodeMoves_TracksNodePosition()
    {
        var node = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 100, 200);
        var port = new PortViewModel(node, "content", "内容", PortDirection.Output, typeof(string), 0);

        node.Position = new Point(180, 260);

        port.AnchorPoint.Should().Be(new Point(400, 292));
    }

    [Fact]
    public void CanConnectTo_CompatibleOutputToInput_ReturnsTrue()
    {
        var source = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 0, 0);
        var target = new NodeViewModel(Guid.NewGuid(), "core.sink.console", "控制台输出", 300, 0);
        var output = new PortViewModel(source, "content", "内容", PortDirection.Output, typeof(string), 0);
        var input = new PortViewModel(target, "value", "值", PortDirection.Input, typeof(object), 0);

        output.CanConnectTo(input).Should().BeTrue();
    }

    [Fact]
    public void CanConnectTo_IncompatibleTypes_ReturnsFalse()
    {
        var source = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 0, 0);
        var target = new NodeViewModel(Guid.NewGuid(), "core.transform.json-parse", "JSON 解析", 300, 0);
        var output = new PortViewModel(source, "content", "内容", PortDirection.Output, typeof(string), 0);
        var input = new PortViewModel(target, "json", "JSON", PortDirection.Input, typeof(int), 0);

        output.CanConnectTo(input).Should().BeFalse();
    }

    [Fact]
    public void CanConnectTo_InputToOutput_ReturnsFalse()
    {
        var source = new NodeViewModel(Guid.NewGuid(), "core.sink.console", "控制台输出", 0, 0);
        var target = new NodeViewModel(Guid.NewGuid(), "core.datasource.text", "文本读取", 300, 0);
        var input = new PortViewModel(source, "value", "值", PortDirection.Input, typeof(object), 0);
        var output = new PortViewModel(target, "content", "内容", PortDirection.Output, typeof(string), 0);

        input.CanConnectTo(output).Should().BeFalse();
    }
}
