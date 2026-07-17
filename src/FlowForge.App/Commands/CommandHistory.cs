namespace FlowForge.App.Commands;

/// <summary>
/// 管理可撤销编辑命令的撤销栈和重做栈。
/// </summary>
public sealed class CommandHistory
{
    private readonly Stack<ICommand> _undoStack = new();
    private readonly Stack<ICommand> _redoStack = new();

    /// <summary>
    /// 获取当前是否可以撤销。
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// 获取当前是否可以重做。
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// 执行新命令并将其加入撤销历史。
    /// </summary>
    /// <param name="command">要执行的命令。</param>
    public void Execute(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
    }

    /// <summary>
    /// 撤销最近成功执行的命令。
    /// </summary>
    /// <returns>存在并成功撤销命令时返回 <see langword="true"/>。</returns>
    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        var command = _undoStack.Peek();
        command.Undo();
        _undoStack.Pop();
        _redoStack.Push(command);
        return true;
    }

    /// <summary>
    /// 重新执行最近撤销的命令。
    /// </summary>
    /// <returns>存在并成功重做命令时返回 <see langword="true"/>。</returns>
    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        var command = _redoStack.Peek();
        command.Execute();
        _redoStack.Pop();
        _undoStack.Push(command);
        return true;
    }

    /// <summary>
    /// 清空撤销和重做历史。
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
