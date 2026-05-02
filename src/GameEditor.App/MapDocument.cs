namespace GameEditor;

public sealed class MapDocument
{
    private readonly TilePlacement[,] baseTiles;
    private readonly TilePlacement[,] advancedTiles;

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
        baseTiles = new TilePlacement[width, height];
        advancedTiles = new TilePlacement[width, height];

        Clear();
    }

    public int Width { get; }

    public int Height { get; }

    public int TileSize { get; }

    public Size PixelSize => new(Width * TileSize, Height * TileSize);

    public TilePlacement GetTile(TileSetKind kind, int x, int y)
    {
        if (!IsInside(x, y))
        {
            return TilePlacement.Empty;
        }

        return GetLayer(kind)[x, y];
    }

    public IEnumerable<(int X, int Y, TilePlacement Placement)> EnumerateTiles(TileSetKind kind)
    {
        var layer = GetLayer(kind);

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var placement = layer[x, y];
                if (!placement.IsEmpty)
                {
                    yield return (x, y, placement);
                }
            }
        }
    }

    public void SetTile(TileSetKind kind, int x, int y, TilePlacement placement)
    {
        if (!IsInside(x, y))
        {
            return;
        }

        GetLayer(kind)[x, y] = placement;
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
                baseTiles[x, y] = TilePlacement.Empty;
                advancedTiles[x, y] = TilePlacement.Empty;
            }
        }
    }

    private TilePlacement[,] GetLayer(TileSetKind kind)
    {
        return kind == TileSetKind.Base ? baseTiles : advancedTiles;
    }
}
