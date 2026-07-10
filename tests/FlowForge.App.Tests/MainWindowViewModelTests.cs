using System.Reactive.Threading.Tasks;
using System.Windows.Input;
using FlowForge.App.Services;
using FlowForge.App.ViewModels;
using FlowForge.Core.Execution;
using FluentAssertions;

namespace FlowForge.App.Tests;

public sealed class MainWindowViewModelTests
{
    private static readonly Guid CsvNodeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ConsoleNodeId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Constructor_DefaultPanels_ExposesThreePaneTitles()
    {
        var viewModel = new MainWindowViewModel(new FakeStage3WorkflowRunner());

        viewModel.ToolboxTitle.Should().Be("节点库");
        viewModel.CanvasTitle.Should().Be("工作流画布");
        viewModel.PropertyPanelTitle.Should().Be("属性面板");
        FindNode(viewModel, CsvNodeId).Outputs.Should().ContainSingle();
        FindNode(viewModel, CsvNodeId).Outputs[0].DataType.Should()
            .Be(typeof(IEnumerable<Dictionary<string, string>>));
    }

    [Fact]
    public async Task RunCommand_WhenWorkflowSucceeds_UpdatesNodeStatesAndStatusMessage()
    {
        var runner = new FakeStage3WorkflowRunner
        {
            RunAsyncHandler = (progress, _) =>
            {
                progress.Report(new NodeExecutionEvent(CsvNodeId, NodeExecutionStatus.Running));
                progress.Report(new NodeExecutionEvent(CsvNodeId, NodeExecutionStatus.Succeeded));
                progress.Report(new NodeExecutionEvent(ConsoleNodeId, NodeExecutionStatus.Running));
                progress.Report(new NodeExecutionEvent(ConsoleNodeId, NodeExecutionStatus.Succeeded));
                return Task.CompletedTask;
            },
        };

        var viewModel = new MainWindowViewModel(runner);

        await viewModel.RunCommand.Execute().ToTask();

        viewModel.IsRunning.Should().BeFalse();
        viewModel.StatusMessage.Should().Be("示例工作流运行完成。");
        FindNode(viewModel, CsvNodeId).ExecutionState.Should().Be(NodeExecutionVisualState.Success);
        FindNode(viewModel, ConsoleNodeId).ExecutionState.Should().Be(NodeExecutionVisualState.Success);
    }

    [Fact]
    public async Task RunCommand_WhileWorkflowRunning_DisablesRunAndEnablesStop()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new FakeStage3WorkflowRunner
        {
            RunAsyncHandler = async (progress, cancellationToken) =>
            {
                progress.Report(new NodeExecutionEvent(CsvNodeId, NodeExecutionStatus.Running));
                started.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
                progress.Report(new NodeExecutionEvent(CsvNodeId, NodeExecutionStatus.Succeeded));
            },
        };

        var viewModel = new MainWindowViewModel(runner);
        var runTask = viewModel.RunCommand.Execute().ToTask();
        await started.Task;
        await WaitUntilAsync(() => viewModel.IsRunning);

        ((ICommand)viewModel.RunCommand).CanExecute(null).Should().BeFalse();
        ((ICommand)viewModel.StopCommand).CanExecute(null).Should().BeTrue();

        release.TrySetResult();
        await runTask;

        ((ICommand)viewModel.RunCommand).CanExecute(null).Should().BeTrue();
        ((ICommand)viewModel.StopCommand).CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task RunCommand_WhenWorkflowFails_MarksFailedNodeAndReportsChineseMessage()
    {
        var runner = new FakeStage3WorkflowRunner
        {
            RunAsyncHandler = (progress, _) =>
            {
                progress.Report(new NodeExecutionEvent(CsvNodeId, NodeExecutionStatus.Running));
                progress.Report(new NodeExecutionEvent(CsvNodeId, NodeExecutionStatus.Succeeded));
                progress.Report(new NodeExecutionEvent(ConsoleNodeId, NodeExecutionStatus.Running));
                throw new WorkflowExecutionException(ConsoleNodeId, new InvalidOperationException("控制台写入失败"));
            },
        };

        var viewModel = new MainWindowViewModel(runner);

        await viewModel.RunCommand.Execute().ToTask();

        viewModel.IsRunning.Should().BeFalse();
        FindNode(viewModel, CsvNodeId).ExecutionState.Should().Be(NodeExecutionVisualState.Success);
        FindNode(viewModel, ConsoleNodeId).ExecutionState.Should().Be(NodeExecutionVisualState.Failed);
        viewModel.StatusMessage.Should().Contain("运行失败");
        viewModel.StatusMessage.Should().Contain("控制台写入失败");
    }

    [Fact]
    public async Task StopCommand_WhenWorkflowCancelled_ResetsRunningNodesToIdle()
    {
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new FakeStage3WorkflowRunner
        {
            RunAsyncHandler = async (progress, cancellationToken) =>
            {
                progress.Report(new NodeExecutionEvent(CsvNodeId, NodeExecutionStatus.Running));

                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    cancellationObserved.TrySetResult();
                    throw;
                }
            },
        };

        var viewModel = new MainWindowViewModel(runner);
        var runTask = viewModel.RunCommand.Execute().ToTask();
        await WaitUntilAsync(() => viewModel.IsRunning);

        viewModel.StopCommand.Execute().Subscribe();
        await cancellationObserved.Task;
        await runTask;

        viewModel.IsRunning.Should().BeFalse();
        viewModel.StatusMessage.Should().Be("工作流运行已取消。");
        FindNode(viewModel, CsvNodeId).ExecutionState.Should().Be(NodeExecutionVisualState.Idle);
    }

    private static NodeViewModel FindNode(MainWindowViewModel viewModel, Guid nodeId)
    {
        return viewModel.Canvas.Nodes.Single(node => node.Id == nodeId);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("等待条件超时。");
            }

            await Task.Delay(10);
        }
    }

    private sealed class FakeStage3WorkflowRunner : IStage3WorkflowRunner
    {
        public Func<IProgress<NodeExecutionEvent>, CancellationToken, Task> RunAsyncHandler { get; set; }
            = static (_, _) => Task.CompletedTask;

        public Task RunAsync(IProgress<NodeExecutionEvent> progress, CancellationToken cancellationToken)
        {
            return RunAsyncHandler(progress, cancellationToken);
        }
    }
}
