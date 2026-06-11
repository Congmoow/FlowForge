using Avalonia;
using ReactiveUI;

namespace FlowForge.App.ViewModels;

/// <summary>
/// 节点端口 ViewModel，描述连接点元数据和画布锚点。
/// </summary>
public sealed class PortViewModel : ReactiveObject
{
    /// <summary>
    /// 节点标准宽度。
    /// </summary>
    public const double NodeWidth = 220;

    /// <summary>
    /// 节点标准高度。
    /// </summary>
    public const double NodeHeight = 96;

    private const double FirstPortOffsetY = 32;
    private const double PortSpacing = 24;

    /// <summary>
    /// 初始化端口 ViewModel。
    /// </summary>
    /// <param name="node">端口所属节点。</param>
    /// <param name="id">端口稳定标识。</param>
    /// <param name="displayName">端口显示名称。</param>
    /// <param name="direction">端口方向。</param>
    /// <param name="dataType">端口声明的数据类型。</param>
    /// <param name="index">端口在同侧边缘上的排序索引。</param>
    public PortViewModel(NodeViewModel node, string id, string displayName, PortDirection direction, Type dataType, int index)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(dataType);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        Node = node;
        Id = id;
        DisplayName = displayName;
        Direction = direction;
        DataType = dataType;
        Index = index;
    }

    /// <summary>
    /// 端口所属节点。
    /// </summary>
    public NodeViewModel Node { get; }

    /// <summary>
    /// 端口稳定标识。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 端口显示名称。
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// 端口方向。
    /// </summary>
    public PortDirection Direction { get; }

    /// <summary>
    /// 端口声明的数据类型。
    /// </summary>
    public Type DataType { get; }

    /// <summary>
    /// 端口在同侧边缘上的排序索引。
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// 端口在画布坐标系中的锚点。
    /// </summary>
    public Point AnchorPoint
    {
        get
        {
            var x = Direction == PortDirection.Input ? Node.Position.X : Node.Position.X + NodeWidth;
            var y = Node.Position.Y + FirstPortOffsetY + Index * PortSpacing;
            return new Point(x, y);
        }
    }

    /// <summary>
    /// 判断当前端口能否连接到目标端口。
    /// </summary>
    /// <param name="target">目标端口。</param>
    /// <returns>如果方向和类型都兼容则返回 true。</returns>
    public bool CanConnectTo(PortViewModel target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return Direction == PortDirection.Output
            && target.Direction == PortDirection.Input
            && target.DataType.IsAssignableFrom(DataType);
    }
}
