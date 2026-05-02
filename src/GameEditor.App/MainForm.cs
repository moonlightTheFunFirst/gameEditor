namespace GameEditor;

public sealed class MainForm : Form
{
    private readonly ToolStripStatusLabel statusLabel = new();
    private readonly MapViewport mapViewport = new();

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
        statusLabel.Text = "Ready";
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

        var list = new ListBox
        {
            Dock = DockStyle.Fill
        };
        list.Items.Add("Tileset: 未読み込み");

        panel.Controls.Add(list);
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

        var properties = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true
        };
        properties.Columns.Add("項目", 120);
        properties.Columns.Add("値", 160);
        properties.Items.Add(new ListViewItem(new[] { "エディタ", "マップ" }));
        properties.Items.Add(new ListViewItem(new[] { "サイズ", "未設定" }));
        properties.Items.Add(new ListViewItem(new[] { "レイヤー", "未設定" }));

        panel.Controls.Add(properties);
        panel.Controls.Add(title);

        return panel;
    }
}
