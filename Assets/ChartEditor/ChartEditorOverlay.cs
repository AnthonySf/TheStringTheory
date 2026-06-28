using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using UnityEngine.Networking;

public sealed class ChartEditorOverlay
{
    private enum ChartEditorScreen
    {
        Startup,
        ImportSummary,
        Editor
    }

    private enum ChartEditorMode
    {
        SyncTiming,
        Notes,
        Sections,
        SongInfo
    }

    private enum ChartEditorCursorKind
    {
        ResizeHorizontal,
        ResizeVertical
    }

    private struct WaveformRenderRange
    {
        public float left;
        public float width;
        public double startSeconds;
        public double endSeconds;

        public bool IsValid => width > 1f && endSeconds > startSeconds;
    }

    private sealed class ChartEditorTrackViewGroup
    {
        public string key;
        public int sourceIndex;
        public Color color;
        public readonly List<ChartEditorTrack> tracks = new List<ChartEditorTrack>();
        public ChartEditorTrack activeTrack;

        public bool ContainsSelected(string selectedTrackId)
        {
            return !string.IsNullOrWhiteSpace(selectedTrackId) &&
                   tracks.Any(track => track != null && string.Equals(track.id, selectedTrackId, StringComparison.OrdinalIgnoreCase));
        }

        public bool Visible => tracks.Any(track => track != null && track.visible);

        public int NoteCount => activeTrack?.notes?.Count ?? 0;
    }

    private sealed class WaveformVectorElement : VisualElement
    {
        private const int MaxColumnsPerMesh = 5000;
        private readonly ChartEditorOverlay owner;
        private float originPixels;

        public WaveformVectorElement(ChartEditorOverlay owner)
        {
            this.owner = owner;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += GenerateWaveform;
        }

        public void SetViewport(float originPixels, float width)
        {
            this.originPixels = Mathf.Max(0f, originPixels);
            style.left = this.originPixels;
            style.width = Mathf.Max(1f, width);
            MarkDirtyRepaint();
        }

        private void GenerateWaveform(MeshGenerationContext context)
        {
            ChartEditorWaveformData data = owner?.waveformData;
            if (owner == null || data == null || !data.IsValid)
                return;

            float height = Mathf.Max(1f, contentRect.height);
            float width = Mathf.Max(1f, contentRect.width);
            float center = (height - 1f) * 0.5f;
            float maxHalf = height * 0.43f;
            int xStart = 0;
            int xEnd = Mathf.CeilToInt(width);
            if (xEnd <= xStart)
                return;

            AddRect(context, 0f, center - 0.5f, width, 1f, new Color(0.18f, 0.24f, 0.34f, 0.32f));

            Color peakColor = new Color(0.20f, 0.38f, 0.58f, 0.23f);
            Color bodyColor = new Color(0.42f, 0.24f, 0.70f, 0.90f);
            Color coreColor = new Color(0.66f, 0.40f, 0.91f, 0.96f);

            for (int chunkStart = xStart; chunkStart < xEnd; chunkStart += MaxColumnsPerMesh)
            {
                int chunkEnd = Mathf.Min(xEnd, chunkStart + MaxColumnsPerMesh);
                int columns = chunkEnd - chunkStart;
                MeshWriteData mesh = context.Allocate(columns * 12, columns * 18);
                ushort vertexBase = 0;

                for (int x = chunkStart; x < chunkEnd; x++)
                {
                    float timelineX = originPixels + x;
                    double sampleStart = owner.PixelsToSeconds(timelineX);
                    double sampleEnd = owner.PixelsToSeconds(timelineX + 1f);
                    ChartEditorWaveformRenderer.SampleRange(data, sampleStart, sampleEnd, out float positive, out float negative, out float rms);

                    float peak = Mathf.Max(Mathf.Abs(positive), Mathf.Abs(negative));
                    float bodyHeight = Mathf.Lerp(0.75f, maxHalf * 0.72f, Mathf.Pow(Mathf.Clamp01(rms), 0.72f));
                    float peakHeight = Mathf.Lerp(bodyHeight + 0.75f, maxHalf, Mathf.Pow(Mathf.Clamp01(peak), 0.70f));

                    AddRect(mesh, ref vertexBase, x, center - peakHeight, 1f, peakHeight * 2f, peakColor);
                    AddRect(mesh, ref vertexBase, x, center - bodyHeight, 1f, bodyHeight * 2f, bodyColor);

                    float coreHeight = Mathf.Max(0.5f, bodyHeight * 0.36f);
                    AddRect(mesh, ref vertexBase, x, center - coreHeight, 1f, coreHeight * 2f, coreColor);
                }
            }
        }

        private static void AddRect(MeshGenerationContext context, float x, float y, float width, float height, Color color)
        {
            MeshWriteData mesh = context.Allocate(4, 6);
            ushort vertexBase = 0;
            AddRect(mesh, ref vertexBase, x, y, width, height, color);
        }

        private static void AddRect(MeshWriteData mesh, ref ushort vertexBase, float x, float y, float width, float height, Color color)
        {
            Color32 tint = color;
            ushort start = vertexBase;
            mesh.SetNextVertex(new Vertex { position = new Vector3(x, y, 0f), tint = tint });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x + width, y, 0f), tint = tint });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x + width, y + height, 0f), tint = tint });
            mesh.SetNextVertex(new Vertex { position = new Vector3(x, y + height, 0f), tint = tint });
            mesh.SetNextIndex(start);
            mesh.SetNextIndex((ushort)(start + 1));
            mesh.SetNextIndex((ushort)(start + 2));
            mesh.SetNextIndex(start);
            mesh.SetNextIndex((ushort)(start + 2));
            mesh.SetNextIndex((ushort)(start + 3));
            vertexBase += 4;
        }
    }

    private sealed class ContextMenuItem
    {
        public string label;
        public Action action;
        public ContextMenuItem[] children;

        public ContextMenuItem(string label, Action action)
        {
            this.label = label;
            this.action = action;
        }

        public ContextMenuItem(string label, params ContextMenuItem[] children)
        {
            this.label = label;
            this.children = children;
        }
    }

    private sealed class ChartEditorNoteHit
    {
        public ChartEditorTrack track;
        public ChartEditorNote note;
        public Rect rect;
    }

    private sealed class ChartEditorNoteReference
    {
        public ChartEditorTrack track;
        public ChartEditorNote note;
    }

    private sealed class ChartEditorCopiedNote
    {
        public ChartEditorNote note;
        public double offsetSeconds;
    }

    private sealed class ChartEditorNoteDragStart
    {
        public ChartEditorTrack track;
        public ChartEditorNote note;
        public VisualElement block;
        public double timeSeconds;
        public double chartTimeSeconds;
        public int visualLane;
        public int laneCount;
        public float laneTop;
        public float laneHeight;
        public float noteHeight;
        public float left;
        public float top;
        public bool selectedTrack;
    }

    private sealed class BeatMarkerVisual
    {
        public string markerId;
        public double beatPosition;
        public VisualElement hit;
        public VisualElement line;
        public Label label;
    }

    private sealed class TechniqueSegmentVisual
    {
        public ChartEditorTrack track;
        public ChartEditorNote note;
        public ChartEditorTechniqueSegment segment;
        public VisualElement box;
        public int laneCount;
        public bool selectedTrack;
    }

    private sealed class TechniqueSettingsRowState
    {
        public ChartEditorTechniqueSegment segment;
        public DropdownField typeDropdown;
        public TextField startField;
        public TextField endField;
        public TextField startFretField;
        public TextField endFretField;
        public TextField startBendField;
        public TextField endBendField;
    }

    private sealed class SidebarExpansionAnimation
    {
        public bool collapsing;
    }

    private static Texture2D resizeHorizontalCursorTexture;
    private static Texture2D resizeVerticalCursorTexture;

    private static readonly Color[] LogoStringColors =
    {
        new Color(0.91f, 0.30f, 0.24f, 1f),
        new Color(0.95f, 0.77f, 0.06f, 1f),
        new Color(0.20f, 0.60f, 0.86f, 1f),
        new Color(0.90f, 0.49f, 0.13f, 1f),
        new Color(0.18f, 0.80f, 0.44f, 1f),
        new Color(0.61f, 0.35f, 0.71f, 1f)
    };

    private const float EditorFontScale = 1.35f;
    private const float SidebarWidth = 640f;
    private const float SidebarSectionMarginX = 14f;
    private const float SidebarSectionTopGap = 14f;
    private const float SidebarSectionBottomGap = 10f;
    private const float SidebarSectionHeaderHeight = 72f;
    private const float SidebarSectionContentPaddingTop = 12f;
    private const float SidebarSectionContentPaddingBottom = 14f;
    private const float SidebarSectionAnimationSeconds = 0.20f;
    private const float SidebarTrackRowHeight = 108f;
    private const float SidebarTrackRowGap = 10f;
    private const float SidebarListRowHeight = 52f;
    private const float SidebarListRowGap = 8f;
    private const int SidebarMaxSectionRows = 14;
    private const string SidebarTracksKey = "tracks";
    private const string SidebarSectionsKey = "sections";
    private const string SidebarAnchorsKey = "anchors";
    private const string SidebarProjectInfoKey = "project-info";
    private const float InspectorWidth = 700f;
    private const float TimelineLabelWidth = 250f;
    private const float SectionBarHeight = 56f;
    private const float WaveformTop = SectionBarHeight + 8f;
    private const float WaveformHeight = 220f;
    private const float WaveformRenderViewportPaddingMultiplier = 0.75f;
    private const float WaveformRenderMinimumPadding = 512f;
    private const float WaveformTextureMinimumPixelsPerLayoutPixel = 0.92f;
    private const float NotesTop = WaveformTop + WaveformHeight + 40f;
    private const float ContextMenuWidth = 520f;
    private const float ContextSubmenuWidth = 500f;
    private const float ContextMenuRowHeight = 72f;
    private const float BeatMarkerHitWidth = 28f;
    private const float AnchorPinTop = WaveformTop + WaveformHeight - 6f;
    private const float AnchorPinSize = 46f;
    private const float SelectedTrackHeight = 690f;
    private const float SelectedTrackHeaderHeight = 0f;
    private const float CompactTrackHeight = 150f;
    private const float TrackRowGap = 22f;
    private const float SelectedNoteHeight = 64f;
    private const float CompactNoteHeight = 30f;
    private const float SelectedLaneLineHeight = 5f;
    private const float CompactLaneLineHeight = 2f;
    private const float TechniqueLabelHeight = 36f;
    private const float TechniqueSegmentBoxHeight = 34f;
    private const float TechniqueSegmentBoxGap = 9f;
    private const float TechniqueSegmentVisualLaneGap = 6f;
    private const float TechniqueSegmentResizeHandleWidth = 12f;
    private const float TechniqueSegmentMinimumSeconds = 0.03f;
    private const float HalfStepBendSemitones = 1f;
    private const float FullStepBendSemitones = 2f;
    private const int TechniqueLabelSlotCount = 3;
    private const float TechniqueLabelSlotGap = 8f;
    private const float TechniqueLabelNoteGap = 10f;
    private const float SustainHandleMinSize = 10f;
    private const float SustainHandleMaxSize = 18f;
    private const float ArrowRepeatInitialDelay = 0.24f;
    private const float ArrowRepeatInterval = 0.045f;
    private const float BaseTimelinePixelsPerSecond = 380f;
    private const float MinTimelineZoom = 0.5f;
    private const float MaxTimelineZoom = 8f;
    private const float MinPlaybackSpeed = 0.25f;
    private const float MaxPlaybackSpeed = 1.50f;
    private const float FollowPlayheadTrailingMargin = 260f;
    private const float FollowPlayheadLeadingMargin = 520f;
    private const float DefaultNoteSquareWidth = SelectedNoteHeight;
    private const float NoteSpacingGap = 10f;
    private const float TimelineBottomPadding = 24f;
    private const float TimelineRightPadding = 900f;
    private const float TimelineMinSecondsWidth = 9000f;
    private const float SeekDragEdgePanZone = 120f;
    private const float SeekDragEdgePanMinPixels = 6f;
    private const float SeekDragEdgePanMaxPixels = 58f;
    private const float HighwayPreviewDefaultHeight = 780f;
    private const float HighwayPreviewMinHeight = 320f;
    private const float HighwayPreviewMinTimelineHeight = 360f;
    private const float HighwayPreviewSplitterHeight = 26f;
    private const float HighwayPreviewPlayingInterval = 1f / 24f;
    private const float HighwayPreviewIdleInterval = 0.12f;
    private const double HighwayPreviewTimeEpsilon = 0.001;

    public VisualElement RootElement { get; }

    private readonly GuitarBridgeServer owner;
    private readonly FontDefinition bodyFont;
    private readonly FontDefinition titleFont;
    private readonly VisualElement contentHost;
    private readonly Label headerTitleLabel;
    private readonly Label headerSubtitleLabel;
    private readonly Label headerTimeLabel;
    private readonly VisualElement headerProgressFill;
    private readonly Label statusLabel;
    private readonly Button saveButton;
    private readonly Button transportPlayButton;
    private Label playbackSpeedLabel;
    private Slider playbackSpeedSlider;

    private ChartEditorProject project;
    private ChartEditorScreen screen = ChartEditorScreen.Startup;
    private ChartEditorMode mode = ChartEditorMode.SyncTiming;
    private List<string> currentWarnings = new List<string>();
    private string selectedNoteId;
    private readonly HashSet<string> selectedNoteIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<ChartEditorNoteHit> currentNoteHits = new List<ChartEditorNoteHit>();
    private readonly Dictionary<string, VisualElement> currentNoteBlocks = new Dictionary<string, VisualElement>(StringComparer.OrdinalIgnoreCase);
    private readonly List<TechniqueSegmentVisual> currentTechniqueSegmentVisuals = new List<TechniqueSegmentVisual>();
    private readonly List<ChartEditorCopiedNote> noteClipboard = new List<ChartEditorCopiedNote>();
    private readonly List<BeatMarkerVisual> currentBeatMarkerVisuals = new List<BeatMarkerVisual>();
    private string selectedSectionId;
    private string selectedSyncPointId;
    private readonly HashSet<string> selectedSyncPointIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private Vector2 timelineScrollOffset;
    private bool timelineScrollInitialized;
    private bool visible;
    private VisualElement cursorElement;
    private VisualElement cursorHandleElement;
    private ScrollView currentTimelineScrollView;
    private AudioSource editorAudioSource;
    private AudioClip editorAudioClip;
    private AudioClip metronomeClickClip;
    private AudioClip metronomeDownbeatClip;
    private AudioClip noteClapClip;
    private string editorAudioPath;
    private readonly List<ChartEditorBeatMarker> auditionBeatMarkers = new List<ChartEditorBeatMarker>();
    private readonly List<ChartEditorNote> auditionNotes = new List<ChartEditorNote>();
    private int auditionBeatIndex;
    private int auditionNoteIndex;
    private string auditionTrackId;
    private bool auditionCacheValid;
    private double auditionCursorAnchorSeconds = -1.0;
    private ChartEditorWaveformData waveformData;
    private Texture2D waveformOverviewTexture;
    private int waveformOverviewTextureWidth;
    private int waveformOverviewTextureHeight;
    private Texture2D waveformTexture;
    private VisualElement waveformTextureElement;
    private WaveformVectorElement waveformVectorElement;
    private int waveformTextureWidth;
    private int waveformTextureHeight;
    private float waveformTextureTimelineLeft;
    private float waveformTextureTimelineWidth;
    private double waveformTextureStartSeconds = -1.0;
    private double waveformTextureEndSeconds = -1.0;
    private bool waveformRefreshScheduled;
    private bool waveformOverviewTextureBuildInProgress;
    private bool waveformTextureBuildInProgress;
    private int waveformCacheVersion;
    private bool openEditorWhenAudioReady;
    private bool audioLoadInProgress;
    private string audioLoadError;
    private bool editorPlaying;
    private double silentPlaybackTimeSeconds;
    private double lastAuditionTimeSeconds = -1.0;
    private KeyCode repeatingArrowKey = KeyCode.None;
    private float nextArrowRepeatTime;
    private float timelineZoom = 1f;
    private float timelineViewportWidth;
    private bool skipTimelineScrollCaptureOnce;
    private VisualElement contextMenuElement;
    private VisualElement contextSubmenuElement;
    private VisualElement editPopupElement;
    private VisualElement saveSuccessPopupElement;
    private double lastTapTempoRealtime = -1.0;
    private double tapTempoAverageIntervalSeconds;
    private bool marqueeSelecting;
    private int marqueePointerId = -1;
    private Vector2 marqueeStart;
    private bool marqueeMoved;
    private VisualElement marqueeTimeline;
    private VisualElement marqueeBox;
    private readonly Dictionary<string, SidebarExpansionAnimation> sidebarExpansionAnimations = new Dictionary<string, SidebarExpansionAnimation>(StringComparer.OrdinalIgnoreCase);
    private bool sectionsExpanded;
    private bool tracksExpanded = true;
    private bool anchorsExpanded;
    private bool projectInfoExpanded;
    private bool seekDragging;
    private bool seekWasPlaying;
    private GuitarHighway3DRenderer highwayPreviewRenderer;
    private GuitarHighway3DRenderHost highwayPreviewHost;
    private RenderTexture highwayPreviewTexture;
    private GameObject highwayPreviewCameraObject;
    private Camera highwayPreviewCamera;
    private string highwayPreviewSignature;
    private ChartEditorHighwayPreviewFrame cachedHighwayPreviewFrame;
    private List<TabSectionData> cachedHighwayPreviewTabSections = new List<TabSectionData>();
    private List<SongTimelineSectionData> cachedHighwayPreviewTimelineSections = new List<SongTimelineSectionData>();
    private string cachedHighwayPreviewSignature;
    private int highwayPreviewRevision;
    private int cachedHighwayPreviewRevision = -1;
    private float nextHighwayPreviewRenderTime;
    private double lastHighwayPreviewRenderTime = -1.0;
    private bool forceHighwayPreviewRender = true;
    private VisualElement highwayPreviewTextureElement;
    private VisualElement highwayPreviewPanelElement;
    private VisualElement chartEditorCenterElement;
    private Label highwayPreviewTitleLabel;
    private Label highwayPreviewMetaLabel;
    private float highwayPreviewPanelHeight = HighwayPreviewDefaultHeight;

    public ChartEditorOverlay(GuitarBridgeServer owner, FontDefinition bodyFont, FontDefinition titleFont)
    {
        this.owner = owner;
        this.bodyFont = bodyFont;
        this.titleFont = titleFont;

        RootElement = new VisualElement();
        RootElement.style.position = Position.Absolute;
        RootElement.style.left = 0f;
        RootElement.style.right = 0f;
        RootElement.style.top = 0f;
        RootElement.style.bottom = 0f;
        RootElement.style.backgroundColor = new Color(0.008f, 0.010f, 0.014f, 0.998f);
        RootElement.style.display = DisplayStyle.None;
        RootElement.pickingMode = PickingMode.Position;

        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.height = 112f;
        header.style.minHeight = 112f;
        header.style.paddingLeft = 28f;
        header.style.paddingRight = 28f;
        header.style.backgroundColor = new Color(0.030f, 0.036f, 0.046f, 0.985f);
        header.style.borderBottomWidth = 1f;
        header.style.borderBottomColor = new Color(0.11f, 0.15f, 0.20f, 1f);

        VisualElement brandBlock = new VisualElement();
        brandBlock.style.width = 410f;
        brandBlock.style.minWidth = 410f;
        brandBlock.style.height = 76f;
        brandBlock.style.justifyContent = Justify.Center;
        brandBlock.style.alignItems = Align.FlexStart;
        brandBlock.style.paddingRight = 28f;
        brandBlock.style.borderRightWidth = 1f;
        brandBlock.style.borderRightColor = new Color(0.10f, 0.14f, 0.20f, 1f);
        brandBlock.Add(CreateHeaderStringTheoryWordmark(30f));

        VisualElement songBlock = new VisualElement();
        songBlock.style.width = 560f;
        songBlock.style.minWidth = 440f;
        songBlock.style.paddingLeft = 34f;
        songBlock.style.paddingRight = 30f;
        songBlock.style.borderRightWidth = 1f;
        songBlock.style.borderRightColor = new Color(0.10f, 0.14f, 0.20f, 1f);
        headerTitleLabel = CreateLabel("String Theory", 31f, Color.white, true, TextAnchor.MiddleLeft, false);
        headerTitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        headerSubtitleLabel = CreateLabel(string.Empty, 22f, new Color(0.72f, 0.76f, 0.82f, 1f), false, TextAnchor.MiddleLeft, false);
        headerSubtitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        songBlock.Add(headerTitleLabel);
        songBlock.Add(headerSubtitleLabel);

        VisualElement transportBlock = new VisualElement();
        transportBlock.style.flexDirection = FlexDirection.Row;
        transportBlock.style.alignItems = Align.Center;
        transportBlock.style.justifyContent = Justify.Center;
        transportBlock.style.paddingLeft = 30f;
        transportBlock.style.paddingRight = 30f;
        transportBlock.style.borderRightWidth = 1f;
        transportBlock.style.borderRightColor = new Color(0.10f, 0.14f, 0.20f, 1f);
        transportBlock.Add(CreateHeaderIconButton("|<", () => SeekTransportTo(0.0), false));
        transportPlayButton = CreateHeaderIconButton("▶", TogglePlayback, true);
        transportBlock.Add(transportPlayButton);
        transportBlock.Add(CreateHeaderIconButton(">|", () => SeekTransportTo(project?.DurationSeconds ?? 0f), false));
        transportBlock.Add(CreateHeaderIconButton("■", StopPlayback, false));

        VisualElement speedControl = CreatePlaybackSpeedControl(out playbackSpeedLabel, out playbackSpeedSlider);
        transportBlock.Add(speedControl);

        VisualElement timeBlock = new VisualElement();
        timeBlock.style.width = 500f;
        timeBlock.style.minWidth = 420f;
        timeBlock.style.justifyContent = Justify.Center;
        timeBlock.style.paddingLeft = 38f;
        timeBlock.style.paddingRight = 38f;
        timeBlock.style.borderRightWidth = 1f;
        timeBlock.style.borderRightColor = new Color(0.10f, 0.14f, 0.20f, 1f);
        headerTimeLabel = CreateLabel("00:00.000 / 00:00.000", 32f, Color.white, true, TextAnchor.MiddleCenter, false);
        headerTimeLabel.style.whiteSpace = WhiteSpace.NoWrap;
        timeBlock.Add(headerTimeLabel);
        VisualElement progressTrack = new VisualElement();
        progressTrack.style.height = 5f;
        progressTrack.style.marginTop = 12f;
        progressTrack.style.backgroundColor = new Color(0.10f, 0.13f, 0.17f, 1f);
        progressTrack.style.borderTopLeftRadius = 2f;
        progressTrack.style.borderTopRightRadius = 2f;
        progressTrack.style.borderBottomLeftRadius = 2f;
        progressTrack.style.borderBottomRightRadius = 2f;
        headerProgressFill = new VisualElement();
        headerProgressFill.style.height = 5f;
        headerProgressFill.style.width = Length.Percent(0f);
        headerProgressFill.style.backgroundColor = new Color(0.64f, 0.35f, 1f, 1f);
        headerProgressFill.style.borderTopLeftRadius = 2f;
        headerProgressFill.style.borderTopRightRadius = 2f;
        headerProgressFill.style.borderBottomLeftRadius = 2f;
        headerProgressFill.style.borderBottomRightRadius = 2f;
        progressTrack.Add(headerProgressFill);
        timeBlock.Add(progressTrack);

        VisualElement headerActions = new VisualElement();
        headerActions.style.flexDirection = FlexDirection.Row;
        headerActions.style.alignItems = Align.Center;
        headerActions.style.justifyContent = Justify.FlexEnd;
        headerActions.style.minWidth = 620f;
        headerActions.style.flexGrow = 1f;
        headerActions.style.paddingLeft = 30f;

        saveButton = CreateHeaderActionButton("▣ Save", ShowSaveOptionsPopup, true);
        Button settingsButton = CreateHeaderIconButton("⚙", ShowSongInfoPopup, false);
        Button closeButton = CreateHeaderIconButton("☰", () => owner?.CloseChartEditorToMainMenuFromUi(), false);

        headerActions.Add(saveButton);
        headerActions.Add(settingsButton);
        headerActions.Add(closeButton);
        header.Add(brandBlock);
        header.Add(songBlock);
        header.Add(transportBlock);
        header.Add(timeBlock);
        header.Add(headerActions);

        contentHost = new VisualElement();
        contentHost.style.flexGrow = 1f;
        contentHost.style.minHeight = 0f;
        contentHost.style.paddingLeft = 18f;
        contentHost.style.paddingRight = 18f;
        contentHost.style.paddingTop = 18f;

        statusLabel = CreateLabel(string.Empty, 32f, new Color(0.74f, 0.80f, 0.88f, 0.96f), false, TextAnchor.MiddleLeft, false);
        statusLabel.style.height = 60f;
        statusLabel.style.minHeight = 60f;
        statusLabel.style.paddingLeft = 24f;
        statusLabel.style.paddingRight = 24f;
        statusLabel.style.backgroundColor = new Color(0.028f, 0.034f, 0.044f, 0.98f);
        statusLabel.style.borderTopWidth = 1f;
        statusLabel.style.borderTopColor = new Color(0.16f, 0.20f, 0.26f, 1f);
        statusLabel.style.whiteSpace = WhiteSpace.NoWrap;

        RootElement.Add(header);
        RootElement.Add(contentHost);
        RootElement.Add(statusLabel);
        Rebuild();
    }

    private VisualElement CreateHeaderStringTheoryWordmark(float size)
    {
        VisualElement wordmark = new VisualElement();
        wordmark.style.flexDirection = FlexDirection.Row;
        wordmark.style.alignItems = Align.Center;
        wordmark.style.justifyContent = Justify.FlexStart;
        wordmark.style.height = 58f;

        const string stringWord = "STRING";
        for (int i = 0; i < stringWord.Length; i++)
        {
            Label letter = CreateLabel(stringWord[i].ToString(), size, LogoStringColors[i % LogoStringColors.Length], true, TextAnchor.MiddleCenter, true);
            letter.style.unityFontStyleAndWeight = FontStyle.Bold;
            letter.style.marginRight = 1.6f;
            wordmark.Add(letter);
        }

        Label theory = CreateLabel("THEORY", size, new Color(0.87f, 0.95f, 1f, 1f), true, TextAnchor.MiddleLeft, true);
        theory.style.unityFontStyleAndWeight = FontStyle.Bold;
        theory.style.marginLeft = 9f;
        wordmark.Add(theory);
        return wordmark;
    }

    private VisualElement CreatePlaybackSpeedControl(out Label speedLabel, out Slider speedSlider)
    {
        VisualElement control = new VisualElement();
        control.style.width = 210f;
        control.style.minWidth = 210f;
        control.style.height = 72f;
        control.style.marginLeft = 18f;
        control.style.justifyContent = Justify.Center;

        VisualElement labelRow = new VisualElement();
        labelRow.style.flexDirection = FlexDirection.Row;
        labelRow.style.justifyContent = Justify.SpaceBetween;
        labelRow.style.alignItems = Align.Center;

        Label title = CreateLabel("Speed", 18f, new Color(0.68f, 0.74f, 0.84f, 1f), true, TextAnchor.MiddleLeft, false);
        speedLabel = CreateLabel("100%", 18f, new Color(0.92f, 0.95f, 1f, 1f), true, TextAnchor.MiddleRight, false);
        speedLabel.style.width = 66f;
        labelRow.Add(title);
        labelRow.Add(speedLabel);
        control.Add(labelRow);

        speedSlider = new Slider(MinPlaybackSpeed * 100f, MaxPlaybackSpeed * 100f);
        speedSlider.focusable = false;
        speedSlider.value = 100f;
        speedSlider.style.height = 28f;
        speedSlider.style.marginTop = 3f;
        speedSlider.style.marginLeft = 0f;
        speedSlider.style.marginRight = 0f;
        ApplyChartSliderStyle(speedSlider);
        speedSlider.RegisterValueChangedCallback(evt => SetPlaybackSpeedPercent(evt.newValue));
        control.Add(speedSlider);
        return control;
    }

    public void SetVisible(bool show)
    {
        if (visible == show)
        {
            RootElement.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            return;
        }

        visible = show;
        RootElement.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (show)
            Rebuild();
        else
            ResetEditorSession();
    }

    public void Update(float deltaTime, bool show)
    {
        SetVisible(show);
        if (!show)
            return;

        if (HandleOverlayKeyboardInput())
        {
            AdvancePlayback(Mathf.Max(0f, deltaTime));
            UpdateHighwayPreview();
            return;
        }

        HandleKeyboardShortcuts();
        AdvancePlayback(Mathf.Max(0f, deltaTime));
        UpdateHighwayPreview();
    }

    private void ResetEditorSession()
    {
        HideContextMenu();
        HideEditPopup();
        HideSaveSuccessPopup();
        SetChartEditorKeyboardCaptureActive(false);
        ClearMarqueeSelection();
        StopPlayback();
        ResetEditorAudioCache();
        project = null;
        screen = ChartEditorScreen.Startup;
        mode = ChartEditorMode.SyncTiming;
        currentWarnings = new List<string>();
        ClearNoteSelection();
        noteClipboard.Clear();
        selectedSectionId = null;
        selectedSyncPointId = null;
        sidebarExpansionAnimations.Clear();
        tracksExpanded = true;
        sectionsExpanded = false;
        anchorsExpanded = false;
        projectInfoExpanded = false;
        timelineScrollOffset = Vector2.zero;
        timelineScrollInitialized = false;
        timelineZoom = 1f;
        timelineViewportWidth = 0f;
        currentTimelineScrollView = null;
        cursorElement = null;
        cursorHandleElement = null;
        silentPlaybackTimeSeconds = 0.0;
        seekDragging = false;
        seekWasPlaying = false;
        InvalidateAuditionCache();
        DisposeHighwayPreview();
        ResetArrowRepeat();
        contentHost.Clear();
        SetStatus(string.Empty);
    }

    private void Rebuild()
    {
        if (skipTimelineScrollCaptureOnce)
            skipTimelineScrollCaptureOnce = false;
        else
            CaptureCurrentTimelineScrollOffset();

        HideContextMenu();
        ClearMarqueeSelection();
        cursorElement = null;
        cursorHandleElement = null;
        waveformTextureElement = null;
        waveformVectorElement = null;
        currentNoteHits.Clear();
        currentNoteBlocks.Clear();
        currentTechniqueSegmentVisuals.Clear();
        MarkHighwayPreviewDirty();
        InvalidateAuditionCache();
        EnsureAudioClipRequested();
        contentHost.Clear();
        headerTitleLabel.text = project == null ? "No project loaded" : FirstNonEmpty(project.metadata?.title, "Untitled Project");
        headerSubtitleLabel.text = project == null
            ? "Import a chart to begin"
            : FirstNonEmpty(project.metadata?.artist, project.sourceKind.ToString(), "Unknown Artist");
        headerTimeLabel.text = project == null
            ? "00:00.000 / 00:00.000"
            : $"{FormatTime(project.cursorTimeSeconds)} / {FormatTime(project.DurationSeconds)}";
        UpdateHeaderProgress();
        bool hasProject = project != null;
        saveButton.SetEnabled(hasProject);
        transportPlayButton.SetEnabled(hasProject);
        transportPlayButton.text = editorPlaying ? "Ⅱ" : "▶";

        playbackSpeedSlider?.SetEnabled(hasProject);
        UpdatePlaybackSpeedControl();

        switch (screen)
        {
            case ChartEditorScreen.ImportSummary:
                BuildImportSummary();
                break;
            case ChartEditorScreen.Editor:
                BuildEditor();
                break;
            default:
                BuildStartup();
                break;
        }
    }

    private void BuildStartup()
    {
        VisualElement shell = new VisualElement();
        shell.style.flexGrow = 1f;
        shell.style.alignItems = Align.Center;
        shell.style.justifyContent = Justify.Center;

        VisualElement panel = new VisualElement();
        panel.style.width = 1120f;
        panel.style.maxWidth = Length.Percent(92f);
        panel.style.paddingLeft = 42f;
        panel.style.paddingRight = 42f;
        panel.style.paddingTop = 38f;
        panel.style.paddingBottom = 38f;
        StylePanel(panel, new Color(0.030f, 0.036f, 0.048f, 0.98f), new Color(0.22f, 0.25f, 0.30f, 0.88f), 16f);

        Label title = CreateLabel("Create or Open", 56f, Color.white, true, TextAnchor.MiddleCenter, true);
        title.style.marginBottom = 12f;
        Label subtitle = CreateLabel("Choose a source to start editing.", 27f, new Color(0.76f, 0.82f, 0.90f, 0.94f), false, TextAnchor.MiddleCenter, false);
        subtitle.style.whiteSpace = WhiteSpace.Normal;
        subtitle.style.marginBottom = 26f;

        panel.Add(title);
        panel.Add(subtitle);
        panel.Add(CreateStartupAction("Open .theory Package", ImportTheoryPackage));
        panel.Add(CreateStartupAction("Create from Guitar Pro / MusicXML + Audio", ImportChartAndAudio));
        panel.Add(CreateStartupAction("Import Rocksmith PSARC", ImportPsarc));
        panel.Add(CreateStartupAction("Open Unpacked Chart Folder", ImportFolder));
        panel.Add(CreateStartupAction("Open Existing Chart Editor Project", OpenExistingProject));

        shell.Add(panel);
        contentHost.Add(shell);
        SetStatus("Ready.");
    }

    private VisualElement CreateStartupAction(string label, Action action)
    {
        Button button = new Button(action) { text = string.Empty };
        button.focusable = false;
        button.style.height = 88f;
        button.style.width = Length.Percent(100f);
        button.style.marginTop = 9f;
        button.style.marginBottom = 9f;
        button.style.paddingLeft = 24f;
        button.style.paddingRight = 24f;
        button.style.flexDirection = FlexDirection.Row;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.SpaceBetween;
        button.style.unityFontDefinition = bodyFont;
        Color accent = label.IndexOf("PSARC", StringComparison.OrdinalIgnoreCase) >= 0
            ? new Color(0.40f, 0.72f, 1f, 1f)
            : label.IndexOf("Folder", StringComparison.OrdinalIgnoreCase) >= 0
                ? new Color(0.47f, 0.88f, 0.64f, 1f)
                : label.IndexOf("Existing", StringComparison.OrdinalIgnoreCase) >= 0
                    ? new Color(0.98f, 0.72f, 0.36f, 1f)
                    : new Color(0.68f, 0.44f, 1f, 1f);
        StyleSoftButton(button, accent);
        SetRadius(button, 12f);

        Label title = CreateLabel(label, 29f, new Color(0.96f, 0.98f, 1f, 1f), true, TextAnchor.MiddleLeft, false);
        title.style.flexGrow = 1f;
        title.style.whiteSpace = WhiteSpace.NoWrap;
        button.Add(title);

        Label actionLabel = CreateLabel("Open", 23f, new Color(accent.r, accent.g, accent.b, 0.96f), true, TextAnchor.MiddleRight, false);
        actionLabel.style.width = 116f;
        button.Add(actionLabel);
        return button;
    }

    private void BuildImportSummary()
    {
        if (project == null)
        {
            screen = ChartEditorScreen.Startup;
            BuildStartup();
            return;
        }

        VisualElement shell = new VisualElement();
        shell.style.flexGrow = 1f;
        shell.style.flexDirection = FlexDirection.Row;
        shell.style.alignItems = Align.Stretch;

        VisualElement left = CreatePanelColumn(420f);
        left.Add(CreateSectionTitle("Import Summary"));
        left.Add(CreateKeyValue("Song", FirstNonEmpty(project.metadata.title, "Unknown")));
        left.Add(CreateKeyValue("Artist", FirstNonEmpty(project.metadata.artist, "Unknown")));
        left.Add(CreateKeyValue("Audio", string.IsNullOrWhiteSpace(project.audio?.displayName) ? "None" : project.audio.displayName));
        left.Add(CreateKeyValue("Tracks", (project.tracks?.Count ?? 0).ToString()));
        left.Add(CreateKeyValue("Sections", (project.sections?.Count ?? 0).ToString()));
        left.Add(CreateKeyValue("Anchors", ChartEditorTimingService.GetAnchors(project).Count.ToString()));

        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Column;
        actions.style.marginTop = 22f;
        Button openButton = CreatePanelActionButton("Open Editor", () =>
        {
            if (ShouldWaitForEditorAudioPreparation())
            {
                openEditorWhenAudioReady = true;
                SetStatus("Preparing audio and waveform before opening the editor...");
                Rebuild();
                return;
            }

            screen = ChartEditorScreen.Editor;
            Rebuild();
        }, primary: true);
        Button detectButton = CreatePanelActionButton("Auto Detect Issues", () =>
        {
            currentWarnings = ChartEditorValidationService.BuildWarnings(project);
            if (currentWarnings.Count == 0)
                currentWarnings.Add("No additional issues detected.");
            Rebuild();
        }, primary: false);
        Button backButton = CreatePanelActionButton("Back to Import", () =>
        {
            screen = ChartEditorScreen.Startup;
            Rebuild();
        }, primary: false);
        actions.Add(openButton);
        actions.Add(detectButton);
        actions.Add(backButton);
        left.Add(actions);

        ScrollView right = new ScrollView(ScrollViewMode.Vertical);
        ConfigureScrollView(right);
        right.style.flexGrow = 1f;
        right.style.marginLeft = 18f;
        right.style.paddingLeft = 18f;
        right.style.paddingRight = 18f;
        right.style.paddingTop = 18f;
        right.style.paddingBottom = 18f;
        StylePanel(right, new Color(0.035f, 0.043f, 0.058f, 0.96f), new Color(0.18f, 0.28f, 0.42f, 0.72f));

        right.Add(CreateSectionTitle("Tracks"));
        if (project.tracks != null)
        {
            for (int i = 0; i < project.tracks.Count; i++)
            {
                ChartEditorTrack track = project.tracks[i];
                right.Add(CreateInfoRow(
                    track?.displayName ?? $"Track {i + 1}",
                    $"{track?.role}  -  {track?.notes?.Count ?? 0} notes  -  {FirstNonEmpty(track?.tuning?.displayName, "Tuning unknown")}"));
            }
        }

        right.Add(CreateSectionTitle("Warnings"));
        if (currentWarnings == null || currentWarnings.Count == 0)
            right.Add(CreateInfoRow("No warnings", "Import is ready to edit."));
        else
        {
            for (int i = 0; i < currentWarnings.Count; i++)
                right.Add(CreateInfoRow($"Warning {i + 1}", currentWarnings[i], new Color(1f, 0.64f, 0.32f, 1f)));
        }

        shell.Add(left);
        shell.Add(right);
        contentHost.Add(shell);
        SetStatus("Import ready.");
    }

    private void BuildEditor()
    {
        if (project == null)
        {
            screen = ChartEditorScreen.Startup;
            BuildStartup();
            return;
        }

        project.EnsureDefaults();
        EnsureSingleVisibleTrack(markDirty: false);
        VisualElement root = new VisualElement();
        root.style.flexGrow = 1f;
        root.style.minHeight = 0f;
        root.style.flexDirection = FlexDirection.Column;

        VisualElement main = new VisualElement();
        main.style.flexDirection = FlexDirection.Row;
        main.style.flexGrow = 1f;
        main.style.minHeight = 0f;

        VisualElement center = new VisualElement();
        chartEditorCenterElement = center;
        center.style.flexGrow = 1f;
        center.style.minWidth = 0f;
        center.style.minHeight = 0f;
        center.style.flexDirection = FlexDirection.Column;
        center.Add(BuildTimelinePanel());
        center.Add(BuildTimelinePreviewSplitter());
        center.Add(BuildHighwayPreviewPanel());

        main.Add(BuildLeftPanel());
        main.Add(center);
        root.Add(main);
        contentHost.Add(root);
        SetStatus(project.dirty ? "Unsaved changes." : "Project saved.");
    }

    private VisualElement BuildTimelinePreviewSplitter()
    {
        VisualElement splitter = new VisualElement();
        splitter.style.height = HighwayPreviewSplitterHeight;
        splitter.style.minHeight = HighwayPreviewSplitterHeight;
        splitter.style.flexShrink = 0f;
        splitter.style.marginRight = 18f;
        splitter.style.justifyContent = Justify.Center;
        splitter.style.alignItems = Align.Center;
        splitter.style.backgroundColor = new Color(0.012f, 0.016f, 0.024f, 0.92f);
        splitter.pickingMode = PickingMode.Position;
        SetElementCursor(splitter, ChartEditorCursorKind.ResizeVertical);

        VisualElement grip = new VisualElement();
        grip.style.width = 104f;
        grip.style.height = 6f;
        grip.style.backgroundColor = new Color(0.78f, 0.84f, 0.94f, 0.32f);
        grip.pickingMode = PickingMode.Ignore;
        SetRadius(grip, 999f);
        splitter.Add(grip);

        AddPreviewSplitterDragHandlers(splitter);
        return splitter;
    }

    private VisualElement BuildHighwayPreviewPanel()
    {
        EnsureHighwayPreviewTexture();

        VisualElement panel = new VisualElement();
        highwayPreviewPanelElement = panel;
        ApplyHighwayPreviewHeight(ClampHighwayPreviewHeight(highwayPreviewPanelHeight));
        panel.style.minHeight = HighwayPreviewMinHeight;
        panel.style.flexShrink = 0f;
        panel.style.flexDirection = FlexDirection.Row;
        panel.style.alignItems = Align.Stretch;
        panel.style.marginTop = 10f;
        panel.style.marginRight = 18f;
        panel.style.paddingLeft = 12f;
        panel.style.paddingRight = 12f;
        panel.style.paddingTop = 12f;
        panel.style.paddingBottom = 12f;
        StylePanel(panel, new Color(0.032f, 0.038f, 0.050f, 0.99f), new Color(0.17f, 0.21f, 0.27f, 1f), 0f);

        VisualElement previewFrame = new VisualElement();
        previewFrame.style.flexGrow = 1f;
        previewFrame.style.minWidth = 0f;
        previewFrame.style.height = Length.Percent(100f);
        previewFrame.style.minHeight = 0f;
        previewFrame.style.backgroundColor = new Color(0.006f, 0.008f, 0.012f, 1f);
        previewFrame.style.overflow = Overflow.Hidden;
        SetRadius(previewFrame, 22f);
        SetBorderWidth(previewFrame, 1f);
        SetBorderColor(previewFrame, new Color(0.18f, 0.23f, 0.31f, 0.85f));

        highwayPreviewTextureElement = new VisualElement();
        highwayPreviewTextureElement.style.position = Position.Absolute;
        highwayPreviewTextureElement.style.left = 0f;
        highwayPreviewTextureElement.style.right = 0f;
        highwayPreviewTextureElement.style.top = 0f;
        highwayPreviewTextureElement.style.bottom = 0f;
        highwayPreviewTextureElement.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        if (highwayPreviewTexture != null)
            highwayPreviewTextureElement.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(highwayPreviewTexture));
        previewFrame.Add(highwayPreviewTextureElement);

        VisualElement overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 24f;
        overlay.style.top = 22f;
        overlay.style.paddingLeft = 18f;
        overlay.style.paddingRight = 18f;
        overlay.style.paddingTop = 12f;
        overlay.style.paddingBottom = 12f;
        overlay.style.backgroundColor = new Color(0.010f, 0.014f, 0.022f, 0.62f);
        SetRadius(overlay, 16f);
        highwayPreviewTitleLabel = CreateLabel("3D Highway Preview", 34f, Color.white, true, TextAnchor.MiddleLeft, false);
        highwayPreviewTitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        highwayPreviewMetaLabel = CreateLabel(string.Empty, 24f, new Color(0.70f, 0.76f, 0.84f, 1f), false, TextAnchor.MiddleLeft, false);
        highwayPreviewMetaLabel.style.whiteSpace = WhiteSpace.Normal;
        highwayPreviewMetaLabel.style.marginTop = 4f;
        overlay.Add(highwayPreviewTitleLabel);
        overlay.Add(highwayPreviewMetaLabel);
        previewFrame.Add(overlay);
        panel.Add(previewFrame);

        UpdateHighwayPreview();
        return panel;
    }

    private void AddPreviewSplitterDragHandlers(VisualElement splitter)
    {
        bool dragging = false;
        int pointerId = -1;
        float startPointerY = 0f;
        float startHeight = HighwayPreviewDefaultHeight;

        splitter.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;

            dragging = true;
            pointerId = evt.pointerId;
            startPointerY = evt.position.y;
            startHeight = GetCurrentHighwayPreviewHeight();
            splitter.CapturePointer(pointerId);
            splitter.style.backgroundColor = new Color(0.028f, 0.036f, 0.052f, 0.98f);
            evt.StopImmediatePropagation();
        });

        splitter.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            float deltaY = evt.position.y - startPointerY;
            ApplyHighwayPreviewHeight(startHeight - deltaY);
            evt.StopImmediatePropagation();
        });

        splitter.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            dragging = false;
            if (splitter.HasPointerCapture(pointerId))
                splitter.ReleasePointer(pointerId);

            splitter.style.backgroundColor = new Color(0.012f, 0.016f, 0.024f, 0.92f);
            RootElement.schedule.Execute(UpdateHighwayPreview);
            evt.StopImmediatePropagation();
        });
    }

    private float GetCurrentHighwayPreviewHeight()
    {
        if (highwayPreviewPanelElement != null && highwayPreviewPanelElement.resolvedStyle.height > 1f)
            return highwayPreviewPanelElement.resolvedStyle.height;
        return highwayPreviewPanelHeight > 1f ? highwayPreviewPanelHeight : HighwayPreviewDefaultHeight;
    }

    private void ApplyHighwayPreviewHeight(float height)
    {
        highwayPreviewPanelHeight = ClampHighwayPreviewHeight(height);
        if (highwayPreviewPanelElement == null)
            return;

        highwayPreviewPanelElement.style.height = highwayPreviewPanelHeight;
        highwayPreviewPanelElement.style.minHeight = HighwayPreviewMinHeight;
        highwayPreviewPanelElement.MarkDirtyRepaint();
    }

    private float ClampHighwayPreviewHeight(float height)
    {
        float maxHeight = HighwayPreviewDefaultHeight * 1.7f;
        if (chartEditorCenterElement != null && chartEditorCenterElement.resolvedStyle.height > 1f)
        {
            maxHeight = Mathf.Max(
                HighwayPreviewMinHeight,
                chartEditorCenterElement.resolvedStyle.height - HighwayPreviewMinTimelineHeight - HighwayPreviewSplitterHeight);
        }

        return Mathf.Clamp(height, HighwayPreviewMinHeight, Mathf.Max(HighwayPreviewMinHeight, maxHeight));
    }

    private void EnsureHighwayPreviewTexture()
    {
        if (highwayPreviewTexture != null)
            return;

        highwayPreviewTexture = new RenderTexture(1600, 640, 24, RenderTextureFormat.ARGB32)
        {
            name = "ChartEditorHighwayPreviewTexture",
            antiAliasing = 4,
            useMipMap = false,
            autoGenerateMips = false
        };
        highwayPreviewTexture.Create();
    }

    private void EnsureHighwayPreviewCamera()
    {
        if (highwayPreviewCamera != null)
            return;

        highwayPreviewCameraObject = new GameObject("ChartEditorHighwayPreviewCamera");
        highwayPreviewCameraObject.hideFlags = HideFlags.HideAndDontSave;
        highwayPreviewCamera = highwayPreviewCameraObject.AddComponent<Camera>();
        highwayPreviewCamera.enabled = false;
        highwayPreviewCamera.clearFlags = CameraClearFlags.SolidColor;
        highwayPreviewCamera.backgroundColor = Color.black;
        highwayPreviewCamera.nearClipPlane = 0.01f;
        highwayPreviewCamera.farClipPlane = Mathf.Max(120f, owner != null ? owner.highwayCameraFarClip : 120f);
        highwayPreviewCamera.targetTexture = highwayPreviewTexture;
    }

    private void EnsureHighwayPreviewRenderer(
        ChartEditorHighwayPreviewFrame frame,
        string signature,
        List<TabSectionData> previewSections)
    {
        if (frame == null || frame.laneCount <= 0)
            return;

        EnsureHighwayPreviewTexture();
        EnsureHighwayPreviewCamera();

        if (highwayPreviewHost == null)
        {
            highwayPreviewHost = new GuitarHighway3DRenderHost
            {
                Camera = highwayPreviewCamera,
                TargetTexture = highwayPreviewTexture,
                ManualRender = true,
                EnableBackground = false,
                EnableHighwayCharacter = false,
                EnableSongHeaderOverlay = false,
                SuppressPendingNoteOutlines = true,
                RenderLayer = 29,
                RootName = "ChartEditorHighwayPreviewRendererRoot",
                RenderableStringCountOverride = frame.laneCount
            };
        }

        highwayPreviewHost.Camera = highwayPreviewCamera;
        highwayPreviewHost.TargetTexture = highwayPreviewTexture;
        highwayPreviewHost.RenderableStringCountOverride = frame.laneCount;

        signature ??= BuildHighwayPreviewSignature(frame, project);
        previewSections ??= BuildHighwayPreviewTabSections(project, frame.notes);

        if (highwayPreviewRenderer == null)
        {
            highwayPreviewRenderer = new GuitarHighway3DRenderer(highwayPreviewHost);
            highwayPreviewRenderer.Initialize(owner, frame.notes, previewSections);
            highwayPreviewSignature = signature;
        }
        else if (!string.Equals(highwayPreviewSignature, signature, StringComparison.Ordinal))
        {
            highwayPreviewRenderer.ResetRenderer(frame.notes, previewSections);
            highwayPreviewSignature = signature;
        }
    }

    private void ClearHighwayPreviewTexture()
    {
        if (highwayPreviewTexture == null)
            return;

        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = highwayPreviewTexture;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = previousActive;
    }

    private void UpdateHighwayPreview()
    {
        if (!visible || screen != ChartEditorScreen.Editor || project == null)
            return;

        bool previewDataDirty = cachedHighwayPreviewFrame == null || cachedHighwayPreviewRevision != highwayPreviewRevision;
        double cursorTime = project.cursorTimeSeconds;
        bool timeChanged = lastHighwayPreviewRenderTime < 0.0 ||
                           Math.Abs(cursorTime - lastHighwayPreviewRenderTime) > HighwayPreviewTimeEpsilon;
        float now = Time.unscaledTime;
        float interval = editorPlaying ? HighwayPreviewPlayingInterval : HighwayPreviewIdleInterval;
        if (!previewDataDirty && !forceHighwayPreviewRender && !timeChanged)
            return;
        if (!forceHighwayPreviewRender && now < nextHighwayPreviewRenderTime)
            return;

        ChartEditorHighwayPreviewFrame frame = GetCachedHighwayPreviewFrame(previewDataDirty);
        if (frame == null || frame.laneCount <= 0)
        {
            highwayPreviewRenderer?.DisposeRenderer();
            highwayPreviewRenderer = null;
            highwayPreviewSignature = null;
            ClearHighwayPreviewTexture();
        }
        else
        {
            frame.songTime = Mathf.Max(0f, (float)cursorTime);
            frame.songDurationSeconds = Mathf.Max(0.1f, project.DurationSeconds);
            EnsureHighwayPreviewRenderer(frame, cachedHighwayPreviewSignature, cachedHighwayPreviewTabSections);
            highwayPreviewRenderer?.Render(BuildHighwayPreviewSnapshot(frame));
        }

        lastHighwayPreviewRenderTime = cursorTime;
        nextHighwayPreviewRenderTime = now + interval;
        forceHighwayPreviewRender = false;

        if (highwayPreviewTextureElement != null && highwayPreviewTexture != null)
            highwayPreviewTextureElement.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(highwayPreviewTexture));

        ChartEditorTrack track = project.SelectedTrack;
        if (highwayPreviewTitleLabel != null)
        {
            highwayPreviewTitleLabel.text = ChartEditorHighwayPreviewSnapshotBuilder.IsSupportedPreviewTrack(track)
                ? $"{FormatTrackName(track)} Live 3D Preview"
                : "3D Preview Unavailable";
        }

        if (highwayPreviewMetaLabel != null)
        {
            if (ChartEditorHighwayPreviewSnapshotBuilder.IsSupportedPreviewTrack(track))
            {
                string tuning = FirstNonEmpty(track?.tuning?.displayName, track?.role.ToString(), "Unknown tuning");
                int laneCount = ChartEditorHighwayPreviewSnapshotBuilder.ResolveLaneCount(track);
                highwayPreviewMetaLabel.text = $"{tuning}  -  {laneCount} strings  -  {track?.notes?.Count ?? 0} notes  -  {FormatTime(project.cursorTimeSeconds)}";
            }
            else
            {
                highwayPreviewMetaLabel.text = "Unsupported track type.";
            }
        }
    }

    private ChartEditorHighwayPreviewFrame GetCachedHighwayPreviewFrame(bool forceRebuild)
    {
        if (!forceRebuild && cachedHighwayPreviewFrame != null)
            return cachedHighwayPreviewFrame;

        cachedHighwayPreviewFrame = ChartEditorHighwayPreviewSnapshotBuilder.Build(project);
        cachedHighwayPreviewRevision = highwayPreviewRevision;
        cachedHighwayPreviewSignature = cachedHighwayPreviewFrame != null
            ? BuildHighwayPreviewSignature(cachedHighwayPreviewFrame, project)
            : null;
        cachedHighwayPreviewTabSections = cachedHighwayPreviewFrame != null
            ? BuildHighwayPreviewTabSections(project, cachedHighwayPreviewFrame.notes)
            : new List<TabSectionData>();
        cachedHighwayPreviewTimelineSections = BuildHighwayPreviewTimelineSections(project);
        return cachedHighwayPreviewFrame;
    }

    private GuitarGameplaySnapshot BuildHighwayPreviewSnapshot(ChartEditorHighwayPreviewFrame frame)
    {
        float songTime = Mathf.Max(0f, frame.songTime);
        List<GameplayNoteState> noteStates = frame.notes != null
            ? frame.notes.Select(note => BuildHighwayPreviewNoteState(note, songTime)).ToList()
            : new List<GameplayNoteState>();

        return new GuitarGameplaySnapshot
        {
            gameplayMode = GuitarGameplayMode.Guitar,
            songLibraryType = SongLibraryType.Guitar,
            songTime = songTime,
            songDurationSeconds = Mathf.Max(0.1f, frame.songDurationSeconds),
            isPaused = !editorPlaying,
            playbackSpeedPercent = 100f,
            tabSpeedOffsetPercent = 100f,
            noteStates = noteStates,
            arpeggioGuides = frame.arpeggioGuides ?? new List<ArpeggioGuideData>(),
            sections = cachedHighwayPreviewTabSections ?? new List<TabSectionData>(),
            songTimelineSections = cachedHighwayPreviewTimelineSections ?? new List<SongTimelineSectionData>(),
            latestDetectedPitches = new HashSet<int>(),
            showHighwayCharacter = false,
            showMainMenu = false,
            mainMenuFlowActive = false,
            showMiniGames = false,
            showChartEditor = false,
            showToneLab = false,
            showTuner = false,
            songEnded = false
        };
    }

    private static GameplayNoteState BuildHighwayPreviewNoteState(NoteData note, float songTime)
    {
        GameplayNoteState state = new GameplayNoteState(note);
        float noteEnd = note.time + Mathf.Max(0f, note.duration);
        if (note.techniqueSegments != null)
        {
            for (int i = 0; i < note.techniqueSegments.Count; i++)
                noteEnd = Mathf.Max(noteEnd, note.time + Mathf.Max(note.techniqueSegments[i].startOffset, note.techniqueSegments[i].endOffset));
        }

        if (songTime > noteEnd + 0.03f)
        {
            state.result = GameplayNoteResult.Hit;
            state.resolvedAt = note.time;
        }

        return state;
    }

    private static List<TabSectionData> BuildHighwayPreviewTabSections(ChartEditorProject sourceProject, List<NoteData> notes)
    {
        List<TabSectionData> sections = new List<TabSectionData>();
        if (sourceProject?.sections == null)
            return sections;

        List<NoteData> safeNotes = notes ?? new List<NoteData>();
        for (int i = 0; i < sourceProject.sections.Count; i++)
        {
            ChartEditorSection section = sourceProject.sections[i];
            if (section == null)
                continue;

            float start = Mathf.Max(0f, (float)section.startTimeSeconds);
            float end = Mathf.Max(start + 0.01f, (float)section.endTimeSeconds);
            TabSectionData data = new TabSectionData
            {
                index = i,
                startTime = start,
                endTime = end,
                noteIds = safeNotes
                    .Where(note => note.time >= start && note.time <= end)
                    .Select(note => note.id)
                    .ToList()
            };
            sections.Add(data);
        }

        return sections;
    }

    private static List<SongTimelineSectionData> BuildHighwayPreviewTimelineSections(ChartEditorProject sourceProject)
    {
        List<SongTimelineSectionData> sections = new List<SongTimelineSectionData>();
        if (sourceProject?.sections == null)
            return sections;

        for (int i = 0; i < sourceProject.sections.Count; i++)
        {
            ChartEditorSection section = sourceProject.sections[i];
            if (section == null)
                continue;

            float start = Mathf.Max(0f, (float)section.startTimeSeconds);
            float end = Mathf.Max(start + 0.01f, (float)section.endTimeSeconds);
            sections.Add(new SongTimelineSectionData
            {
                index = i,
                name = FirstNonEmpty(section.name, $"Section {i + 1}"),
                startTime = start,
                endTime = end
            });
        }

        return sections;
    }

    private static string BuildHighwayPreviewSignature(ChartEditorHighwayPreviewFrame frame, ChartEditorProject sourceProject)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + StablePreviewHash(frame.trackId);
            hash = (hash * 31) + frame.laneCount;
            hash = (hash * 31) + StablePreviewHash(frame.tuningName);
            if (frame.notes != null)
            {
                hash = (hash * 31) + frame.notes.Count;
                for (int i = 0; i < frame.notes.Count; i++)
                {
                    NoteData note = frame.notes[i];
                    hash = (hash * 31) + note.id;
                    hash = (hash * 31) + Mathf.RoundToInt(note.time * 1000f);
                    hash = (hash * 31) + Mathf.RoundToInt(note.duration * 1000f);
                    hash = (hash * 31) + note.stringIdx;
                    hash = (hash * 31) + note.fret;
                    hash = (hash * 31) + (int)note.technique;
                    hash = (hash * 31) + note.slideTargetFret;
                    hash = (hash * 31) + Mathf.RoundToInt(note.bendStep * 1000f);
                    hash = (hash * 31) + (note.isMuted ? 1 : 0);
                    hash = (hash * 31) + (note.isLegato ? 1 : 0);
                    if (note.techniqueSegments != null)
                    {
                        hash = (hash * 31) + note.techniqueSegments.Count;
                        for (int segmentIndex = 0; segmentIndex < note.techniqueSegments.Count; segmentIndex++)
                        {
                            NoteTechniqueSegmentData segment = note.techniqueSegments[segmentIndex];
                            hash = (hash * 31) + (int)segment.type;
                            hash = (hash * 31) + Mathf.RoundToInt(segment.startOffset * 1000f);
                            hash = (hash * 31) + Mathf.RoundToInt(segment.endOffset * 1000f);
                            hash = (hash * 31) + segment.startFret;
                            hash = (hash * 31) + segment.endFret;
                            hash = (hash * 31) + Mathf.RoundToInt(segment.startBend * 1000f);
                            hash = (hash * 31) + Mathf.RoundToInt(segment.endBend * 1000f);
                        }
                    }
                }
            }

            if (sourceProject?.sections != null)
            {
                hash = (hash * 31) + sourceProject.sections.Count;
                for (int i = 0; i < sourceProject.sections.Count; i++)
                {
                    ChartEditorSection section = sourceProject.sections[i];
                    if (section == null)
                        continue;

                    hash = (hash * 31) + StablePreviewHash(section.name);
                    hash = (hash * 31) + Mathf.RoundToInt((float)section.startTimeSeconds * 1000f);
                    hash = (hash * 31) + Mathf.RoundToInt((float)section.endTimeSeconds * 1000f);
                }
            }

            return hash.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static int StablePreviewHash(string value)
    {
        unchecked
        {
            int hash = 23;
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                    hash = (hash * 31) + value[i];
            }

            return hash;
        }
    }

    private void MarkHighwayPreviewDirty(bool renderImmediately = true)
    {
        highwayPreviewRevision++;
        cachedHighwayPreviewRevision = -1;
        if (renderImmediately)
            forceHighwayPreviewRender = true;
    }

    private void DisposeHighwayPreview()
    {
        highwayPreviewRenderer?.DisposeRenderer();
        highwayPreviewRenderer = null;
        highwayPreviewHost = null;
        highwayPreviewSignature = null;
        cachedHighwayPreviewFrame = null;
        cachedHighwayPreviewTabSections = new List<TabSectionData>();
        cachedHighwayPreviewTimelineSections = new List<SongTimelineSectionData>();
        cachedHighwayPreviewSignature = null;
        cachedHighwayPreviewRevision = -1;
        nextHighwayPreviewRenderTime = 0f;
        lastHighwayPreviewRenderTime = -1.0;
        forceHighwayPreviewRender = true;
        if (highwayPreviewCameraObject != null)
            UnityEngine.Object.Destroy(highwayPreviewCameraObject);
        highwayPreviewCameraObject = null;
        highwayPreviewCamera = null;
        if (highwayPreviewTexture != null)
        {
            highwayPreviewTexture.Release();
            UnityEngine.Object.Destroy(highwayPreviewTexture);
        }
        highwayPreviewTexture = null;
        highwayPreviewTextureElement = null;
        highwayPreviewTitleLabel = null;
        highwayPreviewMetaLabel = null;
    }

    private VisualElement BuildModeTabs()
    {
        VisualElement tabs = new VisualElement();
        tabs.style.flexDirection = FlexDirection.Row;
        tabs.style.alignItems = Align.Center;
        tabs.style.height = 84f;
        tabs.style.minHeight = 84f;
        tabs.style.marginBottom = 0f;
        tabs.style.backgroundColor = new Color(0.052f, 0.060f, 0.074f, 0.98f);
        tabs.style.borderBottomWidth = 1f;
        tabs.style.borderBottomColor = new Color(0.18f, 0.22f, 0.28f, 1f);
        tabs.style.paddingLeft = 22f;
        AddModeTab(tabs, "Beat Map", ChartEditorMode.SyncTiming);
        AddModeTab(tabs, "Notes", ChartEditorMode.Notes);
        AddModeTab(tabs, "Sections", ChartEditorMode.Sections);
        AddModeTab(tabs, "Song Info", ChartEditorMode.SongInfo);
        return tabs;
    }

    private void AddModeTab(VisualElement parent, string label, ChartEditorMode tabMode)
    {
        bool selected = mode == tabMode;
        Button button = CreateButton(label, () =>
        {
            mode = tabMode;
            ClearNoteSelection();
            selectedSectionId = null;
            selectedSyncPointId = null;
            Rebuild();
        });
        button.style.height = 84f;
        button.style.minWidth = 270f;
        button.style.marginRight = 4f;
        button.style.fontSize = UiFont(30f);
        button.style.borderTopWidth = 0f;
        button.style.borderRightWidth = 0f;
        button.style.borderLeftWidth = 0f;
        button.style.borderBottomWidth = selected ? 3f : 1f;
        button.style.borderBottomColor = selected ? new Color(0.62f, 0.36f, 0.96f, 1f) : new Color(0.18f, 0.22f, 0.28f, 1f);
        button.style.backgroundColor = selected ? new Color(0.095f, 0.078f, 0.132f, 1f) : new Color(0.052f, 0.060f, 0.074f, 0f);
        button.style.color = selected ? Color.white : new Color(0.72f, 0.76f, 0.82f, 1f);
        parent.Add(button);
    }

    private VisualElement BuildLeftPanel()
    {
        ScrollView panel = new ScrollView(ScrollViewMode.Vertical);
        ConfigureScrollView(panel);
        panel.style.width = SidebarWidth;
        panel.style.minWidth = SidebarWidth;
        panel.style.marginRight = 24f;
        panel.style.paddingLeft = 0f;
        panel.style.paddingRight = 0f;
        panel.style.paddingTop = 10f;
        panel.style.paddingBottom = 22f;
        StylePanel(panel, new Color(0.050f, 0.058f, 0.070f, 0.99f), new Color(0.17f, 0.21f, 0.27f, 1f), 0f);

        List<ChartEditorTrackViewGroup> groups = BuildTrackViewGroups();
        int sectionCount = project.sections?.Count ?? 0;
        int anchorCount = ChartEditorTimingService.GetAnchors(project).Count;
        panel.Add(CreateCollapsibleSidebarSection(
            SidebarTracksKey,
            "TRACKS",
            tracksExpanded,
            ToggleTracksExpanded,
            () => CreateTracksSidebarContent(groups),
            EstimateTracksSidebarContentHeight(groups),
            Mathf.Max(0, groups.Count).ToString(CultureInfo.InvariantCulture)));
        panel.Add(CreateCollapsibleSidebarSection(
            SidebarSectionsKey,
            "SECTIONS",
            sectionsExpanded,
            ToggleSectionsExpanded,
            CreateSectionsSidebarContent,
            EstimateSectionsSidebarContentHeight(),
            sectionCount > 0 ? sectionCount.ToString(CultureInfo.InvariantCulture) : string.Empty,
            AddSectionAtCursor));
        panel.Add(CreateCollapsibleSidebarSection(
            SidebarAnchorsKey,
            "ANCHORS",
            anchorsExpanded,
            ToggleAnchorsExpanded,
            CreateAnchorsList,
            EstimateAnchorsSidebarContentHeight(),
            anchorCount > 0 ? anchorCount.ToString(CultureInfo.InvariantCulture) : string.Empty));

        currentWarnings = ChartEditorValidationService.BuildWarnings(project);
        panel.Add(CreateStaticSidebarSection(
            "WARNINGS",
            currentWarnings.Count > 0 ? currentWarnings.Count.ToString(CultureInfo.InvariantCulture) : string.Empty,
            CreateWarningsSidebarContent));
        panel.Add(CreateSidebarActionButtons());
        panel.Add(CreateCollapsibleSidebarSection(
            SidebarProjectInfoKey,
            "PROJECT INFO",
            projectInfoExpanded,
            ToggleProjectInfoExpanded,
            CreateProjectInfoSidebarContent,
            EstimateProjectInfoSidebarContentHeight()));

        return panel;
    }

    private VisualElement CreateCollapsibleSidebarSection(
        string animationKey,
        string title,
        bool expanded,
        Action toggle,
        Func<VisualElement> buildContent,
        float fallbackContentHeight,
        string metadata = null,
        Action addAction = null)
    {
        VisualElement section = CreateSidebarSectionShell();
        section.Add(CreateSidebarSectionHeader(title, metadata, true, expanded, toggle, addAction));

        bool animating = sidebarExpansionAnimations.ContainsKey(animationKey);
        if (expanded || animating)
            section.Add(CreateSidebarAnimatedContent(animationKey, buildContent, fallbackContentHeight));

        return section;
    }

    private VisualElement CreateStaticSidebarSection(string title, string metadata, Func<VisualElement> buildContent)
    {
        VisualElement section = CreateSidebarSectionShell();
        section.Add(CreateSidebarSectionHeader(title, metadata, false, false, null, null));
        section.Add(CreateSidebarStaticContent(buildContent));
        return section;
    }

    private VisualElement CreateSidebarSectionShell()
    {
        VisualElement section = new VisualElement();
        section.style.marginLeft = SidebarSectionMarginX;
        section.style.marginRight = SidebarSectionMarginX;
        section.style.marginTop = SidebarSectionTopGap;
        section.style.marginBottom = SidebarSectionBottomGap;
        section.style.overflow = Overflow.Hidden;
        section.style.backgroundColor = new Color(0.044f, 0.052f, 0.066f, 0.92f);
        SetRadius(section, 14f);
        SetBorderWidth(section, 1f);
        SetBorderColor(section, new Color(0.17f, 0.21f, 0.28f, 0.94f));
        return section;
    }

    private VisualElement CreateSidebarSectionHeader(
        string title,
        string metadata,
        bool collapsible,
        bool expanded,
        Action toggle,
        Action addAction)
    {
        VisualElement row = new VisualElement();
        row.style.height = SidebarSectionHeaderHeight;
        row.style.minHeight = SidebarSectionHeaderHeight;
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.paddingLeft = 18f;
        row.style.paddingRight = 14f;
        row.style.backgroundColor = new Color(0.030f, 0.037f, 0.050f, 0.34f);

        if (collapsible && toggle != null)
        {
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                toggle();
                evt.StopPropagation();
            });
        }

        VisualElement labelGroup = new VisualElement();
        labelGroup.style.flexDirection = FlexDirection.Row;
        labelGroup.style.alignItems = Align.Center;
        labelGroup.style.flexGrow = 1f;
        labelGroup.style.minWidth = 0f;
        labelGroup.Add(CreateSidebarGripIcon());

        Label label = CreateLabel(title.ToUpperInvariant(), 20f, new Color(0.88f, 0.92f, 0.98f, 0.98f), true, TextAnchor.MiddleLeft, false);
        label.style.whiteSpace = WhiteSpace.NoWrap;
        labelGroup.Add(label);

        if (collapsible && !string.IsNullOrWhiteSpace(metadata))
            labelGroup.Add(CreateSidebarMetadataPill(metadata));

        row.Add(labelGroup);

        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.alignItems = Align.Center;
        actions.style.flexShrink = 0f;

        if (addAction != null)
            actions.Add(CreateSidebarSectionActionButton("+", addAction, 48f));

        if (collapsible && toggle != null)
            actions.Add(CreateSidebarSectionActionButton(expanded ? "Hide  v" : "Show  >", toggle, 104f));
        else if (!string.IsNullOrWhiteSpace(metadata))
            actions.Add(CreateSidebarSectionMetadataButton(metadata));

        row.Add(actions);
        return row;
    }

    private VisualElement CreateSidebarGripIcon()
    {
        VisualElement icon = new VisualElement();
        icon.style.width = 30f;
        icon.style.height = 30f;
        icon.style.marginRight = 12f;
        icon.style.flexDirection = FlexDirection.Column;
        icon.style.alignItems = Align.Center;
        icon.style.justifyContent = Justify.Center;
        icon.style.flexShrink = 0f;

        for (int rowIndex = 0; rowIndex < 3; rowIndex++)
        {
            VisualElement dotRow = new VisualElement();
            dotRow.style.flexDirection = FlexDirection.Row;
            dotRow.style.height = 7f;
            for (int column = 0; column < 3; column++)
            {
                VisualElement dot = new VisualElement();
                dot.style.width = 4f;
                dot.style.height = 4f;
                dot.style.marginLeft = 2f;
                dot.style.marginRight = 2f;
                dot.style.backgroundColor = new Color(0.58f, 0.64f, 0.72f, 0.88f);
                SetRadius(dot, 999f);
                dotRow.Add(dot);
            }

            icon.Add(dotRow);
        }

        return icon;
    }

    private VisualElement CreateSidebarMetadataPill(string text)
    {
        Label pill = CreateLabel(text, 17f, new Color(0.68f, 0.74f, 0.84f, 0.92f), true, TextAnchor.MiddleCenter, false);
        pill.style.height = 30f;
        pill.style.minWidth = 34f;
        pill.style.marginLeft = 12f;
        pill.style.paddingLeft = 10f;
        pill.style.paddingRight = 10f;
        pill.style.backgroundColor = new Color(0.090f, 0.105f, 0.132f, 0.62f);
        SetRadius(pill, 8f);
        SetBorderWidth(pill, 1f);
        SetBorderColor(pill, new Color(0.20f, 0.24f, 0.31f, 0.76f));
        return pill;
    }

    private Button CreateSidebarSectionActionButton(string text, Action action, float width)
    {
        Button button = new Button(action) { text = text };
        button.focusable = false;
        button.style.width = width;
        button.style.height = 42f;
        button.style.marginLeft = 8f;
        button.style.fontSize = UiFont(text.Length > 1 ? 17f : 24f);
        button.style.unityFontDefinition = bodyFont;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        StyleSidebarSectionButton(button, new Color(0.78f, 0.84f, 0.94f, 0.96f));
        button.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
        return button;
    }

    private VisualElement CreateSidebarSectionMetadataButton(string text)
    {
        Label label = CreateLabel(text, 17f, new Color(0.72f, 0.78f, 0.88f, 0.96f), true, TextAnchor.MiddleCenter, false);
        label.style.width = 48f;
        label.style.height = 42f;
        label.style.marginLeft = 8f;
        label.style.backgroundColor = new Color(0.058f, 0.068f, 0.086f, 0.70f);
        SetRadius(label, 8f);
        SetBorderWidth(label, 1f);
        SetBorderColor(label, new Color(0.18f, 0.22f, 0.29f, 0.90f));
        return label;
    }

    private static void StyleSidebarSectionButton(Button button, Color textColor)
    {
        if (button == null)
            return;

        SetRadius(button, 8f);
        SetBorderWidth(button, 1f);
        ApplySidebarSectionButtonState(button, textColor, false);
        button.RegisterCallback<MouseEnterEvent>(_ => ApplySidebarSectionButtonState(button, textColor, true));
        button.RegisterCallback<MouseLeaveEvent>(_ => ApplySidebarSectionButtonState(button, textColor, false));
        button.style.paddingLeft = 0f;
        button.style.paddingRight = 0f;
    }

    private static void ApplySidebarSectionButtonState(Button button, Color textColor, bool hover)
    {
        if (button == null)
            return;

        button.style.backgroundColor = hover
            ? new Color(0.100f, 0.116f, 0.146f, 0.94f)
            : new Color(0.058f, 0.068f, 0.086f, 0.70f);
        button.style.color = hover ? Color.white : textColor;
        SetBorderColor(button, hover
            ? new Color(0.42f, 0.48f, 0.58f, 0.92f)
            : new Color(0.18f, 0.22f, 0.29f, 0.92f));
        button.style.opacity = hover ? 1f : 0.96f;
        button.style.scale = hover ? new Scale(new Vector3(1.01f, 1.01f, 1f)) : new Scale(Vector3.one);
    }

    private VisualElement CreateSidebarStaticContent(Func<VisualElement> buildContent)
    {
        VisualElement clip = new VisualElement();
        clip.style.paddingTop = SidebarSectionContentPaddingTop;
        clip.style.paddingBottom = SidebarSectionContentPaddingBottom;
        clip.style.borderTopWidth = 1f;
        clip.style.borderTopColor = new Color(0.12f, 0.15f, 0.20f, 0.88f);

        VisualElement content = buildContent?.Invoke();
        if (content != null)
            clip.Add(content);

        return clip;
    }

    private VisualElement CreateSidebarAnimatedContent(string animationKey, Func<VisualElement> buildContent, float fallbackContentHeight)
    {
        VisualElement clip = new VisualElement();
        clip.style.overflow = Overflow.Hidden;
        clip.style.minHeight = 0f;
        clip.style.borderTopWidth = 1f;
        clip.style.borderTopColor = new Color(0.12f, 0.15f, 0.20f, 0.88f);

        VisualElement inner = new VisualElement();
        inner.style.paddingTop = SidebarSectionContentPaddingTop;
        inner.style.paddingBottom = SidebarSectionContentPaddingBottom;
        VisualElement content = buildContent?.Invoke();
        if (content != null)
            inner.Add(content);
        clip.Add(inner);

        if (!sidebarExpansionAnimations.TryGetValue(animationKey, out SidebarExpansionAnimation animation))
        {
            clip.style.height = StyleKeyword.Auto;
            clip.style.opacity = 1f;
            return clip;
        }

        float fallbackTargetHeight = Mathf.Max(1f, fallbackContentHeight);
        clip.style.height = animation.collapsing ? fallbackTargetHeight : 0f;
        clip.style.opacity = animation.collapsing ? 1f : 0f;

        float fromHeight = animation.collapsing ? fallbackTargetHeight : 0f;
        float toHeight = animation.collapsing ? 0f : fallbackTargetHeight;
        float fromOpacity = animation.collapsing ? 1f : 0f;
        float toOpacity = animation.collapsing ? 0f : 1f;
        clip.style.height = fromHeight;
        clip.style.opacity = fromOpacity;
        AnimateElementHeightAndOpacity(clip, fromHeight, toHeight, fromOpacity, toOpacity, SidebarSectionAnimationSeconds, () =>
        {
            if (sidebarExpansionAnimations.TryGetValue(animationKey, out SidebarExpansionAnimation current) && ReferenceEquals(current, animation))
                sidebarExpansionAnimations.Remove(animationKey);

            if (animation.collapsing)
            {
                Rebuild();
                return;
            }

            clip.style.height = StyleKeyword.Auto;
            clip.style.opacity = 1f;
        });
        return clip;
    }

    private VisualElement CreateSidebarActionButtons()
    {
        VisualElement section = CreateSidebarSectionShell();
        section.style.paddingTop = 12f;
        section.style.paddingBottom = 12f;
        section.Add(CreateSidebarButton("Beat Map Settings", ShowBeatMapSettingsPopup));
        section.Add(CreateSidebarButton("SynchTheory", ShowSynchTheoryPopup));
        return section;
    }

    private VisualElement CreateTracksSidebarContent(List<ChartEditorTrackViewGroup> groups)
    {
        VisualElement container = new VisualElement();
        if (groups == null || groups.Count == 0)
        {
            container.Add(CreateSidebarText("No tracks yet.", new Color(0.70f, 0.74f, 0.82f, 0.92f)));
            return container;
        }

        for (int i = 0; i < groups.Count; i++)
        {
            ChartEditorTrackViewGroup group = groups[i];
            bool selected = group.ContainsSelected(project.selectedTrackId);
            container.Add(CreateTrackSidebarRow(group, selected));
        }

        return container;
    }

    private VisualElement CreateSectionsSidebarContent()
    {
        VisualElement container = new VisualElement();
        if (project.sections == null || project.sections.Count == 0)
        {
            container.Add(CreateSidebarText("No sections yet.", new Color(0.70f, 0.74f, 0.82f, 0.92f)));
            return container;
        }

        int sectionIndex = 0;
        foreach (ChartEditorSection section in project.sections.Take(SidebarMaxSectionRows))
            container.Add(CreateSectionSidebarRow(section, sectionIndex++));
        return container;
    }

    private VisualElement CreateWarningsSidebarContent()
    {
        VisualElement container = new VisualElement();
        if (currentWarnings.Count == 0)
        {
            container.Add(CreateSidebarText("No issues detected.", new Color(0.54f, 0.90f, 0.68f, 0.95f)));
            return container;
        }

        foreach (string warning in currentWarnings.Take(4))
            container.Add(CreateSidebarWarning(warning));
        return container;
    }

    private VisualElement CreateProjectInfoSidebarContent()
    {
        VisualElement container = new VisualElement();
        container.Add(CreateProjectInfoRow("Source", project.sourceKind.ToString()));
        container.Add(CreateProjectInfoRow("Audio", string.IsNullOrWhiteSpace(project.audio?.displayName) ? "None" : project.audio.displayName));
        container.Add(CreateProjectInfoRow("Length", FormatTime(project.DurationSeconds)));
        container.Add(CreateProjectInfoRow("Beat Map", $"{ChartEditorTimingService.GetBeatMarkers(project).Count} beats / {ChartEditorTimingService.GetAnchors(project).Count} anchors"));
        container.Add(CreateProjectInfoRow("Tempo", $"{ChartEditorTimingService.GetTempoAtBeat(project, ChartEditorTimingService.GetBeatPositionForAudioTime(project, project.cursorTimeSeconds)):0.###} BPM"));
        container.Add(CreateSidebarButton("Edit Song Info", ShowSongInfoPopup));
        return container;
    }

    private static float EstimateTracksSidebarContentHeight(List<ChartEditorTrackViewGroup> groups)
    {
        int count = groups?.Count ?? 0;
        return EstimateSidebarContentHeight(count, SidebarTrackRowHeight + SidebarTrackRowGap);
    }

    private float EstimateSectionsSidebarContentHeight()
    {
        int count = project.sections == null ? 0 : Mathf.Min(project.sections.Count, SidebarMaxSectionRows);
        return EstimateSidebarContentHeight(count, SidebarListRowHeight + SidebarListRowGap);
    }

    private float EstimateAnchorsSidebarContentHeight()
    {
        int count = ChartEditorTimingService.GetAnchors(project).Count;
        return EstimateSidebarContentHeight(count, SidebarListRowHeight + SidebarListRowGap);
    }

    private static float EstimateProjectInfoSidebarContentHeight()
    {
        return SidebarSectionContentPaddingTop + SidebarSectionContentPaddingBottom + 5f * 74f + 86f;
    }

    private static float EstimateSidebarContentHeight(int rowCount, float rowStride)
    {
        float contentHeight = rowCount <= 0 ? 48f : rowCount * rowStride;
        return SidebarSectionContentPaddingTop + SidebarSectionContentPaddingBottom + contentHeight;
    }

    private void ToggleSectionsExpanded()
    {
        ToggleSidebarSectionExpanded(SidebarSectionsKey, ref sectionsExpanded);
    }

    private void ToggleTracksExpanded()
    {
        ToggleSidebarSectionExpanded(SidebarTracksKey, ref tracksExpanded);
    }

    private void ToggleAnchorsExpanded()
    {
        ToggleSidebarSectionExpanded(SidebarAnchorsKey, ref anchorsExpanded);
    }

    private void ToggleProjectInfoExpanded()
    {
        ToggleSidebarSectionExpanded(SidebarProjectInfoKey, ref projectInfoExpanded);
    }

    private void ToggleSidebarSectionExpanded(string animationKey, ref bool expanded)
    {
        bool wasExpanded = expanded;
        expanded = !expanded;
        sidebarExpansionAnimations[animationKey] = new SidebarExpansionAnimation
        {
            collapsing = wasExpanded
        };
        Rebuild();
    }

    private VisualElement CreateAnchorsList()
    {
        VisualElement container = new VisualElement();
        List<ChartEditorBeatMarker> anchors = ChartEditorTimingService.GetAnchors(project);
        if (anchors.Count == 0)
        {
            container.Add(CreateSidebarText("No anchors yet.", new Color(0.70f, 0.74f, 0.82f, 0.92f)));
            return container;
        }

        for (int i = 0; i < anchors.Count; i++)
            container.Add(CreateAnchorSidebarRow(anchors[i], i));
        return container;
    }

    private static void AnimateElementHeight(VisualElement element, float from, float to, float durationSeconds, Action onComplete)
    {
        float startTime = Time.unscaledTime;
        bool done = false;
        element.schedule.Execute(() =>
        {
            float t = durationSeconds <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - startTime) / durationSeconds);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            element.style.height = Mathf.Lerp(from, to, eased);
            if (t >= 1f)
            {
                done = true;
                onComplete?.Invoke();
            }
        }).Every(16).Until(() => done);
    }

    private static void AnimateElementHeightAndOpacity(
        VisualElement element,
        float fromHeight,
        float toHeight,
        float fromOpacity,
        float toOpacity,
        float durationSeconds,
        Action onComplete)
    {
        float startTime = Time.unscaledTime;
        bool done = false;
        element.schedule.Execute(() =>
        {
            float t = durationSeconds <= 0f ? 1f : Mathf.Clamp01((Time.unscaledTime - startTime) / durationSeconds);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            element.style.height = Mathf.Lerp(fromHeight, toHeight, eased);
            element.style.opacity = Mathf.Lerp(fromOpacity, toOpacity, eased);
            if (t >= 1f)
            {
                done = true;
                onComplete?.Invoke();
            }
        }).Every(16).Until(() => done);
    }

    private VisualElement CreateTrackSidebarRow(ChartEditorTrackViewGroup group, bool selected)
    {
        ChartEditorTrack track = group?.activeTrack;
        VisualElement row = new VisualElement();
        row.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button == 1)
            {
                ShowTrackContextMenu(evt.position, group);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
                return;

            SelectTrackGroup(group);
            evt.StopPropagation();
        });
        row.style.height = SidebarTrackRowHeight;
        row.style.minHeight = SidebarTrackRowHeight;
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 24f;
        row.style.paddingRight = 22f;
        row.style.marginLeft = 16f;
        row.style.marginRight = 16f;
        row.style.marginBottom = SidebarTrackRowGap;
        row.style.backgroundColor = selected ? new Color(0.070f, 0.078f, 0.096f, 0.72f) : new Color(0f, 0f, 0f, 0f);
        SetRadius(row, 10f);
        SetBorderWidth(row, selected ? 2f : 0f);
        if (selected)
        {
            row.style.borderTopColor = Color.white;
            row.style.borderRightColor = Color.white;
            row.style.borderBottomColor = Color.white;
            row.style.borderLeftColor = Color.white;
        }

        VisualElement textColumn = new VisualElement();
        textColumn.style.flexGrow = 1f;
        Label name = CreateLabel(FormatTrackGroupName(group), 30f, Color.white, true, TextAnchor.MiddleLeft, false);
        name.style.whiteSpace = WhiteSpace.NoWrap;
        string metaText = FirstNonEmpty(track?.tuning?.displayName, track?.role.ToString(), "Standard");
        Label meta = CreateLabel(metaText, 23f, new Color(0.66f, 0.70f, 0.76f, 1f), false, TextAnchor.MiddleLeft, false);
        meta.style.whiteSpace = WhiteSpace.NoWrap;
        textColumn.Add(name);
        textColumn.Add(meta);
        row.Add(textColumn);

        Label count = CreateLabel((group?.NoteCount ?? 0).ToString(), 24f, new Color(0.72f, 0.76f, 0.82f, 1f), false, TextAnchor.MiddleRight, false);
        count.style.width = 82f;
        row.Add(count);
        return row;
    }

    private VisualElement CreateSectionSidebarRow(ChartEditorSection section, int index)
    {
        VisualElement row = new VisualElement();
        row.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (section == null)
                return;

            if (evt.button == 1)
            {
                ShowSectionContextMenu(evt.position, section);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
                return;

            selectedSectionId = section.id;
            ClearNoteSelection();
            selectedSyncPointId = null;
            mode = ChartEditorMode.Sections;
            SeekAndRevealTime(section.startTimeSeconds, syncAudio: true, rebuild: true);
            evt.StopPropagation();
        });
        row.style.height = SidebarListRowHeight;
        row.style.minHeight = SidebarListRowHeight;
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 30f;
        row.style.paddingRight = 24f;
        row.style.marginLeft = 12f;
        row.style.marginRight = 12f;
        row.style.marginBottom = SidebarListRowGap;
        row.style.backgroundColor = string.Equals(selectedSectionId, section?.id, StringComparison.OrdinalIgnoreCase)
            ? new Color(0.080f, 0.070f, 0.110f, 0.94f)
            : new Color(0.040f, 0.047f, 0.058f, 0.48f);
        SetRadius(row, 12f);

        VisualElement dot = new VisualElement();
        dot.style.width = 11f;
        dot.style.height = 11f;
        dot.style.marginRight = 16f;
        dot.style.backgroundColor = SectionColor(index);
        SetRadius(dot, 999f);
        row.Add(dot);

        Label name = CreateLabel(FirstNonEmpty(section?.name, $"Section {index + 1}"), 24f, new Color(0.88f, 0.92f, 0.96f, 1f), false, TextAnchor.MiddleLeft, false);
        name.style.flexGrow = 1f;
        name.style.whiteSpace = WhiteSpace.NoWrap;
        row.Add(name);

        Label time = CreateLabel(FormatTime(section?.startTimeSeconds ?? 0.0), 24f, new Color(0.66f, 0.70f, 0.76f, 1f), false, TextAnchor.MiddleRight, false);
        time.style.width = 132f;
        row.Add(time);
        return row;
    }

    private VisualElement CreateAnchorSidebarRow(ChartEditorBeatMarker anchor, int index)
    {
        bool selected = IsAnchorSelected(anchor);
        VisualElement row = new VisualElement();
        row.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (anchor == null)
                return;

            if (evt.button == 1)
            {
                ShowBeatMarkerContextMenu(evt.position, anchor);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
                return;

            SelectSingleAnchor(anchor);
            SeekAndRevealTime(anchor.audioTimeSeconds, syncAudio: true, rebuild: true);
            evt.StopPropagation();
        });
        row.style.height = SidebarListRowHeight;
        row.style.minHeight = SidebarListRowHeight;
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 30f;
        row.style.paddingRight = 24f;
        row.style.marginLeft = 12f;
        row.style.marginRight = 12f;
        row.style.marginBottom = SidebarListRowGap;
        row.style.backgroundColor = selected
            ? new Color(0.080f, 0.070f, 0.110f, 0.94f)
            : new Color(0.040f, 0.047f, 0.058f, 0.48f);
        SetRadius(row, 12f);

        VisualElement dot = new VisualElement();
        dot.style.width = 11f;
        dot.style.height = 11f;
        dot.style.marginRight = 16f;
        dot.style.backgroundColor = anchor?.locked == true
            ? new Color(1.00f, 0.72f, 0.30f, 1f)
            : new Color(0.74f, 0.46f, 1f, 1f);
        SetRadius(dot, 999f);
        row.Add(dot);

        Label name = CreateLabel(FirstNonEmpty(anchor?.label, $"Anchor {index + 1}"), 24f, new Color(0.88f, 0.92f, 0.96f, 1f), false, TextAnchor.MiddleLeft, false);
        name.style.flexGrow = 1f;
        name.style.whiteSpace = WhiteSpace.NoWrap;
        row.Add(name);

        string beatLabel = anchor == null
            ? string.Empty
            : $"B{Math.Max(1, anchor.barNumber)}.{Math.Max(1, anchor.beatInBar)}";
        Label beat = CreateLabel(beatLabel, 22f, new Color(0.64f, 0.69f, 0.78f, 1f), false, TextAnchor.MiddleRight, false);
        beat.style.width = 92f;
        row.Add(beat);

        Label time = CreateLabel(FormatTime(anchor?.audioTimeSeconds ?? 0.0), 24f, new Color(0.66f, 0.70f, 0.76f, 1f), false, TextAnchor.MiddleRight, false);
        time.style.width = 132f;
        row.Add(time);
        return row;
    }

    private Button CreateSidebarButton(string text, Action action)
    {
        Button button = new Button(action) { text = string.Empty };
        button.focusable = false;
        button.style.flexDirection = FlexDirection.Row;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.SpaceBetween;
        button.style.height = 70f;
        button.style.minHeight = 70f;
        button.style.marginLeft = 24f;
        button.style.marginRight = 22f;
        button.style.marginTop = 8f;
        button.style.marginBottom = 8f;
        button.style.paddingLeft = 22f;
        button.style.paddingRight = 18f;
        button.style.unityFontDefinition = bodyFont;
        StyleSidebarActionButton(button);

        Label label = CreateLabel(text, 23f, new Color(0.91f, 0.94f, 0.98f, 1f), true, TextAnchor.MiddleLeft, false);
        label.style.flexGrow = 1f;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        Label actionLabel = CreateLabel("Open", 19f, new Color(0.74f, 0.80f, 0.90f, 0.95f), true, TextAnchor.MiddleRight, false);
        actionLabel.style.width = 72f;
        actionLabel.style.flexShrink = 0f;
        button.Add(label);
        button.Add(actionLabel);
        return button;
    }

    private static void StyleSidebarActionButton(Button button)
    {
        if (button == null)
            return;

        SetRadius(button, 10f);
        SetBorderWidth(button, 1f);
        ApplySidebarActionButtonState(button, false);
        button.RegisterCallback<MouseEnterEvent>(_ => ApplySidebarActionButtonState(button, true));
        button.RegisterCallback<MouseLeaveEvent>(_ => ApplySidebarActionButtonState(button, false));
    }

    private static void ApplySidebarActionButtonState(Button button, bool hover)
    {
        if (button == null)
            return;

        button.style.backgroundColor = hover
            ? new Color(0.082f, 0.096f, 0.124f, 0.94f)
            : new Color(0.048f, 0.058f, 0.074f, 0.74f);
        SetBorderColor(button, hover
            ? new Color(0.33f, 0.39f, 0.50f, 0.92f)
            : new Color(0.18f, 0.22f, 0.29f, 0.88f));
        button.style.opacity = hover ? 1f : 0.96f;
        button.style.scale = hover ? new Scale(new Vector3(1.01f, 1.01f, 1f)) : new Scale(Vector3.one);
    }

    private Label CreateSidebarText(string text, Color color)
    {
        Label label = CreateLabel(text, 23f, color, false, TextAnchor.MiddleLeft, false);
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginLeft = 26f;
        label.style.marginRight = 22f;
        label.style.marginBottom = 12f;
        return label;
    }

    private VisualElement CreateSidebarWarning(string text)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.paddingLeft = 26f;
        row.style.paddingRight = 22f;
        row.style.marginBottom = 12f;
        Label marker = CreateLabel("!", 26f, new Color(1f, 0.65f, 0.30f, 1f), true, TextAnchor.MiddleCenter, false);
        marker.style.width = 32f;
        row.Add(marker);
        Label detail = CreateLabel(text, 22f, new Color(0.88f, 0.78f, 0.66f, 1f), false, TextAnchor.MiddleLeft, false);
        detail.style.whiteSpace = WhiteSpace.Normal;
        detail.style.flexGrow = 1f;
        row.Add(detail);
        return row;
    }

    private VisualElement CreateProjectInfoRow(string label, string value)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Column;
        row.style.paddingLeft = 26f;
        row.style.paddingRight = 22f;
        row.style.marginBottom = 14f;
        Label left = CreateLabel(label.ToUpperInvariant(), 19f, new Color(0.64f, 0.70f, 0.80f, 1f), true, TextAnchor.MiddleLeft, false);
        left.style.marginBottom = 2f;
        Label right = CreateLabel(value ?? string.Empty, 22f, new Color(0.90f, 0.94f, 0.98f, 1f), false, TextAnchor.MiddleLeft, false);
        right.style.whiteSpace = WhiteSpace.Normal;
        right.style.overflow = Overflow.Hidden;
        row.Add(left);
        row.Add(right);
        return row;
    }

    private VisualElement CreateInspectorHeader(string context)
    {
        VisualElement header = new VisualElement();
        header.style.height = 78f;
        header.style.minHeight = 78f;
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.marginBottom = 22f;
        header.style.borderBottomWidth = 1f;
        header.style.borderBottomColor = new Color(0.16f, 0.20f, 0.26f, 1f);
        Label title = CreateLabel("INSPECTOR", 30f, Color.white, true, TextAnchor.MiddleLeft, false);
        Label type = CreateLabel(context ?? string.Empty, 23f, new Color(0.70f, 0.74f, 0.80f, 1f), true, TextAnchor.MiddleRight, false);
        header.Add(title);
        header.Add(type);
        return header;
    }

    private VisualElement BuildTimelinePanel()
    {
        VisualElement panel = new VisualElement();
        panel.style.flexGrow = 1f;
        panel.style.minWidth = 0f;
        panel.style.marginRight = 18f;
        panel.style.paddingLeft = 0f;
        panel.style.paddingRight = 0f;
        panel.style.paddingTop = 0f;
        panel.style.paddingBottom = 0f;
        StylePanel(panel, new Color(0.030f, 0.036f, 0.048f, 0.99f), new Color(0.17f, 0.21f, 0.27f, 1f), 0f);

        ScrollView scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
        currentTimelineScrollView = scrollView;
        scrollView.style.flexGrow = 1f;
        scrollView.style.minWidth = 0f;
        scrollView.style.minHeight = 0f;
        scrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;
        scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
        StyleModernScrollView(scrollView);
        scrollView.contentContainer.style.flexDirection = FlexDirection.Row;
        scrollView.RegisterCallback<WheelEvent>(evt =>
        {
            if (evt.ctrlKey)
            {
                Vector2 local = scrollView.WorldToLocal(evt.mousePosition);
                float contentX = scrollView.scrollOffset.x + local.x;
                double anchorTime = PixelsToSeconds(Mathf.Max(0f, contentX - TimelineLabelWidth));
                AdjustTimelineZoom(evt.delta.y < 0f ? 1 : -1, anchorTime, local.x);
                evt.StopPropagation();
                return;
            }

            float amount = evt.delta.y * 64f;
            Vector2 offset = scrollView.scrollOffset;
            if (evt.shiftKey)
                offset.y = ClampTimelineVerticalScroll(offset.y + amount, GetTimelineContentHeight());
            else
                offset.x = Mathf.Max(0f, offset.x + amount);

            if (Mathf.Abs(evt.delta.x) > 0.01f)
                offset.x = Mathf.Max(0f, offset.x + evt.delta.x * 64f);

            scrollView.scrollOffset = offset;
            CaptureTimelineScrollOffset(scrollView);
            evt.StopPropagation();
        });
        scrollView.RegisterCallback<PointerMoveEvent>(_ => CaptureTimelineScrollOffset(scrollView));
        scrollView.RegisterCallback<PointerUpEvent>(_ => CaptureTimelineScrollOffset(scrollView));
        scrollView.RegisterCallback<GeometryChangedEvent>(_ =>
        {
            timelineViewportWidth = Mathf.Max(1f, scrollView.contentViewport.layout.width);
            MarkWaveformDirty();
        });
        scrollView.horizontalScroller.valueChanged += _ => CaptureTimelineScrollOffset(scrollView);
        scrollView.verticalScroller.valueChanged += _ => CaptureTimelineScrollOffset(scrollView);

        float timelineWidth = GetTimelineContentWidth();
        float timelineHeight = GetTimelineContentHeight();
        if (!timelineScrollInitialized)
        {
            timelineScrollOffset = new Vector2(Mathf.Max(0f, TimeToPixels(project.cursorTimeSeconds) - 900f), 0f);
            timelineScrollInitialized = true;
        }
        timelineScrollOffset.y = ClampTimelineVerticalScroll(timelineScrollOffset.y, timelineHeight);

        VisualElement timeline = new VisualElement();
        timeline.style.width = timelineWidth;
        timeline.style.minWidth = timelineWidth;
        timeline.style.height = timelineHeight;
        timeline.style.minHeight = timelineHeight;
        timeline.style.flexShrink = 0f;
        timeline.style.position = Position.Relative;
        timeline.style.overflow = Overflow.Hidden;
        timeline.style.backgroundColor = new Color(0.016f, 0.020f, 0.028f, 1f);
        timeline.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button == 1)
            {
                Vector2 menuLocal = timeline.WorldToLocal(evt.position);
                double menuTime = PixelsToSeconds(Mathf.Max(0f, menuLocal.x - TimelineLabelWidth));
                ShowChartContextMenu(evt.position, menuTime);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
                return;

            HideContextMenu();
            Vector2 local = timeline.WorldToLocal(evt.position);
            if (local.x >= TimelineLabelWidth && local.y >= NotesTop)
                StartMarqueeSelection(timeline, evt, local);
            else
                ClearTimelineSelection();

            evt.StopPropagation();
        });
        timeline.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!marqueeSelecting || evt.pointerId != marqueePointerId)
                return;

            UpdateMarqueeSelection(timeline.WorldToLocal(evt.position));
            evt.StopPropagation();
        });
        timeline.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!marqueeSelecting || evt.pointerId != marqueePointerId)
                return;

            FinishMarqueeSelection(timeline.WorldToLocal(evt.position));
            evt.StopPropagation();
        });

        BuildSectionBar(timeline);
        BuildWaveform(timeline);
        BuildWaveformSeekLayer(timeline);
        BuildBeatGrid(timeline);
        BuildNotes(timeline);
        BuildSyncPoints(timeline);
        BuildCursorLine(timeline);
        scrollView.Add(timeline);
        scrollView.schedule.Execute(() =>
        {
            timelineViewportWidth = Mathf.Max(1f, scrollView.contentViewport.layout.width);
            scrollView.scrollOffset = timelineScrollOffset;
            CaptureTimelineScrollOffset(scrollView);
            MarkWaveformDirty();
        });
        panel.Add(scrollView);
        return panel;
    }

    private void ClearTimelineSelection()
    {
        if (!HasTimelineSelection())
        {
            return;
        }

        ClearTimelineSelectionState();
        Rebuild();
    }

    private bool HasTimelineSelection()
    {
        return !string.IsNullOrWhiteSpace(selectedNoteId) ||
               selectedNoteIds.Count > 0 ||
               !string.IsNullOrWhiteSpace(selectedSectionId) ||
               HasSelectedAnchors();
    }

    private void ClearTimelineSelectionState()
    {
        ClearNoteSelection();
        selectedSectionId = null;
        ClearAnchorSelection();
    }

    private void ClearNoteSelection()
    {
        selectedNoteId = null;
        selectedNoteIds.Clear();
    }

    private void ClearAnchorSelection()
    {
        selectedSyncPointId = null;
        selectedSyncPointIds.Clear();
    }

    private void SelectSingleAnchor(ChartEditorBeatMarker anchor)
    {
        selectedSyncPointIds.Clear();
        if (anchor == null || string.IsNullOrWhiteSpace(anchor.id))
        {
            selectedSyncPointId = null;
            return;
        }

        selectedSyncPointId = anchor.id;
        selectedSyncPointIds.Add(anchor.id);
        selectedSectionId = null;
        ClearNoteSelection();
        mode = ChartEditorMode.SyncTiming;
    }

    private void ToggleAnchorSelection(ChartEditorBeatMarker anchor)
    {
        if (anchor == null || string.IsNullOrWhiteSpace(anchor.id))
            return;

        if (!HasActiveAnchorSelectionSet() && !string.IsNullOrWhiteSpace(selectedSyncPointId))
            selectedSyncPointIds.Add(selectedSyncPointId);

        if (selectedSyncPointIds.Contains(anchor.id))
        {
            selectedSyncPointIds.Remove(anchor.id);
            if (string.Equals(selectedSyncPointId, anchor.id, StringComparison.OrdinalIgnoreCase))
                selectedSyncPointId = selectedSyncPointIds.FirstOrDefault();
        }
        else
        {
            selectedSyncPointIds.Add(anchor.id);
            selectedSyncPointId = anchor.id;
        }

        if (selectedSyncPointIds.Count == 0)
            selectedSyncPointId = null;
        else if (string.IsNullOrWhiteSpace(selectedSyncPointId))
            selectedSyncPointId = selectedSyncPointIds.FirstOrDefault();

        selectedSectionId = null;
        ClearNoteSelection();
        mode = ChartEditorMode.SyncTiming;
    }

    private bool HasSelectedAnchors()
    {
        return GetSelectedAnchorCount() > 0;
    }

    private int GetSelectedAnchorCount()
    {
        if (project?.beatMap?.beatMarkers == null)
            return 0;

        return ChartEditorTimingService.GetAnchors(project).Count(IsAnchorSelected);
    }

    private bool HasActiveAnchorSelectionSet()
    {
        return !string.IsNullOrWhiteSpace(selectedSyncPointId) &&
               selectedSyncPointIds.Contains(selectedSyncPointId);
    }

    private bool IsAnchorSelected(ChartEditorBeatMarker anchor)
    {
        if (anchor == null || string.IsNullOrWhiteSpace(anchor.id))
            return false;

        if (HasActiveAnchorSelectionSet())
            return selectedSyncPointIds.Contains(anchor.id);

        return string.Equals(selectedSyncPointId, anchor.id, StringComparison.OrdinalIgnoreCase);
    }

    private List<ChartEditorBeatMarker> GetSelectedAnchors()
    {
        if (project == null)
            return new List<ChartEditorBeatMarker>();

        List<ChartEditorBeatMarker> anchors = ChartEditorTimingService.GetAnchors(project);
        if (anchors.Count == 0)
            return new List<ChartEditorBeatMarker>();

        return anchors
            .Where(IsAnchorSelected)
            .OrderBy(anchor => anchor.beatPosition)
            .ToList();
    }

    private void SelectSingleNote(ChartEditorTrack track, ChartEditorNote note)
    {
        ClearNoteSelection();
        if (track == null || note == null)
            return;

        selectedNoteId = note.id;
        selectedNoteIds.Add(note.id);
        selectedSectionId = null;
        ClearAnchorSelection();
        project.selectedTrackId = track.id;
        mode = ChartEditorMode.Notes;
    }

    private bool IsNoteSelected(ChartEditorNote note)
    {
        if (note == null || string.IsNullOrWhiteSpace(note.id))
            return false;

        return selectedNoteIds.Contains(note.id) ||
               string.Equals(selectedNoteId, note.id, StringComparison.OrdinalIgnoreCase);
    }

    private List<ChartEditorNoteReference> GetSelectedNoteReferences()
    {
        List<ChartEditorNoteReference> selected = new List<ChartEditorNoteReference>();
        if (project?.tracks == null)
            return selected;

        HashSet<string> ids = new HashSet<string>(selectedNoteIds, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(selectedNoteId))
            ids.Add(selectedNoteId);
        if (ids.Count == 0)
            return selected;

        ChartEditorTrack selectedTrack = project.SelectedTrack;
        IEnumerable<ChartEditorTrack> selectableTracks = selectedTrack != null
            ? new[] { selectedTrack }
            : project.tracks.Where(track => track != null && track.visible);

        foreach (ChartEditorTrack track in selectableTracks)
        {
            if (track?.notes == null)
                continue;

            for (int noteIndex = 0; noteIndex < track.notes.Count; noteIndex++)
            {
                ChartEditorNote note = track.notes[noteIndex];
                if (note != null && !string.IsNullOrWhiteSpace(note.id) && ids.Contains(note.id))
                    selected.Add(new ChartEditorNoteReference { track = track, note = note });
            }
        }

        return selected;
    }

    private void SelectNotesAfter(ChartEditorTrack track, ChartEditorNote anchorNote, bool sameStringOnly)
    {
        if (project == null || track?.notes == null || anchorNote == null)
            return;

        const double epsilon = 0.0005;
        List<ChartEditorNote> notes = track.notes
            .Where(note => note != null &&
                           !string.IsNullOrWhiteSpace(note.id) &&
                           note.timeSeconds >= anchorNote.timeSeconds - epsilon &&
                           (!sameStringOnly || note.stringOrLane == anchorNote.stringOrLane))
            .OrderBy(note => note.timeSeconds)
            .ThenBy(note => note.stringOrLane)
            .ThenBy(note => note.fret)
            .ToList();
        if (notes.Count == 0)
        {
            SetStatus("No matching notes found after the selected note.");
            return;
        }

        ClearNoteSelection();
        for (int i = 0; i < notes.Count; i++)
            selectedNoteIds.Add(notes[i].id);

        selectedNoteId = anchorNote.id;
        selectedSectionId = null;
        ClearAnchorSelection();
        project.selectedTrackId = track.id;
        project.cursorTimeSeconds = anchorNote.timeSeconds;
        mode = ChartEditorMode.Notes;
        SetStatus(sameStringOnly
            ? $"Selected {notes.Count} notes after this note on string {anchorNote.stringOrLane + 1}."
            : $"Selected {notes.Count} notes after this note.");
        Rebuild();
    }

    private void SelectNotesInRect(Rect selectionRect)
    {
        List<ChartEditorNoteHit> hits = currentNoteHits
            .Where(hit => hit?.note != null && hit.track != null && selectionRect.Overlaps(hit.rect))
            .OrderBy(hit => hit.note.timeSeconds)
            .ThenBy(hit => hit.note.stringOrLane)
            .ToList();

        if (hits.Count == 0)
        {
            ClearTimelineSelection();
            return;
        }

        ClearNoteSelection();
        for (int i = 0; i < hits.Count; i++)
            selectedNoteIds.Add(hits[i].note.id);

        selectedNoteId = hits[0].note.id;
        selectedSectionId = null;
        ClearAnchorSelection();
        project.selectedTrackId = hits[0].track.id;
        mode = ChartEditorMode.Notes;
        project.cursorTimeSeconds = hits[0].note.timeSeconds;
        Rebuild();
    }

    private void StartMarqueeSelection(VisualElement timeline, PointerDownEvent evt, Vector2 localStart)
    {
        ClearMarqueeSelection();
        marqueeSelecting = true;
        marqueePointerId = evt.pointerId;
        marqueeStart = localStart;
        marqueeMoved = false;
        marqueeTimeline = timeline;

        marqueeBox = new VisualElement();
        marqueeBox.style.position = Position.Absolute;
        marqueeBox.style.left = localStart.x;
        marqueeBox.style.top = localStart.y;
        marqueeBox.style.width = 1f;
        marqueeBox.style.height = 1f;
        marqueeBox.style.backgroundColor = new Color(0.34f, 0.58f, 0.96f, 0.20f);
        marqueeBox.style.borderTopWidth = 2f;
        marqueeBox.style.borderRightWidth = 2f;
        marqueeBox.style.borderBottomWidth = 2f;
        marqueeBox.style.borderLeftWidth = 2f;
        marqueeBox.style.borderTopColor = new Color(0.62f, 0.78f, 1f, 0.90f);
        marqueeBox.style.borderRightColor = new Color(0.62f, 0.78f, 1f, 0.90f);
        marqueeBox.style.borderBottomColor = new Color(0.62f, 0.78f, 1f, 0.90f);
        marqueeBox.style.borderLeftColor = new Color(0.62f, 0.78f, 1f, 0.90f);
        marqueeBox.pickingMode = PickingMode.Ignore;
        timeline.Add(marqueeBox);
        marqueeBox.BringToFront();
        timeline.CapturePointer(marqueePointerId);
    }

    private void UpdateMarqueeSelection(Vector2 localPosition)
    {
        if (marqueeBox == null)
            return;

        Rect rect = CreateRectFromPoints(marqueeStart, localPosition);
        marqueeMoved = marqueeMoved || rect.width > 5f || rect.height > 5f;
        marqueeBox.style.left = rect.xMin;
        marqueeBox.style.top = rect.yMin;
        marqueeBox.style.width = rect.width;
        marqueeBox.style.height = rect.height;
    }

    private void FinishMarqueeSelection(Vector2 localPosition)
    {
        Rect rect = CreateRectFromPoints(marqueeStart, localPosition);
        bool moved = marqueeMoved || rect.width > 5f || rect.height > 5f;
        VisualElement timeline = marqueeTimeline;
        int pointerId = marqueePointerId;
        ClearMarqueeSelection();
        if (timeline != null && timeline.HasPointerCapture(pointerId))
            timeline.ReleasePointer(pointerId);

        if (moved)
            SelectNotesInRect(rect);
        else
            ClearTimelineSelection();
    }

    private void ClearMarqueeSelection()
    {
        if (marqueeTimeline != null && marqueePointerId >= 0 && marqueeTimeline.HasPointerCapture(marqueePointerId))
            marqueeTimeline.ReleasePointer(marqueePointerId);
        marqueeBox?.RemoveFromHierarchy();
        marqueeBox = null;
        marqueeTimeline = null;
        marqueePointerId = -1;
        marqueeSelecting = false;
        marqueeMoved = false;
    }

    private void CaptureCurrentTimelineScrollOffset()
    {
        CaptureTimelineScrollOffset(currentTimelineScrollView);
    }

    private void CaptureTimelineScrollOffset(ScrollView scrollView)
    {
        if (scrollView == null)
            return;

        Vector2 previous = timelineScrollOffset;
        timelineScrollOffset = scrollView.scrollOffset;
        timelineScrollOffset.y = ClampTimelineVerticalScroll(timelineScrollOffset.y, GetTimelineContentHeight());
        timelineScrollInitialized = true;
        timelineViewportWidth = Mathf.Max(1f, scrollView.contentViewport.layout.width);
        RefreshIosScrollIndicators(scrollView);
        if (Mathf.Abs(previous.x - timelineScrollOffset.x) > 1f)
            MarkWaveformDirty();
    }

    private static Rect CreateRectFromPoints(Vector2 a, Vector2 b)
    {
        float xMin = Mathf.Min(a.x, b.x);
        float yMin = Mathf.Min(a.y, b.y);
        return new Rect(xMin, yMin, Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    private void ShowChartContextMenu(Vector2 worldPosition, double timeSeconds)
    {
        if (project == null)
            return;

        double safeTime = Math.Max(0.0, Math.Min(project.DurationSeconds, timeSeconds));
        ChartEditorBeatMarker nearestBeat = ChartEditorTimingService.GetNearestBeatMarker(project, safeTime);
        List<ContextMenuItem> items = new List<ContextMenuItem>
        {
            new ContextMenuItem($"Add Note at {FormatTime(safeTime)}", () => AddNoteAtTime(safeTime)),
            new ContextMenuItem($"Add Section at {FormatTime(safeTime)}", () => AddSectionAtTime(safeTime)),
            new ContextMenuItem(nearestBeat == null
                ? $"Add Anchor at {FormatTime(safeTime)}"
                : $"Add Anchor on Beat {nearestBeat.beatPosition:0.###}", () =>
                {
                    if (nearestBeat != null)
                        ConvertBeatMarkerToAnchor(nearestBeat);
                    else
                        AddSyncPointAtTime(safeTime);
                })
        };

        if (HasCopiedNotes())
            items.Add(new ContextMenuItem($"Paste Notes at {FormatTime(safeTime)}", () => PasteCopiedNotesAt(safeTime)));

        items.Add(new ContextMenuItem("Beat Map Tools",
                new ContextMenuItem("Beat Map Settings...", ShowBeatMapSettingsPopup),
                new ContextMenuItem("SynchTheory...", ShowSynchTheoryPopup),
                new ContextMenuItem("Set Region BPM Here...", () => ShowSetRegionBpmPopup(ChartEditorTimingService.GetBeatPositionForAudioTime(project, safeTime))),
                new ContextMenuItem("Set Time Signature Here...", () => ShowTimeSignaturePopup(ChartEditorTimingService.GetBeatPositionForAudioTime(project, safeTime))),
                new ContextMenuItem("Quantize Selected Notes", QuantizeSelectedNotesToBeatGrid),
                new ContextMenuItem(project.settings.showBeatGrid ? "Hide Beat Grid" : "Show Beat Grid", () =>
                {
                    project.settings.showBeatGrid = !project.settings.showBeatGrid;
                    project.dirty = true;
                    Rebuild();
                }),
                new ContextMenuItem(project.settings.metronomeEnabled ? "Metronome: OFF" : "Metronome: ON", () =>
                {
                    project.settings.metronomeEnabled = !project.settings.metronomeEnabled;
                    project.dirty = true;
                    Rebuild();
                }),
                new ContextMenuItem(project.settings.noteClapsEnabled ? "Note Claps: OFF" : "Note Claps: ON", () =>
                {
                    project.settings.noteClapsEnabled = !project.settings.noteClapsEnabled;
                    project.dirty = true;
                    Rebuild();
                }),
                new ContextMenuItem("Apply Beat Map", () =>
                {
                    ChartEditorTimingService.ApplyBeatMapToContent(project);
                    project.dirty = true;
                    Rebuild();
                }),
                new ContextMenuItem("Move Entire Chart -100ms", () => MoveScope(-0.1)),
                new ContextMenuItem("Move Entire Chart +100ms", () => MoveScope(0.1)),
                new ContextMenuItem("Move After Cursor +100ms", () =>
                {
                    ChartEditorTimingService.MoveEverythingAfter(project, project.cursorTimeSeconds, 0.1);
                    project.dirty = true;
                    Rebuild();
                }),
                new ContextMenuItem("Stretch Next 8s +1%", () =>
                {
                    ChartEditorTimingService.StretchRegion(project, project.cursorTimeSeconds, project.cursorTimeSeconds + 8.0, 1.01);
                    project.dirty = true;
                    Rebuild();
                })));
        items.Add(new ContextMenuItem("Zoom In", () => AdjustTimelineZoomAroundViewportCenter(1)));
        items.Add(new ContextMenuItem("Zoom Out", () => AdjustTimelineZoomAroundViewportCenter(-1)));
        items.Add(new ContextMenuItem("Reset Zoom", ResetTimelineZoom));
        ShowContextMenu(worldPosition, items.ToArray());
    }

    private void ShowTrackContextMenu(Vector2 worldPosition, ChartEditorTrackViewGroup group)
    {
        ChartEditorTrack track = group?.activeTrack;
        if (track == null)
            return;

        ShowContextMenu(worldPosition,
            new ContextMenuItem("Edit Track", () => ShowTrackEditPopup(track)),
            new ContextMenuItem(group.ContainsSelected(project.selectedTrackId) ? "Selected: ON" : "Show This Track", () => SelectTrackGroup(group)),
            new ContextMenuItem(track.muted ? "Muted: ON" : "Muted: OFF", () =>
            {
                track.muted = !track.muted;
                project.dirty = true;
                Rebuild();
            }),
            new ContextMenuItem(track.solo ? "Solo: ON" : "Solo: OFF", () =>
            {
                track.solo = !track.solo;
                project.dirty = true;
                Rebuild();
            }),
            new ContextMenuItem("Delete Track", () => DeleteTrack(track)),
            new ContextMenuItem("Edit Song Info", ShowSongInfoPopup));
    }

    private void ShowNoteContextMenu(Vector2 worldPosition, ChartEditorTrack track, ChartEditorNote note)
    {
        if (note == null)
            return;

        if (!IsNoteSelected(note))
            SelectSingleNote(track, note);

        List<ChartEditorNoteReference> selectedNotes = GetSelectedNoteReferences();
        if (selectedNotes.Count > 1)
        {
            ShowSelectedNotesContextMenu(worldPosition, selectedNotes, note.timeSeconds, track, note);
            return;
        }

        List<ContextMenuItem> items = new List<ContextMenuItem>
        {
            new ContextMenuItem("Edit Note", () => ShowNoteEditPopup(track, note)),
            new ContextMenuItem("Technique Settings...", () => ShowTechniqueSettingsPopup(track, note)),
            new ContextMenuItem("Move to Cursor", MoveSelectedNotesToCursor),
            new ContextMenuItem("Selection",
                new ContextMenuItem("Select After on This String", () => SelectNotesAfter(track, note, sameStringOnly: true)),
                new ContextMenuItem("Select After on All Strings", () => SelectNotesAfter(track, note, sameStringOnly: false))),
            new ContextMenuItem("Techniques", BuildTechniqueContextItems(new[] { new ChartEditorNoteReference { track = track, note = note } })),
            new ContextMenuItem("Copy Note", CopySelectedNotes),
            new ContextMenuItem("Quantize to Beat Grid", QuantizeSelectedNotesToBeatGrid),
            new ContextMenuItem("Duplicate Note", () => DuplicateNote(note)),
            new ContextMenuItem("Delete Note", () => DeleteNote(note)),
            new ContextMenuItem("Add Note Here", () => AddNoteAtTime(note.timeSeconds))
        };
        if (HasCopiedNotes())
            items.Insert(4, new ContextMenuItem("Paste Notes Here", () => PasteCopiedNotesAt(note.timeSeconds)));
        ShowContextMenu(worldPosition, items.ToArray());
    }

    private void ShowSelectedNotesContextMenu(Vector2 worldPosition, List<ChartEditorNoteReference> selectedNotes, double pasteTimeSeconds, ChartEditorTrack clickedTrack = null, ChartEditorNote clickedNote = null)
    {
        int count = selectedNotes?.Count ?? 0;
        if (count <= 1)
            return;

        List<ContextMenuItem> items = new List<ContextMenuItem>
        {
            new ContextMenuItem($"Move {count} Selected Notes...", ShowMoveSelectedNotesPopup),
            new ContextMenuItem("Move Selected to Cursor", MoveSelectedNotesToCursor),
            new ContextMenuItem("Selection",
                new ContextMenuItem("Select After on This String", () => SelectNotesAfter(clickedTrack, clickedNote, sameStringOnly: true)),
                new ContextMenuItem("Select After on All Strings", () => SelectNotesAfter(clickedTrack, clickedNote, sameStringOnly: false))),
            new ContextMenuItem("Techniques", BuildTechniqueContextItems(selectedNotes)),
            new ContextMenuItem($"Copy {count} Notes", CopySelectedNotes),
            new ContextMenuItem("Quantize Selected to Beat Grid", QuantizeSelectedNotesToBeatGrid),
            new ContextMenuItem($"Duplicate {count} Notes", DuplicateSelectedNotes),
            new ContextMenuItem($"Delete {count} Notes", DeleteSelectedNotes)
        };
        if (HasCopiedNotes())
            items.Insert(4, new ContextMenuItem("Paste Notes Here", () => PasteCopiedNotesAt(pasteTimeSeconds)));
        ShowContextMenu(worldPosition, items.ToArray());
    }

    private void ShowSectionContextMenu(Vector2 worldPosition, ChartEditorSection section)
    {
        if (section == null)
            return;

        ShowContextMenu(worldPosition,
            new ContextMenuItem("Edit Section", () => ShowSectionEditPopup(section)),
            new ContextMenuItem("Add Section After", () => AddSectionAtTime(section.endTimeSeconds)),
            new ContextMenuItem("Add Anchor at Start", () => AddSyncPointAtTime(section.startTimeSeconds)),
            new ContextMenuItem("Delete Section", () => DeleteSection(section)));
    }

    private void ShowSyncPointContextMenu(Vector2 worldPosition, ChartEditorBeatMarker point)
    {
        if (point == null)
            return;

        if (IsAnchorSelected(point) && GetSelectedAnchorCount() > 1)
        {
            ShowSelectedAnchorsContextMenu(worldPosition, point);
            return;
        }

        ShowContextMenu(worldPosition,
            new ContextMenuItem("Edit Anchor", () => ShowSyncPointEditPopup(point)),
            new ContextMenuItem("Move Anchor to Cursor", () => MoveAnchorToCursor(point)),
            new ContextMenuItem("Set Region BPM...", () => ShowSetRegionBpmPopup(point.beatPosition)),
            new ContextMenuItem("Set Time Signature Here...", () => ShowTimeSignaturePopup(point.beatPosition)),
            new ContextMenuItem(point.locked ? "Locked: ON" : "Locked: OFF", () =>
            {
                point.locked = !point.locked;
                project.dirty = true;
                Rebuild();
            }),
            new ContextMenuItem("Apply Beat Map", () =>
            {
                ChartEditorTimingService.ApplyBeatMapToContent(project);
                project.dirty = true;
                Rebuild();
            }),
            new ContextMenuItem("Delete Anchor", () =>
            {
                ChartEditorTimingService.RemoveAnchor(project, point);
                ClearAnchorSelection();
                project.dirty = true;
                Rebuild();
            }));
    }

    private void ShowSelectedAnchorsContextMenu(Vector2 worldPosition, ChartEditorBeatMarker clickedPoint)
    {
        List<ChartEditorBeatMarker> anchors = GetSelectedAnchors();
        if (anchors.Count <= 1)
        {
            ShowSyncPointContextMenu(worldPosition, clickedPoint ?? anchors.FirstOrDefault());
            return;
        }

        ChartEditorBeatMarker first = anchors.First();
        ChartEditorBeatMarker last = anchors.Last();
        ShowContextMenu(worldPosition,
            new ContextMenuItem($"SynchTheory Between {anchors.Count} Selected Anchors", () => RunSynchTheoryBetweenSelectedAnchors(moveContent: true)),
            new ContextMenuItem($"Set Cursor to First Anchor ({FormatTime(first.audioTimeSeconds)})", () =>
            {
                SeekAndRevealTime(first.audioTimeSeconds, syncAudio: true, rebuild: false);
            }),
            new ContextMenuItem($"Set Region BPM at First Anchor...", () => ShowSetRegionBpmPopup(first.beatPosition)),
            new ContextMenuItem($"Lock {anchors.Count} Anchors", () => SetSelectedAnchorsLocked(true)),
            new ContextMenuItem($"Unlock {anchors.Count} Anchors", () => SetSelectedAnchorsLocked(false)),
            new ContextMenuItem("Clear Anchor Selection", () =>
            {
                ClearAnchorSelection();
                Rebuild();
            }),
            new ContextMenuItem("Apply Beat Map", () =>
            {
                ChartEditorTimingService.ApplyBeatMapToContent(project);
                project.dirty = true;
                Rebuild();
            }),
            new ContextMenuItem($"Delete {anchors.Count} Anchors", DeleteSelectedAnchors));
    }

    private void ShowTechniqueSegmentContextMenu(Vector2 worldPosition, ChartEditorTrack track, ChartEditorNote note, ChartEditorTechniqueSegment segment)
    {
        if (note == null || segment == null)
            return;

        SelectSingleNote(track, note);
        ShowContextMenu(worldPosition,
            new ContextMenuItem("Technique Settings...", () => ShowTechniqueSettingsPopup(track, note)),
            new ContextMenuItem("Edit Segment", () => ShowTechniqueSegmentEditPopup(note, segment)),
            new ContextMenuItem("Delete Segment", () => DeleteTechniqueSegment(note, segment)),
            new ContextMenuItem("Set Type",
                new ContextMenuItem("Sustain", () => SetTechniqueSegmentType(note, segment, NoteTechniqueSegmentType.Sustain)),
                new ContextMenuItem("Vibrato", () => SetTechniqueSegmentType(note, segment, NoteTechniqueSegmentType.Vibrato)),
                new ContextMenuItem("Bend", () => SetTechniqueSegmentType(note, segment, NoteTechniqueSegmentType.Bend)),
                new ContextMenuItem("Slide", () => SetTechniqueSegmentType(note, segment, NoteTechniqueSegmentType.Slide))),
            new ContextMenuItem("Add Segment After",
                new ContextMenuItem("Sustain", () => AddTechniqueSegmentAfter(note, segment, NoteTechniqueSegmentType.Sustain)),
                new ContextMenuItem("Vibrato", () => AddTechniqueSegmentAfter(note, segment, NoteTechniqueSegmentType.Vibrato)),
                new ContextMenuItem("Half Bend", () => AddBendTechniqueSegmentAfter(note, segment, 0f, HalfStepBendSemitones)),
                new ContextMenuItem("Full Bend", () => AddBendTechniqueSegmentAfter(note, segment, 0f, FullStepBendSemitones)),
                new ContextMenuItem("Half Release", () => AddBendTechniqueSegmentAfter(note, segment, HalfStepBendSemitones, 0f)),
                new ContextMenuItem("Full Release", () => AddBendTechniqueSegmentAfter(note, segment, FullStepBendSemitones, 0f)),
                new ContextMenuItem("Slide", () => AddTechniqueSegmentAfter(note, segment, NoteTechniqueSegmentType.Slide))));
    }

    private void AddTechniqueSegmentAfter(ChartEditorNote note, ChartEditorTechniqueSegment after, NoteTechniqueSegmentType type)
    {
        if (note == null || after == null)
            return;

        AddTechniqueSegmentToNote(note, type, Mathf.Max(after.startOffset, after.endOffset));
        project.dirty = true;
        Rebuild();
    }

    private void AddBendTechniqueSegmentAfter(ChartEditorNote note, ChartEditorTechniqueSegment after, float startBend, float endBend)
    {
        if (note == null || after == null)
            return;

        AddTechniqueSegmentToNote(note, NoteTechniqueSegmentType.Bend, Mathf.Max(after.startOffset, after.endOffset), segment =>
        {
            segment.startBend = Mathf.Max(0f, startBend);
            segment.endBend = Mathf.Max(0f, endBend);
        });
        project.dirty = true;
        Rebuild();
    }

    private void SetTechniqueSegmentType(ChartEditorNote note, ChartEditorTechniqueSegment segment, NoteTechniqueSegmentType type)
    {
        if (note == null || segment == null)
            return;

        bool bendTypeChanged = IsBendBearingTechniqueSegment(segment) || type == NoteTechniqueSegmentType.Bend;
        segment.type = type;
        ApplyTechniqueSegmentTypeDefaults(note, segment);
        NormalizeTechniqueSegmentLayout(note, segment);
        if (bendTypeChanged && note.bendPoints != null)
            note.bendPoints.Clear();
        ApplyTechniqueSegmentSummaries(note);
        NormalizePrimaryTechnique(note);
        project.dirty = true;
        Rebuild();
    }

    private void DeleteTechniqueSegment(ChartEditorNote note, ChartEditorTechniqueSegment segment)
    {
        if (note?.techniqueSegments == null || segment == null)
            return;

        bool removedBend = IsBendBearingTechniqueSegment(segment);
        note.techniqueSegments.Remove(segment);
        if (note.techniqueSegments.Count > 0)
            SyncNoteDurationToTechniqueSegments(note, allowShrink: true);
        if (removedBend && note.bendPoints != null)
            note.bendPoints.Clear();
        ApplyTechniqueSegmentSummaries(note);
        NormalizePrimaryTechnique(note);
        project.dirty = true;
        Rebuild();
    }

    private void ShowContextMenu(Vector2 worldPosition, params ContextMenuItem[] items)
    {
        HideContextMenu();
        if (items == null || items.Length == 0)
            return;

        Vector2 rootPosition = RootElement.WorldToLocal(worldPosition);
        VisualElement overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.right = 0f;
        overlay.style.top = 0f;
        overlay.style.bottom = 0f;
        overlay.pickingMode = PickingMode.Position;
        overlay.RegisterCallback<PointerDownEvent>(evt =>
        {
            HideContextMenu();
            evt.StopPropagation();
        });

        VisualElement menu = CreateContextMenuSurface(ContextMenuWidth);
        PositionContextMenu(menu, rootPosition, ContextMenuWidth);
        menu.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

        AddContextMenuRows(menu, items, false);

        overlay.Add(menu);
        contextMenuElement = overlay;
        RootElement.Add(contextMenuElement);
        contextMenuElement.BringToFront();
        SetChartEditorKeyboardCaptureActive(true);
    }

    private void HideContextMenu()
    {
        HideContextSubmenu();
        if (contextMenuElement == null)
            return;

        contextMenuElement.RemoveFromHierarchy();
        contextMenuElement = null;
        UpdateChartEditorKeyboardCaptureState();
    }

    private void ShowContextSubmenu(VisualElement parentButton, ContextMenuItem[] items)
    {
        HideContextSubmenu();
        if (contextMenuElement == null || parentButton == null || items == null || items.Length == 0)
            return;

        Vector2 position = RootElement.WorldToLocal(new Vector2(parentButton.worldBound.xMax + 10f, parentButton.worldBound.yMin - 10f));
        if (RootElement.resolvedStyle.width > ContextSubmenuWidth + 36f &&
            position.x + ContextSubmenuWidth > RootElement.resolvedStyle.width - 18f)
        {
            position = RootElement.WorldToLocal(new Vector2(parentButton.worldBound.xMin - ContextSubmenuWidth - 10f, parentButton.worldBound.yMin - 10f));
        }

        VisualElement menu = CreateContextMenuSurface(ContextSubmenuWidth);
        PositionContextMenu(menu, position, ContextSubmenuWidth);
        menu.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

        AddContextMenuRows(menu, items, true);

        contextSubmenuElement = menu;
        contextMenuElement.Add(contextSubmenuElement);
        contextSubmenuElement.BringToFront();
        SetChartEditorKeyboardCaptureActive(true);
    }

    private VisualElement CreateContextMenuSurface(float width)
    {
        VisualElement menu = new VisualElement();
        menu.style.position = Position.Absolute;
        menu.style.width = width;
        menu.style.minWidth = width;
        menu.style.paddingLeft = 12f;
        menu.style.paddingRight = 12f;
        menu.style.paddingTop = 12f;
        menu.style.paddingBottom = 12f;
        StylePopupPanel(menu, new Color(0.022f, 0.026f, 0.034f, 0.99f), 14f);
        return menu;
    }

    private void PositionContextMenu(VisualElement menu, Vector2 rootPosition, float fallbackWidth)
    {
        if (menu == null)
            return;

        const float edgePadding = 18f;
        float left = Mathf.Max(edgePadding, rootPosition.x);
        float top = Mathf.Max(edgePadding, rootPosition.y);
        float rootWidth = RootElement != null ? RootElement.resolvedStyle.width : 0f;
        if (rootWidth > fallbackWidth + edgePadding * 2f)
            left = Mathf.Min(left, rootWidth - fallbackWidth - edgePadding);

        menu.style.left = left;
        menu.style.top = top;
        menu.schedule.Execute(() =>
        {
            if (RootElement == null || menu.parent == null)
                return;

            float resolvedWidth = Mathf.Max(fallbackWidth, menu.resolvedStyle.width);
            float resolvedHeight = Mathf.Max(1f, menu.resolvedStyle.height);
            float resolvedRootWidth = RootElement.resolvedStyle.width;
            float resolvedRootHeight = RootElement.resolvedStyle.height;
            if (resolvedRootWidth > resolvedWidth + edgePadding * 2f)
                menu.style.left = Mathf.Clamp(left, edgePadding, resolvedRootWidth - resolvedWidth - edgePadding);
            if (resolvedRootHeight > resolvedHeight + edgePadding * 2f)
                menu.style.top = Mathf.Clamp(top, edgePadding, resolvedRootHeight - resolvedHeight - edgePadding);
        });
    }

    private void AddContextMenuRows(VisualElement menu, ContextMenuItem[] items, bool nested)
    {
        ContextMenuItem previous = null;
        for (int i = 0; i < items.Length; i++)
        {
            ContextMenuItem item = items[i];
            if (item == null)
                continue;

            if (ShouldSeparateContextItems(previous, item))
                menu.Add(CreateContextMenuSeparator());

            menu.Add(CreateContextMenuRow(item, nested));
            previous = item;
        }
    }

    private Button CreateContextMenuRow(ContextMenuItem item, bool nested)
    {
        bool hasChildren = item.children != null && item.children.Length > 0;
        bool destructive = IsDestructiveContextItem(item.label);
        Color normalText = destructive
            ? new Color(1.00f, 0.48f, 0.44f, 1f)
            : new Color(0.93f, 0.96f, 1f, 1f);
        Color secondaryText = destructive
            ? new Color(1.00f, 0.58f, 0.54f, 0.82f)
            : new Color(0.62f, 0.70f, 0.80f, 0.92f);
        Color hover = destructive
            ? new Color(0.42f, 0.070f, 0.080f, 0.34f)
            : new Color(1f, 1f, 1f, 0.070f);

        Button button = new Button();
        button.text = string.Empty;
        button.focusable = false;
        button.style.height = ContextMenuRowHeight;
        button.style.minHeight = ContextMenuRowHeight;
        button.style.marginLeft = 0f;
        button.style.marginRight = 0f;
        button.style.marginTop = 2f;
        button.style.marginBottom = 2f;
        button.style.paddingLeft = 20f;
        button.style.paddingRight = 20f;
        button.style.paddingTop = 0f;
        button.style.paddingBottom = 0f;
        button.style.flexDirection = FlexDirection.Row;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.SpaceBetween;
        button.style.backgroundColor = Color.clear;
        button.style.color = normalText;
        button.style.unityFontDefinition = bodyFont;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.fontSize = UiFont(23f);
        button.style.opacity = 0.96f;
        button.style.scale = new Scale(Vector3.one);
        SetRadius(button, 10f);
        SetBorderWidth(button, 0f);

        Label label = CreateLabel(item.label, 23f, normalText, true, TextAnchor.MiddleLeft, false);
        label.style.flexGrow = 1f;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        label.style.overflow = Overflow.Hidden;
        button.Add(label);

        if (hasChildren)
        {
            Label chevron = CreateLabel("›", 28f, secondaryText, true, TextAnchor.MiddleCenter, false);
            chevron.text = "More";
            chevron.style.fontSize = UiFont(18f);
            chevron.style.width = 74f;
            chevron.style.unityTextAlign = TextAnchor.MiddleRight;
            chevron.style.marginLeft = 18f;
            chevron.style.flexShrink = 0f;
            button.Add(chevron);
        }

        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            button.style.backgroundColor = hover;
            button.style.opacity = 1f;
            button.style.scale = new Scale(new Vector3(1.01f, 1.01f, 1f));
            if (hasChildren)
                ShowContextSubmenu(button, item.children);
            else if (!nested)
                HideContextSubmenu();
        });
        button.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            button.style.backgroundColor = Color.clear;
            button.style.opacity = 0.96f;
            button.style.scale = new Scale(Vector3.one);
        });
        button.RegisterCallback<PointerDownEvent>(evt =>
        {
            evt.StopPropagation();
            if (hasChildren)
                ShowContextSubmenu(button, item.children);
        });
        button.clicked += () =>
        {
            if (hasChildren)
            {
                ShowContextSubmenu(button, item.children);
                return;
            }

            HideContextMenu();
            item.action?.Invoke();
        };

        return button;
    }

    private static VisualElement CreateContextMenuSeparator()
    {
        VisualElement separator = new VisualElement();
        separator.style.height = 1f;
        separator.style.marginLeft = 10f;
        separator.style.marginRight = 10f;
        separator.style.marginTop = 7f;
        separator.style.marginBottom = 7f;
        separator.style.backgroundColor = new Color(1f, 1f, 1f, 0.10f);
        return separator;
    }

    private static bool ShouldSeparateContextItems(ContextMenuItem previous, ContextMenuItem current)
    {
        if (previous == null || current == null)
            return false;

        string label = current.label ?? string.Empty;
        return label.StartsWith("Timing Tools", StringComparison.OrdinalIgnoreCase) ||
               label.StartsWith("Beat Map Tools", StringComparison.OrdinalIgnoreCase) ||
               label.StartsWith("Zoom ", StringComparison.OrdinalIgnoreCase) ||
               IsDestructiveContextItem(label);
    }

    private static bool IsDestructiveContextItem(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return false;

        return label.IndexOf("delete", StringComparison.OrdinalIgnoreCase) >= 0 ||
               label.IndexOf("remove", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void HideContextSubmenu()
    {
        contextSubmenuElement?.RemoveFromHierarchy();
        contextSubmenuElement = null;
    }

    private bool HandleOverlayKeyboardInput()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return false;

        if (contextSubmenuElement != null)
        {
            HideContextSubmenu();
            UpdateChartEditorKeyboardCaptureState();
            ResetArrowRepeat();
            return true;
        }

        if (contextMenuElement != null)
        {
            HideContextMenu();
            ResetArrowRepeat();
            return true;
        }

        if (editPopupElement != null)
        {
            HideEditPopup();
            ResetArrowRepeat();
            return true;
        }

        return false;
    }

    private void RegisterTextFieldKeyboardCapture(TextField field)
    {
        if (field == null)
            return;

        field.RegisterCallback<FocusInEvent>(_ => SetChartEditorKeyboardCaptureActive(true), TrickleDown.TrickleDown);
        field.RegisterCallback<FocusOutEvent>(_ => UpdateChartEditorKeyboardCaptureState(), TrickleDown.TrickleDown);
        field.RegisterCallback<DetachFromPanelEvent>(_ => UpdateChartEditorKeyboardCaptureState());
    }

    private void SetChartEditorKeyboardCaptureActive(bool active)
    {
        owner?.SetKeyboardTextInputActiveFromUi(active);
    }

    private void UpdateChartEditorKeyboardCaptureState()
    {
        bool active = editPopupElement != null ||
                      contextMenuElement != null ||
                      contextSubmenuElement != null ||
                      IsTextFieldFocused();
        owner?.SetKeyboardTextInputActiveFromUi(active);
    }

    private void SelectTrackGroup(ChartEditorTrackViewGroup group)
    {
        ChartEditorTrack track = group?.activeTrack ?? group?.tracks?.FirstOrDefault(candidate => candidate != null);
        if (track == null)
            return;

        project.selectedTrackId = track.id;
        SetExclusiveVisibleTrack(track, markDirty: true);
        ClearNoteSelection();
        Rebuild();
    }

    private void DeleteTrack(ChartEditorTrack track)
    {
        if (project?.tracks == null || track == null)
            return;

        int index = project.tracks.FindIndex(candidate =>
            ReferenceEquals(candidate, track) ||
            (!string.IsNullOrWhiteSpace(candidate?.id) &&
             string.Equals(candidate.id, track.id, StringComparison.OrdinalIgnoreCase)));
        if (index < 0)
            return;

        string deletedName = FormatTrackName(track);
        bool deletedSelected = string.Equals(project.selectedTrackId, track.id, StringComparison.OrdinalIgnoreCase);
        project.tracks.RemoveAt(index);
        ClearNoteSelection();
        selectedSectionId = null;
        ClearAnchorSelection();

        if (project.tracks.Count == 0)
        {
            project.selectedTrackId = null;
        }
        else if (deletedSelected || project.SelectedTrack == null)
        {
            int nextIndex = Mathf.Clamp(index, 0, project.tracks.Count - 1);
            ChartEditorTrack nextTrack = project.tracks[nextIndex];
            project.selectedTrackId = nextTrack?.id;
            if (nextTrack != null)
                SetExclusiveVisibleTrack(nextTrack, markDirty: false);
        }

        project.dirty = true;
        MarkHighwayPreviewDirty();
        SetStatus($"Deleted track \"{deletedName}\".");
        Rebuild();
    }

    private void EnsureSingleVisibleTrack(bool markDirty)
    {
        if (project?.tracks == null || project.tracks.Count == 0)
            return;

        ChartEditorTrack selectedTrack = project.SelectedTrack;
        if (selectedTrack == null)
        {
            selectedTrack = project.tracks
                .Where(track => track != null && track.visible)
                .OrderByDescending(track => track.notes?.Count ?? 0)
                .FirstOrDefault()
                ?? project.tracks
                    .Where(track => track != null)
                    .OrderByDescending(track => track.notes?.Count ?? 0)
                    .FirstOrDefault();
        }

        if (selectedTrack == null)
            return;

        project.selectedTrackId = selectedTrack.id;
        SetExclusiveVisibleTrack(selectedTrack, markDirty);
    }

    private void SetExclusiveVisibleTrack(ChartEditorTrack visibleTrack, bool markDirty)
    {
        if (project?.tracks == null || visibleTrack == null)
            return;

        bool changed = false;
        for (int i = 0; i < project.tracks.Count; i++)
        {
            ChartEditorTrack track = project.tracks[i];
            if (track == null)
                continue;

            bool shouldBeVisible = ReferenceEquals(track, visibleTrack);
            if (track.visible != shouldBeVisible)
            {
                track.visible = shouldBeVisible;
                changed = true;
            }
        }

        if (changed && markDirty)
            project.dirty = true;
    }

    private void ShowTrackEditPopup(ChartEditorTrack track)
    {
        if (track == null)
            return;

        TextField nameField = CreatePopupTextField("Name", track.displayName ?? string.Empty);
        DropdownField roleDropdown = CreatePopupDropdownField(
            "Instrument Tag",
            TrackRoleChoiceLabels().ToList(),
            TrackRoleToChoiceLabel(track.role));
        ShowEditPopup("Edit Track", new VisualElement[] { nameField, roleDropdown }, () =>
        {
            if (!TryChoiceLabelToTrackRole(roleDropdown.value, out ChartEditorTrackRole role))
            {
                SetStatus("Choose a valid instrument tag.");
                return false;
            }

            bool roleChanged = track.role != role;
            track.displayName = string.IsNullOrWhiteSpace(nameField.value) ? FormatTrackName(track) : nameField.value.Trim();
            track.role = role;
            track.colorHex = DefaultColorHexForRole(role);
            if (roleChanged)
                ApplyGeneratedPartRoleDefaults(track, role);
            project.dirty = true;
            MarkHighwayPreviewDirty();
            HideEditPopup();
            Rebuild();
            return true;
        });
    }

    private void ShowNoteEditPopup(ChartEditorTrack track, ChartEditorNote note)
    {
        if (track == null || note == null)
            return;

        int laneCount = GetTrackLaneCount(track);
        bool editFret = track.role != ChartEditorTrackRole.Drums && IsStringInstrument(track);
        TextField fretField = editFret ? CreatePopupTextField("Fret", note.fret.ToString(CultureInfo.InvariantCulture)) : null;
        TextField laneField = CreatePopupTextField(IsStringInstrument(track) ? "String" : "Lane", note.stringOrLane.ToString(CultureInfo.InvariantCulture));
        TextField timeField = CreatePopupTextField("Time Seconds", note.timeSeconds.ToString("0.000", CultureInfo.InvariantCulture));
        TextField durationField = CreatePopupTextField("Duration Seconds", GetNoteEffectiveDurationSeconds(note).ToString("0.000", CultureInfo.InvariantCulture));

        List<VisualElement> fields = new List<VisualElement>();
        if (fretField != null)
            fields.Add(fretField);
        fields.Add(laneField);
        fields.Add(timeField);
        fields.Add(durationField);

        ShowEditPopup("Edit Note", fields, () =>
        {
            int fret = note.fret;
            if (fretField != null && !TryParseIntInRange(fretField.value, 0, 24, out fret))
            {
                SetStatus("Fret must be a whole number from 0 to 24.");
                return false;
            }

            if (!TryParseIntInRange(laneField.value, 0, Math.Max(0, laneCount - 1), out int lane))
            {
                SetStatus($"{laneField.label} must be a whole number from 0 to {Math.Max(0, laneCount - 1)}.");
                return false;
            }

            if (!TryParseDoubleInRange(timeField.value, 0.0, project.DurationSeconds, out double time))
            {
                SetStatus($"Time must be between 0 and {project.DurationSeconds:0.000} seconds.");
                return false;
            }

            if (!TryParseDoubleInRange(durationField.value, 0.01, 60.0, out double duration))
            {
                SetStatus("Duration must be between 0.010 and 60.000 seconds.");
                return false;
            }

            if (fretField != null)
                note.fret = fret;
            note.stringOrLane = lane;
            note.timeSeconds = time;
            note.chartTimeSeconds = time;
            SetNoteDurationSeconds(note, duration);
            ChartEditorTimingService.UpdateNoteBeatTiming(project, note);
            track.notes = track.notes?.OrderBy(n => n?.timeSeconds ?? 0.0).ThenBy(n => n?.stringOrLane ?? 0).ToList() ?? new List<ChartEditorNote>();
            SelectSingleNote(track, note);
            project.cursorTimeSeconds = note.timeSeconds;
            project.dirty = true;
            HideEditPopup();
            Rebuild();
            return true;
        });
    }

    private void ShowTechniqueSegmentEditPopup(ChartEditorNote note, ChartEditorTechniqueSegment segment)
    {
        if (note == null || segment == null)
            return;

        double noteLimit = Math.Max(0.06, (project?.DurationSeconds ?? note.timeSeconds + GetNoteEffectiveDurationSeconds(note)) - note.timeSeconds);
        TextField typeField = CreatePopupTextField("Type", segment.type.ToString());
        TextField startField = CreatePopupTextField("Start Offset Seconds", Mathf.Max(0f, segment.startOffset).ToString("0.000", CultureInfo.InvariantCulture));
        TextField endField = CreatePopupTextField("End Offset Seconds", Mathf.Max(segment.startOffset + TechniqueSegmentMinimumSeconds, segment.endOffset).ToString("0.000", CultureInfo.InvariantCulture));
        TextField startFretField = CreatePopupTextField("Start Fret", Mathf.Clamp(segment.startFret, 0, 24).ToString(CultureInfo.InvariantCulture));
        TextField endFretField = CreatePopupTextField("End Fret", Mathf.Clamp(segment.endFret, 0, 24).ToString(CultureInfo.InvariantCulture));
        TextField startBendField = CreatePopupTextField("Start Bend Semitones", segment.startBend.ToString("0.###", CultureInfo.InvariantCulture));
        TextField endBendField = CreatePopupTextField("End Bend Semitones", segment.endBend.ToString("0.###", CultureInfo.InvariantCulture));

        ShowEditPopup("Edit Technique Segment",
            new VisualElement[] { typeField, startField, endField, startFretField, endFretField, startBendField, endBendField },
            () =>
            {
                if (!TryParseTechniqueSegmentType(typeField.value, out NoteTechniqueSegmentType type))
                {
                    SetStatus("Type must be Sustain, Vibrato, Bend, or Slide.");
                    return false;
                }

                if (!TryParseDoubleInRange(startField.value, 0.0, noteLimit, out double start))
                {
                    SetStatus($"Start offset must be between 0 and {noteLimit:0.000} seconds.");
                    return false;
                }

                if (!TryParseDoubleInRange(endField.value, start + TechniqueSegmentMinimumSeconds, noteLimit, out double end))
                {
                    SetStatus($"End offset must be at least {TechniqueSegmentMinimumSeconds:0.000}s after start and within the song.");
                    return false;
                }

                if (!TryParseIntInRange(startFretField.value, 0, 24, out int startFret) ||
                    !TryParseIntInRange(endFretField.value, 0, 24, out int endFret))
                {
                    SetStatus("Segment frets must be whole numbers from 0 to 24.");
                    return false;
                }

                if (!TryParseFloatInRange(startBendField.value, 0f, 4f, out float startBend) ||
                    !TryParseFloatInRange(endBendField.value, 0f, 4f, out float endBend))
                {
                    SetStatus("Bend values must be between 0 and 4 semitones.");
                    return false;
                }

                bool bendTypeEdited = IsBendBearingTechniqueSegment(segment) || type == NoteTechniqueSegmentType.Bend;
                segment.type = type;
                segment.startOffset = (float)start;
                segment.endOffset = (float)end;
                segment.startFret = startFret;
                segment.endFret = endFret;
                segment.startBend = startBend;
                segment.endBend = endBend;
                if (type == NoteTechniqueSegmentType.Slide)
                    note.slideTargetFret = endFret;
                if (type == NoteTechniqueSegmentType.Bend)
                    note.bendStep = Mathf.Max(note.bendStep, Mathf.Abs(startBend), Mathf.Abs(endBend));
                if (bendTypeEdited && note.bendPoints != null)
                    note.bendPoints.Clear();
                ApplyTechniqueSegmentSummaries(note);
                NormalizeTechniqueSegmentLayout(note, segment);
                SyncNoteDurationToTechniqueSegments(note, allowShrink: true);
                NormalizePrimaryTechnique(note);
                ChartEditorTimingService.UpdateNoteBeatTiming(project, note);
                project.dirty = true;
                HideEditPopup();
                Rebuild();
                return true;
            });
    }

    private void ShowTechniqueSettingsPopup(ChartEditorTrack track, ChartEditorNote note)
    {
        if (note == null)
            return;

        HideContextMenu();
        HideEditPopup();
        EnsureLegacyTechniqueSegments(note);
        SelectSingleNote(track, note);

        List<TechniqueSettingsRowState> rowStates = (note.techniqueSegments ?? new List<ChartEditorTechniqueSegment>())
            .Where(segment => segment != null)
            .OrderBy(segment => segment.startOffset)
            .ThenBy(segment => segment.endOffset)
            .Select(segment => new TechniqueSettingsRowState { segment = CloneTechniqueSegment(segment) })
            .ToList();
        List<ChartEditorTechniqueSegment> originalSegments = rowStates
            .Select(state => CloneTechniqueSegment(state.segment))
            .ToList();

        bool hammerOn = note.technique == NoteTechnique.HammerOn;
        bool pullOff = note.technique == NoteTechnique.PullOff;
        bool palmMute = IsPalmMuteEnabled(note);
        bool fretHandMute = IsFretHandMuteEnabled(note);
        bool legato = note.legato;
        bool requiresPluck = note.requiresPluck;
        bool harmonic = note.harmonic;
        bool accentFlag = note.accent;
        bool tap = note.tap;
        bool tremolo = note.tremolo;
        bool pinchHarmonic = note.pinchHarmonic;

        VisualElement overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.right = 0f;
        overlay.style.top = 0f;
        overlay.style.bottom = 0f;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.42f);
        overlay.pickingMode = PickingMode.Position;
        overlay.RegisterCallback<PointerDownEvent>(evt =>
        {
            HideEditPopup();
            evt.StopPropagation();
        });

        VisualElement panel = new VisualElement();
        panel.style.width = 1420f;
        panel.style.maxWidth = Length.Percent(92f);
        panel.style.maxHeight = Length.Percent(90f);
        panel.style.paddingLeft = 42f;
        panel.style.paddingRight = 42f;
        panel.style.paddingTop = 38f;
        panel.style.paddingBottom = 34f;
        StylePopupPanel(panel, new Color(0.030f, 0.036f, 0.048f, 0.995f), 16f);
        panel.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 30f;
        VisualElement headerText = new VisualElement();
        Label title = CreateLabel("Technique Settings", 40f, Color.white, true, TextAnchor.MiddleLeft, false);
        Label subtitle = CreateLabel($"Note {note.fret}  -  {FormatTime(note.timeSeconds)}  -  {GetNoteEffectiveDurationSeconds(note):0.000}s", 24f, new Color(0.68f, 0.75f, 0.84f, 1f), false, TextAnchor.MiddleLeft, false);
        subtitle.style.marginTop = 6f;
        headerText.Add(title);
        headerText.Add(subtitle);
        header.Add(headerText);
        Button close = CreateCompactButton("Close", HideEditPopup);
        StyleSoftButton(close, new Color(0.80f, 0.86f, 0.94f, 1f));
        close.style.height = 62f;
        close.style.minWidth = 132f;
        close.style.fontSize = UiFont(22f);
        SetRadius(close, 12f);
        header.Add(close);
        panel.Add(header);

        panel.Add(CreateTechniqueSettingsSectionLabel("Note Flags"));
        VisualElement flagRow = new VisualElement();
        flagRow.style.flexDirection = FlexDirection.Row;
        flagRow.style.flexWrap = Wrap.Wrap;
        flagRow.style.marginBottom = 22f;
        Button hammerButton = null;
        Button pullButton = null;
        Button palmMuteButton = null;
        Button fretHandMuteButton = null;
        Button legatoButton = null;
        Button pluckButton = null;
        Button harmonicButton = null;
        Button accentButton = null;
        Button tapButton = null;
        Button tremoloButton = null;
        Button pinchHarmonicButton = null;

        void UpdateFlagButtons()
        {
            StyleTechniqueSettingsToggleButton(hammerButton, hammerOn);
            StyleTechniqueSettingsToggleButton(pullButton, pullOff);
            StyleTechniqueSettingsToggleButton(palmMuteButton, palmMute);
            StyleTechniqueSettingsToggleButton(fretHandMuteButton, fretHandMute);
            StyleTechniqueSettingsToggleButton(legatoButton, legato);
            StyleTechniqueSettingsToggleButton(pluckButton, requiresPluck);
            StyleTechniqueSettingsToggleButton(harmonicButton, harmonic);
            StyleTechniqueSettingsToggleButton(accentButton, accentFlag);
            StyleTechniqueSettingsToggleButton(tapButton, tap);
            StyleTechniqueSettingsToggleButton(tremoloButton, tremolo);
            StyleTechniqueSettingsToggleButton(pinchHarmonicButton, pinchHarmonic);
        }

        hammerButton = CreateTechniqueSettingsToggleButton("Hammer-On", () =>
        {
            hammerOn = !hammerOn;
            if (hammerOn)
                pullOff = false;
            UpdateFlagButtons();
        });
        pullButton = CreateTechniqueSettingsToggleButton("Pull-Off", () =>
        {
            pullOff = !pullOff;
            if (pullOff)
                hammerOn = false;
            UpdateFlagButtons();
        });
        palmMuteButton = CreateTechniqueSettingsToggleButton("Palm Mute", () =>
        {
            palmMute = !palmMute;
            if (palmMute)
                fretHandMute = false;
            UpdateFlagButtons();
        });
        fretHandMuteButton = CreateTechniqueSettingsToggleButton("Fret-Hand Mute", () =>
        {
            fretHandMute = !fretHandMute;
            if (fretHandMute)
                palmMute = false;
            UpdateFlagButtons();
        });
        legatoButton = CreateTechniqueSettingsToggleButton("Legato", () =>
        {
            legato = !legato;
            UpdateFlagButtons();
        });
        pluckButton = CreateTechniqueSettingsToggleButton("Requires Pluck", () =>
        {
            requiresPluck = !requiresPluck;
            UpdateFlagButtons();
        });
        harmonicButton = CreateTechniqueSettingsToggleButton("Natural Harmonic", () =>
        {
            harmonic = !harmonic;
            if (harmonic)
                pinchHarmonic = false;
            UpdateFlagButtons();
        });
        pinchHarmonicButton = CreateTechniqueSettingsToggleButton("Pinch Harmonic", () =>
        {
            pinchHarmonic = !pinchHarmonic;
            if (pinchHarmonic)
                harmonic = false;
            UpdateFlagButtons();
        });
        accentButton = CreateTechniqueSettingsToggleButton("Accent", () =>
        {
            accentFlag = !accentFlag;
            UpdateFlagButtons();
        });
        tapButton = CreateTechniqueSettingsToggleButton("Tap", () =>
        {
            tap = !tap;
            UpdateFlagButtons();
        });
        tremoloButton = CreateTechniqueSettingsToggleButton("Tremolo", () =>
        {
            tremolo = !tremolo;
            UpdateFlagButtons();
        });

        flagRow.Add(hammerButton);
        flagRow.Add(pullButton);
        flagRow.Add(palmMuteButton);
        flagRow.Add(fretHandMuteButton);
        flagRow.Add(legatoButton);
        flagRow.Add(pluckButton);
        flagRow.Add(harmonicButton);
        flagRow.Add(pinchHarmonicButton);
        flagRow.Add(accentButton);
        flagRow.Add(tapButton);
        flagRow.Add(tremoloButton);
        panel.Add(flagRow);
        UpdateFlagButtons();

        panel.Add(CreateTechniqueSettingsSectionLabel("Add Segment"));
        VisualElement addRow = new VisualElement();
        addRow.style.flexDirection = FlexDirection.Row;
        addRow.style.flexWrap = Wrap.Wrap;
        addRow.style.marginBottom = 24f;
        addRow.Add(CreateTechniqueSettingsAddButton("Add Sustain", () => AddTechniqueSettingsSegment(rowStates, note, NoteTechniqueSegmentType.Sustain)));
        addRow.Add(CreateTechniqueSettingsAddButton("Add Vibrato", () => AddTechniqueSettingsSegment(rowStates, note, NoteTechniqueSegmentType.Vibrato)));
        addRow.Add(CreateTechniqueSettingsAddButton("Half Bend", () => AddTechniqueSettingsBendSegment(rowStates, note, 0f, HalfStepBendSemitones)));
        addRow.Add(CreateTechniqueSettingsAddButton("Full Bend", () => AddTechniqueSettingsBendSegment(rowStates, note, 0f, FullStepBendSemitones)));
        addRow.Add(CreateTechniqueSettingsAddButton("Half Pre-Bend", () => AddTechniqueSettingsBendSegment(rowStates, note, HalfStepBendSemitones, HalfStepBendSemitones)));
        addRow.Add(CreateTechniqueSettingsAddButton("Full Pre-Bend", () => AddTechniqueSettingsBendSegment(rowStates, note, FullStepBendSemitones, FullStepBendSemitones)));
        addRow.Add(CreateTechniqueSettingsAddButton("Half Release", () => AddTechniqueSettingsBendSegment(rowStates, note, HalfStepBendSemitones, 0f)));
        addRow.Add(CreateTechniqueSettingsAddButton("Full Release", () => AddTechniqueSettingsBendSegment(rowStates, note, FullStepBendSemitones, 0f)));
        addRow.Add(CreateTechniqueSettingsAddButton("Add Slide", () => AddTechniqueSettingsSegment(rowStates, note, NoteTechniqueSegmentType.Slide)));
        panel.Add(addRow);

        panel.Add(CreateTechniqueSettingsSectionLabel("Segments"));

        ScrollView listScroll = new ScrollView(ScrollViewMode.Vertical);
        ConfigureScrollView(listScroll);
        listScroll.style.height = 560f;
        listScroll.style.minHeight = 360f;
        listScroll.style.marginBottom = 28f;
        listScroll.style.backgroundColor = new Color(0.018f, 0.023f, 0.032f, 0.92f);
        SetRadius(listScroll, 14f);
        SetBorderWidth(listScroll, 1f);
        SetToneLabBorder(listScroll,
            new Color(0.30f, 0.32f, 0.36f, 0.78f),
            new Color(0.17f, 0.19f, 0.23f, 0.90f),
            new Color(0.10f, 0.12f, 0.15f, 0.98f));
        VisualElement list = new VisualElement();
        list.style.paddingLeft = 18f;
        list.style.paddingRight = 18f;
        list.style.paddingTop = 18f;
        list.style.paddingBottom = 18f;
        listScroll.Add(list);
        panel.Add(listScroll);

        void CaptureRows()
        {
            for (int i = 0; i < rowStates.Count; i++)
                TryCaptureTechniqueSettingsRow(rowStates[i], note, showError: false);
        }

        void RebuildRows()
        {
            list.Clear();
            if (rowStates.Count == 0)
            {
                Label empty = CreateLabel("No timed technique segments. Add Sustain, Vibrato, Bend, or Slide.", 24f, new Color(0.70f, 0.76f, 0.84f, 1f), false, TextAnchor.MiddleCenter, false);
                empty.style.height = 118f;
                list.Add(empty);
                return;
            }

            for (int i = 0; i < rowStates.Count; i++)
                list.Add(CreateTechniqueSettingsRow(rowStates, i, list, RebuildRows, CaptureRows, note));
        }

        void AddAndRebuild(NoteTechniqueSegmentType type)
        {
            CaptureRows();
            AddTechniqueSettingsSegment(rowStates, note, type);
            RebuildRows();
        }

        addRow.Clear();
        addRow.Add(CreateTechniqueSettingsAddButton("Add Sustain", () => AddAndRebuild(NoteTechniqueSegmentType.Sustain)));
        addRow.Add(CreateTechniqueSettingsAddButton("Add Vibrato", () => AddAndRebuild(NoteTechniqueSegmentType.Vibrato)));
        addRow.Add(CreateTechniqueSettingsAddButton("Half Bend", () =>
        {
            CaptureRows();
            AddTechniqueSettingsBendSegment(rowStates, note, 0f, HalfStepBendSemitones);
            RebuildRows();
        }));
        addRow.Add(CreateTechniqueSettingsAddButton("Full Bend", () =>
        {
            CaptureRows();
            AddTechniqueSettingsBendSegment(rowStates, note, 0f, FullStepBendSemitones);
            RebuildRows();
        }));
        addRow.Add(CreateTechniqueSettingsAddButton("Half Pre-Bend", () =>
        {
            CaptureRows();
            AddTechniqueSettingsBendSegment(rowStates, note, HalfStepBendSemitones, HalfStepBendSemitones);
            RebuildRows();
        }));
        addRow.Add(CreateTechniqueSettingsAddButton("Full Pre-Bend", () =>
        {
            CaptureRows();
            AddTechniqueSettingsBendSegment(rowStates, note, FullStepBendSemitones, FullStepBendSemitones);
            RebuildRows();
        }));
        addRow.Add(CreateTechniqueSettingsAddButton("Half Release", () =>
        {
            CaptureRows();
            AddTechniqueSettingsBendSegment(rowStates, note, HalfStepBendSemitones, 0f);
            RebuildRows();
        }));
        addRow.Add(CreateTechniqueSettingsAddButton("Full Release", () =>
        {
            CaptureRows();
            AddTechniqueSettingsBendSegment(rowStates, note, FullStepBendSemitones, 0f);
            RebuildRows();
        }));
        addRow.Add(CreateTechniqueSettingsAddButton("Add Slide", () => AddAndRebuild(NoteTechniqueSegmentType.Slide)));

        RebuildRows();

        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.justifyContent = Justify.FlexEnd;
        Button cancel = CreateCompactButton("Cancel", HideEditPopup);
        Button apply = CreateCompactButton("Apply", () =>
        {
            for (int i = 0; i < rowStates.Count; i++)
            {
                if (!TryCaptureTechniqueSettingsRow(rowStates[i], note, showError: true))
                    return;
            }

            List<ChartEditorTechniqueSegment> capturedSegments = rowStates
                .Select(state => CloneTechniqueSegment(state.segment))
                .ToList();
            if (BendTechniqueSegmentsChanged(originalSegments, capturedSegments))
                ClearBendPoints(note);

            note.techniqueSegments = capturedSegments;
            NormalizeTechniqueSegmentLayout(note);
            SyncNoteDurationToTechniqueSegments(note, allowShrink: true);
            note.palmMute = palmMute;
            note.fretHandMute = fretHandMute;
            note.muted = palmMute || fretHandMute;
            note.harmonic = harmonic;
            note.pinchHarmonic = pinchHarmonic;
            note.accent = accentFlag;
            note.tap = tap;
            note.tremolo = tremolo;
            note.legato = legato || hammerOn || pullOff;
            note.requiresPluck = hammerOn || pullOff ? false : requiresPluck;
            ApplyTechniqueSegmentSummaries(note);
            if (hammerOn)
                note.technique = NoteTechnique.HammerOn;
            else if (pullOff)
                note.technique = NoteTechnique.PullOff;
            else
                NormalizePrimaryTechnique(note);
            ChartEditorTimingService.UpdateNoteBeatTiming(project, note);
            project.dirty = true;
            HideEditPopup();
            Rebuild();
        });
        StyleSoftButton(cancel, new Color(0.80f, 0.86f, 0.94f, 1f));
        StyleFilledButton(apply, new Color(0.62f, 0.38f, 1f, 1f), darkText: false);
        cancel.style.height = 66f;
        cancel.style.minWidth = 154f;
        apply.style.height = 66f;
        apply.style.minWidth = 154f;
        cancel.style.fontSize = UiFont(24f);
        apply.style.fontSize = UiFont(24f);
        SetRadius(cancel, 12f);
        SetRadius(apply, 12f);
        cancel.style.marginRight = 14f;
        actions.Add(cancel);
        actions.Add(apply);
        panel.Add(actions);

        overlay.Add(panel);
        editPopupElement = overlay;
        RootElement.Add(editPopupElement);
        editPopupElement.BringToFront();
        SetChartEditorKeyboardCaptureActive(true);
    }

    private Label CreateTechniqueSettingsSectionLabel(string text)
    {
        Label label = CreateLabel(text, 20f, new Color(0.60f, 0.68f, 0.80f, 1f), true, TextAnchor.MiddleLeft, false);
        label.style.marginBottom = 10f;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        return label;
    }

    private Button CreateTechniqueSettingsToggleButton(string text, Action action)
    {
        Button button = CreateButton(text, action);
        button.style.height = 68f;
        button.style.minWidth = 205f;
        button.style.marginLeft = 0f;
        button.style.marginRight = 12f;
        button.style.marginBottom = 12f;
        button.style.fontSize = UiFont(22f);
        button.style.paddingLeft = 22f;
        button.style.paddingRight = 22f;
        SetRadius(button, 12f);
        return button;
    }

    private static void StyleTechniqueSettingsToggleButton(Button button, bool selected)
    {
        if (button == null)
            return;

        if (selected)
        {
            SetButtonChrome(
                button,
                new Color(0.28f, 0.17f, 0.48f, 0.92f),
                Color.white,
                new Color(0.78f, 0.58f, 1f, 0.96f),
                new Color(0.48f, 0.32f, 0.72f, 0.98f),
                new Color(0.30f, 0.18f, 0.48f, 1f),
                new Color(0.40f, 0.24f, 0.70f, 0.95f),
                Color.white,
                new Color(0.82f, 0.62f, 1f, 0.98f));
        }
        else
        {
            SetButtonChrome(
                button,
                new Color(0f, 0f, 0f, 0f),
                new Color(0.80f, 0.85f, 0.93f, 1f),
                new Color(0.36f, 0.38f, 0.42f, 0.82f),
                new Color(0.22f, 0.24f, 0.28f, 0.95f),
                new Color(0.14f, 0.16f, 0.20f, 1f),
                new Color(1f, 1f, 1f, 0.070f),
                Color.white,
                new Color(0.66f, 0.72f, 0.84f, 0.56f));
        }
        SetRadius(button, 11f);
    }

    private Button CreateTechniqueSettingsAddButton(string text, Action action)
    {
        Button button = CreateButton(text, action);
        button.style.height = 66f;
        button.style.minWidth = 176f;
        button.style.fontSize = UiFont(22f);
        button.style.marginLeft = 0f;
        button.style.marginRight = 12f;
        button.style.marginBottom = 12f;
        button.style.paddingLeft = 20f;
        button.style.paddingRight = 20f;
        StyleSoftButton(button, new Color(0.68f, 0.76f, 0.90f, 1f));
        SetRadius(button, 12f);
        return button;
    }

    private VisualElement CreateTechniqueSettingsRow(
        List<TechniqueSettingsRowState> rowStates,
        int index,
        VisualElement list,
        Action rebuildRows,
        Action captureRows,
        ChartEditorNote note)
    {
        TechniqueSettingsRowState state = rowStates[index];
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Column;
        row.style.marginBottom = 16f;
        row.style.paddingLeft = 20f;
        row.style.paddingRight = 20f;
        row.style.paddingTop = 18f;
        row.style.paddingBottom = 18f;
        row.style.backgroundColor = new Color(0.030f, 0.036f, 0.048f, 0.96f);
        SetRadius(row, 12f);
        SetBorderWidth(row, 1f);
        SetToneLabBorder(row,
            new Color(0.34f, 0.36f, 0.40f, 0.76f),
            new Color(0.18f, 0.20f, 0.24f, 0.92f),
            new Color(0.10f, 0.12f, 0.15f, 0.98f));

        VisualElement topRow = new VisualElement();
        topRow.style.flexDirection = FlexDirection.Row;
        topRow.style.alignItems = Align.Center;
        row.Add(topRow);

        VisualElement grip = new VisualElement();
        grip.style.width = 44f;
        grip.style.height = 68f;
        grip.style.marginRight = 16f;
        grip.style.justifyContent = Justify.Center;
        grip.style.alignItems = Align.Center;
        grip.pickingMode = PickingMode.Position;
        for (int i = 0; i < 3; i++)
        {
            VisualElement bar = new VisualElement();
            bar.style.width = 20f;
            bar.style.height = 3f;
            bar.style.marginTop = 4f;
            bar.style.marginBottom = 4f;
            bar.style.backgroundColor = new Color(0.72f, 0.78f, 0.88f, 0.82f);
            SetRadius(bar, 2f);
            grip.Add(bar);
        }
        topRow.Add(grip);

        DropdownField type = new DropdownField();
        type.label = "Type";
        type.choices = TechniqueSegmentChoiceLabels().ToList();
        type.value = SegmentTypeToChoiceLabel(state.segment.type);
        type.style.width = 260f;
        type.style.marginRight = 18f;
        StyleTechniqueSettingsDropdown(type);
        state.typeDropdown = type;
        topRow.Add(type);

        Label summary = CreateLabel(GetTechniqueSegmentLabel(state.segment), 24f, new Color(0.82f, 0.88f, 0.96f, 1f), true, TextAnchor.MiddleLeft, false);
        summary.style.flexGrow = 1f;
        summary.style.whiteSpace = WhiteSpace.NoWrap;
        topRow.Add(summary);

        Button remove = CreateCompactButton("Remove", () =>
        {
            captureRows?.Invoke();
            rowStates.Remove(state);
            rebuildRows?.Invoke();
        });
        remove.style.height = 62f;
        remove.style.minWidth = 150f;
        remove.style.fontSize = UiFont(22f);
        remove.style.marginLeft = 16f;
        remove.style.marginRight = 0f;
        StyleDangerButton(remove);
        SetRadius(remove, 12f);
        topRow.Add(remove);

        VisualElement fieldsRow = new VisualElement();
        fieldsRow.style.flexDirection = FlexDirection.Row;
        fieldsRow.style.flexWrap = Wrap.Wrap;
        fieldsRow.style.marginTop = 18f;
        row.Add(fieldsRow);

        state.startField = CreateTechniqueSettingsTextField("Start", state.segment.startOffset.ToString("0.000", CultureInfo.InvariantCulture), 166f);
        state.endField = CreateTechniqueSettingsTextField("End", state.segment.endOffset.ToString("0.000", CultureInfo.InvariantCulture), 166f);
        fieldsRow.Add(state.startField);
        fieldsRow.Add(state.endField);

        state.startFretField = CreateTechniqueSettingsTextField("From Fret", Mathf.Clamp(state.segment.startFret, 0, 24).ToString(CultureInfo.InvariantCulture), 170f);
        state.endFretField = CreateTechniqueSettingsTextField("To Fret", Mathf.Clamp(state.segment.endFret, 0, 24).ToString(CultureInfo.InvariantCulture), 170f);
        fieldsRow.Add(state.startFretField);
        fieldsRow.Add(state.endFretField);

        state.startBendField = CreateTechniqueSettingsTextField("From Bend", state.segment.startBend.ToString("0.###", CultureInfo.InvariantCulture), 178f);
        state.endBendField = CreateTechniqueSettingsTextField("To Bend", state.segment.endBend.ToString("0.###", CultureInfo.InvariantCulture), 178f);
        fieldsRow.Add(state.startBendField);
        fieldsRow.Add(state.endBendField);

        int pointerId = -1;
        Vector2 startPointer = Vector2.zero;
        int startIndex = index;
        grip.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;

            captureRows?.Invoke();
            pointerId = evt.pointerId;
            startPointer = PointerPosition(evt);
            startIndex = rowStates.IndexOf(state);
            row.style.opacity = 0.64f;
            grip.CapturePointer(pointerId);
            evt.StopImmediatePropagation();
        });
        grip.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (evt.pointerId != pointerId)
                return;

            if (grip.HasPointerCapture(pointerId))
                grip.ReleasePointer(pointerId);

            row.style.opacity = 1f;
            Vector2 local = list.WorldToLocal(PointerPosition(evt));
            int targetIndex = Mathf.Clamp(Mathf.FloorToInt(local.y / 184f), 0, Math.Max(0, rowStates.Count - 1));
            int currentIndex = rowStates.IndexOf(state);
            if (currentIndex >= 0 && targetIndex != currentIndex)
            {
                rowStates.RemoveAt(currentIndex);
                rowStates.Insert(Mathf.Clamp(targetIndex, 0, rowStates.Count), state);
                ReflowTechniqueSettingsRowsByOrder(rowStates);
            }

            pointerId = -1;
            rebuildRows?.Invoke();
            evt.StopImmediatePropagation();
        });

        type.RegisterValueChangedCallback(_ =>
        {
            TryCaptureTechniqueSettingsRow(state, note, showError: false);
            if (ChoiceLabelToSegmentType(type.value, out NoteTechniqueSegmentType parsedType))
            {
                state.segment.type = parsedType;
                ApplyTechniqueSegmentTypeDefaultsForSettings(note, state.segment);
            }
        });

        return row;
    }

    private TextField CreateTechniqueSettingsTextField(string label, string value, float width)
    {
        TextField field = CreatePopupTextField(label, value);
        field.style.width = width;
        field.style.height = 84f;
        field.style.marginRight = 12f;
        field.style.marginBottom = 12f;
        field.style.fontSize = UiFont(21f);
        SetRadius(field, 12f);
        return field;
    }

    private static void StyleTechniqueSettingsDropdown(DropdownField dropdown)
    {
        if (dropdown == null)
            return;

        dropdown.style.height = 68f;
        dropdown.style.fontSize = UiFont(22f);
        dropdown.style.unityFontStyleAndWeight = FontStyle.Bold;
        dropdown.style.color = Color.white;
        dropdown.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        dropdown.style.paddingLeft = 12f;
        dropdown.style.paddingRight = 12f;
        SetRadius(dropdown, 0f);
        SetBorderWidth(dropdown, 1f);
        dropdown.style.borderTopWidth = 0f;
        dropdown.style.borderRightWidth = 0f;
        dropdown.style.borderLeftWidth = 0f;
        dropdown.style.borderBottomWidth = 1f;
        dropdown.style.borderBottomColor = new Color(1f, 1f, 1f, 0.34f);
        dropdown.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            VisualElement input = dropdown.Q(className: "unity-base-field__input");
            if (input != null)
            {
                input.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
                input.style.borderTopWidth = 0f;
                input.style.borderRightWidth = 0f;
                input.style.borderBottomWidth = 0f;
                input.style.borderLeftWidth = 0f;
                input.style.color = Color.white;
                input.style.fontSize = UiFont(22f);
            }

            Label text = dropdown.Q<Label>(className: "unity-base-popup-field__text");
            if (text != null)
            {
                text.style.color = new Color(0.92f, 0.94f, 0.98f, 1f);
                text.style.fontSize = UiFont(22f);
                text.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            VisualElement arrow = dropdown.Q(className: "unity-base-popup-field__arrow");
            if (arrow != null)
                arrow.style.unityBackgroundImageTintColor = new Color(0.82f, 0.84f, 0.88f, 1f);
        });
    }

    private void AddTechniqueSettingsSegment(List<TechniqueSettingsRowState> rowStates, ChartEditorNote note, NoteTechniqueSegmentType type)
    {
        if (rowStates == null || note == null)
            return;

        float start = rowStates.Count == 0 ? 0f : rowStates.Max(state => Mathf.Max(state.segment.startOffset, state.segment.endOffset));
        float projectLimit = Mathf.Max(TechniqueSegmentMinimumSeconds, (float)((project?.DurationSeconds ?? note.timeSeconds + start + 1.0) - note.timeSeconds));
        float end = Mathf.Min(projectLimit, start + 0.5f);
        if (end <= start + TechniqueSegmentMinimumSeconds)
        {
            start = Mathf.Max(0f, projectLimit - 0.5f);
            end = projectLimit;
        }

        ChartEditorTechniqueSegment segment = CreateTechniqueSegment(note, type, start, end);
        ApplyTechniqueSegmentTypeDefaultsForSettings(note, segment);
        rowStates.Add(new TechniqueSettingsRowState { segment = segment });
        ReflowTechniqueSettingsRowsByOrder(rowStates);
    }

    private void AddTechniqueSettingsBendSegment(List<TechniqueSettingsRowState> rowStates, ChartEditorNote note, float startBend, float endBend)
    {
        if (rowStates == null || note == null)
            return;

        AddTechniqueSettingsSegment(rowStates, note, NoteTechniqueSegmentType.Bend);
        TechniqueSettingsRowState last = rowStates.LastOrDefault();
        if (last?.segment == null)
            return;

        last.segment.startBend = Mathf.Max(0f, startBend);
        last.segment.endBend = Mathf.Max(0f, endBend);
    }

    private static void ReflowTechniqueSettingsRowsByOrder(List<TechniqueSettingsRowState> rowStates)
    {
        if (rowStates == null)
            return;

        float cursor = 0f;
        for (int i = 0; i < rowStates.Count; i++)
        {
            ChartEditorTechniqueSegment segment = rowStates[i]?.segment;
            if (segment == null)
                continue;

            float length = Mathf.Max(TechniqueSegmentMinimumSeconds, segment.endOffset - segment.startOffset);
            segment.startOffset = cursor;
            segment.endOffset = cursor + length;
            cursor = segment.endOffset;
        }
    }

    private bool TryCaptureTechniqueSettingsRow(TechniqueSettingsRowState state, ChartEditorNote note, bool showError)
    {
        if (state == null || state.segment == null)
            return false;

        if (!ChoiceLabelToSegmentType(state.typeDropdown?.value, out NoteTechniqueSegmentType type))
        {
            if (showError)
                SetStatus("Technique type must be Sustain, Vibrato, Bend, or Slide.");
            return false;
        }

        double noteLimit = Math.Max(0.06, (project?.DurationSeconds ?? note.timeSeconds + GetNoteEffectiveDurationSeconds(note)) - note.timeSeconds);
        if (!TryParseDoubleInRange(state.startField?.value, 0.0, noteLimit, out double start))
        {
            if (showError)
                SetStatus($"Technique start must be between 0 and {noteLimit:0.000} seconds.");
            return false;
        }

        if (!TryParseDoubleInRange(state.endField?.value, start + TechniqueSegmentMinimumSeconds, noteLimit, out double end))
        {
            if (showError)
                SetStatus("Technique end must be after the start and within the song.");
            return false;
        }

        if (!TryParseIntInRange(state.startFretField?.value, 0, 24, out int startFret) ||
            !TryParseIntInRange(state.endFretField?.value, 0, 24, out int endFret))
        {
            if (showError)
                SetStatus("Technique frets must be whole numbers from 0 to 24.");
            return false;
        }

        if (!TryParseFloatInRange(state.startBendField?.value, 0f, 4f, out float startBend) ||
            !TryParseFloatInRange(state.endBendField?.value, 0f, 4f, out float endBend))
        {
            if (showError)
                SetStatus("Technique bend values must be between 0 and 4 semitones.");
            return false;
        }

        state.segment.type = type;
        state.segment.startOffset = (float)start;
        state.segment.endOffset = (float)end;
        state.segment.startFret = startFret;
        state.segment.endFret = endFret;
        state.segment.startBend = startBend;
        state.segment.endBend = endBend;
        return true;
    }

    private static ChartEditorTechniqueSegment CloneTechniqueSegment(ChartEditorTechniqueSegment segment)
    {
        if (segment == null)
            return null;

        return new ChartEditorTechniqueSegment
        {
            type = segment.type,
            startOffset = segment.startOffset,
            endOffset = segment.endOffset,
            startFret = segment.startFret,
            endFret = segment.endFret,
            startBend = segment.startBend,
            endBend = segment.endBend
        };
    }

    private static bool BendTechniqueSegmentsChanged(
        IEnumerable<ChartEditorTechniqueSegment> before,
        IEnumerable<ChartEditorTechniqueSegment> after)
    {
        List<ChartEditorTechniqueSegment> beforeBends = ExtractOrderedBendSegments(before);
        List<ChartEditorTechniqueSegment> afterBends = ExtractOrderedBendSegments(after);
        if (beforeBends.Count != afterBends.Count)
            return true;

        for (int i = 0; i < beforeBends.Count; i++)
        {
            ChartEditorTechniqueSegment oldSegment = beforeBends[i];
            ChartEditorTechniqueSegment newSegment = afterBends[i];
            if (!NearlyEqual(oldSegment.startOffset, newSegment.startOffset) ||
                !NearlyEqual(oldSegment.endOffset, newSegment.endOffset) ||
                !NearlyEqual(oldSegment.startBend, newSegment.startBend) ||
                !NearlyEqual(oldSegment.endBend, newSegment.endBend))
            {
                return true;
            }
        }

        return false;
    }

    private static List<ChartEditorTechniqueSegment> ExtractOrderedBendSegments(IEnumerable<ChartEditorTechniqueSegment> segments)
    {
        return segments?
            .Where(IsBendBearingTechniqueSegment)
            .OrderBy(segment => segment.startOffset)
            .ThenBy(segment => segment.endOffset)
            .ThenBy(segment => segment.startBend)
            .ThenBy(segment => segment.endBend)
            .ToList() ?? new List<ChartEditorTechniqueSegment>();
    }

    private static bool NearlyEqual(float left, float right)
    {
        return Mathf.Abs(left - right) <= 0.0005f;
    }

    private static bool IsBendBearingTechniqueSegment(ChartEditorTechniqueSegment segment)
    {
        return segment != null &&
               (segment.type == NoteTechniqueSegmentType.Bend ||
                segment.type == NoteTechniqueSegmentType.Sustain ||
                segment.type == NoteTechniqueSegmentType.Vibrato) &&
               (segment.type == NoteTechniqueSegmentType.Bend ||
                Mathf.Abs(segment.startBend) > 0.01f ||
                Mathf.Abs(segment.endBend) > 0.01f);
    }

    private static void ClearBendPoints(ChartEditorNote note)
    {
        note?.bendPoints?.Clear();
    }

    private static IEnumerable<string> TechniqueSegmentChoiceLabels()
    {
        yield return "Sustain";
        yield return "Vibrato";
        yield return "Bend";
        yield return "Slide";
    }

    private static string SegmentTypeToChoiceLabel(NoteTechniqueSegmentType type)
    {
        switch (type)
        {
            case NoteTechniqueSegmentType.Sustain:
                return "Sustain";
            case NoteTechniqueSegmentType.Vibrato:
                return "Vibrato";
            case NoteTechniqueSegmentType.Bend:
                return "Bend";
            case NoteTechniqueSegmentType.Slide:
                return "Slide";
            default:
                return "Sustain";
        }
    }

    private static bool ChoiceLabelToSegmentType(string value, out NoteTechniqueSegmentType type)
    {
        return TryParseTechniqueSegmentType(value, out type);
    }

    private static void ApplyTechniqueSegmentTypeDefaultsForSettings(ChartEditorNote note, ChartEditorTechniqueSegment segment)
    {
        if (note == null || segment == null)
            return;

        int fret = Mathf.Clamp(note.fret, 0, 24);
        if (segment.startFret < 0 || segment.startFret > 24)
            segment.startFret = fret;
        if (segment.endFret < 0 || segment.endFret > 24)
            segment.endFret = fret;

        if (segment.type == NoteTechniqueSegmentType.Slide && segment.endFret == segment.startFret)
            segment.endFret = Mathf.Clamp(fret + 1, 0, 24);
        if (segment.type == NoteTechniqueSegmentType.Bend && Mathf.Abs(segment.startBend) <= 0.01f && Mathf.Abs(segment.endBend) <= 0.01f)
            segment.endBend = 2f;
    }

    private static void ApplyTechniqueSegmentSummaries(ChartEditorNote note)
    {
        if (note == null)
            return;

        note.slideTargetFret = -1;
        note.bendStep = 0f;
        note.bendPreBend = false;
        note.bendRelease = false;
        note.maxBend = 0f;
        if (note.techniqueSegments == null)
            return;

        for (int i = 0; i < note.techniqueSegments.Count; i++)
        {
            ChartEditorTechniqueSegment segment = note.techniqueSegments[i];
            if (segment == null)
                continue;

            if (segment.type == NoteTechniqueSegmentType.Slide)
                note.slideTargetFret = segment.endFret;
            else if (segment.type == NoteTechniqueSegmentType.Bend ||
                     segment.type == NoteTechniqueSegmentType.Sustain ||
                     segment.type == NoteTechniqueSegmentType.Vibrato)
            {
                float startBend = Mathf.Max(0f, segment.startBend);
                float endBend = Mathf.Max(0f, segment.endBend);
                if (startBend <= 0.01f && endBend <= 0.01f)
                    continue;

                note.bendStep = Mathf.Max(note.bendStep, startBend, endBend);
                note.maxBend = Mathf.Max(note.maxBend, startBend, endBend);
                note.bendPreBend |= segment.startOffset <= 0.001f && startBend > 0.01f;
                note.bendRelease |= startBend > endBend + 0.01f;
            }
        }
    }

    private void ShowSyncPointEditPopup(ChartEditorBeatMarker anchor)
    {
        if (anchor == null)
            return;

        TextField nameField = CreatePopupTextField("Name", FirstNonEmpty(anchor.label, "Anchor"));
        TextField audioField = CreatePopupTextField("Audio Time Seconds", anchor.audioTimeSeconds.ToString("0.000", CultureInfo.InvariantCulture));
        TextField beatField = CreatePopupTextField("Beat Position", anchor.beatPosition.ToString("0.###", CultureInfo.InvariantCulture));

        ShowEditPopup("Edit Anchor", new VisualElement[] { nameField, audioField, beatField }, () =>
        {
            if (!TryParseDoubleInRange(audioField.value, 0.0, project.DurationSeconds, out double audioTime))
            {
                SetStatus($"Audio time must be between 0 and {project.DurationSeconds:0.000} seconds.");
                return false;
            }

            if (!TryParseDoubleInRange(beatField.value, 0.0, Math.Max(project.DurationSeconds * 8.0, 16.0), out double beatPosition))
            {
                SetStatus("Beat position must be a valid non-negative value.");
                return false;
            }

            anchor.label = string.IsNullOrWhiteSpace(nameField.value) ? "Anchor" : nameField.value.Trim();
            anchor.beatPosition = beatPosition;
            anchor.isAnchor = true;
            ChartEditorTimingService.MoveAnchor(project, anchor, audioTime);
            SelectSingleAnchor(anchor);
            project.cursorTimeSeconds = anchor.audioTimeSeconds;
            project.dirty = true;
            HideEditPopup();
            Rebuild();
            return true;
        });
    }

    private void ShowBeatMapSettingsPopup()
    {
        if (project?.beatMap == null)
            return;

        project.EnsureDefaults();
        ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: false);
        double cursorBeat = ChartEditorTimingService.GetBeatPositionForAudioTime(project, project.cursorTimeSeconds);
        ChartEditorBeatMarker nearestBeat = ChartEditorTimingService.GetNearestBeatMarker(project, project.cursorTimeSeconds);
        ChartEditorTimeSignatureChange signature = ChartEditorTimingService.GetTimeSignatureAtBeat(project, cursorBeat);

        TextField defaultBpmField = CreatePopupTextField("Default BPM", project.beatMap.defaultTempoBpm.ToString("0.###", CultureInfo.InvariantCulture));
        TextField regionBpmField = CreatePopupTextField("Current Region BPM", ChartEditorTimingService.GetTempoAtBeat(project, cursorBeat).ToString("0.###", CultureInfo.InvariantCulture));
        TextField numeratorField = CreatePopupTextField("Time Signature Numerator", Math.Max(1, signature?.numerator ?? 4).ToString(CultureInfo.InvariantCulture));
        TextField denominatorField = CreatePopupTextField("Time Signature Denominator", Math.Max(1, signature?.denominator ?? 4).ToString(CultureInfo.InvariantCulture));
        TextField snapField = CreatePopupTextField("Snap Seconds", Math.Max(0.001, project.settings.snapSeconds).ToString("0.###", CultureInfo.InvariantCulture));

        bool showGrid = project.settings.showBeatGrid;
        bool metronome = project.settings.metronomeEnabled;
        bool noteClaps = project.settings.noteClapsEnabled;
        bool snapEnabled = project.settings.snapEnabled;

        VisualElement summary = CreateInfoRow(
            "Beat at Cursor",
            nearestBeat == null
                ? $"Cursor {FormatTime(project.cursorTimeSeconds)}"
                : $"Bar {nearestBeat.barNumber}.{nearestBeat.beatInBar}  -  beat {nearestBeat.beatPosition:0.###}  -  {FormatTime(nearestBeat.audioTimeSeconds)}",
            new Color(0.78f, 0.58f, 1f, 1f));

        VisualElement toggles = new VisualElement();
        toggles.style.flexDirection = FlexDirection.Column;
        toggles.Add(CreatePopupStateButton("Show Beat Grid", () => showGrid, value => showGrid = value));
        toggles.Add(CreatePopupStateButton("Metronome", () => metronome, value => metronome = value));
        toggles.Add(CreatePopupStateButton("Note Claps", () => noteClaps, value => noteClaps = value));
        toggles.Add(CreatePopupStateButton("Snap Editing", () => snapEnabled, value => snapEnabled = value));

        VisualElement actions = CreatePopupActionGrid(
            CreateCompactButton("Tap Tempo", () => RegisterTapTempo(defaultBpmField, regionBpmField)),
            CreateCompactButton("Add Anchor", () =>
            {
                AddSyncPointAtCursor();
                HideEditPopup();
            }),
            CreateCompactButton("Quantize Notes", QuantizeSelectedNotesToBeatGrid),
            CreateCompactButton("SynchTheory", ShowSynchTheoryPopup),
            CreateCompactButton("Apply Now", () =>
            {
                ChartEditorTimingService.ApplyBeatMapToContent(project);
                project.dirty = true;
                Rebuild();
                HideEditPopup();
            }));

        ShowEditPopup("Beat Map Settings",
            new VisualElement[]
            {
                summary,
                defaultBpmField,
                regionBpmField,
                numeratorField,
                denominatorField,
                snapField,
                toggles,
                actions
            },
            () =>
            {
                if (!TryParseDoubleInRange(defaultBpmField.value, 20.0, 360.0, out double defaultBpm))
                {
                    SetStatus("Default BPM must be between 20 and 360.");
                    return false;
                }

                if (!TryParseDoubleInRange(regionBpmField.value, 20.0, 360.0, out double regionBpm))
                {
                    SetStatus("Current region BPM must be between 20 and 360.");
                    return false;
                }

                if (!int.TryParse((numeratorField.value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int numerator) ||
                    numerator < 1 || numerator > 32)
                {
                    SetStatus("Time signature numerator must be between 1 and 32.");
                    return false;
                }

                if (!int.TryParse((denominatorField.value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int denominator) ||
                    denominator < 1 || denominator > 64)
                {
                    SetStatus("Time signature denominator must be between 1 and 64.");
                    return false;
                }

                if (!TryParseDoubleInRange(snapField.value, 0.001, 2.0, out double snapSeconds))
                {
                    SetStatus("Snap seconds must be between 0.001 and 2.0.");
                    return false;
                }

                project.settings.showBeatGrid = showGrid;
                project.settings.metronomeEnabled = metronome;
                project.settings.noteClapsEnabled = noteClaps;
                project.settings.snapEnabled = snapEnabled;
                project.settings.snapSeconds = snapSeconds;
                ChartEditorTimingService.SetDefaultTempo(project, defaultBpm);
                if (!ChartEditorTimingService.SetTempoRegionBpmAtBeat(project, cursorBeat, regionBpm))
                    SetStatus("Region BPM was not changed because the next anchor is locked.");
                ChartEditorTimingService.SetTimeSignatureAtBeat(project, Math.Round(cursorBeat), numerator, denominator);
                ChartEditorTimingService.ApplyBeatMapToContent(project);
                project.dirty = true;
                HideEditPopup();
                Rebuild();
                return true;
            });
    }

    private void ShowSynchTheoryPopup()
    {
        if (project == null)
            return;

        EnsureAudioClipRequested();
        bool moveContent = true;
        int anchors = ChartEditorTimingService.GetAnchors(project).Count;
        int selectedAnchors = GetSelectedAnchorCount();
        VisualElement summary = CreateInfoRow(
            "SynchTheory",
            editorAudioClip == null && audioLoadInProgress
                ? "Audio is still loading. Open this again in a moment."
                : $"{ChartEditorTimingService.GetBeatMarkers(project).Count} beats, {anchors} manual anchors, {selectedAnchors} selected, {project.tracks?.Sum(track => track?.notes?.Count ?? 0) ?? 0} notes",
            new Color(0.72f, 0.52f, 1f, 1f));

        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Column;
        actions.style.marginTop = 10f;
        actions.Add(CreatePopupStateButton("Move Notes With Beat Map", () => moveContent, value => moveContent = value));

        Button fullSong = CreateCompactButton("Auto-Sync Full Song", () =>
        {
            RunSynchTheoryFullSong(moveContent);
            HideEditPopup();
        });
        fullSong.style.width = Length.Percent(100f);
        fullSong.style.marginBottom = 12f;
        actions.Add(fullSong);

        Button betweenAnchors = CreateCompactButton("Auto-Sync Between Surrounding Anchors", () =>
        {
            RunSynchTheoryBetweenSurroundingAnchors(moveContent);
            HideEditPopup();
        });
        betweenAnchors.style.width = Length.Percent(100f);
        betweenAnchors.style.marginBottom = 12f;
        actions.Add(betweenAnchors);

        Button betweenSelectedAnchors = CreateCompactButton("Auto-Sync Between Selected Anchors", () =>
        {
            RunSynchTheoryBetweenSelectedAnchors(moveContent);
            HideEditPopup();
        });
        betweenSelectedAnchors.style.width = Length.Percent(100f);
        betweenSelectedAnchors.style.marginBottom = 12f;
        actions.Add(betweenSelectedAnchors);

        Button clearGenerated = CreateCompactButton("Clear Generated SynchTheory Timing", () =>
        {
            int removed = ChartEditorTimingService.ClearGeneratedSynchTheoryTiming(project);
            project.dirty = true;
            SetStatus(removed > 0 ? $"Cleared {removed} generated SynchTheory timing points." : "No generated SynchTheory timing points were present.");
            HideEditPopup();
            Rebuild();
        });
        clearGenerated.style.width = Length.Percent(100f);
        StyleDangerButton(clearGenerated);
        actions.Add(clearGenerated);

        ShowEditPopup("SynchTheory",
            new VisualElement[]
            {
                summary,
                CreateInfoRow("Workflow", "Run full song first. If a section drifts, Ctrl-click two or more anchors and run between selected anchors.", new Color(0.70f, 0.78f, 0.90f, 1f)),
                actions
            },
            () =>
            {
                HideEditPopup();
                return true;
            });
    }

    private void RunSynchTheoryFullSong(bool moveContent)
    {
        SynchTheory.SynchTheoryOptions options = SynchTheory.SynchTheoryOptions.Default();
        options.scope = SynchTheory.SynchTheoryRunScope.FullSong;
        options.moveContentWithBeatMap = moveContent;
        options.boundarySearchSeconds = 8.0;
        ExecuteSynchTheory(options);
    }

    private void RunSynchTheoryBetweenSurroundingAnchors(bool moveContent)
    {
        if (project == null)
            return;

        ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: false);
        double cursorBeat = ChartEditorTimingService.GetBeatPositionForAudioTime(project, project.cursorTimeSeconds);
        List<ChartEditorBeatMarker> anchors = ChartEditorTimingService.GetAnchors(project);
        ChartEditorBeatMarker left = anchors
            .Where(anchor => anchor != null && anchor.beatPosition < cursorBeat - 0.0001)
            .OrderByDescending(anchor => anchor.beatPosition)
            .FirstOrDefault();
        ChartEditorBeatMarker right = anchors
            .Where(anchor => anchor != null && anchor.beatPosition > cursorBeat + 0.0001)
            .OrderBy(anchor => anchor.beatPosition)
            .FirstOrDefault();

        if (left == null || right == null)
        {
            SetStatus("Add or select two manual anchors around the section, then run SynchTheory between anchors.");
            return;
        }

        SynchTheory.SynchTheoryOptions options = SynchTheory.SynchTheoryOptions.Default();
        options.scope = SynchTheory.SynchTheoryRunScope.BeatRange;
        options.startBeat = left.beatPosition;
        options.endBeat = right.beatPosition;
        options.moveContentWithBeatMap = moveContent;
        options.boundarySearchSeconds = 4.0;
        ExecuteSynchTheory(options);
    }

    private void RunSynchTheoryBetweenSelectedAnchors(bool moveContent)
    {
        if (project == null)
            return;

        ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: false);
        List<ChartEditorBeatMarker> anchors = GetSelectedAnchors();
        if (anchors.Count < 2)
        {
            SetStatus("Ctrl-click at least two manual anchors, then run SynchTheory between selected anchors.");
            return;
        }

        ChartEditorBeatMarker left = anchors.First();
        ChartEditorBeatMarker right = anchors.Last();
        if (right.beatPosition <= left.beatPosition + 0.0001)
        {
            SetStatus("Selected anchors must define a non-empty beat range.");
            return;
        }

        SynchTheory.SynchTheoryOptions options = SynchTheory.SynchTheoryOptions.Default();
        options.scope = SynchTheory.SynchTheoryRunScope.BeatRange;
        options.startBeat = left.beatPosition;
        options.endBeat = right.beatPosition;
        options.moveContentWithBeatMap = moveContent;
        options.boundarySearchSeconds = 4.0;
        ExecuteSynchTheory(options);
    }

    private void ExecuteSynchTheory(SynchTheory.SynchTheoryOptions options)
    {
        if (project == null)
            return;

        EnsureAudioClipRequested();
        if (editorAudioClip == null)
        {
            SetStatus(audioLoadInProgress ? "Audio is still loading for SynchTheory." : "SynchTheory needs a decoded audio file.");
            return;
        }

        SetStatus("SynchTheory analyzing audio and score...");
        if (!ChartEditorSynchTheoryAdapter.TryBuildAudioData(editorAudioClip, out SynchTheory.SynchTheoryAudioData audio, out string audioError))
        {
            SetStatus(audioError);
            return;
        }

        SynchTheory.SynchTheoryScoreMap score = ChartEditorSynchTheoryAdapter.BuildScoreMap(project);
        SynchTheory.SynchTheoryAlignmentResult result = SynchTheory.SynchTheoryEngine.Align(score, audio, options);
        if (result == null || !result.success)
        {
            SetStatus(result?.message ?? "SynchTheory could not align this song.");
            return;
        }

        int applied = ChartEditorSynchTheoryAdapter.ApplyResult(project, result, options.moveContentWithBeatMap, out string summary);
        string warning = result.warnings != null && result.warnings.Count > 0 ? $" {result.warnings[0]}" : string.Empty;
        SetStatus(applied > 0 ? $"{summary}{warning}" : result.message);
        Rebuild();
    }

    private Button CreatePopupStateButton(string text, Func<bool> getter, Action<bool> setter)
    {
        Button button = null;
        Label label = null;
        Label state = null;
        void Refresh()
        {
            bool enabled = getter?.Invoke() == true;
            ApplyToggleButtonChrome(button, enabled);
            if (label != null)
                label.style.color = enabled ? new Color(0.88f, 1f, 0.92f, 1f) : new Color(0.80f, 0.86f, 0.94f, 1f);
            if (state != null)
            {
                state.text = enabled ? "ON" : "OFF";
                state.style.color = enabled ? new Color(0.72f, 1f, 0.80f, 1f) : new Color(0.66f, 0.72f, 0.82f, 1f);
            }
        }

        button = new Button(() =>
        {
            setter?.Invoke(!(getter?.Invoke() == true));
            Refresh();
        });
        button.text = string.Empty;
        button.focusable = false;
        button.style.flexDirection = FlexDirection.Row;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.SpaceBetween;
        button.style.height = 66f;
        button.style.width = Length.Percent(100f);
        button.style.marginBottom = 12f;
        button.style.paddingLeft = 20f;
        button.style.paddingRight = 18f;
        button.style.unityFontDefinition = bodyFont;

        label = CreateLabel(text, 22f, new Color(0.80f, 0.86f, 0.94f, 1f), true, TextAnchor.MiddleLeft, false);
        label.style.flexGrow = 1f;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        state = CreateLabel("OFF", 18f, new Color(0.66f, 0.72f, 0.82f, 1f), true, TextAnchor.MiddleRight, false);
        state.style.width = 70f;
        state.style.flexShrink = 0f;
        button.Add(label);
        button.Add(state);
        Refresh();
        return button;
    }

    private void RegisterTapTempo(TextField defaultBpmField, TextField regionBpmField)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if (lastTapTempoRealtime > 0.0 && now - lastTapTempoRealtime < 2.0)
        {
            double interval = Math.Max(0.05, now - lastTapTempoRealtime);
            tapTempoAverageIntervalSeconds = tapTempoAverageIntervalSeconds <= 0.001
                ? interval
                : Mathf.Lerp((float)tapTempoAverageIntervalSeconds, (float)interval, 0.35f);
            double bpm = Math.Max(20.0, Math.Min(360.0, 60.0 / tapTempoAverageIntervalSeconds));
            string formatted = bpm.ToString("0.###", CultureInfo.InvariantCulture);
            if (defaultBpmField != null)
                defaultBpmField.value = formatted;
            if (regionBpmField != null)
                regionBpmField.value = formatted;
            SetStatus($"Tap tempo: {formatted} BPM");
        }
        else
        {
            tapTempoAverageIntervalSeconds = 0.0;
            SetStatus("Tap again to calculate tempo.");
        }

        lastTapTempoRealtime = now;
    }

    private void ShowSongInfoPopup()
    {
        if (project?.metadata == null)
            return;

        TextField titleField = CreatePopupTextField("Title", project.metadata.title ?? string.Empty);
        TextField artistField = CreatePopupTextField("Artist", project.metadata.artist ?? string.Empty);
        TextField albumField = CreatePopupTextField("Album", project.metadata.album ?? string.Empty);
        TextField genreField = CreatePopupTextField("Genre", project.metadata.genre ?? string.Empty);
        TextField yearField = CreatePopupTextField("Year", project.metadata.year ?? string.Empty);
        string coverImagePath = project.metadata.coverImagePath ?? string.Empty;
        VisualElement coverField = CreateCoverImagePicker(
            () => coverImagePath,
            value => coverImagePath = value ?? string.Empty);

        ShowEditPopup("Song Info", new VisualElement[] { titleField, artistField, albumField, genreField, yearField, coverField }, () =>
        {
            project.metadata.title = titleField.value?.Trim() ?? string.Empty;
            project.metadata.artist = artistField.value?.Trim() ?? string.Empty;
            project.metadata.album = albumField.value?.Trim() ?? string.Empty;
            project.metadata.genre = genreField.value?.Trim() ?? string.Empty;
            project.metadata.year = yearField.value?.Trim() ?? string.Empty;
            project.metadata.coverImagePath = coverImagePath?.Trim() ?? string.Empty;
            project.dirty = true;
            HideEditPopup();
            Rebuild();
            return true;
        });
    }

    private void ShowSectionEditPopup(ChartEditorSection section)
    {
        if (section == null)
            return;

        TextField nameField = CreatePopupTextField("Name", section.name ?? string.Empty);
        TextField startField = CreatePopupTextField("Start Seconds", section.startTimeSeconds.ToString("0.000", CultureInfo.InvariantCulture));
        TextField endField = CreatePopupTextField("End Seconds", section.endTimeSeconds.ToString("0.000", CultureInfo.InvariantCulture));

        ShowEditPopup("Edit Section", new VisualElement[] { nameField, startField, endField }, () =>
        {
            if (!TryParseDoubleInRange(startField.value, 0.0, project.DurationSeconds, out double start))
            {
                SetStatus($"Section start must be between 0 and {project.DurationSeconds:0.000} seconds.");
                return false;
            }

            if (!TryParseDoubleInRange(endField.value, start + 0.05, Math.Max(project.DurationSeconds + 60.0, start + 0.05), out double end))
            {
                SetStatus("Section end must be after section start.");
                return false;
            }

            section.name = string.IsNullOrWhiteSpace(nameField.value) ? "Section" : nameField.value.Trim();
            section.startTimeSeconds = start;
            section.chartStartTimeSeconds = start;
            section.endTimeSeconds = end;
            section.chartEndTimeSeconds = end;
            ChartEditorTimingService.UpdateSectionBeatTiming(project, section);
            section.userEdited = true;
            selectedSectionId = section.id;
            ClearNoteSelection();
            selectedSyncPointId = null;
            project.cursorTimeSeconds = section.startTimeSeconds;
            mode = ChartEditorMode.Sections;
            project.dirty = true;
            HideEditPopup();
            Rebuild();
            return true;
        });
    }

    private void ShowMoveSelectedNotesPopup()
    {
        List<ChartEditorNoteReference> refs = GetSelectedNoteReferences();
        if (refs.Count == 0)
            return;

        TextField timeField = CreatePopupTextField("Time Delta Seconds", "0.000");
        TextField laneField = CreatePopupTextField("String/Lane Delta", "0");

        ShowEditPopup($"Move {refs.Count} Notes", new VisualElement[] { timeField, laneField }, () =>
        {
            if (!double.TryParse((timeField.value ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double timeDelta))
            {
                SetStatus("Time delta must be a number, for example 0.125 or -0.050.");
                return false;
            }

            if (!int.TryParse((laneField.value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int laneDelta))
            {
                SetStatus("String/lane delta must be a whole number.");
                return false;
            }

            MoveSelectedNotes(timeDelta, laneDelta);
            HideEditPopup();
            return true;
        });
    }

    private void MoveSelectedNotes(double timeDelta, int laneDelta)
    {
        List<ChartEditorNoteReference> refs = GetSelectedNoteReferences();
        if (refs.Count == 0)
            return;

        for (int i = 0; i < refs.Count; i++)
        {
            ChartEditorNoteReference noteRef = refs[i];
            ChartEditorNote note = noteRef.note;
            double originalTime = note.timeSeconds;
            note.timeSeconds = Math.Max(0.0, Math.Min(project.DurationSeconds, note.timeSeconds + timeDelta));
            note.chartTimeSeconds = Math.Max(0.0, note.chartTimeSeconds + (note.timeSeconds - originalTime));
            note.stringOrLane = Mathf.Clamp(note.stringOrLane + laneDelta, 0, Math.Max(0, GetTrackLaneCount(noteRef.track) - 1));
            ChartEditorTimingService.UpdateNoteBeatTiming(project, note);
        }

        SortSelectedNoteTracks(refs);
        project.dirty = true;
        Rebuild();
    }

    private void MoveSelectedNotesToCursor()
    {
        List<ChartEditorNoteReference> refs = GetSelectedNoteReferences();
        if (refs.Count == 0)
            return;

        double earliest = refs.Min(noteRef => noteRef.note.timeSeconds);
        MoveSelectedNotes(project.cursorTimeSeconds - earliest, 0);
    }

    private void MoveAnchorToCursor(ChartEditorBeatMarker point)
    {
        if (project == null || point == null)
            return;
        if (point.locked)
        {
            SetStatus("Anchor is locked.");
            return;
        }

        ChartEditorTimingService.MoveAnchor(project, point, project.cursorTimeSeconds);
        SelectSingleAnchor(point);
        project.dirty = true;
        Rebuild();
    }

    private void DeleteSelectedAnchors()
    {
        if (project == null)
            return;

        List<string> anchorIds = GetSelectedAnchors()
            .Where(anchor => anchor != null && !string.IsNullOrWhiteSpace(anchor.id))
            .Select(anchor => anchor.id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (anchorIds.Count == 0)
            return;

        int removed = 0;
        foreach (string anchorId in anchorIds)
        {
            ChartEditorBeatMarker anchor = ChartEditorTimingService.GetAnchors(project)
                .FirstOrDefault(point => point != null && string.Equals(point.id, anchorId, StringComparison.OrdinalIgnoreCase));
            if (anchor == null)
                continue;

            ChartEditorTimingService.RemoveAnchor(project, anchor);
            removed++;
        }

        ClearAnchorSelection();
        project.dirty = true;
        SetStatus(removed == 1 ? "Deleted 1 anchor." : $"Deleted {removed} anchors.");
        Rebuild();
    }

    private void SetSelectedAnchorsLocked(bool locked)
    {
        if (project == null)
            return;

        List<string> anchorIds = GetSelectedAnchors()
            .Where(anchor => anchor != null && !string.IsNullOrWhiteSpace(anchor.id))
            .Select(anchor => anchor.id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (anchorIds.Count == 0)
            return;

        int changed = 0;
        foreach (ChartEditorBeatMarker anchor in ChartEditorTimingService.GetAnchors(project))
        {
            if (anchor == null || !anchorIds.Contains(anchor.id, StringComparer.OrdinalIgnoreCase))
                continue;

            anchor.locked = locked;
            changed++;
        }

        project.dirty = true;
        SetStatus($"{(locked ? "Locked" : "Unlocked")} {changed} selected anchors.");
        Rebuild();
    }

    private void ShowEditPopup(string title, IEnumerable<VisualElement> fields, Func<bool> apply)
    {
        HideContextMenu();
        HideEditPopup();

        VisualElement overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.right = 0f;
        overlay.style.top = 0f;
        overlay.style.bottom = 0f;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.34f);
        overlay.pickingMode = PickingMode.Position;
        overlay.RegisterCallback<PointerDownEvent>(evt =>
        {
            HideEditPopup();
            evt.StopPropagation();
        });

        VisualElement panel = new VisualElement();
        panel.style.width = 820f;
        panel.style.paddingLeft = 34f;
        panel.style.paddingRight = 34f;
        panel.style.paddingTop = 30f;
        panel.style.paddingBottom = 30f;
        StylePopupPanel(panel, new Color(0.030f, 0.036f, 0.048f, 1f), 16f);
        panel.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

        Label titleLabel = CreateLabel(title, 32f, Color.white, true, TextAnchor.MiddleLeft, false);
        titleLabel.style.marginBottom = 18f;
        panel.Add(titleLabel);

        TextField firstField = null;
        foreach (VisualElement field in fields ?? Enumerable.Empty<VisualElement>())
        {
            if (firstField == null)
                firstField = field as TextField;
            panel.Add(field);
        }

        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.justifyContent = Justify.FlexEnd;
        actions.style.marginTop = 18f;
        Button cancel = CreateCompactButton("Cancel", HideEditPopup);
        Button applyButton = CreateCompactButton("Apply", () => apply?.Invoke());
        StyleFilledButton(applyButton, new Color(0.62f, 0.38f, 1f, 1f), darkText: false);
        cancel.style.marginRight = 12f;
        actions.Add(cancel);
        actions.Add(applyButton);
        panel.Add(actions);

        overlay.Add(panel);
        editPopupElement = overlay;
        RootElement.Add(editPopupElement);
        editPopupElement.BringToFront();
        SetChartEditorKeyboardCaptureActive(true);

        if (firstField != null)
            firstField.schedule.Execute(() =>
            {
                firstField.Focus();
                firstField.SelectAll();
            });
    }

    private void HideEditPopup()
    {
        if (editPopupElement == null)
            return;

        editPopupElement.RemoveFromHierarchy();
        editPopupElement = null;
        UpdateChartEditorKeyboardCaptureState();
    }

    private TextField CreatePopupTextField(string label, string value)
    {
        TextField field = new TextField(label);
        field.value = value ?? string.Empty;
        field.style.height = 76f;
        field.style.marginBottom = 12f;
        field.style.fontSize = UiFont(24f);
        field.style.unityFontDefinition = bodyFont;
        StyleTextField(field);
        RegisterTextFieldKeyboardCapture(field);
        return field;
    }

    private VisualElement CreateCoverImagePicker(Func<string> getPath, Action<string> setPath)
    {
        VisualElement field = new VisualElement();
        field.style.marginBottom = 14f;
        field.style.paddingLeft = 16f;
        field.style.paddingRight = 16f;
        field.style.paddingTop = 14f;
        field.style.paddingBottom = 14f;
        field.style.backgroundColor = new Color(0.018f, 0.022f, 0.030f, 0.98f);
        SetRadius(field, 11f);
        SetBorderWidth(field, 1f);
        SetBorderColor(field, new Color(0.24f, 0.27f, 0.33f, 0.95f));

        Label label = CreateLabel("Image", 17f, new Color(0.72f, 0.78f, 0.88f, 1f), true, TextAnchor.MiddleLeft, false);
        label.style.marginBottom = 10f;
        field.Add(label);

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;

        VisualElement preview = new VisualElement();
        preview.style.width = 96f;
        preview.style.height = 96f;
        preview.style.minWidth = 96f;
        preview.style.minHeight = 96f;
        preview.style.flexShrink = 0f;
        preview.style.backgroundColor = new Color(0.010f, 0.013f, 0.018f, 1f);
        SetRadius(preview, 10f);
        SetBorderWidth(preview, 1f);
        SetBorderColor(preview, new Color(0.26f, 0.29f, 0.35f, 0.95f));
        row.Add(preview);

        VisualElement details = new VisualElement();
        details.style.flexGrow = 1f;
        details.style.marginLeft = 18f;
        details.style.minWidth = 0f;

        Label pathLabel = CreateLabel(string.Empty, 19f, new Color(0.86f, 0.90f, 0.96f, 1f), false, TextAnchor.MiddleLeft, false);
        pathLabel.style.whiteSpace = WhiteSpace.NoWrap;
        details.Add(pathLabel);

        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.marginTop = 12f;

        Texture2D previewTexture = null;

        void ReleasePreviewTexture()
        {
            if (previewTexture == null)
                return;

            UnityEngine.Object.Destroy(previewTexture);
            previewTexture = null;
        }

        void Refresh()
        {
            ReleasePreviewTexture();

            string path = getPath?.Invoke() ?? string.Empty;
            pathLabel.text = string.IsNullOrWhiteSpace(path) ? "No image selected" : Path.GetFileName(path);
            preview.style.backgroundImage = StyleKeyword.None;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            try
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    UnityEngine.Object.Destroy(texture);
                    return;
                }

                previewTexture = texture;
                preview.style.backgroundImage = new StyleBackground(previewTexture);
            }
            catch
            {
                ReleasePreviewTexture();
                preview.style.backgroundImage = StyleKeyword.None;
            }
        }

        Button choose = CreateCompactButton("Choose Image", () =>
        {
            if (!ChartEditorFilePicker.TryPickImageFile(out string path))
                return;

            setPath?.Invoke(path);
            Refresh();
        });
        choose.style.marginLeft = 0f;

        Button clear = CreateCompactButton("Clear", () =>
        {
            setPath?.Invoke(string.Empty);
            Refresh();
        });
        clear.style.marginLeft = 10f;

        actions.Add(choose);
        actions.Add(clear);
        details.Add(actions);
        row.Add(details);
        field.Add(row);
        field.RegisterCallback<DetachFromPanelEvent>(_ => ReleasePreviewTexture());

        Refresh();
        return field;
    }

    private DropdownField CreatePopupDropdownField(string label, List<string> choices, string value)
    {
        DropdownField field = new DropdownField(label);
        field.choices = choices ?? new List<string>();
        field.value = field.choices.Contains(value) ? value : field.choices.FirstOrDefault() ?? string.Empty;
        field.style.height = 76f;
        field.style.marginBottom = 12f;
        field.style.fontSize = UiFont(24f);
        field.style.unityFontDefinition = bodyFont;
        StylePopupDropdownField(field);
        return field;
    }

    private static void StylePopupDropdownField(DropdownField field)
    {
        if (field == null)
            return;

        field.style.color = Color.white;
        field.style.backgroundColor = new Color(0.018f, 0.022f, 0.030f, 0.98f);
        field.style.borderTopWidth = 1f;
        field.style.borderRightWidth = 1f;
        field.style.borderBottomWidth = 1f;
        field.style.borderLeftWidth = 1f;
        SetToneLabBorder(field,
            new Color(0.38f, 0.40f, 0.44f, 0.84f),
            new Color(0.22f, 0.24f, 0.28f, 0.95f),
            new Color(0.13f, 0.15f, 0.18f, 1f));
        SetRadius(field, 11f);
        field.style.paddingLeft = 14f;
        field.style.paddingRight = 14f;

        field.schedule.Execute(() =>
        {
            Label label = field.Q<Label>();
            if (label != null)
            {
                label.style.color = new Color(0.72f, 0.78f, 0.88f, 1f);
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            VisualElement input = field.Q(className: "unity-base-field__input");
            if (input == null)
                return;

            input.style.color = Color.white;
            input.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            input.style.borderTopWidth = 0f;
            input.style.borderRightWidth = 0f;
            input.style.borderBottomWidth = 0f;
            input.style.borderLeftWidth = 0f;
            input.style.fontSize = UiFont(24f);
        });
    }

    private static bool TryParseIntInRange(string value, int min, int max, out int result)
    {
        return int.TryParse((value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result) &&
               result >= min &&
               result <= max;
    }

    private static bool TryParseDoubleInRange(string value, double min, double max, out double result)
    {
        return double.TryParse((value ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
               result >= min &&
               result <= max;
    }

    private static bool TryParseFloatInRange(string value, float min, float max, out float result)
    {
        return float.TryParse((value ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
               result >= min &&
               result <= max;
    }

    private static bool TryParseTechniqueSegmentType(string value, out NoteTechniqueSegmentType type)
    {
        string normalized = (value ?? string.Empty).Trim();
        if (Enum.TryParse(normalized, ignoreCase: true, out type))
            return true;

        switch (normalized.ToLowerInvariant())
        {
            case "sus":
            case "sustain":
                type = NoteTechniqueSegmentType.Sustain;
                return true;
            case "vib":
            case "vibrato":
                type = NoteTechniqueSegmentType.Vibrato;
                return true;
            case "bend":
                type = NoteTechniqueSegmentType.Bend;
                return true;
            case "slide":
            case "sl":
                type = NoteTechniqueSegmentType.Slide;
                return true;
            default:
                type = NoteTechniqueSegmentType.Sustain;
                return false;
        }
    }

    private VisualElement BuildInspectorPanel()
    {
        ScrollView panel = new ScrollView(ScrollViewMode.Vertical);
        ConfigureScrollView(panel);
        panel.style.width = InspectorWidth;
        panel.style.minWidth = InspectorWidth;
        panel.style.paddingLeft = 30f;
        panel.style.paddingRight = 30f;
        panel.style.paddingTop = 26f;
        panel.style.paddingBottom = 26f;
        StylePanel(panel, new Color(0.050f, 0.058f, 0.070f, 0.99f), new Color(0.17f, 0.21f, 0.27f, 1f), 0f);

        string inspectorContext = mode == ChartEditorMode.Notes && FindSelectedNote() != null
            ? "NOTE"
            : mode == ChartEditorMode.Sections && FindSelectedSection() != null
                ? "SECTION"
                : mode == ChartEditorMode.SyncTiming && FindSelectedAnchor() != null
                    ? "ANCHOR"
                    : mode == ChartEditorMode.SongInfo
                        ? "SONG INFO"
                        : "TRACK";
        panel.Add(CreateInspectorHeader(inspectorContext));

        if (mode == ChartEditorMode.SongInfo)
        {
            BuildSongInfoInspector(panel);
            return panel;
        }

        ChartEditorNote note = FindSelectedNote();
        ChartEditorSection section = FindSelectedSection();
        ChartEditorBeatMarker syncPoint = FindSelectedAnchor();

        if (mode == ChartEditorMode.Notes && note != null)
            BuildNoteInspector(panel, note);
        else if (mode == ChartEditorMode.Sections && section != null)
            BuildSectionInspector(panel, section);
        else if (mode == ChartEditorMode.SyncTiming && syncPoint != null)
            BuildSyncInspector(panel, syncPoint);
        else
            BuildTrackInspector(panel, project.SelectedTrack);

        return panel;
    }

    private VisualElement BuildBottomToolbar()
    {
        ScrollView toolbar = new ScrollView(ScrollViewMode.Horizontal);
        StyleModernScrollView(toolbar);
        toolbar.style.height = 156f;
        toolbar.style.minHeight = 156f;
        toolbar.style.marginTop = 18f;
        toolbar.style.paddingLeft = 18f;
        toolbar.style.paddingRight = 18f;
        toolbar.style.paddingTop = 18f;
        toolbar.style.paddingBottom = 18f;
        StylePanel(toolbar, new Color(0.042f, 0.052f, 0.070f, 0.99f), new Color(0.25f, 0.36f, 0.52f, 0.78f), 6f);
        toolbar.contentContainer.style.flexDirection = FlexDirection.Row;
        toolbar.contentContainer.style.alignItems = Align.Center;

        toolbar.Add(CreateToolbarButton("Zoom -", () => AdjustTimelineZoomAroundViewportCenter(-1)));
        toolbar.Add(CreateToolbarButton($"Zoom {Mathf.RoundToInt(timelineZoom * 100f)}%", ResetTimelineZoom));
        toolbar.Add(CreateToolbarButton("Zoom +", () => AdjustTimelineZoomAroundViewportCenter(1)));
        toolbar.Add(CreateToolbarButton("Move -100ms", () => MoveScope(-0.1)));
        toolbar.Add(CreateToolbarButton("Move +100ms", () => MoveScope(0.1)));
        toolbar.Add(CreateToolbarButton("Move After +100ms", () =>
        {
            ChartEditorTimingService.MoveEverythingAfter(project, project.cursorTimeSeconds, 0.1);
            Rebuild();
        }));
        toolbar.Add(CreateToolbarButton("Stretch Region +1%", () =>
        {
            ChartEditorTimingService.StretchRegion(project, project.cursorTimeSeconds, project.cursorTimeSeconds + 8.0, 1.01);
            Rebuild();
        }));
        toolbar.Add(CreateToolbarButton("Add Note", AddNoteAtCursor));
        toolbar.Add(CreateToolbarButton("Add Section", AddSectionAtCursor));
        toolbar.Add(CreateToolbarButton("Add Anchor", AddSyncPointAtCursor));
        toolbar.Add(CreateToolbarButton("Apply Beat Map", () =>
        {
            ChartEditorTimingService.ApplyBeatMapToContent(project);
            Rebuild();
        }));
        return toolbar;
    }

    private void BuildSectionBar(VisualElement timeline)
    {
        AddTimelineRowLabel(timeline, "SECTIONS", 0f, SectionBarHeight);
        VisualElement sectionLayer = new VisualElement();
        sectionLayer.style.position = Position.Absolute;
        sectionLayer.style.left = TimelineLabelWidth;
        sectionLayer.style.right = 0f;
        sectionLayer.style.top = 0f;
        sectionLayer.style.height = SectionBarHeight;
        sectionLayer.style.backgroundColor = new Color(0.050f, 0.062f, 0.082f, 1f);
        timeline.Add(sectionLayer);

        if (project.sections == null)
            return;

        foreach (ChartEditorSection section in project.sections)
        {
            if (section == null)
                continue;

            float left = TimeToPixels(section.startTimeSeconds);
            float width = Mathf.Max(90f, TimeToPixels(section.endTimeSeconds) - left);
            Button block = new Button(() =>
            {
                selectedSectionId = section.id;
                ClearNoteSelection();
                selectedSyncPointId = null;
                mode = ChartEditorMode.Sections;
                Rebuild();
            });
            block.text = section.name;
            block.style.position = Position.Absolute;
            block.style.left = left;
            block.style.top = 0f;
            block.style.height = SectionBarHeight;
            block.style.width = width;
            block.style.minWidth = 150f;
            block.style.fontSize = UiFont(19f);
            block.style.unityFontDefinition = bodyFont;
            block.style.unityFontStyleAndWeight = FontStyle.Bold;
            block.style.backgroundColor = SectionColor(Mathf.Abs(section.name?.GetHashCode() ?? 0));
            block.style.color = Color.white;
            block.style.borderTopWidth = 0f;
            block.style.borderRightWidth = 1f;
            block.style.borderBottomWidth = string.Equals(selectedSectionId, section.id, StringComparison.OrdinalIgnoreCase) ? 3f : 0f;
            block.style.borderLeftWidth = 0f;
            block.style.borderRightColor = new Color(0.06f, 0.08f, 0.11f, 0.95f);
            block.style.borderBottomColor = Color.white;
            block.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount >= 2)
                {
                    ShowSectionEditPopup(section);
                    evt.StopPropagation();
                }
            });
            block.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 1)
                {
                    selectedSectionId = section.id;
                    ClearNoteSelection();
                    selectedSyncPointId = null;
                    mode = ChartEditorMode.Sections;
                    ShowSectionContextMenu(evt.position, section);
                    evt.StopPropagation();
                }
            });
            AddSectionDragHandlers(block, section);
            sectionLayer.Add(block);
        }
    }

    private void BuildWaveform(VisualElement timeline)
    {
        AddTimelineRowLabel(timeline, "WAVEFORM", WaveformTop, WaveformHeight);
        VisualElement wave = new VisualElement();
        wave.style.position = Position.Absolute;
        wave.style.left = TimelineLabelWidth;
        wave.style.right = 0f;
        wave.style.top = WaveformTop;
        wave.style.height = WaveformHeight;
        wave.style.backgroundColor = new Color(0.010f, 0.014f, 0.020f, 1f);
        wave.pickingMode = PickingMode.Position;
        AddSeekDragHandlers(wave, worldPosition => wave.WorldToLocal(worldPosition).x);

        if (waveformData != null && waveformData.IsValid)
        {
            WaveformVectorElement waveform = new WaveformVectorElement(this);
            waveform.style.position = Position.Absolute;
            waveform.style.top = 0f;
            waveform.style.height = WaveformHeight;
            waveformVectorElement = waveform;
            UpdateWaveformVectorElementLayout();
            wave.Add(waveform);
        }
        else
        {
            VisualElement centerLine = new VisualElement();
            centerLine.style.position = Position.Absolute;
            centerLine.style.left = 0f;
            centerLine.style.right = 0f;
            centerLine.style.top = WaveformHeight * 0.5f - 1f;
            centerLine.style.height = 2f;
            centerLine.style.backgroundColor = new Color(0.22f, 0.31f, 0.42f, 0.52f);
            centerLine.pickingMode = PickingMode.Ignore;
            wave.Add(centerLine);

            string message = string.IsNullOrWhiteSpace(project?.audio?.sourcePath)
                ? "No audio attached"
                : audioLoadInProgress
                    ? "Loading waveform..."
                    : string.IsNullOrWhiteSpace(audioLoadError)
                        ? "Waveform unavailable"
                        : audioLoadError;
            Label label = CreateLabel(message, 24f, new Color(0.78f, 0.82f, 0.90f, 0.90f), false, TextAnchor.MiddleCenter, false);
            label.style.position = Position.Absolute;
            label.style.left = 0f;
            label.style.right = 0f;
            label.style.top = 0f;
            label.style.bottom = 0f;
            label.pickingMode = PickingMode.Ignore;
            wave.Add(label);
        }

        timeline.Add(wave);

        AddTimelineGridLine(timeline, WaveformTop + WaveformHeight + 10f);
    }

    private void BuildWaveformSeekLayer(VisualElement timeline)
    {
        VisualElement seekLayer = new VisualElement();
        seekLayer.style.position = Position.Absolute;
        seekLayer.style.left = TimelineLabelWidth;
        seekLayer.style.right = 0f;
        seekLayer.style.top = WaveformTop;
        seekLayer.style.height = WaveformHeight;
        seekLayer.style.backgroundColor = Color.clear;
        seekLayer.pickingMode = PickingMode.Position;
        AddSeekDragHandlers(seekLayer, worldPosition => seekLayer.WorldToLocal(worldPosition).x);
        timeline.Add(seekLayer);
    }

    private void BuildBeatGrid(VisualElement timeline)
    {
        currentBeatMarkerVisuals.Clear();
        if (project?.settings?.showBeatGrid == false)
            return;

        List<ChartEditorBeatMarker> markers = ChartEditorTimingService.GetBeatMarkers(project);
        if (markers.Count == 0)
            return;

        VisualElement visualLayer = new VisualElement();
        visualLayer.style.position = Position.Absolute;
        visualLayer.style.left = TimelineLabelWidth;
        visualLayer.style.right = 0f;
        visualLayer.style.top = WaveformTop;
        visualLayer.style.bottom = 0f;
        visualLayer.pickingMode = PickingMode.Ignore;
        timeline.Add(visualLayer);

        float timelineHeight = GetTimelineContentHeight();
        float layerHeight = Mathf.Max(1f, timelineHeight - WaveformTop);
        float pixelsPerSecond = GetTimelinePixelsPerSecond();
        double secondsPerBeat = markers.Count > 1
            ? Math.Max(0.001, markers[1].audioTimeSeconds - markers[0].audioTimeSeconds)
            : 0.5;
        float beatSpacingPixels = Mathf.Max(1f, (float)secondsPerBeat * pixelsPerSecond);
        bool drawRegularBeats = beatSpacingPixels >= 14f;
        bool drawLabels = beatSpacingPixels >= 42f;

        for (int i = 0; i < markers.Count; i++)
        {
            ChartEditorBeatMarker marker = markers[i];
            if (marker == null)
                continue;

            bool important = marker.isDownbeat || marker.isAnchor;
            if (!drawRegularBeats && !important)
                continue;

            BeatMarkerVisual visual = new BeatMarkerVisual
            {
                markerId = marker.id ?? string.Empty,
                beatPosition = marker.beatPosition
            };

            float left = TimeToPixels(marker.audioTimeSeconds);
            VisualElement hit = new VisualElement();
            hit.style.position = Position.Absolute;
            hit.style.left = TimelineLabelWidth + left - BeatMarkerHitWidth * 0.5f;
            hit.style.top = WaveformTop;
            hit.style.width = BeatMarkerHitWidth;
            hit.style.height = layerHeight;
            hit.style.backgroundColor = Color.clear;
            hit.pickingMode = PickingMode.Position;
            AddBeatMarkerInteractionHandlers(hit, marker);
            timeline.Add(hit);
            visual.hit = hit;

            VisualElement line = new VisualElement();
            line.style.position = Position.Absolute;
            line.style.left = left;
            line.style.top = 0f;
            line.style.width = marker.isAnchor ? 5f : marker.isDownbeat ? 3f : 2f;
            line.style.height = layerHeight;
            line.style.marginLeft = marker.isAnchor ? -2.5f : marker.isDownbeat ? -1.5f : -1f;
            line.style.backgroundColor = marker.isAnchor
                ? new Color(0.78f, 0.48f, 1f, 0.82f)
                : marker.isDownbeat
                    ? new Color(0.72f, 0.82f, 0.96f, 0.52f)
                    : new Color(0.50f, 0.60f, 0.74f, 0.30f);
            line.pickingMode = PickingMode.Ignore;
            visualLayer.Add(line);
            visual.line = line;

            if (marker.isDownbeat && drawLabels)
            {
                Label label = CreateLabel(marker.barNumber.ToString(CultureInfo.InvariantCulture), 20f, new Color(0.88f, 0.92f, 1f, 0.88f), true, TextAnchor.MiddleLeft, false);
                label.style.position = Position.Absolute;
                label.style.left = left + 6f;
                label.style.top = 6f;
                label.style.width = 84f;
                label.style.height = 28f;
                label.pickingMode = PickingMode.Ignore;
                visualLayer.Add(label);
                visual.label = label;
            }

            currentBeatMarkerVisuals.Add(visual);
        }
    }

    private void UpdateBeatGridVisuals()
    {
        if (project?.beatMap?.beatMarkers == null || currentBeatMarkerVisuals.Count == 0)
            return;

        Dictionary<string, ChartEditorBeatMarker> markersById = project.beatMap.beatMarkers
            .Where(marker => marker != null && !string.IsNullOrWhiteSpace(marker.id))
            .GroupBy(marker => marker.id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < currentBeatMarkerVisuals.Count; i++)
        {
            BeatMarkerVisual visual = currentBeatMarkerVisuals[i];
            if (visual == null)
                continue;

            ChartEditorBeatMarker marker = null;
            if (!string.IsNullOrWhiteSpace(visual.markerId))
                markersById.TryGetValue(visual.markerId, out marker);

            marker ??= project.beatMap.beatMarkers.FirstOrDefault(candidate =>
                candidate != null &&
                Math.Abs(candidate.beatPosition - visual.beatPosition) <= 0.0001);

            if (marker == null)
                continue;

            float left = TimeToPixels(marker.audioTimeSeconds);
            if (visual.hit != null)
                visual.hit.style.left = TimelineLabelWidth + left - BeatMarkerHitWidth * 0.5f;
            if (visual.line != null)
                visual.line.style.left = left;
            if (visual.label != null)
            {
                visual.label.style.left = left + 6f;
                visual.label.text = marker.barNumber.ToString(CultureInfo.InvariantCulture);
            }
        }
    }

    private void UpdateNoteTimingVisuals()
    {
        if (project == null)
            return;

        MarkHighwayPreviewDirty(renderImmediately: false);
        InvalidateAuditionCache();

        foreach (ChartEditorTrackViewGroup group in BuildTrackViewGroups())
        {
            ChartEditorTrack track = group?.activeTrack;
            if (group == null || track?.notes == null || !group.Visible)
                continue;

            bool selectedTrack = group.ContainsSelected(project.selectedTrackId);
            int laneCount = selectedTrack ? GetTrackLaneCount(track) : 1;
            foreach (ChartEditorNote note in track.notes)
            {
                if (note == null || string.IsNullOrWhiteSpace(note.id))
                    continue;

                if (!currentNoteBlocks.TryGetValue(note.id, out VisualElement block) || block == null)
                    continue;

                float noteLeft = TimeToPixels(note.timeSeconds);
                float noteWidth = GetNoteDrawWidth(track, note, laneCount, selectedTrack, noteLeft);
                block.style.left = noteLeft;
                block.style.width = noteWidth;
            }
        }

        float pixelsPerSecond = GetTimelinePixelsPerSecond();
        for (int i = 0; i < currentTechniqueSegmentVisuals.Count; i++)
        {
            TechniqueSegmentVisual visual = currentTechniqueSegmentVisuals[i];
            if (visual?.box == null || visual.track == null || visual.note == null || visual.segment == null)
                continue;

            float noteLeft = TimeToPixels(visual.note.timeSeconds);
            float noteWidth = GetNoteDrawWidth(visual.track, visual.note, visual.laneCount, visual.selectedTrack, noteLeft);
            ApplyTechniqueSegmentBoxLayout(visual.box, visual.segment, noteLeft, noteWidth, pixelsPerSecond);
        }
    }

    private void AddBeatMarkerInteractionHandlers(VisualElement hit, ChartEditorBeatMarker marker)
    {
        if (hit == null || marker == null)
            return;

        bool dragging = false;
        bool moved = false;
        int pointerId = -1;
        Vector2 startPointer = Vector2.zero;
        double startAudio = 0.0;
        double minAudio = 0.0;
        double maxAudio = 0.0;
        ChartEditorBeatMarker liveAnchor = null;
        bool moveContentWithBeatMap = true;
        bool tempoProbeDrag = false;
        double tempoProbeBeatPosition = 0.0;
        double lastTempoProbeAudio = 0.0;
        SetElementCursor(hit, ChartEditorCursorKind.ResizeHorizontal);

        hit.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button == 2)
            {
                ToggleBeatMarkerAnchor(marker);
                evt.StopPropagation();
                return;
            }

            if (evt.button == 1)
            {
                SeekAndRevealTime(marker.audioTimeSeconds, syncAudio: true, rebuild: false);
                ShowBeatMarkerContextMenu(evt.position, marker);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
                return;

            if (evt.ctrlKey && marker.isAnchor)
            {
                ToggleAnchorSelection(marker);
                Rebuild();
                evt.StopImmediatePropagation();
                return;
            }

            HideContextMenu();
            dragging = true;
            moved = false;
            pointerId = evt.pointerId;
            startPointer = PointerPosition(evt);
            startAudio = marker.audioTimeSeconds;
            moveContentWithBeatMap = !evt.shiftKey;
            tempoProbeBeatPosition = marker.beatPosition;
            tempoProbeDrag = evt.ctrlKey && ChartEditorTimingService.CanUseTrailingTempoProbe(project, marker.beatPosition, marker.isAnchor);
            lastTempoProbeAudio = startAudio;
            liveAnchor = marker.isAnchor ? marker : null;
            if (liveAnchor != null)
                ResolveAnchorDragBounds(liveAnchor, out minAudio, out maxAudio);
            hit.CapturePointer(pointerId);
            evt.StopPropagation();
        });

        hit.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            Vector2 delta = PointerPosition(evt) - startPointer;
            if (!moved && Mathf.Abs(delta.x) <= 2f)
                return;

            moved = true;
            if (tempoProbeDrag)
            {
                lastTempoProbeAudio = Math.Max(0.0, Math.Min(project.DurationSeconds, startAudio + PixelDeltaToSeconds(delta.x)));
                if (ChartEditorTimingService.MoveTrailingBeatAsTempoProbe(project, tempoProbeBeatPosition, lastTempoProbeAudio, moveContentWithBeatMap))
                {
                    UpdateBeatGridVisuals();
                    UpdateNoteTimingVisuals();
                    UpdatePlaybackVisuals();
                }

                evt.StopPropagation();
                return;
            }

            if (liveAnchor == null)
            {
                liveAnchor = ChartEditorTimingService.AddAnchorAtBeat(project, marker.beatPosition, startAudio, moveContentWithBeatMap);
                if (liveAnchor == null)
                    return;

                SelectSingleAnchor(liveAnchor);
                ResolveAnchorDragBounds(liveAnchor, out minAudio, out maxAudio);
            }

            if (liveAnchor.locked)
                return;

            double newAudio = Math.Max(minAudio, Math.Min(maxAudio, startAudio + PixelDeltaToSeconds(delta.x)));
            liveAnchor.audioTimeSeconds = newAudio;
            project.dirty = true;
            ChartEditorTimingService.PreviewBeatMapChange(project, moveContentWithBeatMap);
            UpdateBeatGridVisuals();
            UpdateNoteTimingVisuals();
            UpdatePlaybackVisuals();
            evt.StopPropagation();
        });

        hit.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            dragging = false;
            if (hit.HasPointerCapture(pointerId))
                hit.ReleasePointer(pointerId);

            if (moved && tempoProbeDrag)
            {
                ChartEditorTimingService.MoveTrailingBeatAsTempoProbe(project, tempoProbeBeatPosition, lastTempoProbeAudio, moveContentWithBeatMap);
                ClearAnchorSelection();
                selectedSectionId = null;
                ClearNoteSelection();
                mode = ChartEditorMode.SyncTiming;
                evt.StopImmediatePropagation();
                hit.schedule.Execute(Rebuild);
                return;
            }

            if (moved && liveAnchor != null)
            {
                ChartEditorTimingService.MoveAnchor(project, liveAnchor, liveAnchor.audioTimeSeconds, moveContentWithBeatMap);
                SelectSingleAnchor(liveAnchor);
                evt.StopImmediatePropagation();
                hit.schedule.Execute(Rebuild);
                return;
            }

            if (marker.isAnchor)
            {
                SelectSingleAnchor(marker);
                project.cursorTimeSeconds = marker.audioTimeSeconds;
                Rebuild();
            }
            else
            {
                SeekAndRevealTime(marker.audioTimeSeconds, syncAudio: true, rebuild: false);
            }

            evt.StopPropagation();
        });
    }

    private void ShowBeatMarkerContextMenu(Vector2 worldPosition, ChartEditorBeatMarker marker)
    {
        if (marker == null)
            return;

        if (marker.isAnchor)
        {
            if (!IsAnchorSelected(marker) || GetSelectedAnchorCount() <= 1)
                SelectSingleAnchor(marker);

            if (GetSelectedAnchorCount() > 1)
            {
                ShowSelectedAnchorsContextMenu(worldPosition, marker);
                return;
            }
        }

        List<ContextMenuItem> items = new List<ContextMenuItem>
        {
            new ContextMenuItem($"Set Cursor to {FormatTime(marker.audioTimeSeconds)}", () =>
            {
                SeekAndRevealTime(marker.audioTimeSeconds, syncAudio: true, rebuild: false);
            }),
            marker.isAnchor
                ? new ContextMenuItem("Edit Anchor", () => ShowSyncPointEditPopup(marker))
                : new ContextMenuItem("Convert Beat to Anchor", () => ConvertBeatMarkerToAnchor(marker)),
            new ContextMenuItem(marker.isAnchor ? "Remove Anchor" : "Toggle Anchor", () => ToggleBeatMarkerAnchor(marker)),
            new ContextMenuItem("Move Beat to Cursor", () => MoveBeatMarkerToCursor(marker)),
            new ContextMenuItem("Set Region BPM...", () => ShowSetRegionBpmPopup(marker.beatPosition)),
            new ContextMenuItem("Set Time Signature Here...", () => ShowTimeSignaturePopup(marker.beatPosition)),
            new ContextMenuItem(project.settings.showBeatGrid ? "Hide Beat Grid" : "Show Beat Grid", () =>
            {
                project.settings.showBeatGrid = !project.settings.showBeatGrid;
                project.dirty = true;
                Rebuild();
            })
        };

        if (marker.isAnchor)
        {
            items.Add(new ContextMenuItem("Move Anchor to Cursor", () => MoveAnchorToCursor(marker)));
            items.Add(new ContextMenuItem(marker.locked ? "Locked: ON" : "Locked: OFF", () =>
            {
                marker.locked = !marker.locked;
                project.dirty = true;
                Rebuild();
            }));
        }

        ShowContextMenu(worldPosition, items.ToArray());
    }

    private void ToggleBeatMarkerAnchor(ChartEditorBeatMarker marker)
    {
        if (marker == null || project == null)
            return;

        if (marker.isAnchor)
        {
            ChartEditorTimingService.RemoveAnchor(project, marker);
            selectedSyncPointIds.Remove(marker.id);
            if (string.Equals(selectedSyncPointId, marker.id, StringComparison.OrdinalIgnoreCase))
                selectedSyncPointId = selectedSyncPointIds.FirstOrDefault();
            if (selectedSyncPointIds.Count == 0)
                ClearAnchorSelection();
            SetStatus("Anchor removed.");
        }
        else
        {
            ChartEditorBeatMarker anchor = ChartEditorTimingService.AddAnchorAtBeat(project, marker.beatPosition, marker.audioTimeSeconds);
            SelectSingleAnchor(anchor);
            SetStatus($"Anchor added at beat {marker.beatPosition:0.###}.");
        }

        selectedSectionId = null;
        ClearNoteSelection();
        mode = ChartEditorMode.SyncTiming;
        project.cursorTimeSeconds = Math.Max(0.0, Math.Min(project.DurationSeconds, marker.audioTimeSeconds));
        project.dirty = true;
        Rebuild();
    }

    private void ToggleCurrentBeatAnchor()
    {
        if (project == null)
            return;

        ChartEditorBeatMarker nearest = ChartEditorTimingService.GetNearestBeatMarker(project, project.cursorTimeSeconds);
        if (nearest == null)
        {
            SetStatus("No beat marker is available at the cursor.");
            return;
        }

        ToggleBeatMarkerAnchor(nearest);
    }

    private void ConvertBeatMarkerToAnchor(ChartEditorBeatMarker marker)
    {
        if (marker == null)
            return;

        ChartEditorBeatMarker anchor = ChartEditorTimingService.AddAnchorAtBeat(project, marker.beatPosition, marker.audioTimeSeconds);
        SelectSingleAnchor(anchor);
        selectedSectionId = null;
        ClearNoteSelection();
        mode = ChartEditorMode.SyncTiming;
        project.cursorTimeSeconds = anchor?.audioTimeSeconds ?? marker.audioTimeSeconds;
        project.dirty = true;
        Rebuild();
    }

    private void MoveBeatMarkerToCursor(ChartEditorBeatMarker marker)
    {
        if (marker == null)
            return;

        ChartEditorBeatMarker anchor = ChartEditorTimingService.MoveBeatMarkerAsAnchor(project, marker, project.cursorTimeSeconds);
        SelectSingleAnchor(anchor);
        selectedSectionId = null;
        ClearNoteSelection();
        mode = ChartEditorMode.SyncTiming;
        project.dirty = true;
        Rebuild();
    }

    private void ShowSetRegionBpmPopup(double beatPosition)
    {
        TextField bpmField = CreatePopupTextField("Region BPM", ChartEditorTimingService.GetTempoAtBeat(project, beatPosition).ToString("0.###", CultureInfo.InvariantCulture));
        ShowEditPopup("Set Region BPM", new VisualElement[] { bpmField }, () =>
        {
            if (!TryParseDoubleInRange(bpmField.value, 20.0, 360.0, out double bpm))
            {
                SetStatus("Region BPM must be between 20 and 360.");
                return false;
            }

            if (!ChartEditorTimingService.SetTempoRegionBpmAtBeat(project, beatPosition, bpm))
            {
                SetStatus("Region BPM was not changed because the next anchor is locked.");
                return false;
            }

            project.dirty = true;
            HideEditPopup();
            Rebuild();
            return true;
        });
    }

    private void ShowTimeSignaturePopup(double beatPosition)
    {
        ChartEditorTimeSignatureChange signature = ChartEditorTimingService.GetTimeSignatureAtBeat(project, beatPosition);
        TextField numeratorField = CreatePopupTextField("Numerator", Math.Max(1, signature?.numerator ?? 4).ToString(CultureInfo.InvariantCulture));
        TextField denominatorField = CreatePopupTextField("Denominator", Math.Max(1, signature?.denominator ?? 4).ToString(CultureInfo.InvariantCulture));
        ShowEditPopup("Set Time Signature", new VisualElement[] { numeratorField, denominatorField }, () =>
        {
            if (!int.TryParse((numeratorField.value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int numerator) ||
                numerator < 1 || numerator > 32)
            {
                SetStatus("Numerator must be between 1 and 32.");
                return false;
            }

            if (!int.TryParse((denominatorField.value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int denominator) ||
                denominator < 1 || denominator > 64)
            {
                SetStatus("Denominator must be between 1 and 64.");
                return false;
            }

            ChartEditorTimingService.SetTimeSignatureAtBeat(project, Math.Round(Math.Max(0.0, beatPosition)), numerator, denominator);
            project.dirty = true;
            HideEditPopup();
            Rebuild();
            return true;
        });
    }

    private void BuildSyncPoints(VisualElement timeline)
    {
        List<ChartEditorBeatMarker> anchors = ChartEditorTimingService.GetAnchors(project);
        if (anchors.Count == 0)
            return;

        float markerTop = GetSyncPointMarkerTop();
        float timelineHeight = GetTimelineContentHeight();

        foreach (ChartEditorBeatMarker point in anchors)
        {
            if (point == null)
                continue;

            float left = TimeToPixels(point.audioTimeSeconds);
            float timelineLeft = TimelineLabelWidth + left;
            bool selected = IsAnchorSelected(point);

            VisualElement guide = new VisualElement();
            guide.style.position = Position.Absolute;
            guide.style.left = timelineLeft;
            guide.style.top = markerTop + AnchorPinSize * 0.5f;
            guide.style.width = selected ? 3f : 2f;
            guide.style.height = Mathf.Max(1f, timelineHeight - (markerTop + AnchorPinSize * 0.5f) - 20f);
            guide.style.marginLeft = selected ? -1.5f : -1f;
            guide.style.backgroundColor = selected
                ? new Color(0.78f, 0.50f, 1f, 0.74f)
                : new Color(0.62f, 0.36f, 0.96f, 0.34f);
            guide.pickingMode = PickingMode.Ignore;
            timeline.Add(guide);

            VisualElement marker = new VisualElement();
            marker.pickingMode = PickingMode.Position;
            marker.style.position = Position.Absolute;
            marker.style.left = timelineLeft;
            marker.style.top = markerTop;
            marker.style.width = AnchorPinSize;
            marker.style.height = AnchorPinSize + 18f;
            marker.style.marginLeft = -AnchorPinSize * 0.5f;
            marker.style.backgroundColor = Color.clear;
            marker.style.borderTopWidth = 0f;
            marker.style.borderRightWidth = 0f;
            marker.style.borderBottomWidth = 0f;
            marker.style.borderLeftWidth = 0f;
            marker.style.paddingLeft = 0f;
            marker.style.paddingRight = 0f;
            marker.style.paddingTop = 0f;
            marker.style.paddingBottom = 0f;
            marker.style.flexDirection = FlexDirection.Column;
            marker.style.alignItems = Align.Center;
            marker.style.justifyContent = Justify.FlexStart;

            VisualElement pinHead = new VisualElement();
            pinHead.style.width = AnchorPinSize;
            pinHead.style.height = AnchorPinSize;
            pinHead.style.backgroundColor = selected ? new Color(0.66f, 0.40f, 1f, 1f) : new Color(0.075f, 0.055f, 0.105f, 1f);
            SetRadius(pinHead, 999f);
            SetBorderWidth(pinHead, selected ? 3f : 2f);
            SetBorderColor(pinHead, new Color(0.76f, 0.53f, 1f, selected ? 1f : 0.86f));

            Label pinLabel = CreateLabel("A", 18f, selected ? Color.white : new Color(0.78f, 0.56f, 1f, 1f), true, TextAnchor.MiddleCenter, false);
            pinLabel.style.position = Position.Absolute;
            pinLabel.style.left = 0f;
            pinLabel.style.right = 0f;
            pinLabel.style.top = 0f;
            pinLabel.style.bottom = 0f;
            pinLabel.pickingMode = PickingMode.Ignore;
            pinHead.Add(pinLabel);

            VisualElement pinStem = new VisualElement();
            pinStem.style.width = selected ? 4f : 3f;
            pinStem.style.height = 18f;
            pinStem.style.backgroundColor = selected ? new Color(0.78f, 0.50f, 1f, 0.94f) : new Color(0.62f, 0.36f, 0.96f, 0.72f);
            pinStem.pickingMode = PickingMode.Ignore;
            marker.Add(pinHead);
            marker.Add(pinStem);
            marker.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0 && evt.clickCount >= 2)
                {
                    SelectSingleAnchor(point);
                    ShowSyncPointEditPopup(point);
                    evt.StopPropagation();
                }
            });
            marker.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 1)
                {
                    if (evt.ctrlKey)
                        ToggleAnchorSelection(point);
                    else if (!IsAnchorSelected(point) || GetSelectedAnchorCount() <= 1)
                        SelectSingleAnchor(point);

                    if (GetSelectedAnchorCount() > 1)
                        ShowSelectedAnchorsContextMenu(evt.position, point);
                    else
                        ShowSyncPointContextMenu(evt.position, point);

                    evt.StopImmediatePropagation();
                }
            });
            AddSyncPointDragHandlers(marker, point);
            timeline.Add(marker);

            Label name = CreateLabel(FirstNonEmpty(point.label, "Anchor"), 18f, new Color(0.84f, 0.86f, 0.91f, 0.92f), false, TextAnchor.MiddleCenter, false);
            name.style.position = Position.Absolute;
            name.style.left = timelineLeft;
            name.style.top = markerTop - 28f;
            name.style.width = 148f;
            name.style.marginLeft = -74f;
            name.style.whiteSpace = WhiteSpace.NoWrap;
            name.pickingMode = PickingMode.Ignore;
            name.text = FirstNonEmpty(point.label, $"Bar {point.barNumber}");
            timeline.Add(name);
        }
    }

    private float GetSyncPointMarkerTop()
    {
        return Mathf.Max(WaveformTop + WaveformHeight + 8f, NotesTop - AnchorPinSize - 6f);
    }

    private float GetSelectedTrackRowTop()
    {
        float rowTop = NotesTop;
        foreach (ChartEditorTrackViewGroup group in BuildTrackViewGroups())
        {
            if (group == null || !group.Visible)
                continue;

            if (group.ContainsSelected(project.selectedTrackId))
                return rowTop;

            bool selectedTrack = group.ContainsSelected(project.selectedTrackId);
            rowTop += (selectedTrack ? SelectedTrackHeight : CompactTrackHeight) + TrackRowGap;
        }

        return NotesTop;
    }

    private void BuildNotes(VisualElement timeline)
    {
        List<ChartEditorTrackViewGroup> groups = BuildTrackViewGroups();
        if (groups.Count == 0)
            return;

        float rowTop = NotesTop;
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            ChartEditorTrackViewGroup group = groups[groupIndex];
            ChartEditorTrack track = group?.activeTrack;
            if (group == null || track == null || !group.Visible)
                continue;

            bool selectedTrack = group.ContainsSelected(project.selectedTrackId);
            float rowHeight = selectedTrack ? SelectedTrackHeight : CompactTrackHeight;
            Color accent = group.color;

            VisualElement rowBackground = new VisualElement();
            rowBackground.style.position = Position.Absolute;
            rowBackground.style.left = 0f;
            rowBackground.style.right = 0f;
            rowBackground.style.top = rowTop;
            rowBackground.style.height = rowHeight;
            rowBackground.style.backgroundColor = selectedTrack
                ? new Color(0.038f, 0.045f, 0.056f, 0.96f)
                : new Color(0.026f, 0.031f, 0.040f, 0.92f);
            rowBackground.style.borderTopWidth = 1f;
            rowBackground.style.borderBottomWidth = 1f;
            rowBackground.style.borderTopColor = new Color(0.12f, 0.15f, 0.20f, 1f);
            rowBackground.style.borderBottomColor = new Color(0.12f, 0.15f, 0.20f, 1f);
            rowBackground.pickingMode = PickingMode.Ignore;
            timeline.Add(rowBackground);

            if (!selectedTrack)
            {
                VisualElement rowLabel = new VisualElement();
                rowLabel.style.position = Position.Absolute;
                rowLabel.style.left = 0f;
                rowLabel.style.top = rowTop;
                rowLabel.style.width = TimelineLabelWidth;
                rowLabel.style.height = rowHeight;
                rowLabel.style.paddingLeft = 24f;
                rowLabel.style.paddingTop = 20f;
                rowLabel.style.backgroundColor = new Color(0.038f, 0.044f, 0.054f, 0.96f);
                rowLabel.style.borderLeftWidth = 0f;

                VisualElement trackHeader = new VisualElement();
                trackHeader.style.flexDirection = FlexDirection.Row;
                trackHeader.style.alignItems = Align.Center;
                trackHeader.style.marginBottom = 4f;
                VisualElement trackDot = new VisualElement();
                trackDot.style.width = 11f;
                trackDot.style.height = 11f;
                trackDot.style.marginRight = 13f;
                trackDot.style.backgroundColor = accent;
                SetRadius(trackDot, 999f);
                trackHeader.Add(trackDot);
                Label trackName = CreateLabel(FormatTrackGroupName(group).ToUpperInvariant(), 23f, accent, true, TextAnchor.MiddleLeft, false);
                trackName.style.whiteSpace = WhiteSpace.NoWrap;
                trackHeader.Add(trackName);
                Label tuning = CreateLabel(FirstNonEmpty(track.tuning?.displayName, track.role.ToString()), 22f, new Color(0.66f, 0.70f, 0.76f, 1f), false, TextAnchor.MiddleLeft, false);
                tuning.style.whiteSpace = WhiteSpace.NoWrap;
                rowLabel.Add(trackHeader);
                rowLabel.Add(tuning);
                timeline.Add(rowLabel);
            }

            VisualElement noteLayer = new VisualElement();
            noteLayer.style.position = Position.Absolute;
            noteLayer.style.left = TimelineLabelWidth;
            noteLayer.style.right = 0f;
            noteLayer.style.top = rowTop;
            noteLayer.style.height = rowHeight;
            noteLayer.style.overflow = Overflow.Hidden;
            timeline.Add(noteLayer);

            int laneCount = selectedTrack ? GetTrackLaneCount(track) : 1;
            float laneTop = selectedTrack ? SelectedTrackHeaderHeight : 0f;
            float laneHeight = (rowHeight - laneTop) / Mathf.Max(1, laneCount);
            if (selectedTrack)
                AddStringLaneLabels(timeline, track, rowTop, laneTop, laneCount, laneHeight, accent);

            VisualElement techniqueLayer = null;
            List<Rect> occupiedTechniqueLabels = null;
            if (selectedTrack)
            {
                techniqueLayer = new VisualElement();
                techniqueLayer.style.position = Position.Absolute;
                techniqueLayer.style.left = 0f;
                techniqueLayer.style.right = 0f;
                techniqueLayer.style.top = 0f;
                techniqueLayer.style.bottom = 0f;
                techniqueLayer.style.overflow = Overflow.Hidden;
                techniqueLayer.pickingMode = PickingMode.Ignore;
                occupiedTechniqueLabels = new List<Rect>();
            }

            for (int lane = 0; lane < laneCount; lane++)
            {
                Color stringColor = GetEditorLaneColor(track, lane, laneCount, accent);
                float laneLineHeight = selectedTrack ? SelectedLaneLineHeight : CompactLaneLineHeight;
                VisualElement laneLine = new VisualElement();
                laneLine.style.position = Position.Absolute;
                laneLine.style.left = 0f;
                laneLine.style.right = 0f;
                laneLine.style.top = GetLaneCenterY(laneTop, lane, laneHeight) - laneLineHeight * 0.5f;
                laneLine.style.height = laneLineHeight;
                laneLine.style.backgroundColor = selectedTrack
                    ? new Color(stringColor.r, stringColor.g, stringColor.b, 0.72f)
                    : new Color(accent.r, accent.g, accent.b, 0.45f);
                laneLine.pickingMode = PickingMode.Ignore;
                noteLayer.Add(laneLine);
            }

            foreach (ChartEditorNote note in track.notes ?? new List<ChartEditorNote>())
            {
                if (note == null)
                    continue;

                EnsureNoteDurationCoversTechniqueSegments(note);
                int lane = selectedTrack ? GetVisualLaneForNote(track, note, laneCount) : 0;
                float left = TimeToPixels(note.timeSeconds);
                float width = GetNoteDrawWidth(track, note, laneCount, selectedTrack, left);
                float noteHeight = selectedTrack ? SelectedNoteHeight : CompactNoteHeight;
                float noteTop = selectedTrack ? GetNoteTopForLane(laneTop, lane, laneHeight, noteHeight) : 44f;
                currentNoteHits.Add(new ChartEditorNoteHit
                {
                    track = track,
                    note = note,
                    rect = new Rect(TimelineLabelWidth + left, rowTop + noteTop, width, noteHeight)
                });
                VisualElement block = new VisualElement();
                block.style.position = Position.Absolute;
                block.style.left = left;
                block.style.top = noteTop;
                block.style.width = width;
                block.style.minWidth = 0f;
                block.style.height = noteHeight;
                block.style.alignItems = Align.Center;
                block.style.justifyContent = Justify.Center;
                block.style.fontSize = UiFont(26f);
                block.style.unityFontDefinition = bodyFont;
                block.style.unityTextAlign = TextAnchor.MiddleCenter;
                block.style.overflow = Overflow.Hidden;
                block.pickingMode = PickingMode.Position;
                Color noteBaseColor = selectedTrack
                    ? GetEditorNoteColor(track, note, laneCount, accent)
                    : accent;
                Color noteAccent = IsNoteSelected(note)
                    ? new Color(1f, 0.88f, 0.42f, 1f)
                    : noteBaseColor;
                block.style.backgroundColor = new Color(noteAccent.r, noteAccent.g, noteAccent.b, selectedTrack ? 0.82f : 0.42f);
                block.style.color = Color.white;
                block.style.borderTopWidth = selectedTrack ? 1f : 0f;
                block.style.borderRightWidth = selectedTrack ? 1f : 0f;
                block.style.borderBottomWidth = selectedTrack ? 1f : 0f;
                block.style.borderLeftWidth = selectedTrack ? 1f : 0f;
                block.style.borderTopColor = noteAccent;
                block.style.borderRightColor = noteAccent;
                block.style.borderBottomColor = noteAccent;
                block.style.borderLeftColor = noteAccent;
                if (!string.IsNullOrWhiteSpace(note.id))
                    currentNoteBlocks[note.id] = block;
                if (selectedTrack && track.role != ChartEditorTrackRole.Drums)
                {
                    float fretFontSize = width < 26f ? 17f : width < 42f ? 21f : 26f;
                    Label fretLabel = CreateLabel(note.fret.ToString(), fretFontSize, Color.white, true, TextAnchor.MiddleCenter, false);
                    fretLabel.pickingMode = PickingMode.Ignore;
                    block.Add(fretLabel);
                }

                if (selectedTrack)
                    AddTechniqueOverlayLabel(techniqueLayer, note, left, noteTop, width, occupiedTechniqueLabels);

                if (selectedTrack)
                    AddNoteSustainHandle(block, track, note, laneCount, selectedTrack, noteHeight, width);

                block.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button == 0 && evt.clickCount >= 2)
                    {
                        SelectSingleNote(track, note);
                        ShowNoteEditPopup(track, note);
                        evt.StopPropagation();
                    }
                });
                block.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 1)
                    {
                        if (!IsNoteSelected(note))
                            SelectSingleNote(track, note);
                        ShowNoteContextMenu(evt.position, track, note);
                        evt.StopPropagation();
                    }
                });
                AddNoteDragHandlers(block, track, note, laneCount, laneHeight, laneTop, selectedTrack, noteHeight);
                noteLayer.Add(block);
                if (selectedTrack)
                    AddTechniqueSegmentBoxes(noteLayer, block, track, note, laneCount, selectedTrack, left, noteTop, width);
            }

            if (selectedTrack && techniqueLayer != null)
                noteLayer.Add(techniqueLayer);

            rowTop += rowHeight + TrackRowGap;
        }
    }

    private void BuildCursorLine(VisualElement timeline)
    {
        float left = TimeToPixels(project.cursorTimeSeconds);
        VisualElement layer = new VisualElement();
        layer.style.position = Position.Absolute;
        layer.style.left = TimelineLabelWidth;
        layer.style.right = 0f;
        layer.style.top = 0f;
        layer.style.bottom = 0f;
        layer.pickingMode = PickingMode.Ignore;
        VisualElement cursor = new VisualElement();
        cursor.style.position = Position.Absolute;
        cursor.style.left = left;
        cursor.style.top = 0f;
        cursor.style.bottom = 0f;
        cursor.style.width = 3f;
        cursor.style.backgroundColor = new Color(0.70f, 0.46f, 1f, 0.92f);
        cursor.pickingMode = PickingMode.Ignore;
        cursorElement = cursor;
        layer.Add(cursor);
        timeline.Add(layer);

        VisualElement handle = new VisualElement();
        handle.style.position = Position.Absolute;
        handle.style.left = TimelineLabelWidth + left - 18f;
        handle.style.top = WaveformTop;
        handle.style.width = 36f;
        handle.style.height = WaveformHeight;
        handle.style.backgroundColor = Color.clear;
        handle.style.borderLeftWidth = 0f;
        handle.style.borderRightWidth = 0f;
        handle.style.borderTopWidth = 0f;
        handle.style.borderBottomWidth = 0f;
        handle.pickingMode = PickingMode.Position;
        cursorHandleElement = handle;
        AddSeekDragHandlers(handle, worldPosition => timeline.WorldToLocal(worldPosition).x - TimelineLabelWidth);
        timeline.Add(handle);
    }

    private void EnsureAudioClipRequested()
    {
        string path = project?.audio?.sourcePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ResetEditorAudioCache();
            return;
        }

        string normalizedPath = Path.GetFullPath(path);
        if (string.Equals(editorAudioPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
            return;

        editorPlaying = false;
        if (editorAudioSource != null)
            editorAudioSource.Stop();
        ResetEditorAudioCache();
        editorAudioPath = normalizedPath;
        audioLoadInProgress = true;
        audioLoadError = string.Empty;
        if (owner != null)
            owner.StartCoroutine(LoadEditorAudioClip(normalizedPath));
        else
        {
            audioLoadInProgress = false;
            audioLoadError = "Audio loader is unavailable.";
        }
    }

    private bool ShouldWaitForEditorAudioPreparation()
    {
        string path = project?.audio?.sourcePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;
        if (!string.IsNullOrWhiteSpace(audioLoadError))
            return false;

        EnsureAudioClipRequested();
        return audioLoadInProgress ||
               editorAudioClip == null ||
               waveformData == null ||
               !waveformData.IsValid;
    }

    private IEnumerator LoadEditorAudioClip(string path)
    {
        string uri = "file://" + path.Replace("\\", "/");
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN))
        {
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool failed = request.result != UnityWebRequest.Result.Success;
#else
            bool failed = request.isNetworkError || request.isHttpError;
#endif
            if (!string.Equals(editorAudioPath, path, StringComparison.OrdinalIgnoreCase))
                yield break;

            if (failed)
            {
                audioLoadError = "Could not decode audio for waveform.";
                audioLoadInProgress = false;
                OpenPendingEditorAfterAudioPreparation();
                Rebuild();
                yield break;
            }

            AudioClip loadedClip = DownloadHandlerAudioClip.GetContent(request);
            if (loadedClip == null)
            {
                audioLoadError = "Audio clip was empty.";
                audioLoadInProgress = false;
                OpenPendingEditorAfterAudioPreparation();
                Rebuild();
                yield break;
            }

            loadedClip.name = Path.GetFileNameWithoutExtension(path);
            editorAudioClip = loadedClip;
            if (project?.audio != null && editorAudioClip.length > 0.01f)
                project.audio.durationSeconds = editorAudioClip.length;

            SetStatus("Preparing waveform...");
            ChartEditorWaveformData builtWaveform = null;
            yield return ChartEditorWaveformRenderer.BuildDataAsync(editorAudioClip, data => builtWaveform = data);
            if (!string.Equals(editorAudioPath, path, StringComparison.OrdinalIgnoreCase))
                yield break;

            waveformData = builtWaveform;
            if (waveformData == null || !waveformData.IsValid)
                audioLoadError = "Could not read waveform samples.";
            ResetWaveformTextures();
            waveformCacheVersion++;

            EnsureEditorAudioSource();
            if (editorAudioSource != null)
            {
                editorAudioSource.clip = editorAudioClip;
                SyncEditorAudioToCursor(playImmediately: editorPlaying);
            }

            audioLoadInProgress = false;
            OpenPendingEditorAfterAudioPreparation();
            Rebuild();
        }
    }

    private void OpenPendingEditorAfterAudioPreparation()
    {
        if (!openEditorWhenAudioReady)
            return;

        openEditorWhenAudioReady = false;
        screen = ChartEditorScreen.Editor;
    }

    private void EnsureWaveformOverviewTexture()
    {
        if (waveformData == null || !waveformData.IsValid)
            return;

        double audioEndSeconds = GetWaveformAudioEndSeconds();
        if (audioEndSeconds <= 0.001)
            return;

        int targetWidth = ChartEditorWaveformRenderer.ResolveOverviewTextureWidth(waveformData);
        int targetHeight = ChartEditorWaveformRenderer.ResolveTextureHeight(WaveformHeight);
        if (waveformOverviewTexture != null &&
            waveformOverviewTextureWidth == targetWidth &&
            waveformOverviewTextureHeight == targetHeight)
        {
            return;
        }

        if (waveformOverviewTextureBuildInProgress)
            return;
        if (owner != null)
        {
            owner.StartCoroutine(BuildWaveformOverviewTextureAsync(waveformCacheVersion, rebuildWhenDone: screen == ChartEditorScreen.Editor));
            return;
        }

        ResetWaveformOverviewTexture();
        waveformOverviewTexture = ChartEditorWaveformRenderer.RenderTexture(
            waveformData,
            0.0,
            audioEndSeconds,
            targetWidth,
            WaveformHeight);
        if (waveformOverviewTexture != null)
            waveformOverviewTexture.filterMode = FilterMode.Bilinear;
        waveformOverviewTextureWidth = waveformOverviewTexture != null ? targetWidth : 0;
        waveformOverviewTextureHeight = waveformOverviewTexture != null ? targetHeight : 0;
    }

    private IEnumerator BuildWaveformOverviewTextureAsync(int cacheVersion, bool rebuildWhenDone = false)
    {
        if (waveformOverviewTextureBuildInProgress || waveformData == null || !waveformData.IsValid)
            yield break;

        double audioEndSeconds = GetWaveformAudioEndSeconds();
        if (audioEndSeconds <= 0.001)
            yield break;

        int targetWidth = ChartEditorWaveformRenderer.ResolveOverviewTextureWidth(waveformData);
        int targetHeight = ChartEditorWaveformRenderer.ResolveTextureHeight(WaveformHeight);
        if (waveformOverviewTexture != null &&
            waveformOverviewTextureWidth == targetWidth &&
            waveformOverviewTextureHeight == targetHeight)
        {
            yield break;
        }

        waveformOverviewTextureBuildInProgress = true;
        Texture2D builtTexture = null;
        yield return ChartEditorWaveformRenderer.RenderTextureAsync(
            waveformData,
            0.0,
            audioEndSeconds,
            targetWidth,
            WaveformHeight,
            texture => builtTexture = texture,
            () => cacheVersion != waveformCacheVersion || waveformData == null || !waveformData.IsValid);
        waveformOverviewTextureBuildInProgress = false;

        if (cacheVersion != waveformCacheVersion || waveformData == null || !waveformData.IsValid)
        {
            if (builtTexture != null)
                UnityEngine.Object.Destroy(builtTexture);
            yield break;
        }

        if (builtTexture == null)
            yield break;

        ResetWaveformOverviewTexture();
        waveformOverviewTexture = builtTexture;
        waveformOverviewTexture.filterMode = FilterMode.Bilinear;
        waveformOverviewTextureWidth = targetWidth;
        waveformOverviewTextureHeight = targetHeight;

        if (rebuildWhenDone && visible && screen == ChartEditorScreen.Editor)
            Rebuild();
    }

    private void EnsureWaveformTexture()
    {
        if (waveformData == null || !waveformData.IsValid)
            return;
        if (editorPlaying)
            return;

        if (IsWaveformTextureUsableForCurrentView())
        {
            UpdateWaveformTextureTimelinePlacement();
            return;
        }

        WaveformRenderRange range = CalculateWaveformRenderRange();
        if (!range.IsValid)
            return;

        int targetWidth = ChartEditorWaveformRenderer.ResolveTextureWidth(range.width);
        int targetHeight = ChartEditorWaveformRenderer.ResolveTextureHeight(WaveformHeight);
        if (IsWaveformTextureCacheMatch(range, targetWidth, targetHeight))
        {
            ApplyWaveformTextureRange(range);
            return;
        }

        if (waveformTextureBuildInProgress)
            return;
        if (owner != null)
        {
            owner.StartCoroutine(BuildWaveformTextureAsync(waveformCacheVersion, rebuildWhenDone: screen == ChartEditorScreen.Editor));
            return;
        }

        ResetWaveformTexture();
        waveformTexture = ChartEditorWaveformRenderer.RenderTexture(
            waveformData,
            range.startSeconds,
            range.endSeconds,
            targetWidth,
            WaveformHeight);
        waveformTextureWidth = waveformTexture != null ? targetWidth : 0;
        waveformTextureHeight = waveformTexture != null ? targetHeight : 0;
        ApplyWaveformTextureRange(range);
        waveformTextureStartSeconds = waveformTexture != null ? range.startSeconds : -1.0;
        waveformTextureEndSeconds = waveformTexture != null ? range.endSeconds : -1.0;
    }

    private IEnumerator BuildWaveformTextureAsync(int cacheVersion, bool rebuildWhenDone)
    {
        if (waveformTextureBuildInProgress || waveformData == null || !waveformData.IsValid || editorPlaying)
            yield break;

        WaveformRenderRange range = CalculateWaveformRenderRange();
        if (!range.IsValid)
            yield break;

        int targetWidth = ChartEditorWaveformRenderer.ResolveTextureWidth(range.width);
        int targetHeight = ChartEditorWaveformRenderer.ResolveTextureHeight(WaveformHeight);
        if (IsWaveformTextureCacheMatch(range, targetWidth, targetHeight))
        {
            ApplyWaveformTextureRange(range);
            yield break;
        }

        waveformTextureBuildInProgress = true;
        Texture2D builtTexture = null;
        yield return ChartEditorWaveformRenderer.RenderTextureAsync(
            waveformData,
            range.startSeconds,
            range.endSeconds,
            targetWidth,
            WaveformHeight,
            texture => builtTexture = texture,
            () => cacheVersion != waveformCacheVersion || waveformData == null || !waveformData.IsValid || editorPlaying);
        waveformTextureBuildInProgress = false;

        if (cacheVersion != waveformCacheVersion || waveformData == null || !waveformData.IsValid || editorPlaying)
        {
            if (builtTexture != null)
                UnityEngine.Object.Destroy(builtTexture);
            yield break;
        }

        if (builtTexture == null)
            yield break;

        ResetWaveformTexture();
        waveformTexture = builtTexture;
        waveformTextureWidth = waveformTexture != null ? targetWidth : 0;
        waveformTextureHeight = waveformTexture != null ? targetHeight : 0;
        ApplyWaveformTextureRange(range);
        waveformTextureStartSeconds = waveformTexture != null ? range.startSeconds : -1.0;
        waveformTextureEndSeconds = waveformTexture != null ? range.endSeconds : -1.0;

        if (rebuildWhenDone && visible && screen == ChartEditorScreen.Editor)
            Rebuild();
    }

    private bool IsWaveformTextureUsableForCurrentView()
    {
        if (waveformTexture == null ||
            waveformTextureWidth <= 0 ||
            waveformTextureHeight <= 0 ||
            waveformTextureStartSeconds < 0.0 ||
            waveformTextureEndSeconds <= waveformTextureStartSeconds)
        {
            return false;
        }

        UpdateWaveformTextureTimelinePlacement();
        if (!TryGetVisibleWaveformPixelRange(out float visibleLeft, out float visibleRight))
            return true;

        float renderedWidth = Mathf.Max(1f, waveformTextureTimelineWidth);
        float pixelsPerLayoutPixel = waveformTextureWidth / renderedWidth;
        if (pixelsPerLayoutPixel < WaveformTextureMinimumPixelsPerLayoutPixel)
            return false;

        double audioEndSeconds = GetWaveformAudioEndSeconds();
        double visibleStart = Math.Max(0.0, PixelsToSeconds(visibleLeft));
        double visibleEnd = Math.Min(audioEndSeconds, PixelsToSeconds(visibleRight));
        if (visibleEnd <= visibleStart)
            return true;

        double visibleDuration = Math.Max(0.001, visibleEnd - visibleStart);
        double prefetchSeconds = Math.Max(0.025, visibleDuration * 0.25);
        const double epsilon = 0.001;

        if (visibleStart < waveformTextureStartSeconds - epsilon || visibleEnd > waveformTextureEndSeconds + epsilon)
            return false;
        if (waveformTextureStartSeconds > epsilon && visibleStart < waveformTextureStartSeconds + prefetchSeconds)
            return false;
        if (waveformTextureEndSeconds < audioEndSeconds - epsilon && visibleEnd > waveformTextureEndSeconds - prefetchSeconds)
            return false;

        return true;
    }

    private bool IsWaveformTextureCacheMatch(WaveformRenderRange range, int targetWidth, int targetHeight)
    {
        return waveformTexture != null &&
               waveformTextureWidth == targetWidth &&
               waveformTextureHeight == targetHeight &&
               Math.Abs(waveformTextureStartSeconds - range.startSeconds) < 0.001 &&
               Math.Abs(waveformTextureEndSeconds - range.endSeconds) < 0.001;
    }

    private double GetWaveformAudioEndSeconds()
    {
        if (project == null || waveformData == null || !waveformData.IsValid)
            return 0.0;

        return Math.Min(Math.Max(0.001, project.DurationSeconds), Math.Max(0.001, waveformData.durationSeconds));
    }

    private void GetVisibleWaveformPixelRange(float waveformWidth, out float visibleLeft, out float visibleRight)
    {
        if (TryGetVisibleWaveformPixelRange(out visibleLeft, out visibleRight))
            return;

        float viewportWidth = GetTimelineViewportWidth();
        if (viewportWidth <= 1f)
            viewportWidth = Mathf.Min(1600f, Mathf.Max(1f, waveformWidth));

        visibleLeft = Mathf.Clamp(timelineScrollOffset.x - TimelineLabelWidth, 0f, Mathf.Max(0f, waveformWidth));
        visibleRight = Mathf.Clamp(visibleLeft + viewportWidth, visibleLeft, Mathf.Max(visibleLeft + 1f, waveformWidth));
    }

    private bool TryGetVisibleWaveformPixelRange(out float visibleLeft, out float visibleRight)
    {
        visibleLeft = 0f;
        visibleRight = 0f;
        double audioEndSeconds = GetWaveformAudioEndSeconds();
        if (audioEndSeconds <= 0.001)
            return false;

        float viewportWidth = GetTimelineViewportWidth();
        if (viewportWidth <= 1f)
            return false;

        float audioWidth = Mathf.Max(1f, TimeToPixels(audioEndSeconds));
        float rawLeft = timelineScrollOffset.x - TimelineLabelWidth;
        float rawRight = rawLeft + viewportWidth;
        visibleLeft = Mathf.Clamp(rawLeft, 0f, audioWidth);
        visibleRight = Mathf.Clamp(rawRight, 0f, audioWidth);

        if (visibleRight <= visibleLeft)
        {
            if (rawLeft >= audioWidth)
            {
                visibleRight = audioWidth;
                visibleLeft = Mathf.Max(0f, audioWidth - Mathf.Min(viewportWidth, audioWidth));
            }
            else
            {
                visibleLeft = 0f;
                visibleRight = Mathf.Min(audioWidth, Mathf.Max(1f, viewportWidth));
            }
        }

        return visibleRight > visibleLeft;
    }

    private WaveformRenderRange CalculateWaveformRenderRange()
    {
        WaveformRenderRange range = new WaveformRenderRange();
        if (project == null || waveformData == null || !waveformData.IsValid || project.DurationSeconds <= 0.001)
            return range;

        double audioEndSeconds = GetWaveformAudioEndSeconds();
        if (audioEndSeconds <= 0.001)
            return range;

        float audioWidth = Mathf.Max(1f, TimeToPixels(audioEndSeconds));
        if (!TryGetVisibleWaveformPixelRange(out float visibleLeft, out float visibleRight))
        {
            range.left = 0f;
            range.width = audioWidth;
            range.startSeconds = 0.0;
            range.endSeconds = audioEndSeconds;
            return range;
        }

        float visibleWidth = Mathf.Max(1f, visibleRight - visibleLeft);
        float padding = Mathf.Max(WaveformRenderMinimumPadding, GetTimelineViewportWidth() * WaveformRenderViewportPaddingMultiplier);
        float maxRangeWidth = Mathf.Max(visibleWidth, ChartEditorWaveformRenderer.ResolveMaximumTextureWidth());
        if (visibleWidth + padding * 2f > maxRangeWidth)
            padding = Mathf.Max(0f, (maxRangeWidth - visibleWidth) * 0.5f);

        float left = Mathf.Max(0f, visibleLeft - padding);
        float right = Mathf.Min(audioWidth, visibleRight + padding);
        if (right <= left)
        {
            left = Mathf.Max(0f, visibleLeft);
            right = Mathf.Min(audioWidth, Mathf.Max(left + 1f, visibleRight));
        }

        range.left = left;
        range.width = Mathf.Max(1f, right - left);
        range.startSeconds = Math.Max(0.0, PixelsToSeconds(left));
        range.endSeconds = Math.Min(audioEndSeconds, Math.Max(range.startSeconds + 0.001, PixelsToSeconds(right)));
        return range;
    }

    private void UpdateWaveformTextureTimelinePlacement()
    {
        if (project != null &&
            waveformTextureStartSeconds >= 0.0 &&
            waveformTextureEndSeconds > waveformTextureStartSeconds)
        {
            float left = TimeToPixels(waveformTextureStartSeconds);
            float right = TimeToPixels(waveformTextureEndSeconds);
            waveformTextureTimelineLeft = Mathf.Max(0f, left);
            waveformTextureTimelineWidth = Mathf.Max(1f, right - left);
            return;
        }

        waveformTextureTimelineLeft = 0f;
        waveformTextureTimelineWidth = 1f;
    }

    private void ApplyWaveformTextureRange(WaveformRenderRange range)
    {
        waveformTextureTimelineLeft = Mathf.Max(0f, range.left);
        waveformTextureTimelineWidth = Mathf.Max(1f, range.width);
    }

    private void RequestWaveformRefresh()
    {
        if (waveformRefreshScheduled || waveformData == null || !visible || screen != ChartEditorScreen.Editor || RootElement == null)
            return;

        waveformRefreshScheduled = true;
        RootElement.schedule.Execute(() =>
        {
            waveformRefreshScheduled = false;
            if (!visible || screen != ChartEditorScreen.Editor || waveformData == null)
                return;
            RefreshWaveformTextureElement();
        }).StartingIn(30);
    }

    private void MarkWaveformDirty()
    {
        UpdateWaveformVectorElementLayout();
    }

    private void UpdateWaveformVectorElementLayout()
    {
        if (waveformVectorElement == null || waveformData == null || !waveformData.IsValid)
            return;

        float waveformWidth = Mathf.Max(1f, TimeToPixels(GetWaveformAudioEndSeconds()));
        GetVisibleWaveformPixelRange(waveformWidth, out float visibleLeft, out float visibleRight);
        waveformVectorElement.SetViewport(visibleLeft, Mathf.Max(1f, visibleRight - visibleLeft));
    }

    private bool ShouldRefreshWaveformForScroll()
    {
        return false;
    }

    private void RefreshWaveformTextureElement()
    {
        if (waveformTextureElement == null || waveformTextureElement.parent == null)
            return;

        if (waveformTexture == null || !IsWaveformTextureUsableForCurrentView())
            EnsureWaveformTexture();
        if (waveformTexture == null)
            return;

        UpdateWaveformTextureTimelinePlacement();
        waveformTextureElement.style.left = waveformTextureTimelineLeft;
        waveformTextureElement.style.width = waveformTextureTimelineWidth;
        waveformTextureElement.style.height = WaveformHeight;
        waveformTextureElement.style.backgroundImage = new StyleBackground(waveformTexture);
    }

    private void ResetWaveformTexture()
    {
        if (waveformTexture != null)
            UnityEngine.Object.Destroy(waveformTexture);
        waveformTexture = null;
        waveformTextureElement = null;
        waveformTextureWidth = 0;
        waveformTextureHeight = 0;
        waveformTextureTimelineLeft = 0f;
        waveformTextureTimelineWidth = 0f;
        waveformTextureStartSeconds = -1.0;
        waveformTextureEndSeconds = -1.0;
    }

    private void ResetWaveformOverviewTexture()
    {
        if (waveformOverviewTexture != null)
            UnityEngine.Object.Destroy(waveformOverviewTexture);
        waveformOverviewTexture = null;
        waveformOverviewTextureWidth = 0;
        waveformOverviewTextureHeight = 0;
    }

    private void ResetWaveformTextures()
    {
        ResetWaveformTexture();
        ResetWaveformOverviewTexture();
    }

    private void ResetEditorAudioCache()
    {
        audioLoadInProgress = false;
        audioLoadError = string.Empty;
        editorAudioPath = string.Empty;
        waveformData = null;
        waveformOverviewTextureBuildInProgress = false;
        waveformTextureBuildInProgress = false;
        waveformCacheVersion++;
        ResetWaveformTextures();
        if (editorAudioSource != null)
        {
            editorAudioSource.Stop();
            editorAudioSource.clip = null;
        }

        editorAudioClip = null;
        editorPlaying = false;
    }

    private void EnsureEditorAudioSource()
    {
        if (editorAudioSource != null)
            return;

        GameObject sourceObject = new GameObject("ChartEditorAudioSource");
        if (owner != null)
            sourceObject.transform.SetParent(owner.transform, false);
        sourceObject.hideFlags = HideFlags.HideAndDontSave;
        editorAudioSource = sourceObject.AddComponent<AudioSource>();
        editorAudioSource.playOnAwake = false;
        editorAudioSource.loop = false;
        editorAudioSource.volume = 1f;
        editorAudioSource.pitch = GetPlaybackSpeed();
    }

    private float GetPlaybackSpeed()
    {
        float speed = project?.settings != null ? project.settings.playbackSpeed : 1f;
        return Mathf.Clamp(speed <= 0.001f ? 1f : speed, MinPlaybackSpeed, MaxPlaybackSpeed);
    }

    private void SetPlaybackSpeedPercent(float percent)
    {
        float speed = Mathf.Clamp(percent / 100f, MinPlaybackSpeed, MaxPlaybackSpeed);
        if (project?.settings != null)
        {
            project.settings.playbackSpeed = speed;
            project.dirty = true;
        }

        if (editorAudioSource != null)
            editorAudioSource.pitch = speed;

        UpdatePlaybackSpeedControl();
    }

    private void UpdatePlaybackSpeedControl()
    {
        float speed = GetPlaybackSpeed();
        float percent = speed * 100f;
        if (playbackSpeedLabel != null)
            playbackSpeedLabel.text = $"{Mathf.RoundToInt(percent)}%";
        playbackSpeedSlider?.SetValueWithoutNotify(percent);
        if (editorAudioSource != null)
            editorAudioSource.pitch = speed;
    }

    private AudioClip GetMetronomeClickClip()
    {
        return metronomeClickClip ??= CreateToneClip("ChartEditorMetronomeClick", 1320f, 0.045f, 0.55f);
    }

    private AudioClip GetMetronomeDownbeatClip()
    {
        return metronomeDownbeatClip ??= CreateToneClip("ChartEditorMetronomeDownbeat", 1880f, 0.055f, 0.75f);
    }

    private AudioClip GetNoteClapClip()
    {
        return noteClapClip ??= CreateNoiseClickClip("ChartEditorNoteClap", 0.035f, 0.70f);
    }

    private static AudioClip CreateToneClip(string name, float frequency, float durationSeconds, float gain)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = 1f - (i / (float)sampleCount);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * gain;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateNoiseClickClip(string name, float durationSeconds, float gain)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
        float[] samples = new float[sampleCount];
        uint seed = 2166136261u;
        for (int i = 0; i < sampleCount; i++)
        {
            seed ^= (uint)(i + 1);
            seed *= 16777619u;
            float noise = ((seed & 0xffff) / 32767.5f) - 1f;
            float envelope = 1f - (i / (float)sampleCount);
            samples[i] = noise * envelope * gain;
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void TogglePlayback()
    {
        if (project == null)
            return;

        if (editorPlaying)
        {
            PausePlayback();
            return;
        }

        editorPlaying = true;
        waveformCacheVersion++;
        silentPlaybackTimeSeconds = project.cursorTimeSeconds;
        lastAuditionTimeSeconds = project.cursorTimeSeconds;
        InvalidateAuditionCache();
        EnsureAudioClipRequested();
        SyncEditorAudioToCursor(playImmediately: true);
        UpdatePlaybackVisuals();
        SetStatus(editorAudioClip == null && audioLoadInProgress ? "Loading audio..." : "Playback started.");
    }

    private void PausePlayback()
    {
        editorPlaying = false;
        if (editorAudioSource != null && editorAudioSource.isPlaying)
            editorAudioSource.Pause();
        lastAuditionTimeSeconds = -1.0;
        InvalidateAuditionCache();
        UpdatePlaybackVisuals();
        MarkWaveformDirty();
        SetStatus("Playback paused.");
    }

    private void StopPlayback()
    {
        editorPlaying = false;
        if (editorAudioSource != null)
            editorAudioSource.Stop();
        if (project != null)
            SetCursorTime(0.0, rebuild: false, syncAudio: false);
        lastAuditionTimeSeconds = -1.0;
        InvalidateAuditionCache();
        UpdatePlaybackVisuals();
        MarkWaveformDirty();
    }

    private void SeekTransportTo(double seconds)
    {
        SeekAndRevealTime(seconds, syncAudio: true, rebuild: false);
    }

    private void AdvancePlayback(float deltaTime)
    {
        if (!editorPlaying || project == null)
            return;
        if (seekDragging)
            return;

        double nextTime;
        if (editorAudioSource != null && editorAudioSource.clip != null)
        {
            nextTime = editorAudioSource.isPlaying
                ? editorAudioSource.time
                : project.cursorTimeSeconds + deltaTime;
        }
        else
        {
            silentPlaybackTimeSeconds += deltaTime * GetPlaybackSpeed();
            nextTime = silentPlaybackTimeSeconds;
        }

        double previousTime = project.cursorTimeSeconds;
        if (lastAuditionTimeSeconds >= 0.0)
            previousTime = lastAuditionTimeSeconds;

        if (nextTime >= previousTime)
            PlayBeatMapAudition(previousTime, nextTime);
        lastAuditionTimeSeconds = nextTime;

        if (nextTime >= project.DurationSeconds - 0.0001)
        {
            editorPlaying = false;
            if (editorAudioSource != null)
                editorAudioSource.Stop();
            lastAuditionTimeSeconds = -1.0;
            InvalidateAuditionCache();
            SetCursorTime(project.DurationSeconds, rebuild: false, syncAudio: false);
        }
        else
        {
            SetCursorTime(nextTime, rebuild: false, syncAudio: false);
        }
    }

    private void SyncEditorAudioToCursor(bool playImmediately)
    {
        if (project == null)
            return;

        silentPlaybackTimeSeconds = project.cursorTimeSeconds;
        lastAuditionTimeSeconds = project.cursorTimeSeconds;
        InvalidateAuditionCache();
        EnsureEditorAudioSource();
        if (editorAudioSource == null || editorAudioClip == null)
            return;

        editorAudioSource.clip = editorAudioClip;
        editorAudioSource.pitch = GetPlaybackSpeed();
        editorAudioSource.time = Mathf.Clamp((float)project.cursorTimeSeconds, 0f, Mathf.Max(0.01f, editorAudioClip.length - 0.01f));
        if (playImmediately)
            editorAudioSource.Play();
        else if (editorAudioSource.isPlaying)
            editorAudioSource.Pause();
    }

    private void SetCursorTime(double seconds, bool rebuild, bool syncAudio)
    {
        if (project == null)
            return;

        project.cursorTimeSeconds = Math.Max(0.0, Math.Min(project.DurationSeconds, seconds));
        if (syncAudio)
            lastAuditionTimeSeconds = project.cursorTimeSeconds;
        if (syncAudio)
            SyncEditorAudioToCursor(playImmediately: editorPlaying);
        UpdatePlaybackVisuals();
        if (editorPlaying && !seekDragging)
            FollowTimelineTime(project.cursorTimeSeconds);
        if (rebuild)
            Rebuild();
    }

    private void SeekAndRevealTime(double seconds, bool syncAudio, bool rebuild)
    {
        double target = Math.Max(0.0, Math.Min(project?.DurationSeconds ?? seconds, seconds));
        SetCursorTime(target, rebuild: false, syncAudio: syncAudio);
        ScrollTimelineToTime(project?.cursorTimeSeconds ?? target, 0.28f);
        if (rebuild)
        {
            Rebuild();
            RootElement.schedule.Execute(() =>
            {
                if (project == null)
                    return;

                SetCursorTime(target, rebuild: false, syncAudio: syncAudio);
                ScrollTimelineToTime(target, 0.28f);
                UpdatePlaybackVisuals();
            });
        }
    }

    private void FollowTimelineTime(double seconds)
    {
        if (currentTimelineScrollView == null || project == null)
            return;

        float viewportWidth = GetTimelineViewportWidth();
        if (viewportWidth <= 1f)
            return;

        float contentX = TimelineLabelWidth + TimeToPixels(seconds);
        float visibleStart = timelineScrollOffset.x;
        float visibleEnd = visibleStart + viewportWidth;
        float desiredX = timelineScrollOffset.x;

        if (contentX > visibleEnd - FollowPlayheadLeadingMargin)
            desiredX = contentX - viewportWidth + FollowPlayheadLeadingMargin;
        else if (contentX < visibleStart + FollowPlayheadTrailingMargin)
            desiredX = contentX - FollowPlayheadTrailingMargin;
        else
            return;

        ApplyTimelineScrollX(desiredX);
    }

    private void ScrollTimelineToTime(double seconds, float viewportAnchor01)
    {
        float viewportWidth = GetTimelineViewportWidth();
        if (viewportWidth <= 1f)
            viewportWidth = 1600f;

        float contentX = TimelineLabelWidth + TimeToPixels(seconds);
        ApplyTimelineScrollX(contentX - viewportWidth * Mathf.Clamp01(viewportAnchor01));
    }

    private float GetTimelineViewportWidth()
    {
        if (currentTimelineScrollView?.contentViewport != null && currentTimelineScrollView.contentViewport.layout.width > 1f)
            return currentTimelineScrollView.contentViewport.layout.width;
        return timelineViewportWidth > 1f ? timelineViewportWidth : 0f;
    }

    private void ApplyTimelineScrollX(float x)
    {
        float viewportWidth = GetTimelineViewportWidth();
        if (viewportWidth <= 1f)
            viewportWidth = 1600f;

        float maxScroll = Mathf.Max(0f, GetTimelineContentWidth() - viewportWidth);
        timelineScrollOffset.x = Mathf.Clamp(x, 0f, maxScroll);
        timelineScrollInitialized = true;
        skipTimelineScrollCaptureOnce = true;
        if (currentTimelineScrollView != null)
        {
            currentTimelineScrollView.scrollOffset = timelineScrollOffset;
            RefreshIosScrollIndicators(currentTimelineScrollView);
        }
        MarkWaveformDirty();
    }

    private void PlayBeatMapAudition(double fromSeconds, double toSeconds)
    {
        if (project == null || toSeconds <= fromSeconds)
            return;
        if (project.settings == null || (!project.settings.metronomeEnabled && !project.settings.noteClapsEnabled))
            return;

        EnsureEditorAudioSource();
        if (editorAudioSource == null)
            return;

        EnsureAuditionCache(fromSeconds);
        double maxLookAhead = Math.Min(toSeconds, fromSeconds + 0.25);
        if (project.settings.metronomeEnabled)
        {
            while (auditionBeatIndex < auditionBeatMarkers.Count)
            {
                ChartEditorBeatMarker marker = auditionBeatMarkers[auditionBeatIndex];
                if (marker == null || marker.audioTimeSeconds <= fromSeconds)
                {
                    auditionBeatIndex++;
                    continue;
                }

                if (marker.audioTimeSeconds > maxLookAhead)
                    break;

                editorAudioSource.PlayOneShot(marker.isDownbeat ? GetMetronomeDownbeatClip() : GetMetronomeClickClip(), marker.isDownbeat ? 0.65f : 0.45f);
                auditionBeatIndex++;
            }
        }

        if (project.settings.noteClapsEnabled)
        {
            while (auditionNoteIndex < auditionNotes.Count)
            {
                ChartEditorNote note = auditionNotes[auditionNoteIndex];
                if (note == null || note.timeSeconds <= fromSeconds)
                {
                    auditionNoteIndex++;
                    continue;
                }

                if (note.timeSeconds > maxLookAhead)
                    break;

                editorAudioSource.PlayOneShot(GetNoteClapClip(), 0.52f);
                auditionNoteIndex++;
            }
        }

        auditionCursorAnchorSeconds = fromSeconds;
    }

    private void EnsureAuditionCache(double fromSeconds)
    {
        string trackId = project?.SelectedTrack?.id ?? string.Empty;
        if (auditionCacheValid &&
            string.Equals(auditionTrackId ?? string.Empty, trackId, StringComparison.OrdinalIgnoreCase) &&
            fromSeconds + 0.001 >= auditionCursorAnchorSeconds)
        {
            return;
        }

        auditionBeatMarkers.Clear();
        auditionNotes.Clear();
        auditionTrackId = trackId;

        if (project?.settings?.metronomeEnabled == true)
            auditionBeatMarkers.AddRange(ChartEditorTimingService.GetBeatMarkers(project));

        if (project?.settings?.noteClapsEnabled == true && project.SelectedTrack?.notes != null)
        {
            auditionNotes.AddRange(project.SelectedTrack.notes
                .Where(note => note != null)
                .OrderBy(note => note.timeSeconds)
                .ThenBy(note => note.stringOrLane));
        }

        auditionCacheValid = true;
        PositionAuditionCursors(fromSeconds);
    }

    private void PositionAuditionCursors(double fromSeconds)
    {
        auditionBeatIndex = 0;
        while (auditionBeatIndex < auditionBeatMarkers.Count &&
               (auditionBeatMarkers[auditionBeatIndex] == null || auditionBeatMarkers[auditionBeatIndex].audioTimeSeconds <= fromSeconds))
        {
            auditionBeatIndex++;
        }

        auditionNoteIndex = 0;
        while (auditionNoteIndex < auditionNotes.Count &&
               (auditionNotes[auditionNoteIndex] == null || auditionNotes[auditionNoteIndex].timeSeconds <= fromSeconds))
        {
            auditionNoteIndex++;
        }

        auditionCursorAnchorSeconds = fromSeconds;
    }

    private void InvalidateAuditionCache()
    {
        auditionCacheValid = false;
        auditionBeatMarkers.Clear();
        auditionNotes.Clear();
        auditionBeatIndex = 0;
        auditionNoteIndex = 0;
        auditionTrackId = null;
        auditionCursorAnchorSeconds = -1.0;
    }

    private void UpdatePlaybackVisuals()
    {
        if (project != null)
            headerTimeLabel.text = $"{FormatTime(project.cursorTimeSeconds)} / {FormatTime(project.DurationSeconds)}";
        UpdateHeaderProgress();
        if (transportPlayButton != null)
            transportPlayButton.text = editorPlaying ? "Ⅱ" : "▶";
        if (cursorElement != null && project != null)
            cursorElement.style.left = TimeToPixels(project.cursorTimeSeconds);
        if (cursorHandleElement != null && project != null)
            cursorHandleElement.style.left = TimelineLabelWidth + TimeToPixels(project.cursorTimeSeconds) - 18f;
    }

    private void UpdateHeaderProgress()
    {
        if (headerProgressFill == null)
            return;

        float progress = project == null || project.DurationSeconds <= 0.0001
            ? 0f
            : Mathf.Clamp01((float)(project.cursorTimeSeconds / project.DurationSeconds));
        headerProgressFill.style.width = Length.Percent(progress * 100f);
    }

    private void HandleKeyboardShortcuts()
    {
        if (project == null || IsTextFieldFocused())
        {
            ResetArrowRepeat();
            return;
        }

        bool controlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (controlHeld && shiftHeld)
                SetPlaybackSpeedPercent(25f);
            else if (controlHeld)
                SetPlaybackSpeedPercent(50f);
            else if (Input.GetKey(KeyCode.D))
                SetPlaybackSpeedPercent(100f);

            TogglePlayback();
            ResetArrowRepeat();
            return;
        }

        if (controlHeld && Input.GetKeyDown(KeyCode.C))
        {
            CopySelectedNotes();
            ResetArrowRepeat();
            return;
        }

        if (controlHeld && Input.GetKeyDown(KeyCode.V))
        {
            PasteCopiedNotesNextToSelection();
            ResetArrowRepeat();
            return;
        }

        if (!controlHeld && Input.GetKeyDown(KeyCode.A))
        {
            ToggleCurrentBeatAnchor();
            ResetArrowRepeat();
            return;
        }

        if (!controlHeld && Input.GetKeyDown(KeyCode.M))
        {
            ToggleMetronome();
            ResetArrowRepeat();
            return;
        }

        if (!controlHeld && Input.GetKeyDown(KeyCode.K))
        {
            ToggleNoteClaps();
            ResetArrowRepeat();
            return;
        }

        if (HandlePageNavigation(controlHeld, shiftHeld, altHeld) ||
            HandleTempoShortcut(controlHeld, shiftHeld) ||
            HandleSnapShortcut() ||
            HandleBracketNoteMoveShortcut(controlHeld))
        {
            ResetArrowRepeat();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Delete))
        {
            if (mode == ChartEditorMode.Notes && GetSelectedNoteReferences().Count > 1)
            {
                DeleteSelectedNotes();
                return;
            }

            ChartEditorNote selectedNoteForDelete = mode == ChartEditorMode.Notes ? FindSelectedNote() : null;
            if (selectedNoteForDelete != null)
            {
                DeleteNote(selectedNoteForDelete);
                return;
            }
        }

        bool handledArrow =
            HandleArrowRepeat(KeyCode.LeftArrow, () => ApplyKeyboardHorizontalNudge(-1)) ||
            HandleArrowRepeat(KeyCode.RightArrow, () => ApplyKeyboardHorizontalNudge(1)) ||
            HandleArrowRepeat(KeyCode.UpArrow, () => ApplyKeyboardVerticalNudge(1)) ||
            HandleArrowRepeat(KeyCode.DownArrow, () => ApplyKeyboardVerticalNudge(-1));

        if (!handledArrow && !IsAnyArrowKeyHeld())
            ResetArrowRepeat();
    }

    private bool HandleArrowRepeat(KeyCode key, Action action)
    {
        if (Input.GetKeyDown(key))
        {
            repeatingArrowKey = key;
            nextArrowRepeatTime = Time.unscaledTime + ArrowRepeatInitialDelay;
            action?.Invoke();
            return true;
        }

        if (!Input.GetKey(key) || repeatingArrowKey != key)
            return false;

        if (Time.unscaledTime >= nextArrowRepeatTime)
        {
            nextArrowRepeatTime = Time.unscaledTime + ArrowRepeatInterval;
            action?.Invoke();
        }

        return true;
    }

    private static bool IsAnyArrowKeyHeld()
    {
        return Input.GetKey(KeyCode.LeftArrow) ||
               Input.GetKey(KeyCode.RightArrow) ||
               Input.GetKey(KeyCode.UpArrow) ||
               Input.GetKey(KeyCode.DownArrow);
    }

    private void ResetArrowRepeat()
    {
        repeatingArrowKey = KeyCode.None;
        nextArrowRepeatTime = 0f;
    }

    private void ApplyKeyboardHorizontalNudge(int direction)
    {
        bool controlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (controlHeld)
        {
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            double cursorStep = shiftHeld ? GetSnapStepSeconds() : 0.001;
            SetCursorTimeFromKeyboard(project.cursorTimeSeconds + cursorStep * direction);
            FollowTimelineTime(project.cursorTimeSeconds);
            return;
        }

        double nudge = GetKeyboardNudgeSeconds() * direction;
        if (mode == ChartEditorMode.Notes && GetSelectedNoteReferences().Count > 1)
        {
            NudgeSelectedNotes(nudge);
            return;
        }

        ChartEditorNote selectedNote = mode == ChartEditorMode.Notes ? FindSelectedNote() : null;
        if (selectedNote != null)
            NudgeNote(selectedNote, nudge);
        else
            SetCursorTimeFromKeyboard(project.cursorTimeSeconds + nudge);
    }

    private void SetCursorTimeFromKeyboard(double seconds)
    {
        SetCursorTime(seconds, rebuild: false, syncAudio: false);
        silentPlaybackTimeSeconds = project.cursorTimeSeconds;
        lastAuditionTimeSeconds = project.cursorTimeSeconds;
        InvalidateAuditionCache();
        if (editorPlaying && editorAudioSource != null && editorAudioClip != null && editorAudioSource.isPlaying)
        {
            editorAudioSource.time = Mathf.Clamp(
                (float)project.cursorTimeSeconds,
                0f,
                Mathf.Max(0.01f, editorAudioClip.length - 0.01f));
        }
    }

    private void ApplyKeyboardVerticalNudge(int direction)
    {
        if (mode == ChartEditorMode.Notes && GetSelectedNoteReferences().Count > 1)
        {
            ChangeSelectedNoteLanes(direction);
            return;
        }

        ChartEditorNote selectedNote = mode == ChartEditorMode.Notes ? FindSelectedNote() : null;
        if (selectedNote != null)
            ChangeNoteLane(selectedNote, direction);
    }

    private double GetKeyboardNudgeSeconds()
    {
        ChartEditorProjectSettings settings = project?.settings;
        bool large = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        double fallback = large ? 0.1 : 0.01;
        if (settings == null)
            return fallback;

        double configured = large ? settings.largeNudgeSeconds : settings.smallNudgeSeconds;
        return Math.Max(0.001, configured > 0.0 ? configured : fallback);
    }

    private bool HandlePageNavigation(bool controlHeld, bool shiftHeld, bool altHeld)
    {
        int direction = 0;
        if (Input.GetKeyDown(KeyCode.PageUp))
            direction = -1;
        else if (Input.GetKeyDown(KeyCode.PageDown))
            direction = 1;

        if (direction == 0)
            return false;

        if (controlHeld)
        {
            double viewportSeconds = Math.Max(1.0, GetTimelineViewportWidth() / Math.Max(1f, GetTimelinePixelsPerSecond()));
            SeekAndRevealTime(project.cursorTimeSeconds + direction * viewportSeconds * 0.90, syncAudio: true, rebuild: false);
            return true;
        }

        if (altHeld)
        {
            SeekAdjacentAnchor(direction);
            return true;
        }

        if (shiftHeld)
        {
            SeekAdjacentNote(direction);
            return true;
        }

        SeekAdjacentBeat(direction);
        return true;
    }

    private bool HandleTempoShortcut(bool controlHeld, bool shiftHeld)
    {
        int direction = 0;
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            direction = -1;
        else if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
            direction = 1;

        if (direction == 0)
            return false;

        double step = controlHeld && shiftHeld ? 0.01 : shiftHeld ? 0.1 : 1.0;
        AdjustCurrentRegionBpm(direction * step);
        return true;
    }

    private bool HandleSnapShortcut()
    {
        if (Input.GetKeyDown(KeyCode.Comma))
        {
            CycleSnapStep(-1);
            return true;
        }

        if (Input.GetKeyDown(KeyCode.Period))
        {
            CycleSnapStep(1);
            return true;
        }

        return false;
    }

    private bool HandleBracketNoteMoveShortcut(bool controlHeld)
    {
        int direction = 0;
        if (Input.GetKeyDown(KeyCode.LeftBracket))
            direction = -1;
        else if (Input.GetKeyDown(KeyCode.RightBracket))
            direction = 1;

        if (direction == 0)
            return false;

        List<ChartEditorNoteReference> refs = GetSelectedNoteReferences();
        if (refs.Count == 0)
        {
            SetStatus("Select one or more notes before moving notes by grid snap.");
            return true;
        }

        double step = controlHeld ? 0.001 : GetSnapStepSeconds();
        NudgeSelectedNotes(step * direction);
        SetStatus($"Moved {refs.Count} note{(refs.Count == 1 ? string.Empty : "s")} by {step * 1000.0:0.#} ms.");
        return true;
    }

    private void SeekAdjacentBeat(int direction)
    {
        ChartEditorBeatMarker marker = FindAdjacentBeatMarker(ChartEditorTimingService.GetBeatMarkers(project), direction);
        if (marker == null)
        {
            SetStatus(direction < 0 ? "No previous beat marker." : "No next beat marker.");
            return;
        }

        SeekAndRevealTime(marker.audioTimeSeconds, syncAudio: true, rebuild: false);
        SetStatus($"Cursor moved to beat {marker.beatPosition:0.###}.");
    }

    private void SeekAdjacentAnchor(int direction)
    {
        ChartEditorBeatMarker anchor = FindAdjacentBeatMarker(ChartEditorTimingService.GetAnchors(project), direction);
        if (anchor == null)
        {
            SetStatus(direction < 0 ? "No previous anchor." : "No next anchor.");
            return;
        }

        SelectSingleAnchor(anchor);
        SeekAndRevealTime(anchor.audioTimeSeconds, syncAudio: true, rebuild: true);
        SetStatus($"Cursor moved to anchor at beat {anchor.beatPosition:0.###}.");
    }

    private ChartEditorBeatMarker FindAdjacentBeatMarker(List<ChartEditorBeatMarker> markers, int direction)
    {
        if (markers == null || markers.Count == 0)
            return null;

        double cursor = project?.cursorTimeSeconds ?? 0.0;
        const double epsilon = 0.0005;
        return direction < 0
            ? markers.Where(marker => marker != null && marker.audioTimeSeconds < cursor - epsilon)
                .OrderByDescending(marker => marker.audioTimeSeconds)
                .FirstOrDefault()
            : markers.Where(marker => marker != null && marker.audioTimeSeconds > cursor + epsilon)
                .OrderBy(marker => marker.audioTimeSeconds)
                .FirstOrDefault();
    }

    private void SeekAdjacentNote(int direction)
    {
        ChartEditorTrack track = project.SelectedTrack ?? project.tracks?.FirstOrDefault(candidate => candidate != null && candidate.visible);
        if (track?.notes == null || track.notes.Count == 0)
        {
            SetStatus("No notes are available in the selected track.");
            return;
        }

        double cursor = project.cursorTimeSeconds;
        const double epsilon = 0.0005;
        ChartEditorNote note = direction < 0
            ? track.notes.Where(candidate => candidate != null && candidate.timeSeconds < cursor - epsilon)
                .OrderByDescending(candidate => candidate.timeSeconds)
                .FirstOrDefault()
            : track.notes.Where(candidate => candidate != null && candidate.timeSeconds > cursor + epsilon)
                .OrderBy(candidate => candidate.timeSeconds)
                .FirstOrDefault();

        if (note == null)
        {
            SetStatus(direction < 0 ? "No previous note." : "No next note.");
            return;
        }

        SelectSingleNote(track, note);
        SeekAndRevealTime(note.timeSeconds, syncAudio: true, rebuild: true);
        SetStatus($"Cursor moved to note at {FormatTime(note.timeSeconds)}.");
    }

    private void ToggleMetronome()
    {
        if (project?.settings == null)
            return;

        project.settings.metronomeEnabled = !project.settings.metronomeEnabled;
        project.dirty = true;
        SetStatus(project.settings.metronomeEnabled ? "Metronome enabled." : "Metronome disabled.");
        Rebuild();
    }

    private void ToggleNoteClaps()
    {
        if (project?.settings == null)
            return;

        project.settings.noteClapsEnabled = !project.settings.noteClapsEnabled;
        project.dirty = true;
        SetStatus(project.settings.noteClapsEnabled ? "Note claps enabled." : "Note claps disabled.");
        Rebuild();
    }

    private void AdjustCurrentRegionBpm(double delta)
    {
        if (project == null)
            return;

        double beat = ChartEditorTimingService.GetBeatPositionForAudioTime(project, project.cursorTimeSeconds);
        double currentBpm = ChartEditorTimingService.GetTempoAtBeat(project, beat);
        double nextBpm = Math.Max(20.0, Math.Min(360.0, currentBpm + delta));
        if (!ChartEditorTimingService.SetTempoRegionBpmAtBeat(project, beat, nextBpm))
        {
            SetStatus("BPM was not changed because the next anchor is locked.");
            return;
        }

        project.dirty = true;
        SetStatus($"Region BPM set to {nextBpm:0.###}.");
        Rebuild();
    }

    private void CycleSnapStep(int direction)
    {
        if (project?.settings == null)
            return;

        double[] snapSteps =
        {
            0.001, 0.005, 0.010, 0.025, 0.050, 0.100, 0.125, 0.250, 0.500, 1.000
        };
        double current = Math.Max(0.001, project.settings.snapSeconds);
        int index = 0;
        double bestDistance = double.MaxValue;
        for (int i = 0; i < snapSteps.Length; i++)
        {
            double distance = Math.Abs(snapSteps[i] - current);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                index = i;
            }
        }

        index = Mathf.Clamp(index + direction, 0, snapSteps.Length - 1);
        project.settings.snapEnabled = true;
        project.settings.snapSeconds = snapSteps[index];
        project.dirty = true;
        SetStatus($"Grid snap set to {project.settings.snapSeconds * 1000.0:0.#} ms.");
        Rebuild();
    }

    private double GetSnapStepSeconds()
    {
        return Math.Max(0.001, project?.settings?.snapEnabled == true
            ? project.settings.snapSeconds
            : project?.settings?.smallNudgeSeconds ?? 0.01);
    }

    private bool IsTextFieldFocused()
    {
        Focusable focusedElement = RootElement?.panel?.focusController?.focusedElement;
        if (focusedElement == null)
            return false;

        if (focusedElement is TextField)
            return true;

        VisualElement focusedVisual = focusedElement as VisualElement;
        while (focusedVisual != null)
        {
            if (focusedVisual is TextField)
                return true;
            focusedVisual = focusedVisual.parent;
        }

        return false;
    }

    private float GetTimelineSecondsWidth()
    {
        return Mathf.Max(TimelineMinSecondsWidth, project.DurationSeconds * GetTimelinePixelsPerSecond() + TimelineRightPadding);
    }

    private float GetTimelineContentWidth()
    {
        return TimelineLabelWidth + GetTimelineSecondsWidth();
    }

    private float GetTimelineContentHeight()
    {
        float height = NotesTop;
        List<ChartEditorTrackViewGroup> visibleGroups = BuildTrackViewGroups()
            .Where(group => group != null && group.Visible)
            .ToList();
        if (visibleGroups.Count > 0)
        {
            for (int i = 0; i < visibleGroups.Count; i++)
            {
                ChartEditorTrackViewGroup group = visibleGroups[i];
                bool selectedTrack = group.ContainsSelected(project.selectedTrackId);
                height += selectedTrack ? SelectedTrackHeight : CompactTrackHeight;
                if (i + 1 < visibleGroups.Count)
                    height += TrackRowGap;
            }
        }

        return Mathf.Max(WaveformTop + WaveformHeight + 24f, height + TimelineBottomPadding);
    }

    private float ClampTimelineVerticalScroll(float y, float timelineHeight)
    {
        float viewportHeight = currentTimelineScrollView?.contentViewport != null
            ? currentTimelineScrollView.contentViewport.layout.height
            : 0f;
        if (viewportHeight <= 1f)
            return Mathf.Max(0f, y);

        return Mathf.Clamp(y, 0f, Mathf.Max(0f, timelineHeight - viewportHeight));
    }

    private List<ChartEditorTrackViewGroup> BuildTrackViewGroups()
    {
        List<ChartEditorTrackViewGroup> groups = new List<ChartEditorTrackViewGroup>();
        if (project?.tracks == null)
            return groups;

        Dictionary<string, ChartEditorTrackViewGroup> byKey = new Dictionary<string, ChartEditorTrackViewGroup>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < project.tracks.Count; i++)
        {
            ChartEditorTrack track = project.tracks[i];
            if (track == null)
                continue;

            string key = GetTrackGroupKey(track);
            if (!byKey.TryGetValue(key, out ChartEditorTrackViewGroup group))
            {
                group = new ChartEditorTrackViewGroup
                {
                    key = key,
                    sourceIndex = i,
                    color = ParseColor(track.colorHex, TrackColor(i))
                };
                byKey[key] = group;
                groups.Add(group);
            }

            group.tracks.Add(track);
            if (string.Equals(track.id, project.selectedTrackId, StringComparison.OrdinalIgnoreCase))
                group.activeTrack = track;
        }

        foreach (ChartEditorTrackViewGroup group in groups)
        {
            if (group.activeTrack != null)
                continue;

            group.activeTrack = group.tracks
                .Where(track => track != null && track.visible)
                .OrderByDescending(track => track.notes?.Count ?? 0)
                .ThenBy(track => track.displayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
                ?? group.tracks
                    .Where(track => track != null)
                    .OrderByDescending(track => track.notes?.Count ?? 0)
                    .FirstOrDefault();
        }

        return groups;
    }

    private static string GetTrackGroupKey(ChartEditorTrack track)
    {
        if (track == null)
            return string.Empty;

        string role = track.role.ToString();
        string tuning = NormalizeTrackKey(track.tuning?.displayName);
        string name = track.role == ChartEditorTrackRole.Custom
            ? NormalizeTrackKey(track.displayName)
            : string.Empty;
        return $"{role}|{tuning}|{name}";
    }

    private static string NormalizeTrackKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    private static string FormatTrackGroupName(ChartEditorTrackViewGroup group)
    {
        ChartEditorTrack track = group?.activeTrack;
        if (track == null)
            return "Track";

        return FormatTrackName(track);
    }

    private static string FormatTrackName(ChartEditorTrack track)
    {
        if (track == null)
            return "Track";

        switch (track.role)
        {
            case ChartEditorTrackRole.LeadGuitar:
                return "Lead Guitar";
            case ChartEditorTrackRole.RhythmGuitar:
                return "Rhythm Guitar";
            case ChartEditorTrackRole.Bass:
                return "Bass";
            case ChartEditorTrackRole.Drums:
                return "Drums";
            case ChartEditorTrackRole.Piano:
                return "Piano";
            case ChartEditorTrackRole.Vocals:
                return "Vocals";
            default:
                return FirstNonEmpty(track.displayName, track.importedName, "Track");
        }
    }

    private static IEnumerable<string> TrackRoleChoiceLabels()
    {
        yield return "Lead Guitar";
        yield return "Rhythm Guitar";
        yield return "Bass";
        yield return "Drums";
        yield return "Piano / Keys";
        yield return "Vocals";
        yield return "Custom";
    }

    private static string TrackRoleToChoiceLabel(ChartEditorTrackRole role)
    {
        switch (role)
        {
            case ChartEditorTrackRole.LeadGuitar:
                return "Lead Guitar";
            case ChartEditorTrackRole.RhythmGuitar:
                return "Rhythm Guitar";
            case ChartEditorTrackRole.Bass:
                return "Bass";
            case ChartEditorTrackRole.Drums:
                return "Drums";
            case ChartEditorTrackRole.Piano:
                return "Piano / Keys";
            case ChartEditorTrackRole.Vocals:
                return "Vocals";
            default:
                return "Custom";
        }
    }

    private static bool TryChoiceLabelToTrackRole(string label, out ChartEditorTrackRole role)
    {
        switch ((label ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "lead guitar":
                role = ChartEditorTrackRole.LeadGuitar;
                return true;
            case "rhythm guitar":
                role = ChartEditorTrackRole.RhythmGuitar;
                return true;
            case "bass":
                role = ChartEditorTrackRole.Bass;
                return true;
            case "drums":
                role = ChartEditorTrackRole.Drums;
                return true;
            case "piano / keys":
            case "piano":
            case "keys":
                role = ChartEditorTrackRole.Piano;
                return true;
            case "vocals":
                role = ChartEditorTrackRole.Vocals;
                return true;
            case "custom":
                role = ChartEditorTrackRole.Custom;
                return true;
            default:
                role = ChartEditorTrackRole.Custom;
                return false;
        }
    }

    private static string DefaultColorHexForRole(ChartEditorTrackRole role)
    {
        switch (role)
        {
            case ChartEditorTrackRole.LeadGuitar:
                return "#9B6BFF";
            case ChartEditorTrackRole.RhythmGuitar:
                return "#4AA6FF";
            case ChartEditorTrackRole.Bass:
                return "#53D37D";
            case ChartEditorTrackRole.Drums:
                return "#F59A45";
            case ChartEditorTrackRole.Piano:
                return "#D7E2FF";
            case ChartEditorTrackRole.Vocals:
                return "#F36BC3";
            default:
                return "#D4DAE8";
        }
    }

    private static void ApplyGeneratedPartRoleDefaults(ChartEditorTrack track, ChartEditorTrackRole role)
    {
        if (track == null)
            return;

        track.generatedPart ??= new ChartEditorGeneratedPartInfo();
        track.generatedPart.instrumentName = InstrumentNameForTrackRole(role);
        track.generatedPart.sourceMidiChannel = role == ChartEditorTrackRole.Drums ? 9 : 0;
        track.generatedPart.sourceMidiProgram = DefaultMidiProgramForTrackRole(role);
        track.generatedPart.isDrum = role == ChartEditorTrackRole.Drums;
        track.generatedPart.isGuitarFamily = IsGuitarFamilyTrackRole(role);
    }

    private static string InstrumentNameForTrackRole(ChartEditorTrackRole role)
    {
        switch (role)
        {
            case ChartEditorTrackRole.Bass:
                return "Bass";
            case ChartEditorTrackRole.Drums:
                return "Drums";
            case ChartEditorTrackRole.Piano:
                return "Piano";
            case ChartEditorTrackRole.Vocals:
                return "Vocals";
            default:
                return "Guitar";
        }
    }

    private static int DefaultMidiProgramForTrackRole(ChartEditorTrackRole role)
    {
        switch (role)
        {
            case ChartEditorTrackRole.Bass:
                return 33;
            case ChartEditorTrackRole.Piano:
                return 0;
            default:
                return 29;
        }
    }

    private static bool IsGuitarFamilyTrackRole(ChartEditorTrackRole role)
    {
        return role == ChartEditorTrackRole.LeadGuitar ||
               role == ChartEditorTrackRole.RhythmGuitar ||
               role == ChartEditorTrackRole.Bass ||
               role == ChartEditorTrackRole.Custom;
    }

    private static float GetLaneCenterY(float laneTop, int lane, float laneHeight)
    {
        return laneTop + lane * laneHeight + laneHeight * 0.5f;
    }

    private static float GetNoteTopForLane(float laneTop, int lane, float laneHeight, float noteHeight)
    {
        return GetLaneCenterY(laneTop, lane, laneHeight) - noteHeight * 0.5f;
    }

    private float GetNoteDrawWidth(ChartEditorTrack track, ChartEditorNote note, int laneCount, bool selectedTrack, float left)
    {
        float pixelsPerSecond = GetTimelinePixelsPerSecond();
        float minVisibleWidth = selectedTrack ? 8f : 6f;
        float preferredMinWidth = selectedTrack ? DefaultNoteSquareWidth : 10f;
        float rawWidth = (float)Math.Max(0.055, GetNoteEffectiveDurationSeconds(note)) * pixelsPerSecond;
        float width = selectedTrack
            ? Mathf.Max(preferredMinWidth, rawWidth)
            : Mathf.Clamp(rawWidth, preferredMinWidth, 140f);

        double? nextTime = GetNextNoteTimeInLane(track, note, laneCount, selectedTrack);
        if (nextTime.HasValue)
        {
            float available = TimeToPixels(nextTime.Value) - left - (selectedTrack ? 10f : 5f);
            if (available > 0f)
                width = Mathf.Min(width, Mathf.Max(minVisibleWidth, available));
            else
                width = minVisibleWidth;
        }

        return Mathf.Max(minVisibleWidth, width);
    }

    private static double GetNoteEffectiveDurationSeconds(ChartEditorNote note)
    {
        if (note == null)
            return 0.0;

        double duration = Math.Max(0.0, note.durationSeconds);
        if (note.techniqueSegments != null)
        {
            for (int i = 0; i < note.techniqueSegments.Count; i++)
            {
                ChartEditorTechniqueSegment segment = note.techniqueSegments[i];
                if (segment == null)
                    continue;

                duration = Math.Max(duration, Math.Max(0f, Math.Max(segment.startOffset, segment.endOffset)));
            }
        }

        return duration;
    }

    private static void EnsureNoteDurationCoversTechniqueSegments(ChartEditorNote note)
    {
        if (note == null)
            return;

        double effectiveDuration = GetNoteEffectiveDurationSeconds(note);
        if (effectiveDuration > note.durationSeconds + 0.0005)
            note.durationSeconds = effectiveDuration;
    }

    private static void SetNoteDurationSeconds(ChartEditorNote note, double durationSeconds)
    {
        if (note == null)
            return;

        double oldEffectiveDuration = GetNoteEffectiveDurationSeconds(note);
        double safeDuration = Math.Max(0.01, durationSeconds);
        note.durationSeconds = safeDuration;
        NormalizeTechniqueSegmentsForDuration(note, oldEffectiveDuration, safeDuration);
    }

    private static void NormalizeTechniqueSegmentsForDuration(ChartEditorNote note, double oldEffectiveDuration, double newDuration)
    {
        if (note?.techniqueSegments == null)
            return;

        float maxOffset = Mathf.Max(0.01f, (float)newDuration);
        bool extended = newDuration > oldEffectiveDuration + 0.0005;
        float oldTail = Mathf.Max(0f, (float)oldEffectiveDuration);
        for (int i = 0; i < note.techniqueSegments.Count; i++)
        {
            ChartEditorTechniqueSegment segment = note.techniqueSegments[i];
            if (segment == null)
                continue;

            if (extended && segment.endOffset >= oldTail - 0.001f)
                segment.endOffset = maxOffset;

            segment.startOffset = Mathf.Clamp(segment.startOffset, 0f, maxOffset);
            segment.endOffset = Mathf.Clamp(segment.endOffset, 0f, maxOffset);
            if (segment.endOffset < segment.startOffset)
                segment.endOffset = segment.startOffset;
        }
    }

    private double? GetNextNoteTimeInLane(ChartEditorTrack track, ChartEditorNote current, int laneCount, bool selectedTrack)
    {
        if (track?.notes == null || current == null)
            return null;

        int currentLane = selectedTrack ? GetVisualLaneForNote(track, current, laneCount) : 0;
        double? nextTime = null;
        for (int i = 0; i < track.notes.Count; i++)
        {
            ChartEditorNote candidate = track.notes[i];
            if (candidate == null || ReferenceEquals(candidate, current))
                continue;
            if (candidate.timeSeconds <= current.timeSeconds + 0.0001)
                continue;

            int candidateLane = selectedTrack ? GetVisualLaneForNote(track, candidate, laneCount) : 0;
            if (candidateLane != currentLane)
                continue;

            if (!nextTime.HasValue || candidate.timeSeconds < nextTime.Value)
                nextTime = candidate.timeSeconds;
        }

        return nextTime;
    }

    private void AddTechniqueOverlayLabel(
        VisualElement techniqueLayer,
        ChartEditorNote note,
        float noteLeft,
        float noteTop,
        float noteWidth,
        List<Rect> occupiedLabels)
    {
        string text = GetNoteTechniqueOverlayText(note);
        if (techniqueLayer == null || string.IsNullOrWhiteSpace(text))
            return;

        float labelWidth = EstimateTechniqueLabelWidth(text);
        float desiredLeft = Mathf.Max(0f, noteLeft + noteWidth * 0.5f - labelWidth * 0.5f);
        Rect labelRect = PlaceTechniqueLabelRect(desiredLeft, noteTop, labelWidth, occupiedLabels);

        Label label = CreateLabel(text, 20f, Color.white, true, TextAnchor.MiddleCenter, false);
        label.style.position = Position.Absolute;
        label.style.left = labelRect.x;
        label.style.top = labelRect.y;
        label.style.width = labelRect.width;
        label.style.height = TechniqueLabelHeight;
        label.style.paddingLeft = 8f;
        label.style.paddingRight = 8f;
        label.style.backgroundColor = new Color(0.010f, 0.012f, 0.016f, 0.74f);
        label.style.borderTopWidth = 1f;
        label.style.borderRightWidth = 1f;
        label.style.borderBottomWidth = 1f;
        label.style.borderLeftWidth = 1f;
        label.style.borderTopColor = new Color(1f, 1f, 1f, 0.18f);
        label.style.borderRightColor = new Color(1f, 1f, 1f, 0.18f);
        label.style.borderBottomColor = new Color(1f, 1f, 1f, 0.18f);
        label.style.borderLeftColor = new Color(1f, 1f, 1f, 0.18f);
        label.style.borderTopLeftRadius = 4f;
        label.style.borderTopRightRadius = 4f;
        label.style.borderBottomLeftRadius = 4f;
        label.style.borderBottomRightRadius = 4f;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        label.pickingMode = PickingMode.Ignore;
        techniqueLayer.Add(label);
        occupiedLabels?.Add(ExpandRect(labelRect, TechniqueLabelSlotGap));
    }

    private void AddTechniqueSegmentBoxes(
        VisualElement noteLayer,
        VisualElement noteBlock,
        ChartEditorTrack track,
        ChartEditorNote note,
        int laneCount,
        bool selectedTrack,
        float noteLeft,
        float noteTop,
        float noteWidth)
    {
        if (noteLayer == null || track == null || note == null || note.techniqueSegments == null || note.techniqueSegments.Count == 0)
            return;

        float pixelsPerSecond = GetTimelinePixelsPerSecond();
        float boxTop = Mathf.Max(2f, noteTop - TechniqueSegmentBoxHeight - TechniqueSegmentBoxGap);
        Dictionary<ChartEditorTechniqueSegment, VisualElement> segmentBoxes = new Dictionary<ChartEditorTechniqueSegment, VisualElement>();
        List<ChartEditorTechniqueSegment> ordered = note.techniqueSegments
            .Where(segment => segment != null)
            .OrderBy(segment => segment.startOffset)
            .ThenBy(segment => segment.endOffset)
            .ToList();
        Dictionary<ChartEditorTechniqueSegment, int> visualLanes = AssignTechniqueSegmentVisualLanes(ordered);

        for (int i = 0; i < ordered.Count; i++)
        {
            ChartEditorTechniqueSegment segment = ordered[i];
            Color color = GetTechniqueSegmentColor(segment.type);
            int visualLane = visualLanes.TryGetValue(segment, out int assignedLane) ? assignedLane : 0;

            VisualElement box = new VisualElement();
            box.style.position = Position.Absolute;
            box.style.top = Mathf.Max(2f, boxTop - visualLane * (TechniqueSegmentBoxHeight + TechniqueSegmentVisualLaneGap));
            box.style.height = TechniqueSegmentBoxHeight;
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;
            box.style.justifyContent = Justify.Center;
            box.style.backgroundColor = new Color(color.r, color.g, color.b, 0.82f);
            box.style.overflow = Overflow.Hidden;
            SetRadius(box, 9f);
            SetBorderWidth(box, 1f);
            SetBorderColor(box, new Color(1f, 1f, 1f, 0.22f));
            box.pickingMode = PickingMode.Position;

            Label label = CreateLabel(GetTechniqueSegmentLabel(segment), 18f, Color.white, true, TextAnchor.MiddleCenter, false);
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.flexGrow = 1f;
            label.pickingMode = PickingMode.Ignore;
            box.Add(label);

            VisualElement leftHandle = CreateTechniqueSegmentResizeHandle(leftSide: true);
            VisualElement rightHandle = CreateTechniqueSegmentResizeHandle(leftSide: false);
            box.Add(leftHandle);
            box.Add(rightHandle);

            segmentBoxes[segment] = box;
            currentTechniqueSegmentVisuals.Add(new TechniqueSegmentVisual
            {
                track = track,
                note = note,
                segment = segment,
                box = box,
                laneCount = laneCount,
                selectedTrack = selectedTrack
            });
            AddTechniqueSegmentDragHandlers(box, leftHandle, rightHandle, noteBlock, track, note, segment, segmentBoxes, laneCount, selectedTrack);
            ApplyTechniqueSegmentBoxLayout(box, segment, noteLeft, noteWidth, pixelsPerSecond);
            noteLayer.Add(box);
        }
    }

    private static Dictionary<ChartEditorTechniqueSegment, int> AssignTechniqueSegmentVisualLanes(List<ChartEditorTechniqueSegment> ordered)
    {
        Dictionary<ChartEditorTechniqueSegment, int> lanes = new Dictionary<ChartEditorTechniqueSegment, int>();
        if (ordered == null || ordered.Count == 0)
            return lanes;

        List<float> laneEnds = new List<float>();
        for (int i = 0; i < ordered.Count; i++)
        {
            ChartEditorTechniqueSegment segment = ordered[i];
            if (segment == null)
                continue;

            float start = Mathf.Max(0f, segment.startOffset);
            float end = Mathf.Max(start + TechniqueSegmentMinimumSeconds, segment.endOffset);
            int lane = 0;
            for (; lane < laneEnds.Count; lane++)
            {
                if (start >= laneEnds[lane] - 0.0005f)
                    break;
            }

            if (lane == laneEnds.Count)
                laneEnds.Add(end);
            else
                laneEnds[lane] = end;

            lanes[segment] = lane;
        }

        return lanes;
    }

    private void ApplyTechniqueSegmentBoxLayout(
        VisualElement box,
        ChartEditorTechniqueSegment segment,
        float noteLeft,
        float noteWidth,
        float pixelsPerSecond)
    {
        if (box == null || segment == null)
            return;

        float startPixels = Mathf.Clamp(Mathf.Max(0f, segment.startOffset) * pixelsPerSecond, 0f, Mathf.Max(0f, noteWidth));
        float endPixels = Mathf.Clamp(Mathf.Max(segment.startOffset, segment.endOffset) * pixelsPerSecond, startPixels, Mathf.Max(0f, noteWidth));
        float width = Mathf.Max(2f, endPixels - startPixels);
        if (startPixels >= noteWidth - 1f)
        {
            startPixels = Mathf.Max(0f, noteWidth - 2f);
            width = 2f;
        }

        box.style.left = noteLeft + startPixels;
        box.style.width = width;

        Label label = box.Q<Label>();
        if (label != null)
        {
            label.text = width >= 92f ? GetTechniqueSegmentLabel(segment) : GetTechniqueSegmentCompactLabel(segment);
            label.style.display = width >= 34f ? DisplayStyle.Flex : DisplayStyle.None;
            label.style.fontSize = UiFont(width >= 92f ? 18f : 15f);
        }
    }

    private static void EnsureLegacyTechniqueSegments(ChartEditorNote note)
    {
        if (note == null)
            return;

        note.techniqueSegments ??= new List<ChartEditorTechniqueSegment>();
        float duration = Mathf.Max(0.05f, (float)Math.Max(0.05, note.durationSeconds));
        NormalizeBendPointTechniqueSegments(note);
        bool addedFallbackSegment = false;

        if ((note.technique == NoteTechnique.Slide || note.slideTargetFret >= 0) &&
            !HasTechniqueSegment(note, NoteTechniqueSegmentType.Slide))
        {
            EnsureSlideTechniqueSegment(note);
            addedFallbackSegment = true;
        }

        if ((note.technique == NoteTechnique.Vibrato) &&
            !HasTechniqueSegment(note, NoteTechniqueSegmentType.Vibrato))
        {
            ChartEditorTechniqueSegment vibrato = CreateTechniqueSegment(note, NoteTechniqueSegmentType.Vibrato, 0f, duration);
            note.techniqueSegments.Add(vibrato);
            addedFallbackSegment = true;
        }

        if ((note.technique == NoteTechnique.Bend || Mathf.Abs(note.bendStep) > 0.01f || note.bendPreBend || note.bendRelease) &&
            !HasBendBearingTechniqueSegment(note))
        {
            float bend = Mathf.Max(0.5f, Mathf.Abs(note.bendStep));
            ChartEditorTechniqueSegment bendSegment = CreateTechniqueSegment(note, NoteTechniqueSegmentType.Bend, 0f, duration);
            bendSegment.startBend = note.bendPreBend ? bend : 0f;
            bendSegment.endBend = note.bendRelease ? 0f : bend;
            note.techniqueSegments.Add(bendSegment);
            addedFallbackSegment = true;
        }

        if (addedFallbackSegment)
            NormalizeTechniqueSegmentLayout(note);
        SyncNoteDurationToTechniqueSegments(note, allowShrink: false);
    }

    private static void NormalizeBendPointTechniqueSegments(ChartEditorNote note)
    {
        if (note == null ||
            note.bendPoints == null ||
            note.bendPoints.Count == 0 ||
            !(note.technique == NoteTechnique.Bend || Mathf.Abs(note.bendStep) > 0.01f || note.bendPreBend || note.bendRelease))
        {
            return;
        }

        RocksmithCachedNoteData source = new RocksmithCachedNoteData
        {
            id = note.sourceNoteId,
            time = Mathf.Max(0f, (float)note.timeSeconds),
            duration = Mathf.Max(0f, (float)note.durationSeconds),
            stringIdx = note.stringOrLane,
            fret = note.fret,
            technique = (int)NoteTechnique.Bend,
            slideTargetFret = note.slideTargetFret,
            bendStep = Mathf.Max(note.bendStep, note.maxBend),
            bendVisualStartTime = note.bendVisualStartTime,
            bendVisualDuration = note.bendVisualDuration,
            bendPreBend = note.bendPreBend,
            bendRelease = note.bendRelease,
            hasVibrato = note.technique == NoteTechnique.Vibrato,
            maxBend = Mathf.Max(note.maxBend, note.bendStep),
            bendPoints = new List<RocksmithCachedBendPointData>(),
            techniqueSegments = new List<RocksmithCachedTechniqueSegmentData>()
        };

        for (int i = 0; i < note.bendPoints.Count; i++)
        {
            ChartEditorBendPoint point = note.bendPoints[i];
            if (point == null)
                continue;

            source.bendPoints.Add(new RocksmithCachedBendPointData
            {
                timeSeconds = point.timeSeconds,
                step = point.step
            });
        }

        if (note.techniqueSegments != null)
        {
            for (int i = 0; i < note.techniqueSegments.Count; i++)
            {
                ChartEditorTechniqueSegment segment = note.techniqueSegments[i];
                if (segment == null)
                    continue;

                source.techniqueSegments.Add(new RocksmithCachedTechniqueSegmentData
                {
                    type = (int)segment.type,
                    startOffset = segment.startOffset,
                    endOffset = segment.endOffset,
                    startFret = segment.startFret,
                    endFret = segment.endFret,
                    startBend = segment.startBend,
                    endBend = segment.endBend
                });
            }
        }

        List<NoteTechniqueSegmentData> normalized = RocksmithCachedSongLoader.BuildNormalizedTechniqueSegments(source);
        if (normalized == null || normalized.Count == 0 || TechniqueSegmentsEquivalent(note.techniqueSegments, normalized))
            return;

        note.techniqueSegments = normalized
            .Select(segment => new ChartEditorTechniqueSegment
            {
                type = segment.type,
                startOffset = segment.startOffset,
                endOffset = segment.endOffset,
                startFret = segment.startFret,
                endFret = segment.endFret,
                startBend = segment.startBend,
                endBend = segment.endBend
            })
            .ToList();
    }

    private static bool TechniqueSegmentsEquivalent(List<ChartEditorTechniqueSegment> editorSegments, List<NoteTechniqueSegmentData> runtimeSegments)
    {
        editorSegments ??= new List<ChartEditorTechniqueSegment>();
        runtimeSegments ??= new List<NoteTechniqueSegmentData>();
        List<ChartEditorTechniqueSegment> existing = editorSegments.Where(segment => segment != null).ToList();
        if (existing.Count != runtimeSegments.Count)
            return false;

        for (int i = 0; i < existing.Count; i++)
        {
            ChartEditorTechniqueSegment left = existing[i];
            NoteTechniqueSegmentData right = runtimeSegments[i];
            if (left.type != right.type ||
                !NearlyEqual(left.startOffset, right.startOffset) ||
                !NearlyEqual(left.endOffset, right.endOffset) ||
                left.startFret != right.startFret ||
                left.endFret != right.endFret ||
                !NearlyEqual(left.startBend, right.startBend) ||
                !NearlyEqual(left.endBend, right.endBend))
            {
                return false;
            }
        }

        return true;
    }

    private VisualElement CreateTechniqueSegmentResizeHandle(bool leftSide)
    {
        VisualElement handle = new VisualElement();
        handle.style.position = Position.Absolute;
        handle.style.top = 4f;
        handle.style.bottom = 4f;
        handle.style.width = TechniqueSegmentResizeHandleWidth;
        if (leftSide)
            handle.style.left = 0f;
        else
            handle.style.right = 0f;
        handle.style.backgroundColor = new Color(1f, 1f, 1f, 0.20f);
        handle.pickingMode = PickingMode.Position;
        SetElementCursor(handle, ChartEditorCursorKind.ResizeHorizontal);
        return handle;
    }

    private static string GetTechniqueSegmentLabel(ChartEditorTechniqueSegment segment)
    {
        if (segment == null)
            return "Segment";

        switch (segment.type)
        {
            case NoteTechniqueSegmentType.Slide:
                return string.Equals(FormatSlideTechniqueLabel(segment.startFret, segment.endFret), "SL", StringComparison.OrdinalIgnoreCase)
                    ? "Slide"
                    : FormatSlideTechniqueLabel(segment.startFret, segment.endFret);
            case NoteTechniqueSegmentType.Bend:
                return FormatBendSegmentLabel(segment);
            case NoteTechniqueSegmentType.Sustain:
                if (Mathf.Max(segment.startBend, segment.endBend) > 0.01f)
                    return FormatBentSustainSegmentLabel(segment, compact: false);
                return "Sustain";
            case NoteTechniqueSegmentType.Vibrato:
                return "Vibrato";
            default:
                return "Segment";
        }
    }

    private static string GetTechniqueSegmentCompactLabel(ChartEditorTechniqueSegment segment)
    {
        if (segment == null)
            return string.Empty;

        switch (segment.type)
        {
            case NoteTechniqueSegmentType.Slide:
                return "SL";
            case NoteTechniqueSegmentType.Bend:
                return Mathf.Max(segment.startBend, segment.endBend) > 0.01f ? "^" : "B";
            case NoteTechniqueSegmentType.Sustain:
                if (Mathf.Max(segment.startBend, segment.endBend) > 0.01f)
                    return FormatBentSustainSegmentLabel(segment, compact: true);
                return "S";
            case NoteTechniqueSegmentType.Vibrato:
                return "~";
            default:
                return string.Empty;
        }
    }

    private static string FormatBendSegmentLabel(ChartEditorTechniqueSegment segment)
    {
        if (segment == null)
            return "Bend";

        float start = Mathf.Max(0f, segment.startBend);
        float end = Mathf.Max(0f, segment.endBend);
        float amount = Mathf.Max(start, end);
        string labelAmount = FormatBendAmountTitleLabel(amount);
        if (string.IsNullOrWhiteSpace(labelAmount))
            return "Bend";

        if (end > start + 0.01f)
            return labelAmount + " Bend";
        if (start > end + 0.01f)
            return "Release " + labelAmount;
        if (start > 0.01f && end > 0.01f)
            return segment.startOffset <= 0.001f ? "Pre " + labelAmount : labelAmount + " Bend";
        return "Bend";
    }

    private static string FormatBentSustainSegmentLabel(ChartEditorTechniqueSegment segment, bool compact)
    {
        if (segment == null)
            return compact ? "S" : "Sustain";

        float amount = Mathf.Max(0f, Mathf.Max(segment.startBend, segment.endBend));
        string labelAmount = FormatBendAmountTitleLabel(amount);
        if (string.IsNullOrWhiteSpace(labelAmount))
            return compact ? "S" : "Sustain";

        bool startsBent = segment.startOffset <= 0.001f && segment.startBend > 0.01f;
        if (compact)
            return startsBent ? "pre" : "^";

        return startsBent ? "Pre " + labelAmount + " Sustain" : labelAmount + " Sustain";
    }

    private static Color GetTechniqueSegmentColor(NoteTechniqueSegmentType type)
    {
        switch (type)
        {
            case NoteTechniqueSegmentType.Slide:
                return new Color(0.18f, 0.72f, 0.96f, 1f);
            case NoteTechniqueSegmentType.Bend:
                return new Color(0.96f, 0.62f, 0.20f, 1f);
            case NoteTechniqueSegmentType.Sustain:
                return new Color(0.42f, 0.52f, 0.64f, 1f);
            case NoteTechniqueSegmentType.Vibrato:
                return new Color(0.70f, 0.38f, 1f, 1f);
            default:
                return new Color(0.62f, 0.70f, 0.82f, 1f);
        }
    }

    private void AddTechniqueSegmentDragHandlers(
        VisualElement box,
        VisualElement leftHandle,
        VisualElement rightHandle,
        VisualElement noteBlock,
        ChartEditorTrack track,
        ChartEditorNote note,
        ChartEditorTechniqueSegment segment,
        Dictionary<ChartEditorTechniqueSegment, VisualElement> segmentBoxes,
        int laneCount,
        bool selectedTrack)
    {
        if (box == null || note == null || segment == null)
            return;

        bool dragging = false;
        int pointerId = -1;
        int dragMode = 0; // 1 = move, 2 = left edge, 3 = right edge
        Vector2 startPointer = Vector2.zero;
        float startOffset = 0f;
        float endOffset = 0f;

        void BeginDrag(PointerDownEvent evt, int mode)
        {
            if (evt.button != 0)
                return;

            dragging = true;
            pointerId = evt.pointerId;
            dragMode = mode;
            startPointer = PointerPosition(evt);
            startOffset = Mathf.Max(0f, segment.startOffset);
            endOffset = Mathf.Max(startOffset + TechniqueSegmentMinimumSeconds, segment.endOffset);
            SelectSingleNote(track, note);
            box.BringToFront();
            box.CapturePointer(pointerId);
            evt.StopImmediatePropagation();
        }

        box.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button == 1)
            {
                SelectSingleNote(track, note);
                ShowTechniqueSegmentContextMenu(evt.position, track, note, segment);
                evt.StopImmediatePropagation();
                return;
            }

            BeginDrag(evt, 1);
        });
        leftHandle.RegisterCallback<PointerDownEvent>(evt => BeginDrag(evt, 2));
        rightHandle.RegisterCallback<PointerDownEvent>(evt => BeginDrag(evt, 3));

        box.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            float deltaSeconds = (float)PixelDeltaToSeconds(PointerPosition(evt).x - startPointer.x);
            float newStart = startOffset;
            float newEnd = endOffset;
            float maxEnd = Mathf.Max(TechniqueSegmentMinimumSeconds, (float)((project?.DurationSeconds ?? note.timeSeconds + endOffset + 4.0) - note.timeSeconds));
            float minLength = TechniqueSegmentMinimumSeconds;

            if (dragMode == 1)
            {
                float length = Mathf.Max(minLength, endOffset - startOffset);
                newStart = Mathf.Clamp(startOffset + deltaSeconds, 0f, Mathf.Max(0f, maxEnd - length));
                newEnd = newStart + length;
            }
            else if (dragMode == 2)
            {
                newStart = Mathf.Clamp(startOffset + deltaSeconds, 0f, Mathf.Max(0f, endOffset - minLength));
                newEnd = endOffset;
            }
            else if (dragMode == 3)
            {
                newStart = startOffset;
                newEnd = Mathf.Clamp(endOffset + deltaSeconds, newStart + minLength, maxEnd);
            }

            ApplyTechniqueSegmentOffsets(noteBlock, track, note, segment, segmentBoxes, laneCount, selectedTrack, newStart, newEnd);
            evt.StopImmediatePropagation();
        });

        box.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            dragging = false;
            if (box.HasPointerCapture(pointerId))
                box.ReleasePointer(pointerId);

            SortTechniqueSegments(note);
            ApplyTechniqueSegmentSummaries(note);
            NormalizePrimaryTechnique(note);
            ChartEditorTimingService.UpdateNoteBeatTiming(project, note);
            project.dirty = true;
            evt.StopImmediatePropagation();
            box.schedule.Execute(Rebuild);
        });
    }

    private void ApplyTechniqueSegmentOffsets(
        VisualElement noteBlock,
        ChartEditorTrack track,
        ChartEditorNote note,
        ChartEditorTechniqueSegment segment,
        Dictionary<ChartEditorTechniqueSegment, VisualElement> segmentBoxes,
        int laneCount,
        bool selectedTrack,
        float startOffset,
        float endOffset)
    {
        if (note == null || segment == null)
            return;

        bool clearBendPoints = IsBendBearingTechniqueSegment(segment) &&
            (!NearlyEqual(segment.startOffset, startOffset) || !NearlyEqual(segment.endOffset, endOffset));
        segment.startOffset = Mathf.Max(0f, startOffset);
        segment.endOffset = Mathf.Max(segment.startOffset + TechniqueSegmentMinimumSeconds, endOffset);
        if (clearBendPoints)
            ClearBendPoints(note);
        NormalizeTechniqueSegmentLayout(note, segment);
        SyncNoteDurationToTechniqueSegments(note, allowShrink: true);
        UpdateTechniqueSegmentBoxLayout(noteBlock, track, note, segmentBoxes, laneCount, selectedTrack);

        project.dirty = true;
    }

    private void UpdateTechniqueSegmentBoxLayout(
        VisualElement noteBlock,
        ChartEditorTrack track,
        ChartEditorNote note,
        Dictionary<ChartEditorTechniqueSegment, VisualElement> segmentBoxes,
        int laneCount,
        bool selectedTrack)
    {
        if (note == null)
            return;

        float noteLeft = TimeToPixels(note.timeSeconds);
        float pixelsPerSecond = GetTimelinePixelsPerSecond();
        if (segmentBoxes != null)
        {
            float noteWidth = GetNoteDrawWidth(track, note, laneCount, selectedTrack, noteLeft);
            foreach (KeyValuePair<ChartEditorTechniqueSegment, VisualElement> entry in segmentBoxes)
            {
                ChartEditorTechniqueSegment segment = entry.Key;
                VisualElement box = entry.Value;
                if (segment == null || box == null)
                    continue;

                ApplyTechniqueSegmentBoxLayout(box, segment, noteLeft, noteWidth, pixelsPerSecond);
            }
        }

        if (noteBlock != null)
            noteBlock.style.width = GetNoteDrawWidth(track, note, laneCount, selectedTrack, noteLeft);
    }

    private static void SyncNoteDurationToTechniqueSegments(ChartEditorNote note, bool allowShrink)
    {
        if (note?.techniqueSegments == null || note.techniqueSegments.Count == 0)
            return;

        double maxEnd = 0.0;
        for (int i = 0; i < note.techniqueSegments.Count; i++)
        {
            ChartEditorTechniqueSegment segment = note.techniqueSegments[i];
            if (segment == null)
                continue;

            maxEnd = Math.Max(maxEnd, Math.Max(0f, Math.Max(segment.startOffset, segment.endOffset)));
        }

        if (allowShrink || maxEnd > note.durationSeconds)
            note.durationSeconds = Math.Max(0.01, maxEnd);
    }

    private static void NormalizeTechniqueSegmentLayout(ChartEditorNote note, ChartEditorTechniqueSegment prioritySegment = null)
    {
        if (note?.techniqueSegments == null)
            return;

        List<ChartEditorTechniqueSegment> ordered = note.techniqueSegments
            .Where(segment => segment != null)
            .OrderBy(segment => Mathf.Max(0f, segment.startOffset))
            .ThenBy(segment => ReferenceEquals(segment, prioritySegment) ? 0 : 1)
            .ThenBy(segment => Mathf.Max(segment.startOffset + TechniqueSegmentMinimumSeconds, segment.endOffset))
            .ToList();

        Dictionary<int, float> laneCursors = new Dictionary<int, float>();
        for (int i = 0; i < ordered.Count; i++)
        {
            ChartEditorTechniqueSegment segment = ordered[i];
            int lane = GetTechniqueSegmentLayoutLane(segment.type);
            laneCursors.TryGetValue(lane, out float cursor);
            float length = Mathf.Max(TechniqueSegmentMinimumSeconds, segment.endOffset - segment.startOffset);
            float start = Mathf.Max(cursor, Mathf.Max(0f, segment.startOffset));
            segment.startOffset = start;
            segment.endOffset = start + length;
            laneCursors[lane] = segment.endOffset;
        }

        note.techniqueSegments = ordered
            .OrderBy(segment => segment.startOffset)
            .ThenBy(segment => GetTechniqueSegmentLayoutLane(segment.type))
            .ThenBy(segment => segment.endOffset)
            .ToList();
    }

    private static int GetTechniqueSegmentLayoutLane(NoteTechniqueSegmentType type)
    {
        return type == NoteTechniqueSegmentType.Bend ? 1 : 0;
    }

    private static void SortTechniqueSegments(ChartEditorNote note)
    {
        if (note?.techniqueSegments == null)
            return;

        note.techniqueSegments = note.techniqueSegments
            .Where(segment => segment != null)
            .OrderBy(segment => segment.startOffset)
            .ThenBy(segment => GetTechniqueSegmentLayoutLane(segment.type))
            .ThenBy(segment => segment.endOffset)
            .ToList();
    }

    private void AddNoteSustainHandle(VisualElement block, ChartEditorTrack track, ChartEditorNote note, int laneCount, bool selectedTrack, float noteHeight, float noteWidth)
    {
        if (block == null || track == null || note == null)
            return;

        float handleSize = Mathf.Clamp(noteWidth * 0.42f, SustainHandleMinSize, SustainHandleMaxSize);
        VisualElement handle = new VisualElement();
        handle.style.position = Position.Absolute;
        handle.style.right = Mathf.Clamp(noteWidth * 0.08f, 2f, 4f);
        handle.style.top = noteHeight * 0.5f - handleSize * 0.5f;
        handle.style.width = handleSize;
        handle.style.height = handleSize;
        handle.style.backgroundColor = new Color(0.96f, 0.98f, 1f, 0.94f);
        handle.style.borderTopWidth = 2f;
        handle.style.borderRightWidth = 2f;
        handle.style.borderBottomWidth = 2f;
        handle.style.borderLeftWidth = 2f;
        handle.style.borderTopColor = new Color(0.03f, 0.04f, 0.06f, 0.92f);
        handle.style.borderRightColor = new Color(0.03f, 0.04f, 0.06f, 0.92f);
        handle.style.borderBottomColor = new Color(0.03f, 0.04f, 0.06f, 0.92f);
        handle.style.borderLeftColor = new Color(0.03f, 0.04f, 0.06f, 0.92f);
        SetRadius(handle, 999f);
        handle.pickingMode = PickingMode.Position;
        SetElementCursor(handle, ChartEditorCursorKind.ResizeHorizontal);

        bool dragging = false;
        int pointerId = -1;
        Vector2 startPointer = Vector2.zero;
        double startDuration = 0.0;

        handle.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;

            dragging = true;
            pointerId = evt.pointerId;
            startPointer = PointerPosition(evt);
            startDuration = GetNoteEffectiveDurationSeconds(note);
            SelectSingleNote(track, note);
            handle.CapturePointer(pointerId);
            evt.StopImmediatePropagation();
        });

        handle.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            double requestedDuration = startDuration + PixelDeltaToSeconds(PointerPosition(evt).x - startPointer.x);
            double maxDuration = Math.Max(0.01, project.DurationSeconds - note.timeSeconds);
            double? nextTime = GetNextNoteTimeInLane(track, note, laneCount, selectedTrack);
            if (nextTime.HasValue)
                maxDuration = Math.Max(0.01, Math.Min(maxDuration, nextTime.Value - note.timeSeconds - GetPasteVisualGapSeconds()));

            SetNoteDurationSeconds(note, Math.Max(0.01, Math.Min(maxDuration, requestedDuration)));
            block.style.width = GetNoteDrawWidth(track, note, laneCount, selectedTrack, TimeToPixels(note.timeSeconds));
            project.dirty = true;
            evt.StopImmediatePropagation();
        });

        handle.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            dragging = false;
            if (handle.HasPointerCapture(pointerId))
                handle.ReleasePointer(pointerId);

            evt.StopImmediatePropagation();
            handle.schedule.Execute(Rebuild);
        });

        block.Add(handle);
    }

    private static float EstimateTechniqueLabelWidth(string text)
    {
        return Mathf.Clamp((text?.Length ?? 0) * 18f + 30f, 48f, 240f);
    }

    private static Rect PlaceTechniqueLabelRect(float desiredLeft, float noteTop, float labelWidth, List<Rect> occupiedLabels)
    {
        Rect best = new Rect(desiredLeft, Mathf.Max(2f, noteTop - TechniqueLabelHeight - TechniqueLabelNoteGap), labelWidth, TechniqueLabelHeight);
        float bestScore = float.MaxValue;
        for (int slot = 0; slot < TechniqueLabelSlotCount; slot++)
        {
            float top = Mathf.Max(2f, noteTop - TechniqueLabelHeight - TechniqueLabelNoteGap - slot * (TechniqueLabelHeight + 5f));
            float left = desiredLeft;
            if (occupiedLabels != null)
            {
                for (int iteration = 0; iteration <= occupiedLabels.Count; iteration++)
                {
                    Rect candidate = ExpandRect(new Rect(left, top, labelWidth, TechniqueLabelHeight), TechniqueLabelSlotGap);
                    Rect overlap = occupiedLabels.FirstOrDefault(existing => existing.Overlaps(candidate));
                    if (overlap.width <= 0f && overlap.height <= 0f)
                        break;

                    left = overlap.xMax + TechniqueLabelSlotGap;
                }
            }

            float score = Mathf.Abs(left - desiredLeft) + slot * 34f;
            if (score < bestScore)
            {
                bestScore = score;
                best = new Rect(left, top, labelWidth, TechniqueLabelHeight);
            }
        }

        return best;
    }

    private static Rect ExpandRect(Rect rect, float padding)
    {
        return new Rect(rect.x - padding, rect.y - padding, rect.width + padding * 2f, rect.height + padding * 2f);
    }

    private static string GetNoteTechniqueOverlayText(ChartEditorNote note)
    {
        if (note == null)
            return string.Empty;

        List<string> parts = new List<string>();
        bool hasAnySegment = note.techniqueSegments != null && note.techniqueSegments.Any(segment => segment != null);
        bool hasSegmentSlide = HasTechniqueSegment(note, NoteTechniqueSegmentType.Slide);
        bool hasSegmentBend = HasTechniqueSegment(note, NoteTechniqueSegmentType.Bend);
        bool hasSegmentVibrato = HasTechniqueSegment(note, NoteTechniqueSegmentType.Vibrato);
        bool hasSlide = hasSegmentSlide;
        bool hasBend = hasSegmentBend;
        bool hasVibrato = hasSegmentVibrato;

        switch (note.technique)
        {
            case NoteTechnique.HammerOn:
                AddUniqueTechniqueLabel(parts, "H");
                break;
            case NoteTechnique.PullOff:
                AddUniqueTechniqueLabel(parts, "P");
                break;
            case NoteTechnique.Slide:
                if (!hasAnySegment && !hasSegmentSlide)
                    AddUniqueTechniqueLabel(parts, FormatSlideTechniqueLabel(note.fret, note.slideTargetFret));
                hasSlide = true;
                break;
            case NoteTechnique.Bend:
                if (!hasAnySegment && !hasSegmentBend)
                    AddUniqueTechniqueLabel(parts, FormatBendTechniqueLabel(GetMaxBendSemitones(note), note.bendPreBend, note.bendRelease));
                hasBend = true;
                break;
            case NoteTechnique.Vibrato:
                if (!hasAnySegment && !hasSegmentVibrato)
                    AddUniqueTechniqueLabel(parts, "~");
                hasVibrato = true;
                break;
        }

        if (!hasAnySegment && note.slideTargetFret >= 0 && !hasSlide)
        {
            AddUniqueTechniqueLabel(parts, FormatSlideTechniqueLabel(note.fret, note.slideTargetFret));
            hasSlide = true;
        }

        float noteBend = GetMaxBendSemitones(note);
        if (!hasAnySegment && (noteBend > 0.01f || note.bendPreBend || note.bendRelease) && !hasBend)
        {
            AddUniqueTechniqueLabel(parts, FormatBendTechniqueLabel(noteBend, note.bendPreBend, note.bendRelease));
            hasBend = true;
        }

        if (IsFretHandMuteEnabled(note))
            AddUniqueTechniqueLabel(parts, "FHM");
        if (IsPalmMuteEnabled(note))
            AddUniqueTechniqueLabel(parts, "PM");
        if (note.harmonic)
            AddUniqueTechniqueLabel(parts, "HAR");
        if (note.pinchHarmonic)
            AddUniqueTechniqueLabel(parts, "PH");
        if (note.accent)
            AddUniqueTechniqueLabel(parts, "ACC");
        if (note.tap)
            AddUniqueTechniqueLabel(parts, "TAP");
        if (note.tremolo)
            AddUniqueTechniqueLabel(parts, "TR");
        if (note.legato || !note.requiresPluck || note.linkedFromNoteId >= 0)
            AddUniqueTechniqueLabel(parts, "leg");

        return string.Join(" ", parts);
    }

    private static void AddUniqueTechniqueLabel(List<string> parts, string label)
    {
        if (parts == null || string.IsNullOrWhiteSpace(label))
            return;

        string trimmed = label.Trim();
        if (!parts.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
            parts.Add(trimmed);
    }

    private static string FormatSlideTechniqueLabel(int startFret, int targetFret)
    {
        if (targetFret < 0)
            return "SL";

        return targetFret < startFret ? "\\" + targetFret : "/" + targetFret;
    }

    private static string FormatBendTechniqueLabel(float bendSemitones, bool preBend, bool release)
    {
        string amount = FormatBendAmountTitleLabel(bendSemitones);
        if (preBend && release)
            return string.IsNullOrEmpty(amount) ? "Pre Release" : $"Pre {amount} Release";
        if (preBend)
            return string.IsNullOrEmpty(amount) ? "Pre Bend" : $"Pre {amount}";
        if (release)
            return string.IsNullOrEmpty(amount) ? "Release" : $"Release {amount}";
        return string.IsNullOrEmpty(amount) ? "Bend" : $"{amount} Bend";
    }

    private static string FormatBendAmountLabel(float bendSemitones)
    {
        if (bendSemitones <= 0.01f)
            return string.Empty;

        int quarterStepUnits = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(bendSemitones) * 2f));
        switch (quarterStepUnits)
        {
            case 1:
                return "1/4";
            case 2:
                return "1/2";
            case 3:
                return "3/4";
            case 4:
                return "full";
        }

        int wholeSteps = quarterStepUnits / 4;
        int remainder = quarterStepUnits % 4;
        if (remainder == 0)
            return wholeSteps.ToString();

        string fraction;
        switch (remainder)
        {
            case 1:
                fraction = "1/4";
                break;
            case 2:
                fraction = "1/2";
                break;
            default:
                fraction = "3/4";
                break;
        }

        return wholeSteps > 0 ? wholeSteps + " " + fraction : fraction;
    }

    private static string FormatBendAmountTitleLabel(float bendSemitones)
    {
        string amount = FormatBendAmountLabel(bendSemitones);
        switch (amount)
        {
            case "full":
                return "Full";
            case "1/2":
                return "Half";
            case "1/4":
                return "Quarter";
            case "3/4":
                return "3/4";
            default:
                return amount;
        }
    }

    private static float GetMaxBendSemitones(ChartEditorNote note)
    {
        if (note == null)
            return 0f;

        float maxBend = Mathf.Abs(note.bendStep);
        if (note.techniqueSegments != null)
        {
            for (int i = 0; i < note.techniqueSegments.Count; i++)
            {
                ChartEditorTechniqueSegment segment = note.techniqueSegments[i];
                if (segment.type != NoteTechniqueSegmentType.Bend && segment.type != NoteTechniqueSegmentType.Sustain && segment.type != NoteTechniqueSegmentType.Vibrato)
                    continue;

                maxBend = Mathf.Max(maxBend, Mathf.Abs(segment.startBend), Mathf.Abs(segment.endBend));
            }
        }

        return maxBend;
    }

    private void AddStringLaneLabels(
        VisualElement timeline,
        ChartEditorTrack track,
        float rowTop,
        float laneTop,
        int laneCount,
        float laneHeight,
        Color accent)
    {
        string[] labels = GetVisualLaneLabels(track, laneCount);
        for (int lane = 0; lane < laneCount; lane++)
        {
            Color stringColor = GetEditorLaneColor(track, lane, laneCount, accent);
            Label label = CreateLabel(labels[Mathf.Clamp(lane, 0, labels.Length - 1)], 28f, stringColor, true, TextAnchor.MiddleCenter, false);
            label.style.position = Position.Absolute;
            label.style.left = TimelineLabelWidth - 96f;
            label.style.top = rowTop + laneTop + lane * laneHeight + laneHeight * 0.5f - 24f;
            label.style.width = 72f;
            label.style.height = 48f;
            label.pickingMode = PickingMode.Ignore;
            timeline.Add(label);
        }
    }

    private Color GetEditorLaneColor(ChartEditorTrack track, int visualLane, int laneCount, Color fallback)
    {
        if (!IsStringInstrument(track))
            return fallback;

        int stringIndex = GetStringOrLaneFromVisualLane(track, visualLane, laneCount);
        return GetEditorStringColor(stringIndex, fallback);
    }

    private Color GetEditorNoteColor(ChartEditorTrack track, ChartEditorNote note, int laneCount, Color fallback)
    {
        if (!IsStringInstrument(track))
            return fallback;

        int stringIndex = Mathf.Clamp(note?.stringOrLane ?? 0, 0, Math.Max(0, laneCount - 1));
        return GetEditorStringColor(stringIndex, fallback);
    }

    private Color GetEditorStringColor(int stringIndex, Color fallback)
    {
        if (owner != null)
            return owner.GetStringColor(stringIndex);

        Color[] fallbackColors =
        {
            new Color(0.91f, 0.30f, 0.24f, 1f),
            new Color(0.95f, 0.77f, 0.06f, 1f),
            new Color(0.20f, 0.60f, 0.86f, 1f),
            new Color(0.90f, 0.49f, 0.13f, 1f),
            new Color(0.18f, 0.80f, 0.44f, 1f),
            new Color(0.61f, 0.35f, 0.71f, 1f)
        };

        return stringIndex >= 0 && stringIndex < fallbackColors.Length
            ? fallbackColors[stringIndex]
            : fallback;
    }

    private static string[] GetVisualLaneLabels(ChartEditorTrack track, int laneCount)
    {
        if (track?.role == ChartEditorTrackRole.Drums)
            return new[] { "Kick", "Sn", "Hat", "T1", "T2", "Ride", "Cr", "Perc" };

        int[] pitches = track?.tuning?.stringPitches;
        bool bass = track?.role == ChartEditorTrackRole.Bass;
        if (pitches == null || pitches.Length == 0)
            pitches = bass ? StringTuningUtils.StandardBassTuning : StringTuningUtils.StandardGuitarTuning;

        string[] labels = new string[laneCount];
        for (int visualLane = 0; visualLane < laneCount; visualLane++)
        {
            int stringIndex = laneCount - 1 - visualLane;
            if (stringIndex >= 0 && stringIndex < pitches.Length)
                labels[visualLane] = FormatMidiPitchNoOctave(pitches[stringIndex], lowercaseHighE: pitches[stringIndex] >= 60);
            else
                labels[visualLane] = (laneCount - visualLane).ToString();
        }

        return labels;
    }

    private static int GetVisualLaneForNote(ChartEditorTrack track, ChartEditorNote note, int laneCount)
    {
        int rawLane = Mathf.Clamp(note?.stringOrLane ?? 0, 0, Math.Max(0, laneCount - 1));
        return IsStringInstrument(track) ? laneCount - 1 - rawLane : rawLane;
    }

    private static int GetStringOrLaneFromVisualLane(ChartEditorTrack track, int visualLane, int laneCount)
    {
        int clampedVisualLane = Mathf.Clamp(visualLane, 0, Math.Max(0, laneCount - 1));
        return IsStringInstrument(track) ? laneCount - 1 - clampedVisualLane : clampedVisualLane;
    }

    private static bool IsStringInstrument(ChartEditorTrack track)
    {
        return track == null ||
               track.role == ChartEditorTrackRole.LeadGuitar ||
               track.role == ChartEditorTrackRole.RhythmGuitar ||
               track.role == ChartEditorTrackRole.Bass ||
               track.role == ChartEditorTrackRole.Custom;
    }

    private static int GetTrackLaneCount(ChartEditorTrack track)
    {
        int count = track?.role == ChartEditorTrackRole.Bass ? 4 :
            (track?.role == ChartEditorTrackRole.Drums || track?.role == ChartEditorTrackRole.Piano) ? 8 : 6;

        if (track?.tuning?.stringPitches != null && track.tuning.stringPitches.Length > 0)
            count = Mathf.Max(count, track.tuning.stringPitches.Length);

        if (track?.notes != null)
        {
            for (int i = 0; i < track.notes.Count; i++)
            {
                ChartEditorNote note = track.notes[i];
                if (note != null)
                    count = Mathf.Max(count, note.stringOrLane + 1);
            }
        }

        return Mathf.Clamp(count, 1, 8);
    }

    private static string FormatMidiPitchNoOctave(int midi, bool lowercaseHighE = false)
    {
        string[] names = { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };
        int pitchClass = midi % 12;
        if (pitchClass < 0)
            pitchClass += 12;

        string name = names[pitchClass];
        return lowercaseHighE && string.Equals(name, "E", StringComparison.OrdinalIgnoreCase) ? "e" : name;
    }

    private float TimeToPixels(double seconds)
    {
        return Mathf.Max(0f, (float)seconds * GetTimelinePixelsPerSecond());
    }

    private double PixelsToSeconds(float pixels)
    {
        return Math.Max(0.0, pixels / GetTimelinePixelsPerSecond());
    }

    private double PixelDeltaToSeconds(float pixels)
    {
        return pixels / GetTimelinePixelsPerSecond();
    }

    private float GetTimelinePixelsPerSecond()
    {
        return BaseTimelinePixelsPerSecond * Mathf.Clamp(timelineZoom, MinTimelineZoom, MaxTimelineZoom);
    }

    private double SnapTime(double seconds)
    {
        double clamped = Math.Max(0.0, Math.Min(project.DurationSeconds, seconds));
        double snap = project?.settings?.snapEnabled == true ? Math.Max(0.001, project.settings.snapSeconds) : 0.0;
        return snap > 0.0 ? Math.Round(clamped / snap) * snap : clamped;
    }

    private static Vector2 PointerPosition(IPointerEvent evt)
    {
        return new Vector2(evt.position.x, evt.position.y);
    }

    private void AddSeekDragHandlers(VisualElement target, Func<Vector2, float> timelinePixelFromWorld)
    {
        bool dragging = false;
        bool clearedSelection = false;
        int pointerId = -1;

        target.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || project == null)
                return;

            dragging = true;
            seekDragging = true;
            seekWasPlaying = editorPlaying;
            clearedSelection = HasTimelineSelection();
            if (clearedSelection)
            {
                ClearTimelineSelectionState();
                mode = ChartEditorMode.SyncTiming;
            }

            pointerId = evt.pointerId;
            SeekToTimelinePixel(timelinePixelFromWorld(PointerPosition(evt)));
            target.CapturePointer(pointerId);
            evt.StopImmediatePropagation();
        });

        target.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            Vector2 pointer = PointerPosition(evt);
            PanTimelineDuringSeekDrag(pointer);
            SeekToTimelinePixel(timelinePixelFromWorld(pointer));
            evt.StopImmediatePropagation();
        });

        target.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            dragging = false;
            if (target.HasPointerCapture(pointerId))
                target.ReleasePointer(pointerId);
            SeekToTimelinePixel(timelinePixelFromWorld(PointerPosition(evt)));
            seekDragging = false;
            if (seekWasPlaying)
            {
                editorPlaying = true;
                SyncEditorAudioToCursor(playImmediately: true);
            }

            seekWasPlaying = false;
            if (clearedSelection)
            {
                clearedSelection = false;
                Rebuild();
            }

            evt.StopImmediatePropagation();
        });
    }

    private void SeekToTimelinePixel(float timelinePixel)
    {
        SetCursorTime(PixelsToSeconds(Mathf.Max(0f, timelinePixel)), rebuild: false, syncAudio: true);
    }

    private void PanTimelineDuringSeekDrag(Vector2 pointerWorldPosition)
    {
        if (currentTimelineScrollView?.contentViewport == null)
            return;

        float viewportWidth = GetTimelineViewportWidth();
        if (viewportWidth <= 1f)
            return;

        Vector2 pointerInViewport = currentTimelineScrollView.contentViewport.WorldToLocal(pointerWorldPosition);
        float edgeZone = Mathf.Min(SeekDragEdgePanZone, viewportWidth * 0.35f);
        if (edgeZone <= 1f)
            return;

        float panPixels = 0f;
        if (pointerInViewport.x < edgeZone)
        {
            float strength = Mathf.Clamp01((edgeZone - pointerInViewport.x) / edgeZone);
            panPixels = -Mathf.Lerp(SeekDragEdgePanMinPixels, SeekDragEdgePanMaxPixels, strength);
        }
        else if (pointerInViewport.x > viewportWidth - edgeZone)
        {
            float strength = Mathf.Clamp01((pointerInViewport.x - (viewportWidth - edgeZone)) / edgeZone);
            panPixels = Mathf.Lerp(SeekDragEdgePanMinPixels, SeekDragEdgePanMaxPixels, strength);
        }

        if (Mathf.Abs(panPixels) > 0.01f)
            ApplyTimelineScrollX(timelineScrollOffset.x + panPixels);
    }

    private void AddNoteDragHandlers(VisualElement block, ChartEditorTrack track, ChartEditorNote note, int laneCount, float laneHeight, float laneTop, bool selectedTrack, float noteHeight)
    {
        bool dragging = false;
        bool moved = false;
        int pointerId = -1;
        Vector2 startPointer = Vector2.zero;
        double startTime = 0.0;
        double pendingTimeDelta = 0.0;
        int pendingVisualLaneDelta = 0;
        List<ChartEditorNoteDragStart> dragStarts = new List<ChartEditorNoteDragStart>();

        block.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || note == null || track == null)
                return;

            if (!IsNoteSelected(note))
                SelectSingleNote(track, note);
            else
            {
                selectedNoteId = note.id;
                selectedSectionId = null;
                selectedSyncPointId = null;
                project.selectedTrackId = track.id;
                mode = ChartEditorMode.Notes;
            }

            List<ChartEditorNoteReference> selectedNotes = GetSelectedNoteReferences();
            if (selectedNotes.Count == 0)
                selectedNotes.Add(new ChartEditorNoteReference { track = track, note = note });

            dragStarts = selectedNotes
                .Where(noteRef => noteRef?.track != null && noteRef.note != null)
                .Select(noteRef =>
                {
                    int noteLaneCount = GetTrackLaneCount(noteRef.track);
                    currentNoteBlocks.TryGetValue(noteRef.note.id ?? string.Empty, out VisualElement noteBlock);
                    int visualLane = GetVisualLaneForNote(noteRef.track, noteRef.note, noteLaneCount);
                    bool noteSelectedTrack = selectedTrack && string.Equals(noteRef.track.id, track.id, StringComparison.OrdinalIgnoreCase);
                    float noteTop = noteSelectedTrack
                        ? GetNoteTopForLane(laneTop, visualLane, laneHeight, noteHeight)
                        : 44f;
                    return new ChartEditorNoteDragStart
                    {
                        track = noteRef.track,
                        note = noteRef.note,
                        block = noteBlock,
                        timeSeconds = noteRef.note.timeSeconds,
                        chartTimeSeconds = noteRef.note.chartTimeSeconds,
                        visualLane = visualLane,
                        laneCount = noteLaneCount,
                        laneTop = laneTop,
                        laneHeight = laneHeight,
                        noteHeight = noteHeight,
                        left = TimeToPixels(noteRef.note.timeSeconds),
                        top = noteTop,
                        selectedTrack = noteSelectedTrack
                    };
                })
                .ToList();

            for (int i = 0; i < dragStarts.Count; i++)
                dragStarts[i].block?.BringToFront();

            dragging = true;
            moved = false;
            pointerId = evt.pointerId;
            startPointer = PointerPosition(evt);
            startTime = note.timeSeconds;
            pendingTimeDelta = 0.0;
            pendingVisualLaneDelta = 0;
            block.CapturePointer(pointerId);
            evt.StopImmediatePropagation();
        });

        block.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            Vector2 pointer = PointerPosition(evt);
            Vector2 delta = pointer - startPointer;
            if (Mathf.Abs(delta.x) > 1f || Mathf.Abs(delta.y) > 1f)
                moved = true;

            double requestedTimeDelta = PixelDeltaToSeconds(delta.x);
            double minTimeDelta = dragStarts.Count == 0 ? -startTime : -dragStarts.Min(start => start.timeSeconds);
            double maxTimeDelta = dragStarts.Count == 0 ? project.DurationSeconds - startTime : project.DurationSeconds - dragStarts.Max(start => start.timeSeconds);
            pendingTimeDelta = Math.Max(minTimeDelta, Math.Min(maxTimeDelta, requestedTimeDelta));
            pendingVisualLaneDelta = selectedTrack ? Mathf.RoundToInt(delta.y / Mathf.Max(1f, laneHeight)) : 0;
            float pixelDelta = (float)pendingTimeDelta * GetTimelinePixelsPerSecond();
            for (int i = 0; i < dragStarts.Count; i++)
            {
                ChartEditorNoteDragStart start = dragStarts[i];
                if (start.block != null)
                {
                    int visualLane = Mathf.Clamp(start.visualLane + pendingVisualLaneDelta, 0, Math.Max(0, start.laneCount - 1));
                    start.block.style.left = Mathf.Max(0f, start.left + pixelDelta);
                    start.block.style.top = start.selectedTrack
                        ? GetNoteTopForLane(start.laneTop, visualLane, start.laneHeight, start.noteHeight)
                        : start.top;
                    start.block.BringToFront();
                }
            }

            project.dirty = true;
            evt.StopImmediatePropagation();
        });

        block.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            dragging = false;
            if (block.HasPointerCapture(pointerId))
                block.ReleasePointer(pointerId);

            if (moved)
            {
                for (int i = 0; i < dragStarts.Count; i++)
                {
                    ChartEditorNoteDragStart start = dragStarts[i];
                    double targetTime = Math.Max(0.0, Math.Min(project.DurationSeconds, start.timeSeconds + pendingTimeDelta));
                    double appliedDelta = targetTime - start.timeSeconds;
                    start.note.timeSeconds = targetTime;
                    start.note.chartTimeSeconds = Math.Max(0.0, start.chartTimeSeconds + appliedDelta);
                    if (start.selectedTrack)
                    {
                        int visualLane = Mathf.Clamp(start.visualLane + pendingVisualLaneDelta, 0, Math.Max(0, start.laneCount - 1));
                        start.note.stringOrLane = GetStringOrLaneFromVisualLane(start.track, visualLane, start.laneCount);
                    }
                    ChartEditorTimingService.UpdateNoteBeatTiming(project, start.note);
                }

                foreach (ChartEditorTrack dirtyTrack in dragStarts.Select(start => start.track).Where(dirtyTrack => dirtyTrack != null).Distinct())
                    dirtyTrack.notes = dirtyTrack.notes?.OrderBy(n => n?.timeSeconds ?? 0.0).ThenBy(n => n?.stringOrLane ?? 0).ToList() ?? new List<ChartEditorNote>();
            }

            if (moved)
            {
                evt.StopImmediatePropagation();
                block.schedule.Execute(Rebuild);
            }
            else
            {
                block.schedule.Execute(Rebuild);
                evt.StopImmediatePropagation();
            }
        });
    }

    private void AddSectionDragHandlers(Button block, ChartEditorSection section)
    {
        bool dragging = false;
        bool moved = false;
        int pointerId = -1;
        Vector2 startPointer = Vector2.zero;
        double startTime = 0.0;
        double startEnd = 0.0;
        double startChart = 0.0;
        double startChartEnd = 0.0;

        block.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || section == null)
                return;

            dragging = true;
            moved = false;
            pointerId = evt.pointerId;
            startPointer = PointerPosition(evt);
            startTime = section.startTimeSeconds;
            startEnd = section.endTimeSeconds;
            startChart = section.chartStartTimeSeconds;
            startChartEnd = section.chartEndTimeSeconds;
            selectedSectionId = section.id;
            ClearNoteSelection();
            selectedSyncPointId = null;
            mode = ChartEditorMode.Sections;
            block.CapturePointer(pointerId);
            evt.StopPropagation();
        });

        block.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            Vector2 delta = PointerPosition(evt) - startPointer;
            if (Mathf.Abs(delta.x) > 1f)
                moved = true;

            double newStart = SnapTime(startTime + PixelDeltaToSeconds(delta.x));
            double timeDelta = newStart - startTime;
            section.startTimeSeconds = newStart;
            section.endTimeSeconds = Math.Max(section.startTimeSeconds + 0.05, startEnd + timeDelta);
            section.chartStartTimeSeconds = Math.Max(0.0, startChart + timeDelta);
            section.chartEndTimeSeconds = Math.Max(section.chartStartTimeSeconds + 0.05, startChartEnd + timeDelta);
            section.userEdited = true;
            project.cursorTimeSeconds = section.startTimeSeconds;
            project.dirty = true;
            block.style.left = TimeToPixels(section.startTimeSeconds);
            block.style.width = Mathf.Max(90f, TimeToPixels(section.endTimeSeconds) - TimeToPixels(section.startTimeSeconds));
            evt.StopPropagation();
        });

        block.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            dragging = false;
            if (block.HasPointerCapture(pointerId))
                block.ReleasePointer(pointerId);

            ChartEditorTimingService.NormalizeSections(project);
            if (moved)
            {
                ChartEditorTimingService.UpdateSectionBeatTiming(project, section);
                evt.StopImmediatePropagation();
                block.schedule.Execute(Rebuild);
            }
        });
    }

    private void AddSyncPointDragHandlers(VisualElement marker, ChartEditorBeatMarker point)
    {
        bool dragging = false;
        bool moved = false;
        int pointerId = -1;
        Vector2 startPointer = Vector2.zero;
        double startAudio = 0.0;
        double minAudio = 0.0;
        double maxAudio = 0.0;

        marker.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button == 2 && point != null)
            {
                ToggleBeatMarkerAnchor(point);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0 || point == null)
                return;
            if (evt.ctrlKey)
            {
                ToggleAnchorSelection(point);
                marker.schedule.Execute(Rebuild);
                evt.StopImmediatePropagation();
                return;
            }

            if (point.locked)
            {
                SelectSingleAnchor(point);
                Rebuild();
                evt.StopPropagation();
                return;
            }

            dragging = true;
            moved = false;
            pointerId = evt.pointerId;
            startPointer = PointerPosition(evt);
            startAudio = point.audioTimeSeconds;
            ResolveAnchorDragBounds(point, out minAudio, out maxAudio);
            if (!IsAnchorSelected(point) || GetSelectedAnchorCount() <= 1)
                SelectSingleAnchor(point);
            else
                selectedSyncPointId = point.id;
            marker.CapturePointer(pointerId);
            evt.StopPropagation();
        });

        marker.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            Vector2 delta = PointerPosition(evt) - startPointer;
            if (Mathf.Abs(delta.x) > 1f)
                moved = true;

            double newAudio = startAudio + PixelDeltaToSeconds(delta.x);
            point.audioTimeSeconds = Math.Max(minAudio, Math.Min(maxAudio, newAudio));
            project.cursorTimeSeconds = point.audioTimeSeconds;
            project.dirty = true;
            marker.style.left = TimelineLabelWidth + TimeToPixels(point.audioTimeSeconds);
            evt.StopPropagation();
        });

        marker.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            dragging = false;
            if (marker.HasPointerCapture(pointerId))
                marker.ReleasePointer(pointerId);

            if (moved)
            {
                ChartEditorTimingService.MoveAnchor(project, point, point.audioTimeSeconds);
                evt.StopImmediatePropagation();
                marker.schedule.Execute(Rebuild);
            }
            else
            {
                if (!IsAnchorSelected(point) || GetSelectedAnchorCount() <= 1)
                    SelectSingleAnchor(point);
                else
                    selectedSyncPointId = point.id;
                evt.StopPropagation();
                marker.schedule.Execute(Rebuild);
            }
        });
    }

    private void ResolveAnchorDragBounds(ChartEditorBeatMarker point, out double minAudio, out double maxAudio)
    {
        minAudio = 0.0;
        maxAudio = project?.DurationSeconds ?? 0.0;
        if (project == null || point == null)
            return;

        List<ChartEditorBeatMarker> anchors = ChartEditorTimingService.GetAnchors(project);
        int index = anchors.FindIndex(anchor => anchor != null && string.Equals(anchor.id, point.id, StringComparison.OrdinalIgnoreCase));
        if (index > 0)
            minAudio = anchors[index - 1].audioTimeSeconds + 0.02;
        if (index >= 0 && index + 1 < anchors.Count)
            maxAudio = anchors[index + 1].audioTimeSeconds - 0.02;
        if (maxAudio < minAudio)
            maxAudio = minAudio;
    }

    private void BuildTrackInspector(VisualElement panel, ChartEditorTrack track)
    {
        panel.Add(CreateSectionTitle("Track"));
        if (track == null)
        {
            panel.Add(CreateSmallText("No track selected.", new Color(0.78f, 0.84f, 0.90f, 0.95f)));
            return;
        }

        TextField nameField = CreateTextField("Name", track.displayName, value =>
        {
            track.displayName = value;
            project.dirty = true;
        });
        panel.Add(nameField);
        panel.Add(CreateKeyValue("Role", track.role.ToString()));
        panel.Add(CreateKeyValue("Tuning", FirstNonEmpty(track.tuning?.displayName, "Unknown")));
        panel.Add(CreateKeyValue("Notes", (track.notes?.Count ?? 0).ToString()));
        panel.Add(CreateToggleButton("Visible", track.visible, () => { track.visible = !track.visible; project.dirty = true; Rebuild(); }));
        panel.Add(CreateToggleButton("Muted", track.muted, () => { track.muted = !track.muted; project.dirty = true; Rebuild(); }));
        panel.Add(CreateToggleButton("Solo", track.solo, () => { track.solo = !track.solo; project.dirty = true; Rebuild(); }));
    }

    private void BuildNoteInspector(VisualElement panel, ChartEditorNote note)
    {
        panel.Add(CreateSectionTitle("Selected Note"));
        panel.Add(CreateKeyValue("Time", FormatTime(note.timeSeconds)));
        panel.Add(CreateKeyValue("Duration", $"{GetNoteEffectiveDurationSeconds(note) * 1000.0:F0} ms"));
        panel.Add(CreateKeyValue("String/Lane", note.stringOrLane.ToString()));
        panel.Add(CreateKeyValue("Fret", note.fret.ToString()));
        panel.Add(CreateKeyValue("Techniques", FirstNonEmpty(GetNoteTechniqueOverlayText(note), "None")));
        panel.Add(CreateCompactRow(
            CreateCompactButton("-10ms", () => NudgeNote(note, -0.01)),
            CreateCompactButton("+10ms", () => NudgeNote(note, 0.01))));
        panel.Add(CreateCompactRow(
            CreateCompactButton("-100ms", () => NudgeNote(note, -0.1)),
            CreateCompactButton("+100ms", () => NudgeNote(note, 0.1))));
        panel.Add(CreateCompactRow(
            CreateCompactButton("Fret -", () => ChangeNoteFret(note, -1)),
            CreateCompactButton("Fret +", () => ChangeNoteFret(note, 1))));
        panel.Add(CreateCompactRow(
            CreateCompactButton("Lane -", () => ChangeNoteLane(note, -1)),
            CreateCompactButton("Lane +", () => ChangeNoteLane(note, 1))));
        panel.Add(CreateCompactRow(
            CreateCompactButton("Duplicate", () => DuplicateNote(note)),
            CreateCompactButton("Delete", () => DeleteNote(note))));
        panel.Add(CreateTechniqueButton(note, NoteTechnique.HammerOn));
        panel.Add(CreateTechniqueButton(note, NoteTechnique.PullOff));
        panel.Add(CreateTechniqueButton(note, NoteTechnique.Slide));
        panel.Add(CreateTechniqueButton(note, NoteTechnique.Bend));
        panel.Add(CreateTechniqueButton(note, NoteTechnique.Vibrato));
        panel.Add(CreateToggleButton("Palm Mute", IsPalmMuteEnabled(note), () =>
        {
            SetPalmMute(note, !IsPalmMuteEnabled(note));
            project.dirty = true;
            Rebuild();
        }));
        panel.Add(CreateToggleButton("Fret-Hand Mute", IsFretHandMuteEnabled(note), () =>
        {
            SetFretHandMute(note, !IsFretHandMuteEnabled(note));
            project.dirty = true;
            Rebuild();
        }));
        panel.Add(CreateToggleButton("Natural Harmonic", note.harmonic, () =>
        {
            note.harmonic = !note.harmonic;
            if (note.harmonic)
                note.pinchHarmonic = false;
            project.dirty = true;
            Rebuild();
        }));
        panel.Add(CreateToggleButton("Pinch Harmonic", note.pinchHarmonic, () =>
        {
            note.pinchHarmonic = !note.pinchHarmonic;
            if (note.pinchHarmonic)
                note.harmonic = false;
            project.dirty = true;
            Rebuild();
        }));
        panel.Add(CreateToggleButton("Accent", note.accent, () =>
        {
            note.accent = !note.accent;
            project.dirty = true;
            Rebuild();
        }));
        panel.Add(CreateToggleButton("Tap", note.tap, () =>
        {
            note.tap = !note.tap;
            project.dirty = true;
            Rebuild();
        }));
        panel.Add(CreateToggleButton("Tremolo", note.tremolo, () =>
        {
            note.tremolo = !note.tremolo;
            project.dirty = true;
            Rebuild();
        }));
    }

    private void BuildSectionInspector(VisualElement panel, ChartEditorSection section)
    {
        panel.Add(CreateSectionTitle("Section"));
        panel.Add(CreateTextField("Name", section.name, value =>
        {
            section.name = value;
            section.userEdited = true;
            project.dirty = true;
        }));
        panel.Add(CreateKeyValue("Start", FormatTime(section.startTimeSeconds)));
        panel.Add(CreateKeyValue("End", FormatTime(section.endTimeSeconds)));
        panel.Add(CreateCompactRow(
            CreateCompactButton("Start -100ms", () => NudgeSectionStart(section, -0.1)),
            CreateCompactButton("Start +100ms", () => NudgeSectionStart(section, 0.1))));
        panel.Add(CreateCompactRow(
            CreateCompactButton("End -100ms", () => NudgeSectionEnd(section, -0.1)),
            CreateCompactButton("End +100ms", () => NudgeSectionEnd(section, 0.1))));
        panel.Add(CreateCompactButton("Delete Section", () =>
        {
            DeleteSection(section);
        }));
    }

    private void BuildSyncInspector(VisualElement panel, ChartEditorBeatMarker point)
    {
        panel.Add(CreateSectionTitle("Anchor"));
        panel.Add(CreateTextField("Name", point.label, value =>
        {
            point.label = value;
            project.dirty = true;
        }));
        panel.Add(CreateKeyValue("Bar / Beat", $"{point.barNumber}.{point.beatInBar}"));
        panel.Add(CreateKeyValue("Beat Position", point.beatPosition.ToString("0.###", CultureInfo.InvariantCulture)));
        panel.Add(CreateKeyValue("Audio Time", FormatTime(point.audioTimeSeconds)));
        panel.Add(CreateKeyValue("Tempo Here", $"{ChartEditorTimingService.GetTempoAtBeat(project, point.beatPosition):0.###} BPM"));
        ChartEditorTimeSignatureChange signature = ChartEditorTimingService.GetTimeSignatureAtBeat(project, point.beatPosition);
        panel.Add(CreateKeyValue("Time Signature", $"{Math.Max(1, signature?.numerator ?? 4)}/{Math.Max(1, signature?.denominator ?? 4)}"));
        panel.Add(CreateCompactRow(
            CreateCompactButton("Audio -50ms", () => NudgeSyncPoint(point, -0.05)),
            CreateCompactButton("Audio +50ms", () => NudgeSyncPoint(point, 0.05))));
        panel.Add(CreateCompactRow(
            CreateCompactButton("Audio -250ms", () => NudgeSyncPoint(point, -0.25)),
            CreateCompactButton("Audio +250ms", () => NudgeSyncPoint(point, 0.25))));
        panel.Add(CreateCompactRow(
            CreateCompactButton("Set BPM", () => ShowSetRegionBpmPopup(point.beatPosition)),
            CreateCompactButton("Time Sig", () => ShowTimeSignaturePopup(point.beatPosition))));
        panel.Add(CreateCompactButton("Beat Map Settings", ShowBeatMapSettingsPopup));
        panel.Add(CreateToggleButton("Locked", point.locked, () =>
        {
            point.locked = !point.locked;
            project.dirty = true;
            Rebuild();
        }));
        panel.Add(CreateCompactButton("Delete Anchor", () =>
        {
            ChartEditorTimingService.RemoveAnchor(project, point);
            ClearAnchorSelection();
            project.dirty = true;
            Rebuild();
        }));
    }

    private void BuildSongInfoInspector(VisualElement panel)
    {
        panel.Add(CreateSectionTitle("Song Info"));
        panel.Add(CreateTextField("Title", project.metadata.title, value => { project.metadata.title = value; project.dirty = true; }));
        panel.Add(CreateTextField("Artist", project.metadata.artist, value => { project.metadata.artist = value; project.dirty = true; }));
        panel.Add(CreateTextField("Album", project.metadata.album, value => { project.metadata.album = value; project.dirty = true; }));
        panel.Add(CreateTextField("Genre", project.metadata.genre, value => { project.metadata.genre = value; project.dirty = true; }));
        panel.Add(CreateTextField("Year", project.metadata.year, value => { project.metadata.year = value; project.dirty = true; }));
        panel.Add(CreateKeyValue("Audio", string.IsNullOrWhiteSpace(project.audio?.displayName) ? "None" : project.audio.displayName));
        panel.Add(CreateKeyValue("Source", project.sourceKind.ToString()));
    }

    private void ImportChartAndAudio()
    {
        if (!ChartEditorFilePicker.TryPickChartFile(out string chartPath))
            return;

        if (TheoryPackageFormat.IsPackagePath(chartPath))
        {
            ImportTheoryPackage(chartPath);
            return;
        }

        if (!ChartEditorFilePicker.TryPickAudioFile(out string audioPath))
            audioPath = string.Empty;

        if (ChartEditorImportService.ImportChartAndAudio(chartPath, audioPath, out ChartEditorImportResult result, out string error))
            AcceptImport(result, "Chart imported.");
        else
            SetStatus(error);
    }

    private void ImportTheoryPackage()
    {
        if (!ChartEditorFilePicker.TryPickTheoryPackageFile(out string packagePath))
            return;

        ImportTheoryPackage(packagePath);
    }

    private void ImportTheoryPackage(string packagePath)
    {
        if (ChartEditorImportService.ImportTheoryPackage(packagePath, out ChartEditorImportResult result, out string error))
            AcceptImport(result, "Theory package imported.");
        else
            SetStatus(error);
    }

    private void ImportPsarc()
    {
        if (!ChartEditorFilePicker.TryPickPsarcFile(out string psarcPath))
            return;

        SetStatus("Importing PSARC...");
        if (ChartEditorImportService.ImportPsarc(psarcPath, out ChartEditorImportResult result, out string error))
            AcceptImport(result, "PSARC imported.");
        else
            SetStatus(error);
    }

    private void ImportFolder()
    {
        if (!ChartEditorFilePicker.TryPickFolder("Open Unpacked Chart Folder", ExternalContentPaths.PersistentSongsDirectory, out string folderPath))
            return;

        if (ChartEditorImportService.ImportFolder(folderPath, out ChartEditorImportResult result, out string error))
            AcceptImport(result, "Folder imported.");
        else
            SetStatus(error);
    }

    private void OpenExistingProject()
    {
        if (!ChartEditorFilePicker.TryPickProjectFile(out string path))
            return;

        if (ChartEditorProjectStore.LoadProject(path, out ChartEditorProject loaded, out string error))
        {
            project = loaded;
            int originalTrackCount = project.tracks?.Count ?? 0;
            ChartEditorImportService.KeepFullDifficultyTracksOnly(project);
            ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);
            if ((project.tracks?.Count ?? 0) < originalTrackCount)
                project.dirty = true;
            timelineZoom = ResolveDefaultTimelineZoom(project);
            EnsureSingleVisibleTrack(markDirty: false);
            currentWarnings = ChartEditorValidationService.BuildWarnings(project);
            ClearNoteSelection();
            noteClipboard.Clear();
            selectedSectionId = null;
            selectedSyncPointId = null;
            timelineScrollOffset = Vector2.zero;
            timelineScrollInitialized = false;
            skipTimelineScrollCaptureOnce = true;
            screen = ChartEditorScreen.Editor;
            Rebuild();
            SetStatus("Project opened.");
        }
        else
        {
            SetStatus(error);
        }
    }

    private void AcceptImport(ChartEditorImportResult result, string status)
    {
        project = result?.project;
        project?.EnsureDefaults();
        ChartEditorImportService.KeepFullDifficultyTracksOnly(project);
        ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);
        timelineZoom = ResolveDefaultTimelineZoom(project);
        EnsureSingleVisibleTrack(markDirty: false);
        currentWarnings = result?.warnings ?? ChartEditorValidationService.BuildWarnings(project);
        ClearNoteSelection();
        noteClipboard.Clear();
        selectedSectionId = null;
        selectedSyncPointId = null;
        timelineScrollOffset = Vector2.zero;
        timelineScrollInitialized = false;
        skipTimelineScrollCaptureOnce = true;
        screen = ChartEditorScreen.Editor;
        Rebuild();
        SetStatus(status);
    }

    private void ShowSaveOptionsPopup()
    {
        if (project == null)
            return;

        HideContextMenu();
        HideEditPopup();

        VisualElement overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.right = 0f;
        overlay.style.top = 0f;
        overlay.style.bottom = 0f;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.34f);
        overlay.pickingMode = PickingMode.Position;
        overlay.RegisterCallback<PointerDownEvent>(evt =>
        {
            HideEditPopup();
            evt.StopPropagation();
        });

        VisualElement panel = new VisualElement();
        panel.style.width = 760f;
        panel.style.paddingLeft = 34f;
        panel.style.paddingRight = 34f;
        panel.style.paddingTop = 32f;
        panel.style.paddingBottom = 32f;
        StylePopupPanel(panel, new Color(0.030f, 0.036f, 0.048f, 1f), 18f);
        panel.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

        string saveFolder = ResolveTheoryPackageSaveDirectoryForPopup(saveAs: false);
        string newPackageFolder = ResolveTheoryPackageSaveDirectoryForPopup(saveAs: true);

        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.marginBottom = 22f;

        Label title = CreateLabel("Save Chart", 36f, Color.white, true, TextAnchor.MiddleLeft, false);
        header.Add(title);

        Button openFolder = CreateCompactButton("Open Save Folder", () => OpenTheoryPackageSaveFolder(saveFolder));
        openFolder.style.minWidth = 218f;
        openFolder.style.height = 50f;
        openFolder.style.fontSize = UiFont(18f);
        openFolder.style.marginLeft = 18f;
        openFolder.style.marginRight = 0f;
        header.Add(openFolder);
        panel.Add(header);

        VisualElement choices = new VisualElement();
        choices.style.flexDirection = FlexDirection.Column;
        choices.style.marginTop = 4f;

        Button save = CreateSaveOptionButton(
            "Save .theory",
            BuildSavedUnderSubtitle(saveFolder),
            () => SaveTheoryPackage(saveAs: false));
        Button saveAs = CreateSaveOptionButton(
            "Save As New .theory",
            BuildSavedUnderSubtitle(newPackageFolder),
            () => SaveTheoryPackage(saveAs: true));
        choices.Add(save);
        choices.Add(saveAs);
        panel.Add(choices);

        VisualElement footer = new VisualElement();
        footer.style.flexDirection = FlexDirection.Row;
        footer.style.justifyContent = Justify.FlexEnd;
        footer.style.marginTop = 20f;
        footer.Add(CreateCompactButton("Cancel", HideEditPopup));
        panel.Add(footer);

        overlay.Add(panel);
        editPopupElement = overlay;
        RootElement.Add(editPopupElement);
        editPopupElement.BringToFront();
        SetChartEditorKeyboardCaptureActive(true);
    }

    private Button CreateSaveOptionButton(string title, string subtitle, Action action)
    {
        Button button = new Button(action) { text = string.Empty };
        button.focusable = false;
        button.style.width = Length.Percent(100f);
        button.style.minHeight = 96f;
        button.style.marginBottom = 14f;
        button.style.paddingLeft = 24f;
        button.style.paddingRight = 22f;
        button.style.paddingTop = 18f;
        button.style.paddingBottom = 18f;
        button.style.flexDirection = FlexDirection.Column;
        button.style.alignItems = Align.FlexStart;
        button.style.justifyContent = Justify.Center;
        button.style.unityFontDefinition = bodyFont;
        SetRadius(button, 12f);
        SetBorderWidth(button, 1f);
        ApplySaveOptionButtonState(button, false);
        button.RegisterCallback<MouseEnterEvent>(_ => ApplySaveOptionButtonState(button, true));
        button.RegisterCallback<MouseLeaveEvent>(_ => ApplySaveOptionButtonState(button, false));

        VisualElement textColumn = new VisualElement();
        textColumn.style.flexGrow = 1f;
        textColumn.style.minWidth = 0f;
        Label titleLabel = CreateLabel(title, 25f, Color.white, true, TextAnchor.MiddleLeft, false);
        titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        Label subtitleLabel = CreateLabel(subtitle, 19f, new Color(0.70f, 0.78f, 0.90f, 0.96f), false, TextAnchor.MiddleLeft, false);
        subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
        subtitleLabel.style.marginTop = 5f;
        textColumn.Add(titleLabel);
        textColumn.Add(subtitleLabel);
        button.Add(textColumn);
        return button;
    }

    private static void ApplySaveOptionButtonState(Button button, bool hover)
    {
        if (button == null)
            return;

        button.style.backgroundColor = hover
            ? new Color(0.070f, 0.080f, 0.104f, 0.98f)
            : new Color(0.044f, 0.052f, 0.068f, 0.96f);
        SetBorderColor(button, hover
            ? new Color(0.42f, 0.32f, 0.66f, 0.96f)
            : new Color(0.18f, 0.22f, 0.30f, 0.96f));
        button.style.opacity = hover ? 1f : 0.98f;
        button.style.scale = hover ? new Scale(new Vector3(1.01f, 1.01f, 1f)) : new Scale(Vector3.one);
    }

    private string ResolveTheoryPackageSaveDirectoryForPopup(bool saveAs)
    {
        if (!saveAs &&
            project?.sourceKind == ChartEditorSourceKind.TheoryPackage &&
            TheoryPackageFormat.IsPackagePath(project.sourcePath) &&
            ChartEditorProjectStore.CanSaveCurrentTheoryPackageInPlace(project))
        {
            string currentDirectory = Path.GetDirectoryName(project.sourcePath);
            if (!string.IsNullOrWhiteSpace(currentDirectory))
                return currentDirectory;
        }

        try
        {
            return ChartEditorProjectStore.GetTheoryPackageSaveDirectory(project, createDirectory: false);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ChartEditor] Could not resolve .theory save folder: {ex.Message}");
            return string.Empty;
        }
    }

    private static string BuildSavedUnderSubtitle(string folderPath)
    {
        return $"Saved under {BuildFolderBreadcrumb(folderPath)}";
    }

    private static string BuildFolderBreadcrumb(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return "song folder";

        string trimmed = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string leaf = Path.GetFileName(trimmed);
        if (string.IsNullOrWhiteSpace(leaf))
            return trimmed;

        string parent = Path.GetFileName(Path.GetDirectoryName(trimmed) ?? string.Empty);
        return string.IsNullOrWhiteSpace(parent) ? leaf : $"{parent} > {leaf}";
    }

    private void OpenTheoryPackageSaveFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            folderPath = ResolveTheoryPackageSaveDirectoryForPopup(saveAs: false);

        if (StringTheoryPlatform.TryOpenFolder(folderPath, out string error))
        {
            SetStatus("Opened save folder.");
            return;
        }

        SetStatus(error);
    }

    private void SaveTheoryPackage(bool saveAs)
    {
        if (ChartEditorProjectStore.SaveTheoryPackage(project, saveAs, out string packagePath, out string error))
        {
            HideEditPopup();
            owner?.NotifyChartEditorLibraryChangedFromUi(packagePath);
            SetStatus(saveAs ? $"Saved new .theory chart: {packagePath}" : $"Saved .theory chart: {packagePath}");
            Rebuild();
            ShowSaveSuccessPopup();
            return;
        }

        SetStatus(error);
    }

    private void ShowSaveSuccessPopup()
    {
        HideSaveSuccessPopup();
        if (RootElement == null)
            return;

        VisualElement popup = new VisualElement();
        popup.style.position = Position.Absolute;
        popup.style.right = 32f;
        popup.style.bottom = 88f;
        popup.style.paddingLeft = 22f;
        popup.style.paddingRight = 22f;
        popup.style.paddingTop = 14f;
        popup.style.paddingBottom = 14f;
        StylePopupPanel(popup, new Color(0.030f, 0.040f, 0.052f, 0.98f), 12f);
        popup.pickingMode = PickingMode.Ignore;

        Label label = CreateLabel("Save successful", 20f, Color.white, true, TextAnchor.MiddleCenter, false);
        popup.Add(label);

        saveSuccessPopupElement = popup;
        RootElement.Add(saveSuccessPopupElement);
        saveSuccessPopupElement.BringToFront();
        saveSuccessPopupElement.schedule.Execute(HideSaveSuccessPopup).StartingIn(1800);
    }

    private void HideSaveSuccessPopup()
    {
        if (saveSuccessPopupElement == null)
            return;

        saveSuccessPopupElement.RemoveFromHierarchy();
        saveSuccessPopupElement = null;
    }

    private void ExportProject()
    {
        if (ChartEditorProjectStore.ExportPlayableProject(project, out string exportDirectory, out string theoryPackagePath, out string error))
        {
            owner?.NotifyChartEditorLibraryChangedFromUi(theoryPackagePath);
            SetStatus($"Exported .theory chart: {theoryPackagePath}");
            Rebuild();
        }
        else
        {
            SetStatus(error);
        }
    }

    private void AddNoteAtCursor()
    {
        AddNoteAtTime(project.cursorTimeSeconds);
    }

    private void AddNoteAtTime(double timeSeconds)
    {
        ChartEditorTrack track = project.SelectedTrack;
        if (track == null)
            return;

        double safeTime = Math.Max(0.0, Math.Min(project.DurationSeconds, timeSeconds));
        ChartEditorNote note = new ChartEditorNote
        {
            id = Guid.NewGuid().ToString("N"),
            sourceNoteId = -1,
            chartTimeSeconds = safeTime,
            timeSeconds = safeTime,
            durationSeconds = 0.25,
            stringOrLane = 0,
            fret = 0,
            velocity = 95,
            requiresPluck = true
        };
        track.notes.Add(note);
        ChartEditorTimingService.UpdateNoteBeatTiming(project, note);
        track.notes = track.notes.OrderBy(n => n.timeSeconds).ToList();
        SelectSingleNote(track, note);
        project.cursorTimeSeconds = note.timeSeconds;
        project.dirty = true;
        Rebuild();
    }

    private void AddSectionAtCursor()
    {
        AddSectionAtTime(project.cursorTimeSeconds);
    }

    private void AddSectionAtTime(double timeSeconds)
    {
        double safeTime = Math.Max(0.0, Math.Min(project.DurationSeconds, timeSeconds));
        ChartEditorSection section = new ChartEditorSection
        {
            id = Guid.NewGuid().ToString("N"),
            name = $"Section {(project.sections?.Count ?? 0) + 1}",
            chartStartTimeSeconds = safeTime,
            chartEndTimeSeconds = safeTime + 8.0,
            startTimeSeconds = safeTime,
            endTimeSeconds = safeTime + 8.0,
            userEdited = true
        };
        project.sections.Add(section);
        ChartEditorTimingService.UpdateSectionBeatTiming(project, section);
        selectedSectionId = section.id;
        ClearNoteSelection();
        selectedSyncPointId = null;
        mode = ChartEditorMode.Sections;
        project.cursorTimeSeconds = section.startTimeSeconds;
        project.dirty = true;
        Rebuild();
    }

    private void AddSyncPointAtCursor()
    {
        AddSyncPointAtTime(project.cursorTimeSeconds);
    }

    private void AddSyncPointAtTime(double timeSeconds)
    {
        double safeTime = Math.Max(0.0, Math.Min(project.DurationSeconds, timeSeconds));
        ChartEditorBeatMarker point = ChartEditorTimingService.AddAnchorAtAudioTime(project, safeTime);
        SelectSingleAnchor(point);
        project.cursorTimeSeconds = point?.audioTimeSeconds ?? safeTime;
        project.dirty = true;
        Rebuild();
    }

    private void MoveCursor(double delta)
    {
        SetCursorTime(project.cursorTimeSeconds + delta, rebuild: false, syncAudio: true);
    }

    private void AdjustTimelineZoomAroundViewportCenter(int direction)
    {
        float viewportWidth = timelineViewportWidth > 1f ? timelineViewportWidth : 1600f;
        float anchorViewportX = viewportWidth * 0.5f;
        double anchorTime = PixelsToSeconds(Mathf.Max(0f, timelineScrollOffset.x + anchorViewportX - TimelineLabelWidth));
        AdjustTimelineZoom(direction, anchorTime, anchorViewportX);
    }

    private void AdjustTimelineZoom(int direction, double anchorTimeSeconds, float anchorViewportX)
    {
        if (project == null || direction == 0)
            return;

        float oldZoom = timelineZoom;
        float factor = direction > 0 ? 1.25f : 0.8f;
        timelineZoom = Mathf.Clamp(timelineZoom * factor, MinTimelineZoom, MaxTimelineZoom);
        if (Mathf.Abs(timelineZoom - oldZoom) < 0.001f)
            return;

        timelineScrollOffset.x = Mathf.Max(0f, TimeToPixels(anchorTimeSeconds) + TimelineLabelWidth - Mathf.Max(0f, anchorViewportX));
        timelineScrollInitialized = true;
        skipTimelineScrollCaptureOnce = true;
        Rebuild();
    }

    private void ResetTimelineZoom()
    {
        if (Mathf.Abs(timelineZoom - 1f) < 0.001f)
            return;

        timelineZoom = 1f;
        if (project != null)
        {
            float viewportWidth = timelineViewportWidth > 1f ? timelineViewportWidth : 1600f;
            float anchorViewportX = viewportWidth * 0.5f;
            double anchorTime = PixelsToSeconds(Mathf.Max(0f, timelineScrollOffset.x + anchorViewportX - TimelineLabelWidth));
            timelineScrollOffset.x = Mathf.Max(0f, TimeToPixels(anchorTime) + TimelineLabelWidth - anchorViewportX);
        }
        timelineScrollInitialized = true;
        skipTimelineScrollCaptureOnce = true;
        Rebuild();
    }

    private static float ResolveDefaultTimelineZoom(ChartEditorProject sourceProject)
    {
        if (sourceProject?.tracks == null)
            return 1f;

        double minGapSeconds = double.MaxValue;
        for (int trackIndex = 0; trackIndex < sourceProject.tracks.Count; trackIndex++)
        {
            ChartEditorTrack track = sourceProject.tracks[trackIndex];
            if (track?.notes == null || track.notes.Count < 2)
                continue;

            int laneCount = GetTrackLaneCount(track);
            for (int lane = 0; lane < laneCount; lane++)
            {
                List<double> times = track.notes
                    .Where(note => note != null && GetVisualLaneForNote(track, note, laneCount) == lane)
                    .Select(note => note.timeSeconds)
                    .OrderBy(time => time)
                    .ToList();

                for (int i = 1; i < times.Count; i++)
                {
                    double gap = times[i] - times[i - 1];
                    if (gap > 0.001)
                        minGapSeconds = Math.Min(minGapSeconds, gap);
                }
            }
        }

        if (minGapSeconds == double.MaxValue)
            return 1f;

        float requiredPixelsPerSecond = (DefaultNoteSquareWidth + NoteSpacingGap) / Mathf.Max(0.001f, (float)minGapSeconds);
        float requiredZoom = requiredPixelsPerSecond / BaseTimelinePixelsPerSecond;
        return Mathf.Clamp(Mathf.Max(1f, requiredZoom), MinTimelineZoom, MaxTimelineZoom);
    }

    private void MoveScope(double delta)
    {
        ChartEditorNote note = FindSelectedNote();
        if (mode == ChartEditorMode.Notes && note != null)
            NudgeNote(note, delta);
        else
        {
            ChartEditorTimingService.MoveEntireProject(project, delta);
            Rebuild();
        }
    }

    private void NudgeNote(ChartEditorNote note, double delta)
    {
        note.timeSeconds = Math.Max(0.0, note.timeSeconds + delta);
        note.chartTimeSeconds = Math.Max(0.0, note.chartTimeSeconds + delta);
        ChartEditorTimingService.UpdateNoteBeatTiming(project, note);
        project.dirty = true;
        Rebuild();
    }

    private void NudgeSelectedNotes(double delta)
    {
        List<ChartEditorNoteReference> refs = GetSelectedNoteReferences();
        if (refs.Count == 0)
            return;

        for (int i = 0; i < refs.Count; i++)
        {
            ChartEditorNote note = refs[i].note;
            double originalTime = note.timeSeconds;
            note.timeSeconds = Math.Max(0.0, Math.Min(project.DurationSeconds, note.timeSeconds + delta));
            note.chartTimeSeconds = Math.Max(0.0, note.chartTimeSeconds + (note.timeSeconds - originalTime));
            ChartEditorTimingService.UpdateNoteBeatTiming(project, note);
        }

        SortSelectedNoteTracks(refs);
        project.dirty = true;
        Rebuild();
    }

    private void QuantizeSelectedNotesToBeatGrid()
    {
        if (project == null)
            return;

        List<ChartEditorNoteReference> refs = GetSelectedNoteReferences()
            .Where(noteRef => noteRef?.track != null && noteRef.note != null)
            .ToList();
        if (refs.Count == 0)
        {
            SetStatus("Select one or more notes to quantize.");
            return;
        }

        for (int i = 0; i < refs.Count; i++)
        {
            ChartEditorNote note = refs[i].note;
            double beat = Math.Round(ChartEditorTimingService.GetBeatPositionForAudioTime(project, note.timeSeconds));
            double quantizedTime = Math.Max(0.0, Math.Min(project.DurationSeconds, ChartEditorTimingService.GetAudioTimeForBeat(project, beat)));
            double delta = quantizedTime - note.timeSeconds;
            note.timeSeconds = quantizedTime;
            note.chartTimeSeconds = Math.Max(0.0, note.chartTimeSeconds + delta);
            ChartEditorTimingService.UpdateNoteBeatTiming(project, note);
        }

        SortSelectedNoteTracks(refs);
        project.dirty = true;
        SetStatus($"Quantized {refs.Count} note{(refs.Count == 1 ? string.Empty : "s")} to the beat grid.");
        Rebuild();
    }

    private void ChangeNoteFret(ChartEditorNote note, int delta)
    {
        note.fret = Mathf.Clamp(note.fret + delta, 0, 24);
        project.dirty = true;
        Rebuild();
    }

    private void ChangeNoteLane(ChartEditorNote note, int delta)
    {
        note.stringOrLane = Mathf.Clamp(note.stringOrLane + delta, 0, Math.Max(0, GetTrackLaneCount(project.SelectedTrack) - 1));
        project.dirty = true;
        Rebuild();
    }

    private void ChangeSelectedNoteLanes(int delta)
    {
        List<ChartEditorNoteReference> refs = GetSelectedNoteReferences();
        if (refs.Count == 0)
            return;

        for (int i = 0; i < refs.Count; i++)
        {
            ChartEditorNoteReference noteRef = refs[i];
            noteRef.note.stringOrLane = Mathf.Clamp(noteRef.note.stringOrLane + delta, 0, Math.Max(0, GetTrackLaneCount(noteRef.track) - 1));
        }

        project.dirty = true;
        Rebuild();
    }

    private bool HasCopiedNotes()
    {
        return noteClipboard.Count > 0;
    }

    private void CopySelectedNotes()
    {
        List<ChartEditorNoteReference> refs = GetSelectedNoteReferences()
            .Where(noteRef => noteRef?.note != null)
            .OrderBy(noteRef => noteRef.note.timeSeconds)
            .ThenBy(noteRef => noteRef.note.stringOrLane)
            .ToList();

        if (refs.Count == 0)
        {
            SetStatus("Select one or more notes to copy.");
            return;
        }

        double baseTime = refs.Min(noteRef => noteRef.note.timeSeconds);
        noteClipboard.Clear();
        for (int i = 0; i < refs.Count; i++)
        {
            ChartEditorNote source = refs[i].note;
            noteClipboard.Add(new ChartEditorCopiedNote
            {
                note = CloneNotePayload(source),
                offsetSeconds = Math.Max(0.0, source.timeSeconds - baseTime)
            });
        }

        SetStatus($"Copied {refs.Count} note{(refs.Count == 1 ? string.Empty : "s")}.");
    }

    private void PasteCopiedNotesNextToSelection()
    {
        if (!HasCopiedNotes())
        {
            SetStatus("No copied notes to paste.");
            return;
        }

        PasteCopiedNotesAt(ResolveKeyboardPasteBaseTime());
    }

    private double ResolveKeyboardPasteBaseTime()
    {
        List<ChartEditorNoteReference> refs = GetSelectedNoteReferences()
            .Where(noteRef => noteRef?.track != null && noteRef.note != null)
            .ToList();

        if (refs.Count == 0)
            return Math.Max(0.0, project?.cursorTimeSeconds ?? 0.0);

        double visibleEnd = refs.Max(noteRef => GetReservedNoteEndTime(noteRef.track, noteRef.note));
        return visibleEnd + GetPasteVisualGapSeconds();
    }

    private void PasteCopiedNotesAt(double requestedBaseTime)
    {
        if (project == null)
            return;

        ChartEditorTrack track = project.SelectedTrack;
        if (track == null)
        {
            SetStatus("Select a track before pasting notes.");
            return;
        }

        List<ChartEditorCopiedNote> copied = noteClipboard
            .Where(item => item?.note != null)
            .OrderBy(item => item.offsetSeconds)
            .ThenBy(item => item.note.stringOrLane)
            .ToList();
        if (copied.Count == 0)
        {
            SetStatus("No copied notes to paste.");
            return;
        }

        track.notes ??= new List<ChartEditorNote>();
        double baseTime = ResolveNonOverlappingPasteBaseTime(track, copied, requestedBaseTime);
        int laneCount = Math.Max(1, GetTrackLaneCount(track));
        ClearNoteSelection();

        ChartEditorNote firstClone = null;
        for (int i = 0; i < copied.Count; i++)
        {
            ChartEditorCopiedNote copiedNote = copied[i];
            ChartEditorNote clone = CloneNotePayload(copiedNote.note);
            double targetTime = Math.Max(0.0, baseTime + copiedNote.offsetSeconds);
            double timeDelta = targetTime - clone.timeSeconds;
            clone.id = Guid.NewGuid().ToString("N");
            clone.sourceNoteId = -1;
            clone.timeSeconds = targetTime;
            clone.chartTimeSeconds = Math.Max(0.0, clone.chartTimeSeconds + timeDelta);
            clone.stringOrLane = Mathf.Clamp(clone.stringOrLane, 0, laneCount - 1);
            clone.EnsureDefaults();
            ChartEditorTimingService.UpdateNoteBeatTiming(project, clone);

            track.notes.Add(clone);
            selectedNoteIds.Add(clone.id);
            firstClone ??= clone;
        }

        track.notes = track.notes
            .OrderBy(note => note?.timeSeconds ?? 0.0)
            .ThenBy(note => note?.stringOrLane ?? 0)
            .ToList();

        if (firstClone != null)
        {
            selectedNoteId = firstClone.id;
            project.selectedTrackId = track.id;
            project.cursorTimeSeconds = firstClone.timeSeconds;
        }

        selectedSectionId = null;
        selectedSyncPointId = null;
        mode = ChartEditorMode.Notes;
        SetExclusiveVisibleTrack(track, markDirty: false);
        project.dirty = true;
        Rebuild();
        SetStatus($"Pasted {copied.Count} note{(copied.Count == 1 ? string.Empty : "s")}.");
    }

    private double ResolveNonOverlappingPasteBaseTime(ChartEditorTrack targetTrack, List<ChartEditorCopiedNote> copied, double requestedBaseTime)
    {
        if (targetTrack == null || copied == null || copied.Count == 0)
            return Math.Max(0.0, requestedBaseTime);

        int laneCount = Math.Max(1, GetTrackLaneCount(targetTrack));
        double baseTime = Math.Max(0.0, requestedBaseTime);
        double gapSeconds = GetPasteVisualGapSeconds();
        for (int guard = 0; guard < 512; guard++)
        {
            double shiftedBase = baseTime;
            bool adjusted = false;

            for (int i = 0; i < copied.Count; i++)
            {
                ChartEditorCopiedNote copiedNote = copied[i];
                if (copiedNote?.note == null)
                    continue;

                int targetLane = GetPasteVisualLane(targetTrack, copiedNote.note, laneCount);
                double pastedStart = baseTime + copiedNote.offsetSeconds;
                double pastedEnd = pastedStart + GetReservedNoteDurationSeconds(copiedNote.note);

                for (int noteIndex = 0; noteIndex < targetTrack.notes.Count; noteIndex++)
                {
                    ChartEditorNote existing = targetTrack.notes[noteIndex];
                    if (existing == null)
                        continue;

                    if (GetPasteVisualLane(targetTrack, existing, laneCount) != targetLane)
                        continue;

                    double existingStart = Math.Max(0.0, existing.timeSeconds);
                    double existingEnd = GetReservedNoteEndTime(targetTrack, existing);
                    if (!IntervalsOverlapWithGap(pastedStart, pastedEnd, existingStart, existingEnd, gapSeconds))
                        continue;

                    shiftedBase = Math.Max(shiftedBase, existingEnd + gapSeconds - copiedNote.offsetSeconds);
                    adjusted = true;
                }
            }

            if (!adjusted || shiftedBase <= baseTime + 0.0001)
                break;

            baseTime = shiftedBase;
        }

        return Math.Max(0.0, baseTime);
    }

    private int GetPasteVisualLane(ChartEditorTrack track, ChartEditorNote note, int laneCount)
    {
        if (note == null)
            return 0;

        return Mathf.Clamp(GetVisualLaneForNote(track, note, laneCount), 0, Math.Max(0, laneCount - 1));
    }

    private double GetReservedNoteEndTime(ChartEditorTrack track, ChartEditorNote note)
    {
        if (note == null)
            return 0.0;

        return Math.Max(0.0, note.timeSeconds) + GetReservedNoteDurationSeconds(note);
    }

    private double GetReservedNoteDurationSeconds(ChartEditorNote note)
    {
        double noteDuration = Math.Max(0.055, note?.durationSeconds ?? 0.0);
        double visibleDuration = DefaultNoteSquareWidth / Math.Max(1.0, GetTimelinePixelsPerSecond());
        return Math.Max(noteDuration, visibleDuration);
    }

    private double GetPasteVisualGapSeconds()
    {
        return NoteSpacingGap / Math.Max(1.0, GetTimelinePixelsPerSecond());
    }

    private static bool IntervalsOverlapWithGap(double aStart, double aEnd, double bStart, double bEnd, double gap)
    {
        return aStart < bEnd + gap && bStart < aEnd + gap;
    }

    private static ChartEditorNote CloneNotePayload(ChartEditorNote source)
    {
        if (source == null)
            return new ChartEditorNote();

        string json = JsonUtility.ToJson(source);
        ChartEditorNote clone = JsonUtility.FromJson<ChartEditorNote>(json) ?? new ChartEditorNote();
        clone.EnsureDefaults();
        return clone;
    }

    private void DuplicateNote(ChartEditorNote source)
    {
        ChartEditorTrack track = project.SelectedTrack;
        if (track == null)
            return;

        string json = JsonUtility.ToJson(source);
        ChartEditorNote clone = JsonUtility.FromJson<ChartEditorNote>(json);
        clone.id = Guid.NewGuid().ToString("N");
        clone.sourceNoteId = -1;
        clone.timeSeconds += 0.1;
        clone.chartTimeSeconds += 0.1;
        ChartEditorTimingService.UpdateNoteBeatTiming(project, clone);
        track.notes.Add(clone);
        SelectSingleNote(track, clone);
        project.dirty = true;
        Rebuild();
    }

    private void DuplicateSelectedNotes()
    {
        List<ChartEditorNoteReference> refs = GetSelectedNoteReferences();
        if (refs.Count == 0)
            return;

        ClearNoteSelection();
        ChartEditorNoteReference firstClone = null;
        for (int i = 0; i < refs.Count; i++)
        {
            ChartEditorNoteReference noteRef = refs[i];
            string json = JsonUtility.ToJson(noteRef.note);
            ChartEditorNote clone = JsonUtility.FromJson<ChartEditorNote>(json);
            clone.id = Guid.NewGuid().ToString("N");
            clone.sourceNoteId = -1;
            clone.timeSeconds = Math.Max(0.0, Math.Min(project.DurationSeconds, clone.timeSeconds + 0.1));
            clone.chartTimeSeconds = Math.Max(0.0, clone.chartTimeSeconds + 0.1);
            ChartEditorTimingService.UpdateNoteBeatTiming(project, clone);
            noteRef.track.notes.Add(clone);
            selectedNoteIds.Add(clone.id);
            firstClone ??= new ChartEditorNoteReference { track = noteRef.track, note = clone };
        }

        SortSelectedNoteTracks(refs);
        if (firstClone != null)
        {
            selectedNoteId = firstClone.note.id;
            project.selectedTrackId = firstClone.track.id;
            project.cursorTimeSeconds = firstClone.note.timeSeconds;
        }

        selectedSectionId = null;
        selectedSyncPointId = null;
        mode = ChartEditorMode.Notes;
        project.dirty = true;
        Rebuild();
    }

    private void DeleteNote(ChartEditorNote note)
    {
        ChartEditorTrack track = project.SelectedTrack;
        if (track?.notes == null)
            return;

        track.notes.Remove(note);
        ClearNoteSelection();
        project.dirty = true;
        Rebuild();
    }

    private void DeleteSelectedNotes()
    {
        List<ChartEditorNoteReference> refs = GetSelectedNoteReferences();
        if (refs.Count == 0)
            return;

        foreach (IGrouping<ChartEditorTrack, ChartEditorNoteReference> group in refs.GroupBy(noteRef => noteRef.track))
        {
            if (group.Key?.notes == null)
                continue;

            HashSet<string> ids = new HashSet<string>(group.Select(noteRef => noteRef.note.id), StringComparer.OrdinalIgnoreCase);
            group.Key.notes.RemoveAll(note => note != null && ids.Contains(note.id));
        }

        ClearNoteSelection();
        project.dirty = true;
        Rebuild();
    }

    private void SortSelectedNoteTracks(IEnumerable<ChartEditorNoteReference> refs)
    {
        foreach (ChartEditorTrack track in refs?.Select(noteRef => noteRef?.track).Where(track => track != null).Distinct() ?? Enumerable.Empty<ChartEditorTrack>())
            track.notes = track.notes?.OrderBy(note => note?.timeSeconds ?? 0.0).ThenBy(note => note?.stringOrLane ?? 0).ToList() ?? new List<ChartEditorNote>();
    }

    private void DeleteSection(ChartEditorSection section)
    {
        if (section == null || project?.sections == null)
            return;

        project.sections.Remove(section);
        selectedSectionId = null;
        project.dirty = true;
        Rebuild();
    }

    private void NudgeSectionStart(ChartEditorSection section, double delta)
    {
        section.startTimeSeconds = Math.Max(0.0, section.startTimeSeconds + delta);
        section.chartStartTimeSeconds = Math.Max(0.0, section.chartStartTimeSeconds + delta);
        if (section.endTimeSeconds <= section.startTimeSeconds)
            section.endTimeSeconds = section.startTimeSeconds + 0.05;
        ChartEditorTimingService.UpdateSectionBeatTiming(project, section);
        section.userEdited = true;
        project.dirty = true;
        Rebuild();
    }

    private void NudgeSectionEnd(ChartEditorSection section, double delta)
    {
        section.endTimeSeconds = Math.Max(section.startTimeSeconds + 0.05, section.endTimeSeconds + delta);
        section.chartEndTimeSeconds = Math.Max(section.chartStartTimeSeconds + 0.05, section.chartEndTimeSeconds + delta);
        ChartEditorTimingService.UpdateSectionBeatTiming(project, section);
        section.userEdited = true;
        project.dirty = true;
        Rebuild();
    }

    private void NudgeSyncPoint(ChartEditorBeatMarker point, double delta)
    {
        if (point.locked)
            return;

        ChartEditorTimingService.MoveAnchor(project, point, point.audioTimeSeconds + delta);
        project.cursorTimeSeconds = point.audioTimeSeconds;
        project.dirty = true;
        Rebuild();
    }

    private ChartEditorNote FindSelectedNote()
    {
        ChartEditorTrack track = project?.SelectedTrack;
        return string.IsNullOrWhiteSpace(selectedNoteId) || track?.notes == null
            ? null
            : track.notes.FirstOrDefault(note => note != null && string.Equals(note.id, selectedNoteId, StringComparison.OrdinalIgnoreCase));
    }

    private ChartEditorSection FindSelectedSection()
    {
        return string.IsNullOrWhiteSpace(selectedSectionId) || project?.sections == null
            ? null
            : project.sections.FirstOrDefault(section => section != null && string.Equals(section.id, selectedSectionId, StringComparison.OrdinalIgnoreCase));
    }

    private ChartEditorBeatMarker FindSelectedAnchor()
    {
        return string.IsNullOrWhiteSpace(selectedSyncPointId) || project?.beatMap?.beatMarkers == null
            ? null
            : project.beatMap.beatMarkers.FirstOrDefault(point => point != null && point.isAnchor && string.Equals(point.id, selectedSyncPointId, StringComparison.OrdinalIgnoreCase));
    }

    private Button CreateTransportButton(string text, Action action)
    {
        Button button = CreateButton(text, action);
        button.style.width = 92f;
        button.style.minWidth = 92f;
        button.style.height = 72f;
        button.style.marginLeft = 6f;
        button.style.marginRight = 6f;
        button.style.fontSize = UiFont(30f);
        StyleSoftButton(button, new Color(0.78f, 0.82f, 0.96f, 1f));
        SetRadius(button, 11f);
        return button;
    }

    private Button CreateHeaderIconButton(string text, Action action, bool primary)
    {
        Button button = new Button(action) { text = text };
        button.focusable = false;
        button.style.width = primary ? 74f : 58f;
        button.style.minWidth = primary ? 74f : 58f;
        button.style.height = 58f;
        button.style.marginLeft = 8f;
        button.style.marginRight = 8f;
        button.style.paddingLeft = 0f;
        button.style.paddingRight = 0f;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.unityFontDefinition = bodyFont;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.fontSize = UiFont(primary ? 26f : 23f);
        Color accent = primary ? new Color(0.72f, 0.50f, 1f, 1f) : new Color(0.74f, 0.80f, 0.92f, 1f);
        if (primary)
            StyleFilledButton(button, new Color(0.42f, 0.24f, 0.74f, 1f), darkText: false);
        else
            StyleSoftButton(button, accent);
        SetRadius(button, primary ? 12f : 10f);
        return button;
    }

    private Button CreateHeaderActionButton(string text, Action action, bool primary)
    {
        Button button = new Button(action) { text = text };
        button.focusable = false;
        button.style.height = 58f;
        button.style.minWidth = primary ? 178f : 154f;
        button.style.marginLeft = 10f;
        button.style.marginRight = 10f;
        button.style.paddingLeft = 22f;
        button.style.paddingRight = 22f;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.unityFontDefinition = bodyFont;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.fontSize = UiFont(21f);
        if (primary)
        {
            StyleFilledButton(button, new Color(0.54f, 0.30f, 0.92f, 1f), darkText: false);
        }
        else
        {
            StyleSoftButton(button, new Color(0.80f, 0.86f, 0.94f, 1f));
        }
        SetRadius(button, 11f);
        return button;
    }

    private static void ConfigureScrollView(ScrollView view)
    {
        if (view == null)
            return;

        view.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        view.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        StyleModernScrollView(view);
    }

    private static void StyleModernScrollView(ScrollView view)
    {
        if (view == null)
            return;

        view.style.backgroundColor = Color.clear;
        view.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        view.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        HideNativeScroller(view.verticalScroller);
        HideNativeScroller(view.horizontalScroller);
        AddIosScrollIndicators(view);
        view.schedule.Execute(() =>
        {
            HideNativeScroller(view.verticalScroller);
            HideNativeScroller(view.horizontalScroller);
            AddIosScrollIndicators(view);
        });
    }

    private static void HideNativeScroller(Scroller scroller)
    {
        if (scroller == null)
            return;

        scroller.style.display = DisplayStyle.None;
        scroller.style.width = 0f;
        scroller.style.height = 0f;
        scroller.style.minWidth = 0f;
        scroller.style.minHeight = 0f;
        scroller.style.backgroundColor = Color.clear;
        scroller.style.borderTopWidth = 0f;
        scroller.style.borderRightWidth = 0f;
        scroller.style.borderBottomWidth = 0f;
        scroller.style.borderLeftWidth = 0f;
        if (scroller.lowButton != null)
            scroller.lowButton.style.display = DisplayStyle.None;
        if (scroller.highButton != null)
            scroller.highButton.style.display = DisplayStyle.None;
    }

    private static void AddIosScrollIndicators(ScrollView view)
    {
        if (view == null || view.Q<VisualElement>("chart-editor-ios-scroll-v") != null)
            return;

        VisualElement vertical = CreateIosScrollIndicator("chart-editor-ios-scroll-v", vertical: true);
        VisualElement horizontal = CreateIosScrollIndicator("chart-editor-ios-scroll-h", vertical: false);
        view.hierarchy.Add(vertical);
        view.hierarchy.Add(horizontal);
        AddIosScrollIndicatorDragHandlers(view, vertical, vertical: true);
        AddIosScrollIndicatorDragHandlers(view, horizontal, vertical: false);

        void UpdateIndicators()
        {
            UpdateIosScrollIndicator(view, vertical, vertical: true);
            UpdateIosScrollIndicator(view, horizontal, vertical: false);
        }

        view.RegisterCallback<GeometryChangedEvent>(_ => view.schedule.Execute(UpdateIndicators));
        view.RegisterCallback<WheelEvent>(_ => view.schedule.Execute(UpdateIndicators));
        view.RegisterCallback<PointerUpEvent>(_ => view.schedule.Execute(UpdateIndicators));
        view.schedule.Execute(UpdateIndicators);
    }

    private static VisualElement CreateIosScrollIndicator(string name, bool vertical)
    {
        VisualElement indicator = new VisualElement();
        indicator.name = name;
        indicator.style.position = Position.Absolute;
        indicator.style.width = vertical ? 12f : 60f;
        indicator.style.height = vertical ? 66f : 8f;
        indicator.style.right = vertical ? 6f : StyleKeyword.Auto;
        indicator.style.bottom = vertical ? StyleKeyword.Auto : 6f;
        indicator.style.backgroundColor = new Color(0.92f, 0.95f, 1f, 0.34f);
        indicator.style.borderTopLeftRadius = 999f;
        indicator.style.borderTopRightRadius = 999f;
        indicator.style.borderBottomLeftRadius = 999f;
        indicator.style.borderBottomRightRadius = 999f;
        indicator.style.borderTopWidth = 0f;
        indicator.style.borderRightWidth = 0f;
        indicator.style.borderBottomWidth = 0f;
        indicator.style.borderLeftWidth = 0f;
        indicator.style.opacity = 0.74f;
        indicator.pickingMode = PickingMode.Position;
        return indicator;
    }

    private static void AddIosScrollIndicatorDragHandlers(ScrollView view, VisualElement indicator, bool vertical)
    {
        bool dragging = false;
        int pointerId = -1;
        Vector2 startPointer = Vector2.zero;
        Vector2 startOffset = Vector2.zero;

        indicator.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || !TryGetIosScrollMetrics(view, vertical, out _, out _, out _, out _))
                return;

            dragging = true;
            pointerId = evt.pointerId;
            startPointer = PointerPosition(evt);
            startOffset = view.scrollOffset;
            indicator.style.opacity = 1f;
            indicator.CapturePointer(pointerId);
            evt.StopImmediatePropagation();
        });

        indicator.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            if (!TryGetIosScrollMetrics(view, vertical, out float maxScroll, out float trackTravel, out _, out _))
                return;

            Vector2 delta = PointerPosition(evt) - startPointer;
            float dragDelta = vertical ? delta.y : delta.x;
            float scrollDelta = trackTravel <= 0.001f ? 0f : dragDelta / trackTravel * maxScroll;
            Vector2 offset = startOffset;
            if (vertical)
                offset.y = Mathf.Clamp(startOffset.y + scrollDelta, 0f, maxScroll);
            else
                offset.x = Mathf.Clamp(startOffset.x + scrollDelta, 0f, maxScroll);

            view.scrollOffset = offset;
            UpdateIosScrollIndicator(view, indicator, vertical);
            evt.StopImmediatePropagation();
        });

        indicator.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            dragging = false;
            if (indicator.HasPointerCapture(pointerId))
                indicator.ReleasePointer(pointerId);

            indicator.style.opacity = 0.74f;
            UpdateIosScrollIndicator(view, indicator, vertical);
            evt.StopImmediatePropagation();
        });
    }

    private static void UpdateIosScrollIndicator(ScrollView view, VisualElement indicator, bool vertical)
    {
        if (view == null || indicator == null || view.contentViewport == null || view.contentContainer == null)
            return;

        if (!TryGetIosScrollMetrics(view, vertical, out float maxScroll, out float trackTravel, out float thumbSize, out float viewportSize))
        {
            indicator.style.display = DisplayStyle.None;
            return;
        }

        float inset = 6f;
        float scroll = vertical ? view.scrollOffset.y : view.scrollOffset.x;
        float position = inset + Mathf.Clamp01(scroll / maxScroll) * trackTravel;

        indicator.style.display = DisplayStyle.Flex;
        if (vertical)
        {
            indicator.style.top = position;
            indicator.style.right = 6f;
            indicator.style.width = 12f;
            indicator.style.height = thumbSize;
        }
        else
        {
            indicator.style.left = position;
            indicator.style.bottom = 6f;
            indicator.style.width = thumbSize;
            indicator.style.height = 8f;
        }
    }

    private static void RefreshIosScrollIndicators(ScrollView view)
    {
        if (view == null)
            return;

        VisualElement vertical = view.Q<VisualElement>("chart-editor-ios-scroll-v");
        VisualElement horizontal = view.Q<VisualElement>("chart-editor-ios-scroll-h");
        if (vertical != null)
            UpdateIosScrollIndicator(view, vertical, vertical: true);
        if (horizontal != null)
            UpdateIosScrollIndicator(view, horizontal, vertical: false);
    }

    private static bool TryGetIosScrollMetrics(ScrollView view, bool vertical, out float maxScroll, out float trackTravel, out float thumbSize, out float viewportSize)
    {
        maxScroll = 0f;
        trackTravel = 0f;
        thumbSize = 0f;
        viewportSize = 0f;
        if (view == null || view.contentViewport == null || view.contentContainer == null)
            return false;

        float viewportWidth = Mathf.Max(1f, view.contentViewport.layout.width);
        float viewportHeight = Mathf.Max(1f, view.contentViewport.layout.height);
        float contentWidth = Mathf.Max(view.contentContainer.layout.width, viewportWidth);
        float contentHeight = Mathf.Max(view.contentContainer.layout.height, viewportHeight);
        viewportSize = vertical ? viewportHeight : viewportWidth;
        float contentSize = vertical ? contentHeight : contentWidth;
        maxScroll = Mathf.Max(0f, contentSize - viewportSize);
        if (maxScroll <= 1f)
            return false;

        float inset = 6f;
        float trackSize = Mathf.Max(1f, viewportSize - inset * 2f);
        thumbSize = Mathf.Clamp(viewportSize / contentSize * trackSize, vertical ? 66f : 54f, trackSize);
        trackTravel = Mathf.Max(0f, trackSize - thumbSize);
        return trackTravel > 0.001f;
    }

    private void AddTimelineRowLabel(VisualElement timeline, string text, float top, float height)
    {
        Label label = CreateLabel(text, 22f, new Color(0.74f, 0.78f, 0.84f, 1f), true, TextAnchor.MiddleCenter, false);
        label.style.position = Position.Absolute;
        label.style.left = 0f;
        label.style.top = top;
        label.style.width = TimelineLabelWidth;
        label.style.height = height;
        label.style.backgroundColor = new Color(0.030f, 0.036f, 0.046f, 1f);
        label.style.borderRightWidth = 1f;
        label.style.borderRightColor = new Color(0.16f, 0.19f, 0.24f, 1f);
        label.style.whiteSpace = WhiteSpace.Normal;
        label.pickingMode = PickingMode.Ignore;
        timeline.Add(label);
    }

    private static void AddTimelineGridLine(VisualElement timeline, float top)
    {
        VisualElement line = new VisualElement();
        line.style.position = Position.Absolute;
        line.style.left = TimelineLabelWidth;
        line.style.right = 0f;
        line.style.top = top;
        line.style.height = 1f;
        line.style.backgroundColor = new Color(0.13f, 0.16f, 0.21f, 1f);
        line.pickingMode = PickingMode.Ignore;
        timeline.Add(line);
    }

    private static Color TrackColor(int index)
    {
        Color[] colors =
        {
            new Color(0.62f, 0.36f, 0.96f, 1f),
            new Color(0.34f, 0.58f, 0.82f, 1f),
            new Color(0.38f, 0.72f, 0.34f, 1f),
            new Color(0.90f, 0.58f, 0.20f, 1f),
            new Color(0.86f, 0.34f, 0.62f, 1f),
            new Color(0.78f, 0.66f, 0.28f, 1f)
        };
        return colors[Mathf.Abs(index) % colors.Length];
    }

    private static Color SectionColor(int index)
    {
        Color[] colors =
        {
            new Color(0.33f, 0.24f, 0.56f, 1f),
            new Color(0.25f, 0.43f, 0.58f, 1f),
            new Color(0.32f, 0.52f, 0.30f, 1f),
            new Color(0.58f, 0.46f, 0.22f, 1f),
            new Color(0.26f, 0.43f, 0.61f, 1f),
            new Color(0.50f, 0.29f, 0.62f, 1f),
            new Color(0.55f, 0.30f, 0.24f, 1f)
        };
        return colors[Mathf.Abs(index) % colors.Length];
    }

    private ContextMenuItem[] BuildTechniqueContextItems(IEnumerable<ChartEditorNoteReference> noteRefs)
    {
        List<ChartEditorNoteReference> refs = noteRefs?
            .Where(noteRef => noteRef?.note != null)
            .ToList() ?? new List<ChartEditorNoteReference>();

        return new[]
        {
            CreateTechniqueContextItem(refs, NoteTechnique.HammerOn, "Hammer-On"),
            CreateTechniqueContextItem(refs, NoteTechnique.PullOff, "Pull-Off"),
            CreateAddTechniqueSegmentContextItem(refs, NoteTechniqueSegmentType.Sustain, "Add Sustain"),
            CreateAddTechniqueSegmentContextItem(refs, NoteTechniqueSegmentType.Vibrato, "Add Vibrato"),
            CreateBendTechniqueSegmentContextItem(refs, "Add Half Bend", 0f, HalfStepBendSemitones),
            CreateBendTechniqueSegmentContextItem(refs, "Add Full Bend", 0f, FullStepBendSemitones),
            CreateBendTechniqueSegmentContextItem(refs, "Add Half Pre-Bend", HalfStepBendSemitones, HalfStepBendSemitones),
            CreateBendTechniqueSegmentContextItem(refs, "Add Full Pre-Bend", FullStepBendSemitones, FullStepBendSemitones),
            CreateBendTechniqueSegmentContextItem(refs, "Add Half Release", HalfStepBendSemitones, 0f),
            CreateBendTechniqueSegmentContextItem(refs, "Add Full Release", FullStepBendSemitones, 0f),
            CreateAddTechniqueSegmentContextItem(refs, NoteTechniqueSegmentType.Slide, "Add Slide"),
            new ContextMenuItem("Clear Timed Techniques", () => ClearTechniqueSegmentsForNotes(refs)),
            CreateNoteFlagContextItem(refs, "Palm Mute", IsPalmMuteEnabled, SetPalmMute),
            CreateNoteFlagContextItem(refs, "Fret-Hand Mute", IsFretHandMuteEnabled, SetFretHandMute),
            CreateNoteFlagContextItem(refs, "Natural Harmonic", note => note.harmonic, (note, value) =>
            {
                note.harmonic = value;
                if (value)
                    note.pinchHarmonic = false;
            }),
            CreateNoteFlagContextItem(refs, "Pinch Harmonic", note => note.pinchHarmonic, (note, value) =>
            {
                note.pinchHarmonic = value;
                if (value)
                    note.harmonic = false;
            }),
            CreateNoteFlagContextItem(refs, "Accent", note => note.accent, (note, value) => note.accent = value),
            CreateNoteFlagContextItem(refs, "Tap", note => note.tap, (note, value) => note.tap = value),
            CreateNoteFlagContextItem(refs, "Tremolo", note => note.tremolo, (note, value) => note.tremolo = value),
            CreateNoteFlagContextItem(refs, "Legato", note => note.legato, (note, value) => note.legato = value),
            CreateNoteFlagContextItem(refs, "Requires Pluck", note => note.requiresPluck, (note, value) => note.requiresPluck = value)
        };
    }

    private ContextMenuItem CreateTechniqueContextItem(List<ChartEditorNoteReference> refs, NoteTechnique technique, string label)
    {
        return new ContextMenuItem(FormatToggleMenuLabel(label, refs.Count(noteRef => IsTechniqueEnabled(noteRef.note, technique)), refs.Count),
            () => ToggleTechniqueForNotes(refs, technique));
    }

    private ContextMenuItem CreateAddTechniqueSegmentContextItem(List<ChartEditorNoteReference> refs, NoteTechniqueSegmentType type, string label)
    {
        return new ContextMenuItem(label, () => AddTechniqueSegmentToNotes(refs, type));
    }

    private ContextMenuItem CreateBendTechniqueSegmentContextItem(List<ChartEditorNoteReference> refs, string label, float startBend, float endBend)
    {
        return new ContextMenuItem(label, () => AddTechniqueSegmentToNotes(refs, NoteTechniqueSegmentType.Bend, segment =>
        {
            segment.startBend = Mathf.Max(0f, startBend);
            segment.endBend = Mathf.Max(0f, endBend);
        }));
    }

    private void AddTechniqueSegmentToNotes(List<ChartEditorNoteReference> refs, NoteTechniqueSegmentType type)
    {
        AddTechniqueSegmentToNotes(refs, type, configure: null);
    }

    private void AddTechniqueSegmentToNotes(List<ChartEditorNoteReference> refs, NoteTechniqueSegmentType type, Action<ChartEditorTechniqueSegment> configure)
    {
        refs = refs?
            .Where(noteRef => noteRef?.note != null)
            .ToList() ?? new List<ChartEditorNoteReference>();
        if (refs.Count == 0)
            return;

        for (int i = 0; i < refs.Count; i++)
            AddTechniqueSegmentToNote(refs[i].note, type, configure: configure);

        project.dirty = true;
        Rebuild();
    }

    private void AddTechniqueSegmentToNote(ChartEditorNote note, NoteTechniqueSegmentType type, float? startOverride = null, Action<ChartEditorTechniqueSegment> configure = null)
    {
        if (note == null)
            return;

        note.techniqueSegments ??= new List<ChartEditorTechniqueSegment>();
        float start = Mathf.Max(0f, startOverride ?? GetTechniqueSegmentMaxEnd(note));
        float projectLimit = Mathf.Max(TechniqueSegmentMinimumSeconds, (float)((project?.DurationSeconds ?? note.timeSeconds + start + 1.0) - note.timeSeconds));
        float length = Mathf.Min(0.5f, Mathf.Max(TechniqueSegmentMinimumSeconds, projectLimit - start));
        if (length <= TechniqueSegmentMinimumSeconds && start > 0f)
        {
            start = Mathf.Max(0f, projectLimit - 0.5f);
            length = Mathf.Max(TechniqueSegmentMinimumSeconds, projectLimit - start);
        }

        ChartEditorTechniqueSegment segment = CreateTechniqueSegment(note, type, start, Mathf.Min(projectLimit, start + length));
        ApplyTechniqueSegmentTypeDefaults(note, segment);
        configure?.Invoke(segment);
        note.techniqueSegments.Add(segment);
        if (segment.type == NoteTechniqueSegmentType.Bend)
            ClearBendPoints(note);
        SyncNoteDurationToTechniqueSegments(note, allowShrink: false);
        NormalizeTechniqueSegmentLayout(note, segment);
        ApplyTechniqueSegmentSummaries(note);
        NormalizePrimaryTechnique(note);
    }

    private void ClearTechniqueSegmentsForNotes(List<ChartEditorNoteReference> refs)
    {
        refs = refs?
            .Where(noteRef => noteRef?.note != null)
            .ToList() ?? new List<ChartEditorNoteReference>();
        if (refs.Count == 0)
            return;

        for (int i = 0; i < refs.Count; i++)
        {
            ChartEditorNote note = refs[i].note;
            if (note?.techniqueSegments?.Any(segment => segment != null && segment.type == NoteTechniqueSegmentType.Bend) == true)
                ClearBendPoints(note);
            note?.techniqueSegments?.Clear();
            NormalizePrimaryTechnique(note);
        }

        project.dirty = true;
        Rebuild();
    }

    private static float GetTechniqueSegmentMaxEnd(ChartEditorNote note)
    {
        if (note?.techniqueSegments == null || note.techniqueSegments.Count == 0)
            return 0f;

        float maxEnd = 0f;
        for (int i = 0; i < note.techniqueSegments.Count; i++)
        {
            ChartEditorTechniqueSegment segment = note.techniqueSegments[i];
            if (segment == null)
                continue;

            maxEnd = Mathf.Max(maxEnd, segment.startOffset, segment.endOffset);
        }

        return maxEnd;
    }

    private static void ApplyTechniqueSegmentTypeDefaults(ChartEditorNote note, ChartEditorTechniqueSegment segment)
    {
        if (note == null || segment == null)
            return;

        int fret = Mathf.Clamp(note.fret, 0, 24);
        segment.startFret = fret;
        segment.endFret = fret;
        segment.startBend = 0f;
        segment.endBend = 0f;

        switch (segment.type)
        {
            case NoteTechniqueSegmentType.Slide:
                segment.endFret = note.slideTargetFret >= 0 ? note.slideTargetFret : Mathf.Clamp(fret + 1, 0, 24);
                note.slideTargetFret = segment.endFret;
                break;
            case NoteTechniqueSegmentType.Bend:
                segment.endBend = Mathf.Max(0.5f, Mathf.Abs(note.bendStep) > 0.01f ? Mathf.Abs(note.bendStep) : 2f);
                note.bendStep = Mathf.Max(note.bendStep, segment.endBend);
                break;
        }
    }

    private ContextMenuItem CreateNoteFlagContextItem(List<ChartEditorNoteReference> refs, string label, Func<ChartEditorNote, bool> getter, Action<ChartEditorNote, bool> setter)
    {
        return new ContextMenuItem(FormatToggleMenuLabel(label, refs.Count(noteRef => getter(noteRef.note)), refs.Count), () =>
        {
            if (refs.Count == 0)
                return;

            bool enable = refs.Any(noteRef => !getter(noteRef.note));
            for (int i = 0; i < refs.Count; i++)
                setter(refs[i].note, enable);

            project.dirty = true;
            Rebuild();
        });
    }

    private static bool IsPalmMuteEnabled(ChartEditorNote note)
    {
        if (note == null)
            return false;

        return note.palmMute || (note.muted && !note.fretHandMute && !note.palmMute);
    }

    private static bool IsFretHandMuteEnabled(ChartEditorNote note)
    {
        return note != null && note.fretHandMute;
    }

    private static void SetPalmMute(ChartEditorNote note, bool enabled)
    {
        if (note == null)
            return;

        note.palmMute = enabled;
        if (enabled)
            note.fretHandMute = false;
        note.muted = note.palmMute || note.fretHandMute;
    }

    private static void SetFretHandMute(ChartEditorNote note, bool enabled)
    {
        if (note == null)
            return;

        note.fretHandMute = enabled;
        if (enabled)
            note.palmMute = false;
        note.muted = note.palmMute || note.fretHandMute;
    }

    private void ShowSustainThenVibratoPopup(List<ChartEditorNoteReference> refs)
    {
        refs = refs?
            .Where(noteRef => noteRef?.note != null)
            .ToList() ?? new List<ChartEditorNoteReference>();
        if (refs.Count == 0)
            return;

        double defaultDuration = refs.Max(noteRef => Math.Max(0.25, GetNoteEffectiveDurationSeconds(noteRef.note)));
        double defaultSustain = Math.Min(2.0, Math.Max(0.0, defaultDuration - 0.25));
        double maxDuration = refs.Min(noteRef => Math.Max(0.01, (project?.DurationSeconds ?? defaultDuration) - noteRef.note.timeSeconds));

        TextField sustainField = CreatePopupTextField("Sustain Before Vibrato Seconds", defaultSustain.ToString("0.000", CultureInfo.InvariantCulture));
        TextField totalField = CreatePopupTextField("Total Note Duration Seconds", Math.Min(defaultDuration, maxDuration).ToString("0.000", CultureInfo.InvariantCulture));

        ShowEditPopup(refs.Count == 1 ? "Sustain Then Vibrato" : $"Sustain Then Vibrato ({refs.Count})",
            new VisualElement[] { sustainField, totalField },
            () =>
            {
                if (!TryParseDoubleInRange(totalField.value, 0.06, Math.Max(0.06, maxDuration), out double totalDuration))
                {
                    SetStatus($"Total duration must be between 0.060 and {maxDuration:0.000} seconds.");
                    return false;
                }

                if (!TryParseDoubleInRange(sustainField.value, 0.0, Math.Max(0.0, totalDuration - 0.05), out double sustainDuration))
                {
                    SetStatus("Sustain before vibrato must leave at least 0.050 seconds for vibrato.");
                    return false;
                }

                for (int i = 0; i < refs.Count; i++)
                    ApplySustainThenVibrato(refs[i].note, sustainDuration, totalDuration);

                foreach (ChartEditorTrack dirtyTrack in refs.Select(noteRef => noteRef.track).Where(dirtyTrack => dirtyTrack != null).Distinct())
                    dirtyTrack.notes = dirtyTrack.notes?.OrderBy(n => n?.timeSeconds ?? 0.0).ThenBy(n => n?.stringOrLane ?? 0).ToList() ?? new List<ChartEditorNote>();

                project.dirty = true;
                HideEditPopup();
                Rebuild();
                return true;
            });
    }

    private static void ApplySustainThenVibrato(ChartEditorNote note, double sustainDuration, double totalDuration)
    {
        if (note == null)
            return;

        SetNoteDurationSeconds(note, totalDuration);
        RemoveTechniqueSegments(note, NoteTechniqueSegmentType.Sustain);
        RemoveTechniqueSegments(note, NoteTechniqueSegmentType.Vibrato);

        float sustainEnd = Mathf.Clamp((float)sustainDuration, 0f, Mathf.Max(0.01f, (float)totalDuration));
        float totalEnd = Mathf.Max(sustainEnd + 0.05f, (float)totalDuration);
        note.techniqueSegments ??= new List<ChartEditorTechniqueSegment>();
        if (sustainEnd > 0.001f)
            note.techniqueSegments.Add(CreateTechniqueSegment(note, NoteTechniqueSegmentType.Sustain, 0f, sustainEnd));
        note.techniqueSegments.Add(CreateTechniqueSegment(note, NoteTechniqueSegmentType.Vibrato, sustainEnd, totalEnd));
        NormalizePrimaryTechnique(note);
    }

    private static ChartEditorTechniqueSegment CreateTechniqueSegment(ChartEditorNote note, NoteTechniqueSegmentType type, float startOffset, float endOffset)
    {
        int fret = Mathf.Clamp(note?.fret ?? 0, 0, 24);
        return new ChartEditorTechniqueSegment
        {
            type = type,
            startOffset = Mathf.Max(0f, startOffset),
            endOffset = Mathf.Max(startOffset, endOffset),
            startFret = fret,
            endFret = fret,
            startBend = 0f,
            endBend = 0f
        };
    }

    private static string FormatToggleMenuLabel(string label, int enabledCount, int totalCount)
    {
        string state = enabledCount <= 0 ? "OFF" : enabledCount >= totalCount ? "ON" : "MIXED";
        return $"{label}: {state}";
    }

    private static bool IsTechniqueEnabled(ChartEditorNote note, NoteTechnique technique)
    {
        if (note == null)
            return false;

        if (note.technique == technique)
            return true;

        switch (technique)
        {
            case NoteTechnique.Slide:
                return note.slideTargetFret >= 0 || HasTechniqueSegment(note, NoteTechniqueSegmentType.Slide);
            case NoteTechnique.Bend:
                return Mathf.Abs(note.bendStep) > 0.01f ||
                       note.bendPreBend ||
                       note.bendRelease ||
                       HasTechniqueSegment(note, NoteTechniqueSegmentType.Bend) ||
                       HasBendBearingTechniqueSegment(note);
            case NoteTechnique.Vibrato:
                return HasTechniqueSegment(note, NoteTechniqueSegmentType.Vibrato);
        }

        return false;
    }

    private static bool HasTechniqueSegment(ChartEditorNote note, NoteTechniqueSegmentType type)
    {
        return note?.techniqueSegments != null &&
               note.techniqueSegments.Any(segment => segment != null && segment.type == type);
    }

    private static bool HasBendBearingTechniqueSegment(ChartEditorNote note)
    {
        if (note?.techniqueSegments == null)
            return false;

        return note.techniqueSegments.Any(IsBendBearingTechniqueSegment);
    }

    private void ToggleTechniqueForNotes(List<ChartEditorNoteReference> refs, NoteTechnique technique)
    {
        if (refs == null || refs.Count == 0)
            return;

        bool disable = refs.All(noteRef => IsTechniqueEnabled(noteRef.note, technique));
        for (int i = 0; i < refs.Count; i++)
        {
            ChartEditorNote note = refs[i].note;
            if (disable)
                RemoveTechniqueFromNote(note, technique);
            else
                ApplyTechniqueToNote(note, technique);
        }

        project.dirty = true;
        Rebuild();
    }

    private static void ApplyTechniqueToNote(ChartEditorNote note, NoteTechnique technique)
    {
        if (note == null)
            return;

        switch (technique)
        {
            case NoteTechnique.HammerOn:
            case NoteTechnique.PullOff:
                note.technique = technique;
                note.legato = true;
                note.requiresPluck = false;
                break;
            case NoteTechnique.Slide:
                if (note.slideTargetFret < 0)
                    note.slideTargetFret = Mathf.Clamp(note.fret + 1, 0, 24);
                EnsureSlideTechniqueSegment(note);
                break;
            case NoteTechnique.Bend:
                if (Mathf.Abs(note.bendStep) <= 0.01f)
                    note.bendStep = 2f;
                EnsureTechniqueSegment(note, NoteTechniqueSegmentType.Bend);
                break;
            case NoteTechnique.Vibrato:
                EnsureTechniqueSegment(note, NoteTechniqueSegmentType.Vibrato);
                break;
        }

        NormalizePrimaryTechnique(note);
    }

    private static void RemoveTechniqueFromNote(ChartEditorNote note, NoteTechnique technique)
    {
        if (note == null)
            return;

        if (note.technique == technique)
            note.technique = NoteTechnique.None;

        switch (technique)
        {
            case NoteTechnique.HammerOn:
            case NoteTechnique.PullOff:
                note.legato = false;
                note.requiresPluck = true;
                break;
            case NoteTechnique.Slide:
                note.slideTargetFret = -1;
                RemoveTechniqueSegments(note, NoteTechniqueSegmentType.Slide);
                break;
            case NoteTechnique.Bend:
                note.bendStep = 0f;
                note.bendPreBend = false;
                note.bendRelease = false;
                note.maxBend = 0f;
                RemoveTechniqueSegments(note, NoteTechniqueSegmentType.Bend);
                ClearBendValuesFromTechniqueSegments(note);
                break;
            case NoteTechnique.Vibrato:
                RemoveTechniqueSegments(note, NoteTechniqueSegmentType.Vibrato);
                break;
        }

        NormalizePrimaryTechnique(note);
    }

    private static void NormalizePrimaryTechnique(ChartEditorNote note)
    {
        if (note == null)
            return;

        if (note.technique == NoteTechnique.HammerOn || note.technique == NoteTechnique.PullOff)
            return;

        if (IsTechniqueEnabled(note, NoteTechnique.Slide))
        {
            note.technique = NoteTechnique.Slide;
            return;
        }

        if (IsTechniqueEnabled(note, NoteTechnique.Bend))
        {
            note.technique = NoteTechnique.Bend;
            return;
        }

        if (IsTechniqueEnabled(note, NoteTechnique.Vibrato))
        {
            note.technique = NoteTechnique.Vibrato;
            return;
        }

        note.technique = NoteTechnique.None;
    }

    private static void EnsureSlideTechniqueSegment(ChartEditorNote note)
    {
        if (note == null)
            return;

        note.techniqueSegments ??= new List<ChartEditorTechniqueSegment>();
        ChartEditorTechniqueSegment segment = note.techniqueSegments.FirstOrDefault(candidate => candidate != null && candidate.type == NoteTechniqueSegmentType.Slide);
        if (segment == null)
        {
            segment = new ChartEditorTechniqueSegment
            {
                type = NoteTechniqueSegmentType.Slide,
                startOffset = 0f,
                endOffset = Mathf.Max(0.05f, (float)Math.Max(0.05, note.durationSeconds))
            };
            note.techniqueSegments.Add(segment);
        }

        segment.startFret = note.fret;
        segment.endFret = note.slideTargetFret >= 0 ? note.slideTargetFret : Mathf.Clamp(note.fret + 1, 0, 24);
        segment.startBend = 0f;
        segment.endBend = 0f;
    }

    private static void EnsureTechniqueSegment(ChartEditorNote note, NoteTechniqueSegmentType type)
    {
        note.techniqueSegments ??= new List<ChartEditorTechniqueSegment>();
        if (note.techniqueSegments.Any(segment => segment != null && segment.type == type))
            return;

        ChartEditorTechniqueSegment segment = CreateTechniqueSegment(
            note,
            type,
            0f,
            Mathf.Max(0.05f, (float)Math.Max(0.05, note.durationSeconds)));
        ApplyTechniqueSegmentTypeDefaults(note, segment);
        note.techniqueSegments.Add(segment);
    }

    private static void RemoveTechniqueSegments(ChartEditorNote note, NoteTechniqueSegmentType type)
    {
        if (note?.techniqueSegments == null)
            return;

        if (type == NoteTechniqueSegmentType.Bend)
            ClearBendPoints(note);
        note.techniqueSegments.RemoveAll(segment => segment != null && segment.type == type);
    }

    private static void ClearBendValuesFromTechniqueSegments(ChartEditorNote note)
    {
        if (note?.techniqueSegments == null)
            return;

        for (int i = 0; i < note.techniqueSegments.Count; i++)
        {
            ChartEditorTechniqueSegment segment = note.techniqueSegments[i];
            if (segment == null)
                continue;

            segment.startBend = 0f;
            segment.endBend = 0f;
        }
    }

    private Button CreateTechniqueButton(ChartEditorNote note, NoteTechnique technique)
    {
        return CreateToggleButton(technique.ToString(), IsTechniqueEnabled(note, technique), () =>
        {
            if (IsTechniqueEnabled(note, technique))
                RemoveTechniqueFromNote(note, technique);
            else
                ApplyTechniqueToNote(note, technique);
            project.dirty = true;
            Rebuild();
        });
    }

    private VisualElement CreatePanelColumn(float width)
    {
        VisualElement panel = new VisualElement();
        panel.style.width = width;
        panel.style.minWidth = width;
        panel.style.paddingLeft = 24f;
        panel.style.paddingRight = 24f;
        panel.style.paddingTop = 24f;
        panel.style.paddingBottom = 24f;
        StylePanel(panel, new Color(0.042f, 0.052f, 0.070f, 0.98f), new Color(0.25f, 0.36f, 0.52f, 0.78f));
        return panel;
    }

    private Label CreateSectionTitle(string text)
    {
        Label label = CreateLabel(text, 32f, new Color(0.84f, 0.94f, 1f, 0.98f), true, TextAnchor.MiddleLeft, false);
        label.style.marginTop = 12f;
        label.style.marginBottom = 18f;
        label.style.letterSpacing = 0f;
        return label;
    }

    private VisualElement CreateKeyValue(string key, string value)
    {
        VisualElement row = new VisualElement();
        row.style.marginBottom = 18f;
        Label keyLabel = CreateLabel(key.ToUpperInvariant(), 24f, new Color(0.66f, 0.78f, 0.90f, 0.90f), true, TextAnchor.MiddleLeft, false);
        keyLabel.style.letterSpacing = 0f;
        Label valueLabel = CreateLabel(value ?? string.Empty, 26f, new Color(0.98f, 0.99f, 1f, 0.99f), false, TextAnchor.MiddleLeft, false);
        valueLabel.style.whiteSpace = WhiteSpace.Normal;
        row.Add(keyLabel);
        row.Add(valueLabel);
        return row;
    }

    private VisualElement CreateInfoRow(string title, string detail, Color? titleColor = null)
    {
        VisualElement row = new VisualElement();
        row.style.marginBottom = 16f;
        row.style.paddingLeft = 18f;
        row.style.paddingRight = 18f;
        row.style.paddingTop = 14f;
        row.style.paddingBottom = 14f;
        row.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        SetRadius(row, 12f);
        SetBorderWidth(row, 1f);
        SetToneLabBorder(row,
            new Color(0.36f, 0.38f, 0.44f, 0.84f),
            new Color(0.22f, 0.24f, 0.30f, 0.90f),
            new Color(0.13f, 0.15f, 0.19f, 0.96f));
        Label titleLabel = CreateLabel(title, 32f, titleColor ?? Color.white, true, TextAnchor.MiddleLeft, false);
        Label detailLabel = CreateLabel(detail, 24f, new Color(0.76f, 0.86f, 0.96f, 0.96f), false, TextAnchor.MiddleLeft, false);
        detailLabel.style.whiteSpace = WhiteSpace.Normal;
        row.Add(titleLabel);
        row.Add(detailLabel);
        return row;
    }

    private Label CreateSmallText(string text, Color color)
    {
        Label label = CreateLabel(text, 24f, color, false, TextAnchor.MiddleLeft, false);
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginBottom = 8f;
        return label;
    }

    private TextField CreateTextField(string label, string value, Action<string> onChange)
    {
        TextField field = new TextField(label);
        field.value = value ?? string.Empty;
        field.style.marginBottom = 16f;
        field.style.unityFontDefinition = bodyFont;
        field.style.fontSize = UiFont(24f);
        StyleTextField(field);
        RegisterTextFieldKeyboardCapture(field);
        field.RegisterValueChangedCallback(evt => onChange?.Invoke(evt.newValue ?? string.Empty));
        return field;
    }

    private static void StyleTextField(TextField field)
    {
        if (field == null)
            return;

        field.style.color = Color.white;
        field.style.backgroundColor = new Color(0.018f, 0.022f, 0.030f, 0.98f);
        field.style.borderTopWidth = 1f;
        field.style.borderRightWidth = 1f;
        field.style.borderBottomWidth = 1f;
        field.style.borderLeftWidth = 1f;
        SetToneLabBorder(field,
            new Color(0.38f, 0.40f, 0.44f, 0.84f),
            new Color(0.22f, 0.24f, 0.28f, 0.95f),
            new Color(0.13f, 0.15f, 0.18f, 1f));
        field.style.borderTopLeftRadius = 11f;
        field.style.borderTopRightRadius = 11f;
        field.style.borderBottomLeftRadius = 11f;
        field.style.borderBottomRightRadius = 11f;
        field.style.paddingLeft = 14f;
        field.style.paddingRight = 14f;

        field.schedule.Execute(() =>
        {
            Label label = field.Q<Label>();
            if (label != null)
            {
                label.style.color = new Color(0.72f, 0.78f, 0.88f, 1f);
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            VisualElement input = field.Q<VisualElement>(TextField.textInputUssName) ?? field.Q<VisualElement>("unity-text-input");
            if (input == null)
                return;

            input.style.color = Color.white;
            input.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            input.style.borderTopWidth = 0f;
            input.style.borderRightWidth = 0f;
            input.style.borderBottomWidth = 0f;
            input.style.borderLeftWidth = 0f;
            input.style.borderTopLeftRadius = 10f;
            input.style.borderTopRightRadius = 10f;
            input.style.borderBottomLeftRadius = 10f;
            input.style.borderBottomRightRadius = 10f;
        });
    }

    private VisualElement CreateCompactRow(params VisualElement[] children)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.marginBottom = 12f;
        for (int i = 0; i < children.Length; i++)
        {
            children[i].style.flexGrow = 1f;
            children[i].style.marginRight = i < children.Length - 1 ? 10f : 0f;
            row.Add(children[i]);
        }

        return row;
    }

    private VisualElement CreatePopupActionGrid(params Button[] buttons)
    {
        VisualElement grid = new VisualElement();
        grid.style.flexDirection = FlexDirection.Row;
        grid.style.flexWrap = Wrap.Wrap;
        grid.style.marginTop = 4f;
        grid.style.marginBottom = 12f;

        foreach (Button button in buttons ?? Array.Empty<Button>())
        {
            if (button == null)
                continue;

            button.style.width = 220f;
            button.style.minWidth = 220f;
            button.style.height = 64f;
            button.style.marginLeft = 0f;
            button.style.marginRight = 12f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 12f;
            button.style.fontSize = UiFont(21f);
            grid.Add(button);
        }

        return grid;
    }

    private Button CreateToolbarButton(string text, Action action)
    {
        Button button = CreateButton(text, action);
        button.style.height = 78f;
        button.style.minWidth = 240f;
        button.style.marginLeft = 8f;
        button.style.marginRight = 8f;
        button.style.fontSize = UiFont(24f);
        return button;
    }

    private Button CreateCompactButton(string text, Action action)
    {
        Button button = CreateButton(text, action);
        button.style.height = 62f;
        button.style.minWidth = 146f;
        button.style.fontSize = UiFont(22f);
        return button;
    }

    private Button CreateToggleButton(string text, bool enabled, Action action)
    {
        Button button = new Button(action) { text = string.Empty };
        button.focusable = false;
        button.style.flexDirection = FlexDirection.Row;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.SpaceBetween;
        button.style.height = 64f;
        button.style.width = Length.Percent(100f);
        button.style.marginBottom = 12f;
        button.style.paddingLeft = 20f;
        button.style.paddingRight = 16f;
        button.style.unityFontDefinition = bodyFont;
        ApplyToggleButtonChrome(button, enabled);

        Label label = CreateLabel(text, 21f, enabled ? new Color(0.88f, 1f, 0.92f, 1f) : new Color(0.78f, 0.84f, 0.92f, 1f), true, TextAnchor.MiddleLeft, false);
        label.style.flexGrow = 1f;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        button.Add(label);

        Label state = CreateLabel(enabled ? "ON" : "OFF", 18f, enabled ? new Color(0.72f, 1f, 0.80f, 1f) : new Color(0.66f, 0.72f, 0.82f, 1f), true, TextAnchor.MiddleRight, false);
        state.style.width = 58f;
        state.style.flexShrink = 0f;
        button.Add(state);
        return button;
    }

    private Button CreatePanelActionButton(string text, Action action, bool primary)
    {
        Button button = CreateButton(text, action);
        button.style.height = 74f;
        button.style.width = Length.Percent(100f);
        button.style.marginLeft = 0f;
        button.style.marginRight = 0f;
        button.style.marginTop = 8f;
        button.style.marginBottom = 8f;
        button.style.fontSize = UiFont(24f);
        if (primary)
            StyleFilledButton(button, new Color(0.64f, 0.38f, 1f, 1f), darkText: false);
        else
            StyleSoftButton(button, new Color(0.70f, 0.78f, 0.88f, 1f));
        return button;
    }

    private static void StyleHeaderAction(Button button)
    {
        if (button == null)
            return;

        button.style.height = 64f;
        button.style.minWidth = 136f;
        button.style.fontSize = UiFont(24f);
    }

    private Button CreateButton(string text, Action action)
    {
        Button button = new Button(action) { text = text };
        button.focusable = false;
        button.style.unityFontDefinition = bodyFont;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.fontSize = UiFont(24f);
        button.style.marginLeft = 7f;
        button.style.marginRight = 7f;
        button.style.paddingLeft = 20f;
        button.style.paddingRight = 20f;
        StyleSoftButton(button, new Color(0.62f, 0.70f, 0.82f, 1f));
        return button;
    }

    private Label CreateLabel(string text, float size, Color color, bool bold, TextAnchor align, bool useTitleFont)
    {
        Label label = new Label(text ?? string.Empty);
        label.style.fontSize = UiFont(size);
        label.style.color = color;
        label.style.unityFontDefinition = useTitleFont ? titleFont : bodyFont;
        label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        label.style.unityTextAlign = align;
        return label;
    }

    private static float UiFont(float size)
    {
        return size * EditorFontScale;
    }

    private static void StylePanel(VisualElement element, Color background, Color border, float radius = 8f)
    {
        element.style.backgroundColor = background;
        SetRadius(element, radius);
        SetBorderWidth(element, 1f);
        SetToneLabBorder(element,
            Color.Lerp(border, Color.white, 0.18f),
            border,
            Color.Lerp(border, Color.black, 0.42f));
    }

    private static void StylePopupPanel(VisualElement element, Color background, float radius = 16f)
    {
        if (element == null)
            return;

        element.style.backgroundColor = background;
        SetRadius(element, radius);
        SetBorderWidth(element, 1f);
        SetBorderColor(element, new Color(1f, 1f, 1f, 0.34f));
    }

    private static void SetElementCursor(VisualElement element, ChartEditorCursorKind cursor)
    {
        if (element == null)
            return;

        UnityEngine.UIElements.Cursor uiCursor = new UnityEngine.UIElements.Cursor
        {
            texture = GetResizeCursorTexture(cursor),
            hotspot = new Vector2(16f, 16f)
        };
        element.style.cursor = new StyleCursor(uiCursor);
    }

    private static Texture2D GetResizeCursorTexture(ChartEditorCursorKind cursor)
    {
        bool horizontal = cursor == ChartEditorCursorKind.ResizeHorizontal;
        Texture2D existing = horizontal ? resizeHorizontalCursorTexture : resizeVerticalCursorTexture;
        if (existing != null)
            return existing;

        Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = Enumerable.Repeat(clear, 32 * 32).ToArray();
        texture.SetPixels(pixels);

        Color shadow = new Color(0f, 0f, 0f, 0.78f);
        Color fill = new Color(0.94f, 0.97f, 1f, 1f);
        DrawResizeCursorGlyph(texture, horizontal, shadow, 1);
        DrawResizeCursorGlyph(texture, horizontal, fill, 0);
        texture.Apply(false, true);

        if (horizontal)
            resizeHorizontalCursorTexture = texture;
        else
            resizeVerticalCursorTexture = texture;

        return texture;
    }

    private static void DrawResizeCursorGlyph(Texture2D texture, bool horizontal, Color color, int offset)
    {
        if (texture == null)
            return;

        void SetPixelSafe(int x, int y)
        {
            x += offset;
            y += offset;
            if (x >= 0 && x < 32 && y >= 0 && y < 32)
                texture.SetPixel(x, y, color);
        }

        if (horizontal)
        {
            for (int x = 8; x <= 23; x++)
            {
                SetPixelSafe(x, 15);
                SetPixelSafe(x, 16);
            }

            for (int i = 0; i < 6; i++)
            {
                SetPixelSafe(8 + i, 15 - i);
                SetPixelSafe(8 + i, 16 + i);
                SetPixelSafe(23 - i, 15 - i);
                SetPixelSafe(23 - i, 16 + i);
            }
        }
        else
        {
            for (int y = 8; y <= 23; y++)
            {
                SetPixelSafe(15, y);
                SetPixelSafe(16, y);
            }

            for (int i = 0; i < 6; i++)
            {
                SetPixelSafe(15 - i, 8 + i);
                SetPixelSafe(16 + i, 8 + i);
                SetPixelSafe(15 - i, 23 - i);
                SetPixelSafe(16 + i, 23 - i);
            }
        }
    }

    private sealed class ButtonChromeState
    {
        public Color RestBackground;
        public Color RestText;
        public Color RestBorderTop;
        public Color RestBorderSide;
        public Color RestBorderBottom;
        public Color HoverBackground;
        public Color HoverText;
        public Color HoverBorder;
        public float HoverScale = 1.02f;
    }

    private static void StyleSoftButton(Button button, Color accent)
    {
        if (button == null)
            return;

        SetRadius(button, 11f);
        SetButtonChrome(
            button,
            new Color(0f, 0f, 0f, 0f),
            Color.Lerp(new Color(0.84f, 0.86f, 0.90f, 0.98f), accent, 0.18f),
            new Color(0.36f, 0.38f, 0.42f, 0.90f),
            new Color(0.24f, 0.26f, 0.30f, 0.98f),
            new Color(0.18f, 0.20f, 0.23f, 0.98f),
            new Color(1f, 1f, 1f, 0.075f),
            Color.white,
            new Color(accent.r, accent.g, accent.b, 0.58f));
    }

    private static void StyleFilledButton(Button button, Color fill, bool darkText)
    {
        if (button == null)
            return;

        SetRadius(button, 11f);
        Color restText = darkText ? new Color(0.03f, 0.04f, 0.06f, 1f) : Color.white;
        SetButtonChrome(
            button,
            fill,
            restText,
            Color.Lerp(fill, Color.white, 0.34f),
            Color.Lerp(fill, Color.black, 0.08f),
            Color.Lerp(fill, Color.black, 0.30f),
            Color.Lerp(fill, Color.white, 0.11f),
            Color.white,
            Color.Lerp(fill, Color.white, 0.44f));
    }

    private static void StyleDangerButton(Button button)
    {
        if (button == null)
            return;

        SetRadius(button, 11f);
        SetButtonChrome(
            button,
            new Color(0f, 0f, 0f, 0f),
            new Color(1f, 0.66f, 0.66f, 1f),
            new Color(0.62f, 0.30f, 0.30f, 1f),
            new Color(0.44f, 0.20f, 0.20f, 1f),
            new Color(0.34f, 0.14f, 0.14f, 1f),
            new Color(0.62f, 0.08f, 0.10f, 0.28f),
            Color.white,
            new Color(0.92f, 0.32f, 0.34f, 0.95f));
    }

    private static void StyleIconPillButton(Button button, Color accent)
    {
        if (button == null)
            return;

        SetRadius(button, 10f);
        SetButtonChrome(
            button,
            new Color(0f, 0f, 0f, 0f),
            accent,
            new Color(0.36f, 0.38f, 0.42f, 0.90f),
            new Color(0.24f, 0.26f, 0.30f, 0.98f),
            new Color(0.18f, 0.20f, 0.23f, 0.98f),
            new Color(1f, 1f, 1f, 0.075f),
            Color.white,
            new Color(accent.r, accent.g, accent.b, 0.56f));
        button.style.paddingLeft = 0f;
        button.style.paddingRight = 0f;
    }

    private static void ApplyToggleButtonChrome(Button button, bool enabled)
    {
        if (button == null)
            return;

        SetRadius(button, 11f);
        if (enabled)
        {
            SetButtonChrome(
                button,
                new Color(0.060f, 0.115f, 0.078f, 0.88f),
                new Color(0.84f, 1f, 0.89f, 1f),
                new Color(0.36f, 0.58f, 0.41f, 0.98f),
                new Color(0.19f, 0.35f, 0.24f, 1f),
                new Color(0.14f, 0.27f, 0.18f, 1f),
                new Color(0.16f, 0.34f, 0.22f, 0.82f),
                Color.white,
                new Color(0.48f, 0.82f, 0.56f, 0.95f));
        }
        else
        {
            SetButtonChrome(
                button,
                new Color(0f, 0f, 0f, 0f),
                new Color(0.78f, 0.84f, 0.92f, 1f),
                new Color(0.36f, 0.38f, 0.42f, 0.82f),
                new Color(0.22f, 0.24f, 0.28f, 0.95f),
                new Color(0.14f, 0.16f, 0.20f, 1f),
                new Color(1f, 1f, 1f, 0.070f),
                Color.white,
                new Color(0.70f, 0.76f, 0.86f, 0.56f));
        }
    }

    private static void SetButtonChrome(
        Button button,
        Color restBackground,
        Color restText,
        Color restBorderTop,
        Color restBorderSide,
        Color restBorderBottom,
        Color hoverBackground,
        Color hoverText,
        Color hoverBorder,
        float hoverScale = 1.02f)
    {
        if (button == null)
            return;

        ButtonChromeState state = button.userData as ButtonChromeState;
        if (state == null)
        {
            state = new ButtonChromeState();
            button.userData = state;
            button.RegisterCallback<MouseEnterEvent>(_ => ApplyButtonChromeHover(button, state));
            button.RegisterCallback<MouseLeaveEvent>(_ => ApplyButtonChromeRest(button, state));
        }

        state.RestBackground = restBackground;
        state.RestText = restText;
        state.RestBorderTop = restBorderTop;
        state.RestBorderSide = restBorderSide;
        state.RestBorderBottom = restBorderBottom;
        state.HoverBackground = hoverBackground;
        state.HoverText = hoverText;
        state.HoverBorder = hoverBorder;
        state.HoverScale = hoverScale;
        button.style.opacity = 0.96f;
        button.style.scale = new Scale(Vector3.one);
        ApplyButtonChromeRest(button, state);
    }

    private static void ApplyButtonChromeRest(Button button, ButtonChromeState state)
    {
        if (button == null || state == null)
            return;

        SetBorderWidth(button, 1f);
        button.style.backgroundColor = state.RestBackground;
        button.style.color = state.RestText;
        SetToneLabBorder(button, state.RestBorderTop, state.RestBorderSide, state.RestBorderBottom);
        button.style.opacity = 0.96f;
        button.style.scale = new Scale(Vector3.one);
    }

    private static void ApplyButtonChromeHover(Button button, ButtonChromeState state)
    {
        if (button == null || state == null)
            return;

        button.style.backgroundColor = state.HoverBackground;
        button.style.color = state.HoverText;
        SetToneLabBorder(button,
            state.HoverBorder,
            state.HoverBorder,
            Color.Lerp(state.HoverBorder, Color.black, 0.30f));
        button.style.opacity = 1f;
        button.style.scale = new Scale(new Vector3(state.HoverScale, state.HoverScale, 1f));
    }

    private static void SetRadius(VisualElement element, float radius)
    {
        element.style.borderTopLeftRadius = radius;
        element.style.borderTopRightRadius = radius;
        element.style.borderBottomLeftRadius = radius;
        element.style.borderBottomRightRadius = radius;
    }

    private static void SetBorderWidth(VisualElement element, float width)
    {
        element.style.borderTopWidth = width;
        element.style.borderRightWidth = width;
        element.style.borderBottomWidth = width;
        element.style.borderLeftWidth = width;
    }

    private static void SetBorderColor(VisualElement element, Color color)
    {
        element.style.borderTopColor = color;
        element.style.borderRightColor = color;
        element.style.borderBottomColor = color;
        element.style.borderLeftColor = color;
    }

    private static void SetToneLabBorder(VisualElement element, Color top, Color side, Color bottom)
    {
        if (element == null)
            return;

        element.style.borderTopColor = top;
        element.style.borderRightColor = side;
        element.style.borderLeftColor = side;
        element.style.borderBottomColor = bottom;
    }

    private static void ApplyChartSliderStyle(Slider slider)
    {
        if (slider == null)
            return;

        slider.style.height = 28f;
        slider.style.marginTop = 4f;
        slider.style.marginBottom = 0f;
        slider.style.backgroundColor = Color.clear;
        slider.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            VisualElement input = slider.Q<VisualElement>(className: "unity-base-field__input");
            if (input != null)
            {
                input.style.backgroundColor = Color.clear;
                input.style.borderTopWidth = 0f;
                input.style.borderRightWidth = 0f;
                input.style.borderBottomWidth = 0f;
                input.style.borderLeftWidth = 0f;
            }

            VisualElement dragContainer = slider.Q<VisualElement>(className: "unity-base-slider__drag-container");
            if (dragContainer != null)
            {
                dragContainer.style.height = 8f;
                dragContainer.style.backgroundColor = new Color(0.12f, 0.13f, 0.16f, 0.95f);
                SetRadius(dragContainer, 4f);
            }

            VisualElement tracker = slider.Q<VisualElement>(className: "unity-base-slider__tracker");
            if (tracker != null)
            {
                tracker.style.backgroundColor = new Color(0.62f, 0.38f, 1f, 0.95f);
                SetRadius(tracker, 4f);
            }

            VisualElement dragger = slider.Q<VisualElement>(className: "unity-base-slider__dragger");
            if (dragger != null)
            {
                dragger.style.width = 16f;
                dragger.style.height = 16f;
                dragger.style.backgroundColor = new Color(0.95f, 0.96f, 0.98f, 1f);
                SetRadius(dragger, 8f);
                SetBorderWidth(dragger, 1f);
                SetToneLabBorder(dragger,
                    new Color(1f, 1f, 1f, 0.92f),
                    new Color(0.42f, 0.44f, 0.50f, 1f),
                    new Color(0.30f, 0.32f, 0.38f, 1f));
            }
        });
    }

    private void SetStatus(string text)
    {
        statusLabel.text = text ?? string.Empty;
    }

    private static string FormatTime(double seconds)
    {
        seconds = Math.Max(0.0, seconds);
        int minutes = (int)(seconds / 60.0);
        double remainder = seconds - minutes * 60.0;
        return $"{minutes:00}:{remainder:00.000}";
    }

    private static string LaneLabel(int lane)
    {
        string[] labels = { "K", "S", "HH", "T1", "T2", "FT", "C", "R" };
        return lane >= 0 && lane < labels.Length ? labels[lane] : lane.ToString();
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return string.Empty;

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return string.Empty;
    }

    private static Color ParseColor(string html, Color fallback)
    {
        return !string.IsNullOrWhiteSpace(html) && ColorUtility.TryParseHtmlString(html, out Color parsed)
            ? parsed
            : fallback;
    }
}
