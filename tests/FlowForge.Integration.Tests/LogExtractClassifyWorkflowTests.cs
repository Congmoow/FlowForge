using System.Net;
using System.Text.Json;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Execution;
using FlowForge.Core.Graph;
using FlowForge.Core.Nodes.DataSource;
using FlowForge.Core.Nodes.Llm;
using FlowForge.Core.Nodes.Sink;
using FlowForge.Core.Nodes.Transform;
using FluentAssertions;

namespace FlowForge.Integration.Tests;

public sealed class LogExtractClassifyWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_LogExtractClassifySample_UsesFakeLlmAndPreservesSecretReferenceAsync()
    {
        var sampleDirectory = Path.Combine(AppContext.BaseDirectory, "samples");
        var workflowPath = Path.Combine(sampleDirectory, "log-extract-classify.ffw");
        var logPath = Path.Combine(sampleDirectory, "data", "application.log");

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(workflowPath));
        var root = document.RootElement;
        root.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        var nodes = root.GetProperty("nodes").EnumerateArray().ToArray();
        nodes.Select(node => node.GetProperty("typeId").GetString()).Should().Equal(
            "core.datasource.text",
            "core.transform.regex-extract",
            "core.transform.text-concat",
            "core.llm.openai",
            "core.sink.console");
        File.Exists(logPath).Should().BeTrue();

        var textElement = nodes[0];
        var regexElement = nodes[1];
        var concatElement = nodes[2];
        var llmElement = nodes[3];
        var consoleElement = nodes[4];
        var llmConfig = llmElement.GetProperty("config");

        llmConfig.GetProperty("apiKeySecretId").GetString().Should().Be("openai.demo");
        llmConfig.TryGetProperty("apiKey", out _).Should().BeFalse();

        var text = new TextDataSourceNode(
            textElement.GetProperty("id").GetGuid(),
            new TextDataSourceConfig(
                logPath,
                textElement.GetProperty("config").GetProperty("encoding").GetString()!));
        var regexConfig = regexElement.GetProperty("config");
        var regex = new RegexExtractNode(
            regexElement.GetProperty("id").GetGuid(),
            new RegexExtractNodeConfig(
                regexConfig.GetProperty("pattern").GetString()!,
                regexConfig.GetProperty("group").GetInt32()));
        var concat = new TextConcatNode(
            concatElement.GetProperty("id").GetGuid(),
            new TextConcatNodeConfig(
                concatElement.GetProperty("config").GetProperty("separator").GetString()!));
        var handler = new ClassificationHttpMessageHandler();
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
        var console = new ConsoleSinkNode(
            consoleElement.GetProperty("id").GetGuid(),
            new ConsoleSinkNodeConfig(),
            writer);

        var workflow = new Workflow();
        workflow.AddNode(text);
        workflow.AddNode(regex);
        workflow.AddNode(concat);
        workflow.AddNode(llm);
        workflow.AddNode(console);
        foreach (var edge in root.GetProperty("edges").EnumerateArray())
        {
            workflow.AddEdge(new WorkflowEdge(
                edge.GetProperty("id").GetGuid(),
                edge.GetProperty("sourceNodeId").GetGuid(),
                edge.GetProperty("sourcePortId").GetString()!,
                edge.GetProperty("targetNodeId").GetGuid(),
                edge.GetProperty("targetPortId").GetString()!));
        }

        await new WorkflowScheduler().ExecuteAsync(workflow, progress: null, CancellationToken.None);

        writer.ToString().Should().Contain("支付接口");
        handler.LastRequestBody.Should().Contain("payment failed");
        handler.LastRequestBody.Should().Contain("cache miss");
    }

    private sealed class StaticSecretStore(string secret) : ISecretStore
    {
        public ValueTask<string?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(secret);
        }

        public ValueTask SetSecretAsync(
            string secretId,
            string secretValue,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<bool> DeleteSecretAsync(
            string secretId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ClassificationHttpMessageHandler : HttpMessageHandler
    {
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"分类结果：支付接口超时，需要转交支付平台。\"}}]}"),
            };
        }
    }
}
