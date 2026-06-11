using FlowForge.App.ViewModels;
using FlowForge.App.Views.Canvas;
using FluentAssertions;

namespace FlowForge.App.Tests;

public sealed class NodeViewTests
{
    [Fact]
    public void Constructor_WithNodeViewModel_AcceptsDataContext()
    {
        var node = new NodeViewModel(Guid.NewGuid(), "core.datasource.csv", "CSV 读取", 96, 80);

        var view = new NodeView
        {
            DataContext = node,
        };

        view.DataContext.Should().BeSameAs(node);
    }
}
