using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameEditor;

public sealed class ProjectFile
{
    public ProjectFileHeader Header { get; set; } = new();

    public ProjectFileProject Project { get; set; } = new();

    public List<ProjectFileMap> Maps { get; set; } = [];

    public List<ProjectFileAnimation> Animations { get; set; } = [];

    public List<ProjectFileResource> Resources { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class ProjectFileHeader
{
    public string Format { get; set; } = "gameEditor.project";

    public int SchemaVersion { get; set; } = 1;

    public string CreatedWith { get; set; } = "gameEditor";

    public string EditorVersion { get; set; } = Application.ProductVersion;

    public string SavedAt { get; set; } = DateTimeOffset.Now.ToString("O");

    public Dictionary<string, object?> Attributes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class ProjectFileProject
{
    public string Name { get; set; } = "NewProject";

    public Dictionary<string, object?> Attributes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class ProjectFileMap
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Storage { get; set; } = "embedded";

    public MapFile? MapFile { get; set; }

    public Dictionary<string, object?> Attributes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class ProjectFileAnimation
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public Dictionary<string, object?> Attributes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public sealed class ProjectFileResource
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public Dictionary<string, object?> Attributes { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
