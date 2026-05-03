namespace GameEditor;

public sealed class AttributeEditCommand : IMapEditCommand
{
    private readonly IReadOnlyList<AttributeChange> changes;

    public AttributeEditCommand(string name, TileSetKind layerKind, IReadOnlyList<AttributeChange> changes)
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

