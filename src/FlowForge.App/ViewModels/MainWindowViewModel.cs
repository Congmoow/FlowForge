using ReactiveUI;

namespace FlowForge.App.ViewModels;

/// <summary>
/// 主窗口的根 ViewModel。
/// </summary>
public sealed class MainWindowViewModel : ReactiveObject
{
    /// <summary>
    /// 初始化主窗口 ViewModel。
    /// </summary>
    public MainWindowViewModel()
    {
        Canvas = new CanvasViewModel();
        Toolbox = new ToolboxViewModel();
        var csvNode = new NodeViewModel(Guid.Parse("11111111-1111-1111-1111-111111111111"), "core.datasource.csv", "CSV 读取", 96, 80);
        csvNode.Outputs.Add(new PortViewModel(csvNode, "rows", "rows", PortDirection.Output, typeof(object), 0));
        Canvas.Nodes.Add(csvNode);

        var consoleNode = new NodeViewModel(Guid.Parse("22222222-2222-2222-2222-222222222222"), "core.sink.console", "控制台输出", 420, 120);
        consoleNode.Inputs.Add(new PortViewModel(consoleNode, "value", "value", PortDirection.Input, typeof(object), 0));
        Canvas.Nodes.Add(consoleNode);
    }

    /// <summary>
    /// 应用显示名称。
    /// </summary>
    public string ApplicationName { get; } = "FlowForge";

    /// <summary>
    /// 左侧节点库标题。
    /// </summary>
    public string ToolboxTitle { get; } = "节点库";

    /// <summary>
    /// 中央画布标题。
    /// </summary>
    public string CanvasTitle { get; } = "工作流画布";

    /// <summary>
    /// 右侧属性面板标题。
    /// </summary>
    public string PropertyPanelTitle { get; } = "属性面板";

    /// <summary>
    /// 中央工作流画布。
    /// </summary>
    public CanvasViewModel Canvas { get; }

    /// <summary>
    /// 左侧节点库。
    /// </summary>
    public ToolboxViewModel Toolbox { get; }
}
