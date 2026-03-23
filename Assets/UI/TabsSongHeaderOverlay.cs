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
    private static readonly Color[] LogoStringColors =
    {
        new Color(0.91f, 0.30f, 0.24f, 1f),
        new Color(0.95f, 0.77f, 0.06f, 1f),
        new Color(0.20f, 0.60f, 0.86f, 1f),
        new Color(0.90f, 0.49f, 0.13f, 1f),
        new Color(0.18f, 0.80f, 0.44f, 1f),
        new Color(0.61f, 0.35f, 0.71f, 1f)
    };

    private readonly GuitarBridgeServer owner;
    private readonly GameObject rootObject;
    private readonly UIDocument document;
    private readonly PanelSettings panelSettings;

    private readonly VisualElement songCard;
    private readonly Label songNameLabel;
    private readonly Label trackNameLabel;
    private readonly Label speedBadgeLabel;
    private readonly Label statusDotLabel;
    private readonly Label detectorStatusLabel;
    private readonly VisualElement techniqueLegendCard;
    private readonly List<Label> techniqueLegendIconLabels = new List<Label>();
    private readonly List<Label> techniqueLegendTextLabels = new List<Label>();
    private readonly VisualElement scorePlate;
    private readonly VisualElement scorePedalBody;
    private readonly VisualElement scorePedalScreen;
    private readonly VisualElement scorePedalKnobLeft;
    private readonly VisualElement scorePedalKnobMid;
    private readonly VisualElement scorePedalKnobRight;
    private readonly VisualElement scorePedalLed;
    private readonly VisualElement scorePedalFootswitch;
    private readonly VisualElement scorePedalFootswitchRight;
    private readonly VisualElement scorePedalInputJack;
    private readonly VisualElement scorePedalOutputJack;
    private readonly Label scorePedalBrandLabel;
    private readonly Label scoreTitleLabel;
    private readonly Label scorePercentLabel;
    private readonly Label noteTallyLabel;
    private readonly VisualElement inputMeterWrap;
    private readonly Label inputMeterLabel;
    private readonly VisualElement inputMeterFace;
    private readonly VisualElement inputMeterArcViewport;
    private readonly VisualElement inputMeterArc;
    private readonly VisualElement inputMeterNeedle;
    private readonly VisualElement inputMeterNeedleCap;
    private readonly VisualElement songProgressTrack;
    private readonly VisualElement songProgressFill;
    private readonly List<VisualElement> inputMeterTicks = new List<VisualElement>();
    private readonly VisualElement judgePopupLayer;

    private readonly List<JudgePopupEntry> activeJudgePopups = new List<JudgePopupEntry>();

    private sealed class SongSelectionRow
    {
        public Button button;
        public Label nameLabel;
        public Label scoreLabel;
    }

    private sealed class TrackSelectionRow
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

    private sealed class EnumCycleControl : VisualElement
    {
        private readonly List<string> options;
        private readonly Label valueLabel;
        private bool suppress;

        public Action<string> OnValueChanged;

        public EnumCycleControl(IEnumerable<string> enumOptions, string initialValue, Func<string, float, Color, bool, TextAnchor, bool, Label> createLabel, Func<string, Action, Button> createButton)
        {
            options = enumOptions?.Where(option => !string.IsNullOrWhiteSpace(option)).Distinct().ToList() ?? new List<string>();
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.marginBottom = 6f;

            Button prev = createButton("◀", () => Shift(-1));
            prev.style.minWidth = 90f;
            prev.style.height = 58f;
            prev.style.marginRight = 8f;

            valueLabel = createLabel(string.Empty, 34f, new Color(0.90f, 0.96f, 1f, 1f), true, TextAnchor.MiddleCenter, false);
            valueLabel.style.flexGrow = 1f;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            valueLabel.AddToClassList("global-setting-enum-value");

            Button next = createButton("▶", () => Shift(1));
            next.style.minWidth = 90f;
            next.style.height = 58f;
            next.style.marginLeft = 8f;

            Add(prev);
            Add(valueLabel);
            Add(next);

            SetValueWithoutNotify(initialValue);
        }

        public void SetValueWithoutNotify(string value)
        {
            if (options.Count == 0)
            {
                valueLabel.text = string.IsNullOrEmpty(value) ? "--" : value;
                return;
            }

            string resolved = options.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) ?? options[0];
            suppress = true;
            valueLabel.text = resolved;
            suppress = false;
        }

        private void Shift(int delta)
        {
            if (options.Count == 0)
                return;

            string current = valueLabel.text;
            int currentIndex = options.FindIndex(option => string.Equals(option, current, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
                currentIndex = 0;

            int nextIndex = (currentIndex + delta + options.Count) % options.Count;
            string next = options[nextIndex];
            valueLabel.text = next;
            if (!suppress)
                OnValueChanged?.Invoke(next);
        }
    }

    private readonly VisualElement pauseOverlay;
    private readonly Label pauseTitleLabel;
    private readonly Label pauseHintLabel;
    private readonly Label pauseInfoLabel;
    private readonly Button loopButton;

    private readonly VisualElement mainMenuOverlay;
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
    private readonly VisualElement globalSettingsCard;
    private readonly ScrollView globalSettingsScrollView;
    private readonly Button resetDefaultsButton;
    private readonly Dictionary<string, VisualElement> globalSettingInputs = new Dictionary<string, VisualElement>();
    private readonly Dictionary<string, Label> globalSettingValueLabels = new Dictionary<string, Label>();
    private readonly Dictionary<string, VisualElement> globalSettingsColumns = new Dictionary<string, VisualElement>();

    private readonly VisualElement selectionOverlay;
    private readonly Label selectionSubtitleLabel;
    private readonly ScrollView selectionScrollView;
    private readonly List<SongSelectionRow> selectionRows = new List<SongSelectionRow>();

    private readonly VisualElement trackSelectionOverlay;
    private readonly Label trackSelectionTitleLabel;
    private readonly Label trackSelectionSubtitleLabel;
    private readonly ScrollView trackSelectionScrollView;
    private readonly List<TrackSelectionRow> trackSelectionRows = new List<TrackSelectionRow>();

    private readonly VisualElement songEndOverlay;
    private readonly VisualElement songEndCard;
    private readonly Label songEndTitleLabel;
    private readonly Label songEndSongLabel;
    private readonly Label songEndMetaLabel;
    private readonly Label songEndSpeedValueLabel;
    private readonly Label songEndScoreLabel;
    private readonly Label songEndBestLabel;
    private readonly Label songEndDeltaLabel;
    private readonly Label songEndRatingLabel;
    private readonly Label songEndStatsLabel;

    private readonly VisualElement startupTuningReminderOverlay;

    private int lastScreenHeight = -1;
    private bool suppressCallbacks;
    private bool hasSeenSnapshot;
    private int lastResolvedCount;
    private int hitStreak;
    private float judgePopupFontSize = 82f;
    private float displayedInputMeterLevel;
    private int lastAutoScrolledSongIndex = -1;
    private int lastAutoScrolledTrackIndex = -1;

    private readonly HashSet<int> scoredNoteIds = new HashSet<int>();
    private int scoreHits;
    private int scoreMisses;
    private float lastSongTime = -1f;
    private bool wasLoopEnabled;
    private string lastLoopSignature = string.Empty;
    private readonly FontDefinition bodyFontDefinition;
    private readonly FontDefinition titleFontDefinition;
    private string globalSettingsLayoutSignature = string.Empty;
    private Vector2 globalSettingsScrollOffset = Vector2.zero;

    public TabsSongHeaderOverlay(GuitarBridgeServer owner)
    {
        this.owner = owner;

        GameObject existingHeaderUi = GameObject.Find("TabsSongHeaderUI");
        if (existingHeaderUi != null)
            UnityEngine.Object.Destroy(existingHeaderUi);

        rootObject = new GameObject("TabsSongHeaderUI");
        document = rootObject.AddComponent<UIDocument>();

        panelSettings = ResolvePanelSettings();
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
        songCard.style.minWidth = 560f;
        songCard.style.maxWidth = 960f;
        songCard.style.paddingLeft = 34f;
        songCard.style.paddingRight = 34f;
        songCard.style.paddingTop = 22f;
        songCard.style.paddingBottom = 22f;
        songCard.style.marginBottom = 14f;
        songCard.style.marginRight = 24f;
        StyleCard(songCard, new Color(0.04f, 0.06f, 0.13f, 0.96f), radius: 18f);
        songCard.style.borderBottomWidth = 5f;
        songCard.style.borderBottomColor = new Color(0.16f, 0.12f, 0.42f, 0.98f);

        songNameLabel = CreateLabel("Song", 42f, Color.white, bold: true, useTitleFont: true);
        songNameLabel.style.marginBottom = 8f;
        songNameLabel.style.letterSpacing = 0.7f;
        songNameLabel.style.whiteSpace = WhiteSpace.Normal;
        songNameLabel.style.textOverflow = TextOverflow.Clip;
        songNameLabel.style.overflow = Overflow.Visible;
        songNameLabel.style.maxWidth = 1200f;

        VisualElement compactSongCardLogo = CreateStringTheoryLogo(34f, 32f, 22f, 0.7f, -4f, 1f);
        compactSongCardLogo.style.alignSelf = Align.FlexEnd;
        compactSongCardLogo.style.marginBottom = 6f;

        trackNameLabel = CreateLabel("Lead Guitar", 26f, new Color(0.72f, 0.93f, 1f, 1f), bold: false);
        trackNameLabel.style.letterSpacing = 0.2f;
        trackNameLabel.style.whiteSpace = WhiteSpace.NoWrap;
        trackNameLabel.style.textOverflow = TextOverflow.Ellipsis;
        trackNameLabel.style.overflow = Overflow.Hidden;
        trackNameLabel.style.maxWidth = 1200f;

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

        techniqueLegendCard = new VisualElement();
        techniqueLegendCard.style.position = Position.Absolute;
        techniqueLegendCard.style.top = 24f;
        techniqueLegendCard.style.right = 24f;
        techniqueLegendCard.style.paddingLeft = 16f;
        techniqueLegendCard.style.paddingRight = 16f;
        techniqueLegendCard.style.paddingTop = 12f;
        techniqueLegendCard.style.paddingBottom = 12f;
        techniqueLegendCard.style.alignItems = Align.FlexStart;
        techniqueLegendCard.style.minWidth = 255f;
        techniqueLegendCard.style.display = DisplayStyle.None;
        StyleCard(techniqueLegendCard, new Color(0.03f, 0.07f, 0.14f, 0.90f), radius: 14f);
        techniqueLegendCard.style.borderTopWidth = 1f;
        techniqueLegendCard.style.borderRightWidth = 1f;
        techniqueLegendCard.style.borderBottomWidth = 1f;
        techniqueLegendCard.style.borderLeftWidth = 1f;
        Color legendBorder = new Color(0.41f, 0.65f, 0.93f, 0.55f);
        techniqueLegendCard.style.borderTopColor = legendBorder;
        techniqueLegendCard.style.borderRightColor = legendBorder;
        techniqueLegendCard.style.borderBottomColor = legendBorder;
        techniqueLegendCard.style.borderLeftColor = legendBorder;

        AddTechniqueLegendRow("H", "Hammer-on", new Color(0.55f, 0.91f, 1f, 1f));
        AddTechniqueLegendRow("P", "Pull-off", new Color(0.57f, 1f, 0.74f, 1f));
        AddTechniqueLegendRow("/", "Slide up", new Color(1f, 0.89f, 0.48f, 1f));
        AddTechniqueLegendRow("\\", "Slide down", new Color(1f, 0.78f, 0.48f, 1f));
        AddTechniqueLegendRow("^", "Bend", new Color(1f, 0.66f, 0.73f, 1f));
        AddTechniqueLegendRow("~", "Vibrato", new Color(0.83f, 0.73f, 1f, 1f));

        scorePlate = new VisualElement();
        scorePlate.style.position = Position.Absolute;
        scorePlate.style.top = 8f;
        scorePlate.style.left = 0f;
        scorePlate.style.right = 0f;
        scorePlate.style.alignItems = Align.Center;
        scorePlate.style.justifyContent = Justify.Center;
        scorePlate.style.height = 252f;

        scorePedalBody = new VisualElement();
        scorePedalBody.style.width = 600f;
        scorePedalBody.style.height = 226f;
        scorePedalBody.style.paddingTop = 10f;
        scorePedalBody.style.paddingBottom = 12f;
        scorePedalBody.style.paddingLeft = 14f;
        scorePedalBody.style.paddingRight = 14f;
        scorePedalBody.style.backgroundColor = new Color(0.07f, 0.57f, 0.62f, 0.98f);
        scorePedalBody.style.borderTopWidth = 4f;
        scorePedalBody.style.borderRightWidth = 4f;
        scorePedalBody.style.borderBottomWidth = 12f;
        scorePedalBody.style.borderLeftWidth = 4f;
        Color pedalBorderColor = new Color(0.05f, 0.40f, 0.45f, 0.98f);
        scorePedalBody.style.borderTopColor = pedalBorderColor;
        scorePedalBody.style.borderRightColor = pedalBorderColor;
        scorePedalBody.style.borderBottomColor = pedalBorderColor;
        scorePedalBody.style.borderLeftColor = pedalBorderColor;
        scorePedalBody.style.borderTopLeftRadius = 12f;
        scorePedalBody.style.borderTopRightRadius = 12f;
        scorePedalBody.style.borderBottomLeftRadius = 18f;
        scorePedalBody.style.borderBottomRightRadius = 18f;
        scorePedalBody.style.alignItems = Align.Stretch;

        scorePedalInputJack = CreatePedalJack();
        scorePedalInputJack.style.position = Position.Absolute;
        scorePedalInputJack.style.left = -24f;
        scorePedalInputJack.style.top = 102f;

        scorePedalOutputJack = CreatePedalJack();
        scorePedalOutputJack.style.position = Position.Absolute;
        scorePedalOutputJack.style.scale = new Scale(new Vector3(-1f, 1f, 1f));
        scorePedalOutputJack.style.right = -24f;
        scorePedalOutputJack.style.top = 102f;

        VisualElement pedalFace = new VisualElement();
        pedalFace.style.flexGrow = 1f;
        pedalFace.style.paddingTop = 8f;
        pedalFace.style.paddingBottom = 8f;
        pedalFace.style.paddingLeft = 10f;
        pedalFace.style.paddingRight = 10f;
        pedalFace.style.borderTopWidth = 3f;
        pedalFace.style.borderRightWidth = 3f;
        pedalFace.style.borderBottomWidth = 3f;
        pedalFace.style.borderLeftWidth = 3f;
        pedalFace.style.borderTopColor = new Color(0.99f, 0.99f, 0.99f, 0.98f);
        pedalFace.style.borderRightColor = new Color(0.94f, 0.98f, 0.99f, 0.95f);
        pedalFace.style.borderBottomColor = new Color(0.88f, 0.95f, 0.97f, 0.93f);
        pedalFace.style.borderLeftColor = new Color(0.94f, 0.98f, 0.99f, 0.95f);
        pedalFace.style.borderTopLeftRadius = 8f;
        pedalFace.style.borderTopRightRadius = 8f;
        pedalFace.style.borderBottomLeftRadius = 12f;
        pedalFace.style.borderBottomRightRadius = 12f;

        VisualElement pedalTopRow = new VisualElement();
        pedalTopRow.style.flexDirection = FlexDirection.Row;
        pedalTopRow.style.alignItems = Align.Center;
        pedalTopRow.style.justifyContent = Justify.SpaceBetween;
        pedalTopRow.style.marginBottom = 12f;

        scorePedalBrandLabel = CreateLabel("STRING THEORY", 18f, new Color(0.95f, 0.99f, 1f, 0.98f), true, TextAnchor.MiddleLeft, useTitleFont: true);
        scorePedalBrandLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        scorePedalBrandLabel.style.letterSpacing = 0.9f;
        scorePedalBrandLabel.style.marginBottom = 4f;

        scorePedalLed = new VisualElement();
        scorePedalLed.style.width = 14f;
        scorePedalLed.style.height = 14f;
        scorePedalLed.style.backgroundColor = new Color(1f, 0.35f, 0.22f, 1f);
        scorePedalLed.style.borderTopLeftRadius = 7f;
        scorePedalLed.style.borderTopRightRadius = 7f;
        scorePedalLed.style.borderBottomLeftRadius = 7f;
        scorePedalLed.style.borderBottomRightRadius = 7f;
        scorePedalLed.style.borderTopWidth = 2f;
        scorePedalLed.style.borderRightWidth = 2f;
        scorePedalLed.style.borderBottomWidth = 2f;
        scorePedalLed.style.borderLeftWidth = 2f;
        scorePedalLed.style.borderTopColor = new Color(1f, 0.72f, 0.62f, 1f);
        scorePedalLed.style.borderRightColor = new Color(0.66f, 0.14f, 0.10f, 1f);
        scorePedalLed.style.borderBottomColor = new Color(0.53f, 0.10f, 0.08f, 1f);
        scorePedalLed.style.borderLeftColor = new Color(0.66f, 0.14f, 0.10f, 1f);

        pedalTopRow.Add(scorePedalBrandLabel);
        pedalTopRow.Add(scorePedalLed);

        VisualElement pedalKnobRow = new VisualElement();
        pedalKnobRow.style.flexDirection = FlexDirection.Row;
        pedalKnobRow.style.justifyContent = Justify.SpaceAround;
        pedalKnobRow.style.alignItems = Align.Center;
        pedalKnobRow.style.marginBottom = 24f;

        scorePedalKnobLeft = CreatePedalKnob();
        scorePedalKnobMid = CreatePedalKnob();
        scorePedalKnobRight = CreatePedalKnob();
        SetKnobIndicatorAngle(scorePedalKnobLeft, -28f);
        SetKnobIndicatorAngle(scorePedalKnobMid, -8f);
        SetKnobIndicatorAngle(scorePedalKnobRight, 22f);
        pedalKnobRow.Add(scorePedalKnobLeft);
        pedalKnobRow.Add(scorePedalKnobMid);
        pedalKnobRow.Add(scorePedalKnobRight);

        scorePedalScreen = new VisualElement();
        scorePedalScreen.style.flexGrow = 0f;
        scorePedalScreen.style.paddingTop = 18f;
        scorePedalScreen.style.paddingBottom = 8f;
        scorePedalScreen.style.paddingLeft = 20f;
        scorePedalScreen.style.paddingRight = 20f;
        scorePedalScreen.style.marginBottom = 8f;
        scorePedalScreen.style.borderTopWidth = 3f;
        scorePedalScreen.style.borderRightWidth = 3f;
        scorePedalScreen.style.borderBottomWidth = 5f;
        scorePedalScreen.style.borderLeftWidth = 3f;
        scorePedalScreen.style.borderTopColor = new Color(0.72f, 0.89f, 0.79f, 1f);
        scorePedalScreen.style.borderRightColor = new Color(0.24f, 0.42f, 0.35f, 1f);
        scorePedalScreen.style.borderBottomColor = new Color(0.12f, 0.23f, 0.18f, 1f);
        scorePedalScreen.style.borderLeftColor = new Color(0.24f, 0.42f, 0.35f, 1f);
        scorePedalScreen.style.backgroundColor = new Color(0.70f, 0.88f, 0.76f, 0.95f);
        scorePedalScreen.style.borderTopLeftRadius = 8f;
        scorePedalScreen.style.borderTopRightRadius = 8f;
        scorePedalScreen.style.borderBottomLeftRadius = 8f;
        scorePedalScreen.style.borderBottomRightRadius = 8f;
        scorePedalScreen.style.alignItems = Align.Center;
        scorePedalScreen.style.justifyContent = Justify.FlexStart;
        scorePedalScreen.style.minHeight = 120f;
        scorePedalScreen.style.flexShrink = 0f;
        scorePedalScreen.style.overflow = Overflow.Hidden;

        inputMeterWrap = new VisualElement();
        inputMeterWrap.style.width = 240f;
        inputMeterWrap.style.alignItems = Align.Center;
        inputMeterWrap.style.justifyContent = Justify.Center;
        inputMeterWrap.style.marginBottom = 8f;
        inputMeterWrap.style.flexShrink = 0f;

        inputMeterLabel = CreateLabel("INPUT", 16f, new Color(0.08f, 0.28f, 0.29f, 0.9f), true, TextAnchor.MiddleCenter, useTitleFont: false);
        inputMeterLabel.style.letterSpacing = 0.9f;
        inputMeterLabel.style.marginBottom = 1f;

        inputMeterFace = new VisualElement();
        inputMeterFace.style.width = 220f;
        inputMeterFace.style.height = 84f;
        inputMeterFace.style.position = Position.Relative;
        inputMeterFace.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        inputMeterFace.style.borderTopWidth = 0f;
        inputMeterFace.style.borderRightWidth = 0f;
        inputMeterFace.style.borderBottomWidth = 0f;
        inputMeterFace.style.borderLeftWidth = 0f;
        inputMeterFace.style.borderTopColor = new Color(0f, 0f, 0f, 0f);
        inputMeterFace.style.borderRightColor = new Color(0f, 0f, 0f, 0f);
        inputMeterFace.style.borderBottomColor = new Color(0f, 0f, 0f, 0f);
        inputMeterFace.style.borderLeftColor = new Color(0f, 0f, 0f, 0f);
        inputMeterFace.style.borderTopLeftRadius = 6f;
        inputMeterFace.style.borderTopRightRadius = 6f;
        inputMeterFace.style.borderBottomLeftRadius = 4f;
        inputMeterFace.style.borderBottomRightRadius = 4f;
        inputMeterFace.style.flexShrink = 0f;

        inputMeterArcViewport = new VisualElement();
        inputMeterArcViewport.style.position = Position.Absolute;
        inputMeterArcViewport.style.left = 12f;
        inputMeterArcViewport.style.right = 12f;
        inputMeterArcViewport.style.top = 10f;
        inputMeterArcViewport.style.height = 36f;
        inputMeterArcViewport.style.overflow = Overflow.Hidden;

        inputMeterArc = new VisualElement();
        inputMeterArc.style.position = Position.Absolute;
        inputMeterArc.style.left = 0f;
        inputMeterArc.style.right = 0f;
        inputMeterArc.style.top = 0f;
        inputMeterArc.style.height = 72f;
        inputMeterArc.style.borderTopWidth = 3f;
        inputMeterArc.style.borderRightWidth = 3f;
        inputMeterArc.style.borderBottomWidth = 3f;
        inputMeterArc.style.borderLeftWidth = 3f;
        inputMeterArc.style.borderTopColor = new Color(0.07f, 0.23f, 0.24f, 0.94f);
        inputMeterArc.style.borderRightColor = new Color(0.07f, 0.23f, 0.24f, 0.94f);
        inputMeterArc.style.borderBottomColor = new Color(0.07f, 0.23f, 0.24f, 0.94f);
        inputMeterArc.style.borderLeftColor = new Color(0.07f, 0.23f, 0.24f, 0.94f);
        inputMeterArc.style.borderTopLeftRadius = 120f;
        inputMeterArc.style.borderTopRightRadius = 120f;
        inputMeterArc.style.borderBottomLeftRadius = 120f;
        inputMeterArc.style.borderBottomRightRadius = 120f;

        for (int i = 0; i <= 10; i++)
        {
            VisualElement tick = new VisualElement();
            bool major = i % 2 == 0;
            tick.style.position = Position.Absolute;
            tick.style.width = major ? 3f : 2f;
            tick.style.height = major ? 10f : 6f;
            tick.style.backgroundColor = major ? new Color(0.08f, 0.24f, 0.25f, 0.95f) : new Color(0.09f, 0.26f, 0.27f, 0.82f);
            tick.style.borderTopLeftRadius = 1f;
            tick.style.borderTopRightRadius = 1f;
            tick.style.borderBottomLeftRadius = 1f;
            tick.style.borderBottomRightRadius = 1f;
            inputMeterTicks.Add(tick);
            inputMeterFace.Add(tick);
        }

        inputMeterNeedle = new VisualElement();
        inputMeterNeedle.style.position = Position.Absolute;
        inputMeterNeedle.style.width = 3f;
        inputMeterNeedle.style.height = 30f;
        inputMeterNeedle.style.backgroundColor = new Color(0.05f, 0.19f, 0.20f, 0.98f);
        inputMeterNeedle.style.borderTopLeftRadius = 1f;
        inputMeterNeedle.style.borderTopRightRadius = 1f;
        inputMeterNeedle.style.borderBottomLeftRadius = 1f;
        inputMeterNeedle.style.borderBottomRightRadius = 1f;
        inputMeterNeedle.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(100f), 0f);
        inputMeterNeedle.style.rotate = new Rotate(new Angle(-65f, AngleUnit.Degree));

        inputMeterNeedleCap = new VisualElement();
        inputMeterNeedleCap.style.position = Position.Absolute;
        inputMeterNeedleCap.style.width = 12f;
        inputMeterNeedleCap.style.height = 12f;
        inputMeterNeedleCap.style.backgroundColor = new Color(0.07f, 0.23f, 0.24f, 0.98f);
        inputMeterNeedleCap.style.borderTopWidth = 2f;
        inputMeterNeedleCap.style.borderRightWidth = 2f;
        inputMeterNeedleCap.style.borderBottomWidth = 3f;
        inputMeterNeedleCap.style.borderLeftWidth = 2f;
        inputMeterNeedleCap.style.borderTopColor = new Color(0.12f, 0.30f, 0.31f, 0.92f);
        inputMeterNeedleCap.style.borderRightColor = new Color(0.04f, 0.15f, 0.16f, 0.95f);
        inputMeterNeedleCap.style.borderBottomColor = new Color(0.03f, 0.12f, 0.13f, 0.95f);
        inputMeterNeedleCap.style.borderLeftColor = new Color(0.04f, 0.15f, 0.16f, 0.95f);

        inputMeterArcViewport.Add(inputMeterArc);
        inputMeterFace.Add(inputMeterArcViewport);
        inputMeterFace.Add(inputMeterNeedle);
        inputMeterFace.Add(inputMeterNeedleCap);
        inputMeterWrap.Add(inputMeterLabel);
        inputMeterWrap.Add(inputMeterFace);

        songProgressTrack = new VisualElement();
        songProgressTrack.style.width = 220f;
        songProgressTrack.style.height = 10f;
        songProgressTrack.style.marginTop = 6f;
        songProgressTrack.style.backgroundColor = new Color(0.06f, 0.18f, 0.19f, 0.92f);
        songProgressTrack.style.borderTopLeftRadius = 5f;
        songProgressTrack.style.borderTopRightRadius = 5f;
        songProgressTrack.style.borderBottomLeftRadius = 5f;
        songProgressTrack.style.borderBottomRightRadius = 5f;
        songProgressTrack.style.borderTopWidth = 1f;
        songProgressTrack.style.borderRightWidth = 1f;
        songProgressTrack.style.borderBottomWidth = 1f;
        songProgressTrack.style.borderLeftWidth = 1f;
        Color progressBorderColor = new Color(0.09f, 0.30f, 0.31f, 0.95f);
        songProgressTrack.style.borderTopColor = progressBorderColor;
        songProgressTrack.style.borderRightColor = progressBorderColor;
        songProgressTrack.style.borderBottomColor = progressBorderColor;
        songProgressTrack.style.borderLeftColor = progressBorderColor;

        songProgressFill = new VisualElement();
        songProgressFill.style.width = 0f;
        songProgressFill.style.height = Length.Percent(100f);
        songProgressFill.style.backgroundColor = new Color(0.83f, 0.96f, 1f, 0.98f);
        songProgressFill.style.borderTopLeftRadius = 5f;
        songProgressFill.style.borderBottomLeftRadius = 5f;
        songProgressFill.style.borderTopRightRadius = 5f;
        songProgressFill.style.borderBottomRightRadius = 5f;
        songProgressTrack.Add(songProgressFill);

        inputMeterWrap.Add(songProgressTrack);
        LayoutInputMeterGraphics(220f, 84f);

        scoreTitleLabel = CreateLabel("SCORE", 20f, new Color(0.08f, 0.25f, 0.26f, 0.88f), true, TextAnchor.MiddleCenter, useTitleFont: false);
        scoreTitleLabel.style.letterSpacing = 1.6f;
        scoreTitleLabel.style.marginTop = 0f;
        scoreTitleLabel.style.marginBottom = 0f;
        scoreTitleLabel.style.flexShrink = 0f;

        scorePercentLabel = CreateLabel("0.0", 58f, new Color(0.06f, 0.20f, 0.21f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: true);
        scorePercentLabel.style.letterSpacing = 0.25f;
        scorePercentLabel.style.marginTop = -2f;
        scorePercentLabel.style.marginBottom = 1f;
        scorePercentLabel.style.flexShrink = 0f;

        noteTallyLabel = CreateLabel("HITS 0  •  MISS 0", 24f, new Color(0.09f, 0.22f, 0.21f, 0.95f), true, TextAnchor.MiddleCenter);
        noteTallyLabel.style.marginTop = 0f;
        noteTallyLabel.style.flexShrink = 0f;
        noteTallyLabel.style.letterSpacing = 0.2f;

        VisualElement pedalFooter = new VisualElement();
        pedalFooter.style.flexDirection = FlexDirection.Row;
        pedalFooter.style.justifyContent = Justify.Center;

        scorePedalFootswitch = CreateFootswitch();
        scorePedalFootswitchRight = CreateFootswitch();
        scorePedalFootswitch.style.marginRight = 36f;
        scorePedalFootswitchRight.style.marginLeft = 36f;

        scorePedalScreen.Add(inputMeterWrap);
        scorePedalScreen.Add(scoreTitleLabel);
        scorePedalScreen.Add(scorePercentLabel);
        scorePedalScreen.Add(noteTallyLabel);
        pedalFooter.Add(scorePedalFootswitch);
        pedalFooter.Add(scorePedalFootswitchRight);
        pedalFace.Add(pedalTopRow);
        pedalFace.Add(pedalKnobRow);
        pedalFace.Add(scorePedalScreen);
        pedalFace.Add(pedalFooter);
        scorePedalBody.Add(scorePedalInputJack);
        scorePedalBody.Add(scorePedalOutputJack);
        scorePedalBody.Add(pedalFace);
        scorePlate.Add(scorePedalBody);

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
        Button mainMenuButton = CreateActionButton("Main Menu", () => owner?.OpenMainMenuFromUi());
        Button resumeButton = CreateActionButton("Resume", () => owner?.ResumePlaybackFromUi());

        foreach (Button button in new[] { loopButton, songSelectButton, songSettingsButton, globalSettingsButton, toneLabButton })
        {
            button.style.marginRight = 10f;
            button.style.marginTop = 8f;
            pauseButtons.Add(button);
        }

        pauseCard.Add(pauseInfoLabel);
        pauseCard.Add(speedValueLabel);
        pauseCard.Add(speedSlider);
        pauseCard.Add(pauseButtons);
        AddBottomRightPrimaryButtons(pauseCard, mainMenuButton, resumeButton);
        pauseOverlay.Add(pauseStarsLabel);
        pauseOverlay.Add(pauseTitleLabel);
        pauseOverlay.Add(pauseHintLabel);
        pauseOverlay.Add(pauseCard);

        mainMenuOverlay = CreateFullscreenOverlay();
        Label mainMenuTopTag = CreateLabel("◉ INTERACTIVE MUSIC EXPERIENCE ◉", 30f, new Color(1f, 0.73f, 0.33f, 0.95f), true, TextAnchor.MiddleCenter, useTitleFont: false);
        mainMenuTopTag.style.marginBottom = 6f;
        mainMenuTopTag.style.letterSpacing = 1.4f;

        VisualElement logoWrap = new VisualElement();
        logoWrap.style.alignItems = Align.Center;
        logoWrap.style.marginBottom = 18f;

        logoWrap.Add(CreateStringTheoryLogo(132f, 124f, 84f, 2.2f, -8f, 2f));

        VisualElement mainMenuCard = new VisualElement();
        mainMenuCard.style.width = 1040f;
        mainMenuCard.style.maxWidth = 1200f;
        mainMenuCard.style.paddingLeft = 32f;
        mainMenuCard.style.paddingRight = 32f;
        mainMenuCard.style.paddingTop = 26f;
        mainMenuCard.style.paddingBottom = 26f;
        StyleCard(mainMenuCard, new Color(0.04f, 0.07f, 0.14f, 0.96f), radius: 20f);

        Label mainMenuHint = CreateLabel("Choose your next move", 30f, new Color(0.82f, 0.92f, 1f, 0.98f), false, TextAnchor.MiddleCenter);
        mainMenuHint.style.marginBottom = 14f;

        VisualElement mainMenuButtons = new VisualElement();
        mainMenuButtons.style.flexDirection = FlexDirection.Column;
        mainMenuButtons.style.alignItems = Align.Center;

        Button continueButton = CreateActionButton("Continue", () => owner?.ContinueFromMainMenuFromUi());
        Button libraryButton = CreateActionButton("Song Selection", () => owner?.OpenSongSelectionFromUi());
        Button settingsButton = CreateActionButton("Settings", () => owner?.OpenGlobalSettingsFromUi());
        Button mainMenuToneLabButton = CreateActionButton("Tone Lab", () => owner?.OpenToneLabFromUi());
        Button tunerButton = CreateActionButton("Tuner (Coming Soon)", null);
        tunerButton.SetEnabled(false);
        tunerButton.style.opacity = 0.60f;
        Button exitButton = CreateActionButton("Exit", () => owner?.ExitGameFromUi());

        foreach (Button button in new[] { continueButton, libraryButton, settingsButton, mainMenuToneLabButton, tunerButton, exitButton })
        {
            button.style.width = 620f;
            button.style.maxWidth = Length.Percent(94f);
            button.style.marginTop = 8f;
            button.style.marginBottom = 8f;
            ApplyDefaultButtonEdgeColor(button);
            mainMenuButtons.Add(button);
        }

        mainMenuCard.Add(mainMenuHint);
        mainMenuCard.Add(mainMenuButtons);
        mainMenuOverlay.Add(mainMenuTopTag);
        mainMenuOverlay.Add(logoWrap);
        mainMenuOverlay.Add(mainMenuCard);

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
        settingsStartDelaySlider.style.marginBottom = 14f;
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

        foreach (Button button in new[] { prevTrackButton, nextTrackButton, offsetScopeButton })
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
        AddBottomRightPrimaryButtons(settingsCard, backPauseButton, resumeFromSettingsButton);

        settingsOverlay.Add(settingsTopTag);
        settingsOverlay.Add(settingsTitle);
        settingsOverlay.Add(settingsHelp);
        settingsOverlay.Add(settingsCard);

        globalSettingsOverlay = CreateFullscreenOverlay();
        globalSettingsOverlay.style.paddingTop = 34f;
        globalSettingsOverlay.style.paddingBottom = 20f;
        Label globalSettingsTopTag = CreateLabel("◉ PERFORMANCE SETUP ◉", 30f, new Color(1f, 0.73f, 0.33f, 0.95f), true, TextAnchor.MiddleCenter, useTitleFont: true);
        globalSettingsTopTag.style.marginBottom = 6f;
        globalSettingsTopTag.style.letterSpacing = 1.6f;

        Label globalSettingsTitle = CreateLabel("SETTINGS", 88f, Color.white, true, TextAnchor.MiddleCenter, useTitleFont: true);
        globalSettingsTitle.style.marginBottom = 8f;
        globalSettingsTitle.style.letterSpacing = 1.1f;
        Label globalSettingsHelp = CreateLabel("Gameplay and visual tuning for every song.", 28f, new Color(0.82f, 0.92f, 1f, 0.96f), false, TextAnchor.MiddleCenter);
        globalSettingsHelp.style.marginBottom = 18f;

        globalSettingsCard = new VisualElement();
        globalSettingsCard.style.width = Length.Percent(96f);
        globalSettingsCard.style.maxWidth = 2340f;
        globalSettingsCard.style.minWidth = 1500f;
        globalSettingsCard.style.flexGrow = 1f;
        globalSettingsCard.style.minHeight = 540f;
        globalSettingsCard.style.paddingLeft = 24f;
        globalSettingsCard.style.paddingRight = 24f;
        globalSettingsCard.style.paddingTop = 20f;
        globalSettingsCard.style.paddingBottom = 20f;
        globalSettingsCard.style.flexDirection = FlexDirection.Column;
        StyleCard(globalSettingsCard, new Color(0.04f, 0.07f, 0.14f, 0.96f), radius: 20f);

        VisualElement globalTopButtons = new VisualElement();
        globalTopButtons.style.flexDirection = FlexDirection.Row;
        globalTopButtons.style.flexWrap = Wrap.Wrap;
        globalTopButtons.style.marginBottom = 12f;
        globalTopButtons.style.flexShrink = 0f;

        resetDefaultsButton = CreateActionButton("Reset Settings", () => owner?.ResetGlobalSettingsToDefaultsFromUi());
        resetDefaultsButton.tooltip = "Reload default gameplay and visual tuning values.";
        resetDefaultsButton.style.backgroundColor = new Color(0.36f, 0.16f, 0.20f, 0.98f);
        resetDefaultsButton.style.borderTopColor = new Color(0.95f, 0.48f, 0.53f, 0.95f);
        resetDefaultsButton.style.borderRightColor = new Color(0.80f, 0.35f, 0.39f, 0.95f);
        resetDefaultsButton.style.borderBottomColor = new Color(0.62f, 0.23f, 0.26f, 0.95f);
        resetDefaultsButton.style.borderLeftColor = new Color(0.80f, 0.35f, 0.39f, 0.95f);
        globalTopButtons.Add(resetDefaultsButton);
        globalSettingsCard.Add(globalTopButtons);

        globalSettingsScrollView = new ScrollView(ScrollViewMode.Vertical);
        globalSettingsScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
        globalSettingsScrollView.style.flexGrow = 1f;
        globalSettingsScrollView.style.flexShrink = 1f;
        globalSettingsScrollView.style.position = Position.Relative;
        globalSettingsScrollView.style.minHeight = 0f;
        globalSettingsScrollView.style.marginTop = 8f;
        globalSettingsScrollView.style.marginBottom = 8f;
        ConfigureRuntimeScrollView(globalSettingsScrollView);
        globalSettingsCard.style.overflow = Overflow.Hidden;
        globalSettingsCard.Add(globalSettingsScrollView);

        Button globalBackButton = CreateActionButton("Back", () => owner?.CloseGlobalSettingsFromUi());
        Button globalResumeButton = CreateActionButton("Resume", () => owner?.ResumePlaybackFromUi());
        AddBottomRightPrimaryButtons(globalSettingsCard, globalBackButton, globalResumeButton);
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
        ConfigureRuntimeScrollView(selectionScrollView);
        selectionCard.style.overflow = Overflow.Hidden;
        selectionCard.Add(selectionScrollView);

        VisualElement selectionButtons = new VisualElement();
        selectionButtons.style.flexDirection = FlexDirection.Row;
        selectionButtons.style.flexWrap = Wrap.Wrap;
        selectionButtons.style.marginTop = 14f;

        Button upButton = CreateActionButton("Up", () => owner?.MoveSongSelectionFromUi(-1));
        Button downButton = CreateActionButton("Down", () => owner?.MoveSongSelectionFromUi(1));
        Button openSongsFolderButton = CreateActionButton("Songs Folder", () => owner?.OpenSongsFolderFromUi());
        Button refreshSongsButton = CreateActionButton("Refresh", () => owner?.RefreshSongsFromUi());
        Button closeSelectionButton = CreateActionButton("Back", () => owner?.CloseSongSelectionFromUi());
        Button resumeSelectionButton = CreateActionButton("Resume", () => owner?.ResumePlaybackFromUi());

        foreach (Button button in new[] { upButton, downButton, openSongsFolderButton, refreshSongsButton })
        {
            button.style.marginRight = 10f;
            button.style.marginTop = 8f;
            selectionButtons.Add(button);
        }

        selectionCard.Add(selectionButtons);
        AddBottomRightPrimaryButtons(selectionCard, closeSelectionButton, resumeSelectionButton);

        selectionOverlay.Add(selectionTopTag);
        selectionOverlay.Add(selectionTitle);
        selectionOverlay.Add(selectionSubtitleLabel);
        selectionOverlay.Add(selectionCard);

        trackSelectionOverlay = CreateFullscreenOverlay();
        Label trackSelectionTopTag = CreateLabel("CHOOSE YOUR ARRANGEMENT", 28f, new Color(0.58f, 0.86f, 1f, 0.98f), true, TextAnchor.MiddleCenter, useTitleFont: true);
        trackSelectionTopTag.style.marginBottom = 6f;
        trackSelectionTopTag.style.letterSpacing = 1f;

        trackSelectionTitleLabel = CreateLabel("TRACKS", 88f, Color.white, true, TextAnchor.MiddleCenter, useTitleFont: true);
        trackSelectionTitleLabel.style.letterSpacing = 1.2f;
        trackSelectionSubtitleLabel = CreateLabel("", 30f, new Color(0.86f, 0.95f, 1f, 0.98f), false, TextAnchor.MiddleCenter);
        trackSelectionSubtitleLabel.style.marginBottom = 16f;

        VisualElement trackSelectionCard = new VisualElement();
        trackSelectionCard.style.width = 1240f;
        trackSelectionCard.style.maxWidth = 1440f;
        trackSelectionCard.style.paddingLeft = 30f;
        trackSelectionCard.style.paddingRight = 30f;
        trackSelectionCard.style.paddingTop = 24f;
        trackSelectionCard.style.paddingBottom = 24f;
        StyleCard(trackSelectionCard, new Color(0.03f, 0.10f, 0.18f, 0.98f), radius: 24f);

        trackSelectionScrollView = new ScrollView(ScrollViewMode.Vertical);
        trackSelectionScrollView.style.maxHeight = 640f;
        trackSelectionScrollView.style.minHeight = 380f;
        trackSelectionScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
        ConfigureRuntimeScrollView(trackSelectionScrollView);
        trackSelectionCard.style.overflow = Overflow.Hidden;
        trackSelectionCard.Add(trackSelectionScrollView);

        VisualElement trackSelectionButtons = new VisualElement();
        trackSelectionButtons.style.flexDirection = FlexDirection.Row;
        trackSelectionButtons.style.flexWrap = Wrap.Wrap;
        trackSelectionButtons.style.marginTop = 14f;

        Button trackSelectionUpButton = CreateActionButton("Up", () => owner?.MoveTrackSelectionFromUiList(-1));
        Button trackSelectionDownButton = CreateActionButton("Down", () => owner?.MoveTrackSelectionFromUiList(1));
        Button trackSelectionBackButton = CreateActionButton("Back", () => owner?.BackToSongSelectionFromUi());
        Button trackSelectionResumeButton = CreateActionButton("Resume", () => owner?.ResumePlaybackFromUi());

        foreach (Button button in new[]
        {
            trackSelectionUpButton,
            trackSelectionDownButton
        })
        {
            button.style.marginRight = 10f;
            button.style.marginTop = 8f;
            trackSelectionButtons.Add(button);
        }

        trackSelectionCard.Add(trackSelectionButtons);
        AddBottomRightPrimaryButtons(trackSelectionCard, trackSelectionBackButton, trackSelectionResumeButton);
        trackSelectionOverlay.Add(trackSelectionTopTag);
        trackSelectionOverlay.Add(trackSelectionTitleLabel);
        trackSelectionOverlay.Add(trackSelectionSubtitleLabel);
        trackSelectionOverlay.Add(trackSelectionCard);

        ApplyFont(root, bodyFontDefinition);


        songEndOverlay = CreateFullscreenOverlay();
        songEndOverlay.style.display = DisplayStyle.None;
        songEndOverlay.style.justifyContent = Justify.Center;
        songEndOverlay.style.paddingTop = 26f;
        songEndOverlay.style.paddingBottom = 26f;

        songEndCard = new VisualElement();
        songEndCard.style.width = Length.Percent(94f);
        songEndCard.style.maxWidth = 1720f;
        songEndCard.style.paddingLeft = 64f;
        songEndCard.style.paddingRight = 64f;
        songEndCard.style.paddingTop = 42f;
        songEndCard.style.paddingBottom = 34f;
        songEndCard.style.flexDirection = FlexDirection.Column;
        songEndCard.style.justifyContent = Justify.SpaceBetween;
        StyleCard(songEndCard, new Color(0.04f, 0.07f, 0.14f, 0.985f), radius: 22f);
        songEndCard.style.borderTopWidth = 3f;
        songEndCard.style.borderRightWidth = 3f;
        songEndCard.style.borderBottomWidth = 3f;
        songEndCard.style.borderLeftWidth = 3f;
        Color endCardBorder = new Color(0.83f, 0.89f, 0.99f, 0.90f);
        songEndCard.style.borderTopColor = endCardBorder;
        songEndCard.style.borderRightColor = endCardBorder;
        songEndCard.style.borderBottomColor = endCardBorder;
        songEndCard.style.borderLeftColor = endCardBorder;
        songEndCard.style.alignItems = Align.Center;

        VisualElement songEndMain = new VisualElement();
        songEndMain.style.flexDirection = FlexDirection.Column;
        songEndMain.style.alignItems = Align.Center;
        songEndMain.style.width = Length.Percent(100f);
        songEndMain.style.flexGrow = 1f;
        songEndMain.style.justifyContent = Justify.Center;

        songEndTitleLabel = CreateLabel("SONG COMPLETE", 106f, new Color(0.94f, 0.97f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: true);
        songEndTitleLabel.style.marginBottom = 20f;

        songEndSongLabel = CreateLabel("Song", 72f, Color.white, true, TextAnchor.MiddleCenter, useTitleFont: true);
        songEndSongLabel.style.whiteSpace = WhiteSpace.Normal;
        songEndSongLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        songEndSongLabel.style.maxWidth = Length.Percent(100f);
        songEndSongLabel.style.marginBottom = 14f;

        VisualElement songEndMetaRow = new VisualElement();
        songEndMetaRow.style.flexDirection = FlexDirection.Row;
        songEndMetaRow.style.alignItems = Align.Center;
        songEndMetaRow.style.justifyContent = Justify.Center;
        songEndMetaRow.style.marginBottom = 16f;

        songEndMetaLabel = CreateLabel("Track: Lead  •  Speed", 40f, new Color(0.83f, 0.90f, 1f, 1f), true, TextAnchor.MiddleCenter);
        songEndMetaLabel.style.whiteSpace = WhiteSpace.Normal;
        songEndMetaLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        songEndSpeedValueLabel = CreateLabel("100%", 40f, new Color(1f, 0.86f, 0.45f, 1f), true, TextAnchor.MiddleCenter);
        songEndSpeedValueLabel.style.marginLeft = 10f;

        songEndMetaRow.Add(songEndMetaLabel);
        songEndMetaRow.Add(songEndSpeedValueLabel);

        songEndScoreLabel = CreateLabel("RUN SCORE 100.0%", 62f, new Color(1f, 0.94f, 0.76f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: true);
        songEndScoreLabel.style.whiteSpace = WhiteSpace.Normal;
        songEndScoreLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        songEndScoreLabel.style.maxWidth = Length.Percent(100f);
        songEndScoreLabel.style.marginBottom = 6f;

        VisualElement songEndBestPanel = new VisualElement();
        songEndBestPanel.style.flexDirection = FlexDirection.Column;
        songEndBestPanel.style.alignItems = Align.Center;
        songEndBestPanel.style.justifyContent = Justify.Center;
        songEndBestPanel.style.width = Length.Percent(72f);
        songEndBestPanel.style.maxWidth = 1020f;
        songEndBestPanel.style.paddingTop = 16f;
        songEndBestPanel.style.paddingBottom = 14f;
        songEndBestPanel.style.paddingLeft = 20f;
        songEndBestPanel.style.paddingRight = 20f;
        songEndBestPanel.style.marginBottom = 12f;
        StyleCard(songEndBestPanel, new Color(0.06f, 0.13f, 0.22f, 0.96f), radius: 16f);
        songEndBestPanel.style.borderTopWidth = 2f;
        songEndBestPanel.style.borderRightWidth = 2f;
        songEndBestPanel.style.borderBottomWidth = 2f;
        songEndBestPanel.style.borderLeftWidth = 2f;
        Color bestPanelBorder = new Color(0.52f, 0.84f, 1f, 0.92f);
        songEndBestPanel.style.borderTopColor = bestPanelBorder;
        songEndBestPanel.style.borderRightColor = bestPanelBorder;
        songEndBestPanel.style.borderBottomColor = bestPanelBorder;
        songEndBestPanel.style.borderLeftColor = bestPanelBorder;

        songEndBestLabel = CreateLabel("TRACK BEST 100.0%", 44f, new Color(0.72f, 0.94f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: true);
        songEndBestLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        songEndBestLabel.style.whiteSpace = WhiteSpace.Normal;

        songEndDeltaLabel = CreateLabel("New record +0.0%", 30f, new Color(0.66f, 0.95f, 0.76f, 0.98f), true, TextAnchor.MiddleCenter);
        songEndDeltaLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        songEndDeltaLabel.style.marginTop = 4f;

        songEndBestPanel.Add(songEndBestLabel);
        songEndBestPanel.Add(songEndDeltaLabel);

        songEndRatingLabel = CreateLabel("Perfect!", 54f, new Color(0.62f, 0.90f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: true);
        songEndRatingLabel.style.whiteSpace = WhiteSpace.Normal;
        songEndRatingLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        songEndRatingLabel.style.maxWidth = Length.Percent(100f);
        songEndRatingLabel.style.marginBottom = 6f;

        songEndStatsLabel = CreateLabel("Hits 0  •  Misses 0", 34f, new Color(0.83f, 0.90f, 1f, 0.95f), true, TextAnchor.MiddleCenter);
        songEndStatsLabel.style.whiteSpace = WhiteSpace.Normal;
        songEndStatsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        songEndStatsLabel.style.marginBottom = 10f;

        songEndMain.Add(songEndTitleLabel);
        songEndMain.Add(songEndSongLabel);
        songEndMain.Add(songEndMetaRow);
        songEndMain.Add(songEndScoreLabel);
        songEndMain.Add(songEndRatingLabel);
        songEndMain.Add(songEndStatsLabel);
        songEndMain.Add(songEndBestPanel);

        VisualElement songEndButtons = new VisualElement();
        songEndButtons.style.flexDirection = FlexDirection.Row;
        songEndButtons.style.width = Length.Percent(100f);
        songEndButtons.style.justifyContent = Justify.Center;
        songEndButtons.style.marginTop = 28f;

        Button retryButton = CreateActionButton("Retry", () => owner?.RetrySongFromUi());
        Button selectionButton = CreateActionButton("Song Selection", () => owner?.OpenSongSelectionFromSongEndFromUi());
        retryButton.style.marginRight = 14f;
        selectionButton.style.marginLeft = 14f;
        songEndButtons.Add(retryButton);
        songEndButtons.Add(selectionButton);

        startupTuningReminderOverlay = CreateFullscreenOverlay();
        startupTuningReminderOverlay.style.display = DisplayStyle.None;
        startupTuningReminderOverlay.style.justifyContent = Justify.Center;

        VisualElement startupTuningReminderCard = new VisualElement();
        startupTuningReminderCard.style.width = 1040f;
        startupTuningReminderCard.style.maxWidth = 1100f;
        startupTuningReminderCard.style.paddingLeft = 30f;
        startupTuningReminderCard.style.paddingRight = 30f;
        startupTuningReminderCard.style.paddingTop = 34f;
        startupTuningReminderCard.style.paddingBottom = 28f;
        startupTuningReminderCard.style.alignItems = Align.Center;
        StyleCard(startupTuningReminderCard, new Color(0.05f, 0.09f, 0.16f, 0.97f), radius: 20f);

        Label startupTuningReminderTitle = CreateLabel("Please make sure your strings are tuned.", 60f, new Color(1f, 0.95f, 0.72f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: true);
        startupTuningReminderTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        startupTuningReminderTitle.style.whiteSpace = WhiteSpace.Normal;

        Label startupTuningReminderNote = CreateLabel("Tuner coming soon.", 36f, new Color(0.79f, 0.90f, 1f, 0.97f), false, TextAnchor.MiddleCenter);
        startupTuningReminderNote.style.marginTop = 12f;
        startupTuningReminderNote.style.unityTextAlign = TextAnchor.MiddleCenter;

        Label startupTuningReminderDensityHint = CreateLabel("If notes look too close or too far apart, adjust Tabs Sections duration settings.", 27f, new Color(0.73f, 0.84f, 0.98f, 0.95f), false, TextAnchor.MiddleCenter);
        startupTuningReminderDensityHint.style.marginTop = 10f;
        startupTuningReminderDensityHint.style.unityTextAlign = TextAnchor.MiddleCenter;
        startupTuningReminderDensityHint.style.whiteSpace = WhiteSpace.Normal;

        Button startupTuningReminderContinueButton = CreateActionButton("Continue", () => owner?.DismissStartupTuningReminderFromUi());
        startupTuningReminderContinueButton.style.marginTop = 18f;

        startupTuningReminderCard.Add(startupTuningReminderTitle);
        startupTuningReminderCard.Add(startupTuningReminderNote);
        startupTuningReminderCard.Add(startupTuningReminderDensityHint);
        startupTuningReminderCard.Add(startupTuningReminderContinueButton);
        startupTuningReminderOverlay.Add(startupTuningReminderCard);

        songCard.Add(compactSongCardLogo);
        songCard.Add(songNameLabel);
        songCard.Add(trackNameLabel);
        songCard.Add(statusRow);
        root.Add(songCard);
        root.Add(techniqueLegendCard);
        root.Add(scorePlate);
        root.Add(judgePopupLayer);
        root.Add(pauseOverlay);
        root.Add(mainMenuOverlay);
        root.Add(settingsOverlay);
        root.Add(globalSettingsOverlay);
        root.Add(selectionOverlay);
        root.Add(trackSelectionOverlay);
        root.Add(startupTuningReminderOverlay);
        songEndCard.Add(songEndMain);
        songEndCard.Add(songEndButtons);
        songEndOverlay.Add(songEndCard);
        root.Add(songEndOverlay);

        ApplyResponsiveSizing(force: true);
    }

    public void UpdateFromSnapshot(GuitarGameplaySnapshot snapshot)
    {
        ApplyResponsiveSizing(force: false);
        if (snapshot == null)
            return;

        string songName = string.IsNullOrWhiteSpace(snapshot.currentSongDisplayName)
            ? "No song loaded"
            : snapshot.currentSongDisplayName;

        if (string.IsNullOrWhiteSpace(songName) && snapshot.availableSongNames != null && snapshot.selectedSongIndex >= 0 && snapshot.selectedSongIndex < snapshot.availableSongNames.Count)
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

        int denominator = snapshot.noteStates?.Count(state => state != null && (!loopEnabled || IsNoteInsideLoopWindow(state.data.time, snapshot.loopStartTime, snapshot.loopEndTime))) ?? 0;

        float scorePercent = denominator > 0
            ? (100f * scoreHits / denominator)
            : 0f;
        scorePercentLabel.text = $"{scorePercent:F1}";
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

        float liveInputLevel = detectorConnected ? Mathf.Clamp01(snapshot.inputLevelNormalized) : 0f;
        displayedInputMeterLevel = Mathf.Lerp(displayedInputMeterLevel, liveInputLevel, 0.22f);
        float needleAngle = Mathf.Lerp(-65f, 65f, displayedInputMeterLevel);
        inputMeterNeedle.style.rotate = new Rotate(new Angle(needleAngle, AngleUnit.Degree));

        float songProgress = Mathf.Clamp01(snapshot.songProgressNormalized);
        float progressWidth = Mathf.Max(0f, inputMeterWrap.resolvedStyle.width > 1f ? inputMeterWrap.resolvedStyle.width : 220f);
        songProgressFill.style.width = Mathf.Lerp(songProgressFill.resolvedStyle.width, progressWidth * songProgress, 0.32f);

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

        bool showEnd = snapshot.songEnded;
        bool showMainMenu = snapshot.showMainMenu && !showEnd;
        bool showPause = snapshot.isPaused && !showEnd && !snapshot.showStartupTuningReminder && !snapshot.mainMenuFlowActive && !snapshot.showSongSettings && !snapshot.showSongSelection && !snapshot.showTrackSelection && !snapshot.showGlobalSettings;
        bool showSettings = snapshot.showSongSettings && !showEnd;
        bool showSelection = snapshot.showSongSelection && !showEnd;
        bool showTrackSelection = snapshot.showTrackSelection && !showEnd;
        bool showGlobalSettings = snapshot.showGlobalSettings && !showEnd;
        bool showStartupTuningReminder = snapshot.showStartupTuningReminder && !showEnd && !showMainMenu && !showSelection && !showTrackSelection;
        bool isHighway3D = owner != null && owner.renderMode == GuitarRenderMode.Highway3D;
        bool showTechniqueLegend = !isHighway3D && !showEnd && !showPause && !showMainMenu && !showSettings && !showSelection && !showTrackSelection && !showGlobalSettings && !showStartupTuningReminder && !snapshot.mainMenuFlowActive;

        pauseOverlay.style.display = showPause ? DisplayStyle.Flex : DisplayStyle.None;
        mainMenuOverlay.style.display = showMainMenu ? DisplayStyle.Flex : DisplayStyle.None;
        settingsOverlay.style.display = showSettings ? DisplayStyle.Flex : DisplayStyle.None;
        selectionOverlay.style.display = showSelection ? DisplayStyle.Flex : DisplayStyle.None;
        trackSelectionOverlay.style.display = showTrackSelection ? DisplayStyle.Flex : DisplayStyle.None;
        globalSettingsOverlay.style.display = showGlobalSettings ? DisplayStyle.Flex : DisplayStyle.None;
        startupTuningReminderOverlay.style.display = showStartupTuningReminder ? DisplayStyle.Flex : DisplayStyle.None;
        songEndOverlay.style.display = showEnd ? DisplayStyle.Flex : DisplayStyle.None;
        techniqueLegendCard.style.display = showTechniqueLegend ? DisplayStyle.Flex : DisplayStyle.None;

        songCard.style.display = snapshot.mainMenuFlowActive ? DisplayStyle.None : DisplayStyle.Flex;
        scorePlate.style.display = snapshot.mainMenuFlowActive ? DisplayStyle.None : DisplayStyle.Flex;
        judgePopupLayer.style.display = snapshot.mainMenuFlowActive ? DisplayStyle.None : DisplayStyle.Flex;

        if (showEnd)
        {
            string rating = GetScoreRating(scorePercent);
            float savedTrackBest = Mathf.Clamp(snapshot.currentTrackBestScorePercent, 0f, 100f);
            float deltaToBest = scorePercent - savedTrackBest;
            bool newRecord = deltaToBest >= -0.05f;

            songEndSongLabel.text = songName;
            songEndMetaLabel.text = $"Track: {trackName}  •  Speed";
            songEndSpeedValueLabel.text = $"{speedPercent:F0}%";
            songEndScoreLabel.text = $"RUN SCORE {scorePercent:F1}%";
            songEndBestLabel.text = $"TRACK BEST {savedTrackBest:F1}%";
            songEndDeltaLabel.text = newRecord
                ? "NEW RECORD!"
                : $"Need {Mathf.Abs(deltaToBest):F1}% to beat your best";
            songEndRatingLabel.text = rating;
            songEndStatsLabel.text = $"Hits {scoreHits}  •  Misses {scoreMisses}";

            songEndSongLabel.style.color = Color.white;
            songEndMetaLabel.style.color = new Color(0.83f, 0.90f, 1f, 1f);
            songEndSpeedValueLabel.style.color = new Color(1f, 0.86f, 0.45f, 1f);
            songEndScoreLabel.style.color = new Color(1f, 0.94f, 0.76f, 1f);
            songEndBestLabel.style.color = new Color(0.70f, 0.94f, 1f, 1f);
            songEndDeltaLabel.style.color = newRecord
                ? new Color(0.58f, 0.96f, 0.68f, 1f)
                : new Color(1f, 0.84f, 0.54f, 1f);
            songEndRatingLabel.style.color = scorePercent >= 95f
                ? new Color(0.58f, 0.96f, 0.68f, 1f)
                : scorePercent >= 80f
                    ? new Color(0.62f, 0.84f, 1f, 1f)
                    : new Color(1f, 0.84f, 0.54f, 1f);
        }

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

        if (showTrackSelection)
            UpdateTrackSelectionRows(snapshot);

        if (showGlobalSettings)
            UpdateGlobalSettings(snapshot);
    }

    private static string GetScoreRating(float scorePercent)
    {
        if (scorePercent >= 99.5f) return "Perfect!";
        if (scorePercent >= 95f) return "Amazing!";
        if (scorePercent >= 85f) return "Great!";
        if (scorePercent >= 70f) return "Good!";
        if (scorePercent >= 50f) return "Keep Going!";
        return "Needs Practice";
    }

    public void Dispose()
    {
        if (rootObject != null)
            UnityEngine.Object.Destroy(rootObject);
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

        if (snapshot.selectedSongIndex != lastAutoScrolledSongIndex && snapshot.selectedSongIndex >= 0 && snapshot.selectedSongIndex < selectionRows.Count)
        {
            selectionScrollView.ScrollTo(selectionRows[snapshot.selectedSongIndex].button);
            lastAutoScrolledSongIndex = snapshot.selectedSongIndex;
        }
    }

    private void EnsureSongSelectionRows(int count)
    {
        if (selectionRows.Count == count)
            return;

        selectionScrollView.Clear();
        selectionRows.Clear();
        lastAutoScrolledSongIndex = -1;

        for (int i = 0; i < count; i++)
        {
            int songIndex = i;
            Button rowButton = CreateActionButton(string.Empty, () => OnSongRowClicked(songIndex));
            rowButton.style.height = 98f;
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

            Label nameLabel = CreateLabel(string.Empty, 36f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: false);
            nameLabel.style.flexGrow = 1f;
            nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            Label scoreLabel = CreateLabel("0%", 34f, new Color(1f, 0.85f, 0.45f, 0.98f), true, TextAnchor.MiddleRight, useTitleFont: false);
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


    private void UpdateTrackSelectionRows(GuitarGameplaySnapshot snapshot)
    {
        int total = snapshot.availableTrackNames?.Count ?? 0;
        trackSelectionTitleLabel.text = "TRACKS";
        trackSelectionSubtitleLabel.text = $"{total} arrangements  •  Sorted by best score";

        EnsureTrackSelectionRows(total);

        for (int trackIndex = 0; trackIndex < trackSelectionRows.Count; trackIndex++)
        {
            TrackSelectionRow row = trackSelectionRows[trackIndex];
            bool isSelected = trackIndex == snapshot.selectedTrackIndex;
            string name = snapshot.availableTrackNames[trackIndex];
            float score = (snapshot.availableTrackScores != null && trackIndex < snapshot.availableTrackScores.Count)
                ? snapshot.availableTrackScores[trackIndex]
                : 0f;

            row.nameLabel.text = isSelected ? $"> {name}" : $"  {name}";
            row.scoreLabel.text = $"{score:F1}%";

            row.button.style.backgroundColor = isSelected
                ? new Color(0.14f, 0.28f, 0.46f, 0.99f)
                : new Color(0.05f, 0.12f, 0.20f, 0.95f);
            row.button.style.borderTopColor = isSelected ? new Color(0.56f, 0.88f, 1f, 1f) : new Color(0.35f, 0.66f, 0.94f, 0.74f);
            row.button.style.borderRightColor = row.button.style.borderTopColor;
            row.button.style.borderBottomColor = row.button.style.borderTopColor;
            row.button.style.borderLeftColor = row.button.style.borderTopColor;
        }

        if (snapshot.selectedTrackIndex != lastAutoScrolledTrackIndex && snapshot.selectedTrackIndex >= 0 && snapshot.selectedTrackIndex < trackSelectionRows.Count)
        {
            trackSelectionScrollView.ScrollTo(trackSelectionRows[snapshot.selectedTrackIndex].button);
            lastAutoScrolledTrackIndex = snapshot.selectedTrackIndex;
        }
    }

    private void EnsureTrackSelectionRows(int count)
    {
        if (trackSelectionRows.Count == count)
            return;

        trackSelectionScrollView.Clear();
        trackSelectionRows.Clear();
        lastAutoScrolledTrackIndex = -1;

        for (int i = 0; i < count; i++)
        {
            int trackIndex = i;
            Button rowButton = CreateActionButton(string.Empty, () => OnTrackRowClicked(trackIndex));
            rowButton.style.height = 102f;
            rowButton.style.marginTop = 6f;
            rowButton.style.marginBottom = 2f;
            rowButton.style.borderTopLeftRadius = 14f;
            rowButton.style.borderTopRightRadius = 14f;
            rowButton.style.borderBottomLeftRadius = 14f;
            rowButton.style.borderBottomRightRadius = 14f;

            VisualElement content = new VisualElement();
            content.style.flexDirection = FlexDirection.Row;
            content.style.justifyContent = Justify.SpaceBetween;
            content.style.alignItems = Align.Center;
            content.style.flexGrow = 1f;

            Label nameLabel = CreateLabel(string.Empty, 36f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: false);
            nameLabel.style.flexGrow = 1f;
            Label scoreLabel = CreateLabel("0%", 34f, new Color(0.54f, 0.92f, 1f, 0.99f), true, TextAnchor.MiddleRight, useTitleFont: false);
            scoreLabel.style.minWidth = 130f;

            content.Add(nameLabel);
            content.Add(scoreLabel);
            rowButton.Add(content);
            trackSelectionScrollView.Add(rowButton);

            trackSelectionRows.Add(new TrackSelectionRow
            {
                button = rowButton,
                nameLabel = nameLabel,
                scoreLabel = scoreLabel
            });
        }
    }

    private void OnTrackRowClicked(int rowIndex)
    {
        if (owner == null)
            return;

        owner.SelectTrackByIndexFromUi(rowIndex);
    }


    private void UpdateGlobalSettings(GuitarGameplaySnapshot snapshot)
    {
        bool rebuilt = BuildGlobalSettingsUi(snapshot.runtimeSettingsSections);
        if (rebuilt)
            RestoreGlobalSettingsScrollOffset();

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
                else if (input is EnumCycleControl enumCycle)
                {
                    if (!string.IsNullOrEmpty(setting.value))
                        enumCycle.SetValueWithoutNotify(setting.value);
                }

                if (globalSettingValueLabels.TryGetValue(setting.id, out Label valueLabel))
                    valueLabel.text = setting.value;
            }
        }
        suppressCallbacks = false;
        globalSettingsScrollOffset = globalSettingsScrollView != null ? globalSettingsScrollView.scrollOffset : globalSettingsScrollOffset;
    }

    private bool BuildGlobalSettingsUi(List<RuntimeSettingSectionSnapshot> sections)
    {
        if (sections == null)
            return false;

        string signature = BuildGlobalSettingsLayoutSignature(sections);
        if (signature == globalSettingsLayoutSignature && globalSettingInputs.Count > 0)
            return false;

        globalSettingsScrollOffset = globalSettingsScrollView != null ? globalSettingsScrollView.scrollOffset : globalSettingsScrollOffset;
        globalSettingsLayoutSignature = signature;
        globalSettingsScrollView.Clear();
        globalSettingInputs.Clear();
        globalSettingValueLabels.Clear();
        globalSettingsColumns.Clear();

        VisualElement columnsWrapper = new VisualElement();
        columnsWrapper.style.flexDirection = FlexDirection.Row;
        columnsWrapper.style.alignItems = Align.FlexStart;
        columnsWrapper.style.justifyContent = Justify.SpaceBetween;
        columnsWrapper.style.flexWrap = Wrap.NoWrap;
        columnsWrapper.style.minWidth = 1380f;
        columnsWrapper.style.width = Length.Percent(100f);

        AddGlobalSettingsColumn(columnsWrapper, "Gameplay Mechanics", addRightSpacing: true);
        AddGlobalSettingsColumn(columnsWrapper, "Tabs Visuals", addRightSpacing: true);
        AddGlobalSettingsColumn(columnsWrapper, "Highway 3D", addRightSpacing: true);
        AddGlobalSettingsColumn(columnsWrapper, "General Visuals", addRightSpacing: false);

        globalSettingsScrollView.Add(columnsWrapper);

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
            sectionCard.style.borderTopWidth = 3f;
            sectionCard.style.borderRightWidth = 3f;
            sectionCard.style.borderBottomWidth = 3f;
            sectionCard.style.borderLeftWidth = 3f;
            Color sectionBorderColor = new Color(0.93f, 0.97f, 1f, 0.92f);
            sectionCard.style.borderTopColor = sectionBorderColor;
            sectionCard.style.borderRightColor = sectionBorderColor;
            sectionCard.style.borderBottomColor = sectionBorderColor;
            sectionCard.style.borderLeftColor = sectionBorderColor;

            Label sectionTitle = CreateLabel(section.title, 30f, new Color(1f, 0.87f, 0.62f, 1f), true);
            sectionTitle.AddToClassList("global-section-title");
            sectionTitle.style.marginBottom = 10f;
            sectionTitle.style.whiteSpace = WhiteSpace.Normal;
            sectionTitle.style.flexShrink = 1f;
            sectionCard.Add(sectionTitle);

            if (section.settings != null)
            {
                foreach (RuntimeSettingSnapshot setting in section.settings)
                    sectionCard.Add(CreateGlobalSettingRow(setting));
            }

            string category = CategorizeGlobalSettingsSection(section);
            if (globalSettingsColumns.TryGetValue(category, out VisualElement column))
                column.Add(sectionCard);
            else
                globalSettingsScrollView.Add(sectionCard);
        }

        ApplyResponsiveSizing(force: true);
        return true;
    }

    private void RestoreGlobalSettingsScrollOffset()
    {
        if (globalSettingsScrollView == null)
            return;

        Vector2 preservedOffset = globalSettingsScrollOffset;
        globalSettingsScrollView.schedule.Execute(() =>
        {
            if (globalSettingsScrollView == null)
                return;

            globalSettingsScrollView.scrollOffset = preservedOffset;
        }).ExecuteLater(0);
    }

    private void PreserveGlobalSettingsScrollOffset()
    {
        if (globalSettingsScrollView == null)
            return;

        globalSettingsScrollOffset = globalSettingsScrollView.scrollOffset;
        RestoreGlobalSettingsScrollOffset();
    }

    private void AddGlobalSettingsColumn(VisualElement parent, string title, bool addRightSpacing)
    {
        VisualElement column = new VisualElement();
        column.style.flexGrow = 1f;
        column.style.flexShrink = 1f;
        column.style.flexBasis = 0f;
        column.style.minWidth = 420f;
        if (addRightSpacing)
            column.style.marginRight = 14f;

        Label columnTitle = CreateLabel(title, 34f, new Color(1f, 0.92f, 0.73f, 0.98f), true, TextAnchor.MiddleCenter, useTitleFont: true);
        columnTitle.style.marginBottom = 10f;
        columnTitle.style.unityTextAlign = TextAnchor.MiddleCenter;
        columnTitle.style.paddingTop = 6f;
        columnTitle.style.paddingBottom = 6f;
        columnTitle.style.backgroundColor = new Color(0.13f, 0.19f, 0.30f, 0.88f);
        columnTitle.style.borderTopLeftRadius = 8f;
        columnTitle.style.borderTopRightRadius = 8f;
        columnTitle.style.borderBottomLeftRadius = 8f;
        columnTitle.style.borderBottomRightRadius = 8f;
        columnTitle.style.borderTopWidth = 2f;
        columnTitle.style.borderRightWidth = 2f;
        columnTitle.style.borderBottomWidth = 2f;
        columnTitle.style.borderLeftWidth = 2f;
        Color titleBorder = new Color(0.83f, 0.90f, 1f, 0.78f);
        columnTitle.style.borderTopColor = titleBorder;
        columnTitle.style.borderRightColor = titleBorder;
        columnTitle.style.borderBottomColor = titleBorder;
        columnTitle.style.borderLeftColor = titleBorder;
        column.Add(columnTitle);

        parent.Add(column);
        globalSettingsColumns[title] = column;
    }

    private static string CategorizeGlobalSettingsSection(RuntimeSettingSectionSnapshot section)
    {
        string normalizedTitle = section.title?.ToLowerInvariant() ?? string.Empty;
        List<RuntimeSettingSnapshot> sectionSettings = section.settings;

        if (normalizedTitle.Contains("timing") || normalizedTitle.Contains("forgiveness") || normalizedTitle.Contains("settings") || IsSectionIdPrefix(sectionSettings, "core.") || IsSectionIdPrefix(sectionSettings, "timing."))
            return "Gameplay Mechanics";

        if (normalizedTitle.Contains("tab") || normalizedTitle.Contains("layout") || IsSectionIdPrefix(sectionSettings, "layout."))
            return "Tabs Visuals";

        if (normalizedTitle.Contains("highway") || IsSectionIdPrefix(sectionSettings, "highway.") || IsSectionIdPrefix(sectionSettings, "render."))
            return "Highway 3D";

        if (normalizedTitle.Contains("visual") || normalizedTitle.Contains("color") || normalizedTitle.Contains("background") || IsSectionIdPrefix(sectionSettings, "fx.") || IsSectionIdPrefix(sectionSettings, "bg."))
            return "General Visuals";

        return "Gameplay Mechanics";
    }

    private static bool IsSectionIdPrefix(List<RuntimeSettingSnapshot> settings, string prefix)
    {
        if (settings == null || string.IsNullOrEmpty(prefix))
            return false;

        return settings.Any(setting =>
            setting != null &&
            !string.IsNullOrEmpty(setting.id) &&
            setting.id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private VisualElement CreateGlobalSettingRow(RuntimeSettingSnapshot setting)
    {
        VisualElement row = new VisualElement();
        row.style.marginBottom = 12f;
        row.style.paddingLeft = 12f;
        row.style.paddingRight = 12f;
        row.style.paddingTop = 10f;
        row.style.paddingBottom = 10f;
        row.style.backgroundColor = new Color(0.09f, 0.14f, 0.24f, 0.84f);
        row.style.borderTopWidth = 2f;
        row.style.borderRightWidth = 2f;
        row.style.borderBottomWidth = 2f;
        row.style.borderLeftWidth = 2f;
        Color rowBorderColor = new Color(0.90f, 0.95f, 1f, 0.84f);
        row.style.borderTopColor = rowBorderColor;
        row.style.borderRightColor = rowBorderColor;
        row.style.borderBottomColor = rowBorderColor;
        row.style.borderLeftColor = rowBorderColor;
        row.style.borderTopLeftRadius = 10f;
        row.style.borderTopRightRadius = 10f;
        row.style.borderBottomLeftRadius = 10f;
        row.style.borderBottomRightRadius = 10f;
        row.style.width = Length.Percent(100f);

        Label label = CreateLabel(setting.label, 34f, Color.white, true);
        label.AddToClassList("global-setting-title");
        label.tooltip = setting.tooltip;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.flexShrink = 1f;
        row.Add(label);

        Label help = CreateLabel(setting.tooltip, 28f, new Color(0.75f, 0.88f, 0.96f, 0.95f));
        help.AddToClassList("global-setting-help");
        help.style.marginTop = 2f;
        help.style.marginBottom = 6f;
        help.tooltip = setting.tooltip;
        help.style.whiteSpace = WhiteSpace.Normal;
        help.style.flexShrink = 1f;
        row.Add(help);

        VisualElement input = null;
        if (string.Equals(setting.valueType, "bool", StringComparison.OrdinalIgnoreCase))
        {
            Toggle toggle = new Toggle();
            toggle.value = string.Equals(setting.value, "true", StringComparison.OrdinalIgnoreCase);
            toggle.focusable = false;
            toggle.RegisterCallback<PointerDownEvent>(_ => PreserveGlobalSettingsScrollOffset());
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (suppressCallbacks)
                    return;

                PreserveGlobalSettingsScrollOffset();
                owner?.SetGlobalRuntimeSettingFromUi(setting.id, evt.newValue ? "true" : "false");
            });
            input = toggle;
        }
        else if (string.Equals(setting.valueType, "enum", StringComparison.OrdinalIgnoreCase))
        {
            EnumCycleControl enumCycle = new EnumCycleControl(setting.enumOptions, setting.value, CreateLabel, CreateActionButton);
            enumCycle.focusable = false;
            enumCycle.RegisterCallback<PointerDownEvent>(_ => PreserveGlobalSettingsScrollOffset());
            enumCycle.OnValueChanged += value =>
            {
                if (!suppressCallbacks)
                {
                    PreserveGlobalSettingsScrollOffset();
                    owner?.SetGlobalRuntimeSettingFromUi(setting.id, value);
                }
            };
            input = enumCycle;
        }
        else
        {
            Slider slider = new Slider(setting.min, setting.max) { value = ParseFloat(setting.value, setting.min) };
            slider.focusable = false;
            slider.RegisterCallback<PointerDownEvent>(_ => PreserveGlobalSettingsScrollOffset());
            slider.RegisterValueChangedCallback(evt =>
            {
                if (suppressCallbacks)
                    return;

                PreserveGlobalSettingsScrollOffset();
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

        Label valueLabel = CreateLabel(setting.value, 30f, new Color(1f, 0.95f, 0.76f, 1f));
        valueLabel.style.whiteSpace = WhiteSpace.Normal;
        valueLabel.style.flexShrink = 1f;
        valueLabel.AddToClassList("global-setting-value");
        row.Add(valueLabel);

        globalSettingInputs[setting.id] = input;
        globalSettingValueLabels[setting.id] = valueLabel;
        return row;
    }

    private static void ConfigureRuntimeScrollView(ScrollView scrollView)
    {
        if (scrollView == null)
            return;

        scrollView.style.overflow = Overflow.Hidden;
        scrollView.contentViewport.style.overflow = Overflow.Hidden;
        scrollView.contentContainer.style.flexShrink = 0f;
        scrollView.mouseWheelScrollSize = 96f;
        scrollView.verticalPageSize = 240f;
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

        Color popupColor = success
            ? new Color(0.46f, 0.88f, 1f, 0.99f)
            : new Color(1f, 0.36f, 0.33f, 0.99f);

        Label popup = CreateLabel(text, judgePopupFontSize, popupColor, true, TextAnchor.MiddleCenter, useTitleFont: false);
        popup.style.position = Position.Absolute;
        bool isHighway3D = owner != null && owner.renderMode == GuitarRenderMode.Highway3D;
        if (isHighway3D)
        {
            float pedalWidth = Mathf.Clamp(Screen.width * 0.15f, 430f, 620f);
            float pedalHeight = Mathf.Clamp(Screen.height * 0.30f, 280f, 560f);
            float popupWidth = Mathf.Clamp(Screen.width * 0.18f, 240f, 360f);
            float popupRight = pedalWidth + 88f;
            float highwayPopupBaseY = 8f + pedalHeight - Mathf.Clamp(judgePopupFontSize * 1.15f, 54f, 96f);
            int highwayPopupLayer = Mathf.Min(activeJudgePopups.Count, 4);
            float highwayPopupStartY = highwayPopupBaseY - highwayPopupLayer * 26f;

            popup.style.left = StyleKeyword.Auto;
            popup.style.right = popupRight;
            popup.style.width = popupWidth;
            popup.style.unityTextAlign = TextAnchor.MiddleRight;
            popup.style.top = highwayPopupStartY;

            judgePopupLayer.Add(popup);
            activeJudgePopups.Add(new JudgePopupEntry
            {
                label = popup,
                startTime = Time.unscaledTime,
                startY = highwayPopupStartY,
                endY = highwayPopupStartY - 120f,
                duration = 1.05f
            });
            return;
        }

        popup.style.left = 0f;
        popup.style.right = 0f;
        popup.style.unityTextAlign = TextAnchor.MiddleCenter;
        popup.style.letterSpacing = 1.2f;
        popup.style.opacity = 1f;
        popup.style.scale = new Scale(new Vector3(1.14f, 1.14f, 1f));

        float baseY = Mathf.Clamp(Screen.height * 0.62f, 430f, 780f);
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

    private void AddTechniqueLegendRow(string icon, string description, Color iconColor)
    {
        if (techniqueLegendCard == null)
            return;

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = 1f;
        row.style.marginBottom = 1f;

        Label iconLabel = CreateLabel(icon, 24f, iconColor, true, TextAnchor.MiddleLeft, useTitleFont: false);
        iconLabel.style.minWidth = 26f;
        iconLabel.style.marginRight = 8f;
        iconLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        Label textLabel = CreateLabel($": {description}", 24f, new Color(0.90f, 0.96f, 1f, 0.98f), true, TextAnchor.MiddleLeft, useTitleFont: false);
        textLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        techniqueLegendIconLabels.Add(iconLabel);
        techniqueLegendTextLabels.Add(textLabel);
        row.Add(iconLabel);
        row.Add(textLabel);
        techniqueLegendCard.Add(row);
    }

    private VisualElement CreateStringTheoryLogo(float stringSize, float theorySize, float theoryShiftLeft, float theoryLetterSpacing, float rowBottomMargin, float stringLetterMargin)
    {
        VisualElement logoWrap = new VisualElement();
        logoWrap.style.alignItems = Align.Center;

        VisualElement stringRow = new VisualElement();
        stringRow.style.flexDirection = FlexDirection.Row;
        stringRow.style.justifyContent = Justify.Center;
        stringRow.style.marginBottom = rowBottomMargin;

        const string stringWord = "STRING";
        for (int i = 0; i < stringWord.Length; i++)
        {
            Label letter = CreateLabel(stringWord[i].ToString(), stringSize, LogoStringColors[i % LogoStringColors.Length], true, TextAnchor.MiddleCenter, useTitleFont: true);
            letter.style.marginLeft = stringLetterMargin;
            letter.style.marginRight = stringLetterMargin;
            letter.style.unityFontStyleAndWeight = FontStyle.Bold;
            stringRow.Add(letter);
        }

        Label theoryLabel = CreateLabel("THEORY", theorySize, new Color(0.87f, 0.95f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: true);
        theoryLabel.style.marginLeft = theoryShiftLeft;
        theoryLabel.style.letterSpacing = theoryLetterSpacing;

        logoWrap.Add(stringRow);
        logoWrap.Add(theoryLabel);
        return logoWrap;
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
        ApplyButtonEdgeColorByLabel(button, text);
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.letterSpacing = 0.35f;
        button.style.marginBottom = 3f;
        button.style.unityFontDefinition = bodyFontDefinition;
        return button;
    }

    private static void AddBottomRightPrimaryButtons(VisualElement container, params Button[] buttons)
    {
        if (container == null || buttons == null || buttons.Length == 0)
            return;

        const string spacerName = "primary-actions-dock-spacer";
        const string dockName = "primary-actions-dock";

        container.style.position = Position.Relative;

        VisualElement existingDock = container.Q<VisualElement>(dockName);
        if (existingDock != null)
            existingDock.RemoveFromHierarchy();

        VisualElement spacer = container.Q<VisualElement>(spacerName);
        if (spacer == null)
        {
            spacer = new VisualElement();
            spacer.name = spacerName;
            spacer.style.height = 96f;
            spacer.style.flexShrink = 0f;
            spacer.style.marginTop = 18f;
            spacer.pickingMode = PickingMode.Ignore;
            container.Add(spacer);
        }

        VisualElement dock = new VisualElement();
        dock.name = dockName;
        dock.style.position = Position.Absolute;
        dock.style.right = 0f;
        dock.style.bottom = 0f;
        dock.style.flexDirection = FlexDirection.Row;
        dock.style.justifyContent = Justify.FlexEnd;
        dock.style.alignItems = Align.Center;
        dock.style.paddingRight = 12f;
        dock.style.paddingBottom = 12f;

        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            button.style.marginLeft = 12f;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            dock.Add(button);
        }

        container.Add(dock);
    }


    private static void ApplyDefaultButtonEdgeColor(Button button)
    {
        if (button == null)
            return;

        Color buttonBorderColor = new Color(0.30f, 0.50f, 0.90f, 0.88f);
        button.style.borderTopColor = buttonBorderColor;
        button.style.borderRightColor = buttonBorderColor;
        button.style.borderBottomColor = buttonBorderColor;
        button.style.borderLeftColor = buttonBorderColor;
    }

    private static void ApplyButtonEdgeColorByLabel(Button button, string text)
    {
        if (button == null)
            return;

        string normalized = (text ?? string.Empty).Trim();
        Color accent = new Color(0.30f, 0.50f, 0.90f, 0.88f);

        if (normalized.StartsWith("Loop", StringComparison.OrdinalIgnoreCase))
            accent = new Color(1.00f, 0.20f, 0.20f, 0.94f);
        else if (ContainsIgnoreCase(normalized, "Resume") || ContainsIgnoreCase(normalized, "Continue"))
            accent = new Color(0.19f, 0.81f, 0.55f, 0.94f);
        else if (ContainsIgnoreCase(normalized, "Back") || ContainsIgnoreCase(normalized, "Main Menu"))
            accent = new Color(0.57f, 0.67f, 0.94f, 0.92f);
        else if (ContainsIgnoreCase(normalized, "Song Selection") || ContainsIgnoreCase(normalized, "Library"))
            accent = new Color(0.31f, 0.79f, 0.94f, 0.92f);
        else if (ContainsIgnoreCase(normalized, "Song Settings") || ContainsIgnoreCase(normalized, "Settings"))
            accent = new Color(0.66f, 0.56f, 0.95f, 0.92f);
        else if (ContainsIgnoreCase(normalized, "Tone Lab"))
            accent = new Color(0.17f, 0.84f, 0.85f, 0.92f);
        else if (ContainsIgnoreCase(normalized, "Track") || ContainsIgnoreCase(normalized, "Offset"))
            accent = new Color(0.95f, 0.74f, 0.33f, 0.92f);
        else if (ContainsIgnoreCase(normalized, "Up") || ContainsIgnoreCase(normalized, "Down") || ContainsIgnoreCase(normalized, "Refresh"))
            accent = new Color(0.37f, 0.67f, 0.97f, 0.90f);
        else if (ContainsIgnoreCase(normalized, "Folder"))
            accent = new Color(0.41f, 0.81f, 0.58f, 0.92f);
        else if (ContainsIgnoreCase(normalized, "Exit"))
            accent = new Color(0.92f, 0.37f, 0.45f, 0.92f);

        Color top = new Color(Mathf.Clamp01(accent.r * 1.12f), Mathf.Clamp01(accent.g * 1.12f), Mathf.Clamp01(accent.b * 1.12f), accent.a);
        Color side = new Color(Mathf.Clamp01(accent.r * 0.92f), Mathf.Clamp01(accent.g * 0.92f), Mathf.Clamp01(accent.b * 0.92f), accent.a);
        Color bottom = new Color(Mathf.Clamp01(accent.r * 0.70f), Mathf.Clamp01(accent.g * 0.70f), Mathf.Clamp01(accent.b * 0.70f), accent.a);

        button.style.borderTopColor = top;
        button.style.borderRightColor = side;
        button.style.borderBottomColor = bottom;
        button.style.borderLeftColor = side;
    }

    private static bool ContainsIgnoreCase(string source, string token)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(token))
            return false;

        return source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static VisualElement CreatePedalKnob()
    {
        VisualElement knob = new VisualElement();
        knob.style.width = 30f;
        knob.style.height = 30f;
        knob.style.backgroundColor = new Color(0.20f, 0.27f, 0.34f, 1f);
        knob.style.borderTopWidth = 3f;
        knob.style.borderRightWidth = 3f;
        knob.style.borderBottomWidth = 4f;
        knob.style.borderLeftWidth = 3f;
        knob.style.borderTopColor = new Color(0.51f, 0.66f, 0.75f, 1f);
        knob.style.borderRightColor = new Color(0.06f, 0.16f, 0.23f, 1f);
        knob.style.borderBottomColor = new Color(0.03f, 0.11f, 0.16f, 1f);
        knob.style.borderLeftColor = new Color(0.06f, 0.16f, 0.23f, 1f);
        knob.style.borderTopLeftRadius = 999f;
        knob.style.borderTopRightRadius = 999f;
        knob.style.borderBottomLeftRadius = 999f;
        knob.style.borderBottomRightRadius = 999f;
        knob.style.marginLeft = 10f;
        knob.style.marginRight = 10f;

        VisualElement indicator = new VisualElement();
        indicator.name = "knob-indicator";
        indicator.style.position = Position.Absolute;
        indicator.style.width = 3f;
        indicator.style.height = 11f;
        indicator.style.left = 13f;
        indicator.style.top = 4f;
        indicator.style.backgroundColor = new Color(0.96f, 0.98f, 1f, 0.98f);
        indicator.style.borderTopLeftRadius = 2f;
        indicator.style.borderTopRightRadius = 2f;
        indicator.style.borderBottomLeftRadius = 2f;
        indicator.style.borderBottomRightRadius = 2f;
        knob.Add(indicator);
        return knob;
    }

    private static void SetKnobIndicatorAngle(VisualElement knob, float degrees)
    {
        if (knob == null)
            return;

        VisualElement indicator = knob.Q<VisualElement>("knob-indicator");
        if (indicator == null)
            return;

        indicator.style.rotate = new Rotate(new Angle(degrees, AngleUnit.Degree));
    }

    private static void SetKnobIndicatorSize(VisualElement knob, float knobSize)
    {
        if (knob == null)
            return;

        VisualElement indicator = knob.Q<VisualElement>("knob-indicator");
        if (indicator == null)
            return;

        float indicatorWidth = Mathf.Clamp(knobSize * 0.10f, 2f, 5f);
        float indicatorHeight = Mathf.Clamp(knobSize * 0.35f, 8f, 16f);
        indicator.style.width = indicatorWidth;
        indicator.style.height = indicatorHeight;
        indicator.style.left = (knobSize - indicatorWidth) * 0.5f;
        indicator.style.top = Mathf.Clamp(knobSize * 0.12f, 3f, 8f);
    }

    private void LayoutInputMeterGraphics(float meterWidth, float meterHeight)
    {
        float inset = Mathf.Clamp(meterWidth * 0.08f, 10f, 18f);
        float arcViewportTop = Mathf.Clamp(meterHeight * 0.12f, 8f, 14f);
        float arcViewportHeight = Mathf.Clamp(meterHeight * 0.48f, 28f, 52f);
        float arcHeight = arcViewportHeight * 2f;
        float arcWidth = Mathf.Max(1f, meterWidth - (inset * 2f));
        float rx = arcWidth * 0.5f;
        float ry = arcHeight * 0.5f;
        float centerX = meterWidth * 0.5f;
        float centerY = arcViewportTop + ry;

        inputMeterArcViewport.style.left = inset;
        inputMeterArcViewport.style.right = inset;
        inputMeterArcViewport.style.top = arcViewportTop;
        inputMeterArcViewport.style.height = arcViewportHeight;

        inputMeterArc.style.height = arcHeight;
        inputMeterArc.style.borderTopLeftRadius = arcHeight;
        inputMeterArc.style.borderTopRightRadius = arcHeight;
        inputMeterArc.style.borderBottomLeftRadius = arcHeight;
        inputMeterArc.style.borderBottomRightRadius = arcHeight;

        int tickCount = inputMeterTicks.Count;
        for (int i = 0; i < tickCount; i++)
        {
            VisualElement tick = inputMeterTicks[i];
            if (tick == null)
                continue;

            float t = tickCount <= 1 ? 0f : i / (tickCount - 1f);
            float theta = Mathf.Lerp(Mathf.PI * 0.96f, Mathf.PI * 0.04f, t);
            float arcX = centerX + Mathf.Cos(theta) * rx;
            float arcY = centerY - Mathf.Sin(theta) * ry;
            float tickHeight = i % 2 == 0 ? Mathf.Clamp(meterHeight * 0.13f, 8f, 14f) : Mathf.Clamp(meterHeight * 0.08f, 5f, 9f);
            float tickWidth = i % 2 == 0 ? 3f : 2f;

            tick.style.width = tickWidth;
            tick.style.height = tickHeight;
            tick.style.left = arcX - (tickWidth * 0.5f);
            tick.style.top = arcY - 1f;
        }

        float capSize = Mathf.Clamp(meterHeight * 0.16f, 10f, 16f);
        float pivotY = arcViewportTop + arcViewportHeight + Mathf.Clamp(meterHeight * 0.10f, 6f, 12f);
        float needleHeight = Mathf.Clamp(meterHeight * 0.42f, 24f, 44f);
        inputMeterNeedle.style.height = needleHeight;
        inputMeterNeedle.style.left = centerX - 1.5f;
        inputMeterNeedle.style.top = pivotY - needleHeight;

        inputMeterNeedleCap.style.width = capSize;
        inputMeterNeedleCap.style.height = capSize;
        inputMeterNeedleCap.style.left = centerX - (capSize * 0.5f);
        inputMeterNeedleCap.style.top = pivotY - (capSize * 0.5f);
        inputMeterNeedleCap.style.borderTopLeftRadius = capSize * 0.5f;
        inputMeterNeedleCap.style.borderTopRightRadius = capSize * 0.5f;
        inputMeterNeedleCap.style.borderBottomLeftRadius = capSize * 0.5f;
        inputMeterNeedleCap.style.borderBottomRightRadius = capSize * 0.5f;
    }

    private static VisualElement CreateFootswitch()
    {
        VisualElement footswitch = new VisualElement();
        footswitch.style.width = 46f;
        footswitch.style.height = 46f;
        footswitch.style.backgroundColor = new Color(0.81f, 0.85f, 0.90f, 1f);
        footswitch.style.borderTopWidth = 3f;
        footswitch.style.borderRightWidth = 3f;
        footswitch.style.borderBottomWidth = 6f;
        footswitch.style.borderLeftWidth = 3f;
        footswitch.style.borderTopColor = new Color(0.98f, 0.99f, 1f, 1f);
        footswitch.style.borderRightColor = new Color(0.45f, 0.52f, 0.58f, 1f);
        footswitch.style.borderBottomColor = new Color(0.23f, 0.28f, 0.33f, 1f);
        footswitch.style.borderLeftColor = new Color(0.45f, 0.52f, 0.58f, 1f);
        footswitch.style.borderTopLeftRadius = 23f;
        footswitch.style.borderTopRightRadius = 23f;
        footswitch.style.borderBottomLeftRadius = 23f;
        footswitch.style.borderBottomRightRadius = 23f;
        return footswitch;
    }

    private static VisualElement CreatePedalJack()
    {
        VisualElement jack = new VisualElement();
        jack.name = "pedal-jack";
        jack.style.width = 20f;
        jack.style.height = 52f;
        jack.style.flexDirection = FlexDirection.Row;
        jack.style.alignItems = Align.Center;
        jack.style.justifyContent = Justify.FlexStart;

        VisualElement jackOuter = new VisualElement();
        jackOuter.name = "pedal-jack-outer";
        jackOuter.style.width = 8f;
        jackOuter.style.height = 38f;
        jackOuter.style.backgroundColor = new Color(0.33f, 0.36f, 0.40f, 1f);
        jackOuter.style.borderTopWidth = 2f;
        jackOuter.style.borderRightWidth = 1f;
        jackOuter.style.borderBottomWidth = 3f;
        jackOuter.style.borderLeftWidth = 2f;
        jackOuter.style.borderTopColor = new Color(0.54f, 0.58f, 0.64f, 1f);
        jackOuter.style.borderRightColor = new Color(0.22f, 0.25f, 0.29f, 1f);
        jackOuter.style.borderBottomColor = new Color(0.12f, 0.14f, 0.17f, 1f);
        jackOuter.style.borderLeftColor = new Color(0.26f, 0.30f, 0.34f, 1f);

        VisualElement jackInner = new VisualElement();
        jackInner.name = "pedal-jack-inner";
        jackInner.style.width = 12f;
        jackInner.style.height = 50f;
        jackInner.style.marginLeft = 0f;
        jackInner.style.backgroundColor = new Color(0.27f, 0.30f, 0.34f, 1f);
        jackInner.style.borderTopWidth = 1f;
        jackInner.style.borderRightWidth = 1f;
        jackInner.style.borderBottomWidth = 2f;
        jackInner.style.borderLeftWidth = 1f;
        jackInner.style.borderTopColor = new Color(0.47f, 0.52f, 0.58f, 1f);
        jackInner.style.borderRightColor = new Color(0.17f, 0.20f, 0.24f, 1f);
        jackInner.style.borderBottomColor = new Color(0.09f, 0.11f, 0.14f, 1f);
        jackInner.style.borderLeftColor = new Color(0.20f, 0.24f, 0.28f, 1f);

        VisualElement jackReflection = new VisualElement();
        jackReflection.name = "pedal-jack-reflection";
        jackReflection.style.position = Position.Absolute;
        jackReflection.style.left = 1f;
        jackReflection.style.top = 4f;
        jackReflection.style.width = 2f;
        jackReflection.style.height = 16f;
        jackReflection.style.backgroundColor = new Color(0.80f, 0.86f, 0.92f, 0.30f);

        jackOuter.Add(jackReflection);
        jack.Add(jackOuter);
        jack.Add(jackInner);
        return jack;
    }

    private static void SetPedalJackSize(VisualElement jack, float width, float height)
    {
        if (jack == null)
            return;

        jack.style.width = width;
        jack.style.height = height;

        VisualElement jackOuter = jack.Q<VisualElement>("pedal-jack-outer");
        if (jackOuter != null)
        {
            float outerWidth = Mathf.Clamp(width * 0.40f, 6f, 16f);
            float outerHeight = Mathf.Clamp(height * 0.74f, 18f, 50f);
            jackOuter.style.width = outerWidth;
            jackOuter.style.height = outerHeight;
        }

        VisualElement jackInner = jack.Q<VisualElement>("pedal-jack-inner");
        if (jackInner != null)
        {
            float innerWidth = Mathf.Clamp(width * 0.60f, 10f, 22f);
            float innerHeight = Mathf.Clamp(height * 0.94f, 24f, 64f);
            jackInner.style.width = innerWidth;
            jackInner.style.height = innerHeight;
            jackInner.style.marginLeft = 0f;
        }

        VisualElement jackReflection = jack.Q<VisualElement>("pedal-jack-reflection");
        if (jackReflection != null)
        {
            float outerWidth = jackOuter != null ? jackOuter.resolvedStyle.width : width * 0.34f;
            float outerHeight = jackOuter != null ? jackOuter.resolvedStyle.height : height * 0.74f;
            jackReflection.style.left = Mathf.Clamp(outerWidth * 0.16f, 1f, 4f);
            jackReflection.style.top = Mathf.Clamp(outerHeight * 0.12f, 1f, 7f);
            jackReflection.style.width = Mathf.Clamp(outerWidth * 0.22f, 2f, 5f);
            jackReflection.style.height = Mathf.Clamp(outerHeight * 0.44f, 10f, 26f);
        }
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
        Color borderColor = new Color(0.50f, 0.47f, 0.82f, 0.95f);
        element.style.borderTopColor = borderColor;
        element.style.borderBottomColor = borderColor;
        element.style.borderLeftColor = borderColor;
        element.style.borderRightColor = borderColor;
    }

    private static void ApplyFont(VisualElement root, FontDefinition font)
    {
        root.style.unityFontDefinition = font;
        foreach (VisualElement child in root.Children())
            ApplyFont(child, font);
    }

    private static (Font body, Font title) ResolveUiFonts(Font fallbackFont)
    {
        Font body = LoadRuntimeFont("Fonts/PixelArtFont") ?? LoadRuntimeFont("PixelArtFont");
        Font title = LoadRuntimeFont("Fonts/ArcadeFont") ?? LoadRuntimeFont("ArcadeFont");

        body ??= LoadProjectFont("Assets/UI/PixelArtFont.TTF");
        title ??= LoadProjectFont("Assets/UI/ArcadeFont.ttf");

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

    private static Font LoadRuntimeFont(string resourcesPath)
    {
        if (string.IsNullOrWhiteSpace(resourcesPath))
            return null;

        return Resources.Load<Font>(resourcesPath);
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

    private static PanelSettings sharedPanelSettings;

    private static PanelSettings ResolvePanelSettings()
    {
        if (sharedPanelSettings != null)
            return sharedPanelSettings;

        PanelSettings existing = Resources.FindObjectsOfTypeAll<PanelSettings>()
            .Where(candidate => candidate != null)
            .OrderByDescending(candidate => candidate.themeStyleSheet != null)
            .ThenByDescending(candidate => candidate.textSettings != null)
            .ThenByDescending(candidate => candidate.name == "PanelSettings")
            .FirstOrDefault();

        if (existing != null)
        {
            sharedPanelSettings = existing;
            return sharedPanelSettings;
        }

        PanelSettings runtimeAsset = Resources.Load<PanelSettings>("UIToolkitRuntimePanelSettings");
        sharedPanelSettings = runtimeAsset != null
            ? ScriptableObject.Instantiate(runtimeAsset)
            : ScriptableObject.CreateInstance<PanelSettings>();
        sharedPanelSettings.name = "TabsSongHeaderRuntimePanelSettings";
        return sharedPanelSettings;
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
        bool isHighway3D = owner != null && owner.renderMode == GuitarRenderMode.Highway3D;

        float songSize = Mathf.Clamp(screenHeight * 0.052f, 40f, 64f);
        float trackSize = Mathf.Clamp(screenHeight * 0.032f, 24f, 40f);
        float pauseSize = Mathf.Clamp(screenHeight * 0.135f, 112f, 170f);
        float bodySize = Mathf.Clamp(screenHeight * 0.036f, 30f, 50f);
        float pedalWidth = Mathf.Clamp(Screen.width * 0.15f, 430f, 620f);
        float pedalHeight = Mathf.Clamp(screenHeight * 0.30f, 280f, 560f);
        float knobSize = Mathf.Clamp(pedalHeight * 0.24f, 42f, 78f);
        float ledSize = Mathf.Clamp(knobSize * 0.42f, 12f, 20f);
        float footswitchSize = Mathf.Clamp(pedalHeight * 0.23f, 42f, 74f);
        float meterWidth = Mathf.Clamp(pedalWidth * 0.34f, 200f, 300f);
        float meterHeight = Mathf.Clamp(pedalHeight * 0.30f, 72f, 120f);

        songNameLabel.style.fontSize = songSize;
        trackNameLabel.style.fontSize = trackSize;
        speedBadgeLabel.style.fontSize = bodySize * 0.66f;
        detectorStatusLabel.style.fontSize = bodySize * 0.66f;
        statusDotLabel.style.fontSize = bodySize * 0.66f;
        float techniqueLegendSize = bodySize * 0.66f;
        foreach (Label iconLabel in techniqueLegendIconLabels)
            iconLabel.style.fontSize = techniqueLegendSize;
        foreach (Label textLabel in techniqueLegendTextLabels)
            textLabel.style.fontSize = techniqueLegendSize;
        scoreTitleLabel.style.fontSize = bodySize * 0.48f;
        scorePercentLabel.style.fontSize = bodySize * 1.30f;
        noteTallyLabel.style.fontSize = bodySize * 0.58f;
        scorePedalBrandLabel.style.fontSize = Mathf.Clamp(bodySize * 0.43f, 14f, 24f);
        inputMeterLabel.style.fontSize = Mathf.Clamp(bodySize * 0.44f, 13f, 20f);
        inputMeterWrap.style.width = meterWidth;
        inputMeterFace.style.width = meterWidth;
        songProgressTrack.style.width = meterWidth;
        inputMeterFace.style.height = meterHeight;
        LayoutInputMeterGraphics(meterWidth, meterHeight);
        scorePedalBody.style.width = pedalWidth;
        float meterLabelFont = Mathf.Clamp(bodySize * 0.44f, 13f, 20f);
        float scoreTitleFont = bodySize * 0.48f;
        float scoreFont = bodySize * 1.30f;
        float tallyFont = bodySize * 0.58f;
        float meterLabelHeight = meterLabelFont * 1.45f;
        float scoreTitleLineHeight = scoreTitleFont * 1.35f;
        float scoreLineHeight = scoreFont * 1.35f;
        float tallyLineHeight = tallyFont * 1.65f;
        float screenPaddingAndSpacing = 10f + 8f + 8f + 1f + 1f + 12f;
        float requiredScreenHeight = meterHeight + meterLabelHeight + scoreTitleLineHeight + scoreLineHeight + tallyLineHeight + screenPaddingAndSpacing;

        float topRowHeight = Mathf.Max(ledSize, Mathf.Clamp(bodySize * 0.33f, 12f, 19f) * 1.25f);
        float fixedPedalContentHeight = 16f + topRowHeight + 6f + knobSize + 7f + 8f + footswitchSize + 16f;
        float minPedalHeightForContent = fixedPedalContentHeight + requiredScreenHeight;
        if (pedalHeight < minPedalHeightForContent)
        {
            pedalHeight = Mathf.Clamp(minPedalHeightForContent, 300f, 640f);
            knobSize = Mathf.Clamp(pedalHeight * 0.24f, 42f, 80f);
            ledSize = Mathf.Clamp(knobSize * 0.42f, 12f, 20f);
            footswitchSize = Mathf.Clamp(pedalHeight * 0.23f, 42f, 76f);
            meterHeight = Mathf.Clamp(pedalHeight * 0.30f, 72f, 130f);
            inputMeterFace.style.height = meterHeight;
            LayoutInputMeterGraphics(meterWidth, meterHeight);
            requiredScreenHeight = meterHeight + meterLabelHeight + scoreTitleLineHeight + scoreLineHeight + tallyLineHeight + screenPaddingAndSpacing;
        }

        scorePlate.style.height = pedalHeight + 44f;
        scorePedalBody.style.height = pedalHeight;
        float screenHeightTarget = Mathf.Max(140f, requiredScreenHeight);
        scorePedalScreen.style.height = screenHeightTarget;
        scorePedalScreen.style.minHeight = screenHeightTarget;
        scorePedalScreen.style.maxHeight = screenHeightTarget;

        float jackHeight = Mathf.Clamp(pedalHeight * 0.34f, 60f, 102f);
        float jackWidth = Mathf.Clamp(jackHeight * 0.44f, 22f, 40f);
        float jackOffset = Mathf.Max(0f, jackWidth);
        float jackTop = pedalHeight * 0.36f;
        SetPedalJackSize(scorePedalInputJack, jackWidth, jackHeight);
        scorePedalInputJack.style.left = -jackOffset;
        scorePedalInputJack.style.top = jackTop;
        SetPedalJackSize(scorePedalOutputJack, jackWidth, jackHeight);
        scorePedalOutputJack.style.right = -jackOffset;
        scorePedalOutputJack.style.top = jackTop;
        scorePedalKnobLeft.style.width = knobSize;
        scorePedalKnobLeft.style.height = knobSize;
        scorePedalKnobLeft.style.borderTopLeftRadius = knobSize * 0.5f;
        scorePedalKnobLeft.style.borderTopRightRadius = knobSize * 0.5f;
        scorePedalKnobLeft.style.borderBottomLeftRadius = knobSize * 0.5f;
        scorePedalKnobLeft.style.borderBottomRightRadius = knobSize * 0.5f;
        scorePedalKnobMid.style.width = knobSize;
        scorePedalKnobMid.style.height = knobSize;
        scorePedalKnobMid.style.borderTopLeftRadius = knobSize * 0.5f;
        scorePedalKnobMid.style.borderTopRightRadius = knobSize * 0.5f;
        scorePedalKnobMid.style.borderBottomLeftRadius = knobSize * 0.5f;
        scorePedalKnobMid.style.borderBottomRightRadius = knobSize * 0.5f;
        scorePedalKnobRight.style.width = knobSize;
        scorePedalKnobRight.style.height = knobSize;
        scorePedalKnobRight.style.borderTopLeftRadius = knobSize * 0.5f;
        scorePedalKnobRight.style.borderTopRightRadius = knobSize * 0.5f;
        scorePedalKnobRight.style.borderBottomLeftRadius = knobSize * 0.5f;
        scorePedalKnobRight.style.borderBottomRightRadius = knobSize * 0.5f;
        SetKnobIndicatorSize(scorePedalKnobLeft, knobSize);
        SetKnobIndicatorSize(scorePedalKnobMid, knobSize);
        SetKnobIndicatorSize(scorePedalKnobRight, knobSize);
        scorePedalLed.style.width = ledSize;
        scorePedalLed.style.height = ledSize;
        scorePedalLed.style.borderTopLeftRadius = ledSize * 0.5f;
        scorePedalLed.style.borderTopRightRadius = ledSize * 0.5f;
        scorePedalLed.style.borderBottomLeftRadius = ledSize * 0.5f;
        scorePedalLed.style.borderBottomRightRadius = ledSize * 0.5f;
        scorePedalFootswitch.style.width = footswitchSize;
        scorePedalFootswitch.style.height = footswitchSize;
        scorePedalFootswitch.style.borderTopLeftRadius = footswitchSize * 0.5f;
        scorePedalFootswitch.style.borderTopRightRadius = footswitchSize * 0.5f;
        scorePedalFootswitch.style.borderBottomLeftRadius = footswitchSize * 0.5f;
        scorePedalFootswitch.style.borderBottomRightRadius = footswitchSize * 0.5f;
        scorePedalFootswitchRight.style.width = footswitchSize;
        scorePedalFootswitchRight.style.height = footswitchSize;
        scorePedalFootswitchRight.style.borderTopLeftRadius = footswitchSize * 0.5f;
        scorePedalFootswitchRight.style.borderTopRightRadius = footswitchSize * 0.5f;
        scorePedalFootswitchRight.style.borderBottomLeftRadius = footswitchSize * 0.5f;
        scorePedalFootswitchRight.style.borderBottomRightRadius = footswitchSize * 0.5f;
        judgePopupFontSize = Mathf.Clamp(screenHeight * 0.046f, 38f, 66f);
        pauseTitleLabel.style.fontSize = pauseSize;
        pauseHintLabel.style.fontSize = bodySize * 0.85f;
        pauseInfoLabel.style.fontSize = bodySize * 0.80f;
        speedValueLabel.style.fontSize = bodySize * 0.85f;
        songEndTitleLabel.style.fontSize = pauseSize * 0.82f;
        songEndSongLabel.style.fontSize = bodySize * 1.14f;
        songEndMetaLabel.style.fontSize = bodySize * 0.82f;
        songEndSpeedValueLabel.style.fontSize = bodySize * 0.86f;
        songEndScoreLabel.style.fontSize = bodySize * 1.20f;
        songEndRatingLabel.style.fontSize = bodySize * 0.96f;
        songEndStatsLabel.style.fontSize = bodySize * 0.74f;
        settingsTrackLabel.style.fontSize = bodySize * 0.90f;
        settingsOffsetLabel.style.fontSize = bodySize * 0.80f;
        settingsTabSpeedLabel.style.fontSize = bodySize * 0.80f;
        settingsStartDelayLabel.style.fontSize = bodySize * 0.80f;

        float buttonFontSize = Mathf.Clamp(screenHeight * 0.030f, 28f, 44f);
        float buttonHeight = Mathf.Clamp(screenHeight * 0.078f, 64f, 98f);
        float globalCardMaxHeight = Mathf.Clamp(screenHeight * 0.90f, 580f, 1720f);
        songEndCard.style.minHeight = Mathf.Clamp(screenHeight * 0.74f, 640f, 1180f);

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

        foreach (Label label in document.rootVisualElement.Query<Label>().Class("global-section-title").ToList())
            label.style.fontSize = buttonFontSize * 0.95f;

        foreach (Label label in document.rootVisualElement.Query<Label>().Class("global-setting-title").ToList())
            label.style.fontSize = buttonFontSize;

        foreach (Label label in document.rootVisualElement.Query<Label>().Class("global-setting-help").ToList())
            label.style.fontSize = buttonFontSize * 0.78f;

        foreach (Label label in document.rootVisualElement.Query<Label>().Class("global-setting-value").ToList())
            label.style.fontSize = buttonFontSize * 0.82f;

        foreach (Label label in document.rootVisualElement.Query<Label>().Class("global-setting-enum-value").ToList())
            label.style.fontSize = buttonFontSize;

        globalSettingsCard.style.maxHeight = globalCardMaxHeight;

        if (isHighway3D)
        {
            scorePlate.style.left = StyleKeyword.Auto;
            scorePlate.style.right = 24f;
            scorePlate.style.width = pedalWidth + 56f;
            scorePlate.style.alignItems = Align.FlexEnd;
        }
        else
        {
            scorePlate.style.left = 0f;
            scorePlate.style.right = 0f;
            scorePlate.style.width = StyleKeyword.Auto;
            scorePlate.style.alignItems = Align.Center;
        }

        float pedalLeftEdge = isHighway3D
            ? Screen.width - pedalWidth - 56f
            : (Screen.width - pedalWidth) * 0.5f;
        float songCardAvailableWidth = pedalLeftEdge + 24f;
        float songCardWidth = Mathf.Clamp(songCardAvailableWidth, 520f, 1320f);
        songCard.style.width = songCardWidth;
        songCard.style.minWidth = songCardWidth;
        songCard.style.maxWidth = songCardWidth;
        songCard.style.marginRight = Mathf.Clamp(Screen.width * 0.03f, 24f, 52f);

        float titleMaxWidth = Mathf.Max(340f, songCardWidth - 36f);
        songNameLabel.style.maxWidth = titleMaxWidth;
        trackNameLabel.style.maxWidth = titleMaxWidth;
    }
}
