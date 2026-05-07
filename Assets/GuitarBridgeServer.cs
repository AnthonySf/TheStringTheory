using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

    public enum HighwayCharacterPortalColorPreset
    {
        Orange = 0,
        Black = 1
    }

    public enum RhythmHopoAccentColorPreset
    {
        White = 0,
        Orange = 1
    }

    [Header("Render Mode")]
    public GuitarRenderMode renderMode = GuitarRenderMode.Tabs;
    public GuitarGameplayMode gameplayMode = GuitarGameplayMode.Guitar;

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
    public float rhythmHopoOutlineGap = 0.035f;
    public RhythmHopoAccentColorPreset rhythmHopoAccentColor = RhythmHopoAccentColorPreset.White;

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
    public float highwayCharacterScale = 1f;
    public float highwayCharacterRigOffsetY = 0f;
    public float highwayCharacterOffsetX = 0.03f;
    public float highwayCharacterOffsetY = 0.045f;
    public float highwayCharacterFadeSoftness = 0.10f;
    public bool highwayCharacterMovementEnabled = true;
    public bool highwayCharacterMissColorEnabled = true;
    public bool highwayCharacterMissParticlesEnabled = false;
    public bool highwayCharacterPortalEnabled = true;
    public bool highwayCharacterPortalSwirlsEnabled = true;
    public float highwayCharacterPortalBodyOpacity = 0.80f;
    public HighwayCharacterPortalColorPreset highwayCharacterPortalEdgeColor = HighwayCharacterPortalColorPreset.Black;
    public HighwayCharacterPortalColorPreset highwayCharacterPortalSwirlColor = HighwayCharacterPortalColorPreset.Black;
    public float multiplayerHighwayCameraOffsetX = 0f;
    public float multiplayerHighwayCameraOffsetY = 4.05f;
    public float multiplayerHighwayCameraOffsetZ = -24.3f;
    public float multiplayerHighwayCameraPitchOffset = -10f;
    public float multiplayerHighwayCameraFieldOfView = 32.5f;
    public float multiplayerHighwayHalfSpread = 0.21f;
    public float multiplayerCharacterHorizontalOffset = -0.01f;
    public float multiplayerCharacterVerticalOffset = -0.08f;
    public float multiplayerPortalHorizontalOffset = 0.25f;
    public float multiplayerPortalVerticalOffset = -0.02f;
    public float multiplayerPortalWidthScale = 1f;
    public float multiplayerScoreHorizontalOffset = 0.10f;
    public float multiplayerScoreVerticalOffset = -0.15f;
    public float multiplayerComboBadgeHorizontalOffset = -0.21f;
    public float multiplayerComboBadgeVerticalOffset = 0.28f;
    public float highwayFretNumberYOffset = 0.45f;
    public float highwayFretNumberZOffset = 0.12f;
    public bool highwayHighlightFretBoundaries = false;
    public bool highwayShowApproachLine = false;
    public bool highwayShowLandingDot = false;

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
        public float observedAtUnscaled;
        public string source = string.Empty;
        public HashSet<int> pitches = new HashSet<int>();
        public HashSet<int> consumedKeys = new HashSet<int>();
    }

    private class ArcadeInputEvent
    {
        public int id;
        public float time;
        public bool[] heldLanes = new bool[5];
        public bool[] pressedLanes = new bool[5];
        public bool isStrum;
        public bool isTap;
        public bool isRelease;
        public bool isOpenButton;
        public bool overstrumJudged;
        public HashSet<int> consumedChordIds = new HashSet<int>();
    }

    private sealed class ArcadeActiveSustain
    {
        public int chordId;
        public ArcadeNoteData note;
        public float endTime;
        public float durationSeconds;
        public float basePointsPerSecond;
        public float lastProcessedTime;
        public float pendingScoreRemainder;
        public bool broken;
    }

    [Serializable]
    private sealed class MultiplayerRhythmInputAssignment
    {
        public MultiplayerRhythmInputDeviceKind kind;
        public int controllerSlot;
        public string displayName;
    }

    private sealed class MultiplayerRhythmPlayerState
    {
        public int playerIndex;
        public MultiplayerRhythmInputAssignment assignment = new MultiplayerRhythmInputAssignment();
        public List<ArcadeNoteState> noteStates = new List<ArcadeNoteState>();
        public readonly HashSet<int> sessionScoredChordIds = new HashSet<int>();
        public readonly List<ArcadeInputEvent> recentInputEvents = new List<ArcadeInputEvent>();
        public readonly Dictionary<int, ArcadeActiveSustain> activeSustains = new Dictionary<int, ArcadeActiveSustain>();
        public readonly Dictionary<int, int> awardedSustainScore = new Dictionary<int, int>();
        public readonly bool[] heldLanes = new bool[5];
        public readonly bool[] previousHeldLanes = new bool[5];
        public int latestInputEventId;
        public bool comboActive;
        public bool inputNeedsUnpausedPrime;
        public int comboCount;
        public int maxComboCount;
        public int hitCount;
        public int missCount;
        public int scoreValue;
        public float scorePercent;
    }

    private const float ArcadeMinimumSustainDurationSeconds = 0.12f;
    private const float ArcadeMinimumSustainBeats = 0.25f;
    private const int MultiplayerRhythmPlayerCount = 2;
    private const int MultiplayerRhythmSetupRowCount = 3;
    private const int MultiplayerRhythmSetupControllerButtonScanCount = 20;
    private const float ControllerTriggerAxisPressThreshold = 0.55f;
    private const float ControllerStrumAxisPressThreshold = 0.55f;

    [Flags]
    private enum DetectorHintNoteFlags
    {
        None = 0,
        Legato = 1 << 0,
        Bend = 1 << 1,
        Slide = 1 << 2,
        Harmonic = 1 << 3
    }

    private readonly struct DetectorHintExpectedNote
    {
        public readonly int midi;
        public readonly int stringIndex;
        public readonly int fret;
        public readonly int openMidi;
        public readonly int flags;

        public DetectorHintExpectedNote(int expectedMidi, int expectedStringIndex, int expectedFret, int expectedOpenMidi, DetectorHintNoteFlags expectedFlags)
        {
            midi = expectedMidi;
            stringIndex = expectedStringIndex;
            fret = expectedFret;
            openMidi = expectedOpenMidi;
            flags = (int)expectedFlags;
        }
    }

    private struct DetectorHintWindow
    {
        public float startTime;
        public float endTime;
        public HashSet<int> pitches;
        public DetectorHintExpectedNote[] expectedNotes;

        public DetectorHintWindow(float start, float end, HashSet<int> notePitches, DetectorHintExpectedNote[] noteExpectations = null)
        {
            startTime = start;
            endTime = end;
            pitches = notePitches;
            expectedNotes = noteExpectations ?? Array.Empty<DetectorHintExpectedNote>();
        }
    }

    private int[] activeStringBasePitch = (int[])StringTuningUtils.StandardGuitarTuning.Clone();
    private readonly Dictionary<string, int> noteToIndex = new Dictionary<string, int>();
    private readonly Dictionary<int, NoteData> chartNoteById = new Dictionary<int, NoteData>();
    private readonly List<NoteEvent> recentNoteEvents = new List<NoteEvent>();
    private readonly HashSet<int> latestDetectedPitches = new HashSet<int>();
    private readonly List<GameplayNoteState> chordMatchScratchStates = new List<GameplayNoteState>();
    private readonly List<int> chordMatchScratchConsumeKeys = new List<int>();

    private List<NoteData> chartNotes = new List<NoteData>();
    private List<GameplayNoteState> noteStates = new List<GameplayNoteState>();
    private List<TabSectionData> tabSections = new List<TabSectionData>();
    private List<ArpeggioGuideData> currentArpeggioGuides = new List<ArpeggioGuideData>();

    private IGuitarGameplayRenderer activeRenderer;
    private GuitarRenderMode activeRendererMode = (GuitarRenderMode)(-1);
    private GuitarGameplayMode activeRendererGameplayMode = (GuitarGameplayMode)(-1);
    private bool activeRendererWasMultiplayerRhythm;

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
    private bool loopStartConfigured;
    private bool loopEndConfigured;
    private int selectedLoopMarker = 1;
    private string selectedLoopBookmarkId = string.Empty;
    private readonly List<ArcadePracticeSectionData> currentArcadePracticeSections = new List<ArcadePracticeSectionData>();
    private int selectedArcadePracticeSectionIndex;
    private int arcadePracticeLoopStartSectionIndex = -1;
    private int arcadePracticeLoopEndSectionIndex = -1;
    private bool loopBookmarkRenameActive;
    private string loopBookmarkRenameDraft = string.Empty;
    private bool showLoopSettings;
    private bool showLoopPausePopup;
    private int selectedLoopPausePopupIndex;
    private float loopPauseDurationSeconds;
    private float loopRestartPauseRemainingSeconds;
    private bool pendingLoopRestartFromStartAfterResume;
    private bool pendingLoopStartCountdownAfterResume;
    private bool loopPausePopupResumePlaybackOnConfirm;
    private bool loopSettingsPreviewPlaying;
    private bool loopSettingsOpenedFromGameModes;
    private GuitarRenderMode loopSettingsReturnRenderMode = GuitarRenderMode.Tabs;
    private bool showOffsetHelper;
    private bool showGameModes;
    private bool showRocksmithDifficultyPopup;
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
    private int offsetHelperSavedSessionScoreValue;
    private int offsetHelperSavedGuitarComboCount;
    private bool offsetHelperSavedScoreSaveInvalidated;
    private float offsetHelperWorkingOffsetMs;
    private int latestNoteEventId;
    private bool latestPacketHadEvent;
    private long lastUdpPacketUtcTicks;
    private const float DetectorConnectionTimeoutSeconds = 1.5f;
    private string latestEventNotesText = "--";
    private string latestNotesDetectorAcceptanceSourceText = "--";
    private float latestParsedInputLevel = -1f;
    private float smoothedInputLevel;

    public int midiTrackIndex = -1;
    private int currentLoadedTrackIndex = -999;

    [Header("Rhythm Input")]
    [Min(1)] public int arcadeHighwayLaneCount = 5;
    public ArcadeInputSourceMode arcadeInputSource = ArcadeInputSourceMode.KeyboardAndController;
    [Min(0)] public int arcadeControllerDeviceIndex = 0;
    public bool arcadeGamepadMode;
    public bool arcadeMidiInputEnabled = false;
    [Min(0)] public int arcadeMidiInputDeviceIndex = 0;
    public int arcadeMidiLane0Note = 60;
    public int arcadeMidiLane1Note = 61;
    public int arcadeMidiLane2Note = 62;
    public int arcadeMidiLane3Note = 63;
    public int arcadeMidiLane4Note = 64;
    public int arcadeMidiOpenNote = 65;
    public KeyCode arcadeKeyboardGreen = KeyCode.A;
    public KeyCode arcadeKeyboardRed = KeyCode.S;
    public KeyCode arcadeKeyboardYellow = KeyCode.J;
    public KeyCode arcadeKeyboardBlue = KeyCode.K;
    public KeyCode arcadeKeyboardOrange = KeyCode.L;
    public KeyCode arcadeKeyboardStrumUp = KeyCode.Space;
    public KeyCode arcadeKeyboardStrumDown = KeyCode.None;
    public KeyCode arcadeKeyboardOpen = KeyCode.None;
    public KeyCode arcadeControllerGreen = KeyCode.JoystickButton6;
    public KeyCode arcadeControllerRed = KeyCode.JoystickButton4;
    public KeyCode arcadeControllerYellow = KeyCode.JoystickButton5;
    public KeyCode arcadeControllerBlue = KeyCode.JoystickButton7;
    public KeyCode arcadeControllerOrange = KeyCode.JoystickButton0;
    public KeyCode arcadeControllerStrumUp = KeyCode.JoystickButton13;
    public KeyCode arcadeControllerStrumDown = KeyCode.JoystickButton14;
    public KeyCode arcadeControllerOpen = KeyCode.JoystickButton8;

    [Header("Rhythm Timing")]
    [Min(0f)] public float arcadeHitWindowEarly = 0.14f;
    [Min(0f)] public float arcadeHitWindowLate = 0.14f;
    [Min(0f)] public float arcadeResolvedHoldTime = 0.0f;
    public float arcadeNoteSpawnZ = 100.0f;

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
        public bool favoriteInLibrary = false;
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
        public string selectedArcadeArrangementId;
        public string selectedArcadeDifficulty = "Expert";
        public bool useAllGeneratedPlaybackParts = true;
        public List<string> generatedEnabledPartIds = new List<string>();
        public List<GeneratedPlaybackSelectionOverride> generatedPlaybackSelectionOverrides = new List<GeneratedPlaybackSelectionOverride>();
        public int bestScoreValue = 0;
        public float bestScorePercent = 0f;
        public int bestArcadeScoreValue = 0;
        public List<LoopBookmarkScopeEntry> loopBookmarkScopes = new List<LoopBookmarkScopeEntry>();
        public List<TrackScoreEntry> trackScores = new List<TrackScoreEntry>();
        public List<ArcadeScoreEntry> arcadeScores = new List<ArcadeScoreEntry>();
        public List<TrackOffsetOverride> trackOffsetOverrides = new List<TrackOffsetOverride>();
    }

    [Serializable]
    private class LoopBookmarkScopeEntry
    {
        public string scopeKey;
        public string scopeDisplayName;
        public List<LoopBookmarkEntry> bookmarks = new List<LoopBookmarkEntry>();
    }

    [Serializable]
    private class LoopBookmarkEntry
    {
        public string bookmarkId;
        public string name;
        public float loopStartTime;
        public float loopEndTime;
        public long createdUtcTicks;
        public long updatedUtcTicks;
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
        public int bestScoreValue;
        public float bestScorePercent;
        public int heroBestScoreValue;
        public float heroBestScorePercent;
        public int heroBestHeartsRemaining;
        public int heroBestHeartsTotal;
    }

    [Serializable]
    private class ArcadeScoreEntry
    {
        public string arrangementId;
        public string displayName;
        public string difficulty;
        public float bestScorePercent;
        public float bestAccuracyPercent;
        public int bestScoreValue;
        public float heroBestScorePercent;
        public float heroBestAccuracyPercent;
        public int heroBestScoreValue;
        public int heroBestHeartsRemaining;
        public int heroBestHeartsTotal;
    }

    private readonly struct HeroScoreSummary
    {
        public readonly int scoreValue;
        public readonly float percent;
        public readonly int heartsRemaining;
        public readonly int heartsTotal;

        public HeroScoreSummary(int scoreValue, float percent, int heartsRemaining, int heartsTotal)
        {
            this.scoreValue = Mathf.Max(0, scoreValue);
            this.percent = Mathf.Clamp(percent, 0f, 100f);
            this.heartsRemaining = Mathf.Max(0, heartsRemaining);
            this.heartsTotal = Mathf.Max(0, heartsTotal);
        }

        public bool IsAvailable => heartsTotal > 0;
    }

    private readonly struct ArcadeHeroScoreSummary
    {
        public readonly int scoreValue;
        public readonly float accuracyPercent;
        public readonly int heartsRemaining;
        public readonly int heartsTotal;

        public ArcadeHeroScoreSummary(int scoreValue, float accuracyPercent, int heartsRemaining, int heartsTotal)
        {
            this.scoreValue = Mathf.Max(0, scoreValue);
            this.accuracyPercent = Mathf.Clamp(accuracyPercent, 0f, 100f);
            this.heartsRemaining = Mathf.Max(0, heartsRemaining);
            this.heartsTotal = Mathf.Max(0, heartsTotal);
        }

        public bool IsAvailable => heartsTotal > 0;
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

    [Serializable]
    private class GameSaveState
    {
        public bool firstStartCompleted;
    }

    private enum StartMenuStep
    {
        SelectMode = 0,
        GuitarSetup = 1,
        ArcadeSetup = 2
    }

    private enum SongLibraryBrowseMode
    {
        All = 0,
        Artists = 1,
        Albums = 2
    }

    private sealed class SongLibraryBrowseEntry
    {
        public bool IsSong;
        public SongLibraryEntry Song;
        public string GroupKey;
        public string DisplayName;
        public string Subtitle;
        public string ArtworkPath;
        public string ScoreText;
        public float ScorePercent;
        public string DifficultyLabel;
        public int SongCount;
    }

    private sealed class RocksmithTrackSelectionGroup
    {
        public string GroupId;
        public string DisplayName;
        public string TuningDisplayName;
        public readonly List<MusicXmlLoader.MusicXmlPartSummary> Variants = new List<MusicXmlLoader.MusicXmlPartSummary>();
    }

    private string currentSongFileName = "song.mp3";
    private bool hasBackingTrack;
    private bool showSongSettings;
    private bool showMainMenu;
    private bool mainMenuFlowActive;
    private int selectedMainMenuIndex;
    private bool showStartMenu;
    private bool showMultiplayerRhythmSetup;
    private int selectedMultiplayerRhythmSetupIndex;
    private int selectedMultiplayerRhythmPlayerOneDeviceIndex;
    private int selectedMultiplayerRhythmPlayerTwoDeviceIndex = 1;
    private int activeMultiplayerRhythmSetupCapturePlayerIndex = -1;
    private int activeMultiplayerRhythmSetupCaptureStartFrame = -1;
    private readonly List<MultiplayerRhythmInputAssignment> multiplayerRhythmAvailableDevices = new List<MultiplayerRhythmInputAssignment>();
    private bool pendingMultiplayerRhythmSongSelection;
    private bool returnToMultiplayerRhythmSetupFromSongSelection;
    private bool showLibraryLoadingOverlay;
    private bool firstStartCompleted;
    private StartMenuStep startMenuStep = StartMenuStep.SelectMode;
    private int selectedStartMenuModeIndex;
    private int selectedStartMenuArcadeSetupIndex;
    private int selectedStartMenuArcadeInputIndex;
    private bool startMenuArcadeGamepadMode = true;
    private bool showSongSelection;
    private bool songSelectionSongConfirmed;
    private bool showTrackSelection;
    private bool showGlobalSettings;
    private int selectedGlobalSettingsTopIndex;
    private int selectedGlobalSettingsItemIndex;
    private string activeGlobalSettingsCategory = string.Empty;
    private bool globalSettingsTransparentBackground;
    private bool gameplayHudPreviewInMenus;
    private int selectedSongListIndex;
    private int songListScrollOffset;
    private int selectedTrackListIndex;
    private int trackListScrollOffset;
    private SongLibraryEntry pendingTrackSelectionSong;
    private readonly List<SongLibraryEntry> availableSongs = new List<SongLibraryEntry>();
    private readonly List<SongLibraryBrowseEntry> displayedSongLibraryEntries = new List<SongLibraryBrowseEntry>();
    private SongLibraryBrowseMode songLibraryBrowseMode = SongLibraryBrowseMode.All;
    private string songLibraryBrowseScopeKey = string.Empty;
    private readonly List<MusicXmlLoader.MusicXmlPartSummary> pendingTrackSelectionParts = new List<MusicXmlLoader.MusicXmlPartSummary>();
    private readonly List<RocksmithTrackSelectionGroup> pendingRocksmithTrackSelectionGroups = new List<RocksmithTrackSelectionGroup>();
    private int selectedRocksmithDifficultyIndex;
    private int selectedGameplayRocksmithDifficultyIndex;
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
    private bool persistedUseAutoTrackSelection = true;
    private string persistedSelectedMusicXmlPartId = string.Empty;
    private GeneratedSongPlayer generatedSongPlayer;
    private GeneratedPlaybackArrangement generatedPlaybackSourceArrangement;
    private GeneratedPlaybackArrangement generatedPlaybackArrangement;
    private bool useAllGeneratedPlaybackParts = true;
    private List<string> generatedEnabledPartIds = new List<string>();
    private bool showGeneratedAudioTrackSelectionPopup;
    private int selectedGeneratedAudioTrackSelectionIndex;
    private bool showSongSettingsTrackSelectionPopup;
    private int selectedSongSettingsTrackSelectionIndex;
    private int currentSongBestScoreValue;
    private float currentSongBestScorePercent;
    private int currentTrackBestScoreValue;
    private float currentTrackBestScorePercent;
    private int currentSongBestArcadeScoreValue;
    private int currentTrackBestArcadeScoreValue;
    private int currentTrackBestArcadeHeroScoreValue;
    private int currentTrackHeroBestScoreValue;
    private float currentTrackHeroBestScorePercent;
    private int currentTrackHeroBestHeartsRemaining;
    private int currentTrackHeroBestHeartsTotal;
    private bool scoreSaveInvalidated;
    private readonly HashSet<int> sessionScoredNoteIds = new HashSet<int>();
    private readonly Dictionary<int, int> arcadeChordAwardedSustainScore = new Dictionary<int, int>();
    private int sessionScoreHits;
    private int sessionScoreMisses;
    private int currentSessionScoreValue;
    private float currentSessionScorePercent;
    private int currentSessionArcadeScoreValue;
    private int guitarComboCount;
    private int arcadeComboCount;
    private int arcadeTotalChordCount;
    private const string SelectedSongDirectoryPrefsKey = "guitar_selected_song_directory";
    private const string SelectedSongLibraryTypePrefsKey = "guitar_selected_song_library_type";
    private const string HeroModeEnabledPrefsKey = "guitar_hero_mode_enabled";
    private const string HeroModeHeartCountPrefsKey = "guitar_hero_mode_heart_count";
    private const string NativeDetectorInputDevicePrefsKey = "guitar_native_detector_input_device";
    private const string GameSaveStateFileName = "game_save.json";
    private UnityToneLabRuntime unityToneLabRuntime;
    private UnityToneLabOverlay unityToneLabOverlay;
    private bool isLoadingBackingTrack;
    private string backingTrackLoadError = string.Empty;
    private bool songHasEnded;
    private bool songEndedAsGameOver;
    private Coroutine openSongSelectionRoutine;
    private int openSongSelectionRequestId;
    private const float LibraryLoadingOverlayMinimumSeconds = 0.32f;
    private bool songSelectionOpenedFromSongEnd;
    private bool songSelectionOpenedFromMainMenu;
    private bool showStartupTuningReminder;
    private bool resumeGameplayAfterStartupTuningReminder;
    private int startupTuningReminderShownFrame = -1;
    private SongLibraryEntry currentSongEntry;
    private readonly List<MusicXmlLoader.MusicXmlPartSummary> currentSongPartSummaries = new List<MusicXmlLoader.MusicXmlPartSummary>();
    private SongLibraryType selectedSongLibraryType = SongLibraryType.Guitar;
    private ArcadeChartData currentArcadeChart = new ArcadeChartData();
    private readonly List<ArcadeArrangementSummary> currentArcadeArrangementSummaries = new List<ArcadeArrangementSummary>();
    private readonly List<ArcadeArrangementSummary> pendingArcadeArrangementSummaries = new List<ArcadeArrangementSummary>();
    private string selectedArcadeArrangementId = string.Empty;
    private ArcadeDifficulty selectedArcadeDifficulty = ArcadeDifficulty.Expert;
    private List<ArcadeNoteState> arcadeNoteStates = new List<ArcadeNoteState>();
    private bool multiplayerRhythmModeActive;
    private int multiplayerRhythmWinningPlayerIndex = -1;
    private readonly MultiplayerRhythmPlayerState[] multiplayerRhythmPlayers = new MultiplayerRhythmPlayerState[MultiplayerRhythmPlayerCount];
    private readonly HashSet<int> arcadeSessionScoredNoteIds = new HashSet<int>();
    private readonly List<ArcadeInputEvent> arcadeRecentInputEvents = new List<ArcadeInputEvent>();
    private readonly Dictionary<int, ArcadeActiveSustain> activeArcadeSustains = new Dictionary<int, ArcadeActiveSustain>();
    private readonly bool[] arcadeHeldLanes = new bool[5];
    private readonly bool[] previousArcadeHeldLanes = new bool[5];
    private readonly bool[] arcadeMidiHeldLanes = new bool[5];
    private ArcadeMidiInputBridge arcadeMidiInputBridge;
    private float nextArcadeMidiInputStartRealtime;
    private bool arcadeMidiInputUnavailableLogged;
    private bool arcadeMidiOpenButtonPressed;
    private readonly float[] previousSpecificControllerLeftTriggerAxes = new float[ArcadeControllerSlotCount];
    private readonly float[] currentSpecificControllerLeftTriggerAxes = new float[ArcadeControllerSlotCount];
    private readonly float[] previousSpecificControllerRightTriggerAxes = new float[ArcadeControllerSlotCount];
    private readonly float[] currentSpecificControllerRightTriggerAxes = new float[ArcadeControllerSlotCount];
    private readonly float[] previousSpecificControllerStrumAxes = new float[ArcadeControllerSlotCount];
    private readonly float[] currentSpecificControllerStrumAxes = new float[ArcadeControllerSlotCount];
    private int specificControllerExtendedAxesFrame = -1;
    private bool arcadeComboActive;
    private bool arcadeInputNeedsUnpausedPrime;
    private string activeArcadeBindingSettingId = string.Empty;
    private int activeArcadeBindingStartFrame = -1;
    private int latestArcadeInputEventId;
    private readonly List<AudioSource> arcadeAudioSources = new List<AudioSource>();
    private int pendingArcadeAudioLoadCount;
    private bool useAutoTrackSelection = true;
    private string selectedMusicXmlPartId = string.Empty;
    private bool forceStandardTuning = true;
    private float lastLeftArrowTapTime = -10f;
    private float lastRightArrowTapTime = -10f;
    private float lastMainMenuKeyboardInputTime = -10f;
    private int cachedUiControllerInputFrame = -1;
    private float previousUiControllerHorizontalAxis;
    private float previousUiControllerVerticalAxis;
    private float currentUiControllerHorizontalAxis;
    private float currentUiControllerVerticalAxis;
    private const float ArrowDoubleTapThreshold = 0.35f;
    private const float UiControllerAxisThreshold = 0.55f;
    private const float NoteByNoteTimeEpsilon = 0.0001f;
    private const int MainMenuOptionCount = 7;
    private const int StartMenuModeOptionCount = 2;
    private const int StartMenuGuitarSetupRowCount = 2;
    private const int StartMenuArcadeSetupRowCount = 3;
    private const int StartMenuArcadeInputOptionCount = 3;
    private const int NotesDetectorTestOptionCount = 3;
    private const float NotesDetectorRoutineHintWindowSeconds = 1.25f;
    private const float NotesDetectorTestHintTimelineBaseSeconds = 1000f;
    private const float NotesDetectorTestHintTimelineStepSeconds = 10f;
    private const float NotesDetectorRoutineTargetConfirmSeconds = 0.15f;
    private const float NotesDetectorRoutineRecentEventHoldSeconds = 0.18f;
    private const string NotesDetectorTestSongFolderName = "NotesDetector";
    private const string NotesDetectorTestCatalogFileName = "notes_detector_tests.json";
    private const float MainMenuHoverLockSeconds = 0.20f;
    private ToneLabReturnContext toneLabReturnContext;
    private NotesDetectorBackendMode notesDetectorBackendMode = NotesDetectorBackendMode.NativeEmbeddedBridge;
    private NativeNotesDetectorBridge nativeNotesDetectorBridge;
    private readonly List<NativeDetectorInputDevice> nativeNotesDetectorInputDevices = new List<NativeDetectorInputDevice>();
    private NativeDetectorRuntimeInfo nativeNotesDetectorRuntimeInfo = new NativeDetectorRuntimeInfo();
    private int selectedNativeNotesDetectorInputDeviceIndex = -1;
    private bool showNotesDetectorTestMenu;
    private bool notesDetectorGameplayTestActive;
    private int selectedNotesDetectorTestIndex;
    private bool showNotesDetectorTestSelectionPopup;
    private int selectedNotesDetectorCatalogIndex;
    private LoadedNotesDetectorTestCatalog notesDetectorLoadedTestCatalog;
    private readonly List<LoadedNotesDetectorTestCatalogEntry> notesDetectorSelectableTests = new List<LoadedNotesDetectorTestCatalogEntry>();
    private string selectedNotesDetectorTestId = string.Empty;
    private bool showNotesDetectorRoutinePopup;
    private int notesDetectorRoutineStageIndex;
    private float notesDetectorRoutineMatchedSinceTime = -1f;
    private float notesDetectorRoutineOpenedTime;
    private string notesDetectorEditorLogPath = string.Empty;
    private string loopCountdownEditorLogPath = string.Empty;
    private readonly StringBuilder loopCountdownEditorLogBuffer = new StringBuilder(16384);
    private int loopCountdownEditorLogFrameIndex;
    private SongLibraryEntry notesDetectorGameplayReturnSongEntry;
    private string notesDetectorGameplayReturnSelectedMusicXmlPartId = string.Empty;
    private bool notesDetectorGameplayReturnUseAutoTrackSelection = true;
    private bool notesDetectorGameplayReturnNoteByNoteModeEnabled;
    private bool notesDetectorGameplayReturnHeroModeEnabled;
    private GuitarRenderMode notesDetectorGameplayReturnRenderMode = GuitarRenderMode.Highway3D;

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
    private const int ArcadeControllerSlotCount = 8;
    private const int GlobalSettingsTopLevelCount = 10;
    private const int GlobalSettingsFirstCategoryTopIndex = 2;
    private const int GlobalSettingsLastCategoryTopIndex = 8;
    private const int GlobalSettingsResetTopIndex = 9;


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
        public Action Activator;
        public ArcadeBindingCaptureKind BindingCaptureKind = ArcadeBindingCaptureKind.None;
    }

    public enum ArcadeInputSourceMode
    {
        Keyboard,
        Controller,
        KeyboardAndController,
        Midi,
        All
    }

    private enum ArcadeBindingCaptureKind
    {
        None,
        Keyboard,
        Controller
    }

    private static readonly string[] HighwayCharacterDisplayModeOptions =
    {
        "Always",
        "Never",
        "Only In Hero Mode"
    };

    private static readonly string[] HighwayCharacterPortalColorPresetOptions =
    {
        "Orange",
        "Black"
    };

    private static readonly string[] RhythmHopoAccentColorPresetOptions =
    {
        "White",
        "Orange"
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
        LoadGameSaveState();
        LoadHeroModePreferences();
        LoadNativeDetectorPreferences();
        LoadSongLibraryTypePreference();
        InitializeMultiplayerRhythmPlayers();
        RefreshMultiplayerRhythmAvailableDevices();
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
        bool shouldLogLoopCountdownFrame = Application.isEditor && loopRestartPauseRemainingSeconds > 0.0001f;
        long frameStartTicks = 0L;
        long sectionStartTicks = 0L;
        double handlePauseControlsMs = 0d;
        double disableLoopModeMs = 0d;
        double countdownAdvanceMs = 0d;
        double transportAdvanceMs = 0d;
        double noteByNoteGateMs = 0d;
        double songEndMs = 0d;
        double playbackSpeedMs = 0d;
        double audioSyncMs = 0d;
        double trackReloadMs = 0d;
        double inputUpdateMs = 0d;
        double sessionScoreMs = 0d;
        double gameplayUpdateMs = 0d;
        double inputLevelMs = 0d;
        double hintSendMs = 0d;
        double ensureRendererMs = 0d;
        double snapshotBuildMs = 0d;
        double renderMs = 0d;
        double toneLabMs = 0d;
        double uiMs = 0d;
        GuitarGameplaySnapshot snapshot = null;
        if (shouldLogLoopCountdownFrame)
        {
            if (string.IsNullOrWhiteSpace(loopCountdownEditorLogPath))
                StartLoopCountdownEditorLogSession("countdown-frame");

            frameStartTicks = GetLoopCountdownTimestamp();
            sectionStartTicks = frameStartTicks;
        }

        HandlePauseControls();
        if (shouldLogLoopCountdownFrame)
        {
            long afterHandlePauseControlsTicks = GetLoopCountdownTimestamp();
            handlePauseControlsMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterHandlePauseControlsTicks);
            sectionStartTicks = afterHandlePauseControlsTicks;
        }

        DisableUnsupportedLoopModeState();
        if (shouldLogLoopCountdownFrame)
        {
            long afterDisableLoopModeTicks = GetLoopCountdownTimestamp();
            disableLoopModeMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterDisableLoopModeTicks);
            sectionStartTicks = afterDisableLoopModeTicks;
        }

        bool loopPreviewActive = showLoopSettings && loopSettingsPreviewPlaying;
        bool offsetHelperPreviewActive = showOffsetHelper && offsetHelperAdjusting && offsetHelperPreviewPlaying;
        bool loopGapActive = loopRestartPauseRemainingSeconds > 0.0001f;
        float songTimeBeforeAdvance = songTimer;

        if (loopGapActive)
        {
            loopRestartPauseRemainingSeconds = Mathf.Max(0f, loopRestartPauseRemainingSeconds - Time.deltaTime);
            loopGapActive = loopRestartPauseRemainingSeconds > 0.0001f;
        }
        if (shouldLogLoopCountdownFrame)
        {
            long afterCountdownAdvanceTicks = GetLoopCountdownTimestamp();
            countdownAdvanceMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterCountdownAdvanceTicks);
            sectionStartTicks = afterCountdownAdvanceTicks;
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
        if (shouldLogLoopCountdownFrame)
        {
            long afterTransportAdvanceTicks = GetLoopCountdownTimestamp();
            transportAdvanceMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterTransportAdvanceTicks);
            sectionStartTicks = afterTransportAdvanceTicks;
        }

        ApplyNoteByNoteTransportGate(songTimeBeforeAdvance, loopPreviewActive, offsetHelperPreviewActive, loopGapActive);
        if (shouldLogLoopCountdownFrame)
        {
            long afterNoteByNoteGateTicks = GetLoopCountdownTimestamp();
            noteByNoteGateMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterNoteByNoteGateTicks);
            sectionStartTicks = afterNoteByNoteGateTicks;
        }

        UpdateSongEndState();
        if (shouldLogLoopCountdownFrame)
        {
            long afterSongEndTicks = GetLoopCountdownTimestamp();
            songEndMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterSongEndTicks);
            sectionStartTicks = afterSongEndTicks;
        }

        ApplyPlaybackSpeedToAudio();
        if (shouldLogLoopCountdownFrame)
        {
            long afterPlaybackSpeedTicks = GetLoopCountdownTimestamp();
            playbackSpeedMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterPlaybackSpeedTicks);
            sectionStartTicks = afterPlaybackSpeedTicks;
        }

        if (!loopGapActive)
            SyncAudioToSongTimer(playImmediately: ShouldPlaybackAudio(loopPreviewActive, offsetHelperPreviewActive, loopGapActive));
        if (shouldLogLoopCountdownFrame)
        {
            long afterAudioSyncTicks = GetLoopCountdownTimestamp();
            audioSyncMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterAudioSyncTicks);
            sectionStartTicks = afterAudioSyncTicks;
        }

        if (gameplayMode == GuitarGameplayMode.Guitar && midiTrackIndex != currentLoadedTrackIndex)
            LoadTestSong(preservePauseUiState: isPaused || showMainMenu || showSongSettings || showSongSelection || showTrackSelection || showGlobalSettings || showLoopPausePopup);
        if (shouldLogLoopCountdownFrame)
        {
            long afterTrackReloadTicks = GetLoopCountdownTimestamp();
            trackReloadMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterTrackReloadTicks);
            sectionStartTicks = afterTrackReloadTicks;
        }

        if (gameplayMode == GuitarGameplayMode.Guitar)
        {
            StopArcadeMidiInput();
            if (!loopGapActive)
            {
                ParseDetectorState();
                RefreshDetectorBackendStatus();
            }
        }
        else if (multiplayerRhythmModeActive)
        {
            StopArcadeMidiInput();
            if (!loopGapActive)
                UpdateMultiplayerRhythmInputState();
        }
        else
        {
            if (!loopGapActive)
                UpdateArcadeInputState();
        }
        if (shouldLogLoopCountdownFrame)
        {
            long afterInputUpdateTicks = GetLoopCountdownTimestamp();
            inputUpdateMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterInputUpdateTicks);
            sectionStartTicks = afterInputUpdateTicks;
        }

        if (isPaused)
            UpdateSessionScoreState();
        if (shouldLogLoopCountdownFrame)
        {
            long afterSessionScoreTicks = GetLoopCountdownTimestamp();
            sessionScoreMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterSessionScoreTicks);
            sectionStartTicks = afterSessionScoreTicks;
        }

        if (!isPaused && !loopGapActive)
        {
            if (gameplayMode == GuitarGameplayMode.Guitar)
            {
                PruneHistory();
                UpdateGameplayStates();
                UpdateNoteByNoteWaitingStateAfterJudgment(loopPreviewActive, offsetHelperPreviewActive);
            }
            else
            {
                if (multiplayerRhythmModeActive)
                {
                    PruneMultiplayerRhythmInputHistory();
                    UpdateMultiplayerRhythmGameplayStates();
                }
                else
                {
                    PruneArcadeInputHistory();
                    UpdateArcadeGameplayStates();
                }
            }

            UpdateSessionScoreState();
            if (!multiplayerRhythmModeActive)
            {
                TryTriggerHeroModeGameOver();
                UpdateAndPersistSongBestScore();
            }
        }
        if (shouldLogLoopCountdownFrame)
        {
            long afterGameplayUpdateTicks = GetLoopCountdownTimestamp();
            gameplayUpdateMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterGameplayUpdateTicks);
            sectionStartTicks = afterGameplayUpdateTicks;
        }

        UpdateInputLevelEstimate();
        if (shouldLogLoopCountdownFrame)
        {
            long afterInputLevelTicks = GetLoopCountdownTimestamp();
            inputLevelMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterInputLevelTicks);
            sectionStartTicks = afterInputLevelTicks;
        }

        if (gameplayMode == GuitarGameplayMode.Guitar && !loopGapActive)
            SendDetectorHintPacketIfNeeded();
        if (shouldLogLoopCountdownFrame)
        {
            long afterHintSendTicks = GetLoopCountdownTimestamp();
            hintSendMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterHintSendTicks);
            sectionStartTicks = afterHintSendTicks;
        }

        EnsureRenderer();
        if (shouldLogLoopCountdownFrame)
        {
            long afterEnsureRendererTicks = GetLoopCountdownTimestamp();
            ensureRendererMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterEnsureRendererTicks);
            sectionStartTicks = afterEnsureRendererTicks;
        }

        if (activeRenderer != null)
        {
            if (shouldLogLoopCountdownFrame)
            {
                long snapshotStartTicks = sectionStartTicks;
                snapshot = BuildSnapshot();
                long afterSnapshotTicks = GetLoopCountdownTimestamp();
                snapshotBuildMs = GetLoopCountdownElapsedMilliseconds(snapshotStartTicks, afterSnapshotTicks);
                activeRenderer.Render(snapshot);
                long afterRenderTicks = GetLoopCountdownTimestamp();
                renderMs = GetLoopCountdownElapsedMilliseconds(afterSnapshotTicks, afterRenderTicks);
                sectionStartTicks = afterRenderTicks;
            }
            else
            {
                activeRenderer.Render(BuildSnapshot());
            }
        }

        if (unityToneLabOverlay != null)
        {
            unityToneLabOverlay.SetVisible(showToneLab);
            if (showToneLab)
                unityToneLabOverlay.RefreshUi(syncControls: false);
        }
        if (shouldLogLoopCountdownFrame)
        {
            long afterToneLabTicks = GetLoopCountdownTimestamp();
            toneLabMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterToneLabTicks);
            sectionStartTicks = afterToneLabTicks;
        }

        UpdateUiText();
        if (shouldLogLoopCountdownFrame)
        {
            long afterUiTicks = GetLoopCountdownTimestamp();
            uiMs = GetLoopCountdownElapsedMilliseconds(sectionStartTicks, afterUiTicks);
            double totalMs = GetLoopCountdownElapsedMilliseconds(frameStartTicks, afterUiTicks);
            int noteStateCount = noteStates != null ? noteStates.Count : 0;
            int arcadeNoteStateCount = arcadeNoteStates != null ? arcadeNoteStates.Count : 0;
            string rendererName = activeRenderer != null ? activeRenderer.GetType().Name : "null";
            LogLoopCountdownEditor(
                $"FRAME {loopCountdownEditorLogFrameIndex++} remaining={loopRestartPauseRemainingSeconds.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"song={songTimer.ToString("F3", CultureInfo.InvariantCulture)} audio={audioSongTimer.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"loop=[{loopStartTime.ToString("F3", CultureInfo.InvariantCulture)},{loopEndTime.ToString("F3", CultureInfo.InvariantCulture)}] " +
                $"paused={isPaused} nbmWait={noteByNoteWaitingForMatch} renderer={rendererName} renderMode={renderMode} gameplayMode={gameplayMode} " +
                $"notes={noteStateCount} arcadeNotes={arcadeNoteStateCount} " +
                $"timingsMs pause={handlePauseControlsMs:F3} disable={disableLoopModeMs:F3} countdown={countdownAdvanceMs:F3} advance={transportAdvanceMs:F3} " +
                $"gate={noteByNoteGateMs:F3} songEnd={songEndMs:F3} speed={playbackSpeedMs:F3} audioSync={audioSyncMs:F3} trackReload={trackReloadMs:F3} " +
                $"input={inputUpdateMs:F3} score={sessionScoreMs:F3} gameplay={gameplayUpdateMs:F3} inputLevel={inputLevelMs:F3} hint={hintSendMs:F3} " +
                $"ensureRenderer={ensureRendererMs:F3} snapshot={snapshotBuildMs:F3} render={renderMs:F3} toneLab={toneLabMs:F3} ui={uiMs:F3} total={totalMs:F3}");
        }

        if (Application.isEditor)
        {
            if (loopGapActive)
            {
                if (string.IsNullOrWhiteSpace(loopCountdownEditorLogPath))
                    StartLoopCountdownEditorLogSession("countdown-active");
            }
            else if (!string.IsNullOrWhiteSpace(loopCountdownEditorLogPath))
            {
                StopLoopCountdownEditorLogSession("countdown-finished");
            }
        }
    }

    private void HandlePauseControls()
    {
        if (showStartupTuningReminder)
        {
            if (IsUiSubmitPressed() ||
                IsUiBackPressed() ||
                IsUiPausePressed() ||
                IsStartupTuningReminderHeldDismissPressed())
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

        if (showRocksmithDifficultyPopup)
        {
            HandleRocksmithDifficultyPopupControls();
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
            if (showGlobalSettings)
                globalSettingsTransparentBackground = false;
            gameplayHudPreviewInMenus = false;
            showSongSettings = false;
        }

        if (!showGlobalSettings)
            CancelArcadeBindingCapture();

        if (showMultiplayerRhythmSetup)
        {
            HandleMultiplayerRhythmSetupControls();
            return;
        }

        if (showStartMenu)
        {
            HandleStartMenuControls();
            return;
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

        if (IsRestartPressed())
        {
            if (notesDetectorGameplayTestActive)
                return;

            RetrySongFromUi();
            return;
        }

        if (IsUiPausePressed())
        {
            isPaused = !isPaused;
            if (isPaused)
                selectedPauseActionIndex = GetFirstVisiblePauseActionIndex();
            gameplayHudPreviewInMenus = false;
            showSongSettings = false;
            showMainMenu = false;
            mainMenuFlowActive = false;
            showSongSelection = false;
            showTrackSelection = false;
            showGlobalSettings = false;
            SyncAudioToSongTimer(playImmediately: !isPaused);
            return;
        }

        if (!isPaused)
            return;

        if (IsUiBackPressed())
        {
            ResumePlaybackFromUi();
            return;
        }

        if (notesDetectorGameplayTestActive)
        {
            if (IsUiUpPressed())
            {
                MovePauseActionSelectionFromUi(-1);
                return;
            }

            if (IsUiDownPressed())
            {
                MovePauseActionSelectionFromUi(1);
                return;
            }

            if (IsUiSubmitPressed())
            {
                ActivateSelectedPauseActionFromUi();
                return;
            }

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

        if (IsUiUpPressed())
        {
            MovePauseActionSelectionFromUi(-1);
            return;
        }

        if (IsUiDownPressed())
        {
            MovePauseActionSelectionFromUi(1);
            return;
        }

        if (IsUiSubmitPressed())
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

        if (HandleGameplayHudPreviewToggleInput())
            return;


        if (IsLoopModeAvailable())
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                if (loopStartConfigured)
                {
                    selectedLoopMarker = 1;
                    SeekSongTimeFromUserNavigation(loopStartTime, false);
                }
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                if (loopEndConfigured)
                {
                    selectedLoopMarker = 2;
                    SeekSongTimeFromUserNavigation(loopEndTime, false);
                }
            }
        }

        if (IsUiLeftPressed())
        {
            if (Time.unscaledTime - lastLeftArrowTapTime <= ArrowDoubleTapThreshold)
            {
                JumpToAdjacentNote(false);
                lastLeftArrowTapTime = -10f;
                return;
            }
            lastLeftArrowTapTime = Time.unscaledTime;
        }

        if (IsUiRightPressed())
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
        if (IsUiLeftHeld())
            seekDirection -= 1f;
        if (IsUiRightHeld())
            seekDirection += 1f;

        if (Mathf.Approximately(seekDirection, 0f))
            return;

        SeekSongTimeFromUserNavigation(songTimer + (seekDirection * pauseSeekStepSeconds * Time.deltaTime), false);
        SnapSongTimeIntoLoopWindowIfNeeded();
    }

    private void HandleLoopSettingsControls()
    {
        if (UsesRhythmPracticeSectionLoop())
        {
            HandleRhythmPracticeLoopSettingsControls();
            return;
        }

        if (loopBookmarkRenameActive)
        {
            bool cancelRenamePressed =
                Input.GetKeyDown(KeyCode.Escape) ||
                TryGetButtonDown("Cancel") ||
                Input.GetKeyDown(KeyCode.JoystickButton1) ||
                Input.GetKeyDown(KeyCode.JoystickButton2);
            if (cancelRenamePressed)
            {
                CancelLoopBookmarkRenameFromUi();
                return;
            }

            bool confirmRenamePressed =
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                TryGetButtonDown("Submit") ||
                Input.GetKeyDown(KeyCode.JoystickButton0) ||
                Input.GetKeyDown(KeyCode.JoystickButton7) ||
                Input.GetKeyDown(KeyCode.JoystickButton9);
            if (confirmRenamePressed)
            {
                ConfirmLoopBookmarkRenameFromUi();
                return;
            }

            return;
        }

        if (IsUiBackPressed())
        {
            OpenLoopPausePopup();
            return;
        }

        if (IsPreviewTogglePressed())
        {
            ToggleLoopSettingsPreviewPlayback();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            TrySetLoopStartAtCurrentTime();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            TrySetLoopEndAtCurrentTime();
            return;
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            AddCurrentLoopBookmarkFromUi();
            return;
        }

        if (IsUiUpPressed())
        {
            JumpToAdjacentTabSection(-1);
            return;
        }

        if (IsUiDownPressed())
        {
            JumpToAdjacentTabSection(1);
            return;
        }

        float seekDirection = 0f;
        if (IsUiLeftHeld())
            seekDirection -= 1f;
        if (IsUiRightHeld())
            seekDirection += 1f;

        if (Mathf.Approximately(seekDirection, 0f))
            return;

        SeekSongTimeFromUserNavigation(songTimer + (seekDirection * pauseSeekStepSeconds * Time.deltaTime), false);
    }

    private void HandleLoopPausePopupControls()
    {
        if (IsUiBackPressed())
        {
            CloseLoopPausePopupBackToLoopSettingsFromUi();
            return;
        }

        int horizontalDirection = GetHeldHorizontalArrowDirection();
        if (ConsumeHeldHorizontalUiStep("loop-pause-duration", horizontalDirection))
        {
            AdjustLoopPauseDurationFromUi(horizontalDirection * 1f);
            return;
        }

        if (horizontalDirection == 0)
            ConsumeHeldHorizontalUiStep("loop-pause-duration", 0);

        if (IsUiSubmitPressed())
        {
            ConfirmLoopPausePopupFromUi();
            return;
        }
    }

    private void HandleRhythmPracticeLoopSettingsControls()
    {
        if (IsUiBackPressed())
        {
            StartRhythmPracticeLoopFromUi();
            return;
        }

        if (IsPreviewTogglePressed())
        {
            ToggleLoopSettingsPreviewPlayback();
            return;
        }

        if (IsUiUpPressed())
        {
            MoveArcadePracticeSectionSelectionFromUi(-1);
            return;
        }

        if (IsUiDownPressed())
        {
            MoveArcadePracticeSectionSelectionFromUi(1);
            return;
        }

        if (IsUiSubmitPressed())
        {
            ToggleArcadePracticeSectionSelectionFromUi(selectedArcadePracticeSectionIndex);
            return;
        }
    }

    private void HandleGameModesControls()
    {
        if (HandleGameplayHudPreviewToggleInput())
            return;

        if (IsUiBackPressed())
        {
            CloseGameModesFromUi();
            return;
        }

        if (IsUiUpPressed())
        {
            MoveGameModesSelectionFromUi(-1);
            return;
        }

        if (IsUiDownPressed())
        {
            MoveGameModesSelectionFromUi(1);
            return;
        }

        if (IsUiSubmitPressed())
        {
            ActivateSelectedGameModesActionFromUi();
            return;
        }
    }

    private void HandleHeroModeSettingsControls()
    {
        if (HandleGameplayHudPreviewToggleInput())
            return;

        if (IsUiUpPressed() || IsUiDownPressed())
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

        if (IsUiBackPressed())
        {
            CloseHeroModeSettingsFromUi();
            return;
        }

        if (IsUiSubmitPressed())
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

    private void JumpToAdjacentTabSection(int direction)
    {
        if (direction == 0)
            return;

        float sectionDuration = GetEffectiveTabSectionDuration();
        if (sectionDuration <= 0.05f)
            return;

        int currentSectionIndex = GetSectionIndex(songTimer);
        int targetSectionIndex = Mathf.Max(0, currentSectionIndex + (direction < 0 ? -1 : 1));
        if (targetSectionIndex == currentSectionIndex)
            return;

        float currentSectionStart = currentSectionIndex * sectionDuration;
        float localOffset = Mathf.Clamp(songTimer - currentSectionStart, 0f, Mathf.Max(0f, sectionDuration - 0.0001f));
        float targetTime = (targetSectionIndex * sectionDuration) + localOffset;
        float songEndTime = GetSongEndTimeForLoopEditing();
        targetTime = Mathf.Clamp(targetTime, Mathf.Max(-songStartDelaySeconds, 0f), Mathf.Max(0f, songEndTime));
        SeekSongTimeFromUserNavigation(targetTime, false);
    }

    private void TrySetLoopStartAtCurrentTime()
    {
        float candidateStart = Mathf.Max(0f, songTimer);
        if (loopEndConfigured && candidateStart > loopEndTime - 0.05f)
            return;

        loopStartTime = candidateStart;
        loopStartConfigured = true;
    }

    private void TrySetLoopEndAtCurrentTime()
    {
        float candidateEnd = Mathf.Max(0f, songTimer);
        if (loopStartConfigured && candidateEnd < loopStartTime + 0.05f)
            return;

        loopEndTime = candidateEnd;
        loopEndConfigured = true;
    }

    private bool IsLoopBookmarkScopeAvailable()
    {
        if (currentSongEntry == null)
            return false;

        if (gameplayMode == GuitarGameplayMode.Arcade)
            return !string.IsNullOrWhiteSpace(selectedArcadeArrangementId);

        return !string.IsNullOrWhiteSpace(selectedMusicXmlPartId);
    }

    private string GetCurrentLoopBookmarkScopeKey()
    {
        if (!IsLoopBookmarkScopeAvailable())
            return string.Empty;

        if (gameplayMode == GuitarGameplayMode.Arcade)
            return $"arcade::{selectedArcadeArrangementId?.Trim() ?? string.Empty}";

        if (currentSongEntry != null &&
            currentSongEntry.PrimaryNotationKind == SongNotationSourceKind.Rocksmith &&
            !string.IsNullOrWhiteSpace(selectedMusicXmlPartId))
        {
            string groupId = GetRocksmithGroupId(GetResolvedActiveTrackSummary());
            if (!string.IsNullOrWhiteSpace(groupId))
                return $"guitar::{groupId}";
        }

        return $"guitar::{selectedMusicXmlPartId?.Trim() ?? string.Empty}";
    }

    private string GetCurrentLoopBookmarkScopeDisplayName()
    {
        if (gameplayMode == GuitarGameplayMode.Arcade)
            return GetSelectedArcadeArrangementDisplayName();

        if (currentSongEntry != null && currentSongEntry.PrimaryNotationKind == SongNotationSourceKind.Rocksmith)
        {
            MusicXmlLoader.MusicXmlPartSummary activeSummary = GetResolvedActiveTrackSummary();
            if (activeSummary != null)
                return string.IsNullOrWhiteSpace(activeSummary.GroupDisplayName) ? activeSummary.Name : activeSummary.GroupDisplayName;
        }

        return GetTrackDisplayName(GetCurrentTrackOptionIndex());
    }

    private LoopBookmarkScopeEntry GetLoopBookmarkScopeEntry(SongMetadata metadata, bool createIfMissing)
    {
        if (metadata == null)
            return null;

        if (metadata.loopBookmarkScopes == null)
            metadata.loopBookmarkScopes = new List<LoopBookmarkScopeEntry>();

        string scopeKey = GetCurrentLoopBookmarkScopeKey();
        if (string.IsNullOrWhiteSpace(scopeKey))
            return null;

        LoopBookmarkScopeEntry scope = metadata.loopBookmarkScopes.FirstOrDefault(entry =>
            entry != null && string.Equals(entry.scopeKey ?? string.Empty, scopeKey, StringComparison.OrdinalIgnoreCase));
        if (scope != null)
        {
            if (scope.bookmarks == null)
                scope.bookmarks = new List<LoopBookmarkEntry>();
            scope.scopeDisplayName = GetCurrentLoopBookmarkScopeDisplayName();
            return scope;
        }

        if (!createIfMissing)
            return null;

        scope = new LoopBookmarkScopeEntry
        {
            scopeKey = scopeKey,
            scopeDisplayName = GetCurrentLoopBookmarkScopeDisplayName(),
            bookmarks = new List<LoopBookmarkEntry>()
        };
        metadata.loopBookmarkScopes.Add(scope);
        return scope;
    }

    private List<LoopBookmarkEntry> GetSortedLoopBookmarksForCurrentScope(SongMetadata metadata)
    {
        LoopBookmarkScopeEntry scope = GetLoopBookmarkScopeEntry(metadata, createIfMissing: false);
        if (scope?.bookmarks == null)
            return new List<LoopBookmarkEntry>();

        return scope.bookmarks
            .Where(bookmark => bookmark != null)
            .OrderByDescending(bookmark => bookmark.createdUtcTicks)
            .ThenByDescending(bookmark => bookmark.updatedUtcTicks)
            .ToList();
    }

    private LoopBookmarkEntry GetSelectedLoopBookmarkEntry(SongMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(selectedLoopBookmarkId))
            return null;

        LoopBookmarkScopeEntry scope = GetLoopBookmarkScopeEntry(metadata, createIfMissing: false);
        return scope?.bookmarks?.FirstOrDefault(bookmark =>
            bookmark != null && string.Equals(bookmark.bookmarkId ?? string.Empty, selectedLoopBookmarkId, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsSelectedLoopBookmarkModified()
    {
        LoopBookmarkEntry selected = GetSelectedLoopBookmarkEntry(songMetadata);
        if (selected == null || !loopStartConfigured || !loopEndConfigured)
            return false;

        return Mathf.Abs(selected.loopStartTime - loopStartTime) > 0.0005f ||
               Mathf.Abs(selected.loopEndTime - loopEndTime) > 0.0005f;
    }

    private string BuildDefaultLoopBookmarkName(IEnumerable<LoopBookmarkEntry> bookmarks)
    {
        int nextIndex = 1;
        if (bookmarks != null)
        {
            foreach (LoopBookmarkEntry bookmark in bookmarks)
            {
                if (bookmark == null || string.IsNullOrWhiteSpace(bookmark.name))
                    continue;

                string trimmed = bookmark.name.Trim();
                if (!trimmed.StartsWith("bookmark ", StringComparison.OrdinalIgnoreCase))
                    continue;

                string suffix = trimmed.Substring("bookmark ".Length);
                if (int.TryParse(suffix, out int parsed))
                    nextIndex = Mathf.Max(nextIndex, parsed + 1);
            }
        }

        return $"bookmark {nextIndex}";
    }

    private void EnsureLoopBookmarkSelectionValid()
    {
        if (songMetadata == null || string.IsNullOrWhiteSpace(selectedLoopBookmarkId))
            return;

        if (GetSelectedLoopBookmarkEntry(songMetadata) != null)
            return;

        selectedLoopBookmarkId = string.Empty;
        loopBookmarkRenameActive = false;
        loopBookmarkRenameDraft = string.Empty;
    }

    public void AddCurrentLoopBookmarkFromUi()
    {
        if (!HasConfiguredLoopWindow() || songMetadata == null || !IsLoopBookmarkScopeAvailable())
            return;

        LoopBookmarkScopeEntry scope = GetLoopBookmarkScopeEntry(songMetadata, createIfMissing: true);
        if (scope == null)
            return;

        long nowTicks = DateTime.UtcNow.Ticks;
        LoopBookmarkEntry bookmark = new LoopBookmarkEntry
        {
            bookmarkId = Guid.NewGuid().ToString("N"),
            name = BuildDefaultLoopBookmarkName(scope.bookmarks),
            loopStartTime = loopStartTime,
            loopEndTime = loopEndTime,
            createdUtcTicks = nowTicks,
            updatedUtcTicks = nowTicks
        };
        scope.bookmarks.Add(bookmark);
        selectedLoopBookmarkId = bookmark.bookmarkId;
        loopBookmarkRenameActive = false;
        loopBookmarkRenameDraft = string.Empty;
        SaveSongMetadata();
    }

    public void ActivateLoopCardPrimaryActionFromUi()
    {
        if (UsesRhythmPracticeSectionLoop())
        {
            StartRhythmPracticeLoopFromUi();
            return;
        }

        AddCurrentLoopBookmarkFromUi();
    }

    public void SaveSelectedLoopBookmarkFromUi()
    {
        if (!HasConfiguredLoopWindow() || songMetadata == null)
            return;

        LoopBookmarkEntry selected = GetSelectedLoopBookmarkEntry(songMetadata);
        if (selected == null)
            return;

        selected.loopStartTime = loopStartTime;
        selected.loopEndTime = loopEndTime;
        selected.updatedUtcTicks = DateTime.UtcNow.Ticks;
        SaveSongMetadata();
    }

    public void ActivateLoopCardSecondaryActionFromUi()
    {
        if (UsesRhythmPracticeSectionLoop())
            return;

        SaveSelectedLoopBookmarkFromUi();
    }

    public void SelectLoopBookmarkFromUi(int index)
    {
        if (songMetadata == null)
            return;

        List<LoopBookmarkEntry> bookmarks = GetSortedLoopBookmarksForCurrentScope(songMetadata);
        if (index < 0 || index >= bookmarks.Count)
            return;

        LoopBookmarkEntry bookmark = bookmarks[index];
        if (bookmark == null)
            return;

        selectedLoopBookmarkId = bookmark.bookmarkId ?? string.Empty;
        loopBookmarkRenameActive = false;
        loopBookmarkRenameDraft = string.Empty;
        loopStartTime = Mathf.Max(0f, bookmark.loopStartTime);
        loopEndTime = Mathf.Max(loopStartTime + 0.05f, bookmark.loopEndTime);
        loopStartConfigured = true;
        loopEndConfigured = true;
        SeekSongTimeFromUserNavigation(loopStartTime, false);
    }

    public int GetSelectedLoopBookmarkIndexForUi()
    {
        if (songMetadata == null || string.IsNullOrWhiteSpace(selectedLoopBookmarkId))
            return -1;

        List<LoopBookmarkEntry> bookmarks = GetSortedLoopBookmarksForCurrentScope(songMetadata);
        for (int i = 0; i < bookmarks.Count; i++)
        {
            LoopBookmarkEntry bookmark = bookmarks[i];
            if (bookmark != null && string.Equals(bookmark.bookmarkId ?? string.Empty, selectedLoopBookmarkId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    public void DeleteLoopBookmarkFromUi(int index)
    {
        if (songMetadata == null)
            return;

        List<LoopBookmarkEntry> bookmarks = GetSortedLoopBookmarksForCurrentScope(songMetadata);
        if (index < 0 || index >= bookmarks.Count)
            return;

        LoopBookmarkEntry bookmark = bookmarks[index];
        if (bookmark == null)
            return;

        LoopBookmarkScopeEntry scope = GetLoopBookmarkScopeEntry(songMetadata, createIfMissing: false);
        if (scope?.bookmarks == null)
            return;

        scope.bookmarks.RemoveAll(entry => entry != null && string.Equals(entry.bookmarkId ?? string.Empty, bookmark.bookmarkId ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        if (string.Equals(selectedLoopBookmarkId ?? string.Empty, bookmark.bookmarkId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            selectedLoopBookmarkId = string.Empty;
        loopBookmarkRenameActive = false;
        loopBookmarkRenameDraft = string.Empty;
        SaveSongMetadata();
    }

    public void ActivateLoopCardTertiaryActionFromUi()
    {
        if (UsesRhythmPracticeSectionLoop())
            return;

        BeginLoopBookmarkRenameFromUi();
    }

    public void ActivateLoopCardQuaternaryActionFromUi()
    {
        if (UsesRhythmPracticeSectionLoop())
        {
            ClearArcadePracticeLoopSelectionFromUi();
            return;
        }

        DeleteLoopBookmarkFromUi(GetSelectedLoopBookmarkIndexForUi());
    }

    public void BeginLoopBookmarkRenameFromUi()
    {
        LoopBookmarkEntry selected = GetSelectedLoopBookmarkEntry(songMetadata);
        if (selected == null)
            return;

        loopBookmarkRenameActive = true;
        loopBookmarkRenameDraft = selected.name ?? string.Empty;
    }

    public void SetLoopBookmarkRenameDraftFromUi(string value)
    {
        loopBookmarkRenameDraft = value ?? string.Empty;
    }

    public void ConfirmLoopBookmarkRenameFromUi()
    {
        LoopBookmarkEntry selected = GetSelectedLoopBookmarkEntry(songMetadata);
        if (selected == null)
        {
            CancelLoopBookmarkRenameFromUi();
            return;
        }

        string trimmed = (loopBookmarkRenameDraft ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            trimmed = selected.name ?? BuildDefaultLoopBookmarkName(GetSortedLoopBookmarksForCurrentScope(songMetadata));

        selected.name = trimmed;
        selected.updatedUtcTicks = DateTime.UtcNow.Ticks;
        loopBookmarkRenameActive = false;
        loopBookmarkRenameDraft = trimmed;
        SaveSongMetadata();
    }

    public void CancelLoopBookmarkRenameFromUi()
    {
        loopBookmarkRenameActive = false;
        loopBookmarkRenameDraft = string.Empty;
    }

    private string BuildRhythmPracticeSectionDetailText(ArcadePracticeSectionData section, int index)
    {
        if (section == null)
            return string.Empty;

        string timeRange = $"{FormatPracticeSectionTime(section.startTime)} - {FormatPracticeSectionTime(section.endTime)}";
        if (index == arcadePracticeLoopStartSectionIndex && index == arcadePracticeLoopEndSectionIndex)
            return $"{timeRange}  •  LOOP";
        if (index == arcadePracticeLoopStartSectionIndex)
            return $"{timeRange}  •  START";
        if (index == arcadePracticeLoopEndSectionIndex)
            return $"{timeRange}  •  END";
        if (arcadePracticeLoopStartSectionIndex >= 0 &&
            arcadePracticeLoopEndSectionIndex >= arcadePracticeLoopStartSectionIndex &&
            index > arcadePracticeLoopStartSectionIndex &&
            index < arcadePracticeLoopEndSectionIndex)
        {
            return $"{timeRange}  •  IN RANGE";
        }

        return timeRange;
    }

    private static string FormatPracticeSectionTime(float timeSeconds)
    {
        float clamped = Mathf.Max(0f, timeSeconds);
        int minutes = Mathf.FloorToInt(clamped / 60f);
        float seconds = clamped - (minutes * 60f);
        return FormattableString.Invariant($"{minutes}:{seconds:00.0}");
    }

    private float GetSongEndTimeForLoopEditing()
    {
        float duration = GetSongDurationSeconds();
        if (duration > 0.01f)
            return duration;
        if (chartNotes != null && chartNotes.Count > 0)
            return Mathf.Max(0f, chartNotes.Max(n => n.time + n.duration));
        if (arcadeNoteStates != null && arcadeNoteStates.Count > 0)
            return Mathf.Max(0f, arcadeNoteStates.Max(n => n?.data != null ? n.data.time + n.data.duration : 0f));
        return Mathf.Max(0f, GetEffectiveTabSectionDuration());
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
        offsetHelperSavedSessionScoreValue = currentSessionScoreValue;
        offsetHelperSavedGuitarComboCount = guitarComboCount;
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
        currentSessionScoreValue = offsetHelperSavedSessionScoreValue;
        guitarComboCount = offsetHelperSavedGuitarComboCount;
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
        currentSessionScoreValue = 0;
        currentSessionScorePercent = 0f;
        guitarComboCount = 0;
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
        if (IsUiUpPressed())
        {
            MoveMainMenuSelectionFromUi(-1);
            return;
        }

        if (IsUiDownPressed())
        {
            MoveMainMenuSelectionFromUi(1);
            return;
        }

        if (IsUiSubmitPressed())
        {
            ActivateSelectedMainMenuFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            SetMainMenuSelectionFromUi(0);
            StartFromMainMenuFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            SetMainMenuSelectionFromUi(1);
            OpenSongSelectionFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            SetMainMenuSelectionFromUi(2);
            OpenMultiplayerRhythmSetupFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            SetMainMenuSelectionFromUi(3);
            OpenGlobalSettingsFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            SetMainMenuSelectionFromUi(5);
            OpenToneLabFromUi();
            return;
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            SetMainMenuSelectionFromUi(6);
            ExitGameFromUi();
            return;
        }
    }

    private void HandleStartMenuControls()
    {
        if (IsUiBackPressed())
        {
            if (startMenuStep == StartMenuStep.GuitarSetup || startMenuStep == StartMenuStep.ArcadeSetup)
            {
                startMenuStep = StartMenuStep.SelectMode;
                selectedStartMenuArcadeSetupIndex = 0;
            }
            else
            {
                CloseStartMenuToMainMenuFromUi();
            }

            return;
        }

        if (startMenuStep == StartMenuStep.SelectMode)
        {
            if (IsUiLeftPressed() || IsUiUpPressed())
            {
                MoveStartMenuModeSelection(-1);
                return;
            }

            if (IsUiRightPressed() || IsUiDownPressed())
            {
                MoveStartMenuModeSelection(1);
                return;
            }

            if (IsUiSubmitPressed())
            {
                ActivateStartMenuModeSelection();
                return;
            }

            return;
        }

        if (startMenuStep == StartMenuStep.GuitarSetup)
        {
            if (IsUiUpPressed())
            {
                selectedStartMenuArcadeSetupIndex = WrapIndex(selectedStartMenuArcadeSetupIndex - 1, StartMenuGuitarSetupRowCount);
                return;
            }

            if (IsUiDownPressed())
            {
                selectedStartMenuArcadeSetupIndex = WrapIndex(selectedStartMenuArcadeSetupIndex + 1, StartMenuGuitarSetupRowCount);
                return;
            }

            if (IsUiLeftPressed() || IsUiRightPressed())
            {
                if (selectedStartMenuArcadeSetupIndex == 0)
                    ToggleStartMenuGuitarForceStandardFromUi();
                return;
            }

            if (IsUiSubmitPressed())
            {
                if (selectedStartMenuArcadeSetupIndex == 0)
                    ToggleStartMenuGuitarForceStandardFromUi();
                else
                    ContinueStartMenuGuitarSetupFromUi();
                return;
            }

            return;
        }

        if (IsUiUpPressed())
        {
            MoveStartMenuArcadeSetupSelection(-1);
            return;
        }

        if (IsUiDownPressed())
        {
            MoveStartMenuArcadeSetupSelection(1);
            return;
        }

        if (IsUiLeftPressed())
        {
            AdjustStartMenuArcadeSetupSelection(-1);
            return;
        }

        if (IsUiRightPressed())
        {
            AdjustStartMenuArcadeSetupSelection(1);
            return;
        }

        if (IsUiSubmitPressed())
        {
            ActivateStartMenuArcadeSetupSelection();
            return;
        }
    }

    private void HandleMultiplayerRhythmSetupControls()
    {
        if (HandleActiveMultiplayerRhythmSetupCapture())
            return;

        if (IsUiBackPressed())
        {
            CloseMultiplayerRhythmSetupToMainMenuFromUi();
            return;
        }

        if (IsUiUpPressed())
        {
            selectedMultiplayerRhythmSetupIndex = WrapIndex(selectedMultiplayerRhythmSetupIndex - 1, MultiplayerRhythmSetupRowCount);
            return;
        }

        if (IsUiDownPressed())
        {
            selectedMultiplayerRhythmSetupIndex = WrapIndex(selectedMultiplayerRhythmSetupIndex + 1, MultiplayerRhythmSetupRowCount);
            return;
        }

        if (IsUiSubmitPressed())
        {
            ActivateMultiplayerRhythmSetupSelection();
            return;
        }
    }

    private void InitializeMultiplayerRhythmPlayers()
    {
        for (int i = 0; i < multiplayerRhythmPlayers.Length; i++)
        {
            multiplayerRhythmPlayers[i] = new MultiplayerRhythmPlayerState
            {
                playerIndex = i
            };
        }
    }

    private void RefreshMultiplayerRhythmAvailableDevices()
    {
        multiplayerRhythmAvailableDevices.Clear();
        multiplayerRhythmAvailableDevices.Add(new MultiplayerRhythmInputAssignment
        {
            kind = MultiplayerRhythmInputDeviceKind.Keyboard,
            controllerSlot = 0,
            displayName = "Keyboard"
        });

        string[] joystickNames = Input.GetJoystickNames();
        for (int i = 0; i < Mathf.Min(ArcadeControllerSlotCount, joystickNames.Length); i++)
        {
            string rawName = joystickNames[i];
            string displayName = string.IsNullOrWhiteSpace(rawName)
                ? $"Controller {i + 1}"
                : $"Controller {i + 1}  {rawName.Trim()}";

            multiplayerRhythmAvailableDevices.Add(new MultiplayerRhythmInputAssignment
            {
                kind = MultiplayerRhythmInputDeviceKind.Controller,
                controllerSlot = i + 1,
                displayName = displayName
            });
        }

        selectedMultiplayerRhythmPlayerOneDeviceIndex = Mathf.Clamp(selectedMultiplayerRhythmPlayerOneDeviceIndex, 0, Mathf.Max(0, multiplayerRhythmAvailableDevices.Count - 1));
        selectedMultiplayerRhythmPlayerTwoDeviceIndex = Mathf.Clamp(selectedMultiplayerRhythmPlayerTwoDeviceIndex, 0, Mathf.Max(0, multiplayerRhythmAvailableDevices.Count - 1));

        if (selectedMultiplayerRhythmPlayerTwoDeviceIndex == selectedMultiplayerRhythmPlayerOneDeviceIndex && multiplayerRhythmAvailableDevices.Count > 1)
            selectedMultiplayerRhythmPlayerTwoDeviceIndex = (selectedMultiplayerRhythmPlayerOneDeviceIndex + 1) % multiplayerRhythmAvailableDevices.Count;
    }

    private void BeginMultiplayerRhythmSetupCapture(int playerIndex)
    {
        activeMultiplayerRhythmSetupCapturePlayerIndex = Mathf.Clamp(playerIndex, 0, 1);
        activeMultiplayerRhythmSetupCaptureStartFrame = Time.frameCount;
    }

    private void CancelMultiplayerRhythmSetupCapture()
    {
        activeMultiplayerRhythmSetupCapturePlayerIndex = -1;
        activeMultiplayerRhythmSetupCaptureStartFrame = -1;
    }

    private bool HandleActiveMultiplayerRhythmSetupCapture()
    {
        if (activeMultiplayerRhythmSetupCapturePlayerIndex < 0)
            return false;

        if (Time.frameCount <= activeMultiplayerRhythmSetupCaptureStartFrame)
            return true;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelMultiplayerRhythmSetupCapture();
            return true;
        }

        if (!TryGetPressedMultiplayerRhythmAssignment(out MultiplayerRhythmInputAssignment assignment))
            return true;

        RefreshMultiplayerRhythmAvailableDevices();
        int deviceIndex = FindMultiplayerRhythmAssignmentIndex(assignment);
        if (deviceIndex >= 0)
        {
            if (activeMultiplayerRhythmSetupCapturePlayerIndex == 0)
                SetMultiplayerRhythmPlayerOneDeviceFromUi(deviceIndex);
            else
                SetMultiplayerRhythmPlayerTwoDeviceFromUi(deviceIndex);
        }

        CancelMultiplayerRhythmSetupCapture();
        return true;
    }

    private bool TryGetPressedMultiplayerRhythmAssignment(out MultiplayerRhythmInputAssignment assignment)
    {
        if (TryGetPressedMultiplayerRhythmControllerAssignment(out assignment))
            return true;

        if (TryGetPressedMultiplayerRhythmKeyboardAssignment(out assignment))
            return true;

        assignment = null;
        return false;
    }

    private bool TryGetPressedMultiplayerRhythmControllerAssignment(out MultiplayerRhythmInputAssignment assignment)
    {
        for (int slot = 1; slot <= ArcadeControllerSlotCount; slot++)
        {
            for (int button = 0; button < MultiplayerRhythmSetupControllerButtonScanCount; button++)
            {
                if (!Enum.TryParse($"Joystick{slot}Button{button}", true, out KeyCode candidate))
                    continue;

                if (!Input.GetKeyDown(candidate))
                    continue;

                assignment = new MultiplayerRhythmInputAssignment
                {
                    kind = MultiplayerRhythmInputDeviceKind.Controller,
                    controllerSlot = slot,
                    displayName = GetMultiplayerRhythmControllerDisplayName(slot)
                };
                return true;
            }

            if (DidSpecificControllerSetupAxisActivate(slot))
            {
                assignment = new MultiplayerRhythmInputAssignment
                {
                    kind = MultiplayerRhythmInputDeviceKind.Controller,
                    controllerSlot = slot,
                    displayName = GetMultiplayerRhythmControllerDisplayName(slot)
                };
                return true;
            }
        }

        assignment = null;
        return false;
    }

    private static bool TryGetPressedMultiplayerRhythmKeyboardAssignment(out MultiplayerRhythmInputAssignment assignment)
    {
        Array allCodes = Enum.GetValues(typeof(KeyCode));
        for (int i = 0; i < allCodes.Length; i++)
        {
            KeyCode candidate = (KeyCode)allCodes.GetValue(i);
            if (candidate == KeyCode.None || !Input.GetKeyDown(candidate))
                continue;

            if (!IsArcadeKeyboardBindingKeyCode(candidate))
                continue;

            assignment = new MultiplayerRhythmInputAssignment
            {
                kind = MultiplayerRhythmInputDeviceKind.Keyboard,
                controllerSlot = 0,
                displayName = "Keyboard"
            };
            return true;
        }

        assignment = null;
        return false;
    }

    private bool DidSpecificControllerSetupAxisActivate(int controllerSlot)
    {
        if (controllerSlot <= 0)
            return false;

        int slotIndex = controllerSlot - 1;
        if (slotIndex < 0 || slotIndex >= ArcadeControllerSlotCount)
            return false;

        RefreshSpecificControllerExtendedAxes();
        return (previousSpecificControllerLeftTriggerAxes[slotIndex] < ControllerTriggerAxisPressThreshold &&
                currentSpecificControllerLeftTriggerAxes[slotIndex] >= ControllerTriggerAxisPressThreshold) ||
               (previousSpecificControllerRightTriggerAxes[slotIndex] < ControllerTriggerAxisPressThreshold &&
                currentSpecificControllerRightTriggerAxes[slotIndex] >= ControllerTriggerAxisPressThreshold) ||
               CrossedUiAxisThreshold(previousSpecificControllerStrumAxes[slotIndex], currentSpecificControllerStrumAxes[slotIndex], 1) ||
               CrossedUiAxisThreshold(previousSpecificControllerStrumAxes[slotIndex], currentSpecificControllerStrumAxes[slotIndex], -1);
    }

    private string GetMultiplayerRhythmControllerDisplayName(int controllerSlot)
    {
        string[] joystickNames = Input.GetJoystickNames();
        int index = Mathf.Clamp(controllerSlot - 1, 0, ArcadeControllerSlotCount - 1);
        string rawName = joystickNames != null && index < joystickNames.Length ? joystickNames[index] : string.Empty;
        return string.IsNullOrWhiteSpace(rawName)
            ? $"Controller {controllerSlot}"
            : $"Controller {controllerSlot}  {rawName.Trim()}";
    }

    private int FindMultiplayerRhythmAssignmentIndex(MultiplayerRhythmInputAssignment assignment)
    {
        if (assignment == null)
            return -1;

        for (int i = 0; i < multiplayerRhythmAvailableDevices.Count; i++)
        {
            MultiplayerRhythmInputAssignment candidate = multiplayerRhythmAvailableDevices[i];
            if (candidate == null)
                continue;

            if (candidate.kind == assignment.kind && candidate.controllerSlot == assignment.controllerSlot)
                return i;
        }

        return -1;
    }

    private bool CanStartMultiplayerRhythm()
    {
        return multiplayerRhythmAvailableDevices.Count >= 2 &&
               selectedMultiplayerRhythmPlayerOneDeviceIndex >= 0 &&
               selectedMultiplayerRhythmPlayerTwoDeviceIndex >= 0 &&
               selectedMultiplayerRhythmPlayerOneDeviceIndex < multiplayerRhythmAvailableDevices.Count &&
               selectedMultiplayerRhythmPlayerTwoDeviceIndex < multiplayerRhythmAvailableDevices.Count &&
               selectedMultiplayerRhythmPlayerOneDeviceIndex != selectedMultiplayerRhythmPlayerTwoDeviceIndex;
    }

    private string GetMultiplayerRhythmSetupStatusText()
    {
        if (activeMultiplayerRhythmSetupCapturePlayerIndex == 0)
            return "Player 1: press any controller button or keyboard key. Esc cancels.";
        if (activeMultiplayerRhythmSetupCapturePlayerIndex == 1)
            return "Player 2: press any controller button or keyboard key. Esc cancels.";
        if (multiplayerRhythmAvailableDevices.Count < 2)
            return "Connect at least two distinct devices to start multiplayer.";
        if (selectedMultiplayerRhythmPlayerOneDeviceIndex == selectedMultiplayerRhythmPlayerTwoDeviceIndex)
            return "Player 1 and Player 2 must use different devices.";
        return "Both players share the same Rhythm settings. Multiplayer never saves scores or records.";
    }

    private string GetSelectedMultiplayerRhythmSetupLabel(int playerIndex)
    {
        int selectedIndex = playerIndex == 0
            ? selectedMultiplayerRhythmPlayerOneDeviceIndex
            : selectedMultiplayerRhythmPlayerTwoDeviceIndex;

        string currentLabel = selectedIndex >= 0 && selectedIndex < multiplayerRhythmAvailableDevices.Count
            ? multiplayerRhythmAvailableDevices[selectedIndex].displayName
            : "Not Set";

        if (activeMultiplayerRhythmSetupCapturePlayerIndex == playerIndex)
            return "Press any controller button";

        return $"Press to setup  •  {currentLabel}";
    }

    private void AdjustMultiplayerRhythmSetupSelection(int delta)
    {
        if (delta == 0 || multiplayerRhythmAvailableDevices.Count <= 0)
            return;

        if (selectedMultiplayerRhythmSetupIndex == 0)
        {
            selectedMultiplayerRhythmPlayerOneDeviceIndex = WrapIndex(selectedMultiplayerRhythmPlayerOneDeviceIndex + delta, multiplayerRhythmAvailableDevices.Count);
            if (selectedMultiplayerRhythmPlayerOneDeviceIndex == selectedMultiplayerRhythmPlayerTwoDeviceIndex && multiplayerRhythmAvailableDevices.Count > 1)
                selectedMultiplayerRhythmPlayerOneDeviceIndex = WrapIndex(selectedMultiplayerRhythmPlayerOneDeviceIndex + delta, multiplayerRhythmAvailableDevices.Count);
            return;
        }

        if (selectedMultiplayerRhythmSetupIndex == 1)
        {
            selectedMultiplayerRhythmPlayerTwoDeviceIndex = WrapIndex(selectedMultiplayerRhythmPlayerTwoDeviceIndex + delta, multiplayerRhythmAvailableDevices.Count);
            if (selectedMultiplayerRhythmPlayerTwoDeviceIndex == selectedMultiplayerRhythmPlayerOneDeviceIndex && multiplayerRhythmAvailableDevices.Count > 1)
                selectedMultiplayerRhythmPlayerTwoDeviceIndex = WrapIndex(selectedMultiplayerRhythmPlayerTwoDeviceIndex + delta, multiplayerRhythmAvailableDevices.Count);
        }
    }

    private void ActivateMultiplayerRhythmSetupSelection()
    {
        if (selectedMultiplayerRhythmSetupIndex == 0)
        {
            BeginMultiplayerRhythmSetupCapture(0);
            return;
        }

        if (selectedMultiplayerRhythmSetupIndex == 1)
        {
            BeginMultiplayerRhythmSetupCapture(1);
            return;
        }

        ContinueMultiplayerRhythmSetupFromUi();
    }

    private void HandleNotesDetectorTestControls()
    {
        if (showNotesDetectorTestSelectionPopup)
        {
            if (IsUiBackPressed())
            {
                CloseNotesDetectorTestSelectionPopupFromUi();
                return;
            }

            if (IsUiUpPressed())
            {
                MoveNotesDetectorTestPopupSelectionFromUi(-1);
                return;
            }

            if (IsUiDownPressed())
            {
                MoveNotesDetectorTestPopupSelectionFromUi(1);
                return;
            }

            if (IsUiSubmitPressed())
            {
                ActivateSelectedNotesDetectorTestPopupFromUi();
                return;
            }

            return;
        }

        if (showNotesDetectorRoutinePopup)
        {
            UpdateNotesDetectorRoutine();
            if (IsUiBackPressed())
                CloseNotesDetectorRoutineFromUi();
            return;
        }

        if (IsUiUpPressed())
        {
            MoveNotesDetectorTestSelectionFromUi(-1);
            return;
        }

        if (IsUiDownPressed())
        {
            MoveNotesDetectorTestSelectionFromUi(1);
            return;
        }

        if (IsUiSubmitPressed())
        {
            ActivateSelectedNotesDetectorTestActionFromUi();
            return;
        }

        if (IsUiBackPressed())
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
            : DetectorRoutineMatchesTargets(currentStep);

        if (currentStep.RequireSilence)
        {
            if (matched)
            {
                if (notesDetectorRoutineMatchedSinceTime < 0f)
                    notesDetectorRoutineMatchedSinceTime = Time.unscaledTime;

                if (Time.unscaledTime - notesDetectorRoutineMatchedSinceTime >= 0.35f)
                {
                    AdvanceNotesDetectorRoutineStep();
                }
            }
            else
            {
                notesDetectorRoutineMatchedSinceTime = -1f;
            }
        }
        else
        {
            if (matched)
            {
                if (notesDetectorRoutineMatchedSinceTime < 0f)
                    notesDetectorRoutineMatchedSinceTime = Time.unscaledTime;
            }
            else
            {
                notesDetectorRoutineMatchedSinceTime = -1f;
            }

            if (notesDetectorRoutineMatchedSinceTime >= 0f &&
                Time.unscaledTime - notesDetectorRoutineMatchedSinceTime >= NotesDetectorRoutineTargetConfirmSeconds)
            {
                AdvanceNotesDetectorRoutineStep();
            }
        }
    }

    private bool DetectorRoutineMatchesSilence()
    {
        bool fastSilent = latestDetectedPitches == null || latestDetectedPitches.Count == 0;
        bool aiSilent = string.IsNullOrWhiteSpace(latestEventNotesText) || latestEventNotesText == "--";
        return fastSilent && aiSilent;
    }

    private void ResetLiveDetectorReadState()
    {
        latestDetectedPitches.Clear();
        recentNoteEvents.Clear();
        latestEventNotesText = "--";
        latestNoteEventId = 0;
        latestPacketHadEvent = false;
        latestParsedInputLevel = -1f;
        logNotes = string.Empty;
        Interlocked.Exchange(ref lastUdpPacketUtcTicks, 0L);
    }

    private void AdvanceNotesDetectorRoutineStep()
    {
        notesDetectorRoutineStageIndex++;
        notesDetectorRoutineMatchedSinceTime = -1f;
        ResetLiveDetectorReadState();
        MarkDetectorHintDirty();
    }

    private bool DetectorRoutineMatchesTargets(NotesDetectorRoutineStep step)
    {
        int[] targetMidis = GetNotesDetectorRoutineExpectedMidis(step);
        if (targetMidis == null || targetMidis.Length == 0)
            return false;

        HashSet<int> combinedPitches = BuildLatestDetectorCombinedPitches();

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

    private List<int> GetNotesDetectorRoutineTabRowStates()
    {
        List<int> states = new List<int>(6);
        for (int i = 0; i < 6; i++)
            states.Add(-1);

        if (!showNotesDetectorRoutinePopup)
            return states;

        if (notesDetectorRoutineStageIndex < 0 || notesDetectorRoutineStageIndex >= notesDetectorRoutineSteps.Count)
            return states;

        NotesDetectorRoutineStep currentStep = notesDetectorRoutineSteps[notesDetectorRoutineStageIndex];
        if (currentStep == null || currentStep.RequireSilence)
            return states;

        string[] frets = currentStep.TabFretsTopDown ?? CreateNotesDetectorRoutineTabFrets("-", "-", "-", "-", "-", "-");
        HashSet<int> detectedPitches = BuildLatestDetectorCombinedPitches();
        for (int i = 0; i < 6; i++)
        {
            string fret = i < frets.Length ? frets[i] : "-";
            if (!TryGetNotesDetectorRoutineRowMidi(i, fret, out int targetMidi))
                continue;

            states[i] = detectedPitches.Contains(targetMidi) ? 1 : 0;
        }

        return states;
    }

    private HashSet<int> BuildLatestDetectorCombinedPitches()
    {
        HashSet<int> combinedPitches = new HashSet<int>();
        if (latestDetectedPitches != null)
            combinedPitches.UnionWith(latestDetectedPitches);

        if (!string.IsNullOrWhiteSpace(latestEventNotesText) && latestEventNotesText != "--")
            ParseNoteCsvIntoSet(latestEventNotesText, combinedPitches);

        float recentCutoff = Time.unscaledTime - NotesDetectorRoutineRecentEventHoldSeconds;
        for (int i = recentNoteEvents.Count - 1; i >= 0; i--)
        {
            NoteEvent ev = recentNoteEvents[i];
            if (ev == null)
                continue;
            if (ev.observedAtUnscaled < recentCutoff)
                break;
            if (ev.observedAtUnscaled + 0.0001f < notesDetectorRoutineOpenedTime)
                continue;

            combinedPitches.UnionWith(ev.pitches);
        }

        return combinedPitches;
    }

    private static bool TryGetNotesDetectorRoutineRowMidi(int rowIndexTopDown, string fretText, out int midi)
    {
        midi = -1;
        if (rowIndexTopDown < 0 || rowIndexTopDown >= 6)
            return false;

        fretText = string.IsNullOrWhiteSpace(fretText) ? "-" : fretText.Trim();
        if (fretText == "-" || !int.TryParse(fretText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int fret) || fret < 0)
            return false;

        int[] topDownStandardTuning = { 64, 59, 55, 50, 45, 40 };
        midi = topDownStandardTuning[rowIndexTopDown] + fret;
        return true;
    }

    private int[] GetNotesDetectorRoutineExpectedMidis(NotesDetectorRoutineStep step)
    {
        if (step == null)
            return Array.Empty<int>();

        var rowDerivedMidis = new HashSet<int>();
        string[] frets = step.TabFretsTopDown;
        if (frets != null)
        {
            int rowCount = Mathf.Min(6, frets.Length);
            for (int i = 0; i < rowCount; i++)
            {
                if (TryGetNotesDetectorRoutineRowMidi(i, frets[i], out int rowMidi))
                    rowDerivedMidis.Add(rowMidi);
            }
        }

        int[] configuredMidis = step.TargetMidis == null
            ? Array.Empty<int>()
            : step.TargetMidis
                .Where(midi => midi >= 0)
                .Distinct()
                .OrderBy(midi => midi)
                .ToArray();

        if (rowDerivedMidis.Count > 0)
            return rowDerivedMidis.OrderBy(midi => midi).ToArray();

        return configuredMidis;
    }

    private void HandleToneLabControls()
    {
        if (unityToneLabOverlay != null && unityToneLabOverlay.IsCapturingKeyboardInput)
            return;

        if (IsUiBackPressed() || Input.GetKeyDown(KeyCode.T))
        {
            CloseToneLabFromUi();
            return;
        }
    }

    private void HandleSongSelectionControls()
    {
        if (IsUiBackPressed() || Input.GetKeyDown(KeyCode.L))
        {
            CloseSongSelectionFromUi();
            return;
        }

        if (displayedSongLibraryEntries.Count == 0)
            return;

        if (!songSelectionSongConfirmed)
        {
            if (IsUiUpPressed())
                MoveSongSelection(-1);
            else if (IsUiDownPressed())
                MoveSongSelection(1);

            if (IsUiLeftPressed())
            {
                if (IsSongLibraryScopeActive())
                {
                    songLibraryBrowseScopeKey = string.Empty;
                    RebuildDisplayedSongLibraryEntries();
                }
                else if (songLibraryBrowseMode > SongLibraryBrowseMode.All)
                {
                    SetSongLibraryBrowseMode((SongLibraryBrowseMode)((int)songLibraryBrowseMode - 1));
                }

                return;
            }

            if (IsUiRightPressed())
            {
                if (!IsSongLibraryScopeActive() && songLibraryBrowseMode < SongLibraryBrowseMode.Albums)
                    SetSongLibraryBrowseMode((SongLibraryBrowseMode)((int)songLibraryBrowseMode + 1));
                return;
            }

            if (IsUiSubmitPressed())
                ActivateSelectedSongLibraryEntry();

            return;
        }

        if (IsUiRightPressed())
        {
            if (pendingTrackSelectionSong != null && pendingTrackSelectionSong.LibraryType == SongLibraryType.Arcade)
                MoveArcadeDifficultySelection(1);
            else
                songSelectionSongConfirmed = false;
            return;
        }

        if (IsUiLeftPressed() && pendingTrackSelectionSong != null && pendingTrackSelectionSong.LibraryType == SongLibraryType.Arcade)
        {
            MoveArcadeDifficultySelection(-1);
            return;
        }

        if (IsUiUpPressed())
            MoveTrackSelectionInMenu(-1);
        else if (IsUiDownPressed())
            MoveTrackSelectionInMenu(1);

        if (IsUiSubmitPressed())
            ConfirmTrackSelection();
    }

    private void HandleTrackSelectionControls()
    {
        if (IsUiBackPressed())
        {
            CloseTrackSelection();
            return;
        }

        if (pendingTrackSelectionParts.Count == 0)
            return;

        if (IsUiUpPressed())
            MoveTrackSelectionInMenu(-1);
        else if (IsUiDownPressed())
            MoveTrackSelectionInMenu(1);

        if (IsUiSubmitPressed())
            ConfirmTrackSelection();
    }

    private void HandleSongSettingsControls()
    {
        if (HandleGameplayHudPreviewToggleInput())
            return;

        if (showGeneratedAudioTrackSelectionPopup || showSongSettingsTrackSelectionPopup)
        {
            if (IsUiBackPressed())
            {
                if (showGeneratedAudioTrackSelectionPopup)
                    CloseGeneratedAudioTrackSelectionFromUi();
                else
                    CloseSongSettingsTrackSelectionPopupFromUi();
                return;
            }

            if (IsUiUpPressed())
            {
                if (showGeneratedAudioTrackSelectionPopup)
                    MoveGeneratedAudioTrackSelectionFromUi(-1);
                else
                    MoveSongSettingsTrackSelectionPopupFromUi(-1);
                return;
            }

            if (IsUiDownPressed())
            {
                if (showGeneratedAudioTrackSelectionPopup)
                    MoveGeneratedAudioTrackSelectionFromUi(1);
                else
                    MoveSongSettingsTrackSelectionPopupFromUi(1);
                return;
            }

            if (IsUiSubmitPressed())
            {
                if (showGeneratedAudioTrackSelectionPopup)
                    ActivateSelectedGeneratedAudioTrackSelectionFromUi();
                else
                    ActivateSelectedSongSettingsTrackSelectionPopupFromUi();
                return;
            }
        }

        if (IsUiBackPressed())
        {
            showSongSettings = false;
            showGeneratedAudioTrackSelectionPopup = false;
            showSongSettingsTrackSelectionPopup = false;
            isPaused = true;
            SyncAudioToSongTimer(playImmediately: false);
            return;
        }

        if (IsUiUpPressed())
        {
            MoveSongSettingsSelectionFromUi(-1);
            return;
        }

        if (IsUiDownPressed())
        {
            MoveSongSettingsSelectionFromUi(1);
            return;
        }

        if (IsUiSubmitPressed())
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
            if (selectedSongSettingsIndex == 6 && IsSongSettingsOptionSelectable(selectedSongSettingsIndex) && IsUiLeftPressed())
            {
                AdjustSelectedSongSettingFromUi(-1);
                return;
            }

            if (selectedSongSettingsIndex == 6 && IsSongSettingsOptionSelectable(selectedSongSettingsIndex) && IsUiRightPressed())
            {
                AdjustSelectedSongSettingFromUi(1);
                return;
            }
        }
    }

    private void HandleOffsetHelperControls()
    {
        if (IsUiBackPressed())
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
            if (IsUiLeftPressed() || IsUiUpPressed())
            {
                moveDelta = -1;
            }
            else if (IsUiRightPressed() || IsUiDownPressed())
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

            if (IsUiSubmitPressed())
            {
                StartOffsetHelperAdjustMode();
                return;
            }

            return;
        }

        if (IsPreviewTogglePressed())
        {
            ToggleOffsetHelperPreviewPlayback();
            return;
        }

        if (IsUiSubmitPressed())
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
        if (IsUiUpPressed() || IsUiLeftPressed())
        {
            MoveSongEndActionSelectionFromUi(-1);
            return;
        }

        if (IsUiDownPressed() || IsUiRightPressed())
        {
            MoveSongEndActionSelectionFromUi(1);
            return;
        }

        if (IsUiSubmitPressed())
        {
            ActivateSelectedSongEndActionFromUi();
            return;
        }
    }

    private bool HandleGameplayHudPreviewToggleInput()
    {
        if (!Input.GetKeyDown(KeyCode.P))
            return false;

        gameplayHudPreviewInMenus = !gameplayHudPreviewInMenus;
        return true;
    }

    private void HandleGlobalSettingsControls()
    {
        if (HandleArcadeBindingCaptureInput())
            return;

        if (Input.GetKeyDown(KeyCode.O))
        {
            globalSettingsTransparentBackground = !globalSettingsTransparentBackground;
            return;
        }

        if (HandleGameplayHudPreviewToggleInput())
            return;

        if (IsUiBackPressed() || Input.GetKeyDown(KeyCode.G))
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

        if (IsUiUpPressed())
        {
            if (string.IsNullOrEmpty(activeGlobalSettingsCategory))
                selectedGlobalSettingsTopIndex = (selectedGlobalSettingsTopIndex + GlobalSettingsTopLevelCount - 1) % GlobalSettingsTopLevelCount;
            else
                MoveGlobalSettingsItemSelection(-1);
            return;
        }

        if (IsUiDownPressed())
        {
            if (string.IsNullOrEmpty(activeGlobalSettingsCategory))
                selectedGlobalSettingsTopIndex = (selectedGlobalSettingsTopIndex + 1) % GlobalSettingsTopLevelCount;
            else
                MoveGlobalSettingsItemSelection(1);
            return;
        }

        if (IsUiLeftPressed())
        {
            AdjustCurrentGlobalSettingsValue(-1);
            return;
        }

        if (IsUiRightPressed())
        {
            AdjustCurrentGlobalSettingsValue(1);
            return;
        }

        if (IsUiSubmitPressed())
        {
            ActivateCurrentGlobalSettingsSelection();
            return;
        }
    }

    private int GetTrackOptionCount()
    {
        return 1 + GetCurrentTrackSelectionOptionCount();
    }

    private int GetSongSettingsTrackPopupOptionCount()
    {
        return GetCurrentTrackSelectionOptionCount();
    }

    private int GetCurrentTrackOptionIndex()
    {
        if (useAutoTrackSelection)
            return 0;

        if (string.IsNullOrEmpty(selectedMusicXmlPartId))
            return 0;

        if (IsCurrentRocksmithTrackGroupingActive())
        {
            string selectedGroupId = GetRocksmithGroupIdFromPartId(selectedMusicXmlPartId);
            List<RocksmithTrackSelectionGroup> groups = GetCurrentTrackSelectionGroupsOrdered();
            for (int i = 0; i < groups.Count; i++)
            {
                if (string.Equals(groups[i].GroupId, selectedGroupId, StringComparison.OrdinalIgnoreCase))
                    return i + 1;
            }
        }
        else
        {
            for (int i = 0; i < currentSongPartSummaries.Count; i++)
            {
                if (string.Equals(currentSongPartSummaries[i].PartId, selectedMusicXmlPartId, StringComparison.OrdinalIgnoreCase))
                    return i + 1;
            }
        }

        return 0;
    }

    private string GetTrackDisplayName(int optionIndex)
    {
        if (optionIndex <= 0)
        {
            if (GetCurrentTrackSelectionOptionCount() == 0)
                return "Auto";

            if (IsCurrentRocksmithTrackGroupingActive())
            {
                RocksmithTrackSelectionGroup bestGroup = GetCurrentTrackSelectionGroupsOrdered()
                    .FirstOrDefault();
                return bestGroup == null ? "Auto" : $"Auto ({bestGroup.DisplayName})";
            }

            MusicXmlLoader.MusicXmlPartSummary best = currentSongPartSummaries.OrderByDescending(s => s.Score).First();
            return $"Auto ({best.Name})";
        }

        if (IsCurrentRocksmithTrackGroupingActive())
        {
            int groupIndex = optionIndex - 1;
            List<RocksmithTrackSelectionGroup> groups = GetCurrentTrackSelectionGroupsOrdered();
            if (groupIndex < 0 || groupIndex >= groups.Count)
                return "Auto";

            return groups[groupIndex].DisplayName ?? "--";
        }

        int summaryIndex = optionIndex - 1;
        if (summaryIndex < 0 || summaryIndex >= currentSongPartSummaries.Count)
            return "Auto";

        MusicXmlLoader.MusicXmlPartSummary summary = currentSongPartSummaries[summaryIndex];
        return $"{summary.Name}  [notes:{summary.NoteCount} tab:{summary.TabCount}]";
    }

    private MusicXmlLoader.MusicXmlPartSummary GetResolvedActiveTrackSummary()
    {
        if (currentSongPartSummaries == null || currentSongPartSummaries.Count == 0)
            return null;

        if (!useAutoTrackSelection && !string.IsNullOrEmpty(selectedMusicXmlPartId))
        {
            MusicXmlLoader.MusicXmlPartSummary selected = currentSongPartSummaries
                .FirstOrDefault(summary => string.Equals(summary.PartId, selectedMusicXmlPartId, StringComparison.OrdinalIgnoreCase));
            if (selected != null)
                return selected;
        }

        return currentSongPartSummaries
            .OrderByDescending(summary => summary.Score)
            .FirstOrDefault();
    }

    private MusicXmlLoader.MusicXmlPartSummary GetPendingSelectedTrackSummary()
    {
        if (IsPendingRocksmithDifficultySelectionActive())
            return GetHighestDifficultyRocksmithVariant(GetPendingSelectedRocksmithTrackGroup()?.Variants);

        if (pendingTrackSelectionParts == null || pendingTrackSelectionParts.Count == 0)
            return null;

        int selectedIndex = Mathf.Clamp(selectedTrackListIndex, 0, pendingTrackSelectionParts.Count - 1);
        return pendingTrackSelectionParts[selectedIndex];
    }

    private int GetPendingTrackSelectionDisplayCount()
    {
        if (pendingTrackSelectionSong != null && pendingTrackSelectionSong.LibraryType == SongLibraryType.Arcade)
            return pendingArcadeArrangementSummaries.Count;

        if (IsPendingRocksmithDifficultySelectionActive())
            return pendingRocksmithTrackSelectionGroups.Count;

        return pendingTrackSelectionParts.Count;
    }

    private string GetPendingTrackSelectionDisplayName(int index)
    {
        if (IsPendingRocksmithDifficultySelectionActive())
        {
            if (index < 0 || index >= pendingRocksmithTrackSelectionGroups.Count)
                return "--";

            return pendingRocksmithTrackSelectionGroups[index].DisplayName ?? "--";
        }

        if (index < 0 || index >= pendingTrackSelectionParts.Count)
            return "--";

        return pendingTrackSelectionParts[index].Name ?? "--";
    }

    private string GetPendingTrackSelectionMetaText(int index)
    {
        if (IsPendingRocksmithDifficultySelectionActive())
        {
            if (index < 0 || index >= pendingRocksmithTrackSelectionGroups.Count)
                return string.Empty;

            RocksmithTrackSelectionGroup group = pendingRocksmithTrackSelectionGroups[index];
            return string.IsNullOrWhiteSpace(group.TuningDisplayName)
                ? "Select this arrangement"
                : $"Tuning: {group.TuningDisplayName}";
#if false
            string difficulties = BuildRocksmithDifficultySummary(group.Variants);
            if (string.IsNullOrWhiteSpace(difficulties))
                return string.IsNullOrWhiteSpace(group.TuningDisplayName) ? "Select this arrangement" : $"Tuning: {group.TuningDisplayName}";

            return string.IsNullOrWhiteSpace(group.TuningDisplayName)
                ? $"Difficulties: {difficulties}"
                : $"Difficulties: {difficulties}  •  Tuning: {group.TuningDisplayName}";
#endif
        }

        return string.Empty;
    }

    private void RefreshActiveTrackTuning()
    {
        MusicXmlLoader.MusicXmlPartSummary activeTrack = GetResolvedActiveTrackSummary();
        activeStringBasePitch = forceStandardTuning
            ? GetPreferredStandardTuningForTrack(activeTrack)
            : StringTuningUtils.CloneOrDefault(activeTrack?.StringTuningPitches, IsBassLikeTrackSummary(activeTrack));
    }

    private static bool IsBassLikeTrackSummary(MusicXmlLoader.MusicXmlPartSummary summary)
    {
        if (summary == null)
            return false;

        if (summary.StringTuningPitches != null && summary.StringTuningPitches.Length > 0 && summary.StringTuningPitches.Length <= 4)
            return true;

        string label = !string.IsNullOrWhiteSpace(summary.GroupDisplayName)
            ? summary.GroupDisplayName
            : !string.IsNullOrWhiteSpace(summary.Name)
                ? summary.Name
                : summary.PartId ?? string.Empty;
        return label.IndexOf("bass", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int[] GetPreferredStandardTuningForTrack(MusicXmlLoader.MusicXmlPartSummary summary)
    {
        return IsBassLikeTrackSummary(summary)
            ? (int[])StringTuningUtils.StandardBassTuning.Clone()
            : (int[])StringTuningUtils.StandardGuitarTuning.Clone();
    }

    private string GetResolvedActiveTrackTuningLabel()
    {
        MusicXmlLoader.MusicXmlPartSummary activeTrack = GetResolvedActiveTrackSummary();
        if (activeTrack == null)
            return string.Empty;

        if (activeTrack != null && !string.IsNullOrWhiteSpace(activeTrack.TuningDisplayName))
            return activeTrack.TuningDisplayName;

        return StringTuningUtils.FormatTuningDisplayName(activeTrack?.StringTuningPitches);
    }

    private string GetPendingTrackTuningLabel()
    {
        MusicXmlLoader.MusicXmlPartSummary pendingTrack = GetPendingSelectedTrackSummary();
        if (pendingTrack == null)
            return string.Empty;

        if (pendingTrack != null && !string.IsNullOrWhiteSpace(pendingTrack.TuningDisplayName))
            return pendingTrack.TuningDisplayName;

        return StringTuningUtils.FormatTuningDisplayName(pendingTrack?.StringTuningPitches);
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
            useAutoTrackSelection = false;

            if (IsCurrentRocksmithTrackGroupingActive())
            {
                int groupIndex = clampedOption - 1;
                List<RocksmithTrackSelectionGroup> groups = GetCurrentTrackSelectionGroupsOrdered();
                if (groupIndex < 0 || groupIndex >= groups.Count)
                    return;

                MusicXmlLoader.MusicXmlPartSummary preferredVariant = ResolvePreferredCurrentRocksmithTrackVariant(groups[groupIndex]);
                if (preferredVariant == null)
                    return;

                selectedMusicXmlPartId = preferredVariant.PartId;
            }
            else
            {
                int summaryIndex = clampedOption - 1;
                if (summaryIndex < 0 || summaryIndex >= currentSongPartSummaries.Count)
                    return;

                selectedMusicXmlPartId = currentSongPartSummaries[summaryIndex].PartId;
            }
        }

        UpdatePersistedTrackSelectionStateFromActiveSelection();
        ApplyTrackSelectionPreference();
        RestoreGeneratedPlaybackSelectionForCurrentTrack();
        ApplyGeneratedPlaybackSelection();
        RefreshEffectiveAudioOffset();
        SaveSongMetadata();
    }

    private int GetCurrentTrackSelectionOptionCount()
    {
        if (!IsCurrentRocksmithTrackGroupingActive())
            return currentSongPartSummaries?.Count ?? 0;

        return GetCurrentTrackSelectionGroupsOrdered().Count;
    }

    private bool IsCurrentRocksmithTrackGroupingActive()
    {
        return gameplayMode == GuitarGameplayMode.Guitar &&
               currentSongEntry != null &&
               currentSongEntry.PrimaryNotationKind == SongNotationSourceKind.Rocksmith &&
               currentSongPartSummaries != null &&
               currentSongPartSummaries.Any(summary => summary != null && summary.HasDifficultyVariants);
    }

    private List<RocksmithTrackSelectionGroup> GetCurrentRocksmithTrackSelectionGroups()
    {
        List<RocksmithTrackSelectionGroup> groups = new List<RocksmithTrackSelectionGroup>();
        if (!IsCurrentRocksmithTrackGroupingActive())
            return groups;

        Dictionary<string, RocksmithTrackSelectionGroup> groupsById = new Dictionary<string, RocksmithTrackSelectionGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (MusicXmlLoader.MusicXmlPartSummary summary in currentSongPartSummaries)
        {
            if (summary == null)
                continue;

            string groupId = GetRocksmithGroupId(summary);
            if (string.IsNullOrWhiteSpace(groupId))
                continue;

            if (!groupsById.TryGetValue(groupId, out RocksmithTrackSelectionGroup group))
            {
                group = new RocksmithTrackSelectionGroup
                {
                    GroupId = groupId,
                    DisplayName = string.IsNullOrWhiteSpace(summary.GroupDisplayName) ? summary.Name : summary.GroupDisplayName,
                    TuningDisplayName = summary.TuningDisplayName ?? string.Empty
                };
                groupsById[groupId] = group;
                groups.Add(group);
            }

            if (!string.IsNullOrWhiteSpace(summary.TuningDisplayName) && string.IsNullOrWhiteSpace(group.TuningDisplayName))
                group.TuningDisplayName = summary.TuningDisplayName;

            group.Variants.Add(summary);
        }

        foreach (RocksmithTrackSelectionGroup group in groups)
        {
            List<MusicXmlLoader.MusicXmlPartSummary> orderedVariants = OrderRocksmithVariants(group.Variants);
            group.Variants.Clear();
            group.Variants.AddRange(orderedVariants);
        }

        return groups;
    }

    private List<RocksmithTrackSelectionGroup> GetCurrentTrackSelectionGroupsOrdered()
    {
        return GetCurrentRocksmithTrackSelectionGroups()
            .OrderByDescending(group => GetDefaultRoutePriorityForTrack(GetHighestDifficultyRocksmithVariant(group.Variants)))
            .ThenBy(group => group.DisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private MusicXmlLoader.MusicXmlPartSummary ResolvePreferredCurrentRocksmithTrackVariant(RocksmithTrackSelectionGroup group)
    {
        if (group == null || group.Variants == null || group.Variants.Count == 0)
            return null;

        int requestedDifficulty = ResolveRocksmithDifficultyUiIndex(GetResolvedActiveTrackSummary());
        if (requestedDifficulty >= 0)
        {
            MusicXmlLoader.MusicXmlPartSummary matchingVariant = group.Variants
                .FirstOrDefault(variant => ResolveRocksmithDifficultyUiIndex(variant) == requestedDifficulty);
            if (matchingVariant != null)
                return matchingVariant;
        }

        return GetHighestDifficultyRocksmithVariant(group.Variants);
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
        RefreshActiveTrackTuning();
    }

    private bool ReloadCurrentGuitarChartForSelectedTrack()
    {
        if (currentSongEntry == null || gameplayMode != GuitarGameplayMode.Guitar)
            return false;

        ApplyTrackSelectionPreference();

        List<NoteData> reloadedNotes = null;
        List<ArpeggioGuideData> reloadedArpeggioGuides = new List<ArpeggioGuideData>();
        try
        {
            reloadedNotes = SongNotationFacade.LoadSong(currentSongEntry.PrimaryNotationPath, currentSongEntry.PrimaryNotationKind, midiTrackIndex);
            reloadedArpeggioGuides = SongNotationFacade.LoadArpeggioGuides(currentSongEntry.PrimaryNotationPath, currentSongEntry.PrimaryNotationKind, midiTrackIndex);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Primary notation reload error: " + e.Message);
        }

        if ((reloadedNotes == null || reloadedNotes.Count == 0) &&
            currentSongEntry.PrimaryNotationKind != SongNotationSourceKind.MusicXml &&
            !string.IsNullOrEmpty(currentSongEntry.XmlPath))
        {
            try
            {
                reloadedNotes = MusicXmlLoader.LoadMusicXmlSong(currentSongEntry.XmlPath, midiTrackIndex);
            }
            catch (Exception e)
            {
                Debug.LogWarning("MusicXmlLoader reload fallback error: " + e.Message);
            }
        }

        if (reloadedNotes == null || reloadedNotes.Count == 0)
            return false;

        chartNotes = reloadedNotes;
        currentArpeggioGuides = reloadedArpeggioGuides ?? new List<ArpeggioGuideData>();
        chartNoteById.Clear();
        for (int i = 0; i < chartNotes.Count; i++)
        {
            NoteData note = chartNotes[i];
            if (note.id < 0)
                note.id = i;
            chartNotes[i] = note;
            chartNoteById[note.id] = note;
        }

        noteStates = chartNotes.Select(note => new GameplayNoteState(note)).ToList();
        GenerateTabSections();
        ResetActiveRendererContent();
        currentLoadedTrackIndex = midiTrackIndex;
        RestoreGeneratedPlaybackSelectionForCurrentTrack();
        ApplyGeneratedPlaybackSelection();
        MarkDetectorHintDirty();

        if (songMetadata != null)
        {
            currentTrackBestScoreValue = GetStoredTrackScoreValue(songMetadata, selectedMusicXmlPartId);
            currentTrackBestScorePercent = Mathf.Clamp(GetStoredTrackScore(songMetadata, selectedMusicXmlPartId), 0f, 100f);
            HeroScoreSummary currentHeroTrackBest = GetStoredHeroTrackScoreSummary(songMetadata, selectedMusicXmlPartId);
            currentTrackHeroBestScoreValue = currentHeroTrackBest.scoreValue;
            currentTrackHeroBestScorePercent = currentHeroTrackBest.percent;
            currentTrackHeroBestHeartsRemaining = currentHeroTrackBest.heartsRemaining;
            currentTrackHeroBestHeartsTotal = currentHeroTrackBest.heartsTotal;
        }

        return true;
    }

    private void UpdatePersistedTrackSelectionStateFromActiveSelection()
    {
        persistedUseAutoTrackSelection = useAutoTrackSelection;
        persistedSelectedMusicXmlPartId = persistedUseAutoTrackSelection
            ? string.Empty
            : GetPersistentRocksmithPartId(selectedMusicXmlPartId);
    }

    private void OpenSongSelectionMenu()
    {
        showStartMenu = false;
        showMainMenu = false;
        showLibraryLoadingOverlay = true;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGameModes = false;
        showHeroModeSettings = false;
        loopSettingsOpenedFromGameModes = false;
        pendingTrackSelectionSong = null;
        pendingTrackSelectionParts.Clear();
        pendingRocksmithTrackSelectionGroups.Clear();
        pendingArcadeArrangementSummaries.Clear();
        showSongSettings = false;
        showGlobalSettings = false;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);

        BeginDeferredSongSelectionOpen();
    }

    private void BeginDeferredSongSelectionOpen()
    {
        openSongSelectionRequestId++;
        if (openSongSelectionRoutine != null)
            StopCoroutine(openSongSelectionRoutine);

        openSongSelectionRoutine = StartCoroutine(OpenSongSelectionMenuDeferred(openSongSelectionRequestId));
    }

    private System.Collections.IEnumerator OpenSongSelectionMenuDeferred(int requestId)
    {
        float overlayOpenedAt = Time.unscaledTime;
        yield return null;
        yield return null;

        while (Time.unscaledTime - overlayOpenedAt < LibraryLoadingOverlayMinimumSeconds)
            yield return null;

        if (requestId != openSongSelectionRequestId)
            yield break;

        Task<List<SongLibraryEntry>> loadTask = Task.Run(() => SongLibraryService.GetAvailableSongs(forceRefresh: false));
        while (!loadTask.IsCompleted)
            yield return null;

        if (requestId != openSongSelectionRequestId)
            yield break;

        if (loadTask.IsFaulted || loadTask.IsCanceled)
        {
            RefreshAvailableSongs();
        }
        else
        {
            ApplyAvailableSongsSnapshot(loadTask.Result);
        }

        if (displayedSongLibraryEntries.Count == 0)
        {
            selectedSongListIndex = 0;
            songListScrollOffset = 0;
            songSelectionSongConfirmed = false;
            showSongSelection = true;
            showLibraryLoadingOverlay = false;
            openSongSelectionRoutine = null;
            yield break;
        }

        int selectedIndex = -1;
        if (songLibraryBrowseMode == SongLibraryBrowseMode.All && !IsSongLibraryScopeActive())
        {
            selectedIndex = displayedSongLibraryEntries.FindIndex(entry =>
                entry.IsSong &&
                currentSongEntry != null &&
                entry.Song != null &&
                string.Equals(entry.Song.SongDirectory, currentSongEntry.SongDirectory, StringComparison.OrdinalIgnoreCase));
        }

        selectedSongListIndex = selectedIndex >= 0 ? selectedIndex : 0;
        SyncPendingTrackSelectionToDisplayedEntry();
        EnsureSongSelectionVisible();
        yield return null;
        showSongSelection = true;
        showLibraryLoadingOverlay = false;
        SyncAudioToSongTimer(playImmediately: false);
        openSongSelectionRoutine = null;
    }

    private void CancelDeferredSongSelectionOpen()
    {
        openSongSelectionRequestId++;
        if (openSongSelectionRoutine != null)
        {
            StopCoroutine(openSongSelectionRoutine);
            openSongSelectionRoutine = null;
        }

        showLibraryLoadingOverlay = false;
    }

    private void RefreshAvailableSongs(bool forceRefresh = false)
    {
        ApplyAvailableSongsSnapshot(SongLibraryService.GetAvailableSongs(forceRefresh));
    }

    private void ApplyAvailableSongsSnapshot(IEnumerable<SongLibraryEntry> songs)
    {
        availableSongs.Clear();
        availableSongs.AddRange((songs ?? Enumerable.Empty<SongLibraryEntry>())
            .Where(song => song != null && song.LibraryType == selectedSongLibraryType));
        availableSongs.Sort((a, b) =>
        {
            bool favoriteA = IsSongFavorited(a);
            bool favoriteB = IsSongFavorited(b);
            int favoriteCompare = favoriteB.CompareTo(favoriteA);
            if (favoriteCompare != 0)
                return favoriteCompare;

            int artistCompare = string.Compare(a?.Artist ?? string.Empty, b?.Artist ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (artistCompare != 0)
                return artistCompare;

            int albumCompare = string.Compare(a?.Album ?? string.Empty, b?.Album ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (albumCompare != 0)
                return albumCompare;

            return string.Compare(a?.DisplayName ?? string.Empty, b?.DisplayName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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

        RebuildDisplayedSongLibraryEntries();
    }

    private bool IsSongLibraryScopeActive()
    {
        return !string.IsNullOrWhiteSpace(songLibraryBrowseScopeKey);
    }

    private bool IsSongLibraryShowingGroupList()
    {
        return songLibraryBrowseMode != SongLibraryBrowseMode.All && !IsSongLibraryScopeActive();
    }

    private SongLibraryBrowseEntry GetSelectedSongLibraryBrowseEntry()
    {
        if (selectedSongListIndex < 0 || selectedSongListIndex >= displayedSongLibraryEntries.Count)
            return null;

        return displayedSongLibraryEntries[selectedSongListIndex];
    }

    private string GetSongLibraryListTitle()
    {
        string root = selectedSongLibraryType == SongLibraryType.Arcade ? "Rhythm Songs" : "Guitar Songs";
        if (songLibraryBrowseMode == SongLibraryBrowseMode.All)
            return root;

        if (!IsSongLibraryScopeActive())
            return root;

        string scope = songLibraryBrowseScopeKey;
        if (string.IsNullOrWhiteSpace(scope))
            return root;

        return $"{root} > {scope}";
    }

    private string GetSongLibraryStatusText()
    {
        int total = displayedSongLibraryEntries.Count;
        if (total <= 0)
            return "No songs";

        if (songLibraryBrowseMode == SongLibraryBrowseMode.All)
            return total == 1 ? "1 song" : $"{total} songs";

        if (IsSongLibraryScopeActive())
            return total == 1 ? "1 song" : $"{total} songs";

        if (songLibraryBrowseMode == SongLibraryBrowseMode.Artists)
            return total == 1 ? "1 artist" : $"{total} artists";

        if (songLibraryBrowseMode == SongLibraryBrowseMode.Albums)
            return total == 1 ? "1 album" : $"{total} albums";

        return total == 1 ? "1 item" : $"{total} items";
    }

    private static string BuildSongLibraryArtistSummary(IGrouping<string, SongLibraryEntry> group)
    {
        HashSet<string> albums = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (SongLibraryEntry song in group)
        {
            if (!string.IsNullOrWhiteSpace(song?.Album))
                albums.Add(song.Album.Trim());
        }

        if (albums.Count <= 0)
            return string.Empty;

        if (albums.Count == 1)
            return albums.First();

        return $"{albums.Count} albums";
    }

    private static string BuildSongLibraryAlbumSubtitle(IGrouping<string, SongLibraryEntry> group)
    {
        HashSet<string> artists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (SongLibraryEntry song in group)
        {
            if (!string.IsNullOrWhiteSpace(song?.Artist))
                artists.Add(song.Artist.Trim());
        }

        if (artists.Count <= 0)
            return string.Empty;

        if (artists.Count == 1)
            return artists.First();

        return "Various Artists";
    }

    private static string GetSongLibraryBrowseValue(SongLibraryEntry song, SongLibraryBrowseMode browseMode)
    {
        if (song == null)
            return string.Empty;

        if (browseMode == SongLibraryBrowseMode.Artists)
            return string.IsNullOrWhiteSpace(song.Artist) ? "Unknown Artist" : song.Artist.Trim();

        if (browseMode == SongLibraryBrowseMode.Albums)
            return string.IsNullOrWhiteSpace(song.Album) ? "Unknown Album" : song.Album.Trim();

        return string.Empty;
    }

    private static string GetSongLibraryNotationLabel(SongLibraryEntry song)
    {
        if (song == null)
            return string.Empty;

        if (song.LibraryType == SongLibraryType.Arcade)
        {
            string extension = Path.GetExtension(song.ArcadeChartPath)?.TrimStart('.').ToUpperInvariant();
            return string.IsNullOrWhiteSpace(extension) ? "CHART" : extension;
        }

        switch (song.PrimaryNotationKind)
        {
            case SongNotationSourceKind.Rocksmith:
                return "RS";
            case SongNotationSourceKind.Gp5:
                return "GP";
            case SongNotationSourceKind.MusicXml:
                return "XML";
            default:
                return string.Empty;
        }
    }

    private static string BuildSongLibraryAudioSummary(SongLibraryEntry song)
    {
        if (song == null)
            return "--";

        if (song.LibraryType == SongLibraryType.Arcade)
        {
            int stemCount = song.ArcadeAudioPaths?.Count(path => !string.IsNullOrWhiteSpace(path)) ?? 0;
            string chartLabel = GetSongLibraryNotationLabel(song);
            string arcadeAudioLabel = stemCount > 1 ? $"{stemCount} STEMS" : stemCount == 1 ? GetSongLibraryBackingAudioLabel(song.ArcadeAudioPaths[0]) : string.Empty;
            if (!string.IsNullOrWhiteSpace(arcadeAudioLabel) && !string.IsNullOrWhiteSpace(chartLabel))
                return $"{arcadeAudioLabel} / {chartLabel}";
            if (!string.IsNullOrWhiteSpace(arcadeAudioLabel))
                return arcadeAudioLabel;
            if (!string.IsNullOrWhiteSpace(chartLabel))
                return chartLabel;
            return "--";
        }

        bool hasMp3 = !string.IsNullOrWhiteSpace(song.Mp3Path);
        string audioLabel = hasMp3 ? GetSongLibraryBackingAudioLabel(song.Mp3Path) : string.Empty;
        string notationLabel = GetSongLibraryNotationLabel(song);
        if (!string.IsNullOrWhiteSpace(audioLabel) && !string.IsNullOrWhiteSpace(notationLabel))
            return $"{audioLabel} / {notationLabel}";

        if (!string.IsNullOrWhiteSpace(audioLabel))
            return audioLabel;

        if (!string.IsNullOrWhiteSpace(notationLabel))
            return notationLabel;

        return "--";
    }

    private static string GetSongLibraryBackingAudioLabel(string audioPath)
    {
        if (string.IsNullOrWhiteSpace(audioPath))
            return string.Empty;

        string extension = Path.GetExtension(audioPath)?.TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(extension))
            return "AUDIO";

        return extension;
    }

    private bool IsSongFavorited(SongLibraryEntry entry)
    {
        if (entry == null)
            return false;

        if (currentSongEntry != null &&
            string.Equals(currentSongEntry.SongDirectory, entry.SongDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return songMetadata != null ? songMetadata.favoriteInLibrary : entry.CachedFavoriteInLibrary;
        }

        return entry.CachedFavoriteInLibrary;
    }

    private HeroScoreSummary GetStoredSongHeroScoreSummary(SongLibraryEntry entry)
    {
        if (entry == null)
            return default;

        if (currentSongEntry != null &&
            string.Equals(currentSongEntry.SongDirectory, entry.SongDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return songMetadata != null
                ? GetHighestHeroTrackScoreSummary(songMetadata)
                : new HeroScoreSummary(entry.CachedHeroBestScoreValue, entry.CachedHeroBestScorePercent, entry.CachedHeroBestHeartsRemaining, entry.CachedHeroBestHeartsTotal);
        }

        return new HeroScoreSummary(entry.CachedHeroBestScoreValue, entry.CachedHeroBestScorePercent, entry.CachedHeroBestHeartsRemaining, entry.CachedHeroBestHeartsTotal);
    }

    private void RebuildDisplayedSongLibraryEntries()
    {
        SongLibraryBrowseEntry previousEntry = GetSelectedSongLibraryBrowseEntry();
        string previousSongDirectory = previousEntry != null && previousEntry.IsSong ? previousEntry.Song?.SongDirectory : string.Empty;
        string previousGroupKey = previousEntry != null && !previousEntry.IsSong ? previousEntry.GroupKey : string.Empty;

        displayedSongLibraryEntries.Clear();

        if (songLibraryBrowseMode == SongLibraryBrowseMode.All)
        {
            for (int i = 0; i < availableSongs.Count; i++)
            {
                SongLibraryEntry song = availableSongs[i];
                if (song == null)
                    continue;

                displayedSongLibraryEntries.Add(new SongLibraryBrowseEntry
                {
                    IsSong = true,
                    Song = song,
                    GroupKey = song.SongDirectory,
                    DisplayName = song.DisplayName,
                    Subtitle = song.Subtitle ?? string.Empty,
                    ArtworkPath = song.ArtworkPath ?? string.Empty,
                    ScorePercent = GetStoredSongBestScorePercent(song),
                    DifficultyLabel = GetSongLibraryDifficultyDisplayLabel(song)
                });
            }
        }
        else if (IsSongLibraryScopeActive())
        {
            IEnumerable<SongLibraryEntry> scopedSongs = availableSongs.Where(song =>
            {
                if (song == null)
                    return false;

                string value = GetSongLibraryBrowseValue(song, songLibraryBrowseMode);
                return string.Equals(value, songLibraryBrowseScopeKey, StringComparison.OrdinalIgnoreCase);
            });

            foreach (SongLibraryEntry song in scopedSongs)
            {
                displayedSongLibraryEntries.Add(new SongLibraryBrowseEntry
                {
                    IsSong = true,
                    Song = song,
                    GroupKey = song.SongDirectory,
                    DisplayName = song.DisplayName,
                    Subtitle = song.Subtitle ?? string.Empty,
                    ArtworkPath = song.ArtworkPath ?? string.Empty,
                    ScorePercent = GetStoredSongBestScorePercent(song),
                    DifficultyLabel = GetSongLibraryDifficultyDisplayLabel(song)
                });
            }
        }
        else if (songLibraryBrowseMode == SongLibraryBrowseMode.Artists)
        {
            IEnumerable<IGrouping<string, SongLibraryEntry>> groups = availableSongs
                .Where(song => song != null)
                .GroupBy(song => GetSongLibraryBrowseValue(song, SongLibraryBrowseMode.Artists), StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<string, SongLibraryEntry> group in groups)
            {
                SongLibraryEntry first = group.FirstOrDefault(song => song != null && !string.IsNullOrWhiteSpace(song.ArtworkPath))
                    ?? group.FirstOrDefault();
                int count = group.Count();
                displayedSongLibraryEntries.Add(new SongLibraryBrowseEntry
                {
                    IsSong = false,
                    GroupKey = group.Key,
                    DisplayName = group.Key,
                    Subtitle = BuildSongLibraryArtistSummary(group),
                    ArtworkPath = first?.ArtworkPath ?? string.Empty,
                    ScoreText = count == 1 ? "1 song" : $"{count} songs",
                    SongCount = count
                });
            }
        }
        else
        {
            IEnumerable<IGrouping<string, SongLibraryEntry>> groups = availableSongs
                .Where(song => song != null)
                .GroupBy(song => GetSongLibraryBrowseValue(song, SongLibraryBrowseMode.Albums), StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<string, SongLibraryEntry> group in groups)
            {
                SongLibraryEntry first = group.FirstOrDefault(song => song != null && !string.IsNullOrWhiteSpace(song.ArtworkPath))
                    ?? group.FirstOrDefault();
                int count = group.Count();
                displayedSongLibraryEntries.Add(new SongLibraryBrowseEntry
                {
                    IsSong = false,
                    GroupKey = group.Key,
                    DisplayName = group.Key,
                    Subtitle = BuildSongLibraryAlbumSubtitle(group),
                    ArtworkPath = first?.ArtworkPath ?? string.Empty,
                    ScoreText = count == 1 ? "1 song" : $"{count} songs",
                    SongCount = count
                });
            }
        }

        if (IsSongLibraryScopeActive() && displayedSongLibraryEntries.Count == 0)
            songLibraryBrowseScopeKey = string.Empty;

        int restoredIndex = -1;
        if (!string.IsNullOrWhiteSpace(previousSongDirectory))
        {
            restoredIndex = displayedSongLibraryEntries.FindIndex(entry =>
                entry.IsSong &&
                entry.Song != null &&
                string.Equals(entry.Song.SongDirectory, previousSongDirectory, StringComparison.OrdinalIgnoreCase));
        }
        else if (!string.IsNullOrWhiteSpace(previousGroupKey))
        {
            restoredIndex = displayedSongLibraryEntries.FindIndex(entry =>
                !entry.IsSong &&
                string.Equals(entry.GroupKey, previousGroupKey, StringComparison.OrdinalIgnoreCase));
        }

        selectedSongListIndex = restoredIndex >= 0
            ? restoredIndex
            : Mathf.Clamp(selectedSongListIndex, 0, Mathf.Max(0, displayedSongLibraryEntries.Count - 1));

        songSelectionSongConfirmed = false;
        SyncPendingTrackSelectionToDisplayedEntry();
        EnsureSongSelectionVisible();
    }

    private void SyncPendingTrackSelectionToDisplayedEntry()
    {
        SongLibraryBrowseEntry selectedEntry = GetSelectedSongLibraryBrowseEntry();
        if (selectedEntry == null || !selectedEntry.IsSong || selectedEntry.Song == null)
        {
            pendingTrackSelectionSong = null;
            pendingTrackSelectionParts.Clear();
            pendingArcadeArrangementSummaries.Clear();
            selectedTrackListIndex = 0;
            trackListScrollOffset = 0;
            return;
        }

        int songIndex = availableSongs.FindIndex(song =>
            song != null &&
            string.Equals(song.SongDirectory, selectedEntry.Song.SongDirectory, StringComparison.OrdinalIgnoreCase));
        SyncPendingTrackSelectionToSong(songIndex, preserveTrackIfPossible: true);
    }

    private void ClearSongSelectionCaches()
    {
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
        pendingRocksmithTrackSelectionGroups.Clear();
        pendingArcadeArrangementSummaries.Clear();
        if (selected.LibraryType == SongLibraryType.Arcade)
            pendingArcadeArrangementSummaries.AddRange(ArcadeCloneHeroLoader.GetArrangementSummaries(selected.ArcadeChartPath));
        else
        {
            pendingTrackSelectionParts.AddRange(GetSortedTrackSummaries(selected));
            RebuildPendingRocksmithTrackSelectionGroups();
        }

        selectedTrackListIndex = 0;
        trackListScrollOffset = 0;
        ClampSelectedRocksmithDifficultyToPendingGroup();
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
            pendingRocksmithTrackSelectionGroups.Clear();
            pendingArcadeArrangementSummaries.Clear();
            selectedTrackListIndex = 0;
            trackListScrollOffset = 0;
            return;
        }

        SongLibraryEntry selected = availableSongs[songIndex];
        if (selected.LibraryType == SongLibraryType.Arcade)
        {
            SongMetadata arcadeSelectedMetadata = LoadSongMetadataForEntry(selected);
            bool samePendingArcadeSong = pendingTrackSelectionSong != null &&
                string.Equals(pendingTrackSelectionSong.SongDirectory, selected.SongDirectory, StringComparison.OrdinalIgnoreCase);
            string previousArrangementId = preserveTrackIfPossible &&
                samePendingArcadeSong &&
                selectedTrackListIndex >= 0 &&
                selectedTrackListIndex < pendingArcadeArrangementSummaries.Count
                ? pendingArcadeArrangementSummaries[selectedTrackListIndex].ArrangementId
                : string.Empty;
            string savedArrangementId = arcadeSelectedMetadata?.selectedArcadeArrangementId ?? string.Empty;

            pendingTrackSelectionSong = selected;
            pendingTrackSelectionParts.Clear();
            pendingRocksmithTrackSelectionGroups.Clear();
            pendingArcadeArrangementSummaries.Clear();
            pendingArcadeArrangementSummaries.AddRange(ArcadeCloneHeroLoader.GetArrangementSummaries(selected.ArcadeChartPath));
            selectedTrackListIndex = 0;

            string preferredArrangementId = !string.IsNullOrWhiteSpace(previousArrangementId) ? previousArrangementId : savedArrangementId;
            if (!string.IsNullOrWhiteSpace(preferredArrangementId))
            {
                int arrangementIndex = pendingArcadeArrangementSummaries.FindIndex(summary =>
                    string.Equals(summary.ArrangementId, preferredArrangementId, StringComparison.OrdinalIgnoreCase));
                if (arrangementIndex >= 0)
                    selectedTrackListIndex = arrangementIndex;
            }

            ArcadeArrangementSummary selectedSummary = GetPendingSelectedArcadeArrangementSummary();
            if (selectedSummary != null)
            {
                ArcadeDifficulty savedDifficulty = ArcadeCloneHeroLoader.ParseDifficulty(arcadeSelectedMetadata?.selectedArcadeDifficulty, ArcadeCloneHeroLoader.GetBestDefaultDifficulty(selectedSummary.Difficulties));
                selectedArcadeDifficulty = selectedSummary.Difficulties.Contains(savedDifficulty)
                    ? savedDifficulty
                    : ArcadeCloneHeroLoader.GetBestDefaultDifficulty(selectedSummary.Difficulties);
            }

            trackListScrollOffset = 0;
            EnsureTrackSelectionVisible();
            return;
        }

        SongMetadata selectedMetadata = LoadSongMetadataForEntry(selected);
        bool samePendingSong = pendingTrackSelectionSong != null &&
            string.Equals(pendingTrackSelectionSong.SongDirectory, selected.SongDirectory, StringComparison.OrdinalIgnoreCase);
        string previousPartId = string.Empty;
        if (preserveTrackIfPossible && samePendingSong)
        {
            if (IsPendingRocksmithDifficultySelectionActive())
            {
                previousPartId = GetHighestDifficultyRocksmithVariant(GetPendingSelectedRocksmithTrackGroup()?.Variants)?.PartId ?? string.Empty;
            }
            else if (selectedTrackListIndex >= 0 && selectedTrackListIndex < pendingTrackSelectionParts.Count)
            {
                previousPartId = pendingTrackSelectionParts[selectedTrackListIndex].PartId;
            }
        }
        string savedPartId = selectedMetadata != null && !selectedMetadata.useAutoTrackSelection
            ? selectedMetadata.selectedMusicXmlPartId ?? string.Empty
            : string.Empty;

        pendingTrackSelectionSong = selected;
        pendingTrackSelectionParts.Clear();
        pendingRocksmithTrackSelectionGroups.Clear();
        pendingArcadeArrangementSummaries.Clear();
        pendingTrackSelectionParts.AddRange(GetSortedTrackSummaries(selected));
        RebuildPendingRocksmithTrackSelectionGroups();
        selectedTrackListIndex = 0;

        string preferredPartId = !string.IsNullOrEmpty(previousPartId) ? previousPartId : savedPartId;
        if (!string.IsNullOrEmpty(preferredPartId))
        {
            MusicXmlLoader.MusicXmlPartSummary preferredSummary = pendingTrackSelectionParts.FirstOrDefault(track =>
                string.Equals(track.PartId, preferredPartId, StringComparison.OrdinalIgnoreCase));
            if (preferredSummary != null)
            {
                if (IsPendingRocksmithDifficultySelectionActive())
                {
                    int groupIndex = pendingRocksmithTrackSelectionGroups.FindIndex(group =>
                        string.Equals(group.GroupId, preferredSummary.GroupId, StringComparison.OrdinalIgnoreCase));
                    if (groupIndex >= 0)
                        selectedTrackListIndex = groupIndex;

                    int difficultyIndex = ResolveRocksmithDifficultyUiIndex(preferredSummary);
                    if (difficultyIndex >= 0)
                        selectedRocksmithDifficultyIndex = difficultyIndex;
                }
                else
                {
                    int savedIndex = pendingTrackSelectionParts.FindIndex(track =>
                        string.Equals(track.PartId, preferredPartId, StringComparison.OrdinalIgnoreCase));
                    if (savedIndex >= 0)
                        selectedTrackListIndex = savedIndex;
                }
            }
        }

        trackListScrollOffset = 0;
        ClampSelectedRocksmithDifficultyToPendingGroup();
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
        pendingRocksmithTrackSelectionGroups.Clear();
        pendingArcadeArrangementSummaries.Clear();
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    private void MoveSongSelection(int delta)
    {
        if (displayedSongLibraryEntries.Count == 0)
            return;

        selectedSongListIndex = Mathf.Clamp(selectedSongListIndex + delta, 0, displayedSongLibraryEntries.Count - 1);
        songSelectionSongConfirmed = false;
        SyncPendingTrackSelectionToDisplayedEntry();
        EnsureSongSelectionVisible();
    }

    private void MoveTrackSelectionInMenu(int delta)
    {
        int count = pendingTrackSelectionSong != null && pendingTrackSelectionSong.LibraryType == SongLibraryType.Arcade
            ? pendingArcadeArrangementSummaries.Count
            : IsPendingRocksmithDifficultySelectionActive()
                ? pendingRocksmithTrackSelectionGroups.Count
                : pendingTrackSelectionParts.Count;
        if (count == 0)
            return;

        selectedTrackListIndex = Mathf.Clamp(selectedTrackListIndex + delta, 0, count - 1);
        ClampSelectedArcadeDifficultyToPendingArrangement();
        ClampSelectedRocksmithDifficultyToPendingGroup();
        EnsureTrackSelectionVisible();
    }

    private void EnsureSongSelectionVisible()
    {
        const int visibleCount = 12;
        if (selectedSongListIndex < songListScrollOffset)
            songListScrollOffset = selectedSongListIndex;

        if (selectedSongListIndex >= songListScrollOffset + visibleCount)
            songListScrollOffset = selectedSongListIndex - visibleCount + 1;

        songListScrollOffset = Mathf.Clamp(songListScrollOffset, 0, Mathf.Max(0, displayedSongLibraryEntries.Count - visibleCount));
    }

    private void EnsureTrackSelectionVisible()
    {
        const int visibleCount = 10;
        int count = pendingTrackSelectionSong != null && pendingTrackSelectionSong.LibraryType == SongLibraryType.Arcade
            ? pendingArcadeArrangementSummaries.Count
            : IsPendingRocksmithDifficultySelectionActive()
                ? pendingRocksmithTrackSelectionGroups.Count
                : pendingTrackSelectionParts.Count;
        if (selectedTrackListIndex < trackListScrollOffset)
            trackListScrollOffset = selectedTrackListIndex;

        if (selectedTrackListIndex >= trackListScrollOffset + visibleCount)
            trackListScrollOffset = selectedTrackListIndex - visibleCount + 1;

        trackListScrollOffset = Mathf.Clamp(trackListScrollOffset, 0, Mathf.Max(0, count - visibleCount));
    }

    private void ConfirmTrackSelection()
    {
        if (pendingTrackSelectionSong != null && pendingTrackSelectionSong.LibraryType == SongLibraryType.Arcade)
        {
            ConfirmArcadeArrangementSelection();
            return;
        }

        if (pendingTrackSelectionSong == null || selectedTrackListIndex < 0 || selectedTrackListIndex >= pendingTrackSelectionParts.Count)
            return;

        MusicXmlLoader.MusicXmlPartSummary selectedTrack = GetPendingSelectedTrackSummary();
        if (selectedTrack == null)
            return;
        SelectSongAndTrack(pendingTrackSelectionSong, selectedTrack.PartId);
    }

    private void ConfirmArcadeArrangementSelection()
    {
        if (pendingTrackSelectionSong == null || selectedTrackListIndex < 0 || selectedTrackListIndex >= pendingArcadeArrangementSummaries.Count)
            return;

        ArcadeArrangementSummary selectedArrangement = pendingArcadeArrangementSummaries[selectedTrackListIndex];
        if (pendingMultiplayerRhythmSongSelection)
        {
            SelectMultiplayerRhythmSongAndArrangement(pendingTrackSelectionSong, selectedArrangement.ArrangementId, selectedArcadeDifficulty);
            return;
        }

        SelectArcadeSongAndArrangement(pendingTrackSelectionSong, selectedArrangement.ArrangementId, selectedArcadeDifficulty);
    }

    private void SelectSongAndTrack(SongLibraryEntry songEntry, string selectedPartId)
    {
        if (songEntry == null)
            return;

        multiplayerRhythmModeActive = false;
        pendingMultiplayerRhythmSongSelection = false;

        bool isCurrentSong = currentSongEntry != null && string.Equals(currentSongEntry.SongDirectory, songEntry.SongDirectory, StringComparison.OrdinalIgnoreCase);
        if (isCurrentSong)
        {
            showTrackSelection = false;
            showSongSelection = false;
            songSelectionSongConfirmed = false;
            pendingTrackSelectionSong = null;
            pendingTrackSelectionParts.Clear();
            pendingRocksmithTrackSelectionGroups.Clear();
            pendingArcadeArrangementSummaries.Clear();

            useAutoTrackSelection = false;
            selectedMusicXmlPartId = selectedPartId ?? string.Empty;
            UpdatePersistedTrackSelectionStateFromActiveSelection();
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
            pendingRocksmithTrackSelectionGroups.Clear();
            pendingArcadeArrangementSummaries.Clear();
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
        if (autoplayFromMainMenuFlow && !multiplayerRhythmModeActive)
            ShowStartupTuningReminder(resumePlaybackAfterDismiss: true);
        SeekSongTime(-songStartDelaySeconds, false);
        SyncAudioToSongTimer(playImmediately: autoplay && !showStartupTuningReminder);
    }

    private void SelectArcadeSongAndArrangement(SongLibraryEntry songEntry, string arrangementId, ArcadeDifficulty difficulty)
    {
        if (songEntry == null)
            return;

        multiplayerRhythmModeActive = false;
        pendingMultiplayerRhythmSongSelection = false;
        gameplayMode = GuitarGameplayMode.Arcade;
        renderMode = GuitarRenderMode.Highway3D;
        DisableUnsupportedLoopModeState();
        selectedArcadeArrangementId = arrangementId ?? string.Empty;
        selectedArcadeDifficulty = difficulty;

        string metadataPath = BuildSongMetadataPath(songEntry);
        string metadataFileName = ResolveSongMetadataFileName(songEntry);
        SongMetadata metadata = LoadSongMetadata(metadataFileName, metadataPath);
        metadata.selectedArcadeArrangementId = selectedArcadeArrangementId;
        metadata.selectedArcadeDifficulty = ArcadeCloneHeroLoader.SerializeDifficulty(selectedArcadeDifficulty);
        SaveSongMetadata(metadata, metadataPath, metadataFileName);

        bool isCurrentSong = currentSongEntry != null && string.Equals(currentSongEntry.SongDirectory, songEntry.SongDirectory, StringComparison.OrdinalIgnoreCase);
        if (isCurrentSong)
        {
            LoadSongFromEntry(songEntry);
            return;
        }

        LoadSongFromEntry(songEntry);
        showTrackSelection = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showMainMenu = false;
        mainMenuFlowActive = false;
        pendingTrackSelectionSong = null;
        pendingTrackSelectionParts.Clear();
        pendingRocksmithTrackSelectionGroups.Clear();
        pendingArcadeArrangementSummaries.Clear();
    }

    private void SelectMultiplayerRhythmSongAndArrangement(SongLibraryEntry songEntry, string arrangementId, ArcadeDifficulty difficulty)
    {
        if (songEntry == null)
            return;

        showToneLab = false;
        showSongSettings = false;
        showGlobalSettings = false;
        showStartMenu = false;
        showMultiplayerRhythmSetup = false;
        showNotesDetectorTestMenu = false;
        showOffsetHelper = false;
        showLibraryLoadingOverlay = false;
        showStartupTuningReminder = false;
        resumeGameplayAfterStartupTuningReminder = false;
        gameplayMode = GuitarGameplayMode.Arcade;
        renderMode = GuitarRenderMode.Highway3D;
        selectedArcadeArrangementId = arrangementId ?? string.Empty;
        selectedArcadeDifficulty = difficulty;
        multiplayerRhythmModeActive = true;
        multiplayerRhythmWinningPlayerIndex = -1;
        heroModeEnabled = false;
        showHeroModeSettings = false;
        showGameModes = false;
        showLoopSettings = false;
        showLoopPausePopup = false;
        loopEnabled = false;
        loopStartConfigured = false;
        loopEndConfigured = false;
        selectedSongLibraryType = SongLibraryType.Arcade;
        pendingMultiplayerRhythmSongSelection = false;
        returnToMultiplayerRhythmSetupFromSongSelection = false;

        LoadSongFromEntry(songEntry);
        showToneLab = false;
        showSongSettings = false;
        showGlobalSettings = false;
        showStartMenu = false;
        showMultiplayerRhythmSetup = false;
        showNotesDetectorTestMenu = false;
        showOffsetHelper = false;
        showLibraryLoadingOverlay = false;
        showStartupTuningReminder = false;
        showTrackSelection = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showMainMenu = false;
        mainMenuFlowActive = false;
        pendingTrackSelectionSong = null;
        pendingTrackSelectionParts.Clear();
        pendingRocksmithTrackSelectionGroups.Clear();
        pendingArcadeArrangementSummaries.Clear();
    }

    private void HandleLoopPlayback()
    {
        if (!loopEnabled || !HasConfiguredLoopWindow())
            return;

        if (songTimer < loopStartTime - 0.0001f)
        {
            SeekSongTime(loopStartTime, false);
            return;
        }

        if (songTimer < loopEndTime)
            return;

        if (GetActiveLoopPauseDurationSeconds() > 0.001f)
        {
            StartLoopRestartPause();
            return;
        }

        ResetSinglePlayerLoopRunState(loopStartTime);
        SeekSongTime(loopStartTime, false);
    }

    private void StartLoopRestartPause()
    {
        bool shouldLogLoopCountdown = Application.isEditor;
        if (shouldLogLoopCountdown && string.IsNullOrWhiteSpace(loopCountdownEditorLogPath))
            StartLoopCountdownEditorLogSession("loop-restart-pause");

        long totalStartTicks = shouldLogLoopCountdown ? GetLoopCountdownTimestamp() : 0L;
        long resetStartTicks = shouldLogLoopCountdown ? totalStartTicks : 0L;
        ResetSinglePlayerLoopRunState(loopStartTime);
        long resetEndTicks = shouldLogLoopCountdown ? GetLoopCountdownTimestamp() : 0L;
        float pauseSeconds = Mathf.Max(0f, GetActiveLoopPauseDurationSeconds());
        long seekStartTicks = shouldLogLoopCountdown ? resetEndTicks : 0L;
        SeekSongTimeForLoopRestartPause(loopStartTime);
        long seekEndTicks = shouldLogLoopCountdown ? GetLoopCountdownTimestamp() : 0L;
        songTimer = loopStartTime;
        audioSongTimer = loopStartTime;
        loopRestartPauseRemainingSeconds = pauseSeconds;
        long syncStartTicks = shouldLogLoopCountdown ? seekEndTicks : 0L;
        SyncAudioToSongTimer(playImmediately: false);
        if (shouldLogLoopCountdown)
        {
            long totalEndTicks = GetLoopCountdownTimestamp();
            LogLoopCountdownEditor(
                $"PAUSE_ARM restartTime={loopStartTime.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"pauseSeconds={pauseSeconds.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"timingsMs reset={GetLoopCountdownElapsedMilliseconds(resetStartTicks, resetEndTicks):F3} " +
                $"seek={GetLoopCountdownElapsedMilliseconds(seekStartTicks, seekEndTicks):F3} " +
                $"sync={GetLoopCountdownElapsedMilliseconds(syncStartTicks, totalEndTicks):F3} " +
                $"total={GetLoopCountdownElapsedMilliseconds(totalStartTicks, totalEndTicks):F3} " +
                $"notes={(noteStates != null ? noteStates.Count : 0)} arcadeNotes={(arcadeNoteStates != null ? arcadeNoteStates.Count : 0)}");
        }
    }

    private void ResetSinglePlayerLoopRunState(float restartTime)
    {
        ResetSessionScoreState();
        if (multiplayerRhythmModeActive)
            return;

        float resolvedBeforeLoopThreshold = restartTime - 0.0001f;
        if (gameplayMode == GuitarGameplayMode.Arcade)
        {
            if (arcadeNoteStates == null)
                return;

            int firstArcadeLoopIndex = FindFirstArcadeNoteStateIndexAtOrAfter(arcadeNoteStates, resolvedBeforeLoopThreshold);
            for (int i = firstArcadeLoopIndex; i < arcadeNoteStates.Count; i++)
            {
                ArcadeNoteState noteState = arcadeNoteStates[i];
                if (noteState == null)
                    continue;

                noteState.result = GameplayNoteResult.Pending;
                noteState.resolvedAt = -1f;
                noteState.isJudgeable = false;
            }

            return;
        }

        if (noteStates == null)
            return;

        int firstLoopIndex = FindFirstGameplayNoteStateIndexAtOrAfter(noteStates, resolvedBeforeLoopThreshold);
        for (int i = firstLoopIndex; i < noteStates.Count; i++)
        {
            GameplayNoteState noteState = noteStates[i];
            if (noteState == null)
                continue;

            noteState.result = GameplayNoteResult.Pending;
            noteState.resolvedAt = -1f;
            noteState.isJudgeable = false;
        }
    }

    private static int FindFirstGameplayNoteStateIndexAtOrAfter(List<GameplayNoteState> states, float thresholdTime)
    {
        if (states == null || states.Count == 0)
            return 0;

        int low = 0;
        int high = states.Count;
        while (low < high)
        {
            int mid = low + ((high - low) / 2);
            GameplayNoteState state = states[mid];
            float noteTime = state?.data.time ?? float.NegativeInfinity;
            if (noteTime < thresholdTime)
                low = mid + 1;
            else
                high = mid;
        }

        return Mathf.Clamp(low, 0, states.Count);
    }

    private static int FindFirstArcadeNoteStateIndexAtOrAfter(List<ArcadeNoteState> states, float thresholdTime)
    {
        if (states == null || states.Count == 0)
            return 0;

        int low = 0;
        int high = states.Count;
        while (low < high)
        {
            int mid = low + ((high - low) / 2);
            ArcadeNoteState state = states[mid];
            float noteTime = state?.data.time ?? float.NegativeInfinity;
            if (noteTime < thresholdTime)
                low = mid + 1;
            else
                high = mid;
        }

        return Mathf.Clamp(low, 0, states.Count);
    }

    private void SnapSongTimeIntoLoopWindowIfNeeded()
    {
        if (!loopEnabled || !HasConfiguredLoopWindow())
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

        if (gameplayMode == GuitarGameplayMode.Guitar && renderMode == GuitarRenderMode.Highway3D)
            renderMode = GuitarRenderMode.Tabs;

        SyncAudioToSongTimer(playImmediately: false);
    }

    private void OpenLoopPausePopup()
    {
        loopSettingsPreviewPlaying = false;
        showLoopSettings = false;
        showLoopPausePopup = true;
        selectedLoopPausePopupIndex = 0;
        loopPausePopupResumePlaybackOnConfirm = true;
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
        if (loopSettingsPreviewPlaying && HasConfiguredLoopWindow())
            SnapSongTimeIntoLoopWindowIfNeeded();

        SyncAudioToSongTimer(playImmediately: loopSettingsPreviewPlaying);
    }

    private bool HasConfiguredLoopWindow()
    {
        return loopStartConfigured && loopEndConfigured && loopEndTime > loopStartTime + 0.01f;
    }

    private bool IsLoopModeAvailable()
    {
        return (gameplayMode == GuitarGameplayMode.Guitar || gameplayMode == GuitarGameplayMode.Arcade) && !multiplayerRhythmModeActive;
    }

    private bool UsesRhythmPracticeSectionLoop()
    {
        return gameplayMode == GuitarGameplayMode.Arcade && !multiplayerRhythmModeActive;
    }

    private float GetActiveLoopPauseDurationSeconds()
    {
        return loopPauseDurationSeconds;
    }

    private void DisableUnsupportedLoopModeState()
    {
        if (IsLoopModeAvailable())
            return;

        if (renderMode == GuitarRenderMode.Tabs)
            renderMode = GuitarRenderMode.Highway3D;

        showLoopSettings = false;
        showLoopPausePopup = false;
        loopSettingsPreviewPlaying = false;
        loopRestartPauseRemainingSeconds = 0f;
        loopEnabled = false;
        selectedLoopMarker = 1;
        arcadePracticeLoopStartSectionIndex = -1;
        arcadePracticeLoopEndSectionIndex = -1;
    }


    public void ToggleLoopFromUi()
    {
        if (!IsLoopModeAvailable())
        {
            DisableUnsupportedLoopModeState();
            return;
        }

        if (loopEnabled)
        {
            bool wasInLoopFlow = showLoopSettings || showLoopPausePopup;
            loopEnabled = false;
            loopRestartPauseRemainingSeconds = 0f;
            pendingLoopRestartFromStartAfterResume = false;
            pendingLoopStartCountdownAfterResume = false;
            loopPausePopupResumePlaybackOnConfirm = false;
            showLoopPausePopup = false;
            selectedLoopPausePopupIndex = 0;
            loopSettingsPreviewPlaying = false;
            ResetSessionScoreState(ignoreCurrentlyResolvedNotes: true);
            SaveSongMetadata();
            if (wasInLoopFlow)
                ExitLoopSettingsMode();
            return;
        }

        if (UsesRhythmPracticeSectionLoop())
        {
            loopSettingsOpenedFromGameModes = showGameModes;
            showGameModes = false;
            showHeroModeSettings = false;
            EnterLoopSettingsMode();
            return;
        }

        loopEnabled = true;
        scoreSaveInvalidated = true;
        ResetSessionScoreState(ignoreCurrentlyResolvedNotes: true);
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

    private int GetHeroModeLostHeartCount()
    {
        if (gameplayMode == GuitarGameplayMode.Arcade)
            return GetArcadeHeroModeLostHeartCount();

        if (noteStates == null || noteStates.Count == 0)
            return 0;

        HashSet<int> missedChordIds = new HashSet<int>();
        int missedSingles = 0;
        for (int i = 0; i < noteStates.Count; i++)
        {
            GameplayNoteState noteState = noteStates[i];
            if (noteState == null || !noteState.IsMissed)
                continue;

            if (noteState.data.chordId >= 0)
                missedChordIds.Add(noteState.data.chordId);
            else
                missedSingles++;
        }

        return missedSingles + missedChordIds.Count;
    }

    private int GetArcadeHeroModeLostHeartCount()
    {
        if (arcadeNoteStates == null || arcadeNoteStates.Count == 0)
            return 0;

        HashSet<int> missedChordIds = new HashSet<int>();
        int missedSingles = 0;
        for (int i = 0; i < arcadeNoteStates.Count; i++)
        {
            ArcadeNoteState noteState = arcadeNoteStates[i];
            if (noteState == null || !noteState.IsMissed)
                continue;

            int chordId = GetArcadeConsumeChordId(noteState.data);
            if (chordId >= 0)
                missedChordIds.Add(chordId);
            else
                missedSingles++;
        }

        return missedSingles + missedChordIds.Count;
    }

    private int GetCurrentHeroHeartsRemaining()
    {
        return Mathf.Clamp(heroModeHeartCount - GetHeroModeLostHeartCount(), 0, Mathf.Max(1, heroModeHeartCount));
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
        showRocksmithDifficultyPopup = false;
        showHeroModeSettings = false;
        loopSettingsOpenedFromGameModes = false;
        SetSongEndState(true, asGameOver);
        SyncAudioToSongTimer(playImmediately: false);
    }

    private bool TryTriggerHeroModeGameOver()
    {
        if (!heroModeEnabled || songHasEnded || GetCurrentHeroHeartsRemaining() > 0)
            return false;

        if (loopEnabled && HasConfiguredLoopWindow())
        {
            StartLoopRestartPause();
            return true;
        }

        EnterSongEndState(asGameOver: true);
        return true;
    }

    private void RestartCurrentSongForModeChange()
    {
        SetSongEndState(false);
        selectedSongEndActionIndex = 0;
        scoreSaveInvalidated = IsScoreInvalidatingModeActive();
        ResetSessionScoreState();
        ClearNoteByNoteWaitingState();
        SeekSongTime(-songStartDelaySeconds, false);
        SyncAudioToSongTimer(playImmediately: false);
    }

    private int GetFirstVisibleGameModesIndex()
    {
        for (int index = 0; index <= 6; index++)
        {
            if (IsGameModesSelectionVisible(index))
                return index;
        }

        return 6;
    }

    private bool IsPauseActionVisible(int index)
    {
        if (notesDetectorGameplayTestActive)
            return index == 8 || index == 9;

        if (multiplayerRhythmModeActive && index == 1)
            return false;

        return index >= 0 && index <= 10;
    }

    private int GetFirstVisiblePauseActionIndex()
    {
        if (notesDetectorGameplayTestActive)
            return 8;

        if (IsPauseActionVisible(1))
            return 1;

        if (IsPauseActionVisible(2))
            return 2;

        return IsPauseActionVisible(0) ? 0 : 3;
    }

    private int GetResolvedSongSettingsTrackPopupIndex()
    {
        int optionCount = GetSongSettingsTrackPopupOptionCount();
        if (optionCount <= 0)
            return 0;

        if (IsCurrentRocksmithTrackGroupingActive())
        {
            string selectedGroupId = !string.IsNullOrWhiteSpace(selectedMusicXmlPartId)
                ? GetRocksmithGroupIdFromPartId(selectedMusicXmlPartId)
                : GetRocksmithGroupId(GetResolvedActiveTrackSummary());
            if (!string.IsNullOrWhiteSpace(selectedGroupId))
            {
                List<RocksmithTrackSelectionGroup> groups = GetCurrentTrackSelectionGroupsOrdered();
                int matchedGroupIndex = groups.FindIndex(group =>
                    string.Equals(group.GroupId, selectedGroupId, StringComparison.OrdinalIgnoreCase));
                if (matchedGroupIndex >= 0)
                    return matchedGroupIndex;
            }

            return 0;
        }

        if (!useAutoTrackSelection && !string.IsNullOrEmpty(selectedMusicXmlPartId))
        {
            int matchedIndex = currentSongPartSummaries.FindIndex(summary =>
                string.Equals(summary.PartId, selectedMusicXmlPartId, StringComparison.OrdinalIgnoreCase));
            if (matchedIndex >= 0)
                return matchedIndex;
        }

        MusicXmlLoader.MusicXmlPartSummary resolvedSummary = GetResolvedActiveTrackSummary();
        if (resolvedSummary != null)
        {
            int resolvedIndex = currentSongPartSummaries.FindIndex(summary =>
                string.Equals(summary.PartId, resolvedSummary.PartId, StringComparison.OrdinalIgnoreCase));
            if (resolvedIndex >= 0)
                return resolvedIndex;
        }

        return 0;
    }

    private bool IsGameModesSelectionVisible(int index)
    {
        bool loopModeAvailable = IsLoopModeAvailable();
        return index switch
        {
            0 => loopModeAvailable,
            1 => loopModeAvailable && (loopEnabled || UsesRhythmPracticeSectionLoop()),
            2 => gameplayMode == GuitarGameplayMode.Guitar,
            3 => true,
            4 => IsCurrentRocksmithDifficultyModeAvailable(),
            5 => heroModeEnabled,
            6 => true,
            _ => false
        };
    }

    public void OpenGameModesFromUi()
    {
        if (multiplayerRhythmModeActive)
            return;

        gameplayHudPreviewInMenus = false;
        showGameModes = true;
        showRocksmithDifficultyPopup = false;
        showHeroModeSettings = false;
        selectedHeroModeSettingsIndex = 0;
        selectedGameModesIndex = IsGameModesSelectionVisible(selectedGameModesIndex) ? selectedGameModesIndex : GetFirstVisibleGameModesIndex();
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void CloseGameModesFromUi()
    {
        gameplayHudPreviewInMenus = false;
        showGameModes = false;
        showRocksmithDifficultyPopup = false;
        showHeroModeSettings = false;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void SetGameModesSelectionFromUi(int index)
    {
        selectedGameModesIndex = Mathf.Clamp(index, 0, 6);
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

        const int optionCount = 7;
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
                OpenRocksmithDifficultyPopupFromUi();
                break;
            case 5:
                OpenHeroModeSettingsFromUi();
                break;
            case 6:
                CloseGameModesFromUi();
                break;
        }
    }

    public void OpenLoopConfigurationFromUi()
    {
        if (!IsLoopModeAvailable() || (!loopEnabled && !UsesRhythmPracticeSectionLoop()))
            return;

        loopSettingsOpenedFromGameModes = true;
        showGameModes = false;
        showHeroModeSettings = false;
        EnterLoopSettingsMode();
    }

    public int GetSelectedArcadePracticeSectionIndexForUi()
    {
        return selectedArcadePracticeSectionIndex;
    }

    public void SelectArcadePracticeSectionFromUi(int index)
    {
        if (!UsesRhythmPracticeSectionLoop() || currentArcadePracticeSections.Count == 0)
            return;

        selectedArcadePracticeSectionIndex = Mathf.Clamp(index, 0, currentArcadePracticeSections.Count - 1);
        ArcadePracticeSectionData section = currentArcadePracticeSections[selectedArcadePracticeSectionIndex];
        SeekSongTime(section.startTime, false);
    }

    public void ActivateArcadePracticeSectionFromUi(int index)
    {
        if (!UsesRhythmPracticeSectionLoop() || currentArcadePracticeSections.Count == 0)
            return;

        SelectArcadePracticeSectionFromUi(index);
        ToggleArcadePracticeSectionSelectionFromUi(selectedArcadePracticeSectionIndex);
    }

    public void MoveArcadePracticeSectionSelectionFromUi(int delta)
    {
        if (!UsesRhythmPracticeSectionLoop() || currentArcadePracticeSections.Count == 0 || delta == 0)
            return;

        int nextIndex = (selectedArcadePracticeSectionIndex + delta + currentArcadePracticeSections.Count) % currentArcadePracticeSections.Count;
        SelectArcadePracticeSectionFromUi(nextIndex);
    }

    public void ToggleArcadePracticeSectionSelectionFromUi(int index)
    {
        if (!UsesRhythmPracticeSectionLoop() || currentArcadePracticeSections.Count == 0)
            return;

        int candidateIndex = Mathf.Clamp(index, 0, currentArcadePracticeSections.Count - 1);
        selectedArcadePracticeSectionIndex = candidateIndex;

        if (arcadePracticeLoopStartSectionIndex < 0)
        {
            arcadePracticeLoopStartSectionIndex = candidateIndex;
            arcadePracticeLoopEndSectionIndex = -1;
            return;
        }

        if (arcadePracticeLoopEndSectionIndex < 0)
        {
            if (candidateIndex == arcadePracticeLoopStartSectionIndex)
            {
                arcadePracticeLoopStartSectionIndex = -1;
                arcadePracticeLoopEndSectionIndex = -1;
                return;
            }

            arcadePracticeLoopEndSectionIndex = candidateIndex;
            NormalizeArcadePracticeLoopSelectionBounds();
            return;
        }

        int currentStart = arcadePracticeLoopStartSectionIndex;
        int currentEnd = arcadePracticeLoopEndSectionIndex;
        if (candidateIndex == currentStart)
        {
            arcadePracticeLoopStartSectionIndex = currentEnd;
            arcadePracticeLoopEndSectionIndex = -1;
            return;
        }

        if (candidateIndex == currentEnd)
        {
            arcadePracticeLoopEndSectionIndex = -1;
            return;
        }

        if (candidateIndex < currentStart)
        {
            arcadePracticeLoopStartSectionIndex = candidateIndex;
            return;
        }

        if (candidateIndex > currentEnd)
        {
            arcadePracticeLoopEndSectionIndex = candidateIndex;
            return;
        }

        int distanceToStart = Mathf.Abs(candidateIndex - currentStart);
        int distanceToEnd = Mathf.Abs(currentEnd - candidateIndex);
        if (distanceToStart <= distanceToEnd)
            arcadePracticeLoopStartSectionIndex = candidateIndex;
        else
            arcadePracticeLoopEndSectionIndex = candidateIndex;

        NormalizeArcadePracticeLoopSelectionBounds();
    }

    public void StartRhythmPracticeLoopFromUi()
    {
        if (!UsesRhythmPracticeSectionLoop())
            return;

        loopSettingsPreviewPlaying = false;
        loopRestartPauseRemainingSeconds = 0f;

        if (arcadePracticeLoopStartSectionIndex >= 0 && currentArcadePracticeSections.Count > 0)
        {
            ApplyArcadePracticeLoopRangeToCurrentSelection(enableLoop: true, snapToStart: true);
            OpenLoopPausePopup();
            loopPausePopupResumePlaybackOnConfirm = true;
        }
        else
        {
            ClearArcadePracticeLoopSelectionFromUi();
            ResumePlaybackFromUi();
        }
    }

    public void ClearArcadePracticeLoopSelectionFromUi()
    {
        arcadePracticeLoopStartSectionIndex = -1;
        arcadePracticeLoopEndSectionIndex = -1;
        loopEnabled = false;
        loopStartConfigured = false;
        loopEndConfigured = false;
        loopRestartPauseRemainingSeconds = 0f;
        pendingLoopRestartFromStartAfterResume = false;
        pendingLoopStartCountdownAfterResume = false;
        loopPausePopupResumePlaybackOnConfirm = false;
        loopSettingsPreviewPlaying = false;
        selectedLoopMarker = 1;
        ResetSessionScoreState(ignoreCurrentlyResolvedNotes: true);
        SyncAudioToSongTimer(playImmediately: false);
    }

    private void NormalizeArcadePracticeLoopSelectionBounds()
    {
        if (arcadePracticeLoopStartSectionIndex < 0)
        {
            arcadePracticeLoopEndSectionIndex = -1;
            return;
        }

        if (arcadePracticeLoopEndSectionIndex < 0)
            return;

        if (arcadePracticeLoopEndSectionIndex < arcadePracticeLoopStartSectionIndex)
        {
            int previousStart = arcadePracticeLoopStartSectionIndex;
            arcadePracticeLoopStartSectionIndex = arcadePracticeLoopEndSectionIndex;
            arcadePracticeLoopEndSectionIndex = previousStart;
        }

        if (arcadePracticeLoopEndSectionIndex == arcadePracticeLoopStartSectionIndex)
            arcadePracticeLoopEndSectionIndex = -1;
    }

    private void ApplyArcadePracticeLoopRangeToCurrentSelection(bool enableLoop, bool snapToStart)
    {
        if (currentArcadePracticeSections.Count == 0 ||
            arcadePracticeLoopStartSectionIndex < 0 ||
            arcadePracticeLoopStartSectionIndex >= currentArcadePracticeSections.Count)
        {
            return;
        }

        int effectiveEndSectionIndex = arcadePracticeLoopEndSectionIndex >= arcadePracticeLoopStartSectionIndex
            ? Mathf.Clamp(arcadePracticeLoopEndSectionIndex, arcadePracticeLoopStartSectionIndex, currentArcadePracticeSections.Count - 1)
            : arcadePracticeLoopStartSectionIndex;

        ArcadePracticeSectionData startSection = currentArcadePracticeSections[arcadePracticeLoopStartSectionIndex];
        ArcadePracticeSectionData endSection = currentArcadePracticeSections[effectiveEndSectionIndex];
        loopStartTime = Mathf.Max(0f, startSection.startTime);
        loopEndTime = Mathf.Max(loopStartTime + 0.05f, endSection.endTime);
        loopStartConfigured = true;
        loopEndConfigured = true;
        if (enableLoop)
        {
            loopEnabled = true;
            scoreSaveInvalidated = true;
        }

        selectedLoopMarker = 1;
        loopRestartPauseRemainingSeconds = 0f;
        ResetSessionScoreState();

        if (snapToStart)
            SeekSongTime(loopStartTime, false);

        SyncAudioToSongTimer(playImmediately: loopSettingsPreviewPlaying);
    }

    public void ToggleHeroModeFromUi()
    {
        heroModeEnabled = !heroModeEnabled;
        SaveHeroModePreferences();
        showHeroModeSettings = false;
        selectedGameModesIndex = Mathf.Min(selectedGameModesIndex, heroModeEnabled ? 5 : 4);
        RestartCurrentSongForModeChange();
    }

    public void OpenHeroModeSettingsFromUi()
    {
        if (!heroModeEnabled)
            return;

        gameplayHudPreviewInMenus = false;
        showGameModes = false;
        showHeroModeSettings = true;
        selectedHeroModeSettingsIndex = 0;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void CloseHeroModeSettingsFromUi()
    {
        gameplayHudPreviewInMenus = false;
        showHeroModeSettings = false;
        selectedHeroModeSettingsIndex = 0;
        showGameModes = true;
        selectedGameModesIndex = 5;
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
        if (!IsPauseActionVisible(selectedPauseActionIndex))
            selectedPauseActionIndex = GetFirstVisiblePauseActionIndex();
    }

    public void HoverPauseActionSelectionFromUi(int index)
    {
        if (!isPaused || showMainMenu || showSongSettings || showSongSelection || showTrackSelection || showGlobalSettings || showGameModes || showHeroModeSettings)
            return;

        SetPauseActionSelectionFromUi(index);
    }

    public void MovePauseActionSelectionFromUi(int delta)
    {
        if (delta == 0)
            return;

        const int optionCount = 11;
        int nextIndex = selectedPauseActionIndex;
        for (int attempt = 0; attempt < optionCount; attempt++)
        {
            nextIndex = (nextIndex + delta + optionCount) % optionCount;
            if (IsPauseActionVisible(nextIndex))
            {
                selectedPauseActionIndex = nextIndex;
                return;
            }
        }
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
        loopPauseDurationSeconds = Mathf.Clamp(Mathf.Round(seconds), 0f, 8f);
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
        bool shouldRestartLoopFromStart = loopEnabled && HasConfiguredLoopWindow();
        bool shouldArmLoopCountdown = shouldRestartLoopFromStart && GetActiveLoopPauseDurationSeconds() > 0.001f;
        pendingLoopRestartFromStartAfterResume = shouldRestartLoopFromStart;
        pendingLoopStartCountdownAfterResume = shouldArmLoopCountdown;

        if (loopPausePopupResumePlaybackOnConfirm)
        {
            loopPausePopupResumePlaybackOnConfirm = false;
            ResumePlaybackFromUi();
            return;
        }

        ExitLoopSettingsMode();
    }

    public void CloseLoopPausePopupBackToLoopSettingsFromUi()
    {
        showLoopPausePopup = false;
        selectedLoopPausePopupIndex = 0;
        pendingLoopRestartFromStartAfterResume = false;
        pendingLoopStartCountdownAfterResume = false;
        loopPausePopupResumePlaybackOnConfirm = false;
        showLoopSettings = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    private bool IsUiSubmitPressed()
    {
        bool hasInputSystemGamepad = HasInputSystemGamepadConnected();
        bool suppressControllerSubmit = ShouldUseControllerPointerUiMode();
        return Input.GetKeyDown(KeyCode.Return) ||
               Input.GetKeyDown(KeyCode.KeypadEnter) ||
               Input.GetKeyDown(KeyCode.Space) ||
               (!suppressControllerSubmit && IsAnyGamepadSubmitPressedThisFrame()) ||
               (!suppressControllerSubmit && !hasInputSystemGamepad && TryGetButtonDown("Submit")) ||
               (!suppressControllerSubmit && !hasInputSystemGamepad && Input.GetKeyDown(KeyCode.JoystickButton0));
    }

    private bool IsUiBackPressed()
    {
        bool hasInputSystemGamepad = HasInputSystemGamepadConnected();
        return Input.GetKeyDown(KeyCode.Escape) ||
               Input.GetKeyDown(KeyCode.Backspace) ||
               IsAnyGamepadBackPressedThisFrame() ||
               (!hasInputSystemGamepad && TryGetButtonDown("Cancel")) ||
               (!hasInputSystemGamepad && Input.GetKeyDown(KeyCode.JoystickButton1)) ||
               (!hasInputSystemGamepad && Input.GetKeyDown(KeyCode.JoystickButton2));
    }

    private bool IsUiPausePressed()
    {
        bool hasInputSystemGamepad = HasInputSystemGamepadConnected();
        return Input.GetKeyDown(KeyCode.Escape) ||
               IsAnyGamepadPausePressedThisFrame() ||
               (!hasInputSystemGamepad && Input.GetKeyDown(KeyCode.JoystickButton7)) ||
               (!hasInputSystemGamepad && Input.GetKeyDown(KeyCode.JoystickButton9));
    }

    private bool IsPreviewTogglePressed()
    {
        bool hasInputSystemGamepad = HasInputSystemGamepadConnected();
        return Input.GetKeyDown(KeyCode.Space) ||
               (!hasInputSystemGamepad && Input.GetKeyDown(KeyCode.JoystickButton7)) ||
               (!hasInputSystemGamepad && Input.GetKeyDown(KeyCode.JoystickButton9));
    }

    private static bool IsRestartPressed()
    {
        return Input.GetKeyDown(KeyCode.R);
    }

    private bool IsStartupTuningReminderHeldDismissPressed()
    {
        if (startupTuningReminderShownFrame < 0 || Time.frameCount <= startupTuningReminderShownFrame)
            return false;

        return Input.GetKey(KeyCode.Return) ||
               Input.GetKey(KeyCode.KeypadEnter) ||
               Input.GetKeyUp(KeyCode.Return) ||
               Input.GetKeyUp(KeyCode.KeypadEnter);
    }

    private bool IsUiUpPressed()
    {
        RefreshUiControllerAxes();
        bool hasInputSystemGamepad = HasInputSystemGamepadConnected();
        return Input.GetKeyDown(KeyCode.UpArrow) ||
               IsAnyGamepadDpadUpPressedThisFrame() ||
               (!hasInputSystemGamepad && Input.GetKeyDown(KeyCode.JoystickButton13)) ||
               (!hasInputSystemGamepad && CrossedUiAxisThreshold(previousUiControllerVerticalAxis, currentUiControllerVerticalAxis, 1));
    }

    private bool IsUiDownPressed()
    {
        RefreshUiControllerAxes();
        bool hasInputSystemGamepad = HasInputSystemGamepadConnected();
        return Input.GetKeyDown(KeyCode.DownArrow) ||
               IsAnyGamepadDpadDownPressedThisFrame() ||
               (!hasInputSystemGamepad && Input.GetKeyDown(KeyCode.JoystickButton14)) ||
               (!hasInputSystemGamepad && CrossedUiAxisThreshold(previousUiControllerVerticalAxis, currentUiControllerVerticalAxis, -1));
    }

    private bool IsUiLeftPressed()
    {
        RefreshUiControllerAxes();
        bool hasInputSystemGamepad = HasInputSystemGamepadConnected();
        return Input.GetKeyDown(KeyCode.LeftArrow) ||
               IsAnyGamepadDpadLeftPressedThisFrame() ||
               (!hasInputSystemGamepad && Input.GetKeyDown(KeyCode.JoystickButton15)) ||
               (!hasInputSystemGamepad && CrossedUiAxisThreshold(previousUiControllerHorizontalAxis, currentUiControllerHorizontalAxis, -1));
    }

    private bool IsUiRightPressed()
    {
        RefreshUiControllerAxes();
        bool hasInputSystemGamepad = HasInputSystemGamepadConnected();
        return Input.GetKeyDown(KeyCode.RightArrow) ||
               IsAnyGamepadDpadRightPressedThisFrame() ||
               (!hasInputSystemGamepad && Input.GetKeyDown(KeyCode.JoystickButton16)) ||
               (!hasInputSystemGamepad && CrossedUiAxisThreshold(previousUiControllerHorizontalAxis, currentUiControllerHorizontalAxis, 1));
    }

    private bool IsUiLeftHeld()
    {
        RefreshUiControllerAxes();
        bool hasInputSystemGamepad = HasInputSystemGamepadConnected();
        return Input.GetKey(KeyCode.LeftArrow) ||
               IsAnyGamepadDpadLeftHeld() ||
               (!hasInputSystemGamepad && Input.GetKey(KeyCode.JoystickButton15)) ||
               (!hasInputSystemGamepad && currentUiControllerHorizontalAxis <= -UiControllerAxisThreshold);
    }

    private bool IsUiRightHeld()
    {
        RefreshUiControllerAxes();
        bool hasInputSystemGamepad = HasInputSystemGamepadConnected();
        return Input.GetKey(KeyCode.RightArrow) ||
               IsAnyGamepadDpadRightHeld() ||
               (!hasInputSystemGamepad && Input.GetKey(KeyCode.JoystickButton16)) ||
               (!hasInputSystemGamepad && currentUiControllerHorizontalAxis >= UiControllerAxisThreshold);
    }

#if ENABLE_INPUT_SYSTEM
    private static bool IsAnyGamepadSubmitPressedThisFrame()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad == null)
                continue;

            if (gamepad.buttonSouth.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    private static bool IsAnyGamepadBackPressedThisFrame()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad == null)
                continue;

            if (gamepad.buttonEast.wasPressedThisFrame || gamepad.selectButton.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    private static bool IsAnyGamepadPausePressedThisFrame()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad == null)
                continue;

            if (gamepad.startButton.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    private static bool IsAnyGamepadDpadUpPressedThisFrame()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad?.dpad.up.wasPressedThisFrame == true)
                return true;
        }

        return false;
    }

    private static bool IsAnyGamepadDpadDownPressedThisFrame()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad?.dpad.down.wasPressedThisFrame == true)
                return true;
        }

        return false;
    }

    private static bool IsAnyGamepadDpadLeftPressedThisFrame()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad?.dpad.left.wasPressedThisFrame == true)
                return true;
        }

        return false;
    }

    private static bool IsAnyGamepadDpadRightPressedThisFrame()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad?.dpad.right.wasPressedThisFrame == true)
                return true;
        }

        return false;
    }

    private static bool IsAnyGamepadDpadLeftHeld()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad?.dpad.left.isPressed == true)
                return true;
        }

        return false;
    }

    private static bool IsAnyGamepadDpadRightHeld()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad?.dpad.right.isPressed == true)
                return true;
        }

        return false;
    }
#else
    private static bool IsAnyGamepadSubmitPressedThisFrame() => false;
    private static bool IsAnyGamepadBackPressedThisFrame() => false;
    private static bool IsAnyGamepadPausePressedThisFrame() => false;
    private static bool IsAnyGamepadDpadUpPressedThisFrame() => false;
    private static bool IsAnyGamepadDpadDownPressedThisFrame() => false;
    private static bool IsAnyGamepadDpadLeftPressedThisFrame() => false;
    private static bool IsAnyGamepadDpadRightPressedThisFrame() => false;
    private static bool IsAnyGamepadDpadLeftHeld() => false;
    private static bool IsAnyGamepadDpadRightHeld() => false;
#endif

    private bool ShouldUseControllerPointerUiMode()
    {
        return showMainMenu ||
               showStartMenu ||
               showSongSelection ||
               showTrackSelection ||
               showSongSettings ||
               showGlobalSettings ||
               showLoopSettings ||
               showLoopPausePopup ||
               showGameModes ||
               showHeroModeSettings ||
               showNotesDetectorTestMenu ||
               showToneLab ||
               showGeneratedAudioTrackSelectionPopup ||
               showSongSettingsTrackSelectionPopup ||
               showStartupTuningReminder ||
               showLibraryLoadingOverlay ||
               showOffsetHelper ||
               showMultiplayerRhythmSetup ||
               songHasEnded ||
               (isPaused && !showToneLab);
    }

    private static bool HasInputSystemGamepadConnected()
    {
#if ENABLE_INPUT_SYSTEM
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad != null)
                return true;
        }
#endif
        return false;
    }

    private static bool TryGetInputSystemGamepadForSlot(int controllerSlot, out Gamepad gamepad)
    {
#if ENABLE_INPUT_SYSTEM
        int slotIndex = controllerSlot - 1;
        if (slotIndex >= 0 && slotIndex < Gamepad.all.Count)
        {
            gamepad = Gamepad.all[slotIndex];
            return gamepad != null;
        }
#endif

        gamepad = null;
        return false;
    }

    private static bool CrossedUiAxisThreshold(float previous, float current, int direction)
    {
        if (direction > 0)
            return previous < UiControllerAxisThreshold && current >= UiControllerAxisThreshold;

        return previous > -UiControllerAxisThreshold && current <= -UiControllerAxisThreshold;
    }

    private void RefreshUiControllerAxes()
    {
        if (cachedUiControllerInputFrame == Time.frameCount)
            return;

        cachedUiControllerInputFrame = Time.frameCount;
        previousUiControllerHorizontalAxis = currentUiControllerHorizontalAxis;
        previousUiControllerVerticalAxis = currentUiControllerVerticalAxis;
        currentUiControllerHorizontalAxis = ReadStrongestUiAxis("DPadX", "DPad Horizontal");
        currentUiControllerVerticalAxis = ReadStrongestUiAxis("DPadY", "DPad Vertical");
    }

    private static float ReadStrongestUiAxis(params string[] axisNames)
    {
        float strongest = 0f;
        if (axisNames == null)
            return strongest;

        for (int i = 0; i < axisNames.Length; i++)
        {
            float value = TryGetAxisRaw(axisNames[i]);
            if (Mathf.Abs(value) > Mathf.Abs(strongest))
                strongest = value;
        }

        return Mathf.Clamp(strongest, -1f, 1f);
    }

    private static float TryGetAxisRaw(string axisName)
    {
        if (string.IsNullOrWhiteSpace(axisName))
            return 0f;

        try
        {
            return Input.GetAxisRaw(axisName);
        }
        catch (ArgumentException)
        {
            return 0f;
        }
    }

    private static bool TryGetButtonDown(string buttonName)
    {
        if (string.IsNullOrWhiteSpace(buttonName))
            return false;

        try
        {
            return Input.GetButtonDown(buttonName);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private int GetHeldHorizontalArrowDirection()
    {
        bool leftHeld = IsUiLeftHeld();
        bool rightHeld = IsUiRightHeld();
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

        bool initialDown = (direction < 0 && IsUiLeftPressed()) ||
                           (direction > 0 && IsUiRightPressed());
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
                OpenSongSettingsTrackSelectionPopupFromUi();
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
        if (notesDetectorGameplayTestActive)
        {
            ExitNotesDetectorGameplayTest(reopenDetectorMenu: true);
            return;
        }

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
        if (!IsPauseActionVisible(selectedPauseActionIndex))
        {
            selectedPauseActionIndex = GetFirstVisiblePauseActionIndex();
            return;
        }

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
        CancelDeferredSongSelectionOpen();
        showToneLab = false;
        HideToneLabUi();
        ResetTransientMenuNavigationState();
        showNotesDetectorTestMenu = false;
        showStartMenu = false;
        showMultiplayerRhythmSetup = false;
        multiplayerRhythmModeActive = false;
        multiplayerRhythmWinningPlayerIndex = -1;
        pendingMultiplayerRhythmSongSelection = false;
        returnToMultiplayerRhythmSetupFromSongSelection = false;
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

    public void StartFromMainMenuFromUi()
    {
        if (!firstStartCompleted)
        {
            OpenStartMenuFromUi();
            return;
        }

        ContinueFromMainMenuFromUi();
    }

    public void ContinueFromMainMenuFromUi()
    {
        CancelDeferredSongSelectionOpen();
        showToneLab = false;
        HideToneLabUi();
        SetSongEndState(false);
        showStartMenu = false;
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

    public void OpenStartMenuFromUi()
    {
        CancelDeferredSongSelectionOpen();
        showToneLab = false;
        HideToneLabUi();
        ResetTransientMenuNavigationState();
        SetSongEndState(false);
        if (!firstStartCompleted && !forceStandardTuning)
        {
            forceStandardTuning = true;
            RefreshActiveTrackTuning();
            SaveGlobalRuntimeSettingsMetadata();
        }
        showStartMenu = true;
        showMainMenu = false;
        mainMenuFlowActive = true;
        showSongSettings = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        showNotesDetectorTestMenu = false;
        showGameModes = false;
        showHeroModeSettings = false;
        showStartupTuningReminder = false;
        resumeGameplayAfterStartupTuningReminder = false;
        startMenuStep = StartMenuStep.SelectMode;
        selectedStartMenuModeIndex = Mathf.Clamp((int)selectedSongLibraryType, 0, StartMenuModeOptionCount - 1);
        selectedStartMenuArcadeSetupIndex = 0;
        selectedStartMenuArcadeInputIndex = GetStartMenuArcadeInputIndexFromCurrentSettings();
        startMenuArcadeGamepadMode = selectedStartMenuArcadeInputIndex == 0 || arcadeGamepadMode;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void CloseStartMenuToMainMenuFromUi()
    {
        CancelDeferredSongSelectionOpen();
        showStartMenu = false;
        showMainMenu = true;
        mainMenuFlowActive = true;
        startMenuStep = StartMenuStep.SelectMode;
        selectedStartMenuArcadeSetupIndex = 0;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void HoverStartMenuModeFromUi(int modeIndex)
    {
        if (!showStartMenu || startMenuStep != StartMenuStep.SelectMode)
            return;

        selectedStartMenuModeIndex = Mathf.Clamp(modeIndex, 0, StartMenuModeOptionCount - 1);
    }

    public void SelectStartMenuModeFromUi(int modeIndex)
    {
        if (!showStartMenu)
            return;

        selectedStartMenuModeIndex = Mathf.Clamp(modeIndex, 0, StartMenuModeOptionCount - 1);
        ActivateStartMenuModeSelection();
    }

    public void ContinueStartMenuGuitarSetupFromUi()
    {
        if (!showStartMenu || startMenuStep != StartMenuStep.GuitarSetup)
            return;

        selectedStartMenuArcadeSetupIndex = 1;
        CompleteFirstStartAndOpenLibrary(SongLibraryType.Guitar);
    }

    public void HoverStartMenuGuitarSetupRowFromUi(int rowIndex)
    {
        if (!showStartMenu || startMenuStep != StartMenuStep.GuitarSetup)
            return;

        selectedStartMenuArcadeSetupIndex = Mathf.Clamp(rowIndex, 0, StartMenuGuitarSetupRowCount - 1);
    }

    public void ToggleStartMenuGuitarForceStandardFromUi()
    {
        if (!showStartMenu || startMenuStep != StartMenuStep.GuitarSetup)
            return;

        selectedStartMenuArcadeSetupIndex = 0;
        forceStandardTuning = !forceStandardTuning;
        RefreshActiveTrackTuning();
        SaveGlobalRuntimeSettingsMetadata();
    }

    public void HoverStartMenuArcadeSetupRowFromUi(int rowIndex)
    {
        if (!showStartMenu || startMenuStep != StartMenuStep.ArcadeSetup)
            return;

        selectedStartMenuArcadeSetupIndex = Mathf.Clamp(rowIndex, 0, StartMenuArcadeSetupRowCount - 1);
    }

    public void HoverStartMenuArcadeInputFromUi(int inputIndex)
    {
        if (!showStartMenu || startMenuStep != StartMenuStep.ArcadeSetup)
            return;

        selectedStartMenuArcadeSetupIndex = 0;
        selectedStartMenuArcadeInputIndex = Mathf.Clamp(inputIndex, 0, StartMenuArcadeInputOptionCount - 1);
    }

    public void SelectStartMenuArcadeInputFromUi(int inputIndex)
    {
        if (!showStartMenu || startMenuStep != StartMenuStep.ArcadeSetup)
            return;

        selectedStartMenuArcadeSetupIndex = 0;
        SetStartMenuArcadeInputIndex(inputIndex, applyRecommendedGamepadMode: true);
    }

    public void ToggleStartMenuArcadeGamepadModeFromUi()
    {
        if (!showStartMenu || startMenuStep != StartMenuStep.ArcadeSetup)
            return;

        selectedStartMenuArcadeSetupIndex = 1;
        startMenuArcadeGamepadMode = !startMenuArcadeGamepadMode;
    }

    public void ContinueStartMenuArcadeSetupFromUi()
    {
        if (!showStartMenu || startMenuStep != StartMenuStep.ArcadeSetup)
            return;

        selectedStartMenuArcadeSetupIndex = 2;
        ApplyStartMenuArcadeSetup();
        CompleteFirstStartAndOpenLibrary(SongLibraryType.Arcade);
    }

    public void OpenMultiplayerRhythmSetupFromUi()
    {
        CancelDeferredSongSelectionOpen();
        showToneLab = false;
        HideToneLabUi();
        ResetTransientMenuNavigationState();
        SetSongEndState(false);
        multiplayerRhythmModeActive = false;
        multiplayerRhythmWinningPlayerIndex = -1;
        pendingMultiplayerRhythmSongSelection = false;
        returnToMultiplayerRhythmSetupFromSongSelection = false;
        CancelMultiplayerRhythmSetupCapture();
        RefreshMultiplayerRhythmAvailableDevices();
        showMultiplayerRhythmSetup = true;
        showStartMenu = false;
        showMainMenu = false;
        mainMenuFlowActive = true;
        showSongSettings = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        showNotesDetectorTestMenu = false;
        showGameModes = false;
        showHeroModeSettings = false;
        showStartupTuningReminder = false;
        resumeGameplayAfterStartupTuningReminder = false;
        selectedMultiplayerRhythmSetupIndex = 0;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void CloseMultiplayerRhythmSetupToMainMenuFromUi()
    {
        CancelMultiplayerRhythmSetupCapture();
        multiplayerRhythmModeActive = false;
        multiplayerRhythmWinningPlayerIndex = -1;
        pendingMultiplayerRhythmSongSelection = false;
        returnToMultiplayerRhythmSetupFromSongSelection = false;
        showMultiplayerRhythmSetup = false;
        showMainMenu = true;
        mainMenuFlowActive = true;
        selectedMainMenuIndex = 0;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void HoverMultiplayerRhythmSetupRowFromUi(int rowIndex)
    {
        if (!showMultiplayerRhythmSetup)
            return;

        selectedMultiplayerRhythmSetupIndex = Mathf.Clamp(rowIndex, 0, MultiplayerRhythmSetupRowCount - 1);
    }

    public void ActivateMultiplayerRhythmSetupRowFromUi(int rowIndex)
    {
        if (!showMultiplayerRhythmSetup)
            return;

        selectedMultiplayerRhythmSetupIndex = Mathf.Clamp(rowIndex, 0, MultiplayerRhythmSetupRowCount - 1);
        ActivateMultiplayerRhythmSetupSelection();
    }

    public void SetMultiplayerRhythmPlayerOneDeviceFromUi(int deviceIndex)
    {
        if (!showMultiplayerRhythmSetup || multiplayerRhythmAvailableDevices.Count == 0)
            return;

        CancelMultiplayerRhythmSetupCapture();
        selectedMultiplayerRhythmSetupIndex = 0;
        selectedMultiplayerRhythmPlayerOneDeviceIndex = Mathf.Clamp(deviceIndex, 0, multiplayerRhythmAvailableDevices.Count - 1);
        if (selectedMultiplayerRhythmPlayerOneDeviceIndex == selectedMultiplayerRhythmPlayerTwoDeviceIndex && multiplayerRhythmAvailableDevices.Count > 1)
            selectedMultiplayerRhythmPlayerTwoDeviceIndex = (selectedMultiplayerRhythmPlayerOneDeviceIndex + 1) % multiplayerRhythmAvailableDevices.Count;
    }

    public void SetMultiplayerRhythmPlayerTwoDeviceFromUi(int deviceIndex)
    {
        if (!showMultiplayerRhythmSetup || multiplayerRhythmAvailableDevices.Count == 0)
            return;

        CancelMultiplayerRhythmSetupCapture();
        selectedMultiplayerRhythmSetupIndex = 1;
        selectedMultiplayerRhythmPlayerTwoDeviceIndex = Mathf.Clamp(deviceIndex, 0, multiplayerRhythmAvailableDevices.Count - 1);
        if (selectedMultiplayerRhythmPlayerTwoDeviceIndex == selectedMultiplayerRhythmPlayerOneDeviceIndex && multiplayerRhythmAvailableDevices.Count > 1)
            selectedMultiplayerRhythmPlayerOneDeviceIndex = (selectedMultiplayerRhythmPlayerTwoDeviceIndex + 1) % multiplayerRhythmAvailableDevices.Count;
    }

    public void ContinueMultiplayerRhythmSetupFromUi()
    {
        if (!showMultiplayerRhythmSetup || !CanStartMultiplayerRhythm())
            return;

        CancelMultiplayerRhythmSetupCapture();
        multiplayerRhythmPlayers[0].assignment = CloneMultiplayerRhythmAssignment(multiplayerRhythmAvailableDevices[selectedMultiplayerRhythmPlayerOneDeviceIndex]);
        multiplayerRhythmPlayers[1].assignment = CloneMultiplayerRhythmAssignment(multiplayerRhythmAvailableDevices[selectedMultiplayerRhythmPlayerTwoDeviceIndex]);

        multiplayerRhythmModeActive = false;
        heroModeEnabled = false;
        showHeroModeSettings = false;
        showGameModes = false;
        showLoopSettings = false;
        showLoopPausePopup = false;
        loopEnabled = false;
        loopStartConfigured = false;
        loopEndConfigured = false;
        selectedSongLibraryType = SongLibraryType.Arcade;
        selectedMultiplayerRhythmSetupIndex = 2;
        showMultiplayerRhythmSetup = false;
        pendingMultiplayerRhythmSongSelection = true;
        returnToMultiplayerRhythmSetupFromSongSelection = true;
        songSelectionOpenedFromSongEnd = false;
        songSelectionOpenedFromMainMenu = true;
        OpenSongSelectionMenu();
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

    private void MoveStartMenuModeSelection(int delta)
    {
        if (delta == 0)
            return;

        selectedStartMenuModeIndex = WrapIndex(selectedStartMenuModeIndex + delta, StartMenuModeOptionCount);
    }

    private void MoveStartMenuArcadeSetupSelection(int delta)
    {
        if (delta == 0)
            return;

        selectedStartMenuArcadeSetupIndex = WrapIndex(selectedStartMenuArcadeSetupIndex + delta, StartMenuArcadeSetupRowCount);
    }

    private void AdjustStartMenuArcadeSetupSelection(int delta)
    {
        if (selectedStartMenuArcadeSetupIndex == 0)
        {
            SetStartMenuArcadeInputIndex(selectedStartMenuArcadeInputIndex + delta, applyRecommendedGamepadMode: true);
            return;
        }

        if (selectedStartMenuArcadeSetupIndex == 1)
            startMenuArcadeGamepadMode = !startMenuArcadeGamepadMode;
    }

    private void ActivateStartMenuModeSelection()
    {
        if (selectedStartMenuModeIndex <= 0)
        {
            startMenuStep = StartMenuStep.GuitarSetup;
            selectedStartMenuArcadeSetupIndex = 0;
            return;
        }

        startMenuStep = StartMenuStep.ArcadeSetup;
        selectedStartMenuArcadeSetupIndex = 0;
        selectedStartMenuArcadeInputIndex = GetStartMenuArcadeInputIndexFromCurrentSettings();
        startMenuArcadeGamepadMode = selectedStartMenuArcadeInputIndex == 0 || arcadeGamepadMode;
    }

    private void ActivateStartMenuArcadeSetupSelection()
    {
        switch (selectedStartMenuArcadeSetupIndex)
        {
            case 0:
                SetStartMenuArcadeInputIndex(selectedStartMenuArcadeInputIndex + 1, applyRecommendedGamepadMode: true);
                break;
            case 1:
                startMenuArcadeGamepadMode = !startMenuArcadeGamepadMode;
                break;
            default:
                ApplyStartMenuArcadeSetup();
                CompleteFirstStartAndOpenLibrary(SongLibraryType.Arcade);
                break;
        }
    }

    private void SetStartMenuArcadeInputIndex(int inputIndex, bool applyRecommendedGamepadMode)
    {
        selectedStartMenuArcadeInputIndex = WrapIndex(inputIndex, StartMenuArcadeInputOptionCount);
        if (!applyRecommendedGamepadMode)
            return;

        startMenuArcadeGamepadMode = selectedStartMenuArcadeInputIndex == 0;
    }

    private int GetStartMenuArcadeInputIndexFromCurrentSettings()
    {
        switch (arcadeInputSource)
        {
            case ArcadeInputSourceMode.Controller:
                return 1;
            case ArcadeInputSourceMode.Midi:
                return 2;
            default:
                return 0;
        }
    }

    private void ApplyStartMenuArcadeSetup()
    {
        switch (selectedStartMenuArcadeInputIndex)
        {
            case 1:
                arcadeInputSource = ArcadeInputSourceMode.Controller;
                break;
            case 2:
                arcadeInputSource = ArcadeInputSourceMode.Midi;
                break;
            default:
                arcadeInputSource = ArcadeInputSourceMode.KeyboardAndController;
                break;
        }

        arcadeGamepadMode = startMenuArcadeGamepadMode;
        arcadeMidiInputEnabled = UsesArcadeMidiInput();
        if (!UsesArcadeMidiInput())
            StopArcadeMidiInput();

        SaveGlobalRuntimeSettingsMetadata();
    }

    private void CompleteFirstStartAndOpenLibrary(SongLibraryType type)
    {
        firstStartCompleted = true;
        SaveGameSaveState();
        SetSongLibraryType(type);
        startMenuStep = StartMenuStep.SelectMode;
        selectedStartMenuArcadeSetupIndex = 0;
        showStartMenu = false;
        showMainMenu = false;
        mainMenuFlowActive = true;
        pendingMultiplayerRhythmSongSelection = false;
        songSelectionOpenedFromMainMenu = true;
        OpenSongSelectionMenu();
    }

    private static int WrapIndex(int index, int count)
    {
        if (count <= 0)
            return 0;

        int result = index % count;
        return result < 0 ? result + count : result;
    }

    private static MultiplayerRhythmInputAssignment CloneMultiplayerRhythmAssignment(MultiplayerRhythmInputAssignment source)
    {
        if (source == null)
            return new MultiplayerRhythmInputAssignment();

        return new MultiplayerRhythmInputAssignment
        {
            kind = source.kind,
            controllerSlot = source.controllerSlot,
            displayName = source.displayName ?? string.Empty
        };
    }

    public void ActivateSelectedMainMenuFromUi()
    {
        switch (Mathf.Clamp(selectedMainMenuIndex, 0, MainMenuOptionCount - 1))
        {
            case 0:
                StartFromMainMenuFromUi();
                break;
            case 1:
                OpenSongSelectionFromUi();
                break;
            case 2:
                OpenMultiplayerRhythmSetupFromUi();
                break;
            case 3:
                OpenGlobalSettingsFromUi();
                break;
            case 4:
                OpenNotesDetectorTestFromUi();
                break;
            case 5:
                OpenToneLabFromUi();
                break;
            case 6:
                ExitGameFromUi();
                break;
        }
    }

    public void OpenNotesDetectorTestFromUi()
    {
        StopNotesDetectorEditorLogSession("reopen");
        StartNotesDetectorEditorLogSession("open-detector-menu");
        notesDetectorGameplayTestActive = false;
        CancelDeferredSongSelectionOpen();
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
        showNotesDetectorTestSelectionPopup = false;
        showNotesDetectorRoutinePopup = false;
        selectedNotesDetectorTestIndex = 0;
        selectedNotesDetectorCatalogIndex = 0;
        isPaused = true;
        if (notesDetectorBackendMode != NotesDetectorBackendMode.NativeEmbeddedBridge)
            SwitchNotesDetectorBackend(NotesDetectorBackendMode.NativeEmbeddedBridge);
        else
            StartConfiguredNotesDetectorBackend();
        ResetLiveDetectorReadState();
        RefreshNativeNotesDetectorUiState();
        RefreshDetectorBackendStatus();
        MarkDetectorHintDirty();
        SyncAudioToSongTimer(playImmediately: false);
        LogNotesDetectorEditor($"DETECTOR_MENU_OPEN backend={notesDetectorBackendMode} selectedDevice={selectedNativeNotesDetectorInputDeviceIndex} status={GetNotesDetectorStatusText()}");
    }

    public void CloseNotesDetectorTestFromUi()
    {
        if (notesDetectorGameplayTestActive)
        {
            ExitNotesDetectorGameplayTest(reopenDetectorMenu: false);
            return;
        }

        showNotesDetectorTestSelectionPopup = false;
        showNotesDetectorRoutinePopup = false;
        nativeNotesDetectorBridge?.RestoreSelectedPresetWorkingSettings();
        RefreshNativeNotesDetectorUiState();
        showNotesDetectorTestMenu = false;
        showMainMenu = true;
        mainMenuFlowActive = true;
        isPaused = true;
        MarkDetectorHintDirty();
        SyncAudioToSongTimer(playImmediately: false);
        StopNotesDetectorEditorLogSession("close-detector-menu");
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
                OpenNotesDetectorTestSelectionPopupFromUi();
                break;
            case 2:
                CloseNotesDetectorTestFromUi();
                break;
        }
    }

    public void OpenNotesDetectorTestSelectionPopupFromUi()
    {
        if (!showNotesDetectorTestMenu || notesDetectorGameplayTestActive)
            return;

        if (!TryRefreshNotesDetectorTestCatalog())
            return;

        showNotesDetectorTestSelectionPopup = notesDetectorSelectableTests.Count > 0;
        selectedNotesDetectorCatalogIndex = Mathf.Clamp(selectedNotesDetectorCatalogIndex, 0, Mathf.Max(0, notesDetectorSelectableTests.Count - 1));
        if (notesDetectorSelectableTests.Count > 0)
            selectedNotesDetectorTestId = notesDetectorSelectableTests[selectedNotesDetectorCatalogIndex].Id ?? string.Empty;
    }

    public void CloseNotesDetectorTestSelectionPopupFromUi()
    {
        showNotesDetectorTestSelectionPopup = false;
    }

    public void SetNotesDetectorTestPopupSelectionFromUi(int index)
    {
        if (notesDetectorSelectableTests.Count == 0)
        {
            selectedNotesDetectorCatalogIndex = 0;
            selectedNotesDetectorTestId = string.Empty;
            return;
        }

        selectedNotesDetectorCatalogIndex = Mathf.Clamp(index, 0, notesDetectorSelectableTests.Count - 1);
        selectedNotesDetectorTestId = notesDetectorSelectableTests[selectedNotesDetectorCatalogIndex].Id ?? string.Empty;
    }

    public void HoverNotesDetectorTestPopupSelectionFromUi(int index)
    {
        if (!showNotesDetectorTestSelectionPopup)
            return;

        SetNotesDetectorTestPopupSelectionFromUi(index);
    }

    public void MoveNotesDetectorTestPopupSelectionFromUi(int delta)
    {
        if (delta == 0 || notesDetectorSelectableTests.Count == 0)
            return;

        int optionCount = notesDetectorSelectableTests.Count;
        int nextIndex = (selectedNotesDetectorCatalogIndex + delta + optionCount) % optionCount;
        SetNotesDetectorTestPopupSelectionFromUi(nextIndex);
    }

    public void ActivateSelectedNotesDetectorTestPopupFromUi()
    {
        if (!showNotesDetectorTestSelectionPopup)
            return;

        if (notesDetectorSelectableTests.Count == 0)
        {
            showNotesDetectorTestSelectionPopup = false;
            return;
        }

        selectedNotesDetectorTestId = notesDetectorSelectableTests[Mathf.Clamp(selectedNotesDetectorCatalogIndex, 0, notesDetectorSelectableTests.Count - 1)].Id ?? string.Empty;
        StartNotesDetectorGameplayTest();
    }

    public void RunNotesDetectorRoutineFromUi()
    {
        if (!showNotesDetectorTestMenu)
            return;

        ResetLiveDetectorReadState();
        StartNotesDetectorGameplayTest();
    }

    public void CloseNotesDetectorRoutineFromUi()
    {
        if (notesDetectorGameplayTestActive)
        {
            ExitNotesDetectorGameplayTest(reopenDetectorMenu: true);
            return;
        }

        showNotesDetectorRoutinePopup = false;
        notesDetectorRoutineStageIndex = 0;
        notesDetectorRoutineMatchedSinceTime = -1f;
        notesDetectorRoutineOpenedTime = 0f;
        MarkDetectorHintDirty();
    }

    private static string GetNotesDetectorTestCatalogPath()
    {
        return Path.Combine(ExternalContentPaths.StreamingRoot, NotesDetectorTestSongFolderName, NotesDetectorTestCatalogFileName);
    }

    private static string GetNotesDetectorTestMetadataPath()
    {
        return Path.Combine(ExternalContentPaths.PersistentRoot, NotesDetectorTestSongFolderName, ExternalContentPaths.SongMetadataFileName);
    }

    private bool TryRefreshNotesDetectorTestCatalog()
    {
        string catalogPath = GetNotesDetectorTestCatalogPath();
        if (!NotesDetectorTestSongLoader.TryLoadCatalog(catalogPath, out LoadedNotesDetectorTestCatalog loadedCatalog, out string loadError))
        {
            Debug.LogWarning($"[GuitarBridgeServer] Detector test catalog load failed: {loadError}");
            LogNotesDetectorEditor($"TEST_CATALOG_LOAD_FAILED path={catalogPath} error={loadError}");
            notesDetectorLoadedTestCatalog = null;
            notesDetectorSelectableTests.Clear();
            selectedNotesDetectorCatalogIndex = 0;
            selectedNotesDetectorTestId = string.Empty;
            showNotesDetectorTestSelectionPopup = false;
            return false;
        }

        notesDetectorLoadedTestCatalog = loadedCatalog;
        notesDetectorSelectableTests.Clear();
        if (loadedCatalog.Tests != null)
            notesDetectorSelectableTests.AddRange(loadedCatalog.Tests.Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.Id)));

        if (notesDetectorSelectableTests.Count == 0)
        {
            selectedNotesDetectorCatalogIndex = 0;
            selectedNotesDetectorTestId = string.Empty;
            showNotesDetectorTestSelectionPopup = false;
            return false;
        }

        int resolvedIndex = notesDetectorSelectableTests.FindIndex(entry => string.Equals(entry.Id, selectedNotesDetectorTestId, StringComparison.OrdinalIgnoreCase));
        if (resolvedIndex < 0)
            resolvedIndex = Mathf.Clamp(selectedNotesDetectorCatalogIndex, 0, notesDetectorSelectableTests.Count - 1);

        SetNotesDetectorTestPopupSelectionFromUi(resolvedIndex);
        return true;
    }

    private void StartNotesDetectorEditorLogSession(string reason)
    {
        if (!Application.isEditor)
            return;

        try
        {
            string directory = Path.Combine(Application.persistentDataPath, "NotesDetectorLogs");
            Directory.CreateDirectory(directory);
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            notesDetectorEditorLogPath = Path.Combine(directory, $"editor-session-{timestamp}.log");
            File.WriteAllText(notesDetectorEditorLogPath, string.Empty, Encoding.UTF8);
            LogNotesDetectorEditor($"SESSION START reason={reason} unityTime={Time.realtimeSinceStartup:F3}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NotesDetectorLog] Failed to start editor log session: {ex.Message}");
            notesDetectorEditorLogPath = string.Empty;
        }

        if (nativeNotesDetectorBridge != null && !string.IsNullOrWhiteSpace(notesDetectorEditorLogPath))
            nativeNotesDetectorBridge.SetDebugLogPath(notesDetectorEditorLogPath);
    }

    private void StopNotesDetectorEditorLogSession(string reason)
    {
        if (!Application.isEditor)
            return;

        if (!string.IsNullOrWhiteSpace(notesDetectorEditorLogPath))
            LogNotesDetectorEditor($"SESSION END reason={reason} unityTime={Time.realtimeSinceStartup:F3}");

        if (nativeNotesDetectorBridge != null)
            nativeNotesDetectorBridge.SetDebugLogPath(string.Empty);

        notesDetectorEditorLogPath = string.Empty;
    }

    private void LogNotesDetectorEditor(string message)
    {
        if (!Application.isEditor || string.IsNullOrWhiteSpace(notesDetectorEditorLogPath) || string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            File.AppendAllText(notesDetectorEditorLogPath, $"[{timestamp}] {message}{Environment.NewLine}", Encoding.UTF8);
        }
        catch
        {
        }
    }

    private void StartLoopCountdownEditorLogSession(string reason)
    {
        if (!Application.isEditor)
            return;

        try
        {
            string directory = Path.Combine(Application.persistentDataPath, "LoopCountdownLogs");
            Directory.CreateDirectory(directory);
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            loopCountdownEditorLogPath = Path.Combine(directory, $"loop-countdown-{timestamp}.log");
            loopCountdownEditorLogBuffer.Clear();
            loopCountdownEditorLogFrameIndex = 0;
            LogLoopCountdownEditor(
                $"SESSION START reason={reason} unityTime={Time.realtimeSinceStartup:F3} " +
                $"song={songTimer.ToString("F3", CultureInfo.InvariantCulture)} audio={audioSongTimer.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"loopStart={loopStartTime.ToString("F3", CultureInfo.InvariantCulture)} loopEnd={loopEndTime.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"pauseSeconds={loopRestartPauseRemainingSeconds.ToString("F3", CultureInfo.InvariantCulture)} " +
                $"renderer={(activeRenderer != null ? activeRenderer.GetType().Name : "null")} renderMode={renderMode} gameplayMode={gameplayMode}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LoopCountdownLog] Failed to start editor log session: {ex.Message}");
            loopCountdownEditorLogPath = string.Empty;
            loopCountdownEditorLogBuffer.Clear();
        }
    }

    private void StopLoopCountdownEditorLogSession(string reason)
    {
        if (!Application.isEditor || string.IsNullOrWhiteSpace(loopCountdownEditorLogPath))
            return;

        try
        {
            LogLoopCountdownEditor($"SESSION END reason={reason} unityTime={Time.realtimeSinceStartup:F3}");
            File.WriteAllText(loopCountdownEditorLogPath, loopCountdownEditorLogBuffer.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LoopCountdownLog] Failed to write editor log session: {ex.Message}");
        }
        finally
        {
            loopCountdownEditorLogPath = string.Empty;
            loopCountdownEditorLogBuffer.Clear();
            loopCountdownEditorLogFrameIndex = 0;
        }
    }

    private void LogLoopCountdownEditor(string message)
    {
        if (!Application.isEditor || string.IsNullOrWhiteSpace(loopCountdownEditorLogPath) || string.IsNullOrWhiteSpace(message))
            return;

        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        loopCountdownEditorLogBuffer.Append('[');
        loopCountdownEditorLogBuffer.Append(timestamp);
        loopCountdownEditorLogBuffer.Append("] ");
        loopCountdownEditorLogBuffer.AppendLine(message);
    }

    internal bool ShouldLogLoopCountdownRendererDetail()
    {
        return Application.isEditor && !string.IsNullOrWhiteSpace(loopCountdownEditorLogPath);
    }

    internal void LogLoopCountdownRendererDetail(string message)
    {
        LogLoopCountdownEditor(message);
    }

    private static long GetLoopCountdownTimestamp()
    {
        return System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private static double GetLoopCountdownElapsedMilliseconds(long startTimestamp, long endTimestamp)
    {
        return (endTimestamp - startTimestamp) * 1000d / System.Diagnostics.Stopwatch.Frequency;
    }

    private string FormatDetectorHintExpectedNotesForLog(DetectorHintExpectedNote[] expectedNotes)
    {
        if (expectedNotes == null || expectedNotes.Length == 0)
            return "--";

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < expectedNotes.Length; i++)
        {
            if (i > 0)
                builder.Append(" | ");

            DetectorHintExpectedNote expected = expectedNotes[i];
            builder.Append(GetNoteNameFromMidi(expected.midi));
            builder.Append(" s");
            builder.Append(expected.stringIndex);
            builder.Append(" f");
            builder.Append(expected.fret);
            builder.Append(" open=");
            builder.Append(GetNoteNameFromMidi(expected.openMidi));
            builder.Append(" flags=");
            builder.Append(expected.flags);
        }

        return builder.ToString();
    }

    private string DescribeGameplayNoteStateForDetectorLog(GameplayNoteState note)
    {
        if (note == null)
            return "<null>";

        StringBuilder builder = new StringBuilder();
        builder.Append("id=").Append(note.data.id);
        builder.Append(" chord=").Append(note.data.chordId);
        builder.Append(" string=").Append(note.data.stringIdx);
        builder.Append(" fret=").Append(note.data.fret);
        builder.Append(" note=").Append(note.data.note ?? "--");
        builder.Append(" time=").Append(note.data.time.ToString("F3", CultureInfo.InvariantCulture));
        builder.Append(" reqPluck=").Append(note.data.requiresPluck);
        builder.Append(" resolved=").Append(note.IsResolved);
        return builder.ToString();
    }

    private static string NormalizeDetectorEventSourceLabel(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "Other";

        string[] rawParts = source.Split(new[] { '+', ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> normalizedParts = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < rawParts.Length; i++)
        {
            string token = rawParts[i]?.Trim();
            if (string.IsNullOrWhiteSpace(token))
                continue;

            string normalizedToken;
            switch (token.ToLowerInvariant())
            {
                case "ai":
                case "deep":
                    normalizedToken = "AI";
                    break;
                case "fast-chord":
                case "fast chord":
                    normalizedToken = "Fast Chord";
                    break;
                case "fast-single":
                case "fast single":
                    normalizedToken = "Fast Single";
                    break;
                case "fast-continuous":
                case "fast continuous":
                case "continuous":
                    normalizedToken = "Fast Continuous";
                    break;
                default:
                    normalizedToken = token;
                    break;
            }

            if (seen.Add(normalizedToken))
                normalizedParts.Add(normalizedToken);
        }

        return normalizedParts.Count == 0 ? "Other" : string.Join(" + ", normalizedParts);
    }

    private static string BuildDetectorAcceptanceSourceLabel(string source, bool viaHighStringRescue = false)
    {
        string normalized = NormalizeDetectorEventSourceLabel(source);
        return viaHighStringRescue ? $"High String Rescue ({normalized})" : normalized;
    }

    private void ResetNotesDetectorAcceptanceSource()
    {
        latestNotesDetectorAcceptanceSourceText = "--";
    }

    private void RecordNotesDetectorAcceptanceSource(string source)
    {
        if (!notesDetectorGameplayTestActive)
            return;

        latestNotesDetectorAcceptanceSourceText = string.IsNullOrWhiteSpace(source) ? "--" : source;
    }

    private void LogChordMatchAttempt(string stage, GameplayNoteState note, NoteEvent ev, List<GameplayNoteState> chordStates, List<int> consumeKeys, string detail)
    {
        if (!Application.isEditor || !notesDetectorGameplayTestActive)
            return;

        string eventNotes = ev != null && ev.pitches != null && ev.pitches.Count > 0 ? FormatMidiSetCsv(ev.pitches) : "--";
        string eventSource = ev != null ? NormalizeDetectorEventSourceLabel(ev.source) : "--";
        string chordNotes = chordStates != null && chordStates.Count > 0
            ? string.Join(" || ", chordStates.Select(DescribeGameplayNoteStateForDetectorLog))
            : "--";
        string keys = consumeKeys != null && consumeKeys.Count > 0 ? string.Join(",", consumeKeys) : "--";
        string eventTime = ev != null ? ev.time.ToString("F3", CultureInfo.InvariantCulture) : "--";
        LogNotesDetectorEditor($"CHORD_{stage} target=[{DescribeGameplayNoteStateForDetectorLog(note)}] chordStates=[{chordNotes}] eventTime={eventTime} eventNotes={eventNotes} eventSource={eventSource} consumeKeys={keys} detail={detail}");
    }

    private void CaptureNotesDetectorGameplayReturnState()
    {
        notesDetectorGameplayReturnSongEntry = currentSongEntry;
        notesDetectorGameplayReturnSelectedMusicXmlPartId = selectedMusicXmlPartId ?? string.Empty;
        notesDetectorGameplayReturnUseAutoTrackSelection = useAutoTrackSelection;
        notesDetectorGameplayReturnNoteByNoteModeEnabled = noteByNoteModeEnabled;
        notesDetectorGameplayReturnHeroModeEnabled = heroModeEnabled;
        notesDetectorGameplayReturnRenderMode = renderMode;
    }

    private void StartNotesDetectorGameplayTest()
    {
        string definitionPath = GetNotesDetectorTestCatalogPath();
        if (!NotesDetectorTestSongLoader.TryLoadSong(definitionPath, selectedNotesDetectorTestId, out LoadedNotesDetectorTestSong loadedSong, out string loadError))
        {
            Debug.LogWarning($"[GuitarBridgeServer] Detector gameplay test load failed: {loadError}");
            LogNotesDetectorEditor($"TEST_LOAD_FAILED path={definitionPath} error={loadError}");
            return;
        }

        CaptureNotesDetectorGameplayReturnState();
        LogNotesDetectorEditor($"TEST_START song={loadedSong.DisplayName} path={definitionPath} notes={loadedSong.Notes?.Count ?? 0}");

        showToneLab = false;
        HideToneLabUi();
        notesDetectorGameplayTestActive = true;
        showNotesDetectorTestMenu = false;
        showNotesDetectorTestSelectionPopup = false;
        showNotesDetectorRoutinePopup = false;
        notesDetectorRoutineStageIndex = 0;
        notesDetectorRoutineMatchedSinceTime = -1f;
        notesDetectorRoutineOpenedTime = 0f;
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSettings = false;
        showSongSelection = false;
        songSelectionSongConfirmed = false;
        showTrackSelection = false;
        showGlobalSettings = false;
        showGameModes = false;
        showHeroModeSettings = false;
        showLoopSettings = false;
        showLoopPausePopup = false;
        showRocksmithDifficultyPopup = false;
        showStartupTuningReminder = false;
        resumeGameplayAfterStartupTuningReminder = false;
        loopSettingsOpenedFromGameModes = false;
        selectedPauseActionIndex = 8;
        selectedSongEndActionIndex = 0;
        selectedLoopPausePopupIndex = 0;
        loopSettingsPreviewPlaying = false;
        loopRestartPauseRemainingSeconds = 0f;
        gameplayMode = GuitarGameplayMode.Guitar;
        renderMode = GuitarRenderMode.Highway3D;
        noteByNoteModeEnabled = true;
        heroModeEnabled = false;
        songPlaybackAudioMode = SongPlaybackAudioMode.Generated;
        scoreSaveInvalidated = true;
        currentLoadedTrackIndex = -1;
        midiTrackIndex = -1;
        useAutoTrackSelection = true;
        selectedMusicXmlPartId = string.Empty;
        currentSongPartSummaries.Clear();
        latestDetectedPitches.Clear();
        recentNoteEvents.Clear();
        latestNoteEventId = 0;
        latestEventNotesText = "--";
        ResetNotesDetectorAcceptanceSource();
        latestPacketHadEvent = false;
        currentSongEntry = new SongLibraryEntry
        {
            SongId = "__detector_test__",
            LibraryType = SongLibraryType.Guitar,
            DisplayName = string.IsNullOrWhiteSpace(loadedSong.DisplayName) ? "Detection Check" : loadedSong.DisplayName,
            Subtitle = string.IsNullOrWhiteSpace(loadedSong.CategoryTitle) ? "Detector Test" : loadedSong.CategoryTitle,
            SongDirectory = Path.Combine(ExternalContentPaths.PersistentRoot, NotesDetectorTestSongFolderName),
            MetadataPath = GetNotesDetectorTestMetadataPath(),
            PrimaryNotationPath = definitionPath,
            PrimaryNotationKind = SongNotationSourceKind.None,
            DifficultyDisplayLabel = "Detector"
        };

        string metadataDirectory = Path.GetDirectoryName(currentSongEntry.MetadataPath);
        if (!string.IsNullOrWhiteSpace(metadataDirectory))
            Directory.CreateDirectory(metadataDirectory);

        ResetSessionScoreState();
        ClearNoteByNoteWaitingState();
        SetSongEndState(false);
        InitializeSongMetadataAndAudio();

        chartNotes = loadedSong.Notes ?? new List<NoteData>();
        currentArpeggioGuides = new List<ArpeggioGuideData>();
        chartNoteById.Clear();
        for (int i = 0; i < chartNotes.Count; i++)
        {
            NoteData note = chartNotes[i];
            if (note.id < 0)
                note.id = i;
            chartNotes[i] = note;
            chartNoteById[note.id] = note;
        }

        if (Application.isEditor)
        {
            for (int i = 0; i < chartNotes.Count; i++)
            {
                NoteData note = chartNotes[i];
                LogNotesDetectorEditor($"TEST_NOTE index={i} id={note.id} chord={note.chordId} string={note.stringIdx} fret={note.fret} note={note.note ?? "--"} time={note.time.ToString("F3", CultureInfo.InvariantCulture)} duration={note.duration.ToString("F3", CultureInfo.InvariantCulture)}");
            }
        }

        noteStates = chartNotes.Select(note => new GameplayNoteState(note)).ToList();
        activeStringBasePitch = (int[])StringTuningUtils.StandardGuitarTuning.Clone();
        GenerateTabSections();
        ResetActiveRendererContent();

        songTimer = -songStartDelaySeconds;
        audioSongTimer = -songStartDelaySeconds;
        isPaused = false;
        MarkDetectorHintDirty();
        ApplyPlaybackSpeedToAudio();
        SyncAudioToSongTimer(playImmediately: false, forceSeek: true);
    }

    private void ExitNotesDetectorGameplayTest(bool reopenDetectorMenu)
    {
        LogNotesDetectorEditor($"TEST_EXIT reopenMenu={reopenDetectorMenu} songTimer={songTimer.ToString("F3", CultureInfo.InvariantCulture)}");
        notesDetectorGameplayTestActive = false;
        showNotesDetectorRoutinePopup = false;
        notesDetectorRoutineStageIndex = 0;
        notesDetectorRoutineMatchedSinceTime = -1f;
        notesDetectorRoutineOpenedTime = 0f;
        ResetNotesDetectorAcceptanceSource();
        ClearNoteByNoteWaitingState();

        SongLibraryEntry restoreSongEntry = notesDetectorGameplayReturnSongEntry;
        string restoreSelectedPartId = notesDetectorGameplayReturnUseAutoTrackSelection
            ? null
            : notesDetectorGameplayReturnSelectedMusicXmlPartId;
        bool restoreNoteByNoteMode = notesDetectorGameplayReturnNoteByNoteModeEnabled;
        bool restoreHeroMode = notesDetectorGameplayReturnHeroModeEnabled;
        GuitarRenderMode restoreRenderMode = notesDetectorGameplayReturnRenderMode;

        notesDetectorGameplayReturnSongEntry = null;
        notesDetectorGameplayReturnSelectedMusicXmlPartId = string.Empty;
        notesDetectorGameplayReturnUseAutoTrackSelection = true;
        notesDetectorGameplayReturnNoteByNoteModeEnabled = false;
        notesDetectorGameplayReturnHeroModeEnabled = false;

        if (restoreSongEntry != null)
        {
            LoadSongFromEntry(restoreSongEntry, restoreSelectedPartId);
        }
        else
        {
            currentSongEntry = null;
            LoadTestSong(preservePauseUiState: true);
        }

        noteByNoteModeEnabled = restoreNoteByNoteMode;
        heroModeEnabled = restoreHeroMode;
        renderMode = restoreRenderMode;
        ClearNoteByNoteWaitingState();
        SetSongEndState(false);
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false, forceSeek: true);

        if (reopenDetectorMenu)
            OpenNotesDetectorTestFromUi();
        else
            CloseNotesDetectorTestFromUi();
    }

    public void OpenSongSelectionFromUi()
    {
        if (!firstStartCompleted)
        {
            OpenStartMenuFromUi();
            return;
        }

        multiplayerRhythmModeActive = false;
        multiplayerRhythmWinningPlayerIndex = -1;
        pendingMultiplayerRhythmSongSelection = false;
        returnToMultiplayerRhythmSetupFromSongSelection = false;
        songSelectionOpenedFromSongEnd = false;
        songSelectionOpenedFromMainMenu = showMainMenu || mainMenuFlowActive;
        showNotesDetectorTestMenu = false;
        if (!showMainMenu)
            mainMenuFlowActive = false;
        OpenSongSelectionMenu();
    }

    public void OpenLibraryFromPauseFromUi()
    {
        bool reopenMultiplayerRhythm = multiplayerRhythmModeActive;
        ResetTransientMenuNavigationState();
        OpenMainMenuFromUi();
        pendingMultiplayerRhythmSongSelection = reopenMultiplayerRhythm;
        returnToMultiplayerRhythmSetupFromSongSelection = false;
        songSelectionOpenedFromSongEnd = false;
        songSelectionOpenedFromMainMenu = showMainMenu || mainMenuFlowActive;
        showNotesDetectorTestMenu = false;
        OpenSongSelectionMenu();
    }

    private void ResetTransientMenuNavigationState()
    {
        gameplayHudPreviewInMenus = false;
        selectedPauseActionIndex = GetFirstVisiblePauseActionIndex();
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
        showMultiplayerRhythmSetup = false;
        showStartMenu = false;
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
        pendingRocksmithTrackSelectionGroups.Clear();
        pendingArcadeArrangementSummaries.Clear();
        showStartupTuningReminder = false;
        resumeGameplayAfterStartupTuningReminder = false;
    }

    private void ShowStartupTuningReminder(bool resumePlaybackAfterDismiss)
    {
        showStartupTuningReminder = true;
        resumeGameplayAfterStartupTuningReminder = resumePlaybackAfterDismiss;
        startupTuningReminderShownFrame = Time.frameCount;
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void DismissStartupTuningReminderFromUi()
    {
        if (!showStartupTuningReminder)
            return;

        showStartupTuningReminder = false;
        startupTuningReminderShownFrame = -1;
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
        showRocksmithDifficultyPopup = false;
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
        RestartCurrentRunFromUi();
    }

    private void RestartCurrentRunFromUi()
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
        showStartupTuningReminder = false;
        resumeGameplayAfterStartupTuningReminder = false;
        showLoopPausePopup = false;
        selectedLoopPausePopupIndex = 0;
        loopRestartPauseRemainingSeconds = 0f;
        loopSettingsPreviewPlaying = false;
        isPaused = false;
        ClearNoteByNoteWaitingState();
        scoreSaveInvalidated = IsScoreInvalidatingModeActive();
        ResetSessionScoreState();

        if (loopEnabled && HasConfiguredLoopWindow())
        {
            if (loopPauseDurationSeconds > 0.001f)
            {
                StartLoopRestartPause();
                return;
            }

            SeekSongTime(loopStartTime, false);
            SyncAudioToSongTimer(playImmediately: true);
            return;
        }

        SeekSongTime(-songStartDelaySeconds, false);
        SyncAudioToSongTimer(playImmediately: true);
    }

    public void OpenSongSettingsFromUi()
    {
        showToneLab = false;
        HideToneLabUi();
        showNotesDetectorTestMenu = false;
        gameplayHudPreviewInMenus = false;
        showSongSettings = true;
        showGeneratedAudioTrackSelectionPopup = false;
        showSongSettingsTrackSelectionPopup = false;
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
        CancelDeferredSongSelectionOpen();
        showToneLab = false;
        HideToneLabUi();
        showNotesDetectorTestMenu = false;
        gameplayHudPreviewInMenus = false;
        if (!showMainMenu)
            mainMenuFlowActive = false;

        showGlobalSettings = true;
        globalSettingsTransparentBackground = false;
        selectedGlobalSettingsTopIndex = 0;
        selectedGlobalSettingsItemIndex = 0;
        activeGlobalSettingsCategory = string.Empty;
        showSongSettings = false;
        showStartMenu = false;
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
        CancelDeferredSongSelectionOpen();
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
        ClearSongSelectionCaches();
        SongLibraryService.ClearCache();
        RefreshAvailableSongs(forceRefresh: true);

        songSelectionSongConfirmed = false;
        EnsureSongSelectionVisible();
    }

    public void MoveSongSelectionFromUi(int delta)
    {
        MoveSongSelection(delta);
    }

    public void SelectSongByIndexFromUi(int songIndex)
    {
        selectedSongListIndex = Mathf.Clamp(songIndex, 0, Mathf.Max(0, displayedSongLibraryEntries.Count - 1));
        songSelectionSongConfirmed = false;
        ActivateSelectedSongLibraryEntry();
        EnsureSongSelectionVisible();
    }

    public void ToggleSongFavoriteByIndexFromUi(int songIndex)
    {
        if (songIndex < 0 || songIndex >= displayedSongLibraryEntries.Count)
            return;

        SongLibraryBrowseEntry entry = displayedSongLibraryEntries[songIndex];
        if (entry == null || !entry.IsSong || entry.Song == null)
            return;

        SongLibraryEntry songEntry = entry.Song;
        string toggledSongDirectory = songEntry.SongDirectory;
        SongMetadata metadata;
        bool isCurrentSong = currentSongEntry != null &&
                             string.Equals(currentSongEntry.SongDirectory, songEntry.SongDirectory, StringComparison.OrdinalIgnoreCase);
        if (isCurrentSong)
        {
            metadata = songMetadata ?? new SongMetadata();
        }
        else
        {
            metadata = LoadSongMetadataForEntry(songEntry);
        }

        metadata.favoriteInLibrary = !metadata.favoriteInLibrary;

        if (isCurrentSong)
            songMetadata.favoriteInLibrary = metadata.favoriteInLibrary;

        SaveSongMetadata(metadata, BuildSongMetadataPath(songEntry), ResolveSongMetadataFileName(songEntry));
        RefreshAvailableSongs();

        if (!string.IsNullOrWhiteSpace(toggledSongDirectory))
        {
            int toggledIndex = displayedSongLibraryEntries.FindIndex(displayedEntry =>
                displayedEntry != null &&
                displayedEntry.IsSong &&
                displayedEntry.Song != null &&
                string.Equals(displayedEntry.Song.SongDirectory, toggledSongDirectory, StringComparison.OrdinalIgnoreCase));
            if (toggledIndex >= 0)
            {
                selectedSongListIndex = toggledIndex;
                songSelectionSongConfirmed = false;
                SyncPendingTrackSelectionToDisplayedEntry();
                EnsureSongSelectionVisible();
            }
        }
    }

    public void SetSongLibraryBrowseModeFromUi(int modeIndex)
    {
        SongLibraryBrowseMode requestedMode = (SongLibraryBrowseMode)Mathf.Clamp(modeIndex, (int)SongLibraryBrowseMode.All, (int)SongLibraryBrowseMode.Albums);
        SetSongLibraryBrowseMode(requestedMode);
    }

    public void SetSongLibraryTypeFromUi(int typeIndex)
    {
        if (pendingMultiplayerRhythmSongSelection || multiplayerRhythmModeActive || showMultiplayerRhythmSetup)
        {
            SetSongLibraryType(SongLibraryType.Arcade);
            return;
        }

        SongLibraryType requestedType = (SongLibraryType)Mathf.Clamp(typeIndex, (int)SongLibraryType.Guitar, (int)SongLibraryType.Arcade);
        SetSongLibraryType(requestedType);
    }

    private void SetSongLibraryType(SongLibraryType type)
    {
        if (selectedSongLibraryType == type)
            return;

        selectedSongLibraryType = type;
        PlayerPrefs.SetInt(SelectedSongLibraryTypePrefsKey, (int)selectedSongLibraryType);
        PlayerPrefs.Save();
        songLibraryBrowseScopeKey = string.Empty;
        selectedSongListIndex = 0;
        selectedTrackListIndex = 0;
        songSelectionSongConfirmed = false;
        pendingTrackSelectionSong = null;
        pendingTrackSelectionParts.Clear();
        pendingRocksmithTrackSelectionGroups.Clear();
        pendingArcadeArrangementSummaries.Clear();
        RefreshAvailableSongs();
    }

    private void SetSongLibraryBrowseMode(SongLibraryBrowseMode mode)
    {
        if (songLibraryBrowseMode == mode && !IsSongLibraryScopeActive())
            return;

        songLibraryBrowseMode = mode;
        songLibraryBrowseScopeKey = string.Empty;
        selectedSongListIndex = 0;
        songSelectionSongConfirmed = false;
        RebuildDisplayedSongLibraryEntries();
    }

    private void ActivateSelectedSongLibraryEntry()
    {
        SongLibraryBrowseEntry selectedEntry = GetSelectedSongLibraryBrowseEntry();
        if (selectedEntry == null)
            return;

        if (!selectedEntry.IsSong)
        {
            songLibraryBrowseScopeKey = selectedEntry.GroupKey ?? string.Empty;
            selectedSongListIndex = 0;
            songSelectionSongConfirmed = false;
            RebuildDisplayedSongLibraryEntries();
            return;
        }

        SyncPendingTrackSelectionToDisplayedEntry();
        songSelectionSongConfirmed = true;
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
        bool returnToMultiplayerSetup = returnToMultiplayerRhythmSetupFromSongSelection;
        showMultiplayerRhythmSetup = returnToMultiplayerSetup;
        showMainMenu = !returnToMultiplayerSetup && mainMenuFlowActive;
        if (!returnToMultiplayerSetup)
        {
            pendingMultiplayerRhythmSongSelection = false;
            returnToMultiplayerRhythmSetupFromSongSelection = false;
        }
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
        int count = pendingTrackSelectionSong != null && pendingTrackSelectionSong.LibraryType == SongLibraryType.Arcade
            ? pendingArcadeArrangementSummaries.Count
            : IsPendingRocksmithDifficultySelectionActive()
                ? pendingRocksmithTrackSelectionGroups.Count
                : pendingTrackSelectionParts.Count;
        selectedTrackListIndex = Mathf.Clamp(trackIndex, 0, Mathf.Max(0, count - 1));
        ClampSelectedArcadeDifficultyToPendingArrangement();
        ClampSelectedRocksmithDifficultyToPendingGroup();
        songSelectionSongConfirmed = true;
        EnsureTrackSelectionVisible();
    }

    public void StartSelectedSongFromUi()
    {
        songSelectionSongConfirmed = true;
        ConfirmTrackSelection();
    }

    public void SetArcadeDifficultyFromUi(int difficultyIndex)
    {
        ArcadeDifficulty requested = DifficultyFromUiIndex(difficultyIndex);
        ArcadeArrangementSummary selectedSummary = GetPendingSelectedArcadeArrangementSummary();
        if (selectedSummary == null || selectedSummary.Difficulties == null || !selectedSummary.Difficulties.Contains(requested))
            return;

        selectedArcadeDifficulty = requested;
        songSelectionSongConfirmed = true;
        if (pendingTrackSelectionSong != null && pendingTrackSelectionSong.LibraryType == SongLibraryType.Arcade)
        {
            SongMetadata metadata = LoadSongMetadataForEntry(pendingTrackSelectionSong);
            metadata.selectedArcadeArrangementId = selectedSummary.ArrangementId;
            metadata.selectedArcadeDifficulty = ArcadeCloneHeroLoader.SerializeDifficulty(selectedArcadeDifficulty);
            SaveSongMetadata(metadata, BuildSongMetadataPath(pendingTrackSelectionSong), ResolveSongMetadataFileName(pendingTrackSelectionSong));
        }
    }

    private void HandleRocksmithDifficultyPopupControls()
    {
        if (IsUiBackPressed())
        {
            CloseRocksmithDifficultyPopupFromUi();
            return;
        }

        int horizontalDirection = GetHeldHorizontalArrowDirection();
        if (ConsumeHeldHorizontalUiStep("rocksmith-difficulty-popup", horizontalDirection))
        {
            AdjustRocksmithGameplayDifficultyFromUi(horizontalDirection);
            return;
        }

        if (horizontalDirection == 0)
            ConsumeHeldHorizontalUiStep("rocksmith-difficulty-popup", 0);

        if (IsUiSubmitPressed())
        {
            ConfirmRocksmithDifficultyPopupFromUi();
            return;
        }
    }

    public void SetLibraryDifficultyFromUi(int difficultyIndex)
    {
        if (pendingTrackSelectionSong != null && pendingTrackSelectionSong.LibraryType == SongLibraryType.Arcade)
            SetArcadeDifficultyFromUi(difficultyIndex);
    }

    private static ArcadeDifficulty DifficultyFromUiIndex(int difficultyIndex)
    {
        switch (difficultyIndex)
        {
            case 0:
                return ArcadeDifficulty.Expert;
            case 1:
                return ArcadeDifficulty.Hard;
            case 2:
                return ArcadeDifficulty.Medium;
            default:
                return ArcadeDifficulty.Easy;
        }
    }

    private static int DifficultyToUiIndex(ArcadeDifficulty difficulty)
    {
        switch (difficulty)
        {
            case ArcadeDifficulty.Expert:
                return 0;
            case ArcadeDifficulty.Hard:
                return 1;
            case ArcadeDifficulty.Medium:
                return 2;
            default:
                return 3;
        }
    }

    private ArcadeArrangementSummary GetPendingSelectedArcadeArrangementSummary()
    {
        if (selectedTrackListIndex < 0 || selectedTrackListIndex >= pendingArcadeArrangementSummaries.Count)
            return null;

        return pendingArcadeArrangementSummaries[selectedTrackListIndex];
    }

    private ArcadeArrangementSummary GetCurrentArcadeArrangementSummary()
    {
        if (currentArcadeArrangementSummaries == null)
            return null;

        return currentArcadeArrangementSummaries.FirstOrDefault(summary =>
            string.Equals(summary.ArrangementId, selectedArcadeArrangementId, StringComparison.OrdinalIgnoreCase));
    }

    private string GetSelectedArcadeArrangementDisplayName()
    {
        return GetCurrentArcadeArrangementSummary()?.DisplayName
               ?? GetPendingSelectedArcadeArrangementSummary()?.DisplayName
               ?? ArcadeCloneHeroLoader.GetArrangementDisplayName(ArcadeInstrument.Guitar);
    }

    private void ClampSelectedArcadeDifficultyToPendingArrangement()
    {
        ArcadeArrangementSummary selectedSummary = GetPendingSelectedArcadeArrangementSummary();
        if (selectedSummary == null || selectedSummary.Difficulties == null || selectedSummary.Difficulties.Count == 0)
            return;

        if (!selectedSummary.Difficulties.Contains(selectedArcadeDifficulty))
            selectedArcadeDifficulty = ArcadeCloneHeroLoader.GetBestDefaultDifficulty(selectedSummary.Difficulties);
    }

    private void MoveArcadeDifficultySelection(int delta)
    {
        ArcadeArrangementSummary selectedSummary = GetPendingSelectedArcadeArrangementSummary();
        if (selectedSummary == null || selectedSummary.Difficulties == null || selectedSummary.Difficulties.Count == 0)
            return;

        List<ArcadeDifficulty> ordered = new List<ArcadeDifficulty>
        {
            ArcadeDifficulty.Expert,
            ArcadeDifficulty.Hard,
            ArcadeDifficulty.Medium,
            ArcadeDifficulty.Easy
        }.Where(difficulty => selectedSummary.Difficulties.Contains(difficulty)).ToList();
        if (ordered.Count == 0)
            return;

        int currentIndex = ordered.IndexOf(selectedArcadeDifficulty);
        if (currentIndex < 0)
            currentIndex = 0;

        int nextIndex = Mathf.Clamp(currentIndex + delta, 0, ordered.Count - 1);
        SetArcadeDifficultyFromUi(DifficultyToUiIndex(ordered[nextIndex]));
    }

    private bool IsPendingRocksmithDifficultySelectionActive()
    {
        return pendingTrackSelectionSong != null &&
               pendingTrackSelectionSong.LibraryType == SongLibraryType.Guitar &&
               pendingTrackSelectionSong.PrimaryNotationKind == SongNotationSourceKind.Rocksmith &&
               pendingRocksmithTrackSelectionGroups.Count > 0;
    }

    private bool PendingRocksmithSelectionHasMultipleDifficulties()
    {
        return pendingRocksmithTrackSelectionGroups.Any(group => group != null && group.Variants.Count > 1);
    }

    private RocksmithTrackSelectionGroup GetPendingSelectedRocksmithTrackGroup()
    {
        if (!IsPendingRocksmithDifficultySelectionActive())
            return null;

        if (selectedTrackListIndex < 0 || selectedTrackListIndex >= pendingRocksmithTrackSelectionGroups.Count)
            return null;

        return pendingRocksmithTrackSelectionGroups[selectedTrackListIndex];
    }

    private static string GetRocksmithDifficultyLabelFromUiIndex(int difficultyIndex)
    {
        switch (Mathf.Clamp(difficultyIndex, 0, 3))
        {
            case 0: return "X";
            case 1: return "H";
            case 2: return "M";
            default: return "E";
        }
    }

    private static string BuildRocksmithDifficultySummary(IEnumerable<MusicXmlLoader.MusicXmlPartSummary> variants)
    {
        if (variants == null)
            return string.Empty;

        HashSet<string> labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (MusicXmlLoader.MusicXmlPartSummary variant in variants)
        {
            if (variant == null || string.IsNullOrWhiteSpace(variant.DifficultyLabel))
                continue;

            labels.Add(variant.DifficultyLabel.Trim().ToUpperInvariant());
        }

        string[] ordered = { "X", "H", "M", "E" };
        return string.Concat(ordered.Where(labels.Contains));
    }

    private static string GetRocksmithDifficultyDisplayNameFromUiIndex(int difficultyIndex)
    {
        switch (Mathf.Clamp(difficultyIndex, 0, 3))
        {
            case 0: return "Expert";
            case 1: return "Hard";
            case 2: return "Medium";
            default: return "Easy";
        }
    }

    private static string GetRocksmithGroupId(MusicXmlLoader.MusicXmlPartSummary summary)
    {
        if (summary == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(summary.GroupId))
            return summary.GroupId.Trim();

        return GetRocksmithGroupIdFromPartId(summary.PartId);
    }

    private static string GetRocksmithGroupIdFromPartId(string partId)
    {
        if (string.IsNullOrWhiteSpace(partId))
            return string.Empty;

        string trimmed = partId.Trim();
        int levelSuffixIndex = trimmed.IndexOf("::level-", StringComparison.OrdinalIgnoreCase);
        if (levelSuffixIndex > 0)
            return trimmed.Substring(0, levelSuffixIndex);

        return trimmed;
    }

    private static int ResolveRocksmithDifficultyUiIndex(MusicXmlLoader.MusicXmlPartSummary summary)
    {
        if (summary == null)
            return -1;

        if (summary.DifficultyUiIndex >= 0)
            return summary.DifficultyUiIndex;

        string label = summary.DifficultyLabel?.Trim();
        if (string.Equals(label, "Full", StringComparison.OrdinalIgnoreCase))
            return 0;

        return int.TryParse(label, out int numericLevel) ? Mathf.Max(0, numericLevel) : -1;
    }

    private static List<MusicXmlLoader.MusicXmlPartSummary> OrderRocksmithVariants(IEnumerable<MusicXmlLoader.MusicXmlPartSummary> variants)
    {
        if (variants == null)
            return new List<MusicXmlLoader.MusicXmlPartSummary>();

        return variants
            .Where(variant => variant != null)
            .OrderBy(ResolveRocksmithDifficultyUiIndex)
            .ThenByDescending(variant => variant.Score)
            .ThenBy(variant => variant.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static MusicXmlLoader.MusicXmlPartSummary GetHighestDifficultyRocksmithVariant(IEnumerable<MusicXmlLoader.MusicXmlPartSummary> variants)
    {
        return OrderRocksmithVariants(variants).FirstOrDefault();
    }

    private void RebuildPendingRocksmithTrackSelectionGroups()
    {
        pendingRocksmithTrackSelectionGroups.Clear();
        if (pendingTrackSelectionSong == null ||
            pendingTrackSelectionSong.LibraryType != SongLibraryType.Guitar ||
            pendingTrackSelectionSong.PrimaryNotationKind != SongNotationSourceKind.Rocksmith ||
            pendingTrackSelectionParts.Count == 0)
        {
            return;
        }

        Dictionary<string, RocksmithTrackSelectionGroup> groupsById = new Dictionary<string, RocksmithTrackSelectionGroup>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < pendingTrackSelectionParts.Count; i++)
        {
            MusicXmlLoader.MusicXmlPartSummary summary = pendingTrackSelectionParts[i];
            if (summary == null)
                continue;

            string groupId = string.IsNullOrWhiteSpace(summary.GroupId) ? summary.PartId ?? string.Empty : summary.GroupId;
            if (!groupsById.TryGetValue(groupId, out RocksmithTrackSelectionGroup group))
            {
                group = new RocksmithTrackSelectionGroup
                {
                    GroupId = groupId,
                    DisplayName = string.IsNullOrWhiteSpace(summary.GroupDisplayName) ? summary.Name : summary.GroupDisplayName,
                    TuningDisplayName = summary.TuningDisplayName ?? string.Empty
                };
                groupsById[groupId] = group;
                pendingRocksmithTrackSelectionGroups.Add(group);
            }

            if (!string.IsNullOrWhiteSpace(summary.TuningDisplayName) && string.IsNullOrWhiteSpace(group.TuningDisplayName))
                group.TuningDisplayName = summary.TuningDisplayName;

            group.Variants.Add(summary);
        }

        for (int i = 0; i < pendingRocksmithTrackSelectionGroups.Count; i++)
        {
            pendingRocksmithTrackSelectionGroups[i].Variants.Sort((left, right) =>
            {
                int leftIndex = ResolveRocksmithDifficultyUiIndex(left);
                int rightIndex = ResolveRocksmithDifficultyUiIndex(right);
                int compare = leftIndex.CompareTo(rightIndex);
                if (compare != 0)
                    return compare;
                return string.Compare(left?.Name ?? string.Empty, right?.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            });
        }
    }

    private MusicXmlLoader.MusicXmlPartSummary GetPendingSelectedRocksmithVariant(bool allowFallbackToNearest)
    {
        RocksmithTrackSelectionGroup group = GetPendingSelectedRocksmithTrackGroup();
        if (group == null || group.Variants.Count == 0)
            return null;

        for (int i = 0; i < group.Variants.Count; i++)
        {
            MusicXmlLoader.MusicXmlPartSummary variant = group.Variants[i];
            if (ResolveRocksmithDifficultyUiIndex(variant) == selectedRocksmithDifficultyIndex)
                return variant;
        }

        if (!allowFallbackToNearest)
            return null;

        return group.Variants
            .OrderBy(variant => Mathf.Abs(ResolveRocksmithDifficultyUiIndex(variant) - selectedRocksmithDifficultyIndex))
            .ThenByDescending(variant => variant.Score)
            .FirstOrDefault();
    }

    private void ClampSelectedRocksmithDifficultyToPendingGroup()
    {
        RocksmithTrackSelectionGroup group = GetPendingSelectedRocksmithTrackGroup();
        if (group == null || group.Variants.Count == 0)
            return;

        if (group.Variants.Any(variant => ResolveRocksmithDifficultyUiIndex(variant) == selectedRocksmithDifficultyIndex))
            return;

        MusicXmlLoader.MusicXmlPartSummary fallback = group.Variants
            .OrderBy(variant => ResolveRocksmithDifficultyUiIndex(variant))
            .FirstOrDefault();
        selectedRocksmithDifficultyIndex = fallback != null ? Mathf.Max(0, ResolveRocksmithDifficultyUiIndex(fallback)) : 0;
    }

    private void SetRocksmithDifficultyFromUi(int difficultyIndex)
    {
        if (!IsPendingRocksmithDifficultySelectionActive())
            return;

        RocksmithTrackSelectionGroup group = GetPendingSelectedRocksmithTrackGroup();
        if (group == null)
            return;

        int requested = Mathf.Max(0, difficultyIndex);
        if (!group.Variants.Any(variant => ResolveRocksmithDifficultyUiIndex(variant) == requested))
            return;

        selectedRocksmithDifficultyIndex = requested;
        songSelectionSongConfirmed = true;

        MusicXmlLoader.MusicXmlPartSummary selectedVariant = GetPendingSelectedRocksmithVariant(allowFallbackToNearest: false);
        if (selectedVariant != null && pendingTrackSelectionSong != null)
        {
            SongMetadata metadata = LoadSongMetadataForEntry(pendingTrackSelectionSong);
            metadata.useAutoTrackSelection = false;
            metadata.selectedMusicXmlPartId = GetPersistentRocksmithPartId(selectedVariant.PartId, pendingTrackSelectionParts);
            SaveSongMetadata(metadata, BuildSongMetadataPath(pendingTrackSelectionSong), ResolveSongMetadataFileName(pendingTrackSelectionSong));
        }
    }

    private void MoveRocksmithDifficultySelection(int delta)
    {
        RocksmithTrackSelectionGroup group = GetPendingSelectedRocksmithTrackGroup();
        if (group == null || group.Variants.Count == 0)
            return;

        List<int> ordered = group.Variants
            .Select(ResolveRocksmithDifficultyUiIndex)
            .Where(index => index >= 0)
            .Distinct()
            .OrderBy(index => index)
            .ToList();
        if (ordered.Count == 0)
            return;

        int currentIndex = ordered.IndexOf(selectedRocksmithDifficultyIndex);
        if (currentIndex < 0)
            currentIndex = 0;

        int nextIndex = Mathf.Clamp(currentIndex + delta, 0, ordered.Count - 1);
        SetRocksmithDifficultyFromUi(ordered[nextIndex]);
    }

    private bool IsCurrentRocksmithDifficultyModeAvailable()
    {
        return gameplayMode == GuitarGameplayMode.Guitar &&
               currentSongEntry != null &&
               currentSongEntry.PrimaryNotationKind == SongNotationSourceKind.Rocksmith &&
               GetCurrentRocksmithDifficultyVariants().Count > 1;
    }

    private List<MusicXmlLoader.MusicXmlPartSummary> GetCurrentRocksmithDifficultyVariants()
    {
        if (currentSongPartSummaries == null || currentSongPartSummaries.Count == 0)
            return new List<MusicXmlLoader.MusicXmlPartSummary>();

        MusicXmlLoader.MusicXmlPartSummary activeSummary = GetResolvedActiveTrackSummary();
        string groupId = GetRocksmithGroupId(activeSummary);
        if (string.IsNullOrWhiteSpace(groupId))
            return new List<MusicXmlLoader.MusicXmlPartSummary>();

        return OrderRocksmithVariants(currentSongPartSummaries.Where(summary =>
            string.Equals(GetRocksmithGroupId(summary), groupId, StringComparison.OrdinalIgnoreCase)));
    }

    private int GetCurrentRocksmithDifficultyVariantIndex()
    {
        List<MusicXmlLoader.MusicXmlPartSummary> variants = GetCurrentRocksmithDifficultyVariants();
        if (variants.Count == 0)
            return 0;

        string activePartId = GetResolvedActiveTrackSummary()?.PartId ?? string.Empty;
        int index = variants.FindIndex(variant =>
            string.Equals(variant.PartId, activePartId, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : 0;
    }

    private bool IsCurrentRocksmithDifficultyReduced()
    {
        return IsCurrentRocksmithDifficultyModeAvailable() && GetCurrentRocksmithDifficultyVariantIndex() > 0;
    }

    private int GetCurrentRocksmithDifficultyDisplayIndex()
    {
        List<MusicXmlLoader.MusicXmlPartSummary> variants = GetCurrentRocksmithDifficultyVariants();
        if (variants.Count == 0)
            return 0;

        int backendIndex = Mathf.Clamp(selectedGameplayRocksmithDifficultyIndex, 0, variants.Count - 1);
        return variants.Count - 1 - backendIndex;
    }

    private MusicXmlLoader.MusicXmlPartSummary GetCurrentRocksmithSelectedVariant()
    {
        List<MusicXmlLoader.MusicXmlPartSummary> variants = GetCurrentRocksmithDifficultyVariants();
        if (variants.Count == 0)
            return null;

        int resolvedIndex = Mathf.Clamp(selectedGameplayRocksmithDifficultyIndex, 0, variants.Count - 1);
        return variants[resolvedIndex];
    }

    private string GetPersistentRocksmithPartId(string partId, IEnumerable<MusicXmlLoader.MusicXmlPartSummary> sourceSummaries = null)
    {
        if (string.IsNullOrWhiteSpace(partId))
            return string.Empty;

        IEnumerable<MusicXmlLoader.MusicXmlPartSummary> summaries = sourceSummaries ?? currentSongPartSummaries;
        if (summaries == null)
            return GetRocksmithGroupIdFromPartId(partId);

        string groupId = GetRocksmithGroupIdFromPartId(partId);
        List<MusicXmlLoader.MusicXmlPartSummary> matchingSummaries = summaries.Where(summary =>
            string.Equals(GetRocksmithGroupId(summary), groupId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Recover from older bad saved values like "lead" / "rhythm" / "bass" that lost the
        // Rocksmith arrangement instance suffix (for example "lead::1").
        if (matchingSummaries.Count == 0 && !groupId.Contains("::", StringComparison.Ordinal))
        {
            string legacyPrefix = groupId + "::";
            matchingSummaries = summaries.Where(summary =>
            {
                string summaryGroupId = GetRocksmithGroupId(summary);
                return summaryGroupId.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase);
            }).ToList();
        }

        MusicXmlLoader.MusicXmlPartSummary highestVariant = GetHighestDifficultyRocksmithVariant(matchingSummaries);
        return highestVariant?.PartId ?? groupId;
    }

    private bool IsScoreInvalidatingModeActive()
    {
        return playbackSpeedPercent < 100f || loopEnabled || IsCurrentRocksmithDifficultyReduced();
    }

    public void OpenRocksmithDifficultyPopupFromUi()
    {
        if (!IsCurrentRocksmithDifficultyModeAvailable())
            return;

        gameplayHudPreviewInMenus = false;
        showGameModes = false;
        showHeroModeSettings = false;
        showRocksmithDifficultyPopup = true;
        selectedGameplayRocksmithDifficultyIndex = GetCurrentRocksmithDifficultyVariantIndex();
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void CloseRocksmithDifficultyPopupFromUi()
    {
        showRocksmithDifficultyPopup = false;
        showGameModes = true;
        selectedGameplayRocksmithDifficultyIndex = GetCurrentRocksmithDifficultyVariantIndex();
        isPaused = true;
        SyncAudioToSongTimer(playImmediately: false);
    }

    public void SetRocksmithGameplayDifficultyFromUi(float variantIndex)
    {
        List<MusicXmlLoader.MusicXmlPartSummary> variants = GetCurrentRocksmithDifficultyVariants();
        if (variants.Count == 0)
            return;

        int displayIndex = Mathf.Clamp(Mathf.RoundToInt(variantIndex), 0, variants.Count - 1);
        selectedGameplayRocksmithDifficultyIndex = variants.Count - 1 - displayIndex;
    }

    public void AdjustRocksmithGameplayDifficultyFromUi(int delta)
    {
        if (delta == 0)
            return;

        List<MusicXmlLoader.MusicXmlPartSummary> variants = GetCurrentRocksmithDifficultyVariants();
        if (variants.Count == 0)
            return;

        int currentDisplayIndex = GetCurrentRocksmithDifficultyDisplayIndex();
        int nextDisplayIndex = Mathf.Clamp(currentDisplayIndex + delta, 0, variants.Count - 1);
        selectedGameplayRocksmithDifficultyIndex = variants.Count - 1 - nextDisplayIndex;
    }

    public void ConfirmRocksmithDifficultyPopupFromUi()
    {
        List<MusicXmlLoader.MusicXmlPartSummary> variants = GetCurrentRocksmithDifficultyVariants();
        if (variants.Count == 0)
        {
            CloseRocksmithDifficultyPopupFromUi();
            return;
        }

        MusicXmlLoader.MusicXmlPartSummary selectedVariant = GetCurrentRocksmithSelectedVariant();
        if (selectedVariant == null)
        {
            CloseRocksmithDifficultyPopupFromUi();
            return;
        }

        bool selectionChanged = !string.Equals(selectedVariant.PartId, selectedMusicXmlPartId, StringComparison.OrdinalIgnoreCase);
        showRocksmithDifficultyPopup = false;
        if (!selectionChanged)
        {
            ResumePlaybackFromUi();
            return;
        }

        useAutoTrackSelection = false;
        selectedMusicXmlPartId = selectedVariant.PartId;
        if (!ReloadCurrentGuitarChartForSelectedTrack())
        {
            selectedMusicXmlPartId = GetPersistentRocksmithPartId(selectedMusicXmlPartId, currentSongPartSummaries);
            ApplyTrackSelectionPreference();
            CloseRocksmithDifficultyPopupFromUi();
            return;
        }

        RefreshEffectiveAudioOffset();

        if (selectedGameplayRocksmithDifficultyIndex > 0)
            scoreSaveInvalidated = true;

        bool shouldRestartLoopFromStart = loopEnabled && HasConfiguredLoopWindow();
        bool shouldArmLoopCountdown = shouldRestartLoopFromStart && GetActiveLoopPauseDurationSeconds() > 0.001f;
        pendingLoopRestartFromStartAfterResume = shouldRestartLoopFromStart;
        pendingLoopStartCountdownAfterResume = shouldArmLoopCountdown;

        RestartCurrentSongForModeChange();
        ResumePlaybackFromUi();
    }

    public void BackToSongSelectionFromUi()
    {
        CloseTrackSelection();
    }

    public void CloseSongSettingsFromUi()
    {
        showToneLab = false;
        HideToneLabUi();
        gameplayHudPreviewInMenus = false;
        showMainMenu = false;
        mainMenuFlowActive = false;
        showSongSettings = false;
        showGeneratedAudioTrackSelectionPopup = false;
        showSongSettingsTrackSelectionPopup = false;
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
        CancelArcadeBindingCapture();
        gameplayHudPreviewInMenus = false;
        showGlobalSettings = false;
        globalSettingsTransparentBackground = false;
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

    public void ActivateGlobalRuntimeSettingFromUi(string settingId)
    {
        if (string.IsNullOrWhiteSpace(settingId) || !runtimeSettingById.TryGetValue(settingId, out RuntimeSettingDefinition definition))
            return;

        definition.Activator?.Invoke();
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

        SongPlaybackAudioMode[] cycle = GetAvailableSongPlaybackAudioModesForCurrentGameMode();
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
        mode = NormalizeSongPlaybackAudioModeForCurrentGameMode(mode);
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

        selectedGlobalSettingsTopIndex = Mathf.Clamp(index, 0, GlobalSettingsTopLevelCount - 1);
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

        selectedGlobalSettingsTopIndex = Mathf.Clamp(index, 0, GlobalSettingsTopLevelCount - 1);
        ActivateCurrentGlobalSettingsSelection();
    }

    public void AdjustGlobalSettingsTopValueFromUi(int index, int delta)
    {
        if (!showGlobalSettings)
            return;

        selectedGlobalSettingsTopIndex = Mathf.Clamp(index, 0, GlobalSettingsTopLevelCount - 1);
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
        bool restartLoopFromStartAfterResume = pendingLoopRestartFromStartAfterResume && loopEnabled && HasConfiguredLoopWindow();
        bool startWithLoopCountdown = pendingLoopStartCountdownAfterResume && loopEnabled && HasConfiguredLoopWindow();
        pendingLoopRestartFromStartAfterResume = false;
        pendingLoopStartCountdownAfterResume = false;
        loopPausePopupResumePlaybackOnConfirm = false;
        showToneLab = false;
        HideToneLabUi();
        gameplayHudPreviewInMenus = false;
        SetSongEndState(false);
        if ((showLoopSettings || showLoopPausePopup) && renderMode != loopSettingsReturnRenderMode)
            renderMode = loopSettingsReturnRenderMode;
        showLoopSettings = false;
        showLoopPausePopup = false;
        showRocksmithDifficultyPopup = false;
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
        if (startWithLoopCountdown)
        {
            StartLoopRestartPause();
            return;
        }

        if (restartLoopFromStartAfterResume)
        {
            ResetSinglePlayerLoopRunState(loopStartTime);
            SeekSongTime(loopStartTime, false);
            return;
        }

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
            preserveResolvedProgressOnRewind: false,
            clearLoopRestartPause: true,
            syncAudioAfterSeek: true,
            playImmediatelyAfterSeek: !isPaused || (showLoopSettings && loopSettingsPreviewPlaying));
    }

    private void SeekSongTimeFromUserNavigation(float targetTime, bool updateSelectedMarker)
    {
        SeekSongTimeInternal(
            targetTime,
            updateSelectedMarker,
            preserveResolvedProgressOnRewind: false,
            clearLoopRestartPause: true,
            syncAudioAfterSeek: true,
            playImmediatelyAfterSeek: !isPaused || (showLoopSettings && loopSettingsPreviewPlaying));
        RebuildSessionProgressAfterUserSeek();
    }

    private void SeekSongTimeForLoopRestartPause(float targetTime)
    {
        SeekSongTimeInternal(
            targetTime,
            updateSelectedMarker: false,
            preserveResolvedProgressOnRewind: true,
            clearLoopRestartPause: false,
            syncAudioAfterSeek: false,
            playImmediatelyAfterSeek: false);
    }

    private void SeekSongTimeInternal(float targetTime, bool updateSelectedMarker, bool preserveResolvedProgressOnRewind, bool clearLoopRestartPause, bool syncAudioAfterSeek, bool playImmediatelyAfterSeek)
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
        if (multiplayerRhythmModeActive)
        {
            ApplyMultiplayerRhythmSeekState(clampedTime, isRewinding, preserveResolvedProgressOnRewind);
        }
        else
        {
        if (isRewinding)
        {
            if (!preserveResolvedProgressOnRewind)
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

                for (int i = 0; i < arcadeNoteStates.Count; i++)
                {
                    ArcadeNoteState noteState = arcadeNoteStates[i];
                    if (noteState.data.time > songTimer)
                    {
                        noteState.result = GameplayNoteResult.Pending;
                        noteState.resolvedAt = -1f;
                        noteState.isJudgeable = false;
                    }
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

            for (int i = 0; i < arcadeNoteStates.Count; i++)
            {
                ArcadeNoteState noteState = arcadeNoteStates[i];
                if (noteState.IsResolved)
                    continue;

                if (clampedTime > noteState.data.time + arcadeHitWindowLate)
                {
                    noteState.result = GameplayNoteResult.Missed;
                    noteState.resolvedAt = clampedTime;
                    noteState.isJudgeable = false;
                }
            }
        }
        }

        recentNoteEvents.Clear();
        arcadeRecentInputEvents.Clear();
        activeArcadeSustains.Clear();
        latestArcadeInputEventId = 0;
        ResetArcadeCombo();
        if (multiplayerRhythmModeActive)
            ClearMultiplayerRhythmSeekTransientState();
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

    private void RebuildSessionProgressAfterUserSeek()
    {
        if (multiplayerRhythmModeActive)
        {
            RebuildMultiplayerRhythmSessionProgressFromResolvedStates();
            return;
        }

        if (gameplayMode == GuitarGameplayMode.Arcade)
        {
            RebuildArcadeSessionProgressFromResolvedStates();
            return;
        }

        RebuildGuitarSessionProgressFromResolvedStates();
    }

    private void RebuildGuitarSessionProgressFromResolvedStates()
    {
        sessionScoredNoteIds.Clear();
        sessionScoreHits = 0;
        sessionScoreMisses = 0;
        currentSessionScoreValue = 0;
        currentSessionScorePercent = 0f;
        guitarComboCount = 0;

        if (noteStates == null || noteStates.Count == 0)
            return;

        Dictionary<int, int> eventNoteCounts = new Dictionary<int, int>();
        Dictionary<int, float> eventTimes = new Dictionary<int, float>();
        Dictionary<int, bool> eventHasPending = new Dictionary<int, bool>();
        Dictionary<int, bool> eventHasMiss = new Dictionary<int, bool>();

        for (int i = 0; i < noteStates.Count; i++)
        {
            GameplayNoteState noteState = noteStates[i];
            if (noteState == null)
                continue;

            int eventKey = GetGuitarScoreEventKey(noteState.data, i);
            eventNoteCounts[eventKey] = eventNoteCounts.TryGetValue(eventKey, out int existingCount) ? existingCount + 1 : 1;
            if (!eventTimes.ContainsKey(eventKey))
                eventTimes[eventKey] = noteState.data.time;

            if (!noteState.IsResolved)
            {
                eventHasPending[eventKey] = true;
                continue;
            }

            if (noteState.IsHit)
                sessionScoreHits++;
            else if (noteState.IsMissed)
            {
                sessionScoreMisses++;
                eventHasMiss[eventKey] = true;
            }
        }

        int comboCount = 0;
        List<int> orderedEventKeys = eventTimes
            .OrderBy(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .Select(pair => pair.Key)
            .ToList();

        for (int i = 0; i < orderedEventKeys.Count; i++)
        {
            int eventKey = orderedEventKeys[i];
            if (eventHasPending.ContainsKey(eventKey))
                continue;

            sessionScoredNoteIds.Add(eventKey);
            if (eventHasMiss.ContainsKey(eventKey))
            {
                comboCount = 0;
                continue;
            }

            int noteCount = eventNoteCounts.TryGetValue(eventKey, out int countedNotes) ? Mathf.Max(1, countedNotes) : 1;
            currentSessionScoreValue += noteCount * 50 * GetGuitarScoreMultiplier(comboCount);
            comboCount++;
        }

        int total = noteStates.Count;
        currentSessionScorePercent = total > 0
            ? Mathf.Clamp(100f * sessionScoreHits / total, 0f, 100f)
            : 0f;
        guitarComboCount = comboCount;
    }

    private void RebuildArcadeSessionProgressFromResolvedStates()
    {
        arcadeSessionScoredNoteIds.Clear();
        sessionScoreHits = 0;
        sessionScoreMisses = 0;
        currentSessionScorePercent = 0f;
        currentSessionArcadeScoreValue = 0;
        activeArcadeSustains.Clear();
        ResetArcadeCombo();

        if (arcadeNoteStates == null || arcadeNoteStates.Count == 0)
            return;

        List<ArcadeNoteState> orderedStates = arcadeNoteStates
            .Where(state => state != null)
            .OrderBy(state => state.data.time)
            .ThenBy(state => GetArcadeConsumeChordId(state.data))
            .ToList();

        int simulatedComboCount = 0;
        HashSet<int> processedChordIds = new HashSet<int>();
        for (int i = 0; i < orderedStates.Count; i++)
        {
            ArcadeNoteState noteState = orderedStates[i];
            if (!noteState.IsResolved)
                continue;

            int chordId = GetArcadeConsumeChordId(noteState.data);
            if (!processedChordIds.Add(chordId))
                continue;

            arcadeSessionScoredNoteIds.Add(chordId);
            if (noteState.IsHit)
            {
                sessionScoreHits++;
                currentSessionArcadeScoreValue += Mathf.Max(1, GetArcadeChordLaneCount(noteState.data)) * 50 * GetArcadeScoreMultiplier(simulatedComboCount);
                currentSessionArcadeScoreValue += GetAwardedArcadeSustainScore(chordId);
                simulatedComboCount++;
            }
            else if (noteState.IsMissed)
            {
                sessionScoreMisses++;
                simulatedComboCount = 0;
            }
        }

        int total = arcadeTotalChordCount > 0 ? arcadeTotalChordCount : CountArcadeChordGroups(arcadeNoteStates);
        currentSessionScorePercent = total > 0
            ? Mathf.Clamp(100f * sessionScoreHits / total, 0f, 100f)
            : 0f;
        arcadeComboCount = 0;
        arcadeComboActive = false;
    }

    private void ApplyMultiplayerRhythmSeekState(float clampedTime, bool isRewinding, bool preserveResolvedProgressOnRewind)
    {
        if (multiplayerRhythmPlayers == null)
            return;

        for (int playerIndex = 0; playerIndex < multiplayerRhythmPlayers.Length; playerIndex++)
        {
            MultiplayerRhythmPlayerState player = multiplayerRhythmPlayers[playerIndex];
            if (player == null || player.noteStates == null)
                continue;

            if (isRewinding)
            {
                if (preserveResolvedProgressOnRewind)
                    continue;

                for (int i = 0; i < player.noteStates.Count; i++)
                {
                    ArcadeNoteState noteState = player.noteStates[i];
                    if (noteState == null || noteState.data.time <= songTimer)
                        continue;

                    noteState.result = GameplayNoteResult.Pending;
                    noteState.resolvedAt = -1f;
                    noteState.isJudgeable = false;
                }

                continue;
            }

            for (int i = 0; i < player.noteStates.Count; i++)
            {
                ArcadeNoteState noteState = player.noteStates[i];
                if (noteState == null || noteState.IsResolved)
                    continue;

                if (clampedTime > noteState.data.time + arcadeHitWindowLate)
                {
                    noteState.result = GameplayNoteResult.Missed;
                    noteState.resolvedAt = clampedTime;
                    noteState.isJudgeable = false;
                }
            }
        }
    }

    private void ClearMultiplayerRhythmSeekTransientState()
    {
        if (multiplayerRhythmPlayers == null)
            return;

        for (int i = 0; i < multiplayerRhythmPlayers.Length; i++)
        {
            MultiplayerRhythmPlayerState player = multiplayerRhythmPlayers[i];
            if (player == null)
                continue;

            player.recentInputEvents.Clear();
            player.activeSustains.Clear();
            player.latestInputEventId = 0;
            player.inputNeedsUnpausedPrime = true;
            Array.Clear(player.heldLanes, 0, player.heldLanes.Length);
            Array.Clear(player.previousHeldLanes, 0, player.previousHeldLanes.Length);
            ResetMultiplayerRhythmCombo(player);
        }
    }

    private void RebuildMultiplayerRhythmSessionProgressFromResolvedStates()
    {
        if (multiplayerRhythmPlayers == null)
            return;

        int total = arcadeTotalChordCount > 0 ? arcadeTotalChordCount : 0;
        for (int playerIndex = 0; playerIndex < multiplayerRhythmPlayers.Length; playerIndex++)
        {
            MultiplayerRhythmPlayerState player = multiplayerRhythmPlayers[playerIndex];
            if (player == null)
                continue;

            player.sessionScoredChordIds.Clear();
            player.hitCount = 0;
            player.missCount = 0;
            player.scorePercent = 0f;
            player.scoreValue = 0;
            player.comboCount = 0;
            player.maxComboCount = 0;
            player.comboActive = false;
            player.activeSustains.Clear();

            if (player.noteStates == null || player.noteStates.Count == 0)
                continue;

            List<ArcadeNoteState> orderedStates = player.noteStates
                .Where(state => state != null)
                .OrderBy(state => state.data.time)
                .ThenBy(state => GetArcadeConsumeChordId(state.data))
                .ToList();

            int simulatedComboCount = 0;
            int maxCombo = 0;
            HashSet<int> processedChordIds = new HashSet<int>();
            for (int i = 0; i < orderedStates.Count; i++)
            {
                ArcadeNoteState noteState = orderedStates[i];
                if (!noteState.IsResolved)
                    continue;

                int chordId = GetArcadeConsumeChordId(noteState.data);
                if (!processedChordIds.Add(chordId))
                    continue;

                player.sessionScoredChordIds.Add(chordId);
                if (noteState.IsHit)
                {
                    player.hitCount++;
                    player.scoreValue += Mathf.Max(1, GetArcadeChordLaneCount(player.noteStates, noteState.data)) * 50 * GetArcadeScoreMultiplier(simulatedComboCount);
                    player.scoreValue += GetAwardedMultiplayerRhythmSustainScore(player, chordId);
                    simulatedComboCount++;
                    maxCombo = Mathf.Max(maxCombo, simulatedComboCount);
                }
                else if (noteState.IsMissed)
                {
                    player.missCount++;
                    simulatedComboCount = 0;
                }
            }

            player.comboCount = 0;
            player.maxComboCount = maxCombo;
            player.comboActive = false;
            player.scorePercent = total > 0
                ? Mathf.Clamp(100f * player.hitCount / total, 0f, 100f)
                : 0f;
        }

        UpdateMultiplayerRhythmWinnerState();
    }

    private void UpdateSelectedLoopMarker(float markerTime)
    {
        if (selectedLoopMarker == 1)
        {
            loopStartTime = Mathf.Max(0f, markerTime);
            loopStartConfigured = true;
            if (loopEndTime < loopStartTime + 0.05f)
            {
                loopEndTime = loopStartTime + 0.05f;
                loopEndConfigured = true;
            }
        }
        else if (selectedLoopMarker == 2)
        {
            loopEndTime = Mathf.Max(loopStartTime + 0.05f, markerTime);
            loopEndConfigured = true;
        }
    }

    private void OnApplicationQuit()
    {
        StopLoopCountdownEditorLogSession("application-quit");
        isRunning = false;
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Join(500);
        if (udpClient != null) udpClient.Close();
        CloseDetectorHintClient();
        StopArcadeMidiInput();
        ShutdownNativeNotesDetectorIfRunning();
        ShutdownNotesDetectorIfRunning();
    }

    private void OnDestroy()
    {
        StopLoopCountdownEditorLogSession("destroy");
        CloseDetectorHintClient();
        StopArcadeMidiInput();
        ShutdownNativeNotesDetectorIfRunning();
        ShutdownNotesDetectorIfRunning();
        ShutdownGeneratedSongPlayer();
    }

    private void OnDisable()
    {
        StopLoopCountdownEditorLogSession("disable");
        CloseDetectorHintClient();
        StopArcadeMidiInput();
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
        {
            if (Application.isEditor && !string.IsNullOrWhiteSpace(notesDetectorEditorLogPath))
                nativeNotesDetectorBridge.SetDebugLogPath(notesDetectorEditorLogPath);
            return;
        }

        nativeNotesDetectorBridge = new NativeNotesDetectorBridge();
        if (Application.isEditor && !string.IsNullOrWhiteSpace(notesDetectorEditorLogPath))
            nativeNotesDetectorBridge.Initialize();
        if (Application.isEditor && !string.IsNullOrWhiteSpace(notesDetectorEditorLogPath))
            nativeNotesDetectorBridge.SetDebugLogPath(notesDetectorEditorLogPath);
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

    public Material CreateSharedTabsGlowMaterial(Color c, float intensity)
    {
        Shader shader = Resources.Load<Shader>("Shaders/TabsGlowUnlit");
        if (shader == null)
            return CreateSharedGlowMaterial(c, intensity);

        Material m = new Material(shader);
        if (m.HasProperty("_Color"))
            m.SetColor("_Color", c);
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", c);
        if (m.HasProperty("_EmissionColor"))
            m.SetColor("_EmissionColor", intensity > 0f ? c * Mathf.Pow(2f, intensity) : Color.black);
        m.color = c;
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
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

    public Material CreateSharedRuntimeTransparentGlowMaterial(Color c, float emission = 0f)
    {
        Shader shader = Resources.Load<Shader>("Shaders/TabsTransparentUnlit");
        if (shader == null)
            return CreateSharedTransparentMaterial(c, emission);

        Material m = new Material(shader);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetColor("_Color", c);
        m.SetColor("_BaseColor", c);
        m.color = c;

        if (m.HasProperty("_ZWrite"))
            m.SetInt("_ZWrite", 0);
        if (m.HasProperty("_Cull"))
            m.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", c * Mathf.Pow(2f, emission));
        }
        else if (m.HasProperty("_EmissionColor"))
        {
            m.SetColor("_EmissionColor", Color.black);
        }

        return m;
    }

    public Material CreateSharedTabsTransparentMaterial(Color c, float emission = 0f)
    {
        Shader shader = Resources.Load<Shader>("Shaders/TabsTransparentUnlit");
        if (shader == null)
            return CreateSharedTransparentMaterial(c, emission);

        Material m = new Material(shader);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (m.HasProperty("_Color"))
            m.SetColor("_Color", c);
        if (m.HasProperty("_BaseColor"))
            m.SetColor("_BaseColor", c);
        m.color = c;

        if (m.HasProperty("_EmissionColor"))
            m.SetColor("_EmissionColor", emission > 0f ? c * Mathf.Pow(2f, emission) : Color.black);

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
        if (activeStringBasePitch == null || activeStringBasePitch.Length == 0)
            activeStringBasePitch = GetPreferredStandardTuningForTrack(GetResolvedActiveTrackSummary());

        if (stringIdx < 0 || stringIdx >= activeStringBasePitch.Length)
            return 0;

        return activeStringBasePitch[stringIdx];
    }

    public int ActiveStringCount
    {
        get
        {
            int tuningCount = activeStringBasePitch != null ? activeStringBasePitch.Length : 0;
            int chartCount = 0;
            if (chartNotes != null && chartNotes.Count > 0)
                chartCount = chartNotes.Max(note => note.stringIdx + 1);

            int resolved = Mathf.Max(tuningCount, chartCount);
            if (resolved <= 0)
                resolved = 6;

            int colorSlotCount = stringColors != null && stringColors.Length > 0 ? stringColors.Length : 6;
            return Mathf.Clamp(resolved, 1, colorSlotCount);
        }
    }

    public bool TryGetChartNoteById(int id, out NoteData data)
    {
        return chartNoteById.TryGetValue(id, out data);
    }

    public IReadOnlyList<ArcadeNoteState> ArcadeNoteStates => arcadeNoteStates;

    public int ArcadeLaneCount => currentArcadeChart != null ? Mathf.Max(5, currentArcadeChart.LaneCount) : 5;
    public int ArcadeHighwayLaneCount => Mathf.Clamp(Mathf.Max(arcadeHighwayLaneCount, ArcadeLaneCount), 1, 8);
    public float ArcadeSpawnZ => Mathf.Max(StrikeLineZ + 1f, arcadeNoteSpawnZ);
    public float ArcadeResolvedHoldTime => Mathf.Max(0f, arcadeResolvedHoldTime);
    public bool HasArcadeVisibleSustain(ArcadeNoteData note) => HasMeaningfulArcadeSustain(note.duration, note.sustainBeats);
    public bool IsArcadeSustainActivelyHeld(ArcadeNoteData note)
    {
        int chordId = GetArcadeConsumeChordId(note);
        return activeArcadeSustains.TryGetValue(chordId, out ArcadeActiveSustain sustain) &&
               sustain != null &&
               !sustain.broken &&
               songTimer < sustain.endTime - 0.0001f;
    }

    public bool IsMultiplayerRhythmSustainActivelyHeld(int playerIndex, ArcadeNoteData note)
    {
        if (multiplayerRhythmPlayers == null ||
            playerIndex < 0 ||
            playerIndex >= multiplayerRhythmPlayers.Length)
            return false;

        MultiplayerRhythmPlayerState player = multiplayerRhythmPlayers[playerIndex];
        if (player == null)
            return false;

        int chordId = GetArcadeConsumeChordId(note);
        return player.activeSustains.TryGetValue(chordId, out ArcadeActiveSustain sustain) &&
               sustain != null &&
               !sustain.broken &&
               songTimer < sustain.endTime - 0.0001f;
    }

    public SongLibraryType CurrentSongLibraryType => selectedSongLibraryType;

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
        HashSet<int> processedChordIds = new HashSet<int>();

        for (int i = 0; i < noteStates.Count; i++)
        {
            GameplayNoteState noteState = noteStates[i];

            if (noteState.IsResolved)
            {
                noteState.isJudgeable = false;
                continue;
            }

            if (noteState.data.requiresPluck && noteState.data.chordId >= 0)
            {
                if (!processedChordIds.Add(noteState.data.chordId))
                    continue;

                PopulateUnresolvedChordScratchStates(noteState.data.chordId);
                if (chordMatchScratchStates.Count == 0)
                    continue;

                float chordTime = noteState.data.time;
                float chordHighStringExtraLate = 0f;
                for (int chordIndex = 0; chordIndex < chordMatchScratchStates.Count; chordIndex++)
                {
                    GameplayNoteState chordState = chordMatchScratchStates[chordIndex];
                    chordState.isJudgeable = IsNoteJudgeableNow(chordState);
                    if (chordState.data.stringIdx >= 4)
                        chordHighStringExtraLate = Mathf.Max(chordHighStringExtraLate, highStringExtraLate);
                }

                if (songTimer < chordTime - hitWindowEarly)
                    continue;

                if (TryFindMatchingChordEvent(noteState, out NoteEvent matchedChordEvent, out float matchedChordEventTime))
                {
                    for (int keyIndex = 0; keyIndex < chordMatchScratchConsumeKeys.Count; keyIndex++)
                        matchedChordEvent.consumedKeys.Add(chordMatchScratchConsumeKeys[keyIndex]);

                    RecordNotesDetectorAcceptanceSource(BuildDetectorAcceptanceSourceLabel(matchedChordEvent?.source));

                    for (int chordIndex = 0; chordIndex < chordMatchScratchStates.Count; chordIndex++)
                    {
                        GameplayNoteState chordState = chordMatchScratchStates[chordIndex];
                        chordState.result = GameplayNoteResult.Hit;
                        chordState.resolvedAt = songTimer;
                        chordState.isJudgeable = false;
                    }

                    continue;
                }

                float chordLatestJudgeTime = chordTime + hitWindowLate + judgmentGrace;
                if (songTimer > chordLatestJudgeTime + chordHighStringExtraLate)
                {
                    for (int chordIndex = 0; chordIndex < chordMatchScratchStates.Count; chordIndex++)
                    {
                        GameplayNoteState chordState = chordMatchScratchStates[chordIndex];
                        chordState.result = GameplayNoteResult.Missed;
                        chordState.resolvedAt = songTimer;
                        chordState.isJudgeable = false;
                    }

                    LogMissReason(noteState);
                }

                continue;
            }

            noteState.isJudgeable = IsNoteJudgeableNow(noteState);

            if (songTimer < noteState.data.time - hitWindowEarly)
                continue;

            NoteEvent matchedEvent;
            int consumeKey;
            float matchedEventTime;

            bool matched = false;
            bool matchedViaHighStringRescue = false;
            string matchedSourceLabel = string.Empty;

            if (noteState.data.requiresPluck)
            {
                matched = TryFindMatchingNoteEvent(noteState, out matchedEvent, out consumeKey, out matchedEventTime);
                if (!matched)
                {
                    matched = TryFindHighStringSupportEvent(noteState, out matchedEvent, out consumeKey);
                    matchedViaHighStringRescue = matched;
                }

                if (matched)
                {
                    matchedEvent.consumedKeys.Add(consumeKey);
                    matchedSourceLabel = BuildDetectorAcceptanceSourceLabel(matchedEvent?.source, matchedViaHighStringRescue);
                }
            }
            else
            {
                matched = TryFindLegatoMatch(noteState, out matchedEventTime, out matchedSourceLabel);
                matchedEvent = null;
                consumeKey = -1;
            }

            if (matched)
            {
                RecordNotesDetectorAcceptanceSource(matchedSourceLabel);
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

    private void UpdateArcadeInputState()
    {
        if (isPaused)
        {
            SyncArcadeInputHeldStateWithoutEvents();
            arcadeInputNeedsUnpausedPrime = true;
            return;
        }

        if (arcadeInputNeedsUnpausedPrime)
        {
            SyncArcadeInputHeldStateWithoutEvents();
            arcadeInputNeedsUnpausedPrime = false;
            return;
        }

        bool midiStrum = UpdateArcadeMidiInputState();

        bool anyPreviousHeld = false;
        for (int lane = 0; lane < arcadeHeldLanes.Length; lane++)
        {
            previousArcadeHeldLanes[lane] = arcadeHeldLanes[lane];
            anyPreviousHeld |= previousArcadeHeldLanes[lane];
            arcadeHeldLanes[lane] = IsArcadeLaneHeld(lane) || arcadeMidiHeldLanes[lane];
        }

        bool[] pressedLanes = new bool[arcadeHeldLanes.Length];
        bool strum = GetArcadeStrumDown() || midiStrum;
        bool openButton = GetArcadeOpenButtonDown() || arcadeMidiOpenButtonPressed;
        bool tap = false;
        bool anyHeld = false;
        for (int lane = 0; lane < arcadeHeldLanes.Length; lane++)
        {
            anyHeld |= arcadeHeldLanes[lane];
            if (arcadeHeldLanes[lane] && !previousArcadeHeldLanes[lane])
            {
                pressedLanes[lane] = true;
                tap = true;
            }
        }

        bool release = anyPreviousHeld && !anyHeld;
        if (!strum && !tap && !release && !openButton)
            return;

        ArcadeInputEvent inputEvent = new ArcadeInputEvent
        {
            id = ++latestArcadeInputEventId,
            time = Mathf.Max(0f, songTimer),
            isStrum = strum,
            isTap = tap,
            isRelease = release,
            isOpenButton = openButton
        };
        for (int lane = 0; lane < arcadeHeldLanes.Length; lane++)
        {
            inputEvent.heldLanes[lane] = arcadeHeldLanes[lane];
            inputEvent.pressedLanes[lane] = pressedLanes[lane];
        }

        arcadeRecentInputEvents.Add(inputEvent);
    }

    private void UpdateMultiplayerRhythmInputState()
    {
        if (multiplayerRhythmPlayers == null)
            return;

        for (int i = 0; i < multiplayerRhythmPlayers.Length; i++)
        {
            MultiplayerRhythmPlayerState player = multiplayerRhythmPlayers[i];
            if (player == null || player.assignment == null)
                continue;

            UpdateMultiplayerRhythmInputState(player);
        }
    }

    private void UpdateMultiplayerRhythmInputState(MultiplayerRhythmPlayerState player)
    {
        if (player == null)
            return;

        if (isPaused)
        {
            SyncMultiplayerRhythmInputHeldStateWithoutEvents(player);
            player.inputNeedsUnpausedPrime = true;
            return;
        }

        if (player.inputNeedsUnpausedPrime)
        {
            SyncMultiplayerRhythmInputHeldStateWithoutEvents(player);
            player.inputNeedsUnpausedPrime = false;
            return;
        }

        bool anyPreviousHeld = false;
        for (int lane = 0; lane < player.heldLanes.Length; lane++)
        {
            player.previousHeldLanes[lane] = player.heldLanes[lane];
            anyPreviousHeld |= player.previousHeldLanes[lane];
            player.heldLanes[lane] = IsMultiplayerRhythmLaneHeld(player, lane);
        }

        bool[] pressedLanes = new bool[player.heldLanes.Length];
        bool strum = GetMultiplayerRhythmStrumDown(player);
        bool openButton = GetMultiplayerRhythmOpenButtonDown(player);
        bool tap = false;
        bool anyHeld = false;
        for (int lane = 0; lane < player.heldLanes.Length; lane++)
        {
            anyHeld |= player.heldLanes[lane];
            if (player.heldLanes[lane] && !player.previousHeldLanes[lane])
            {
                pressedLanes[lane] = true;
                tap = true;
            }
        }

        bool release = anyPreviousHeld && !anyHeld;
        if (!strum && !tap && !release && !openButton)
            return;

        ArcadeInputEvent inputEvent = new ArcadeInputEvent
        {
            id = ++player.latestInputEventId,
            time = Mathf.Max(0f, songTimer),
            isStrum = strum,
            isTap = tap,
            isRelease = release,
            isOpenButton = openButton
        };

        for (int lane = 0; lane < player.heldLanes.Length; lane++)
        {
            inputEvent.heldLanes[lane] = player.heldLanes[lane];
            inputEvent.pressedLanes[lane] = pressedLanes[lane];
        }

        player.recentInputEvents.Add(inputEvent);
    }

    private void SyncMultiplayerRhythmInputHeldStateWithoutEvents(MultiplayerRhythmPlayerState player)
    {
        if (player == null)
            return;

        for (int lane = 0; lane < player.heldLanes.Length; lane++)
        {
            bool held = IsMultiplayerRhythmLaneHeld(player, lane);
            player.heldLanes[lane] = held;
            player.previousHeldLanes[lane] = held;
        }
    }

    private bool IsMultiplayerRhythmLaneHeld(MultiplayerRhythmPlayerState player, int lane)
    {
        if (player == null || player.assignment == null)
            return false;

        if (player.assignment.kind == MultiplayerRhythmInputDeviceKind.Keyboard)
        {
            switch (lane)
            {
                case 0: return arcadeKeyboardGreen != KeyCode.None && Input.GetKey(arcadeKeyboardGreen);
                case 1: return arcadeKeyboardRed != KeyCode.None && Input.GetKey(arcadeKeyboardRed);
                case 2: return arcadeKeyboardYellow != KeyCode.None && Input.GetKey(arcadeKeyboardYellow);
                case 3: return arcadeKeyboardBlue != KeyCode.None && Input.GetKey(arcadeKeyboardBlue);
                case 4: return arcadeKeyboardOrange != KeyCode.None && Input.GetKey(arcadeKeyboardOrange);
                default: return false;
            }
        }

        switch (lane)
        {
            case 0: return IsSpecificArcadeControllerBindingHeld(arcadeControllerGreen, player.assignment.controllerSlot);
            case 1: return IsSpecificArcadeControllerBindingHeld(arcadeControllerRed, player.assignment.controllerSlot);
            case 2: return IsSpecificArcadeControllerBindingHeld(arcadeControllerYellow, player.assignment.controllerSlot);
            case 3: return IsSpecificArcadeControllerBindingHeld(arcadeControllerBlue, player.assignment.controllerSlot);
            case 4: return IsSpecificArcadeControllerBindingHeld(arcadeControllerOrange, player.assignment.controllerSlot);
            default: return false;
        }
    }

    private bool GetMultiplayerRhythmStrumDown(MultiplayerRhythmPlayerState player)
    {
        if (player == null || player.assignment == null)
            return false;

        if (player.assignment.kind == MultiplayerRhythmInputDeviceKind.Keyboard)
        {
            return (arcadeKeyboardStrumUp != KeyCode.None && Input.GetKeyDown(arcadeKeyboardStrumUp)) ||
                   (arcadeKeyboardStrumDown != KeyCode.None && Input.GetKeyDown(arcadeKeyboardStrumDown));
        }

        return IsSpecificArcadeControllerBindingDown(arcadeControllerStrumUp, player.assignment.controllerSlot) ||
               IsSpecificArcadeControllerBindingDown(arcadeControllerStrumDown, player.assignment.controllerSlot) ||
               GetSpecificControllerAxisStrumDown(player.assignment.controllerSlot);
    }

    private bool GetMultiplayerRhythmOpenButtonDown(MultiplayerRhythmPlayerState player)
    {
        if (player == null || player.assignment == null)
            return false;

        if (player.assignment.kind == MultiplayerRhythmInputDeviceKind.Keyboard)
            return arcadeKeyboardOpen != KeyCode.None && Input.GetKeyDown(arcadeKeyboardOpen);

        return IsSpecificArcadeControllerBindingDown(arcadeControllerOpen, player.assignment.controllerSlot);
    }

    private bool GetMultiplayerRhythmOpenButtonHeld(MultiplayerRhythmPlayerState player)
    {
        if (player == null || player.assignment == null)
            return false;

        if (player.assignment.kind == MultiplayerRhythmInputDeviceKind.Keyboard)
            return arcadeKeyboardOpen != KeyCode.None && Input.GetKey(arcadeKeyboardOpen);

        return IsSpecificArcadeControllerBindingHeld(arcadeControllerOpen, player.assignment.controllerSlot);
    }

    private bool IsSpecificArcadeControllerBindingHeld(KeyCode binding, int controllerSlot)
    {
        return QuerySpecificArcadeControllerBinding(binding, controllerSlot, keyDownOnly: false);
    }

    private bool IsSpecificArcadeControllerBindingDown(KeyCode binding, int controllerSlot)
    {
        return QuerySpecificArcadeControllerBinding(binding, controllerSlot, keyDownOnly: true);
    }

    private bool QuerySpecificArcadeControllerBinding(KeyCode binding, int controllerSlot, bool keyDownOnly)
    {
        if (binding == KeyCode.None || controllerSlot <= 0)
            return false;

        KeyCode normalized = NormalizeArcadeBinding(binding);
        if (!TryGetSpecificArcadeControllerBinding(normalized, controllerSlot, out KeyCode specificBinding))
            specificBinding = normalized;

        bool buttonPressed = keyDownOnly ? Input.GetKeyDown(specificBinding) : Input.GetKey(specificBinding);
        if (buttonPressed)
            return true;

        return TryGetSpecificArcadeControllerVirtualBindingState(normalized, controllerSlot, keyDownOnly);
    }

    private bool IsArcadeLaneHeld(int lane)
    {
        switch (lane)
        {
            case 0:
                return IsArcadeKeyboardBindingHeld(arcadeKeyboardGreen) || IsArcadeControllerBindingHeld(arcadeControllerGreen);
            case 1:
                return IsArcadeKeyboardBindingHeld(arcadeKeyboardRed) || IsArcadeControllerBindingHeld(arcadeControllerRed);
            case 2:
                return IsArcadeKeyboardBindingHeld(arcadeKeyboardYellow) || IsArcadeControllerBindingHeld(arcadeControllerYellow);
            case 3:
                return IsArcadeKeyboardBindingHeld(arcadeKeyboardBlue) || IsArcadeControllerBindingHeld(arcadeControllerBlue);
            case 4:
                return IsArcadeKeyboardBindingHeld(arcadeKeyboardOrange) || IsArcadeControllerBindingHeld(arcadeControllerOrange);
            default:
                return false;
        }
    }

    private bool GetArcadeStrumDown()
    {
        return IsArcadeKeyboardBindingDown(arcadeKeyboardStrumUp) ||
               IsArcadeKeyboardBindingDown(arcadeKeyboardStrumDown) ||
               IsArcadeControllerBindingDown(arcadeControllerStrumUp) ||
               IsArcadeControllerBindingDown(arcadeControllerStrumDown) ||
               GetArcadeControllerAxisStrumDown();
    }

    private void SyncArcadeInputHeldStateWithoutEvents()
    {
        bool midiStrum = UpdateArcadeMidiInputState();
        for (int lane = 0; lane < arcadeHeldLanes.Length; lane++)
        {
            bool held = IsArcadeLaneHeld(lane) || arcadeMidiHeldLanes[lane];
            arcadeHeldLanes[lane] = held;
            previousArcadeHeldLanes[lane] = held;
        }

        if (midiStrum)
            arcadeInputNeedsUnpausedPrime = true;
    }

    private bool GetArcadeControllerAxisStrumDown()
    {
        if (!UsesArcadeControllerInput())
            return false;

        if (arcadeControllerDeviceIndex > 0 && GetSpecificControllerAxisStrumDown(arcadeControllerDeviceIndex))
            return true;

        RefreshUiControllerAxes();
        return CrossedUiAxisThreshold(previousUiControllerVerticalAxis, currentUiControllerVerticalAxis, 1) ||
               CrossedUiAxisThreshold(previousUiControllerVerticalAxis, currentUiControllerVerticalAxis, -1);
    }

    private bool GetArcadeOpenButtonDown()
    {
        return IsArcadeKeyboardBindingDown(arcadeKeyboardOpen) ||
               IsArcadeControllerBindingDown(arcadeControllerOpen);
    }

    private bool IsArcadeKeyboardBindingHeld(KeyCode binding)
    {
        return UsesArcadeKeyboardInput() && binding != KeyCode.None && Input.GetKey(binding);
    }

    private bool IsArcadeKeyboardBindingDown(KeyCode binding)
    {
        return UsesArcadeKeyboardInput() && binding != KeyCode.None && Input.GetKeyDown(binding);
    }

    private bool IsArcadeControllerBindingHeld(KeyCode binding)
    {
        return UsesArcadeControllerInput() && QueryArcadeControllerBinding(binding, keyDownOnly: false);
    }

    private bool IsArcadeControllerBindingDown(KeyCode binding)
    {
        return UsesArcadeControllerInput() && QueryArcadeControllerBinding(binding, keyDownOnly: true);
    }

    private bool QueryArcadeControllerBinding(KeyCode binding, bool keyDownOnly)
    {
        if (binding == KeyCode.None)
            return false;

        KeyCode normalized = NormalizeArcadeBinding(binding);
        if (arcadeControllerDeviceIndex <= 0)
        {
            bool genericPressed = keyDownOnly ? Input.GetKeyDown(normalized) : Input.GetKey(normalized);
            if (genericPressed)
                return true;

            for (int slot = 1; slot <= ArcadeControllerSlotCount; slot++)
            {
                if (TryGetSpecificArcadeControllerVirtualBindingState(normalized, slot, keyDownOnly))
                    return true;
            }

            return false;
        }

        if (!TryGetSpecificArcadeControllerBinding(normalized, arcadeControllerDeviceIndex, out KeyCode specificBinding))
        {
            bool fallbackPressed = keyDownOnly ? Input.GetKeyDown(normalized) : Input.GetKey(normalized);
            if (fallbackPressed)
                return true;

            return TryGetSpecificArcadeControllerVirtualBindingState(normalized, arcadeControllerDeviceIndex, keyDownOnly);
        }

        bool specificPressed = keyDownOnly ? Input.GetKeyDown(specificBinding) : Input.GetKey(specificBinding);
        if (specificPressed)
            return true;

        return TryGetSpecificArcadeControllerVirtualBindingState(normalized, arcadeControllerDeviceIndex, keyDownOnly);
    }

    private static bool TryGetSpecificArcadeControllerBinding(KeyCode binding, int joystickIndex, out KeyCode specificBinding)
    {
        joystickIndex = Mathf.Clamp(joystickIndex, 1, ArcadeControllerSlotCount);
        Match genericBinding = Regex.Match(binding.ToString(), @"JoystickButton(\d+)$", RegexOptions.CultureInvariant);
        if (!genericBinding.Success)
        {
            specificBinding = binding;
            return false;
        }

        string candidate = $"Joystick{joystickIndex}Button{genericBinding.Groups[1].Value}";
        if (Enum.TryParse(candidate, true, out specificBinding))
            return true;

        specificBinding = binding;
        return false;
    }

    private bool TryGetSpecificArcadeControllerVirtualBindingState(KeyCode binding, int controllerSlot, bool keyDownOnly)
    {
        if (controllerSlot <= 0)
            return false;

        int slotIndex = controllerSlot - 1;
        if (slotIndex < 0 || slotIndex >= ArcadeControllerSlotCount)
            return false;

        RefreshSpecificControllerExtendedAxes();

        if (binding == KeyCode.JoystickButton6)
        {
            float previous = previousSpecificControllerLeftTriggerAxes[slotIndex];
            float current = currentSpecificControllerLeftTriggerAxes[slotIndex];
            return keyDownOnly
                ? previous < ControllerTriggerAxisPressThreshold && current >= ControllerTriggerAxisPressThreshold
                : current >= ControllerTriggerAxisPressThreshold;
        }

        if (binding == KeyCode.JoystickButton7)
        {
            float previous = previousSpecificControllerRightTriggerAxes[slotIndex];
            float current = currentSpecificControllerRightTriggerAxes[slotIndex];
            return keyDownOnly
                ? previous < ControllerTriggerAxisPressThreshold && current >= ControllerTriggerAxisPressThreshold
                : current >= ControllerTriggerAxisPressThreshold;
        }

        return false;
    }

    private bool GetSpecificControllerAxisStrumDown(int controllerSlot)
    {
        if (controllerSlot <= 0)
            return false;

        int slotIndex = controllerSlot - 1;
        if (slotIndex < 0 || slotIndex >= ArcadeControllerSlotCount)
            return false;

        RefreshSpecificControllerExtendedAxes();
        return CrossedUiAxisThreshold(previousSpecificControllerStrumAxes[slotIndex], currentSpecificControllerStrumAxes[slotIndex], 1) ||
               CrossedUiAxisThreshold(previousSpecificControllerStrumAxes[slotIndex], currentSpecificControllerStrumAxes[slotIndex], -1);
    }

    private void RefreshSpecificControllerExtendedAxes()
    {
        if (specificControllerExtendedAxesFrame == Time.frameCount)
            return;

        specificControllerExtendedAxesFrame = Time.frameCount;
        for (int slot = 1; slot <= ArcadeControllerSlotCount; slot++)
        {
            int slotIndex = slot - 1;
            previousSpecificControllerLeftTriggerAxes[slotIndex] = currentSpecificControllerLeftTriggerAxes[slotIndex];
            previousSpecificControllerRightTriggerAxes[slotIndex] = currentSpecificControllerRightTriggerAxes[slotIndex];
            previousSpecificControllerStrumAxes[slotIndex] = currentSpecificControllerStrumAxes[slotIndex];

            float leftTriggerAxis;
            float rightTriggerAxis;
            if (TryGetInputSystemGamepadForSlot(slot, out Gamepad inputSystemGamepad))
            {
                // Prefer the Input System trigger values for real gamepads. The legacy
                // Unity axes can expose the same trigger through multiple paths
                // (combined + dedicated), which can cause one trigger press to light
                // more than one lane.
                leftTriggerAxis = inputSystemGamepad.leftTrigger.ReadValue();
                rightTriggerAxis = inputSystemGamepad.rightTrigger.ReadValue();
            }
            else
            {
                float combinedTriggerAxis = ReadStrongestSpecificControllerAxis(slot, "RhythmJ{0}TriggerCombined3");
                leftTriggerAxis = Mathf.Abs(ReadStrongestSpecificControllerAxis(slot, "RhythmJ{0}TriggerLeft9"));
                rightTriggerAxis = Mathf.Abs(ReadStrongestSpecificControllerAxis(slot, "RhythmJ{0}TriggerRight10"));

                if (combinedTriggerAxis > 0f)
                    leftTriggerAxis = Mathf.Max(leftTriggerAxis, combinedTriggerAxis);
                else if (combinedTriggerAxis < 0f)
                    rightTriggerAxis = Mathf.Max(rightTriggerAxis, -combinedTriggerAxis);
            }

            currentSpecificControllerLeftTriggerAxes[slotIndex] = leftTriggerAxis;
            currentSpecificControllerRightTriggerAxes[slotIndex] = rightTriggerAxis;
            currentSpecificControllerStrumAxes[slotIndex] = ReadStrongestSpecificControllerAxis(slot, "RhythmJ{0}StrumVertical7");
        }
    }

    private static float ReadStrongestSpecificControllerAxis(int controllerSlot, params string[] axisNameFormats)
    {
        float strongest = 0f;
        if (axisNameFormats == null)
            return strongest;

        for (int i = 0; i < axisNameFormats.Length; i++)
        {
            string axisName = string.Format(CultureInfo.InvariantCulture, axisNameFormats[i], controllerSlot);
            float value = TryGetAxisRaw(axisName);
            if (Mathf.Abs(value) > Mathf.Abs(strongest))
                strongest = value;
        }

        return strongest;
    }

    private bool UsesArcadeKeyboardInput()
    {
        return arcadeInputSource == ArcadeInputSourceMode.Keyboard ||
               arcadeInputSource == ArcadeInputSourceMode.KeyboardAndController ||
               arcadeInputSource == ArcadeInputSourceMode.All;
    }

    private bool UsesArcadeControllerInput()
    {
        return arcadeInputSource == ArcadeInputSourceMode.Controller ||
               arcadeInputSource == ArcadeInputSourceMode.KeyboardAndController ||
               arcadeInputSource == ArcadeInputSourceMode.All;
    }

    private bool UsesArcadeMidiInput()
    {
        return arcadeInputSource == ArcadeInputSourceMode.Midi ||
               arcadeInputSource == ArcadeInputSourceMode.All;
    }

    private bool UpdateArcadeMidiInputState()
    {
        arcadeMidiOpenButtonPressed = false;
        for (int lane = 0; lane < arcadeMidiHeldLanes.Length; lane++)
            arcadeMidiHeldLanes[lane] = false;

        if (!UsesArcadeMidiInput())
        {
            StopArcadeMidiInput();
            return false;
        }

        EnsureArcadeMidiInput();
        if (arcadeMidiInputBridge == null || !arcadeMidiInputBridge.IsRunning)
            return false;

        HashSet<int> heldNotes = arcadeMidiInputBridge.GetHeldNotesSnapshot();
        foreach (int note in heldNotes)
        {
            if (TryMapArcadeMidiInputNote(note, out int lane, out bool isOpen) && !isOpen && lane >= 0 && lane < arcadeMidiHeldLanes.Length)
                arcadeMidiHeldLanes[lane] = true;
        }

        bool midiStrum = false;
        List<ArcadeMidiInputBridge.MidiInputEvent> events = arcadeMidiInputBridge.ConsumeEvents();
        for (int i = 0; i < events.Count; i++)
        {
            ArcadeMidiInputBridge.MidiInputEvent midiEvent = events[i];
            if (!midiEvent.noteOn)
                continue;

            if (TryMapArcadeMidiInputNote(midiEvent.note, out _, out bool isOpen))
            {
                if (isOpen)
                    arcadeMidiOpenButtonPressed = true;
                midiStrum = true;
            }
        }

        return midiStrum;
    }

    private void EnsureArcadeMidiInput()
    {
        if (arcadeMidiInputBridge == null)
            arcadeMidiInputBridge = new ArcadeMidiInputBridge();

        if (arcadeMidiInputBridge.IsRunning)
            return;

        if (Time.unscaledTime < nextArcadeMidiInputStartRealtime)
            return;

        nextArcadeMidiInputStartRealtime = Time.unscaledTime + 3f;
        if (arcadeMidiInputBridge.Start(arcadeMidiInputDeviceIndex))
        {
            arcadeMidiInputUnavailableLogged = false;
            return;
        }

        if (!arcadeMidiInputUnavailableLogged && !string.IsNullOrWhiteSpace(arcadeMidiInputBridge.LastError))
        {
            Debug.LogWarning($"[Arcade MIDI] {arcadeMidiInputBridge.LastError}");
            arcadeMidiInputUnavailableLogged = true;
        }
    }

    private void StopArcadeMidiInput()
    {
        if (arcadeMidiInputBridge != null && arcadeMidiInputBridge.IsRunning)
            arcadeMidiInputBridge.Stop();

        arcadeMidiOpenButtonPressed = false;
        for (int lane = 0; lane < arcadeMidiHeldLanes.Length; lane++)
            arcadeMidiHeldLanes[lane] = false;
    }

    public bool IsArcadeInputLaneHeld(int lane)
    {
        return lane >= 0 && lane < arcadeHeldLanes.Length && arcadeHeldLanes[lane];
    }

    private bool TryMapArcadeMidiInputNote(int midiNote, out int lane, out bool isOpen)
    {
        lane = -1;
        isOpen = false;

        if (midiNote == arcadeMidiOpenNote)
        {
            isOpen = true;
            return true;
        }

        if (midiNote == arcadeMidiLane0Note)
        {
            lane = 0;
            return true;
        }

        if (midiNote == arcadeMidiLane1Note)
        {
            lane = 1;
            return true;
        }

        if (midiNote == arcadeMidiLane2Note)
        {
            lane = 2;
            return true;
        }

        if (midiNote == arcadeMidiLane3Note)
        {
            lane = 3;
            return true;
        }

        if (midiNote == arcadeMidiLane4Note)
        {
            lane = 4;
            return true;
        }

        return TryMapCloneHeroMidiInputNote(midiNote, out lane, out isOpen);
    }

    private static bool TryMapCloneHeroMidiInputNote(int midiNote, out int lane, out bool isOpen)
    {
        int[] difficultyRows = { 60, 72, 84, 96 };
        for (int i = 0; i < difficultyRows.Length; i++)
        {
            int offset = midiNote - difficultyRows[i];
            if (offset >= 0 && offset <= 4)
            {
                lane = offset;
                isOpen = false;
                return true;
            }

            if (offset == 5)
            {
                lane = -1;
                isOpen = true;
                return true;
            }
        }

        lane = -1;
        isOpen = false;
        return false;
    }

    private void PruneArcadeInputHistory()
    {
        float cutoff = songTimer - 3.0f;
        arcadeRecentInputEvents.RemoveAll(e => e.time < cutoff);
    }

    private void UpdateArcadeGameplayStates()
    {
        if (arcadeNoteStates == null)
            return;

        UpdateActiveArcadeSustains();

        for (int i = 0; i < arcadeNoteStates.Count; i++)
        {
            ArcadeNoteState noteState = arcadeNoteStates[i];
            if (noteState == null)
                continue;

            if (noteState.IsResolved)
            {
                noteState.isJudgeable = false;
                continue;
            }

            noteState.isJudgeable = IsArcadeNoteInHitWindow(noteState, songTimer);
            if (songTimer < noteState.data.time - arcadeHitWindowEarly)
                continue;

            if (TryFindMatchingArcadeInput(noteState, out ArcadeInputEvent matchedInput))
            {
                ResolveArcadeChord(noteState.data, GameplayNoteResult.Hit, songTimer);
                matchedInput.consumedChordIds.Add(GetArcadeConsumeChordId(noteState.data));
                continue;
            }

            if (CanPassivelyHitArcadeSpecialNote(noteState))
            {
                ResolveArcadeChord(noteState.data, GameplayNoteResult.Hit, songTimer);
                continue;
            }

            if (songTimer > noteState.data.time + arcadeHitWindowLate)
            {
                ResolveArcadeChord(noteState.data, GameplayNoteResult.Missed, songTimer);
            }
        }

        ResolveArcadeOverstrums();
    }

    private void PruneMultiplayerRhythmInputHistory()
    {
        if (multiplayerRhythmPlayers == null)
            return;

        float cutoff = songTimer - 3.0f;
        for (int i = 0; i < multiplayerRhythmPlayers.Length; i++)
        {
            MultiplayerRhythmPlayerState player = multiplayerRhythmPlayers[i];
            player?.recentInputEvents.RemoveAll(e => e.time < cutoff);
        }
    }

    private void UpdateMultiplayerRhythmGameplayStates()
    {
        if (multiplayerRhythmPlayers == null)
            return;

        for (int i = 0; i < multiplayerRhythmPlayers.Length; i++)
        {
            MultiplayerRhythmPlayerState player = multiplayerRhythmPlayers[i];
            if (player == null || player.noteStates == null)
                continue;

            UpdateMultiplayerRhythmGameplayStates(player);
        }
    }

    private void UpdateMultiplayerRhythmGameplayStates(MultiplayerRhythmPlayerState player)
    {
        if (player == null || player.noteStates == null)
            return;

        UpdateMultiplayerRhythmActiveSustains(player);

        for (int i = 0; i < player.noteStates.Count; i++)
        {
            ArcadeNoteState noteState = player.noteStates[i];
            if (noteState == null)
                continue;

            if (noteState.IsResolved)
            {
                noteState.isJudgeable = false;
                continue;
            }

            noteState.isJudgeable = IsArcadeNoteInHitWindow(noteState, songTimer);
            if (songTimer < noteState.data.time - arcadeHitWindowEarly)
                continue;

            if (TryFindMatchingMultiplayerRhythmInput(player, noteState, out ArcadeInputEvent matchedInput))
            {
                ResolveMultiplayerRhythmChord(player, noteState.data, GameplayNoteResult.Hit, songTimer);
                matchedInput.consumedChordIds.Add(GetArcadeConsumeChordId(noteState.data));
                continue;
            }

            if (CanPassivelyHitMultiplayerRhythmSpecialNote(player, noteState))
            {
                ResolveMultiplayerRhythmChord(player, noteState.data, GameplayNoteResult.Hit, songTimer);
                continue;
            }

            if (songTimer > noteState.data.time + arcadeHitWindowLate)
                ResolveMultiplayerRhythmChord(player, noteState.data, GameplayNoteResult.Missed, songTimer);
        }

        ResolveMultiplayerRhythmOverstrums(player);
    }

    private bool CanPassivelyHitMultiplayerRhythmSpecialNote(MultiplayerRhythmPlayerState player, ArcadeNoteState noteState)
    {
        if (player == null || noteState == null || noteState.data.isOpen)
            return false;

        if (noteState.data.noteType != ArcadeNoteType.Hopo || !player.comboActive)
            return false;

        if (songTimer < noteState.data.time || songTimer > noteState.data.time + arcadeHitWindowLate)
            return false;

        if (IsSameArcadeChordShapeAsPrevious(player.noteStates, noteState.data))
            return false;

        return DoesArcadeHeldStateMatchNote(player.noteStates, noteState.data, player.heldLanes, allowAnchoring: true, openButton: false);
    }

    private bool TryFindMatchingMultiplayerRhythmInput(MultiplayerRhythmPlayerState player, ArcadeNoteState note, out ArcadeInputEvent matchedInput)
    {
        matchedInput = null;
        if (player == null || note == null)
            return false;

        float windowStart = note.data.time - arcadeHitWindowEarly;
        float windowEnd = note.data.time + arcadeHitWindowLate;
        float bestDistance = float.MaxValue;

        for (int i = player.recentInputEvents.Count - 1; i >= 0; i--)
        {
            ArcadeInputEvent inputEvent = player.recentInputEvents[i];
            if (inputEvent.time < windowStart)
                break;
            if (inputEvent.time > windowEnd)
                continue;

            int consumeChordId = GetArcadeConsumeChordId(note.data);
            if (inputEvent.consumedChordIds.Count > 0 && !inputEvent.consumedChordIds.Contains(consumeChordId))
                continue;

            if (!CanMultiplayerRhythmInputHitNote(player, note.data, inputEvent))
                continue;

            float distance = Mathf.Abs(inputEvent.time - note.data.time);
            if (distance >= bestDistance)
                continue;

            matchedInput = inputEvent;
            bestDistance = distance;
        }

        return matchedInput != null;
    }

    private bool CanMultiplayerRhythmInputHitNote(MultiplayerRhythmPlayerState player, ArcadeNoteData note, ArcadeInputEvent inputEvent)
    {
        if (player == null || inputEvent == null)
            return false;

        if (inputEvent.isStrum &&
            DoesArcadeHeldStateMatchNote(player.noteStates, note, inputEvent.heldLanes, allowAnchoring: note.noteType != ArcadeNoteType.Strum, openButton: inputEvent.isOpenButton))
        {
            return true;
        }

        if (arcadeGamepadMode && DoesMultiplayerRhythmGamepadModeInputMatchNote(player, note, inputEvent))
            return true;

        if (inputEvent.isOpenButton && note.isOpen)
            return true;

        if (note.noteType == ArcadeNoteType.Tap)
            return DoesMultiplayerRhythmTapInputMatchNote(player, note, inputEvent);

        if (note.noteType == ArcadeNoteType.Hopo && player.comboActive)
            return DoesMultiplayerRhythmTapInputMatchNote(player, note, inputEvent);

        return false;
    }

    private bool DoesMultiplayerRhythmGamepadModeInputMatchNote(MultiplayerRhythmPlayerState player, ArcadeNoteData note, ArcadeInputEvent inputEvent)
    {
        if (player == null)
            return false;

        if (note.isOpen)
        {
            if (note.noteType == ArcadeNoteType.Strum)
                return inputEvent.isOpenButton;

            if (!inputEvent.isRelease && !inputEvent.isOpenButton)
                return false;

            return DoesArcadeHeldStateMatchNote(player.noteStates, note, inputEvent.heldLanes, allowAnchoring: true, openButton: inputEvent.isOpenButton);
        }

        if (!inputEvent.isTap)
            return false;

        if (!AnyRequiredArcadeLanePressed(player.noteStates, note, inputEvent.pressedLanes))
            return false;

        return DoesArcadeHeldStateMatchNote(player.noteStates, note, inputEvent.heldLanes, allowAnchoring: true, openButton: false);
    }

    private bool DoesMultiplayerRhythmTapInputMatchNote(MultiplayerRhythmPlayerState player, ArcadeNoteData note, ArcadeInputEvent inputEvent)
    {
        if (player == null)
            return false;

        if (note.isOpen)
        {
            if (!inputEvent.isRelease && !inputEvent.isOpenButton)
                return false;

            return DoesArcadeHeldStateMatchNote(player.noteStates, note, inputEvent.heldLanes, allowAnchoring: true, openButton: inputEvent.isOpenButton);
        }

        if (!inputEvent.isTap)
            return false;

        if (!AnyRequiredArcadeLanePressed(player.noteStates, note, inputEvent.pressedLanes))
            return false;

        return DoesArcadeHeldStateMatchNote(player.noteStates, note, inputEvent.heldLanes, allowAnchoring: true, openButton: false);
    }

    private bool IsArcadeNoteInHitWindow(ArcadeNoteState noteState, float eventTime)
    {
        return noteState != null &&
               eventTime >= noteState.data.time - arcadeHitWindowEarly &&
               eventTime <= noteState.data.time + arcadeHitWindowLate;
    }

    private bool CanPassivelyHitArcadeSpecialNote(ArcadeNoteState noteState)
    {
        if (noteState == null || noteState.data.isOpen)
            return false;

        if (noteState.data.noteType != ArcadeNoteType.Hopo || !arcadeComboActive)
            return false;

        if (songTimer < noteState.data.time || songTimer > noteState.data.time + arcadeHitWindowLate)
            return false;

        if (IsSameArcadeChordShapeAsPrevious(noteState.data))
            return false;

        return DoesArcadeHeldStateMatchNote(noteState.data, arcadeHeldLanes, allowAnchoring: true, openButton: false);
    }

    private bool TryFindMatchingArcadeInput(ArcadeNoteState note, out ArcadeInputEvent matchedInput)
    {
        matchedInput = null;
        if (note == null)
            return false;

        float windowStart = note.data.time - arcadeHitWindowEarly;
        float windowEnd = note.data.time + arcadeHitWindowLate;
        float bestDistance = float.MaxValue;

        for (int i = arcadeRecentInputEvents.Count - 1; i >= 0; i--)
        {
            ArcadeInputEvent inputEvent = arcadeRecentInputEvents[i];
            if (inputEvent.time < windowStart)
                break;
            if (inputEvent.time > windowEnd)
                continue;

            int consumeChordId = GetArcadeConsumeChordId(note.data);
            if (inputEvent.consumedChordIds.Count > 0 && !inputEvent.consumedChordIds.Contains(consumeChordId))
                continue;

            if (!CanArcadeInputHitNote(note.data, inputEvent))
                continue;

            float distance = Mathf.Abs(inputEvent.time - note.data.time);
            if (distance >= bestDistance)
                continue;

            matchedInput = inputEvent;
            bestDistance = distance;
        }

        return matchedInput != null;
    }

    private bool CanArcadeInputHitNote(ArcadeNoteData note, ArcadeInputEvent inputEvent)
    {
        if (inputEvent == null)
            return false;

        if (inputEvent.isStrum &&
            DoesArcadeHeldStateMatchNote(note, inputEvent.heldLanes, allowAnchoring: note.noteType != ArcadeNoteType.Strum, openButton: inputEvent.isOpenButton))
        {
            return true;
        }

        if (arcadeGamepadMode && DoesArcadeGamepadModeInputMatchNote(note, inputEvent))
            return true;

        if (inputEvent.isOpenButton && note.isOpen)
            return true;

        if (note.noteType == ArcadeNoteType.Tap)
            return DoesArcadeTapInputMatchNote(note, inputEvent);

        if (note.noteType == ArcadeNoteType.Hopo && arcadeComboActive)
            return DoesArcadeTapInputMatchNote(note, inputEvent);

        return false;
    }

    private bool DoesArcadeGamepadModeInputMatchNote(ArcadeNoteData note, ArcadeInputEvent inputEvent)
    {
        if (note.isOpen)
        {
            if (note.noteType == ArcadeNoteType.Strum)
                return inputEvent.isOpenButton;

            if (!inputEvent.isRelease && !inputEvent.isOpenButton)
                return false;

            return DoesArcadeHeldStateMatchNote(note, inputEvent.heldLanes, allowAnchoring: true, openButton: inputEvent.isOpenButton);
        }

        if (!inputEvent.isTap)
            return false;

        if (!AnyRequiredArcadeLanePressed(note, inputEvent.pressedLanes))
            return false;

        return DoesArcadeHeldStateMatchNote(note, inputEvent.heldLanes, allowAnchoring: true, openButton: false);
    }

    private bool DoesArcadeTapInputMatchNote(ArcadeNoteData note, ArcadeInputEvent inputEvent)
    {
        if (note.isOpen)
        {
            if (!inputEvent.isRelease && !inputEvent.isOpenButton)
                return false;

            return DoesArcadeHeldStateMatchNote(note, inputEvent.heldLanes, allowAnchoring: true, openButton: inputEvent.isOpenButton);
        }

        if (!inputEvent.isTap)
            return false;

        if (!AnyRequiredArcadeLanePressed(note, inputEvent.pressedLanes))
            return false;

        return DoesArcadeHeldStateMatchNote(note, inputEvent.heldLanes, allowAnchoring: true, openButton: false);
    }

    private static int GetArcadeConsumeChordId(ArcadeNoteData note)
    {
        return note.chordId >= 0 ? note.chordId : note.id;
    }

    private static int CountArcadeChordGroups(IReadOnlyList<ArcadeNoteState> states)
    {
        if (states == null || states.Count == 0)
            return 0;

        HashSet<int> chordIds = new HashSet<int>();
        for (int i = 0; i < states.Count; i++)
        {
            ArcadeNoteState state = states[i];
            if (state == null)
                continue;

            chordIds.Add(GetArcadeConsumeChordId(state.data));
        }

        return chordIds.Count;
    }

    private bool AnyRequiredArcadeLanePressed(ArcadeNoteData note, bool[] pressedLanes)
    {
        if (pressedLanes == null || pressedLanes.Length < 5)
            return false;

        bool[] requiredLanes = new bool[5];
        if (!BuildArcadeRequiredLanes(note, requiredLanes, out _, out _))
            return false;

        for (int lane = 0; lane < 5; lane++)
        {
            if (requiredLanes[lane] && pressedLanes[lane])
                return true;
        }

        return false;
    }

    private bool AnyRequiredArcadeLanePressed(IReadOnlyList<ArcadeNoteState> sourceStates, ArcadeNoteData note, bool[] pressedLanes)
    {
        if (pressedLanes == null || pressedLanes.Length < 5)
            return false;

        bool[] requiredLanes = new bool[5];
        if (!BuildArcadeRequiredLanes(sourceStates, note, requiredLanes, out _, out _))
            return false;

        for (int lane = 0; lane < 5; lane++)
        {
            if (requiredLanes[lane] && pressedLanes[lane])
                return true;
        }

        return false;
    }

    private bool DoesArcadeHeldStateMatchNote(ArcadeNoteData note, bool[] heldLanes, bool allowAnchoring, bool openButton)
    {
        if (heldLanes == null || heldLanes.Length < 5)
            return false;

        if (note.isOpen)
        {
            if (openButton)
                return true;

            for (int lane = 0; lane < 5; lane++)
            {
                if (heldLanes[lane])
                    return false;
            }

            return true;
        }

        bool hasRequiredLane = false;
        bool[] requiredLanes = new bool[5];
        int highestRequiredLane = -1;
        int requiredCount = 0;
        hasRequiredLane = BuildArcadeRequiredLanes(note, requiredLanes, out requiredCount, out highestRequiredLane);

        if (!hasRequiredLane)
            return false;

        for (int lane = 0; lane < 5; lane++)
        {
            if (requiredLanes[lane] && !heldLanes[lane])
                return false;
        }

        if (!allowAnchoring && requiredCount > 1)
        {
            for (int lane = 0; lane < 5; lane++)
            {
                if (!requiredLanes[lane] && heldLanes[lane])
                    return false;
            }

            return true;
        }

        for (int lane = highestRequiredLane + 1; lane < 5; lane++)
        {
            if (heldLanes[lane])
                return false;
        }

        return true;
    }

    private bool DoesArcadeHeldStateMatchNote(IReadOnlyList<ArcadeNoteState> sourceStates, ArcadeNoteData note, bool[] heldLanes, bool allowAnchoring, bool openButton)
    {
        if (heldLanes == null || heldLanes.Length < 5)
            return false;

        if (note.isOpen)
        {
            if (openButton)
                return true;

            for (int lane = 0; lane < 5; lane++)
            {
                if (heldLanes[lane])
                    return false;
            }

            return true;
        }

        bool[] requiredLanes = new bool[5];
        if (!BuildArcadeRequiredLanes(sourceStates, note, requiredLanes, out int requiredCount, out int highestRequiredLane))
            return false;

        for (int lane = 0; lane < 5; lane++)
        {
            if (requiredLanes[lane] && !heldLanes[lane])
                return false;
        }

        if (!allowAnchoring && requiredCount > 1)
        {
            for (int lane = 0; lane < 5; lane++)
            {
                if (!requiredLanes[lane] && heldLanes[lane])
                    return false;
            }

            return true;
        }

        for (int lane = highestRequiredLane + 1; lane < 5; lane++)
        {
            if (heldLanes[lane])
                return false;
        }

        return true;
    }

    private bool BuildArcadeRequiredLanes(ArcadeNoteData note, bool[] requiredLanes, out int requiredCount, out int highestRequiredLane)
    {
        requiredCount = 0;
        highestRequiredLane = -1;
        if (requiredLanes == null || requiredLanes.Length < 5)
            return false;

        for (int lane = 0; lane < 5; lane++)
            requiredLanes[lane] = false;

        if (note.chordId >= 0 && arcadeNoteStates != null)
        {
            for (int i = 0; i < arcadeNoteStates.Count; i++)
            {
                ArcadeNoteState chordNoteState = arcadeNoteStates[i];
                if (chordNoteState == null || chordNoteState.data.chordId != note.chordId)
                    continue;

                ArcadeNoteData chordNote = chordNoteState.data;
                if (chordNote.lane < 0 || chordNote.lane >= 5)
                    continue;

                requiredLanes[chordNote.lane] = true;
            }
        }

        if (note.lane >= 0 && note.lane < 5)
            requiredLanes[note.lane] = true;

        for (int lane = 0; lane < 5; lane++)
        {
            if (!requiredLanes[lane])
                continue;

            requiredCount++;
            highestRequiredLane = Mathf.Max(highestRequiredLane, lane);
        }

        return requiredCount > 0;
    }

    private static bool BuildArcadeRequiredLanes(IReadOnlyList<ArcadeNoteState> sourceStates, ArcadeNoteData note, bool[] requiredLanes, out int requiredCount, out int highestRequiredLane)
    {
        requiredCount = 0;
        highestRequiredLane = -1;
        if (requiredLanes == null || requiredLanes.Length < 5)
            return false;

        for (int lane = 0; lane < 5; lane++)
            requiredLanes[lane] = false;

        if (note.chordId >= 0 && sourceStates != null)
        {
            for (int i = 0; i < sourceStates.Count; i++)
            {
                ArcadeNoteState chordNoteState = sourceStates[i];
                if (chordNoteState == null || chordNoteState.data.chordId != note.chordId)
                    continue;

                ArcadeNoteData chordNote = chordNoteState.data;
                if (chordNote.lane < 0 || chordNote.lane >= 5)
                    continue;

                requiredLanes[chordNote.lane] = true;
            }
        }

        if (note.lane >= 0 && note.lane < 5)
            requiredLanes[note.lane] = true;

        for (int lane = 0; lane < 5; lane++)
        {
            if (!requiredLanes[lane])
                continue;

            requiredCount++;
            highestRequiredLane = Mathf.Max(highestRequiredLane, lane);
        }

        return requiredCount > 0;
    }

    private bool IsSameArcadeChordShapeAsPrevious(ArcadeNoteData note)
    {
        if (arcadeNoteStates == null || arcadeNoteStates.Count == 0)
            return false;

        int currentChordId = GetArcadeConsumeChordId(note);
        ArcadeNoteData? previous = null;
        float previousTime = -1f;

        for (int i = 0; i < arcadeNoteStates.Count; i++)
        {
            ArcadeNoteState state = arcadeNoteStates[i];
            if (state == null || GetArcadeConsumeChordId(state.data) == currentChordId)
                continue;

            float candidateTime = state.data.time;
            if (candidateTime >= note.time - 0.0001f || candidateTime <= previousTime)
                continue;

            previous = state.data;
            previousTime = candidateTime;
        }

        if (!previous.HasValue)
            return false;

        return ArcadeChordShapesEqual(note, previous.Value);
    }

    private static bool IsSameArcadeChordShapeAsPrevious(IReadOnlyList<ArcadeNoteState> sourceStates, ArcadeNoteData note)
    {
        if (sourceStates == null || sourceStates.Count == 0)
            return false;

        int currentChordId = GetArcadeConsumeChordId(note);
        ArcadeNoteData? previous = null;
        float previousTime = -1f;

        for (int i = 0; i < sourceStates.Count; i++)
        {
            ArcadeNoteState state = sourceStates[i];
            if (state == null || GetArcadeConsumeChordId(state.data) == currentChordId)
                continue;

            float candidateTime = state.data.time;
            if (candidateTime >= note.time - 0.0001f || candidateTime <= previousTime)
                continue;

            previous = state.data;
            previousTime = candidateTime;
        }

        if (!previous.HasValue)
            return false;

        return ArcadeChordShapesEqual(sourceStates, note, previous.Value);
    }

    private bool ArcadeChordShapesEqual(ArcadeNoteData first, ArcadeNoteData second)
    {
        if (first.isOpen || second.isOpen)
            return first.isOpen == second.isOpen;

        bool[] firstRequired = new bool[5];
        bool[] secondRequired = new bool[5];
        if (!BuildArcadeRequiredLanes(first, firstRequired, out int firstCount, out _) ||
            !BuildArcadeRequiredLanes(second, secondRequired, out int secondCount, out _) ||
            firstCount != secondCount)
        {
            return false;
        }

        for (int lane = 0; lane < 5; lane++)
        {
            if (firstRequired[lane] != secondRequired[lane])
                return false;
        }

        return true;
    }

    private static bool ArcadeChordShapesEqual(IReadOnlyList<ArcadeNoteState> sourceStates, ArcadeNoteData first, ArcadeNoteData second)
    {
        if (first.isOpen || second.isOpen)
            return first.isOpen == second.isOpen;

        bool[] firstRequired = new bool[5];
        bool[] secondRequired = new bool[5];
        if (!BuildArcadeRequiredLanes(sourceStates, first, firstRequired, out int firstCount, out _) ||
            !BuildArcadeRequiredLanes(sourceStates, second, secondRequired, out int secondCount, out _) ||
            firstCount != secondCount)
        {
            return false;
        }

        for (int lane = 0; lane < 5; lane++)
        {
            if (firstRequired[lane] != secondRequired[lane])
                return false;
        }

        return true;
    }

    private void ResolveArcadeChord(ArcadeNoteData note, GameplayNoteResult result, float resolvedTime)
    {
        int consumeChordId = GetArcadeConsumeChordId(note);
        bool changed = false;
        for (int i = 0; i < arcadeNoteStates.Count; i++)
        {
            ArcadeNoteState state = arcadeNoteStates[i];
            if (state == null || state.IsResolved || GetArcadeConsumeChordId(state.data) != consumeChordId)
                continue;

            state.result = result;
            state.resolvedAt = resolvedTime;
            state.isJudgeable = false;
            changed = true;
        }

        if (changed)
            ApplyArcadeChordScoreAndState(note, result);
    }

    private void ResolveArcadeOverstrums()
    {
        for (int i = 0; i < arcadeRecentInputEvents.Count; i++)
        {
            ArcadeInputEvent inputEvent = arcadeRecentInputEvents[i];
            if (inputEvent == null || inputEvent.overstrumJudged)
                continue;

            bool overstrumCandidate =
                inputEvent.isStrum ||
                inputEvent.isOpenButton;
            if (!overstrumCandidate)
                continue;

            inputEvent.overstrumJudged = true;
            if (inputEvent.consumedChordIds.Count == 0)
                ResetArcadeCombo();
        }
    }

    private void ApplyArcadeChordScoreAndState(ArcadeNoteData note, GameplayNoteResult result)
    {
        int chordId = GetArcadeConsumeChordId(note);
        activeArcadeSustains.Remove(chordId);

        if (result != GameplayNoteResult.Hit)
        {
            ResetArcadeCombo();
            return;
        }

        int noteCount = GetArcadeChordLaneCount(note);
        currentSessionArcadeScoreValue += Mathf.Max(1, noteCount) * 50 * GetArcadeScoreMultiplier(arcadeComboCount);
        arcadeComboCount++;
        arcadeComboActive = true;
        TryStartArcadeSustain(note);
    }

    private void ResolveMultiplayerRhythmChord(MultiplayerRhythmPlayerState player, ArcadeNoteData note, GameplayNoteResult result, float resolvedTime)
    {
        if (player == null || player.noteStates == null)
            return;

        int consumeChordId = GetArcadeConsumeChordId(note);
        bool changed = false;
        for (int i = 0; i < player.noteStates.Count; i++)
        {
            ArcadeNoteState state = player.noteStates[i];
            if (state == null || state.IsResolved || GetArcadeConsumeChordId(state.data) != consumeChordId)
                continue;

            state.result = result;
            state.resolvedAt = resolvedTime;
            state.isJudgeable = false;
            changed = true;
        }

        if (changed)
            ApplyMultiplayerRhythmChordScoreAndState(player, note, result);
    }

    private void ResolveMultiplayerRhythmOverstrums(MultiplayerRhythmPlayerState player)
    {
        if (player == null)
            return;

        for (int i = 0; i < player.recentInputEvents.Count; i++)
        {
            ArcadeInputEvent inputEvent = player.recentInputEvents[i];
            if (inputEvent == null || inputEvent.overstrumJudged)
                continue;

            bool overstrumCandidate =
                inputEvent.isStrum ||
                inputEvent.isOpenButton;
            if (!overstrumCandidate)
                continue;

            inputEvent.overstrumJudged = true;
            if (inputEvent.consumedChordIds.Count == 0)
                ResetMultiplayerRhythmCombo(player);
        }
    }

    private void ApplyMultiplayerRhythmChordScoreAndState(MultiplayerRhythmPlayerState player, ArcadeNoteData note, GameplayNoteResult result)
    {
        if (player == null)
            return;

        int chordId = GetArcadeConsumeChordId(note);
        player.activeSustains.Remove(chordId);

        if (result != GameplayNoteResult.Hit)
        {
            ResetMultiplayerRhythmCombo(player);
            return;
        }

        int noteCount = GetArcadeChordLaneCount(player.noteStates, note);
        player.scoreValue += Mathf.Max(1, noteCount) * 50 * GetArcadeScoreMultiplier(player.comboCount);
        player.comboCount++;
        player.maxComboCount = Mathf.Max(player.maxComboCount, player.comboCount);
        player.comboActive = true;
        TryStartMultiplayerRhythmSustain(player, note);
    }

    private int GetAwardedArcadeSustainScore(int chordId)
    {
        return arcadeChordAwardedSustainScore.TryGetValue(chordId, out int score)
            ? Mathf.Max(0, score)
            : 0;
    }

    private void TryStartArcadeSustain(ArcadeNoteData note)
    {
        float sustainDuration = GetArcadeChordSustainDuration(note);
        float sustainBeats = GetArcadeChordSustainBeats(note);
        if (!HasMeaningfulArcadeSustain(sustainDuration, sustainBeats))
            return;

        int chordId = GetArcadeConsumeChordId(note);
        activeArcadeSustains[chordId] = new ArcadeActiveSustain
        {
            chordId = chordId,
            note = note,
            endTime = note.time + sustainDuration,
            durationSeconds = sustainDuration,
            basePointsPerSecond = sustainDuration > 0.0001f ? (25f * sustainBeats) / sustainDuration : 0f,
            lastProcessedTime = Mathf.Max(songTimer, note.time),
            pendingScoreRemainder = 0f,
            broken = false
        };
    }

    private int GetAwardedMultiplayerRhythmSustainScore(MultiplayerRhythmPlayerState player, int chordId)
    {
        if (player == null)
            return 0;

        return player.awardedSustainScore.TryGetValue(chordId, out int score)
            ? Mathf.Max(0, score)
            : 0;
    }

    private void TryStartMultiplayerRhythmSustain(MultiplayerRhythmPlayerState player, ArcadeNoteData note)
    {
        if (player == null)
            return;

        float sustainDuration = GetArcadeChordSustainDuration(player.noteStates, note);
        float sustainBeats = GetArcadeChordSustainBeats(player.noteStates, note);
        if (!HasMeaningfulArcadeSustain(sustainDuration, sustainBeats))
            return;

        int chordId = GetArcadeConsumeChordId(note);
        player.activeSustains[chordId] = new ArcadeActiveSustain
        {
            chordId = chordId,
            note = note,
            endTime = note.time + sustainDuration,
            durationSeconds = sustainDuration,
            basePointsPerSecond = sustainDuration > 0.0001f ? (25f * sustainBeats) / sustainDuration : 0f,
            lastProcessedTime = Mathf.Max(songTimer, note.time),
            pendingScoreRemainder = 0f,
            broken = false
        };
    }

    private void UpdateActiveArcadeSustains()
    {
        if (activeArcadeSustains.Count == 0)
            return;

        List<int> completed = null;
        foreach (KeyValuePair<int, ArcadeActiveSustain> pair in activeArcadeSustains)
        {
            ArcadeActiveSustain sustain = pair.Value;
            if (sustain == null)
            {
                completed ??= new List<int>();
                completed.Add(pair.Key);
                continue;
            }

            float clampedNow = Mathf.Min(songTimer, sustain.endTime);
            if (clampedNow <= sustain.lastProcessedTime + 0.0001f)
            {
                if (songTimer >= sustain.endTime - 0.0001f || sustain.broken)
                {
                    completed ??= new List<int>();
                    completed.Add(pair.Key);
                }

                continue;
            }

            if (sustain.broken || !DoesArcadeHeldStateMatchNote(sustain.note, arcadeHeldLanes, allowAnchoring: true, openButton: false))
            {
                sustain.broken = true;
                completed ??= new List<int>();
                completed.Add(pair.Key);
                continue;
            }

            float elapsed = clampedNow - sustain.lastProcessedTime;
            if (elapsed > 0.0001f && sustain.basePointsPerSecond > 0f)
            {
                float addedScore = (elapsed * sustain.basePointsPerSecond * GetArcadeScoreMultiplier(arcadeComboCount)) + sustain.pendingScoreRemainder;
                int wholePoints = Mathf.FloorToInt(addedScore + 0.0001f);
                if (wholePoints > 0)
                {
                    currentSessionArcadeScoreValue += wholePoints;
                    arcadeChordAwardedSustainScore[sustain.chordId] = GetAwardedArcadeSustainScore(sustain.chordId) + wholePoints;
                }
                sustain.pendingScoreRemainder = Mathf.Max(0f, addedScore - wholePoints);
            }

            sustain.lastProcessedTime = clampedNow;
            if (songTimer >= sustain.endTime - 0.0001f)
            {
                completed ??= new List<int>();
                completed.Add(pair.Key);
            }
        }

        if (completed == null)
            return;

        for (int i = 0; i < completed.Count; i++)
            activeArcadeSustains.Remove(completed[i]);
    }

    private void UpdateMultiplayerRhythmActiveSustains(MultiplayerRhythmPlayerState player)
    {
        if (player == null || player.activeSustains.Count == 0)
            return;

        List<int> completed = null;
        foreach (KeyValuePair<int, ArcadeActiveSustain> pair in player.activeSustains)
        {
            ArcadeActiveSustain sustain = pair.Value;
            if (sustain == null)
            {
                completed ??= new List<int>();
                completed.Add(pair.Key);
                continue;
            }

            float clampedNow = Mathf.Min(songTimer, sustain.endTime);
            if (clampedNow <= sustain.lastProcessedTime + 0.0001f)
            {
                if (songTimer >= sustain.endTime - 0.0001f || sustain.broken)
                {
                    completed ??= new List<int>();
                    completed.Add(pair.Key);
                }

                continue;
            }

            bool openButtonHeld = sustain.note.isOpen && GetMultiplayerRhythmOpenButtonHeld(player);
            if (sustain.broken || !DoesArcadeHeldStateMatchNote(player.noteStates, sustain.note, player.heldLanes, allowAnchoring: true, openButton: openButtonHeld))
            {
                sustain.broken = true;
                completed ??= new List<int>();
                completed.Add(pair.Key);
                continue;
            }

            float elapsed = clampedNow - sustain.lastProcessedTime;
            if (elapsed > 0.0001f && sustain.basePointsPerSecond > 0f)
            {
                float addedScore = (elapsed * sustain.basePointsPerSecond * GetArcadeScoreMultiplier(player.comboCount)) + sustain.pendingScoreRemainder;
                int wholePoints = Mathf.FloorToInt(addedScore + 0.0001f);
                if (wholePoints > 0)
                {
                    player.scoreValue += wholePoints;
                    player.awardedSustainScore[sustain.chordId] = GetAwardedMultiplayerRhythmSustainScore(player, sustain.chordId) + wholePoints;
                }

                sustain.pendingScoreRemainder = Mathf.Max(0f, addedScore - wholePoints);
            }

            sustain.lastProcessedTime = clampedNow;
            if (songTimer >= sustain.endTime - 0.0001f)
            {
                completed ??= new List<int>();
                completed.Add(pair.Key);
            }
        }

        if (completed == null)
            return;

        for (int i = 0; i < completed.Count; i++)
            player.activeSustains.Remove(completed[i]);
    }

    private int GetArcadeScoreMultiplier(int comboCount)
    {
        return Mathf.Clamp(1 + Mathf.Max(0, comboCount) / 10, 1, 4);
    }

    private int GetGuitarScoreMultiplier(int comboCount)
    {
        return GetArcadeScoreMultiplier(comboCount);
    }

    private static int GetGuitarScoreEventKey(NoteData note, int fallbackIndex)
    {
        if (note.chordId >= 0)
            return ~note.chordId;

        if (note.id >= 0)
            return note.id;

        return fallbackIndex;
    }

    private void ResetArcadeCombo()
    {
        arcadeComboCount = 0;
        arcadeComboActive = false;
    }

    private static void ResetMultiplayerRhythmCombo(MultiplayerRhythmPlayerState player)
    {
        if (player == null)
            return;

        player.comboCount = 0;
        player.comboActive = false;
    }

    private int GetArcadeChordLaneCount(ArcadeNoteData note)
    {
        bool[] requiredLanes = new bool[5];
        return BuildArcadeRequiredLanes(note, requiredLanes, out int requiredCount, out _) ? Mathf.Max(1, requiredCount) : 1;
    }

    private static int GetArcadeChordLaneCount(IReadOnlyList<ArcadeNoteState> sourceStates, ArcadeNoteData note)
    {
        bool[] requiredLanes = new bool[5];
        return BuildArcadeRequiredLanes(sourceStates, note, requiredLanes, out int requiredCount, out _)
            ? Mathf.Max(1, requiredCount)
            : 1;
    }

    private float GetArcadeChordSustainDuration(ArcadeNoteData note)
    {
        float best = Mathf.Max(0f, note.duration);
        int chordId = GetArcadeConsumeChordId(note);
        if (arcadeNoteStates == null)
            return best;

        for (int i = 0; i < arcadeNoteStates.Count; i++)
        {
            ArcadeNoteState state = arcadeNoteStates[i];
            if (state == null || GetArcadeConsumeChordId(state.data) != chordId)
                continue;

            best = Mathf.Max(best, Mathf.Max(0f, state.data.duration));
        }

        return best;
    }

    private static float GetArcadeChordSustainDuration(IReadOnlyList<ArcadeNoteState> sourceStates, ArcadeNoteData note)
    {
        float best = Mathf.Max(0f, note.duration);
        int chordId = GetArcadeConsumeChordId(note);
        if (sourceStates == null)
            return best;

        for (int i = 0; i < sourceStates.Count; i++)
        {
            ArcadeNoteState state = sourceStates[i];
            if (state == null || GetArcadeConsumeChordId(state.data) != chordId)
                continue;

            best = Mathf.Max(best, Mathf.Max(0f, state.data.duration));
        }

        return best;
    }

    private float GetArcadeChordSustainBeats(ArcadeNoteData note)
    {
        float best = Mathf.Max(0f, note.sustainBeats);
        int chordId = GetArcadeConsumeChordId(note);
        if (arcadeNoteStates == null)
            return best;

        for (int i = 0; i < arcadeNoteStates.Count; i++)
        {
            ArcadeNoteState state = arcadeNoteStates[i];
            if (state == null || GetArcadeConsumeChordId(state.data) != chordId)
                continue;

            best = Mathf.Max(best, Mathf.Max(0f, state.data.sustainBeats));
        }

        return best;
    }

    private static float GetArcadeChordSustainBeats(IReadOnlyList<ArcadeNoteState> sourceStates, ArcadeNoteData note)
    {
        float best = Mathf.Max(0f, note.sustainBeats);
        int chordId = GetArcadeConsumeChordId(note);
        if (sourceStates == null)
            return best;

        for (int i = 0; i < sourceStates.Count; i++)
        {
            ArcadeNoteState state = sourceStates[i];
            if (state == null || GetArcadeConsumeChordId(state.data) != chordId)
                continue;

            best = Mathf.Max(best, Mathf.Max(0f, state.data.sustainBeats));
        }

        return best;
    }

    private static bool HasMeaningfulArcadeSustain(float durationSeconds, float sustainBeats)
    {
        return durationSeconds >= ArcadeMinimumSustainDurationSeconds &&
               sustainBeats >= ArcadeMinimumSustainBeats;
    }

    private void PopulateUnresolvedChordScratchStates(int chordId)
    {
        chordMatchScratchStates.Clear();

        if (chordId < 0 || noteStates == null)
            return;

        for (int i = 0; i < noteStates.Count; i++)
        {
            GameplayNoteState chordState = noteStates[i];
            if (chordState == null || chordState.IsResolved || chordState.data.chordId != chordId)
                continue;

            chordMatchScratchStates.Add(chordState);
        }
    }

    private bool TryFindMatchingChordEvent(GameplayNoteState note, out NoteEvent matchedEvent, out float matchedEventTime)
    {
        matchedEvent = null;
        matchedEventTime = -999f;
        chordMatchScratchConsumeKeys.Clear();

        if (note == null || note.data.chordId < 0)
            return false;

        PopulateUnresolvedChordScratchStates(note.data.chordId);
        if (chordMatchScratchStates.Count == 0)
            return false;

        float extraEarly = 0f;
        float extraLate = 0f;
        for (int i = 0; i < chordMatchScratchStates.Count; i++)
        {
            GameplayNoteState chordState = chordMatchScratchStates[i];
            if (chordState.data.stringIdx >= 4)
            {
                extraEarly = Mathf.Max(extraEarly, highStringExtraEarly);
                extraLate = Mathf.Max(extraLate, highStringExtraLate);
            }
        }

        float windowStart = note.data.time - eventMatchEarly - eventTimeSlack - extraEarly;
        float windowEnd = note.data.time + eventMatchLate + eventTimeSlack + extraLate;
        float bestDistance = float.MaxValue;
        if (Application.isEditor && notesDetectorGameplayTestActive)
            LogChordMatchAttempt("SEARCH_START", note, null, chordMatchScratchStates, null, $"windowStart={windowStart.ToString("F3", CultureInfo.InvariantCulture)} windowEnd={windowEnd.ToString("F3", CultureInfo.InvariantCulture)} recentEvents={recentNoteEvents.Count}");

        List<int> bestConsumeKeys = chordMatchScratchConsumeKeys;
        List<int> candidateConsumeKeys = new List<int>();

        for (int i = recentNoteEvents.Count - 1; i >= 0; i--)
        {
            NoteEvent ev = recentNoteEvents[i];
            if (ev.time < windowStart)
                break;
            if (ev.time > windowEnd)
                continue;
            if (ev.pitches == null || ev.pitches.Count < chordMatchScratchStates.Count)
            {
                if (Application.isEditor && notesDetectorGameplayTestActive)
                    LogChordMatchAttempt("CANDIDATE_REJECT", note, ev, chordMatchScratchStates, null, "reason=insufficient-pitch-count");
                continue;
            }

            candidateConsumeKeys.Clear();
            bool validEvent = true;
            string invalidReason = string.Empty;

            for (int chordIndex = 0; chordIndex < chordMatchScratchStates.Count; chordIndex++)
            {
                GameplayNoteState chordState = chordMatchScratchStates[chordIndex];
                if (!TryGetAcceptedConsumeKeyExact(chordState, ev.pitches, out int acceptedConsumeKey))
                {
                    validEvent = false;
                    invalidReason = $"no-exact-match-for-{DescribeGameplayNoteStateForDetectorLog(chordState)}";
                    break;
                }

                if (ev.consumedKeys.Contains(acceptedConsumeKey) || candidateConsumeKeys.Contains(acceptedConsumeKey))
                {
                    validEvent = false;
                    invalidReason = $"consume-key-collision-{acceptedConsumeKey}";
                    break;
                }

                candidateConsumeKeys.Add(acceptedConsumeKey);
            }

            if (!validEvent)
            {
                if (Application.isEditor && notesDetectorGameplayTestActive)
                    LogChordMatchAttempt("CANDIDATE_REJECT", note, ev, chordMatchScratchStates, candidateConsumeKeys, $"reason={invalidReason}");
                continue;
            }

            float distance = Mathf.Abs(ev.time - note.data.time);
            if (distance >= bestDistance)
            {
                if (Application.isEditor && notesDetectorGameplayTestActive)
                    LogChordMatchAttempt("CANDIDATE_REJECT", note, ev, chordMatchScratchStates, candidateConsumeKeys, $"reason=farther distance={distance.ToString("F3", CultureInfo.InvariantCulture)} bestDistance={bestDistance.ToString("F3", CultureInfo.InvariantCulture)}");
                continue;
            }

            matchedEvent = ev;
            matchedEventTime = ev.time;
            bestDistance = distance;
            bestConsumeKeys.Clear();
            bestConsumeKeys.AddRange(candidateConsumeKeys);
            if (Application.isEditor && notesDetectorGameplayTestActive)
                LogChordMatchAttempt("CANDIDATE_ACCEPT", note, ev, chordMatchScratchStates, candidateConsumeKeys, $"distance={distance.ToString("F3", CultureInfo.InvariantCulture)}");
        }

        if (matchedEvent == null)
        {
            chordMatchScratchConsumeKeys.Clear();
            if (Application.isEditor && notesDetectorGameplayTestActive)
                LogChordMatchAttempt("SEARCH_END", note, null, chordMatchScratchStates, null, "result=no-match");
        }
        else if (Application.isEditor && notesDetectorGameplayTestActive)
        {
            LogChordMatchAttempt("SEARCH_END", note, matchedEvent, chordMatchScratchStates, chordMatchScratchConsumeKeys, "result=matched");
        }

        return matchedEvent != null;
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

    private bool TryFindLegatoMatch(GameplayNoteState note, out float matchedTime, out string matchedSourceLabel)
    {
        matchedTime = -999f;
        matchedSourceLabel = string.Empty;

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
                matchedSourceLabel = "Fast Continuous";
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
                matchedSourceLabel = BuildDetectorAcceptanceSourceLabel(ev.source);
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
        return GetStringBasePitch(note.data.stringIdx) + note.data.fret;
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

    private bool TryGetAcceptedConsumeKeyExact(GameplayNoteState note, HashSet<int> pitches, out int consumeKey)
    {
        return TryGetAcceptedConsumeKey(note, pitches, allowPitchClassFallback: false, out consumeKey);
    }

    private bool TryGetAcceptedConsumeKey(GameplayNoteState note, HashSet<int> pitches, out int consumeKey)
    {
        return TryGetAcceptedConsumeKey(note, pitches, allowPitchClassFallback: false, out consumeKey);
    }

    private bool TryGetAcceptedConsumeKey(GameplayNoteState note, HashSet<int> pitches, bool allowPitchClassFallback, out int consumeKey)
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

        if (!allowPitchClassFallback)
            return false;

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
        
        int exactTargetPitch = GetStringBasePitch(noteState.data.stringIdx) + noteState.data.fret;
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
        float realtimeNow = Time.realtimeSinceStartup;
        if (!detectorHintForceSend && (realtimeNow - lastDetectorHintSendRealtime) < detectorHintSendIntervalSeconds)
            return;

        string payload = BuildDetectorHintPayload(songTimer);
        if (string.IsNullOrEmpty(payload))
            return;

        if (Application.isEditor && notesDetectorGameplayTestActive)
            LogNotesDetectorEditor($"HINT_SEND songTimer={songTimer.ToString("F3", CultureInfo.InvariantCulture)} payload={payload}");

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

        if (!sendDetectorHintsToPython)
            return;

        EnsureDetectorHintClient();
        if (detectorHintClient == null || detectorHintEndpoint == null)
            return;

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
        if (TryBuildNotesDetectorTestHintPayload(currentSongTime, out string testPayload))
            return testPayload;

        var windows = new List<DetectorHintWindow>();
        BuildDetectorHintWindows(currentSongTime, windows);

        if (windows.Count == 0)
            return $"SYNC|{currentSongTime.ToString("F3", CultureInfo.InvariantCulture)}";

        if (notesDetectorBackendMode == NotesDetectorBackendMode.NativeEmbeddedBridge)
            return BuildNativeDetectorHintPayload(currentSongTime, windows);

        return BuildLegacyDetectorHintPayload(currentSongTime, windows);
    }

    private string BuildLegacyDetectorHintPayload(float currentSongTime, List<DetectorHintWindow> windows)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("HINT|");
        builder.Append(currentSongTime.ToString("F3", CultureInfo.InvariantCulture));

        for (int i = 0; i < windows.Count; i++)
        {
            DetectorHintWindow window = windows[i];
            string notesCsv = BuildDetectorHintMidiCsv(window.pitches);
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

    private string BuildNativeDetectorHintPayload(float currentSongTime, List<DetectorHintWindow> windows)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("HINT|");
        builder.Append(currentSongTime.ToString("F3", CultureInfo.InvariantCulture));

        for (int i = 0; i < windows.Count; i++)
        {
            DetectorHintWindow window = windows[i];
            string notesCsv = BuildDetectorHintMidiCsv(window.pitches);
            if (string.IsNullOrEmpty(notesCsv))
                continue;

            builder.Append('|');
            builder.Append(window.startTime.ToString("F3", CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(window.endTime.ToString("F3", CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(notesCsv);

            string expectedNotesCsv = BuildDetectorHintExpectedNotesCsv(window.expectedNotes);
            if (!string.IsNullOrEmpty(expectedNotesCsv))
            {
                builder.Append(':');
                builder.Append(expectedNotesCsv);
            }
        }

        return builder.ToString();
    }

    private string BuildDetectorHintMidiCsv(IEnumerable<int> midis)
    {
        if (midis == null)
            return string.Empty;

        bool useNumericMidi = notesDetectorBackendMode == NotesDetectorBackendMode.NativeEmbeddedBridge;
        return string.Join(",",
            midis
                .Where(midi => midi >= 0)
                .Distinct()
                .OrderBy(midi => midi)
                .Select(midi => useNumericMidi
                    ? midi.ToString(CultureInfo.InvariantCulture)
                    : GetNoteNameFromMidi(midi)));
    }

    private string BuildDetectorHintExpectedNotesCsv(IEnumerable<DetectorHintExpectedNote> expectedNotes)
    {
        if (expectedNotes == null)
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        bool first = true;
        foreach (DetectorHintExpectedNote expectedNote in expectedNotes)
        {
            if (expectedNote.midi < 0 || expectedNote.stringIndex < 0 || expectedNote.openMidi < 0)
                continue;

            if (!first)
                builder.Append(',');

            builder.Append(expectedNote.midi.ToString(CultureInfo.InvariantCulture));
            builder.Append('~');
            builder.Append(expectedNote.stringIndex.ToString(CultureInfo.InvariantCulture));
            builder.Append('~');
            builder.Append(expectedNote.fret.ToString(CultureInfo.InvariantCulture));
            builder.Append('~');
            builder.Append(expectedNote.openMidi.ToString(CultureInfo.InvariantCulture));
            builder.Append('~');
            builder.Append(expectedNote.flags.ToString(CultureInfo.InvariantCulture));
            first = false;
        }

        return builder.ToString();
    }

    private bool TryBuildNotesDetectorTestHintPayload(float currentSongTime, out string payload)
    {
        payload = null;

        if (!showNotesDetectorTestMenu)
            return false;

        float hintTimelineTime = GetNotesDetectorTestHintTimelineTime();

        if (!showNotesDetectorRoutinePopup)
        {
            payload = BuildDetectorTestSyncPayload(hintTimelineTime);
            return true;
        }

        if (notesDetectorRoutineStageIndex < 0 || notesDetectorRoutineStageIndex >= notesDetectorRoutineSteps.Count)
        {
            payload = BuildDetectorTestSyncPayload(hintTimelineTime);
            return true;
        }

        NotesDetectorRoutineStep currentStep = notesDetectorRoutineSteps[notesDetectorRoutineStageIndex];
        int[] expectedMidis = GetNotesDetectorRoutineExpectedMidis(currentStep);
        if (currentStep == null || currentStep.RequireSilence || expectedMidis.Length == 0)
        {
            payload = BuildDetectorTestSyncPayload(hintTimelineTime);
            return true;
        }

        string notesCsv = BuildDetectorHintMidiCsv(expectedMidis);
        if (string.IsNullOrWhiteSpace(notesCsv))
        {
            payload = BuildDetectorTestSyncPayload(hintTimelineTime);
            return true;
        }

        float hintStart = hintTimelineTime - NotesDetectorRoutineHintWindowSeconds;
        float hintEnd = hintTimelineTime + NotesDetectorRoutineHintWindowSeconds;
        payload =
            $"HINT|{hintTimelineTime.ToString("F3", CultureInfo.InvariantCulture)}|" +
            $"{hintStart.ToString("F3", CultureInfo.InvariantCulture)}:{hintEnd.ToString("F3", CultureInfo.InvariantCulture)}:{notesCsv}";
        return true;
    }

    private float GetNotesDetectorTestHintTimelineTime()
    {
        int stepBucket = showNotesDetectorRoutinePopup
            ? Mathf.Clamp(notesDetectorRoutineStageIndex + 1, 1, notesDetectorRoutineSteps.Count + 1)
            : 0;
        return NotesDetectorTestHintTimelineBaseSeconds + (stepBucket * NotesDetectorTestHintTimelineStepSeconds);
    }

    private static string BuildDetectorTestSyncPayload(float hintTimelineTime)
    {
        return $"SYNC|{hintTimelineTime.ToString("F3", CultureInfo.InvariantCulture)}";
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

            if (AreHintPitchSetsEqual(current.pitches, next.pitches) &&
                AreHintExpectedNotesEqual(current.expectedNotes, next.expectedNotes) &&
                next.startTime <= current.endTime + 0.001f)
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

    private bool AreHintExpectedNotesEqual(DetectorHintExpectedNote[] a, DetectorHintExpectedNote[] b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a == null || b == null || a.Length != b.Length)
            return false;

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].midi != b[i].midi ||
                a[i].stringIndex != b[i].stringIndex ||
                a[i].fret != b[i].fret ||
                a[i].openMidi != b[i].openMidi ||
                a[i].flags != b[i].flags)
            {
                return false;
            }
        }

        return true;
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
            int noteMidi = GetNoteMidiFromStringFret(note.stringIdx, note.fret);
            DetectorHintExpectedNote expectedNote = BuildDetectorHintExpectedNote(note, noteMidi, note.fret, null);
            AddDetectorHintWindow(noteStart - detectorHintLookbackSeconds, noteEnd, GetPitchSetForMidi(noteMidi), expectedNote, rangeStart, rangeEnd, output);
            return;
        }

        if (noteEnd > finalSegmentEnd + 0.001f)
        {
            int tailMidi = GetTailMidiForNote(note);
            float tailFret = tailMidi - GetStringBasePitch(note.stringIdx);
            DetectorHintExpectedNote expectedNote = BuildDetectorHintExpectedNote(note, tailMidi, tailFret, null);
            AddDetectorHintWindow(finalSegmentEnd, noteEnd, GetPitchSetForMidi(tailMidi), expectedNote, rangeStart, rangeEnd, output);
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
                float sustainFret = EvaluateTechniqueSegmentFret(segment, 1f);
                DetectorHintExpectedNote expectedNote = BuildDetectorHintExpectedNote(note, sustainMidi, sustainFret, segment);
                AddDetectorHintWindow(
                    includeOnsetLookback ? segmentStart - detectorHintLookbackSeconds : segmentStart,
                    segmentEnd,
                    GetPitchSetForMidi(sustainMidi),
                    expectedNote,
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

            float bucketFret = EvaluateTechniqueSegmentFret(segment, Mathf.Clamp01((bucketStart - segmentStart) / duration));
            DetectorHintExpectedNote expectedNote = BuildDetectorHintExpectedNote(note, bucketMidi, bucketFret, segment);
            AddDetectorHintWindow(sendStart, sampleTime, GetPitchSetForMidi(bucketMidi), expectedNote, rangeStart, rangeEnd, output);
            bucketStart = sampleTime;
            bucketMidi = sampleMidi;
        }
    }

    private float EvaluateTechniqueSegmentFret(NoteTechniqueSegmentData segment, float t)
    {
        return Mathf.Lerp(segment.startFret, segment.endFret, Mathf.Clamp01(t));
    }

    private float EvaluateTechniqueSegmentMidi(int stringIdx, NoteTechniqueSegmentData segment, float t)
    {
        float clampedT = Mathf.Clamp01(t);
        float fret = EvaluateTechniqueSegmentFret(segment, clampedT);
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

    private DetectorHintExpectedNote BuildDetectorHintExpectedNote(NoteData note, int midi, float fretValue, NoteTechniqueSegmentData? segment)
    {
        DetectorHintNoteFlags flags = DetectorHintNoteFlags.None;

        if (!note.requiresPluck || note.isLegato || note.technique == NoteTechnique.HammerOn || note.technique == NoteTechnique.PullOff)
            flags |= DetectorHintNoteFlags.Legato;

        bool bendLike = note.technique == NoteTechnique.Bend || note.bendStep > 0f || note.bendPreBend || note.bendRelease ||
                        (segment.HasValue && segment.Value.type == NoteTechniqueSegmentType.Bend);
        if (bendLike)
            flags |= DetectorHintNoteFlags.Bend;

        bool slideLike = note.technique == NoteTechnique.Slide || note.slideTargetFret >= 0 ||
                         (segment.HasValue && segment.Value.type == NoteTechniqueSegmentType.Slide);
        if (slideLike)
            flags |= DetectorHintNoteFlags.Slide;

        int openMidi = GetStringBasePitch(note.stringIdx);
        int fret = Mathf.Max(0, Mathf.RoundToInt(fretValue));
        return new DetectorHintExpectedNote(midi, note.stringIdx, fret, openMidi, flags);
    }

    private void AddDetectorHintWindow(float startTime, float endTime, HashSet<int> pitches, DetectorHintExpectedNote expectedNote, float rangeStart, float rangeEnd, List<DetectorHintWindow> output)
    {
        if (pitches == null || pitches.Count == 0)
            return;

        float clippedStart = Mathf.Max(startTime, rangeStart);
        float clippedEnd = Mathf.Min(endTime, rangeEnd);
        if (clippedEnd <= clippedStart + 0.0001f)
            return;

        output.Add(new DetectorHintWindow(clippedStart, clippedEnd, pitches, new[] { expectedNote }));
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

        if (string.IsNullOrEmpty(detectorPacket) || detectorPacket == "--")
            return;

        if (Application.isEditor && notesDetectorGameplayTestActive)
            LogNotesDetectorEditor($"PACKET_RAW {detectorPacket}");

        int eventId = 0;
        float eventAge = 0f;
        string eventCsv = "--";
        string eventSource = string.Empty;

        if (detectorPacket.StartsWith("A|"))
        {
            string[] parts = detectorPacket.Split('|');
            if (parts.Length < 5) return;

            ParseNoteCsvIntoSet(parts[1], latestDetectedPitches);

            int.TryParse(parts[2], out eventId);
            float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out eventAge);
            eventCsv = parts[4];
            if (parts.Length >= 6 && float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedLevel))
            {
                if (parsedLevel > 1f)
                    parsedLevel /= 100f;
                latestParsedInputLevel = Mathf.Clamp01(parsedLevel);
            }
            if (parts.Length >= 7)
                eventSource = parts[6];
            
            latestEventNotesText = string.IsNullOrWhiteSpace(eventCsv) ? "--" : eventCsv;

            if (eventId <= 0 || string.IsNullOrWhiteSpace(eventCsv) || eventCsv == "--") return;

            float estimatedEventTime = GetEstimatedNoteEventSongTime(eventAge);
            
            // Log exactly what timestamp Unity is assigning this event on the timeline
            if (TryStoreNoteEvent(eventId, estimatedEventTime, eventCsv, eventSource, out NoteEvent ev))
            {
                latestPacketHadEvent = true;
                latestEventNotesText = FormatMidiSetCsv(ev.pitches);
                latestNoteEventId = Mathf.Max(latestNoteEventId, eventId);
                if (Application.isEditor && notesDetectorGameplayTestActive)
                    LogNotesDetectorEditor($"EVENT_STORED id={eventId} eventAge={eventAge.ToString("F3", CultureInfo.InvariantCulture)} estimatedTime={estimatedEventTime.ToString("F3", CultureInfo.InvariantCulture)} source={NormalizeDetectorEventSourceLabel(ev.source)} pitches={latestEventNotesText}");
            }
        }
    }

    private bool TryStoreNoteEvent(int id, float timeStamp, string csv, string source, out NoteEvent storedEvent)
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
                existing.observedAtUnscaled = Time.unscaledTime;
                if (!string.IsNullOrWhiteSpace(source))
                {
                    string normalizedNew = NormalizeDetectorEventSourceLabel(source);
                    if (string.IsNullOrWhiteSpace(existing.source))
                    {
                        existing.source = normalizedNew;
                    }
                    else
                    {
                        string normalizedExisting = NormalizeDetectorEventSourceLabel(existing.source);
                        existing.source = string.Equals(normalizedExisting, normalizedNew, StringComparison.OrdinalIgnoreCase)
                            ? normalizedExisting
                            : $"{normalizedExisting} + {normalizedNew}";
                    }
                }
                storedEvent = existing;
                if (Application.isEditor && notesDetectorGameplayTestActive)
                    LogNotesDetectorEditor($"EVENT_MERGED id={id} time={timeStamp.ToString("F3", CultureInfo.InvariantCulture)} source={NormalizeDetectorEventSourceLabel(existing.source)} pitches={FormatMidiSetCsv(existing.pitches)}");
                return existing.pitches.Count > beforeCount;
            }
        }

        NoteEvent newEv = new NoteEvent
        {
            id = id,
            time = timeStamp,
            observedAtUnscaled = Time.unscaledTime,
            source = NormalizeDetectorEventSourceLabel(source),
            pitches = pitches
        };
        recentNoteEvents.Add(newEv);
        storedEvent = newEv;
        if (Application.isEditor && notesDetectorGameplayTestActive)
            LogNotesDetectorEditor($"EVENT_NEW id={id} time={timeStamp.ToString("F3", CultureInfo.InvariantCulture)} source={NormalizeDetectorEventSourceLabel(newEv.source)} pitches={FormatMidiSetCsv(pitches)}");
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
            float derived = Mathf.Clamp01(latestDetectedPitches.Count / Mathf.Max(1f, ActiveStringCount));

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

        if (gameplayMode == GuitarGameplayMode.Arcade && arcadeAudioSources != null)
        {
            for (int i = 0; i < arcadeAudioSources.Count; i++)
            {
                AudioSource source = arcadeAudioSources[i];
                if (source != null && source.clip != null)
                    duration = Mathf.Max(duration, source.clip.length);
            }
        }

        if (generatedSongPlayer != null && IsGeneratedPlaybackAvailable())
            duration = Mathf.Max(duration, generatedSongPlayer.ArrangementDurationSeconds);

        if (chartNotes != null && chartNotes.Count > 0)
            duration = Mathf.Max(duration, chartNotes.Max(note => note.time + Mathf.Max(0.05f, note.duration)));

        if (gameplayMode == GuitarGameplayMode.Arcade && arcadeNoteStates != null && arcadeNoteStates.Count > 0)
            duration = Mathf.Max(duration, arcadeNoteStates.Max(note => note.data.time + Mathf.Max(0.05f, note.data.duration)));

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

        if (notesDetectorGameplayTestActive &&
            !loopEnabled &&
            !showMainMenu &&
            !showSongSelection &&
            !showTrackSelection &&
            songTimer >= duration)
        {
            songTimer = duration;
            audioSongTimer = duration;
            ExitNotesDetectorGameplayTest(reopenDetectorMenu: true);
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
        List<bool> availableSongFavorited = new List<bool>(displayedSongLibraryEntries.Count);
        List<float> availableSongScores = new List<float>(displayedSongLibraryEntries.Count);
        List<string> availableSongScoreTexts = new List<string>(displayedSongLibraryEntries.Count);
        List<string> availableSongDurationLabels = new List<string>(displayedSongLibraryEntries.Count);
        List<string> availableSongDifficultyLabels = new List<string>(displayedSongLibraryEntries.Count);
        for (int i = 0; i < displayedSongLibraryEntries.Count; i++)
        {
            SongLibraryBrowseEntry entry = displayedSongLibraryEntries[i];
            if (entry != null && entry.IsSong && entry.Song != null)
            {
                SongLibraryEntry song = entry.Song;
                bool isCurrentSong = currentSongEntry != null &&
                                     string.Equals(currentSongEntry.SongDirectory, song.SongDirectory, StringComparison.OrdinalIgnoreCase);
                availableSongFavorited.Add(isCurrentSong
                    ? (songMetadata != null ? songMetadata.favoriteInLibrary : song.CachedFavoriteInLibrary)
                    : song.CachedFavoriteInLibrary);
                int arcadeBestScore = song.LibraryType == SongLibraryType.Arcade
                    ? (isCurrentSong && songMetadata != null ? currentSongBestArcadeScoreValue : song.CachedBestArcadeScoreValue)
                    : 0;
                int normalScore = song.LibraryType == SongLibraryType.Arcade
                    ? arcadeBestScore
                    : (isCurrentSong && songMetadata != null ? currentSongBestScoreValue : Mathf.Max(0, song.CachedBestScoreValue));
                HeroScoreSummary heroScore = song.LibraryType == SongLibraryType.Arcade
                    ? default
                    : GetStoredSongHeroScoreSummary(song);
                availableSongScores.Add(normalScore);
                availableSongScoreTexts.Add(song.LibraryType == SongLibraryType.Arcade
                    ? FormatArcadeMenuScoreText(arcadeBestScore)
                    : BuildCombinedScoreText(normalScore, heroScore));
                availableSongDurationLabels.Add(FormatSongLibraryDurationLabel(song.DurationSeconds));
                availableSongDifficultyLabels.Add(GetSongLibraryDifficultyDisplayLabel(song));
            }
            else
            {
                availableSongFavorited.Add(false);
                availableSongScores.Add(0f);
                availableSongScoreTexts.Add(entry?.ScoreText ?? "--");
                availableSongDurationLabels.Add(string.Empty);
                availableSongDifficultyLabels.Add(string.Empty);
            }
        }

        bool pendingSongIsArcade = pendingTrackSelectionSong != null && pendingTrackSelectionSong.LibraryType == SongLibraryType.Arcade;
        int pendingTrackDisplayCount = GetPendingTrackSelectionDisplayCount();
        List<string> availableTrackNames = new List<string>(pendingTrackDisplayCount);
        List<string> availableTrackMetaTexts = new List<string>(pendingTrackDisplayCount);
        List<float> availableTrackScores = new List<float>(pendingTrackDisplayCount);
        List<string> availableTrackScoreTexts = new List<string>(pendingTrackDisplayCount);
        if (pendingSongIsArcade)
        {
            for (int i = 0; i < pendingArcadeArrangementSummaries.Count; i++)
            {
                ArcadeArrangementSummary arrangement = pendingArcadeArrangementSummaries[i];
                availableTrackNames.Add(arrangement?.DisplayName ?? $"Arrangement {i + 1}");
                availableTrackMetaTexts.Add(string.Empty);
                bool supportsSelectedDifficulty = arrangement?.Difficulties != null &&
                                                  arrangement.Difficulties.Contains(selectedArcadeDifficulty);
                int score = supportsSelectedDifficulty
                    ? GetStoredArcadeScoreValue(pendingTrackMetadata, arrangement.ArrangementId, selectedArcadeDifficulty)
                    : 0;

                availableTrackScores.Add(score);
                availableTrackScoreTexts.Add(supportsSelectedDifficulty ? FormatArcadeMenuScoreText(score) : "--");
            }
        }
        else
        {
            for (int i = 0; i < pendingTrackDisplayCount; i++)
            {
                availableTrackNames.Add(GetPendingTrackSelectionDisplayName(i));
                availableTrackMetaTexts.Add(GetPendingTrackSelectionMetaText(i));
                MusicXmlLoader.MusicXmlPartSummary track = IsPendingRocksmithDifficultySelectionActive()
                    ? (i >= 0 && i < pendingRocksmithTrackSelectionGroups.Count
                        ? GetHighestDifficultyRocksmithVariant(pendingRocksmithTrackSelectionGroups[i].Variants)
                        : null)
                    : (i >= 0 && i < pendingTrackSelectionParts.Count ? pendingTrackSelectionParts[i] : null);
                int normalScore = track != null && pendingTrackMetadata != null ? GetStoredTrackScoreValue(pendingTrackMetadata, track.PartId) : 0;
                HeroScoreSummary heroScore = track != null && pendingTrackMetadata != null
                    ? GetStoredHeroTrackScoreSummary(pendingTrackMetadata, track.PartId)
                    : default;
                availableTrackScores.Add(normalScore);
                availableTrackScoreTexts.Add(track != null ? BuildCombinedScoreText(normalScore, heroScore) : "--");
            }
        }

        SongLibraryBrowseEntry selectedBrowseEntry = GetSelectedSongLibraryBrowseEntry();
        SongLibraryEntry selectedLibrarySongEntry = selectedBrowseEntry != null && selectedBrowseEntry.IsSong
            ? selectedBrowseEntry.Song
            : null;
        SongMetadata selectedLibrarySongMetadata =
            selectedLibrarySongEntry != null &&
            currentSongEntry != null &&
            string.Equals(selectedLibrarySongEntry.SongDirectory, currentSongEntry.SongDirectory, StringComparison.OrdinalIgnoreCase)
                ? songMetadata
                : LoadSongMetadataForEntry(selectedLibrarySongEntry);
        ArcadeArrangementSummary pendingArcadeArrangement = GetPendingSelectedArcadeArrangementSummary();
        HeroScoreSummary selectedLibraryHeroScore = default;
        if (selectedLibrarySongEntry != null && selectedLibrarySongEntry.LibraryType != SongLibraryType.Arcade)
        {
            MusicXmlLoader.MusicXmlPartSummary selectedTrackSummary = GetPendingSelectedTrackSummary();
            if (selectedTrackSummary != null && selectedLibrarySongMetadata != null)
            {
                selectedLibraryHeroScore = GetStoredHeroTrackScoreSummary(selectedLibrarySongMetadata, selectedTrackSummary.PartId);
            }
            else
            {
                selectedLibraryHeroScore = GetHighestHeroTrackScoreSummary(selectedLibrarySongMetadata);
            }
        }
        ArcadeHeroScoreSummary selectedLibraryArcadeHeroScore = selectedLibrarySongEntry != null && selectedLibrarySongEntry.LibraryType == SongLibraryType.Arcade && pendingArcadeArrangement != null
            ? GetStoredArcadeHeroScoreSummary(selectedLibrarySongMetadata, pendingArcadeArrangement.ArrangementId, selectedArcadeDifficulty)
            : default;
        string selectedLibraryHeroScoreText = selectedLibrarySongEntry == null
            ? "--"
            : selectedLibrarySongEntry.LibraryType == SongLibraryType.Arcade
                ? FormatLibraryHeroScoreText(selectedLibraryArcadeHeroScore)
                : FormatLibraryHeroScoreText(selectedLibraryHeroScore);
        List<string> arcadeDifficultyLabels = new List<string> { "X", "H", "M", "E" };
        List<bool> arcadeDifficultyAvailable = new List<bool>(4);
        for (int i = 0; i < 4; i++)
        {
            ArcadeDifficulty difficulty = DifficultyFromUiIndex(i);
            bool available = pendingArcadeArrangement != null &&
                             pendingArcadeArrangement.Difficulties != null &&
                             pendingArcadeArrangement.Difficulties.Contains(difficulty);
            arcadeDifficultyAvailable.Add(available);
        }

        List<string> libraryDifficultyLabels = new List<string> { "X", "H", "M", "E" };
        List<bool> libraryDifficultyAvailable = new List<bool>(4);
        bool showLibraryDifficultySelector = false;
        int selectedLibraryDifficultyIndex = 0;
        if (pendingSongIsArcade)
        {
            showLibraryDifficultySelector = pendingArcadeArrangement != null &&
                                            pendingArcadeArrangement.Difficulties != null &&
                                            pendingArcadeArrangement.Difficulties.Count > 0;
            selectedLibraryDifficultyIndex = DifficultyToUiIndex(selectedArcadeDifficulty);
            for (int i = 0; i < 4; i++)
                libraryDifficultyAvailable.Add(arcadeDifficultyAvailable[i]);
        }
        else
        {
            for (int i = 0; i < 4; i++)
                libraryDifficultyAvailable.Add(false);
        }

        List<MusicXmlLoader.MusicXmlPartSummary> currentRocksmithDifficultyVariants = GetCurrentRocksmithDifficultyVariants();
        List<string> rocksmithDifficultyOptionLabels = currentRocksmithDifficultyVariants
            .AsEnumerable()
            .Reverse()
            .Select(variant => string.IsNullOrWhiteSpace(variant?.DifficultyLabel) ? "Full" : variant.DifficultyLabel.Trim())
            .ToList();
        if (rocksmithDifficultyOptionLabels.Count == 0)
            rocksmithDifficultyOptionLabels.Add("Full");
        int selectedRocksmithDifficultyOptionIndex = currentRocksmithDifficultyVariants.Count > 0
            ? GetCurrentRocksmithDifficultyDisplayIndex()
            : 0;

        EnsureLoopBookmarkSelectionValid();
        List<LoopBookmarkEntry> currentLoopBookmarks = GetSortedLoopBookmarksForCurrentScope(songMetadata);
        int currentSelectedLoopBookmarkIndex = currentLoopBookmarks.FindIndex(bookmark =>
            bookmark != null && string.Equals(bookmark.bookmarkId ?? string.Empty, selectedLoopBookmarkId ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        List<string> currentLoopBookmarkNames = currentLoopBookmarks
            .Select(bookmark => string.IsNullOrWhiteSpace(bookmark?.name) ? "bookmark" : bookmark.name)
            .ToList();
        List<string> currentLoopBookmarkDetails = currentLoopBookmarks
            .Select(bookmark => bookmark == null
                ? string.Empty
                : FormattableString.Invariant($"{bookmark.loopStartTime:F2}s - {bookmark.loopEndTime:F2}s"))
            .ToList();
        List<string> currentRhythmPracticeSectionNames = currentArcadePracticeSections
            .Select(section => string.IsNullOrWhiteSpace(section?.name) ? "Section" : section.name)
            .ToList();
        List<string> currentRhythmPracticeSectionDetails = currentArcadePracticeSections
            .Select((section, index) => BuildRhythmPracticeSectionDetailText(section, index))
            .ToList();

        List<string> multiplayerDeviceLabels = multiplayerRhythmAvailableDevices
            .Select(device => device?.displayName ?? string.Empty)
            .ToList();
        List<MultiplayerRhythmPlayerSnapshot> multiplayerPlayerSnapshots = new List<MultiplayerRhythmPlayerSnapshot>(MultiplayerRhythmPlayerCount);
        if (multiplayerRhythmPlayers != null)
        {
            for (int i = 0; i < multiplayerRhythmPlayers.Length; i++)
            {
                MultiplayerRhythmPlayerState player = multiplayerRhythmPlayers[i];
                if (player == null)
                    continue;

                multiplayerPlayerSnapshots.Add(new MultiplayerRhythmPlayerSnapshot
                {
                    playerIndex = i,
                    playerLabel = $"Player {i + 1}",
                    deviceLabel = player.assignment?.displayName ?? "--",
                    arcadeNoteStates = player.noteStates,
                    heldLanes = (bool[])player.heldLanes.Clone(),
                    scoreValue = Mathf.Max(0, player.scoreValue),
                    scorePercent = Mathf.Clamp(player.scorePercent, 0f, 100f),
                    comboCount = Mathf.Max(0, player.comboCount),
                    multiplier = GetArcadeScoreMultiplier(player.comboCount),
                    hitCount = Mathf.Max(0, player.hitCount),
                    missCount = Mathf.Max(0, player.missCount),
                    maxCombo = Mathf.Max(player.maxComboCount, player.comboCount),
                    winner = multiplayerRhythmWinningPlayerIndex == i
                });
            }
        }

        bool multiplayerRhythmUiMode = multiplayerRhythmModeActive || pendingMultiplayerRhythmSongSelection || showMultiplayerRhythmSetup;

        return new GuitarGameplaySnapshot
        {
            gameplayMode = gameplayMode,
            multiplayerRhythmMode = multiplayerRhythmUiMode,
            showMultiplayerRhythmSetup = showMultiplayerRhythmSetup,
            multiplayerRhythmAvailableInputDevices = multiplayerDeviceLabels,
            selectedMultiplayerRhythmSetupIndex = selectedMultiplayerRhythmSetupIndex,
            selectedMultiplayerRhythmPlayerOneDeviceIndex = selectedMultiplayerRhythmPlayerOneDeviceIndex,
            selectedMultiplayerRhythmPlayerTwoDeviceIndex = selectedMultiplayerRhythmPlayerTwoDeviceIndex,
            multiplayerRhythmPlayerOneSetupLabel = GetSelectedMultiplayerRhythmSetupLabel(0),
            multiplayerRhythmPlayerTwoSetupLabel = GetSelectedMultiplayerRhythmSetupLabel(1),
            multiplayerRhythmSetupCapturePlayerIndex = activeMultiplayerRhythmSetupCapturePlayerIndex,
            multiplayerRhythmSetupCanContinue = CanStartMultiplayerRhythm(),
            multiplayerRhythmSetupStatusText = GetMultiplayerRhythmSetupStatusText(),
            multiplayerRhythmPlayers = multiplayerPlayerSnapshots,
            multiplayerRhythmWinningPlayerIndex = multiplayerRhythmWinningPlayerIndex,
            multiplayerRhythmDraw = multiplayerRhythmModeActive && multiplayerRhythmWinningPlayerIndex < 0,
            songLibraryType = selectedSongLibraryType,
            selectedSongLibraryTypeIndex = (int)selectedSongLibraryType,
            songTime = songTimer,
            isPaused = isPaused,
            noteByNoteModeEnabled = noteByNoteModeEnabled,
            noteByNoteWaitingForMatch = noteByNoteWaitingForMatch,
            heroModeEnabled = multiplayerRhythmUiMode ? false : heroModeEnabled,
            heroModeHeartCount = heroModeHeartCount,
            currentHeroHeartsRemaining = GetCurrentHeroHeartsRemaining(),
            showHighwayCharacter = !notesDetectorGameplayTestActive && (multiplayerRhythmModeActive || ShouldDisplayHighwayCharacter(heroModeEnabled)),
            forceStandardTuning = forceStandardTuning,
            selectedPauseActionIndex = selectedPauseActionIndex,
            selectedGameModesIndex = selectedGameModesIndex,
            selectedHeroModeSettingsIndex = selectedHeroModeSettingsIndex,
            selectedSongEndActionIndex = selectedSongEndActionIndex,
            selectedSongSettingsIndex = selectedSongSettingsIndex,
            loopEnabled = loopEnabled,
            loopStartTime = loopStartTime,
            loopEndTime = loopEndTime,
            loopStartConfigured = loopStartConfigured,
            loopEndConfigured = loopEndConfigured,
            selectedLoopMarker = selectedLoopMarker,
            showLoopSettings = showLoopSettings,
            showGameModes = multiplayerRhythmUiMode ? false : showGameModes,
            showHeroModeSettings = showHeroModeSettings,
            loopPreviewPlaying = loopSettingsPreviewPlaying,
            showLoopPausePopup = showLoopPausePopup,
            selectedLoopPausePopupIndex = selectedLoopPausePopupIndex,
            loopPauseDurationSeconds = GetActiveLoopPauseDurationSeconds(),
            loopRestartPauseRemainingSeconds = loopRestartPauseRemainingSeconds,
            showRocksmithDifficultyPopup = showRocksmithDifficultyPopup,
            rocksmithDifficultyModeAvailable = IsCurrentRocksmithDifficultyModeAvailable(),
            rocksmithDifficultyOptionLabels = rocksmithDifficultyOptionLabels,
            selectedRocksmithDifficultyOptionIndex = selectedRocksmithDifficultyOptionIndex,
            loopBookmarkNames = currentLoopBookmarkNames,
            loopBookmarkDetails = currentLoopBookmarkDetails,
            selectedLoopBookmarkIndex = currentSelectedLoopBookmarkIndex,
            selectedLoopBookmarkModified = IsSelectedLoopBookmarkModified(),
            loopBookmarkRenameActive = loopBookmarkRenameActive,
            loopBookmarkRenameDraft = loopBookmarkRenameDraft ?? string.Empty,
            loopBookmarkTrackLabel = GetCurrentLoopBookmarkScopeDisplayName(),
            rhythmPracticeSectionNames = currentRhythmPracticeSectionNames,
            rhythmPracticeSectionDetails = currentRhythmPracticeSectionDetails,
            selectedRhythmPracticeSectionIndex = selectedArcadePracticeSectionIndex,
            rhythmPracticeLoopStartSectionIndex = arcadePracticeLoopStartSectionIndex,
            rhythmPracticeLoopEndSectionIndex = arcadePracticeLoopEndSectionIndex,
            playbackSpeedPercent = playbackSpeedPercent,
            scoreSaveInvalidated = scoreSaveInvalidated,
            currentSectionIndex = currentSectionIndex,
            nextSectionIndex = currentSectionIndex + 1,
            currentSectionProgress = progress,
            sectionDuration = GetEffectiveTabSectionDuration(),
            currentSessionScoreHits = sessionScoreHits,
            currentSessionScoreMisses = sessionScoreMisses,
            currentSessionScorePercent = currentSessionScorePercent,
            currentSessionScoreValue = currentSessionScoreValue,
            currentSessionScoreCombo = guitarComboCount,
            currentSessionScoreMultiplier = GetGuitarScoreMultiplier(guitarComboCount),
            currentSessionArcadeScore = currentSessionArcadeScoreValue,
            currentSessionArcadeCombo = arcadeComboCount,
            currentSessionArcadeMultiplier = GetArcadeScoreMultiplier(arcadeComboCount),
            noteStates = noteStates,
            arpeggioGuides = currentArpeggioGuides,
            arcadeNoteStates = arcadeNoteStates,
            arcadeLaneCount = ArcadeLaneCount,
            selectedArcadeArrangementId = selectedArcadeArrangementId,
            selectedArcadeArrangementDisplayName = GetSelectedArcadeArrangementDisplayName(),
            selectedArcadeDifficultyLabel = ArcadeCloneHeroLoader.GetDifficultyLabel(selectedArcadeDifficulty),
            arcadeDifficultyLabels = arcadeDifficultyLabels,
            arcadeDifficultyAvailable = arcadeDifficultyAvailable,
            selectedArcadeDifficultyIndex = DifficultyToUiIndex(selectedArcadeDifficulty),
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
            showStartMenu = showStartMenu,
            showLibraryLoadingOverlay = showLibraryLoadingOverlay,
            selectedStartMenuStepIndex = (int)startMenuStep,
            selectedStartMenuModeIndex = selectedStartMenuModeIndex,
            selectedStartMenuArcadeSetupIndex = selectedStartMenuArcadeSetupIndex,
            selectedStartMenuArcadeInputIndex = selectedStartMenuArcadeInputIndex,
            startMenuArcadeGamepadMode = startMenuArcadeGamepadMode,
            showSongSelection = showSongSelection,
            songSelectionSongConfirmed = songSelectionSongConfirmed,
            showTrackSelection = showTrackSelection,
            showToneLab = showToneLab,
            showNotesDetectorTestMenu = showNotesDetectorTestMenu,
            notesDetectorGameplayTestActive = notesDetectorGameplayTestActive,
            showNotesDetectorTestSelectionPopup = showNotesDetectorTestSelectionPopup,
            showGlobalSettings = showGlobalSettings,
            selectedGlobalSettingsTopIndex = selectedGlobalSettingsTopIndex,
            selectedGlobalSettingsItemIndex = selectedGlobalSettingsItemIndex,
            activeGlobalSettingsCategory = activeGlobalSettingsCategory,
            globalSettingsTransparentBackground = globalSettingsTransparentBackground,
            showGameplayHudPreviewInMenus = gameplayHudPreviewInMenus,
            selectedNotesDetectorTestIndex = selectedNotesDetectorTestIndex,
            selectedNotesDetectorCatalogIndex = selectedNotesDetectorCatalogIndex,
            songLibraryListTitle = GetSongLibraryListTitle(),
            songLibraryListStatusText = GetSongLibraryStatusText(),
            songLibraryBrowseModeIndex = (int)songLibraryBrowseMode,
            availableSongNames = displayedSongLibraryEntries.Select(entry => entry?.DisplayName ?? string.Empty).ToList(),
            availableSongSubtitles = displayedSongLibraryEntries.Select(entry => entry?.Subtitle ?? string.Empty).ToList(),
            availableSongAlbums = displayedSongLibraryEntries.Select(entry => entry != null && entry.IsSong ? entry.Song?.Album ?? string.Empty : string.Empty).ToList(),
            availableSongArtworkPaths = displayedSongLibraryEntries.Select(entry => entry?.ArtworkPath ?? string.Empty).ToList(),
            availableSongDurationLabels = availableSongDurationLabels,
            availableSongDifficultyLabels = availableSongDifficultyLabels,
            availableSongFavorited = availableSongFavorited,
            availableSongScores = availableSongScores,
            availableSongScoreTexts = availableSongScoreTexts,
            selectedSongIndex = selectedSongListIndex,
            selectedLibrarySongSubtitle = selectedBrowseEntry?.Subtitle ?? string.Empty,
            selectedLibrarySongAlbum = selectedLibrarySongEntry?.Album ?? string.Empty,
            selectedLibrarySongArtworkPath = selectedBrowseEntry?.ArtworkPath ?? string.Empty,
            selectedLibrarySongDifficultyLabel = selectedBrowseEntry?.DifficultyLabel ?? string.Empty,
            selectedLibrarySongAudioLabel = BuildSongLibraryAudioSummary(selectedLibrarySongEntry),
            selectedLibrarySongTuningLabel = pendingSongIsArcade ? string.Empty : GetPendingTrackTuningLabel(),
            selectedLibrarySongTrackCount = selectedLibrarySongEntry != null ? pendingTrackDisplayCount : 0,
            selectedLibraryHeroScoreText = selectedLibraryHeroScoreText,
            selectedLibrarySongHeroBestHeartsRemaining = selectedLibrarySongEntry != null && selectedLibrarySongEntry.LibraryType == SongLibraryType.Arcade
                ? Mathf.Max(0, selectedLibraryArcadeHeroScore.heartsRemaining)
                : Mathf.Max(0, selectedLibraryHeroScore.heartsRemaining),
            selectedLibrarySongHeroBestHeartsTotal = selectedLibrarySongEntry != null && selectedLibrarySongEntry.LibraryType == SongLibraryType.Arcade
                ? Mathf.Max(0, selectedLibraryArcadeHeroScore.heartsTotal)
                : Mathf.Max(0, selectedLibraryHeroScore.heartsTotal),
            selectedLibrarySongHasMp3 = selectedLibrarySongEntry != null && !string.IsNullOrWhiteSpace(selectedLibrarySongEntry.Mp3Path),
            selectedLibrarySongHasMidi = selectedLibrarySongEntry != null && !string.IsNullOrWhiteSpace(selectedLibrarySongEntry.MidiPath),
            selectedLibrarySongIsCurrent = selectedLibrarySongEntry != null &&
                                          currentSongEntry != null &&
                                          string.Equals(selectedLibrarySongEntry.SongDirectory, currentSongEntry.SongDirectory, StringComparison.OrdinalIgnoreCase),
            showLibraryDifficultySelector = showLibraryDifficultySelector,
            libraryDifficultyLabels = libraryDifficultyLabels,
            libraryDifficultyAvailable = libraryDifficultyAvailable,
            selectedLibraryDifficultyIndex = selectedLibraryDifficultyIndex,
            availableTrackNames = availableTrackNames,
            availableTrackMetaTexts = availableTrackMetaTexts,
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
            showSongSettingsTrackSelectionPopup = showSongSettingsTrackSelectionPopup,
            songSettingsTrackOptionNames = IsCurrentRocksmithTrackGroupingActive()
                ? GetCurrentTrackSelectionGroupsOrdered().Select(group => group.DisplayName ?? "--").ToList()
                : currentSongPartSummaries.Select(summary => summary.Name).ToList(),
            selectedSongSettingsTrackOptionIndex = Mathf.Clamp(
                showSongSettingsTrackSelectionPopup ? selectedSongSettingsTrackSelectionIndex : GetResolvedSongSettingsTrackPopupIndex(),
                0,
                Mathf.Max(0, GetSongSettingsTrackPopupOptionCount() - 1)),
            selectedTrackDisplayName = gameplayMode == GuitarGameplayMode.Arcade
                ? $"{GetSelectedArcadeArrangementDisplayName()} {ArcadeCloneHeroLoader.GetDifficultyLabel(selectedArcadeDifficulty)}"
                : (showTrackSelection ? GetPendingSelectedTrackSummary()?.Name ?? GetTrackDisplayName(GetCurrentTrackOptionIndex()) : GetTrackDisplayName(GetCurrentTrackOptionIndex())),
            selectedTrackTuningLabel = gameplayMode == GuitarGameplayMode.Arcade ? string.Empty : GetResolvedActiveTrackTuningLabel(),
            trackSelectionHint = pendingSongIsArcade
                ? "Arrangement: click row or Q/E. Difficulty: left/right or the X/H/M/E buttons."
                : IsPendingRocksmithDifficultySelectionActive() && PendingRocksmithSelectionHasMultipleDifficulties()
                    ? "Arrangement: click row or Q/E. Difficulty: left/right or the X/H/M/E buttons."
                    : GetTrackOptionCount() > 1 ? "Track: click row or Q/E" : "Track: single detected part",
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
            currentTrackBestScoreValue = currentTrackBestScoreValue,
            currentTrackBestScorePercent = Mathf.Clamp(currentTrackBestScorePercent, 0f, 100f),
            currentTrackBestArcadeScore = currentTrackBestArcadeScoreValue,
            currentTrackBestArcadeHeroScore = currentTrackBestArcadeHeroScoreValue,
            currentSongBestArcadeScore = currentSongBestArcadeScoreValue,
            currentTrackHeroBestScoreValue = currentTrackHeroBestScoreValue,
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
            notesDetectorLatestAcceptanceSourceText = latestNotesDetectorAcceptanceSourceText,
            notesDetectorAvailableTestCategories = notesDetectorSelectableTests.Select(entry => entry?.CategoryTitle ?? string.Empty).ToList(),
            notesDetectorAvailableTestNames = notesDetectorSelectableTests.Select(entry => entry?.Label ?? string.Empty).ToList(),
            notesDetectorAvailableTestDescriptions = notesDetectorSelectableTests.Select(entry => entry?.Description ?? string.Empty).ToList(),
            showNotesDetectorRoutinePopup = showNotesDetectorRoutinePopup,
            notesDetectorRoutineInstructionText = GetNotesDetectorRoutineInstructionText(),
            notesDetectorRoutineTargetText = GetNotesDetectorRoutineTargetText(),
            notesDetectorRoutineStatusText = GetNotesDetectorRoutineStatusText(),
            notesDetectorRoutineProgressText = GetNotesDetectorRoutineProgressText(),
            notesDetectorRoutineTabRows = GetNotesDetectorRoutineTabRows(),
            notesDetectorRoutineTabRowStates = GetNotesDetectorRoutineTabRowStates(),
            notesDetectorRoutineStatusOk = showNotesDetectorRoutinePopup && (notesDetectorRoutineStageIndex >= notesDetectorRoutineSteps.Count || notesDetectorRoutineMatchedSinceTime >= 0f),
            notesDetectorRoutineCompleted = showNotesDetectorRoutinePopup && notesDetectorRoutineStageIndex >= notesDetectorRoutineSteps.Count,
            showStartupTuningReminder = showStartupTuningReminder,
            runtimeSettingsSections = BuildRuntimeSettingsSnapshot()
        };
    }

    private void EnsureRenderer()
    {
        if (activeRenderer != null &&
            activeRendererMode == renderMode &&
            activeRendererGameplayMode == gameplayMode &&
            activeRendererWasMultiplayerRhythm == multiplayerRhythmModeActive)
        {
            return;
        }

        if (activeRenderer != null) activeRenderer.DisposeRenderer();

        if (multiplayerRhythmModeActive)
            activeRenderer = new MultiplayerRhythm3DRenderer();
        else if (gameplayMode == GuitarGameplayMode.Arcade)
            activeRenderer = new ArcadeHighway3DRenderer();
        else if (renderMode == GuitarRenderMode.Tabs)
            activeRenderer = new GuitarTabsRenderer();
        else
            activeRenderer = new GuitarHighway3DRenderer();

        activeRenderer.Initialize(this, chartNotes, tabSections);
        activeRendererMode = renderMode;
        activeRendererGameplayMode = gameplayMode;
        activeRendererWasMultiplayerRhythm = multiplayerRhythmModeActive;
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
        showRocksmithDifficultyPopup = false;
        selectedLoopPausePopupIndex = 0;
        loopSettingsPreviewPlaying = false;
        loopRestartPauseRemainingSeconds = 0f;
        scoreSaveInvalidated = false;
        ResetSessionScoreState();
        loopSettingsReturnRenderMode = renderMode;

        float sectionDuration = GetEffectiveTabSectionDuration();
        loopStartTime = Mathf.Max(0.2f, sectionDuration * 0.40f);
        loopEndTime = Mathf.Max(loopStartTime + 0.5f, sectionDuration * 0.60f);
        loopStartConfigured = false;
        loopEndConfigured = false;
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
        List<ArpeggioGuideData> loadedArpeggioGuides = new List<ArpeggioGuideData>();
        currentArcadeChart = new ArcadeChartData();
        arcadeNoteStates = new List<ArcadeNoteState>();
        arcadeTotalChordCount = 0;
        currentArcadeArrangementSummaries.Clear();
        arcadeRecentInputEvents.Clear();
        activeArcadeSustains.Clear();
        latestArcadeInputEventId = 0;
        ResetArcadeCombo();
        if (useBuiltInDemoSong)
            gameplayMode = GuitarGameplayMode.Guitar;

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
                gameplayMode = currentSongEntry.LibraryType == SongLibraryType.Arcade ? GuitarGameplayMode.Arcade : GuitarGameplayMode.Guitar;
                if (gameplayMode == GuitarGameplayMode.Arcade)
                    renderMode = GuitarRenderMode.Highway3D;

                Debug.Log($"[GuitarBridgeServer] Selected runtime song '{currentSongEntry.SongId}' from {currentSongEntry.SongDirectory}");
                SaveSelectedSongPreference(currentSongEntry);
                currentSongFileName = ResolveSongMetadataFileName(currentSongEntry);

                if (gameplayMode == GuitarGameplayMode.Arcade)
                {
                    loadedNotes = new List<NoteData>();
                    loadedArpeggioGuides = new List<ArpeggioGuideData>();
                    LoadArcadeSongContent(currentSongEntry);
                }
                else
                {
                    SongMetadata trackMetadata = LoadSongMetadata(currentSongFileName);
                    persistedUseAutoTrackSelection = trackMetadata.useAutoTrackSelection;
                    persistedSelectedMusicXmlPartId = string.IsNullOrEmpty(trackMetadata.selectedMusicXmlPartId) ? string.Empty : trackMetadata.selectedMusicXmlPartId;
                    useAutoTrackSelection = persistedUseAutoTrackSelection;
                    selectedMusicXmlPartId = persistedSelectedMusicXmlPartId;

                    currentSongPartSummaries.AddRange(GetPartSummariesWithFallback(currentSongEntry));
                    if (!useAutoTrackSelection &&
                        currentSongEntry.PrimaryNotationKind == SongNotationSourceKind.Rocksmith &&
                        !string.IsNullOrWhiteSpace(selectedMusicXmlPartId))
                    {
                        selectedMusicXmlPartId = GetPersistentRocksmithPartId(selectedMusicXmlPartId, currentSongPartSummaries);
                        persistedSelectedMusicXmlPartId = GetPersistentRocksmithPartId(persistedSelectedMusicXmlPartId, currentSongPartSummaries);
                    }
                    ApplyTrackSelectionPreference();

                    try
                    {
                        loadedNotes = SongNotationFacade.LoadSong(currentSongEntry.PrimaryNotationPath, currentSongEntry.PrimaryNotationKind, midiTrackIndex);
                        loadedArpeggioGuides = SongNotationFacade.LoadArpeggioGuides(currentSongEntry.PrimaryNotationPath, currentSongEntry.PrimaryNotationKind, midiTrackIndex);
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
                            loadedArpeggioGuides = new List<ArpeggioGuideData>();
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
                            loadedArpeggioGuides = new List<ArpeggioGuideData>();
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning("MidiLoader Error: " + e.Message);
                        }
                    }
                }
            }
            else
            {
                gameplayMode = GuitarGameplayMode.Guitar;
                currentSongPartSummaries.Clear();
                Debug.LogWarning("[GuitarBridgeServer] No valid runtime songs were found in persistent storage.");
            }
        }


        InitializeSongMetadataAndAudio();

        bool useDemo = gameplayMode == GuitarGameplayMode.Guitar &&
                       (useBuiltInDemoSong || (useDemoSongIfMidiMissing && (loadedNotes == null || loadedNotes.Count == 0)));

        // 2. Load the demo song if no MIDI was found
        if (useDemo)
        {
            loadedNotes = BuildDemoSong();
            Debug.Log($"Using built-in demo song. Notes: {loadedNotes.Count}");
        }

        // 3. Fallback to random notes if absolutely everything fails
        if (gameplayMode == GuitarGameplayMode.Guitar && (loadedNotes == null || loadedNotes.Count == 0))
        {
            loadedNotes = new List<NoteData>();
            for (int i = 0; i < 50; i++)
            {
                loadedNotes.Add(new NoteData(i * 1.5f + 2f, i % 6, UnityEngine.Random.Range(0, 15), "E"));
            }
        }

        chartNotes = loadedNotes ?? new List<NoteData>();
        currentArpeggioGuides = loadedArpeggioGuides ?? new List<ArpeggioGuideData>();
        chartNoteById.Clear();

        for (int i = 0; i < chartNotes.Count; i++)
        {
            NoteData nd = chartNotes[i];
            if (nd.id < 0) nd.id = i; 
            chartNotes[i] = nd;
            chartNoteById[nd.id] = nd;
        }

        noteStates = chartNotes.Select(n => new GameplayNoteState(n)).ToList();
        

        float songEndTime = gameplayMode == GuitarGameplayMode.Arcade && arcadeNoteStates != null && arcadeNoteStates.Count > 0
            ? arcadeNoteStates.Max(n => n.data.time + n.data.duration)
            : chartNotes.Count > 0 ? chartNotes.Max(n => n.time + n.duration) : GetEffectiveTabSectionDuration();
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

    private void LoadArcadeSongContent(SongLibraryEntry entry)
    {
        currentArcadeArrangementSummaries.Clear();
        currentArcadePracticeSections.Clear();
        currentArcadeChart = new ArcadeChartData();
        arcadeNoteStates = new List<ArcadeNoteState>();
        arcadeTotalChordCount = 0;
        activeArcadeSustains.Clear();
        ResetArcadeCombo();
        selectedArcadePracticeSectionIndex = 0;
        arcadePracticeLoopStartSectionIndex = -1;
        arcadePracticeLoopEndSectionIndex = -1;
        selectedMusicXmlPartId = string.Empty;
        useAutoTrackSelection = false;
        midiTrackIndex = -1;
        currentLoadedTrackIndex = -1;

        if (entry == null || string.IsNullOrWhiteSpace(entry.ArcadeChartPath))
            return;

        List<ArcadeArrangementSummary> summaries = ArcadeCloneHeroLoader.GetArrangementSummaries(entry.ArcadeChartPath);
        currentArcadeArrangementSummaries.AddRange(summaries.Select(summary => summary.Clone()));
        SongMetadata metadata = LoadSongMetadata(ResolveSongMetadataFileName(entry), BuildSongMetadataPath(entry));

        string preferredArrangementId = multiplayerRhythmModeActive && !string.IsNullOrWhiteSpace(selectedArcadeArrangementId)
            ? selectedArcadeArrangementId
            : (metadata != null ? metadata.selectedArcadeArrangementId : string.Empty);
        ArcadeArrangementSummary selectedSummary = !string.IsNullOrWhiteSpace(preferredArrangementId)
            ? currentArcadeArrangementSummaries.FirstOrDefault(summary => string.Equals(summary.ArrangementId, preferredArrangementId, StringComparison.OrdinalIgnoreCase))
            : null;
        selectedSummary ??= currentArcadeArrangementSummaries.FirstOrDefault();

        if (selectedSummary == null)
        {
            Debug.LogWarning($"[GuitarBridgeServer] Clone Hero chart had no playable arrangements: {entry.ArcadeChartPath}");
            return;
        }

        selectedArcadeArrangementId = selectedSummary.ArrangementId;
        ArcadeDifficulty preferredDifficulty = multiplayerRhythmModeActive
            ? selectedArcadeDifficulty
            : ArcadeCloneHeroLoader.ParseDifficulty(metadata?.selectedArcadeDifficulty, ArcadeCloneHeroLoader.GetBestDefaultDifficulty(selectedSummary.Difficulties));
        selectedArcadeDifficulty = selectedSummary.Difficulties.Contains(preferredDifficulty)
            ? preferredDifficulty
            : ArcadeCloneHeroLoader.GetBestDefaultDifficulty(selectedSummary.Difficulties);

        currentArcadeChart = ArcadeCloneHeroLoader.Load(entry.ArcadeChartPath, selectedArcadeArrangementId, selectedArcadeDifficulty);
        if (currentArcadeChart.Arrangements == null || currentArcadeChart.Arrangements.Count == 0)
            currentArcadeChart.Arrangements = currentArcadeArrangementSummaries.Select(summary => summary.Clone()).ToList();
        if (currentArcadeChart.PracticeSections != null)
            currentArcadePracticeSections.AddRange(currentArcadeChart.PracticeSections);

        arcadeNoteStates = currentArcadeChart.Notes != null
            ? currentArcadeChart.Notes.Select(note => new ArcadeNoteState(note)).ToList()
            : new List<ArcadeNoteState>();
        arcadeTotalChordCount = CountArcadeChordGroups(arcadeNoteStates);
        InitializeMultiplayerRhythmChartState();

        currentTrackBestScoreValue = 0;
        currentTrackBestScorePercent = 0f;
        currentTrackBestArcadeScoreValue = GetStoredArcadeScoreValue(metadata, selectedArcadeArrangementId, selectedArcadeDifficulty);
        ArcadeHeroScoreSummary currentArcadeHeroBest = GetStoredArcadeHeroScoreSummary(metadata, selectedArcadeArrangementId, selectedArcadeDifficulty);
        currentTrackBestArcadeHeroScoreValue = currentArcadeHeroBest.scoreValue;
        currentTrackHeroBestScoreValue = currentArcadeHeroBest.scoreValue;
        currentTrackHeroBestScorePercent = currentArcadeHeroBest.accuracyPercent;
        currentTrackHeroBestHeartsRemaining = currentArcadeHeroBest.heartsRemaining;
        currentTrackHeroBestHeartsTotal = currentArcadeHeroBest.heartsTotal;
        currentSongBestScoreValue = 0;
        currentSongBestScorePercent = 0f;
        currentSongBestArcadeScoreValue = GetHighestArcadeScoreValue(metadata);
    }

    private void InitializeMultiplayerRhythmChartState()
    {
        if (multiplayerRhythmPlayers == null)
            return;

        for (int i = 0; i < multiplayerRhythmPlayers.Length; i++)
        {
            MultiplayerRhythmPlayerState player = multiplayerRhythmPlayers[i];
            if (player == null)
                continue;

            player.noteStates = currentArcadeChart != null && currentArcadeChart.Notes != null
                ? currentArcadeChart.Notes.Select(note => new ArcadeNoteState(note)).ToList()
                : new List<ArcadeNoteState>();
            player.sessionScoredChordIds.Clear();
            player.recentInputEvents.Clear();
            player.activeSustains.Clear();
            player.awardedSustainScore.Clear();
            player.latestInputEventId = 0;
            player.comboActive = false;
            player.inputNeedsUnpausedPrime = false;
            player.comboCount = 0;
            player.maxComboCount = 0;
            player.hitCount = 0;
            player.missCount = 0;
            player.scoreValue = 0;
            player.scorePercent = 0f;
            Array.Clear(player.heldLanes, 0, player.heldLanes.Length);
            Array.Clear(player.previousHeldLanes, 0, player.previousHeldLanes.Length);
        }

        multiplayerRhythmWinningPlayerIndex = -1;
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

        if (activeRenderer is ArcadeHighway3DRenderer arcadeRenderer)
            arcadeRenderer.ResetRenderer(chartNotes, tabSections);

        if (activeRenderer is MultiplayerRhythm3DRenderer multiplayerRenderer)
            multiplayerRenderer.ResetRenderer(chartNotes, tabSections);
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
               (metadata.useAllGeneratedPlaybackParts || (metadata.generatedEnabledPartIds?.Count ?? 0) > 0);
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
        useAllGeneratedPlaybackParts = true;
        generatedEnabledPartIds.Clear();
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

        bool matchesDefault = useAllGeneratedPlaybackParts;

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

    public void OpenSongSettingsTrackSelectionPopupFromUi()
    {
        int optionCount = GetSongSettingsTrackPopupOptionCount();
        if (!showSongSettings || optionCount <= 0)
            return;

        showSongSettingsTrackSelectionPopup = true;
        selectedSongSettingsTrackSelectionIndex = Mathf.Clamp(GetResolvedSongSettingsTrackPopupIndex(), 0, Mathf.Max(0, optionCount - 1));
    }

    public void CloseSongSettingsTrackSelectionPopupFromUi()
    {
        showSongSettingsTrackSelectionPopup = false;
    }

    public void MoveSongSettingsTrackSelectionPopupFromUi(int delta)
    {
        int optionCount = GetSongSettingsTrackPopupOptionCount();
        if (!showSongSettingsTrackSelectionPopup || optionCount <= 0)
            return;

        selectedSongSettingsTrackSelectionIndex = (selectedSongSettingsTrackSelectionIndex + delta + optionCount) % optionCount;
    }

    public void ActivateSelectedSongSettingsTrackSelectionPopupFromUi()
    {
        if (!showSongSettingsTrackSelectionPopup)
            return;

        int optionCount = GetSongSettingsTrackPopupOptionCount();
        if (optionCount <= 0)
            return;

        int selectedIndex = Mathf.Clamp(selectedSongSettingsTrackSelectionIndex, 0, optionCount - 1);
        SetTrackSelectionByOption(selectedIndex + 1);
        showSongSettingsTrackSelectionPopup = false;
    }

    public void SetSelectedSongSettingsTrackSelectionPopupIndexFromUi(int index)
    {
        int optionCount = GetSongSettingsTrackPopupOptionCount();
        if (optionCount <= 0)
        {
            selectedSongSettingsTrackSelectionIndex = 0;
            return;
        }

        selectedSongSettingsTrackSelectionIndex = Mathf.Clamp(index, 0, optionCount - 1);
    }

    public void SetSelectedSongSettingsPopupTrackRowIndexFromUi(int index)
    {
        if (showSongSettingsTrackSelectionPopup)
        {
            SetSelectedSongSettingsTrackSelectionPopupIndexFromUi(index);
            return;
        }

        if (showGeneratedAudioTrackSelectionPopup)
            SetSelectedGeneratedAudioTrackSelectionIndexFromUi(index + 3);
    }

    public void ActivateSongSettingsPopupTrackRowFromUi(int index)
    {
        if (showSongSettingsTrackSelectionPopup)
        {
            SetSelectedSongSettingsTrackSelectionPopupIndexFromUi(index);
            ActivateSelectedSongSettingsTrackSelectionPopupFromUi();
            return;
        }

        if (showGeneratedAudioTrackSelectionPopup)
        {
            SetSelectedGeneratedAudioTrackSelectionIndexFromUi(index + 3);
            ActivateSelectedGeneratedAudioTrackSelectionFromUi();
        }
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
            if (gameplayMode == GuitarGameplayMode.Arcade)
                return hasBackingTrack ? SongPlaybackAudioMode.Mp3 : SongPlaybackAudioMode.Muted;

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

    private SongPlaybackAudioMode[] GetAvailableSongPlaybackAudioModesForCurrentGameMode()
    {
        if (gameplayMode == GuitarGameplayMode.Arcade)
            return new[] { SongPlaybackAudioMode.Mp3, SongPlaybackAudioMode.Muted };

        return new[] { SongPlaybackAudioMode.Generated, SongPlaybackAudioMode.Mp3, SongPlaybackAudioMode.Muted };
    }

    private SongPlaybackAudioMode NormalizeSongPlaybackAudioModeForCurrentGameMode(SongPlaybackAudioMode mode)
    {
        if (gameplayMode == GuitarGameplayMode.Arcade)
            return mode == SongPlaybackAudioMode.Muted ? SongPlaybackAudioMode.Muted : SongPlaybackAudioMode.Mp3;

        return mode;
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
            showSongSettingsTrackSelectionPopup = false;
            hasBackingTrack = false;
            backingTrackLoadError = "No runtime song selected.";
            currentSongBestScoreValue = 0;
            currentSongBestScorePercent = 0f;
            currentTrackBestScoreValue = 0;
            currentTrackBestScorePercent = 0f;
            currentSongBestArcadeScoreValue = 0;
            currentTrackBestArcadeScoreValue = 0;
            currentTrackBestArcadeHeroScoreValue = 0;
            currentTrackHeroBestScoreValue = 0;
            currentTrackHeroBestScorePercent = 0f;
            currentTrackHeroBestHeartsRemaining = 0;
            currentTrackHeroBestHeartsTotal = 0;
            selectedLoopBookmarkId = string.Empty;
            loopBookmarkRenameActive = false;
            loopBookmarkRenameDraft = string.Empty;
            Debug.LogWarning(backingTrackLoadError);
            return;
        }

        string songPath = currentSongEntry.Mp3Path;
        currentSongFileName = ResolveSongMetadataFileName(currentSongEntry);
        bool isRocksmithGuitarSong = currentSongEntry.LibraryType == SongLibraryType.Guitar &&
                                     currentSongEntry.PrimaryNotationKind == SongNotationSourceKind.Rocksmith;
        bool rocksmithHasBackingTrack = isRocksmithGuitarSong &&
                                        !string.IsNullOrWhiteSpace(songPath) &&
                                        File.Exists(songPath);

        string metadataPath = BuildSongMetadataPath(currentSongEntry);
        bool metadataExists = !string.IsNullOrWhiteSpace(metadataPath) && File.Exists(metadataPath);
        songMetadata = LoadSongMetadata(currentSongFileName, metadataPath);
        SongPlaybackAudioMode loadedPlaybackAudioMode = songMetadata.playbackAudioMode;
        if (rocksmithHasBackingTrack)
            songMetadata.playbackAudioMode = SongPlaybackAudioMode.Mp3;
        selectedLoopBookmarkId = string.Empty;
        loopBookmarkRenameActive = false;
        loopBookmarkRenameDraft = string.Empty;
        globalAudioOffsetMs = songMetadata.audioOffsetMs;
        audioOffsetMs = globalAudioOffsetMs;
        tabSpeedOffsetPercent = Mathf.Clamp(songMetadata.tabSpeedOffsetPercent <= 0f ? 100f : songMetadata.tabSpeedOffsetPercent, 50f, 150f);
        songStartDelaySeconds = Mathf.Clamp(songMetadata.songStartDelaySeconds <= 0f ? defaultSongStartDelaySeconds : songMetadata.songStartDelaySeconds, 0f, 8f);
        songVolumePercent = Mathf.Clamp(songMetadata.songVolumePercent, 0f, 100f);
        songPlaybackAudioMode = NormalizeSongPlaybackAudioModeForCurrentGameMode(songMetadata.playbackAudioMode);
        loopPauseDurationSeconds = Mathf.Clamp(songMetadata.loopPauseDurationSeconds, 0f, 8f);
        loopStartConfigured = false;
        loopEndConfigured = false;
        if (songMetadata.hasSavedLoopWindow)
        {
            loopStartTime = Mathf.Max(0f, songMetadata.loopStartTime);
            loopEndTime = Mathf.Max(loopStartTime + 0.05f, songMetadata.loopEndTime);
            loopStartConfigured = true;
            loopEndConfigured = true;
        }

        if (gameplayMode == GuitarGameplayMode.Arcade)
        {
            if (songMetadata.playbackAudioMode != songPlaybackAudioMode)
            {
                songMetadata.playbackAudioMode = songPlaybackAudioMode;
                SaveSongMetadata(songMetadata, GetMetadataPath(currentSongFileName), currentSongFileName);
            }

            generatedPlaybackSourceArrangement = null;
            generatedPlaybackArrangement = null;
            generatedSongPlayer?.ClearArrangement();
            showGeneratedAudioTrackSelectionPopup = false;
            showSongSettingsTrackSelectionPopup = false;
            selectedArcadeArrangementId = string.IsNullOrWhiteSpace(songMetadata.selectedArcadeArrangementId)
                ? selectedArcadeArrangementId
                : songMetadata.selectedArcadeArrangementId;
            selectedArcadeDifficulty = ArcadeCloneHeroLoader.ParseDifficulty(songMetadata.selectedArcadeDifficulty, selectedArcadeDifficulty);
            currentSongBestScoreValue = 0;
            currentSongBestScorePercent = 0f;
            currentTrackBestScoreValue = 0;
            currentTrackBestScorePercent = 0f;
            currentSongBestArcadeScoreValue = GetHighestArcadeScoreValue(songMetadata);
            currentTrackBestArcadeScoreValue = GetStoredArcadeScoreValue(songMetadata, selectedArcadeArrangementId, selectedArcadeDifficulty);
            ArcadeHeroScoreSummary currentArcadeHeroBest = GetStoredArcadeHeroScoreSummary(songMetadata, selectedArcadeArrangementId, selectedArcadeDifficulty);
            currentTrackBestArcadeHeroScoreValue = currentArcadeHeroBest.scoreValue;
            currentTrackHeroBestScoreValue = currentArcadeHeroBest.scoreValue;
            currentTrackHeroBestScorePercent = currentArcadeHeroBest.accuracyPercent;
            currentTrackHeroBestHeartsRemaining = currentArcadeHeroBest.heartsRemaining;
            currentTrackHeroBestHeartsTotal = currentArcadeHeroBest.heartsTotal;
            RefreshEffectiveAudioOffset();
            LoadArcadeBackingTracks(currentSongEntry);
            return;
        }

        persistedUseAutoTrackSelection = songMetadata.useAutoTrackSelection;
        persistedSelectedMusicXmlPartId = string.IsNullOrEmpty(songMetadata.selectedMusicXmlPartId) ? string.Empty : songMetadata.selectedMusicXmlPartId;
        useAutoTrackSelection = persistedUseAutoTrackSelection;
        selectedMusicXmlPartId = persistedSelectedMusicXmlPartId;
        bool rocksmithTrackSelectionNormalized = false;
        if (!useAutoTrackSelection &&
            isRocksmithGuitarSong &&
            !string.IsNullOrWhiteSpace(selectedMusicXmlPartId))
        {
            string normalizedSelectedPartId = GetPersistentRocksmithPartId(selectedMusicXmlPartId, currentSongPartSummaries);
            string normalizedPersistedPartId = GetPersistentRocksmithPartId(persistedSelectedMusicXmlPartId, currentSongPartSummaries);
            rocksmithTrackSelectionNormalized =
                !string.Equals(normalizedSelectedPartId, selectedMusicXmlPartId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(normalizedPersistedPartId, persistedSelectedMusicXmlPartId, StringComparison.OrdinalIgnoreCase);
            selectedMusicXmlPartId = normalizedSelectedPartId;
            persistedSelectedMusicXmlPartId = normalizedPersistedPartId;
        }
        useAllGeneratedPlaybackParts = songMetadata.useAllGeneratedPlaybackParts;
        generatedEnabledPartIds = songMetadata.generatedEnabledPartIds != null
            ? songMetadata.generatedEnabledPartIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();
        showGeneratedAudioTrackSelectionPopup = false;
        showSongSettingsTrackSelectionPopup = false;
        selectedGeneratedAudioTrackSelectionIndex = 0;
        selectedSongSettingsTrackSelectionIndex = 0;
        currentSongBestScoreValue = GetHighestTrackScoreValue(songMetadata);
        currentSongBestScorePercent = Mathf.Clamp(GetHighestTrackScore(songMetadata), 0f, 100f);
        currentTrackBestScoreValue = GetStoredTrackScoreValue(songMetadata, selectedMusicXmlPartId);
        currentTrackBestScorePercent = Mathf.Clamp(GetStoredTrackScore(songMetadata, selectedMusicXmlPartId), 0f, 100f);
        currentSongBestArcadeScoreValue = 0;
        currentTrackBestArcadeScoreValue = 0;
        HeroScoreSummary currentHeroTrackBest = GetStoredHeroTrackScoreSummary(songMetadata, selectedMusicXmlPartId);
        currentTrackHeroBestScoreValue = currentHeroTrackBest.scoreValue;
        currentTrackHeroBestScorePercent = currentHeroTrackBest.percent;
        currentTrackHeroBestHeartsRemaining = currentHeroTrackBest.heartsRemaining;
        currentTrackHeroBestHeartsTotal = currentHeroTrackBest.heartsTotal;
        if ((isRocksmithGuitarSong &&
             (rocksmithHasBackingTrack
                 ? loadedPlaybackAudioMode != SongPlaybackAudioMode.Mp3 || !metadataExists
                 : !metadataExists)) ||
            rocksmithTrackSelectionNormalized)
        {
            songMetadata.selectedMusicXmlPartId = persistedSelectedMusicXmlPartId;
            songMetadata.playbackAudioMode = songPlaybackAudioMode;
            SaveSongMetadata(songMetadata, metadataPath, currentSongFileName);
        }
        RefreshEffectiveAudioOffset();
        LoadGeneratedPlaybackArrangementForCurrentSong();
        generatedSongPlayer?.SetMasterVolumePercent(songVolumePercent);

        backingTrackLoadError = string.Empty;
        isLoadingBackingTrack = false;

        ClearArcadeAudioSources();
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

    private void LoadArcadeBackingTracks(SongLibraryEntry entry)
    {
        EnsureBackingTrackSource();
        ShutdownGeneratedSongPlayer();
        ClearArcadeAudioSources();

        List<string> audioPaths = entry?.ArcadeAudioPaths != null
            ? entry.ArcadeAudioPaths.Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();

        if (audioPaths.Count == 0 && !string.IsNullOrWhiteSpace(entry?.Mp3Path) && File.Exists(entry.Mp3Path))
            audioPaths.Add(entry.Mp3Path);

        if (audioPaths.Count == 0)
        {
            hasBackingTrack = false;
            isLoadingBackingTrack = false;
            backingTrackLoadError = $"No Clone Hero audio files found for: {entry?.SongDirectory}";
            Debug.LogWarning(backingTrackLoadError);
            return;
        }

        EnsureArcadeAudioSourceCount(audioPaths.Count);
        backingTrackLoadError = string.Empty;
        hasBackingTrack = false;
        isLoadingBackingTrack = true;
        pendingArcadeAudioLoadCount = audioPaths.Count;

        for (int i = 0; i < audioPaths.Count; i++)
            StartCoroutine(LoadArcadeAudioStemFromFile(audioPaths[i], arcadeAudioSources[i]));
    }

    private void EnsureArcadeAudioSourceCount(int count)
    {
        EnsureBackingTrackSource();
        if (!arcadeAudioSources.Contains(backingTrackSource))
            arcadeAudioSources.Insert(0, backingTrackSource);

        while (arcadeAudioSources.Count < count)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            arcadeAudioSources.Add(source);
        }

        for (int i = 0; i < arcadeAudioSources.Count; i++)
        {
            AudioSource source = arcadeAudioSources[i];
            if (source == null)
                continue;

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.clip = null;
            source.Stop();
            source.enabled = i < count;
        }
    }

    private void ClearArcadeAudioSources()
    {
        for (int i = 0; i < arcadeAudioSources.Count; i++)
        {
            AudioSource source = arcadeAudioSources[i];
            if (source == null)
                continue;

            source.Stop();
            source.clip = null;
            if (source != backingTrackSource)
                source.enabled = false;
        }

        if (backingTrackSource != null)
        {
            backingTrackSource.Stop();
            backingTrackSource.clip = null;
        }
    }

    private System.Collections.IEnumerator LoadArcadeAudioStemFromFile(string absolutePath, AudioSource targetSource)
    {
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
                backingTrackLoadError = $"Failed to load Clone Hero audio '{absolutePath}': {request.error}";
                Debug.LogWarning(backingTrackLoadError);
            }
            else
            {
                AudioClip loadedClip = DownloadHandlerAudioClip.GetContent(request);
                if (loadedClip != null && targetSource != null)
                {
                    loadedClip.name = Path.GetFileNameWithoutExtension(absolutePath);
                    targetSource.clip = loadedClip;
                    hasBackingTrack = true;
                }
            }
        }

        pendingArcadeAudioLoadCount = Mathf.Max(0, pendingArcadeAudioLoadCount - 1);
        if (pendingArcadeAudioLoadCount <= 0)
        {
            isLoadingBackingTrack = false;
            ApplyPlaybackSpeedToAudio();
            SyncAudioToSongTimer(playImmediately: !isPaused);
        }
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

    private static int GetHighestTrackScoreValue(SongMetadata metadata)
    {
        if (metadata == null || metadata.trackScores == null || metadata.trackScores.Count == 0)
            return 0;

        int highest = 0;
        for (int i = 0; i < metadata.trackScores.Count; i++)
            highest = Mathf.Max(highest, Mathf.Max(0, metadata.trackScores[i].bestScoreValue));

        return highest;
    }

    private static int GetHighestArcadeScoreValue(SongMetadata metadata)
    {
        if (metadata == null || metadata.arcadeScores == null || metadata.arcadeScores.Count == 0)
            return 0;

        int highest = 0;
        for (int i = 0; i < metadata.arcadeScores.Count; i++)
            highest = Mathf.Max(highest, Mathf.Max(0, metadata.arcadeScores[i].bestScoreValue));

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
            HeroScoreSummary candidate = new HeroScoreSummary(entry.heroBestScoreValue, entry.heroBestScorePercent, entry.heroBestHeartsRemaining, entry.heroBestHeartsTotal);
            if (ShouldReplaceHeroBest(best, candidate.scoreValue, candidate.percent, candidate.heartsRemaining, candidate.heartsTotal))
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

    private static int GetStoredTrackScoreValue(SongMetadata metadata, string partId)
    {
        if (metadata == null || metadata.trackScores == null || string.IsNullOrEmpty(partId))
            return 0;

        TrackScoreEntry entry = metadata.trackScores.FirstOrDefault(score => string.Equals(score.partId ?? string.Empty, partId, StringComparison.OrdinalIgnoreCase));
        return entry != null ? Mathf.Max(0, entry.bestScoreValue) : 0;
    }

    private static int GetStoredArcadeScoreValue(SongMetadata metadata, string arrangementId, ArcadeDifficulty difficulty)
    {
        if (metadata == null || metadata.arcadeScores == null || string.IsNullOrEmpty(arrangementId))
            return 0;

        string difficultyKey = ArcadeCloneHeroLoader.SerializeDifficulty(difficulty);
        ArcadeScoreEntry entry = metadata.arcadeScores.FirstOrDefault(score =>
            string.Equals(score.arrangementId ?? string.Empty, arrangementId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(score.difficulty ?? string.Empty, difficultyKey, StringComparison.OrdinalIgnoreCase));
        return entry != null ? Mathf.Max(0, entry.bestScoreValue) : 0;
    }

    private static ArcadeHeroScoreSummary GetStoredArcadeHeroScoreSummary(SongMetadata metadata, string arrangementId, ArcadeDifficulty difficulty)
    {
        if (metadata == null || metadata.arcadeScores == null || string.IsNullOrEmpty(arrangementId))
            return default;

        string difficultyKey = ArcadeCloneHeroLoader.SerializeDifficulty(difficulty);
        ArcadeScoreEntry entry = metadata.arcadeScores.FirstOrDefault(score =>
            string.Equals(score.arrangementId ?? string.Empty, arrangementId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(score.difficulty ?? string.Empty, difficultyKey, StringComparison.OrdinalIgnoreCase));
        return entry != null
            ? new ArcadeHeroScoreSummary(entry.heroBestScoreValue, entry.heroBestScorePercent, entry.heroBestHeartsRemaining, entry.heroBestHeartsTotal)
            : default;
    }

    private static HeroScoreSummary GetStoredHeroTrackScoreSummary(SongMetadata metadata, string partId)
    {
        if (metadata == null || metadata.trackScores == null || string.IsNullOrEmpty(partId))
            return default;

        TrackScoreEntry entry = metadata.trackScores.FirstOrDefault(score => string.Equals(score.partId ?? string.Empty, partId, StringComparison.OrdinalIgnoreCase));
        return entry != null
            ? new HeroScoreSummary(entry.heroBestScoreValue, entry.heroBestScorePercent, entry.heroBestHeartsRemaining, entry.heroBestHeartsTotal)
            : default;
    }

    private static void UpsertTrackScore(SongMetadata metadata, string partId, string displayName, int scoreValue, float percent)
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
                bestScoreValue = Mathf.Max(0, scoreValue),
                bestScorePercent = Mathf.Clamp(percent, 0f, 100f)
            });
            return;
        }

        existing.displayName = string.IsNullOrEmpty(displayName) ? existing.displayName : displayName;
        int clampedScoreValue = Mathf.Max(0, scoreValue);
        float clampedPercent = Mathf.Clamp(percent, 0f, 100f);
        if (clampedScoreValue > existing.bestScoreValue)
        {
            existing.bestScoreValue = clampedScoreValue;
            existing.bestScorePercent = clampedPercent;
        }
        else if (clampedScoreValue == existing.bestScoreValue)
        {
            existing.bestScorePercent = Mathf.Max(existing.bestScorePercent, clampedPercent);
        }
    }

    private static void UpsertArcadeScore(SongMetadata metadata, string arrangementId, string displayName, ArcadeDifficulty difficulty, int scoreValue, float accuracyPercent, int heartsRemaining, int heartsTotal)
    {
        if (metadata == null || string.IsNullOrEmpty(arrangementId))
            return;

        if (metadata.arcadeScores == null)
            metadata.arcadeScores = new List<ArcadeScoreEntry>();

        string difficultyKey = ArcadeCloneHeroLoader.SerializeDifficulty(difficulty);
        ArcadeScoreEntry existing = metadata.arcadeScores.FirstOrDefault(score =>
            string.Equals(score.arrangementId ?? string.Empty, arrangementId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(score.difficulty ?? string.Empty, difficultyKey, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            metadata.arcadeScores.Add(new ArcadeScoreEntry
            {
                arrangementId = arrangementId,
                displayName = displayName,
                difficulty = difficultyKey,
                bestScorePercent = heartsTotal > 0 ? 0f : Mathf.Clamp(accuracyPercent, 0f, 100f),
                bestAccuracyPercent = heartsTotal > 0 ? 0f : Mathf.Clamp(accuracyPercent, 0f, 100f),
                bestScoreValue = heartsTotal > 0 ? 0 : Mathf.Max(0, scoreValue),
                heroBestScorePercent = heartsTotal > 0 ? Mathf.Clamp(accuracyPercent, 0f, 100f) : 0f,
                heroBestAccuracyPercent = heartsTotal > 0 ? Mathf.Clamp(accuracyPercent, 0f, 100f) : 0f,
                heroBestScoreValue = heartsTotal > 0 ? Mathf.Max(0, scoreValue) : 0,
                heroBestHeartsRemaining = heartsTotal > 0 ? Mathf.Max(0, heartsRemaining) : 0,
                heroBestHeartsTotal = heartsTotal > 0 ? Mathf.Max(0, heartsTotal) : 0
            });
            return;
        }

        existing.displayName = string.IsNullOrEmpty(displayName) ? existing.displayName : displayName;
        existing.difficulty = difficultyKey;
        float clampedAccuracy = Mathf.Clamp(accuracyPercent, 0f, 100f);
        if (heartsTotal <= 0)
        {
            if (scoreValue > existing.bestScoreValue)
            {
                existing.bestScoreValue = Mathf.Max(0, scoreValue);
                existing.bestAccuracyPercent = clampedAccuracy;
                existing.bestScorePercent = clampedAccuracy;
            }
            else if (scoreValue == existing.bestScoreValue)
            {
                existing.bestAccuracyPercent = Mathf.Max(existing.bestAccuracyPercent, clampedAccuracy);
                existing.bestScorePercent = Mathf.Max(existing.bestScorePercent, clampedAccuracy);
            }
        }

        ArcadeHeroScoreSummary existingHero = new ArcadeHeroScoreSummary(existing.heroBestScoreValue, existing.heroBestScorePercent, existing.heroBestHeartsRemaining, existing.heroBestHeartsTotal);
        bool replaceHero = !existingHero.IsAvailable;
        if (!replaceHero)
        {
            if (scoreValue > existingHero.scoreValue)
                replaceHero = true;
            else if (scoreValue == existingHero.scoreValue)
            {
                if (heartsTotal > 0 && existingHero.heartsTotal > 0 && heartsTotal < existingHero.heartsTotal)
                    replaceHero = true;
                else if (heartsTotal == existingHero.heartsTotal && clampedAccuracy > existingHero.accuracyPercent + 0.01f)
                    replaceHero = true;
            }
        }

        if (heartsTotal > 0 && replaceHero)
        {
            existing.heroBestScoreValue = Mathf.Max(0, scoreValue);
            existing.heroBestAccuracyPercent = clampedAccuracy;
            existing.heroBestScorePercent = clampedAccuracy;
            existing.heroBestHeartsRemaining = Mathf.Max(0, heartsRemaining);
            existing.heroBestHeartsTotal = Mathf.Max(0, heartsTotal);
        }
    }

    private static bool ShouldReplaceHeroBest(HeroScoreSummary existing, int scoreValue, float percent, int heartsRemaining, int heartsTotal)
    {
        HeroScoreSummary candidate = new HeroScoreSummary(scoreValue, percent, heartsRemaining, heartsTotal);
        if (!candidate.IsAvailable)
            return false;

        if (!existing.IsAvailable)
            return true;

        if (candidate.scoreValue > existing.scoreValue)
            return true;

        if (candidate.scoreValue < existing.scoreValue)
            return false;

        if (candidate.heartsTotal > 0 && existing.heartsTotal > 0)
        {
            if (candidate.heartsTotal < existing.heartsTotal)
                return true;

            if (candidate.heartsTotal > existing.heartsTotal)
                return false;
        }

        if (candidate.percent > existing.percent + 0.01f)
            return true;

        if (candidate.percent < existing.percent - 0.01f)
            return false;

        if (candidate.heartsRemaining > existing.heartsRemaining)
            return true;

        if (candidate.heartsRemaining < existing.heartsRemaining)
            return false;

        return false;
    }

    private static void UpsertHeroTrackScore(SongMetadata metadata, string partId, string displayName, int scoreValue, float percent, int heartsRemaining, int heartsTotal)
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

        HeroScoreSummary existingSummary = new HeroScoreSummary(existing.heroBestScoreValue, existing.heroBestScorePercent, existing.heroBestHeartsRemaining, existing.heroBestHeartsTotal);
        if (!ShouldReplaceHeroBest(existingSummary, scoreValue, percent, heartsRemaining, heartsTotal))
            return;

        existing.displayName = string.IsNullOrEmpty(displayName) ? existing.displayName : displayName;
        existing.heroBestScoreValue = Mathf.Max(0, scoreValue);
        existing.heroBestScorePercent = Mathf.Clamp(percent, 0f, 100f);
        existing.heroBestHeartsRemaining = Mathf.Max(0, heartsRemaining);
        existing.heroBestHeartsTotal = Mathf.Max(0, heartsTotal);
    }

    private static string FormatHeroScoreText(HeroScoreSummary heroScore)
    {
        if (!heroScore.IsAvailable)
            return string.Empty;

        return FormattableString.Invariant($"H {FormatGuitarScoreValue(heroScore.scoreValue)} ({heroScore.heartsTotal}H)");
    }

    private static string FormatLibraryHeroScoreText(HeroScoreSummary heroScore)
    {
        return heroScore.IsAvailable
            ? FormattableString.Invariant($"{FormatGuitarScoreValue(heroScore.scoreValue)} ({heroScore.heartsTotal}H)")
            : "--";
    }

    private static string FormatLibraryHeroScoreText(ArcadeHeroScoreSummary heroScore)
    {
        return heroScore.IsAvailable
            ? FormattableString.Invariant($"{Mathf.Max(0, heroScore.scoreValue).ToString("N0", CultureInfo.InvariantCulture)} ({heroScore.heartsTotal}H)")
            : "--";
    }

    private static string BuildCombinedScoreText(int normalScoreValue, HeroScoreSummary heroScore)
    {
        string normalText = FormatGuitarMenuScoreText(normalScoreValue);
        if (!heroScore.IsAvailable)
            return normalText;

        string heroText = FormatHeroScoreText(heroScore);
        return normalScoreValue > 0
            ? $"{normalText}  |  {heroText}"
            : heroText;
    }

    private static string FormatGuitarScoreValue(int scoreValue)
    {
        return Mathf.Max(0, scoreValue).ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string FormatGuitarMenuScoreText(int scoreValue)
    {
        return scoreValue > 0 ? FormatGuitarScoreValue(scoreValue) : "--";
    }

    private static string FormatArcadeMenuScoreText(int scoreValue)
    {
        if (scoreValue <= 0)
            return "--";

        return scoreValue >= 100000
            ? FormatArcadeCompactScoreText(scoreValue)
            : scoreValue.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string FormatArcadeCompactScoreText(int scoreValue)
    {
        if (scoreValue <= 0)
            return "--";
        if (scoreValue >= 1000000)
            return FormattableString.Invariant($"{scoreValue / 1000000f:0.#}M");
        if (scoreValue >= 1000)
            return FormattableString.Invariant($"{scoreValue / 1000f:0.#}k");
        return scoreValue.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatSongLibraryDurationLabel(float durationSeconds)
    {
        if (durationSeconds <= 0.5f)
            return string.Empty;

        int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(durationSeconds));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return FormattableString.Invariant($"{minutes}:{seconds:00}");
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
            return entry.LibraryType == SongLibraryType.Arcade
                ? Mathf.Max(0f, songMetadata != null ? currentSongBestArcadeScoreValue : entry.CachedBestArcadeScoreValue)
                : Mathf.Clamp(songMetadata != null ? currentSongBestScorePercent : entry.CachedBestScorePercent, 0f, 100f);

        return entry.LibraryType == SongLibraryType.Arcade
            ? Mathf.Max(0f, entry.CachedBestArcadeScoreValue)
            : Mathf.Clamp(entry.CachedBestScorePercent, 0f, 100f);
    }

    private static string GetSongLibraryDifficultyDisplayLabel(SongLibraryEntry entry)
    {
        if (entry == null)
            return string.Empty;

        if (entry.LibraryType == SongLibraryType.Arcade)
            return !string.IsNullOrWhiteSpace(entry.ArcadeDifficultySummary) ? entry.ArcadeDifficultySummary : "Rhythm";

        if (entry.PrimaryNotationKind == SongNotationSourceKind.Rocksmith)
            return SongLibraryService.GetDifficultyLabel(entry.DifficultyRating);

        if (!string.IsNullOrWhiteSpace(entry.DifficultyDisplayLabel))
            return entry.DifficultyDisplayLabel;

        return SongLibraryService.GetDifficultyLabel(entry.DifficultyRating);
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
            summaries = cachedSummaries.Select(summary => summary.Clone()).ToList();
        }
        else
        {
            summaries = GetPartSummariesWithFallback(entry);
            cachedTrackSummariesByNotationPath[entry.PrimaryNotationPath] = summaries.Select(summary => summary.Clone()).ToList();
            cachedTrackSummaryTicksByNotationPath[entry.PrimaryNotationPath] = notationTicks;
        }

        SongMetadata metadata = LoadSongMetadataForEntry(entry);

        bool isRocksmithDifficultySong = entry.PrimaryNotationKind == SongNotationSourceKind.Rocksmith &&
                                         summaries.Any(summary => summary != null && summary.HasDifficultyVariants);
        if (isRocksmithDifficultySong)
        {
            return summaries
                .OrderByDescending(summary => GetDefaultRoutePriorityForTrack(summary))
                .ThenBy(summary => summary.GroupDisplayName ?? summary.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(summary => summary.DifficultyUiIndex < 0 ? int.MaxValue : summary.DifficultyUiIndex)
                .ToList();
        }

        return summaries
            .OrderByDescending(summary => GetStoredTrackScore(metadata, summary.PartId))
            .ThenBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int GetDefaultRoutePriorityForTrack(MusicXmlLoader.MusicXmlPartSummary summary)
    {
        if (summary == null)
            return int.MinValue;

        string name = !string.IsNullOrWhiteSpace(summary.GroupDisplayName) ? summary.GroupDisplayName : summary.Name;
        if (string.IsNullOrWhiteSpace(name))
            return summary.Score;

        if (name.IndexOf("lead", StringComparison.OrdinalIgnoreCase) >= 0)
            return 4000 + summary.Score;
        if (name.IndexOf("combo", StringComparison.OrdinalIgnoreCase) >= 0)
            return 3000 + summary.Score;
        if (name.IndexOf("rhythm", StringComparison.OrdinalIgnoreCase) >= 0)
            return 2000 + summary.Score;
        if (name.IndexOf("bass", StringComparison.OrdinalIgnoreCase) >= 0)
            return 1000 + summary.Score;
        return summary.Score;
    }

    private void SaveSelectedSongPreference(SongLibraryEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.SongDirectory))
            return;

        PlayerPrefs.SetString(SelectedSongDirectoryPrefsKey, entry.SongDirectory);
        PlayerPrefs.SetInt(SelectedSongLibraryTypePrefsKey, (int)entry.LibraryType);
        PlayerPrefs.Save();
    }

    private static string LoadSelectedSongPreference()
    {
        return PlayerPrefs.GetString(SelectedSongDirectoryPrefsKey, string.Empty);
    }

    private void LoadGameSaveState()
    {
        firstStartCompleted = false;
        string path = Path.Combine(ExternalContentPaths.PersistentRoot, GameSaveStateFileName);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (!File.Exists(path))
            {
                SaveGameSaveState();
                return;
            }

            string json = File.ReadAllText(path);
            GameSaveState state = JsonUtility.FromJson<GameSaveState>(json);
            firstStartCompleted = state != null && state.firstStartCompleted;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load game save state: {ex.Message}");
        }
    }

    private void SaveGameSaveState()
    {
        string path = Path.Combine(ExternalContentPaths.PersistentRoot, GameSaveStateFileName);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            GameSaveState state = new GameSaveState
            {
                firstStartCompleted = firstStartCompleted
            };
            File.WriteAllText(path, JsonUtility.ToJson(state, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to save game save state: {ex.Message}");
        }
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

    private void LoadSongLibraryTypePreference()
    {
        selectedSongLibraryType = (SongLibraryType)Mathf.Clamp(
            PlayerPrefs.GetInt(SelectedSongLibraryTypePrefsKey, (int)SongLibraryType.Guitar),
            (int)SongLibraryType.Guitar,
            (int)SongLibraryType.Arcade);
    }

    private void SaveNativeDetectorPreferences()
    {
        PlayerPrefs.SetInt(NativeDetectorInputDevicePrefsKey, selectedNativeNotesDetectorInputDeviceIndex);
        PlayerPrefs.Save();
    }

    private void ResetSessionScoreState(bool ignoreCurrentlyResolvedNotes = false)
    {
        if (multiplayerRhythmModeActive)
        {
            ResetMultiplayerRhythmSessionScoreState(ignoreCurrentlyResolvedNotes);
            return;
        }

        sessionScoredNoteIds.Clear();
        arcadeSessionScoredNoteIds.Clear();
        arcadeChordAwardedSustainScore.Clear();
        sessionScoreHits = 0;
        sessionScoreMisses = 0;
        currentSessionScoreValue = 0;
        currentSessionScorePercent = 0f;
        currentSessionArcadeScoreValue = 0;
        guitarComboCount = 0;
        activeArcadeSustains.Clear();
        ResetArcadeCombo();

        if (!ignoreCurrentlyResolvedNotes)
            return;

        if (gameplayMode == GuitarGameplayMode.Arcade)
        {
            if (arcadeNoteStates == null)
                return;

            for (int i = 0; i < arcadeNoteStates.Count; i++)
            {
                ArcadeNoteState noteState = arcadeNoteStates[i];
                if (noteState == null || !noteState.IsResolved)
                    continue;

                int noteKey = GetArcadeConsumeChordId(noteState.data);
                arcadeSessionScoredNoteIds.Add(noteKey);
            }

            return;
        }

        if (noteStates == null)
            return;

        for (int i = 0; i < noteStates.Count; i++)
        {
            GameplayNoteState noteState = noteStates[i];
            if (noteState == null || !noteState.IsResolved)
                continue;

            int noteKey = GetGuitarScoreEventKey(noteState.data, i);
            sessionScoredNoteIds.Add(noteKey);
        }
    }

    private void ResetMultiplayerRhythmSessionScoreState(bool ignoreCurrentlyResolvedNotes = false)
    {
        if (multiplayerRhythmPlayers == null)
            return;

        for (int i = 0; i < multiplayerRhythmPlayers.Length; i++)
        {
            MultiplayerRhythmPlayerState player = multiplayerRhythmPlayers[i];
            if (player == null)
                continue;

            player.sessionScoredChordIds.Clear();
            player.recentInputEvents.Clear();
            player.activeSustains.Clear();
            player.awardedSustainScore.Clear();
            player.hitCount = 0;
            player.missCount = 0;
            player.scoreValue = 0;
            player.scorePercent = 0f;
            player.comboCount = 0;
            player.maxComboCount = 0;
            player.comboActive = false;
            player.latestInputEventId = 0;
            Array.Clear(player.heldLanes, 0, player.heldLanes.Length);
            Array.Clear(player.previousHeldLanes, 0, player.previousHeldLanes.Length);

            if (!ignoreCurrentlyResolvedNotes || player.noteStates == null)
                continue;

            for (int noteIndex = 0; noteIndex < player.noteStates.Count; noteIndex++)
            {
                ArcadeNoteState noteState = player.noteStates[noteIndex];
                if (noteState == null || !noteState.IsResolved)
                    continue;

                player.sessionScoredChordIds.Add(GetArcadeConsumeChordId(noteState.data));
            }
        }

        multiplayerRhythmWinningPlayerIndex = -1;
    }

    private void UpdateSessionScoreState()
    {
        if (multiplayerRhythmModeActive)
        {
            UpdateMultiplayerRhythmSessionScoreState();
            return;
        }

        if (gameplayMode == GuitarGameplayMode.Arcade)
        {
            UpdateArcadeSessionScoreState();
            return;
        }

        if (loopEnabled || noteStates == null || noteStates.Count == 0)
            return;

        RebuildGuitarSessionProgressFromResolvedStates();
    }

    private void UpdateArcadeSessionScoreState()
    {
        if (loopEnabled || arcadeNoteStates == null || arcadeNoteStates.Count == 0)
            return;

        for (int i = 0; i < arcadeNoteStates.Count; i++)
        {
            ArcadeNoteState noteState = arcadeNoteStates[i];
            if (noteState == null || !noteState.IsResolved)
                continue;

            int noteKey = GetArcadeConsumeChordId(noteState.data);
            if (!arcadeSessionScoredNoteIds.Add(noteKey))
                continue;

            if (noteState.IsHit)
                sessionScoreHits++;
            else if (noteState.IsMissed)
                sessionScoreMisses++;
        }

        int total = arcadeTotalChordCount > 0 ? arcadeTotalChordCount : CountArcadeChordGroups(arcadeNoteStates);
        currentSessionScorePercent = total > 0
            ? Mathf.Clamp(100f * sessionScoreHits / total, 0f, 100f)
            : 0f;
    }

    private void UpdateMultiplayerRhythmSessionScoreState()
    {
        if (loopEnabled || multiplayerRhythmPlayers == null)
            return;

        int total = arcadeTotalChordCount > 0
            ? arcadeTotalChordCount
            : (currentArcadeChart != null && currentArcadeChart.Notes != null
                ? CountArcadeChordGroups(currentArcadeChart.Notes.Select(note => new ArcadeNoteState(note)).ToList())
                : 0);

        for (int i = 0; i < multiplayerRhythmPlayers.Length; i++)
        {
            MultiplayerRhythmPlayerState player = multiplayerRhythmPlayers[i];
            if (player == null)
                continue;

            UpdateMultiplayerRhythmSessionScoreState(player, total);
        }

        UpdateMultiplayerRhythmWinnerState();
    }

    private void UpdateMultiplayerRhythmSessionScoreState(MultiplayerRhythmPlayerState player, int totalChordCount)
    {
        if (player == null || player.noteStates == null || player.noteStates.Count == 0)
            return;

        int hits = 0;
        int misses = 0;
        HashSet<int> processedChordIds = new HashSet<int>();
        for (int i = 0; i < player.noteStates.Count; i++)
        {
            ArcadeNoteState noteState = player.noteStates[i];
            if (noteState == null || !noteState.IsResolved)
                continue;

            int chordId = GetArcadeConsumeChordId(noteState.data);
            if (!processedChordIds.Add(chordId))
                continue;

            if (noteState.IsHit)
                hits++;
            else if (noteState.IsMissed)
                misses++;
        }

        player.hitCount = hits;
        player.missCount = misses;
        player.scorePercent = totalChordCount > 0
            ? Mathf.Clamp(100f * hits / totalChordCount, 0f, 100f)
            : 0f;
        player.maxComboCount = Mathf.Max(player.maxComboCount, player.comboCount);
    }

    private void UpdateMultiplayerRhythmWinnerState()
    {
        multiplayerRhythmWinningPlayerIndex = -1;
        if (multiplayerRhythmPlayers == null || multiplayerRhythmPlayers.Length < MultiplayerRhythmPlayerCount)
            return;

        MultiplayerRhythmPlayerState first = multiplayerRhythmPlayers[0];
        MultiplayerRhythmPlayerState second = multiplayerRhythmPlayers[1];
        if (first == null || second == null)
            return;

        int comparison = first.scoreValue.CompareTo(second.scoreValue);
        if (comparison == 0)
            comparison = first.scorePercent.CompareTo(second.scorePercent);
        if (comparison == 0)
            comparison = second.missCount.CompareTo(first.missCount);
        if (comparison == 0)
            comparison = first.maxComboCount.CompareTo(second.maxComboCount);

        if (comparison > 0)
            multiplayerRhythmWinningPlayerIndex = 0;
        else if (comparison < 0)
            multiplayerRhythmWinningPlayerIndex = 1;
    }

    private void UpdateAndPersistSongBestScore()
    {
        if (gameplayMode == GuitarGameplayMode.Arcade)
        {
            UpdateAndPersistArcadeBestScore();
            return;
        }

        if (scoreSaveInvalidated || loopEnabled || currentSongEntry == null || noteStates == null || noteStates.Count == 0 || string.IsNullOrEmpty(selectedMusicXmlPartId))
            return;

        string trackName = GetTrackDisplayName(GetCurrentTrackOptionIndex());
        int scoreValue = Mathf.Max(0, currentSessionScoreValue);
        float percent = Mathf.Clamp(currentSessionScorePercent, 0f, 100f);
        if (heroModeEnabled)
        {
            int heartsRemaining = GetCurrentHeroHeartsRemaining();
            HeroScoreSummary currentHeroBest = GetStoredHeroTrackScoreSummary(songMetadata, selectedMusicXmlPartId);
            if (!ShouldReplaceHeroBest(currentHeroBest, scoreValue, percent, heartsRemaining, heroModeHeartCount))
                return;

            currentTrackHeroBestScoreValue = scoreValue;
            currentTrackHeroBestScorePercent = percent;
            currentTrackHeroBestHeartsRemaining = heartsRemaining;
            currentTrackHeroBestHeartsTotal = heroModeHeartCount;
            UpsertHeroTrackScore(songMetadata, selectedMusicXmlPartId, trackName, scoreValue, percent, heartsRemaining, heroModeHeartCount);
            currentSongBestScoreValue = GetHighestTrackScoreValue(songMetadata);
            currentSongBestScorePercent = Mathf.Clamp(GetHighestTrackScore(songMetadata), 0f, 100f);
            SaveSongMetadata();
            return;
        }

        if (scoreValue < currentTrackBestScoreValue || (scoreValue == currentTrackBestScoreValue && percent <= currentTrackBestScorePercent + 0.01f))
            return;

        currentTrackBestScoreValue = scoreValue;
        currentTrackBestScorePercent = percent;
        UpsertTrackScore(songMetadata, selectedMusicXmlPartId, trackName, scoreValue, percent);
        currentSongBestScoreValue = GetHighestTrackScoreValue(songMetadata);
        currentSongBestScorePercent = Mathf.Clamp(GetHighestTrackScore(songMetadata), 0f, 100f);
        SaveSongMetadata();
    }

    private void UpdateAndPersistArcadeBestScore()
    {
        if (scoreSaveInvalidated || loopEnabled || currentSongEntry == null || arcadeNoteStates == null || arcadeNoteStates.Count == 0 || string.IsNullOrEmpty(selectedArcadeArrangementId))
            return;

        int scoreValue = Mathf.Max(0, currentSessionArcadeScoreValue);
        string arrangementName = GetSelectedArcadeArrangementDisplayName();
        int heartsRemaining = heroModeEnabled ? GetCurrentHeroHeartsRemaining() : 0;
        int heartsTotal = heroModeEnabled ? heroModeHeartCount : 0;
        if (heroModeEnabled)
        {
            ArcadeHeroScoreSummary existingHero = GetStoredArcadeHeroScoreSummary(songMetadata, selectedArcadeArrangementId, selectedArcadeDifficulty);
            bool beatsHero = !existingHero.IsAvailable || scoreValue > existingHero.scoreValue;
            if (!beatsHero && scoreValue == existingHero.scoreValue)
            {
                if (heartsTotal > 0 && existingHero.heartsTotal > 0 && heartsTotal < existingHero.heartsTotal)
                    beatsHero = true;
                else if (heartsTotal == existingHero.heartsTotal && currentSessionScorePercent > existingHero.accuracyPercent + 0.01f)
                    beatsHero = true;
            }

            if (!beatsHero)
                return;

            currentTrackBestArcadeHeroScoreValue = scoreValue;
            currentTrackHeroBestScorePercent = Mathf.Clamp(currentSessionScorePercent, 0f, 100f);
            currentTrackHeroBestHeartsRemaining = heartsRemaining;
            currentTrackHeroBestHeartsTotal = heartsTotal;
        }
        else
        {
            if (scoreValue < currentTrackBestArcadeScoreValue)
                return;

            currentTrackBestArcadeScoreValue = scoreValue;
        }

        UpsertArcadeScore(songMetadata, selectedArcadeArrangementId, arrangementName, selectedArcadeDifficulty, scoreValue, currentSessionScorePercent, heartsRemaining, heartsTotal);
        currentSongBestArcadeScoreValue = GetHighestArcadeScoreValue(songMetadata);
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
            favoriteInLibrary = false,
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
            selectedArcadeArrangementId = string.Empty,
            selectedArcadeDifficulty = ArcadeDifficulty.Expert.ToString(),
            useAllGeneratedPlaybackParts = true,
            generatedEnabledPartIds = new List<string>(),
            generatedPlaybackSelectionOverrides = new List<GeneratedPlaybackSelectionOverride>(),
            bestArcadeScoreValue = 0,
            loopBookmarkScopes = new List<LoopBookmarkScopeEntry>(),
            trackScores = new List<TrackScoreEntry>(),
            arcadeScores = new List<ArcadeScoreEntry>(),
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
                if (data.loopBookmarkScopes == null)
                    data.loopBookmarkScopes = new List<LoopBookmarkScopeEntry>();
                if (data.trackScores == null)
                    data.trackScores = new List<TrackScoreEntry>();
                if (data.arcadeScores == null)
                    data.arcadeScores = new List<ArcadeScoreEntry>();
                for (int i = 0; i < data.loopBookmarkScopes.Count; i++)
                {
                    LoopBookmarkScopeEntry scope = data.loopBookmarkScopes[i];
                    if (scope == null)
                        continue;
                    if (scope.bookmarks == null)
                        scope.bookmarks = new List<LoopBookmarkEntry>();
                    for (int j = scope.bookmarks.Count - 1; j >= 0; j--)
                    {
                        LoopBookmarkEntry bookmark = scope.bookmarks[j];
                        if (bookmark == null)
                        {
                            scope.bookmarks.RemoveAt(j);
                            continue;
                        }

                        bookmark.name = string.IsNullOrWhiteSpace(bookmark.name) ? "bookmark" : bookmark.name.Trim();
                        bookmark.loopStartTime = Mathf.Max(0f, bookmark.loopStartTime);
                        bookmark.loopEndTime = Mathf.Max(bookmark.loopStartTime + 0.05f, bookmark.loopEndTime);
                        if (string.IsNullOrWhiteSpace(bookmark.bookmarkId))
                            bookmark.bookmarkId = Guid.NewGuid().ToString("N");
                        if (bookmark.createdUtcTicks <= 0L)
                            bookmark.createdUtcTicks = bookmark.updatedUtcTicks > 0L ? bookmark.updatedUtcTicks : DateTime.UtcNow.Ticks;
                        if (bookmark.updatedUtcTicks <= 0L)
                            bookmark.updatedUtcTicks = bookmark.createdUtcTicks;
                    }
                }
                data.bestScoreValue = Mathf.Max(0, data.bestScoreValue);
                if (data.bestArcadeScoreValue < 0)
                    data.bestArcadeScoreValue = 0;
                if (data.trackScores == null)
                    data.trackScores = new List<TrackScoreEntry>();
                for (int i = 0; i < data.trackScores.Count; i++)
                {
                    TrackScoreEntry trackScore = data.trackScores[i];
                    if (trackScore == null)
                        continue;

                    trackScore.bestScoreValue = Mathf.Max(0, trackScore.bestScoreValue);
                    trackScore.bestScorePercent = Mathf.Clamp(trackScore.bestScorePercent, 0f, 100f);
                    trackScore.heroBestScoreValue = Mathf.Max(0, trackScore.heroBestScoreValue);
                    trackScore.heroBestScorePercent = Mathf.Clamp(trackScore.heroBestScorePercent, 0f, 100f);
                    trackScore.heroBestHeartsRemaining = Mathf.Max(0, trackScore.heroBestHeartsRemaining);
                    trackScore.heroBestHeartsTotal = Mathf.Max(0, trackScore.heroBestHeartsTotal);
                }
                for (int i = 0; i < data.arcadeScores.Count; i++)
                {
                    ArcadeScoreEntry arcadeScore = data.arcadeScores[i];
                    if (arcadeScore == null)
                        continue;

                    if (arcadeScore.bestAccuracyPercent <= 0.01f && arcadeScore.bestScorePercent > 0.01f)
                        arcadeScore.bestAccuracyPercent = Mathf.Clamp(arcadeScore.bestScorePercent, 0f, 100f);
                    arcadeScore.bestScoreValue = Mathf.Max(0, arcadeScore.bestScoreValue);
                    if (arcadeScore.heroBestAccuracyPercent <= 0.01f && arcadeScore.heroBestScorePercent > 0.01f)
                        arcadeScore.heroBestAccuracyPercent = Mathf.Clamp(arcadeScore.heroBestScorePercent, 0f, 100f);
                    arcadeScore.heroBestScoreValue = Mathf.Max(0, arcadeScore.heroBestScoreValue);
                    arcadeScore.heroBestHeartsRemaining = Mathf.Max(0, arcadeScore.heroBestHeartsRemaining);
                    arcadeScore.heroBestHeartsTotal = Mathf.Max(0, arcadeScore.heroBestHeartsTotal);
                }
                data.bestScoreValue = Mathf.Max(data.bestScoreValue, GetHighestTrackScoreValue(data));
                data.bestArcadeScoreValue = Mathf.Max(data.bestArcadeScoreValue, GetHighestArcadeScoreValue(data));
                if (string.IsNullOrWhiteSpace(data.selectedArcadeDifficulty))
                    data.selectedArcadeDifficulty = ArcadeDifficulty.Expert.ToString();
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
                        bestScoreValue = Mathf.Max(0, data.bestScoreValue),
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
        songMetadata.hasSavedLoopWindow = HasConfiguredLoopWindow();
        songMetadata.loopStartTime = loopStartTime;
        songMetadata.loopEndTime = loopEndTime;
        songMetadata.loopPauseDurationSeconds = loopPauseDurationSeconds;
        songMetadata.useAutoTrackSelection = persistedUseAutoTrackSelection;
        songMetadata.selectedMusicXmlPartId = persistedSelectedMusicXmlPartId;
        songMetadata.selectedArcadeArrangementId = selectedArcadeArrangementId;
        songMetadata.selectedArcadeDifficulty = ArcadeCloneHeroLoader.SerializeDifficulty(selectedArcadeDifficulty);
        songMetadata.useAllGeneratedPlaybackParts = useAllGeneratedPlaybackParts;
        songMetadata.generatedEnabledPartIds = useAllGeneratedPlaybackParts
            ? new List<string>()
            : generatedEnabledPartIds != null
            ? generatedEnabledPartIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();
        songMetadata.bestScoreValue = GetHighestTrackScoreValue(songMetadata);
        songMetadata.bestScorePercent = Mathf.Clamp(GetHighestTrackScore(songMetadata), 0f, 100f);
        songMetadata.bestArcadeScoreValue = GetHighestArcadeScoreValue(songMetadata);
        currentSongBestScoreValue = songMetadata.bestScoreValue;
        currentSongBestScorePercent = songMetadata.bestScorePercent;
        currentSongBestArcadeScoreValue = songMetadata.bestArcadeScoreValue;

        SaveSongMetadata(songMetadata, GetMetadataPath(currentSongFileName), currentSongFileName);
    }

    private void SaveSongMetadata(SongMetadata metadata, string metadataPath, string songFileName)
    {
        if (metadata == null || string.IsNullOrEmpty(metadataPath))
            return;

        metadata.songFileName = songFileName;
        metadata.bestScoreValue = GetHighestTrackScoreValue(metadata);
        metadata.bestScorePercent = Mathf.Clamp(GetHighestTrackScore(metadata), 0f, 100f);
        metadata.bestArcadeScoreValue = GetHighestArcadeScoreValue(metadata);
        HeroScoreSummary heroSummary = GetHighestHeroTrackScoreSummary(metadata);

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
        string songDirectory = Path.GetDirectoryName(metadataPath);
        SongLibraryService.UpdateCachedMetadataSummary(
            songDirectory,
            metadata.favoriteInLibrary,
            metadata.bestScoreValue,
            metadata.bestScorePercent,
            heroSummary.scoreValue,
            heroSummary.percent,
            heroSummary.heartsRemaining,
            heroSummary.heartsTotal,
            metadata.bestArcadeScoreValue);
        ApplyCachedSummaryToKnownSongEntries(
            songDirectory,
            metadata.favoriteInLibrary,
            metadata.bestScoreValue,
            metadata.bestScorePercent,
            heroSummary.scoreValue,
            heroSummary.percent,
            heroSummary.heartsRemaining,
            heroSummary.heartsTotal,
            metadata.bestArcadeScoreValue);
    }

    private void ApplyCachedSummaryToKnownSongEntries(
        string songDirectory,
        bool favoriteInLibrary,
        int bestScoreValue,
        float bestScorePercent,
        int heroBestScoreValue,
        float heroBestScorePercent,
        int heroBestHeartsRemaining,
        int heroBestHeartsTotal,
        int bestArcadeScoreValue)
    {
        if (string.IsNullOrWhiteSpace(songDirectory))
            return;

        ApplyCachedSummaryToSongEntry(currentSongEntry, songDirectory, favoriteInLibrary, bestScoreValue, bestScorePercent, heroBestScoreValue, heroBestScorePercent, heroBestHeartsRemaining, heroBestHeartsTotal, bestArcadeScoreValue);
        ApplyCachedSummaryToSongEntry(pendingTrackSelectionSong, songDirectory, favoriteInLibrary, bestScoreValue, bestScorePercent, heroBestScoreValue, heroBestScorePercent, heroBestHeartsRemaining, heroBestHeartsTotal, bestArcadeScoreValue);
        for (int i = 0; i < availableSongs.Count; i++)
        {
            ApplyCachedSummaryToSongEntry(availableSongs[i], songDirectory, favoriteInLibrary, bestScoreValue, bestScorePercent, heroBestScoreValue, heroBestScorePercent, heroBestHeartsRemaining, heroBestHeartsTotal, bestArcadeScoreValue);
        }
    }

    private static void ApplyCachedSummaryToSongEntry(
        SongLibraryEntry entry,
        string songDirectory,
        bool favoriteInLibrary,
        int bestScoreValue,
        float bestScorePercent,
        int heroBestScoreValue,
        float heroBestScorePercent,
        int heroBestHeartsRemaining,
        int heroBestHeartsTotal,
        int bestArcadeScoreValue)
    {
        if (entry == null || !PathsMatch(entry.SongDirectory, songDirectory))
            return;

        entry.CachedFavoriteInLibrary = favoriteInLibrary;
        entry.CachedBestScoreValue = Mathf.Max(0, bestScoreValue);
        entry.CachedBestScorePercent = Mathf.Clamp(bestScorePercent, 0f, 100f);
        entry.CachedHeroBestScoreValue = Mathf.Max(0, heroBestScoreValue);
        entry.CachedHeroBestScorePercent = Mathf.Clamp(heroBestScorePercent, 0f, 100f);
        entry.CachedHeroBestHeartsRemaining = Mathf.Max(0, heroBestHeartsRemaining);
        entry.CachedHeroBestHeartsTotal = Mathf.Max(0, heroBestHeartsTotal);
        entry.CachedBestArcadeScoreValue = Mathf.Max(0, bestArcadeScoreValue);
    }

    private static bool PathsMatch(string a, string b)
    {
        return string.Equals(NormalizePathKey(a), NormalizePathKey(b), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePathKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string normalized = path.Replace('\\', '/').Trim();
        while (normalized.EndsWith("/", StringComparison.Ordinal))
            normalized = normalized.Substring(0, normalized.Length - 1);
        return normalized;
    }

    private static SongMetadata CloneSongMetadata(SongMetadata source)
    {
        if (source == null)
            return new SongMetadata();

        return new SongMetadata
        {
            songFileName = source.songFileName,
            favoriteInLibrary = source.favoriteInLibrary,
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
            selectedArcadeArrangementId = source.selectedArcadeArrangementId,
            selectedArcadeDifficulty = string.IsNullOrWhiteSpace(source.selectedArcadeDifficulty) ? ArcadeDifficulty.Expert.ToString() : source.selectedArcadeDifficulty,
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
            bestScoreValue = source.bestScoreValue,
            bestScorePercent = source.bestScorePercent,
            bestArcadeScoreValue = source.bestArcadeScoreValue,
            loopBookmarkScopes = source.loopBookmarkScopes != null
                ? source.loopBookmarkScopes
                    .Where(scope => scope != null)
                    .Select(scope => new LoopBookmarkScopeEntry
                    {
                        scopeKey = scope.scopeKey,
                        scopeDisplayName = scope.scopeDisplayName,
                        bookmarks = scope.bookmarks != null
                            ? scope.bookmarks
                                .Where(bookmark => bookmark != null)
                                .Select(bookmark => new LoopBookmarkEntry
                                {
                                    bookmarkId = bookmark.bookmarkId,
                                    name = bookmark.name,
                                    loopStartTime = bookmark.loopStartTime,
                                    loopEndTime = bookmark.loopEndTime,
                                    createdUtcTicks = bookmark.createdUtcTicks,
                                    updatedUtcTicks = bookmark.updatedUtcTicks
                                }).ToList()
                            : new List<LoopBookmarkEntry>()
                    }).ToList()
                : new List<LoopBookmarkScopeEntry>(),
            trackScores = source.trackScores != null
                ? source.trackScores.Select(score => new TrackScoreEntry
                {
                    partId = score.partId,
                    displayName = score.displayName,
                    bestScoreValue = score.bestScoreValue,
                    bestScorePercent = score.bestScorePercent,
                    heroBestScoreValue = score.heroBestScoreValue,
                    heroBestScorePercent = score.heroBestScorePercent,
                    heroBestHeartsRemaining = score.heroBestHeartsRemaining,
                    heroBestHeartsTotal = score.heroBestHeartsTotal
                }).ToList()
                : new List<TrackScoreEntry>(),
            arcadeScores = source.arcadeScores != null
                ? source.arcadeScores
                    .Where(score => score != null)
                    .Select(score => new ArcadeScoreEntry
                    {
                        arrangementId = score.arrangementId,
                        displayName = score.displayName,
                        difficulty = score.difficulty,
                        bestScorePercent = score.bestScorePercent,
                        bestAccuracyPercent = score.bestAccuracyPercent,
                        bestScoreValue = score.bestScoreValue,
                        heroBestScorePercent = score.heroBestScorePercent,
                        heroBestAccuracyPercent = score.heroBestAccuracyPercent,
                        heroBestScoreValue = score.heroBestScoreValue,
                        heroBestHeartsRemaining = score.heroBestHeartsRemaining,
                        heroBestHeartsTotal = score.heroBestHeartsTotal
                    }).ToList()
                : new List<ArcadeScoreEntry>(),
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
        RegisterBoolSetting("core.forceStandardTuning", "Settings", "Force Standard Tuning", "When ON, pitch validation uses E Standard so you can play songs that are not in E Standard without retuning your guitar. When OFF, tune your guitar to the song's required tuning.", () => forceStandardTuning, v => { forceStandardTuning = v; RefreshActiveTrackTuning(); });
        RegisterIntSetting("arcade.highwayLaneCount", "Rhythm Mode", "Rhythm Lanes", "Minimum number of lane columns used by the Rhythm highway.", 1, 8, 1, () => arcadeHighwayLaneCount, v => arcadeHighwayLaneCount = Mathf.Clamp(v, 1, 8));
        RegisterFloatSetting("arcade.hitWindowEarly", "Rhythm Mode", "Early Hit Window", "How far before the strike line a Rhythm input can hit.", 0.02f, 0.30f, 0.005f, () => arcadeHitWindowEarly, v => arcadeHitWindowEarly = Mathf.Max(0f, v));
        RegisterFloatSetting("arcade.hitWindowLate", "Rhythm Mode", "Late Hit Window", "How far after the strike line a Rhythm input can hit after the gem has visually passed.", 0.02f, 0.30f, 0.005f, () => arcadeHitWindowLate, v => arcadeHitWindowLate = Mathf.Max(0f, v));
        RegisterFloatSetting("arcade.noteSpawnZ", "Rhythm Mode", "Note Spawn Z", "How far back Rhythm notes first appear on the 3D highway.", 10f, 180f, 0.5f, () => arcadeNoteSpawnZ, v => arcadeNoteSpawnZ = Mathf.Max(StrikeLineZ + 1f, v));
        RegisterFloatSetting("arcade.resolvedHoldTime", "Rhythm Mode", "Resolved Hold Time", "How long hit Rhythm gems remain visible. Zero matches Clone Hero-style immediate disappearance.", 0f, 0.5f, 0.005f, () => arcadeResolvedHoldTime, v => arcadeResolvedHoldTime = Mathf.Max(0f, v));
        RegisterEnumSetting("arcade.controls.inputSource", "Rhythm Controls", "Input Source", "Selects which Rhythm input sources are live. Keyboard defaults use A S J K L with Space as the strum key.", new []{"Keyboard","Controller","Keyboard + Controller","MIDI","All"}, () => SerializeArcadeInputSource(arcadeInputSource), v => { arcadeInputSource = ParseArcadeInputSource(v); arcadeMidiInputEnabled = UsesArcadeMidiInput(); if (!UsesArcadeMidiInput()) StopArcadeMidiInput(); });
        RegisterEnumSetting("arcade.controls.controllerDevice", "Rhythm Controls", "Controller Device", "Selects which connected joystick slot Rhythm mode listens to. 'Any' matches every connected controller, while numbered slots map to Unity's joystick slots.", BuildArcadeControllerDeviceOptions(), () => SerializeArcadeControllerDevice(), v => arcadeControllerDeviceIndex = ParseArcadeControllerDevice(v));
        RegisterBoolSetting("arcade.controls.gamepadMode", "Rhythm Controls", "Gamepad Mode", "Clone Hero-style gamepad mode: each fret/button press acts like a strum for fretted notes. Open strum notes use the Open Button; open HOPO/tap notes can use release or the Open Button.", () => arcadeGamepadMode, v => arcadeGamepadMode = v);
        RegisterIntSetting("arcade.midiDeviceIndex", "Rhythm Controls", "MIDI Device", "Windows MIDI input device index used when the Rhythm input source includes MIDI.", 0, 16, 1, () => arcadeMidiInputDeviceIndex, v => { arcadeMidiInputDeviceIndex = v; StopArcadeMidiInput(); });
        RegisterBindingSetting("arcade.controls.keyboard.green", "Rhythm Controls", "Keyboard Green", "Clone Hero default keyboard green fret.", () => arcadeKeyboardGreen, v => arcadeKeyboardGreen = v, ArcadeBindingCaptureKind.Keyboard);
        RegisterBindingSetting("arcade.controls.keyboard.red", "Rhythm Controls", "Keyboard Red", "Clone Hero default keyboard red fret.", () => arcadeKeyboardRed, v => arcadeKeyboardRed = v, ArcadeBindingCaptureKind.Keyboard);
        RegisterBindingSetting("arcade.controls.keyboard.yellow", "Rhythm Controls", "Keyboard Yellow", "Clone Hero default keyboard yellow fret.", () => arcadeKeyboardYellow, v => arcadeKeyboardYellow = v, ArcadeBindingCaptureKind.Keyboard);
        RegisterBindingSetting("arcade.controls.keyboard.blue", "Rhythm Controls", "Keyboard Blue", "Clone Hero default keyboard blue fret.", () => arcadeKeyboardBlue, v => arcadeKeyboardBlue = v, ArcadeBindingCaptureKind.Keyboard);
        RegisterBindingSetting("arcade.controls.keyboard.orange", "Rhythm Controls", "Keyboard Orange", "Clone Hero default keyboard orange fret.", () => arcadeKeyboardOrange, v => arcadeKeyboardOrange = v, ArcadeBindingCaptureKind.Keyboard);
        RegisterBindingSetting("arcade.controls.keyboard.strumUp", "Rhythm Controls", "Keyboard Strum", "Primary keyboard strum key.", () => arcadeKeyboardStrumUp, v => arcadeKeyboardStrumUp = v, ArcadeBindingCaptureKind.Keyboard);
        RegisterBindingSetting("arcade.controls.keyboard.strumDown", "Rhythm Controls", "Keyboard Strum Alt", "Optional secondary keyboard strum key.", () => arcadeKeyboardStrumDown, v => arcadeKeyboardStrumDown = v, ArcadeBindingCaptureKind.Keyboard);
        RegisterBindingSetting("arcade.controls.keyboard.open", "Rhythm Controls", "Keyboard Open Button", "Optional keyboard open-button binding. Clone Hero's default keyboard layout leaves this unbound.", () => arcadeKeyboardOpen, v => arcadeKeyboardOpen = v, ArcadeBindingCaptureKind.Keyboard);
        RegisterActionSetting("arcade.controls.keyboard.reset", "Rhythm Controls", "Reset Keyboard Defaults", "Restores the default keyboard layout: A S J K L with Space as the strum key.", "RESET", () => { ResetArcadeKeyboardBindingsToDefaults(); SaveGlobalRuntimeSettingsMetadata(); });
        RegisterBindingSetting("arcade.controls.controller.green", "Rhythm Controls", "Controller Green", "Primary green fret button for controller or Clone Hero guitar input.", () => arcadeControllerGreen, v => arcadeControllerGreen = v, ArcadeBindingCaptureKind.Controller);
        RegisterBindingSetting("arcade.controls.controller.red", "Rhythm Controls", "Controller Red", "Primary red fret button for controller or Clone Hero guitar input.", () => arcadeControllerRed, v => arcadeControllerRed = v, ArcadeBindingCaptureKind.Controller);
        RegisterBindingSetting("arcade.controls.controller.yellow", "Rhythm Controls", "Controller Yellow", "Primary yellow fret button for controller or Clone Hero guitar input.", () => arcadeControllerYellow, v => arcadeControllerYellow = v, ArcadeBindingCaptureKind.Controller);
        RegisterBindingSetting("arcade.controls.controller.blue", "Rhythm Controls", "Controller Blue", "Primary blue fret button for controller or Clone Hero guitar input.", () => arcadeControllerBlue, v => arcadeControllerBlue = v, ArcadeBindingCaptureKind.Controller);
        RegisterBindingSetting("arcade.controls.controller.orange", "Rhythm Controls", "Controller Orange", "Primary orange fret button for controller or Clone Hero guitar input.", () => arcadeControllerOrange, v => arcadeControllerOrange = v, ArcadeBindingCaptureKind.Controller);
        RegisterBindingSetting("arcade.controls.controller.strumUp", "Rhythm Controls", "Controller Strum Up", "Controller or guitar strum-up input.", () => arcadeControllerStrumUp, v => arcadeControllerStrumUp = v, ArcadeBindingCaptureKind.Controller);
        RegisterBindingSetting("arcade.controls.controller.strumDown", "Rhythm Controls", "Controller Strum Down", "Controller or guitar strum-down input.", () => arcadeControllerStrumDown, v => arcadeControllerStrumDown = v, ArcadeBindingCaptureKind.Controller);
        RegisterBindingSetting("arcade.controls.controller.open", "Rhythm Controls", "Controller Open Button", "Optional open-button binding for gamepad-style or guitar controller play.", () => arcadeControllerOpen, v => arcadeControllerOpen = v, ArcadeBindingCaptureKind.Controller);
        RegisterActionSetting("arcade.controls.controller.reset", "Rhythm Controls", "Reset Controller Defaults", "Restores the Guitar Hero-style gamepad defaults: LT LB RB RT A with D-pad strum.", "RESET", () => { ResetArcadeControllerBindingsToDefaults(); SaveGlobalRuntimeSettingsMetadata(); });
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
        RegisterFloatSetting("fx.characterScale", "Visuals - Character", "Character Scale", "Scales the highway character up or down. Pixel-art scaling still snaps cleanly.", 0.5f, 3f, 0.05f, () => highwayCharacterScale, v => highwayCharacterScale = v);
        RegisterFloatSetting("fx.characterRigOffsetY", "Visuals - Character", "Character Rig Position Y", "Moves the entire character + portal rig up or down without changing the character's position relative to the portal.", -0.25f, 0.25f, 0.005f, () => highwayCharacterRigOffsetY, v => highwayCharacterRigOffsetY = v);
        RegisterFloatSetting("fx.characterOffsetX", "Visuals - Character", "Character Position X", "Moves the highway character left or right relative to the portal.", -0.25f, 0.25f, 0.005f, () => highwayCharacterOffsetX, v => highwayCharacterOffsetX = v);
        RegisterFloatSetting("fx.characterOffsetY", "Visuals - Character", "Character Position Y", "Moves the highway character up or down relative to the portal and automatically rebalances the fade/portal blend.", -0.20f, 0.20f, 0.005f, () => highwayCharacterOffsetY, v => highwayCharacterOffsetY = v);
        RegisterFloatSetting("fx.characterFadeSoftness", "Visuals - Character", "Character Fade Softness", "Controls how gradually the character fades into the portal.", 0.02f, 0.40f, 0.005f, () => highwayCharacterFadeSoftness, v => highwayCharacterFadeSoftness = v);
        RegisterBoolSetting("fx.characterMovementEnabled", "Visuals - Character", "Character Animation", "Enables the character idle motion, note bop, and miss movement.", () => highwayCharacterMovementEnabled, v => highwayCharacterMovementEnabled = v);
        RegisterBoolSetting("fx.characterMissColorEnabled", "Visuals - Character", "Character Color Change", "Enables the red miss flash/color change on the character.", () => highwayCharacterMissColorEnabled, v => highwayCharacterMissColorEnabled = v);
        RegisterBoolSetting("fx.characterMissParticlesEnabled", "Visuals - Character", "Character Particles", "Enables miss particles around the character.", () => highwayCharacterMissParticlesEnabled, v => highwayCharacterMissParticlesEnabled = v);
        RegisterBoolSetting("fx.characterPortalEnabled", "Visuals - Character", "Character Portal", "Shows or hides the portal behind the highway character.", () => highwayCharacterPortalEnabled, v => highwayCharacterPortalEnabled = v);
        RegisterBoolSetting("fx.characterPortalSwirlsEnabled", "Visuals - Character", "Portal Swirls", "Toggles the animated swirl energy inside the character portal.", () => highwayCharacterPortalSwirlsEnabled, v => highwayCharacterPortalSwirlsEnabled = v);
        RegisterFloatSetting("fx.characterPortalBodyOpacity", "Visuals - Character", "Portal Body Opacity", "Controls how solid the portal body appears behind the character.", 0.15f, 1f, 0.01f, () => highwayCharacterPortalBodyOpacity, v => highwayCharacterPortalBodyOpacity = Mathf.Clamp01(v));
        RegisterEnumSetting("fx.characterPortalEdgeColor", "Visuals - Character", "Portal Edge Color", "Selects the glowing edge color for the character portal.", HighwayCharacterPortalColorPresetOptions, () => SerializeHighwayCharacterPortalColorPreset(highwayCharacterPortalEdgeColor), value => highwayCharacterPortalEdgeColor = ParseHighwayCharacterPortalColorPreset(value));
        RegisterEnumSetting("fx.characterPortalSwirlColor", "Visuals - Character", "Portal Swirl Color", "Selects the swirl-energy color inside the character portal.", HighwayCharacterPortalColorPresetOptions, () => SerializeHighwayCharacterPortalColorPreset(highwayCharacterPortalSwirlColor), value => highwayCharacterPortalSwirlColor = ParseHighwayCharacterPortalColorPreset(value));
        RegisterFloatSetting("fx.rhythmHopoOutlineGap", "Visuals - Rhythm Notes", "HOPO Outline Gap", "Adds more dark spacing between the HOPO body and its outer accent so it stays readable on bright lanes like yellow.", 0f, 0.18f, 0.005f, () => rhythmHopoOutlineGap, v => rhythmHopoOutlineGap = Mathf.Max(0f, v));
        RegisterEnumSetting("fx.rhythmHopoAccentColor", "Visuals - Rhythm Notes", "HOPO Accent Color", "Switches the HOPO outer accent between the classic white edge and a darker glowing orange.", RhythmHopoAccentColorPresetOptions, () => SerializeRhythmHopoAccentColorPreset(rhythmHopoAccentColor), value => rhythmHopoAccentColor = ParseRhythmHopoAccentColorPreset(value));
        RegisterFloatSetting("mp.cameraOffsetX", "Visuals - Multiplayer", "Camera Offset X", "Moves the multiplayer camera left or right.", -20f, 20f, 0.05f, () => multiplayerHighwayCameraOffsetX, v => multiplayerHighwayCameraOffsetX = v);
        RegisterFloatSetting("mp.cameraOffsetY", "Visuals - Multiplayer", "Camera Offset Y", "Moves the multiplayer camera up or down.", -10f, 10f, 0.05f, () => multiplayerHighwayCameraOffsetY, v => multiplayerHighwayCameraOffsetY = v);
        RegisterFloatSetting("mp.cameraOffsetZ", "Visuals - Multiplayer", "Camera Offset Z", "Moves the multiplayer camera forward or backward.", -40f, 20f, 0.05f, () => multiplayerHighwayCameraOffsetZ, v => multiplayerHighwayCameraOffsetZ = v);
        RegisterFloatSetting("mp.cameraPitchOffset", "Visuals - Multiplayer", "Camera Pitch Offset", "Tilts the multiplayer camera up or down.", -25f, 25f, 0.25f, () => multiplayerHighwayCameraPitchOffset, v => multiplayerHighwayCameraPitchOffset = v);
        RegisterFloatSetting("mp.cameraFov", "Visuals - Multiplayer", "Camera FOV", "Controls multiplayer field of view. Lower values flatten perspective.", 30f, 90f, 0.5f, () => multiplayerHighwayCameraFieldOfView, v => multiplayerHighwayCameraFieldOfView = v);
        RegisterFloatSetting("mp.highwayHalfSpread", "Visuals - Multiplayer", "Highway Spread", "Controls how far apart the two multiplayer highways are. Lower values bring them closer together.", 0.08f, 0.38f, 0.005f, () => multiplayerHighwayHalfSpread, v => multiplayerHighwayHalfSpread = v);
        RegisterFloatSetting("mp.characterHorizontalOffset", "Visuals - Multiplayer", "Character Horizontal Offset", "Moves both multiplayer characters outward or inward symmetrically.", -0.15f, 0.15f, 0.005f, () => multiplayerCharacterHorizontalOffset, v => multiplayerCharacterHorizontalOffset = v);
        RegisterFloatSetting("mp.characterVerticalOffset", "Visuals - Multiplayer", "Character Vertical Offset", "Moves both multiplayer character rigs up or down together.", -0.15f, 0.15f, 0.005f, () => multiplayerCharacterVerticalOffset, v => multiplayerCharacterVerticalOffset = v);
        RegisterFloatSetting("mp.portalHorizontalOffset", "Visuals - Multiplayer", "Portal Horizontal Offset", "Moves both multiplayer portals inward or outward symmetrically relative to their characters.", -0.50f, 0.50f, 0.01f, () => multiplayerPortalHorizontalOffset, v => multiplayerPortalHorizontalOffset = v);
        RegisterFloatSetting("mp.portalVerticalOffset", "Visuals - Multiplayer", "Portal Vertical Offset", "Moves both multiplayer portals up or down relative to their characters.", -0.50f, 0.50f, 0.01f, () => multiplayerPortalVerticalOffset, v => multiplayerPortalVerticalOffset = v);
        RegisterFloatSetting("mp.portalWidthScale", "Visuals - Multiplayer", "Portal Width", "Scales the width of both multiplayer portals symmetrically.", 0.40f, 2.20f, 0.01f, () => multiplayerPortalWidthScale, v => multiplayerPortalWidthScale = v);
        RegisterFloatSetting("mp.scoreHorizontalOffset", "Visuals - Multiplayer", "Score Horizontal Offset", "Moves both multiplayer score blocks outward or inward symmetrically.", -1.00f, 1.00f, 0.01f, () => multiplayerScoreHorizontalOffset, v => multiplayerScoreHorizontalOffset = v);
        RegisterFloatSetting("mp.scoreVerticalOffset", "Visuals - Multiplayer", "Score Vertical Offset", "Moves both multiplayer score blocks up or down together.", -1.00f, 1.00f, 0.01f, () => multiplayerScoreVerticalOffset, v => multiplayerScoreVerticalOffset = v);
        RegisterFloatSetting("mp.comboBadgeHorizontalOffset", "Visuals - Multiplayer", "Combo Badge Horizontal Offset", "Moves both multiplayer combo badges symmetrically toward or away from the highways.", -1.25f, 1.25f, 0.01f, () => multiplayerComboBadgeHorizontalOffset, v => multiplayerComboBadgeHorizontalOffset = v);
        RegisterFloatSetting("mp.comboBadgeVerticalOffset", "Visuals - Multiplayer", "Combo Badge Vertical Offset", "Moves both multiplayer combo badges up or down together.", -1.25f, 1.25f, 0.01f, () => multiplayerComboBadgeVerticalOffset, v => multiplayerComboBadgeVerticalOffset = v);
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

    private void RegisterBindingSetting(string id, string section, string label, string tooltip, Func<KeyCode> getter, Action<KeyCode> setter, ArcadeBindingCaptureKind captureKind)
    {
        RegisterSetting(new RuntimeSettingDefinition
        {
            Id = id,
            Section = section,
            Label = label,
            Tooltip = tooltip,
            ValueType = "binding",
            Getter = () =>
            {
                if (string.Equals(activeArcadeBindingSettingId, id, StringComparison.OrdinalIgnoreCase))
                    return captureKind == ArcadeBindingCaptureKind.Controller ? "PRESS A CONTROLLER INPUT" : "PRESS A KEY";
                return SerializeArcadeBinding(getter());
            },
            Setter = value => setter(ParseArcadeBinding(value, getter())),
            Activator = () => BeginArcadeBindingCapture(id),
            BindingCaptureKind = captureKind
        });
    }

    private void RegisterActionSetting(string id, string section, string label, string tooltip, string valueLabel, Action activator)
    {
        RegisterSetting(new RuntimeSettingDefinition
        {
            Id = id,
            Section = section,
            Label = label,
            Tooltip = tooltip,
            ValueType = "action",
            Getter = () => string.IsNullOrWhiteSpace(valueLabel) ? "RUN" : valueLabel,
            Setter = null,
            Activator = activator
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

    private static string SerializeArcadeInputSource(ArcadeInputSourceMode source)
    {
        switch (source)
        {
            case ArcadeInputSourceMode.Keyboard:
                return "Keyboard";
            case ArcadeInputSourceMode.Controller:
                return "Controller";
            case ArcadeInputSourceMode.Midi:
                return "MIDI";
            case ArcadeInputSourceMode.All:
                return "All";
            default:
                return "Keyboard + Controller";
        }
    }

    private static List<string> BuildArcadeControllerDeviceOptions()
    {
        List<string> options = new List<string> { "Any Connected Controller" };
        string[] joystickNames = Input.GetJoystickNames();
        for (int i = 0; i < ArcadeControllerSlotCount; i++)
        {
            string deviceName = joystickNames != null && i < joystickNames.Length ? joystickNames[i] : string.Empty;
            string label = string.IsNullOrWhiteSpace(deviceName)
                ? $"Joystick {i + 1}"
                : $"Joystick {i + 1} ({deviceName.Trim()})";
            options.Add(label);
        }

        return options;
    }

    private string SerializeArcadeControllerDevice()
    {
        List<string> options = BuildArcadeControllerDeviceOptions();
        int index = Mathf.Clamp(arcadeControllerDeviceIndex, 0, options.Count - 1);
        return options[index];
    }

    private static int ParseArcadeControllerDevice(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("Any", StringComparison.OrdinalIgnoreCase))
            return 0;

        Match match = Regex.Match(value, @"Joystick\s+(\d+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            return Mathf.Clamp(parsed, 0, ArcadeControllerSlotCount);

        return 0;
    }

    private static ArcadeInputSourceMode ParseArcadeInputSource(string value)
    {
        if (string.Equals(value, "Keyboard", StringComparison.OrdinalIgnoreCase))
            return ArcadeInputSourceMode.Keyboard;
        if (string.Equals(value, "Controller", StringComparison.OrdinalIgnoreCase))
            return ArcadeInputSourceMode.Controller;
        if (string.Equals(value, "MIDI", StringComparison.OrdinalIgnoreCase))
            return ArcadeInputSourceMode.Midi;
        if (string.Equals(value, "All", StringComparison.OrdinalIgnoreCase))
            return ArcadeInputSourceMode.All;
        return ArcadeInputSourceMode.KeyboardAndController;
    }

    private static string SerializeArcadeBinding(KeyCode binding)
    {
        return binding == KeyCode.None ? "None" : NormalizeArcadeBinding(binding).ToString();
    }

    private static KeyCode ParseArcadeBinding(string value, KeyCode fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        if (string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
            return KeyCode.None;
        return Enum.TryParse(value, true, out KeyCode parsed) ? NormalizeArcadeBinding(parsed) : fallback;
    }

    private static KeyCode NormalizeArcadeBinding(KeyCode binding)
    {
        string name = binding.ToString();
        Match specificJoystickMatch = Regex.Match(name, @"^Joystick\d+Button(\d+)$", RegexOptions.CultureInvariant);
        if (specificJoystickMatch.Success && Enum.TryParse($"JoystickButton{specificJoystickMatch.Groups[1].Value}", out KeyCode normalized))
            return normalized;
        return binding;
    }

    private void ResetArcadeKeyboardBindingsToDefaults()
    {
        arcadeKeyboardGreen = KeyCode.A;
        arcadeKeyboardRed = KeyCode.S;
        arcadeKeyboardYellow = KeyCode.J;
        arcadeKeyboardBlue = KeyCode.K;
        arcadeKeyboardOrange = KeyCode.L;
        arcadeKeyboardStrumUp = KeyCode.Space;
        arcadeKeyboardStrumDown = KeyCode.None;
        arcadeKeyboardOpen = KeyCode.None;
        runtimeSettingsSnapshotDirty = true;
    }

    private void ResetArcadeControllerBindingsToDefaults()
    {
        arcadeControllerGreen = KeyCode.JoystickButton6;
        arcadeControllerRed = KeyCode.JoystickButton4;
        arcadeControllerYellow = KeyCode.JoystickButton5;
        arcadeControllerBlue = KeyCode.JoystickButton7;
        arcadeControllerOrange = KeyCode.JoystickButton0;
        arcadeControllerStrumUp = KeyCode.JoystickButton13;
        arcadeControllerStrumDown = KeyCode.JoystickButton14;
        arcadeControllerOpen = KeyCode.JoystickButton8;
        runtimeSettingsSnapshotDirty = true;
    }

    private void BeginArcadeBindingCapture(string settingId)
    {
        if (string.IsNullOrWhiteSpace(settingId) || !runtimeSettingById.TryGetValue(settingId, out RuntimeSettingDefinition definition) || definition.BindingCaptureKind == ArcadeBindingCaptureKind.None)
            return;

        activeArcadeBindingSettingId = settingId;
        activeArcadeBindingStartFrame = Time.frameCount;
        runtimeSettingsSnapshotDirty = true;
    }

    private void CancelArcadeBindingCapture()
    {
        if (string.IsNullOrEmpty(activeArcadeBindingSettingId))
            return;

        activeArcadeBindingSettingId = string.Empty;
        activeArcadeBindingStartFrame = -1;
        runtimeSettingsSnapshotDirty = true;
    }

    private bool HandleArcadeBindingCaptureInput()
    {
        if (string.IsNullOrEmpty(activeArcadeBindingSettingId))
            return false;

        if (Time.frameCount <= activeArcadeBindingStartFrame)
            return true;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelArcadeBindingCapture();
            return true;
        }

        if (Input.GetKeyDown(KeyCode.Delete))
        {
            ApplyRuntimeSettingValue(activeArcadeBindingSettingId, "None", saveMetadata: true);
            CancelArcadeBindingCapture();
            return true;
        }

        if (!runtimeSettingById.TryGetValue(activeArcadeBindingSettingId, out RuntimeSettingDefinition definition))
        {
            CancelArcadeBindingCapture();
            return true;
        }

        if (TryGetPressedArcadeBinding(definition.BindingCaptureKind, out KeyCode binding))
        {
            ApplyRuntimeSettingValue(activeArcadeBindingSettingId, SerializeArcadeBinding(binding), saveMetadata: true);
            CancelArcadeBindingCapture();
        }

        return true;
    }

    private bool TryGetPressedArcadeBinding(ArcadeBindingCaptureKind captureKind, out KeyCode binding)
    {
        Array allCodes = Enum.GetValues(typeof(KeyCode));
        for (int i = 0; i < allCodes.Length; i++)
        {
            KeyCode candidate = (KeyCode)allCodes.GetValue(i);
            if (candidate == KeyCode.None || !Input.GetKeyDown(candidate))
                continue;

            bool valid = captureKind == ArcadeBindingCaptureKind.Controller
                ? IsArcadeControllerBindingKeyCode(candidate)
                : IsArcadeKeyboardBindingKeyCode(candidate);
            if (!valid)
                continue;

            binding = NormalizeArcadeBinding(candidate);
            return true;
        }

        if (captureKind == ArcadeBindingCaptureKind.Controller)
        {
            RefreshSpecificControllerExtendedAxes();
            for (int slot = 1; slot <= ArcadeControllerSlotCount; slot++)
            {
                int slotIndex = slot - 1;
                if (previousSpecificControllerLeftTriggerAxes[slotIndex] < ControllerTriggerAxisPressThreshold &&
                    currentSpecificControllerLeftTriggerAxes[slotIndex] >= ControllerTriggerAxisPressThreshold)
                {
                    binding = KeyCode.JoystickButton6;
                    return true;
                }

                if (previousSpecificControllerRightTriggerAxes[slotIndex] < ControllerTriggerAxisPressThreshold &&
                    currentSpecificControllerRightTriggerAxes[slotIndex] >= ControllerTriggerAxisPressThreshold)
                {
                    binding = KeyCode.JoystickButton7;
                    return true;
                }
            }
        }

        binding = KeyCode.None;
        return false;
    }

    private static bool IsArcadeKeyboardBindingKeyCode(KeyCode code)
    {
        return code != KeyCode.None && !IsArcadeControllerBindingKeyCode(code) && !code.ToString().StartsWith("Mouse", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsArcadeControllerBindingKeyCode(KeyCode code)
    {
        string name = code.ToString();
        return name.StartsWith("JoystickButton", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(name, @"^Joystick\d+Button\d+$", RegexOptions.CultureInvariant);
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

    private static string SerializeHighwayCharacterPortalColorPreset(HighwayCharacterPortalColorPreset preset)
    {
        switch (preset)
        {
            case HighwayCharacterPortalColorPreset.Black:
                return "Black";
            default:
                return "Orange";
        }
    }

    private static HighwayCharacterPortalColorPreset ParseHighwayCharacterPortalColorPreset(string value)
    {
        if (string.Equals(value, "Black", StringComparison.OrdinalIgnoreCase))
            return HighwayCharacterPortalColorPreset.Black;

        return HighwayCharacterPortalColorPreset.Orange;
    }

    private static string SerializeRhythmHopoAccentColorPreset(RhythmHopoAccentColorPreset preset)
    {
        return preset == RhythmHopoAccentColorPreset.Orange ? "Orange" : "White";
    }

    private static RhythmHopoAccentColorPreset ParseRhythmHopoAccentColorPreset(string value)
    {
        if (string.Equals(value, "Orange", StringComparison.OrdinalIgnoreCase))
            return RhythmHopoAccentColorPreset.Orange;

        return RhythmHopoAccentColorPreset.White;
    }

    public float GetRhythmHopoOutlineGap()
    {
        return Mathf.Max(0f, rhythmHopoOutlineGap);
    }

    public Color GetRhythmHopoAccentColor()
    {
        switch (rhythmHopoAccentColor)
        {
            case RhythmHopoAccentColorPreset.Orange:
                return new Color(1f, 0.30f, 0.02f, 1f);
            default:
                return Color.white;
        }
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
            if (selectedGlobalSettingsTopIndex >= GlobalSettingsFirstCategoryTopIndex && selectedGlobalSettingsTopIndex <= GlobalSettingsLastCategoryTopIndex)
            {
                activeGlobalSettingsCategory = GetGlobalSettingsCategoryFromTopIndex(selectedGlobalSettingsTopIndex);
                selectedGlobalSettingsItemIndex = 0;
                return;
            }

            if (selectedGlobalSettingsTopIndex == GlobalSettingsResetTopIndex)
                ResetGlobalSettingsToDefaultsFromUi();

            return;
        }

        List<RuntimeSettingSnapshot> settings = GetActiveGlobalSettingsItems();
        if (settings.Count == 0)
            return;

        RuntimeSettingSnapshot setting = settings[Mathf.Clamp(selectedGlobalSettingsItemIndex, 0, settings.Count - 1)];
        if (setting == null)
            return;

        if (runtimeSettingById.TryGetValue(setting.id, out RuntimeSettingDefinition definition) && definition.Activator != null &&
            (string.Equals(setting.valueType, "binding", StringComparison.OrdinalIgnoreCase) || string.Equals(setting.valueType, "action", StringComparison.OrdinalIgnoreCase)))
        {
            definition.Activator();
        }
        else if (string.Equals(setting.valueType, "bool", StringComparison.OrdinalIgnoreCase))
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
                case GlobalSettingsResetTopIndex:
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

        if (string.Equals(setting.valueType, "binding", StringComparison.OrdinalIgnoreCase) || string.Equals(setting.valueType, "action", StringComparison.OrdinalIgnoreCase))
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
            case 5: return "Rhythm";
            case 6: return "Controls";
            case 7: return "Visuals";
            case 8: return "Multiplayer Visuals";
            default: return string.Empty;
        }
    }

    private static string CategorizeRuntimeSettingsSectionForMenu(RuntimeSettingSectionSnapshot section)
    {
        string normalizedTitle = section?.title?.ToLowerInvariant() ?? string.Empty;
        List<RuntimeSettingSnapshot> sectionSettings = section?.settings;

        if (normalizedTitle.Contains("multiplayer") || IsRuntimeSettingsSectionIdPrefix(sectionSettings, "mp."))
            return "Multiplayer Visuals";

        if (normalizedTitle.Contains("control") || IsRuntimeSettingsSectionIdPrefix(sectionSettings, "arcade.controls."))
            return "Controls";

        if (normalizedTitle.Contains("arcade") || IsRuntimeSettingsSectionIdPrefix(sectionSettings, "arcade."))
            return "Rhythm";

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
            settingId.StartsWith("arcade.highway", StringComparison.OrdinalIgnoreCase) ||
            settingId.StartsWith("arcade.noteSpawn", StringComparison.OrdinalIgnoreCase) ||
            settingId.StartsWith("arcade.resolvedHold", StringComparison.OrdinalIgnoreCase) ||
            settingId.StartsWith("highway.", StringComparison.OrdinalIgnoreCase) ||
            settingId.StartsWith("bg.", StringComparison.OrdinalIgnoreCase) ||
            settingId.StartsWith("layout.", StringComparison.OrdinalIgnoreCase) ||
            settingId.StartsWith("fx.", StringComparison.OrdinalIgnoreCase);

        if (requiresSectionRebuild)
            GenerateTabSections();

        if (requiresRendererRefresh)
            ResetActiveRendererContent();
    }

    private bool NormalizeLegacyRhythmRuntimeSettings(Dictionary<string, string> values)
    {
        if (values == null || values.Count == 0)
            return false;

        bool changed = false;

        changed |= TryMigrateLegacyCharacterSettings(values);

        changed |= ReplaceTransientRuntimeBindingPromptWithDefault(values, "arcade.controls.controller.green");
        changed |= ReplaceTransientRuntimeBindingPromptWithDefault(values, "arcade.controls.controller.red");
        changed |= ReplaceTransientRuntimeBindingPromptWithDefault(values, "arcade.controls.controller.yellow");
        changed |= ReplaceTransientRuntimeBindingPromptWithDefault(values, "arcade.controls.controller.blue");
        changed |= ReplaceTransientRuntimeBindingPromptWithDefault(values, "arcade.controls.controller.orange");
        changed |= ReplaceTransientRuntimeBindingPromptWithDefault(values, "arcade.controls.controller.strumUp");
        changed |= ReplaceTransientRuntimeBindingPromptWithDefault(values, "arcade.controls.controller.strumDown");
        changed |= ReplaceTransientRuntimeBindingPromptWithDefault(values, "arcade.controls.controller.open");

        if (TryMigrateLegacyKeyboardStrumDefaults(values))
            changed = true;

        if (TryMigrateLegacyControllerDefaults(values))
            changed = true;

        return changed;
    }

    private static bool TryMigrateLegacyCharacterSettings(Dictionary<string, string> values)
    {
        if (values == null || !values.TryGetValue("fx.characterAnimationsEnabled", out string legacyValue))
            return false;

        bool changed = false;
        if (!values.ContainsKey("fx.characterMovementEnabled"))
        {
            values["fx.characterMovementEnabled"] = legacyValue ?? "true";
            changed = true;
        }

        if (!values.ContainsKey("fx.characterMissColorEnabled"))
        {
            values["fx.characterMissColorEnabled"] = legacyValue ?? "true";
            changed = true;
        }

        if (!values.ContainsKey("fx.characterMissParticlesEnabled"))
        {
            values["fx.characterMissParticlesEnabled"] = "false";
            changed = true;
        }

        if (values.Remove("fx.characterAnimationsEnabled"))
            changed = true;

        return changed;
    }

    private bool ReplaceTransientRuntimeBindingPromptWithDefault(Dictionary<string, string> values, string settingId)
    {
        if (values == null || string.IsNullOrEmpty(settingId) || !values.TryGetValue(settingId, out string value) || string.IsNullOrWhiteSpace(value))
            return false;

        if (!value.StartsWith("PRESS ", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!runtimeSettingDefaultValues.TryGetValue(settingId, out string defaultValue))
            return false;

        values[settingId] = defaultValue ?? string.Empty;
        return true;
    }

    private static bool TryMigrateLegacyKeyboardStrumDefaults(Dictionary<string, string> values)
    {
        if (values == null)
            return false;

        bool matchesLegacyKeyboardDefaults =
            MatchesRuntimeSettingValue(values, "arcade.controls.keyboard.green", "A") &&
            MatchesRuntimeSettingValue(values, "arcade.controls.keyboard.red", "S") &&
            MatchesRuntimeSettingValue(values, "arcade.controls.keyboard.yellow", "J") &&
            MatchesRuntimeSettingValue(values, "arcade.controls.keyboard.blue", "K") &&
            MatchesRuntimeSettingValue(values, "arcade.controls.keyboard.orange", "L") &&
            MatchesRuntimeSettingValue(values, "arcade.controls.keyboard.strumUp", "UpArrow") &&
            MatchesRuntimeSettingValue(values, "arcade.controls.keyboard.strumDown", "DownArrow") &&
            MatchesRuntimeSettingValue(values, "arcade.controls.keyboard.open", "None");

        if (!matchesLegacyKeyboardDefaults)
            return false;

        values["arcade.controls.keyboard.strumUp"] = KeyCode.Space.ToString();
        values["arcade.controls.keyboard.strumDown"] = KeyCode.None.ToString();
        return true;
    }

    private static bool TryMigrateLegacyControllerDefaults(Dictionary<string, string> values)
    {
        if (values == null)
            return false;

        bool legacyPadLayout =
            (MatchesRuntimeSettingValue(values, "arcade.controls.controller.green", "JoystickButton0") ||
             MatchesRuntimeSettingValue(values, "arcade.controls.controller.green", "JoystickButton6")) &&
            MatchesRuntimeSettingValue(values, "arcade.controls.controller.red", "JoystickButton1") &&
            MatchesRuntimeSettingValue(values, "arcade.controls.controller.yellow", "JoystickButton2") &&
            MatchesRuntimeSettingValue(values, "arcade.controls.controller.blue", "JoystickButton3") &&
            MatchesRuntimeSettingValue(values, "arcade.controls.controller.orange", "JoystickButton4") &&
            MatchesRuntimeSettingValue(values, "arcade.controls.controller.strumUp", "JoystickButton5") &&
            MatchesRuntimeSettingValue(values, "arcade.controls.controller.strumDown", "JoystickButton6") &&
            MatchesRuntimeSettingValue(values, "arcade.controls.controller.open", "JoystickButton8");

        if (!legacyPadLayout)
            return false;

        values["arcade.controls.controller.green"] = KeyCode.JoystickButton6.ToString();
        values["arcade.controls.controller.red"] = KeyCode.JoystickButton4.ToString();
        values["arcade.controls.controller.yellow"] = KeyCode.JoystickButton5.ToString();
        values["arcade.controls.controller.blue"] = KeyCode.JoystickButton7.ToString();
        values["arcade.controls.controller.orange"] = KeyCode.JoystickButton0.ToString();
        values["arcade.controls.controller.strumUp"] = KeyCode.JoystickButton13.ToString();
        values["arcade.controls.controller.strumDown"] = KeyCode.JoystickButton14.ToString();
        return true;
    }

    private static bool MatchesRuntimeSettingValue(Dictionary<string, string> values, string settingId, string expectedValue)
    {
        return values.TryGetValue(settingId, out string currentValue) &&
               string.Equals(currentValue ?? string.Empty, expectedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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

            bool normalizedLegacyBindings = NormalizeLegacyRhythmRuntimeSettings(pendingGlobalRuntimeSettingValues);

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

            if (appliedMissingDefaults || normalizedLegacyBindings)
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

        if (gameplayMode == GuitarGameplayMode.Arcade && arcadeAudioSources != null)
        {
            for (int i = 0; i < arcadeAudioSources.Count; i++)
            {
                AudioSource source = arcadeAudioSources[i];
                if (source == null || source == backingTrackSource)
                    continue;

                if (!Mathf.Approximately(source.pitch, speed))
                    source.pitch = speed;
                if (!Mathf.Approximately(source.volume, volume))
                    source.volume = volume;
            }
        }
    }

    private void SyncAudioToSongTimer(bool playImmediately, bool forceSeek = false)
    {
        SongPlaybackAudioMode effectiveMode = GetEffectiveSongPlaybackAudioMode();
        float playbackSpeedScale = GetPlaybackSpeedScale();

        if (effectiveMode == SongPlaybackAudioMode.Generated)
        {
            if (backingTrackSource != null && backingTrackSource.isPlaying)
                backingTrackSource.Pause();
            PauseArcadeAudioSources(skipPrimary: true);

            generatedSongPlayer?.SyncTransport(audioSongTimer, playbackSpeedScale, playImmediately, forceSeek);
            return;
        }

        generatedSongPlayer?.SyncTransport(audioSongTimer, playbackSpeedScale, false, forceSeek);

        if (effectiveMode == SongPlaybackAudioMode.Muted)
        {
            if (backingTrackSource != null && backingTrackSource.isPlaying)
                backingTrackSource.Pause();
            PauseArcadeAudioSources(skipPrimary: true);
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

        if (gameplayMode == GuitarGameplayMode.Arcade)
            SyncArcadeAudioSources(timelineAudioTime, playImmediately, forceSeek);
    }

    private void SyncArcadeAudioSources(float timelineAudioTime, bool playImmediately, bool forceSeek)
    {
        if (arcadeAudioSources == null || arcadeAudioSources.Count <= 1)
            return;

        bool shouldBeSilentForCountdown = timelineAudioTime <= 0f;
        for (int i = 1; i < arcadeAudioSources.Count; i++)
        {
            AudioSource source = arcadeAudioSources[i];
            if (source == null || source.clip == null)
                continue;

            float audioTime = Mathf.Clamp(timelineAudioTime, 0f, source.clip.length);
            if (forceSeek || Mathf.Abs(source.time - audioTime) > 0.04f)
                source.time = audioTime;

            if (shouldBeSilentForCountdown || !playImmediately)
            {
                if (source.isPlaying)
                    source.Pause();
                continue;
            }

            if (!source.isPlaying && audioTime < source.clip.length)
                source.Play();
        }
    }

    private void PauseArcadeAudioSources(bool skipPrimary)
    {
        if (arcadeAudioSources == null)
            return;

        for (int i = skipPrimary ? 1 : 0; i < arcadeAudioSources.Count; i++)
        {
            AudioSource source = arcadeAudioSources[i];
            if (source != null && source.isPlaying)
                source.Pause();
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
