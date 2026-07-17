using System.Net;
using System.Text.Json;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Execution;
using FlowForge.Core.Graph;
using FlowForge.Core.Nodes.DataSource;
using FlowForge.Core.Nodes.Llm;
using FlowForge.Core.Nodes.Sink;
using FluentAssertions;

namespace FlowForge.Integration.Tests;

public sealed class CsvSummarizeWorkflowTests
{
    [Fact]
    public async Task CsvSummarizeSample_ContainsSecretReferenceWithoutPlaintextApiKeyAsync()
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "samples", "csv-summarize-with-llm.ffw");

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(samplePath));
        var openAiNode = document.RootElement.GetProperty("nodes")
            .EnumerateArray()
            .Single(node => node.GetProperty("typeId").GetString() == "core.llm.openai");

        openAiNode.GetProperty("config").GetProperty("apiKeySecretId").GetString().Should().Be("openai.demo");
        openAiNode.GetProperty("config").TryGetProperty("apiKey", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_CsvSummarizeSample_UsesFakeLlmAndWritesSummaryAsync()
    {
        var sampleDirectory = Path.Combine(AppContext.BaseDirectory, "samples");
        var samplePath = Path.Combine(sampleDirectory, "csv-summarize-with-llm.ffw");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(samplePath));
        var nodes = document.RootElement.GetProperty("nodes").EnumerateArray().ToArray();
        var textElement = nodes.Single(node => node.GetProperty("typeId").GetString() == "core.datasource.text");
        var llmElement = nodes.Single(node => node.GetProperty("typeId").GetString() == "core.llm.openai");
        var consoleElement = nodes.Single(node => node.GetProperty("typeId").GetString() == "core.sink.console");
        var textConfig = textElement.GetProperty("config");
        var llmConfig = llmElement.GetProperty("config");
        var text = new TextDataSourceNode(
            textElement.GetProperty("id").GetGuid(),
            new TextDataSourceConfig(Path.Combine(sampleDirectory, "data", "csv-summary-prompt.txt"), textConfig.GetProperty("encoding").GetString()!));
        var handler = new SummaryHttpMessageHandler();
        using var client = new HttpClient(handler);
        var llm = new OpenAiLlmNode(
            llmElement.GetProperty("id").GetGuid(),
            new OpenAiLlmNodeConfig(
                llmConfig.GetProperty("apiKeySecretId").GetString()!,
                llmConfig.GetProperty("model").GetString()!,
                llmConfig.GetProperty("temperature").GetDouble(),
                llmConfig.GetProperty("baseUrl").GetString()!),
            new StaticSecretStore("fake-key"),
            client);
        using var writer = new StringWriter();
        var console = new ConsoleSinkNode(consoleElement.GetProperty("id").GetGuid(), new ConsoleSinkNodeConfig(), writer);
        var workflow = new Workflow();
        workflow.AddNode(text);
        workflow.AddNode(llm);
        workflow.AddNode(console);
        foreach (var edge in document.RootElement.GetProperty("edges").EnumerateArray())
        {
            workflow.AddEdge(new WorkflowEdge(
                edge.GetProperty("id").GetGuid(),
                edge.GetProperty("sourceNodeId").GetGuid(),
                edge.GetProperty("sourcePortId").GetString()!,
                edge.GetProperty("targetNodeId").GetGuid(),
                edge.GetProperty("targetPortId").GetString()!));
        }

        await new WorkflowScheduler().ExecuteAsync(workflow, progress: null, CancellationToken.None);

        writer.ToString().Should().Contain("杭州和上海");
        using var request = JsonDocument.Parse(handler.LastRequestBody);
        request.RootElement.GetProperty("messages")[0].GetProperty("content").GetString().Should().Contain("Alice,杭州");
    }

    private sealed class StaticSecretStore(string secret) : ISecretStore
    {
        public ValueTask<string?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(secret);
        }

        public ValueTask SetSecretAsync(string secretId, string secretValue, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<bool> DeleteSecretAsync(string secretId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class SummaryHttpMessageHandler : HttpMessageHandler
    {
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"CSV 包含 Alice 和 Bob，分别来自杭州和上海。\"}}]}"),
            };
        }
    }
}
