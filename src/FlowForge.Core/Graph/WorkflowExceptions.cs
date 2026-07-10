namespace FlowForge.Core.Graph;

/// <summary>
/// 表示工作流元素标识发生重复。
/// </summary>
public sealed class DuplicateWorkflowElementException : InvalidOperationException
{
    /// <summary>
    /// 使用指定错误消息初始化异常。
    /// </summary>
    /// <param name="message">描述重复元素的错误消息。</param>
    public DuplicateWorkflowElementException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// 表示工作流边引用了不存在的节点或端口。
/// </summary>
public sealed class WorkflowReferenceException : InvalidOperationException
{
    /// <summary>
    /// 使用指定错误消息初始化异常。
    /// </summary>
    /// <param name="message">描述无效引用的错误消息。</param>
    public WorkflowReferenceException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// 表示工作流边使用了方向错误的端口。
/// </summary>
public sealed class PortDirectionException : InvalidOperationException
{
    /// <summary>
    /// 使用指定错误消息初始化异常。
    /// </summary>
    /// <param name="message">描述端口方向错误的消息。</param>
    public PortDirectionException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// 表示连接两端的端口数据类型不兼容。
/// </summary>
public sealed class PortTypeMismatchException : InvalidOperationException
{
    /// <summary>
    /// 使用指定错误消息初始化异常。
    /// </summary>
    /// <param name="message">描述类型不兼容的错误消息。</param>
    public PortTypeMismatchException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// 表示目标输入端口已经存在连接。
/// </summary>
public sealed class InputPortAlreadyConnectedException : InvalidOperationException
{
    /// <summary>
    /// 使用指定错误消息初始化异常。
    /// </summary>
    /// <param name="message">描述输入端口连接冲突的错误消息。</param>
    public InputPortAlreadyConnectedException(string message)
        : base(message)
    {
    }
}
