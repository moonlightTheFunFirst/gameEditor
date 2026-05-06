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
    private readonly List<AttributeChange> pendingAttributeChanges = [];
    private MapEditTool pendingStrokeTool;
    private TileSetKind? pendingStrokeLayerKind;
    private IReadOnlyList<TileSelectionCell> selectedTileSelection = [];
    private Point? stampPreviewCell;
    private readonly List<MapStampCell> mapStampCells = [];
    private Size mapStampSize = new(1, 1);
    private Rectangle? mapStampSourceRange;
    private bool mapStampSelectionActive;
    private Point mapStampSelectionStart;
    private Point mapStampSelectionCurrent;
    private TileSetKind? mapStampLayerKind;

    public MapViewport()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(36, 38, 42);
        DoubleBuffered = true;
        ResizeRedraw = true;
        AutoScroll = true;
        TabStop = true;
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

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<int> SelectedAttributeValues { get; set; } = [];

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool AttributeMode { get; set; }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<TileSelectionCell> SelectedTileSelection
    {
        get => selectedTileSelection;
        set
        {
            selectedTileSelection = value.ToArray();
            InvalidateStampPreview();
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
        if (AttributeMode)
        {
            DrawAttributeOverlay(e.Graphics);
        }

        DrawGrid(e.Graphics);
        DrawMapStampSourceSelection(e.Graphics);
        DrawStampPreview(e.Graphics);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        activeMouseButton = e.Button;
        lastEditedCell = null;
        if (TryBeginMapStampSelection(e.Location, e.Button))
        {
            return;
        }

        UpdateStampPreview(e.Location, ResolveTool(e.Button));
        BeginStroke(ResolveTool(e.Button));
        ApplyToolAt(e.Location, e.Button, isDrag: false);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var previewTool = activeMouseButton == MouseButtons.None
            ? EditTool
            : ResolveTool(activeMouseButton);
        UpdateStampPreview(e.Location, previewTool);

        if (mapStampSelectionActive)
        {
            UpdateMapStampSelection(e.Location);
            return;
        }

        if (activeMouseButton == MouseButtons.None || (e.Button & activeMouseButton) == 0)
        {
            return;
        }

        ApplyToolAt(e.Location, activeMouseButton, isDrag: true);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (mapStampSelectionActive && e.Button == activeMouseButton)
        {
            CommitMapStampSelection();
            mapStampSelectionActive = false;
            activeMouseButton = MouseButtons.None;
            lastEditedCell = null;
            return;
        }

        if (e.Button == activeMouseButton)
        {
            CommitStroke();
            activeMouseButton = MouseButtons.None;
            lastEditedCell = null;
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        InvalidateStampPreview();
        stampPreviewCell = null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode != Keys.Escape || mapStampCells.Count == 0)
        {
            return;
        }

        mapStampCells.Clear();
        mapStampSize = new Size(1, 1);
        mapStampSourceRange = null;
        Invalidate();
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        if (!AttributeMode || document is null || selectedTileSet is null || e.Button != MouseButtons.Left)
        {
            return;
        }

        var cell = GetCellFromLocation(e.Location);
        if (!document.IsInside(cell.X, cell.Y))
        {
            return;
        }

        var current = document.GetTile(selectedTileSet.Kind, cell.X, cell.Y);
        if (current.IsEmpty)
        {
            return;
        }

        using var dialog = new AttributeSetEditorDialog(document.ActiveAttributeList, current.AttributeValues);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var change = document.SetAttributesWithChange(selectedTileSet.Kind, cell.X, cell.Y, dialog.SelectedValues);
        if (change is null)
        {
            return;
        }

        Invalidate(GetInvalidationRectangle(cell.X, cell.Y));
        EditCommandCommitted?.Invoke(
            this,
            new AttributeEditCommand(GetCommandName(MapEditTool.Attribute), selectedTileSet.Kind, [change.Value]));
        EditApplied?.Invoke(this, new MapEditAppliedEventArgs(MapEditTool.Attribute, selectedTileSet.Kind, cell, 1));
    }

    private void ApplyToolAt(Point location, MouseButtons button, bool isDrag)
    {
        if (document is null)
        {
            return;
        }

        var tool = ResolveTool(button);
        var cell = GetEffectiveCellFromLocation(location, tool);

        if (!document.IsInside(cell.X, cell.Y) || (isDrag && lastEditedCell == cell))
        {
            return;
        }

        if (isDrag && tool != MapEditTool.Pen && tool != MapEditTool.Eraser && tool != MapEditTool.Attribute)
        {
            return;
        }

        if (isDrag && lastEditedCell is { } previousCell && previousCell != cell)
        {
            var cells = IsStampSnapActive(tool)
                ? EnumerateStampLine(previousCell, cell)
                : EnumerateLine(previousCell, cell);
            foreach (var lineCell in cells.Skip(1))
            {
                if (tool == MapEditTool.Pen)
                {
                    ApplyPen(lineCell);
                }
                else if (tool == MapEditTool.Eraser)
                {
                    ApplyEraser(lineCell);
                }
                else if (tool == MapEditTool.Attribute)
                {
                    if (AttributeMode)
                    {
                        ApplyAttribute(lineCell);
                    }
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
            case MapEditTool.Attribute:
                if (AttributeMode)
                {
                    ApplyAttribute(cell);
                }

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

        var placement = new TilePlacement(
            selectedTileSet.Index,
            selectedTileId,
            TilePlacement.FormatAttributeValues(selectedTileSet.GetDefaultAttributes(selectedTileId)));

        if (mapStampCells.Count > 0)
        {
            ApplyMapStamp(cell);
            return;
        }

        if (selectedTileSelection.Count > 1)
        {
            ApplyTileSelection(cell);
            return;
        }

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

    private void ApplyTileSelection(Point cell)
    {
        if (document is null || selectedTileSet is null)
        {
            return;
        }

        var changes = new List<TileChange>();
        var invalidation = Rectangle.Empty;
        foreach (var selectionCell in selectedTileSelection)
        {
            var x = cell.X + selectionCell.OffsetX;
            var y = cell.Y + selectionCell.OffsetY;
            if (!document.IsInside(x, y))
            {
                continue;
            }

            var placement = new TilePlacement(
                selectedTileSet.Index,
                selectionCell.TileId,
                TilePlacement.FormatAttributeValues(selectedTileSet.GetDefaultAttributes(selectionCell.TileId)));
            if (document.SetTileWithChange(selectedTileSet.Kind, x, y, placement) is not { } change)
            {
                continue;
            }

            changes.Add(change);
            var tileInvalidation = GetInvalidationRectangle(x, y);
            invalidation = invalidation.IsEmpty ? tileInvalidation : Rectangle.Union(invalidation, tileInvalidation);
        }

        lastEditedCell = cell;
        if (changes.Count == 0)
        {
            return;
        }

        foreach (var change in changes)
        {
            AddStrokeChange(change);
        }

        Invalidate(invalidation);
        EditApplied?.Invoke(this, new MapEditAppliedEventArgs(MapEditTool.Pen, selectedTileSet.Kind, cell, changes.Count));
    }

    private void ApplyMapStamp(Point cell)
    {
        if (document is null || mapStampLayerKind is not { } layerKind)
        {
            return;
        }

        var changes = new List<TileChange>();
        var invalidation = Rectangle.Empty;
        foreach (var stampCell in mapStampCells)
        {
            var x = cell.X + stampCell.OffsetX;
            var y = cell.Y + stampCell.OffsetY;
            if (!document.IsInside(x, y))
            {
                continue;
            }

            if (document.SetTileWithChange(layerKind, x, y, stampCell.Placement) is not { } change)
            {
                continue;
            }

            changes.Add(change);
            var tileInvalidation = GetInvalidationRectangle(x, y);
            invalidation = invalidation.IsEmpty ? tileInvalidation : Rectangle.Union(invalidation, tileInvalidation);
        }

        lastEditedCell = cell;
        if (changes.Count == 0)
        {
            return;
        }

        foreach (var change in changes)
        {
            pendingStrokeLayerKind = layerKind;
            AddStrokeChange(change);
        }

        Invalidate(invalidation);
        EditApplied?.Invoke(this, new MapEditAppliedEventArgs(MapEditTool.Pen, layerKind, cell, changes.Count));
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

        var placement = new TilePlacement(
            selectedTileSet.Index,
            selectedTileId,
            TilePlacement.FormatAttributeValues(selectedTileSet.GetDefaultAttributes(selectedTileId)));
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

    private void ApplyAttribute(Point cell)
    {
        if (!AttributeMode || document is null || selectedTileSet is null)
        {
            return;
        }

        var change = document.SetAttributesWithChange(selectedTileSet.Kind, cell.X, cell.Y, SelectedAttributeValues);
        if (change is null)
        {
            lastEditedCell = cell;
            return;
        }

        AddAttributeStrokeChange(change.Value);
        lastEditedCell = cell;
        Invalidate(GetInvalidationRectangle(cell.X, cell.Y));
        EditApplied?.Invoke(this, new MapEditAppliedEventArgs(MapEditTool.Attribute, selectedTileSet.Kind, cell, 1));
    }

    private void BeginStroke(MapEditTool tool)
    {
        pendingStrokeChanges.Clear();
        pendingAttributeChanges.Clear();
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

    private void AddAttributeStrokeChange(AttributeChange change)
    {
        if (pendingStrokeLayerKind is null)
        {
            pendingStrokeLayerKind = selectedTileSet?.Kind;
        }

        pendingAttributeChanges.Add(change);
    }

    private void CommitStroke()
    {
        if (pendingStrokeLayerKind is not { } layerKind)
        {
            pendingStrokeChanges.Clear();
            pendingAttributeChanges.Clear();
            return;
        }

        if (pendingStrokeChanges.Count > 0)
        {
            EditCommandCommitted?.Invoke(
                this,
                new TileEditCommand(GetCommandName(pendingStrokeTool), layerKind, pendingStrokeChanges.ToArray()));
        }

        if (pendingAttributeChanges.Count > 0)
        {
            EditCommandCommitted?.Invoke(
                this,
                new AttributeEditCommand(GetCommandName(pendingStrokeTool), layerKind, pendingAttributeChanges.ToArray()));
        }

        pendingStrokeChanges.Clear();
        pendingAttributeChanges.Clear();
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

    private Point GetEffectiveCellFromLocation(Point location, MapEditTool tool)
    {
        var cell = GetCellFromLocation(location);
        return IsStampSnapActive(tool) ? SnapCellToStamp(cell) : cell;
    }

    private bool IsStampSnapActive(MapEditTool tool)
    {
        return tool == MapEditTool.Pen
            && HasActiveStamp()
            && (ModifierKeys & Keys.Control) == Keys.Control;
    }

    private bool IsStampPreviewActive(MapEditTool tool)
    {
        return tool == MapEditTool.Pen;
    }

    private bool HasActiveStamp()
    {
        return mapStampCells.Count > 0 || selectedTileSelection.Count > 1;
    }

    private Point SnapCellToStamp(Point cell)
    {
        var stampSize = GetStampSize();
        return new Point(
            SnapCoordinate(cell.X, stampSize.Width),
            SnapCoordinate(cell.Y, stampSize.Height));
    }

    private static int SnapCoordinate(int value, int step)
    {
        if (step <= 1)
        {
            return value;
        }

        return value < 0
            ? ((value - step + 1) / step) * step
            : (value / step) * step;
    }

    private Size GetStampSize()
    {
        if (mapStampCells.Count > 0)
        {
            return mapStampSize;
        }

        if (selectedTileSelection.Count == 0)
        {
            return new Size(1, 1);
        }

        return new Size(
            selectedTileSelection.Max(cell => cell.OffsetX) + 1,
            selectedTileSelection.Max(cell => cell.OffsetY) + 1);
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
            MapEditTool.Attribute => "Attribute",
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

    private bool TryBeginMapStampSelection(Point location, MouseButtons button)
    {
        if (document is null || selectedTileSet is null || button != MouseButtons.Left || (ModifierKeys & Keys.Shift) != Keys.Shift)
        {
            return false;
        }

        var cell = GetCellFromLocation(location);
        if (!document.IsInside(cell.X, cell.Y))
        {
            return false;
        }

        mapStampSelectionActive = true;
        mapStampSelectionStart = cell;
        mapStampSelectionCurrent = cell;
        mapStampLayerKind = selectedTileSet.Kind;
        Invalidate(GetMapCellRangeInvalidationRectangle(mapStampSelectionStart, mapStampSelectionCurrent));
        return true;
    }

    private void UpdateMapStampSelection(Point location)
    {
        if (document is null)
        {
            return;
        }

        var cell = GetCellFromLocation(location);
        cell = new Point(
            Math.Clamp(cell.X, 0, document.Width - 1),
            Math.Clamp(cell.Y, 0, document.Height - 1));

        if (cell == mapStampSelectionCurrent)
        {
            return;
        }

        var previousRectangle = GetMapCellRangeInvalidationRectangle(mapStampSelectionStart, mapStampSelectionCurrent);
        mapStampSelectionCurrent = cell;
        Invalidate(previousRectangle);
        Invalidate(GetMapCellRangeInvalidationRectangle(mapStampSelectionStart, mapStampSelectionCurrent));
    }

    private void CommitMapStampSelection()
    {
        if (document is null || mapStampLayerKind is not { } layerKind)
        {
            return;
        }

        var range = GetCellRange(mapStampSelectionStart, mapStampSelectionCurrent);
        mapStampCells.Clear();
        mapStampSize = new Size(range.Width, range.Height);
        mapStampSourceRange = range;

        for (var y = range.Top; y < range.Bottom; y++)
        {
            for (var x = range.Left; x < range.Right; x++)
            {
                var placement = document.GetTile(layerKind, x, y);
                if (placement.IsEmpty)
                {
                    continue;
                }

                mapStampCells.Add(new MapStampCell(x - range.Left, y - range.Top, placement));
            }
        }

        Invalidate(GetMapCellRangeInvalidationRectangle(mapStampSelectionStart, mapStampSelectionCurrent));
        InvalidateStampPreview();
    }

    private IEnumerable<Point> EnumerateStampLine(Point start, Point end)
    {
        var stampSize = GetStampSize();
        var startGrid = new Point(start.X / stampSize.Width, start.Y / stampSize.Height);
        var endGrid = new Point(end.X / stampSize.Width, end.Y / stampSize.Height);

        foreach (var gridCell in EnumerateLine(startGrid, endGrid))
        {
            yield return new Point(gridCell.X * stampSize.Width, gridCell.Y * stampSize.Height);
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

    private void DrawAttributeOverlay(Graphics graphics)
    {
        if (document is null || document.ActiveAttributeList is not { } list)
        {
            return;
        }

        for (var y = 0; y < document.Height; y++)
        {
            for (var x = 0; x < document.Width; x++)
            {
                DrawAttributeCell(graphics, TileSetKind.Base, x, y, list);
                DrawAttributeCell(graphics, TileSetKind.Advanced, x, y, list);
            }
        }
    }

    private void DrawAttributeCell(Graphics graphics, TileSetKind kind, int x, int y, AttributeListDefinition list)
    {
        if (document is null)
        {
            return;
        }

        var placement = document.GetTile(kind, x, y);
        var attributeValues = placement.AttributeValues;
        if (attributeValues.Count == 0)
        {
            return;
        }

        using (var dimBrush = new SolidBrush(Color.FromArgb(92, 0, 0, 0)))
        {
            graphics.FillRectangle(
                dimBrush,
                x * document.TileSize,
                y * document.TileSize,
                document.TileSize,
                document.TileSize);
        }

        DrawAttributeLabel(graphics, GetAttributeAbbreviation(attributeValues, list), new Rectangle(
            x * document.TileSize,
            y * document.TileSize,
            document.TileSize,
            document.TileSize));
    }

    private void DrawAttributeLabel(Graphics graphics, string label, Rectangle destination)
    {
        using var font = new Font(Font.FontFamily, Math.Max(6f, Math.Min(8f, destination.Height / 4f)), FontStyle.Bold);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        var rect = destination;
        rect.Inflate(-2, -2);
        using var shadowBrush = new SolidBrush(Color.FromArgb(220, 0, 0, 0));
        using var textBrush = new SolidBrush(Color.White);
        var shadow = rect;
        shadow.Offset(1, 1);
        graphics.DrawString(label, font, shadowBrush, shadow, format);
        graphics.DrawString(label, font, textBrush, rect, format);
    }

    private static string GetAttributeAbbreviation(IReadOnlyList<int> values, AttributeListDefinition list)
    {
        return string.Join("", values.Select(value =>
            list.FindValue(value) is { } definition
                ? definition.GetDisplayText()
                : value.ToString()));
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

    private void DrawStampPreview(Graphics graphics)
    {
        if (document is null || stampPreviewCell is not { } cell || !IsStampPreviewActive(EditTool))
        {
            return;
        }

        var stampSize = GetStampSize();
        var rectangle = new Rectangle(
            cell.X * document.TileSize,
            cell.Y * document.TileSize,
            stampSize.Width * document.TileSize,
            stampSize.Height * document.TileSize);

        using var brush = new SolidBrush(Color.FromArgb(28, 255, 224, 64));
        using var pen = new Pen(Color.FromArgb(255, 255, 224, 64), 2f);
        graphics.FillRectangle(brush, rectangle);
        graphics.DrawRectangle(pen, rectangle.X, rectangle.Y, rectangle.Width - 1, rectangle.Height - 1);
    }

    private void DrawMapStampSourceSelection(Graphics graphics)
    {
        if (document is null)
        {
            return;
        }

        Rectangle range;
        if (mapStampSelectionActive)
        {
            range = GetCellRange(mapStampSelectionStart, mapStampSelectionCurrent);
        }
        else if (mapStampSourceRange is { } sourceRange)
        {
            range = sourceRange;
        }
        else
        {
            return;
        }

        var rectangle = new Rectangle(
            range.X * document.TileSize,
            range.Y * document.TileSize,
            range.Width * document.TileSize,
            range.Height * document.TileSize);

        using var brush = new SolidBrush(Color.FromArgb(20, 255, 224, 64));
        using var pen = new Pen(Color.FromArgb(255, 255, 224, 64), 2f);
        graphics.FillRectangle(brush, rectangle);
        graphics.DrawRectangle(pen, rectangle.X, rectangle.Y, rectangle.Width - 1, rectangle.Height - 1);
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

    private Rectangle GetMapCellRangeInvalidationRectangle(Point start, Point end)
    {
        if (document is null)
        {
            return ClientRectangle;
        }

        var range = GetCellRange(start, end);
        var rectangle = new Rectangle(
            range.X * document.TileSize + AutoScrollPosition.X,
            range.Y * document.TileSize + AutoScrollPosition.Y,
            range.Width * document.TileSize + 1,
            range.Height * document.TileSize + 1);
        rectangle.Inflate(3, 3);
        return rectangle;
    }

    private static Rectangle GetCellRange(Point start, Point end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var right = Math.Max(start.X, end.X);
        var bottom = Math.Max(start.Y, end.Y);
        return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    private void UpdateStampPreview(Point location, MapEditTool tool)
    {
        if (document is null || !IsStampPreviewActive(tool))
        {
            InvalidateStampPreview();
            stampPreviewCell = null;
            return;
        }

        var cell = GetCellFromLocation(location);
        if (IsStampSnapActive(tool))
        {
            cell = SnapCellToStamp(cell);
        }

        if (!document.IsInside(cell.X, cell.Y))
        {
            InvalidateStampPreview();
            stampPreviewCell = null;
            return;
        }

        if (stampPreviewCell == cell)
        {
            return;
        }

        InvalidateStampPreview();
        stampPreviewCell = cell;
        InvalidateStampPreview();
    }

    private void InvalidateStampPreview()
    {
        if (stampPreviewCell is not { } cell)
        {
            return;
        }

        Invalidate(GetStampPreviewInvalidationRectangle(cell));
    }

    private Rectangle GetStampPreviewInvalidationRectangle(Point cell)
    {
        if (document is null)
        {
            return ClientRectangle;
        }

        var stampSize = GetStampSize();
        var rectangle = new Rectangle(
            cell.X * document.TileSize + AutoScrollPosition.X,
            cell.Y * document.TileSize + AutoScrollPosition.Y,
            stampSize.Width * document.TileSize + 1,
            stampSize.Height * document.TileSize + 1);
        rectangle.Inflate(3, 3);
        return rectangle;
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
