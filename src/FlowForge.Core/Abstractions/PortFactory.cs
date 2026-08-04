using FlowForge.Core.Nodes.Internal;

namespace FlowForge.Core.Abstractions;

/// <summary>
/// 为内置节点和外部插件创建强类型端口。
/// </summary>
public static class PortFactory
{
    /// <summary>
    /// 创建一个传输 <typeparamref name="T"/> 类型数据的端口。
    /// </summary>
    /// <typeparam name="T">端口传输的数据类型。</typeparam>
    /// <param name="id">节点范围内稳定且唯一的端口标识。</param>
    /// <param name="name">用于界面展示的端口名称。</param>
    /// <returns>新创建的强类型端口。</returns>
    public static IPort<T> Create<T>(string id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new NodePort<T>(id, name);
    }
}
