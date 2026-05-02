namespace GameEditor;

public static class TileSetCatalog
{
    private static readonly string[] SupportedExtensions = [".bmp", ".png"];
    private static readonly Color TransparentColor = Color.Magenta;

    public static List<TileSetDefinition> Load()
    {
        var directory = ResolveTilesDirectory();
        if (directory is null)
        {
            return [];
        }

        var files = Directory
            .EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);

        var definitions = new List<TileSetDefinition>();
        foreach (var path in files)
        {
            var hasTransparency = ContainsTransparentColor(path);
            var kind = hasTransparency ? TileSetKind.Advanced : TileSetKind.Base;
            var id = Path.GetFileNameWithoutExtension(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var imagePath = Path.Combine("resources", "tiles", Path.GetFileName(path)).Replace('\\', '/');

            definitions.Add(new TileSetDefinition(
                id,
                name,
                kind,
                path,
                imagePath,
                hasTransparency ? TransparentColor : null));
        }

        return definitions;
    }

    private static bool ContainsTransparentColor(string path)
    {
        using var image = new Bitmap(path);
        var transparentArgb = TransparentColor.ToArgb();

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image.GetPixel(x, y).ToArgb() == transparentArgb)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? ResolveTilesDirectory()
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "resources", "tiles"),
            Path.Combine(Environment.CurrentDirectory, "resources", "tiles")
        };

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            candidates.Add(Path.Combine(directory.FullName, "resources", "tiles"));
            directory = directory.Parent;
        }

        return candidates.FirstOrDefault(Directory.Exists);
    }
}
