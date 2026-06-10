using Avalonia;
using System;

namespace FlowForge.App;

internal static class Program
{
    // Avalonia 初始化前不要访问 UI 或依赖同步上下文的 API。
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia 设计器也会使用此配置入口。
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
