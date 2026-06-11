namespace FlowForge.App.ViewModels;

/// <summary>
/// Toolbox 中可拖放到画布的节点模板。
/// </summary>
/// <param name="TypeId">节点类型标识。</param>
/// <param name="Title">节点显示标题。</param>
/// <param name="Inputs">输入端口模板。</param>
/// <param name="Outputs">输出端口模板。</param>
public sealed record NodeTemplateViewModel(
    string TypeId,
    string Title,
    IReadOnlyList<PortTemplateViewModel> Inputs,
    IReadOnlyList<PortTemplateViewModel> Outputs);
