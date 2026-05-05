namespace GameEditor;

public sealed class AttributeSetEditorDialog : Form
{
    private readonly CheckedListBox values = new();

    public AttributeSetEditorDialog(AttributeListDefinition? attributeList, IEnumerable<int> selectedValues)
    {
        Text = "Attribute Set";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(260, 320);
        ClientSize = new Size(320, 420);

        var selected = selectedValues.ToHashSet();
        values.Dock = DockStyle.Fill;
        values.CheckOnClick = true;
        if (attributeList is not null)
        {
            foreach (var value in attributeList.Values.OrderBy(value => value.Value))
            {
                var index = values.Items.Add(new AttributeValueItem(value.Value, value.Name));
                values.SetItemChecked(index, selected.Contains(value.Value));
            }
        }

        var clearButton = new Button
        {
            Text = "Clear",
            Dock = DockStyle.Left,
            Width = 80
        };
        clearButton.Click += (_, _) =>
        {
            for (var i = 0; i < values.Items.Count; i++)
            {
                values.SetItemChecked(i, false);
            }
        };

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Dock = DockStyle.Right,
            Width = 80
        };
        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Dock = DockStyle.Right,
            Width = 80
        };
        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            Padding = new Padding(6)
        };
        bottom.Controls.Add(cancelButton);
        bottom.Controls.Add(okButton);
        bottom.Controls.Add(clearButton);

        Controls.Add(values);
        Controls.Add(bottom);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public IReadOnlyList<int> SelectedValues => values.CheckedItems
        .OfType<AttributeValueItem>()
        .Select(item => item.Value)
        .Order()
        .ToArray();

    private sealed record AttributeValueItem(int Value, string Name)
    {
        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Name) ? Value.ToString() : $"{Value}: {Name}";
        }
    }
}

