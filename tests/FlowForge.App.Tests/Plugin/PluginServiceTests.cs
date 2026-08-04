using FlowForge.App.Services;
using FlowForge.App.ViewModels;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Execution;
using FlowForge.Core.Plugin;
using FlowForge.Core.Serialization;
using FluentAssertions;

namespace FlowForge.App.Tests.Plugin;

public sealed class PluginServiceTests
{
    [Fact]
    public async Task LoadAsync_WhenDirectoryDoesNotExist_ReturnsEmptyReport()
    {
        using var directory = new TemporaryDirectory();
        using var service = new PluginService(
            NodeRegistry.CreateDefault(),
            Path.Combine(directory.Path, "plugins"));

        var report = await service.LoadAsync();

        report.LoadedPluginCount.Should().Be(0);
        report.RegisteredDefinitionCount.Should().Be(0);
        report.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        using var directory = new TemporaryDirectory();
        var service = new PluginService(NodeRegistry.CreateDefault(), directory.Path);
        service.Dispose();

        var act = () => service.LoadAsync();

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void MainWindowViewModel_ApplyPluginLoadReport_RefreshesToolboxAndStatus()
    {
        var registry = NodeRegistry.CreateDefault();
        registry.Register(NodeDefinition.FromJsonFactory(
            "plugin.test",
            static (_, _) => throw new NotSupportedException()));
        using var viewModel = new MainWindowViewModel(
            new WorkflowRunner(new WorkflowScheduler()),
            new NoOpWorkflowFileService(),
            registry);

        viewModel.ApplyPluginLoadReport(new PluginLoadReport(
            1,
            1,
            [new PluginDiagnostic("sample.dll", PluginDiagnosticSeverity.Warning, "插件测试诊断。", null)]));

        viewModel.Toolbox.Templates.Should().Contain(template => template.TypeId == "plugin.test");
        viewModel.StatusMessage.Should().Contain("插件");
        viewModel.StatusMessage.Should().Contain("插件测试诊断");
    }

    private sealed class NoOpWorkflowFileService : IWorkflowFileService
    {
        public Task<WorkflowOpenResult?> OpenAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WorkflowOpenResult?>(null);
        }

        public Task<string?> SaveAsync(
            WorkflowDocument document,
            string? currentPath,
            bool saveAs,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(currentPath);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"flowforge-app-plugin-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
