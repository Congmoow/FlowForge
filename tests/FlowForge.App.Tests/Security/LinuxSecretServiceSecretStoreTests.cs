using FlowForge.App.Security;
using FluentAssertions;

namespace FlowForge.App.Tests.Security;

public sealed class LinuxSecretServiceSecretStoreTests
{
    [Fact]
    public async Task GetSecretAsync_MissingSecretServiceEntry_ReturnsNullAsync()
    {
        var runner = new FakeProcessRunner(new ProcessResult(1, string.Empty, string.Empty));
        var store = new LinuxSecretServiceSecretStore(runner);

        var value = await store.GetSecretAsync("deepseek.default");

        value.Should().BeNull();
        runner.Commands.Should().ContainSingle().Which.Should().BeEquivalentTo(new ProcessCommand(
            "secret-tool",
            ["lookup", "application", "FlowForge", "secret-id", "deepseek.default"]));
    }

    [Fact]
    public async Task SetSecretAsync_NewSecret_WritesValueToStandardInputAsync()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty, string.Empty));
        var store = new LinuxSecretServiceSecretStore(runner);

        await store.SetSecretAsync("deepseek.default", "sk-deepseek-key");

        runner.Commands.Should().ContainSingle().Which.Should().BeEquivalentTo(new ProcessCommand(
            "secret-tool",
            ["store", "--label=FlowForge", "application", "FlowForge", "secret-id", "deepseek.default"],
            "sk-deepseek-key"));
    }

    [Fact]
    public async Task DeleteSecretAsync_ExistingSecretServiceEntry_ReturnsTrueAsync()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty, string.Empty));
        var store = new LinuxSecretServiceSecretStore(runner);

        var deleted = await store.DeleteSecretAsync("deepseek.default");

        deleted.Should().BeTrue();
        runner.Commands.Should().ContainSingle().Which.Arguments.Should().Equal(
            "clear",
            "application",
            "FlowForge",
            "secret-id",
            "deepseek.default");
    }

    private sealed class FakeProcessRunner(params ProcessResult[] results) : IProcessRunner
    {
        private readonly Queue<ProcessResult> results = new(results);

        public List<ProcessCommand> Commands { get; } = [];

        public Task<ProcessResult> RunAsync(ProcessCommand command, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(command);
            return Task.FromResult(results.Dequeue());
        }
    }
}
