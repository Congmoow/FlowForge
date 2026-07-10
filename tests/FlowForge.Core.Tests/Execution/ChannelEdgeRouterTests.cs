using FluentAssertions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Execution;
using FlowForge.Core.Graph;

namespace FlowForge.Core.Tests.Execution;

public sealed class ChannelEdgeRouterTests
{
    [Fact]
    public async Task WriteAsync_ConnectedPorts_RoutesValueToTargetAsync()
    {
        var sourceOutput = new TestPort<string>("output", "输出");
        var targetInput = new TestPort<string>("input", "输入");
        var source = CreateNode(outputs: [sourceOutput]);
        var target = CreateNode(inputs: [targetInput]);
        var router = CreateRouter(source, target, CreateEdge(source, sourceOutput, target, targetInput));

        await router.CreateExecutionContext(source.Id).WriteAsync(sourceOutput, "hello", CancellationToken.None);
        var value = await router.CreateExecutionContext(target.Id).ReadAsync(targetInput, CancellationToken.None);

        value.Should().Be("hello");
    }

    [Fact]
    public async Task WriteAsync_FanOutOutput_BroadcastsValueToEveryTargetAsync()
    {
        var sourceOutput = new TestPort<string>("output", "输出");
        var firstInput = new TestPort<object>("input", "输入");
        var secondInput = new TestPort<string>("input", "输入");
        var source = CreateNode(outputs: [sourceOutput]);
        var firstTarget = CreateNode(inputs: [firstInput]);
        var secondTarget = CreateNode(inputs: [secondInput]);
        var router = CreateRouter(
            source,
            firstTarget,
            secondTarget,
            CreateEdge(source, sourceOutput, firstTarget, firstInput),
            CreateEdge(source, sourceOutput, secondTarget, secondInput));

        await router.CreateExecutionContext(source.Id).WriteAsync(sourceOutput, "value", CancellationToken.None);

        (await router.CreateExecutionContext(firstTarget.Id).ReadAsync(firstInput, CancellationToken.None)).Should().Be("value");
        (await router.CreateExecutionContext(secondTarget.Id).ReadAsync(secondInput, CancellationToken.None)).Should().Be("value");
    }

    [Fact]
    public async Task ReadAllAsync_CompletedOutput_DrainsValuesAndCompletesAsync()
    {
        var sourceOutput = new TestPort<int>("output", "输出");
        var targetInput = new TestPort<int>("input", "输入");
        var source = CreateNode(outputs: [sourceOutput]);
        var target = CreateNode(inputs: [targetInput]);
        var router = CreateRouter(source, target, CreateEdge(source, sourceOutput, target, targetInput));
        var sourceContext = router.CreateExecutionContext(source.Id);

        await sourceContext.WriteAsync(sourceOutput, 1, CancellationToken.None);
        await sourceContext.WriteAsync(sourceOutput, 2, CancellationToken.None);
        router.CompleteOutputs(source.Id);

        var values = new List<int?>();
        await foreach (var value in router.CreateExecutionContext(target.Id).ReadAllAsync(targetInput, CancellationToken.None))
        {
            values.Add(value);
        }

        values.Should().Equal(1, 2);
    }

    [Fact]
    public async Task ReadAllAsync_FailedOutput_PropagatesOriginalErrorAsync()
    {
        var sourceOutput = new TestPort<string>("output", "输出");
        var targetInput = new TestPort<string>("input", "输入");
        var source = CreateNode(outputs: [sourceOutput]);
        var target = CreateNode(inputs: [targetInput]);
        var router = CreateRouter(source, target, CreateEdge(source, sourceOutput, target, targetInput));
        var expected = new InvalidOperationException("节点失败");
        router.CompleteOutputs(source.Id, expected);

        var action = async () =>
        {
            await foreach (var _ in router.CreateExecutionContext(target.Id).ReadAllAsync(targetInput, CancellationToken.None))
            {
            }
        };

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("节点失败");
    }

    [Fact]
    public async Task ReadAsync_UnconnectedInput_ThrowsExecutionPortExceptionAsync()
    {
        var input = new TestPort<string>("input", "输入");
        var node = CreateNode(inputs: [input]);
        var router = CreateRouter(node);

        var action = async () => await router.CreateExecutionContext(node.Id).ReadAsync(input, CancellationToken.None);

        await action.Should().ThrowAsync<UnconnectedInputPortException>();
    }

    [Fact]
    public async Task WriteAsync_UnconnectedOutput_CompletesWithoutErrorAsync()
    {
        var output = new TestPort<string>("output", "输出");
        var node = CreateNode(outputs: [output]);
        var router = CreateRouter(node);

        var action = async () => await router.CreateExecutionContext(node.Id).WriteAsync(output, "ignored", CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ReadAsync_CancelledToken_ThrowsCancellationAsync()
    {
        var sourceOutput = new TestPort<string>("output", "输出");
        var targetInput = new TestPort<string>("input", "输入");
        var source = CreateNode(outputs: [sourceOutput]);
        var target = CreateNode(inputs: [targetInput]);
        var router = CreateRouter(source, target, CreateEdge(source, sourceOutput, target, targetInput));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = async () => await router.CreateExecutionContext(target.Id).ReadAsync(targetInput, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task WriteAsync_PortFromAnotherNode_ThrowsExecutionPortExceptionAsync()
    {
        var output = new TestPort<string>("output", "输出");
        var foreignOutput = new TestPort<string>("output", "外部输出");
        var node = CreateNode(outputs: [output]);
        var router = CreateRouter(node);

        var action = async () => await router.CreateExecutionContext(node.Id).WriteAsync(foreignOutput, "value", CancellationToken.None);

        await action.Should().ThrowAsync<ExecutionPortException>();
    }

    private static ChannelEdgeRouter CreateRouter(params object[] elements)
    {
        var workflow = new Workflow();
        foreach (var node in elements.OfType<INode>())
        {
            workflow.AddNode(node);
        }

        foreach (var edge in elements.OfType<WorkflowEdge>())
        {
            workflow.AddEdge(edge);
        }

        return new ChannelEdgeRouter(workflow);
    }

    private static TestNode CreateNode(
        IReadOnlyList<IPort>? inputs = null,
        IReadOnlyList<IPort>? outputs = null)
    {
        return new TestNode(Guid.NewGuid(), inputs ?? [], outputs ?? []);
    }

    private static WorkflowEdge CreateEdge(INode source, IPort sourcePort, INode target, IPort targetPort)
    {
        return new WorkflowEdge(Guid.NewGuid(), source.Id, sourcePort.Id, target.Id, targetPort.Id);
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
