namespace FlowForge.App.ViewModels;

/// <summary>
/// 表示节点在画布上的执行高亮状态。
/// </summary>
public enum NodeExecutionVisualState
{
    /// <summary>节点尚未执行。</summary>
    Idle,

    /// <summary>节点正在执行。</summary>
    Running,

    /// <summary>节点执行成功。</summary>
    Success,

    /// <summary>节点执行失败。</summary>
    Failed,
}
