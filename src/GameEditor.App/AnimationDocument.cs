namespace GameEditor;

public sealed class AnimationDocument
{
    public AnimationDocument(AnimationClip clip)
    {
        Clip = clip;
    }

    public string? FilePath { get; set; }

    public bool IsDirty { get; set; } = true;

    public AnimationClip Clip { get; }
}

public sealed class AnimationClip
{
    public string Name { get; set; } = "NewAnimation";

    public int Fps { get; set; } = 8;

    public bool Loop { get; set; } = true;

    public List<AnimationFrameKey> Frames { get; } = [];

    public List<AnimationEventKey> Events { get; } = [];

    public int FrameDurationMs => Math.Max(1, (int)Math.Round(1000.0 / Math.Clamp(Fps, 1, 60)));

    public int DurationMs => Math.Max(FrameDurationMs, Frames.Count * FrameDurationMs);
}

public sealed class AnimationFrameKey
{
    public int TimeMs { get; set; }

    public string TileSetId { get; set; } = "";

    public int TileId { get; set; }
}

public sealed class AnimationEventKey
{
    public int TimeMs { get; set; }

    public string Name { get; set; } = "";

    public string Memo { get; set; } = "";
}
