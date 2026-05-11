using System.ComponentModel;

namespace GameEditor;

public sealed class AnimationPreviewControl : Control
{
    private TileSet? tileSet;
    private int tileId;
    private string eventName = "";

    public AnimationPreviewControl()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(34, 36, 40);
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TileSet? TileSet
    {
        get => tileSet;
        set
        {
            tileSet = value;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int TileId
    {
        get => tileId;
        set
        {
            tileId = value;
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string EventName
    {
        get => eventName;
        set
        {
            eventName = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(BackColor);
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

        DrawChecker(e.Graphics);
        if (tileSet is null || tileId < 0)
        {
            DrawEmpty(e.Graphics);
            return;
        }

        var size = tileSet.TileSize;
        var scale = Math.Max(1, Math.Min((ClientSize.Width - 48) / size, (ClientSize.Height - 72) / size));
        var drawSize = size * scale;
        var destination = new Rectangle(
            (ClientSize.Width - drawSize) / 2,
            Math.Max(24, (ClientSize.Height - drawSize) / 2),
            drawSize,
            drawSize);

        tileSet.DrawTile(e.Graphics, tileId, destination);
        using var borderPen = new Pen(Color.FromArgb(210, 230, 232, 238));
        e.Graphics.DrawRectangle(borderPen, destination.X, destination.Y, destination.Width - 1, destination.Height - 1);

        if (!string.IsNullOrWhiteSpace(eventName))
        {
            DrawEventLabel(e.Graphics, eventName);
        }
    }

    private void DrawChecker(Graphics graphics)
    {
        const int cell = 16;
        using var a = new SolidBrush(Color.FromArgb(47, 50, 55));
        using var b = new SolidBrush(Color.FromArgb(39, 42, 47));
        for (var y = 0; y < Height; y += cell)
        {
            for (var x = 0; x < Width; x += cell)
            {
                graphics.FillRectangle(((x / cell) + (y / cell)) % 2 == 0 ? a : b, x, y, cell, cell);
            }
        }
    }

    private void DrawEmpty(Graphics graphics)
    {
        const string message = "No frame";
        using var brush = new SolidBrush(Color.FromArgb(180, 188, 194));
        var size = graphics.MeasureString(message, Font);
        graphics.DrawString(message, Font, brush, (Width - size.Width) / 2f, (Height - size.Height) / 2f);
    }

    private void DrawEventLabel(Graphics graphics, string label)
    {
        using var font = new Font(Font.FontFamily, 9f, FontStyle.Bold);
        var rect = new Rectangle(12, Height - 42, Width - 24, 26);
        using var background = new SolidBrush(Color.FromArgb(190, 24, 26, 30));
        using var text = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };
        graphics.FillRectangle(background, rect);
        graphics.DrawString($"event: {label}", font, text, rect, format);
    }
}
