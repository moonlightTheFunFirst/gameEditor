namespace GameEditor;

public sealed class AttributeListEditorDialog : Form
{
    private readonly ListBox lists = new();
    private readonly DataGridView values = new();
    private readonly Button addListButton = new();
    private readonly Button removeListButton = new();
    private readonly BindingSource valueBinding = new();
    private readonly List<AttributeListDefinition> attributeLists;

    public AttributeListEditorDialog(IEnumerable<AttributeListDefinition> source)
    {
        attributeLists = source.Select(CloneList).ToList();

        Text = "Attribute Lists";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 420);
        ClientSize = new Size(720, 460);

        Controls.Add(BuildLayout());
        RefreshLists();
    }

    public IReadOnlyList<AttributeListDefinition> AttributeLists => attributeLists;

    private Control BuildLayout()
    {
        var root = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            SplitterDistance = 220
        };

        lists.Dock = DockStyle.Fill;
        lists.DisplayMember = nameof(AttributeListDefinition.Name);
        lists.SelectedIndexChanged += (_, _) => UpdateSelectedList();

        addListButton.Text = "Add";
        addListButton.Dock = DockStyle.Left;
        addListButton.Width = 80;
        addListButton.Click += (_, _) => AddList();

        removeListButton.Text = "Remove";
        removeListButton.Dock = DockStyle.Left;
        removeListButton.Width = 80;
        removeListButton.Click += (_, _) => RemoveSelectedList();

        var listButtons = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 34
        };
        listButtons.Controls.Add(removeListButton);
        listButtons.Controls.Add(addListButton);

        var left = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        left.Controls.Add(lists);
        left.Controls.Add(listButtons);

        values.Dock = DockStyle.Fill;
        values.AutoGenerateColumns = false;
        values.AllowUserToAddRows = true;
        values.AllowUserToDeleteRows = true;
        values.DataSource = valueBinding;
        values.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(AttributeDefinition.Value),
            HeaderText = "Value",
            Width = 80
        });
        values.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(AttributeDefinition.Name),
            HeaderText = "Name",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Dock = DockStyle.Right,
            Width = 90
        };
        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Dock = DockStyle.Right,
            Width = 90
        };

        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            Padding = new Padding(8)
        };
        bottom.Controls.Add(cancelButton);
        bottom.Controls.Add(okButton);

        var right = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        right.Controls.Add(values);
        right.Controls.Add(bottom);

        root.Panel1.Controls.Add(left);
        root.Panel2.Controls.Add(right);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        return root;
    }

    private void RefreshLists()
    {
        lists.Items.Clear();
        foreach (var list in attributeLists)
        {
            lists.Items.Add(list);
        }

        lists.SelectedIndex = lists.Items.Count > 0 ? 0 : -1;
    }

    private void UpdateSelectedList()
    {
        valueBinding.DataSource = lists.SelectedItem is AttributeListDefinition list
            ? list.Values
            : null;
        removeListButton.Enabled = lists.SelectedItem is not null;
    }

    private void AddList()
    {
        var index = attributeLists.Count + 1;
        var list = new AttributeListDefinition(
            $"attributes-{index}",
            $"Attributes {index}",
            [
                new AttributeDefinition(0, "None", Color.Transparent),
                new AttributeDefinition(1, "Blocked", Color.FromArgb(160, 220, 50, 50))
            ]);

        attributeLists.Add(list);
        RefreshLists();
        lists.SelectedItem = list;
    }

    private void RemoveSelectedList()
    {
        if (lists.SelectedItem is not AttributeListDefinition list)
        {
            return;
        }

        attributeLists.Remove(list);
        RefreshLists();
    }

    private static AttributeListDefinition CloneList(AttributeListDefinition list)
    {
        return new AttributeListDefinition(
            list.Id,
            list.Name,
            list.Values
                .Select(value => new AttributeDefinition(value.Value, value.Name, value.Color))
                .ToList());
    }
}

