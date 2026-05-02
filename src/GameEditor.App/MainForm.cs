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
    private readonly MapDocument mapDocument = new(40, 30, InitialTileSize);
    private readonly ListView properties = new();

    private readonly List<TileSet> tileSets = [];
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
        mapViewport.TilePlaced += (_, point) =>
        {
            var tileSet = mapViewport.SelectedTileSet;
            statusLabel.Text = tileSet is null
                ? $"配置: ({point.X}, {point.Y})"
                : $"配置: ({point.X}, {point.Y}) / {GetKindName(tileSet.Kind)} Tile {mapViewport.SelectedTileId}";
        };
        Load += (_, _) => LoadSampleTileSets();

        mapViewport.Document = mapDocument;
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
        fileMenu.DropDownItems.Add("開く");
        fileMenu.DropDownItems.Add("保存", null, (_, _) => SaveMap());
        fileMenu.DropDownItems.Add("名前を付けて保存", null, (_, _) => SaveMapAs());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("終了", null, (_, _) => Close());

        var editorMenu = new ToolStripMenuItem("エディタ");
        editorMenu.DropDownItems.Add("マップエディター");
        editorMenu.DropDownItems.Add("アニメーションエディター");
        editorMenu.DropDownItems.Add("エフェクトエディター");
        editorMenu.DropDownItems.Add("コリジョンエディター");

        var viewMenu = new ToolStripMenuItem("表示");
        viewMenu.DropDownItems.Add("グリッド");
        viewMenu.DropDownItems.Add("ズームリセット");

        menu.Items.Add(fileMenu);
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
        toolStrip.Items.Add(new ToolStripButton("開く"));
        toolStrip.Items.Add(new ToolStripButton("保存", null, (_, _) => SaveMap()));
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripButton("ペン"));
        toolStrip.Items.Add(new ToolStripButton("消しゴム"));
        toolStrip.Items.Add(new ToolStripButton("選択"));

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
            tileSets.Clear();

            const string baseImagePath = "resources/image/base_tiles.bmp";
            const string advancedImagePath = "resources/image/advanced_tiles.bmp";

            tileSets.Add(TileSet.Load(
                0,
                "base",
                "ベース",
                TileSetKind.Base,
                ResolveResourcePath(baseImagePath),
                baseImagePath,
                InitialTileSize));

            tileSets.Add(TileSet.Load(
                1,
                "advanced",
                "アドバンス",
                TileSetKind.Advanced,
                ResolveResourcePath(advancedImagePath),
                advancedImagePath,
                InitialTileSize,
                AdvancedTransparentColor));

            basePalette.TileSet = tileSets[0];
            advancedPalette.TileSet = tileSets[1];
            mapViewport.TileSets = tileSets;
            activePalette = basePalette;
            UpdateActivePalette();
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
