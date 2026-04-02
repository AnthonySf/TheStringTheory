using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class GuitarHighway3DRenderer : IGuitarGameplayRenderer
{
    private readonly Dictionary<int, NoteData> chartById = new Dictionary<int, NoteData>();
    private readonly Dictionary<int, List<NoteData>> chordGroups = new Dictionary<int, List<NoteData>>();
    private readonly Dictionary<int, HighwayNoteView> noteViews = new Dictionary<int, HighwayNoteView>();
    private readonly Dictionary<int, GameObject> chordFrames = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, int> slideDestinationBySourceId = new Dictionary<int, int>();
    private readonly Dictionary<int, int> bendDestinationBySourceId = new Dictionary<int, int>();
    private readonly Dictionary<int, int> bendSourceByDestinationId = new Dictionary<int, int>();
    private readonly Dictionary<int, GameplayNoteState> noteStatesById = new Dictionary<int, GameplayNoteState>();
    private readonly Dictionary<int, string> noteLaneTagTextById = new Dictionary<int, string>();
    private readonly List<LaneHighlightChunk> laneHighlightChunks = new List<LaneHighlightChunk>();
    private readonly HashSet<int> debugLoggedBendProfileIds = new HashSet<int>();
    private readonly HashSet<int> debugLoggedBendNearStrikeIds = new HashSet<int>();
    private readonly HashSet<int> visibleNoteIdsThisFrame = new HashSet<int>();
    private readonly List<int> noteViewRemovalBuffer = new List<int>();
    private readonly HashSet<int> activeChordIdsThisFrame = new HashSet<int>();
    private readonly List<int> chordFrameRemovalBuffer = new List<int>();
    private Mesh techniqueRibbonMesh;
    private Material sharedTechniqueRibbonMaterial;
    private Material sharedBendArrowMaterial;
    private Material sharedMuteSymbolMaterial;
    private Material sharedHighwayCharacterMaterial;

    private GuitarBridgeServer owner;
    private Camera mainCamera;
    private Camera backgroundCamera;
    private GameObject root;
    private GameObject gameplayRoot;
    private GameObject characterRoot;
    private Renderer highwayCharacterRenderer;
    private Texture2D highwayCharacterTexture;
    private float highwayCharacterAspect = 1f;
    private readonly GameObject[] stringVisuals = new GameObject[6];
    private readonly Material[] stringVisualMats = new Material[6];
    private readonly Renderer[] stringVisualRenderers = new Renderer[6];
    private readonly Dictionary<int, TextMeshPro> fretNumberLabels = new Dictionary<int, TextMeshPro>();
    private Material[] fretBoundaryMats;
    private Renderer[] fretBoundaryRenderers;
    private Material[] laneSurfaceMats;
    private Renderer[] laneSurfaceRenderers;
    private Material[] laneGuideMats;
    private Renderer[] laneGuideRenderers;
    private Material[,] fretLightMats;
    private Renderer[,] fretLightRenderers;
    private ITabsBackgroundEffect backgroundEffect;
    private GameObject backgroundRoot;
    private bool backgroundUsingMenuMode = true;
    private TabsSongHeaderOverlay songHeaderOverlay;
    private int originalMainCameraCullingMask = -1;
    private CameraClearFlags originalMainCameraClearFlags; 
    private float originalMainCameraDepth;
    private float currentVisualNoteSpeed = 12f;
    private bool currentNoteByNoteModeEnabled;
    private bool currentNoteByNoteWaitingForMatch;
    private float cameraTargetX;
    private float cameraTargetFOV = 60f;
    private float cameraXVelocity;
    private float cameraFovVelocity;
    private bool gameplayVisualsVisible = true;
    private bool gameplayBuilt;
    private const int BackgroundLayer = 2;
    private const float HighwayCharacterViewportMarginX = 0.035f;
    private const float HighwayCharacterViewportMarginY = 0.035f;
    private const float HighwayCharacterDepth = 44f;
    private const float HighwayCharacterHeightViewportFraction = 1.02f;
    private const float HighwayCharacterAdditionalLeftOffsetInWidths = 0.25f;
    private const float HighwayCharacterAdditionalDownOffsetInHeights = 0.35f;
    private const float HighwayCharacterBottomFadeStart01 = 0.62f;
    private const float HighwayCharacterBottomFadeEnd01 = 0.38f;
    private const float StringLaneSpacing = 1.2f;
    private const float BendRibbonVisualHeightInStrings = 2f;
    private const float BendRibbonLeadOutDistance = 0.9f;
    private const float BendRibbonCornerDepth = 0.25f;
    private const float BendRibbonCornerRoundness = 3f;
    private const float BendRibbonMinimumTopHoldDistance = 0.45f;
    private const float BendRibbonHeadMaximumFlatHoldSeconds = 1.2f;
    private const float BendRibbonFlatLightStrength = 0.85f;
    private const float BendRibbonDarkBandPaddingDistance = 0.32f;
    private const float BendArrowWidthFraction = 0.82f;
    private const float BendArrowFrontOffset = 0.035f;
    private const float BendArrowStackOffsetFraction = 0.72f;
    private const float LegatoCurveWidthFraction = 0.22f;
    private const int LegatoCurveSamples = 18;
    private const float MuteSymbolScaleFraction = 1.76f;
    private const float MuteSymbolFrontOffset = 0.04f;
    private const bool ForceMuteSymbolPreviewOnAllNotes = false;
    private const float VibratoRibbonAmplitudeInStrings = 0.42f;
    private const float VibratoCyclesPerSecond = 5f;
    private const int VibratoMinimumHalfWaves = 4;
    private const int VibratoMaximumHalfWaves = 12;
    private const bool DebugBendRibbonLogs = false;
    private string backgroundSignature = string.Empty;
    private static readonly int CurveP0ShaderId = Shader.PropertyToID("_CurveP0");
    private static readonly int CurveP1ShaderId = Shader.PropertyToID("_CurveP1");
    private static readonly int CurveP2ShaderId = Shader.PropertyToID("_CurveP2");
    private static readonly int CurveP3ShaderId = Shader.PropertyToID("_CurveP3");
    private static readonly int HalfWidthShaderId = Shader.PropertyToID("_HalfWidth");
    private static readonly int CenterColorShaderId = Shader.PropertyToID("_CenterColor");
    private static readonly int EdgeColorShaderId = Shader.PropertyToID("_EdgeColor");
    private static readonly int EmissionColorShaderId = Shader.PropertyToID("_EmissionColor");
    private static readonly int VisibleStart01ShaderId = Shader.PropertyToID("_VisibleStart01");
    private static readonly int VisibleFadeSoftness01ShaderId = Shader.PropertyToID("_VisibleFadeSoftness01");
    private static readonly int LengthFadeSoftness01ShaderId = Shader.PropertyToID("_LengthFadeSoftness01");
    private static readonly int FlatLightStrengthShaderId = Shader.PropertyToID("_FlatLightStrength");
    private static readonly int PathModeShaderId = Shader.PropertyToID("_PathMode");
    private static readonly int CornerRoundnessShaderId = Shader.PropertyToID("_CornerRoundness");
    private static readonly int DarkBandStart01ShaderId = Shader.PropertyToID("_DarkBandStart01");
    private static readonly int DarkBandEnd01ShaderId = Shader.PropertyToID("_DarkBandEnd01");
    private static readonly int BendArrowBaseColorShaderId = Shader.PropertyToID("_BaseColor");
    private static readonly int CharacterFadeStartShaderId = Shader.PropertyToID("_FadeStartY");
    private static readonly int CharacterFadeEndShaderId = Shader.PropertyToID("_FadeEndY");

    private struct TechniqueRibbonProfile
    {
        public Vector3 start;
        public Vector3 control1;
        public Vector3 control2;
        public Vector3 end;
        public float halfWidth;
        public float pathMode;
        public float cornerRoundness;
        public float darkBandStart01;
        public float darkBandEnd01;
    }

    private struct SlideRibbonFadeState
    {
        public bool freezeActive;
        public float fadeStartSongTime;
        public float fadeEndSongTime;
    }

    private sealed class LaneHighlightChunk
    {
        public float startTime;
        public float endTime;
        public bool[] laneSurfaceMask;
        public bool[] laneGuideMask;
    }

    public void Initialize(GuitarBridgeServer owner, List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        this.owner = owner;
        mainCamera = Camera.main;
        root = new GameObject("Highway3DRendererRoot");
        backgroundRoot = new GameObject("Highway3DBackgroundRoot");
        backgroundRoot.transform.SetParent(root.transform, false);
        characterRoot = new GameObject("Highway3DCharacterRoot");
        characterRoot.transform.SetParent(root.transform, false);
        gameplayRoot = new GameObject("Highway3DGameplayRoot");
        gameplayRoot.transform.SetParent(root.transform, false);
        originalMainCameraClearFlags = mainCamera != null ? mainCamera.clearFlags : CameraClearFlags.SolidColor;
        originalMainCameraCullingMask = mainCamera != null ? mainCamera.cullingMask : -1;
        originalMainCameraDepth = mainCamera != null ? mainCamera.depth : 0f;

        BuildChartCaches(chartNotes);
        BuildLaneHighlightChunks(chartNotes, sections);
        InitializeBackgroundEffect(menuMode: true);
        InitializeHighwayCharacter();
        InitializeBackgroundCamera();
        ConfigureCamera();
        songHeaderOverlay = new TabsSongHeaderOverlay(owner);
        gameplayBuilt = false;
    }

    public void ResetRenderer(List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        if (root != null)
            Object.Destroy(root);

        noteViews.Clear();
        chordFrames.Clear();
        fretNumberLabels.Clear();
        Initialize(owner, chartNotes, sections);
    }

    public void Render(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null)
            return;

        if (mainCamera == null)
            return;

        currentVisualNoteSpeed = GetVisualNoteSpeed(snapshot);
        currentNoteByNoteModeEnabled = snapshot.noteByNoteModeEnabled;
        currentNoteByNoteWaitingForMatch = snapshot.noteByNoteWaitingForMatch;

        bool suppressGameplay = snapshot.mainMenuFlowActive;
        EnsureBackgroundMode(suppressGameplay);
        ConfigureCamera();

        UpdateHighwayCharacterPlacement();

        if (!suppressGameplay)
            UpdateBackgroundPlacement();

        SetGameplayVisualsVisible(!suppressGameplay);

        if (!suppressGameplay)
        {
            EnsureGameplayVisualsBuilt();
            UpdateStringVisuals(snapshot);
            UpdateFretBoundaries(snapshot);
            UpdateLaneSurfaces(snapshot);
            UpdateLaneGuides(snapshot);
            UpdateFretboardLights(snapshot.latestDetectedPitches);
            UpdateNotes(snapshot);
            UpdateChordFrames(snapshot);
            UpdateSectionCamera(snapshot);
        }

        backgroundEffect?.Tick(Time.deltaTime);
        songHeaderOverlay?.UpdateFromSnapshot(snapshot);
    }

    public void DisposeRenderer()
    {
        songHeaderOverlay?.Dispose();
        songHeaderOverlay = null;

        backgroundEffect?.Dispose();
        backgroundEffect = null;

        if (mainCamera != null && originalMainCameraCullingMask >= 0)
        {
            mainCamera.cullingMask = originalMainCameraCullingMask;
            mainCamera.clearFlags = originalMainCameraClearFlags;
            mainCamera.depth = originalMainCameraDepth;
        }

        if (backgroundCamera != null)
            backgroundCamera.enabled = false;

        if (root != null)
            Object.Destroy(root);

        if (sharedTechniqueRibbonMaterial != null)
        {
            Object.Destroy(sharedTechniqueRibbonMaterial);
            sharedTechniqueRibbonMaterial = null;
        }

        if (sharedBendArrowMaterial != null)
        {
            Object.Destroy(sharedBendArrowMaterial);
            sharedBendArrowMaterial = null;
        }

        if (sharedMuteSymbolMaterial != null)
        {
            Object.Destroy(sharedMuteSymbolMaterial);
            sharedMuteSymbolMaterial = null;
        }

        if (sharedHighwayCharacterMaterial != null)
        {
            Object.Destroy(sharedHighwayCharacterMaterial);
            sharedHighwayCharacterMaterial = null;
        }

        if (techniqueRibbonMesh != null)
        {
            Object.Destroy(techniqueRibbonMesh);
            techniqueRibbonMesh = null;
        }

        highwayCharacterTexture = null;
    }

    private void SetGameplayVisualsVisible(bool visible)
    {
        if (gameplayVisualsVisible == visible)
            return;

        gameplayVisualsVisible = visible;
        if (gameplayRoot != null)
            gameplayRoot.SetActive(visible);
    }

    private void BuildChartCaches(List<NoteData> chartNotes)
    {
        chartById.Clear();
        chordGroups.Clear();
        slideDestinationBySourceId.Clear();
        bendDestinationBySourceId.Clear();
        bendSourceByDestinationId.Clear();
        noteLaneTagTextById.Clear();
        debugLoggedBendProfileIds.Clear();
        debugLoggedBendNearStrikeIds.Clear();

        if (chartNotes == null)
            return;

        for (int i = 0; i < chartNotes.Count; i++)
        {
            NoteData note = chartNotes[i];
            chartById[note.id] = note;

            if (DebugBendRibbonLogs && HasBendRibbon(note))
            {
                Debug.Log(
                    $"[BEND CACHE] id={note.id} t={note.time:F3} dur={note.duration:F3} string={note.stringIdx} fret={note.fret} " +
                    $"bend={note.bendStep:F2} pre={note.bendPreBend} rel={note.bendRelease} " +
                    $"visualStart={note.bendVisualStartTime:F3} visualDur={note.bendVisualDuration:F3}");
            }

            if (note.linkedFromNoteId >= 0)
                slideDestinationBySourceId[note.linkedFromNoteId] = note.id;

            if (note.chordId >= 0)
            {
                if (!chordGroups.TryGetValue(note.chordId, out List<NoteData> group))
                {
                    group = new List<NoteData>();
                    chordGroups[note.chordId] = group;
                }

                group.Add(note);
            }
        }

        foreach (var key in chordGroups.Keys.ToList())
            chordGroups[key] = chordGroups[key].OrderBy(n => n.stringIdx).ThenBy(n => n.fret).ToList();

        for (int i = 0; i < chartNotes.Count; i++)
        {
            NoteData source = chartNotes[i];
            if (!HasBendRibbon(source))
                continue;

            int destinationIndex = FindBendDestinationIndex(chartNotes, i);
            if (destinationIndex < 0)
                continue;

            NoteData destination = chartNotes[destinationIndex];
            bendDestinationBySourceId[source.id] = destination.id;
            bendSourceByDestinationId[destination.id] = source.id;
        }

        BuildLaneTagNoteMap(chartNotes);
    }

    private static int FindBendDestinationIndex(List<NoteData> chartNotes, int sourceIndex)
    {
        NoteData source = chartNotes[sourceIndex];
        float expectedEndTime = source.time + Mathf.Max(0.05f, source.duration);
        const float earlyTolerance = 0.06f;
        const float lateTolerance = 0.14f;

        int bestIndex = -1;
        float bestDelta = float.MaxValue;

        for (int i = sourceIndex + 1; i < chartNotes.Count; i++)
        {
            NoteData candidate = chartNotes[i];
            if (candidate.time > expectedEndTime + lateTolerance)
                break;

            if (candidate.stringIdx != source.stringIdx || candidate.fret != source.fret)
                continue;

            // If the candidate starts its own bend, it is a real new anchor note and
            // should keep its travelling box instead of being hidden as a continuation.
            if (candidate.bendStep > 0f || candidate.technique == NoteTechnique.Bend || candidate.bendPreBend || candidate.bendRelease)
                continue;

            if (candidate.time < expectedEndTime - earlyTolerance)
                continue;

            float delta = Mathf.Abs(candidate.time - expectedEndTime);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void BuildLaneTagNoteMap(List<NoteData> chartNotes)
    {
        if (chartNotes == null || chartNotes.Count == 0)
            return;

        List<NoteData> orderedNotes = chartNotes
            .OrderBy(note => note.time)
            .ThenBy(note => note.id)
            .ToList();

        const int notesPerSection = 5;
        for (int start = 0; start < orderedNotes.Count; start += notesPerSection)
        {
            int endExclusive = Mathf.Min(start + notesPerSection, orderedNotes.Count);
            HashSet<int> seenFrets = new HashSet<int>();

            for (int i = start; i < endExclusive; i++)
            {
                NoteData note = orderedNotes[i];
                if (note.fret <= 0)
                    continue;

                if (!seenFrets.Add(note.fret))
                    continue;

                noteLaneTagTextById[note.id] = note.fret.ToString();
            }
        }
    }

    private void BuildLaneHighlightChunks(List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        laneHighlightChunks.Clear();
        if (chartNotes == null || chartNotes.Count == 0)
            return;

        List<TabSectionData> sourceSections = sections != null && sections.Count > 0
            ? sections
            : BuildFallbackLaneSections(chartNotes);

        int laneCount = GetFretLightColumnCount();
        HashSet<int> processedChordIds = new HashSet<int>();

        for (int sectionIndex = 0; sectionIndex < sourceSections.Count; sectionIndex++)
        {
            TabSectionData section = sourceSections[sectionIndex];
            if (section == null)
                continue;

            bool[] surfaceMask = new bool[laneCount];
            bool[] guideMask = new bool[laneCount];
            List<int> frettedSurfaceAnchors = new List<int>();
            List<int> frettedGuideAnchors = new List<int>();

            if (section.noteIds != null && section.noteIds.Count > 0)
            {
                for (int noteIndex = 0; noteIndex < section.noteIds.Count; noteIndex++)
                {
                    if (!chartById.TryGetValue(section.noteIds[noteIndex], out NoteData note))
                        continue;

                    if (note.chordId >= 0)
                    {
                        if (!processedChordIds.Add(note.chordId))
                            continue;

                        if (chordGroups.TryGetValue(note.chordId, out List<NoteData> chordGroup))
                            AddGroupToChunkMasks(chordGroup, surfaceMask, guideMask, frettedSurfaceAnchors, frettedGuideAnchors);
                        else
                            AddGroupToChunkMasks(new List<NoteData> { note }, surfaceMask, guideMask, frettedSurfaceAnchors, frettedGuideAnchors);

                        continue;
                    }

                    AddGroupToChunkMasks(new List<NoteData> { note }, surfaceMask, guideMask, frettedSurfaceAnchors, frettedGuideAnchors);
                }
            }

            processedChordIds.Clear();
            MarkChunkedLaneRanges(surfaceMask, frettedSurfaceAnchors, maxChunkGap: 3);
            MarkChunkedLaneRanges(guideMask, frettedGuideAnchors, maxChunkGap: 2);

            laneHighlightChunks.Add(new LaneHighlightChunk
            {
                startTime = section.startTime,
                endTime = section.endTime,
                laneSurfaceMask = surfaceMask,
                laneGuideMask = guideMask
            });
        }
    }

    private List<TabSectionData> BuildFallbackLaneSections(List<NoteData> chartNotes)
    {
        List<TabSectionData> generatedSections = new List<TabSectionData>();
        if (chartNotes == null || chartNotes.Count == 0)
            return generatedSections;

        float chunkDuration = Mathf.Max(0.75f, GetVisibleLeadTime() * 0.75f);
        float maxTime = chartNotes.Max(n => n.time + n.duration);
        int totalSections = Mathf.Max(1, Mathf.CeilToInt(maxTime / chunkDuration) + 1);

        for (int i = 0; i < totalSections; i++)
        {
            float start = i * chunkDuration;
            float end = start + chunkDuration;
            List<int> noteIds = chartNotes
                .Where(n => n.time >= start && n.time < end)
                .Select(n => n.id)
                .ToList();

            generatedSections.Add(new TabSectionData
            {
                index = i,
                startTime = start,
                endTime = end,
                noteIds = noteIds
            });
        }

        return generatedSections;
    }

    private void ConfigureCamera()
    {
        if (mainCamera == null)
            return;

        if (backgroundUsingMenuMode)
        {
            if (backgroundCamera != null)
                backgroundCamera.enabled = false;

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = owner.tabCameraSize;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            if (originalMainCameraCullingMask >= 0)
                mainCamera.cullingMask = originalMainCameraCullingMask | (1 << BackgroundLayer);
            mainCamera.depth = originalMainCameraDepth;
            mainCamera.transform.position = new Vector3(0f, 0f, owner.tabCameraZ);
            mainCamera.transform.rotation = Quaternion.identity;
        }
        else
        {
            mainCamera.orthographic = false;
            mainCamera.clearFlags = CameraClearFlags.Depth;
            if (originalMainCameraCullingMask >= 0)
                mainCamera.cullingMask = originalMainCameraCullingMask & ~(1 << BackgroundLayer);
            mainCamera.depth = originalMainCameraDepth;
            mainCamera.farClipPlane = Mathf.Max(mainCamera.farClipPlane, owner.highwayCameraFarClip);
            mainCamera.transform.position = new Vector3(cameraTargetX, owner.highwayCameraY, owner.highwayCameraZ);
            mainCamera.transform.rotation = Quaternion.Euler(owner.highwayCameraPitch, 0f, 0f);
            SyncBackgroundCamera();
        }

        mainCamera.backgroundColor = GetCameraBackgroundColor();
    }

    private Color GetCameraBackgroundColor()
    {
        if (owner == null)
            return Color.black;

        if (owner.tabBackgroundMode == GuitarBridgeServer.TabsBackgroundMode.BlueSky)
        {
            switch (owner.tabSkyMood)
            {
                case GuitarBridgeServer.TabsSkyMood.Sunset:
                    return owner.tabSkySunsetBottomColor;
                case GuitarBridgeServer.TabsSkyMood.Midnight:
                    return owner.tabSkyMidnightBottomColor;
                default:
                    return owner.tabSkyBottomColor;
            }
        }

        return owner.tabBackgroundColor;
    }

    private void EnsureGameplayVisualsBuilt()
    {
        if (gameplayBuilt)
            return;

        fretLightMats = new Material[6, GetFretLightColumnCount()];
        fretLightRenderers = new Renderer[6, GetFretLightColumnCount()];
        fretBoundaryMats = new Material[GetFretLightColumnCount()];
        fretBoundaryRenderers = new Renderer[GetFretLightColumnCount()];
        laneSurfaceMats = new Material[GetFretLightColumnCount()];
        laneSurfaceRenderers = new Renderer[GetFretLightColumnCount()];
        laneGuideMats = new Material[GetFretLightColumnCount()];
        laneGuideRenderers = new Renderer[GetFretLightColumnCount()];
        GenerateFretboard();
        GenerateStrings();
        GenerateLaneSurfaces();
        GenerateLaneGuides();
        GenerateFretLightGrid();
        gameplayBuilt = true;
    }

    private void InitializeHighwayCharacter()
    {
        if (characterRoot == null)
            return;

        Sprite characterSprite = Resources.Load<Sprite>("char");
        if (characterSprite != null)
        {
            highwayCharacterTexture = characterSprite.texture;
            Rect spriteRect = characterSprite.rect;
            highwayCharacterAspect = Mathf.Max(0.05f, spriteRect.width / Mathf.Max(1f, spriteRect.height));
        }
        else
        {
            highwayCharacterTexture = Resources.Load<Texture2D>("char");
            if (highwayCharacterTexture == null)
                return;

            highwayCharacterAspect = Mathf.Max(0.05f, highwayCharacterTexture.width / (float)Mathf.Max(1, highwayCharacterTexture.height));
        }

        GameObject characterObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        characterObject.name = "HighwayCharacter";
        characterObject.transform.SetParent(characterRoot.transform, false);
        Object.Destroy(characterObject.GetComponent<Collider>());

        highwayCharacterRenderer = characterObject.GetComponent<Renderer>();
        highwayCharacterRenderer.sharedMaterial = GetHighwayCharacterMaterial();
        highwayCharacterRenderer.shadowCastingMode = ShadowCastingMode.Off;
        highwayCharacterRenderer.receiveShadows = false;

        SetLayerRecursively(characterRoot, 0);
        characterRoot.SetActive(false);
    }

    private void InitializeBackgroundCamera()
    {
        if (mainCamera == null || backgroundCamera != null)
            return;

        GameObject cameraObject = new GameObject("Highway3DBackgroundCamera");
        cameraObject.transform.SetParent(root.transform, false);
        backgroundCamera = cameraObject.AddComponent<Camera>();
        backgroundCamera.enabled = false;
        backgroundCamera.depth = originalMainCameraDepth - 1f;
    }

    private void InitializeBackgroundEffect(bool menuMode)
    {
        backgroundEffect?.Dispose();
        backgroundEffect = TabsBackgroundFactory.Create(owner, applyHighwayOverrides: !menuMode);
        backgroundUsingMenuMode = menuMode;
        backgroundSignature = GetBackgroundSignature(menuMode);

        if (backgroundRoot == null || backgroundEffect == null)
            return;

        backgroundEffect.Initialize(backgroundRoot.transform, owner);
        SetLayerRecursively(backgroundRoot, BackgroundLayer);
        if (menuMode)
        {
            backgroundRoot.transform.localPosition = Vector3.zero;
            backgroundRoot.transform.localRotation = Quaternion.identity;
            backgroundRoot.transform.localScale = Vector3.one;
        }
        else
            UpdateBackgroundPlacement();
    }

    private void UpdateBackgroundPlacement()
    {
        if (backgroundRoot == null || mainCamera == null)
            return;

        backgroundRoot.transform.position = new Vector3(
            Mathf.Max(0f, owner.TotalFrets * owner.FretSpacing * 0.5f),
            owner.highwayBackgroundCenterY,
            owner.highwayBackgroundDistance);
        backgroundRoot.transform.localScale = Vector3.one * owner.highwayBackgroundScale;
    }

    private void UpdateHighwayCharacterPlacement()
    {
        if (characterRoot == null)
            return;

        bool shouldShow = !backgroundUsingMenuMode && mainCamera != null && highwayCharacterRenderer != null && highwayCharacterTexture != null;
        if (characterRoot.activeSelf != shouldShow)
            characterRoot.SetActive(shouldShow);

        if (!shouldShow)
            return;

        float viewportHeight = HighwayCharacterHeightViewportFraction;
        float viewportWidth = viewportHeight * highwayCharacterAspect / Mathf.Max(0.1f, mainCamera.aspect);
        float left = HighwayCharacterViewportMarginX;
        float bottom = HighwayCharacterViewportMarginY;
        Vector3 lowerLeft = mainCamera.ViewportToWorldPoint(new Vector3(left, bottom, HighwayCharacterDepth));
        Vector3 lowerRight = mainCamera.ViewportToWorldPoint(new Vector3(left + viewportWidth, bottom, HighwayCharacterDepth));
        Vector3 upperLeft = mainCamera.ViewportToWorldPoint(new Vector3(left, bottom + viewportHeight, HighwayCharacterDepth));
        float targetWidth = Vector3.Distance(lowerLeft, lowerRight);
        float targetHeight = Vector3.Distance(lowerLeft, upperLeft);
        Vector3 worldPosition = (lowerLeft + lowerRight + upperLeft + (lowerRight + (upperLeft - lowerLeft))) * 0.25f;
        worldPosition -= mainCamera.transform.right * (targetWidth * HighwayCharacterAdditionalLeftOffsetInWidths);
        worldPosition -= mainCamera.transform.up * (targetHeight * HighwayCharacterAdditionalDownOffsetInHeights);

        characterRoot.transform.position = worldPosition;
        characterRoot.transform.rotation = mainCamera.transform.rotation;
        characterRoot.transform.localScale = new Vector3(targetWidth, targetHeight, 1f);
    }

    private void SyncBackgroundCamera()
    {
        if (mainCamera == null || backgroundCamera == null)
            return;

        backgroundCamera.enabled = true;
        backgroundCamera.CopyFrom(mainCamera);
        backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        backgroundCamera.backgroundColor = GetCameraBackgroundColor();
        backgroundCamera.cullingMask = 1 << BackgroundLayer;
        backgroundCamera.depth = originalMainCameraDepth - 1f;
    }

    private void EnsureBackgroundMode(bool menuMode)
    {
        if (backgroundEffect == null || menuMode != backgroundUsingMenuMode || backgroundSignature != GetBackgroundSignature(menuMode))
            InitializeBackgroundEffect(menuMode);
    }

    private string GetBackgroundSignature(bool menuMode)
    {
        if (owner == null)
            return string.Empty;

        return $"{owner.tabBackgroundMode}|{owner.tabSkyUseStageBackdrop}|{menuMode}";
    }

    private Material GetHighwayCharacterMaterial()
    {
        if (sharedHighwayCharacterMaterial != null)
            return sharedHighwayCharacterMaterial;

        Shader shader = Resources.Load<Shader>("Shaders/HighwayCharacterFade");
        if (shader == null)
            shader = Shader.Find("Custom/HighwayCharacterFade");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        sharedHighwayCharacterMaterial = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(Color.white, 0f);
        sharedHighwayCharacterMaterial.color = Color.white;
        sharedHighwayCharacterMaterial.mainTexture = highwayCharacterTexture;
        sharedHighwayCharacterMaterial.SetTexture("_MainTex", highwayCharacterTexture);
        if (sharedHighwayCharacterMaterial.HasProperty(CharacterFadeStartShaderId))
            sharedHighwayCharacterMaterial.SetFloat(CharacterFadeStartShaderId, HighwayCharacterBottomFadeStart01);
        if (sharedHighwayCharacterMaterial.HasProperty(CharacterFadeEndShaderId))
            sharedHighwayCharacterMaterial.SetFloat(CharacterFadeEndShaderId, HighwayCharacterBottomFadeEnd01);
        // Render the character before lane transparencies so they can blend over it,
        // while opaque gameplay geometry still occludes it by depth.
        sharedHighwayCharacterMaterial.renderQueue = (int)RenderQueue.Transparent - 50;
        sharedHighwayCharacterMaterial.SetInt("_ZWrite", 0);
        sharedHighwayCharacterMaterial.SetInt("_Cull", (int)CullMode.Off);
        sharedHighwayCharacterMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        return sharedHighwayCharacterMaterial;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
            return;

        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private void GenerateFretboard()
    {
        fretNumberLabels.Clear();
        float fretLineCenterY = GetFretLineCenterY();
        float fretLineHeight = GetFretLineHeight();

        GameObject nut = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nut.transform.SetParent(gameplayRoot.transform, false);
        nut.transform.position = new Vector3(0f, fretLineCenterY, owner.StrikeLineZ + 0.05f);
        nut.transform.localScale = new Vector3(0.5f, fretLineHeight, 0.3f);
        Renderer nutRenderer = nut.GetComponent<Renderer>();
        Material nutMat = owner.CreateSharedTransparentMaterial(new Color(0.22f, 0.23f, 0.27f, 0.28f), 0f);
        ConfigureOverlayMaterial(nutMat, 120, true);
        nutRenderer.material = nutMat;
        fretBoundaryMats[0] = nutMat;
        fretBoundaryRenderers[0] = nutRenderer;

        for (int fret = 1; fret <= owner.TotalFrets; fret++)
        {
            float wireX = fret * owner.FretSpacing;

            GameObject wire = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wire.transform.SetParent(gameplayRoot.transform, false);
            wire.transform.position = new Vector3(wireX, fretLineCenterY, owner.StrikeLineZ + 0.05f);
            wire.transform.localScale = new Vector3(0.15f, fretLineHeight, 0.15f);
            Renderer wireRenderer = wire.GetComponent<Renderer>();
            Material wireMat = owner.CreateSharedTransparentMaterial(new Color(0.22f, 0.23f, 0.27f, 0.28f), 0f);
            ConfigureOverlayMaterial(wireMat, 120, true);
            wireRenderer.material = wireMat;
            fretBoundaryMats[fret] = wireMat;
            fretBoundaryRenderers[fret] = wireRenderer;

            if (fret % 3 == 0 || fret == 5 || fret == 7 || fret == 9 || fret == 12 || fret == 15)
            {
                CreateFretNumberLabel(fret, GetFretNumberX(fret));
            }
        }

        if (!owner.hideOpenFretNumber)
            CreateFretNumberLabel(0, GetOpenFretNumberX());
    }

    private void GenerateLaneSurfaces()
    {
        int laneCount = GetFretLightColumnCount();
        float laneSurfaceY = GetLaneSurfaceY();
        const float laneBackOverhang = 8f;
        float depth = 150f + laneBackOverhang;
        float centerZ = owner.StrikeLineZ - laneBackOverhang + (depth * 0.5f);
        // Keep adjacent lane floors from overlapping while leaving only a hairline seam.
        float laneWidth = Mathf.Max(0.0f, owner.FretSpacing * 1);
        const float laneHeight = 0.025f;

        for (int lane = 0; lane < laneCount; lane++)
        {
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "LaneSurface_" + lane;
            surface.transform.SetParent(gameplayRoot.transform, false);
            surface.transform.position = new Vector3(GetNoteX(lane), laneSurfaceY, centerZ);
            surface.transform.localScale = new Vector3(laneWidth, laneHeight, depth);

            Material mat = CreateLaneSurfaceMaterial();
            Renderer renderer = surface.GetComponent<Renderer>();
            renderer.material = mat;
            laneSurfaceMats[lane] = mat;
            laneSurfaceRenderers[lane] = renderer;

            Object.Destroy(surface.GetComponent<Collider>());
        }
    }

    private void GenerateStrings()
    {
        float stringStartX = 0f;
        float stringEndX = (owner.TotalFrets * owner.FretSpacing) + (owner.FretSpacing * 0.75f);
        float stringLength = Mathf.Max(0.01f, stringEndX - stringStartX);
        float stringCenterX = stringStartX + (stringLength * 0.5f);

        for (int i = 0; i < 6; i++)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s.name = "String_" + i;
            s.transform.SetParent(gameplayRoot.transform, false);
            s.transform.position = new Vector3(stringCenterX, GetStringY(i), owner.StrikeLineZ);
            s.transform.localScale = new Vector3(stringLength, 0.1f, 0.1f);
            Material mat = owner.CreateSharedGlowMaterial(owner.GetStringColor(i), 0.9f);
            Renderer renderer = s.GetComponent<Renderer>();
            renderer.material = mat;
            stringVisuals[i] = s;
            stringVisualMats[i] = mat;
            stringVisualRenderers[i] = renderer;
        }
    }

    private void UpdateStringVisuals(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null)
            return;

        float renderSongTime = GetRenderSongTime(snapshot);
        bool[] stringHasIncomingNotes = new bool[6];

        if (snapshot.noteStates != null)
        {
            for (int i = 0; i < snapshot.noteStates.Count; i++)
            {
                GameplayNoteState state = snapshot.noteStates[i];
                if (state == null || state.IsResolved)
                    continue;

                int stringIdx = state.data.stringIdx;
                if (stringIdx < 0 || stringIdx >= stringHasIncomingNotes.Length)
                    continue;

                float travelZ = owner.StrikeLineZ + ((state.data.time - renderSongTime) * currentVisualNoteSpeed);
                if (travelZ > owner.SpawnZ)
                    continue;

                stringHasIncomingNotes[stringIdx] = true;
            }
        }

        for (int i = 0; i < stringVisualMats.Length; i++)
        {
            Material mat = stringVisualMats[i];
            if (mat == null)
                continue;

            if (stringVisuals[i] != null)
            {
                Vector3 position = stringVisuals[i].transform.position;
                position.y = GetStringY(i);
                stringVisuals[i].transform.position = position;
            }

            Color baseColor = owner.GetStringColor(i);
            bool isActive = stringHasIncomingNotes[i];
            Color appliedColor = isActive
                ? new Color(baseColor.r, baseColor.g, baseColor.b, 0.95f)
                : new Color(baseColor.r * 0.28f, baseColor.g * 0.28f, baseColor.b * 0.28f, 0.42f);
            float emission = isActive ? 0.6f : 0f;

            mat.color = appliedColor;
            mat.SetColor("_Color", appliedColor);
            mat.SetColor("_BaseColor", appliedColor);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission > 0f ? baseColor * Mathf.Pow(2f, emission) : Color.black);

            if (stringVisualRenderers[i] != null)
                stringVisualRenderers[i].enabled = true;
        }
    }

    private void GenerateFretLightGrid()
    {
        int fretLightColumns = GetFretLightColumnCount();

        for (int s = 0; s < 6; s++)
        {
            for (int f = 0; f < fretLightColumns; f++)
            {
                GameObject light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                light.transform.SetParent(gameplayRoot.transform, false);
                float xPos = f == 0 ? GetNoteX(Mathf.RoundToInt(owner.defaultOpenAnchorFret)) : GetNoteX(f);
                light.transform.position = new Vector3(xPos, GetStringY(s), owner.StrikeLineZ);
                light.transform.localScale = new Vector3(0.6f, 0.6f, 0.2f);

                Material mat = owner.CreateSharedGlowMaterial(Color.black, 0f);
                Renderer lightRenderer = light.GetComponent<Renderer>();
                lightRenderer.material = mat;
                lightRenderer.enabled = false;
                fretLightMats[s, f] = mat;
                fretLightRenderers[s, f] = lightRenderer;
            }
        }
    }

    private void GenerateLaneGuides()
    {
        int laneCount = GetFretLightColumnCount();
        float laneSurfaceY = GetLaneGuideStringY();
        float depth = 150f;
        float centerZ = owner.StrikeLineZ + (depth * 0.5f);
        // Make lane guides read more like slim glowing planes that bridge the lane seams.
        const float laneGuideHeight = 0.085f;
        const float laneGuideLift = 0.038f;

        for (int lane = 0; lane < laneCount; lane++)
        {
            GameObject guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guide.name = "LaneGuide_" + lane;
            guide.transform.SetParent(gameplayRoot.transform, false);
            float xPos = lane * owner.FretSpacing;
            float guideWidth = Mathf.Max(Mathf.Max(0.02f, owner.highwayLaneGuideThickness), owner.FretSpacing * 0.03f);
            guide.transform.position = new Vector3(xPos, laneSurfaceY + laneGuideLift, centerZ);
            guide.transform.localScale = new Vector3(guideWidth, laneGuideHeight, depth);

            Material mat = CreateLaneGuideMaterial();
            Renderer renderer = guide.GetComponent<Renderer>();
            renderer.material = mat;
            laneGuideMats[lane] = mat;
            laneGuideRenderers[lane] = renderer;

            Object.Destroy(guide.GetComponent<Collider>());
        }
    }

    private void UpdateFretBoundaries(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null || fretBoundaryMats == null || fretBoundaryRenderers == null)
            return;

        bool[] boundaryActive = BuildFretBoundaryActivityFlags(snapshot);

        Color activeColor = new Color(0.46f, 0.50f, 0.56f, 0.92f);
        Color idleColor = new Color(0.20f, 0.22f, 0.25f, 0.18f);

        for (int i = 0; i < fretBoundaryMats.Length; i++)
        {
            Material mat = fretBoundaryMats[i];
            Renderer renderer = fretBoundaryRenderers[i];
            if (mat == null || renderer == null)
                continue;

            Color color = boundaryActive[i] ? activeColor : idleColor;
            float emission = boundaryActive[i]
                ? (owner.highwayHighlightFretBoundaries ? 0.18f : 0.04f)
                : 0f;
            mat.color = color;
            mat.SetColor("_Color", color);
            mat.SetColor("_BaseColor", color);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", boundaryActive[i] ? color * Mathf.Pow(2f, emission) : Color.black);
            renderer.enabled = true;
        }

        UpdateFretNumberLabels(boundaryActive);
    }

    private bool[] BuildFretBoundaryActivityFlags(GuitarGameplaySnapshot snapshot)
    {
        int boundaryCount = fretBoundaryMats != null ? fretBoundaryMats.Length : GetFretLightColumnCount();
        bool[] boundaryActive = new bool[boundaryCount];
        bool[] laneMask = GetChunkLaneMask(snapshot, boundaryCount, useGuideMask: false);

        for (int fret = 0; fret < boundaryCount; fret++)
        {
            if (fret == 0)
            {
                boundaryActive[fret] = laneMask.Length > 1 && laneMask[1];
                continue;
            }

            bool lowerFretLaneActive = fret < laneMask.Length && laneMask[fret];
            bool higherFretLaneActive = fret + 1 < laneMask.Length && laneMask[fret + 1];
            boundaryActive[fret] = lowerFretLaneActive || higherFretLaneActive;
        }

        return boundaryActive;
    }

    private void UpdateLaneGuides(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null || laneGuideMats == null || laneGuideRenderers == null)
            return;

        bool[] laneHasIncomingNotes = BuildLaneGuideActivityFlags(snapshot);

        for (int lane = 0; lane < laneGuideMats.Length; lane++)
        {
            Material mat = laneGuideMats[lane];
            Renderer renderer = laneGuideRenderers[lane];
            if (mat == null || renderer == null)
                continue;

            bool isActive = laneHasIncomingNotes[lane];
            Color laneColor = isActive
                ? new Color(0.34f, 0.74f, 1f, 1f)
                : new Color(0.03f, 0.07f, 0.14f, 0.18f);
            float emission = isActive ? 2.2f : 0f;

            mat.color = laneColor;
            mat.SetColor("_Color", laneColor);
            mat.SetColor("_BaseColor", laneColor);
            mat.SetColor("_TintColor", laneColor);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.18f, 0.45f, 1f, 1f) * Mathf.Pow(2f, emission));
            renderer.enabled = true;
        }
    }

    private float GetFretNumberY()
    {
        float lowestStringY = float.MaxValue;
        for (int stringIdx = 0; stringIdx < 6; stringIdx++)
            lowestStringY = Mathf.Min(lowestStringY, GetStringY(stringIdx));

        // Adjust this value to move fret numbers lower or higher relative to the lowest string.
        return lowestStringY - 2f + owner.highwayFretNumberYOffset;
    }

    private float GetFretLineCenterY()
    {
        GetStringVerticalBounds(out float minY, out float maxY);
        return (minY + maxY) * 0.5f;
    }

    private float GetLaneGuideY()
    {
        GetStringVerticalBounds(out float minY, out _);
        const float laneGuideHeight = 0.045f;
        const float laneGuideLift = 0.14f;
        const float noteClearanceMargin = 0.03f;

        float lowestNoteHalfHeight = Mathf.Max(
            GetSingleFrettedNoteScale().y * 0.5f,
            GetGroupedFrettedNoteScale().y * 0.5f);

        float highestSafeGuideTop = minY - lowestNoteHalfHeight - noteClearanceMargin;
        return highestSafeGuideTop - laneGuideLift - (laneGuideHeight * 0.5f) + owner.highwayLaneGuideYOffset;
    }

    private float GetLaneGuideStringY()
    {
        GetStringVerticalBounds(out float minY, out _);
        return minY + owner.highwayLaneGuideYOffset;
    }

    private float GetLaneSurfaceY()
    {
        return GetLaneGuideY() - 0.03f;
    }

    private float GetLaneSurfaceTopY()
    {
        const float laneHeight = 0.025f;
        return GetLaneSurfaceY() + (laneHeight * 0.5f);
    }

    private float GetFretLineHeight()
    {
        GetStringVerticalBounds(out float minY, out float maxY);
        float endOverhang = GetTrackLowerEdgeOverhang();
        return Mathf.Max(0.2f, (maxY - minY) + (endOverhang * 2f));
    }

    private float GetTrackLowerEdgeOverhang()
    {
        return 0.12f;
    }

    private void GetStringVerticalBounds(out float minY, out float maxY)
    {
        minY = float.MaxValue;
        maxY = float.MinValue;

        for (int stringIdx = 0; stringIdx < 6; stringIdx++)
        {
            float y = GetStringY(stringIdx);
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }
    }

    private float GetFretNumberX(int fret)
    {
        float leftBoundaryX = fret <= 1 ? 0f : (fret - 1) * owner.FretSpacing;
        float rightBoundaryX = fret * owner.FretSpacing;
        return (leftBoundaryX + rightBoundaryX) * 0.5f;
    }

    private float GetOpenFretNumberX()
    {
        int openAnchorFret = Mathf.Clamp(Mathf.RoundToInt(owner.defaultOpenAnchorFret), 1, owner.TotalFrets);
        return GetFretNumberX(openAnchorFret);
    }

    private string FormatFretNumberLabel(int fret)
    {
        return Mathf.Max(0, fret).ToString();
    }

    private void CreateFretNumberLabel(int fret, float x)
    {
        GameObject textObj = new GameObject("FretNum_" + fret);
        textObj.transform.SetParent(gameplayRoot.transform, false);
        textObj.transform.position = new Vector3(x, GetFretNumberY(), owner.StrikeLineZ + 0.18f + owner.highwayFretNumberZOffset);
        textObj.transform.rotation = Quaternion.identity;
        textObj.transform.localScale = Vector3.one;

        TextMeshPro tm = textObj.AddComponent<TextMeshPro>();
        tm.text = FormatFretNumberLabel(fret);
        // Adjust this value to change fret number size.
        tm.fontSize = 14f;
        tm.fontStyle = FontStyles.Bold;
        tm.alignment = TextAlignmentOptions.Center;
        tm.overflowMode = TextOverflowModes.Overflow;
        tm.enableWordWrapping = false;
        tm.characterSpacing = 0f;
        tm.lineSpacing = 0f; 
        tm.rectTransform.sizeDelta = new Vector2(12f, 10f);
        tm.color = new Color(0.38f, 0.62f, 1f, 0.92f);
        tm.sortingOrder = 250;

        if (tm.fontSharedMaterial != null)
            tm.fontMaterial = new Material(tm.fontSharedMaterial);

        fretNumberLabels[fret] = tm;
        ApplyFretNumberLabelStyle(tm, false);
    }

    private TextMeshPro CreateLaneTagLabelIfNeeded(NoteData data)
    {
        if (!noteLaneTagTextById.TryGetValue(data.id, out string laneText))
            return null;

        GameObject textObj = new GameObject("LaneTag_" + data.id);
        textObj.transform.SetParent(gameplayRoot.transform, false);
        textObj.transform.rotation = Quaternion.identity;
        textObj.transform.localScale = Vector3.one;

        TextMeshPro tm = textObj.AddComponent<TextMeshPro>();
        tm.text = laneText;
        tm.fontSize = 22f;
        tm.fontStyle = FontStyles.Bold;
        tm.alignment = TextAlignmentOptions.Center;
        tm.overflowMode = TextOverflowModes.Overflow;
        tm.enableWordWrapping = false;
        tm.characterSpacing = 0f;
        tm.lineSpacing = 0f;
        tm.rectTransform.sizeDelta = new Vector2(16f, 14f);
        tm.sortingOrder = 255;

        if (tm.fontSharedMaterial != null)
            tm.fontMaterial = new Material(tm.fontSharedMaterial);

        ConfigureLaneTagLabelMaterial(tm);

        ApplyFretNumberLabelStyle(tm, true);
        return tm;
    }

    private static void ConfigureLaneTagLabelMaterial(TextMeshPro label)
    {
        if (label == null)
            return;

        Material fontMat = label.fontMaterial;
        if (fontMat == null)
            return;

        // Keep moving lane tags above the lane floor while staying below higher overlay elements.
        fontMat.renderQueue = (int)RenderQueue.Transparent + 89;
        if (fontMat.HasProperty("_ZWrite"))
            fontMat.SetFloat("_ZWrite", 0f);
        if (fontMat.HasProperty("_CullMode"))
            fontMat.SetFloat("_CullMode", 0f);
        if (fontMat.HasProperty("_ZTestMode"))
            fontMat.SetFloat("_ZTestMode", (float)CompareFunction.Always);
        else if (fontMat.HasProperty("_ZTest"))
            fontMat.SetFloat("_ZTest", (float)CompareFunction.Always);
    }

    private void UpdateFretNumberLabels(bool[] boundaryActive)
    {
        if (fretNumberLabels.Count == 0)
            return;

        foreach (KeyValuePair<int, TextMeshPro> pair in fretNumberLabels)
        {
            TextMeshPro label = pair.Value;
            if (label == null)
                continue;

            bool isActive = pair.Key >= 0 && pair.Key < boundaryActive.Length && boundaryActive[pair.Key];
            ApplyFretNumberLabelStyle(label, isActive);
        }
    }

    private void ApplyFretNumberLabelStyle(TextMeshPro label, bool isActive)
    {
        if (label == null)
            return;

        Color faceColor = isActive
            ? new Color(1f, 0.90f, 0.20f, 1f)
            : new Color(0.38f, 0.62f, 1f, 0.92f);
        label.color = faceColor;

        Material fontMat = label.fontMaterial;
        if (fontMat == null)
            return;

        fontMat.SetColor("_FaceColor", faceColor);
        if (fontMat.HasProperty("_GlowColor"))
        {
            fontMat.SetFloat("_GlowPower", isActive ? 0.55f : 0f);
            fontMat.SetFloat("_GlowInner", isActive ? 0.04f : 0f);
            fontMat.SetFloat("_GlowOuter", isActive ? 0.18f : 0f);
            fontMat.SetColor("_GlowColor", isActive ? new Color(1f, 0.84f, 0.12f, 0.9f) : Color.clear);
        }
        if (fontMat.HasProperty("_UnderlaySoftness"))
        {
            fontMat.SetFloat("_UnderlaySoftness", 0f);
            fontMat.SetFloat("_UnderlayDilate", 0f);
            fontMat.SetColor("_UnderlayColor", Color.clear);
        }
    }

    private void UpdateLaneSurfaces(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null || laneSurfaceMats == null || laneSurfaceRenderers == null)
            return;

        bool[] activeLanes = BuildLaneSurfaceActivityFlags(snapshot);

        for (int lane = 0; lane < laneSurfaceMats.Length; lane++)
        {
            Material mat = laneSurfaceMats[lane];
            Renderer renderer = laneSurfaceRenderers[lane];
            if (mat == null || renderer == null)
                continue;

            bool isActive = activeLanes[lane];
            bool hasLeftNeighbor = lane > 0 && activeLanes[lane - 1];
            bool hasRightNeighbor = lane + 1 < activeLanes.Length && activeLanes[lane + 1];
            Color laneColor = isActive
                ? new Color(0.08f, 0.10f, 0.14f, 1f)
                : new Color(0.025f, 0.03f, 0.045f, 0.14f);

            mat.color = laneColor;
            mat.SetColor("_Color", laneColor);
            mat.SetColor("_BaseColor", laneColor);
            mat.SetColor("_TintColor", laneColor);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", isActive ? new Color(0.18f, 0.32f, 0.46f, 1f) * Mathf.Pow(2f, 0.15f) : Color.black);
            if (mat.HasProperty("_EdgeFadeLeft"))
                mat.SetFloat("_EdgeFadeLeft", isActive && !hasLeftNeighbor ? 0.12f : 0.008f);
            if (mat.HasProperty("_EdgeFadeRight"))
                mat.SetFloat("_EdgeFadeRight", isActive && !hasRightNeighbor ? 0.12f : 0.008f);
            if (mat.HasProperty("_FrontBackFade"))
                mat.SetFloat("_FrontBackFade", 0.1f);
            renderer.enabled = true;
        }
    }

    private bool[] BuildLaneGuideActivityFlags(GuitarGameplaySnapshot snapshot)
    {
        int laneCount = laneGuideMats != null ? laneGuideMats.Length : GetFretLightColumnCount();
        bool[] laneMask = GetChunkLaneMask(snapshot, laneCount, useGuideMask: false);
        bool[] guideMask = new bool[laneCount];

        for (int guide = 0; guide < laneCount; guide++)
        {
            bool lowerLaneActive = guide < laneMask.Length && laneMask[guide];
            bool higherLaneActive = guide + 1 < laneMask.Length && laneMask[guide + 1];
            guideMask[guide] = lowerLaneActive || higherLaneActive;
        }

        return guideMask;
    }

    private bool[] BuildLaneSurfaceActivityFlags(GuitarGameplaySnapshot snapshot)
    {
        int laneCount = laneSurfaceMats != null ? laneSurfaceMats.Length : GetFretLightColumnCount();
        return ExpandLaneMask(GetChunkLaneMask(snapshot, laneCount, useGuideMask: false), 1);
    }

    private bool[] ExpandLaneMask(bool[] sourceMask, int extraLanesPerSide)
    {
        if (sourceMask == null || sourceMask.Length == 0 || extraLanesPerSide <= 0)
            return sourceMask ?? new bool[0];

        bool[] expanded = new bool[sourceMask.Length];
        for (int lane = 0; lane < sourceMask.Length; lane++)
        {
            if (!sourceMask[lane])
                continue;

            int start = Mathf.Clamp(lane - extraLanesPerSide, 0, sourceMask.Length - 1);
            int end = Mathf.Clamp(lane + extraLanesPerSide, 0, sourceMask.Length - 1);
            for (int i = start; i <= end; i++)
                expanded[i] = true;
        }

        return expanded;
    }

    private bool[] GetChunkLaneMask(GuitarGameplaySnapshot snapshot, int laneCount, bool useGuideMask)
    {
        bool[] emptyMask = new bool[laneCount];
        if (laneHighlightChunks == null || laneHighlightChunks.Count == 0 || snapshot == null)
            return emptyMask;

        float renderSongTime = GetRenderSongTime(snapshot);
        if (renderSongTime < laneHighlightChunks[0].startTime)
            return CloneChunkMask(laneHighlightChunks[0], laneCount, useGuideMask);

        for (int i = 0; i < laneHighlightChunks.Count; i++)
        {
            LaneHighlightChunk chunk = laneHighlightChunks[i];
            if (chunk == null)
                continue;

            bool isInChunk = renderSongTime >= chunk.startTime && renderSongTime < chunk.endTime;
            bool isLastChunk = i == laneHighlightChunks.Count - 1 && renderSongTime >= chunk.startTime;
            if (!isInChunk && !isLastChunk)
                continue;

            return CloneChunkMask(chunk, laneCount, useGuideMask);
        }

        return emptyMask;
    }

    private bool[] CloneChunkMask(LaneHighlightChunk chunk, int laneCount, bool useGuideMask)
    {
        bool[] clonedMask = new bool[laneCount];
        if (chunk == null)
            return clonedMask;

        bool[] sourceMask = useGuideMask ? chunk.laneGuideMask : chunk.laneSurfaceMask;
        if (sourceMask == null)
            return clonedMask;

        int copyLength = Mathf.Min(laneCount, sourceMask.Length);
        for (int lane = 0; lane < copyLength; lane++)
            clonedMask[lane] = sourceMask[lane];
        return clonedMask;
    }

    private void AddGroupToChunkMasks(List<NoteData> group, bool[] surfaceMask, bool[] guideMask, List<int> frettedSurfaceAnchors, List<int> frettedGuideAnchors)
    {
        if (group == null || group.Count == 0)
            return;

        List<NoteData> fretted = group.Where(n => n.fret > 0).ToList();
        if (fretted.Count == 0)
        {
            int handFret = GetGroupHandFret(group);
            MarkOpenGroupRange(surfaceMask, handFret, group);
            MarkOpenGroupRange(guideMask, handFret, group);
            return;
        }

        for (int i = 0; i < fretted.Count; i++)
        {
            NoteData note = fretted[i];
            int laneIndex = Mathf.Clamp(note.fret, 0, surfaceMask.Length - 1);
            frettedSurfaceAnchors.Add(laneIndex);
            frettedGuideAnchors.Add(laneIndex);
            MarkLaneRange(guideMask, laneIndex - 1, laneIndex);
        }
    }

    private void MarkChunkedLaneRanges(bool[] activeFlags, List<int> anchors, int maxChunkGap)
    {
        if (activeFlags == null || anchors == null || anchors.Count == 0)
            return;

        int[] ordered = anchors
            .Where(index => index >= 0 && index < activeFlags.Length)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

        if (ordered.Length == 0)
            return;

        int chunkStart = ordered[0];
        int previous = ordered[0];
        for (int i = 1; i < ordered.Length; i++)
        {
            int current = ordered[i];
            if (current - previous > maxChunkGap)
            {
                MarkLaneRange(activeFlags, chunkStart, previous);
                chunkStart = current;
            }

            previous = current;
        }

        MarkLaneRange(activeFlags, chunkStart, previous);
    }

    private static void MarkLaneRange(bool[] activeFlags, int startIndex, int endIndex)
    {
        if (activeFlags == null || activeFlags.Length == 0)
            return;

        int clampedStart = Mathf.Clamp(Mathf.Min(startIndex, endIndex), 0, activeFlags.Length - 1);
        int clampedEnd = Mathf.Clamp(Mathf.Max(startIndex, endIndex), 0, activeFlags.Length - 1);
        for (int i = clampedStart; i <= clampedEnd; i++)
            activeFlags[i] = true;
    }

    private void MarkOpenGroupRange(bool[] activeFlags, int handFret, List<NoteData> group)
    {
        int startLane = Mathf.Clamp(handFret - 1, 0, activeFlags.Length - 1);
        int endLane = Mathf.Clamp(GetOpenGroupEndLane(handFret, group), 0, activeFlags.Length - 1);
        MarkLaneRange(activeFlags, startLane, endLane);
    }

    private int GetOpenGroupEndLane(int handFret, List<NoteData> group)
    {
        int furthestFret = handFret + 3;
        if (group != null)
        {
            int highestGroupFret = group.Where(n => n.fret > 0).Select(n => n.fret).DefaultIfEmpty(furthestFret).Max();
            furthestFret = Mathf.Max(furthestFret, highestGroupFret);
        }

        return furthestFret;
    }

    private void UpdateNotes(GuitarGameplaySnapshot snapshot)
    {
        float renderSongTime = GetRenderSongTime(snapshot);
        visibleNoteIdsThisFrame.Clear();
        RebuildVisibleNoteStateCache(snapshot);

        for (int i = 0; i < snapshot.noteStates.Count; i++)
        {
            GameplayNoteState state = snapshot.noteStates[i];
            float travelZ = owner.StrikeLineZ + ((state.data.time - renderSongTime) * currentVisualNoteSpeed);
            bool keepForResult = state.IsResolved && renderSongTime - state.resolvedAt <= GetResolvedFadeTime();
            bool keepForTechnique = state.IsResolved && ShouldKeepTechniqueAliveAfterResolution(state.data, renderSongTime);
            bool visible = travelZ <= owner.SpawnZ && (!state.IsResolved || keepForResult || keepForTechnique || travelZ >= owner.StrikeLineZ);

            if (!visible)
                continue;

            visibleNoteIdsThisFrame.Add(state.data.id);

            if (!noteViews.TryGetValue(state.data.id, out HighwayNoteView view) || view == null)
            {
                view = CreateNoteView(state.data);
                noteViews[state.data.id] = view;
            }

            float displayZ = Mathf.Max(owner.StrikeLineZ, travelZ);
            UpdateNoteView(view, state, displayZ, travelZ, renderSongTime);
        }

        noteViewRemovalBuffer.Clear();
        foreach (KeyValuePair<int, HighwayNoteView> pair in noteViews)
        {
            if (visibleNoteIdsThisFrame.Contains(pair.Key))
                continue;

            noteViewRemovalBuffer.Add(pair.Key);
        }

        for (int i = 0; i < noteViewRemovalBuffer.Count; i++)
        {
            int key = noteViewRemovalBuffer[i];
            noteViews[key].Destroy();
            noteViews.Remove(key);
        }
    }

    private HighwayNoteView CreateNoteView(NoteData data)
    {
        List<NoteData> group = GetChordGroup(data);
        bool isGrouped = group.Count > 1;
        bool isOpen = data.fret == 0;

        float xPos = isOpen ? GetGroupAnchorX(group) : GetNoteX(data.fret);
        float yPos = GetStringY(data.stringIdx);

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "HighwayNote_" + data.id;
        cube.transform.SetParent(gameplayRoot.transform, false);
        cube.transform.position = new Vector3(xPos, yPos, owner.SpawnZ);

        Material noteMat = owner.CreateSharedGlowMaterial(owner.GetStringColor(data.stringIdx), 0.8f);
        ConfigureOverlayMaterial(noteMat, 120, true);
        cube.GetComponent<Renderer>().material = noteMat;

        GameObject textObj = null;
        TextMeshPro laneTagLabel = CreateLaneTagLabelIfNeeded(data);

        if (isGrouped)
        {
            if (isOpen)
            {
                float leftX = GetHandWindowStartX(GetGroupHandFret(group));
                float rightX = GetHandWindowEndX(GetGroupHandFret(group), group);
                cube.transform.localScale = new Vector3(Mathf.Max(owner.FretSpacing * 0.8f, rightX - leftX), GetScaledOpenHeight(), GetScaledOpenDepth());
            }
            else
            {
                cube.transform.localScale = GetGroupedFrettedNoteScale();
            }
        }
        else
        {
            if (isOpen)
                cube.transform.localScale = GetSingleOpenNoteScale();
            else
                cube.transform.localScale = GetSingleFrettedNoteScale();
        }

        GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tail.name = "Tail_" + data.id;
        tail.transform.SetParent(gameplayRoot.transform, false);
        Material tailMat = owner.CreateSharedTransparentMaterial(owner.GetStringColor(data.stringIdx) * 0.4f, 0.2f);
        ConfigureOverlayMaterial(tailMat, 90, true);
        tail.GetComponent<Renderer>().material = tailMat;
        tail.SetActive(owner.highwayShowApproachLine);

        GameObject tether = null;
        Material tetherMat = null;
        Renderer tetherRenderer = null;
        if (!isOpen && !isGrouped)
        {
            tether = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tether.name = "LaneTether_" + data.id;
            tether.transform.SetParent(gameplayRoot.transform, false);
            tetherMat = CreateNoteTetherMaterial(owner.GetStringColor(data.stringIdx));
            tetherRenderer = tether.GetComponent<Renderer>();
            tetherRenderer.material = tetherMat;
            Object.Destroy(tether.GetComponent<Collider>());
        }

        GameObject marker = null; 
        Renderer markerRenderer = null;
        Material markerMaterial = null;
        if (!isOpen) 
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Marker_" + data.id;
            marker.transform.SetParent(gameplayRoot.transform, false);
            marker.transform.position = new Vector3(xPos, yPos, owner.StrikeLineZ);
            marker.transform.localScale = GetMarkerScale();
            markerMaterial = owner.CreateSharedTransparentMaterial(owner.GetStringColor(data.stringIdx), 1.1f);
            ConfigureOverlayMaterial(markerMaterial, 130, true);
            markerRenderer = marker.GetComponent<Renderer>();
            markerRenderer.material = markerMaterial;
            SetGameObjectActive(marker, owner.highwayShowLandingDot);
        }

        GameObject bendArrow = null;
        Renderer bendArrowRenderer = null;
        MaterialPropertyBlock bendArrowPropertyBlock = null;
        GameObject bendArrowSecondary = null;
        Renderer bendArrowSecondaryRenderer = null;
        MaterialPropertyBlock bendArrowSecondaryPropertyBlock = null;
        if (HasBendRibbon(data))
        {
            EnsureBendArrowResources();
            if (sharedBendArrowMaterial != null)
            {
                bendArrow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bendArrow.name = "BendArrow_" + data.id;
                bendArrow.transform.SetParent(gameplayRoot.transform, false);
                Object.Destroy(bendArrow.GetComponent<Collider>());
                bendArrowRenderer = bendArrow.GetComponent<Renderer>();
                bendArrowRenderer.sharedMaterial = sharedBendArrowMaterial;
                bendArrowRenderer.shadowCastingMode = ShadowCastingMode.Off;
                bendArrowRenderer.receiveShadows = false;
                bendArrowRenderer.lightProbeUsage = LightProbeUsage.Off;
                bendArrowRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                bendArrowPropertyBlock = new MaterialPropertyBlock();

                bendArrowSecondary = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bendArrowSecondary.name = "BendArrowSecondary_" + data.id;
                bendArrowSecondary.transform.SetParent(gameplayRoot.transform, false);
                Object.Destroy(bendArrowSecondary.GetComponent<Collider>());
                bendArrowSecondaryRenderer = bendArrowSecondary.GetComponent<Renderer>();
                bendArrowSecondaryRenderer.sharedMaterial = sharedBendArrowMaterial;
                bendArrowSecondaryRenderer.shadowCastingMode = ShadowCastingMode.Off;
                bendArrowSecondaryRenderer.receiveShadows = false;
                bendArrowSecondaryRenderer.lightProbeUsage = LightProbeUsage.Off;
                bendArrowSecondaryRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                bendArrowSecondaryPropertyBlock = new MaterialPropertyBlock();
            }
        }

        GameObject muteSymbol = null;
        Renderer muteSymbolRenderer = null;
        if (ShouldShowMuteSymbolForNote(data))
        {
            EnsureMuteSymbolResources();
            if (sharedMuteSymbolMaterial != null)
            {
                muteSymbol = GameObject.CreatePrimitive(PrimitiveType.Quad);
                muteSymbol.name = "MuteSymbol_" + data.id;
                muteSymbol.transform.SetParent(gameplayRoot.transform, false);
                Object.Destroy(muteSymbol.GetComponent<Collider>());
                muteSymbolRenderer = muteSymbol.GetComponent<Renderer>();
                muteSymbolRenderer.sharedMaterial = sharedMuteSymbolMaterial;
                muteSymbolRenderer.shadowCastingMode = ShadowCastingMode.Off;
                muteSymbolRenderer.receiveShadows = false;
                muteSymbolRenderer.lightProbeUsage = LightProbeUsage.Off;
                muteSymbolRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        GameObject outlineRoot = CreateNoteOutline(cube.transform.localScale, owner.GetStringColor(data.stringIdx));
        outlineRoot.SetActive(false);

        GameObject techniqueRoot = new GameObject("Technique_" + data.id);
        techniqueRoot.transform.SetParent(gameplayRoot.transform, false);

        GameObject[] techniqueSegmentRibbons = null;
        Renderer[] techniqueSegmentRibbonRenderers = null;
        MaterialPropertyBlock[] techniqueSegmentRibbonPropertyBlocks = null;
        if (HasTechniqueSegments(data))
        {
            EnsureTechniqueRibbonResources();
            if (techniqueRibbonMesh != null && sharedTechniqueRibbonMaterial != null)
            {
                int slotCount = GetTechniqueSegmentRibbonSlotCount(data);
                techniqueSegmentRibbons = new GameObject[slotCount];
                techniqueSegmentRibbonRenderers = new Renderer[slotCount];
                techniqueSegmentRibbonPropertyBlocks = new MaterialPropertyBlock[slotCount];

                for (int i = 0; i < slotCount; i++)
                {
                    techniqueSegmentRibbons[i] = CreateTechniqueRibbonObject(
                        "TechniqueSegmentRibbon_" + data.id + "_" + i,
                        techniqueRoot.transform,
                        techniqueRibbonMesh,
                        sharedTechniqueRibbonMaterial,
                        out techniqueSegmentRibbonRenderers[i]);
                    techniqueSegmentRibbonPropertyBlocks[i] = techniqueSegmentRibbonRenderers[i] != null ? new MaterialPropertyBlock() : null;
                }
            }
        }

        GameObject slideRibbon = null;
        Renderer slideRibbonRenderer = null;
        GameObject legatoCurve = null;
        LineRenderer legatoCurveRenderer = null;
        if (!HasTechniqueSegments(data) && data.slideTargetFret >= 0)
        {
            if (IsLegatoCurveTechnique(data))
            {
                legatoCurve = new GameObject("LegatoCurve_" + data.id);
                legatoCurve.transform.SetParent(techniqueRoot.transform, false);
                legatoCurveRenderer = legatoCurve.AddComponent<LineRenderer>();
                legatoCurveRenderer.useWorldSpace = true;
                legatoCurveRenderer.loop = false;
                legatoCurveRenderer.alignment = LineAlignment.View;
                legatoCurveRenderer.textureMode = LineTextureMode.Stretch;
                legatoCurveRenderer.numCapVertices = 6;
                legatoCurveRenderer.numCornerVertices = 4;
                legatoCurveRenderer.shadowCastingMode = ShadowCastingMode.Off;
                legatoCurveRenderer.receiveShadows = false;
                legatoCurveRenderer.lightProbeUsage = LightProbeUsage.Off;
                legatoCurveRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                Material legatoMat = owner.CreateSharedGlowMaterial(owner.GetStringColor(data.stringIdx), 1.4f);
                ConfigureOverlayMaterial(legatoMat, 101, true);
                legatoCurveRenderer.material = legatoMat;
            }
            else
            {
                EnsureTechniqueRibbonResources();
                if (techniqueRibbonMesh != null && sharedTechniqueRibbonMaterial != null)
                {
                    slideRibbon = CreateTechniqueRibbonObject("SlideRibbon_" + data.id, techniqueRoot.transform, techniqueRibbonMesh, sharedTechniqueRibbonMaterial, out slideRibbonRenderer);
                }
            }
        }

        GameObject bendRibbon = null;
        Renderer bendRibbonRenderer = null;
        GameObject bendSustainRibbon = null;
        Renderer bendSustainRibbonRenderer = null;
        GameObject sustainRibbon = null;
        Renderer sustainRibbonRenderer = null;
        if (!HasTechniqueSegments(data) && HasBendRibbon(data))
        {
            EnsureTechniqueRibbonResources();
            if (techniqueRibbonMesh != null && sharedTechniqueRibbonMaterial != null)
            {
                bendRibbon = CreateTechniqueRibbonObject("BendRibbon_" + data.id, techniqueRoot.transform, techniqueRibbonMesh, sharedTechniqueRibbonMaterial, out bendRibbonRenderer);
                bendSustainRibbon = CreateTechniqueRibbonObject("BendSustainRibbon_" + data.id, techniqueRoot.transform, techniqueRibbonMesh, sharedTechniqueRibbonMaterial, out bendSustainRibbonRenderer);
            }
        }

        if (!HasTechniqueSegments(data) && HasNoteSustainRibbon(data))
        {
            EnsureTechniqueRibbonResources();
            if (techniqueRibbonMesh != null && sharedTechniqueRibbonMaterial != null)
            {
                sustainRibbon = CreateTechniqueRibbonObject("SustainRibbon_" + data.id, techniqueRoot.transform, techniqueRibbonMesh, sharedTechniqueRibbonMaterial, out sustainRibbonRenderer);
            }
        }

        return new HighwayNoteView
        {
            noteRoot = cube,
            noteRenderer = cube.GetComponent<Renderer>(),
            noteMaterial = noteMat,
            label = textObj != null ? textObj.GetComponent<TextMeshPro>() : null,
            laneTagLabel = laneTagLabel,
            tail = tail,
            tether = tether,
            tetherRenderer = tetherRenderer,
            tetherMaterial = tetherMat,
            marker = marker,
            markerRenderer = markerRenderer,
            markerMaterial = markerMaterial,
            bendArrow = bendArrow,
            bendArrowRenderer = bendArrowRenderer,
            bendArrowPropertyBlock = bendArrowPropertyBlock,
            bendArrowSecondary = bendArrowSecondary,
            bendArrowSecondaryRenderer = bendArrowSecondaryRenderer,
            bendArrowSecondaryPropertyBlock = bendArrowSecondaryPropertyBlock,
            muteSymbol = muteSymbol,
            muteSymbolRenderer = muteSymbolRenderer,
            outlineRoot = outlineRoot,
            techniqueRoot = techniqueRoot,
            techniqueSegmentRibbons = techniqueSegmentRibbons,
            techniqueSegmentRibbonRenderers = techniqueSegmentRibbonRenderers,
            techniqueSegmentRibbonPropertyBlocks = techniqueSegmentRibbonPropertyBlocks,
            slideRibbon = slideRibbon,
            slideRibbonRenderer = slideRibbonRenderer,
            slideRibbonPropertyBlock = slideRibbonRenderer != null ? new MaterialPropertyBlock() : null,
            legatoCurve = legatoCurve,
            legatoCurveRenderer = legatoCurveRenderer,
            bendRibbon = bendRibbon,
            bendRibbonRenderer = bendRibbonRenderer,
            bendRibbonPropertyBlock = bendRibbonRenderer != null ? new MaterialPropertyBlock() : null,
            bendSustainRibbon = bendSustainRibbon,
            bendSustainRibbonRenderer = bendSustainRibbonRenderer,
            bendSustainRibbonPropertyBlock = bendSustainRibbonRenderer != null ? new MaterialPropertyBlock() : null,
            sustainRibbon = sustainRibbon,
            sustainRibbonRenderer = sustainRibbonRenderer,
            sustainRibbonPropertyBlock = sustainRibbonRenderer != null ? new MaterialPropertyBlock() : null,
            baseColor = owner.GetStringColor(data.stringIdx),
            baseScale = cube.transform.localScale
        };
    }

    private void UpdateNoteView(HighwayNoteView view, GameplayNoteState state, float z, float rawTravelZ, float songTime)
    {
        if (view.noteRoot == null)
            return;

        float x = GetVisualNoteX(state.data);
        float y = GetStringY(state.data.stringIdx);
        float floorY = GetLaneSurfaceTopY();
        float visualNoteZ = z - GetVisualNoteStrikeOffset(view);
        float rawVisualNoteZ = rawTravelZ - GetVisualNoteStrikeOffset(view);
        float laneTagY = GetLaneGuideStringY() + 0.15f;
        float laneTagZ = visualNoteZ - 0.55f;

        view.noteRoot.transform.position = new Vector3(x, y, visualNoteZ);
        if (view.marker != null)
            view.marker.transform.position = new Vector3(x, y, owner.StrikeLineZ);
        if (view.outlineRoot != null)
        {
            view.outlineRoot.transform.position = new Vector3(x, y, GetStuckOutlineCenterZ());
            view.outlineRoot.transform.localScale = Vector3.one;
        }

        bool isStuckOnString = !state.IsResolved && z <= owner.StrikeLineZ + 0.001f;
        bool hideBendTargetBox = bendSourceByDestinationId.ContainsKey(state.data.id);
        bool hideSlideTargetBox = IsSlideDestinationNote(state.data);
        bool hideTravelingNoteBox = hideBendTargetBox || hideSlideTargetBox;
        bool keepNoteBoxVisibleOnString =
            currentNoteByNoteModeEnabled &&
            currentNoteByNoteWaitingForMatch &&
            !state.IsResolved &&
            isStuckOnString &&
            !hideTravelingNoteBox;
        bool hideResolvedCoreVisuals = state.IsResolved &&
            songTime - state.resolvedAt > GetResolvedFadeTime() &&
            ShouldKeepTechniqueAliveAfterResolution(state.data, songTime);
        if (view.noteRenderer != null)
            view.noteRenderer.enabled = !hideResolvedCoreVisuals && (!isStuckOnString || keepNoteBoxVisibleOnString) && !hideTravelingNoteBox;
        if (view.outlineRoot != null)
            SetGameObjectActive(view.outlineRoot, !hideResolvedCoreVisuals && isStuckOnString && !keepNoteBoxVisibleOnString);
        if (view.label != null)
            SetGameObjectActive(view.label.gameObject, !hideResolvedCoreVisuals);

        float tailLength = Mathf.Max(0f, z - owner.StrikeLineZ);
        if (view.tail != null)
        {
            view.tail.transform.position = new Vector3(x, y, owner.StrikeLineZ + (tailLength * 0.5f));
            view.tail.transform.localScale = new Vector3(owner.FretSpacing * 0.06f, 0.06f, tailLength);
            SetGameObjectActive(view.tail, owner.highwayShowApproachLine && tailLength > 0.01f && !state.IsResolved);
        }

        if (view.tether != null && view.tetherRenderer != null && view.tetherMaterial != null)
        {
            float noteBottomY = y - (view.baseScale.y * 0.5f);
            float tetherTopGap = Mathf.Max(0.18f, view.baseScale.y * 0.7f);
            float tetherTopY = noteBottomY - tetherTopGap;
            float tetherLength = Mathf.Max(0f, tetherTopY - floorY);
            bool showTether = tetherLength > 0.02f && z > owner.StrikeLineZ + 0.001f && !state.IsResolved;
            view.tether.transform.position = new Vector3(x, floorY + (tetherLength * 0.5f), visualNoteZ);
            view.tether.transform.localScale = new Vector3(Mathf.Max(0.04f, owner.FretSpacing * 0.05f), tetherLength, Mathf.Max(0.03f, owner.FretSpacing * 0.04f));
            SetGameObjectActive(view.tether, showTether);
        }

        if (view.laneTagLabel != null)
        {
            view.laneTagLabel.transform.position = new Vector3(x, laneTagY, laneTagZ);
            ApplyFretNumberLabelStyle(view.laneTagLabel, true);
            SetGameObjectActive(view.laneTagLabel.gameObject, z > owner.StrikeLineZ + 0.001f && !state.IsResolved);
        }

        view.noteRoot.transform.localScale = view.baseScale;

        Color finalColor = view.baseColor;
        float emission = 0.8f;

        if (state.IsHit || state.IsMissed)
        {
            float fade = Mathf.Clamp01((songTime - state.resolvedAt) / Mathf.Max(0.01f, GetResolvedFadeTime()));
            Color resolvedColor = state.IsHit ? Color.white : owner.highwayMissColor;
            finalColor = Color.Lerp(resolvedColor, owner.highwayBackgroundColor, fade);
            emission = Mathf.Lerp(state.IsHit ? 1.8f : 0.45f, 0f, fade);
            if (state.IsHit)
                view.noteRoot.transform.localScale = view.baseScale * Mathf.Lerp(1.18f, 1f, fade);
        }
        else if (state.isJudgeable)
        {
            emission = 0.95f;
            finalColor = view.baseColor;
        }

        view.noteMaterial.color = finalColor;
        view.noteMaterial.EnableKeyword("_EMISSION");
        view.noteMaterial.SetColor("_EmissionColor", finalColor * Mathf.Pow(2f, emission));

        if (view.tetherMaterial != null)
        {
            Color tetherColor = new Color(finalColor.r, finalColor.g, finalColor.b, state.IsResolved ? Mathf.Clamp01(finalColor.a * 0.55f) : 0.95f);
            view.tetherMaterial.color = tetherColor;
            view.tetherMaterial.SetColor("_Color", tetherColor);
            view.tetherMaterial.SetColor("_BaseColor", tetherColor);
            view.tetherMaterial.SetColor("_TintColor", tetherColor);
        }

        if (view.marker != null)
        {
            SetGameObjectActive(view.marker, owner.highwayShowLandingDot && !hideResolvedCoreVisuals);
            Color markerColor = state.IsHit ? owner.highwayHitColor : (state.IsMissed ? owner.highwayMissColor : view.baseColor);
            if (view.markerMaterial != null)
            {
                view.markerMaterial.color = markerColor;
                view.markerMaterial.SetColor("_EmissionColor", markerColor * (state.IsHit ? 2f : 0.8f));
            }
        }

        bool hideOverlaySymbol = songTime >= state.data.time - 0.0001f;

        if (view.bendArrow != null && view.bendArrowRenderer != null && view.bendArrowPropertyBlock != null)
        {
            Vector3 currentScale = view.noteRoot.transform.localScale;
            float arrowWidth = Mathf.Max(0.05f, currentScale.x * BendArrowWidthFraction);
            float arrowHeight = Mathf.Max(0.05f, currentScale.y);
            float arrowFrontZ = visualNoteZ - (currentScale.z * 0.5f) - BendArrowFrontOffset;
            float arrowBaseY = y + (currentScale.y * 0.5f);
            bool showPrimaryArrow = !hideResolvedCoreVisuals && !hideTravelingNoteBox && !hideOverlaySymbol;
            int roundedBendSemitones = Mathf.Max(0, Mathf.RoundToInt(state.data.bendStep));
            bool showSecondaryArrow = showPrimaryArrow && roundedBendSemitones > 1;

            view.bendArrow.transform.position = new Vector3(x, arrowBaseY, arrowFrontZ);
            view.bendArrow.transform.rotation = Quaternion.identity;
            view.bendArrow.transform.localScale = new Vector3(arrowWidth, arrowHeight, 1f);
            SetGameObjectActive(view.bendArrow, showPrimaryArrow);
            view.bendArrowRenderer.GetPropertyBlock(view.bendArrowPropertyBlock);
            view.bendArrowPropertyBlock.SetColor(BendArrowBaseColorShaderId, finalColor);
            view.bendArrowRenderer.SetPropertyBlock(view.bendArrowPropertyBlock);

            if (view.bendArrowSecondary != null && view.bendArrowSecondaryRenderer != null && view.bendArrowSecondaryPropertyBlock != null)
            {
                view.bendArrowSecondary.transform.position = new Vector3(x, arrowBaseY + (arrowHeight * BendArrowStackOffsetFraction), arrowFrontZ);
                view.bendArrowSecondary.transform.rotation = Quaternion.identity;
                view.bendArrowSecondary.transform.localScale = new Vector3(arrowWidth, arrowHeight, 1f);
                SetGameObjectActive(view.bendArrowSecondary, showSecondaryArrow);
                view.bendArrowSecondaryRenderer.GetPropertyBlock(view.bendArrowSecondaryPropertyBlock);
                view.bendArrowSecondaryPropertyBlock.SetColor(BendArrowBaseColorShaderId, finalColor);
                view.bendArrowSecondaryRenderer.SetPropertyBlock(view.bendArrowSecondaryPropertyBlock);
            }
        }

        if (view.muteSymbol != null)
        {
            Vector3 currentScale = view.noteRoot.transform.localScale;
            float referenceNoteSize = Mathf.Max(GetSingleFrettedNoteScale().y, currentScale.y);
            float symbolSize = Mathf.Max(0.05f, referenceNoteSize * MuteSymbolScaleFraction);
            float symbolFrontZ = visualNoteZ - (currentScale.z * 0.5f) - MuteSymbolFrontOffset;
            bool showMuteSymbol = ShouldShowMuteSymbolForNote(state.data) && !hideResolvedCoreVisuals && !hideTravelingNoteBox && !hideOverlaySymbol;

            view.muteSymbol.transform.position = new Vector3(x, y, symbolFrontZ);
            view.muteSymbol.transform.rotation = Quaternion.identity;
            view.muteSymbol.transform.localScale = new Vector3(symbolSize, symbolSize, 1f);
            SetGameObjectActive(view.muteSymbol, showMuteSymbol);
        }

        UpdateTechniqueView(view, state, visualNoteZ, rawVisualNoteZ, songTime);
    }


    private void RebuildVisibleNoteStateCache(GuitarGameplaySnapshot snapshot)
    {
        noteStatesById.Clear();
        if (snapshot == null || snapshot.noteStates == null)
            return;

        for (int i = 0; i < snapshot.noteStates.Count; i++)
        {
            GameplayNoteState state = snapshot.noteStates[i];
            if (state == null)
                continue;

            noteStatesById[state.data.id] = state;
        }
    }

    private void UpdateTechniqueView(HighwayNoteView view, GameplayNoteState state, float displayVisualZ, float rawVisualNoteZ, float songTime)
    {
        if (view.techniqueRoot == null)
            return;

        if (IsSlideDestinationNote(state.data))
        {
            HideTechniqueView(view);
            return;
        }

        if (HasTechniqueSegments(state.data))
        {
            bool showSegments = UpdateTechniqueSegmentRibbons(view, state, rawVisualNoteZ, songTime);
            SetGameObjectActive(view.techniqueRoot, showSegments);
            return;
        }

        bool showSlide = UpdateSlideTechnique(view, state, displayVisualZ, songTime);
        bool showBend = UpdateBendTechnique(view, state, rawVisualNoteZ, songTime);
        bool showSustain = UpdateNoteSustainTechnique(view, state, rawVisualNoteZ, songTime);
        SetGameObjectActive(view.techniqueRoot, showSlide || showBend || showSustain);
    }

    private void HideTechniqueView(HighwayNoteView view)
    {
        if (view.slideRibbon != null)
            SetGameObjectActive(view.slideRibbon, false);
        if (view.legatoCurve != null)
            SetGameObjectActive(view.legatoCurve, false);
        if (view.bendRibbon != null)
            SetGameObjectActive(view.bendRibbon, false);
        if (view.bendSustainRibbon != null)
            SetGameObjectActive(view.bendSustainRibbon, false);
        if (view.sustainRibbon != null)
            SetGameObjectActive(view.sustainRibbon, false);

        if (view.techniqueSegmentRibbons != null)
        {
            for (int i = 0; i < view.techniqueSegmentRibbons.Length; i++)
                SetGameObjectActive(view.techniqueSegmentRibbons[i], false);
        }

        SetGameObjectActive(view.techniqueRoot, false);
    }

    private bool UpdateSlideTechnique(HighwayNoteView view, GameplayNoteState state, float z, float songTime)
    {
        bool useLegatoCurve = view.legatoCurve != null && view.legatoCurveRenderer != null;
        if (!useLegatoCurve && (view.slideRibbon == null || view.slideRibbonRenderer == null))
            return false;

        if (state.data.linkedFromNoteId >= 0 && state.data.slideTargetFret < 0)
        {
            if (view.slideRibbon != null)
                view.slideRibbon.SetActive(false);
            if (view.legatoCurve != null)
                view.legatoCurve.SetActive(false);
            return false;
        }

        NoteData anchorData = state.data;
        int targetFret = anchorData.slideTargetFret;
        if (targetFret < 0 || anchorData.fret <= 0)
        {
            if (view.slideRibbon != null)
                view.slideRibbon.SetActive(false);
            if (view.legatoCurve != null)
                view.legatoCurve.SetActive(false);
            return false;
        }

        if (!TryBuildSlideRibbonProfile(view, state, z, songTime, out TechniqueRibbonProfile liveProfile))
        {
            view.slideRibbonFadeState.freezeActive = false;
            if (view.slideRibbon != null)
                view.slideRibbon.SetActive(false);
            if (view.legatoCurve != null)
                view.legatoCurve.SetActive(false);
            return false;
        }

        float fadeStartSongTime = anchorData.time;
        float fadeEndSongTime = GetSlideRibbonFadeEndTime(anchorData, songTime, liveProfile);
        bool shouldFreezeRibbon = songTime >= fadeStartSongTime - 0.0001f;

        if (shouldFreezeRibbon)
        {
            if (!view.slideRibbonFadeState.freezeActive)
            {
                view.slideRibbonFadeState.freezeActive = true;
                view.slideRibbonFadeState.fadeStartSongTime = fadeStartSongTime;
                view.slideRibbonFadeState.fadeEndSongTime = Mathf.Max(fadeStartSongTime + 0.02f, fadeEndSongTime);
            }
        }
        else
        {
            view.slideRibbonFadeState.freezeActive = false;
        }

        float visibleStart01 = 0f;
        if (view.slideRibbonFadeState.freezeActive)
        {
            float fadeDuration = Mathf.Max(0.02f, view.slideRibbonFadeState.fadeEndSongTime - view.slideRibbonFadeState.fadeStartSongTime);
            visibleStart01 = Mathf.Clamp01((songTime - view.slideRibbonFadeState.fadeStartSongTime) / fadeDuration);
            if (visibleStart01 >= 0.999f)
            {
                if (view.slideRibbon != null)
                    view.slideRibbon.SetActive(false);
                if (view.legatoCurve != null)
                    view.legatoCurve.SetActive(false);
                return false;
            }
        }

        if (useLegatoCurve)
            ApplyLegatoCurveTechnique(view, liveProfile, state.IsResolved, visibleStart01);
        else
            ApplySlideTechniqueRibbon(view, liveProfile, state.IsResolved, visibleStart01);
        return true;
    }

    private float GetSlideRibbonFadeEndTime(NoteData anchorData, float songTime, TechniqueRibbonProfile profile)
    {
        if (slideDestinationBySourceId.TryGetValue(anchorData.id, out int destinationId) &&
            chartById.TryGetValue(destinationId, out NoteData destinationData))
        {
            return destinationData.time;
        }

        float estimatedTravelSeconds = Vector3.Distance(profile.start, profile.end) / Mathf.Max(0.01f, currentVisualNoteSpeed);
        return Mathf.Max(anchorData.time + 0.1f, songTime + estimatedTravelSeconds);
    }

    private bool TryBuildSlideRibbonProfile(HighwayNoteView view, GameplayNoteState state, float noteCenterZ, float songTime, out TechniqueRibbonProfile profile)
    {
        profile = default;

        NoteData anchorData = state.data;
        float startDepth = Mathf.Max(0.1f, view.baseScale.z);
        float startTravelZ = noteCenterZ + (startDepth * 0.5f);
        float startAttachZ = startTravelZ;
        float startX = GetVisualNoteX(anchorData);
        float startY = GetStringY(anchorData.stringIdx);

        NoteData? destinationData = null;
        if (slideDestinationBySourceId.TryGetValue(anchorData.id, out int destinationId) && chartById.TryGetValue(destinationId, out NoteData resolvedDestination))
            destinationData = resolvedDestination;

        float endX = destinationData.HasValue ? GetVisualNoteX(destinationData.Value) : GetNoteX(anchorData.slideTargetFret);
        float endY = destinationData.HasValue ? GetStringY(destinationData.Value.stringIdx) : startY;
        float endDepth = startDepth;
        if (destinationData.HasValue)
        {
            if (noteViews.TryGetValue(destinationData.Value.id, out HighwayNoteView destinationView) && destinationView != null)
                endDepth = Mathf.Max(0.1f, destinationView.baseScale.z);
            else
                endDepth = GetApproximateTechniqueNoteDepth(destinationData.Value);
        }
        float endTravelZ;
        if (destinationData.HasValue && noteStatesById.TryGetValue(destinationData.Value.id, out GameplayNoteState destinationState))
        {
            endTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((destinationState.data.time - songTime) * currentVisualNoteSpeed));
        }
        else
        {
            endTravelZ = Mathf.Max(startTravelZ + 0.75f, startTravelZ + Mathf.Abs(endX - startX) * 0.50f);
        }

        float endAttachZ = endTravelZ - (endDepth * 0.95f);
        if (endAttachZ <= startAttachZ + 0.05f)
            endAttachZ = startAttachZ + 0.05f;

        Vector3 start = new Vector3(startX, startY, startAttachZ);
        Vector3 end = new Vector3(endX, endY, endAttachZ);
        float length = Vector3.Distance(start, end);
        if (length <= 0.05f)
            return false;

        float leadDistance = Mathf.Clamp(Mathf.Abs(endX - startX) * 0.55f + Mathf.Abs(endAttachZ - startAttachZ) * 0.16f, 0.35f, 2.2f);
        profile.start = start;
        profile.control1 = start + new Vector3(0f, 0f, leadDistance);
        profile.control2 = end - new Vector3(0f, 0f, leadDistance);
        profile.end = end;
        profile.halfWidth = Mathf.Max(0.18f, view.baseScale.x * 0.38f);
        profile.pathMode = 0f;
        profile.cornerRoundness = 0f;
        return true;
    }

    private float GetApproximateTechniqueNoteDepth(NoteData data)
    {
        if (data.fret <= 0)
            return GetSingleOpenNoteScale().z;

        return GetSingleFrettedNoteScale().z;
    }

    private bool IsSlideDestinationNote(NoteData data)
    {
        if (data.linkedFromNoteId < 0)
            return false;

        return chartById.TryGetValue(data.linkedFromNoteId, out NoteData source) &&
               source.technique == NoteTechnique.Slide;
    }

    private static bool IsLegatoCurveTechnique(NoteData data)
    {
        return data.technique == NoteTechnique.HammerOn || data.technique == NoteTechnique.PullOff;
    }

    private static bool HasBendRibbon(NoteData data)
    {
        return data.technique == NoteTechnique.Bend || data.bendStep > 0f || data.bendPreBend || data.bendRelease;
    }

    private static bool HasTechniqueSegments(NoteData data)
    {
        return data.techniqueSegments != null && data.techniqueSegments.Count > 0;
    }

    private static int GetTechniqueSegmentRibbonSlotCount(NoteData data)
    {
        if (!HasTechniqueSegments(data))
            return 0;

        int slotCount = 0;
        for (int i = 0; i < data.techniqueSegments.Count; i++)
            slotCount += GetTechniqueSegmentVisualSlotCount(data.techniqueSegments[i]);

        return Mathf.Max(1, slotCount);
    }

    private static int GetTechniqueSegmentVisualSlotCount(NoteTechniqueSegmentData segment)
    {
        switch (segment.type)
        {
            case NoteTechniqueSegmentType.Bend:
                return 2;
            case NoteTechniqueSegmentType.Vibrato:
                return GetVibratoSubSegmentCount(segment);
            default:
                return 1;
        }
    }

    private static int GetVibratoSubSegmentCount(NoteTechniqueSegmentData segment)
    {
        float duration = Mathf.Max(0.02f, segment.endOffset - segment.startOffset);
        int cycles = Mathf.Max(2, Mathf.RoundToInt(duration * VibratoCyclesPerSecond));
        return Mathf.Clamp(cycles * 2, VibratoMinimumHalfWaves, VibratoMaximumHalfWaves);
    }

    private static bool HasNoteSustainRibbon(NoteData data)
    {
        return data.duration > GuitarTechniqueVisualThresholds.SustainSeconds &&
               data.fret > 0 &&
               !HasBendRibbon(data) &&
               data.slideTargetFret < 0 &&
               data.linkedFromNoteId < 0;
    }

    private bool TryBuildBendRibbonProfiles(
        HighwayNoteView view,
        GameplayNoteState state,
        float noteCenterZ,
        float songTime,
        out TechniqueRibbonProfile headProfile,
        out bool hasSustainTail,
        out TechniqueRibbonProfile sustainTailProfile,
        out float totalDisplayedDepth)
    {
        headProfile = default;
        sustainTailProfile = default;
        hasSustainTail = false;
        totalDisplayedDepth = 0f;

        float bendAmount = Mathf.Max(0f, state.data.bendStep);
        bool startsBent = state.data.bendPreBend || state.data.bendRelease;
        if (bendAmount <= 0f && !startsBent)
            return false;

        float startDepth = Mathf.Max(0.1f, view.baseScale.z);
        float startAttachZ = noteCenterZ + (startDepth * 0.5f);
        float startX = GetVisualNoteX(state.data);
        float startY = GetStringY(state.data.stringIdx);
        float bendHeight = GetStringLaneSpacing() * BendRibbonVisualHeightInStrings;
        float targetY = startY + bendHeight;

        float bendEndTime = state.data.time + Mathf.Max(0.14f, state.data.duration);
        float fullEndTravelZ = Mathf.Max(
            owner.StrikeLineZ,
            owner.StrikeLineZ + ((bendEndTime - songTime) * currentVisualNoteSpeed));
        float minimumVisualDepth = BendRibbonLeadOutDistance + BendRibbonCornerDepth + BendRibbonMinimumTopHoldDistance;
        float fullEndAttachZ = Mathf.Max(startAttachZ + minimumVisualDepth, Mathf.Max(startAttachZ + 0.4f, fullEndTravelZ));
        float totalDepth = Mathf.Max(minimumVisualDepth, fullEndAttachZ - startAttachZ);
        float leadOutZ = Mathf.Clamp(BendRibbonLeadOutDistance, 0.12f, totalDepth - 0.16f);
        float riseDepth = Mathf.Clamp(BendRibbonCornerDepth, 0.03f, totalDepth - leadOutZ - 0.12f);
        float topHoldLength = Mathf.Max(BendRibbonMinimumTopHoldDistance, totalDepth - leadOutZ - riseDepth);
        float maxHeadTopHoldLength = Mathf.Max(BendRibbonMinimumTopHoldDistance, currentVisualNoteSpeed * BendRibbonHeadMaximumFlatHoldSeconds);
        float headTopHoldLength = Mathf.Min(topHoldLength, maxHeadTopHoldLength);
        float curveEntryZ = startAttachZ + leadOutZ;
        float curvePeakZ = curveEntryZ + riseDepth;
        float headEndAttachZ = curvePeakZ + headTopHoldLength;

        headProfile.start = new Vector3(startX, startY, startAttachZ);
        headProfile.control1 = new Vector3(startX, startY, curveEntryZ);
        headProfile.control2 = new Vector3(startX, targetY, curvePeakZ);
        headProfile.end = new Vector3(startX, targetY, headEndAttachZ);

        headProfile.halfWidth = Mathf.Max(0.16f, view.baseScale.x * 0.34f);
        headProfile.pathMode = 1f;
        headProfile.cornerRoundness = Mathf.Max(0f, BendRibbonCornerRoundness);
        float totalSpan = Mathf.Max(0.01f, headEndAttachZ - startAttachZ);
        float darkBandPadding = Mathf.Clamp(BendRibbonDarkBandPaddingDistance, 0.04f, totalSpan * 0.35f);
        headProfile.darkBandStart01 = Mathf.Clamp01(((curveEntryZ - darkBandPadding) - startAttachZ) / totalSpan);
        headProfile.darkBandEnd01 = Mathf.Clamp01(((curvePeakZ + darkBandPadding) - startAttachZ) / totalSpan);

        if (fullEndAttachZ > headEndAttachZ + 0.05f)
        {
            hasSustainTail = true;
            float headFlatTopLength = Mathf.Max(0.01f, headEndAttachZ - curvePeakZ);
            float initialTailStartZ = headEndAttachZ;
            float initialTailDepth = Mathf.Max(0.01f, fullEndAttachZ - initialTailStartZ);
            TechniqueRibbonProfile initialTailProfile = default;
            initialTailProfile.start = new Vector3(startX, targetY, initialTailStartZ);
            initialTailProfile.control1 = new Vector3(startX, targetY, initialTailStartZ + (initialTailDepth / 3f));
            initialTailProfile.control2 = new Vector3(startX, targetY, initialTailStartZ + ((initialTailDepth * 2f) / 3f));
            initialTailProfile.end = new Vector3(startX, targetY, fullEndAttachZ);

            float joinOverlap = Mathf.Min(
                headFlatTopLength,
                GetRibbonLengthFadeWorldDistance(headProfile) + GetRibbonLengthFadeWorldDistance(initialTailProfile));
            float tailStartZ = headEndAttachZ - joinOverlap;
            float tailEndZ = fullEndAttachZ;
            float tailDepth = Mathf.Max(0.01f, tailEndZ - tailStartZ);
            float firstControlZ = tailStartZ + (tailDepth / 3f);
            float secondControlZ = tailStartZ + ((tailDepth * 2f) / 3f);

            sustainTailProfile.start = new Vector3(startX, targetY, tailStartZ);
            sustainTailProfile.control1 = new Vector3(startX, targetY, firstControlZ);
            sustainTailProfile.control2 = new Vector3(startX, targetY, secondControlZ);
            sustainTailProfile.end = new Vector3(startX, targetY, tailEndZ);
            sustainTailProfile.halfWidth = headProfile.halfWidth;
            sustainTailProfile.pathMode = 0f;
            sustainTailProfile.cornerRoundness = 0f;
            sustainTailProfile.darkBandStart01 = 1f;
            sustainTailProfile.darkBandEnd01 = 1f;
        }

        totalDisplayedDepth = Mathf.Max(0.01f, fullEndAttachZ - startAttachZ);
        return true;
    }

    private static float GetRibbonLengthFadeWorldDistance(TechniqueRibbonProfile profile)
    {
        float approximateLength =
            Vector3.Distance(profile.start, profile.control1) +
            Vector3.Distance(profile.control1, profile.control2) +
            Vector3.Distance(profile.control2, profile.end);
        float fadeSoftness01 = Mathf.Clamp(0.75f / Mathf.Max(0.01f, approximateLength), 0.005f, 0.05f);
        return approximateLength * fadeSoftness01;
    }

    private void ApplySlideTechniqueRibbon(HighwayNoteView view, TechniqueRibbonProfile profile, bool isResolved, float visibleStart01)
    {
        Color centerBaseColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.04f : 0.08f);
        Color centerColor = new Color(centerBaseColor.r, centerBaseColor.g, centerBaseColor.b, isResolved ? 0.28f : 0.58f);
        Color edgeColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.34f : 0.64f);
        edgeColor.a = isResolved ? 0.46f : 0.98f;
        Color emissionColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.34f : 0.82f) * Mathf.Pow(2f, isResolved ? 0.40f : 1.32f);

        ApplyTechniqueRibbon(
            view.techniqueRoot.transform,
            view.slideRibbon,
            view.slideRibbonRenderer,
            view.slideRibbonPropertyBlock,
            profile,
            centerColor,
            edgeColor,
            emissionColor,
            visibleStart01,
            0f);
    }

    private void ApplyLegatoCurveTechnique(HighwayNoteView view, TechniqueRibbonProfile profile, bool isResolved, float visibleStart01)
    {
        if (view.legatoCurve == null || view.legatoCurveRenderer == null)
            return;

        SetGameObjectActive(view.legatoCurve, true);

        LineRenderer line = view.legatoCurveRenderer;
        int sampleCount = Mathf.Max(2, LegatoCurveSamples);
        float startT = Mathf.Clamp01(visibleStart01);
        float remaining = 1f - startT;
        if (remaining <= 0.001f)
        {
            view.legatoCurve.SetActive(false);
            return;
        }

        line.positionCount = sampleCount;
        for (int i = 0; i < sampleCount; i++)
        {
            float normalized = i / (float)(sampleCount - 1);
            float t = startT + (normalized * remaining);
            line.SetPosition(i, EvaluateTechniqueBezier(profile, t));
        }

        float width = Mathf.Max(0.04f, view.baseScale.x * LegatoCurveWidthFraction);
        line.startWidth = width;
        line.endWidth = width * 0.92f;

        Color lineColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.12f : 0.08f);
        float alpha = isResolved ? 0.58f : 0.96f;
        lineColor.a = alpha * (1f - (visibleStart01 * 0.8f));
        line.startColor = lineColor;
        line.endColor = lineColor;

        Material lineMat = line.material;
        if (lineMat != null)
        {
            lineMat.color = lineColor;
            lineMat.EnableKeyword("_EMISSION");
            lineMat.SetColor("_EmissionColor", Color.Lerp(view.baseColor, Color.white, 0.18f) * Mathf.Pow(2f, isResolved ? 0.55f : 1.35f));
        }

        if (view.slideRibbon != null)
            view.slideRibbon.SetActive(false);
    }

    private static Vector3 EvaluateTechniqueBezier(TechniqueRibbonProfile profile, float t)
    {
        float u = 1f - t;
        return
            (u * u * u * profile.start) +
            (3f * u * u * t * profile.control1) +
            (3f * u * t * t * profile.control2) +
            (t * t * t * profile.end);
    }

    private void ApplyBendTechniqueRibbon(HighwayNoteView view, TechniqueRibbonProfile profile, bool isResolved, float visibleStart01)
    {
        ApplyBendTechniqueRibbon(
            view,
            view.bendRibbon,
            view.bendRibbonRenderer,
            view.bendRibbonPropertyBlock,
            profile,
            isResolved,
            visibleStart01);
    }

    private void ApplyBendTechniqueRibbon(
        HighwayNoteView view,
        GameObject ribbon,
        Renderer ribbonRenderer,
        MaterialPropertyBlock propertyBlock,
        TechniqueRibbonProfile profile,
        bool isResolved,
        float visibleStart01)
    {
        Color centerBaseColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.10f : 0.16f);
        Color centerColor = new Color(centerBaseColor.r, centerBaseColor.g, centerBaseColor.b, isResolved ? 0.34f : 0.70f);
        Color edgeColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.42f : 0.70f);
        edgeColor.a = isResolved ? 0.50f : 1.0f;
        Color emissionColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.44f : 0.90f) * Mathf.Pow(2f, isResolved ? 0.46f : 1.38f);

        ApplyTechniqueRibbon(
            view.techniqueRoot.transform,
            ribbon,
            ribbonRenderer,
            propertyBlock,
            profile,
            centerColor,
            edgeColor,
            emissionColor,
            visibleStart01,
            BendRibbonFlatLightStrength);
    }

    private void ApplyNoteSustainTechniqueRibbon(HighwayNoteView view, TechniqueRibbonProfile profile, bool isResolved, float visibleStart01)
    {
        Color centerBaseColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.06f : 0.10f);
        Color centerColor = new Color(centerBaseColor.r, centerBaseColor.g, centerBaseColor.b, isResolved ? 0.26f : 0.54f);
        Color edgeColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.26f : 0.52f);
        edgeColor.a = isResolved ? 0.40f : 0.88f;
        Color emissionColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.26f : 0.62f) * Mathf.Pow(2f, isResolved ? 0.34f : 1.0f);

        ApplyTechniqueRibbon(
            view.techniqueRoot.transform,
            view.sustainRibbon,
            view.sustainRibbonRenderer,
            view.sustainRibbonPropertyBlock,
            profile,
            centerColor,
            edgeColor,
            emissionColor,
            visibleStart01,
            0f);
    }

    private void ApplyTechniqueRibbon(
        Transform techniqueRoot,
        GameObject ribbon,
        Renderer ribbonRenderer,
        MaterialPropertyBlock propertyBlock,
        TechniqueRibbonProfile profile,
        Color centerColor,
        Color edgeColor,
        Color emissionColor,
        float visibleStart01,
        float flatLightStrength)
    {
        if (ribbon == null || ribbonRenderer == null || techniqueRoot == null || propertyBlock == null)
            return;

        Vector3 center = (profile.start + profile.end) * 0.5f;
        ribbon.transform.localPosition = center;
        ribbon.transform.localRotation = Quaternion.identity;
        ribbon.transform.localScale = Vector3.one;

        propertyBlock.Clear();

        propertyBlock.SetVector(CurveP0ShaderId, profile.start - center);
        propertyBlock.SetVector(CurveP1ShaderId, profile.control1 - center);
        propertyBlock.SetVector(CurveP2ShaderId, profile.control2 - center);
        propertyBlock.SetVector(CurveP3ShaderId, profile.end - center);
        propertyBlock.SetFloat(HalfWidthShaderId, profile.halfWidth);
        propertyBlock.SetColor(CenterColorShaderId, centerColor);
        propertyBlock.SetColor(EdgeColorShaderId, edgeColor);
        propertyBlock.SetColor(EmissionColorShaderId, emissionColor);
        float approxRibbonLength =
            Vector3.Distance(profile.start, profile.control1) +
            Vector3.Distance(profile.control1, profile.control2) +
            Vector3.Distance(profile.control2, profile.end);
        float visibleFadeSoftness01 = Mathf.Clamp(0.55f / Mathf.Max(0.01f, approxRibbonLength), 0.0025f, 0.03f);
        float lengthFadeSoftness01 = Mathf.Clamp(0.75f / Mathf.Max(0.01f, approxRibbonLength), 0.005f, 0.05f);
        propertyBlock.SetFloat(VisibleStart01ShaderId, Mathf.Clamp01(visibleStart01));
        propertyBlock.SetFloat(VisibleFadeSoftness01ShaderId, visibleFadeSoftness01);
        propertyBlock.SetFloat(LengthFadeSoftness01ShaderId, lengthFadeSoftness01);
        propertyBlock.SetFloat(FlatLightStrengthShaderId, Mathf.Clamp01(flatLightStrength));
        propertyBlock.SetFloat(PathModeShaderId, profile.pathMode);
        propertyBlock.SetFloat(CornerRoundnessShaderId, Mathf.Max(0f, profile.cornerRoundness));
        propertyBlock.SetFloat(DarkBandStart01ShaderId, Mathf.Clamp01(profile.darkBandStart01));
        propertyBlock.SetFloat(DarkBandEnd01ShaderId, Mathf.Clamp01(profile.darkBandEnd01));
        ribbonRenderer.SetPropertyBlock(propertyBlock);
        ribbon.SetActive(true);
    }

    private bool UpdateBendTechnique(HighwayNoteView view, GameplayNoteState state, float z, float songTime)
    {
        if (view.bendRibbon == null || view.bendRibbonRenderer == null)
            return false;

        float bendAmount = Mathf.Max(0f, state.data.bendStep);
        if (bendAmount <= 0f && !state.data.bendPreBend && !state.data.bendRelease)
        {
            view.bendRibbon.SetActive(false);
            if (view.bendSustainRibbon != null)
                view.bendSustainRibbon.SetActive(false);
            return false;
        }

        if (!TryBuildBendRibbonProfiles(
                view,
                state,
                z,
                songTime,
                out TechniqueRibbonProfile headProfile,
                out bool hasSustainTail,
                out TechniqueRibbonProfile sustainTailProfile,
                out float totalDisplayedDepth))
        {
            view.bendRibbon.SetActive(false);
            if (view.bendSustainRibbon != null)
                view.bendSustainRibbon.SetActive(false);
            return false;
        }

        float fadeStartSongTime = state.data.time;
        float displayedRibbonDuration = Mathf.Max(0.02f, totalDisplayedDepth / Mathf.Max(0.01f, currentVisualNoteSpeed));
        float fadeEndSongTime = fadeStartSongTime + displayedRibbonDuration;
        float visibleDistance = 0f;
        if (songTime >= fadeStartSongTime)
        {
            float overallVisibleStart01 = Mathf.Clamp01((songTime - fadeStartSongTime) / Mathf.Max(0.02f, fadeEndSongTime - fadeStartSongTime));
            visibleDistance = overallVisibleStart01 * totalDisplayedDepth;
        }

        float headDepth = Mathf.Max(0.01f, headProfile.end.z - headProfile.start.z);
        float headVisibleStart01 = Mathf.Clamp01(visibleDistance / headDepth);
        float tailVisibleStart01 = 0f;
        if (hasSustainTail)
        {
            float tailDepth = Mathf.Max(0.01f, sustainTailProfile.end.z - sustainTailProfile.start.z);
            tailVisibleStart01 = Mathf.Clamp01((visibleDistance - headDepth) / tailDepth);
        }

        if (DebugBendRibbonLogs && !debugLoggedBendProfileIds.Contains(state.data.id))
        {
            debugLoggedBendProfileIds.Add(state.data.id);
            Debug.Log(
                $"[BEND RENDER] id={state.data.id} songTime={songTime:F3} noteTime={state.data.time:F3} " +
                $"dur={state.data.duration:F3} visualStart={state.data.bendVisualStartTime:F3} visualDur={state.data.bendVisualDuration:F3} " +
                $"bend={state.data.bendStep:F2} pre={state.data.bendPreBend} rel={state.data.bendRelease} " +
                $"visibleStart01={headVisibleStart01:F3} start={headProfile.start} c1={headProfile.control1} c2={headProfile.control2} end={headProfile.end}");
        }

        if (DebugBendRibbonLogs &&
            !debugLoggedBendNearStrikeIds.Contains(state.data.id) &&
            Mathf.Abs(songTime - state.data.time) <= 0.08f)
        {
            debugLoggedBendNearStrikeIds.Add(state.data.id);
            Debug.Log(
                $"[BEND NEAR STRIKE] id={state.data.id} songTime={songTime:F3} noteTime={state.data.time:F3} " +
                $"dur={state.data.duration:F3} visualStart={state.data.bendVisualStartTime:F3} visualDur={state.data.bendVisualDuration:F3} " +
                $"bend={state.data.bendStep:F2} pre={state.data.bendPreBend} rel={state.data.bendRelease} " +
                $"visibleStart01={headVisibleStart01:F3} z={z:F3} start={headProfile.start} c1={headProfile.control1} c2={headProfile.control2} end={headProfile.end}");
        }

        bool anyVisible = false;
        if (headVisibleStart01 < 0.999f)
        {
            ApplyBendTechniqueRibbon(view, headProfile, state.IsResolved, headVisibleStart01);
            anyVisible = true;
        }
        else
        {
            view.bendRibbon.SetActive(false);
        }

        if (hasSustainTail && view.bendSustainRibbon != null && view.bendSustainRibbonRenderer != null && tailVisibleStart01 < 0.999f)
        {
            ApplyBendTechniqueRibbon(
                view,
                view.bendSustainRibbon,
                view.bendSustainRibbonRenderer,
                view.bendSustainRibbonPropertyBlock,
                sustainTailProfile,
                state.IsResolved,
                tailVisibleStart01);
            anyVisible = true;
        }
        else if (view.bendSustainRibbon != null)
        {
            view.bendSustainRibbon.SetActive(false);
        }

        return anyVisible;
    }

    private bool TryBuildNoteSustainRibbonProfile(HighwayNoteView view, GameplayNoteState state, float noteCenterZ, float songTime, out TechniqueRibbonProfile profile)
    {
        profile = default;

        if (!HasNoteSustainRibbon(state.data))
            return false;

        float startDepth = Mathf.Max(0.1f, view.baseScale.z);
        float startAttachZ = noteCenterZ + (startDepth * 0.5f);
        float startX = GetVisualNoteX(state.data);
        float startY = GetStringY(state.data.stringIdx);
        float sustainEndTime = state.data.time + Mathf.Max(GuitarTechniqueVisualThresholds.SustainSeconds, state.data.duration);
        float endTravelZ = Mathf.Max(
            owner.StrikeLineZ,
            owner.StrikeLineZ + ((sustainEndTime - songTime) * currentVisualNoteSpeed));
        float endAttachZ = Mathf.Max(startAttachZ + 0.35f, endTravelZ);
        float totalDepth = Mathf.Max(0.35f, endAttachZ - startAttachZ);
        float firstControlZ = startAttachZ + (totalDepth / 3f);
        float secondControlZ = startAttachZ + ((totalDepth * 2f) / 3f);

        profile.start = new Vector3(startX, startY, startAttachZ);
        profile.control1 = new Vector3(startX, startY, firstControlZ);
        profile.control2 = new Vector3(startX, startY, secondControlZ);
        profile.end = new Vector3(startX, startY, startAttachZ + totalDepth);
        profile.halfWidth = Mathf.Max(0.16f, view.baseScale.x * 0.30f);
        profile.pathMode = 0f;
        profile.cornerRoundness = 0f;
        profile.darkBandStart01 = 1f;
        profile.darkBandEnd01 = 1f;
        return true;
    }

    private bool UpdateNoteSustainTechnique(HighwayNoteView view, GameplayNoteState state, float z, float songTime)
    {
        if (view.sustainRibbon == null || view.sustainRibbonRenderer == null)
            return false;

        if (!TryBuildNoteSustainRibbonProfile(view, state, z, songTime, out TechniqueRibbonProfile profile))
        {
            view.sustainRibbon.SetActive(false);
            return false;
        }

        float fadeStartSongTime = state.data.time;
        float fadeEndSongTime = state.data.time + Mathf.Max(GuitarTechniqueVisualThresholds.SustainSeconds, state.data.duration);
        float visibleStart01 = 0f;
        if (songTime >= fadeStartSongTime)
        {
            visibleStart01 = Mathf.Clamp01((songTime - fadeStartSongTime) / Mathf.Max(0.02f, fadeEndSongTime - fadeStartSongTime));
            if (visibleStart01 >= 0.999f)
            {
                view.sustainRibbon.SetActive(false);
                return false;
            }
        }

        ApplyNoteSustainTechniqueRibbon(view, profile, state.IsResolved, visibleStart01);
        return true;
    }

    private bool UpdateTechniqueSegmentRibbons(HighwayNoteView view, GameplayNoteState state, float z, float songTime)
    {
        if (view.techniqueSegmentRibbons == null ||
            view.techniqueSegmentRibbonRenderers == null ||
            view.techniqueSegmentRibbonPropertyBlocks == null ||
            state.data.techniqueSegments == null)
            return false;

        List<NoteTechniqueSegmentData> orderedSegments = state.data.techniqueSegments
            .OrderBy(segment => segment.startOffset)
            .ToList();

        int slotIndex = 0;
        bool anyVisible = false;
        TechniqueRibbonProfile previousProfile = default;
        bool hasPreviousProfile = false;
        float previousEndOffset = -1f;

        for (int segmentIndex = 0; segmentIndex < orderedSegments.Count; segmentIndex++)
        {
            NoteTechniqueSegmentData segment = orderedSegments[segmentIndex];
            if (slotIndex >= view.techniqueSegmentRibbons.Length)
                break;

            bool connectsToNextSegment =
                segmentIndex + 1 < orderedSegments.Count &&
                Mathf.Abs(orderedSegments[segmentIndex + 1].startOffset - segment.endOffset) <= 0.0001f;

            float segmentStartTime = state.data.time + segment.startOffset;
            float segmentEndTime = state.data.time + segment.endOffset;
            float segmentVisibleStart01 = 0f;
            if (songTime >= segmentStartTime)
            {
                segmentVisibleStart01 = Mathf.Clamp01((songTime - segmentStartTime) / Mathf.Max(0.02f, segmentEndTime - segmentStartTime));
                if (segmentVisibleStart01 >= 0.999f)
                {
                    int consumedSlots = GetTechniqueSegmentVisualSlotCount(segment);
                    for (int i = 0; i < consumedSlots && slotIndex + i < view.techniqueSegmentRibbons.Length; i++)
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex + i], false);
                    slotIndex += consumedSlots;
                    continue;
                }
            }

            if (segment.type == NoteTechniqueSegmentType.Bend)
            {
                if (!TryBuildBendSegmentRibbonProfiles(
                        view,
                        state,
                        z,
                        songTime,
                        segment,
                        out TechniqueRibbonProfile headProfile,
                        out bool hasSustainTail,
                        out TechniqueRibbonProfile sustainTailProfile,
                        out float totalDisplayedDepth))
                {
                    if (slotIndex < view.techniqueSegmentRibbons.Length)
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex], false);
                    if (slotIndex + 1 < view.techniqueSegmentRibbons.Length)
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex + 1], false);
                    slotIndex += 2;
                    continue;
                }

                if (hasPreviousProfile && Mathf.Abs(segment.startOffset - previousEndOffset) <= 0.0001f)
                    ApplyRibbonJoinOverlap(previousProfile, ref headProfile);

                float visibleDistance = segmentVisibleStart01 * Mathf.Max(0.01f, totalDisplayedDepth);
                float headDepth = Mathf.Max(0.01f, headProfile.end.z - headProfile.start.z);
                float headVisibleStart01 = Mathf.Clamp01(visibleDistance / headDepth);
                float tailVisibleStart01 = 0f;

                if (headVisibleStart01 < 0.999f)
                {
                    ApplyTechniqueSegmentRibbon(view, slotIndex, segment.type, headProfile, state.IsResolved, headVisibleStart01);
                    anyVisible = true;
                }
                else if (slotIndex < view.techniqueSegmentRibbons.Length)
                {
                    SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex], false);
                }

                if (hasSustainTail)
                {
                    float tailDepth = Mathf.Max(0.01f, sustainTailProfile.end.z - sustainTailProfile.start.z);
                    tailVisibleStart01 = Mathf.Clamp01((visibleDistance - headDepth) / tailDepth);
                    if (tailVisibleStart01 < 0.999f && slotIndex + 1 < view.techniqueSegmentRibbons.Length)
                    {
                        ApplyTechniqueSegmentRibbon(view, slotIndex + 1, NoteTechniqueSegmentType.Sustain, sustainTailProfile, state.IsResolved, tailVisibleStart01);
                        anyVisible = true;
                    }
                    else if (slotIndex + 1 < view.techniqueSegmentRibbons.Length)
                    {
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex + 1], false);
                    }
                    previousProfile = sustainTailProfile;
                }
                else
                {
                    if (slotIndex + 1 < view.techniqueSegmentRibbons.Length)
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex + 1], false);
                    previousProfile = headProfile;
                }

                previousEndOffset = segment.endOffset;
                hasPreviousProfile = true;
                slotIndex += 2;
                continue;
            }

            if (segment.type == NoteTechniqueSegmentType.Vibrato)
            {
                int vibratoSlotCount = GetVibratoSubSegmentCount(segment);
                if (!TryBuildVibratoSegmentMetrics(
                        view,
                        state,
                        z,
                        songTime,
                        segment,
                        out float vibratoStartX,
                        out float vibratoEndX,
                        out float vibratoBaseStartY,
                        out float vibratoBaseEndY,
                        out float vibratoStartAttachZ,
                        out float vibratoTotalDepth))
                {
                    for (int i = 0; i < vibratoSlotCount && slotIndex + i < view.techniqueSegmentRibbons.Length; i++)
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex + i], false);
                    slotIndex += vibratoSlotCount;
                    continue;
                }

                float visibleDistance = segmentVisibleStart01 * Mathf.Max(0.01f, vibratoTotalDepth);
                float cumulativeDepth = 0f;
                TechniqueRibbonProfile lastVibratoProfile = default;
                bool hasLastVibratoProfile = false;

                for (int vibratoIndex = 0; vibratoIndex < vibratoSlotCount && slotIndex + vibratoIndex < view.techniqueSegmentRibbons.Length; vibratoIndex++)
                {
                    if (!TryBuildVibratoSubRibbonProfile(
                            view,
                            segment,
                            vibratoStartX,
                            vibratoEndX,
                            vibratoBaseStartY,
                            vibratoBaseEndY,
                            vibratoStartAttachZ,
                            vibratoTotalDepth,
                            vibratoIndex,
                            vibratoSlotCount,
                            out TechniqueRibbonProfile vibratoProfile))
                    {
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex + vibratoIndex], false);
                        continue;
                    }

                    if (vibratoIndex == 0 &&
                        hasPreviousProfile &&
                        Mathf.Abs(segment.startOffset - previousEndOffset) <= 0.0001f)
                    {
                        ApplyRibbonJoinOverlap(previousProfile, ref vibratoProfile);
                    }
                    else if (vibratoIndex > 0 && hasLastVibratoProfile)
                    {
                        ApplyRibbonJoinOverlap(lastVibratoProfile, ref vibratoProfile);
                    }

                    float vibratoDepth = Mathf.Max(0.01f, vibratoProfile.end.z - vibratoProfile.start.z);
                    float vibratoVisibleStart01 = Mathf.Clamp01((visibleDistance - cumulativeDepth) / vibratoDepth);
                    if (vibratoVisibleStart01 < 0.999f)
                    {
                        ApplyTechniqueSegmentRibbon(view, slotIndex + vibratoIndex, segment.type, vibratoProfile, state.IsResolved, vibratoVisibleStart01);
                        anyVisible = true;
                    }
                    else
                    {
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex + vibratoIndex], false);
                    }

                    cumulativeDepth += vibratoDepth;
                    lastVibratoProfile = vibratoProfile;
                    hasLastVibratoProfile = true;
                }

                previousEndOffset = segment.endOffset;
                if (hasLastVibratoProfile)
                {
                    previousProfile = lastVibratoProfile;
                    hasPreviousProfile = true;
                }
                slotIndex += vibratoSlotCount;
                continue;
            }

            if (!TryBuildTechniqueSegmentRibbonProfile(view, state, z, songTime, segment, connectsToNextSegment, out TechniqueRibbonProfile profile))
            {
                if (slotIndex < view.techniqueSegmentRibbons.Length)
                    SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex], false);
                slotIndex++;
                continue;
            }

            if (hasPreviousProfile && Mathf.Abs(segment.startOffset - previousEndOffset) <= 0.0001f)
                ApplyRibbonJoinOverlap(previousProfile, ref profile);

            ApplyTechniqueSegmentRibbon(view, slotIndex, segment.type, profile, state.IsResolved, segmentVisibleStart01);
            anyVisible = true;
            previousProfile = profile;
            previousEndOffset = segment.endOffset;
            hasPreviousProfile = true;
            slotIndex++;
        }

        for (int i = slotIndex; i < view.techniqueSegmentRibbons.Length; i++)
            SetGameObjectActive(view.techniqueSegmentRibbons[i], false);

        return anyVisible;
    }

    private bool TryBuildTechniqueSegmentRibbonProfile(
        HighwayNoteView view,
        GameplayNoteState state,
        float noteCenterZ,
        float songTime,
        NoteTechniqueSegmentData segment,
        bool connectsToNextSegment,
        out TechniqueRibbonProfile profile)
    {
        switch (segment.type)
        {
            case NoteTechniqueSegmentType.Slide:
                return TryBuildSlideSegmentRibbonProfile(view, state, noteCenterZ, songTime, segment, connectsToNextSegment, out profile);
            case NoteTechniqueSegmentType.Bend:
                return TryBuildBendSegmentRibbonProfile(view, state, noteCenterZ, songTime, segment, out profile);
            case NoteTechniqueSegmentType.Sustain:
                return TryBuildFlatSegmentRibbonProfile(view, state, noteCenterZ, songTime, segment, out profile);
            case NoteTechniqueSegmentType.Vibrato:
                if (!TryBuildVibratoSegmentMetrics(
                        view,
                        state,
                        noteCenterZ,
                        songTime,
                        segment,
                        out float vibratoStartX,
                        out float vibratoEndX,
                        out float vibratoBaseStartY,
                        out float vibratoBaseEndY,
                        out float vibratoStartAttachZ,
                        out float vibratoTotalDepth))
                {
                    profile = default;
                    return false;
                }

                return TryBuildVibratoSubRibbonProfile(
                    view,
                    segment,
                    vibratoStartX,
                    vibratoEndX,
                    vibratoBaseStartY,
                    vibratoBaseEndY,
                    vibratoStartAttachZ,
                    vibratoTotalDepth,
                    0,
                    GetVibratoSubSegmentCount(segment),
                    out profile);
            default:
                profile = default;
                return false;
        }
    }

    private bool TryBuildSlideSegmentRibbonProfile(
        HighwayNoteView view,
        GameplayNoteState state,
        float noteCenterZ,
        float songTime,
        NoteTechniqueSegmentData segment,
        bool connectsToNextSegment,
        out TechniqueRibbonProfile profile)
    {
        profile = default;

        if (segment.startFret <= 0)
            return false;

        float startDepth = Mathf.Max(0.1f, view.baseScale.z);
        float segmentStartTime = state.data.time + segment.startOffset;
        float segmentEndTime = state.data.time + segment.endOffset;
        float startTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentStartTime - songTime) * currentVisualNoteSpeed));
        float endTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentEndTime - songTime) * currentVisualNoteSpeed));
        float startAttachZ = startTravelZ + (startDepth * 0.5f);
        float endAttachZ = connectsToNextSegment
            ? endTravelZ + (startDepth * 0.5f)
            : endTravelZ - (startDepth * 0.95f);
        if (endAttachZ <= startAttachZ + 0.05f)
            endAttachZ = startAttachZ + 0.05f;

        float startX = GetNoteX(segment.startFret);
        float endX = GetNoteX(segment.endFret);
        float startY = GetSegmentBendVisualY(state.data.stringIdx, segment.startBend);
        float endY = GetSegmentBendVisualY(state.data.stringIdx, segment.endBend);

        Vector3 start = new Vector3(startX, startY, startAttachZ);
        Vector3 end = new Vector3(endX, endY, endAttachZ);
        float length = Vector3.Distance(start, end);
        if (length <= 0.05f)
            return false;

        float leadDistance = Mathf.Clamp(Mathf.Abs(endX - startX) * 0.55f + Mathf.Abs(endAttachZ - startAttachZ) * 0.16f, 0.35f, 2.2f);
        profile.start = start;
        profile.control1 = start + new Vector3(0f, 0f, leadDistance);
        profile.control2 = end - new Vector3(0f, 0f, leadDistance);
        profile.end = end;
        profile.halfWidth = Mathf.Max(0.18f, view.baseScale.x * 0.38f);
        profile.pathMode = 0f;
        profile.cornerRoundness = 0f;
        profile.darkBandStart01 = 1f;
        profile.darkBandEnd01 = 1f;
        return true;
    }

    private bool TryBuildFlatSegmentRibbonProfile(
        HighwayNoteView view,
        GameplayNoteState state,
        float noteCenterZ,
        float songTime,
        NoteTechniqueSegmentData segment,
        out TechniqueRibbonProfile profile)
    {
        profile = default;

        float startDepth = Mathf.Max(0.1f, view.baseScale.z);
        float segmentStartTime = state.data.time + segment.startOffset;
        float segmentEndTime = state.data.time + segment.endOffset;
        float startTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentStartTime - songTime) * currentVisualNoteSpeed));
        float endTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentEndTime - songTime) * currentVisualNoteSpeed));
        float startAttachZ = startTravelZ + (startDepth * 0.5f);
        float endAttachZ = Mathf.Max(startAttachZ + 0.35f, endTravelZ);
        float totalDepth = Mathf.Max(0.35f, endAttachZ - startAttachZ);
        float firstControlZ = startAttachZ + (totalDepth / 3f);
        float secondControlZ = startAttachZ + ((totalDepth * 2f) / 3f);
        float x = GetNoteX(segment.endFret);
        float y = GetSegmentBendVisualY(state.data.stringIdx, segment.endBend);

        profile.start = new Vector3(x, y, startAttachZ);
        profile.control1 = new Vector3(x, y, firstControlZ);
        profile.control2 = new Vector3(x, y, secondControlZ);
        profile.end = new Vector3(x, y, startAttachZ + totalDepth);
        profile.halfWidth = Mathf.Max(0.16f, view.baseScale.x * 0.30f);
        profile.pathMode = 0f;
        profile.cornerRoundness = 0f;
        profile.darkBandStart01 = 1f;
        profile.darkBandEnd01 = 1f;
        return true;
    }

    private bool TryBuildBendSegmentRibbonProfile(
        HighwayNoteView view,
        GameplayNoteState state,
        float noteCenterZ,
        float songTime,
        NoteTechniqueSegmentData segment,
        out TechniqueRibbonProfile profile)
    {
        return TryBuildBendSegmentRibbonProfiles(
            view,
            state,
            noteCenterZ,
            songTime,
            segment,
            out profile,
            out _,
            out _,
            out _);
    }

    private bool TryBuildBendSegmentRibbonProfiles(
        HighwayNoteView view,
        GameplayNoteState state,
        float noteCenterZ,
        float songTime,
        NoteTechniqueSegmentData segment,
        out TechniqueRibbonProfile headProfile,
        out bool hasSustainTail,
        out TechniqueRibbonProfile sustainTailProfile,
        out float totalDisplayedDepth)
    {
        headProfile = default;
        sustainTailProfile = default;
        hasSustainTail = false;
        totalDisplayedDepth = 0f;

        float startDepth = Mathf.Max(0.1f, view.baseScale.z);
        float segmentStartTime = state.data.time + segment.startOffset;
        float segmentEndTime = state.data.time + segment.endOffset;
        float startTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentStartTime - songTime) * currentVisualNoteSpeed));
        float fullEndTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentEndTime - songTime) * currentVisualNoteSpeed));
        float startAttachZ = startTravelZ + (startDepth * 0.5f);
        float startX = GetNoteX(segment.startFret);
        float endX = GetNoteX(segment.endFret);
        float startY = GetSegmentBendVisualY(state.data.stringIdx, segment.startBend);
        float targetY = GetSegmentBendVisualY(state.data.stringIdx, segment.endBend);

        float minimumVisualDepth = BendRibbonLeadOutDistance + BendRibbonCornerDepth + BendRibbonMinimumTopHoldDistance;
        float fullEndAttachZ = Mathf.Max(startAttachZ + minimumVisualDepth, Mathf.Max(startAttachZ + 0.4f, fullEndTravelZ));
        float totalDepth = Mathf.Max(minimumVisualDepth, fullEndAttachZ - startAttachZ);
        float leadOutZ = Mathf.Clamp(BendRibbonLeadOutDistance, 0.12f, totalDepth - 0.16f);
        float riseDepth = Mathf.Clamp(BendRibbonCornerDepth, 0.03f, totalDepth - leadOutZ - 0.12f);
        float topHoldLength = Mathf.Max(BendRibbonMinimumTopHoldDistance, totalDepth - leadOutZ - riseDepth);
        float maxHeadTopHoldLength = Mathf.Max(BendRibbonMinimumTopHoldDistance, currentVisualNoteSpeed * BendRibbonHeadMaximumFlatHoldSeconds);
        float headTopHoldLength = Mathf.Min(topHoldLength, maxHeadTopHoldLength);
        float curveEntryZ = startAttachZ + leadOutZ;
        float curvePeakZ = curveEntryZ + riseDepth;
        float headEndAttachZ = curvePeakZ + headTopHoldLength;

        headProfile.start = new Vector3(startX, startY, startAttachZ);
        headProfile.control1 = new Vector3(startX, startY, curveEntryZ);
        headProfile.control2 = new Vector3(endX, targetY, curvePeakZ);
        headProfile.end = new Vector3(endX, targetY, headEndAttachZ);
        headProfile.halfWidth = Mathf.Max(0.16f, view.baseScale.x * 0.34f);
        headProfile.pathMode = 1f;
        headProfile.cornerRoundness = Mathf.Max(0f, BendRibbonCornerRoundness);
        float totalSpan = Mathf.Max(0.01f, headEndAttachZ - startAttachZ);
        float darkBandPadding = Mathf.Clamp(BendRibbonDarkBandPaddingDistance, 0.04f, totalSpan * 0.35f);
        headProfile.darkBandStart01 = Mathf.Clamp01(((curveEntryZ - darkBandPadding) - startAttachZ) / totalSpan);
        headProfile.darkBandEnd01 = Mathf.Clamp01(((curvePeakZ + darkBandPadding) - startAttachZ) / totalSpan);

        if (fullEndAttachZ > headEndAttachZ + 0.05f)
        {
            hasSustainTail = true;
            float headFlatTopLength = Mathf.Max(0.01f, headEndAttachZ - curvePeakZ);
            float initialTailStartZ = headEndAttachZ;
            float initialTailDepth = Mathf.Max(0.01f, fullEndAttachZ - initialTailStartZ);
            TechniqueRibbonProfile initialTailProfile = default;
            initialTailProfile.start = new Vector3(endX, targetY, initialTailStartZ);
            initialTailProfile.control1 = new Vector3(endX, targetY, initialTailStartZ + (initialTailDepth / 3f));
            initialTailProfile.control2 = new Vector3(endX, targetY, initialTailStartZ + ((initialTailDepth * 2f) / 3f));
            initialTailProfile.end = new Vector3(endX, targetY, fullEndAttachZ);

            float joinOverlap = Mathf.Min(
                headFlatTopLength,
                GetRibbonLengthFadeWorldDistance(headProfile) + GetRibbonLengthFadeWorldDistance(initialTailProfile));
            float tailStartZ = headEndAttachZ - joinOverlap;
            float tailEndZ = fullEndAttachZ;
            float tailDepth = Mathf.Max(0.01f, tailEndZ - tailStartZ);
            float firstControlZ = tailStartZ + (tailDepth / 3f);
            float secondControlZ = tailStartZ + ((tailDepth * 2f) / 3f);

            sustainTailProfile.start = new Vector3(endX, targetY, tailStartZ);
            sustainTailProfile.control1 = new Vector3(endX, targetY, firstControlZ);
            sustainTailProfile.control2 = new Vector3(endX, targetY, secondControlZ);
            sustainTailProfile.end = new Vector3(endX, targetY, tailEndZ);
            sustainTailProfile.halfWidth = headProfile.halfWidth;
            sustainTailProfile.pathMode = 0f;
            sustainTailProfile.cornerRoundness = 0f;
            sustainTailProfile.darkBandStart01 = 1f;
            sustainTailProfile.darkBandEnd01 = 1f;
        }

        totalDisplayedDepth = Mathf.Max(0.01f, fullEndAttachZ - startAttachZ);
        return true;
    }

    private bool TryBuildVibratoSegmentMetrics(
        HighwayNoteView view,
        GameplayNoteState state,
        float noteCenterZ,
        float songTime,
        NoteTechniqueSegmentData segment,
        out float startX,
        out float endX,
        out float baseStartY,
        out float baseEndY,
        out float startAttachZ,
        out float totalDisplayedDepth)
    {
        startX = 0f;
        endX = 0f;
        baseStartY = 0f;
        baseEndY = 0f;
        startAttachZ = 0f;
        totalDisplayedDepth = 0f;

        float startDepth = Mathf.Max(0.1f, view.baseScale.z);
        float segmentStartTime = state.data.time + segment.startOffset;
        float segmentEndTime = state.data.time + segment.endOffset;
        float startTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentStartTime - songTime) * currentVisualNoteSpeed));
        float endTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentEndTime - songTime) * currentVisualNoteSpeed));
        startAttachZ = startTravelZ + (startDepth * 0.5f);
        float endAttachZ = Mathf.Max(startAttachZ + 0.35f, endTravelZ);
        totalDisplayedDepth = Mathf.Max(0.35f, endAttachZ - startAttachZ);
        if (totalDisplayedDepth <= 0.05f)
            return false;

        startX = GetNoteX(segment.startFret);
        endX = GetNoteX(segment.endFret);
        baseStartY = GetSegmentBendVisualY(state.data.stringIdx, segment.startBend);
        baseEndY = GetSegmentBendVisualY(state.data.stringIdx, segment.endBend);
        return true;
    }

    private bool TryBuildVibratoSubRibbonProfile(
        HighwayNoteView view,
        NoteTechniqueSegmentData segment,
        float startX,
        float endX,
        float baseStartY,
        float baseEndY,
        float startAttachZ,
        float totalDisplayedDepth,
        int vibratoIndex,
        int vibratoSlotCount,
        out TechniqueRibbonProfile profile)
    {
        profile = default;

        if (vibratoSlotCount <= 0 || vibratoIndex < 0 || vibratoIndex >= vibratoSlotCount)
            return false;

        float t0 = vibratoIndex / (float)vibratoSlotCount;
        float t1 = (vibratoIndex + 1) / (float)vibratoSlotCount;
        float cycles = vibratoSlotCount * 0.5f;
        float omega = cycles * Mathf.PI * 2f;
        float amplitude = GetStringLaneSpacing() * VibratoRibbonAmplitudeInStrings;
        float baselineSlopeY = baseEndY - baseStartY;
        float subTSpan = t1 - t0;

        Vector3 p0 = EvaluateVibratoPoint(startX, endX, baseStartY, baseEndY, startAttachZ, totalDisplayedDepth, amplitude, omega, t0);
        Vector3 p3 = EvaluateVibratoPoint(startX, endX, baseStartY, baseEndY, startAttachZ, totalDisplayedDepth, amplitude, omega, t1);
        Vector3 d0 = EvaluateVibratoDerivative(startX, endX, baselineSlopeY, totalDisplayedDepth, amplitude, omega, t0) * subTSpan;
        Vector3 d1 = EvaluateVibratoDerivative(startX, endX, baselineSlopeY, totalDisplayedDepth, amplitude, omega, t1) * subTSpan;

        profile.start = p0;
        profile.control1 = p0 + (d0 / 3f);
        profile.control2 = p3 - (d1 / 3f);
        profile.end = p3;
        profile.halfWidth = Mathf.Max(0.16f, view.baseScale.x * 0.30f);
        profile.pathMode = 0f;
        profile.cornerRoundness = 0f;
        profile.darkBandStart01 = 1f;
        profile.darkBandEnd01 = 1f;
        return profile.end.z > profile.start.z + 0.01f;
    }

    private static Vector3 EvaluateVibratoPoint(
        float startX,
        float endX,
        float baseStartY,
        float baseEndY,
        float startAttachZ,
        float totalDisplayedDepth,
        float amplitude,
        float omega,
        float t)
    {
        float x = Mathf.Lerp(startX, endX, t);
        float baselineY = Mathf.Lerp(baseStartY, baseEndY, t);
        float y = baselineY + (Mathf.Sin(t * omega) * amplitude);
        float z = startAttachZ + (totalDisplayedDepth * t);
        return new Vector3(x, y, z);
    }

    private static Vector3 EvaluateVibratoDerivative(
        float startX,
        float endX,
        float baselineSlopeY,
        float totalDisplayedDepth,
        float amplitude,
        float omega,
        float t)
    {
        float dx = endX - startX;
        float dy = baselineSlopeY + (Mathf.Cos(t * omega) * amplitude * omega);
        float dz = totalDisplayedDepth;
        return new Vector3(dx, dy, dz);
    }

    private void ApplyRibbonJoinOverlap(TechniqueRibbonProfile previousProfile, ref TechniqueRibbonProfile currentProfile)
    {
        float overlap = GetRibbonLengthFadeWorldDistance(previousProfile) + GetRibbonLengthFadeWorldDistance(currentProfile);
        Vector3 direction = currentProfile.control1 - currentProfile.start;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = currentProfile.end - currentProfile.start;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        direction.Normalize();
        Vector3 offset = direction * Mathf.Min(overlap, Vector3.Distance(currentProfile.start, currentProfile.end) * 0.45f);
        currentProfile.start -= offset;
        currentProfile.control1 -= offset;
    }

    private float GetSegmentBendVisualY(int stringIdx, float bendValue)
    {
        float baseY = GetStringY(stringIdx);
        if (Mathf.Abs(bendValue) <= 0.01f)
            return baseY;

        return baseY + (GetStringLaneSpacing() * BendRibbonVisualHeightInStrings);
    }

    private void ApplyTechniqueSegmentRibbon(
        HighwayNoteView view,
        int slotIndex,
        NoteTechniqueSegmentType segmentType,
        TechniqueRibbonProfile profile,
        bool isResolved,
        float visibleStart01)
    {
        if (view.techniqueSegmentRibbons == null ||
            slotIndex < 0 ||
            slotIndex >= view.techniqueSegmentRibbons.Length)
            return;

        float flatLightStrength = segmentType == NoteTechniqueSegmentType.Bend ? BendRibbonFlatLightStrength : 0f;

        Color centerColor;
        Color edgeColor;
        Color emissionColor;
        if (segmentType == NoteTechniqueSegmentType.Slide)
        {
            Color centerBaseColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.04f : 0.08f);
            centerColor = new Color(centerBaseColor.r, centerBaseColor.g, centerBaseColor.b, isResolved ? 0.28f : 0.58f);
            edgeColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.34f : 0.64f);
            edgeColor.a = isResolved ? 0.46f : 0.98f;
            emissionColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.34f : 0.82f) * Mathf.Pow(2f, isResolved ? 0.40f : 1.32f);
        }
        else if (segmentType == NoteTechniqueSegmentType.Bend)
        {
            Color centerBaseColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.10f : 0.16f);
            centerColor = new Color(centerBaseColor.r, centerBaseColor.g, centerBaseColor.b, isResolved ? 0.34f : 0.70f);
            edgeColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.42f : 0.70f);
            edgeColor.a = isResolved ? 0.50f : 1.0f;
            emissionColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.44f : 0.90f) * Mathf.Pow(2f, isResolved ? 0.46f : 1.38f);
        }
        else
        {
            Color centerBaseColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.06f : 0.10f);
            centerColor = new Color(centerBaseColor.r, centerBaseColor.g, centerBaseColor.b, isResolved ? 0.26f : 0.54f);
            edgeColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.26f : 0.52f);
            edgeColor.a = isResolved ? 0.40f : 0.88f;
            emissionColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.26f : 0.62f) * Mathf.Pow(2f, isResolved ? 0.34f : 1.0f);
        }

        ApplyTechniqueRibbon(
            view.techniqueRoot.transform,
            view.techniqueSegmentRibbons[slotIndex],
            view.techniqueSegmentRibbonRenderers[slotIndex],
            view.techniqueSegmentRibbonPropertyBlocks[slotIndex],
            profile,
            centerColor,
            edgeColor,
            emissionColor,
            visibleStart01,
            flatLightStrength);
    }

    private void UpdateChordFrames(GuitarGameplaySnapshot snapshot)
    {
        float renderSongTime = GetRenderSongTime(snapshot);
        activeChordIdsThisFrame.Clear();

        foreach (var pair in chordGroups)
        {
            List<NoteData> group = pair.Value;
            if (group == null || group.Count < 2)
                continue;

            float anchorTime = group[0].time;
            float z = owner.StrikeLineZ + ((anchorTime - renderSongTime) * currentVisualNoteSpeed);
            bool anyRecent = group.Any(n => TryGetState(snapshot.noteStates, n.id, out GameplayNoteState state) && state.IsResolved && renderSongTime - state.resolvedAt <= GetResolvedFadeTime());
            bool visible = z <= owner.SpawnZ && z > owner.StrikeLineZ + 0.001f;

            if (!visible && !anyRecent)
                continue;

            activeChordIdsThisFrame.Add(pair.Key);

            if (!chordFrames.TryGetValue(pair.Key, out GameObject frame) || frame == null)
            {
                int handFret = GetGroupHandFret(group);
                float leftX = GetHandWindowStartX(handFret);
                float rightX = GetHandWindowEndX(handFret, group);
                frame = CreateChordFrame(leftX, rightX, GetChordBoxCenterY(group), GetChordBoxHeight(group));
                chordFrames[pair.Key] = frame;
            }

            frame.transform.position = new Vector3(frame.transform.position.x, frame.transform.position.y, z + 0.01f);
        }

        chordFrameRemovalBuffer.Clear();
        foreach (KeyValuePair<int, GameObject> pair in chordFrames)
        {
            if (activeChordIdsThisFrame.Contains(pair.Key))
                continue;

            chordFrameRemovalBuffer.Add(pair.Key);
        }

        for (int i = 0; i < chordFrameRemovalBuffer.Count; i++)
        {
            int key = chordFrameRemovalBuffer[i];
            if (chordFrames[key] != null)
                Object.Destroy(chordFrames[key]);
            chordFrames.Remove(key);
        }
    }

    private void UpdateFretboardLights(HashSet<int> pitchesToLight)
    {
        if (fretLightMats == null || fretLightRenderers == null)
            return;

        int fretLightColumns = GetFretLightColumnCount();

        for (int s = 0; s < 6; s++)
        {
            for (int f = 0; f < fretLightColumns; f++)
            {
                if (fretLightRenderers[s, f] != null)
                {
                    float xPos = f == 0 ? GetNoteX(Mathf.RoundToInt(owner.defaultOpenAnchorFret)) : GetNoteX(f);
                    Vector3 position = fretLightRenderers[s, f].transform.position;
                    position.x = xPos;
                    position.y = GetStringY(s);
                    position.z = owner.StrikeLineZ;
                    fretLightRenderers[s, f].transform.position = position;
                }

                fretLightMats[s, f].SetColor("_EmissionColor", Color.black);
                if (fretLightRenderers[s, f] != null)
                    fretLightRenderers[s, f].enabled = false;
            }
        }

        if (pitchesToLight == null)
            return;

        foreach (int pitch in pitchesToLight)
        {
            for (int s = 0; s < 6; s++)
            {
                for (int f = 0; f < fretLightColumns; f++)
                {
                    int exactFretPitch = owner.GetStringBasePitch(s) + f;
                    int genericFretPitch = exactFretPitch % 12;
                    if (exactFretPitch == pitch || (pitch < 12 && genericFretPitch == pitch))
                    {
                        fretLightMats[s, f].SetColor("_EmissionColor", owner.GetStringColor(s) * 8f);
                        if (fretLightRenderers[s, f] != null)
                            fretLightRenderers[s, f].enabled = true;
                    }
                }
            }
        }
    }

    private void UpdateSectionCamera(GuitarGameplaySnapshot snapshot)
    {
        float renderSongTime = GetRenderSongTime(snapshot);
        float previewWindow = Mathf.Max(1.6f, owner.lookaheadWindow * 1.75f);
        float weightedCenterSum = 0f;
        float weightSum = 0f;
        float requiredMin = 0f;
        float requiredMax = 0f;
        bool foundFraming = false;

        for (int i = 0; i < snapshot.noteStates.Count; i++)
        {
            GameplayNoteState state = snapshot.noteStates[i];
            if (state == null || state.IsResolved)
                continue;

            float timeUntilNote = state.data.time - renderSongTime;
            if (timeUntilNote < -0.1f || timeUntilNote > previewWindow)
                continue;

            GetFramingRange(state.data, out float minX, out float maxX);
            float noteCenter = (minX + maxX) * 0.5f;
            float noteWeight = Mathf.Lerp(1.15f, 0.75f, Mathf.Clamp01(timeUntilNote / previewWindow));

            weightedCenterSum += noteCenter * noteWeight;
            weightSum += noteWeight;

            if (!foundFraming)
            {
                requiredMin = minX;
                requiredMax = maxX;
                foundFraming = true;
            }
            else
            {
                requiredMin = Mathf.Min(requiredMin, minX);
                requiredMax = Mathf.Max(requiredMax, maxX);
            }
        }

        if (foundFraming && weightSum > 0.0001f)
        {
            float desiredTargetX = weightedCenterSum / weightSum;
            float horizontalPadding = Mathf.Max(owner.FretSpacing * 0.8f, 0.8f);
            float halfSpan = Mathf.Max(
                desiredTargetX - requiredMin,
                requiredMax - desiredTargetX) + horizontalPadding;
            float desiredSpread = (halfSpan * 2f) / Mathf.Max(0.01f, owner.FretSpacing);
            float desiredFov = Mathf.Clamp(50f + (desiredSpread * 3.0f), 50f, 90f);

            float targetBlend = 1f - Mathf.Exp(-Time.deltaTime * 1.35f);
            cameraTargetX = Mathf.Lerp(cameraTargetX, desiredTargetX, targetBlend);
            cameraTargetFOV = Mathf.Lerp(cameraTargetFOV, desiredFov, targetBlend * 0.75f);
        }

        float smoothedX = Mathf.SmoothDamp(mainCamera.transform.position.x, cameraTargetX, ref cameraXVelocity, 0.46f, Mathf.Infinity, Time.deltaTime);
        mainCamera.transform.position = new Vector3(smoothedX, owner.highwayCameraY, owner.highwayCameraZ);
        mainCamera.fieldOfView = Mathf.SmoothDamp(mainCamera.fieldOfView, cameraTargetFOV, ref cameraFovVelocity, 0.58f, Mathf.Infinity, Time.deltaTime);
    }

    private float GetRenderSongTime(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null)
            return 0f;

        float renderSongTime = snapshot.songTime;
        float visibleWindow = GetVisibleLeadTime();

        if (snapshot.noteStates == null || snapshot.noteStates.Count == 0)
            return renderSongTime;

        bool shouldPreviewUpcoming = snapshot.showMainMenu || snapshot.showSongSelection || snapshot.showTrackSelection;
        if (!shouldPreviewUpcoming)
            return renderSongTime;

        bool hasVisiblePendingNote = snapshot.noteStates.Any(state =>
            state != null &&
            !state.IsResolved &&
            state.data.time >= renderSongTime &&
            state.data.time <= renderSongTime + visibleWindow);

        if (hasVisiblePendingNote)
            return renderSongTime;

        GameplayNoteState nextPending = snapshot.noteStates
            .Where(state => state != null && !state.IsResolved && state.data.time >= renderSongTime)
            .OrderBy(state => state.data.time)
            .FirstOrDefault();

        if (nextPending == null)
            return renderSongTime;

        float previewRenderTime = Mathf.Max(0f, nextPending.data.time - (visibleWindow * 0.85f));
        return previewRenderTime;
    }

    private float GetVisibleLeadTime()
    {
        return Mathf.Max(0.01f, (owner.SpawnZ - owner.StrikeLineZ) / Mathf.Max(0.01f, currentVisualNoteSpeed));
    }

    private float GetVisualNoteSpeed(GuitarGameplaySnapshot snapshot)
    {
        float spacingScale = 1f;
        if (snapshot != null)
            spacingScale = Mathf.Clamp(snapshot.tabSpeedOffsetPercent / 100f, 0.5f, 1.5f);

        return Mathf.Max(0.01f, owner.noteSpeed * spacingScale);
    }

    private bool TryGetState(List<GameplayNoteState> states, int noteId, out GameplayNoteState state)
    {
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].data.id == noteId)
            {
                state = states[i];
                return true;
            }
        }

        state = null;
        return false;
    }

    private List<NoteData> GetChordGroup(NoteData data)
    {
        if (data.chordId >= 0 && chordGroups.TryGetValue(data.chordId, out List<NoteData> group))
            return group;

        return new List<NoteData> { data };
    }

    private int GetGroupHandFret(List<NoteData> group)
    {
        if (group == null || group.Count == 0)
            return Mathf.Clamp(Mathf.RoundToInt(owner.defaultOpenAnchorFret), 1, owner.TotalFrets - 3);

        List<NoteData> fretted = group.Where(n => n.fret > 0).ToList();
        if (fretted.Count > 0)
            return Mathf.Clamp(fretted.Min(n => n.fret), 1, owner.TotalFrets - 3);

        float groupTime = group[0].time;
        List<NoteData> futureFretted = chartById.Values.Where(n => n.time > groupTime + 0.0001f && n.fret > 0).OrderBy(n => n.time).ToList();
        if (futureFretted.Count > 0)
            return Mathf.Clamp(futureFretted[0].fret, 1, owner.TotalFrets - 3);

        return Mathf.Clamp(Mathf.RoundToInt(owner.defaultOpenAnchorFret), 1, owner.TotalFrets - 3);
    }

    private float GetHandWindowStartX(int handFret)
    {
        return GetNoteX(handFret - 1) - (owner.FretSpacing * 0.2f);
    }

    private float GetHandWindowEndX(int handFret, List<NoteData> group = null)
    {
        int furthestFret = handFret + 3;
        if (group != null)
        {
            int highestGroupFret = group.Where(n => n.fret > 0).Select(n => n.fret).DefaultIfEmpty(furthestFret).Max();
            furthestFret = Mathf.Max(furthestFret, highestGroupFret);
        }

        return GetNoteX(furthestFret) + (owner.FretSpacing * 0.2f);
    }

    private float GetGroupAnchorX(List<NoteData> group)
    {
        int handFret = GetGroupHandFret(group);
        return (GetHandWindowStartX(handFret) + GetHandWindowEndX(handFret, group)) * 0.5f;
    }

    private float GetVisualNoteX(NoteData data)
    {
        List<NoteData> group = GetChordGroup(data);
        if (data.fret == 0)
            return GetGroupAnchorX(group);

        return GetNoteX(data.fret);
    }

    private void GetFramingRange(NoteData data, out float minX, out float maxX)
    {
        List<NoteData> group = GetChordGroup(data);
        bool isGrouped = group.Count > 1;

        if (isGrouped || data.fret == 0)
        {
            int handFret = GetGroupHandFret(group);
            minX = GetHandWindowStartX(handFret);
            maxX = GetHandWindowEndX(handFret, group);
            return;
        }

        float x = GetNoteX(data.fret);
        minX = x;
        maxX = x;
    }

    private float GetChordBoxHeight(List<NoteData> group)
    {
        if (group == null || group.Count == 0)
            return GetStringLaneSpacing();

        float minY = float.MaxValue;
        float maxY = float.MinValue;
        for (int i = 0; i < group.Count; i++)
        {
            float y = GetStringY(group[i].stringIdx);
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }

        return Mathf.Max(1f, (maxY - minY) + owner.chordFrameVerticalPadding);
    }

    private float GetChordBoxCenterY(List<NoteData> group)
    {
        if (group == null || group.Count == 0)
            return 0f;

        float minY = float.MaxValue;
        float maxY = float.MinValue;
        for (int i = 0; i < group.Count; i++)
        {
            float y = GetStringY(group[i].stringIdx);
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }

        return (minY + maxY) * 0.5f;
    }

    private Vector3 GetSingleFrettedNoteScale()
    {
        return new Vector3(
            owner.FretSpacing * 0.56f,
            0.44f * GetNoteHeightScale(),
            Mathf.Max(0.48f, owner.FretSpacing * 0.28f));
    }

    private Vector3 GetGroupedFrettedNoteScale()
    {
        return new Vector3(
            owner.FretSpacing * 0.54f,
            0.4f * GetNoteHeightScale(),
            Mathf.Max(0.44f, owner.FretSpacing * 0.26f));
    }

    private Vector3 GetSingleOpenNoteScale()
    {
        return new Vector3(
            owner.FretSpacing * 3.6f,
            GetScaledOpenHeight(),
            GetScaledOpenDepth());
    }

    private float GetScaledOpenHeight()
    {
        return 0.2f * GetNoteHeightScale();
    }

    private float GetNoteHeightScale()
    {
        return Mathf.Max(0.2f, owner.highwayNoteHeightScale);
    }

    private float GetScaledOpenDepth()
    {
        return Mathf.Max(0.36f, owner.FretSpacing * 0.22f);
    }

    private Vector3 GetMarkerScale()
    {
        float diameter = Mathf.Max(0.38f, owner.FretSpacing * 0.16f);
        return new Vector3(diameter, diameter, Mathf.Max(0.16f, diameter * 0.35f));
    }

    private static float GetVisualNoteStrikeOffset(HighwayNoteView view)
    {
        return Mathf.Max(0f, view.baseScale.z * 0.5f);
    }

    private float GetResolvedFadeTime()
    {
        return Mathf.Max(0.45f, owner.highwayResolvedHoldTime);
    }

    private static void SetGameObjectActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private bool ShouldKeepTechniqueAliveAfterResolution(NoteData data, float songTime)
    {
        if (!HasPersistentTechniqueVisual(data))
            return false;

        return songTime <= GetTechniqueVisualEndTime(data) + 0.02f;
    }

    private bool HasPersistentTechniqueVisual(NoteData data)
    {
        return HasTechniqueSegments(data) || HasBendRibbon(data) || data.slideTargetFret >= 0 || HasNoteSustainRibbon(data);
    }

    private float GetTechniqueVisualEndTime(NoteData data)
    {
        float endTime = data.time;
        if (HasTechniqueSegments(data))
            endTime = Mathf.Max(endTime, data.time + data.techniqueSegments.Max(segment => segment.endOffset));
        if (HasBendRibbon(data))
            endTime = Mathf.Max(endTime, data.time + Mathf.Max(0.14f, data.duration));
        if (HasNoteSustainRibbon(data))
            endTime = Mathf.Max(endTime, data.time + Mathf.Max(GuitarTechniqueVisualThresholds.SustainSeconds, data.duration));

        if (data.slideTargetFret >= 0 &&
            slideDestinationBySourceId.TryGetValue(data.id, out int targetId) &&
            chartById.TryGetValue(targetId, out NoteData slideTarget))
        {
            endTime = Mathf.Max(endTime, slideTarget.time);
        }

        return endTime;
    }

    private GameObject CreateChordFrame(float leftX, float rightX, float centerY, float height)
    {
        GameObject parent = new GameObject("ChordFrame");
        parent.transform.SetParent(gameplayRoot.transform, false);
        float centerX = (leftX + rightX) * 0.5f;
        float width = Mathf.Max(0.5f, rightX - leftX);
        parent.transform.position = new Vector3(centerX, centerY, owner.SpawnZ);

        Material frameMat = owner.CreateSharedGlowMaterial(new Color(0.55f, 0.95f, 1f), 1.6f);
        float halfW = width * 0.5f;
        float halfH = height * 0.5f;

        CreateFramePiece(parent.transform, new Vector3(0f, halfH, 0f), new Vector3(width, owner.chordFrameThickness, 0.08f), frameMat);
        CreateFramePiece(parent.transform, new Vector3(0f, -halfH, 0f), new Vector3(width, owner.chordFrameThickness, 0.08f), frameMat);
        CreateFramePiece(parent.transform, new Vector3(-halfW, 0f, 0f), new Vector3(owner.chordFrameThickness, height, 0.08f), frameMat);
        CreateFramePiece(parent.transform, new Vector3(halfW, 0f, 0f), new Vector3(owner.chordFrameThickness, height, 0.08f), frameMat);
        return parent;
    }

    private void CreateFramePiece(Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = localScale;
        piece.GetComponent<Renderer>().material = material;
    }

    private GameObject CreateNoteOutline(Vector3 noteScale, Color color)
    {
        GameObject outlineRoot = new GameObject("NoteOutline");
        outlineRoot.transform.SetParent(gameplayRoot.transform, false);

        float thickness = Mathf.Max(0.02f, owner.highwayStuckOutlineThickness);
        float depth = Mathf.Max(0.01f, owner.highwayStuckOutlineDepth);
        float width = Mathf.Max(thickness * 2f, noteScale.x);
        float height = Mathf.Max(thickness * 2f, noteScale.y);
        float insetHalfWidth = Mathf.Max(0f, (width - thickness) * 0.5f);
        float insetHalfHeight = Mathf.Max(0f, (height - thickness) * 0.5f);
        Material outlineMat = owner.CreateSharedTransparentMaterial(new Color(color.r, color.g, color.b, 0.38f), 0.12f);
        ConfigureOverlayMaterial(outlineMat, 110, true);
        float outlinePlaneZ = 0f;

        CreateFramePiece(outlineRoot.transform, new Vector3(0f, insetHalfHeight, outlinePlaneZ), new Vector3(width, thickness, depth), outlineMat);
        CreateFramePiece(outlineRoot.transform, new Vector3(0f, -insetHalfHeight, outlinePlaneZ), new Vector3(width, thickness, depth), outlineMat);
        CreateFramePiece(outlineRoot.transform, new Vector3(-insetHalfWidth, 0f, outlinePlaneZ), new Vector3(thickness, height, depth), outlineMat);
        CreateFramePiece(outlineRoot.transform, new Vector3(insetHalfWidth, 0f, outlinePlaneZ), new Vector3(thickness, height, depth), outlineMat);
        return outlineRoot;
    }

    private float GetStuckOutlineCenterZ()
    {
        return owner.StrikeLineZ + (Mathf.Max(0.01f, owner.highwayStuckOutlineDepth) * 0.5f);
    }

    private static void ConfigureOverlayMaterial(Material material, int renderQueueOffset, bool renderOnTop)
    {
        if (material == null)
            return;

        material.renderQueue = (int)RenderQueue.Transparent + renderQueueOffset;
        material.SetInt("_ZWrite", 0);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.SetInt("_ZTest", (int)(renderOnTop ? CompareFunction.Always : CompareFunction.LessEqual));
    }

    private void EnsureTechniqueRibbonResources()
    {
        if (techniqueRibbonMesh == null)
            techniqueRibbonMesh = CreateTechniqueRibbonMesh(28);

        if (sharedTechniqueRibbonMaterial == null)
        {
            Shader shader = Shader.Find("Custom/HighwaySlideRibbon");
            if (shader == null)
                return;

            sharedTechniqueRibbonMaterial = new Material(shader);
            ConfigureOverlayMaterial(sharedTechniqueRibbonMaterial, 100, true);
        }
    }

    private void EnsureBendArrowResources()
    {
        if (sharedBendArrowMaterial != null)
            return;

        Shader shader = Shader.Find("Custom/HighwayNoteArrow");
        if (shader == null)
            return;

        sharedBendArrowMaterial = new Material(shader);
        ConfigureOverlayMaterial(sharedBendArrowMaterial, 145, true);
    }

    private void EnsureMuteSymbolResources()
    {
        if (sharedMuteSymbolMaterial != null)
            return;

        Shader shader = Shader.Find("Custom/HighwayMuteSymbol");
        if (shader == null)
            return;

        sharedMuteSymbolMaterial = new Material(shader);
        ConfigureOverlayMaterial(sharedMuteSymbolMaterial, 146, true);
    }

    private static bool IsMutedNoteVisual(NoteData data)
    {
        if (data.isMuted)
            return true;

        if (data.fret < 0)
            return true;

        string noteName = data.note ?? string.Empty;
        return noteName.Equals("x", System.StringComparison.OrdinalIgnoreCase)
            || noteName.Equals("mute", System.StringComparison.OrdinalIgnoreCase)
            || noteName.Equals("muted", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldShowMuteSymbolForNote(NoteData data)
    {
        return ForceMuteSymbolPreviewOnAllNotes || IsMutedNoteVisual(data);
    }

    private static Mesh CreateTechniqueRibbonMesh(int segments)
    {
        int clampedSegments = Mathf.Max(8, segments);
        int vertexPairs = clampedSegments + 1;
        Vector3[] vertices = new Vector3[vertexPairs * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[clampedSegments * 6];

        for (int i = 0; i < vertexPairs; i++)
        {
            float t = i / (float)clampedSegments;
            int baseIndex = i * 2;
            vertices[baseIndex] = new Vector3(-1f, 0f, t);
            vertices[baseIndex + 1] = new Vector3(1f, 0f, t);
            uvs[baseIndex] = new Vector2(0f, t);
            uvs[baseIndex + 1] = new Vector2(1f, t);

            if (i >= clampedSegments)
                continue;

            int triangleIndex = i * 6;
            triangles[triangleIndex] = baseIndex;
            triangles[triangleIndex + 1] = baseIndex + 2;
            triangles[triangleIndex + 2] = baseIndex + 1;
            triangles[triangleIndex + 3] = baseIndex + 1;
            triangles[triangleIndex + 4] = baseIndex + 2;
            triangles[triangleIndex + 5] = baseIndex + 3;
        }

        Mesh mesh = new Mesh
        {
            name = "HighwayTechniqueRibbonMesh"
        };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(256f, 64f, 256f));
        return mesh;
    }

    private static GameObject CreateTechniqueRibbonObject(string name, Transform parent, Mesh mesh, Material material, out Renderer renderer)
    {
        GameObject ribbon = new GameObject(name);
        ribbon.transform.SetParent(parent, false);
        MeshFilter meshFilter = ribbon.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;
        MeshRenderer meshRenderer = ribbon.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer = meshRenderer;
        return ribbon;
    }

    private Material CreateLaneSurfaceMaterial()
    {
        Shader shader = Shader.Find("Custom/HighwayLaneFloorFade");
        Material mat = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(new Color(0.025f, 0.03f, 0.045f, 0.14f), 0f);

        // Keep lane floors behind strings and overlay effects in both editor and player.
        mat.renderQueue = (int)RenderQueue.Transparent - 40;

        mat.SetColor("_Color", new Color(0.025f, 0.03f, 0.045f, 0.14f));
        mat.SetColor("_BaseColor", new Color(0.025f, 0.03f, 0.045f, 0.14f));
        mat.SetColor("_TintColor", new Color(0.025f, 0.03f, 0.045f, 0.14f));
        if (mat.HasProperty("_EdgeFadeLeft"))
            mat.SetFloat("_EdgeFadeLeft", 0.008f);
        if (mat.HasProperty("_EdgeFadeRight"))
            mat.SetFloat("_EdgeFadeRight", 0.008f);
        if (mat.HasProperty("_FrontBackFade"))
            mat.SetFloat("_FrontBackFade", 0.45f);
        return mat;
    }

    private Material CreateNoteTetherMaterial(Color color)
    {
        Shader shader = Shader.Find("Custom/HighwayNoteTetherFade");
        Material mat = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(new Color(color.r, color.g, color.b, 0.95f), 0f);

        Color tetherColor = new Color(color.r, color.g, color.b, 0.95f);
        mat.SetColor("_Color", tetherColor);
        mat.SetColor("_BaseColor", tetherColor);
        mat.SetColor("_TintColor", tetherColor);
        if (mat.HasProperty("_FadeTop"))
            mat.SetFloat("_FadeTop", 0.5f);
        ConfigureOverlayMaterial(mat, 92, true);
        return mat;
    }

    private Material CreateLaneGuideMaterial()
    {
        Shader shader = Shader.Find("Custom/HighwayLaneGuideFade");
        Material mat = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(new Color(0.12f, 0.26f, 0.55f, 0.85f), 0.15f);

        ConfigureOverlayMaterial(mat, 95, true);
        mat.SetColor("_Color", new Color(0.12f, 0.26f, 0.55f, 0.85f));
        mat.SetColor("_BaseColor", new Color(0.12f, 0.26f, 0.55f, 0.85f));
        mat.SetColor("_TintColor", new Color(0.12f, 0.26f, 0.55f, 0.85f));
        if (mat.HasProperty("_FadeStart"))
            mat.SetFloat("_FadeStart", 0.0f);
        if (mat.HasProperty("_FadeEnd"))
            mat.SetFloat("_FadeEnd", 0.38f);
        return mat;
    }

    private int GetFretLightColumnCount()
    {
        return Mathf.Max(1, owner.TotalFrets + 1);
    }

    private float GetStringY(int stringIdx)
    {
        int row = owner.invertStrings ? (5 - stringIdx) : stringIdx;
        return (row * GetStringLaneSpacing()) + GetStringLaneSpacing();
    }

    private static float GetStringLaneSpacing()
    {
        return StringLaneSpacing;
    }

    private float GetNoteX(int fret)
    {
        if (fret <= 0)
            return -owner.FretSpacing * 0.5f;

        return (fret * owner.FretSpacing) - (owner.FretSpacing * 0.5f);
    }

    private sealed class HighwayNoteView
    {
        public GameObject noteRoot;
        public Renderer noteRenderer;
        public Material noteMaterial;
        public TextMeshPro label;
        public TextMeshPro laneTagLabel;
        public GameObject tail;
        public GameObject tether;
        public Renderer tetherRenderer;
        public Material tetherMaterial;
        public GameObject marker;
        public Renderer markerRenderer;
        public Material markerMaterial;
        public GameObject bendArrow;
        public Renderer bendArrowRenderer;
        public MaterialPropertyBlock bendArrowPropertyBlock;
        public GameObject bendArrowSecondary;
        public Renderer bendArrowSecondaryRenderer;
        public MaterialPropertyBlock bendArrowSecondaryPropertyBlock;
        public GameObject muteSymbol;
        public Renderer muteSymbolRenderer;
        public GameObject outlineRoot;
        public GameObject techniqueRoot;
        public GameObject[] techniqueSegmentRibbons;
        public Renderer[] techniqueSegmentRibbonRenderers;
        public MaterialPropertyBlock[] techniqueSegmentRibbonPropertyBlocks;
        public GameObject slideRibbon;
        public Renderer slideRibbonRenderer;
        public MaterialPropertyBlock slideRibbonPropertyBlock;
        public GameObject legatoCurve;
        public LineRenderer legatoCurveRenderer;
        public SlideRibbonFadeState slideRibbonFadeState;
        public GameObject bendRibbon;
        public Renderer bendRibbonRenderer;
        public MaterialPropertyBlock bendRibbonPropertyBlock;
        public GameObject bendSustainRibbon;
        public Renderer bendSustainRibbonRenderer;
        public MaterialPropertyBlock bendSustainRibbonPropertyBlock;
        public GameObject sustainRibbon;
        public Renderer sustainRibbonRenderer;
        public MaterialPropertyBlock sustainRibbonPropertyBlock;
        public Color baseColor;
        public Vector3 baseScale;

        public void Destroy()
        {
            if (noteRoot != null)
                Object.Destroy(noteRoot);
            if (laneTagLabel != null)
                Object.Destroy(laneTagLabel.gameObject);
            if (tail != null)
                Object.Destroy(tail);
            if (tether != null)
                Object.Destroy(tether);
            if (marker != null)
                Object.Destroy(marker);
            if (bendArrow != null)
                Object.Destroy(bendArrow);
            if (bendArrowSecondary != null)
                Object.Destroy(bendArrowSecondary);
            if (muteSymbol != null)
                Object.Destroy(muteSymbol);
            if (legatoCurve != null)
                Object.Destroy(legatoCurve);
            if (outlineRoot != null)
                Object.Destroy(outlineRoot);
            if (techniqueRoot != null)
                Object.Destroy(techniqueRoot);
        }
    }
}
