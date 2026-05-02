namespace GameEditor;

public sealed class DocumentTabControl : TabControl
{
    private const int WmPaint = 0x000F;

    public DocumentTabControl()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        Padding = new Point(12, 4);
        ItemSize = new Size(112, 24);
        BackColor = SystemColors.Control;
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color HeaderBackColor { get; set; } = SystemColors.Control;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color PageBackColor { get; set; } = SystemColors.Control;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color SelectedTabBackColor { get; set; } = SystemColors.Window;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color SelectedTextColor { get; set; } = SystemColors.ControlText;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color TabBackColor { get; set; } = SystemColors.Control;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color TextColor { get; set; } = SystemColors.ControlText;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = SystemColors.ControlDark;

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);

        if (e.Control is TabPage page)
        {
            ConfigurePage(page);
        }
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        DrawTab(e.Graphics, e.Index);
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WmPaint && !IsDisposed)
        {
            using var graphics = Graphics.FromHwnd(Handle);
            PaintHeader(graphics);
        }
    }

    private void ConfigurePage(TabPage page)
    {
        page.BackColor = PageBackColor;
        page.Padding = System.Windows.Forms.Padding.Empty;
        page.UseVisualStyleBackColor = false;
    }

    private void PaintHeader(Graphics graphics)
    {
        var headerHeight = GetHeaderHeight();
        if (headerHeight <= 0)
        {
            return;
        }

        using var backgroundBrush = new SolidBrush(HeaderBackColor);
        graphics.FillRectangle(backgroundBrush, new Rectangle(0, 0, Width, headerHeight));

        for (var i = 0; i < TabPages.Count; i++)
        {
            DrawTab(graphics, i);
        }
    }

    private int GetHeaderHeight()
    {
        var bottom = 0;
        for (var i = 0; i < TabPages.Count; i++)
        {
            bottom = Math.Max(bottom, GetTabRect(i).Bottom);
        }

        return bottom == 0 ? 0 : Math.Min(Height, bottom + 1);
    }

    private void DrawTab(Graphics graphics, int index)
    {
        if (index < 0 || index >= TabPages.Count)
        {
            return;
        }

        var selected = index == SelectedIndex;
        var bounds = GetTabRect(index);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (selected)
        {
            bounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height + 1);
        }
        else
        {
            bounds = new Rectangle(bounds.X, bounds.Y + 2, bounds.Width, Math.Max(1, bounds.Height - 2));
        }

        using var tabBrush = new SolidBrush(selected ? SelectedTabBackColor : TabBackColor);
        using var borderPen = new Pen(BorderColor);
        graphics.FillRectangle(tabBrush, bounds);
        graphics.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);

        var textBounds = Rectangle.Inflate(bounds, -8, -2);
        TextRenderer.DrawText(
            graphics,
            TabPages[index].Text,
            Font,
            textBounds,
            selected ? SelectedTextColor : TextColor,
            TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}
