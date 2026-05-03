namespace GameEditor;

public sealed class NewProjectDialog : Form
{
    private readonly TextBox nameTextBox = new();

    public NewProjectDialog()
    {
        Text = "新規プロジェクト";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(320, 110);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 2,
            RowCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        nameTextBox.Text = "NewProject";
        nameTextBox.Dock = DockStyle.Fill;

        layout.Controls.Add(new Label
        {
            Text = "名前",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        layout.Controls.Add(nameTextBox, 1, 0);

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
        layout.Controls.Add(buttons, 0, 1);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public string ProjectName => string.IsNullOrWhiteSpace(nameTextBox.Text) ? "NewProject" : nameTextBox.Text.Trim();
}
