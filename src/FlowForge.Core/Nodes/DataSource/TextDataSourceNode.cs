using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Internal;

namespace FlowForge.Core.Nodes.DataSource;

/// <summary>
/// 表示文本数据源节点的配置。
/// </summary>
/// <param name="FilePath">要读取的文本文件路径。</param>
/// <param name="Encoding">用于读取文件的编码名称。</param>
public sealed record TextDataSourceConfig(
    [property: ConfigField(Label = "文件路径", Editor = "FilePicker", Required = true, Order = 0)]
    string FilePath = "",
    [property: ConfigField(Label = "编码", Editor = "ComboBox", Options = "utf-8,utf-16,unicode", Order = 1)]
    string Encoding = "utf-8") : INodeConfig;

/// <summary>
/// 异步读取文本文件并输出完整文本内容。
/// </summary>
public sealed class TextDataSourceNode : INode
{
    private TextDataSourceConfig _config;

    /// <summary>
    /// 初始化使用新节点标识和默认配置的文本数据源节点。
    /// </summary>
    public TextDataSourceNode()
        : this(Guid.NewGuid(), new TextDataSourceConfig())
    {
    }

    /// <summary>
    /// 初始化使用新节点标识和指定配置的文本数据源节点。
    /// </summary>
    /// <param name="config">文本读取配置。</param>
    public TextDataSourceNode(TextDataSourceConfig config)
        : this(Guid.NewGuid(), config)
    {
    }

    /// <summary>
    /// 初始化使用指定节点标识和配置的文本数据源节点。
    /// </summary>
    /// <param name="id">跨会话保持稳定的节点标识。</param>
    /// <param name="config">文本读取配置。</param>
    public TextDataSourceNode(Guid id, TextDataSourceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Id = id;
        _config = config;
        ContentOutput = new NodePort<string>("content", "内容");
        Inputs = NodePortList.Empty;
        Outputs = NodePortList.Create(ContentOutput);
    }

    /// <summary>
    /// 获取跨会话保持稳定的节点标识。
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// 获取文本数据源节点的稳定类型标识。
    /// </summary>
    public string TypeId => "core.datasource.text";

    /// <summary>
    /// 获取空的输入端口集合。
    /// </summary>
    public IReadOnlyList<IPort> Inputs { get; }

    /// <summary>
    /// 获取仅包含内容输出端口的不可变集合。
    /// </summary>
    public IReadOnlyList<IPort> Outputs { get; }

    /// <summary>
    /// 获取文本内容输出端口。
    /// </summary>
    public IPort<string> ContentOutput { get; }

    /// <summary>
    /// 获取或设置文本数据源配置。
    /// </summary>
    public INodeConfig Config
    {
        get => _config;
        set => _config = value as TextDataSourceConfig
            ?? throw new ArgumentException($"配置必须是 {nameof(TextDataSourceConfig)}。", nameof(value));
    }

    /// <summary>
    /// 按配置的编码异步读取文本文件，然后写入内容输出端口。
    /// </summary>
    /// <param name="ctx">当前节点的执行上下文。</param>
    /// <param name="ct">用于取消文件读取和输出写入的令牌。</param>
    /// <returns>表示节点执行过程的异步结果。</returns>
    public async ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ct.ThrowIfCancellationRequested();

        var encoding = System.Text.Encoding.GetEncoding(_config.Encoding);
        var content = await File.ReadAllTextAsync(_config.FilePath, encoding, ct).ConfigureAwait(false);
        await ctx.WriteAsync(ContentOutput, content, ct).ConfigureAwait(false);
    }
}
