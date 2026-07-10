using FlowForge.Core.Execution;
using FlowForge.Core.Graph;
using FlowForge.Core.Nodes.DataSource;
using FlowForge.Core.Nodes.Sink;

namespace FlowForge.App.Services;

/// <summary>
/// 构建并执行预置 CSV 到控制台工作流。
/// </summary>
public sealed class Stage3SampleWorkflowRunner(WorkflowScheduler scheduler) : IStage3WorkflowRunner
{
    /// <summary>
    /// CSV 示例节点的稳定标识。
    /// </summary>
    public static Guid CsvNodeId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// 控制台示例节点的稳定标识。
    /// </summary>
    public static Guid ConsoleNodeId { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <inheritdoc />
    public Task RunAsync(IProgress<NodeExecutionEvent> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var csvPath = Path.Combine(AppContext.BaseDirectory, "samples", "data", "stage3-people.csv");
        var csv = new CsvDataSourceNode(CsvNodeId, new CsvDataSourceConfig(csvPath));
        var console = new ConsoleSinkNode(ConsoleNodeId, new ConsoleSinkNodeConfig());
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
