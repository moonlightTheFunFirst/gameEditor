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
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void Save(string path, MapDocument document, IReadOnlyList<TileSet> tileSets, string mapName)
    {
        var mapFile = CreateMapFile(document, tileSets, mapName);
        var json = JsonSerializer.Serialize(mapFile, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static MapFile CreateMapFile(MapDocument document, IReadOnlyList<TileSet> tileSets, string mapName)
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
            TransparentColor = ToHexColor(tileSet.TransparentColor)
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
                TileId = placement.TileId
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

    private static string? ToHexColor(Color? color)
    {
        return color is { } value ? $"#{value.R:X2}{value.G:X2}{value.B:X2}" : null;
    }
}
