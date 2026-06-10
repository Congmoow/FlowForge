using FlowForge.App.ViewModels;
using FluentAssertions;

namespace FlowForge.App.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_DefaultPanels_ExposesThreePaneTitles()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.ToolboxTitle.Should().Be("节点库");
        viewModel.CanvasTitle.Should().Be("工作流画布");
        viewModel.PropertyPanelTitle.Should().Be("属性面板");
    }
}
