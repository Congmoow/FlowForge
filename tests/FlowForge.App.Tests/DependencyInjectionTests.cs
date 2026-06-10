using FlowForge.App.Services;
using FlowForge.App.ViewModels;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace FlowForge.App.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddFlowForgeApp_DefaultServices_ResolvesMainWindowViewModel()
    {
        var services = new ServiceCollection();

        services.AddFlowForgeApp();
        using var provider = services.BuildServiceProvider();

        var viewModel = provider.GetRequiredService<MainWindowViewModel>();

        viewModel.ApplicationName.Should().Be("FlowForge");
    }
}
