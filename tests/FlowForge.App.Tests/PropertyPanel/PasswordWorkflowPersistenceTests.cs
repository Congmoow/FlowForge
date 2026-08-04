using System.Reactive.Threading.Tasks;
using System.Text.Json;
using Avalonia;
using FlowForge.App.Services;
using FlowForge.App.ViewModels;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Execution;
using FlowForge.Core.Nodes.Llm;
using FlowForge.Core.Serialization;
using FluentAssertions;

namespace FlowForge.App.Tests.PropertyPanel;

public sealed class PasswordWorkflowPersistenceTests
{
    [Fact]
    public async Task PasswordEditor_SaveUndoAndRedo_RealFfwNeverContainsPlaintextAsync()
    {
        var directory = Directory.CreateTempSubdirectory("flowforge-password-");
        try
        {
            var path = Path.Combine(directory.FullName, "workflow.ffw");
            var files = new LocalWorkflowFileService(path);
            var secrets = new InMemorySecretStore();
            var registry = NodeRegistry.CreateDefault(secretStore: secrets);
            using var viewModel = CreateViewModel(files, registry, secrets);
            var node = AddOpenAiNode(viewModel, registry);
            viewModel.PropertyPanel.SetSelectedNode(node);

            await viewModel.PropertyPanel.EditPropertyAsync(
                nameof(OpenAiLlmNodeConfig.ApiKeySecretId),
                "plain-api-key");
            var reference = node.Config.Should().BeOfType<OpenAiLlmNodeConfig>().Subject.ApiKeySecretId;
            reference.Should().StartWith("secret:");

            await viewModel.SaveCommand.Execute().ToTask();
            AssertWorkflowContainsReferenceWithoutPlaintext(path, reference, expectedReference: reference);

            viewModel.Canvas.CommandHistory.Undo();
            await viewModel.SaveCommand.Execute().ToTask();
            AssertWorkflowContainsReferenceWithoutPlaintext(path, reference, expectedReference: string.Empty);

            viewModel.Canvas.CommandHistory.Redo();
            await viewModel.SaveCommand.Execute().ToTask();
            AssertWorkflowContainsReferenceWithoutPlaintext(path, reference, expectedReference: reference);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task PasswordEditor_FileSaveFailure_LeavesRealFfwWithoutPlaintextAsync()
    {
        var directory = Directory.CreateTempSubdirectory("flowforge-password-");
        try
        {
            var path = Path.Combine(directory.FullName, "workflow.ffw");
            await File.WriteAllTextAsync(path, "{\"schemaVersion\":1,\"nodes\":[],\"edges\":[]}");
            var files = new LocalWorkflowFileService(path) { FailSaves = true };
            var secrets = new InMemorySecretStore();
            var registry = NodeRegistry.CreateDefault(secretStore: secrets);
            using var viewModel = CreateViewModel(files, registry, secrets);
            var node = AddOpenAiNode(viewModel, registry);
            viewModel.PropertyPanel.SetSelectedNode(node);

            await viewModel.PropertyPanel.EditPropertyAsync(
                nameof(OpenAiLlmNodeConfig.ApiKeySecretId),
                "plain-api-key");

            var action = () => viewModel.SaveCommand.Execute().ToTask();

            await action.Should().ThrowAsync<IOException>();
            var persisted = await File.ReadAllTextAsync(path);
            persisted.Should().NotContain("plain-api-key");
            JsonDocument.Parse(persisted).RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task PasswordEditor_SecretStoreFailure_RealFfwSaveContainsNoPlaintextAsync()
    {
        var directory = Directory.CreateTempSubdirectory("flowforge-password-");
        try
        {
            var path = Path.Combine(directory.FullName, "workflow.ffw");
            var files = new LocalWorkflowFileService(path);
            var registry = NodeRegistry.CreateDefault();
            using var viewModel = CreateViewModel(files, registry, new FailingSecretStore());
            var node = AddOpenAiNode(viewModel, registry);
            viewModel.PropertyPanel.SetSelectedNode(node);

            var edited = await viewModel.PropertyPanel.EditPropertyAsync(
                nameof(OpenAiLlmNodeConfig.ApiKeySecretId),
                "plain-api-key");

            edited.Should().BeFalse();
            await viewModel.SaveCommand.Execute().ToTask();
            AssertWorkflowContainsReferenceWithoutPlaintext(path, "", expectedReference: string.Empty);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static MainWindowViewModel CreateViewModel(
        IWorkflowFileService files,
        NodeRegistry registry,
        ISecretStore secrets)
    {
        return new MainWindowViewModel(
            new NoOpWorkflowRunner(),
            files,
            registry,
            secrets);
    }

    private static NodeViewModel AddOpenAiNode(MainWindowViewModel viewModel, NodeRegistry registry)
    {
        var definition = registry.GetDefinition("core.llm.openai");
        var node = definition.Create(Guid.NewGuid(), new OpenAiLlmNodeConfig());
        var nodeViewModel = new NodeViewModel(node, definition, new Point(10, 20));
        viewModel.Canvas.Nodes.Add(nodeViewModel);
        return nodeViewModel;
    }

    private static void AssertWorkflowContainsReferenceWithoutPlaintext(
        string path,
        string reference,
        string expectedReference)
    {
        var persisted = File.ReadAllText(path);
        persisted.Should().NotContain("plain-api-key");
        using var document = JsonDocument.Parse(persisted);
        document.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        var config = document.RootElement
            .GetProperty("nodes")
            .EnumerateArray()
            .Single()
            .GetProperty("config");
        config.GetProperty("apiKeySecretId").GetString().Should().Be(expectedReference);
        if (expectedReference.Length > 0)
        {
            persisted.Should().Contain(reference);
        }
    }

    private sealed class InMemorySecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);

        public ValueTask<string?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default)
        {
            values.TryGetValue(secretId, out var value);
            return ValueTask.FromResult(value);
        }

        public ValueTask SetSecretAsync(
            string secretId,
            string secretValue,
            CancellationToken cancellationToken = default)
        {
            values[secretId] = secretValue;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> DeleteSecretAsync(
            string secretId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(values.Remove(secretId));
        }
    }

    private sealed class FailingSecretStore : ISecretStore
    {
        public ValueTask<string?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask SetSecretAsync(
            string secretId,
            string secretValue,
            CancellationToken cancellationToken = default)
        {
            throw new SecretStoreException("测试密钥保存失败。");
        }

        public ValueTask<bool> DeleteSecretAsync(
            string secretId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(false);
        }
    }

    private sealed class LocalWorkflowFileService(string path) : IWorkflowFileService
    {
        public bool FailSaves { get; init; }

        public Task<WorkflowOpenResult?> OpenAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public async Task<string?> SaveAsync(
            WorkflowDocument document,
            string? currentPath,
            bool saveAs,
            CancellationToken cancellationToken = default)
        {
            if (FailSaves)
            {
                throw new IOException("测试保存失败。");
            }

            await using var stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);
            await WorkflowSerializer.SaveAsync(document, stream, cancellationToken);
            return path;
        }
    }

    private sealed class NoOpWorkflowRunner : IWorkflowRunner
    {
        public Task RunAsync(
            FlowForge.Core.Graph.Workflow workflow,
            IProgress<NodeExecutionEvent> progress,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
