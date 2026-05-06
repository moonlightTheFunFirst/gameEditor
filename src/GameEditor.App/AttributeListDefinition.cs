namespace GameEditor;

public sealed class AttributeListDefinition
{
    public AttributeListDefinition()
    {
    }

    public AttributeListDefinition(string id, string name, List<AttributeDefinition> values)
    {
        Id = id;
        Name = name;
        Values = values;
        EnsureBuiltInAttributes();
    }

    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public List<AttributeDefinition> Values { get; set; } = [];

    public bool EnsureBuiltInAttributes()
    {
        return AttributeDefinition.EnsureBuiltInDefaults(Values);
    }

    public AttributeDefinition? FindValue(int value)
    {
        return Values.FirstOrDefault(item => item.Value == value);
    }

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Name) ? Id : Name;
    }
}
