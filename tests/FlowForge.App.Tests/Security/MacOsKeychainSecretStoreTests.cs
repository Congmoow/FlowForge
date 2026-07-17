using FlowForge.App.Security;
using FluentAssertions;

namespace FlowForge.App.Tests.Security;

public sealed class MacOsKeychainSecretStoreTests
{
    [Fact]
    public async Task GetSecretAsync_MissingKeychainEntry_ReturnsNullAsync()
    {
        var runner = new FakeProcessRunner(new ProcessResult(44, string.Empty, "找不到指定的项。"));
        var store = new MacOsKeychainSecretStore(runner);

        var value = await store.GetSecretAsync("openai.default");

        value.Should().BeNull();
        runner.Commands.Should().ContainSingle().Which.Should().BeEquivalentTo(new ProcessCommand(
            "security",
            ["find-generic-password", "-a", "openai.default", "-s", "FlowForge", "-w"]));
    }

    [Fact]
    public async Task SetSecretAsync_NewSecret_UsesKeychainUpdateCommandAsync()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty, string.Empty));
        var store = new MacOsKeychainSecretStore(runner);

        await store.SetSecretAsync("openai.default", "sk-test-key");

        runner.Commands.Should().ContainSingle().Which.Should().BeEquivalentTo(new ProcessCommand(
            "security",
            ["add-generic-password", "-a", "openai.default", "-s", "FlowForge", "-w", "sk-test-key", "-U"]));
    }

    [Fact]
    public async Task DeleteSecretAsync_ExistingKeychainEntry_ReturnsTrueAsync()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty, string.Empty));
        var store = new MacOsKeychainSecretStore(runner);

        var deleted = await store.DeleteSecretAsync("openai.default");

        deleted.Should().BeTrue();
        runner.Commands.Should().ContainSingle().Which.Arguments.Should().Equal(
            "delete-generic-password",
            "-a",
            "openai.default",
            "-s",
            "FlowForge");
    }

    private sealed class FakeProcessRunner(params ProcessResult[] results) : IProcessRunner
    {
        private readonly Queue<ProcessResult> results = new(results);

        public List<ProcessCommand> Commands { get; } = [];

        public Task<ProcessResult> RunAsync(ProcessCommand command, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            return Task.FromResult(this.results.Dequeue());
        }
    }
}
