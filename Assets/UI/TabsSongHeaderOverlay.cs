using System;
using System.Linq;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

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
    private readonly VisualElement scorePlate;
    private readonly Label scorePercentLabel;
    private readonly Label noteTallyLabel;
    private readonly Label judgePopupLabel;

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

    private readonly VisualElement selectionOverlay;
    private readonly Label selectionSubtitleLabel;
    private readonly Button[] selectionRowButtons;

    private readonly Label marqueeLabel;
    private readonly Label vibeLabel;

    private int lastScreenHeight = -1;
    private int currentSongListScrollOffset;
    private bool suppressCallbacks;
    private bool hasSeenSnapshot;
    private int lastResolvedCount;
    private float judgePopupStartTime = -1f;
    private const float JudgePopupDuration = 0.65f;

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

        Font dynamicFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        VisualElement root = document.rootVisualElement;
        root.style.flexGrow = 1f;
        root.style.paddingTop = 30f;
        root.style.paddingLeft = 34f;
        root.style.paddingRight = 34f;
        root.style.paddingBottom = 30f;
        root.style.backgroundColor = new Color(0.01f, 0.02f, 0.05f, 0.20f);

        VisualElement hudStripe = new VisualElement();
        hudStripe.style.height = 8f;
        hudStripe.style.width = 480f;
        hudStripe.style.marginBottom = 12f;
        hudStripe.style.backgroundColor = new Color(1f, 0.32f, 0.69f, 0.80f);
        hudStripe.style.borderTopLeftRadius = 999f;
        hudStripe.style.borderTopRightRadius = 999f;
        hudStripe.style.borderBottomLeftRadius = 999f;
        hudStripe.style.borderBottomRightRadius = 999f;

        marqueeLabel = CreateLabel("★  ★  ★  ARCADE SESSION  ★  ★  ★", 24f, new Color(1f, 0.82f, 0.49f, 1f), true);
        marqueeLabel.style.marginBottom = 6f;
        marqueeLabel.style.letterSpacing = 0.8f;

        vibeLabel = CreateLabel("NEON RHYTHM // LIVE INPUT // HIGH SCORE ENERGY", 19f, new Color(0.64f, 0.87f, 1f, 0.95f), false);
        vibeLabel.style.marginBottom = 16f;
        vibeLabel.style.letterSpacing = 0.5f;

        songCard = new VisualElement();
        songCard.style.minWidth = 680f;
        songCard.style.maxWidth = 1160f;
        songCard.style.paddingLeft = 34f;
        songCard.style.paddingRight = 34f;
        songCard.style.paddingTop = 22f;
        songCard.style.paddingBottom = 22f;
        StyleCard(songCard, new Color(0.04f, 0.06f, 0.13f, 0.94f), radius: 20f);

        songNameLabel = CreateLabel("Song", 42f, Color.white, bold: true);
        songNameLabel.style.marginBottom = 8f;
        songNameLabel.style.letterSpacing = 0.7f;

        trackNameLabel = CreateLabel("Lead Guitar", 26f, new Color(0.72f, 0.93f, 1f, 1f), bold: false);
        trackNameLabel.style.letterSpacing = 0.2f;

        speedBadgeLabel = CreateLabel("SPEED 100%", 24f, new Color(1f, 0.96f, 0.76f, 1f), bold: true);
        speedBadgeLabel.style.position = Position.Absolute;
        speedBadgeLabel.style.right = 34f;
        speedBadgeLabel.style.top = 32f;
        speedBadgeLabel.style.paddingLeft = 20f;
        speedBadgeLabel.style.paddingRight = 20f;
        speedBadgeLabel.style.paddingTop = 10f;
        speedBadgeLabel.style.paddingBottom = 10f;
        speedBadgeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        StyleCard(speedBadgeLabel, new Color(0.22f, 0.10f, 0.28f, 0.95f), radius: 999f);

        scorePlate = new VisualElement();
        scorePlate.style.position = Position.Absolute;
        scorePlate.style.top = 16f;
        scorePlate.style.left = 0f;
        scorePlate.style.right = 0f;
        scorePlate.style.alignItems = Align.Center;
        scorePlate.style.justifyContent = Justify.Center;
        scorePlate.style.height = 90f;

        VisualElement scorePlateCard = new VisualElement();
        scorePlateCard.style.minWidth = 380f;
        scorePlateCard.style.paddingLeft = 24f;
        scorePlateCard.style.paddingRight = 24f;
        scorePlateCard.style.paddingTop = 10f;
        scorePlateCard.style.paddingBottom = 8f;
        StyleCard(scorePlateCard, new Color(0.06f, 0.07f, 0.20f, 0.92f), radius: 999f);

        scorePercentLabel = CreateLabel("SCORE 100.0%", 38f, new Color(1f, 0.85f, 0.49f, 1f), true, TextAnchor.MiddleCenter);
        scorePercentLabel.style.letterSpacing = 0.7f;

        noteTallyLabel = CreateLabel("HITS 0  •  MISS 0", 21f, new Color(0.79f, 0.93f, 1f, 0.96f), false, TextAnchor.MiddleCenter);
        noteTallyLabel.style.marginTop = 2f;
        noteTallyLabel.style.letterSpacing = 0.35f;

        scorePlateCard.Add(scorePercentLabel);
        scorePlateCard.Add(noteTallyLabel);
        scorePlate.Add(scorePlateCard);

        judgePopupLabel = CreateLabel("SUCCESS", 72f, new Color(1f, 0.82f, 0.36f, 0.98f), true, TextAnchor.MiddleCenter);
        judgePopupLabel.style.position = Position.Absolute;
        judgePopupLabel.style.top = 230f;
        judgePopupLabel.style.left = 0f;
        judgePopupLabel.style.right = 0f;
        judgePopupLabel.style.paddingTop = 10f;
        judgePopupLabel.style.paddingBottom = 10f;
        judgePopupLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        judgePopupLabel.style.letterSpacing = 1.6f;
        judgePopupLabel.style.display = DisplayStyle.None;
        pauseOverlay = CreateFullscreenOverlay();
        Label pauseStarsLabel = CreateLabel("★ ★ ★", 34f, new Color(1f, 0.74f, 0.32f, 0.95f), true, TextAnchor.MiddleCenter);
        pauseStarsLabel.style.marginBottom = 8f;
        pauseStarsLabel.style.letterSpacing = 2.4f;
        pauseTitleLabel = CreateLabel("PAUSED", 132f, new Color(0.96f, 0.99f, 1f, 1f), true, TextAnchor.MiddleCenter);
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

        speedValueLabel = CreateLabel("Song Speed 100%", 34f, new Color(1f, 0.96f, 0.87f, 1f), true);
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
        Button settingsButton = CreateActionButton("Settings", () => owner?.OpenSongSettingsFromUi());
        Button toneLabButton = CreateActionButton("Tone Lab", () => owner?.OpenToneLabFromUi());
        Button resumeButton = CreateActionButton("Resume", () => owner?.ResumePlaybackFromUi());

        foreach (Button button in new[] { loopButton, songSelectButton, settingsButton, toneLabButton, resumeButton })
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
        Label settingsTopTag = CreateLabel("◉ TUNE DECK ◉", 30f, new Color(1f, 0.73f, 0.33f, 0.95f), true, TextAnchor.MiddleCenter);
        settingsTopTag.style.marginBottom = 6f;
        settingsTopTag.style.letterSpacing = 1.6f;

        Label settingsTitle = CreateLabel("SESSION SETTINGS", 88f, Color.white, true, TextAnchor.MiddleCenter);
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

        selectionOverlay = CreateFullscreenOverlay();
        Label selectionTopTag = CreateLabel("PRESS START TO PICK YOUR TRACK", 28f, new Color(1f, 0.73f, 0.33f, 0.95f), true, TextAnchor.MiddleCenter);
        selectionTopTag.style.marginBottom = 6f;
        selectionTopTag.style.letterSpacing = 1f;

        Label selectionTitle = CreateLabel("TRACK LIBRARY", 90f, Color.white, true, TextAnchor.MiddleCenter);
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

        selectionRowButtons = new Button[8];
        for (int i = 0; i < selectionRowButtons.Length; i++)
        {
            int rowIndex = i;
            Button rowButton = CreateActionButton("", () => OnSongRowClicked(rowIndex));
            rowButton.style.height = 76f;
            rowButton.style.marginTop = 6f;
            rowButton.style.marginBottom = 2f;
            rowButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            rowButton.style.borderTopLeftRadius = 12f;
            rowButton.style.borderTopRightRadius = 12f;
            rowButton.style.borderBottomLeftRadius = 12f;
            rowButton.style.borderBottomRightRadius = 12f;
            selectionCard.Add(rowButton);
            selectionRowButtons[i] = rowButton;
        }

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

        if (dynamicFont != null)
            ApplyFont(root, FontDefinition.FromFont(dynamicFont));

        songCard.Add(songNameLabel);
        songCard.Add(trackNameLabel);
        root.Add(hudStripe);
        root.Add(marqueeLabel);
        root.Add(vibeLabel);
        root.Add(songCard);
        root.Add(speedBadgeLabel);
        root.Add(scorePlate);
        root.Add(judgePopupLabel);
        root.Add(pauseOverlay);
        root.Add(settingsOverlay);
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

        int hitCount = 0;
        int missCount = 0;
        int resolvedCount = 0;
        GameplayNoteState latestResolved = null;

        if (snapshot.noteStates != null)
        {
            foreach (GameplayNoteState noteState in snapshot.noteStates)
            {
                if (noteState == null || !noteState.IsResolved)
                    continue;

                resolvedCount++;
                if (noteState.IsHit)
                    hitCount++;
                else if (noteState.IsMissed)
                    missCount++;

                if (latestResolved == null || noteState.resolvedAt > latestResolved.resolvedAt)
                    latestResolved = noteState;
            }
        }

        float scorePercent = resolvedCount > 0
            ? (100f * hitCount / resolvedCount)
            : 100f;
        scorePercentLabel.text = $"SCORE {scorePercent:F1}%";
        noteTallyLabel.text = $"HITS {hitCount}  •  MISS {missCount}";

        if (!hasSeenSnapshot)
        {
            hasSeenSnapshot = true;
            lastResolvedCount = resolvedCount;
        }
        else if (resolvedCount > lastResolvedCount && latestResolved != null)
        {
            bool success = latestResolved.IsHit;
            judgePopupLabel.text = success ? "SUCCESS" : "FAIL";
            judgePopupLabel.style.color = success
                ? new Color(1f, 0.90f, 0.46f, 0.99f)
                : new Color(1f, 0.44f, 0.62f, 0.99f);
            judgePopupStartTime = Time.unscaledTime;
            lastResolvedCount = resolvedCount;
        }
        else if (resolvedCount < lastResolvedCount)
        {
            lastResolvedCount = resolvedCount;
        }

        float popupElapsed = Time.unscaledTime - judgePopupStartTime;
        if (popupElapsed >= 0f && popupElapsed <= JudgePopupDuration)
        {
            float t = popupElapsed / JudgePopupDuration;
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            judgePopupLabel.style.display = DisplayStyle.Flex;
            judgePopupLabel.style.top = Mathf.Lerp(238f, 148f, eased);
            judgePopupLabel.style.opacity = Mathf.Lerp(1f, 0f, t);
            judgePopupLabel.style.scale = new Scale(new Vector3(Mathf.Lerp(1.08f, 0.96f, eased), Mathf.Lerp(1.08f, 0.96f, eased), 1f));
        }
        else
        {
            judgePopupLabel.style.display = DisplayStyle.None;
            judgePopupLabel.style.opacity = 1f;
            judgePopupLabel.style.scale = new Scale(Vector3.one);
        }

        float speedPercent = Mathf.Clamp(snapshot.playbackSpeedPercent, 1f, 200f);
        speedBadgeLabel.text = $"SPEED {speedPercent:F0}%";
        speedValueLabel.text = $"Song Speed {speedPercent:F0}%";

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

        bool showPause = snapshot.isPaused && !snapshot.showSongSettings && !snapshot.showSongSelection;
        bool showSettings = snapshot.showSongSettings;
        bool showSelection = snapshot.showSongSelection;

        pauseOverlay.style.display = showPause ? DisplayStyle.Flex : DisplayStyle.None;
        settingsOverlay.style.display = showSettings ? DisplayStyle.Flex : DisplayStyle.None;
        selectionOverlay.style.display = showSelection ? DisplayStyle.Flex : DisplayStyle.None;

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
        int scroll = Mathf.Max(0, snapshot.songListScrollOffset);
        currentSongListScrollOffset = scroll;
        selectionSubtitleLabel.text = $"{total} songs  •  Selected #{snapshot.selectedSongIndex + 1}";

        for (int row = 0; row < selectionRowButtons.Length; row++)
        {
            Button button = selectionRowButtons[row];
            int songIndex = scroll + row;

            if (snapshot.availableSongNames == null || songIndex >= snapshot.availableSongNames.Count)
            {
                button.style.display = DisplayStyle.None;
                continue;
            }

            button.style.display = DisplayStyle.Flex;
            string name = snapshot.availableSongNames[songIndex];
            bool isSelected = songIndex == snapshot.selectedSongIndex;
            button.text = isSelected ? $"> {name}" : $"  {name}";
            button.style.backgroundColor = isSelected
                ? new Color(0.42f, 0.18f, 0.52f, 0.98f)
                : new Color(0.08f, 0.15f, 0.24f, 0.93f);
            button.style.borderTopColor = isSelected ? new Color(1f, 0.54f, 0.80f, 1f) : new Color(0.36f, 0.58f, 1f, 0.75f);
            button.style.borderRightColor = button.style.borderTopColor;
            button.style.borderBottomColor = button.style.borderTopColor;
            button.style.borderLeftColor = button.style.borderTopColor;
        }
    }

    private void OnSongRowClicked(int rowIndex)
    {
        if (owner == null)
            return;

        owner.SelectSongByIndexFromUi(rowIndex + currentSongListScrollOffset);
    }

    private static Label CreateLabel(string text, float size, Color color, bool bold = false, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        Label label = new Label(text);
        label.style.fontSize = size;
        label.style.color = color;
        label.style.unityTextAlign = anchor;
        if (bold)
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
        return label;
    }

    private static Button CreateActionButton(string text, Action onClick)
    {
        Button button = new Button(() => onClick?.Invoke()) { text = text };
        button.style.height = 64f;
        button.style.minWidth = 220f;
        button.style.paddingLeft = 18f;
        button.style.paddingRight = 18f;
        button.style.backgroundColor = new Color(0.16f, 0.11f, 0.33f, 0.95f);
        button.style.color = Color.white;
        button.style.fontSize = 28f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.borderTopLeftRadius = 12f;
        button.style.borderTopRightRadius = 12f;
        button.style.borderBottomLeftRadius = 12f;
        button.style.borderBottomRightRadius = 12f;
        button.style.borderTopWidth = 2f;
        button.style.borderRightWidth = 2f;
        button.style.borderBottomWidth = 2f;
        button.style.borderLeftWidth = 2f;
        button.style.borderTopColor = new Color(1f, 0.56f, 0.87f, 0.96f);
        button.style.borderRightColor = new Color(0.66f, 0.54f, 1f, 0.94f);
        button.style.borderBottomColor = new Color(0.45f, 0.34f, 0.85f, 0.92f);
        button.style.borderLeftColor = new Color(0.66f, 0.54f, 1f, 0.94f);
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.letterSpacing = 0.35f;
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
        marqueeLabel.style.fontSize = bodySize * 0.58f;
        vibeLabel.style.fontSize = bodySize * 0.42f;
        speedBadgeLabel.style.fontSize = bodySize;
        scorePercentLabel.style.fontSize = bodySize * 0.88f;
        noteTallyLabel.style.fontSize = bodySize * 0.50f;
        judgePopupLabel.style.fontSize = Mathf.Clamp(screenHeight * 0.072f, 64f, 108f);
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

        foreach (Button button in document.rootVisualElement.Query<Button>().ToList())
        {
            button.style.fontSize = buttonFontSize;
            if (button.style.height.value.value < buttonHeight)
                button.style.height = buttonHeight;
        }

        songCard.style.minWidth = Mathf.Clamp(Screen.width * 0.46f, 640f, 1400f);
    }
}
