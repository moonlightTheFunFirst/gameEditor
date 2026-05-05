namespace GameEditor;

public sealed class TileSet : IDisposable
{
    private TileSet(
        int index,
        string id,
        string name,
        TileSetKind kind,
        Bitmap image,
        string sourcePath,
        string imagePath,
        int tileSize,
        Color? transparentColor,
        string? attributeListId,
        Dictionary<int, string> tileAttributes)
    {
        Index = index;
        Id = id;
        Name = name;
        Kind = kind;
        Image = image;
        SourcePath = sourcePath;
        ImagePath = imagePath;
        TileSize = tileSize;
        TransparentColor = transparentColor;
        AttributeListId = attributeListId;
        TileAttributes = tileAttributes;
        Columns = image.Width / tileSize;
        Rows = image.Height / tileSize;
    }

    public int Index { get; }

    public string Id { get; }

    public string Name { get; }

    public TileSetKind Kind { get; }

    public Bitmap Image { get; }

    public string SourcePath { get; }

    public string ImagePath { get; }

    public int TileSize { get; }

    public Color? TransparentColor { get; }

    public string? AttributeListId { get; set; }

    public Dictionary<int, string> TileAttributes { get; }

    public int Columns { get; }

    public int Rows { get; }

    public int TileCount => Columns * Rows;

    public static TileSet Load(
        int index,
        string id,
        string name,
        TileSetKind kind,
        string path,
        string imagePath,
        int tileSize,
        Color? transparentColor = null,
        string? attributeListId = null,
        Dictionary<int, string>? tileAttributes = null)
    {
        if (tileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize));
        }

        using var source = System.Drawing.Image.FromFile(path);
        var image = new Bitmap(source);

        if (image.Width % tileSize != 0 || image.Height % tileSize != 0)
        {
            image.Dispose();
            throw new InvalidOperationException($"Tileset size must be divisible by {tileSize}. Actual: {source.Width}x{source.Height}");
        }

        return new TileSet(
            index,
            id,
            name,
            kind,
            image,
            path,
            imagePath,
            tileSize,
            transparentColor,
            attributeListId,
            tileAttributes ?? []);
    }

    public IReadOnlyList<int> GetDefaultAttributes(int tileId)
    {
        return TileAttributes.TryGetValue(tileId, out var value)
            ? TilePlacement.ParseAttributeValues(value)
            : [];
    }

    public void SetDefaultAttributes(int tileId, IEnumerable<int> values)
    {
        if (tileId < 0 || tileId >= TileCount)
        {
            return;
        }

        var csv = TilePlacement.FormatAttributeValues(values);
        if (string.IsNullOrEmpty(csv))
        {
            TileAttributes.Remove(tileId);
            return;
        }

        TileAttributes[tileId] = csv;
    }

    public Rectangle GetSourceRectangle(int tileId)
    {
        if (tileId < 0 || tileId >= TileCount)
        {
            throw new ArgumentOutOfRangeException(nameof(tileId));
        }

        var x = tileId % Columns;
        var y = tileId / Columns;
        return new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize);
    }

    public void DrawTile(Graphics graphics, int tileId, Rectangle destination)
    {
        if (tileId < 0 || tileId >= TileCount)
        {
            return;
        }

        var source = GetSourceRectangle(tileId);
        if (TransparentColor is not { } colorKey)
        {
            graphics.DrawImage(Image, destination, source, GraphicsUnit.Pixel);
            return;
        }

        using var attributes = new System.Drawing.Imaging.ImageAttributes();
        attributes.SetColorKey(colorKey, colorKey);
        graphics.DrawImage(Image, destination, source.X, source.Y, source.Width, source.Height, GraphicsUnit.Pixel, attributes);
    }

    public void Dispose()
    {
        Image.Dispose();
    }
}
