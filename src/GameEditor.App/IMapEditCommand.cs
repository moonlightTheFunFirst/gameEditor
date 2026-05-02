namespace GameEditor;

public interface IMapEditCommand
{
    string Name { get; }

    int AffectedTiles { get; }

    void Undo(MapDocument document);

    void Redo(MapDocument document);
}
