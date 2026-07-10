using System.Text.Json;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Internal;

namespace FlowForge.Core.Nodes.Transform;

/// <summary>
/// 表示 JSON 解析节点的配置。
/// </summary>
public sealed record JsonParseNodeConfig : INodeConfig;

/// <summary>
/// 将文本输入解析为独立的 JSON 元素。
/// </summary>
public sealed class JsonParseNode : INode
{
    private JsonParseNodeConfig _config;

    /// <summary>
    /// 初始化使用新节点标识和默认配置的 JSON 解析节点。
    /// </summary>
    public JsonParseNode()
        : this(Guid.NewGuid(), new JsonParseNodeConfig())
    {
    }

    /// <summary>
    /// 初始化使用新节点标识和指定配置的 JSON 解析节点。
    /// </summary>
    /// <param name="config">JSON 解析配置。</param>
    public JsonParseNode(JsonParseNodeConfig config)
        : this(Guid.NewGuid(), config)
    {
    }

    /// <summary>
    /// 初始化使用指定节点标识和配置的 JSON 解析节点。
    /// </summary>
    /// <param name="id">跨会话保持稳定的节点标识。</param>
    /// <param name="config">JSON 解析配置。</param>
    public JsonParseNode(Guid id, JsonParseNodeConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Id = id;
        _config = config;
        TextInput = new NodePort<string>("text", "文本");
        JsonOutput = new NodePort<JsonElement>("json", "JSON");
        Inputs = NodePortList.Create(TextInput);
        Outputs = NodePortList.Create(JsonOutput);
    }

    /// <summary>
    /// 获取跨会话保持稳定的节点标识。
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// 获取 JSON 解析节点的稳定类型标识。
    /// </summary>
    public string TypeId => "core.transform.json-parse";

    /// <summary>
    /// 获取仅包含文本输入端口的不可变集合。
    /// </summary>
    public IReadOnlyList<IPort> Inputs { get; }

    /// <summary>
    /// 获取仅包含 JSON 输出端口的不可变集合。
    /// </summary>
    public IReadOnlyList<IPort> Outputs { get; }

    /// <summary>
    /// 获取待解析文本的输入端口。
    /// </summary>
    public IPort<string> TextInput { get; }

    /// <summary>
    /// 获取 JSON 元素输出端口。
    /// </summary>
    public IPort<JsonElement> JsonOutput { get; }

    /// <summary>
    /// 获取或设置 JSON 解析配置。
    /// </summary>
    public INodeConfig Config
    {
        get => _config;
        set => _config = value as JsonParseNodeConfig
            ?? throw new ArgumentException($"配置必须是 {nameof(JsonParseNodeConfig)}。", nameof(value));
    }

    /// <summary>
    /// 异步读取文本，解析并复制根 JSON 元素，然后写入输出端口。
    /// </summary>
    /// <param name="ctx">当前节点的执行上下文。</param>
    /// <param name="ct">用于取消输入读取和输出写入的令牌。</param>
    /// <returns>表示节点执行过程的异步结果。</returns>
    /// <exception cref="JsonException">输入为空或不是有效 JSON。</exception>
    public async ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var text = await ctx.ReadAsync(TextInput, ct).ConfigureAwait(false);
        if (text is null)
        {
            throw new JsonException("JSON 文本不能为空。");
        }

        ct.ThrowIfCancellationRequested();
        using var document = JsonDocument.Parse(text);
        var json = document.RootElement.Clone();
        await ctx.WriteAsync(JsonOutput, json, ct).ConfigureAwait(false);
    }
}
