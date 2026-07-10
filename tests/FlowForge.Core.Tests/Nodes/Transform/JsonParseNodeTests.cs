using System.Text.Json;
using FluentAssertions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Transform;

namespace FlowForge.Core.Tests.Nodes.Transform;

public sealed class JsonParseNodeTests
{
    [Fact]
    public void Constructor_ConfigAndId_ExposesStableContract()
    {
        var id = Guid.NewGuid();
        var config = new JsonParseNodeConfig();

        var node = new JsonParseNode(id, config);

        node.Id.Should().Be(id);
        node.TypeId.Should().Be("core.transform.json-parse");
        node.Inputs.Should().ContainSingle().Which.Should().BeSameAs(node.TextInput);
        node.Outputs.Should().ContainSingle().Which.Should().BeSameAs(node.JsonOutput);
        node.Inputs.Should().NotBeAssignableTo<ICollection<IPort>>();
        node.Outputs.Should().NotBeAssignableTo<ICollection<IPort>>();
        node.TextInput.Id.Should().Be("text");
        node.TextInput.DataType.Should().Be(typeof(string));
        node.JsonOutput.Id.Should().Be("json");
        node.JsonOutput.DataType.Should().Be(typeof(JsonElement));
        node.Config.Should().BeSameAs(config);
        (config with { }).Should().Be(config);
    }

    [Fact]
    public async Task ExecuteAsync_ValidJson_WritesIndependentCloneAsync()
    {
        var node = new JsonParseNode();
        var context = new TestExecutionContext();
        context.SetInput(node.TextInput, """{"name":"FlowForge","values":[1,2]}""");
        using var cancellation = new CancellationTokenSource();

        await node.ExecuteAsync(context, cancellation.Token);

        var json = context.GetOutput(node.JsonOutput);
        json.GetProperty("name").GetString().Should().Be("FlowForge");
        json.GetProperty("values").GetArrayLength().Should().Be(2);
        context.LastReadPort.Should().BeSameAs(node.TextInput);
        context.LastReadCancellationToken.Should().Be(cancellation.Token);
        context.LastWritePort.Should().BeSameAs(node.JsonOutput);
        context.LastWriteCancellationToken.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJson_ThrowsJsonExceptionAsync()
    {
        var node = new JsonParseNode();
        var context = new TestExecutionContext();
        context.SetInput(node.TextInput, "{invalid");

        var action = async () => await node.ExecuteAsync(context, CancellationToken.None);

        await action.Should().ThrowAsync<JsonException>();
        context.WriteCount.Should().Be(0);
    }
}
