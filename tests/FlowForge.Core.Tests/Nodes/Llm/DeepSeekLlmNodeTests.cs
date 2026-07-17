using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FluentAssertions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Llm;

namespace FlowForge.Core.Tests.Nodes.Llm;

public sealed class DeepSeekLlmNodeTests
{
    [Fact]
    public void Constructor_ConfigAndPorts_ExposeStableContract()
    {
        var id = Guid.NewGuid();
        var config = new DeepSeekLlmNodeConfig("deepseek.default", "deepseek-chat", 0.25);
        using var client = new HttpClient(new StubHttpMessageHandler(_ => CreateSuccessResponse("unused")));

        var node = new DeepSeekLlmNode(id, config, new StaticSecretStore("test-key"), client);

        node.Id.Should().Be(id);
        node.TypeId.Should().Be("core.llm.deepseek");
        node.Inputs.Should().ContainSingle().Which.Should().BeSameAs(node.PromptInput);
        node.Outputs.Should().ContainSingle().Which.Should().BeSameAs(node.ResponseOutput);
        node.PromptInput.Id.Should().Be("prompt");
        node.ResponseOutput.Id.Should().Be("response");
        node.Config.Should().BeSameAs(config);
    }

    [Fact]
    public async Task ExecuteAsync_ValidPrompt_UsesFixedDeepSeekEndpointAndWritesResponseAsync()
    {
        var handler = new StubHttpMessageHandler(_ => CreateSuccessResponse("DeepSeek 回答"));
        using var client = new HttpClient(handler);
        var node = new DeepSeekLlmNode(
            new DeepSeekLlmNodeConfig("deepseek.default", "deepseek-chat", 0.25),
            new StaticSecretStore("test-key"),
            client);
        var context = new TestExecutionContext();
        context.SetInput(node.PromptInput, "请说明工作流");
        using var cancellation = new CancellationTokenSource();

        await node.ExecuteAsync(context, cancellation.Token);

        handler.Requests.Should().ContainSingle();
        var request = handler.Requests[0];
        request.RequestUri.Should().Be(new Uri("https://api.deepseek.com/v1/chat/completions"));
        request.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "test-key"));
        using var document = JsonDocument.Parse(request.Content);
        document.RootElement.GetProperty("model").GetString().Should().Be("deepseek-chat");
        document.RootElement.GetProperty("temperature").GetDouble().Should().Be(0.25);
        document.RootElement.GetProperty("messages")[0].GetProperty("content").GetString().Should().Be("请说明工作流");
        context.GetOutput(node.ResponseOutput).Should().Be("DeepSeek 回答");
        context.LastReadCancellationToken.Should().Be(cancellation.Token);
        context.LastWriteCancellationToken.Should().Be(cancellation.Token);
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

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.RequestUri, request.Headers.Authorization, content));
            return responseFactory(Requests.Count);
        }
    }

    private sealed record CapturedRequest(Uri? RequestUri, AuthenticationHeaderValue? Authorization, string Content);
}
