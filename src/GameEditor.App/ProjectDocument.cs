namespace GameEditor;

public sealed class ProjectDocument
{
    public ProjectDocument(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "NewProject" : name.Trim();
    }

    public string Name { get; set; }

    public List<ProjectMapItem> Maps { get; } = [];
}

public sealed class ProjectMapItem
{
    public ProjectMapItem(MapEditorDocument document)
    {
        Document = document;
    }

    public MapEditorDocument Document { get; }

    public string Name => Document.Name;
}
