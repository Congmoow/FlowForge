using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Llm;

namespace FlowForge.Core.Tests.Nodes.Llm;

public sealed class OpenAiLlmNodeTests
{
    [Fact]
    public void Constructor_ConfigAndPorts_ExposeStableContract()
    {
        var id = Guid.NewGuid();
        var config = new OpenAiLlmNodeConfig("openai.default", "gpt-4o-mini", 0.25, "https://api.example.test/v1");
        using var client = new HttpClient(new StubHttpMessageHandler(_ => CreateSuccessResponse("unused")));

        var node = new OpenAiLlmNode(id, config, new StaticSecretStore("test-key"), client);

        node.Id.Should().Be(id);
        node.TypeId.Should().Be("core.llm.openai");
        node.Inputs.Should().ContainSingle().Which.Should().BeSameAs(node.PromptInput);
        node.Outputs.Should().ContainSingle().Which.Should().BeSameAs(node.ResponseOutput);
        node.PromptInput.Id.Should().Be("prompt");
        node.PromptInput.DataType.Should().Be(typeof(string));
        node.ResponseOutput.Id.Should().Be("response");
        node.ResponseOutput.DataType.Should().Be(typeof(string));
        node.Config.Should().BeSameAs(config);
    }

    [Fact]
    public async Task ExecuteAsync_ValidPrompt_SendsChatCompletionAndWritesResponseAsync()
    {
        var handler = new StubHttpMessageHandler(_ => CreateSuccessResponse("生成的回答"));
        using var client = new HttpClient(handler);
        var node = new OpenAiLlmNode(
            new OpenAiLlmNodeConfig("openai.default", "gpt-4o-mini", 0.25, "https://api.example.test/v1"),
            new StaticSecretStore("test-key"),
            client);
        var context = new TestExecutionContext();
        context.SetInput(node.PromptInput, "请总结这段文本");
        using var cancellation = new CancellationTokenSource();

        await node.ExecuteAsync(context, cancellation.Token);

        handler.RequestCount.Should().Be(1);
        handler.Requests.Should().ContainSingle();
        var request = handler.Requests[0];
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri.Should().Be(new Uri("https://api.example.test/v1/chat/completions"));
        request.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "test-key"));
        using var document = JsonDocument.Parse(request.Content);
        document.RootElement.GetProperty("model").GetString().Should().Be("gpt-4o-mini");
        document.RootElement.GetProperty("temperature").GetDouble().Should().Be(0.25);
        document.RootElement.GetProperty("messages")[0].GetProperty("role").GetString().Should().Be("user");
        document.RootElement.GetProperty("messages")[0].GetProperty("content").GetString().Should().Be("请总结这段文本");
        context.GetOutput(node.ResponseOutput).Should().Be("生成的回答");
        context.LastReadCancellationToken.Should().Be(cancellation.Token);
        context.LastWriteCancellationToken.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task ExecuteAsync_TransientServerErrors_RetriesBeforeWritingResponseAsync()
    {
        var handler = new StubHttpMessageHandler(requestNumber => requestNumber < 3
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : CreateSuccessResponse("重试后的回答"));
        using var client = new HttpClient(handler);
        var node = new OpenAiLlmNode(
            new OpenAiLlmNodeConfig("openai.default", "gpt-4o-mini", 0.25, "https://api.example.test/v1"),
            new StaticSecretStore("test-key"),
            client);
        var context = new TestExecutionContext();
        context.SetInput(node.PromptInput, "请重试");

        await node.ExecuteAsync(context, CancellationToken.None);

        handler.RequestCount.Should().Be(3);
        context.GetOutput(node.ResponseOutput).Should().Be("重试后的回答");
    }

    private static HttpResponseMessage CreateSuccessResponse(string content)
    {
        var json = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content,
                    },
                },
            },
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        };
    }

    private sealed class StaticSecretStore(string secret) : ISecretStore
    {
        public ValueTask<string?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<string?>(secret);
        }

        public ValueTask SetSecretAsync(string secretId, string secretValue, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<bool> DeleteSecretAsync(string secretId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubHttpMessageHandler(Func<int, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var content = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri, request.Headers.Authorization, content));
            return responseFactory(RequestCount);
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri? RequestUri,
        AuthenticationHeaderValue? Authorization,
        string Content);
}
