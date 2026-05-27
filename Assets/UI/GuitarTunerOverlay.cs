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

    private GuitarBridgeServer owner;
    private GuitarTunerService tunerService;
    private UIDocument document;
    private PanelSettings panelSettings;
    private FontDefinition fontDefinition;
    private bool isBuilt;
    private bool isVisible;
    private float animatedCents;
    private float animatedInputLevel;

    private VisualElement overlayRoot;
    private VisualElement needle;
    private VisualElement needleGlow;
    private VisualElement inputLevelFill;
    private Label titleLabel;
    private Label targetLabel;
    private Label detectedLabel;
    private Label centsLabel;
    private Label frequencyLabel;
    private Label statusLabel;
    private Label routeLabel;
    private Button autoModeButton;
    private Button manualModeButton;
    private Button backButton;
    private readonly List<Button> targetButtons = new List<Button>();

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

        VisualElement root = document.rootVisualElement;
        root.styleSheets.Clear();
        root.style.flexGrow = 1f;
        root.style.width = Length.Percent(100f);
        root.style.height = Length.Percent(100f);
        root.pickingMode = PickingMode.Position;

        BuildUi(root);
        SetVisible(false);
        isBuilt = true;
    }

    public void SetVisible(bool visible)
    {
        isVisible = visible;
        if (overlayRoot != null)
            overlayRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void Update()
    {
        if (!isVisible || tunerService == null)
            return;

        RefreshUi(tunerService.GetSnapshot());
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
        overlayRoot.style.paddingLeft = 84f;
        overlayRoot.style.paddingRight = 84f;
        overlayRoot.style.paddingTop = 56f;
        overlayRoot.style.paddingBottom = 54f;
        overlayRoot.style.backgroundColor = new Color(0.01f, 0.025f, 0.045f, 0.48f);
        overlayRoot.pickingMode = PickingMode.Position;

        VisualElement topBar = new VisualElement();
        topBar.style.flexDirection = FlexDirection.Row;
        topBar.style.alignItems = Align.Center;
        topBar.style.justifyContent = Justify.SpaceBetween;
        topBar.style.marginBottom = 26f;

        VisualElement titleColumn = new VisualElement();
        titleColumn.style.flexDirection = FlexDirection.Column;
        titleColumn.style.alignItems = Align.FlexStart;

        Label eyebrow = CreateLabel("STRING THEORY", 18f, new Color(0.58f, 0.82f, 0.96f, 0.95f), true, TextAnchor.MiddleLeft);
        eyebrow.style.letterSpacing = 2.5f;
        eyebrow.style.marginBottom = 8f;

        titleLabel = CreateLabel("Tuner", 58f, Color.white, true, TextAnchor.MiddleLeft);
        titleLabel.style.letterSpacing = 0.2f;

        titleColumn.Add(eyebrow);
        titleColumn.Add(titleLabel);

        backButton = new Button(() => owner?.CloseTunerFromUi()) { text = "Back" };
        StyleOutlineButton(backButton, 150f, 58f, new Color(0.90f, 0.94f, 0.98f, 0.96f), new Color(0.48f, 0.62f, 0.75f, 0.82f));

        topBar.Add(titleColumn);
        topBar.Add(backButton);

        VisualElement modeRow = new VisualElement();
        modeRow.style.flexDirection = FlexDirection.Row;
        modeRow.style.alignItems = Align.Center;
        modeRow.style.justifyContent = Justify.Center;
        modeRow.style.marginTop = 4f;
        modeRow.style.marginBottom = 18f;

        autoModeButton = new Button(() => owner?.SetTunerModeFromUi(0)) { text = "Auto" };
        manualModeButton = new Button(() => owner?.SetTunerModeFromUi(1)) { text = "Manual" };
        StyleSegmentButton(autoModeButton);
        StyleSegmentButton(manualModeButton);
        autoModeButton.style.marginRight = 10f;
        manualModeButton.style.marginLeft = 10f;

        modeRow.Add(autoModeButton);
        modeRow.Add(manualModeButton);

        VisualElement tunerRegion = new VisualElement();
        tunerRegion.style.flexGrow = 1f;
        tunerRegion.style.alignItems = Align.Center;
        tunerRegion.style.justifyContent = Justify.Center;
        tunerRegion.style.flexDirection = FlexDirection.Column;

        VisualElement meterShell = new VisualElement();
        meterShell.style.width = 940f;
        meterShell.style.height = 412f;
        meterShell.style.maxWidth = Length.Percent(96f);
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

        targetLabel = CreateLabel("E2", 124f, Color.white, true, TextAnchor.MiddleCenter);
        targetLabel.style.marginTop = -22f;
        targetLabel.style.marginBottom = 2f;
        targetLabel.style.letterSpacing = 0.4f;

        statusLabel = CreateLabel("Play a string", 34f, new Color(0.77f, 0.86f, 0.94f, 0.96f), true, TextAnchor.MiddleCenter);
        statusLabel.style.marginBottom = 10f;

        centsLabel = CreateLabel("0 cents", 40f, new Color(0.94f, 0.98f, 1f, 0.96f), true, TextAnchor.MiddleCenter);
        centsLabel.style.marginBottom = 14f;

        detectedLabel = CreateLabel("Detected: --", 26f, new Color(0.72f, 0.80f, 0.88f, 0.96f), false, TextAnchor.MiddleCenter);
        detectedLabel.style.marginBottom = 4f;

        frequencyLabel = CreateLabel("-- Hz", 26f, new Color(0.72f, 0.80f, 0.88f, 0.92f), false, TextAnchor.MiddleCenter);

        tunerRegion.Add(meterShell);
        tunerRegion.Add(targetLabel);
        tunerRegion.Add(statusLabel);
        tunerRegion.Add(centsLabel);
        tunerRegion.Add(detectedLabel);
        tunerRegion.Add(frequencyLabel);

        VisualElement targetsRow = new VisualElement();
        targetsRow.style.flexDirection = FlexDirection.Row;
        targetsRow.style.justifyContent = Justify.Center;
        targetsRow.style.alignItems = Align.Center;
        targetsRow.style.marginTop = 26f;
        targetsRow.style.marginBottom = 26f;

        GuitarTunerTarget[] targets = tunerService != null ? tunerService.GetSnapshot().targets : Array.Empty<GuitarTunerTarget>();
        for (int i = 0; i < targets.Length; i++)
        {
            int targetIndex = i;
            Button targetButton = new Button(() => owner?.SetTunerManualTargetFromUi(targetIndex)) { text = targets[i].noteName };
            StyleTargetButton(targetButton, targets[i], selected: false, tuned: false);
            targetButton.tooltip = $"{targets[i].label}  {targets[i].frequencyHz:F2} Hz";
            targetButton.style.marginLeft = 8f;
            targetButton.style.marginRight = 8f;
            targetButtons.Add(targetButton);
            targetsRow.Add(targetButton);
        }

        VisualElement bottomBar = new VisualElement();
        bottomBar.style.flexDirection = FlexDirection.Row;
        bottomBar.style.alignItems = Align.Center;
        bottomBar.style.justifyContent = Justify.SpaceBetween;
        bottomBar.style.marginTop = 12f;

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

        routeLabel = CreateLabel("Input 1", 22f, new Color(0.75f, 0.84f, 0.92f, 0.92f), false, TextAnchor.MiddleRight);
        routeLabel.style.minWidth = 360f;

        bottomBar.Add(levelGroup);
        bottomBar.Add(routeLabel);

        overlayRoot.Add(topBar);
        overlayRoot.Add(tunerRegion);
        overlayRoot.Add(targetsRow);
        overlayRoot.Add(modeRow);
        overlayRoot.Add(bottomBar);
        root.Add(overlayRoot);
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
        detectedLabel.text = snapshot.hasSignal
            ? $"Detected: {snapshot.detectedNoteName}"
            : "Detected: --";
        frequencyLabel.text = snapshot.hasSignal && snapshot.detectedFrequencyHz > 0f
            ? $"{snapshot.detectedFrequencyHz.ToString("F2", CultureInfo.InvariantCulture)} Hz"
            : $"{snapshot.targetFrequencyHz.ToString("F2", CultureInfo.InvariantCulture)} Hz target";
        routeLabel.text = string.IsNullOrWhiteSpace(snapshot.inputRouteLabel)
            ? "Input route: --"
            : $"Input route: {snapshot.inputRouteLabel}";

        StyleModeButton(autoModeButton, snapshot.mode == GuitarTunerMode.Automatic);
        StyleModeButton(manualModeButton, snapshot.mode == GuitarTunerMode.Manual);

        for (int i = 0; i < targetButtons.Count; i++)
        {
            GuitarTunerTarget target = snapshot.targets != null && i < snapshot.targets.Length ? snapshot.targets[i] : null;
            bool tuned = snapshot.tunedTargets != null && i < snapshot.tunedTargets.Length && snapshot.tunedTargets[i];
            targetButtons[i].text = tuned && target != null ? $"{target.noteName}\nOK" : target?.noteName ?? "--";
            StyleTargetButton(targetButtons[i], target, i == snapshot.selectedTargetIndex, tuned);
        }
    }

    private static string FormatCents(float cents)
    {
        if (Mathf.Abs(cents) < 0.05f)
            return "0 cents";

        string sign = cents > 0f ? "+" : string.Empty;
        return $"{sign}{cents.ToString("F0", CultureInfo.InvariantCulture)} cents";
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

    private void StyleSegmentButton(Button button)
    {
        StyleModeButton(button, selected: false);
        button.style.minWidth = 170f;
        button.style.height = 60f;
        button.style.fontSize = 25f;
        button.style.unityFontDefinition = fontDefinition;
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
        button.style.minWidth = 92f;
        button.style.height = 76f;
        button.style.fontSize = tuned ? 23f : 27f;
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

    private void OnDestroy()
    {
        if (panelSettings != null)
            Destroy(panelSettings);
    }
}
