namespace FlowForge.App.ViewModels;

/// <summary>
/// 节点选择手势。
/// </summary>
public enum SelectionGesture
{
    /// <summary>
    /// 替换当前选择。
    /// </summary>
    Replace,

    /// <summary>
    /// 追加到当前选择。
    /// </summary>
    Add,

    /// <summary>
    /// 切换当前节点选择状态。
    /// </summary>
    Toggle,
}
