using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameEditor;

public static class MapSerializer
{
    private const int SchemaVersion = 1;
    private const string Format = "gameEditor.map";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void Save(string path, MapDocument document, IReadOnlyList<TileSet> tileSets, string mapName)
    {
        var mapFile = CreateMapFile(document, tileSets, mapName);
        var json = JsonSerializer.Serialize(mapFile, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static MapLoadResult Load(string path)
    {
        var json = File.ReadAllText(path);
        var mapFile = JsonSerializer.Deserialize<MapFile>(json, JsonOptions)
            ?? throw new InvalidOperationException("Map file is empty or invalid.");

        return LoadFromMapFile(path, mapFile);
    }

    public static MapLoadResult LoadFromMapFile(string basePath, MapFile mapFile)
    {
        ValidateHeader(mapFile.Header);

        if (mapFile.Map.Width <= 0 || mapFile.Map.Height <= 0 || mapFile.Map.TileSize <= 0)
        {
            throw new InvalidOperationException("Map size or tile size is invalid.");
        }

        var tileSets = LoadTileSets(basePath, mapFile);
        var document = new MapDocument(mapFile.Map.Width, mapFile.Map.Height, mapFile.Map.TileSize);
        ApplyAttributeLists(document, mapFile);
        ApplyLayers(document, tileSets, mapFile);

        return new MapLoadResult(document, tileSets, mapFile.Map.Name);
    }

    public static MapFile CreateMapFile(MapDocument document, IReadOnlyList<TileSet> tileSets, string mapName)
    {
        return new MapFile
        {
            Header = new MapFileHeader
            {
                Format = Format,
                SchemaVersion = SchemaVersion,
                CreatedWith = "gameEditor",
                EditorVersion = Application.ProductVersion,
                SavedAt = DateTimeOffset.Now.ToString("O")
            },
            Map = new MapFileMap
            {
                Name = mapName,
                Width = document.Width,
                Height = document.Height,
                TileSize = document.TileSize
            },
            TileSets = tileSets.Select(CreateTileSet).ToList(),
            AttributeLists = document.AttributeLists.Select(CreateAttributeList).ToList(),
            Layers =
            [
                CreateLayer(document, tileSets, TileSetKind.Base, "base", "ベース", 0),
                CreateLayer(document, tileSets, TileSetKind.Advanced, "advanced", "アドバンス", 100)
            ]
        };
    }

    private static MapFileTileSet CreateTileSet(TileSet tileSet)
    {
        return new MapFileTileSet
        {
            Id = tileSet.Id,
            Name = tileSet.Name,
            Kind = GetKindId(tileSet.Kind),
            Image = tileSet.ImagePath,
            TileSize = tileSet.TileSize,
            TransparentColor = ToHexColor(tileSet.TransparentColor),
            AttributeListId = tileSet.AttributeListId,
            TileAttributes = tileSet.TileAttributes.ToDictionary()
        };
    }

    private static MapFileAttributeList CreateAttributeList(AttributeListDefinition list)
    {
        return new MapFileAttributeList
        {
            Id = list.Id,
            Name = list.Name,
            Values = list.Values.Select(value => new MapFileAttributeValue
            {
                Value = value.Value,
                Name = value.Name,
                Color = ToHexColor(value.Color)
            }).ToList()
        };
    }

    private static MapFileLayer CreateLayer(
        MapDocument document,
        IReadOnlyList<TileSet> tileSets,
        TileSetKind kind,
        string id,
        string name,
        int zIndex)
    {
        var tilesByIndex = tileSets.ToDictionary(tileSet => tileSet.Index);
        var tiles = new List<MapFileTile>();

        foreach (var (x, y, placement) in document.EnumerateTiles(kind))
        {
            if (!tilesByIndex.TryGetValue(placement.TileSetIndex, out var tileSet))
            {
                continue;
            }

            tiles.Add(new MapFileTile
            {
                X = x,
                Y = y,
                TileSetId = tileSet.Id,
                TileId = placement.TileId,
                Attribute = placement.AttributeValue
            });
        }

        return new MapFileLayer
        {
            Id = id,
            Name = name,
            Kind = GetKindId(kind),
            Visible = true,
            Locked = false,
            ZIndex = zIndex,
            Tiles = tiles
        };
    }

    private static string GetKindId(TileSetKind kind)
    {
        return kind == TileSetKind.Base ? "base" : "advanced";
    }

    private static void ValidateHeader(MapFileHeader header)
    {
        if (!string.Equals(header.Format, Format, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported map format: {header.Format}");
        }

        if (header.SchemaVersion > SchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported schema version: {header.SchemaVersion}");
        }
    }

    private static List<TileSet> LoadTileSets(string mapFilePath, MapFile mapFile)
    {
        var tileSets = new List<TileSet>();

        for (var index = 0; index < mapFile.TileSets.Count; index++)
        {
            var tileSetFile = mapFile.TileSets[index];
            var kind = ParseKind(tileSetFile.Kind);
            var imagePath = ResolveReferencedPath(mapFilePath, tileSetFile.Image);
            var tileSize = tileSetFile.TileSize > 0 ? tileSetFile.TileSize : mapFile.Map.TileSize;

            tileSets.Add(TileSet.Load(
                index,
                tileSetFile.Id,
                tileSetFile.Name,
                kind,
                imagePath,
                tileSetFile.Image,
                tileSize,
                ParseHexColor(tileSetFile.TransparentColor),
                tileSetFile.AttributeListId,
                tileSetFile.TileAttributes.ToDictionary()));
        }

        return tileSets;
    }

    private static void ApplyAttributeLists(MapDocument document, MapFile mapFile)
    {
        if (mapFile.AttributeLists.Count == 0)
        {
            return;
        }

        document.AttributeLists = mapFile.AttributeLists.Select(list => new AttributeListDefinition(
            list.Id,
            list.Name,
            list.Values.Select(value => new AttributeDefinition(
                value.Value,
                value.Name,
                ParseHexColor(value.Color) ?? Color.Transparent)).ToList())).ToList();
        document.ActiveAttributeListId = document.AttributeLists.FirstOrDefault()?.Id;
    }

    private static void ApplyLayers(MapDocument document, IReadOnlyList<TileSet> tileSets, MapFile mapFile)
    {
        var tileSetsById = tileSets.ToDictionary(tileSet => tileSet.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var layer in mapFile.Layers.OrderBy(layer => layer.ZIndex))
        {
            var layerKind = ParseKind(layer.Kind);

            foreach (var tile in layer.Tiles)
            {
                if (!tileSetsById.TryGetValue(tile.TileSetId, out var tileSet))
                {
                    continue;
                }

                if (tileSet.Kind != layerKind)
                {
                    continue;
                }

                document.SetTile(layerKind, tile.X, tile.Y, new TilePlacement(tileSet.Index, tile.TileId, tile.Attribute));
            }
        }
    }

    private static TileSetKind ParseKind(string value)
    {
        return value.Equals("base", StringComparison.OrdinalIgnoreCase)
            ? TileSetKind.Base
            : value.Equals("advanced", StringComparison.OrdinalIgnoreCase)
                ? TileSetKind.Advanced
                : throw new InvalidOperationException($"Unsupported tileset kind: {value}");
    }

    private static string ResolveReferencedPath(string mapFilePath, string referencedPath)
    {
        if (Path.IsPathRooted(referencedPath) && File.Exists(referencedPath))
        {
            return referencedPath;
        }

        var mapDirectory = Path.GetDirectoryName(mapFilePath) ?? "";
        var candidates = new List<string>
        {
            Path.Combine(mapDirectory, referencedPath),
            Path.Combine(Environment.CurrentDirectory, referencedPath),
            Path.Combine(AppContext.BaseDirectory, referencedPath)
        };

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            candidates.Add(Path.Combine(directory.FullName, referencedPath));
            directory = directory.Parent;
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Referenced image not found: {referencedPath}");
    }

    private static Color? ParseHexColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var hex = value.Trim().TrimStart('#');
        if (hex.Length != 6)
        {
            throw new InvalidOperationException($"Invalid color value: {value}");
        }

        return Color.FromArgb(
            Convert.ToInt32(hex[0..2], 16),
            Convert.ToInt32(hex[2..4], 16),
            Convert.ToInt32(hex[4..6], 16));
    }

    private static string? ToHexColor(Color? color)
    {
        return color is { } value ? $"#{value.R:X2}{value.G:X2}{value.B:X2}" : null;
    }
}
