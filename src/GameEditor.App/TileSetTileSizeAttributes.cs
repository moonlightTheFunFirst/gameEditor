namespace GameEditor;

public sealed class TileSetTileSizeAttributes
{
    public TileSetTileSizeAttributes(
        string? attributeListId,
        Dictionary<int, string>? tileAttributes = null,
        Dictionary<int, int>? tilePriorities = null)
    {
        AttributeListId = attributeListId;
        TileAttributes = tileAttributes ?? [];
        TilePriorities = tilePriorities ?? [];
    }

    public string? AttributeListId { get; set; }

    public Dictionary<int, string> TileAttributes { get; }

    public Dictionary<int, int> TilePriorities { get; }
}
