namespace GameEditor;

public sealed class TilePaletteControl : ScrollableControl
{
    private TileSet? tileSet;
    private int selectedTileId;

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
        }

        DrawSelection(e.Graphics);
        DrawGrid(e.Graphics);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (tileSet is null || e.Button != MouseButtons.Left)
        {
            return;
        }

        var x = e.X - AutoScrollPosition.X;
        var y = e.Y - AutoScrollPosition.Y;
        var column = x / tileSet.TileSize;
        var row = y / tileSet.TileSize;
        var tileId = row * tileSet.Columns + column;

        if (column >= 0 && row >= 0 && column < tileSet.Columns && row < tileSet.Rows)
        {
            SelectedTileId = tileId;
        }
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
