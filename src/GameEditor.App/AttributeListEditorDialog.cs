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
    private readonly Dictionary<string, Dictionary<int, int?>> valueRemaps = [];
    private readonly Func<IReadOnlyList<AttributeListDefinition>, Dictionary<string, Dictionary<int, int?>>, bool>? saveHandler;
    private readonly Func<string, int, bool>? attributeUsageChecker;
    private bool isDirty;
    private bool updating;
    private bool selectAllEditingText;
    private AttributeDefinition? editingNameValue;
    private string editingNameOriginalName = "";

    public AttributeListEditorDialog(
        IEnumerable<AttributeListDefinition> source,
        Func<IReadOnlyList<AttributeListDefinition>, Dictionary<string, Dictionary<int, int?>>, bool>? saveHandler = null,
        Func<string, int, bool>? attributeUsageChecker = null)
    {
        attributeLists = source.Select(CloneList).ToList();
        this.saveHandler = saveHandler;
        this.attributeUsageChecker = attributeUsageChecker;
        ResetValueRemapBaseline();

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
        values.AllowUserToAddRows = false;
        values.AllowUserToDeleteRows = true;
        values.DataSource = valueBinding;
        values.CellValueChanged += (_, _) => MarkDirty();
        values.CellValidating += ValuesCellValidating;
        values.SelectionChanged += (_, _) => UpdateValueActions();
        values.UserDeletingRow += ValuesUserDeletingRow;
        values.UserAddedRow += (_, _) => MarkDirty();
        values.UserDeletedRow += (_, _) => MarkDirty();
        values.CellBeginEdit += ValuesCellBeginEdit;
        values.CellEndEdit += ValuesCellEndEdit;
        values.EditingControlShowing += ValuesEditingControlShowing;
        values.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(AttributeDefinition.Value),
            DataPropertyName = nameof(AttributeDefinition.Value),
            HeaderText = "Value",
            ReadOnly = true,
            Width = 80
        });
        values.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(AttributeDefinition.Name),
            DataPropertyName = nameof(AttributeDefinition.Name),
            HeaderText = "Name",
            Width = 160
        });
        values.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(AttributeDefinition.DisplayText),
            DataPropertyName = nameof(AttributeDefinition.DisplayText),
            HeaderText = "表示",
            Width = 60
        });
        values.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(AttributeDefinition.Memo),
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
            Dock = DockStyle.Right,
            Width = 90
        };
        okButton.Click += (_, _) =>
        {
            if (!SaveFromDialog())
            {
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
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

        var nextValue = GetNextAttributeValue(list);
        var name = $"Attribute {nextValue}";
        var value = new AttributeDefinition(
            nextValue,
            name,
            Color.Transparent,
            GetDefaultDisplayText(name, nextValue));
        list.Values.Add(value);
        valueBinding.ResetBindings(false);
        SelectValue(value);
        BeginInvoke(new Action(() => BeginEditName(value)));
        MarkDirty();
    }

    private void RemoveSelectedValue()
    {
        if (lists.SelectedItem is not AttributeListDefinition list || GetSelectedValue() is not { } value)
        {
            return;
        }

        RemoveValue(list, value);
    }

    private void ValuesUserDeletingRow(object? sender, DataGridViewRowCancelEventArgs e)
    {
        if (e.Row?.DataBoundItem is not AttributeDefinition value)
        {
            return;
        }

        e.Cancel = true;
        if (IsDefaultAttribute(value))
        {
            MessageBox.Show(this, "組み込み属性は削除できません。", "属性", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        BeginInvoke(new Action(() =>
        {
            if (lists.SelectedItem is AttributeListDefinition list && list.Values.Contains(value))
            {
                RemoveValue(list, value);
            }
        }));
    }

    private void RemoveValue(AttributeListDefinition list, AttributeDefinition value)
    {
        if (IsDefaultAttribute(value))
        {
            MessageBox.Show(this, "組み込み属性は削除できません。", "属性", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (IsAttributeValueInUse(list, value.Value))
        {
            var result = MessageBox.Show(
                this,
                "この属性を使用しているマップチップがあります。削除してよろしいですか？",
                "属性",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        var oldValues = list.Values.ToDictionary(item => item, item => item.Value);
        var deletedValue = value.Value;
        list.Values.Remove(value);
        NormalizeAttributeValues(list);
        ComposeValueRemap(list.Id, oldValues, value, deletedValue);
        valueBinding.ResetBindings(false);
        UpdateValueActions();
        MarkDirty();
    }

    private bool IsAttributeValueInUse(AttributeListDefinition list, int currentValue)
    {
        if (attributeUsageChecker is null)
        {
            return false;
        }

        if (!valueRemaps.TryGetValue(list.Id, out var valueRemap))
        {
            return attributeUsageChecker(list.Id, currentValue);
        }

        foreach (var (originalValue, mappedValue) in valueRemap)
        {
            if (mappedValue == currentValue && attributeUsageChecker(list.Id, originalValue))
            {
                return true;
            }
        }

        return false;
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

    private void ValuesCellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
    {
        if (values.Columns[e.ColumnIndex].DataPropertyName != nameof(AttributeDefinition.Name)
            || values.Rows[e.RowIndex].DataBoundItem is not AttributeDefinition value)
        {
            editingNameValue = null;
            return;
        }

        editingNameValue = value;
        editingNameOriginalName = value.Name;
    }

    private void ValuesCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (editingNameValue is not { } value
            || values.Columns[e.ColumnIndex].DataPropertyName != nameof(AttributeDefinition.Name))
        {
            editingNameValue = null;
            return;
        }

        var originalDefault = GetDefaultDisplayText(editingNameOriginalName, value.Value);
        if (string.IsNullOrWhiteSpace(value.DisplayText)
            || string.Equals(value.DisplayText, originalDefault, StringComparison.Ordinal))
        {
            value.DisplayText = GetDefaultDisplayText(value.Name, value.Value);
            valueBinding.ResetBindings(false);
            MarkDirty();
        }

        editingNameValue = null;
        editingNameOriginalName = "";
    }

    private void ValuesEditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
    {
        if (!selectAllEditingText || e.Control is not TextBox textBox)
        {
            return;
        }

        textBox.BeginInvoke(new Action(textBox.SelectAll));
        selectAllEditingText = false;
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

    private void BeginEditName(AttributeDefinition value)
    {
        foreach (DataGridViewRow row in values.Rows)
        {
            if (!ReferenceEquals(row.DataBoundItem, value))
            {
                continue;
            }

            var nameColumn = values.Columns[nameof(AttributeDefinition.Name)];
            if (nameColumn is null)
            {
                return;
            }

            values.CurrentCell = row.Cells[nameColumn.Index];
            selectAllEditingText = true;
            values.BeginEdit(true);
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

    private void ResetValueRemapBaseline()
    {
        valueRemaps.Clear();
        foreach (var list in attributeLists)
        {
            var map = new Dictionary<int, int?>();
            foreach (var value in list.Values)
            {
                map[value.Value] = value.Value;
            }

            valueRemaps[list.Id] = map;
        }
    }

    private Dictionary<string, Dictionary<int, int?>> GetChangedValueRemaps()
    {
        var changed = new Dictionary<string, Dictionary<int, int?>>();
        foreach (var (listId, valueRemap) in valueRemaps)
        {
            var changedValues = valueRemap
                .Where(pair => pair.Value != pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            if (changedValues.Count > 0)
            {
                changed[listId] = changedValues;
            }
        }

        return changed;
    }

    private void ComposeValueRemap(
        string listId,
        IReadOnlyDictionary<AttributeDefinition, int> oldValues,
        AttributeDefinition deletedValue,
        int deletedAttributeValue)
    {
        var currentToNew = new Dictionary<int, int?>
        {
            [deletedAttributeValue] = null
        };

        foreach (var (definition, oldValue) in oldValues)
        {
            if (ReferenceEquals(definition, deletedValue))
            {
                continue;
            }

            currentToNew[oldValue] = definition.Value;
        }

        ComposeValueRemap(listId, currentToNew);
    }

    private void ComposeValueRemap(string listId, IReadOnlyDictionary<int, int?> currentToNew)
    {
        if (!valueRemaps.TryGetValue(listId, out var valueRemap))
        {
            return;
        }

        foreach (var originalValue in valueRemap.Keys.ToArray())
        {
            var currentValue = valueRemap[originalValue];
            if (!currentValue.HasValue)
            {
                continue;
            }

            if (currentToNew.TryGetValue(currentValue.Value, out var newValue))
            {
                valueRemap[originalValue] = newValue;
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
        if (!isDirty)
        {
            return true;
        }

        if (saveHandler is null)
        {
            isDirty = false;
            ResetValueRemapBaseline();
            return true;
        }

        if (!saveHandler(AttributeLists, GetChangedValueRemaps()))
        {
            return false;
        }

        isDirty = false;
        ResetValueRemapBaseline();
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

    private static int GetNextAttributeValue(AttributeListDefinition list)
    {
        var maxValue = list.Values
            .Where(value => !IsDefaultAttribute(value))
            .Select(value => value.Value)
            .DefaultIfEmpty(2)
            .Max();
        return Math.Max(3, maxValue + 1);
    }

    private static void NormalizeAttributeValues(AttributeListDefinition list)
    {
        var nextValue = 3;
        foreach (var value in list.Values
            .Where(value => !IsDefaultAttribute(value))
            .OrderBy(value => value.Value)
            .ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray())
        {
            value.Value = nextValue++;
        }

        list.Values.Sort((left, right) => left.Value.CompareTo(right.Value));
    }

    private static string GetDefaultDisplayText(string name, int value)
    {
        var trimmed = name.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? value.ToString()
            : trimmed[0].ToString().ToUpperInvariant();
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
