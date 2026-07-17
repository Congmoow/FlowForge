using FlowForge.Core.Serialization;

namespace FlowForge.App.Services;

/// <summary>
/// 提供工作流文件选择、读取和保存能力。
/// </summary>
public interface IWorkflowFileService
{
    /// <summary>
    /// 选择并读取一个工作流文档。
    /// </summary>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>打开结果；用户取消时返回 <see langword="null"/>。</returns>
    Task<WorkflowOpenResult?> OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存工作流文档。
    /// </summary>
    /// <param name="document">要保存的文档。</param>
    /// <param name="currentPath">当前文件路径。</param>
    /// <param name="saveAs">是否强制选择新路径。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>实际保存路径；用户取消时返回 <see langword="null"/>。</returns>
    Task<string?> SaveAsync(
        WorkflowDocument document,
        string? currentPath,
        bool saveAs,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 表示打开的工作流文档及其文件路径。
/// </summary>
/// <param name="Path">文件路径。</param>
/// <param name="Document">工作流文档。</param>
public sealed record WorkflowOpenResult(string Path, WorkflowDocument Document);
