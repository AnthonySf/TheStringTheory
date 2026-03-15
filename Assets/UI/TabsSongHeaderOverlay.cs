using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class TabsSongHeaderOverlay
{
    private readonly GuitarBridgeServer owner;
    private readonly GameObject rootObject;
    private readonly UIDocument document;
    private readonly PanelSettings panelSettings;
    private readonly bool ownsPanelSettings;

    private readonly VisualElement songCard;
    private readonly Label songNameLabel;
    private readonly Label trackNameLabel;
    private readonly Label speedBadgeLabel;
    private readonly Label statusDotLabel;
    private readonly Label detectorStatusLabel;
    private readonly VisualElement scorePlate;
    private readonly Label scorePercentLabel;
    private readonly Label noteTallyLabel;
    private readonly VisualElement judgePopupLayer;

    private readonly List<JudgePopupEntry> activeJudgePopups = new List<JudgePopupEntry>();

    private sealed class SongSelectionRow
    {
        public Button button;
        public Label nameLabel;
        public Label scoreLabel;
    }

    private sealed class JudgePopupEntry
    {
        public Label label;
        public float startTime;
        public float startY;
        public float endY;
        public float duration;
    }

    private readonly VisualElement pauseOverlay;
    private readonly Label pauseTitleLabel;
    private readonly Label pauseHintLabel;
    private readonly Label pauseInfoLabel;
    private readonly Button loopButton;
    private readonly Slider speedSlider;
    private readonly Label speedValueLabel;

    private readonly VisualElement settingsOverlay;
    private readonly Label settingsTrackLabel;
    private readonly Label settingsOffsetLabel;
    private readonly Slider settingsOffsetSlider;
    private readonly Label settingsTabSpeedLabel;
    private readonly Slider settingsTabSpeedSlider;
    private readonly Label settingsStartDelayLabel;
    private readonly Slider settingsStartDelaySlider;

    private readonly VisualElement globalSettingsOverlay;
    private readonly ScrollView globalSettingsScrollView;
    private readonly Dictionary<string, VisualElement> globalSettingInputs = new Dictionary<string, VisualElement>();
    private readonly Dictionary<string, Label> globalSettingValueLabels = new Dictionary<string, Label>();

    private readonly VisualElement selectionOverlay;
    private readonly Label selectionSubtitleLabel;
    private readonly ScrollView selectionScrollView;
    private readonly List<SongSelectionRow> selectionRows = new List<SongSelectionRow>();


    private int lastScreenHeight = -1;
    private bool suppressCallbacks;
    private bool hasSeenSnapshot;
    private int lastResolvedCount;
    private int hitStreak;
    private float judgePopupFontSize = 82f;

    private readonly HashSet<int> scoredNoteIds = new HashSet<int>();
    private int scoreHits;
    private int scoreMisses;
    private float lastSongTime = -1f;
    private bool wasLoopEnabled;
    private string lastLoopSignature = string.Empty;
    private readonly FontDefinition bodyFontDefinition;
    private readonly FontDefinition titleFontDefinition;
    private string globalSettingsLayoutSignature = string.Empty;

    public TabsSongHeaderOverlay(GuitarBridgeServer owner)
    {
        this.owner = owner;

        rootObject = new GameObject("TabsSongHeaderUI");
        document = rootObject.AddComponent<UIDocument>();

        panelSettings = ResolvePanelSettings(out ownsPanelSettings);
        panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
        panelSettings.scale = 1f;
        panelSettings.targetDisplay = 0;
        panelSettings.sortingOrder = 220;
        EnsurePanelSettingsSupportAssets(panelSettings);
        document.panelSettings = panelSettings;

        Font fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        (Font bodyFont, Font titleFont) = ResolveUiFonts(fallbackFont);
        bodyFontDefinition = FontDefinition.FromFont(bodyFont);
        titleFontDefinition = FontDefinition.FromFont(titleFont);

        VisualElement root = document.rootVisualElement;
        root.style.flexGrow = 1f;
        root.style.paddingTop = 30f;
        root.style.paddingLeft = 34f;
        root.style.paddingRight = 34f;
        root.style.paddingBottom = 30f;
        root.style.backgroundColor = new Color(0.01f, 0.02f, 0.05f, 0.20f);

        songCard = new VisualElement();
        songCard.style.minWidth = 680f;
        songCard.style.maxWidth = 1160f;
        songCard.style.paddingLeft = 34f;
        songCard.style.paddingRight = 34f;
        songCard.style.paddingTop = 22f;
        songCard.style.paddingBottom = 22f;
        StyleCard(songCard, new Color(0.04f, 0.06f, 0.13f, 0.96f), radius: 18f);
        songCard.style.borderBottomWidth = 5f;
        songCard.style.borderBottomColor = new Color(0.16f, 0.12f, 0.42f, 0.98f);

        songNameLabel = CreateLabel("Song", 42f, Color.white, bold: true, useTitleFont: true);
        songNameLabel.style.marginBottom = 8f;
        songNameLabel.style.letterSpacing = 0.7f;

        trackNameLabel = CreateLabel("Lead Guitar", 26f, new Color(0.72f, 0.93f, 1f, 1f), bold: false);
        trackNameLabel.style.letterSpacing = 0.2f;

        VisualElement statusRow = new VisualElement();
        statusRow.style.flexDirection = FlexDirection.Row;
        statusRow.style.alignItems = Align.Center;
        statusRow.style.marginTop = 8f;

        speedBadgeLabel = CreateLabel("Speed 100%", 24f, new Color(1f, 0.96f, 0.76f, 1f), bold: true, useTitleFont: false);
        speedBadgeLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        speedBadgeLabel.style.letterSpacing = 0.45f;

        statusDotLabel = CreateLabel(" • ", 24f, new Color(0.78f, 0.86f, 1f, 0.85f), bold: true, useTitleFont: false);
        statusDotLabel.style.marginLeft = 8f;
        statusDotLabel.style.marginRight = 8f;

        detectorStatusLabel = CreateLabel("Instrument Detector: DISCONNECTED", 24f, new Color(1f, 0.47f, 0.53f, 1f), bold: true, useTitleFont: false);
        detectorStatusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        detectorStatusLabel.style.letterSpacing = 0.2f;

        statusRow.Add(speedBadgeLabel);
        statusRow.Add(statusDotLabel);
        statusRow.Add(detectorStatusLabel);

        scorePlate = new VisualElement();
        scorePlate.style.position = Position.Absolute;
        scorePlate.style.top = 16f;
        scorePlate.style.left = 0f;
        scorePlate.style.right = 0f;
        scorePlate.style.alignItems = Align.Center;
        scorePlate.style.justifyContent = Justify.Center;
        scorePlate.style.height = 122f;

        scorePercentLabel = CreateLabel("SCORE 100.0%", 46f, new Color(1f, 0.85f, 0.49f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: true);
        scorePercentLabel.style.letterSpacing = 0.7f;
        scorePercentLabel.style.paddingLeft = 20f;
        scorePercentLabel.style.paddingRight = 20f;

        noteTallyLabel = CreateLabel("HITS 0  •  MISS 0", 22f, new Color(0.79f, 0.93f, 1f, 0.96f), false, TextAnchor.MiddleCenter);
        noteTallyLabel.style.marginTop = 3f;
        noteTallyLabel.style.letterSpacing = 0.35f;
        noteTallyLabel.style.paddingLeft = 20f;
        noteTallyLabel.style.paddingRight = 20f;

        scorePlate.Add(scorePercentLabel);
        scorePlate.Add(noteTallyLabel);

        judgePopupLayer = new VisualElement();
        judgePopupLayer.style.position = Position.Absolute;
        judgePopupLayer.style.left = 0f;
        judgePopupLayer.style.right = 0f;
        judgePopupLayer.style.top = 0f;
        judgePopupLayer.style.bottom = 0f;
        judgePopupLayer.pickingMode = PickingMode.Ignore;
        pauseOverlay = CreateFullscreenOverlay();
        Label pauseStarsLabel = CreateLabel("★ ★ ★", 34f, new Color(1f, 0.74f, 0.32f, 0.95f), true, TextAnchor.MiddleCenter, useTitleFont: false);
        pauseStarsLabel.style.marginBottom = 8f;
        pauseStarsLabel.style.letterSpacing = 2.4f;
        pauseTitleLabel = CreateLabel("PAUSED", 132f, new Color(0.96f, 0.99f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: true);
        pauseTitleLabel.style.letterSpacing = 1.4f;
        pauseHintLabel = CreateLabel("SPACE Resume   •   ←/→ Seek   •   1/2 Marker", 34f, new Color(0.82f, 0.92f, 1f, 1f), false, TextAnchor.MiddleCenter);
        pauseHintLabel.style.marginTop = 10f;
        pauseHintLabel.style.marginBottom = 22f;

        VisualElement pauseCard = new VisualElement();
        pauseCard.style.width = 1200f;
        pauseCard.style.maxWidth = 1320f;
        pauseCard.style.paddingLeft = 32f;
        pauseCard.style.paddingRight = 32f;
        pauseCard.style.paddingTop = 28f;
        pauseCard.style.paddingBottom = 28f;
        StyleCard(pauseCard, new Color(0.04f, 0.07f, 0.14f, 0.96f), radius: 20f);

        pauseInfoLabel = CreateLabel("", 32f, new Color(0.90f, 0.96f, 1f, 1f));
        pauseInfoLabel.style.marginBottom = 12f;

        speedValueLabel = CreateLabel("Song Speed 100%", 34f, new Color(1f, 0.96f, 0.87f, 1f), true, useTitleFont: false);
        speedSlider = new Slider(1f, 200f);
        speedSlider.focusable = false;
        speedSlider.style.marginTop = 8f;
        speedSlider.style.marginBottom = 18f;
        speedSlider.RegisterValueChangedCallback(evt => { if (!suppressCallbacks) owner?.SetPlaybackSpeedPercentFromUi(evt.newValue); });

        VisualElement pauseButtons = new VisualElement();
        pauseButtons.style.flexDirection = FlexDirection.Row;
        pauseButtons.style.flexWrap = Wrap.Wrap;
        pauseButtons.style.marginTop = 8f;

        loopButton = CreateActionButton("Loop", () => owner?.ToggleLoopFromUi());
        Button songSelectButton = CreateActionButton("Library", () => owner?.OpenSongSelectionFromUi());
        Button songSettingsButton = CreateActionButton("Song Settings", () => owner?.OpenSongSettingsFromUi());
        Button globalSettingsButton = CreateActionButton("Settings", () => owner?.OpenGlobalSettingsFromUi());
        Button toneLabButton = CreateActionButton("Tone Lab", () => owner?.OpenToneLabFromUi());
        Button resumeButton = CreateActionButton("Resume", () => owner?.ResumePlaybackFromUi());

        foreach (Button button in new[] { loopButton, songSelectButton, songSettingsButton, globalSettingsButton, toneLabButton, resumeButton })
        {
            button.style.marginRight = 10f;
            button.style.marginTop = 8f;
            pauseButtons.Add(button);
        }

        pauseCard.Add(pauseInfoLabel);
        pauseCard.Add(speedValueLabel);
        pauseCard.Add(speedSlider);
        pauseCard.Add(pauseButtons);
        pauseOverlay.Add(pauseStarsLabel);
        pauseOverlay.Add(pauseTitleLabel);
        pauseOverlay.Add(pauseHintLabel);
        pauseOverlay.Add(pauseCard);

        settingsOverlay = CreateFullscreenOverlay();
        Label settingsTopTag = CreateLabel("◉ TUNE DECK ◉", 30f, new Color(1f, 0.73f, 0.33f, 0.95f), true, TextAnchor.MiddleCenter, useTitleFont: true);
        settingsTopTag.style.marginBottom = 6f;
        settingsTopTag.style.letterSpacing = 1.6f;

        Label settingsTitle = CreateLabel("SONG SETTINGS", 88f, Color.white, true, TextAnchor.MiddleCenter, useTitleFont: true);
        settingsTitle.style.marginBottom = 8f;
        settingsTitle.style.letterSpacing = 1.1f;
        Label settingsHelp = CreateLabel("Fine tune timing, offsets, and playback behavior.", 28f, new Color(0.82f, 0.92f, 1f, 0.96f), false, TextAnchor.MiddleCenter);
        settingsHelp.style.marginBottom = 18f;

        VisualElement settingsCard = new VisualElement();
        settingsCard.style.width = 1220f;
        settingsCard.style.maxWidth = 1360f;
        settingsCard.style.paddingLeft = 32f;
        settingsCard.style.paddingRight = 32f;
        settingsCard.style.paddingTop = 26f;
        settingsCard.style.paddingBottom = 26f;
        StyleCard(settingsCard, new Color(0.04f, 0.07f, 0.14f, 0.96f), radius: 20f);

        settingsTrackLabel = CreateLabel("Track", 34f, new Color(0.93f, 0.98f, 1f, 1f), true);
        settingsOffsetLabel = CreateLabel("Offset", 31f, new Color(0.84f, 0.95f, 1f, 1f));
        settingsOffsetSlider = new Slider(-2000f, 2000f);
        settingsOffsetSlider.focusable = false;
        settingsOffsetSlider.style.marginBottom = 14f;
        settingsOffsetSlider.RegisterValueChangedCallback(evt => { if (!suppressCallbacks) owner?.SetAudioOffsetMsFromUi(evt.newValue); });

        settingsTabSpeedLabel = CreateLabel("Tab Speed", 31f, new Color(0.84f, 0.95f, 1f, 1f));
        settingsTabSpeedSlider = new Slider(50f, 150f);
        settingsTabSpeedSlider.focusable = false;
        settingsTabSpeedSlider.style.marginBottom = 14f;
        settingsTabSpeedSlider.RegisterValueChangedCallback(evt => { if (!suppressCallbacks) owner?.SetTabSpeedOffsetPercentFromUi(evt.newValue); });

        settingsStartDelayLabel = CreateLabel("Start Delay", 31f, new Color(0.84f, 0.95f, 1f, 1f));
        settingsStartDelaySlider = new Slider(0f, 8f);
        settingsStartDelaySlider.focusable = false;
        settingsStartDelaySlider.RegisterValueChangedCallback(evt => { if (!suppressCallbacks) owner?.SetSongStartDelaySecondsFromUi(evt.newValue); });

        VisualElement settingsButtons = new VisualElement();
        settingsButtons.style.flexDirection = FlexDirection.Row;
        settingsButtons.style.flexWrap = Wrap.Wrap;
        settingsButtons.style.marginTop = 14f;

        Button prevTrackButton = CreateActionButton("Track -", () => owner?.MoveTrackSelectionFromUi(-1));
        Button nextTrackButton = CreateActionButton("Track +", () => owner?.MoveTrackSelectionFromUi(1));
        Button offsetScopeButton = CreateActionButton("Offset Scope", () => owner?.ToggleOffsetScopeFromUi());
        Button backPauseButton = CreateActionButton("Back", () => owner?.CloseSongSettingsFromUi());
        Button resumeFromSettingsButton = CreateActionButton("Resume", () => owner?.ResumePlaybackFromUi());

        foreach (Button button in new[] { prevTrackButton, nextTrackButton, offsetScopeButton, backPauseButton, resumeFromSettingsButton })
        {
            button.style.marginRight = 10f;
            button.style.marginTop = 8f;
            settingsButtons.Add(button);
        }

        settingsCard.Add(settingsTrackLabel);
        settingsCard.Add(settingsOffsetLabel);
        settingsCard.Add(settingsOffsetSlider);
        settingsCard.Add(settingsTabSpeedLabel);
        settingsCard.Add(settingsTabSpeedSlider);
        settingsCard.Add(settingsStartDelayLabel);
        settingsCard.Add(settingsStartDelaySlider);
        settingsCard.Add(settingsButtons);

        settingsOverlay.Add(settingsTopTag);
        settingsOverlay.Add(settingsTitle);
        settingsOverlay.Add(settingsHelp);
        settingsOverlay.Add(settingsCard);

        globalSettingsOverlay = CreateFullscreenOverlay();
        Label globalSettingsTopTag = CreateLabel("◉ PERFORMANCE SETUP ◉", 30f, new Color(1f, 0.73f, 0.33f, 0.95f), true, TextAnchor.MiddleCenter, useTitleFont: true);
        globalSettingsTopTag.style.marginBottom = 6f;
        globalSettingsTopTag.style.letterSpacing = 1.6f;

        Label globalSettingsTitle = CreateLabel("SETTINGS", 88f, Color.white, true, TextAnchor.MiddleCenter, useTitleFont: true);
        globalSettingsTitle.style.marginBottom = 8f;
        globalSettingsTitle.style.letterSpacing = 1.1f;
        Label globalSettingsHelp = CreateLabel("Gameplay and visual tuning for every song.", 28f, new Color(0.82f, 0.92f, 1f, 0.96f), false, TextAnchor.MiddleCenter);
        globalSettingsHelp.style.marginBottom = 18f;

        VisualElement globalSettingsCard = new VisualElement();
        globalSettingsCard.style.width = 1260f;
        globalSettingsCard.style.maxWidth = 1400f;
        globalSettingsCard.style.maxHeight = 760f;
        globalSettingsCard.style.paddingLeft = 24f;
        globalSettingsCard.style.paddingRight = 24f;
        globalSettingsCard.style.paddingTop = 20f;
        globalSettingsCard.style.paddingBottom = 20f;
        StyleCard(globalSettingsCard, new Color(0.04f, 0.07f, 0.14f, 0.96f), radius: 20f);

        globalSettingsScrollView = new ScrollView(ScrollViewMode.Vertical);
        globalSettingsScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
        globalSettingsScrollView.style.flexGrow = 1f;
        globalSettingsScrollView.style.maxHeight = 620f;
        globalSettingsCard.Add(globalSettingsScrollView);

        VisualElement globalButtons = new VisualElement();
        globalButtons.style.flexDirection = FlexDirection.Row;
        globalButtons.style.flexWrap = Wrap.Wrap;
        globalButtons.style.marginTop = 14f;

        Button globalBackButton = CreateActionButton("Back", () => owner?.CloseGlobalSettingsFromUi());
        Button globalResumeButton = CreateActionButton("Resume", () => owner?.ResumePlaybackFromUi());
        foreach (Button button in new[] { globalBackButton, globalResumeButton })
        {
            button.style.marginRight = 10f;
            button.style.marginTop = 8f;
            globalButtons.Add(button);
        }

        globalSettingsCard.Add(globalButtons);
        globalSettingsOverlay.Add(globalSettingsTopTag);
        globalSettingsOverlay.Add(globalSettingsTitle);
        globalSettingsOverlay.Add(globalSettingsHelp);
        globalSettingsOverlay.Add(globalSettingsCard);

        selectionOverlay = CreateFullscreenOverlay();
        Label selectionTopTag = CreateLabel("PRESS START TO PICK YOUR TRACK", 28f, new Color(1f, 0.73f, 0.33f, 0.95f), true, TextAnchor.MiddleCenter, useTitleFont: true);
        selectionTopTag.style.marginBottom = 6f;
        selectionTopTag.style.letterSpacing = 1f;

        Label selectionTitle = CreateLabel("TRACK LIBRARY", 90f, Color.white, true, TextAnchor.MiddleCenter, useTitleFont: true);
        selectionTitle.style.letterSpacing = 1.1f;
        selectionSubtitleLabel = CreateLabel("", 30f, new Color(0.84f, 0.94f, 1f, 0.98f), false, TextAnchor.MiddleCenter);
        selectionSubtitleLabel.style.marginBottom = 16f;

        VisualElement selectionCard = new VisualElement();
        selectionCard.style.width = 1120f;
        selectionCard.style.maxWidth = 1300f;
        selectionCard.style.paddingLeft = 28f;
        selectionCard.style.paddingRight = 28f;
        selectionCard.style.paddingTop = 20f;
        selectionCard.style.paddingBottom = 20f;
        StyleCard(selectionCard, new Color(0.04f, 0.07f, 0.14f, 0.96f), radius: 20f);

        selectionScrollView = new ScrollView(ScrollViewMode.Vertical);
        selectionScrollView.style.maxHeight = 620f;
        selectionScrollView.style.minHeight = 360f;
        selectionScrollView.style.marginTop = 4f;
        selectionScrollView.style.marginBottom = 4f;
        selectionScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
        selectionCard.Add(selectionScrollView);

        VisualElement selectionButtons = new VisualElement();
        selectionButtons.style.flexDirection = FlexDirection.Row;
        selectionButtons.style.flexWrap = Wrap.Wrap;
        selectionButtons.style.marginTop = 14f;

        Button upButton = CreateActionButton("Up", () => owner?.MoveSongSelectionFromUi(-1));
        Button downButton = CreateActionButton("Down", () => owner?.MoveSongSelectionFromUi(1));
        Button closeSelectionButton = CreateActionButton("Back", () => owner?.CloseSongSelectionFromUi());
        Button resumeSelectionButton = CreateActionButton("Resume", () => owner?.ResumePlaybackFromUi());

        foreach (Button button in new[] { upButton, downButton, closeSelectionButton, resumeSelectionButton })
        {
            button.style.marginRight = 10f;
            button.style.marginTop = 8f;
            selectionButtons.Add(button);
        }

        selectionCard.Add(selectionButtons);

        selectionOverlay.Add(selectionTopTag);
        selectionOverlay.Add(selectionTitle);
        selectionOverlay.Add(selectionSubtitleLabel);
        selectionOverlay.Add(selectionCard);

        ApplyFont(root, bodyFontDefinition);

        songCard.Add(songNameLabel);
        songCard.Add(trackNameLabel);
        songCard.Add(statusRow);
        root.Add(songCard);
        root.Add(scorePlate);
        root.Add(judgePopupLayer);
        root.Add(pauseOverlay);
        root.Add(settingsOverlay);
        root.Add(globalSettingsOverlay);
        root.Add(selectionOverlay);

        ApplyResponsiveSizing(force: true);
    }

    public void UpdateFromSnapshot(GuitarGameplaySnapshot snapshot)
    {
        ApplyResponsiveSizing(force: false);
        if (snapshot == null)
            return;

        string songName = "No song loaded";
        if (snapshot.availableSongNames != null && snapshot.selectedSongIndex >= 0 && snapshot.selectedSongIndex < snapshot.availableSongNames.Count)
            songName = snapshot.availableSongNames[snapshot.selectedSongIndex];

        string trackName = FormatTrackName(snapshot.selectedTrackDisplayName);
        songNameLabel.text = songName;
        trackNameLabel.text = trackName;

        int resolvedCount = 0;
        GameplayNoteState latestResolved = null;

        bool loopEnabled = snapshot.loopEnabled;
        string loopSignature = loopEnabled
            ? FormattableString.Invariant($"{snapshot.loopStartTime:F3}|{snapshot.loopEndTime:F3}|{snapshot.selectedLoopMarker}")
            : string.Empty;

        bool loopJustExited = wasLoopEnabled && !loopEnabled;
        bool loopJustEntered = !wasLoopEnabled && loopEnabled;
        bool loopDefinitionChanged = loopEnabled && wasLoopEnabled && loopSignature != lastLoopSignature;
        bool loopWrapped = loopEnabled && wasLoopEnabled && snapshot.songTime + 0.02f < lastSongTime;

        if (loopJustExited || loopJustEntered || loopDefinitionChanged || loopWrapped)
            ResetScoreCounters();

        if (snapshot.noteStates != null)
        {
            for (int i = 0; i < snapshot.noteStates.Count; i++)
            {
                GameplayNoteState noteState = snapshot.noteStates[i];
                if (noteState == null)
                    continue;

                bool inLoopWindow = !loopEnabled || IsNoteInsideLoopWindow(noteState.data.time, snapshot.loopStartTime, snapshot.loopEndTime);
                if (!inLoopWindow)
                    continue;

                if (!noteState.IsResolved)
                    continue;

                resolvedCount++;
                if (latestResolved == null || noteState.resolvedAt > latestResolved.resolvedAt)
                    latestResolved = noteState;

                int noteKey = noteState.data.id >= 0 ? noteState.data.id : i;
                if (scoredNoteIds.Contains(noteKey))
                    continue;

                scoredNoteIds.Add(noteKey);
                if (noteState.IsHit)
                    scoreHits++;
                else if (noteState.IsMissed)
                    scoreMisses++;
            }
        }

        int denominator;
        if (loopEnabled)
        {
            denominator = snapshot.noteStates?.Count(state => state != null && IsNoteInsideLoopWindow(state.data.time, snapshot.loopStartTime, snapshot.loopEndTime)) ?? 0;
        }
        else
        {
            denominator = scoreHits + scoreMisses;
        }

        float scorePercent = denominator > 0
            ? (100f * scoreHits / denominator)
            : 100f;
        scorePercentLabel.text = $"SCORE {scorePercent:F1}%";
        noteTallyLabel.text = $"HITS {scoreHits}  •  MISS {scoreMisses}";

        wasLoopEnabled = loopEnabled;
        lastLoopSignature = loopSignature;
        lastSongTime = snapshot.songTime;

        if (!hasSeenSnapshot)
        {
            hasSeenSnapshot = true;
            lastResolvedCount = resolvedCount;
        }
        else if (resolvedCount > lastResolvedCount && latestResolved != null)
        {
            bool success = latestResolved.IsHit;
            if (success)
                hitStreak++;
            else
                hitStreak = 0;

            SpawnJudgePopup(success, hitStreak);
            lastResolvedCount = resolvedCount;
        }
        else if (resolvedCount < lastResolvedCount)
        {
            lastResolvedCount = resolvedCount;
            hitStreak = 0;
        }

        UpdateJudgePopups();

        float speedPercent = Mathf.Clamp(snapshot.playbackSpeedPercent, 1f, 200f);
        speedBadgeLabel.text = $"Speed {speedPercent:F0}%";
        speedValueLabel.text = $"Song Speed {speedPercent:F0}%";

        bool detectorConnected = snapshot.noteDetectorConnected;
        detectorStatusLabel.text = detectorConnected
            ? "Instrument Detector: CONNECTED"
            : "Instrument Detector: DISCONNECTED";
        detectorStatusLabel.style.color = detectorConnected
            ? new Color(0.49f, 0.95f, 0.63f, 1f)
            : new Color(1f, 0.47f, 0.53f, 1f);

        suppressCallbacks = true;
        speedSlider.SetValueWithoutNotify(speedPercent);
        settingsOffsetSlider.SetValueWithoutNotify(Mathf.Clamp(snapshot.audioOffsetMs, -2000f, 2000f));
        settingsTabSpeedSlider.SetValueWithoutNotify(Mathf.Clamp(snapshot.tabSpeedOffsetPercent, 50f, 150f));
        settingsStartDelaySlider.SetValueWithoutNotify(Mathf.Clamp(snapshot.songStartDelaySeconds, 0f, 8f));
        suppressCallbacks = false;

        settingsTrackLabel.text = $"Track: {trackName}   •   Scope: {snapshot.offsetScopeLabel}";
        settingsOffsetLabel.text = $"Audio Offset  {snapshot.audioOffsetMs:F0} ms";
        settingsTabSpeedLabel.text = $"Tab Speed Offset  {snapshot.tabSpeedOffsetPercent:F0}%";
        settingsStartDelayLabel.text = $"Start Delay  {snapshot.songStartDelaySeconds:F2}s";

        bool showPause = snapshot.isPaused && !snapshot.showSongSettings && !snapshot.showSongSelection && !snapshot.showGlobalSettings;
        bool showSettings = snapshot.showSongSettings;
        bool showSelection = snapshot.showSongSelection;
        bool showGlobalSettings = snapshot.showGlobalSettings;

        pauseOverlay.style.display = showPause ? DisplayStyle.Flex : DisplayStyle.None;
        settingsOverlay.style.display = showSettings ? DisplayStyle.Flex : DisplayStyle.None;
        selectionOverlay.style.display = showSelection ? DisplayStyle.Flex : DisplayStyle.None;
        globalSettingsOverlay.style.display = showGlobalSettings ? DisplayStyle.Flex : DisplayStyle.None;

        if (showPause)
        {
            pauseInfoLabel.text =
                $"Loop: {(snapshot.loopEnabled ? "ON" : "OFF")}   Marker: {snapshot.selectedLoopMarker}   " +
                $"Audio: {(snapshot.hasBackingTrack ? (snapshot.isBackingTrackPlaying ? "Playing" : "Paused") : "Missing")}   " +
                $"Time: {snapshot.songTime:F2}s";
            loopButton.text = snapshot.loopEnabled ? "Loop: ON" : "Loop: OFF";
        }

        if (showSelection)
            UpdateSongSelectionRows(snapshot);

        if (showGlobalSettings)
            UpdateGlobalSettings(snapshot);
    }

    public void Dispose()
    {
        if (rootObject != null)
            UnityEngine.Object.Destroy(rootObject);

        if (ownsPanelSettings && panelSettings != null)
            UnityEngine.Object.Destroy(panelSettings);
    }

    private void UpdateSongSelectionRows(GuitarGameplaySnapshot snapshot)
    {
        int total = snapshot.availableSongNames?.Count ?? 0;
        selectionSubtitleLabel.text = $"{total} songs  •  Selected #{snapshot.selectedSongIndex + 1}";

        EnsureSongSelectionRows(total);

        for (int songIndex = 0; songIndex < selectionRows.Count; songIndex++)
        {
            SongSelectionRow row = selectionRows[songIndex];
            bool isSelected = songIndex == snapshot.selectedSongIndex;
            string name = snapshot.availableSongNames[songIndex];
            float score = (snapshot.availableSongScores != null && songIndex < snapshot.availableSongScores.Count)
                ? snapshot.availableSongScores[songIndex]
                : 0f;

            row.nameLabel.text = isSelected ? $"> {name}" : $"  {name}";
            row.scoreLabel.text = $"{score:F1}%";

            row.button.style.backgroundColor = isSelected
                ? new Color(0.42f, 0.18f, 0.52f, 0.98f)
                : new Color(0.08f, 0.15f, 0.24f, 0.93f);
            row.button.style.borderTopColor = isSelected ? new Color(1f, 0.54f, 0.80f, 1f) : new Color(0.36f, 0.58f, 1f, 0.75f);
            row.button.style.borderRightColor = row.button.style.borderTopColor;
            row.button.style.borderBottomColor = row.button.style.borderTopColor;
            row.button.style.borderLeftColor = row.button.style.borderTopColor;
        }

        if (snapshot.selectedSongIndex >= 0 && snapshot.selectedSongIndex < selectionRows.Count)
            selectionScrollView.ScrollTo(selectionRows[snapshot.selectedSongIndex].button);
    }

    private void EnsureSongSelectionRows(int count)
    {
        if (selectionRows.Count == count)
            return;

        selectionScrollView.Clear();
        selectionRows.Clear();

        for (int i = 0; i < count; i++)
        {
            int songIndex = i;
            Button rowButton = CreateActionButton(string.Empty, () => OnSongRowClicked(songIndex));
            rowButton.style.height = 76f;
            rowButton.style.marginTop = 6f;
            rowButton.style.marginBottom = 2f;
            rowButton.style.borderTopLeftRadius = 12f;
            rowButton.style.borderTopRightRadius = 12f;
            rowButton.style.borderBottomLeftRadius = 12f;
            rowButton.style.borderBottomRightRadius = 12f;

            VisualElement content = new VisualElement();
            content.style.flexDirection = FlexDirection.Row;
            content.style.justifyContent = Justify.SpaceBetween;
            content.style.alignItems = Align.Center;
            content.style.flexGrow = 1f;

            Label nameLabel = CreateLabel(string.Empty, 28f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: false);
            nameLabel.style.flexGrow = 1f;
            nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            Label scoreLabel = CreateLabel("0%", 26f, new Color(1f, 0.85f, 0.45f, 0.98f), true, TextAnchor.MiddleRight, useTitleFont: false);
            scoreLabel.style.minWidth = 130f;
            scoreLabel.style.unityTextAlign = TextAnchor.MiddleRight;

            content.Add(nameLabel);
            content.Add(scoreLabel);
            rowButton.Add(content);
            selectionScrollView.Add(rowButton);

            selectionRows.Add(new SongSelectionRow
            {
                button = rowButton,
                nameLabel = nameLabel,
                scoreLabel = scoreLabel
            });
        }
    }

    private void OnSongRowClicked(int rowIndex)
    {
        if (owner == null)
            return;

        owner.SelectSongByIndexFromUi(rowIndex);
    }


    private void UpdateGlobalSettings(GuitarGameplaySnapshot snapshot)
    {
        BuildGlobalSettingsUi(snapshot.runtimeSettingsSections);

        if (snapshot.runtimeSettingsSections == null)
            return;

        suppressCallbacks = true;
        foreach (RuntimeSettingSectionSnapshot section in snapshot.runtimeSettingsSections)
        {
            if (section?.settings == null)
                continue;

            foreach (RuntimeSettingSnapshot setting in section.settings)
            {
                if (setting == null || string.IsNullOrEmpty(setting.id) || !globalSettingInputs.TryGetValue(setting.id, out VisualElement input))
                    continue;

                if (input is Toggle toggle)
                    toggle.SetValueWithoutNotify(string.Equals(setting.value, "true", StringComparison.OrdinalIgnoreCase));
                else if (input is Slider slider)
                {
                    if (float.TryParse(setting.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                        slider.SetValueWithoutNotify(parsed);
                }
                else if (input is DropdownField dropdown)
                {
                    if (!string.IsNullOrEmpty(setting.value))
                        dropdown.SetValueWithoutNotify(setting.value);
                }

                if (globalSettingValueLabels.TryGetValue(setting.id, out Label valueLabel))
                    valueLabel.text = setting.value;
            }
        }
        suppressCallbacks = false;
    }

    private void BuildGlobalSettingsUi(List<RuntimeSettingSectionSnapshot> sections)
    {
        if (sections == null)
            return;

        string signature = BuildGlobalSettingsLayoutSignature(sections);
        if (signature == globalSettingsLayoutSignature && globalSettingInputs.Count > 0)
            return;

        globalSettingsLayoutSignature = signature;
        globalSettingsScrollView.Clear();
        globalSettingInputs.Clear();
        globalSettingValueLabels.Clear();

        foreach (RuntimeSettingSectionSnapshot section in sections)
        {
            if (section == null)
                continue;

            VisualElement sectionCard = new VisualElement();
            sectionCard.style.marginBottom = 12f;
            sectionCard.style.paddingLeft = 18f;
            sectionCard.style.paddingRight = 18f;
            sectionCard.style.paddingTop = 14f;
            sectionCard.style.paddingBottom = 14f;
            StyleCard(sectionCard, new Color(0.06f, 0.10f, 0.18f, 0.94f), 14f);

            Label sectionTitle = CreateLabel(section.title, 30f, new Color(1f, 0.87f, 0.62f, 1f), true);
            sectionTitle.style.marginBottom = 8f;
            sectionCard.Add(sectionTitle);

            if (section.settings != null)
            {
                foreach (RuntimeSettingSnapshot setting in section.settings)
                    sectionCard.Add(CreateGlobalSettingRow(setting));
            }

            globalSettingsScrollView.Add(sectionCard);
        }
    }

    private VisualElement CreateGlobalSettingRow(RuntimeSettingSnapshot setting)
    {
        VisualElement row = new VisualElement();
        row.style.marginBottom = 10f;
        row.style.paddingBottom = 10f;
        row.style.borderBottomWidth = 1f;
        row.style.borderBottomColor = new Color(0.28f, 0.42f, 0.65f, 0.36f);

        Label label = CreateLabel(setting.label, 25f, Color.white, true);
        label.tooltip = setting.tooltip;
        row.Add(label);

        Label help = CreateLabel(setting.tooltip, 20f, new Color(0.75f, 0.88f, 0.96f, 0.95f));
        help.style.marginTop = 2f;
        help.style.marginBottom = 6f;
        help.tooltip = setting.tooltip;
        row.Add(help);

        VisualElement input = null;
        if (string.Equals(setting.valueType, "bool", StringComparison.OrdinalIgnoreCase))
        {
            Toggle toggle = new Toggle();
            toggle.value = string.Equals(setting.value, "true", StringComparison.OrdinalIgnoreCase);
            toggle.RegisterValueChangedCallback(evt => { if (!suppressCallbacks) owner?.SetGlobalRuntimeSettingFromUi(setting.id, evt.newValue ? "true" : "false"); });
            input = toggle;
        }
        else if (string.Equals(setting.valueType, "enum", StringComparison.OrdinalIgnoreCase))
        {
            DropdownField dropdown = new DropdownField(setting.enumOptions ?? new List<string>(), setting.value);
            dropdown.RegisterValueChangedCallback(evt => { if (!suppressCallbacks) owner?.SetGlobalRuntimeSettingFromUi(setting.id, evt.newValue); });
            input = dropdown;
        }
        else
        {
            Slider slider = new Slider(setting.min, setting.max) { value = ParseFloat(setting.value, setting.min) };
            slider.RegisterValueChangedCallback(evt =>
            {
                if (suppressCallbacks)
                    return;

                float snapped = setting.step > 0.0001f ? Mathf.Round(evt.newValue / setting.step) * setting.step : evt.newValue;
                string serialized = string.Equals(setting.valueType, "int", StringComparison.OrdinalIgnoreCase)
                    ? Mathf.RoundToInt(snapped).ToString(CultureInfo.InvariantCulture)
                    : snapped.ToString("0.###", CultureInfo.InvariantCulture);
                owner?.SetGlobalRuntimeSettingFromUi(setting.id, serialized);
            });
            input = slider;
        }

        input.tooltip = setting.tooltip;
        input.style.marginBottom = 4f;
        row.Add(input);

        Label valueLabel = CreateLabel(setting.value, 18f, new Color(1f, 0.95f, 0.76f, 1f));
        row.Add(valueLabel);

        globalSettingInputs[setting.id] = input;
        globalSettingValueLabels[setting.id] = valueLabel;
        return row;
    }

    private static string BuildGlobalSettingsLayoutSignature(List<RuntimeSettingSectionSnapshot> sections)
    {
        if (sections == null)
            return string.Empty;

        List<string> tokens = new List<string>();
        foreach (RuntimeSettingSectionSnapshot section in sections)
        {
            tokens.Add(section?.title ?? string.Empty);
            if (section?.settings == null)
                continue;

            foreach (RuntimeSettingSnapshot setting in section.settings)
                tokens.Add($"{setting?.id}:{setting?.valueType}");
        }

        return string.Join("|", tokens);
    }

    private static float ParseFloat(string value, float fallback)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
    }

    private static bool IsNoteInsideLoopWindow(float noteTime, float loopStart, float loopEnd)
    {
        if (loopEnd <= loopStart)
            return false;

        return noteTime >= loopStart - 0.0001f && noteTime <= loopEnd + 0.0001f;
    }

    private void ResetScoreCounters()
    {
        scoreHits = 0;
        scoreMisses = 0;
        scoredNoteIds.Clear();
    }

    private void SpawnJudgePopup(bool success, int streak)
    {
        string text;
        if (success)
        {
            if (streak >= 8)
                text = "UNSTOPPABLE!";
            else if (streak >= 5)
                text = "ON FIRE!";
            else
            {
                string[] hitTexts = { "Great!", "Awesome!", "Perfect!", "Nice!" };
                text = hitTexts[UnityEngine.Random.Range(0, hitTexts.Length)];
            }
        }
        else
        {
            string[] missTexts = { "Miss!", "Oops!", "Late!" };
            text = missTexts[UnityEngine.Random.Range(0, missTexts.Length)];
        }

        Label popup = CreateLabel(text, judgePopupFontSize, success ? new Color(1f, 0.90f, 0.46f, 0.99f) : new Color(1f, 0.44f, 0.62f, 0.99f), true, TextAnchor.MiddleCenter, useTitleFont: false);
        popup.style.position = Position.Absolute;
        popup.style.left = 0f;
        popup.style.right = 0f;
        popup.style.unityTextAlign = TextAnchor.MiddleCenter;
        popup.style.letterSpacing = 1.2f;
        popup.style.opacity = 1f;
        popup.style.scale = new Scale(new Vector3(1.14f, 1.14f, 1f));

        float baseY = Mathf.Clamp(Screen.height * 0.34f, 240f, 420f);
        int layer = Mathf.Min(activeJudgePopups.Count, 4);
        float startY = baseY + layer * 24f;
        popup.style.top = startY;

        judgePopupLayer.Add(popup);
        activeJudgePopups.Add(new JudgePopupEntry
        {
            label = popup,
            startTime = Time.unscaledTime,
            startY = startY,
            endY = startY - 150f,
            duration = 1.05f
        });
    }

    private void UpdateJudgePopups()
    {
        float now = Time.unscaledTime;
        for (int i = activeJudgePopups.Count - 1; i >= 0; i--)
        {
            JudgePopupEntry popup = activeJudgePopups[i];
            if (popup == null || popup.label == null)
            {
                activeJudgePopups.RemoveAt(i);
                continue;
            }

            float elapsed = now - popup.startTime;
            if (elapsed >= popup.duration)
            {
                judgePopupLayer.Remove(popup.label);
                activeJudgePopups.RemoveAt(i);
                continue;
            }

            float t = Mathf.Clamp01(elapsed / popup.duration);
            float moveEase = 1f - Mathf.Pow(1f - t, 2.2f);
            popup.label.style.top = Mathf.Lerp(popup.startY, popup.endY, moveEase);
            popup.label.style.opacity = 1f - Mathf.Pow(t, 1.35f);

            float scale;
            if (t < 0.16f)
            {
                float popT = t / 0.16f;
                scale = Mathf.Lerp(1.22f, 1.00f, popT);
            }
            else
            {
                float settleT = (t - 0.16f) / 0.84f;
                scale = Mathf.Lerp(1.00f, 0.92f, settleT);
            }

            popup.label.style.scale = new Scale(new Vector3(scale, scale, 1f));
            popup.label.style.fontSize = judgePopupFontSize;
        }
    }

    private Label CreateLabel(string text, float size, Color color, bool bold = false, TextAnchor anchor = TextAnchor.MiddleLeft, bool useTitleFont = false)
    {
        Label label = new Label(text);
        label.style.fontSize = size;
        label.style.color = color;
        label.style.unityTextAlign = anchor;
        label.style.unityFontDefinition = useTitleFont ? titleFontDefinition : bodyFontDefinition;
        if (bold)
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
        return label;
    }

    private Button CreateActionButton(string text, Action onClick)
    {
        Button button = new Button(() => onClick?.Invoke()) { text = text };
        button.style.height = 64f;
        button.style.minWidth = 220f;
        button.style.paddingLeft = 18f;
        button.style.paddingRight = 18f;
        button.style.backgroundColor = new Color(0.08f, 0.15f, 0.24f, 0.96f);
        button.style.color = Color.white;
        button.style.fontSize = 28f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.borderTopLeftRadius = 12f;
        button.style.borderTopRightRadius = 12f;
        button.style.borderBottomLeftRadius = 12f;
        button.style.borderBottomRightRadius = 12f;
        button.style.borderTopWidth = 2f;
        button.style.borderRightWidth = 2f;
        button.style.borderBottomWidth = 6f;
        button.style.borderLeftWidth = 2f;
        button.style.borderTopColor = new Color(0.36f, 0.58f, 1f, 0.88f);
        button.style.borderRightColor = new Color(0.30f, 0.50f, 0.90f, 0.82f);
        button.style.borderBottomColor = new Color(0.20f, 0.36f, 0.65f, 0.96f);
        button.style.borderLeftColor = new Color(0.30f, 0.50f, 0.90f, 0.82f);
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.letterSpacing = 0.35f;
        button.style.marginBottom = 3f;
        button.style.unityFontDefinition = bodyFontDefinition;
        return button;
    }

    private static VisualElement CreateFullscreenOverlay()
    {
        VisualElement overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.right = 0f;
        overlay.style.top = 0f;
        overlay.style.bottom = 0f;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.FlexStart;
        overlay.style.paddingTop = 66f;
        overlay.style.backgroundColor = new Color(0.01f, 0.01f, 0.03f, 0.84f);
        return overlay;
    }

    private static void StyleCard(VisualElement element, Color backgroundColor, float radius = 16f)
    {
        element.style.backgroundColor = backgroundColor;
        element.style.borderTopLeftRadius = radius;
        element.style.borderTopRightRadius = radius;
        element.style.borderBottomLeftRadius = radius;
        element.style.borderBottomRightRadius = radius;
        element.style.borderTopWidth = 2f;
        element.style.borderBottomWidth = 1f;
        element.style.borderLeftWidth = 1f;
        element.style.borderRightWidth = 1f;
        element.style.borderTopColor = new Color(1f, 0.47f, 0.78f, 0.98f);
        element.style.borderBottomColor = new Color(0.34f, 0.31f, 0.68f, 0.93f);
        element.style.borderLeftColor = new Color(0.34f, 0.31f, 0.68f, 0.93f);
        element.style.borderRightColor = new Color(0.34f, 0.31f, 0.68f, 0.93f);
    }

    private static void ApplyFont(VisualElement root, FontDefinition font)
    {
        root.style.unityFontDefinition = font;
        foreach (VisualElement child in root.Children())
            ApplyFont(child, font);
    }

    private static (Font body, Font title) ResolveUiFonts(Font fallbackFont)
    {
        Font body = LoadProjectFont("Assets/UI/PixelArtFont.TTF");
        Font title = LoadProjectFont("Assets/UI/ArcadeFont.ttf");

        body ??= TryFindFontByName("pixelartfont", "pixel_art", "pixel");
        title ??= TryFindFontByName("arcadefont", "arcade", "shadow");

        body ??= fallbackFont;
        title ??= body ?? fallbackFont;

        return (body, title);
    }

    private static Font TryFindFontByName(params string[] keywords)
    {
        if (keywords == null || keywords.Length == 0)
            return null;

        Font[] availableFonts = Resources.FindObjectsOfTypeAll<Font>();
        Font best = null;

        foreach (Font font in availableFonts)
        {
            if (font == null || string.IsNullOrWhiteSpace(font.name))
                continue;

            string normalized = font.name.ToLowerInvariant();
            for (int i = 0; i < keywords.Length; i++)
            {
                string keyword = keywords[i];
                if (string.IsNullOrWhiteSpace(keyword))
                    continue;

                string normalizedKeyword = keyword.ToLowerInvariant();
                if (!normalized.Contains(normalizedKeyword))
                    continue;

                if (normalized == normalizedKeyword)
                    return font;

                best ??= font;
            }
        }

        return best;
    }

    private static Font LoadProjectFont(string assetPath)
    {
#if UNITY_EDITOR
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        return AssetDatabase.LoadAssetAtPath<Font>(assetPath);
#else
        return null;
#endif
    }

    private static string FormatTrackName(string trackDisplayName)
    {
        if (string.IsNullOrWhiteSpace(trackDisplayName))
            return "Default";

        int metricsIndex = trackDisplayName.IndexOf(" [", StringComparison.Ordinal);
        string trimmed = metricsIndex >= 0 ? trackDisplayName.Substring(0, metricsIndex) : trackDisplayName;

        if (trimmed.StartsWith("Auto (", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(")", StringComparison.Ordinal))
            trimmed = trimmed.Substring(6, trimmed.Length - 7);

        return trimmed.Trim();
    }

    private static PanelSettings ResolvePanelSettings(out bool ownsInstance)
    {
        PanelSettings existing = Resources.FindObjectsOfTypeAll<PanelSettings>()
            .Where(candidate => candidate != null)
            .OrderByDescending(candidate => candidate.themeStyleSheet != null)
            .ThenByDescending(candidate => candidate.textSettings != null)
            .ThenByDescending(candidate => candidate.name == "PanelSettings")
            .FirstOrDefault();

        if (existing != null)
        {
            ownsInstance = false;
            return existing;
        }

        ownsInstance = true;
        return ScriptableObject.CreateInstance<PanelSettings>();
    }

    private static void EnsurePanelSettingsSupportAssets(PanelSettings settings)
    {
        if (settings == null)
            return;

        if (settings.themeStyleSheet == null)
            settings.themeStyleSheet = Resources.FindObjectsOfTypeAll<ThemeStyleSheet>().FirstOrDefault();

        if (settings.textSettings == null)
            settings.textSettings = Resources.FindObjectsOfTypeAll<PanelTextSettings>().FirstOrDefault();
    }

    private void ApplyResponsiveSizing(bool force)
    {
        int screenHeight = Mathf.Max(1, Screen.height);
        if (!force && screenHeight == lastScreenHeight)
            return;

        lastScreenHeight = screenHeight;

        float songSize = Mathf.Clamp(screenHeight * 0.052f, 40f, 64f);
        float trackSize = Mathf.Clamp(screenHeight * 0.032f, 24f, 40f);
        float pauseSize = Mathf.Clamp(screenHeight * 0.135f, 112f, 170f);
        float bodySize = Mathf.Clamp(screenHeight * 0.036f, 30f, 50f);

        songNameLabel.style.fontSize = songSize;
        trackNameLabel.style.fontSize = trackSize;
        speedBadgeLabel.style.fontSize = bodySize * 0.66f;
        detectorStatusLabel.style.fontSize = bodySize * 0.66f;
        statusDotLabel.style.fontSize = bodySize * 0.66f;
        scorePercentLabel.style.fontSize = bodySize * 1.08f;
        noteTallyLabel.style.fontSize = bodySize * 0.50f;
        judgePopupFontSize = Mathf.Clamp(screenHeight * 0.072f, 64f, 108f);
        pauseTitleLabel.style.fontSize = pauseSize;
        pauseHintLabel.style.fontSize = bodySize * 0.85f;
        pauseInfoLabel.style.fontSize = bodySize * 0.80f;
        speedValueLabel.style.fontSize = bodySize * 0.85f;
        settingsTrackLabel.style.fontSize = bodySize * 0.90f;
        settingsOffsetLabel.style.fontSize = bodySize * 0.80f;
        settingsTabSpeedLabel.style.fontSize = bodySize * 0.80f;
        settingsStartDelayLabel.style.fontSize = bodySize * 0.80f;

        float buttonFontSize = Mathf.Clamp(screenHeight * 0.026f, 24f, 38f);
        float buttonHeight = Mathf.Clamp(screenHeight * 0.070f, 58f, 90f);

        foreach (SongSelectionRow row in selectionRows)
        {
            if (row == null)
                continue;

            if (row.nameLabel != null)
                row.nameLabel.style.fontSize = Mathf.Clamp(screenHeight * 0.030f, 22f, 34f);
            if (row.scoreLabel != null)
                row.scoreLabel.style.fontSize = Mathf.Clamp(screenHeight * 0.027f, 20f, 30f);
        }

        foreach (Button button in document.rootVisualElement.Query<Button>().ToList())
        {
            button.style.fontSize = buttonFontSize;
            if (button.style.height.value.value < buttonHeight)
                button.style.height = buttonHeight;
        }

        songCard.style.minWidth = Mathf.Clamp(Screen.width * 0.46f, 640f, 1400f);
    }
}
