namespace GameEditor;

public readonly record struct TileChange(int X, int Y, TilePlacement Before, TilePlacement After);
