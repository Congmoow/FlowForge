using System.Text.Json;
using FluentAssertions;
using FlowForge.Core.Graph;
using FlowForge.Core.Nodes.DataSource;
using FlowForge.Core.Nodes.Sink;
using FlowForge.Core.Serialization;

namespace FlowForge.Core.Tests.Serialization;

public sealed class WorkflowSerializerTests
{
    [Fact]
    public async Task SaveAndLoadAsync_WorkflowDocument_RoundTripsSchemaDataAsync()
    {
        using var stream = new MemoryStream();
        using var config = JsonDocument.Parse("{\"filePath\":\"input.txt\",\"encoding\":\"utf-8\"}");
        var document = new WorkflowDocument(
            new WorkflowMetadata("示例", DateTimeOffset.UnixEpoch, "0.1.0"),
            [new WorkflowNodeDocument(Guid.NewGuid(), "core.datasource.text", new WorkflowNodePosition(20, 30), config.RootElement.Clone())],
            []);

        await WorkflowSerializer.SaveAsync(document, stream, CancellationToken.None);
        stream.Position = 0;
        var loaded = await WorkflowSerializer.LoadAsync(stream, CancellationToken.None);

        loaded.SchemaVersion.Should().Be(document.SchemaVersion);
        loaded.Metadata.Should().Be(document.Metadata);
        loaded.Nodes.Should().ContainSingle();
        loaded.Nodes[0].Id.Should().Be(document.Nodes[0].Id);
        loaded.Nodes[0].TypeId.Should().Be(document.Nodes[0].TypeId);
        loaded.Nodes[0].Position.Should().Be(document.Nodes[0].Position);
        loaded.Nodes[0].Config.GetRawText().Should().Be(document.Nodes[0].Config.GetRawText());
        loaded.Edges.Should().BeEmpty();
    }

    [Fact]
    public void CreateWorkflow_RegisteredNodesAndEdge_RebuildsDomainGraph()
    {
        var source = new TextDataSourceNode(Guid.NewGuid(), new TextDataSourceConfig("input.txt"));
        var sink = new ConsoleSinkNode(Guid.NewGuid(), new ConsoleSinkNodeConfig());
        var workflow = new Workflow();
        workflow.AddNode(source);
        workflow.AddNode(sink);
        workflow.AddEdge(new WorkflowEdge(Guid.NewGuid(), source.Id, source.ContentOutput.Id, sink.Id, sink.ValueInput.Id));
        var document = WorkflowSerializer.CreateDocument(
            workflow,
            new Dictionary<Guid, WorkflowNodePosition>
            {
                [source.Id] = new(10, 20),
                [sink.Id] = new(30, 40),
            },
            new WorkflowMetadata("示例", DateTimeOffset.UnixEpoch, "0.1.0"));
        var registry = CreateRegistry();

        var rebuilt = WorkflowSerializer.CreateWorkflow(document, registry);

        rebuilt.Nodes.Should().HaveCount(2);
        rebuilt.Edges.Should().ContainSingle().Which.Should().BeEquivalentTo(workflow.Edges[0]);
        rebuilt.Nodes.Should().ContainSingle(node => node.Id == source.Id && node is TextDataSourceNode);
        rebuilt.Nodes.Should().ContainSingle(node => node.Id == sink.Id && node is ConsoleSinkNode);
    }

    private static NodeRegistry CreateRegistry()
    {
        var registry = new NodeRegistry();
        registry.Register("core.datasource.text", static (id, config) => new TextDataSourceNode(id, config.Deserialize<TextDataSourceConfig>(WorkflowJsonSerializerOptions.Default)!));
        registry.Register("core.sink.console", static (id, config) => new ConsoleSinkNode(id, config.Deserialize<ConsoleSinkNodeConfig>(WorkflowJsonSerializerOptions.Default)!));
        return registry;
    }
}
