using System.Text;
using FlowForge.App.Security;
using FluentAssertions;

namespace FlowForge.App.Tests.Security;

public sealed class WindowsDpapiSecretStoreTests : IDisposable
{
    private readonly string secretDirectory = Path.Combine(Path.GetTempPath(), "FlowForge.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SetSecretAsync_ExistingSecret_OverwritesAndDeletesEncryptedValueAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store = new WindowsDpapiSecretStore(secretDirectory);

        await store.SetSecretAsync("openai.default", "first-value");
        await store.SetSecretAsync("openai.default", "second-value");

        (await store.GetSecretAsync("openai.default")).Should().Be("second-value");
        (await store.DeleteSecretAsync("openai.default")).Should().BeTrue();
        (await store.GetSecretAsync("openai.default")).Should().BeNull();
        (await store.DeleteSecretAsync("openai.default")).Should().BeFalse();
    }

    [Fact]
    public async Task SetSecretAsync_NewSecret_WritesCiphertextWithoutPlaintextValueAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        const string secretValue = "sk-flowforge-secret-value";
        var store = new WindowsDpapiSecretStore(secretDirectory);

        await store.SetSecretAsync("openai.default", secretValue);

        var ciphertextFiles = Directory.GetFiles(secretDirectory, "*.bin");
        ciphertextFiles.Should().ContainSingle();
        var bytes = await File.ReadAllBytesAsync(ciphertextFiles[0]);
        Encoding.UTF8.GetString(bytes).Should().NotContain(secretValue);
    }

    public void Dispose()
    {
        if (Directory.Exists(secretDirectory))
        {
            Directory.Delete(secretDirectory, recursive: true);
        }
    }
}
