using BenchmarkDotNet.Attributes;
using FlowForge.Core.Graph;

namespace FlowForge.Benchmarks;

/// <summary>
/// 测量不同规模线性 DAG 的拓扑排序开销。
/// </summary>
[MemoryDiagnoser]
public class TopologicalSortBenchmarks
{
    private FlowForge.Core.Graph.Workflow workflow = null!;

    /// <summary>当前基准的节点数量。</summary>
    [Params(100, 1000)]
    public int NodeCount { get; set; }

    /// <summary>
    /// 仅构造一次待测 DAG；图构造不计入 Benchmark 方法。
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        workflow = BenchmarkWorkflowFactory.CreateLinearWorkflow(NodeCount);
    }

    /// <summary>
    /// 执行真实的核心拓扑排序实现。
    /// </summary>
    /// <returns>排序后的节点列表。</returns>
    [Benchmark]
    public IReadOnlyList<FlowForge.Core.Abstractions.INode> Sort()
    {
        return TopologicalSorter.Sort(workflow);
    }
}
