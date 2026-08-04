using System.Collections.ObjectModel;
using System.Diagnostics;
using FlowForge.App.Commands;
using FlowForge.App.Services;
using FlowForge.Core.Abstractions;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace FlowForge.App.ViewModels;

/// <summary>
/// 根据当前选中节点的 immutable 配置生成属性字段，并通过命令历史提交修改。
/// </summary>
public sealed class PropertyPanelViewModel : ReactiveObject, IDisposable
{
    private readonly CanvasViewModel? canvas;
    private readonly CommandHistory commandHistory;
    private readonly ISecretStore secretStore;
    private readonly IFilePickerService filePickerService;
    private ConfigDescriptor? descriptor;
    private bool isDisposed;

    /// <summary>
    /// 初始化未绑定画布的属性面板。
    /// </summary>
    public PropertyPanelViewModel()
        : this(null)
    {
    }

    /// <summary>
    /// 绑定指定画布的属性面板。
    /// </summary>
    /// <param name="canvas">提供选中节点的画布。</param>
    /// <param name="commandHistory">可选命令历史；未提供时使用画布历史或新历史。</param>
    /// <param name="secretStore">可选的密钥存储。</param>
    /// <param name="filePickerService">可选的文件选择服务。</param>
    public PropertyPanelViewModel(
        CanvasViewModel? canvas,
        CommandHistory? commandHistory = null,
        ISecretStore? secretStore = null,
        IFilePickerService? filePickerService = null)
    {
        this.canvas = canvas;
        this.commandHistory = commandHistory ?? canvas?.CommandHistory ?? new CommandHistory();
        this.secretStore = secretStore ?? UnavailableSecretStore.Instance;
        this.filePickerService = filePickerService ?? UnavailableFilePickerService.Instance;
        if (canvas is not null)
        {
            canvas.PropertyChanged += OnCanvasPropertyChanged;
        }

        this.commandHistory.Changed += OnCommandHistoryChanged;
    }

    /// <summary>
    /// 当前属性面板绑定的节点。
    /// </summary>
    [Reactive]
    public NodeViewModel? SelectedNode { get; private set; }

    /// <summary>
    /// 当前节点的可编辑字段。
    /// </summary>
    public ObservableCollection<ConfigFieldViewModel> Fields { get; } = [];

    /// <summary>
    /// 最近一次编辑错误；成功编辑后清空。
    /// </summary>
    [Reactive]
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// 设置当前选中节点并重建字段集合。
    /// </summary>
    /// <param name="node">要显示的节点；为空时清空面板。</param>
    public void SetSelectedNode(NodeViewModel? node)
    {
        SelectedNode = node;
        descriptor = node is null ? null : ConfigDescriptor.Create(node.Config.GetType());
        Fields.Clear();
        ErrorMessage = null;

        if (node is null || descriptor is null)
        {
            return;
        }

        foreach (var field in descriptor.Fields)
        {
            var fieldViewModel = new ConfigFieldViewModel(field, field.GetValue(node.Config));
            fieldViewModel.Configure(
                (value, cancellationToken) => EditPropertyAsync(field.PropertyName, value, cancellationToken),
                cancellationToken => PickFileAsync(field.PropertyName, cancellationToken));
            Fields.Add(fieldViewModel);
        }
    }

    /// <summary>
    /// 将编辑器原始值转换为 typed config，并以可撤销命令提交。
    /// </summary>
    /// <param name="propertyName">配置属性名。</param>
    /// <param name="rawValue">编辑器提供的原始值。</param>
    /// <returns>成功提交时返回 <see langword="true"/>。</returns>
    public bool EditProperty(string propertyName, object? rawValue)
    {
        if (descriptor?.TryGetField(propertyName, out var field) == true
            && field is not null
            && string.Equals(field.Editor, "Password", StringComparison.Ordinal))
        {
            ErrorMessage = "密码字段必须通过异步密钥编辑器提交。";
            return false;
        }

        return EditPropertyCore(propertyName, rawValue);
    }

    /// <summary>
    /// 异步提交配置字段；Password 字段先写入密钥存储，再保存稳定引用。
    /// </summary>
    /// <param name="propertyName">配置属性名。</param>
    /// <param name="rawValue">编辑器原始值。</param>
    /// <param name="cancellationToken">用于取消密钥或文件操作的令牌。</param>
    /// <returns>成功提交时返回 <see langword="true"/>。</returns>
    public async Task<bool> EditPropertyAsync(
        string propertyName,
        object? rawValue,
        CancellationToken cancellationToken = default)
    {
        if (descriptor?.TryGetField(propertyName, out var field) == true
            && field is not null
            && string.Equals(field.Editor, "Password", StringComparison.Ordinal))
        {
            if (rawValue is not string secretValue || string.IsNullOrWhiteSpace(secretValue))
            {
                ErrorMessage = "密钥不能为空。";
                return false;
            }

            if (SelectedNode is null || descriptor is null)
            {
                ErrorMessage = "当前没有选中的节点。";
                return false;
            }

            var oldConfig = SelectedNode.Config;
            var existingReference = field.GetValue(oldConfig) as string;
            var secretReference = string.IsNullOrWhiteSpace(existingReference)
                ? $"secret:{SelectedNode.Id:N}:{propertyName}"
                : existingReference;
            try
            {
                await secretStore.SetSecretAsync(secretReference, secretValue, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                ErrorMessage = "密钥保存已取消。";
                return false;
            }
            catch (Exception)
            {
                Trace.TraceError("属性面板密钥保存失败。");
                ErrorMessage = "密钥保存失败。";
                return false;
            }

            return EditPropertyCore(propertyName, secretReference);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return EditPropertyCore(propertyName, rawValue);
    }

    /// <summary>
    /// 使用文件选择器编辑 FilePicker 字段。
    /// </summary>
    /// <param name="propertyName">文件路径属性名。</param>
    /// <param name="cancellationToken">用于取消选择的令牌。</param>
    /// <returns>选择并提交路径时返回 <see langword="true"/>。</returns>
    public async Task<bool> PickFileAsync(
        string propertyName,
        CancellationToken cancellationToken = default)
    {
        if (SelectedNode is null || descriptor is null
            || !descriptor.TryGetField(propertyName, out var field)
            || field is null
            || !string.Equals(field.Editor, "FilePicker", StringComparison.Ordinal))
        {
            ErrorMessage = $"属性 {propertyName} 不是文件选择字段。";
            return false;
        }

        var currentPath = field.GetValue(SelectedNode.Config) as string;
        var selectedPath = await filePickerService.PickFileAsync(currentPath, cancellationToken)
            .ConfigureAwait(false);
        return selectedPath is not null
            && await EditPropertyAsync(propertyName, selectedPath, cancellationToken).ConfigureAwait(false);
    }

    private bool EditPropertyCore(string propertyName, object? rawValue)
    {
        ErrorMessage = null;
        if (SelectedNode is null || descriptor is null)
        {
            ErrorMessage = "当前没有选中的节点。";
            return false;
        }

        try
        {
            var oldConfig = SelectedNode.Config;
            var newConfig = descriptor.CreateUpdatedConfig(oldConfig, propertyName, rawValue);
            commandHistory.Execute(new ChangePropertyCommand<INodeConfig>(
                config => SelectedNode.Config = config,
                oldConfig,
                newConfig));
            RefreshFieldValues();
            return true;
        }
        catch (ConfigValidationException exception)
        {
            ErrorMessage = exception.Message;
            var field = Fields.FirstOrDefault(item =>
                string.Equals(item.PropertyName, propertyName, StringComparison.Ordinal));
            if (field is not null)
            {
                field.ErrorMessage = exception.Message;
            }

            return false;
        }
    }

    /// <summary>
    /// 释放画布和命令历史事件订阅。
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        if (canvas is not null)
        {
            canvas.PropertyChanged -= OnCanvasPropertyChanged;
        }

        commandHistory.Changed -= OnCommandHistoryChanged;
        GC.SuppressFinalize(this);
    }

    private void OnCanvasPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (string.Equals(args.PropertyName, nameof(CanvasViewModel.SelectedNode), StringComparison.Ordinal))
        {
            SetSelectedNode(canvas?.SelectedNode);
        }
    }

    private void OnCommandHistoryChanged(object? sender, EventArgs args)
    {
        RefreshFieldValues();
    }

    private void RefreshFieldValues()
    {
        if (SelectedNode is null || descriptor is null)
        {
            return;
        }

        foreach (var field in Fields)
        {
            if (descriptor.TryGetField(field.PropertyName, out var descriptorField)
                && descriptorField is not null)
            {
                field.Value = field.IsPassword
                    ? null
                    : descriptorField.GetValue(SelectedNode.Config);
                field.ErrorMessage = null;
            }
        }
    }
}

/// <summary>
/// 属性面板单个配置字段的可绑定 ViewModel。
/// </summary>
public sealed class ConfigFieldViewModel : ReactiveObject
{
    internal ConfigFieldViewModel(ConfigFieldDescriptor descriptor, object? value)
    {
        Descriptor = descriptor;
        Value = IsPassword ? null : value;
    }

    /// <summary>获取底层字段描述。</summary>
    public ConfigFieldDescriptor Descriptor { get; }

    /// <summary>获取配置属性名。</summary>
    public string PropertyName => Descriptor.PropertyName;

    /// <summary>获取显示标签。</summary>
    public string Label => Descriptor.Label;

    /// <summary>获取编辑器类型。</summary>
    public string Editor => Descriptor.Editor;

    /// <summary>获取值类型。</summary>
    public Type PropertyType => Descriptor.PropertyType;

    /// <summary>获取下拉选项。</summary>
    public IReadOnlyList<string> Options => Descriptor.Options;

    /// <summary>获取当前字段是否为密码编辑器。</summary>
    public bool IsPassword => string.Equals(Editor, "Password", StringComparison.Ordinal);

    /// <summary>获取当前字段是否为多行文本编辑器。</summary>
    public bool IsMultilineText => string.Equals(Editor, "MultilineText", StringComparison.Ordinal);

    /// <summary>获取或设置当前字段值。</summary>
    [Reactive]
    public object? Value { get; set; }

    /// <summary>获取或设置当前字段错误。</summary>
    [Reactive]
    public string? ErrorMessage { get; set; }

    private Func<object?, CancellationToken, Task<bool>>? commitHandler;
    private Func<CancellationToken, Task<bool>>? pickFileHandler;

    internal void Configure(
        Func<object?, CancellationToken, Task<bool>> commitHandler,
        Func<CancellationToken, Task<bool>> pickFileHandler)
    {
        this.commitHandler = commitHandler;
        this.pickFileHandler = pickFileHandler;
    }

    /// <summary>
    /// 提交字段编辑器值。
    /// </summary>
    /// <param name="value">编辑器值。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>提交是否成功。</returns>
    public Task<bool> CommitAsync(object? value, CancellationToken cancellationToken = default)
    {
        return commitHandler is null
            ? Task.FromResult(false)
            : commitHandler(value, cancellationToken);
    }

    /// <summary>
    /// 调起 FilePicker 编辑器。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>选择并提交是否成功。</returns>
    public Task<bool> PickFileAsync(CancellationToken cancellationToken = default)
    {
        return pickFileHandler is null
            ? Task.FromResult(false)
            : pickFileHandler(cancellationToken);
    }
}

internal sealed class UnavailableSecretStore : ISecretStore
{
    public static UnavailableSecretStore Instance { get; } = new();

    public ValueTask<string?> GetSecretAsync(string secretId, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<string?>(null);
    }

    public ValueTask SetSecretAsync(string secretId, string secretValue, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("属性面板未配置密钥存储。");
    }

    public ValueTask<bool> DeleteSecretAsync(string secretId, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(false);
    }
}

internal sealed class UnavailableFilePickerService : IFilePickerService
{
    public static UnavailableFilePickerService Instance { get; } = new();

    public Task<string?> PickFileAsync(string? currentPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }
}
