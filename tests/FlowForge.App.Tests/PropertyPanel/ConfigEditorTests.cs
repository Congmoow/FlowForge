using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using FlowForge.App.Controls;
using FlowForge.App.Services;
using FlowForge.App.ViewModels;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Nodes.DataSource;
using FlowForge.Core.Nodes.Llm;
using FlowForge.Core.Nodes.Transform;
using FlowForge.Core.Serialization;
using FluentAssertions;

namespace FlowForge.App.Tests.PropertyPanel;

public sealed class ConfigEditorTests
{
    [Fact]
    public void BuiltInConfigs_DeclareAllSixSupportedEditorKinds()
    {
        var editorKinds = NodeRegistry.CreateDefault()
            .Definitions
            .SelectMany(definition => ConfigDescriptor.Create(definition.ConfigType).Fields)
            .Select(field => field.Editor)
            .ToHashSet(StringComparer.Ordinal);

        editorKinds.Should().BeEquivalentTo(
            "TextBox",
            "NumericUpDown",
            "ComboBox",
            "FilePicker",
            "MultilineText",
            "Password");
        ConfigFieldEditor.SupportedEditors.Should().BeEquivalentTo(editorKinds);
    }

    [Fact]
    public async Task FilePickerEditor_UsesPickerResultAndCreatesNewConfig()
    {
        var registry = NodeRegistry.CreateDefault();
        var node = registry.GetDefinition("core.datasource.text")
            .Create(Guid.NewGuid(), new TextDataSourceConfig("old.txt"));
        var canvas = new CanvasViewModel(registry);
        var viewModel = new NodeViewModel(node, registry.GetDefinition(node.TypeId), new Point(0, 0));
        canvas.Nodes.Add(viewModel);
        var picker = new FakeFilePickerService { SelectedPath = "new.txt" };
        var panel = new PropertyPanelViewModel(canvas, filePickerService: picker);
        panel.SetSelectedNode(viewModel);

        await panel.PickFileAsync(nameof(TextDataSourceConfig.FilePath));

        node.Config.Should().Be(new TextDataSourceConfig("new.txt"));
    }

    [Fact]
    public void TextBoxEditor_TracksConfigUndoAndRedoValues()
    {
        var registry = NodeRegistry.CreateDefault();
        var node = registry.GetDefinition("core.transform.text-concat")
            .Create(Guid.NewGuid(), new TextConcatNodeConfig("旧"));
        var canvas = new CanvasViewModel(registry);
        var viewModel = new NodeViewModel(node, registry.GetDefinition(node.TypeId), new Point(0, 0));
        canvas.Nodes.Add(viewModel);
        var panel = new PropertyPanelViewModel(canvas);
        panel.SetSelectedNode(viewModel);
        var field = panel.Fields.Single(item => item.PropertyName == nameof(TextConcatNodeConfig.Separator));
        var editor = new ConfigFieldEditor { Field = field };
        var textBox = editor.Content.Should().BeOfType<TextBox>().Subject;

        textBox.Text.Should().Be("旧");
        panel.EditProperty(nameof(TextConcatNodeConfig.Separator), "新").Should().BeTrue();
        textBox.Text.Should().Be("新");

        canvas.CommandHistory.Undo();
        textBox.Text.Should().Be("旧");
        canvas.CommandHistory.Redo();
        textBox.Text.Should().Be("新");
    }

    [Fact]
    public async Task PasswordEditor_StoresSecretReferenceWithoutLeakingPlaintext()
    {
        var secrets = new InMemorySecretStore();
        var registry = NodeRegistry.CreateDefault();
        var node = registry.GetDefinition("core.llm.openai")
            .Create(Guid.NewGuid(), new OpenAiLlmNodeConfig());
        var canvas = new CanvasViewModel(registry);
        var viewModel = new NodeViewModel(node, registry.GetDefinition(node.TypeId), new Point(0, 0));
        canvas.Nodes.Add(viewModel);
        var panel = new PropertyPanelViewModel(canvas, secretStore: secrets);
        panel.SetSelectedNode(viewModel);

        await panel.EditPropertyAsync(nameof(OpenAiLlmNodeConfig.ApiKeySecretId), "plain-api-key");

        var config = node.Config.Should().BeOfType<OpenAiLlmNodeConfig>().Subject;
        config.ApiKeySecretId.Should().NotBe("plain-api-key");
        config.ApiKeySecretId.Should().StartWith("secret:");
        secrets.Values[config.ApiKeySecretId].Should().Be("plain-api-key");
        panel.Fields.Single(field => field.PropertyName == nameof(OpenAiLlmNodeConfig.ApiKeySecretId))
            .Value.Should().BeNull();
        JsonSerializer.Serialize(config, WorkflowJsonSerializerOptions.Default)
            .Should().NotContain("plain-api-key");

        canvas.CommandHistory.Undo();
        node.Config.Should().Be(new OpenAiLlmNodeConfig());
        canvas.CommandHistory.Redo();
        node.Config.Should().Be(config);
    }

    [Fact]
    public async Task PasswordEditor_WhenSecretStoreFails_LeavesConfigUntouched()
    {
        var registry = NodeRegistry.CreateDefault();
        var node = registry.GetDefinition("core.llm.deepseek")
            .Create(Guid.NewGuid(), new DeepSeekLlmNodeConfig());
        var canvas = new CanvasViewModel(registry);
        var viewModel = new NodeViewModel(node, registry.GetDefinition(node.TypeId), new Point(0, 0));
        canvas.Nodes.Add(viewModel);
        var panel = new PropertyPanelViewModel(canvas, secretStore: new FailingSecretStore());
        panel.SetSelectedNode(viewModel);

        await panel.EditPropertyAsync(nameof(DeepSeekLlmNodeConfig.ApiKeySecretId), "plain-api-key");

        node.Config.Should().Be(new DeepSeekLlmNodeConfig());
        panel.ErrorMessage.Should().Be("密钥保存失败。");
        panel.ErrorMessage.Should().NotContain("plain-api-key");
    }

    [Fact]
    public async Task NumericEditor_ConvertsValueAndRejectsOutOfRangeValue()
    {
        var registry = NodeRegistry.CreateDefault();
        var node = registry.GetDefinition("core.transform.regex-extract")
            .Create(Guid.NewGuid(), new RegexExtractNodeConfig("word", 0));
        var canvas = new CanvasViewModel(registry);
        var viewModel = new NodeViewModel(node, registry.GetDefinition(node.TypeId), new Point(0, 0));
        canvas.Nodes.Add(viewModel);
        var panel = new PropertyPanelViewModel(canvas);
        panel.SetSelectedNode(viewModel);

        var converted = await panel.EditPropertyAsync(nameof(RegexExtractNodeConfig.Group), "2");
        converted.Should().BeTrue();
        panel.EditProperty(nameof(RegexExtractNodeConfig.Group), "-1").Should().BeFalse();

        node.Config.Should().Be(new RegexExtractNodeConfig("word", 2));
        panel.ErrorMessage.Should().Contain(nameof(RegexExtractNodeConfig.Group));
    }

    [Fact]
    public async Task FilePickerEditor_CancellationPropagatesWithoutChangingConfig()
    {
        var registry = NodeRegistry.CreateDefault();
        var node = registry.GetDefinition("core.datasource.text")
            .Create(Guid.NewGuid(), new TextDataSourceConfig("old.txt"));
        var canvas = new CanvasViewModel(registry);
        var viewModel = new NodeViewModel(node, registry.GetDefinition(node.TypeId), new Point(0, 0));
        canvas.Nodes.Add(viewModel);
        var panel = new PropertyPanelViewModel(canvas, filePickerService: new CanceledFilePickerService());

        panel.SetSelectedNode(viewModel);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var action = () => panel.PickFileAsync(nameof(TextDataSourceConfig.FilePath), cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        node.Config.Should().Be(new TextDataSourceConfig("old.txt"));
    }

    private sealed class FakeFilePickerService : IFilePickerService
    {
        public string? SelectedPath { get; init; }

        public Task<string?> PickFileAsync(string? currentPath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SelectedPath);
        }
    }

    private sealed class InMemorySecretStore : ISecretStore
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

        public ValueTask<string?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default)
        {
            Values.TryGetValue(secretId, out var value);
            return ValueTask.FromResult(value);
        }

        public ValueTask SetSecretAsync(string secretId, string secretValue, CancellationToken cancellationToken = default)
        {
            Values[secretId] = secretValue;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> DeleteSecretAsync(string secretId, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Values.Remove(secretId));
        }
    }

    private sealed class FailingSecretStore : ISecretStore
    {
        public ValueTask<string?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<string?>(null);
        }

        public ValueTask SetSecretAsync(string secretId, string secretValue, CancellationToken cancellationToken = default)
        {
            throw new SecretStoreException("保存失败");
        }

        public ValueTask<bool> DeleteSecretAsync(string secretId, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(false);
        }
    }

    private sealed class CanceledFilePickerService : IFilePickerService
    {
        public Task<string?> PickFileAsync(string? currentPath, CancellationToken cancellationToken = default)
        {
            return Task.FromCanceled<string?>(cancellationToken);
        }
    }
}
