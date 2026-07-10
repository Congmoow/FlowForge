namespace FlowForge.Core.Execution;

/// <summary>
/// 表示节点执行期间访问了无效端口。
/// </summary>
public class ExecutionPortException : InvalidOperationException
{
    /// <summary>
    /// 使用指定错误消息初始化异常。
    /// </summary>
    /// <param name="message">描述端口错误的消息。</param>
    public ExecutionPortException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// 表示执行上下文尝试读取未连接的输入端口。
/// </summary>
public sealed class UnconnectedInputPortException : ExecutionPortException
{
    /// <summary>
    /// 使用节点与端口标识初始化异常。
    /// </summary>
    /// <param name="nodeId">节点标识。</param>
    /// <param name="portId">端口标识。</param>
    public UnconnectedInputPortException(Guid nodeId, string portId)
        : base($"输入端口 {nodeId}/{portId} 未连接。")
    {
    }
}

/// <summary>
/// 表示 Channel 中的运行时值与声明端口类型不兼容。
/// </summary>
public sealed class PortValueTypeMismatchException : ExecutionPortException
{
    /// <summary>
    /// 使用端口和实际值类型初始化异常。
    /// </summary>
    /// <param name="nodeId">节点标识。</param>
    /// <param name="portId">端口标识。</param>
    /// <param name="expectedType">声明的数据类型。</param>
    /// <param name="actualType">实际值类型。</param>
    public PortValueTypeMismatchException(Guid nodeId, string portId, Type expectedType, Type actualType)
        : base($"端口 {nodeId}/{portId} 需要 {expectedType}，实际收到 {actualType}。")
    {
    }
}
