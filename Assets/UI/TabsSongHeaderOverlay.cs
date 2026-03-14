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

    private readonly VisualElement pauseOverlay;
    private readonly Label pauseTitleLabel;
    private readonly Label pauseHintLabel;
    private readonly Label pauseInfoLabel;
    private readonly Button loopButton;
    private readonly Slider speedSlider;
    private readonly Label speedValueLabel;
    private readonly Label legacyHintLabel;

    private int lastScreenHeight = -1;
    private bool suppressSpeedSliderCallback;

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
        root.style.paddingLeft = 30f;
        root.style.paddingRight = 30f;
        root.style.paddingBottom = 30f;

        songCard = new VisualElement();
        songCard.style.backgroundColor = new Color(0.05f, 0.08f, 0.12f, 0.90f);
        songCard.style.borderTopLeftRadius = 16f;
        songCard.style.borderTopRightRadius = 16f;
        songCard.style.borderBottomLeftRadius = 16f;
        songCard.style.borderBottomRightRadius = 16f;
        songCard.style.borderTopWidth = 2f;
        songCard.style.borderBottomWidth = 1f;
        songCard.style.borderLeftWidth = 1f;
        songCard.style.borderRightWidth = 1f;
        songCard.style.borderTopColor = new Color(0.46f, 0.75f, 1f, 0.92f);
        songCard.style.borderBottomColor = new Color(0.20f, 0.30f, 0.43f, 0.90f);
        songCard.style.borderLeftColor = new Color(0.20f, 0.30f, 0.43f, 0.90f);
        songCard.style.borderRightColor = new Color(0.20f, 0.30f, 0.43f, 0.90f);
        songCard.style.paddingLeft = 24f;
        songCard.style.paddingRight = 24f;
        songCard.style.paddingTop = 16f;
        songCard.style.paddingBottom = 14f;
        songCard.style.minWidth = 560f;
        songCard.style.maxWidth = 980f;

        songNameLabel = new Label("Song Name");
        songNameLabel.style.color = new Color(0.95f, 0.98f, 1f, 1f);
        songNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        songNameLabel.style.fontSize = 40f;
        songNameLabel.style.marginBottom = 4f;

        trackNameLabel = new Label("Guitar 1");
        trackNameLabel.style.color = new Color(0.71f, 0.88f, 1f, 1f);
        trackNameLabel.style.fontSize = 24f;

        speedBadgeLabel = new Label("SPEED 100%");
        speedBadgeLabel.style.position = Position.Absolute;
        speedBadgeLabel.style.right = 34f;
        speedBadgeLabel.style.top = 30f;
        speedBadgeLabel.style.paddingLeft = 16f;
        speedBadgeLabel.style.paddingRight = 16f;
        speedBadgeLabel.style.paddingTop = 10f;
        speedBadgeLabel.style.paddingBottom = 10f;
        speedBadgeLabel.style.backgroundColor = new Color(0.06f, 0.10f, 0.16f, 0.92f);
        speedBadgeLabel.style.color = new Color(0.82f, 0.96f, 1f, 1f);
        speedBadgeLabel.style.borderTopLeftRadius = 12f;
        speedBadgeLabel.style.borderTopRightRadius = 12f;
        speedBadgeLabel.style.borderBottomLeftRadius = 12f;
        speedBadgeLabel.style.borderBottomRightRadius = 12f;
        speedBadgeLabel.style.borderTopWidth = 1f;
        speedBadgeLabel.style.borderBottomWidth = 1f;
        speedBadgeLabel.style.borderLeftWidth = 1f;
        speedBadgeLabel.style.borderRightWidth = 1f;
        speedBadgeLabel.style.borderTopColor = new Color(0.38f, 0.74f, 0.93f, 0.90f);
        speedBadgeLabel.style.borderBottomColor = new Color(0.26f, 0.45f, 0.56f, 0.90f);
        speedBadgeLabel.style.borderLeftColor = new Color(0.26f, 0.45f, 0.56f, 0.90f);
        speedBadgeLabel.style.borderRightColor = new Color(0.26f, 0.45f, 0.56f, 0.90f);
        speedBadgeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        speedBadgeLabel.style.fontSize = 24f;

        pauseOverlay = new VisualElement();
        pauseOverlay.style.position = Position.Absolute;
        pauseOverlay.style.left = 0f;
        pauseOverlay.style.right = 0f;
        pauseOverlay.style.top = 0f;
        pauseOverlay.style.bottom = 0f;
        pauseOverlay.style.paddingTop = 60f;
        pauseOverlay.style.alignItems = Align.Center;
        pauseOverlay.style.justifyContent = Justify.FlexStart;
        pauseOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.12f);

        pauseTitleLabel = new Label("PAUSE");
        pauseTitleLabel.style.color = Color.white;
        pauseTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        pauseTitleLabel.style.fontSize = 96f;
        pauseTitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        pauseHintLabel = new Label("Space: Resume  •  Left/Right: Seek  •  1/2: Marker  •  P: Legacy pause UI");
        pauseHintLabel.style.color = new Color(0.80f, 0.90f, 1f, 0.95f);
        pauseHintLabel.style.fontSize = 22f;
        pauseHintLabel.style.marginTop = 8f;
        pauseHintLabel.style.marginBottom = 20f;

        VisualElement pauseCard = new VisualElement();
        pauseCard.style.width = 760f;
        pauseCard.style.maxWidth = 900f;
        pauseCard.style.backgroundColor = new Color(0.05f, 0.09f, 0.14f, 0.94f);
        pauseCard.style.borderTopLeftRadius = 14f;
        pauseCard.style.borderTopRightRadius = 14f;
        pauseCard.style.borderBottomLeftRadius = 14f;
        pauseCard.style.borderBottomRightRadius = 14f;
        pauseCard.style.borderTopWidth = 2f;
        pauseCard.style.borderRightWidth = 1f;
        pauseCard.style.borderBottomWidth = 1f;
        pauseCard.style.borderLeftWidth = 1f;
        pauseCard.style.borderTopColor = new Color(0.49f, 0.74f, 0.95f, 0.95f);
        pauseCard.style.borderRightColor = new Color(0.20f, 0.34f, 0.49f, 0.90f);
        pauseCard.style.borderBottomColor = new Color(0.20f, 0.34f, 0.49f, 0.90f);
        pauseCard.style.borderLeftColor = new Color(0.20f, 0.34f, 0.49f, 0.90f);
        pauseCard.style.paddingTop = 20f;
        pauseCard.style.paddingRight = 22f;
        pauseCard.style.paddingBottom = 20f;
        pauseCard.style.paddingLeft = 22f;

        pauseInfoLabel = new Label();
        pauseInfoLabel.style.color = new Color(0.85f, 0.94f, 1f, 1f);
        pauseInfoLabel.style.fontSize = 22f;
        pauseInfoLabel.style.marginBottom = 14f;

        speedValueLabel = new Label("Song Speed 100%");
        speedValueLabel.style.color = Color.white;
        speedValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        speedValueLabel.style.fontSize = 22f;

        speedSlider = new Slider(1f, 200f);
        speedSlider.style.marginTop = 6f;
        speedSlider.style.marginBottom = 16f;
        speedSlider.RegisterValueChangedCallback(evt =>
        {
            if (suppressSpeedSliderCallback || owner == null)
                return;

            owner.SetPlaybackSpeedPercentFromUi(evt.newValue);
        });

        VisualElement buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.justifyContent = Justify.SpaceBetween;

        loopButton = CreateActionButton("Loop Toggle", () => owner?.ToggleLoopFromUi());
        Button songSelectButton = CreateActionButton("Song Select (L)", () => owner?.OpenSongSelectionFromUi());
        Button settingsButton = CreateActionButton("Song Settings (S)", () => owner?.OpenSongSettingsFromUi());
        Button toneLabButton = CreateActionButton("Tone Lab (T)", () => owner?.OpenToneLabFromUi());

        loopButton.style.marginRight = 8f;
        songSelectButton.style.marginRight = 8f;
        settingsButton.style.marginRight = 8f;
        buttonRow.Add(loopButton);
        buttonRow.Add(songSelectButton);
        buttonRow.Add(settingsButton);
        buttonRow.Add(toneLabButton);

        legacyHintLabel = new Label("Legacy menu: Press P to toggle old pause UI.");
        legacyHintLabel.style.color = new Color(0.64f, 0.82f, 0.99f, 0.90f);
        legacyHintLabel.style.fontSize = 18f;
        legacyHintLabel.style.marginTop = 12f;

        pauseCard.Add(pauseInfoLabel);
        pauseCard.Add(speedValueLabel);
        pauseCard.Add(speedSlider);
        pauseCard.Add(buttonRow);
        pauseCard.Add(legacyHintLabel);

        pauseOverlay.Add(pauseTitleLabel);
        pauseOverlay.Add(pauseHintLabel);
        pauseOverlay.Add(pauseCard);

        if (dynamicFont != null)
        {
            FontDefinition fd = FontDefinition.FromFont(dynamicFont);
            songNameLabel.style.unityFontDefinition = fd;
            trackNameLabel.style.unityFontDefinition = fd;
            speedBadgeLabel.style.unityFontDefinition = fd;
            pauseTitleLabel.style.unityFontDefinition = fd;
            pauseHintLabel.style.unityFontDefinition = fd;
            pauseInfoLabel.style.unityFontDefinition = fd;
            speedValueLabel.style.unityFontDefinition = fd;
            legacyHintLabel.style.unityFontDefinition = fd;
        }

        songCard.Add(songNameLabel);
        songCard.Add(trackNameLabel);
        root.Add(songCard);
        root.Add(speedBadgeLabel);
        root.Add(pauseOverlay);

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

        float speedPercent = Mathf.Clamp(snapshot.playbackSpeedPercent, 1f, 200f);
        speedBadgeLabel.text = $"SPEED {speedPercent:F0}%";
        speedValueLabel.text = $"Song Speed {speedPercent:F0}%";

        suppressSpeedSliderCallback = true;
        speedSlider.SetValueWithoutNotify(speedPercent);
        suppressSpeedSliderCallback = false;

        bool showNewPauseUi = snapshot.isPaused && !snapshot.showSongSettings && !snapshot.showSongSelection && !snapshot.showLegacyPauseUi;
        pauseOverlay.style.display = showNewPauseUi ? DisplayStyle.Flex : DisplayStyle.None;

        if (showNewPauseUi)
        {
            pauseInfoLabel.text =
                $"Loop: {(snapshot.loopEnabled ? "ON" : "OFF")}    Marker: {snapshot.selectedLoopMarker}    " +
                $"Audio: {(snapshot.hasBackingTrack ? (snapshot.isBackingTrackPlaying ? "Playing" : "Paused") : "Missing")}    " +
                $"Time: {snapshot.songTime:F2}s";

            SetButtonState(loopButton, snapshot.loopEnabled ? "Loop: ON" : "Loop: OFF", snapshot.loopEnabled ? new Color(0.13f, 0.52f, 0.25f, 0.92f) : new Color(0.47f, 0.17f, 0.19f, 0.92f));
        }
    }

    public void Dispose()
    {
        if (rootObject != null)
            UnityEngine.Object.Destroy(rootObject);

        if (ownsPanelSettings && panelSettings != null)
            UnityEngine.Object.Destroy(panelSettings);
    }

    private Button CreateActionButton(string text, Action onClick)
    {
        Button button = new Button(() => onClick?.Invoke()) { text = text };
        button.style.flexGrow = 1f;
        button.style.height = 44f;
        button.style.backgroundColor = new Color(0.16f, 0.29f, 0.44f, 0.92f);
        button.style.color = new Color(0.94f, 0.98f, 1f, 1f);
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.borderTopLeftRadius = 8f;
        button.style.borderTopRightRadius = 8f;
        button.style.borderBottomLeftRadius = 8f;
        button.style.borderBottomRightRadius = 8f;
        return button;
    }

    private static void SetButtonState(Button button, string text, Color backgroundColor)
    {
        if (button == null)
            return;

        button.text = text;
        button.style.backgroundColor = backgroundColor;
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

        float songSize = Mathf.Clamp(screenHeight * 0.050f, 30f, 54f);
        float trackSize = Mathf.Clamp(screenHeight * 0.030f, 18f, 34f);
        float titleSize = Mathf.Clamp(screenHeight * 0.090f, 64f, 110f);

        songNameLabel.style.fontSize = songSize;
        trackNameLabel.style.fontSize = trackSize;
        pauseTitleLabel.style.fontSize = titleSize;

        songCard.style.minWidth = Mathf.Clamp(Screen.width * 0.33f, 460f, 980f);
    }
}
