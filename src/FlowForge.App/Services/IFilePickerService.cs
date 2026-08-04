namespace FlowForge.App.Services;

/// <summary>
/// 提供可替换的文件选择能力，供属性面板使用。
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// 异步选择一个文件路径。
    /// </summary>
    /// <param name="currentPath">当前字段已有路径。</param>
    /// <param name="cancellationToken">用于取消选择的令牌。</param>
    /// <returns>选择的本地路径；取消时返回 <see langword="null"/>。</returns>
    Task<string?> PickFileAsync(
        string? currentPath,
        CancellationToken cancellationToken = default);
}
