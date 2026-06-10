using ReactiveUI;

namespace FlowForge.App.ViewModels;

/// <summary>
/// 主窗口的根 ViewModel。
/// </summary>
public sealed class MainWindowViewModel : ReactiveObject
{
    /// <summary>
    /// 应用显示名称。
    /// </summary>
    public string ApplicationName { get; } = "FlowForge";
}
