using FluentAssertions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Graph;

namespace FlowForge.Core.Tests.Graph;

public sealed class WorkflowTests
{
    [Fact]
    public void AddNode_NewNode_AddsNodeToReadOnlyCollection()
    {
        var workflow = new Workflow();
        var node = CreateNode();

        workflow.AddNode(node);

        workflow.Nodes.Should().ContainSingle().Which.Should().BeSameAs(node);
        workflow.Nodes.Should().BeAssignableTo<IReadOnlyList<INode>>();
        workflow.Nodes.Should().NotBeAssignableTo<ICollection<INode>>();
    }

    [Fact]
    public void AddNode_DuplicateNodeId_ThrowsAndKeepsExistingNode()
    {
        var id = Guid.NewGuid();
        var existing = CreateNode(id);
        var duplicate = CreateNode(id);
        var workflow = new Workflow();
        workflow.AddNode(existing);

        var action = () => workflow.AddNode(duplicate);

        action.Should().Throw<DuplicateWorkflowElementException>();
        workflow.Nodes.Should().ContainSingle().Which.Should().BeSameAs(existing);
    }

    [Fact]
    public void AddEdge_CompatiblePorts_AddsEdge()
    {
        var source = CreateNode(outputs: [new TestPort<string>("output", "输出")]);
        var target = CreateNode(inputs: [new TestPort<string>("input", "输入")]);
        var edge = CreateEdge(source, "output", target, "input");
        var workflow = CreateWorkflow(source, target);

        workflow.AddEdge(edge);

        workflow.Edges.Should().ContainSingle().Which.Should().Be(edge);
        workflow.Edges.Should().NotBeAssignableTo<ICollection<WorkflowEdge>>();
    }

    [Fact]
    public void AddEdge_DerivedOutputToBaseInput_AddsEdge()
    {
        var source = CreateNode(outputs: [new TestPort<MemoryStream>("output", "输出")]);
        var target = CreateNode(inputs: [new TestPort<Stream>("input", "输入")]);
        var workflow = CreateWorkflow(source, target);

        workflow.AddEdge(CreateEdge(source, "output", target, "input"));

        workflow.Edges.Should().ContainSingle();
    }

    [Fact]
    public void AddEdge_BaseOutputToDerivedInput_ThrowsTypeMismatch()
    {
        var source = CreateNode(outputs: [new TestPort<Stream>("output", "输出")]);
        var target = CreateNode(inputs: [new TestPort<MemoryStream>("input", "输入")]);
        var workflow = CreateWorkflow(source, target);

        var action = () => workflow.AddEdge(CreateEdge(source, "output", target, "input"));

        action.Should().Throw<PortTypeMismatchException>();
        workflow.Edges.Should().BeEmpty();
    }

    [Fact]
    public void AddEdge_UnknownSourceNode_ThrowsInvalidReference()
    {
        var target = CreateNode(inputs: [new TestPort<string>("input", "输入")]);
        var workflow = CreateWorkflow(target);
        var edge = new WorkflowEdge(Guid.NewGuid(), Guid.NewGuid(), "output", target.Id, "input");

        var action = () => workflow.AddEdge(edge);

        action.Should().Throw<WorkflowReferenceException>();
        workflow.Edges.Should().BeEmpty();
    }

    [Fact]
    public void AddEdge_UnknownPort_ThrowsInvalidReference()
    {
        var source = CreateNode(outputs: [new TestPort<string>("output", "输出")]);
        var target = CreateNode(inputs: [new TestPort<string>("input", "输入")]);
        var workflow = CreateWorkflow(source, target);

        var action = () => workflow.AddEdge(CreateEdge(source, "missing", target, "input"));

        action.Should().Throw<WorkflowReferenceException>();
        workflow.Edges.Should().BeEmpty();
    }

    [Fact]
    public void AddEdge_InputPortUsedAsSource_ThrowsInvalidDirection()
    {
        var source = CreateNode(inputs: [new TestPort<string>("input", "输入")]);
        var target = CreateNode(inputs: [new TestPort<string>("target", "目标")]);
        var workflow = CreateWorkflow(source, target);

        var action = () => workflow.AddEdge(CreateEdge(source, "input", target, "target"));

        action.Should().Throw<PortDirectionException>();
        workflow.Edges.Should().BeEmpty();
    }

    [Fact]
    public void AddEdge_OutputPortUsedAsTarget_ThrowsInvalidDirection()
    {
        var source = CreateNode(outputs: [new TestPort<string>("source", "源")]);
        var target = CreateNode(outputs: [new TestPort<string>("output", "输出")]);
        var workflow = CreateWorkflow(source, target);

        var action = () => workflow.AddEdge(CreateEdge(source, "source", target, "output"));

        action.Should().Throw<PortDirectionException>();
        workflow.Edges.Should().BeEmpty();
    }

    [Fact]
    public void AddEdge_SecondEdgeToSameInput_ThrowsConnectionLimit()
    {
        var firstSource = CreateNode(outputs: [new TestPort<string>("output", "输出")]);
        var secondSource = CreateNode(outputs: [new TestPort<string>("output", "输出")]);
        var target = CreateNode(inputs: [new TestPort<string>("input", "输入")]);
        var workflow = CreateWorkflow(firstSource, secondSource, target);
        workflow.AddEdge(CreateEdge(firstSource, "output", target, "input"));

        var action = () => workflow.AddEdge(CreateEdge(secondSource, "output", target, "input"));

        action.Should().Throw<InputPortAlreadyConnectedException>();
        workflow.Edges.Should().ContainSingle();
    }

    [Fact]
    public void AddEdge_MultipleEdgesFromSameOutput_AllowsFanOut()
    {
        var source = CreateNode(outputs: [new TestPort<string>("output", "输出")]);
        var firstTarget = CreateNode(inputs: [new TestPort<string>("input", "输入")]);
        var secondTarget = CreateNode(inputs: [new TestPort<string>("input", "输入")]);
        var workflow = CreateWorkflow(source, firstTarget, secondTarget);

        workflow.AddEdge(CreateEdge(source, "output", firstTarget, "input"));
        workflow.AddEdge(CreateEdge(source, "output", secondTarget, "input"));

        workflow.Edges.Should().HaveCount(2);
    }

    [Fact]
    public void AddEdge_DuplicateEdgeId_ThrowsAndKeepsExistingEdge()
    {
        var edgeId = Guid.NewGuid();
        var source = CreateNode(outputs: [new TestPort<string>("output", "输出")]);
        var firstTarget = CreateNode(inputs: [new TestPort<string>("input", "输入")]);
        var secondTarget = CreateNode(inputs: [new TestPort<string>("input", "输入")]);
        var workflow = CreateWorkflow(source, firstTarget, secondTarget);
        var existing = CreateEdge(source, "output", firstTarget, "input", edgeId);
        workflow.AddEdge(existing);

        var action = () => workflow.AddEdge(CreateEdge(source, "output", secondTarget, "input", edgeId));

        action.Should().Throw<DuplicateWorkflowElementException>();
        workflow.Edges.Should().ContainSingle().Which.Should().Be(existing);
    }

    [Fact]
    public void AddEdge_SelfLoop_AddsEdgeForLaterCycleDetection()
    {
        var node = CreateNode(
            inputs: [new TestPort<string>("input", "输入")],
            outputs: [new TestPort<string>("output", "输出")]);
        var workflow = CreateWorkflow(node);

        workflow.AddEdge(CreateEdge(node, "output", node, "input"));

        workflow.Edges.Should().ContainSingle();
    }

    [Fact]
    public void RemoveNode_ConnectedNode_RemovesNodeAndRelatedEdges()
    {
        var source = CreateNode(outputs: [new TestPort<string>("output", "输出")]);
        var target = CreateNode(inputs: [new TestPort<string>("input", "输入")]);
        var workflow = CreateWorkflow(source, target);
        workflow.AddEdge(CreateEdge(source, "output", target, "input"));

        var removed = workflow.RemoveNode(source.Id);

        removed.Should().BeTrue();
        workflow.Nodes.Should().ContainSingle().Which.Should().BeSameAs(target);
        workflow.Edges.Should().BeEmpty();
    }

    [Fact]
    public void RemoveEdge_ExistingEdge_RemovesEdge()
    {
        var source = CreateNode(outputs: [new TestPort<string>("output", "输出")]);
        var target = CreateNode(inputs: [new TestPort<string>("input", "输入")]);
        var edge = CreateEdge(source, "output", target, "input");
        var workflow = CreateWorkflow(source, target);
        workflow.AddEdge(edge);

        var removed = workflow.RemoveEdge(edge.Id);

        removed.Should().BeTrue();
        workflow.Edges.Should().BeEmpty();
    }

    private static Workflow CreateWorkflow(params INode[] nodes)
    {
        var workflow = new Workflow();
        foreach (var node in nodes)
        {
            workflow.AddNode(node);
        }

        return workflow;
    }

    private static TestNode CreateNode(
        Guid? id = null,
        IReadOnlyList<IPort>? inputs = null,
        IReadOnlyList<IPort>? outputs = null)
    {
        return new TestNode(id ?? Guid.NewGuid(), inputs ?? [], outputs ?? []);
    }

    private static WorkflowEdge CreateEdge(
        INode source,
        string sourcePortId,
        INode target,
        string targetPortId,
        Guid? id = null)
    {
        return new WorkflowEdge(id ?? Guid.NewGuid(), source.Id, sourcePortId, target.Id, targetPortId);
    }

    private sealed record TestConfig : INodeConfig;

    private sealed class TestPort<T>(string id, string name) : IPort<T>
    {
        public string Id { get; } = id;

        public string Name { get; } = name;

        public Type DataType => typeof(T);
    }

    private sealed class TestNode(
        Guid id,
        IReadOnlyList<IPort> inputs,
        IReadOnlyList<IPort> outputs) : INode
    {
        public Guid Id { get; } = id;

        public string TypeId => "test.node";

        public IReadOnlyList<IPort> Inputs { get; } = inputs;

        public IReadOnlyList<IPort> Outputs { get; } = outputs;

        public INodeConfig Config { get; set; } = new TestConfig();

        public ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }
    }
}
