using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

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
        AddNodeFromTemplateCommand = ReactiveCommand.Create<AddNodeFromTemplateRequest>(AddNodeFromTemplate);
        BeginEdgeDragCommand = ReactiveCommand.Create<BeginEdgeDragRequest>(BeginEdgeDrag);
        UpdateEdgeDragCommand = ReactiveCommand.Create<Point>(UpdateEdgeDrag);
        PreviewEdgeTargetCommand = ReactiveCommand.Create<PortViewModel?>(PreviewEdgeTarget);
        CompleteEdgeDragCommand = ReactiveCommand.Create<PortViewModel>(CompleteEdgeDrag);
        CancelEdgeDragCommand = ReactiveCommand.Create(CancelEdgeDrag);
    }

    /// <summary>
    /// 当前画布上的节点集合。
    /// </summary>
    public ObservableCollection<NodeViewModel> Nodes { get; } = [];

    /// <summary>
    /// 当前画布上的连线集合。
    /// </summary>
    public ObservableCollection<EdgeViewModel> Edges { get; } = [];

    /// <summary>
    /// 当前拖拽中的草稿连线。
    /// </summary>
    [Reactive]
    public EdgeViewModel? DraftEdge { get; private set; }

    /// <summary>
    /// 当前预览的目标端口。
    /// </summary>
    [Reactive]
    public PortViewModel? PreviewTargetPort { get; private set; }

    /// <summary>
    /// 当前连线目标兼容状态。
    /// </summary>
    [Reactive]
    public ConnectionPreviewState ConnectionPreviewState { get; private set; }

    /// <summary>
    /// 按位移移动节点的命令。
    /// </summary>
    public ICommand MoveNodeCommand { get; }

    /// <summary>
    /// 从节点模板创建节点的命令。
    /// </summary>
    public ICommand AddNodeFromTemplateCommand { get; }

    /// <summary>
    /// 开始从输出端口拖拽连线的命令。
    /// </summary>
    public ICommand BeginEdgeDragCommand { get; }

    /// <summary>
    /// 更新草稿连线终点的命令。
    /// </summary>
    public ICommand UpdateEdgeDragCommand { get; }

    /// <summary>
    /// 预览当前悬停端口兼容性的命令。
    /// </summary>
    public ICommand PreviewEdgeTargetCommand { get; }

    /// <summary>
    /// 完成端口连线拖拽的命令。
    /// </summary>
    public ICommand CompleteEdgeDragCommand { get; }

    /// <summary>
    /// 取消端口连线拖拽的命令。
    /// </summary>
    public ICommand CancelEdgeDragCommand { get; }

    private void MoveNode(MoveNodeRequest request)
    {
        var node = Nodes.FirstOrDefault(candidate => candidate.Id == request.NodeId);
        if (node is null)
        {
            return;
        }

        node.Position += request.Delta;
    }

    private void AddNodeFromTemplate(AddNodeFromTemplateRequest request)
    {
        var node = new NodeViewModel(Guid.NewGuid(), request.Template.TypeId, request.Template.Title, request.Position.X, request.Position.Y);

        for (var index = 0; index < request.Template.Inputs.Count; index++)
        {
            var input = request.Template.Inputs[index];
            node.Inputs.Add(new PortViewModel(node, input.Id, input.DisplayName, input.Direction, input.DataType, index));
        }

        for (var index = 0; index < request.Template.Outputs.Count; index++)
        {
            var output = request.Template.Outputs[index];
            node.Outputs.Add(new PortViewModel(node, output.Id, output.DisplayName, output.Direction, output.DataType, index));
        }

        Nodes.Add(node);
    }

    private void BeginEdgeDrag(BeginEdgeDragRequest request)
    {
        if (request.Source.Direction != PortDirection.Output)
        {
            return;
        }

        DraftEdge = EdgeViewModel.CreateDraft(request.Source, request.CurrentPoint);
    }

    private void UpdateEdgeDrag(Point currentPoint)
    {
        if (DraftEdge is null)
        {
            return;
        }

        DraftEdge.DraftEndPoint = currentPoint;
    }

    private void CompleteEdgeDrag(PortViewModel target)
    {
        if (DraftEdge is null || !DraftEdge.Source.CanConnectTo(target))
        {
            ClearDraftEdge();
            return;
        }

        Edges.Add(new EdgeViewModel(Guid.NewGuid(), DraftEdge.Source, target));
        ClearDraftEdge();
    }

    private void CancelEdgeDrag()
    {
        ClearDraftEdge();
    }

    private void PreviewEdgeTarget(PortViewModel? target)
    {
        PreviewTargetPort = target;

        if (DraftEdge is null || target is null || target.Direction != PortDirection.Input)
        {
            ConnectionPreviewState = ConnectionPreviewState.None;
            return;
        }

        ConnectionPreviewState = DraftEdge.Source.CanConnectTo(target)
            ? ConnectionPreviewState.Compatible
            : ConnectionPreviewState.Incompatible;
    }

    private void ClearDraftEdge()
    {
        DraftEdge = null;
        PreviewTargetPort = null;
        ConnectionPreviewState = ConnectionPreviewState.None;
    }
}

/// <summary>
/// 移动节点命令的参数。
/// </summary>
/// <param name="NodeId">要移动的节点标识。</param>
/// <param name="Delta">本次移动位移。</param>
public sealed record MoveNodeRequest(Guid NodeId, Vector Delta);

/// <summary>
/// 从模板创建节点命令的参数。
/// </summary>
/// <param name="Template">节点模板。</param>
/// <param name="Position">投放位置。</param>
public sealed record AddNodeFromTemplateRequest(NodeTemplateViewModel Template, Point Position);

/// <summary>
/// 开始拖拽连线命令的参数。
/// </summary>
/// <param name="Source">输出端源端口。</param>
/// <param name="CurrentPoint">当前指针位置。</param>
public sealed record BeginEdgeDragRequest(PortViewModel Source, Point CurrentPoint);
