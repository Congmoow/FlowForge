using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System.Diagnostics;
using FlowForge.App.Diagnostics;
using FlowForge.App.Services;
using FlowForge.App.ViewModels;
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
            var pluginService = serviceProvider.GetRequiredService<IPluginService>();
            desktop.Exit += (_, _) => serviceProvider.Dispose();
            _ = LoadPluginsAsync(desktop.MainWindow, pluginService);

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

    private static async Task LoadPluginsAsync(Window window, IPluginService pluginService)
    {
        try
        {
            var report = await pluginService.LoadAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (window.DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.ApplyPluginLoadReport(report);
                }
            });
        }
        catch (Exception error)
        {
            Trace.TraceError("异步加载插件失败：{0}", error);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (window.DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.ReportPluginLoadFailure(error);
                }
            });
        }
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
            if (window is not MainWindow mainWindow
                || mainWindow.DataContext is not MainWindowViewModel viewModel)
            {
                throw new InvalidOperationException("性能模式缺少主窗口 ViewModel。");
            }

            var runOptions = PerfRunOptions.From(options);
            if (runOptions.ShowHud)
            {
                viewModel.PerfHud.Start(runOptions);
            }

            var scenario = StressScenarioGenerator.Create(options.NodeCount, options.Seed);
            var result = await PerfScenarioRunner.RunAsync(
                mainWindow.CanvasControl,
                scenario,
                runOptions,
                new Progress<PerfSample>(viewModel.PerfHud.ApplySample),
                windowSize: window.Bounds.Size);
            viewModel.PerfHud.Complete(result);
            if (!string.IsNullOrWhiteSpace(options.ScreenshotPath))
            {
                mainWindow.CanvasControl.Viewport.Reset();
                PerfScreenshotExporter.Save(window, options.ScreenshotPath);
            }
            Trace.WriteLine(
                $"性能采样完成：节点={result.NodeCount}，平均 FPS={result.AverageFramesPerSecond:F2}，" +
                $"峰值内存={result.PeakWorkingSetBytes} bytes，样本={result.Samples.Count}。");
            desktop.TryShutdown(0);
        }
        catch (Exception error)
        {
            if (window is MainWindow mainWindow
                && mainWindow.DataContext is MainWindowViewModel viewModel)
            {
                viewModel.PerfHud.Fail(error);
            }

            Trace.TraceError("性能采样失败：{0}", error);
            desktop.TryShutdown(1);
        }
    }
}
