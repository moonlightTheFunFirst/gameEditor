namespace GameEditor;

public static class AnimationSampleAssets
{
    private const int TileSize = 32;
    private const int FrameCount = 4;
    private const string SampleFileName = "sample_actor.bmp";

    public static string EnsureSampleSpriteSheet()
    {
        var directory = ResolveAnimationResourceDirectory();
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, SampleFileName);
        if (!File.Exists(path))
        {
            CreateSampleSpriteSheet(path);
        }

        return path;
    }

    public static string SampleImagePath => Path.Combine("resources", "animations", SampleFileName).Replace('\\', '/');

    private static string ResolveAnimationResourceDirectory()
    {
        var candidates = new List<string>();
        AddResourceDirectoryCandidates(candidates, Environment.CurrentDirectory);
        AddResourceDirectoryCandidates(candidates, AppContext.BaseDirectory);

        var existing = candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path => Directory.Exists(Path.GetDirectoryName(path)))
            .OrderBy(IsBuildOutputPath)
            .FirstOrDefault();

        return existing ?? Path.Combine(AppContext.BaseDirectory, "resources", "animations");
    }

    private static void AddResourceDirectoryCandidates(List<string> candidates, string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            candidates.Add(Path.Combine(directory.FullName, "resources", "animations"));
            directory = directory.Parent;
        }
    }

    private static bool IsBuildOutputPath(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase));
    }

    private static void CreateSampleSpriteSheet(string path)
    {
        using var bitmap = new Bitmap(TileSize * FrameCount, TileSize);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Magenta);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

        for (var frame = 0; frame < FrameCount; frame++)
        {
            DrawActorFrame(graphics, frame);
        }

        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Bmp);
    }

    private static void DrawActorFrame(Graphics graphics, int frame)
    {
        var originX = frame * TileSize;
        var step = frame switch
        {
            1 => -2,
            3 => 2,
            _ => 0
        };

        using var outline = new SolidBrush(Color.FromArgb(42, 38, 48));
        using var tunic = new SolidBrush(Color.FromArgb(64, 154, 220));
        using var skin = new SolidBrush(Color.FromArgb(248, 197, 142));
        using var hair = new SolidBrush(Color.FromArgb(76, 48, 36));
        using var boots = new SolidBrush(Color.FromArgb(62, 52, 60));
        using var scarf = new SolidBrush(Color.FromArgb(230, 74, 88));

        graphics.FillRectangle(outline, originX + 11, 4, 10, 9);
        graphics.FillRectangle(skin, originX + 12, 5, 8, 8);
        graphics.FillRectangle(hair, originX + 11, 4, 10, 3);
        graphics.FillRectangle(outline, originX + 9, 13, 14, 12);
        graphics.FillRectangle(tunic, originX + 10, 14, 12, 10);
        graphics.FillRectangle(scarf, originX + 10, 13, 12, 3);
        graphics.FillRectangle(outline, originX + 7, 15 + step, 4, 8);
        graphics.FillRectangle(skin, originX + 8, 15 + step, 2, 7);
        graphics.FillRectangle(outline, originX + 21, 15 - step, 4, 8);
        graphics.FillRectangle(skin, originX + 22, 15 - step, 2, 7);
        graphics.FillRectangle(outline, originX + 11, 23, 4, 6);
        graphics.FillRectangle(outline, originX + 17, 23, 4, 6);
        graphics.FillRectangle(boots, originX + 9 + Math.Max(0, step), 28, 6, 3);
        graphics.FillRectangle(boots, originX + 17 + Math.Min(0, step), 28, 6, 3);
    }
}
