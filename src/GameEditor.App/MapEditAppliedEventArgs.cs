namespace GameEditor;

public sealed class MapEditAppliedEventArgs : EventArgs
{
    public MapEditAppliedEventArgs(MapEditTool tool, TileSetKind? layerKind, Point cell, int affectedTiles)
    {
        Tool = tool;
        LayerKind = layerKind;
        Cell = cell;
        AffectedTiles = affectedTiles;
    }

    public MapEditTool Tool { get; }

    public TileSetKind? LayerKind { get; }

    public Point Cell { get; }

    public int AffectedTiles { get; }
}
