using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public sealed class GuitarHighway3DRenderer : IGuitarGameplayRenderer
{
    private readonly Dictionary<int, NoteData> chartById = new Dictionary<int, NoteData>();
    private readonly Dictionary<int, List<NoteData>> chordGroups = new Dictionary<int, List<NoteData>>();
    private readonly Dictionary<int, HighwayNoteView> noteViews = new Dictionary<int, HighwayNoteView>();
    private readonly Dictionary<int, GameObject> chordFrames = new Dictionary<int, GameObject>();

    private GuitarBridgeServer owner;
    private Camera mainCamera;
    private GameObject root;
    private readonly GameObject[] stringVisuals = new GameObject[6];
    private readonly Material[] stringVisualMats = new Material[6];
    private Material[,] fretLightMats;
    private float cameraTargetX;
    private float cameraTargetFOV = 60f;
    private float nextDiagnosticsLogTime;
    private float nextPreviewLogTime;
    private bool hasLoggedMissingCamera;

    public void Initialize(GuitarBridgeServer owner, List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        this.owner = owner;
        mainCamera = Camera.main;
        root = new GameObject("Highway3DRendererRoot");
        nextDiagnosticsLogTime = Time.unscaledTime;
        nextPreviewLogTime = Time.unscaledTime;
        hasLoggedMissingCamera = false;

        BuildChartCaches(chartNotes);
        ConfigureCamera();
        GenerateFretboard();
        GenerateStrings();
        fretLightMats = new Material[6, GetFretLightColumnCount()];
        GenerateFretLightGrid();
        LogInitialization(chartNotes, sections);
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
        {
            if (!hasLoggedMissingCamera)
            {
                Debug.LogWarning("[Highway3D] Render skipped because Camera.main is null.");
                hasLoggedMissingCamera = true;
            }

            return;
        }

        ConfigureCamera();
        UpdateFretboardLights(snapshot.latestDetectedPitches);
        UpdateNotes(snapshot);
        UpdateChordFrames(snapshot);
        UpdateSectionCamera(snapshot);
        LogRenderDiagnostics(snapshot);
    }

    public void DisposeRenderer()
    {
        if (root != null)
            Object.Destroy(root);
    }

    private void BuildChartCaches(List<NoteData> chartNotes)
    {
        chartById.Clear();
        chordGroups.Clear();

        if (chartNotes == null)
            return;

        for (int i = 0; i < chartNotes.Count; i++)
        {
            NoteData note = chartNotes[i];
            chartById[note.id] = note;

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

        mainCamera.orthographic = false;
        mainCamera.transform.position = new Vector3(cameraTargetX, owner.highwayCameraY, owner.highwayCameraZ);
        mainCamera.transform.rotation = Quaternion.Euler(owner.highwayCameraPitch, 0f, 0f);
        mainCamera.backgroundColor = owner.highwayBackgroundColor;
    }

    private void GenerateFretboard()
    {
        GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        float neckWidth = (owner.TotalFrets + 2) * owner.FretSpacing + 10f;
        neck.transform.SetParent(root.transform, false);
        neck.transform.position = new Vector3(neckWidth / 2f - 10f, -2f, 25f);
        neck.transform.localScale = new Vector3(neckWidth, 0.1f, 150f);
        neck.GetComponent<Renderer>().material = owner.CreateSharedGlowMaterial(new Color(0.1f, 0.05f, 0.02f), 0f);

        GameObject nut = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nut.transform.SetParent(root.transform, false);
        nut.transform.position = new Vector3(0f, 3.5f, owner.StrikeLineZ + 0.05f);
        nut.transform.localScale = new Vector3(0.5f, 12f, 0.3f);
        nut.GetComponent<Renderer>().material = owner.CreateSharedGlowMaterial(new Color(0.8f, 0.7f, 0.4f), 0.2f);

        for (int fret = 1; fret <= owner.TotalFrets; fret++)
        {
            float wireX = fret * owner.FretSpacing;

            GameObject wire = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wire.transform.SetParent(root.transform, false);
            wire.transform.position = new Vector3(wireX, 3.5f, owner.StrikeLineZ + 0.05f);
            wire.transform.localScale = new Vector3(0.15f, 12f, 0.15f);
            wire.GetComponent<Renderer>().material = owner.CreateSharedGlowMaterial(Color.gray, 0.3f);

            if (fret % 3 == 0 || fret == 5 || fret == 7 || fret == 9 || fret == 12 || fret == 15)
            {
                GameObject textObj = new GameObject("FretNum_" + fret);
                textObj.transform.SetParent(root.transform, false);
                textObj.transform.position = new Vector3(wireX - (owner.FretSpacing * 0.5f), -1f, owner.StrikeLineZ - 5f);
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
            openText.transform.position = new Vector3(GetNoteX(Mathf.RoundToInt(owner.defaultOpenAnchorFret)), -1f, owner.StrikeLineZ - 5f);
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
            s.transform.SetParent(root.transform, false);
            s.transform.position = new Vector3(0f, GetStringY(i), owner.StrikeLineZ);
            s.transform.localScale = new Vector3(600f, 0.1f, 0.1f);
            Material mat = owner.CreateSharedGlowMaterial(owner.GetStringColor(i), 2f);
            s.GetComponent<Renderer>().material = mat;
            stringVisuals[i] = s;
            stringVisualMats[i] = mat;
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
                light.transform.SetParent(root.transform, false);
                float xPos = f == 0 ? GetNoteX(Mathf.RoundToInt(owner.defaultOpenAnchorFret)) : GetNoteX(f);
                light.transform.position = new Vector3(xPos, GetStringY(s), owner.StrikeLineZ);
                light.transform.localScale = new Vector3(0.6f, 0.6f, 0.2f);

                Material mat = owner.CreateSharedGlowMaterial(Color.black, 0f);
                light.GetComponent<Renderer>().material = mat;
                fretLightMats[s, f] = mat;
            }
        }
    }

    private void UpdateNotes(GuitarGameplaySnapshot snapshot)
    {
        float renderSongTime = GetRenderSongTime(snapshot);
        float removeDist = owner.noteSpeed * (owner.hitWindowLate + owner.judgmentGrace) + 1f;
        HashSet<int> visibleThisFrame = new HashSet<int>();

        for (int i = 0; i < snapshot.noteStates.Count; i++)
        {
            GameplayNoteState state = snapshot.noteStates[i];
            float travelZ = owner.StrikeLineZ + ((state.data.time - renderSongTime) * owner.noteSpeed);
            bool keepForResult = state.IsResolved && renderSongTime - state.resolvedAt <= GetResolvedFadeTime();
            bool visible = travelZ <= owner.SpawnZ && (travelZ >= owner.StrikeLineZ || keepForResult);

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
        Debug.Log($"[Highway3D] Creating note view id={data.id} time={data.time:F2} string={data.stringIdx} fret={data.fret} grouped={isGrouped} open={isOpen} pos=({xPos:F2},{yPos:F2},{owner.SpawnZ:F2})");

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "HighwayNote_" + data.id;
        cube.transform.SetParent(root.transform, false);
        cube.transform.position = new Vector3(xPos, yPos, owner.SpawnZ);

        Material noteMat = owner.CreateSharedGlowMaterial(owner.GetStringColor(data.stringIdx), 0.8f);
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
        tail.transform.SetParent(root.transform, false);
        tail.GetComponent<Renderer>().material = owner.CreateSharedGlowMaterial(owner.GetStringColor(data.stringIdx) * 0.4f, 0.2f);

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "Marker_" + data.id;
        marker.transform.SetParent(root.transform, false);
        marker.transform.position = new Vector3(xPos, yPos, owner.StrikeLineZ);
        marker.transform.localScale = GetMarkerScale();
        marker.GetComponent<Renderer>().material = owner.CreateSharedGlowMaterial(owner.GetStringColor(data.stringIdx), 1.1f);

        return new HighwayNoteView
        {
            noteRoot = cube,
            noteRenderer = cube.GetComponent<Renderer>(),
            noteMaterial = noteMat,
            label = textObj != null ? textObj.GetComponent<TextMeshPro>() : null,
            tail = tail,
            marker = marker,
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
        view.marker.transform.position = new Vector3(x, y, owner.StrikeLineZ);

        float tailLength = Mathf.Max(0f, z - owner.StrikeLineZ);
        view.tail.transform.position = new Vector3(x, y, owner.StrikeLineZ + (tailLength * 0.5f));
        view.tail.transform.localScale = new Vector3(owner.FretSpacing * 0.06f, 0.06f, tailLength);
        view.tail.SetActive(tailLength > 0.01f && !state.IsResolved);

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
            Renderer markerRenderer = view.marker.GetComponent<Renderer>();
            Color markerColor = state.IsHit ? owner.highwayHitColor : (state.IsMissed ? owner.highwayMissColor : view.baseColor);
            markerRenderer.material.SetColor("_EmissionColor", markerColor * (state.IsHit ? 2f : 0.8f));
        }
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
        if (fretLightMats == null)
            return;

        int fretLightColumns = GetFretLightColumnCount();

        for (int s = 0; s < 6; s++)
        {
            for (int f = 0; f < fretLightColumns; f++)
                fretLightMats[s, f].SetColor("_EmissionColor", Color.black);
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
                        fretLightMats[s, f].SetColor("_EmissionColor", owner.GetStringColor(s) * 8f);
                }
            }
        }
    }

    private void UpdateSectionCamera(GuitarGameplaySnapshot snapshot)
    {
        float renderSongTime = GetRenderSongTime(snapshot);
        float activeMin = -1000f;
        float activeMax = -1000f;
        bool foundActive = false;

        for (int i = 0; i < snapshot.noteStates.Count; i++)
        {
            GameplayNoteState state = snapshot.noteStates[i];
            float z = owner.StrikeLineZ + ((state.data.time - renderSongTime) * owner.noteSpeed);
            if (z > owner.SpawnZ || z < owner.StrikeLineZ - 2f)
                continue;

            GetFramingRange(state.data, out float minX, out float maxX);
            if (!foundActive)
            {
                activeMin = minX;
                activeMax = maxX;
                foundActive = true;
            }
            else
            {
                activeMin = Mathf.Min(activeMin, minX);
                activeMax = Mathf.Max(activeMax, maxX);
            }
        }

        List<NoteData> upcoming = chartById.Values.Where(n => n.time > renderSongTime && n.time < renderSongTime + owner.lookaheadWindow).ToList();
        float futureMin = activeMin;
        float futureMax = activeMax;
        bool foundUpcoming = false;

        for (int i = 0; i < upcoming.Count; i++)
        {
            GetFramingRange(upcoming[i], out float minX, out float maxX);
            if (!foundUpcoming)
            {
                futureMin = minX;
                futureMax = maxX;
                foundUpcoming = true;
            }
            else
            {
                futureMin = Mathf.Min(futureMin, minX);
                futureMax = Mathf.Max(futureMax, maxX);
            }
        }

        if (foundActive || foundUpcoming)
        {
            float finalMin = foundActive ? Mathf.Min(activeMin, futureMin) : futureMin;
            float finalMax = foundActive ? Mathf.Max(activeMax, futureMax) : futureMax;
            cameraTargetX = (finalMin + finalMax) * 0.5f;
            float spread = (finalMax - finalMin) / owner.FretSpacing;
            cameraTargetFOV = Mathf.Clamp(50f + (spread * 3.5f), 50f, 95f);
        }

        mainCamera.transform.position = Vector3.Lerp(
            mainCamera.transform.position,
            new Vector3(cameraTargetX, owner.highwayCameraY, owner.highwayCameraZ),
            Time.deltaTime * owner.camMoveSpeed);

        mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, cameraTargetFOV, Time.deltaTime * owner.camMoveSpeed);
    }

    private void LogInitialization(List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        int noteCount = chartNotes != null ? chartNotes.Count : 0;
        int sectionCount = sections != null ? sections.Count : 0;
        string noteRange = noteCount > 0
            ? $"first={chartNotes.Min(n => n.time):F2}s last={chartNotes.Max(n => n.time):F2}s"
            : "no chart notes";
        string previewNotes = noteCount > 0
            ? string.Join(" | ", chartNotes.OrderBy(n => n.time).Take(5).Select(n => $"id={n.id}@{n.time:F2}s s{n.stringIdx} f{n.fret}"))
            : "none";

        Debug.Log(
            $"[Highway3D] Initialize root={(root != null ? root.name : "null")} camera={(mainCamera != null ? mainCamera.name : "null")} " +
            $"notes={noteCount} sections={sectionCount} fretCols={GetFretLightColumnCount()} totalFrets={owner.TotalFrets} noteSpeed={owner.noteSpeed:F2} strikeZ={owner.StrikeLineZ:F2} spawnZ={owner.SpawnZ:F2} {noteRange} preview={previewNotes}");
    }

    private void LogRenderDiagnostics(GuitarGameplaySnapshot snapshot)
    {
        if (Time.unscaledTime < nextDiagnosticsLogTime)
            return;

        nextDiagnosticsLogTime = Time.unscaledTime + 1f;

        float renderSongTime = GetRenderSongTime(snapshot);
        float removeDist = owner.noteSpeed * (owner.hitWindowLate + owner.judgmentGrace) + 1f;
        int totalStates = snapshot.noteStates != null ? snapshot.noteStates.Count : 0;
        int visibleCount = 0;
        int upcomingCount = 0;
        GameplayNoteState nextPending = null;

        if (snapshot.noteStates != null)
        {
            for (int i = 0; i < snapshot.noteStates.Count; i++)
            {
                GameplayNoteState state = snapshot.noteStates[i];
                if (state == null || state.IsResolved)
                    continue;

                if (nextPending == null || state.data.time < nextPending.data.time)
                    nextPending = state;

                if (state.data.time >= renderSongTime)
                    upcomingCount++;

                float z = owner.StrikeLineZ + ((state.data.time - renderSongTime) * owner.noteSpeed);
                if (z <= owner.SpawnZ && z >= owner.StrikeLineZ - removeDist)
                    visibleCount++;
            }
        }

        string nextPendingText = nextPending != null
            ? $"id={nextPending.data.id} time={nextPending.data.time:F2} string={nextPending.data.stringIdx} fret={nextPending.data.fret}"
            : "none";

        string sampleViewText = "none";
        if (noteViews.Count > 0)
        {
            HighwayNoteView sampleView = noteViews.Values.FirstOrDefault(view => view != null && view.noteRoot != null);
            if (sampleView != null)
            {
                Vector3 worldPos = sampleView.noteRoot.transform.position;
                Vector3 viewportPos = mainCamera.WorldToViewportPoint(worldPos);
                sampleViewText = $"world={worldPos} viewport={viewportPos}";
            }
        }

        Debug.Log(
            $"[Highway3D] Render diag songTime={snapshot.songTime:F2} renderSongTime={renderSongTime:F2} paused={snapshot.isPaused} mainMenu={snapshot.showMainMenu} " +
            $"trackSelect={snapshot.showTrackSelection} songSelect={snapshot.showSongSelection} states={totalStates} visible={visibleCount} upcoming={upcomingCount} " +
            $"spawnedViews={noteViews.Count} chordFrames={chordFrames.Count} cameraPos={mainCamera.transform.position} cameraRot={mainCamera.transform.rotation.eulerAngles} fov={mainCamera.fieldOfView:F2} " +
            $"nextPending={nextPendingText} sampleView={sampleViewText}");
    }

    private float GetRenderSongTime(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null)
            return 0f;

        float renderSongTime = Mathf.Max(0f, snapshot.songTime);
        float visibleWindow = GetVisibleLeadTime();

        if (snapshot.noteStates == null || snapshot.noteStates.Count == 0)
            return renderSongTime;

        bool shouldPreviewUpcoming = snapshot.isPaused || snapshot.songTime < 0f || snapshot.showMainMenu || snapshot.showSongSelection || snapshot.showTrackSelection;
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
        if (Time.unscaledTime >= nextPreviewLogTime)
        {
            nextPreviewLogTime = Time.unscaledTime + 1f;
            Debug.Log($"[Highway3D] Previewing upcoming note id={nextPending.data.id} noteTime={nextPending.data.time:F2} renderSongTime={previewRenderTime:F2} visibleWindow={visibleWindow:F2} paused={snapshot.isPaused} songTime={snapshot.songTime:F2}");
        }

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
        int furthestFret = handFret + 2;
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
        return new Vector3(owner.FretSpacing * 0.56f, 0.44f, Mathf.Max(0.48f, owner.FretSpacing * 0.28f));
    }

    private Vector3 GetGroupedFrettedNoteScale()
    {
        return new Vector3(
            owner.FretSpacing * 0.54f,
            0.4f,
            Mathf.Max(0.44f, owner.FretSpacing * 0.26f));
    }

    private Vector3 GetSingleOpenNoteScale()
    {
        return new Vector3(
            owner.FretSpacing * 2.7f,
            GetScaledOpenHeight(),
            GetScaledOpenDepth());
    }

    private float GetScaledOpenHeight()
    {
        return 0.2f;
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
        parent.transform.SetParent(root.transform, false);
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
        }
    }
}