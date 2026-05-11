namespace GameEditor;

public sealed class TileSetAttributeFile
{
    public string? AttributeListId { get; set; }

    public List<MapFileAttributeList> AttributeLists { get; set; } = [];

    public Dictionary<int, List<int>> TileAttributes { get; set; } = [];

    public Dictionary<int, int> TilePriorities { get; set; } = [];

    public Dictionary<int, TileSetTileSizeAttributeFile> TileSizeData { get; set; } = [];
}

public sealed class TileSetTileSizeAttributeFile
{
    public string? AttributeListId { get; set; }

    public Dictionary<int, List<int>> TileAttributes { get; set; } = [];

    public Dictionary<int, int> TilePriorities { get; set; } = [];
}
