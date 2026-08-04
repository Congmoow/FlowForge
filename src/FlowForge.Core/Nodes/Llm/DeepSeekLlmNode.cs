using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Internal;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace FlowForge.Core.Nodes.Llm;

/// <summary>
/// 表示 DeepSeek 聊天补全节点的配置。
/// </summary>
/// <param name="ApiKeySecretId">保存 API Key 的不透明密钥引用。</param>
/// <param name="Model">用于生成回答的模型标识。</param>
/// <param name="Temperature">控制输出随机性的温度值。</param>
public sealed record DeepSeekLlmNodeConfig(
    [property: ConfigField(Label = "API Key 密钥引用", Editor = "Password", Required = true, Order = 0)]
    string ApiKeySecretId = "",
    [property: ConfigField(Label = "模型", Editor = "ComboBox", Options = "deepseek-chat,deepseek-reasoner", Order = 1)]
    string Model = "deepseek-chat",
    [property: ConfigField(Label = "温度", Editor = "NumericUpDown", Min = 0, Max = 2, Order = 2)]
    double Temperature = 0.7) : INodeConfig;

/// <summary>
/// 调用 DeepSeek Chat Completions 接口并输出模型回答。
/// </summary>
public sealed class DeepSeekLlmNode : INode
{
    private static readonly Uri ChatCompletionsEndpoint = new("https://api.deepseek.com/v1/chat/completions");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ResiliencePipeline<HttpResponseMessage> SendPipeline = CreateSendPipeline();
    private DeepSeekLlmNodeConfig _config;
    private readonly ISecretStore _secretStore;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// 初始化使用新节点标识和默认配置的 DeepSeek 聊天补全节点。
    /// </summary>
    /// <param name="secretStore">用于读取 API Key 的密钥存储。</param>
    /// <param name="httpClient">用于调用 DeepSeek 服务的 HTTP 客户端。</param>
    public DeepSeekLlmNode(ISecretStore secretStore, HttpClient httpClient)
        : this(Guid.NewGuid(), new DeepSeekLlmNodeConfig(), secretStore, httpClient)
    {
    }

    /// <summary>
    /// 初始化使用新节点标识和指定配置的 DeepSeek 聊天补全节点。
    /// </summary>
    /// <param name="config">DeepSeek 节点配置。</param>
    /// <param name="secretStore">用于读取 API Key 的密钥存储。</param>
    /// <param name="httpClient">用于调用 DeepSeek 服务的 HTTP 客户端。</param>
    public DeepSeekLlmNode(
        DeepSeekLlmNodeConfig config,
        ISecretStore secretStore,
        HttpClient httpClient)
        : this(Guid.NewGuid(), config, secretStore, httpClient)
    {
    }

    /// <summary>
    /// 初始化使用指定节点标识和配置的 DeepSeek 聊天补全节点。
    /// </summary>
    /// <param name="id">跨会话保持稳定的节点标识。</param>
    /// <param name="config">DeepSeek 节点配置。</param>
    /// <param name="secretStore">用于读取 API Key 的密钥存储。</param>
    /// <param name="httpClient">用于调用 DeepSeek 服务的 HTTP 客户端。</param>
    public DeepSeekLlmNode(
        Guid id,
        DeepSeekLlmNodeConfig config,
        ISecretStore secretStore,
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(httpClient);

        Id = id;
        _config = config;
        _secretStore = secretStore;
        _httpClient = httpClient;
        PromptInput = new NodePort<string>("prompt", "提示词");
        ResponseOutput = new NodePort<string>("response", "回答");
        Inputs = NodePortList.Create(PromptInput);
        Outputs = NodePortList.Create(ResponseOutput);
    }

    /// <summary>
    /// 获取跨会话保持稳定的节点标识。
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// 获取 DeepSeek 节点的稳定类型标识。
    /// </summary>
    public string TypeId => "core.llm.deepseek";

    /// <summary>
    /// 获取仅包含提示词输入端口的不可变集合。
    /// </summary>
    public IReadOnlyList<IPort> Inputs { get; }

    /// <summary>
    /// 获取仅包含回答输出端口的不可变集合。
    /// </summary>
    public IReadOnlyList<IPort> Outputs { get; }

    /// <summary>
    /// 获取提示词输入端口。
    /// </summary>
    public IPort<string> PromptInput { get; }

    /// <summary>
    /// 获取模型回答输出端口。
    /// </summary>
    public IPort<string> ResponseOutput { get; }

    /// <summary>
    /// 获取或设置 DeepSeek 节点配置。
    /// </summary>
    public INodeConfig Config
    {
        get => _config;
        set => _config = value as DeepSeekLlmNodeConfig
            ?? throw new ArgumentException($"配置必须是 {nameof(DeepSeekLlmNodeConfig)}。", nameof(value));
    }

    /// <summary>
    /// 读取提示词和 API Key，调用 DeepSeek Chat Completions 接口后写入回答。
    /// </summary>
    /// <param name="ctx">当前节点的执行上下文。</param>
    /// <param name="ct">用于取消读取、网络调用和输出写入的令牌。</param>
    /// <returns>表示节点执行过程的异步结果。</returns>
    public async ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ct.ThrowIfCancellationRequested();

        var prompt = await ctx.ReadAsync(PromptInput, ct).ConfigureAwait(false);
        var apiKey = await GetApiKeyAsync(ct).ConfigureAwait(false);

        using var response = await SendPipeline.ExecuteAsync(
            innerCancellationToken => SendChatCompletionAsync(prompt ?? string.Empty, apiKey, innerCancellationToken),
            ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var completion = await response.Content
            .ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, ct)
            .ConfigureAwait(false);
        var choices = completion?.Choices;
        var content = choices is { Count: > 0 }
            ? choices[0].Message?.Content
            : null;

        if (content is null)
        {
            throw new InvalidOperationException("DeepSeek 响应不包含可用的回答内容。");
        }

        await ctx.WriteAsync(ResponseOutput, content, ct).ConfigureAwait(false);
    }

    private static ResiliencePipeline<HttpResponseMessage> CreateSendPipeline()
    {
        var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
        {
            MaxRetryAttempts = 2,
            Delay = TimeSpan.FromMilliseconds(100),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = false,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(static response =>
                    response.StatusCode == HttpStatusCode.TooManyRequests
                    || response.StatusCode == HttpStatusCode.RequestTimeout
                    || (int)response.StatusCode >= 500),
            OnRetry = static arguments =>
            {
                arguments.Outcome.Result?.Dispose();
                return default;
            },
        };

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(retryOptions)
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(30),
            })
            .Build();
    }

    private async ValueTask<HttpResponseMessage> SendChatCompletionAsync(string prompt, string apiKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsEndpoint)
        {
            Content = JsonContent.Create(
                new ChatCompletionRequest(
                    _config.Model,
                    _config.Temperature,
                    [new ChatMessage("user", prompt)]),
                options: JsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
    }

    private async ValueTask<string> GetApiKeyAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_config.ApiKeySecretId))
        {
            throw new SecretStoreException("DeepSeek 节点未配置 API Key 密钥引用。");
        }

        var apiKey = await _secretStore.GetSecretAsync(_config.ApiKeySecretId, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new SecretStoreException("未找到 DeepSeek 节点引用的 API Key。");
        }

        return apiKey;
    }

    private sealed record ChatCompletionRequest(string Model, double Temperature, IReadOnlyList<ChatMessage> Messages);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record ChatCompletionResponse(IReadOnlyList<ChatCompletionChoice>? Choices);

    private sealed record ChatCompletionChoice(ChatCompletionMessage? Message);

    private sealed record ChatCompletionMessage(string? Content);
}
