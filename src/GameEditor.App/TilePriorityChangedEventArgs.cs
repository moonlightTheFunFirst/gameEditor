namespace GameEditor;

public sealed class TilePriorityChangedEventArgs : EventArgs
{
    public TilePriorityChangedEventArgs(TileSet tileSet, int tileId, int displayPriority)
    {
        TileSet = tileSet;
        TileId = tileId;
        DisplayPriority = displayPriority;
    }

    public TileSet TileSet { get; }

    public int TileId { get; }

    public int DisplayPriority { get; }
}
