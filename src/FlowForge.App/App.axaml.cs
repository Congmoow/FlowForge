using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FlowForge.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FlowForge.App;

/// <summary>
/// FlowForge Avalonia 应用入口。
/// </summary>
public partial class App : Application
{
    private ServiceProvider? serviceProvider;

    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddFlowForgeApp();
        serviceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = serviceProvider.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
