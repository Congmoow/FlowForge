using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text;
using FlowForge.Core.Abstractions;

namespace FlowForge.App.Security;

/// <summary>
/// 使用 Windows CurrentUser DPAPI 加密本地密文文件的密钥存储。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsDpapiSecretStore : ISecretStore
{
    private static readonly byte[] AdditionalEntropy = Encoding.UTF8.GetBytes("FlowForge.SecretStore.v1");
    private readonly string secretDirectory;

    /// <summary>
    /// 使用当前用户应用数据目录初始化密钥存储。
    /// </summary>
    public WindowsDpapiSecretStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FlowForge",
            "secrets"))
    {
    }

    /// <summary>
    /// 使用指定密文目录初始化密钥存储。
    /// </summary>
    /// <param name="secretDirectory">保存 DPAPI 密文文件的目录。</param>
    public WindowsDpapiSecretStore(string secretDirectory)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows DPAPI 密钥存储只能在 Windows 上使用。");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(secretDirectory);
        this.secretDirectory = Path.GetFullPath(secretDirectory);
    }

    /// <inheritdoc />
    public async ValueTask<string?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default)
    {
        ValidateSecretId(secretId);
        cancellationToken.ThrowIfCancellationRequested();

        var path = GetSecretPath(secretId);
        if (!File.Exists(path))
        {
            return null;
        }

        byte[]? ciphertext = null;
        byte[]? plaintext = null;
        try
        {
            ciphertext = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            plaintext = ProtectedData.Unprotect(ciphertext, AdditionalEntropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException exception)
        {
            throw new SecretStoreException($"无法解密密钥引用 {secretId} 的本地密文。", exception);
        }
        catch (IOException exception)
        {
            throw new SecretStoreException($"无法读取密钥引用 {secretId} 的本地密文。", exception);
        }
        finally
        {
            Clear(ciphertext);
            Clear(plaintext);
        }
    }

    /// <inheritdoc />
    public async ValueTask SetSecretAsync(
        string secretId,
        string secretValue,
        CancellationToken cancellationToken = default)
    {
        ValidateSecretId(secretId);
        ArgumentNullException.ThrowIfNull(secretValue);
        cancellationToken.ThrowIfCancellationRequested();

        var path = GetSecretPath(secretId);
        var temporaryPath = Path.Combine(secretDirectory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        byte[]? plaintext = null;
        byte[]? ciphertext = null;
        try
        {
            Directory.CreateDirectory(secretDirectory);
            plaintext = Encoding.UTF8.GetBytes(secretValue);
            ciphertext = ProtectedData.Protect(plaintext, AdditionalEntropy, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(temporaryPath, ciphertext, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (CryptographicException exception)
        {
            throw new SecretStoreException($"无法加密密钥引用 {secretId}。", exception);
        }
        catch (IOException exception)
        {
            throw new SecretStoreException($"无法写入密钥引用 {secretId} 的本地密文。", exception);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            Clear(plaintext);
            Clear(ciphertext);
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> DeleteSecretAsync(string secretId, CancellationToken cancellationToken = default)
    {
        ValidateSecretId(secretId);
        cancellationToken.ThrowIfCancellationRequested();

        var path = GetSecretPath(secretId);
        if (!File.Exists(path))
        {
            return ValueTask.FromResult(false);
        }

        try
        {
            File.Delete(path);
            return ValueTask.FromResult(true);
        }
        catch (IOException exception)
        {
            throw new SecretStoreException($"无法删除密钥引用 {secretId} 的本地密文。", exception);
        }
    }

    private string GetSecretPath(string secretId)
    {
        var identifierBytes = Encoding.UTF8.GetBytes(secretId);
        try
        {
            var hash = SHA256.HashData(identifierBytes);
            return Path.Combine(secretDirectory, $"{Convert.ToHexString(hash)}.bin");
        }
        finally
        {
            Clear(identifierBytes);
        }
    }

    private static void ValidateSecretId(string secretId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretId);
    }

    private static void Clear(byte[]? value)
    {
        if (value is not null)
        {
            CryptographicOperations.ZeroMemory(value);
        }
    }
}
