namespace GameEditor;

public readonly record struct TilePlacement(int TileSetIndex, int TileId)
{
    public static TilePlacement Empty { get; } = new(-1, -1);

    public bool IsEmpty => TileSetIndex < 0 || TileId < 0;
}
