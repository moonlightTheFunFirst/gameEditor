namespace GameEditor;

public sealed class NewMapDialog : Form
{
    private readonly TextBox nameTextBox = new();
    private readonly NumericUpDown widthInput = new();
    private readonly NumericUpDown heightInput = new();
    private readonly NumericUpDown tileSizeInput = new();

    public NewMapDialog()
    {
        Text = "新規マップ";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(320, 180);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        nameTextBox.Text = "NewMap";
        nameTextBox.Dock = DockStyle.Fill;

        ConfigureNumberInput(widthInput, 1, 999, 40);
        ConfigureNumberInput(heightInput, 1, 999, 30);
        ConfigureNumberInput(tileSizeInput, 32, 32, 32);
        tileSizeInput.Enabled = false;

        layout.Controls.Add(CreateLabel("マップ名"), 0, 0);
        layout.Controls.Add(nameTextBox, 1, 0);
        layout.Controls.Add(CreateLabel("幅"), 0, 1);
        layout.Controls.Add(widthInput, 1, 1);
        layout.Controls.Add(CreateLabel("高さ"), 0, 2);
        layout.Controls.Add(heightInput, 1, 2);
        layout.Controls.Add(CreateLabel("チップサイズ"), 0, 3);
        layout.Controls.Add(tileSizeInput, 1, 3);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Width = 80
        };
        var cancelButton = new Button
        {
            Text = "キャンセル",
            DialogResult = DialogResult.Cancel,
            Width = 90
        };
        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);

        layout.SetColumnSpan(buttons, 2);
        layout.Controls.Add(buttons, 0, 4);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public string MapName => string.IsNullOrWhiteSpace(nameTextBox.Text) ? "NewMap" : nameTextBox.Text.Trim();

    public int MapWidth => (int)widthInput.Value;

    public int MapHeight => (int)heightInput.Value;

    public int TileSize => (int)tileSizeInput.Value;

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static void ConfigureNumberInput(NumericUpDown input, int min, int max, int value)
    {
        input.Minimum = min;
        input.Maximum = max;
        input.Value = value;
        input.Dock = DockStyle.Left;
        input.Width = 100;
    }
}
