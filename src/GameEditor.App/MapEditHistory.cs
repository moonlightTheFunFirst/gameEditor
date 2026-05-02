namespace GameEditor;

public sealed class MapEditHistory
{
    private readonly Stack<IMapEditCommand> undoStack = new();
    private readonly Stack<IMapEditCommand> redoStack = new();

    public bool CanUndo => undoStack.Count > 0;

    public bool CanRedo => redoStack.Count > 0;

    public void Push(IMapEditCommand command)
    {
        if (command.AffectedTiles <= 0)
        {
            return;
        }

        undoStack.Push(command);
        redoStack.Clear();
    }

    public IMapEditCommand? Undo(MapDocument document)
    {
        if (!CanUndo)
        {
            return null;
        }

        var command = undoStack.Pop();
        command.Undo(document);
        redoStack.Push(command);
        return command;
    }

    public IMapEditCommand? Redo(MapDocument document)
    {
        if (!CanRedo)
        {
            return null;
        }

        var command = redoStack.Pop();
        command.Redo(document);
        undoStack.Push(command);
        return command;
    }

    public void Clear()
    {
        undoStack.Clear();
        redoStack.Clear();
    }
}
