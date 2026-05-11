using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameEditor;

public sealed class AnimationFile
{
    public AnimationFileHeader Header { get; set; } = new();

    public AnimationFileClip Clip { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class AnimationFileHeader
{
    public string Format { get; set; } = "gameEditor.animation";

    public int SchemaVersion { get; set; } = 1;

    public string CreatedWith { get; set; } = "gameEditor";

    public string EditorVersion { get; set; } = Application.ProductVersion;

    public string SavedAt { get; set; } = DateTimeOffset.Now.ToString("O");

    public Dictionary<string, object?> Attributes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class AnimationFileClip
{
    public string Name { get; set; } = "NewAnimation";

    public int DurationMs { get; set; } = 400;

    public int Fps { get; set; } = 8;

    public bool Loop { get; set; } = true;

    public List<AnimationFileTrack> Tracks { get; set; } = [];

    public Dictionary<string, object?> Attributes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class AnimationFileTrack
{
    public string Type { get; set; } = "";

    public string Target { get; set; } = "";

    public List<AnimationFileKey> Keys { get; set; } = [];

    public Dictionary<string, object?> Attributes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class AnimationFileKey
{
    public int TimeMs { get; set; }

    public string? TileSetId { get; set; }

    public int? TileId { get; set; }

    public string? Name { get; set; }

    public string? Memo { get; set; }

    public Dictionary<string, object?> Attributes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
