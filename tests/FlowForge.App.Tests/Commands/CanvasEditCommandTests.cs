using Avalonia;
using FluentAssertions;
using FlowForge.App.Commands;
using FlowForge.App.ViewModels;

namespace FlowForge.App.Tests.Commands;

public sealed class CanvasEditCommandTests
{
    [Fact]
    public void AddNodeCommand_ExecuteAndUndo_AddsThenRemovesNode()
    {
        var canvas = new CanvasViewModel();
        var node = CreateNode("node");
        var command = new AddNodeCommand(canvas, node);

        command.Execute();
        command.Undo();

        canvas.Nodes.Should().BeEmpty();
    }

    [Fact]
    public void RemoveNodeCommand_NodeWithEdge_UndoRestoresNodeAndEdge()
    {
        var canvas = CreateConnectedCanvas(out var source, out _);
        var command = new RemoveNodeCommand(canvas, source);

        command.Execute();
        canvas.Nodes.Should().HaveCount(1);
        canvas.Edges.Should().BeEmpty();
        command.Undo();

        canvas.Nodes.Should().HaveCount(2);
        canvas.Edges.Should().ContainSingle();
    }

    [Fact]
    public void AddEdgeCommand_ExecuteAndUndo_AddsThenRemovesEdge()
    {
        var canvas = CreateConnectedCanvas(out _, out _);
        var edge = canvas.Edges.Single();
        canvas.Edges.Clear();
        var command = new AddEdgeCommand(canvas, edge);

        command.Execute();
        command.Undo();

        canvas.Edges.Should().BeEmpty();
    }

    [Fact]
    public void RemoveEdgeCommand_ExecuteAndUndo_RemovesThenRestoresEdge()
    {
        var canvas = CreateConnectedCanvas(out _, out _);
        var edge = canvas.Edges.Single();
        var command = new RemoveEdgeCommand(canvas, edge);

        command.Execute();
        command.Undo();

        canvas.Edges.Should().ContainSingle().Which.Should().BeSameAs(edge);
    }

    [Fact]
    public void MoveNodeCommand_ExecuteAndUndo_ChangesPosition()
    {
        var node = CreateNode("node");
        var command = new MoveNodeEditCommand(node, new Point(0, 0), new Point(50, 60));

        command.Execute();
        node.Position.Should().Be(new Point(50, 60));
        command.Undo();

        node.Position.Should().Be(new Point(0, 0));
    }

    [Fact]
    public void ChangePropertyCommand_ExecuteAndUndo_ChangesValue()
    {
        var value = "旧值";
        var command = new ChangePropertyCommand<string>(newValue => value = newValue, "旧值", "新值");

        command.Execute();
        value.Should().Be("新值");
        command.Undo();

        value.Should().Be("旧值");
    }

    [Fact]
    public void CompositeCommand_Undo_ReversesChildOrder()
    {
        var events = new List<string>();
        var command = new CompositeCommand([
            new CallbackCommand(() => events.Add("执行1"), () => events.Add("撤销1")),
            new CallbackCommand(() => events.Add("执行2"), () => events.Add("撤销2")),
        ]);

        command.Execute();
        command.Undo();

        events.Should().Equal("执行1", "执行2", "撤销2", "撤销1");
    }

    private static CanvasViewModel CreateConnectedCanvas(out NodeViewModel source, out NodeViewModel target)
    {
        var canvas = new CanvasViewModel();
        source = CreateNode("source");
        target = CreateNode("target");
        var output = new PortViewModel(source, "output", "输出", PortDirection.Output, typeof(string), 0);
        var input = new PortViewModel(target, "input", "输入", PortDirection.Input, typeof(string), 0);
        source.Outputs.Add(output);
        target.Inputs.Add(input);
        canvas.Nodes.Add(source);
        canvas.Nodes.Add(target);
        canvas.Edges.Add(new EdgeViewModel(Guid.NewGuid(), output, input));
        return canvas;
    }

    private static NodeViewModel CreateNode(string typeId)
    {
        return new NodeViewModel(Guid.NewGuid(), typeId, typeId, 0, 0);
    }

    private sealed class CallbackCommand(Action execute, Action undo) : FlowForge.App.Commands.ICommand
    {
        public void Execute() => execute();

        public void Undo() => undo();
    }
}
