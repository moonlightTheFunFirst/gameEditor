namespace GameEditor;

public readonly record struct TilePlacement(int TileSetIndex, int TileId, int? AttributeValue = null)
{
    public static TilePlacement Empty { get; } = new(-1, -1);

    public bool IsEmpty => TileSetIndex < 0 || TileId < 0;

    public TilePlacement WithAttribute(int? attributeValue)
    {
        return this with { AttributeValue = attributeValue };
    }
}
