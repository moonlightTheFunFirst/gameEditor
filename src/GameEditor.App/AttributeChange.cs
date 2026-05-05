namespace GameEditor;

public readonly record struct AttributeChange(int X, int Y, TilePlacement Before, TilePlacement After);
