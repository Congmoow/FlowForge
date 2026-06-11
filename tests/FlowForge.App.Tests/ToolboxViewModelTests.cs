using FlowForge.App.ViewModels;
using FluentAssertions;

namespace FlowForge.App.Tests;

public sealed class ToolboxViewModelTests
{
    [Fact]
    public void Constructor_DefaultTemplates_IncludesCoreNodeTemplates()
    {
        var viewModel = new ToolboxViewModel();

        viewModel.Templates.Should().Contain(template => template.TypeId == "core.datasource.csv");
        viewModel.Templates.Should().Contain(template => template.TypeId == "core.sink.console");
    }

    [Fact]
    public void Constructor_DefaultState_SelectsFirstTemplate()
    {
        var viewModel = new ToolboxViewModel();

        viewModel.SelectedTemplate.Should().BeSameAs(viewModel.Templates[0]);
    }
}
