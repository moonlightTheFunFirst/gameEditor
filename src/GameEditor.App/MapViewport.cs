namespace GameEditor;

public sealed class MapViewport : Control
{
    private const int TileSize = 32;

    public MapViewport()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(36, 38, 42);
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.Clear(BackColor);
        DrawGrid(e.Graphics);
        DrawEmptyState(e.Graphics);
    }

    private void DrawGrid(Graphics graphics)
    {
        using var gridPen = new Pen(Color.FromArgb(64, 68, 76));

        for (var x = 0; x < Width; x += TileSize)
        {
            graphics.DrawLine(gridPen, x, 0, x, Height);
        }

        for (var y = 0; y < Height; y += TileSize)
        {
            graphics.DrawLine(gridPen, 0, y, Width, y);
        }
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
