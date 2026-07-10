using FluentAssertions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Transform;

namespace FlowForge.Core.Tests.Nodes.Transform;

public sealed class TextConcatNodeTests
{
    [Fact]
    public void Constructor_ConfigAndId_ExposesStableContract()
    {
        var id = Guid.NewGuid();
        var config = new TextConcatNodeConfig(" | ");

        var node = new TextConcatNode(id, config);

        node.Id.Should().Be(id);
        node.TypeId.Should().Be("core.transform.text-concat");
        node.Inputs.Should().ContainSingle().Which.Should().BeSameAs(node.PartsInput);
        node.Outputs.Should().ContainSingle().Which.Should().BeSameAs(node.ResultOutput);
        node.Inputs.Should().NotBeAssignableTo<ICollection<IPort>>();
        node.Outputs.Should().NotBeAssignableTo<ICollection<IPort>>();
        node.PartsInput.Id.Should().Be("parts");
        node.PartsInput.DataType.Should().Be(typeof(string[]));
        node.ResultOutput.Id.Should().Be("result");
        node.ResultOutput.DataType.Should().Be(typeof(string));
        node.Config.Should().BeSameAs(config);
        (config with { Separator = "," }).Separator.Should().Be(",");
    }

    [Fact]
    public async Task ExecuteAsync_PartsInput_JoinsWithConfiguredSeparatorAsync()
    {
        var node = new TextConcatNode(new TextConcatNodeConfig(" / "));
        var context = new TestExecutionContext();
        var parts = new[] { "first", "second", "third" };
        context.SetInput(node.PartsInput, parts);
        using var cancellation = new CancellationTokenSource();

        await node.ExecuteAsync(context, cancellation.Token);

        context.GetOutput(node.ResultOutput).Should().Be("first / second / third");
        context.LastReadPort.Should().BeSameAs(node.PartsInput);
        context.LastReadCancellationToken.Should().Be(cancellation.Token);
        context.LastWritePort.Should().BeSameAs(node.ResultOutput);
        context.LastWriteCancellationToken.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task ExecuteAsync_NullParts_WritesEmptyStringAsync()
    {
        var node = new TextConcatNode();
        var context = new TestExecutionContext();
        context.SetInput<string[]>(node.PartsInput, null);

        await node.ExecuteAsync(context, CancellationToken.None);

        context.GetOutput(node.ResultOutput).Should().BeEmpty();
    }
}
