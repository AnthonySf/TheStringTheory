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
        BlueSky = 2,
        Space = 3
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
        Sunset = 1,
        Midnight = 2
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

    [Header("Detector Hint UDP")]
    public bool sendDetectorHintsToPython = true;
    public string detectorHintIp = "127.0.0.1";
    public int detectorHintPort = 9001;
    public float detectorHintSendIntervalSeconds = 0.05f;
    public float detectorHintLookbackSeconds = 0.08f;
    public float detectorHintLookaheadSeconds = 0.45f;
    public float detectorHintInterpolationStepSeconds = 0.035f;
    public int detectorHintMaxWindowsPerPacket = 20;
    private UdpClient detectorHintClient;
    private IPEndPoint detectorHintEndpoint;
    private float lastDetectorHintSendRealtime = -999f;
    private bool detectorHintForceSend = true;

    [Header("Notes Detector")]
    public bool autoLaunchNotesDetector = false;
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
    public Color highwayBackgroundColor = new Color(0.01f, 0.015f, 0.045f, 1f);

    [Header("Highway 3D Dimensions")]
    public int TotalFrets = 24;
    public float FretSpacing = 4.0f;
    public float StrikeLineZ = -5.0f;
    public float SpawnZ = 100.0f;
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
    public float highwayLaneGuideYOffset = 0f;
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
    public bool tabSkyUseStageBackdrop = false;
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
    public Color tabSkyMidnightTopColor = new Color(0.008f, 0.010f, 0.030f, 1f);
    public Color tabSkyMidnightMidColor = new Color(0.022f, 0.032f, 0.082f, 1f);
    public Color tabSkyMidnightBottomColor = new Color(0.055f, 0.075f, 0.155f, 1f);
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
    public Color tabSkyMidnightCloudTopTint = new Color(0.22f, 0.24f, 0.34f, 1f);
    public Color tabSkyMidnightCloudBottomTint = new Color(0.035f, 0.042f, 0.085f, 1f);
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

    [Header("Background - Space")]
    public Color tabSpaceBackgroundColor = new Color(0.015f, 0.028f, 0.09f, 1f);
    public Color tabSpaceGlowColor = new Color(0.16f, 0.82f, 1f, 1f);
    public Color tabSpaceAccentColor = new Color(0.50f, 0.38f, 0.96f, 1f);
    [Min(0.01f)] public float tabSpaceFlowSpeed = 0.58f;
    [Range(0.1f, 4f)] public float tabSpaceLineIntensity = 1.35f;
    [Range(0.1f, 4f)] public float tabSpaceSparkIntensity = 1.15f;

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

    private struct DetectorHintWindow
    {
        public float startTime;
        public float endTime;
        public HashSet<int> pitches;

        public DetectorHintWindow(float start, float end, HashSet<int> notePitches)
        {
            startTime = start;
            endTime = end;
            pitches = notePitches;
        }
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
    private bool noteByNoteModeEnabled;
    private bool noteByNoteWaitingForMatch;
    private bool heroModeEnabled = false;
    private HighwayCharacterDisplayMode highwayCharacterDisplayMode = HighwayCharacterDisplayMode.Always;
    private int heroModeHeartCount = 5;
    private float noteByNoteWaitingNoteTime = -1f;
    private int selectedPauseActionIndex;
    private int selectedGameModesIndex;
    private int selectedHeroModeSettingsIndex;
    private int selectedSongEndActionIndex;
    private int selectedSongSettingsIndex;
    private float pauseSeekStepSeconds = 3.2f;
    private float playbackSpeedPercent = 100f;
    private float heldUiHorizontalNextRepeatTime = -1f;
    private int heldUiHorizontalDirection;
    private string heldUiHorizontalContext = string.Empty;

    private bool loopEnabled;
    private float loopStartTime;
    private float loopEndTime;
    private int selectedLoopMarker = 1;
    private bool showLoopSettings;
    private bool showLoopPausePopup;
    private int selectedLoopPausePopupIndex;
    private float loopPauseDurationSeconds;
    private float loopRestartPauseRemainingSeconds;
    private bool loopSettingsPreviewPlaying;
    private bool loopSettingsOpenedFromGameModes;
    private GuitarRenderMode loopSettingsReturnRenderMode = GuitarRenderMode.Tabs;
    private bool showOffsetHelper;
    private bool showGameModes;
    private bool showHeroModeSettings;
    private bool showToneLab;
    private bool offsetHelperAdjusting;
    private bool offsetHelperPreviewPlaying;
    private float offsetHelperAnchorTime;
    private float offsetHelperPreviewStartTime;
    private float offsetHelperPreviewEndTime;
    private GuitarRenderMode offsetHelperReturnRenderMode = GuitarRenderMode.Tabs;
    private List<GameplayNoteState> offsetHelperSavedNoteStates;
    private float offsetHelperSavedSongTimer;
    private float offsetHelperSavedAudioSongTimer;
    private readonly HashSet<int> offsetHelperSavedSessionScoredNoteIds = new HashSet<int>();
    private int offsetHelperSavedSessionScoreHits;
    private int offsetHelperSavedSessionScoreMisses;
    private float offsetHelperSavedSessionScorePercent;
    private bool offsetHelperSavedScoreSaveInvalidated;
    private float offsetHelperWorkingOffsetMs;
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
        public float songVolumePercent = 100f;
        public SongPlaybackAudioMode playbackAudioMode = SongPlaybackAudioMode.Generated;
        public bool hasSavedLoopWindow = false;
        public float loopStartTime = 0f;
        public float loopEndTime = 0f;
        public float loopPauseDurationSeconds = 0f;
        public bool useAutoTrackSelection = true;
        public string selectedMusicXmlPartId;
        public bool useAllGeneratedPlaybackParts = true;
        public List<string> generatedEnabledPartIds = new List<string>();
        public List<GeneratedPlaybackSelectionOverride> generatedPlaybackSelectionOverrides = new List<GeneratedPlaybackSelectionOverride>();
        public float bestScorePercent = 0f;
        public List<TrackScoreEntry> trackScores = new List<TrackScoreEntry>();
        public List<TrackOffsetOverride> trackOffsetOverrides = new List<TrackOffsetOverride>();
    }

    [Serializable]
    private class GeneratedPlaybackSelectionOverride
    {
        public string partId;
        public bool useAllGeneratedPlaybackParts;
        public List<string> generatedEnabledPartIds = new List<string>();
    }

    [Serializable]
    private class TrackScoreEntry
    {
        public string partId;
        public string displayName;
        public float bestScorePercent;
        public float heroBestScorePercent;
        public int heroBestHeartsRemaining;
        public int heroBestHeartsTotal;
    }

    private readonly struct HeroScoreSummary
    {
        public readonly float percent;
        public readonly int heartsRemaining;
        public readonly int heartsTotal;

        public HeroScoreSummary(float percent, int heartsRemaining, int heartsTotal)
        {
            this.percent = Mathf.Clamp(percent, 0f, 100f);
            this.heartsRemaining = Mathf.Max(0, heartsRemaining);
            this.heartsTotal = Mathf.Max(0, heartsTotal);
        }

        public bool IsAvailable => percent > 0.01f && heartsTotal > 0;
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
    private int selectedMainMenuIndex;
    private bool showSongSelection;
    private bool songSelectionSongConfirmed;
    private bool showTrackSelection;
    private bool showGlobalSettings;
    private int selectedGlobalSettingsTopIndex;
    private int selectedGlobalSettingsItemIndex;
    private string activeGlobalSettingsCategory = string.Empty;
    private int selectedSongListIndex;
    private int songListScrollOffset;
    private int selectedTrackListIndex;
    private int trackListScrollOffset;
    private SongLibraryEntry pendingTrackSelectionSong;
    private readonly List<SongLibraryEntry> availableSongs = new List<SongLibraryEntry>();
    private readonly List<MusicXmlLoader.MusicXmlPartSummary> pendingTrackSelectionParts = new List<MusicXmlLoader.MusicXmlPartSummary>();
    private readonly Dictionary<string, SongMetadata> cachedSongMetadataByPath = new Dictionary<string, SongMetadata>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> cachedSongMetadataTicksByPath = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<MusicXmlLoader.MusicXmlPartSummary>> cachedTrackSummariesByNotationPath = new Dictionary<string, List<MusicXmlLoader.MusicXmlPartSummary>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> cachedTrackSummaryTicksByNotationPath = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
    private float audioOffsetMs;
    private float globalAudioOffsetMs;
    private bool useTrackOffsetForCurrentTrack;
    private float tabSpeedOffsetPercent = 100f;
    private float songStartDelaySeconds = 2.0f;
    private float songVolumePercent = 100f;
    private SongPlaybackAudioMode songPlaybackAudioMode = SongPlaybackAudioMode.Generated;
    private SongMetadata songMetadata = new SongMetadata();
    private GeneratedSongPlayer generatedSongPlayer;
    private GeneratedPlaybackArrangement generatedPlaybackSourceArrangement;
    private GeneratedPlaybackArrangement generatedPlaybackArrangement;
    private bool useAllGeneratedPlaybackParts = true;
    private List<string> generatedEnabledPartIds = new List<string>();
    private bool showGeneratedAudioTrackSelectionPopup;
    private int selectedGeneratedAudioTrackSelectionIndex;
    private float currentSongBestScorePercent;
    private float currentTrackBestScorePercent;
    private float currentTrackHeroBestScorePercent;
    private int currentTrackHeroBestHeartsRemaining;
    private int currentTrackHeroBestHeartsTotal;
    private bool scoreSaveInvalidated;
    private readonly HashSet<int> sessionScoredNoteIds = new HashSet<int>();
    private int sessionScoreHits;
    private int sessionScoreMisses;
    private float currentSessionScorePercent;
    private const string SelectedSongDirectoryPrefsKey = "guitar_selected_song_directory";
    private const string HeroModeEnabledPrefsKey = "guitar_hero_mode_enabled";
    private const string HeroModeHeartCountPrefsKey = "guitar_hero_mode_heart_count";
    private const string NativeDetectorInputDevicePrefsKey = "guitar_native_detector_input_device";
    private UnityToneLabRuntime unityToneLabRuntime;
    private UnityToneLabOverlay unityToneLabOverlay;
    private bool isLoadingBackingTrack;
    private string backingTrackLoadError = string.Empty;
    private bool songHasEnded;
    private bool songEndedAsGameOver;
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
    private float lastMainMenuKeyboardInputTime = -10f;
    private const float ArrowDoubleTapThreshold = 0.35f;
    private const float NoteByNoteTimeEpsilon = 0.0001f;
    private const int MainMenuOptionCount = 6;
    private const int NotesDetectorTestOptionCount = 3;
    private const float MainMenuHoverLockSeconds = 0.20f;
    private ToneLabReturnContext toneLabReturnContext;
    private NotesDetectorBackendMode notesDetectorBackendMode = NotesDetectorBackendMode.NativeEmbeddedBridge;
    private NativeNotesDetectorBridge nativeNotesDetectorBridge;
    private readonly List<NativeDetectorInputDevice> nativeNotesDetectorInputDevices = new List<NativeDetectorInputDevice>();
    private NativeDetectorRuntimeInfo nativeNotesDetectorRuntimeInfo = new NativeDetectorRuntimeInfo();
    private int selectedNativeNotesDetectorInputDeviceIndex = -1;
    private bool showNotesDetectorTestMenu;
    private int selectedNotesDetectorTestIndex;
    private bool showNotesDetectorRoutinePopup;
    private int notesDetectorRoutineStageIndex;
    private float notesDetectorRoutineMatchedSinceTime = -1f;
    private float notesDetectorRoutineOpenedTime;

    private sealed class NotesDetectorRoutineStep
    {
        public string Instruction;
        public string TargetLabel;
        public bool RequireSilence;
        public int[] TargetMidis;
        public string[] TabFretsTopDown;
    }

    private static string[] CreateNotesDetectorRoutineTabFrets(string lowE, string a, string d, string g, string b, string highE)
    {
        return new[] { highE, b, g, d, a, lowE };
    }

    private readonly List<NotesDetectorRoutineStep> notesDetectorRoutineSteps = new List<NotesDetectorRoutineStep>
    {
        new NotesDetectorRoutineStep
        {
            Instruction = "Silence the strings and stop any ringing or handling noise.",
            TargetLabel = "TARGET  SILENCE",
            RequireSilence = true,
            TargetMidis = Array.Empty<int>(),
            TabFretsTopDown = CreateNotesDetectorRoutineTabFrets("-", "-", "-", "-", "-", "-")
        },
        new NotesDetectorRoutineStep
        {
            Instruction = "Play the open low E string clearly and let it ring for a moment.",
            TargetLabel = "TARGET  E2",
            TargetMidis = new[] { 40 },
            TabFretsTopDown = CreateNotesDetectorRoutineTabFrets("0", "-", "-", "-", "-", "-")
        },
        new NotesDetectorRoutineStep
        {
            Instruction = "Play the A string open clearly and let it ring.",
            TargetLabel = "TARGET  A2",
            TargetMidis = new[] { 45 },
            TabFretsTopDown = CreateNotesDetectorRoutineTabFrets("-", "0", "-", "-", "-", "-")
        },
        new NotesDetectorRoutineStep
        {
            Instruction = "Play the D string open clearly and let it ring.",
            TargetLabel = "TARGET  D3",
            TargetMidis = new[] { 50 },
            TabFretsTopDown = CreateNotesDetectorRoutineTabFrets("-", "-", "0", "-", "-", "-")
        },
        new NotesDetectorRoutineStep
        {
            Instruction = "Play the G string open clearly and let it ring.",
            TargetLabel = "TARGET  G3",
            TargetMidis = new[] { 55 },
            TabFretsTopDown = CreateNotesDetectorRoutineTabFrets("-", "-", "-", "0", "-", "-")
        },
        new NotesDetectorRoutineStep
        {
            Instruction = "Play the B string open clearly and let it ring.",
            TargetLabel = "TARGET  B3",
            TargetMidis = new[] { 59 },
            TabFretsTopDown = CreateNotesDetectorRoutineTabFrets("-", "-", "-", "-", "0", "-")
        },
        new NotesDetectorRoutineStep
        {
            Instruction = "Play the high E string open clearly and let it ring.",
            TargetLabel = "TARGET  E4",
            TargetMidis = new[] { 64 },
            TabFretsTopDown = CreateNotesDetectorRoutineTabFrets("-", "-", "-", "-", "-", "0")
        },
        new NotesDetectorRoutineStep
        {
            Instruction = "Play low E string fret 3 so the detector should hear G.",
            TargetLabel = "TARGET  G2",
            TargetMidis = new[] { 43 },
            TabFretsTopDown = CreateNotesDetectorRoutineTabFrets("3", "-", "-", "-", "-", "-")
        },
        new NotesDetectorRoutineStep
        {
            Instruction = "Play the A string fret 2 so the detector should hear B.",
            TargetLabel = "TARGET  B2",
            TargetMidis = new[] { 47 },
            TabFretsTopDown = CreateNotesDetectorRoutineTabFrets("-", "2", "-", "-", "-", "-")
        },
        new NotesDetectorRoutineStep
        {
            Instruction = "Play the G string fret 2 so the detector should hear A.",
            TargetLabel = "TARGET  A3",
            TargetMidis = new[] { 57 },
            TabFretsTopDown = CreateNotesDetectorRoutineTabFrets("-", "-", "-", "2", "-", "-")
        },
        new NotesDetectorRoutineStep
        {
            Instruction = "Play the B string fret 3 so the detector should hear D.",
            TargetLabel = "TARGET  D4",
            TargetMidis = new[] { 62 },
            TabFretsTopDown = CreateNotesDetectorRoutineTabFrets("-", "-", "-", "-", "3", "-")
        },
        new NotesDetectorRoutineStep
        {
            Instruction = "Play the high E string fret 3 so the detector should hear G.",
            TargetLabel = "TARGET  G4",
            TargetMidis = new[] { 67 },
            TabFretsTopDown = CreateNotesDetectorRoutineTabFrets("-", "-", "-", "-", "-", "3")
        },
        new NotesDetectorRoutineStep
        {
            Instruction = "Strum a clean open G chord shape.",
            TargetLabel = "TARGET  G MAJOR",
            TargetMidis = new[] { 43, 47, 50, 55, 59, 67 },
            TabFretsTopDown = CreateNotesDetectorRoutineTabFrets("3", "2", "0", "0", "0", "3")
        },
        new NotesDetectorRoutineStep
        {
            Instruction = "Strum a clean open C chord shape.",
            TargetLabel = "TARGET  C MAJOR",
            TargetMidis = new[] { 48, 52, 55, 60, 64 },
            TabFretsTopDown = CreateNotesDetectorRoutineTabFrets("-", "3", "2", "0", "1", "0")
        },
        new NotesDetectorRoutineStep
        {
            Instruction = "Strum a clean open D minor chord shape.",
            TargetLabel = "TARGET  D MINOR",
            TargetMidis = new[] { 50, 57, 62, 65 },
            TabFretsTopDown = CreateNotesDetectorRoutineTabFrets("-", "-", "0", "2", "3", "1")
        }
    };
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

    private static readonly string[] HighwayCharacterDisplayModeOptions =
    {
        "Always",
        "Never",
        "Only In Hero Mode"
    };

    private enum ToneLabReturnContext
    {
        Pause,
        MainMenu
    }

    private enum NotesDetectorBackendMode
    {
        ExternalProcessUdp,
        NativeEmbeddedBridge
    }

    private void Start()
    {
        Application.targetFrameRate = 60;
        ExternalContentBootstrap.EnsureRuntimeContentReady();
        Debug.Log($"[GuitarBridgeServer] Using persistent content folder: {ExternalContentPaths.PersistentRoot}");
        Debug.Log($"[NotesDetector] Start() called on '{gameObject.name}'. autoLaunchNotesDetector={autoLaunchNotesDetector}, enabled={enabled}, activeInHierarchy={gameObject.activeInHierarchy}, platform={Application.platform}");
        isRunning = true;
        BuildNoteIndices();
        StartUdpThread();
        EnsureBackingTrackSource();
        EnsureGeneratedSongPlayerInitialized();
        RegisterRuntimeSettings();
        LoadGlobalRuntimeSettingsMetadata();
        LoadHeroModePreferences();
        LoadNativeDetectorPreferences();
        StartConfiguredNotesDetectorBackend();
        bool startInMainMenu = true;
        showMainMenu = startInMainMenu;
        mainMenuFlowActive = startInMainMenu;
        isPaused = startInMainMenu;
        LoadTestSong();
        isPaused = startInMainMenu;
        EnsureToneLabRuntimeComponent();
        unityToneLabRuntime?.StartBackgroundMonitoring();
        EnsureRenderer();
        SyncAudioToSongTimer(playImmediately: false);
    }

    private void Update()
    {
        HandlePauseControls();

        bool loopPreviewActive = showLoopSettings && loopSettingsPreviewPlaying;
        bool offsetHelperPreviewActive = showOffsetHelper && offsetHelperAdjusting && offsetHelperPreviewPlaying;
        bool loopGapActive = loopRestartPauseRemainingSeconds > 0.0001f;
        float songTimeBeforeAdvance = songTimer;

        if (loopGapActive)
        {
            songTimer = loopStartTime;
            audioSongTimer = loopStartTime;
            loopRestartPauseRemainingSeconds = Mathf.Max(0f, loopRestartPauseRemainingSeconds - Time.deltaTime);
            loopGapActive = loopRestartPauseRemainingSeconds > 0.0001f;
        }

        if ((!isPaused || loopPreviewActive || offsetHelperPreviewActive) && !loopGapActive)
        {
            audioSongTimer += Time.deltaTime * GetPlaybackSpeedScale();
            songTimer += Time.deltaTime * GetPlaybackSpeedScale();
            if (showOffsetHelper)
                HandleOffsetHelperLoopPlayback();
            else
                HandleLoopPlayback();
            loopGapActive = loopRestartPauseRemainingSeconds > 0.0001f;
        }

        ApplyNoteByNoteTransportGate(songTimeBeforeAdvance, loopPreviewActive, offsetHelperPreviewActive, loopGapActive);

        UpdateSongEndState();

        ApplyPlaybackSpeedToAudio();
        SyncAudioToSongTimer(playImmediately: ShouldPlaybackAudio(loopPreviewActive, offsetHelperPreviewActive, loopGapActive));

        if (midiTrackIndex != currentLoadedTrackIndex)
            LoadTestSong(preservePauseUiState: isPaused || showMainMenu || showSongSettings || showSongSelection || showTrackSelection || showGlobalSettings || showLoopPausePopup);

        ParseDetectorState();
        RefreshDetectorBackendStatus();
        if (isPaused)
            UpdateSessionScoreState();

        if (!isPaused)
        {
            PruneHistory();
            UpdateGameplayStates();
            UpdateNoteByNoteWaitingStateAfterJudgment(loopPreviewActive, offsetHelperPreviewActive);
            UpdateSessionScoreState();
            TryTriggerHeroModeGameOver();
            UpdateAndPersistSongBestScore();
        }

        UpdateInputLevelEstimate();
        SendDetectorHintPacketIfNeeded();

        EnsureRenderer();

        if (activeRenderer != null)
            activeRenderer.Render(BuildSnapshot());

        if (unityToneLabOverlay != null)
        {
            unityToneLabOverlay.SetVisible(showToneLab);
            if (showToneLab)
                unityToneLabOverlay.RefreshUi(syncControls: false);
        }

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

        if (showLoopPausePopup)
        {
            HandleLoopPausePopupControls();
            return;
        }

        if (showHeroModeSettings)
        {
            HandleHeroModeSettingsControls();
            return;
        }

        if (showToneLab)
        {
            HandleToneLabControls();
            return;
        }

        if (showNotesDetectorTestMenu)
        {
            HandleNotesDetectorTestControls();
            return;
        }

        if (showGameModes)
        {
            HandleGameModesControls();
            return;
        }

        if (showOffsetHelper)
        {
            HandleOffsetHelperControls();
            return;
        }

        if (showLoopSettings)
        {
            HandleLoopSettingsControls();
            return;
        }

        if (Input.GetKeyDown(KeyCode.S) && renderMode == GuitarRenderMode.Tabs && (isPaused || showSongSettings))
        {
            showSongSettings = !showSongSettings;
            if (showSongSettings)
                selectedSongSettingsIndex = 0;
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
            HandleSongEndControls();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            isPaused = !isPaused;
            if (isPaused)
                selectedPauseActionIndex = 1;
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

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ResumePlaybackFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.M))
        {
            SetPauseActionSelectionFromUi(7);
            OpenMainMenuFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            SetPauseActionSelectionFromUi(4);
            OpenLibraryFromPauseFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            SetPauseActionSelectionFromUi(6);
            OpenToneLabFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            MovePauseActionSelectionFromUi(-1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            MovePauseActionSelectionFromUi(1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ActivateSelectedPauseActionFromUi();
            return;
        }

        if (selectedPauseActionIndex == 0)
        {
            int horizontalDirection = GetHeldHorizontalArrowDirection();
            if (ConsumeHeldHorizontalUiStep("pause-speed", horizontalDirection))
            {
                AdjustPauseSpeedFromUi(horizontalDirection * 5);
                return;
            }

            return;
        }

        ConsumeHeldHorizontalUiStep("pause-speed", 0);

        if (selectedPauseActionIndex == 3)
        {
            int horizontalDirection = GetHeldHorizontalArrowDirection();
            if (ConsumeHeldHorizontalUiStep("pause-audio-mode", horizontalDirection))
            {
                CycleSongPlaybackAudioModeFromUi(horizontalDirection);
                return;
            }

            return;
        }

        ConsumeHeldHorizontalUiStep("pause-audio-mode", 0);


        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            selectedLoopMarker = 1;
            SeekSongTimeFromUserNavigation(loopStartTime, false);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            selectedLoopMarker = 2;
            SeekSongTimeFromUserNavigation(loopEndTime, false);
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

        SeekSongTimeFromUserNavigation(songTimer + (seekDirection * pauseSeekStepSeconds * Time.deltaTime), false);
        SnapSongTimeIntoLoopWindowIfNeeded();
    }

    private void HandleLoopSettingsControls()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            OpenLoopPausePopup();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleLoopSettingsPreviewPlayback();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            selectedLoopMarker = 1;
            UpdateSelectedLoopMarker(songTimer);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            selectedLoopMarker = 2;
            UpdateSelectedLoopMarker(songTimer);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            selectedLoopMarker = 3;
            return;
        }

        float seekDirection = 0f;
        if (Input.GetKey(KeyCode.LeftArrow))
            seekDirection -= 1f;
        if (Input.GetKey(KeyCode.RightArrow))
            seekDirection += 1f;

        if (Mathf.Approximately(seekDirection, 0f))
            return;

        bool moveLoopMarker = selectedLoopMarker == 1 || selectedLoopMarker == 2;
        SeekSongTimeFromUserNavigation(songTimer + (seekDirection * pauseSeekStepSeconds * Time.deltaTime), moveLoopMarker);
    }

    private void HandleLoopPausePopupControls()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            CloseLoopPausePopupBackToLoopSettingsFromUi();
            return;
        }

        int horizontalDirection = GetHeldHorizontalArrowDirection();
        if (ConsumeHeldHorizontalUiStep("loop-pause-duration", horizontalDirection))
        {
            AdjustLoopPauseDurationFromUi(horizontalDirection * 0.05f);
            return;
        }

        if (horizontalDirection == 0)
            ConsumeHeldHorizontalUiStep("loop-pause-duration", 0);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ConfirmLoopPausePopupFromUi();
            return;
        }
    }

    private void HandleGameModesControls()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            CloseGameModesFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            MoveGameModesSelectionFromUi(-1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            MoveGameModesSelectionFromUi(1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ActivateSelectedGameModesActionFromUi();
            return;
        }
    }

    private void HandleHeroModeSettingsControls()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            selectedHeroModeSettingsIndex = selectedHeroModeSettingsIndex == 0 ? 1 : 0;
            ConsumeHeldHorizontalUiStep("hero-mode-hearts", 0);
            return;
        }

        int horizontalDirection = GetHeldHorizontalArrowDirection();
        if (selectedHeroModeSettingsIndex == 0 &&
            ConsumeHeldHorizontalUiStep("hero-mode-hearts", horizontalDirection, 0.42f, 0.14f))
        {
            AdjustHeroModeHeartCountFromUi(horizontalDirection);
            return;
        }

        ConsumeHeldHorizontalUiStep("hero-mode-hearts", 0);

        if (Input.GetKeyDown(KeyCode.Escape) ||
            Input.GetKeyDown(KeyCode.Backspace))
        {
            CloseHeroModeSettingsFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (selectedHeroModeSettingsIndex == 1)
                CloseHeroModeSettingsFromUi();
            return;
        }
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

        SeekSongTimeFromUserNavigation(targetTime, false);
    }

    private bool ShouldUseNoteByNotePlayback(bool loopPreviewActive, bool offsetHelperPreviewActive, bool loopGapActive)
    {
        return noteByNoteModeEnabled &&
               !isPaused &&
               !loopPreviewActive &&
               !offsetHelperPreviewActive &&
               !loopGapActive &&
               !showMainMenu &&
               !showSongSettings &&
               !showSongSelection &&
               !showTrackSelection &&
               !showGlobalSettings &&
               !showLoopSettings &&
               !showLoopPausePopup &&
               !showOffsetHelper &&
               !songHasEnded;
    }

    private bool ShouldPlaybackAudio(bool loopPreviewActive, bool offsetHelperPreviewActive, bool loopGapActive)
    {
        return ((!isPaused || loopPreviewActive || offsetHelperPreviewActive) && !loopGapActive && !noteByNoteWaitingForMatch);
    }

    private void ClearNoteByNoteWaitingState()
    {
        noteByNoteWaitingForMatch = false;
        noteByNoteWaitingNoteTime = -1f;
    }

    private void ApplyNoteByNoteTransportGate(float previousSongTime, bool loopPreviewActive, bool offsetHelperPreviewActive, bool loopGapActive)
    {
        if (!ShouldUseNoteByNotePlayback(loopPreviewActive, offsetHelperPreviewActive, loopGapActive))
        {
            if (!noteByNoteModeEnabled)
                ClearNoteByNoteWaitingState();
            return;
        }

        if (noteByNoteWaitingForMatch)
        {
            if (HasUnresolvedNotesAtTime(noteByNoteWaitingNoteTime))
            {
                songTimer = noteByNoteWaitingNoteTime;
                audioSongTimer = noteByNoteWaitingNoteTime;
                return;
            }

            ClearNoteByNoteWaitingState();
        }

        if (TryGetPendingNoteTimeInRange(previousSongTime, songTimer, out float pendingNoteTime))
        {
            noteByNoteWaitingForMatch = true;
            noteByNoteWaitingNoteTime = pendingNoteTime;
            songTimer = pendingNoteTime;
            audioSongTimer = pendingNoteTime;
        }
    }

    private void UpdateNoteByNoteWaitingStateAfterJudgment(bool loopPreviewActive, bool offsetHelperPreviewActive)
    {
        if (!noteByNoteWaitingForMatch)
            return;

        if (!ShouldUseNoteByNotePlayback(loopPreviewActive, offsetHelperPreviewActive, loopGapActive: false))
            return;

        if (HasUnresolvedNotesAtTime(noteByNoteWaitingNoteTime))
            return;

        ClearNoteByNoteWaitingState();
    }

    private bool TryGetPendingNoteTimeInRange(float rangeStart, float rangeEnd, out float targetTime)
    {
        targetTime = 0f;
        if (noteStates == null || noteStates.Count == 0)
            return false;

        float minTime = Mathf.Min(rangeStart, rangeEnd) - NoteByNoteTimeEpsilon;
        float maxTime = Mathf.Max(rangeStart, rangeEnd) + NoteByNoteTimeEpsilon;
        float best = float.MaxValue;

        for (int i = 0; i < noteStates.Count; i++)
        {
            GameplayNoteState noteState = noteStates[i];
            if (noteState == null || noteState.IsResolved)
                continue;

            float noteTime = noteState.data.time;
            if (noteTime < minTime || noteTime > maxTime)
                continue;

            if (noteTime < best)
                best = noteTime;
        }

        if (best == float.MaxValue)
            return false;

        targetTime = best;
        return true;
    }

    private bool HasUnresolvedNotesAtTime(float targetTime)
    {
        if (noteStates == null || noteStates.Count == 0)
            return false;

        for (int i = 0; i < noteStates.Count; i++)
        {
            GameplayNoteState noteState = noteStates[i];
            if (noteState == null || noteState.IsResolved)
                continue;

            if (Mathf.Abs(noteState.data.time - targetTime) <= NoteByNoteTimeEpsilon)
                return true;
        }

        return false;
    }

    private void CacheOffsetHelperRunState()
    {
        offsetHelperSavedSongTimer = songTimer;
        offsetHelperSavedAudioSongTimer = audioSongTimer;
        offsetHelperSavedNoteStates = CloneNoteStates(noteStates);
        offsetHelperSavedSessionScoredNoteIds.Clear();
        foreach (int noteId in sessionScoredNoteIds)
            offsetHelperSavedSessionScoredNoteIds.Add(noteId);
        offsetHelperSavedSessionScoreHits = sessionScoreHits;
        offsetHelperSavedSessionScoreMisses = sessionScoreMisses;
        offsetHelperSavedSessionScorePercent = currentSessionScorePercent;
        offsetHelperSavedScoreSaveInvalidated = scoreSaveInvalidated;
    }

    private void RestoreOffsetHelperRunState()
    {
        songTimer = offsetHelperSavedSongTimer;
        audioSongTimer = offsetHelperSavedAudioSongTimer;
        noteStates = offsetHelperSavedNoteStates != null ? CloneNoteStates(offsetHelperSavedNoteStates) : chartNotes.Select(n => new GameplayNoteState(n)).ToList();
        sessionScoredNoteIds.Clear();
        foreach (int noteId in offsetHelperSavedSessionScoredNoteIds)
            sessionScoredNoteIds.Add(noteId);
        sessionScoreHits = offsetHelperSavedSessionScoreHits;
        sessionScoreMisses = offsetHelperSavedSessionScoreMisses;
        currentSessionScorePercent = offsetHelperSavedSessionScorePercent;
        scoreSaveInvalidated = offsetHelperSavedScoreSaveInvalidated;
    }

    private void SeekOffsetHelperTime(float targetTime, bool syncAudio, bool playImmediately)
    {
        float duration = GetSongDurationSeconds();
        float clampedTime = duration > 0.001f
            ? Mathf.Clamp(targetTime, 0f, duration)
            : Mathf.Max(0f, targetTime);

        songTimer = clampedTime;
        audioSongTimer = clampedTime;
        recentNoteEvents.Clear();
        latestDetectedPitches.Clear();
        latestEventNotesText = "--";
        latestNoteEventId = 0;
        latestPacketHadEvent = false;
        Interlocked.Exchange(ref lastUdpPacketUtcTicks, 0L);
        MarkDetectorHintDirty();

        if (syncAudio)
            SyncAudioToSongTimer(playImmediately, forceSeek: true);
    }

    private float GetNearestChartNoteTime(float referenceTime)
    {
        if (chartNotes == null || chartNotes.Count == 0)
            return 0f;

        float bestTime = chartNotes[0].time;
        float bestDistance = Mathf.Abs(bestTime - referenceTime);
        for (int i = 1; i < chartNotes.Count; i++)
        {
            float candidate = chartNotes[i].time;
            float distance = Mathf.Abs(candidate - referenceTime);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTime = candidate;
            }
        }

        return Mathf.Max(0f, bestTime);
    }

    private bool TryGetAdjacentChartNoteTime(float referenceTime, bool moveForward, out float targetTime)
    {
        targetTime = referenceTime;
        if (chartNotes == null || chartNotes.Count == 0)
            return false;

        const float epsilon = 0.0001f;
        bool found = false;

        if (moveForward)
        {
            float best = float.MaxValue;
            for (int i = 0; i < chartNotes.Count; i++)
            {
                float candidate = chartNotes[i].time;
                if (candidate > referenceTime + epsilon && candidate < best)
                {
                    best = candidate;
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
                float candidate = chartNotes[i].time;
                if (candidate < referenceTime - epsilon && candidate > best)
                {
                    best = candidate;
                    found = true;
                }
            }

            if (found)
                targetTime = best;
        }

        return found;
    }

    private string GetOffsetHelperAnchorLabel()
    {
        if (chartNotes == null || chartNotes.Count == 0)
            return "NOTE";

        const float epsilon = 0.0001f;
        List<NoteData> notesAtTime = chartNotes
            .Where(note => Mathf.Abs(note.time - offsetHelperAnchorTime) <= epsilon)
            .OrderBy(note => note.stringIdx)
            .ThenBy(note => note.fret)
            .ToList();

        if (notesAtTime.Count <= 0)
            return "NOTE";

        if (notesAtTime.Count > 1)
            return $"CHORD ({notesAtTime.Count} NOTES)";

        NoteData noteData = notesAtTime[0];
        string fretText = noteData.fret < 0 ? "X" : noteData.fret.ToString();
        return $"STRING {noteData.stringIdx + 1} \u2022 FRET {fretText}";
    }

    private void MoveOffsetHelperAnchor(int delta)
    {
        if (delta == 0)
            return;

        if (TryGetAdjacentChartNoteTime(offsetHelperAnchorTime, delta > 0, out float targetTime))
        {
            offsetHelperAnchorTime = targetTime;
            SeekOffsetHelperTime(offsetHelperAnchorTime, syncAudio: true, playImmediately: false);
        }
    }

    private void StartOffsetHelperAdjustMode()
    {
        if (!showOffsetHelper)
            return;

        float duration = GetSongDurationSeconds();
        const float targetPreviewDuration = 2.0f;
        float halfPreviewDuration = targetPreviewDuration * 0.5f;
        offsetHelperPreviewStartTime = Mathf.Max(0f, offsetHelperAnchorTime - halfPreviewDuration);
        offsetHelperPreviewEndTime = offsetHelperPreviewStartTime + targetPreviewDuration;

        if (duration > 0.001f && offsetHelperPreviewEndTime > duration)
        {
            offsetHelperPreviewEndTime = duration;
            offsetHelperPreviewStartTime = Mathf.Max(0f, offsetHelperPreviewEndTime - targetPreviewDuration);
        }

        if (offsetHelperPreviewEndTime <= offsetHelperPreviewStartTime + 0.30f)
        {
            offsetHelperPreviewEndTime = offsetHelperPreviewStartTime + 0.30f;
            if (duration > 0.001f && offsetHelperPreviewEndTime > duration)
            {
                offsetHelperPreviewEndTime = duration;
                offsetHelperPreviewStartTime = Mathf.Max(0f, offsetHelperPreviewEndTime - 0.30f);
            }
        }

        offsetHelperAdjusting = true;
        offsetHelperPreviewPlaying = true;
        SeekOffsetHelperTime(offsetHelperPreviewStartTime, syncAudio: true, playImmediately: true);
    }

    private void ToggleOffsetHelperPreviewPlayback()
    {
        if (!showOffsetHelper || !offsetHelperAdjusting)
            return;

        offsetHelperPreviewPlaying = !offsetHelperPreviewPlaying;
        if (offsetHelperPreviewPlaying)
        {
            if (songTimer < offsetHelperPreviewStartTime - 0.0001f || songTimer > offsetHelperPreviewEndTime + 0.0001f)
                SeekOffsetHelperTime(offsetHelperPreviewStartTime, syncAudio: true, playImmediately: true);
            else
                SyncAudioToSongTimer(playImmediately: true, forceSeek: true);
        }
        else
        {
            SyncAudioToSongTimer(playImmediately: false, forceSeek: true);
        }
    }

    private void HandleOffsetHelperLoopPlayback()
    {
        if (!showOffsetHelper || !offsetHelperAdjusting || !offsetHelperPreviewPlaying)
            return;

        if (songTimer < offsetHelperPreviewStartTime - 0.0001f || songTimer > offsetHelperPreviewEndTime + 0.0001f)
        {
            songTimer = offsetHelperPreviewStartTime;
            audioSongTimer = offsetHelperPreviewStartTime;
            return;
        }

        if (songTimer < offsetHelperPreviewEndTime)
            return;

        songTimer = offsetHelperPreviewStartTime;
        audioSongTimer = offsetHelperPreviewStartTime;
    }

    private void AdjustOffsetHelperWorkingOffset(float deltaMs)
    {
        if (!showOffsetHelper || !offsetHelperAdjusting || Mathf.Abs(deltaMs) <= 0.001f)
            return;

        offsetHelperWorkingOffsetMs = Mathf.Clamp(offsetHelperWorkingOffsetMs + deltaMs, -2000f, 2000f);
        audioOffsetMs = offsetHelperWorkingOffsetMs;
        SyncAudioToSongTimer(playImmediately: offsetHelperPreviewPlaying, forceSeek: true);
    }

    public void OpenOffsetHelperFromUi()
    {
        if (!showSongSettings || chartNotes == null || chartNotes.Count == 0 || !hasBackingTrack)
            return;

        CacheOffsetHelperRunState();
        noteStates = chartNotes.Select(n => new GameplayNoteState(n)).ToList();
        sessionScoredNoteIds.Clear();
        sessionScoreHits = 0;
        sessionScoreMisses = 0;
        currentSessionScorePercent = 0f;
        offsetHelperWorkingOffsetMs = audioOffsetMs;
        offsetHelperAnchorTime = GetNearestChartNoteTime(offsetHelperSavedSongTimer);
        offsetHelperAdjusting = false;
        offsetHelperPreviewPlaying = false;
        offsetHelperPreviewStartTime = offsetHelperAnchorTime;
        offsetHelperPreviewEndTime = offsetHelperAnchorTime;
        showOffsetHelper = true;
        showSongSettings = false;
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSelection = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        isPaused = true;
        loopRestartPauseRemainingSeconds = 0f;
        offsetHelperReturnRenderMode = renderMode;

        if (renderMode == GuitarRenderMode.Highway3D)
            renderMode = GuitarRenderMode.Tabs;

        SeekOffsetHelperTime(offsetHelperAnchorTime, syncAudio: true, playImmediately: false);
    }

    private void ConfirmOffsetHelperFromUi()
    {
        if (!showOffsetHelper)
            return;

        SetEffectiveOffsetForCurrentScope(offsetHelperWorkingOffsetMs);
        SaveSongMetadata();
        CloseOffsetHelperBackToSongSettings(saveChanges: true);
    }

    private void CloseOffsetHelperBackToSongSettings(bool saveChanges)
    {
        if (!showOffsetHelper)
            return;

        showOffsetHelper = false;
        offsetHelperAdjusting = false;
        offsetHelperPreviewPlaying = false;

        if (!saveChanges)
            RefreshEffectiveAudioOffset();

        RestoreOffsetHelperRunState();

        if (renderMode != offsetHelperReturnRenderMode)
            renderMode = offsetHelperReturnRenderMode;

        showSongSettings = true;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false, forceSeek: true);
    }

    private void HandleMainMenuControls()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            MoveMainMenuSelectionFromUi(-1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            MoveMainMenuSelectionFromUi(1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
        {
            ActivateSelectedMainMenuFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            SetMainMenuSelectionFromUi(0);
            ContinueFromMainMenuFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            SetMainMenuSelectionFromUi(1);
            OpenSongSelectionFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            SetMainMenuSelectionFromUi(2);
            OpenGlobalSettingsFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            SetMainMenuSelectionFromUi(4);
            OpenToneLabFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            SetMainMenuSelectionFromUi(5);
            ExitGameFromUi();
            return;
        }
    }

    private void HandleNotesDetectorTestControls()
    {
        if (showNotesDetectorRoutinePopup)
        {
            UpdateNotesDetectorRoutine();
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
                CloseNotesDetectorRoutineFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            CloseNotesDetectorTestFromUi();
            return;
        }
    }

    private void UpdateNotesDetectorRoutine()
    {
        if (!showNotesDetectorRoutinePopup)
            return;

        if (notesDetectorRoutineStageIndex < 0)
            notesDetectorRoutineStageIndex = 0;

        int stepCount = notesDetectorRoutineSteps.Count;
        if (notesDetectorRoutineStageIndex >= stepCount)
            return;

        NotesDetectorRoutineStep currentStep = notesDetectorRoutineSteps[notesDetectorRoutineStageIndex];
        bool matched = currentStep.RequireSilence
            ? DetectorRoutineMatchesSilence()
            : DetectorRoutineMatchesTargets(currentStep.TargetMidis);

        if (currentStep.RequireSilence)
        {
            if (matched)
            {
                if (notesDetectorRoutineMatchedSinceTime < 0f)
                    notesDetectorRoutineMatchedSinceTime = Time.unscaledTime;

                if (Time.unscaledTime - notesDetectorRoutineMatchedSinceTime >= 0.35f)
                {
                    notesDetectorRoutineStageIndex++;
                    notesDetectorRoutineMatchedSinceTime = -1f;
                }
            }
            else
            {
                notesDetectorRoutineMatchedSinceTime = -1f;
            }
        }
        else
        {
            if (notesDetectorRoutineMatchedSinceTime < 0f && matched)
                notesDetectorRoutineMatchedSinceTime = Time.unscaledTime;

            if (notesDetectorRoutineMatchedSinceTime >= 0f &&
                Time.unscaledTime - notesDetectorRoutineMatchedSinceTime >= 0.45f)
            {
                notesDetectorRoutineStageIndex++;
                notesDetectorRoutineMatchedSinceTime = -1f;
            }
        }
    }

    private bool DetectorRoutineMatchesSilence()
    {
        bool fastSilent = latestDetectedPitches == null || latestDetectedPitches.Count == 0;
        bool aiSilent = string.IsNullOrWhiteSpace(latestEventNotesText) || latestEventNotesText == "--";
        return fastSilent && aiSilent;
    }

    private bool DetectorRoutineMatchesTargets(int[] targetMidis)
    {
        if (targetMidis == null || targetMidis.Length == 0)
            return false;

        HashSet<int> combinedPitches = new HashSet<int>();
        if (latestDetectedPitches != null)
            combinedPitches.UnionWith(latestDetectedPitches);

        if (!string.IsNullOrWhiteSpace(latestEventNotesText) && latestEventNotesText != "--")
            ParseNoteCsvIntoSet(latestEventNotesText, combinedPitches);

        for (int i = 0; i < targetMidis.Length; i++)
        {
            if (!combinedPitches.Contains(targetMidis[i]))
                return false;
        }

        return true;
    }

    private string GetNotesDetectorRoutineInstructionText()
    {
        if (!showNotesDetectorRoutinePopup)
            return string.Empty;

        if (notesDetectorRoutineStageIndex >= notesDetectorRoutineSteps.Count)
            return "Routine complete. Your detector is responding through the full string set.";

        return notesDetectorRoutineSteps[notesDetectorRoutineStageIndex].Instruction ?? string.Empty;
    }

    private string GetNotesDetectorRoutineTargetText()
    {
        if (!showNotesDetectorRoutinePopup)
            return string.Empty;

        if (notesDetectorRoutineStageIndex >= notesDetectorRoutineSteps.Count)
            return "TARGET  COMPLETE";

        return notesDetectorRoutineSteps[notesDetectorRoutineStageIndex].TargetLabel ?? string.Empty;
    }

    private string GetNotesDetectorRoutineStatusText()
    {
        if (!showNotesDetectorRoutinePopup)
            return string.Empty;

        if (notesDetectorRoutineStageIndex >= notesDetectorRoutineSteps.Count)
            return "COMPLETE";

        bool matched = notesDetectorRoutineMatchedSinceTime >= 0f;
        return matched ? "OK" : "NOT DETECTED";
    }

    private string GetNotesDetectorRoutineProgressText()
    {
        if (!showNotesDetectorRoutinePopup)
            return string.Empty;

        int totalSteps = notesDetectorRoutineSteps.Count;
        int displayStep = Mathf.Clamp(notesDetectorRoutineStageIndex + 1, 1, totalSteps);
        if (notesDetectorRoutineStageIndex >= notesDetectorRoutineSteps.Count)
            return $"Complete  {totalSteps}/{totalSteps}";

        return $"Step {displayStep}/{totalSteps}";
    }

    private List<string> GetNotesDetectorRoutineTabRows()
    {
        if (!showNotesDetectorRoutinePopup)
            return new List<string>();

        string[] frets;
        if (notesDetectorRoutineStageIndex >= notesDetectorRoutineSteps.Count)
            frets = CreateNotesDetectorRoutineTabFrets("-", "-", "-", "-", "-", "-");
        else
            frets = notesDetectorRoutineSteps[notesDetectorRoutineStageIndex].TabFretsTopDown ?? CreateNotesDetectorRoutineTabFrets("-", "-", "-", "-", "-", "-");

        string[] stringLabels = { "e", "B", "G", "D", "A", "E" };
        List<string> rows = new List<string>(6);
        for (int i = 0; i < 6; i++)
        {
            string fret = i < frets.Length ? frets[i] : "-";
            fret = string.IsNullOrWhiteSpace(fret) ? "-" : fret;
            string center = fret.Length == 1 ? $"---{fret}---" : $"--{fret}---";
            rows.Add($"{stringLabels[i]}|{center}|");
        }

        return rows;
    }

    private void HandleToneLabControls()
    {
        if (unityToneLabOverlay != null && unityToneLabOverlay.IsCapturingKeyboardInput)
            return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.T))
        {
            CloseToneLabFromUi();
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

        if (!songSelectionSongConfirmed)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
                MoveSongSelection(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow))
                MoveSongSelection(1);

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.LeftArrow))
                songSelectionSongConfirmed = true;

            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            songSelectionSongConfirmed = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
            MoveTrackSelectionInMenu(-1);
        else if (Input.GetKeyDown(KeyCode.DownArrow))
            MoveTrackSelectionInMenu(1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            ConfirmTrackSelection();
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
        if (showGeneratedAudioTrackSelectionPopup)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            {
                CloseGeneratedAudioTrackSelectionFromUi();
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveGeneratedAudioTrackSelectionFromUi(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveGeneratedAudioTrackSelectionFromUi(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ActivateSelectedGeneratedAudioTrackSelectionFromUi();
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            showSongSettings = false;
            showGeneratedAudioTrackSelectionPopup = false;
            isPaused = true;
            SyncAudioToSongTimer(playImmediately: false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            MoveSongSettingsSelectionFromUi(-1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            MoveSongSettingsSelectionFromUi(1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ActivateSelectedSongSettingsItemFromUi();
            return;
        }

        int horizontalDirection = GetHeldHorizontalArrowDirection();
        if (selectedSongSettingsIndex >= 1 && selectedSongSettingsIndex <= 4 && IsSongSettingsOptionSelectable(selectedSongSettingsIndex))
        {
            if (ConsumeHeldHorizontalUiStep("song-settings-slider", horizontalDirection))
            {
                AdjustSelectedSongSettingFromUi(horizontalDirection);
                return;
            }
        }
        else
        {
            ConsumeHeldHorizontalUiStep("song-settings-slider", 0);
            if ((selectedSongSettingsIndex == 5 || selectedSongSettingsIndex == 6) && IsSongSettingsOptionSelectable(selectedSongSettingsIndex) && Input.GetKeyDown(KeyCode.LeftArrow))
            {
                AdjustSelectedSongSettingFromUi(-1);
                return;
            }

            if ((selectedSongSettingsIndex == 5 || selectedSongSettingsIndex == 6) && IsSongSettingsOptionSelectable(selectedSongSettingsIndex) && Input.GetKeyDown(KeyCode.RightArrow))
            {
                AdjustSelectedSongSettingFromUi(1);
                return;
            }
        }
    }

    private void HandleOffsetHelperControls()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
        {
            if (offsetHelperAdjusting)
            {
                offsetHelperAdjusting = false;
                offsetHelperPreviewPlaying = false;
                SeekOffsetHelperTime(offsetHelperAnchorTime, syncAudio: true, playImmediately: false);
            }
            else
            {
                CloseOffsetHelperBackToSongSettings(saveChanges: false);
            }

            return;
        }

        if (!offsetHelperAdjusting)
        {
            int moveDelta = 0;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                moveDelta = -1;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                moveDelta = 1;
            }
            else
            {
                int heldDirection = GetHeldHorizontalArrowDirection();
                if (ConsumeHeldHorizontalUiStep("offset-helper-anchor", heldDirection))
                    moveDelta = heldDirection;
                else if (heldDirection == 0)
                    ConsumeHeldHorizontalUiStep("offset-helper-anchor", 0);
            }

            if (moveDelta != 0)
            {
                MoveOffsetHelperAnchor(moveDelta);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                StartOffsetHelperAdjustMode();
                return;
            }

            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            ToggleOffsetHelperPreviewPlayback();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ConfirmOffsetHelperFromUi();
            return;
        }

        int horizontalDirection = GetHeldHorizontalArrowDirection();
        if (ConsumeHeldHorizontalUiStep("offset-helper-adjust", horizontalDirection))
        {
            AdjustOffsetHelperWorkingOffset(horizontalDirection * 10f);
            return;
        }

        if (horizontalDirection == 0)
            ConsumeHeldHorizontalUiStep("offset-helper-adjust", 0);
    }

    private void HandleSongEndControls()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            MoveSongEndActionSelectionFromUi(-1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            MoveSongEndActionSelectionFromUi(1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ActivateSelectedSongEndActionFromUi();
            return;
        }
    }

    private void HandleGlobalSettingsControls()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.G))
        {
            if (!string.IsNullOrEmpty(activeGlobalSettingsCategory))
            {
                activeGlobalSettingsCategory = string.Empty;
                selectedGlobalSettingsItemIndex = 0;
            }
            else
            {
                CloseGlobalSettingsFromUi();
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (string.IsNullOrEmpty(activeGlobalSettingsCategory))
                selectedGlobalSettingsTopIndex = (selectedGlobalSettingsTopIndex + 6) % 7;
            else
                MoveGlobalSettingsItemSelection(-1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            if (string.IsNullOrEmpty(activeGlobalSettingsCategory))
                selectedGlobalSettingsTopIndex = (selectedGlobalSettingsTopIndex + 1) % 7;
            else
                MoveGlobalSettingsItemSelection(1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            AdjustCurrentGlobalSettingsValue(-1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            AdjustCurrentGlobalSettingsValue(1);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ActivateCurrentGlobalSettingsSelection();
            return;
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
        RestoreGeneratedPlaybackSelectionForCurrentTrack();
        ApplyGeneratedPlaybackSelection();
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
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGameModes = false;
        showHeroModeSettings = false;
        loopSettingsOpenedFromGameModes = false;
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
            songSelectionSongConfirmed = false;
            return;
        }

        int selectedIndex = availableSongs.FindIndex(song =>
            currentSongEntry != null &&
            string.Equals(song.SongDirectory, currentSongEntry.SongDirectory, StringComparison.OrdinalIgnoreCase));

        selectedSongListIndex = selectedIndex >= 0 ? selectedIndex : 0;
        SyncPendingTrackSelectionToSong(selectedSongListIndex, preserveTrackIfPossible: true);
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

    private void ClearSongSelectionCaches()
    {
        SongLibraryService.ClearCache();
        cachedSongMetadataByPath.Clear();
        cachedSongMetadataTicksByPath.Clear();
        cachedTrackSummariesByNotationPath.Clear();
        cachedTrackSummaryTicksByNotationPath.Clear();
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

    private void SyncPendingTrackSelectionToSong(int songIndex, bool preserveTrackIfPossible)
    {
        if (songIndex < 0 || songIndex >= availableSongs.Count)
        {
            pendingTrackSelectionSong = null;
            pendingTrackSelectionParts.Clear();
            selectedTrackListIndex = 0;
            trackListScrollOffset = 0;
            return;
        }

        SongLibraryEntry selected = availableSongs[songIndex];
        SongMetadata selectedMetadata = LoadSongMetadataForEntry(selected);
        bool samePendingSong = pendingTrackSelectionSong != null &&
            string.Equals(pendingTrackSelectionSong.SongDirectory, selected.SongDirectory, StringComparison.OrdinalIgnoreCase);
        string previousPartId = preserveTrackIfPossible &&
            samePendingSong &&
            selectedTrackListIndex >= 0 &&
            selectedTrackListIndex < pendingTrackSelectionParts.Count
            ? pendingTrackSelectionParts[selectedTrackListIndex].PartId
            : string.Empty;
        string savedPartId = selectedMetadata != null && !selectedMetadata.useAutoTrackSelection
            ? selectedMetadata.selectedMusicXmlPartId ?? string.Empty
            : string.Empty;

        pendingTrackSelectionSong = selected;
        pendingTrackSelectionParts.Clear();
        pendingTrackSelectionParts.AddRange(GetSortedTrackSummaries(selected));
        selectedTrackListIndex = 0;

        if (!string.IsNullOrEmpty(previousPartId))
        {
            int preservedIndex = pendingTrackSelectionParts.FindIndex(track =>
                string.Equals(track.PartId, previousPartId, StringComparison.OrdinalIgnoreCase));
            if (preservedIndex >= 0)
                selectedTrackListIndex = preservedIndex;
        }
        else if (!string.IsNullOrEmpty(savedPartId))
        {
            int savedIndex = pendingTrackSelectionParts.FindIndex(track =>
                string.Equals(track.PartId, savedPartId, StringComparison.OrdinalIgnoreCase));
            if (savedIndex >= 0)
                selectedTrackListIndex = savedIndex;
        }

        trackListScrollOffset = 0;
        EnsureTrackSelectionVisible();
    }

    private void CloseTrackSelection()
    {
        showTrackSelection = false;
        showMainMenu = false;
        showSongSelection = true;
        songSelectionSongConfirmed = false;
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
        songSelectionSongConfirmed = false;
        SyncPendingTrackSelectionToSong(selectedSongListIndex, preserveTrackIfPossible: true);
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
            songSelectionSongConfirmed = false;
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
        songSelectionSongConfirmed = false;
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
            string metadataPath = BuildSongMetadataPath(entry);
            string metadataFileName = ResolveSongMetadataFileName(entry);
            SongMetadata trackMetadata = LoadSongMetadata(metadataFileName, metadataPath);
            trackMetadata.useAutoTrackSelection = false;
            trackMetadata.selectedMusicXmlPartId = preferredPartId;
            SaveSongMetadata(trackMetadata, metadataPath, metadataFileName);
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
        if (!loopEnabled || loopEndTime <= loopStartTime + 0.01f)
            return;

        if (songTimer < loopStartTime - 0.0001f)
        {
            SeekSongTime(loopStartTime, false);
            return;
        }

        if (songTimer < loopEndTime)
            return;

        if (loopPauseDurationSeconds > 0.001f)
        {
            StartLoopRestartPause();
            return;
        }

        SeekSongTime(loopStartTime, false);
    }

    private void StartLoopRestartPause()
    {
        float pauseSeconds = Mathf.Max(0f, loopPauseDurationSeconds);
        SeekSongTimeForLoopRestartPause(loopStartTime);
        songTimer = loopStartTime;
        audioSongTimer = loopStartTime;
        loopRestartPauseRemainingSeconds = pauseSeconds;
        SyncAudioToSongTimer(playImmediately: false);
    }

    private void SnapSongTimeIntoLoopWindowIfNeeded()
    {
        if (!loopEnabled || loopEndTime <= loopStartTime + 0.01f)
            return;

        if (songTimer < loopStartTime - 0.0001f || songTimer > loopEndTime + 0.0001f)
            SeekSongTime(loopStartTime, false);
    }

    private void EnterLoopSettingsMode()
    {
        showLoopPausePopup = false;
        selectedLoopPausePopupIndex = 0;
        loopRestartPauseRemainingSeconds = 0f;
        loopSettingsPreviewPlaying = false;
        showLoopSettings = true;
        showSongSettings = false;
        showSongSelection = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        selectedLoopMarker = 3;
        loopSettingsReturnRenderMode = renderMode;

        if (renderMode == GuitarRenderMode.Highway3D)
            renderMode = GuitarRenderMode.Tabs;

        SyncAudioToSongTimer(playImmediately: false);
    }

    private void OpenLoopPausePopup()
    {
        loopSettingsPreviewPlaying = false;
        showLoopSettings = false;
        showLoopPausePopup = true;
        selectedLoopPausePopupIndex = 0;
        SyncAudioToSongTimer(playImmediately: false);
    }

    private void ExitLoopSettingsMode()
    {
        showLoopPausePopup = false;
        selectedLoopPausePopupIndex = 0;
        loopSettingsPreviewPlaying = false;
        showLoopSettings = false;
        SnapSongTimeIntoLoopWindowIfNeeded();

        if (renderMode != loopSettingsReturnRenderMode)
            renderMode = loopSettingsReturnRenderMode;

        if (loopSettingsOpenedFromGameModes && isPaused && !songHasEnded)
        {
            showGameModes = true;
            selectedGameModesIndex = GetFirstVisibleGameModesIndex();
        }

        loopSettingsOpenedFromGameModes = false;

        SyncAudioToSongTimer(playImmediately: false);
    }

    private void ToggleLoopSettingsPreviewPlayback()
    {
        loopSettingsPreviewPlaying = !loopSettingsPreviewPlaying;
        loopRestartPauseRemainingSeconds = 0f;
        if (loopSettingsPreviewPlaying)
            SnapSongTimeIntoLoopWindowIfNeeded();

        SyncAudioToSongTimer(playImmediately: loopSettingsPreviewPlaying);
    }


    public void ToggleLoopFromUi()
    {
        if (loopEnabled)
        {
            bool wasInLoopFlow = showLoopSettings || showLoopPausePopup;
            loopEnabled = false;
            loopRestartPauseRemainingSeconds = 0f;
            showLoopPausePopup = false;
            selectedLoopPausePopupIndex = 0;
            loopSettingsPreviewPlaying = false;
            ResetSessionScoreState(ignoreCurrentlyResolvedNotes: true);
            SaveSongMetadata();
            if (wasInLoopFlow)
                ExitLoopSettingsMode();
            return;
        }

        loopEnabled = true;
        ResetSessionScoreState(ignoreCurrentlyResolvedNotes: true);
        if (loopEndTime <= loopStartTime)
            loopEndTime = loopStartTime + 0.25f;
        loopSettingsOpenedFromGameModes = showGameModes;
        showGameModes = false;
        showHeroModeSettings = false;
        EnterLoopSettingsMode();
    }

    public void ToggleNoteByNoteModeFromUi()
    {
        noteByNoteModeEnabled = !noteByNoteModeEnabled;
        if (!noteByNoteModeEnabled)
        {
            ClearNoteByNoteWaitingState();
        }
        else if (!isPaused)
        {
            ApplyNoteByNoteTransportGate(songTimer, showLoopSettings && loopSettingsPreviewPlaying, showOffsetHelper && offsetHelperAdjusting && offsetHelperPreviewPlaying, loopRestartPauseRemainingSeconds > 0.0001f);
        }

        SyncAudioToSongTimer(playImmediately: ShouldPlaybackAudio(showLoopSettings && loopSettingsPreviewPlaying, showOffsetHelper && offsetHelperAdjusting && offsetHelperPreviewPlaying, loopRestartPauseRemainingSeconds > 0.0001f), forceSeek: noteByNoteWaitingForMatch);
    }

    private int GetCurrentHeroHeartsRemaining()
    {
        return Mathf.Clamp(heroModeHeartCount - sessionScoreMisses, 0, Mathf.Max(1, heroModeHeartCount));
    }

    private void SetSongEndState(bool ended, bool asGameOver = false)
    {
        songHasEnded = ended;
        songEndedAsGameOver = ended && asGameOver;
    }

    private void EnterSongEndState(bool asGameOver)
    {
        isPaused = true;
        selectedSongEndActionIndex = 0;
        showSongSettings = false;
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        showGameModes = false;
        showHeroModeSettings = false;
        loopSettingsOpenedFromGameModes = false;
        SetSongEndState(true, asGameOver);
        SyncAudioToSongTimer(playImmediately: false);
    }

    private bool TryTriggerHeroModeGameOver()
    {
        // In loop mode, hero hearts are a loop-local practice visual only.
        if (loopEnabled || !heroModeEnabled || songHasEnded || GetCurrentHeroHeartsRemaining() > 0)
            return false;

        EnterSongEndState(asGameOver: true);
        return true;
    }

    private void RestartCurrentSongForModeChange()
    {
        SetSongEndState(false);
        selectedSongEndActionIndex = 0;
        scoreSaveInvalidated = playbackSpeedPercent < 100f;
        ResetSessionScoreState();
        ClearNoteByNoteWaitingState();
        SeekSongTime(-songStartDelaySeconds, false);
        SyncAudioToSongTimer(playImmediately: false);
    }

    private int GetFirstVisibleGameModesIndex()
    {
        return 0;
    }

    private bool IsGameModesSelectionVisible(int index)
    {
        return index switch
        {
            0 => true,
            1 => loopEnabled,
            2 => true,
            3 => true,
            4 => heroModeEnabled,
            5 => true,
            _ => false
        };
    }

    public void OpenGameModesFromUi()
    {
        showGameModes = true;
        showHeroModeSettings = false;
        selectedHeroModeSettingsIndex = 0;
        selectedGameModesIndex = IsGameModesSelectionVisible(selectedGameModesIndex) ? selectedGameModesIndex : GetFirstVisibleGameModesIndex();
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void CloseGameModesFromUi()
    {
        showGameModes = false;
        showHeroModeSettings = false;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void SetGameModesSelectionFromUi(int index)
    {
        selectedGameModesIndex = Mathf.Clamp(index, 0, 5);
        if (!IsGameModesSelectionVisible(selectedGameModesIndex))
            selectedGameModesIndex = GetFirstVisibleGameModesIndex();
    }

    public void HoverGameModesSelectionFromUi(int index)
    {
        if (!showGameModes)
            return;

        SetGameModesSelectionFromUi(index);
    }

    public void MoveGameModesSelectionFromUi(int delta)
    {
        if (delta == 0)
            return;

        const int optionCount = 6;
        int nextIndex = selectedGameModesIndex;
        for (int attempt = 0; attempt < optionCount; attempt++)
        {
            nextIndex = (nextIndex + delta + optionCount) % optionCount;
            if (IsGameModesSelectionVisible(nextIndex))
            {
                selectedGameModesIndex = nextIndex;
                return;
            }
        }
    }

    public void ActivateSelectedGameModesActionFromUi()
    {
        switch (selectedGameModesIndex)
        {
            case 0:
                ToggleLoopFromUi();
                break;
            case 1:
                OpenLoopConfigurationFromUi();
                break;
            case 2:
                ToggleNoteByNoteModeFromUi();
                break;
            case 3:
                ToggleHeroModeFromUi();
                break;
            case 4:
                OpenHeroModeSettingsFromUi();
                break;
            case 5:
                CloseGameModesFromUi();
                break;
        }
    }

    public void OpenLoopConfigurationFromUi()
    {
        if (!loopEnabled)
            return;

        loopSettingsOpenedFromGameModes = true;
        showGameModes = false;
        showHeroModeSettings = false;
        EnterLoopSettingsMode();
    }

    public void ToggleHeroModeFromUi()
    {
        heroModeEnabled = !heroModeEnabled;
        SaveHeroModePreferences();
        showHeroModeSettings = false;
        selectedGameModesIndex = Mathf.Min(selectedGameModesIndex, heroModeEnabled ? 4 : 3);
        RestartCurrentSongForModeChange();
    }

    public void OpenHeroModeSettingsFromUi()
    {
        if (!heroModeEnabled)
            return;

        showGameModes = false;
        showHeroModeSettings = true;
        selectedHeroModeSettingsIndex = 0;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void CloseHeroModeSettingsFromUi()
    {
        showHeroModeSettings = false;
        selectedHeroModeSettingsIndex = 0;
        showGameModes = true;
        selectedGameModesIndex = 4;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void SetHeroModeSettingsSelectionFromUi(int index)
    {
        selectedHeroModeSettingsIndex = Mathf.Clamp(index, 0, 1);
    }

    public void HoverHeroModeSettingsSelectionFromUi(int index)
    {
        if (!showHeroModeSettings)
            return;

        SetHeroModeSettingsSelectionFromUi(index);
    }

    public void AdjustHeroModeHeartCountFromUi(int delta)
    {
        if (delta == 0)
            return;

        int newHeartCount = Mathf.Clamp(heroModeHeartCount + delta, 1, 30);
        if (newHeartCount == heroModeHeartCount)
            return;

        heroModeHeartCount = newHeartCount;
        SaveHeroModePreferences();
        RestartCurrentSongForModeChange();
    }

    public void SetPauseActionSelectionFromUi(int index)
    {
        selectedPauseActionIndex = Mathf.Clamp(index, 0, 10);
    }

    public void HoverPauseActionSelectionFromUi(int index)
    {
        if (!isPaused || showMainMenu || showSongSettings || showSongSelection || showTrackSelection || showGlobalSettings || showGameModes || showHeroModeSettings)
            return;

        SetPauseActionSelectionFromUi(index);
    }

    public void MovePauseActionSelectionFromUi(int delta)
    {
        const int optionCount = 11;
        selectedPauseActionIndex = (selectedPauseActionIndex + delta + optionCount) % optionCount;
    }

    public void AdjustPauseSpeedFromUi(int deltaPercent)
    {
        if (deltaPercent == 0)
            return;

        SetPlaybackSpeedPercentFromUi(playbackSpeedPercent + deltaPercent);
    }

    public void SetLoopPausePopupSelectionFromUi(int index)
    {
        selectedLoopPausePopupIndex = Mathf.Clamp(index, 0, 1);
    }

    public void HoverLoopPausePopupSelectionFromUi(int index)
    {
        if (!showLoopPausePopup)
            return;

        SetLoopPausePopupSelectionFromUi(index);
    }

    public void MoveLoopPausePopupSelectionFromUi(int delta)
    {
        const int optionCount = 2;
        selectedLoopPausePopupIndex = (selectedLoopPausePopupIndex + delta + optionCount) % optionCount;
    }

    public void SetLoopPauseDurationFromUi(float seconds)
    {
        loopPauseDurationSeconds = Mathf.Clamp(seconds, 0f, 8f);
        SaveSongMetadata();
    }

    public void AdjustLoopPauseDurationFromUi(float deltaSeconds)
    {
        if (Mathf.Approximately(deltaSeconds, 0f))
            return;

        SetLoopPauseDurationFromUi(loopPauseDurationSeconds + deltaSeconds);
    }

    public void ConfirmLoopPausePopupFromUi()
    {
        SaveSongMetadata();
        ExitLoopSettingsMode();
    }

    public void CloseLoopPausePopupBackToLoopSettingsFromUi()
    {
        showLoopPausePopup = false;
        selectedLoopPausePopupIndex = 0;
        showLoopSettings = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    private int GetHeldHorizontalArrowDirection()
    {
        bool leftHeld = Input.GetKey(KeyCode.LeftArrow);
        bool rightHeld = Input.GetKey(KeyCode.RightArrow);
        if (leftHeld == rightHeld)
            return 0;

        return leftHeld ? -1 : 1;
    }

    private bool ConsumeHeldHorizontalUiStep(string context, int direction)
    {
        return ConsumeHeldHorizontalUiStep(context, direction, 0.35f, 0.06f);
    }

    private bool ConsumeHeldHorizontalUiStep(string context, int direction, float initialDelaySeconds, float repeatDelaySeconds)
    {
        if (direction == 0)
        {
            if (string.Equals(heldUiHorizontalContext, context, StringComparison.Ordinal))
            {
                heldUiHorizontalContext = string.Empty;
                heldUiHorizontalDirection = 0;
                heldUiHorizontalNextRepeatTime = -1f;
            }

            return false;
        }

        float now = Time.unscaledTime;
        if (!string.Equals(heldUiHorizontalContext, context, StringComparison.Ordinal) ||
            heldUiHorizontalDirection != direction)
        {
            heldUiHorizontalContext = context;
            heldUiHorizontalDirection = direction;
            heldUiHorizontalNextRepeatTime = now + initialDelaySeconds;
            return true;
        }

        bool initialDown = (direction < 0 && Input.GetKeyDown(KeyCode.LeftArrow)) ||
                           (direction > 0 && Input.GetKeyDown(KeyCode.RightArrow));
        if (initialDown)
        {
            heldUiHorizontalNextRepeatTime = now + initialDelaySeconds;
            return true;
        }

        if (now >= heldUiHorizontalNextRepeatTime)
        {
            heldUiHorizontalNextRepeatTime = now + repeatDelaySeconds;
            return true;
        }

        return false;
    }

    private bool IsGeneratedSongSettingsMode()
    {
        return GetEffectiveSongPlaybackAudioMode() == SongPlaybackAudioMode.Generated;
    }

    private bool IsSongSettingsOptionSelectable(int index)
    {
        if (index < 0 || index > 8)
            return false;

        if (!IsGeneratedSongSettingsMode())
            return true;

        switch (index)
        {
            case 0:
            case 2:
            case 3:
            case 4:
            case 5:
            case 7:
            case 8:
                return true;
            default:
                return false;
        }
    }

    public void MoveSongSettingsSelectionFromUi(int delta)
    {
        const int optionCount = 9;
        if (delta == 0)
            return;

        int candidate = selectedSongSettingsIndex;
        for (int i = 0; i < optionCount; i++)
        {
            candidate = (candidate + delta + optionCount) % optionCount;
            if (IsSongSettingsOptionSelectable(candidate))
            {
                selectedSongSettingsIndex = candidate;
                return;
            }
        }
    }

    public void AdjustSelectedSongSettingFromUi(int delta)
    {
        if (delta == 0)
            return;

        switch (selectedSongSettingsIndex)
        {
            case 1:
                if (IsGeneratedSongSettingsMode())
                    break;
                SetAudioOffsetMsFromUi(audioOffsetMs + (delta * 10f));
                break;
            case 2:
                SetTabSpeedOffsetPercentFromUi(tabSpeedOffsetPercent + delta);
                break;
            case 3:
                SetSongStartDelaySecondsFromUi(songStartDelaySeconds + (delta * 0.05f));
                break;
            case 4:
                SetSongVolumePercentFromUi(songVolumePercent + (delta * 5f));
                break;
            case 5:
                if (delta < 0)
                    MoveTrackSelectionFromUi(-1);
                else if (delta > 0)
                    MoveTrackSelectionFromUi(1);
                break;
            case 6:
                if (IsGeneratedSongSettingsMode())
                    break;
                ToggleOffsetScopeFromUi();
                break;
        }
    }

    public void ActivateSelectedSongSettingsItemFromUi()
    {
        switch (selectedSongSettingsIndex)
        {
            case 0:
                if (IsGeneratedSongSettingsMode())
                    OpenGeneratedAudioTrackSelectionFromUi();
                else
                    OpenOffsetHelperFromUi();
                break;
            case 5:
                MoveTrackSelectionFromUi(1);
                break;
            case 6:
                if (!IsGeneratedSongSettingsMode())
                    ToggleOffsetScopeFromUi();
                break;
            case 7:
                CloseSongSettingsFromUi();
                break;
            case 8:
                ResumePlaybackFromUi();
                break;
        }
    }

    public void EndSongFromUi()
    {
        isPaused = true;
        selectedSongEndActionIndex = 0;
        showSongSettings = false;
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        showGameModes = false;
        showHeroModeSettings = false;
        loopSettingsOpenedFromGameModes = false;
        float duration = GetSongDurationSeconds();
        if (duration > 0.001f)
        {
            SeekSongTime(duration, false);
            songTimer = duration;
            audioSongTimer = duration;
        }
        EnterSongEndState(asGameOver: false);
    }

    public void SetSongEndActionSelectionFromUi(int index)
    {
        selectedSongEndActionIndex = Mathf.Clamp(index, 0, 2);
    }

    public void HoverSongEndActionSelectionFromUi(int index)
    {
        if (!songHasEnded || showMainMenu || showSongSettings || showSongSelection || showTrackSelection || showGlobalSettings)
            return;

        SetSongEndActionSelectionFromUi(index);
    }

    public void MoveSongEndActionSelectionFromUi(int delta)
    {
        const int optionCount = 3;
        selectedSongEndActionIndex = (selectedSongEndActionIndex + delta + optionCount) % optionCount;
    }

    public void ActivateSelectedSongEndActionFromUi()
    {
        switch (selectedSongEndActionIndex)
        {
            case 0:
                RetrySongFromUi();
                break;
            case 1:
                OpenSongSelectionFromSongEndFromUi();
                break;
            case 2:
                OpenMainMenuFromSongEndFromUi();
                break;
        }
    }

    public void ActivateSelectedPauseActionFromUi()
    {
        switch (selectedPauseActionIndex)
        {
            case 0:
                break;
            case 1:
                OpenGameModesFromUi();
                break;
            case 2:
                OpenSongSettingsFromUi();
                break;
            case 3:
                CycleSongPlaybackAudioModeFromUi(1);
                break;
            case 4:
                OpenLibraryFromPauseFromUi();
                break;
            case 5:
                OpenGlobalSettingsFromUi();
                break;
            case 6:
                OpenToneLabFromUi();
                break;
            case 7:
                OpenMainMenuFromUi();
                break;
            case 8:
                ResumePlaybackFromUi();
                break;
            case 9:
                EndSongFromUi();
                break;
            case 10:
                RetrySongFromUi();
                break;
        }
    }

    public void OpenMainMenuFromUi()
    {
        showToneLab = false;
        HideToneLabUi();
        ResetTransientMenuNavigationState();
        showNotesDetectorTestMenu = false;
        showMainMenu = true;
        mainMenuFlowActive = true;
        selectedMainMenuIndex = 0;
        showSongSettings = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        isPaused = true;
        SetSongEndState(false);
        songSelectionOpenedFromSongEnd = false;
        songSelectionOpenedFromMainMenu = false;
        showStartupTuningReminder = false;
        resumeGameplayAfterStartupTuningReminder = false;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void ContinueFromMainMenuFromUi()
    {
        showToneLab = false;
        HideToneLabUi();
        SetSongEndState(false);
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSettings = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        showNotesDetectorTestMenu = false;
        showGameModes = false;
        showHeroModeSettings = false;
        loopSettingsOpenedFromGameModes = false;
        ShowStartupTuningReminder(resumePlaybackAfterDismiss: true);
    }

    public void SetMainMenuSelectionFromUi(int index)
    {
        selectedMainMenuIndex = Mathf.Clamp(index, 0, MainMenuOptionCount - 1);
    }

    public void HoverMainMenuSelectionFromUi(int index)
    {
        if (Time.unscaledTime - lastMainMenuKeyboardInputTime < MainMenuHoverLockSeconds)
            return;

        SetMainMenuSelectionFromUi(index);
    }

    public void MoveMainMenuSelectionFromUi(int delta)
    {
        if (MainMenuOptionCount <= 0)
            return;

        if (delta == 0)
            return;

        int normalized = selectedMainMenuIndex;
        if (normalized < 0 || normalized >= MainMenuOptionCount)
            normalized = 0;

        normalized = (normalized + delta) % MainMenuOptionCount;
        if (normalized < 0)
            normalized += MainMenuOptionCount;

        lastMainMenuKeyboardInputTime = Time.unscaledTime;
        selectedMainMenuIndex = normalized;
    }

    public void ActivateSelectedMainMenuFromUi()
    {
        switch (Mathf.Clamp(selectedMainMenuIndex, 0, MainMenuOptionCount - 1))
        {
            case 0:
                ContinueFromMainMenuFromUi();
                break;
            case 1:
                OpenSongSelectionFromUi();
                break;
            case 2:
                OpenGlobalSettingsFromUi();
                break;
            case 3:
                OpenNotesDetectorTestFromUi();
                break;
            case 4:
                OpenToneLabFromUi();
                break;
            case 5:
                ExitGameFromUi();
                break;
        }
    }

    public void OpenNotesDetectorTestFromUi()
    {
        showToneLab = false;
        HideToneLabUi();
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSettings = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        showGameModes = false;
        showHeroModeSettings = false;
        showNotesDetectorTestMenu = true;
        showNotesDetectorRoutinePopup = false;
        selectedNotesDetectorTestIndex = 0;
        isPaused = true;
        if (notesDetectorBackendMode != NotesDetectorBackendMode.NativeEmbeddedBridge)
            SwitchNotesDetectorBackend(NotesDetectorBackendMode.NativeEmbeddedBridge);
        else
            StartConfiguredNotesDetectorBackend();
        RefreshNativeNotesDetectorUiState();
        RefreshDetectorBackendStatus();
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void CloseNotesDetectorTestFromUi()
    {
        showNotesDetectorRoutinePopup = false;
        nativeNotesDetectorBridge?.RestoreSelectedPresetWorkingSettings();
        RefreshNativeNotesDetectorUiState();
        showNotesDetectorTestMenu = false;
        showMainMenu = true;
        mainMenuFlowActive = true;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void SetNotesDetectorTestSelectionFromUi(int index)
    {
        selectedNotesDetectorTestIndex = Mathf.Clamp(index, 0, NotesDetectorTestOptionCount - 1);
    }

    public void HoverNotesDetectorTestSelectionFromUi(int index)
    {
        SetNotesDetectorTestSelectionFromUi(index);
    }

    public void MoveNotesDetectorTestSelectionFromUi(int delta)
    {
        if (delta == 0)
            return;

        int normalized = selectedNotesDetectorTestIndex;
        if (normalized < 0 || normalized >= NotesDetectorTestOptionCount)
            normalized = 0;

        normalized = (normalized + delta) % NotesDetectorTestOptionCount;
        if (normalized < 0)
            normalized += NotesDetectorTestOptionCount;

        selectedNotesDetectorTestIndex = normalized;
    }

    public void ActivateSelectedNotesDetectorTestActionFromUi()
    {
        switch (Mathf.Clamp(selectedNotesDetectorTestIndex, 0, NotesDetectorTestOptionCount - 1))
        {
            case 0:
                if (notesDetectorBackendMode == NotesDetectorBackendMode.NativeEmbeddedBridge)
                {
                    ShutdownNativeNotesDetectorIfRunning();
                    EnsureNativeNotesDetectorBridge();
                    nativeNotesDetectorBridge?.Start(selectedNativeNotesDetectorInputDeviceIndex);
                    RefreshNativeNotesDetectorUiState();
                }
                else
                {
                    ShutdownNotesDetectorIfRunning();
                    TryLaunchNotesDetector(forceLaunch: true);
                }
                MarkDetectorHintDirty();
                break;
            case 1:
                RunNotesDetectorRoutineFromUi();
                break;
            case 2:
                CloseNotesDetectorTestFromUi();
                break;
        }
    }

    public void RunNotesDetectorRoutineFromUi()
    {
        if (!showNotesDetectorTestMenu)
            return;

        showNotesDetectorRoutinePopup = true;
        notesDetectorRoutineStageIndex = 0;
        notesDetectorRoutineMatchedSinceTime = -1f;
        notesDetectorRoutineOpenedTime = Time.unscaledTime;
    }

    public void CloseNotesDetectorRoutineFromUi()
    {
        showNotesDetectorRoutinePopup = false;
        notesDetectorRoutineStageIndex = 0;
        notesDetectorRoutineMatchedSinceTime = -1f;
        notesDetectorRoutineOpenedTime = 0f;
    }

    public void OpenSongSelectionFromUi()
    {
        songSelectionOpenedFromSongEnd = false;
        songSelectionOpenedFromMainMenu = showMainMenu || mainMenuFlowActive;
        showNotesDetectorTestMenu = false;
        if (!showMainMenu)
            mainMenuFlowActive = false;
        OpenSongSelectionMenu();
    }

    public void OpenLibraryFromPauseFromUi()
    {
        ResetTransientMenuNavigationState();
        OpenMainMenuFromUi();
        OpenSongSelectionFromUi();
    }

    private void ResetTransientMenuNavigationState()
    {
        selectedPauseActionIndex = 1;
        selectedGameModesIndex = 0;
        selectedHeroModeSettingsIndex = 0;
        selectedSongEndActionIndex = 0;
        selectedSongSettingsIndex = 0;
        selectedLoopPausePopupIndex = 0;
        loopRestartPauseRemainingSeconds = 0f;
        loopSettingsPreviewPlaying = false;
        if (showLoopSettings && renderMode != loopSettingsReturnRenderMode)
            renderMode = loopSettingsReturnRenderMode;
        showLoopSettings = false;
        showLoopPausePopup = false;
        showGameModes = false;
        showHeroModeSettings = false;
        showNotesDetectorTestMenu = false;
        showNotesDetectorRoutinePopup = false;
        loopSettingsOpenedFromGameModes = false;
        selectedNotesDetectorTestIndex = 0;
        selectedGlobalSettingsTopIndex = 0;
        selectedGlobalSettingsItemIndex = 0;
        activeGlobalSettingsCategory = string.Empty;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        selectedTrackListIndex = 0;
        trackListScrollOffset = 0;
        pendingTrackSelectionSong = null;
        pendingTrackSelectionParts.Clear();
        showStartupTuningReminder = false;
        resumeGameplayAfterStartupTuningReminder = false;
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
        ResetSongEndSessionForMenuExit();
        OpenLibraryFromPauseFromUi();
    }

    public void OpenMainMenuFromSongEndFromUi()
    {
        ResetSongEndSessionForMenuExit();
        OpenMainMenuFromUi();
    }

    private void ResetSongEndSessionForMenuExit()
    {
        showToneLab = false;
        HideToneLabUi();
        SetSongEndState(false);
        selectedSongEndActionIndex = 0;
        ResetSessionScoreState();
        showSongSettings = false;
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        showGameModes = false;
        showHeroModeSettings = false;
        loopSettingsOpenedFromGameModes = false;
        showStartupTuningReminder = false;
        resumeGameplayAfterStartupTuningReminder = false;
        songSelectionOpenedFromSongEnd = false;
        songSelectionOpenedFromMainMenu = false;
        isPaused = true;
        SeekSongTime(-songStartDelaySeconds, false);
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void RetrySongFromUi()
    {
        showToneLab = false;
        HideToneLabUi();
        SetSongEndState(false);
        selectedSongEndActionIndex = 0;
        songSelectionOpenedFromSongEnd = false;
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showSongSettings = false;
        showGlobalSettings = false;
        showGameModes = false;
        showHeroModeSettings = false;
        loopSettingsOpenedFromGameModes = false;
        isPaused = false;
        scoreSaveInvalidated = playbackSpeedPercent < 100f;
        ResetSessionScoreState();
        SeekSongTime(-songStartDelaySeconds, false);
        SyncAudioToSongTimer(playImmediately: true);
    }

    public void OpenSongSettingsFromUi()
    {
        showToneLab = false;
        HideToneLabUi();
        showNotesDetectorTestMenu = false;
        showSongSettings = true;
        showGeneratedAudioTrackSelectionPopup = false;
        selectedSongSettingsIndex = 0;
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        showGameModes = false;
        showHeroModeSettings = false;
        loopSettingsOpenedFromGameModes = false;
        isPaused = true;
    }
    public void OpenGlobalSettingsFromUi()
    {
        showToneLab = false;
        HideToneLabUi();
        showNotesDetectorTestMenu = false;
        if (!showMainMenu)
            mainMenuFlowActive = false;

        showGlobalSettings = true;
        selectedGlobalSettingsTopIndex = 0;
        selectedGlobalSettingsItemIndex = 0;
        activeGlobalSettingsCategory = string.Empty;
        showSongSettings = false;
        showMainMenu = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGameModes = false;
        showHeroModeSettings = false;
        loopSettingsOpenedFromGameModes = false;
        isPaused = true;
    }

    public void OpenPrimarySongSettingsToolFromUi()
    {
        if (IsGeneratedSongSettingsMode())
            OpenGeneratedAudioTrackSelectionFromUi();
        else
            OpenOffsetHelperFromUi();
    }

    public void OpenToneLabFromUi()
    {
        EnsureToneLabRuntimeComponent();
        EnsureToneLabOverlayComponent();
        toneLabReturnContext = (showMainMenu || mainMenuFlowActive) ? ToneLabReturnContext.MainMenu : ToneLabReturnContext.Pause;
        showToneLab = true;
        showNotesDetectorTestMenu = false;
        showSongSettings = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        showGameModes = false;
        showHeroModeSettings = false;
        showLoopSettings = false;
        showLoopPausePopup = false;
        showOffsetHelper = false;
        showStartupTuningReminder = false;
        resumeGameplayAfterStartupTuningReminder = false;
        showMainMenu = false;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
        unityToneLabRuntime?.OpenForSession();
        unityToneLabOverlay?.SetVisible(true);
        unityToneLabOverlay?.RefreshUi(syncControls: true, refreshDevices: true);
    }

    public void CloseToneLabFromUi()
    {
        showToneLab = false;
        HideToneLabUi();

        if (toneLabReturnContext == ToneLabReturnContext.MainMenu)
        {
            showMainMenu = true;
            mainMenuFlowActive = true;
            isPaused = true;
        }
        else
        {
            showMainMenu = false;
            mainMenuFlowActive = false;
            isPaused = true;
        }

        SyncAudioToSongTimer(playImmediately: false);
    }

    private void HideToneLabUi()
    {
        unityToneLabRuntime?.RestoreSelectedPresetWorkingRig();
        unityToneLabOverlay?.SetVisible(false);
    }

    private void EnsureToneLabComponents()
    {
        EnsureToneLabRuntimeComponent();
        EnsureToneLabOverlayComponent();
    }

    private void EnsureToneLabRuntimeComponent()
    {
        if (unityToneLabRuntime != null)
            return;

        Transform existingRuntime = transform.Find("UnityToneLabRuntime");
        GameObject runtimeHost = existingRuntime != null ? existingRuntime.gameObject : new GameObject("UnityToneLabRuntime");
        runtimeHost.transform.SetParent(transform, false);
        unityToneLabRuntime = runtimeHost.GetComponent<UnityToneLabRuntime>();
        if (unityToneLabRuntime == null)
            unityToneLabRuntime = runtimeHost.AddComponent<UnityToneLabRuntime>();
    }

    private void EnsureToneLabOverlayComponent()
    {
        EnsureToneLabRuntimeComponent();

        if (unityToneLabOverlay != null)
            return;

        Transform existingOverlay = transform.Find("UnityToneLabUI");
        GameObject overlayHost = existingOverlay != null ? existingOverlay.gameObject : new GameObject("UnityToneLabUI");
        overlayHost.transform.SetParent(transform, false);
        unityToneLabOverlay = overlayHost.GetComponent<UnityToneLabOverlay>();
        if (unityToneLabOverlay == null)
            unityToneLabOverlay = overlayHost.AddComponent<UnityToneLabOverlay>();
        unityToneLabOverlay.Initialize(this, unityToneLabRuntime);
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
        if (playbackSpeedPercent < 100f)
            scoreSaveInvalidated = true;
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

        ClearSongSelectionCaches();
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

        songSelectionSongConfirmed = false;
        EnsureSongSelectionVisible();
    }

    public void MoveSongSelectionFromUi(int delta)
    {
        MoveSongSelection(delta);
    }

    public void SelectSongByIndexFromUi(int songIndex)
    {
        selectedSongListIndex = Mathf.Clamp(songIndex, 0, Mathf.Max(0, availableSongs.Count - 1));
        SyncPendingTrackSelectionToSong(selectedSongListIndex, preserveTrackIfPossible: true);
        songSelectionSongConfirmed = true;
        EnsureSongSelectionVisible();
    }

    public void CloseSongSelectionFromUi()
    {
        showToneLab = false;
        HideToneLabUi();
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGameModes = false;
        showHeroModeSettings = false;
        loopSettingsOpenedFromGameModes = false;
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
        songSelectionSongConfirmed = true;
        EnsureTrackSelectionVisible();
    }

    public void StartSelectedSongFromUi()
    {
        songSelectionSongConfirmed = true;
        ConfirmTrackSelection();
    }

    public void BackToSongSelectionFromUi()
    {
        CloseTrackSelection();
    }

    public void CloseSongSettingsFromUi()
    {
        showToneLab = false;
        HideToneLabUi();
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSettings = false;
        showGeneratedAudioTrackSelectionPopup = false;
        showGameModes = false;
        showHeroModeSettings = false;
        loopSettingsOpenedFromGameModes = false;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void CloseGlobalSettingsFromUi()
    {
        showToneLab = false;
        HideToneLabUi();
        showGlobalSettings = false;
        activeGlobalSettingsCategory = string.Empty;
        selectedGlobalSettingsItemIndex = 0;
        showGameModes = false;
        showHeroModeSettings = false;
        loopSettingsOpenedFromGameModes = false;
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
        SyncAudioToSongTimer(playImmediately: !isPaused || (showLoopSettings && loopSettingsPreviewPlaying));
    }

    public void SetTabSpeedOffsetPercentFromUi(float percent)
    {
        tabSpeedOffsetPercent = Mathf.Clamp(percent, 50f, 150f);
        GenerateTabSections();
        ResetActiveRendererContent();
        SaveSongMetadata();
    }

    public void SetSongStartDelaySecondsFromUi(float seconds)
    {
        songStartDelaySeconds = Mathf.Clamp(seconds, 0f, 8f);
        SaveSongMetadata();
    }

    public void SetSongVolumePercentFromUi(float percent)
    {
        songVolumePercent = Mathf.Clamp(percent, 0f, 100f);
        SaveSongMetadata();
        ApplyPlaybackSpeedToAudio();
    }

    public void CycleSongPlaybackAudioModeFromUi(int delta)
    {
        if (delta == 0)
            return;

        SongPlaybackAudioMode[] cycle = { SongPlaybackAudioMode.Generated, SongPlaybackAudioMode.Mp3, SongPlaybackAudioMode.Muted };
        int currentIndex = Array.IndexOf(cycle, songPlaybackAudioMode);
        if (currentIndex < 0)
            currentIndex = 0;

        int nextIndex = (currentIndex + delta) % cycle.Length;
        if (nextIndex < 0)
            nextIndex += cycle.Length;

        SetSongPlaybackAudioModeFromUi(cycle[nextIndex]);
    }

    public void SetSongPlaybackAudioModeFromUi(SongPlaybackAudioMode mode)
    {
        if (songPlaybackAudioMode == mode)
            return;

        songPlaybackAudioMode = mode;
        SaveSongMetadata();
        SyncAudioToSongTimer(playImmediately: ShouldPlaybackAudio(showLoopSettings && loopSettingsPreviewPlaying, showOffsetHelper && offsetHelperAdjusting && offsetHelperPreviewPlaying, loopRestartPauseRemainingSeconds > 0.0001f), forceSeek: true);
    }

    public void MoveTrackSelectionFromUi(int delta)
    {
        MoveTrackSelection(delta);
    }

    private static List<GameplayNoteState> CloneNoteStates(List<GameplayNoteState> source)
    {
        if (source == null)
            return new List<GameplayNoteState>();

        List<GameplayNoteState> clone = new List<GameplayNoteState>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            GameplayNoteState state = source[i];
            if (state == null)
            {
                clone.Add(null);
                continue;
            }

            clone.Add(new GameplayNoteState(state.data)
            {
                result = state.result,
                resolvedAt = state.resolvedAt,
                isJudgeable = state.isJudgeable
            });
        }

        return clone;
    }

    private float GetFirstChartNoteTime()
    {
        if (noteStates == null || noteStates.Count == 0)
            return 0f;

        float first = float.MaxValue;
        for (int i = 0; i < noteStates.Count; i++)
        {
            GameplayNoteState state = noteStates[i];
            if (state == null)
                continue;

            first = Mathf.Min(first, state.data.time);
        }

        return first == float.MaxValue ? 0f : Mathf.Max(0f, first);
    }


    public void HoverSongSettingsSelectionFromUi(int index)
    {
        if (!showSongSettings)
            return;

        int clampedIndex = Mathf.Clamp(index, 0, 8);
        if (IsSongSettingsOptionSelectable(clampedIndex))
            selectedSongSettingsIndex = clampedIndex;
    }

    public void HoverGlobalSettingsTopSelectionFromUi(int index)
    {
        if (!showGlobalSettings || !string.IsNullOrEmpty(activeGlobalSettingsCategory))
            return;

        selectedGlobalSettingsTopIndex = Mathf.Clamp(index, 0, 6);
    }

    public void HoverGlobalSettingsItemSelectionFromUi(int index)
    {
        if (!showGlobalSettings || string.IsNullOrEmpty(activeGlobalSettingsCategory))
            return;

        List<RuntimeSettingSnapshot> settings = GetActiveGlobalSettingsItems();
        selectedGlobalSettingsItemIndex = Mathf.Clamp(index, 0, Mathf.Max(0, settings.Count - 1));
    }

    public void ActivateGlobalSettingsTopSelectionFromUi(int index)
    {
        if (!showGlobalSettings)
            return;

        selectedGlobalSettingsTopIndex = Mathf.Clamp(index, 0, 6);
        ActivateCurrentGlobalSettingsSelection();
    }

    public void AdjustGlobalSettingsTopValueFromUi(int index, int delta)
    {
        if (!showGlobalSettings)
            return;

        selectedGlobalSettingsTopIndex = Mathf.Clamp(index, 0, 6);
        AdjustCurrentGlobalSettingsValue(delta);
    }

    public void ActivateGlobalSettingsItemSelectionFromUi(int index)
    {
        if (!showGlobalSettings || string.IsNullOrEmpty(activeGlobalSettingsCategory))
            return;

        List<RuntimeSettingSnapshot> settings = GetActiveGlobalSettingsItems();
        selectedGlobalSettingsItemIndex = Mathf.Clamp(index, 0, Mathf.Max(0, settings.Count - 1));
        ActivateCurrentGlobalSettingsSelection();
    }

    public void AdjustGlobalSettingsItemValueFromUi(int index, int delta)
    {
        if (!showGlobalSettings || string.IsNullOrEmpty(activeGlobalSettingsCategory))
            return;

        List<RuntimeSettingSnapshot> settings = GetActiveGlobalSettingsItems();
        selectedGlobalSettingsItemIndex = Mathf.Clamp(index, 0, Mathf.Max(0, settings.Count - 1));
        AdjustCurrentGlobalSettingsValue(delta);
    }

    public void ResumePlaybackFromUi()
    {
        showToneLab = false;
        HideToneLabUi();
        SetSongEndState(false);
        if (showLoopSettings && renderMode != loopSettingsReturnRenderMode)
            renderMode = loopSettingsReturnRenderMode;
        showLoopSettings = false;
        showLoopPausePopup = false;
        selectedLoopPausePopupIndex = 0;
        loopRestartPauseRemainingSeconds = 0f;
        loopSettingsPreviewPlaying = false;
        SnapSongTimeIntoLoopWindowIfNeeded();
        isPaused = false;
        showSongSettings = false;
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        showGameModes = false;
        showHeroModeSettings = false;
        loopSettingsOpenedFromGameModes = false;
        SyncAudioToSongTimer(playImmediately: ShouldPlaybackAudio(false, false, false), forceSeek: noteByNoteWaitingForMatch);
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

    private float GetVisualTabSpacingScale()
    {
        return Mathf.Clamp(tabSpeedOffsetPercent / 100f, 0.5f, 1.5f);
    }

    private void SeekSongTime(float targetTime, bool updateSelectedMarker)
    {
        SeekSongTimeInternal(
            targetTime,
            updateSelectedMarker,
            clearLoopRestartPause: true,
            syncAudioAfterSeek: true,
            playImmediatelyAfterSeek: !isPaused || (showLoopSettings && loopSettingsPreviewPlaying));
    }

    private void SeekSongTimeFromUserNavigation(float targetTime, bool updateSelectedMarker)
    {
        float previousTime = songTimer;
        SeekSongTimeInternal(
            targetTime,
            updateSelectedMarker,
            clearLoopRestartPause: true,
            syncAudioAfterSeek: true,
            playImmediatelyAfterSeek: !isPaused || (showLoopSettings && loopSettingsPreviewPlaying));
        UpdateScoreSaveInvalidationForUserSeek(previousTime, songTimer);
    }

    private void SeekSongTimeForLoopRestartPause(float targetTime)
    {
        SeekSongTimeInternal(
            targetTime,
            updateSelectedMarker: false,
            clearLoopRestartPause: false,
            syncAudioAfterSeek: false,
            playImmediatelyAfterSeek: false);
    }

    private void SeekSongTimeInternal(float targetTime, bool updateSelectedMarker, bool clearLoopRestartPause, bool syncAudioAfterSeek, bool playImmediatelyAfterSeek)
    {
        float previousTime = songTimer;
        float clampedTime = Mathf.Max(-songStartDelaySeconds, targetTime);
        if (clearLoopRestartPause)
            loopRestartPauseRemainingSeconds = 0f;
        ClearNoteByNoteWaitingState();
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
        MarkDetectorHintDirty();
        if (syncAudioAfterSeek)
            SyncAudioToSongTimer(playImmediately: playImmediatelyAfterSeek);
        UpdateSongEndState();
    }

    private void UpdateScoreSaveInvalidationForUserSeek(float previousTime, float newTime)
    {
        if (Mathf.Abs(newTime - previousTime) <= 0.0001f)
            return;

        float resetThreshold = GetScoreSaveResetTimeThreshold();
        if (newTime <= resetThreshold + 0.0001f && playbackSpeedPercent >= 100f)
        {
            scoreSaveInvalidated = false;
            ResetSessionScoreState();
            return;
        }
    }

    private float GetScoreSaveResetTimeThreshold()
    {
        if (chartNotes == null || chartNotes.Count == 0)
            return 0f;

        return chartNotes.Min(note => note.time);
    }

    private void UpdateSelectedLoopMarker(float markerTime)
    {
        if (selectedLoopMarker == 1)
        {
            loopStartTime = Mathf.Max(0f, markerTime);
            if (loopEndTime < loopStartTime + 0.05f)
                loopEndTime = loopStartTime + 0.05f;
        }
        else if (selectedLoopMarker == 2)
        {
            loopEndTime = Mathf.Max(loopStartTime + 0.05f, markerTime);
        }
    }

    private void OnApplicationQuit()
    {
        isRunning = false;
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Join(500);
        if (udpClient != null) udpClient.Close();
        CloseDetectorHintClient();
        ShutdownNativeNotesDetectorIfRunning();
        ShutdownNotesDetectorIfRunning();
    }

    private void OnDestroy()
    {
        CloseDetectorHintClient();
        ShutdownNativeNotesDetectorIfRunning();
        ShutdownNotesDetectorIfRunning();
        ShutdownGeneratedSongPlayer();
    }

    private void OnDisable()
    {
        CloseDetectorHintClient();
        ShutdownNativeNotesDetectorIfRunning();
        ShutdownNotesDetectorIfRunning();
        ShutdownGeneratedSongPlayer();
    }

    private void TryLaunchNotesDetector(bool forceLaunch = false)
    {
        Debug.Log("[NotesDetector] TryLaunchNotesDetector() invoked.");

        if (notesDetectorBackendMode != NotesDetectorBackendMode.ExternalProcessUdp)
        {
            Debug.Log("[NotesDetector] Skipping external launch because native detector backend is selected.");
            return;
        }

        if (!forceLaunch && !autoLaunchNotesDetector)
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

    private void StartConfiguredNotesDetectorBackend()
    {
        if (notesDetectorBackendMode == NotesDetectorBackendMode.NativeEmbeddedBridge)
        {
            EnsureNativeNotesDetectorBridge();
            nativeNotesDetectorBridge?.Start(selectedNativeNotesDetectorInputDeviceIndex);
            RefreshNativeNotesDetectorUiState();
            MarkDetectorHintDirty();
            return;
        }

        if (autoLaunchNotesDetector)
            TryLaunchNotesDetector();
    }

    private void EnsureNativeNotesDetectorBridge()
    {
        if (nativeNotesDetectorBridge != null)
            return;

        nativeNotesDetectorBridge = new NativeNotesDetectorBridge();
    }

    private void RefreshNativeNotesDetectorDeviceList()
    {
        EnsureNativeNotesDetectorBridge();
        if (nativeNotesDetectorBridge == null)
            return;

        NativeDetectorDeviceListPayload payload = nativeNotesDetectorBridge.RefreshDevices();
        nativeNotesDetectorInputDevices.Clear();

        if (payload?.devices == null)
            return;

        for (int i = 0; i < payload.devices.Length; i++)
        {
            NativeDetectorInputDevice device = payload.devices[i];
            if (device != null)
                nativeNotesDetectorInputDevices.Add(device);
        }
    }

    private void RefreshNativeNotesDetectorUiState()
    {
        EnsureNativeNotesDetectorBridge();
        if (nativeNotesDetectorBridge == null)
            return;

        RefreshNativeNotesDetectorDeviceList();
        nativeNotesDetectorBridge.RefreshNativeStatus();
        nativeNotesDetectorRuntimeInfo = nativeNotesDetectorBridge.RuntimeInfo ?? new NativeDetectorRuntimeInfo();
    }

    private string GetSelectedNativeNotesDetectorInputDeviceLabel()
    {
        if (selectedNativeNotesDetectorInputDeviceIndex < 0)
            return "Automatic";

        for (int i = 0; i < nativeNotesDetectorInputDevices.Count; i++)
        {
            NativeDetectorInputDevice device = nativeNotesDetectorInputDevices[i];
            if (device != null && device.index == selectedNativeNotesDetectorInputDeviceIndex)
                return string.IsNullOrWhiteSpace(device.displayName) ? device.name : device.displayName;
        }

        return $"Device {selectedNativeNotesDetectorInputDeviceIndex}";
    }

    private int GetSelectedNativeDetectorInputDeviceUiIndex()
    {
        if (selectedNativeNotesDetectorInputDeviceIndex < 0)
            return 0;

        for (int i = 0; i < nativeNotesDetectorInputDevices.Count; i++)
        {
            NativeDetectorInputDevice device = nativeNotesDetectorInputDevices[i];
            if (device != null && device.index == selectedNativeNotesDetectorInputDeviceIndex)
                return i + 1;
        }

        return 0;
    }

    private List<string> BuildNotesDetectorInputDeviceSnapshot()
    {
        List<string> result = new List<string>
        {
            "Automatic"
        };

        for (int i = 0; i < nativeNotesDetectorInputDevices.Count; i++)
        {
            NativeDetectorInputDevice device = nativeNotesDetectorInputDevices[i];
            if (device == null)
                continue;

            string displayName = string.IsNullOrWhiteSpace(device.displayName) ? device.name : device.displayName;
            result.Add(string.IsNullOrWhiteSpace(displayName) ? $"Device {device.index}" : displayName);
        }

        return result;
    }

    private List<string> BuildNotesDetectorPresetLabelsSnapshot()
    {
        if (nativeNotesDetectorBridge != null)
            return nativeNotesDetectorBridge.BuildPresetLabels();

        return NativeDetectorSettingCatalog.BuildPresetLabels();
    }

    private int GetSelectedNativeDetectorPresetUiIndex()
    {
        if (nativeNotesDetectorBridge != null)
            return Mathf.Clamp(nativeNotesDetectorBridge.SelectedPresetIndex, 0, NativeDetectorSettingCatalog.PresetDescriptors.Count - 1);

        return NativeDetectorSettingCatalog.GetPresetIndex(NativeDetectorSettingCatalog.DefaultPresetId);
    }

    private string GetSelectedNativeDetectorPresetLabel()
    {
        if (nativeNotesDetectorBridge != null)
            return nativeNotesDetectorBridge.SelectedPresetLabel;

        return NativeDetectorSettingCatalog.GetPresetLabel(NativeDetectorSettingCatalog.DefaultPresetId);
    }

    private List<NativeDetectorSettingSnapshot> BuildNativeDetectorSettingsSnapshot()
    {
        if (nativeNotesDetectorBridge != null)
            return nativeNotesDetectorBridge.BuildSettingSnapshots();

        return NativeDetectorSettingCatalog.BuildSettingSnapshots(NativeDetectorSettingCatalog.CreatePresetById(NativeDetectorSettingCatalog.DefaultPresetId));
    }

    public void RefreshNativeNotesDetectorDevicesFromUi()
    {
        RefreshNativeNotesDetectorUiState();
    }

    public void SetNativeNotesDetectorInputDeviceFromUi(int uiIndex)
    {
        int resolvedDeviceIndex = -1;
        if (uiIndex > 0)
        {
            int deviceListIndex = uiIndex - 1;
            if (deviceListIndex >= 0 && deviceListIndex < nativeNotesDetectorInputDevices.Count)
            {
                NativeDetectorInputDevice device = nativeNotesDetectorInputDevices[deviceListIndex];
                if (device != null)
                    resolvedDeviceIndex = device.index;
            }
        }

        if (selectedNativeNotesDetectorInputDeviceIndex == resolvedDeviceIndex)
            return;

        selectedNativeNotesDetectorInputDeviceIndex = resolvedDeviceIndex;
        SaveNativeDetectorPreferences();

        if (notesDetectorBackendMode == NotesDetectorBackendMode.NativeEmbeddedBridge)
        {
            EnsureNativeNotesDetectorBridge();
            nativeNotesDetectorBridge?.Start(selectedNativeNotesDetectorInputDeviceIndex);
        }

        RefreshNativeNotesDetectorUiState();
        MarkDetectorHintDirty();
    }

    public void SetNativeNotesDetectorPresetFromUi(int presetUiIndex)
    {
        EnsureNativeNotesDetectorBridge();
        if (nativeNotesDetectorBridge == null)
            return;

        nativeNotesDetectorBridge.SelectPresetByIndex(presetUiIndex);
        RefreshNativeNotesDetectorUiState();
        MarkDetectorHintDirty();
    }

    public void SetNativeNotesDetectorSettingFromUi(string key, float value)
    {
        EnsureNativeNotesDetectorBridge();
        if (nativeNotesDetectorBridge == null || string.IsNullOrWhiteSpace(key))
            return;

        nativeNotesDetectorBridge.UpdateWorkingSetting(key, value);
        RefreshDetectorBackendStatus();
        MarkDetectorHintDirty();
    }

    public void SaveNativeNotesDetectorCustomPresetFromUi()
    {
        EnsureNativeNotesDetectorBridge();
        if (nativeNotesDetectorBridge == null)
            return;

        nativeNotesDetectorBridge.SaveWorkingSettingsToCustomPreset();
        RefreshNativeNotesDetectorUiState();
        MarkDetectorHintDirty();
    }

    private void ShutdownNativeNotesDetectorIfRunning()
    {
        if (nativeNotesDetectorBridge == null)
            return;

        nativeNotesDetectorBridge.Stop();
        nativeNotesDetectorBridge.Shutdown();
    }

    private void RefreshDetectorBackendStatus()
    {
        if (notesDetectorBackendMode == NotesDetectorBackendMode.NativeEmbeddedBridge)
        {
            nativeNotesDetectorBridge?.RefreshNativeStatus();
            nativeNotesDetectorRuntimeInfo = nativeNotesDetectorBridge != null
                ? (nativeNotesDetectorBridge.RuntimeInfo ?? new NativeDetectorRuntimeInfo())
                : new NativeDetectorRuntimeInfo();
        }
    }

    private string GetNotesDetectorBackendLabel()
    {
        return notesDetectorBackendMode == NotesDetectorBackendMode.NativeEmbeddedBridge
            ? "Native C++ Detector"
            : "Legacy UDP Process";
    }

    private string GetNotesDetectorStatusText()
    {
        if (notesDetectorBackendMode == NotesDetectorBackendMode.NativeEmbeddedBridge)
            return nativeNotesDetectorBridge != null ? nativeNotesDetectorBridge.LastStatus : "Native detector not initialized.";

        if (IsNoteDetectorConnected())
            return "External detector connected.";

        if (notesDetectorProcess != null && !notesDetectorProcess.HasExited)
            return "External detector running. Waiting for packets...";

        return "External detector offline.";
    }

    private string GetNotesDetectorDetailText()
    {
        if (notesDetectorBackendMode == NotesDetectorBackendMode.NativeEmbeddedBridge)
        {
            string error = nativeNotesDetectorBridge != null ? nativeNotesDetectorBridge.LastError : string.Empty;
            string selectedDevice = !string.IsNullOrWhiteSpace(nativeNotesDetectorRuntimeInfo.selectedInputDeviceDisplayName)
                ? nativeNotesDetectorRuntimeInfo.selectedInputDeviceDisplayName
                : GetSelectedNativeNotesDetectorInputDeviceLabel();
            string hostApi = !string.IsNullOrWhiteSpace(nativeNotesDetectorRuntimeInfo.selectedHostApiName)
                ? nativeNotesDetectorRuntimeInfo.selectedHostApiName
                : "--";
            string presetLabel = GetSelectedNativeDetectorPresetLabel();
            string detail = $"Preset  {presetLabel}  \u2022  Input  {selectedDevice}  \u2022  Host  {hostApi}  \u2022  {nativeNotesDetectorRuntimeInfo.sampleRate} Hz  \u2022  Hop {nativeNotesDetectorRuntimeInfo.hopSize}  \u2022  Level {(nativeNotesDetectorRuntimeInfo.inputLevelNormalized * 100f):F0}%";
            if (!string.IsNullOrWhiteSpace(error))
                detail = $"{detail}\n{error}";
            return detail;
        }

        string detectorPath = Path.Combine(Application.streamingAssetsPath, notesDetectorRelativePath);
        return $"UDP {detectorHintIp}:{detectorHintPort} hints  •  Rx {udpPort}  •  {Path.GetFileName(detectorPath)}";
    }

    private void SwitchNotesDetectorBackend(NotesDetectorBackendMode backend)
    {
        if (notesDetectorBackendMode == backend)
        {
            if (backend == NotesDetectorBackendMode.NativeEmbeddedBridge)
            {
                EnsureNativeNotesDetectorBridge();
                nativeNotesDetectorBridge?.Start(selectedNativeNotesDetectorInputDeviceIndex);
                RefreshNativeNotesDetectorUiState();
            }
            else
                TryLaunchNotesDetector(forceLaunch: true);

            MarkDetectorHintDirty();
            return;
        }

        if (backend == NotesDetectorBackendMode.NativeEmbeddedBridge)
        {
            ShutdownNotesDetectorIfRunning();
            CloseDetectorHintClient();
            logNotes = string.Empty;
            EnsureNativeNotesDetectorBridge();
            notesDetectorBackendMode = NotesDetectorBackendMode.NativeEmbeddedBridge;
            nativeNotesDetectorBridge?.Start(selectedNativeNotesDetectorInputDeviceIndex);
            RefreshNativeNotesDetectorUiState();
        }
        else
        {
            ShutdownNativeNotesDetectorIfRunning();
            logNotes = string.Empty;
            notesDetectorBackendMode = NotesDetectorBackendMode.ExternalProcessUdp;
            TryLaunchNotesDetector(forceLaunch: true);
        }

        Interlocked.Exchange(ref lastUdpPacketUtcTicks, 0L);
        MarkDetectorHintDirty();
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

        if (m.HasProperty("_Surface"))
            m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend"))
            m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_AlphaClip"))
            m.SetFloat("_AlphaClip", 0f);
        if (m.HasProperty("_SrcBlend"))
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend"))
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite"))
            m.SetInt("_ZWrite", 0);
        if (m.HasProperty("_Cull"))
            m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        m.EnableKeyword("_ALPHABLEND_ON");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.DisableKeyword("_ALPHATEST_ON");

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

        float bestDistance = float.MaxValue;

        for (int i = recentNoteEvents.Count - 1; i >= 0; i--)
        {
            NoteEvent ev = recentNoteEvents[i];
            
            if (ev.time < windowStart) break;
            if (ev.time > windowEnd) continue;

            if (!TryGetAcceptedConsumeKey(note, ev.pitches, out int acceptedConsumeKey)) continue;
            if (ev.consumedKeys.Contains(acceptedConsumeKey)) continue; 

            float distance = Mathf.Abs(ev.time - note.data.time);
            if (distance >= bestDistance) continue;

            matchedEvent = ev;
            consumeKey = acceptedConsumeKey;
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

        float windowStart = note.data.time - eventMatchEarly - eventTimeSlack;
        float windowEnd = note.data.time + eventMatchLate + eventTimeSlack + 0.1f;

        if (songTimer >= windowStart && songTimer <= windowEnd)
        {
            if (ContainsAcceptedPitch(note, latestDetectedPitches))
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

            if (ContainsAcceptedPitch(note, ev.pitches))
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

        float windowStart = note.data.time - highStringRescueTightWindow - eventTimeSlack;
        float windowEnd = note.data.time + highStringRescueTightWindow + eventTimeSlack;

        for (int i = recentNoteEvents.Count - 1; i >= 0; i--)
        {
            NoteEvent ev = recentNoteEvents[i];
            if (ev.time < windowStart) break;
            if (ev.time > windowEnd) continue;

            if (!TryGetAcceptedConsumeKey(note, ev.pitches, out int acceptedConsumeKey)) continue;
            rescueConsumeKey = 500000 + (acceptedConsumeKey * 8) + note.data.stringIdx;
            if (ev.consumedKeys.Contains(rescueConsumeKey) || ev.consumedKeys.Contains(acceptedConsumeKey)) continue;

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

    private int GetBaseTargetPitch(GameplayNoteState note)
    {
        return stringBasePitch[note.data.stringIdx] + note.data.fret;
    }

    private bool TryGetBentTargetPitch(GameplayNoteState note, out int bentTargetPitch)
    {
        bentTargetPitch = -1;
        bool hasBend = note != null && (
            note.data.technique == NoteTechnique.Bend ||
            note.data.bendStep > 0f ||
            note.data.bendPreBend ||
            note.data.bendRelease);
        if (!hasBend)
            return false;

        int bendSemitones = Mathf.RoundToInt(note.data.bendStep);
        if (bendSemitones == 0)
            return false;

        int baseTargetPitch = GetBaseTargetPitch(note);
        bentTargetPitch = baseTargetPitch + bendSemitones;
        return bentTargetPitch != baseTargetPitch;
    }

    private bool ContainsAcceptedPitch(GameplayNoteState note, HashSet<int> pitches)
    {
        return TryGetAcceptedConsumeKey(note, pitches, out _);
    }

    private bool TryGetAcceptedConsumeKey(GameplayNoteState note, HashSet<int> pitches, out int consumeKey)
    {
        consumeKey = -1;
        if (pitches == null || pitches.Count == 0)
            return false;

        int baseTargetPitch = GetBaseTargetPitch(note);
        if (pitches.Contains(baseTargetPitch))
        {
            consumeKey = baseTargetPitch;
            return true;
        }

        if (TryGetBentTargetPitch(note, out int bentTargetPitch) && pitches.Contains(bentTargetPitch))
        {
            consumeKey = bentTargetPitch;
            return true;
        }

        int baseModulo = baseTargetPitch % 12;
        if (pitches.Any(p => p % 12 == baseModulo))
        {
            consumeKey = baseTargetPitch;
            return true;
        }

        if (TryGetBentTargetPitch(note, out bentTargetPitch))
        {
            int bentModulo = bentTargetPitch % 12;
            if (pitches.Any(p => p % 12 == bentModulo))
            {
                consumeKey = bentTargetPitch;
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
    private void EnsureDetectorHintClient()
    {
        if (detectorHintClient != null || !sendDetectorHintsToPython)
            return;

        try
        {
            detectorHintClient = new UdpClient();
            IPAddress address;
            if (!IPAddress.TryParse(detectorHintIp, out address))
                address = IPAddress.Loopback;
            detectorHintEndpoint = new IPEndPoint(address, detectorHintPort);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Detector hint UDP init error: " + e.Message);
            detectorHintClient = null;
            detectorHintEndpoint = null;
        }
    }

    private void CloseDetectorHintClient()
    {
        if (detectorHintClient == null)
            return;

        try
        {
            detectorHintClient.Close();
        }
        catch
        {
        }

        detectorHintClient = null;
        detectorHintEndpoint = null;
    }

    private void MarkDetectorHintDirty()
    {
        detectorHintForceSend = true;
        lastDetectorHintSendRealtime = -999f;
    }

    private void SendDetectorHintPacketIfNeeded()
    {
        if (!sendDetectorHintsToPython)
            return;

        float realtimeNow = Time.realtimeSinceStartup;
        if (!detectorHintForceSend && (realtimeNow - lastDetectorHintSendRealtime) < detectorHintSendIntervalSeconds)
            return;

        EnsureDetectorHintClient();
        if (detectorHintClient == null || detectorHintEndpoint == null)
            return;

        string payload = BuildDetectorHintPayload(songTimer);
        if (string.IsNullOrEmpty(payload))
            return;

        if (notesDetectorBackendMode == NotesDetectorBackendMode.NativeEmbeddedBridge)
        {
            EnsureNativeNotesDetectorBridge();
            if (nativeNotesDetectorBridge != null && nativeNotesDetectorBridge.SetHintPayload(payload))
            {
                lastDetectorHintSendRealtime = realtimeNow;
                detectorHintForceSend = false;
            }
            return;
        }

        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            detectorHintClient.Send(bytes, bytes.Length, detectorHintEndpoint);
            lastDetectorHintSendRealtime = realtimeNow;
            detectorHintForceSend = false;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Detector hint UDP send error: " + e.Message);
            CloseDetectorHintClient();
        }
    }

    private string BuildDetectorHintPayload(float currentSongTime)
    {
        var windows = new List<DetectorHintWindow>();
        BuildDetectorHintWindows(currentSongTime, windows);

        if (windows.Count == 0)
            return $"SYNC|{currentSongTime.ToString("F3", CultureInfo.InvariantCulture)}";

        StringBuilder builder = new StringBuilder();
        builder.Append("HINT|");
        builder.Append(currentSongTime.ToString("F3", CultureInfo.InvariantCulture));

        for (int i = 0; i < windows.Count; i++)
        {
            DetectorHintWindow window = windows[i];
            string notesCsv = string.Join(",", window.pitches
                .OrderBy(p => p)
                .Select(GetNoteNameFromMidi));
            if (string.IsNullOrEmpty(notesCsv))
                continue;

            builder.Append('|');
            builder.Append(window.startTime.ToString("F3", CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(window.endTime.ToString("F3", CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(notesCsv);
        }

        return builder.ToString();
    }

    private void BuildDetectorHintWindows(float currentSongTime, List<DetectorHintWindow> output)
    {
        output.Clear();
        if (noteStates == null || noteStates.Count == 0)
            return;

        float minTime = currentSongTime - detectorHintLookbackSeconds;
        float maxTime = currentSongTime + detectorHintLookaheadSeconds;

        for (int i = 0; i < noteStates.Count; i++)
        {
            GameplayNoteState noteState = noteStates[i];
            if (noteState == null || noteState.result == GameplayNoteResult.Missed)
                continue;

            AppendDetectorHintWindowsForNote(noteState, minTime, maxTime, output);
        }

        output.Sort((a, b) =>
        {
            int startCompare = a.startTime.CompareTo(b.startTime);
            if (startCompare != 0)
                return startCompare;
            return a.endTime.CompareTo(b.endTime);
        });

        MergeDetectorHintWindows(output);

        if (output.Count > detectorHintMaxWindowsPerPacket)
            output.RemoveRange(detectorHintMaxWindowsPerPacket, output.Count - detectorHintMaxWindowsPerPacket);
    }

    private void MergeDetectorHintWindows(List<DetectorHintWindow> windows)
    {
        if (windows.Count <= 1)
            return;

        int writeIndex = 0;
        for (int readIndex = 1; readIndex < windows.Count; readIndex++)
        {
            DetectorHintWindow current = windows[writeIndex];
            DetectorHintWindow next = windows[readIndex];

            if (AreHintPitchSetsEqual(current.pitches, next.pitches) && next.startTime <= current.endTime + 0.001f)
            {
                current.endTime = Mathf.Max(current.endTime, next.endTime);
                windows[writeIndex] = current;
                continue;
            }

            writeIndex++;
            windows[writeIndex] = next;
        }

        if (writeIndex < windows.Count - 1)
            windows.RemoveRange(writeIndex + 1, windows.Count - writeIndex - 1);
    }

    private bool AreHintPitchSetsEqual(HashSet<int> a, HashSet<int> b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null || a.Count != b.Count)
            return false;
        return a.SetEquals(b);
    }

    private void AppendDetectorHintWindowsForNote(GameplayNoteState noteState, float rangeStart, float rangeEnd, List<DetectorHintWindow> output)
    {
        NoteData note = noteState.data;
        float noteStart = note.time;
        float noteEnd = note.time + Mathf.Max(0.05f, note.duration);
        List<NoteTechniqueSegmentData> segments = note.techniqueSegments;
        bool hasSegments = segments != null && segments.Count > 0;
        float maxTechniqueEnd = noteStart;
        if (hasSegments)
        {
            for (int i = 0; i < segments.Count; i++)
                maxTechniqueEnd = Mathf.Max(maxTechniqueEnd, note.time + Mathf.Max(segments[i].startOffset, segments[i].endOffset));
        }

        float overallHintEnd = Mathf.Max(noteEnd, maxTechniqueEnd);
        if (overallHintEnd < rangeStart || noteStart > rangeEnd)
            return;

        float finalSegmentEnd = noteStart;

        if (hasSegments)
        {
            List<NoteTechniqueSegmentData> orderedSegments = segments
                .OrderBy(segment => segment.startOffset)
                .ThenBy(segment => segment.endOffset)
                .ToList();

            bool appliedOnsetLookback = false;
            for (int i = 0; i < orderedSegments.Count; i++)
            {
                NoteTechniqueSegmentData segment = orderedSegments[i];
                float segmentStart = noteStart + Mathf.Max(0f, segment.startOffset);
                float segmentEnd = noteStart + Mathf.Max(segment.startOffset, segment.endOffset);
                if (segmentEnd <= segmentStart + 0.0001f)
                    continue;

                finalSegmentEnd = Mathf.Max(finalSegmentEnd, segmentEnd);
                AppendDetectorHintWindowsForSegment(note, segment, segmentStart, segmentEnd, !appliedOnsetLookback, rangeStart, rangeEnd, output);
                appliedOnsetLookback = true;
            }
        }

        if (!hasSegments)
        {
            AddDetectorHintWindow(noteStart - detectorHintLookbackSeconds, noteEnd, GetPitchSetForMidi(GetNoteMidiFromStringFret(note.stringIdx, note.fret)), rangeStart, rangeEnd, output);
            return;
        }

        if (noteEnd > finalSegmentEnd + 0.001f)
        {
            int tailMidi = GetTailMidiForNote(note);
            AddDetectorHintWindow(finalSegmentEnd, noteEnd, GetPitchSetForMidi(tailMidi), rangeStart, rangeEnd, output);
        }
    }

    private void AppendDetectorHintWindowsForSegment(NoteData note, NoteTechniqueSegmentData segment, float segmentStart, float segmentEnd, bool includeOnsetLookback, float rangeStart, float rangeEnd, List<DetectorHintWindow> output)
    {
        switch (segment.type)
        {
            case NoteTechniqueSegmentType.Slide:
            case NoteTechniqueSegmentType.Bend:
                AppendInterpolatedDetectorHintWindows(note, segment, segmentStart, segmentEnd, includeOnsetLookback, rangeStart, rangeEnd, output);
                break;
            case NoteTechniqueSegmentType.Sustain:
            case NoteTechniqueSegmentType.Vibrato:
            default:
                int sustainMidi = Mathf.RoundToInt(EvaluateTechniqueSegmentMidi(note.stringIdx, segment, 1f));
                AddDetectorHintWindow(
                    includeOnsetLookback ? segmentStart - detectorHintLookbackSeconds : segmentStart,
                    segmentEnd,
                    GetPitchSetForMidi(sustainMidi),
                    rangeStart,
                    rangeEnd,
                    output);
                break;
        }
    }

    private void AppendInterpolatedDetectorHintWindows(NoteData note, NoteTechniqueSegmentData segment, float segmentStart, float segmentEnd, bool includeOnsetLookback, float rangeStart, float rangeEnd, List<DetectorHintWindow> output)
    {
        float duration = Mathf.Max(0.0001f, segmentEnd - segmentStart);
        int stepCount = Mathf.Clamp(Mathf.CeilToInt(duration / Mathf.Max(0.01f, detectorHintInterpolationStepSeconds)), 1, 64);
        float bucketStart = segmentStart;
        int bucketMidi = Mathf.RoundToInt(EvaluateTechniqueSegmentMidi(note.stringIdx, segment, 0f));

        for (int stepIndex = 1; stepIndex <= stepCount; stepIndex++)
        {
            float t = stepIndex / (float)stepCount;
            float sampleTime = Mathf.Lerp(segmentStart, segmentEnd, t);
            int sampleMidi = Mathf.RoundToInt(EvaluateTechniqueSegmentMidi(note.stringIdx, segment, t));

            if (sampleMidi == bucketMidi && stepIndex < stepCount)
                continue;

            float sendStart = bucketStart;
            if (includeOnsetLookback && Mathf.Abs(bucketStart - segmentStart) <= 0.0001f)
                sendStart -= detectorHintLookbackSeconds;

            AddDetectorHintWindow(sendStart, sampleTime, GetPitchSetForMidi(bucketMidi), rangeStart, rangeEnd, output);
            bucketStart = sampleTime;
            bucketMidi = sampleMidi;
        }
    }

    private float EvaluateTechniqueSegmentMidi(int stringIdx, NoteTechniqueSegmentData segment, float t)
    {
        float clampedT = Mathf.Clamp01(t);
        float fret = Mathf.Lerp(segment.startFret, segment.endFret, clampedT);
        float bend = Mathf.Lerp(segment.startBend, segment.endBend, clampedT);
        return GetStringBasePitch(stringIdx) + fret + bend;
    }

    private int GetTailMidiForNote(NoteData note)
    {
        List<NoteTechniqueSegmentData> segments = note.techniqueSegments;
        if (segments != null && segments.Count > 0)
        {
            NoteTechniqueSegmentData last = segments
                .OrderBy(segment => segment.endOffset)
                .ThenBy(segment => segment.startOffset)
                .Last();
            return Mathf.RoundToInt(EvaluateTechniqueSegmentMidi(note.stringIdx, last, 1f));
        }

        return GetNoteMidiFromStringFret(note.stringIdx, note.fret);
    }

    private HashSet<int> GetPitchSetForMidi(int midi)
    {
        return new HashSet<int> { midi };
    }

    private void AddDetectorHintWindow(float startTime, float endTime, HashSet<int> pitches, float rangeStart, float rangeEnd, List<DetectorHintWindow> output)
    {
        if (pitches == null || pitches.Count == 0)
            return;

        float clippedStart = Mathf.Max(startTime, rangeStart);
        float clippedEnd = Mathf.Min(endTime, rangeEnd);
        if (clippedEnd <= clippedStart + 0.0001f)
            return;

        output.Add(new DetectorHintWindow(clippedStart, clippedEnd, pitches));
    }

    private int GetNoteMidiFromStringFret(int stringIdx, int fret)
    {
        return GetStringBasePitch(stringIdx) + fret;
    }

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

    private void ParseDetectorState()
    {
        if (notesDetectorBackendMode == NotesDetectorBackendMode.NativeEmbeddedBridge)
        {
            string nativePacket = nativeNotesDetectorBridge != null ? nativeNotesDetectorBridge.PollLatestPacket() : string.Empty;
            if (!string.IsNullOrEmpty(nativePacket) && nativePacket != "--")
                Interlocked.Exchange(ref lastUdpPacketUtcTicks, DateTime.UtcNow.Ticks);

            ParseDetectorPacket(nativePacket);
            return;
        }

        ParseDetectorPacket(logNotes);
    }

private void ParseDetectorPacket(string detectorPacket)
    {
        latestDetectedPitches.Clear();
        latestPacketHadEvent = false;
        latestEventNotesText = "--";
        latestParsedInputLevel = -1f;

        if (string.IsNullOrEmpty(detectorPacket) || detectorPacket == "--") return;

        if (detectorPacket.StartsWith("A|"))
        {
            string[] parts = detectorPacket.Split('|');
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
                Debug.LogWarning($"<color=cyan>[RAW DETECTOR RECEIVED]</color> {detectorPacket}  ||  Parsed ID: {eventId}, Parsed Age: {eventAge:F3}");
            }

            if (eventId <= 0 || string.IsNullOrWhiteSpace(eventCsv) || eventCsv == "--") return;

            float estimatedEventTime = GetEstimatedNoteEventSongTime(eventAge);
            
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

    private float GetEstimatedNoteEventSongTime(float eventAge)
    {
        if (noteByNoteWaitingForMatch)
            return Mathf.Max(0f, noteByNoteWaitingNoteTime >= 0f ? noteByNoteWaitingNoteTime : songTimer);

        float eventAgeInSongTime = Mathf.Max(0f, eventAge) * GetPlaybackSpeedScale();
        return Mathf.Max(0f, songTimer - eventAgeInSongTime);
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

    public float GetEffectiveTabSectionDuration()
    {
        float baseDuration = tabSectionDuration * Mathf.Max(0.5f, tabSectionLengthMultiplier);
        return Mathf.Max(0.25f, baseDuration / GetVisualTabSpacingScale());
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
        float duration = 0f;

        if (backingTrackSource != null && backingTrackSource.clip != null)
            duration = Mathf.Max(duration, backingTrackSource.clip.length);

        if (generatedSongPlayer != null && IsGeneratedPlaybackAvailable())
            duration = Mathf.Max(duration, generatedSongPlayer.ArrangementDurationSeconds);

        if (chartNotes != null && chartNotes.Count > 0)
            duration = Mathf.Max(duration, chartNotes.Max(note => note.time + Mathf.Max(0.05f, note.duration)));

        return Mathf.Max(0f, duration);
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
            SetSongEndState(false);
            return;
        }

        if (songHasEnded)
        {
            if (!isPaused && songTimer < duration - 0.02f)
                SetSongEndState(false);
            return;
        }

        if (!loopEnabled && !showMainMenu && !showSongSelection && !showTrackSelection && songTimer >= duration)
        {
            songTimer = duration;
            audioSongTimer = duration;
            EnterSongEndState(asGameOver: false);
            return;
        }

        if (!isPaused && songTimer < duration - 0.02f)
            SetSongEndState(false);
    }

    private GuitarGameplaySnapshot BuildSnapshot()
    {
        int currentSectionIndex = GetSectionIndex(songTimer);
        float sectionDuration = GetEffectiveTabSectionDuration();
        float sectionStart = currentSectionIndex * sectionDuration;
        float progress = Mathf.Clamp01((songTimer - sectionStart) / Mathf.Max(0.01f, sectionDuration));
        SongMetadata pendingTrackMetadata = pendingTrackSelectionSong != null ? LoadSongMetadataForEntry(pendingTrackSelectionSong) : null;
        List<float> availableSongScores = new List<float>(availableSongs.Count);
        List<string> availableSongScoreTexts = new List<string>(availableSongs.Count);
        for (int i = 0; i < availableSongs.Count; i++)
        {
            SongLibraryEntry song = availableSongs[i];
            SongMetadata metadata = currentSongEntry != null &&
                                    song != null &&
                                    string.Equals(currentSongEntry.SongDirectory, song.SongDirectory, StringComparison.OrdinalIgnoreCase)
                ? songMetadata
                : LoadSongMetadataForEntry(song);
            float normalScore = Mathf.Clamp(GetHighestTrackScore(metadata), 0f, 100f);
            HeroScoreSummary heroScore = GetHighestHeroTrackScoreSummary(metadata);
            availableSongScores.Add(normalScore);
            availableSongScoreTexts.Add(BuildCombinedScoreText(normalScore, heroScore));
        }

        List<float> availableTrackScores = new List<float>(pendingTrackSelectionParts.Count);
        List<string> availableTrackScoreTexts = new List<string>(pendingTrackSelectionParts.Count);
        for (int i = 0; i < pendingTrackSelectionParts.Count; i++)
        {
            MusicXmlLoader.MusicXmlPartSummary track = pendingTrackSelectionParts[i];
            float normalScore = pendingTrackMetadata != null ? GetStoredTrackScore(pendingTrackMetadata, track.PartId) : 0f;
            HeroScoreSummary heroScore = pendingTrackMetadata != null
                ? GetStoredHeroTrackScoreSummary(pendingTrackMetadata, track.PartId)
                : default;
            availableTrackScores.Add(normalScore);
            availableTrackScoreTexts.Add(BuildCombinedScoreText(normalScore, heroScore));
        }

        SongLibraryEntry selectedLibrarySongEntry =
            selectedSongListIndex >= 0 && selectedSongListIndex < availableSongs.Count
                ? availableSongs[selectedSongListIndex]
                : null;
        SongMetadata selectedLibrarySongMetadata =
            selectedLibrarySongEntry != null &&
            currentSongEntry != null &&
            string.Equals(selectedLibrarySongEntry.SongDirectory, currentSongEntry.SongDirectory, StringComparison.OrdinalIgnoreCase)
                ? songMetadata
                : LoadSongMetadataForEntry(selectedLibrarySongEntry);
        HeroScoreSummary selectedLibraryHeroScore = GetHighestHeroTrackScoreSummary(selectedLibrarySongMetadata);

        return new GuitarGameplaySnapshot
        {
            songTime = songTimer,
            isPaused = isPaused,
            noteByNoteModeEnabled = noteByNoteModeEnabled,
            noteByNoteWaitingForMatch = noteByNoteWaitingForMatch,
            heroModeEnabled = heroModeEnabled,
            heroModeHeartCount = heroModeHeartCount,
            currentHeroHeartsRemaining = GetCurrentHeroHeartsRemaining(),
            showHighwayCharacter = ShouldDisplayHighwayCharacter(heroModeEnabled),
            selectedPauseActionIndex = selectedPauseActionIndex,
            selectedGameModesIndex = selectedGameModesIndex,
            selectedHeroModeSettingsIndex = selectedHeroModeSettingsIndex,
            selectedSongEndActionIndex = selectedSongEndActionIndex,
            selectedSongSettingsIndex = selectedSongSettingsIndex,
            loopEnabled = loopEnabled,
            loopStartTime = loopStartTime,
            loopEndTime = loopEndTime,
            selectedLoopMarker = selectedLoopMarker,
            showLoopSettings = showLoopSettings,
            showGameModes = showGameModes,
            showHeroModeSettings = showHeroModeSettings,
            loopPreviewPlaying = loopSettingsPreviewPlaying,
            showLoopPausePopup = showLoopPausePopup,
            selectedLoopPausePopupIndex = selectedLoopPausePopupIndex,
            loopPauseDurationSeconds = loopPauseDurationSeconds,
            loopRestartPauseRemainingSeconds = loopRestartPauseRemainingSeconds,
            playbackSpeedPercent = playbackSpeedPercent,
            scoreSaveInvalidated = scoreSaveInvalidated,
            currentSectionIndex = currentSectionIndex,
            nextSectionIndex = currentSectionIndex + 1,
            currentSectionProgress = progress,
            sectionDuration = GetEffectiveTabSectionDuration(),
            currentSessionScoreHits = sessionScoreHits,
            currentSessionScoreMisses = sessionScoreMisses,
            currentSessionScorePercent = currentSessionScorePercent,
            noteStates = noteStates,
            sections = tabSections,
            latestDetectedPitches = latestDetectedPitches,
            showSongSettings = showSongSettings,
            showOffsetHelper = showOffsetHelper,
            offsetHelperAdjusting = offsetHelperAdjusting,
            offsetHelperPreviewPlaying = offsetHelperPreviewPlaying,
            offsetHelperAnchorTime = offsetHelperAnchorTime,
            offsetHelperPreviewStartTime = offsetHelperPreviewStartTime,
            offsetHelperPreviewEndTime = offsetHelperPreviewEndTime,
            offsetHelperAnchorLabel = GetOffsetHelperAnchorLabel(),
            showMainMenu = showMainMenu,
            mainMenuFlowActive = mainMenuFlowActive,
            selectedMainMenuIndex = selectedMainMenuIndex,
            showSongSelection = showSongSelection,
            songSelectionSongConfirmed = songSelectionSongConfirmed,
            showTrackSelection = showTrackSelection,
            showToneLab = showToneLab,
            showNotesDetectorTestMenu = showNotesDetectorTestMenu,
            showGlobalSettings = showGlobalSettings,
            selectedGlobalSettingsTopIndex = selectedGlobalSettingsTopIndex,
            selectedGlobalSettingsItemIndex = selectedGlobalSettingsItemIndex,
            activeGlobalSettingsCategory = activeGlobalSettingsCategory,
            selectedNotesDetectorTestIndex = selectedNotesDetectorTestIndex,
            availableSongNames = availableSongs.Select(song => song.DisplayName).ToList(),
            availableSongSubtitles = availableSongs.Select(song => song.Subtitle ?? string.Empty).ToList(),
            availableSongArtworkPaths = availableSongs.Select(song => song?.ArtworkPath ?? string.Empty).ToList(),
            availableSongScores = availableSongScores,
            availableSongScoreTexts = availableSongScoreTexts,
            selectedSongIndex = selectedSongListIndex,
            selectedLibrarySongSubtitle = selectedLibrarySongEntry?.Subtitle ?? string.Empty,
            selectedLibrarySongArtworkPath = selectedLibrarySongEntry?.ArtworkPath ?? string.Empty,
            selectedLibrarySongTrackCount = pendingTrackSelectionParts.Count,
            selectedLibrarySongHeroBestHeartsRemaining = Mathf.Max(0, selectedLibraryHeroScore.heartsRemaining),
            selectedLibrarySongHeroBestHeartsTotal = Mathf.Max(0, selectedLibraryHeroScore.heartsTotal),
            selectedLibrarySongHasMp3 = selectedLibrarySongEntry != null && !string.IsNullOrWhiteSpace(selectedLibrarySongEntry.Mp3Path),
            selectedLibrarySongHasMidi = selectedLibrarySongEntry != null && !string.IsNullOrWhiteSpace(selectedLibrarySongEntry.MidiPath),
            selectedLibrarySongIsCurrent = selectedLibrarySongEntry != null &&
                                          currentSongEntry != null &&
                                          string.Equals(selectedLibrarySongEntry.SongDirectory, currentSongEntry.SongDirectory, StringComparison.OrdinalIgnoreCase),
            availableTrackNames = pendingTrackSelectionParts.Select(track => track.Name).ToList(),
            availableTrackScores = availableTrackScores,
            availableTrackScoreTexts = availableTrackScoreTexts,
            selectedTrackIndex = selectedTrackListIndex,
            currentSongDisplayName = currentSongEntry != null ? currentSongEntry.DisplayName : string.Empty,
            songListScrollOffset = songListScrollOffset,
            audioOffsetMs = audioOffsetMs,
            tabSpeedOffsetPercent = tabSpeedOffsetPercent,
            songStartDelaySeconds = songStartDelaySeconds,
            songVolumePercent = songVolumePercent,
            songPlaybackAudioModeLabel = GetCurrentSongPlaybackAudioModeLabel(),
            songPlaybackUsesGeneratedMode = GetEffectiveSongPlaybackAudioMode() == SongPlaybackAudioMode.Generated,
            generatedAudioTrackSelectionAvailable = GetAvailableGeneratedPlaybackParts().Count > 0,
            generatedAudioTrackSelectionSummary = GetGeneratedPlaybackTrackSelectionSummary(),
            showGeneratedAudioTrackSelectionPopup = showGeneratedAudioTrackSelectionPopup,
            generatedAudioTrackNames = GetAvailableGeneratedPlaybackParts().Select(part => part.displayName).ToList(),
            generatedAudioTrackEnabled = GetAvailableGeneratedPlaybackParts().Select(part => IsGeneratedPlaybackPartEnabled(part.partId)).ToList(),
            selectedGeneratedAudioTrackIndex = selectedGeneratedAudioTrackSelectionIndex,
            selectedTrackDisplayName = GetTrackDisplayName(GetCurrentTrackOptionIndex()),
            trackSelectionHint = GetTrackOptionCount() > 1 ? "Track: click row or Q/E" : "Track: single detected part",
            offsetScopeLabel = useTrackOffsetForCurrentTrack ? "Track" : "Song",
            offsetScopeHint = "Offset scope: O toggles Song/Track",
            hasBackingTrack = hasBackingTrack,
            isBackingTrackPlaying = backingTrackSource != null && backingTrackSource.isPlaying,
            backingTrackTime = backingTrackSource != null && backingTrackSource.clip != null ? backingTrackSource.time : 0f,
            noteDetectorConnected = IsNoteDetectorConnected(),
            inputLevelNormalized = smoothedInputLevel,
            songDuration = GetSongDurationSeconds(),
            songProgressNormalized = GetSongProgressNormalized(),
            songEnded = songHasEnded,
            songEndedAsGameOver = songEndedAsGameOver,
            currentTrackBestScorePercent = Mathf.Clamp(currentTrackBestScorePercent, 0f, 100f),
            currentTrackHeroBestScorePercent = Mathf.Clamp(currentTrackHeroBestScorePercent, 0f, 100f),
            currentTrackHeroBestHeartsRemaining = Mathf.Max(0, currentTrackHeroBestHeartsRemaining),
            currentTrackHeroBestHeartsTotal = Mathf.Max(0, currentTrackHeroBestHeartsTotal),
            notesDetectorBackendLabel = GetNotesDetectorBackendLabel(),
            notesDetectorStatusText = GetNotesDetectorStatusText(),
            notesDetectorDetailText = GetNotesDetectorDetailText(),
            notesDetectorAvailableInputDevices = BuildNotesDetectorInputDeviceSnapshot(),
            selectedNotesDetectorInputDeviceIndex = GetSelectedNativeDetectorInputDeviceUiIndex(),
            notesDetectorPresetLabels = BuildNotesDetectorPresetLabelsSnapshot(),
            selectedNotesDetectorPresetIndex = GetSelectedNativeDetectorPresetUiIndex(),
            notesDetectorSelectedPresetLabel = GetSelectedNativeDetectorPresetLabel(),
            notesDetectorSettings = BuildNativeDetectorSettingsSnapshot(),
            notesDetectorFastNotesText = latestDetectedPitches != null && latestDetectedPitches.Count > 0 ? FormatMidiSetCsv(latestDetectedPitches) : "--",
            notesDetectorAiNotesText = string.IsNullOrWhiteSpace(latestEventNotesText) ? "--" : latestEventNotesText,
            showNotesDetectorRoutinePopup = showNotesDetectorRoutinePopup,
            notesDetectorRoutineInstructionText = GetNotesDetectorRoutineInstructionText(),
            notesDetectorRoutineTargetText = GetNotesDetectorRoutineTargetText(),
            notesDetectorRoutineStatusText = GetNotesDetectorRoutineStatusText(),
            notesDetectorRoutineProgressText = GetNotesDetectorRoutineProgressText(),
            notesDetectorRoutineTabRows = GetNotesDetectorRoutineTabRows(),
            notesDetectorRoutineStatusOk = showNotesDetectorRoutinePopup && (notesDetectorRoutineStageIndex >= notesDetectorRoutineSteps.Count || notesDetectorRoutineMatchedSinceTime >= 0f),
            notesDetectorRoutineCompleted = showNotesDetectorRoutinePopup && notesDetectorRoutineStageIndex >= notesDetectorRoutineSteps.Count,
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
            $"SONG VOLUME: <color=cyan>{songVolumePercent:F0}%</color>\n" +
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
        ClearNoteByNoteWaitingState();
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
        SetSongEndState(false);
        showLoopSettings = false;
        showLoopPausePopup = false;
        selectedLoopPausePopupIndex = 0;
        loopSettingsPreviewPlaying = false;
        loopRestartPauseRemainingSeconds = 0f;
        scoreSaveInvalidated = false;
        ResetSessionScoreState();
        loopSettingsReturnRenderMode = renderMode;

        float sectionDuration = GetEffectiveTabSectionDuration();
        loopStartTime = Mathf.Max(0.2f, sectionDuration * 0.40f);
        loopEndTime = Mathf.Max(loopStartTime + 0.5f, sectionDuration * 0.60f);
        loopEnabled = false;
        loopPauseDurationSeconds = 0f;
        selectedLoopMarker = 1;
        playbackSpeedPercent = 100f;
        showSongSettings = preservePauseUiState ? wasShowingSongSettings : false;
        showMainMenu = preservePauseUiState ? wasShowingMainMenu : showMainMenu;
        mainMenuFlowActive = preservePauseUiState ? wasMainMenuFlowActive : showMainMenu;
        showSongSelection = preservePauseUiState ? wasShowingSongSelection : false;
        showTrackSelection = preservePauseUiState ? wasShowingTrackSelection : false;
        showGlobalSettings = preservePauseUiState ? wasShowingGlobalSettings : false;
        tabSpeedOffsetPercent = 100f;
        songVolumePercent = 100f;

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
                currentSongFileName = ResolveSongMetadataFileName(currentSongEntry);
                SongMetadata trackMetadata = LoadSongMetadata(currentSongFileName);
                useAutoTrackSelection = trackMetadata.useAutoTrackSelection;
                selectedMusicXmlPartId = string.IsNullOrEmpty(trackMetadata.selectedMusicXmlPartId) ? string.Empty : trackMetadata.selectedMusicXmlPartId;

                currentSongPartSummaries.AddRange(GetPartSummariesWithFallback(currentSongEntry));
                ApplyTrackSelectionPreference();

                try
                {
                    loadedNotes = SongNotationFacade.LoadSong(currentSongEntry.PrimaryNotationPath, currentSongEntry.PrimaryNotationKind, midiTrackIndex);
                    Debug.Log($"Notation load attempt: {currentSongEntry.PrimaryNotationPath} ({currentSongEntry.PrimaryNotationKind})");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Primary notation loader error: " + e.Message);
                }

                if ((loadedNotes == null || loadedNotes.Count == 0) &&
                    currentSongEntry.PrimaryNotationKind != SongNotationSourceKind.MusicXml &&
                    !string.IsNullOrEmpty(currentSongEntry.XmlPath))
                {
                    try
                    {
                        loadedNotes = MusicXmlLoader.LoadMusicXmlSong(currentSongEntry.XmlPath, midiTrackIndex);
                        Debug.Log($"MusicXML fallback load attempt: {currentSongEntry.XmlPath}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("MusicXmlLoader fallback error: " + e.Message);
                    }
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
        MarkDetectorHintDirty();
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

    private void EnsureGeneratedSongPlayerInitialized()
    {
        if (generatedSongPlayer == null)
            generatedSongPlayer = new GeneratedSongPlayer();

        generatedSongPlayer.EnsureInitialized(transform);
        generatedSongPlayer.SetMasterVolumePercent(songVolumePercent);
    }

    private void ShutdownGeneratedSongPlayer()
    {
        if (generatedSongPlayer == null)
            return;

        generatedSongPlayer.Dispose();
        generatedSongPlayer = null;
    }

    private void LoadGeneratedPlaybackArrangementForCurrentSong()
    {
        generatedPlaybackSourceArrangement = null;
        generatedPlaybackArrangement = null;

        if (currentSongEntry == null || string.IsNullOrWhiteSpace(currentSongEntry.PrimaryNotationPath))
        {
            generatedSongPlayer?.ClearArrangement();
            return;
        }

        EnsureGeneratedSongPlayerInitialized();
        generatedPlaybackSourceArrangement = SongNotationFacade.LoadGeneratedArrangement(currentSongEntry.PrimaryNotationPath, currentSongEntry.PrimaryNotationKind);
        if ((generatedPlaybackSourceArrangement == null || !generatedPlaybackSourceArrangement.IsValid) &&
            currentSongEntry.PrimaryNotationKind != SongNotationSourceKind.MusicXml &&
            !string.IsNullOrWhiteSpace(currentSongEntry.XmlPath))
        {
            generatedPlaybackSourceArrangement = MusicXmlBandPlaybackLoader.LoadArrangement(currentSongEntry.XmlPath);
        }
        RestoreGeneratedPlaybackSelectionForCurrentTrack();
        ApplyGeneratedPlaybackSelection();
    }

    private bool IsGeneratedPlaybackAvailable()
    {
        return generatedPlaybackArrangement != null && generatedPlaybackArrangement.IsValid;
    }

    private void ApplyGeneratedPlaybackSelection()
    {
        if (generatedPlaybackSourceArrangement == null)
        {
            generatedPlaybackArrangement = null;
            generatedSongPlayer?.ClearArrangement();
            return;
        }

        generatedPlaybackArrangement = GeneratedPlaybackArrangementFilter.CreateFiltered(
            generatedPlaybackSourceArrangement,
            generatedEnabledPartIds,
            useAllGeneratedPlaybackParts);

        if (generatedPlaybackArrangement != null && generatedPlaybackArrangement.IsValid)
        {
            generatedSongPlayer?.LoadArrangement(transform, generatedPlaybackArrangement);
            generatedSongPlayer?.SetMasterVolumePercent(songVolumePercent);
        }
        else
        {
            generatedSongPlayer?.ClearArrangement();
        }
    }

    private string GetCurrentGeneratedPlaybackSelectionKey()
    {
        return GetCurrentOffsetPartId();
    }

    private static GeneratedPlaybackSelectionOverride GetGeneratedPlaybackSelectionOverride(SongMetadata metadata, string partId)
    {
        if (metadata?.generatedPlaybackSelectionOverrides == null || string.IsNullOrWhiteSpace(partId))
            return null;

        return metadata.generatedPlaybackSelectionOverrides.FirstOrDefault(entry =>
            string.Equals(entry.partId ?? string.Empty, partId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasLegacyCustomizedGeneratedPlaybackSelection(SongMetadata metadata)
    {
        return metadata != null &&
               (!metadata.useAllGeneratedPlaybackParts || (metadata.generatedEnabledPartIds?.Count ?? 0) > 0);
    }

    private void RestoreGeneratedPlaybackSelectionForCurrentTrack()
    {
        List<GeneratedPlaybackPartInfo> availableParts = GetAvailableGeneratedPlaybackParts();
        if (availableParts.Count == 0)
        {
            useAllGeneratedPlaybackParts = true;
            generatedEnabledPartIds.Clear();
            return;
        }

        string selectionKey = GetCurrentGeneratedPlaybackSelectionKey();
        GeneratedPlaybackSelectionOverride selectionOverride = GetGeneratedPlaybackSelectionOverride(songMetadata, selectionKey);
        if (selectionOverride != null)
        {
            useAllGeneratedPlaybackParts = selectionOverride.useAllGeneratedPlaybackParts;
            generatedEnabledPartIds = selectionOverride.generatedEnabledPartIds != null
                ? selectionOverride.generatedEnabledPartIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>();
            NormalizeGeneratedPlaybackSelectionToAvailableParts(availableParts);
            return;
        }

        if ((songMetadata.generatedPlaybackSelectionOverrides == null || songMetadata.generatedPlaybackSelectionOverrides.Count == 0) &&
            HasLegacyCustomizedGeneratedPlaybackSelection(songMetadata))
        {
            useAllGeneratedPlaybackParts = songMetadata.useAllGeneratedPlaybackParts;
            generatedEnabledPartIds = songMetadata.generatedEnabledPartIds != null
                ? songMetadata.generatedEnabledPartIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>();
            NormalizeGeneratedPlaybackSelectionToAvailableParts(availableParts);
            return;
        }

        ApplyDefaultGeneratedPlaybackSelectionForCurrentTrack(availableParts);
    }

    private void ApplyDefaultGeneratedPlaybackSelectionForCurrentTrack(List<GeneratedPlaybackPartInfo> availableParts = null)
    {
        availableParts ??= GetAvailableGeneratedPlaybackParts();
        useAllGeneratedPlaybackParts = false;

        string selectionKey = GetCurrentGeneratedPlaybackSelectionKey();
        List<string> enabledIds = new List<string>();

        if (!string.IsNullOrWhiteSpace(selectionKey))
        {
            enabledIds.AddRange(availableParts
                .Where(part => string.Equals(part.partId, selectionKey, StringComparison.OrdinalIgnoreCase))
                .Select(part => part.partId));
        }

        enabledIds.AddRange(availableParts
            .Where(part => part.isDrum)
            .Select(part => part.partId));

        generatedEnabledPartIds = enabledIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (generatedEnabledPartIds.Count == 0 && availableParts.Count > 0)
            generatedEnabledPartIds.Add(availableParts[0].partId);
    }

    private void NormalizeGeneratedPlaybackSelectionToAvailableParts(List<GeneratedPlaybackPartInfo> availableParts = null)
    {
        availableParts ??= GetAvailableGeneratedPlaybackParts();
        List<string> availableIds = availableParts
            .Select(part => part.partId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (useAllGeneratedPlaybackParts)
        {
            generatedEnabledPartIds = new List<string>();
            return;
        }

        generatedEnabledPartIds = (generatedEnabledPartIds ?? new List<string>())
            .Where(id => availableIds.Contains(id, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void SaveGeneratedPlaybackSelectionOverrideForCurrentTrack()
    {
        if (songMetadata == null)
            return;

        if (songMetadata.generatedPlaybackSelectionOverrides == null)
            songMetadata.generatedPlaybackSelectionOverrides = new List<GeneratedPlaybackSelectionOverride>();

        List<GeneratedPlaybackPartInfo> availableParts = GetAvailableGeneratedPlaybackParts();
        NormalizeGeneratedPlaybackSelectionToAvailableParts(availableParts);

        string selectionKey = GetCurrentGeneratedPlaybackSelectionKey();
        if (string.IsNullOrWhiteSpace(selectionKey))
            return;

        List<string> defaultEnabledIds = new List<string>();
        if (!string.IsNullOrWhiteSpace(selectionKey))
        {
            defaultEnabledIds.AddRange(availableParts
                .Where(part => string.Equals(part.partId, selectionKey, StringComparison.OrdinalIgnoreCase))
                .Select(part => part.partId));
        }

        defaultEnabledIds.AddRange(availableParts.Where(part => part.isDrum).Select(part => part.partId));
        defaultEnabledIds = defaultEnabledIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        bool matchesDefault = !useAllGeneratedPlaybackParts &&
                              generatedEnabledPartIds.Count == defaultEnabledIds.Count &&
                              generatedEnabledPartIds.All(id => defaultEnabledIds.Contains(id, StringComparer.OrdinalIgnoreCase));

        songMetadata.generatedPlaybackSelectionOverrides.RemoveAll(entry =>
            string.Equals(entry.partId ?? string.Empty, selectionKey, StringComparison.OrdinalIgnoreCase));

        if (matchesDefault)
            return;

        songMetadata.generatedPlaybackSelectionOverrides.Add(new GeneratedPlaybackSelectionOverride
        {
            partId = selectionKey,
            useAllGeneratedPlaybackParts = useAllGeneratedPlaybackParts,
            generatedEnabledPartIds = useAllGeneratedPlaybackParts
                ? new List<string>()
                : generatedEnabledPartIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        });
    }

    private List<GeneratedPlaybackPartInfo> GetAvailableGeneratedPlaybackParts()
    {
        return generatedPlaybackSourceArrangement != null && generatedPlaybackSourceArrangement.parts != null
            ? generatedPlaybackSourceArrangement.parts
            : new List<GeneratedPlaybackPartInfo>();
    }

    private bool IsGeneratedPlaybackPartEnabled(string partId)
    {
        if (string.IsNullOrWhiteSpace(partId))
            return false;

        if (useAllGeneratedPlaybackParts)
            return true;

        return generatedEnabledPartIds != null &&
               generatedEnabledPartIds.Any(id => string.Equals(id, partId, StringComparison.OrdinalIgnoreCase));
    }

    private string GetGeneratedPlaybackTrackSelectionSummary()
    {
        List<GeneratedPlaybackPartInfo> availableParts = GetAvailableGeneratedPlaybackParts();
        if (availableParts.Count == 0)
            return "No MusicXML tracks";

        if (useAllGeneratedPlaybackParts)
            return "All tracks";

        int enabledCount = availableParts.Count(part => IsGeneratedPlaybackPartEnabled(part.partId));
        if (enabledCount <= 0)
            return "No tracks";
        if (enabledCount == 1)
            return "1 track";

        return $"{enabledCount} tracks";
    }

    private void SetGeneratedPlaybackPartEnabled(string partId, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(partId))
            return;

        List<GeneratedPlaybackPartInfo> availableParts = GetAvailableGeneratedPlaybackParts();
        if (availableParts.Count == 0)
            return;

        if (useAllGeneratedPlaybackParts)
        {
            generatedEnabledPartIds = availableParts.Select(part => part.partId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            useAllGeneratedPlaybackParts = false;
        }

        bool currentlyEnabled = IsGeneratedPlaybackPartEnabled(partId);
        if (enabled == currentlyEnabled)
            return;

        if (enabled)
        {
            if (generatedEnabledPartIds == null)
                generatedEnabledPartIds = new List<string>();

            if (!generatedEnabledPartIds.Any(id => string.Equals(id, partId, StringComparison.OrdinalIgnoreCase)))
                generatedEnabledPartIds.Add(partId);
        }
        else if (generatedEnabledPartIds != null)
        {
            generatedEnabledPartIds.RemoveAll(id => string.Equals(id, partId, StringComparison.OrdinalIgnoreCase));
        }

        List<string> availableIds = availableParts.Select(part => part.partId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        generatedEnabledPartIds = generatedEnabledPartIds
            .Where(id => availableIds.Contains(id, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (generatedEnabledPartIds.Count == availableIds.Count)
        {
            useAllGeneratedPlaybackParts = true;
            generatedEnabledPartIds.Clear();
        }

        SaveGeneratedPlaybackSelectionOverrideForCurrentTrack();
        SaveSongMetadata();
        ApplyGeneratedPlaybackSelection();
        SyncAudioToSongTimer(playImmediately: ShouldPlaybackAudio(showLoopSettings && loopSettingsPreviewPlaying, showOffsetHelper && offsetHelperAdjusting && offsetHelperPreviewPlaying, loopRestartPauseRemainingSeconds > 0.0001f), forceSeek: true);
    }

    public void SelectAllGeneratedPlaybackPartsFromUi()
    {
        useAllGeneratedPlaybackParts = true;
        generatedEnabledPartIds.Clear();
        SaveGeneratedPlaybackSelectionOverrideForCurrentTrack();
        SaveSongMetadata();
        ApplyGeneratedPlaybackSelection();
        SyncAudioToSongTimer(playImmediately: ShouldPlaybackAudio(showLoopSettings && loopSettingsPreviewPlaying, showOffsetHelper && offsetHelperAdjusting && offsetHelperPreviewPlaying, loopRestartPauseRemainingSeconds > 0.0001f), forceSeek: true);
    }

    public void DeselectAllGeneratedPlaybackPartsFromUi()
    {
        useAllGeneratedPlaybackParts = false;
        generatedEnabledPartIds.Clear();
        SaveGeneratedPlaybackSelectionOverrideForCurrentTrack();
        SaveSongMetadata();
        ApplyGeneratedPlaybackSelection();
        SyncAudioToSongTimer(playImmediately: ShouldPlaybackAudio(showLoopSettings && loopSettingsPreviewPlaying, showOffsetHelper && offsetHelperAdjusting && offsetHelperPreviewPlaying, loopRestartPauseRemainingSeconds > 0.0001f), forceSeek: true);
    }

    public void OpenGeneratedAudioTrackSelectionFromUi()
    {
        if (GetEffectiveSongPlaybackAudioMode() != SongPlaybackAudioMode.Generated || GetAvailableGeneratedPlaybackParts().Count == 0)
            return;

        showGeneratedAudioTrackSelectionPopup = true;
        selectedGeneratedAudioTrackSelectionIndex = Mathf.Clamp(selectedGeneratedAudioTrackSelectionIndex, 0, Mathf.Max(0, GetAvailableGeneratedPlaybackParts().Count + 2));
    }

    public void CloseGeneratedAudioTrackSelectionFromUi()
    {
        showGeneratedAudioTrackSelectionPopup = false;
    }

    public void MoveGeneratedAudioTrackSelectionFromUi(int delta)
    {
        int optionCount = GetAvailableGeneratedPlaybackParts().Count + 3;
        if (!showGeneratedAudioTrackSelectionPopup || optionCount <= 0)
            return;

        selectedGeneratedAudioTrackSelectionIndex = (selectedGeneratedAudioTrackSelectionIndex + delta + optionCount) % optionCount;
    }

    public void ActivateSelectedGeneratedAudioTrackSelectionFromUi()
    {
        if (!showGeneratedAudioTrackSelectionPopup)
            return;

        if (selectedGeneratedAudioTrackSelectionIndex == 0)
        {
            SelectAllGeneratedPlaybackPartsFromUi();
            return;
        }

        if (selectedGeneratedAudioTrackSelectionIndex == 1)
        {
            DeselectAllGeneratedPlaybackPartsFromUi();
            return;
        }

        if (selectedGeneratedAudioTrackSelectionIndex == 2)
        {
            CloseGeneratedAudioTrackSelectionFromUi();
            return;
        }

        int partIndex = selectedGeneratedAudioTrackSelectionIndex - 3;
        List<GeneratedPlaybackPartInfo> availableParts = GetAvailableGeneratedPlaybackParts();
        if (partIndex < 0 || partIndex >= availableParts.Count)
            return;

        GeneratedPlaybackPartInfo part = availableParts[partIndex];
        SetGeneratedPlaybackPartEnabled(part.partId, !IsGeneratedPlaybackPartEnabled(part.partId));
    }

    public void SetSelectedGeneratedAudioTrackSelectionIndexFromUi(int index)
    {
        int maxIndex = Mathf.Max(0, GetAvailableGeneratedPlaybackParts().Count + 2);
        selectedGeneratedAudioTrackSelectionIndex = Mathf.Clamp(index, 0, maxIndex);
    }

    private static string GetSongPlaybackAudioModeLabel(SongPlaybackAudioMode mode)
    {
        switch (mode)
        {
            case SongPlaybackAudioMode.Mp3:
                return "MP3";
            case SongPlaybackAudioMode.Muted:
                return "Muted";
            default:
                return "Generated";
        }
    }

    private SongPlaybackAudioMode GetEffectiveSongPlaybackAudioMode()
    {
        if (songPlaybackAudioMode == SongPlaybackAudioMode.Generated)
        {
            if (IsGeneratedPlaybackAvailable())
                return SongPlaybackAudioMode.Generated;

            return hasBackingTrack ? SongPlaybackAudioMode.Mp3 : SongPlaybackAudioMode.Muted;
        }

        return songPlaybackAudioMode;
    }

    private string GetCurrentSongPlaybackAudioModeLabel()
    {
        SongPlaybackAudioMode effectiveMode = GetEffectiveSongPlaybackAudioMode();
        if (effectiveMode != songPlaybackAudioMode)
            return $"{GetSongPlaybackAudioModeLabel(songPlaybackAudioMode)} (Fallback {GetSongPlaybackAudioModeLabel(effectiveMode)})";

        return GetSongPlaybackAudioModeLabel(songPlaybackAudioMode);
    }

    private static string ResolveSongMetadataFileName(SongLibraryEntry entry)
    {
        if (entry == null)
            return "song";

        if (!string.IsNullOrWhiteSpace(entry.Mp3Path))
            return Path.GetFileName(entry.Mp3Path);

        if (!string.IsNullOrWhiteSpace(entry.PrimaryNotationPath))
            return Path.GetFileName(entry.PrimaryNotationPath);

        if (!string.IsNullOrWhiteSpace(entry.XmlPath))
            return Path.GetFileName(entry.XmlPath);

        if (!string.IsNullOrWhiteSpace(entry.SongId))
            return entry.SongId;

        return "song";
    }

    private void InitializeSongMetadataAndAudio()
    {
        EnsureBackingTrackSource();

        if (currentSongEntry == null)
        {
            generatedPlaybackSourceArrangement = null;
            generatedPlaybackArrangement = null;
            generatedSongPlayer?.ClearArrangement();
            showGeneratedAudioTrackSelectionPopup = false;
            hasBackingTrack = false;
            backingTrackLoadError = "No runtime song selected.";
            currentSongBestScorePercent = 0f;
            currentTrackBestScorePercent = 0f;
            currentTrackHeroBestScorePercent = 0f;
            currentTrackHeroBestHeartsRemaining = 0;
            currentTrackHeroBestHeartsTotal = 0;
            Debug.LogWarning(backingTrackLoadError);
            return;
        }

        string songPath = currentSongEntry.Mp3Path;
        currentSongFileName = ResolveSongMetadataFileName(currentSongEntry);

        songMetadata = LoadSongMetadata(currentSongFileName);
        globalAudioOffsetMs = songMetadata.audioOffsetMs;
        audioOffsetMs = globalAudioOffsetMs;
        tabSpeedOffsetPercent = Mathf.Clamp(songMetadata.tabSpeedOffsetPercent <= 0f ? 100f : songMetadata.tabSpeedOffsetPercent, 50f, 150f);
        songStartDelaySeconds = Mathf.Clamp(songMetadata.songStartDelaySeconds <= 0f ? defaultSongStartDelaySeconds : songMetadata.songStartDelaySeconds, 0f, 8f);
        songVolumePercent = Mathf.Clamp(songMetadata.songVolumePercent, 0f, 100f);
        songPlaybackAudioMode = songMetadata.playbackAudioMode;
        loopPauseDurationSeconds = Mathf.Clamp(songMetadata.loopPauseDurationSeconds, 0f, 8f);
        if (songMetadata.hasSavedLoopWindow)
        {
            loopStartTime = Mathf.Max(0f, songMetadata.loopStartTime);
            loopEndTime = Mathf.Max(loopStartTime + 0.05f, songMetadata.loopEndTime);
        }
        useAutoTrackSelection = songMetadata.useAutoTrackSelection;
        selectedMusicXmlPartId = string.IsNullOrEmpty(songMetadata.selectedMusicXmlPartId) ? string.Empty : songMetadata.selectedMusicXmlPartId;
        useAllGeneratedPlaybackParts = songMetadata.useAllGeneratedPlaybackParts;
        generatedEnabledPartIds = songMetadata.generatedEnabledPartIds != null
            ? songMetadata.generatedEnabledPartIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();
        showGeneratedAudioTrackSelectionPopup = false;
        selectedGeneratedAudioTrackSelectionIndex = 0;
        currentSongBestScorePercent = Mathf.Clamp(GetHighestTrackScore(songMetadata), 0f, 100f);
        currentTrackBestScorePercent = Mathf.Clamp(GetStoredTrackScore(songMetadata, selectedMusicXmlPartId), 0f, 100f);
        HeroScoreSummary currentHeroTrackBest = GetStoredHeroTrackScoreSummary(songMetadata, selectedMusicXmlPartId);
        currentTrackHeroBestScorePercent = currentHeroTrackBest.percent;
        currentTrackHeroBestHeartsRemaining = currentHeroTrackBest.heartsRemaining;
        currentTrackHeroBestHeartsTotal = currentHeroTrackBest.heartsTotal;
        RefreshEffectiveAudioOffset();
        LoadGeneratedPlaybackArrangementForCurrentSong();
        generatedSongPlayer?.SetMasterVolumePercent(songVolumePercent);

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
        if (entry != null)
        {
            if (!string.IsNullOrEmpty(entry.MetadataPath))
                return entry.MetadataPath;

            if (!string.IsNullOrEmpty(entry.SongDirectory))
                return Path.Combine(entry.SongDirectory, ExternalContentPaths.SongMetadataFileName);

            if (!string.IsNullOrEmpty(entry.Mp3Path))
            {
                string songDirectory = Path.GetDirectoryName(entry.Mp3Path);
                if (!string.IsNullOrEmpty(songDirectory))
                    return Path.Combine(songDirectory, ExternalContentPaths.SongMetadataFileName);
            }
        }

        string fallbackFileName = entry != null
            ? ResolveSongMetadataFileName(entry)
            : "song";
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

    private static HeroScoreSummary GetHighestHeroTrackScoreSummary(SongMetadata metadata)
    {
        if (metadata == null || metadata.trackScores == null || metadata.trackScores.Count == 0)
            return default;

        HeroScoreSummary best = default;
        for (int i = 0; i < metadata.trackScores.Count; i++)
        {
            TrackScoreEntry entry = metadata.trackScores[i];
            HeroScoreSummary candidate = new HeroScoreSummary(entry.heroBestScorePercent, entry.heroBestHeartsRemaining, entry.heroBestHeartsTotal);
            if (ShouldReplaceHeroBest(best, candidate.percent, candidate.heartsRemaining, candidate.heartsTotal))
                best = candidate;
        }

        return best;
    }

    private static float GetStoredTrackScore(SongMetadata metadata, string partId)
    {
        if (metadata == null || metadata.trackScores == null || string.IsNullOrEmpty(partId))
            return 0f;

        TrackScoreEntry entry = metadata.trackScores.FirstOrDefault(score => string.Equals(score.partId ?? string.Empty, partId, StringComparison.OrdinalIgnoreCase));
        return entry != null ? Mathf.Clamp(entry.bestScorePercent, 0f, 100f) : 0f;
    }

    private static HeroScoreSummary GetStoredHeroTrackScoreSummary(SongMetadata metadata, string partId)
    {
        if (metadata == null || metadata.trackScores == null || string.IsNullOrEmpty(partId))
            return default;

        TrackScoreEntry entry = metadata.trackScores.FirstOrDefault(score => string.Equals(score.partId ?? string.Empty, partId, StringComparison.OrdinalIgnoreCase));
        return entry != null
            ? new HeroScoreSummary(entry.heroBestScorePercent, entry.heroBestHeartsRemaining, entry.heroBestHeartsTotal)
            : default;
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

    private static bool ShouldReplaceHeroBest(HeroScoreSummary existing, float percent, int heartsRemaining, int heartsTotal)
    {
        HeroScoreSummary candidate = new HeroScoreSummary(percent, heartsRemaining, heartsTotal);
        if (!candidate.IsAvailable)
            return false;

        if (!existing.IsAvailable)
            return true;

        if (candidate.percent > existing.percent + 0.01f)
            return true;

        if (candidate.percent < existing.percent - 0.01f)
            return false;

        if (candidate.heartsRemaining > existing.heartsRemaining)
            return true;

        if (candidate.heartsRemaining < existing.heartsRemaining)
            return false;

        return candidate.heartsTotal > existing.heartsTotal;
    }

    private static void UpsertHeroTrackScore(SongMetadata metadata, string partId, string displayName, float percent, int heartsRemaining, int heartsTotal)
    {
        if (metadata == null || string.IsNullOrEmpty(partId))
            return;

        if (metadata.trackScores == null)
            metadata.trackScores = new List<TrackScoreEntry>();

        TrackScoreEntry existing = metadata.trackScores.FirstOrDefault(score => string.Equals(score.partId ?? string.Empty, partId, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            existing = new TrackScoreEntry
            {
                partId = partId,
                displayName = displayName
            };
            metadata.trackScores.Add(existing);
        }

        HeroScoreSummary existingSummary = new HeroScoreSummary(existing.heroBestScorePercent, existing.heroBestHeartsRemaining, existing.heroBestHeartsTotal);
        if (!ShouldReplaceHeroBest(existingSummary, percent, heartsRemaining, heartsTotal))
            return;

        existing.displayName = string.IsNullOrEmpty(displayName) ? existing.displayName : displayName;
        existing.heroBestScorePercent = Mathf.Clamp(percent, 0f, 100f);
        existing.heroBestHeartsRemaining = Mathf.Max(0, heartsRemaining);
        existing.heroBestHeartsTotal = Mathf.Max(0, heartsTotal);
    }

    private static string FormatHeroScoreText(HeroScoreSummary heroScore)
    {
        if (!heroScore.IsAvailable)
            return string.Empty;

        return FormattableString.Invariant($"H {heroScore.percent:F1}% ({heroScore.heartsRemaining}/{heroScore.heartsTotal})");
    }

    private static string BuildCombinedScoreText(float normalPercent, HeroScoreSummary heroScore)
    {
        string normalText = FormattableString.Invariant($"{Mathf.Clamp(normalPercent, 0f, 100f):F1}%");
        if (!heroScore.IsAvailable)
            return normalText;

        string heroText = FormatHeroScoreText(heroScore);
        return normalPercent > 0.01f
            ? $"{normalText}  |  {heroText}"
            : heroText;
    }

    private SongMetadata LoadSongMetadataForEntry(SongLibraryEntry entry)
    {
        if (entry == null)
            return new SongMetadata();

        string fileName = ResolveSongMetadataFileName(entry);
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

    private List<MusicXmlLoader.MusicXmlPartSummary> GetPartSummariesWithFallback(SongLibraryEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.PrimaryNotationPath))
            return new List<MusicXmlLoader.MusicXmlPartSummary>();

        List<MusicXmlLoader.MusicXmlPartSummary> summaries =
            SongNotationFacade.GetPartSummaries(entry.PrimaryNotationPath, entry.PrimaryNotationKind) ??
            new List<MusicXmlLoader.MusicXmlPartSummary>();
        if (summaries.Count > 0 ||
            entry.PrimaryNotationKind == SongNotationSourceKind.MusicXml ||
            string.IsNullOrWhiteSpace(entry.XmlPath) ||
            !File.Exists(entry.XmlPath))
        {
            return summaries;
        }

        List<MusicXmlLoader.MusicXmlPartSummary> xmlFallbackSummaries = MusicXmlLoader.GetPartSummaries(entry.XmlPath);
        if (xmlFallbackSummaries != null && xmlFallbackSummaries.Count > 0)
        {
            Debug.LogWarning(
                $"[GuitarBridgeServer] Primary notation '{entry.PrimaryNotationPath}' returned no arrangement summaries. " +
                $"Falling back to MusicXML summaries from '{entry.XmlPath}'.");
            return xmlFallbackSummaries;
        }

        return summaries;
    }

    private List<MusicXmlLoader.MusicXmlPartSummary> GetSortedTrackSummaries(SongLibraryEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.PrimaryNotationPath))
            return new List<MusicXmlLoader.MusicXmlPartSummary>();

        long notationTicks = File.Exists(entry.PrimaryNotationPath) ? File.GetLastWriteTimeUtc(entry.PrimaryNotationPath).Ticks : -1L;
        List<MusicXmlLoader.MusicXmlPartSummary> summaries;
        if (cachedTrackSummariesByNotationPath.TryGetValue(entry.PrimaryNotationPath, out List<MusicXmlLoader.MusicXmlPartSummary> cachedSummaries) &&
            cachedTrackSummaryTicksByNotationPath.TryGetValue(entry.PrimaryNotationPath, out long cachedTicks) &&
            cachedTicks == notationTicks &&
            cachedSummaries != null)
        {
            summaries = cachedSummaries
                .Select(summary => new MusicXmlLoader.MusicXmlPartSummary
                {
                    Index = summary.Index,
                    PartId = summary.PartId,
                    Name = summary.Name,
                    NoteCount = summary.NoteCount,
                    TabCount = summary.TabCount,
                    Score = summary.Score
                })
                .ToList();
        }
        else
        {
            summaries = GetPartSummariesWithFallback(entry);
            cachedTrackSummariesByNotationPath[entry.PrimaryNotationPath] = summaries
                .Select(summary => new MusicXmlLoader.MusicXmlPartSummary
                {
                    Index = summary.Index,
                    PartId = summary.PartId,
                    Name = summary.Name,
                    NoteCount = summary.NoteCount,
                    TabCount = summary.TabCount,
                    Score = summary.Score
                })
                .ToList();
            cachedTrackSummaryTicksByNotationPath[entry.PrimaryNotationPath] = notationTicks;
        }

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

    private void LoadHeroModePreferences()
    {
        heroModeEnabled = PlayerPrefs.GetInt(HeroModeEnabledPrefsKey, 0) != 0;
        heroModeHeartCount = Mathf.Clamp(PlayerPrefs.GetInt(HeroModeHeartCountPrefsKey, 5), 1, 30);
    }

    private void SaveHeroModePreferences()
    {
        PlayerPrefs.SetInt(HeroModeEnabledPrefsKey, heroModeEnabled ? 1 : 0);
        PlayerPrefs.SetInt(HeroModeHeartCountPrefsKey, Mathf.Clamp(heroModeHeartCount, 1, 30));
        PlayerPrefs.Save();
    }

    private void LoadNativeDetectorPreferences()
    {
        selectedNativeNotesDetectorInputDeviceIndex = PlayerPrefs.GetInt(NativeDetectorInputDevicePrefsKey, -1);
    }

    private void SaveNativeDetectorPreferences()
    {
        PlayerPrefs.SetInt(NativeDetectorInputDevicePrefsKey, selectedNativeNotesDetectorInputDeviceIndex);
        PlayerPrefs.Save();
    }

    private void ResetSessionScoreState(bool ignoreCurrentlyResolvedNotes = false)
    {
        sessionScoredNoteIds.Clear();
        sessionScoreHits = 0;
        sessionScoreMisses = 0;
        currentSessionScorePercent = 0f;

        if (!ignoreCurrentlyResolvedNotes || noteStates == null)
            return;

        for (int i = 0; i < noteStates.Count; i++)
        {
            GameplayNoteState noteState = noteStates[i];
            if (noteState == null || !noteState.IsResolved)
                continue;

            int noteKey = noteState.data.id >= 0 ? noteState.data.id : i;
            sessionScoredNoteIds.Add(noteKey);
        }
    }

    private void UpdateSessionScoreState()
    {
        if (loopEnabled || noteStates == null || noteStates.Count == 0)
            return;

        for (int i = 0; i < noteStates.Count; i++)
        {
            GameplayNoteState noteState = noteStates[i];
            if (noteState == null || !noteState.IsResolved)
                continue;

            int noteKey = noteState.data.id >= 0 ? noteState.data.id : i;
            if (!sessionScoredNoteIds.Add(noteKey))
                continue;

            if (noteState.IsHit)
                sessionScoreHits++;
            else if (noteState.IsMissed)
                sessionScoreMisses++;
        }

        int total = noteStates.Count;
        currentSessionScorePercent = total > 0
            ? Mathf.Clamp(100f * sessionScoreHits / total, 0f, 100f)
            : 0f;
    }

    private void UpdateAndPersistSongBestScore()
    {
        if (scoreSaveInvalidated || loopEnabled || currentSongEntry == null || noteStates == null || noteStates.Count == 0 || string.IsNullOrEmpty(selectedMusicXmlPartId))
            return;

        string trackName = GetTrackDisplayName(GetCurrentTrackOptionIndex());
        float percent = Mathf.Clamp(currentSessionScorePercent, 0f, 100f);
        if (heroModeEnabled)
        {
            int heartsRemaining = GetCurrentHeroHeartsRemaining();
            HeroScoreSummary currentHeroBest = GetStoredHeroTrackScoreSummary(songMetadata, selectedMusicXmlPartId);
            if (!ShouldReplaceHeroBest(currentHeroBest, percent, heartsRemaining, heroModeHeartCount))
                return;

            currentTrackHeroBestScorePercent = percent;
            currentTrackHeroBestHeartsRemaining = heartsRemaining;
            currentTrackHeroBestHeartsTotal = heroModeHeartCount;
            UpsertHeroTrackScore(songMetadata, selectedMusicXmlPartId, trackName, percent, heartsRemaining, heroModeHeartCount);
            currentSongBestScorePercent = Mathf.Clamp(GetHighestTrackScore(songMetadata), 0f, 100f);
            SaveSongMetadata();
            return;
        }

        if (percent <= currentTrackBestScorePercent + 0.01f)
            return;

        currentTrackBestScorePercent = percent;
        UpsertTrackScore(songMetadata, selectedMusicXmlPartId, trackName, percent);
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
        if (!string.IsNullOrEmpty(metadataPath))
        {
            long lastWriteTicks = File.Exists(metadataPath) ? File.GetLastWriteTimeUtc(metadataPath).Ticks : -1L;
            if (cachedSongMetadataByPath.TryGetValue(metadataPath, out SongMetadata cachedMetadata) &&
                cachedSongMetadataTicksByPath.TryGetValue(metadataPath, out long cachedTicks) &&
                cachedTicks == lastWriteTicks &&
                cachedMetadata != null)
            {
                return CloneSongMetadata(cachedMetadata);
            }
        }

        SongMetadata data = new SongMetadata
        {
            songFileName = songFileName,
            audioOffsetMs = 0f,
            tabSpeedOffsetPercent = 100f,
            songStartDelaySeconds = defaultSongStartDelaySeconds,
            songVolumePercent = 100f,
            playbackAudioMode = SongPlaybackAudioMode.Generated,
            hasSavedLoopWindow = false,
            loopStartTime = 0f,
            loopEndTime = 0f,
            loopPauseDurationSeconds = 0f,
            useAutoTrackSelection = true,
            selectedMusicXmlPartId = string.Empty,
            useAllGeneratedPlaybackParts = true,
            generatedEnabledPartIds = new List<string>(),
            generatedPlaybackSelectionOverrides = new List<GeneratedPlaybackSelectionOverride>(),
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
                if (data.generatedEnabledPartIds == null)
                    data.generatedEnabledPartIds = new List<string>();
                if (data.generatedPlaybackSelectionOverrides == null)
                    data.generatedPlaybackSelectionOverrides = new List<GeneratedPlaybackSelectionOverride>();
                if (!Enum.IsDefined(typeof(SongPlaybackAudioMode), data.playbackAudioMode))
                    data.playbackAudioMode = SongPlaybackAudioMode.Generated;
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

        if (!string.IsNullOrEmpty(metadataPath))
        {
            cachedSongMetadataByPath[metadataPath] = CloneSongMetadata(data);
            cachedSongMetadataTicksByPath[metadataPath] = File.Exists(metadataPath) ? File.GetLastWriteTimeUtc(metadataPath).Ticks : -1L;
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
        songMetadata.songVolumePercent = songVolumePercent;
        songMetadata.playbackAudioMode = songPlaybackAudioMode;
        songMetadata.hasSavedLoopWindow = loopEndTime > loopStartTime + 0.01f;
        songMetadata.loopStartTime = loopStartTime;
        songMetadata.loopEndTime = loopEndTime;
        songMetadata.loopPauseDurationSeconds = loopPauseDurationSeconds;
        songMetadata.useAutoTrackSelection = useAutoTrackSelection;
        songMetadata.selectedMusicXmlPartId = selectedMusicXmlPartId;
        songMetadata.useAllGeneratedPlaybackParts = useAllGeneratedPlaybackParts;
        songMetadata.generatedEnabledPartIds = useAllGeneratedPlaybackParts
            ? new List<string>()
            : generatedEnabledPartIds != null
            ? generatedEnabledPartIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();
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

        cachedSongMetadataByPath[metadataPath] = CloneSongMetadata(metadata);
        cachedSongMetadataTicksByPath[metadataPath] = File.Exists(metadataPath) ? File.GetLastWriteTimeUtc(metadataPath).Ticks : -1L;
    }

    private static SongMetadata CloneSongMetadata(SongMetadata source)
    {
        if (source == null)
            return new SongMetadata();

        return new SongMetadata
        {
            songFileName = source.songFileName,
            audioOffsetMs = source.audioOffsetMs,
            tabSpeedOffsetPercent = source.tabSpeedOffsetPercent,
            songStartDelaySeconds = source.songStartDelaySeconds,
            songVolumePercent = source.songVolumePercent,
            playbackAudioMode = source.playbackAudioMode,
            hasSavedLoopWindow = source.hasSavedLoopWindow,
            loopStartTime = source.loopStartTime,
            loopEndTime = source.loopEndTime,
            loopPauseDurationSeconds = source.loopPauseDurationSeconds,
            useAutoTrackSelection = source.useAutoTrackSelection,
            selectedMusicXmlPartId = source.selectedMusicXmlPartId,
            useAllGeneratedPlaybackParts = source.useAllGeneratedPlaybackParts,
            generatedEnabledPartIds = source.generatedEnabledPartIds != null
                ? source.generatedEnabledPartIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>(),
            generatedPlaybackSelectionOverrides = source.generatedPlaybackSelectionOverrides != null
                ? source.generatedPlaybackSelectionOverrides
                    .Where(entry => entry != null)
                    .Select(entry => new GeneratedPlaybackSelectionOverride
                    {
                        partId = entry.partId,
                        useAllGeneratedPlaybackParts = entry.useAllGeneratedPlaybackParts,
                        generatedEnabledPartIds = entry.generatedEnabledPartIds != null
                            ? entry.generatedEnabledPartIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                            : new List<string>()
                    }).ToList()
                : new List<GeneratedPlaybackSelectionOverride>(),
            bestScorePercent = source.bestScorePercent,
            trackScores = source.trackScores != null
                ? source.trackScores.Select(score => new TrackScoreEntry
                {
                    partId = score.partId,
                    displayName = score.displayName,
                    bestScorePercent = score.bestScorePercent,
                    heroBestScorePercent = score.heroBestScorePercent,
                    heroBestHeartsRemaining = score.heroBestHeartsRemaining,
                    heroBestHeartsTotal = score.heroBestHeartsTotal
                }).ToList()
                : new List<TrackScoreEntry>(),
            trackOffsetOverrides = source.trackOffsetOverrides != null
                ? source.trackOffsetOverrides.Select(entry => new TrackOffsetOverride
                {
                    partId = entry.partId,
                    useTrackOffset = entry.useTrackOffset,
                    offsetMs = entry.offsetMs
                }).ToList()
                : new List<TrackOffsetOverride>()
        };
    }

    private string GetMetadataPath(string songFileName)
    {
        if (currentSongEntry != null)
        {
            if (!string.IsNullOrEmpty(currentSongEntry.MetadataPath))
                return currentSongEntry.MetadataPath;

            if (!string.IsNullOrEmpty(currentSongEntry.SongDirectory))
                return Path.Combine(currentSongEntry.SongDirectory, ExternalContentPaths.SongMetadataFileName);

            if (!string.IsNullOrEmpty(currentSongEntry.Mp3Path))
            {
                string songDirectory = Path.GetDirectoryName(currentSongEntry.Mp3Path);
                if (!string.IsNullOrEmpty(songDirectory))
                    return Path.Combine(songDirectory, ExternalContentPaths.SongMetadataFileName);
            }
        }

        string safeName = Regex.Replace(Path.GetFileNameWithoutExtension(songFileName), "[^a-zA-Z0-9_-]", "_");
        return Path.Combine(ExternalContentPaths.PersistentSongsDirectory, safeName, ExternalContentPaths.SongMetadataFileName);
    }

    private void RegisterRuntimeSettings()
    {
        runtimeSettingDefinitions.Clear();
        runtimeSettingById.Clear();
        runtimeSettingDefaultValues.Clear();
        runtimeSettingsSnapshotDirty = true;

        RegisterFloatSetting("core.noteSpeed", "Settings", "Note Speed", "Controls how quickly notes travel toward the hit line. This also controls the visible distance between notes.", 4f, 30f, 0.1f, () => noteSpeed, v => noteSpeed = v);
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

        RegisterEnumSetting(
            "fx.characterDisplay",
            "Visuals",
            "Character Display",
            "Controls when the Highway3D character is shown. Hearts remain hero-mode only.",
            HighwayCharacterDisplayModeOptions,
            () => SerializeHighwayCharacterDisplayMode(highwayCharacterDisplayMode),
            value => highwayCharacterDisplayMode = ParseHighwayCharacterDisplayMode(value));
        RegisterFloatSetting("fx.judgeableDarkenMultiplier", "Visuals", "Judgeable Darken", "Darkens upcoming notes until they enter the hit window.", 1f, 8f, 0.1f, () => judgeableDarkenMultiplier, v => judgeableDarkenMultiplier = v);
        RegisterFloatSetting("fx.tabIdleFillDarken", "Colors - Status", "Idle Fill Darken", "Controls how muted unresolved tab notes appear.", 0f, 1f, 0.01f, () => tabIdleFillDarken, v => tabIdleFillDarken = v);

        RegisterEnumSetting("bg.mode", "Background", "Background Mode", "Switches between static and animated backgrounds.", new []{"SolidColor","Starfield","BlueSky","Space"}, () => tabBackgroundMode.ToString(), v => { if (Enum.TryParse(v, out TabsBackgroundMode mode)) tabBackgroundMode = mode; });
        RegisterEnumSetting("bg.skyMood", "Background - Blue Sky", "Sky Mood", "Switches BlueSky mood grading between daytime, sunset, and midnight palettes.", new []{"Day","Sunset","Midnight"}, () => tabSkyMood.ToString(), v => { if (Enum.TryParse(v, out TabsSkyMood mood)) tabSkyMood = mood; });
        RegisterBoolSetting("bg.skyUseStage", "Background - Blue Sky", "Use Stage Backdrop", "Switches BlueSky mode between the sunset-cloud scene and a stylized stage backdrop.", () => tabSkyUseStageBackdrop, v => tabSkyUseStageBackdrop = v);
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
        RegisterFloatSetting("highway.fretSpacing", "Highway 3D - Layout", "Fret Spacing", "Horizontal spacing between fret lanes in Highway3D.", 0.4f, 6f, 0.01f, () => FretSpacing, v => FretSpacing = v);
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

    private static string SerializeHighwayCharacterDisplayMode(HighwayCharacterDisplayMode mode)
    {
        switch (mode)
        {
            case HighwayCharacterDisplayMode.Never:
                return "Never";
            case HighwayCharacterDisplayMode.HeroModeOnly:
                return "Only In Hero Mode";
            default:
                return "Always";
        }
    }

    private static HighwayCharacterDisplayMode ParseHighwayCharacterDisplayMode(string value)
    {
        if (string.Equals(value, "Never", StringComparison.OrdinalIgnoreCase))
            return HighwayCharacterDisplayMode.Never;

        if (string.Equals(value, "Only In Hero Mode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "HeroModeOnly", StringComparison.OrdinalIgnoreCase))
            return HighwayCharacterDisplayMode.HeroModeOnly;

        return HighwayCharacterDisplayMode.Always;
    }

    private bool ShouldDisplayHighwayCharacter(bool heroModeActive)
    {
        switch (highwayCharacterDisplayMode)
        {
            case HighwayCharacterDisplayMode.Never:
                return false;
            case HighwayCharacterDisplayMode.HeroModeOnly:
                return heroModeActive;
            default:
                return true;
        }
    }

    private void MoveGlobalSettingsItemSelection(int delta)
    {
        List<RuntimeSettingSnapshot> settings = GetActiveGlobalSettingsItems();
        if (settings.Count == 0)
        {
            selectedGlobalSettingsItemIndex = 0;
            return;
        }

        selectedGlobalSettingsItemIndex = (selectedGlobalSettingsItemIndex + delta + settings.Count) % settings.Count;
    }

    private void ActivateCurrentGlobalSettingsSelection()
    {
        if (string.IsNullOrEmpty(activeGlobalSettingsCategory))
        {
            if (selectedGlobalSettingsTopIndex >= 2 && selectedGlobalSettingsTopIndex <= 5)
            {
                activeGlobalSettingsCategory = GetGlobalSettingsCategoryFromTopIndex(selectedGlobalSettingsTopIndex);
                selectedGlobalSettingsItemIndex = 0;
                return;
            }

            if (selectedGlobalSettingsTopIndex == 6)
                ResetGlobalSettingsToDefaultsFromUi();

            return;
        }

        List<RuntimeSettingSnapshot> settings = GetActiveGlobalSettingsItems();
        if (settings.Count == 0)
            return;

        RuntimeSettingSnapshot setting = settings[Mathf.Clamp(selectedGlobalSettingsItemIndex, 0, settings.Count - 1)];
        if (setting == null)
            return;

        if (string.Equals(setting.valueType, "bool", StringComparison.OrdinalIgnoreCase))
        {
            string nextValue = string.Equals(setting.value, "true", StringComparison.OrdinalIgnoreCase) ? "false" : "true";
            ApplyRuntimeSettingValue(setting.id, nextValue, saveMetadata: true);
        }
        else if (string.Equals(setting.valueType, "enum", StringComparison.OrdinalIgnoreCase) && setting.enumOptions != null && setting.enumOptions.Count > 0)
        {
            int currentIndex = setting.enumOptions.FindIndex(option => string.Equals(option, setting.value, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
                currentIndex = 0;

            int nextIndex = (currentIndex + 1) % setting.enumOptions.Count;
            ApplyRuntimeSettingValue(setting.id, setting.enumOptions[nextIndex], saveMetadata: true);
        }
    }

    private void AdjustCurrentGlobalSettingsValue(int delta)
    {
        if (delta == 0)
            return;

        if (string.IsNullOrEmpty(activeGlobalSettingsCategory))
        {
            switch (selectedGlobalSettingsTopIndex)
            {
                case 0:
                    ApplyRuntimeSettingValue("core.invertStrings", delta > 0 ? "true" : "false", saveMetadata: true);
                    break;
                case 1:
                    if (runtimeSettingById.TryGetValue("render.mode", out RuntimeSettingDefinition renderDefinition) &&
                        renderDefinition.EnumOptions != null &&
                        renderDefinition.EnumOptions.Count > 0)
                    {
                        string current = renderDefinition.Getter != null ? renderDefinition.Getter() : renderMode.ToString();
                        int currentIndex = renderDefinition.EnumOptions.FindIndex(option => string.Equals(option, current, StringComparison.OrdinalIgnoreCase));
                        if (currentIndex < 0)
                            currentIndex = 0;
                        int nextIndex = (currentIndex + delta + renderDefinition.EnumOptions.Count) % renderDefinition.EnumOptions.Count;
                        ApplyRuntimeSettingValue("render.mode", renderDefinition.EnumOptions[nextIndex], saveMetadata: true);
                    }
                    break;
                case 6:
                    if (delta != 0)
                        ResetGlobalSettingsToDefaultsFromUi();
                    break;
            }

            return;
        }

        List<RuntimeSettingSnapshot> settings = GetActiveGlobalSettingsItems();
        if (settings.Count == 0)
            return;

        RuntimeSettingSnapshot setting = settings[Mathf.Clamp(selectedGlobalSettingsItemIndex, 0, settings.Count - 1)];
        if (setting == null)
            return;

        if (string.Equals(setting.valueType, "bool", StringComparison.OrdinalIgnoreCase))
        {
            ApplyRuntimeSettingValue(setting.id, delta > 0 ? "true" : "false", saveMetadata: true);
            return;
        }

        if (string.Equals(setting.valueType, "enum", StringComparison.OrdinalIgnoreCase))
        {
            if (setting.enumOptions == null || setting.enumOptions.Count == 0)
                return;

            int currentIndex = setting.enumOptions.FindIndex(option => string.Equals(option, setting.value, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
                currentIndex = 0;

            int nextIndex = (currentIndex + delta + setting.enumOptions.Count) % setting.enumOptions.Count;
            ApplyRuntimeSettingValue(setting.id, setting.enumOptions[nextIndex], saveMetadata: true);
            return;
        }

        if (!float.TryParse(setting.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float currentValue))
            currentValue = setting.min;

        float step = Mathf.Abs(setting.step) > 0.0001f ? setting.step : 1f;
        float nextValue = Mathf.Clamp(currentValue + (delta * step), setting.min, setting.max);
        string serialized = string.Equals(setting.valueType, "int", StringComparison.OrdinalIgnoreCase)
            ? Mathf.RoundToInt(nextValue).ToString(CultureInfo.InvariantCulture)
            : nextValue.ToString("0.###", CultureInfo.InvariantCulture);
        ApplyRuntimeSettingValue(setting.id, serialized, saveMetadata: true);
    }

    private List<RuntimeSettingSnapshot> GetActiveGlobalSettingsItems()
    {
        string category = activeGlobalSettingsCategory;
        if (string.IsNullOrEmpty(category))
            return new List<RuntimeSettingSnapshot>();

        return BuildRuntimeSettingsSnapshot()
            .Where(section => string.Equals(CategorizeRuntimeSettingsSectionForMenu(section), category, StringComparison.OrdinalIgnoreCase))
            .SelectMany(section => section.settings ?? new List<RuntimeSettingSnapshot>())
            .Where(setting => setting != null && !string.Equals(setting.id, "core.invertStrings", StringComparison.OrdinalIgnoreCase) && !string.Equals(setting.id, "render.mode", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string GetGlobalSettingsCategoryFromTopIndex(int index)
    {
        switch (index)
        {
            case 2: return "Gameplay";
            case 3: return "2D Tabs";
            case 4: return "Highway3D";
            case 5: return "Visuals";
            default: return string.Empty;
        }
    }

    private static string CategorizeRuntimeSettingsSectionForMenu(RuntimeSettingSectionSnapshot section)
    {
        string normalizedTitle = section?.title?.ToLowerInvariant() ?? string.Empty;
        List<RuntimeSettingSnapshot> sectionSettings = section?.settings;

        if (normalizedTitle.Contains("timing") || normalizedTitle.Contains("forgiveness") || normalizedTitle.Contains("settings") || IsRuntimeSettingsSectionIdPrefix(sectionSettings, "core.") || IsRuntimeSettingsSectionIdPrefix(sectionSettings, "timing."))
            return "Gameplay";

        if (normalizedTitle.Contains("tab") || normalizedTitle.Contains("layout") || IsRuntimeSettingsSectionIdPrefix(sectionSettings, "layout."))
            return "2D Tabs";

        if (normalizedTitle.Contains("highway") || IsRuntimeSettingsSectionIdPrefix(sectionSettings, "highway.") || IsRuntimeSettingsSectionIdPrefix(sectionSettings, "render."))
            return "Highway3D";

        if (normalizedTitle.Contains("visual") || normalizedTitle.Contains("color") || normalizedTitle.Contains("background") || IsRuntimeSettingsSectionIdPrefix(sectionSettings, "fx.") || IsRuntimeSettingsSectionIdPrefix(sectionSettings, "bg."))
            return "Visuals";

        return "Gameplay";
    }

    private static bool IsRuntimeSettingsSectionIdPrefix(List<RuntimeSettingSnapshot> settings, string prefix)
    {
        if (settings == null || string.IsNullOrEmpty(prefix))
            return false;

        return settings.Any(setting =>
            setting != null &&
            !string.IsNullOrEmpty(setting.id) &&
            setting.id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
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

            Dictionary<string, string> defaults = LoadRuntimeSettingDefaultsFromFile();
            bool appliedMissingDefaults = false;

            foreach (RuntimeSettingDefinition definition in runtimeSettingDefinitions)
            {
                if (definition == null || string.IsNullOrEmpty(definition.Id))
                    continue;

                if (pendingGlobalRuntimeSettingValues.ContainsKey(definition.Id))
                    continue;

                string value;
                if (!defaults.TryGetValue(definition.Id, out value) && !runtimeSettingDefaultValues.TryGetValue(definition.Id, out value))
                    continue;

                ApplyRuntimeSettingValue(definition.Id, value, saveMetadata: false);
                pendingGlobalRuntimeSettingValues[definition.Id] = value;
                appliedMissingDefaults = true;
            }

            if (appliedMissingDefaults)
                SaveGlobalRuntimeSettingsMetadata();
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
        float speed = GetPlaybackSpeedScale();
        float volume = Mathf.Clamp01(songVolumePercent / 100f);
        generatedSongPlayer?.SetMasterVolumePercent(songVolumePercent);

        if (backingTrackSource == null)
            return;

        if (!Mathf.Approximately(backingTrackSource.pitch, speed))
            backingTrackSource.pitch = speed;

        if (!Mathf.Approximately(backingTrackSource.volume, volume))
            backingTrackSource.volume = volume;
    }

    private void SyncAudioToSongTimer(bool playImmediately, bool forceSeek = false)
    {
        SongPlaybackAudioMode effectiveMode = GetEffectiveSongPlaybackAudioMode();
        float playbackSpeedScale = GetPlaybackSpeedScale();

        if (effectiveMode == SongPlaybackAudioMode.Generated)
        {
            if (backingTrackSource != null && backingTrackSource.isPlaying)
                backingTrackSource.Pause();

            generatedSongPlayer?.SyncTransport(audioSongTimer, playbackSpeedScale, playImmediately, forceSeek);
            return;
        }

        generatedSongPlayer?.SyncTransport(audioSongTimer, playbackSpeedScale, false, forceSeek);

        if (effectiveMode == SongPlaybackAudioMode.Muted)
        {
            if (backingTrackSource != null && backingTrackSource.isPlaying)
                backingTrackSource.Pause();
            return;
        }

        if (backingTrackSource == null || backingTrackSource.clip == null)
            return;

        float timelineAudioTime = audioSongTimer + (audioOffsetMs / 1000f);
        float audioTime = Mathf.Clamp(timelineAudioTime, 0f, backingTrackSource.clip.length);

        if (forceSeek || Mathf.Abs(backingTrackSource.time - audioTime) > 0.04f)
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
