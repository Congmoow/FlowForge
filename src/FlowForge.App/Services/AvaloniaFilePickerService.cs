using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace FlowForge.App.Services;

/// <summary>
/// 使用 Avalonia 存储提供程序选择本地文件。
/// </summary>
public sealed class AvaloniaFilePickerService : IFilePickerService
{
    /// <inheritdoc />
    public async Task<string?> PickFileAsync(
        string? currentPath,
        CancellationToken cancellationToken = default)
    {
        var provider = GetStorageProvider();
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "选择文件",
        });
        cancellationToken.ThrowIfCancellationRequested();
        return files.Count == 0 ? null : files[0].Path.LocalPath;
    }

    private static IStorageProvider GetStorageProvider()
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime;
        return lifetime?.MainWindow?.StorageProvider
            ?? throw new InvalidOperationException("主窗口存储提供程序尚未就绪。");
    }
}
