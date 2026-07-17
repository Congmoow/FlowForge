using System.Text.Json;

namespace FlowForge.Core.Serialization;

/// <summary>
/// 表示可保存为 .ffw 文件的工作流文档。
/// </summary>
/// <param name="Metadata">工作流元数据。</param>
/// <param name="Nodes">节点文档集合。</param>
/// <param name="Edges">边文档集合。</param>
/// <param name="SchemaVersion">文档结构版本。</param>
public sealed record WorkflowDocument(
    WorkflowMetadata Metadata,
    IReadOnlyList<WorkflowNodeDocument> Nodes,
    IReadOnlyList<WorkflowEdgeDocument> Edges,
    int SchemaVersion = 1);

/// <summary>
/// 表示工作流文档的元数据。
/// </summary>
/// <param name="Name">工作流显示名称。</param>
/// <param name="CreatedAt">工作流创建时间。</param>
/// <param name="FlowForgeVersion">创建该文档的 FlowForge 版本。</param>
public sealed record WorkflowMetadata(string Name, DateTimeOffset CreatedAt, string FlowForgeVersion);

/// <summary>
/// 表示工作流节点的可序列化描述。
/// </summary>
/// <param name="Id">节点稳定标识。</param>
/// <param name="TypeId">节点类型标识。</param>
/// <param name="Position">节点画布位置。</param>
/// <param name="Config">节点特定配置 JSON。</param>
public sealed record WorkflowNodeDocument(Guid Id, string TypeId, WorkflowNodePosition Position, JsonElement Config);

/// <summary>
/// 表示节点在画布中的位置。
/// </summary>
/// <param name="X">横向坐标。</param>
/// <param name="Y">纵向坐标。</param>
public sealed record WorkflowNodePosition(double X, double Y);

/// <summary>
/// 表示工作流中连接两个节点端口的可序列化边。
/// </summary>
/// <param name="Id">边稳定标识。</param>
/// <param name="SourceNodeId">源节点标识。</param>
/// <param name="SourcePortId">源输出端口标识。</param>
/// <param name="TargetNodeId">目标节点标识。</param>
/// <param name="TargetPortId">目标输入端口标识。</param>
public sealed record WorkflowEdgeDocument(
    Guid Id,
    Guid SourceNodeId,
    string SourcePortId,
    Guid TargetNodeId,
    string TargetPortId);

/// <summary>
/// 提供 .ffw 文档使用的 JSON 序列化选项。
/// </summary>
public static class WorkflowJsonSerializerOptions
{
    /// <summary>
    /// 获取使用 camelCase 属性名的默认序列化选项。
    /// </summary>
    public static JsonSerializerOptions Default { get; } = new(JsonSerializerDefaults.Web);
}
