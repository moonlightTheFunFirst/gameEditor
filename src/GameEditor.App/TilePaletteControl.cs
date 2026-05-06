namespace GameEditor;

public sealed class TilePaletteControl : ScrollableControl
{
    private TileSet? tileSet;
    private int selectedTileId;
    private bool attributeMode;
    private bool priorityMode;
    private AttributeListDefinition? attributeList;
    private IReadOnlyList<int> selectedAttributeValues = [];
    private int selectedDisplayPriority;
    private Point selectionStartCell;
    private Point selectionEndCell;
    private bool rangeDragActive;
    private Point rangeDragStartCell;
    private Point rangeDragCurrentCell;
    private MouseButtons rangeDragButton;

    public TilePaletteControl()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(44, 46, 50);
        DoubleBuffered = true;
        ResizeRedraw = true;
        AutoScroll = true;
        selectedTileId = -1;
    }

    public event EventHandler? SelectedTileChanged;

    public event EventHandler<TileAttributeChangedEventArgs>? TileAttributeChanged;

    public event EventHandler<TilePriorityChangedEventArgs>? TilePriorityChanged;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public TileSet? TileSet
    {
        get => tileSet;
        set
        {
            tileSet = value;
            selectedTileId = value is null ? -1 : 0;
            selectionStartCell = Point.Empty;
            selectionEndCell = Point.Empty;
            UpdateScrollSize();
            Invalidate();
            SelectedTileChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int SelectedTileId
    {
        get => selectedTileId;
        private set
        {
            if (selectedTileId == value)
            {
                return;
            }

            selectedTileId = value;
            SetSelectionToTile(value);
            Invalidate();
            SelectedTileChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool AttributeMode
    {
        get => attributeMode;
        set
        {
            if (attributeMode == value)
            {
                return;
            }

            attributeMode = value;
            Invalidate();
        }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool PriorityMode
    {
        get => priorityMode;
        set
        {
            if (priorityMode == value)
            {
                return;
            }

            priorityMode = value;
            Invalidate();
        }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public AttributeListDefinition? AttributeList
    {
        get => attributeList;
        set
        {
            attributeList = value;
            Invalidate();
        }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<int> SelectedAttributeValues
    {
        get => selectedAttributeValues;
        set => selectedAttributeValues = value.Distinct().Order().ToArray();
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SelectedDisplayPriority
    {
        get => selectedDisplayPriority;
        set => selectedDisplayPriority = value;
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<TileSelectionCell> SelectedTileSelection => GetSelectedTileSelection();

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.Clear(BackColor);

        if (tileSet is null)
        {
            DrawEmptyState(e.Graphics);
            return;
        }

        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

        for (var tileId = 0; tileId < tileSet.TileCount; tileId++)
        {
            var destination = GetTileRectangle(tileId);
            tileSet.DrawTile(e.Graphics, tileId, destination);
            if (attributeMode)
            {
                DrawAttributeTileOverlay(e.Graphics, tileId, destination);
            }
            else if (priorityMode)
            {
                DrawPriorityTileOverlay(e.Graphics, tileId, destination);
            }
        }

        DrawSelection(e.Graphics);
        DrawRangeDrag(e.Graphics);
        DrawGrid(e.Graphics);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (tileSet is null)
        {
            return;
        }

        if (TryBeginRangeDrag(e.Location, e.Button))
        {
            return;
        }

        ApplyMouseAction(e.Location, e.Button);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (tileSet is null || e.Button == MouseButtons.None)
        {
            return;
        }

        if (rangeDragActive)
        {
            UpdateRangeDrag(e.Location);
            return;
        }

        if (!attributeMode && !priorityMode)
        {
            return;
        }

        ApplyMouseAction(e.Location, e.Button);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (!rangeDragActive || e.Button != rangeDragButton)
        {
            return;
        }

        CommitRangeDrag();
        rangeDragActive = false;
        Invalidate(GetRangeInvalidationRectangle(rangeDragStartCell, rangeDragCurrentCell));
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        if ((!attributeMode && !priorityMode) || tileSet is null || e.Button != MouseButtons.Left)
        {
            return;
        }

        var tileId = GetTileIdFromLocation(e.Location);
        if (tileId < 0)
        {
            return;
        }

        SelectedTileId = tileId;
        if (attributeMode)
        {
            using var dialog = new AttributeSetEditorDialog(attributeList, tileSet.GetDefaultAttributes(tileId));
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            ApplyAttributeValues(tileId, dialog.SelectedValues);
            return;
        }

        using var priorityDialog = new PriorityEditorDialog(tileSet.GetDefaultDisplayPriority(tileId));
        if (priorityDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ApplyDisplayPriority(tileId, priorityDialog.Priority);
    }

    private bool TryBeginRangeDrag(Point location, MouseButtons button)
    {
        if (tileSet is null || !IsRangeDragModifierActive())
        {
            return false;
        }

        if (button != MouseButtons.Left && (button != MouseButtons.Right || (!attributeMode && !priorityMode)))
        {
            return false;
        }

        var tileId = GetTileIdFromLocation(location);
        if (tileId < 0)
        {
            return false;
        }

        var cell = GetCellFromTileId(tileId);
        rangeDragActive = true;
        rangeDragButton = button;
        rangeDragStartCell = cell;
        rangeDragCurrentCell = cell;
        Invalidate(GetRangeInvalidationRectangle(cell, cell));
        return true;
    }

    private void UpdateRangeDrag(Point location)
    {
        if (tileSet is null)
        {
            return;
        }

        var cell = GetCellFromLocation(location);
        cell = new Point(
            Math.Clamp(cell.X, 0, tileSet.Columns - 1),
            Math.Clamp(cell.Y, 0, tileSet.Rows - 1));

        if (cell == rangeDragCurrentCell)
        {
            return;
        }

        var previousRectangle = GetRangeInvalidationRectangle(rangeDragStartCell, rangeDragCurrentCell);
        rangeDragCurrentCell = cell;
        Invalidate(previousRectangle);
        Invalidate(GetRangeInvalidationRectangle(rangeDragStartCell, rangeDragCurrentCell));
    }

    private void CommitRangeDrag()
    {
        if (tileSet is null)
        {
            return;
        }

        if (!attributeMode && !priorityMode)
        {
            SetSelectionRange(rangeDragStartCell, rangeDragCurrentCell);
            return;
        }

        var changedCount = 0;
        if (attributeMode)
        {
            IReadOnlyList<int> values = rangeDragButton == MouseButtons.Right ? [] : selectedAttributeValues;
            foreach (var tileId in GetTileIdsInRange(rangeDragStartCell, rangeDragCurrentCell))
            {
                if (ApplyAttributeValues(tileId, values))
                {
                    changedCount++;
                }
            }
        }
        else
        {
            var priority = rangeDragButton == MouseButtons.Right ? 0 : selectedDisplayPriority;
            foreach (var tileId in GetTileIdsInRange(rangeDragStartCell, rangeDragCurrentCell))
            {
                if (ApplyDisplayPriority(tileId, priority))
                {
                    changedCount++;
                }
            }
        }

        if (changedCount > 0)
        {
            SelectedTileChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyMouseAction(Point location, MouseButtons button)
    {
        var tileId = GetTileIdFromLocation(location);
        if (tileSet is null || tileId < 0)
        {
            return;
        }

        SelectedTileId = tileId;
        if (!attributeMode && !priorityMode)
        {
            SetSelectionToTile(tileId);
            return;
        }

        if (button != MouseButtons.Left && button != MouseButtons.Right)
        {
            return;
        }

        if (attributeMode)
        {
            IReadOnlyList<int> values = button == MouseButtons.Right ? [] : selectedAttributeValues;
            ApplyAttributeValues(tileId, values);
            return;
        }

        var priority = button == MouseButtons.Right ? 0 : selectedDisplayPriority;
        ApplyDisplayPriority(tileId, priority);
    }

    private bool ApplyAttributeValues(int tileId, IReadOnlyList<int> values)
    {
        if (tileSet is null)
        {
            return false;
        }

        var normalized = values.Distinct().Order().ToArray();
        if (TilePlacement.FormatAttributeValues(tileSet.GetDefaultAttributes(tileId)) == TilePlacement.FormatAttributeValues(normalized))
        {
            return false;
        }

        tileSet.SetDefaultAttributes(tileId, normalized);
        Invalidate(GetTileRectangle(tileId));
        TileAttributeChanged?.Invoke(this, new TileAttributeChangedEventArgs(tileSet, tileId, normalized));
        return true;
    }

    private bool ApplyDisplayPriority(int tileId, int displayPriority)
    {
        if (tileSet is null)
        {
            return false;
        }

        if (tileSet.GetDefaultDisplayPriority(tileId) == displayPriority)
        {
            return false;
        }

        tileSet.SetDefaultDisplayPriority(tileId, displayPriority);
        Invalidate(GetTileRectangle(tileId));
        TilePriorityChanged?.Invoke(this, new TilePriorityChangedEventArgs(tileSet, tileId, displayPriority));
        return true;
    }

    private void UpdateScrollSize()
    {
        AutoScrollMinSize = tileSet is null
            ? Size.Empty
            : new Size(tileSet.Columns * tileSet.TileSize, tileSet.Rows * tileSet.TileSize);
    }

    private Rectangle GetTileRectangle(int tileId)
    {
        if (tileSet is null)
        {
            return Rectangle.Empty;
        }

        var column = tileId % tileSet.Columns;
        var row = tileId / tileSet.Columns;
        return new Rectangle(column * tileSet.TileSize, row * tileSet.TileSize, tileSet.TileSize, tileSet.TileSize);
    }

    private Point GetCellFromTileId(int tileId)
    {
        if (tileSet is null || tileId < 0)
        {
            return Point.Empty;
        }

        return new Point(tileId % tileSet.Columns, tileId / tileSet.Columns);
    }

    private Point GetCellFromLocation(Point location)
    {
        if (tileSet is null)
        {
            return new Point(-1, -1);
        }

        var x = location.X - AutoScrollPosition.X;
        var y = location.Y - AutoScrollPosition.Y;
        return new Point(x / tileSet.TileSize, y / tileSet.TileSize);
    }

    private int GetTileIdFromLocation(Point location)
    {
        if (tileSet is null)
        {
            return -1;
        }

        var x = location.X - AutoScrollPosition.X;
        var y = location.Y - AutoScrollPosition.Y;
        if (x < 0 || y < 0)
        {
            return -1;
        }

        var column = x / tileSet.TileSize;
        var row = y / tileSet.TileSize;
        if (column < 0 || row < 0 || column >= tileSet.Columns || row >= tileSet.Rows)
        {
            return -1;
        }

        return row * tileSet.Columns + column;
    }

    private void DrawAttributeTileOverlay(Graphics graphics, int tileId, Rectangle destination)
    {
        if (tileSet is null)
        {
            return;
        }

        using (var dimBrush = new SolidBrush(Color.FromArgb(138, 0, 0, 0)))
        {
            graphics.FillRectangle(dimBrush, destination);
        }

        var attributeValues = tileSet.GetDefaultAttributes(tileId);
        if (attributeValues.Count == 0)
        {
            return;
        }

        var label = GetAttributeAbbreviation(attributeValues);
        DrawAttributeLabel(graphics, label, destination);
    }

    private void DrawPriorityTileOverlay(Graphics graphics, int tileId, Rectangle destination)
    {
        if (tileSet is null)
        {
            return;
        }

        using (var dimBrush = new SolidBrush(Color.FromArgb(138, 0, 0, 0)))
        {
            graphics.FillRectangle(dimBrush, destination);
        }

        DrawAttributeLabel(graphics, tileSet.GetDefaultDisplayPriority(tileId).ToString(), destination);
    }

    private void DrawAttributeLabel(Graphics graphics, string label, Rectangle destination)
    {
        var text = FitLabel(graphics, label, destination.Width - 4);
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
        graphics.DrawString(text, font, shadowBrush, shadow, format);
        graphics.DrawString(text, font, textBrush, rect, format);
    }

    private string FitLabel(Graphics graphics, string label, int maxWidth)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return "";
        }

        using var font = new Font(Font.FontFamily, 8f, FontStyle.Bold);
        if (graphics.MeasureString(label, font).Width <= maxWidth)
        {
            return label;
        }

        for (var length = Math.Min(label.Length, 8); length > 1; length--)
        {
            var candidate = label[..length];
            if (graphics.MeasureString(candidate, font).Width <= maxWidth)
            {
                return candidate;
            }
        }

        return label[0].ToString();
    }

    private string GetAttributeAbbreviation(IReadOnlyList<int> values)
    {
        var parts = values
            .Select(value => attributeList?.FindValue(value) is { } definition
                ? definition.GetDisplayText()
                : value.ToString())
            .ToArray();
        return string.Join("", parts);
    }

    private void DrawSelection(Graphics graphics)
    {
        if (tileSet is null)
        {
            return;
        }

        if (selectedTileId < 0)
        {
            return;
        }

        var range = GetCellRange(selectionStartCell, selectionEndCell);
        var rect = new Rectangle(
            range.X * tileSet.TileSize,
            range.Y * tileSet.TileSize,
            range.Width * tileSet.TileSize,
            range.Height * tileSet.TileSize);
        rect.Width -= 1;
        rect.Height -= 1;

        using var outerPen = new Pen(Color.FromArgb(255, 230, 80), 2);
        using var innerPen = new Pen(Color.FromArgb(40, 40, 40));
        graphics.DrawRectangle(outerPen, rect);
        rect.Inflate(-2, -2);
        graphics.DrawRectangle(innerPen, rect);
    }

    private void DrawRangeDrag(Graphics graphics)
    {
        if (tileSet is null || !rangeDragActive)
        {
            return;
        }

        var range = GetCellRange(rangeDragStartCell, rangeDragCurrentCell);
        var rect = new Rectangle(
            range.X * tileSet.TileSize,
            range.Y * tileSet.TileSize,
            range.Width * tileSet.TileSize,
            range.Height * tileSet.TileSize);

        using var brush = new SolidBrush(Color.FromArgb(34, 255, 224, 64));
        using var pen = new Pen(Color.FromArgb(255, 255, 224, 64), 2f);
        graphics.FillRectangle(brush, rect);
        graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
    }

    private void DrawGrid(Graphics graphics)
    {
        if (tileSet is null)
        {
            return;
        }

        using var pen = new Pen(Color.FromArgb(80, 20, 20, 20));
        var width = tileSet.Columns * tileSet.TileSize;
        var height = tileSet.Rows * tileSet.TileSize;

        for (var x = 0; x <= width; x += tileSet.TileSize)
        {
            graphics.DrawLine(pen, x, 0, x, height);
        }

        for (var y = 0; y <= height; y += tileSet.TileSize)
        {
            graphics.DrawLine(pen, 0, y, width, y);
        }
    }

    private void DrawEmptyState(Graphics graphics)
    {
        const string message = "Tileset not loaded";

        using var brush = new SolidBrush(Color.FromArgb(180, 184, 190));
        var size = graphics.MeasureString(message, Font);
        graphics.DrawString(message, Font, brush, (Width - size.Width) / 2f, (Height - size.Height) / 2f);
    }

    private void SetSelectionToTile(int tileId)
    {
        selectionStartCell = GetCellFromTileId(tileId);
        selectionEndCell = selectionStartCell;
    }

    private void SetSelectionRange(Point start, Point end)
    {
        if (tileSet is null)
        {
            return;
        }

        var range = GetCellRange(start, end);
        var firstTileId = range.Top * tileSet.Columns + range.Left;
        if (firstTileId >= tileSet.TileCount)
        {
            return;
        }

        selectedTileId = firstTileId;
        selectionStartCell = new Point(range.Left, range.Top);
        selectionEndCell = new Point(range.Right - 1, range.Bottom - 1);
        Invalidate();
        SelectedTileChanged?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<TileSelectionCell> GetSelectedTileSelection()
    {
        if (tileSet is null || selectedTileId < 0)
        {
            return [];
        }

        var range = GetCellRange(selectionStartCell, selectionEndCell);
        var cells = new List<TileSelectionCell>();
        for (var row = range.Top; row < range.Bottom; row++)
        {
            for (var column = range.Left; column < range.Right; column++)
            {
                var tileId = row * tileSet.Columns + column;
                if (tileId >= tileSet.TileCount)
                {
                    continue;
                }

                cells.Add(new TileSelectionCell(column - range.Left, row - range.Top, tileId));
            }
        }

        return cells.Count == 0 ? [new TileSelectionCell(0, 0, selectedTileId)] : cells;
    }

    private IEnumerable<int> GetTileIdsInRange(Point start, Point end)
    {
        if (tileSet is null)
        {
            yield break;
        }

        var range = GetCellRange(start, end);
        for (var row = range.Top; row < range.Bottom; row++)
        {
            for (var column = range.Left; column < range.Right; column++)
            {
                var tileId = row * tileSet.Columns + column;
                if (tileId < tileSet.TileCount)
                {
                    yield return tileId;
                }
            }
        }
    }

    private Rectangle GetRangeInvalidationRectangle(Point start, Point end)
    {
        if (tileSet is null)
        {
            return ClientRectangle;
        }

        var range = GetCellRange(start, end);
        var rectangle = new Rectangle(
            range.X * tileSet.TileSize + AutoScrollPosition.X,
            range.Y * tileSet.TileSize + AutoScrollPosition.Y,
            range.Width * tileSet.TileSize + 1,
            range.Height * tileSet.TileSize + 1);
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

    private static bool IsRangeDragModifierActive()
    {
        return (ModifierKeys & Keys.Shift) == Keys.Shift;
    }
}
