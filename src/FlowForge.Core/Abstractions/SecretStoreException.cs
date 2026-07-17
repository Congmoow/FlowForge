namespace FlowForge.Core.Abstractions;

/// <summary>
/// 表示密钥存储或读取过程中的领域失败。
/// </summary>
public sealed class SecretStoreException : InvalidOperationException
{
    /// <summary>
    /// 使用指定错误消息初始化异常。
    /// </summary>
    /// <param name="message">说明密钥存储失败原因的消息。</param>
    public SecretStoreException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 使用指定错误消息和内部异常初始化异常。
    /// </summary>
    /// <param name="message">说明密钥存储失败原因的消息。</param>
    /// <param name="innerException">导致当前异常的底层异常。</param>
    public SecretStoreException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
