using System.Text;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Internal;

namespace FlowForge.Core.Nodes.Sink;

/// <summary>
/// 表示文件写入节点的不可变配置。
/// </summary>
/// <param name="FilePath">要写入的目标文件路径。</param>
/// <param name="Append">是否保留已有内容并在文件尾部追加。</param>
public sealed record FileWriterSinkNodeConfig(
    [property: ConfigField(Label = "文件路径", Editor = "FilePicker", Required = true, Order = 0)]
    string FilePath = "",
    [property: ConfigField(Label = "追加写入", Editor = "ComboBox", Options = "true,false", Order = 1)]
    bool Append = false) : INodeConfig;

/// <summary>
/// 将字符串输入流原文写入 UTF-8 无 BOM 文件。
/// </summary>
public sealed class FileWriterSinkNode : INode
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private FileWriterSinkNodeConfig _config;

    /// <summary>
    /// 初始化使用新节点标识和默认配置的文件写入节点。
    /// </summary>
    public FileWriterSinkNode()
        : this(Guid.NewGuid(), new FileWriterSinkNodeConfig())
    {
    }

    /// <summary>
    /// 初始化使用新节点标识和指定配置的文件写入节点。
    /// </summary>
    /// <param name="config">文件写入配置。</param>
    public FileWriterSinkNode(FileWriterSinkNodeConfig config)
        : this(Guid.NewGuid(), config)
    {
    }

    /// <summary>
    /// 初始化使用指定节点标识和配置的文件写入节点。
    /// </summary>
    /// <param name="id">跨会话保持稳定的节点标识。</param>
    /// <param name="config">文件写入配置。</param>
    public FileWriterSinkNode(Guid id, FileWriterSinkNodeConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Id = id;
        _config = config;
        ContentInput = new NodePort<string>("content", "内容");
        Inputs = NodePortList.Create(ContentInput);
        Outputs = NodePortList.Empty;
    }

    /// <summary>获取跨会话保持稳定的节点标识。</summary>
    public Guid Id { get; }

    /// <summary>获取文件写入节点的稳定类型标识。</summary>
    public string TypeId => "core.sink.file-writer";

    /// <summary>获取仅包含内容输入端口的不可变集合。</summary>
    public IReadOnlyList<IPort> Inputs { get; }

    /// <summary>获取空的输出端口集合。</summary>
    public IReadOnlyList<IPort> Outputs { get; }

    /// <summary>获取待写入内容的字符串输入端口。</summary>
    public IPort<string> ContentInput { get; }

    /// <summary>获取或设置文件写入配置。</summary>
    public INodeConfig Config
    {
        get => _config;
        set => _config = value as FileWriterSinkNodeConfig
            ?? throw new ArgumentException(
                $"配置必须是 {nameof(FileWriterSinkNodeConfig)}。",
                nameof(value));
    }

    /// <summary>
    /// 在执行期间打开一次目标文件并逐项写入字符串，不自动追加换行。
    /// </summary>
    /// <param name="ctx">当前节点的执行上下文。</param>
    /// <param name="ct">用于取消读取和写入的令牌。</param>
    /// <returns>表示节点执行过程的异步结果。</returns>
    public async ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ct.ThrowIfCancellationRequested();

        var mode = _config.Append ? FileMode.Append : FileMode.Create;
        await using var stream = new FileStream(
            _config.FilePath,
            mode,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        await using var writer = new StreamWriter(stream, Utf8WithoutBom, 4096, leaveOpen: false);

        await foreach (var value in ctx.ReadAllAsync(ContentInput, ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteAsync((value ?? string.Empty).AsMemory(), ct).ConfigureAwait(false);
        }

        await writer.FlushAsync(ct).ConfigureAwait(false);
    }
}
