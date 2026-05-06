namespace GameEditor;

public sealed class AttributeListEditorDialog : Form
{
    private readonly ListBox lists = new();
    private readonly DataGridView values = new();
    private readonly Button addListButton = new();
    private readonly Button removeListButton = new();
    private readonly Button addValueButton = new();
    private readonly Button removeValueButton = new();
    private readonly Button saveButton = new();
    private readonly BindingSource valueBinding = new();
    private readonly List<AttributeListDefinition> attributeLists;
    private readonly Func<IReadOnlyList<AttributeListDefinition>, bool>? saveHandler;
    private bool isDirty;
    private bool updating;

    public AttributeListEditorDialog(
        IEnumerable<AttributeListDefinition> source,
        Func<IReadOnlyList<AttributeListDefinition>, bool>? saveHandler = null)
    {
        attributeLists = source.Select(CloneList).ToList();
        this.saveHandler = saveHandler;

        Text = "Attribute Lists";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(820, 460);
        ClientSize = new Size(900, 520);

        Controls.Add(BuildLayout());
        RefreshLists();
        isDirty = false;
    }

    public IReadOnlyList<AttributeListDefinition> AttributeLists => attributeLists;

    private Control BuildLayout()
    {
        var root = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            SplitterDistance = 240
        };

        lists.Dock = DockStyle.Fill;
        lists.DisplayMember = nameof(AttributeListDefinition.Name);
        lists.SelectedIndexChanged += (_, _) => UpdateSelectedList();

        addListButton.Text = "Add";
        addListButton.Dock = DockStyle.Top;
        addListButton.Height = 28;
        addListButton.Click += (_, _) => AddList();

        removeListButton.Text = "Remove";
        removeListButton.Dock = DockStyle.Top;
        removeListButton.Height = 28;
        removeListButton.Click += (_, _) => RemoveSelectedList();

        var listButtons = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 62,
            Padding = new Padding(0, 4, 0, 0)
        };
        listButtons.Controls.Add(addListButton);
        listButtons.Controls.Add(removeListButton);

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
        values.CellValueChanged += (_, _) => MarkDirty();
        values.CellValidating += ValuesCellValidating;
        values.SelectionChanged += (_, _) => UpdateValueActions();
        values.UserDeletingRow += ValuesUserDeletingRow;
        values.UserAddedRow += (_, _) => MarkDirty();
        values.UserDeletedRow += (_, _) => MarkDirty();
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
            Width = 160
        });
        values.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(AttributeDefinition.DisplayText),
            HeaderText = "表示",
            Width = 60
        });
        values.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(AttributeDefinition.Memo),
            HeaderText = "メモ",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        addValueButton.Text = "Add";
        addValueButton.Dock = DockStyle.Top;
        addValueButton.Height = 28;
        addValueButton.Click += (_, _) => AddValue();

        removeValueButton.Text = "Remove";
        removeValueButton.Dock = DockStyle.Top;
        removeValueButton.Height = 28;
        removeValueButton.Click += (_, _) => RemoveSelectedValue();

        var valueButtons = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 62,
            Padding = new Padding(0, 4, 0, 0)
        };
        valueButtons.Controls.Add(addValueButton);
        valueButtons.Controls.Add(removeValueButton);

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Dock = DockStyle.Right,
            Width = 90
        };
        okButton.Click += (_, _) => EnsureDefaultAttributes();
        saveButton.Text = "保存";
        saveButton.Dock = DockStyle.Left;
        saveButton.Width = 90;
        saveButton.Click += (_, _) => SaveFromDialog();
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
        bottom.Controls.Add(saveButton);

        var right = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        right.Controls.Add(values);
        right.Controls.Add(valueButtons);
        right.Controls.Add(bottom);

        root.Panel1.Controls.Add(left);
        root.Panel2.Controls.Add(right);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        return root;
    }

    private void RefreshLists()
    {
        updating = true;
        lists.Items.Clear();
        foreach (var list in attributeLists)
        {
            lists.Items.Add(list);
        }

        lists.SelectedIndex = lists.Items.Count > 0 ? 0 : -1;
        updating = false;
    }

    private void UpdateSelectedList()
    {
        valueBinding.DataSource = lists.SelectedItem is AttributeListDefinition list
            ? list.Values
            : null;
        removeListButton.Enabled = lists.SelectedItem is not null;
        UpdateValueActions();
    }

    private void AddList()
    {
        var index = attributeLists.Count + 1;
        var list = new AttributeListDefinition(
            $"attributes-{index}",
            $"Attributes {index}",
            AttributeDefinition.CreateBuiltInDefaults().ToList());

        attributeLists.Add(list);
        RefreshLists();
        lists.SelectedItem = list;
        MarkDirty();
    }

    private void RemoveSelectedList()
    {
        if (lists.SelectedItem is not AttributeListDefinition list)
        {
            return;
        }

        attributeLists.Remove(list);
        RefreshLists();
        MarkDirty();
    }

    private void AddValue()
    {
        if (lists.SelectedItem is not AttributeListDefinition list)
        {
            return;
        }

        var nextValue = list.Values.Count == 0 ? 1 : list.Values.Max(value => value.Value) + 1;
        var value = new AttributeDefinition(nextValue, $"Attribute {nextValue}", Color.Transparent);
        list.Values.Add(value);
        valueBinding.ResetBindings(false);
        SelectValue(value);
        MarkDirty();
    }

    private void RemoveSelectedValue()
    {
        if (lists.SelectedItem is not AttributeListDefinition list || GetSelectedValue() is not { } value)
        {
            return;
        }

        if (IsDefaultAttribute(value))
        {
            MessageBox.Show(this, "組み込み属性は削除できません。", "属性", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        list.Values.Remove(value);
        valueBinding.ResetBindings(false);
        UpdateValueActions();
        MarkDirty();
    }

    private void ValuesUserDeletingRow(object? sender, DataGridViewRowCancelEventArgs e)
    {
        if (e.Row?.DataBoundItem is not AttributeDefinition value || !IsDefaultAttribute(value))
        {
            return;
        }

        e.Cancel = true;
        MessageBox.Show(this, "組み込み属性は削除できません。", "属性", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ValuesCellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (values.Columns[e.ColumnIndex].DataPropertyName == nameof(AttributeDefinition.Value)
            && values.Rows[e.RowIndex].DataBoundItem is AttributeDefinition editedDefinition
            && int.TryParse(e.FormattedValue?.ToString(), out var requestedValue))
        {
            if (IsDefaultAttribute(editedDefinition) && requestedValue != editedDefinition.Value)
            {
                e.Cancel = true;
                MessageBox.Show(this, "組み込み属性の値は変更できません。", "属性", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!IsDefaultAttribute(editedDefinition) && AttributeDefinition.IsBuiltInValue(requestedValue))
            {
                e.Cancel = true;
                MessageBox.Show(this, "組み込み属性の値は使用できません。", "属性", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            return;
        }

        if (values.Columns[e.ColumnIndex].DataPropertyName != nameof(AttributeDefinition.Value)
            || values.Rows[e.RowIndex].DataBoundItem is not AttributeDefinition value
            || !IsDefaultAttribute(value))
        {
            return;
        }

        if (int.TryParse(e.FormattedValue?.ToString(), out var editedValue) && editedValue == 0)
        {
            return;
        }

        e.Cancel = true;
        MessageBox.Show(this, "組み込み属性の値は変更できません。", "属性", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UpdateValueActions()
    {
        removeValueButton.Enabled = GetSelectedValue() is { } value && !IsDefaultAttribute(value);
    }

    private AttributeDefinition? GetSelectedValue()
    {
        return values.CurrentRow?.DataBoundItem as AttributeDefinition;
    }

    private void SelectValue(AttributeDefinition value)
    {
        foreach (DataGridViewRow row in values.Rows)
        {
            if (!ReferenceEquals(row.DataBoundItem, value))
            {
                continue;
            }

            row.Selected = true;
            values.CurrentCell = row.Cells[0];
            break;
        }
    }

    private void EnsureDefaultAttributes()
    {
        foreach (var list in attributeLists)
        {
            if (list.EnsureBuiltInAttributes())
            {
                MarkDirty();
            }
        }
    }

    private bool SaveFromDialog()
    {
        values.EndEdit();
        valueBinding.EndEdit();
        if (!ValidateChildren())
        {
            return false;
        }

        EnsureDefaultAttributes();

        if (saveHandler is null)
        {
            isDirty = false;
            return true;
        }

        if (!saveHandler(AttributeLists))
        {
            return false;
        }

        isDirty = false;
        return true;
    }

    private void MarkDirty()
    {
        if (!updating)
        {
            isDirty = true;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult == DialogResult.OK)
        {
            EnsureDefaultAttributes();
        }

        if (!isDirty)
        {
            base.OnFormClosing(e);
            return;
        }

        var result = MessageBox.Show(
            this,
            "属性リストが保存されていません。保存しますか？",
            "属性リストの保存確認",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning);

        if (result == DialogResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (result == DialogResult.Yes && !SaveFromDialog())
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    private static bool IsDefaultAttribute(AttributeDefinition value)
    {
        return AttributeDefinition.IsBuiltInValue(value.Value);
    }

    private static AttributeListDefinition CloneList(AttributeListDefinition list)
    {
        return new AttributeListDefinition(
            list.Id,
            list.Name,
            list.Values
                .Select(value => new AttributeDefinition(value.Value, value.Name, value.Color, value.DisplayText, value.Memo))
                .ToList());
    }
}
