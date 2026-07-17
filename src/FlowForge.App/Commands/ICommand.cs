namespace FlowForge.App.Commands;

/// <summary>
/// 表示可执行并可撤销的编辑命令。
/// </summary>
public interface ICommand
{
    /// <summary>
    /// 执行命令。
    /// </summary>
    void Execute();

    /// <summary>
    /// 撤销命令产生的变更。
    /// </summary>
    void Undo();
}
