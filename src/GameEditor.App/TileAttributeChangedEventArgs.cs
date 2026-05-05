namespace GameEditor;

public sealed class TileAttributeChangedEventArgs : EventArgs
{
    public TileAttributeChangedEventArgs(TileSet tileSet, int tileId, IReadOnlyList<int> attributeValues)
    {
        TileSet = tileSet;
        TileId = tileId;
        AttributeValues = attributeValues;
    }

    public TileSet TileSet { get; }

    public int TileId { get; }

    public IReadOnlyList<int> AttributeValues { get; }
}
