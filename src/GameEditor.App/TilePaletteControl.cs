namespace GameEditor;

public sealed class TilePaletteControl : ScrollableControl
{
    private TileSet? tileSet;
    private int selectedTileId;
    private bool attributeMode;
    private AttributeListDefinition? attributeList;
    private IReadOnlyList<int> selectedAttributeValues = [];

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

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public TileSet? TileSet
    {
        get => tileSet;
        set
        {
            tileSet = value;
            selectedTileId = value is null ? -1 : 0;
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
        }

        DrawSelection(e.Graphics);
        DrawGrid(e.Graphics);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (tileSet is null)
        {
            return;
        }

        ApplyMouseAction(e.Location, e.Button);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!attributeMode || tileSet is null || e.Button == MouseButtons.None)
        {
            return;
        }

        ApplyMouseAction(e.Location, e.Button);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        if (!attributeMode || tileSet is null || e.Button != MouseButtons.Left)
        {
            return;
        }

        var tileId = GetTileIdFromLocation(e.Location);
        if (tileId < 0)
        {
            return;
        }

        SelectedTileId = tileId;
        using var dialog = new AttributeSetEditorDialog(attributeList, tileSet.GetDefaultAttributes(tileId));
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        ApplyAttributeValues(tileId, dialog.SelectedValues);
    }

    private void ApplyMouseAction(Point location, MouseButtons button)
    {
        var tileId = GetTileIdFromLocation(location);
        if (tileSet is null || tileId < 0)
        {
            return;
        }

        SelectedTileId = tileId;
        if (!attributeMode)
        {
            return;
        }

        if (button != MouseButtons.Left && button != MouseButtons.Right)
        {
            return;
        }

        IReadOnlyList<int> values = button == MouseButtons.Right ? [] : selectedAttributeValues;
        ApplyAttributeValues(tileId, values);
    }

    private void ApplyAttributeValues(int tileId, IReadOnlyList<int> values)
    {
        if (tileSet is null)
        {
            return;
        }

        var normalized = values.Distinct().Order().ToArray();
        if (TilePlacement.FormatAttributeValues(tileSet.GetDefaultAttributes(tileId)) == TilePlacement.FormatAttributeValues(normalized))
        {
            return;
        }

        tileSet.SetDefaultAttributes(tileId, normalized);
        Invalidate(GetTileRectangle(tileId));
        TileAttributeChanged?.Invoke(this, new TileAttributeChangedEventArgs(tileSet, tileId, normalized));
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
            .Select(value => attributeList?.FindValue(value)?.Name is { Length: > 0 } name
                ? name[0].ToString().ToUpperInvariant()
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

        var rect = GetTileRectangle(selectedTileId);
        rect.Width -= 1;
        rect.Height -= 1;

        using var outerPen = new Pen(Color.FromArgb(255, 230, 80), 2);
        using var innerPen = new Pen(Color.FromArgb(40, 40, 40));
        graphics.DrawRectangle(outerPen, rect);
        rect.Inflate(-2, -2);
        graphics.DrawRectangle(innerPen, rect);
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
}
