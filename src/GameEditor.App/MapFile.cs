using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameEditor;

public sealed class MapFile
{
    public MapFileHeader Header { get; set; } = new();

    public MapFileMap Map { get; set; } = new();

    public List<MapFileTileSet> TileSets { get; set; } = [];

    public List<MapFileAttributeList> AttributeLists { get; set; } = [];

    public List<MapFileLayer> Layers { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class MapFileHeader
{
    public string Format { get; set; } = "gameEditor.map";

    public int SchemaVersion { get; set; } = 1;

    public string CreatedWith { get; set; } = "gameEditor";

    public string EditorVersion { get; set; } = Application.ProductVersion;

    public string SavedAt { get; set; } = DateTimeOffset.Now.ToString("O");

    public Dictionary<string, object?> Attributes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class MapFileMap
{
    public string Name { get; set; } = "Untitled";

    public int Width { get; set; }

    public int Height { get; set; }

    public int TileSize { get; set; }

    public Dictionary<string, object?> Attributes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class MapFileTileSet
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Kind { get; set; } = "";

    public string Image { get; set; } = "";

    public int TileSize { get; set; }

    public string? TransparentColor { get; set; }

    public string? AttributeListId { get; set; }

    public Dictionary<int, List<int>> TileAttributes { get; set; } = [];

    public Dictionary<string, object?> Attributes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class MapFileLayer
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Kind { get; set; } = "";

    public bool Visible { get; set; } = true;

    public bool Locked { get; set; }

    public int ZIndex { get; set; }

    public List<MapFileTile> Tiles { get; set; } = [];

    public Dictionary<string, object?> Attributes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class MapFileTile
{
    public int X { get; set; }

    public int Y { get; set; }

    public string TileSetId { get; set; } = "";

    public int TileId { get; set; }

    public int? Attribute { get; set; }

    public List<int> AttributeValues { get; set; } = [];

    public Dictionary<string, object?> Attributes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class MapFileAttributeList
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public List<MapFileAttributeValue> Values { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class MapFileAttributeValue
{
    public int Value { get; set; }

    public string Name { get; set; } = "";

    public string? Color { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
