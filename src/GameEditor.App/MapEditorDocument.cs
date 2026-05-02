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

    public TileSet? GetTileSet(TileSetKind kind)
    {
        return TileSets.FirstOrDefault(tileSet => tileSet.Kind == kind);
    }

    public void ReplaceTileSet(TileSetKind kind, TileSet replacement)
    {
        for (var i = 0; i < TileSets.Count; i++)
        {
            if (TileSets[i].Kind != kind)
            {
                continue;
            }

            TileSets[i].Dispose();
            TileSets[i] = replacement;
            Viewport.TileSets = TileSets;
            Viewport.Invalidate();
            return;
        }

        TileSets.Add(replacement);
        Viewport.TileSets = TileSets;
        Viewport.Invalidate();
    }

    public void Dispose()
    {
        foreach (var tileSet in TileSets)
        {
            tileSet.Dispose();
        }
    }
}
