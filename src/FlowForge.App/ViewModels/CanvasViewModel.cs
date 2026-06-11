using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using ReactiveUI;

namespace FlowForge.App.ViewModels;

/// <summary>
/// 画布 ViewModel，管理节点集合与画布级命令。
/// </summary>
public sealed class CanvasViewModel : ReactiveObject
{
    /// <summary>
    /// 初始化画布 ViewModel。
    /// </summary>
    public CanvasViewModel()
    {
        MoveNodeCommand = ReactiveCommand.Create<MoveNodeRequest>(MoveNode);
    }

    /// <summary>
    /// 当前画布上的节点集合。
    /// </summary>
    public ObservableCollection<NodeViewModel> Nodes { get; } = [];

    /// <summary>
    /// 按位移移动节点的命令。
    /// </summary>
    public ICommand MoveNodeCommand { get; }

    private void MoveNode(MoveNodeRequest request)
    {
        var node = Nodes.FirstOrDefault(candidate => candidate.Id == request.NodeId);
        if (node is null)
        {
            return;
        }

        node.Position += request.Delta;
    }
}

/// <summary>
/// 移动节点命令的参数。
/// </summary>
/// <param name="NodeId">要移动的节点标识。</param>
/// <param name="Delta">本次移动位移。</param>
public sealed record MoveNodeRequest(Guid NodeId, Vector Delta);
