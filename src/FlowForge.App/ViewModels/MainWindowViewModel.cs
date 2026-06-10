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

    /// <summary>
    /// 左侧节点库标题。
    /// </summary>
    public string ToolboxTitle { get; } = "节点库";

    /// <summary>
    /// 中央画布标题。
    /// </summary>
    public string CanvasTitle { get; } = "工作流画布";

    /// <summary>
    /// 右侧属性面板标题。
    /// </summary>
    public string PropertyPanelTitle { get; } = "属性面板";
}
