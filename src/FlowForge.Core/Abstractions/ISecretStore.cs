namespace FlowForge.Core.Abstractions;

/// <summary>
/// 提供跨平台的应用密钥读写能力。
/// </summary>
public interface ISecretStore
{
    /// <summary>
    /// 按稳定引用读取密钥。
    /// </summary>
    /// <param name="secretId">密钥的不透明稳定引用。</param>
    /// <param name="cancellationToken">用于取消读取操作的令牌。</param>
    /// <returns>已保存的密钥；不存在时返回 <see langword="null"/>。</returns>
    ValueTask<string?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 新增或覆盖指定引用对应的密钥。
    /// </summary>
    /// <param name="secretId">密钥的不透明稳定引用。</param>
    /// <param name="secretValue">要安全保存的密钥值。</param>
    /// <param name="cancellationToken">用于取消写入操作的令牌。</param>
    /// <returns>表示写入操作的异步结果。</returns>
    ValueTask SetSecretAsync(
        string secretId,
        string secretValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除指定引用对应的密钥。
    /// </summary>
    /// <param name="secretId">密钥的不透明稳定引用。</param>
    /// <param name="cancellationToken">用于取消删除操作的令牌。</param>
    /// <returns>删除已有密钥时返回 <see langword="true"/>；不存在时返回 <see langword="false"/>。</returns>
    ValueTask<bool> DeleteSecretAsync(string secretId, CancellationToken cancellationToken = default);
}
