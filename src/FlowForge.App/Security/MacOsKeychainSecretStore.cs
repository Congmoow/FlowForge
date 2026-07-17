using FlowForge.Core.Abstractions;

namespace FlowForge.App.Security;

/// <summary>
/// 使用 macOS Keychain 命令行工具保存密钥。
/// </summary>
public sealed class MacOsKeychainSecretStore : ISecretStore
{
    private const int ItemNotFoundExitCode = 44;
    private const string ServiceName = "FlowForge";
    private readonly IProcessRunner processRunner;

    /// <summary>
    /// 使用默认进程执行器初始化 Keychain 密钥存储。
    /// </summary>
    public MacOsKeychainSecretStore()
        : this(new ProcessRunner())
    {
    }

    /// <summary>
    /// 使用指定进程执行器初始化 Keychain 密钥存储。
    /// </summary>
    /// <param name="processRunner">执行系统 Keychain 命令的进程执行器。</param>
    public MacOsKeychainSecretStore(IProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        this.processRunner = processRunner;
    }

    /// <inheritdoc />
    public async ValueTask<string?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default)
    {
        ValidateSecretId(secretId);
        var result = await processRunner.RunAsync(new ProcessCommand(
            "security",
            ["find-generic-password", "-a", secretId, "-s", ServiceName, "-w"]), cancellationToken).ConfigureAwait(false);

        if (result.ExitCode == ItemNotFoundExitCode)
        {
            return null;
        }

        EnsureSuccess(result, secretId, "读取");
        return result.StandardOutput.TrimEnd('\r', '\n');
    }

    /// <inheritdoc />
    public async ValueTask SetSecretAsync(
        string secretId,
        string secretValue,
        CancellationToken cancellationToken = default)
    {
        ValidateSecretId(secretId);
        ArgumentNullException.ThrowIfNull(secretValue);
        var result = await processRunner.RunAsync(new ProcessCommand(
            "security",
            ["add-generic-password", "-a", secretId, "-s", ServiceName, "-w", secretValue, "-U"]), cancellationToken).ConfigureAwait(false);

        EnsureSuccess(result, secretId, "写入");
    }

    /// <inheritdoc />
    public async ValueTask<bool> DeleteSecretAsync(string secretId, CancellationToken cancellationToken = default)
    {
        ValidateSecretId(secretId);
        var result = await processRunner.RunAsync(new ProcessCommand(
            "security",
            ["delete-generic-password", "-a", secretId, "-s", ServiceName]), cancellationToken).ConfigureAwait(false);

        if (result.ExitCode == ItemNotFoundExitCode)
        {
            return false;
        }

        EnsureSuccess(result, secretId, "删除");
        return true;
    }

    private static void ValidateSecretId(string secretId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretId);
    }

    private static void EnsureSuccess(ProcessResult result, string secretId, string operation)
    {
        if (result.ExitCode != 0)
        {
            throw new SecretStoreException($"无法通过 macOS Keychain {operation}密钥引用 {secretId}。");
        }
    }
}
