namespace GameEditor;

public sealed class MapLoadResult
{
    public MapLoadResult(MapDocument document, List<TileSet> tileSets, string mapName)
    {
        Document = document;
        TileSets = tileSets;
        MapName = mapName;
    }

    public MapDocument Document { get; }

    public List<TileSet> TileSets { get; }

    public string MapName { get; }
}
