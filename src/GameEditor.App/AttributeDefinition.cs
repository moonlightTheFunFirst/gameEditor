namespace GameEditor;

public sealed class AttributeDefinition
{
    public AttributeDefinition()
    {
    }

    public AttributeDefinition(int value, string name, Color color)
    {
        Value = value;
        Name = name;
        Color = color;
    }

    public int Value { get; set; }

    public string Name { get; set; } = "";

    public Color Color { get; set; } = Color.Transparent;
}

