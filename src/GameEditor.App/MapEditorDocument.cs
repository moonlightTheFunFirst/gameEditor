namespace GameEditor;

public sealed class MapEditorDocument : IDisposable
{
    public MapEditorDocument(string name, MapDocument map, List<TileSet> tileSets)
    {
        Name = name;
        Map = map;
        TileSets = tileSets;
        Viewport = new MapViewport
        {
            Document = map,
            TileSets = tileSets
        };
    }

    public string Name { get; set; }

    public string? FilePath { get; set; }

    public MapDocument Map { get; }

    public List<TileSet> TileSets { get; }

    public MapEditHistory History { get; } = new();

    public MapViewport Viewport { get; }

    public bool IsDirty { get; set; }

    public void Dispose()
    {
        foreach (var tileSet in TileSets)
        {
            tileSet.Dispose();
        }
    }
}
