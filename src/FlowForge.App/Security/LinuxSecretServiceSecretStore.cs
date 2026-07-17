using FlowForge.Core.Abstractions;

namespace FlowForge.App.Security;

/// <summary>
/// 使用 Linux Secret Service 的 <c>secret-tool</c> 命令保存密钥。
/// </summary>
public sealed class LinuxSecretServiceSecretStore : ISecretStore
{
    private const int ItemNotFoundExitCode = 1;
    private const string ApplicationAttribute = "application";
    private const string ApplicationName = "FlowForge";
    private const string SecretIdAttribute = "secret-id";
    private readonly IProcessRunner processRunner;

    /// <summary>
    /// 使用默认进程执行器初始化 Secret Service 密钥存储。
    /// </summary>
    public LinuxSecretServiceSecretStore()
        : this(new ProcessRunner())
    {
    }

    /// <summary>
    /// 使用指定进程执行器初始化 Secret Service 密钥存储。
    /// </summary>
    /// <param name="processRunner">执行 <c>secret-tool</c> 命令的进程执行器。</param>
    public LinuxSecretServiceSecretStore(IProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        this.processRunner = processRunner;
    }

    /// <inheritdoc />
    public async ValueTask<string?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default)
    {
        ValidateSecretId(secretId);
        var result = await processRunner.RunAsync(new ProcessCommand(
            "secret-tool",
            ["lookup", ApplicationAttribute, ApplicationName, SecretIdAttribute, secretId]), cancellationToken).ConfigureAwait(false);

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
            "secret-tool",
            ["store", "--label=FlowForge", ApplicationAttribute, ApplicationName, SecretIdAttribute, secretId],
            secretValue), cancellationToken).ConfigureAwait(false);

        EnsureSuccess(result, secretId, "写入");
    }

    /// <inheritdoc />
    public async ValueTask<bool> DeleteSecretAsync(string secretId, CancellationToken cancellationToken = default)
    {
        ValidateSecretId(secretId);
        var result = await processRunner.RunAsync(new ProcessCommand(
            "secret-tool",
            ["clear", ApplicationAttribute, ApplicationName, SecretIdAttribute, secretId]), cancellationToken).ConfigureAwait(false);

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
            throw new SecretStoreException($"无法通过 Linux Secret Service {operation}密钥引用 {secretId}。");
        }
    }
}
