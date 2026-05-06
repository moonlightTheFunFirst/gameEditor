namespace GameEditor;

public sealed class AttributeDefinition
{
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
}
