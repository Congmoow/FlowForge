using System.Collections;
using System.Text.Json;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.DataSource;
using FlowForge.Core.Nodes.Llm;
using FlowForge.Core.Nodes.Sink;
using FlowForge.Core.Nodes.Transform;

namespace FlowForge.Core.Serialization;

/// <summary>
/// 根据稳定类型标识创建工作流节点。
/// </summary>
public sealed class NodeRegistry
{
    private static readonly Lazy<HttpClient> SharedHttpClient = new(static () => new HttpClient());
    private readonly Dictionary<string, NodeDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    /// <summary>
    /// 获取按注册顺序排列的节点定义快照。
    /// </summary>
    public IReadOnlyList<NodeDefinition> Definitions
    {
        get
        {
            lock (_sync)
            {
                return _definitions.Values.ToArray();
            }
        }
    }

    /// <summary>
    /// 创建包含全部内置节点的默认目录。
    /// </summary>
    /// <param name="secretStore">LLM 节点使用的密钥存储。</param>
    /// <param name="httpClient">LLM 节点使用的 HTTP 客户端。</param>
    /// <param name="consoleWriter">控制台输出节点使用的文本写入器。</param>
    /// <returns>已注册内置节点的目录。</returns>
    public static NodeRegistry CreateDefault(
        ISecretStore? secretStore = null,
        HttpClient? httpClient = null,
        TextWriter? consoleWriter = null)
    {
        var effectiveSecretStore = secretStore ?? UnavailableSecretStore.Instance;
        var effectiveHttpClient = httpClient ?? SharedHttpClient.Value;
        var effectiveConsoleWriter = consoleWriter ?? Console.Out;
        var registry = new NodeRegistry();

        registry.RegisterRange([
            NodeDefinition.Create<CsvDataSourceConfig>(
                "core.datasource.csv",
                "CSV 读取",
                static (id, config) => new CsvDataSourceNode(id, config),
                static () => new CsvDataSourceConfig(),
                outputs: [new PortDefinition("rows", "行", typeof(IEnumerable<Dictionary<string, string>>))]),
            NodeDefinition.Create<TextDataSourceConfig>(
                "core.datasource.text",
                "文本读取",
                static (id, config) => new TextDataSourceNode(id, config),
                static () => new TextDataSourceConfig(),
                outputs: [new PortDefinition("content", "内容", typeof(string))]),
            NodeDefinition.Create<JsonParseNodeConfig>(
                "core.transform.json-parse",
                "JSON 解析",
                static (id, config) => new JsonParseNode(id, config),
                static () => new JsonParseNodeConfig(),
                inputs: [new PortDefinition("text", "文本", typeof(string))],
                outputs: [new PortDefinition("json", "JSON", typeof(JsonElement))]),
            NodeDefinition.Create<TextConcatNodeConfig>(
                "core.transform.text-concat",
                "文本拼接",
                static (id, config) => new TextConcatNode(id, config),
                static () => new TextConcatNodeConfig(),
                inputs: [new PortDefinition("parts", "文本片段", typeof(string[]))],
                outputs: [new PortDefinition("result", "结果", typeof(string))]),
            NodeDefinition.Create<ForeachNodeConfig>(
                "core.transform.foreach",
                "遍历",
                static (id, config) => new ForeachNode(id, config),
                static () => new ForeachNodeConfig(),
                inputs: [new PortDefinition("items", "集合", typeof(IEnumerable))],
                outputs: [new PortDefinition("item", "项目", typeof(object))]),
            NodeDefinition.Create<RegexExtractNodeConfig>(
                "core.transform.regex-extract",
                "正则提取",
                static (id, config) => new RegexExtractNode(id, config),
                static () => new RegexExtractNodeConfig(),
                inputs: [new PortDefinition("text", "文本", typeof(string))],
                outputs: [new PortDefinition("matches", "匹配项", typeof(string[]))]),
            NodeDefinition.Create<OpenAiLlmNodeConfig>(
                "core.llm.openai",
                "OpenAI 调用",
                (id, config) => new OpenAiLlmNode(id, config, effectiveSecretStore, effectiveHttpClient),
                static () => new OpenAiLlmNodeConfig(),
                inputs: [new PortDefinition("prompt", "提示词", typeof(string))],
                outputs: [new PortDefinition("response", "回答", typeof(string))]),
            NodeDefinition.Create<DeepSeekLlmNodeConfig>(
                "core.llm.deepseek",
                "DeepSeek 调用",
                (id, config) => new DeepSeekLlmNode(id, config, effectiveSecretStore, effectiveHttpClient),
                static () => new DeepSeekLlmNodeConfig(),
                inputs: [new PortDefinition("prompt", "提示词", typeof(string))],
                outputs: [new PortDefinition("response", "回答", typeof(string))]),
            NodeDefinition.Create<ConsoleSinkNodeConfig>(
                "core.sink.console",
                "控制台输出",
                (id, config) => new ConsoleSinkNode(id, config, effectiveConsoleWriter),
                static () => new ConsoleSinkNodeConfig(),
                inputs: [new PortDefinition("value", "值", typeof(object))]),
            NodeDefinition.Create<FileWriterSinkNodeConfig>(
                "core.sink.file-writer",
                "写文件",
                static (id, config) => new FileWriterSinkNode(id, config),
                static () => new FileWriterSinkNodeConfig(),
                inputs: [new PortDefinition("content", "内容", typeof(string))]),
        ]);

        return registry;
    }

    /// <summary>
    /// 注册一个节点类型的创建工厂。
    /// </summary>
    /// <param name="typeId">节点稳定类型标识。</param>
    /// <param name="factory">接收节点标识和配置 JSON 的创建工厂。</param>
    /// <exception cref="InvalidOperationException">类型标识已注册。</exception>
    public void Register(string typeId, Func<Guid, JsonElement, INode> factory)
    {
        Register(NodeDefinition.FromJsonFactory(typeId, factory));
    }

    /// <summary>
    /// 注册一个节点定义。
    /// </summary>
    /// <param name="definition">要注册的节点定义。</param>
    /// <exception cref="InvalidOperationException">类型标识已注册。</exception>
    public void Register(NodeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RegisterRange([definition]);
    }

    /// <summary>
    /// 原子注册一批节点定义。
    /// </summary>
    /// <param name="definitions">要注册的节点定义集合。</param>
    /// <exception cref="InvalidOperationException">批次内或目录中存在重复类型标识。</exception>
    public void RegisterRange(IEnumerable<NodeDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var batch = definitions.ToArray();
        if (batch.Any(static definition => definition is null))
        {
            throw new ArgumentException("节点定义集合不能包含 null。", nameof(definitions));
        }

        var batchIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in batch)
        {
            if (!batchIds.Add(definition.TypeId))
            {
                throw new InvalidOperationException($"节点类型 {definition.TypeId} 在注册批次中重复。");
            }
        }

        lock (_sync)
        {
            var existing = batch.FirstOrDefault(definition => _definitions.ContainsKey(definition.TypeId));
            if (existing is not null)
            {
                throw new InvalidOperationException($"节点类型 {existing.TypeId} 已注册。");
            }

            foreach (var definition in batch)
            {
                _definitions.Add(definition.TypeId, definition);
            }
        }
    }

    /// <summary>
    /// 原子移除一批节点定义。
    /// </summary>
    /// <param name="typeIds">要移除的节点类型标识。</param>
    internal void UnregisterRange(IEnumerable<string> typeIds)
    {
        ArgumentNullException.ThrowIfNull(typeIds);
        var ids = typeIds.ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("节点类型标识不能包含空值。", nameof(typeIds));
        }

        lock (_sync)
        {
            foreach (var typeId in ids)
            {
                _definitions.Remove(typeId);
            }
        }
    }

    /// <summary>
    /// 按稳定类型标识获取节点定义。
    /// </summary>
    /// <param name="typeId">节点稳定类型标识。</param>
    /// <returns>节点定义。</returns>
    public NodeDefinition GetDefinition(string typeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);

        lock (_sync)
        {
            if (_definitions.TryGetValue(typeId, out var definition))
            {
                return definition;
            }
        }

        throw new InvalidOperationException($"节点类型 {typeId} 未注册。");
    }

    /// <summary>
    /// 尝试按稳定类型标识获取节点定义。
    /// </summary>
    /// <param name="typeId">节点稳定类型标识。</param>
    /// <param name="definition">找到的节点定义。</param>
    /// <returns>找到定义时返回 <see langword="true"/>。</returns>
    public bool TryGetDefinition(string typeId, out NodeDefinition? definition)
    {
        if (string.IsNullOrWhiteSpace(typeId))
        {
            definition = null;
            return false;
        }

        lock (_sync)
        {
            return _definitions.TryGetValue(typeId, out definition);
        }
    }

    /// <summary>
    /// 根据节点文档创建节点实例。
    /// </summary>
    /// <param name="document">要转换的节点文档。</param>
    /// <returns>使用文档标识和配置创建的节点。</returns>
    /// <exception cref="InvalidOperationException">节点类型未注册。</exception>
    public INode Create(WorkflowNodeDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return GetDefinition(document.TypeId).Create(document.Id, document.Config);
    }

    private sealed class UnavailableSecretStore : ISecretStore
    {
        public static UnavailableSecretStore Instance { get; } = new();

        public ValueTask<string?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask SetSecretAsync(
            string secretId,
            string secretValue,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("默认节点目录未配置密钥存储。");
        }

        public ValueTask<bool> DeleteSecretAsync(string secretId, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(false);
        }
    }
}
