using System.Collections.Concurrent;
using System.Diagnostics;
using FluentAssertions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Execution;
using FlowForge.Core.Graph;

namespace FlowForge.Core.Tests.Execution;

public sealed class WorkflowSchedulerTests
{
    [Fact]
    public async Task ExecuteAsync_EmptyWorkflow_CompletesWithoutEventsAsync()
    {
        var progress = new RecordingProgress();

        await new WorkflowScheduler().ExecuteAsync(new Workflow(), progress, CancellationToken.None);

        progress.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_StreamingEdge_StartsDownstreamBeforeSourceCompletesAsync()
    {
        var output = new TestPort<int>("output", "输出");
        var input = new TestPort<int>("input", "输入");
        var releaseSource = CreateCompletionSource();
        var targetReceived = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new ScriptedNode(
            outputs: [output],
            execute: async (context, ct) =>
            {
                await context.WriteAsync(output, 42, ct);
                await releaseSource.Task.WaitAsync(ct);
            });
        var target = new ScriptedNode(
            inputs: [input],
            execute: async (context, ct) =>
            {
                targetReceived.TrySetResult(await context.ReadAsync(input, ct));
            });
        var workflow = CreateWorkflow(source, target);
        workflow.AddEdge(CreateEdge(source, output, target, input));

        var execution = new WorkflowScheduler().ExecuteAsync(workflow, progress: null, CancellationToken.None);
        var received = await targetReceived.Task.WaitAsync(TimeSpan.FromSeconds(1));

        received.Should().Be(42);
        execution.IsCompleted.Should().BeFalse();

        releaseSource.TrySetResult();
        await execution;
    }

    [Fact]
    public async Task ExecuteAsync_CyclicWorkflow_ThrowsBeforeStartingAnyNodeAsync()
    {
        var firstInput = new TestPort<string>("input", "输入");
        var firstOutput = new TestPort<string>("output", "输出");
        var secondInput = new TestPort<string>("input", "输入");
        var secondOutput = new TestPort<string>("output", "输出");
        var first = new ScriptedNode(inputs: [firstInput], outputs: [firstOutput]);
        var second = new ScriptedNode(inputs: [secondInput], outputs: [secondOutput]);
        var workflow = CreateWorkflow(first, second);
        workflow.AddEdge(CreateEdge(first, firstOutput, second, secondInput));
        workflow.AddEdge(CreateEdge(second, secondOutput, first, firstInput));

        var action = async () => await new WorkflowScheduler().ExecuteAsync(workflow, progress: null, CancellationToken.None);

        await action.Should().ThrowAsync<CyclicGraphException>();
        first.ExecutionCount.Should().Be(0);
        second.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_NodeFailure_ThrowsRootFailureAndCancelsSiblingAsync()
    {
        var siblingStarted = CreateCompletionSource();
        var siblingCancelled = CreateCompletionSource();
        var expected = new InvalidOperationException("boom");
        var failing = new ScriptedNode(execute: async (_, ct) =>
        {
            await siblingStarted.Task.WaitAsync(ct);
            throw expected;
        });
        var sibling = new ScriptedNode(execute: async (_, ct) =>
        {
            siblingStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException)
            {
                siblingCancelled.TrySetResult();
                throw;
            }
        });
        var workflow = CreateWorkflow(failing, sibling);

        var action = async () => await new WorkflowScheduler().ExecuteAsync(workflow, progress: null, CancellationToken.None);

        var exception = (await action.Should().ThrowAsync<WorkflowExecutionException>()).Which;
        exception.NodeId.Should().Be(failing.Id);
        exception.InnerException.Should().BeSameAs(expected);
        await siblingCancelled.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_ExternalCancellation_StopsCooperativeNodeWithinBudgetAsync()
    {
        var started = CreateCompletionSource();
        var node = new ScriptedNode(execute: async (_, ct) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        });
        var workflow = CreateWorkflow(node);
        using var cancellation = new CancellationTokenSource();
        var execution = new WorkflowScheduler().ExecuteAsync(workflow, progress: null, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var stopwatch = Stopwatch.StartNew();
        cancellation.Cancel();
        var action = async () => await execution;

        await action.Should().ThrowAsync<OperationCanceledException>();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulNode_ReportsRunningThenSucceededAsync()
    {
        var node = new ScriptedNode();
        var progress = new RecordingProgress();

        await new WorkflowScheduler().ExecuteAsync(CreateWorkflow(node), progress, CancellationToken.None);

        progress.Events.Should().Equal(
            new NodeExecutionEvent(node.Id, NodeExecutionStatus.Running),
            new NodeExecutionEvent(node.Id, NodeExecutionStatus.Succeeded));
    }

    [Fact]
    public async Task ExecuteAsync_FailingNode_ReportsRunningThenFailedAsync()
    {
        var expected = new InvalidOperationException("失败");
        var node = new ScriptedNode(execute: (_, _) => ValueTask.FromException(expected));
        var progress = new RecordingProgress();

        var action = async () => await new WorkflowScheduler().ExecuteAsync(CreateWorkflow(node), progress, CancellationToken.None);

        await action.Should().ThrowAsync<WorkflowExecutionException>();
        progress.Events.Should().HaveCount(2);
        progress.Events[0].Should().Be(new NodeExecutionEvent(node.Id, NodeExecutionStatus.Running));
        progress.Events[1].NodeId.Should().Be(node.Id);
        progress.Events[1].Status.Should().Be(NodeExecutionStatus.Failed);
        progress.Events[1].Error.Should().BeSameAs(expected);
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

    private static WorkflowEdge CreateEdge(INode source, IPort sourcePort, INode target, IPort targetPort)
    {
        return new WorkflowEdge(Guid.NewGuid(), source.Id, sourcePort.Id, target.Id, targetPort.Id);
    }

    private static TaskCompletionSource CreateCompletionSource()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class RecordingProgress : IProgress<NodeExecutionEvent>
    {
        private readonly ConcurrentQueue<NodeExecutionEvent> _events = new();

        public IReadOnlyList<NodeExecutionEvent> Events => _events.ToArray();

        public void Report(NodeExecutionEvent value)
        {
            _events.Enqueue(value);
        }
    }

    private sealed record TestConfig : INodeConfig;

    private sealed class TestPort<T>(string id, string name) : IPort<T>
    {
        public string Id { get; } = id;

        public string Name { get; } = name;

        public Type DataType => typeof(T);
    }

    private sealed class ScriptedNode(
        IReadOnlyList<IPort>? inputs = null,
        IReadOnlyList<IPort>? outputs = null,
        Func<IExecutionContext, CancellationToken, ValueTask>? execute = null) : INode
    {
        private readonly Func<IExecutionContext, CancellationToken, ValueTask> _execute =
            execute ?? ((_, _) => ValueTask.CompletedTask);
        private int _executionCount;

        public Guid Id { get; } = Guid.NewGuid();

        public string TypeId => "test.scripted";

        public IReadOnlyList<IPort> Inputs { get; } = inputs ?? [];

        public IReadOnlyList<IPort> Outputs { get; } = outputs ?? [];

        public INodeConfig Config { get; set; } = new TestConfig();

        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
        {
            Interlocked.Increment(ref _executionCount);
            return _execute(ctx, ct);
        }
    }
}
