namespace GameEditor;

public sealed class TileEditCommand : IMapEditCommand
{
    private readonly IReadOnlyList<TileChange> changes;

    public TileEditCommand(string name, TileSetKind layerKind, IReadOnlyList<TileChange> changes)
    {
        Name = name;
        LayerKind = layerKind;
        this.changes = changes;
    }

    public string Name { get; }

    public TileSetKind LayerKind { get; }

    public int AffectedTiles => changes.Count;

    public void Undo(MapDocument document)
    {
        foreach (var change in changes)
        {
            document.SetTile(LayerKind, change.X, change.Y, change.Before);
        }
    }

    public void Redo(MapDocument document)
    {
        foreach (var change in changes)
        {
            document.SetTile(LayerKind, change.X, change.Y, change.After);
        }
    }
}
