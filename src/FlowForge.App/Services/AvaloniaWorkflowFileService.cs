using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using FlowForge.Core.Serialization;

namespace FlowForge.App.Services;

/// <summary>
/// 使用 Avalonia 存储提供程序读写 .ffw 工作流文件。
/// </summary>
public sealed class AvaloniaWorkflowFileService : IWorkflowFileService
{
    private static readonly FilePickerFileType WorkflowFileType = new("FlowForge 工作流")
    {
        Patterns = ["*.ffw"],
    };

    /// <inheritdoc />
    public async Task<WorkflowOpenResult?> OpenAsync(CancellationToken cancellationToken = default)
    {
        var files = await GetStorageProvider().OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [WorkflowFileType],
            Title = "打开工作流",
        });
        if (files.Count == 0)
        {
            return null;
        }

        var file = files[0];

        await using var stream = await file.OpenReadAsync();
        var document = await WorkflowSerializer.LoadAsync(stream, cancellationToken).ConfigureAwait(false);
        return new WorkflowOpenResult(file.Path.LocalPath, document);
    }

    /// <inheritdoc />
    public async Task<string?> SaveAsync(
        WorkflowDocument document,
        string? currentPath,
        bool saveAs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!saveAs && !string.IsNullOrWhiteSpace(currentPath))
        {
            await using var stream = new FileStream(currentPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
            await WorkflowSerializer.SaveAsync(document, stream, cancellationToken).ConfigureAwait(false);
            return currentPath;
        }

        var file = await GetStorageProvider().SaveFilePickerAsync(new FilePickerSaveOptions
        {
            DefaultExtension = "ffw",
            FileTypeChoices = [WorkflowFileType],
            SuggestedFileName = document.Metadata.Name,
            Title = "保存工作流",
        });
        if (file is null)
        {
            return null;
        }

        await using var output = await file.OpenWriteAsync();
        await WorkflowSerializer.SaveAsync(document, output, cancellationToken).ConfigureAwait(false);
        return file.Path.LocalPath;
    }

    private static IStorageProvider GetStorageProvider()
    {
        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        return lifetime?.MainWindow?.StorageProvider
            ?? throw new InvalidOperationException("主窗口存储提供程序尚未就绪。");
    }
}
