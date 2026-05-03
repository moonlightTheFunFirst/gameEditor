namespace GameEditor;

public sealed class ProjectDocument
{
    public ProjectDocument(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "NewProject" : name.Trim();
    }

    public string Name { get; set; }

    public string? FilePath { get; set; }

    public bool IsDirty { get; set; } = true;

    public List<ProjectMapItem> Maps { get; } = [];
}

public sealed class ProjectMapItem
{
    public ProjectMapItem(MapEditorDocument document, string? id = null)
    {
        Document = document;
        Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id.Trim();
    }

    public string Id { get; }

    public MapEditorDocument Document { get; }

    public string Name => Document.Name;
}
