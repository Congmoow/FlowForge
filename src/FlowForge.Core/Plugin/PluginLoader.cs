using System.Reflection;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Serialization;

namespace FlowForge.Core.Plugin;

/// <summary>
/// 按稳定文件顺序加载显式节点插件，并隔离每个插件的程序集上下文。
/// </summary>
public sealed class PluginLoader : IDisposable
{
    private static readonly string CoreAssemblyName = typeof(INodePlugin).Assembly.GetName().Name!;
    private readonly NodeRegistry registry;
    private readonly string pluginDirectory;
    private readonly object sync = new();
    private readonly List<LoadedPluginContext> contexts = [];
    private readonly List<PluginDiagnostic> diagnostics = [];
    private bool isDisposed;

    /// <summary>
    /// 初始化插件加载器。
    /// </summary>
    /// <param name="registry">接收插件节点定义的统一目录。</param>
    /// <param name="pluginDirectory">要扫描的顶层插件目录。</param>
    public PluginLoader(NodeRegistry registry, string pluginDirectory)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        this.registry = registry;
        this.pluginDirectory = Path.GetFullPath(pluginDirectory);
    }

    /// <summary>
    /// 获取当前仍由加载器持有的可卸载上下文数量。
    /// </summary>
    public int LoadedContextCount
    {
        get
        {
            lock (sync)
            {
                return contexts.Count;
            }
        }
    }

    /// <summary>
    /// 扫描插件目录中的顶层 DLL 并返回本次加载诊断。
    /// </summary>
    /// <returns>插件加载结果。</returns>
    public PluginLoadReport LoadAll()
    {
        lock (sync)
        {
            ThrowIfDisposed();
            diagnostics.Clear();

            if (!Directory.Exists(pluginDirectory))
            {
                return CreateReport(0, 0);
            }

            string[] assemblyPaths;
            try
            {
                assemblyPaths = Directory
                    .EnumerateFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception error)
            {
                AddDiagnostic(
                    pluginDirectory,
                    PluginDiagnosticSeverity.Error,
                    $"扫描插件目录失败：{error.Message}",
                    error);
                return CreateReport(0, 0);
            }

            var loadedPluginCount = 0;
            var registeredDefinitionCount = 0;
            foreach (var assemblyPath in assemblyPaths)
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(assemblyPath), CoreAssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    AddDiagnostic(
                        assemblyPath,
                        PluginDiagnosticSeverity.Warning,
                        "跳过插件目录中的 FlowForge.Core.dll，以保持 Core 类型身份来自 Default 加载上下文。");
                    continue;
                }

                var result = LoadAssembly(assemblyPath);
                loadedPluginCount += result.LoadedPluginCount;
                registeredDefinitionCount += result.RegisteredDefinitionCount;
            }

            return CreateReport(loadedPluginCount, registeredDefinitionCount);
        }
    }

    /// <summary>
    /// 释放所有插件加载上下文；重复调用安全。
    /// </summary>
    public void Dispose()
    {
        lock (sync)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            for (var index = contexts.Count - 1; index >= 0; index--)
            {
                var loadedPlugin = contexts[index];
                registry.UnregisterRange(loadedPlugin.TypeIds);
                loadedPlugin.Context.Unload();
            }

            contexts.Clear();
        }

        GC.SuppressFinalize(this);
    }

    private LoadAssemblyResult LoadAssembly(string assemblyPath)
    {
        PluginLoadContext? context = null;
        try
        {
            context = new PluginLoadContext(assemblyPath);
            var assembly = context.LoadFromAssemblyPath(assemblyPath);
            var types = GetTypes(assembly, assemblyPath);
            var loadedPluginCount = 0;
            var registeredDefinitionCount = 0;
            var registeredTypeIds = new List<string>();

            foreach (var type in types)
            {
                if (!IsPublicPluginType(type))
                {
                    continue;
                }

                try
                {
                    if (type.GetConstructor(Type.EmptyTypes) is null)
                    {
                        AddDiagnostic(
                            assemblyPath,
                            PluginDiagnosticSeverity.Error,
                            $"插件类型 {type.FullName} 必须提供公共无参构造函数。");
                        continue;
                    }

                    var plugin = Activator.CreateInstance(type) as INodePlugin
                        ?? throw new InvalidOperationException($"插件类型 {type.FullName} 无法转换为 INodePlugin。");
                    var definitions = plugin.Definitions
                        ?? throw new InvalidOperationException($"插件类型 {type.FullName} 返回了 null 节点目录。");
                    var definitionBatch = definitions.ToArray();

                    registry.RegisterRange(definitionBatch);
                    registeredTypeIds.AddRange(definitionBatch.Select(definition => definition.TypeId));
                    loadedPluginCount++;
                    registeredDefinitionCount += definitionBatch.Length;
                }
                catch (Exception error)
                {
                    AddDiagnostic(
                        assemblyPath,
                        PluginDiagnosticSeverity.Error,
                        $"注册插件类型 {type.FullName} 失败，可能缺少插件依赖：{error.Message}",
                        error);
                }
            }

            if (loadedPluginCount > 0)
            {
                lock (sync)
                {
                    contexts.Add(new LoadedPluginContext(context, registeredTypeIds.ToArray()));
                }
            }
            else
            {
                context.Unload();
            }

            return new LoadAssemblyResult(loadedPluginCount, registeredDefinitionCount);
        }
        catch (Exception error)
        {
            AddDiagnostic(
                assemblyPath,
                PluginDiagnosticSeverity.Error,
                $"加载插件 DLL 失败：{error.Message}",
                error);
            context?.Unload();
            return new LoadAssemblyResult(0, 0);
        }
    }

    private IEnumerable<Type> GetTypes(Assembly assembly, string assemblyPath)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException error)
        {
            var detail = string.Join(
                "；",
                error.LoaderExceptions
                    .Where(exception => exception is not null)
                    .Select(exception => exception!.Message));
            AddDiagnostic(
                assemblyPath,
                PluginDiagnosticSeverity.Error,
                $"ReflectionTypeLoadException：{detail}",
                error);
            return error.Types.Where(type => type is not null).Cast<Type>();
        }
    }

    private static bool IsPublicPluginType(Type type)
    {
        return type.IsClass
            && !type.IsAbstract
            && (type.IsPublic || type.IsNestedPublic)
            && typeof(INodePlugin).IsAssignableFrom(type);
    }

    private PluginLoadReport CreateReport(int loadedPluginCount, int registeredDefinitionCount)
    {
        return new(
            loadedPluginCount,
            registeredDefinitionCount,
            Array.AsReadOnly(diagnostics.ToArray()));
    }

    private void AddDiagnostic(
        string assemblyPath,
        PluginDiagnosticSeverity severity,
        string message,
        Exception? error = null)
    {
        diagnostics.Add(new PluginDiagnostic(assemblyPath, severity, message, error));
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
    }

    private sealed record LoadAssemblyResult(int LoadedPluginCount, int RegisteredDefinitionCount);

    private sealed record LoadedPluginContext(
        PluginLoadContext Context,
        IReadOnlyList<string> TypeIds);
}
