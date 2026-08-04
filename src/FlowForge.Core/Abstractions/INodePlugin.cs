namespace FlowForge.Core.Abstractions;

/// <summary>
/// 定义显式提供节点目录项的插件契约。
/// </summary>
public interface INodePlugin
{
    /// <summary>
    /// 获取该插件一次性注册的节点定义集合。
    /// </summary>
    IReadOnlyList<NodeDefinition> Definitions { get; }
}
