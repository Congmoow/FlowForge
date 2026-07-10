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
    public void Constructor_CsvTemplate_UsesCoreRowsPortType()
    {
        var viewModel = new ToolboxViewModel();

        var csv = viewModel.Templates.Single(template => template.TypeId == "core.datasource.csv");

        csv.Outputs.Should().ContainSingle();
        csv.Outputs[0].DataType.Should().Be(typeof(IEnumerable<Dictionary<string, string>>));
    }

    [Fact]
    public void Constructor_DefaultState_SelectsFirstTemplate()
    {
        var viewModel = new ToolboxViewModel();

        viewModel.SelectedTemplate.Should().BeSameAs(viewModel.Templates[0]);
    }
}
