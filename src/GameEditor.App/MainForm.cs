namespace GameEditor;

public sealed class MainForm : Form
{
    private const int InitialTileSize = 32;
    private const int TileSetColumns = 8;
    private const int TilePanelPadding = 8;
    private const int TilePanelExtraWidth = 10;
    private const int TilePanelInitialWidth = (InitialTileSize * TileSetColumns) + (TilePanelPadding * 2) + TilePanelExtraWidth;
    private const int WmSetRedraw = 0x000B;
    private static readonly Color EmptyWorkspaceColor = Color.FromArgb(44, 46, 50);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    private readonly ToolStripStatusLabel statusLabel = new();
    private readonly SplitContainer workspaceSplit = new();
    private readonly DocumentTabControl mapTabs = new();
    private readonly Panel emptyMapPanel = new();
    private readonly TabControl tileSetTabs = new();
    private readonly ComboBox baseTileSetSelector = new();
    private readonly ComboBox advancedTileSetSelector = new();
    private readonly TilePaletteControl basePalette = new();
    private readonly TilePaletteControl advancedPalette = new();
    private readonly ListView properties = new();
    private readonly ToolStripButton penToolButton = new("ペン");
    private readonly ToolStripButton fillToolButton = new("塗りつぶし");
    private readonly ToolStripButton eraserToolButton = new("消しゴム");
    private readonly ToolStripButton selectToolButton = new("選択");
    private readonly ToolStripButton undoButton = new("元に戻す");
    private readonly ToolStripButton redoButton = new("やり直し");
    private readonly ToolStripMenuItem undoMenuItem = new("元に戻す");
    private readonly ToolStripMenuItem redoMenuItem = new("やり直し");
    private readonly ToolStripMenuItem closeMapMenuItem = new("閉じる");
    private readonly ContextMenuStrip mapTabContextMenu = new();
    private readonly ToolStripMenuItem closeMapTabMenuItem = new("閉じる");

    private TilePaletteControl? activePalette;
    private MapEditTool currentEditTool = MapEditTool.Pen;
    private MapEditTool currentSecondaryEditTool = MapEditTool.Eraser;
    private readonly List<TileSetDefinition> baseTileSetDefinitions = [];
    private readonly List<TileSetDefinition> advancedTileSetDefinitions = [];
    private TileSet? previewBaseTileSet;
    private TileSet? previewAdvancedTileSet;
    private bool updatingTileSetSelectors;
    private bool suppressDocumentActivation;

    public MainForm()
    {
        Text = "gameEditor";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1024, 640);
        ClientSize = new Size(1280, 800);
        LoadTileSetDefinitions();

        var menu = BuildMenu();
        var toolStrip = BuildToolStrip();
        var statusStrip = BuildStatusStrip();
        var workspace = BuildWorkspace();

        Controls.Add(workspace);
        Controls.Add(statusStrip);
        Controls.Add(toolStrip);
        Controls.Add(menu);

        MainMenuStrip = menu;
        basePalette.SelectedTileChanged += (_, _) => UpdateSelectedTile();
        advancedPalette.SelectedTileChanged += (_, _) => UpdateSelectedTile();
        tileSetTabs.SelectedIndexChanged += (_, _) => UpdateActivePalette();
        mapTabs.SelectedIndexChanged += (_, _) =>
        {
            if (!suppressDocumentActivation)
            {
                ActivateCurrentDocument();
            }
        };
        mapTabs.MouseUp += ShowMapTabContextMenu;
        baseTileSetSelector.SelectedIndexChanged += (_, _) => ChangeActiveTileSet(TileSetKind.Base);
        advancedTileSetSelector.SelectedIndexChanged += (_, _) => ChangeActiveTileSet(TileSetKind.Advanced);
        Shown += (_, _) => ApplyInitialTilePanelWidth();

        ConfigureEmptyMapPanel();
        SetDefaultPaletteTileSets();
        SetEditTool(MapEditTool.Pen);
        UpdateMapWorkspaceState();
        UpdateDocumentActionsState();
        statusLabel.Text = "Ready";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var document in EnumerateDocuments())
            {
                document.Dispose();
            }

            previewBaseTileSet?.Dispose();
            previewAdvancedTileSet?.Dispose();
            mapTabContextMenu.Dispose();
        }

        base.Dispose(disposing);
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();

        var fileMenu = new ToolStripMenuItem("ファイル");
        fileMenu.DropDownItems.Add("新規マップ", null, (_, _) => NewMap());
        fileMenu.DropDownItems.Add("開く", null, (_, _) => OpenMap());
        fileMenu.DropDownItems.Add("保存", null, (_, _) => SaveMap());
        fileMenu.DropDownItems.Add("名前を付けて保存", null, (_, _) => SaveMapAs());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("終了", null, (_, _) => Close());

        undoMenuItem.ShortcutKeys = Keys.Control | Keys.Z;
        undoMenuItem.Click += (_, _) => UndoMapEdit();
        redoMenuItem.ShortcutKeys = Keys.Control | Keys.Y;
        redoMenuItem.Click += (_, _) => RedoMapEdit();
        closeMapMenuItem.Click += (_, _) => CloseCurrentMap();

        var editMenu = new ToolStripMenuItem("編集");
        editMenu.DropDownItems.Add(undoMenuItem);
        editMenu.DropDownItems.Add(redoMenuItem);
        editMenu.DropDownItems.Add(new ToolStripSeparator());
        editMenu.DropDownItems.Add(closeMapMenuItem);

        var editorMenu = new ToolStripMenuItem("エディタ");
        editorMenu.DropDownItems.Add("マップエディター");
        editorMenu.DropDownItems.Add("アニメーションエディター");
        editorMenu.DropDownItems.Add("エフェクトエディター");
        editorMenu.DropDownItems.Add("コリジョンエディター");

        var viewMenu = new ToolStripMenuItem("表示");
        viewMenu.DropDownItems.Add("グリッド");
        viewMenu.DropDownItems.Add("ズームリセット");

        menu.Items.Add(fileMenu);
        menu.Items.Add(editMenu);
        menu.Items.Add(editorMenu);
        menu.Items.Add(viewMenu);

        return menu;
    }

    private ToolStrip BuildToolStrip()
    {
        var toolStrip = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            Dock = DockStyle.Top
        };

        toolStrip.Items.Add(new ToolStripButton("新規", null, (_, _) => NewMap()));
        toolStrip.Items.Add(new ToolStripButton("開く", null, (_, _) => OpenMap()));
        toolStrip.Items.Add(new ToolStripButton("保存", null, (_, _) => SaveMap()));
        toolStrip.Items.Add(new ToolStripSeparator());

        undoButton.Click += (_, _) => UndoMapEdit();
        redoButton.Click += (_, _) => RedoMapEdit();
        toolStrip.Items.Add(undoButton);
        toolStrip.Items.Add(redoButton);
        toolStrip.Items.Add(new ToolStripSeparator());

        toolStrip.Items.Add(ConfigureToolButton(penToolButton, MapEditTool.Pen));
        toolStrip.Items.Add(ConfigureToolButton(fillToolButton, MapEditTool.Fill));
        toolStrip.Items.Add(ConfigureToolButton(eraserToolButton, MapEditTool.Eraser));
        toolStrip.Items.Add(ConfigureToolButton(selectToolButton, MapEditTool.Select));

        return toolStrip;
    }

    private StatusStrip BuildStatusStrip()
    {
        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(statusLabel);
        return statusStrip;
    }

    private Control BuildWorkspace()
    {
        workspaceSplit.Dock = DockStyle.Fill;
        workspaceSplit.FixedPanel = FixedPanel.Panel1;
        workspaceSplit.Panel1MinSize = TilePanelInitialWidth;
        workspaceSplit.SplitterWidth = 6;
        workspaceSplit.SplitterDistance = TilePanelInitialWidth;

        workspaceSplit.Panel1.Controls.Add(BuildTilePanel());
        workspaceSplit.Panel2.Controls.Add(BuildEditorArea());

        return workspaceSplit;
    }

    private void ConfigureMapTabContextMenu()
    {
        if (mapTabContextMenu.Items.Count > 0)
        {
            return;
        }

        closeMapTabMenuItem.Click += (_, _) => CloseCurrentMap();
        mapTabContextMenu.Items.Add(closeMapTabMenuItem);
    }

    private Control BuildTilePanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(TilePanelPadding)
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = "マップチップ",
            TextAlign = ContentAlignment.MiddleLeft
        };

        ConfigureTileSetTabs();
        panel.Controls.Add(tileSetTabs);
        panel.Controls.Add(title);

        return panel;
    }

    private void ApplyInitialTilePanelWidth()
    {
        if (workspaceSplit.Width <= TilePanelInitialWidth)
        {
            return;
        }

        workspaceSplit.Panel1MinSize = TilePanelInitialWidth;
        workspaceSplit.SplitterDistance = TilePanelInitialWidth;
    }

    private Control BuildEditorArea()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            BackColor = SystemColors.Control,
            FixedPanel = FixedPanel.Panel2,
            SplitterWidth = 6,
            SplitterDistance = 760
        };
        split.Panel1.BackColor = SystemColors.Control;
        split.Panel2.BackColor = SystemColors.Control;

        var mapHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SystemColors.Control
        };

        ConfigureMapTabContextMenu();
        mapTabs.Dock = DockStyle.Fill;
        mapTabs.HeaderBackColor = SystemColors.Control;
        mapTabs.PageBackColor = SystemColors.Control;
        mapTabs.Visible = false;
        emptyMapPanel.Dock = DockStyle.Fill;
        emptyMapPanel.Visible = true;
        mapHost.Controls.Add(mapTabs);
        mapHost.Controls.Add(emptyMapPanel);
        split.Panel1.Controls.Add(mapHost);
        split.Panel2.Controls.Add(BuildPropertyPanel());

        return split;
    }

    private Control BuildPropertyPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };

        var title = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = "プロパティ",
            TextAlign = ContentAlignment.MiddleLeft
        };

        properties.Dock = DockStyle.Fill;
        properties.View = View.Details;
        properties.FullRowSelect = true;
        properties.Columns.Add("項目", 120);
        properties.Columns.Add("値", 160);
        RefreshProperties();

        panel.Controls.Add(properties);
        panel.Controls.Add(title);

        return panel;
    }

    private void LoadTileSetDefinitions()
    {
        var definitions = TileSetCatalog.Load();
        baseTileSetDefinitions.Clear();
        advancedTileSetDefinitions.Clear();

        baseTileSetDefinitions.AddRange(definitions.Where(definition => definition.Kind == TileSetKind.Base));
        advancedTileSetDefinitions.AddRange(definitions.Where(definition => definition.Kind == TileSetKind.Advanced));
    }

    private void ConfigureTileSetTabs()
    {
        if (tileSetTabs.TabPages.Count > 0)
        {
            return;
        }

        tileSetTabs.Dock = DockStyle.Fill;

        var basePage = new TabPage("ベース");
        var advancedPage = new TabPage("アドバンス");
        basePage.Controls.Add(BuildTileSetPage(baseTileSetSelector, basePalette, baseTileSetDefinitions));
        advancedPage.Controls.Add(BuildTileSetPage(advancedTileSetSelector, advancedPalette, advancedTileSetDefinitions));

        tileSetTabs.TabPages.Add(basePage);
        tileSetTabs.TabPages.Add(advancedPage);
    }

    private static Control BuildTileSetPage(
        ComboBox selector,
        TilePaletteControl palette,
        IReadOnlyList<TileSetDefinition> definitions)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill
        };

        selector.Dock = DockStyle.Top;
        selector.DropDownStyle = ComboBoxStyle.DropDownList;
        selector.IntegralHeight = false;
        selector.Height = 28;
        selector.DisplayMember = nameof(TileSetDefinition.Name);
        selector.ValueMember = nameof(TileSetDefinition.Id);
        selector.Items.Clear();

        foreach (var definition in definitions)
        {
            selector.Items.Add(definition);
        }

        if (selector.Items.Count > 0)
        {
            selector.SelectedIndex = 0;
        }

        palette.Dock = DockStyle.Fill;
        panel.Controls.Add(palette);
        panel.Controls.Add(selector);
        return panel;
    }

    private void NewMap()
    {
        using var dialog = new NewMapDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        List<TileSet> tileSets;
        try
        {
            tileSets = CreateActiveTileSets(dialog.TileSize);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Tileset load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var document = new MapEditorDocument(
            dialog.MapName,
            new MapDocument(dialog.MapWidth, dialog.MapHeight, dialog.TileSize),
            tileSets);
        AddDocumentTab(document);
        statusLabel.Text = $"新規マップ: {document.Name}";
    }

    private void OpenMap()
    {
        using var dialog = new OpenFileDialog
        {
            AddExtension = true,
            DefaultExt = "gemap.json",
            Filter = "gameEditor map (*.gemap.json)|*.gemap.json|JSON (*.json)|*.json|All files (*.*)|*.*",
            Title = "マップを開く"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        LoadMapFrom(dialog.FileName);
    }

    private void LoadMapFrom(string path)
    {
        try
        {
            var loaded = MapSerializer.Load(path);
            var document = new MapEditorDocument(loaded.MapName, loaded.Document, loaded.TileSets)
            {
                FilePath = path
            };
            ApplyCatalogTileSets(document, markDirty: false);

            AddDocumentTab(document);
            statusLabel.Text = $"読み込みました: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "Load failed";
        }
    }

    private void AddDocumentTab(MapEditorDocument document)
    {
        ConfigureViewport(document);

        var page = new TabPage
        {
            Text = document.Name,
            Tag = document,
            BackColor = SystemColors.Control,
            Padding = Padding.Empty,
            UseVisualStyleBackColor = false
        };
        page.Controls.Add(document.Viewport);
        mapTabs.TabPages.Add(page);
        mapTabs.SelectedTab = page;
        UpdateMapWorkspaceState();
        ActivateCurrentDocument();
    }

    private void ConfigureViewport(MapEditorDocument document)
    {
        document.Viewport.EditTool = currentEditTool;
        document.Viewport.SecondaryEditTool = currentSecondaryEditTool;
        document.Viewport.EditApplied += (_, args) =>
        {
            var layer = args.LayerKind is null ? "" : $" / {GetKindName(args.LayerKind.Value)}";
            statusLabel.Text = $"{GetToolName(args.Tool)}: ({args.Cell.X}, {args.Cell.Y}){layer} / {args.AffectedTiles} tiles";
        };
        document.Viewport.EditCommandCommitted += (_, command) =>
        {
            document.History.Push(command);
            document.IsDirty = true;
            UpdateDocumentTabTitle(document);
            UpdateDocumentActionsState();
        };
    }

    private void ActivateCurrentDocument()
    {
        var document = CurrentDocument;
        if (document is null)
        {
            UpdateTileSetSelectorsForDocument(null);
            SetDefaultPaletteTileSets();
            activePalette = tileSetTabs.SelectedIndex == 1 ? advancedPalette : basePalette;
            Text = "gameEditor";
            UpdateMapWorkspaceState();
            UpdateDocumentActionsState();
            RefreshProperties();
            return;
        }

        UpdateTileSetSelectorsForDocument(document);
        basePalette.TileSet = document.GetTileSet(TileSetKind.Base);
        advancedPalette.TileSet = document.GetTileSet(TileSetKind.Advanced);
        activePalette = tileSetTabs.SelectedIndex == 1 ? advancedPalette : basePalette;
        document.Viewport.TileSets = document.TileSets;
        SyncCurrentViewportSelection();
        Text = $"gameEditor - {document.Name}";
        UpdateMapWorkspaceState();
        UpdateDocumentActionsState();
        RefreshProperties();
    }

    private void UpdateActivePalette()
    {
        activePalette = tileSetTabs.SelectedIndex == 1 ? advancedPalette : basePalette;
        UpdateSelectedTile();
    }

    private void UpdateSelectedTile()
    {
        SyncCurrentViewportSelection();

        if (activePalette?.TileSet is { } tileSet)
        {
            statusLabel.Text = $"選択中: {GetKindName(tileSet.Kind)} Tile {activePalette.SelectedTileId}";
        }

        RefreshProperties();
    }

    private void SyncCurrentViewportSelection()
    {
        var viewport = CurrentDocument?.Viewport;
        if (viewport is null || activePalette?.TileSet is not { } tileSet)
        {
            return;
        }

        viewport.SelectedTileSet = tileSet;
        viewport.SelectedTileId = activePalette.SelectedTileId;
        viewport.EditTool = currentEditTool;
        viewport.SecondaryEditTool = currentSecondaryEditTool;
    }

    private void ShowMapTabContextMenu(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var tabIndex = GetMapTabIndexAt(e.Location);
        if (tabIndex < 0)
        {
            return;
        }

        mapTabs.SelectedIndex = tabIndex;
        closeMapTabMenuItem.Enabled = CurrentDocument is not null;
        mapTabContextMenu.Show(mapTabs, e.Location);
    }

    private int GetMapTabIndexAt(Point location)
    {
        for (var i = 0; i < mapTabs.TabPages.Count; i++)
        {
            if (mapTabs.GetTabRect(i).Contains(location))
            {
                return i;
            }
        }

        return -1;
    }

    private void ChangeActiveTileSet(TileSetKind kind)
    {
        if (updatingTileSetSelectors)
        {
            return;
        }

        var definition = GetSelectedTileSetDefinition(kind);
        if (definition is null)
        {
            return;
        }

        var document = CurrentDocument;
        if (document is null)
        {
            SetPreviewTileSet(kind, definition);
            activePalette = tileSetTabs.SelectedIndex == 1 ? advancedPalette : basePalette;
            UpdateSelectedTile();
            return;
        }

        try
        {
            ReplaceDocumentTileSet(document, definition, markDirty: true);
            SetDocumentPalette(kind, document.GetTileSet(kind));
            SyncCurrentViewportSelection();
            UpdateDocumentActionsState();
            statusLabel.Text = $"Tileset: {definition.Name}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Tileset load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private List<TileSet> CreateActiveTileSets(int tileSize)
    {
        var baseDefinition = GetSelectedTileSetDefinition(TileSetKind.Base)
            ?? throw new InvalidOperationException("Base tileset is not loaded.");
        var advancedDefinition = GetSelectedTileSetDefinition(TileSetKind.Advanced)
            ?? throw new InvalidOperationException("Advanced tileset is not loaded.");

        return
        [
            CreateTileSet(baseDefinition, GetTileSetIndex(TileSetKind.Base), tileSize),
            CreateTileSet(advancedDefinition, GetTileSetIndex(TileSetKind.Advanced), tileSize)
        ];
    }

    private void ApplyCatalogTileSets(MapEditorDocument document, bool markDirty)
    {
        foreach (var kind in new[] { TileSetKind.Base, TileSetKind.Advanced })
        {
            var current = document.GetTileSet(kind);
            var definition = FindDefinitionForTileSet(current, kind);
            if (definition is not null)
            {
                ReplaceDocumentTileSet(document, definition, markDirty);
            }
        }
    }

    private void ReplaceDocumentTileSet(MapEditorDocument document, TileSetDefinition definition, bool markDirty)
    {
        var replacement = CreateTileSet(definition, GetTileSetIndex(definition.Kind), document.Map.TileSize);
        document.ReplaceTileSet(definition.Kind, replacement);

        if (markDirty)
        {
            document.IsDirty = true;
            UpdateDocumentTabTitle(document);
        }
    }

    private void UpdateTileSetSelectorsForDocument(MapEditorDocument? document)
    {
        updatingTileSetSelectors = true;
        try
        {
            SelectDefinition(baseTileSetSelector, document is null
                ? GetSelectedTileSetDefinition(TileSetKind.Base)
                : FindDefinitionForTileSet(document.GetTileSet(TileSetKind.Base), TileSetKind.Base),
                fallbackToFirst: document is null);
            SelectDefinition(advancedTileSetSelector, document is null
                ? GetSelectedTileSetDefinition(TileSetKind.Advanced)
                : FindDefinitionForTileSet(document.GetTileSet(TileSetKind.Advanced), TileSetKind.Advanced),
                fallbackToFirst: document is null);
        }
        finally
        {
            updatingTileSetSelectors = false;
        }
    }

    private static void SelectDefinition(ComboBox selector, TileSetDefinition? definition, bool fallbackToFirst)
    {
        if (definition is null)
        {
            selector.SelectedIndex = fallbackToFirst && selector.Items.Count > 0 ? 0 : -1;
            return;
        }

        for (var i = 0; i < selector.Items.Count; i++)
        {
            if (selector.Items[i] is TileSetDefinition item && item.Id == definition.Id)
            {
                selector.SelectedIndex = i;
                return;
            }
        }

        selector.SelectedIndex = fallbackToFirst && selector.Items.Count > 0 ? 0 : -1;
    }

    private void SetDefaultPaletteTileSets()
    {
        var baseDefinition = GetSelectedTileSetDefinition(TileSetKind.Base);
        var advancedDefinition = GetSelectedTileSetDefinition(TileSetKind.Advanced);

        if (baseDefinition is not null)
        {
            SetPreviewTileSet(TileSetKind.Base, baseDefinition);
        }
        else
        {
            basePalette.TileSet = null;
        }

        if (advancedDefinition is not null)
        {
            SetPreviewTileSet(TileSetKind.Advanced, advancedDefinition);
        }
        else
        {
            advancedPalette.TileSet = null;
        }

        activePalette = tileSetTabs.SelectedIndex == 1 ? advancedPalette : basePalette;
    }

    private void SetPreviewTileSet(TileSetKind kind, TileSetDefinition definition)
    {
        var replacement = CreateTileSet(definition, GetTileSetIndex(kind), InitialTileSize);

        if (kind == TileSetKind.Base)
        {
            previewBaseTileSet?.Dispose();
            previewBaseTileSet = replacement;
            basePalette.TileSet = previewBaseTileSet;
        }
        else
        {
            previewAdvancedTileSet?.Dispose();
            previewAdvancedTileSet = replacement;
            advancedPalette.TileSet = previewAdvancedTileSet;
        }
    }

    private void SetDocumentPalette(TileSetKind kind, TileSet? tileSet)
    {
        if (kind == TileSetKind.Base)
        {
            basePalette.TileSet = tileSet;
        }
        else
        {
            advancedPalette.TileSet = tileSet;
        }
    }

    private TileSetDefinition? GetSelectedTileSetDefinition(TileSetKind kind)
    {
        var selector = kind == TileSetKind.Base ? baseTileSetSelector : advancedTileSetSelector;
        if (selector.SelectedItem is TileSetDefinition selected)
        {
            return selected;
        }

        return kind == TileSetKind.Base
            ? baseTileSetDefinitions.FirstOrDefault()
            : advancedTileSetDefinitions.FirstOrDefault();
    }

    private TileSetDefinition? FindDefinitionForTileSet(TileSet? tileSet, TileSetKind kind)
    {
        if (tileSet is null)
        {
            return null;
        }

        var definitions = kind == TileSetKind.Base ? baseTileSetDefinitions : advancedTileSetDefinitions;
        return definitions.FirstOrDefault(definition =>
            string.Equals(definition.ImagePath, tileSet.ImagePath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileName(definition.ImagePath), Path.GetFileName(tileSet.ImagePath), StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFullPath(definition.SourcePath), Path.GetFullPath(tileSet.SourcePath), StringComparison.OrdinalIgnoreCase));
    }

    private static TileSet CreateTileSet(TileSetDefinition definition, int index, int tileSize)
    {
        return TileSet.Load(
            index,
            GetTileSetId(definition.Kind),
            definition.Name,
            definition.Kind,
            definition.SourcePath,
            definition.ImagePath,
            tileSize,
            definition.TransparentColor);
    }

    private static int GetTileSetIndex(TileSetKind kind)
    {
        return kind == TileSetKind.Base ? 0 : 1;
    }

    private static string GetTileSetId(TileSetKind kind)
    {
        return kind == TileSetKind.Base ? "base" : "advanced";
    }

    private void SaveMap()
    {
        var document = CurrentDocument;
        if (document is null)
        {
            statusLabel.Text = "保存するマップがありません";
            return;
        }

        if (document.FilePath is null)
        {
            SaveMapAs();
            return;
        }

        SaveMapTo(document, document.FilePath);
    }

    private void SaveMapAs()
    {
        var document = CurrentDocument;
        if (document is null)
        {
            statusLabel.Text = "保存するマップがありません";
            return;
        }

        using var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = "gemap.json",
            FileName = $"{document.Name}.gemap.json",
            Filter = "gameEditor map (*.gemap.json)|*.gemap.json|JSON (*.json)|*.json|All files (*.*)|*.*",
            Title = "マップを保存"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        document.FilePath = dialog.FileName;
        SaveMapTo(document, document.FilePath);
    }

    private void SaveMapTo(MapEditorDocument document, string path)
    {
        try
        {
            MapSerializer.Save(path, document.Map, document.TileSets, document.Name);
            document.IsDirty = false;
            UpdateDocumentTabTitle(document);
            statusLabel.Text = $"保存しました: {Path.GetFileName(path)}";
            RefreshProperties();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Save error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "Save failed";
        }
    }

    private void CloseCurrentMap()
    {
        var page = mapTabs.SelectedTab;
        if (page is null || page.Tag is not MapEditorDocument document)
        {
            statusLabel.Text = "閉じるマップがありません";
            return;
        }

        var closedName = document.Name;
        var closingIndex = mapTabs.SelectedIndex;
        var nextIndex = mapTabs.TabPages.Count > 1
            ? Math.Min(closingIndex, mapTabs.TabPages.Count - 2)
            : -1;

        suppressDocumentActivation = true;
        SetRedraw(mapTabs, enabled: false);
        mapTabs.SuspendLayout();
        try
        {
            mapTabs.TabPages.Remove(page);
            document.Dispose();
            page.Tag = null;
            page.Dispose();

            if (nextIndex >= 0)
            {
                mapTabs.SelectedIndex = nextIndex;
            }
        }
        finally
        {
            suppressDocumentActivation = false;
            ActivateCurrentDocument();
            mapTabs.ResumeLayout(performLayout: true);
            SetRedraw(mapTabs, enabled: true);
        }

        statusLabel.Text = $"閉じました: {closedName}";
    }

    private static void SetRedraw(Control control, bool enabled)
    {
        if (!control.IsHandleCreated)
        {
            return;
        }

        SendMessage(control.Handle, WmSetRedraw, new IntPtr(enabled ? 1 : 0), IntPtr.Zero);

        if (enabled)
        {
            control.Invalidate(invalidateChildren: true);
            control.Update();
        }
    }

    private void UndoMapEdit()
    {
        var document = CurrentDocument;
        if (document is null)
        {
            statusLabel.Text = "Undo できません";
            return;
        }

        var command = document.History.Undo(document.Map);
        if (command is null)
        {
            statusLabel.Text = "Undo できません";
            return;
        }

        document.IsDirty = true;
        document.Viewport.Invalidate();
        UpdateDocumentTabTitle(document);
        UpdateDocumentActionsState();
        statusLabel.Text = $"Undo: {command.Name}";
    }

    private void RedoMapEdit()
    {
        var document = CurrentDocument;
        if (document is null)
        {
            statusLabel.Text = "Redo できません";
            return;
        }

        var command = document.History.Redo(document.Map);
        if (command is null)
        {
            statusLabel.Text = "Redo できません";
            return;
        }

        document.IsDirty = true;
        document.Viewport.Invalidate();
        UpdateDocumentTabTitle(document);
        UpdateDocumentActionsState();
        statusLabel.Text = $"Redo: {command.Name}";
    }

    private void SetEditTool(MapEditTool tool)
    {
        currentEditTool = tool;

        foreach (var document in EnumerateDocuments())
        {
            document.Viewport.EditTool = currentEditTool;
        }

        UpdateToolButtonChecks();
        RefreshProperties();
        statusLabel.Text = $"ツール: {GetToolName(tool)}";
    }

    private void UpdateToolButtonChecks()
    {
        penToolButton.Checked = currentEditTool == MapEditTool.Pen;
        fillToolButton.Checked = currentEditTool == MapEditTool.Fill;
        eraserToolButton.Checked = currentEditTool == MapEditTool.Eraser;
        selectToolButton.Checked = currentEditTool == MapEditTool.Select;
    }

    private ToolStripButton ConfigureToolButton(ToolStripButton button, MapEditTool tool)
    {
        button.CheckOnClick = false;
        button.Click += (_, _) => SetEditTool(tool);
        return button;
    }

    private void UpdateDocumentActionsState()
    {
        var document = CurrentDocument;
        var hasDocument = document is not null;
        undoButton.Enabled = document?.History.CanUndo == true;
        redoButton.Enabled = document?.History.CanRedo == true;
        undoMenuItem.Enabled = undoButton.Enabled;
        redoMenuItem.Enabled = redoButton.Enabled;
        closeMapMenuItem.Enabled = hasDocument;
        closeMapTabMenuItem.Enabled = hasDocument;
        penToolButton.Enabled = hasDocument;
        fillToolButton.Enabled = hasDocument;
        eraserToolButton.Enabled = hasDocument;
        selectToolButton.Enabled = hasDocument;
        RefreshProperties();
    }

    private void RefreshProperties()
    {
        var document = CurrentDocument;
        var tileSet = activePalette?.TileSet;
        var selectedTileId = activePalette?.SelectedTileId ?? -1;

        properties.Items.Clear();
        properties.Items.Add(new ListViewItem(new[] { "エディタ", "マップ" }));

        if (document is null)
        {
            properties.Items.Add(new ListViewItem(new[] { "マップ", "未選択" }));
            properties.Items.Add(new ListViewItem(new[] { "左クリック", GetToolName(currentEditTool) }));
            properties.Items.Add(new ListViewItem(new[] { "右クリック", GetToolName(currentSecondaryEditTool) }));
            return;
        }

        properties.Items.Add(new ListViewItem(new[] { "マップ名", document.Name }));
        properties.Items.Add(new ListViewItem(new[] { "マップサイズ", $"{document.Map.Width}x{document.Map.Height}" }));
        properties.Items.Add(new ListViewItem(new[] { "チップサイズ", $"{document.Map.TileSize}x{document.Map.TileSize}" }));
        properties.Items.Add(new ListViewItem(new[] { "編集レイヤー", tileSet is null ? "未読み込み" : GetKindName(tileSet.Kind) }));
        properties.Items.Add(new ListViewItem(new[] { "左クリック", GetToolName(currentEditTool) }));
        properties.Items.Add(new ListViewItem(new[] { "右クリック", GetToolName(currentSecondaryEditTool) }));
        properties.Items.Add(new ListViewItem(new[] { "Undo", document.History.CanUndo ? "可" : "不可" }));
        properties.Items.Add(new ListViewItem(new[] { "Redo", document.History.CanRedo ? "可" : "不可" }));
        properties.Items.Add(new ListViewItem(new[] { "選択チップ", selectedTileId.ToString() }));

        if (tileSet is not null)
        {
            properties.Items.Add(new ListViewItem(new[] { "タイルセット", $"{tileSet.Name} {tileSet.Columns}x{tileSet.Rows}" }));
            properties.Items.Add(new ListViewItem(new[] { "画像", tileSet.ImagePath }));
            properties.Items.Add(new ListViewItem(new[] { "透過色", tileSet.TransparentColor is null ? "なし" : "#FF00FF" }));
        }

        properties.Items.Add(new ListViewItem(new[] { "保存先", document.FilePath is null ? "未保存" : Path.GetFileName(document.FilePath) }));
    }

    private void UpdateDocumentTabTitle(MapEditorDocument document)
    {
        foreach (TabPage page in mapTabs.TabPages)
        {
            if (ReferenceEquals(page.Tag, document))
            {
                page.Text = document.IsDirty ? $"{document.Name}*" : document.Name;
                return;
            }
        }
    }

    private IEnumerable<MapEditorDocument> EnumerateDocuments()
    {
        foreach (TabPage page in mapTabs.TabPages)
        {
            if (page.Tag is MapEditorDocument document)
            {
                yield return document;
            }
        }
    }

    private MapEditorDocument? CurrentDocument => mapTabs.SelectedTab?.Tag as MapEditorDocument;

    private void ConfigureEmptyMapPanel()
    {
        emptyMapPanel.BackColor = EmptyWorkspaceColor;

        var message = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = "マップがありません",
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(168, 174, 184),
            BackColor = EmptyWorkspaceColor
        };

        emptyMapPanel.Controls.Add(message);
    }

    private void UpdateMapWorkspaceState()
    {
        var hasDocument = mapTabs.TabPages.Count > 0;
        mapTabs.Visible = hasDocument;
        emptyMapPanel.Visible = !hasDocument;
        if (hasDocument)
        {
            mapTabs.BringToFront();
        }
        else
        {
            emptyMapPanel.BringToFront();
        }
    }

    private static string GetKindName(TileSetKind kind)
    {
        return kind == TileSetKind.Base ? "ベース" : "アドバンス";
    }

    private static string GetToolName(MapEditTool tool)
    {
        return tool switch
        {
            MapEditTool.Pen => "ペン",
            MapEditTool.Fill => "塗りつぶし",
            MapEditTool.Eraser => "消しゴム",
            MapEditTool.Select => "選択",
            _ => tool.ToString()
        };
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.Z))
        {
            UndoMapEdit();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Y) || keyData == (Keys.Control | Keys.Shift | Keys.Z))
        {
            RedoMapEdit();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static string ResolveResourcePath(params string[] segments)
    {
        var candidates = new List<string>
        {
            Path.Combine(new[] { AppContext.BaseDirectory }.Concat(segments).ToArray()),
            Path.Combine(new[] { Environment.CurrentDirectory }.Concat(segments).ToArray())
        };

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            candidates.Add(Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray()));
            directory = directory.Parent;
        }

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Resource not found: {Path.Combine(segments)}");
    }
}
