using System.Collections.ObjectModel;
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
    {
        Templates =
        [
            new NodeTemplateViewModel(
                "core.datasource.csv",
                "CSV 读取",
                [],
                [new PortTemplateViewModel("rows", "rows", PortDirection.Output, typeof(object))]),
            new NodeTemplateViewModel(
                "core.datasource.text",
                "文本读取",
                [],
                [new PortTemplateViewModel("content", "content", PortDirection.Output, typeof(string))]),
            new NodeTemplateViewModel(
                "core.sink.console",
                "控制台输出",
                [new PortTemplateViewModel("value", "value", PortDirection.Input, typeof(object))],
                []),
        ];

        SelectedTemplate = Templates[0];
    }

    /// <summary>
    /// 可创建的节点模板。
    /// </summary>
    public ObservableCollection<NodeTemplateViewModel> Templates { get; }

    /// <summary>
    /// 当前选中的节点模板。
    /// </summary>
    [Reactive]
    public NodeTemplateViewModel SelectedTemplate { get; set; }
}
