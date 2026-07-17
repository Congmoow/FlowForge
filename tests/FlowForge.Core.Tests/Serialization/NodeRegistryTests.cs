using System.Text.Json;
using FluentAssertions;
using FlowForge.Core.Nodes.DataSource;
using FlowForge.Core.Serialization;

namespace FlowForge.Core.Tests.Serialization;

public sealed class NodeRegistryTests
{
    [Fact]
    public void Create_RegisteredTextNode_UsesDocumentIdAndConfig()
    {
        var registry = new NodeRegistry();
        registry.Register("core.datasource.text", static (id, config) =>
            new TextDataSourceNode(id, config.Deserialize<TextDataSourceConfig>(WorkflowJsonSerializerOptions.Default)!));
        using var config = JsonDocument.Parse("{\"filePath\":\"input.txt\",\"encoding\":\"utf-16\"}");
        var document = new WorkflowNodeDocument(Guid.NewGuid(), "core.datasource.text", new WorkflowNodePosition(0, 0), config.RootElement.Clone());

        var node = registry.Create(document);

        node.Should().BeOfType<TextDataSourceNode>();
        node.Id.Should().Be(document.Id);
        node.Config.Should().Be(new TextDataSourceConfig("input.txt", "utf-16"));
    }

    [Fact]
    public void Register_DuplicateTypeId_Throws()
    {
        var registry = new NodeRegistry();
        registry.Register("example.node", static (_, _) => new TextDataSourceNode());

        var action = () => registry.Register("example.node", static (_, _) => new TextDataSourceNode());

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Create_UnknownTypeId_Throws()
    {
        var registry = new NodeRegistry();
        using var config = JsonDocument.Parse("{}");
        var document = new WorkflowNodeDocument(Guid.NewGuid(), "unknown.node", new WorkflowNodePosition(0, 0), config.RootElement.Clone());

        var action = () => registry.Create(document);

        action.Should().Throw<InvalidOperationException>();
    }
}
