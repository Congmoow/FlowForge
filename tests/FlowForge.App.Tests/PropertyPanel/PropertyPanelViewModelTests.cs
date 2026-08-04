using Avalonia;
using FlowForge.App.ViewModels;
using FlowForge.Core.Nodes.DataSource;
using FlowForge.Core.Nodes.Transform;
using FlowForge.Core.Serialization;
using FluentAssertions;

namespace FlowForge.App.Tests.PropertyPanel;

public sealed class PropertyPanelViewModelTests
{
    [Fact]
    public void ConfigDescriptor_ReflectsFieldMetadataAndCreatesImmutableTypedConfig()
    {
        var descriptor = ConfigDescriptor.Create(typeof(TextDataSourceConfig));
        var field = descriptor.Fields.Single(item => item.PropertyName == nameof(TextDataSourceConfig.FilePath));
        var original = new TextDataSourceConfig("old.txt", "utf-8");

        var updated = descriptor.CreateUpdatedConfig(original, field.PropertyName, "new.txt");

        field.Label.Should().Be("文件路径");
        field.Editor.Should().Be("FilePicker");
        updated.Should().Be(new TextDataSourceConfig("new.txt", "utf-8"));
        original.Should().Be(new TextDataSourceConfig("old.txt", "utf-8"));
    }

    [Fact]
    public void PropertyPanel_SelectionSync_ExposesFieldsForSelectedNode()
    {
        var registry = NodeRegistry.CreateDefault();
        var canvas = new CanvasViewModel(registry);
        var node = registry.GetDefinition("core.transform.regex-extract")
            .Create(Guid.NewGuid(), new RegexExtractNodeConfig("word", 0));
        var nodeViewModel = new NodeViewModel(node, registry.GetDefinition(node.TypeId), new Point(0, 0));
        canvas.Nodes.Add(nodeViewModel);
        var panel = new PropertyPanelViewModel(canvas);

        canvas.SelectNodeCommand.Execute(new SelectNodeRequest(nodeViewModel.Id, SelectionGesture.Replace));

        panel.SelectedNode.Should().BeSameAs(nodeViewModel);
        panel.Fields.Select(field => field.PropertyName)
            .Should().Equal(nameof(RegexExtractNodeConfig.Pattern), nameof(RegexExtractNodeConfig.Group));
    }

    [Fact]
    public void PropertyPanel_EditProperty_UsesCommandHistoryForUndoAndRedo()
    {
        var registry = NodeRegistry.CreateDefault();
        var canvas = new CanvasViewModel(registry);
        var node = registry.GetDefinition("core.transform.text-concat")
            .Create(Guid.NewGuid(), new TextConcatNodeConfig("旧"));
        var nodeViewModel = new NodeViewModel(node, registry.GetDefinition(node.TypeId), new Point(0, 0));
        canvas.Nodes.Add(nodeViewModel);
        var panel = new PropertyPanelViewModel(canvas);
        panel.SetSelectedNode(nodeViewModel);

        panel.EditProperty(nameof(TextConcatNodeConfig.Separator), "新").Should().BeTrue();
        node.Config.Should().Be(new TextConcatNodeConfig("新"));

        canvas.CommandHistory.Undo();
        node.Config.Should().Be(new TextConcatNodeConfig("旧"));
        canvas.CommandHistory.Redo();
        node.Config.Should().Be(new TextConcatNodeConfig("新"));
    }

    [Fact]
    public void PropertyPanel_InvalidValue_LeavesConfigAndReportsError()
    {
        var registry = NodeRegistry.CreateDefault();
        var canvas = new CanvasViewModel(registry);
        var node = registry.GetDefinition("core.transform.regex-extract")
            .Create(Guid.NewGuid(), new RegexExtractNodeConfig("word", 0));
        var nodeViewModel = new NodeViewModel(node, registry.GetDefinition(node.TypeId), new Point(0, 0));
        canvas.Nodes.Add(nodeViewModel);
        var panel = new PropertyPanelViewModel(canvas);
        panel.SetSelectedNode(nodeViewModel);

        panel.EditProperty(nameof(RegexExtractNodeConfig.Group), "不是数字").Should().BeFalse();

        node.Config.Should().Be(new RegexExtractNodeConfig("word", 0));
        panel.ErrorMessage.Should().Contain("Group");
    }
}
