using System.Text;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.Internal;

namespace FlowForge.Core.Nodes.DataSource;

/// <summary>
/// 表示 CSV 数据源节点的配置。
/// </summary>
/// <param name="FilePath">要读取的 CSV 文件路径。</param>
/// <param name="HasHeader">是否将第一行作为列名。</param>
/// <param name="Delimiter">字段分隔符。</param>
public sealed record CsvDataSourceConfig(
    [property: ConfigField(Label = "文件路径", Editor = "FilePicker", Required = true, Order = 0)]
    string FilePath = "",
    [property: ConfigField(Label = "包含表头", Editor = "ComboBox", Options = "true,false", Order = 1)]
    bool HasHeader = true,
    [property: ConfigField(Label = "分隔符", Editor = "TextBox", Order = 2)]
    char Delimiter = ',') : INodeConfig;

/// <summary>
/// 异步读取 CSV 文件并输出按列名组织的行集合。
/// </summary>
public sealed class CsvDataSourceNode : INode
{
    private CsvDataSourceConfig _config;

    /// <summary>
    /// 初始化使用新节点标识和默认配置的 CSV 数据源节点。
    /// </summary>
    public CsvDataSourceNode()
        : this(Guid.NewGuid(), new CsvDataSourceConfig())
    {
    }

    /// <summary>
    /// 初始化使用新节点标识和指定配置的 CSV 数据源节点。
    /// </summary>
    /// <param name="config">CSV 读取配置。</param>
    public CsvDataSourceNode(CsvDataSourceConfig config)
        : this(Guid.NewGuid(), config)
    {
    }

    /// <summary>
    /// 初始化使用指定节点标识和配置的 CSV 数据源节点。
    /// </summary>
    /// <param name="id">跨会话保持稳定的节点标识。</param>
    /// <param name="config">CSV 读取配置。</param>
    public CsvDataSourceNode(Guid id, CsvDataSourceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        Id = id;
        _config = config;
        RowsOutput = new NodePort<IEnumerable<Dictionary<string, string>>>("rows", "行");
        Inputs = NodePortList.Empty;
        Outputs = NodePortList.Create(RowsOutput);
    }

    /// <summary>
    /// 获取跨会话保持稳定的节点标识。
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// 获取 CSV 数据源节点的稳定类型标识。
    /// </summary>
    public string TypeId => "core.datasource.csv";

    /// <summary>
    /// 获取空的输入端口集合。
    /// </summary>
    public IReadOnlyList<IPort> Inputs { get; }

    /// <summary>
    /// 获取仅包含行输出端口的不可变集合。
    /// </summary>
    public IReadOnlyList<IPort> Outputs { get; }

    /// <summary>
    /// 获取 CSV 行集合输出端口。
    /// </summary>
    public IPort<IEnumerable<Dictionary<string, string>>> RowsOutput { get; }

    /// <summary>
    /// 获取或设置 CSV 数据源配置。
    /// </summary>
    public INodeConfig Config
    {
        get => _config;
        set => _config = value as CsvDataSourceConfig
            ?? throw new ArgumentException($"配置必须是 {nameof(CsvDataSourceConfig)}。", nameof(value));
    }

    /// <summary>
    /// 异步读取并解析 CSV 文件，然后写入行输出端口。
    /// </summary>
    /// <param name="ctx">当前节点的执行上下文。</param>
    /// <param name="ct">用于取消文件读取和输出写入的令牌。</param>
    /// <returns>表示节点执行过程的异步结果。</returns>
    /// <exception cref="FormatException">CSV 引号格式、表头或列数无效。</exception>
    public async ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var rows = await ReadRowsAsync(_config, ct).ConfigureAwait(false);
        await ctx.WriteAsync(RowsOutput, rows, ct).ConfigureAwait(false);
    }

    private static async Task<IEnumerable<Dictionary<string, string>>> ReadRowsAsync(
        CsvDataSourceConfig config,
        CancellationToken ct)
    {
        ValidateDelimiter(config.Delimiter);
        ct.ThrowIfCancellationRequested();

        var streamOptions = new FileStreamOptions
        {
            Access = FileAccess.Read,
            Mode = FileMode.Open,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            Share = FileShare.Read,
        };

        await using var stream = new FileStream(config.FilePath, streamOptions);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);

        var rows = new List<Dictionary<string, string>>();
        var firstLine = await reader.ReadLineAsync(ct).ConfigureAwait(false);
        if (firstLine is null)
        {
            return rows;
        }

        var firstFields = ParseLine(firstLine, config.Delimiter, 1);
        IReadOnlyList<string> headers;
        var nextLineNumber = 2;

        if (config.HasHeader)
        {
            headers = firstFields;
            ValidateHeaders(headers);
        }
        else
        {
            headers = Enumerable.Range(1, firstFields.Count)
                .Select(index => $"Column{index}")
                .ToArray();
            rows.Add(CreateRow(headers, firstFields, 1));
        }

        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            var fields = ParseLine(line, config.Delimiter, nextLineNumber);
            rows.Add(CreateRow(headers, fields, nextLineNumber));
            nextLineNumber++;
        }

        return rows;
    }

    private static void ValidateDelimiter(char delimiter)
    {
        if (delimiter is '\r' or '\n' or '"')
        {
            throw new FormatException("CSV 分隔符不能是换行符或双引号。");
        }
    }

    private static void ValidateHeaders(IReadOnlyList<string> headers)
    {
        var uniqueHeaders = new HashSet<string>(StringComparer.Ordinal);
        foreach (var header in headers)
        {
            if (!uniqueHeaders.Add(header))
            {
                throw new FormatException($"CSV 表头包含重复列名：{header}。");
            }
        }
    }

    private static Dictionary<string, string> CreateRow(
        IReadOnlyList<string> headers,
        List<string> fields,
        int lineNumber)
    {
        if (fields.Count != headers.Count)
        {
            throw new FormatException(
                $"CSV 第 {lineNumber} 行包含 {fields.Count} 列，预期为 {headers.Count} 列。");
        }

        var row = new Dictionary<string, string>(headers.Count, StringComparer.Ordinal);
        for (var index = 0; index < headers.Count; index++)
        {
            row.Add(headers[index], fields[index]);
        }

        return row;
    }

    private static List<string> ParseLine(string line, char delimiter, int lineNumber)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var quoteClosed = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];

            if (inQuotes)
            {
                if (character != '"')
                {
                    field.Append(character);
                    continue;
                }

                if (index + 1 < line.Length && line[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                    continue;
                }

                inQuotes = false;
                quoteClosed = true;
                continue;
            }

            if (quoteClosed)
            {
                if (character != delimiter)
                {
                    throw new FormatException($"CSV 第 {lineNumber} 行的结束引号后存在非法字符。");
                }

                fields.Add(field.ToString());
                field.Clear();
                quoteClosed = false;
                continue;
            }

            if (character == delimiter)
            {
                fields.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (character == '"')
            {
                if (field.Length != 0)
                {
                    throw new FormatException($"CSV 第 {lineNumber} 行的双引号位置无效。");
                }

                inQuotes = true;
                continue;
            }

            field.Append(character);
        }

        if (inQuotes)
        {
            throw new FormatException($"CSV 第 {lineNumber} 行包含未闭合引号，不支持跨行引号字段。");
        }

        fields.Add(field.ToString());
        return fields;
    }
}
