namespace GameEditor;

public sealed class TileSetDefinition
{
    public TileSetDefinition(
        string id,
        string name,
        TileSetKind kind,
        string sourcePath,
        string imagePath,
        Color? transparentColor,
        string attributeFilePath,
        string? attributeListId,
        List<AttributeListDefinition> attributeLists,
        Dictionary<int, string> tileAttributes)
    {
        Id = id;
        Name = name;
        Kind = kind;
        SourcePath = sourcePath;
        ImagePath = imagePath;
        TransparentColor = transparentColor;
        AttributeFilePath = attributeFilePath;
        AttributeListId = attributeListId;
        AttributeLists = attributeLists;
        TileAttributes = tileAttributes;
    }

    public string Id { get; }

    public string Name { get; }

    public TileSetKind Kind { get; }

    public string SourcePath { get; }

    public string ImagePath { get; }

    public Color? TransparentColor { get; }

    public string AttributeFilePath { get; }

    public string? AttributeListId { get; set; }

    public List<AttributeListDefinition> AttributeLists { get; }

    public Dictionary<int, string> TileAttributes { get; }

    public override string ToString()
    {
        return Name;
    }
}
