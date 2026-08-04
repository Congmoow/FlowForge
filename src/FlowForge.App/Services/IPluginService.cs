using FlowForge.Core.Plugin;

namespace FlowForge.App.Services;

/// <summary>
/// 管理应用启动期间的插件扫描和应用关闭时的插件上下文释放。
/// </summary>
public interface IPluginService : IDisposable
{
    /// <summary>
    /// 异步扫描应用插件目录并注册可用节点定义。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次扫描的不可变诊断报告。</returns>
    Task<PluginLoadReport> LoadAsync(CancellationToken cancellationToken = default);
}
