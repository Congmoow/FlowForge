using FlowForge.App.Security;
using FlowForge.Core.Abstractions;
using FluentAssertions;

namespace FlowForge.App.Tests.Security;

public sealed class SecretStoreContractTests : IDisposable
{
    private readonly string secretDirectory = Path.Combine(Path.GetTempPath(), "FlowForge.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ISecretStore_WindowsDpapi_ConformsToReadWriteDeleteContractAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await VerifyContractAsync(new WindowsDpapiSecretStore(secretDirectory));
    }

    [Fact]
    public async Task ISecretStore_MacOsKeychainAdapter_ConformsToReadWriteDeleteContractAsync()
    {
        await VerifyContractAsync(new MacOsKeychainSecretStore(new MemorySecretProcessRunner()));
    }

    [Fact]
    public async Task ISecretStore_LinuxSecretServiceAdapter_ConformsToReadWriteDeleteContractAsync()
    {
        await VerifyContractAsync(new LinuxSecretServiceSecretStore(new MemorySecretProcessRunner()));
    }

    [Fact]
    public void CreateDefault_CurrentWindowsPlatform_ReturnsDpapiStore()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        SecretStoreFactory.CreateDefault().Should().BeOfType<WindowsDpapiSecretStore>();
    }

    public void Dispose()
    {
        if (Directory.Exists(secretDirectory))
        {
            Directory.Delete(secretDirectory, recursive: true);
        }
    }

    private static async Task VerifyContractAsync(ISecretStore store)
    {
        (await store.GetSecretAsync("provider.default")).Should().BeNull();
        (await store.DeleteSecretAsync("provider.default")).Should().BeFalse();

        await store.SetSecretAsync("provider.default", "first-value");
        (await store.GetSecretAsync("provider.default")).Should().Be("first-value");

        await store.SetSecretAsync("provider.default", "second-value");
        (await store.GetSecretAsync("provider.default")).Should().Be("second-value");
        (await store.DeleteSecretAsync("provider.default")).Should().BeTrue();
        (await store.GetSecretAsync("provider.default")).Should().BeNull();
    }

    private sealed class MemorySecretProcessRunner : IProcessRunner
    {
        private readonly Dictionary<string, string> secrets = new(StringComparer.Ordinal);

        public Task<ProcessResult> RunAsync(ProcessCommand command, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(command.FileName switch
            {
                "security" => RunMacOsCommand(command),
                "secret-tool" => RunLinuxCommand(command),
                _ => new ProcessResult(127, string.Empty, "未知命令。"),
            });
        }

        private ProcessResult RunMacOsCommand(ProcessCommand command)
        {
            var operation = command.Arguments[0];
            var secretId = command.Arguments[Array.IndexOf(command.Arguments.ToArray(), "-a") + 1];
            return operation switch
            {
                "find-generic-password" => Read(secretId, 44),
                "add-generic-password" => Write(secretId, command.Arguments[Array.IndexOf(command.Arguments.ToArray(), "-w") + 1]),
                "delete-generic-password" => Delete(secretId, 44),
                _ => new ProcessResult(64, string.Empty, "未知 Keychain 操作。"),
            };
        }

        private ProcessResult RunLinuxCommand(ProcessCommand command)
        {
            var operation = command.Arguments[0];
            var secretId = command.Arguments[Array.IndexOf(command.Arguments.ToArray(), "secret-id") + 1];
            return operation switch
            {
                "lookup" => Read(secretId, 1),
                "store" => Write(secretId, command.StandardInput ?? string.Empty),
                "clear" => Delete(secretId, 1),
                _ => new ProcessResult(64, string.Empty, "未知 Secret Service 操作。"),
            };
        }

        private ProcessResult Read(string secretId, int missingExitCode)
        {
            return secrets.TryGetValue(secretId, out var value)
                ? new ProcessResult(0, $"{value}{Environment.NewLine}", string.Empty)
                : new ProcessResult(missingExitCode, string.Empty, string.Empty);
        }

        private ProcessResult Write(string secretId, string value)
        {
            secrets[secretId] = value;
            return new ProcessResult(0, string.Empty, string.Empty);
        }

        private ProcessResult Delete(string secretId, int missingExitCode)
        {
            return secrets.Remove(secretId)
                ? new ProcessResult(0, string.Empty, string.Empty)
                : new ProcessResult(missingExitCode, string.Empty, string.Empty);
        }
    }
}
