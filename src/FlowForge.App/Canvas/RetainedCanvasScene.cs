using Avalonia;
using Avalonia.Media;
using FlowForge.App.ViewModels;

namespace FlowForge.App.Canvas;

/// <summary>
/// 按节点和连线维护 retained 绘制操作，并计算局部失效区域。
/// </summary>
public sealed class RetainedCanvasScene : IDisposable
{
    private readonly Dictionary<Guid, NodeDrawOperation> nodeOperations = [];
    private readonly Dictionary<Guid, EdgeDrawOperation> edgeOperations = [];
    private bool disposed;

    /// <summary>当前节点绘制操作。</summary>
    public IReadOnlyCollection<NodeDrawOperation> NodeOperations => nodeOperations.Values;

    /// <summary>当前连线绘制操作。</summary>
    public IReadOnlyCollection<EdgeDrawOperation> EdgeOperations => edgeOperations.Values;

    /// <summary>最近一次替换产生的 dirty rect。</summary>
    public Rect? LastDirtyRect { get; private set; }

    /// <summary>替换指定节点操作并返回旧/新 bounds union。</summary>
    /// <param name="operation">新的节点操作。</param>
    /// <returns>需要失效的 world 矩形。</returns>
    public Rect ReplaceNode(NodeDrawOperation operation)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(operation);

        var dirty = Replace(nodeOperations, operation.Snapshot.Id, operation, static value => value.Bounds);
        LastDirtyRect = dirty;
        return dirty;
    }

    /// <summary>替换指定连线操作并返回旧/新 bounds union。</summary>
    /// <param name="operation">新的连线操作。</param>
    /// <returns>需要失效的 world 矩形。</returns>
    public Rect ReplaceEdge(EdgeDrawOperation operation)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(operation);

        var dirty = Replace(edgeOperations, operation.Snapshot.Id, operation, static value => value.Bounds);
        LastDirtyRect = dirty;
        return dirty;
    }

    /// <summary>按当前 ViewModel 集合重建并同步 retained scene。</summary>
    /// <param name="nodes">当前节点集合。</param>
    /// <param name="edges">当前连线集合。</param>
    public void Rebuild(IEnumerable<NodeViewModel> nodes, IEnumerable<EdgeViewModel> edges)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        var nodeList = nodes.ToArray();
        var edgeList = edges.ToArray();
        var dirty = (Rect?)null;

        foreach (var node in nodeList)
        {
            var next = CreateNodeOperation(node);
            if (nodeOperations.TryGetValue(node.Id, out var current) && current.Equals(next))
            {
                next.Dispose();
                continue;
            }

            dirty = UnionNullable(dirty, ReplaceNode(next));
        }

        foreach (var removed in nodeOperations.Keys.Except(nodeList.Select(node => node.Id)).ToArray())
        {
            var old = nodeOperations[removed];
            nodeOperations.Remove(removed);
            old.Dispose();
            dirty = UnionNullable(dirty, old.Bounds);
        }

        foreach (var edge in edgeList)
        {
            var next = CreateEdgeOperation(edge);
            if (edgeOperations.TryGetValue(edge.Id, out var current) && current.Equals(next))
            {
                next.Dispose();
                continue;
            }

            dirty = UnionNullable(dirty, ReplaceEdge(next));
        }

        foreach (var removed in edgeOperations.Keys.Except(edgeList.Select(edge => edge.Id)).ToArray())
        {
            var old = edgeOperations[removed];
            edgeOperations.Remove(removed);
            old.Dispose();
            dirty = UnionNullable(dirty, old.Bounds);
        }

        LastDirtyRect = dirty;
    }

    /// <summary>释放全部 retained operation。</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach (var operation in nodeOperations.Values)
        {
            operation.Dispose();
        }

        foreach (var operation in edgeOperations.Values)
        {
            operation.Dispose();
        }

        nodeOperations.Clear();
        edgeOperations.Clear();
    }

    private static NodeDrawOperation CreateNodeOperation(NodeViewModel node)
    {
        return new NodeDrawOperation(new NodeDrawSnapshot(
            node.Id,
            CanvasCulling.NodeBounds(node.Position),
            node.Title,
            node.TypeId,
            ResolveNodeBorderColor(node.ExecutionState),
            node.IsSelected,
            System.Collections.Immutable.ImmutableArray.CreateRange(
                node.Inputs.Concat(node.Outputs).Select(port => port.AnchorPoint))));
    }

    private static EdgeDrawOperation CreateEdgeOperation(EdgeViewModel edge)
    {
        return new EdgeDrawOperation(new EdgeDrawSnapshot(edge.Id, edge.StartPoint, edge.EndPoint, 2));
    }

    private static Color ResolveNodeBorderColor(NodeExecutionVisualState state)
    {
        return state switch
        {
            NodeExecutionVisualState.Running => Color.Parse("#D97706"),
            NodeExecutionVisualState.Success => Color.Parse("#16A34A"),
            NodeExecutionVisualState.Failed => Color.Parse("#DC2626"),
            _ => Color.Parse("#2563EB"),
        };
    }

    private static Rect Replace<T>(
        Dictionary<Guid, T> operations,
        Guid id,
        T next,
        Func<T, Rect> getBounds)
        where T : IDisposable
    {
        if (operations.TryGetValue(id, out var previous))
        {
            operations[id] = next;
            previous.Dispose();
            return Union(getBounds(previous), getBounds(next));
        }

        operations.Add(id, next);
        return getBounds(next);
    }

    private static Rect? UnionNullable(Rect? current, Rect next)
    {
        return current is { } value ? Union(value, next) : next;
    }

    private static Rect Union(Rect first, Rect second)
    {
        var left = Math.Min(first.Left, second.Left);
        var top = Math.Min(first.Top, second.Top);
        var right = Math.Max(first.Right, second.Right);
        var bottom = Math.Max(first.Bottom, second.Bottom);
        return new Rect(left, top, right - left, bottom - top);
    }
}
