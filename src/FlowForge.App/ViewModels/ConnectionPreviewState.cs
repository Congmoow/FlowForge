namespace FlowForge.App.ViewModels;

/// <summary>
/// 端口拖拽连线时的目标兼容状态。
/// </summary>
public enum ConnectionPreviewState
{
    /// <summary>
    /// 没有可预览的连接目标。
    /// </summary>
    None,

    /// <summary>
    /// 当前悬停目标可连接。
    /// </summary>
    Compatible,

    /// <summary>
    /// 当前悬停目标不可连接。
    /// </summary>
    Incompatible,
}
