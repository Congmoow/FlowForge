using FluentAssertions;
using FlowForge.App.Commands;

namespace FlowForge.App.Tests.Commands;

public sealed class CommandHistoryTests
{
    [Fact]
    public void Execute_NewCommand_ExecutesAndEnablesUndo()
    {
        var command = new RecordingCommand();
        var history = new CommandHistory();

        history.Execute(command);

        command.ExecuteCount.Should().Be(1);
        history.CanUndo.Should().BeTrue();
        history.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Undo_ExecutedCommand_UndoesAndEnablesRedo()
    {
        var command = new RecordingCommand();
        var history = new CommandHistory();
        history.Execute(command);

        history.Undo();

        command.UndoCount.Should().Be(1);
        history.CanUndo.Should().BeFalse();
        history.CanRedo.Should().BeTrue();
    }

    [Fact]
    public void Redo_UndoneCommand_ExecutesAgain()
    {
        var command = new RecordingCommand();
        var history = new CommandHistory();
        history.Execute(command);
        history.Undo();

        history.Redo();

        command.ExecuteCount.Should().Be(2);
        history.CanUndo.Should().BeTrue();
        history.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Execute_AfterUndo_ClearsRedoBranch()
    {
        var history = new CommandHistory();
        history.Execute(new RecordingCommand());
        history.Undo();

        history.Execute(new RecordingCommand());

        history.CanRedo.Should().BeFalse();
    }

    private sealed class RecordingCommand : FlowForge.App.Commands.ICommand
    {
        public int ExecuteCount { get; private set; }

        public int UndoCount { get; private set; }

        public void Execute()
        {
            ExecuteCount++;
        }

        public void Undo()
        {
            UndoCount++;
        }
    }
}
