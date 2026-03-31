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
    private readonly Dictionary<int, GameplayNoteState> noteStatesById = new Dictionary<int, GameplayNoteState>();
    private readonly Dictionary<int, string> noteLaneTagTextById = new Dictionary<int, string>();
    private readonly List<LaneHighlightChunk> laneHighlightChunks = new List<LaneHighlightChunk>();

    private GuitarBridgeServer owner;
    private Camera mainCamera;
    private GameObject root;
    private GameObject gameplayRoot;
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
    private float cameraTargetX;
    private float cameraTargetFOV = 60f;
    private float cameraXVelocity;
    private float cameraFovVelocity;
    private bool gameplayVisualsVisible = true;
    private bool gameplayBuilt;
    private const int BackgroundLayer = 2;
    private string backgroundSignature = string.Empty;

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
        gameplayRoot = new GameObject("Highway3DGameplayRoot");
        gameplayRoot.transform.SetParent(root.transform, false);
        backgroundRoot = new GameObject("Highway3DBackgroundRoot");
        backgroundRoot.transform.SetParent(root.transform, false);
        originalMainCameraClearFlags = mainCamera != null ? mainCamera.clearFlags : CameraClearFlags.SolidColor;
        originalMainCameraCullingMask = mainCamera != null ? mainCamera.cullingMask : -1;

        BuildChartCaches(chartNotes);
        BuildLaneHighlightChunks(chartNotes, sections);
        InitializeBackgroundEffect(menuMode: true);
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

        bool suppressGameplay = snapshot.mainMenuFlowActive;
        EnsureBackgroundMode(suppressGameplay);
        ConfigureCamera();

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
        }

        if (root != null)
            Object.Destroy(root);
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
        noteLaneTagTextById.Clear();

        if (chartNotes == null)
            return;

        for (int i = 0; i < chartNotes.Count; i++)
        {
            NoteData note = chartNotes[i];
            chartById[note.id] = note;

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

        BuildLaneTagNoteMap(chartNotes);
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
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = owner.tabCameraSize;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            if (originalMainCameraCullingMask >= 0)
                mainCamera.cullingMask = originalMainCameraCullingMask | (1 << BackgroundLayer);
            mainCamera.transform.position = new Vector3(0f, 0f, owner.tabCameraZ);
            mainCamera.transform.rotation = Quaternion.identity;
        }
        else
        {
            mainCamera.orthographic = false;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            if (originalMainCameraCullingMask >= 0)
                mainCamera.cullingMask = originalMainCameraCullingMask | (1 << BackgroundLayer);
            mainCamera.farClipPlane = Mathf.Max(mainCamera.farClipPlane, owner.highwayCameraFarClip);
            mainCamera.transform.position = new Vector3(cameraTargetX, owner.highwayCameraY, owner.highwayCameraZ);
            mainCamera.transform.rotation = Quaternion.Euler(owner.highwayCameraPitch, 0f, 0f);
        }

        mainCamera.backgroundColor = owner.tabBackgroundColor;
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
        for (int i = 0; i < 6; i++)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s.name = "String_" + i;
            s.transform.SetParent(gameplayRoot.transform, false);
            s.transform.position = new Vector3(0f, GetStringY(i), owner.StrikeLineZ);
            s.transform.localScale = new Vector3(600f, 0.1f, 0.1f);
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

                float travelZ = owner.StrikeLineZ + ((state.data.time - renderSongTime) * owner.noteSpeed);
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

        ApplyFretNumberLabelStyle(tm, true);
        return tm;
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
        HashSet<int> visibleThisFrame = new HashSet<int>();
        RebuildVisibleNoteStateCache(snapshot);

        for (int i = 0; i < snapshot.noteStates.Count; i++)
        {
            GameplayNoteState state = snapshot.noteStates[i];
            float travelZ = owner.StrikeLineZ + ((state.data.time - renderSongTime) * owner.noteSpeed);
            bool keepForResult = state.IsResolved && renderSongTime - state.resolvedAt <= GetResolvedFadeTime();
            bool visible = travelZ <= owner.SpawnZ && (!state.IsResolved || keepForResult || travelZ >= owner.StrikeLineZ);

            if (!visible)
                continue;

            visibleThisFrame.Add(state.data.id);

            if (!noteViews.TryGetValue(state.data.id, out HighwayNoteView view) || view == null)
            {
                view = CreateNoteView(state.data);
                noteViews[state.data.id] = view;
            }

            float displayZ = Mathf.Max(owner.StrikeLineZ, travelZ);
            UpdateNoteView(view, state, displayZ, renderSongTime);
        }

        foreach (int key in noteViews.Keys.ToList())
        {
            if (visibleThisFrame.Contains(key))
                continue;

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
        if (!isOpen) 
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Marker_" + data.id;
            marker.transform.SetParent(gameplayRoot.transform, false);
            marker.transform.position = new Vector3(xPos, yPos, owner.StrikeLineZ);
            marker.transform.localScale = GetMarkerScale();
            Material markerMat = owner.CreateSharedTransparentMaterial(owner.GetStringColor(data.stringIdx), 1.1f);
            ConfigureOverlayMaterial(markerMat, 130, true);
            marker.GetComponent<Renderer>().material = markerMat;
            marker.SetActive(owner.highwayShowLandingDot);
        }

        GameObject outlineRoot = CreateNoteOutline(cube.transform.localScale, owner.GetStringColor(data.stringIdx));
        outlineRoot.SetActive(false);

        GameObject techniqueRoot = new GameObject("Technique_" + data.id);
        techniqueRoot.transform.SetParent(gameplayRoot.transform, false);

        GameObject slideRibbon = null;
        Renderer slideRibbonRenderer = null;
        if (data.slideTargetFret >= 0)
        {
            slideRibbon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slideRibbon.name = "SlideRibbon_" + data.id;
            slideRibbon.transform.SetParent(techniqueRoot.transform, false);
            slideRibbonRenderer = slideRibbon.GetComponent<Renderer>();
            slideRibbonRenderer.material = owner.CreateSharedTransparentMaterial(new Color(owner.GetStringColor(data.stringIdx).r, owner.GetStringColor(data.stringIdx).g, owner.GetStringColor(data.stringIdx).b, 0.32f), 0.16f);
            ConfigureOverlayMaterial(slideRibbonRenderer.material, 100, true);
            Object.Destroy(slideRibbon.GetComponent<Collider>());

        }

        GameObject bendRibbon = null;
        Renderer bendRibbonRenderer = null;
        if (data.technique == NoteTechnique.Bend || data.bendStep > 0f)
        {
            bendRibbon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bendRibbon.name = "BendRibbon_" + data.id;
            bendRibbon.transform.SetParent(techniqueRoot.transform, false);
            bendRibbonRenderer = bendRibbon.GetComponent<Renderer>();
            bendRibbonRenderer.material = owner.CreateSharedTransparentMaterial(new Color(0.7f, 0.92f, 1f, 0.3f), 0.12f);
            ConfigureOverlayMaterial(bendRibbonRenderer.material, 100, true);
            Object.Destroy(bendRibbon.GetComponent<Collider>());

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
            outlineRoot = outlineRoot,
            techniqueRoot = techniqueRoot,
            slideRibbon = slideRibbon,
            slideRibbonRenderer = slideRibbonRenderer,
            bendRibbon = bendRibbon,
            bendRibbonRenderer = bendRibbonRenderer,
            baseColor = owner.GetStringColor(data.stringIdx),
            baseScale = cube.transform.localScale
        };
    }

    private void UpdateNoteView(HighwayNoteView view, GameplayNoteState state, float z, float songTime)
    {
        if (view.noteRoot == null)
            return;

        float x = GetVisualNoteX(state.data);
        float y = GetStringY(state.data.stringIdx);
        float floorY = GetLaneSurfaceTopY();
        float laneTagY = GetLaneGuideStringY() + 0.06f;
        float laneTagZ = z - 0.08f;

        view.noteRoot.transform.position = new Vector3(x, y, z);
        if (view.marker != null)
            view.marker.transform.position = new Vector3(x, y, owner.StrikeLineZ);
        if (view.outlineRoot != null)
        {
            view.outlineRoot.transform.position = new Vector3(x, y, GetStuckOutlineCenterZ());
            view.outlineRoot.transform.localScale = Vector3.one;
        }

        bool isStuckOnString = !state.IsResolved && z <= owner.StrikeLineZ + 0.001f;
        if (view.noteRenderer != null)
            view.noteRenderer.enabled = !isStuckOnString;
        if (view.outlineRoot != null)
            view.outlineRoot.SetActive(isStuckOnString);

        float tailLength = Mathf.Max(0f, z - owner.StrikeLineZ);
        if (view.tail != null)
        {
            view.tail.transform.position = new Vector3(x, y, owner.StrikeLineZ + (tailLength * 0.5f));
            view.tail.transform.localScale = new Vector3(owner.FretSpacing * 0.06f, 0.06f, tailLength);
            view.tail.SetActive(owner.highwayShowApproachLine && tailLength > 0.01f && !state.IsResolved);
        }

        if (view.tether != null && view.tetherRenderer != null && view.tetherMaterial != null)
        {
            float noteBottomY = y - (view.baseScale.y * 0.5f);
            float tetherTopGap = Mathf.Max(0.18f, view.baseScale.y * 0.7f);
            float tetherTopY = noteBottomY - tetherTopGap;
            float tetherLength = Mathf.Max(0f, tetherTopY - floorY);
            bool showTether = tetherLength > 0.02f && z > owner.StrikeLineZ + 0.001f && !state.IsResolved;
            view.tether.transform.position = new Vector3(x, floorY + (tetherLength * 0.5f), z);
            view.tether.transform.localScale = new Vector3(Mathf.Max(0.04f, owner.FretSpacing * 0.05f), tetherLength, Mathf.Max(0.03f, owner.FretSpacing * 0.04f));
            view.tether.SetActive(showTether);
        }

        if (view.laneTagLabel != null)
        {
            view.laneTagLabel.transform.position = new Vector3(x, laneTagY, laneTagZ);
            ApplyFretNumberLabelStyle(view.laneTagLabel, true);
            view.laneTagLabel.gameObject.SetActive(z > owner.StrikeLineZ + 0.001f && !state.IsResolved);
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
            view.marker.SetActive(owner.highwayShowLandingDot);
            Renderer markerRenderer = view.marker.GetComponent<Renderer>();
            Color markerColor = state.IsHit ? owner.highwayHitColor : (state.IsMissed ? owner.highwayMissColor : view.baseColor);
            markerRenderer.material.color = markerColor;
            markerRenderer.material.SetColor("_EmissionColor", markerColor * (state.IsHit ? 2f : 0.8f));
        }

        UpdateTechniqueView(view, state, z, songTime);
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

    private void UpdateTechniqueView(HighwayNoteView view, GameplayNoteState state, float z, float songTime)
    {
        if (view.techniqueRoot == null)
            return;

        bool showSlide = UpdateSlideTechnique(view, state, z, songTime);
        bool showBend = UpdateBendTechnique(view, state, z, songTime);
        view.techniqueRoot.SetActive(showSlide || showBend);
    }

    private bool UpdateSlideTechnique(HighwayNoteView view, GameplayNoteState state, float z, float songTime)
    {
        if (view.slideRibbon == null || view.slideRibbonRenderer == null)
            return false;

        if (state.data.linkedFromNoteId >= 0)
            return false;

        NoteData anchorData = state.data;
        int targetFret = anchorData.slideTargetFret;
        if (targetFret < 0)
            return false;

        float startX = GetVisualNoteX(anchorData);
        float startY = GetStringY(anchorData.stringIdx);
        float startZ = noteStatesById.TryGetValue(anchorData.id, out GameplayNoteState anchorState)
            ? Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((anchorState.data.time - songTime) * owner.noteSpeed))
            : z;

        NoteData? destinationData = null;
        if (slideDestinationBySourceId.TryGetValue(anchorData.id, out int destinationId) && chartById.TryGetValue(destinationId, out NoteData resolvedDestination))
            destinationData = resolvedDestination;

        float endX = destinationData.HasValue ? GetVisualNoteX(destinationData.Value) : GetNoteX(targetFret);
        float endY = destinationData.HasValue ? GetStringY(destinationData.Value.stringIdx) : startY;
        float endZ;
        if (destinationData.HasValue && noteStatesById.TryGetValue(destinationData.Value.id, out GameplayNoteState destinationState))
        {
            endZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((destinationState.data.time - songTime) * owner.noteSpeed));
        }
        else
        {
            endZ = Mathf.Max(startZ + 0.6f, startZ + Mathf.Abs(endX - startX) * 0.35f);
        }

        Vector3 start = new Vector3(startX, startY, startZ);
        Vector3 end = new Vector3(endX, endY, endZ);
        Vector3 direction = end - start;
        float length = direction.magnitude;
        if (length <= 0.01f)
            return false;

        Vector3 center = (start + end) * 0.5f;
        float thickness = Mathf.Max(0.08f, owner.FretSpacing * 0.1f);
        view.techniqueRoot.transform.position = center;
        view.slideRibbon.transform.position = center;
        view.slideRibbon.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        view.slideRibbon.transform.localScale = new Vector3(thickness, thickness, length);

        Color slideColor = new Color(view.baseColor.r, view.baseColor.g, view.baseColor.b, state.IsResolved ? 0.2f : 0.88f);
        view.slideRibbonRenderer.material.color = slideColor;
        view.slideRibbonRenderer.material.SetColor("_BaseColor", slideColor);
        view.slideRibbonRenderer.material.SetColor("_Color", slideColor);
        view.slideRibbonRenderer.material.EnableKeyword("_EMISSION");
        view.slideRibbonRenderer.material.SetColor("_EmissionColor", view.baseColor * Mathf.Pow(2f, state.IsResolved ? 0.2f : 1.3f));
        view.slideRibbon.SetActive(true);
        return true;
    }

    private bool UpdateBendTechnique(HighwayNoteView view, GameplayNoteState state, float z, float songTime)
    {
        if (view.bendRibbon == null || view.bendRibbonRenderer == null)
            return false;

        float bendAmount = Mathf.Max(0f, state.data.bendStep);
        if (bendAmount <= 0f)
            return false;

        float x = GetVisualNoteX(state.data);
        float y = GetStringY(state.data.stringIdx);
        float height = Mathf.Max(0.35f, bendAmount * 0.75f);
        view.bendRibbon.transform.position = new Vector3(x, y + (height * 0.5f) + 0.18f, z);
        view.bendRibbon.transform.localScale = new Vector3(Mathf.Max(0.12f, owner.FretSpacing * 0.14f), height, Mathf.Max(0.18f, owner.FretSpacing * 0.12f));
        Color bendColor = Color.Lerp(new Color(0.6f, 0.85f, 1f, 0.24f), new Color(1f, 1f, 1f, 0.16f), state.IsResolved ? 1f : 0f);
        view.bendRibbonRenderer.material.color = bendColor;
        view.bendRibbonRenderer.material.SetColor("_BaseColor", bendColor);
        view.bendRibbonRenderer.material.SetColor("_Color", bendColor);
        view.bendRibbon.SetActive(true);

        return true;
    }

    private void UpdateChordFrames(GuitarGameplaySnapshot snapshot)
    {
        float renderSongTime = GetRenderSongTime(snapshot);
        HashSet<int> activeChordIds = new HashSet<int>();

        foreach (var pair in chordGroups)
        {
            List<NoteData> group = pair.Value;
            if (group == null || group.Count < 2)
                continue;

            float anchorTime = group[0].time;
            float z = owner.StrikeLineZ + ((anchorTime - renderSongTime) * owner.noteSpeed);
            bool anyRecent = group.Any(n => TryGetState(snapshot.noteStates, n.id, out GameplayNoteState state) && state.IsResolved && renderSongTime - state.resolvedAt <= GetResolvedFadeTime());
            bool visible = z <= owner.SpawnZ && z > owner.StrikeLineZ + 0.001f;

            if (!visible && !anyRecent)
                continue;

            activeChordIds.Add(pair.Key);

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

        foreach (int key in chordFrames.Keys.ToList())
        {
            if (activeChordIds.Contains(key))
                continue;

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
        return Mathf.Max(0.01f, (owner.SpawnZ - owner.StrikeLineZ) / Mathf.Max(0.01f, owner.noteSpeed));
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
            return 1.2f;

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

    private float GetResolvedFadeTime()
    {
        return Mathf.Max(0.45f, owner.highwayResolvedHoldTime);
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
        return (row * 1.2f) + 1.2f;
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
        public GameObject outlineRoot;
        public GameObject techniqueRoot;
        public GameObject slideRibbon;
        public Renderer slideRibbonRenderer;
        public GameObject bendRibbon;
        public Renderer bendRibbonRenderer;
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
            if (outlineRoot != null)
                Object.Destroy(outlineRoot);
            if (techniqueRoot != null)
                Object.Destroy(techniqueRoot);
        }
    }
}
