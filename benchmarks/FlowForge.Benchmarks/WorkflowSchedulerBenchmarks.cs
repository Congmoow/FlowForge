using BenchmarkDotNet.Attributes;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Execution;
using FlowForge.Core.Graph;

namespace FlowForge.Benchmarks;

/// <summary>
/// 测量真实工作流调度器和每边 Channel 路由的异步执行开销。
/// </summary>
[MemoryDiagnoser]
public class WorkflowSchedulerBenchmarks
{
    private Workflow workflow = null!;

    /// <summary>当前基准的节点数量。</summary>
    [Params(10, 100, 1000)]
    public int NodeCount { get; set; }

    /// <summary>
    /// 为每次测量迭代创建独立的内存工作流。
    /// </summary>
    [IterationSetup]
    public void IterationSetup()
    {
        workflow = SchedulerBenchmarkWorkflowFactory.CreateLinearWorkflow(NodeCount);
    }

    /// <summary>
    /// 通过真实 WorkflowScheduler 执行当前内存工作流。
    /// </summary>
    /// <returns>表示完整工作流执行过程的任务。</returns>
    [Benchmark]
    public Task ExecuteAsync()
    {
        return new WorkflowScheduler().ExecuteAsync(
            workflow,
            progress: null,
            CancellationToken.None);
    }
}

internal static class SchedulerBenchmarkWorkflowFactory
{
    public static Workflow CreateLinearWorkflow(int nodeCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nodeCount);

        var workflow = new Workflow();
        var nodes = new SchedulerBenchmarkNode[nodeCount];
        for (var index = 0; index < nodeCount; index++)
        {
            nodes[index] = new SchedulerBenchmarkNode(
                GetId(index, kind: 1),
                hasInput: index > 0,
                hasOutput: index < nodeCount - 1);
            workflow.AddNode(nodes[index]);
        }

        for (var index = 0; index < nodeCount - 1; index++)
        {
            workflow.AddEdge(new WorkflowEdge(
                GetId(index, kind: 2),
                nodes[index].Id,
                "output",
                nodes[index + 1].Id,
                "input"));
        }

        return workflow;
    }

    private static Guid GetId(int index, byte kind)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, index);
        bytes[8] = kind;
        bytes[9] = 0x53;
        bytes[10] = 0x43;
        bytes[11] = 0x48;
        return new Guid(bytes);
    }
}

internal sealed class SchedulerBenchmarkNode : INode
{
    private readonly SchedulerBenchmarkPort<int>? input;
    private readonly SchedulerBenchmarkPort<int>? output;

    public SchedulerBenchmarkNode(Guid id, bool hasInput, bool hasOutput)
    {
        Id = id;
        input = hasInput ? new SchedulerBenchmarkPort<int>("input") : null;
        output = hasOutput ? new SchedulerBenchmarkPort<int>("output") : null;
        Inputs = input is null ? [] : [input];
        Outputs = output is null ? [] : [output];
    }

    public Guid Id { get; }

    public string TypeId => "benchmark.scheduler-node";

    public IReadOnlyList<IPort> Inputs { get; }

    public IReadOnlyList<IPort> Outputs { get; }

    public INodeConfig Config { get; set; } = new SchedulerBenchmarkNodeConfig();

    public async ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
    {
        if (input is not null)
        {
            await ctx.ReadAsync(input, ct).ConfigureAwait(false);
        }

        if (output is not null)
        {
            await ctx.WriteAsync(output, 1, ct).ConfigureAwait(false);
        }
    }
}

internal sealed record SchedulerBenchmarkNodeConfig : INodeConfig;

internal sealed class SchedulerBenchmarkPort<T>(string id) : IPort<T>
{
    public string Id { get; } = id;

    public string Name => Id;

    public Type DataType => typeof(T);
}
