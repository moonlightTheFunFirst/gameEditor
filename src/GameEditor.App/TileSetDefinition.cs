namespace GameEditor;

public sealed class TileSetDefinition
{
    public TileSetDefinition(
        string id,
        string name,
        TileSetKind kind,
        string sourcePath,
        string imagePath,
        Color? transparentColor)
    {
        Id = id;
        Name = name;
        Kind = kind;
        SourcePath = sourcePath;
        ImagePath = imagePath;
        TransparentColor = transparentColor;
    }

    public string Id { get; }

    public string Name { get; }

    public TileSetKind Kind { get; }

    public string SourcePath { get; }

    public string ImagePath { get; }

    public Color? TransparentColor { get; }

    public override string ToString()
    {
        return Name;
    }
}
