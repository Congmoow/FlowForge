using System.Reflection;
using System.Runtime.Loader;
using FlowForge.Core.Abstractions;

namespace FlowForge.Core.Plugin;

/// <summary>
/// 为单个插件提供依赖解析和可卸载边界。
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver dependencyResolver;
    private readonly string coreAssemblyName = typeof(INodePlugin).Assembly.GetName().Name!;

    public PluginLoadContext(string pluginAssemblyPath)
        : base($"FlowForge.Plugin:{Path.GetFileName(pluginAssemblyPath)}:{Guid.NewGuid():N}", isCollectible: true)
    {
        dependencyResolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (string.Equals(assemblyName.Name, coreAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return typeof(INodePlugin).Assembly;
        }

        var assemblyPath = dependencyResolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is null
            ? null
            : LoadFromAssemblyPath(assemblyPath);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = dependencyResolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath is null
            ? IntPtr.Zero
            : LoadUnmanagedDllFromPath(libraryPath);
    }
}
