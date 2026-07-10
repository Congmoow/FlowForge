using System.Collections.ObjectModel;
using Avalonia;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace FlowForge.App.ViewModels;

/// <summary>
/// 画布节点的 ViewModel，保存节点身份、位置与选中状态。
/// </summary>
public sealed class NodeViewModel : ReactiveObject
{
    /// <summary>
    /// 初始化节点 ViewModel。
    /// </summary>
    /// <param name="id">节点在画布中的稳定标识。</param>
    /// <param name="typeId">节点类型标识。</param>
    /// <param name="title">节点显示标题。</param>
    /// <param name="x">节点左上角 X 坐标。</param>
    /// <param name="y">节点左上角 Y 坐标。</param>
    public NodeViewModel(Guid id, string typeId, string title, double x, double y)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Id = id;
        TypeId = typeId;
        Title = title;
        Position = new Point(x, y);
    }

    /// <summary>
    /// 节点在画布中的稳定标识。
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// 节点类型标识，例如 core.datasource.csv。
    /// </summary>
    public string TypeId { get; }

    /// <summary>
    /// 节点显示标题。
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 节点输入端口集合。
    /// </summary>
    public ObservableCollection<PortViewModel> Inputs { get; } = [];

    /// <summary>
    /// 节点输出端口集合。
    /// </summary>
    public ObservableCollection<PortViewModel> Outputs { get; } = [];

    /// <summary>
    /// 节点左上角坐标。
    /// </summary>
    [Reactive]
    public Point Position { get; set; }

    /// <summary>
    /// 节点是否处于选中状态。
    /// </summary>
    [Reactive]
    public bool IsSelected { get; set; }

    /// <summary>
    /// 节点当前的执行高亮状态。
    /// </summary>
    [Reactive]
    public NodeExecutionVisualState ExecutionState { get; set; }
}
