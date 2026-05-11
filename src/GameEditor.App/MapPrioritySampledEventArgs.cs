namespace GameEditor;

public sealed class MapPrioritySampledEventArgs : EventArgs
{
    public MapPrioritySampledEventArgs(TileSetKind layerKind, Point cell, int displayPriority)
    {
        LayerKind = layerKind;
        Cell = cell;
        DisplayPriority = displayPriority;
    }

    public TileSetKind LayerKind { get; }

    public Point Cell { get; }

    public int DisplayPriority { get; }
}
