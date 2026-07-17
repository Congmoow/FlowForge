using System.Text.Json;
using FlowForge.Core.Graph;

namespace FlowForge.Core.Serialization;

/// <summary>
/// 提供 .ffw 工作流文档与领域工作流之间的转换和异步读写。
/// </summary>
public static class WorkflowSerializer
{
    /// <summary>
    /// 将工作流文档异步写入目标流。
    /// </summary>
    /// <param name="document">要保存的工作流文档。</param>
    /// <param name="stream">接收 UTF-8 JSON 的可写流。</param>
    /// <param name="cancellationToken">用于取消写入的令牌。</param>
    /// <returns>表示保存过程的异步任务。</returns>
    public static Task SaveAsync(WorkflowDocument document, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(stream);
        return JsonSerializer.SerializeAsync(stream, document, WorkflowJsonSerializerOptions.Default, cancellationToken);
    }

    /// <summary>
    /// 从 JSON 流异步读取工作流文档。
    /// </summary>
    /// <param name="stream">提供 UTF-8 JSON 的可读流。</param>
    /// <param name="cancellationToken">用于取消读取的令牌。</param>
    /// <returns>读取到的工作流文档。</returns>
    public static async Task<WorkflowDocument> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var document = await JsonSerializer.DeserializeAsync<WorkflowDocument>(
            stream,
            WorkflowJsonSerializerOptions.Default,
            cancellationToken).ConfigureAwait(false);
        return document ?? throw new JsonException("工作流文档不能为空。");
    }

    /// <summary>
    /// 将领域工作流转换为可保存的文档。
    /// </summary>
    /// <param name="workflow">要转换的领域工作流。</param>
    /// <param name="positions">按节点标识提供的画布位置。</param>
    /// <param name="metadata">要保存的文档元数据。</param>
    /// <returns>包含节点、边、位置和配置的工作流文档。</returns>
    public static WorkflowDocument CreateDocument(
        Workflow workflow,
        IReadOnlyDictionary<Guid, WorkflowNodePosition> positions,
        WorkflowMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(metadata);

        var nodes = workflow.Nodes.Select(node => new WorkflowNodeDocument(
            node.Id,
            node.TypeId,
            GetPosition(node.Id, positions),
            JsonSerializer.SerializeToElement(node.Config, node.Config.GetType(), WorkflowJsonSerializerOptions.Default))).ToArray();
        var edges = workflow.Edges.Select(edge => new WorkflowEdgeDocument(
            edge.Id,
            edge.SourceNodeId,
            edge.SourcePortId,
            edge.TargetNodeId,
            edge.TargetPortId)).ToArray();
        return new WorkflowDocument(metadata, nodes, edges);
    }

    /// <summary>
    /// 使用节点注册表从文档重建领域工作流。
    /// </summary>
    /// <param name="document">要重建的工作流文档。</param>
    /// <param name="registry">负责构造节点的注册表。</param>
    /// <returns>已校验并恢复节点和边的领域工作流。</returns>
    public static Workflow CreateWorkflow(WorkflowDocument document, NodeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(registry);

        if (document.SchemaVersion != 1)
        {
            throw new NotSupportedException($"不支持工作流 schema 版本 {document.SchemaVersion}。");
        }

        var workflow = new Workflow();
        foreach (var node in document.Nodes)
        {
            workflow.AddNode(registry.Create(node));
        }

        foreach (var edge in document.Edges)
        {
            workflow.AddEdge(new WorkflowEdge(
                edge.Id,
                edge.SourceNodeId,
                edge.SourcePortId,
                edge.TargetNodeId,
                edge.TargetPortId));
        }

        return workflow;
    }

    private static WorkflowNodePosition GetPosition(
        Guid nodeId,
        IReadOnlyDictionary<Guid, WorkflowNodePosition> positions)
    {
        return positions.TryGetValue(nodeId, out var position)
            ? position
            : throw new InvalidOperationException($"节点 {nodeId} 缺少画布位置。");
    }
}
