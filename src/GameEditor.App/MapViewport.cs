namespace GameEditor;

public sealed class MapViewport : ScrollableControl
{
    private MapDocument? document;
    private IReadOnlyList<TileSet> tileSets = Array.Empty<TileSet>();
    private TileSet? selectedTileSet;
    private int selectedTileId;
    private MouseButtons activeMouseButton;
    private Point? lastEditedCell;
    private readonly List<TileChange> pendingStrokeChanges = [];
    private MapEditTool pendingStrokeTool;
    private TileSetKind? pendingStrokeLayerKind;

    public MapViewport()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(36, 38, 42);
        DoubleBuffered = true;
        ResizeRedraw = true;
        AutoScroll = true;
    }

    public event EventHandler<MapEditAppliedEventArgs>? EditApplied;

    public event EventHandler<IMapEditCommand>? EditCommandCommitted;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public MapEditTool EditTool { get; set; } = MapEditTool.Pen;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public MapEditTool SecondaryEditTool { get; set; } = MapEditTool.Eraser;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public MapDocument? Document
    {
        get => document;
        set
        {
            document = value;
            UpdateScrollSize();
            Invalidate();
        }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<TileSet> TileSets
    {
        get => tileSets;
        set
        {
            tileSets = value;
            Invalidate();
        }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public TileSet? SelectedTileSet
    {
        get => selectedTileSet;
        set
        {
            selectedTileSet = value;
            Invalidate();
        }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SelectedTileId
    {
        get => selectedTileId;
        set
        {
            selectedTileId = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.Clear(BackColor);
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

        if (document is null)
        {
            DrawEmptyState(e.Graphics);
            return;
        }

        e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
        DrawPlacedTiles(e.Graphics);
        DrawGrid(e.Graphics);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        activeMouseButton = e.Button;
        lastEditedCell = null;
        BeginStroke(ResolveTool(e.Button));
        ApplyToolAt(e.Location, e.Button, isDrag: false);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (activeMouseButton == MouseButtons.None || (e.Button & activeMouseButton) == 0)
        {
            return;
        }

        ApplyToolAt(e.Location, activeMouseButton, isDrag: true);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == activeMouseButton)
        {
            CommitStroke();
            activeMouseButton = MouseButtons.None;
            lastEditedCell = null;
        }
    }

    private void ApplyToolAt(Point location, MouseButtons button, bool isDrag)
    {
        if (document is null)
        {
            return;
        }

        var cell = GetCellFromLocation(location);

        if (!document.IsInside(cell.X, cell.Y) || (isDrag && lastEditedCell == cell))
        {
            return;
        }

        var tool = ResolveTool(button);
        if (isDrag && tool != MapEditTool.Pen && tool != MapEditTool.Eraser)
        {
            return;
        }

        if (isDrag && lastEditedCell is { } previousCell && previousCell != cell)
        {
            foreach (var lineCell in EnumerateLine(previousCell, cell).Skip(1))
            {
                if (tool == MapEditTool.Pen)
                {
                    ApplyPen(lineCell);
                }
                else if (tool == MapEditTool.Eraser)
                {
                    ApplyEraser(lineCell);
                }
            }

            return;
        }

        switch (tool)
        {
            case MapEditTool.Pen:
                ApplyPen(cell);
                break;
            case MapEditTool.Eraser:
                ApplyEraser(cell);
                break;
            case MapEditTool.Fill:
                if (!isDrag)
                {
                    ApplyFill(cell);
                }

                break;
            case MapEditTool.Select:
                break;
        }
    }

    private void UpdateScrollSize()
    {
        AutoScrollMinSize = document?.PixelSize ?? Size.Empty;
    }

    private void DrawPlacedTiles(Graphics graphics)
    {
        if (document is null)
        {
            return;
        }

        DrawLayer(graphics, TileSetKind.Base);
        DrawLayer(graphics, TileSetKind.Advanced);
    }

    private void ApplyPen(Point cell)
    {
        if (document is null || selectedTileSet is null || selectedTileId < 0)
        {
            return;
        }

        var placement = new TilePlacement(selectedTileSet.Index, selectedTileId);
        var current = document.GetTile(selectedTileSet.Kind, cell.X, cell.Y);
        if (current == placement)
        {
            lastEditedCell = cell;
            return;
        }

        var change = document.SetTileWithChange(selectedTileSet.Kind, cell.X, cell.Y, placement);
        if (change is null)
        {
            lastEditedCell = cell;
            return;
        }

        AddStrokeChange(change.Value);
        lastEditedCell = cell;
        Invalidate(GetInvalidationRectangle(cell.X, cell.Y));
        EditApplied?.Invoke(this, new MapEditAppliedEventArgs(MapEditTool.Pen, selectedTileSet.Kind, cell, 1));
    }

    private void ApplyEraser(Point cell)
    {
        if (document is null || selectedTileSet is null)
        {
            return;
        }

        var current = document.GetTile(selectedTileSet.Kind, cell.X, cell.Y);
        if (current.IsEmpty)
        {
            lastEditedCell = cell;
            return;
        }

        var change = document.SetTileWithChange(selectedTileSet.Kind, cell.X, cell.Y, TilePlacement.Empty);
        if (change is null)
        {
            lastEditedCell = cell;
            return;
        }

        AddStrokeChange(change.Value);
        lastEditedCell = cell;
        Invalidate(GetInvalidationRectangle(cell.X, cell.Y));
        EditApplied?.Invoke(this, new MapEditAppliedEventArgs(MapEditTool.Eraser, selectedTileSet.Kind, cell, 1));
    }

    private void ApplyFill(Point cell)
    {
        if (document is null || selectedTileSet is null || selectedTileId < 0)
        {
            return;
        }

        var placement = new TilePlacement(selectedTileSet.Index, selectedTileId);
        var changes = document.FloodFillWithChanges(selectedTileSet.Kind, cell.X, cell.Y, placement);
        if (changes.Count == 0)
        {
            lastEditedCell = cell;
            return;
        }

        lastEditedCell = cell;
        Invalidate();
        EditCommandCommitted?.Invoke(
            this,
            new TileEditCommand(GetCommandName(MapEditTool.Fill), selectedTileSet.Kind, changes));
        EditApplied?.Invoke(this, new MapEditAppliedEventArgs(MapEditTool.Fill, selectedTileSet.Kind, cell, changes.Count));
    }

    private void BeginStroke(MapEditTool tool)
    {
        pendingStrokeChanges.Clear();
        pendingStrokeTool = tool;
        pendingStrokeLayerKind = selectedTileSet?.Kind;
    }

    private void AddStrokeChange(TileChange change)
    {
        if (pendingStrokeLayerKind is null)
        {
            pendingStrokeLayerKind = selectedTileSet?.Kind;
        }

        pendingStrokeChanges.Add(change);
    }

    private void CommitStroke()
    {
        if (pendingStrokeLayerKind is not { } layerKind || pendingStrokeChanges.Count == 0)
        {
            pendingStrokeChanges.Clear();
            return;
        }

        EditCommandCommitted?.Invoke(
            this,
            new TileEditCommand(GetCommandName(pendingStrokeTool), layerKind, pendingStrokeChanges.ToArray()));
        pendingStrokeChanges.Clear();
    }

    private Point GetCellFromLocation(Point location)
    {
        if (document is null)
        {
            return new Point(-1, -1);
        }

        var mapX = location.X - AutoScrollPosition.X;
        var mapY = location.Y - AutoScrollPosition.Y;
        return new Point(mapX / document.TileSize, mapY / document.TileSize);
    }

    private MapEditTool ResolveTool(MouseButtons button)
    {
        return button == MouseButtons.Right ? SecondaryEditTool : EditTool;
    }

    private static string GetCommandName(MapEditTool tool)
    {
        return tool switch
        {
            MapEditTool.Pen => "ペン",
            MapEditTool.Fill => "塗りつぶし",
            MapEditTool.Eraser => "消しゴム",
            MapEditTool.Select => "選択",
            _ => tool.ToString()
        };
    }

    private static IEnumerable<Point> EnumerateLine(Point start, Point end)
    {
        var x0 = start.X;
        var y0 = start.Y;
        var x1 = end.X;
        var y1 = end.Y;
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;

        while (true)
        {
            yield return new Point(x0, y0);

            if (x0 == x1 && y0 == y1)
            {
                yield break;
            }

            var doubledError = 2 * error;
            if (doubledError >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (doubledError <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private void DrawLayer(Graphics graphics, TileSetKind kind)
    {
        if (document is null)
        {
            return;
        }

        for (var y = 0; y < document.Height; y++)
        {
            for (var x = 0; x < document.Width; x++)
            {
                var placement = document.GetTile(kind, x, y);
                if (placement.IsEmpty || placement.TileSetIndex < 0 || placement.TileSetIndex >= tileSets.Count)
                {
                    continue;
                }

                var tileSet = tileSets[placement.TileSetIndex];
                if (tileSet.Kind != kind)
                {
                    continue;
                }

                var destination = new Rectangle(
                    x * document.TileSize,
                    y * document.TileSize,
                    document.TileSize,
                    document.TileSize);
                tileSet.DrawTile(graphics, placement.TileId, destination);
            }
        }
    }

    private void DrawGrid(Graphics graphics)
    {
        if (document is null)
        {
            return;
        }

        using var gridPen = new Pen(Color.FromArgb(78, 84, 94));
        var width = document.Width * document.TileSize;
        var height = document.Height * document.TileSize;

        for (var x = 0; x <= width; x += document.TileSize)
        {
            graphics.DrawLine(gridPen, x, 0, x, height);
        }

        for (var y = 0; y <= height; y += document.TileSize)
        {
            graphics.DrawLine(gridPen, 0, y, width, y);
        }
    }

    private Rectangle GetInvalidationRectangle(int tileX, int tileY)
    {
        if (document is null)
        {
            return ClientRectangle;
        }

        return new Rectangle(
            tileX * document.TileSize + AutoScrollPosition.X,
            tileY * document.TileSize + AutoScrollPosition.Y,
            document.TileSize + 1,
            document.TileSize + 1);
    }

    private void DrawEmptyState(Graphics graphics)
    {
        const string title = "Map Editor";
        const string subtitle = "マップデータはまだ読み込まれていません";

        using var titleFont = new Font(Font.FontFamily, 18, FontStyle.Bold);
        using var subtitleFont = new Font(Font.FontFamily, 10, FontStyle.Regular);
        using var titleBrush = new SolidBrush(Color.FromArgb(235, 238, 242));
        using var subtitleBrush = new SolidBrush(Color.FromArgb(168, 174, 184));

        var titleSize = graphics.MeasureString(title, titleFont);
        var subtitleSize = graphics.MeasureString(subtitle, subtitleFont);
        var centerX = Width / 2f;
        var centerY = Height / 2f;

        graphics.DrawString(title, titleFont, titleBrush, centerX - titleSize.Width / 2f, centerY - 32);
        graphics.DrawString(subtitle, subtitleFont, subtitleBrush, centerX - subtitleSize.Width / 2f, centerY + 4);
    }
}
