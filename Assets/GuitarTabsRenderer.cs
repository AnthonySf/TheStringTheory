using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class GuitarTabsRenderer : IGuitarGameplayRenderer
{
    private readonly Dictionary<int, GameplayNoteState> stateById = new Dictionary<int, GameplayNoteState>();
    private readonly Dictionary<int, TabSectionData> sectionByIndex = new Dictionary<int, TabSectionData>();

    private GuitarBridgeServer owner;
    private Camera mainCamera;
    private GameObject root;
    private TabPanelView topPanel;
    private TabPanelView bottomPanel;
    private TabPanelView reservePanel;
    private GameObject playhead;
    private GameObject loopMarkerStart;
    private GameObject loopMarkerEnd;


    private int displayedTopSectionIndex = -999;
    private int displayedBottomSectionIndex = -999;

    private bool isTransitioning;
    private float transitionElapsed;
    private TabPanelView transitionOutgoingPanel;
    private TabPanelView transitionIncomingPanel;
    private TabPanelView transitionReservePanel;
    private int queuedBottomSectionIndex = -1;
    private int queuedTopSectionIndex = -1;
    private bool transitionIsReverse;

    private TabsSongHeaderOverlay songHeaderOverlay;
    private ITabsBackgroundEffect backgroundEffect;
    private GameObject backgroundRoot;
    private bool gameplayVisualsVisible = true;

    public void Initialize(GuitarBridgeServer owner, List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        this.owner = owner;
        mainCamera = Camera.main;

        root = new GameObject("TabsRendererRoot");
        backgroundRoot = new GameObject("TabsBackgroundRoot");
        backgroundRoot.transform.SetParent(root.transform, false);

        InitializeBackgroundEffect();

        topPanel = new TabPanelView(root.transform, "TopTabPanel", owner, true);
        bottomPanel = new TabPanelView(root.transform, "BottomTabPanel", owner, false);
        reservePanel = new TabPanelView(root.transform, "ReserveTabPanel", owner, false);

        playhead = GameObject.CreatePrimitive(PrimitiveType.Cube);
        playhead.name = "TabPlayhead";
        playhead.transform.SetParent(root.transform, false);
        Renderer playheadRenderer = playhead.GetComponent<Renderer>();
        playheadRenderer.material = CreateGlowMaterial(owner.tabPlayheadColor, 4f);
        ConfigureRendererNoShadows(playheadRenderer);

        songHeaderOverlay = new TabsSongHeaderOverlay(owner);

        loopMarkerStart = GameObject.CreatePrimitive(PrimitiveType.Cube);
        loopMarkerStart.name = "LoopMarkerStart";
        loopMarkerStart.transform.SetParent(root.transform, false);
        Renderer loopStartRenderer = loopMarkerStart.GetComponent<Renderer>();
        loopStartRenderer.material = CreateGlowMaterial(new Color(1f, 0.2f, 0.2f, 0.95f), 4f);
        ConfigureRendererNoShadows(loopStartRenderer);

        loopMarkerEnd = GameObject.CreatePrimitive(PrimitiveType.Cube);
        loopMarkerEnd.name = "LoopMarkerEnd";
        loopMarkerEnd.transform.SetParent(root.transform, false);
        Renderer loopEndRenderer = loopMarkerEnd.GetComponent<Renderer>();
        loopEndRenderer.material = CreateGlowMaterial(new Color(1f, 0.2f, 0.2f, 0.95f), 4f);
        ConfigureRendererNoShadows(loopEndRenderer);

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
        InitializeBackgroundEffect();

        displayedTopSectionIndex = -999;
        displayedBottomSectionIndex = -999;
        isTransitioning = false;
        transitionElapsed = 0f;
        queuedBottomSectionIndex = -1;
        queuedTopSectionIndex = -1;
        transitionIsReverse = false;

        topPanel.ClearAndHide();
        bottomPanel.ClearAndHide();
        reservePanel.ClearAndHide();

        SetPanelWorldY(topPanel, owner.TabTopPanelY);
        SetPanelWorldY(bottomPanel, owner.TabBottomPanelY);
        SetPanelWorldY(reservePanel, GetReserveBelowY());
    }

    public void Render(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null || mainCamera == null)
            return;

        ConfigureCamera();
        RefreshStateCache(snapshot.noteStates);

        bool suppressGameplay = snapshot.mainMenuFlowActive;
        SetGameplayVisualsVisible(!suppressGameplay);

        if (!suppressGameplay)
        {
            if (displayedTopSectionIndex == -999)
                BuildInitialPanels(snapshot);

            HandleSectionPaging(snapshot);
            UpdateTransition();
            UpdatePanelColors(topPanel);
            UpdatePanelColors(bottomPanel);
            UpdatePlayhead(snapshot);
            UpdateLoopMarkers(snapshot);
        }

        backgroundEffect?.Tick(Time.deltaTime);
        songHeaderOverlay?.UpdateFromSnapshot(snapshot);
    }

    private void SetGameplayVisualsVisible(bool visible)
    {
        if (gameplayVisualsVisible == visible)
            return;

        gameplayVisualsVisible = visible;

        if (!visible)
        {
            topPanel?.Root?.SetActive(false);
            bottomPanel?.Root?.SetActive(false);
            reservePanel?.Root?.SetActive(false);
            if (playhead != null)
                playhead.SetActive(false);
            if (loopMarkerStart != null)
                loopMarkerStart.SetActive(false);
            if (loopMarkerEnd != null)
                loopMarkerEnd.SetActive(false);
            return;
        }

        displayedTopSectionIndex = -999;
        displayedBottomSectionIndex = -999;
        isTransitioning = false;
        transitionElapsed = 0f;
        queuedBottomSectionIndex = -1;
        queuedTopSectionIndex = -1;
        transitionIsReverse = false;
    }

    public void DisposeRenderer()
    {
        songHeaderOverlay?.Dispose();
        songHeaderOverlay = null;

        backgroundEffect?.Dispose();
        backgroundEffect = null;

        if (root != null)
            UnityEngine.Object.Destroy(root);

        backgroundRoot = null;
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

    private void InitializeBackgroundEffect()
    {
        backgroundEffect?.Dispose();
        backgroundEffect = null;

        if (backgroundRoot == null)
            return;

        switch (owner.tabBackgroundMode)
        {
            case GuitarBridgeServer.TabsBackgroundMode.BlueSky:
                backgroundEffect = new TabsBlueSkyBackground();
                break;
            case GuitarBridgeServer.TabsBackgroundMode.Starfield:
                backgroundEffect = new TabsStarfieldBackground();
                break;
            case GuitarBridgeServer.TabsBackgroundMode.SolidColor:
            default:
                return;
        }

        backgroundEffect.Initialize(backgroundRoot.transform, owner);
    }

    private void BuildInitialPanels(GuitarGameplaySnapshot snapshot)
    {
        displayedTopSectionIndex = snapshot.currentSectionIndex;
        displayedBottomSectionIndex = snapshot.nextSectionIndex;

        SetPanelWorldY(topPanel, owner.TabTopPanelY);
        SetPanelWorldY(bottomPanel, owner.TabBottomPanelY);
        SetPanelWorldY(reservePanel, GetReserveBelowY());

        topPanel.Build(GetSection(displayedTopSectionIndex));
        bottomPanel.Build(GetSection(displayedBottomSectionIndex));
        reservePanel.Build(GetSection(displayedBottomSectionIndex + 1));
        reservePanel.SetAlpha(1f);
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
            transitionReservePanel = reservePanel;
            queuedBottomSectionIndex = snapshot.currentSectionIndex + 1;
            SetPanelWorldY(transitionReservePanel, GetReserveBelowY());
            transitionReservePanel.Build(GetSection(queuedBottomSectionIndex));
            transitionReservePanel.SetAlpha(1f);
            return;
        }

        if (snapshot.currentSectionIndex + 1 == displayedTopSectionIndex)
        {
            isTransitioning = true;
            transitionElapsed = 0f;
            transitionIsReverse = true;
            transitionOutgoingPanel = bottomPanel;
            transitionIncomingPanel = topPanel;
            transitionReservePanel = reservePanel;
            queuedTopSectionIndex = snapshot.currentSectionIndex;
            SetPanelWorldY(transitionReservePanel, GetReserveAboveY());
            transitionReservePanel.Build(GetSection(queuedTopSectionIndex));
            transitionReservePanel.SetAlpha(1f);
            return;
        }

        displayedTopSectionIndex = snapshot.currentSectionIndex;
        displayedBottomSectionIndex = snapshot.nextSectionIndex;

        SetPanelWorldY(topPanel, owner.TabTopPanelY);
        SetPanelWorldY(bottomPanel, owner.TabBottomPanelY);
        SetPanelWorldY(reservePanel, GetReserveBelowY());

        topPanel.Build(GetSection(displayedTopSectionIndex));
        bottomPanel.Build(GetSection(displayedBottomSectionIndex));
        reservePanel.Build(GetSection(displayedBottomSectionIndex + 1));
        reservePanel.SetAlpha(1f);
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
        float outgoingEndY = transitionIsReverse ? GetReserveBelowY() : GetReserveAboveY();
        float reserveStartY = transitionIsReverse ? GetReserveAboveY() : GetReserveBelowY();
        float reserveEndY = transitionIsReverse ? owner.TabTopPanelY : owner.TabBottomPanelY;

        float incomingY = Mathf.Lerp(incomingStartY, incomingEndY, t);
        float outgoingY = Mathf.Lerp(outgoingStartY, outgoingEndY, t);
        float reserveY = Mathf.Lerp(reserveStartY, reserveEndY, t);

        SetPanelWorldY(transitionIncomingPanel, incomingY);
        SetPanelWorldY(transitionOutgoingPanel, outgoingY);
        SetPanelWorldY(transitionReservePanel, reserveY);
        transitionOutgoingPanel.SetAlpha(1f - t);

        if (t < 1f)
            return;

        transitionOutgoingPanel.SetAlpha(1f);

        if (!transitionIsReverse)
        {
            TabPanelView oldTop = topPanel;
            topPanel = bottomPanel;
            bottomPanel = reservePanel;
            reservePanel = oldTop;

            displayedTopSectionIndex = topPanel.SectionIndex;
            displayedBottomSectionIndex = queuedBottomSectionIndex;

            SetPanelWorldY(topPanel, owner.TabTopPanelY);
            SetPanelWorldY(bottomPanel, owner.TabBottomPanelY);
            SetPanelWorldY(reservePanel, GetReserveBelowY());

            reservePanel.Build(GetSection(displayedBottomSectionIndex + 1));
            bottomPanel.SetAlpha(1f);
            reservePanel.SetAlpha(1f);
        }
        else
        {
            TabPanelView oldBottom = bottomPanel;
            bottomPanel = topPanel;
            topPanel = reservePanel;
            reservePanel = oldBottom;

            displayedTopSectionIndex = queuedTopSectionIndex;
            displayedBottomSectionIndex = bottomPanel.SectionIndex;

            SetPanelWorldY(topPanel, owner.TabTopPanelY);
            SetPanelWorldY(bottomPanel, owner.TabBottomPanelY);
            SetPanelWorldY(reservePanel, GetReserveAboveY());

            reservePanel.Build(GetSection(displayedTopSectionIndex - 1));
            topPanel.SetAlpha(1f);
            reservePanel.SetAlpha(1f);
        }

        isTransitioning = false;
        transitionElapsed = 0f;
        transitionOutgoingPanel = null;
        transitionIncomingPanel = null;
        transitionReservePanel = null;
        queuedBottomSectionIndex = -1;
        queuedTopSectionIndex = -1;
        transitionIsReverse = false;
    }

    private float GetReserveBelowY()
    {
        return owner.TabBottomPanelY - owner.tabPanelLiftDistance;
    }

    private float GetReserveAboveY()
    {
        return owner.TabTopPanelY + owner.tabPanelLiftDistance;
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

        panel.RefreshStaticStyle();

        foreach (var kv in panel.NoteViews)
        {
            if (!stateById.TryGetValue(kv.Key, out GameplayNoteState state))
                continue;

            TabNoteView noteView = kv.Value;

            if (state.IsHit)
            {
                noteView.SetStateColors(owner.tabHitColor, owner.tabHitColor, Color.white, true, null);
            }
            else if (state.IsMissed)
            {
                noteView.SetStateColors(owner.tabMissColor, owner.tabMissColor, Color.white, false, null);
            }
            else if (state.isJudgeable)
            {
                Color outline = owner.GetStringColor(state.data.stringIdx);
                noteView.SetStateColors(outline, owner.tabJudgeableColor, Color.white, true, null);
            }
            else
            {
                Color outline = owner.GetStringColor(state.data.stringIdx);
                Color fill = owner.GetDarkenedStringColor(state.data.stringIdx, owner.tabIdleFillDarken);
                noteView.SetStateColors(outline, fill, Color.white, false, null);
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
        if (owner == null)
            throw new InvalidOperationException("Cannot create glow material because renderer owner is missing.");

        return owner.CreateSharedGlowMaterial(c, intensity);
    }

    private static void ConfigureRendererNoShadows(Renderer renderer)
    {
        if (renderer == null)
            return;

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private sealed class TabPanelView
    {
        private readonly GuitarBridgeServer owner;
        private readonly float lineSpacing;
        private readonly List<Renderer> staticRenderers = new List<Renderer>();
        private readonly List<GameObject> dynamicObjects = new List<GameObject>();
        private Renderer backdropRenderer;
        private float panelAlpha = 1f;
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

            CreateBackdrop();
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
                ConfigureRendererNoShadows(outlineRenderer);
                fillRenderer.material = owner.CreateSharedGlowMaterial(owner.GetDarkenedStringColor(note.stringIdx, owner.tabIdleFillDarken), 0.3f);
                ConfigureRendererNoShadows(fillRenderer);

                GameObject textObj = new GameObject($"Label_{note.id}");
                textObj.transform.SetParent(markerRoot.transform, false);
                textObj.transform.localPosition = new Vector3(0f, 0f, -0.08f);

                TextMeshPro text = textObj.AddComponent<TextMeshPro>();
                text.text = GetNoteLabelText(note);
                text.fontSize = owner.tabNoteFontSize;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.enableAutoSizing = false;
                text.sortingOrder = 30;

                dynamicObjects.Add(markerRoot);

                List<Renderer> extraRenderers = new List<Renderer>();
                List<TextMeshPro> extraTexts = new List<TextMeshPro>();
                AddCornerTechniqueGlyph(markerRoot.transform, note, extraTexts);
                GameObject tunnelRoot = BuildTechniqueTunnel(note, section, x, y, extraRenderers, extraTexts);
                if (tunnelRoot != null)
                    dynamicObjects.Add(tunnelRoot);

                NoteViews[note.id] = new TabNoteView(outlineRenderer, fillRenderer, text, extraRenderers, extraTexts);
            }
        }


        private static string GetNoteLabelText(NoteData note)
        {
            if (note.fret < 0)
                return "X";

            string noteName = note.note ?? string.Empty;
            if (noteName.Equals("x", StringComparison.OrdinalIgnoreCase) || noteName.Equals("mute", StringComparison.OrdinalIgnoreCase) || noteName.Equals("muted", StringComparison.OrdinalIgnoreCase))
                return "X";

            return Mathf.Max(0, note.fret).ToString();
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

            if (note.technique == NoteTechnique.Slide)
                CreateSlideDirectionLine(root.transform, width, height, depth, note, extraRenderers);

            string glyph = GetTechniqueGlyph(note);
            if (!string.IsNullOrEmpty(glyph) && note.technique != NoteTechnique.Bend && note.technique != NoteTechnique.Vibrato)
            {
                GameObject glyphObj = new GameObject($"TechniqueGlyph_{note.id}");
                glyphObj.transform.SetParent(root.transform, false);

                float visibleMiddleX = 0f;
                float maxYOffsetBeforeNextString = lineSpacing * 0.33f;
                float glyphYOffset = Mathf.Min(Mathf.Max(height * 0.72f, owner.tabTechniqueGlyphFontSize * 0.10f), maxYOffsetBeforeNextString);
                glyphObj.transform.localPosition = new Vector3(visibleMiddleX, glyphYOffset, -0.08f);

                TextMeshPro glyphText = glyphObj.AddComponent<TextMeshPro>();
                glyphText.text = glyph;
                glyphText.fontSize = owner.tabTechniqueGlyphFontSize * 1.35f;
                glyphText.alignment = TextAlignmentOptions.Center;
                glyphText.color = owner.tabTechniqueGlyphColor;
                glyphText.enableAutoSizing = false;
                glyphText.fontStyle = FontStyles.Bold;
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
            ConfigureRendererNoShadows(renderer);
            extraRenderers.Add(renderer);
        }

        private void CreateSlideDirectionLine(Transform parent, float width, float height, float depth, NoteData note, List<Renderer> extraRenderers)
        {
            GameObject lineObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lineObj.name = $"SlideDirection_{note.id}";
            lineObj.transform.SetParent(parent, false);

            float xSpan = Mathf.Max(0.08f, width - Mathf.Max(0.08f, height * 0.35f));
            float ySpan = Mathf.Max(0.03f, height * 0.62f);
            float lineLength = Mathf.Sqrt(xSpan * xSpan + ySpan * ySpan);
            float angle = Mathf.Atan2(ySpan, xSpan) * Mathf.Rad2Deg;
            bool descending = note.slideTargetFret >= 0 && note.slideTargetFret < note.fret;

            lineObj.transform.localRotation = Quaternion.Euler(0f, 0f, descending ? -angle : angle);
            lineObj.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            lineObj.transform.localScale = new Vector3(lineLength, Mathf.Max(0.05f, height * 0.20f), Mathf.Max(0.03f, depth * 0.35f));

            Renderer lineRenderer = lineObj.GetComponent<Renderer>();
            lineRenderer.material = owner.CreateSharedGlowMaterial(new Color(1f, 1f, 1f, 0.95f), 1.4f);
            ConfigureRendererNoShadows(lineRenderer);
            extraRenderers.Add(lineRenderer);
        }

        private void AddCornerTechniqueGlyph(Transform noteRoot, NoteData note, List<TextMeshPro> extraTexts)
        {
            if (note.technique != NoteTechnique.Bend && note.technique != NoteTechnique.Vibrato)
                return;

            string glyph = GetTechniqueGlyph(note);
            if (string.IsNullOrEmpty(glyph))
                return;

            GameObject glyphObj = new GameObject($"NoteCornerTechnique_{note.id}");
            glyphObj.transform.SetParent(noteRoot, false);

            float xOffset = Mathf.Max(owner.tabNoteCircleDiameter * 0.24f, 0.11f);
            float yOffset = Mathf.Max(owner.tabNoteCircleDiameter * 0.18f, 0.08f);
            glyphObj.transform.localPosition = new Vector3(xOffset, yOffset, -0.09f);

            TextMeshPro glyphText = glyphObj.AddComponent<TextMeshPro>();
            glyphText.text = glyph;
            glyphText.fontSize = owner.tabTechniqueGlyphFontSize * 1.48f;
            glyphText.alignment = TextAlignmentOptions.Center;
            glyphText.color = owner.tabTechniqueGlyphColor;
            glyphText.enableAutoSizing = false;
            glyphText.fontStyle = FontStyles.Bold;
            glyphText.sortingOrder = 31;
            extraTexts?.Add(glyphText);
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
            panelAlpha = Mathf.Clamp01(alpha);

            foreach (Renderer r in staticRenderers)
            {
                if (r == null || r.material == null)
                    continue;

                Color c = r.material.color;
                if (r == backdropRenderer)
                {
                    Color baseColor = owner.tabPanelBackdropColor;
                    c.r = baseColor.r;
                    c.g = baseColor.g;
                    c.b = baseColor.b;
                    c.a = baseColor.a * panelAlpha;
                }
                else
                {
                    c.a = panelAlpha;
                }
                r.material.color = c;
                r.material.SetColor("_Color", c);
                r.material.SetColor("_BaseColor", c);
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

        public void RefreshStaticStyle()
        {
            if (backdropRenderer == null || backdropRenderer.material == null)
                return;

            Color c = owner.tabPanelBackdropColor;
            c.a *= panelAlpha;
            backdropRenderer.material.color = c;
            backdropRenderer.material.SetColor("_Color", c);
            backdropRenderer.material.SetColor("_BaseColor", c);
        }

        private void CreateBackdrop()
        {
            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "TabsBackdrop";
            backdrop.transform.SetParent(Root.transform, false);
            backdrop.transform.localPosition = new Vector3(0f, 0f, owner.tabZDepth + 0.22f);
            backdrop.transform.localScale = new Vector3(
                owner.tabPanelWidth - 0.18f,
                owner.tabPanelHeight - 0.16f,
                1f
            );

            backdropRenderer = backdrop.GetComponent<Renderer>();
            backdropRenderer.material = owner.CreateSharedTransparentMaterial(owner.tabPanelBackdropColor, 0f);
            backdropRenderer.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 2;
            backdropRenderer.material.SetInt("_ZWrite", 0);
            backdropRenderer.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            backdropRenderer.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            backdropRenderer.material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            backdropRenderer.material.EnableKeyword("_ALPHABLEND_ON");
            ConfigureRendererNoShadows(backdropRenderer);
            UnityEngine.Object.Destroy(backdrop.GetComponent<Collider>());
            staticRenderers.Add(backdropRenderer);
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
            ConfigureRendererNoShadows(renderer);
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
                ConfigureRendererNoShadows(renderer);

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
                    UnityEngine.Object.Destroy(dynamicObjects[i]);
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
        private readonly string defaultLabelText;

        public TabNoteView(Renderer outlineRenderer, Renderer fillRenderer, TextMeshPro text, List<Renderer> extraRenderers, List<TextMeshPro> extraTexts)
        {
            this.outlineRenderer = outlineRenderer;
            this.fillRenderer = fillRenderer;
            this.text = text;
            this.extraRenderers = extraRenderers ?? new List<Renderer>();
            this.extraTexts = extraTexts ?? new List<TextMeshPro>();
            defaultLabelText = text != null ? text.text : string.Empty;
        }

        public void SetStateColors(Color outlineColor, Color fillColor, Color textColor, bool emphasize, string textOverride)
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
            {
                text.color = textColor;
                text.text = string.IsNullOrEmpty(textOverride) ? defaultLabelText : textOverride;
            }

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
                r.material.SetColor("_Color", c);
                r.material.SetColor("_BaseColor", c);
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
