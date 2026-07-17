using System.Text.Json;
using System.Globalization;
using FluentAssertions;
using FlowForge.Core.Serialization;

namespace FlowForge.Core.Tests.Serialization;

public sealed class WorkflowSchemaTests
{
    [Fact]
    public void WorkflowDocument_DefaultVersion_UsesSchemaVersionOne()
    {
        var document = new WorkflowDocument(
            new WorkflowMetadata("示例", DateTimeOffset.Parse("2026-07-17T00:00:00Z", CultureInfo.InvariantCulture), "0.1.0"),
            [],
            []);

        document.SchemaVersion.Should().Be(1);
        document.Nodes.Should().BeEmpty();
        document.Edges.Should().BeEmpty();
    }

    [Fact]
    public void WorkflowDocument_Serialize_UsesFfwV1FieldNames()
    {
        using var config = JsonDocument.Parse("{\"separator\":\",\"}");
        var document = new WorkflowDocument(
            new WorkflowMetadata("示例", DateTimeOffset.Parse("2026-07-17T00:00:00Z", CultureInfo.InvariantCulture), "0.1.0"),
            [new WorkflowNodeDocument(Guid.Parse("11111111-1111-1111-1111-111111111111"), "core.transform.text-concat", new WorkflowNodePosition(100, 200), config.RootElement.Clone())],
            [new WorkflowEdgeDocument(Guid.Parse("22222222-2222-2222-2222-222222222222"), Guid.Parse("11111111-1111-1111-1111-111111111111"), "result", Guid.Parse("33333333-3333-3333-3333-333333333333"), "parts")]);

        var json = JsonSerializer.Serialize(document, WorkflowJsonSerializerOptions.Default);

        using var parsed = JsonDocument.Parse(json);
        parsed.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        parsed.RootElement.GetProperty("metadata").GetProperty("name").GetString().Should().Be("示例");
        parsed.RootElement.GetProperty("nodes")[0].GetProperty("position").GetProperty("x").GetDouble().Should().Be(100);
        parsed.RootElement.GetProperty("nodes")[0].GetProperty("config").GetProperty("separator").GetString().Should().Be(",");
        parsed.RootElement.GetProperty("edges")[0].GetProperty("sourcePortId").GetString().Should().Be("result");
    }
}
