namespace GameEditor;

public sealed class MapViewport : ScrollableControl
{
    private MapDocument? document;
    private TileSet? tileSet;
    private int selectedTileId;

    public MapViewport()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(36, 38, 42);
        DoubleBuffered = true;
        ResizeRedraw = true;
        AutoScroll = true;
    }

    public event EventHandler<Point>? TilePlaced;

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
    public TileSet? TileSet
    {
        get => tileSet;
        set
        {
            tileSet = value;
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

        if (document is null || tileSet is null || e.Button != MouseButtons.Left)
        {
            return;
        }

        var mapX = e.X - AutoScrollPosition.X;
        var mapY = e.Y - AutoScrollPosition.Y;
        var tileX = mapX / document.TileSize;
        var tileY = mapY / document.TileSize;

        if (!document.IsInside(tileX, tileY))
        {
            return;
        }

        document.SetTile(tileX, tileY, selectedTileId);
        Invalidate(GetInvalidationRectangle(tileX, tileY));
        TilePlaced?.Invoke(this, new Point(tileX, tileY));
    }

    private void UpdateScrollSize()
    {
        AutoScrollMinSize = document?.PixelSize ?? Size.Empty;
    }

    private void DrawPlacedTiles(Graphics graphics)
    {
        if (document is null || tileSet is null)
        {
            return;
        }

        for (var y = 0; y < document.Height; y++)
        {
            for (var x = 0; x < document.Width; x++)
            {
                var tileId = document.GetTile(x, y);
                if (tileId < 0)
                {
                    continue;
                }

                var destination = new Rectangle(
                    x * document.TileSize,
                    y * document.TileSize,
                    document.TileSize,
                    document.TileSize);
                tileSet.DrawTile(graphics, tileId, destination);
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
