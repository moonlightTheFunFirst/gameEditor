namespace GameEditor;

public static class AnimationSampleAssets
{
    private const int TileSize = 32;
    private const int FrameCount = 8;
    private const string SampleFileName = "sample_actor.bmp";

    public static string EnsureSampleSpriteSheet()
    {
        var directory = ResolveAnimationResourceDirectory();
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, SampleFileName);
        if (!IsSampleSpriteSheetCurrent(path))
        {
            CreateSampleSpriteSheet(path);
        }

        return path;
    }

    private static bool IsSampleSpriteSheetCurrent(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var image = Image.FromFile(path);
            return image.Width == TileSize * FrameCount && image.Height == TileSize;
        }
        catch
        {
            return false;
        }
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
            DrawSampleFrame(graphics, frame);
        }

        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Bmp);
    }

    private static void DrawSampleFrame(Graphics graphics, int frame)
    {
        var originX = frame * TileSize;
        switch (frame)
        {
            case 4:
                DrawSwordFrame(graphics, originX);
                break;
            case 5:
                DrawCastFrame(graphics, originX);
                break;
            case 6:
                DrawSparkFrame(graphics, originX);
                break;
            case 7:
                DrawCrateFrame(graphics, originX);
                break;
            default:
                DrawWalkFrame(graphics, originX, frame);
                break;
        }
    }

    private static void DrawWalkFrame(Graphics graphics, int originX, int frame)
    {
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

    private static void DrawSwordFrame(Graphics graphics, int originX)
    {
        using var outline = new SolidBrush(Color.FromArgb(42, 38, 48));
        using var skin = new SolidBrush(Color.FromArgb(248, 197, 142));
        using var hair = new SolidBrush(Color.FromArgb(76, 48, 36));
        using var boots = new SolidBrush(Color.FromArgb(62, 52, 60));
        using var red = new SolidBrush(Color.FromArgb(224, 86, 92));
        using var gold = new SolidBrush(Color.FromArgb(248, 198, 82));

        graphics.FillRectangle(outline, originX + 11, 4, 10, 9);
        graphics.FillRectangle(skin, originX + 12, 5, 8, 8);
        graphics.FillRectangle(hair, originX + 11, 4, 10, 3);
        graphics.FillRectangle(outline, originX + 9, 13, 14, 12);
        graphics.FillRectangle(red, originX + 10, 14, 12, 10);
        graphics.FillRectangle(outline, originX + 7, 15, 4, 8);
        graphics.FillRectangle(skin, originX + 8, 15, 2, 7);
        graphics.FillRectangle(outline, originX + 21, 15, 6, 4);
        graphics.FillRectangle(skin, originX + 22, 16, 4, 2);
        graphics.FillRectangle(outline, originX + 11, 23, 4, 6);
        graphics.FillRectangle(outline, originX + 17, 23, 4, 6);
        graphics.FillRectangle(boots, originX + 9, 28, 6, 3);
        graphics.FillRectangle(boots, originX + 17, 28, 6, 3);
        graphics.FillRectangle(outline, originX + 25, 11, 2, 11);
        graphics.FillRectangle(gold, originX + 27, 9, 2, 15);
    }

    private static void DrawCastFrame(Graphics graphics, int originX)
    {
        using var outline = new SolidBrush(Color.FromArgb(42, 38, 48));
        using var skin = new SolidBrush(Color.FromArgb(248, 197, 142));
        using var hair = new SolidBrush(Color.FromArgb(76, 48, 36));
        using var boots = new SolidBrush(Color.FromArgb(62, 52, 60));
        using var green = new SolidBrush(Color.FromArgb(78, 180, 126));
        using var glow = new SolidBrush(Color.FromArgb(117, 226, 185));
        using var staff = new SolidBrush(Color.FromArgb(112, 72, 46));

        graphics.FillRectangle(outline, originX + 11, 4, 10, 9);
        graphics.FillRectangle(skin, originX + 12, 5, 8, 8);
        graphics.FillRectangle(hair, originX + 11, 4, 10, 3);
        graphics.FillRectangle(outline, originX + 9, 13, 14, 12);
        graphics.FillRectangle(green, originX + 10, 14, 12, 10);
        graphics.FillRectangle(outline, originX + 21, 15, 4, 8);
        graphics.FillRectangle(skin, originX + 22, 15, 2, 7);
        graphics.FillRectangle(outline, originX + 6, 10, 3, 16);
        graphics.FillRectangle(staff, originX + 7, 11, 1, 14);
        graphics.FillRectangle(glow, originX + 4, 7, 7, 4);
        graphics.FillRectangle(glow, originX + 5, 5, 5, 8);
        graphics.FillRectangle(outline, originX + 11, 23, 4, 6);
        graphics.FillRectangle(outline, originX + 17, 23, 4, 6);
        graphics.FillRectangle(boots, originX + 9, 28, 6, 3);
        graphics.FillRectangle(boots, originX + 17, 28, 6, 3);
    }

    private static void DrawSparkFrame(Graphics graphics, int originX)
    {
        using var outline = new SolidBrush(Color.FromArgb(42, 38, 48));
        using var gold = new SolidBrush(Color.FromArgb(248, 198, 82));
        using var orange = new SolidBrush(Color.FromArgb(238, 116, 72));
        using var white = new SolidBrush(Color.FromArgb(255, 244, 180));

        graphics.FillRectangle(outline, originX + 15, 3, 2, 26);
        graphics.FillRectangle(outline, originX + 4, 14, 24, 2);
        graphics.FillRectangle(gold, originX + 15, 5, 2, 22);
        graphics.FillRectangle(gold, originX + 6, 14, 20, 2);
        graphics.FillRectangle(orange, originX + 10, 9, 12, 12);
        graphics.FillRectangle(white, originX + 13, 12, 6, 6);
        graphics.FillRectangle(gold, originX + 7, 6, 3, 3);
        graphics.FillRectangle(gold, originX + 22, 23, 3, 3);
    }

    private static void DrawCrateFrame(Graphics graphics, int originX)
    {
        using var outline = new SolidBrush(Color.FromArgb(58, 42, 32));
        using var wood = new SolidBrush(Color.FromArgb(166, 112, 66));
        using var darkWood = new SolidBrush(Color.FromArgb(112, 72, 46));
        using var metal = new SolidBrush(Color.FromArgb(186, 190, 176));

        graphics.FillRectangle(outline, originX + 5, 8, 22, 20);
        graphics.FillRectangle(wood, originX + 6, 9, 20, 18);
        graphics.FillRectangle(darkWood, originX + 7, 12, 18, 3);
        graphics.FillRectangle(darkWood, originX + 7, 21, 18, 3);
        graphics.FillRectangle(darkWood, originX + 14, 10, 4, 16);
        graphics.FillRectangle(metal, originX + 13, 16, 6, 4);
    }
}
