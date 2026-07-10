namespace FlowForge.Core.Abstractions;

/// <summary>
/// 提供节点执行期间读取输入和写入输出的上下文。
/// </summary>
public interface IExecutionContext
{
    /// <summary>
    /// 从指定输入端口异步读取一个值。
    /// </summary>
    /// <typeparam name="T">输入值类型。</typeparam>
    /// <param name="port">要读取的输入端口。</param>
    /// <param name="ct">用于取消读取操作的令牌。</param>
    /// <returns>读取到的值。</returns>
    ValueTask<T?> ReadAsync<T>(IPort<T> port, CancellationToken ct);

    /// <summary>
    /// 从指定输入端口异步读取完整数据流。
    /// </summary>
    /// <typeparam name="T">输入值类型。</typeparam>
    /// <param name="port">要读取的输入端口。</param>
    /// <param name="ct">用于取消枚举的令牌。</param>
    /// <returns>输入端口产生的异步数据流。</returns>
    IAsyncEnumerable<T?> ReadAllAsync<T>(IPort<T> port, CancellationToken ct);

    /// <summary>
    /// 向指定输出端口异步写入一个值。
    /// </summary>
    /// <typeparam name="T">输出值类型。</typeparam>
    /// <param name="port">要写入的输出端口。</param>
    /// <param name="value">要写入的值。</param>
    /// <param name="ct">用于取消写入操作的令牌。</param>
    /// <returns>表示写入操作的异步结果。</returns>
    ValueTask WriteAsync<T>(IPort<T> port, T? value, CancellationToken ct);
}
