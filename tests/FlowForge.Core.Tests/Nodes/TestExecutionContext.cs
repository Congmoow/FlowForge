using FlowForge.Core.Abstractions;
using System.Runtime.CompilerServices;

namespace FlowForge.Core.Tests.Nodes;

internal sealed class TestExecutionContext : IExecutionContext
{
    private readonly Dictionary<IPort, object?> _inputs = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IPort, IReadOnlyList<object?>> _inputStreams = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<IPort, object?> _outputs = new(ReferenceEqualityComparer.Instance);

    public IPort? LastReadPort { get; private set; }

    public CancellationToken LastReadCancellationToken { get; private set; }

    public IPort? LastWritePort { get; private set; }

    public CancellationToken LastWriteCancellationToken { get; private set; }

    public int WriteCount { get; private set; }

    public void SetInput<T>(IPort<T> port, T? value)
    {
        _inputs[port] = value;
        _inputStreams[port] = [value];
    }

    public void SetInputStream<T>(IPort<T> port, params T?[] values)
    {
        _inputs[port] = values.FirstOrDefault();
        _inputStreams[port] = values.Cast<object?>().ToArray();
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

    public async IAsyncEnumerable<T?> ReadAllAsync<T>(
        IPort<T> port,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LastReadPort = port;
        LastReadCancellationToken = ct;

        if (!_inputStreams.TryGetValue(port, out var values))
        {
            throw new InvalidOperationException($"端口 {port.Id} 尚未设置输入流。");
        }

        foreach (var value in values)
        {
            ct.ThrowIfCancellationRequested();
            yield return (T?)value;
            await Task.Yield();
        }
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
