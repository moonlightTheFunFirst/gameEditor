namespace GameEditor;

public sealed class AnimationEditorControl : UserControl
{
    private readonly ToolStrip toolStrip = new();
    private readonly ToolStripTextBox nameTextBox = new();
    private readonly NumericUpDown fpsBox = new();
    private readonly CheckBox loopCheckBox = new();
    private readonly ComboBox tileSetSelector = new();
    private readonly TilePaletteControl palette = new();
    private readonly AnimationPreviewControl preview = new();
    private readonly DataGridView timeline = new();
    private readonly TextBox eventTextBox = new();
    private readonly Label documentLabel = new();
    private readonly System.Windows.Forms.Timer playbackTimer = new();
    private readonly List<AnimationTileSetOption> tileSetOptions = [];

    private AnimationDocument? document;
    private int selectedFrameIndex;
    private bool updatingUi;
    private bool playing;

    public AnimationEditorControl()
    {
        Dock = DockStyle.Fill;
        BackColor = SystemColors.Control;
        BuildLayout();
        LoadTileSets();
        playbackTimer.Tick += (_, _) => AdvancePlayback();
        NewSampleAnimation();
    }

    public void NewSampleAnimation()
    {
        var option = GetSelectedTileSetOption() ?? tileSetOptions.FirstOrDefault();
        if (option is null)
        {
            return;
        }

        var clip = new AnimationClip
        {
            Name = "sample_walk",
            Fps = 8,
            Loop = true
        };
        for (var i = 0; i < Math.Min(4, option.TileSet.TileCount); i++)
        {
            clip.Frames.Add(new AnimationFrameKey
            {
                TileSetId = option.TileSet.Id,
                TileId = i
            });
        }

        clip.Events.Add(new AnimationEventKey { TimeMs = clip.FrameDurationMs, Name = "footstep" });
        clip.Events.Add(new AnimationEventKey { TimeMs = clip.FrameDurationMs * 3, Name = "footstep" });
        AnimationSerializer.NormalizeFrameTimes(clip);
        LoadDocument(new AnimationDocument(clip));
    }

    public void OpenAnimationFromDialog(IWin32Window owner)
    {
        using var dialog = new OpenFileDialog
        {
            AddExtension = true,
            DefaultExt = "geanim.json",
            Filter = "gameEditor animation (*.geanim.json)|*.geanim.json|JSON (*.json)|*.json|All files (*.*)|*.*",
            Title = "アニメーションを開く"
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return;
        }

        var loaded = AnimationSerializer.Load(dialog.FileName);
        LoadDocument(loaded);
    }

    public bool SaveAnimation(IWin32Window owner)
    {
        if (document is null)
        {
            return false;
        }

        return document.FilePath is null
            ? SaveAnimationAs(owner)
            : SaveAnimationTo(owner, document.FilePath);
    }

    public bool SaveAnimationAs(IWin32Window owner)
    {
        if (document is null)
        {
            return false;
        }

        using var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = "geanim.json",
            FileName = $"{document.Clip.Name}.geanim.json",
            Filter = "gameEditor animation (*.geanim.json)|*.geanim.json|JSON (*.json)|*.json|All files (*.*)|*.*",
            Title = "アニメーションを保存"
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
        {
            return false;
        }

        document.FilePath = dialog.FileName;
        return SaveAnimationTo(owner, document.FilePath);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            playbackTimer.Dispose();
            foreach (var option in tileSetOptions)
            {
                option.TileSet.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        toolStrip.GripStyle = ToolStripGripStyle.Hidden;
        toolStrip.Dock = DockStyle.Top;
        toolStrip.Items.Add(new ToolStripButton("新規サンプル", null, (_, _) => NewSampleAnimation()));
        toolStrip.Items.Add(new ToolStripButton("開く", null, (_, _) => RunWithErrorDialog(() => OpenAnimationFromDialog(this))));
        toolStrip.Items.Add(new ToolStripButton("保存", null, (_, _) => RunWithErrorDialog(() => SaveAnimation(this))));
        toolStrip.Items.Add(new ToolStripButton("名前を付けて保存", null, (_, _) => RunWithErrorDialog(() => SaveAnimationAs(this))));
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripButton("再生", null, (_, _) => StartPlayback()));
        toolStrip.Items.Add(new ToolStripButton("停止", null, (_, _) => StopPlayback()));
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripLabel("名前:"));
        nameTextBox.AutoSize = false;
        nameTextBox.Width = 150;
        nameTextBox.TextChanged += (_, _) => UpdateClipName();
        toolStrip.Items.Add(nameTextBox);
        toolStrip.Items.Add(new ToolStripLabel("FPS:"));
        fpsBox.Minimum = 1;
        fpsBox.Maximum = 60;
        fpsBox.Width = 54;
        fpsBox.ValueChanged += (_, _) => UpdateFps();
        toolStrip.Items.Add(new ToolStripControlHost(fpsBox));
        loopCheckBox.Text = "ループ";
        loopCheckBox.CheckedChanged += (_, _) => UpdateLoop();
        toolStrip.Items.Add(new ToolStripControlHost(loopCheckBox));

        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            FixedPanel = FixedPanel.Panel1,
            SplitterDistance = 300,
            SplitterWidth = 6
        };

        mainSplit.Panel1.Controls.Add(BuildPalettePanel());
        mainSplit.Panel2.Controls.Add(BuildClipPanel());

        Controls.Add(mainSplit);
        Controls.Add(toolStrip);
    }

    private Control BuildPalettePanel()
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
            Text = "素材",
            TextAlign = ContentAlignment.MiddleLeft
        };

        tileSetSelector.Dock = DockStyle.Top;
        tileSetSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        tileSetSelector.SelectedIndexChanged += (_, _) => SelectTileSet();
        palette.SelectedTileChanged += (_, _) => UpdateSelectedTileStatus();

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        buttonPanel.Controls.Add(new Button { Text = "フレーム追加", AutoSize = true });
        buttonPanel.Controls[0].Click += (_, _) => AddFrameFromSelection();
        buttonPanel.Controls.Add(new Button { Text = "差し込み", AutoSize = true });
        buttonPanel.Controls[1].Click += (_, _) => InsertFrameFromSelection();

        panel.Controls.Add(palette);
        panel.Controls.Add(buttonPanel);
        panel.Controls.Add(tileSetSelector);
        panel.Controls.Add(title);
        return panel;
    }

    private Control BuildClipPanel()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 330,
            SplitterWidth = 6
        };

        var previewPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        documentLabel.Dock = DockStyle.Top;
        documentLabel.Height = 24;
        documentLabel.TextAlign = ContentAlignment.MiddleLeft;
        previewPanel.Controls.Add(preview);
        previewPanel.Controls.Add(documentLabel);

        var timelinePanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        var frameToolStrip = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            Dock = DockStyle.Top
        };
        frameToolStrip.Items.Add(new ToolStripButton("削除", null, (_, _) => DeleteSelectedFrame()));
        frameToolStrip.Items.Add(new ToolStripButton("前へ", null, (_, _) => MoveSelectedFrame(-1)));
        frameToolStrip.Items.Add(new ToolStripButton("次へ", null, (_, _) => MoveSelectedFrame(1)));
        frameToolStrip.Items.Add(new ToolStripSeparator());
        frameToolStrip.Items.Add(new ToolStripLabel("イベント:"));
        eventTextBox.Width = 180;
        eventTextBox.TextChanged += (_, _) => UpdateSelectedFrameEvent();
        frameToolStrip.Items.Add(new ToolStripControlHost(eventTextBox));

        timeline.Dock = DockStyle.Fill;
        timeline.AllowUserToAddRows = false;
        timeline.AllowUserToDeleteRows = false;
        timeline.AllowUserToResizeRows = false;
        timeline.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        timeline.BackgroundColor = SystemColors.Window;
        timeline.MultiSelect = false;
        timeline.ReadOnly = true;
        timeline.RowHeadersVisible = false;
        timeline.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        timeline.Columns.Add("index", "#");
        timeline.Columns.Add("time", "Time");
        timeline.Columns.Add("tileSet", "TileSet");
        timeline.Columns.Add("tile", "Tile");
        timeline.Columns.Add("event", "Event");
        timeline.SelectionChanged += (_, _) => SelectTimelineFrame();

        timelinePanel.Controls.Add(timeline);
        timelinePanel.Controls.Add(frameToolStrip);
        split.Panel1.Controls.Add(previewPanel);
        split.Panel2.Controls.Add(timelinePanel);
        return split;
    }

    private void LoadTileSets()
    {
        foreach (var option in tileSetOptions)
        {
            option.TileSet.Dispose();
        }

        tileSetOptions.Clear();
        var samplePath = AnimationSampleAssets.EnsureSampleSpriteSheet();
        var sample = TileSet.Load(
            0,
            "sample_actor",
            "Sample Actor",
            TileSetKind.Advanced,
            samplePath,
            AnimationSampleAssets.SampleImagePath,
            32,
            Color.Magenta);
        tileSetOptions.Add(new AnimationTileSetOption(sample));

        tileSetSelector.DataSource = null;
        tileSetSelector.DataSource = tileSetOptions;
        if (tileSetOptions.Count > 0)
        {
            tileSetSelector.SelectedIndex = 0;
        }
    }

    private void SelectTileSet()
    {
        var option = GetSelectedTileSetOption();
        palette.TileSet = option?.TileSet;
    }

    private void LoadDocument(AnimationDocument value)
    {
        StopPlayback();
        document = value;
        selectedFrameIndex = 0;
        RefreshDocumentUi();
        RefreshTimeline();
        SelectFrame(0);
    }

    private void RefreshDocumentUi()
    {
        if (document is null)
        {
            return;
        }

        updatingUi = true;
        nameTextBox.Text = document.Clip.Name;
        fpsBox.Value = Math.Clamp(document.Clip.Fps, (int)fpsBox.Minimum, (int)fpsBox.Maximum);
        loopCheckBox.Checked = document.Clip.Loop;
        documentLabel.Text = GetDocumentLabel();
        updatingUi = false;
    }

    private void RefreshTimeline()
    {
        if (document is null)
        {
            return;
        }

        updatingUi = true;
        timeline.Rows.Clear();
        for (var i = 0; i < document.Clip.Frames.Count; i++)
        {
            var frame = document.Clip.Frames[i];
            timeline.Rows.Add(
                i + 1,
                $"{frame.TimeMs} ms",
                frame.TileSetId,
                frame.TileId,
                GetEventNameAt(frame.TimeMs));
        }

        updatingUi = false;
    }

    private void SelectTimelineFrame()
    {
        if (updatingUi || timeline.CurrentRow is null)
        {
            return;
        }

        SelectFrame(timeline.CurrentRow.Index);
    }

    private void SelectFrame(int index)
    {
        if (document is null || document.Clip.Frames.Count == 0)
        {
            return;
        }

        selectedFrameIndex = Math.Clamp(index, 0, document.Clip.Frames.Count - 1);
        if (timeline.Rows.Count > selectedFrameIndex && !timeline.Rows[selectedFrameIndex].Selected)
        {
            updatingUi = true;
            timeline.ClearSelection();
            timeline.Rows[selectedFrameIndex].Selected = true;
            timeline.CurrentCell = timeline.Rows[selectedFrameIndex].Cells[0];
            updatingUi = false;
        }

        var frame = document.Clip.Frames[selectedFrameIndex];
        var option = FindTileSetOption(frame.TileSetId) ?? GetSelectedTileSetOption();
        preview.TileSet = option?.TileSet;
        preview.TileId = frame.TileId;
        preview.EventName = GetEventNameAt(frame.TimeMs);
        updatingUi = true;
        eventTextBox.Text = preview.EventName;
        updatingUi = false;
        documentLabel.Text = GetDocumentLabel();
    }

    private void AddFrameFromSelection()
    {
        AddFrameFromSelection(insert: false);
    }

    private void InsertFrameFromSelection()
    {
        AddFrameFromSelection(insert: true);
    }

    private void AddFrameFromSelection(bool insert)
    {
        if (document is null || GetSelectedTileSetOption() is not { } option || palette.SelectedTileId < 0)
        {
            return;
        }

        var frame = new AnimationFrameKey
        {
            TileSetId = option.TileSet.Id,
            TileId = palette.SelectedTileId
        };

        var insertIndex = insert
            ? selectedFrameIndex
            : Math.Min(document.Clip.Frames.Count, selectedFrameIndex + 1);
        document.Clip.Frames.Insert(insertIndex, frame);
        NormalizeAndRefresh();
        MarkDirty();
        SelectFrame(insertIndex);
    }

    private void DeleteSelectedFrame()
    {
        if (document is null || document.Clip.Frames.Count <= 1)
        {
            return;
        }

        var eventNames = GetFrameEventNames();
        document.Clip.Frames.RemoveAt(selectedFrameIndex);
        eventNames.RemoveAt(selectedFrameIndex);
        ApplyFrameEventNames(eventNames);
        NormalizeAndRefresh();
        MarkDirty();
        SelectFrame(Math.Min(selectedFrameIndex, document.Clip.Frames.Count - 1));
    }

    private void MoveSelectedFrame(int delta)
    {
        if (document is null)
        {
            return;
        }

        var target = selectedFrameIndex + delta;
        if (target < 0 || target >= document.Clip.Frames.Count)
        {
            return;
        }

        var eventNames = GetFrameEventNames();
        (document.Clip.Frames[selectedFrameIndex], document.Clip.Frames[target]) =
            (document.Clip.Frames[target], document.Clip.Frames[selectedFrameIndex]);
        (eventNames[selectedFrameIndex], eventNames[target]) = (eventNames[target], eventNames[selectedFrameIndex]);
        ApplyFrameEventNames(eventNames);
        NormalizeAndRefresh();
        MarkDirty();
        SelectFrame(target);
    }

    private void UpdateSelectedFrameEvent()
    {
        if (updatingUi || document is null || selectedFrameIndex < 0 || selectedFrameIndex >= document.Clip.Frames.Count)
        {
            return;
        }

        var frame = document.Clip.Frames[selectedFrameIndex];
        document.Clip.Events.RemoveAll(animationEvent => animationEvent.TimeMs == frame.TimeMs);
        if (!string.IsNullOrWhiteSpace(eventTextBox.Text))
        {
            document.Clip.Events.Add(new AnimationEventKey
            {
                TimeMs = frame.TimeMs,
                Name = eventTextBox.Text.Trim()
            });
        }

        RefreshTimeline();
        SelectFrame(selectedFrameIndex);
        MarkDirty();
    }

    private void UpdateClipName()
    {
        if (updatingUi || document is null)
        {
            return;
        }

        document.Clip.Name = string.IsNullOrWhiteSpace(nameTextBox.Text) ? "NewAnimation" : nameTextBox.Text.Trim();
        MarkDirty();
        documentLabel.Text = GetDocumentLabel();
    }

    private void UpdateFps()
    {
        if (updatingUi || document is null)
        {
            return;
        }

        var eventNames = GetFrameEventNames();
        document.Clip.Fps = (int)fpsBox.Value;
        ApplyFrameEventNames(eventNames);
        NormalizeAndRefresh();
        MarkDirty();
        SelectFrame(selectedFrameIndex);
    }

    private void UpdateLoop()
    {
        if (updatingUi || document is null)
        {
            return;
        }

        document.Clip.Loop = loopCheckBox.Checked;
        MarkDirty();
    }

    private void NormalizeAndRefresh()
    {
        if (document is null)
        {
            return;
        }

        AnimationSerializer.NormalizeFrameTimes(document.Clip);
        RefreshTimeline();
    }

    private void StartPlayback()
    {
        if (document is null || document.Clip.Frames.Count == 0)
        {
            return;
        }

        playing = true;
        playbackTimer.Interval = Math.Max(16, document.Clip.FrameDurationMs);
        playbackTimer.Start();
        AdvancePlayback();
        documentLabel.Text = GetDocumentLabel();
    }

    private void StopPlayback()
    {
        playing = false;
        playbackTimer.Stop();
        documentLabel.Text = GetDocumentLabel();
    }

    private void AdvancePlayback()
    {
        if (!playing || document is null || document.Clip.Frames.Count == 0)
        {
            return;
        }

        var next = selectedFrameIndex + 1;
        if (next >= document.Clip.Frames.Count)
        {
            if (!document.Clip.Loop)
            {
                StopPlayback();
                return;
            }

            next = 0;
        }

        SelectFrame(next);
    }

    private void UpdateSelectedTileStatus()
    {
        if (GetSelectedTileSetOption() is not { } option || palette.SelectedTileId < 0)
        {
            return;
        }

        documentLabel.Text = $"{GetDocumentLabel()} / selected tile: {option.TileSet.Name} #{palette.SelectedTileId}";
    }

    private bool SaveAnimationTo(IWin32Window owner, string path)
    {
        if (document is null)
        {
            return false;
        }

        try
        {
            AnimationSerializer.Save(path, document);
            document.FilePath = path;
            document.IsDirty = false;
            documentLabel.Text = GetDocumentLabel();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, ex.Message, "Animation save error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void RunWithErrorDialog(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Animation editor error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void MarkDirty()
    {
        if (document is null)
        {
            return;
        }

        document.IsDirty = true;
        documentLabel.Text = GetDocumentLabel();
    }

    private string GetDocumentLabel()
    {
        if (document is null)
        {
            return "Animation";
        }

        var dirty = document.IsDirty ? "*" : "";
        var playback = playing ? "  playing" : "";
        return $"{document.Clip.Name}{dirty}{playback}  frame={selectedFrameIndex + 1}/{document.Clip.Frames.Count}  duration={document.Clip.DurationMs}ms";
    }

    private List<string> GetFrameEventNames()
    {
        if (document is null)
        {
            return [];
        }

        return document.Clip.Frames
            .Select(frame => GetEventNameAt(frame.TimeMs))
            .ToList();
    }

    private void ApplyFrameEventNames(IReadOnlyList<string> eventNames)
    {
        if (document is null)
        {
            return;
        }

        document.Clip.Events.Clear();
        for (var i = 0; i < eventNames.Count && i < document.Clip.Frames.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(eventNames[i]))
            {
                continue;
            }

            document.Clip.Events.Add(new AnimationEventKey
            {
                TimeMs = i * document.Clip.FrameDurationMs,
                Name = eventNames[i]
            });
        }
    }

    private string GetEventNameAt(int timeMs)
    {
        return document?.Clip.Events.FirstOrDefault(animationEvent => animationEvent.TimeMs == timeMs)?.Name ?? "";
    }

    private AnimationTileSetOption? GetSelectedTileSetOption()
    {
        return tileSetSelector.SelectedItem as AnimationTileSetOption;
    }

    private AnimationTileSetOption? FindTileSetOption(string tileSetId)
    {
        return tileSetOptions.FirstOrDefault(option => string.Equals(option.TileSet.Id, tileSetId, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class AnimationTileSetOption(TileSet tileSet)
    {
        public TileSet TileSet { get; } = tileSet;

        public override string ToString()
        {
            return TileSet.Name;
        }
    }
}
