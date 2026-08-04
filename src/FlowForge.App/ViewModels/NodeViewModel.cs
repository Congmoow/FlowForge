using System.Collections.ObjectModel;
using Avalonia;
using FlowForge.Core.Abstractions;
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
        : this(new CompatibilityNode(id, typeId), null, title, new Point(x, y))
    {
    }

    /// <summary>
    /// 使用真实节点模型初始化节点 ViewModel。
    /// </summary>
    /// <param name="node">被包装的 Core 节点。</param>
    /// <param name="definition">节点目录定义。</param>
    /// <param name="x">节点左上角 X 坐标。</param>
    /// <param name="y">节点左上角 Y 坐标。</param>
    public NodeViewModel(INode node, NodeDefinition definition, double x, double y)
        : this(node, definition, definition.Title, new Point(x, y))
    {
    }

    /// <summary>
    /// 使用真实节点模型和画布位置初始化节点 ViewModel。
    /// </summary>
    /// <param name="node">被包装的 Core 节点。</param>
    /// <param name="definition">节点目录定义。</param>
    /// <param name="position">节点左上角画布坐标。</param>
    public NodeViewModel(INode node, NodeDefinition definition, Point position)
        : this(node, definition, definition.Title, position)
    {
    }

    private NodeViewModel(INode node, NodeDefinition? definition, string title, Point position)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Id = node.Id;
        TypeId = node.TypeId;
        Title = title;
        Node = node;
        Definition = definition;
        Position = position;

        foreach (var port in node.Inputs)
        {
            Inputs.Add(new PortViewModel(this, port.Id, port.Name, PortDirection.Input, port.DataType, Inputs.Count));
        }

        foreach (var port in node.Outputs)
        {
            Outputs.Add(new PortViewModel(this, port.Id, port.Name, PortDirection.Output, port.DataType, Outputs.Count));
        }
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
    /// 获取被 ViewModel 包装的真实 Core 节点。
    /// </summary>
    public INode Node { get; }

    /// <summary>
    /// 获取节点目录定义；兼容旧的手工模板节点时可能为空。
    /// </summary>
    public NodeDefinition? Definition { get; }

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
    /// 获取或设置真实节点的 immutable 配置。
    /// </summary>
    public INodeConfig Config
    {
        get => Node.Config;
        set
        {
            Node.Config = value;
            this.RaisePropertyChanged();
        }
    }

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

    private sealed class CompatibilityNode(Guid id, string typeId) : INode
    {
        public Guid Id { get; } = id;

        public string TypeId { get; } = typeId;

        public IReadOnlyList<IPort> Inputs { get; } = [];

        public IReadOnlyList<IPort> Outputs { get; } = [];

        public INodeConfig Config { get; set; } = new CompatibilityConfig();

        public ValueTask ExecuteAsync(IExecutionContext ctx, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed record CompatibilityConfig : INodeConfig;
}
