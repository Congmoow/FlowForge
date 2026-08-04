using System.Runtime.Loader;
using System.Text.Json;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Plugin;
using FlowForge.Core.Serialization;
using FluentAssertions;

namespace FlowForge.Core.Tests.Plugin;

public sealed class PluginLoaderTests
{
    [Fact]
    public void LoadAll_ExplicitPlugin_RegistersDefinitionsWithDefaultCoreIdentity()
    {
        using var directory = PluginDirectory.Create();
        directory.CopyAssembly(PluginDirectory.ValidFixturePath, "valid-plugin.dll");
        directory.CopyAssembly(PluginDirectory.DependencyFixturePath, "FlowForge.PluginFixture.Dependency.dll");
        var registry = new NodeRegistry();
        using var loader = new PluginLoader(registry, directory.Path);

        var report = loader.LoadAll();

        report.Diagnostics.Should().BeEmpty();
        registry.GetDefinition("fixture.plugin.valid").Should().NotBeNull();
        var node = registry.GetDefinition("fixture.plugin.valid").Create(Guid.NewGuid(), default(JsonElement));
        node.Should().BeAssignableTo<INode>();
        AssemblyLoadContext.GetLoadContext(typeof(INode).Assembly)
            .Should().BeSameAs(AssemblyLoadContext.Default);
    }

    [Fact]
    public void LoadAll_DuplicateDefinitions_DoesNotPartiallyRegisterPluginBatch()
    {
        using var directory = PluginDirectory.Create();
        directory.CopyAssembly(typeof(ExplicitTestPlugin).Assembly.Location, "duplicate-plugin.dll");
        var registry = new NodeRegistry();
        using var loader = new PluginLoader(registry, directory.Path);

        var report = loader.LoadAll();

        report.Diagnostics.Should().Contain(d => d.Message.Contains("重复", StringComparison.Ordinal));
        registry.TryGetDefinition("test.plugin.duplicate", out _).Should().BeFalse();
        registry.GetDefinition("test.plugin.valid").Should().NotBeNull();
    }

    [Fact]
    public void LoadAll_CorruptDll_ReportsDiagnosticAndContinues()
    {
        using var directory = PluginDirectory.Create();
        File.WriteAllBytes(Path.Combine(directory.Path, "broken.dll"), [0x46, 0x46, 0x57, 0x00]);
        directory.CopyAssembly(PluginDirectory.ValidFixturePath, "valid-plugin.dll");
        directory.CopyAssembly(PluginDirectory.DependencyFixturePath, "FlowForge.PluginFixture.Dependency.dll");
        var registry = new NodeRegistry();
        using var loader = new PluginLoader(registry, directory.Path);

        var report = loader.LoadAll();

        report.Diagnostics.Should().Contain(d => d.Message.Contains("加载", StringComparison.Ordinal));
        registry.GetDefinition("fixture.plugin.valid").Should().NotBeNull();
    }

    [Fact]
    public void LoadAll_TopLevelDlls_UsesOrdinalFileNameOrder()
    {
        using var directory = PluginDirectory.Create();
        directory.CopyAssembly(typeof(ExplicitTestPlugin).Assembly.Location, "z-plugin.dll");
        directory.CopyAssembly(typeof(ExplicitTestPlugin).Assembly.Location, "a-plugin.dll");
        var registry = new NodeRegistry();
        using var loader = new PluginLoader(registry, directory.Path);

        var report = loader.LoadAll();

        report.Diagnostics.Should().NotBeEmpty();
        Path.GetFileName(report.Diagnostics[0].AssemblyPath).Should().Be("a-plugin.dll");
    }

    [Fact]
    public void LoadAll_MissingDependency_ReportsReflectionDiagnosticWithoutThrowing()
    {
        using var directory = PluginDirectory.Create();
        directory.CopyAssembly(PluginDirectory.ReflectionFailureFixturePath, "missing-dependency.dll");
        var registry = new NodeRegistry();
        using var loader = new PluginLoader(registry, directory.Path);

        var report = loader.LoadAll();

        report.Diagnostics.Should().Contain(d =>
            d.Message.Contains("ReflectionTypeLoadException", StringComparison.Ordinal)
            || d.Message.Contains("依赖", StringComparison.Ordinal));
        report.RegisteredDefinitionCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void LoadAll_IgnoresUnrelatedNodeTypes()
    {
        using var directory = PluginDirectory.Create();
        directory.CopyAssembly(typeof(ExplicitTestPlugin).Assembly.Location, "explicit-plugin.dll");
        var registry = new NodeRegistry();
        using var loader = new PluginLoader(registry, directory.Path);

        var report = loader.LoadAll();

        report.Diagnostics.Should().NotContain(d => d.Message.Contains("不应构造", StringComparison.Ordinal));
        registry.GetDefinition("test.plugin.valid").Should().NotBeNull();
    }

    [Fact]
    public void Dispose_UnloadsAllPluginContextsAndIsIdempotent()
    {
        using var directory = PluginDirectory.Create();
        directory.CopyAssembly(PluginDirectory.ValidFixturePath, "valid-plugin.dll");
        directory.CopyAssembly(PluginDirectory.DependencyFixturePath, "FlowForge.PluginFixture.Dependency.dll");
        var loader = new PluginLoader(new NodeRegistry(), directory.Path);

        loader.LoadAll();
        loader.LoadedContextCount.Should().Be(1);
        loader.Dispose();
        loader.LoadedContextCount.Should().Be(0);
        loader.Dispose();
    }

    [Fact]
    public void Dispose_RemovesPluginDefinitionsBeforeUnloadingContext()
    {
        using var directory = PluginDirectory.Create();
        directory.CopySampleAssembly();
        var registry = new NodeRegistry();

        var weakContext = LoadSampleAndDispose(directory.Path, registry);

        registry.TryGetDefinition("core.transform.uppercase", out _).Should().BeFalse();
        for (var attempt = 0; attempt < 5 && weakContext.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        weakContext.IsAlive.Should().BeFalse();
    }

    private static WeakReference LoadSampleAndDispose(string pluginDirectory, NodeRegistry registry)
    {
        var loader = new PluginLoader(registry, pluginDirectory);
        loader.LoadAll();
        var definition = registry.GetDefinition("core.transform.uppercase");
        var context = AssemblyLoadContext.GetLoadContext(definition.ConfigType.Assembly);
        var weakContext = new WeakReference(context);

        loader.Dispose();
        return weakContext;
    }

    public sealed class ExplicitTestPlugin : INodePlugin
    {
        public IReadOnlyList<NodeDefinition> Definitions { get; } =
        [
            NodeDefinition.FromJsonFactory(
                "test.plugin.valid",
                static (id, _) => new TestPluginNode(id)),
        ];
    }

    public sealed class DuplicateDefinitionsPlugin : INodePlugin
    {
        public IReadOnlyList<NodeDefinition> Definitions { get; } =
        [
            NodeDefinition.FromJsonFactory(
                "test.plugin.duplicate",
                static (id, _) => new TestPluginNode(id)),
            NodeDefinition.FromJsonFactory(
                "test.plugin.duplicate",
                static (id, _) => new TestPluginNode(id)),
        ];
    }

    public sealed class TestPluginNode(Guid id) : INode
    {
        public Guid Id { get; } = id;

        public string TypeId => "test.plugin.valid";

        public IReadOnlyList<IPort> Inputs { get; } = [];

        public IReadOnlyList<IPort> Outputs { get; } = [];

        public INodeConfig Config { get; set; } = new TestPluginConfig();

        public ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }
    }

    public sealed record TestPluginConfig : INodeConfig;

    public sealed class UnrelatedNode : INode
    {
        public UnrelatedNode()
        {
            throw new InvalidOperationException("不应构造");
        }

        public Guid Id => Guid.Empty;

        public string TypeId => "test.unrelated";

        public IReadOnlyList<IPort> Inputs => [];

        public IReadOnlyList<IPort> Outputs => [];

        public INodeConfig Config { get; set; } = new TestPluginConfig();

        public ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class PluginDirectory : IDisposable
    {
        private PluginDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static string ReflectionFailureFixturePath =>
            GetRepositoryRootPath("tests", "FlowForge.PluginFixtures", "bin", "Release", "net8.0", "FlowForge.PluginFixtures.dll");

        public static string ValidFixturePath => ReflectionFailureFixturePath;

        public static string DependencyFixturePath =>
            GetRepositoryRootPath("tests", "FlowForge.PluginFixture.Dependency", "bin", "Release", "net8.0", "FlowForge.PluginFixture.Dependency.dll");

        public static PluginDirectory Create()
        {
            return new PluginDirectory(System.IO.Directory.CreateTempSubdirectory("flowforge-plugin-").FullName);
        }

        public void CopyAssembly(string sourcePath, string fileName)
        {
            File.Copy(sourcePath, System.IO.Path.Combine(Path, fileName));
        }

        public void CopySampleAssembly()
        {
            CopyAssembly(
                GetRepositoryRootPath(
                    "src",
                    "FlowForge.Plugins.Sample",
                    "bin",
                    "Release",
                    "net8.0",
                    "FlowForge.Plugins.Sample.dll"),
                "FlowForge.Plugins.Sample.dll");
        }

        public void Dispose()
        {
            for (var attempt = 0; attempt < 5 && System.IO.Directory.Exists(Path); attempt++)
            {
                try
                {
                    System.IO.Directory.Delete(Path, recursive: true);
                }
                catch (UnauthorizedAccessException) when (attempt < 4)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    Thread.Sleep(50);
                }
                catch (IOException) when (attempt < 4)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    Thread.Sleep(50);
                }
                catch (UnauthorizedAccessException error)
                {
                    System.Diagnostics.Trace.WriteLine($"测试临时插件目录清理延迟：{error.Message}");
                    break;
                }
                catch (IOException error)
                {
                    System.Diagnostics.Trace.WriteLine($"测试临时插件目录清理延迟：{error.Message}");
                    break;
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
