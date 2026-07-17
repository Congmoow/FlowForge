using System.Net;
using FluentAssertions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Llm;
using NSubstitute;

namespace FlowForge.Core.Tests.Nodes.Llm;

public sealed class LlmNodeFailureTests
{
    [Fact]
    public async Task ExecuteAsync_MissingOpenAiSecret_DoesNotSendRequestAsync()
    {
        var secretStore = Substitute.For<ISecretStore>();
        var handler = new CountingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler);
        var node = new OpenAiLlmNode(
            new OpenAiLlmNodeConfig("openai.default"),
            secretStore,
            client);
        var context = new TestExecutionContext();
        context.SetInput(node.PromptInput, "提示词");

        var action = async () => await node.ExecuteAsync(context, CancellationToken.None);

        await action.Should().ThrowAsync<SecretStoreException>();
        handler.RequestCount.Should().Be(0);
        context.WriteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_OpenAiClientError_DoesNotRetryOrWriteOutputAsync()
    {
        var secretStore = new StaticSecretStore("test-key");
        var handler = new CountingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        using var client = new HttpClient(handler);
        var node = new OpenAiLlmNode(
            new OpenAiLlmNodeConfig("openai.default", BaseUrl: "https://api.example.test/v1"),
            secretStore,
            client);
        var context = new TestExecutionContext();
        context.SetInput(node.PromptInput, "提示词");

        var action = async () => await node.ExecuteAsync(context, CancellationToken.None);

        await action.Should().ThrowAsync<HttpRequestException>();
        handler.RequestCount.Should().Be(1);
        context.WriteCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledBeforeExecution_DoesNotReadSecretOrSendDeepSeekRequestAsync()
    {
        var secretStore = Substitute.For<ISecretStore>();
        var handler = new CountingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler);
        var node = new DeepSeekLlmNode(
            new DeepSeekLlmNodeConfig("deepseek.default"),
            secretStore,
            client);
        var context = new TestExecutionContext();
        context.SetInput(node.PromptInput, "提示词");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = async () => await node.ExecuteAsync(context, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        secretStore.ReceivedCalls().Should().BeEmpty();
        handler.RequestCount.Should().Be(0);
        context.WriteCount.Should().Be(0);
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

    private sealed class CountingHttpMessageHandler(Func<int, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(responseFactory(RequestCount));
        }
    }
}
