using FlowForge.Core.Execution;
using FlowForge.Core.Graph;
using FlowForge.Core.Nodes.DataSource;
using FlowForge.Core.Nodes.Sink;

namespace FlowForge.App.Services;

/// <summary>
/// 构建并执行预置 CSV 到控制台工作流。
/// </summary>
public sealed class Stage3SampleWorkflowRunner : IStage3WorkflowRunner
{
    private readonly string csvPath;
    private readonly WorkflowScheduler scheduler;
    private readonly TextWriter writer;

    /// <summary>
    /// CSV 示例节点的稳定标识。
    /// </summary>
    public static Guid CsvNodeId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// 控制台示例节点的稳定标识。
    /// </summary>
    public static Guid ConsoleNodeId { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// 使用标准输出和应用输出目录中的预置 CSV 初始化运行器。
    /// </summary>
    /// <param name="scheduler">工作流调度器。</param>
    public Stage3SampleWorkflowRunner(WorkflowScheduler scheduler)
        : this(
            scheduler,
            Console.Out,
            Path.Combine(AppContext.BaseDirectory, "samples", "data", "stage3-people.csv"))
    {
    }

    /// <summary>
    /// 使用指定输出器和 CSV 路径初始化运行器。
    /// </summary>
    /// <param name="scheduler">工作流调度器。</param>
    /// <param name="writer">接收 Console Sink 输出的文本输出器。</param>
    /// <param name="csvPath">要读取的 CSV 文件路径。</param>
    public Stage3SampleWorkflowRunner(WorkflowScheduler scheduler, TextWriter writer, string csvPath)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);

        this.scheduler = scheduler;
        this.writer = writer;
        this.csvPath = csvPath;
    }

    /// <inheritdoc />
    public Task RunAsync(IProgress<NodeExecutionEvent> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var csv = new CsvDataSourceNode(CsvNodeId, new CsvDataSourceConfig(csvPath));
        var console = new ConsoleSinkNode(ConsoleNodeId, new ConsoleSinkNodeConfig(), writer);
        var workflow = new Workflow();
        workflow.AddNode(csv);
        workflow.AddNode(console);
        workflow.AddEdge(new WorkflowEdge(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            csv.Id,
            csv.RowsOutput.Id,
            console.Id,
            console.ValueInput.Id));

        return scheduler.ExecuteAsync(workflow, progress, cancellationToken);
    }
}
