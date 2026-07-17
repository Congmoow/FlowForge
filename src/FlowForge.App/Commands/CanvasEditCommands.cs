using Avalonia;
using FlowForge.App.ViewModels;

namespace FlowForge.App.Commands;

/// <summary>向画布添加节点的可撤销命令。</summary>
public sealed class AddNodeCommand(CanvasViewModel canvas, NodeViewModel node) : ICommand
{
    private int insertionIndex = -1;

    /// <inheritdoc />
    public void Execute()
    {
        insertionIndex = insertionIndex < 0 ? canvas.Nodes.Count : Math.Min(insertionIndex, canvas.Nodes.Count);
        canvas.Nodes.Insert(insertionIndex, node);
    }

    /// <inheritdoc />
    public void Undo()
    {
        insertionIndex = canvas.Nodes.IndexOf(node);
        canvas.Nodes.Remove(node);
    }
}

/// <summary>从画布删除节点及其关联连线的可撤销命令。</summary>
public sealed class RemoveNodeCommand(CanvasViewModel canvas, NodeViewModel node) : ICommand
{
    private readonly List<(EdgeViewModel Edge, int Index)> removedEdges = [];
    private int nodeIndex;

    /// <inheritdoc />
    public void Execute()
    {
        nodeIndex = canvas.Nodes.IndexOf(node);
        if (nodeIndex < 0)
        {
            throw new InvalidOperationException("要删除的节点不在画布中。");
        }

        removedEdges.Clear();
        for (var index = 0; index < canvas.Edges.Count; index++)
        {
            var edge = canvas.Edges[index];
            if (ReferenceEquals(edge.Source.Node, node) || ReferenceEquals(edge.Target?.Node, node))
            {
                removedEdges.Add((edge, index));
            }
        }

        for (var index = removedEdges.Count - 1; index >= 0; index--)
        {
            canvas.Edges.RemoveAt(removedEdges[index].Index);
        }

        canvas.Nodes.RemoveAt(nodeIndex);
    }

    /// <inheritdoc />
    public void Undo()
    {
        canvas.Nodes.Insert(nodeIndex, node);
        foreach (var removed in removedEdges)
        {
            canvas.Edges.Insert(Math.Min(removed.Index, canvas.Edges.Count), removed.Edge);
        }
    }
}

/// <summary>向画布添加连线的可撤销命令。</summary>
public sealed class AddEdgeCommand(CanvasViewModel canvas, EdgeViewModel edge) : ICommand
{
    private int insertionIndex = -1;

    /// <inheritdoc />
    public void Execute()
    {
        insertionIndex = insertionIndex < 0 ? canvas.Edges.Count : Math.Min(insertionIndex, canvas.Edges.Count);
        canvas.Edges.Insert(insertionIndex, edge);
    }

    /// <inheritdoc />
    public void Undo()
    {
        insertionIndex = canvas.Edges.IndexOf(edge);
        canvas.Edges.Remove(edge);
    }
}

/// <summary>从画布删除连线的可撤销命令。</summary>
public sealed class RemoveEdgeCommand(CanvasViewModel canvas, EdgeViewModel edge) : ICommand
{
    private int edgeIndex;

    /// <inheritdoc />
    public void Execute()
    {
        edgeIndex = canvas.Edges.IndexOf(edge);
        if (edgeIndex < 0)
        {
            throw new InvalidOperationException("要删除的连线不在画布中。");
        }

        canvas.Edges.RemoveAt(edgeIndex);
    }

    /// <inheritdoc />
    public void Undo()
    {
        canvas.Edges.Insert(edgeIndex, edge);
    }
}

/// <summary>移动节点的可撤销命令。</summary>
public sealed class MoveNodeEditCommand(NodeViewModel node, Point oldPosition, Point newPosition) : ICommand
{
    /// <inheritdoc />
    public void Execute()
    {
        node.Position = newPosition;
    }

    /// <inheritdoc />
    public void Undo()
    {
        node.Position = oldPosition;
    }
}

/// <summary>修改属性值的可撤销命令。</summary>
/// <typeparam name="T">属性值类型。</typeparam>
public sealed class ChangePropertyCommand<T>(Action<T> setter, T oldValue, T newValue) : ICommand
{
    /// <inheritdoc />
    public void Execute()
    {
        setter(newValue);
    }

    /// <inheritdoc />
    public void Undo()
    {
        setter(oldValue);
    }
}

/// <summary>将多个编辑命令组合为一个历史项。</summary>
public sealed class CompositeCommand : ICommand
{
    private readonly IReadOnlyList<ICommand> commands;

    /// <summary>初始化批量命令。</summary>
    /// <param name="commands">按执行顺序排列的子命令。</param>
    public CompositeCommand(IEnumerable<ICommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        this.commands = commands.ToArray();
    }

    /// <inheritdoc />
    public void Execute()
    {
        var executedCount = 0;
        try
        {
            for (; executedCount < commands.Count; executedCount++)
            {
                commands[executedCount].Execute();
            }
        }
        catch
        {
            for (var index = executedCount - 1; index >= 0; index--)
            {
                commands[index].Undo();
            }

            throw;
        }
    }

    /// <inheritdoc />
    public void Undo()
    {
        for (var index = commands.Count - 1; index >= 0; index--)
        {
            commands[index].Undo();
        }
    }
}
