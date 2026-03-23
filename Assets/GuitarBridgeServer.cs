using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class GuitarBridgeServer : MonoBehaviour
{
    public enum TabsBackgroundMode
    {
        SolidColor = 0,
        Starfield = 1,
        BlueSky = 2
    }

    public enum TabsStarStyle
    {
        SoftDots = 0,
        Crystal = 1,
        Neon = 2
    }

    public enum TabsSkyMood
    {
        Day = 0,
        Sunset = 1
    }

    [Header("Render Mode")]
    public GuitarRenderMode renderMode = GuitarRenderMode.Tabs;

    [Header("Settings")]
    public bool invertStrings = true;
    public float noteSpeed = 12f;

    [Header("Timing & Forgiveness")]
    public float hitWindowEarly = 0.3f;
    public float hitWindowLate = 0.5f;
    public float judgmentGrace = 0.75f; 
    public float eventTimeSlack = 0.05f;
    public float highStringExtraEarly = 0.02f;
    public float highStringExtraLate = 0.08f;

    [Header("Onset Matching")]
    public float eventMatchEarly = 0.15f; 
    public float eventMatchLate = 0.15f;  
    public float duplicateEventMergeWindow = 0.085f;

    [Header("High String Rescue")]
    public bool allowHighStringActiveRescue = true;
    public float highStringRescueTightWindow = 0.065f;

    [Header("Chord / Open Visuals")]
    public float chordGroupWindow = 0.06f;
    public float defaultOpenAnchorFret = 2.0f;
    public float chordSidePaddingFrets = 0.85f;
    public float chordFrameThickness = 0.10f;
    public float chordFrameVerticalPadding = 0.55f;
    public float chordOpenLineHeight = 0.18f;
    public float chordOpenLineDepth = 0.42f;
    public float chordFrettedNoteWidth = 0.65f;
    public float chordFrettedNoteHeight = 0.18f;
    public float chordFrettedNoteDepth = 0.45f;
    public float singleOpenWidth = 0.8f;
    public float singleOpenHeight = 0.18f;
    public float singleOpenDepth = 0.45f;
    public bool hideOpenFretNumber = true;

    [Header("Visuals")]
    public float judgeableDarkenMultiplier = 5f;

    [Header("UI & Logs")]
    public TextMeshProUGUI uiText;
    public bool showCenterDebugOverlay = false;
    private string logNotes = "--";

    [Header("Python UDP Config")]
    public int udpPort = 9000;
    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning;

    [Header("Notes Detector")]
    public bool autoLaunchNotesDetector = true;
    public string notesDetectorRelativePath = "NotesReader/guitar_ai2_continuous.exe";
    public bool openNotesDetectorConsoleWindowInEditor = true;
    private System.Diagnostics.Process notesDetectorProcess;

    [Header("Debug")]
    public bool logSpawnedNotes = false;
    public bool useBuiltInDemoSong = false;
    public bool useDemoSongIfMidiMissing = true;

    [Header("Colors - Strings")]
    public Color[] stringColors = new Color[]
    {
        new Color(0.91f, 0.30f, 0.24f, 1f),
        new Color(0.95f, 0.77f, 0.06f, 1f),
        new Color(0.20f, 0.60f, 0.86f, 1f),
        new Color(0.90f, 0.49f, 0.13f, 1f),
        new Color(0.18f, 0.80f, 0.44f, 1f),
        new Color(0.61f, 0.35f, 0.71f, 1f)
    };

    [Header("Colors - Status")]
    public Color highwayHitColor = new Color(1f, 1f, 1f, 1.5f);
    public Color highwayMissColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    public Color tabHitColor = Color.green;
    public Color tabMissColor = Color.red;
    public Color tabJudgeableColor = Color.white;
    public float tabIdleFillDarken = 0.4f;

    [Header("Colors - Highway Config")]
    public Color highwayBackgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.9f);

    [Header("Highway 3D Dimensions")]
    public int TotalFrets = 24;
    public float FretSpacing = 1.0f;
    public float StrikeLineZ = -5.0f;
    public float SpawnZ = 50.0f;
    public float highwayCameraY = 8.0f;
    public float highwayCameraZ = -10.0f;
    public float highwayCameraPitch = 45f;
    public float lookaheadWindow = 3.0f;
    public float highwayResolvedHoldTime = 0.4f;
    public float camMoveSpeed = 8.0f;
    public float highwayNoteHeightScale = 1.35f;
    public float highwayStuckOutlineThickness = 0.06f;
    public float highwayStuckOutlineDepth = 0.04f;
    public float highwayCameraFarClip = 5000f;
    public float highwayBackgroundDistance = 1200f;
    public float highwayBackgroundCenterY = -1500f;
    public float highwayBackgroundScale = 250f;
    public float highwayBackgroundCloudYOffset = 0f;
    public float highwayBackgroundStarScale = 1f;
    public float highwayBackgroundCloudScale = 1f;
    public float highwayBackgroundStarSpread = 1f;
    public float highwayBackgroundCloudSpread = 1f;
    public float highwayLaneGuideThickness = 0.14f;
    public float highwayLaneGuideYOffset = -1.84f;
    public float highwayFretNumberYOffset = 0.45f;
    public float highwayFretNumberZOffset = 0.12f;
    public bool highwayHighlightFretBoundaries = false;
    public bool highwayShowApproachLine = false;
    public bool highwayShowLandingDot = true;

    [Header("Tabs Dimensions")]
    public float tabPanelWidth = 22f;
    public float tabHorizontalPadding = 1f;
    public float tabLineSpacing = 0.5f;
    public float tabNoteCircleDiameter = 0.45f;
    public float tabNoteCircleDepth = 0.02f;
    public float tabNoteOutlineThickness = 0.05f;
    public float tabNoteFontSize = 2.5f;
    public float tabSustainThickness = 0.15f;
    public float tabSustainDepth = 0.05f;
    public float tabSustainMinWidth = 0.3f;
    public Color tabSustainColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
    public float tabTechniqueTunnelHeight = 0.26f;
    public float tabTechniqueTunnelDepth = 0.06f;
    public float tabTechniqueInnerPadding = 0.05f;
    public float tabTechniqueGlyphFontSize = 2.0f;
    public Color tabTechniqueGlyphColor = Color.white;
    public Color tabTechniqueFillColor = new Color(0.28f, 0.31f, 0.36f, 0.95f);

    [Header("Tabs Panels Layout")]
    public float tabCameraSize = 5f;
    public float tabCameraZ = -10f;
    public Color tabBackgroundColor = new Color(0.05f, 0.05f, 0.05f);
    public float tabPanelsVerticalOffset = 0f;
    public float tabPanelGap = 0.8f;
    public float tabPanelHeight = 4.2f;
    public float tabBorderThickness = 0.05f;
    public float tabBorderDepth = 0.05f;
    public Color tabBorderColor = new Color(0.3f, 0.3f, 0.3f);
    public float tabZDepth = 0f;
    public float tabStringThickness = 0.03f;
    public float tabStringDepth = 0.01f;
    public Color tabPanelBackdropColor = new Color(0.02f, 0.03f, 0.06f, 0.28f);

    [Header("Background")]
    public TabsBackgroundMode tabBackgroundMode = TabsBackgroundMode.Starfield;

    [Header("Background - Starfield Core")]
    public TabsStarStyle tabStarStyle = TabsStarStyle.SoftDots;
    public int tabStarSeed = 1337;
    [Min(0.01f)] public float tabStarfieldWidth = 46f;
    public float tabStarfieldNearZ = -2.6f;
    public float tabStarfieldFarZ = -8.2f;
    public float tabStarfieldMinY = -6.6f;
    public float tabStarfieldMaxY = 6.6f;
    [Min(0f)] public float tabStarDriftSpeed = 0.55f;
    [Range(0f, 1f)] public float tabStarTwinkleStrength = 0.25f;
    [Range(0f, 1f)] public float tabStarSubtleVerticalWave = 0.05f;

    [Header("Background - Star Layers")]
    [Range(8, 1200)] public int tabNearStarCount = 130;
    [Range(8, 1200)] public int tabMidStarCount = 170;
    [Range(8, 1200)] public int tabFarStarCount = 220;
    [Min(0.001f)] public float tabNearStarSizeMin = 0.06f;
    [Min(0.001f)] public float tabNearStarSizeMax = 0.16f;
    [Min(0.001f)] public float tabMidStarSizeMin = 0.04f;
    [Min(0.001f)] public float tabMidStarSizeMax = 0.11f;
    [Min(0.001f)] public float tabFarStarSizeMin = 0.02f;
    [Min(0.001f)] public float tabFarStarSizeMax = 0.07f;
    [Range(0f, 1f)] public float tabNearStarAlphaMin = 0.35f;
    [Range(0f, 1f)] public float tabNearStarAlphaMax = 0.95f;
    [Range(0f, 1f)] public float tabMidStarAlphaMin = 0.22f;
    [Range(0f, 1f)] public float tabMidStarAlphaMax = 0.8f;
    [Range(0f, 1f)] public float tabFarStarAlphaMin = 0.15f;
    [Range(0f, 1f)] public float tabFarStarAlphaMax = 0.55f;
    [Min(0f)] public float tabNearLayerSpeedMultiplier = 1.35f;
    [Min(0f)] public float tabMidLayerSpeedMultiplier = 0.95f;
    [Min(0f)] public float tabFarLayerSpeedMultiplier = 0.60f;
    public Color tabNearStarColor = new Color(0.95f, 0.96f, 1f, 0.95f);
    public Color tabMidStarColor = new Color(0.74f, 0.85f, 1f, 0.85f);
    public Color tabFarStarColor = new Color(0.56f, 0.70f, 0.96f, 0.7f);
    [Range(0f, 8f)] public float tabStarEmission = 0.35f;

    [Header("Background - Shooting Stars")]
    public bool tabShootingStarsEnabled = true;
    [Range(1, 8)] public int tabShootingStarMaxConcurrent = 2;
    [Min(0.1f)] public float tabShootingStarIntervalMin = 2.2f;
    [Min(0.1f)] public float tabShootingStarIntervalMax = 6.5f;
    [Min(0.1f)] public float tabShootingStarSpeed = 8.5f;
    [Min(0.05f)] public float tabShootingStarLength = 0.9f;
    [Range(0f, 1f)] public float tabShootingStarAlpha = 0.9f;
    public Color tabShootingStarColor = new Color(0.95f, 0.97f, 1f, 0.9f);

    [Header("Background - Blue Sky")]
    public TabsSkyMood tabSkyMood = TabsSkyMood.Day;
    [Min(0.01f)] public float tabSkyWidth = 54f;
    public float tabSkyNearZ = 1.4f;
    public float tabSkyFarZ = 7.8f;
    public float tabSkyMinY = -7.2f;
    public float tabSkyMaxY = 7.2f;
    public Color tabSkyTopColor = new Color(0.17f, 0.55f, 0.98f, 1f);
    public Color tabSkyMidColor = new Color(0.38f, 0.72f, 0.99f, 1f);
    public Color tabSkyBottomColor = new Color(0.76f, 0.90f, 1f, 1f);
    public Color tabSkySunsetTopColor = new Color(0.96f, 0.50f, 0.22f, 1f);
    public Color tabSkySunsetMidColor = new Color(0.98f, 0.66f, 0.30f, 1f);
    public Color tabSkySunsetBottomColor = new Color(1f, 0.84f, 0.52f, 1f);
    [Range(8, 220)] public int tabSkyCloudCountNear = 42;
    [Range(8, 220)] public int tabSkyCloudCountMid = 30;
    [Range(8, 220)] public int tabSkyCloudCountFar = 18;
    [Min(0.01f)] public float tabSkyCloudSpeedNear = 0.34f;
    [Min(0.01f)] public float tabSkyCloudSpeedMid = 0.20f;
    [Min(0.01f)] public float tabSkyCloudSpeedFar = 0.11f;
    [Range(0f, 1f)] public float tabSkyCloudAlphaNear = 0.92f;
    [Range(0f, 1f)] public float tabSkyCloudAlphaMid = 0.78f;
    [Range(0f, 1f)] public float tabSkyCloudAlphaFar = 0.62f;
    [Min(0.1f)] public float tabSkyCloudScaleMinNear = 1.8f;
    [Min(0.1f)] public float tabSkyCloudScaleMaxNear = 3.6f;
    [Min(0.1f)] public float tabSkyCloudScaleMinMid = 1.3f;
    [Min(0.1f)] public float tabSkyCloudScaleMaxMid = 2.8f;
    [Min(0.1f)] public float tabSkyCloudScaleMinFar = 1.0f;
    [Min(0.1f)] public float tabSkyCloudScaleMaxFar = 2.1f;
    [Min(0.2f)] public float tabSkyCloudGlobalScale = 2.65f;
    public Color tabSkyDayCloudTopTint = new Color(0.98f, 0.99f, 1f, 1f);
    public Color tabSkyDayCloudBottomTint = new Color(0.90f, 0.95f, 1f, 1f);
    public Color tabSkySunsetCloudTopTint = new Color(1f, 0.84f, 0.68f, 1f);
    public Color tabSkySunsetCloudBottomTint = new Color(0.98f, 0.62f, 0.42f, 1f);
    public bool tabSkyStarsEnabled = true;
    [Range(8, 1200)] public int tabSkyStarCount = 320;
    [Min(0.001f)] public float tabSkyStarSizeMin = 0.015f;
    [Min(0.001f)] public float tabSkyStarSizeMax = 0.065f;
    [Range(0f, 1f)] public float tabSkyStarAlpha = 0.78f;
    [Range(0f, 1f)] public float tabSkyStarTwinkleFraction = 0.28f;
    [Range(0f, 1f)] public float tabSkyStarTwinkleStrength = 0.16f;
    [Min(0.05f)] public float tabSkyStarTwinkleSpeedMin = 0.45f;
    [Min(0.05f)] public float tabSkyStarTwinkleSpeedMax = 1.2f;
    [Range(0f, 0.2f)] public float tabSkyCloudVerticalBob = 0.04f;

    [Header("Tabs Header")]
    public float tabLabelFontSize = 3f;
    public Color tabHeaderCurrentColor = Color.white;
    public Color tabHeaderNextColor = Color.gray;

    [Header("Tabs Playhead")]
    public float tabPlayheadWidth = 0.1f;
    public float tabPlayheadDepth = 0.1f;
    public Color tabPlayheadColor = new Color(1f, 1f, 0f, 0.8f);

    [Header("Tabs Sections")]
    public float tabSectionDuration = 4.0f;
    [Range(0.5f, 3.0f)] public float tabSectionLengthMultiplier = 1.0f;
    public float tabPanelSwapDuration = 0.4f;
    public float tabPanelLiftDistance = 2.0f;

    public float TabTopPanelY => tabPanelsVerticalOffset + (tabPanelGap * 0.5f);
    public float TabBottomPanelY => tabPanelsVerticalOffset - (tabPanelGap * 0.5f) - tabPanelHeight;
    public float tabPanelCenterX => 0f;

    private class NoteEvent
    {
        public int id;
        public float time;
        public HashSet<int> pitches = new HashSet<int>();
        public HashSet<int> consumedKeys = new HashSet<int>();
    }

    private readonly int[] stringBasePitch = { 40, 45, 50, 55, 59, 64 };
    private readonly Dictionary<string, int> noteToIndex = new Dictionary<string, int>();
    private readonly Dictionary<int, NoteData> chartNoteById = new Dictionary<int, NoteData>();
    private readonly List<NoteEvent> recentNoteEvents = new List<NoteEvent>();
    private readonly HashSet<int> latestDetectedPitches = new HashSet<int>();

    private List<NoteData> chartNotes = new List<NoteData>();
    private List<GameplayNoteState> noteStates = new List<GameplayNoteState>();
    private List<TabSectionData> tabSections = new List<TabSectionData>();

    private IGuitarGameplayRenderer activeRenderer;
    private GuitarRenderMode activeRendererMode = (GuitarRenderMode)(-1);

    private float songTimer;
    private float audioSongTimer;
    private bool isPaused;
    private float pauseSeekStepSeconds = 3.2f;
    private float playbackSpeedPercent = 100f;

    private bool loopEnabled;
    private float loopStartTime;
    private float loopEndTime;
    private int selectedLoopMarker = 1;
    private int latestNoteEventId;
    private bool latestPacketHadEvent;
    private long lastUdpPacketUtcTicks;
    private const float DetectorConnectionTimeoutSeconds = 1.5f;
    private string latestEventNotesText = "--";
    private float latestParsedInputLevel = -1f;
    private float smoothedInputLevel;

    public int midiTrackIndex = -1;
    private int currentLoadedTrackIndex = -999;

    [Header("Backing Track")]
    public AudioSource backingTrackSource;
    public string backingTrackFileName = "song.mp3";
    [Min(0f)] public float defaultSongStartDelaySeconds = 2.0f;

    [Serializable]
    private class TrackOffsetOverride
    {
        public string partId;
        public bool useTrackOffset;
        public float offsetMs;
    }

    [Serializable]
    private class SongMetadata
    {
        public string songFileName;
        public float audioOffsetMs = 0f;
        public float tabSpeedOffsetPercent = 100f;
        public float songStartDelaySeconds = 2.0f;
        public bool useAutoTrackSelection = true;
        public string selectedMusicXmlPartId;
        public float bestScorePercent = 0f;
        public List<TrackScoreEntry> trackScores = new List<TrackScoreEntry>();
        public List<TrackOffsetOverride> trackOffsetOverrides = new List<TrackOffsetOverride>();
    }

    [Serializable]
    private class TrackScoreEntry
    {
        public string partId;
        public string displayName;
        public float bestScorePercent;
    }

    [Serializable]
    private class GlobalRuntimeSettingsMetadata
    {
        public List<RuntimeSettingValueEntry> values = new List<RuntimeSettingValueEntry>();
    }

    [Serializable]
    private class RuntimeSettingValueEntry
    {
        public string id;
        public string value;
    }

    private string currentSongFileName = "song.mp3";
    private bool hasBackingTrack;
    private bool showSongSettings;
    private bool showMainMenu;
    private bool mainMenuFlowActive;
    private bool showSongSelection;
    private bool showTrackSelection;
    private bool showGlobalSettings;
    private int selectedSongListIndex;
    private int songListScrollOffset;
    private int selectedTrackListIndex;
    private int trackListScrollOffset;
    private SongLibraryEntry pendingTrackSelectionSong;
    private readonly List<SongLibraryEntry> availableSongs = new List<SongLibraryEntry>();
    private readonly List<MusicXmlLoader.MusicXmlPartSummary> pendingTrackSelectionParts = new List<MusicXmlLoader.MusicXmlPartSummary>();
    private float audioOffsetMs;
    private float globalAudioOffsetMs;
    private bool useTrackOffsetForCurrentTrack;
    private float tabSpeedOffsetPercent = 100f;
    private float songStartDelaySeconds = 2.0f;
    private SongMetadata songMetadata = new SongMetadata();
    private float currentSongBestScorePercent;
    private float currentTrackBestScorePercent;
    private const string SelectedSongDirectoryPrefsKey = "guitar_selected_song_directory";
    private bool isLoadingBackingTrack;
    private string backingTrackLoadError = string.Empty;
    private bool songHasEnded;
    private bool songSelectionOpenedFromSongEnd;
    private bool songSelectionOpenedFromMainMenu;
    private bool showStartupTuningReminder;
    private bool resumeGameplayAfterStartupTuningReminder;
    private SongLibraryEntry currentSongEntry;
    private readonly List<MusicXmlLoader.MusicXmlPartSummary> currentSongPartSummaries = new List<MusicXmlLoader.MusicXmlPartSummary>();
    private bool useAutoTrackSelection = true;
    private string selectedMusicXmlPartId = string.Empty;
    private float lastLeftArrowTapTime = -10f;
    private float lastRightArrowTapTime = -10f;
    private const float ArrowDoubleTapThreshold = 0.35f;
    private readonly List<RuntimeSettingDefinition> runtimeSettingDefinitions = new List<RuntimeSettingDefinition>();
    private readonly Dictionary<string, RuntimeSettingDefinition> runtimeSettingById = new Dictionary<string, RuntimeSettingDefinition>();
    private readonly Dictionary<string, string> runtimeSettingDefaultValues = new Dictionary<string, string>();
    private readonly Dictionary<string, string> pendingGlobalRuntimeSettingValues = new Dictionary<string, string>();
    private List<RuntimeSettingSectionSnapshot> cachedRuntimeSettingsSnapshot = new List<RuntimeSettingSectionSnapshot>();
    private bool runtimeSettingsSnapshotDirty = true;
    private const string GlobalRuntimeSettingsFileName = "runtime_settings_metadata.json";


    private sealed class RuntimeSettingDefinition
    {
        public string Id;
        public string Section;
        public string Label;
        public string Tooltip;
        public string ValueType;
        public float Min;
        public float Max;
        public float Step;
        public Func<string> Getter;
        public Action<string> Setter;
        public List<string> EnumOptions;
    }

    private void Start()
    {
        Application.targetFrameRate = 60;
        ExternalContentBootstrap.EnsureRuntimeContentReady();
        Debug.Log($"[GuitarBridgeServer] Using persistent content folder: {ExternalContentPaths.PersistentRoot}");
        Debug.Log($"[NotesDetector] Start() called on '{gameObject.name}'. autoLaunchNotesDetector={autoLaunchNotesDetector}, enabled={enabled}, activeInHierarchy={gameObject.activeInHierarchy}, platform={Application.platform}");
        TryLaunchNotesDetector();
        isRunning = true;
        BuildNoteIndices();
        StartUdpThread();
        EnsureBackingTrackSource();
        RegisterRuntimeSettings();
        LoadGlobalRuntimeSettingsMetadata();
        bool startInMainMenu = true;
        showMainMenu = startInMainMenu;
        mainMenuFlowActive = startInMainMenu;
        isPaused = startInMainMenu;
        LoadTestSong();
        isPaused = startInMainMenu;
        EnsureRenderer();
        SyncAudioToSongTimer(playImmediately: false);
    }

    private void Update()
    {
        HandlePauseControls();

        if (!isPaused)
        {
            audioSongTimer += Time.deltaTime * GetPlaybackSpeedScale();
            songTimer += Time.deltaTime * GetTabPlaybackSpeedScale();
            HandleLoopPlayback();
        }

        UpdateSongEndState();

        ApplyPlaybackSpeedToAudio();
        SyncAudioToSongTimer(playImmediately: !isPaused);

        if (midiTrackIndex != currentLoadedTrackIndex)
            LoadTestSong(preservePauseUiState: isPaused || showMainMenu || showSongSettings || showSongSelection || showTrackSelection || showGlobalSettings);

        ParseUdpState();

        if (!isPaused)
        {
            PruneHistory();
            UpdateGameplayStates();
            UpdateAndPersistSongBestScore();
        }

        UpdateInputLevelEstimate();

        EnsureRenderer();

        if (activeRenderer != null)
            activeRenderer.Render(BuildSnapshot());

        UpdateUiText();
    }

    private void HandlePauseControls()
    {
        if (showStartupTuningReminder)
        {
            if (Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Escape))
            {
                DismissStartupTuningReminderFromUi();
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.S) && renderMode == GuitarRenderMode.Tabs && (isPaused || showSongSettings))
        {
            showSongSettings = !showSongSettings;
            showGlobalSettings = false;
        }

        if (Input.GetKeyDown(KeyCode.G) && renderMode == GuitarRenderMode.Tabs && (isPaused || showGlobalSettings))
        {
            showGlobalSettings = !showGlobalSettings;
            showSongSettings = false;
        }

        if (showMainMenu)
        {
            HandleMainMenuControls();
            return;
        }

        if (showTrackSelection)
        {
            HandleTrackSelectionControls();
            return;
        }

        if (showSongSelection)
        {
            HandleSongSelectionControls();
            return;
        }

        if (showSongSettings)
        {
            HandleSongSettingsControls();
            return;
        }

        if (showGlobalSettings)
        {
            HandleGlobalSettingsControls();
            return;
        }

        if (songHasEnded)
        {
            isPaused = true;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            isPaused = !isPaused;
            showSongSettings = false;
            showMainMenu = false;
            mainMenuFlowActive = false;
            showSongSelection = false;
            showTrackSelection = false;
            showGlobalSettings = false;
            SyncAudioToSongTimer(playImmediately: !isPaused);
        }

        if (!isPaused)
            return;

        if (Input.GetKeyDown(KeyCode.M))
        {
            OpenMainMenuFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            OpenSongSelectionMenu();
            return;
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            OpenOrFocusToneLab();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (renderMode != GuitarRenderMode.Highway3D)
            {
                loopEnabled = !loopEnabled;
                if (loopEnabled && loopEndTime <= loopStartTime)
                    loopEndTime = loopStartTime + 0.25f;
            }
        }


        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            selectedLoopMarker = 1;
            SeekSongTime(loopStartTime, false);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            selectedLoopMarker = 2;
            SeekSongTime(loopEndTime, false);
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (Time.unscaledTime - lastLeftArrowTapTime <= ArrowDoubleTapThreshold)
            {
                JumpToAdjacentNote(false);
                lastLeftArrowTapTime = -10f;
                return;
            }
            lastLeftArrowTapTime = Time.unscaledTime;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (Time.unscaledTime - lastRightArrowTapTime <= ArrowDoubleTapThreshold)
            {
                JumpToAdjacentNote(true);
                lastRightArrowTapTime = -10f;
                return;
            }
            lastRightArrowTapTime = Time.unscaledTime;
        }

        float seekDirection = 0f;
        if (Input.GetKey(KeyCode.LeftArrow))
            seekDirection -= 1f;
        if (Input.GetKey(KeyCode.RightArrow))
            seekDirection += 1f;

        if (Mathf.Approximately(seekDirection, 0f))
            return;

        SeekSongTime(songTimer + (seekDirection * pauseSeekStepSeconds * Time.deltaTime), true);
    }

    private void JumpToAdjacentNote(bool moveForward)
    {
        if (chartNotes == null || chartNotes.Count == 0)
            return;

        const float epsilon = 0.0001f;
        float targetTime = songTimer;
        bool found = false;

        if (moveForward)
        {
            float best = float.MaxValue;
            for (int i = 0; i < chartNotes.Count; i++)
            {
                float t = chartNotes[i].time;
                if (t > songTimer + epsilon && t < best)
                {
                    best = t;
                    found = true;
                }
            }
            if (found)
                targetTime = best;
        }
        else
        {
            float best = float.MinValue;
            for (int i = 0; i < chartNotes.Count; i++)
            {
                float t = chartNotes[i].time;
                if (t < songTimer - epsilon && t > best)
                {
                    best = t;
                    found = true;
                }
            }
            if (found)
                targetTime = best;
        }

        if (!found)
            return;

        SeekSongTime(targetTime, false);
    }

    private void HandleMainMenuControls()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.M))
        {
            showMainMenu = false;
            isPaused = true;
            SyncAudioToSongTimer(playImmediately: false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            ContinueFromMainMenuFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            OpenSongSelectionFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            OpenGlobalSettingsFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            ExitGameFromUi();
            return;
        }
    }

    private void HandleSongSelectionControls()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.L))
        {
            CloseSongSelectionFromUi();
            return;
        }

        if (availableSongs.Count == 0)
            return;

        if (Input.GetKeyDown(KeyCode.UpArrow))
            MoveSongSelection(-1);
        else if (Input.GetKeyDown(KeyCode.DownArrow))
            MoveSongSelection(1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            OpenTrackSelectionForSong(selectedSongListIndex);
    }

    private void HandleTrackSelectionControls()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            CloseTrackSelection();
            return;
        }

        if (pendingTrackSelectionParts.Count == 0)
            return;

        if (Input.GetKeyDown(KeyCode.UpArrow))
            MoveTrackSelectionInMenu(-1);
        else if (Input.GetKeyDown(KeyCode.DownArrow))
            MoveTrackSelectionInMenu(1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            ConfirmTrackSelection();
    }

    private void HandleSongSettingsControls()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            showSongSettings = false;
            isPaused = true;
            SyncAudioToSongTimer(playImmediately: false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            isPaused = !isPaused;
            SyncAudioToSongTimer(playImmediately: !isPaused);
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            ToggleOffsetScope();
            SaveSongMetadata();
            SyncAudioToSongTimer(playImmediately: !isPaused);
        }

        if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Comma))
            MoveTrackSelection(-1);
        else if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Period))
            MoveTrackSelection(1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            isPaused = false;
            SyncAudioToSongTimer(playImmediately: true);
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (Time.unscaledTime - lastLeftArrowTapTime <= ArrowDoubleTapThreshold)
            {
                JumpToAdjacentNote(false);
                lastLeftArrowTapTime = -10f;
                return;
            }
            lastLeftArrowTapTime = Time.unscaledTime;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (Time.unscaledTime - lastRightArrowTapTime <= ArrowDoubleTapThreshold)
            {
                JumpToAdjacentNote(true);
                lastRightArrowTapTime = -10f;
                return;
            }
            lastRightArrowTapTime = Time.unscaledTime;
        }

        float seekDirection = 0f;
        if (Input.GetKey(KeyCode.LeftArrow))
            seekDirection -= 1f;
        if (Input.GetKey(KeyCode.RightArrow))
            seekDirection += 1f;

        if (!Mathf.Approximately(seekDirection, 0f))
            SeekSongTime(songTimer + (seekDirection * pauseSeekStepSeconds * Time.deltaTime), true);
    }

    private void HandleGlobalSettingsControls()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.G))
        {
            CloseGlobalSettingsFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (mainMenuFlowActive)
            {
                CloseGlobalSettingsFromUi();
                return;
            }

            isPaused = !isPaused;
            if (!isPaused)
                showGlobalSettings = false;
            SyncAudioToSongTimer(playImmediately: !isPaused);
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (mainMenuFlowActive)
            {
                CloseGlobalSettingsFromUi();
                return;
            }

            isPaused = false;
            showGlobalSettings = false;
            SyncAudioToSongTimer(playImmediately: true);
        }
    }

    private int GetTrackOptionCount()
    {
        return 1 + currentSongPartSummaries.Count;
    }

    private int GetCurrentTrackOptionIndex()
    {
        if (useAutoTrackSelection)
            return 0;

        if (string.IsNullOrEmpty(selectedMusicXmlPartId))
            return 0;

        for (int i = 0; i < currentSongPartSummaries.Count; i++)
        {
            if (string.Equals(currentSongPartSummaries[i].PartId, selectedMusicXmlPartId, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        }

        return 0;
    }

    private string GetTrackDisplayName(int optionIndex)
    {
        if (optionIndex <= 0)
        {
            if (currentSongPartSummaries.Count == 0)
                return "Auto";

            MusicXmlLoader.MusicXmlPartSummary best = currentSongPartSummaries.OrderByDescending(s => s.Score).First();
            return $"Auto ({best.Name})";
        }

        int summaryIndex = optionIndex - 1;
        if (summaryIndex < 0 || summaryIndex >= currentSongPartSummaries.Count)
            return "Auto";

        MusicXmlLoader.MusicXmlPartSummary summary = currentSongPartSummaries[summaryIndex];
        return $"{summary.Name}  [notes:{summary.NoteCount} tab:{summary.TabCount}]";
    }

    private void MoveTrackSelection(int delta)
    {
        int optionCount = GetTrackOptionCount();
        if (optionCount <= 1)
            return;

        int currentOption = GetCurrentTrackOptionIndex();
        int nextOption = Mathf.Clamp(currentOption + delta, 0, optionCount - 1);
        SetTrackSelectionByOption(nextOption);
    }

    private void SetTrackSelectionByOption(int optionIndex)
    {
        int clampedOption = Mathf.Clamp(optionIndex, 0, Mathf.Max(0, GetTrackOptionCount() - 1));

        if (clampedOption == 0)
        {
            useAutoTrackSelection = true;
            selectedMusicXmlPartId = string.Empty;
        }
        else
        {
            int summaryIndex = clampedOption - 1;
            if (summaryIndex < 0 || summaryIndex >= currentSongPartSummaries.Count)
                return;

            useAutoTrackSelection = false;
            selectedMusicXmlPartId = currentSongPartSummaries[summaryIndex].PartId;
        }

        ApplyTrackSelectionPreference();
        RefreshEffectiveAudioOffset();
        SaveSongMetadata();
    }

    private void ApplyTrackSelectionPreference()
    {
        int resolvedTrackIndex = -1;

        if (!useAutoTrackSelection && !string.IsNullOrEmpty(selectedMusicXmlPartId))
        {
            int matchedIndex = currentSongPartSummaries.FindIndex(summary => string.Equals(summary.PartId, selectedMusicXmlPartId, StringComparison.OrdinalIgnoreCase));
            if (matchedIndex >= 0)
            {
                resolvedTrackIndex = currentSongPartSummaries[matchedIndex].Index;
            }
            else
            {
                Debug.LogWarning($"[GuitarBridgeServer] Saved MusicXML part '{selectedMusicXmlPartId}' was not found in current score. Falling back to auto track selection.");
                useAutoTrackSelection = true;
                selectedMusicXmlPartId = string.Empty;
            }
        }

        midiTrackIndex = resolvedTrackIndex;
    }

    private void OpenSongSelectionMenu()
    {
        RefreshAvailableSongs();
        showMainMenu = false;
        showSongSelection = true;
        showTrackSelection = false;
        pendingTrackSelectionSong = null;
        pendingTrackSelectionParts.Clear();
        showSongSettings = false;
        showGlobalSettings = false;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);

        if (availableSongs.Count == 0)
        {
            selectedSongListIndex = 0;
            songListScrollOffset = 0;
            return;
        }

        int selectedIndex = availableSongs.FindIndex(song =>
            currentSongEntry != null &&
            string.Equals(song.SongDirectory, currentSongEntry.SongDirectory, StringComparison.OrdinalIgnoreCase));

        selectedSongListIndex = selectedIndex >= 0 ? selectedIndex : 0;
        EnsureSongSelectionVisible();
        SyncAudioToSongTimer(playImmediately: false);
    }

    private void RefreshAvailableSongs()
    {
        availableSongs.Clear();
        availableSongs.AddRange(SongLibraryService.GetAvailableSongs());
        availableSongs.Sort((a, b) =>
        {
            float scoreA = GetStoredSongBestScorePercent(a);
            float scoreB = GetStoredSongBestScorePercent(b);
            int scoreCompare = scoreB.CompareTo(scoreA);
            if (scoreCompare != 0)
                return scoreCompare;

            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        });

        if (currentSongEntry != null)
        {
            int currentIndex = availableSongs.FindIndex(song =>
                song != null &&
                string.Equals(song.SongDirectory, currentSongEntry.SongDirectory, StringComparison.OrdinalIgnoreCase));

            if (currentIndex >= 0)
                selectedSongListIndex = currentIndex;
        }

        if (selectedSongListIndex >= availableSongs.Count)
            selectedSongListIndex = Mathf.Max(0, availableSongs.Count - 1);
    }

    private void OpenTrackSelectionForSong(int songIndex)
    {
        if (songIndex < 0 || songIndex >= availableSongs.Count)
            return;

        SongLibraryEntry selected = availableSongs[songIndex];
        pendingTrackSelectionSong = selected;
        pendingTrackSelectionParts.Clear();
        pendingTrackSelectionParts.AddRange(GetSortedTrackSummaries(selected));

        selectedTrackListIndex = 0;
        trackListScrollOffset = 0;
        EnsureTrackSelectionVisible();

        showSongSelection = false;
        showTrackSelection = true;
    }

    private void CloseTrackSelection()
    {
        showTrackSelection = false;
        showMainMenu = false;
        showSongSelection = true;
        pendingTrackSelectionSong = null;
        pendingTrackSelectionParts.Clear();
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    private void MoveSongSelection(int delta)
    {
        if (availableSongs.Count == 0)
            return;

        selectedSongListIndex = Mathf.Clamp(selectedSongListIndex + delta, 0, availableSongs.Count - 1);
        EnsureSongSelectionVisible();
    }

    private void MoveTrackSelectionInMenu(int delta)
    {
        if (pendingTrackSelectionParts.Count == 0)
            return;

        selectedTrackListIndex = Mathf.Clamp(selectedTrackListIndex + delta, 0, pendingTrackSelectionParts.Count - 1);
        EnsureTrackSelectionVisible();
    }

    private void EnsureSongSelectionVisible()
    {
        const int visibleCount = 12;
        if (selectedSongListIndex < songListScrollOffset)
            songListScrollOffset = selectedSongListIndex;

        if (selectedSongListIndex >= songListScrollOffset + visibleCount)
            songListScrollOffset = selectedSongListIndex - visibleCount + 1;

        songListScrollOffset = Mathf.Clamp(songListScrollOffset, 0, Mathf.Max(0, availableSongs.Count - visibleCount));
    }

    private void EnsureTrackSelectionVisible()
    {
        const int visibleCount = 10;
        if (selectedTrackListIndex < trackListScrollOffset)
            trackListScrollOffset = selectedTrackListIndex;

        if (selectedTrackListIndex >= trackListScrollOffset + visibleCount)
            trackListScrollOffset = selectedTrackListIndex - visibleCount + 1;

        trackListScrollOffset = Mathf.Clamp(trackListScrollOffset, 0, Mathf.Max(0, pendingTrackSelectionParts.Count - visibleCount));
    }

    private void ConfirmTrackSelection()
    {
        if (pendingTrackSelectionSong == null || selectedTrackListIndex < 0 || selectedTrackListIndex >= pendingTrackSelectionParts.Count)
            return;

        MusicXmlLoader.MusicXmlPartSummary selectedTrack = pendingTrackSelectionParts[selectedTrackListIndex];
        SelectSongAndTrack(pendingTrackSelectionSong, selectedTrack.PartId);
    }

    private void SelectSongAndTrack(SongLibraryEntry songEntry, string selectedPartId)
    {
        if (songEntry == null)
            return;

        bool isCurrentSong = currentSongEntry != null && string.Equals(currentSongEntry.SongDirectory, songEntry.SongDirectory, StringComparison.OrdinalIgnoreCase);
        if (isCurrentSong)
        {
            showTrackSelection = false;
            showSongSelection = false;
            pendingTrackSelectionSong = null;
            pendingTrackSelectionParts.Clear();

            useAutoTrackSelection = false;
            selectedMusicXmlPartId = selectedPartId ?? string.Empty;
            ApplyTrackSelectionPreference();
            RefreshEffectiveAudioOffset();
            SaveSongMetadata();

            if (songSelectionOpenedFromSongEnd)
            {
                songSelectionOpenedFromSongEnd = false;
                RetrySongFromUi();
            }
            else if (songSelectionOpenedFromMainMenu || mainMenuFlowActive)
            {
                showMainMenu = false;
                mainMenuFlowActive = false;
                songSelectionOpenedFromMainMenu = false;
                ShowStartupTuningReminder(resumePlaybackAfterDismiss: true);
            }
            return;
        }

        LoadSongFromEntry(songEntry, selectedPartId);
        showTrackSelection = false;
        showSongSelection = false;
        showMainMenu = false;
        mainMenuFlowActive = false;
        pendingTrackSelectionSong = null;
        pendingTrackSelectionParts.Clear();
    }

    private void LoadSongFromEntry(SongLibraryEntry entry, string preferredPartId = null)
    {
        currentSongEntry = entry;
        if (entry != null)
        {
            int selectedIndex = availableSongs.FindIndex(song =>
                song != null &&
                string.Equals(song.SongDirectory, entry.SongDirectory, StringComparison.OrdinalIgnoreCase));
            if (selectedIndex >= 0)
            {
                selectedSongListIndex = selectedIndex;
                EnsureSongSelectionVisible();
            }
        }

        SaveSelectedSongPreference(entry);
        if (!string.IsNullOrEmpty(preferredPartId) && entry != null)
        {
            SongMetadata trackMetadata = LoadSongMetadata(Path.GetFileName(entry.Mp3Path));
            trackMetadata.useAutoTrackSelection = false;
            trackMetadata.selectedMusicXmlPartId = preferredPartId;
            SaveSongMetadata(trackMetadata, entry.MetadataPath, Path.GetFileName(entry.Mp3Path));
        }

        LoadTestSong();
        bool autoplayFromSongEnd = songSelectionOpenedFromSongEnd;
        bool autoplayFromMainMenuFlow = mainMenuFlowActive || songSelectionOpenedFromMainMenu;
        songSelectionOpenedFromSongEnd = false;
        songSelectionOpenedFromMainMenu = false;
        showMainMenu = false;
        mainMenuFlowActive = false;
        bool autoplay = autoplayFromSongEnd || autoplayFromMainMenuFlow;
        isPaused = !autoplay;
        if (autoplayFromMainMenuFlow)
            ShowStartupTuningReminder(resumePlaybackAfterDismiss: true);
        SeekSongTime(-songStartDelaySeconds, false);
        SyncAudioToSongTimer(playImmediately: autoplay && !showStartupTuningReminder);
    }

    private void HandleLoopPlayback()
    {
        if (renderMode == GuitarRenderMode.Highway3D)
            return;

        if (!loopEnabled || loopEndTime <= loopStartTime + 0.01f)
            return;

        if (songTimer < loopEndTime)
            return;

        SeekSongTime(loopStartTime, false);
    }


    public void ToggleLoopFromUi()
    {
        if (renderMode == GuitarRenderMode.Highway3D)
        {
            loopEnabled = false;
            return;
        }

        loopEnabled = !loopEnabled;
        if (loopEnabled && loopEndTime <= loopStartTime)
            loopEndTime = loopStartTime + 0.25f;
    }

    public void OpenMainMenuFromUi()
    {
        showMainMenu = true;
        mainMenuFlowActive = true;
        showSongSettings = false;
        showSongSelection = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        isPaused = true;
        songHasEnded = false;
        songSelectionOpenedFromSongEnd = false;
        songSelectionOpenedFromMainMenu = false;
        showStartupTuningReminder = false;
        resumeGameplayAfterStartupTuningReminder = false;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void ContinueFromMainMenuFromUi()
    {
        songHasEnded = false;
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSettings = false;
        showSongSelection = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        ShowStartupTuningReminder(resumePlaybackAfterDismiss: true);
    }

    public void OpenSongSelectionFromUi()
    {
        songSelectionOpenedFromSongEnd = false;
        songSelectionOpenedFromMainMenu = showMainMenu || mainMenuFlowActive;
        if (!showMainMenu)
            mainMenuFlowActive = false;
        OpenSongSelectionMenu();
    }

    private void ShowStartupTuningReminder(bool resumePlaybackAfterDismiss)
    {
        showStartupTuningReminder = true;
        resumeGameplayAfterStartupTuningReminder = resumePlaybackAfterDismiss;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void DismissStartupTuningReminderFromUi()
    {
        if (!showStartupTuningReminder)
            return;

        showStartupTuningReminder = false;
        bool shouldResume = resumeGameplayAfterStartupTuningReminder;
        resumeGameplayAfterStartupTuningReminder = false;

        if (shouldResume)
        {
            isPaused = false;
            SyncAudioToSongTimer(playImmediately: true);
        }
    }

    public void OpenSongSelectionFromSongEndFromUi()
    {
        songHasEnded = false;
        songSelectionOpenedFromSongEnd = true;
        songSelectionOpenedFromMainMenu = false;
        mainMenuFlowActive = false;
        OpenSongSelectionMenu();
    }
    public void RetrySongFromUi()
    {
        songHasEnded = false;
        songSelectionOpenedFromSongEnd = false;
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSelection = false;
        showTrackSelection = false;
        showSongSettings = false;
        showGlobalSettings = false;
        isPaused = false;
        SeekSongTime(-songStartDelaySeconds, false);
        SyncAudioToSongTimer(playImmediately: true);
    }

    public void OpenSongSettingsFromUi()
    {
        showSongSettings = true;
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSelection = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        isPaused = true;
    }
    public void OpenGlobalSettingsFromUi()
    {
        if (!showMainMenu)
            mainMenuFlowActive = false;

        showGlobalSettings = true;
        showSongSettings = false;
        showMainMenu = false;
        showSongSelection = false;
        showTrackSelection = false;
        isPaused = true;
    }
    public void OpenToneLabFromUi()
    {
        OpenOrFocusToneLab();
    }

    public void ExitGameFromUi()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void SetPlaybackSpeedPercentFromUi(float speedPercent)
    {
        playbackSpeedPercent = Mathf.Clamp(speedPercent, 1f, 200f);
    }



    public void OpenSongsFolderFromUi()
    {
        string songsDirectory = ExternalContentPaths.PersistentSongsDirectory;

        try
        {
            Directory.CreateDirectory(songsDirectory);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            System.Diagnostics.Process.Start("explorer.exe", songsDirectory.Replace('/', '\\'));
#else
            Application.OpenURL($"file://{songsDirectory}");
#endif
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to open songs folder: {ex.Message}");
            Application.OpenURL($"file://{songsDirectory}");
        }
    }

    public void RefreshSongsFromUi()
    {
        string selectedDirectory =
            selectedSongListIndex >= 0 &&
            selectedSongListIndex < availableSongs.Count
                ? availableSongs[selectedSongListIndex]?.SongDirectory
                : null;

        RefreshAvailableSongs();

        if (!string.IsNullOrEmpty(selectedDirectory))
        {
            int idx = availableSongs.FindIndex(song =>
                song != null &&
                string.Equals(song.SongDirectory, selectedDirectory, StringComparison.OrdinalIgnoreCase));

            if (idx >= 0)
                selectedSongListIndex = idx;
        }

        if (selectedSongListIndex >= availableSongs.Count)
            selectedSongListIndex = Mathf.Max(0, availableSongs.Count - 1);

        EnsureSongSelectionVisible();
    }

    public void MoveSongSelectionFromUi(int delta)
    {
        MoveSongSelection(delta);
    }

    public void SelectSongByIndexFromUi(int songIndex)
    {
        selectedSongListIndex = Mathf.Clamp(songIndex, 0, Mathf.Max(0, availableSongs.Count - 1));
        EnsureSongSelectionVisible();
        OpenTrackSelectionForSong(selectedSongListIndex);
    }

    public void CloseSongSelectionFromUi()
    {
        showSongSelection = false;
        showTrackSelection = false;
        showMainMenu = mainMenuFlowActive;
        songSelectionOpenedFromMainMenu = false;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }
    public void MoveTrackSelectionFromUiList(int delta)
    {
        MoveTrackSelectionInMenu(delta);
    }

    public void SelectTrackByIndexFromUi(int trackIndex)
    {
        selectedTrackListIndex = Mathf.Clamp(trackIndex, 0, Mathf.Max(0, pendingTrackSelectionParts.Count - 1));
        EnsureTrackSelectionVisible();
        ConfirmTrackSelection();
    }

    public void BackToSongSelectionFromUi()
    {
        CloseTrackSelection();
    }

    public void CloseSongSettingsFromUi()
    {
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSettings = false;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void CloseGlobalSettingsFromUi()
    {
        showGlobalSettings = false;
        showMainMenu = mainMenuFlowActive;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void SetGlobalRuntimeSettingFromUi(string settingId, string serializedValue)
    {
        ApplyRuntimeSettingValue(settingId, serializedValue, saveMetadata: true);
    }

    public void ResetGlobalSettingsToDefaultsFromUi()
    {
        ApplyDefaultRuntimeSettings();
        SaveGlobalRuntimeSettingsMetadata();
    }

    public void ToggleOffsetScopeFromUi()
    {
        ToggleOffsetScope();
        SaveSongMetadata();
        SyncAudioToSongTimer(playImmediately: !isPaused);
    }

    public void SetAudioOffsetMsFromUi(float offsetMs)
    {
        SetEffectiveOffsetForCurrentScope(Mathf.Clamp(offsetMs, -2000f, 2000f));
        SaveSongMetadata();
        SyncAudioToSongTimer(playImmediately: !isPaused);
    }

    public void SetTabSpeedOffsetPercentFromUi(float percent)
    {
        tabSpeedOffsetPercent = Mathf.Clamp(percent, 50f, 150f);
        SaveSongMetadata();
    }

    public void SetSongStartDelaySecondsFromUi(float seconds)
    {
        songStartDelaySeconds = Mathf.Clamp(seconds, 0f, 8f);
        SaveSongMetadata();
    }

    public void MoveTrackSelectionFromUi(int delta)
    {
        MoveTrackSelection(delta);
    }

    public void ResumePlaybackFromUi()
    {
        songHasEnded = false;
        isPaused = false;
        showSongSettings = false;
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSelection = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        SyncAudioToSongTimer(playImmediately: true);
    }






private void OpenOrFocusToneLab()
{
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    const string toneLabWindowTitle = "Tone Lab";

    if (!ToneLabService.EnsureToneLabRuntimeFiles())
        return;

    string toneLabPath = ToneLabService.GetToneLabExecutablePath();
    string toneLabWorkingDirectory = Path.GetDirectoryName(toneLabPath);
    Debug.Log($"[ToneLab] Launch requested. Runtime executable path: {toneLabPath}");

    try
    {
        IntPtr existing = FindWindow(null, toneLabWindowTitle);
        if (existing != IntPtr.Zero)
        {
            ShowWindow(existing, SW_RESTORE);
            SetForegroundWindow(existing);
            CenterWindowOnUnityDisplay(existing);
            return;
        }

        if (!File.Exists(toneLabPath))
        {
            Debug.LogWarning($"Tone Lab executable not found at runtime path '{toneLabPath}'.");
            return;
        }

        if (TryStartToneLabProcess(toneLabPath, string.Empty, toneLabWorkingDirectory, false))
        {
            StartCoroutine(FocusToneLabWindowWhenReady(toneLabWindowTitle));
            return;
        }

        Debug.LogWarning("[ToneLab] Failed to launch Tone Lab executable.");
    }
    catch (Exception ex)
    {
        Debug.LogWarning($"Failed to launch Tone Lab: {ex}");
    }
#else
    Debug.LogWarning("Tone Lab launcher is currently implemented for Windows builds only.");
#endif
}

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private bool TryStartToneLabProcess(string fileName, string arguments, string workingDirectory, bool useShellExecute)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = useShellExecute,
                CreateNoWindow = false
            };

            System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                Debug.Log($"[ToneLab] Launched using '{fileName} {arguments}' (UseShellExecute={useShellExecute}).");
                return true;
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            Debug.LogWarning($"[ToneLab] Launch canceled by user/UAC for '{fileName}'. If ToneLab.exe requests admin privileges, remove that requirement from the executable manifest.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ToneLab] Launch attempt failed: '{fileName} {arguments}'. {ex.Message}");
        }

        return false;
    }

    private System.Collections.IEnumerator FocusToneLabWindowWhenReady(string windowTitle)
    {
        const int attempts = 60;
        for (int i = 0; i < attempts; i++)
        {
            IntPtr window = FindWindow(null, windowTitle);
            if (window != IntPtr.Zero)
            {
                ShowWindow(window, SW_RESTORE);
                SetForegroundWindow(window);
                CenterWindowOnUnityDisplay(window);
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.1f);
        }
    }

    private void CenterWindowOnUnityDisplay(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero || Camera.main == null)
            return;

        IntPtr unityWindow = GetForegroundWindow();
        if (unityWindow == IntPtr.Zero)
            return;

        if (!GetWindowRect(unityWindow, out RECT unityRect) || !GetWindowRect(targetWindow, out RECT targetRect))
            return;

        int unityCenterX = unityRect.Left + ((unityRect.Right - unityRect.Left) / 2);
        int unityCenterY = unityRect.Top + ((unityRect.Bottom - unityRect.Top) / 2);
        int targetWidth = targetRect.Right - targetRect.Left;
        int targetHeight = targetRect.Bottom - targetRect.Top;

        int targetX = unityCenterX - (targetWidth / 2);
        int targetY = unityCenterY - (targetHeight / 2);

        SetWindowPos(targetWindow, IntPtr.Zero, targetX, targetY, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private const int SW_RESTORE = 9;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
#endif


    private string GetCurrentOffsetPartId()
    {
        if (useAutoTrackSelection)
        {
            if (currentSongPartSummaries.Count == 0)
                return string.Empty;

            MusicXmlLoader.MusicXmlPartSummary best = currentSongPartSummaries.OrderByDescending(s => s.Score).FirstOrDefault();
            return best != null ? (best.PartId ?? string.Empty) : string.Empty;
        }

        return selectedMusicXmlPartId ?? string.Empty;
    }

    private TrackOffsetOverride GetOrCreateTrackOffsetOverride(string partId)
    {
        if (songMetadata.trackOffsetOverrides == null)
            songMetadata.trackOffsetOverrides = new List<TrackOffsetOverride>();

        TrackOffsetOverride existing = songMetadata.trackOffsetOverrides.FirstOrDefault(o => string.Equals(o.partId ?? string.Empty, partId ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        TrackOffsetOverride created = new TrackOffsetOverride
        {
            partId = partId ?? string.Empty,
            useTrackOffset = false,
            offsetMs = globalAudioOffsetMs
        };

        songMetadata.trackOffsetOverrides.Add(created);
        return created;
    }

    private void RefreshEffectiveAudioOffset()
    {
        string partId = GetCurrentOffsetPartId();
        TrackOffsetOverride entry = null;

        if (!string.IsNullOrEmpty(partId) && songMetadata.trackOffsetOverrides != null)
            entry = songMetadata.trackOffsetOverrides.FirstOrDefault(o => string.Equals(o.partId ?? string.Empty, partId, StringComparison.OrdinalIgnoreCase));

        useTrackOffsetForCurrentTrack = entry != null && entry.useTrackOffset;
        audioOffsetMs = useTrackOffsetForCurrentTrack ? entry.offsetMs : globalAudioOffsetMs;
    }

    private void SetEffectiveOffsetForCurrentScope(float offsetMs)
    {
        if (useTrackOffsetForCurrentTrack)
        {
            string partId = GetCurrentOffsetPartId();
            TrackOffsetOverride entry = GetOrCreateTrackOffsetOverride(partId);
            entry.useTrackOffset = true;
            entry.offsetMs = offsetMs;
            audioOffsetMs = offsetMs;
            return;
        }

        globalAudioOffsetMs = offsetMs;
        audioOffsetMs = offsetMs;
    }

    private void ToggleOffsetScope()
    {
        string partId = GetCurrentOffsetPartId();
        if (string.IsNullOrEmpty(partId))
            return;

        TrackOffsetOverride entry = GetOrCreateTrackOffsetOverride(partId);
        entry.useTrackOffset = !entry.useTrackOffset;

        if (entry.useTrackOffset)
        {
            if (Mathf.Abs(entry.offsetMs) < 0.0001f)
                entry.offsetMs = globalAudioOffsetMs;
            useTrackOffsetForCurrentTrack = true;
            audioOffsetMs = entry.offsetMs;
        }
        else
        {
            useTrackOffsetForCurrentTrack = false;
            audioOffsetMs = globalAudioOffsetMs;
        }
    }





    private float GetPlaybackSpeedScale()
    {
        return Mathf.Clamp(playbackSpeedPercent / 100f, 0.01f, 2f);
    }

    private float GetTabPlaybackSpeedScale()
    {
        return Mathf.Clamp(GetPlaybackSpeedScale() * (tabSpeedOffsetPercent / 100f), 0.01f, 4f);
    }

    private void SeekSongTime(float targetTime, bool updateSelectedMarker)
    {
        float previousTime = songTimer;
        float clampedTime = Mathf.Max(-songStartDelaySeconds, targetTime);
        songTimer = clampedTime;
        audioSongTimer = clampedTime;

        if (updateSelectedMarker)
            UpdateSelectedLoopMarker(clampedTime);

        bool isRewinding = clampedTime < previousTime;
        if (isRewinding)
        {
            for (int i = 0; i < noteStates.Count; i++)
            {
                GameplayNoteState noteState = noteStates[i];
                if (noteState.data.time > songTimer)
                {
                    noteState.result = GameplayNoteResult.Pending;
                    noteState.resolvedAt = -1f;
                    noteState.isJudgeable = false;
                }
            }
        }
        else
        {
            for (int i = 0; i < noteStates.Count; i++)
            {
                GameplayNoteState noteState = noteStates[i];
                if (noteState.IsResolved)
                    continue;

                float latestJudgeTime = noteState.data.time + hitWindowLate + judgmentGrace;
                if (clampedTime > latestJudgeTime + (noteState.data.stringIdx >= 4 ? highStringExtraLate : 0f))
                {
                    noteState.result = GameplayNoteResult.Missed;
                    noteState.resolvedAt = clampedTime;
                    noteState.isJudgeable = false;
                }
            }
        }

        recentNoteEvents.Clear();
        latestDetectedPitches.Clear();
        latestEventNotesText = "--";
        latestNoteEventId = 0;
        latestPacketHadEvent = false;
        Interlocked.Exchange(ref lastUdpPacketUtcTicks, 0L);
        SyncAudioToSongTimer(playImmediately: !isPaused);
        UpdateSongEndState();
    }

    private void UpdateSelectedLoopMarker(float markerTime)
    {
        if (selectedLoopMarker == 1)
        {
            loopStartTime = Mathf.Max(0f, markerTime);
            if (loopEndTime < loopStartTime + 0.05f)
                loopEndTime = loopStartTime + 0.05f;
        }
        else
        {
            loopEndTime = Mathf.Max(loopStartTime + 0.05f, markerTime);
        }
    }

    private void OnApplicationQuit()
    {
        isRunning = false;
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Join(500);
        if (udpClient != null) udpClient.Close();
        ShutdownNotesDetectorIfRunning();
    }

    private void OnDestroy()
    {
        ShutdownNotesDetectorIfRunning();
    }

    private void OnDisable()
    {
        ShutdownNotesDetectorIfRunning();
    }

    private void TryLaunchNotesDetector()
    {
        Debug.Log("[NotesDetector] TryLaunchNotesDetector() invoked.");

        if (!autoLaunchNotesDetector)
        {
            Debug.Log("[NotesDetector] Auto-launch is disabled in inspector; skipping launch.");
            return;
        }

        if (notesDetectorProcess != null && !notesDetectorProcess.HasExited)
        {
            Debug.Log($"[NotesDetector] Process is already running (PID {notesDetectorProcess.Id}); skipping launch.");
            return;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        string detectorPath = Path.Combine(Application.streamingAssetsPath, notesDetectorRelativePath);
        Debug.Log($"[NotesDetector] Resolved executable path: {detectorPath}");

        if (!File.Exists(detectorPath))
        {
            Debug.LogWarning($"[NotesDetector] Executable not found at: {detectorPath}");
            return;
        }

        try
        {
            string detectorWorkingDirectory = Path.GetDirectoryName(detectorPath);
            Debug.Log($"[NotesDetector] Launching from working directory: {detectorWorkingDirectory}");

            System.Diagnostics.ProcessStartInfo startInfo;

#if UNITY_EDITOR_WIN
            if (openNotesDetectorConsoleWindowInEditor)
            {
                startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/k \"{detectorPath}\"",
                    WorkingDirectory = detectorWorkingDirectory,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                };
                Debug.Log("[NotesDetector] Launch mode: cmd.exe /k (visible console window).");
            }
            else
#endif
            {
                startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = detectorPath,
                    Arguments = string.Empty,
                    WorkingDirectory = detectorWorkingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                };
                Debug.Log("[NotesDetector] Launch mode: direct process start.");
            }

            notesDetectorProcess = System.Diagnostics.Process.Start(startInfo);
            if (notesDetectorProcess != null)
                Debug.Log($"[NotesDetector] Launched successfully (PID {notesDetectorProcess.Id}): {detectorPath}");
            else
                Debug.LogWarning("[NotesDetector] Process.Start returned null process handle.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NotesDetector] Failed to launch '{detectorPath}': {ex}");
        }
#else
        Debug.Log($"[NotesDetector] Auto-launch is currently only enabled on Windows. Current platform: {Application.platform}");
#endif
    }

    private void ShutdownNotesDetectorIfRunning()
    {
        if (notesDetectorProcess == null)
            return;

        try
        {
            if (!notesDetectorProcess.HasExited)
                TryKillProcessTree(notesDetectorProcess);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NotesDetector] Failed to stop process cleanly: {ex.Message}");
        }
        finally
        {
            notesDetectorProcess.Dispose();
            notesDetectorProcess = null;
        }
    }

    private static void TryKillProcessTree(System.Diagnostics.Process process)
    {
        if (process == null)
            return;

        try
        {
            System.Reflection.MethodInfo killTreeMethod = typeof(System.Diagnostics.Process).GetMethod("Kill", new[] { typeof(bool) });
            if (killTreeMethod != null)
            {
                killTreeMethod.Invoke(process, new object[] { true });
                return;
            }
        }
        catch (Exception)
        {
            // Fall back to regular kill when process-tree termination is unavailable or unsupported.
        }

        process.Kill();
    }

    public Color GetStringColor(int stringIdx)
    {
        if (stringIdx < 0 || stringIdx >= stringColors.Length) return Color.white;
        return stringColors[stringIdx];
    }

    public Color GetDarkenedStringColor(int stringIdx, float multiplier)
    {
        Color baseColor = GetStringColor(stringIdx);
        float h, s, v;
        Color.RGBToHSV(baseColor, out h, out s, out v);
        return Color.HSVToRGB(h, s, v * multiplier);
    }

    public Material CreateSharedGlowMaterial(Color c, float intensity)
    {
        bool isURP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        Shader shader = ResolveFirstAvailableShader(
            isURP
                ? new[]
                {
                    "Universal Render Pipeline/Lit",
                    "Universal Render Pipeline/Simple Lit",
                    "Universal Render Pipeline/Unlit",
                    "Unlit/Color",
                    "Sprites/Default",
                    "Standard",
                    "Hidden/InternalErrorShader"
                }
                : new[]
                {
                    "Standard",
                    "Legacy Shaders/Diffuse",
                    "Unlit/Color",
                    "Sprites/Default",
                    "Hidden/InternalErrorShader"
                });

        Material m = shader != null ? new Material(shader) : CreateMaterialFromPrimitiveFallback("glow");
        m.color = c;
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.SetColor("_EmissionColor", intensity > 0f ? c * Mathf.Pow(2f, intensity) : Color.black);
        return m;
    }

    public Material CreateSharedTransparentMaterial(Color c, float emission = 0f)
    {
        Shader shader = ResolveFirstAvailableShader(
            "Sprites/Default",
            "Unlit/Transparent",
            "Universal Render Pipeline/Unlit",
            "Unlit/Color",
            "Standard",
            "Hidden/InternalErrorShader");

        Material m = shader != null ? new Material(shader) : CreateMaterialFromPrimitiveFallback("transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        m.SetColor("_Color", c);
        m.SetColor("_BaseColor", c);
        m.color = c;

        m.SetInt("_ZWrite", 0);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        m.EnableKeyword("_ALPHABLEND_ON");

        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", c * Mathf.Pow(2f, emission));
        }

        return m;
    }

    private static Material CreateMaterialFromPrimitiveFallback(string materialKind)
    {
        GameObject primitive = null;
        try
        {
            primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Renderer renderer = primitive.GetComponent<Renderer>();
            Material source = renderer != null ? renderer.sharedMaterial : null;
            if (source != null)
            {
                Debug.LogWarning($"[GuitarBridgeServer] Falling back to primitive default shader for {materialKind} material.");
                return new Material(source);
            }
        }
        finally
        {
            if (primitive != null)
                UnityEngine.Object.Destroy(primitive);
        }

        throw new InvalidOperationException($"Unable to create {materialKind} material because no shader and no primitive fallback material were available.");
    }

    private static Shader ResolveFirstAvailableShader(params string[] shaderNames)
    {
        if (shaderNames == null)
            return null;

        for (int i = 0; i < shaderNames.Length; i++)
        {
            string shaderName = shaderNames[i];
            if (string.IsNullOrWhiteSpace(shaderName))
                continue;

            Shader found = Shader.Find(shaderName);
            if (found != null)
                return found;
        }

        return null;
    }

    public int GetStringBasePitch(int stringIdx)
    {
        if (stringIdx < 0 || stringIdx >= stringBasePitch.Length) return 0;
        return stringBasePitch[stringIdx];
    }

    public bool TryGetChartNoteById(int id, out NoteData data)
    {
        return chartNoteById.TryGetValue(id, out data);
    }

    public bool TryGetNoteStateById(int id, out GameplayNoteState state)
    {
        for (int i = 0; i < noteStates.Count; i++)
        {
            if (noteStates[i].data.id == id)
            {
                state = noteStates[i];
                return true;
            }
        }

        state = null;
        return false;
    }

    // =========================================================
    // THE CORE HIT DETECTION ENGINE
    // =========================================================
    private void UpdateGameplayStates()
    {
        for (int i = 0; i < noteStates.Count; i++)
        {
            GameplayNoteState noteState = noteStates[i];

            if (noteState.IsResolved)
            {
                noteState.isJudgeable = false;
                continue;
            }

            noteState.isJudgeable = IsNoteJudgeableNow(noteState);

            if (songTimer < noteState.data.time - hitWindowEarly)
                continue;

            NoteEvent matchedEvent;
            int consumeKey;
            float matchedEventTime;

            bool matched = false;

            if (noteState.data.requiresPluck)
            {
                matched = TryFindMatchingNoteEvent(noteState, out matchedEvent, out consumeKey, out matchedEventTime) ||
                          TryFindHighStringSupportEvent(noteState, out matchedEvent, out consumeKey);

                if (matched)
                {
                    matchedEvent.consumedKeys.Add(consumeKey);
                }
            }
            else
            {
                matched = TryFindLegatoMatch(noteState, out matchedEventTime);
                matchedEvent = null;
                consumeKey = -1;
            }

            if (matched)
            {
                noteState.result = GameplayNoteResult.Hit;
                noteState.resolvedAt = songTimer;
                noteState.isJudgeable = false;
                continue;
            }

            float latestJudgeTime = noteState.data.time + hitWindowLate + judgmentGrace;
            if (songTimer > latestJudgeTime + (noteState.data.stringIdx >= 4 ? highStringExtraLate : 0f))
            {
                noteState.result = GameplayNoteResult.Missed;
                noteState.resolvedAt = songTimer;
                noteState.isJudgeable = false;
                LogMissReason(noteState);
            }
        }
    }

    private bool TryFindMatchingNoteEvent(GameplayNoteState note, out NoteEvent matchedEvent, out int consumeKey, out float matchedEventTime)
    {
        matchedEvent = null;
        consumeKey = -1;
        matchedEventTime = -999f;

        float extraEarly = note.data.stringIdx >= 4 ? highStringExtraEarly : 0f;
        float extraLate = note.data.stringIdx >= 4 ? highStringExtraLate : 0f;

        float windowStart = note.data.time - eventMatchEarly - eventTimeSlack - extraEarly;
        float windowEnd = note.data.time + eventMatchLate + eventTimeSlack + extraLate;

        int exactTargetPitch = stringBasePitch[note.data.stringIdx] + note.data.fret;
        int targetPitchModulo = exactTargetPitch % 12; 
        float bestDistance = float.MaxValue;

        for (int i = recentNoteEvents.Count - 1; i >= 0; i--)
        {
            NoteEvent ev = recentNoteEvents[i];
            
            if (ev.time < windowStart) break;
            if (ev.time > windowEnd) continue;

            if (!ev.pitches.Any(p => p % 12 == targetPitchModulo)) continue;
            if (ev.consumedKeys.Contains(exactTargetPitch)) continue; 

            float distance = Mathf.Abs(ev.time - note.data.time);
            if (distance >= bestDistance) continue;

            matchedEvent = ev;
            consumeKey = exactTargetPitch;
            matchedEventTime = ev.time;
            bestDistance = distance;
        }

        return matchedEvent != null;
    }

    private bool TryFindLegatoMatch(GameplayNoteState note, out float matchedTime)
    {
        matchedTime = -999f;

        if (note.data.linkedFromNoteId >= 0)
        {
            GameplayNoteState sourceState;
            if (!TryGetNoteStateById(note.data.linkedFromNoteId, out sourceState) || !sourceState.IsHit)
                return false;
        }

        int exactTargetPitch = stringBasePitch[note.data.stringIdx] + note.data.fret;
        int targetPitchModulo = exactTargetPitch % 12;

        float windowStart = note.data.time - eventMatchEarly - eventTimeSlack;
        float windowEnd = note.data.time + eventMatchLate + eventTimeSlack + 0.1f;

        if (songTimer >= windowStart && songTimer <= windowEnd)
        {
            if (latestDetectedPitches.Contains(exactTargetPitch) || latestDetectedPitches.Any(p => p % 12 == targetPitchModulo))
            {
                matchedTime = songTimer;
                return true;
            }
        }

        for (int i = recentNoteEvents.Count - 1; i >= 0; i--)
        {
            NoteEvent ev = recentNoteEvents[i];
            if (ev.time < windowStart)
                break;
            if (ev.time > windowEnd)
                continue;

            if (ev.pitches.Contains(exactTargetPitch) || ev.pitches.Any(p => p % 12 == targetPitchModulo))
            {
                matchedTime = ev.time;
                return true;
            }
        }

        return false;
    }

    private bool TryFindHighStringSupportEvent(GameplayNoteState note, out NoteEvent supportEvent, out int rescueConsumeKey)
    {
        supportEvent = null;
        rescueConsumeKey = -1;

        if (!allowHighStringActiveRescue || note.data.stringIdx < 4) return false;

        int exactTargetPitch = stringBasePitch[note.data.stringIdx] + note.data.fret;
        int targetPitchModulo = exactTargetPitch % 12;

        float windowStart = note.data.time - highStringRescueTightWindow - eventTimeSlack;
        float windowEnd = note.data.time + highStringRescueTightWindow + eventTimeSlack;

        rescueConsumeKey = 500000 + (exactTargetPitch * 8) + note.data.stringIdx;

        for (int i = recentNoteEvents.Count - 1; i >= 0; i--)
        {
            NoteEvent ev = recentNoteEvents[i];
            if (ev.time < windowStart) break;
            if (ev.time > windowEnd) continue;

            if (!ev.pitches.Any(p => p % 12 == targetPitchModulo)) continue;
            if (ev.consumedKeys.Contains(rescueConsumeKey) || ev.consumedKeys.Contains(exactTargetPitch)) continue;

            bool closeEnough = Mathf.Abs(ev.time - note.data.time) <= highStringRescueTightWindow;
            bool chordish = ev.pitches.Count >= 2;

            if (closeEnough || chordish)
            {
                supportEvent = ev;
                return true;
            }
        }
        return false;
    }

    private void LogMissReason(GameplayNoteState noteState)
    {
        float windowStart = noteState.data.time - eventMatchEarly - eventTimeSlack;
        float windowEnd = noteState.data.time + eventMatchLate + eventTimeSlack;
        
        int exactTargetPitch = stringBasePitch[noteState.data.stringIdx] + noteState.data.fret;
        string targetNoteName = GetNoteNameFromMidi(exactTargetPitch);

        List<string> heardNotesInWindow = new List<string>();
        foreach (var ev in recentNoteEvents)
        {
            if (ev.time >= windowStart && ev.time <= windowEnd)
            {
                heardNotesInWindow.AddRange(ev.pitches.Select(p => GetNoteNameFromMidi(p)));
            }
        }

        if (heardNotesInWindow.Count > 0)
        {
            string heardList = string.Join(", ", heardNotesInWindow.Distinct());
            Debug.LogWarning($"<color=red>MISSED NOTE:</color> Expected <b>{targetNoteName}</b> at {noteState.data.time:F2}s. \n<color=yellow>AI Heard:</color> [{heardList}] during this window.");
        }
        else
        {
            Debug.LogWarning($"<color=red>MISSED NOTE:</color> Expected <b>{targetNoteName}</b> at {noteState.data.time:F2}s. \n<color=yellow>Reason:</color> No pluck events detected at this time.");
        }
    }

    // =========================================================
    // NETWORKING AND DATA PARSING
    // =========================================================
    private void StartUdpThread()
    {
        receiveThread = new Thread(new ThreadStart(ReceiveUdpData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    private void ReceiveUdpData()
    {
        try
        {
            udpClient = new UdpClient();
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, udpPort));

            while (isRunning)
            {
                if (udpClient.Available > 0)
                {
                    IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udpClient.Receive(ref anyIP);
                    logNotes = Encoding.UTF8.GetString(data);
                    Interlocked.Exchange(ref lastUdpPacketUtcTicks, DateTime.UtcNow.Ticks);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }
        catch (Exception e) { Debug.LogWarning("UDP Error: " + e.Message); }
    }

private void ParseUdpState()
    {
        latestDetectedPitches.Clear();
        latestPacketHadEvent = false;
        latestEventNotesText = "--";
        latestParsedInputLevel = -1f;

        if (string.IsNullOrEmpty(logNotes) || logNotes == "--") return;

        if (logNotes.StartsWith("A|"))
        {
            string[] parts = logNotes.Split('|');
            if (parts.Length < 5) return;

            ParseNoteCsvIntoSet(parts[1], latestDetectedPitches);

            int.TryParse(parts[2], out int eventId);
            float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float eventAge);
            string eventCsv = parts[4];
            if (parts.Length >= 6 && float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedLevel))
            {
                if (parsedLevel > 1f)
                    parsedLevel /= 100f;
                latestParsedInputLevel = Mathf.Clamp01(parsedLevel);
            }
            
            latestEventNotesText = string.IsNullOrWhiteSpace(eventCsv) ? "--" : eventCsv;

            // --- BRUTE FORCE LOGGING ---
            // If the event ID is anything greater than 0, force a yellow warning log
            if (eventId > 0)
            {
                Debug.LogWarning($"<color=cyan>[RAW UDP RECEIVED]</color> {logNotes}  ||  Parsed ID: {eventId}, Parsed Age: {eventAge:F3}");
            }

            if (eventId <= 0 || string.IsNullOrWhiteSpace(eventCsv) || eventCsv == "--") return;

            float eventAgeInSongTime = Mathf.Max(0f, eventAge) * GetTabPlaybackSpeedScale();
            float estimatedEventTime = Mathf.Max(0f, songTimer - eventAgeInSongTime);
            
            // Log exactly what timestamp Unity is assigning this event on the timeline
            if (TryStoreNoteEvent(eventId, estimatedEventTime, eventCsv, out NoteEvent ev))
            {
                latestPacketHadEvent = true;
                latestEventNotesText = FormatMidiSetCsv(ev.pitches);
                latestNoteEventId = Mathf.Max(latestNoteEventId, eventId);
                
                Debug.LogWarning($"<color=green>[EVENT STORED]</color> Pluck {eventId} saved at Timeline: {estimatedEventTime:F3}s. Current Game Time: {songTimer:F3}s");
            }
        }
    }

    private bool TryStoreNoteEvent(int id, float timeStamp, string csv, out NoteEvent storedEvent)
    {
        storedEvent = null;
        HashSet<int> pitches = new HashSet<int>();
        ParseNoteCsvIntoSet(csv, pitches);
        if (pitches.Count == 0) return false;

        for (int i = recentNoteEvents.Count - 1; i >= 0; i--)
        {
            NoteEvent existing = recentNoteEvents[i];
            if (existing.id == id)
            {
                int beforeCount = existing.pitches.Count;
                existing.pitches.UnionWith(pitches);
                existing.time = Mathf.Min(existing.time, timeStamp);
                storedEvent = existing;
                return existing.pitches.Count > beforeCount;
            }
        }

        NoteEvent newEv = new NoteEvent { id = id, time = timeStamp, pitches = pitches };
        recentNoteEvents.Add(newEv);
        storedEvent = newEv;
        return true;
    }

    private void ParseNoteCsvIntoSet(string csv, HashSet<int> targetSet)
    {
        if (string.IsNullOrWhiteSpace(csv) || csv == "--") return;
        string[] parts = csv.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            int midi = ParseNoteToMidi(parts[i].Trim());
            if (midi != -1) targetSet.Add(midi);
        }
    }

    // =========================================================
    // UTILS & BOILERPLATE
    // =========================================================
    private void PruneHistory()
    {
        float cutoff = songTimer - 3.0f;
        recentNoteEvents.RemoveAll(e => e.time < cutoff);
    }

    private bool IsNoteJudgeableNow(GameplayNoteState noteState)
    {
        float start = noteState.data.time - hitWindowEarly;
        float end = noteState.data.time + hitWindowLate;
        return songTimer >= start && songTimer <= end;
    }

    private int ParseNoteToMidi(string noteStr)
    {
        if (string.IsNullOrEmpty(noteStr)) return -1;
        Match match = Regex.Match(noteStr, @"([A-G]#?b?)(-?\d+)");
        if (!match.Success) return -1;
        string name = match.Groups[1].Value;
        int octave = int.Parse(match.Groups[2].Value);
        if (noteToIndex.TryGetValue(name, out int pitchClass))
            return (octave + 1) * 12 + pitchClass;
        return -1;
    }

    private string GetNoteNameFromMidi(int midi)
    {
        string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#" , "A", "A#", "B" };
        if (midi < 12) return "---";
        int octave = (midi / 12) - 1;
        int pitchClass = midi % 12;
        return $"{names[pitchClass]}{octave}";
    }

    private string FormatMidiSetCsv(HashSet<int> pitches)
    {
        if (pitches.Count == 0) return "--";
        var sorted = pitches.ToList();
        sorted.Sort();
        return string.Join(",", sorted.Select(GetNoteNameFromMidi));
    }

    private void BuildNoteIndices()
    {
        noteToIndex["C"] = 0; noteToIndex["C#"] = 1; noteToIndex["Db"] = 1;
        noteToIndex["D"] = 2; noteToIndex["D#"] = 3; noteToIndex["Eb"] = 3;
        noteToIndex["E"] = 4;
        noteToIndex["F"] = 5; noteToIndex["F#"] = 6; noteToIndex["Gb"] = 6;
        noteToIndex["G"] = 7; noteToIndex["G#"] = 8; noteToIndex["Ab"] = 8;
        noteToIndex["A"] = 9; noteToIndex["A#"] = 10; noteToIndex["Bb"] = 10;
        noteToIndex["B"] = 11;
    }

    private float GetEffectiveTabSectionDuration()
    {
        return Mathf.Max(0.25f, tabSectionDuration * Mathf.Max(0.5f, tabSectionLengthMultiplier));
    }

    private int GetSectionIndex(float time)
    {
        float sectionDuration = GetEffectiveTabSectionDuration();
        if (sectionDuration <= 0.05f) return 0;
        return Mathf.FloorToInt(time / sectionDuration);
    }

    private bool IsNoteDetectorConnected()
    {
        long lastTicks = Interlocked.Read(ref lastUdpPacketUtcTicks);
        if (lastTicks <= 0)
            return false;

        DateTime lastUtc = new DateTime(lastTicks, DateTimeKind.Utc);
        return (DateTime.UtcNow - lastUtc).TotalSeconds <= DetectorConnectionTimeoutSeconds;
    }

    private void UpdateInputLevelEstimate()
    {
        float target = 0f;

        if (IsNoteDetectorConnected())
        {
            float derived = Mathf.Clamp01(latestDetectedPitches.Count / 6f);

            if (latestPacketHadEvent)
                derived = Mathf.Max(derived, 0.95f);

            if (recentNoteEvents.Count > 0)
            {
                NoteEvent lastEvent = recentNoteEvents[recentNoteEvents.Count - 1];
                float age = Mathf.Max(0f, songTimer - lastEvent.time);
                float transient = Mathf.Clamp01(1f - (age / 0.35f));
                if (transient > 0f)
                    derived = Mathf.Max(derived, Mathf.Lerp(0.22f, 0.9f, transient));
            }

            target = latestParsedInputLevel >= 0f
                ? Mathf.Max(latestParsedInputLevel, derived)
                : derived;
        }

        float rise = 6.5f;
        float fall = 2.0f;
        float rate = target > smoothedInputLevel ? rise : fall;
        smoothedInputLevel = Mathf.MoveTowards(smoothedInputLevel, target, Time.deltaTime * rate);
    }


    private float GetSongDurationSeconds()
    {
        if (backingTrackSource != null && backingTrackSource.clip != null)
            return Mathf.Max(0f, backingTrackSource.clip.length);

        if (chartNotes != null && chartNotes.Count > 0)
            return Mathf.Max(0f, chartNotes.Max(note => note.time + Mathf.Max(0.05f, note.duration)));

        return 0f;
    }

    private float GetSongProgressNormalized()
    {
        float duration = GetSongDurationSeconds();
        if (duration <= 0.001f)
            return 0f;

        return Mathf.Clamp01(songTimer / duration);
    }

    private void UpdateSongEndState()
    {
        float duration = GetSongDurationSeconds();
        if (duration <= 0.001f)
        {
            songHasEnded = false;
            return;
        }

        if (!loopEnabled && !songHasEnded && !showMainMenu && !showSongSelection && !showTrackSelection && songTimer >= duration)
        {
            songTimer = duration;
            audioSongTimer = duration;
            songHasEnded = true;
            isPaused = true;
            showSongSettings = false;
            showMainMenu = false;
            mainMenuFlowActive = false;
            showSongSelection = false;
            showTrackSelection = false;
            showGlobalSettings = false;
            SyncAudioToSongTimer(playImmediately: false);
            return;
        }

        if (songTimer < duration - 0.02f)
            songHasEnded = false;
    }

    private GuitarGameplaySnapshot BuildSnapshot()
    {
        int currentSectionIndex = GetSectionIndex(songTimer);
        float sectionDuration = GetEffectiveTabSectionDuration();
        float sectionStart = currentSectionIndex * sectionDuration;
        float progress = Mathf.Clamp01((songTimer - sectionStart) / Mathf.Max(0.01f, sectionDuration));
        SongMetadata pendingTrackMetadata = pendingTrackSelectionSong != null ? LoadSongMetadataForEntry(pendingTrackSelectionSong) : null;

        return new GuitarGameplaySnapshot
        {
            songTime = songTimer,
            isPaused = isPaused,
            loopEnabled = loopEnabled,
            loopStartTime = loopStartTime,
            loopEndTime = loopEndTime,
            selectedLoopMarker = selectedLoopMarker,
            playbackSpeedPercent = playbackSpeedPercent,
            currentSectionIndex = currentSectionIndex,
            nextSectionIndex = currentSectionIndex + 1,
            currentSectionProgress = progress,
            sectionDuration = GetEffectiveTabSectionDuration(),
            noteStates = noteStates,
            sections = tabSections,
            latestDetectedPitches = latestDetectedPitches,
            showSongSettings = showSongSettings,
            showMainMenu = showMainMenu,
            mainMenuFlowActive = mainMenuFlowActive,
            showSongSelection = showSongSelection,
            showTrackSelection = showTrackSelection,
            showGlobalSettings = showGlobalSettings,
            availableSongNames = availableSongs.Select(song => song.DisplayName).ToList(),
            availableSongScores = availableSongs.Select(GetStoredSongBestScorePercent).ToList(),
            selectedSongIndex = selectedSongListIndex,
            availableTrackNames = pendingTrackSelectionParts.Select(track => track.Name).ToList(),
            availableTrackScores = pendingTrackMetadata != null ? pendingTrackSelectionParts.Select(track => GetStoredTrackScore(pendingTrackMetadata, track.PartId)).ToList() : new List<float>(),
            selectedTrackIndex = selectedTrackListIndex,
            currentSongDisplayName = currentSongEntry != null ? currentSongEntry.DisplayName : string.Empty,
            songListScrollOffset = songListScrollOffset,
            audioOffsetMs = audioOffsetMs,
            tabSpeedOffsetPercent = tabSpeedOffsetPercent,
            songStartDelaySeconds = songStartDelaySeconds,
            selectedTrackDisplayName = GetTrackDisplayName(GetCurrentTrackOptionIndex()),
            trackSelectionHint = GetTrackOptionCount() > 1 ? "Track: click row or Q/E" : "Track: single detected part",
            offsetScopeLabel = useTrackOffsetForCurrentTrack ? "Track" : "Song",
            offsetScopeHint = "Offset scope: O toggles Song/Track",
            hasBackingTrack = hasBackingTrack,
            isBackingTrackPlaying = backingTrackSource != null && backingTrackSource.isPlaying,
            backingTrackTime = backingTrackSource != null ? backingTrackSource.time : 0f,
            noteDetectorConnected = IsNoteDetectorConnected(),
            inputLevelNormalized = smoothedInputLevel,
            songDuration = GetSongDurationSeconds(),
            songProgressNormalized = GetSongProgressNormalized(),
            songEnded = songHasEnded,
            currentTrackBestScorePercent = Mathf.Clamp(currentTrackBestScorePercent, 0f, 100f),
            showStartupTuningReminder = showStartupTuningReminder,
            runtimeSettingsSections = BuildRuntimeSettingsSnapshot()
        };
    }

    private void EnsureRenderer()
    {
        if (activeRenderer != null && activeRendererMode == renderMode) return;

        if (activeRenderer != null) activeRenderer.DisposeRenderer();

        if (renderMode == GuitarRenderMode.Tabs)
            activeRenderer = new GuitarTabsRenderer();
        else
            activeRenderer = new GuitarHighway3DRenderer();

        activeRenderer.Initialize(this, chartNotes, tabSections);
        activeRendererMode = renderMode;
    }

    private void UpdateUiText()
    {
        if (uiText == null) return;

        if (!showCenterDebugOverlay)
        {
            if (uiText.enabled)
                uiText.enabled = false;

            if (!string.IsNullOrEmpty(uiText.text))
                uiText.text = string.Empty;

            return;
        }

        if (!uiText.enabled)
            uiText.enabled = true;

        List<string> stableNames = latestDetectedPitches.Select(GetNoteNameFromMidi).ToList();
        string eventTxt = latestPacketHadEvent ? "YES" : "NO";
        string loopTxt = loopEnabled ? $"ON ({loopStartTime:F2}s - {loopEndTime:F2}s)" : "OFF";
        uiText.text =
            $"ACTIVE: <color=green>{string.Join(",", stableNames)}</color>\n" +
            $"NEW EVENT: <color=orange>{eventTxt}</color>  ID:{latestNoteEventId}\n" +
            $"EVENT NOTES: <color=cyan>{latestEventNotesText}</color>\n" +
            $"TIME: <color=white>{songTimer:F2}</color>\n" +
            $"LOOP: <color=yellow>{loopTxt}</color> Marker:{selectedLoopMarker}\n" +
            $"SPEED: <color=white>{playbackSpeedPercent:F0}%</color>\n" +
            $"AUDIO: <color=white>{(isLoadingBackingTrack ? "LOADING" : (hasBackingTrack ? "READY" : "MISSING"))}</color>  OFFSET:<color=cyan>{audioOffsetMs:F0}ms</color>\n" +
            $"TAB SPEED OFFSET: <color=cyan>{tabSpeedOffsetPercent:F0}%</color>\n" +
            $"TRACK: <color=cyan>{GetTrackDisplayName(GetCurrentTrackOptionIndex())}</color>\n" +
            $"START DELAY: <color=cyan>{songStartDelaySeconds:F2}s</color>\n" +
            $"AUDIO SRC: <color=grey>{(string.IsNullOrEmpty(backingTrackLoadError) ? currentSongFileName : backingTrackLoadError)}</color>";
    }

    // =========================================================
    // SONG LOADING AND TAB GENERATION
    // =========================================================
    private void LoadTestSong(bool preservePauseUiState = false)
    {
        currentLoadedTrackIndex = midiTrackIndex;
        latestDetectedPitches.Clear();
        recentNoteEvents.Clear();
        latestNoteEventId = 0;
        bool wasPaused = isPaused;
        bool wasShowingSongSettings = showSongSettings;
        bool wasShowingMainMenu = showMainMenu;
        bool wasMainMenuFlowActive = mainMenuFlowActive;
        bool wasShowingSongSelection = showSongSelection;
        bool wasShowingTrackSelection = showTrackSelection;
        bool wasShowingGlobalSettings = showGlobalSettings;

        songTimer = 0f;
        audioSongTimer = 0f;
        isPaused = preservePauseUiState ? wasPaused : false;
        songHasEnded = false;

        float sectionDuration = GetEffectiveTabSectionDuration();
        loopStartTime = Mathf.Max(0.2f, sectionDuration * 0.40f);
        loopEndTime = Mathf.Max(loopStartTime + 0.5f, sectionDuration * 0.60f);
        loopEnabled = false;
        selectedLoopMarker = 1;
        playbackSpeedPercent = 100f;
        showSongSettings = preservePauseUiState ? wasShowingSongSettings : false;
        showMainMenu = preservePauseUiState ? wasShowingMainMenu : showMainMenu;
        mainMenuFlowActive = preservePauseUiState ? wasMainMenuFlowActive : showMainMenu;
        showSongSelection = preservePauseUiState ? wasShowingSongSelection : false;
        showTrackSelection = preservePauseUiState ? wasShowingTrackSelection : false;
        showGlobalSettings = preservePauseUiState ? wasShowingGlobalSettings : false;
        tabSpeedOffsetPercent = 100f;

        List<NoteData> loadedNotes = null;

        // 1. Discover and load a valid runtime song from persistentDataPath/Songs.
        if (!useBuiltInDemoSong)
        {
            RefreshAvailableSongs();
            currentSongPartSummaries.Clear();

            if (currentSongEntry == null || !availableSongs.Any(song => string.Equals(song.SongDirectory, currentSongEntry.SongDirectory, StringComparison.OrdinalIgnoreCase)))
            {
                string preferredSongDirectory = LoadSelectedSongPreference();
                currentSongEntry = availableSongs.FirstOrDefault(song =>
                    !string.IsNullOrEmpty(preferredSongDirectory) &&
                    string.Equals(song.SongDirectory, preferredSongDirectory, StringComparison.OrdinalIgnoreCase))
                    ?? availableSongs.FirstOrDefault();
            }

            if (currentSongEntry != null)
            {
                Debug.Log($"[GuitarBridgeServer] Selected runtime song '{currentSongEntry.SongId}' from {currentSongEntry.SongDirectory}");
                SaveSelectedSongPreference(currentSongEntry);
                currentSongFileName = !string.IsNullOrEmpty(currentSongEntry.Mp3Path) ? Path.GetFileName(currentSongEntry.Mp3Path) : backingTrackFileName;
                SongMetadata trackMetadata = LoadSongMetadata(currentSongFileName);
                useAutoTrackSelection = trackMetadata.useAutoTrackSelection;
                selectedMusicXmlPartId = string.IsNullOrEmpty(trackMetadata.selectedMusicXmlPartId) ? string.Empty : trackMetadata.selectedMusicXmlPartId;

                currentSongPartSummaries.AddRange(MusicXmlLoader.GetPartSummaries(currentSongEntry.XmlPath));
                ApplyTrackSelectionPreference();

                try
                {
                    loadedNotes = MusicXmlLoader.LoadMusicXmlSong(currentSongEntry.XmlPath, midiTrackIndex);
                    Debug.Log($"MusicXML load attempt: {currentSongEntry.XmlPath}");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("MusicXmlLoader Error: " + e.Message);
                }

                if ((loadedNotes == null || loadedNotes.Count == 0) && !string.IsNullOrEmpty(currentSongEntry.MidiPath))
                {
                    try
                    {
                        loadedNotes = MidiLoader.LoadMidiSong(currentSongEntry.MidiPath, midiTrackIndex);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("MidiLoader Error: " + e.Message);
                    }
                }
            }
            else
            {
                currentSongPartSummaries.Clear();
                Debug.LogWarning("[GuitarBridgeServer] No valid runtime songs were found in persistent storage.");
            }
        }


        InitializeSongMetadataAndAudio();

        bool useDemo = useBuiltInDemoSong || (useDemoSongIfMidiMissing && (loadedNotes == null || loadedNotes.Count == 0));

        // 2. Load the demo song if no MIDI was found
        if (useDemo)
        {
            loadedNotes = BuildDemoSong();
            Debug.Log($"Using built-in demo song. Notes: {loadedNotes.Count}");
        }

        // 3. Fallback to random notes if absolutely everything fails
        if (loadedNotes == null || loadedNotes.Count == 0)
        {
            loadedNotes = new List<NoteData>();
            for (int i = 0; i < 50; i++)
            {
                loadedNotes.Add(new NoteData(i * 1.5f + 2f, i % 6, UnityEngine.Random.Range(0, 15), "E"));
            }
        }

        chartNotes = loadedNotes;
        chartNoteById.Clear();

        for (int i = 0; i < chartNotes.Count; i++)
        {
            NoteData nd = chartNotes[i];
            if (nd.id < 0) nd.id = i; 
            chartNotes[i] = nd;
            chartNoteById[nd.id] = nd;
        }

        noteStates = chartNotes.Select(n => new GameplayNoteState(n)).ToList();
        

        float songEndTime = chartNotes.Count > 0 ? chartNotes.Max(n => n.time + n.duration) : GetEffectiveTabSectionDuration();
        loopStartTime = Mathf.Clamp(loopStartTime, 0f, Mathf.Max(0f, songEndTime - 0.05f));
        loopEndTime = Mathf.Clamp(loopEndTime, loopStartTime + 0.05f, Mathf.Max(loopStartTime + 0.05f, songEndTime));

        // 4. GENERATE THE SECTIONS (This is what brings the renderer back to life!)
        GenerateTabSections();
        ResetActiveRendererContent();

        songTimer = -songStartDelaySeconds;
        audioSongTimer = -songStartDelaySeconds;
        currentLoadedTrackIndex = midiTrackIndex;
        ApplyPlaybackSpeedToAudio();
        SyncAudioToSongTimer(playImmediately: !isPaused);
    }

    private void ResetActiveRendererContent()
    {
        if (activeRenderer == null)
            return;

        if (activeRenderer is GuitarTabsRenderer tabsRenderer)
        {
            tabsRenderer.ResetRenderer(chartNotes, tabSections);
            return;
        }

        if (activeRenderer is GuitarHighway3DRenderer highwayRenderer)
            highwayRenderer.ResetRenderer(chartNotes, tabSections);
    }

    private void EnsureBackingTrackSource()
    {
        if (backingTrackSource != null)
            return;

        backingTrackSource = GetComponent<AudioSource>();
        if (backingTrackSource == null)
            backingTrackSource = gameObject.AddComponent<AudioSource>();

        backingTrackSource.playOnAwake = false;
        backingTrackSource.loop = false;
        backingTrackSource.spatialBlend = 0f;
    }

    private void InitializeSongMetadataAndAudio()
    {
        EnsureBackingTrackSource();

        if (currentSongEntry == null)
        {
            hasBackingTrack = false;
            backingTrackLoadError = "No runtime song selected.";
            Debug.LogWarning(backingTrackLoadError);
            return;
        }

        string songPath = currentSongEntry.Mp3Path;
        currentSongFileName = Path.GetFileName(songPath);

        songMetadata = LoadSongMetadata(currentSongFileName);
        globalAudioOffsetMs = songMetadata.audioOffsetMs;
        audioOffsetMs = globalAudioOffsetMs;
        tabSpeedOffsetPercent = Mathf.Clamp(songMetadata.tabSpeedOffsetPercent <= 0f ? 100f : songMetadata.tabSpeedOffsetPercent, 50f, 150f);
        songStartDelaySeconds = Mathf.Clamp(songMetadata.songStartDelaySeconds <= 0f ? defaultSongStartDelaySeconds : songMetadata.songStartDelaySeconds, 0f, 8f);
        useAutoTrackSelection = songMetadata.useAutoTrackSelection;
        selectedMusicXmlPartId = string.IsNullOrEmpty(songMetadata.selectedMusicXmlPartId) ? string.Empty : songMetadata.selectedMusicXmlPartId;
        currentSongBestScorePercent = Mathf.Clamp(GetHighestTrackScore(songMetadata), 0f, 100f);
        currentTrackBestScorePercent = Mathf.Clamp(GetStoredTrackScore(songMetadata, selectedMusicXmlPartId), 0f, 100f);
        RefreshEffectiveAudioOffset();

        backingTrackLoadError = string.Empty;
        isLoadingBackingTrack = false;

        if (backingTrackSource.clip != null)
            backingTrackSource.clip = null;

        if (File.Exists(songPath))
        {
            StartCoroutine(LoadBackingTrackFromFile(songPath));
            return;
        }

        hasBackingTrack = false;
        backingTrackLoadError = $"Backing track not found at: {songPath}";
        Debug.LogWarning(backingTrackLoadError);
    }


    private System.Collections.IEnumerator LoadBackingTrackFromFile(string absolutePath)
    {
        isLoadingBackingTrack = true;
        hasBackingTrack = false;

        string uri = "file://" + absolutePath.Replace("\\", "/");
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.UNKNOWN))
        {
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool failed = request.result != UnityWebRequest.Result.Success;
#else
            bool failed = request.isNetworkError || request.isHttpError;
#endif
            if (failed)
            {
                backingTrackLoadError = $"Failed to load backing track '{absolutePath}': {request.error}";
                Debug.LogWarning(backingTrackLoadError);
                hasBackingTrack = false;
                isLoadingBackingTrack = false;
                yield break;
            }

            AudioClip loadedClip = DownloadHandlerAudioClip.GetContent(request);
            if (loadedClip == null)
            {
                backingTrackLoadError = $"Audio clip content was null for backing track: {absolutePath}";
                Debug.LogWarning(backingTrackLoadError);
                hasBackingTrack = false;
                isLoadingBackingTrack = false;
                yield break;
            }

            loadedClip.name = Path.GetFileNameWithoutExtension(absolutePath);
            backingTrackSource.clip = loadedClip;
            hasBackingTrack = true;

            ApplyPlaybackSpeedToAudio();
            SyncAudioToSongTimer(playImmediately: !isPaused);
        }

        isLoadingBackingTrack = false;
    }

    private static string BuildSongMetadataPath(SongLibraryEntry entry)
    {
        if (entry != null && !string.IsNullOrEmpty(entry.MetadataPath))
            return entry.MetadataPath;

        string fallbackFileName = entry != null && !string.IsNullOrEmpty(entry.Mp3Path)
            ? Path.GetFileName(entry.Mp3Path)
            : "song.mp3";
        string safeName = Regex.Replace(Path.GetFileNameWithoutExtension(fallbackFileName), "[^a-zA-Z0-9_-]", "_");
        return Path.Combine(ExternalContentPaths.PersistentSongsDirectory, safeName, ExternalContentPaths.SongMetadataFileName);
    }

    private static float GetHighestTrackScore(SongMetadata metadata)
    {
        if (metadata == null || metadata.trackScores == null || metadata.trackScores.Count == 0)
            return 0f;

        float highest = 0f;
        for (int i = 0; i < metadata.trackScores.Count; i++)
            highest = Mathf.Max(highest, Mathf.Clamp(metadata.trackScores[i].bestScorePercent, 0f, 100f));

        return highest;
    }

    private static float GetStoredTrackScore(SongMetadata metadata, string partId)
    {
        if (metadata == null || metadata.trackScores == null || string.IsNullOrEmpty(partId))
            return 0f;

        TrackScoreEntry entry = metadata.trackScores.FirstOrDefault(score => string.Equals(score.partId ?? string.Empty, partId, StringComparison.OrdinalIgnoreCase));
        return entry != null ? Mathf.Clamp(entry.bestScorePercent, 0f, 100f) : 0f;
    }

    private static void UpsertTrackScore(SongMetadata metadata, string partId, string displayName, float percent)
    {
        if (metadata == null || string.IsNullOrEmpty(partId))
            return;

        if (metadata.trackScores == null)
            metadata.trackScores = new List<TrackScoreEntry>();

        TrackScoreEntry existing = metadata.trackScores.FirstOrDefault(score => string.Equals(score.partId ?? string.Empty, partId, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            metadata.trackScores.Add(new TrackScoreEntry
            {
                partId = partId,
                displayName = displayName,
                bestScorePercent = Mathf.Clamp(percent, 0f, 100f)
            });
            return;
        }

        existing.displayName = string.IsNullOrEmpty(displayName) ? existing.displayName : displayName;
        existing.bestScorePercent = Mathf.Max(existing.bestScorePercent, Mathf.Clamp(percent, 0f, 100f));
    }

    private SongMetadata LoadSongMetadataForEntry(SongLibraryEntry entry)
    {
        if (entry == null)
            return new SongMetadata();

        string fileName = Path.GetFileName(entry.Mp3Path);
        string metadataPath = BuildSongMetadataPath(entry);
        return LoadSongMetadata(fileName, metadataPath);
    }

    private float GetStoredSongBestScorePercent(SongLibraryEntry entry)
    {
        if (entry == null)
            return 0f;

        if (currentSongEntry != null && string.Equals(currentSongEntry.SongDirectory, entry.SongDirectory, StringComparison.OrdinalIgnoreCase))
            return Mathf.Clamp(currentSongBestScorePercent, 0f, 100f);

        SongMetadata metadata = LoadSongMetadataForEntry(entry);
        return Mathf.Clamp(GetHighestTrackScore(metadata), 0f, 100f);
    }

    private List<MusicXmlLoader.MusicXmlPartSummary> GetSortedTrackSummaries(SongLibraryEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.XmlPath))
            return new List<MusicXmlLoader.MusicXmlPartSummary>();

        List<MusicXmlLoader.MusicXmlPartSummary> summaries = MusicXmlLoader.GetPartSummaries(entry.XmlPath);
        SongMetadata metadata = LoadSongMetadataForEntry(entry);

        return summaries
            .OrderByDescending(summary => GetStoredTrackScore(metadata, summary.PartId))
            .ThenBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void SaveSelectedSongPreference(SongLibraryEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.SongDirectory))
            return;

        PlayerPrefs.SetString(SelectedSongDirectoryPrefsKey, entry.SongDirectory);
        PlayerPrefs.Save();
    }

    private static string LoadSelectedSongPreference()
    {
        return PlayerPrefs.GetString(SelectedSongDirectoryPrefsKey, string.Empty);
    }

    private void UpdateAndPersistSongBestScore()
    {
        if (loopEnabled || currentSongEntry == null || noteStates == null || noteStates.Count == 0 || string.IsNullOrEmpty(selectedMusicXmlPartId))
            return;

        int total = noteStates.Count;
        int hits = 0;
        for (int i = 0; i < noteStates.Count; i++)
        {
            if (noteStates[i] != null && noteStates[i].IsHit)
                hits++;
        }

        float percent = total > 0 ? (100f * hits / total) : 0f;
        if (percent <= currentTrackBestScorePercent + 0.01f)
            return;

        currentTrackBestScorePercent = Mathf.Clamp(percent, 0f, 100f);
        string trackName = GetTrackDisplayName(GetCurrentTrackOptionIndex());
        UpsertTrackScore(songMetadata, selectedMusicXmlPartId, trackName, currentTrackBestScorePercent);
        currentSongBestScorePercent = Mathf.Clamp(GetHighestTrackScore(songMetadata), 0f, 100f);
        SaveSongMetadata();
    }

    private SongMetadata LoadSongMetadata(string songFileName)
    {
        string path = GetMetadataPath(songFileName);
        return LoadSongMetadata(songFileName, path);
    }

    private SongMetadata LoadSongMetadata(string songFileName, string metadataPath)
    {
        SongMetadata data = new SongMetadata
        {
            songFileName = songFileName,
            audioOffsetMs = 0f,
            tabSpeedOffsetPercent = 100f,
            songStartDelaySeconds = defaultSongStartDelaySeconds,
            useAutoTrackSelection = true,
            selectedMusicXmlPartId = string.Empty,
            trackScores = new List<TrackScoreEntry>(),
            trackOffsetOverrides = new List<TrackOffsetOverride>()
        };

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(metadataPath));
            if (File.Exists(metadataPath))
            {
                string json = File.ReadAllText(metadataPath);
                SongMetadata loaded = JsonUtility.FromJson<SongMetadata>(json);
                if (loaded != null)
                    data = loaded;

                if (data.trackOffsetOverrides == null)
                    data.trackOffsetOverrides = new List<TrackOffsetOverride>();
                if (data.trackScores == null)
                    data.trackScores = new List<TrackScoreEntry>();
                if (data.trackScores.Count == 0 && data.bestScorePercent > 0.01f && !string.IsNullOrEmpty(data.selectedMusicXmlPartId))
                {
                    data.trackScores.Add(new TrackScoreEntry
                    {
                        partId = data.selectedMusicXmlPartId,
                        displayName = data.selectedMusicXmlPartId,
                        bestScorePercent = Mathf.Clamp(data.bestScorePercent, 0f, 100f)
                    });
                }
            }
            else
            {
                File.WriteAllText(metadataPath, JsonUtility.ToJson(data, true));
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load metadata {metadataPath}: {ex.Message}");
        }

        return data;
    }

    private void SaveSongMetadata()
    {
        if (string.IsNullOrEmpty(currentSongFileName))
            return;

        songMetadata.songFileName = currentSongFileName;
        songMetadata.audioOffsetMs = globalAudioOffsetMs;
        songMetadata.tabSpeedOffsetPercent = tabSpeedOffsetPercent;
        songMetadata.songStartDelaySeconds = songStartDelaySeconds;
        songMetadata.useAutoTrackSelection = useAutoTrackSelection;
        songMetadata.selectedMusicXmlPartId = selectedMusicXmlPartId;
        songMetadata.bestScorePercent = Mathf.Clamp(GetHighestTrackScore(songMetadata), 0f, 100f);
        currentSongBestScorePercent = songMetadata.bestScorePercent;

        SaveSongMetadata(songMetadata, GetMetadataPath(currentSongFileName), currentSongFileName);
    }

    private void SaveSongMetadata(SongMetadata metadata, string metadataPath, string songFileName)
    {
        if (metadata == null || string.IsNullOrEmpty(metadataPath))
            return;

        metadata.songFileName = songFileName;
        metadata.bestScorePercent = Mathf.Clamp(GetHighestTrackScore(metadata), 0f, 100f);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(metadataPath));
            File.WriteAllText(metadataPath, JsonUtility.ToJson(metadata, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to save song metadata: {ex.Message}");
        }
    }

    private string GetMetadataPath(string songFileName)
    {
        if (currentSongEntry != null && !string.IsNullOrEmpty(currentSongEntry.MetadataPath))
            return currentSongEntry.MetadataPath;

        string safeName = Regex.Replace(Path.GetFileNameWithoutExtension(songFileName), "[^a-zA-Z0-9_-]", "_");
        return Path.Combine(ExternalContentPaths.PersistentSongsDirectory, safeName, ExternalContentPaths.SongMetadataFileName);
    }

    private void RegisterRuntimeSettings()
    {
        runtimeSettingDefinitions.Clear();
        runtimeSettingById.Clear();
        runtimeSettingDefaultValues.Clear();
        runtimeSettingsSnapshotDirty = true;

        RegisterFloatSetting("core.noteSpeed", "Settings", "Note Speed", "Controls how quickly notes travel toward the hit line.", 4f, 30f, 0.1f, () => noteSpeed, v => noteSpeed = v);
        RegisterBoolSetting("core.invertStrings", "Settings", "Invert Strings", "Reverses string order so the low string appears at the top.", () => invertStrings, v => invertStrings = v);
        RegisterEnumSetting("render.mode", "Highway 3D", "Render Mode", "Switches between Tabs and Highway3D presentation.", new []{"Tabs","Highway3D"}, () => renderMode.ToString(), v => { if (Enum.TryParse(v, out GuitarRenderMode mode)) renderMode = mode; });

        RegisterFloatSetting("timing.hitWindowEarly", "Timing & Forgiveness", "Hit Window Early", "How far before a note you can strike and still get credit.", 0.05f, 0.6f, 0.005f, () => hitWindowEarly, v => hitWindowEarly = v);
        RegisterFloatSetting("timing.hitWindowLate", "Timing & Forgiveness", "Hit Window Late", "How far after a note you can strike and still get credit.", 0.05f, 0.8f, 0.005f, () => hitWindowLate, v => hitWindowLate = v);
        RegisterFloatSetting("timing.judgmentGrace", "Timing & Forgiveness", "Judgment Grace", "Extends visibility for judged notes so feedback is easier to read.", 0.1f, 1.2f, 0.01f, () => judgmentGrace, v => judgmentGrace = v);

        RegisterFloatSetting("tabs.tabSectionDuration", "Tabs Sections", "Section Duration", "Length of each tab panel section in seconds.", 1f, 12f, 0.1f, () => tabSectionDuration, v => tabSectionDuration = v);
        RegisterFloatSetting("tabs.tabSectionLengthMultiplier", "Tabs Sections", "Section Length Multiplier", "Scales section length without changing beat timing.", 0.5f, 3f, 0.05f, () => tabSectionLengthMultiplier, v => tabSectionLengthMultiplier = v);

        RegisterFloatSetting("layout.tabPanelGap", "Tabs Panels Layout", "Panel Gap", "Vertical spacing between upper and lower tab panels.", 0.3f, 2.2f, 0.01f, () => tabPanelGap, v => tabPanelGap = v);
        RegisterFloatSetting("layout.tabPanelHeight", "Tabs Panels Layout", "Panel Height", "Height of each tab panel lane.", 2f, 7f, 0.05f, () => tabPanelHeight, v => tabPanelHeight = v);
        RegisterFloatSetting("layout.tabLineSpacing", "Tabs Dimensions", "Line Spacing", "Spacing between strings inside a panel.", 0.25f, 1.2f, 0.01f, () => tabLineSpacing, v => tabLineSpacing = v);
        RegisterFloatSetting("layout.tabNoteCircleDiameter", "Tabs Dimensions", "Note Circle Size", "Diameter of tab note circles.", 0.2f, 1.2f, 0.01f, () => tabNoteCircleDiameter, v => tabNoteCircleDiameter = v);
        RegisterFloatSetting("layout.tabNoteFontSize", "Tabs Dimensions", "Note Font Size", "Size of fret numbers shown on notes.", 1f, 5f, 0.05f, () => tabNoteFontSize, v => tabNoteFontSize = v);

        RegisterFloatSetting("layout.tabBackdropOpacity", "Tabs Panels Layout", "Backdrop Opacity", "Opacity for the tab panel backdrop fill.", 0f, 1f, 0.01f, () => tabPanelBackdropColor.a, v => { Color c = tabPanelBackdropColor; c.a = v; tabPanelBackdropColor = c; });
        RegisterFloatSetting("layout.tabBackdropColorR", "Tabs Panels Layout", "Backdrop Color R", "Red channel of the tab panel backdrop color.", 0f, 1f, 0.01f, () => tabPanelBackdropColor.r, v => { Color c = tabPanelBackdropColor; c.r = v; tabPanelBackdropColor = c; });
        RegisterFloatSetting("layout.tabBackdropColorG", "Tabs Panels Layout", "Backdrop Color G", "Green channel of the tab panel backdrop color.", 0f, 1f, 0.01f, () => tabPanelBackdropColor.g, v => { Color c = tabPanelBackdropColor; c.g = v; tabPanelBackdropColor = c; });
        RegisterFloatSetting("layout.tabBackdropColorB", "Tabs Panels Layout", "Backdrop Color B", "Blue channel of the tab panel backdrop color.", 0f, 1f, 0.01f, () => tabPanelBackdropColor.b, v => { Color c = tabPanelBackdropColor; c.b = v; tabPanelBackdropColor = c; });

        RegisterFloatSetting("fx.judgeableDarkenMultiplier", "Visuals", "Judgeable Darken", "Darkens upcoming notes until they enter the hit window.", 1f, 8f, 0.1f, () => judgeableDarkenMultiplier, v => judgeableDarkenMultiplier = v);
        RegisterFloatSetting("fx.tabIdleFillDarken", "Colors - Status", "Idle Fill Darken", "Controls how muted unresolved tab notes appear.", 0f, 1f, 0.01f, () => tabIdleFillDarken, v => tabIdleFillDarken = v);

        RegisterEnumSetting("bg.mode", "Background", "Background Mode", "Switches between static and animated backgrounds.", new []{"SolidColor","Starfield","BlueSky"}, () => tabBackgroundMode.ToString(), v => { if (Enum.TryParse(v, out TabsBackgroundMode mode)) tabBackgroundMode = mode; });
        RegisterEnumSetting("bg.skyMood", "Background - Blue Sky", "Sky Mood", "Switches BlueSky mood grading between daytime and sunset palettes.", new []{"Day","Sunset"}, () => tabSkyMood.ToString(), v => { if (Enum.TryParse(v, out TabsSkyMood mood)) tabSkyMood = mood; });
        RegisterBoolSetting("bg.skyStars", "Background - Blue Sky", "Static Sky Stars", "Adds non-moving stars behind clouds in BlueSky mode.", () => tabSkyStarsEnabled, v => tabSkyStarsEnabled = v);
        RegisterIntSetting("bg.skyStarCount", "Background - Blue Sky", "Sky Star Count", "Controls how many static stars are rendered in BlueSky mode.", 8, 1200, 1, () => tabSkyStarCount, v => tabSkyStarCount = v);
        RegisterFloatSetting("bg.skyStarTwinkleFraction", "Background - Blue Sky", "Star Twinkle Fraction", "Percentage of stars allowed to twinkle.", 0f, 1f, 0.01f, () => tabSkyStarTwinkleFraction, v => tabSkyStarTwinkleFraction = v);
        RegisterFloatSetting("bg.skyStarTwinkleStrength", "Background - Blue Sky", "Star Twinkle Strength", "How much brightness variation twinkling stars receive.", 0f, 0.6f, 0.01f, () => tabSkyStarTwinkleStrength, v => tabSkyStarTwinkleStrength = v);
        RegisterFloatSetting("bg.skyStarTwinkleSpeedMin", "Background - Blue Sky", "Star Twinkle Speed Min", "Minimum twinkle speed for twinkling stars.", 0.05f, 4f, 0.01f, () => tabSkyStarTwinkleSpeedMin, v => tabSkyStarTwinkleSpeedMin = v);
        RegisterFloatSetting("bg.skyStarTwinkleSpeedMax", "Background - Blue Sky", "Star Twinkle Speed Max", "Maximum twinkle speed for twinkling stars.", 0.05f, 4f, 0.01f, () => tabSkyStarTwinkleSpeedMax, v => tabSkyStarTwinkleSpeedMax = v);
        RegisterEnumSetting("bg.starStyle", "Background - Starfield Core", "Star Style", "Visual style used for star sprites in the background.", new []{"SoftDots","Crystal","Neon"}, () => tabStarStyle.ToString(), v => { if (Enum.TryParse(v, out TabsStarStyle style)) tabStarStyle = style; });
        RegisterIntSetting("bg.starSeed", "Background - Starfield Core", "Star Seed", "Changes the procedural star layout while keeping it deterministic.", 0, 99999, 1, () => tabStarSeed, v => tabStarSeed = v);
        RegisterFloatSetting("bg.starDriftSpeed", "Background - Starfield Core", "Star Drift Speed", "Horizontal motion speed of star layers.", 0f, 2.5f, 0.01f, () => tabStarDriftSpeed, v => tabStarDriftSpeed = v);
        RegisterBoolSetting("bg.shootingStars", "Background - Shooting Stars", "Shooting Stars", "Turns occasional shooting star streaks on or off.", () => tabShootingStarsEnabled, v => tabShootingStarsEnabled = v);
        RegisterFloatSetting("bg.skyCloudNearSpeed", "Background - Blue Sky", "Cloud Speed (Near)", "Horizontal drift speed for the nearest cloud layer.", 0.01f, 2f, 0.01f, () => tabSkyCloudSpeedNear, v => tabSkyCloudSpeedNear = v);
        RegisterFloatSetting("bg.skyCloudMidSpeed", "Background - Blue Sky", "Cloud Speed (Mid)", "Horizontal drift speed for the middle cloud layer.", 0.01f, 2f, 0.01f, () => tabSkyCloudSpeedMid, v => tabSkyCloudSpeedMid = v);
        RegisterFloatSetting("bg.skyCloudFarSpeed", "Background - Blue Sky", "Cloud Speed (Far)", "Horizontal drift speed for the far cloud layer.", 0.01f, 2f, 0.01f, () => tabSkyCloudSpeedFar, v => tabSkyCloudSpeedFar = v);
        RegisterFloatSetting("bg.skyCloudGlobalScale", "Background - Blue Sky", "Cloud Global Scale", "Scales all BlueSky clouds live without restarting.", 0.2f, 6f, 0.05f, () => tabSkyCloudGlobalScale, v => tabSkyCloudGlobalScale = v);

        RegisterIntSetting("highway.totalFrets", "Highway 3D - Layout", "Total Frets", "How many fret lanes are generated for the 3D highway.", 12, 36, 1, () => TotalFrets, v => TotalFrets = v);
        RegisterFloatSetting("highway.fretSpacing", "Highway 3D - Layout", "Fret Spacing", "Horizontal spacing between fret lanes in Highway3D.", 0.4f, 2.5f, 0.01f, () => FretSpacing, v => FretSpacing = v);
        RegisterFloatSetting("highway.strikeLineZ", "Highway 3D - Layout", "Strike Line Z", "Depth of the hit line in Highway3D.", -20f, 5f, 0.05f, () => StrikeLineZ, v => StrikeLineZ = v);
        RegisterFloatSetting("highway.spawnZ", "Highway 3D - Layout", "Spawn Z", "Depth where incoming Highway3D notes appear.", 10f, 120f, 0.5f, () => SpawnZ, v => SpawnZ = v);
        RegisterFloatSetting("highway.defaultOpenAnchorFret", "Highway 3D - Layout", "Open Anchor Fret", "Anchor fret used to visualize open notes in Highway3D.", 1f, 8f, 0.1f, () => defaultOpenAnchorFret, v => defaultOpenAnchorFret = v);
        RegisterBoolSetting("highway.hideOpenFretNumber", "Highway 3D - Layout", "Hide Open Fret Number", "Hides the open fret index marker on the Highway3D board.", () => hideOpenFretNumber, v => hideOpenFretNumber = v);

        RegisterFloatSetting("highway.cameraY", "Highway 3D - Camera", "Camera Y", "Vertical placement of the Highway3D camera.", 2f, 18f, 0.05f, () => highwayCameraY, v => highwayCameraY = v);
        RegisterFloatSetting("highway.cameraZ", "Highway 3D - Camera", "Camera Z", "Depth placement of the Highway3D camera.", -30f, 5f, 0.05f, () => highwayCameraZ, v => highwayCameraZ = v);
        RegisterFloatSetting("highway.cameraPitch", "Highway 3D - Camera", "Camera Pitch", "Pitch angle of the Highway3D camera.", 10f, 80f, 0.5f, () => highwayCameraPitch, v => highwayCameraPitch = v);
        RegisterFloatSetting("highway.lookaheadWindow", "Highway 3D - Camera", "Lookahead Window", "How far ahead the Highway3D camera frames upcoming notes.", 0.5f, 6f, 0.05f, () => lookaheadWindow, v => lookaheadWindow = v);
        RegisterFloatSetting("highway.cameraFarClip", "Highway 3D - Camera", "Camera Far Clip", "Far clipping plane for the Highway3D camera.", 100f, 6000f, 10f, () => highwayCameraFarClip, v => highwayCameraFarClip = v);
        RegisterFloatSetting("highway.cameraMoveSpeed", "Highway 3D - Camera", "Camera Move Speed", "Movement speed tuning value for Highway3D camera transitions.", 0.5f, 20f, 0.1f, () => camMoveSpeed, v => camMoveSpeed = v);

        RegisterFloatSetting("highway.noteHeightScale", "Highway 3D - Notes", "Note Height Scale", "Scales the vertical size of Highway3D note bodies.", 0.6f, 3f, 0.05f, () => highwayNoteHeightScale, v => highwayNoteHeightScale = v);
        RegisterFloatSetting("highway.resolvedHoldTime", "Highway 3D - Notes", "Resolved Hold Time", "How long hit/miss note feedback stays visible.", 0.1f, 1.5f, 0.01f, () => highwayResolvedHoldTime, v => highwayResolvedHoldTime = v);
        RegisterFloatSetting("highway.outlineThickness", "Highway 3D - Notes", "Stuck Outline Thickness", "Thickness of the stuck-note outline frame.", 0.01f, 0.3f, 0.005f, () => highwayStuckOutlineThickness, v => highwayStuckOutlineThickness = v);
        RegisterFloatSetting("highway.outlineDepth", "Highway 3D - Notes", "Stuck Outline Depth", "Depth of the stuck-note outline frame.", 0.005f, 0.2f, 0.005f, () => highwayStuckOutlineDepth, v => highwayStuckOutlineDepth = v);
        RegisterBoolSetting("highway.showApproachLine", "Highway 3D - Notes", "Show Approach Line", "Shows the line connecting notes to the strike line.", () => highwayShowApproachLine, v => highwayShowApproachLine = v);
        RegisterBoolSetting("highway.showLandingDot", "Highway 3D - Notes", "Show Landing Dot", "Shows the landing dot for fretted notes.", () => highwayShowLandingDot, v => highwayShowLandingDot = v);

        RegisterFloatSetting("highway.backgroundDistance", "Highway 3D - Background", "Background Distance", "How far behind the track the Highway3D background sits.", 50f, 4000f, 10f, () => highwayBackgroundDistance, v => highwayBackgroundDistance = v);
        RegisterFloatSetting("highway.backgroundCenterY", "Highway 3D - Background", "Background Center Y", "Vertical offset of the Highway3D background anchor.", -3000f, 1000f, 10f, () => highwayBackgroundCenterY, v => highwayBackgroundCenterY = v);
        RegisterFloatSetting("highway.backgroundScale", "Highway 3D - Background", "Background Scale", "Overall scale of the Highway3D background.", 10f, 1000f, 5f, () => highwayBackgroundScale, v => highwayBackgroundScale = v);
        RegisterFloatSetting("highway.cloudYOffset", "Highway 3D - Background", "Cloud Y Offset", "Vertical offset applied to highway-mode clouds.", -500f, 500f, 5f, () => highwayBackgroundCloudYOffset, v => highwayBackgroundCloudYOffset = v);
        RegisterFloatSetting("highway.starScale", "Highway 3D - Background", "Star Scale", "Highway override scale for starfield elements.", 0.05f, 5f, 0.05f, () => highwayBackgroundStarScale, v => highwayBackgroundStarScale = v);
        RegisterFloatSetting("highway.cloudScale", "Highway 3D - Background", "Cloud Scale", "Highway override scale for clouds.", 0.05f, 5f, 0.05f, () => highwayBackgroundCloudScale, v => highwayBackgroundCloudScale = v);
        RegisterFloatSetting("highway.starSpread", "Highway 3D - Background", "Star Spread", "Highway override spread for starfield elements.", 0.05f, 5f, 0.05f, () => highwayBackgroundStarSpread, v => highwayBackgroundStarSpread = v);
        RegisterFloatSetting("highway.cloudSpread", "Highway 3D - Background", "Cloud Spread", "Highway override spread for clouds.", 0.05f, 5f, 0.05f, () => highwayBackgroundCloudSpread, v => highwayBackgroundCloudSpread = v);
        RegisterFloatSetting("highway.backgroundColorR", "Highway 3D - Background", "Background Color R", "Red channel of the Highway3D background color.", 0f, 1f, 0.01f, () => highwayBackgroundColor.r, v => { Color c = highwayBackgroundColor; c.r = v; highwayBackgroundColor = c; });
        RegisterFloatSetting("highway.backgroundColorG", "Highway 3D - Background", "Background Color G", "Green channel of the Highway3D background color.", 0f, 1f, 0.01f, () => highwayBackgroundColor.g, v => { Color c = highwayBackgroundColor; c.g = v; highwayBackgroundColor = c; });
        RegisterFloatSetting("highway.backgroundColorB", "Highway 3D - Background", "Background Color B", "Blue channel of the Highway3D background color.", 0f, 1f, 0.01f, () => highwayBackgroundColor.b, v => { Color c = highwayBackgroundColor; c.b = v; highwayBackgroundColor = c; });
        RegisterFloatSetting("highway.backgroundColorA", "Highway 3D - Background", "Background Color A", "Alpha channel of the Highway3D background color.", 0f, 1f, 0.01f, () => highwayBackgroundColor.a, v => { Color c = highwayBackgroundColor; c.a = v; highwayBackgroundColor = c; });

        RegisterFloatSetting("highway.laneGuideThickness", "Highway 3D - Lanes", "Lane Guide Thickness", "Thickness of the Highway3D fret-boundary lane guides.", 0.02f, 0.5f, 0.01f, () => highwayLaneGuideThickness, v => highwayLaneGuideThickness = v);
        RegisterFloatSetting("highway.laneGuideYOffset", "Highway 3D - Lanes", "Lane Guide Y Offset", "Vertical offset for the Highway3D lane guides so you can lift them above or sink them into the board.", -3f, 2f, 0.01f, () => highwayLaneGuideYOffset, v => highwayLaneGuideYOffset = v);
        RegisterBoolSetting("highway.highlightFretBoundaries", "Highway 3D - Lanes", "Highlight Fret Boundaries", "Brightens fret metal boundaries when incoming notes are between them.", () => highwayHighlightFretBoundaries, v => highwayHighlightFretBoundaries = v);
        RegisterFloatSetting("highway.fretNumberYOffset", "Highway 3D - Layout", "Fret Number Y Offset", "Vertical offset for the Highway3D fret numbers.", -3f, 3f, 0.01f, () => highwayFretNumberYOffset, v => highwayFretNumberYOffset = v);
        RegisterFloatSetting("highway.fretNumberZOffset", "Highway 3D - Layout", "Fret Number Z Offset", "Depth offset for the Highway3D fret numbers relative to the strike line.", -3f, 3f, 0.01f, () => highwayFretNumberZOffset, v => highwayFretNumberZOffset = v);
    }

    private void RegisterFloatSetting(string id, string section, string label, string tooltip, float min, float max, float step, Func<float> getter, Action<float> setter)
    {
        RegisterSetting(new RuntimeSettingDefinition
        {
            Id = id,
            Section = section,
            Label = label,
            Tooltip = tooltip,
            ValueType = "float",
            Min = min,
            Max = max,
            Step = step,
            Getter = () => getter().ToString("0.###", CultureInfo.InvariantCulture),
            Setter = value =>
            {
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                    return;
                setter(Mathf.Clamp(parsed, min, max));
            }
        });
    }

    private void RegisterIntSetting(string id, string section, string label, string tooltip, int min, int max, int step, Func<int> getter, Action<int> setter)
    {
        RegisterSetting(new RuntimeSettingDefinition
        {
            Id = id,
            Section = section,
            Label = label,
            Tooltip = tooltip,
            ValueType = "int",
            Min = min,
            Max = max,
            Step = Mathf.Max(1, step),
            Getter = () => getter().ToString(CultureInfo.InvariantCulture),
            Setter = value =>
            {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                    return;
                setter(Mathf.Clamp(parsed, min, max));
            }
        });
    }

    private void RegisterBoolSetting(string id, string section, string label, string tooltip, Func<bool> getter, Action<bool> setter)
    {
        RegisterSetting(new RuntimeSettingDefinition
        {
            Id = id,
            Section = section,
            Label = label,
            Tooltip = tooltip,
            ValueType = "bool",
            Getter = () => getter() ? "true" : "false",
            Setter = value =>
            {
                if (!bool.TryParse(value, out bool parsed))
                    return;
                setter(parsed);
            }
        });
    }

    private void RegisterEnumSetting(string id, string section, string label, string tooltip, IEnumerable<string> options, Func<string> getter, Action<string> setter)
    {
        RegisterSetting(new RuntimeSettingDefinition
        {
            Id = id,
            Section = section,
            Label = label,
            Tooltip = tooltip,
            ValueType = "enum",
            EnumOptions = options.ToList(),
            Getter = getter,
            Setter = setter
        });
    }

    private void RegisterSetting(RuntimeSettingDefinition definition)
    {
        if (definition == null || string.IsNullOrEmpty(definition.Id))
            return;

        runtimeSettingDefinitions.Add(definition);
        runtimeSettingById[definition.Id] = definition;
        runtimeSettingDefaultValues[definition.Id] = definition.Getter != null ? definition.Getter() : string.Empty;
        runtimeSettingsSnapshotDirty = true;
    }

    private List<RuntimeSettingSectionSnapshot> BuildRuntimeSettingsSnapshot()
    {
        if (!runtimeSettingsSnapshotDirty && cachedRuntimeSettingsSnapshot != null)
            return cachedRuntimeSettingsSnapshot;

        cachedRuntimeSettingsSnapshot = runtimeSettingDefinitions
            .GroupBy(def => def.Section)
            .Select(group => new RuntimeSettingSectionSnapshot
            {
                title = group.Key,
                settings = group.Select(def => new RuntimeSettingSnapshot
                {
                    id = def.Id,
                    label = def.Label,
                    tooltip = def.Tooltip,
                    valueType = def.ValueType,
                    value = def.Getter != null ? def.Getter() : string.Empty,
                    min = def.Min,
                    max = def.Max,
                    step = def.Step,
                    enumOptions = def.EnumOptions != null ? new List<string>(def.EnumOptions) : new List<string>()
                }).ToList()
            })
            .ToList();

        runtimeSettingsSnapshotDirty = false;
        return cachedRuntimeSettingsSnapshot;
    }

    private void ApplyRuntimeSettingValue(string settingId, string serializedValue, bool saveMetadata)
    {
        if (string.IsNullOrEmpty(settingId) || !runtimeSettingById.TryGetValue(settingId, out RuntimeSettingDefinition definition) || definition.Setter == null)
            return;

        definition.Setter(serializedValue ?? string.Empty);
        runtimeSettingsSnapshotDirty = true;
        RefreshRuntimeSettingVisuals(settingId);

        if (saveMetadata)
            SaveGlobalRuntimeSettingsMetadata();
    }

    private void RefreshRuntimeSettingVisuals(string settingId)
    {
        if (string.IsNullOrEmpty(settingId))
            return;

        bool requiresSectionRebuild = settingId.StartsWith("tabs.tabSection", StringComparison.OrdinalIgnoreCase);
        bool requiresRendererRefresh =
            requiresSectionRebuild ||
            settingId.StartsWith("render.", StringComparison.OrdinalIgnoreCase) ||
            settingId.StartsWith("highway.", StringComparison.OrdinalIgnoreCase) ||
            settingId.StartsWith("bg.", StringComparison.OrdinalIgnoreCase) ||
            settingId.StartsWith("layout.", StringComparison.OrdinalIgnoreCase) ||
            settingId.StartsWith("fx.", StringComparison.OrdinalIgnoreCase);

        if (requiresSectionRebuild)
            GenerateTabSections();

        if (requiresRendererRefresh)
            ResetActiveRendererContent();
    }

    private void LoadGlobalRuntimeSettingsMetadata()
    {
        pendingGlobalRuntimeSettingValues.Clear();
        string path = Path.Combine(ExternalContentPaths.PersistentRoot, GlobalRuntimeSettingsFileName);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (!File.Exists(path))
            {
                ApplyDefaultRuntimeSettings();
                SaveGlobalRuntimeSettingsMetadata();
                return;
            }

            string json = File.ReadAllText(path);
            GlobalRuntimeSettingsMetadata metadata = JsonUtility.FromJson<GlobalRuntimeSettingsMetadata>(json);
            if (metadata?.values == null)
                return;

            foreach (RuntimeSettingValueEntry entry in metadata.values)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id))
                    continue;

                pendingGlobalRuntimeSettingValues[entry.id] = entry.value ?? string.Empty;
            }

            foreach (KeyValuePair<string, string> pair in pendingGlobalRuntimeSettingValues)
                ApplyRuntimeSettingValue(pair.Key, pair.Value, saveMetadata: false);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load global settings metadata: {ex.Message}");
        }
    }

    private void SaveGlobalRuntimeSettingsMetadata()
    {
        string path = Path.Combine(ExternalContentPaths.PersistentRoot, GlobalRuntimeSettingsFileName);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            GlobalRuntimeSettingsMetadata metadata = new GlobalRuntimeSettingsMetadata
            {
                values = runtimeSettingDefinitions.Select(def => new RuntimeSettingValueEntry
                {
                    id = def.Id,
                    value = def.Getter != null ? def.Getter() : string.Empty
                }).ToList()
            };

            File.WriteAllText(path, JsonUtility.ToJson(metadata, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to save global settings metadata: {ex.Message}");
        }
    }

    private void ApplyDefaultRuntimeSettings()
    {
        Dictionary<string, string> defaults = LoadRuntimeSettingDefaultsFromFile();

        foreach (RuntimeSettingDefinition definition in runtimeSettingDefinitions)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id))
                continue;

            string value;
            if (!defaults.TryGetValue(definition.Id, out value) && !runtimeSettingDefaultValues.TryGetValue(definition.Id, out value))
                continue;

            ApplyRuntimeSettingValue(definition.Id, value, saveMetadata: false);
        }
    }

    private static Dictionary<string, string> LoadRuntimeSettingDefaultsFromFile()
    {
        Dictionary<string, string> result = new Dictionary<string, string>();
        string path = Path.Combine(ExternalContentPaths.StreamingRoot, "runtime_settings_defaults.json");

        try
        {
            if (!File.Exists(path))
                return result;

            string json = File.ReadAllText(path);
            GlobalRuntimeSettingsMetadata metadata = JsonUtility.FromJson<GlobalRuntimeSettingsMetadata>(json);
            if (metadata?.values == null)
                return result;

            foreach (RuntimeSettingValueEntry entry in metadata.values)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id))
                    continue;

                result[entry.id] = entry.value ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load runtime setting defaults: {ex.Message}");
        }

        return result;
    }

    private void ApplyPlaybackSpeedToAudio()
    {
        if (backingTrackSource == null)
            return;

        float speed = GetPlaybackSpeedScale();
        if (!Mathf.Approximately(backingTrackSource.pitch, speed))
            backingTrackSource.pitch = speed;
    }

    private void SyncAudioToSongTimer(bool playImmediately)
    {
        if (backingTrackSource == null || backingTrackSource.clip == null)
            return;

        float timelineAudioTime = audioSongTimer + (audioOffsetMs / 1000f);
        float audioTime = Mathf.Clamp(timelineAudioTime, 0f, backingTrackSource.clip.length);

        if (Mathf.Abs(backingTrackSource.time - audioTime) > 0.04f)
            backingTrackSource.time = audioTime;

        bool shouldBeSilentForCountdown = timelineAudioTime <= 0f;
        if (shouldBeSilentForCountdown)
        {
            if (backingTrackSource.isPlaying)
                backingTrackSource.Pause();
            return;
        }

        if (playImmediately)
        {
            if (!backingTrackSource.isPlaying && audioTime < backingTrackSource.clip.length)
                backingTrackSource.Play();
        }
        else if (backingTrackSource.isPlaying)
        {
            backingTrackSource.Pause();
        }
    }

    private List<NoteData> BuildDemoSong()
    {
        List<NoteData> demo = new List<NoteData>();
        float t = 2.0f;
        int idCounter = 0;

        Action<float, int, int, string> addNote = (time, str, fret, note) => {
            demo.Add(new NoteData { id = idCounter++, time = time, duration = 0, stringIdx = str, fret = fret, note = note });
        };

        // --- Block 1: High E (String 5) - Slow spacing ---
        addNote(t, 5, 0, "E4"); t += 1.0f;
        addNote(t, 5, 0, "E4"); t += 1.0f;
        addNote(t, 5, 0, "E4"); t += 1.0f;

        // --- Block 2: High E (String 5) - Fast picking ---
        addNote(t, 5, 0, "E4"); t += 0.25f;
        addNote(t, 5, 0, "E4"); t += 0.25f;
        addNote(t, 5, 0, "E4"); t += 0.25f;
        addNote(t, 5, 0, "E4"); t += 0.25f;
        addNote(t, 5, 0, "E4"); t += 1.5f;

        // --- Block 3: Low E (String 0) and High B (String 4) mix ---
        addNote(t, 0, 0, "E2"); t += 1.0f;
        addNote(t, 4, 0, "B3"); t += 1.0f;
        addNote(t, 0, 0, "E2"); t += 0.5f;
        addNote(t, 4, 0, "B3"); t += 0.5f;
        addNote(t, 0, 0, "E2"); t += 0.5f;
        addNote(t, 4, 0, "B3"); t += 1.5f;

        // --- Block 4: Open Chords ---
        
        // 3-String Open Chord: G, B, High E (Strings 3, 4, 5)
        addNote(t, 3, 0, "G3");
        addNote(t, 4, 0, "B3");
        addNote(t, 5, 0, "E4"); t += 1.5f;

        // 5-String Open Chord: Low E, D, G, B, High E (Skipping the A string)
        addNote(t, 0, 0, "E2");
        addNote(t, 2, 0, "D3");
        addNote(t, 3, 0, "G3");
        addNote(t, 4, 0, "B3");
        addNote(t, 5, 0, "E4"); t += 1.5f;

        // Full 6-String Open E Minor Chord
        addNote(t, 0, 0, "E2");
        addNote(t, 1, 2, "B2");
        addNote(t, 2, 2, "E3");
        addNote(t, 3, 0, "G3");
        addNote(t, 4, 0, "B3");
        addNote(t, 5, 0, "E4"); t += 2.0f;

        return demo;
    }

    private void GenerateTabSections()
    {
        tabSections = new List<TabSectionData>();
        if (chartNotes == null || chartNotes.Count == 0) return;

        float maxTime = chartNotes.Max(n => n.time + n.duration);
        float sectionDuration = GetEffectiveTabSectionDuration();
        int totalSections = Mathf.Max(2, Mathf.CeilToInt(maxTime / sectionDuration) + 1);

        for (int i = 0; i < totalSections; i++)
        {
            float start = i * sectionDuration;
            float end = start + sectionDuration;

            var ids = chartNotes
                .Where(n => n.time >= start && n.time < end)
                .Select(n => n.id)
                .ToList();

            tabSections.Add(new TabSectionData
            {
                index = i,
                startTime = start,
                endTime = end,
                noteIds = ids
            });
        }
    }
}
