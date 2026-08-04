using FlowForge.Core.Abstractions;
using FlowForge.Core.Graph;

namespace FlowForge.Benchmarks;

internal static class BenchmarkWorkflowFactory
{
    public static Workflow CreateLinearWorkflow(int nodeCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nodeCount);

        var workflow = new Workflow();
        var nodes = new BenchmarkNode[nodeCount];
        for (var index = 0; index < nodeCount; index++)
        {
            nodes[index] = new BenchmarkNode(
                GuidFor(index),
                hasInput: index > 0,
                hasOutput: index < nodeCount - 1);
            workflow.AddNode(nodes[index]);
        }

        for (var index = 0; index < nodeCount - 1; index++)
        {
            workflow.AddEdge(new WorkflowEdge(
                GuidFor(index, kind: 2),
                nodes[index].Id,
                "output",
                nodes[index + 1].Id,
                "input"));
        }

        return workflow;
    }

    private static Guid GuidFor(int index, byte kind = 1)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, index);
        bytes[8] = kind;
        bytes[9] = 0x42;
        bytes[10] = 0x4D;
        bytes[11] = 0x4B;
        return new Guid(bytes);
    }
}

internal sealed class BenchmarkNode : INode
{
    public BenchmarkNode(Guid id, bool hasInput, bool hasOutput)
    {
        Id = id;
        Inputs = hasInput ? [new BenchmarkPort<object>("input")] : [];
        Outputs = hasOutput ? [new BenchmarkPort<object>("output")] : [];
    }

    public Guid Id { get; }

    public string TypeId => "benchmark.node";

    public IReadOnlyList<IPort> Inputs { get; }

    public IReadOnlyList<IPort> Outputs { get; }

    public INodeConfig Config { get; set; } = new BenchmarkNodeConfig();

    public ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }
}

internal sealed record BenchmarkNodeConfig : INodeConfig;

internal sealed class BenchmarkPort<T>(string id) : IPort<T>
{
    public string Id { get; } = id;

    public string Name => Id;

    public Type DataType => typeof(T);
}
