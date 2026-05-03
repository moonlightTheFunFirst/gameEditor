namespace GameEditor;

public sealed class ProjectLoadResult
{
    public ProjectLoadResult(ProjectDocument project)
    {
        Project = project;
    }

    public ProjectDocument Project { get; }
}
