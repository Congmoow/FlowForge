using FlowForge.Core.Plugin;
using FlowForge.Core.Serialization;

namespace FlowForge.App.Services;

/// <summary>
/// 在后台线程扫描插件目录，并在应用生命周期内持有插件加载上下文。
/// </summary>
public sealed class PluginService : IPluginService
{
    private readonly NodeRegistry registry;
    private readonly string pluginDirectory;
    private readonly object sync = new();
    private PluginLoader? loader;
    private bool isDisposed;

    /// <summary>
    /// 初始化插件服务。
    /// </summary>
    /// <param name="registry">应用共享的节点目录。</param>
    /// <param name="pluginDirectory">插件 DLL 所在目录。</param>
    public PluginService(NodeRegistry registry, string pluginDirectory)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDirectory);

        this.registry = registry;
        this.pluginDirectory = Path.GetFullPath(pluginDirectory);
    }

    /// <inheritdoc />
    public Task<PluginLoadReport> LoadAsync(CancellationToken cancellationToken = default)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            loader?.Dispose();
            loader = new PluginLoader(registry, pluginDirectory);
            var activeLoader = loader;

            return Task.Run(activeLoader.LoadAll, cancellationToken);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (sync)
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            loader?.Dispose();
            loader = null;
        }

        GC.SuppressFinalize(this);
    }
}
