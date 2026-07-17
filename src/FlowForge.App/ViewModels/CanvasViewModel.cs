using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using CommandHistory = FlowForge.App.Commands.CommandHistory;
using CompositeCommand = FlowForge.App.Commands.CompositeCommand;
using MoveNodeEditCommand = FlowForge.App.Commands.MoveNodeEditCommand;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace FlowForge.App.ViewModels;

/// <summary>
/// 画布 ViewModel，管理节点集合与画布级命令。
/// </summary>
public sealed class CanvasViewModel : ReactiveObject
{
    private readonly CommandHistory _commandHistory;
    /// <summary>
    /// 初始化画布 ViewModel。
    /// </summary>
    public CanvasViewModel(CommandHistory? commandHistory = null)
    {
        _commandHistory = commandHistory ?? new CommandHistory();
        MoveNodeCommand = ReactiveCommand.Create<MoveNodeRequest>(MoveNode);
        MoveSelectedNodesCommand = ReactiveCommand.Create<Vector>(MoveSelectedNodes);
        SelectNodeCommand = ReactiveCommand.Create<SelectNodeRequest>(SelectNode);
        ClearSelectionCommand = ReactiveCommand.Create(ClearSelection);
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
    /// 移动所有已选节点的命令。
    /// </summary>
    public ICommand MoveSelectedNodesCommand { get; }

    /// <summary>
    /// 选择节点的命令。
    /// </summary>
    public ICommand SelectNodeCommand { get; }

    /// <summary>
    /// 清空节点选择的命令。
    /// </summary>
    public ICommand ClearSelectionCommand { get; }

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

    /// <summary>获取画布编辑命令历史。</summary>
    public CommandHistory CommandHistory => _commandHistory;

    /// <summary>
    /// 将已完成的一次节点拖动写入命令历史。
    /// </summary>
    /// <param name="originalPositions">拖动开始时按节点记录的位置。</param>
    public void CommitNodeMove(IReadOnlyDictionary<NodeViewModel, Point> originalPositions)
    {
        ArgumentNullException.ThrowIfNull(originalPositions);
        var commands = originalPositions
            .Where(pair => pair.Key.Position != pair.Value)
            .Select(pair => (FlowForge.App.Commands.ICommand)new MoveNodeEditCommand(pair.Key, pair.Value, pair.Key.Position))
            .ToArray();
        if (commands.Length == 1)
        {
            _commandHistory.Execute(commands[0]);
        }
        else if (commands.Length > 1)
        {
            _commandHistory.Execute(new CompositeCommand(commands));
        }
    }

    private void MoveNode(MoveNodeRequest request)
    {
        var node = Nodes.FirstOrDefault(candidate => candidate.Id == request.NodeId);
        if (node is null)
        {
            return;
        }

        node.Position += request.Delta;
    }

    private void MoveSelectedNodes(Vector delta)
    {
        foreach (var node in Nodes.Where(candidate => candidate.IsSelected))
        {
            node.Position += delta;
        }
    }

    private void SelectNode(SelectNodeRequest request)
    {
        var node = Nodes.FirstOrDefault(candidate => candidate.Id == request.NodeId);
        if (node is null)
        {
            return;
        }

        switch (request.Gesture)
        {
            case SelectionGesture.Replace:
                ClearSelection();
                node.IsSelected = true;
                break;
            case SelectionGesture.Add:
                node.IsSelected = true;
                break;
            case SelectionGesture.Toggle:
                node.IsSelected = !node.IsSelected;
                break;
        }
    }

    private void ClearSelection()
    {
        foreach (var node in Nodes)
        {
            node.IsSelected = false;
        }
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
        if (DraftEdge is null || !DraftEdge.Source.CanConnectTo(target) || HasIncomingEdge(target))
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

    private bool HasIncomingEdge(PortViewModel target)
    {
        return Edges.Any(edge => edge.Target == target);
    }
}

/// <summary>
/// 移动节点命令的参数。
/// </summary>
/// <param name="NodeId">要移动的节点标识。</param>
/// <param name="Delta">本次移动位移。</param>
public sealed record MoveNodeRequest(Guid NodeId, Vector Delta);

/// <summary>
/// 选择节点命令的参数。
/// </summary>
/// <param name="NodeId">要选择的节点标识。</param>
/// <param name="Gesture">选择手势。</param>
public sealed record SelectNodeRequest(Guid NodeId, SelectionGesture Gesture);

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
