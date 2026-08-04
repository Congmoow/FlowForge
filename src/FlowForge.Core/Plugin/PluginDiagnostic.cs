namespace FlowForge.Core.Plugin;

/// <summary>
/// 插件加载诊断的严重级别。
/// </summary>
public enum PluginDiagnosticSeverity
{
    /// <summary>提示性诊断，不影响其他插件继续加载。</summary>
    Warning,

    /// <summary>错误性诊断，不影响其他插件或应用继续启动。</summary>
    Error,
}

/// <summary>
/// 描述一个插件加载过程中的可见诊断。
/// </summary>
/// <param name="AssemblyPath">产生诊断的 DLL 路径。</param>
/// <param name="Severity">诊断严重级别。</param>
/// <param name="Message">面向日志和状态栏的中文消息。</param>
/// <param name="Exception">可选的原始异常。</param>
public sealed record PluginDiagnostic(
    string AssemblyPath,
    PluginDiagnosticSeverity Severity,
    string Message,
    Exception? Exception = null);

/// <summary>
/// 汇总一次插件目录扫描的结果。
/// </summary>
/// <param name="LoadedPluginCount">成功读取并注册的插件实例数量。</param>
/// <param name="RegisteredDefinitionCount">成功注册的节点定义数量。</param>
/// <param name="Diagnostics">本次扫描产生的不可变诊断快照。</param>
public sealed record PluginLoadReport(
    int LoadedPluginCount,
    int RegisteredDefinitionCount,
    IReadOnlyList<PluginDiagnostic> Diagnostics);
