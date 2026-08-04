using System.Text.Json;
using FluentAssertions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.DataSource;
using FlowForge.Core.Serialization;

namespace FlowForge.Core.Tests.Abstractions;

public sealed class NodeCatalogTests
{
    [Fact]
    public void PortFactory_Create_ReturnsPublicTypedPort()
    {
        var port = PortFactory.Create<int>("count", "数量");

        port.Id.Should().Be("count");
        port.Name.Should().Be("数量");
        port.DataType.Should().Be(typeof(int));
        port.Should().BeAssignableTo<IPort<int>>();
    }

    [Fact]
    public void CreateDefault_ContainsAllBuiltInDefinitionsAndCreatesNodes()
    {
        var registry = NodeRegistry.CreateDefault();
        var expectedTypeIds = new[]
        {
            "core.datasource.csv",
            "core.datasource.text",
            "core.transform.json-parse",
            "core.transform.text-concat",
            "core.transform.foreach",
            "core.transform.regex-extract",
            "core.llm.openai",
            "core.llm.deepseek",
            "core.sink.console",
            "core.sink.file-writer",
        };

        registry.Definitions.Select(definition => definition.TypeId)
            .Should().Equal(expectedTypeIds);

        foreach (var definition in registry.Definitions)
        {
            using var config = JsonDocument.Parse("{}");
            var document = new WorkflowNodeDocument(
                Guid.NewGuid(),
                definition.TypeId,
                new WorkflowNodePosition(0, 0),
                config.RootElement.Clone());

            var node = registry.Create(document);

            node.TypeId.Should().Be(definition.TypeId);
            node.Config.Should().BeAssignableTo<INodeConfig>();
        }
    }

    [Fact]
    public void DefaultDefinition_ExposesTitlePortsAndTypedConfigMetadata()
    {
        var definition = NodeRegistry.CreateDefault().GetDefinition("core.datasource.text");

        definition.Title.Should().Be("文本读取");
        definition.DisplayName.Should().Be(definition.Title);
        definition.ConfigType.Should().Be(typeof(TextDataSourceConfig));
        definition.Inputs.Should().BeEmpty();
        definition.Outputs.Should().ContainSingle(port =>
            port.Id == "content"
            && port.Name == "内容"
            && port.DataType == typeof(string));

        var node = definition.Create(Guid.NewGuid(), new TextDataSourceConfig("input.txt"));

        node.Should().BeOfType<TextDataSourceNode>();
        node.Config.Should().Be(new TextDataSourceConfig("input.txt"));
    }

    [Fact]
    public void RegisterRange_DuplicateTypeId_IsAtomic()
    {
        var registry = new NodeRegistry();
        var existing = NodeDefinition.Create(
            "existing.node",
            "已有节点",
            static (id, config) => new TextDataSourceNode(id, config),
            static () => new TextDataSourceConfig());
        var added = NodeDefinition.Create(
            "added.node",
            "新增节点",
            static (id, config) => new TextDataSourceNode(id, config),
            static () => new TextDataSourceConfig());
        var duplicate = NodeDefinition.Create(
            "existing.node",
            "重复节点",
            static (id, config) => new TextDataSourceNode(id, config),
            static () => new TextDataSourceConfig());

        registry.Register(existing);
        var action = () => registry.RegisterRange([added, duplicate]);

        action.Should().Throw<InvalidOperationException>();
        registry.Definitions.Select(definition => definition.TypeId)
            .Should().Equal("existing.node");
    }

    [Fact]
    public void NodeDefinition_CreateFromJson_UsesTypedConfigFactory()
    {
        var definition = NodeDefinition.Create(
            "core.datasource.text",
            "文本读取",
            static (id, config) => new TextDataSourceNode(id, config),
            static () => new TextDataSourceConfig());
        using var json = JsonDocument.Parse("{\"filePath\":\"input.txt\",\"encoding\":\"utf-16\"}");

        var node = definition.Create(Guid.NewGuid(), json.RootElement);

        node.Config.Should().Be(new TextDataSourceConfig("input.txt", "utf-16"));
    }

    private sealed class TestPlugin : INodePlugin
    {
        public IReadOnlyList<NodeDefinition> Definitions { get; } = [];
    }
}
