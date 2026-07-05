using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Profiling;
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

    private enum ChartEditorPopupKind
    {
        None,
        Generic,
        SaveOptions,
        UnsavedClosePrompt
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

    private sealed class TimelineNotesMeshElement : VisualElement
    {
        private struct NoteQuad
        {
            public float x;
            public float y;
            public float width;
            public float height;
            public Color color;
        }

        private const int MaxQuadsPerMesh = 4000;
        private readonly List<NoteQuad> quads = new List<NoteQuad>();

        public TimelineNotesMeshElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        public void AddQuad(float x, float y, float width, float height, Color color)
        {
            quads.Add(new NoteQuad { x = x, y = y, width = Mathf.Max(1f, width), height = height, color = color });
        }

        public void Commit()
        {
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            int index = 0;
            while (index < quads.Count)
            {
                int count = Mathf.Min(MaxQuadsPerMesh, quads.Count - index);
                MeshWriteData mesh = context.Allocate(count * 4, count * 6);
                ushort vertexBase = 0;
                for (int i = 0; i < count; i++)
                {
                    NoteQuad quad = quads[index + i];
                    Color32 tint = quad.color;
                    mesh.SetNextVertex(new Vertex { position = new Vector3(quad.x, quad.y, 0f), tint = tint });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(quad.x + quad.width, quad.y, 0f), tint = tint });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(quad.x + quad.width, quad.y + quad.height, 0f), tint = tint });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(quad.x, quad.y + quad.height, 0f), tint = tint });
                    mesh.SetNextIndex(vertexBase);
                    mesh.SetNextIndex((ushort)(vertexBase + 1));
                    mesh.SetNextIndex((ushort)(vertexBase + 2));
                    mesh.SetNextIndex(vertexBase);
                    mesh.SetNextIndex((ushort)(vertexBase + 2));
                    mesh.SetNextIndex((ushort)(vertexBase + 3));
                    vertexBase += 4;
                }

                index += count;
            }
        }
    }

    private sealed class ToneMarkerVisual
    {
        public ChartEditorToneChange change;
        public VisualElement hit;
        public VisualElement line;
        public VisualElement cap;
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
    private const string SidebarDifficultiesKey = "difficulties";
    private const string SidebarTonesKey = "tones";
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
    private const float ToneMarkerHitWidth = 38f;
    private const float ToneMarkerCapWidth = 136f;
    private const float ToneMarkerCapHeight = 40f;
    private const float ToneMarkerLaneTop = WaveformTop + 18f;
    private const float ToneMarkerLaneHeight = 58f;
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
    private const float ToneEditorPreferredHeight = 560f;
    private const float ToneEditorMinHeight = 460f;
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
    private readonly Button toneEditorButton;
    private readonly Button saveButton;
    private readonly Button transportPlayButton;
    private VisualElement transportPlayIcon;
    private VisualElement transportPauseIcon;
    private VisualElement leftPanelElement;
    private VisualElement editorLoadingOverlay;
    private UnityToneLabRuntime cachedToneLabRuntime;
    private VisualElement timelinePanelElement;
    private bool timelineRefreshQueued;
    private float builtTimelineWindowStartPx = float.MinValue;
    private float builtTimelineWindowEndPx = float.MaxValue;
    private bool timelineScrollRestorePending;
    private VisualElement timelineWindowedLayer;
    private bool timelineWindowRefillQueued;
    private int timelineNoteBuildGeneration;
    private static readonly ProfilerMarker RebuildMarker = new ProfilerMarker("ChartEditor.Rebuild");
    private static readonly ProfilerMarker TimelinePanelMarker = new ProfilerMarker("ChartEditor.RefreshTimelinePanel");
    private static readonly ProfilerMarker WindowRefillMarker = new ProfilerMarker("ChartEditor.RefreshTimelineWindowContent");
    private static readonly ProfilerMarker LeftPanelMarker = new ProfilerMarker("ChartEditor.RefreshLeftPanel");
    private static readonly ProfilerMarker BuildNotesMarker = new ProfilerMarker("ChartEditor.BuildNotes.Gather");
    private static readonly ProfilerMarker NoteChunkMarker = new ProfilerMarker("ChartEditor.BuildNoteChunk");
    private static readonly ProfilerMarker CompactRowMarker = new ProfilerMarker("ChartEditor.BuildCompactTrackRow");
    private static readonly ProfilerMarker ToneWorkspaceMarker = new ProfilerMarker("ChartEditor.RefreshToneEditorPanel");
    private static readonly ProfilerMarker BeatGridVisualsMarker = new ProfilerMarker("ChartEditor.UpdateBeatGridVisuals");
    private static readonly ProfilerMarker NoteTimingVisualsMarker = new ProfilerMarker("ChartEditor.UpdateNoteTimingVisuals");
    private VisualElement toneMarkerTimelineElement;
    private readonly List<VisualElement> toneMarkerLaneElements = new List<VisualElement>();
    private Label playbackSpeedLabel;
    private Slider playbackSpeedSlider;

    private ChartEditorProject project;
    private float cachedProjectDurationSeconds;
    private int cachedProjectDurationFrame = -1;
    private ChartEditorProject cachedProjectDurationSource;
    private ChartEditorScreen screen = ChartEditorScreen.Startup;
    private ChartEditorMode mode = ChartEditorMode.SyncTiming;
    private List<string> currentWarnings = new List<string>();
    private string selectedNoteId;
    private readonly HashSet<string> selectedNoteIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<ChartEditorNoteHit> currentNoteHits = new List<ChartEditorNoteHit>();
    private readonly Dictionary<string, VisualElement> currentNoteBlocks = new Dictionary<string, VisualElement>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (Color baseColor, bool selectedTrack)> currentNoteBlockStyles = new Dictionary<string, (Color, bool)>(StringComparer.OrdinalIgnoreCase);
    private sealed class NoteBlockTimingRef
    {
        public VisualElement block;
        public ChartEditorTrack track;
        public ChartEditorNote note;
        public int laneCount;
        public bool selectedTrack;
    }
    private readonly List<NoteBlockTimingRef> currentNoteBlockTimings = new List<NoteBlockTimingRef>();
    private readonly List<TechniqueSegmentVisual> currentTechniqueSegmentVisuals = new List<TechniqueSegmentVisual>();
    private readonly List<ChartEditorCopiedNote> noteClipboard = new List<ChartEditorCopiedNote>();
    private readonly List<BeatMarkerVisual> currentBeatMarkerVisuals = new List<BeatMarkerVisual>();
    private readonly List<ToneMarkerVisual> currentToneMarkerVisuals = new List<ToneMarkerVisual>();
    private string selectedSectionId;
    private string selectedSyncPointId;
    private ChartEditorToneChange selectedToneChange;
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
    private ChartEditorPopupKind editPopupKind = ChartEditorPopupKind.None;
    private VisualElement saveSuccessPopupElement;
    private bool closeAfterSuccessfulSave;
    private double lastTapTempoRealtime = -1.0;
    private double tapTempoAverageIntervalSeconds;
    private bool marqueeSelecting;
    private int marqueePointerId = -1;
    private Vector2 marqueeStart;
    private bool marqueeMoved;
    private bool marqueeToggleSelection;
    private VisualElement marqueeTimeline;
    private VisualElement marqueeBox;
    private readonly Dictionary<string, SidebarExpansionAnimation> sidebarExpansionAnimations = new Dictionary<string, SidebarExpansionAnimation>(StringComparer.OrdinalIgnoreCase);
    private bool sectionsExpanded;
    private bool tracksExpanded = true;
    private bool difficultiesExpanded;
    private bool toneChangesExpanded;
    private bool anchorsExpanded;
    private bool projectInfoExpanded;
    private bool toneEditorEnabled;
    private bool toneLabPanelFocused;
    private string toneLabSelectedPedalInstanceId = string.Empty;
    private ChartEditorToneLabEmbeddedView.SidePanelMode toneLabSidePanelMode = ChartEditorToneLabEmbeddedView.SidePanelMode.Presets;
    private string toneLabLoadedToneKey = string.Empty;
    private string appliedToneEditorPlaybackKey = string.Empty;
    private string toneLabWorkingLibraryPresetId = string.Empty;
    private bool toneLabWorkingToneEditedAfterLibrarySelection;
    private bool seekDragging;
    private bool seekWasPlaying;
    private IGuitarGameplayRenderer highwayPreviewRenderer;
    private GuitarHighway3DRenderHost highwayPreviewGuitarHost;
    private ArcadeHighway3DRenderHost highwayPreviewArcadeHost;
    private ChartEditorTrackRole? highwayPreviewRendererRole;
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
    // While a timeline drag is live, the 3D preview keeps rendering its last
    // snapshot instead of rebuilding note GameObjects every dirty tick.
    private bool suppressHighwayPreviewRebuild;
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
        header.style.height = 128f;
        header.style.minHeight = 128f;
        header.style.paddingLeft = 28f;
        header.style.paddingRight = 24f;
        header.style.backgroundColor = new Color(0.083f, 0.080f, 0.094f, 1f);
        header.style.borderBottomWidth = 2f;
        header.style.borderBottomColor = new Color(0.75f, 0.72f, 0.82f, 0.14f);

        VisualElement CreateHeaderSeparator()
        {
            VisualElement separator = new VisualElement();
            separator.style.width = 2f;
            separator.style.minWidth = 2f;
            separator.style.height = 60f;
            separator.style.flexShrink = 0f;
            separator.style.alignSelf = Align.Center;
            separator.style.backgroundColor = new Color(0.75f, 0.72f, 0.82f, 0.12f);
            SetRadius(separator, 1f);
            return separator;
        }

        VisualElement brandBlock = new VisualElement();
        brandBlock.style.width = 400f;
        brandBlock.style.minWidth = 400f;
        brandBlock.style.height = 76f;
        brandBlock.style.justifyContent = Justify.Center;
        brandBlock.style.alignItems = Align.FlexStart;
        brandBlock.style.paddingRight = 28f;
        brandBlock.Add(CreateHeaderStringTheoryWordmark(30f));

        VisualElement songBlock = new VisualElement();
        songBlock.style.width = 560f;
        songBlock.style.minWidth = 440f;
        songBlock.style.paddingLeft = 30f;
        songBlock.style.paddingRight = 30f;
        headerTitleLabel = CreateLabel("String Theory", 30f, new Color(0.97f, 0.96f, 0.98f, 1f), true, TextAnchor.MiddleLeft, false);
        headerTitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        headerSubtitleLabel = CreateLabel(string.Empty, 20f, new Color(0.60f, 0.58f, 0.66f, 1f), false, TextAnchor.MiddleLeft, false);
        headerSubtitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        headerSubtitleLabel.style.marginTop = 2f;
        songBlock.Add(headerTitleLabel);
        songBlock.Add(headerSubtitleLabel);

        VisualElement transportBlock = new VisualElement();
        transportBlock.style.flexDirection = FlexDirection.Row;
        transportBlock.style.alignItems = Align.Center;
        transportBlock.style.justifyContent = Justify.Center;
        transportBlock.style.paddingLeft = 26f;
        transportBlock.style.paddingRight = 26f;

        VisualElement transportGroup = new VisualElement();
        transportGroup.style.flexDirection = FlexDirection.Row;
        transportGroup.style.alignItems = Align.Center;
        transportGroup.style.paddingLeft = 8f;
        transportGroup.style.paddingRight = 8f;
        transportGroup.style.paddingTop = 8f;
        transportGroup.style.paddingBottom = 8f;
        transportGroup.style.backgroundColor = new Color(0.040f, 0.038f, 0.046f, 1f);
        SetRadius(transportGroup, 14f);
        SetBorderWidth(transportGroup, 2f);
        SetBorderColor(transportGroup, new Color(0.75f, 0.72f, 0.82f, 0.16f));

        transportGroup.Add(CreateHeaderIconButton(NewProjectIconKind.SkipStart, () => SeekTransportTo(0.0), false, grouped: true));
        transportPlayButton = CreateHeaderIconButton(NewProjectIconKind.Play, TogglePlayback, true, grouped: true);
        transportPlayIcon = transportPlayButton.childCount > 0 ? transportPlayButton.ElementAt(0) : null;
        transportPauseIcon = CreateNewProjectIcon(NewProjectIconKind.Pause, Color.white, 68f * 0.48f);
        transportPauseIcon.style.display = DisplayStyle.None;
        transportPlayButton.Add(transportPauseIcon);
        transportGroup.Add(transportPlayButton);
        transportGroup.Add(CreateHeaderIconButton(NewProjectIconKind.SkipEnd, () => SeekTransportTo(GetProjectDurationSeconds()), false, grouped: true));
        transportGroup.Add(CreateHeaderIconButton(NewProjectIconKind.Stop, StopPlayback, false, grouped: true));
        transportBlock.Add(transportGroup);

        VisualElement speedControl = CreatePlaybackSpeedControl(out playbackSpeedLabel, out playbackSpeedSlider);
        transportBlock.Add(speedControl);

        VisualElement timeBlock = new VisualElement();
        timeBlock.style.width = 500f;
        timeBlock.style.minWidth = 420f;
        timeBlock.style.justifyContent = Justify.Center;
        timeBlock.style.paddingLeft = 38f;
        timeBlock.style.paddingRight = 38f;
        headerTimeLabel = CreateLabel("00:00.000 / 00:00.000", 30f, new Color(0.97f, 0.96f, 0.98f, 1f), true, TextAnchor.MiddleCenter, false);
        headerTimeLabel.style.whiteSpace = WhiteSpace.NoWrap;
        timeBlock.Add(headerTimeLabel);
        VisualElement progressTrack = new VisualElement();
        progressTrack.style.height = 6f;
        progressTrack.style.marginTop = 12f;
        progressTrack.style.backgroundColor = new Color(1f, 1f, 1f, 0.09f);
        SetRadius(progressTrack, 3f);
        progressTrack.style.overflow = Overflow.Hidden;
        headerProgressFill = new VisualElement();
        headerProgressFill.style.height = 6f;
        headerProgressFill.style.width = Length.Percent(0f);
        headerProgressFill.style.backgroundColor = new Color(0.62f, 0.38f, 1f, 1f);
        SetRadius(headerProgressFill, 3f);
        progressTrack.Add(headerProgressFill);
        timeBlock.Add(progressTrack);

        VisualElement headerActions = new VisualElement();
        headerActions.style.flexDirection = FlexDirection.Row;
        headerActions.style.alignItems = Align.Center;
        headerActions.style.justifyContent = Justify.FlexEnd;
        headerActions.style.minWidth = 620f;
        headerActions.style.flexGrow = 1f;
        headerActions.style.paddingLeft = 26f;

        toneEditorButton = CreateHeaderActionButton("Tone Editor", ToggleToneEditorFromHeader, false);
        toneEditorButton.RegisterCallback<MouseEnterEvent>(_ => ApplyToneEditorHeaderButtonState());
        toneEditorButton.RegisterCallback<MouseLeaveEvent>(_ => ApplyToneEditorHeaderButtonState());
        saveButton = CreateHeaderActionButton("Save", () => ShowSaveOptionsPopup(), true);
        Button settingsButton = CreateHeaderIconButton(NewProjectIconKind.Gear, ShowProjectSettingsPopup, false);
        Button closeButton = CreateHeaderIconButton(NewProjectIconKind.Cross, RequestCloseFromUi, false);

        headerActions.Add(toneEditorButton);
        headerActions.Add(saveButton);
        headerActions.Add(settingsButton);
        headerActions.Add(closeButton);
        header.Add(brandBlock);
        header.Add(CreateHeaderSeparator());
        header.Add(songBlock);
        header.Add(CreateHeaderSeparator());
        header.Add(transportBlock);
        header.Add(CreateHeaderSeparator());
        header.Add(timeBlock);
        header.Add(CreateHeaderSeparator());
        header.Add(headerActions);

        contentHost = new VisualElement();
        contentHost.style.flexGrow = 1f;
        contentHost.style.minHeight = 0f;
        contentHost.style.paddingLeft = 18f;
        contentHost.style.paddingRight = 18f;
        contentHost.style.paddingTop = 18f;

        statusLabel = CreateLabel(string.Empty, 26f, new Color(0.78f, 0.76f, 0.82f, 1f), false, TextAnchor.MiddleLeft, false);
        statusLabel.style.height = 60f;
        statusLabel.style.minHeight = 60f;
        statusLabel.style.paddingLeft = 24f;
        statusLabel.style.paddingRight = 24f;
        statusLabel.style.backgroundColor = new Color(0.083f, 0.080f, 0.094f, 1f);
        statusLabel.style.borderTopWidth = 2f;
        statusLabel.style.borderTopColor = new Color(0.75f, 0.72f, 0.82f, 0.14f);
        statusLabel.style.whiteSpace = WhiteSpace.NoWrap;

        RootElement.Add(header);
        RootElement.Add(contentHost);
        RootElement.schedule.Execute(CheckTimelineWindowRefill).Every(200);
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
        control.style.width = 300f;
        control.style.minWidth = 300f;
        control.style.height = 84f;
        control.style.marginLeft = 20f;
        control.style.justifyContent = Justify.Center;
        control.style.paddingLeft = 22f;
        control.style.paddingRight = 22f;
        control.style.backgroundColor = new Color(0.040f, 0.038f, 0.046f, 1f);
        SetRadius(control, 14f);
        SetBorderWidth(control, 2f);
        SetBorderColor(control, new Color(0.75f, 0.72f, 0.82f, 0.16f));

        VisualElement labelRow = new VisualElement();
        labelRow.style.flexDirection = FlexDirection.Row;
        labelRow.style.justifyContent = Justify.SpaceBetween;
        labelRow.style.alignItems = Align.Center;

        Label title = CreateLabel("SPEED", 17f, new Color(0.60f, 0.58f, 0.66f, 1f), true, TextAnchor.MiddleLeft, false);
        title.style.letterSpacing = 2f;
        speedLabel = CreateLabel("100%", 21f, new Color(0.97f, 0.96f, 0.98f, 1f), true, TextAnchor.MiddleRight, false);
        speedLabel.style.width = 80f;
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

        if (owner != null && owner.ConsumeChartEditorCloseRequestFromUi())
        {
            RequestCloseFromUi();
            AdvancePlayback(Mathf.Max(0f, deltaTime));
            UpdateToneEditorPlaybackOverride();
            UpdateHighwayPreview();
            return;
        }

        if (HandleOverlayKeyboardInput())
        {
            AdvancePlayback(Mathf.Max(0f, deltaTime));
            UpdateToneEditorPlaybackOverride();
            UpdateHighwayPreview();
            return;
        }

        HandleKeyboardShortcuts();
        AdvancePlayback(Mathf.Max(0f, deltaTime));
        UpdateToneEditorPlaybackOverride();
        UpdateHighwayPreview();
    }

    public void RequestCloseFromUi()
    {
        HideContextMenu();
        HideSaveSuccessPopup();

        if (project != null && project.dirty)
        {
            ShowUnsavedClosePrompt();
            return;
        }

        owner?.CloseChartEditorToMainMenuFromUi();
    }

    private void ShowUnsavedClosePrompt()
    {
        HideEditPopup();

        VisualElement overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.right = 0f;
        overlay.style.top = 0f;
        overlay.style.bottom = 0f;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.48f);
        overlay.pickingMode = PickingMode.Position;
        overlay.RegisterCallback<PointerDownEvent>(evt =>
        {
            HideEditPopup();
            evt.StopPropagation();
        });

        VisualElement panel = new VisualElement();
        panel.style.width = 720f;
        panel.style.paddingLeft = 36f;
        panel.style.paddingRight = 36f;
        panel.style.paddingTop = 34f;
        panel.style.paddingBottom = 32f;
        StyleStrongPopupPanel(panel, new Color(0.030f, 0.036f, 0.048f, 1f), 18f);
        panel.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

        Label title = CreateLabel("Unsaved Changes", 36f, Color.white, true, TextAnchor.MiddleLeft, false);
        title.style.marginBottom = 12f;
        panel.Add(title);

        Label body = CreateLabel("You have unsaved changes. Save before exiting, or discard the changes and close the chart editor.", 22f, new Color(0.76f, 0.82f, 0.91f, 1f), false, TextAnchor.MiddleLeft, false);
        body.style.whiteSpace = WhiteSpace.Normal;
        body.style.marginBottom = 28f;
        panel.Add(body);

        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.justifyContent = Justify.FlexEnd;
        actions.style.alignItems = Align.Center;

        Button cancel = CreatePopupDialogButton("Cancel", HideEditPopup, new Color(0.70f, 0.78f, 0.88f, 1f));
        cancel.style.minWidth = 126f;
        cancel.style.height = 54f;
        cancel.style.marginRight = 12f;

        Button discard = CreatePopupDialogButton("Discard Changes", DiscardChangesAndClose, new Color(1f, 0.44f, 0.38f, 1f));
        discard.style.minWidth = 218f;
        discard.style.height = 54f;
        discard.style.marginRight = 12f;

        Button save = CreatePopupDialogButton("Save", () => ShowSaveOptionsPopup(closeAfterSave: true), new Color(0.62f, 0.38f, 1f, 1f), filled: true);
        save.style.minWidth = 132f;
        save.style.height = 54f;

        actions.Add(cancel);
        actions.Add(discard);
        actions.Add(save);
        panel.Add(actions);

        overlay.Add(panel);
        editPopupElement = overlay;
        editPopupKind = ChartEditorPopupKind.UnsavedClosePrompt;
        RootElement.Add(editPopupElement);
        editPopupElement.BringToFront();
        SetChartEditorKeyboardCaptureActive(true);
    }

    private void DiscardChangesAndClose()
    {
        closeAfterSuccessfulSave = false;
        HideEditPopup();
        owner?.CloseChartEditorToMainMenuFromUi();
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
        // Release the duration cache's strong reference so the closed
        // project's note graph can be collected at the startup screen.
        cachedProjectDurationSource = null;
        cachedProjectDurationFrame = -1;
        suppressHighwayPreviewRebuild = false;
        screen = ChartEditorScreen.Startup;
        mode = ChartEditorMode.SyncTiming;
        currentWarnings = new List<string>();
        ClearNoteSelection();
        noteClipboard.Clear();
        selectedSectionId = null;
        selectedSyncPointId = null;
        selectedToneChange = null;
        sidebarExpansionAnimations.Clear();
        tracksExpanded = true;
        difficultiesExpanded = false;
        toneChangesExpanded = false;
        sectionsExpanded = false;
        anchorsExpanded = false;
        projectInfoExpanded = false;
        ClearToneEditorPlaybackOverride();
        toneEditorEnabled = false;
        toneLabPanelFocused = false;
        toneLabSelectedPedalInstanceId = string.Empty;
        toneLabSidePanelMode = ChartEditorToneLabEmbeddedView.SidePanelMode.Presets;
        toneLabLoadedToneKey = string.Empty;
        toneLabWorkingLibraryPresetId = string.Empty;
        toneLabWorkingToneEditedAfterLibrarySelection = false;
        appliedToneEditorPlaybackKey = string.Empty;
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
        using ProfilerMarker.AutoScope rebuildScope = RebuildMarker.Auto();
        // A full rebuild destroys any in-flight timeline drag's elements, so
        // never leave the 3D preview frozen behind an abandoned drag flag.
        suppressHighwayPreviewRebuild = false;
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
        currentNoteBlockStyles.Clear();
        currentNoteBlockTimings.Clear();
        currentTechniqueSegmentVisuals.Clear();
        currentToneMarkerVisuals.Clear();
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
            : $"{FormatTime(project.cursorTimeSeconds)} / {FormatTime(GetProjectDurationSeconds())}";
        UpdateHeaderProgress();
        bool hasProject = project != null;
        ApplyToneEditorHeaderButtonState();
        saveButton.SetEnabled(hasProject);
        toneEditorButton.SetEnabled(hasProject);
        transportPlayButton.SetEnabled(hasProject);
        UpdateTransportPlayButtonIcon();

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
        Color accentPurple = new Color(0.62f, 0.38f, 1f, 1f);
        Color panelBackground = new Color(0.072f, 0.069f, 0.080f, 1f);
        Color bandBackground = new Color(0.100f, 0.096f, 0.112f, 1f);
        Color hairline = new Color(0.75f, 0.72f, 0.82f, 0.16f);
        Color textPrimary = new Color(0.97f, 0.96f, 0.98f, 1f);
        Color textMuted = new Color(0.78f, 0.76f, 0.82f, 1f);
        Color textFaint = new Color(0.60f, 0.58f, 0.66f, 1f);

        VisualElement shell = new VisualElement();
        shell.style.flexGrow = 1f;
        shell.style.alignItems = Align.Center;
        shell.style.justifyContent = Justify.Center;

        VisualElement panel = new VisualElement();
        panel.style.width = 1560f;
        panel.style.maxWidth = Length.Percent(92f);
        panel.style.maxHeight = Length.Percent(88f);
        panel.style.flexDirection = FlexDirection.Column;
        panel.style.backgroundColor = panelBackground;
        panel.style.overflow = Overflow.Hidden;
        SetRadius(panel, 26f);
        SetBorderWidth(panel, 3f);
        SetBorderColor(panel, new Color(0.78f, 0.75f, 0.88f, 0.36f));

        VisualElement headerBand = new VisualElement();
        headerBand.style.flexDirection = FlexDirection.Row;
        headerBand.style.alignItems = Align.Center;
        headerBand.style.flexShrink = 0f;
        headerBand.style.backgroundColor = bandBackground;
        headerBand.style.paddingLeft = 44f;
        headerBand.style.paddingRight = 44f;
        headerBand.style.paddingTop = 34f;
        headerBand.style.paddingBottom = 34f;
        headerBand.style.borderBottomWidth = 2f;
        headerBand.style.borderBottomColor = hairline;

        headerBand.Add(CreateNewProjectIconTile(NewProjectIconKind.Note, accentPurple, 92f));

        VisualElement headerText = new VisualElement();
        headerText.style.marginLeft = 26f;
        headerText.style.flexGrow = 1f;
        headerText.style.flexShrink = 1f;
        headerText.style.minWidth = 0f;
        headerText.Add(CreateLabel("Get Started", 42f, textPrimary, true, TextAnchor.MiddleLeft, false));
        Label subtitle = CreateLabel("Create a new project, or open an existing chart.", 22f, textMuted, false, TextAnchor.MiddleLeft, false);
        subtitle.style.whiteSpace = WhiteSpace.Normal;
        subtitle.style.marginTop = 6f;
        headerText.Add(subtitle);
        headerBand.Add(headerText);
        panel.Add(headerBand);

        ScrollView body = new ScrollView(ScrollViewMode.Vertical);
        ConfigureScrollView(body);
        body.style.flexGrow = 1f;
        body.style.minHeight = 0f;
        body.contentContainer.style.paddingLeft = 44f;
        body.contentContainer.style.paddingRight = 44f;
        body.contentContainer.style.paddingTop = 40f;
        body.contentContainer.style.paddingBottom = 28f;
        body.contentContainer.style.flexShrink = 0f;

        body.Add(CreateStartupHeroAction(
            "New Project",
            "Start from a blank chart, or import Guitar Pro / MusicXML with audio.",
            ShowNewProjectPopup));

        Label openCaption = CreateLabel("OPEN EXISTING", 19f, textFaint, true, TextAnchor.MiddleLeft, false);
        openCaption.style.letterSpacing = 3f;
        openCaption.style.marginTop = 40f;
        openCaption.style.marginBottom = 18f;
        openCaption.style.flexShrink = 0f;
        body.Add(openCaption);

        body.Add(CreateStartupAction("Open .theory Package", ImportTheoryPackage));
        AddExternalImporterStartupActions(body);
        body.Add(CreateStartupAction("Open Unpacked Chart Folder", ImportFolder));

        panel.Add(body);
        shell.Add(panel);
        contentHost.Add(shell);
        SetStatus("Ready.");
    }

    private Button CreateStartupHeroAction(string label, string description, Action action)
    {
        Color accent = new Color(0.62f, 0.38f, 1f, 1f);
        Button button = new Button(action) { text = string.Empty };
        button.focusable = false;
        button.style.width = Length.Percent(100f);
        button.style.flexDirection = FlexDirection.Row;
        button.style.alignItems = Align.Center;
        button.style.flexShrink = 0f;
        button.style.marginLeft = 0f;
        button.style.marginRight = 0f;
        button.style.marginTop = 0f;
        button.style.marginBottom = 0f;
        button.style.paddingLeft = 34f;
        button.style.paddingRight = 34f;
        button.style.paddingTop = 30f;
        button.style.paddingBottom = 30f;
        button.style.unityFontDefinition = bodyFont;
        SetRadius(button, 20f);

        button.Add(CreateNewProjectIconTile(NewProjectIconKind.Plus, accent, 84f));

        VisualElement text = new VisualElement();
        text.style.marginLeft = 26f;
        text.style.flexGrow = 1f;
        text.style.flexShrink = 1f;
        text.style.minWidth = 0f;
        Label title = CreateLabel(label, 32f, new Color(0.97f, 0.96f, 1f, 1f), true, TextAnchor.MiddleLeft, false);
        title.style.whiteSpace = WhiteSpace.NoWrap;
        text.Add(title);
        Label detail = CreateLabel(description, 21f, new Color(0.80f, 0.78f, 0.95f, 1f), false, TextAnchor.MiddleLeft, false);
        detail.style.whiteSpace = WhiteSpace.Normal;
        detail.style.marginTop = 6f;
        text.Add(detail);
        button.Add(text);

        VisualElement chevron = CreateNewProjectIcon(NewProjectIconKind.ChevronRight, new Color(0.85f, 0.80f, 1f, 0.95f), 44f);
        chevron.style.marginLeft = 20f;
        button.Add(chevron);

        void Apply(bool hover)
        {
            button.style.backgroundColor = hover
                ? new Color(accent.r * 0.30f, accent.g * 0.30f, accent.b * 0.34f, 1f)
                : new Color(accent.r * 0.20f, accent.g * 0.20f, accent.b * 0.25f, 1f);
            SetBorderWidth(button, 3f);
            SetBorderColor(button, new Color(accent.r, accent.g, accent.b, hover ? 0.85f : 0.55f));
            button.style.scale = hover ? new Scale(new Vector3(1.01f, 1.01f, 1f)) : new Scale(Vector3.one);
        }

        Apply(false);
        button.RegisterCallback<MouseEnterEvent>(_ => Apply(true));
        button.RegisterCallback<MouseLeaveEvent>(_ => Apply(false));
        return button;
    }

    private VisualElement CreateStartupAction(string label, Action action)
    {
        bool isFolder = label.IndexOf("Folder", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isPackage = label.IndexOf("Package", StringComparison.OrdinalIgnoreCase) >= 0;
        Color accent = isFolder
            ? new Color(0.52f, 0.84f, 0.72f, 1f)
            : isPackage
                ? new Color(0.98f, 0.72f, 0.36f, 1f)
                : new Color(0.48f, 0.74f, 1f, 1f);
        NewProjectIconKind icon = isFolder ? NewProjectIconKind.Folder : NewProjectIconKind.File;

        Button button = new Button(action) { text = string.Empty };
        button.focusable = false;
        button.style.width = Length.Percent(100f);
        button.style.flexDirection = FlexDirection.Row;
        button.style.alignItems = Align.Center;
        button.style.flexShrink = 0f;
        button.style.marginLeft = 0f;
        button.style.marginRight = 0f;
        button.style.marginTop = 0f;
        button.style.marginBottom = 16f;
        button.style.paddingLeft = 26f;
        button.style.paddingRight = 26f;
        button.style.paddingTop = 22f;
        button.style.paddingBottom = 22f;
        button.style.unityFontDefinition = bodyFont;
        SetRadius(button, 16f);

        button.Add(CreateNewProjectIconTile(icon, accent, 64f));

        Label title = CreateLabel(label, 26f, new Color(0.94f, 0.93f, 0.96f, 1f), true, TextAnchor.MiddleLeft, false);
        title.style.flexGrow = 1f;
        title.style.flexShrink = 1f;
        title.style.minWidth = 0f;
        title.style.marginLeft = 22f;
        title.style.whiteSpace = WhiteSpace.Normal;
        button.Add(title);

        VisualElement chevron = CreateNewProjectIcon(NewProjectIconKind.ChevronRight, new Color(0.63f, 0.61f, 0.70f, 0.90f), 36f);
        chevron.style.marginLeft = 18f;
        button.Add(chevron);

        void Apply(bool hover)
        {
            button.style.backgroundColor = hover
                ? new Color(0.068f, 0.064f, 0.078f, 1f)
                : new Color(0.042f, 0.040f, 0.048f, 1f);
            SetBorderWidth(button, 2f);
            SetBorderColor(button, hover
                ? new Color(accent.r, accent.g, accent.b, 0.60f)
                : new Color(0.75f, 0.72f, 0.82f, 0.22f));
            button.style.scale = hover ? new Scale(new Vector3(1.01f, 1.01f, 1f)) : new Scale(Vector3.one);
        }

        Apply(false);
        button.RegisterCallback<MouseEnterEvent>(_ => Apply(true));
        button.RegisterCallback<MouseLeaveEvent>(_ => Apply(false));
        return button;
    }

    private void AddExternalImporterStartupActions(VisualElement panel)
    {
        List<SongImporterDescriptor> importers = SongImporterRegistry.GetAvailableImporters(forceRefresh: true);
        for (int i = 0; i < importers.Count; i++)
        {
            SongImporterDescriptor importer = importers[i];
            if (importer == null || !SongImporterRegistry.ImporterHasUsableEntrypoint(importer))
                continue;

            string importerId = importer.Id;
            bool hasFolderSignatures = HasImporterFolderSignatures(importer);
            if (HasImporterFileExtensions(importer))
            {
                string label = BuildImporterFileActionLabel(importer, hasFolderSignatures);
                panel.Add(CreateStartupAction(label, () => ImportExternalImporterFile(importerId)));
            }

            if (!hasFolderSignatures)
                continue;

            for (int signatureIndex = 0; signatureIndex < importer.FolderSignatures.Count; signatureIndex++)
            {
                SongImporterFolderSignature signature = importer.FolderSignatures[signatureIndex];
                if (signature == null)
                    continue;

                int capturedSignatureIndex = signatureIndex;
                string label = BuildImporterFolderActionLabel(importer, signature);
                panel.Add(CreateStartupAction(label, () => ImportExternalImporterFolder(importerId, capturedSignatureIndex)));
            }
        }
    }

    private static bool HasImporterFileExtensions(SongImporterDescriptor importer)
    {
        return importer?.Extensions != null &&
               importer.Extensions.Any(extension => !string.IsNullOrWhiteSpace(extension));
    }

    private static bool HasImporterFolderSignatures(SongImporterDescriptor importer)
    {
        return importer?.FolderSignatures != null &&
               importer.FolderSignatures.Any(signature => signature != null);
    }

    private static string BuildImporterFileActionLabel(SongImporterDescriptor importer, bool hasFolderSignatures)
    {
        string displayName = FirstNonEmpty(importer?.DisplayName, "Importer");
        return hasFolderSignatures ? $"Import {displayName} File" : $"Import {displayName}";
    }

    private static string BuildImporterFolderActionLabel(
        SongImporterDescriptor importer,
        SongImporterFolderSignature signature)
    {
        string displayName = FirstNonEmpty(signature?.displayName, importer?.DisplayName, "Importer");
        return $"Import {displayName} Folder";
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
        timelinePanelElement = BuildTimelinePanel();
        center.Add(timelinePanelElement);
        center.Add(BuildTimelinePreviewSplitter());
        center.Add(BuildHighwayPreviewPanel());

        leftPanelElement = BuildLeftPanel();
        main.Add(leftPanelElement);
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
        splitter.style.backgroundColor = new Color(0.016f, 0.015f, 0.019f, 0.92f);
        splitter.pickingMode = PickingMode.Position;
        SetElementCursor(splitter, ChartEditorCursorKind.ResizeVertical);

        VisualElement grip = new VisualElement();
        grip.style.width = 120f;
        grip.style.height = 8f;
        grip.style.backgroundColor = new Color(0.80f, 0.78f, 0.88f, 0.30f);
        grip.pickingMode = PickingMode.Ignore;
        SetRadius(grip, 999f);
        splitter.Add(grip);

        AddPreviewSplitterDragHandlers(splitter);
        return splitter;
    }

    private VisualElement BuildHighwayPreviewPanel()
    {
        if (toneEditorEnabled)
            return BuildToneEditorPanel();

        EnsureHighwayPreviewTexture();

        VisualElement panel = new VisualElement();
        highwayPreviewPanelElement = panel;
        ApplyHighwayPreviewHeight(ClampHighwayPreviewHeight(highwayPreviewPanelHeight));
        panel.style.minHeight = HighwayPreviewMinHeight;
        panel.style.flexShrink = 0f;
        panel.style.flexDirection = FlexDirection.Row;
        panel.style.alignItems = Align.Stretch;
        panel.style.marginTop = 12f;
        panel.style.marginRight = 18f;

        VisualElement previewFrame = new VisualElement();
        previewFrame.style.flexGrow = 1f;
        previewFrame.style.minWidth = 0f;
        previewFrame.style.height = Length.Percent(100f);
        previewFrame.style.minHeight = 0f;
        previewFrame.style.backgroundColor = new Color(0.006f, 0.006f, 0.009f, 1f);
        previewFrame.style.overflow = Overflow.Hidden;
        SetRadius(previewFrame, 20f);
        SetBorderWidth(previewFrame, 2f);
        SetBorderColor(previewFrame, new Color(0.75f, 0.72f, 0.82f, 0.18f));

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
        overlay.style.left = 22f;
        overlay.style.top = 20f;
        overlay.style.paddingLeft = 22f;
        overlay.style.paddingRight = 22f;
        overlay.style.paddingTop = 14f;
        overlay.style.paddingBottom = 14f;
        overlay.style.backgroundColor = new Color(0.030f, 0.029f, 0.036f, 0.80f);
        SetRadius(overlay, 14f);
        SetBorderWidth(overlay, 2f);
        SetBorderColor(overlay, new Color(0.75f, 0.72f, 0.82f, 0.14f));
        highwayPreviewTitleLabel = CreateLabel("3D Highway Preview", 30f, new Color(0.97f, 0.96f, 0.98f, 1f), true, TextAnchor.MiddleLeft, false);
        highwayPreviewTitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        highwayPreviewMetaLabel = CreateLabel(string.Empty, 21f, new Color(0.72f, 0.70f, 0.78f, 1f), false, TextAnchor.MiddleLeft, false);
        highwayPreviewMetaLabel.style.whiteSpace = WhiteSpace.Normal;
        highwayPreviewMetaLabel.style.marginTop = 5f;
        overlay.Add(highwayPreviewTitleLabel);
        overlay.Add(highwayPreviewMetaLabel);
        previewFrame.Add(overlay);
        panel.Add(previewFrame);

        UpdateHighwayPreview();
        return panel;
    }

    private VisualElement BuildToneEditorPanel()
    {
        DisposeHighwayPreview();
        EnsureToneSelectionForCurrentTrack();

        VisualElement panel = new VisualElement();
        highwayPreviewPanelElement = panel;
        ApplyHighwayPreviewHeight(ClampHighwayPreviewHeight(Mathf.Max(highwayPreviewPanelHeight, ToneEditorPreferredHeight)));
        panel.style.minHeight = ToneEditorMinHeight;
        panel.style.flexShrink = 0f;
        panel.style.flexDirection = FlexDirection.Row;
        panel.style.alignItems = Align.Stretch;
        panel.style.marginTop = 10f;
        panel.style.marginRight = 18f;
        panel.style.paddingLeft = 12f;
        panel.style.paddingRight = 12f;
        panel.style.paddingTop = 12f;
        panel.style.paddingBottom = 12f;
        StylePanel(panel, new Color(0.026f, 0.036f, 0.042f, 0.99f), new Color(0.00f, 0.48f, 0.44f, 0.74f), 0f);
        panel.RegisterCallback<PointerDownEvent>(_ => FocusToneLabPanel());

        UnityToneLabRuntime runtime = GetToneLabRuntime();
        if (runtime == null)
        {
            Label unavailable = CreateLabel("Tone Lab unavailable", 30f, new Color(0.82f, 0.88f, 0.92f, 1f), true, TextAnchor.MiddleCenter, false);
            unavailable.style.flexGrow = 1f;
            unavailable.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(unavailable);
            return panel;
        }

        ChartEditorToneLabEmbeddedView embedded = new ChartEditorToneLabEmbeddedView(new ChartEditorToneLabEmbeddedView.Options
        {
            Runtime = runtime,
            SelectedPedalInstanceId = toneLabSelectedPedalInstanceId,
            SidePanel = toneLabSidePanelMode,
            SetSelectedPedalInstanceId = value => toneLabSelectedPedalInstanceId = value ?? string.Empty,
            SetSidePanel = mode => toneLabSidePanelMode = mode,
            Focus = FocusToneLabPanel,
            MarkWorkingToneChanged = () =>
            {
                toneLabWorkingToneEditedAfterLibrarySelection = true;
                // Keep the loaded-tone key so the selected switch does not reload
                // its saved tone over unassigned edits in the embedded Tone Lab.
                appliedToneEditorPlaybackKey = string.Empty;
            },
            MarkPresetSelected = presetId =>
            {
                toneLabWorkingLibraryPresetId = presetId ?? string.Empty;
                toneLabWorkingToneEditedAfterLibrarySelection = false;
                appliedToneEditorPlaybackKey = string.Empty;
            },
            IsWorkingToneCustom = () =>
                toneLabWorkingToneEditedAfterLibrarySelection ||
                string.IsNullOrWhiteSpace(runtime.CurrentPresetId),
            RequestRebuild = () => RefreshToneEditorPanelOnly(),
            AssignToSelectedToneSwitch = () =>
            {
                EnsureToneSelectionForCurrentTrack();
                if (selectedToneChange != null)
                    AssignCurrentToneToChange(selectedToneChange);
                else
                    AddToneChangeAtCursor();
            },
            AddToneSwitchAtCursor = AddToneChangeAtCursor,
            SetStatus = SetStatus,
            RegisterTextFieldKeyboardCapture = RegisterTextFieldKeyboardCapture,
            BodyFont = bodyFont,
            TitleFont = titleFont,
            FontScale = EditorFontScale
        });
        panel.Add(embedded.Root);
        return panel;
    }

    private void FocusToneLabPanel()
    {
        if (!toneEditorEnabled)
            return;

        toneLabPanelFocused = true;
        ClearToneEditorPlaybackOverride();
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
        highwayPreviewPanelElement.style.minHeight = toneEditorEnabled ? ToneEditorMinHeight : HighwayPreviewMinHeight;
        highwayPreviewPanelElement.MarkDirtyRepaint();
    }

    private float ClampHighwayPreviewHeight(float height)
    {
        float minHeight = toneEditorEnabled ? ToneEditorMinHeight : HighwayPreviewMinHeight;
        float maxHeight = HighwayPreviewDefaultHeight * 1.7f;
        if (chartEditorCenterElement != null && chartEditorCenterElement.resolvedStyle.height > 1f)
        {
            maxHeight = Mathf.Max(
                minHeight,
                chartEditorCenterElement.resolvedStyle.height - HighwayPreviewMinTimelineHeight - HighwayPreviewSplitterHeight);
        }

        return Mathf.Clamp(height, minHeight, Mathf.Max(minHeight, maxHeight));
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
        bool drumPreview = frame.role == ChartEditorTrackRole.Drums;

        if (highwayPreviewRenderer != null && highwayPreviewRendererRole.HasValue && highwayPreviewRendererRole.Value != frame.role)
        {
            highwayPreviewRenderer.DisposeRenderer();
            highwayPreviewRenderer = null;
            highwayPreviewSignature = null;
        }

        if (drumPreview)
        {
            if (highwayPreviewArcadeHost == null)
            {
                highwayPreviewArcadeHost = new ArcadeHighway3DRenderHost
                {
                    Camera = highwayPreviewCamera,
                    TargetTexture = highwayPreviewTexture,
                    ManualRender = true,
                    EnableBackground = false,
                    EnableHighwayCharacter = false,
                    EnableSongHeaderOverlay = false,
                    EnableDrumKit = false,
                    RenderLayer = 29,
                    RootName = "ChartEditorDrumHighwayPreviewRendererRoot",
                    LaneCountOverride = frame.laneCount
                };
            }

            highwayPreviewArcadeHost.Camera = highwayPreviewCamera;
            highwayPreviewArcadeHost.TargetTexture = highwayPreviewTexture;
            highwayPreviewArcadeHost.LaneCountOverride = frame.laneCount;
        }
        else
        {
            if (highwayPreviewGuitarHost == null)
            {
                highwayPreviewGuitarHost = new GuitarHighway3DRenderHost
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

            highwayPreviewGuitarHost.Camera = highwayPreviewCamera;
            highwayPreviewGuitarHost.TargetTexture = highwayPreviewTexture;
            highwayPreviewGuitarHost.RenderableStringCountOverride = frame.laneCount;
            highwayPreviewGuitarHost.FretLightColumnCountOverride = null;
        }

        signature ??= BuildHighwayPreviewSignature(frame, project);
        previewSections ??= BuildHighwayPreviewTabSections(project, frame.notes);

        if (highwayPreviewRenderer == null)
        {
            highwayPreviewRenderer = drumPreview
                ? new ArcadeHighway3DRenderer(highwayPreviewArcadeHost)
                : new GuitarHighway3DRenderer(highwayPreviewGuitarHost);
            highwayPreviewRenderer.Initialize(owner, frame.notes, previewSections);
            highwayPreviewRendererRole = frame.role;
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
        if (toneEditorEnabled)
            return;

        if (!visible || screen != ChartEditorScreen.Editor || project == null)
            return;

        bool previewDataDirty = cachedHighwayPreviewFrame == null || cachedHighwayPreviewRevision != highwayPreviewRevision;
        if (suppressHighwayPreviewRebuild && cachedHighwayPreviewFrame != null)
            previewDataDirty = false;
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
            highwayPreviewRendererRole = null;
            highwayPreviewSignature = null;
            ClearHighwayPreviewTexture();
        }
        else
        {
            frame.songTime = Mathf.Max(0f, (float)cursorTime);
            frame.songDurationSeconds = Mathf.Max(0.1f, GetProjectDurationSeconds());
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
                string laneLabel = track?.role == ChartEditorTrackRole.Drums ? "lanes" : "strings";
                highwayPreviewMetaLabel.text = $"{tuning}  ·  {laneCount} {laneLabel}  ·  {track?.notes?.Count ?? 0} notes  ·  {FormatTime(project.cursorTimeSeconds)}";
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
        bool drumPreview = frame.role == ChartEditorTrackRole.Drums;
        List<GameplayNoteState> noteStates = frame.notes != null
            ? (drumPreview ? new List<GameplayNoteState>() : frame.notes.Select(note => BuildHighwayPreviewNoteState(note, songTime)).ToList())
            : new List<GameplayNoteState>();
        List<ArcadeNoteState> arcadeNoteStates = drumPreview && frame.notes != null
            ? frame.notes.Select(note => BuildHighwayPreviewArcadeNoteState(note, frame.laneCount, songTime)).ToList()
            : new List<ArcadeNoteState>();

        return new GuitarGameplaySnapshot
        {
            gameplayMode = drumPreview ? GuitarGameplayMode.Arcade : GuitarGameplayMode.Guitar,
            songLibraryType = drumPreview ? SongLibraryType.Arcade : SongLibraryType.Guitar,
            songTime = songTime,
            songDurationSeconds = Mathf.Max(0.1f, frame.songDurationSeconds),
            isPaused = !editorPlaying,
            playbackSpeedPercent = 100f,
            tabSpeedOffsetPercent = 100f,
            noteStates = noteStates,
            arcadeLaneCount = drumPreview ? frame.laneCount : 0,
            arcadeNoteStates = arcadeNoteStates,
            selectedArcadeArrangementId = drumPreview ? "drums" : string.Empty,
            selectedArcadeArrangementDisplayName = drumPreview ? "Drums" : string.Empty,
            selectedArcadeInstrument = drumPreview ? ArcadeInstrument.Drums : ArcadeInstrument.Guitar,
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

    private static ArcadeNoteState BuildHighwayPreviewArcadeNoteState(NoteData note, int laneCount, float songTime)
    {
        int lane = Mathf.Clamp(note.stringIdx, 0, Math.Max(0, laneCount - 1));
        ArcadeNoteState state = new ArcadeNoteState(new ArcadeNoteData(
            note.id,
            Mathf.Max(0f, note.time),
            Mathf.Max(0f, note.duration),
            0f,
            lane,
            openNote: false,
            tapNote: false,
            assignedChordId: note.chordId));

        float noteEnd = note.time + Mathf.Max(0f, note.duration);
        if (songTime > noteEnd + 0.03f)
        {
            state.result = GameplayNoteResult.Hit;
            state.resolvedAt = note.time;
        }

        return state;
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
            hash = (hash * 31) + (int)frame.role;
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
                    // These feed the renderer's creation-time caches (slide/bend
                    // ribbon endpoints, legato curves, chord grouping) — edits
                    // to them must invalidate the cached preview.
                    hash = (hash * 31) + (note.requiresPluck ? 1 : 0);
                    hash = (hash * 31) + note.linkedFromNoteId;
                    hash = (hash * 31) + note.chordId;
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
        highwayPreviewGuitarHost = null;
        highwayPreviewArcadeHost = null;
        highwayPreviewRendererRole = null;
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
        leftPanelElement = null;
        cachedToneLabRuntime = null;
        timelinePanelElement = null;
        toneMarkerTimelineElement = null;
        toneMarkerLaneElements.Clear();
        timelineWindowedLayer = null;
        timelineNoteBuildGeneration++;
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
            if (toneEditorEnabled)
                SetToneEditorEnabled(false, rebuild: false);
            mode = tabMode;
            ClearNoteSelection();
            selectedSectionId = null;
            selectedSyncPointId = null;
            selectedToneChange = null;
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

    private void ToggleToneEditorFromHeader()
    {
        SetToneEditorEnabled(!toneEditorEnabled, rebuild: true);
    }

    private void SetToneEditorEnabled(bool enabled, bool rebuild)
    {
        if (toneEditorEnabled == enabled && (!enabled || toneChangesExpanded))
        {
            if (rebuild)
                Rebuild();
            return;
        }

        toneEditorEnabled = enabled;
        toneLabPanelFocused = false;
        if (enabled)
        {
            ClearNoteSelection();
            selectedSectionId = null;
            selectedSyncPointId = null;
            ClearAnchorSelection();
            tracksExpanded = false;
            difficultiesExpanded = false;
            sectionsExpanded = false;
            anchorsExpanded = false;
            projectInfoExpanded = false;
            toneChangesExpanded = true;
            toneLabSidePanelMode = ChartEditorToneLabEmbeddedView.SidePanelMode.Presets;
            highwayPreviewPanelHeight = Mathf.Max(highwayPreviewPanelHeight, ToneEditorPreferredHeight);
            EnsureToneSelectionForCurrentTrack();
            LoadSelectedToneIntoToneLab(forceReload: true);
            SetStatus("Tone editor enabled.");
        }
        else
        {
            selectedToneChange = null;
            toneLabPanelFocused = false;
            toneLabSelectedPedalInstanceId = string.Empty;
            toneLabSidePanelMode = ChartEditorToneLabEmbeddedView.SidePanelMode.Presets;
            toneLabLoadedToneKey = string.Empty;
            toneLabWorkingLibraryPresetId = string.Empty;
            toneLabWorkingToneEditedAfterLibrarySelection = false;
            ClearToneEditorPlaybackOverride();
            appliedToneEditorPlaybackKey = string.Empty;
            SetStatus(project?.dirty == true ? "Unsaved changes." : "Project saved.");
        }

        if (rebuild)
            Rebuild();
        else
            ApplyToneEditorHeaderButtonState();
    }

    private void ApplyToneEditorHeaderButtonState()
    {
        if (toneEditorButton == null)
            return;

        toneEditorButton.text = toneEditorEnabled ? "Tone Editor On" : "Tone Editor";
        SetBorderWidth(toneEditorButton, 2f);
        if (toneEditorEnabled)
        {
            toneEditorButton.style.backgroundColor = new Color(0.00f, 0.42f, 0.37f, 0.32f);
            toneEditorButton.style.color = Color.white;
            SetBorderColor(toneEditorButton, new Color(0.10f, 0.85f, 0.74f, 0.60f));
        }
        else
        {
            toneEditorButton.style.backgroundColor = Color.clear;
            toneEditorButton.style.color = new Color(1f, 0.70f, 0.30f, 1f);
            SetBorderColor(toneEditorButton, new Color(1f, 0.62f, 0.22f, 0.74f));
        }
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
        panel.style.backgroundColor = new Color(0.055f, 0.053f, 0.063f, 1f);
        SetRadius(panel, 18f);
        SetBorderWidth(panel, 2f);
        SetBorderColor(panel, new Color(0.75f, 0.72f, 0.82f, 0.16f));

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
        ChartEditorTrackViewGroup selectedGroup = GetSelectedTrackViewGroup(groups);
        int difficultyCount = selectedGroup?.tracks?.Count ?? 0;
        int toneChangeCount = GetSelectedTrackToneChanges().Count;
        panel.Add(CreateCollapsibleSidebarSection(
            SidebarDifficultiesKey,
            "DIFFICULTIES",
            difficultiesExpanded,
            ToggleDifficultiesExpanded,
            () => CreateDifficultiesSidebarContent(selectedGroup),
            EstimateDifficultiesSidebarContentHeight(selectedGroup),
            difficultyCount > 1 ? difficultyCount.ToString(CultureInfo.InvariantCulture) : string.Empty));
        panel.Add(CreateCollapsibleSidebarSection(
            SidebarTonesKey,
            "TONE CHANGES",
            toneChangesExpanded,
            ToggleToneChangesExpanded,
            CreateToneChangesSidebarContent,
            EstimateToneChangesSidebarContentHeight(),
            toneChangeCount > 0 ? toneChangeCount.ToString(CultureInfo.InvariantCulture) : string.Empty,
            AddToneChangeAtCursor));
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
        section.style.flexShrink = 0f;
        section.style.backgroundColor = new Color(0.096f, 0.092f, 0.108f, 1f);
        SetRadius(section, 14f);
        SetBorderWidth(section, 2f);
        SetBorderColor(section, new Color(0.75f, 0.72f, 0.82f, 0.16f));
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
        row.style.paddingLeft = 22f;
        row.style.paddingRight = 16f;

        if (collapsible && toggle != null)
        {
            row.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                toggle();
                evt.StopPropagation();
            });
            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = new Color(1f, 1f, 1f, 0.03f));
            row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = Color.clear);
        }

        VisualElement labelGroup = new VisualElement();
        labelGroup.style.flexDirection = FlexDirection.Row;
        labelGroup.style.alignItems = Align.Center;
        labelGroup.style.flexGrow = 1f;
        labelGroup.style.minWidth = 0f;

        Label label = CreateLabel(title.ToUpperInvariant(), 19f, new Color(0.62f, 0.60f, 0.70f, 1f), true, TextAnchor.MiddleLeft, false);
        label.style.letterSpacing = 2f;
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
            actions.Add(CreateSidebarSectionIconButton(NewProjectIconKind.Plus, addAction));

        if (collapsible && toggle != null)
        {
            VisualElement chevron = CreateNewProjectIcon(NewProjectIconKind.ChevronRight, new Color(0.62f, 0.60f, 0.70f, 0.95f), 30f);
            chevron.style.marginLeft = 12f;
            chevron.style.rotate = expanded ? new Rotate(90f) : new Rotate(0f);
            actions.Add(chevron);
        }
        else if (!string.IsNullOrWhiteSpace(metadata))
        {
            actions.Add(CreateSidebarSectionMetadataButton(metadata));
        }

        row.Add(actions);
        return row;
    }

    private Button CreateSidebarSectionIconButton(NewProjectIconKind icon, Action action)
    {
        Button button = new Button(action) { text = string.Empty };
        button.focusable = false;
        button.style.width = 44f;
        button.style.height = 44f;
        button.style.minWidth = 44f;
        button.style.minHeight = 44f;
        button.style.marginLeft = 8f;
        button.style.marginRight = 0f;
        button.style.marginTop = 0f;
        button.style.marginBottom = 0f;
        button.style.paddingLeft = 0f;
        button.style.paddingRight = 0f;
        button.style.paddingTop = 0f;
        button.style.paddingBottom = 0f;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.Center;
        button.style.flexShrink = 0f;
        SetRadius(button, 10f);
        button.Add(CreateNewProjectIcon(icon, new Color(0.83f, 0.82f, 0.88f, 0.95f), 22f));

        void Apply(bool hover)
        {
            button.style.backgroundColor = hover ? new Color(1f, 1f, 1f, 0.09f) : new Color(1f, 1f, 1f, 0.03f);
            SetBorderWidth(button, 2f);
            SetBorderColor(button, hover ? new Color(0.80f, 0.78f, 0.88f, 0.50f) : new Color(0.75f, 0.72f, 0.82f, 0.20f));
        }

        Apply(false);
        button.RegisterCallback<MouseEnterEvent>(_ => Apply(true));
        button.RegisterCallback<MouseLeaveEvent>(_ => Apply(false));
        button.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
        return button;
    }

    private VisualElement CreateSidebarMetadataPill(string text)
    {
        Label pill = CreateLabel(text, 16f, new Color(0.78f, 0.76f, 0.82f, 1f), true, TextAnchor.MiddleCenter, false);
        pill.style.height = 30f;
        pill.style.minWidth = 34f;
        pill.style.marginLeft = 12f;
        pill.style.paddingLeft = 10f;
        pill.style.paddingRight = 10f;
        pill.style.backgroundColor = new Color(1f, 1f, 1f, 0.05f);
        SetRadius(pill, 9f);
        SetBorderWidth(pill, 2f);
        SetBorderColor(pill, new Color(0.75f, 0.72f, 0.82f, 0.20f));
        return pill;
    }

    private VisualElement CreateSidebarSectionMetadataButton(string text)
    {
        Label label = CreateLabel(text, 17f, new Color(1f, 0.80f, 0.56f, 1f), true, TextAnchor.MiddleCenter, false);
        label.style.width = 48f;
        label.style.height = 42f;
        label.style.marginLeft = 8f;
        label.style.backgroundColor = new Color(1f, 0.65f, 0.30f, 0.10f);
        SetRadius(label, 10f);
        SetBorderWidth(label, 2f);
        SetBorderColor(label, new Color(1f, 0.65f, 0.30f, 0.38f));
        return label;
    }

    private VisualElement CreateSidebarStaticContent(Func<VisualElement> buildContent)
    {
        VisualElement clip = new VisualElement();
        clip.style.paddingTop = SidebarSectionContentPaddingTop;
        clip.style.paddingBottom = SidebarSectionContentPaddingBottom;
        clip.style.borderTopWidth = 2f;
        clip.style.borderTopColor = new Color(0.75f, 0.72f, 0.82f, 0.10f);

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
        clip.style.borderTopWidth = 2f;
        clip.style.borderTopColor = new Color(0.75f, 0.72f, 0.82f, 0.10f);

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
                RefreshLeftPanel();
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
        section.Add(CreateSidebarButton("SynchTheory", ShowSynchTheoryPopup, synchTheoryAccent: true));
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

    private VisualElement CreateDifficultiesSidebarContent(ChartEditorTrackViewGroup group)
    {
        VisualElement container = new VisualElement();
        if (group?.tracks == null || group.tracks.Count == 0)
        {
            container.Add(CreateSidebarText("No arrangement selected.", new Color(0.70f, 0.74f, 0.82f, 0.92f)));
            return container;
        }

        List<ChartEditorTrack> variants = OrderDifficultyTracks(group.tracks);
        for (int i = 0; i < variants.Count; i++)
        {
            ChartEditorTrack track = variants[i];
            bool selected = string.Equals(track?.id ?? string.Empty, project.selectedTrackId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            container.Add(CreateDifficultySidebarRow(track, selected));
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

    private VisualElement CreateToneChangesSidebarContent()
    {
        VisualElement container = new VisualElement();
        ChartEditorTrack track = project?.SelectedTrack;
        if (track == null)
        {
            container.Add(CreateSidebarText("No arrangement selected.", new Color(0.70f, 0.74f, 0.82f, 0.92f)));
            return container;
        }

        List<ChartEditorToneChange> changes = GetSelectedTrackToneChanges();
        if (changes.Count == 0)
        {
            container.Add(CreateSidebarText("No tone changes yet.", new Color(0.70f, 0.74f, 0.82f, 0.92f)));
            return container;
        }

        for (int i = 0; i < changes.Count; i++)
            container.Add(CreateToneChangeSidebarRow(track, changes[i], i));
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
        container.Add(CreateProjectInfoRow("Length", FormatTime(GetProjectDurationSeconds())));
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

    private static float EstimateDifficultiesSidebarContentHeight(ChartEditorTrackViewGroup group)
    {
        int count = group?.tracks?.Count ?? 0;
        return EstimateSidebarContentHeight(count, SidebarListRowHeight + SidebarListRowGap);
    }

    private float EstimateSectionsSidebarContentHeight()
    {
        int count = project.sections == null ? 0 : Mathf.Min(project.sections.Count, SidebarMaxSectionRows);
        return EstimateSidebarContentHeight(count, SidebarListRowHeight + SidebarListRowGap);
    }

    private float EstimateToneChangesSidebarContentHeight()
    {
        int count = Mathf.Min(GetSelectedTrackToneChanges().Count, SidebarMaxSectionRows);
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

    private void ToggleDifficultiesExpanded()
    {
        ToggleSidebarSectionExpanded(SidebarDifficultiesKey, ref difficultiesExpanded);
    }

    private void ToggleToneChangesExpanded()
    {
        ToggleSidebarSectionExpanded(SidebarTonesKey, ref toneChangesExpanded);
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
        RefreshLeftPanel();
    }

    private void RefreshLeftPanel()
    {
        using ProfilerMarker.AutoScope scope = LeftPanelMarker.Auto();
        if (screen != ChartEditorScreen.Editor ||
            project == null ||
            leftPanelElement == null ||
            leftPanelElement.panel == null ||
            leftPanelElement.parent == null)
        {
            Rebuild();
            return;
        }

        VisualElement parent = leftPanelElement.parent;
        int index = parent.IndexOf(leftPanelElement);
        VisualElement fresh = BuildLeftPanel();
        parent.Insert(index, fresh);
        leftPanelElement.RemoveFromHierarchy();
        leftPanelElement = fresh;
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
        Color trackIdleBackground = selected ? new Color(0.078f, 0.075f, 0.090f, 0.72f) : new Color(0f, 0f, 0f, 0f);
        row.style.backgroundColor = trackIdleBackground;
        AddSidebarRowHoverEffect(row, trackIdleBackground, selected
            ? new Color(0.094f, 0.090f, 0.106f, 0.80f)
            : new Color(1f, 1f, 1f, 0.05f));
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

    private VisualElement CreateDifficultySidebarRow(ChartEditorTrack track, bool selected)
    {
        VisualElement row = new VisualElement();
        row.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (track == null)
                return;

            if (evt.button == 1)
            {
                ShowDifficultyContextMenu(evt.position, track);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
                return;

            SelectDifficultyTrack(track);
            evt.StopPropagation();
        });

        row.style.height = SidebarListRowHeight;
        row.style.minHeight = SidebarListRowHeight;
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.paddingLeft = 24f;
        row.style.paddingRight = 20f;
        row.style.marginLeft = 20f;
        row.style.marginRight = 20f;
        row.style.marginBottom = SidebarListRowGap;
        Color difficultyIdleBackground = selected ? new Color(0.086f, 0.083f, 0.098f, 0.86f) : new Color(0f, 0f, 0f, 0f);
        row.style.backgroundColor = difficultyIdleBackground;
        SetRadius(row, 8f);
        SetBorderWidth(row, selected ? 2f : 0f);
        if (selected)
            SetBorderColor(row, new Color(0.78f, 0.76f, 0.90f, 1f));
        AddSidebarRowHoverEffect(row, difficultyIdleBackground, selected
            ? new Color(0.100f, 0.096f, 0.114f, 0.90f)
            : new Color(1f, 1f, 1f, 0.05f));

        Label name = CreateLabel(FormatDifficultyLabel(track), 23f, Color.white, selected, TextAnchor.MiddleLeft, false);
        name.style.flexGrow = 1f;
        name.style.whiteSpace = WhiteSpace.NoWrap;
        row.Add(name);

        Label count = CreateLabel((track?.notes?.Count ?? 0).ToString(), 21f, new Color(0.72f, 0.76f, 0.82f, 1f), false, TextAnchor.MiddleRight, false);
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
        bool sectionSelected = string.Equals(selectedSectionId, section?.id, StringComparison.OrdinalIgnoreCase);
        Color sectionIdleBackground = sectionSelected
            ? new Color(0.080f, 0.070f, 0.110f, 0.94f)
            : new Color(0.046f, 0.044f, 0.053f, 0.48f);
        row.style.backgroundColor = sectionIdleBackground;
        SetRadius(row, 12f);
        AddSidebarRowHoverEffect(row, sectionIdleBackground, sectionSelected
            ? new Color(0.100f, 0.088f, 0.135f, 0.96f)
            : new Color(1f, 1f, 1f, 0.06f));

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

    private VisualElement CreateToneChangeSidebarRow(ChartEditorTrack track, ChartEditorToneChange change, int index)
    {
        bool selected = ReferenceEquals(selectedToneChange, change);
        VisualElement row = new VisualElement();
        row.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (change == null)
                return;

            if (evt.button == 1)
            {
                toneLabPanelFocused = false;
                SelectToneChange(change, seek: false, rebuild: false, focusToneLab: false);
                ShowToneChangeContextMenu(evt.position, change);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
                return;

            toneLabPanelFocused = false;
            SelectToneChange(change, seek: true, rebuild: true, focusToneLab: true);
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
        Color toneIdleBackground = selected
            ? new Color(0.000f, 0.135f, 0.130f, 0.94f)
            : new Color(0.046f, 0.044f, 0.053f, 0.48f);
        row.style.backgroundColor = toneIdleBackground;
        SetRadius(row, 12f);
        SetBorderWidth(row, selected ? 2f : 0f);
        if (selected)
            SetBorderColor(row, ToneMarkerSelectedColor());
        AddSidebarRowHoverEffect(row, toneIdleBackground, selected
            ? new Color(0.000f, 0.165f, 0.158f, 0.96f)
            : new Color(1f, 1f, 1f, 0.06f));

        VisualElement dot = new VisualElement();
        dot.style.width = 12f;
        dot.style.height = 12f;
        dot.style.marginRight = 16f;
        dot.style.backgroundColor = ToneMarkerColor(index, selected);
        SetRadius(dot, 999f);
        row.Add(dot);

        Label name = CreateLabel(FirstNonEmpty(ResolveToneChangeName(track, change), $"Tone {index + 1}"), 24f, new Color(0.90f, 0.96f, 0.96f, 1f), selected, TextAnchor.MiddleLeft, false);
        name.style.flexGrow = 1f;
        name.style.whiteSpace = WhiteSpace.NoWrap;
        row.Add(name);

        Label time = CreateLabel(FormatTime(change?.timeSeconds ?? 0.0), 24f, new Color(0.66f, 0.76f, 0.76f, 1f), false, TextAnchor.MiddleRight, false);
        time.style.width = 132f;
        row.Add(time);
        return row;
    }

    private List<ChartEditorToneChange> GetSelectedTrackToneChanges()
    {
        ChartEditorTrack track = project?.SelectedTrack;
        if (track?.tones == null)
            return new List<ChartEditorToneChange>();

        track.tones.EnsureDefaults();
        return track.tones.changes
            .Where(change => change != null)
            .OrderBy(change => change.timeSeconds)
            .ThenBy(change => change.toneName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void EnsureToneData(ChartEditorTrack track)
    {
        if (track == null)
            return;

        track.tones ??= new ChartEditorToneData();
        track.tones.EnsureDefaults();
    }

    private void NormalizeToneChanges(ChartEditorTrack track)
    {
        EnsureToneData(track);
        if (track?.tones?.changes == null)
            return;

        track.tones.changes = track.tones.changes
            .Where(change => change != null)
            .OrderBy(change => change.timeSeconds)
            .ThenBy(change => change.toneName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // All tone-change time edits funnel through here — keep the beat-map
        // tracking in sync or the next beat edit snaps the change back to its
        // stale beat position.
        foreach (ChartEditorToneChange change in track.tones.changes)
        {
            change.beatPosition = ChartEditorTimingService.GetBeatPositionForAudioTime(project, change.timeSeconds);
            change.usesBeatMapTiming = true;
        }

        ChartEditorToneScopeService.PropagateToneDataFromTrack(project, track);
    }

    private void EnsureToneSelectionForCurrentTrack()
    {
        List<ChartEditorToneChange> changes = GetSelectedTrackToneChanges();
        if (changes.Count == 0)
        {
            selectedToneChange = null;
            return;
        }

        if (selectedToneChange != null && changes.Contains(selectedToneChange))
            return;

        double cursorTime = project?.cursorTimeSeconds ?? 0.0;
        selectedToneChange = changes.LastOrDefault(change => change.timeSeconds <= cursorTime + 0.0001)
                             ?? changes.FirstOrDefault();
    }

    private void SelectToneChange(ChartEditorToneChange change, bool seek, bool rebuild, bool focusToneLab)
    {
        if (project == null || change == null)
            return;

        selectedToneChange = change;
        toneEditorEnabled = true;
        selectedSectionId = null;
        selectedSyncPointId = null;
        ClearAnchorSelection();
        ClearNoteSelection();
        if (seek)
            SeekAndRevealTime(change.timeSeconds, syncAudio: true, rebuild: false);
        if (focusToneLab)
        {
            toneLabPanelFocused = true;
            ClearToneEditorPlaybackOverride();
            LoadSelectedToneIntoToneLab(forceReload: true);
        }

        if (rebuild)
            RefreshToneEditorWorkspace();
        else
            UpdateToneEditorPlaybackOverride();
    }

    private void RefreshToneEditorWorkspace()
    {
        if (!RefreshToneEditorPanelOnly())
            return;

        RefreshLeftPanel();
        RefreshToneMarkerSelectionVisuals();
        UpdateToneMarkerVisuals();
    }

    private bool RefreshToneEditorPanelOnly()
    {
        using ProfilerMarker.AutoScope scope = ToneWorkspaceMarker.Auto();
        if (screen != ChartEditorScreen.Editor ||
            project == null ||
            highwayPreviewPanelElement == null ||
            highwayPreviewPanelElement.panel == null ||
            highwayPreviewPanelElement.parent == null)
        {
            Rebuild();
            return false;
        }

        VisualElement previousPanel = highwayPreviewPanelElement;
        VisualElement parent = previousPanel.parent;
        int index = parent.IndexOf(previousPanel);
        VisualElement fresh = BuildHighwayPreviewPanel();
        parent.Insert(index, fresh);
        previousPanel.RemoveFromHierarchy();
        ApplyToneEditorHeaderButtonState();
        return true;
    }

    private void RefreshTimelinePanel()
    {
        using ProfilerMarker.AutoScope scope = TimelinePanelMarker.Auto();
        if (screen != ChartEditorScreen.Editor ||
            project == null ||
            timelinePanelElement == null ||
            timelinePanelElement.panel == null ||
            timelinePanelElement.parent == null)
        {
            Rebuild();
            return;
        }

        HideContextMenu();
        ClearMarqueeSelection();
        cursorElement = null;
        cursorHandleElement = null;
        waveformTextureElement = null;
        waveformVectorElement = null;
        currentNoteHits.Clear();
        currentNoteBlocks.Clear();
        currentNoteBlockStyles.Clear();
        currentNoteBlockTimings.Clear();
        currentTechniqueSegmentVisuals.Clear();
        currentToneMarkerVisuals.Clear();
        MarkHighwayPreviewDirty();
        InvalidateAuditionCache();

        VisualElement previousPanel = timelinePanelElement;
        VisualElement parent = previousPanel.parent;
        int index = parent.IndexOf(previousPanel);
        VisualElement fresh = BuildTimelinePanel();
        parent.Insert(index, fresh);
        previousPanel.RemoveFromHierarchy();
        timelinePanelElement = fresh;
    }

    private void QueueTimelineRefresh()
    {
        if (timelineRefreshQueued)
            return;

        timelineRefreshQueued = true;
        ScheduleQueuedTimelineRefresh(45);
    }

    private void ScheduleQueuedTimelineRefresh(long delayMs)
    {
        RootElement.schedule.Execute(() =>
        {
            // Never tear the timeline down while the user is mid-gesture:
            // rebuilding would destroy the element holding the pointer capture.
            if (marqueeSelecting || IsAnyPointerCaptured())
            {
                ScheduleQueuedTimelineRefresh(90);
                return;
            }

            timelineRefreshQueued = false;
            RefreshTimelinePanel();
        }).ExecuteLater(delayMs);
    }

    private bool IsAnyPointerCaptured()
    {
        IPanel panel = RootElement?.panel;
        if (panel == null)
            return false;

        // Touch and pen drags capture non-mouse pointer ids; checking only the
        // mouse let the refill watchdog tear the timeline down mid-touch-drag.
        for (int pointerId = 0; pointerId < PointerId.maxPointers; pointerId++)
        {
            if (panel.GetCapturingElement(pointerId) != null)
                return true;
        }

        return false;
    }

    private void GetTimelineBuildWindowSeconds(out double windowStartSeconds, out double windowEndSeconds)
    {
        float viewportWidth = timelineViewportWidth > 1f ? timelineViewportWidth : 2600f;
        float margin = viewportWidth * 1.0f;
        float scrollX = Mathf.Max(0f, timelineScrollOffset.x);
        builtTimelineWindowStartPx = Mathf.Max(0f, scrollX - margin);
        builtTimelineWindowEndPx = scrollX + viewportWidth + margin;
        windowStartSeconds = PixelsToSeconds(Mathf.Max(0f, scrollX - TimelineLabelWidth - margin));
        windowEndSeconds = PixelsToSeconds(Mathf.Max(0f, scrollX + viewportWidth - TimelineLabelWidth + margin));
    }

    private void CheckTimelineWindowRefill()
    {
        if (!visible ||
            screen != ChartEditorScreen.Editor ||
            project == null ||
            timelinePanelElement == null ||
            timelinePanelElement.panel == null ||
            builtTimelineWindowEndPx <= builtTimelineWindowStartPx)
        {
            return;
        }

        float viewStart = Mathf.Max(0f, timelineScrollOffset.x);
        float viewEnd = viewStart + Mathf.Max(1f, timelineViewportWidth);
        float slack = Mathf.Max(1f, timelineViewportWidth) * 0.5f;
        bool needsLeft = builtTimelineWindowStartPx > 1f && viewStart < builtTimelineWindowStartPx + slack;
        bool needsRight = viewEnd > builtTimelineWindowEndPx - slack;
        if (needsLeft || needsRight)
            QueueTimelineWindowRefill();
    }

    private void QueueTimelineWindowRefill()
    {
        if (timelineWindowRefillQueued || timelineRefreshQueued)
            return;

        timelineWindowRefillQueued = true;
        ScheduleQueuedTimelineWindowRefill(30);
    }

    private void ScheduleQueuedTimelineWindowRefill(long delayMs)
    {
        RootElement.schedule.Execute(() =>
        {
            if (marqueeSelecting || IsAnyPointerCaptured())
            {
                ScheduleQueuedTimelineWindowRefill(90);
                return;
            }

            timelineWindowRefillQueued = false;
            RefreshTimelineWindowContent();
        }).ExecuteLater(delayMs);
    }

    private void RefreshTimelineWindowContent()
    {
        using ProfilerMarker.AutoScope scope = WindowRefillMarker.Auto();
        if (screen != ChartEditorScreen.Editor ||
            project == null ||
            timelineWindowedLayer == null ||
            timelineWindowedLayer.panel == null)
        {
            RefreshTimelinePanel();
            return;
        }

        timelineNoteBuildGeneration++;
        timelineWindowedLayer.Clear();
        currentNoteHits.Clear();
        currentNoteBlocks.Clear();
        currentNoteBlockStyles.Clear();
        currentNoteBlockTimings.Clear();
        currentTechniqueSegmentVisuals.Clear();
        BuildBeatGrid(timelineWindowedLayer);
        BuildNotes(timelineWindowedLayer);
    }

    private void RefreshNoteSelectionVisuals(IEnumerable<string> affectedNoteIds)
    {
        if (affectedNoteIds == null)
            return;

        foreach (string id in affectedNoteIds)
        {
            if (string.IsNullOrWhiteSpace(id) ||
                !currentNoteBlocks.TryGetValue(id, out VisualElement block) ||
                block == null ||
                !currentNoteBlockStyles.TryGetValue(id, out (Color baseColor, bool selectedTrack) style))
            {
                continue;
            }

            bool isSelected = selectedNoteIds.Contains(id) ||
                              string.Equals(selectedNoteId, id, StringComparison.OrdinalIgnoreCase);
            Color accent = isSelected ? new Color(1f, 0.88f, 0.42f, 1f) : style.baseColor;
            block.style.backgroundColor = new Color(accent.r, accent.g, accent.b, style.selectedTrack ? 0.82f : 0.42f);
            SetBorderColor(block, accent);
        }
    }

    private void RefreshTimelineAndSidebar()
    {
        RefreshTimelinePanel();
        RefreshLeftPanel();
    }

    private void RefreshToneMarkerLane()
    {
        if (screen != ChartEditorScreen.Editor ||
            toneMarkerTimelineElement == null ||
            toneMarkerTimelineElement.panel == null)
        {
            Rebuild();
            return;
        }

        for (int i = 0; i < currentToneMarkerVisuals.Count; i++)
        {
            ToneMarkerVisual visual = currentToneMarkerVisuals[i];
            visual?.hit?.RemoveFromHierarchy();
            visual?.cap?.RemoveFromHierarchy();
            visual?.line?.RemoveFromHierarchy();
        }

        for (int i = 0; i < toneMarkerLaneElements.Count; i++)
            toneMarkerLaneElements[i]?.RemoveFromHierarchy();

        BuildToneMarkers(toneMarkerTimelineElement);
    }

    private void RefreshToneMarkerSelectionVisuals()
    {
        if (currentToneMarkerVisuals.Count == 0)
            return;

        List<ChartEditorToneChange> changes = GetSelectedTrackToneChanges();
        for (int i = 0; i < currentToneMarkerVisuals.Count; i++)
        {
            ToneMarkerVisual visual = currentToneMarkerVisuals[i];
            ChartEditorToneChange change = visual?.change;
            if (change == null)
                continue;

            bool selected = ReferenceEquals(selectedToneChange, change);
            int changeIndex = changes.IndexOf(change);
            Color markerColor = ToneMarkerColor(changeIndex < 0 ? i : changeIndex, selected);
            if (visual.line != null)
            {
                visual.line.style.width = selected ? 6f : 4f;
                visual.line.style.marginLeft = selected ? -3f : -2f;
                visual.line.style.backgroundColor = new Color(markerColor.r, markerColor.g, markerColor.b, selected ? 0.98f : 0.86f);
            }

            if (visual.cap != null)
            {
                visual.cap.style.backgroundColor = new Color(markerColor.r, markerColor.g, markerColor.b, selected ? 0.94f : 0.76f);
                SetBorderWidth(visual.cap, selected ? 2f : 1f);
                SetBorderColor(visual.cap, selected ? Color.white : new Color(0.94f, 1f, 0.98f, 0.62f));
            }
        }
    }

    private string ResolveToneChangeName(ChartEditorTrack track, ChartEditorToneChange change)
    {
        ChartEditorToneDefinition definition = ResolveToneDefinitionForChange(track, change);
        return FirstNonEmpty(
            change?.toneName,
            definition?.name,
            definition?.key,
            track?.tones?.baseToneName,
            "Tone");
    }

    private ChartEditorToneDefinition ResolveToneDefinitionForChange(ChartEditorTrack track, ChartEditorToneChange change)
    {
        EnsureToneData(track);
        if (track?.tones?.definitions == null || track.tones.definitions.Count == 0)
            return null;

        string toneName = change?.toneName?.Trim();
        ChartEditorToneDefinition byName = FindToneDefinition(track, toneName);
        if (byName != null)
            return byName;

        int toneId = change?.toneId ?? -1;
        if (toneId >= 0 && toneId < track.tones.definitions.Count)
            return track.tones.definitions[toneId];

        return FindToneDefinition(track, track.tones.baseToneName);
    }

    private static ChartEditorToneDefinition FindToneDefinition(ChartEditorTrack track, string toneName)
    {
        if (track?.tones?.definitions == null || string.IsNullOrWhiteSpace(toneName))
            return null;

        // Tiered matching: an exact definition name/key match must always win
        // over fuzzy preset-name matches. Multiple definitions can share the
        // same source preset name (for example a base tone and a customized
        // copy captured from it), and the fuzzy match would otherwise resolve
        // to whichever one appears first in the list.
        string normalized = toneName.Trim();
        List<ChartEditorToneDefinition> definitions = track.tones.definitions;

        ChartEditorToneDefinition match = definitions.FirstOrDefault(definition =>
            definition != null &&
            string.Equals(definition.name ?? string.Empty, normalized, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            return match;

        match = definitions.FirstOrDefault(definition =>
            definition != null &&
            string.Equals(definition.key ?? string.Empty, normalized, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            return match;

        match = definitions.FirstOrDefault(definition =>
            definition != null &&
            string.Equals(definition.preset?.presetName ?? string.Empty, normalized, StringComparison.OrdinalIgnoreCase));
        if (match != null)
            return match;

        return definitions.FirstOrDefault(definition =>
            definition != null &&
            string.Equals(definition.preset?.presetId ?? string.Empty, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private ChartEditorToneChange GetToneChangeAtTime(ChartEditorTrack track, double timeSeconds)
    {
        EnsureToneData(track);
        if (track?.tones?.changes == null || track.tones.changes.Count == 0)
            return null;

        float safeTime = Mathf.Max(0f, (float)timeSeconds);
        return track.tones.changes
            .Where(change => change != null && change.timeSeconds <= safeTime + 0.0001f)
            .OrderBy(change => change.timeSeconds)
            .LastOrDefault()
            ?? track.tones.changes
                .Where(change => change != null)
                .OrderBy(change => change.timeSeconds)
                .FirstOrDefault();
    }

    private static Color ToneMarkerSelectedColor()
    {
        return new Color(1.00f, 0.70f, 0.18f, 1f);
    }

    private static Color ToneMarkerBaseColor()
    {
        return new Color(0.00f, 0.88f, 0.78f, 1f);
    }

    private static Color ToneMarkerColor(int index, bool selected)
    {
        if (selected)
            return ToneMarkerSelectedColor();

        Color baseColor = ToneMarkerBaseColor();
        float hueShift = (index % 5) * 0.045f;
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);
        return Color.HSVToRGB(Mathf.Repeat(h + hueShift, 1f), Mathf.Clamp01(s * 0.92f), Mathf.Clamp01(v));
    }

    private string MakeUniqueToneDefinitionName(
        ChartEditorTrack track,
        string requestedName,
        ChartEditorToneDefinition allowedExisting = null)
    {
        EnsureToneData(track);
        string seed = FirstNonEmpty(requestedName, "Tone").Trim();
        HashSet<string> existing = new HashSet<string>(
            track?.tones?.definitions?
                .Where(definition => definition != null)
                .Where(definition => allowedExisting == null || !ReferenceEquals(definition, allowedExisting))
                .Select(definition => definition.name ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name)) ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains(seed))
            return seed;

        for (int i = 2; i < 1000; i++)
        {
            string candidate = $"{seed} {i}";
            if (!existing.Contains(candidate))
                return candidate;
        }

        return $"{seed} {DateTime.Now:HHmmss}";
    }

    private static string BuildNeutralToneKey(string toneName)
    {
        string source = string.IsNullOrWhiteSpace(toneName) ? "tone" : toneName.Trim().ToLowerInvariant();
        char[] chars = source.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        string normalized = new string(chars).Trim('_');
        while (normalized.Contains("__"))
            normalized = normalized.Replace("__", "_");
        return string.IsNullOrWhiteSpace(normalized) ? "tone" : $"tone_{normalized}";
    }

    private UnityToneLabRuntime GetToneLabRuntime()
    {
        if (cachedToneLabRuntime == null)
            cachedToneLabRuntime = owner?.GetChartEditorToneLabRuntime();
        return cachedToneLabRuntime;
    }

    private void AddToneChangeAtCursor()
    {
        AddToneChangeAtTime(project?.cursorTimeSeconds ?? 0.0);
    }

    private void AddToneChangeAtTime(double timeSeconds)
    {
        ChartEditorTrack track = project?.SelectedTrack;
        if (track == null)
            return;

        EnsureToneData(track);
        double safeTime = Math.Max(0.0, Math.Min(GetProjectDurationSeconds(), timeSeconds));
        ChartEditorToneChange existingAtTime = GetToneChangeAtTime(track, safeTime);
        string toneName = ResolveToneChangeName(track, existingAtTime);
        UnityToneLabRuntime.ToneLabPreset currentPreset = toneLabPanelFocused
            ? CaptureCurrentToneLabPresetSnapshot(FirstNonEmpty(toneName, "Tone"))
            : null;

        ChartEditorToneChange change = new ChartEditorToneChange
        {
            timeSeconds = (float)safeTime,
            toneName = FirstNonEmpty(currentPreset?.preset_name, toneName, track.tones.baseToneName, $"Tone {track.tones.changes.Count + 1}"),
            toneId = -1
        };
        track.tones.changes.Add(change);
        if (currentPreset != null)
            UpsertToneDefinition(track, change, change.toneName, currentPreset);
        else
            UpdateToneChangeToneId(track, change);

        NormalizeToneChanges(track);
        project.dirty = true;
        bool enteringToneEditor = !toneEditorEnabled;
        SelectToneChange(change, seek: true, rebuild: false, focusToneLab: currentPreset != null);
        if (enteringToneEditor)
        {
            Rebuild();
            return;
        }

        RefreshToneMarkerLane();
        RefreshToneEditorWorkspace();
    }

    private void DuplicateToneChange(ChartEditorToneChange source)
    {
        ChartEditorTrack track = project?.SelectedTrack;
        if (track == null || source == null)
            return;

        EnsureToneData(track);
        ChartEditorToneChange clone = new ChartEditorToneChange
        {
            timeSeconds = Mathf.Clamp(source.timeSeconds + 0.10f, 0f, (float)Math.Max(0.0, GetProjectDurationSeconds())),
            toneName = source.toneName ?? string.Empty,
            toneId = source.toneId
        };
        track.tones.changes.Add(clone);
        UpdateToneChangeToneId(track, clone);
        NormalizeToneChanges(track);
        project.dirty = true;
        SelectToneChange(clone, seek: true, rebuild: false, focusToneLab: true);
        RefreshToneMarkerLane();
        RefreshToneEditorWorkspace();
    }

    private void DeleteToneChange(ChartEditorToneChange change)
    {
        ChartEditorTrack track = project?.SelectedTrack;
        if (track?.tones?.changes == null || change == null)
            return;

        track.tones.changes.Remove(change);
        NormalizeToneChanges(track);
        selectedToneChange = null;
        EnsureToneSelectionForCurrentTrack();
        project.dirty = true;
        toneLabLoadedToneKey = string.Empty;
        appliedToneEditorPlaybackKey = string.Empty;
        RefreshToneMarkerLane();
        RefreshToneEditorWorkspace();
    }

    private void ShowToneChangeContextMenu(Vector2 worldPosition, ChartEditorToneChange change)
    {
        if (change == null)
            return;

        SelectToneChange(change, seek: false, rebuild: false, focusToneLab: false);
        ShowContextMenu(worldPosition,
            new ContextMenuItem("Assign Current Tone", () => AssignCurrentToneToChange(change)),
            new ContextMenuItem("Settings...", () => ShowToneChangeSettingsPopup(change)),
            new ContextMenuItem("Move to Cursor", () => MoveToneChangeToCursor(change)),
            new ContextMenuItem("Duplicate Tone Change", () => DuplicateToneChange(change)),
            new ContextMenuItem("Delete Tone Change", () => DeleteToneChange(change)));
    }

    private void MoveToneChangeToCursor(ChartEditorToneChange change)
    {
        if (project == null || change == null)
            return;

        change.timeSeconds = Mathf.Clamp((float)project.cursorTimeSeconds, 0f, (float)Math.Max(0.0, GetProjectDurationSeconds()));
        NormalizeToneChanges(project.SelectedTrack);
        project.dirty = true;
        SelectToneChange(change, seek: true, rebuild: true, focusToneLab: true);
    }

    private void ShowToneChangeSettingsPopup(ChartEditorToneChange change)
    {
        ChartEditorTrack track = project?.SelectedTrack;
        if (track == null || change == null)
            return;

        string currentName = ResolveToneChangeName(track, change);
        TextField nameField = CreatePopupTextField("Tone Name", currentName);
        TextField timeField = CreatePopupTextField("Time Seconds", change.timeSeconds.ToString("0.000", CultureInfo.InvariantCulture));
        ShowEditPopup("Tone Change Settings", new VisualElement[] { nameField, timeField }, () =>
        {
            string nextName = FirstNonEmpty(nameField.value, currentName, "Tone").Trim();
            if (!double.TryParse(timeField.value, NumberStyles.Float, CultureInfo.InvariantCulture, out double nextTime))
            {
                SetStatus("Tone change time must be a number.");
                return false;
            }

            nextTime = Math.Max(0.0, Math.Min(GetProjectDurationSeconds(), nextTime));
            RenameToneChange(track, change, nextName);
            change.timeSeconds = (float)nextTime;
            NormalizeToneChanges(track);
            project.cursorTimeSeconds = nextTime;
            project.dirty = true;
            toneLabLoadedToneKey = string.Empty;
            appliedToneEditorPlaybackKey = string.Empty;
            HideEditPopup();
            SelectToneChange(change, seek: true, rebuild: true, focusToneLab: true);
            return true;
        });
    }

    private void RenameToneChange(ChartEditorTrack track, ChartEditorToneChange change, string nextName)
    {
        EnsureToneData(track);
        if (track == null || change == null)
            return;

        string oldName = ResolveToneChangeName(track, change);
        ChartEditorToneDefinition oldDefinition = ResolveToneDefinitionForChange(track, change);
        ChartEditorToneDefinition existingDefinition = FindToneDefinition(track, nextName);
        int oldDefinitionIndex = oldDefinition != null ? track.tones.definitions.IndexOf(oldDefinition) : -1;
        List<ChartEditorToneChange> changesSharingOldDefinition = new List<ChartEditorToneChange>();
        bool updateBaseToneName = false;
        if (existingDefinition == null && oldDefinition != null)
        {
            if (track.tones.changes != null)
            {
                changesSharingOldDefinition = track.tones.changes
                    .Where(candidate =>
                        candidate != null &&
                        (ReferenceEquals(candidate, change) ||
                         ReferenceEquals(ResolveToneDefinitionForChange(track, candidate), oldDefinition) ||
                         (oldDefinitionIndex >= 0 && candidate.toneId == oldDefinitionIndex) ||
                         string.Equals(candidate.toneName ?? string.Empty, oldName, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            updateBaseToneName = ReferenceEquals(FindToneDefinition(track, track.tones.baseToneName), oldDefinition) ||
                                 string.Equals(track.tones.baseToneName ?? string.Empty, oldName, StringComparison.OrdinalIgnoreCase);
            oldDefinition.name = nextName;
            oldDefinition.key = BuildNeutralToneKey(nextName);
            existingDefinition = oldDefinition;
        }
        else if (existingDefinition == null && oldDefinition == null)
        {
            existingDefinition = new ChartEditorToneDefinition
            {
                name = nextName,
                key = BuildNeutralToneKey(nextName),
                preset = new ChartEditorTonePresetData
                {
                    presetId = BuildNeutralToneKey(nextName),
                    presetName = nextName
                },
                fallback = new ChartEditorToneFallbackData
                {
                    preferredPresetName = nextName,
                    searchText = FirstNonEmpty(oldName, nextName)
                }
            };
            track.tones.definitions.Add(existingDefinition);
        }

        if (changesSharingOldDefinition.Count > 0)
        {
            for (int i = 0; i < changesSharingOldDefinition.Count; i++)
            {
                ChartEditorToneChange sharedChange = changesSharingOldDefinition[i];
                sharedChange.toneName = nextName;
                UpdateToneChangeToneId(track, sharedChange);
            }

            if (updateBaseToneName)
                track.tones.baseToneName = nextName;
        }
        else
        {
            change.toneName = nextName;
            UpdateToneChangeToneId(track, change);
        }
    }

    private void AssignCurrentToneToChange(ChartEditorToneChange change)
    {
        ChartEditorTrack track = project?.SelectedTrack;
        if (track == null || change == null)
            return;

        FocusToneLabPanel();
        string toneName = ResolveToneChangeName(track, change);
        UnityToneLabRuntime.ToneLabPreset preset = CaptureCurrentToneLabPresetSnapshot(toneName);
        if (preset == null || preset.pedal_chain == null || preset.pedal_chain.Count == 0)
        {
            SetStatus("Tone Lab does not have a current tone chain to assign.");
            return;
        }

        string resolvedName = FirstNonEmpty(preset.preset_name, toneName, $"Tone {(track.tones?.definitions?.Count ?? 0) + 1}");
        UpsertToneDefinition(track, change, resolvedName, preset);
        ChartEditorToneScopeService.PropagateToneDataFromTrack(project, track);
        project.dirty = true;
        toneLabLoadedToneKey = string.Empty;
        appliedToneEditorPlaybackKey = string.Empty;
        SetStatus($"Assigned current tone to \"{resolvedName}\".");
        SelectToneChange(change, seek: false, rebuild: true, focusToneLab: true);
    }

    private UnityToneLabRuntime.ToneLabPreset CaptureCurrentToneLabPresetSnapshot(string fallbackName)
    {
        UnityToneLabRuntime runtime = GetToneLabRuntime();
        if (runtime == null)
            return null;

        string currentPresetName = runtime.CurrentPresets
            .FirstOrDefault(preset => string.Equals(preset?.preset_id, runtime.CurrentPresetId, StringComparison.Ordinal))
            ?.preset_name;
        string sourcePresetId = !toneLabWorkingToneEditedAfterLibrarySelection
            ? FirstNonEmpty(toneLabWorkingLibraryPresetId, runtime.CurrentPresetId)
            : string.Empty;
        string name = FirstNonEmpty(currentPresetName, fallbackName, "Tone");
        string id = FirstNonEmpty(sourcePresetId, BuildNeutralToneKey($"{name}_{Guid.NewGuid():N}"));
        return runtime.CaptureCurrentPresetSnapshot(name, id);
    }

    private void UpsertToneDefinition(ChartEditorTrack track, ChartEditorToneChange change, string toneName, UnityToneLabRuntime.ToneLabPreset preset)
    {
        EnsureToneData(track);
        if (track == null || change == null || preset == null)
            return;

        string resolvedName = FirstNonEmpty(toneName, preset.preset_name, "Tone");
        ChartEditorToneDefinition currentDefinition = ResolveToneDefinitionForChange(track, change);
        bool createDedicatedDefinition = ShouldCreateDedicatedToneDefinition(track, change, currentDefinition);
        ChartEditorToneDefinition definition = createDedicatedDefinition ? null : currentDefinition;
        if (definition == null)
        {
            resolvedName = MakeUniqueToneDefinitionName(track, resolvedName);
            definition = new ChartEditorToneDefinition
            {
                name = resolvedName,
                key = BuildNeutralToneKey(resolvedName),
                fallback = new ChartEditorToneFallbackData()
            };
            track.tones.definitions.Add(definition);
        }
        else
        {
            resolvedName = MakeUniqueToneDefinitionName(track, resolvedName, definition);
        }

        definition.name = resolvedName;
        definition.key = BuildNeutralToneKey(resolvedName);
        preset.preset_name = FirstNonEmpty(preset.preset_name, resolvedName);
        if (string.IsNullOrWhiteSpace(preset.preset_id))
            preset.preset_id = BuildNeutralToneKey($"{resolvedName}_{Guid.NewGuid():N}");
        definition.preset = ToChartEditorTonePreset(preset);
        definition.fallback ??= new ChartEditorToneFallbackData();
        definition.fallback.preferredPresetName = FirstNonEmpty(preset.preset_name, resolvedName);
        definition.fallback.searchText = FirstNonEmpty(resolvedName, preset.preset_name);
        change.toneName = definition.name;
        change.toneId = track.tones.definitions.IndexOf(definition);
    }

    private bool ShouldCreateDedicatedToneDefinition(
        ChartEditorTrack track,
        ChartEditorToneChange change,
        ChartEditorToneDefinition currentDefinition)
    {
        EnsureToneData(track);
        if (track?.tones == null || change == null || currentDefinition == null)
            return true;

        if (ReferenceEquals(FindToneDefinition(track, track.tones.baseToneName), currentDefinition))
            return true;

        if (track.tones.changes == null)
            return false;

        for (int i = 0; i < track.tones.changes.Count; i++)
        {
            ChartEditorToneChange other = track.tones.changes[i];
            if (other == null || ReferenceEquals(other, change))
                continue;

            if (ReferenceEquals(ResolveToneDefinitionForChange(track, other), currentDefinition))
                return true;
        }

        return false;
    }

    private void UpdateToneChangeToneId(ChartEditorTrack track, ChartEditorToneChange change)
    {
        EnsureToneData(track);
        if (track?.tones?.definitions == null || change == null)
            return;

        ChartEditorToneDefinition definition = FindToneDefinition(track, change.toneName) ?? ResolveToneDefinitionForChange(track, change);
        int index = definition == null ? -1 : track.tones.definitions.IndexOf(definition);
        change.toneId = index;
    }

    private void LoadSelectedToneIntoToneLab(bool forceReload = false)
    {
        ChartEditorTrack track = project?.SelectedTrack;
        if (track == null || selectedToneChange == null)
            return;

        ChartEditorToneDefinition definition = ResolveToneDefinitionForChange(track, selectedToneChange);
        UnityToneLabRuntime.ToneLabPreset preset = ToUnityToneLabPreset(definition);
        if (preset == null || preset.pedal_chain == null || preset.pedal_chain.Count == 0)
            return;

        string key = BuildToneRuntimeKey(track, selectedToneChange, definition, preset);
        if (!forceReload && string.Equals(toneLabLoadedToneKey, key, StringComparison.Ordinal))
            return;

        UnityToneLabRuntime runtime = GetToneLabRuntime();
        if (runtime != null && runtime.LoadWorkingPresetSnapshot(preset))
        {
            toneLabLoadedToneKey = key;
            toneLabSelectedPedalInstanceId = runtime.CurrentPedalChain.FirstOrDefault()?.pedal_instance_id ?? string.Empty;
            toneLabWorkingLibraryPresetId = runtime.CurrentPresetId ?? string.Empty;
            toneLabWorkingToneEditedAfterLibrarySelection = false;
            appliedToneEditorPlaybackKey = string.Empty;
        }
    }

    private void UpdateToneEditorPlaybackOverride()
    {
        if (!toneEditorEnabled || toneLabPanelFocused || project == null)
            return;

        ChartEditorTrack track = project.SelectedTrack;
        ChartEditorToneChange change = GetToneChangeAtTime(track, project.cursorTimeSeconds);
        ChartEditorToneDefinition definition = ResolveToneDefinitionForChange(track, change);
        UnityToneLabRuntime.ToneLabPreset preset = ToUnityToneLabPreset(definition);
        if (preset == null || preset.pedal_chain == null || preset.pedal_chain.Count == 0)
        {
            ClearToneEditorPlaybackOverride();
            return;
        }

        string key = BuildToneRuntimeKey(track, change, definition, preset);
        if (string.Equals(appliedToneEditorPlaybackKey, key, StringComparison.Ordinal))
            return;

        if (owner != null && owner.ApplyChartEditorTonePresetOverride(preset))
            appliedToneEditorPlaybackKey = key;
    }

    private void ClearToneEditorPlaybackOverride()
    {
        if (!string.IsNullOrWhiteSpace(appliedToneEditorPlaybackKey) || toneEditorEnabled)
            owner?.ClearChartEditorTonePresetOverride();
        appliedToneEditorPlaybackKey = string.Empty;
    }

    private static string BuildToneRuntimeKey(
        ChartEditorTrack track,
        ChartEditorToneChange change,
        ChartEditorToneDefinition definition,
        UnityToneLabRuntime.ToneLabPreset preset)
    {
        int pedalCount = preset?.pedal_chain?.Count ?? 0;
        return string.Join("|",
            track?.id ?? string.Empty,
            change?.timeSeconds.ToString("0.000", CultureInfo.InvariantCulture) ?? string.Empty,
            definition?.name ?? string.Empty,
            definition?.key ?? string.Empty,
            preset?.preset_id ?? string.Empty,
            preset?.preset_name ?? string.Empty,
            pedalCount.ToString(CultureInfo.InvariantCulture),
            (preset?.input_gain_db ?? 0f).ToString("0.###", CultureInfo.InvariantCulture),
            (preset?.output_gain_db ?? 0f).ToString("0.###", CultureInfo.InvariantCulture),
            JsonUtility.ToJson(preset, false) ?? string.Empty);
    }

    private static UnityToneLabRuntime.ToneLabPreset ToUnityToneLabPreset(ChartEditorToneDefinition definition)
    {
        return ToUnityToneLabPreset(definition?.preset, definition?.name, definition?.key);
    }

    private static UnityToneLabRuntime.ToneLabPreset ToUnityToneLabPreset(ChartEditorTonePresetData source, string fallbackName, string fallbackKey)
    {
        if (source == null)
            return null;

        UnityToneLabRuntime.ToneLabPreset preset = new UnityToneLabRuntime.ToneLabPreset
        {
            preset_id = FirstNonEmpty(source.presetId, BuildNeutralToneKey(FirstNonEmpty(fallbackName, fallbackKey, "Tone"))),
            preset_name = FirstNonEmpty(source.presetName, fallbackName, fallbackKey, "Tone"),
            input_gain_db = source.inputGainDb,
            output_gain_db = source.outputGainDb,
            pedal_chain = new List<UnityToneLabRuntime.ToneLabPedalSlot>()
        };

        if (source.pedalChain != null)
        {
            for (int i = 0; i < source.pedalChain.Count; i++)
            {
                ChartEditorTonePedalSlotData slot = source.pedalChain[i];
                if (slot == null)
                    continue;

                UnityToneLabRuntime.ToneLabPedalType pedalType = UnityToneLabRuntime.ToneLabPedalType.Amp;
                if (!string.IsNullOrWhiteSpace(slot.pedalType))
                    Enum.TryParse(slot.pedalType, ignoreCase: true, out pedalType);

                preset.pedal_chain.Add(new UnityToneLabRuntime.ToneLabPedalSlot
                {
                    pedal_instance_id = slot.instanceId ?? string.Empty,
                    pedal_type = pedalType,
                    descriptor_id = slot.descriptorId ?? string.Empty,
                    enabled = slot.enabled,
                    settings_json = slot.settingsJson ?? string.Empty
                });
            }
        }

        return preset;
    }

    private static ChartEditorTonePresetData ToChartEditorTonePreset(UnityToneLabRuntime.ToneLabPreset source)
    {
        ChartEditorTonePresetData result = new ChartEditorTonePresetData
        {
            presetId = source?.preset_id ?? string.Empty,
            presetName = source?.preset_name ?? string.Empty,
            inputGainDb = source?.input_gain_db ?? 0f,
            outputGainDb = source?.output_gain_db ?? 0f,
            pedalChain = new List<ChartEditorTonePedalSlotData>()
        };

        if (source?.pedal_chain != null)
        {
            for (int i = 0; i < source.pedal_chain.Count; i++)
            {
                UnityToneLabRuntime.ToneLabPedalSlot slot = source.pedal_chain[i];
                if (slot == null)
                    continue;

                result.pedalChain.Add(new ChartEditorTonePedalSlotData
                {
                    instanceId = slot.pedal_instance_id ?? string.Empty,
                    pedalType = slot.pedal_type.ToString(),
                    descriptorId = slot.descriptor_id ?? string.Empty,
                    enabled = slot.enabled,
                    settingsJson = slot.settings_json ?? string.Empty
                });
            }
        }

        result.EnsureDefaults();
        return result;
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
        Color anchorIdleBackground = selected
            ? new Color(0.080f, 0.070f, 0.110f, 0.94f)
            : new Color(0.046f, 0.044f, 0.053f, 0.48f);
        row.style.backgroundColor = anchorIdleBackground;
        SetRadius(row, 12f);
        AddSidebarRowHoverEffect(row, anchorIdleBackground, selected
            ? new Color(0.100f, 0.088f, 0.135f, 0.96f)
            : new Color(1f, 1f, 1f, 0.06f));

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

    private Button CreateSidebarButton(string text, Action action, bool synchTheoryAccent = false)
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
        if (synchTheoryAccent)
            StyleSynchTheorySidebarButton(button);
        else
            StyleSidebarActionButton(button);

        Color titleColor = synchTheoryAccent
            ? new Color(1f, 0.90f, 0.70f, 1f)
            : new Color(0.94f, 0.93f, 0.96f, 1f);
        Color actionColor = synchTheoryAccent
            ? new Color(1f, 0.70f, 0.30f, 0.95f)
            : new Color(0.63f, 0.61f, 0.70f, 0.90f);

        Label label = CreateLabel(text, 23f, titleColor, true, TextAnchor.MiddleLeft, false);
        label.style.flexGrow = 1f;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        VisualElement chevron = CreateNewProjectIcon(NewProjectIconKind.ChevronRight, actionColor, 30f);
        chevron.style.marginLeft = 12f;
        button.Add(label);
        button.Add(chevron);
        return button;
    }

    private static void StyleSidebarActionButton(Button button)
    {
        if (button == null)
            return;

        SetRadius(button, 12f);
        SetBorderWidth(button, 2f);
        ApplySidebarActionButtonState(button, false);
        button.RegisterCallback<MouseEnterEvent>(_ => ApplySidebarActionButtonState(button, true));
        button.RegisterCallback<MouseLeaveEvent>(_ => ApplySidebarActionButtonState(button, false));
    }

    private static void ApplySidebarActionButtonState(Button button, bool hover)
    {
        if (button == null)
            return;

        button.style.backgroundColor = hover
            ? new Color(1f, 1f, 1f, 0.07f)
            : new Color(0.040f, 0.038f, 0.046f, 1f);
        SetBorderColor(button, hover
            ? new Color(0.80f, 0.78f, 0.88f, 0.50f)
            : new Color(0.75f, 0.72f, 0.82f, 0.20f));
        button.style.opacity = hover ? 1f : 0.98f;
        button.style.scale = hover ? new Scale(new Vector3(1.01f, 1.01f, 1f)) : new Scale(Vector3.one);
    }

    private static void StyleSynchTheorySidebarButton(Button button)
    {
        if (button == null)
            return;

        SetRadius(button, 12f);
        SetBorderWidth(button, 2f);
        ApplySynchTheorySidebarButtonState(button, false);
        button.RegisterCallback<MouseEnterEvent>(_ => ApplySynchTheorySidebarButtonState(button, true));
        button.RegisterCallback<MouseLeaveEvent>(_ => ApplySynchTheorySidebarButtonState(button, false));
    }

    private static void ApplySynchTheorySidebarButtonState(Button button, bool hover)
    {
        if (button == null)
            return;

        button.style.backgroundColor = hover
            ? new Color(0.16f, 0.10f, 0.045f, 0.85f)
            : new Color(0.10f, 0.070f, 0.040f, 0.75f);
        SetBorderWidth(button, 2f);
        SetBorderColor(button, hover
            ? new Color(1f, 0.70f, 0.30f, 0.85f)
            : new Color(1f, 0.62f, 0.22f, 0.55f));
        button.style.opacity = hover ? 1f : 0.98f;
        button.style.scale = hover ? new Scale(new Vector3(1.01f, 1.01f, 1f)) : new Scale(Vector3.one);
    }

    private static void AddSidebarRowHoverEffect(VisualElement row, Color idleBackground, Color hoverBackground)
    {
        if (row == null)
            return;

        row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = hoverBackground);
        row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = idleBackground);
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
        row.style.alignItems = Align.FlexStart;
        row.style.paddingLeft = 26f;
        row.style.paddingRight = 22f;
        row.style.marginBottom = 12f;
        Label marker = CreateLabel("!", 20f, new Color(1f, 0.78f, 0.52f, 1f), true, TextAnchor.MiddleCenter, false);
        marker.style.width = 34f;
        marker.style.height = 34f;
        marker.style.minWidth = 34f;
        marker.style.marginRight = 12f;
        marker.style.flexShrink = 0f;
        marker.style.backgroundColor = new Color(1f, 0.65f, 0.30f, 0.10f);
        SetRadius(marker, 9f);
        SetBorderWidth(marker, 2f);
        SetBorderColor(marker, new Color(1f, 0.65f, 0.30f, 0.35f));
        row.Add(marker);
        Label detail = CreateLabel(text, 21f, new Color(0.86f, 0.82f, 0.78f, 1f), false, TextAnchor.MiddleLeft, false);
        detail.style.whiteSpace = WhiteSpace.Normal;
        detail.style.flexGrow = 1f;
        detail.style.flexShrink = 1f;
        detail.style.minWidth = 0f;
        detail.style.marginTop = 3f;
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
        Label left = CreateLabel(label.ToUpperInvariant(), 17f, new Color(0.60f, 0.58f, 0.66f, 1f), true, TextAnchor.MiddleLeft, false);
        left.style.letterSpacing = 1.5f;
        left.style.marginBottom = 3f;
        Label right = CreateLabel(value ?? string.Empty, 22f, new Color(0.94f, 0.93f, 0.96f, 1f), false, TextAnchor.MiddleLeft, false);
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
        header.style.borderBottomColor = new Color(0.20f, 0.19f, 0.24f, 1f);
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
        StylePanel(panel, new Color(0.030f, 0.036f, 0.048f, 0.99f), new Color(0.22f, 0.21f, 0.26f, 1f), 0f);

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
                // Anchor on the authoritative stored offset, not the live
                // ScrollView offset: right after a zoom rebuild the fresh
                // ScrollView reports offset 0 until its scheduled restore
                // runs, which would anchor the zoom at the song start and
                // make the view leap.
                Vector2 local = scrollView.WorldToLocal(evt.mousePosition);
                float contentX = timelineScrollOffset.x + local.x;
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
            if (toneEditorEnabled)
            {
                toneLabPanelFocused = false;
                ClearToneEditorPlaybackOverride();
                UpdateToneEditorPlaybackOverride();
                evt.StopPropagation();
                return;
            }

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

            PanTimelineDuringSeekDrag(PointerPosition(evt));
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
        timelineWindowedLayer = new VisualElement();
        timelineWindowedLayer.style.position = Position.Absolute;
        timelineWindowedLayer.style.left = 0f;
        timelineWindowedLayer.style.right = 0f;
        timelineWindowedLayer.style.top = 0f;
        timelineWindowedLayer.style.bottom = 0f;
        timelineWindowedLayer.pickingMode = PickingMode.Ignore;
        timeline.Add(timelineWindowedLayer);
        BuildBeatGrid(timelineWindowedLayer);
        BuildNotes(timelineWindowedLayer);
        BuildSyncPoints(timeline);
        BuildToneMarkers(timeline);
        BuildCursorLine(timeline);
        scrollView.Add(timeline);
        timelineScrollRestorePending = true;
        scrollView.schedule.Execute(() =>
        {
            timelineViewportWidth = Mathf.Max(1f, scrollView.contentViewport.layout.width);
            timelineScrollRestorePending = false;
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

        bool hadNonNoteSelection = !string.IsNullOrWhiteSpace(selectedSectionId) || HasSelectedAnchors();
        List<string> affectedNoteIds = new List<string>(selectedNoteIds);
        if (!string.IsNullOrWhiteSpace(selectedNoteId))
            affectedNoteIds.Add(selectedNoteId);

        ClearTimelineSelectionState();
        if (hadNonNoteSelection)
            RefreshTimelinePanel();
        else
            RefreshNoteSelectionVisuals(affectedNoteIds);
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

    private void ToggleNoteSelection(ChartEditorTrack track, ChartEditorNote note)
    {
        if (project == null || track == null || note == null || string.IsNullOrWhiteSpace(note.id))
            return;

        bool hadNonNoteSelection = !string.IsNullOrWhiteSpace(selectedSectionId) || HasSelectedAnchors();
        string previousSelectedTrackId = project.selectedTrackId;
        List<string> affectedNoteIds = new List<string>(selectedNoteIds);
        if (!string.IsNullOrWhiteSpace(selectedNoteId))
        {
            affectedNoteIds.Add(selectedNoteId);
            selectedNoteIds.Add(selectedNoteId);
        }
        affectedNoteIds.Add(note.id);

        bool selected = selectedNoteIds.Contains(note.id);
        if (selected)
        {
            selectedNoteIds.Remove(note.id);
            if (string.Equals(selectedNoteId, note.id, StringComparison.OrdinalIgnoreCase))
                selectedNoteId = selectedNoteIds.FirstOrDefault();
        }
        else
        {
            selectedNoteIds.Add(note.id);
            selectedNoteId = note.id;
            project.selectedTrackId = track.id;
        }

        if (selectedNoteIds.Count == 0)
            selectedNoteId = null;
        else if (string.IsNullOrWhiteSpace(selectedNoteId) || !selectedNoteIds.Contains(selectedNoteId))
            selectedNoteId = selectedNoteIds.FirstOrDefault();

        selectedSectionId = null;
        ClearAnchorSelection();
        mode = ChartEditorMode.Notes;

        affectedNoteIds.AddRange(selectedNoteIds);
        bool trackChanged = !string.Equals(previousSelectedTrackId ?? string.Empty, project.selectedTrackId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        if (trackChanged)
            RefreshTimelineAndSidebar();
        else if (hadNonNoteSelection)
            RefreshTimelinePanel();
        else
            RefreshNoteSelectionVisuals(affectedNoteIds);
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

        // A marquee can legitimately capture notes on several visible rows, and
        // the highlight is id-based across all of them — operations must act on
        // everything that renders selected, not just the selected track. The
        // selected track is scanned first so duplicated ids (difficulty clones)
        // deterministically resolve there.
        ChartEditorTrack selectedTrack = project.SelectedTrack;

        void CollectFromTrack(ChartEditorTrack track)
        {
            if (track?.notes == null || ids.Count == 0)
                return;

            for (int noteIndex = 0; noteIndex < track.notes.Count; noteIndex++)
            {
                ChartEditorNote note = track.notes[noteIndex];
                if (note != null && !string.IsNullOrWhiteSpace(note.id) && ids.Remove(note.id))
                    selected.Add(new ChartEditorNoteReference { track = track, note = note });
            }
        }

        CollectFromTrack(selectedTrack);
        foreach (ChartEditorTrack track in project.tracks)
        {
            if (track == null || ReferenceEquals(track, selectedTrack) || !track.visible)
                continue;

            CollectFromTrack(track);
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

        bool hadNonNoteSelection = !string.IsNullOrWhiteSpace(selectedSectionId) || HasSelectedAnchors();
        List<string> affectedNoteIds = new List<string>(selectedNoteIds);
        if (!string.IsNullOrWhiteSpace(selectedNoteId))
            affectedNoteIds.Add(selectedNoteId);

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
        affectedNoteIds.AddRange(selectedNoteIds);
        if (hadNonNoteSelection)
            RefreshTimelinePanel();
        else
            RefreshNoteSelectionVisuals(affectedNoteIds);
    }

    private void SelectNotesInRect(Rect selectionRect, bool toggleSelection)
    {
        List<ChartEditorNoteHit> hits = currentNoteHits
            .Where(hit => hit?.note != null && hit.track != null && selectionRect.Overlaps(hit.rect))
            .OrderBy(hit => hit.note.timeSeconds)
            .ThenBy(hit => hit.note.stringOrLane)
            .ToList();

        if (hits.Count == 0)
        {
            if (!toggleSelection)
                ClearTimelineSelection();
            return;
        }

        bool hadNonNoteSelection = !string.IsNullOrWhiteSpace(selectedSectionId) || HasSelectedAnchors();
        string previousSelectedTrackId = project.selectedTrackId;
        List<string> affectedNoteIds = new List<string>(selectedNoteIds);
        if (!string.IsNullOrWhiteSpace(selectedNoteId))
        {
            affectedNoteIds.Add(selectedNoteId);
            selectedNoteIds.Add(selectedNoteId);
        }

        if (toggleSelection)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                string id = hits[i].note.id;
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                affectedNoteIds.Add(id);
                if (selectedNoteIds.Contains(id))
                    selectedNoteIds.Remove(id);
                else
                    selectedNoteIds.Add(id);
            }
        }
        else
        {
            ClearNoteSelection();
            for (int i = 0; i < hits.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(hits[i].note.id))
                    selectedNoteIds.Add(hits[i].note.id);
            }
        }

        selectedNoteId = hits
            .Select(hit => hit.note?.id)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id) && selectedNoteIds.Contains(id));
        if (string.IsNullOrWhiteSpace(selectedNoteId))
            selectedNoteId = selectedNoteIds.FirstOrDefault();
        selectedSectionId = null;
        ClearAnchorSelection();
        if (!string.IsNullOrWhiteSpace(selectedNoteId))
        {
            ChartEditorNoteHit primaryHit = hits.FirstOrDefault(hit => string.Equals(hit.note?.id, selectedNoteId, StringComparison.OrdinalIgnoreCase));
            if (primaryHit?.track != null)
                project.selectedTrackId = primaryHit.track.id;
        }
        mode = ChartEditorMode.Notes;
        ChartEditorNoteHit cursorHit = hits.FirstOrDefault(hit => string.Equals(hit.note?.id, selectedNoteId, StringComparison.OrdinalIgnoreCase));
        if (cursorHit?.note != null)
            project.cursorTimeSeconds = cursorHit.note.timeSeconds;
        affectedNoteIds.AddRange(selectedNoteIds);
        bool trackChanged = !string.Equals(previousSelectedTrackId ?? string.Empty, project.selectedTrackId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        if (trackChanged)
            RefreshTimelineAndSidebar();
        else if (hadNonNoteSelection)
            RefreshTimelinePanel();
        else
            RefreshNoteSelectionVisuals(affectedNoteIds);
    }

    private void StartMarqueeSelection(VisualElement timeline, PointerDownEvent evt, Vector2 localStart)
    {
        ClearMarqueeSelection();
        marqueeSelecting = true;
        marqueePointerId = evt.pointerId;
        marqueeStart = localStart;
        marqueeMoved = false;
        marqueeToggleSelection = evt.ctrlKey || IsControlKeyHeld();
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
        bool toggleSelection = marqueeToggleSelection;
        ClearMarqueeSelection();
        if (timeline != null && timeline.HasPointerCapture(pointerId))
            timeline.ReleasePointer(pointerId);

        if (moved)
            SelectNotesInRect(rect, toggleSelection);
        else if (!toggleSelection)
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
        marqueeToggleSelection = false;
    }

    private void CaptureCurrentTimelineScrollOffset()
    {
        CaptureTimelineScrollOffset(currentTimelineScrollView);
    }

    private void CaptureTimelineScrollOffset(ScrollView scrollView)
    {
        if (scrollView == null)
            return;

        // A freshly rebuilt timeline sits at scroll offset zero until its
        // scheduled restore runs. Capturing during that window would stomp
        // the stored position and snap the view back to the song start.
        // Likewise, while a zoom refresh is queued the stored offset was
        // computed for the NEW zoom level — capturing the old view's offset
        // would pair the new zoom with a stale position and drift the view.
        if (timelineScrollRestorePending || timelineRefreshQueued)
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

        double safeTime = Math.Max(0.0, Math.Min(GetProjectDurationSeconds(), timeSeconds));
        if (toneEditorEnabled)
        {
            ShowContextMenu(worldPosition,
                new ContextMenuItem($"Add Tone Change at {FormatTime(safeTime)}", () => AddToneChangeAtTime(safeTime)),
                new ContextMenuItem("Assign Current Tone to Selected", () =>
                {
                    EnsureToneSelectionForCurrentTrack();
                    if (selectedToneChange != null)
                        AssignCurrentToneToChange(selectedToneChange);
                    else
                        AddToneChangeAtTime(safeTime);
                }),
                new ContextMenuItem("Zoom In", () => AdjustTimelineZoomAroundViewportCenter(1)),
                new ContextMenuItem("Zoom Out", () => AdjustTimelineZoomAroundViewportCenter(-1)),
                new ContextMenuItem("Reset Zoom", ResetTimelineZoom));
            return;
        }

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
            new ContextMenuItem("Duplicate Difficulty", () => DuplicateDifficulty(track)),
            new ContextMenuItem("Rename Difficulty", () => ShowRenameDifficultyPopup(track)),
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
            new ContextMenuItem("Delete Difficulty", () => DeleteTrack(track)),
            new ContextMenuItem("Edit Song Info", ShowSongInfoPopup));
    }

    private void ShowDifficultyContextMenu(Vector2 worldPosition, ChartEditorTrack track)
    {
        if (track == null)
            return;

        ShowContextMenu(worldPosition,
            new ContextMenuItem(string.Equals(project.selectedTrackId, track.id, StringComparison.OrdinalIgnoreCase) ? "Selected: ON" : "Select Difficulty", () => SelectDifficultyTrack(track)),
            new ContextMenuItem("Rename Difficulty", () => ShowRenameDifficultyPopup(track)),
            new ContextMenuItem("Duplicate Difficulty", () => DuplicateDifficulty(track)),
            new ContextMenuItem("Delete Difficulty", () => DeleteTrack(track)));
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
            new ContextMenuItem("Edit Note", () => ShowNoteEditPopup(track, note))
        };
        if (!IsDrumTrack(track))
            items.Add(new ContextMenuItem("Technique Settings...", () => ShowTechniqueSettingsPopup(track, note)));
        items.Add(new ContextMenuItem("Move to Cursor", MoveSelectedNotesToCursor));
        items.Add(new ContextMenuItem("Selection",
            new ContextMenuItem("Select After on This String", () => SelectNotesAfter(track, note, sameStringOnly: true)),
            new ContextMenuItem("Select After on All Strings", () => SelectNotesAfter(track, note, sameStringOnly: false))));
        ContextMenuItem[] techniqueItems = BuildTechniqueContextItems(new[] { new ChartEditorNoteReference { track = track, note = note } });
        if (techniqueItems.Length > 0)
            items.Add(new ContextMenuItem("Techniques", techniqueItems));
        items.Add(new ContextMenuItem("Copy Note", CopySelectedNotes));
        items.Add(new ContextMenuItem("Quantize to Beat Grid", QuantizeSelectedNotesToBeatGrid));
        items.Add(new ContextMenuItem("Duplicate Note", () => DuplicateNote(note)));
        items.Add(new ContextMenuItem("Delete Note", () => DeleteNote(note)));
        items.Add(new ContextMenuItem("Add Note Here", () => AddNoteAtTime(note.timeSeconds)));
        if (HasCopiedNotes())
            items.Insert(Math.Min(4, items.Count), new ContextMenuItem("Paste Notes Here", () => PasteCopiedNotesAt(note.timeSeconds)));
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
                new ContextMenuItem("Select After on All Strings", () => SelectNotesAfter(clickedTrack, clickedNote, sameStringOnly: false)))
        };
        ContextMenuItem[] techniqueItems = BuildTechniqueContextItems(selectedNotes);
        if (techniqueItems.Length > 0)
            items.Add(new ContextMenuItem("Techniques", techniqueItems));
        items.Add(new ContextMenuItem($"Copy {count} Notes", CopySelectedNotes));
        items.Add(new ContextMenuItem("Quantize Selected to Beat Grid", QuantizeSelectedNotesToBeatGrid));
        items.Add(new ContextMenuItem($"Duplicate {count} Notes", DuplicateSelectedNotes));
        items.Add(new ContextMenuItem($"Delete {count} Notes", DeleteSelectedNotes));
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
        if (IsDrumTrack(track))
        {
            if (ChartEditorDrumNoteSanitizer.Sanitize(note))
            {
                project.dirty = true;
                MarkHighwayPreviewDirty();
                Rebuild();
            }
            SetStatus("Drum notes do not use guitar techniques.");
            return;
        }

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
        ChartEditorTrack track = group?.activeTrack ?? SelectPreferredDifficultyTrack(group?.tracks);
        if (track == null)
            return;

        SelectDifficultyTrack(track);
    }

    private void SelectDifficultyTrack(ChartEditorTrack track)
    {
        if (track == null)
            return;

        project.selectedTrackId = track.id;
        SetExclusiveVisibleTrack(track, markDirty: true);
        ClearNoteSelection();
        Rebuild();
    }

    private void ShowRenameDifficultyPopup(ChartEditorTrack track)
    {
        if (track == null)
            return;

        TextField labelField = CreatePopupTextField("Difficulty", FormatDifficultyLabel(track));
        ShowEditPopup("Rename Difficulty", new VisualElement[] { labelField }, () =>
        {
            string value = labelField.value?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                SetStatus("Enter a difficulty name.");
                return false;
            }

            track.difficultyLabel = value;
            track.difficultyUiIndex = ResolveDifficultyUiIndex(track);
            project.dirty = true;
            HideEditPopup();
            Rebuild();
            return true;
        });
    }

    private void DuplicateDifficulty(ChartEditorTrack source)
    {
        if (project?.tracks == null || source == null)
            return;

        string json = JsonUtility.ToJson(source);
        ChartEditorTrack clone = JsonUtility.FromJson<ChartEditorTrack>(json) ?? new ChartEditorTrack();
        clone.id = BuildUniqueTrackId($"{source.id}_copy");
        clone.difficultyLabel = BuildDuplicateDifficultyLabel(source);
        clone.difficultyUiIndex = BuildDuplicateDifficultyIndex(source);
        clone.hasDifficultyVariants = true;
        clone.visible = false;
        clone.solo = false;
        clone.muted = source.muted;
        clone.notes ??= new List<ChartEditorNote>();
        for (int i = 0; i < clone.notes.Count; i++)
        {
            if (clone.notes[i] != null)
                clone.notes[i].id = Guid.NewGuid().ToString("N");
        }

        clone.EnsureDefaults();
        if (clone.generatedNotes != null && clone.generatedNotes.Count > 0)
            clone.generatedPlaybackNoteFingerprint = ChartEditorGeneratedPlaybackIntegrity.ComputeNoteFingerprint(clone);

        int sourceIndex = project.tracks.FindIndex(track => ReferenceEquals(track, source) ||
                                                            string.Equals(track?.id ?? string.Empty, source.id ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        int insertIndex = sourceIndex >= 0 ? sourceIndex + 1 : project.tracks.Count;
        project.tracks.Insert(insertIndex, clone);
        project.EnsureDefaults();
        project.selectedTrackId = clone.id;
        SetExclusiveVisibleTrack(clone, markDirty: false);
        ClearNoteSelection();
        selectedSectionId = null;
        ClearAnchorSelection();
        project.dirty = true;
        MarkHighwayPreviewDirty();
        SetStatus($"Duplicated difficulty \"{FormatDifficultyLabel(source)}\".");
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

    private string BuildUniqueTrackId(string seed)
    {
        string baseId = string.IsNullOrWhiteSpace(seed) ? "track_copy" : seed.Trim();
        string candidate = baseId;
        int suffix = 2;
        while (project?.tracks != null &&
               project.tracks.Any(track => track != null && string.Equals(track.id ?? string.Empty, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{baseId}_{suffix++}";
        }

        return candidate;
    }

    private string BuildDuplicateDifficultyLabel(ChartEditorTrack source)
    {
        string baseLabel = FormatDifficultyLabel(source);
        string candidate = $"{baseLabel} Copy";
        int suffix = 2;
        HashSet<string> existing = new HashSet<string>(
            project?.tracks?
                .Where(track => track != null &&
                                string.Equals(GetTrackGroupKey(track), GetTrackGroupKey(source), StringComparison.OrdinalIgnoreCase))
                .Select(FormatDifficultyLabel) ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        while (existing.Contains(candidate))
            candidate = $"{baseLabel} Copy {suffix++}";

        return candidate;
    }

    private int BuildDuplicateDifficultyIndex(ChartEditorTrack source)
    {
        int max = -1;
        if (project?.tracks != null)
        {
            string groupKey = GetTrackGroupKey(source);
            for (int i = 0; i < project.tracks.Count; i++)
            {
                ChartEditorTrack track = project.tracks[i];
                if (track == null ||
                    !string.Equals(GetTrackGroupKey(track), groupKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int index = ResolveDifficultyUiIndex(track);
                if (index != int.MaxValue)
                    max = Mathf.Max(max, index);
            }
        }

        return max + 1;
    }

    private void EnsureSingleVisibleTrack(bool markDirty)
    {
        if (project?.tracks == null || project.tracks.Count == 0)
            return;

        ChartEditorTrack selectedTrack = project.SelectedTrack;
        if (selectedTrack == null)
        {
            selectedTrack = SelectPreferredDifficultyTrack(project.tracks);
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
            track.arrangementRoute = ArrangementRouteForTrackRole(role);
            track.arrangementInstrumentType = ArrangementInstrumentTypeForTrackRole(role);
            if (roleChanged)
            {
                ApplyGeneratedPartRoleDefaults(track, role);
                if (role == ChartEditorTrackRole.Drums)
                    SanitizeDrumTrackTechniqueData(track);
            }
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

            if (!TryParseDoubleInRange(timeField.value, 0.0, GetProjectDurationSeconds(), out double time))
            {
                SetStatus($"Time must be between 0 and {GetProjectDurationSeconds():0.000} seconds.");
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

        double noteLimit = Math.Max(0.06, (project != null ? GetProjectDurationSeconds() : note.timeSeconds + GetNoteEffectiveDurationSeconds(note)) - note.timeSeconds);
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
        if (IsDrumTrack(track))
        {
            if (ChartEditorDrumNoteSanitizer.Sanitize(note))
            {
                project.dirty = true;
                MarkHighwayPreviewDirty();
                Rebuild();
            }
            SetStatus("Drum notes do not use guitar techniques.");
            return;
        }

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
        int linkedFromNoteId = note.linkedFromNoteId;

        VisualElement overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.right = 0f;
        overlay.style.top = 0f;
        overlay.style.bottom = 0f;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.66f);
        overlay.pickingMode = PickingMode.Position;
        overlay.RegisterCallback<PointerDownEvent>(evt =>
        {
            HideEditPopup();
            evt.StopPropagation();
        });

        VisualElement panel = new VisualElement();
        panel.style.width = 1650f;
        panel.style.height = Length.Percent(98f);
        panel.style.minHeight = Length.Percent(92f);
        panel.style.maxWidth = Length.Percent(96f);
        panel.style.maxHeight = Length.Percent(98f);
        panel.style.paddingLeft = 52f;
        panel.style.paddingRight = 52f;
        panel.style.paddingTop = 46f;
        panel.style.paddingBottom = 40f;
        StylePopupPanel(panel, TechniqueSettingsPanelBackgroundColor, 14f);
        SetTechniqueSettingsBorder(panel, TechniqueSettingsModalBorderColor, 2f, 14f);
        panel.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 24f;
        VisualElement headerText = new VisualElement();
        Label title = CreateTechniqueSettingsLabel("Technique Settings", 42f, Color.white, true, TextAnchor.MiddleLeft);
        Label subtitle = CreateTechniqueSettingsLabel($"Note {note.fret}  -  {FormatTime(note.timeSeconds)}  -  {GetNoteEffectiveDurationSeconds(note):0.000}s", 26f, new Color(0.68f, 0.71f, 0.77f, 1f), false, TextAnchor.MiddleLeft);
        subtitle.style.marginTop = 8f;
        headerText.Add(title);
        headerText.Add(subtitle);
        header.Add(headerText);
        Button close = CreateTechniqueSettingsButton("X", HideEditPopup);
        StyleTechniqueSettingsIconButton(close, new Color(0.84f, 0.86f, 0.90f, 1f));
        close.style.width = 58f;
        close.style.minWidth = 58f;
        close.style.height = 58f;
        close.style.fontSize = UiFont(28f);
        header.Add(close);
        panel.Add(header);
        panel.Add(CreateTechniqueSettingsDivider(30f));

        VisualElement addRow = new VisualElement();
        addRow.style.flexDirection = FlexDirection.Row;
        addRow.style.alignItems = Align.Center;
        addRow.style.marginBottom = 24f;
        Button addFlagButton = CreateTechniqueSettingsAddButton("Add Flag", null);
        Button addSegmentButton = CreateTechniqueSettingsAddButton("Add Segment", null);
        addRow.Add(addFlagButton);
        addRow.Add(addSegmentButton);
        panel.Add(addRow);

        panel.Add(CreateTechniqueSettingsSectionLabel("Flags"));
        VisualElement selectedFlags = new VisualElement();
        selectedFlags.style.flexDirection = FlexDirection.Row;
        selectedFlags.style.flexWrap = Wrap.Wrap;
        selectedFlags.style.marginBottom = 18f;
        panel.Add(selectedFlags);

        VisualElement flagDetails = new VisualElement();
        flagDetails.style.marginBottom = 28f;
        panel.Add(flagDetails);

        TextField linkedFromField = null;

        int ResolveNoteExportId(ChartEditorNote source)
        {
            if (source == null)
                return -1;
            if (source.sourceNoteId >= 0)
                return source.sourceNoteId;

            List<ChartEditorNote> ordered = track?.notes?
                .Where(candidate => candidate != null)
                .OrderBy(candidate => candidate.timeSeconds)
                .ThenBy(candidate => candidate.stringOrLane)
                .ToList();
            int resolvedIndex = ordered?.IndexOf(source) ?? -1;
            return resolvedIndex >= 0 ? resolvedIndex : -1;
        }

        int FindPreviousLinkedNoteId()
        {
            if (track?.notes == null)
                return -1;

            ChartEditorNote previous = track.notes
                .Where(candidate =>
                    candidate != null &&
                    !ReferenceEquals(candidate, note) &&
                    candidate.timeSeconds < note.timeSeconds - 0.0001 &&
                    candidate.stringOrLane == note.stringOrLane)
                .OrderByDescending(candidate => candidate.timeSeconds)
                .ThenByDescending(candidate => candidate.fret)
                .FirstOrDefault();
            previous ??= track.notes
                .Where(candidate =>
                    candidate != null &&
                    !ReferenceEquals(candidate, note) &&
                    candidate.timeSeconds < note.timeSeconds - 0.0001)
                .OrderByDescending(candidate => candidate.timeSeconds)
                .ThenByDescending(candidate => candidate.stringOrLane)
                .FirstOrDefault();
            return ResolveNoteExportId(previous);
        }

        void EnsureLinkedFromPrevious()
        {
            if (linkedFromNoteId >= 0)
                return;

            linkedFromNoteId = FindPreviousLinkedNoteId();
            if (linkedFromField != null)
                linkedFromField.value = linkedFromNoteId >= 0 ? linkedFromNoteId.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        void SetHammerOn(bool value)
        {
            hammerOn = value;
            if (hammerOn)
            {
                pullOff = false;
                legato = true;
                requiresPluck = false;
                EnsureLinkedFromPrevious();
            }
            else if (!pullOff)
            {
                legato = false;
                requiresPluck = true;
                linkedFromNoteId = -1;
            }
        }

        void SetPullOff(bool value)
        {
            pullOff = value;
            if (pullOff)
            {
                hammerOn = false;
                legato = true;
                requiresPluck = false;
                EnsureLinkedFromPrevious();
            }
            else if (!hammerOn)
            {
                legato = false;
                requiresPluck = true;
                linkedFromNoteId = -1;
            }
        }

        void SetPalmMuteFlag(bool value)
        {
            palmMute = value;
            if (palmMute)
                fretHandMute = false;
        }

        void SetFretHandMuteFlag(bool value)
        {
            fretHandMute = value;
            if (fretHandMute)
                palmMute = false;
        }

        void SetNaturalHarmonicFlag(bool value)
        {
            harmonic = value;
            if (harmonic)
                pinchHarmonic = false;
        }

        void SetPinchHarmonicFlag(bool value)
        {
            pinchHarmonic = value;
            if (pinchHarmonic)
                harmonic = false;
        }

        void AddFlagChip(string label, Action remove)
        {
            selectedFlags.Add(CreateTechniqueSettingsChip(label, remove));
        }

        void RebuildFlagDetails()
        {
            flagDetails.Clear();
            linkedFromField = null;
            if (!hammerOn && !pullOff && !legato && requiresPluck)
                return;

            VisualElement detailRow = new VisualElement();
            detailRow.style.flexDirection = FlexDirection.Column;
            detailRow.style.paddingLeft = 26f;
            detailRow.style.paddingRight = 26f;
            detailRow.style.paddingTop = 22f;
            detailRow.style.paddingBottom = 22f;
            detailRow.style.marginTop = 2f;
            detailRow.style.backgroundColor = TechniqueSettingsSurfaceColor;
            SetRadius(detailRow, 8f);
            SetTechniqueSettingsBorder(detailRow, TechniqueSettingsBorderColor, 2f, 8f);

            VisualElement linkControls = new VisualElement();
            linkControls.style.flexDirection = FlexDirection.Column;

            linkedFromField = CreateTechniqueSettingsTextField(
                "Source Note ID",
                linkedFromNoteId >= 0 ? linkedFromNoteId.ToString(CultureInfo.InvariantCulture) : string.Empty,
                310f);
            linkedFromField.style.width = Length.Percent(100f);
            linkedFromField.style.marginRight = 0f;
            linkControls.Add(linkedFromField);

            VisualElement linkButtonRow = new VisualElement();
            linkButtonRow.style.flexDirection = FlexDirection.Row;
            linkButtonRow.style.flexWrap = Wrap.Wrap;
            linkButtonRow.style.alignItems = Align.Center;
            linkButtonRow.style.marginTop = 4f;

            Button previous = CreateTechniqueSettingsAddButton("Use Previous Note", () =>
            {
                linkedFromNoteId = FindPreviousLinkedNoteId();
                linkedFromField.value = linkedFromNoteId >= 0 ? linkedFromNoteId.ToString(CultureInfo.InvariantCulture) : string.Empty;
            });
            previous.style.width = 340f;
            previous.style.minWidth = 340f;
            linkButtonRow.Add(previous);

            Button clear = CreateTechniqueSettingsAddButton("Clear Link", () =>
            {
                linkedFromNoteId = -1;
                linkedFromField.value = string.Empty;
            });
            clear.style.width = 220f;
            clear.style.minWidth = 220f;
            linkButtonRow.Add(clear);
            linkControls.Add(linkButtonRow);
            detailRow.Add(linkControls);
            flagDetails.Add(detailRow);
        }

        void RebuildFlags()
        {
            selectedFlags.Clear();
            if (hammerOn)
                AddFlagChip("Hammer-On", () => { SetHammerOn(false); RebuildFlags(); });
            if (pullOff)
                AddFlagChip("Pull-Off", () => { SetPullOff(false); RebuildFlags(); });
            if (palmMute)
                AddFlagChip("Palm Mute", () => { SetPalmMuteFlag(false); RebuildFlags(); });
            if (fretHandMute)
                AddFlagChip("Fret-Hand Mute", () => { SetFretHandMuteFlag(false); RebuildFlags(); });
            if (legato && !hammerOn && !pullOff)
                AddFlagChip("Legato", () =>
                {
                    legato = false;
                    requiresPluck = true;
                    linkedFromNoteId = -1;
                    RebuildFlags();
                });
            if (!requiresPluck && !hammerOn && !pullOff && !legato)
                AddFlagChip("No Pluck", () =>
                {
                    requiresPluck = true;
                    linkedFromNoteId = -1;
                    RebuildFlags();
                });
            if (harmonic)
                AddFlagChip("Natural Harmonic", () => { SetNaturalHarmonicFlag(false); RebuildFlags(); });
            if (pinchHarmonic)
                AddFlagChip("Pinch Harmonic", () => { SetPinchHarmonicFlag(false); RebuildFlags(); });
            if (accentFlag)
                AddFlagChip("Accent", () => { accentFlag = false; RebuildFlags(); });
            if (tap)
                AddFlagChip("Tap", () => { tap = false; RebuildFlags(); });
            if (tremolo)
                AddFlagChip("Tremolo", () => { tremolo = false; RebuildFlags(); });

            if (selectedFlags.childCount == 0)
                selectedFlags.Add(CreateTechniqueSettingsEmptyFlags());

            RebuildFlagDetails();
        }

        panel.Add(CreateTechniqueSettingsSectionLabel("Segments"));

        ScrollView listScroll = new ScrollView(ScrollViewMode.Vertical);
        ConfigureScrollView(listScroll);
        listScroll.style.height = new StyleLength(StyleKeyword.Auto);
        listScroll.style.minHeight = 620f;
        listScroll.style.flexGrow = 1f;
        listScroll.style.flexShrink = 1f;
        listScroll.style.marginBottom = 0f;
        listScroll.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        VisualElement list = new VisualElement();
        list.style.paddingLeft = 0f;
        list.style.paddingRight = 0f;
        list.style.paddingTop = 8f;
        list.style.paddingBottom = 8f;
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
                list.Add(CreateTechniqueSettingsEmptyState(
                    170f,
                    "No segments added yet.",
                    "Use \"Add Segment\" to create one.",
                    CreateTechniqueSettingsSegmentEmptyIcon()));
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

        void AddBendAndRebuild(float startBend, float endBend)
        {
            CaptureRows();
            AddTechniqueSettingsBendSegment(rowStates, note, startBend, endBend);
            RebuildRows();
        }

        void AddBentCarryAndRebuild(NoteTechniqueSegmentType type, float bend)
        {
            CaptureRows();
            AddTechniqueSettingsSegment(rowStates, note, type);
            TechniqueSettingsRowState last = rowStates.LastOrDefault();
            if (last?.segment != null)
            {
                last.segment.startBend = Mathf.Max(0f, bend);
                last.segment.endBend = Mathf.Max(0f, bend);
            }
            RebuildRows();
        }

        void ShowMenuFromButton(Button button, params ContextMenuItem[] items)
        {
            if (button == null)
                return;

            ShowContextMenu(new Vector2(button.worldBound.xMin, button.worldBound.yMax + 8f), items);
        }

        ContextMenuItem FlagItem(string label, bool enabled, Action add)
        {
            return new ContextMenuItem((enabled ? "On: " : string.Empty) + label, () =>
            {
                add?.Invoke();
                RebuildFlags();
            });
        }

        addFlagButton.clicked += () => ShowMenuFromButton(addFlagButton,
            FlagItem("Hammer-On", hammerOn, () => SetHammerOn(true)),
            FlagItem("Pull-Off", pullOff, () => SetPullOff(true)),
            FlagItem("Palm Mute", palmMute, () => SetPalmMuteFlag(true)),
            FlagItem("Fret-Hand Mute", fretHandMute, () => SetFretHandMuteFlag(true)),
            FlagItem("Legato", legato && !hammerOn && !pullOff, () =>
            {
                legato = true;
                requiresPluck = false;
                EnsureLinkedFromPrevious();
            }),
            FlagItem("No Pluck", !requiresPluck && !hammerOn && !pullOff && !legato, () =>
            {
                requiresPluck = false;
                EnsureLinkedFromPrevious();
            }),
            FlagItem("Natural Harmonic", harmonic, () => SetNaturalHarmonicFlag(true)),
            FlagItem("Pinch Harmonic", pinchHarmonic, () => SetPinchHarmonicFlag(true)),
            FlagItem("Accent", accentFlag, () => accentFlag = true),
            FlagItem("Tap", tap, () => tap = true),
            FlagItem("Tremolo", tremolo, () => tremolo = true));

        addSegmentButton.clicked += () => ShowMenuFromButton(addSegmentButton,
            new ContextMenuItem("Sustain", () => AddAndRebuild(NoteTechniqueSegmentType.Sustain)),
            new ContextMenuItem("Vibrato", () => AddAndRebuild(NoteTechniqueSegmentType.Vibrato)),
            new ContextMenuItem("Bent Sustain", () => AddBentCarryAndRebuild(NoteTechniqueSegmentType.Sustain, FullStepBendSemitones)),
            new ContextMenuItem("Bent Vibrato", () => AddBentCarryAndRebuild(NoteTechniqueSegmentType.Vibrato, FullStepBendSemitones)),
            new ContextMenuItem("Half Bend", () => AddBendAndRebuild(0f, HalfStepBendSemitones)),
            new ContextMenuItem("Full Bend", () => AddBendAndRebuild(0f, FullStepBendSemitones)),
            new ContextMenuItem("Half Pre-Bend", () => AddBendAndRebuild(HalfStepBendSemitones, HalfStepBendSemitones)),
            new ContextMenuItem("Full Pre-Bend", () => AddBendAndRebuild(FullStepBendSemitones, FullStepBendSemitones)),
            new ContextMenuItem("Half Release", () => AddBendAndRebuild(HalfStepBendSemitones, 0f)),
            new ContextMenuItem("Full Release", () => AddBendAndRebuild(FullStepBendSemitones, 0f)),
            new ContextMenuItem("Slide", () => AddAndRebuild(NoteTechniqueSegmentType.Slide)));

        RebuildFlags();
        RebuildRows();

        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.justifyContent = Justify.FlexEnd;
        actions.style.marginTop = 30f;
        Button cancel = CreateTechniqueSettingsButton("Cancel", HideEditPopup);
        Button apply = CreateTechniqueSettingsButton("Apply", () =>
        {
            if (linkedFromField != null)
            {
                string linkedValue = linkedFromField.value?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(linkedValue))
                {
                    linkedFromNoteId = -1;
                }
                else if (!int.TryParse(linkedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out linkedFromNoteId) ||
                         linkedFromNoteId < 0)
                {
                    SetStatus("Source Note ID must be empty or a non-negative note id.");
                    return;
                }
            }

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
            note.hasRuntimeMuted = true;
            note.runtimeMuted = note.muted;
            note.hasRuntimePalmMute = true;
            note.runtimePalmMute = note.palmMute;
            note.harmonic = harmonic;
            note.pinchHarmonic = pinchHarmonic;
            note.accent = accentFlag;
            note.tap = tap;
            note.tremolo = tremolo;
            note.legato = legato || hammerOn || pullOff;
            note.requiresPluck = hammerOn || pullOff ? false : requiresPluck;
            note.linkedFromNoteId = note.legato || !note.requiresPluck ? linkedFromNoteId : -1;
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
        panel.Add(CreateTechniqueSettingsDivider(22f, 0f));
        StyleTechniqueSettingsFlatButton(cancel, TechniqueSettingsButtonBackgroundColor, Color.white, TechniqueSettingsBorderColor, TechniqueSettingsButtonHoverColor, TechniqueSettingsBorderColor);
        StyleTechniqueSettingsFlatButton(apply, TechniqueSettingsAccentColor, Color.white, TechniqueSettingsAccentColor, new Color(0.94f, 0.58f, 0.19f, 1f), TechniqueSettingsAccentColor);
        cancel.style.height = 76f;
        cancel.style.minWidth = 230f;
        cancel.style.width = 230f;
        apply.style.height = 76f;
        apply.style.minWidth = 230f;
        apply.style.width = 230f;
        cancel.style.fontSize = UiFont(26f);
        apply.style.fontSize = UiFont(26f);
        SetRadius(cancel, 7f);
        SetRadius(apply, 7f);
        cancel.style.marginRight = 18f;
        actions.Add(cancel);
        actions.Add(apply);
        panel.Add(actions);

        overlay.Add(panel);
        ApplyTechniqueSettingsTypographyTree(panel);
        editPopupElement = overlay;
        RootElement.Add(editPopupElement);
        editPopupElement.BringToFront();
        SetChartEditorKeyboardCaptureActive(true);
    }

    private Label CreateTechniqueSettingsSectionLabel(string text)
    {
        Label label = CreateTechniqueSettingsLabel(text, 26f, Color.white, true, TextAnchor.MiddleLeft);
        label.style.marginBottom = 14f;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        return label;
    }

    private static Color TechniqueSettingsPanelBackgroundColor => new Color(0.075f, 0.086f, 0.100f, 0.985f);
    private static Color TechniqueSettingsSurfaceColor => new Color(0.092f, 0.105f, 0.123f, 0.96f);
    private static Color TechniqueSettingsInputColor => new Color(0.052f, 0.060f, 0.073f, 0.98f);
    private static Color TechniqueSettingsAccentColor => new Color(0.86f, 0.50f, 0.16f, 1f);
    private static Color TechniqueSettingsBorderColor => new Color(0.36f, 0.39f, 0.44f, 0.86f);
    private static Color TechniqueSettingsSubtleBorderColor => new Color(0.48f, 0.51f, 0.56f, 0.58f);
    private static Color TechniqueSettingsModalBorderColor => new Color(0.54f, 0.57f, 0.62f, 0.86f);
    private static Color TechniqueSettingsButtonBackgroundColor => new Color(0.088f, 0.100f, 0.116f, 0.96f);
    private static Color TechniqueSettingsButtonHoverColor => new Color(0.130f, 0.145f, 0.166f, 0.98f);

    private Label CreateTechniqueSettingsLabel(string text, float size, Color color, bool bold, TextAnchor align)
    {
        Label label = new Label(text ?? string.Empty);
        label.style.fontSize = UiFont(size);
        label.style.color = color;
        label.style.unityFontDefinition = bodyFont;
        label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        label.style.unityTextAlign = align;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginLeft = 0f;
        label.style.marginRight = 0f;
        label.style.marginTop = 0f;
        label.style.marginBottom = 0f;
        ApplyTechniqueSettingsTypography(label);
        return label;
    }

    private Button CreateTechniqueSettingsButton(string text, Action action)
    {
        Button button = new Button(action) { text = text ?? string.Empty };
        button.focusable = false;
        button.style.height = 66f;
        button.style.minWidth = TechniqueSettingsButtonMinWidthForText(text, 180f);
        button.style.marginLeft = 0f;
        button.style.marginRight = 0f;
        button.style.marginTop = 0f;
        button.style.marginBottom = 0f;
        button.style.flexShrink = 0f;
        button.style.paddingLeft = 18f;
        button.style.paddingRight = 18f;
        button.style.fontSize = UiFont(23f);
        button.style.unityFontDefinition = bodyFont;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.Center;
        button.style.whiteSpace = WhiteSpace.NoWrap;
        button.style.overflow = Overflow.Visible;
        ApplyTechniqueSettingsTypography(button);
        StyleTechniqueSettingsFlatButton(
            button,
            TechniqueSettingsButtonBackgroundColor,
            Color.white,
            TechniqueSettingsBorderColor,
            TechniqueSettingsButtonHoverColor,
            TechniqueSettingsBorderColor);
        return button;
    }

    private void ApplyTechniqueSettingsTypography(VisualElement element)
    {
        if (element == null)
            return;

        element.style.unityFontDefinition = bodyFont;
        element.style.letterSpacing = 0f;
    }

    private void ApplyTechniqueSettingsTypographyTree(VisualElement root)
    {
        if (root == null)
            return;

        ApplyTechniqueSettingsTypography(root);
        foreach (VisualElement child in root.Children())
            ApplyTechniqueSettingsTypographyTree(child);
    }

    private static float TechniqueSettingsButtonMinWidthForText(string text, float floor)
    {
        int length = string.IsNullOrWhiteSpace(text) ? 0 : text.Trim().Length;
        return Mathf.Max(floor, length * 15f + 70f);
    }

    private static void SetTechniqueSettingsBorder(VisualElement element, Color color, float width = 1f, float radius = -1f)
    {
        if (element == null)
            return;

        SetBorderWidth(element, width);
        SetBorderColor(element, color);
        if (radius >= 0f)
            SetRadius(element, radius);
    }

    private static void SetTechniqueSettingsBorder(VisualElement element)
    {
        SetTechniqueSettingsBorder(element, TechniqueSettingsBorderColor);
    }

    private void StyleTechniqueSettingsFlatButton(
        Button button,
        Color restBackground,
        Color restText,
        Color restBorder,
        Color hoverBackground,
        Color hoverBorder)
    {
        if (button == null)
            return;

        ApplyTechniqueSettingsTypography(button);
        SetRadius(button, 7f);
        button.style.backgroundImage = StyleKeyword.None;
        button.style.backgroundColor = restBackground;
        button.style.color = restText;
        button.style.unityFontDefinition = bodyFont;
        button.style.flexShrink = 0f;
        SetTechniqueSettingsBorder(button, restBorder, 2f, 7f);
        button.schedule.Execute(() =>
        {
            button.style.backgroundImage = StyleKeyword.None;
            button.style.backgroundColor = restBackground;
            button.style.color = restText;
            button.style.unityFontDefinition = bodyFont;
            SetTechniqueSettingsBorder(button, restBorder, 2f, 7f);
        }).ExecuteLater(0);
        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            button.style.backgroundColor = hoverBackground;
            button.style.color = Color.white;
            SetTechniqueSettingsBorder(button, hoverBorder, 2f, 7f);
        });
        button.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            button.style.backgroundColor = restBackground;
            button.style.color = restText;
            SetTechniqueSettingsBorder(button, restBorder, 2f, 7f);
        });
        button.RegisterCallback<FocusInEvent>(_ => SetTechniqueSettingsBorder(button, hoverBorder, 2f, 7f));
        button.RegisterCallback<FocusOutEvent>(_ => SetTechniqueSettingsBorder(button, restBorder, 2f, 7f));
    }

    private VisualElement CreateTechniqueSettingsDivider(float marginBottom, float marginTop = 0f)
    {
        VisualElement divider = new VisualElement();
        divider.style.height = 1f;
        divider.style.marginTop = marginTop;
        divider.style.marginBottom = marginBottom;
        divider.style.backgroundColor = new Color(1f, 1f, 1f, 0.13f);
        return divider;
    }

    private VisualElement CreateTechniqueSettingsEmptyFlags()
    {
        VisualElement empty = CreateTechniqueSettingsEmptyState(
            122f,
            "No flags added yet.",
            "Use \"Add Flag\" to create one.",
            CreateTechniqueSettingsFlagEmptyIcon());
        empty.style.width = Length.Percent(100f);
        return empty;
    }

    private VisualElement CreateTechniqueSettingsEmptyState(float height, string titleText, string detailText, VisualElement icon)
    {
        VisualElement box = new VisualElement();
        box.style.position = Position.Relative;
        box.style.height = height;
        box.style.marginBottom = 12f;
        box.style.paddingLeft = 28f;
        box.style.paddingRight = 28f;
        box.style.paddingTop = 20f;
        box.style.paddingBottom = 20f;
        box.style.alignItems = Align.Center;
        box.style.justifyContent = Justify.Center;
        box.style.backgroundColor = new Color(0.055f, 0.064f, 0.076f, 0.26f);
        SetRadius(box, 8f);
        SetTechniqueSettingsBorder(box, TechniqueSettingsSubtleBorderColor, 2f, 8f);

        VisualElement content = new VisualElement();
        content.style.flexDirection = FlexDirection.Row;
        content.style.alignItems = Align.Center;
        content.style.justifyContent = Justify.Center;
        content.style.maxWidth = 520f;

        if (icon != null)
        {
            icon.style.marginRight = 24f;
            content.Add(icon);
        }

        VisualElement text = new VisualElement();
        text.style.flexDirection = FlexDirection.Column;
        Label title = CreateTechniqueSettingsLabel(titleText, 24f, new Color(0.72f, 0.75f, 0.81f, 1f), false, TextAnchor.MiddleLeft);
        Label detail = CreateTechniqueSettingsLabel(detailText, 24f, new Color(0.72f, 0.75f, 0.81f, 1f), false, TextAnchor.MiddleLeft);
        detail.style.marginTop = 6f;
        text.Add(title);
        text.Add(detail);
        content.Add(text);

        box.Add(content);
        return box;
    }

    private static VisualElement CreateTechniqueSettingsFlagEmptyIcon()
    {
        VisualElement icon = new VisualElement();
        icon.style.width = 62f;
        icon.style.height = 62f;
        icon.style.position = Position.Relative;
        icon.style.flexShrink = 0f;

        Color line = new Color(0.60f, 0.64f, 0.70f, 0.88f);
        VisualElement pole = new VisualElement();
        pole.style.position = Position.Absolute;
        pole.style.left = 19f;
        pole.style.top = 11f;
        pole.style.width = 3f;
        pole.style.height = 42f;
        pole.style.backgroundColor = line;
        SetRadius(pole, 2f);
        icon.Add(pole);

        VisualElement flagTop = new VisualElement();
        flagTop.style.position = Position.Absolute;
        flagTop.style.left = 22f;
        flagTop.style.top = 12f;
        flagTop.style.width = 26f;
        flagTop.style.height = 3f;
        flagTop.style.backgroundColor = line;
        SetRadius(flagTop, 2f);
        icon.Add(flagTop);

        VisualElement flagSide = new VisualElement();
        flagSide.style.position = Position.Absolute;
        flagSide.style.left = 47f;
        flagSide.style.top = 12f;
        flagSide.style.width = 3f;
        flagSide.style.height = 22f;
        flagSide.style.backgroundColor = line;
        SetRadius(flagSide, 2f);
        icon.Add(flagSide);

        VisualElement flagBottom = new VisualElement();
        flagBottom.style.position = Position.Absolute;
        flagBottom.style.left = 22f;
        flagBottom.style.top = 34f;
        flagBottom.style.width = 26f;
        flagBottom.style.height = 3f;
        flagBottom.style.backgroundColor = line;
        SetRadius(flagBottom, 2f);
        icon.Add(flagBottom);

        return icon;
    }

    private static VisualElement CreateTechniqueSettingsSegmentEmptyIcon()
    {
        VisualElement icon = new VisualElement();
        icon.style.width = 62f;
        icon.style.height = 62f;
        icon.style.justifyContent = Justify.Center;
        icon.style.alignItems = Align.Center;
        icon.style.flexShrink = 0f;

        Color line = new Color(0.60f, 0.64f, 0.70f, 0.88f);
        for (int i = 0; i < 3; i++)
        {
            VisualElement row = new VisualElement();
            row.style.width = i == 1 ? 40f : 32f;
            row.style.height = 4f;
            row.style.marginTop = 5f;
            row.style.marginBottom = 5f;
            row.style.backgroundColor = line;
            SetRadius(row, 2f);
            icon.Add(row);
        }

        return icon;
    }

    private Button CreateTechniqueSettingsToggleButton(string text, Action action)
    {
        Button button = CreateTechniqueSettingsButton(text, action);
        button.style.height = 72f;
        button.style.minWidth = 230f;
        button.style.marginLeft = 0f;
        button.style.marginRight = 12f;
        button.style.marginBottom = 12f;
        button.style.fontSize = UiFont(24f);
        button.style.paddingLeft = 22f;
        button.style.paddingRight = 22f;
        SetRadius(button, 12f);
        return button;
    }

    private void StyleTechniqueSettingsToggleButton(Button button, bool selected)
    {
        if (button == null)
            return;

        if (selected)
        {
            StyleTechniqueSettingsFlatButton(
                button,
                new Color(0.28f, 0.17f, 0.48f, 0.92f),
                Color.white,
                new Color(0.40f, 0.24f, 0.70f, 0.95f),
                new Color(0.40f, 0.24f, 0.70f, 0.95f),
                new Color(0.82f, 0.62f, 1f, 0.98f));
        }
        else
        {
            StyleTechniqueSettingsFlatButton(
                button,
                TechniqueSettingsButtonBackgroundColor,
                new Color(0.80f, 0.85f, 0.93f, 1f),
                TechniqueSettingsBorderColor,
                TechniqueSettingsButtonHoverColor,
                TechniqueSettingsBorderColor);
        }
        SetRadius(button, 11f);
    }

    private Button CreateTechniqueSettingsAddButton(string text, Action action)
    {
        Button button = CreateTechniqueSettingsButton(text, action);
        button.style.height = 68f;
        button.style.minWidth = TechniqueSettingsButtonMinWidthForText(text, 260f);
        button.style.fontSize = UiFont(23f);
        button.style.marginLeft = 0f;
        button.style.marginRight = 18f;
        button.style.marginBottom = 0f;
        button.style.paddingLeft = 22f;
        button.style.paddingRight = 22f;
        StyleTechniqueSettingsOutlineButton(button);
        return button;
    }

    private void StyleTechniqueSettingsOutlineButton(Button button)
    {
        if (button == null)
            return;

        StyleTechniqueSettingsFlatButton(
            button,
            TechniqueSettingsButtonBackgroundColor,
            Color.white,
            TechniqueSettingsBorderColor,
            TechniqueSettingsButtonHoverColor,
            TechniqueSettingsBorderColor);
        SetRadius(button, 7f);
    }

    private void StyleTechniqueSettingsIconButton(Button button, Color color, bool danger = false)
    {
        if (button == null)
            return;

        StyleTechniqueSettingsFlatButton(
            button,
            new Color(0.075f, 0.085f, 0.100f, 0.95f),
            color,
            danger ? new Color(0.86f, 0.20f, 0.14f, 0.92f) : TechniqueSettingsBorderColor,
            danger ? new Color(0.88f, 0.16f, 0.12f, 0.12f) : new Color(1f, 1f, 1f, 0.040f),
            danger ? new Color(0.95f, 0.23f, 0.18f, 0.95f) : new Color(0.62f, 0.66f, 0.72f, 0.72f));
        SetRadius(button, 7f);
    }

    private VisualElement CreateTechniqueSettingsChip(string text, Action remove)
    {
        VisualElement chip = new VisualElement();
        chip.style.flexDirection = FlexDirection.Row;
        chip.style.alignItems = Align.Center;
        chip.style.height = 56f;
        chip.style.marginRight = 12f;
        chip.style.marginBottom = 12f;
        chip.style.paddingLeft = 20f;
        chip.style.paddingRight = 8f;
        chip.style.backgroundColor = TechniqueSettingsSurfaceColor;
        SetRadius(chip, 7f);
        SetTechniqueSettingsBorder(chip, TechniqueSettingsBorderColor, 2f, 7f);

        Label label = CreateTechniqueSettingsLabel(text, 22f, new Color(0.91f, 0.90f, 0.94f, 1f), true, TextAnchor.MiddleLeft);
        label.style.whiteSpace = WhiteSpace.NoWrap;
        chip.Add(label);

        Label close = CreateTechniqueSettingsLabel("X", 18f, new Color(0.78f, 0.82f, 0.88f, 1f), true, TextAnchor.MiddleCenter);
        close.pickingMode = PickingMode.Position;
        close.style.width = 30f;
        close.style.minWidth = 30f;
        close.style.height = 34f;
        close.style.marginLeft = 6f;
        close.style.marginRight = 0f;
        close.style.marginTop = 0f;
        close.style.marginBottom = 0f;
        close.style.paddingLeft = 0f;
        close.style.paddingRight = 0f;
        close.style.backgroundColor = Color.clear;
        close.style.unityTextAlign = TextAnchor.MiddleCenter;
        close.RegisterCallback<PointerDownEvent>(evt =>
        {
            remove?.Invoke();
            evt.StopPropagation();
        });
        close.RegisterCallback<MouseEnterEvent>(_ => close.style.color = Color.white);
        close.RegisterCallback<MouseLeaveEvent>(_ => close.style.color = new Color(0.78f, 0.82f, 0.88f, 1f));
        chip.Add(close);
        return chip;
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
        row.style.marginBottom = 22f;
        row.style.paddingLeft = 24f;
        row.style.paddingRight = 24f;
        row.style.paddingTop = 22f;
        row.style.paddingBottom = 22f;
        row.style.backgroundColor = TechniqueSettingsSurfaceColor;
        row.style.translate = new Translate(0f, 0f, 0f);
        row.style.scale = new Scale(Vector3.one);
        row.style.transitionProperty = new List<StylePropertyName>
        {
            new StylePropertyName("background-color"),
            new StylePropertyName("scale"),
            new StylePropertyName("translate")
        };
        row.style.transitionDuration = new List<TimeValue>
        {
            new TimeValue(100f, TimeUnit.Millisecond),
            new TimeValue(100f, TimeUnit.Millisecond),
            new TimeValue(100f, TimeUnit.Millisecond)
        };
        row.style.transitionTimingFunction = new List<EasingFunction>
        {
            new EasingFunction(EasingMode.EaseOutCubic),
            new EasingFunction(EasingMode.EaseOutCubic),
            new EasingFunction(EasingMode.EaseOutCubic)
        };
        SetRadius(row, 8f);
        SetTechniqueSettingsBorder(row, index == 0 ? TechniqueSettingsAccentColor : TechniqueSettingsBorderColor, 2f, 8f);

        VisualElement topRow = new VisualElement();
        topRow.style.flexDirection = FlexDirection.Row;
        topRow.style.alignItems = Align.Center;
        topRow.style.minHeight = 66f;
        row.Add(topRow);

        VisualElement grip = new VisualElement();
        grip.style.width = 62f;
        grip.style.height = 62f;
        grip.style.marginRight = 24f;
        grip.style.justifyContent = Justify.Center;
        grip.style.alignItems = Align.Center;
        grip.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        grip.pickingMode = PickingMode.Position;
        for (int dotRow = 0; dotRow < 3; dotRow++)
        {
            VisualElement dotLine = new VisualElement();
            dotLine.style.flexDirection = FlexDirection.Row;
            dotLine.style.justifyContent = Justify.Center;
            dotLine.style.marginTop = 3f;
            dotLine.style.marginBottom = 3f;
            for (int dotColumn = 0; dotColumn < 2; dotColumn++)
            {
                VisualElement dot = new VisualElement();
                dot.style.width = 5f;
                dot.style.height = 5f;
                dot.style.marginLeft = 3f;
                dot.style.marginRight = 3f;
                dot.style.backgroundColor = new Color(0.72f, 0.75f, 0.80f, 0.82f);
                SetRadius(dot, 3f);
                dotLine.Add(dot);
            }
            grip.Add(dotLine);
        }
        topRow.Add(grip);

        DropdownField type = new DropdownField();
        type.label = string.Empty;
        type.choices = TechniqueSegmentChoiceLabels().ToList();
        type.value = SegmentTypeToChoiceLabel(state.segment.type);
        type.style.width = 340f;
        type.style.marginRight = 22f;
        StyleTechniqueSettingsDropdown(type);
        state.typeDropdown = type;
        topRow.Add(type);

        VisualElement spacer = new VisualElement();
        spacer.style.flexGrow = 1f;
        topRow.Add(spacer);

        Button up = CreateTechniqueSettingsButton("^", () =>
        {
            captureRows?.Invoke();
            int current = rowStates.IndexOf(state);
            if (current <= 0)
                return;

            rowStates.RemoveAt(current);
            rowStates.Insert(current - 1, state);
            ReflowTechniqueSettingsRowsByOrder(rowStates);
            rebuildRows?.Invoke();
        });
        up.style.width = 64f;
        up.style.height = 60f;
        up.style.minWidth = 64f;
        up.style.fontSize = UiFont(28f);
        up.style.marginLeft = 12f;
        up.style.marginRight = 8f;
        StyleTechniqueSettingsIconButton(up, new Color(0.88f, 0.90f, 0.94f, 1f));
        topRow.Add(up);

        Button down = CreateTechniqueSettingsButton("v", () =>
        {
            captureRows?.Invoke();
            int current = rowStates.IndexOf(state);
            if (current < 0 || current >= rowStates.Count - 1)
                return;

            rowStates.RemoveAt(current);
            rowStates.Insert(current + 1, state);
            ReflowTechniqueSettingsRowsByOrder(rowStates);
            rebuildRows?.Invoke();
        });
        down.style.width = 64f;
        down.style.height = 60f;
        down.style.minWidth = 64f;
        down.style.fontSize = UiFont(27f);
        down.style.marginLeft = 0f;
        down.style.marginRight = 8f;
        StyleTechniqueSettingsIconButton(down, new Color(0.88f, 0.90f, 0.94f, 1f));
        topRow.Add(down);

        Button remove = CreateTechniqueSettingsButton("X", () =>
        {
            captureRows?.Invoke();
            rowStates.Remove(state);
            rebuildRows?.Invoke();
        });
        remove.style.width = 64f;
        remove.style.height = 60f;
        remove.style.minWidth = 64f;
        remove.style.fontSize = UiFont(24f);
        remove.style.marginLeft = 0f;
        remove.style.marginRight = 0f;
        StyleTechniqueSettingsIconButton(remove, new Color(1f, 0.27f, 0.20f, 1f), danger: true);
        topRow.Add(remove);

        VisualElement fieldsRow = new VisualElement();
        fieldsRow.style.flexDirection = FlexDirection.Row;
        fieldsRow.style.flexWrap = Wrap.Wrap;
        fieldsRow.style.marginTop = 22f;
        fieldsRow.style.paddingLeft = 86f;
        row.Add(fieldsRow);

        state.startField = CreateTechniqueSettingsTextField("Start", state.segment.startOffset.ToString("0.000", CultureInfo.InvariantCulture), 210f);
        state.endField = CreateTechniqueSettingsTextField("End", state.segment.endOffset.ToString("0.000", CultureInfo.InvariantCulture), 210f);
        fieldsRow.Add(state.startField);
        fieldsRow.Add(state.endField);

        state.startFretField = null;
        state.endFretField = null;
        if (ShouldShowTechniqueSettingsFretFields(state.segment))
        {
            state.startFretField = CreateTechniqueSettingsTextField("From Fret", Mathf.Clamp(state.segment.startFret, 0, 24).ToString(CultureInfo.InvariantCulture), 210f);
            state.endFretField = CreateTechniqueSettingsTextField("To Fret", Mathf.Clamp(state.segment.endFret, 0, 24).ToString(CultureInfo.InvariantCulture), 210f);
            fieldsRow.Add(state.startFretField);
            fieldsRow.Add(state.endFretField);
        }

        state.startBendField = null;
        state.endBendField = null;
        if (ShouldShowTechniqueSettingsBendFields(state.segment))
        {
            state.startBendField = CreateTechniqueSettingsTextField("Bend Start", state.segment.startBend.ToString("0.###", CultureInfo.InvariantCulture), 210f);
            state.endBendField = CreateTechniqueSettingsTextField("Bend End", state.segment.endBend.ToString("0.###", CultureInfo.InvariantCulture), 210f);
            fieldsRow.Add(state.startBendField);
            fieldsRow.Add(state.endBendField);
            if (CanCarryBendValues(state.segment))
            {
                Button clearBendValues = CreateTechniqueSettingsAddButton("Clear Bend Values", () =>
                {
                    captureRows?.Invoke();
                    state.segment.startBend = 0f;
                    state.segment.endBend = 0f;
                    rebuildRows?.Invoke();
                });
                clearBendValues.style.minWidth = 320f;
                clearBendValues.style.width = 320f;
                fieldsRow.Add(clearBendValues);
            }
        }
        else if (CanCarryBendValues(state.segment))
        {
            Button addBendValues = CreateTechniqueSettingsAddButton("Add Bend Values", () =>
            {
                captureRows?.Invoke();
                float bend = Mathf.Abs(note?.bendStep ?? 0f) > 0.01f
                    ? Mathf.Abs(note.bendStep)
                    : FullStepBendSemitones;
                state.segment.startBend = Mathf.Max(0.5f, bend);
                state.segment.endBend = Mathf.Max(0.5f, bend);
                rebuildRows?.Invoke();
            });
            addBendValues.style.minWidth = 300f;
            addBendValues.style.width = 300f;
            fieldsRow.Add(addBendValues);
        }

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
            row.style.scale = new Scale(new Vector3(1.012f, 1.012f, 1f));
            grip.CapturePointer(pointerId);
            evt.StopImmediatePropagation();
        });
        grip.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (evt.pointerId != pointerId || !grip.HasPointerCapture(pointerId))
                return;

            Vector2 pointer = PointerPosition(evt);
            row.style.translate = new Translate(0f, Mathf.Clamp((pointer.y - startPointer.y) * 0.08f, -8f, 8f), 0f);

            int currentIndex = rowStates.IndexOf(state);
            if (currentIndex < 0)
            {
                evt.StopImmediatePropagation();
                return;
            }

            int targetIndex = ResolveTechniqueSettingsDragTargetIndex(list, row, pointer);
            if (targetIndex != currentIndex)
            {
                rowStates.RemoveAt(currentIndex);
                targetIndex = Mathf.Clamp(targetIndex, 0, rowStates.Count);
                rowStates.Insert(targetIndex, state);
                MoveTechniqueSettingsRowElement(list, row, targetIndex);
                RefreshTechniqueSettingsRowOutlines(list);
            }

            evt.StopImmediatePropagation();
        });
        grip.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (evt.pointerId != pointerId)
                return;

            if (grip.HasPointerCapture(pointerId))
                grip.ReleasePointer(pointerId);

            row.style.opacity = 1f;
            row.style.scale = new Scale(Vector3.one);
            row.style.translate = new Translate(0f, 0f, 0f);
            ReflowTechniqueSettingsRowsByOrder(rowStates);
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
            rebuildRows?.Invoke();
        });

        return row;
    }

    private static int ResolveTechniqueSettingsDragTargetIndex(VisualElement list, VisualElement draggedRow, Vector2 worldPosition)
    {
        if (list == null)
            return 0;

        Vector2 local = list.WorldToLocal(worldPosition);
        List<VisualElement> siblings = list.Children()
            .Where(child => child != null && !ReferenceEquals(child, draggedRow))
            .ToList();
        for (int i = 0; i < siblings.Count; i++)
        {
            VisualElement sibling = siblings[i];
            float midpoint = sibling.layout.y + sibling.layout.height * 0.5f;
            if (local.y < midpoint)
                return i;
        }

        return siblings.Count;
    }

    private static void MoveTechniqueSettingsRowElement(VisualElement list, VisualElement row, int targetIndex)
    {
        if (list == null || row == null)
            return;

        List<VisualElement> siblings = list.Children()
            .Where(child => child != null && !ReferenceEquals(child, row))
            .ToList();
        if (siblings.Count == 0)
            return;

        if (targetIndex <= 0)
        {
            row.PlaceBehind(siblings[0]);
            return;
        }

        if (targetIndex >= siblings.Count)
        {
            row.PlaceInFront(siblings[siblings.Count - 1]);
            return;
        }

        row.PlaceBehind(siblings[targetIndex]);
    }

    private static void RefreshTechniqueSettingsRowOutlines(VisualElement list)
    {
        if (list == null)
            return;

        int index = 0;
        foreach (VisualElement child in list.Children())
        {
            if (child == null)
                continue;

            SetTechniqueSettingsBorder(child, index == 0 ? TechniqueSettingsAccentColor : TechniqueSettingsBorderColor, 2f, 8f);
            index++;
        }
    }

    private TextField CreateTechniqueSettingsTextField(string label, string value, float width)
    {
        TextField field = CreatePopupTextField(label, value);
        field.style.width = width;
        field.style.height = 102f;
        field.style.marginRight = 30f;
        field.style.marginBottom = 16f;
        field.style.fontSize = UiFont(23f);
        field.style.unityFontDefinition = bodyFont;
        field.style.flexDirection = FlexDirection.Column;
        field.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        SetBorderWidth(field, 0f);
        SetRadius(field, 0f);
        ApplyTechniqueSettingsTypography(field);
        field.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            Label fieldLabel = field.Q<Label>();
            if (fieldLabel != null)
            {
                fieldLabel.style.width = Length.Percent(100f);
                fieldLabel.style.minWidth = 0f;
                fieldLabel.style.marginBottom = 8f;
                fieldLabel.style.fontSize = UiFont(21f);
                fieldLabel.style.unityFontDefinition = bodyFont;
                fieldLabel.style.color = new Color(0.70f, 0.72f, 0.78f, 1f);
                fieldLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                fieldLabel.style.whiteSpace = WhiteSpace.NoWrap;
                fieldLabel.style.overflow = Overflow.Hidden;
                fieldLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                ApplyTechniqueSettingsTypography(fieldLabel);
            }

            VisualElement input = field.Q<VisualElement>(TextField.textInputUssName) ?? field.Q<VisualElement>("unity-text-input");
            if (input != null)
            {
                input.style.width = Length.Percent(100f);
                input.style.minHeight = 58f;
                input.style.fontSize = UiFont(23f);
                input.style.unityFontDefinition = bodyFont;
                input.style.color = Color.white;
                input.style.backgroundColor = TechniqueSettingsInputColor;
                input.style.paddingLeft = 16f;
                input.style.paddingRight = 16f;
                SetRadius(input, 7f);
                SetTechniqueSettingsBorder(input, TechniqueSettingsBorderColor, 2f, 7f);
                ApplyTechniqueSettingsTypography(input);
                field.RegisterCallback<FocusInEvent>(_ => SetTechniqueSettingsBorder(input, TechniqueSettingsAccentColor, 2f, 7f));
                field.RegisterCallback<FocusOutEvent>(_ => SetTechniqueSettingsBorder(input, TechniqueSettingsBorderColor, 2f, 7f));
            }
        });
        return field;
    }

    private void StyleTechniqueSettingsDropdown(DropdownField dropdown)
    {
        if (dropdown == null)
            return;

        dropdown.style.height = 62f;
        dropdown.style.fontSize = UiFont(25f);
        dropdown.style.unityFontDefinition = bodyFont;
        dropdown.style.unityFontStyleAndWeight = FontStyle.Bold;
        dropdown.style.color = Color.white;
        dropdown.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        dropdown.style.paddingLeft = 0f;
        dropdown.style.paddingRight = 0f;
        SetRadius(dropdown, 0f);
        SetBorderWidth(dropdown, 0f);
        ApplyTechniqueSettingsTypography(dropdown);
        dropdown.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            Label label = dropdown.Q<Label>();
            if (label != null)
            {
                label.style.display = DisplayStyle.None;
                label.style.unityFontDefinition = bodyFont;
            }

            VisualElement input = dropdown.Q(className: "unity-base-field__input");
            if (input != null)
            {
                input.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
                input.style.borderTopWidth = 0f;
                input.style.borderRightWidth = 0f;
                input.style.borderBottomWidth = 0f;
                input.style.borderLeftWidth = 0f;
                input.style.color = Color.white;
                input.style.fontSize = UiFont(25f);
                input.style.unityFontDefinition = bodyFont;
                input.style.paddingLeft = 0f;
                ApplyTechniqueSettingsTypography(input);
            }

            Label text = dropdown.Q<Label>(className: "unity-base-popup-field__text");
            if (text != null)
            {
                text.style.color = new Color(0.92f, 0.94f, 0.98f, 1f);
                text.style.fontSize = UiFont(25f);
                text.style.unityFontDefinition = bodyFont;
                text.style.unityFontStyleAndWeight = FontStyle.Bold;
                ApplyTechniqueSettingsTypography(text);
            }

            VisualElement arrow = dropdown.Q(className: "unity-base-popup-field__arrow");
            if (arrow != null)
                arrow.style.unityBackgroundImageTintColor = new Color(0.82f, 0.84f, 0.88f, 1f);
        });
    }

    private static bool ShouldShowTechniqueSettingsFretFields(ChartEditorTechniqueSegment segment)
    {
        return segment != null && segment.type == NoteTechniqueSegmentType.Slide;
    }

    private static bool ShouldShowTechniqueSettingsBendFields(ChartEditorTechniqueSegment segment)
    {
        return segment != null &&
               (segment.type == NoteTechniqueSegmentType.Bend ||
                Mathf.Abs(segment.startBend) > 0.01f ||
                Mathf.Abs(segment.endBend) > 0.01f);
    }

    private static bool CanCarryBendValues(ChartEditorTechniqueSegment segment)
    {
        return segment != null &&
               (segment.type == NoteTechniqueSegmentType.Sustain ||
                segment.type == NoteTechniqueSegmentType.Vibrato);
    }

    private void AddTechniqueSettingsSegment(List<TechniqueSettingsRowState> rowStates, ChartEditorNote note, NoteTechniqueSegmentType type)
    {
        if (rowStates == null || note == null)
            return;

        float start = rowStates.Count == 0 ? 0f : rowStates.Max(state => Mathf.Max(state.segment.startOffset, state.segment.endOffset));
        float projectLimit = Mathf.Max(TechniqueSegmentMinimumSeconds, (float)((project != null ? GetProjectDurationSeconds() : note.timeSeconds + start + 1.0) - note.timeSeconds));
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

        double noteLimit = Math.Max(0.06, (project != null ? GetProjectDurationSeconds() : note.timeSeconds + GetNoteEffectiveDurationSeconds(note)) - note.timeSeconds);
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

        int noteFret = Mathf.Clamp(note?.fret ?? 0, 0, 24);
        int startFret = noteFret;
        int endFret = noteFret;
        if (type == NoteTechniqueSegmentType.Slide)
        {
            if (!TryParseIntInRange(state.startFretField?.value, 0, 24, out startFret) ||
                !TryParseIntInRange(state.endFretField?.value, 0, 24, out endFret))
            {
                if (showError)
                    SetStatus("Technique frets must be whole numbers from 0 to 24.");
                return false;
            }
        }

        float startBend = 0f;
        float endBend = 0f;
        if (state.startBendField != null || state.endBendField != null)
        {
            if (!TryParseFloatInRange(state.startBendField?.value, 0f, 4f, out startBend) ||
                !TryParseFloatInRange(state.endBendField?.value, 0f, 4f, out endBend))
            {
                if (showError)
                    SetStatus("Technique bend values must be between 0 and 4 semitones.");
                return false;
            }
        }
        else if (type == NoteTechniqueSegmentType.Bend)
        {
            startBend = Mathf.Max(0f, state.segment.startBend);
            endBend = Mathf.Max(0.5f, state.segment.endBend);
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
        if (note == null)
            return;

        note.bendPoints?.Clear();
        note.bendVisualStartTime = -1f;
        note.bendVisualDuration = 0f;
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
            if (!TryParseDoubleInRange(audioField.value, 0.0, GetProjectDurationSeconds(), out double audioTime))
            {
                SetStatus($"Audio time must be between 0 and {GetProjectDurationSeconds():0.000} seconds.");
                return false;
            }

            if (!TryParseDoubleInRange(beatField.value, 0.0, Math.Max(GetProjectDurationSeconds() * 8.0, 16.0), out double beatPosition))
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

        Button synchTheoryButton = CreatePopupDialogButton("SynchTheory", ShowSynchTheoryPopup, new Color(0.96f, 0.54f, 0.18f, 1f));
        VisualElement actions = CreatePopupActionGrid(
            CreateCompactButton("Tap Tempo", () => RegisterTapTempo(defaultBpmField, regionBpmField)),
            CreateCompactButton("Add Anchor", () =>
            {
                AddSyncPointAtCursor();
                HideEditPopup();
            }),
            CreateCompactButton("Quantize Notes", QuantizeSelectedNotesToBeatGrid),
            synchTheoryButton,
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

    // Swaps the project's audio file. Chart data and the beat map are
    // authored against song time, so they are kept untouched; the clip reload
    // refreshes audio duration and every duration-derived cache follows.
    private bool ReplaceProjectAudioFile(string newAudioPath, out string error)
    {
        error = string.Empty;
        if (project == null)
        {
            error = "No project is loaded.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(newAudioPath) || !File.Exists(newAudioPath))
        {
            error = "Audio file was not found.";
            return false;
        }

        project.audio = new ChartEditorAudioInfo
        {
            sourcePath = newAudioPath,
            displayName = Path.GetFileName(newAudioPath),
            extension = Path.GetExtension(newAudioPath)?.ToLowerInvariant() ?? string.Empty,
            durationSeconds = 0.0
        };
        project.dirty = true;

        ChartEditorTimingService.InvalidateBeatMapCache(project);
        ResetEditorAudioCache();
        return true;
    }

    // Re-imports tracks/notes/tones from a different chart file while keeping
    // the user's timing work. The imported notes carry beat positions from the
    // FILE's own tempo grid; mapping those beats through the project's synced
    // beat map places them at the user's synced audio times.
    private bool ReplaceProjectChartFile(string newChartPath, List<int> selectedPartIndices, out string error)
    {
        error = string.Empty;
        if (project == null)
        {
            error = "No project is loaded.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(newChartPath) || !File.Exists(newChartPath))
        {
            error = "Chart file was not found.";
            return false;
        }

        ChartEditorNewProjectRequest request = new ChartEditorNewProjectRequest
        {
            chartPath = newChartPath,
            audioPath = project.audio?.sourcePath,
            coverImagePath = project.metadata?.coverImagePath,
            title = project.metadata?.title,
            artist = project.metadata?.artist,
            album = project.metadata?.album,
            genre = project.metadata?.genre,
            year = project.metadata?.year,
            selectedPartIndices = selectedPartIndices ?? new List<int>()
        };

        if (!ChartEditorImportService.CreateNewProject(request, out ChartEditorImportResult result, out error) ||
            result?.project == null)
        {
            if (string.IsNullOrWhiteSpace(error))
                error = "The chart file could not be imported.";
            return false;
        }

        ChartEditorProject imported = result.project;

        // Musical content comes from the new file; the user's timing work
        // (beat markers/anchors), sections, audio, metadata, and identity
        // (project id / save path) stay.
        project.tracks = imported.tracks ?? new List<ChartEditorTrack>();
        project.selectedTrackId = imported.selectedTrackId;
        project.sourcePath = newChartPath;
        project.sourceKind = imported.sourceKind;
        project.sourceFolder = Path.GetDirectoryName(newChartPath) ?? string.Empty;
        if (imported.beatMap?.timeSignatures != null && imported.beatMap.timeSignatures.Count > 0)
            project.beatMap.timeSignatures = imported.beatMap.timeSignatures;

        ClearNoteSelection();
        selectedToneChange = null;
        selectedSectionId = null;

        ChartEditorTimingService.InvalidateBeatMapCache(project);
        ChartEditorTimingService.ApplyBeatMapToContent(project);
        project.dirty = true;
        return true;
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
            if (!TryParseDoubleInRange(startField.value, 0.0, GetProjectDurationSeconds(), out double start))
            {
                SetStatus($"Section start must be between 0 and {GetProjectDurationSeconds():0.000} seconds.");
                return false;
            }

            if (!TryParseDoubleInRange(endField.value, start + 0.05, Math.Max(GetProjectDurationSeconds() + 60.0, start + 0.05), out double end))
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
            note.timeSeconds = Math.Max(0.0, Math.Min(GetProjectDurationSeconds(), note.timeSeconds + timeDelta));
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
        editPopupKind = ChartEditorPopupKind.Generic;
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

        if (editPopupKind == ChartEditorPopupKind.SaveOptions)
            closeAfterSuccessfulSave = false;

        editPopupElement.RemoveFromHierarchy();
        editPopupElement = null;
        editPopupKind = ChartEditorPopupKind.None;
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
        field.style.backgroundColor = new Color(0.024f, 0.023f, 0.028f, 0.98f);
        SetRadius(field, 11f);
        SetBorderWidth(field, 1f);
        SetBorderColor(field, new Color(0.28f, 0.27f, 0.32f, 0.95f));

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
        SetBorderColor(preview, new Color(0.30f, 0.29f, 0.34f, 0.95f));
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
        field.style.backgroundColor = new Color(0.024f, 0.023f, 0.028f, 0.98f);
        field.style.borderTopWidth = 1f;
        field.style.borderRightWidth = 1f;
        field.style.borderBottomWidth = 1f;
        field.style.borderLeftWidth = 1f;
        SetToneLabBorder(field,
            new Color(0.42f, 0.41f, 0.45f, 0.84f),
            new Color(0.25f, 0.24f, 0.28f, 0.95f),
            new Color(0.16f, 0.155f, 0.185f, 1f));
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
        StylePanel(panel, new Color(0.058f, 0.056f, 0.066f, 0.99f), new Color(0.22f, 0.21f, 0.26f, 1f), 0f);

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
            BuildNoteInspector(panel, project.SelectedTrack, note);
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
                if (toneEditorEnabled)
                    return;

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
                if (toneEditorEnabled)
                    return;

                if (evt.button == 0 && evt.clickCount >= 2)
                {
                    ShowSectionEditPopup(section);
                    evt.StopPropagation();
                }
            });
            block.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (toneEditorEnabled)
                    return;

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
        GetTimelineBuildWindowSeconds(out double windowStartSeconds, out double windowEndSeconds);

        for (int i = 0; i < markers.Count; i++)
        {
            ChartEditorBeatMarker marker = markers[i];
            if (marker == null)
                continue;

            if (marker.audioTimeSeconds < windowStartSeconds || marker.audioTimeSeconds > windowEndSeconds)
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

    private void BuildToneMarkers(VisualElement timeline)
    {
        currentToneMarkerVisuals.Clear();
        toneMarkerLaneElements.Clear();
        toneMarkerTimelineElement = timeline;
        if (!toneEditorEnabled || timeline == null)
            return;

        ChartEditorTrack track = project?.SelectedTrack;
        List<ChartEditorToneChange> changes = GetSelectedTrackToneChanges();
        if (track == null || changes.Count == 0)
            return;

        float timelineHeight = GetTimelineContentHeight();
        VisualElement lane = new VisualElement();
        lane.style.position = Position.Absolute;
        lane.style.left = TimelineLabelWidth;
        lane.style.right = 0f;
        lane.style.top = ToneMarkerLaneTop;
        lane.style.height = ToneMarkerLaneHeight;
        lane.style.backgroundColor = Color.clear;
        lane.pickingMode = PickingMode.Ignore;
        timeline.Add(lane);
        toneMarkerLaneElements.Add(lane);

        Label laneLabel = CreateLabel("TONES", 20f, new Color(0.00f, 0.90f, 0.82f, 0.94f), true, TextAnchor.MiddleCenter, false);
        laneLabel.style.position = Position.Absolute;
        laneLabel.style.left = 0f;
        laneLabel.style.top = ToneMarkerLaneTop;
        laneLabel.style.width = TimelineLabelWidth;
        laneLabel.style.height = ToneMarkerLaneHeight;
        laneLabel.style.backgroundColor = new Color(0.030f, 0.055f, 0.056f, 0.88f);
        laneLabel.pickingMode = PickingMode.Ignore;
        timeline.Add(laneLabel);
        toneMarkerLaneElements.Add(laneLabel);

        VisualElement visualLayer = new VisualElement();
        visualLayer.style.position = Position.Absolute;
        visualLayer.style.left = TimelineLabelWidth;
        visualLayer.style.right = 0f;
        visualLayer.style.top = 0f;
        visualLayer.style.bottom = 0f;
        visualLayer.pickingMode = PickingMode.Ignore;
        timeline.Add(visualLayer);
        toneMarkerLaneElements.Add(visualLayer);

        for (int i = 0; i < changes.Count; i++)
        {
            ChartEditorToneChange change = changes[i];
            if (change == null)
                continue;

            bool selected = ReferenceEquals(selectedToneChange, change);
            Color markerColor = ToneMarkerColor(i, selected);
            float left = TimeToPixels(change.timeSeconds);

            ToneMarkerVisual visual = new ToneMarkerVisual { change = change };
            VisualElement line = new VisualElement();
            line.style.position = Position.Absolute;
            line.style.left = left;
            line.style.top = WaveformTop;
            line.style.width = selected ? 6f : 4f;
            line.style.height = Mathf.Max(1f, timelineHeight - WaveformTop - 20f);
            line.style.marginLeft = selected ? -3f : -2f;
            line.style.backgroundColor = new Color(markerColor.r, markerColor.g, markerColor.b, selected ? 0.98f : 0.86f);
            line.pickingMode = PickingMode.Ignore;
            visualLayer.Add(line);
            visual.line = line;

            VisualElement cap = new VisualElement();
            cap.style.position = Position.Absolute;
            cap.style.left = TimelineLabelWidth + Mathf.Max(0f, left - ToneMarkerCapWidth * 0.5f);
            cap.style.top = ToneMarkerLaneTop + 9f;
            cap.style.width = ToneMarkerCapWidth;
            cap.style.height = ToneMarkerCapHeight;
            cap.style.alignItems = Align.Center;
            cap.style.justifyContent = Justify.Center;
            cap.style.backgroundColor = new Color(markerColor.r, markerColor.g, markerColor.b, selected ? 0.94f : 0.76f);
            SetRadius(cap, 10f);
            SetBorderWidth(cap, selected ? 2f : 1f);
            SetBorderColor(cap, selected ? Color.white : new Color(0.94f, 1f, 0.98f, 0.62f));
            cap.pickingMode = PickingMode.Position;
            SetElementCursor(cap, ChartEditorCursorKind.ResizeHorizontal);

            Label label = CreateLabel(ResolveToneChangeName(track, change), 16f, Color.white, true, TextAnchor.MiddleCenter, false);
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.maxWidth = ToneMarkerCapWidth - 16f;
            label.pickingMode = PickingMode.Ignore;
            cap.Add(label);
            AddToneMarkerInteractionHandlers(cap, change);
            visual.cap = cap;
            visual.label = label;

            VisualElement hit = new VisualElement();
            hit.style.position = Position.Absolute;
            hit.style.left = TimelineLabelWidth + left - ToneMarkerHitWidth * 0.5f;
            hit.style.top = WaveformTop;
            hit.style.width = ToneMarkerHitWidth;
            hit.style.height = Mathf.Max(1f, timelineHeight - WaveformTop);
            hit.style.backgroundColor = Color.clear;
            hit.pickingMode = PickingMode.Position;
            SetElementCursor(hit, ChartEditorCursorKind.ResizeHorizontal);
            AddToneMarkerInteractionHandlers(hit, change);
            timeline.Add(hit);
            visual.hit = hit;
            timeline.Add(cap);
            cap.BringToFront();

            currentToneMarkerVisuals.Add(visual);
        }
    }

    private void UpdateToneMarkerVisuals()
    {
        if (!toneEditorEnabled || currentToneMarkerVisuals.Count == 0)
            return;

        ChartEditorTrack track = project?.SelectedTrack;
        for (int i = 0; i < currentToneMarkerVisuals.Count; i++)
        {
            ToneMarkerVisual visual = currentToneMarkerVisuals[i];
            ChartEditorToneChange change = visual?.change;
            if (visual == null || change == null)
                continue;

            float left = TimeToPixels(change.timeSeconds);
            if (visual.hit != null)
                visual.hit.style.left = TimelineLabelWidth + left - ToneMarkerHitWidth * 0.5f;
            if (visual.line != null)
                visual.line.style.left = left;
            if (visual.cap != null)
                visual.cap.style.left = TimelineLabelWidth + Mathf.Max(0f, left - ToneMarkerCapWidth * 0.5f);
            if (visual.label != null)
                visual.label.text = ResolveToneChangeName(track, change);
        }
    }

    private void UpdateBeatGridVisuals()
    {
        if (project?.beatMap?.beatMarkers == null || currentBeatMarkerVisuals.Count == 0)
            return;

        using ProfilerMarker.AutoScope scope = BeatGridVisualsMarker.Auto();

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

        using ProfilerMarker.AutoScope scope = NoteTimingVisualsMarker.Auto();
        MarkHighwayPreviewDirty(renderImmediately: false);
        InvalidateAuditionCache();

        // Walk only the on-screen note blocks (captured at build time) instead
        // of probing the block dictionary with every note of every visible
        // track — with large projects the latter is O(all notes) per frame.
        for (int i = 0; i < currentNoteBlockTimings.Count; i++)
        {
            NoteBlockTimingRef timing = currentNoteBlockTimings[i];
            if (timing?.block == null || timing.track == null || timing.note == null)
                continue;

            float noteLeft = TimeToPixels(timing.note.timeSeconds);
            float noteWidth = GetNoteDrawWidth(timing.track, timing.note, timing.laneCount, timing.selectedTrack, noteLeft);
            timing.block.style.left = noteLeft;
            timing.block.style.width = noteWidth;
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

    // The set of tracks whose notes are actually drawn as movable blocks in
    // the timeline (the selected group's active difficulty). Live drag
    // previews only remap these; everything else updates on commit.
    private HashSet<string> BuildDragPreviewTrackIds()
    {
        ChartEditorTrack active = GetSelectedTrackViewGroup(BuildTrackViewGroups())?.activeTrack;
        if (string.IsNullOrWhiteSpace(active?.id))
            return null; // null falls back to the visible-tracks preview, never "skip everything"

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { active.id };
    }

    private void AddBeatMarkerInteractionHandlers(VisualElement hit, ChartEditorBeatMarker marker)
    {
        if (hit == null || marker == null)
            return;

        bool dragging = false;
        bool moved = false;
        int pointerId = -1;
        HashSet<string> dragPreviewTrackIds = null;
        Vector2 startPointer = Vector2.zero;
        float startTimelineScrollX = 0f;
        double startAudio = 0.0;
        double minAudio = 0.0;
        double maxAudio = 0.0;
        ChartEditorBeatMarker liveAnchor = null;
        bool moveContentWithBeatMap = true;
        bool tempoProbeDrag = false;
        double tempoProbeBeatPosition = 0.0;
        double lastTempoProbeAudio = 0.0;
        int lastPreviewFrame = -1;
        SetElementCursor(hit, ChartEditorCursorKind.ResizeHorizontal);

        hit.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (toneEditorEnabled)
                return;

            // Extra buttons pressed while a drag is captured would rebuild the
            // timeline out from under the capturing element and abandon the
            // drag commit — ignore them until the drag resolves.
            if (dragging)
            {
                evt.StopPropagation();
                return;
            }

            if (evt.button == 2)
            {
                ToggleBeatMarkerAnchor(marker);
                evt.StopPropagation();
                return;
            }

            if (evt.button == 1)
            {
                // No seek here: moving the cursor to the right-clicked beat
                // made "Move Anchor to Cursor" a no-op.
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
            startTimelineScrollX = timelineScrollOffset.x;
            startAudio = marker.audioTimeSeconds;
            moveContentWithBeatMap = !evt.shiftKey;
            tempoProbeBeatPosition = marker.beatPosition;
            tempoProbeDrag = evt.ctrlKey && ChartEditorTimingService.CanUseTrailingTempoProbe(project, marker.beatPosition, marker.isAnchor);
            lastTempoProbeAudio = startAudio;
            liveAnchor = marker.isAnchor ? marker : null;
            if (liveAnchor != null)
                ResolveAnchorDragBounds(liveAnchor, out minAudio, out maxAudio);
            dragPreviewTrackIds = BuildDragPreviewTrackIds();
            suppressHighwayPreviewRebuild = true;
            hit.CapturePointer(pointerId);
            evt.StopPropagation();
        });

        hit.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            Vector2 pointer = PointerPosition(evt);
            float horizontalDragPixels = GetTimelineDragPixelsWithAutoPan(pointer, startPointer, startTimelineScrollX);
            if (!moved && Mathf.Abs(horizontalDragPixels) <= 2f)
                return;

            moved = true;
            if (tempoProbeDrag)
            {
                lastTempoProbeAudio = Math.Max(0.0, Math.Min(GetProjectDurationSeconds(), startAudio + PixelDeltaToSeconds(horizontalDragPixels)));

                // Pointer-move events can fire several times per frame and the
                // live preview remaps every beat-timed note in the project.
                // Run the heavy preview at most once per rendered frame; the
                // latest pointer position is kept above so nothing is lost.
                if (lastPreviewFrame != Time.frameCount &&
                    ChartEditorTimingService.MoveTrailingBeatAsTempoProbe(project, tempoProbeBeatPosition, lastTempoProbeAudio, moveContentWithBeatMap, visibleTracksOnly: true, previewTrackIds: dragPreviewTrackIds))
                {
                    lastPreviewFrame = Time.frameCount;
                    UpdateBeatGridVisuals();
                    UpdateNoteTimingVisuals();
                    UpdatePlaybackVisuals();
                }

                evt.StopPropagation();
                return;
            }

            if (liveAnchor == null)
            {
                liveAnchor = ChartEditorTimingService.AddAnchorAtBeat(project, marker.beatPosition, startAudio, moveContentWithBeatMap, dragPreviewTrackIds);
                if (liveAnchor == null)
                    return;

                SelectSingleAnchor(liveAnchor);
                ResolveAnchorDragBounds(liveAnchor, out minAudio, out maxAudio);
            }

            if (liveAnchor.locked)
                return;

            double newAudio = Math.Max(minAudio, Math.Min(maxAudio, startAudio + PixelDeltaToSeconds(horizontalDragPixels)));
            liveAnchor.audioTimeSeconds = newAudio;
            project.dirty = true;
            if (lastPreviewFrame != Time.frameCount)
            {
                lastPreviewFrame = Time.frameCount;
                ChartEditorTimingService.PreviewBeatMapChange(project, moveContentWithBeatMap, visibleTracksOnly: true, previewTrackIds: dragPreviewTrackIds);
                UpdateBeatGridVisuals();
                UpdateNoteTimingVisuals();
                UpdatePlaybackVisuals();
            }

            evt.StopPropagation();
        });

        hit.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            // Only a primary-button release commits: releasing a stray
            // right/middle press mid-drag arrives on the captured pointer and
            // would otherwise commit the drag early.
            if (evt.button != 0)
                return;

            dragging = false;
            suppressHighwayPreviewRebuild = false;
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
                hit.schedule.Execute(RefreshTimelineAndSidebar);
                return;
            }

            if (moved && liveAnchor != null)
            {
                ChartEditorTimingService.MoveAnchor(project, liveAnchor, liveAnchor.audioTimeSeconds, moveContentWithBeatMap);
                SelectSingleAnchor(liveAnchor);
                evt.StopImmediatePropagation();
                hit.schedule.Execute(RefreshTimelineAndSidebar);
                return;
            }

            if (marker.isAnchor)
            {
                SelectSingleAnchor(marker);
                project.cursorTimeSeconds = marker.audioTimeSeconds;
                RefreshTimelineAndSidebar();
            }
            else
            {
                SeekAndRevealTime(marker.audioTimeSeconds, syncAudio: true, rebuild: false);
            }

            evt.StopPropagation();
        });

        hit.RegisterCallback<PointerCaptureOutEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            // The drag was interrupted (element rebuilt, pointer cancelled)
            // before PointerUp could commit. Heal the preview-scoped remap
            // with a full pass so no track keeps stale timing, and release
            // the 3D preview freeze.
            dragging = false;
            suppressHighwayPreviewRebuild = false;
            if (moved && project != null)
            {
                if (moveContentWithBeatMap)
                    ChartEditorTimingService.ApplyBeatMapToContent(project);
                else
                    ChartEditorTimingService.AttachContentToBeatMap(project);
                RootElement?.schedule.Execute(RefreshTimelineAndSidebar);
            }
        });
    }

    private void AddToneMarkerInteractionHandlers(VisualElement hit, ChartEditorToneChange change)
    {
        if (hit == null || change == null)
            return;

        bool dragging = false;
        bool moved = false;
        int pointerId = -1;
        Vector2 startPointer = Vector2.zero;
        float startTimelineScrollX = 0f;
        float startTime = 0f;

        hit.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (!toneEditorEnabled)
                return;

            if (evt.button == 1)
            {
                toneLabPanelFocused = false;
                SelectToneChange(change, seek: false, rebuild: false, focusToneLab: false);
                ShowToneChangeContextMenu(evt.position, change);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
                return;

            HideContextMenu();
            toneLabPanelFocused = false;
            SelectToneChange(change, seek: false, rebuild: false, focusToneLab: true);
            dragging = true;
            moved = false;
            pointerId = evt.pointerId;
            startPointer = PointerPosition(evt);
            startTimelineScrollX = timelineScrollOffset.x;
            startTime = change.timeSeconds;
            hit.CapturePointer(pointerId);
            evt.StopPropagation();
        });

        hit.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            Vector2 pointer = PointerPosition(evt);
            float horizontalDragPixels = GetTimelineDragPixelsWithAutoPan(pointer, startPointer, startTimelineScrollX);
            if (Mathf.Abs(horizontalDragPixels) > 1f)
                moved = true;

            float nextTime = Mathf.Clamp(startTime + (float)PixelDeltaToSeconds(horizontalDragPixels), 0f, Mathf.Max(0f, GetProjectDurationSeconds()));
            change.timeSeconds = nextTime;
            if (project != null)
                project.dirty = true;

            appliedToneEditorPlaybackKey = string.Empty;
            UpdateToneMarkerVisuals();
            UpdatePlaybackVisuals();
            evt.StopPropagation();
        });

        hit.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            if (evt.button != 0)
                return;

            dragging = false;
            if (hit.HasPointerCapture(pointerId))
                hit.ReleasePointer(pointerId);

            NormalizeToneChanges(project?.SelectedTrack);
            if (moved)
            {
                evt.StopImmediatePropagation();
                hit.schedule.Execute(() =>
                {
                    RefreshToneMarkerLane();
                    RefreshLeftPanel();
                });
                return;
            }

            SelectToneChange(change, seek: false, rebuild: true, focusToneLab: true);
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
        project.cursorTimeSeconds = Math.Max(0.0, Math.Min(GetProjectDurationSeconds(), marker.audioTimeSeconds));
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

    private void BuildCompactTrackRow(
        VisualElement noteLayer,
        ChartEditorTrack track,
        Color accent,
        float rowTop,
        double windowStartSeconds,
        double windowEndSeconds)
    {
        using ProfilerMarker.AutoScope scope = CompactRowMarker.Auto();
        TimelineNotesMeshElement meshElement = new TimelineNotesMeshElement();
        meshElement.style.position = Position.Absolute;
        meshElement.style.left = 0f;
        meshElement.style.right = 0f;
        meshElement.style.top = 0f;
        meshElement.style.bottom = 0f;
        noteLayer.Add(meshElement);

        List<ChartEditorNote> notes = track.notes ?? new List<ChartEditorNote>();
        for (int i = 0; i < notes.Count; i++)
        {
            ChartEditorNote note = notes[i];
            if (note == null)
                continue;

            EnsureNoteDurationCoversTechniqueSegments(note);
            float left = TimeToPixels(note.timeSeconds);
            float width = GetNoteDrawWidth(track, note, 1, false, left);
            Color color = IsNoteSelected(note)
                ? new Color(1f, 0.88f, 0.42f, 0.62f)
                : new Color(accent.r, accent.g, accent.b, 0.42f);
            meshElement.AddQuad(left, 44f, width, CompactNoteHeight, color);

            if (note.timeSeconds <= windowEndSeconds &&
                note.timeSeconds + Math.Max(0.0, GetNoteEffectiveDurationSeconds(note)) >= windowStartSeconds)
            {
                currentNoteHits.Add(new ChartEditorNoteHit
                {
                    track = track,
                    note = note,
                    rect = new Rect(TimelineLabelWidth + left, rowTop + 44f, width, CompactNoteHeight)
                });
            }
        }

        meshElement.Commit();

        noteLayer.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (toneEditorEnabled)
                return;

            if (evt.button != 0 && evt.button != 1)
                return;

            ChartEditorNote nearest = FindNearestNoteAtPixel(track, evt.localPosition.x);
            if (nearest == null)
                return;

            SelectSingleNote(track, nearest);
            if (evt.button == 1)
                ShowNoteContextMenu(evt.position, track, nearest);
            evt.StopPropagation();
            noteLayer.schedule.Execute(RefreshTimelineAndSidebar);
        });
    }

    private ChartEditorNote FindNearestNoteAtPixel(ChartEditorTrack track, float localX)
    {
        if (track?.notes == null || track.notes.Count == 0)
            return null;

        double time = PixelsToSeconds(Mathf.Max(0f, localX));
        double tolerance = Math.Max(0.05, PixelDeltaToSeconds(14f));
        ChartEditorNote best = null;
        double bestDistance = double.MaxValue;
        for (int i = 0; i < track.notes.Count; i++)
        {
            ChartEditorNote note = track.notes[i];
            if (note == null)
                continue;

            double distance = Math.Abs(note.timeSeconds - time);
            double end = note.timeSeconds + Math.Max(0.0, GetNoteEffectiveDurationSeconds(note));
            if (time >= note.timeSeconds && time <= end)
                distance = 0.0;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = note;
            }
        }

        return bestDistance <= tolerance ? best : null;
    }

    private void BuildNotes(VisualElement timeline)
    {
        using ProfilerMarker.AutoScope scope = BuildNotesMarker.Auto();
        List<ChartEditorTrackViewGroup> groups = BuildTrackViewGroups();
        if (groups.Count == 0)
            return;

        GetTimelineBuildWindowSeconds(out double windowStartSeconds, out double windowEndSeconds);
        int generation = ++timelineNoteBuildGeneration;
        List<Action> pendingNoteBuilders = new List<Action>();
        List<double> pendingNoteTimes = new List<double>();
        List<VisualElement> pendingTechniqueLayers = new List<VisualElement>();
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
            rowBackground.style.borderTopColor = new Color(0.15f, 0.145f, 0.18f, 1f);
            rowBackground.style.borderBottomColor = new Color(0.15f, 0.145f, 0.18f, 1f);
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

            if (!selectedTrack)
            {
                // Non-selected tracks are density strips: draw every note as a
                // quad in a single mesh instead of thousands of picked
                // elements. One row-level handler covers click-to-select.
                BuildCompactTrackRow(noteLayer, track, accent, rowTop, windowStartSeconds, windowEndSeconds);
                rowTop += rowHeight + TrackRowGap;
                continue;
            }

            float[] laneDrawnRight = new float[Mathf.Max(1, laneCount)];
            for (int laneIndex = 0; laneIndex < laneDrawnRight.Length; laneIndex++)
                laneDrawnRight[laneIndex] = float.MinValue;

            foreach (ChartEditorNote note in track.notes ?? new List<ChartEditorNote>())
            {
                if (note == null)
                    continue;

                EnsureNoteDurationCoversTechniqueSegments(note);

                // Timeline virtualization: only notes near the visible viewport
                // get elements. Offscreen ranges are filled in on demand when
                // the view scrolls (see CheckTimelineWindowRefill).
                if (note.timeSeconds > windowEndSeconds ||
                    note.timeSeconds + Math.Max(0.0, GetNoteEffectiveDurationSeconds(note)) < windowStartSeconds)
                {
                    continue;
                }

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

                // When zoomed far out, notes shrink below a pixel; drawing
                // every one produces tens of thousands of invisible elements.
                // Skip blocks that add less than ~1px of new coverage in their
                // lane (hit records above are still added, so selection data
                // stays complete). Normal zoom levels are unaffected.
                int drawnLane = Mathf.Clamp(lane, 0, laneDrawnRight.Length - 1);
                bool subPixel = selectedTrack ? width < 2f : true;
                if (subPixel && left + Mathf.Max(1f, width) <= laneDrawnRight[drawnLane] + 1f)
                    continue;
                laneDrawnRight[drawnLane] = Mathf.Max(laneDrawnRight[drawnLane], left + Mathf.Max(1f, width));

                // The visual itself is created lazily in per-frame slices so a
                // dense window never stalls a single frame.
                pendingNoteTimes.Add(note.timeSeconds);
                pendingNoteBuilders.Add(() =>
                {
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
                {
                    currentNoteBlocks[note.id] = block;
                    currentNoteBlockStyles[note.id] = (noteBaseColor, selectedTrack);
                    currentNoteBlockTimings.Add(new NoteBlockTimingRef
                    {
                        block = block,
                        track = track,
                        note = note,
                        laneCount = laneCount,
                        selectedTrack = selectedTrack
                    });
                }
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
                    if (toneEditorEnabled)
                        return;

                    if (evt.button == 0 && evt.clickCount >= 2)
                    {
                        SelectSingleNote(track, note);
                        ShowNoteEditPopup(track, note);
                        evt.StopPropagation();
                    }
                });
                block.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (toneEditorEnabled)
                        return;

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
                });
            }

            if (selectedTrack && techniqueLayer != null)
            {
                noteLayer.Add(techniqueLayer);
                pendingTechniqueLayers.Add(techniqueLayer);
            }

            rowTop += rowHeight + TrackRowGap;
        }

        if (pendingNoteBuilders.Count == 0)
            return;

        // Build the strictly visible viewport first so the screen fills
        // instantly, then stream the margin notes in over following frames.
        float buildScrollX = Mathf.Max(0f, timelineScrollOffset.x);
        float buildViewportWidth = timelineViewportWidth > 1f ? timelineViewportWidth : 2600f;
        double strictStartSeconds = PixelsToSeconds(Mathf.Max(0f, buildScrollX - TimelineLabelWidth));
        double strictEndSeconds = PixelsToSeconds(Mathf.Max(0f, buildScrollX + buildViewportWidth));
        int[] buildOrder = Enumerable.Range(0, pendingNoteBuilders.Count)
            .OrderBy(i => pendingNoteTimes[i] >= strictStartSeconds && pendingNoteTimes[i] <= strictEndSeconds ? 0 : 1)
            .ToArray();

        int buildCursor = 0;
        void BuildNoteChunk(int budget)
        {
            using ProfilerMarker.AutoScope chunkScope = NoteChunkMarker.Auto();
            while (buildCursor < buildOrder.Length && budget-- > 0)
                pendingNoteBuilders[buildOrder[buildCursor++]]?.Invoke();

            for (int i = 0; i < pendingTechniqueLayers.Count; i++)
                pendingTechniqueLayers[i]?.BringToFront();
        }

        BuildNoteChunk(700);
        if (buildCursor < buildOrder.Length)
        {
            timeline.schedule.Execute(() =>
            {
                if (generation != timelineNoteBuildGeneration)
                    return;

                BuildNoteChunk(450);
            }).Every(16).Until(() => generation != timelineNoteBuildGeneration || buildCursor >= buildOrder.Length);
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

        return Math.Min(Math.Max(0.001, GetProjectDurationSeconds()), Math.Max(0.001, waveformData.durationSeconds));
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
        if (project == null || waveformData == null || !waveformData.IsValid || GetProjectDurationSeconds() <= 0.001)
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

        if (nextTime >= GetProjectDurationSeconds() - 0.0001)
        {
            editorPlaying = false;
            if (editorAudioSource != null)
                editorAudioSource.Stop();
            lastAuditionTimeSeconds = -1.0;
            InvalidateAuditionCache();
            SetCursorTime(GetProjectDurationSeconds(), rebuild: false, syncAudio: false);
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

        project.cursorTimeSeconds = Math.Max(0.0, Math.Min(GetProjectDurationSeconds(), seconds));
        if (syncAudio)
            lastAuditionTimeSeconds = project.cursorTimeSeconds;
        if (syncAudio)
            SyncEditorAudioToCursor(playImmediately: editorPlaying);
        UpdatePlaybackVisuals();
        UpdateToneEditorPlaybackOverride();
        if (editorPlaying && !seekDragging)
            FollowTimelineTime(project.cursorTimeSeconds);
        if (rebuild)
            Rebuild();
    }

    private void SeekAndRevealTime(double seconds, bool syncAudio, bool rebuild)
    {
        double target = Math.Max(0.0, project != null ? Math.Min((double)GetProjectDurationSeconds(), seconds) : seconds);
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

    // ChartEditorProject.DurationSeconds scans every note of every track per
    // access, which is far too slow for per-frame and per-pointer-move code.
    // All overlay reads go through this once-per-frame cache instead.
    private float GetProjectDurationSeconds()
    {
        if (project == null)
            return 0f;

        if (cachedProjectDurationFrame != Time.frameCount || !ReferenceEquals(cachedProjectDurationSource, project))
        {
            cachedProjectDurationSeconds = project.DurationSeconds;
            cachedProjectDurationFrame = Time.frameCount;
            cachedProjectDurationSource = project;
        }

        return cachedProjectDurationSeconds;
    }

    private void UpdatePlaybackVisuals()
    {
        if (project != null)
            headerTimeLabel.text = $"{FormatTime(project.cursorTimeSeconds)} / {FormatTime(GetProjectDurationSeconds())}";
        UpdateHeaderProgress();
        if (transportPlayButton != null)
            UpdateTransportPlayButtonIcon();
        if (cursorElement != null && project != null)
            cursorElement.style.left = TimeToPixels(project.cursorTimeSeconds);
        if (cursorHandleElement != null && project != null)
            cursorHandleElement.style.left = TimelineLabelWidth + TimeToPixels(project.cursorTimeSeconds) - 18f;
    }

    private void UpdateHeaderProgress()
    {
        if (headerProgressFill == null)
            return;

        float progress = project == null || GetProjectDurationSeconds() <= 0.0001
            ? 0f
            : Mathf.Clamp01((float)(project.cursorTimeSeconds / GetProjectDurationSeconds()));
        headerProgressFill.style.width = Length.Percent(progress * 100f);
    }

    private void HandleKeyboardShortcuts()
    {
        // No global shortcuts while a modal popup or context menu is open
        // (Delete/Space would act on the timeline behind the dialog), or while
        // a pointer drag is captured (shortcuts that Rebuild() would destroy
        // the capturing element mid-gesture and abandon the drag commit).
        if (project == null || IsTextFieldFocused() ||
            editPopupElement != null || contextMenuElement != null || contextSubmenuElement != null ||
            IsAnyPointerCaptured())
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

        if (toneEditorEnabled)
        {
            if (HandleToneEditorKeyboardShortcuts(controlHeld, shiftHeld, altHeld))
            {
                ResetArrowRepeat();
                return;
            }

            if (!IsAnyArrowKeyHeld())
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

    private bool HandleToneEditorKeyboardShortcuts(bool controlHeld, bool shiftHeld, bool altHeld)
    {
        if (!controlHeld && Input.GetKeyDown(KeyCode.A))
        {
            AddToneChangeAtCursor();
            return true;
        }

        if (Input.GetKeyDown(KeyCode.Delete))
        {
            EnsureToneSelectionForCurrentTrack();
            if (selectedToneChange != null)
                DeleteToneChange(selectedToneChange);
            return true;
        }

        if (HandlePageNavigation(controlHeld, shiftHeld: false, altHeld: false))
            return true;

        bool handledArrow =
            HandleArrowRepeat(KeyCode.LeftArrow, () => ApplyToneEditorHorizontalNudge(-1, controlHeld)) ||
            HandleArrowRepeat(KeyCode.RightArrow, () => ApplyToneEditorHorizontalNudge(1, controlHeld));

        if (handledArrow)
            return true;

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            return true;

        return false;
    }

    private void ApplyToneEditorHorizontalNudge(int direction, bool moveCursorOnly)
    {
        if (project == null)
            return;

        double delta = (moveCursorOnly ? GetSnapStepSeconds() : GetKeyboardNudgeSeconds()) * direction;
        if (moveCursorOnly || selectedToneChange == null)
        {
            SetCursorTimeFromKeyboard(project.cursorTimeSeconds + delta);
            FollowTimelineTime(project.cursorTimeSeconds);
            return;
        }

        ChartEditorTrack track = project.SelectedTrack;
        selectedToneChange.timeSeconds = Mathf.Clamp(
            selectedToneChange.timeSeconds + (float)delta,
            0f,
            (float)Math.Max(0.0, GetProjectDurationSeconds()));
        NormalizeToneChanges(track);
        project.cursorTimeSeconds = selectedToneChange.timeSeconds;
        project.dirty = true;
        appliedToneEditorPlaybackKey = string.Empty;
        UpdateToneMarkerVisuals();
        UpdatePlaybackVisuals();
        UpdateToneEditorPlaybackOverride();
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
        return Mathf.Max(TimelineMinSecondsWidth, GetProjectDurationSeconds() * GetTimelinePixelsPerSecond() + TimelineRightPadding);
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

            group.activeTrack = SelectPreferredDifficultyTrack(group.tracks);
        }

        return groups;
    }

    private ChartEditorTrackViewGroup GetSelectedTrackViewGroup(List<ChartEditorTrackViewGroup> groups)
    {
        if (groups == null || groups.Count == 0)
            return null;

        ChartEditorTrackViewGroup selected = groups.FirstOrDefault(group => group != null && group.ContainsSelected(project?.selectedTrackId));
        return selected ?? groups.FirstOrDefault(group => group != null && group.activeTrack != null);
    }

    private static ChartEditorTrack SelectPreferredDifficultyTrack(IEnumerable<ChartEditorTrack> tracks)
    {
        return OrderDifficultyTracks(tracks)
            .Where(track => track != null && track.visible)
            .FirstOrDefault()
            ?? OrderDifficultyTracks(tracks).FirstOrDefault();
    }

    private static List<ChartEditorTrack> OrderDifficultyTracks(IEnumerable<ChartEditorTrack> tracks)
    {
        return (tracks ?? Enumerable.Empty<ChartEditorTrack>())
            .Where(track => track != null)
            .OrderBy(ResolveDifficultyUiIndex)
            .ThenByDescending(track => track.notes?.Count ?? 0)
            .ThenBy(track => track.difficultyLabel ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetTrackGroupKey(ChartEditorTrack track)
    {
        if (track == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(track.arrangementGroupId))
            return $"arrangement|{NormalizeTrackKey(track.arrangementGroupId)}";

        string role = track.role.ToString();
        string tuning = NormalizeTrackKey(track.tuning?.displayName);
        string name = track.role == ChartEditorTrackRole.Custom
            ? NormalizeTrackKey(track.displayName)
            : string.Empty;
        return $"{role}|{tuning}|{name}";
    }

    private static int ResolveDifficultyUiIndex(ChartEditorTrack track)
    {
        if (track == null)
            return int.MaxValue;
        if (track.difficultyUiIndex >= 0)
            return track.difficultyUiIndex;
        if (string.Equals(track.difficultyLabel?.Trim(), "Full", StringComparison.OrdinalIgnoreCase))
            return 0;
        return int.TryParse(track.difficultyLabel, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? Mathf.Max(0, parsed)
            : int.MaxValue;
    }

    private static string FormatDifficultyLabel(ChartEditorTrack track)
    {
        if (track == null)
            return "Full";
        if (!string.IsNullOrWhiteSpace(track.difficultyLabel))
            return track.difficultyLabel.Trim();
        int index = ResolveDifficultyUiIndex(track);
        return index == 0 || index == int.MaxValue ? "Full" : index.ToString(CultureInfo.InvariantCulture);
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

    private static string ArrangementRouteForTrackRole(ChartEditorTrackRole role)
    {
        switch (role)
        {
            case ChartEditorTrackRole.Bass:
                return "Bass";
            case ChartEditorTrackRole.RhythmGuitar:
                return "Rhythm";
            case ChartEditorTrackRole.Drums:
                return "Drums";
            case ChartEditorTrackRole.Piano:
                return "Piano";
            case ChartEditorTrackRole.Vocals:
                return "Vocals";
            case ChartEditorTrackRole.Custom:
                return "Custom";
            default:
                return "Lead";
        }
    }

    private static string ArrangementInstrumentTypeForTrackRole(ChartEditorTrackRole role)
    {
        switch (role)
        {
            case ChartEditorTrackRole.Bass:
                return "bass";
            case ChartEditorTrackRole.Drums:
                return "drums";
            case ChartEditorTrackRole.Piano:
                return "piano";
            case ChartEditorTrackRole.Vocals:
                return "vocals";
            default:
                return "guitar";
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

    private static bool IsDrumTrack(ChartEditorTrack track)
    {
        return track?.role == ChartEditorTrackRole.Drums;
    }

    private static bool SanitizeDrumTrackTechniqueData(ChartEditorTrack track)
    {
        if (!IsDrumTrack(track) || track.notes == null)
            return false;

        bool changed = false;
        for (int i = 0; i < track.notes.Count; i++)
            changed |= ChartEditorDrumNoteSanitizer.Sanitize(track.notes[i]);

        return changed;
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

        PsarcCachedNoteData source = new PsarcCachedNoteData
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
            bendPoints = new List<PsarcCachedBendPointData>(),
            techniqueSegments = new List<PsarcCachedTechniqueSegmentData>()
        };

        for (int i = 0; i < note.bendPoints.Count; i++)
        {
            ChartEditorBendPoint point = note.bendPoints[i];
            if (point == null)
                continue;

            source.bendPoints.Add(new PsarcCachedBendPointData
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

                source.techniqueSegments.Add(new PsarcCachedTechniqueSegmentData
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

        List<NoteTechniqueSegmentData> normalized = PsarcTechniqueSegmentNormalizer.BuildNormalizedTechniqueSegments(source);
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
        float startTimelineScrollX = 0f;
        float startOffset = 0f;
        float endOffset = 0f;

        void BeginDrag(PointerDownEvent evt, int mode)
        {
            if (toneEditorEnabled)
                return;

            if (evt.button != 0)
                return;

            dragging = true;
            pointerId = evt.pointerId;
            dragMode = mode;
            startPointer = PointerPosition(evt);
            startTimelineScrollX = timelineScrollOffset.x;
            startOffset = Mathf.Max(0f, segment.startOffset);
            endOffset = Mathf.Max(startOffset + TechniqueSegmentMinimumSeconds, segment.endOffset);
            SelectSingleNote(track, note);
            box.BringToFront();
            box.CapturePointer(pointerId);
            evt.StopImmediatePropagation();
        }

        box.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (toneEditorEnabled)
                return;

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

            Vector2 pointer = PointerPosition(evt);
            float horizontalDragPixels = GetTimelineDragPixelsWithAutoPan(pointer, startPointer, startTimelineScrollX);
            float deltaSeconds = (float)PixelDeltaToSeconds(horizontalDragPixels);
            float newStart = startOffset;
            float newEnd = endOffset;
            float maxEnd = Mathf.Max(TechniqueSegmentMinimumSeconds, (float)((project != null ? GetProjectDurationSeconds() : note.timeSeconds + endOffset + 4.0) - note.timeSeconds));
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
            box.schedule.Execute(RefreshTimelinePanel);
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
        float startTimelineScrollX = 0f;
        double startDuration = 0.0;

        handle.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (toneEditorEnabled)
                return;

            if (evt.button != 0)
                return;

            dragging = true;
            pointerId = evt.pointerId;
            startPointer = PointerPosition(evt);
            startTimelineScrollX = timelineScrollOffset.x;
            startDuration = GetNoteEffectiveDurationSeconds(note);
            SelectSingleNote(track, note);
            handle.CapturePointer(pointerId);
            evt.StopImmediatePropagation();
        });

        handle.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!dragging || evt.pointerId != pointerId)
                return;

            Vector2 pointer = PointerPosition(evt);
            float horizontalDragPixels = GetTimelineDragPixelsWithAutoPan(pointer, startPointer, startTimelineScrollX);
            double requestedDuration = startDuration + PixelDeltaToSeconds(horizontalDragPixels);
            double maxDuration = Math.Max(0.01, GetProjectDurationSeconds() - note.timeSeconds);
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

            if (evt.button != 0)
                return;

            dragging = false;
            if (handle.HasPointerCapture(pointerId))
                handle.ReleasePointer(pointerId);

            // Commit the resize into beat-map space too — otherwise the next
            // beat-map pass restores the old durationBeats and silently
            // reverts the resize.
            ChartEditorTimingService.UpdateNoteBeatTiming(project, note);
            evt.StopImmediatePropagation();
            handle.schedule.Execute(RefreshTimelinePanel);
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
        {
            string[] drumLabels = new string[laneCount];
            for (int lane = 0; lane < laneCount; lane++)
                drumLabels[lane] = DrumLaneMapper.GetLaneLabel(lane);
            return drumLabels;
        }

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
        if (track?.role == ChartEditorTrackRole.Drums)
            return DrumLaneMapper.LaneCount;

        int count = track?.role == ChartEditorTrackRole.Bass ? 4 :
            track?.role == ChartEditorTrackRole.Piano ? 8 : 6;

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
        double clamped = Math.Max(0.0, Math.Min(GetProjectDurationSeconds(), seconds));
        double snap = project?.settings?.snapEnabled == true ? Math.Max(0.001, project.settings.snapSeconds) : 0.0;
        return snap > 0.0 ? Math.Round(clamped / snap) * snap : clamped;
    }

    private static Vector2 PointerPosition(IPointerEvent evt)
    {
        return new Vector2(evt.position.x, evt.position.y);
    }

    private static bool IsControlKeyHeld()
    {
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
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

    private float GetTimelineDragPixelsWithAutoPan(Vector2 pointerWorldPosition, Vector2 startPointerWorldPosition, float startScrollX)
    {
        PanTimelineDuringSeekDrag(pointerWorldPosition);
        return (pointerWorldPosition.x - startPointerWorldPosition.x) + (timelineScrollOffset.x - startScrollX);
    }

    private void AddNoteDragHandlers(VisualElement block, ChartEditorTrack track, ChartEditorNote note, int laneCount, float laneHeight, float laneTop, bool selectedTrack, float noteHeight)
    {
        bool dragging = false;
        bool moved = false;
        bool clearedNonNoteSelection = false;
        int pointerId = -1;
        Vector2 startPointer = Vector2.zero;
        float startTimelineScrollX = 0f;
        double startTime = 0.0;
        double pendingTimeDelta = 0.0;
        int pendingVisualLaneDelta = 0;
        List<ChartEditorNoteDragStart> dragStarts = new List<ChartEditorNoteDragStart>();

        block.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (toneEditorEnabled)
                return;

            if (evt.button != 0 || note == null || track == null)
                return;

            if (evt.ctrlKey || IsControlKeyHeld())
            {
                ToggleNoteSelection(track, note);
                evt.StopImmediatePropagation();
                return;
            }

            clearedNonNoteSelection = !string.IsNullOrWhiteSpace(selectedSectionId) || HasSelectedAnchors();
            List<string> affectedSelectionIds = new List<string>(selectedNoteIds);
            if (!string.IsNullOrWhiteSpace(selectedNoteId))
                affectedSelectionIds.Add(selectedNoteId);

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

            affectedSelectionIds.AddRange(selectedNoteIds);
            RefreshNoteSelectionVisuals(affectedSelectionIds);

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
            startTimelineScrollX = timelineScrollOffset.x;
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
            float horizontalDragPixels = GetTimelineDragPixelsWithAutoPan(pointer, startPointer, startTimelineScrollX);
            float verticalDragPixels = pointer.y - startPointer.y;
            if (Mathf.Abs(horizontalDragPixels) > 1f || Mathf.Abs(verticalDragPixels) > 1f)
                moved = true;

            double requestedTimeDelta = PixelDeltaToSeconds(horizontalDragPixels);
            double minTimeDelta = dragStarts.Count == 0 ? -startTime : -dragStarts.Min(start => start.timeSeconds);
            double maxTimeDelta = dragStarts.Count == 0 ? GetProjectDurationSeconds() - startTime : GetProjectDurationSeconds() - dragStarts.Max(start => start.timeSeconds);
            pendingTimeDelta = Math.Max(minTimeDelta, Math.Min(maxTimeDelta, requestedTimeDelta));
            pendingVisualLaneDelta = selectedTrack ? Mathf.RoundToInt(verticalDragPixels / Mathf.Max(1f, laneHeight)) : 0;
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

            if (evt.button != 0)
                return;

            dragging = false;
            if (block.HasPointerCapture(pointerId))
                block.ReleasePointer(pointerId);

            if (moved)
            {
                for (int i = 0; i < dragStarts.Count; i++)
                {
                    ChartEditorNoteDragStart start = dragStarts[i];
                    double targetTime = Math.Max(0.0, Math.Min(GetProjectDurationSeconds(), start.timeSeconds + pendingTimeDelta));
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

                // RefreshTimelineWindowContent (the common commit path) never
                // marks the 3D preview dirty, so without this the highway kept
                // rendering pre-drag note data.
                MarkHighwayPreviewDirty(renderImmediately: false);
                InvalidateAuditionCache();
            }

            if (moved)
            {
                evt.StopImmediatePropagation();
                block.schedule.Execute(clearedNonNoteSelection
                    ? (Action)RefreshTimelinePanel
                    : RefreshTimelineWindowContent);
            }
            else
            {
                if (clearedNonNoteSelection)
                    block.schedule.Execute(RefreshTimelinePanel);
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
        float startTimelineScrollX = 0f;
        double startTime = 0.0;
        double startEnd = 0.0;
        double startChart = 0.0;
        double startChartEnd = 0.0;

        block.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (toneEditorEnabled)
                return;

            if (evt.button != 0 || section == null)
                return;

            dragging = true;
            moved = false;
            pointerId = evt.pointerId;
            startPointer = PointerPosition(evt);
            startTimelineScrollX = timelineScrollOffset.x;
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

            Vector2 pointer = PointerPosition(evt);
            float horizontalDragPixels = GetTimelineDragPixelsWithAutoPan(pointer, startPointer, startTimelineScrollX);
            if (Mathf.Abs(horizontalDragPixels) > 1f)
                moved = true;

            double newStart = SnapTime(startTime + PixelDeltaToSeconds(horizontalDragPixels));
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

            if (evt.button != 0)
                return;

            dragging = false;
            if (block.HasPointerCapture(pointerId))
                block.ReleasePointer(pointerId);

            ChartEditorTimingService.NormalizeSections(project);
            if (moved)
            {
                ChartEditorTimingService.UpdateSectionBeatTiming(project, section);
                evt.StopImmediatePropagation();
                block.schedule.Execute(RefreshTimelineAndSidebar);
            }
        });
    }

    private void AddSyncPointDragHandlers(VisualElement marker, ChartEditorBeatMarker point)
    {
        bool dragging = false;
        bool moved = false;
        int pointerId = -1;
        Vector2 startPointer = Vector2.zero;
        float startTimelineScrollX = 0f;
        double startAudio = 0.0;
        double minAudio = 0.0;
        double maxAudio = 0.0;

        marker.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (toneEditorEnabled)
                return;

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
                marker.schedule.Execute(RefreshTimelineAndSidebar);
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
            startTimelineScrollX = timelineScrollOffset.x;
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

            Vector2 pointer = PointerPosition(evt);
            float horizontalDragPixels = GetTimelineDragPixelsWithAutoPan(pointer, startPointer, startTimelineScrollX);
            if (Mathf.Abs(horizontalDragPixels) > 1f)
                moved = true;

            double newAudio = startAudio + PixelDeltaToSeconds(horizontalDragPixels);
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
                marker.schedule.Execute(RefreshTimelineAndSidebar);
            }
            else
            {
                if (!IsAnchorSelected(point) || GetSelectedAnchorCount() <= 1)
                    SelectSingleAnchor(point);
                else
                    selectedSyncPointId = point.id;
                evt.StopPropagation();
                marker.schedule.Execute(RefreshTimelineAndSidebar);
            }
        });
    }

    private void ResolveAnchorDragBounds(ChartEditorBeatMarker point, out double minAudio, out double maxAudio)
    {
        minAudio = 0.0;
        maxAudio = GetProjectDurationSeconds();
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
        panel.Add(CreateKeyValue("Difficulty", FormatDifficultyLabel(track)));
        panel.Add(CreateKeyValue("Tuning", FirstNonEmpty(track.tuning?.displayName, "Unknown")));
        panel.Add(CreateKeyValue("Notes", (track.notes?.Count ?? 0).ToString()));
        panel.Add(CreateToggleButton("Visible", track.visible, () => { track.visible = !track.visible; project.dirty = true; Rebuild(); }));
        panel.Add(CreateToggleButton("Muted", track.muted, () => { track.muted = !track.muted; project.dirty = true; Rebuild(); }));
        panel.Add(CreateToggleButton("Solo", track.solo, () => { track.solo = !track.solo; project.dirty = true; Rebuild(); }));
    }

    private void BuildNoteInspector(VisualElement panel, ChartEditorTrack track, ChartEditorNote note)
    {
        panel.Add(CreateSectionTitle("Selected Note"));
        panel.Add(CreateKeyValue("Time", FormatTime(note.timeSeconds)));
        panel.Add(CreateKeyValue("Duration", $"{GetNoteEffectiveDurationSeconds(note) * 1000.0:F0} ms"));
        panel.Add(CreateKeyValue("String/Lane", note.stringOrLane.ToString()));
        panel.Add(CreateKeyValue("Fret", note.fret.ToString()));
        if (!IsDrumTrack(track))
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
        if (IsDrumTrack(track))
            return;

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

    private void ShowNewProjectPopup() => ShowProjectFormPopup(editExisting: false);

    private void ShowProjectSettingsPopup()
    {
        if (project == null)
            return;

        ShowProjectFormPopup(editExisting: true);
    }

    // One form, two modes: creating a new project, or editing the current one
    // (same visuals, pre-filled; picking a chart/audio file stages a
    // replacement that is applied with correct re-alignment semantics).
    private void ShowProjectFormPopup(bool editExisting)
    {
        HideContextMenu();
        HideEditPopup();

        string currentChartName = editExisting ? Path.GetFileName(project.sourcePath ?? string.Empty) : string.Empty;
        string currentAudioName = editExisting ? (project.audio?.displayName ?? string.Empty) : string.Empty;

        // In edit mode these hold a PENDING replacement; empty means "keep the
        // current file".
        string chartPath = string.Empty;
        string audioPath = string.Empty;
        string coverImagePath = editExisting ? (project.metadata?.coverImagePath ?? string.Empty) : string.Empty;
        SongNotationSourceKind chartKind = SongNotationSourceKind.None;
        List<MusicXmlLoader.MusicXmlPartSummary> arrangements = new List<MusicXmlLoader.MusicXmlPartSummary>();
        HashSet<int> selectedArrangementIndices = new HashSet<int>();
        string lastAutoFilledTitle = string.Empty;
        string lastAutoFilledArtist = string.Empty;

        Color accentPurple = new Color(0.62f, 0.38f, 1f, 1f);
        Color accentBlue = new Color(0.48f, 0.74f, 1f, 1f);
        Color accentGreen = new Color(0.52f, 0.84f, 0.72f, 1f);
        Color textPrimary = new Color(0.97f, 0.96f, 0.98f, 1f);
        Color textMuted = new Color(0.78f, 0.76f, 0.82f, 1f);
        Color textFaint = new Color(0.60f, 0.58f, 0.66f, 1f);
        Color panelBackground = new Color(0.072f, 0.069f, 0.080f, 1f);
        Color bandBackground = new Color(0.100f, 0.096f, 0.112f, 1f);
        Color cardBackground = new Color(0.108f, 0.103f, 0.122f, 1f);
        Color insetBackground = new Color(0.042f, 0.040f, 0.048f, 1f);
        Color cardBorderColor = new Color(0.75f, 0.72f, 0.82f, 0.22f);
        Color hairline = new Color(0.75f, 0.72f, 0.82f, 0.16f);

        TextField titleField = CreateNewProjectTextField("Title", editExisting ? project.metadata?.title ?? string.Empty : string.Empty);
        TextField artistField = CreateNewProjectTextField("Artist", editExisting ? project.metadata?.artist ?? string.Empty : string.Empty);
        TextField albumField = CreateNewProjectTextField("Album", editExisting ? project.metadata?.album ?? string.Empty : string.Empty);
        TextField genreField = CreateNewProjectTextField("Genre", editExisting ? project.metadata?.genre ?? string.Empty : string.Empty);
        TextField yearField = CreateNewProjectTextField("Year", editExisting ? project.metadata?.year ?? string.Empty : string.Empty);

        Label chartPathLabel = CreateLabel("No chart selected", 28f, textMuted, false, TextAnchor.MiddleLeft, false);
        chartPathLabel.style.whiteSpace = WhiteSpace.Normal;
        chartPathLabel.style.marginTop = 20f;
        Label chartDetailLabel = CreateLabel("Start blank, or import a Guitar Pro / MusicXML file.", 22f, textFaint, false, TextAnchor.MiddleLeft, false);
        chartDetailLabel.style.whiteSpace = WhiteSpace.Normal;
        chartDetailLabel.style.marginTop = 6f;
        VisualElement arrangementList = new VisualElement();

        Label audioPathLabel = CreateLabel("No audio selected", 28f, textMuted, false, TextAnchor.MiddleLeft, false);
        audioPathLabel.style.whiteSpace = WhiteSpace.Normal;
        audioPathLabel.style.marginTop = 20f;
        Label audioDetailLabel = CreateLabel("Audio can be added now or later.", 22f, textFaint, false, TextAnchor.MiddleLeft, false);
        audioDetailLabel.style.whiteSpace = WhiteSpace.Normal;
        audioDetailLabel.style.marginTop = 6f;

        Button selectChartButton = null;
        Button clearChartButton = null;
        Button selectAudioButton = null;
        Button clearAudioButton = null;

        Label statusLabel = CreateLabel(string.Empty, 24f, new Color(1f, 0.64f, 0.54f, 1f), true, TextAnchor.MiddleLeft, false);
        statusLabel.style.whiteSpace = WhiteSpace.Normal;
        statusLabel.style.flexGrow = 1f;
        statusLabel.style.flexShrink = 1f;
        statusLabel.style.minWidth = 0f;
        statusLabel.style.marginRight = 24f;
        statusLabel.style.alignSelf = Align.Center;
        statusLabel.style.display = DisplayStyle.None;

        void SetDialogStatus(string text, bool isError)
        {
            statusLabel.text = text ?? string.Empty;
            statusLabel.style.color = isError
                ? new Color(1f, 0.60f, 0.52f, 1f)
                : new Color(0.80f, 0.74f, 1f, 1f);
            statusLabel.style.display = string.IsNullOrWhiteSpace(statusLabel.text)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            if (!string.IsNullOrWhiteSpace(statusLabel.text))
                SetStatus(statusLabel.text);
        }

        VisualElement CreateCard()
        {
            VisualElement card = new VisualElement();
            card.style.backgroundColor = cardBackground;
            SetRadius(card, 20f);
            SetBorderWidth(card, 3f);
            SetBorderColor(card, new Color(0.75f, 0.72f, 0.82f, 0.28f));
            card.style.paddingLeft = 30f;
            card.style.paddingRight = 30f;
            card.style.paddingTop = 28f;
            card.style.paddingBottom = 28f;
            card.style.marginBottom = 28f;
            card.style.flexShrink = 0f;
            return card;
        }

        Label CreateChipLabel(string text)
        {
            Label chip = CreateLabel((text ?? string.Empty).ToUpperInvariant(), 17f, textFaint, true, TextAnchor.MiddleCenter, false);
            chip.style.letterSpacing = 2f;
            chip.style.paddingLeft = 14f;
            chip.style.paddingRight = 14f;
            chip.style.paddingTop = 5f;
            chip.style.paddingBottom = 5f;
            chip.style.marginLeft = 16f;
            chip.style.backgroundColor = new Color(0.75f, 0.72f, 0.82f, 0.10f);
            SetRadius(chip, 10f);
            SetBorderWidth(chip, 2f);
            SetBorderColor(chip, new Color(0.75f, 0.72f, 0.82f, 0.28f));
            chip.style.flexShrink = 0f;
            return chip;
        }

        Label CreateSectionCaption(string text)
        {
            Label caption = CreateLabel((text ?? string.Empty).ToUpperInvariant(), 19f, textFaint, true, TextAnchor.MiddleLeft, false);
            caption.style.letterSpacing = 3f;
            return caption;
        }

        VisualElement BuildFileCard(
            NewProjectIconKind iconKind,
            Color accent,
            string cardTitle,
            string chipText,
            Label nameLabel,
            Label detailLabel,
            Button browseButton,
            Button clearButton,
            VisualElement extraContent)
        {
            VisualElement card = CreateCard();

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;

            header.Add(CreateNewProjectIconTile(iconKind, accent, 68f));

            VisualElement titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.flexGrow = 1f;
            titleRow.style.flexShrink = 1f;
            titleRow.style.minWidth = 0f;
            titleRow.style.marginLeft = 20f;
            Label cardTitleLabel = CreateLabel(cardTitle, 31f, textPrimary, true, TextAnchor.MiddleLeft, false);
            cardTitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
            titleRow.Add(cardTitleLabel);
            if (!string.IsNullOrEmpty(chipText))
                titleRow.Add(CreateChipLabel(chipText));
            header.Add(titleRow);

            VisualElement headerActions = new VisualElement();
            headerActions.style.flexDirection = FlexDirection.Row;
            headerActions.style.alignItems = Align.Center;
            headerActions.style.flexShrink = 0f;
            headerActions.style.marginLeft = 20f;
            headerActions.Add(browseButton);
            clearButton.style.marginLeft = 12f;
            headerActions.Add(clearButton);
            header.Add(headerActions);

            card.Add(header);
            card.Add(nameLabel);
            card.Add(detailLabel);
            if (extraContent != null)
                card.Add(extraContent);
            return card;
        }

        string FormatChartKind(SongNotationSourceKind kind)
        {
            switch (kind)
            {
                case SongNotationSourceKind.Gp5:
                    return "Guitar Pro";
                case SongNotationSourceKind.MusicXml:
                    return "MusicXML";
                default:
                    return "Chart";
            }
        }

        string FormatArrangementTitle(MusicXmlLoader.MusicXmlPartSummary summary)
        {
            string name = FirstNonEmpty(summary?.GroupDisplayName, summary?.Name, "Track");
            string difficulty = summary?.DifficultyLabel?.Trim();
            if (string.IsNullOrWhiteSpace(difficulty) ||
                string.Equals(difficulty, "Full", StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }

            return $"{name} - {difficulty}";
        }

        string FormatArrangementDetail(MusicXmlLoader.MusicXmlPartSummary summary)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(summary?.Route))
                parts.Add(summary.Route.Trim());
            if (!string.IsNullOrWhiteSpace(summary?.InstrumentType))
                parts.Add(summary.InstrumentType.Trim());
            parts.Add($"{Math.Max(0, summary?.NoteCount ?? 0)} notes");
            if (!string.IsNullOrWhiteSpace(summary?.TuningDisplayName))
                parts.Add(summary.TuningDisplayName.Trim());
            return string.Join("  ·  ", parts);
        }

        void RefreshArrangementList()
        {
            arrangementList.Clear();
            arrangementList.style.marginTop = 0f;

            if (string.IsNullOrWhiteSpace(chartPath))
                return;

            arrangementList.style.marginTop = 28f;

            if (arrangements.Count == 0)
            {
                Label warn = CreateLabel("No arrangements were detected in this file.", 22f, new Color(1f, 0.74f, 0.50f, 1f), false, TextAnchor.MiddleLeft, false);
                warn.style.whiteSpace = WhiteSpace.Normal;
                arrangementList.Add(warn);
                return;
            }

            int selectedCount = arrangements.Count(summary => summary != null && selectedArrangementIndices.Contains(summary.Index));

            VisualElement listHeader = new VisualElement();
            listHeader.style.flexDirection = FlexDirection.Row;
            listHeader.style.alignItems = Align.Center;
            listHeader.style.justifyContent = Justify.SpaceBetween;
            listHeader.style.marginBottom = 14f;
            listHeader.Add(CreateSectionCaption("Arrangements"));
            listHeader.Add(CreateLabel($"{selectedCount} of {arrangements.Count} selected", 20f, textFaint, false, TextAnchor.MiddleRight, false));
            arrangementList.Add(listHeader);

            ScrollView rowScroll = new ScrollView(ScrollViewMode.Vertical);
            ConfigureScrollView(rowScroll);
            rowScroll.style.maxHeight = 640f;
            rowScroll.style.minHeight = 0f;
            rowScroll.style.flexShrink = 0f;

            int visibleCount = 0;
            for (int i = 0; i < arrangements.Count; i++)
            {
                MusicXmlLoader.MusicXmlPartSummary summary = arrangements[i];
                if (summary == null || !selectedArrangementIndices.Contains(summary.Index))
                    continue;

                visibleCount++;
                int arrangementIndex = summary.Index;
                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 12f;
                row.style.paddingLeft = 22f;
                row.style.paddingRight = 16f;
                row.style.paddingTop = 18f;
                row.style.paddingBottom = 18f;
                row.style.backgroundColor = insetBackground;
                row.style.flexShrink = 0f;
                SetRadius(row, 14f);
                SetBorderWidth(row, 2f);
                SetBorderColor(row, new Color(0.75f, 0.72f, 0.82f, 0.20f));

                VisualElement text = new VisualElement();
                text.style.flexGrow = 1f;
                text.style.flexShrink = 1f;
                text.style.minWidth = 0f;
                Label nameLabel = CreateLabel(FormatArrangementTitle(summary), 26f, textPrimary, true, TextAnchor.MiddleLeft, false);
                nameLabel.style.whiteSpace = WhiteSpace.Normal;
                Label detailLabel = CreateLabel(FormatArrangementDetail(summary), 20f, textFaint, false, TextAnchor.MiddleLeft, false);
                detailLabel.style.whiteSpace = WhiteSpace.Normal;
                detailLabel.style.marginTop = 5f;
                text.Add(nameLabel);
                text.Add(detailLabel);
                row.Add(text);

                Button remove = CreateNewProjectIconButton(NewProjectIconKind.Cross, () =>
                {
                    selectedArrangementIndices.Remove(arrangementIndex);
                    RefreshArrangementList();
                    RefreshChartLabels();
                }, danger: true, size: 60f);
                remove.style.marginLeft = 18f;
                row.Add(remove);

                rowScroll.Add(row);
            }

            if (visibleCount == 0)
            {
                Label none = CreateLabel("No arrangements selected.", 22f, new Color(1f, 0.62f, 0.54f, 1f), false, TextAnchor.MiddleLeft, false);
                none.style.whiteSpace = WhiteSpace.Normal;
                arrangementList.Add(none);
            }
            else
            {
                arrangementList.Add(rowScroll);
            }
        }

        void RefreshChartLabels()
        {
            bool hasChart = !string.IsNullOrWhiteSpace(chartPath);
            if (selectChartButton != null)
                selectChartButton.text = hasChart || editExisting ? "Replace" : "Browse";
            if (clearChartButton != null)
                clearChartButton.style.display = hasChart ? DisplayStyle.Flex : DisplayStyle.None;

            if (!hasChart)
            {
                if (editExisting)
                {
                    bool hasCurrent = !string.IsNullOrEmpty(currentChartName);
                    chartPathLabel.text = hasCurrent ? currentChartName : "No chart file";
                    chartPathLabel.style.color = hasCurrent ? textPrimary : textMuted;
                    chartPathLabel.style.unityFontStyleAndWeight = hasCurrent ? FontStyle.Bold : FontStyle.Normal;
                    chartDetailLabel.text = "Current chart source. Replacing re-imports every track, note and tone from the new file.";
                }
                else
                {
                    chartPathLabel.text = "No chart selected";
                    chartPathLabel.style.color = textMuted;
                    chartPathLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                    chartDetailLabel.text = "Start blank, or import a Guitar Pro / MusicXML file.";
                }

                return;
            }

            int selectedCount = arrangements.Count(summary => summary != null && selectedArrangementIndices.Contains(summary.Index));
            chartPathLabel.text = Path.GetFileName(chartPath);
            chartPathLabel.style.color = textPrimary;
            chartPathLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            chartDetailLabel.text = editExisting
                ? $"{FormatChartKind(chartKind)}  ·  {selectedCount} of {arrangements.Count} arrangements  ·  Applying REPLACES all tracks, notes and tones. Your beat map, sections, audio and details are kept, and notes are re-aligned to your beat map."
                : $"{FormatChartKind(chartKind)}  ·  {selectedCount} of {arrangements.Count} arrangements selected";
        }

        void RefreshAudioLabels()
        {
            bool hasAudio = !string.IsNullOrWhiteSpace(audioPath);
            if (selectAudioButton != null)
                selectAudioButton.text = hasAudio || editExisting ? "Replace" : "Browse";
            if (clearAudioButton != null)
                clearAudioButton.style.display = hasAudio ? DisplayStyle.Flex : DisplayStyle.None;

            if (!hasAudio)
            {
                if (editExisting)
                {
                    bool hasCurrent = !string.IsNullOrEmpty(currentAudioName);
                    audioPathLabel.text = hasCurrent ? currentAudioName : "No audio file";
                    audioPathLabel.style.color = hasCurrent ? textPrimary : textMuted;
                    audioPathLabel.style.unityFontStyleAndWeight = hasCurrent ? FontStyle.Bold : FontStyle.Normal;
                    audioDetailLabel.text = "Current audio. Replacing swaps the file only — notes and the beat map keep their timing.";
                }
                else
                {
                    audioPathLabel.text = "No audio selected";
                    audioPathLabel.style.color = textMuted;
                    audioPathLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                    audioDetailLabel.text = "Audio can be added now or later.";
                }

                return;
            }

            audioPathLabel.text = Path.GetFileName(audioPath);
            audioPathLabel.style.color = textPrimary;
            audioPathLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            string extension = Path.GetExtension(audioPath)?.TrimStart('.').ToUpperInvariant();
            audioDetailLabel.text = editExisting
                ? $"{(string.IsNullOrEmpty(extension) ? "Audio file" : extension + " audio file")}  ·  Will replace the current audio. Notes and the beat map keep their timing — re-sync if this is a different mix or edit."
                : (string.IsNullOrEmpty(extension) ? "Audio file" : $"{extension} audio file");
        }

        void SelectChart()
        {
            if (!ChartEditorFilePicker.TryPickNotationFile(out string pickedPath))
                return;

            if (!ChartEditorImportService.TryReadNewProjectChartInfo(
                    pickedPath,
                    out SongNotationSourceKind detectedKind,
                    out List<MusicXmlLoader.MusicXmlPartSummary> detectedArrangements,
                    out string detectedTitle,
                    out string detectedArtist,
                    out string error))
            {
                SetDialogStatus(error, true);
                return;
            }

            chartPath = pickedPath;
            chartKind = detectedKind;
            arrangements = detectedArrangements ?? new List<MusicXmlLoader.MusicXmlPartSummary>();
            selectedArrangementIndices = new HashSet<int>(arrangements
                .Where(summary => summary != null)
                .Select(summary => summary.Index));

            bool shouldAutoFillTitle = string.IsNullOrWhiteSpace(titleField.value) ||
                                       string.Equals(titleField.value?.Trim() ?? string.Empty, lastAutoFilledTitle, StringComparison.Ordinal);
            bool shouldAutoFillArtist = string.IsNullOrWhiteSpace(artistField.value) ||
                                        string.Equals(artistField.value?.Trim() ?? string.Empty, lastAutoFilledArtist, StringComparison.Ordinal);
            if (shouldAutoFillTitle)
            {
                lastAutoFilledTitle = detectedTitle ?? string.Empty;
                titleField.value = lastAutoFilledTitle;
            }
            if (shouldAutoFillArtist)
            {
                lastAutoFilledArtist = detectedArtist ?? string.Empty;
                artistField.value = lastAutoFilledArtist;
            }

            SetDialogStatus(string.Empty, false);
            RefreshChartLabels();
            RefreshArrangementList();
        }

        void ClearChart()
        {
            chartPath = string.Empty;
            chartKind = SongNotationSourceKind.None;
            arrangements = new List<MusicXmlLoader.MusicXmlPartSummary>();
            selectedArrangementIndices = new HashSet<int>();
            SetDialogStatus(string.Empty, false);
            RefreshChartLabels();
            RefreshArrangementList();
        }

        void SelectAudio()
        {
            if (!ChartEditorFilePicker.TryPickAudioFile(out string pickedPath))
                return;

            audioPath = pickedPath;
            SetDialogStatus(string.Empty, false);
            RefreshAudioLabels();
        }

        void ClearAudio()
        {
            audioPath = string.Empty;
            SetDialogStatus(string.Empty, false);
            RefreshAudioLabels();
        }

        void CreateProject()
        {
            List<int> selectedPartIndices = arrangements
                .Where(summary => summary != null && selectedArrangementIndices.Contains(summary.Index))
                .Select(summary => summary.Index)
                .ToList();

            ChartEditorNewProjectRequest request = new ChartEditorNewProjectRequest
            {
                chartPath = chartPath,
                audioPath = audioPath,
                coverImagePath = coverImagePath,
                title = titleField.value?.Trim() ?? string.Empty,
                artist = artistField.value?.Trim() ?? string.Empty,
                album = albumField.value?.Trim() ?? string.Empty,
                genre = genreField.value?.Trim() ?? string.Empty,
                year = yearField.value?.Trim() ?? string.Empty,
                selectedPartIndices = selectedPartIndices
            };

            SetDialogStatus("Creating project...", false);
            RunWithEditorLoadingOverlay("Creating project...", () =>
            {
                if (ChartEditorImportService.CreateNewProject(request, out ChartEditorImportResult result, out string error))
                {
                    HideEditPopup();
                    AcceptImport(result, "Project created.");
                    return;
                }

                SetDialogStatus(error, true);
            });
        }

        void ApplyProjectSettings()
        {
            bool replaceChart = !string.IsNullOrWhiteSpace(chartPath);
            bool replaceAudio = !string.IsNullOrWhiteSpace(audioPath);
            List<int> selectedPartIndices = arrangements
                .Where(summary => summary != null && selectedArrangementIndices.Contains(summary.Index))
                .Select(summary => summary.Index)
                .ToList();

            if (replaceChart && selectedPartIndices.Count == 0)
            {
                SetDialogStatus("Select at least one arrangement to import from the new chart file.", true);
                return;
            }

            project.metadata.title = titleField.value?.Trim() ?? string.Empty;
            project.metadata.artist = artistField.value?.Trim() ?? string.Empty;
            project.metadata.album = albumField.value?.Trim() ?? string.Empty;
            project.metadata.genre = genreField.value?.Trim() ?? string.Empty;
            project.metadata.year = yearField.value?.Trim() ?? string.Empty;
            project.metadata.coverImagePath = coverImagePath?.Trim() ?? string.Empty;
            project.dirty = true;

            SetDialogStatus("Applying changes...", false);
            RunWithEditorLoadingOverlay("Applying project changes...", () =>
            {
                if (replaceChart && !ReplaceProjectChartFile(chartPath, selectedPartIndices, out string chartError))
                {
                    SetDialogStatus(chartError, true);
                    return;
                }

                if (replaceAudio && !ReplaceProjectAudioFile(audioPath, out string audioError))
                {
                    SetDialogStatus(audioError, true);
                    return;
                }

                HideEditPopup();
                Rebuild();
                SetStatus(replaceChart || replaceAudio ? "Project files updated." : "Project details updated.");
            });
        }

        VisualElement BuildCoverPicker()
        {
            VisualElement container = new VisualElement();
            container.style.flexShrink = 0f;
            container.style.width = 300f;

            VisualElement art = new VisualElement();
            art.style.width = 300f;
            art.style.height = 300f;
            art.style.alignItems = Align.Center;
            art.style.justifyContent = Justify.Center;
            art.style.overflow = Overflow.Hidden;
            art.style.backgroundColor = insetBackground;
            art.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            SetRadius(art, 18f);
            SetBorderWidth(art, 2f);
            SetBorderColor(art, new Color(0.75f, 0.72f, 0.82f, 0.26f));

            VisualElement emptyState = new VisualElement();
            emptyState.style.alignItems = Align.Center;
            emptyState.pickingMode = PickingMode.Ignore;
            VisualElement emptyIcon = CreateNewProjectIcon(NewProjectIconKind.Image, new Color(0.60f, 0.58f, 0.66f, 0.80f), 84f);
            emptyState.Add(emptyIcon);
            Label emptyLabel = CreateLabel("Add cover", 22f, textFaint, true, TextAnchor.MiddleCenter, false);
            emptyLabel.pickingMode = PickingMode.Ignore;
            emptyLabel.style.marginTop = 12f;
            emptyState.Add(emptyLabel);
            art.Add(emptyState);

            Texture2D previewTexture = null;

            void ReleasePreviewTexture()
            {
                if (previewTexture == null)
                    return;

                UnityEngine.Object.Destroy(previewTexture);
                previewTexture = null;
            }

            Label fileLabel = CreateLabel("No image selected", 20f, textFaint, false, TextAnchor.MiddleCenter, false);
            fileLabel.style.marginTop = 12f;
            fileLabel.style.width = 300f;
            fileLabel.style.overflow = Overflow.Hidden;
            fileLabel.style.whiteSpace = WhiteSpace.NoWrap;
            fileLabel.style.textOverflow = TextOverflow.Ellipsis;

            Button removeButton = null;

            void Refresh()
            {
                ReleasePreviewTexture();
                art.style.backgroundImage = StyleKeyword.None;

                string path = coverImagePath ?? string.Empty;
                bool hasImage = false;
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    try
                    {
                        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                        {
                            hideFlags = HideFlags.HideAndDontSave,
                            filterMode = FilterMode.Bilinear,
                            wrapMode = TextureWrapMode.Clamp
                        };

                        if (texture.LoadImage(File.ReadAllBytes(path)))
                        {
                            previewTexture = texture;
                            art.style.backgroundImage = new StyleBackground(previewTexture);
                            hasImage = true;
                        }
                        else
                        {
                            UnityEngine.Object.Destroy(texture);
                        }
                    }
                    catch
                    {
                        ReleasePreviewTexture();
                        art.style.backgroundImage = StyleKeyword.None;
                    }
                }

                bool hasPath = !string.IsNullOrWhiteSpace(path);
                fileLabel.text = hasPath ? Path.GetFileName(path) : "No image selected";
                emptyState.style.display = hasImage ? DisplayStyle.None : DisplayStyle.Flex;
                removeButton.style.display = hasPath ? DisplayStyle.Flex : DisplayStyle.None;
            }

            void ClearCover()
            {
                coverImagePath = string.Empty;
                Refresh();
            }

            removeButton = CreateNewProjectIconButton(NewProjectIconKind.Cross, ClearCover, danger: true, size: 56f, solid: true);
            removeButton.style.position = Position.Absolute;
            removeButton.style.top = 12f;
            removeButton.style.right = 12f;
            removeButton.style.display = DisplayStyle.None;
            art.Add(removeButton);

            art.RegisterCallback<ClickEvent>(evt =>
            {
                VisualElement target = evt.target as VisualElement;
                if (target != null && (target == removeButton || removeButton.Contains(target)))
                    return;

                if (!ChartEditorFilePicker.TryPickImageFile(out string path))
                    return;

                coverImagePath = path;
                Refresh();
            });
            art.RegisterCallback<MouseEnterEvent>(_ =>
            {
                SetBorderColor(art, new Color(accentPurple.r, accentPurple.g, accentPurple.b, 0.65f));
                art.style.backgroundColor = new Color(0.060f, 0.056f, 0.070f, 1f);
            });
            art.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                SetBorderColor(art, new Color(0.75f, 0.72f, 0.82f, 0.26f));
                art.style.backgroundColor = insetBackground;
            });
            container.RegisterCallback<DetachFromPanelEvent>(_ => ReleasePreviewTexture());

            container.Add(art);
            container.Add(fileLabel);
            Refresh();
            return container;
        }

        selectChartButton = CreateNewProjectGhostButton("Browse", SelectChart);
        clearChartButton = CreateNewProjectIconButton(NewProjectIconKind.Cross, ClearChart, danger: true, size: 72f);
        selectAudioButton = CreateNewProjectGhostButton("Browse", SelectAudio);
        clearAudioButton = CreateNewProjectIconButton(NewProjectIconKind.Cross, ClearAudio, danger: true, size: 72f);
        RefreshChartLabels();
        RefreshAudioLabels();

        Button CreateFooterButton(string text, Action action, bool primary)
        {
            Button button = new Button(action) { text = text ?? string.Empty };
            button.focusable = false;
            button.style.unityFontDefinition = bodyFont;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.fontSize = 31f;
            button.style.height = 92f;
            button.style.minWidth = primary ? 380f : 230f;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.paddingLeft = 44f;
            button.style.paddingRight = 44f;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            SetRadius(button, 16f);

            void Apply(bool hover)
            {
                if (primary)
                {
                    button.style.backgroundColor = hover ? Color.Lerp(accentPurple, Color.white, 0.12f) : accentPurple;
                    SetBorderWidth(button, 2f);
                    SetBorderColor(button, Color.Lerp(accentPurple, Color.white, hover ? 0.50f : 0.30f));
                    button.style.color = Color.white;
                }
                else
                {
                    button.style.backgroundColor = hover ? new Color(1f, 1f, 1f, 0.10f) : new Color(1f, 1f, 1f, 0.03f);
                    SetBorderWidth(button, 2f);
                    SetBorderColor(button, hover ? new Color(0.84f, 0.82f, 0.92f, 0.60f) : new Color(0.80f, 0.78f, 0.88f, 0.30f));
                    button.style.color = hover ? Color.white : new Color(0.92f, 0.91f, 0.95f, 1f);
                }

                button.style.scale = hover ? new Scale(new Vector3(1.02f, 1.02f, 1f)) : new Scale(Vector3.one);
            }

            Apply(false);
            button.RegisterCallback<MouseEnterEvent>(_ => Apply(true));
            button.RegisterCallback<MouseLeaveEvent>(_ => Apply(false));
            return button;
        }

        VisualElement overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.right = 0f;
        overlay.style.top = 0f;
        overlay.style.bottom = 0f;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.backgroundColor = new Color(0.004f, 0.004f, 0.006f, 0.72f);
        overlay.pickingMode = PickingMode.Position;
        overlay.RegisterCallback<PointerDownEvent>(evt =>
        {
            HideEditPopup();
            evt.StopPropagation();
        });

        VisualElement panel = new VisualElement();
        panel.style.width = 2280f;
        panel.style.maxWidth = Length.Percent(94f);
        panel.style.maxHeight = Length.Percent(92f);
        panel.style.flexDirection = FlexDirection.Column;
        panel.style.backgroundColor = panelBackground;
        panel.style.overflow = Overflow.Hidden;
        SetRadius(panel, 26f);
        SetBorderWidth(panel, 3f);
        SetBorderColor(panel, new Color(0.78f, 0.75f, 0.88f, 0.36f));
        panel.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

        VisualElement headerBand = new VisualElement();
        headerBand.style.flexDirection = FlexDirection.Row;
        headerBand.style.alignItems = Align.Center;
        headerBand.style.flexShrink = 0f;
        headerBand.style.backgroundColor = bandBackground;
        headerBand.style.paddingLeft = 44f;
        headerBand.style.paddingRight = 36f;
        headerBand.style.paddingTop = 34f;
        headerBand.style.paddingBottom = 34f;
        headerBand.style.borderBottomWidth = 2f;
        headerBand.style.borderBottomColor = hairline;

        headerBand.Add(CreateNewProjectIconTile(NewProjectIconKind.Note, accentPurple, 92f));

        VisualElement headerText = new VisualElement();
        headerText.style.marginLeft = 26f;
        headerText.style.flexGrow = 1f;
        headerText.style.flexShrink = 1f;
        headerText.style.minWidth = 0f;
        Label title = CreateLabel(editExisting ? "Project Settings" : "New Project", 42f, textPrimary, true, TextAnchor.MiddleLeft, false);
        headerText.Add(title);
        Label subtitle = CreateLabel(
            editExisting
                ? "Update the song details, or replace the chart and audio files."
                : "Import a chart and audio, or start from a blank project.",
            22f, textMuted, false, TextAnchor.MiddleLeft, false);
        subtitle.style.whiteSpace = WhiteSpace.Normal;
        subtitle.style.marginTop = 6f;
        headerText.Add(subtitle);
        headerBand.Add(headerText);

        Button closeButton = CreateNewProjectIconButton(NewProjectIconKind.Cross, HideEditPopup, danger: false, size: 76f);
        closeButton.style.marginLeft = 20f;
        headerBand.Add(closeButton);
        panel.Add(headerBand);

        ScrollView body = new ScrollView(ScrollViewMode.Vertical);
        ConfigureScrollView(body);
        body.style.flexGrow = 1f;
        body.style.minHeight = 0f;
        body.contentContainer.style.paddingLeft = 44f;
        body.contentContainer.style.paddingRight = 44f;
        body.contentContainer.style.paddingTop = 40f;
        body.contentContainer.style.paddingBottom = 40f;
        body.contentContainer.style.flexShrink = 0f;

        VisualElement chartCard = BuildFileCard(NewProjectIconKind.Note, accentBlue, "Chart", editExisting ? "Current" : "Optional", chartPathLabel, chartDetailLabel, selectChartButton, clearChartButton, arrangementList);
        VisualElement audioCard = BuildFileCard(NewProjectIconKind.Waveform, accentGreen, "Audio", editExisting ? "Current" : "Optional", audioPathLabel, audioDetailLabel, selectAudioButton, clearAudioButton, null);
        audioCard.style.marginBottom = 0f;

        VisualElement detailsCard = CreateCard();
        detailsCard.style.marginBottom = 0f;

        VisualElement detailsHeader = new VisualElement();
        detailsHeader.style.flexDirection = FlexDirection.Row;
        detailsHeader.style.alignItems = Align.Center;
        detailsHeader.Add(CreateNewProjectIconTile(NewProjectIconKind.Image, accentPurple, 68f));

        VisualElement detailsInfo = new VisualElement();
        detailsInfo.style.flexGrow = 1f;
        detailsInfo.style.flexShrink = 1f;
        detailsInfo.style.minWidth = 0f;
        detailsInfo.style.marginLeft = 20f;
        detailsInfo.Add(CreateLabel("Song Details", 31f, textPrimary, true, TextAnchor.MiddleLeft, false));
        Label detailsHint = CreateLabel("Shown in the song library. Everything can be edited later.", 21f, textFaint, false, TextAnchor.MiddleLeft, false);
        detailsHint.style.whiteSpace = WhiteSpace.Normal;
        detailsHint.style.marginTop = 5f;
        detailsInfo.Add(detailsHint);
        detailsHeader.Add(detailsInfo);
        detailsCard.Add(detailsHeader);

        VisualElement detailsBody = new VisualElement();
        detailsBody.style.marginTop = 30f;

        VisualElement coverRow = new VisualElement();
        coverRow.style.flexDirection = FlexDirection.Row;
        coverRow.style.alignItems = Align.FlexStart;
        coverRow.Add(BuildCoverPicker());

        VisualElement titleArtistColumn = new VisualElement();
        titleArtistColumn.style.flexGrow = 1f;
        titleArtistColumn.style.flexShrink = 1f;
        titleArtistColumn.style.minWidth = 0f;
        titleArtistColumn.style.marginLeft = 32f;
        titleArtistColumn.Add(titleField);
        artistField.style.marginBottom = 0f;
        titleArtistColumn.Add(artistField);
        coverRow.Add(titleArtistColumn);
        detailsBody.Add(coverRow);

        albumField.style.marginTop = 28f;
        detailsBody.Add(albumField);

        VisualElement genreYearRow = new VisualElement();
        genreYearRow.style.flexDirection = FlexDirection.Row;
        genreField.style.flexGrow = 1f;
        genreField.style.flexShrink = 1f;
        genreField.style.flexBasis = 0f;
        genreField.style.minWidth = 0f;
        genreField.style.marginRight = 24f;
        genreField.style.marginBottom = 0f;
        yearField.style.width = 300f;
        yearField.style.flexShrink = 0f;
        yearField.style.marginBottom = 0f;
        genreYearRow.Add(genreField);
        genreYearRow.Add(yearField);
        detailsBody.Add(genreYearRow);
        detailsCard.Add(detailsBody);

        VisualElement columns = new VisualElement();
        columns.style.flexDirection = FlexDirection.Row;
        columns.style.alignItems = Align.FlexStart;
        columns.style.flexShrink = 0f;

        VisualElement filesColumn = new VisualElement();
        filesColumn.style.flexGrow = 1f;
        filesColumn.style.flexShrink = 1f;
        filesColumn.style.flexBasis = 0f;
        filesColumn.style.minWidth = 0f;
        filesColumn.style.marginRight = 36f;
        filesColumn.Add(chartCard);
        filesColumn.Add(audioCard);

        VisualElement detailsColumn = new VisualElement();
        detailsColumn.style.flexGrow = 1f;
        detailsColumn.style.flexShrink = 1f;
        detailsColumn.style.flexBasis = 0f;
        detailsColumn.style.minWidth = 0f;
        detailsColumn.Add(detailsCard);

        columns.Add(filesColumn);
        columns.Add(detailsColumn);
        body.Add(columns);
        panel.Add(body);

        bool stackedLayout = false;
        bool layoutInitialized = false;
        void ApplyResponsiveLayout(float panelWidth)
        {
            bool stacked = panelWidth < 1720f;
            if (layoutInitialized && stacked == stackedLayout)
                return;

            layoutInitialized = true;
            stackedLayout = stacked;
            columns.style.flexDirection = stacked ? FlexDirection.Column : FlexDirection.Row;
            columns.style.alignItems = stacked ? Align.Stretch : Align.FlexStart;
            filesColumn.style.marginRight = stacked ? 0f : 36f;
            filesColumn.style.marginBottom = stacked ? 28f : 0f;
            filesColumn.style.flexShrink = stacked ? 0f : 1f;
            detailsColumn.style.flexShrink = stacked ? 0f : 1f;
            filesColumn.style.flexBasis = stacked ? new StyleLength(StyleKeyword.Auto) : new StyleLength(0f);
            detailsColumn.style.flexBasis = stacked ? new StyleLength(StyleKeyword.Auto) : new StyleLength(0f);
        }
        panel.RegisterCallback<GeometryChangedEvent>(evt => ApplyResponsiveLayout(evt.newRect.width));

        VisualElement footerBand = new VisualElement();
        footerBand.style.flexDirection = FlexDirection.Row;
        footerBand.style.alignItems = Align.Center;
        footerBand.style.justifyContent = Justify.FlexEnd;
        footerBand.style.flexShrink = 0f;
        footerBand.style.backgroundColor = bandBackground;
        footerBand.style.paddingLeft = 44f;
        footerBand.style.paddingRight = 44f;
        footerBand.style.paddingTop = 28f;
        footerBand.style.paddingBottom = 28f;
        footerBand.style.borderTopWidth = 2f;
        footerBand.style.borderTopColor = hairline;
        footerBand.Add(statusLabel);
        Button cancel = CreateFooterButton("Cancel", HideEditPopup, primary: false);
        Button create = CreateFooterButton(
            editExisting ? "Apply Changes" : "Create Project",
            editExisting ? ApplyProjectSettings : (Action)CreateProject,
            primary: true);
        cancel.style.marginRight = 20f;
        footerBand.Add(cancel);
        footerBand.Add(create);
        panel.Add(footerBand);

        panel.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity"), new StylePropertyName("translate") };
        panel.style.transitionDuration = new List<TimeValue> { new TimeValue(180, TimeUnit.Millisecond), new TimeValue(180, TimeUnit.Millisecond) };
        panel.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic), new EasingFunction(EasingMode.EaseOutCubic) };
        panel.style.opacity = 0f;
        panel.style.translate = new Translate(0f, 18f);

        overlay.Add(panel);
        editPopupElement = overlay;
        editPopupKind = ChartEditorPopupKind.Generic;
        RootElement.Add(editPopupElement);
        editPopupElement.BringToFront();
        SetChartEditorKeyboardCaptureActive(true);

        panel.schedule.Execute(() =>
        {
            panel.style.opacity = 1f;
            panel.style.translate = new Translate(0f, 0f);
        }).ExecuteLater(30);

        RefreshChartLabels();
        RefreshAudioLabels();
        RefreshArrangementList();
        titleField.schedule.Execute(() => titleField.Focus());
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

        RunWithEditorLoadingOverlay("Importing chart...", () =>
        {
            if (ChartEditorImportService.ImportChartAndAudio(chartPath, audioPath, out ChartEditorImportResult result, out string error))
                AcceptImport(result, "Chart imported.");
            else
                SetStatus(error);
        });
    }

    private void ImportTheoryPackage()
    {
        if (!ChartEditorFilePicker.TryPickTheoryPackageFile(out string packagePath))
            return;

        ImportTheoryPackage(packagePath);
    }

    private void ImportTheoryPackage(string packagePath)
    {
        RunWithEditorLoadingOverlay("Opening theory package...", () =>
        {
            if (ChartEditorImportService.ImportTheoryPackage(packagePath, out ChartEditorImportResult result, out string error))
                AcceptImport(result, "Theory package imported.");
            else
                SetStatus(error);
        });
    }

    private void ShowEditorLoadingOverlay(string message)
    {
        HideEditorLoadingOverlay();

        VisualElement overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.right = 0f;
        overlay.style.top = 0f;
        overlay.style.bottom = 0f;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.backgroundColor = new Color(0.004f, 0.004f, 0.006f, 0.84f);
        overlay.pickingMode = PickingMode.Position;

        VisualElement card = new VisualElement();
        card.style.alignItems = Align.Center;
        card.pickingMode = PickingMode.Ignore;

        Color accent = new Color(0.62f, 0.38f, 1f, 1f);
        VisualElement spinner = new VisualElement();
        spinner.style.width = 96f;
        spinner.style.height = 96f;
        spinner.pickingMode = PickingMode.Ignore;
        spinner.generateVisualContent += context =>
        {
            Rect rect = context.visualElement.contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            Painter2D painter = context.painter2D;
            Vector2 center = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
            float radius = Mathf.Min(rect.width, rect.height) * 0.5f - 7f;
            painter.lineWidth = 10f;
            painter.lineCap = LineCap.Round;
            painter.strokeColor = new Color(accent.r, accent.g, accent.b, 0.16f);
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Stroke();
            painter.strokeColor = accent;
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 100f);
            painter.Stroke();
        };
        float spinAngle = 0f;
        spinner.schedule.Execute(() =>
        {
            spinAngle = (spinAngle + 7f) % 360f;
            spinner.style.rotate = new Rotate(spinAngle);
        }).Every(16);
        card.Add(spinner);

        Label messageLabel = CreateLabel(string.IsNullOrWhiteSpace(message) ? "Loading..." : message, 30f, new Color(0.97f, 0.96f, 0.98f, 1f), true, TextAnchor.MiddleCenter, false);
        messageLabel.style.marginTop = 30f;
        card.Add(messageLabel);

        Label hintLabel = CreateLabel("This can take a moment for large songs.", 20f, new Color(0.60f, 0.58f, 0.66f, 1f), false, TextAnchor.MiddleCenter, false);
        hintLabel.style.marginTop = 8f;
        card.Add(hintLabel);

        overlay.Add(card);
        RootElement.Add(overlay);
        overlay.BringToFront();
        editorLoadingOverlay = overlay;
    }

    private void HideEditorLoadingOverlay()
    {
        editorLoadingOverlay?.RemoveFromHierarchy();
        editorLoadingOverlay = null;
    }

    private void RunWithEditorLoadingOverlay(string message, Action operation)
    {
        ShowEditorLoadingOverlay(message);
        RootElement.schedule.Execute(() =>
        {
            try
            {
                operation?.Invoke();
            }
            finally
            {
                HideEditorLoadingOverlay();
            }
        }).ExecuteLater(50);
    }

    private void ImportExternalImporterFile(string importerId)
    {
        if (!SongImporterRegistry.TryGetImporterById(importerId, out SongImporterDescriptor importer))
        {
            SetStatus("Importer is no longer available.");
            return;
        }

        if (!SongImporterRegistry.ImporterHasUsableEntrypoint(importer))
        {
            SetStatus($"{importer.DisplayName} importer is missing its executable for this platform.");
            return;
        }

        if (!ChartEditorFilePicker.TryPickImporterSourceFile(importer, out string sourcePath))
            return;

        SetStatus($"Importing {importer.DisplayName}...");
        RunWithEditorLoadingOverlay($"Importing {importer.DisplayName}...", () =>
        {
            if (ChartEditorImportService.ImportExternalImporterSource(sourcePath, out ChartEditorImportResult result, out string error, importer.Id))
                AcceptImport(result, $"{importer.DisplayName} imported.");
            else
                SetStatus(error);
        });
    }

    private void ImportExternalImporterFolder(string importerId, int signatureIndex)
    {
        if (!SongImporterRegistry.TryGetImporterById(importerId, out SongImporterDescriptor importer))
        {
            SetStatus("Importer is no longer available.");
            return;
        }

        if (!SongImporterRegistry.ImporterHasUsableEntrypoint(importer))
        {
            SetStatus($"{importer.DisplayName} importer is missing its executable for this platform.");
            return;
        }

        if (importer.FolderSignatures == null ||
            signatureIndex < 0 ||
            signatureIndex >= importer.FolderSignatures.Count)
        {
            SetStatus("Importer folder type is no longer available.");
            return;
        }

        SongImporterFolderSignature signature = importer.FolderSignatures[signatureIndex];
        if (!ChartEditorFilePicker.TryPickImporterSourceFolder(importer, signature, out string sourcePath))
            return;

        string sourceLabel = FirstNonEmpty(signature?.displayName, importer.DisplayName);
        SetStatus($"Importing {sourceLabel}...");
        RunWithEditorLoadingOverlay($"Importing {sourceLabel}...", () =>
        {
            if (ChartEditorImportService.ImportExternalImporterSource(sourcePath, out ChartEditorImportResult result, out string error, importer.Id))
                AcceptImport(result, $"{sourceLabel} imported.");
            else
                SetStatus(error);
        });
    }

    private void ImportFolder()
    {
        if (!ChartEditorFilePicker.TryPickFolder("Open Unpacked Chart Folder", ExternalContentPaths.PersistentSongsDirectory, out string folderPath))
            return;

        RunWithEditorLoadingOverlay("Opening chart folder...", () =>
        {
            if (ChartEditorImportService.ImportFolder(folderPath, out ChartEditorImportResult result, out string error))
                AcceptImport(result, "Folder imported.");
            else
                SetStatus(error);
        });
    }

    private void OpenExistingProject()
    {
        if (!ChartEditorFilePicker.TryPickProjectFile(out string path))
            return;

        RunWithEditorLoadingOverlay("Opening project...", () => OpenExistingProjectFromPath(path));
    }

    private void OpenExistingProjectFromPath(string path)
    {
        if (ChartEditorProjectStore.LoadProject(path, out ChartEditorProject loaded, out string error))
        {
            project = loaded;
            ChartEditorTimingService.EnsureBeatMap(project, attachContentToBeatMap: true);
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

    private void ShowSaveOptionsPopup(bool closeAfterSave = false)
    {
        if (project == null)
            return;

        HideContextMenu();
        HideEditPopup();
        closeAfterSuccessfulSave = closeAfterSave;

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
        StyleStrongPopupPanel(panel, new Color(0.030f, 0.036f, 0.048f, 1f), 18f);
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

        Button openFolder = CreatePopupDialogButton("Open Save Folder", () => OpenTheoryPackageSaveFolder(saveFolder), new Color(0.70f, 0.78f, 0.88f, 1f));
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
        Button cancel = CreatePopupDialogButton("Cancel", HideEditPopup, new Color(0.70f, 0.78f, 0.88f, 1f));
        cancel.style.minWidth = 132f;
        cancel.style.height = 52f;
        footer.Add(cancel);
        panel.Add(footer);

        overlay.Add(panel);
        editPopupElement = overlay;
        editPopupKind = ChartEditorPopupKind.SaveOptions;
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
        SetBorderWidth(button, 2f);
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
            bool shouldClose = closeAfterSuccessfulSave;
            closeAfterSuccessfulSave = false;
            HideEditPopup();
            owner?.NotifyChartEditorLibraryChangedFromUi(packagePath);
            SetStatus(saveAs ? $"Saved new .theory chart: {packagePath}" : $"Saved .theory chart: {packagePath}");
            if (shouldClose)
            {
                owner?.CloseChartEditorToMainMenuFromUi();
                return;
            }

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

        double safeTime = Math.Max(0.0, Math.Min(GetProjectDurationSeconds(), timeSeconds));
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
        double safeTime = Math.Max(0.0, Math.Min(GetProjectDurationSeconds(), timeSeconds));
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
        double safeTime = Math.Max(0.0, Math.Min(GetProjectDurationSeconds(), timeSeconds));
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
        QueueTimelineRefresh();
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
        QueueTimelineRefresh();
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
            note.timeSeconds = Math.Max(0.0, Math.Min(GetProjectDurationSeconds(), note.timeSeconds + delta));
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
            double quantizedTime = Math.Max(0.0, Math.Min(GetProjectDurationSeconds(), ChartEditorTimingService.GetAudioTimeForBeat(project, beat)));
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
        // Paste always targets a single track, so cross-track selections must
        // not feed foreign-role notes (e.g. drum lanes) into the clipboard.
        ChartEditorTrack copySourceTrack = project?.SelectedTrack;
        List<ChartEditorNoteReference> refs = GetSelectedNoteReferences()
            .Where(noteRef => noteRef?.note != null &&
                              (copySourceTrack == null || ReferenceEquals(noteRef.track, copySourceTrack)))
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
            clone.timeSeconds = Math.Max(0.0, Math.Min(GetProjectDurationSeconds(), clone.timeSeconds + 0.1));
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

    private Button CreateHeaderIconButton(NewProjectIconKind icon, Action action, bool primary, bool grouped = false)
    {
        Button button = new Button(action) { text = string.Empty };
        button.focusable = false;
        button.style.width = primary ? 84f : 68f;
        button.style.minWidth = primary ? 84f : 68f;
        button.style.height = 68f;
        button.style.marginLeft = grouped ? 4f : 8f;
        button.style.marginRight = grouped ? 4f : 8f;
        button.style.paddingLeft = 0f;
        button.style.paddingRight = 0f;
        button.style.paddingTop = 0f;
        button.style.paddingBottom = 0f;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.Center;
        SetRadius(button, 12f);

        Color iconColor = primary ? Color.white : new Color(0.83f, 0.82f, 0.88f, 0.95f);
        button.Add(CreateNewProjectIcon(icon, iconColor, 68f * 0.48f));

        void Apply(bool hover)
        {
            if (primary)
            {
                Color accent = new Color(0.62f, 0.38f, 1f, 1f);
                button.style.backgroundColor = hover ? Color.Lerp(accent, Color.white, 0.12f) : accent;
                SetBorderWidth(button, 2f);
                SetBorderColor(button, Color.Lerp(accent, Color.white, hover ? 0.45f : 0.28f));
            }
            else if (grouped)
            {
                button.style.backgroundColor = hover ? new Color(1f, 1f, 1f, 0.09f) : Color.clear;
                SetBorderWidth(button, 0f);
            }
            else
            {
                button.style.backgroundColor = hover ? new Color(1f, 1f, 1f, 0.09f) : new Color(1f, 1f, 1f, 0.03f);
                SetBorderWidth(button, 2f);
                SetBorderColor(button, hover ? new Color(0.80f, 0.78f, 0.88f, 0.55f) : new Color(0.75f, 0.72f, 0.82f, 0.22f));
            }
        }

        Apply(false);
        button.RegisterCallback<MouseEnterEvent>(_ => Apply(true));
        button.RegisterCallback<MouseLeaveEvent>(_ => Apply(false));
        return button;
    }

    private void UpdateTransportPlayButtonIcon()
    {
        if (transportPlayIcon != null)
            transportPlayIcon.style.display = editorPlaying ? DisplayStyle.None : DisplayStyle.Flex;
        if (transportPauseIcon != null)
            transportPauseIcon.style.display = editorPlaying ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private Button CreateHeaderActionButton(string text, Action action, bool primary)
    {
        Button button = new Button(action) { text = text };
        button.focusable = false;
        button.style.height = 68f;
        button.style.minWidth = primary ? 210f : 190f;
        button.style.marginLeft = 10f;
        button.style.marginRight = 10f;
        button.style.paddingLeft = 30f;
        button.style.paddingRight = 30f;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.unityFontDefinition = bodyFont;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.fontSize = UiFont(24f);
        SetRadius(button, 14f);

        void Apply(bool hover)
        {
            if (primary)
            {
                Color accent = new Color(0.62f, 0.38f, 1f, 1f);
                button.style.backgroundColor = hover ? Color.Lerp(accent, Color.white, 0.12f) : accent;
                SetBorderWidth(button, 2f);
                SetBorderColor(button, Color.Lerp(accent, Color.white, hover ? 0.45f : 0.28f));
                button.style.color = Color.white;
            }
            else
            {
                button.style.backgroundColor = hover ? new Color(1f, 1f, 1f, 0.09f) : new Color(1f, 1f, 1f, 0.03f);
                SetBorderWidth(button, 2f);
                SetBorderColor(button, hover ? new Color(0.80f, 0.78f, 0.88f, 0.55f) : new Color(0.75f, 0.72f, 0.82f, 0.22f));
                button.style.color = hover ? Color.white : new Color(0.91f, 0.90f, 0.94f, 1f);
            }

            button.style.scale = hover ? new Scale(new Vector3(1.02f, 1.02f, 1f)) : new Scale(Vector3.one);
        }

        Apply(false);
        button.RegisterCallback<MouseEnterEvent>(_ => Apply(true));
        button.RegisterCallback<MouseLeaveEvent>(_ => Apply(false));
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
        const float thickness = 12f;
        const float radius = thickness * 0.5f;

        VisualElement indicator = new VisualElement();
        indicator.name = name;
        indicator.style.position = Position.Absolute;
        indicator.style.width = vertical ? thickness : 72f;
        indicator.style.height = vertical ? 72f : thickness;
        indicator.style.minWidth = vertical ? thickness : 48f;
        indicator.style.minHeight = vertical ? 48f : thickness;
        indicator.style.right = vertical ? 8f : StyleKeyword.Auto;
        indicator.style.bottom = vertical ? StyleKeyword.Auto : 8f;
        indicator.style.backgroundColor = new Color(0.92f, 0.95f, 1f, 0.64f);
        indicator.style.borderTopLeftRadius = radius;
        indicator.style.borderTopRightRadius = radius;
        indicator.style.borderBottomLeftRadius = radius;
        indicator.style.borderBottomRightRadius = radius;
        indicator.style.borderTopWidth = 0f;
        indicator.style.borderRightWidth = 0f;
        indicator.style.borderBottomWidth = 0f;
        indicator.style.borderLeftWidth = 0f;
        indicator.style.opacity = 1f;
        indicator.style.overflow = Overflow.Hidden;
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

            indicator.style.opacity = 1f;
            UpdateIosScrollIndicator(view, indicator, vertical);
            evt.StopImmediatePropagation();
        });
    }

    private static void UpdateIosScrollIndicator(ScrollView view, VisualElement indicator, bool vertical)
    {
        const float thickness = 12f;
        const float radius = thickness * 0.5f;

        if (view == null || indicator == null || view.contentViewport == null || view.contentContainer == null)
            return;

        if (!TryGetIosScrollMetrics(view, vertical, out float maxScroll, out float trackTravel, out float thumbSize, out float viewportSize))
        {
            indicator.style.display = DisplayStyle.None;
            return;
        }

        float inset = 8f;
        float scroll = vertical ? view.scrollOffset.y : view.scrollOffset.x;
        float position = Mathf.Round(inset + Mathf.Clamp01(scroll / maxScroll) * trackTravel);
        float roundedThumbSize = Mathf.Round(thumbSize);

        indicator.style.display = DisplayStyle.Flex;
        indicator.style.opacity = 1f;
        if (vertical)
        {
            indicator.style.top = position;
            indicator.style.right = 8f;
            indicator.style.width = thickness;
            indicator.style.height = roundedThumbSize;
            SetRadius(indicator, radius);
        }
        else
        {
            indicator.style.left = position;
            indicator.style.bottom = 8f;
            indicator.style.width = roundedThumbSize;
            indicator.style.height = thickness;
            SetRadius(indicator, radius);
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

        float inset = 8f;
        float trackSize = Mathf.Max(1f, viewportSize - inset * 2f);
        thumbSize = Mathf.Clamp(viewportSize / contentSize * trackSize, vertical ? 52f : 48f, trackSize);
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
        label.style.backgroundColor = new Color(0.038f, 0.036f, 0.044f, 1f);
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
            .Where(noteRef => noteRef?.note != null && !IsDrumTrack(noteRef.track))
            .ToList() ?? new List<ChartEditorNoteReference>();
        if (refs.Count == 0)
            return Array.Empty<ContextMenuItem>();

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
            .Where(noteRef => noteRef?.note != null && !IsDrumTrack(noteRef.track))
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
        float projectLimit = Mathf.Max(TechniqueSegmentMinimumSeconds, (float)((project != null ? GetProjectDurationSeconds() : note.timeSeconds + start + 1.0) - note.timeSeconds));
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
            .Where(noteRef => noteRef?.note != null && !IsDrumTrack(noteRef.track))
            .ToList() ?? new List<ChartEditorNoteReference>();
        if (refs.Count == 0)
            return;

        for (int i = 0; i < refs.Count; i++)
        {
            ChartEditorNote note = refs[i].note;
            if (note?.techniqueSegments?.Any(segment => segment != null &&
                (segment.type == NoteTechniqueSegmentType.Bend || IsBendBearingTechniqueSegment(segment))) == true)
                ClearBendPoints(note);
            note?.techniqueSegments?.Clear();
            ApplyTechniqueSegmentSummaries(note);
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
        note.hasRuntimeMuted = true;
        note.runtimeMuted = note.muted;
        note.hasRuntimePalmMute = true;
        note.runtimePalmMute = note.palmMute;
    }

    private static void SetFretHandMute(ChartEditorNote note, bool enabled)
    {
        if (note == null)
            return;

        note.fretHandMute = enabled;
        if (enabled)
            note.palmMute = false;
        note.muted = note.palmMute || note.fretHandMute;
        note.hasRuntimeMuted = true;
        note.runtimeMuted = note.muted;
        note.hasRuntimePalmMute = true;
        note.runtimePalmMute = note.palmMute;
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
        double maxDuration = refs.Min(noteRef => Math.Max(0.01, (project != null ? GetProjectDurationSeconds() : defaultDuration) - noteRef.note.timeSeconds));

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
        refs = refs?
            .Where(noteRef => noteRef?.note != null && !IsDrumTrack(noteRef.track))
            .ToList() ?? new List<ChartEditorNoteReference>();
        if (refs.Count == 0)
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

        if (note.slideTargetFret >= 0 || HasTechniqueSegment(note, NoteTechniqueSegmentType.Slide))
        {
            note.technique = NoteTechnique.Slide;
            return;
        }

        if (Mathf.Abs(note.bendStep) > 0.01f ||
            note.bendPreBend ||
            note.bendRelease ||
            HasTechniqueSegment(note, NoteTechniqueSegmentType.Bend) ||
            HasBendBearingTechniqueSegment(note))
        {
            note.technique = NoteTechnique.Bend;
            return;
        }

        if (HasTechniqueSegment(note, NoteTechniqueSegmentType.Vibrato))
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
            new Color(0.16f, 0.155f, 0.19f, 0.96f));
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
        field.style.backgroundColor = new Color(0.024f, 0.023f, 0.028f, 0.98f);
        field.style.borderTopWidth = 1f;
        field.style.borderRightWidth = 1f;
        field.style.borderBottomWidth = 1f;
        field.style.borderLeftWidth = 1f;
        SetToneLabBorder(field,
            new Color(0.42f, 0.41f, 0.45f, 0.84f),
            new Color(0.25f, 0.24f, 0.28f, 0.95f),
            new Color(0.16f, 0.155f, 0.185f, 1f));
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

    private Button CreatePopupDialogButton(string text, Action action, Color accent, bool filled = false)
    {
        Button button = new Button(action) { text = text ?? string.Empty };
        button.focusable = false;
        button.style.unityFontDefinition = bodyFont;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.fontSize = UiFont(22f);
        button.style.height = 54f;
        button.style.minWidth = 146f;
        button.style.marginLeft = 0f;
        button.style.marginRight = 0f;
        button.style.marginTop = 0f;
        button.style.marginBottom = 0f;
        button.style.paddingLeft = 22f;
        button.style.paddingRight = 22f;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        SetRadius(button, 11f);
        ApplyPopupDialogButtonState(button, accent, filled, hover: false);
        button.RegisterCallback<MouseEnterEvent>(_ => ApplyPopupDialogButtonState(button, accent, filled, hover: true));
        button.RegisterCallback<MouseLeaveEvent>(_ => ApplyPopupDialogButtonState(button, accent, filled, hover: false));
        return button;
    }

    private static void ApplyPopupDialogButtonState(Button button, Color accent, bool filled, bool hover)
    {
        if (button == null)
            return;

        Color background = filled
            ? (hover ? Color.Lerp(accent, Color.white, 0.10f) : accent)
            : (hover ? new Color(accent.r, accent.g, accent.b, 0.16f) : new Color(0.044f, 0.052f, 0.068f, 0.96f));
        Color border = filled
            ? Color.Lerp(accent, Color.white, hover ? 0.46f : 0.30f)
            : new Color(accent.r, accent.g, accent.b, hover ? 0.98f : 0.78f);

        button.style.backgroundColor = background;
        button.style.color = Color.white;
        SetBorderWidth(button, 2f);
        SetBorderColor(button, border);
        button.style.opacity = hover ? 1f : 0.98f;
        button.style.scale = hover ? new Scale(new Vector3(1.01f, 1.01f, 1f)) : new Scale(Vector3.one);
    }

    private enum NewProjectIconKind
    {
        Note,
        Waveform,
        Image,
        Cross,
        Plus,
        Folder,
        File,
        ChevronRight,
        Play,
        Pause,
        Stop,
        SkipStart,
        SkipEnd,
        Menu,
        Gear
    }

    private static VisualElement CreateNewProjectIcon(NewProjectIconKind kind, Color color, float size)
    {
        VisualElement icon = new VisualElement();
        icon.style.width = size;
        icon.style.height = size;
        icon.style.minWidth = size;
        icon.style.minHeight = size;
        icon.style.flexShrink = 0f;
        icon.pickingMode = PickingMode.Ignore;
        icon.generateVisualContent += context => DrawNewProjectIcon(context, kind, color);
        return icon;
    }

    private static void DrawNewProjectIcon(MeshGenerationContext context, NewProjectIconKind kind, Color color)
    {
        Rect rect = context.visualElement.contentRect;
        float w = rect.width;
        float h = rect.height;
        if (w <= 1f || h <= 1f)
            return;

        Painter2D painter = context.painter2D;
        float unit = Mathf.Min(w, h);
        painter.lineWidth = Mathf.Max(1.5f, unit * 0.09f);
        painter.strokeColor = color;
        painter.fillColor = color;
        painter.lineCap = LineCap.Round;
        painter.lineJoin = LineJoin.Round;

        switch (kind)
        {
            case NewProjectIconKind.Note:
            {
                float headRadius = unit * 0.14f;
                Vector2 head1 = new Vector2(w * 0.28f, h * 0.76f);
                Vector2 head2 = new Vector2(w * 0.70f, h * 0.68f);
                float stemX1 = head1.x + headRadius;
                float stemX2 = head2.x + headRadius;
                float stemTop1 = h * 0.28f;
                float stemTop2 = h * 0.20f;

                painter.BeginPath();
                painter.Arc(head1, headRadius, 0f, 360f);
                painter.Fill();
                painter.BeginPath();
                painter.Arc(head2, headRadius, 0f, 360f);
                painter.Fill();

                painter.BeginPath();
                painter.MoveTo(new Vector2(stemX1, head1.y));
                painter.LineTo(new Vector2(stemX1, stemTop1));
                painter.MoveTo(new Vector2(stemX2, head2.y));
                painter.LineTo(new Vector2(stemX2, stemTop2));
                painter.Stroke();

                painter.lineWidth = unit * 0.16f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(stemX1, stemTop1));
                painter.LineTo(new Vector2(stemX2, stemTop2));
                painter.Stroke();
                break;
            }
            case NewProjectIconKind.Waveform:
            {
                float centerY = h * 0.5f;
                float[] positions = { 0.12f, 0.31f, 0.50f, 0.69f, 0.88f };
                float[] halfHeights = { 0.14f, 0.30f, 0.42f, 0.24f, 0.11f };
                painter.lineWidth = Mathf.Max(2f, unit * 0.11f);
                painter.BeginPath();
                for (int i = 0; i < positions.Length; i++)
                {
                    float x = w * positions[i];
                    float half = h * halfHeights[i];
                    painter.MoveTo(new Vector2(x, centerY - half));
                    painter.LineTo(new Vector2(x, centerY + half));
                }
                painter.Stroke();
                break;
            }
            case NewProjectIconKind.Image:
            {
                float left = w * 0.14f;
                float top = h * 0.18f;
                float right = w * 0.86f;
                float bottom = h * 0.82f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(left, top));
                painter.LineTo(new Vector2(right, top));
                painter.LineTo(new Vector2(right, bottom));
                painter.LineTo(new Vector2(left, bottom));
                painter.ClosePath();
                painter.Stroke();

                painter.BeginPath();
                painter.Arc(new Vector2(w * 0.38f, h * 0.40f), unit * 0.065f, 0f, 360f);
                painter.Fill();

                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.20f, h * 0.74f));
                painter.LineTo(new Vector2(w * 0.44f, h * 0.52f));
                painter.LineTo(new Vector2(w * 0.58f, h * 0.65f));
                painter.LineTo(new Vector2(w * 0.70f, h * 0.54f));
                painter.LineTo(new Vector2(w * 0.82f, h * 0.74f));
                painter.Stroke();
                break;
            }
            case NewProjectIconKind.Cross:
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.32f, h * 0.32f));
                painter.LineTo(new Vector2(w * 0.68f, h * 0.68f));
                painter.MoveTo(new Vector2(w * 0.68f, h * 0.32f));
                painter.LineTo(new Vector2(w * 0.32f, h * 0.68f));
                painter.Stroke();
                break;
            }
            case NewProjectIconKind.Plus:
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.50f, h * 0.22f));
                painter.LineTo(new Vector2(w * 0.50f, h * 0.78f));
                painter.MoveTo(new Vector2(w * 0.22f, h * 0.50f));
                painter.LineTo(new Vector2(w * 0.78f, h * 0.50f));
                painter.Stroke();
                break;
            }
            case NewProjectIconKind.Folder:
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.14f, h * 0.72f));
                painter.LineTo(new Vector2(w * 0.14f, h * 0.30f));
                painter.LineTo(new Vector2(w * 0.40f, h * 0.30f));
                painter.LineTo(new Vector2(w * 0.48f, h * 0.40f));
                painter.LineTo(new Vector2(w * 0.86f, h * 0.40f));
                painter.LineTo(new Vector2(w * 0.86f, h * 0.72f));
                painter.ClosePath();
                painter.Stroke();
                break;
            }
            case NewProjectIconKind.File:
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.26f, h * 0.14f));
                painter.LineTo(new Vector2(w * 0.60f, h * 0.14f));
                painter.LineTo(new Vector2(w * 0.74f, h * 0.28f));
                painter.LineTo(new Vector2(w * 0.74f, h * 0.86f));
                painter.LineTo(new Vector2(w * 0.26f, h * 0.86f));
                painter.ClosePath();
                painter.Stroke();

                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.60f, h * 0.14f));
                painter.LineTo(new Vector2(w * 0.60f, h * 0.28f));
                painter.LineTo(new Vector2(w * 0.74f, h * 0.28f));
                painter.Stroke();

                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.36f, h * 0.50f));
                painter.LineTo(new Vector2(w * 0.64f, h * 0.50f));
                painter.MoveTo(new Vector2(w * 0.36f, h * 0.64f));
                painter.LineTo(new Vector2(w * 0.56f, h * 0.64f));
                painter.Stroke();
                break;
            }
            case NewProjectIconKind.ChevronRight:
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.40f, h * 0.26f));
                painter.LineTo(new Vector2(w * 0.62f, h * 0.50f));
                painter.LineTo(new Vector2(w * 0.40f, h * 0.74f));
                painter.Stroke();
                break;
            }
            case NewProjectIconKind.Play:
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.34f, h * 0.24f));
                painter.LineTo(new Vector2(w * 0.78f, h * 0.50f));
                painter.LineTo(new Vector2(w * 0.34f, h * 0.76f));
                painter.ClosePath();
                painter.Fill();
                break;
            }
            case NewProjectIconKind.Pause:
            {
                painter.lineWidth = unit * 0.16f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.38f, h * 0.28f));
                painter.LineTo(new Vector2(w * 0.38f, h * 0.72f));
                painter.MoveTo(new Vector2(w * 0.62f, h * 0.28f));
                painter.LineTo(new Vector2(w * 0.62f, h * 0.72f));
                painter.Stroke();
                break;
            }
            case NewProjectIconKind.Stop:
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.30f, h * 0.30f));
                painter.LineTo(new Vector2(w * 0.70f, h * 0.30f));
                painter.LineTo(new Vector2(w * 0.70f, h * 0.70f));
                painter.LineTo(new Vector2(w * 0.30f, h * 0.70f));
                painter.ClosePath();
                painter.Fill();
                break;
            }
            case NewProjectIconKind.SkipStart:
            {
                painter.lineWidth = unit * 0.12f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.28f, h * 0.28f));
                painter.LineTo(new Vector2(w * 0.28f, h * 0.72f));
                painter.Stroke();

                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.74f, h * 0.26f));
                painter.LineTo(new Vector2(w * 0.40f, h * 0.50f));
                painter.LineTo(new Vector2(w * 0.74f, h * 0.74f));
                painter.ClosePath();
                painter.Fill();
                break;
            }
            case NewProjectIconKind.SkipEnd:
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.26f, h * 0.26f));
                painter.LineTo(new Vector2(w * 0.60f, h * 0.50f));
                painter.LineTo(new Vector2(w * 0.26f, h * 0.74f));
                painter.ClosePath();
                painter.Fill();

                painter.lineWidth = unit * 0.12f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.72f, h * 0.28f));
                painter.LineTo(new Vector2(w * 0.72f, h * 0.72f));
                painter.Stroke();
                break;
            }
            case NewProjectIconKind.Menu:
            {
                painter.lineWidth = unit * 0.10f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.24f, h * 0.30f));
                painter.LineTo(new Vector2(w * 0.76f, h * 0.30f));
                painter.MoveTo(new Vector2(w * 0.24f, h * 0.50f));
                painter.LineTo(new Vector2(w * 0.76f, h * 0.50f));
                painter.MoveTo(new Vector2(w * 0.24f, h * 0.70f));
                painter.LineTo(new Vector2(w * 0.76f, h * 0.70f));
                painter.Stroke();
                break;
            }
            case NewProjectIconKind.Gear:
            {
                Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
                painter.lineWidth = unit * 0.11f;
                painter.BeginPath();
                painter.Arc(center, unit * 0.21f, 0f, 360f);
                painter.Stroke();

                painter.lineWidth = unit * 0.13f;
                painter.BeginPath();
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 0.25f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    painter.MoveTo(center + direction * unit * 0.27f);
                    painter.LineTo(center + direction * unit * 0.38f);
                }
                painter.Stroke();

                painter.BeginPath();
                painter.Arc(center, unit * 0.06f, 0f, 360f);
                painter.Fill();
                break;
            }
        }
    }

    private static VisualElement CreateNewProjectIconTile(NewProjectIconKind kind, Color accent, float size)
    {
        VisualElement tile = new VisualElement();
        tile.style.width = size;
        tile.style.height = size;
        tile.style.minWidth = size;
        tile.style.minHeight = size;
        tile.style.flexShrink = 0f;
        tile.style.alignItems = Align.Center;
        tile.style.justifyContent = Justify.Center;
        tile.style.backgroundColor = new Color(accent.r * 0.16f, accent.g * 0.16f, accent.b * 0.22f, 1f);
        SetRadius(tile, size * 0.26f);
        SetBorderWidth(tile, 2f);
        SetBorderColor(tile, new Color(accent.r, accent.g, accent.b, 0.40f));
        tile.Add(CreateNewProjectIcon(kind, new Color(accent.r, accent.g, accent.b, 0.96f), size * 0.54f));
        return tile;
    }

    private TextField CreateNewProjectTextField(string label, string value)
    {
        TextField field = new TextField((label ?? string.Empty).ToUpperInvariant());
        field.value = value ?? string.Empty;
        field.style.flexDirection = FlexDirection.Column;
        field.style.alignItems = Align.Stretch;
        field.style.marginLeft = 0f;
        field.style.marginRight = 0f;
        field.style.marginTop = 0f;
        field.style.marginBottom = 24f;
        field.style.backgroundColor = Color.clear;
        field.style.fontSize = 34f;
        field.style.unityFontDefinition = bodyFont;
        RegisterTextFieldKeyboardCapture(field);

        Color idleBorder = new Color(0.75f, 0.72f, 0.82f, 0.28f);
        Color focusBorder = new Color(0.62f, 0.38f, 1f, 0.90f);
        Color idleBackground = new Color(0.042f, 0.040f, 0.048f, 1f);
        Color focusBackground = new Color(0.058f, 0.054f, 0.070f, 1f);

        void StyleInput(bool focused)
        {
            VisualElement input = field.Q<VisualElement>(TextField.textInputUssName) ?? field.Q<VisualElement>("unity-text-input");
            if (input == null)
                return;

            input.style.backgroundColor = focused ? focusBackground : idleBackground;
            SetBorderWidth(input, 2f);
            SetBorderColor(input, focused ? focusBorder : idleBorder);
            SetRadius(input, 14f);
            input.style.height = 84f;
            input.style.minHeight = 84f;
            input.style.paddingLeft = 26f;
            input.style.paddingRight = 26f;
            input.style.color = new Color(0.97f, 0.96f, 0.98f, 1f);
            input.style.fontSize = 34f;
            input.style.unityTextAlign = TextAnchor.MiddleLeft;
        }

        field.schedule.Execute(() =>
        {
            Label fieldLabel = field.Q<Label>();
            if (fieldLabel != null)
            {
                fieldLabel.style.color = new Color(0.62f, 0.60f, 0.70f, 1f);
                fieldLabel.style.fontSize = 25f;
                fieldLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                fieldLabel.style.letterSpacing = 3f;
                fieldLabel.style.marginBottom = 12f;
                fieldLabel.style.marginLeft = 4f;
                fieldLabel.style.minWidth = 0f;
                fieldLabel.style.width = StyleKeyword.Auto;
                fieldLabel.style.paddingLeft = 0f;
                fieldLabel.style.paddingRight = 0f;
            }

            StyleInput(false);
        });

        field.RegisterCallback<FocusInEvent>(_ => StyleInput(true));
        field.RegisterCallback<FocusOutEvent>(_ => StyleInput(false));
        return field;
    }

    private Button CreateNewProjectGhostButton(string text, Action action)
    {
        Button button = new Button(action) { text = text ?? string.Empty };
        button.focusable = false;
        button.style.unityFontDefinition = bodyFont;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.fontSize = 27f;
        button.style.height = 76f;
        button.style.minWidth = 190f;
        button.style.marginLeft = 0f;
        button.style.marginRight = 0f;
        button.style.marginTop = 0f;
        button.style.marginBottom = 0f;
        button.style.paddingLeft = 32f;
        button.style.paddingRight = 32f;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        SetRadius(button, 14f);

        void Apply(bool hover)
        {
            button.style.backgroundColor = hover ? new Color(1f, 1f, 1f, 0.10f) : new Color(1f, 1f, 1f, 0.045f);
            SetBorderWidth(button, 2f);
            SetBorderColor(button, hover ? new Color(0.84f, 0.82f, 0.92f, 0.60f) : new Color(0.80f, 0.78f, 0.88f, 0.30f));
            button.style.color = hover ? Color.white : new Color(0.91f, 0.90f, 0.94f, 1f);
            button.style.scale = hover ? new Scale(new Vector3(1.02f, 1.02f, 1f)) : new Scale(Vector3.one);
        }

        Apply(false);
        button.RegisterCallback<MouseEnterEvent>(_ => Apply(true));
        button.RegisterCallback<MouseLeaveEvent>(_ => Apply(false));
        return button;
    }

    private Button CreateNewProjectIconButton(NewProjectIconKind kind, Action action, bool danger = false, float size = 44f, bool solid = false)
    {
        Button button = new Button(action) { text = string.Empty };
        button.focusable = false;
        button.style.width = size;
        button.style.height = size;
        button.style.minWidth = size;
        button.style.minHeight = size;
        button.style.marginLeft = 0f;
        button.style.marginRight = 0f;
        button.style.marginTop = 0f;
        button.style.marginBottom = 0f;
        button.style.paddingLeft = 0f;
        button.style.paddingRight = 0f;
        button.style.paddingTop = 0f;
        button.style.paddingBottom = 0f;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.Center;
        button.style.flexShrink = 0f;
        SetRadius(button, size * 0.22f);
        button.Add(CreateNewProjectIcon(kind, new Color(0.83f, 0.82f, 0.88f, 0.95f), size * 0.46f));

        Color hoverTint = danger ? new Color(1f, 0.42f, 0.38f, 1f) : Color.white;
        Color idleBackground = solid ? new Color(0.035f, 0.033f, 0.042f, 0.88f) : new Color(1f, 1f, 1f, 0.035f);
        Color hoverBackground = solid
            ? (danger ? new Color(0.30f, 0.10f, 0.10f, 0.90f) : new Color(0.17f, 0.16f, 0.20f, 0.90f))
            : new Color(hoverTint.r, hoverTint.g, hoverTint.b, danger ? 0.14f : 0.10f);

        void Apply(bool hover)
        {
            button.style.backgroundColor = hover ? hoverBackground : idleBackground;
            SetBorderWidth(button, 2f);
            SetBorderColor(button, hover
                ? new Color(hoverTint.r, hoverTint.g, hoverTint.b, danger ? 0.60f : 0.42f)
                : new Color(0.78f, 0.76f, 0.86f, 0.26f));
        }

        Apply(false);
        button.RegisterCallback<MouseEnterEvent>(_ => Apply(true));
        button.RegisterCallback<MouseLeaveEvent>(_ => Apply(false));
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

    private static void StyleStrongPopupPanel(VisualElement element, Color background, float radius = 16f)
    {
        if (element == null)
            return;

        StylePopupPanel(element, background, radius);
        SetBorderWidth(element, 2f);
        SetBorderColor(element, new Color(0.50f, 0.48f, 0.56f, 0.96f));
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
                new Color(0.25f, 0.24f, 0.28f, 0.95f),
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

        element.style.borderTopColor = side;
        element.style.borderRightColor = side;
        element.style.borderLeftColor = side;
        element.style.borderBottomColor = side;
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
                            