using FlowForge.Core.Abstractions;
using FlowForge.Core.Plugin;
using FlowForge.Core.Serialization;
using FlowForge.Core.Tests.Nodes;
using FluentAssertions;

namespace FlowForge.Core.Tests.Plugin;

public sealed class SamplePluginTests
{
    [Fact]
    public async Task LoadAll_SamplePlugin_RegistersUppercaseNodeWithChinesePortsAsync()
    {
        using var directory = PluginDirectory.Create();
        directory.CopySampleAssembly();
        await AssertSamplePluginAsync(directory.Path);
    }

    private static async Task AssertSamplePluginAsync(string pluginDirectory)
    {
        var registry = new NodeRegistry();
        using var loader = new PluginLoader(registry, pluginDirectory);

        var report = loader.LoadAll();

        report.LoadedPluginCount.Should().Be(1);
        report.RegisteredDefinitionCount.Should().Be(1);
        report.Diagnostics.Should().BeEmpty();

        var definition = registry.GetDefinition("core.transform.uppercase");
        definition.Should().NotBeNull();
        definition!.Title.Should().Be("转大写");
        definition.Inputs.Should().ContainSingle(port =>
            port.Id == "text"
            && port.Name == "文本"
            && port.DataType == typeof(string));
        definition.Outputs.Should().ContainSingle(port =>
            port.Id == "result"
            && port.Name == "大写文本"
            && port.DataType == typeof(string));

        var node = definition.Create(Guid.NewGuid());
        var input = node.Inputs.OfType<IPort<string>>().Single();
        var output = node.Outputs.OfType<IPort<string>>().Single();
        var context = new TestExecutionContext();
        context.SetInput(input, "Hello, FlowForge!");

        await node.ExecuteAsync(context, CancellationToken.None);

        context.GetOutput(output).Should().Be("HELLO, FLOWFORGE!");
    }

    private sealed class PluginDirectory : IDisposable
    {
        private PluginDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static PluginDirectory Create()
        {
            return new PluginDirectory(Directory.CreateTempSubdirectory("flowforge-sample-plugin-").FullName);
        }

        public void CopySampleAssembly()
        {
            var sourcePath = GetRepositoryRootPath(
                "src",
                "FlowForge.Plugins.Sample",
                "bin",
                "Release",
                "net8.0",
                "FlowForge.Plugins.Sample.dll");
            File.Exists(sourcePath).Should().BeTrue("示例插件必须先构建为可加载 DLL");
            File.Copy(sourcePath, System.IO.Path.Combine(Path, "FlowForge.Plugins.Sample.dll"));
        }

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
            }
        }

        private static string GetRepositoryRootPath(params string[] segments)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "FlowForge.sln")))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("测试必须从 FlowForge 仓库运行");
            var pathSegments = new string[segments.Length + 1];
            pathSegments[0] = directory!.FullName;
            Array.Copy(segments, 0, pathSegments, 1, segments.Length);
            return System.IO.Path.Combine(pathSegments);
        }
    }
}
