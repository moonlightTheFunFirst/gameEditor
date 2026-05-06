namespace GameEditor;

public readonly record struct TilePlacement(int TileSetIndex, int TileId, string AttributeValuesCsv = "")
{
    public static TilePlacement Empty { get; } = new(-1, -1);

    public bool IsEmpty => TileSetIndex < 0 || TileId < 0;

    public IReadOnlyList<int> AttributeValues => ParseAttributeValues(AttributeValuesCsv);

    public TilePlacement WithAttributes(IEnumerable<int> attributeValues)
    {
        return this with { AttributeValuesCsv = FormatAttributeValues(attributeValues) };
    }

    public static string FormatAttributeValues(IEnumerable<int> attributeValues)
    {
        return string.Join(",", attributeValues.Distinct().Order());
    }

    public static IReadOnlyList<int> RemapAttributeValues(
        IEnumerable<int> attributeValues,
        IReadOnlyDictionary<int, int?> valueRemap)
    {
        return attributeValues
            .Select(value => valueRemap.TryGetValue(value, out var remapped) ? remapped : value)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .Distinct()
            .Order()
            .ToArray();
    }

    public static string RemapAttributeValuesCsv(string? value, IReadOnlyDictionary<int, int?> valueRemap)
    {
        return FormatAttributeValues(RemapAttributeValues(ParseAttributeValues(value), valueRemap));
    }

    public static IReadOnlyList<int> ParseAttributeValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var parsed) ? parsed : (int?)null)
            .Where(parsed => parsed is not null)
            .Select(parsed => parsed!.Value)
            .Distinct()
            .Order()
            .ToArray();
    }
}
