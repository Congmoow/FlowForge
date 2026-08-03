using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Diagnostics;
using FlowForge.App.Diagnostics;
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

            try
            {
                if (PerfSamplingOptions.TryParse(desktop.Args ?? Array.Empty<string>(), out var parsedOptions))
                {
                    var options = parsedOptions with
                    {
                        OutputPath = parsedOptions.OutputPath
                            ?? Path.Combine("artifacts", "perf", $"fps-{parsedOptions.NodeCount}.json"),
                    };
                    desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    desktop.MainWindow.Opened += (_, _) => StartPerfRun(desktop, desktop.MainWindow, options);
                }
            }
            catch (Exception error)
            {
                Trace.TraceError("解析性能模式参数失败：{0}", error);
                desktop.TryShutdown(2);
                return;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void StartPerfRun(
        IClassicDesktopStyleApplicationLifetime desktop,
        Window window,
        PerfSamplingOptions options)
    {
        _ = RunPerfAsync(desktop, window, options);
    }

    private static async Task RunPerfAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        Window window,
        PerfSamplingOptions options)
    {
        try
        {
            var scenario = StressScenarioGenerator.Create(options.NodeCount, options.Seed);
            var result = await PerfScenarioRunner.RunAsync(
                ((MainWindow)window).CanvasControl,
                scenario,
                options);
            Trace.WriteLine(
                $"性能采样完成：节点={result.NodeCount}，平均 FPS={result.AverageFramesPerSecond:F2}，" +
                $"峰值内存={result.PeakWorkingSetBytes} bytes，样本={result.Samples.Count}。");
            desktop.TryShutdown(0);
        }
        catch (Exception error)
        {
            Trace.TraceError("性能采样失败：{0}", error);
            desktop.TryShutdown(1);
        }
    }
}
