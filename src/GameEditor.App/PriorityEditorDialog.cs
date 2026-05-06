namespace GameEditor;

public sealed class PriorityEditorDialog : Form
{
    private readonly NumericUpDown priorityInput = new();

    public PriorityEditorDialog(int priority)
    {
        Text = "表示優先度";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(260, 96);

        var label = new Label
        {
            AutoSize = true,
            Location = new Point(12, 16),
            Text = "優先度:"
        };

        priorityInput.Location = new Point(72, 12);
        priorityInput.Minimum = -9999;
        priorityInput.Maximum = 9999;
        priorityInput.Value = Math.Clamp(priority, (int)priorityInput.Minimum, (int)priorityInput.Maximum);
        priorityInput.Width = 160;

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(76, 58),
            Width = 80
        };
        var cancelButton = new Button
        {
            Text = "キャンセル",
            DialogResult = DialogResult.Cancel,
            Location = new Point(164, 58),
            Width = 80
        };

        Controls.Add(label);
        Controls.Add(priorityInput);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public int Priority => (int)priorityInput.Value;
}
