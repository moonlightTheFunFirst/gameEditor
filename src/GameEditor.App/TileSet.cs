namespace GameEditor;

public sealed class TileSet : IDisposable
{
    private TileSet(Bitmap image, string sourcePath, int tileSize)
    {
        Image = image;
        SourcePath = sourcePath;
        TileSize = tileSize;
        Columns = image.Width / tileSize;
        Rows = image.Height / tileSize;
    }

    public Bitmap Image { get; }

    public string SourcePath { get; }

    public int TileSize { get; }

    public int Columns { get; }

    public int Rows { get; }

    public int TileCount => Columns * Rows;

    public static TileSet Load(string path, int tileSize)
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

        return new TileSet(image, path, tileSize);
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

        graphics.DrawImage(Image, destination, GetSourceRectangle(tileId), GraphicsUnit.Pixel);
    }

    public void Dispose()
    {
        Image.Dispose();
    }
}
