using System.Collections.Concurrent;
using System.Text.Json;
using FlowForge.App.Services;
using FluentAssertions;
using FlowForge.Core.Execution;
using FlowForge.Core.Graph;
using FlowForge.Core.Nodes.DataSource;
using FlowForge.Core.Nodes.Sink;

namespace FlowForge.Integration.Tests;

public sealed class CsvToConsoleWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_CsvToConsoleSample_EmitsRowsAndSucceedsAsync()
    {
        var sampleDirectory = Path.Combine(AppContext.BaseDirectory, "samples");
        var workflowPath = Path.Combine(sampleDirectory, "csv-to-console.ffw");
        var csvPath = Path.Combine(sampleDirectory, "data", "stage3-people.csv");

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(workflowPath));
        var root = document.RootElement;
        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        var nodes = root.GetProperty("nodes").EnumerateArray().ToArray();
        nodes.Select(node => node.GetProperty("typeId").GetString()).Should().Equal(
            "core.datasource.csv",
            "core.sink.console");
        var edgeElement = root.GetProperty("edges").EnumerateArray().Should().ContainSingle().Which;

        var csvId = nodes[0].GetProperty("id").GetGuid();
        var consoleId = nodes[1].GetProperty("id").GetGuid();
        csvId.Should().Be(Stage3SampleWorkflowRunner.CsvNodeId);
        consoleId.Should().Be(Stage3SampleWorkflowRunner.ConsoleNodeId);
        edgeElement.GetProperty("sourceNodeId").GetGuid().Should().Be(csvId);
        edgeElement.GetProperty("sourcePortId").GetString().Should().Be("rows");
        edgeElement.GetProperty("targetNodeId").GetGuid().Should().Be(consoleId);
        edgeElement.GetProperty("targetPortId").GetString().Should().Be("value");

        var csvConfig = nodes[0].GetProperty("config");
        csvConfig.GetProperty("filePath").GetString().Should().Be("data/stage3-people.csv");
        var csv = new CsvDataSourceNode(
            csvId,
            new CsvDataSourceConfig(
                csvPath,
                csvConfig.GetProperty("hasHeader").GetBoolean(),
                csvConfig.GetProperty("delimiter").GetString()!.Single()));
        using var writer = new StringWriter();
        var console = new ConsoleSinkNode(consoleId, new ConsoleSinkNodeConfig(), writer);
        var workflow = new Workflow();
        workflow.AddNode(csv);
        workflow.AddNode(console);
        workflow.AddEdge(new WorkflowEdge(
            edgeElement.GetProperty("id").GetGuid(),
            csv.Id,
            csv.RowsOutput.Id,
            console.Id,
            console.ValueInput.Id));
        var progress = new RecordingProgress();

        await new WorkflowScheduler().ExecuteAsync(workflow, progress, CancellationToken.None);

        writer.ToString().Should().Contain("\"name\":\"Alice\"");
        writer.ToString().Should().Contain("\"city\":\"杭州\"");
        writer.ToString().Should().Contain("\"name\":\"Bob\"");
        progress.Events.Should().HaveCount(4);
        progress.Events.Should().OnlyContain(executionEvent =>
            executionEvent.Status == NodeExecutionStatus.Running
            || executionEvent.Status == NodeExecutionStatus.Succeeded);
        progress.Events.Count(executionEvent => executionEvent.Status == NodeExecutionStatus.Succeeded).Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_Stage3SampleRunner_ExecutesPackagedCsvWorkflowAsync()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "samples", "data", "stage3-people.csv");
        using var writer = new StringWriter();
        var runner = new Stage3SampleWorkflowRunner(new WorkflowScheduler(), writer, csvPath);
        var progress = new RecordingProgress();

        await runner.RunAsync(progress, CancellationToken.None);

        writer.ToString().Should().Contain("\"name\":\"Alice\"");
        writer.ToString().Should().Contain("\"city\":\"上海\"");
        progress.Events.Where(executionEvent => executionEvent.Status == NodeExecutionStatus.Succeeded)
            .Select(executionEvent => executionEvent.NodeId)
            .Should().BeEquivalentTo([
                Stage3SampleWorkflowRunner.CsvNodeId,
                Stage3SampleWorkflowRunner.ConsoleNodeId,
            ]);
    }

    private sealed class RecordingProgress : IProgress<NodeExecutionEvent>
    {
        private readonly ConcurrentQueue<NodeExecutionEvent> _events = new();

        public IReadOnlyList<NodeExecutionEvent> Events => _events.ToArray();

        public void Report(NodeExecutionEvent value)
        {
            _events.Enqueue(value);
        }
    }
}
