using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

public sealed class MiniGamesScreenOverlay
{
    private readonly GuitarBridgeServer owner;
    private readonly FontDefinition bodyFontDefinition;
    private readonly FontDefinition titleFontDefinition;
    private readonly FontDefinition modernFontDefinition;
    private readonly MiniGameChordPreview3DRenderer chordPreviewRenderer;
    private readonly MiniGameFightStage3DRenderer fightStageRenderer;
    private readonly List<Button> gameButtons = new List<Button>();
    private readonly List<Button> pauseButtons = new List<Button>();

    public VisualElement RootElement { get; }

    private readonly VisualElement selectionRoot;
    private readonly Label selectionTitleLabel;
    private readonly ScrollView selectionList;
    private readonly Label selectionFooterLabel;

    private readonly VisualElement setupRoot;
    private readonly VisualElement setupModeChoiceRoot;
    private readonly VisualElement setupArcadeSummaryRoot;
    private readonly VisualElement setupRandomSummaryRoot;
    private readonly ScrollView setupLevelsList;
    private readonly VisualElement setupManualWorkspace;
    private readonly Label setupStatusLabel;
    private readonly Button setupArcadeButton;
    private readonly Button setupRandomButton;
    private readonly Button setupManualButton;
    private readonly Button setupCatalogButton;
    private readonly Button setupSongsButton;
    private readonly Button setupSelectAllGroupsButton;
    private readonly Button setupAddButton;
    private readonly Button setupClearButton;
    private readonly Button setupStartButton;
    private readonly VisualElement setupSongSearchRoot;
    private readonly TextField setupSongSearchField;
    private readonly Label setupSongSearchPlaceholderLabel;
    private readonly ScrollView setupGroupsList;
    private readonly ScrollView setupAvailableList;
    private readonly ScrollView setupSongsList;
    private readonly ScrollView setupPlayableList;

    private readonly VisualElement runSettingsRoot;
    private readonly Label runSettingsTitleLabel;
    private readonly Label runSettingsSubtitleLabel;
    private readonly Label runSettingsLeniencyValueLabel;
    private readonly Label runSettingsLeniencyDescriptionLabel;
    private readonly Label runSettingsBeatValueLabel;
    private readonly Label runSettingsMetronomeSoundValueLabel;
    private readonly Label runSettingsChordInstrumentValueLabel;
    private readonly Label runSettingsCountdownValueLabel;
    private readonly Label runSettingsChordCountValueLabel;
    private readonly Label runSettingsPracticeModeValueLabel;
    private readonly Label runSettingsShowMissedNotesValueLabel;
    private readonly Label runSettingsFailsValueLabel;
    private readonly Button runSettingsPrimaryButton;

    private readonly VisualElement fightHudRoot;
    private readonly Label fightTitleLabel;
    private readonly Label fightScoreLabel;
    private readonly Label fightScoreDetailsLabel;
    private readonly Label fightCountdownLabel;
    private readonly Label fightStatusLabel;
    private readonly VisualElement beatFill;

    private readonly VisualElement pauseOverlay;
    private readonly Label pauseTitleLabel;
    private readonly Button resumeButton;
    private readonly Button settingsButton;
    private readonly Button exitButton;
    private readonly VisualElement endOverlay;
    private readonly VisualElement endPanel;
    private readonly Label endTitleLabel;
    private readonly Label endScoreLabel;
    private readonly Label endSubtitleLabel;
    private readonly VisualElement endStatsGrid;
    private readonly ScrollView endChordResultsList;

    private string lastMenuSignature = string.Empty;
    private string lastSetupSignature = string.Empty;
    private string lastRunSettingsSignature = string.Empty;
    private string setupSongSearchQuery = string.Empty;
    private FightClubSetupSnapshot currentSetupSnapshot;
    private static readonly Dictionary<string, Texture2D> setupSongArtworkTextureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
    private static Texture2D setupPrimaryGradientTexture;
    private static Texture2D setupModeGradientTexture;
    private static Texture2D setupModeInactiveBorderTexture;
    private GameObject fightAudioRoot;
    private StringTheoryMetronome fightMetronome;
    private StringTheoryChordAudioPlayer fightChordAudioPlayer;
    private string fightMetronomeSignature = string.Empty;
    private int lastFightMetronomeSyncSerial = -1;
    private int lastOpponentChordSoundSerial = -1;

    public MiniGamesScreenOverlay(
        GuitarBridgeServer owner,
        FontDefinition bodyFontDefinition,
        FontDefinition titleFontDefinition,
        FontDefinition modernFontDefinition)
    {
        this.owner = owner;
        this.bodyFontDefinition = bodyFontDefinition;
        this.titleFontDefinition = titleFontDefinition;
        this.modernFontDefinition = modernFontDefinition;
        chordPreviewRenderer = new MiniGameChordPreview3DRenderer(owner);
        fightStageRenderer = new MiniGameFightStage3DRenderer(owner);

        RootElement = CreateRoot();

        selectionRoot = new VisualElement();
        selectionRoot.style.position = Position.Absolute;
        selectionRoot.style.left = 0f;
        selectionRoot.style.right = 0f;
        selectionRoot.style.top = 0f;
        selectionRoot.style.bottom = 0f;
        selectionRoot.style.alignItems = Align.Center;
        selectionRoot.style.justifyContent = Justify.Center;
        selectionRoot.style.paddingTop = 76f;
        selectionRoot.style.paddingBottom = 76f;

        Label selectionEyebrow = CreateLabel("MINI GAMES", 32f, new Color(0.12f, 0.93f, 1f, 0.96f), true, TextAnchor.MiddleCenter, false);
        selectionEyebrow.style.unityFontDefinition = modernFontDefinition;
        selectionEyebrow.style.unityFontStyleAndWeight = FontStyle.BoldAndItalic;
        selectionEyebrow.style.letterSpacing = 2.4f;
        selectionEyebrow.style.marginBottom = 4f;

        selectionTitleLabel = CreateLabel("Select Game", 124f, Color.white, true, TextAnchor.MiddleCenter, true);
        selectionTitleLabel.style.unityFontDefinition = titleFontDefinition;
        selectionTitleLabel.style.letterSpacing = 0.8f;

        VisualElement selectionTitleRule = new VisualElement();
        selectionTitleRule.style.width = 250f;
        selectionTitleRule.style.height = 4f;
        selectionTitleRule.style.marginTop = 12f;
        selectionTitleRule.style.marginBottom = 48f;
        selectionTitleRule.style.backgroundColor = new Color(0.08f, 0.92f, 1f, 0.92f);

        selectionList = new ScrollView(ScrollViewMode.Vertical);
        selectionList.style.width = Length.Percent(62f);
        selectionList.style.minWidth = 720f;
        selectionList.style.maxWidth = 1080f;
        selectionList.style.maxHeight = Length.Percent(56f);
        selectionList.style.paddingLeft = 0f;
        selectionList.style.paddingRight = 0f;
        selectionList.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        selectionList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        selectionList.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        selectionFooterLabel = CreateLabel("Esc Back  •  Enter Select", 24f, new Color(0.78f, 0.88f, 0.98f, 0.72f), true, TextAnchor.MiddleCenter, false);
        selectionFooterLabel.style.unityFontDefinition = modernFontDefinition;
        selectionFooterLabel.style.letterSpacing = 1.2f;
        selectionFooterLabel.style.marginTop = 30f;

        selectionRoot.Add(selectionEyebrow);
        selectionRoot.Add(selectionTitleLabel);
        selectionRoot.Add(selectionTitleRule);
        selectionRoot.Add(selectionList);
        selectionRoot.Add(selectionFooterLabel);

        setupRoot = new VisualElement();
        setupRoot.style.position = Position.Absolute;
        setupRoot.style.left = 0f;
        setupRoot.style.right = 0f;
        setupRoot.style.top = 0f;
        setupRoot.style.bottom = 0f;
        setupRoot.style.paddingLeft = 56f;
        setupRoot.style.paddingRight = 56f;
        setupRoot.style.paddingTop = 30f;
        setupRoot.style.paddingBottom = 36f;
        setupRoot.style.alignItems = Align.Stretch;
        setupRoot.style.justifyContent = Justify.FlexStart;

        VisualElement setupHeader = new VisualElement();
        setupHeader.style.flexDirection = FlexDirection.Row;
        setupHeader.style.alignItems = Align.Center;
        setupHeader.style.justifyContent = Justify.SpaceBetween;
        setupHeader.style.marginBottom = 2f;
        setupHeader.style.flexShrink = 0f;

        VisualElement setupTitleStack = new VisualElement();
        setupTitleStack.style.flexShrink = 0f;
        setupTitleStack.style.marginLeft = 10f;
        Label setupEyebrow = CreateLabel("FIGHT CLUB", 38f, new Color(0.12f, 0.93f, 1f, 0.96f), true, TextAnchor.MiddleLeft, false);
        setupEyebrow.style.unityFontDefinition = modernFontDefinition;
        setupEyebrow.style.unityFontStyleAndWeight = FontStyle.BoldAndItalic;
        setupEyebrow.style.letterSpacing = 1.6f;
        setupEyebrow.style.marginBottom = 3f;
        Label setupTitleLabel = CreateLabel("CHORD SETUP", 118f, Color.white, true, TextAnchor.MiddleLeft, true);
        setupTitleLabel.style.unityFontDefinition = titleFontDefinition;
        setupTitleLabel.style.letterSpacing = 0f;
        VisualElement setupTitleRule = new VisualElement();
        setupTitleRule.style.width = 290f;
        setupTitleRule.style.height = 4f;
        setupTitleRule.style.marginTop = 8f;
        setupTitleRule.style.backgroundColor = new Color(0.08f, 0.92f, 1f, 0.92f);
        setupTitleStack.Add(setupEyebrow);
        setupTitleStack.Add(setupTitleLabel);
        setupTitleStack.Add(setupTitleRule);

        VisualElement setupTopActions = new VisualElement();
        setupTopActions.style.flexDirection = FlexDirection.Row;
        setupTopActions.style.alignItems = Align.Center;
        setupTopActions.style.justifyContent = Justify.FlexEnd;
        setupTopActions.style.flexShrink = 0f;
        Button setupBackButton = CreateSetupActionButton("<  Back", () => owner?.CloseFightClubSetupFromUi(), 190f);
        setupBackButton.style.height = 74f;
        setupBackButton.style.fontSize = 31f;
        SetRadius(setupBackButton, 10f);
        setupTopActions.Add(setupBackButton);

        setupHeader.Add(setupTitleStack);
        setupHeader.Add(setupTopActions);

        setupModeChoiceRoot = new VisualElement();
        setupModeChoiceRoot.style.flexDirection = FlexDirection.Row;
        setupModeChoiceRoot.style.alignSelf = Align.Center;
        setupModeChoiceRoot.style.width = Length.Percent(74f);
        setupModeChoiceRoot.style.minWidth = 1580f;
        setupModeChoiceRoot.style.maxWidth = 2140f;
        setupModeChoiceRoot.style.marginTop = 2f;
        setupModeChoiceRoot.style.marginBottom = 22f;
        setupModeChoiceRoot.style.flexShrink = 0f;
        setupArcadeButton = CreateSetupModeButton(
            "Arcade",
            "Arcade run with a growing randomized chord pool.",
            () => owner?.SetFightClubSetupModeFromUi((int)FightClubSetupMode.Arcade));
        setupArcadeButton.style.marginRight = 14f;
        setupRandomButton = CreateSetupModeButton(
            "Levels",
            "Sequential chord drills that unlock as you improve.",
            () => owner?.SetFightClubSetupModeFromUi((int)FightClubSetupMode.Levels));
        setupRandomButton.style.marginLeft = 14f;
        setupRandomButton.style.marginRight = 14f;
        setupManualButton = CreateSetupModeButton(
            "Manual Selection",
            "Build a chord pool from groups or song chords.",
            () => owner?.SetFightClubSetupModeFromUi((int)FightClubSetupMode.Manual));
        setupManualButton.style.marginLeft = 14f;
        setupModeChoiceRoot.Add(setupArcadeButton);
        setupModeChoiceRoot.Add(setupRandomButton);
        setupModeChoiceRoot.Add(setupManualButton);

        setupArcadeSummaryRoot = CreateSetupPanel();
        setupArcadeSummaryRoot.style.alignSelf = Align.Center;
        setupArcadeSummaryRoot.style.width = Length.Percent(70f);
        setupArcadeSummaryRoot.style.minWidth = 0f;
        setupArcadeSummaryRoot.style.maxWidth = 1700f;
        setupArcadeSummaryRoot.style.flexGrow = 1f;
        setupArcadeSummaryRoot.style.minHeight = 0f;
        setupArcadeSummaryRoot.style.maxHeight = Length.Percent(78f);
        setupArcadeSummaryRoot.style.justifyContent = Justify.Center;
        setupArcadeSummaryRoot.style.alignItems = Align.Center;
        setupArcadeSummaryRoot.style.paddingLeft = 68f;
        setupArcadeSummaryRoot.style.paddingRight = 68f;
        Label arcadeTitle = CreateLabel("Arcade", 76f, Color.white, true, TextAnchor.MiddleCenter, false);
        arcadeTitle.style.unityFontDefinition = modernFontDefinition;
        arcadeTitle.style.marginBottom = 16f;
        Label arcadeText = CreateLabel("Starts with a tiny set of basic open chords, then adds one or two new chords every few rounds until the whole catalog is active.", 38f, new Color(0.82f, 0.88f, 0.96f, 0.86f), false, TextAnchor.MiddleCenter, false);
        arcadeText.style.unityFontDefinition = modernFontDefinition;
        arcadeText.style.whiteSpace = WhiteSpace.Normal;
        arcadeText.style.maxWidth = 1280f;
        arcadeText.style.marginBottom = 18f;
        Label arcadeHighScore = CreateLabel("Survive until you miss too many rounds.", 32f, new Color(0.56f, 0.95f, 1f, 0.76f), true, TextAnchor.MiddleCenter, false);
        arcadeHighScore.style.unityFontDefinition = modernFontDefinition;
        setupArcadeSummaryRoot.Add(arcadeTitle);
        setupArcadeSummaryRoot.Add(arcadeText);
        setupArcadeSummaryRoot.Add(arcadeHighScore);

        setupRandomSummaryRoot = CreateSetupPanel();
        setupRandomSummaryRoot.style.alignSelf = Align.Center;
        setupRandomSummaryRoot.style.width = Length.Percent(82f);
        setupRandomSummaryRoot.style.minWidth = 0f;
        setupRandomSummaryRoot.style.maxWidth = 2300f;
        setupRandomSummaryRoot.style.flexGrow = 1f;
        setupRandomSummaryRoot.style.minHeight = 0f;
        setupRandomSummaryRoot.style.maxHeight = Length.Percent(78f);
        setupRandomSummaryRoot.style.justifyContent = Justify.FlexStart;
        setupRandomSummaryRoot.style.alignItems = Align.Center;
        setupRandomSummaryRoot.style.paddingLeft = 58f;
        setupRandomSummaryRoot.style.paddingRight = 58f;
        setupRandomSummaryRoot.style.paddingTop = 54f;
        setupRandomSummaryRoot.style.paddingBottom = 44f;
        Label randomTitle = CreateLabel("Choose a level", 70f, Color.white, true, TextAnchor.MiddleCenter, false);
        randomTitle.style.unityFontDefinition = modernFontDefinition;
        randomTitle.style.marginBottom = 10f;
        Label randomText = CreateLabel("Clear three chords per round. Later levels unlock in order and add harder shapes.", 34f, new Color(0.82f, 0.86f, 0.93f, 0.82f), false, TextAnchor.MiddleCenter, false);
        randomText.style.unityFontDefinition = modernFontDefinition;
        randomText.style.whiteSpace = WhiteSpace.Normal;
        randomText.style.maxWidth = 1500f;
        randomText.style.marginBottom = 46f;
        setupLevelsList = CreateSetupScrollList(1f);
        setupLevelsList.style.width = Length.Percent(95f);
        setupLevelsList.style.maxWidth = 1900f;
        setupLevelsList.style.minHeight = 0f;
        setupRandomSummaryRoot.Add(randomTitle);
        setupRandomSummaryRoot.Add(randomText);
        setupRandomSummaryRoot.Add(setupLevelsList);

        setupManualWorkspace = new VisualElement();
        setupManualWorkspace.style.flexDirection = FlexDirection.Row;
        setupManualWorkspace.style.flexGrow = 1f;
        setupManualWorkspace.style.flexShrink = 1f;
        setupManualWorkspace.style.minHeight = 0f;
        setupManualWorkspace.style.alignSelf = Align.Center;
        setupManualWorkspace.style.width = Length.Percent(100f);
        setupManualWorkspace.style.maxWidth = 3560f;

        VisualElement sourceRail = CreateSetupPanel();
        sourceRail.style.width = 1180f;
        sourceRail.style.minWidth = 1180f;
        sourceRail.style.marginRight = 36f;
        Label sourceTitle = CreateLabel("Choose Source", 52f, Color.white, true, TextAnchor.MiddleLeft, false);
        sourceTitle.style.unityFontDefinition = modernFontDefinition;
        sourceTitle.style.marginBottom = 30f;
        VisualElement sourceTabs = new VisualElement();
        sourceTabs.style.flexDirection = FlexDirection.Row;
        sourceTabs.style.alignItems = Align.Center;
        sourceTabs.style.marginBottom = 36f;
        setupCatalogButton = CreateSetupActionButton("Catalog", () => owner?.SetFightClubSetupSourceModeFromUi(0), 260f);
        setupCatalogButton.style.marginLeft = 0f;
        setupSongsButton = CreateSetupActionButton("Songs", () => owner?.SetFightClubSetupSourceModeFromUi(1), 230f);
        sourceTabs.Add(setupCatalogButton);
        sourceTabs.Add(setupSongsButton);
        setupSongSearchRoot = CreateSetupSongSearchField("search songs...", out setupSongSearchField, out setupSongSearchPlaceholderLabel);
        sourceTabs.Add(setupSongSearchRoot);
        setupSelectAllGroupsButton = CreateSetupActionButton("Select All Groups", () => owner?.SelectAllFightClubSetupGroupsFromUi(), 520f);
        setupSelectAllGroupsButton.style.marginLeft = 0f;
        setupSelectAllGroupsButton.style.marginBottom = 28f;
        setupGroupsList = CreateSetupScrollList(1f);
        setupSongsList = CreateSetupScrollList(1f);
        sourceRail.Add(sourceTitle);
        sourceRail.Add(sourceTabs);
        sourceRail.Add(setupSelectAllGroupsButton);
        sourceRail.Add(setupGroupsList);
        sourceRail.Add(setupSongsList);

        VisualElement availablePanel = CreateSetupPanel();
        availablePanel.style.width = 720f;
        availablePanel.style.minWidth = 700f;
        availablePanel.style.flexGrow = 0f;
        availablePanel.style.flexShrink = 1f;
        availablePanel.style.marginRight = 34f;
        Label availableTitle = CreateLabel("Available Chords", 52f, Color.white, true, TextAnchor.MiddleLeft, false);
        availableTitle.style.unityFontDefinition = modernFontDefinition;
        availableTitle.style.marginBottom = 30f;
        setupAvailableList = CreateSetupScrollList(1f);
        availablePanel.Add(availableTitle);
        availablePanel.Add(setupAvailableList);

        VisualElement addColumn = new VisualElement();
        addColumn.style.width = 330f;
        addColumn.style.minWidth = 330f;
        addColumn.style.marginRight = 34f;
        addColumn.style.alignItems = Align.Center;
        addColumn.style.justifyContent = Justify.Center;
        setupAddButton = CreateSetupActionButton("Add Selected", () => owner?.AddCheckedFightClubChordsToPlayableFromUi(), 300f);
        setupAddButton.style.height = 142f;
        setupAddButton.style.fontSize = 52f;
        setupAddButton.style.marginLeft = 0f;
        addColumn.Add(setupAddButton);

        VisualElement gameChordPanel = CreateSetupPanel();
        gameChordPanel.style.width = 900f;
        gameChordPanel.style.minWidth = 900f;
        gameChordPanel.style.maxWidth = 980f;
        VisualElement gameChordHeader = new VisualElement();
        gameChordHeader.style.flexDirection = FlexDirection.Row;
        gameChordHeader.style.alignItems = Align.Center;
        gameChordHeader.style.justifyContent = Justify.SpaceBetween;
        gameChordHeader.style.marginBottom = 30f;
        Label gameChordTitle = CreateLabel("Game Chords", 52f, Color.white, true, TextAnchor.MiddleLeft, false);
        gameChordTitle.style.unityFontDefinition = modernFontDefinition;
        setupClearButton = CreateSetupActionButton("Clear", () => owner?.ClearFightClubPlayableChordsFromUi(), 210f);
        gameChordHeader.Add(gameChordTitle);
        gameChordHeader.Add(setupClearButton);
        setupPlayableList = CreateSetupScrollList(1f);
        gameChordPanel.Add(gameChordHeader);
        gameChordPanel.Add(setupPlayableList);

        setupManualWorkspace.Add(sourceRail);
        setupManualWorkspace.Add(availablePanel);
        setupManualWorkspace.Add(addColumn);
        setupManualWorkspace.Add(gameChordPanel);

        VisualElement setupFooter = new VisualElement();
        setupFooter.style.flexDirection = FlexDirection.Column;
        setupFooter.style.alignItems = Align.Center;
        setupFooter.style.justifyContent = Justify.Center;
        setupFooter.style.marginTop = 20f;
        setupFooter.style.flexShrink = 0f;
        setupStatusLabel = CreateLabel(string.Empty, 34f, new Color(0.86f, 0.92f, 1f, 0.96f), true, TextAnchor.MiddleCenter, false);
        setupStatusLabel.style.unityFontDefinition = modernFontDefinition;
        setupStatusLabel.style.whiteSpace = WhiteSpace.Normal;
        setupStatusLabel.style.marginBottom = 12f;
        setupStartButton = CreateSetupPrimaryButton("Next: Run Setup", () => owner?.StartConfiguredFightClubMiniGameFromUi());
        setupFooter.Add(setupStatusLabel);
        setupFooter.Add(setupStartButton);

        setupRoot.Add(setupHeader);
        setupRoot.Add(setupModeChoiceRoot);
        setupRoot.Add(setupArcadeSummaryRoot);
        setupRoot.Add(setupRandomSummaryRoot);
        setupRoot.Add(setupManualWorkspace);
        setupRoot.Add(setupFooter);

        runSettingsRoot = new VisualElement();
        runSettingsRoot.style.position = Position.Absolute;
        runSettingsRoot.style.left = 0f;
        runSettingsRoot.style.right = 0f;
        runSettingsRoot.style.top = 0f;
        runSettingsRoot.style.bottom = 0f;
        runSettingsRoot.style.alignItems = Align.Center;
        runSettingsRoot.style.justifyContent = Justify.Center;
        runSettingsRoot.style.paddingTop = 80f;
        runSettingsRoot.style.paddingBottom = 80f;
        runSettingsRoot.style.backgroundColor = new Color(0.00f, 0.01f, 0.035f, 0.42f);

        VisualElement runSettingsPanel = CreateSetupPanel();
        runSettingsPanel.style.width = Length.Percent(58f);
        runSettingsPanel.style.minWidth = 900f;
        runSettingsPanel.style.maxWidth = 1320f;
        runSettingsPanel.style.paddingLeft = 62f;
        runSettingsPanel.style.paddingRight = 62f;
        runSettingsPanel.style.paddingTop = 48f;
        runSettingsPanel.style.paddingBottom = 44f;
        runSettingsPanel.style.backgroundColor = new Color(0.004f, 0.014f, 0.030f, 0.88f);
        SetBorder(runSettingsPanel, new Color(0.64f, 0.84f, 1f, 0.34f), 1.5f);

        Label runSettingsEyebrow = CreateLabel("FIGHT CLUB", 28f, new Color(0.12f, 0.93f, 1f, 0.96f), true, TextAnchor.MiddleCenter, false);
        runSettingsEyebrow.style.unityFontDefinition = modernFontDefinition;
        runSettingsEyebrow.style.unityFontStyleAndWeight = FontStyle.BoldAndItalic;
        runSettingsEyebrow.style.letterSpacing = 1.8f;
        runSettingsEyebrow.style.marginBottom = 4f;

        runSettingsTitleLabel = CreateLabel("Run Setup", 82f, Color.white, true, TextAnchor.MiddleCenter, true);
        runSettingsTitleLabel.style.unityFontDefinition = titleFontDefinition;
        runSettingsTitleLabel.style.marginBottom = 8f;

        runSettingsSubtitleLabel = CreateLabel("Tune the rules for this run only.", 30f, new Color(0.82f, 0.90f, 1f, 0.78f), false, TextAnchor.MiddleCenter, false);
        runSettingsSubtitleLabel.style.unityFontDefinition = modernFontDefinition;
        runSettingsSubtitleLabel.style.whiteSpace = WhiteSpace.Normal;
        runSettingsSubtitleLabel.style.marginBottom = 34f;

        VisualElement runSettingsList = new VisualElement();
        runSettingsList.style.flexDirection = FlexDirection.Column;
        runSettingsList.style.marginBottom = 34f;

        runSettingsList.Add(CreateRunSettingRow(
            "Chord Leniency",
            "Temporary Fight Club matching only. Normal game detection is not changed.",
            out runSettingsLeniencyValueLabel,
            () => owner?.CycleFightClubChordLeniencyFromUi(-1),
            () => owner?.CycleFightClubChordLeniencyFromUi(1),
            out runSettingsLeniencyDescriptionLabel));
        runSettingsList.Add(CreateRunSettingRow(
            "Tempo",
            "BPM for the metronome, opponent demo, and player chord windows.",
            out runSettingsBeatValueLabel,
            () => owner?.AdjustFightClubTempoFromUi(-5),
            () => owner?.AdjustFightClubTempoFromUi(5)));
        runSettingsList.Add(CreateRunSettingRow(
            "Metronome",
            "Sound used for the beat during setup and the run.",
            out runSettingsMetronomeSoundValueLabel,
            () => owner?.CycleFightClubMetronomeSoundFromUi(-1),
            () => owner?.CycleFightClubMetronomeSoundFromUi(1)));
        runSettingsList.Add(CreateRunSettingRow(
            "Chord Sound",
            "Instrument used when player 2 demonstrates a chord.",
            out runSettingsChordInstrumentValueLabel,
            () => owner?.CycleFightClubChordPreviewInstrumentFromUi(-1),
            () => owner?.CycleFightClubChordPreviewInstrumentFromUi(1)));
        runSettingsList.Add(CreateRunSettingRow(
            "Count-In",
            "How many beats count down before each player starts.",
            out runSettingsCountdownValueLabel,
            () => owner?.AdjustFightClubCountdownFromUi(-1f),
            () => owner?.AdjustFightClubCountdownFromUi(1f)));
        runSettingsList.Add(CreateRunSettingRow(
            "Chords Per Round",
            "Four keeps the phrase locked to the 4-beat metronome. Three keeps rounds shorter.",
            out runSettingsChordCountValueLabel,
            () => owner?.CycleFightClubChordCountFromUi(-1),
            () => owner?.CycleFightClubChordCountFromUi(1)));
        runSettingsList.Add(CreateRunSettingRow(
            "Practice Mode",
            "Repeats missed sequences until every chord is right. Practice scores are not saved.",
            out runSettingsPracticeModeValueLabel,
            () => owner?.ToggleFightClubPracticeModeFromUi(),
            () => owner?.ToggleFightClubPracticeModeFromUi()));
        runSettingsList.Add(CreateRunSettingRow(
            "Show Missed Notes",
            "Outlines the expected chord notes that were absent when a chord fails.",
            out runSettingsShowMissedNotesValueLabel,
            () => owner?.ToggleFightClubShowMissedNotesFromUi(),
            () => owner?.ToggleFightClubShowMissedNotesFromUi()));
        runSettingsList.Add(CreateRunSettingRow(
            "Failed Rounds",
            "How many imperfect rounds end the run.",
            out runSettingsFailsValueLabel,
            () => owner?.AdjustFightClubMaxFailedRoundsFromUi(-1),
            () => owner?.AdjustFightClubMaxFailedRoundsFromUi(1)));

        VisualElement runSettingsActions = new VisualElement();
        runSettingsActions.style.flexDirection = FlexDirection.Row;
        runSettingsActions.style.alignItems = Align.Center;
        runSettingsActions.style.justifyContent = Justify.Center;
        Button runSettingsBackButton = CreateEndActionButton("Back", () => owner?.CloseFightClubRunSettingsFromUi(), false);
        runSettingsPrimaryButton = CreateEndActionButton("Start Fight Club", () => owner?.ConfirmFightClubRunSettingsFromUi(), true);
        runSettingsPrimaryButton.style.width = 360f;
        runSettingsActions.Add(runSettingsBackButton);
        runSettingsActions.Add(runSettingsPrimaryButton);

        runSettingsPanel.Add(runSettingsEyebrow);
        runSettingsPanel.Add(runSettingsTitleLabel);
        runSettingsPanel.Add(runSettingsSubtitleLabel);
        runSettingsPanel.Add(runSettingsList);
        runSettingsPanel.Add(runSettingsActions);
        runSettingsRoot.Add(runSettingsPanel);

        fightHudRoot = new VisualElement();
        fightHudRoot.style.position = Position.Absolute;
        fightHudRoot.style.left = 0f;
        fightHudRoot.style.right = 0f;
        fightHudRoot.style.top = 0f;
        fightHudRoot.style.bottom = 0f;
        fightHudRoot.style.paddingTop = 46f;
        fightHudRoot.style.paddingLeft = 48f;
        fightHudRoot.style.paddingRight = 48f;
        fightHudRoot.style.paddingBottom = 36f;
        fightHudRoot.pickingMode = PickingMode.Ignore;

        VisualElement fightTop = new VisualElement();
        fightTop.style.flexDirection = FlexDirection.Row;
        fightTop.style.alignItems = Align.FlexStart;
        fightTop.style.justifyContent = Justify.SpaceBetween;

        VisualElement titleStack = new VisualElement();
        titleStack.style.alignItems = Align.FlexStart;
        fightTitleLabel = CreateLabel("Fight Club", 60f, Color.white, true, TextAnchor.MiddleLeft, true);
        Label fightHintLabel = CreateLabel("Pause (Esc)  •  Restart (R)", 23f, new Color(0.78f, 0.88f, 0.98f, 0.84f), true, TextAnchor.MiddleLeft, false);
        fightHintLabel.style.unityFontDefinition = modernFontDefinition;
        titleStack.Add(fightTitleLabel);
        titleStack.Add(fightHintLabel);

        fightScoreLabel = CreateLabel("0", 108f, new Color(0.76f, 0.95f, 1f, 1f), true, TextAnchor.MiddleRight, false);
        fightScoreLabel.style.unityFontDefinition = titleFontDefinition;
        fightScoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        fightScoreLabel.style.whiteSpace = WhiteSpace.NoWrap;
        fightScoreLabel.style.marginBottom = 0f;

        fightScoreDetailsLabel = CreateLabel("Round 1  •  Streak 0  •  Failed 0/3", 24f, new Color(0.84f, 0.91f, 1f, 0.88f), true, TextAnchor.MiddleCenter, false);
        fightScoreDetailsLabel.style.unityFontDefinition = modernFontDefinition;
        fightScoreDetailsLabel.style.fontSize = 30f;
        fightScoreDetailsLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        fightScoreDetailsLabel.style.whiteSpace = WhiteSpace.NoWrap;

        VisualElement fightScoreBlock = new VisualElement();
        fightScoreBlock.style.position = Position.Absolute;
        fightScoreBlock.style.right = 48f;
        fightScoreBlock.style.top = 28f;
        fightScoreBlock.style.width = 560f;
        fightScoreBlock.style.alignItems = Align.FlexEnd;
        fightScoreBlock.style.justifyContent = Justify.FlexStart;
        fightScoreBlock.pickingMode = PickingMode.Ignore;
        fightScoreBlock.Add(fightScoreLabel);
        fightScoreBlock.Add(fightScoreDetailsLabel);

        fightTop.Add(titleStack);

        fightCountdownLabel = CreateLabel(string.Empty, 132f, new Color(0.55f, 0.94f, 1f, 1f), true, TextAnchor.MiddleCenter, true);
        fightCountdownLabel.style.position = Position.Absolute;
        fightCountdownLabel.style.left = 0f;
        fightCountdownLabel.style.right = 0f;
        fightCountdownLabel.style.top = Length.Percent(38f);
        fightCountdownLabel.style.height = 178f;

        fightStatusLabel = CreateLabel(string.Empty, 38f, new Color(0.92f, 0.97f, 1f, 0.94f), true, TextAnchor.MiddleCenter, false);
        fightStatusLabel.style.unityFontDefinition = modernFontDefinition;
        fightStatusLabel.style.position = Position.Absolute;
        fightStatusLabel.style.left = 0f;
        fightStatusLabel.style.right = 0f;
        fightStatusLabel.style.bottom = 108f;
        fightStatusLabel.style.height = 42f;

        VisualElement beatTrack = new VisualElement();
        beatTrack.style.position = Position.Absolute;
        beatTrack.style.left = Length.Percent(33f);
        beatTrack.style.right = Length.Percent(33f);
        beatTrack.style.bottom = 78f;
        beatTrack.style.height = 8f;
        beatTrack.style.borderTopLeftRadius = 8f;
        beatTrack.style.borderTopRightRadius = 8f;
        beatTrack.style.borderBottomLeftRadius = 8f;
        beatTrack.style.borderBottomRightRadius = 8f;
        beatTrack.style.backgroundColor = new Color(0.06f, 0.12f, 0.18f, 0.84f);

        beatFill = new VisualElement();
        beatFill.style.width = Length.Percent(0f);
        beatFill.style.height = Length.Percent(100f);
        beatFill.style.borderTopLeftRadius = 8f;
        beatFill.style.borderTopRightRadius = 8f;
        beatFill.style.borderBottomLeftRadius = 8f;
        beatFill.style.borderBottomRightRadius = 8f;
        beatFill.style.backgroundColor = new Color(0.35f, 0.92f, 1f, 0.95f);
        beatTrack.Add(beatFill);

        fightHudRoot.Add(fightTop);
        fightHudRoot.Add(fightScoreBlock);
        fightHudRoot.Add(fightCountdownLabel);
        fightHudRoot.Add(fightStatusLabel);
        fightHudRoot.Add(beatTrack);

        pauseOverlay = new VisualElement();
        pauseOverlay.style.position = Position.Absolute;
        pauseOverlay.style.left = 0f;
        pauseOverlay.style.right = 0f;
        pauseOverlay.style.top = 0f;
        pauseOverlay.style.bottom = 0f;
        pauseOverlay.style.alignItems = Align.FlexEnd;
        pauseOverlay.style.justifyContent = Justify.Center;
        pauseOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        VisualElement pausePanel = new VisualElement();
        pausePanel.style.width = Length.Percent(34f);
        pausePanel.style.minWidth = 520f;
        pausePanel.style.maxWidth = 720f;
        pausePanel.style.height = Length.Percent(100f);
        pausePanel.style.paddingLeft = 34f;
        pausePanel.style.paddingRight = 46f;
        pausePanel.style.paddingTop = 128f;
        pausePanel.style.paddingBottom = 64f;
        pausePanel.style.alignItems = Align.Stretch;
        pausePanel.style.justifyContent = Justify.Center;
        pausePanel.style.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.40f);
        pausePanel.style.borderLeftWidth = 2f;
        pausePanel.style.borderLeftColor = new Color(0.92f, 0.96f, 1f, 0.78f);

        Label pauseEyebrow = CreateLabel("FIGHT CLUB", 25f, new Color(0.57f, 0.90f, 1f, 0.96f), true, TextAnchor.MiddleLeft, false);
        pauseEyebrow.style.unityFontDefinition = modernFontDefinition;
        pauseEyebrow.style.letterSpacing = 2.8f;
        pauseEyebrow.style.marginBottom = 8f;

        pauseTitleLabel = CreateLabel("Paused", 106f, Color.white, true, TextAnchor.MiddleLeft, true);
        pauseTitleLabel.style.unityFontDefinition = titleFontDefinition;
        pauseTitleLabel.style.marginBottom = 34f;
        resumeButton = CreatePauseButton("Resume", 0, () => owner?.ResumeFightClubMiniGameFromUi());
        settingsButton = CreatePauseButton("Settings", 1, () => owner?.OpenFightClubRunSettingsFromPauseUi());
        exitButton = CreatePauseButton("End", 2, () => owner?.EndFightClubMiniGameFromUi());
        pauseButtons.Add(resumeButton);
        pauseButtons.Add(settingsButton);
        pauseButtons.Add(exitButton);

        pausePanel.Add(pauseEyebrow);
        pausePanel.Add(pauseTitleLabel);
        pausePanel.Add(resumeButton);
        pausePanel.Add(settingsButton);
        pausePanel.Add(exitButton);
        pauseOverlay.Add(pausePanel);

        endOverlay = new VisualElement();
        endOverlay.style.position = Position.Absolute;
        endOverlay.style.left = 0f;
        endOverlay.style.right = 0f;
        endOverlay.style.top = 0f;
        endOverlay.style.bottom = 0f;
        endOverlay.style.alignItems = Align.Center;
        endOverlay.style.justifyContent = Justify.Center;
        endOverlay.style.paddingTop = 68f;
        endOverlay.style.paddingBottom = 68f;
        endOverlay.style.backgroundColor = new Color(0f, 0.004f, 0.018f, 0.46f);

        endPanel = CreateSetupPanel();
        endPanel.style.width = Length.Percent(62f);
        endPanel.style.minWidth = 980f;
        endPanel.style.maxWidth = 1480f;
        endPanel.style.maxHeight = Length.Percent(90f);
        endPanel.style.minHeight = 0f;
        endPanel.style.paddingLeft = 58f;
        endPanel.style.paddingRight = 58f;
        endPanel.style.paddingTop = 46f;
        endPanel.style.paddingBottom = 42f;
        endPanel.style.alignItems = Align.Stretch;
        endPanel.style.overflow = Overflow.Hidden;
        endPanel.style.backgroundColor = new Color(0.004f, 0.012f, 0.028f, 0.88f);
        SetBorder(endPanel, new Color(0.72f, 0.84f, 1f, 0.28f), 1.5f);

        Label endEyebrow = CreateLabel("FIGHT CLUB", 27f, new Color(0.12f, 0.93f, 1f, 0.94f), true, TextAnchor.MiddleCenter, false);
        endEyebrow.style.unityFontDefinition = modernFontDefinition;
        endEyebrow.style.unityFontStyleAndWeight = FontStyle.BoldAndItalic;
        endEyebrow.style.letterSpacing = 1.6f;
        endEyebrow.style.marginBottom = 2f;

        endTitleLabel = CreateLabel("Run Complete", 112f, Color.white, true, TextAnchor.MiddleCenter, true);
        endTitleLabel.style.unityFontDefinition = titleFontDefinition;
        endTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        endTitleLabel.style.marginBottom = 10f;

        endScoreLabel = CreateLabel("0", 142f, new Color(0.66f, 0.94f, 1f, 1f), true, TextAnchor.MiddleCenter, false);
        endScoreLabel.style.unityFontDefinition = modernFontDefinition;
        endScoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        endScoreLabel.style.marginBottom = 0f;

        endSubtitleLabel = CreateLabel(string.Empty, 29f, new Color(0.84f, 0.91f, 1f, 0.88f), true, TextAnchor.MiddleCenter, false);
        endSubtitleLabel.style.unityFontDefinition = modernFontDefinition;
        endSubtitleLabel.style.whiteSpace = WhiteSpace.Normal;
        endSubtitleLabel.style.marginBottom = 26f;

        endStatsGrid = new VisualElement();
        endStatsGrid.style.flexDirection = FlexDirection.Row;
        endStatsGrid.style.alignItems = Align.Center;
        endStatsGrid.style.justifyContent = Justify.Center;
        endStatsGrid.style.marginBottom = 28f;

        Label resultsTitle = CreateLabel("Chord Report", 38f, Color.white, true, TextAnchor.MiddleLeft, false);
        resultsTitle.style.unityFontDefinition = modernFontDefinition;
        resultsTitle.style.marginBottom = 12f;

        endChordResultsList = new ScrollView(ScrollViewMode.Vertical);
        endChordResultsList.style.flexGrow = 1f;
        endChordResultsList.style.flexShrink = 1f;
        endChordResultsList.style.minHeight = 180f;
        endChordResultsList.style.maxHeight = Length.Percent(34f);
        endChordResultsList.style.paddingRight = 0f;
        endChordResultsList.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        endChordResultsList.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        endChordResultsList.style.backgroundColor = new Color(0.006f, 0.018f, 0.034f, 0.34f);
        SetRadius(endChordResultsList, 12f);
        SetBorder(endChordResultsList, new Color(0.62f, 0.78f, 1f, 0.18f), 1f);

        VisualElement endActions = new VisualElement();
        endActions.style.flexDirection = FlexDirection.Row;
        endActions.style.alignItems = Align.Center;
        endActions.style.justifyContent = Justify.Center;
        endActions.style.marginTop = 28f;
        endActions.Add(CreateEndActionButton("Retry", () => owner?.RestartFightClubMiniGameFromUi(), true));
        endActions.Add(CreateEndActionButton("Chord Setup", () => owner?.OpenFightClubSetupFromResultFromUi(), false));
        endActions.Add(CreateEndActionButton("Mini Games", () => owner?.ExitFightClubMiniGameToSelectionFromUi(), false));

        endPanel.Add(endEyebrow);
        endPanel.Add(endTitleLabel);
        endPanel.Add(endScoreLabel);
        endPanel.Add(endSubtitleLabel);
        endPanel.Add(endStatsGrid);
        endPanel.Add(resultsTitle);
        endPanel.Add(endChordResultsList);
        endPanel.Add(endActions);
        endOverlay.Add(endPanel);

        RootElement.Add(selectionRoot);
        RootElement.Add(setupRoot);
        RootElement.Add(runSettingsRoot);
        RootElement.Add(fightHudRoot);
        RootElement.Add(pauseOverlay);
        RootElement.Add(endOverlay);
    }

    public void Update(GuitarGameplaySnapshot snapshot, bool visible)
    {
        RootElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible || snapshot == null)
        {
            chordPreviewRenderer.Hide();
            fightStageRenderer.Hide();
            StopFightAudio();
            return;
        }

        MiniGameScreenSnapshot miniSnapshot = snapshot.miniGameSnapshot ?? new MiniGameScreenSnapshot();
        bool fightActive = miniSnapshot.fightClubActive;
        bool fightEnded = fightActive && miniSnapshot.fightClub != null && miniSnapshot.fightClub.ended;
        bool runSettingsVisible = miniSnapshot.fightClubRunSettings != null && miniSnapshot.fightClubRunSettings.visible;
        bool setupVisible = !fightActive && miniSnapshot.fightClubSetup != null && miniSnapshot.fightClubSetup.visible;
        RootElement.style.backgroundColor = fightActive
            ? new Color(0f, 0f, 0f, 0f)
            : new Color(0.00f, 0.01f, 0.035f, 0.36f);

        selectionRoot.style.display = !fightActive && !setupVisible && !runSettingsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        setupRoot.style.display = setupVisible && !runSettingsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        runSettingsRoot.style.display = runSettingsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        fightHudRoot.style.display = fightActive && !fightEnded && !runSettingsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        pauseOverlay.style.display = fightActive && !fightEnded && snapshot.isPaused && !runSettingsVisible ? DisplayStyle.Flex : DisplayStyle.None;
        endOverlay.style.display = fightEnded && !runSettingsVisible ? DisplayStyle.Flex : DisplayStyle.None;

        if (!fightActive && !setupVisible)
        {
            string menuSignature = BuildMenuSignature(miniSnapshot);
            if (!string.Equals(menuSignature, lastMenuSignature, StringComparison.Ordinal))
            {
                RebuildSelectionList(miniSnapshot);
                lastMenuSignature = menuSignature;
            }
        }

        if (setupVisible)
        {
            string setupSignature = BuildSetupSignature(miniSnapshot.fightClubSetup);
            if (!string.Equals(setupSignature, lastSetupSignature, StringComparison.Ordinal))
            {
                RebuildSetupScreen(miniSnapshot.fightClubSetup);
                lastSetupSignature = setupSignature;
            }
        }

        if (runSettingsVisible)
        {
            string runSettingsSignature = BuildRunSettingsSignature(miniSnapshot.fightClubRunSettings);
            if (!string.Equals(runSettingsSignature, lastRunSettingsSignature, StringComparison.Ordinal))
            {
                UpdateRunSettingsScreen(miniSnapshot.fightClubRunSettings);
                lastRunSettingsSignature = runSettingsSignature;
            }
        }

        UpdateFightHud(fightActive && !fightEnded && !runSettingsVisible, miniSnapshot.fightClub);
        UpdateFightEnd(fightEnded && !runSettingsVisible, miniSnapshot.fightClub);
        UpdatePauseSelection(miniSnapshot.selectedPauseActionIndex);
        bool showFightStage = visible && fightActive && !fightEnded && !runSettingsVisible;
        fightStageRenderer.Update(miniSnapshot.fightClub, showFightStage);
        chordPreviewRenderer.Update(miniSnapshot.fightClub, showFightStage);
        UpdateFightAudio(snapshot, miniSnapshot, runSettingsVisible, fightActive, fightEnded);
    }

    private void UpdateFightAudio(
        GuitarGameplaySnapshot gameplaySnapshot,
        MiniGameScreenSnapshot miniSnapshot,
        bool runSettingsVisible,
        bool fightActive,
        bool fightEnded)
    {
        FightClubRunSettingsSnapshot runSettings = miniSnapshot?.fightClubRunSettings;
        FightClubMiniGameSnapshot fight = miniSnapshot?.fightClub;
        bool playSettingsMetronome = runSettingsVisible && runSettings != null && runSettings.visible;
        bool playRunMetronome = fightActive && !fightEnded && fight != null && !runSettingsVisible && !(gameplaySnapshot?.isPaused ?? false);

        if (playSettingsMetronome || playRunMetronome)
        {
            EnsureFightAudio();
            EnsureFightChordAudioPlayer();
            float beatInterval = playSettingsMetronome ? runSettings.beatIntervalSeconds : fight.beatIntervalSeconds;
            int soundIndex = playSettingsMetronome ? runSettings.metronomeSoundIndex : fight.metronomeSoundIndex;
            StringTheoryMetronomeSound sound = StringTheoryMetronome.NormalizeSoundIndex(soundIndex);
            string signature = $"{beatInterval:0.000}|{(int)sound}";
            int syncSerial = playSettingsMetronome ? -1 : fight.metronomeSyncSerial;
            bool restart = !string.Equals(signature, fightMetronomeSignature, StringComparison.Ordinal) ||
                           (!playSettingsMetronome && syncSerial != lastFightMetronomeSyncSerial) ||
                           fightMetronome == null ||
                           !fightMetronome.IsRunning;

            if (restart)
            {
                double startDspTime = GetFightMetronomeStartDspTime(playSettingsMetronome, fight, beatInterval);
                int initialBeatIndex = GetFightMetronomeInitialBeatIndex(playSettingsMetronome, fight, beatInterval);
                fightMetronome.StartMetronome(startDspTime, Mathf.Max(0.2f, beatInterval), sound, 4, 0.80f, initialBeatIndex);
                fightMetronomeSignature = signature;
                lastFightMetronomeSyncSerial = syncSerial;
            }
            else
            {
                fightMetronome.Reconfigure(Mathf.Max(0.2f, beatInterval), sound, 4, 0.80f);
            }
        }
        else if (fightMetronome != null && fightMetronome.IsRunning)
        {
            fightMetronome.StopMetronome();
            fightMetronomeSignature = string.Empty;
            lastFightMetronomeSyncSerial = -1;
        }

        if (!playRunMetronome || fight == null || !fight.opponentPreviewActive)
            return;

        if (fight.opponentChordSoundSerial == lastOpponentChordSoundSerial ||
            fight.opponentChordSoundIndex < 0 ||
            fight.chords == null ||
            fight.opponentChordSoundIndex >= fight.chords.Count)
        {
            return;
        }

        FightClubChordSnapshot chord = fight.chords[fight.opponentChordSoundIndex];
        if (chord?.expectedMidis == null || chord.expectedMidis.Length == 0)
            return;

        EnsureFightAudio();
        EnsureFightChordAudioPlayer();
        if (!fightChordAudioPlayer.IsReady)
        {
            lastOpponentChordSoundSerial = fight.opponentChordSoundSerial;
            return;
        }

        fightChordAudioPlayer.PlayChord(
            chord.expectedMidis,
            StringTheoryChordAudioPlayer.NormalizeInstrumentIndex(fight.chordPreviewInstrumentIndex));
        lastOpponentChordSoundSerial = fight.opponentChordSoundSerial;
    }

    private static double GetFightMetronomeStartDspTime(
        bool playSettingsMetronome,
        FightClubMiniGameSnapshot fight,
        float beatIntervalSeconds)
    {
        const double settingsLeadSeconds = 0.03d;
        const double runLeadSeconds = 0.012d;
        double now = AudioSettings.dspTime;
        double beatInterval = Math.Max(0.2d, beatIntervalSeconds);
        if (playSettingsMetronome || fight == null)
            return now + settingsLeadSeconds;

        double beatProgress = Math.Max(0d, Math.Min(1d, fight.beatProgress01));
        double elapsedIntoBeat = beatInterval * beatProgress;
        if (elapsedIntoBeat <= runLeadSeconds)
            return now + runLeadSeconds;

        double startDspTime = now - elapsedIntoBeat;
        double earliestStart = now + runLeadSeconds;
        while (startDspTime < earliestStart)
            startDspTime += beatInterval;

        return startDspTime;
    }

    private static int GetFightMetronomeInitialBeatIndex(
        bool playSettingsMetronome,
        FightClubMiniGameSnapshot fight,
        float beatIntervalSeconds)
    {
        if (playSettingsMetronome || fight == null)
            return 0;

        const double runLeadSeconds = 0.012d;
        double beatInterval = Math.Max(0.2d, beatIntervalSeconds);
        double beatProgress = Math.Max(0d, Math.Min(1d, fight.beatProgress01));
        double elapsedIntoBeat = beatInterval * beatProgress;
        int beatIndex = Mathf.Clamp(fight.beatIndexInBar, 0, 3);
        if (elapsedIntoBeat > runLeadSeconds)
            beatIndex = (beatIndex + 1) % 4;

        return beatIndex;
    }

    private void EnsureFightAudio()
    {
        if (fightAudioRoot == null)
        {
            fightAudioRoot = new GameObject("FightClubAudio");
            if (owner != null)
                fightAudioRoot.transform.SetParent(owner.transform, false);
        }

        if (fightMetronome == null)
            fightMetronome = fightAudioRoot.AddComponent<StringTheoryMetronome>();
    }

    private void EnsureFightChordAudioPlayer()
    {
        fightChordAudioPlayer ??= new StringTheoryChordAudioPlayer();
        fightChordAudioPlayer.EnsureInitialized(fightAudioRoot != null ? fightAudioRoot.transform : owner?.transform);
    }

    private void StopFightAudio()
    {
        if (fightMetronome != null)
            fightMetronome.StopMetronome();
        if (fightChordAudioPlayer != null)
            fightChordAudioPlayer.StopImmediately();

        fightMetronomeSignature = string.Empty;
        lastFightMetronomeSyncSerial = -1;
        lastOpponentChordSoundSerial = -1;
    }

    private void RebuildSelectionList(MiniGameScreenSnapshot snapshot)
    {
        gameButtons.Clear();
        selectionList.Clear();

        List<MiniGameMenuEntrySnapshot> entries = snapshot.entries ?? new List<MiniGameMenuEntrySnapshot>();
        for (int i = 0; i < entries.Count; i++)
        {
            MiniGameMenuEntrySnapshot entry = entries[i];
            int index = i;
            Button button = CreateGameListButton(entry, index);
            selectionList.Add(button);
            gameButtons.Add(button);
        }
    }

    private Button CreateGameListButton(MiniGameMenuEntrySnapshot entry, int index)
    {
        bool selected = entry != null && entry.selected;
        Color accentColor = new Color(0.12f, 0.93f, 1f, 0.96f);
        Color idleBorderColor = new Color(0.64f, 0.74f, 0.92f, 0.20f);
        Color selectedBorderColor = new Color(0.12f, 0.93f, 1f, 0.66f);
        Color idleBackgroundColor = new Color(0.004f, 0.014f, 0.030f, 0.72f);
        Color selectedBackgroundColor = new Color(0.012f, 0.055f, 0.095f, 0.86f);

        Button button = new Button(() =>
        {
            owner?.SetMiniGameSelectionFromUi(index);
            owner?.ActivateSelectedMiniGameFromUi();
        });
        button.focusable = false;
        button.text = string.Empty;
        button.style.minHeight = 148f;
        button.style.marginBottom = 22f;
        button.style.paddingLeft = 0f;
        button.style.paddingRight = 36f;
        button.style.paddingTop = 0f;
        button.style.paddingBottom = 0f;
        button.style.backgroundColor = selected ? selectedBackgroundColor : idleBackgroundColor;
        SetBorder(button, selected ? selectedBorderColor : idleBorderColor, selected ? 1.5f : 1f);
        SetRadius(button, 14f);
        button.style.overflow = Overflow.Hidden;

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.flexGrow = 1f;
        row.style.paddingTop = 26f;
        row.style.paddingBottom = 26f;

        VisualElement accentBar = new VisualElement();
        accentBar.style.width = 6f;
        accentBar.style.alignSelf = Align.Stretch;
        accentBar.style.marginRight = 34f;
        accentBar.style.marginTop = -26f;
        accentBar.style.marginBottom = -26f;
        accentBar.style.backgroundColor = selected ? accentColor : new Color(0f, 0f, 0f, 0f);

        VisualElement textColumn = new VisualElement();
        textColumn.style.flexGrow = 1f;
        textColumn.style.flexShrink = 1f;

        Label title = CreateLabel(entry?.title ?? "Mini Game", 54f, selected ? new Color(0.63f, 0.93f, 1f, 1f) : new Color(0.88f, 0.92f, 0.97f, 0.95f), true, TextAnchor.MiddleLeft, true);
        title.style.unityFontDefinition = titleFontDefinition;
        title.style.letterSpacing = 1.1f;

        Label subtitle = CreateLabel(entry?.subtitle ?? string.Empty, 26f, new Color(0.78f, 0.86f, 0.95f, selected ? 0.88f : 0.66f), false, TextAnchor.MiddleLeft, false);
        subtitle.style.unityFontDefinition = modernFontDefinition;
        subtitle.style.whiteSpace = WhiteSpace.Normal;
        subtitle.style.marginTop = 8f;
        subtitle.style.display = string.IsNullOrWhiteSpace(entry?.subtitle) ? DisplayStyle.None : DisplayStyle.Flex;

        textColumn.Add(title);
        textColumn.Add(subtitle);

        row.Add(accentBar);
        row.Add(textColumn);

        if (entry != null && entry.highScore > 0)
        {
            VisualElement scoreColumn = new VisualElement();
            scoreColumn.style.alignItems = Align.FlexEnd;
            scoreColumn.style.justifyContent = Justify.Center;
            scoreColumn.style.marginLeft = 36f;
            scoreColumn.style.flexShrink = 0f;

            Label scoreCaption = CreateLabel("BEST", 20f, new Color(0.56f, 0.95f, 1f, selected ? 0.85f : 0.55f), true, TextAnchor.MiddleRight, false);
            scoreCaption.style.unityFontDefinition = modernFontDefinition;
            scoreCaption.style.letterSpacing = 2.2f;

            Label scoreValue = CreateLabel(entry.highScore.ToString(CultureInfo.InvariantCulture), 44f, selected ? new Color(0.66f, 0.94f, 1f, 1f) : new Color(0.84f, 0.90f, 0.97f, 0.85f), true, TextAnchor.MiddleRight, false);
            scoreValue.style.unityFontDefinition = modernFontDefinition;
            scoreValue.style.whiteSpace = WhiteSpace.NoWrap;

            scoreColumn.Add(scoreCaption);
            scoreColumn.Add(scoreValue);
            row.Add(scoreColumn);
        }

        button.Add(row);
        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            owner?.HoverMiniGameSelectionFromUi(index);
            SetBorder(button, selectedBorderColor, 1.5f);
            button.style.backgroundColor = selectedBackgroundColor;
            accentBar.style.backgroundColor = accentColor;
        });
        button.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            SetBorder(button, selected ? selectedBorderColor : idleBorderColor, selected ? 1.5f : 1f);
            button.style.backgroundColor = selected ? selectedBackgroundColor : idleBackgroundColor;
            accentBar.style.backgroundColor = selected ? accentColor : new Color(0f, 0f, 0f, 0f);
        });
        return button;
    }

    private void RebuildSetupScreen(FightClubSetupSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        currentSetupSnapshot = snapshot;
        FightClubSetupMode setupMode = GetSetupMode(snapshot);
        bool arcadeMode = setupMode == FightClubSetupMode.Arcade;
        bool levelMode = setupMode == FightClubSetupMode.Levels;
        bool manualMode = setupMode == FightClubSetupMode.Manual;
        bool songMode = snapshot.sourceMode == 1;
        StyleSetupModeButton(setupArcadeButton, arcadeMode);
        StyleSetupModeButton(setupRandomButton, levelMode);
        StyleSetupModeButton(setupManualButton, manualMode);
        StyleSetupButton(setupCatalogButton, !songMode);
        StyleSetupButton(setupSongsButton, songMode);
        string setupStatus = snapshot.statusLabel ?? string.Empty;
        setupStatusLabel.text = setupStatus;
        setupStatusLabel.style.display = string.IsNullOrWhiteSpace(setupStatus) ? DisplayStyle.None : DisplayStyle.Flex;
        setupStartButton.SetEnabled(snapshot.canStart);
        StyleSetupPrimaryButton(setupStartButton, snapshot.canStart);
        bool canAdd = manualMode && snapshot.availableChords != null && snapshot.availableChords.Exists(chord => chord != null && chord.selected);
        setupAddButton.SetEnabled(canAdd);
        StyleSetupAddButton(setupAddButton, canAdd);
        setupArcadeSummaryRoot.style.display = arcadeMode ? DisplayStyle.Flex : DisplayStyle.None;
        setupRandomSummaryRoot.style.display = levelMode ? DisplayStyle.Flex : DisplayStyle.None;
        setupManualWorkspace.style.display = manualMode ? DisplayStyle.Flex : DisplayStyle.None;
        setupGroupsList.style.display = songMode ? DisplayStyle.None : DisplayStyle.Flex;
        setupSongsList.style.display = songMode ? DisplayStyle.Flex : DisplayStyle.None;
        setupSongSearchRoot.style.display = songMode ? DisplayStyle.Flex : DisplayStyle.None;
        setupSelectAllGroupsButton.style.display = songMode ? DisplayStyle.None : DisplayStyle.Flex;

        setupLevelsList.Clear();
        if (snapshot.levels != null && snapshot.levels.Count > 0)
        {
            for (int i = 0; i < snapshot.levels.Count; i++)
                setupLevelsList.Add(CreateSetupLevelRow(snapshot.levels[i]));
        }
        else
        {
            setupLevelsList.Add(CreateSetupEmptyLabel("No levels are available."));
        }

        setupGroupsList.Clear();
        if (snapshot.groups != null)
        {
            for (int i = 0; i < snapshot.groups.Count; i++)
                setupGroupsList.Add(CreateSetupGroupRow(snapshot.groups[i]));
        }

        setupSongsList.Clear();
        string normalizedSongSearch = NormalizeSetupSearchQuery(setupSongSearchQuery);
        int visibleSongCount = 0;
        if (snapshot.songs != null)
        {
            for (int i = 0; i < snapshot.songs.Count; i++)
            {
                FightClubSongChordSourceSnapshot song = snapshot.songs[i];
                if (!MatchesSetupSongSearch(song, normalizedSongSearch))
                    continue;

                visibleSongCount++;
                setupSongsList.Add(CreateSetupSongRow(song));
            }
        }

        if (songMode && visibleSongCount == 0)
        {
            setupSongsList.Add(CreateSetupEmptyLabel(string.IsNullOrWhiteSpace(normalizedSongSearch)
                ? "No songs with recognized chords were found."
                : "No songs match this search."));
        }

        setupAvailableList.Clear();
        if (snapshot.availableChords != null && snapshot.availableChords.Count > 0)
        {
            for (int i = 0; i < snapshot.availableChords.Count; i++)
                setupAvailableList.Add(CreateSetupChordRow(snapshot.availableChords[i], true));
        }
        else
        {
            setupAvailableList.Add(CreateSetupEmptyLabel(songMode ? "Select songs with known chord names." : "Select a group or use the full catalog."));
        }

        setupPlayableList.Clear();
        if (arcadeMode)
        {
            setupPlayableList.Add(CreateSetupEmptyLabel("Arcade grows its chord pool automatically."));
        }
        else if (levelMode)
        {
            setupPlayableList.Add(CreateSetupEmptyLabel("Levels mode uses the selected level's chord pool."));
        }
        else if (snapshot.playableChords != null && snapshot.playableChords.Count > 0)
        {
            for (int i = 0; i < snapshot.playableChords.Count; i++)
                setupPlayableList.Add(CreateSetupChordRow(snapshot.playableChords[i], false));
        }
        else
        {
            setupPlayableList.Add(CreateSetupEmptyLabel("Add chords here before starting."));
        }
    }

    private static FightClubSetupMode GetSetupMode(FightClubSetupSnapshot snapshot)
    {
        if (snapshot == null)
            return FightClubSetupMode.Arcade;

        if (snapshot.mode == (int)FightClubSetupMode.Levels)
            return FightClubSetupMode.Levels;
        if (snapshot.mode == (int)FightClubSetupMode.Manual)
            return FightClubSetupMode.Manual;
        return FightClubSetupMode.Arcade;
    }

    private Button CreateSetupGroupRow(FightClubChordGroupSnapshot group)
    {
        string groupId = group?.id ?? string.Empty;
        bool selected = group != null && group.selected;
        Button button = CreateSetupListButton(() => owner?.ToggleFightClubSetupGroupFromUi(groupId), selected);
        button.style.minHeight = 126f;
        Label title = CreateLabel(group?.name ?? "Group", 44f, selected ? new Color(0.56f, 0.95f, 1f, 1f) : Color.white, true, TextAnchor.MiddleLeft, false);
        title.style.unityFontDefinition = modernFontDefinition;
        title.style.whiteSpace = WhiteSpace.NoWrap;
        title.style.overflow = Overflow.Hidden;
        title.style.textOverflow = TextOverflow.Ellipsis;
        Label subtitle = CreateLabel($"{Mathf.Max(0, group?.chordCount ?? 0)} chords", 28f, new Color(0.78f, 0.87f, 0.96f, 0.74f), false, TextAnchor.MiddleLeft, false);
        subtitle.style.unityFontDefinition = modernFontDefinition;
        button.Add(title);
        button.Add(subtitle);
        return button;
    }

    private Button CreateSetupLevelRow(FightClubLevelSnapshot level)
    {
        int index = Mathf.Max(0, level?.index ?? 0);
        bool unlocked = level == null || level.unlocked;
        bool selected = level != null && level.selected;
        Button button = new Button(() =>
        {
            if (unlocked)
                owner?.SelectFightClubSetupLevelFromUi(index);
        })
        {
            text = string.Empty
        };
        button.focusable = false;
        button.SetEnabled(true);
        button.style.minHeight = 136f;
        button.style.marginBottom = 12f;
        button.style.paddingLeft = 26f;
        button.style.paddingRight = 34f;
        button.style.paddingTop = 0f;
        button.style.paddingBottom = 0f;
        SetRadius(button, 9f);
        StyleSetupLevelRow(button, selected, unlocked);

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.height = Length.Percent(100f);
        row.style.minWidth = 0f;

        VisualElement accent = new VisualElement();
        accent.style.width = 7f;
        accent.style.minWidth = 7f;
        accent.style.height = 82f;
        accent.style.marginRight = 34f;
        accent.style.backgroundColor = selected
            ? new Color(0.06f, 0.92f, 1f, 1f)
            : new Color(0.06f, 0.92f, 1f, unlocked ? 0.18f : 0.08f);
        SetRadius(accent, 4f);

        Label number = CreateLabel((index + 1).ToString("00", CultureInfo.InvariantCulture), 50f, selected ? new Color(0.12f, 0.94f, 1f, 1f) : new Color(0.62f, 0.68f, 0.80f, unlocked ? 0.82f : 0.60f), true, TextAnchor.MiddleCenter, false);
        number.style.unityFontDefinition = modernFontDefinition;
        number.style.width = 84f;
        number.style.minWidth = 84f;

        VisualElement separator = new VisualElement();
        separator.style.width = 2f;
        separator.style.minWidth = 2f;
        separator.style.height = 74f;
        separator.style.marginLeft = 26f;
        separator.style.marginRight = 44f;
        separator.style.backgroundColor = new Color(0.60f, 0.72f, 0.90f, selected ? 0.34f : 0.18f);

        VisualElement textStack = new VisualElement();
        textStack.style.flexGrow = 1f;
        textStack.style.minWidth = 0f;
        Label title = CreateLabel(level?.name ?? "Level", 39f, unlocked ? Color.white : new Color(0.70f, 0.76f, 0.86f, 0.66f), true, TextAnchor.MiddleLeft, false);
        title.style.unityFontDefinition = modernFontDefinition;
        title.style.whiteSpace = WhiteSpace.NoWrap;
        title.style.overflow = Overflow.Hidden;
        title.style.textOverflow = TextOverflow.Ellipsis;

        Label description = CreateLabel(level?.subtitle ?? string.Empty, 27f, unlocked ? new Color(0.78f, 0.84f, 0.92f, 0.68f) : new Color(0.66f, 0.72f, 0.82f, 0.48f), false, TextAnchor.MiddleLeft, false);
        description.style.unityFontDefinition = modernFontDefinition;
        description.style.whiteSpace = WhiteSpace.NoWrap;
        description.style.overflow = Overflow.Hidden;
        description.style.textOverflow = TextOverflow.Ellipsis;
        description.style.marginTop = 7f;

        textStack.Add(title);
        textStack.Add(description);

        string statusText = unlocked
            ? Mathf.Max(0, level?.highScore ?? 0) > 0
                ? $"Best {Mathf.Max(0, level?.highScore ?? 0).ToString("N0", CultureInfo.InvariantCulture)}"
                : $"{Mathf.Max(0, level?.chordCount ?? 0).ToString(CultureInfo.InvariantCulture)} chords"
            : $"Need {Mathf.Max(1, level?.unlockScore ?? 0).ToString(CultureInfo.InvariantCulture)}";
        Label status = CreateLabel(statusText, 28f, unlocked ? new Color(0.12f, 0.94f, 1f, 1f) : new Color(1f, 0.66f, 0.30f, 0.95f), true, TextAnchor.MiddleCenter, false);
        status.style.unityFontDefinition = modernFontDefinition;
        status.style.width = 250f;
        status.style.minWidth = 250f;
        status.style.height = 60f;
        status.style.marginLeft = 38f;
        status.style.backgroundColor = new Color(0.02f, 0.05f, 0.10f, 0.54f);
        SetRadius(status, 5f);
        SetBorder(status, unlocked ? new Color(0.08f, 0.90f, 1f, 0.26f) : new Color(1f, 0.62f, 0.24f, 0.24f), 1f);

        row.Add(accent);
        row.Add(number);
        row.Add(separator);
        row.Add(textStack);
        row.Add(status);
        button.Add(row);
        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            if (unlocked)
            {
                button.style.scale = new Scale(new Vector3(1.004f, 1.004f, 1f));
                if (!selected)
                    StyleSetupLevelRow(button, false, true, true);
            }
        });
        button.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            button.style.scale = new Scale(Vector3.one);
            StyleSetupLevelRow(button, selected, unlocked);
        });
        return button;
    }

    private VisualElement CreateSetupLevelIcon(bool unlocked, bool selected)
    {
        Color color = selected
            ? new Color(0.10f, 0.94f, 1f, 0.96f)
            : new Color(0.72f, 0.78f, 0.90f, unlocked ? 0.70f : 0.56f);

        VisualElement root = new VisualElement();
        root.style.width = 78f;
        root.style.minWidth = 78f;
        root.style.height = 72f;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;

        if (unlocked)
        {
            VisualElement grid = new VisualElement();
            grid.style.position = Position.Relative;
            grid.style.width = 52f;
            grid.style.height = 58f;
            SetRadius(grid, 2f);
            SetBorder(grid, color, 1.5f);

            for (int i = 1; i < 4; i++)
                grid.Add(CreateSetupIconLine(true, i / 4f, color));

            for (int i = 1; i < 5; i++)
                grid.Add(CreateSetupIconLine(false, i / 5f, color));

            root.Add(grid);
            return root;
        }

        VisualElement lockRoot = new VisualElement();
        lockRoot.style.position = Position.Relative;
        lockRoot.style.width = 52f;
        lockRoot.style.height = 58f;

        VisualElement shackle = new VisualElement();
        shackle.style.position = Position.Absolute;
        shackle.style.left = 13f;
        shackle.style.top = 3f;
        shackle.style.width = 26f;
        shackle.style.height = 28f;
        shackle.style.borderTopWidth = 3f;
        shackle.style.borderLeftWidth = 3f;
        shackle.style.borderRightWidth = 3f;
        shackle.style.borderTopColor = color;
        shackle.style.borderLeftColor = color;
        shackle.style.borderRightColor = color;
        SetRadius(shackle, 8f);

        VisualElement body = new VisualElement();
        body.style.position = Position.Absolute;
        body.style.left = 6f;
        body.style.right = 6f;
        body.style.bottom = 4f;
        body.style.height = 34f;
        body.style.backgroundColor = new Color(0.02f, 0.04f, 0.08f, 0.22f);
        SetRadius(body, 4f);
        SetBorder(body, color, 2f);

        lockRoot.Add(shackle);
        lockRoot.Add(body);
        root.Add(lockRoot);
        return root;
    }

    private static VisualElement CreateSetupIconLine(bool vertical, float t, Color color)
    {
        VisualElement line = new VisualElement();
        line.style.position = Position.Absolute;
        line.style.backgroundColor = color;
        if (vertical)
        {
            line.style.top = 0f;
            line.style.bottom = 0f;
            line.style.width = 1.5f;
            line.style.left = Length.Percent(Mathf.Clamp01(t) * 100f);
        }
        else
        {
            line.style.left = 0f;
            line.style.right = 0f;
            line.style.height = 1.5f;
            line.style.top = Length.Percent(Mathf.Clamp01(t) * 100f);
        }

        return line;
    }

    private static void StyleSetupLevelRow(Button button, bool selected, bool unlocked, bool hovered = false)
    {
        if (button == null)
            return;

        button.style.backgroundColor = selected
            ? new Color(0.010f, 0.065f, 0.105f, 0.82f)
            : hovered
                ? new Color(0.012f, 0.044f, 0.075f, 0.62f)
                : new Color(0.006f, 0.018f, 0.034f, unlocked ? 0.42f : 0.30f);
        button.style.opacity = unlocked ? 1f : 0.82f;
        SetBorder(
            button,
            selected
                ? new Color(0.06f, 0.88f, 1f, 0.88f)
                : new Color(0.55f, 0.68f, 0.86f, hovered ? 0.26f : 0.11f),
            selected ? 1.5f : 1f);
    }

    private Button CreateSetupSongRow(FightClubSongChordSourceSnapshot song)
    {
        string songKey = song?.songKey ?? string.Empty;
        bool selected = song != null && song.selected;
        Button button = CreateSetupListButton(() => owner?.ToggleFightClubSetupSongFromUi(songKey), selected, showUnselectedBorder: false);
        button.style.minHeight = 184f;
        button.style.flexDirection = FlexDirection.Row;
        button.style.alignItems = Align.Center;
        button.style.paddingTop = 18f;
        button.style.paddingBottom = 18f;
        button.Add(CreateSetupSongArtworkElement(song?.artworkPath, song?.displayName));

        VisualElement textColumn = new VisualElement();
        textColumn.style.flexGrow = 1f;
        textColumn.style.flexShrink = 1f;
        textColumn.style.minWidth = 0f;
        Label title = CreateLabel(song?.displayName ?? "Song", 44f, selected ? new Color(0.56f, 0.95f, 1f, 1f) : Color.white, true, TextAnchor.MiddleLeft, false);
        title.style.unityFontDefinition = modernFontDefinition;
        title.style.whiteSpace = WhiteSpace.NoWrap;
        title.style.overflow = Overflow.Hidden;
        title.style.textOverflow = TextOverflow.Ellipsis;
        Label subtitle = CreateLabel(
            selected
                ? $"{Mathf.Max(0, song?.matchedChordCount ?? 0)} matched chords"
                : song?.artist ?? string.Empty,
            31f,
            new Color(0.78f, 0.87f, 0.96f, 0.74f),
            false,
            TextAnchor.MiddleLeft,
            false);
        subtitle.style.unityFontDefinition = modernFontDefinition;
        subtitle.style.whiteSpace = WhiteSpace.NoWrap;
        subtitle.style.overflow = Overflow.Hidden;
        subtitle.style.textOverflow = TextOverflow.Ellipsis;
        textColumn.Add(title);
        textColumn.Add(subtitle);
        button.Add(textColumn);
        return button;
    }

    private Button CreateSetupChordRow(FightClubChordOptionSnapshot chord, bool availableList)
    {
        string chordId = chord?.id ?? string.Empty;
        bool selected = chord != null && chord.selected;
        Button button = CreateSetupListButton(
            () =>
            {
                if (availableList)
                    owner?.ToggleFightClubSetupChordFromUi(chordId);
                else
                    owner?.RemoveFightClubPlayableChordFromUi(chordId);
            },
            selected);
        button.style.minHeight = 118f;

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;

        Label mark = CreateLabel(availableList ? (selected ? "ON" : "+") : "x", 34f, selected ? new Color(0.44f, 1f, 0.72f, 1f) : new Color(0.83f, 0.92f, 1f, 0.82f), true, TextAnchor.MiddleCenter, false);
        mark.style.unityFontDefinition = modernFontDefinition;
        mark.style.width = 70f;
        mark.style.marginRight = 20f;

        VisualElement textStack = new VisualElement();
        textStack.style.flexGrow = 1f;
        Label title = CreateLabel(chord?.name ?? "Chord", 44f, selected ? new Color(0.56f, 0.95f, 1f, 1f) : Color.white, true, TextAnchor.MiddleLeft, false);
        title.style.unityFontDefinition = modernFontDefinition;
        title.style.whiteSpace = WhiteSpace.NoWrap;
        title.style.overflow = Overflow.Hidden;
        title.style.textOverflow = TextOverflow.Ellipsis;
        Label subtitle = CreateLabel($"{chord?.sourceLabel ?? string.Empty} - Level {Mathf.Max(1, chord?.difficulty ?? 1)}", 27f, new Color(0.78f, 0.87f, 0.96f, 0.66f), false, TextAnchor.MiddleLeft, false);
        subtitle.style.unityFontDefinition = modernFontDefinition;
        textStack.Add(title);
        textStack.Add(subtitle);

        row.Add(mark);
        row.Add(textStack);
        button.Add(row);
        return button;
    }

    private Button CreateSetupListButton(Action action, bool selected, bool showUnselectedBorder = true)
    {
        Button button = new Button(() => action?.Invoke()) { text = string.Empty };
        button.focusable = false;
        button.style.minHeight = 118f;
        button.style.marginBottom = 14f;
        button.style.paddingLeft = 28f;
        button.style.paddingRight = 28f;
        button.style.paddingTop = 16f;
        button.style.paddingBottom = 16f;
        button.style.backgroundColor = selected ? new Color(0.08f, 0.28f, 0.34f, 0.38f) : new Color(0.02f, 0.05f, 0.08f, 0.22f);
        SetRadius(button, 14f);
        SetBorder(
            button,
            selected
                ? new Color(0.46f, 0.94f, 1f, 0.78f)
                : showUnselectedBorder
                    ? new Color(0.75f, 0.86f, 1f, 0.16f)
                    : new Color(0f, 0f, 0f, 0f),
            selected ? 3f : 2f);
        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            button.style.scale = new Scale(new Vector3(1.018f, 1.018f, 1f));
            if (!selected)
                button.style.backgroundColor = new Color(0.08f, 0.16f, 0.24f, 0.44f);
        });
        button.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            button.style.scale = new Scale(Vector3.one);
            if (!selected)
                button.style.backgroundColor = new Color(0.02f, 0.05f, 0.08f, 0.22f);
        });
        return button;
    }

    private VisualElement CreateSetupSongSearchField(string placeholderText, out TextField searchField, out Label placeholderLabel)
    {
        VisualElement root = new VisualElement();
        root.style.position = Position.Relative;
        root.style.height = 82f;
        root.style.flexGrow = 1f;
        root.style.flexShrink = 1f;
        root.style.minWidth = 0f;
        root.style.marginLeft = 36f;
        root.style.marginBottom = 0f;
        root.style.borderBottomWidth = 3f;
        root.style.borderBottomColor = new Color(1f, 1f, 1f, 0.30f);

        TextField field = new TextField();
        searchField = field;
        field.isDelayed = false;
        field.style.position = Position.Absolute;
        field.style.left = 0f;
        field.style.right = 0f;
        field.style.top = 0f;
        field.style.bottom = 0f;
        field.style.height = 82f;
        field.style.minHeight = 82f;
        field.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        field.style.color = Color.white;
        field.style.borderTopWidth = 0f;
        field.style.borderRightWidth = 0f;
        field.style.borderBottomWidth = 0f;
        field.style.borderLeftWidth = 0f;
        field.style.paddingLeft = 0f;
        field.style.paddingRight = 0f;
        field.style.unityFontDefinition = modernFontDefinition;
        field.RegisterCallback<AttachToPanelEvent>(_ => ApplySetupSearchFieldStyle(field, 34f));

        Label label = new Label(placeholderText);
        placeholderLabel = label;
        label.pickingMode = PickingMode.Ignore;
        label.style.position = Position.Absolute;
        label.style.left = 0f;
        label.style.right = 0f;
        label.style.top = 0f;
        label.style.bottom = 0f;
        label.style.color = Color.white;
        label.style.fontSize = 34f;
        label.style.unityFontDefinition = modernFontDefinition;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.opacity = 0.64f;

        void Refresh()
        {
            bool hasValue = !string.IsNullOrWhiteSpace(field.value);
            bool focused = IsSetupTextFieldFocused(field);
            label.style.display = hasValue || focused ? DisplayStyle.None : DisplayStyle.Flex;
            root.style.borderBottomColor = focused
                ? new Color(0.56f, 0.95f, 1f, 0.95f)
                : new Color(1f, 1f, 1f, 0.30f);
        }

        field.RegisterCallback<FocusInEvent>(_ =>
        {
            owner?.SetMiniGameTextInputFocusedFromUi(true);
            Refresh();
        });
        field.RegisterCallback<FocusOutEvent>(_ =>
        {
            owner?.SetMiniGameTextInputFocusedFromUi(false);
            Refresh();
        });
        field.RegisterCallback<MouseEnterEvent>(_ =>
        {
            if (!IsSetupTextFieldFocused(field))
                root.style.borderBottomColor = new Color(1f, 1f, 1f, 0.48f);
        });
        field.RegisterCallback<MouseLeaveEvent>(_ => Refresh());
        field.RegisterCallback<KeyDownEvent>(evt => evt.StopPropagation());
        field.RegisterCallback<KeyUpEvent>(evt => evt.StopPropagation());
        field.RegisterValueChangedCallback(evt =>
        {
            setupSongSearchQuery = evt.newValue ?? string.Empty;
            Refresh();
            if (currentSetupSnapshot != null)
            {
                lastSetupSignature = string.Empty;
                RebuildSetupScreen(currentSetupSnapshot);
            }
        });

        root.Add(field);
        root.Add(label);
        root.RegisterCallback<AttachToPanelEvent>(_ => Refresh());
        return root;
    }

    private static void ApplySetupSearchFieldStyle(TextField searchField, float fontSize)
    {
        if (searchField == null)
            return;

        searchField.style.color = Color.white;
        searchField.style.fontSize = fontSize;

        VisualElement textInputElement =
            searchField.Q(className: TextInputBaseField<string>.textInputUssName)
            ?? searchField.Q(className: "unity-text-field__input")
            ?? searchField.Q(className: "unity-base-text-field__input")
            ?? searchField.Q(className: "unity-base-field__input");

        if (textInputElement != null)
        {
            textInputElement.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            textInputElement.style.color = Color.white;
            textInputElement.style.fontSize = fontSize;
            textInputElement.style.unityTextAlign = TextAnchor.MiddleLeft;
            textInputElement.style.borderTopWidth = 0f;
            textInputElement.style.borderRightWidth = 0f;
            textInputElement.style.borderBottomWidth = 0f;
            textInputElement.style.borderLeftWidth = 0f;
        }
    }

    private static bool IsSetupTextFieldFocused(TextField field)
    {
        if (field == null || field.panel == null)
            return false;

        Focusable focusedElement = field.panel.focusController?.focusedElement;
        if (focusedElement == null)
            return false;

        if (ReferenceEquals(focusedElement, field))
            return true;

        return focusedElement is VisualElement focusedVisual && field.Contains(focusedVisual);
    }

    private static string NormalizeSetupSearchQuery(string query)
    {
        return string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
    }

    private static bool MatchesSetupSongSearch(FightClubSongChordSourceSnapshot song, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        if (song == null)
            return false;

        return ContainsSetupSearch(song.displayName, query)
            || ContainsSetupSearch(song.artist, query)
            || ContainsSetupSearch(song.songKey, query);
    }

    private static bool ContainsSetupSearch(string value, string query)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private Button CreateSetupModeButton(string title, string subtitle, Action action)
    {
        Button button = new Button(() => action?.Invoke()) { text = string.Empty };
        button.focusable = false;
        button.style.flexGrow = 1f;
        button.style.flexBasis = 0f;
        button.style.height = 148f;
        button.style.paddingLeft = 2f;
        button.style.paddingRight = 2f;
        button.style.paddingTop = 2f;
        button.style.paddingBottom = 2f;
        button.style.marginLeft = 0f;
        button.style.marginRight = 0f;
        button.style.justifyContent = Justify.Center;
        button.style.alignItems = Align.Stretch;
        button.style.overflow = Overflow.Hidden;
        SetRadius(button, 16f);

        VisualElement inner = new VisualElement { name = "setup-mode-inner" };
        inner.style.flexGrow = 1f;
        inner.style.flexDirection = FlexDirection.Row;
        inner.style.justifyContent = Justify.FlexStart;
        inner.style.alignItems = Align.Center;
        inner.style.paddingLeft = 30f;
        inner.style.paddingRight = 30f;
        inner.style.paddingTop = 18f;
        inner.style.paddingBottom = 18f;
        SetRadius(inner, 12f);

        VisualElement column = new VisualElement();
        column.style.flexDirection = FlexDirection.Column;
        column.style.justifyContent = Justify.Center;
        column.style.alignItems = Align.FlexStart;
        column.style.flexGrow = 1f;
        column.style.minWidth = 0f;

        Label titleLabel = CreateLabel(title, 44f, Color.white, true, TextAnchor.MiddleLeft, false);
        titleLabel.name = "setup-mode-title";
        titleLabel.style.unityFontDefinition = modernFontDefinition;
        titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        titleLabel.style.overflow = Overflow.Hidden;
        titleLabel.style.textOverflow = TextOverflow.Ellipsis;
        titleLabel.style.marginBottom = 9f;

        Label subtitleLabel = CreateLabel(subtitle ?? string.Empty, 25f, new Color(0.80f, 0.88f, 0.96f, 0.86f), false, TextAnchor.MiddleLeft, false);
        subtitleLabel.name = "setup-mode-subtitle";
        subtitleLabel.style.unityFontDefinition = modernFontDefinition;
        subtitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        subtitleLabel.style.overflow = Overflow.Hidden;
        subtitleLabel.style.textOverflow = TextOverflow.Ellipsis;

        column.Add(titleLabel);
        column.Add(subtitleLabel);
        inner.Add(column);
        button.Add(inner);
        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            button.style.scale = new Scale(new Vector3(1.018f, 1.018f, 1f));
            button.style.opacity = 1f;
        });
        button.RegisterCallback<MouseLeaveEvent>(_ => button.style.scale = new Scale(Vector3.one));
        button.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            bool isSelected = button.userData is bool selectedState && selectedState;
            button.style.opacity = isSelected ? 1f : 0.94f;
        });
        StyleSetupModeButton(button, false);
        return button;
    }

    private void StyleSetupModeButton(Button button, bool selected)
    {
        if (button == null)
            return;

        button.userData = selected;
        VisualElement inner = button.Q<VisualElement>("setup-mode-inner");
        Label titleLabel = button.Q<Label>("setup-mode-title");
        Label subtitleLabel = button.Q<Label>("setup-mode-subtitle");

        button.style.backgroundImage = new StyleBackground(selected ? GetSetupPrimaryGradientTexture() : GetSetupModeInactiveBorderTexture());
        button.style.backgroundColor = selected ? new Color(0.14f, 0.72f, 0.86f, 0.96f) : new Color(1f, 0.42f, 0.12f, 0.88f);
        button.style.opacity = selected ? 1f : 0.94f;
        SetBorder(button, selected ? new Color(0.72f, 1f, 0.98f, 0.90f) : new Color(1f, 0.62f, 0.22f, 0.74f), selected ? 1.25f : 1f);

        if (inner != null)
        {
            inner.style.backgroundImage = selected ? new StyleBackground(GetSetupModeGradientTexture()) : StyleKeyword.None;
            inner.style.backgroundColor = selected ? new Color(0.02f, 0.16f, 0.23f, 0.86f) : new Color(0.018f, 0.016f, 0.026f, 0.92f);
            SetBorder(inner, selected ? new Color(0.82f, 1f, 0.96f, 0.18f) : new Color(1f, 0.55f, 0.20f, 0.16f), 0.5f);
        }

        if (titleLabel != null)
            titleLabel.style.color = selected ? Color.white : new Color(1f, 0.92f, 0.82f, 0.96f);
        if (subtitleLabel != null)
            subtitleLabel.style.color = selected ? new Color(0.78f, 0.96f, 1f, 0.88f) : new Color(1f, 0.72f, 0.46f, 0.78f);
    }

    private Button CreateSetupPrimaryButton(string text, Action action)
    {
        Button button = new Button(() => action?.Invoke()) { text = text };
        button.focusable = false;
        button.style.width = 900f;
        button.style.height = 106f;
        button.style.alignSelf = Align.Center;
        button.style.unityFontDefinition = modernFontDefinition;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.fontSize = 38f;
        button.style.letterSpacing = 0f;
        button.style.marginTop = 6f;
        button.style.marginBottom = 0f;
        SetRadius(button, 12f);
        StyleSetupPrimaryButton(button, true);
        button.RegisterCallback<MouseEnterEvent>(_ => button.style.scale = new Scale(new Vector3(1.018f, 1.018f, 1f)));
        button.RegisterCallback<MouseLeaveEvent>(_ => button.style.scale = new Scale(Vector3.one));
        return button;
    }

    private void StyleSetupPrimaryButton(Button button, bool enabled)
    {
        if (button == null)
            return;

        button.style.backgroundImage = enabled ? new StyleBackground(GetSetupPrimaryGradientTexture()) : StyleKeyword.None;
        button.style.backgroundColor = enabled ? new Color(0.16f, 0.62f, 0.68f, 0.96f) : new Color(0.04f, 0.07f, 0.11f, 0.66f);
        button.style.color = enabled ? Color.white : new Color(0.78f, 0.86f, 0.96f, 0.58f);
        button.style.opacity = enabled ? 1f : 0.62f;
        SetBorder(button, enabled ? new Color(0.76f, 1f, 0.98f, 0.94f) : new Color(0.72f, 0.84f, 1f, 0.2f), enabled ? 2f : 1f);
    }

    private void StyleSetupAddButton(Button button, bool enabled)
    {
        if (button == null)
            return;

        button.style.backgroundImage = enabled ? new StyleBackground(GetSetupPrimaryGradientTexture()) : StyleKeyword.None;
        button.style.backgroundColor = enabled ? new Color(0.12f, 0.48f, 0.56f, 0.9f) : new Color(0.03f, 0.06f, 0.1f, 0.42f);
        button.style.color = enabled ? Color.white : new Color(0.78f, 0.86f, 0.96f, 0.5f);
        button.style.opacity = enabled ? 1f : 0.55f;
        SetBorder(button, enabled ? new Color(0.66f, 1f, 0.95f, 0.9f) : new Color(0.72f, 0.84f, 1f, 0.18f), enabled ? 2f : 1f);
    }

    private VisualElement CreateSetupSongArtworkElement(string artworkPath, string fallbackKey)
    {
        VisualElement artwork = new VisualElement();
        artwork.style.width = 150f;
        artwork.style.minWidth = 150f;
        artwork.style.height = 150f;
        artwork.style.marginRight = 34f;
        artwork.style.backgroundColor = GetSetupDeterministicAccentColor(fallbackKey);
        artwork.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        SetRadius(artwork, 12f);
        SetBorder(artwork, new Color(1f, 1f, 1f, 0.14f), 2f);

        Texture2D texture = GetSetupSongArtworkTexture(artworkPath);
        if (texture != null)
            artwork.style.backgroundImage = new StyleBackground(texture);

        return artwork;
    }

    private static Texture2D GetSetupSongArtworkTexture(string artworkPath)
    {
        if (string.IsNullOrWhiteSpace(artworkPath) || !File.Exists(artworkPath))
            return null;

        if (setupSongArtworkTextureCache.TryGetValue(artworkPath, out Texture2D cached))
            return cached;

        try
        {
            byte[] bytes = File.ReadAllBytes(artworkPath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = $"FightClubSongArtwork_{Path.GetFileNameWithoutExtension(artworkPath)}",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            if (!texture.LoadImage(bytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            setupSongArtworkTextureCache[artworkPath] = texture;
            return texture;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MiniGamesScreenOverlay] Failed to load song artwork '{artworkPath}': {ex.Message}");
            return null;
        }
    }

    private static Color GetSetupDeterministicAccentColor(string key)
    {
        int hash = 17;
        if (!string.IsNullOrEmpty(key))
        {
            for (int i = 0; i < key.Length; i++)
                hash = unchecked(hash * 31 + key[i]);
        }

        float hue = Mathf.Repeat(hash * 0.0137f, 1f);
        return Color.HSVToRGB(hue, 0.52f, 0.62f);
    }

    private static Texture2D GetSetupPrimaryGradientTexture()
    {
        if (setupPrimaryGradientTexture == null)
            setupPrimaryGradientTexture = CreateGradientTexture("FightClubSetupPrimaryGradient", new Color(0.11f, 0.76f, 0.79f, 1f), new Color(0.78f, 0.36f, 0.95f, 1f));

        return setupPrimaryGradientTexture;
    }

    private static Texture2D GetSetupModeGradientTexture()
    {
        if (setupModeGradientTexture == null)
            setupModeGradientTexture = CreateGradientTexture("FightClubSetupModeGradient", new Color(0.02f, 0.34f, 0.42f, 0.96f), new Color(0.23f, 0.12f, 0.48f, 0.96f));

        return setupModeGradientTexture;
    }

    private static Texture2D GetSetupModeInactiveBorderTexture()
    {
        if (setupModeInactiveBorderTexture == null)
            setupModeInactiveBorderTexture = CreateGradientTexture("FightClubSetupModeInactiveBorderGradient", new Color(1f, 0.62f, 0.16f, 0.96f), new Color(1f, 0.22f, 0.34f, 0.92f));

        return setupModeInactiveBorderTexture;
    }

    private static Texture2D CreateGradientTexture(string name, Color left, Color right)
    {
        const int width = 64;
        Texture2D texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int x = 0; x < width; x++)
        {
            float t = x / (width - 1f);
            texture.SetPixel(x, 0, Color.Lerp(left, right, t));
        }

        texture.Apply(false, true);
        return texture;
    }

    private Label CreateSetupEmptyLabel(string text)
    {
        Label label = CreateLabel(text, 46f, new Color(0.78f, 0.87f, 0.96f, 0.76f), false, TextAnchor.MiddleCenter, false);
        label.style.unityFontDefinition = modernFontDefinition;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.paddingLeft = 40f;
        label.style.paddingRight = 40f;
        label.style.paddingTop = 50f;
        label.style.paddingBottom = 50f;
        return label;
    }

    private void UpdateRunSettingsScreen(FightClubRunSettingsSnapshot snapshot)
    {
        if (snapshot == null)
            return;

        runSettingsTitleLabel.text = snapshot.activeRun ? "Run Settings" : "Run Setup";
        runSettingsSubtitleLabel.text = snapshot.activeRun
            ? "These settings apply only to Fight Club and do not change normal gameplay."
            : "Set the rules for this Fight Club run.";
        runSettingsLeniencyValueLabel.text = snapshot.chordLeniencyLabel ?? "Normal";
        runSettingsLeniencyDescriptionLabel.text = snapshot.chordLeniencyDescription ?? string.Empty;
        int bpm = Mathf.Clamp(snapshot.tempoBpm > 0 ? snapshot.tempoBpm : Mathf.RoundToInt(60f / Mathf.Clamp(snapshot.beatIntervalSeconds, 0.66f, 2.5f)), FightClubRunSettings.MinTempoBpm, FightClubRunSettings.MaxTempoBpm);
        runSettingsBeatValueLabel.text = $"{bpm.ToString(CultureInfo.InvariantCulture)} BPM";
        runSettingsMetronomeSoundValueLabel.text = snapshot.metronomeSoundLabel ?? "Drums";
        runSettingsChordInstrumentValueLabel.text = snapshot.chordPreviewInstrumentLabel ?? "Electric";
        runSettingsCountdownValueLabel.text = $"{FightClubRunSettings.NormalizeCountdownBeats(snapshot.countdownSeconds).ToString(CultureInfo.InvariantCulture)} beats";
        runSettingsChordCountValueLabel.text = $"{FightClubRunSettings.NormalizeChordCount(snapshot.chordCount).ToString(CultureInfo.InvariantCulture)} chords";
        runSettingsPracticeModeValueLabel.text = snapshot.practiceMode ? "ON" : "OFF";
        runSettingsShowMissedNotesValueLabel.text = snapshot.showMissedNotes ? "ON" : "OFF";
        runSettingsFailsValueLabel.text = Mathf.Clamp(snapshot.maxFailedRounds, 1, 10).ToString(CultureInfo.InvariantCulture);
        runSettingsPrimaryButton.text = snapshot.activeRun ? "Apply" : "Start Fight Club";
        runSettingsPrimaryButton.SetEnabled(snapshot.activeRun || snapshot.canStart);
        StyleSetupPrimaryButton(runSettingsPrimaryButton, snapshot.activeRun || snapshot.canStart);
    }

    private VisualElement CreateRunSettingRow(
        string title,
        string subtitle,
        out Label valueLabel,
        Action decreaseAction,
        Action increaseAction)
    {
        return CreateRunSettingRow(title, subtitle, out valueLabel, decreaseAction, increaseAction, out _);
    }

    private VisualElement CreateRunSettingRow(
        string title,
        string subtitle,
        out Label valueLabel,
        Action decreaseAction,
        Action increaseAction,
        out Label descriptionLabel)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.minHeight = 112f;
        row.style.marginBottom = 16f;
        row.style.paddingLeft = 28f;
        row.style.paddingRight = 24f;
        row.style.paddingTop = 18f;
        row.style.paddingBottom = 18f;
        row.style.backgroundColor = new Color(0.02f, 0.05f, 0.09f, 0.50f);
        SetRadius(row, 12f);
        SetBorder(row, new Color(0.66f, 0.82f, 1f, 0.18f), 1f);

        VisualElement textStack = new VisualElement();
        textStack.style.flexGrow = 1f;
        textStack.style.flexShrink = 1f;
        textStack.style.minWidth = 0f;
        Label titleLabel = CreateLabel(title, 34f, Color.white, true, TextAnchor.MiddleLeft, false);
        titleLabel.style.unityFontDefinition = modernFontDefinition;
        Label subtitleLabel = CreateLabel(subtitle, 23f, new Color(0.76f, 0.85f, 0.96f, 0.72f), false, TextAnchor.MiddleLeft, false);
        subtitleLabel.style.unityFontDefinition = modernFontDefinition;
        subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
        subtitleLabel.style.marginTop = 4f;
        textStack.Add(titleLabel);
        textStack.Add(subtitleLabel);
        descriptionLabel = subtitleLabel;

        Button minus = CreateRunSettingStepButton("-", decreaseAction);
        Button plus = CreateRunSettingStepButton("+", increaseAction);

        valueLabel = CreateLabel("--", 36f, new Color(0.54f, 0.96f, 1f, 1f), true, TextAnchor.MiddleCenter, false);
        valueLabel.style.unityFontDefinition = modernFontDefinition;
        valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        valueLabel.style.width = 190f;
        valueLabel.style.minWidth = 190f;

        row.Add(textStack);
        row.Add(minus);
        row.Add(valueLabel);
        row.Add(plus);
        return row;
    }

    private Button CreateRunSettingStepButton(string text, Action action)
    {
        Button button = new Button(() => action?.Invoke()) { text = text };
        button.focusable = false;
        button.style.width = 58f;
        button.style.height = 58f;
        button.style.marginLeft = 8f;
        button.style.marginRight = 8f;
        button.style.unityFontDefinition = modernFontDefinition;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.fontSize = 30f;
        button.style.color = Color.white;
        button.style.backgroundColor = new Color(0.02f, 0.05f, 0.10f, 0.58f);
        SetRadius(button, 10f);
        SetBorder(button, new Color(0.60f, 0.86f, 1f, 0.34f), 1.5f);
        button.RegisterCallback<MouseEnterEvent>(_ => button.style.scale = new Scale(new Vector3(1.08f, 1.08f, 1f)));
        button.RegisterCallback<MouseLeaveEvent>(_ => button.style.scale = new Scale(Vector3.one));
        return button;
    }

    private void UpdateFightHud(bool active, FightClubMiniGameSnapshot snapshot)
    {
        if (!active || snapshot == null)
            return;

        fightTitleLabel.text = "Fight Club";
        fightScoreLabel.text = snapshot.score.ToString("N0", CultureInfo.InvariantCulture);
        fightScoreDetailsLabel.text =
            $"Round {Mathf.Max(1, snapshot.round).ToString(CultureInfo.InvariantCulture)}  •  " +
            $"Streak {Mathf.Max(0, snapshot.streak).ToString(CultureInfo.InvariantCulture)}  •  " +
            $"Failed {Mathf.Max(0, snapshot.failedRounds).ToString(CultureInfo.InvariantCulture)}/{Mathf.Max(1, snapshot.maxFailedRounds).ToString(CultureInfo.InvariantCulture)}";
        if (snapshot.practiceMode)
        {
            fightScoreDetailsLabel.text =
                $"Round {Mathf.Max(1, snapshot.round).ToString(CultureInfo.InvariantCulture)}  |  Practice  |  Streak {Mathf.Max(0, snapshot.streak).ToString(CultureInfo.InvariantCulture)}";
        }

        string countdownText = string.IsNullOrWhiteSpace(snapshot.countdownLabel) ? string.Empty : snapshot.countdownLabel;
        fightCountdownLabel.text = countdownText;
        bool bannerPrompt = countdownText.Length > 1;
        float countdownPulse = Mathf.Sin(Mathf.Clamp01(snapshot.beatProgress01) * Mathf.PI);
        float countdownScale = string.IsNullOrEmpty(countdownText)
            ? 1f
            : bannerPrompt
                ? Mathf.Lerp(0.92f, 1.04f, countdownPulse)
                : Mathf.Lerp(0.88f, 1.08f, countdownPulse);
        fightCountdownLabel.style.fontSize = bannerPrompt ? 76f : 132f;
        fightCountdownLabel.style.opacity = string.IsNullOrEmpty(countdownText)
            ? 0f
            : bannerPrompt
                ? Mathf.Lerp(0.72f, 1f, countdownPulse)
                : 1f;
        fightCountdownLabel.style.scale = new Scale(new Vector3(countdownScale, countdownScale, 1f));
        fightStatusLabel.text = string.IsNullOrWhiteSpace(snapshot.statusLabel) ? snapshot.phaseLabel : snapshot.statusLabel;
        beatFill.style.width = Length.Percent(Mathf.Clamp01(snapshot.beatProgress01) * 100f);
    }

    private void UpdateFightEnd(bool visible, FightClubMiniGameSnapshot snapshot)
    {
        if (!visible || snapshot == null)
            return;

        int hits = 0;
        int misses = Mathf.Max(0, snapshot.misses);
        List<FightClubChordResultSnapshot> results = snapshot.chordResults ?? new List<FightClubChordResultSnapshot>();
        for (int i = 0; i < results.Count; i++)
        {
            FightClubChordResultSnapshot result = results[i];
            if (result == null)
                continue;

            hits += Mathf.Max(0, result.hits);
        }

        int attempts = hits + misses;
        int accuracy = attempts > 0 ? Mathf.RoundToInt((hits / (float)attempts) * 100f) : 0;
        endTitleLabel.text = snapshot.endedByLoss ? "Game Over" : "Run Complete";
        endScoreLabel.text = snapshot.score.ToString("N0", CultureInfo.InvariantCulture);
        endSubtitleLabel.text =
            $"Round {Mathf.Max(1, snapshot.round).ToString(CultureInfo.InvariantCulture)}  •  " +
            $"Best streak {Mathf.Max(0, snapshot.bestStreak).ToString(CultureInfo.InvariantCulture)}  •  " +
            $"Accuracy {accuracy.ToString(CultureInfo.InvariantCulture)}%";

        float pulse = 0.5f + (0.5f * Mathf.Sin(Time.unscaledTime * 2.0f));
        Color endBorder = snapshot.endedByLoss
            ? Color.Lerp(new Color(1f, 0.36f, 0.34f, 0.62f), new Color(1f, 0.78f, 0.70f, 0.90f), pulse)
            : Color.Lerp(new Color(0.35f, 0.82f, 1f, 0.56f), new Color(0.82f, 0.96f, 1f, 0.92f), pulse);
        SetBorder(endPanel, endBorder, 2.2f);
        endPanel.style.scale = new Scale(new Vector3(1f + (pulse * 0.004f), 1f + (pulse * 0.004f), 1f));
        endScoreLabel.style.color = snapshot.endedByLoss
            ? new Color(1f, 0.55f, 0.48f, 1f)
            : new Color(0.66f, 0.94f, 1f, 1f);

        endStatsGrid.Clear();
        endStatsGrid.Add(CreateEndStatCard(snapshot.highScoreEnabled ? snapshot.highScoreLabel : "Run Score", snapshot.highScoreEnabled ? Mathf.Max(snapshot.highScore, snapshot.score).ToString("N0", CultureInfo.InvariantCulture) : snapshot.score.ToString("N0", CultureInfo.InvariantCulture), new Color(0.62f, 0.92f, 1f, 1f)));
        endStatsGrid.Add(CreateEndStatCard("Hits", hits.ToString("N0", CultureInfo.InvariantCulture), new Color(0.58f, 1f, 0.78f, 1f)));
        endStatsGrid.Add(CreateEndStatCard("Misses", misses.ToString("N0", CultureInfo.InvariantCulture), new Color(1f, 0.50f, 0.46f, 1f)));
        endStatsGrid.Add(CreateEndStatCard("Failed Rounds", $"{Mathf.Max(0, snapshot.failedRounds).ToString(CultureInfo.InvariantCulture)}/{Mathf.Max(1, snapshot.maxFailedRounds).ToString(CultureInfo.InvariantCulture)}", new Color(1f, 0.73f, 0.40f, 1f)));

        endChordResultsList.Clear();
        if (results.Count == 0)
        {
            endChordResultsList.Add(CreateSetupEmptyLabel("No chords were scored."));
            return;
        }

        endChordResultsList.Add(CreateEndChordHeaderRow());
        for (int i = 0; i < results.Count; i++)
            endChordResultsList.Add(CreateEndChordResultRow(results[i]));
    }

    private VisualElement CreateEndStatCard(string labelText, string valueText, Color accent)
    {
        VisualElement card = new VisualElement();
        card.style.flexGrow = 1f;
        card.style.minWidth = 0f;
        card.style.marginLeft = 6f;
        card.style.marginRight = 6f;
        card.style.paddingTop = 16f;
        card.style.paddingBottom = 16f;
        card.style.paddingLeft = 18f;
        card.style.paddingRight = 18f;
        card.style.alignItems = Align.Center;
        card.style.backgroundColor = new Color(0.02f, 0.05f, 0.09f, 0.56f);
        SetRadius(card, 10f);
        SetBorder(card, new Color(accent.r, accent.g, accent.b, 0.28f), 1.5f);

        Label value = CreateLabel(valueText, 38f, accent, true, TextAnchor.MiddleCenter, false);
        value.style.unityFontDefinition = modernFontDefinition;
        value.style.unityFontStyleAndWeight = FontStyle.Bold;
        Label label = CreateLabel(labelText, 19f, new Color(0.82f, 0.90f, 1f, 0.74f), true, TextAnchor.MiddleCenter, false);
        label.style.unityFontDefinition = modernFontDefinition;
        label.style.marginTop = 4f;
        card.Add(value);
        card.Add(label);
        return card;
    }

    private VisualElement CreateEndChordHeaderRow()
    {
        VisualElement row = CreateEndChordBaseRow();
        row.style.backgroundColor = new Color(0.04f, 0.10f, 0.16f, 0.46f);
        row.Add(CreateEndChordCell("Chord", 28f, Color.white, true, TextAnchor.MiddleLeft, 1f));
        row.Add(CreateEndChordCell("Hits", 28f, new Color(0.58f, 1f, 0.78f, 1f), true, TextAnchor.MiddleCenter, 0f, 110f));
        row.Add(CreateEndChordCell("Misses", 28f, new Color(1f, 0.50f, 0.46f, 1f), true, TextAnchor.MiddleCenter, 0f, 120f));
        row.Add(CreateEndChordCell("Accuracy", 28f, new Color(0.62f, 0.92f, 1f, 1f), true, TextAnchor.MiddleCenter, 0f, 150f));
        return row;
    }

    private VisualElement CreateEndChordResultRow(FightClubChordResultSnapshot result)
    {
        int hits = Mathf.Max(0, result?.hits ?? 0);
        int misses = Mathf.Max(0, result?.misses ?? 0);
        int attempts = hits + misses;
        int accuracy = attempts > 0 ? Mathf.RoundToInt((hits / (float)attempts) * 100f) : 0;

        VisualElement row = CreateEndChordBaseRow();
        row.Add(CreateEndChordCell(result?.name ?? "Chord", 31f, Color.white, true, TextAnchor.MiddleLeft, 1f));
        row.Add(CreateEndChordCell(hits.ToString(CultureInfo.InvariantCulture), 31f, new Color(0.58f, 1f, 0.78f, 1f), true, TextAnchor.MiddleCenter, 0f, 110f));
        row.Add(CreateEndChordCell(misses.ToString(CultureInfo.InvariantCulture), 31f, new Color(1f, 0.50f, 0.46f, 1f), true, TextAnchor.MiddleCenter, 0f, 120f));
        row.Add(CreateEndChordCell($"{accuracy.ToString(CultureInfo.InvariantCulture)}%", 31f, new Color(0.62f, 0.92f, 1f, 1f), true, TextAnchor.MiddleCenter, 0f, 150f));
        return row;
    }

    private static VisualElement CreateEndChordBaseRow()
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.minHeight = 58f;
        row.style.paddingLeft = 20f;
        row.style.paddingRight = 20f;
        row.style.borderBottomWidth = 1f;
        row.style.borderBottomColor = new Color(0.70f, 0.82f, 1f, 0.12f);
        return row;
    }

    private Label CreateEndChordCell(string text, float size, Color color, bool bold, TextAnchor anchor, float grow, float width = 0f)
    {
        Label cell = CreateLabel(text, size, color, bold, anchor, false);
        cell.style.unityFontDefinition = modernFontDefinition;
        cell.style.whiteSpace = WhiteSpace.NoWrap;
        cell.style.overflow = Overflow.Hidden;
        cell.style.textOverflow = TextOverflow.Ellipsis;
        cell.style.flexGrow = grow;
        cell.style.flexShrink = grow > 0f ? 1f : 0f;
        if (width > 0f)
        {
            cell.style.width = width;
            cell.style.minWidth = width;
        }

        return cell;
    }

    private Button CreateEndActionButton(string text, Action action, bool primary)
    {
        Button button = new Button(() => action?.Invoke()) { text = text };
        button.focusable = false;
        button.style.width = primary ? 250f : 210f;
        button.style.height = 68f;
        button.style.marginLeft = 9f;
        button.style.marginRight = 9f;
        button.style.unityFontDefinition = modernFontDefinition;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.fontSize = 27f;
        button.style.color = primary ? Color.white : new Color(0.88f, 0.94f, 1f, 0.94f);
        button.style.backgroundImage = primary ? new StyleBackground(GetSetupPrimaryGradientTexture()) : StyleKeyword.None;
        button.style.backgroundColor = primary ? Color.white : new Color(0.02f, 0.04f, 0.08f, 0.42f);
        SetRadius(button, 10f);
        SetBorder(button, primary ? new Color(0.76f, 1f, 0.98f, 0.88f) : new Color(0.76f, 0.88f, 1f, 0.30f), primary ? 2f : 1f);
        button.RegisterCallback<MouseEnterEvent>(_ => button.style.scale = new Scale(new Vector3(1.045f, 1.045f, 1f)));
        button.RegisterCallback<MouseLeaveEvent>(_ => button.style.scale = new Scale(Vector3.one));
        return button;
    }

    private void UpdatePauseSelection(int selectedIndex)
    {
        for (int i = 0; i < pauseButtons.Count; i++)
            StylePauseButton(pauseButtons[i], i == selectedIndex);
    }

    private Button CreatePauseButton(string text, int index, Action action)
    {
        Button button = new Button(() => action?.Invoke()) { text = text };
        button.focusable = false;
        button.style.height = 128f;
        button.style.marginBottom = 14f;
        button.style.fontSize = 92f;
        button.style.unityFontDefinition = modernFontDefinition;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.color = Color.white;
        button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        SetRadius(button, 0f);
        StylePauseButton(button, false);
        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            owner?.SetMiniGamePauseSelectionFromUi(index);
            button.style.scale = new Scale(new Vector3(1.06f, 1.06f, 1f));
        });
        button.RegisterCallback<MouseLeaveEvent>(_ => button.style.scale = new Scale(Vector3.one));
        return button;
    }

    private VisualElement CreateSetupPanel()
    {
        VisualElement panel = new VisualElement();
        panel.style.paddingLeft = 32f;
        panel.style.paddingRight = 32f;
        panel.style.paddingTop = 30f;
        panel.style.paddingBottom = 30f;
        panel.style.minHeight = 0f;
        panel.style.backgroundColor = new Color(0.004f, 0.014f, 0.030f, 0.78f);
        SetRadius(panel, 14f);
        SetBorder(panel, new Color(0.64f, 0.74f, 0.92f, 0.22f), 1f);
        return panel;
    }

    private ScrollView CreateSetupScrollList(float grow)
    {
        ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = grow;
        scroll.style.flexShrink = 1f;
        scroll.style.minWidth = 0f;
        scroll.style.minHeight = 0f;
        scroll.style.paddingRight = 0f;
        scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        return scroll;
    }

    private Button CreateSetupActionButton(string text, Action action, float width)
    {
        Button button = new Button(() => action?.Invoke()) { text = text };
        button.focusable = false;
        button.style.width = width;
        button.style.height = 82f;
        button.style.marginLeft = 18f;
        button.style.paddingLeft = 24f;
        button.style.paddingRight = 24f;
        button.style.unityFontDefinition = modernFontDefinition;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.fontSize = 32f;
        button.style.letterSpacing = 0f;
        SetRadius(button, 14f);
        StyleSetupButton(button, false);
        button.RegisterCallback<MouseEnterEvent>(_ => button.style.scale = new Scale(new Vector3(1.045f, 1.045f, 1f)));
        button.RegisterCallback<MouseLeaveEvent>(_ => button.style.scale = new Scale(Vector3.one));
        return button;
    }

    private static void StyleSetupButton(Button button, bool selected)
    {
        if (button == null)
            return;

        button.style.backgroundColor = selected ? new Color(0.12f, 0.58f, 0.62f, 0.72f) : new Color(0.02f, 0.05f, 0.09f, 0.34f);
        button.style.color = selected ? Color.white : new Color(0.88f, 0.94f, 1f, 0.92f);
        SetBorder(button, selected ? new Color(0.58f, 0.98f, 1f, 0.96f) : new Color(0.78f, 0.88f, 1f, 0.36f), selected ? 2f : 1f);
    }

    private static void StylePauseButton(Button button, bool selected)
    {
        if (button == null)
            return;

        button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        button.style.color = selected ? new Color(0.63f, 0.93f, 1f, 1f) : new Color(0.94f, 0.96f, 0.98f, 0.88f);
        SetBorder(button, selected ? new Color(0.63f, 0.93f, 1f, 0.95f) : new Color(0f, 0f, 0f, 0f), selected ? 2f : 0f);
    }

    private VisualElement CreateRoot()
    {
        VisualElement root = new VisualElement();
        root.style.position = Position.Absolute;
        root.style.left = 0f;
        root.style.right = 0f;
        root.style.top = 0f;
        root.style.bottom = 0f;
        root.style.display = DisplayStyle.None;
        return root;
    }

    private Label CreateLabel(string text, float size, Color color, bool bold, TextAnchor anchor, bool titleFont)
    {
        Label label = new Label(text);
        label.style.fontSize = size;
        label.style.color = color;
        label.style.unityTextAlign = anchor;
        label.style.unityFontDefinition = titleFont ? titleFontDefinition : bodyFontDefinition;
        if (bold)
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
        return label;
    }

    private static string BuildMenuSignature(MiniGameScreenSnapshot snapshot)
    {
        if (snapshot?.entries == null || snapshot.entries.Count == 0)
            return "empty";

        var parts = new List<string> { snapshot.selectedIndex.ToString() };
        for (int i = 0; i < snapshot.entries.Count; i++)
        {
            MiniGameMenuEntrySnapshot entry = snapshot.entries[i];
            parts.Add($"{entry?.id}:{entry?.highScore}:{entry?.selected}");
        }

        return string.Join("|", parts);
    }

    private string BuildSetupSignature(FightClubSetupSnapshot snapshot)
    {
        if (snapshot == null)
            return "empty";

        var parts = new List<string>
        {
            snapshot.visible.ToString(),
            snapshot.mode.ToString(CultureInfo.InvariantCulture),
            snapshot.arcadeMode.ToString(),
            snapshot.randomMode.ToString(),
            snapshot.sourceMode.ToString(),
            snapshot.canStart.ToString(),
            snapshot.statusLabel ?? string.Empty,
            setupSongSearchQuery ?? string.Empty
        };

        if (snapshot.levels != null)
        {
            for (int i = 0; i < snapshot.levels.Count; i++)
            {
                FightClubLevelSnapshot level = snapshot.levels[i];
                parts.Add($"l:{level?.index}:{level?.unlocked}:{level?.selected}:{level?.chordCount}:{level?.unlockScore}:{level?.highScore}");
            }
        }

        if (snapshot.groups != null)
        {
            for (int i = 0; i < snapshot.groups.Count; i++)
            {
                FightClubChordGroupSnapshot group = snapshot.groups[i];
                parts.Add($"g:{group?.id}:{group?.selected}:{group?.chordCount}");
            }
        }

        if (snapshot.songs != null)
        {
            for (int i = 0; i < snapshot.songs.Count; i++)
            {
                FightClubSongChordSourceSnapshot song = snapshot.songs[i];
                parts.Add($"s:{song?.songKey}:{song?.selected}:{song?.matchedChordCount}:{song?.artworkPath}");
            }
        }

        if (snapshot.availableChords != null)
        {
            for (int i = 0; i < snapshot.availableChords.Count; i++)
            {
                FightClubChordOptionSnapshot chord = snapshot.availableChords[i];
                parts.Add($"a:{chord?.id}:{chord?.selected}");
            }
        }

        if (snapshot.playableChords != null)
        {
            for (int i = 0; i < snapshot.playableChords.Count; i++)
            {
                FightClubChordOptionSnapshot chord = snapshot.playableChords[i];
                parts.Add($"p:{chord?.id}");
            }
        }

        return string.Join("|", parts);
    }

    private static string BuildRunSettingsSignature(FightClubRunSettingsSnapshot snapshot)
    {
        if (snapshot == null)
            return "empty";

        return string.Join("|",
            snapshot.visible,
            snapshot.activeRun,
            snapshot.canStart,
            snapshot.chordLeniencyIndex,
            snapshot.chordLeniencyLabel,
            snapshot.chordLeniencyDescription,
            snapshot.beatIntervalSeconds.ToString("F2", CultureInfo.InvariantCulture),
            snapshot.tempoBpm,
            snapshot.metronomeSoundIndex,
            snapshot.metronomeSoundLabel,
            snapshot.chordPreviewInstrumentIndex,
            snapshot.chordPreviewInstrumentLabel,
            snapshot.countdownSeconds.ToString("F1", CultureInfo.InvariantCulture),
            snapshot.chordCount,
            snapshot.practiceMode,
            snapshot.showMissedNotes,
            snapshot.maxFailedRounds);
    }

    private static void SetBorder(VisualElement element, Color color, float width)
    {
        element.style.borderTopWidth = width;
        element.style.borderRightWidth = width;
        element.style.borderBottomWidth = width;
        element.style.borderLeftWidth = width;
        element.style.borderTopColor = color;
        element.style.borderRightColor = color;
        element.style.borderBottomColor = color;
        element.style.borderLeftColor = color;
    }

    private static void SetRadius(VisualElement element, float radius)
    {
        element.style.borderTopLeftRadius = radius;
        element.style.borderTopRightRadius = radius;
        element.style.borderBottomLeftRadius = radius;
        element.style.borderBottomRightRadius = radius;
    }
}
