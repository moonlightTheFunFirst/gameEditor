namespace GameEditor;

public sealed class MainForm : Form
{
    private const int InitialTileSize = 32;

    private readonly ToolStripStatusLabel statusLabel = new();
    private readonly MapViewport mapViewport = new();
    private readonly TilePaletteControl tilePalette = new();
    private readonly MapDocument mapDocument = new(40, 30, InitialTileSize);
    private readonly ListView properties = new();

    private TileSet? tileSet;

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
        tilePalette.SelectedTileChanged += (_, _) => UpdateSelectedTile();
        mapViewport.TilePlaced += (_, point) => statusLabel.Text = $"配置: ({point.X}, {point.Y}) / Tile {mapViewport.SelectedTileId}";
        Load += (_, _) => LoadSampleTileset();

        mapViewport.Document = mapDocument;
        statusLabel.Text = "Ready";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            tileSet?.Dispose();
        }

        base.Dispose(disposing);
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();

        var fileMenu = new ToolStripMenuItem("ファイル");
        fileMenu.DropDownItems.Add("新規プロジェクト");
        fileMenu.DropDownItems.Add("開く");
        fileMenu.DropDownItems.Add("保存");
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
        toolStrip.Items.Add(new ToolStripButton("保存"));
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

        panel.Controls.Add(tilePalette);
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

    private void LoadSampleTileset()
    {
        try
        {
            var path = ResolveResourcePath("resources", "image", "sample.bmp");
            tileSet = TileSet.Load(path, InitialTileSize);
            tilePalette.TileSet = tileSet;
            mapViewport.TileSet = tileSet;
            UpdateSelectedTile();
            RefreshProperties();
            statusLabel.Text = $"Tileset loaded: {tileSet.Columns}x{tileSet.Rows}, {tileSet.TileSize}px";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Tileset load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "Tileset load failed";
        }
    }

    private void UpdateSelectedTile()
    {
        mapViewport.SelectedTileId = tilePalette.SelectedTileId;
        RefreshProperties();

        if (tileSet is not null)
        {
            statusLabel.Text = $"選択中: Tile {tilePalette.SelectedTileId}";
        }
    }

    private void RefreshProperties()
    {
        properties.Items.Clear();
        properties.Items.Add(new ListViewItem(new[] { "エディタ", "マップ" }));
        properties.Items.Add(new ListViewItem(new[] { "マップサイズ", $"{mapDocument.Width}x{mapDocument.Height}" }));
        properties.Items.Add(new ListViewItem(new[] { "チップサイズ", $"{mapDocument.TileSize}x{mapDocument.TileSize}" }));
        properties.Items.Add(new ListViewItem(new[] { "レイヤー", "1" }));
        properties.Items.Add(new ListViewItem(new[] { "選択チップ", tilePalette.SelectedTileId.ToString() }));

        if (tileSet is not null)
        {
            properties.Items.Add(new ListViewItem(new[] { "タイルセット", $"{tileSet.Columns}x{tileSet.Rows}" }));
            properties.Items.Add(new ListViewItem(new[] { "画像", Path.GetFileName(tileSet.SourcePath) }));
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
