using System.Diagnostics;
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
    public async Task LoadAsync_ConcurrentCalls_DoNotDisposeWorkBeforeItStarts()
    {
        using var directory = new TemporaryDirectory();
        File.Copy(
            GetRepositoryRootPath(
                "src",
                "FlowForge.Plugins.Sample",
                "bin",
                "Release",
                "net8.0",
            "FlowForge.Plugins.Sample.dll"),
            Path.Combine(directory.Path, "FlowForge.Plugins.Sample.dll"));
        var registry = new NodeRegistry();
        await RunConcurrentLoadsAsync(directory.Path, registry);

        registry.TryGetDefinition("core.transform.uppercase", out _).Should().BeFalse();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var deleteDirectory = () => Directory.Delete(directory.Path, recursive: true);
        deleteDirectory.Should().NotThrow();
    }

    private static async Task RunConcurrentLoadsAsync(string pluginDirectory, NodeRegistry registry)
    {
        using var service = new PluginService(registry, pluginDirectory);
        var loads = Enumerable.Range(0, 32)
            .Select(_ => service.LoadAsync())
            .ToArray();

        var act = async () => await Task.WhenAll(loads);
        await act.Should().NotThrowAsync();
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
            for (var attempt = 0; attempt < 5 && Directory.Exists(Path); attempt++)
            {
                try
                {
                    Directory.Delete(Path, recursive: true);
                }
                catch (IOException) when (attempt < 4)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    Thread.Sleep(50);
                }
                catch (UnauthorizedAccessException) when (attempt < 4)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    Thread.Sleep(50);
                }
                catch (Exception error)
                {
                    Trace.WriteLine($"测试临时插件目录清理延迟：{error.Message}");
                    break;
                }
            }
        }
    }

    private static string GetRepositoryRootPath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FlowForge.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("测试必须从 FlowForge 仓库运行");
        var pathSegments = new string[segments.Length + 1];
        pathSegments[0] = directory!.FullName;
        Array.Copy(segments, 0, pathSegments, 1, segments.Length);
        return Path.Combine(pathSegments);
    }
}
