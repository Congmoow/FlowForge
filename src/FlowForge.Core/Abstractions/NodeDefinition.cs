using System.Text.Json;
using FlowForge.Core.Serialization;

namespace FlowForge.Core.Abstractions;

/// <summary>
/// 描述一个可由目录创建、序列化和展示的节点类型。
/// </summary>
public sealed class NodeDefinition
{
    private readonly Func<INodeConfig> _defaultConfigFactory;
    private readonly Func<Guid, INodeConfig, INode> _nodeFactory;
    private readonly Func<Guid, JsonElement, INode>? _jsonNodeFactory;

    private NodeDefinition(
        string typeId,
        string title,
        IReadOnlyList<PortDefinition> inputs,
        IReadOnlyList<PortDefinition> outputs,
        Type configType,
        Func<INodeConfig> defaultConfigFactory,
        Func<Guid, INodeConfig, INode> nodeFactory,
        Func<Guid, JsonElement, INode>? jsonNodeFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(configType);
        ArgumentNullException.ThrowIfNull(defaultConfigFactory);
        ArgumentNullException.ThrowIfNull(nodeFactory);

        if (!typeof(INodeConfig).IsAssignableFrom(configType))
        {
            throw new ArgumentException(
                $"配置类型必须实现 {nameof(INodeConfig)}。",
                nameof(configType));
        }

        var allPortIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var port in inputs.Concat(outputs))
        {
            ArgumentNullException.ThrowIfNull(port);
            if (!allPortIds.Add(port.Id))
            {
                throw new ArgumentException($"节点定义包含重复端口标识 {port.Id}。", nameof(inputs));
            }
        }

        TypeId = typeId;
        Title = title;
        Inputs = Array.AsReadOnly(inputs.ToArray());
        Outputs = Array.AsReadOnly(outputs.ToArray());
        ConfigType = configType;
        _defaultConfigFactory = defaultConfigFactory;
        _nodeFactory = nodeFactory;
        _jsonNodeFactory = jsonNodeFactory;
    }

    /// <summary>
    /// 获取节点稳定类型标识。
    /// </summary>
    public string TypeId { get; }

    /// <summary>
    /// 获取节点中文显示标题。
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 获取节点显示名称的兼容别名。
    /// </summary>
    public string DisplayName => Title;

    /// <summary>
    /// 获取输入端口描述。
    /// </summary>
    public IReadOnlyList<PortDefinition> Inputs { get; }

    /// <summary>
    /// 获取输出端口描述。
    /// </summary>
    public IReadOnlyList<PortDefinition> Outputs { get; }

    /// <summary>
    /// 获取节点配置的运行时类型。
    /// </summary>
    public Type ConfigType { get; }

    /// <summary>
    /// 创建一个新的默认配置实例。
    /// </summary>
    /// <returns>节点默认配置。</returns>
    public INodeConfig CreateDefaultConfig()
    {
        return _defaultConfigFactory();
    }

    /// <summary>
    /// 创建一个使用默认配置的新节点。
    /// </summary>
    /// <param name="id">节点稳定标识。</param>
    /// <returns>新节点实例。</returns>
    public INode Create(Guid id)
    {
        return Create(id, CreateDefaultConfig());
    }

    /// <summary>
    /// 使用强类型配置创建节点。
    /// </summary>
    /// <param name="id">节点稳定标识。</param>
    /// <param name="config">节点配置。</param>
    /// <returns>新节点实例。</returns>
    public INode Create(Guid id, INodeConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!ConfigType.IsInstanceOfType(config))
        {
            throw new ArgumentException(
                $"节点 {TypeId} 的配置必须是 {ConfigType.Name}。",
                nameof(config));
        }

        return _nodeFactory(id, config);
    }

    /// <summary>
    /// 从 JSON 配置创建节点。
    /// </summary>
    /// <param name="id">节点稳定标识。</param>
    /// <param name="config">节点配置 JSON。</param>
    /// <returns>新节点实例。</returns>
    public INode Create(Guid id, JsonElement config)
    {
        if (_jsonNodeFactory is not null)
        {
            return _jsonNodeFactory(id, config);
        }

        var typedConfig = config.Deserialize(ConfigType, WorkflowJsonSerializerOptions.Default) as INodeConfig;
        if (typedConfig is null)
        {
            throw new JsonException($"节点 {TypeId} 的配置 JSON 无法转换为 {ConfigType.Name}。");
        }

        return Create(id, typedConfig);
    }

    /// <summary>
    /// 创建一个使用强类型配置工厂的节点定义。
    /// </summary>
    /// <typeparam name="TConfig">节点配置类型。</typeparam>
    /// <param name="typeId">节点稳定类型标识。</param>
    /// <param name="title">节点中文显示标题。</param>
    /// <param name="nodeFactory">使用节点标识和配置创建节点的工厂。</param>
    /// <param name="defaultConfigFactory">创建默认配置的工厂。</param>
    /// <param name="inputs">输入端口描述。</param>
    /// <param name="outputs">输出端口描述。</param>
    /// <returns>节点定义。</returns>
    public static NodeDefinition Create<TConfig>(
        string typeId,
        string title,
        Func<Guid, TConfig, INode> nodeFactory,
        Func<TConfig> defaultConfigFactory,
        IReadOnlyList<PortDefinition>? inputs = null,
        IReadOnlyList<PortDefinition>? outputs = null)
        where TConfig : INodeConfig
    {
        ArgumentNullException.ThrowIfNull(nodeFactory);
        ArgumentNullException.ThrowIfNull(defaultConfigFactory);

        return new NodeDefinition(
            typeId,
            title,
            inputs ?? [],
            outputs ?? [],
            typeof(TConfig),
            () => defaultConfigFactory(),
            (id, config) => nodeFactory(id, (TConfig)config),
            null);
    }

    /// <summary>
    /// 创建一个保留旧 JSON 工厂 API 的节点定义。
    /// </summary>
    /// <param name="typeId">节点稳定类型标识。</param>
    /// <param name="factory">接收节点标识和 JSON 配置的工厂。</param>
    /// <returns>节点定义。</returns>
    public static NodeDefinition FromJsonFactory(
        string typeId,
        Func<Guid, JsonElement, INode> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return new NodeDefinition(
            typeId,
            typeId,
            [],
            [],
            typeof(INodeConfig),
            static () => throw new InvalidOperationException("旧 JSON 工厂没有默认配置。"),
            static (_, _) => throw new InvalidOperationException("旧 JSON 工厂不支持强类型配置。"),
            factory);
    }
}

/// <summary>
/// 描述节点一个端口的稳定标识、显示名称和数据类型。
/// </summary>
public sealed record PortDefinition
{
    /// <summary>
    /// 初始化端口描述并验证公共字段。
    /// </summary>
    public PortDefinition(string id, string name, Type dataType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(dataType);
        Id = id;
        Name = name;
        DataType = dataType;
    }

    /// <summary>
    /// 获取端口稳定标识。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 获取端口显示名称。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 获取端口传输的数据类型。
    /// </summary>
    public Type DataType { get; }
}
