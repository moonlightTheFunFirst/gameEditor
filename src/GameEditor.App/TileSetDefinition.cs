namespace GameEditor;

public sealed class TileSetDefinition
{
    public const int LegacyTileSize = 32;

    public TileSetDefinition(
        string id,
        string name,
        TileSetKind kind,
        string sourcePath,
        string imagePath,
        int imageWidth,
        int imageHeight,
        Color? transparentColor,
        string attributeFilePath,
        string? attributeListId,
        List<AttributeListDefinition> attributeLists,
        Dictionary<int, string> tileAttributes,
        Dictionary<int, int> tilePriorities,
        Dictionary<int, TileSetTileSizeAttributes>? tileSizeAttributes = null)
    {
        Id = id;
        Name = name;
        Kind = kind;
        SourcePath = sourcePath;
        ImagePath = imagePath;
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
        TransparentColor = transparentColor;
        AttributeFilePath = attributeFilePath;
        AttributeListId = attributeListId;
        AttributeLists = attributeLists;
        TileAttributes = tileAttributes;
        TilePriorities = tilePriorities;
        TileSizeAttributes = tileSizeAttributes ?? [];
        TileSizeAttributes.TryAdd(
            LegacyTileSize,
            new TileSetTileSizeAttributes(
                attributeListId,
                tileAttributes.ToDictionary(),
                tilePriorities.ToDictionary()));
    }

    public string Id { get; }

    public string Name { get; }

    public TileSetKind Kind { get; }

    public string SourcePath { get; }

    public string ImagePath { get; }

    public int ImageWidth { get; }

    public int ImageHeight { get; }

    public Color? TransparentColor { get; }

    public string AttributeFilePath { get; }

    public string? AttributeListId { get; set; }

    public List<AttributeListDefinition> AttributeLists { get; }

    public Dictionary<int, string> TileAttributes { get; }

    public Dictionary<int, int> TilePriorities { get; }

    public Dictionary<int, TileSetTileSizeAttributes> TileSizeAttributes { get; }

    public TileSetTileSizeAttributes GetAttributesForTileSize(int tileSize)
    {
        if (TileSizeAttributes.TryGetValue(tileSize, out var attributes))
        {
            return attributes;
        }

        var created = new TileSetTileSizeAttributes(AttributeListId);
        TileSizeAttributes[tileSize] = created;
        return created;
    }

    public bool SupportsTileSize(int tileSize)
    {
        return tileSize > 0
            && ImageWidth % tileSize == 0
            && ImageHeight % tileSize == 0;
    }

    public void SetAttributesForTileSize(
        int tileSize,
        string? attributeListId,
        Dictionary<int, string> tileAttributes,
        Dictionary<int, int> tilePriorities)
    {
        var attributes = GetAttributesForTileSize(tileSize);
        attributes.AttributeListId = attributeListId;
        attributes.TileAttributes.Clear();
        foreach (var (tileId, value) in tileAttributes)
        {
            attributes.TileAttributes[tileId] = value;
        }

        attributes.TilePriorities.Clear();
        foreach (var (tileId, value) in tilePriorities)
        {
            attributes.TilePriorities[tileId] = value;
        }

        if (tileSize != LegacyTileSize)
        {
            return;
        }

        AttributeListId = attributeListId;
        TileAttributes.Clear();
        foreach (var (tileId, value) in tileAttributes)
        {
            TileAttributes[tileId] = value;
        }

        TilePriorities.Clear();
        foreach (var (tileId, value) in tilePriorities)
        {
            TilePriorities[tileId] = value;
        }
    }

    public override string ToString()
    {
        return Name;
    }
}
