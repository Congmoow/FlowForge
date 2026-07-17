using System.Text.Json;
using FlowForge.Core.Abstractions;

namespace FlowForge.Core.Serialization;

/// <summary>
/// 根据稳定类型标识创建工作流节点。
/// </summary>
public sealed class NodeRegistry
{
    private readonly Dictionary<string, Func<Guid, JsonElement, INode>> _factories = new(StringComparer.Ordinal);

    /// <summary>
    /// 注册一个节点类型的创建工厂。
    /// </summary>
    /// <param name="typeId">节点稳定类型标识。</param>
    /// <param name="factory">接收节点标识和配置 JSON 的创建工厂。</param>
    /// <exception cref="InvalidOperationException">类型标识已注册。</exception>
    public void Register(string typeId, Func<Guid, JsonElement, INode> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        ArgumentNullException.ThrowIfNull(factory);

        if (!_factories.TryAdd(typeId, factory))
        {
            throw new InvalidOperationException($"节点类型 {typeId} 已注册。");
        }
    }

    /// <summary>
    /// 根据节点文档创建节点实例。
    /// </summary>
    /// <param name="document">要转换的节点文档。</param>
    /// <returns>使用文档标识和配置创建的节点。</returns>
    /// <exception cref="InvalidOperationException">节点类型未注册。</exception>
    public INode Create(WorkflowNodeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!_factories.TryGetValue(document.TypeId, out var factory))
        {
            throw new InvalidOperationException($"节点类型 {document.TypeId} 未注册。");
        }

        return factory(document.Id, document.Config);
    }
}
