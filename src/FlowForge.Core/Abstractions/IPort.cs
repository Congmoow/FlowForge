namespace FlowForge.Core.Abstractions;

/// <summary>
/// 表示节点上具有稳定标识和运行时数据类型的端口。
/// </summary>
public interface IPort
{
    /// <summary>
    /// 获取节点类型范围内稳定且唯一的端口标识。
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 获取用于界面展示的端口名称。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 获取端口传输的数据类型。
    /// </summary>
    Type DataType { get; }
}

/// <summary>
/// 表示传输 <typeparamref name="T"/> 类型数据的强类型端口。
/// </summary>
/// <typeparam name="T">端口传输的数据类型。</typeparam>
public interface IPort<T> : IPort
{
}
