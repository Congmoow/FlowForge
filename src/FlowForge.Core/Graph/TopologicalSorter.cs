using FlowForge.Core.Abstractions;

namespace FlowForge.Core.Graph;

/// <summary>
/// 提供工作流节点的稳定拓扑排序功能。
/// </summary>
public static class TopologicalSorter
{
    /// <summary>
    /// 使用 Kahn 算法对工作流节点进行稳定拓扑排序。
    /// </summary>
    /// <param name="workflow">要排序的工作流。</param>
    /// <returns>按依赖关系排序的只读节点列表；并列节点按其在工作流中的加入顺序排列。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="workflow"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="CyclicGraphException">工作流包含环。</exception>
    public static IReadOnlyList<INode> Sort(Workflow workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var nodes = workflow.Nodes;
        var nodeIndexes = new Dictionary<Guid, int>(nodes.Count);
        var incomingEdgeCounts = new int[nodes.Count];
        var successors = new List<int>[nodes.Count];

        for (var index = 0; index < nodes.Count; index++)
        {
            nodeIndexes.Add(nodes[index].Id, index);
            successors[index] = [];
        }

        foreach (var edge in workflow.Edges)
        {
            var sourceIndex = nodeIndexes[edge.SourceNodeId];
            var targetIndex = nodeIndexes[edge.TargetNodeId];
            successors[sourceIndex].Add(targetIndex);
            incomingEdgeCounts[targetIndex]++;
        }

        var readyNodeIndexes = new PriorityQueue<int, int>();
        for (var index = 0; index < nodes.Count; index++)
        {
            if (incomingEdgeCounts[index] == 0)
            {
                readyNodeIndexes.Enqueue(index, index);
            }
        }

        var sortedNodes = new List<INode>(nodes.Count);
        while (readyNodeIndexes.TryDequeue(out var nodeIndex, out _))
        {
            sortedNodes.Add(nodes[nodeIndex]);

            foreach (var successorIndex in successors[nodeIndex])
            {
                incomingEdgeCounts[successorIndex]--;
                if (incomingEdgeCounts[successorIndex] == 0)
                {
                    readyNodeIndexes.Enqueue(successorIndex, successorIndex);
                }
            }
        }

        if (sortedNodes.Count != nodes.Count)
        {
            var remainingNodeIds = nodes
                .Where((_, index) => incomingEdgeCounts[index] > 0)
                .Select(node => node.Id);

            throw new CyclicGraphException(remainingNodeIds);
        }

        return sortedNodes.AsReadOnly();
    }
}
