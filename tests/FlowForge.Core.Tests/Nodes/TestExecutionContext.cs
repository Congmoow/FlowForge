using FlowForge.Core.Abstractions;

namespace FlowForge.Core.Tests.Nodes;

internal sealed class TestExecutionContext : IExecutionContext
{
    private readonly Dictionary<IPort, object?> _inputs = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IPort, object?> _outputs = new(ReferenceEqualityComparer.Instance);

    public IPort? LastReadPort { get; private set; }

    public CancellationToken LastReadCancellationToken { get; private set; }

    public IPort? LastWritePort { get; private set; }

    public CancellationToken LastWriteCancellationToken { get; private set; }

    public int WriteCount { get; private set; }

    public void SetInput<T>(IPort<T> port, T? value)
    {
        _inputs[port] = value;
    }

    public T? GetOutput<T>(IPort<T> port)
    {
        if (!_outputs.TryGetValue(port, out var value))
        {
            throw new InvalidOperationException($"端口 {port.Id} 尚未写入输出。");
        }

        return (T?)value;
    }

    public ValueTask<T?> ReadAsync<T>(IPort<T> port, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LastReadPort = port;
        LastReadCancellationToken = ct;

        if (!_inputs.TryGetValue(port, out var value))
        {
            throw new InvalidOperationException($"端口 {port.Id} 尚未设置输入。");
        }

        return ValueTask.FromResult((T?)value);
    }

    public IAsyncEnumerable<T?> ReadAllAsync<T>(IPort<T> port, CancellationToken ct)
    {
        throw new NotSupportedException("当前节点测试不使用流式输入。");
    }

    public ValueTask WriteAsync<T>(IPort<T> port, T? value, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LastWritePort = port;
        LastWriteCancellationToken = ct;
        WriteCount++;
        _outputs[port] = value;
        return ValueTask.CompletedTask;
    }
}
