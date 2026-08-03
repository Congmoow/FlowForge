using System.Text.RegularExpressions;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Internal;

namespace FlowForge.Core.Nodes.Transform;

/// <summary>
/// 表示正则提取节点的配置。
/// </summary>
/// <param name="Pattern">用于匹配文本的正则表达式。</param>
/// <param name="Group">要输出的捕获组编号。</param>
public sealed record RegexExtractNodeConfig(
    string Pattern = "",
    int Group = 0) : INodeConfig;

/// <summary>
/// 使用正则表达式提取文本中的匹配项或指定捕获组。
/// </summary>
public sealed class RegexExtractNode : INode
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);
    private RegexExtractNodeConfig _config;

    /// <summary>
    /// 初始化使用新节点标识和默认配置的正则提取节点。
    /// </summary>
    public RegexExtractNode()
        : this(Guid.NewGuid(), new RegexExtractNodeConfig())
    {
    }

    /// <summary>
    /// 初始化使用新节点标识和指定配置的正则提取节点。
    /// </summary>
    /// <param name="config">正则提取配置。</param>
    public RegexExtractNode(RegexExtractNodeConfig config)
        : this(Guid.NewGuid(), config)
    {
    }

    /// <summary>
    /// 初始化使用指定节点标识和配置的正则提取节点。
    /// </summary>
    /// <param name="id">跨会话保持稳定的节点标识。</param>
    /// <param name="config">正则提取配置。</param>
    public RegexExtractNode(Guid id, RegexExtractNodeConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Id = id;
        _config = config;
        TextInput = new NodePort<string>("text", "文本");
        MatchesOutput = new NodePort<string[]>("matches", "匹配项");
        Inputs = NodePortList.Create(TextInput);
        Outputs = NodePortList.Create(MatchesOutput);
    }

    /// <summary>
    /// 获取跨会话保持稳定的节点标识。
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// 获取正则提取节点的稳定类型标识。
    /// </summary>
    public string TypeId => "core.transform.regex-extract";

    /// <summary>
    /// 获取仅包含文本输入端口的不可变集合。
    /// </summary>
    public IReadOnlyList<IPort> Inputs { get; }

    /// <summary>
    /// 获取仅包含匹配项输出端口的不可变集合。
    /// </summary>
    public IReadOnlyList<IPort> Outputs { get; }

    /// <summary>
    /// 获取待提取文本的输入端口。
    /// </summary>
    public IPort<string> TextInput { get; }

    /// <summary>
    /// 获取匹配项数组输出端口。
    /// </summary>
    public IPort<string[]> MatchesOutput { get; }

    /// <summary>
    /// 获取或设置正则提取配置。
    /// </summary>
    public INodeConfig Config
    {
        get => _config;
        set => _config = value as RegexExtractNodeConfig
            ?? throw new ArgumentException($"配置必须是 {nameof(RegexExtractNodeConfig)}。", nameof(value));
    }

    /// <summary>
    /// 异步读取文本，使用固定文化和超时时间执行匹配，然后写入提取结果。
    /// </summary>
    /// <param name="ctx">当前节点的执行上下文。</param>
    /// <param name="ct">用于取消输入读取和输出写入的令牌。</param>
    /// <returns>表示节点执行过程的异步结果。</returns>
    /// <exception cref="ArgumentException">正则表达式或捕获组编号无效。</exception>
    /// <exception cref="RegexMatchTimeoutException">正则匹配超过一秒。</exception>
    public async ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var text = await ctx.ReadAsync(TextInput, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        var regex = new Regex(
            _config.Pattern,
            RegexOptions.CultureInvariant,
            MatchTimeout);
        ValidateGroup(regex, _config.Group);

        var matches = regex.Matches(text ?? string.Empty)
            .Cast<Match>()
            .Select(match => match.Groups[_config.Group].Value)
            .ToArray();

        ct.ThrowIfCancellationRequested();
        await ctx.WriteAsync(MatchesOutput, matches, ct).ConfigureAwait(false);
    }

    private static void ValidateGroup(Regex regex, int group)
    {
        if (Array.IndexOf(regex.GetGroupNumbers(), group) < 0)
        {
            throw new ArgumentException($"正则捕获组编号无效：{group}。", nameof(group));
        }
    }
}
