using System.Collections.ObjectModel;
using FlowForge.App.Commands;
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
    public PropertyPanelViewModel(CanvasViewModel? canvas, CommandHistory? commandHistory = null)
    {
        this.canvas = canvas;
        this.commandHistory = commandHistory ?? canvas?.CommandHistory ?? new CommandHistory();
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
            Fields.Add(new ConfigFieldViewModel(field, field.GetValue(node.Config)));
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
                field.Value = descriptorField.GetValue(SelectedNode.Config);
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
        Value = value;
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

    /// <summary>获取或设置当前字段值。</summary>
    [Reactive]
    public object? Value { get; set; }

    /// <summary>获取或设置当前字段错误。</summary>
    [Reactive]
    public string? ErrorMessage { get; set; }
}
