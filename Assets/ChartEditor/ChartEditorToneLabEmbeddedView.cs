using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

public sealed class ChartEditorToneLabEmbeddedView
{
    private const float EmbeddedPedalScale = 1.26f;
    private const float SidebarWidth = 440f;

    public enum SidePanelMode
    {
        Presets,
        Library,
        Details
    }

    public sealed class Options
    {
        public UnityToneLabRuntime Runtime;
        public string SelectedPedalInstanceId;
        public SidePanelMode SidePanel;
        public Action<string> SetSelectedPedalInstanceId;
        public Action<SidePanelMode> SetSidePanel;
        public Action Focus;
        public Action MarkWorkingToneChanged;
        public Action<string> MarkPresetSelected;
        public Func<bool> IsWorkingToneCustom;
        public Action RequestRebuild;
        public Action AssignToSelectedToneSwitch;
        public Action AddToneSwitchAtCursor;
        public Action<TextField> RegisterTextFieldKeyboardCapture;
        public Action<string> SetStatus;
        public FontDefinition BodyFont;
        public FontDefinition TitleFont;
        public float FontScale = 1f;
    }

    private readonly Options options;
    private readonly UnityToneLabRuntime runtime;
    private readonly VisualElement root;
    private readonly VisualElement sidePanelHost;
    private readonly ToneLabPedalBoardView pedalBoardView;
    private TextField presetSearchField;
    private TextField librarySearchField;
    private string presetSearchQuery = string.Empty;
    private string librarySearchQuery = string.Empty;

    public VisualElement Root => root;

    public ChartEditorToneLabEmbeddedView(Options options)
    {
        this.options = options ?? new Options();
        runtime = this.options.Runtime;
        root = BuildRoot();
        if (runtime == null)
        {
            root.Add(BuildUnavailableState());
            return;
        }

        EnsureSelectedPedal();
        sidePanelHost = new VisualElement();
        pedalBoardView = new ToneLabPedalBoardView(
            EmbeddedPedalScale,
            singleRowBoard: true,
            fontDefinition: this.options.BodyFont,
            textScale: this.options.FontScale);
        BuildLayout();
        RefreshBoard();
        RefreshSidePanel();
    }

    private VisualElement BuildRoot()
    {
        VisualElement element = new VisualElement();
        ApplyBodyFont(element);
        element.style.flexGrow = 1f;
        element.style.minHeight = 0f;
        element.style.flexDirection = FlexDirection.Column;
        element.style.paddingLeft = 22f;
        element.style.paddingRight = 22f;
        element.style.paddingTop = 20f;
        element.style.paddingBottom = 18f;
        element.style.backgroundColor = new Color(0.018f, 0.022f, 0.030f, 0.995f);
        element.style.borderTopWidth = 1f;
        element.style.borderRightWidth = 1f;
        element.style.borderBottomWidth = 1f;
        element.style.borderLeftWidth = 1f;
        element.style.borderTopColor = new Color(0.18f, 0.22f, 0.28f, 0.90f);
        element.style.borderRightColor = new Color(0.10f, 0.13f, 0.18f, 0.95f);
        element.style.borderBottomColor = new Color(0.05f, 0.07f, 0.10f, 1f);
        element.style.borderLeftColor = new Color(0.10f, 0.13f, 0.18f, 0.95f);
        element.style.borderTopLeftRadius = 14f;
        element.style.borderTopRightRadius = 14f;
        element.style.borderBottomLeftRadius = 14f;
        element.style.borderBottomRightRadius = 14f;
        element.RegisterCallback<PointerDownEvent>(_ => options.Focus?.Invoke());
        return element;
    }

    private VisualElement BuildUnavailableState()
    {
        Label label = CreateLabel("Tone Lab unavailable", 28f, new Color(0.82f, 0.88f, 0.92f, 1f), true);
        label.style.flexGrow = 1f;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        return label;
    }

    private void BuildLayout()
    {
        VisualElement main = new VisualElement();
        main.style.flexGrow = 1f;
        main.style.minHeight = 0f;
        main.style.flexDirection = FlexDirection.Row;
        main.style.overflow = Overflow.Hidden;
        root.Add(main);

        VisualElement sidebar = new VisualElement();
        sidebar.style.width = SidebarWidth;
        sidebar.style.minWidth = SidebarWidth;
        sidebar.style.maxWidth = SidebarWidth;
        sidebar.style.flexShrink = 0f;
        sidebar.style.marginRight = 28f;
        sidebar.style.flexDirection = FlexDirection.Column;
        sidebar.style.minHeight = 0f;
        main.Add(sidebar);

        Label title = CreateLabel("Tone Lab", 42f, Color.white, true, useTitleFont: true);
        title.style.marginBottom = 16f;
        sidebar.Add(title);

        VisualElement tabs = new VisualElement();
        tabs.style.height = 56f;
        tabs.style.flexShrink = 0f;
        tabs.style.flexDirection = FlexDirection.Row;
        tabs.style.marginBottom = 18f;
        sidebar.Add(tabs);
        tabs.Add(CreateTabButton("PRESETS", SidePanelMode.Presets, StartupLogoColor(1)));
        tabs.Add(CreateTabButton("LIBRARY", SidePanelMode.Library, StartupLogoColor(2)));
        tabs.Add(CreateTabButton("DETAILS", SidePanelMode.Details, StartupLogoColor(4)));

        sidePanelHost.style.flexGrow = 1f;
        sidePanelHost.style.minHeight = 0f;
        sidePanelHost.style.overflow = Overflow.Hidden;
        sidebar.Add(sidePanelHost);

        VisualElement right = new VisualElement();
        right.style.flexGrow = 1f;
        right.style.minWidth = 0f;
        right.style.minHeight = 0f;
        right.style.flexDirection = FlexDirection.Column;
        main.Add(right);

        VisualElement actionBar = new VisualElement();
        actionBar.style.height = 88f;
        actionBar.style.flexShrink = 0f;
        actionBar.style.flexDirection = FlexDirection.Row;
        actionBar.style.alignItems = Align.Center;
        actionBar.style.justifyContent = Justify.FlexEnd;
        actionBar.style.borderBottomWidth = 1f;
        actionBar.style.borderBottomColor = new Color(1f, 1f, 1f, 0.14f);
        actionBar.style.marginBottom = 16f;
        right.Add(actionBar);

        VisualElement actionCopy = new VisualElement();
        actionCopy.style.flexGrow = 1f;
        actionCopy.style.minWidth = 0f;
        actionCopy.style.display = DisplayStyle.None;
        actionBar.Add(actionCopy);
        Label actionTitle = CreateLabel("Chart Tone Assignment", 26f, Color.white, true);
        actionCopy.Add(actionTitle);
        Label actionSub = CreateLabel("Build or select a tone here, then assign it to a switch.", 17f, new Color(0.86f, 0.88f, 0.92f, 0.72f), false);
        actionCopy.Add(actionSub);

        VisualElement actions = new VisualElement();
        actions.style.flexDirection = FlexDirection.Row;
        actions.style.alignItems = Align.Center;
        actions.style.flexShrink = 0f;
        actionBar.Add(actions);
        actions.Add(CreateActionButton("Assign To Selected", () =>
        {
            options.Focus?.Invoke();
            options.AssignToSelectedToneSwitch?.Invoke();
        }, primary: true, 300f));
        actions.Add(CreateActionButton("Add Switch Here", () =>
        {
            options.Focus?.Invoke();
            options.AddToneSwitchAtCursor?.Invoke();
        }, primary: false, 250f));

        VisualElement boardSection = new VisualElement();
        boardSection.style.flexGrow = 1f;
        boardSection.style.minHeight = 0f;
        boardSection.style.flexDirection = FlexDirection.Column;
        right.Add(boardSection);

        VisualElement headerControls = pedalBoardView.HeaderControls;
        headerControls.Add(CreateGainSectionLabel("Preset", 82f));
        headerControls.Add(CreateGainSlider(
            "Input Gain",
            -36f,
            36f,
            value => $"{value:F1} dB",
            settings => settings.input_gain_db,
            (settings, value) => settings.input_gain_db = value,
            208f));
        VisualElement outputGain = CreateGainSlider(
            "Output Gain",
            -36f,
            36f,
            value => $"{value:F1} dB",
            settings => settings.output_gain_db,
            (settings, value) => settings.output_gain_db = value,
            208f);
        outputGain.style.marginRight = 0f;
        headerControls.Add(outputGain);

        pedalBoardView.AddPedalRequested += () =>
        {
            SetSidePanel(SidePanelMode.Library);
            Rebuild();
        };
        pedalBoardView.PedalSelected += pedalId =>
        {
            SetSelectedPedal(pedalId);
            SetSidePanel(SidePanelMode.Details);
            Rebuild();
        };
        pedalBoardView.PedalEnabledChanged += (pedalId, enabled) =>
        {
            options.Focus?.Invoke();
            runtime.SetPedalEnabled(pedalId, enabled);
            SetSelectedPedal(pedalId);
            MarkToneChanged();
            SetSidePanel(SidePanelMode.Details);
            Rebuild();
        };
        pedalBoardView.PedalRemoveRequested += pedalId =>
        {
            options.Focus?.Invoke();
            runtime.RemovePedalFromChain(pedalId);
            SetSelectedPedal(runtime.CurrentPedalChain.FirstOrDefault()?.pedal_instance_id ?? string.Empty);
            MarkToneChanged();
            Rebuild();
        };
        pedalBoardView.PedalOrderCommitted += orderedIds =>
        {
            options.Focus?.Invoke();
            runtime.SetPedalChainOrder(orderedIds);
            MarkToneChanged();
            Rebuild();
        };

        VisualElement boardRoot = pedalBoardView.Root;
        boardRoot.style.flexGrow = 1f;
        boardRoot.style.minHeight = 390f;
        boardSection.Add(boardRoot);
    }

    private void RefreshBoard()
    {
        pedalBoardView?.Refresh(runtime.CurrentPedalChain, options.SelectedPedalInstanceId);
    }

    private void RefreshSidePanel()
    {
        if (sidePanelHost == null)
            return;

        sidePanelHost.Clear();
        switch (options.SidePanel)
        {
            case SidePanelMode.Library:
                BuildLibraryPanel(sidePanelHost);
                break;
            case SidePanelMode.Details:
                BuildDetailsPanel(sidePanelHost);
                break;
            default:
                BuildPresetsPanel(sidePanelHost);
                break;
        }
    }

    private void BuildPresetsPanel(VisualElement host)
    {
        host.Add(CreateSidebarHeader("Presets", string.Empty));
        ScrollView scroll = CreateScrollView();
        VisualElement content = scroll.contentContainer;
        presetSearchField = CreateSearchField("Search...",presetSearchQuery, value =>
        {
            presetSearchQuery = value ?? string.Empty;
            PopulatePresetList(content);
        });
        host.Add(presetSearchField);
        host.Add(scroll);
        PopulatePresetList(content);
    }

    private void PopulatePresetList(VisualElement content)
    {
        if (content == null)
            return;

        content.Clear();
        IReadOnlyList<UnityToneLabRuntime.ToneLabPreset> presets = runtime.CurrentPresets;
        bool workingToneIsCustom = options.IsWorkingToneCustom?.Invoke() ?? false;
        string selectedPresetId = workingToneIsCustom ? string.Empty : runtime.CurrentPresetId ?? string.Empty;
        if (workingToneIsCustom)
            content.Add(CreateCustomToneRow());

        string query = NormalizeSearch(presetSearchQuery);
        int visible = 0;
        for (int i = 0; i < presets.Count; i++)
        {
            UnityToneLabRuntime.ToneLabPreset preset = presets[i];
            if (preset == null || string.IsNullOrWhiteSpace(preset.preset_id))
                continue;

            string name = string.IsNullOrWhiteSpace(preset.preset_name) ? $"Preset {i + 1}" : preset.preset_name.Trim();
            if (!MatchesSearch(name, query))
                continue;

            bool selected = string.Equals(preset.preset_id, selectedPresetId, StringComparison.Ordinal);
            content.Add(CreatePresetRow(preset, i, selected));
            visible++;
        }

        if (visible == 0)
            content.Add(CreateEmptyLabel("No matching presets."));
    }

    private void BuildLibraryPanel(VisualElement host)
    {
        host.Add(CreateSidebarHeader("Library", string.Empty));
        ScrollView scroll = CreateScrollView();
        VisualElement content = scroll.contentContainer;
        librarySearchField = CreateSearchField("Search...",librarySearchQuery, value =>
        {
            librarySearchQuery = value ?? string.Empty;
            PopulateLibraryList(content);
        });
        host.Add(librarySearchField);
        host.Add(scroll);
        PopulateLibraryList(content);
    }

    private void PopulateLibraryList(VisualElement content)
    {
        if (content == null)
            return;

        content.Clear();
        string query = NormalizeSearch(librarySearchQuery);
        int visible = 0;
        IReadOnlyList<IToneLabPedalDescriptor> descriptors = ToneLabPedalRegistry.AllDescriptors;
        for (int i = 0; i < descriptors.Count; i++)
        {
            IToneLabPedalDescriptor descriptor = descriptors[i];
            if (descriptor == null || !MatchesLibrarySearch(descriptor, query))
                continue;

            Action addAction = () =>
            {
                options.Focus?.Invoke();
                string pedalId = runtime.AddPedalToChain(descriptor.DescriptorId);
                SetSelectedPedal(pedalId);
                MarkToneChanged();
                SetSidePanel(SidePanelMode.Details);
                Rebuild();
            };
            content.Add(new ToneLabPedalLibraryItem(
                descriptor,
                addAction,
                1.18f,
                options.BodyFont,
                options.FontScale));
            visible++;
        }

        if (visible == 0)
            content.Add(CreateEmptyLabel("No matching effects."));
    }

    private void BuildDetailsPanel(VisualElement host)
    {
        UnityToneLabRuntime.ToneLabPedalSlot selectedSlot = FindSelectedSlot();
        IToneLabPedalDescriptor descriptor = selectedSlot != null ? ToneLabPedalRegistry.GetDescriptor(selectedSlot) : null;
        host.Add(CreateSidebarHeader(descriptor?.DisplayName ?? "Details", descriptor?.Description ?? "Pedal settings"));

        ScrollView scroll = CreateScrollView();
        host.Add(scroll);
        VisualElement content = scroll.contentContainer;
        if (selectedSlot == null || descriptor == null)
        {
            content.Add(CreateEmptyLabel("Select a pedal on the board to edit it."));
            Button openLibrary = CreateActionButton("Open Library", () =>
            {
                SetSidePanel(SidePanelMode.Library);
                Rebuild();
            }, primary: false, 190f);
            openLibrary.style.marginTop = 16f;
            content.Add(openLibrary);
            return;
        }

        VisualElement infoRow = new VisualElement();
        infoRow.style.flexDirection = FlexDirection.Row;
        infoRow.style.alignItems = Align.Center;
        infoRow.style.marginBottom = 14f;
        content.Add(infoRow);

        Button toggle = CreateActionButton(selectedSlot.enabled ? "ON" : "OFF", () =>
        {
            options.Focus?.Invoke();
            runtime.SetPedalEnabled(selectedSlot.pedal_instance_id, !selectedSlot.enabled);
            MarkToneChanged();
            Rebuild();
        }, primary: selectedSlot.enabled, 118f);
        infoRow.Add(toggle);

        Button remove = CreateDangerButton("Remove", () =>
        {
            options.Focus?.Invoke();
            runtime.RemovePedalFromChain(selectedSlot.pedal_instance_id);
            SetSelectedPedal(runtime.CurrentPedalChain.FirstOrDefault()?.pedal_instance_id ?? string.Empty);
            MarkToneChanged();
            Rebuild();
        }, 138f);
        infoRow.Add(remove);

        VisualElement divider = CreateDivider();
        content.Add(divider);

        object settingsObject = descriptor.DeserializeSettingsObject(selectedSlot.settings_json);
        IReadOnlyList<ToneLabPedalParameterDefinition> parameters = descriptor.Parameters ?? Array.Empty<ToneLabPedalParameterDefinition>();
        if (parameters.Count == 0)
        {
            content.Add(CreateEmptyLabel("This pedal has no editable parameters."));
            return;
        }

        for (int i = 0; i < parameters.Count; i++)
            content.Add(CreatePedalParameterSlider(selectedSlot.pedal_instance_id, settingsObject, parameters[i]));
    }

    private VisualElement CreateCustomToneRow()
    {
        VisualElement row = new VisualElement();
        row.style.minHeight = 86f;
        row.style.marginBottom = 12f;
        row.style.paddingLeft = 18f;
        row.style.paddingRight = 14f;
        row.style.paddingTop = 12f;
        row.style.paddingBottom = 12f;
        row.style.justifyContent = Justify.Center;
        row.style.backgroundColor = new Color(0.136f, 0.084f, 0.264f, 1f);
        row.style.borderTopWidth = 2f;
        row.style.borderRightWidth = 2f;
        row.style.borderBottomWidth = 2f;
        row.style.borderLeftWidth = 2f;
        row.style.borderTopColor = new Color(0.62f, 0.38f, 1f, 0.60f);
        row.style.borderRightColor = new Color(0.62f, 0.38f, 1f, 0.60f);
        row.style.borderBottomColor = new Color(0.62f, 0.38f, 1f, 0.60f);
        row.style.borderLeftColor = new Color(0.62f, 0.38f, 1f, 0.60f);
        row.style.borderTopLeftRadius = 12f;
        row.style.borderTopRightRadius = 12f;
        row.style.borderBottomLeftRadius = 12f;
        row.style.borderBottomRightRadius = 12f;

        Label title = CreateLabel("Custom Tone", 22f, Color.white, true);
        row.Add(title);
        Label detail = CreateLabel("Unsaved chain — Assign To Selected stores it on the switch.", 16f, new Color(0.84f, 0.80f, 0.96f, 0.92f), false);
        detail.style.whiteSpace = WhiteSpace.Normal;
        detail.style.marginTop = 4f;
        row.Add(detail);
        return row;
    }

    private Button CreatePresetRow(UnityToneLabRuntime.ToneLabPreset preset, int index, bool selected)
    {
        Button row = new Button(() =>
        {
            if (preset == null)
                return;

            options.Focus?.Invoke();
            runtime.SelectPreset(preset.preset_id);
            SetSelectedPedal(runtime.CurrentPedalChain.FirstOrDefault()?.pedal_instance_id ?? string.Empty);
            options.MarkPresetSelected?.Invoke(preset.preset_id ?? string.Empty);
            Rebuild();
        });
        ApplyBodyFont(row);
        row.text = string.Empty;
        row.style.height = 72f;
        row.style.minHeight = 72f;
        row.style.marginBottom = 4f;
        row.style.paddingLeft = 0f;
        row.style.paddingRight = 0f;
        row.style.paddingTop = 0f;
        row.style.paddingBottom = 0f;
        row.style.borderTopWidth = 0f;
        row.style.borderRightWidth = 0f;
        row.style.borderBottomWidth = selected ? 0f : 1f;
        row.style.borderLeftWidth = 0f;
        row.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);
        row.style.backgroundColor = selected ? new Color(1f, 0.58f, 0.08f, 0.92f) : new Color(0f, 0f, 0f, 0f);
        row.style.borderTopLeftRadius = 0f;
        row.style.borderTopRightRadius = 0f;
        row.style.borderBottomLeftRadius = 0f;
        row.style.borderBottomRightRadius = 0f;

        VisualElement content = new VisualElement();
        content.style.flexGrow = 1f;
        content.style.height = Length.Percent(100f);
        content.style.flexDirection = FlexDirection.Row;
        content.style.alignItems = Align.Center;
        content.style.paddingLeft = selected ? 22f : 14f;
        content.style.paddingRight = 12f;
        row.Add(content);

        VisualElement accent = new VisualElement();
        accent.style.width = 6f;
        accent.style.height = 46f;
        accent.style.marginRight = 16f;
        accent.style.backgroundColor = GetPresetAccentColor(preset);
        content.Add(accent);

        Label name = new Label(string.IsNullOrWhiteSpace(preset?.preset_name) ? $"Preset {index + 1}" : preset.preset_name.Trim());
        name.style.flexGrow = 1f;
        name.style.color = selected ? Color.white : new Color(0.96f, 0.97f, 0.99f, 0.98f);
        name.style.fontSize = UiFont(selected ? 29f : 24f);
        ApplyBodyFont(name);
        name.style.unityFontStyleAndWeight = FontStyle.Bold;
        name.style.unityTextAlign = TextAnchor.MiddleLeft;
        name.style.whiteSpace = WhiteSpace.NoWrap;
        content.Add(name);

        row.RegisterCallback<MouseEnterEvent>(_ =>
        {
            if (selected)
                return;
            row.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
            name.style.color = Color.white;
        });
        row.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            if (selected)
                return;
            row.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            name.style.color = new Color(0.96f, 0.97f, 0.99f, 0.98f);
        });
        return row;
    }

    private VisualElement CreatePedalParameterSlider(
        string pedalInstanceId,
        object settingsObject,
        ToneLabPedalParameterDefinition parameter)
    {
        VisualElement row = new VisualElement();
        row.style.marginBottom = 24f;

        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 9f;
        row.Add(header);

        Label title = new Label(parameter.DisplayName);
        title.style.flexGrow = 1f;
        title.style.color = new Color(0.91f, 0.94f, 0.98f, 0.98f);
        title.style.fontSize = UiFont(21f);
        ApplyBodyFont(title);
        header.Add(title);

        Label value = new Label(parameter.Formatter(parameter.GetValue(settingsObject)));
        value.style.color = new Color(1f, 0.79f, 0.56f, 0.98f);
        value.style.fontSize = UiFont(20f);
        ApplyBodyFont(value);
        value.style.unityTextAlign = TextAnchor.MiddleRight;
        header.Add(value);

        Slider slider = new Slider(parameter.MinimumValue, parameter.MaximumValue);
        ApplySliderStyle(slider);
        slider.SetValueWithoutNotify(parameter.GetValue(settingsObject));
        slider.RegisterValueChangedCallback(evt =>
        {
            options.Focus?.Invoke();
            value.text = parameter.Formatter(evt.newValue);
            runtime.UpdatePedalParameter(pedalInstanceId, parameter.ParameterId, evt.newValue);
            MarkToneChanged();
        });
        row.Add(slider);
        return row;
    }

    private VisualElement CreateGainSlider(
        string label,
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
        field.style.marginRight = 18f;
        field.style.flexDirection = FlexDirection.Column;

        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.marginBottom = 4f;
        field.Add(header);

        Label name = new Label(label);
        name.style.flexGrow = 1f;
        name.style.flexShrink = 1f;
        name.style.color = new Color(0.72f, 0.76f, 0.82f, 0.90f);
        name.style.fontSize = UiFont(15f);
        ApplyBodyFont(name);
        name.style.unityFontStyleAndWeight = FontStyle.Bold;
        name.style.whiteSpace = WhiteSpace.NoWrap;
        header.Add(name);

        float current = getter(runtime.CurrentSettings);
        Label value = new Label(formatter(current));
        value.style.color = new Color(1f, 0.79f, 0.56f, 0.98f);
        value.style.fontSize = UiFont(15f);
        value.style.width = 88f;
        value.style.minWidth = 88f;
        ApplyBodyFont(value);
        value.style.unityTextAlign = TextAnchor.MiddleRight;
        header.Add(value);

        Slider slider = new Slider(min, max);
        ApplySliderStyle(slider);
        slider.SetValueWithoutNotify(current);
        slider.RegisterValueChangedCallback(evt =>
        {
            options.Focus?.Invoke();
            value.text = formatter(evt.newValue);
            runtime.UpdateSettings(settings => setter(settings, evt.newValue), restartMonitoring: false);
            MarkToneChanged();
        });
        field.Add(slider);
        return field;
    }

    private Label CreateGainSectionLabel(string text, float width)
    {
        Label label = new Label(text);
        label.style.width = width;
        label.style.minWidth = width;
        label.style.color = new Color(0.98f, 0.62f, 0.42f, 1f);
        label.style.fontSize = UiFont(18f);
        ApplyBodyFont(label);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        return label;
    }

    private Button CreateTabButton(string text, SidePanelMode mode, Color accent)
    {
        bool selected = options.SidePanel == mode;
        Button button = new Button(() =>
        {
            SetSidePanel(mode);
            Rebuild();
        })
        {
            text = text
        };
        button.style.flexGrow = 1f;
        button.style.height = 54f;
        button.style.marginRight = 8f;
        button.style.fontSize = UiFont(18f);
        ApplyBodyFont(button);
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.color = selected ? Color.white : new Color(0.82f, 0.84f, 0.88f, 0.92f);
        button.style.backgroundColor = selected ? new Color(accent.r, accent.g, accent.b, 0.20f) : new Color(0f, 0f, 0f, 0f);
        button.style.borderTopWidth = 1f;
        button.style.borderRightWidth = 1f;
        button.style.borderBottomWidth = 1f;
        button.style.borderLeftWidth = 1f;
        button.style.borderTopColor = selected ? accent : new Color(1f, 1f, 1f, 0.16f);
        button.style.borderRightColor = selected ? new Color(accent.r, accent.g, accent.b, 0.46f) : new Color(1f, 1f, 1f, 0.08f);
        button.style.borderBottomColor = selected ? new Color(accent.r, accent.g, accent.b, 0.32f) : new Color(1f, 1f, 1f, 0.06f);
        button.style.borderLeftColor = selected ? new Color(accent.r, accent.g, accent.b, 0.46f) : new Color(1f, 1f, 1f, 0.08f);
        button.style.borderTopLeftRadius = 8f;
        button.style.borderTopRightRadius = 8f;
        button.style.borderBottomLeftRadius = 8f;
        button.style.borderBottomRightRadius = 8f;
        return button;
    }

    private VisualElement CreateSidebarHeader(string title, string subtitle)
    {
        VisualElement header = new VisualElement();
        header.style.flexShrink = 0f;
        header.style.marginBottom = 16f;

        Label titleLabel = CreateLabel(title, 30f, Color.white, true);
        titleLabel.style.whiteSpace = WhiteSpace.Normal;
        header.Add(titleLabel);

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            Label subtitleLabel = CreateLabel(subtitle, 17f, new Color(0.70f, 0.73f, 0.78f, 0.88f), false);
            subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
            subtitleLabel.style.marginTop = 3f;
            header.Add(subtitleLabel);
        }

        return header;
    }

    private TextField CreateSearchField(string placeholder, string currentValue, Action<string> changed)
    {
        TextField field = new TextField();
        field.SetValueWithoutNotify(currentValue ?? string.Empty);
        field.label = string.Empty;
        field.tooltip = placeholder ?? string.Empty;
        field.style.height = 58f;
        field.style.marginBottom = 18f;
        field.style.paddingLeft = 0f;
        field.style.paddingRight = 0f;
        field.style.borderBottomWidth = 1f;
        field.style.borderBottomColor = new Color(1f, 1f, 1f, 0.30f);
        field.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        field.style.color = Color.white;
        field.style.fontSize = UiFont(22f);
        ApplyBodyFont(field);
        options.RegisterTextFieldKeyboardCapture?.Invoke(field);

        Label placeholderLabel = new Label(string.IsNullOrWhiteSpace(placeholder) ? "Search..." : placeholder);
        placeholderLabel.pickingMode = PickingMode.Ignore;
        placeholderLabel.style.position = Position.Absolute;
        placeholderLabel.style.left = 4f;
        placeholderLabel.style.top = 0f;
        placeholderLabel.style.bottom = 0f;
        placeholderLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        placeholderLabel.style.color = new Color(0.60f, 0.58f, 0.66f, 1f);
        placeholderLabel.style.fontSize = UiFont(22f);
        placeholderLabel.style.unityFontDefinition = options.BodyFont;
        field.Add(placeholderLabel);

        void RefreshPlaceholder()
        {
            placeholderLabel.style.display = string.IsNullOrEmpty(field.value)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        RefreshPlaceholder();
        field.RegisterValueChangedCallback(evt =>
        {
            changed?.Invoke(evt.newValue);
            RefreshPlaceholder();
        });
        field.RegisterCallback<KeyDownEvent>(evt => evt.StopPropagation());
        field.RegisterCallback<KeyUpEvent>(evt => evt.StopPropagation());
        field.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            VisualElement input = field.Q(className: TextInputBaseField<string>.inputUssClassName);
            if (input != null)
            {
                input.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
                input.style.unityFontDefinition = options.BodyFont;
                input.style.fontSize = UiFont(22f);
                input.style.color = Color.white;
                input.style.letterSpacing = 0f;
                input.style.unityTextAlign = TextAnchor.MiddleLeft;
                input.style.paddingLeft = 0f;
                input.style.paddingRight = 0f;
            }
            UnityEngine.UIElements.TextElement textElement = field.Q<UnityEngine.UIElements.TextElement>();
            if (textElement != null)
            {
                textElement.style.unityFontDefinition = options.BodyFont;
                textElement.style.fontSize = UiFont(22f);
                textElement.style.color = Color.white;
                textElement.style.letterSpacing = 0f;
            }
        });
        return field;
    }

    private ScrollView CreateScrollView()
    {
        ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1f;
        scroll.style.minHeight = 0f;
        scroll.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.contentContainer.style.flexDirection = FlexDirection.Column;
        scroll.contentContainer.style.paddingBottom = 10f;
        return scroll;
    }

    private Button CreateActionButton(string text, Action action, bool primary, float width)
    {
        Button button = new Button(action) { text = text };
        button.style.width = width;
        button.style.minWidth = width;
        button.style.height = 62f;
        button.style.marginLeft = 14f;
        button.style.paddingLeft = 24f;
        button.style.paddingRight = 24f;
        button.style.fontSize = UiFont(19f);
        ApplyBodyFont(button);
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.whiteSpace = WhiteSpace.NoWrap;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.backgroundColor = primary ? new Color(1f, 0.58f, 0.08f, 0.92f) : new Color(0f, 0f, 0f, 0f);
        button.style.color = primary ? Color.white : new Color(0.92f, 0.93f, 0.95f, 1f);
        button.style.borderTopWidth = 1f;
        button.style.borderRightWidth = 1f;
        button.style.borderBottomWidth = 1f;
        button.style.borderLeftWidth = 1f;
        Color border = primary ? new Color(1f, 0.78f, 0.24f, 0.78f) : new Color(0.33f, 0.35f, 0.40f, 1f);
        button.style.borderTopColor = border;
        button.style.borderRightColor = border;
        button.style.borderBottomColor = primary ? new Color(0.84f, 0.34f, 0.24f, 0.74f) : new Color(0.20f, 0.22f, 0.25f, 1f);
        button.style.borderLeftColor = border;
        button.style.borderTopLeftRadius = 10f;
        button.style.borderTopRightRadius = 10f;
        button.style.borderBottomLeftRadius = 10f;
        button.style.borderBottomRightRadius = 10f;
        return button;
    }

    private Button CreateDangerButton(string text, Action action, float width)
    {
        Button button = CreateActionButton(text, action, primary: false, width: width);
        button.style.color = new Color(1f, 0.58f, 0.54f, 1f);
        button.style.borderTopColor = new Color(0.76f, 0.28f, 0.28f, 1f);
        button.style.borderRightColor = new Color(0.56f, 0.18f, 0.18f, 1f);
        button.style.borderBottomColor = new Color(0.36f, 0.10f, 0.10f, 1f);
        button.style.borderLeftColor = new Color(0.56f, 0.18f, 0.18f, 1f);
        return button;
    }

    private Label CreateEmptyLabel(string text)
    {
        Label label = CreateLabel(text, 23f, new Color(0.90f, 0.92f, 0.96f, 0.72f), true);
        label.style.marginTop = 22f;
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private VisualElement CreateDivider()
    {
        VisualElement divider = new VisualElement();
        divider.style.height = 1f;
        divider.style.marginBottom = 20f;
        divider.style.backgroundColor = new Color(1f, 1f, 1f, 0.14f);
        return divider;
    }

    private Label CreateLabel(string text, float size, Color color, bool bold, bool useTitleFont = false)
    {
        Label label = new Label(text ?? string.Empty);
        label.style.color = color;
        label.style.fontSize = UiFont(size);
        label.style.unityFontDefinition = useTitleFont ? options.TitleFont : options.BodyFont;
        label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
        label.style.letterSpacing = 0f;
        return label;
    }

    private float UiFont(float size)
    {
        float scale = options.FontScale > 0f ? options.FontScale : 1f;
        return size * scale;
    }

    private void ApplyBodyFont(VisualElement element)
    {
        if (element == null)
            return;

        element.style.unityFontDefinition = options.BodyFont;
        element.style.letterSpacing = 0f;
    }

    private static void ApplySliderStyle(Slider slider)
    {
        slider.style.height = 28f;
        slider.style.marginTop = 0f;
        slider.style.marginBottom = 0f;
        slider.style.marginLeft = 0f;
        slider.style.marginRight = 0f;
        slider.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        slider.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            VisualElement dragContainer = slider.Q<VisualElement>(className: "unity-base-slider__drag-container");
            if (dragContainer != null)
            {
                dragContainer.style.height = 8f;
                dragContainer.style.backgroundColor = new Color(0.18f, 0.20f, 0.24f, 1f);
                dragContainer.style.borderTopLeftRadius = 3f;
                dragContainer.style.borderTopRightRadius = 3f;
                dragContainer.style.borderBottomLeftRadius = 3f;
                dragContainer.style.borderBottomRightRadius = 3f;
            }

            VisualElement tracker = slider.Q<VisualElement>(className: "unity-base-slider__tracker");
            if (tracker != null)
            {
                tracker.style.backgroundColor = new Color(1f, 0.58f, 0.08f, 1f);
                tracker.style.height = 8f;
                tracker.style.borderTopLeftRadius = 3f;
                tracker.style.borderTopRightRadius = 3f;
                tracker.style.borderBottomLeftRadius = 3f;
                tracker.style.borderBottomRightRadius = 3f;
            }

            VisualElement dragger = slider.Q<VisualElement>(className: "unity-base-slider__dragger");
            if (dragger != null)
            {
                dragger.style.width = 24f;
                dragger.style.height = 24f;
                dragger.style.backgroundColor = Color.white;
                dragger.style.borderTopLeftRadius = 12f;
                dragger.style.borderTopRightRadius = 12f;
                dragger.style.borderBottomLeftRadius = 12f;
                dragger.style.borderBottomRightRadius = 12f;
            }
        });
    }

    private void EnsureSelectedPedal()
    {
        UnityToneLabRuntime.ToneLabPedalSlot[] chain = runtime?.CurrentPedalChain ?? Array.Empty<UnityToneLabRuntime.ToneLabPedalSlot>();
        if (chain.Length == 0)
        {
            SetSelectedPedal(string.Empty);
            return;
        }

        if (chain.Any(slot => slot != null && string.Equals(slot.pedal_instance_id, options.SelectedPedalInstanceId, StringComparison.Ordinal)))
            return;

        SetSelectedPedal(chain[0]?.pedal_instance_id ?? string.Empty);
    }

    private UnityToneLabRuntime.ToneLabPedalSlot FindSelectedSlot()
    {
        return runtime?.CurrentPedalChain?.FirstOrDefault(slot =>
            slot != null && string.Equals(slot.pedal_instance_id, options.SelectedPedalInstanceId, StringComparison.Ordinal));
    }

    private void SetSelectedPedal(string pedalId)
    {
        options.SelectedPedalInstanceId = pedalId ?? string.Empty;
        options.SetSelectedPedalInstanceId?.Invoke(options.SelectedPedalInstanceId);
    }

    private void SetSidePanel(SidePanelMode mode)
    {
        options.SidePanel = mode;
        options.SetSidePanel?.Invoke(mode);
    }

    private void MarkToneChanged()
    {
        options.MarkWorkingToneChanged?.Invoke();
    }

    private void Rebuild()
    {
        options.RequestRebuild?.Invoke();
    }

    private static string NormalizeSearch(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static bool MatchesSearch(string value, string normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return true;
        return (value ?? string.Empty).ToLowerInvariant().Contains(normalizedQuery);
    }

    private static bool MatchesLibrarySearch(IToneLabPedalDescriptor descriptor, string normalizedQuery)
    {
        if (descriptor == null)
            return false;
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return true;
        return MatchesSearch(descriptor.DisplayName, normalizedQuery) ||
               MatchesSearch(descriptor.ShortName, normalizedQuery) ||
               MatchesSearch(descriptor.Description, normalizedQuery) ||
               MatchesSearch(descriptor.DescriptorId, normalizedQuery);
    }

    private static Color GetPresetAccentColor(UnityToneLabRuntime.ToneLabPreset preset)
    {
        bool hasLv2 = false;
        bool hasNam = false;
        IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> chain = preset?.pedal_chain;
        if (chain != null)
        {
            for (int i = 0; i < chain.Count; i++)
            {
                UnityToneLabRuntime.ToneLabPedalSlot slot = chain[i];
                if (slot == null)
                    continue;

                if (slot.pedal_type == UnityToneLabRuntime.ToneLabPedalType.Lv2Plugin)
                    hasLv2 = true;
                else if (slot.pedal_type == UnityToneLabRuntime.ToneLabPedalType.NamModel)
                    hasNam = true;
            }
        }

        if (hasLv2 && hasNam)
            return new Color(0.68f, 0.36f, 1f, 1f);
        if (hasNam)
            return new Color(1f, 0.72f, 0.10f, 1f);
        if (hasLv2)
            return new Color(1f, 0.22f, 0.28f, 1f);
        return new Color(0.20f, 0.84f, 0.46f, 1f);
    }

    private static Color StartupLogoColor(int index)
    {
        switch (index)
        {
            case 1: return new Color(1.00f, 0.77f, 0.15f, 1f);
            case 2: return new Color(0.24f, 0.62f, 1.00f, 1f);
            case 4: return new Color(0.30f, 0.78f, 0.45f, 1f);
            default: return new Color(1.00f, 0.58f, 0.08f, 1f);
        }
    }
}
