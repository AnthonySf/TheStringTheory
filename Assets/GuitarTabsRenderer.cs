using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class GuitarTabsRenderer : IGuitarGameplayRenderer
{
    private readonly Dictionary<int, GameplayNoteState> stateById = new Dictionary<int, GameplayNoteState>();
    private readonly Dictionary<int, TabSectionData> sectionByIndex = new Dictionary<int, TabSectionData>();

    private GuitarBridgeServer owner;
    private Camera mainCamera;
    private GameObject root;
    private TabPanelView topPanel;
    private TabPanelView bottomPanel;
    private GameObject playhead;
    private GameObject loopMarkerStart;
    private GameObject loopMarkerEnd;

    private GameObject pauseMenuRoot;
    private TextMeshPro pauseTitleText;
    private TextMeshPro pauseHelpText;
    private TextMeshPro pauseLoopText;
    private GameObject pauseLoopButton;
    private GameObject speedSliderTrack;
    private GameObject speedSliderFill;
    private GameObject speedSliderKnob;
    private TextMeshPro speedSliderText;
    private GameObject songSelectionButton;
    private TextMeshPro songSelectionButtonText;
    private GameObject songSettingsButton;
    private TextMeshPro songSettingsButtonText;
    private GameObject toneLabButton;
    private TextMeshPro toneLabButtonText;

    private GameObject songSettingsRoot;
    private TextMeshPro songSettingsTitleText;
    private TextMeshPro songSettingsHelpText;
    private TextMeshPro offsetSliderText;
    private GameObject offsetSliderTrack;
    private GameObject offsetSliderFill;
    private GameObject offsetSliderKnob;
    private TextMeshPro tabSpeedOffsetSliderText;
    private GameObject tabSpeedOffsetSliderTrack;
    private GameObject tabSpeedOffsetSliderFill;
    private GameObject tabSpeedOffsetSliderKnob;
    private TextMeshPro songStartDelaySliderText;
    private GameObject songStartDelaySliderTrack;
    private GameObject songStartDelaySliderFill;
    private GameObject songStartDelaySliderKnob;
    private TextMeshPro songStatusText;

    private GameObject songSelectionRoot;
    private TextMeshPro songSelectionTitleText;
    private TextMeshPro songSelectionHelpText;
    private readonly List<TextMeshPro> songSelectionRows = new List<TextMeshPro>();

    private int displayedTopSectionIndex = -999;
    private int displayedBottomSectionIndex = -999;

    private bool isTransitioning;
    private float transitionElapsed;
    private TabPanelView transitionOutgoingPanel;
    private TabPanelView transitionIncomingPanel;
    private int queuedBottomSectionIndex = -1;
    private bool transitionIsReverse;

    private TabsSongHeaderOverlay songHeaderOverlay;

    public void Initialize(GuitarBridgeServer owner, List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        this.owner = owner;
        mainCamera = Camera.main;

        root = new GameObject("TabsRendererRoot");
        topPanel = new TabPanelView(root.transform, "TopTabPanel", owner, true);
        bottomPanel = new TabPanelView(root.transform, "BottomTabPanel", owner, false);

        playhead = GameObject.CreatePrimitive(PrimitiveType.Cube);
        playhead.name = "TabPlayhead";
        playhead.transform.SetParent(root.transform, false);
        playhead.GetComponent<Renderer>().material = CreateGlowMaterial(owner.tabPlayheadColor, 4f);

        CreatePauseMenuVisuals();
        CreateSongSettingsVisuals();
        CreateSongSelectionVisuals();

        songHeaderOverlay = new TabsSongHeaderOverlay(owner);

        loopMarkerStart = GameObject.CreatePrimitive(PrimitiveType.Cube);
        loopMarkerStart.name = "LoopMarkerStart";
        loopMarkerStart.transform.SetParent(root.transform, false);
        loopMarkerStart.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(1f, 0.2f, 0.2f, 0.95f), 4f);

        loopMarkerEnd = GameObject.CreatePrimitive(PrimitiveType.Cube);
        loopMarkerEnd.name = "LoopMarkerEnd";
        loopMarkerEnd.transform.SetParent(root.transform, false);
        loopMarkerEnd.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(1f, 0.2f, 0.2f, 0.95f), 4f);

        RebuildCaches(sections);
        ConfigureCamera();

        displayedTopSectionIndex = -999;
        displayedBottomSectionIndex = -999;
        isTransitioning = false;
        transitionElapsed = 0f;
        transitionIsReverse = false;
    }

    public void ResetRenderer(List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        RebuildCaches(sections);

        displayedTopSectionIndex = -999;
        displayedBottomSectionIndex = -999;
        isTransitioning = false;
        transitionElapsed = 0f;
        queuedBottomSectionIndex = -1;
        transitionIsReverse = false;

        topPanel.ClearAndHide();
        bottomPanel.ClearAndHide();

        SetPanelWorldY(topPanel, owner.TabTopPanelY);
        SetPanelWorldY(bottomPanel, owner.TabBottomPanelY);
    }

    public void Render(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null || mainCamera == null)
            return;

        ConfigureCamera();
        RefreshStateCache(snapshot.noteStates);

        if (displayedTopSectionIndex == -999)
            BuildInitialPanels(snapshot);

        HandleSectionPaging(snapshot);
        UpdateTransition();
        UpdatePanelColors(topPanel);
        UpdatePanelColors(bottomPanel);
        UpdatePlayhead(snapshot);
        UpdatePauseMenu(snapshot);
        UpdateSongSettings(snapshot);
        UpdateSongSelection(snapshot);
        UpdateLoopMarkers(snapshot);
        songHeaderOverlay?.UpdateFromSnapshot(snapshot);
    }

    public void DisposeRenderer()
    {
        songHeaderOverlay?.Dispose();
        songHeaderOverlay = null;

        if (root != null)
            Object.Destroy(root);
    }

    private void RebuildCaches(List<TabSectionData> sections)
    {
        sectionByIndex.Clear();

        if (sections != null)
        {
            for (int i = 0; i < sections.Count; i++)
                sectionByIndex[sections[i].index] = sections[i];
        }
    }

    private void RefreshStateCache(List<GameplayNoteState> noteStates)
    {
        stateById.Clear();

        if (noteStates == null)
            return;

        for (int i = 0; i < noteStates.Count; i++)
            stateById[noteStates[i].data.id] = noteStates[i];
    }

    private void ConfigureCamera()
    {
        if (mainCamera == null)
            return;

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = owner.tabCameraSize;
        mainCamera.transform.position = new Vector3(0f, 0f, owner.tabCameraZ);
        mainCamera.transform.rotation = Quaternion.identity;
        mainCamera.backgroundColor = owner.tabBackgroundColor;
    }

    private void BuildInitialPanels(GuitarGameplaySnapshot snapshot)
    {
        displayedTopSectionIndex = snapshot.currentSectionIndex;
        displayedBottomSectionIndex = snapshot.nextSectionIndex;

        SetPanelWorldY(topPanel, owner.TabTopPanelY);
        SetPanelWorldY(bottomPanel, owner.TabBottomPanelY);

        topPanel.Build(GetSection(displayedTopSectionIndex));
        bottomPanel.Build(GetSection(displayedBottomSectionIndex));
    }

    private void HandleSectionPaging(GuitarGameplaySnapshot snapshot)
    {
        if (isTransitioning)
            return;

        if (snapshot.currentSectionIndex == displayedTopSectionIndex)
            return;

        if (snapshot.currentSectionIndex == displayedBottomSectionIndex)
        {
            isTransitioning = true;
            transitionElapsed = 0f;
            transitionIsReverse = false;
            transitionOutgoingPanel = topPanel;
            transitionIncomingPanel = bottomPanel;
            queuedBottomSectionIndex = snapshot.currentSectionIndex + 1;
            return;
        }

        if (snapshot.currentSectionIndex + 1 == displayedTopSectionIndex)
        {
            isTransitioning = true;
            transitionElapsed = 0f;
            transitionIsReverse = true;
            transitionOutgoingPanel = bottomPanel;
            transitionIncomingPanel = topPanel;
            queuedBottomSectionIndex = snapshot.currentSectionIndex;
            return;
        }

        displayedTopSectionIndex = snapshot.currentSectionIndex;
        displayedBottomSectionIndex = snapshot.nextSectionIndex;

        SetPanelWorldY(topPanel, owner.TabTopPanelY);
        SetPanelWorldY(bottomPanel, owner.TabBottomPanelY);

        topPanel.Build(GetSection(displayedTopSectionIndex));
        bottomPanel.Build(GetSection(displayedBottomSectionIndex));
    }

    private void UpdateTransition()
    {
        if (!isTransitioning)
            return;

        transitionElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(transitionElapsed / Mathf.Max(0.01f, owner.tabPanelSwapDuration));

        float incomingStartY = transitionIsReverse ? owner.TabTopPanelY : owner.TabBottomPanelY;
        float incomingEndY = transitionIsReverse ? owner.TabBottomPanelY : owner.TabTopPanelY;
        float outgoingStartY = transitionIsReverse ? owner.TabBottomPanelY : owner.TabTopPanelY;
        float outgoingEndY = transitionIsReverse ? owner.TabBottomPanelY - owner.tabPanelLiftDistance : owner.TabTopPanelY + owner.tabPanelLiftDistance;

        float incomingY = Mathf.Lerp(incomingStartY, incomingEndY, t);
        float outgoingY = Mathf.Lerp(outgoingStartY, outgoingEndY, t);

        SetPanelWorldY(transitionIncomingPanel, incomingY);
        SetPanelWorldY(transitionOutgoingPanel, outgoingY);
        transitionOutgoingPanel.SetAlpha(1f - t);

        if (t < 1f)
            return;

        transitionOutgoingPanel.SetAlpha(1f);

        if (!transitionIsReverse)
        {
            TabPanelView oldTop = topPanel;
            topPanel = bottomPanel;
            bottomPanel = oldTop;

            displayedTopSectionIndex = topPanel.SectionIndex;
            displayedBottomSectionIndex = queuedBottomSectionIndex;

            SetPanelWorldY(topPanel, owner.TabTopPanelY);
            SetPanelWorldY(bottomPanel, owner.TabBottomPanelY);

            bottomPanel.Build(GetSection(displayedBottomSectionIndex));
            bottomPanel.SetAlpha(1f);
        }
        else
        {
            TabPanelView oldBottom = bottomPanel;
            bottomPanel = topPanel;
            topPanel = oldBottom;

            displayedTopSectionIndex = queuedBottomSectionIndex;
            displayedBottomSectionIndex = bottomPanel.SectionIndex;

            SetPanelWorldY(topPanel, owner.TabTopPanelY);
            SetPanelWorldY(bottomPanel, owner.TabBottomPanelY);

            topPanel.Build(GetSection(displayedTopSectionIndex));
            topPanel.SetAlpha(1f);
        }

        isTransitioning = false;
        transitionElapsed = 0f;
        transitionOutgoingPanel = null;
        transitionIncomingPanel = null;
        queuedBottomSectionIndex = -1;
        transitionIsReverse = false;
    }

    private void UpdatePlayhead(GuitarGameplaySnapshot snapshot)
    {
        if (playhead == null)
            return;

        if (topPanel == null)
        {
            playhead.SetActive(false);
            return;
        }

        playhead.SetActive(true);

        float sectionDuration = Mathf.Max(0.01f, snapshot.sectionDuration);
        float sectionStart = topPanel.SectionIndex * sectionDuration;
        float localProgress = Mathf.Clamp01((snapshot.songTime - sectionStart) / sectionDuration);
        float x = topPanel.LeftEdge + (localProgress * topPanel.UsableWidth);

        playhead.transform.position = new Vector3(x, topPanel.CenterY, owner.tabZDepth + 0.10f);
        playhead.transform.localScale = new Vector3(owner.tabPlayheadWidth, owner.tabPanelHeight + 0.4f, owner.tabPlayheadDepth);
    }

    private void CreatePauseMenuVisuals()
    {
        pauseMenuRoot = new GameObject("PauseMenu");
        pauseMenuRoot.transform.SetParent(root.transform, false);
        pauseMenuRoot.transform.position = new Vector3(owner.tabPanelCenterX, owner.TabTopPanelY + owner.tabPanelHeight * 1.08f, owner.tabZDepth - 0.35f);

        GameObject menuBg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        menuBg.name = "PauseMenuBg";
        menuBg.transform.SetParent(pauseMenuRoot.transform, false);
        menuBg.transform.localScale = new Vector3(owner.tabPanelWidth * 0.52f, 2.45f, 0.055f);
        menuBg.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(0.06f, 0.08f, 0.12f, 0.95f), 0.4f);

        GameObject titleObj = new GameObject("PauseTitle");
        titleObj.transform.SetParent(pauseMenuRoot.transform, false);
        titleObj.transform.localPosition = new Vector3(0f, 0.43f, -0.05f);
        pauseTitleText = titleObj.AddComponent<TextMeshPro>();
        pauseTitleText.text = "PAUSE";
        pauseTitleText.fontSize = owner.tabLabelFontSize * 1.35f;
        pauseTitleText.alignment = TextAlignmentOptions.Center;
        pauseTitleText.color = Color.white;
        pauseTitleText.sortingOrder = 35;

        GameObject helpObj = new GameObject("PauseHelp");
        helpObj.transform.SetParent(pauseMenuRoot.transform, false);
        helpObj.transform.localPosition = new Vector3(0f, 0.02f, -0.05f);
        pauseHelpText = helpObj.AddComponent<TextMeshPro>();
        pauseHelpText.text = "Left/Right Seek   |   1/2 Select Marker   |   L Song Select   |   T Tone Lab";
        pauseHelpText.fontSize = owner.tabLabelFontSize * 0.62f;
        pauseHelpText.alignment = TextAlignmentOptions.Center;
        pauseHelpText.color = new Color(0.86f, 0.89f, 0.95f);
        pauseHelpText.sortingOrder = 35;

        GameObject speedLabelObj = new GameObject("SpeedLabel");
        speedLabelObj.transform.SetParent(pauseMenuRoot.transform, false);
        speedLabelObj.transform.localPosition = new Vector3(0f, -0.20f, -0.06f);
        speedSliderText = speedLabelObj.AddComponent<TextMeshPro>();
        speedSliderText.fontSize = owner.tabLabelFontSize * 0.60f;
        speedSliderText.alignment = TextAlignmentOptions.Center;
        speedSliderText.color = new Color(0.90f, 0.93f, 1f);
        speedSliderText.sortingOrder = 38;

        speedSliderTrack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        speedSliderTrack.name = "SpeedSliderTrack";
        speedSliderTrack.transform.SetParent(pauseMenuRoot.transform, false);
        speedSliderTrack.transform.localPosition = new Vector3(0f, -0.08f, 0f);
        speedSliderTrack.transform.localScale = new Vector3(3.60f, 0.12f, 0.07f);
        speedSliderTrack.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(0.22f, 0.25f, 0.31f, 0.95f), 0.8f);

        speedSliderFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        speedSliderFill.name = "SpeedSliderFill";
        speedSliderFill.transform.SetParent(pauseMenuRoot.transform, false);
        speedSliderFill.transform.localPosition = new Vector3(-1.78f, -0.08f, -0.01f);
        speedSliderFill.transform.localScale = new Vector3(0.04f, 0.10f, 0.06f);
        speedSliderFill.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(0.95f, 0.78f, 0.18f, 0.95f), 1.6f);

        speedSliderKnob = GameObject.CreatePrimitive(PrimitiveType.Cube);
        speedSliderKnob.name = "SpeedSliderKnob";
        speedSliderKnob.transform.SetParent(pauseMenuRoot.transform, false);
        speedSliderKnob.transform.localPosition = new Vector3(-1.78f, -0.08f, -0.03f);
        speedSliderKnob.transform.localScale = new Vector3(0.17f, 0.23f, 0.09f);
        speedSliderKnob.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(1f, 0.95f, 0.85f, 0.98f), 1.4f);

        pauseLoopButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pauseLoopButton.name = "PauseLoopButton";
        pauseLoopButton.transform.SetParent(pauseMenuRoot.transform, false);
        pauseLoopButton.transform.localPosition = new Vector3(0f, -0.62f, 0f);
        pauseLoopButton.transform.localScale = new Vector3(4.7f, 0.64f, 0.08f);

        GameObject loopTextObj = new GameObject("PauseLoopLabel");
        loopTextObj.transform.SetParent(pauseMenuRoot.transform, false);
        loopTextObj.transform.localPosition = new Vector3(0f, -0.64f, -0.06f);
        pauseLoopText = loopTextObj.AddComponent<TextMeshPro>();
        pauseLoopText.fontSize = owner.tabLabelFontSize * 0.72f;
        pauseLoopText.alignment = TextAlignmentOptions.Center;
        pauseLoopText.sortingOrder = 38;

        songSelectionButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
        songSelectionButton.name = "SongSelectionButton";
        songSelectionButton.transform.SetParent(pauseMenuRoot.transform, false);
        songSelectionButton.transform.localPosition = new Vector3(0f, -1.02f, 0f);
        songSelectionButton.transform.localScale = new Vector3(4.7f, 0.50f, 0.08f);

        GameObject songSelectionTextObj = new GameObject("SongSelectionButtonLabel");
        songSelectionTextObj.transform.SetParent(pauseMenuRoot.transform, false);
        songSelectionTextObj.transform.localPosition = new Vector3(0f, -1.04f, -0.06f);
        songSelectionButtonText = songSelectionTextObj.AddComponent<TextMeshPro>();
        songSelectionButtonText.text = "SONG SELECTION [L / Click]";
        songSelectionButtonText.fontSize = owner.tabLabelFontSize * 0.62f;
        songSelectionButtonText.alignment = TextAlignmentOptions.Center;
        songSelectionButtonText.color = new Color(0.95f, 1f, 0.93f);
        songSelectionButtonText.sortingOrder = 38;

        songSettingsButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
        songSettingsButton.name = "SongSettingsButton";
        songSettingsButton.transform.SetParent(pauseMenuRoot.transform, false);
        songSettingsButton.transform.localPosition = new Vector3(0f, -1.38f, 0f);
        songSettingsButton.transform.localScale = new Vector3(4.7f, 0.50f, 0.08f);

        GameObject songSettingsTextObj = new GameObject("SongSettingsButtonLabel");
        songSettingsTextObj.transform.SetParent(pauseMenuRoot.transform, false);
        songSettingsTextObj.transform.localPosition = new Vector3(0f, -1.40f, -0.06f);
        songSettingsButtonText = songSettingsTextObj.AddComponent<TextMeshPro>();
        songSettingsButtonText.text = "SONG SETTINGS [S / Click]";
        songSettingsButtonText.fontSize = owner.tabLabelFontSize * 0.62f;
        songSettingsButtonText.alignment = TextAlignmentOptions.Center;
        songSettingsButtonText.color = new Color(0.95f, 0.98f, 1f);
        songSettingsButtonText.sortingOrder = 38;

        toneLabButton = GameObject.CreatePrimitive(PrimitiveType.Cube);
        toneLabButton.name = "ToneLabButton";
        toneLabButton.transform.SetParent(pauseMenuRoot.transform, false);
        toneLabButton.transform.localPosition = new Vector3(0f, -1.74f, 0f);
        toneLabButton.transform.localScale = new Vector3(4.7f, 0.50f, 0.08f);

        GameObject toneLabTextObj = new GameObject("ToneLabButtonLabel");
        toneLabTextObj.transform.SetParent(pauseMenuRoot.transform, false);
        toneLabTextObj.transform.localPosition = new Vector3(0f, -1.76f, -0.06f);
        toneLabButtonText = toneLabTextObj.AddComponent<TextMeshPro>();
        toneLabButtonText.text = "TONE LAB [T / Click]";
        toneLabButtonText.fontSize = owner.tabLabelFontSize * 0.62f;
        toneLabButtonText.alignment = TextAlignmentOptions.Center;
        toneLabButtonText.color = new Color(0.97f, 0.95f, 1f);
        toneLabButtonText.sortingOrder = 38;

        pauseMenuRoot.SetActive(false);
    }

    private void CreateSongSettingsVisuals()
    {
        songSettingsRoot = new GameObject("SongSettingsMenu");
        songSettingsRoot.transform.SetParent(root.transform, false);
        songSettingsRoot.transform.position = new Vector3(owner.tabPanelCenterX, owner.TabTopPanelY + owner.tabPanelHeight * 1.08f, owner.tabZDepth - 0.35f);

        GameObject menuBg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        menuBg.name = "SongSettingsBg";
        menuBg.transform.SetParent(songSettingsRoot.transform, false);
        menuBg.transform.localScale = new Vector3(owner.tabPanelWidth * 0.52f, 3.25f, 0.055f);
        menuBg.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(0.08f, 0.10f, 0.16f, 0.97f), 0.4f);

        GameObject titleObj = new GameObject("SongSettingsTitle");
        titleObj.transform.SetParent(songSettingsRoot.transform, false);
        titleObj.transform.localPosition = new Vector3(0f, 0.56f, -0.05f);
        songSettingsTitleText = titleObj.AddComponent<TextMeshPro>();
        songSettingsTitleText.text = "SONG SETTINGS";
        songSettingsTitleText.fontSize = owner.tabLabelFontSize * 1.05f;
        songSettingsTitleText.alignment = TextAlignmentOptions.Center;
        songSettingsTitleText.color = Color.white;
        songSettingsTitleText.sortingOrder = 35;

        GameObject helpObj = new GameObject("SongSettingsHelp");
        helpObj.transform.SetParent(songSettingsRoot.transform, false);
        helpObj.transform.localPosition = new Vector3(0f, 0.24f, -0.05f);
        songSettingsHelpText = helpObj.AddComponent<TextMeshPro>();
        songSettingsHelpText.text = "Space: Play/Pause  |  Left/Right: Seek (Double-tap: Prev/Next note)  |  Q/E: Track  |  O: Offset Scope  |  Esc: Back";
        songSettingsHelpText.fontSize = owner.tabLabelFontSize * 0.48f;
        songSettingsHelpText.alignment = TextAlignmentOptions.Center;
        songSettingsHelpText.color = new Color(0.86f, 0.91f, 1f);
        songSettingsHelpText.sortingOrder = 35;

        GameObject offsetLabelObj = new GameObject("OffsetLabel");
        offsetLabelObj.transform.SetParent(songSettingsRoot.transform, false);
        offsetLabelObj.transform.localPosition = new Vector3(0f, -0.02f, -0.06f);
        offsetSliderText = offsetLabelObj.AddComponent<TextMeshPro>();
        offsetSliderText.fontSize = owner.tabLabelFontSize * 0.60f;
        offsetSliderText.alignment = TextAlignmentOptions.Center;
        offsetSliderText.color = new Color(0.90f, 0.93f, 1f);
        offsetSliderText.sortingOrder = 38;

        offsetSliderTrack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        offsetSliderTrack.name = "OffsetSliderTrack";
        offsetSliderTrack.transform.SetParent(songSettingsRoot.transform, false);
        offsetSliderTrack.transform.localPosition = new Vector3(0f, -0.30f, 0f);
        offsetSliderTrack.transform.localScale = new Vector3(3.60f, 0.12f, 0.07f);
        offsetSliderTrack.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(0.22f, 0.25f, 0.31f, 0.95f), 0.8f);

        offsetSliderFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        offsetSliderFill.name = "OffsetSliderFill";
        offsetSliderFill.transform.SetParent(songSettingsRoot.transform, false);
        offsetSliderFill.transform.localPosition = new Vector3(0f, -0.30f, -0.01f);
        offsetSliderFill.transform.localScale = new Vector3(0.04f, 0.10f, 0.06f);
        offsetSliderFill.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(0.25f, 0.83f, 0.96f, 0.95f), 1.3f);

        offsetSliderKnob = GameObject.CreatePrimitive(PrimitiveType.Cube);
        offsetSliderKnob.name = "OffsetSliderKnob";
        offsetSliderKnob.transform.SetParent(songSettingsRoot.transform, false);
        offsetSliderKnob.transform.localPosition = new Vector3(0f, -0.30f, -0.03f);
        offsetSliderKnob.transform.localScale = new Vector3(0.17f, 0.23f, 0.09f);
        offsetSliderKnob.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(0.9f, 0.98f, 1f, 0.98f), 1.2f);

        GameObject tabSpeedLabelObj = new GameObject("TabSpeedOffsetLabel");
        tabSpeedLabelObj.transform.SetParent(songSettingsRoot.transform, false);
        tabSpeedLabelObj.transform.localPosition = new Vector3(0f, -0.50f, -0.06f);
        tabSpeedOffsetSliderText = tabSpeedLabelObj.AddComponent<TextMeshPro>();
        tabSpeedOffsetSliderText.fontSize = owner.tabLabelFontSize * 0.58f;
        tabSpeedOffsetSliderText.alignment = TextAlignmentOptions.Center;
        tabSpeedOffsetSliderText.color = new Color(0.93f, 0.95f, 1f);
        tabSpeedOffsetSliderText.sortingOrder = 38;

        tabSpeedOffsetSliderTrack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tabSpeedOffsetSliderTrack.name = "TabSpeedOffsetSliderTrack";
        tabSpeedOffsetSliderTrack.transform.SetParent(songSettingsRoot.transform, false);
        tabSpeedOffsetSliderTrack.transform.localPosition = new Vector3(0f, -0.74f, 0f);
        tabSpeedOffsetSliderTrack.transform.localScale = new Vector3(3.60f, 0.12f, 0.07f);
        tabSpeedOffsetSliderTrack.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(0.22f, 0.25f, 0.31f, 0.95f), 0.8f);

        tabSpeedOffsetSliderFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tabSpeedOffsetSliderFill.name = "TabSpeedOffsetSliderFill";
        tabSpeedOffsetSliderFill.transform.SetParent(songSettingsRoot.transform, false);
        tabSpeedOffsetSliderFill.transform.localPosition = new Vector3(-1.78f, -0.74f, -0.01f);
        tabSpeedOffsetSliderFill.transform.localScale = new Vector3(0.04f, 0.10f, 0.06f);
        tabSpeedOffsetSliderFill.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(0.97f, 0.60f, 0.22f, 0.95f), 1.3f);

        tabSpeedOffsetSliderKnob = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tabSpeedOffsetSliderKnob.name = "TabSpeedOffsetSliderKnob";
        tabSpeedOffsetSliderKnob.transform.SetParent(songSettingsRoot.transform, false);
        tabSpeedOffsetSliderKnob.transform.localPosition = new Vector3(-1.78f, -0.74f, -0.03f);
        tabSpeedOffsetSliderKnob.transform.localScale = new Vector3(0.17f, 0.23f, 0.09f);
        tabSpeedOffsetSliderKnob.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(1f, 0.95f, 0.85f, 0.98f), 1.3f);

        GameObject startDelayLabelObj = new GameObject("SongStartDelayLabel");
        startDelayLabelObj.transform.SetParent(songSettingsRoot.transform, false);
        startDelayLabelObj.transform.localPosition = new Vector3(0f, -0.92f, -0.06f);
        songStartDelaySliderText = startDelayLabelObj.AddComponent<TextMeshPro>();
        songStartDelaySliderText.fontSize = owner.tabLabelFontSize * 0.58f;
        songStartDelaySliderText.alignment = TextAlignmentOptions.Center;
        songStartDelaySliderText.color = new Color(0.93f, 0.95f, 1f);
        songStartDelaySliderText.sortingOrder = 38;

        songStartDelaySliderTrack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        songStartDelaySliderTrack.name = "SongStartDelaySliderTrack";
        songStartDelaySliderTrack.transform.SetParent(songSettingsRoot.transform, false);
        songStartDelaySliderTrack.transform.localPosition = new Vector3(0f, -1.10f, 0f);
        songStartDelaySliderTrack.transform.localScale = new Vector3(3.60f, 0.12f, 0.07f);
        songStartDelaySliderTrack.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(0.22f, 0.25f, 0.31f, 0.95f), 0.8f);

        songStartDelaySliderFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        songStartDelaySliderFill.name = "SongStartDelaySliderFill";
        songStartDelaySliderFill.transform.SetParent(songSettingsRoot.transform, false);
        songStartDelaySliderFill.transform.localPosition = new Vector3(-1.78f, -1.10f, -0.01f);
        songStartDelaySliderFill.transform.localScale = new Vector3(0.04f, 0.10f, 0.06f);
        songStartDelaySliderFill.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(0.58f, 0.93f, 0.35f, 0.95f), 1.3f);

        songStartDelaySliderKnob = GameObject.CreatePrimitive(PrimitiveType.Cube);
        songStartDelaySliderKnob.name = "SongStartDelaySliderKnob";
        songStartDelaySliderKnob.transform.SetParent(songSettingsRoot.transform, false);
        songStartDelaySliderKnob.transform.localPosition = new Vector3(-1.78f, -1.10f, -0.03f);
        songStartDelaySliderKnob.transform.localScale = new Vector3(0.17f, 0.23f, 0.09f);
        songStartDelaySliderKnob.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(0.92f, 1f, 0.88f, 0.98f), 1.3f);

        GameObject statusObj = new GameObject("SongStatusLabel");
        statusObj.transform.SetParent(songSettingsRoot.transform, false);
        statusObj.transform.localPosition = new Vector3(0f, -1.42f, -0.05f);
        songStatusText = statusObj.AddComponent<TextMeshPro>();
        songStatusText.fontSize = owner.tabLabelFontSize * 0.52f;
        songStatusText.alignment = TextAlignmentOptions.Center;
        songStatusText.color = new Color(0.90f, 0.95f, 1f);
        songStatusText.sortingOrder = 38;

        songSettingsRoot.SetActive(false);
    }

    private void CreateSongSelectionVisuals()
    {
        songSelectionRoot = new GameObject("SongSelectionMenu");
        songSelectionRoot.transform.SetParent(root.transform, false);
        songSelectionRoot.transform.position = new Vector3(owner.tabPanelCenterX, owner.TabTopPanelY + owner.tabPanelHeight * 1.08f, owner.tabZDepth - 0.35f);

        GameObject menuBg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        menuBg.name = "SongSelectionBg";
        menuBg.transform.SetParent(songSelectionRoot.transform, false);
        menuBg.transform.localScale = new Vector3(owner.tabPanelWidth * 0.52f, 3.1f, 0.055f);
        menuBg.GetComponent<Renderer>().material = CreateGlowMaterial(new Color(0.08f, 0.12f, 0.10f, 0.97f), 0.4f);

        GameObject titleObj = new GameObject("SongSelectionTitle");
        titleObj.transform.SetParent(songSelectionRoot.transform, false);
        titleObj.transform.localPosition = new Vector3(0f, 0.75f, -0.05f);
        songSelectionTitleText = titleObj.AddComponent<TextMeshPro>();
        songSelectionTitleText.text = "SONG SELECTION";
        songSelectionTitleText.fontSize = owner.tabLabelFontSize * 1.1f;
        songSelectionTitleText.alignment = TextAlignmentOptions.Center;
        songSelectionTitleText.color = Color.white;
        songSelectionTitleText.sortingOrder = 38;

        GameObject helpObj = new GameObject("SongSelectionHelp");
        helpObj.transform.SetParent(songSelectionRoot.transform, false);
        helpObj.transform.localPosition = new Vector3(0f, 0.48f, -0.05f);
        songSelectionHelpText = helpObj.AddComponent<TextMeshPro>();
        songSelectionHelpText.text = "Up/Down: Navigate  |  Enter/Click: Select  |  Esc: Back";
        songSelectionHelpText.fontSize = owner.tabLabelFontSize * 0.50f;
        songSelectionHelpText.alignment = TextAlignmentOptions.Center;
        songSelectionHelpText.color = new Color(0.88f, 0.95f, 0.92f);
        songSelectionHelpText.sortingOrder = 38;

        for (int i = 0; i < 8; i++)
        {
            GameObject rowObj = new GameObject($"SongRow_{i}");
            rowObj.transform.SetParent(songSelectionRoot.transform, false);
            rowObj.transform.localPosition = new Vector3(-2.25f, 0.20f - (i * 0.26f), -0.05f);

            TextMeshPro rowText = rowObj.AddComponent<TextMeshPro>();
            rowText.fontSize = owner.tabLabelFontSize * 0.56f;
            rowText.alignment = TextAlignmentOptions.Left;
            rowText.color = new Color(0.84f, 0.90f, 0.86f);
            rowText.sortingOrder = 38;
            rowText.text = string.Empty;
            songSelectionRows.Add(rowText);
        }

        songSelectionRoot.SetActive(false);
    }

    private void UpdateSongSelection(GuitarGameplaySnapshot snapshot)
    {
        if (songSelectionRoot == null)
            return;

        bool visible = snapshot != null && snapshot.showSongSelection && snapshot.showLegacyPauseUi;
        songSelectionRoot.SetActive(visible);

        if (!visible)
            return;

        List<string> songs = snapshot.availableSongNames;
        int selected = snapshot.selectedSongIndex;
        int scroll = snapshot.songListScrollOffset;

        for (int row = 0; row < songSelectionRows.Count; row++)
        {
            TextMeshPro rowText = songSelectionRows[row];
            int songIndex = scroll + row;
            if (songs == null || songIndex >= songs.Count)
            {
                rowText.text = string.Empty;
                continue;
            }

            string songName = songs[songIndex];
            bool isSelected = songIndex == selected;
            rowText.text = isSelected ? $"> {songName}" : $"  {songName}";
            rowText.color = isSelected ? new Color(0.96f, 1f, 0.65f) : new Color(0.84f, 0.90f, 0.86f);
        }
    }

    private void UpdatePauseMenu(GuitarGameplaySnapshot snapshot)
    {
        if (pauseMenuRoot == null)
            return;

        bool visible = snapshot != null && snapshot.isPaused && !snapshot.showSongSettings && !snapshot.showSongSelection && snapshot.showLegacyPauseUi;
        pauseMenuRoot.SetActive(visible);

        if (!visible)
            return;

        bool isOn = snapshot.loopEnabled;
        bool markerOne = snapshot.selectedLoopMarker == 1;
        float speedPercent = Mathf.Clamp(snapshot.playbackSpeedPercent, 1f, 200f);
        pauseLoopText.text = $"LOOP {(isOn ? "ON" : "OFF")}  [Enter / Click]   Active Marker: {(markerOne ? "1" : "2")}";
        pauseLoopText.color = isOn ? new Color(0.95f, 1f, 0.95f) : new Color(0.95f, 0.9f, 0.9f);

        if (speedSliderText != null)
            speedSliderText.text = $"SPEED  {speedPercent:F0}%";

        float sliderT = Mathf.InverseLerp(1f, 200f, speedPercent);
        if (speedSliderKnob != null)
            speedSliderKnob.transform.localPosition = new Vector3(Mathf.Lerp(-1.78f, 1.78f, sliderT), -0.08f, -0.03f);

        if (speedSliderFill != null)
        {
            float fillWidth = Mathf.Lerp(0.04f, 3.56f, sliderT);
            speedSliderFill.transform.localScale = new Vector3(fillWidth, 0.10f, 0.06f);
            speedSliderFill.transform.localPosition = new Vector3(-1.78f + (fillWidth * 0.5f), -0.08f, -0.01f);
        }

        if (pauseLoopButton != null)
        {
            Renderer r = pauseLoopButton.GetComponent<Renderer>();
            if (r != null)
            {
                Color buttonColor = isOn ? new Color(0.16f, 0.55f, 0.28f, 0.97f) : new Color(0.44f, 0.15f, 0.15f, 0.97f);
                r.material.color = buttonColor;
                r.material.EnableKeyword("_EMISSION");
                r.material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                r.material.SetColor("_EmissionColor", buttonColor * Mathf.Pow(2f, 1.8f));
            }
        }

        if (songSelectionButton != null)
        {
            Renderer r = songSelectionButton.GetComponent<Renderer>();
            if (r != null)
            {
                Color buttonColor = new Color(0.18f, 0.41f, 0.22f, 0.97f);
                r.material.color = buttonColor;
                r.material.EnableKeyword("_EMISSION");
                r.material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                r.material.SetColor("_EmissionColor", buttonColor * Mathf.Pow(2f, 1.8f));
            }
        }

        if (songSettingsButton != null)
        {
            Renderer r = songSettingsButton.GetComponent<Renderer>();
            if (r != null)
            {
                Color buttonColor = new Color(0.15f, 0.27f, 0.48f, 0.97f);
                r.material.color = buttonColor;
                r.material.EnableKeyword("_EMISSION");
                r.material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                r.material.SetColor("_EmissionColor", buttonColor * Mathf.Pow(2f, 1.8f));
            }
        }

        if (toneLabButton != null)
        {
            Renderer r = toneLabButton.GetComponent<Renderer>();
            if (r != null)
            {
                Color buttonColor = new Color(0.34f, 0.18f, 0.48f, 0.97f);
                r.material.color = buttonColor;
                r.material.EnableKeyword("_EMISSION");
                r.material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                r.material.SetColor("_EmissionColor", buttonColor * Mathf.Pow(2f, 1.8f));
            }
        }
    }

    private void UpdateSongSettings(GuitarGameplaySnapshot snapshot)
    {
        if (songSettingsRoot == null)
            return;

        bool visible = snapshot != null && snapshot.showSongSettings && snapshot.showLegacyPauseUi;
        songSettingsRoot.SetActive(visible);

        if (!visible)
            return;

        float offsetMs = Mathf.Clamp(snapshot.audioOffsetMs, -2000f, 2000f);
        if (offsetSliderText != null)
            offsetSliderText.text = $"AUDIO OFFSET  {offsetMs:F0} ms";

        float sliderT = Mathf.InverseLerp(-2000f, 2000f, offsetMs);
        if (offsetSliderKnob != null)
            offsetSliderKnob.transform.localPosition = new Vector3(Mathf.Lerp(-1.78f, 1.78f, sliderT), -0.30f, -0.03f);

        if (offsetSliderFill != null)
        {
            float centerX = 0f;
            float leftX = -1.78f;
            float rightX = 1.78f;
            float knobX = Mathf.Lerp(leftX, rightX, sliderT);
            float fillWidth = Mathf.Max(0.04f, Mathf.Abs(knobX - centerX));
            offsetSliderFill.transform.localScale = new Vector3(fillWidth, 0.10f, 0.06f);
            offsetSliderFill.transform.localPosition = new Vector3((knobX + centerX) * 0.5f, -0.30f, -0.01f);
        }

        float tabSpeedOffsetPercent = Mathf.Clamp(snapshot.tabSpeedOffsetPercent, 50f, 150f);
        if (tabSpeedOffsetSliderText != null)
            tabSpeedOffsetSliderText.text = $"TAB SPEED OFFSET  {tabSpeedOffsetPercent:F0}%";

        float tabSpeedSliderT = Mathf.InverseLerp(50f, 150f, tabSpeedOffsetPercent);
        if (tabSpeedOffsetSliderKnob != null)
            tabSpeedOffsetSliderKnob.transform.localPosition = new Vector3(Mathf.Lerp(-1.78f, 1.78f, tabSpeedSliderT), -0.74f, -0.03f);

        if (tabSpeedOffsetSliderFill != null)
        {
            float fillWidth = Mathf.Lerp(0.04f, 3.56f, tabSpeedSliderT);
            tabSpeedOffsetSliderFill.transform.localScale = new Vector3(fillWidth, 0.10f, 0.06f);
            tabSpeedOffsetSliderFill.transform.localPosition = new Vector3(-1.78f + (fillWidth * 0.5f), -0.74f, -0.01f);
        }

        float songStartDelaySeconds = Mathf.Clamp(snapshot.songStartDelaySeconds, 0f, 8f);
        if (songStartDelaySliderText != null)
            songStartDelaySliderText.text = $"START DELAY  {songStartDelaySeconds:F2}s";

        float startDelaySliderT = Mathf.InverseLerp(0f, 8f, songStartDelaySeconds);
        if (songStartDelaySliderKnob != null)
            songStartDelaySliderKnob.transform.localPosition = new Vector3(Mathf.Lerp(-1.78f, 1.78f, startDelaySliderT), -1.10f, -0.03f);

        if (songStartDelaySliderFill != null)
        {
            float fillWidth = Mathf.Lerp(0.04f, 3.56f, startDelaySliderT);
            songStartDelaySliderFill.transform.localScale = new Vector3(fillWidth, 0.10f, 0.06f);
            songStartDelaySliderFill.transform.localPosition = new Vector3(-1.78f + (fillWidth * 0.5f), -1.10f, -0.01f);
        }

        if (songStatusText != null)
        {
            string status = snapshot.hasBackingTrack ? "Loaded" : "Missing";
            string play = snapshot.isBackingTrackPlaying ? "Playing" : "Paused";
            string notesState = snapshot.isPaused ? "Notes paused" : "Notes moving";
            songStatusText.text =
                $"Track: {status}   Audio: {play}   {notesState}   T={snapshot.backingTrackTime:F2}s\n" +
                $"PART: {snapshot.selectedTrackDisplayName}\n" +
                $"OFFSET SCOPE: {snapshot.offsetScopeLabel}\n" +
                $"{snapshot.trackSelectionHint}  |  {snapshot.offsetScopeHint}";
            songStatusText.color = snapshot.hasBackingTrack ? new Color(0.88f, 1f, 0.9f) : new Color(1f, 0.75f, 0.75f);
        }
    }

    private void UpdateLoopMarkers(GuitarGameplaySnapshot snapshot)
    {
        bool showMarkers = snapshot != null && snapshot.loopEnabled;

        if (loopMarkerStart != null)
            loopMarkerStart.SetActive(showMarkers);
        if (loopMarkerEnd != null)
            loopMarkerEnd.SetActive(showMarkers);

        if (!showMarkers)
            return;

        if (TryGetMarkerWorldPosition(snapshot.loopStartTime, snapshot.sectionDuration, out Vector3 startPos, out float startHeight))
        {
            loopMarkerStart.transform.position = startPos;
            loopMarkerStart.transform.localScale = new Vector3(owner.tabPlayheadWidth * 1.15f, startHeight, owner.tabPlayheadDepth * 1.25f);
        }
        else
        {
            loopMarkerStart.SetActive(false);
        }

        if (TryGetMarkerWorldPosition(snapshot.loopEndTime, snapshot.sectionDuration, out Vector3 endPos, out float endHeight))
        {
            loopMarkerEnd.transform.position = endPos;
            loopMarkerEnd.transform.localScale = new Vector3(owner.tabPlayheadWidth * 1.15f, endHeight, owner.tabPlayheadDepth * 1.25f);
        }
        else
        {
            loopMarkerEnd.SetActive(false);
        }
    }

    private bool TryGetMarkerWorldPosition(float markerTime, float sectionDuration, out Vector3 position, out float height)
    {
        position = Vector3.zero;
        height = owner.tabPanelHeight + 0.42f;

        float safeSectionDuration = Mathf.Max(0.01f, sectionDuration);
        int markerSection = Mathf.Max(0, Mathf.FloorToInt(markerTime / safeSectionDuration));
        TabPanelView panel = null;

        if (topPanel != null && topPanel.SectionIndex == markerSection)
            panel = topPanel;
        else if (bottomPanel != null && bottomPanel.SectionIndex == markerSection)
            panel = bottomPanel;

        if (panel == null)
            return false;

        float sectionStart = markerSection * safeSectionDuration;
        float localProgress = Mathf.Clamp01((markerTime - sectionStart) / safeSectionDuration);
        float x = panel.LeftEdge + localProgress * panel.UsableWidth;

        position = new Vector3(x, panel.CenterY, owner.tabZDepth + 0.11f);
        return true;
    }

    private void UpdatePanelColors(TabPanelView panel)
    {
        if (panel == null)
            return;

        foreach (var kv in panel.NoteViews)
        {
            if (!stateById.TryGetValue(kv.Key, out GameplayNoteState state))
                continue;

            TabNoteView noteView = kv.Value;

            if (state.IsHit)
            {
                noteView.SetStateColors(owner.tabHitColor, owner.tabHitColor, Color.white, true);
            }
            else if (state.IsMissed)
            {
                noteView.SetStateColors(owner.tabMissColor, owner.tabMissColor, Color.white, false);
            }
            else if (state.isJudgeable)
            {
                Color outline = owner.GetStringColor(state.data.stringIdx);
                noteView.SetStateColors(outline, owner.tabJudgeableColor, Color.white, true);
            }
            else
            {
                Color outline = owner.GetStringColor(state.data.stringIdx);
                Color fill = owner.GetDarkenedStringColor(state.data.stringIdx, owner.tabIdleFillDarken);
                noteView.SetStateColors(outline, fill, Color.white, false);
            }
        }
    }

    private TabSectionData GetSection(int sectionIndex)
    {
        if (sectionByIndex.TryGetValue(sectionIndex, out TabSectionData section))
            return section;

        float sectionDuration = Mathf.Max(0.25f, owner.tabSectionDuration * Mathf.Max(0.5f, owner.tabSectionLengthMultiplier));
        return new TabSectionData
        {
            index = sectionIndex,
            startTime = sectionIndex * sectionDuration,
            endTime = (sectionIndex + 1) * sectionDuration,
            noteIds = new List<int>()
        };
    }

    private void SetPanelWorldY(TabPanelView panel, float y)
    {
        if (panel == null || panel.Root == null)
            return;

        panel.CenterY = y;
        panel.Root.transform.position = new Vector3(owner.tabPanelCenterX, y, owner.tabZDepth);
    }

    private Material CreateGlowMaterial(Color c, float intensity)
    {
        bool isURP = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        string shaderName = isURP ? "Universal Render Pipeline/Lit" : "Standard";

        Material material = new Material(Shader.Find(shaderName));
        material.color = c;
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        material.SetColor("_EmissionColor", c * Mathf.Pow(2f, intensity));
        return material;
    }

    private sealed class TabPanelView
    {
        private readonly GuitarBridgeServer owner;
        private readonly float lineSpacing;
        private readonly List<Renderer> staticRenderers = new List<Renderer>();
        private readonly List<GameObject> dynamicObjects = new List<GameObject>();
        private readonly TextMeshPro sectionLabel;
        private readonly string headerPrefix;

        public GameObject Root { get; }
        public Dictionary<int, TabNoteView> NoteViews { get; } = new Dictionary<int, TabNoteView>();
        public int SectionIndex { get; private set; } = -1;
        public float CenterY { get; set; }
        public float LeftEdge => owner.tabPanelCenterX - (owner.tabPanelWidth * 0.5f) + owner.tabHorizontalPadding;
        public float RightEdge => owner.tabPanelCenterX + (owner.tabPanelWidth * 0.5f) - owner.tabHorizontalPadding;
        public float UsableWidth => RightEdge - LeftEdge;

        public TabPanelView(Transform parent, string name, GuitarBridgeServer owner, bool showPlayNowLabel)
        {
            this.owner = owner;
            lineSpacing = owner.tabLineSpacing;
            headerPrefix = showPlayNowLabel ? "NOW" : "NEXT";

            Root = new GameObject(name);
            Root.transform.SetParent(parent, false);

            CreateBorder();
            CreateStrings();

            GameObject labelObj = new GameObject(name + "_Label");
            labelObj.transform.SetParent(Root.transform, false);
            labelObj.transform.localPosition = new Vector3(-owner.tabPanelWidth * 0.5f + 1.2f, owner.tabPanelHeight * 0.5f + 0.55f, 0f);
            sectionLabel = labelObj.AddComponent<TextMeshPro>();
            sectionLabel.fontSize = owner.tabLabelFontSize;
            sectionLabel.color = showPlayNowLabel ? owner.tabHeaderCurrentColor : owner.tabHeaderNextColor;
            sectionLabel.text = headerPrefix;
            sectionLabel.alignment = TextAlignmentOptions.Left;
            sectionLabel.sortingOrder = 20;
        }

        public void Build(TabSectionData section)
        {
            ClearDynamic();
            NoteViews.Clear();

            if (section == null)
            {
                SectionIndex = -1;
                Root.SetActive(false);
                return;
            }

            Root.SetActive(true);
            SectionIndex = section.index;
            sectionLabel.text = section.index >= 0 ? $"{headerPrefix}  {section.startTime:F1}s" : headerPrefix;

            for (int i = 0; i < section.noteIds.Count; i++)
            {
                if (!owner.TryGetChartNoteById(section.noteIds[i], out NoteData note))
                    continue;

                float normalizedX = (note.time - section.startTime) / Mathf.Max(0.01f, section.endTime - section.startTime);
                float x = LeftEdge + normalizedX * UsableWidth;
                float y = GetStringY(note.stringIdx);

                GameObject markerRoot = new GameObject($"TabNote_{note.id}");
                markerRoot.transform.SetParent(Root.transform, false);
                markerRoot.transform.position = new Vector3(x, y, owner.tabZDepth - 0.12f);

                GameObject outlineDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                outlineDisc.name = $"Outline_{note.id}";
                outlineDisc.transform.SetParent(markerRoot.transform, false);
                outlineDisc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                outlineDisc.transform.localPosition = Vector3.zero;
                outlineDisc.transform.localScale = new Vector3(
                    owner.tabNoteCircleDiameter * 0.5f,
                    owner.tabNoteCircleDepth * 0.5f,
                    owner.tabNoteCircleDiameter * 0.5f
                );

                float innerDiameter = Mathf.Max(0.05f, owner.tabNoteCircleDiameter - owner.tabNoteOutlineThickness);

                GameObject fillDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fillDisc.name = $"Fill_{note.id}";
                fillDisc.transform.SetParent(markerRoot.transform, false);
                fillDisc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                fillDisc.transform.localPosition = new Vector3(0f, 0f, -0.02f);
                fillDisc.transform.localScale = new Vector3(
                    innerDiameter * 0.5f,
                    owner.tabNoteCircleDepth * 0.28f,
                    innerDiameter * 0.5f
                );

                Renderer outlineRenderer = outlineDisc.GetComponent<Renderer>();
                Renderer fillRenderer = fillDisc.GetComponent<Renderer>();
                outlineRenderer.material = owner.CreateSharedGlowMaterial(owner.GetStringColor(note.stringIdx), 1.0f);
                fillRenderer.material = owner.CreateSharedGlowMaterial(owner.GetDarkenedStringColor(note.stringIdx, owner.tabIdleFillDarken), 0.3f);

                GameObject textObj = new GameObject($"Label_{note.id}");
                textObj.transform.SetParent(markerRoot.transform, false);
                textObj.transform.localPosition = new Vector3(0f, 0f, -0.08f);

                TextMeshPro text = textObj.AddComponent<TextMeshPro>();
                text.text = Mathf.Max(0, note.fret).ToString();
                text.fontSize = owner.tabNoteFontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.enableAutoSizing = false;
                text.sortingOrder = 30;

                dynamicObjects.Add(markerRoot);

                List<Renderer> extraRenderers = new List<Renderer>();
                List<TextMeshPro> extraTexts = new List<TextMeshPro>();
                GameObject tunnelRoot = BuildTechniqueTunnel(note, section, x, y, extraRenderers, extraTexts);
                if (tunnelRoot != null)
                    dynamicObjects.Add(tunnelRoot);

                NoteViews[note.id] = new TabNoteView(outlineRenderer, fillRenderer, text, extraRenderers, extraTexts);
            }
        }

        private GameObject BuildTechniqueTunnel(NoteData note, TabSectionData section, float x, float y, List<Renderer> extraRenderers, List<TextMeshPro> extraTexts)
        {
            bool hasTechnique = note.technique != NoteTechnique.None;
            bool hasSustain = note.duration > 0.05f;
            if (!hasTechnique && !hasSustain)
                return null;

            float visibleNoteRadius = GetVisibleNoteRadius();
            float tunnelCircleOverlap = Mathf.Min(visibleNoteRadius * 0.65f, 0.2f);
            float startX = x + visibleNoteRadius - tunnelCircleOverlap;

            float naturalEndTime = Mathf.Min(section.endTime, note.time + Mathf.Max(note.duration, 0.05f));
            float visualEndTime = naturalEndTime;
            float rightX;

            float plainSustainCutBeforeNextNote = GetPlainSustainCutBeforeNextNote();
            float plainSustainMinVisibleWidth = GetPlainSustainMinVisibleWidth();

            if (!hasTechnique && TryFindNextNoteInSection(section, note, out NoteData nextNote))
            {
                float nextNormalized = (nextNote.time - section.startTime) / Mathf.Max(0.01f, section.endTime - section.startTime);
                float nextNoteX = LeftEdge + nextNormalized * UsableWidth;

                float desiredRightX = nextNoteX - visibleNoteRadius - plainSustainCutBeforeNextNote;
                float naturalNormalized = (visualEndTime - section.startTime) / Mathf.Max(0.01f, section.endTime - section.startTime);
                float naturalRightX = LeftEdge + naturalNormalized * UsableWidth;
                rightX = Mathf.Min(naturalRightX, desiredRightX);

                float plainWidth = rightX - startX;
                if (plainWidth < plainSustainMinVisibleWidth)
                    return null;
            }
            else
            {
                float normalizedRight = (visualEndTime - section.startTime) / Mathf.Max(0.01f, section.endTime - section.startTime);
                rightX = LeftEdge + normalizedRight * UsableWidth;
            }

            float width = rightX - startX;
            if (hasTechnique)
                width = Mathf.Max(owner.tabSustainMinWidth, width);

            if (width <= 0.01f)
                return null;

            float height = Mathf.Max(owner.tabSustainThickness, owner.tabTechniqueTunnelHeight);
            float depth = Mathf.Max(owner.tabSustainDepth, owner.tabTechniqueTunnelDepth);
            float radius = height * 0.5f;
            float centerX = startX + width * 0.5f;

            GameObject root = new GameObject($"TechniqueTunnel_{note.id}");
            root.transform.SetParent(Root.transform, false);
            root.transform.position = new Vector3(centerX, y, owner.tabZDepth - 0.07f);

            Color outlineColor = owner.GetStringColor(note.stringIdx);
            Color fillColor = owner.tabTechniqueFillColor;

            CreateCapsulePiece(root.transform, Vector3.zero, new Vector3(Mathf.Max(0.01f, width - height), height, depth), PrimitiveType.Cube, outlineColor, 0.9f, extraRenderers);
            CreateCapsulePiece(root.transform, new Vector3(-(width * 0.5f) + radius, 0f, 0f), new Vector3(radius, depth * 0.5f, radius), PrimitiveType.Cylinder, outlineColor, 0.9f, extraRenderers);
            CreateCapsulePiece(root.transform, new Vector3((width * 0.5f) - radius, 0f, 0f), new Vector3(radius, depth * 0.5f, radius), PrimitiveType.Cylinder, outlineColor, 0.9f, extraRenderers);

            float innerHeight = Mathf.Max(0.03f, height - owner.tabTechniqueInnerPadding * 2f);
            float innerWidth = Mathf.Max(0.02f, width - owner.tabTechniqueInnerPadding * 2f);
            float innerRadius = innerHeight * 0.5f;
            CreateCapsulePiece(root.transform, new Vector3(0f, 0f, -0.015f), new Vector3(Mathf.Max(0.01f, innerWidth - innerHeight), innerHeight, depth * 0.55f), PrimitiveType.Cube, fillColor, 0.2f, extraRenderers);
            CreateCapsulePiece(root.transform, new Vector3(-(innerWidth * 0.5f) + innerRadius, 0f, -0.015f), new Vector3(innerRadius, depth * 0.28f, innerRadius), PrimitiveType.Cylinder, fillColor, 0.2f, extraRenderers);
            CreateCapsulePiece(root.transform, new Vector3((innerWidth * 0.5f) - innerRadius, 0f, -0.015f), new Vector3(innerRadius, depth * 0.28f, innerRadius), PrimitiveType.Cylinder, fillColor, 0.2f, extraRenderers);

            string glyph = GetTechniqueGlyph(note);
            if (!string.IsNullOrEmpty(glyph))
            {
                GameObject glyphObj = new GameObject($"TechniqueGlyph_{note.id}");
                glyphObj.transform.SetParent(root.transform, false);

                float visibleMiddleX = 0f;
                float glyphYOffset = Mathf.Max(height * 0.82f, owner.tabTechniqueGlyphFontSize * 0.08f);
                glyphObj.transform.localPosition = new Vector3(visibleMiddleX, glyphYOffset, -0.08f);

                TextMeshPro glyphText = glyphObj.AddComponent<TextMeshPro>();
                glyphText.text = glyph;
                glyphText.fontSize = owner.tabTechniqueGlyphFontSize;
                glyphText.alignment = TextAlignmentOptions.Center;
                glyphText.color = owner.tabTechniqueGlyphColor;
                glyphText.enableAutoSizing = false;
                glyphText.sortingOrder = 28;
                extraTexts.Add(glyphText);
            }

            return root;
        }

        private float GetVisibleNoteRadius()
        {
            return Mathf.Max(0.01f, owner.tabNoteCircleDiameter * 0.25f);
        }

        private float GetPlainSustainCutBeforeNextNote()
        {
            // Increase this to cut plain sustains earlier before the next note.
            return 0.12f;
        }

        private float GetPlainSustainMinVisibleWidth()
        {
            // If a plain sustain would be shorter than this, it is not drawn.
            return 0.16f;
        }

        private bool TryFindNextNoteInSection(TabSectionData section, NoteData current, out NoteData next)
        {
            next = default;
            if (section == null || current.id < 0)
                return false;

            bool found = false;

            for (int i = 0; i < section.noteIds.Count; i++)
            {
                if (!owner.TryGetChartNoteById(section.noteIds[i], out NoteData candidate))
                    continue;

                if (candidate.id == current.id)
                    continue;

                if (candidate.time <= current.time + 0.0001f)
                    continue;

                if (candidate.stringIdx != current.stringIdx)
                    continue;

                if (!found || candidate.time < next.time)
                {
                    next = candidate;
                    found = true;
                }
            }

            return found;
        }

        private void CreateCapsulePiece(Transform parent, Vector3 localPos, Vector3 scale, PrimitiveType primitiveType, Color color, float emission, List<Renderer> extraRenderers)
        {
            GameObject go = GameObject.CreatePrimitive(primitiveType);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            if (primitiveType == PrimitiveType.Cylinder)
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = scale;
            Renderer renderer = go.GetComponent<Renderer>();
            renderer.material = owner.CreateSharedGlowMaterial(color, emission);
            extraRenderers.Add(renderer);
        }

        private string GetTechniqueGlyph(NoteData note)
        {
            switch (note.technique)
            {
                case NoteTechnique.Slide:
                    return note.slideTargetFret >= 0 && note.slideTargetFret < note.fret ? "\\" : "/";
                case NoteTechnique.HammerOn:
                    return "H";
                case NoteTechnique.PullOff:
                    return "P";
                case NoteTechnique.Bend:
                    return "^";
                case NoteTechnique.Vibrato:
                    return "~";
                default:
                    return string.Empty;
            }
        }

        public void ClearAndHide()
        {
            ClearDynamic();
            NoteViews.Clear();
            SectionIndex = -1;
            Root.SetActive(false);
        }

        public void SetAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);

            foreach (Renderer r in staticRenderers)
            {
                if (r == null || r.material == null)
                    continue;

                Color c = r.material.color;
                c.a = alpha;
                r.material.color = c;
            }

            if (sectionLabel != null)
            {
                Color c = sectionLabel.color;
                c.a = alpha;
                sectionLabel.color = c;
            }

            foreach (var kv in NoteViews)
                kv.Value.SetAlpha(alpha);
        }

        private void CreateBorder()
        {
            CreateBorderSegment(new Vector3(0f, owner.tabPanelHeight * 0.5f, 0f), new Vector3(owner.tabPanelWidth, owner.tabBorderThickness, owner.tabBorderDepth));
            CreateBorderSegment(new Vector3(0f, -owner.tabPanelHeight * 0.5f, 0f), new Vector3(owner.tabPanelWidth, owner.tabBorderThickness, owner.tabBorderDepth));
            CreateBorderSegment(new Vector3(-owner.tabPanelWidth * 0.5f, 0f, 0f), new Vector3(owner.tabBorderThickness, owner.tabPanelHeight, owner.tabBorderDepth));
            CreateBorderSegment(new Vector3(owner.tabPanelWidth * 0.5f, 0f, 0f), new Vector3(owner.tabBorderThickness, owner.tabPanelHeight, owner.tabBorderDepth));
        }

        private void CreateBorderSegment(Vector3 localPosition, Vector3 localScale)
        {
            GameObject border = GameObject.CreatePrimitive(PrimitiveType.Cube);
            border.transform.SetParent(Root.transform, false);
            border.transform.localPosition = localPosition;
            border.transform.localScale = localScale;
            Renderer renderer = border.GetComponent<Renderer>();
            renderer.material = owner.CreateSharedGlowMaterial(owner.tabBorderColor, 0.4f);
            staticRenderers.Add(renderer);
        }

        private void CreateStrings()
        {
            for (int i = 0; i < 6; i++)
            {
                GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                line.name = $"TabString_{i}";
                line.transform.SetParent(Root.transform, false);
                line.transform.localPosition = new Vector3(0f, GetLocalStringY(i), 0f);

                // Keep lines visually behind the markers.
                line.transform.localScale = new Vector3(
                    owner.tabPanelWidth - (owner.tabHorizontalPadding * 0.8f),
                    owner.tabStringThickness,
                    owner.tabStringDepth
                );

                Renderer renderer = line.GetComponent<Renderer>();
                renderer.material = owner.CreateSharedGlowMaterial(owner.GetStringColor(i), 0.25f);

                staticRenderers.Add(renderer);
            }
        }

        private float GetStringY(int stringIdx)
        {
            return CenterY + GetLocalStringY(stringIdx);
        }

        private float GetLocalStringY(int stringIdx)
        {
            int row = owner.invertStrings ? stringIdx : (5 - stringIdx);
            float centered = ((5 * 0.5f) - row) * lineSpacing;
            return centered;
        }

        private void ClearDynamic()
        {
            for (int i = 0; i < dynamicObjects.Count; i++)
            {
                if (dynamicObjects[i] != null)
                    Object.Destroy(dynamicObjects[i]);
            }

            dynamicObjects.Clear();
        }
    }

    private sealed class TabNoteView
    {
        private readonly Renderer outlineRenderer;
        private readonly Renderer fillRenderer;
        private readonly TextMeshPro text;
        private readonly List<Renderer> extraRenderers;
        private readonly List<TextMeshPro> extraTexts;

        public TabNoteView(Renderer outlineRenderer, Renderer fillRenderer, TextMeshPro text, List<Renderer> extraRenderers, List<TextMeshPro> extraTexts)
        {
            this.outlineRenderer = outlineRenderer;
            this.fillRenderer = fillRenderer;
            this.text = text;
            this.extraRenderers = extraRenderers ?? new List<Renderer>();
            this.extraTexts = extraTexts ?? new List<TextMeshPro>();
        }

        public void SetStateColors(Color outlineColor, Color fillColor, Color textColor, bool emphasize)
        {
            if (outlineRenderer != null)
            {
                outlineRenderer.material.color = outlineColor;
                outlineRenderer.material.EnableKeyword("_EMISSION");
                outlineRenderer.material.SetColor("_EmissionColor", outlineColor * Mathf.Pow(2f, emphasize ? 2.2f : 0.6f));
            }

            if (fillRenderer != null)
            {
                fillRenderer.material.color = fillColor;
                fillRenderer.material.EnableKeyword("_EMISSION");
                fillRenderer.material.SetColor("_EmissionColor", fillColor * Mathf.Pow(2f, emphasize ? 1.4f : 0.2f));
            }

            if (text != null)
                text.color = textColor;

            for (int i = 0; i < extraRenderers.Count; i++)
            {
                Renderer r = extraRenderers[i];
                if (r == null || r.material == null)
                    continue;

                bool isOutlineLike = i < 3;
                Color targetColor = isOutlineLike ? outlineColor : fillColor;
                r.material.color = targetColor;
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor", targetColor * Mathf.Pow(2f, emphasize ? (isOutlineLike ? 1.6f : 0.8f) : 0.25f));
            }

            for (int i = 0; i < extraTexts.Count; i++)
            {
                if (extraTexts[i] != null)
                    extraTexts[i].color = textColor;
            }
        }

        public void SetAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);

            if (outlineRenderer != null && outlineRenderer.material != null)
            {
                Color c = outlineRenderer.material.color;
                c.a = alpha;
                outlineRenderer.material.color = c;
            }

            if (fillRenderer != null && fillRenderer.material != null)
            {
                Color c = fillRenderer.material.color;
                c.a = alpha;
                fillRenderer.material.color = c;
            }

            if (text != null)
            {
                Color c = text.color;
                c.a = alpha;
                text.color = c;
            }

            for (int i = 0; i < extraRenderers.Count; i++)
            {
                Renderer r = extraRenderers[i];
                if (r == null || r.material == null)
                    continue;
                Color c = r.material.color;
                c.a = alpha;
                r.material.color = c;
            }

            for (int i = 0; i < extraTexts.Count; i++)
            {
                if (extraTexts[i] == null)
                    continue;
                Color c = extraTexts[i].color;
                c.a = alpha;
                extraTexts[i].color = c;
            }
        }
    }
}
