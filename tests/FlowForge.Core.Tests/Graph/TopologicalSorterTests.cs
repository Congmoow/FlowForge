using FluentAssertions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Graph;

namespace FlowForge.Core.Tests.Graph;

public sealed class TopologicalSorterTests
{
    [Fact]
    public void Sort_EmptyWorkflow_ReturnsEmpty()
    {
        var workflow = new Workflow();

        var sortedNodes = TopologicalSorter.Sort(workflow);

        sortedNodes.Should().BeEmpty();
    }

    [Fact]
    public void Sort_LinearChain_ReturnsNodesInDependencyOrder()
    {
        var first = CreateNode(outputs: [new TestPort<string>("output", "输出")]);
        var second = CreateNode(
            inputs: [new TestPort<string>("input", "输入")],
            outputs: [new TestPort<string>("output", "输出")]);
        var third = CreateNode(
            inputs: [new TestPort<string>("input", "输入")],
            outputs: [new TestPort<string>("output", "输出")]);
        var workflow = CreateWorkflow(first, second, third);
        workflow.AddEdge(CreateEdge(first, "output", second, "input"));
        workflow.AddEdge(CreateEdge(second, "output", third, "input"));

        var sortedNodes = TopologicalSorter.Sort(workflow);

        sortedNodes.Should().ContainInOrder(first, second, third);
    }

    [Fact]
    public void Sort_BranchingGraph_ReturnsStableOrderWithinSameLevel()
    {
        var root = CreateNode(outputs: [new TestPort<string>("root", "根输出")]);
        var left = CreateNode(
            inputs: [new TestPort<string>("input", "输入")],
            outputs: [new TestPort<string>("output", "输出")]);
        var right = CreateNode(
            inputs: [new TestPort<string>("input", "输入")],
            outputs: [new TestPort<string>("output", "输出")]);
        var sink = CreateNode(
            inputs: [new TestPort<string>("left", "左输入"), new TestPort<string>("right", "右输入")]);
        var workflow = CreateWorkflow(root, left, right, sink);

        workflow.AddEdge(CreateEdge(root, "root", left, "input"));
        workflow.AddEdge(CreateEdge(root, "root", right, "input"));
        workflow.AddEdge(CreateEdge(left, "output", sink, "left"));
        workflow.AddEdge(CreateEdge(right, "output", sink, "right"));

        var sortedNodes = TopologicalSorter.Sort(workflow);

        sortedNodes.Should().ContainInOrder(root, left, right, sink);
    }

    [Fact]
    public void Sort_WorkflowContainsIsolatedNodes_PreservesWorkflowNodeOrderForParallelNodes()
    {
        var firstIsolated = CreateNode();
        var upstream = CreateNode(outputs: [new TestPort<string>("output", "输出")]);
        var secondIsolated = CreateNode();
        var dependent = CreateNode(inputs: [new TestPort<string>("input", "输入")]);
        var workflow = CreateWorkflow(firstIsolated, upstream, secondIsolated, dependent);
        workflow.AddEdge(CreateEdge(upstream, "output", dependent, "input"));

        var sortedNodes = TopologicalSorter.Sort(workflow);

        sortedNodes.Should().ContainInOrder(firstIsolated, upstream, secondIsolated, dependent);
    }

    [Fact]
    public void Sort_NodeBecomesReadyAfterLaterNode_WaitsForEarlierReadyNodeByWorkflowOrder()
    {
        var first = CreateNode(outputs: [new TestPort<string>("output", "输出")]);
        var second = CreateNode(inputs: [new TestPort<string>("input", "输入")]);
        var third = CreateNode(outputs: [new TestPort<string>("output", "输出")]);
        var fourth = CreateNode(inputs: [new TestPort<string>("input", "输入")]);
        var workflow = CreateWorkflow(first, second, third, fourth);

        workflow.AddEdge(CreateEdge(first, "output", fourth, "input"));
        workflow.AddEdge(CreateEdge(third, "output", second, "input"));

        var sortedNodes = TopologicalSorter.Sort(workflow);

        sortedNodes.Should().ContainInOrder(first, third, second, fourth);
    }

    [Fact]
    public void Sort_SelfLoop_ThrowsCyclicGraphExceptionWithRemainingNodeId()
    {
        var node = CreateNode(
            inputs: [new TestPort<string>("input", "输入")],
            outputs: [new TestPort<string>("output", "输出")]);
        var workflow = CreateWorkflow(node);
        workflow.AddEdge(CreateEdge(node, "output", node, "input"));

        var action = () => TopologicalSorter.Sort(workflow);

        var exception = action.Should().Throw<CyclicGraphException>().Which;
        exception.RemainingNodeIds.Should().Equal(node.Id);
    }

    [Fact]
    public void Sort_MultiNodeCycle_ThrowsCyclicGraphExceptionWithRemainingNodeIdsInWorkflowOrder()
    {
        var isolated = CreateNode();
        var first = CreateNode(
            inputs: [new TestPort<string>("input", "输入")],
            outputs: [new TestPort<string>("output", "输出")]);
        var second = CreateNode(
            inputs: [new TestPort<string>("input", "输入")],
            outputs: [new TestPort<string>("output", "输出")]);
        var third = CreateNode(
            inputs: [new TestPort<string>("input", "输入")],
            outputs: [new TestPort<string>("output", "输出")]);
        var workflow = CreateWorkflow(isolated, first, second, third);

        workflow.AddEdge(CreateEdge(first, "output", second, "input"));
        workflow.AddEdge(CreateEdge(second, "output", third, "input"));
        workflow.AddEdge(CreateEdge(third, "output", first, "input"));

        var action = () => TopologicalSorter.Sort(workflow);

        var exception = action.Should().Throw<CyclicGraphException>().Which;
        exception.RemainingNodeIds.Should().Equal(first.Id, second.Id, third.Id);
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
