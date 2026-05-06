namespace GameEditor;

public sealed class MapDocument
{
    private readonly TilePlacement[,] baseTiles;
    private readonly TilePlacement[,] advancedTiles;

    public MapDocument(int width, int height, int tileSize)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (tileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize));
        }

        Width = width;
        Height = height;
        TileSize = tileSize;
        baseTiles = new TilePlacement[width, height];
        advancedTiles = new TilePlacement[width, height];
        AttributeLists =
        [
            new AttributeListDefinition(
                "default",
                "Default",
                [
                    new AttributeDefinition(0, "None", Color.Transparent),
                    new AttributeDefinition(1, "Blocked", Color.FromArgb(160, 220, 50, 50)),
                    new AttributeDefinition(2, "Event", Color.FromArgb(160, 80, 160, 255))
                ])
        ];
        ActiveAttributeListId = "default";

        Clear();
    }

    public int Width { get; }

    public int Height { get; }

    public int TileSize { get; }

    public Size PixelSize => new(Width * TileSize, Height * TileSize);

    public List<AttributeListDefinition> AttributeLists { get; set; }

    public string? ActiveAttributeListId { get; set; }

    public AttributeListDefinition? ActiveAttributeList => AttributeLists.FirstOrDefault(list => list.Id == ActiveAttributeListId)
        ?? AttributeLists.FirstOrDefault();

    public TilePlacement GetTile(TileSetKind kind, int x, int y)
    {
        if (!IsInside(x, y))
        {
            return TilePlacement.Empty;
        }

        return GetLayer(kind)[x, y];
    }

    public IEnumerable<(int X, int Y, TilePlacement Placement)> EnumerateTiles(TileSetKind kind)
    {
        var layer = GetLayer(kind);

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var placement = layer[x, y];
                if (!placement.IsEmpty)
                {
                    yield return (x, y, placement);
                }
            }
        }
    }

    public void SetTile(TileSetKind kind, int x, int y, TilePlacement placement)
    {
        if (!IsInside(x, y))
        {
            return;
        }

        GetLayer(kind)[x, y] = placement;
    }

    public TileChange? SetTileWithChange(TileSetKind kind, int x, int y, TilePlacement placement)
    {
        if (!IsInside(x, y))
        {
            return null;
        }

        var layer = GetLayer(kind);
        var before = layer[x, y];
        if (before == placement)
        {
            return null;
        }

        layer[x, y] = placement;
        return new TileChange(x, y, before, placement);
    }

    public AttributeChange? SetAttributesWithChange(TileSetKind kind, int x, int y, IEnumerable<int> attributeValues)
    {
        if (!IsInside(x, y))
        {
            return null;
        }

        var layer = GetLayer(kind);
        var before = layer[x, y];
        if (before.IsEmpty)
        {
            return null;
        }

        var after = before.WithAttributes(attributeValues);
        if (before == after)
        {
            return null;
        }

        layer[x, y] = after;
        return new AttributeChange(x, y, before, after);
    }

    public bool ContainsAttributeValue(TileSetKind kind, int attributeValue)
    {
        return EnumerateTiles(kind)
            .Any(tile => tile.Placement.AttributeValues.Contains(attributeValue));
    }

    public bool RemapAttributes(TileSetKind kind, IReadOnlyDictionary<int, int?> valueRemap)
    {
        var changed = false;
        var layer = GetLayer(kind);
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var placement = layer[x, y];
                if (placement.IsEmpty)
                {
                    continue;
                }

                var remapped = TilePlacement.RemapAttributeValuesCsv(placement.AttributeValuesCsv, valueRemap);
                if (string.Equals(placement.AttributeValuesCsv, remapped, StringComparison.Ordinal))
                {
                    continue;
                }

                layer[x, y] = placement with { AttributeValuesCsv = remapped };
                changed = true;
            }
        }

        return changed;
    }

    public bool ResetAttributesToTileDefaults(TileSetKind kind, TileSet tileSet)
    {
        var changed = false;
        var layer = GetLayer(kind);
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var placement = layer[x, y];
                if (placement.IsEmpty)
                {
                    continue;
                }

                var defaultAttributes = TilePlacement.FormatAttributeValues(tileSet.GetDefaultAttributes(placement.TileId));
                if (string.Equals(placement.AttributeValuesCsv, defaultAttributes, StringComparison.Ordinal))
                {
                    continue;
                }

                layer[x, y] = placement with { AttributeValuesCsv = defaultAttributes };
                changed = true;
            }
        }

        return changed;
    }

    public IReadOnlyList<TileChange> FloodFillWithChanges(TileSetKind kind, int x, int y, TilePlacement replacement)
    {
        if (!IsInside(x, y))
        {
            return [];
        }

        var layer = GetLayer(kind);
        var target = layer[x, y];
        if (target == replacement)
        {
            return [];
        }

        var changes = new List<TileChange>();
        var visited = new bool[Width, Height];
        var queue = new Queue<Point>();
        queue.Enqueue(new Point(x, y));
        visited[x, y] = true;

        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            if (layer[point.X, point.Y] != target)
            {
                continue;
            }

            var before = layer[point.X, point.Y];
            layer[point.X, point.Y] = replacement;
            changes.Add(new TileChange(point.X, point.Y, before, replacement));

            EnqueueIfNeeded(point.X - 1, point.Y);
            EnqueueIfNeeded(point.X + 1, point.Y);
            EnqueueIfNeeded(point.X, point.Y - 1);
            EnqueueIfNeeded(point.X, point.Y + 1);
        }

        return changes;

        void EnqueueIfNeeded(int nextX, int nextY)
        {
            if (!IsInside(nextX, nextY) || visited[nextX, nextY])
            {
                return;
            }

            visited[nextX, nextY] = true;
            queue.Enqueue(new Point(nextX, nextY));
        }
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }

    private void Clear()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                baseTiles[x, y] = TilePlacement.Empty;
                advancedTiles[x, y] = TilePlacement.Empty;
            }
        }
    }

    private TilePlacement[,] GetLayer(TileSetKind kind)
    {
        return kind == TileSetKind.Base ? baseTiles : advancedTiles;
    }
}
