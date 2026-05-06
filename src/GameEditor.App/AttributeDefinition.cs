namespace GameEditor;

public sealed class AttributeDefinition
{
    private static readonly IReadOnlyList<AttributeDefinition> BuiltInDefinitions =
    [
        new AttributeDefinition(0, "None", Color.Transparent, "N", "なし"),
        new AttributeDefinition(1, "Blocked", Color.FromArgb(160, 220, 50, 50), "B", "通行禁止"),
        new AttributeDefinition(2, "Event", Color.FromArgb(160, 80, 160, 255), "E", "イベント")
    ];

    public AttributeDefinition()
    {
    }

    public AttributeDefinition(int value, string name, Color color, string displayText = "", string memo = "")
    {
        Value = value;
        Name = name;
        Color = color;
        DisplayText = displayText;
        Memo = memo;
    }

    public int Value { get; set; }

    public string Name { get; set; } = "";

    public string DisplayText { get; set; } = "";

    public string Memo { get; set; } = "";

    public Color Color { get; set; } = Color.Transparent;

    public static IReadOnlyList<AttributeDefinition> CreateBuiltInDefaults()
    {
        return BuiltInDefinitions
            .Select(definition => new AttributeDefinition(
                definition.Value,
                definition.Name,
                definition.Color,
                definition.DisplayText,
                definition.Memo))
            .ToArray();
    }

    public static bool IsBuiltInValue(int value)
    {
        return BuiltInDefinitions.Any(definition => definition.Value == value);
    }

    public static bool EnsureBuiltInDefaults(IList<AttributeDefinition> values)
    {
        var changed = false;
        foreach (var builtIn in BuiltInDefinitions)
        {
            var existing = values.FirstOrDefault(value => value.Value == builtIn.Value);
            if (existing is null)
            {
                InsertByValue(values, new AttributeDefinition(
                    builtIn.Value,
                    builtIn.Name,
                    builtIn.Color,
                    builtIn.DisplayText,
                    builtIn.Memo));
                changed = true;
                continue;
            }

            changed |= FillBuiltInDefaults(existing, builtIn);
        }

        return changed;
    }

    public string GetDisplayText()
    {
        if (!string.IsNullOrWhiteSpace(DisplayText))
        {
            return DisplayText.Trim();
        }

        return string.IsNullOrWhiteSpace(Name)
            ? Value.ToString()
            : Name.Trim()[0].ToString().ToUpperInvariant();
    }

    private static void InsertByValue(IList<AttributeDefinition> values, AttributeDefinition definition)
    {
        var insertIndex = values
            .Select((value, index) => new { value.Value, index })
            .FirstOrDefault(item => item.Value > definition.Value)
            ?.index;
        if (insertIndex is null)
        {
            values.Add(definition);
            return;
        }

        values.Insert(insertIndex.Value, definition);
    }

    private static bool FillBuiltInDefaults(AttributeDefinition value, AttributeDefinition builtIn)
    {
        var changed = false;
        if (string.IsNullOrWhiteSpace(value.Name))
        {
            value.Name = builtIn.Name;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(value.DisplayText))
        {
            value.DisplayText = builtIn.DisplayText;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(value.Memo))
        {
            value.Memo = builtIn.Memo;
            changed = true;
        }

        if (value.Color.ToArgb() == Color.Transparent.ToArgb()
            && builtIn.Color.ToArgb() != Color.Transparent.ToArgb())
        {
            value.Color = builtIn.Color;
            changed = true;
        }

        return changed;
    }
}
