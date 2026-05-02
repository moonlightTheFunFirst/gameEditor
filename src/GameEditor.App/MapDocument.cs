namespace GameEditor;

public sealed class MapDocument
{
    private readonly int[,] tileIds;

    public MapDocument(int width, int height, int tileSize)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (tileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize));
        }

        Width = width;
        Height = height;
        TileSize = tileSize;
        tileIds = new int[width, height];

        Clear();
    }

    public int Width { get; }

    public int Height { get; }

    public int TileSize { get; }

    public Size PixelSize => new(Width * TileSize, Height * TileSize);

    public int GetTile(int x, int y)
    {
        return IsInside(x, y) ? tileIds[x, y] : -1;
    }

    public void SetTile(int x, int y, int tileId)
    {
        if (!IsInside(x, y))
        {
            return;
        }

        tileIds[x, y] = tileId;
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }

    private void Clear()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                tileIds[x, y] = -1;
            }
        }
    }
}
