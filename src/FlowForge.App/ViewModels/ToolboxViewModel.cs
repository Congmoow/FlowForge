using System.Collections.ObjectModel;
using FlowForge.Core.Abstractions;
using FlowForge.Core.Serialization;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace FlowForge.App.ViewModels;

/// <summary>
/// 左侧节点库 ViewModel。
/// </summary>
public sealed class ToolboxViewModel : ReactiveObject
{
    /// <summary>
    /// 初始化节点库 ViewModel。
    /// </summary>
    public ToolboxViewModel()
        : this(NodeRegistry.CreateDefault())
    {
    }

    /// <summary>
    /// 使用节点目录初始化节点库 ViewModel。
    /// </summary>
    /// <param name="registry">提供节点定义的目录。</param>
    public ToolboxViewModel(NodeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        Registry = registry;
        Templates = [];
        Refresh();
    }

    private NodeRegistry Registry { get; }

    /// <summary>
    /// 可创建的节点模板。
    /// </summary>
    public ObservableCollection<NodeTemplateViewModel> Templates { get; }

    /// <summary>
    /// 当前选中的节点模板。
    /// </summary>
    [Reactive]
    public NodeTemplateViewModel SelectedTemplate { get; set; } = null!;

    /// <summary>
    /// 从共享节点目录刷新模板，保留仍然存在的当前选择。
    /// </summary>
    public void Refresh()
    {
        var selectedTypeId = SelectedTemplate?.TypeId;
        var templates = Registry.Definitions.Select(ToTemplate).ToArray();
        if (templates.Length == 0)
        {
            throw new InvalidOperationException("节点目录不能为空。");
        }

        Templates.Clear();
        foreach (var template in templates)
        {
            Templates.Add(template);
        }

        SelectedTemplate = Templates.FirstOrDefault(template => template.TypeId == selectedTypeId)
            ?? Templates[0];
    }

    private static NodeTemplateViewModel ToTemplate(NodeDefinition definition)
    {
        return new NodeTemplateViewModel(
            definition.TypeId,
            definition.Title,
            definition.Inputs.Select(port => new PortTemplateViewModel(
                port.Id,
                port.Name,
                PortDirection.Input,
                port.DataType)).ToArray(),
            definition.Outputs.Select(port => new PortTemplateViewModel(
                port.Id,
                port.Name,
                PortDirection.Output,
                port.DataType)).ToArray());
    }
}
