using System.Runtime.CompilerServices;
using System.Text;
using FlowForge.Core.Abstractions;
using FluentAssertions;
using FlowForge.Core.Nodes.Sink;

namespace FlowForge.Core.Tests.Nodes.Sink;

public sealed class FileWriterSinkNodeTests
{
    [Fact]
    public void Constructor_ConfigAndPorts_ExposeStableContract()
    {
        var id = Guid.NewGuid();
        var config = new FileWriterSinkNodeConfig("output.txt", Append: true);

        var node = new FileWriterSinkNode(id, config);

        node.Id.Should().Be(id);
        node.TypeId.Should().Be("core.sink.file-writer");
        node.Inputs.Should().ContainSingle().Which.Should().BeSameAs(node.ContentInput);
        node.Outputs.Should().BeEmpty();
        node.ContentInput.Id.Should().Be("content");
        node.ContentInput.DataType.Should().Be(typeof(string));
        node.Config.Should().BeSameAs(config);
        (config with { Append = false }).Append.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_MultipleValues_OpensOnceAndWritesOriginalTextWithoutBomOrNewlineAsync()
    {
        var directory = Directory.CreateTempSubdirectory("flowforge-writer-");
        try
        {
            var path = Path.Combine(directory.FullName, "output.txt");
            var node = new FileWriterSinkNode(new FileWriterSinkNodeConfig(path));
            var context = new TestExecutionContext();
            context.SetInputStream(node.ContentInput, "第一", "\n", "第二");

            await node.ExecuteAsync(context, CancellationToken.None);

            var bytes = await File.ReadAllBytesAsync(path);
            (bytes.Length >= 3 && bytes[..3].SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF })).Should().BeFalse();
            Encoding.UTF8.GetString(bytes).Should().Be("第一\n第二");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_MultipleValues_HoldsOneWriteHandleForEntireStreamAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Directory.CreateTempSubdirectory("flowforge-writer-");
        try
        {
            var path = Path.Combine(directory.FullName, "output.txt");
            var node = new FileWriterSinkNode(new FileWriterSinkNodeConfig(path));
            var context = new BlockingStreamExecutionContext(node.ContentInput);
            var execution = node.ExecuteAsync(context, CancellationToken.None).AsTask();

            try
            {
                await context.FirstValueConsumed.Task.WaitAsync(TimeSpan.FromSeconds(3));

                var openAttempt = () =>
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read);
                };
                openAttempt.Should().Throw<IOException>();
            }
            finally
            {
                context.ReleaseSecondValue.TrySetResult();
                await execution;
            }

            (await File.ReadAllTextAsync(path, new UTF8Encoding(false))).Should().Be("第一第二");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_AppendMode_PreservesExistingContentAndAppendsValuesAsync()
    {
        var directory = Directory.CreateTempSubdirectory("flowforge-writer-");
        try
        {
            var path = Path.Combine(directory.FullName, "output.txt");
            await File.WriteAllTextAsync(path, "已有", new UTF8Encoding(false));
            var node = new FileWriterSinkNode(new FileWriterSinkNodeConfig(path, Append: true));
            var context = new TestExecutionContext();
            context.SetInputStream(node.ContentInput, "追加", "内容");

            await node.ExecuteAsync(context, CancellationToken.None);

            (await File.ReadAllTextAsync(path, new UTF8Encoding(false))).Should().Be("已有追加内容");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_CancelledToken_StopsBeforeOpeningFileAsync()
    {
        var directory = Directory.CreateTempSubdirectory("flowforge-writer-");
        try
        {
            var path = Path.Combine(directory.FullName, "output.txt");
            var node = new FileWriterSinkNode(new FileWriterSinkNodeConfig(path));
            var context = new TestExecutionContext();
            context.SetInputStream(node.ContentInput, "不会写入");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var action = async () => await node.ExecuteAsync(context, cancellation.Token);

            await action.Should().ThrowAsync<OperationCanceledException>();
            File.Exists(path).Should().BeFalse();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_MissingDirectory_ReportsFileSystemErrorAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"flowforge-missing-{Guid.NewGuid():N}", "output.txt");
        var node = new FileWriterSinkNode(new FileWriterSinkNodeConfig(path));
        var context = new TestExecutionContext();
        context.SetInputStream(node.ContentInput, "内容");

        var action = async () => await node.ExecuteAsync(context, CancellationToken.None);

        await action.Should().ThrowAsync<DirectoryNotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_DirectoryPath_ReportsPermissionErrorAsync()
    {
        var directory = Directory.CreateTempSubdirectory("flowforge-writer-");
        try
        {
            var node = new FileWriterSinkNode(new FileWriterSinkNodeConfig(directory.FullName));
            var context = new TestExecutionContext();
            context.SetInputStream(node.ContentInput, "内容");

            var action = async () => await node.ExecuteAsync(context, CancellationToken.None);

            await action.Should().ThrowAsync<UnauthorizedAccessException>();
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private sealed class BlockingStreamExecutionContext(IPort<string> contentPort) : IExecutionContext
    {
        public TaskCompletionSource FirstValueConsumed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseSecondValue { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<T?> ReadAsync<T>(IPort<T> port, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<T?> ReadAllAsync<T>(IPort<T> port, CancellationToken ct)
        {
            if (!ReferenceEquals(port, contentPort))
            {
                throw new InvalidOperationException("测试上下文收到了未知输入端口。");
            }

            return ReadValuesAsync<T>(ct);
        }

        public ValueTask WriteAsync<T>(IPort<T> port, T? value, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        private async IAsyncEnumerable<T?> ReadValuesAsync<T>(
            [EnumeratorCancellation] CancellationToken ct)
        {
            yield return (T?)(object?)"第一";
            FirstValueConsumed.TrySetResult();
            await ReleaseSecondValue.Task.WaitAsync(ct);
            yield return (T?)(object?)"第二";
        }
    }
}
