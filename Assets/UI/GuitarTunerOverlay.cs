using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

public sealed class GuitarTunerOverlay : MonoBehaviour
{
    private const float NeedleMaxCents = 50f;
    private const float NeedleMaxDegrees = 58f;
    private const float ModelIntroDelaySeconds = 0.05f;
    private const float ModelIntroDurationSeconds = 1.12f;
    private const float UiIntroDelaySeconds = 0.62f;
    private const float UiIntroDurationSeconds = 0.72f;
    private const float TargetButtonWidth = 86f;
    private const float TargetButtonHeight = 54f;
    private const float TargetButtonMargin = 22f;
    private static readonly Vector2[] TargetButtonAnchorPositions =
    {
        new Vector2(0.25f, 0.685f),
        new Vector2(0.275f, 0.565f),
        new Vector2(0.305f, 0.445f),
        new Vector2(0.335f, 0.325f),
        new Vector2(0.36f, 0.205f),
        new Vector2(0.38f, 0.085f)
    };

    private GuitarBridgeServer owner;
    private GuitarTunerService tunerService;
    private GuitarTunerModelPreview modelPreview;
    private UIDocument document;
    private PanelSettings panelSettings;
    private FontDefinition fontDefinition;
    private FontDefinition titleFontDefinition;
    private bool isBuilt;
    private bool isVisible;
    private float visibleTime;
    private float animatedCents;
    private float animatedInputLevel;

    private VisualElement overlayRoot;
    private VisualElement blurBackdrop;
    private VisualElement modelPreviewElement;
    private VisualElement modelPegGlowLayer;
    private VisualElement modelTargetButtonLayer;
    private VisualElement contentRoot;
    private UIBackdropBlurController backdropBlurController;
    private VisualElement needle;
    private VisualElement needleGlow;
    private VisualElement inputLevelFill;
    private Label titleLabel;
    private Label targetLabel;
    private Label centsLabel;
    private Label statusLabel;
    private VisualElement tuningInfoPanel;
    private Label tuningNameLabel;
    private Label tuningNotesLabel;
    private Label tuningExplanationLabel;
    private Label standaloneTuningPresetLabel;
    private DropdownField standaloneTuningPresetDropdown;
    private Button forceStandardButton;
    private Button songToneMappingsButton;
    private Label songToneMappingsExplanationLabel;
    private DropdownField audioInputDropdown;
    private DropdownField audioChannelDropdown;
    private Button refreshDevicesButton;
    private Button autoModeButton;
    private Button manualModeButton;
    private VisualElement instrumentSwitchRow;
    private Button guitarInstrumentButton;
    private Button bassInstrumentButton;
    private Button backButton;
    private readonly List<Button> targetButtons = new List<Button>();
    private readonly List<Label> debugIslandLabels = new List<Label>();
    private readonly List<TunerNavigationEntry> navigationEntries = new List<TunerNavigationEntry>();
    private readonly Dictionary<VisualElement, TunerInteractionState> interactiveElementStates = new Dictionary<VisualElement, TunerInteractionState>();
    private readonly HashSet<VisualElement> hoveredInteractiveElements = new HashSet<VisualElement>();
    private readonly HashSet<VisualElement> pressedInteractiveElements = new HashSet<VisualElement>();
    private bool suppressTunerSettingsCallbacks;
    private bool navigationActive;
    private int navigationIndex;

    private enum TunerNavigationKind
    {
        AutoMode,
        ManualMode,
        GuitarInstrument,
        BassInstrument,
        Target,
        StandaloneTuningPreset,
        ForceStandard,
        SongToneMappings,
        AudioInput,
        AudioChannel,
        RefreshDevices,
        Back
    }

    private struct TunerNavigationEntry
    {
        public VisualElement Element;
        public TunerNavigationKind Kind;
        public int TargetIndex;
    }

    private struct TunerInteractionState
    {
        public float HoverScale;
        public float PressedScale;
    }

    public void Initialize(GuitarBridgeServer owner, GuitarTunerService tunerService)
    {
        this.owner = owner;
        this.tunerService = tunerService;

        if (isBuilt)
            return;

        document = gameObject.GetComponent<UIDocument>();
        if (document == null)
            document = gameObject.AddComponent<UIDocument>();

        panelSettings = ResolvePanelSettings();
        document.panelSettings = panelSettings;

        Font fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        fontDefinition = FontDefinition.FromFont(fallbackFont);
        Font titleFont = Resources.Load<Font>("MetalLord") ?? Resources.Load<Font>("Fonts/MetalLord") ?? fallbackFont;
        titleFontDefinition = FontDefinition.FromFont(titleFont);

        VisualElement root = document.rootVisualElement;
        root.styleSheets.Clear();
        root.style.flexGrow = 1f;
        root.style.width = Length.Percent(100f);
        root.style.height = Length.Percent(100f);
        root.pickingMode = PickingMode.Ignore;

        modelPreview = new GuitarTunerModelPreview();
        modelPreview.Initialize(transform);
        RefreshModelInstrument();

        BuildUi(root);
        SetVisible(false);
        isBuilt = true;
    }

    public void SetVisible(bool visible)
    {
        bool visibilityChanged = visible != isVisible;
        isVisible = visible;

        if (visibilityChanged && visible)
        {
            visibleTime = 0f;
            animatedCents = 0f;
            animatedInputLevel = 0f;
            RefreshModelInstrument();
        }

        if (overlayRoot != null)
        {
            overlayRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            overlayRoot.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
        }

        if (blurBackdrop != null)
            blurBackdrop.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (contentRoot != null && visibilityChanged)
            contentRoot.style.opacity = visible ? 0f : 1f;

        if (modelPreviewElement != null && visibilityChanged)
        {
            modelPreviewElement.style.display = visible && modelPreview != null && modelPreview.IsReady ? DisplayStyle.Flex : DisplayStyle.None;
            modelPreviewElement.style.opacity = visible ? 0f : 1f;
        }

        if (modelPegGlowLayer != null && visibilityChanged)
        {
            modelPegGlowLayer.style.display = visible && modelPreview != null && modelPreview.IsReady ? DisplayStyle.Flex : DisplayStyle.None;
            modelPegGlowLayer.style.opacity = visible ? 0f : 1f;
        }

        if (modelTargetButtonLayer != null && visibilityChanged)
        {
            modelTargetButtonLayer.style.display = visible && modelPreview != null && modelPreview.IsReady ? DisplayStyle.Flex : DisplayStyle.None;
            modelTargetButtonLayer.style.opacity = visible ? 0f : 1f;
        }

        if (visibilityChanged)
        {
            modelPreview?.SetVisible(visible);
            if (!visible)
                ClearNavigationSelection();
        }
    }

    private void Update()
    {
        if (!isVisible || tunerService == null)
            return;

        RefreshUi(tunerService.GetSnapshot());
        UpdateIntroAnimation(Time.unscaledDeltaTime);
    }

    private void BuildUi(VisualElement root)
    {
        overlayRoot = new VisualElement();
        overlayRoot.style.position = Position.Absolute;
        overlayRoot.style.left = 0f;
        overlayRoot.style.right = 0f;
        overlayRoot.style.top = 0f;
        overlayRoot.style.bottom = 0f;
        overlayRoot.style.flexDirection = FlexDirection.Column;
        overlayRoot.style.alignItems = Align.Stretch;
        overlayRoot.style.justifyContent = Justify.FlexStart;
        overlayRoot.style.overflow = Overflow.Hidden;
        overlayRoot.style.backgroundColor = new Color(0.01f, 0.025f, 0.045f, 0.10f);
        overlayRoot.pickingMode = PickingMode.Position;

        BuildBlurBackdrop();
        BuildModelPreviewElement();

        contentRoot = new VisualElement();
        contentRoot.style.position = Position.Relative;
        contentRoot.style.flexGrow = 1f;
        contentRoot.style.flexDirection = FlexDirection.Column;
        contentRoot.style.alignItems = Align.Stretch;
        contentRoot.style.justifyContent = Justify.FlexStart;
        contentRoot.style.paddingLeft = 84f;
        contentRoot.style.paddingRight = 84f;
        contentRoot.style.paddingTop = 28f;
        contentRoot.style.paddingBottom = 54f;
        contentRoot.pickingMode = PickingMode.Position;

        VisualElement topBar = new VisualElement();
        topBar.style.flexDirection = FlexDirection.Row;
        topBar.style.alignItems = Align.Center;
        topBar.style.justifyContent = Justify.SpaceBetween;
        topBar.style.marginBottom = 16f;

        VisualElement headerControls = new VisualElement();
        headerControls.style.flexDirection = FlexDirection.Row;
        headerControls.style.alignItems = Align.Center;

        titleLabel = CreateLabel("Tuner", 78f, Color.white, true, TextAnchor.MiddleLeft);
        titleLabel.style.unityFontDefinition = titleFontDefinition;
        titleLabel.style.letterSpacing = 1.0f;
        titleLabel.style.marginTop = -4f;

        backButton = new Button(() => owner?.CloseTunerFromUi()) { text = "Skip" };
        StyleOutlineButton(backButton, 150f, 58f, new Color(0.90f, 0.94f, 0.98f, 0.96f), new Color(0.48f, 0.62f, 0.75f, 0.82f));
        EnableButtonInteraction(backButton);

        VisualElement modeRow = new VisualElement();
        modeRow.style.flexDirection = FlexDirection.Row;
        modeRow.style.alignItems = Align.Center;
        modeRow.style.justifyContent = Justify.Center;
        modeRow.style.marginLeft = 32f;

        autoModeButton = new Button(() => owner?.SetTunerModeFromUi(0)) { text = "Auto" };
        manualModeButton = new Button(() => owner?.SetTunerModeFromUi(1)) { text = "Manual" };
        StyleSegmentButton(autoModeButton);
        StyleSegmentButton(manualModeButton);
        EnableButtonInteraction(autoModeButton);
        EnableButtonInteraction(manualModeButton);
        autoModeButton.style.marginRight = 10f;
        manualModeButton.style.marginLeft = 10f;

        modeRow.Add(autoModeButton);
        modeRow.Add(manualModeButton);

        instrumentSwitchRow = new VisualElement();
        instrumentSwitchRow.style.flexDirection = FlexDirection.Row;
        instrumentSwitchRow.style.alignItems = Align.Center;
        instrumentSwitchRow.style.justifyContent = Justify.Center;
        instrumentSwitchRow.style.marginLeft = 24f;

        guitarInstrumentButton = new Button(() => owner?.SetTunerMenuInstrumentFromUi(GuitarTunerInstrument.Guitar)) { text = "Guitar" };
        bassInstrumentButton = new Button(() => owner?.SetTunerMenuInstrumentFromUi(GuitarTunerInstrument.Bass)) { text = "Bass" };
        StyleInstrumentButton(guitarInstrumentButton, selected: true);
        StyleInstrumentButton(bassInstrumentButton, selected: false);
        EnableButtonInteraction(guitarInstrumentButton);
        EnableButtonInteraction(bassInstrumentButton);
        guitarInstrumentButton.style.marginRight = 8f;
        bassInstrumentButton.style.marginLeft = 8f;
        instrumentSwitchRow.Add(guitarInstrumentButton);
        instrumentSwitchRow.Add(bassInstrumentButton);

        headerControls.Add(titleLabel);
        headerControls.Add(modeRow);
        headerControls.Add(instrumentSwitchRow);

        topBar.Add(headerControls);
        topBar.Add(backButton);

        VisualElement tunerRegion = new VisualElement();
        tunerRegion.style.position = Position.Absolute;
        tunerRegion.style.left = 64f;
        tunerRegion.style.top = 148f;
        tunerRegion.style.bottom = 112f;
        tunerRegion.style.width = 520f;
        tunerRegion.style.alignItems = Align.Center;
        tunerRegion.style.justifyContent = Justify.Center;
        tunerRegion.style.flexDirection = FlexDirection.Column;

        VisualElement meterShell = new VisualElement();
        meterShell.style.width = 520f;
        meterShell.style.height = 336f;
        meterShell.style.maxWidth = Length.Percent(100f);
        meterShell.style.position = Position.Relative;
        meterShell.style.alignItems = Align.Center;
        meterShell.style.justifyContent = Justify.FlexEnd;
        meterShell.style.marginBottom = 18f;

        BuildMeterScale(meterShell);

        needleGlow = new VisualElement();
        needleGlow.style.position = Position.Absolute;
        needleGlow.style.left = Length.Percent(50f);
        needleGlow.style.bottom = 40f;
        needleGlow.style.width = 18f;
        needleGlow.style.height = 282f;
        needleGlow.style.marginLeft = -9f;
        needleGlow.style.backgroundColor = new Color(0.25f, 0.88f, 0.78f, 0.18f);
        needleGlow.style.borderTopLeftRadius = 9f;
        needleGlow.style.borderTopRightRadius = 9f;
        needleGlow.style.borderBottomLeftRadius = 9f;
        needleGlow.style.borderBottomRightRadius = 9f;
        needleGlow.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(100f), 0f);

        needle = new VisualElement();
        needle.style.position = Position.Absolute;
        needle.style.left = Length.Percent(50f);
        needle.style.bottom = 42f;
        needle.style.width = 7f;
        needle.style.height = 274f;
        needle.style.marginLeft = -3.5f;
        needle.style.backgroundColor = new Color(0.34f, 0.98f, 0.84f, 1f);
        needle.style.borderTopLeftRadius = 4f;
        needle.style.borderTopRightRadius = 4f;
        needle.style.borderBottomLeftRadius = 4f;
        needle.style.borderBottomRightRadius = 4f;
        needle.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(100f), 0f);

        VisualElement cap = new VisualElement();
        cap.style.position = Position.Absolute;
        cap.style.left = Length.Percent(50f);
        cap.style.bottom = 23f;
        cap.style.width = 48f;
        cap.style.height = 48f;
        cap.style.marginLeft = -24f;
        cap.style.backgroundColor = new Color(0.92f, 0.98f, 1f, 1f);
        cap.style.borderTopLeftRadius = 24f;
        cap.style.borderTopRightRadius = 24f;
        cap.style.borderBottomLeftRadius = 24f;
        cap.style.borderBottomRightRadius = 24f;
        cap.style.borderTopWidth = 6f;
        cap.style.borderRightWidth = 6f;
        cap.style.borderBottomWidth = 6f;
        cap.style.borderLeftWidth = 6f;
        cap.style.borderTopColor = new Color(0.18f, 0.22f, 0.26f, 1f);
        cap.style.borderRightColor = new Color(0.18f, 0.22f, 0.26f, 1f);
        cap.style.borderBottomColor = new Color(0.18f, 0.22f, 0.26f, 1f);
        cap.style.borderLeftColor = new Color(0.18f, 0.22f, 0.26f, 1f);

        meterShell.Add(needleGlow);
        meterShell.Add(needle);
        meterShell.Add(cap);

        targetLabel = CreateLabel("E2", 104f, Color.white, true, TextAnchor.MiddleCenter);
        targetLabel.style.marginTop = -22f;
        targetLabel.style.marginBottom = 2f;
        targetLabel.style.letterSpacing = 0.4f;

        statusLabel = CreateLabel("Play a string", 30f, new Color(0.77f, 0.86f, 0.94f, 0.96f), true, TextAnchor.MiddleCenter);
        statusLabel.style.marginBottom = 10f;

        centsLabel = CreateLabel("0 cents", 34f, new Color(0.94f, 0.98f, 1f, 0.96f), true, TextAnchor.MiddleCenter);
        centsLabel.style.marginBottom = 14f;

        tunerRegion.Add(meterShell);
        tunerRegion.Add(targetLabel);
        tunerRegion.Add(statusLabel);
        tunerRegion.Add(centsLabel);

        VisualElement targetsRow = null;
        if (modelTargetButtonLayer == null)
        {
            targetsRow = new VisualElement();
            targetsRow.style.flexDirection = FlexDirection.Row;
            targetsRow.style.justifyContent = Justify.Center;
            targetsRow.style.alignItems = Align.Center;
            targetsRow.style.marginTop = 26f;
            targetsRow.style.marginBottom = 26f;
        }

        GuitarTunerTarget[] targets = tunerService != null ? tunerService.GetSnapshot().targets : Array.Empty<GuitarTunerTarget>();
        int targetButtonCount = TargetButtonAnchorPositions.Length;
        for (int i = 0; i < targetButtonCount; i++)
        {
            int targetIndex = i;
            GuitarTunerTarget target = i < targets.Length ? targets[i] : null;
            Button targetButton = new Button(() => owner?.SetTunerManualTargetFromUi(targetIndex)) { text = target?.noteName ?? "--" };
            StyleTargetButton(targetButton, target, selected: false, tuned: false);
            EnableButtonInteraction(targetButton, 1.07f, 0.94f);
            targetButton.tooltip = target != null ? $"{target.label}  {target.frequencyHz:F2} Hz" : string.Empty;
            if (modelTargetButtonLayer != null)
            {
                PositionTargetButton(targetButton, i, targets.Length);
                modelTargetButtonLayer.Add(targetButton);
            }
            else
            {
                targetButton.style.marginLeft = 8f;
                targetButton.style.marginRight = 8f;
                targetsRow?.Add(targetButton);
            }
            targetButtons.Add(targetButton);
        }

        BuildTuningInfoPanel();

        VisualElement bottomBar = new VisualElement();
        bottomBar.style.position = Position.Absolute;
        bottomBar.style.left = 84f;
        bottomBar.style.right = 84f;
        bottomBar.style.bottom = 54f;
        bottomBar.style.flexDirection = FlexDirection.Row;
        bottomBar.style.alignItems = Align.Center;
        bottomBar.style.justifyContent = Justify.SpaceBetween;

        VisualElement levelGroup = new VisualElement();
        levelGroup.style.flexDirection = FlexDirection.Column;
        levelGroup.style.width = 360f;

        Label levelLabel = CreateLabel("INPUT", 15f, new Color(0.66f, 0.76f, 0.86f, 0.88f), true, TextAnchor.MiddleLeft);
        levelLabel.style.letterSpacing = 2.2f;
        levelLabel.style.marginBottom = 8f;

        VisualElement levelTrack = new VisualElement();
        levelTrack.style.height = 10f;
        levelTrack.style.backgroundColor = new Color(0.08f, 0.12f, 0.16f, 0.90f);
        levelTrack.style.borderTopLeftRadius = 5f;
        levelTrack.style.borderTopRightRadius = 5f;
        levelTrack.style.borderBottomLeftRadius = 5f;
        levelTrack.style.borderBottomRightRadius = 5f;

        inputLevelFill = new VisualElement();
        inputLevelFill.style.height = Length.Percent(100f);
        inputLevelFill.style.width = Length.Percent(0f);
        inputLevelFill.style.backgroundColor = new Color(0.34f, 0.98f, 0.72f, 1f);
        inputLevelFill.style.borderTopLeftRadius = 5f;
        inputLevelFill.style.borderTopRightRadius = 5f;
        inputLevelFill.style.borderBottomLeftRadius = 5f;
        inputLevelFill.style.borderBottomRightRadius = 5f;

        levelTrack.Add(inputLevelFill);
        levelGroup.Add(levelLabel);
        levelGroup.Add(levelTrack);

        bottomBar.Add(levelGroup);

        contentRoot.Add(topBar);
        contentRoot.Add(tunerRegion);
        if (targetsRow != null)
            contentRoot.Add(targetsRow);
        if (tuningInfoPanel != null)
            contentRoot.Add(tuningInfoPanel);
        contentRoot.Add(bottomBar);

        if (modelPreviewElement != null)
        {
            overlayRoot.Add(modelPreviewElement);
            overlayRoot.Add(modelPegGlowLayer);
        }
        overlayRoot.Add(contentRoot);
        if (modelTargetButtonLayer != null)
            overlayRoot.Add(modelTargetButtonLayer);
        root.Add(overlayRoot);
    }

    private void BuildTuningInfoPanel()
    {
        tuningInfoPanel = new VisualElement();
        tuningInfoPanel.style.position = Position.Absolute;
        tuningInfoPanel.style.right = 84f;
        tuningInfoPanel.style.top = 152f;
        tuningInfoPanel.style.width = 430f;
        tuningInfoPanel.style.flexDirection = FlexDirection.Column;
        tuningInfoPanel.style.paddingTop = 24f;
        tuningInfoPanel.style.paddingRight = 24f;
        tuningInfoPanel.style.paddingBottom = 24f;
        tuningInfoPanel.style.paddingLeft = 24f;
        tuningInfoPanel.style.backgroundColor = new Color(0.015f, 0.030f, 0.050f, 0.76f);
        tuningInfoPanel.style.borderTopWidth = 1f;
        tuningInfoPanel.style.borderRightWidth = 1f;
        tuningInfoPanel.style.borderBottomWidth = 1f;
        tuningInfoPanel.style.borderLeftWidth = 1f;
        tuningInfoPanel.style.borderTopColor = new Color(0.30f, 0.42f, 0.54f, 0.52f);
        tuningInfoPanel.style.borderRightColor = new Color(0.30f, 0.42f, 0.54f, 0.52f);
        tuningInfoPanel.style.borderBottomColor = new Color(0.30f, 0.42f, 0.54f, 0.52f);
        tuningInfoPanel.style.borderLeftColor = new Color(0.30f, 0.42f, 0.54f, 0.52f);
        tuningInfoPanel.style.borderTopLeftRadius = 10f;
        tuningInfoPanel.style.borderTopRightRadius = 10f;
        tuningInfoPanel.style.borderBottomLeftRadius = 10f;
        tuningInfoPanel.style.borderBottomRightRadius = 10f;

        Label tuningTitle = CreateLabel("TUNING TARGET", 15f, new Color(0.56f, 0.74f, 0.90f, 0.96f), true, TextAnchor.MiddleLeft);
        tuningTitle.style.letterSpacing = 2.0f;
        tuningTitle.style.marginBottom = 8f;
        tuningInfoPanel.Add(tuningTitle);

        tuningNameLabel = CreateLabel("E Standard", 34f, Color.white, true, TextAnchor.MiddleLeft);
        tuningNameLabel.style.marginBottom = 2f;
        tuningInfoPanel.Add(tuningNameLabel);

        tuningNotesLabel = CreateLabel("E2  A2  D3  G3  B3  E4", 21f, new Color(0.74f, 0.86f, 0.96f, 0.96f), true, TextAnchor.MiddleLeft);
        tuningNotesLabel.style.marginBottom = 18f;
        tuningInfoPanel.Add(tuningNotesLabel);

        standaloneTuningPresetLabel = CreateFieldLabel("Tuning");
        tuningInfoPanel.Add(standaloneTuningPresetLabel);

        standaloneTuningPresetDropdown = new DropdownField();
        StyleSettingsDropdown(standaloneTuningPresetDropdown);
        standaloneTuningPresetDropdown.style.marginBottom = 12f;
        standaloneTuningPresetDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressTunerSettingsCallbacks)
                return;

            owner?.SetStandaloneTunerTuningPresetFromUi(evt.newValue);
        });
        tuningInfoPanel.Add(standaloneTuningPresetDropdown);

        forceStandardButton = new Button(() => owner?.ToggleTunerForceStandardFromUi()) { text = "Force Standard: ON" };
        StyleTuningToggleButton(forceStandardButton, enabled: true);
        EnableButtonInteraction(forceStandardButton);
        forceStandardButton.style.marginBottom = 12f;
        tuningInfoPanel.Add(forceStandardButton);

        tuningExplanationLabel = CreateLabel(string.Empty, 16f, new Color(0.76f, 0.84f, 0.92f, 0.92f), false, TextAnchor.MiddleLeft);
        tuningExplanationLabel.style.whiteSpace = WhiteSpace.Normal;
        tuningExplanationLabel.style.marginBottom = 14f;
        tuningInfoPanel.Add(tuningExplanationLabel);

        songToneMappingsButton = new Button(() => owner?.ToggleTunerUseSongToneMappingsFromUi()) { text = "Song Tone Mapping: ON" };
        StyleTuningToggleButton(songToneMappingsButton, enabled: true);
        EnableButtonInteraction(songToneMappingsButton);
        songToneMappingsButton.style.marginTop = 2f;
        songToneMappingsButton.style.marginBottom = 10f;
        tuningInfoPanel.Add(songToneMappingsButton);

        songToneMappingsExplanationLabel = CreateLabel(string.Empty, 15f, new Color(0.76f, 0.84f, 0.92f, 0.90f), false, TextAnchor.MiddleLeft);
        songToneMappingsExplanationLabel.style.whiteSpace = WhiteSpace.Normal;
        songToneMappingsExplanationLabel.style.marginBottom = 22f;
        tuningInfoPanel.Add(songToneMappingsExplanationLabel);

        VisualElement separator = new VisualElement();
        separator.style.height = 1f;
        separator.style.backgroundColor = new Color(0.42f, 0.56f, 0.68f, 0.24f);
        separator.style.marginBottom = 20f;
        tuningInfoPanel.Add(separator);

        Label audioTitle = CreateLabel("AUDIO INPUT", 15f, new Color(0.56f, 0.74f, 0.90f, 0.96f), true, TextAnchor.MiddleLeft);
        audioTitle.style.letterSpacing = 2.0f;
        audioTitle.style.marginBottom = 12f;
        tuningInfoPanel.Add(audioTitle);

        tuningInfoPanel.Add(CreateFieldLabel("Input Device"));
        audioInputDropdown = new DropdownField();
        StyleSettingsDropdown(audioInputDropdown);
        audioInputDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressTunerSettingsCallbacks)
                return;

            owner?.SetSharedAudioInputDeviceFromUi(evt.newValue);
        });
        tuningInfoPanel.Add(audioInputDropdown);

        tuningInfoPanel.Add(CreateFieldLabel("Input Channel"));
        audioChannelDropdown = new DropdownField();
        StyleSettingsDropdown(audioChannelDropdown);
        audioChannelDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressTunerSettingsCallbacks)
                return;

            owner?.SetSharedAudioInputChannelModeFromUi(evt.newValue);
        });
        tuningInfoPanel.Add(audioChannelDropdown);

        refreshDevicesButton = new Button(() =>
        {
            owner?.RefreshSharedAudioDevicesFromUi();
            RefreshTuningInfoPanel();
        })
        {
            text = "Refresh Devices"
        };
        StylePanelActionButton(refreshDevicesButton);
        EnableButtonInteraction(refreshDevicesButton);
        refreshDevicesButton.style.marginTop = 16f;
        tuningInfoPanel.Add(refreshDevicesButton);
    }

    private void BuildBlurBackdrop()
    {
        blurBackdrop = new VisualElement();
        blurBackdrop.style.position = Position.Absolute;
        blurBackdrop.style.left = 0f;
        blurBackdrop.style.right = 0f;
        blurBackdrop.style.top = 0f;
        blurBackdrop.style.bottom = 0f;
        blurBackdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        blurBackdrop.pickingMode = PickingMode.Ignore;

        overlayRoot.Add(blurBackdrop);

        backdropBlurController = gameObject.GetComponent<UIBackdropBlurController>();
        if (backdropBlurController == null)
            backdropBlurController = gameObject.AddComponent<UIBackdropBlurController>();
        backdropBlurController.TargetElement = blurBackdrop;
        backdropBlurController.Brightness = 0.70f;
    }

    private void BuildModelPreviewElement()
    {
        if (modelPreview == null || !modelPreview.IsReady)
            return;

        modelPreviewElement = new VisualElement();
        modelPreviewElement.style.position = Position.Absolute;
        modelPreviewElement.style.left = Length.Percent(50f);
        modelPreviewElement.style.top = 74f;
        modelPreviewElement.style.width = 760f;
        modelPreviewElement.style.height = 900f;
        modelPreviewElement.style.marginLeft = -380f;
        modelPreviewElement.style.opacity = 0f;
        modelPreviewElement.style.display = DisplayStyle.None;
        modelPreviewElement.style.scale = new Scale(new Vector3(-1f, -1f, 1f));
        modelPreviewElement.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(modelPreview.Texture));
        modelPreviewElement.pickingMode = PickingMode.Ignore;

        modelPegGlowLayer = new VisualElement();
        modelPegGlowLayer.style.position = Position.Absolute;
        modelPegGlowLayer.style.left = modelPreviewElement.style.left;
        modelPegGlowLayer.style.top = modelPreviewElement.style.top;
        modelPegGlowLayer.style.width = modelPreviewElement.style.width;
        modelPegGlowLayer.style.height = modelPreviewElement.style.height;
        modelPegGlowLayer.style.marginLeft = modelPreviewElement.style.marginLeft;
        modelPegGlowLayer.style.opacity = 0f;
        modelPegGlowLayer.style.display = DisplayStyle.None;
        modelPegGlowLayer.style.overflow = Overflow.Hidden;
        modelPegGlowLayer.pickingMode = PickingMode.Ignore;

        modelTargetButtonLayer = new VisualElement();
        modelTargetButtonLayer.style.position = Position.Absolute;
        modelTargetButtonLayer.style.left = modelPreviewElement.style.left;
        modelTargetButtonLayer.style.top = modelPreviewElement.style.top;
        modelTargetButtonLayer.style.width = modelPreviewElement.style.width;
        modelTargetButtonLayer.style.height = modelPreviewElement.style.height;
        modelTargetButtonLayer.style.marginLeft = modelPreviewElement.style.marginLeft;
        modelTargetButtonLayer.style.opacity = 0f;
        modelTargetButtonLayer.style.display = DisplayStyle.None;
        modelTargetButtonLayer.style.overflow = Overflow.Visible;
        modelTargetButtonLayer.pickingMode = PickingMode.Position;
    }

    private void UpdateIntroAnimation(float deltaTime)
    {
        visibleTime += Mathf.Max(0f, deltaTime);

        float modelProgress = SmoothStep01((visibleTime - ModelIntroDelaySeconds) / ModelIntroDurationSeconds);
        float uiProgress = SmoothStep01((visibleTime - UiIntroDelaySeconds) / UiIntroDurationSeconds);

        if (contentRoot != null)
            contentRoot.style.opacity = uiProgress;

        if (modelPreviewElement != null && modelPreview != null && modelPreview.IsReady)
        {
            modelPreview.Render(modelProgress);
            modelPreviewElement.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(modelPreview.Texture));
            float modelOpacity = Mathf.Lerp(0.02f, 0.86f, SmoothStep01((visibleTime - 0.02f) / 0.32f));
            ApplyModelPreviewLayout(modelPreviewElement, modelProgress, modelOpacity);
            ApplyModelPreviewLayout(modelPegGlowLayer, modelProgress, modelOpacity);
            ApplyModelPreviewLayout(modelTargetButtonLayer, modelProgress, uiProgress);
            RefreshModelDebugIslandLabels();
        }
    }

    private void RefreshModelDebugIslandLabels()
    {
        if (modelPegGlowLayer == null || modelPreview == null || !modelPreview.ShowDebugIslandLabels)
        {
            for (int i = 0; i < debugIslandLabels.Count; i++)
                debugIslandLabels[i].style.display = DisplayStyle.None;
            return;
        }

        IReadOnlyList<GuitarTunerModelPreview.DebugIslandMarker> markers = modelPreview.DebugIslandMarkers;
        EnsureDebugIslandLabelCount(markers.Count);

        for (int i = 0; i < debugIslandLabels.Count; i++)
        {
            Label label = debugIslandLabels[i];
            if (i >= markers.Count)
            {
                label.style.display = DisplayStyle.None;
                continue;
            }

            GuitarTunerModelPreview.DebugIslandMarker marker = markers[i];
            label.text = marker.mapped
                ? $"{marker.islandIndex}*"
                : marker.islandIndex.ToString(CultureInfo.InvariantCulture);
            label.tooltip = marker.mapped
                ? $"Island {marker.islandIndex}, mapped to string {marker.pegIndex}"
                : $"Island {marker.islandIndex}";
            label.style.display = DisplayStyle.Flex;
            label.style.left = Length.Percent(Mathf.Clamp01(1f - marker.viewport.x) * 100f);
            label.style.top = Length.Percent(Mathf.Clamp01(marker.viewport.y) * 100f);
            label.style.color = marker.mapped
                ? new Color(1f, 0.93f, 0.22f, 1f)
                : new Color(0.88f, 0.96f, 1f, 1f);
            label.style.backgroundColor = marker.mapped
                ? new Color(0.16f, 0.08f, 0.00f, 0.86f)
                : new Color(0.00f, 0.04f, 0.08f, 0.74f);
        }
    }

    private void EnsureDebugIslandLabelCount(int count)
    {
        while (debugIslandLabels.Count < count)
        {
            Label label = CreateLabel(string.Empty, 11f, Color.white, true, TextAnchor.MiddleCenter);
            label.style.position = Position.Absolute;
            label.style.width = 38f;
            label.style.height = 18f;
            label.style.marginLeft = -19f;
            label.style.marginTop = -9f;
            label.style.borderTopLeftRadius = 5f;
            label.style.borderTopRightRadius = 5f;
            label.style.borderBottomLeftRadius = 5f;
            label.style.borderBottomRightRadius = 5f;
            label.style.borderTopWidth = 1f;
            label.style.borderRightWidth = 1f;
            label.style.borderBottomWidth = 1f;
            label.style.borderLeftWidth = 1f;
            label.style.borderTopColor = new Color(1f, 1f, 1f, 0.20f);
            label.style.borderRightColor = new Color(1f, 1f, 1f, 0.20f);
            label.style.borderBottomColor = new Color(1f, 1f, 1f, 0.20f);
            label.style.borderLeftColor = new Color(1f, 1f, 1f, 0.20f);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.pickingMode = PickingMode.Ignore;
            modelPegGlowLayer.Add(label);
            debugIslandLabels.Add(label);
        }
    }

    private static void ApplyModelPreviewLayout(VisualElement element, float modelProgress, float opacity)
    {
        if (element == null)
            return;

        element.style.display = DisplayStyle.Flex;
        element.style.opacity = opacity;
        element.style.left = Length.Percent(50f);
        element.style.top = Mathf.Lerp(74f, 112f, modelProgress);
        element.style.width = Mathf.Lerp(760f, 560f, modelProgress);
        element.style.height = Mathf.Lerp(900f, 820f, modelProgress);
        element.style.marginLeft = Mathf.Lerp(-380f, -280f, modelProgress);
    }

    private void BuildMeterScale(VisualElement meterShell)
    {
        for (int i = -5; i <= 5; i++)
        {
            bool major = i == -5 || i == 0 || i == 5;
            VisualElement tick = new VisualElement();
            tick.style.position = Position.Absolute;
            tick.style.left = Length.Percent(50f);
            tick.style.bottom = 42f;
            tick.style.width = major ? 5f : 3f;
            tick.style.height = major ? 270f : 225f;
            tick.style.marginLeft = major ? -2.5f : -1.5f;
            tick.style.backgroundColor = i == 0
                ? new Color(0.34f, 0.98f, 0.84f, 0.74f)
                : new Color(0.78f, 0.88f, 0.96f, major ? 0.44f : 0.26f);
            tick.style.borderTopLeftRadius = 2f;
            tick.style.borderTopRightRadius = 2f;
            tick.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(100f), 0f);
            tick.style.rotate = new Rotate(new Angle(i * 11.6f, AngleUnit.Degree));
            meterShell.Add(tick);
        }

        Label flatLabel = CreateLabel("FLAT", 22f, new Color(0.70f, 0.78f, 0.86f, 0.82f), true, TextAnchor.MiddleCenter);
        flatLabel.style.position = Position.Absolute;
        flatLabel.style.left = 104f;
        flatLabel.style.bottom = 38f;
        flatLabel.style.width = 160f;
        flatLabel.style.letterSpacing = 1.4f;
        meterShell.Add(flatLabel);

        Label sharpLabel = CreateLabel("SHARP", 22f, new Color(0.70f, 0.78f, 0.86f, 0.82f), true, TextAnchor.MiddleCenter);
        sharpLabel.style.position = Position.Absolute;
        sharpLabel.style.right = 104f;
        sharpLabel.style.bottom = 38f;
        sharpLabel.style.width = 160f;
        sharpLabel.style.letterSpacing = 1.4f;
        meterShell.Add(sharpLabel);
    }

    private void RefreshUi(GuitarTunerSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        RefreshModelInstrument();
        float targetCents = snapshot.hasSignal ? Mathf.Clamp(snapshot.cents, -NeedleMaxCents, NeedleMaxCents) : 0f;
        float targetLevel = Mathf.Clamp01(snapshot.inputLevel);
        float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.045f);
        animatedCents = Mathf.Lerp(animatedCents, targetCents, Mathf.Clamp01(blend));
        animatedInputLevel = Mathf.Lerp(animatedInputLevel, targetLevel, Mathf.Clamp01(blend));

        float degrees = Mathf.Clamp(animatedCents / NeedleMaxCents, -1f, 1f) * NeedleMaxDegrees;
        Color accent = snapshot.isInTune
            ? new Color(0.34f, 0.98f, 0.72f, 1f)
            : snapshot.hasSignal
                ? new Color(0.98f, 0.74f, 0.36f, 1f)
                : new Color(0.42f, 0.58f, 0.72f, 0.82f);

        Rotate needleRotation = new Rotate(new Angle(degrees, AngleUnit.Degree));
        needle.style.rotate = needleRotation;
        needle.style.backgroundColor = accent;
        needle.style.opacity = snapshot.hasSignal ? 1f : 0.54f;
        needleGlow.style.rotate = needleRotation;
        needleGlow.style.backgroundColor = new Color(accent.r, accent.g, accent.b, snapshot.hasSignal ? 0.22f : 0.08f);
        needleGlow.style.opacity = snapshot.hasSignal ? 1f : 0.60f;

        inputLevelFill.style.width = Length.Percent(animatedInputLevel * 100f);
        inputLevelFill.style.backgroundColor = targetLevel > 0.85f
            ? new Color(0.98f, 0.45f, 0.38f, 1f)
            : new Color(0.34f, 0.98f, 0.72f, 1f);

        bool showCompletionMessage = snapshot.allTargetsTuned && !snapshot.hasSignal;
        targetLabel.text = showCompletionMessage
            ? "Ready to jam!"
            : string.IsNullOrWhiteSpace(snapshot.targetNoteName) ? "--" : snapshot.targetNoteName;
        targetLabel.style.fontSize = showCompletionMessage ? 76f : 124f;
        targetLabel.style.color = showCompletionMessage
            ? new Color(0.34f, 0.98f, 0.72f, 1f)
            : snapshot.hasSignal ? accent : Color.white;
        statusLabel.text = snapshot.statusText;
        statusLabel.style.color = accent;
        centsLabel.text = snapshot.hasSignal ? FormatCents(snapshot.cents) : "-- cents";
        centsLabel.style.color = snapshot.hasSignal ? new Color(0.94f, 0.98f, 1f, 0.98f) : new Color(0.70f, 0.78f, 0.86f, 0.78f);

        StyleModeButton(autoModeButton, snapshot.mode == GuitarTunerMode.Automatic);
        StyleModeButton(manualModeButton, snapshot.mode == GuitarTunerMode.Manual);
        RefreshInstrumentSwitch();
        RefreshTuningInfoPanel();

        for (int i = 0; i < targetButtons.Count; i++)
        {
            GuitarTunerTarget target = snapshot.targets != null && i < snapshot.targets.Length ? snapshot.targets[i] : null;
            bool tuned = snapshot.tunedTargets != null && i < snapshot.tunedTargets.Length && snapshot.tunedTargets[i];
            bool hasTarget = target != null && i < TargetButtonAnchorPositions.Length;
            targetButtons[i].style.display = hasTarget ? DisplayStyle.Flex : DisplayStyle.None;
            if (!hasTarget)
                continue;

            targetButtons[i].text = tuned && target != null ? $"{target.noteName}\nOK" : target?.noteName ?? "--";
            targetButtons[i].tooltip = target != null ? $"{target.label}  {target.frequencyHz:F2} Hz" : string.Empty;
            StyleTargetButton(targetButtons[i], target, i == snapshot.selectedTargetIndex, tuned);
            if (modelTargetButtonLayer != null)
                PositionTargetButton(targetButtons[i], i, snapshot.targets != null ? snapshot.targets.Length : 6);
        }

        RefreshPegHighlights(snapshot);
        RefreshNavigationVisuals();
    }

    private void RefreshModelInstrument()
    {
        if (modelPreview == null || owner == null)
            return;

        modelPreview.SetInstrument(owner.GetTunerInstrumentForUi());
    }

    private void RefreshInstrumentSwitch()
    {
        if (instrumentSwitchRow == null)
            return;

        bool visible = owner != null && owner.IsStandaloneTunerInstrumentSwitchVisibleForUi();
        instrumentSwitchRow.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible)
            return;

        GuitarTunerInstrument instrument = owner.GetTunerInstrumentForUi();
        StyleInstrumentButton(guitarInstrumentButton, instrument == GuitarTunerInstrument.Guitar);
        StyleInstrumentButton(bassInstrumentButton, instrument == GuitarTunerInstrument.Bass);
    }

    private void RefreshTuningInfoPanel()
    {
        if (tuningInfoPanel == null)
            return;

        bool standaloneTuner = owner != null && owner.IsStandaloneTunerTuningPresetVisibleForUi();
        bool forceStandard = owner == null || owner.GetForceStandardTuningForUi();
        bool songToneMappingsEnabled = owner == null || owner.GetTunerUseSongToneMappingsForUi();

        if (tuningNameLabel != null)
            tuningNameLabel.text = owner != null ? owner.GetTunerTuningDisplayNameForUi() : "E Standard";
        if (tuningNotesLabel != null)
            tuningNotesLabel.text = owner != null ? owner.GetTunerTargetNotesForUi() : "E2  A2  D3  G3  B3  E4";
        if (tuningExplanationLabel != null)
            tuningExplanationLabel.text = owner != null
                ? owner.GetTunerTuningExplanationForUi()
                : "Tune to the active target before starting the song.";
        if (standaloneTuningPresetLabel != null)
            standaloneTuningPresetLabel.style.display = standaloneTuner ? DisplayStyle.Flex : DisplayStyle.None;
        if (standaloneTuningPresetDropdown != null)
            standaloneTuningPresetDropdown.style.display = standaloneTuner ? DisplayStyle.Flex : DisplayStyle.None;
        if (forceStandardButton != null)
        {
            forceStandardButton.style.display = standaloneTuner ? DisplayStyle.None : DisplayStyle.Flex;
            forceStandardButton.text = forceStandard ? "Force Standard: ON" : "Force Standard: OFF";
            StyleTuningToggleButton(forceStandardButton, forceStandard);
        }
        if (songToneMappingsButton != null)
        {
            songToneMappingsButton.text = songToneMappingsEnabled ? "Song Tone Mapping: ON" : "Song Tone Mapping: OFF";
            StyleTuningToggleButton(songToneMappingsButton, songToneMappingsEnabled);
        }
        if (songToneMappingsExplanationLabel != null)
        {
            songToneMappingsExplanationLabel.text = owner != null
                ? owner.GetTunerSongToneMappingsExplanationForUi()
                : "When enabled, song startup tuning previews the song's first mapped tone.";
        }

        suppressTunerSettingsCallbacks = true;
        try
        {
            if (standaloneTuningPresetDropdown != null && standaloneTuner)
            {
                IReadOnlyList<string> rawChoices = owner != null ? owner.GetStandaloneTunerTuningPresetChoicesForUi() : null;
                List<string> choices = rawChoices != null
                    ? rawChoices.Where(choice => !string.IsNullOrWhiteSpace(choice)).ToList()
                    : new List<string>();
                if (choices.Count == 0)
                    choices.Add("E Standard");

                if (!DropdownChoicesMatch(standaloneTuningPresetDropdown, choices))
                    standaloneTuningPresetDropdown.choices = choices;

                string selected = owner != null ? owner.GetStandaloneTunerSelectedTuningPresetForUi() : choices[0];
                if (string.IsNullOrWhiteSpace(selected) || !choices.Contains(selected))
                    selected = choices[0];
                if (!string.Equals(standaloneTuningPresetDropdown.value, selected, StringComparison.Ordinal))
                    standaloneTuningPresetDropdown.SetValueWithoutNotify(selected);
                standaloneTuningPresetDropdown.SetEnabled(choices.Count > 0);
            }

            if (audioInputDropdown != null)
            {
                IReadOnlyList<string> rawChoices = owner != null ? owner.GetSharedAudioInputDeviceChoices() : null;
                List<string> choices = rawChoices != null
                    ? rawChoices.Where(choice => !string.IsNullOrWhiteSpace(choice)).ToList()
                    : new List<string>();
                if (choices.Count == 0)
                    choices.Add("Automatic");

                if (!DropdownChoicesMatch(audioInputDropdown, choices))
                    audioInputDropdown.choices = choices;

                string selected = owner != null ? owner.GetSharedAudioSelectedInputLabel() : choices[0];
                if (string.IsNullOrWhiteSpace(selected) || !choices.Contains(selected))
                    selected = choices[0];
                if (!string.Equals(audioInputDropdown.value, selected, StringComparison.Ordinal))
                    audioInputDropdown.SetValueWithoutNotify(selected);
                audioInputDropdown.SetEnabled(choices.Count > 0);
            }

            if (audioChannelDropdown != null)
            {
                List<string> choices = SharedAudioInputChannelModes.Choices.ToList();
                if (!DropdownChoicesMatch(audioChannelDropdown, choices))
                    audioChannelDropdown.choices = choices;

                string selected = owner != null
                    ? owner.GetSharedAudioInputChannelModeLabel()
                    : SharedAudioInputChannelModes.Input1;
                selected = SharedAudioInputChannelModes.Normalize(selected);
                if (!choices.Contains(selected))
                    selected = SharedAudioInputChannelModes.Input1;
                if (!string.Equals(audioChannelDropdown.value, selected, StringComparison.Ordinal))
                    audioChannelDropdown.SetValueWithoutNotify(selected);
                audioChannelDropdown.SetEnabled(choices.Count > 0);
            }
        }
        finally
        {
            suppressTunerSettingsCallbacks = false;
        }
    }

    public void MoveNavigationSelection(int delta)
    {
        RefreshNavigationEntries();
        if (navigationEntries.Count == 0)
            return;

        navigationActive = true;
        navigationIndex = WrapIndex(navigationIndex + delta, navigationEntries.Count);
        RefreshNavigationVisuals();
    }

    public bool ActivateNavigationSelection()
    {
        if (!navigationActive)
            return false;

        RefreshNavigationEntries();
        if (navigationEntries.Count == 0)
            return false;

        ActivateNavigationEntry(navigationEntries[Mathf.Clamp(navigationIndex, 0, navigationEntries.Count - 1)]);
        RefreshNavigationVisuals();
        return true;
    }

    public bool AdjustNavigationSelection(int delta)
    {
        if (!navigationActive)
            return false;

        RefreshNavigationEntries();
        if (navigationEntries.Count == 0)
            return false;

        if (!AdjustNavigationEntry(navigationEntries[Mathf.Clamp(navigationIndex, 0, navigationEntries.Count - 1)], delta))
            return false;

        RefreshNavigationVisuals();
        return true;
    }

    private void ClearNavigationSelection()
    {
        navigationActive = false;
        navigationIndex = 0;
        RefreshNavigationVisuals();
    }

    private void RefreshNavigationEntries()
    {
        navigationEntries.Clear();

        AddNavigationEntry(autoModeButton, TunerNavigationKind.AutoMode);
        AddNavigationEntry(manualModeButton, TunerNavigationKind.ManualMode);
        AddNavigationEntry(guitarInstrumentButton, TunerNavigationKind.GuitarInstrument);
        AddNavigationEntry(bassInstrumentButton, TunerNavigationKind.BassInstrument);

        for (int i = 0; i < targetButtons.Count; i++)
            AddNavigationEntry(targetButtons[i], TunerNavigationKind.Target, i);

        AddNavigationEntry(standaloneTuningPresetDropdown, TunerNavigationKind.StandaloneTuningPreset);
        AddNavigationEntry(forceStandardButton, TunerNavigationKind.ForceStandard);
        AddNavigationEntry(songToneMappingsButton, TunerNavigationKind.SongToneMappings);
        AddNavigationEntry(audioInputDropdown, TunerNavigationKind.AudioInput);
        AddNavigationEntry(audioChannelDropdown, TunerNavigationKind.AudioChannel);
        AddNavigationEntry(refreshDevicesButton, TunerNavigationKind.RefreshDevices);
        AddNavigationEntry(backButton, TunerNavigationKind.Back);

        if (navigationIndex >= navigationEntries.Count)
            navigationIndex = Mathf.Max(0, navigationEntries.Count - 1);
    }

    private void AddNavigationEntry(VisualElement element, TunerNavigationKind kind, int targetIndex = -1)
    {
        if (!IsElementVisible(element))
            return;

        navigationEntries.Add(new TunerNavigationEntry
        {
            Element = element,
            Kind = kind,
            TargetIndex = targetIndex
        });
    }

    private void ActivateNavigationEntry(TunerNavigationEntry entry)
    {
        switch (entry.Kind)
        {
            case TunerNavigationKind.AutoMode:
                owner?.SetTunerModeFromUi(0);
                break;
            case TunerNavigationKind.ManualMode:
                owner?.SetTunerModeFromUi(1);
                break;
            case TunerNavigationKind.GuitarInstrument:
                owner?.SetTunerMenuInstrumentFromUi(GuitarTunerInstrument.Guitar);
                break;
            case TunerNavigationKind.BassInstrument:
                owner?.SetTunerMenuInstrumentFromUi(GuitarTunerInstrument.Bass);
                break;
            case TunerNavigationKind.Target:
                owner?.SetTunerManualTargetFromUi(entry.TargetIndex);
                break;
            case TunerNavigationKind.StandaloneTuningPreset:
                CycleStandaloneTuningPreset(1);
                break;
            case TunerNavigationKind.ForceStandard:
                owner?.ToggleTunerForceStandardFromUi();
                break;
            case TunerNavigationKind.SongToneMappings:
                owner?.ToggleTunerUseSongToneMappingsFromUi();
                break;
            case TunerNavigationKind.AudioInput:
                CycleAudioInputDevice(1);
                break;
            case TunerNavigationKind.AudioChannel:
                CycleAudioInputChannel(1);
                break;
            case TunerNavigationKind.RefreshDevices:
                owner?.RefreshSharedAudioDevicesFromUi();
                RefreshTuningInfoPanel();
                break;
            case TunerNavigationKind.Back:
                owner?.CloseTunerFromUi();
                break;
        }
    }

    private bool AdjustNavigationEntry(TunerNavigationEntry entry, int delta)
    {
        switch (entry.Kind)
        {
            case TunerNavigationKind.AutoMode:
            case TunerNavigationKind.ManualMode:
                owner?.SetTunerModeFromUi(delta < 0 ? 0 : 1);
                return true;
            case TunerNavigationKind.GuitarInstrument:
            case TunerNavigationKind.BassInstrument:
                owner?.SetTunerMenuInstrumentFromUi(delta < 0 ? GuitarTunerInstrument.Guitar : GuitarTunerInstrument.Bass);
                return true;
            case TunerNavigationKind.Target:
                owner?.MoveTunerManualTargetFromUi(delta);
                return true;
            case TunerNavigationKind.StandaloneTuningPreset:
                CycleStandaloneTuningPreset(delta);
                return true;
            case TunerNavigationKind.ForceStandard:
                owner?.ToggleTunerForceStandardFromUi();
                return true;
            case TunerNavigationKind.SongToneMappings:
                owner?.ToggleTunerUseSongToneMappingsFromUi();
                return true;
            case TunerNavigationKind.AudioInput:
                CycleAudioInputDevice(delta);
                return true;
            case TunerNavigationKind.AudioChannel:
                CycleAudioInputChannel(delta);
                return true;
            default:
                return false;
        }
    }

    private void RefreshNavigationVisuals()
    {
        ResetNavigationVisual(autoModeButton);
        ResetNavigationVisual(manualModeButton);
        ResetNavigationVisual(guitarInstrumentButton);
        ResetNavigationVisual(bassInstrumentButton);
        ResetNavigationVisual(standaloneTuningPresetDropdown);
        ResetNavigationVisual(forceStandardButton);
        ResetNavigationVisual(songToneMappingsButton);
        ResetNavigationVisual(audioInputDropdown);
        ResetNavigationVisual(audioChannelDropdown);
        ResetNavigationVisual(refreshDevicesButton);
        ResetNavigationVisual(backButton);
        for (int i = 0; i < targetButtons.Count; i++)
            ResetNavigationVisual(targetButtons[i]);

        if (!navigationActive)
            return;

        RefreshNavigationEntries();
        if (navigationEntries.Count == 0)
            return;

        navigationIndex = Mathf.Clamp(navigationIndex, 0, navigationEntries.Count - 1);
        ApplyNavigationVisual(navigationEntries[navigationIndex].Element);
    }

    private void ResetNavigationVisual(VisualElement element)
    {
        if (element == null)
            return;

        ApplyInteractiveVisual(element, navigationHighlighted: false);
    }

    private void ApplyNavigationVisual(VisualElement element)
    {
        if (element == null)
            return;

        ApplyInteractiveVisual(element, navigationHighlighted: true);
    }

    private void CycleStandaloneTuningPreset(int delta)
    {
        if (owner == null)
            return;

        IReadOnlyList<string> choices = owner.GetStandaloneTunerTuningPresetChoicesForUi();
        CycleStringChoice(choices, owner.GetStandaloneTunerSelectedTuningPresetForUi(), delta, owner.SetStandaloneTunerTuningPresetFromUi);
    }

    private void CycleAudioInputDevice(int delta)
    {
        if (owner == null)
            return;

        IReadOnlyList<string> choices = owner.GetSharedAudioInputDeviceChoices();
        CycleStringChoice(choices, owner.GetSharedAudioSelectedInputLabel(), delta, owner.SetSharedAudioInputDeviceFromUi);
    }

    private void CycleAudioInputChannel(int delta)
    {
        if (owner == null)
            return;

        CycleStringChoice(SharedAudioInputChannelModes.Choices, owner.GetSharedAudioInputChannelModeLabel(), delta, owner.SetSharedAudioInputChannelModeFromUi);
    }

    private static void CycleStringChoice(IReadOnlyList<string> rawChoices, string selected, int delta, Action<string> setter)
    {
        if (rawChoices == null || setter == null)
            return;

        List<string> choices = rawChoices
            .Where(choice => !string.IsNullOrWhiteSpace(choice))
            .ToList();
        if (choices.Count == 0)
            return;

        int index = choices.FindIndex(choice => string.Equals(choice, selected, StringComparison.Ordinal));
        if (index < 0)
            index = 0;
        setter(choices[WrapIndex(index + delta, choices.Count)]);
    }

    private static bool IsElementVisible(VisualElement element)
    {
        for (VisualElement current = element; current != null; current = current.parent)
        {
            if (current.style.display.value == DisplayStyle.None || current.resolvedStyle.display == DisplayStyle.None)
                return false;
        }

        return element != null;
    }

    private static int WrapIndex(int index, int count)
    {
        if (count <= 0)
            return 0;

        int wrapped = index % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }

    private void RefreshPegHighlights(GuitarTunerSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        int targetCount = snapshot.targets != null && snapshot.targets.Length > 0 ? snapshot.targets.Length : 6;
        int selectedIndex = Mathf.Clamp(snapshot.selectedTargetIndex, 0, Mathf.Max(0, targetCount - 1));
        GuitarTunerTarget selectedTarget = snapshot.targets != null && selectedIndex < snapshot.targets.Length ? snapshot.targets[selectedIndex] : null;
        modelPreview?.SetActiveTuningPeg(selectedIndex, GetTargetStringColor(selectedTarget));
    }

    private static void PositionTargetButton(Button button, int targetIndex, int targetCount)
    {
        if (button == null || targetIndex < 0 || targetIndex >= TargetButtonAnchorPositions.Length)
            return;

        Vector2 anchor = GetTargetButtonAnchor(targetIndex, targetCount);
        button.style.position = Position.Absolute;
        button.style.left = Length.Percent(anchor.x * 100f);
        button.style.top = Length.Percent(anchor.y * 100f);
        button.style.marginLeft = -(TargetButtonWidth + TargetButtonMargin);
        button.style.marginTop = -TargetButtonHeight * 0.5f;
        button.pickingMode = PickingMode.Position;
    }

    private static Vector2 GetTargetButtonAnchor(int targetIndex, int targetCount)
    {
        if (targetCount == TargetButtonAnchorPositions.Length && targetIndex < TargetButtonAnchorPositions.Length)
            return TargetButtonAnchorPositions[targetIndex];

        int safeCount = Mathf.Clamp(targetCount, 1, TargetButtonAnchorPositions.Length);
        float t = safeCount <= 1 ? 0f : targetIndex / (float)(safeCount - 1);
        Vector2 lowStringAnchor = TargetButtonAnchorPositions[0];
        Vector2 highStringAnchor = TargetButtonAnchorPositions[TargetButtonAnchorPositions.Length - 1];
        return Vector2.Lerp(lowStringAnchor, highStringAnchor, Mathf.Clamp01(t));
    }

    private static string FormatCents(float cents)
    {
        if (Mathf.Abs(cents) < 0.05f)
            return "0 cents";

        string sign = cents > 0f ? "+" : string.Empty;
        return $"{sign}{cents.ToString("F0", CultureInfo.InvariantCulture)} cents";
    }

    private static float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private Label CreateLabel(string text, float size, Color color, bool bold, TextAnchor alignment)
    {
        Label label = new Label(text);
        label.style.fontSize = size;
        label.style.color = color;
        label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        label.style.unityTextAlign = alignment;
        label.style.unityFontDefinition = fontDefinition;
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private Label CreateFieldLabel(string text)
    {
        Label label = CreateLabel(text, 15f, new Color(0.66f, 0.76f, 0.86f, 0.90f), true, TextAnchor.MiddleLeft);
        label.style.marginTop = 10f;
        label.style.marginBottom = 6f;
        label.style.letterSpacing = 1.2f;
        return label;
    }

    private void StyleSegmentButton(Button button)
    {
        StyleModeButton(button, selected: false);
        button.style.minWidth = 132f;
        button.style.height = 48f;
        button.style.fontSize = 20f;
        button.style.unityFontDefinition = fontDefinition;
    }

    private void StyleInstrumentButton(Button button, bool selected)
    {
        if (button == null)
            return;

        Color accent = selected
            ? new Color(0.62f, 0.80f, 1f, 1f)
            : new Color(0.28f, 0.36f, 0.44f, 0.72f);
        button.focusable = false;
        button.style.minWidth = 108f;
        button.style.height = 44f;
        button.style.paddingLeft = 14f;
        button.style.paddingRight = 14f;
        button.style.fontSize = 18f;
        button.style.unityFontDefinition = fontDefinition;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.backgroundColor = selected
            ? new Color(0.18f, 0.30f, 0.44f, 0.72f)
            : new Color(0.03f, 0.06f, 0.09f, 0.62f);
        button.style.color = selected ? Color.white : new Color(0.76f, 0.84f, 0.92f, 0.94f);
        button.style.borderTopWidth = 2f;
        button.style.borderRightWidth = 2f;
        button.style.borderBottomWidth = 2f;
        button.style.borderLeftWidth = 2f;
        button.style.borderTopColor = accent;
        button.style.borderRightColor = accent;
        button.style.borderBottomColor = accent;
        button.style.borderLeftColor = accent;
        button.style.borderTopLeftRadius = 8f;
        button.style.borderTopRightRadius = 8f;
        button.style.borderBottomLeftRadius = 8f;
        button.style.borderBottomRightRadius = 8f;
    }

    private void EnableButtonInteraction(Button button, float hoverScale = 1.045f, float pressedScale = 0.955f)
    {
        if (button == null)
            return;

        button.focusable = false;
        button.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(50f), 0f);

        interactiveElementStates[button] = new TunerInteractionState
        {
            HoverScale = hoverScale,
            PressedScale = pressedScale
        };

        button.RegisterCallback<PointerEnterEvent>(_ =>
        {
            hoveredInteractiveElements.Add(button);
            ApplyInteractiveVisual(button, IsNavigationHighlighted(button));
        });
        button.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            hoveredInteractiveElements.Remove(button);
            pressedInteractiveElements.Remove(button);
            ApplyInteractiveVisual(button, IsNavigationHighlighted(button));
        });
        button.RegisterCallback<PointerDownEvent>(_ =>
        {
            pressedInteractiveElements.Add(button);
            ApplyInteractiveVisual(button, IsNavigationHighlighted(button));
        });
        button.RegisterCallback<PointerUpEvent>(_ =>
        {
            pressedInteractiveElements.Remove(button);
            ApplyInteractiveVisual(button, IsNavigationHighlighted(button));
        });
        button.RegisterCallback<BlurEvent>(_ =>
        {
            pressedInteractiveElements.Remove(button);
            ApplyInteractiveVisual(button, IsNavigationHighlighted(button));
        });
        button.RegisterCallback<DetachFromPanelEvent>(_ =>
        {
            hoveredInteractiveElements.Remove(button);
            pressedInteractiveElements.Remove(button);
            interactiveElementStates.Remove(button);
        });

        ApplyInteractiveVisual(button, navigationHighlighted: false);
    }

    private void ApplyInteractiveVisual(VisualElement element, bool navigationHighlighted)
    {
        if (element == null)
            return;

        TunerInteractionState state = interactiveElementStates.TryGetValue(element, out TunerInteractionState storedState)
            ? storedState
            : new TunerInteractionState { HoverScale = 1.045f, PressedScale = 0.955f };
        bool pressed = pressedInteractiveElements.Contains(element);
        bool hovered = hoveredInteractiveElements.Contains(element);
        float scale = pressed
            ? state.PressedScale
            : (hovered || navigationHighlighted ? state.HoverScale : 1f);
        element.style.scale = new Scale(new Vector3(scale, scale, 1f));
        element.style.opacity = pressed ? 0.90f : 1f;
    }

    private bool IsNavigationHighlighted(VisualElement element)
    {
        if (element == null || !navigationActive || navigationEntries.Count == 0)
            return false;

        int index = Mathf.Clamp(navigationIndex, 0, navigationEntries.Count - 1);
        return navigationEntries[index].Element == element;
    }

    private void StyleTuningToggleButton(Button button, bool enabled)
    {
        if (button == null)
            return;

        Color accent = enabled
            ? new Color(0.34f, 0.98f, 0.84f, 1f)
            : new Color(0.86f, 0.58f, 0.32f, 1f);
        button.focusable = false;
        button.style.height = 48f;
        button.style.fontSize = 19f;
        button.style.unityFontDefinition = fontDefinition;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.backgroundColor = new Color(accent.r, accent.g, accent.b, enabled ? 0.20f : 0.14f);
        button.style.color = Color.white;
        button.style.borderTopWidth = 2f;
        button.style.borderRightWidth = 2f;
        button.style.borderBottomWidth = 2f;
        button.style.borderLeftWidth = 2f;
        button.style.borderTopColor = new Color(accent.r, accent.g, accent.b, 0.82f);
        button.style.borderRightColor = new Color(accent.r, accent.g, accent.b, 0.82f);
        button.style.borderBottomColor = new Color(accent.r, accent.g, accent.b, 0.82f);
        button.style.borderLeftColor = new Color(accent.r, accent.g, accent.b, 0.82f);
        button.style.borderTopLeftRadius = 8f;
        button.style.borderTopRightRadius = 8f;
        button.style.borderBottomLeftRadius = 8f;
        button.style.borderBottomRightRadius = 8f;
    }

    private void StylePanelActionButton(Button button)
    {
        if (button == null)
            return;

        button.focusable = false;
        button.style.height = 44f;
        button.style.fontSize = 17f;
        button.style.unityFontDefinition = fontDefinition;
        button.style.backgroundColor = new Color(0.03f, 0.06f, 0.09f, 0.58f);
        button.style.color = new Color(0.90f, 0.95f, 0.98f, 0.96f);
        button.style.borderTopWidth = 1f;
        button.style.borderRightWidth = 1f;
        button.style.borderBottomWidth = 1f;
        button.style.borderLeftWidth = 1f;
        button.style.borderTopColor = new Color(0.44f, 0.56f, 0.66f, 0.68f);
        button.style.borderRightColor = new Color(0.44f, 0.56f, 0.66f, 0.68f);
        button.style.borderBottomColor = new Color(0.44f, 0.56f, 0.66f, 0.68f);
        button.style.borderLeftColor = new Color(0.44f, 0.56f, 0.66f, 0.68f);
        button.style.borderTopLeftRadius = 8f;
        button.style.borderTopRightRadius = 8f;
        button.style.borderBottomLeftRadius = 8f;
        button.style.borderBottomRightRadius = 8f;
    }

    private void StyleSettingsDropdown(DropdownField dropdown)
    {
        if (dropdown == null)
            return;

        dropdown.focusable = false;
        dropdown.style.height = 48f;
        dropdown.style.marginBottom = 4f;
        dropdown.style.unityFontDefinition = fontDefinition;
        dropdown.style.fontSize = 17f;
        dropdown.style.color = new Color(0.92f, 0.96f, 1f, 0.96f);
        dropdown.style.backgroundColor = new Color(0.03f, 0.06f, 0.09f, 0.70f);
        dropdown.style.borderTopWidth = 1f;
        dropdown.style.borderRightWidth = 1f;
        dropdown.style.borderBottomWidth = 1f;
        dropdown.style.borderLeftWidth = 1f;
        dropdown.style.borderTopColor = new Color(0.35f, 0.47f, 0.58f, 0.70f);
        dropdown.style.borderRightColor = new Color(0.35f, 0.47f, 0.58f, 0.70f);
        dropdown.style.borderBottomColor = new Color(0.35f, 0.47f, 0.58f, 0.70f);
        dropdown.style.borderLeftColor = new Color(0.35f, 0.47f, 0.58f, 0.70f);
        dropdown.style.borderTopLeftRadius = 8f;
        dropdown.style.borderTopRightRadius = 8f;
        dropdown.style.borderBottomLeftRadius = 8f;
        dropdown.style.borderBottomRightRadius = 8f;
        dropdown.RegisterCallback<GeometryChangedEvent>(_ =>
        {
            VisualElement inputElement = dropdown.Q(className: "unity-base-field__input");
            if (inputElement != null)
            {
                inputElement.style.backgroundColor = new Color(0.03f, 0.06f, 0.09f, 0.70f);
                inputElement.style.color = new Color(0.92f, 0.96f, 1f, 0.96f);
            }

            Label textLabel = dropdown.Q<Label>(className: "unity-base-popup-field__text");
            if (textLabel != null)
            {
                textLabel.style.color = new Color(0.92f, 0.96f, 1f, 0.96f);
                textLabel.style.fontSize = 17f;
            }

            VisualElement arrowElement = dropdown.Q(className: "unity-base-popup-field__arrow");
            if (arrowElement != null)
                arrowElement.style.unityBackgroundImageTintColor = new Color(0.78f, 0.88f, 0.96f, 0.96f);
        });
    }

    private static void StyleModeButton(Button button, bool selected)
    {
        if (button == null)
            return;

        Color accent = new Color(0.34f, 0.98f, 0.84f, 1f);
        Color border = selected ? accent : new Color(0.28f, 0.36f, 0.44f, 0.72f);
        button.style.backgroundColor = selected
            ? new Color(accent.r, accent.g, accent.b, 0.20f)
            : new Color(0.03f, 0.06f, 0.09f, 0.72f);
        button.style.color = selected ? Color.white : new Color(0.76f, 0.84f, 0.92f, 0.94f);
        button.style.borderTopWidth = 2f;
        button.style.borderRightWidth = 2f;
        button.style.borderBottomWidth = 2f;
        button.style.borderLeftWidth = 2f;
        button.style.borderTopColor = border;
        button.style.borderRightColor = border;
        button.style.borderBottomColor = border;
        button.style.borderLeftColor = border;
        button.style.borderTopLeftRadius = 8f;
        button.style.borderTopRightRadius = 8f;
        button.style.borderBottomLeftRadius = 8f;
        button.style.borderBottomRightRadius = 8f;
    }

    private void StyleTargetButton(Button button, GuitarTunerTarget target, bool selected, bool tuned)
    {
        if (button == null)
            return;

        Color stringColor = GetTargetStringColor(target);
        Color accent = tuned || selected ? stringColor : new Color(stringColor.r, stringColor.g, stringColor.b, 0.72f);
        button.style.width = TargetButtonWidth;
        button.style.minWidth = TargetButtonWidth;
        button.style.height = TargetButtonHeight;
        button.style.fontSize = tuned ? 17f : 22f;
        button.style.unityFontDefinition = fontDefinition;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.whiteSpace = WhiteSpace.Normal;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.backgroundColor = selected
            ? new Color(stringColor.r, stringColor.g, stringColor.b, tuned ? 0.28f : 0.18f)
            : tuned
                ? new Color(stringColor.r, stringColor.g, stringColor.b, 0.16f)
                : new Color(0.03f, 0.06f, 0.09f, 0.64f);
        button.style.color = stringColor;
        button.style.borderTopWidth = tuned ? 3f : 2f;
        button.style.borderRightWidth = tuned ? 3f : 2f;
        button.style.borderBottomWidth = tuned ? 5f : 2f;
        button.style.borderLeftWidth = tuned ? 3f : 2f;
        button.style.borderTopColor = accent;
        button.style.borderRightColor = accent;
        button.style.borderBottomColor = accent;
        button.style.borderLeftColor = accent;
        button.style.borderTopLeftRadius = 8f;
        button.style.borderTopRightRadius = 8f;
        button.style.borderBottomLeftRadius = 8f;
        button.style.borderBottomRightRadius = 8f;
    }

    private Color GetTargetStringColor(GuitarTunerTarget target)
    {
        int stringIndex = target != null ? target.stringIndex : -1;
        if (owner != null)
            return owner.GetStringColor(stringIndex);

        switch (stringIndex)
        {
            case 0: return new Color(0.91f, 0.30f, 0.24f, 1f);
            case 1: return new Color(0.95f, 0.77f, 0.06f, 1f);
            case 2: return new Color(0.20f, 0.60f, 0.86f, 1f);
            case 3: return new Color(0.90f, 0.49f, 0.13f, 1f);
            case 4: return new Color(0.18f, 0.80f, 0.44f, 1f);
            case 5: return new Color(0.61f, 0.35f, 0.71f, 1f);
            default: return new Color(0.76f, 0.84f, 0.92f, 0.94f);
        }
    }

    private void StyleOutlineButton(Button button, float minWidth, float height, Color textColor, Color borderColor)
    {
        button.focusable = false;
        button.style.minWidth = minWidth;
        button.style.height = height;
        button.style.paddingLeft = 18f;
        button.style.paddingRight = 18f;
        button.style.fontSize = 24f;
        button.style.unityFontDefinition = fontDefinition;
        button.style.backgroundColor = new Color(0.03f, 0.06f, 0.09f, 0.54f);
        button.style.color = textColor;
        button.style.borderTopWidth = 2f;
        button.style.borderRightWidth = 2f;
        button.style.borderBottomWidth = 2f;
        button.style.borderLeftWidth = 2f;
        button.style.borderTopColor = borderColor;
        button.style.borderRightColor = borderColor;
        button.style.borderBottomColor = borderColor;
        button.style.borderLeftColor = borderColor;
        button.style.borderTopLeftRadius = 8f;
        button.style.borderTopRightRadius = 8f;
        button.style.borderBottomLeftRadius = 8f;
        button.style.borderBottomRightRadius = 8f;
    }

    private static PanelSettings ResolvePanelSettings()
    {
        PanelSettings runtimeAsset = Resources.Load<PanelSettings>("UIToolkitRuntimePanelSettings");
        PanelSettings settings = runtimeAsset != null
            ? ScriptableObject.Instantiate(runtimeAsset)
            : ScriptableObject.CreateInstance<PanelSettings>();
        settings.name = "GuitarTunerPanelSettings";
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = new Vector2Int(1920, 1080);
        settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        settings.match = 0.5f;
        settings.scale = 1f;
        settings.targetDisplay = 0;
        settings.sortingOrder = 235;

        if (settings.themeStyleSheet == null)
            settings.themeStyleSheet = Resources.FindObjectsOfTypeAll<ThemeStyleSheet>().FirstOrDefault();
        if (settings.textSettings == null)
            settings.textSettings = Resources.FindObjectsOfTypeAll<PanelTextSettings>().FirstOrDefault();

        return settings;
    }

    private static bool DropdownChoicesMatch(DropdownField dropdown, IReadOnlyList<string> expectedChoices)
    {
        if (dropdown == null || dropdown.choices == null || expectedChoices == null)
            return false;
        if (dropdown.choices.Count != expectedChoices.Count)
            return false;

        for (int i = 0; i < expectedChoices.Count; i++)
        {
            if (!string.Equals(dropdown.choices[i], expectedChoices[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private void OnDestroy()
    {
        modelPreview?.Dispose();
        modelPreview = null;

        if (panelSettings != null)
            Destroy(panelSettings);
    }
}
