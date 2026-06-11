namespace FlowForge.App.ViewModels;

/// <summary>
/// 节点模板中的端口定义。
/// </summary>
/// <param name="Id">端口稳定标识。</param>
/// <param name="DisplayName">端口显示名称。</param>
/// <param name="Direction">端口方向。</param>
/// <param name="DataType">端口数据类型。</param>
public sealed record PortTemplateViewModel(string Id, string DisplayName, PortDirection Direction, Type DataType);
