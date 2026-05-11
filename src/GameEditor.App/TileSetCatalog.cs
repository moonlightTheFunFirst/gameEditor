using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameEditor;

public static class TileSetCatalog
{
    private static readonly string[] SupportedExtensions = [".bmp", ".png"];
    private static readonly Color TransparentColor = Color.Magenta;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static List<TileSetDefinition> Load()
    {
        var directory = ResolveTilesDirectory();
        if (directory is null)
        {
            return [];
        }

        var files = Directory
            .EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

        var definitions = new List<TileSetDefinition>();
        foreach (var path in files)
        {
            var imageInfo = InspectImage(path);
            var hasTransparency = imageInfo.HasTransparentColor;
            var kind = hasTransparency ? TileSetKind.Advanced : TileSetKind.Base;
            var id = Path.GetFileNameWithoutExtension(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var imagePath = Path.Combine("resources", "tiles", Path.GetFileName(path)).Replace('\\', '/');
            var attributeFilePath = GetAttributeFilePath(path);
            var attributes = LoadAttributes(attributeFilePath);
            var tileSizeAttributes = CreateTileSizeAttributes(attributes);
            var legacyAttributes = tileSizeAttributes.TryGetValue(TileSetDefinition.LegacyTileSize, out var legacy)
                ? legacy
                : new TileSetTileSizeAttributes(
                    attributes.AttributeListId,
                    attributes.TileAttributes.ToDictionary(
                        pair => pair.Key,
                        pair => TilePlacement.FormatAttributeValues(pair.Value)),
                    attributes.TilePriorities.ToDictionary(pair => pair.Key, pair => pair.Value));

            definitions.Add(new TileSetDefinition(
                id,
                name,
                kind,
                path,
                imagePath,
                imageInfo.Width,
                imageInfo.Height,
                hasTransparency ? TransparentColor : null,
                attributeFilePath,
                legacyAttributes.AttributeListId,
                attributes.AttributeLists.Select(ToAttributeListDefinition).ToList(),
                legacyAttributes.TileAttributes.ToDictionary(),
                legacyAttributes.TilePriorities.ToDictionary(),
                tileSizeAttributes));
        }

        return definitions;
    }

    public static void SaveAttributes(
        TileSetDefinition definition,
        TileSet tileSet,
        IReadOnlyList<AttributeListDefinition> attributeLists)
    {
        var tileSizeAttributes = definition.TileSizeAttributes.ToDictionary(
            pair => pair.Key,
            pair => CloneAttributes(pair.Value));
        tileSizeAttributes[tileSet.TileSize] = new TileSetTileSizeAttributes(
            tileSet.AttributeListId,
            tileSet.TileAttributes.ToDictionary(),
            tileSet.TilePriorities.ToDictionary());
        var legacyAttributes = tileSizeAttributes.TryGetValue(TileSetDefinition.LegacyTileSize, out var legacy)
            ? legacy
            : new TileSetTileSizeAttributes(
                definition.AttributeListId,
                definition.TileAttributes.ToDictionary(),
                definition.TilePriorities.ToDictionary());

        var file = new TileSetAttributeFile
        {
            AttributeListId = legacyAttributes.AttributeListId,
            AttributeLists = attributeLists.Select(ToMapFileAttributeList).ToList(),
            TileAttributes = legacyAttributes.TileAttributes.ToDictionary(
                pair => pair.Key,
                pair => TilePlacement.ParseAttributeValues(pair.Value).ToList()),
            TilePriorities = legacyAttributes.TilePriorities.ToDictionary(pair => pair.Key, pair => pair.Value),
            TileSizeData = tileSizeAttributes
                .OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => ToTileSizeAttributeFile(pair.Value))
        };

        var json = JsonSerializer.Serialize(file, JsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(definition.AttributeFilePath) ?? ".");
        File.WriteAllText(definition.AttributeFilePath, json);
    }

    private static string GetAttributeFilePath(string imagePath)
    {
        var directory = Path.GetDirectoryName(imagePath) ?? "";
        var name = Path.GetFileNameWithoutExtension(imagePath);
        return Path.Combine(directory, $"{name}.attrs.json");
    }

    private static TileSetAttributeFile LoadAttributes(string path)
    {
        if (!File.Exists(path))
        {
            return new TileSetAttributeFile();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<TileSetAttributeFile>(json, JsonOptions) ?? new TileSetAttributeFile();
        }
        catch
        {
            return new TileSetAttributeFile();
        }
    }

    private static Dictionary<int, TileSetTileSizeAttributes> CreateTileSizeAttributes(TileSetAttributeFile file)
    {
        var result = new Dictionary<int, TileSetTileSizeAttributes>();
        foreach (var (tileSize, data) in file.TileSizeData.Where(pair => pair.Key > 0))
        {
            result[tileSize] = new TileSetTileSizeAttributes(
                data.AttributeListId,
                data.TileAttributes.ToDictionary(
                    pair => pair.Key,
                    pair => TilePlacement.FormatAttributeValues(pair.Value)),
                data.TilePriorities.ToDictionary(pair => pair.Key, pair => pair.Value));
        }

        if (!result.ContainsKey(TileSetDefinition.LegacyTileSize))
        {
            result[TileSetDefinition.LegacyTileSize] = new TileSetTileSizeAttributes(
                file.AttributeListId,
                file.TileAttributes.ToDictionary(
                    pair => pair.Key,
                    pair => TilePlacement.FormatAttributeValues(pair.Value)),
                file.TilePriorities.ToDictionary(pair => pair.Key, pair => pair.Value));
        }

        return result;
    }

    private static TileSetTileSizeAttributes CloneAttributes(TileSetTileSizeAttributes attributes)
    {
        return new TileSetTileSizeAttributes(
            attributes.AttributeListId,
            attributes.TileAttributes.ToDictionary(),
            attributes.TilePriorities.ToDictionary());
    }

    private static TileSetTileSizeAttributeFile ToTileSizeAttributeFile(TileSetTileSizeAttributes attributes)
    {
        return new TileSetTileSizeAttributeFile
        {
            AttributeListId = attributes.AttributeListId,
            TileAttributes = attributes.TileAttributes.ToDictionary(
                pair => pair.Key,
                pair => TilePlacement.ParseAttributeValues(pair.Value).ToList()),
            TilePriorities = attributes.TilePriorities.ToDictionary(pair => pair.Key, pair => pair.Value)
        };
    }

    private static AttributeListDefinition ToAttributeListDefinition(MapFileAttributeList list)
    {
        return new AttributeListDefinition(
            list.Id,
            list.Name,
            list.Values.Select(value => new AttributeDefinition(
                value.Value,
                value.Name,
                ParseHexColor(value.Color) ?? Color.Transparent,
                value.DisplayText,
                value.Memo)).ToList());
    }

    private static MapFileAttributeList ToMapFileAttributeList(AttributeListDefinition list)
    {
        return new MapFileAttributeList
        {
            Id = list.Id,
            Name = list.Name,
            Values = list.Values.Select(value => new MapFileAttributeValue
            {
                Value = value.Value,
                Name = value.Name,
                DisplayText = value.DisplayText,
                Memo = value.Memo,
                Color = ToHexColor(value.Color)
            }).ToList()
        };
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
            return null;
        }

        return Color.FromArgb(
            Convert.ToInt32(hex[0..2], 16),
            Convert.ToInt32(hex[2..4], 16),
            Convert.ToInt32(hex[4..6], 16));
    }

    private static string? ToHexColor(Color color)
    {
        return color.A == 0 ? null : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static (bool HasTransparentColor, int Width, int Height) InspectImage(string path)
    {
        using var image = new Bitmap(path);
        var transparentArgb = TransparentColor.ToArgb();
        var hasTransparentColor = false;

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image.GetPixel(x, y).ToArgb() == transparentArgb)
                {
                    hasTransparentColor = true;
                    break;
                }
            }

            if (hasTransparentColor)
            {
                break;
            }
        }

        return (hasTransparentColor, image.Width, image.Height);
    }

    private static string? ResolveTilesDirectory()
    {
        var candidates = new List<string>();
        AddTilesDirectoryCandidates(candidates, Environment.CurrentDirectory);
        AddTilesDirectoryCandidates(candidates, AppContext.BaseDirectory);

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists)
            .OrderBy(IsBuildOutputPath)
            .FirstOrDefault();
    }

    private static void AddTilesDirectoryCandidates(List<string> candidates, string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            candidates.Add(Path.Combine(directory.FullName, "resources", "tiles"));
            directory = directory.Parent;
        }
    }

    private static bool IsBuildOutputPath(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase));
    }
}
