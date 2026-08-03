using System.Buffers.Binary;
using FlowForge.App.ViewModels;

namespace FlowForge.App.Diagnostics;

/// <summary>
/// 由固定节点数量和种子生成可重复的画布压力场景。
/// </summary>
public static class StressScenarioGenerator
{
    private const double ColumnSpacing = 280;
    private const double RowSpacing = 140;

    /// <summary>支持的性能场景节点数量。</summary>
    public static IReadOnlyList<int> SupportedNodeCounts { get; } = [100, 250, 500, 1000];

    /// <summary>
    /// 创建确定性的节点链场景。
    /// </summary>
    /// <param name="nodeCount">节点数量。</param>
    /// <param name="seed">用于生成坐标和稳定标识的种子。</param>
    /// <returns>包含节点和边的压力场景。</returns>
    public static StressScenario Create(int nodeCount, int seed = PerfSamplingOptions.DefaultSeed)
    {
        if (!SupportedNodeCounts.Contains(nodeCount))
        {
            throw new ArgumentOutOfRangeException(nameof(nodeCount), "节点数量必须是 100、250、500 或 1000。");
        }

        var canvas = new CanvasViewModel();
        var nodes = new NodeViewModel[nodeCount];
        var random = new Random(seed);
        var columns = (int)Math.Ceiling(Math.Sqrt(nodeCount));

        for (var index = 0; index < nodeCount; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var jitterX = (random.NextDouble() - 0.5) * 24;
            var jitterY = (random.NextDouble() - 0.5) * 24;
            var node = new NodeViewModel(
                CreateStableId(seed, index, 1),
                "perf.stress.node",
                $"压力节点 {index + 1}",
                48 + column * ColumnSpacing + jitterX,
                48 + row * RowSpacing + jitterY);
            node.Inputs.Add(new PortViewModel(node, "input", "输入", PortDirection.Input, typeof(object), 0));
            node.Outputs.Add(new PortViewModel(node, "output", "输出", PortDirection.Output, typeof(object), 0));
            nodes[index] = node;
            canvas.Nodes.Add(node);
        }

        var edges = new EdgeViewModel[nodeCount - 1];
        for (var index = 0; index < edges.Length; index++)
        {
            edges[index] = new EdgeViewModel(
                CreateStableId(seed, index, 2),
                nodes[index].Outputs[0],
                nodes[index + 1].Inputs[0]);
            canvas.Edges.Add(edges[index]);
        }

        return new StressScenario(seed, canvas, nodes, edges);
    }

    private static Guid CreateStableId(int seed, int index, byte kind)
    {
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, seed);
        BinaryPrimitives.WriteInt32LittleEndian(bytes[4..], index);
        bytes[8] = kind;
        bytes[9] = 0x46;
        bytes[10] = 0x46;
        bytes[11] = 0x50;
        return new Guid(bytes);
    }
}

/// <summary>
/// 由压力场景生成器创建的画布快照。
/// </summary>
public sealed class StressScenario
{
    internal StressScenario(
        int seed,
        CanvasViewModel canvas,
        IReadOnlyList<NodeViewModel> nodes,
        IReadOnlyList<EdgeViewModel> edges)
    {
        Seed = seed;
        Canvas = canvas;
        Nodes = nodes;
        Edges = edges;
    }

    /// <summary>场景使用的确定性种子。</summary>
    public int Seed { get; }

    /// <summary>可直接绑定到画布控件的 ViewModel。</summary>
    public CanvasViewModel Canvas { get; }

    /// <summary>场景节点的只读视图。</summary>
    public IReadOnlyList<NodeViewModel> Nodes { get; }

    /// <summary>场景连线的只读视图。</summary>
    public IReadOnlyList<EdgeViewModel> Edges { get; }
}
