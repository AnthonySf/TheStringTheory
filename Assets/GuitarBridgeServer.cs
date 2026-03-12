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
    private string logNotes = "--";

    [Header("Python UDP Config")]
    public int udpPort = 9000;
    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning;

    [Header("Debug")]
    public bool logSpawnedNotes = false;
    public bool useBuiltInDemoSong = false;
    public bool useDemoSongIfMidiMissing = true;

    [Header("Colors - Strings")]
    public Color[] stringColors = new Color[]
    {
        Color.red,
        Color.yellow,
        Color.cyan,
        new Color(1f, 0.5f, 0f), 
        Color.green,
        Color.magenta
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
    private string latestEventNotesText = "--";

    public int midiTrackIndex = -1;
    private int currentLoadedTrackIndex = -999;

    [Header("Backing Track")]
    public AudioSource backingTrackSource;
    public string backingTrackFileName = "song.mp3";
    [Min(0f)] public float defaultSongStartDelaySeconds = 2.0f;

    [Serializable]
    private class SongMetadata
    {
        public string songFileName;
        public float audioOffsetMs = 0f;
        public float tabSpeedOffsetPercent = 100f;
        public float songStartDelaySeconds = 2.0f;
    }

    private string currentSongFileName = "song.mp3";
    private bool hasBackingTrack;
    private bool showSongSettings;
    private float audioOffsetMs;
    private float tabSpeedOffsetPercent = 100f;
    private float songStartDelaySeconds = 2.0f;
    private SongMetadata songMetadata = new SongMetadata();
    private bool isLoadingBackingTrack;
    private string backingTrackLoadError = string.Empty;

    private void Start()
    {
        Application.targetFrameRate = 60;
        isRunning = true;
        BuildNoteIndices();
        StartUdpThread();
        EnsureBackingTrackSource();
        LoadTestSong();
        EnsureRenderer();
        SyncAudioToSongTimer(playImmediately: !isPaused);
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

        ApplyPlaybackSpeedToAudio();
        SyncAudioToSongTimer(playImmediately: !isPaused);

        if (midiTrackIndex != currentLoadedTrackIndex)
            LoadTestSong();

        if (!isPaused)
        {
            ParseUdpState();
            PruneHistory();
            UpdateGameplayStates();
        }
        else
        {
            latestPacketHadEvent = false;
        }

        EnsureRenderer();

        if (activeRenderer != null)
            activeRenderer.Render(BuildSnapshot());

        UpdateUiText();
    }

    private void HandlePauseControls()
    {
        if (Input.GetKeyDown(KeyCode.S) && renderMode == GuitarRenderMode.Tabs && (isPaused || showSongSettings))
            showSongSettings = !showSongSettings;

        if (showSongSettings)
        {
            HandleSongSettingsControls();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            isPaused = !isPaused;
            showSongSettings = false;
            SyncAudioToSongTimer(playImmediately: !isPaused);
        }

        if (!isPaused)
            return;

        if (IsSongSettingsClicked())
        {
            showSongSettings = true;
            return;
        }

        if (Input.GetKeyDown(KeyCode.T) || IsToneLabButtonClicked())
        {
            OpenOrFocusToneLab();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || IsLoopToggleClicked())
        {
            loopEnabled = !loopEnabled;
            if (loopEnabled && loopEndTime <= loopStartTime)
                loopEndTime = loopStartTime + 0.25f;
        }

        if (TryReadSpeedSliderPercent(out float sliderPercent))
            playbackSpeedPercent = sliderPercent;

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

        float seekDirection = 0f;
        if (Input.GetKey(KeyCode.LeftArrow))
            seekDirection -= 1f;
        if (Input.GetKey(KeyCode.RightArrow))
            seekDirection += 1f;

        if (Mathf.Approximately(seekDirection, 0f))
            return;

        SeekSongTime(songTimer + (seekDirection * pauseSeekStepSeconds * Time.deltaTime), true);
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

        if (TryReadOffsetSliderMs(out float sliderOffsetMs))
        {
            audioOffsetMs = sliderOffsetMs;
            SaveSongMetadata();
            SyncAudioToSongTimer(playImmediately: !isPaused);
        }

        if (TryReadTabSpeedOffsetSliderPercent(out float tabSpeedOffsetSlider))
        {
            tabSpeedOffsetPercent = tabSpeedOffsetSlider;
            SaveSongMetadata();
        }

        if (TryReadSongStartDelaySliderSeconds(out float songStartDelaySlider))
        {
            songStartDelaySeconds = songStartDelaySlider;
            SaveSongMetadata();
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            isPaused = false;
            SyncAudioToSongTimer(playImmediately: true);
        }

        float seekDirection = 0f;
        if (Input.GetKey(KeyCode.LeftArrow))
            seekDirection -= 1f;
        if (Input.GetKey(KeyCode.RightArrow))
            seekDirection += 1f;

        if (!Mathf.Approximately(seekDirection, 0f))
            SeekSongTime(songTimer + (seekDirection * pauseSeekStepSeconds * Time.deltaTime), true);
    }

    private void HandleLoopPlayback()
    {
        if (!loopEnabled || loopEndTime <= loopStartTime + 0.01f)
            return;

        if (songTimer < loopEndTime)
            return;

        SeekSongTime(loopStartTime, false);
    }

    private bool IsLoopToggleClicked()
    {
        if (renderMode != GuitarRenderMode.Tabs)
            return false;

        if (!Input.GetMouseButtonDown(0))
            return false;

        if (Camera.main == null)
            return false;

        Vector3 center = new Vector3(tabPanelCenterX, TabTopPanelY + (tabPanelHeight * 1.08f) - 0.62f, tabZDepth - 0.35f);
        Vector3 screenCenter = Camera.main.WorldToScreenPoint(center);
        float halfWidth = 180f;
        float halfHeight = 42f;

        Vector3 mouse = Input.mousePosition;
        return mouse.x >= screenCenter.x - halfWidth &&
               mouse.x <= screenCenter.x + halfWidth &&
               mouse.y >= screenCenter.y - halfHeight &&
               mouse.y <= screenCenter.y + halfHeight;
    }

    private bool IsSongSettingsClicked()
    {
        if (renderMode != GuitarRenderMode.Tabs)
            return false;

        if (!Input.GetMouseButtonDown(0) || Camera.main == null)
            return false;

        Vector3 center = new Vector3(tabPanelCenterX, TabTopPanelY + (tabPanelHeight * 1.08f) - 1.02f, tabZDepth - 0.35f);
        Vector3 screenCenter = Camera.main.WorldToScreenPoint(center);
        float halfWidth = 180f;
        float halfHeight = 26f;

        Vector3 mouse = Input.mousePosition;
        return mouse.x >= screenCenter.x - halfWidth &&
               mouse.x <= screenCenter.x + halfWidth &&
               mouse.y >= screenCenter.y - halfHeight &&
               mouse.y <= screenCenter.y + halfHeight;
    }

    private bool IsToneLabButtonClicked()
    {
        if (renderMode != GuitarRenderMode.Tabs)
            return false;

        if (!Input.GetMouseButtonDown(0) || Camera.main == null)
            return false;

        Vector3 center = new Vector3(tabPanelCenterX, TabTopPanelY + (tabPanelHeight * 1.08f) - 1.38f, tabZDepth - 0.35f);
        Vector3 screenCenter = Camera.main.WorldToScreenPoint(center);
        float halfWidth = 180f;
        float halfHeight = 26f;

        Vector3 mouse = Input.mousePosition;
        return mouse.x >= screenCenter.x - halfWidth &&
               mouse.x <= screenCenter.x + halfWidth &&
               mouse.y >= screenCenter.y - halfHeight &&
               mouse.y <= screenCenter.y + halfHeight;
    }

    private void OpenOrFocusToneLab()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        const string toneLabWindowTitle = "Tone Lab";
        string toneLabPath = @"C:\Users\Anthony\Desktop\guitarProject\ToneLab.py";

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
                Debug.LogWarning($"ToneLab script not found at '{toneLabPath}'.");
                return;
            }

            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{toneLabPath}\"",
                WorkingDirectory = Path.GetDirectoryName(toneLabPath),
                UseShellExecute = true,
                CreateNoWindow = false
            };

            System.Diagnostics.Process.Start(startInfo);
            StartCoroutine(FocusToneLabWindowWhenReady(toneLabWindowTitle));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to launch Tone Lab: {ex.Message}");
        }
#else
        Debug.LogWarning("Tone Lab launcher is currently implemented for Windows builds only.");
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
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

    private bool TryReadSpeedSliderPercent(out float speedPercent)
    {
        speedPercent = playbackSpeedPercent;

        if (renderMode != GuitarRenderMode.Tabs)
            return false;

        if (!Input.GetMouseButton(0))
            return false;

        if (Camera.main == null)
            return false;

        Vector3 center = new Vector3(tabPanelCenterX, TabTopPanelY + (tabPanelHeight * 1.08f) - 0.08f, tabZDepth - 0.35f);
        Vector3 screenCenter = Camera.main.WorldToScreenPoint(center);
        float halfWidth = 180f;
        float halfHeight = 26f;

        Vector3 mouse = Input.mousePosition;
        if (mouse.y < screenCenter.y - halfHeight || mouse.y > screenCenter.y + halfHeight)
            return false;

        float clampedX = Mathf.Clamp(mouse.x, screenCenter.x - halfWidth, screenCenter.x + halfWidth);
        float t = Mathf.InverseLerp(screenCenter.x - halfWidth, screenCenter.x + halfWidth, clampedX);
        speedPercent = Mathf.Lerp(1f, 200f, t);
        return true;
    }

    private bool TryReadOffsetSliderMs(out float offsetMs)
    {
        offsetMs = audioOffsetMs;

        if (!showSongSettings || renderMode != GuitarRenderMode.Tabs)
            return false;

        if (!Input.GetMouseButton(0) || Camera.main == null)
            return false;

        Vector3 center = new Vector3(tabPanelCenterX, TabTopPanelY + (tabPanelHeight * 1.08f) - 0.30f, tabZDepth - 0.35f);
        Vector3 screenCenter = Camera.main.WorldToScreenPoint(center);
        float halfWidth = 180f;
        float halfHeight = 26f;

        Vector3 mouse = Input.mousePosition;
        if (mouse.y < screenCenter.y - halfHeight || mouse.y > screenCenter.y + halfHeight)
            return false;

        float clampedX = Mathf.Clamp(mouse.x, screenCenter.x - halfWidth, screenCenter.x + halfWidth);
        float t = Mathf.InverseLerp(screenCenter.x - halfWidth, screenCenter.x + halfWidth, clampedX);
        offsetMs = Mathf.Lerp(-2000f, 2000f, t);
        return true;
    }

    private bool TryReadTabSpeedOffsetSliderPercent(out float tabSpeedPercent)
    {
        tabSpeedPercent = tabSpeedOffsetPercent;

        if (!showSongSettings || renderMode != GuitarRenderMode.Tabs)
            return false;

        if (!Input.GetMouseButton(0) || Camera.main == null)
            return false;

        Vector3 center = new Vector3(tabPanelCenterX, TabTopPanelY + (tabPanelHeight * 1.08f) - 0.74f, tabZDepth - 0.35f);
        Vector3 screenCenter = Camera.main.WorldToScreenPoint(center);
        float halfWidth = 180f;
        float halfHeight = 26f;

        Vector3 mouse = Input.mousePosition;
        if (mouse.y < screenCenter.y - halfHeight || mouse.y > screenCenter.y + halfHeight)
            return false;

        float clampedX = Mathf.Clamp(mouse.x, screenCenter.x - halfWidth, screenCenter.x + halfWidth);
        float t = Mathf.InverseLerp(screenCenter.x - halfWidth, screenCenter.x + halfWidth, clampedX);
        tabSpeedPercent = Mathf.Lerp(50f, 150f, t);
        return true;
    }

    private bool TryReadSongStartDelaySliderSeconds(out float delaySeconds)
    {
        delaySeconds = songStartDelaySeconds;

        if (!showSongSettings || renderMode != GuitarRenderMode.Tabs)
            return false;

        if (!Input.GetMouseButton(0) || Camera.main == null)
            return false;

        Vector3 center = new Vector3(tabPanelCenterX, TabTopPanelY + (tabPanelHeight * 1.08f) - 1.10f, tabZDepth - 0.35f);
        Vector3 screenCenter = Camera.main.WorldToScreenPoint(center);
        float halfWidth = 180f;
        float halfHeight = 26f;

        Vector3 mouse = Input.mousePosition;
        if (mouse.y < screenCenter.y - halfHeight || mouse.y > screenCenter.y + halfHeight)
            return false;

        float clampedX = Mathf.Clamp(mouse.x, screenCenter.x - halfWidth, screenCenter.x + halfWidth);
        float t = Mathf.InverseLerp(screenCenter.x - halfWidth, screenCenter.x + halfWidth, clampedX);
        delaySeconds = Mathf.Lerp(0f, 8f, t);
        return true;
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

        recentNoteEvents.Clear();
        latestDetectedPitches.Clear();
        latestEventNotesText = "--";
        latestNoteEventId = 0;
        latestPacketHadEvent = false;
        SyncAudioToSongTimer(playImmediately: !isPaused);
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
        string shaderName = isURP ? "Universal Render Pipeline/Lit" : "Standard";

        Shader shader = Shader.Find(shaderName);
        if (shader == null) shader = Shader.Find("Standard"); 

        Material m = new Material(shader);
        m.color = c;
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.SetColor("_EmissionColor", intensity > 0f ? c * Mathf.Pow(2f, intensity) : Color.black);
        return m;
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

        if (string.IsNullOrEmpty(logNotes) || logNotes == "--") return;

        if (logNotes.StartsWith("A|"))
        {
            string[] parts = logNotes.Split('|');
            if (parts.Length < 5) return;

            ParseNoteCsvIntoSet(parts[1], latestDetectedPitches);

            int.TryParse(parts[2], out int eventId);
            float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float eventAge);
            string eventCsv = parts[4];
            
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

    private GuitarGameplaySnapshot BuildSnapshot()
    {
        int currentSectionIndex = GetSectionIndex(songTimer);
        float sectionDuration = GetEffectiveTabSectionDuration();
        float sectionStart = currentSectionIndex * sectionDuration;
        float progress = Mathf.Clamp01((songTimer - sectionStart) / Mathf.Max(0.01f, sectionDuration));

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
            audioOffsetMs = audioOffsetMs,
            tabSpeedOffsetPercent = tabSpeedOffsetPercent,
            songStartDelaySeconds = songStartDelaySeconds,
            hasBackingTrack = hasBackingTrack,
            isBackingTrackPlaying = backingTrackSource != null && backingTrackSource.isPlaying,
            backingTrackTime = backingTrackSource != null ? backingTrackSource.time : 0f
        };
    }

    private void EnsureRenderer()
    {
        if (activeRenderer != null && activeRendererMode == renderMode) return;

        if (activeRenderer != null) activeRenderer.DisposeRenderer();

        if (renderMode == GuitarRenderMode.Tabs)
            activeRenderer = new GuitarTabsRenderer();
        else
            activeRenderer = new GuitarTabsRenderer(); 

        activeRenderer.Initialize(this, chartNotes, tabSections);
        activeRendererMode = renderMode;
    }

    private void UpdateUiText()
    {
        if (uiText == null) return;
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
            $"START DELAY: <color=cyan>{songStartDelaySeconds:F2}s</color>\n" +
            $"AUDIO SRC: <color=grey>{(string.IsNullOrEmpty(backingTrackLoadError) ? currentSongFileName : backingTrackLoadError)}</color>";
    }

    // =========================================================
    // SONG LOADING AND TAB GENERATION
    // =========================================================
    private void LoadTestSong()
    {
        currentLoadedTrackIndex = midiTrackIndex;
        latestDetectedPitches.Clear();
        recentNoteEvents.Clear();
        latestNoteEventId = 0;
        songTimer = 0f;
        audioSongTimer = 0f;
        isPaused = false;

        float sectionDuration = GetEffectiveTabSectionDuration();
        loopStartTime = Mathf.Max(0.2f, sectionDuration * 0.40f);
        loopEndTime = Mathf.Max(loopStartTime + 0.5f, sectionDuration * 0.60f);
        loopEnabled = false;
        selectedLoopMarker = 1;
        playbackSpeedPercent = 100f;
        showSongSettings = false;
        tabSpeedOffsetPercent = 100f;

        InitializeSongMetadataAndAudio();

        List<NoteData> loadedNotes = null;

        // 1. Try MusicXML first so guitar techniques can be imported when available.
        if (!useBuiltInDemoSong)
        {
            try
            {
                string xmlPath = System.IO.Path.Combine(Application.dataPath, "song.musicxml");
                string xmlFallbackPath = System.IO.Path.Combine(Application.dataPath, "song.xml");

                if (System.IO.File.Exists(xmlPath))
                {
                    loadedNotes = MusicXmlLoader.LoadMusicXmlSong(xmlPath, midiTrackIndex);
                    Debug.Log($"MusicXML load attempt: {xmlPath}");
                }
                else if (System.IO.File.Exists(xmlFallbackPath))
                {
                    loadedNotes = MusicXmlLoader.LoadMusicXmlSong(xmlFallbackPath, midiTrackIndex);
                    Debug.Log($"MusicXML load attempt: {xmlFallbackPath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("MusicXmlLoader Error: " + e.Message);
            }

            if (loadedNotes == null || loadedNotes.Count == 0)
            {
                try
                {
                    string path = System.IO.Path.Combine(Application.dataPath, "song.mid");
                    loadedNotes = MidiLoader.LoadMidiSong(path, midiTrackIndex);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("MidiLoader Error: " + e.Message);
                }
            }
        }

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

        songTimer = -songStartDelaySeconds;
        audioSongTimer = -songStartDelaySeconds;
        ApplyPlaybackSpeedToAudio();
        SyncAudioToSongTimer(playImmediately: !isPaused);
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

        string songPath = Path.Combine(Application.dataPath, backingTrackFileName);
        currentSongFileName = Path.GetFileName(songPath);

        songMetadata = LoadSongMetadata(currentSongFileName);
        audioOffsetMs = songMetadata.audioOffsetMs;
        tabSpeedOffsetPercent = Mathf.Clamp(songMetadata.tabSpeedOffsetPercent <= 0f ? 100f : songMetadata.tabSpeedOffsetPercent, 50f, 150f);
        songStartDelaySeconds = Mathf.Clamp(songMetadata.songStartDelaySeconds <= 0f ? defaultSongStartDelaySeconds : songMetadata.songStartDelaySeconds, 0f, 8f);

        backingTrackLoadError = string.Empty;
        isLoadingBackingTrack = false;

        if (backingTrackSource.clip != null)
        {
            hasBackingTrack = true;
            return;
        }

        AudioClip clip = Resources.Load<AudioClip>(Path.GetFileNameWithoutExtension(backingTrackFileName));
        if (clip != null)
        {
            backingTrackSource.clip = clip;
            hasBackingTrack = true;
            return;
        }

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

    private SongMetadata LoadSongMetadata(string songFileName)
    {
        SongMetadata data = new SongMetadata { songFileName = songFileName, audioOffsetMs = 0f, tabSpeedOffsetPercent = 100f, songStartDelaySeconds = defaultSongStartDelaySeconds };
        string path = GetMetadataPath(songFileName);

        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                SongMetadata loaded = JsonUtility.FromJson<SongMetadata>(json);
                if (loaded != null)
                    data = loaded;
            }
            else
            {
                File.WriteAllText(path, JsonUtility.ToJson(data, true));
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load metadata {path}: {ex.Message}");
        }

        return data;
    }

    private void SaveSongMetadata()
    {
        if (string.IsNullOrEmpty(currentSongFileName))
            return;

        songMetadata.songFileName = currentSongFileName;
        songMetadata.audioOffsetMs = audioOffsetMs;
        songMetadata.tabSpeedOffsetPercent = tabSpeedOffsetPercent;
        songMetadata.songStartDelaySeconds = songStartDelaySeconds;

        try
        {
            File.WriteAllText(GetMetadataPath(currentSongFileName), JsonUtility.ToJson(songMetadata, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to save song metadata: {ex.Message}");
        }
    }

    private string GetMetadataPath(string songFileName)
    {
        string safeName = Regex.Replace(Path.GetFileNameWithoutExtension(songFileName), "[^a-zA-Z0-9_-]", "_");
        return Path.Combine(Application.persistentDataPath, $"{safeName}.metadata.json");
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