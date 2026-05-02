namespace GameEditor;

public sealed class MainForm : Form
{
    private const int InitialTileSize = 32;
    private static readonly Color AdvancedTransparentColor = Color.Magenta;

    private readonly ToolStripStatusLabel statusLabel = new();
    private readonly MapViewport mapViewport = new();
    private readonly TabControl tileSetTabs = new();
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
    private readonly MapEditHistory editHistory = new();

    private readonly List<TileSet> tileSets = [];
    private MapDocument mapDocument = new(40, 30, InitialTileSize);
    private TilePaletteControl? activePalette;
    private string? currentMapPath;

    public MainForm()
    {
        Text = "gameEditor";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1024, 640);
        ClientSize = new Size(1280, 800);

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
        mapViewport.EditApplied += (_, args) =>
        {
            var layer = args.LayerKind is null ? "" : $" / {GetKindName(args.LayerKind.Value)}";
            statusLabel.Text = $"{GetToolName(args.Tool)}: ({args.Cell.X}, {args.Cell.Y}){layer} / {args.AffectedTiles} tiles";
        };
        mapViewport.EditCommandCommitted += (_, command) =>
        {
            editHistory.Push(command);
            UpdateUndoRedoState();
        };
        Load += (_, _) => LoadSampleTileSets();

        mapViewport.Document = mapDocument;
        mapViewport.SecondaryEditTool = MapEditTool.Eraser;
        SetEditTool(MapEditTool.Pen);
        UpdateUndoRedoState();
        statusLabel.Text = "Ready";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var tileSet in tileSets)
            {
                tileSet.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();

        var fileMenu = new ToolStripMenuItem("ファイル");
        fileMenu.DropDownItems.Add("新規プロジェクト");
        fileMenu.DropDownItems.Add("開く", null, (_, _) => OpenMap());
        fileMenu.DropDownItems.Add("保存", null, (_, _) => SaveMap());
        fileMenu.DropDownItems.Add("名前を付けて保存", null, (_, _) => SaveMapAs());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("終了", null, (_, _) => Close());

        var editorMenu = new ToolStripMenuItem("エディタ");
        editorMenu.DropDownItems.Add("マップエディター");
        editorMenu.DropDownItems.Add("アニメーションエディター");
        editorMenu.DropDownItems.Add("エフェクトエディター");
        editorMenu.DropDownItems.Add("コリジョンエディター");

        undoMenuItem.ShortcutKeys = Keys.Control | Keys.Z;
        undoMenuItem.Click += (_, _) => UndoMapEdit();
        redoMenuItem.ShortcutKeys = Keys.Control | Keys.Y;
        redoMenuItem.Click += (_, _) => RedoMapEdit();

        var editMenu = new ToolStripMenuItem("編集");
        editMenu.DropDownItems.Add(undoMenuItem);
        editMenu.DropDownItems.Add(redoMenuItem);

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

        toolStrip.Items.Add(new ToolStripButton("新規"));
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
        var rootSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            SplitterWidth = 6,
            SplitterDistance = 260
        };

        rootSplit.Panel1.Controls.Add(BuildTilePanel());
        rootSplit.Panel2.Controls.Add(BuildEditorArea());

        return rootSplit;
    }

    private Control BuildTilePanel()
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
            Text = "マップチップ",
            TextAlign = ContentAlignment.MiddleLeft
        };

        ConfigureTileSetTabs();
        panel.Controls.Add(tileSetTabs);
        panel.Controls.Add(title);

        return panel;
    }

    private Control BuildEditorArea()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel2,
            SplitterWidth = 6,
            SplitterDistance = 760
        };

        split.Panel1.Controls.Add(mapViewport);
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

    private void LoadSampleTileSets()
    {
        try
        {
            const string baseImagePath = "resources/image/base_tiles.bmp";
            const string advancedImagePath = "resources/image/advanced_tiles.bmp";

            ReplaceTileSets(
            [
                TileSet.Load(
                    0,
                    "base",
                    "ベース",
                    TileSetKind.Base,
                    ResolveResourcePath(baseImagePath),
                    baseImagePath,
                    InitialTileSize),
                TileSet.Load(
                    1,
                    "advanced",
                    "アドバンス",
                    TileSetKind.Advanced,
                    ResolveResourcePath(advancedImagePath),
                    advancedImagePath,
                    InitialTileSize,
                    AdvancedTransparentColor)
            ]);

            RefreshProperties();
            statusLabel.Text = "Tilesets loaded";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Tileset load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "Tileset load failed";
        }
    }

    private void UpdateSelectedTile()
    {
        if (activePalette?.TileSet is not { } tileSet)
        {
            return;
        }

        mapViewport.SelectedTileSet = tileSet;
        mapViewport.SelectedTileId = activePalette.SelectedTileId;
        RefreshProperties();

        statusLabel.Text = $"選択中: {GetKindName(tileSet.Kind)} Tile {activePalette.SelectedTileId}";
    }

    private void UpdateActivePalette()
    {
        activePalette = tileSetTabs.SelectedIndex == 1 ? advancedPalette : basePalette;
        UpdateSelectedTile();
    }

    private void RefreshProperties()
    {
        var tileSet = activePalette?.TileSet;
        var selectedTileId = activePalette?.SelectedTileId ?? -1;

        properties.Items.Clear();
        properties.Items.Add(new ListViewItem(new[] { "エディタ", "マップ" }));
        properties.Items.Add(new ListViewItem(new[] { "マップサイズ", $"{mapDocument.Width}x{mapDocument.Height}" }));
        properties.Items.Add(new ListViewItem(new[] { "チップサイズ", $"{mapDocument.TileSize}x{mapDocument.TileSize}" }));
        properties.Items.Add(new ListViewItem(new[] { "編集レイヤー", tileSet is null ? "未読み込み" : GetKindName(tileSet.Kind) }));
        properties.Items.Add(new ListViewItem(new[] { "左クリック", GetToolName(mapViewport.EditTool) }));
        properties.Items.Add(new ListViewItem(new[] { "右クリック", GetToolName(mapViewport.SecondaryEditTool) }));
        properties.Items.Add(new ListViewItem(new[] { "Undo", editHistory.CanUndo ? "可" : "不可" }));
        properties.Items.Add(new ListViewItem(new[] { "Redo", editHistory.CanRedo ? "可" : "不可" }));
        properties.Items.Add(new ListViewItem(new[] { "選択チップ", selectedTileId.ToString() }));

        if (tileSet is not null)
        {
            properties.Items.Add(new ListViewItem(new[] { "タイルセット", $"{tileSet.Name} {tileSet.Columns}x{tileSet.Rows}" }));
            properties.Items.Add(new ListViewItem(new[] { "画像", tileSet.ImagePath }));
            properties.Items.Add(new ListViewItem(new[] { "透過色", tileSet.TransparentColor is null ? "なし" : "#FF00FF" }));
        }

        properties.Items.Add(new ListViewItem(new[] { "保存先", currentMapPath is null ? "未保存" : Path.GetFileName(currentMapPath) }));
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
        basePage.Controls.Add(basePalette);
        advancedPage.Controls.Add(advancedPalette);

        tileSetTabs.TabPages.Add(basePage);
        tileSetTabs.TabPages.Add(advancedPage);
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

    private ToolStripButton ConfigureToolButton(ToolStripButton button, MapEditTool tool)
    {
        button.CheckOnClick = false;
        button.Click += (_, _) => SetEditTool(tool);
        return button;
    }

    private void SetEditTool(MapEditTool tool)
    {
        mapViewport.EditTool = tool;
        UpdateToolButtonChecks();
        RefreshProperties();
        statusLabel.Text = $"ツール: {GetToolName(tool)}";
    }

    private void UpdateToolButtonChecks()
    {
        penToolButton.Checked = mapViewport.EditTool == MapEditTool.Pen;
        fillToolButton.Checked = mapViewport.EditTool == MapEditTool.Fill;
        eraserToolButton.Checked = mapViewport.EditTool == MapEditTool.Eraser;
        selectToolButton.Checked = mapViewport.EditTool == MapEditTool.Select;
    }

    private void UndoMapEdit()
    {
        var command = editHistory.Undo(mapDocument);
        if (command is null)
        {
            statusLabel.Text = "Undo できません";
            return;
        }

        mapViewport.Invalidate();
        UpdateUndoRedoState();
        statusLabel.Text = $"Undo: {command.Name}";
    }

    private void RedoMapEdit()
    {
        var command = editHistory.Redo(mapDocument);
        if (command is null)
        {
            statusLabel.Text = "Redo できません";
            return;
        }

        mapViewport.Invalidate();
        UpdateUndoRedoState();
        statusLabel.Text = $"Redo: {command.Name}";
    }

    private void UpdateUndoRedoState()
    {
        undoButton.Enabled = editHistory.CanUndo;
        redoButton.Enabled = editHistory.CanRedo;
        undoMenuItem.Enabled = editHistory.CanUndo;
        redoMenuItem.Enabled = editHistory.CanRedo;
        RefreshProperties();
    }

    private void SaveMap()
    {
        if (currentMapPath is null)
        {
            SaveMapAs();
            return;
        }

        SaveMapTo(currentMapPath);
    }

    private void SaveMapAs()
    {
        using var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = "gemap.json",
            FileName = "untitled.gemap.json",
            Filter = "gameEditor map (*.gemap.json)|*.gemap.json|JSON (*.json)|*.json|All files (*.*)|*.*",
            Title = "マップを保存"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        currentMapPath = dialog.FileName;
        SaveMapTo(currentMapPath);
    }

    private void SaveMapTo(string path)
    {
        if (tileSets.Count == 0)
        {
            MessageBox.Show(this, "タイルセットが読み込まれていません。", "Save error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var mapName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
            MapSerializer.Save(path, mapDocument, tileSets, string.IsNullOrWhiteSpace(mapName) ? "Untitled" : mapName);
            statusLabel.Text = $"保存しました: {Path.GetFileName(path)}";
            RefreshProperties();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Save error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "Save failed";
        }
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

            mapDocument = loaded.Document;
            mapViewport.Document = mapDocument;
            ReplaceTileSets(loaded.TileSets);
            editHistory.Clear();
            UpdateUndoRedoState();

            currentMapPath = path;
            Text = $"gameEditor - {loaded.MapName}";
            statusLabel.Text = $"読み込みました: {Path.GetFileName(path)}";
            RefreshProperties();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "Load failed";
        }
    }

    private void ReplaceTileSets(IEnumerable<TileSet> newTileSets)
    {
        foreach (var tileSet in tileSets)
        {
            tileSet.Dispose();
        }

        tileSets.Clear();
        tileSets.AddRange(newTileSets);

        basePalette.TileSet = tileSets.FirstOrDefault(tileSet => tileSet.Kind == TileSetKind.Base);
        advancedPalette.TileSet = tileSets.FirstOrDefault(tileSet => tileSet.Kind == TileSetKind.Advanced);
        mapViewport.TileSets = tileSets;
        activePalette = tileSetTabs.SelectedIndex == 1 ? advancedPalette : basePalette;
        UpdateActivePalette();
        editHistory.Clear();
        UpdateUndoRedoState();
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
