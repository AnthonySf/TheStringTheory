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

    private GuitarBridgeServer owner;
    private Camera mainCamera;
    private GameObject root;
    private GameObject gameplayRoot;
    private readonly GameObject[] stringVisuals = new GameObject[6];
    private readonly Material[] stringVisualMats = new Material[6];
    private readonly Renderer[] stringVisualRenderers = new Renderer[6];
    private Material[] fretBoundaryMats;
    private Renderer[] fretBoundaryRenderers;
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
        laneGuideMats = new Material[GetFretLightColumnCount()];
        laneGuideRenderers = new Renderer[GetFretLightColumnCount()];
        GenerateFretboard();
        GenerateStrings();
        GenerateLaneGuides();
        GenerateFretLightGrid();
        gameplayBuilt = true;
    }

    private void InitializeBackgroundEffect(bool menuMode)
    {
        backgroundEffect?.Dispose();
        backgroundEffect = TabsBackgroundFactory.Create(owner, applyHighwayOverrides: !menuMode);
        backgroundUsingMenuMode = menuMode;

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
        if (backgroundEffect == null || menuMode != backgroundUsingMenuMode)
            InitializeBackgroundEffect(menuMode);
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
        GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        float neckWidth = (owner.TotalFrets + 2) * owner.FretSpacing + 10f;
        neck.transform.SetParent(gameplayRoot.transform, false);
        neck.transform.position = new Vector3(neckWidth / 2f - 10f, -2f, 25f);
        neck.transform.localScale = new Vector3(neckWidth, 0.1f, 150f);
        neck.GetComponent<Renderer>().material = owner.CreateSharedGlowMaterial(new Color(0.1f, 0.05f, 0.02f), 0f);

        GameObject nut = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nut.transform.SetParent(gameplayRoot.transform, false);
        nut.transform.position = new Vector3(0f, 3.5f, owner.StrikeLineZ + 0.05f);
        nut.transform.localScale = new Vector3(0.5f, 12f, 0.3f);
        Renderer nutRenderer = nut.GetComponent<Renderer>();
        Material nutMat = owner.CreateSharedGlowMaterial(new Color(0.22f, 0.23f, 0.27f, 1f), 0f);
        nutRenderer.material = nutMat;
        fretBoundaryMats[0] = nutMat;
        fretBoundaryRenderers[0] = nutRenderer;

        for (int fret = 1; fret <= owner.TotalFrets; fret++)
        {
            float wireX = fret * owner.FretSpacing;

            GameObject wire = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wire.transform.SetParent(gameplayRoot.transform, false);
            wire.transform.position = new Vector3(wireX, 3.5f, owner.StrikeLineZ + 0.05f);
            wire.transform.localScale = new Vector3(0.15f, 12f, 0.15f);
            Renderer wireRenderer = wire.GetComponent<Renderer>();
            Material wireMat = owner.CreateSharedGlowMaterial(new Color(0.22f, 0.23f, 0.27f, 1f), 0f);
            wireRenderer.material = wireMat;
            fretBoundaryMats[fret] = wireMat;
            fretBoundaryRenderers[fret] = wireRenderer;

            if (fret % 3 == 0 || fret == 5 || fret == 7 || fret == 9 || fret == 12 || fret == 15)
            {
                GameObject textObj = new GameObject("FretNum_" + fret);
                textObj.transform.SetParent(root.transform, false);
                textObj.transform.position = new Vector3(wireX - (owner.FretSpacing * 0.5f), owner.highwayFretNumberYOffset, owner.StrikeLineZ + owner.highwayFretNumberZOffset);
                textObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                TextMeshPro tm = textObj.AddComponent<TextMeshPro>();
                tm.text = fret.ToString();
                tm.fontSize = 16;
                tm.alignment = TextAlignmentOptions.Center;
                tm.color = new Color(1f, 1f, 1f, 0.5f);
            }
        }

        if (!owner.hideOpenFretNumber)
        {
            GameObject openText = new GameObject("FretNum_0");
            openText.transform.SetParent(root.transform, false);
            openText.transform.position = new Vector3(GetNoteX(Mathf.RoundToInt(owner.defaultOpenAnchorFret)), owner.highwayFretNumberYOffset, owner.StrikeLineZ + owner.highwayFretNumberZOffset);
            openText.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            TextMeshPro tm0 = openText.AddComponent<TextMeshPro>();
            tm0.text = "0";
            tm0.fontSize = 16;
            tm0.alignment = TextAlignmentOptions.Center;
            tm0.color = new Color(1f, 1f, 1f, 0.5f);
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
        float laneSurfaceY = owner.highwayLaneGuideYOffset;
        float depth = 150f;
        float centerZ = owner.StrikeLineZ + (depth * 0.5f);
        const float laneGuideHeight = 0.03f;
        const float laneGuideLift = 0.055f;

        for (int lane = 0; lane < laneCount; lane++)
        {
            GameObject guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guide.name = "LaneGuide_" + lane;
            guide.transform.SetParent(gameplayRoot.transform, false);
            float xPos = lane * owner.FretSpacing;
            guide.transform.position = new Vector3(xPos, laneSurfaceY + laneGuideLift, centerZ);
            guide.transform.localScale = new Vector3(Mathf.Max(0.02f, owner.highwayLaneGuideThickness), laneGuideHeight, depth);

            Color baseColor = new Color(0.18f, 0.45f, 1f, 0.14f);
            Material mat = owner.CreateSharedTransparentMaterial(baseColor, 0.02f);
            ConfigureOverlayMaterial(mat, 60, false);
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

        if (!owner.highwayHighlightFretBoundaries)
        {
            Color disabledColor = new Color(0.20f, 0.22f, 0.25f, 1f);
            for (int i = 0; i < fretBoundaryMats.Length; i++)
            {
                Material mat = fretBoundaryMats[i];
                Renderer renderer = fretBoundaryRenderers[i];
                if (mat == null || renderer == null)
                    continue;

                mat.color = disabledColor;
                mat.SetColor("_Color", disabledColor);
                mat.SetColor("_BaseColor", disabledColor);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                renderer.enabled = true;
            }

            return;
        }

        float renderSongTime = GetRenderSongTime(snapshot);
        bool[] boundaryActive = new bool[fretBoundaryMats.Length];

        if (snapshot.noteStates != null)
        {
            for (int i = 0; i < snapshot.noteStates.Count; i++)
            {
                GameplayNoteState state = snapshot.noteStates[i];
                if (state == null || state.IsResolved)
                    continue;

                float travelZ = owner.StrikeLineZ + ((state.data.time - renderSongTime) * owner.noteSpeed);
                if (travelZ > owner.SpawnZ)
                    continue;

                int fret = Mathf.Clamp(state.data.fret, 0, owner.TotalFrets);
                if (fret <= 0)
                {
                    boundaryActive[0] = true;
                    continue;
                }

                boundaryActive[Mathf.Clamp(fret - 1, 0, boundaryActive.Length - 1)] = true;
                boundaryActive[Mathf.Clamp(fret, 0, boundaryActive.Length - 1)] = true;
            }
        }

        Color activeColor = new Color(0.40f, 0.43f, 0.48f, 1f);
        Color idleColor = new Color(0.20f, 0.22f, 0.25f, 1f);

        for (int i = 0; i < fretBoundaryMats.Length; i++)
        {
            Material mat = fretBoundaryMats[i];
            Renderer renderer = fretBoundaryRenderers[i];
            if (mat == null || renderer == null)
                continue;

            Color color = boundaryActive[i] ? activeColor : idleColor;
            float emission = boundaryActive[i] ? 0.18f : 0f;
            mat.color = color;
            mat.SetColor("_Color", color);
            mat.SetColor("_BaseColor", color);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", boundaryActive[i] ? color * Mathf.Pow(2f, emission) : Color.black);
            renderer.enabled = true;
        }
    }

    private void UpdateLaneGuides(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null || laneGuideMats == null || laneGuideRenderers == null)
            return;

        float renderSongTime = GetRenderSongTime(snapshot);
        bool[] laneHasIncomingNotes = new bool[laneGuideMats.Length];

        if (snapshot.noteStates != null)
        {
            for (int i = 0; i < snapshot.noteStates.Count; i++)
            {
                GameplayNoteState state = snapshot.noteStates[i];
                if (state == null || state.IsResolved)
                    continue;

                float travelZ = owner.StrikeLineZ + ((state.data.time - renderSongTime) * owner.noteSpeed);
                if (travelZ > owner.SpawnZ)
                    continue;

                int fret = Mathf.Clamp(state.data.fret, 0, owner.TotalFrets);
                if (fret <= 0)
                {
                    laneHasIncomingNotes[0] = true;
                    continue;
                }

                laneHasIncomingNotes[Mathf.Clamp(fret - 1, 0, laneHasIncomingNotes.Length - 1)] = true;
                laneHasIncomingNotes[Mathf.Clamp(fret, 0, laneHasIncomingNotes.Length - 1)] = true;
            }
        }

        for (int lane = 0; lane < laneGuideMats.Length; lane++)
        {
            Material mat = laneGuideMats[lane];
            Renderer renderer = laneGuideRenderers[lane];
            if (mat == null || renderer == null)
                continue;

            bool isActive = laneHasIncomingNotes[lane];
            Color laneColor = isActive
                ? new Color(0.30f, 0.62f, 1f, 0.30f)
                : new Color(0.12f, 0.24f, 0.60f, 0.10f);
            float emission = isActive ? 0.75f : 0.05f;

            mat.color = laneColor;
            mat.SetColor("_Color", laneColor);
            mat.SetColor("_BaseColor", laneColor);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.18f, 0.45f, 1f, 1f) * Mathf.Pow(2f, emission));
            renderer.enabled = true;
        }
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
            tail = tail,
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
            bool visible = z <= owner.SpawnZ && z >= owner.StrikeLineZ - (owner.noteSpeed * (owner.hitWindowLate + owner.judgmentGrace) + 1f);

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
        float previewWindow = Mathf.Max(1.1f, owner.lookaheadWindow);
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
            float noteWeight = Mathf.Lerp(1.6f, 0.45f, Mathf.Clamp01(timeUntilNote / previewWindow));

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

            float targetBlend = 1f - Mathf.Exp(-Time.deltaTime * 2.2f);
            cameraTargetX = Mathf.Lerp(cameraTargetX, desiredTargetX, targetBlend);
            cameraTargetFOV = Mathf.Lerp(cameraTargetFOV, desiredFov, targetBlend * 0.9f);
        }

        float smoothedX = Mathf.SmoothDamp(mainCamera.transform.position.x, cameraTargetX, ref cameraXVelocity, 0.28f, Mathf.Infinity, Time.deltaTime);
        mainCamera.transform.position = new Vector3(smoothedX, owner.highwayCameraY, owner.highwayCameraZ);
        mainCamera.fieldOfView = Mathf.SmoothDamp(mainCamera.fieldOfView, cameraTargetFOV, ref cameraFovVelocity, 0.42f, Mathf.Infinity, Time.deltaTime);
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

        int minString = group.Min(n => n.stringIdx);
        int maxString = group.Max(n => n.stringIdx);
        return Mathf.Max(1f, (GetStringY(maxString) - GetStringY(minString)) + owner.chordFrameVerticalPadding);
    }

    private float GetChordBoxCenterY(List<NoteData> group)
    {
        if (group == null || group.Count == 0)
            return 0f;

        int minString = group.Min(n => n.stringIdx);
        int maxString = group.Max(n => n.stringIdx);
        return (GetStringY(minString) + GetStringY(maxString)) * 0.5f;
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
        public GameObject tail;
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
            if (tail != null)
                Object.Destroy(tail);
            if (marker != null)
                Object.Destroy(marker);
            if (outlineRoot != null)
                Object.Destroy(outlineRoot);
            if (techniqueRoot != null)
                Object.Destroy(techniqueRoot);
        }
    }
}
