using System.Text;
using FluentAssertions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.DataSource;

namespace FlowForge.Core.Tests.Nodes.DataSource;

public sealed class TextDataSourceNodeTests
{
    [Fact]
    public void Constructor_ConfigAndId_ExposesStableContract()
    {
        var id = Guid.NewGuid();
        var config = new TextDataSourceConfig("input.txt");

        var node = new TextDataSourceNode(id, config);

        node.Id.Should().Be(id);
        node.TypeId.Should().Be("core.datasource.text");
        node.Inputs.Should().BeEmpty();
        node.Outputs.Should().ContainSingle().Which.Should().BeSameAs(node.ContentOutput);
        node.Outputs.Should().NotBeAssignableTo<ICollection<IPort>>();
        node.ContentOutput.Id.Should().Be("content");
        node.ContentOutput.DataType.Should().Be(typeof(string));
        node.Config.Should().BeSameAs(config);
        config.Encoding.Should().Be("utf-8");
        (config with { Encoding = "utf-16" }).Encoding.Should().Be("utf-16");
    }

    [Fact]
    public async Task ExecuteAsync_DefaultEncoding_ReadsUtf8TextAsync()
    {
        const string expected = "你好，FlowForge";
        await using var file = await TemporaryFile.CreateAsync(expected);
        var node = new TextDataSourceNode(new TextDataSourceConfig(file.Path));
        var context = new TestExecutionContext();
        using var cancellation = new CancellationTokenSource();

        await node.ExecuteAsync(context, cancellation.Token);

        context.GetOutput(node.ContentOutput).Should().Be(expected);
        context.LastWritePort.Should().BeSameAs(node.ContentOutput);
        context.LastWriteCancellationToken.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task ExecuteAsync_NamedEncoding_ReadsRequestedEncodingAsync()
    {
        const string expected = "按 UTF-16 读取";
        await using var file = await TemporaryFile.CreateAsync(expected, Encoding.Unicode);
        var node = new TextDataSourceNode(new TextDataSourceConfig(file.Path, "utf-16"));
        var context = new TestExecutionContext();

        await node.ExecuteAsync(context, CancellationToken.None);

        context.GetOutput(node.ContentOutput).Should().Be(expected);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledToken_StopsBeforeWritingAsync()
    {
        await using var file = await TemporaryFile.CreateAsync("content");
        var node = new TextDataSourceNode(new TextDataSourceConfig(file.Path));
        var context = new TestExecutionContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = async () => await node.ExecuteAsync(context, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        context.WriteCount.Should().Be(0);
    }
}
