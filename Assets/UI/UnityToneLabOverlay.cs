using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

public sealed class UnityToneLabOverlay : MonoBehaviour
{
    private enum ToneLabSidePanelMode
    {
        Pedal,
        Library
    }

    private enum ToneLabPresetModalMode
    {
        Create,
        SaveAs,
        ResetAll
    }

    private sealed class ToneSliderBinding
    {
        public Slider slider;
        public Label valueLabel;
        public Func<UnityToneLabRuntime.ToneLabSettings, float> getter;
        public Action<UnityToneLabRuntime.ToneLabSettings, float> setter;
        public Func<float, string> formatter;
    }

    private sealed class ToneToggleBinding
    {
        public Button toggleButton;
        public Func<UnityToneLabRuntime.ToneLabSettings, bool> getter;
        public Action<UnityToneLabRuntime.ToneLabSettings, bool> setter;
    }

    private GuitarBridgeServer owner;
    private UnityToneLabRuntime runtime;
    private UIDocument document;
    private PanelSettings panelSettings;
    private bool isBuilt;
    private bool isVisible;
    private bool suppressCallbacks;

    private VisualElement overlayRoot;
    private Label statusLabel;
    private Label routeLabel;
    private Label backendLabel;
    private ToneLabPedalBoardView pedalBoardView;
    private VisualElement pedalInspectorHost;
    private VisualElement pedalLibraryHost;
    private DropdownField presetDropdown;
    private DropdownField inputDropdown;
    private DropdownField outputDropdown;
    private DropdownField latencyDropdown;
    private Button createPresetButton;
    private Button refreshDevicesButton;
    private Button savePresetButton;
    private Button saveAsPresetButton;
    private Button deletePresetButton;
    private Button resetAllButton;
    private Button startButton;
    private Button stopButton;
    private Button backButton;
    private VisualElement rigPanelCard;
    private VisualElement pedalSidePanelCard;
    private VisualElement sidePanelHost;
    private Label sidePanelTitleLabel;
    private Label sidePanelSubtitleLabel;
    private ScrollView rigSettingsScroll;
    private ScrollView pedalInspectorScroll;
    private ScrollView pedalLibraryScroll;
    private VisualElement presetModalScrim;
    private TextField presetNameField;
    private Button presetCreateButton;
    private Button presetCancelButton;
    private Label presetModalTitleLabel;
    private Label presetModalSubtitleLabel;
    private VisualElement presetNameSection;
    private VisualElement actionToast;
    private Label actionToastLabel;
    private string selectedPedalInstanceId = string.Empty;
    private ToneLabSidePanelMode sidePanelMode = ToneLabSidePanelMode.Pedal;
    private ToneLabPresetModalMode presetModalMode = ToneLabPresetModalMode.Create;
    private readonly Dictionary<string, string> presetChoiceToId = new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly List<ToneSliderBinding> sliderBindings = new List<ToneSliderBinding>();
    private readonly List<ToneToggleBinding> toggleBindings = new List<ToneToggleBinding>();

    public void Initialize(GuitarBridgeServer owner, UnityToneLabRuntime runtime)
    {
        this.owner = owner;
        this.runtime = runtime;

        if (isBuilt)
            return;

        document = gameObject.GetComponent<UIDocument>();
        if (document == null)
            document = gameObject.AddComponent<UIDocument>();

        panelSettings = ResolvePanelSettings();
        document.panelSettings = panelSettings;

        VisualElement root = document.rootVisualElement;
        root.styleSheets.Clear();
        root.style.flexGrow = 1f;
        root.style.width = Length.Percent(100f);
        root.style.height = Length.Percent(100f);
        root.pickingMode = PickingMode.Position;

        BuildUi(root);
        if (overlayRoot != null)
            overlayRoot.style.display = DisplayStyle.None;
        isVisible = false;
        isBuilt = true;
    }

    public bool IsCapturingKeyboardInput
    {
        get
        {
            return isVisible
                && presetModalScrim != null
                && presetModalScrim.style.display == DisplayStyle.Flex;
        }
    }

    public void SetVisible(bool visible)
    {
        if (!isBuilt || overlayRoot == null)
            return;

        DisplayStyle targetDisplay = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (isVisible == visible && overlayRoot.style.display == targetDisplay)
            return;

        isVisible = visible;
        overlayRoot.style.display = targetDisplay;
        if (visible)
        {
            sidePanelMode = ToneLabSidePanelMode.Pedal;
            RefreshUi(syncControls: true, refreshDevices: true);
        }
        else
        {
            CloseCreatePresetModal();
            HideActionToast();
        }
    }

    public void RefreshUi(bool syncControls, bool refreshDevices = false)
    {
        if (!isBuilt || runtime == null)
            return;

        bool refreshInteractiveState = syncControls || refreshDevices;
        if (refreshInteractiveState)
        {
            if (refreshDevices)
                runtime.RefreshInputDevices();

            UnityToneLabRuntime.ToneLabSettings settings = runtime.CurrentSettings;
            suppressCallbacks = true;

            List<string> deviceChoices = runtime.InputDevices != null && runtime.InputDevices.Length > 0
                ? runtime.InputDevices.ToList()
                : new List<string> { "No microphone inputs" };
            inputDropdown.choices = deviceChoices;
            string selectedInput = !string.IsNullOrWhiteSpace(settings.input_device_name) && deviceChoices.Contains(settings.input_device_name)
                ? settings.input_device_name
                : deviceChoices[0];
            inputDropdown.SetValueWithoutNotify(selectedInput);
            inputDropdown.SetEnabled(runtime.InputDevices != null && runtime.InputDevices.Length > 0);

            List<string> outputChoices = runtime.OutputDevices != null && runtime.OutputDevices.Length > 0
                ? runtime.OutputDevices.ToList()
                : new List<string> { "No output devices" };
            outputDropdown.choices = outputChoices;
            string selectedOutput = !string.IsNullOrWhiteSpace(settings.output_device_name) && outputChoices.Contains(settings.output_device_name)
                ? settings.output_device_name
                : outputChoices[0];
            outputDropdown.SetValueWithoutNotify(selectedOutput);
            outputDropdown.SetEnabled(runtime.OutputDevices != null && runtime.OutputDevices.Length > 0);

            List<string> latencyChoices = runtime.MonitoringLatencyOptions != null && runtime.MonitoringLatencyOptions.Length > 0
                ? runtime.MonitoringLatencyOptions.ToList()
                : new List<string> { "Low (128)" };
            latencyDropdown.choices = latencyChoices;
            string selectedLatency = latencyChoices.Contains(runtime.CurrentMonitoringLatencyOption)
                ? runtime.CurrentMonitoringLatencyOption
                : latencyChoices[0];
            latencyDropdown.SetValueWithoutNotify(selectedLatency);
            latencyDropdown.SetEnabled(true);

            UnityToneLabRuntime.ToneLabPreset[] presets = runtime.CurrentPresets;
            presetChoiceToId.Clear();
            List<string> presetChoices = new List<string>();
            for (int i = 0; i < presets.Length; i++)
            {
                UnityToneLabRuntime.ToneLabPreset preset = presets[i];
                if (preset == null || string.IsNullOrWhiteSpace(preset.preset_id))
                    continue;

                string presetName = string.IsNullOrWhiteSpace(preset.preset_name) ? $"Preset {i + 1}" : preset.preset_name.Trim();
                if (presetChoiceToId.ContainsKey(presetName))
                    presetName = $"{presetName}  [{i + 1}]";
                presetChoiceToId[presetName] = preset.preset_id;
                presetChoices.Add(presetName);
            }

            presetDropdown.choices = presetChoices;
            string selectedPresetChoice = presetChoices.Count > 0 ? presetChoices[0] : string.Empty;
            string currentPresetId = runtime.CurrentPresetId;
            if (!string.IsNullOrWhiteSpace(currentPresetId))
            {
                foreach (KeyValuePair<string, string> entry in presetChoiceToId)
                {
                    if (string.Equals(entry.Value, currentPresetId, StringComparison.Ordinal))
                    {
                        selectedPresetChoice = entry.Key;
                        break;
                    }
                }
            }

            presetDropdown.SetValueWithoutNotify(selectedPresetChoice);
            savePresetButton?.SetEnabled(!string.IsNullOrWhiteSpace(currentPresetId));
            deletePresetButton?.SetEnabled(!string.IsNullOrWhiteSpace(currentPresetId) && presets.Length > 1);

            for (int i = 0; i < sliderBindings.Count; i++)
            {
                ToneSliderBinding binding = sliderBindings[i];
                float value = binding.getter(settings);
                binding.slider.SetValueWithoutNotify(value);
                binding.valueLabel.text = binding.formatter(value);
            }

            for (int i = 0; i < toggleBindings.Count; i++)
            {
                ToneToggleBinding binding = toggleBindings[i];
                bool enabled = binding.getter(settings);
                ApplyToggleButtonState(binding.toggleButton, enabled);
            }
            UnityToneLabRuntime.ToneLabPedalSlot[] pedalChain = runtime.CurrentPedalChain;
            EnsureSelectedPedal(pedalChain);
            pedalBoardView?.Refresh(pedalChain, selectedPedalInstanceId);
            RefreshPedalLibrary(pedalChain);
            RefreshSidePanel(pedalChain);

            suppressCallbacks = false;
        }

        startButton.SetEnabled(!runtime.IsMonitoring && !runtime.IsAwaitingStartup);
        stopButton.SetEnabled(runtime.IsMonitoring || runtime.IsAwaitingStartup);
        RefreshSidePanelButtonStates();
    }

    private void BuildUi(VisualElement root)
    {
        Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        FontDefinition uiFontDefinition = FontDefinition.FromFont(uiFont);
        Font toneLabTitleFont = Resources.Load<Font>("MetalLord") ?? Resources.Load<Font>("Fonts/MetalLord") ?? uiFont;
        FontDefinition toneLabTitleFontDefinition = FontDefinition.FromFont(toneLabTitleFont);

        overlayRoot = new VisualElement();
        overlayRoot.AddToClassList("tone-lab-overlay");
        overlayRoot.pickingMode = PickingMode.Position;
        overlayRoot.style.display = DisplayStyle.None;
        ApplyOverlayRootStyle(overlayRoot);

        VisualElement backdrop = new VisualElement();
        backdrop.AddToClassList("tone-lab-backdrop");
        ApplyBackdropStyle(backdrop);
        overlayRoot.Add(backdrop);

        VisualElement window = new VisualElement();
        window.AddToClassList("tone-lab-window");
        ApplyWindowStyle(window);
        overlayRoot.Add(window);

        VisualElement mainContent = new VisualElement();
        mainContent.style.flexGrow = 1f;
        mainContent.style.minHeight = 0f;
        mainContent.style.flexDirection = FlexDirection.Row;
        mainContent.style.overflow = Overflow.Hidden;
        window.Add(mainContent);

        VisualElement boardColumn = new VisualElement();
        boardColumn.style.flexGrow = 1f;
        boardColumn.style.minHeight = 0f;
        boardColumn.style.marginRight = 14f;
        mainContent.Add(boardColumn);

        VisualElement header = new VisualElement();
        header.AddToClassList("tone-lab-header");
        header.style.flexDirection = FlexDirection.Column;
        header.style.alignItems = Align.FlexStart;
        header.style.marginBottom = 18f;
        header.style.flexShrink = 0f;
        boardColumn.Add(header);

        Label titleLabel = CreateLabel("Tone Lab", "tone-lab-title", toneLabTitleFontDefinition);
        titleLabel.style.color = Color.white;
        titleLabel.style.fontSize = 30f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
        titleLabel.style.marginBottom = 8f;
        header.Add(titleLabel);

        VisualElement boardToolbar = new VisualElement();
        boardToolbar.style.flexDirection = FlexDirection.Row;
        boardToolbar.style.justifyContent = Justify.FlexEnd;
        boardToolbar.style.alignItems = Align.Center;
        boardToolbar.style.marginBottom = 10f;
        boardToolbar.style.flexShrink = 0f;
        boardColumn.Add(boardToolbar);

        VisualElement boardToolbarPresetGroup = new VisualElement();
        boardToolbarPresetGroup.style.flexDirection = FlexDirection.Row;
        boardToolbarPresetGroup.style.alignItems = Align.FlexEnd;
        boardToolbarPresetGroup.style.justifyContent = Justify.FlexEnd;
        boardToolbarPresetGroup.style.flexGrow = 1f;
        boardToolbar.Add(boardToolbarPresetGroup);

        VisualElement presetField = new VisualElement();
        presetField.style.width = 248f;
        presetField.style.marginRight = 10f;
        presetField.style.alignItems = Align.FlexEnd;
        boardToolbarPresetGroup.Add(presetField);

        Label presetLabel = new Label("Preset");
        presetLabel.style.color = new Color(0.66f, 0.69f, 0.73f, 0.95f);
        presetLabel.style.fontSize = 12f;
        presetLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        presetLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        presetLabel.style.marginBottom = 4f;
        presetField.Add(presetLabel);

        presetDropdown = new DropdownField();
        ApplyDropdownStyle(presetDropdown);
        presetDropdown.style.width = 248f;
        presetDropdown.style.minWidth = 248f;
        presetDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks || runtime == null)
                return;

            if (presetChoiceToId.TryGetValue(evt.newValue, out string presetId))
            {
                runtime.SelectPreset(presetId);
            }

            RefreshUi(syncControls: true);
        });
        presetField.Add(presetDropdown);

        createPresetButton = CreateButton("+", "tone-lab-button tone-lab-button-secondary", () => OpenPresetModal(ToneLabPresetModalMode.Create));
        createPresetButton.style.minWidth = 42f;
        createPresetButton.style.width = 42f;
        createPresetButton.style.height = 42f;
        createPresetButton.style.fontSize = 22f;
        createPresetButton.style.paddingLeft = 0f;
        createPresetButton.style.paddingRight = 0f;
        createPresetButton.style.marginRight = 10f;
        boardToolbarPresetGroup.Add(createPresetButton);

        savePresetButton = CreateButton("Save", "tone-lab-button tone-lab-button-secondary", () =>
        {
            if (runtime == null || string.IsNullOrWhiteSpace(runtime.CurrentPresetId))
                return;

            runtime.SaveCurrentToPreset(runtime.CurrentPresetId);
            RefreshUi(syncControls: true);
            ShowActionToast($"Saved preset \"{GetPresetName(runtime.CurrentPresetId)}\".");
        });
        savePresetButton.style.minWidth = 90f;
        savePresetButton.style.height = 42f;
        savePresetButton.style.fontSize = 15f;
        savePresetButton.style.marginRight = 10f;
        boardToolbarPresetGroup.Add(savePresetButton);

        saveAsPresetButton = CreateButton("Save As", "tone-lab-button tone-lab-button-secondary", () => OpenPresetModal(ToneLabPresetModalMode.SaveAs));
        saveAsPresetButton.style.minWidth = 112f;
        saveAsPresetButton.style.height = 42f;
        saveAsPresetButton.style.fontSize = 15f;
        saveAsPresetButton.style.marginRight = 10f;
        boardToolbarPresetGroup.Add(saveAsPresetButton);

        deletePresetButton = CreateButton("Delete", "tone-lab-button tone-lab-button-danger", () =>
        {
            if (runtime == null || string.IsNullOrWhiteSpace(runtime.CurrentPresetId))
                return;

            string deletedPresetName = GetPresetName(runtime.CurrentPresetId);
            if (runtime.DeletePreset(runtime.CurrentPresetId))
            {
                RefreshUi(syncControls: true);
                ShowActionToast($"Deleted preset \"{deletedPresetName}\".", true);
            }
        });
        deletePresetButton.style.minWidth = 102f;
        deletePresetButton.style.height = 42f;
        deletePresetButton.style.fontSize = 15f;
        deletePresetButton.style.marginRight = 10f;
        boardToolbarPresetGroup.Add(deletePresetButton);

        resetAllButton = CreateButton("Reset All", "tone-lab-button tone-lab-button-danger", () => OpenPresetModal(ToneLabPresetModalMode.ResetAll));
        resetAllButton.style.minWidth = 126f;
        resetAllButton.style.height = 42f;
        resetAllButton.style.fontSize = 15f;
        resetAllButton.style.marginRight = 0f;
        boardToolbarPresetGroup.Add(resetAllButton);

        pedalBoardView = new ToneLabPedalBoardView();
        pedalBoardView.AddPedalRequested += () =>
        {
            sidePanelMode = ToneLabSidePanelMode.Library;
            RefreshUi(syncControls: true);
        };
        pedalBoardView.PedalSelected += pedalInstanceId =>
        {
            selectedPedalInstanceId = pedalInstanceId;
            sidePanelMode = ToneLabSidePanelMode.Pedal;
            RefreshUi(syncControls: true);
        };
        pedalBoardView.PedalEnabledChanged += (pedalInstanceId, enabled) =>
        {
            runtime?.SetPedalEnabled(pedalInstanceId, enabled);
            sidePanelMode = ToneLabSidePanelMode.Pedal;
            RefreshUi(syncControls: true);
        };
        pedalBoardView.PedalOrderCommitted += orderedPedalIds =>
        {
            runtime?.SetPedalChainOrder(orderedPedalIds);
            RefreshUi(syncControls: true);
        };
        pedalBoardView.Root.style.flexGrow = 1f;
        pedalBoardView.Root.style.minHeight = 0f;
        boardColumn.Add(pedalBoardView.Root);

        VisualElement sidePanel = new VisualElement();
        sidePanel.style.width = 560f;
        sidePanel.style.minWidth = 560f;
        sidePanel.style.maxWidth = 560f;
        sidePanel.style.flexShrink = 0f;
        sidePanel.style.flexGrow = 0f;
        sidePanel.style.height = Length.Percent(100f);
        sidePanel.style.minHeight = 0f;
        sidePanel.style.flexDirection = FlexDirection.Column;
        sidePanel.style.paddingLeft = 18f;
        sidePanel.style.paddingRight = 4f;
        sidePanel.style.paddingTop = 2f;
        sidePanel.style.paddingBottom = 0f;
        sidePanel.style.borderLeftWidth = 1f;
        sidePanel.style.borderLeftColor = new Color(1f, 1f, 1f, 0.18f);
        mainContent.Add(sidePanel);

        VisualElement rigPanelHost;
        rigPanelCard = CreateSideSectionCard(sidePanel, "Rig Setup", "Gain staging and audio control for the full rig.", out _, out _, out rigPanelHost);
        rigPanelCard.style.flexGrow = 0f;
        rigPanelCard.style.minHeight = 300f;
        rigPanelCard.style.maxHeight = 360f;
        rigPanelCard.style.flexShrink = 0f;
        rigPanelCard.style.marginBottom = 16f;

        sidePanelTitleLabel = new Label("Selected Pedal");
        sidePanelTitleLabel.style.color = Color.white;
        sidePanelTitleLabel.style.fontSize = 24f;
        sidePanelTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        sidePanelSubtitleLabel = new Label("Pedal settings and library.");
        sidePanelSubtitleLabel.style.color = new Color(0.60f, 0.63f, 0.68f, 0.95f);
        sidePanelSubtitleLabel.style.fontSize = 13f;
        sidePanelSubtitleLabel.style.whiteSpace = WhiteSpace.Normal;
        pedalSidePanelCard = CreateSideSectionCard(sidePanel, sidePanelTitleLabel, sidePanelSubtitleLabel, out sidePanelHost);
        pedalSidePanelCard.style.flexGrow = 1f;
        pedalSidePanelCard.style.minHeight = 0f;

        inputDropdown = new DropdownField();
        ApplyDropdownStyle(inputDropdown);
        inputDropdown.style.minWidth = 286f;
        inputDropdown.style.width = 286f;
        inputDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks || runtime == null)
                return;

            runtime.UpdateSettings(settings => settings.input_device_name = evt.newValue, restartMonitoring: true);
            RefreshUi(syncControls: true);
        });

        outputDropdown = new DropdownField();
        ApplyDropdownStyle(outputDropdown);
        outputDropdown.style.minWidth = 286f;
        outputDropdown.style.width = 286f;
        outputDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks || runtime == null)
                return;

            runtime.UpdateSettings(settings => settings.output_device_name = evt.newValue, restartMonitoring: true);
            RefreshUi(syncControls: true);
        });

        latencyDropdown = new DropdownField();
        ApplyDropdownStyle(latencyDropdown);
        latencyDropdown.style.minWidth = 192f;
        latencyDropdown.style.width = 192f;
        latencyDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks || runtime == null)
                return;

            runtime.UpdateSettings(settings => settings.monitoring_buffer_size = ParseLatencyPresetBufferSize(evt.newValue), restartMonitoring: true);
            RefreshUi(syncControls: true);
        });

        refreshDevicesButton = CreateButton("Refresh", "tone-lab-button tone-lab-button-secondary", () =>
        {
            runtime?.RefreshInputDevices();
            RefreshUi(syncControls: true, refreshDevices: true);
        });
        refreshDevicesButton.style.minWidth = 120f;
        refreshDevicesButton.style.height = 38f;
        refreshDevicesButton.style.fontSize = 14f;

        startButton = CreateButton("Start Audio", "tone-lab-button tone-lab-button-primary", () =>
        {
            runtime?.TryStartMonitoring();
            RefreshUi(syncControls: false);
        });
        startButton.style.minWidth = 140f;
        startButton.style.height = 38f;
        startButton.style.fontSize = 14f;

        stopButton = CreateButton("Stop Audio", "tone-lab-button tone-lab-button-secondary", () =>
        {
            runtime?.StopMonitoring();
            RefreshUi(syncControls: false);
        });
        stopButton.style.minWidth = 128f;
        stopButton.style.height = 38f;
        stopButton.style.fontSize = 14f;

        VisualElement routingRow = new VisualElement();
        routingRow.style.flexDirection = FlexDirection.Row;
        routingRow.style.alignItems = Align.FlexEnd;
        routingRow.style.justifyContent = Justify.FlexStart;
        routingRow.style.flexWrap = Wrap.NoWrap;
        routingRow.style.marginBottom = 6f;
        routingRow.style.width = Length.Auto();
        routingRow.Add(CreateToolbarField("Input", inputDropdown, 286f));
        routingRow.Add(CreateToolbarField("Output", outputDropdown, 286f));
        routingRow.Add(CreateToolbarField("Latency", latencyDropdown, 192f));
        header.Add(routingRow);
        
        rigSettingsScroll = new ScrollView(ScrollViewMode.Vertical);
        rigSettingsScroll.style.flexGrow = 1f;
        rigSettingsScroll.style.minHeight = 0f;
        rigSettingsScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        rigSettingsScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        rigSettingsScroll.style.marginRight = 0f;
        rigSettingsScroll.style.paddingRight = 0f;
        VisualElement rigSettingsHost = rigSettingsScroll.contentContainer;
        rigSettingsHost.style.flexDirection = FlexDirection.Column;
        rigSettingsHost.style.paddingRight = 4f;

        VisualElement transportRow = new VisualElement();
        transportRow.style.flexDirection = FlexDirection.Row;
        transportRow.style.alignItems = Align.Center;
        transportRow.style.flexWrap = Wrap.Wrap;
        transportRow.style.marginTop = 8f;
        transportRow.style.marginBottom = 14f;
        rigSettingsHost.Add(transportRow);
        transportRow.Add(refreshDevicesButton);
        transportRow.Add(startButton);
        transportRow.Add(stopButton);

        rigSettingsHost.Add(CreateCompactSliderField(
            "Input Gain",
            -36f,
            36f,
            value => $"{value:F1} dB",
            settings => settings.input_gain_db,
            (settings, value) => settings.input_gain_db = value,
            410f));

        rigSettingsHost.Add(CreateCompactSliderField(
            "Output Gain",
            -36f,
            36f,
            value => $"{value:F1} dB",
            settings => settings.output_gain_db,
            (settings, value) => settings.output_gain_db = value,
            410f));
        rigPanelHost.Add(rigSettingsScroll);

        pedalInspectorScroll = new ScrollView(ScrollViewMode.Vertical);
        pedalInspectorScroll.style.flexGrow = 1f;
        pedalInspectorScroll.style.minHeight = 0f;
        pedalInspectorScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        pedalInspectorScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        pedalInspectorHost = pedalInspectorScroll.contentContainer;

        pedalLibraryScroll = new ScrollView(ScrollViewMode.Vertical);
        pedalLibraryScroll.style.flexGrow = 1f;
        pedalLibraryScroll.style.minHeight = 0f;
        pedalLibraryScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        pedalLibraryScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        pedalLibraryHost = pedalLibraryScroll.contentContainer;
        pedalLibraryHost.style.flexDirection = FlexDirection.Column;
        pedalLibraryHost.style.paddingBottom = 8f;

        VisualElement footerRow = new VisualElement();
        footerRow.style.flexDirection = FlexDirection.Row;
        footerRow.style.alignItems = Align.Center;
        footerRow.style.justifyContent = Justify.FlexEnd;
        footerRow.style.marginTop = 10f;
        footerRow.style.flexShrink = 0f;
        window.Add(footerRow);

        backButton = CreateButton("Back", "tone-lab-button tone-lab-button-secondary", () => owner?.CloseToneLabFromUi());
        backButton.style.marginRight = 0f;
        footerRow.Add(backButton);

        presetModalScrim = new VisualElement();
        presetModalScrim.style.position = Position.Absolute;
        presetModalScrim.style.left = 0f;
        presetModalScrim.style.right = 0f;
        presetModalScrim.style.top = 0f;
        presetModalScrim.style.bottom = 0f;
        presetModalScrim.style.display = DisplayStyle.None;
        presetModalScrim.style.alignItems = Align.Center;
        presetModalScrim.style.justifyContent = Justify.Center;
        presetModalScrim.style.backgroundColor = new Color(0.01f, 0.02f, 0.03f, 0.78f);
        presetModalScrim.RegisterCallback<MouseDownEvent>(_ => CloseCreatePresetModal());
        overlayRoot.Add(presetModalScrim);

        VisualElement presetModalCard = new VisualElement();
        presetModalCard.style.width = 420f;
        presetModalCard.style.paddingLeft = 22f;
        presetModalCard.style.paddingRight = 22f;
        presetModalCard.style.paddingTop = 20f;
        presetModalCard.style.paddingBottom = 18f;
        presetModalCard.style.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        presetModalCard.style.borderTopWidth = 1f;
        presetModalCard.style.borderRightWidth = 1f;
        presetModalCard.style.borderBottomWidth = 1f;
        presetModalCard.style.borderLeftWidth = 1f;
        presetModalCard.style.borderTopColor = new Color(0.24f, 0.26f, 0.30f, 1f);
        presetModalCard.style.borderRightColor = new Color(0.17f, 0.18f, 0.21f, 1f);
        presetModalCard.style.borderBottomColor = new Color(0.12f, 0.13f, 0.15f, 1f);
        presetModalCard.style.borderLeftColor = new Color(0.17f, 0.18f, 0.21f, 1f);
        presetModalCard.style.borderTopLeftRadius = 16f;
        presetModalCard.style.borderTopRightRadius = 16f;
        presetModalCard.style.borderBottomLeftRadius = 16f;
        presetModalCard.style.borderBottomRightRadius = 16f;
        presetModalCard.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
        presetModalScrim.Add(presetModalCard);

        presetModalTitleLabel = new Label("Create Preset");
        presetModalTitleLabel.style.color = Color.white;
        presetModalTitleLabel.style.fontSize = 24f;
        presetModalTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        presetModalTitleLabel.style.marginBottom = 6f;
        presetModalCard.Add(presetModalTitleLabel);

        presetModalSubtitleLabel = new Label("Save the current pedalboard and gain staging as a reusable preset.");
        presetModalSubtitleLabel.style.color = new Color(0.66f, 0.69f, 0.73f, 0.96f);
        presetModalSubtitleLabel.style.fontSize = 13f;
        presetModalSubtitleLabel.style.whiteSpace = WhiteSpace.Normal;
        presetModalSubtitleLabel.style.marginBottom = 10f;
        presetModalCard.Add(presetModalSubtitleLabel);

        presetNameSection = new VisualElement();
        presetNameSection.style.flexDirection = FlexDirection.Column;
        presetNameSection.style.display = DisplayStyle.Flex;
        presetModalCard.Add(presetNameSection);

        Label presetNameCaption = new Label("Preset Name");
        presetNameCaption.style.color = new Color(0.84f, 0.86f, 0.90f, 0.96f);
        presetNameCaption.style.fontSize = 13f;
        presetNameCaption.style.unityFontStyleAndWeight = FontStyle.Bold;
        presetNameCaption.style.marginBottom = 6f;
        presetNameSection.Add(presetNameCaption);

        presetNameField = new TextField();
        presetNameField.style.marginBottom = 16f;
        presetNameField.style.height = 42f;
        presetNameField.style.color = Color.white;
        presetNameField.style.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
        presetNameField.style.borderTopWidth = 1f;
        presetNameField.style.borderRightWidth = 1f;
        presetNameField.style.borderBottomWidth = 1f;
        presetNameField.style.borderLeftWidth = 1f;
        presetNameField.style.borderTopColor = new Color(0.26f, 0.28f, 0.32f, 1f);
        presetNameField.style.borderRightColor = new Color(0.18f, 0.20f, 0.23f, 1f);
        presetNameField.style.borderBottomColor = new Color(0.14f, 0.16f, 0.18f, 1f);
        presetNameField.style.borderLeftColor = new Color(0.18f, 0.20f, 0.23f, 1f);
        presetNameField.style.borderTopLeftRadius = 10f;
        presetNameField.style.borderTopRightRadius = 10f;
        presetNameField.style.borderBottomLeftRadius = 10f;
        presetNameField.style.borderBottomRightRadius = 10f;
        presetNameField.RegisterCallback<AttachToPanelEvent>(_ => ApplyPresetNameFieldStyle());
        presetNameField.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                CommitCreatePreset();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                CloseCreatePresetModal();
                evt.StopPropagation();
            }
        });
        presetNameSection.Add(presetNameField);

        VisualElement presetModalActions = new VisualElement();
        presetModalActions.style.flexDirection = FlexDirection.Row;
        presetModalActions.style.justifyContent = Justify.FlexEnd;
        presetModalActions.style.alignItems = Align.Center;
        presetModalCard.Add(presetModalActions);

        presetCancelButton = CreateButton("Cancel", "tone-lab-button tone-lab-button-secondary", CloseCreatePresetModal);
        presetCancelButton.style.minWidth = 110f;
        presetCancelButton.style.height = 40f;
        presetCancelButton.style.fontSize = 15f;
        presetModalActions.Add(presetCancelButton);

        presetCreateButton = CreateButton("Create", "tone-lab-button tone-lab-button-primary", CommitCreatePreset);
        presetCreateButton.style.minWidth = 118f;
        presetCreateButton.style.height = 40f;
        presetCreateButton.style.fontSize = 15f;
        presetCreateButton.style.marginRight = 0f;
        presetModalActions.Add(presetCreateButton);

        actionToast = new VisualElement();
        actionToast.style.position = Position.Absolute;
        actionToast.style.right = 26f;
        actionToast.style.bottom = 78f;
        actionToast.style.display = DisplayStyle.None;
        actionToast.style.paddingLeft = 14f;
        actionToast.style.paddingRight = 14f;
        actionToast.style.paddingTop = 10f;
        actionToast.style.paddingBottom = 10f;
        actionToast.style.backgroundColor = new Color(0.15f, 0.18f, 0.20f, 0.98f);
        actionToast.style.borderTopLeftRadius = 12f;
        actionToast.style.borderTopRightRadius = 12f;
        actionToast.style.borderBottomLeftRadius = 12f;
        actionToast.style.borderBottomRightRadius = 12f;
        actionToast.style.borderTopWidth = 1f;
        actionToast.style.borderRightWidth = 1f;
        actionToast.style.borderBottomWidth = 1f;
        actionToast.style.borderLeftWidth = 1f;
        actionToast.style.borderTopColor = new Color(0.34f, 0.37f, 0.42f, 0.95f);
        actionToast.style.borderRightColor = new Color(0.24f, 0.26f, 0.30f, 0.98f);
        actionToast.style.borderBottomColor = new Color(0.18f, 0.20f, 0.23f, 0.98f);
        actionToast.style.borderLeftColor = new Color(0.24f, 0.26f, 0.30f, 0.98f);
        overlayRoot.Add(actionToast);

        actionToastLabel = new Label(string.Empty);
        actionToastLabel.style.color = Color.white;
        actionToastLabel.style.fontSize = 14f;
        actionToastLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        actionToast.Add(actionToastLabel);

        root.Add(overlayRoot);
        root.pickingMode = PickingMode.Ignore;
        overlayRoot.pickingMode = PickingMode.Position;
    }

    private void OpenPresetModal(ToneLabPresetModalMode mode)
    {
        if (presetModalScrim == null)
            return;

        presetModalMode = mode;
        bool isSaveAs = mode == ToneLabPresetModalMode.SaveAs;
        bool isResetAll = mode == ToneLabPresetModalMode.ResetAll;
        if (presetModalTitleLabel != null)
            presetModalTitleLabel.text = isResetAll
                ? "Reset All"
                : (isSaveAs ? "Save Preset As" : "Create Preset");
        if (presetModalSubtitleLabel != null)
            presetModalSubtitleLabel.text = isResetAll
                ? "Restore the factory preset library and the active rig. Audio device routing and latency stay as they are."
                : (isSaveAs
                    ? "Save the current pedalboard as a new custom preset without overwriting the active one."
                    : "Save the current pedalboard and gain staging as a reusable preset.");
        if (presetCreateButton != null)
        {
            presetCreateButton.text = isResetAll ? "Reset All" : (isSaveAs ? "Save As" : "Create");
            ApplyModalActionButtonStyle(presetCreateButton, isResetAll);
        }
        if (presetNameSection != null)
            presetNameSection.style.display = isResetAll ? DisplayStyle.None : DisplayStyle.Flex;
        presetNameField?.SetValueWithoutNotify(string.Empty);
        presetModalScrim.style.display = DisplayStyle.Flex;
        ApplyPresetNameFieldStyle();
        if (!isResetAll)
            presetNameField?.Focus();
    }

    private void CloseCreatePresetModal()
    {
        if (presetModalScrim == null)
            return;

        presetModalScrim.style.display = DisplayStyle.None;
    }

    private void CommitCreatePreset()
    {
        if (runtime == null)
            return;

        if (presetModalMode == ToneLabPresetModalMode.ResetAll)
        {
            runtime.ResetAllToFactoryDefaults();
            ShowActionToast("Restored factory presets and rig.", true);
            CloseCreatePresetModal();
            RefreshUi(syncControls: true);
            return;
        }

        string requestedName = presetNameField?.value ?? string.Empty;
        if (presetModalMode == ToneLabPresetModalMode.SaveAs)
        {
            runtime.SaveCurrentAsNewPreset(requestedName);
            ShowActionToast($"Created preset \"{GetPresetName(runtime.CurrentPresetId)}\".");
        }
        else
        {
            runtime.CreatePresetFromCurrent(requestedName);
            ShowActionToast($"Created preset \"{GetPresetName(runtime.CurrentPresetId)}\".");
        }
        CloseCreatePresetModal();
        RefreshUi(syncControls: true);
    }

    private void EnsureSelectedPedal(IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> pedalChain)
    {
        if (pedalChain == null || pedalChain.Count == 0)
        {
            selectedPedalInstanceId = string.Empty;
            return;
        }

        for (int i = 0; i < pedalChain.Count; i++)
        {
            UnityToneLabRuntime.ToneLabPedalSlot slot = pedalChain[i];
            if (slot != null && string.Equals(slot.pedal_instance_id, selectedPedalInstanceId, StringComparison.Ordinal))
                return;
        }

        selectedPedalInstanceId = pedalChain[0]?.pedal_instance_id ?? string.Empty;
    }

    private void RefreshPedalLibrary(IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> pedalChain)
    {
        if (pedalLibraryHost == null)
            return;

        pedalLibraryHost.Clear();

        IReadOnlyList<IToneLabPedalDescriptor> availablePedals = ToneLabPedalRegistry.AllDescriptors;
        for (int i = 0; i < availablePedals.Count; i++)
        {
            IToneLabPedalDescriptor descriptor = availablePedals[i];
            ToneLabPedalLibraryItem libraryItem = new ToneLabPedalLibraryItem(
                descriptor,
                () =>
                {
                    selectedPedalInstanceId = runtime?.AddPedalToChain(descriptor.PedalType) ?? string.Empty;
                    sidePanelMode = ToneLabSidePanelMode.Pedal;
                    RefreshUi(syncControls: true);
                });
            pedalLibraryHost.Add(libraryItem);
        }
    }

    private void RefreshSidePanel(IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> pedalChain)
    {
        if (sidePanelHost == null)
            return;

        UnityToneLabRuntime.ToneLabPedalSlot selectedSlot = null;
        if (pedalChain != null)
        {
            for (int i = 0; i < pedalChain.Count; i++)
            {
                UnityToneLabRuntime.ToneLabPedalSlot slot = pedalChain[i];
                if (slot != null && string.Equals(slot.pedal_instance_id, selectedPedalInstanceId, StringComparison.Ordinal))
                {
                    selectedSlot = slot;
                    break;
                }
            }
        }

        sidePanelHost.Clear();
        switch (sidePanelMode)
        {
            case ToneLabSidePanelMode.Library:
                sidePanelTitleLabel.text = "Pedal Library";
                sidePanelSubtitleLabel.text = "Choose a pedal and add it to the board.";
                sidePanelHost.Add(pedalLibraryScroll);
                break;
            case ToneLabSidePanelMode.Pedal:
                IToneLabPedalDescriptor descriptor = selectedSlot != null ? ToneLabPedalRegistry.GetDescriptor(selectedSlot.pedal_type) : null;
                sidePanelTitleLabel.text = descriptor?.DisplayName ?? "Pedal";
                sidePanelSubtitleLabel.text = descriptor?.Description ?? "Pedal settings";
                RebuildPedalInspector();
                sidePanelHost.Add(pedalInspectorScroll);
                break;
        }
    }

    private void RefreshSidePanelButtonStates()
    {
    }

    private void RebuildPedalInspector()
    {
        if (pedalInspectorHost == null || runtime == null)
            return;

        pedalInspectorHost.Clear();
        UnityToneLabRuntime.ToneLabPedalSlot[] pedalChain = runtime.CurrentPedalChain;
        UnityToneLabRuntime.ToneLabPedalSlot selectedSlot = null;
        for (int i = 0; i < pedalChain.Length; i++)
        {
            UnityToneLabRuntime.ToneLabPedalSlot slot = pedalChain[i];
            if (slot != null && string.Equals(slot.pedal_instance_id, selectedPedalInstanceId, StringComparison.Ordinal))
            {
                selectedSlot = slot;
                break;
            }
        }

        if (selectedSlot == null)
        {
            Label emptyTitleLabel = new Label("No Pedal Selected");
            emptyTitleLabel.style.color = Color.white;
            emptyTitleLabel.style.fontSize = 24f;
            emptyTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            emptyTitleLabel.style.marginBottom = 6f;
            pedalInspectorHost.Add(emptyTitleLabel);

            Label emptySubtitleLabel = new Label("The board is empty. Open the pedal library and add a pedal to start building your rig.");
            emptySubtitleLabel.style.color = new Color(0.63f, 0.66f, 0.72f, 0.94f);
            emptySubtitleLabel.style.fontSize = 14f;
            emptySubtitleLabel.style.whiteSpace = WhiteSpace.Normal;
            emptySubtitleLabel.style.marginBottom = 14f;
            pedalInspectorHost.Add(emptySubtitleLabel);

            Button openLibraryButton = CreateButton("Open Library", "tone-lab-button tone-lab-button-secondary", () =>
            {
                sidePanelMode = ToneLabSidePanelMode.Library;
                RefreshUi(syncControls: true);
            });
            openLibraryButton.style.minWidth = 200f;
            openLibraryButton.style.marginRight = 0f;
            pedalInspectorHost.Add(openLibraryButton);
            return;
        }

        IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(selectedSlot.pedal_type);
        object pedalSettings = descriptor.DeserializeSettingsObject(selectedSlot.settings_json);

        VisualElement infoRow = new VisualElement();
        infoRow.style.flexDirection = FlexDirection.Row;
        infoRow.style.justifyContent = Justify.FlexStart;
        infoRow.style.alignItems = Align.Center;
        infoRow.style.marginBottom = 12f;
        pedalInspectorHost.Add(infoRow);

        Button pedalToggleButton = CreateButton("ON", "tone-lab-button tone-lab-button-secondary", () =>
        {
            if (runtime == null)
                return;

            bool nextEnabled = !selectedSlot.enabled;
            runtime.SetPedalEnabled(selectedSlot.pedal_instance_id, nextEnabled);
            RefreshUi(syncControls: true);
        });
        pedalToggleButton.style.minWidth = 96f;
        pedalToggleButton.style.height = 36f;
        pedalToggleButton.style.marginRight = 10f;
        ApplyInspectorToggleStyle(pedalToggleButton, selectedSlot.enabled);
        infoRow.Add(pedalToggleButton);

        Button removePedalButton = CreateButton("Remove", "tone-lab-button tone-lab-button-danger", () =>
        {
            runtime?.RemovePedalFromChain(selectedSlot.pedal_instance_id);
            sidePanelMode = ToneLabSidePanelMode.Pedal;
            RefreshUi(syncControls: true);
        });
        removePedalButton.style.minWidth = 96f;
        removePedalButton.style.height = 36f;
        removePedalButton.style.marginRight = 0f;
        infoRow.Add(removePedalButton);

        VisualElement divider = new VisualElement();
        divider.style.height = 1f;
        divider.style.backgroundColor = new Color(0.19f, 0.21f, 0.25f, 0.92f);
        divider.style.marginBottom = 10f;
        pedalInspectorHost.Add(divider);

        IReadOnlyList<ToneLabPedalParameterDefinition> parameters = descriptor.Parameters;
        for (int i = 0; i < parameters.Count; i++)
        {
            pedalInspectorHost.Add(CreatePedalParameterSlider(selectedSlot.pedal_instance_id, pedalSettings, parameters[i]));
        }
    }

    private static void ApplyInspectorToggleStyle(Button button, bool enabled)
    {
        button.text = enabled ? "ON" : "OFF";
        button.style.backgroundColor = enabled ? new Color(0.12f, 0.20f, 0.14f, 0.92f) : new Color(0f, 0f, 0f, 0f);
        button.style.color = enabled ? new Color(0.82f, 0.93f, 0.84f, 1f) : new Color(0.88f, 0.90f, 0.94f, 0.96f);
        button.style.borderTopWidth = 1f;
        button.style.borderRightWidth = 1f;
        button.style.borderBottomWidth = 1f;
        button.style.borderLeftWidth = 1f;
        button.style.borderTopColor = enabled ? new Color(0.35f, 0.58f, 0.41f, 1f) : new Color(0.33f, 0.35f, 0.40f, 1f);
        button.style.borderRightColor = enabled ? new Color(0.19f, 0.35f, 0.24f, 1f) : new Color(0.24f, 0.26f, 0.30f, 1f);
        button.style.borderBottomColor = enabled ? new Color(0.17f, 0.28f, 0.20f, 1f) : new Color(0.20f, 0.22f, 0.25f, 1f);
        button.style.borderLeftColor = enabled ? new Color(0.19f, 0.35f, 0.24f, 1f) : new Color(0.24f, 0.26f, 0.30f, 1f);
    }

    private VisualElement CreatePedalParameterSlider(string pedalInstanceId, object settingsObject, ToneLabPedalParameterDefinition parameterDefinition)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("tone-lab-slider-row");

        VisualElement header = new VisualElement();
        header.AddToClassList("tone-lab-slider-header");
        row.Add(header);

        Label titleLabel = new Label(parameterDefinition.DisplayName);
        titleLabel.AddToClassList("tone-lab-slider-title");
        titleLabel.style.color = new Color(0.91f, 0.94f, 0.98f, 0.98f);
        titleLabel.style.fontSize = 15f;
        header.Add(titleLabel);

        Label valueLabel = new Label(parameterDefinition.Formatter(parameterDefinition.GetValue(settingsObject)));
        valueLabel.AddToClassList("tone-lab-slider-value");
        valueLabel.style.color = new Color(1f, 0.79f, 0.56f, 0.98f);
        valueLabel.style.fontSize = 14f;
        valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        header.Add(valueLabel);

        Slider slider = new Slider(parameterDefinition.MinimumValue, parameterDefinition.MaximumValue);
        slider.AddToClassList("tone-lab-slider");
        ApplySliderStyle(slider);
        slider.SetValueWithoutNotify(parameterDefinition.GetValue(settingsObject));
        slider.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks || runtime == null)
                return;

            valueLabel.text = parameterDefinition.Formatter(evt.newValue);
            runtime.UpdatePedalParameter(pedalInstanceId, parameterDefinition.ParameterId, evt.newValue);
        });
        row.Add(slider);
        return row;
    }

    private VisualElement CreateEffectCard(
        string title,
        string subtitle,
        Func<UnityToneLabRuntime.ToneLabSettings, bool> getter,
        Action<UnityToneLabRuntime.ToneLabSettings, bool> setter,
        Color accentColor)
    {
        VisualElement card = CreateCard(title, subtitle);
        card.AddToClassList("tone-lab-effect-card");
        card.style.borderTopColor = accentColor;
        card.style.width = Length.Percent(49f);
        card.style.marginBottom = 16f;

        Button toggleButton = CreateButton("OFF", "tone-lab-toggle", () =>
        {
            if (runtime == null)
                return;

            bool nextValue = !getter(runtime.CurrentSettings);
            runtime.UpdateSettings(settings => setter(settings, nextValue), restartMonitoring: false);
            RefreshUi(syncControls: true);
        });

        ApplyToggleButtonState(toggleButton, getter(runtime.CurrentSettings));
        VisualElement header = card.Q<VisualElement>(className: "tone-lab-card-header");
        header?.Add(toggleButton);
        toggleBindings.Add(new ToneToggleBinding
        {
            toggleButton = toggleButton,
            getter = getter,
            setter = setter
        });

        switch (title)
        {
            case "Distortion":
                card.Add(CreateSliderControl("Drive", 0f, 36f, value => $"{value:F1} dB", settings => settings.dist_drive_db, (settings, value) => settings.dist_drive_db = value));
                break;
            case "Chorus":
                card.Add(CreateSliderControl("Rate", 0.1f, 4f, value => $"{value:F2} Hz", settings => settings.chorus_rate_hz, (settings, value) => settings.chorus_rate_hz = value));
                card.Add(CreateSliderControl("Depth", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.chorus_depth, (settings, value) => settings.chorus_depth = value));
                card.Add(CreateSliderControl("Mix", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.chorus_mix, (settings, value) => settings.chorus_mix = value));
                break;
            case "Phaser":
                card.Add(CreateSliderControl("Rate", 0.1f, 3f, value => $"{value:F2} Hz", settings => settings.phaser_rate_hz, (settings, value) => settings.phaser_rate_hz = value));
                card.Add(CreateSliderControl("Depth", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.phaser_depth, (settings, value) => settings.phaser_depth = value));
                card.Add(CreateSliderControl("Mix", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.phaser_mix, (settings, value) => settings.phaser_mix = value));
                card.Add(CreateSliderControl("Center", 120f, 4200f, value => $"{value:F0} Hz", settings => settings.phaser_center_hz, (settings, value) => settings.phaser_center_hz = value));
                card.Add(CreateSliderControl("Feedback", -0.9f, 0.9f, value => value.ToString("F2"), settings => settings.phaser_feedback, (settings, value) => settings.phaser_feedback = value));
                break;
            case "Delay":
                card.Add(CreateSliderControl("Time", 0.02f, 1.5f, value => $"{value:F2} s", settings => settings.delay_seconds, (settings, value) => settings.delay_seconds = value));
                card.Add(CreateSliderControl("Feedback", 0f, 0.95f, value => $"{value * 100f:F0}%", settings => settings.delay_feedback, (settings, value) => settings.delay_feedback = value));
                card.Add(CreateSliderControl("Mix", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.delay_mix, (settings, value) => settings.delay_mix = value));
                break;
            case "Reverb":
                card.Add(CreateSliderControl("Room Size", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.reverb_room_size, (settings, value) => settings.reverb_room_size = value));
                card.Add(CreateSliderControl("Damping", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.reverb_damping, (settings, value) => settings.reverb_damping = value));
                card.Add(CreateSliderControl("Wet", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.reverb_wet, (settings, value) => settings.reverb_wet = value));
                card.Add(CreateSliderControl("Dry", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.reverb_dry, (settings, value) => settings.reverb_dry = value));
                card.Add(CreateSliderControl("Width", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.reverb_width, (settings, value) => settings.reverb_width = value));
                card.Add(CreateSliderControl("Freeze", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.reverb_freeze, (settings, value) => settings.reverb_freeze = value));
                break;
            case "Compressor":
                card.Add(CreateSliderControl("Threshold", -60f, 0f, value => $"{value:F0} dB", settings => settings.comp_threshold_db, (settings, value) => settings.comp_threshold_db = value));
                card.Add(CreateSliderControl("Ratio", 1f, 8f, value => $"{value:F1}:1", settings => settings.comp_ratio, (settings, value) => settings.comp_ratio = value));
                card.Add(CreateSliderControl("Attack", 1f, 120f, value => $"{value:F0} ms", settings => settings.comp_attack_ms, (settings, value) => settings.comp_attack_ms = value));
                card.Add(CreateSliderControl("Release", 20f, 600f, value => $"{value:F0} ms", settings => settings.comp_release_ms, (settings, value) => settings.comp_release_ms = value));
                break;
        }

        if (title == "Distortion" || title == "Phaser" || title == "Reverb")
            card.style.marginRight = Length.Percent(1f);
        else
            card.style.marginLeft = Length.Percent(1f);

        return card;
    }

    private VisualElement CreateSliderControl(
        string title,
        float min,
        float max,
        Func<float, string> formatter,
        Func<UnityToneLabRuntime.ToneLabSettings, float> getter,
        Action<UnityToneLabRuntime.ToneLabSettings, float> setter,
        bool includeInRefreshBindings = true)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("tone-lab-slider-row");

        VisualElement header = new VisualElement();
        header.AddToClassList("tone-lab-slider-header");
        row.Add(header);

        Label titleLabel = new Label(title);
        titleLabel.AddToClassList("tone-lab-slider-title");
        titleLabel.style.color = new Color(0.91f, 0.94f, 0.98f, 0.98f);
        titleLabel.style.fontSize = 15f;
        header.Add(titleLabel);

        Label valueLabel = new Label(string.Empty);
        valueLabel.AddToClassList("tone-lab-slider-value");
        valueLabel.style.color = new Color(1f, 0.79f, 0.56f, 0.98f);
        valueLabel.style.fontSize = 14f;
        valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        header.Add(valueLabel);

        Slider slider = new Slider(min, max);
        slider.AddToClassList("tone-lab-slider");
        ApplySliderStyle(slider);
        float initialValue = runtime != null ? getter(runtime.CurrentSettings) : min;
        slider.SetValueWithoutNotify(initialValue);
        valueLabel.text = formatter(initialValue);
        slider.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks || runtime == null)
                return;

            valueLabel.text = formatter(evt.newValue);
            runtime.UpdateSettings(settings => setter(settings, evt.newValue), restartMonitoring: false);
        });
        row.Add(slider);

        if (includeInRefreshBindings)
        {
            sliderBindings.Add(new ToneSliderBinding
            {
                slider = slider,
                valueLabel = valueLabel,
                getter = getter,
                setter = setter,
                formatter = formatter
            });
        }

        return row;
    }

    private static VisualElement CreateCard(string title, string subtitle)
    {
        VisualElement card = new VisualElement();
        card.AddToClassList("tone-lab-card");
        card.style.flexGrow = 1f;
        card.style.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 0.72f);
        card.style.borderTopWidth = 1f;
        card.style.borderRightWidth = 1f;
        card.style.borderBottomWidth = 1f;
        card.style.borderLeftWidth = 1f;
        card.style.borderTopColor = new Color(0.23f, 0.24f, 0.29f, 0.98f);
        card.style.borderRightColor = new Color(0.18f, 0.19f, 0.23f, 0.98f);
        card.style.borderBottomColor = new Color(0.15f, 0.16f, 0.19f, 0.98f);
        card.style.borderLeftColor = new Color(0.18f, 0.19f, 0.23f, 0.98f);
        card.style.borderTopLeftRadius = 16f;
        card.style.borderTopRightRadius = 16f;
        card.style.borderBottomLeftRadius = 16f;
        card.style.borderBottomRightRadius = 16f;
        card.style.paddingLeft = 18f;
        card.style.paddingRight = 18f;
        card.style.paddingTop = 16f;
        card.style.paddingBottom = 14f;

        VisualElement header = new VisualElement();
        header.AddToClassList("tone-lab-card-header");
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.FlexStart;
        header.style.marginBottom = 12f;
        card.Add(header);

        VisualElement textColumn = new VisualElement();
        textColumn.AddToClassList("tone-lab-card-copy");
        textColumn.style.flexGrow = 1f;
        textColumn.style.paddingRight = 10f;
        header.Add(textColumn);

        Label titleLabel = new Label(title);
        titleLabel.AddToClassList("tone-lab-card-title");
        titleLabel.style.color = Color.white;
        titleLabel.style.fontSize = 20f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 4f;
        textColumn.Add(titleLabel);

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            Label subtitleLabel = new Label(subtitle);
            subtitleLabel.AddToClassList("tone-lab-card-subtitle");
            subtitleLabel.style.color = new Color(0.59f, 0.62f, 0.67f, 0.94f);
            subtitleLabel.style.fontSize = 13f;
            subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
            textColumn.Add(subtitleLabel);
        }

        return card;
    }

    private static VisualElement CreateSideSectionCard(
        VisualElement parent,
        string title,
        string subtitle,
        out Label titleLabel,
        out Label subtitleLabel,
        out VisualElement contentHost)
    {
        titleLabel = new Label(title);
        subtitleLabel = new Label(subtitle);
        return CreateSideSectionCard(parent, titleLabel, subtitleLabel, out contentHost);
    }

    private static VisualElement CreateSideSectionCard(
        VisualElement parent,
        Label titleLabel,
        Label subtitleLabel,
        out VisualElement contentHost)
    {
        VisualElement section = new VisualElement();
        section.style.flexDirection = FlexDirection.Column;
        section.style.flexGrow = 1f;
        section.style.minHeight = 0f;
        section.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        section.style.borderTopWidth = 1f;
        section.style.borderRightWidth = 1f;
        section.style.borderBottomWidth = 1f;
        section.style.borderLeftWidth = 1f;
        section.style.borderTopColor = new Color(1f, 1f, 1f, 0.92f);
        section.style.borderRightColor = new Color(1f, 1f, 1f, 0.78f);
        section.style.borderBottomColor = new Color(1f, 1f, 1f, 0.66f);
        section.style.borderLeftColor = new Color(1f, 1f, 1f, 0.78f);
        section.style.borderTopLeftRadius = 18f;
        section.style.borderTopRightRadius = 18f;
        section.style.borderBottomLeftRadius = 18f;
        section.style.borderBottomRightRadius = 18f;
        section.style.paddingLeft = 16f;
        section.style.paddingRight = 12f;
        section.style.paddingTop = 16f;
        section.style.paddingBottom = 14f;
        parent.Add(section);

        titleLabel.style.color = Color.white;
        titleLabel.style.fontSize = 24f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 4f;
        titleLabel.style.flexShrink = 0f;
        section.Add(titleLabel);

        subtitleLabel.style.color = new Color(0.60f, 0.63f, 0.68f, 0.95f);
        subtitleLabel.style.fontSize = 13f;
        subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
        subtitleLabel.style.marginBottom = 12f;
        subtitleLabel.style.flexShrink = 0f;
        section.Add(subtitleLabel);

        contentHost = new VisualElement();
        contentHost.style.flexGrow = 1f;
        contentHost.style.minHeight = 0f;
        contentHost.style.overflow = Overflow.Hidden;
        section.Add(contentHost);

        return section;
    }

    private static VisualElement CreateToolbarField(string labelText, VisualElement control, float width)
    {
        VisualElement field = new VisualElement();
        field.style.width = width;
        field.style.minWidth = width;
        field.style.marginRight = 12f;

        Label label = new Label(labelText);
        label.style.color = new Color(0.66f, 0.69f, 0.73f, 0.95f);
        label.style.fontSize = 12f;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginBottom = 4f;
        field.Add(label);

        field.Add(control);
        return field;
    }

    private static VisualElement CreateToolbarReadout(string labelText, VisualElement valueElement, bool flexGrow)
    {
        VisualElement field = new VisualElement();
        field.style.marginLeft = 12f;
        field.style.alignItems = Align.FlexEnd;
        field.style.justifyContent = Justify.Center;
        if (flexGrow)
            field.style.flexGrow = 1f;

        Label label = new Label(labelText);
        label.style.color = new Color(0.66f, 0.69f, 0.73f, 0.95f);
        label.style.fontSize = 12f;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginBottom = 4f;
        field.Add(label);

        field.Add(valueElement);
        return field;
    }

    private VisualElement CreateCompactSliderField(
        string title,
        float min,
        float max,
        Func<float, string> formatter,
        Func<UnityToneLabRuntime.ToneLabSettings, float> getter,
        Action<UnityToneLabRuntime.ToneLabSettings, float> setter,
        float width)
    {
        VisualElement field = new VisualElement();
        field.style.width = width;
        field.style.minWidth = width;
        field.style.marginRight = 12f;

        Label titleLabel = new Label(title);
        titleLabel.style.color = new Color(0.66f, 0.69f, 0.73f, 0.95f);
        titleLabel.style.fontSize = 12f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 4f;
        field.Add(titleLabel);

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        field.Add(row);

        Slider slider = new Slider(min, max);
        ApplySliderStyle(slider);
        slider.style.flexGrow = 1f;
        slider.style.marginTop = 0f;
        slider.style.marginBottom = 0f;
        slider.style.marginRight = 10f;
        float initialValue = runtime != null ? getter(runtime.CurrentSettings) : min;
        slider.SetValueWithoutNotify(initialValue);
        row.Add(slider);

        Label valueLabel = new Label(formatter(initialValue));
        valueLabel.style.color = new Color(0.84f, 0.81f, 0.74f, 0.98f);
        valueLabel.style.fontSize = 13f;
        valueLabel.style.minWidth = 64f;
        valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        row.Add(valueLabel);

        slider.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks || runtime == null)
                return;

            valueLabel.text = formatter(evt.newValue);
            runtime.UpdateSettings(settings => setter(settings, evt.newValue), restartMonitoring: false);
        });

        sliderBindings.Add(new ToneSliderBinding
        {
            slider = slider,
            valueLabel = valueLabel,
            getter = getter,
            setter = setter,
            formatter = formatter
        });

        return field;
    }

    private static VisualElement CreateSettingRow(string labelText, out VisualElement valueHost)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("tone-lab-setting-row");
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.marginBottom = 12f;

        Label label = new Label(labelText);
        label.AddToClassList("tone-lab-setting-label");
        label.style.color = new Color(0.80f, 0.84f, 0.91f, 0.96f);
        label.style.fontSize = 15f;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        row.Add(label);

        valueHost = new VisualElement();
        valueHost.AddToClassList("tone-lab-setting-value-host");
        valueHost.style.minWidth = 280f;
        valueHost.style.alignItems = Align.FlexEnd;
        row.Add(valueHost);
        return row;
    }

    private static Button CreateButton(string text, string classNames, Action onClick)
    {
        Button button = new Button(onClick) { text = text };
        foreach (string className in classNames.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            button.AddToClassList(className);

        button.style.minWidth = 150f;
        button.style.height = 42f;
        button.style.marginRight = 10f;
        button.style.paddingLeft = 16f;
        button.style.paddingRight = 16f;
        button.style.borderTopLeftRadius = 11f;
        button.style.borderTopRightRadius = 11f;
        button.style.borderBottomLeftRadius = 11f;
        button.style.borderBottomRightRadius = 11f;
        button.style.fontSize = 15f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.opacity = 0.96f;
        button.style.scale = new Scale(Vector3.one);

        bool isPrimary = classNames.Contains("tone-lab-button-primary");
        bool isSecondary = classNames.Contains("tone-lab-button-secondary");
        bool isDanger = classNames.Contains("tone-lab-button-danger");
        bool isToggle = classNames.Contains("tone-lab-toggle");
        if (isDanger)
        {
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            button.style.color = new Color(0.95f, 0.60f, 0.60f, 1f);
            button.style.borderTopWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftWidth = 1f;
            button.style.borderTopColor = new Color(0.62f, 0.30f, 0.30f, 1f);
            button.style.borderRightColor = new Color(0.44f, 0.20f, 0.20f, 1f);
            button.style.borderBottomColor = new Color(0.36f, 0.16f, 0.16f, 1f);
            button.style.borderLeftColor = new Color(0.44f, 0.20f, 0.20f, 1f);
        }
        else if (isPrimary)
        {
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            button.style.color = new Color(0.96f, 0.97f, 0.98f, 0.98f);
            button.style.borderTopWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftWidth = 1f;
            button.style.borderTopColor = new Color(0.62f, 0.64f, 0.68f, 0.98f);
            button.style.borderRightColor = new Color(0.38f, 0.40f, 0.44f, 1f);
            button.style.borderBottomColor = new Color(0.29f, 0.30f, 0.34f, 1f);
            button.style.borderLeftColor = new Color(0.38f, 0.40f, 0.44f, 1f);
        }
        else if (isSecondary)
        {
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            button.style.color = new Color(0.84f, 0.86f, 0.90f, 0.98f);
            button.style.borderTopWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftWidth = 1f;
            button.style.borderTopColor = new Color(0.36f, 0.38f, 0.42f, 0.90f);
            button.style.borderRightColor = new Color(0.24f, 0.26f, 0.30f, 0.98f);
            button.style.borderBottomColor = new Color(0.18f, 0.20f, 0.23f, 0.98f);
            button.style.borderLeftColor = new Color(0.24f, 0.26f, 0.30f, 0.98f);
        }
        else if (isToggle)
        {
            button.style.minWidth = 84f;
            button.style.height = 36f;
            button.style.fontSize = 14f;
            button.style.marginRight = 0f;
        }

        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            button.style.scale = new Scale(new Vector3(1.02f, 1.02f, 1f));
            button.style.opacity = 1f;
        });
        button.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            button.style.scale = new Scale(Vector3.one);
            button.style.opacity = 0.96f;
        });

        return button;
    }

    private static void ApplyModalActionButtonStyle(Button button, bool isDanger)
    {
        if (button == null)
            return;

        if (isDanger)
        {
            button.style.color = new Color(0.95f, 0.60f, 0.60f, 1f);
            button.style.borderTopColor = new Color(0.62f, 0.30f, 0.30f, 1f);
            button.style.borderRightColor = new Color(0.44f, 0.20f, 0.20f, 1f);
            button.style.borderBottomColor = new Color(0.36f, 0.16f, 0.16f, 1f);
            button.style.borderLeftColor = new Color(0.44f, 0.20f, 0.20f, 1f);
        }
        else
        {
            button.style.color = new Color(0.96f, 0.97f, 0.98f, 0.98f);
            button.style.borderTopColor = new Color(0.62f, 0.64f, 0.68f, 0.98f);
            button.style.borderRightColor = new Color(0.38f, 0.40f, 0.44f, 1f);
            button.style.borderBottomColor = new Color(0.29f, 0.30f, 0.34f, 1f);
            button.style.borderLeftColor = new Color(0.38f, 0.40f, 0.44f, 1f);
        }
    }

    private static Label CreateLabel(string text, string className, FontDefinition fontDefinition)
    {
        Label label = new Label(text);
        label.AddToClassList(className);
        label.style.unityFontDefinition = fontDefinition;
        return label;
    }

    private static void ApplyToggleButtonState(Button button, bool enabled)
    {
        button.text = enabled ? "ON" : "OFF";
        button.EnableInClassList("tone-lab-toggle-on", enabled);
        button.EnableInClassList("tone-lab-toggle-off", !enabled);
        button.style.backgroundColor = enabled
            ? new Color(0.14f, 0.23f, 0.16f, 0.98f)
            : new Color(0f, 0f, 0f, 0f);
        button.style.color = enabled
            ? new Color(0.83f, 0.92f, 0.85f, 1f)
            : new Color(0.82f, 0.87f, 0.94f, 0.96f);
        button.style.borderTopWidth = 1f;
        button.style.borderRightWidth = 1f;
        button.style.borderBottomWidth = 1f;
        button.style.borderLeftWidth = 1f;
        button.style.borderTopColor = enabled ? new Color(0.35f, 0.58f, 0.41f, 0.98f) : new Color(0.34f, 0.36f, 0.40f, 0.92f);
        button.style.borderRightColor = enabled ? new Color(0.19f, 0.35f, 0.24f, 1f) : new Color(0.23f, 0.25f, 0.29f, 0.98f);
        button.style.borderBottomColor = enabled ? new Color(0.17f, 0.28f, 0.20f, 1f) : new Color(0.18f, 0.20f, 0.23f, 0.98f);
        button.style.borderLeftColor = enabled ? new Color(0.19f, 0.35f, 0.24f, 1f) : new Color(0.23f, 0.25f, 0.29f, 0.98f);
    }

    private static void ApplyModeButtonState(Button button, bool selected)
    {
        if (button == null)
            return;

        button.style.backgroundColor = selected ? new Color(0.10f, 0.11f, 0.13f, 1f) : new Color(0f, 0f, 0f, 0f);
        button.style.color = selected ? Color.white : new Color(0.84f, 0.86f, 0.90f, 0.98f);
        button.style.borderTopColor = selected ? new Color(0.62f, 0.64f, 0.68f, 0.98f) : new Color(0.36f, 0.38f, 0.42f, 0.90f);
        button.style.borderRightColor = selected ? new Color(0.38f, 0.40f, 0.44f, 1f) : new Color(0.24f, 0.26f, 0.30f, 0.98f);
        button.style.borderBottomColor = selected ? new Color(0.29f, 0.30f, 0.34f, 1f) : new Color(0.18f, 0.20f, 0.23f, 0.98f);
        button.style.borderLeftColor = selected ? new Color(0.38f, 0.40f, 0.44f, 1f) : new Color(0.24f, 0.26f, 0.30f, 0.98f);
    }

    private static void ApplyOverlayRootStyle(VisualElement overlay)
    {
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.right = 0f;
        overlay.style.top = 0f;
        overlay.style.bottom = 0f;
        overlay.style.alignItems = Align.Stretch;
        overlay.style.justifyContent = Justify.FlexStart;
    }

    private static void ApplyBackdropStyle(VisualElement backdrop)
    {
        backdrop.style.position = Position.Absolute;
        backdrop.style.left = 0f;
        backdrop.style.right = 0f;
        backdrop.style.top = 0f;
        backdrop.style.bottom = 0f;
        backdrop.style.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 0.52f);
    }

    private static void ApplyWindowStyle(VisualElement window)
    {
        window.style.position = Position.Absolute;
        window.style.left = 0f;
        window.style.right = 0f;
        window.style.top = 0f;
        window.style.bottom = 0f;
        window.style.paddingLeft = 26f;
        window.style.paddingRight = 26f;
        window.style.paddingTop = 22f;
        window.style.paddingBottom = 18f;
        window.style.overflow = Overflow.Hidden;
        window.style.backgroundColor = new Color(0.10f, 0.10f, 0.11f, 0.98f);
        window.style.borderTopWidth = 0f;
        window.style.borderRightWidth = 0f;
        window.style.borderBottomWidth = 0f;
        window.style.borderLeftWidth = 0f;
        window.style.borderTopLeftRadius = 0f;
        window.style.borderTopRightRadius = 0f;
        window.style.borderBottomLeftRadius = 0f;
        window.style.borderBottomRightRadius = 0f;
        window.style.flexDirection = FlexDirection.Column;
    }

    private static void ApplySliderStyle(Slider slider)
    {
        slider.style.height = 24f;
        slider.style.marginTop = 4f;
        slider.style.marginBottom = 10f;

        slider.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            VisualElement dragContainer = slider.Q<VisualElement>(className: "unity-base-slider__drag-container");
            if (dragContainer != null)
            {
                dragContainer.style.height = 10f;
                dragContainer.style.backgroundColor = new Color(0.14f, 0.15f, 0.18f, 0.98f);
                dragContainer.style.borderTopLeftRadius = 5f;
                dragContainer.style.borderTopRightRadius = 5f;
                dragContainer.style.borderBottomLeftRadius = 5f;
                dragContainer.style.borderBottomRightRadius = 5f;
            }

            VisualElement tracker = slider.Q<VisualElement>(className: "unity-base-slider__tracker");
            if (tracker != null)
            {
                tracker.style.backgroundColor = new Color(0.76f, 0.62f, 0.42f, 0.94f);
                tracker.style.borderTopLeftRadius = 5f;
                tracker.style.borderTopRightRadius = 5f;
                tracker.style.borderBottomLeftRadius = 5f;
                tracker.style.borderBottomRightRadius = 5f;
            }

            VisualElement dragger = slider.Q<VisualElement>(className: "unity-base-slider__dragger");
            if (dragger != null)
            {
                dragger.style.width = 16f;
                dragger.style.height = 16f;
                dragger.style.backgroundColor = new Color(0.94f, 0.94f, 0.92f, 1f);
                dragger.style.borderTopLeftRadius = 8f;
                dragger.style.borderTopRightRadius = 8f;
                dragger.style.borderBottomLeftRadius = 8f;
                dragger.style.borderBottomRightRadius = 8f;
                dragger.style.borderTopWidth = 1f;
                dragger.style.borderRightWidth = 1f;
                dragger.style.borderBottomWidth = 1f;
                dragger.style.borderLeftWidth = 1f;
                dragger.style.borderTopColor = new Color(0.90f, 0.88f, 0.84f, 1f);
                dragger.style.borderRightColor = new Color(0.50f, 0.46f, 0.40f, 1f);
                dragger.style.borderBottomColor = new Color(0.40f, 0.36f, 0.31f, 1f);
                dragger.style.borderLeftColor = new Color(0.50f, 0.46f, 0.40f, 1f);
            }
        });
    }

    private static void ApplyDropdownStyle(DropdownField dropdown)
    {
        dropdown.style.minWidth = 280f;
        dropdown.style.minHeight = 40f;
        dropdown.style.fontSize = 15f;
        dropdown.style.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.98f);
        dropdown.style.color = new Color(0.90f, 0.91f, 0.93f, 0.98f);
        dropdown.style.borderTopWidth = 1f;
        dropdown.style.borderRightWidth = 1f;
        dropdown.style.borderBottomWidth = 1f;
        dropdown.style.borderLeftWidth = 1f;
        dropdown.style.borderTopColor = new Color(0.33f, 0.35f, 0.39f, 0.90f);
        dropdown.style.borderRightColor = new Color(0.23f, 0.24f, 0.28f, 0.98f);
        dropdown.style.borderBottomColor = new Color(0.18f, 0.19f, 0.22f, 0.98f);
        dropdown.style.borderLeftColor = new Color(0.23f, 0.24f, 0.28f, 0.98f);
        dropdown.style.borderTopLeftRadius = 10f;
        dropdown.style.borderTopRightRadius = 10f;
        dropdown.style.borderBottomLeftRadius = 10f;
        dropdown.style.borderBottomRightRadius = 10f;
        dropdown.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            VisualElement inputElement = dropdown.Q(className: "unity-base-field__input");
            if (inputElement != null)
            {
                inputElement.style.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
                inputElement.style.color = new Color(0.90f, 0.91f, 0.93f, 1f);
            }

            Label textLabel = dropdown.Q<Label>(className: "unity-base-popup-field__text");
            if (textLabel != null)
                textLabel.style.color = new Color(0.90f, 0.91f, 0.93f, 1f);

            VisualElement arrowElement = dropdown.Q(className: "unity-base-popup-field__arrow");
            if (arrowElement != null)
                arrowElement.style.unityBackgroundImageTintColor = new Color(0.82f, 0.84f, 0.88f, 1f);
        });
    }

    private static int ParseLatencyPresetBufferSize(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return 128;
        if (label.IndexOf("64", StringComparison.Ordinal) >= 0)
            return 64;
        if (label.IndexOf("256", StringComparison.Ordinal) >= 0)
            return 256;
        return 128;
    }

    private string GetPresetName(string presetId)
    {
        if (runtime == null || string.IsNullOrWhiteSpace(presetId))
            return "Preset";

        UnityToneLabRuntime.ToneLabPreset[] presets = runtime.CurrentPresets;
        for (int i = 0; i < presets.Length; i++)
        {
            UnityToneLabRuntime.ToneLabPreset preset = presets[i];
            if (preset != null && string.Equals(preset.preset_id, presetId, StringComparison.Ordinal))
                return string.IsNullOrWhiteSpace(preset.preset_name) ? "Preset" : preset.preset_name.Trim();
        }

        return "Preset";
    }

    private void ShowActionToast(string message, bool danger = false)
    {
        if (actionToast == null || actionToastLabel == null || string.IsNullOrWhiteSpace(message))
            return;

        actionToastLabel.text = message;
        actionToast.style.backgroundColor = danger
            ? new Color(0.26f, 0.11f, 0.12f, 0.98f)
            : new Color(0.15f, 0.18f, 0.20f, 0.98f);
        actionToast.style.borderTopColor = danger ? new Color(0.58f, 0.26f, 0.28f, 0.95f) : new Color(0.34f, 0.37f, 0.42f, 0.95f);
        actionToast.style.borderRightColor = danger ? new Color(0.42f, 0.18f, 0.20f, 0.98f) : new Color(0.24f, 0.26f, 0.30f, 0.98f);
        actionToast.style.borderBottomColor = danger ? new Color(0.34f, 0.15f, 0.16f, 0.98f) : new Color(0.18f, 0.20f, 0.23f, 0.98f);
        actionToast.style.borderLeftColor = danger ? new Color(0.42f, 0.18f, 0.20f, 0.98f) : new Color(0.24f, 0.26f, 0.30f, 0.98f);
        actionToast.style.display = DisplayStyle.Flex;

        CancelInvoke(nameof(HideActionToast));
        Invoke(nameof(HideActionToast), 1.8f);
    }

    private void HideActionToast()
    {
        if (actionToast != null)
            actionToast.style.display = DisplayStyle.None;
    }

    private void ApplyPresetNameFieldStyle()
    {
        if (presetNameField == null)
            return;

        presetNameField.style.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
        presetNameField.style.color = Color.white;

        VisualElement inputElement = presetNameField.Q(className: TextInputBaseField<string>.textInputUssName);
        if (inputElement != null)
        {
            inputElement.style.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
            inputElement.style.color = Color.white;
            inputElement.style.unityTextAlign = TextAnchor.MiddleLeft;
        }

        VisualElement baseInputElement = presetNameField.Q(className: TextInputBaseField<string>.inputUssClassName);
        if (baseInputElement != null)
        {
            baseInputElement.style.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
            baseInputElement.style.color = Color.white;
        }

        foreach (UnityEngine.UIElements.TextElement textElement in presetNameField.Query<UnityEngine.UIElements.TextElement>().ToList())
        {
            textElement.style.color = Color.white;
            textElement.style.unityTextAlign = TextAnchor.MiddleLeft;
            textElement.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }
    }

    private static PanelSettings ResolvePanelSettings()
    {
        PanelSettings runtimeAsset = Resources.Load<PanelSettings>("UIToolkitRuntimePanelSettings");
        PanelSettings settings = runtimeAsset != null
            ? ScriptableObject.Instantiate(runtimeAsset)
            : ScriptableObject.CreateInstance<PanelSettings>();
        settings.name = "UnityToneLabRuntimePanelSettings";
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = new Vector2Int(1920, 1080);
        settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        settings.match = 0.5f;
        settings.scale = 1f;
        settings.targetDisplay = 0;
        settings.sortingOrder = 240;

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
