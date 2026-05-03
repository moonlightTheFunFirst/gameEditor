using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameEditor;

public static class ProjectSerializer
{
    private const int SchemaVersion = 1;
    private const string Format = "gameEditor.project";
    private const string EmbeddedStorage = "embedded";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void Save(string path, ProjectDocument project)
    {
        var projectFile = CreateProjectFile(project);
        var json = JsonSerializer.Serialize(projectFile, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static ProjectLoadResult Load(string path)
    {
        var json = File.ReadAllText(path);
        var projectFile = JsonSerializer.Deserialize<ProjectFile>(json, JsonOptions)
            ?? throw new InvalidOperationException("Project file is empty or invalid.");

        ValidateHeader(projectFile.Header);

        var project = new ProjectDocument(projectFile.Project.Name)
        {
            FilePath = path,
            IsDirty = false
        };

        foreach (var map in projectFile.Maps)
        {
            if (!string.Equals(map.Storage, EmbeddedStorage, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unsupported project map storage: {map.Storage}");
            }

            if (map.MapFile is null)
            {
                throw new InvalidOperationException($"Project map is missing embedded data: {map.Name}");
            }

            var loaded = MapSerializer.LoadFromMapFile(path, map.MapFile);
            var name = string.IsNullOrWhiteSpace(map.Name) ? loaded.MapName : map.Name;
            var document = new MapEditorDocument(name, loaded.Document, loaded.TileSets);
            project.Maps.Add(new ProjectMapItem(document, map.Id));
        }

        return new ProjectLoadResult(project);
    }

    private static ProjectFile CreateProjectFile(ProjectDocument project)
    {
        return new ProjectFile
        {
            Header = new ProjectFileHeader
            {
                Format = Format,
                SchemaVersion = SchemaVersion,
                CreatedWith = "gameEditor",
                EditorVersion = Application.ProductVersion,
                SavedAt = DateTimeOffset.Now.ToString("O")
            },
            Project = new ProjectFileProject
            {
                Name = project.Name
            },
            Maps = project.Maps.Select(CreateProjectMap).ToList()
        };
    }

    private static ProjectFileMap CreateProjectMap(ProjectMapItem map)
    {
        return new ProjectFileMap
        {
            Id = map.Id,
            Name = map.Name,
            Storage = EmbeddedStorage,
            MapFile = MapSerializer.CreateMapFile(map.Document.Map, map.Document.TileSets, map.Document.Name)
        };
    }

    private static void ValidateHeader(ProjectFileHeader header)
    {
        if (!string.Equals(header.Format, Format, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported project format: {header.Format}");
        }

        if (header.SchemaVersion > SchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported project schema version: {header.SchemaVersion}");
        }
    }
}
