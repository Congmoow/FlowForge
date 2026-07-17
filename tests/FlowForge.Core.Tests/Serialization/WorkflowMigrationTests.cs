using System.Text.Json.Nodes;
using FluentAssertions;
using FlowForge.Core.Serialization;

namespace FlowForge.Core.Tests.Serialization;

public sealed class WorkflowMigrationTests
{
    [Fact]
    public void Upgrade_VersionZeroDocument_AddsSchemaVersionOne()
    {
        var document = JsonNode.Parse("{\"metadata\":{\"name\":\"示例\"},\"nodes\":[],\"edges\":[]}")!.AsObject();
        var migrator = new WorkflowMigrator([new V0ToV1WorkflowMigration()]);

        var upgraded = migrator.Upgrade(document);

        upgraded["schemaVersion"]!.GetValue<int>().Should().Be(1);
        upgraded["metadata"]!["name"]!.GetValue<string>().Should().Be("示例");
    }

    [Fact]
    public void Upgrade_MissingIntermediateMigration_Throws()
    {
        var document = JsonNode.Parse("{\"schemaVersion\":0}")!.AsObject();
        var migrator = new WorkflowMigrator([]);

        var action = () => migrator.Upgrade(document);

        action.Should().Throw<NotSupportedException>();
    }
}
