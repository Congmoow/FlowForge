using FluentAssertions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Sink;

namespace FlowForge.Core.Tests.Nodes.Sink;

public sealed class ConsoleSinkNodeTests
{
    [Fact]
    public void Constructor_ConfigAndId_ExposesStableContract()
    {
        var id = Guid.NewGuid();
        var config = new ConsoleSinkNodeConfig();
        using var writer = new StringWriter();

        var node = new ConsoleSinkNode(id, config, writer);

        node.Id.Should().Be(id);
        node.TypeId.Should().Be("core.sink.console");
        node.Inputs.Should().ContainSingle().Which.Should().BeSameAs(node.ValueInput);
        node.Outputs.Should().BeEmpty();
        node.Inputs.Should().NotBeAssignableTo<ICollection<IPort>>();
        node.ValueInput.Id.Should().Be("value");
        node.ValueInput.DataType.Should().Be(typeof(object));
        node.Config.Should().BeSameAs(config);
        (config with { }).Should().Be(config);
    }

    [Fact]
    public async Task ExecuteAsync_Value_WritesLineToInjectedWriterAsync()
    {
        using var writer = new RecordingTextWriter();
        var node = new ConsoleSinkNode(writer);
        var context = new TestExecutionContext();
        context.SetInput<object>(node.ValueInput, 42);
        using var cancellation = new CancellationTokenSource();

        await node.ExecuteAsync(context, cancellation.Token);

        writer.ToString().Should().Be($"42{writer.NewLine}");
        writer.LastCancellationToken.Should().Be(cancellation.Token);
        context.LastReadPort.Should().BeSameAs(node.ValueInput);
        context.LastReadCancellationToken.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task ExecuteAsync_NullValue_WritesEmptyLineAsync()
    {
        using var writer = new StringWriter();
        var node = new ConsoleSinkNode(writer);
        var context = new TestExecutionContext();
        context.SetInput<object>(node.ValueInput, null);

        await node.ExecuteAsync(context, CancellationToken.None);

        writer.ToString().Should().Be(writer.NewLine);
    }

    [Fact]
    public async Task ExecuteAsync_StructuredRows_WritesJsonDataAsync()
    {
        using var writer = new StringWriter();
        var node = new ConsoleSinkNode(writer);
        var context = new TestExecutionContext();
        IEnumerable<Dictionary<string, string>> rows =
        [
            new Dictionary<string, string>
            {
                ["name"] = "Alice",
                ["city"] = "杭州",
            },
        ];
        context.SetInput<object>(node.ValueInput, rows);

        await node.ExecuteAsync(context, CancellationToken.None);

        writer.ToString().Should().Be("""[{"name":"Alice","city":"杭州"}]""" + writer.NewLine);
    }

    [Fact]
    public async Task ExecuteAsync_StreamValues_WritesEveryValueAsync()
    {
        using var writer = new StringWriter();
        var node = new ConsoleSinkNode(writer);
        var context = new TestExecutionContext();
        context.SetInputStream<object>(node.ValueInput, "first", 2, "third");

        await node.ExecuteAsync(context, CancellationToken.None);

        writer.ToString().Should().Be($"first{writer.NewLine}2{writer.NewLine}third{writer.NewLine}");
    }

    private sealed class RecordingTextWriter : StringWriter
    {
        public CancellationToken LastCancellationToken { get; private set; }

        public override Task WriteLineAsync(
            ReadOnlyMemory<char> buffer,
            CancellationToken cancellationToken = default)
        {
            LastCancellationToken = cancellationToken;
            return base.WriteLineAsync(buffer, cancellationToken);
        }
    }
}
