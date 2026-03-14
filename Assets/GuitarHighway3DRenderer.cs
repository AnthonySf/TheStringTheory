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
    private readonly Material[,] fretLightMats = new Material[6, 24];
    private float cameraTargetX;
    private float cameraTargetFOV = 60f;

    public void Initialize(GuitarBridgeServer owner, List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        this.owner = owner;
        mainCamera = Camera.main;
        root = new GameObject("Highway3DRendererRoot");

        BuildChartCaches(chartNotes);
        ConfigureCamera();
        GenerateFretboard();
        GenerateStrings();
        GenerateFretLightGrid();
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
        if (snapshot == null || mainCamera == null)
            return;

        ConfigureCamera();
        UpdateFretboardLights(snapshot.latestDetectedPitches);
        UpdateNotes(snapshot);
        UpdateChordFrames(snapshot);
        UpdateSectionCamera(snapshot);
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
        for (int s = 0; s < 6; s++)
        {
            for (int f = 0; f <= owner.TotalFrets; f++)
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
        float renderSongTime = Mathf.Max(0f, snapshot.songTime);
        float removeDist = owner.noteSpeed * (owner.hitWindowLate + owner.judgmentGrace) + 1f;
        HashSet<int> visibleThisFrame = new HashSet<int>();

        for (int i = 0; i < snapshot.noteStates.Count; i++)
        {
            GameplayNoteState state = snapshot.noteStates[i];
            float z = owner.StrikeLineZ + ((state.data.time - renderSongTime) * owner.noteSpeed);
            bool keepForResult = state.IsResolved && renderSongTime - state.resolvedAt <= owner.highwayResolvedHoldTime;
            bool visible = z <= owner.SpawnZ && z >= owner.StrikeLineZ - removeDist;

            if (!visible && !keepForResult)
                continue;

            visibleThisFrame.Add(state.data.id);

            if (!noteViews.TryGetValue(state.data.id, out HighwayNoteView view) || view == null)
            {
                view = CreateNoteView(state.data);
                noteViews[state.data.id] = view;
            }

            UpdateNoteView(view, state, z, renderSongTime);
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
        cube.transform.SetParent(root.transform, false);
        cube.transform.position = new Vector3(xPos, yPos, owner.SpawnZ);

        Material noteMat = owner.CreateSharedGlowMaterial(owner.GetStringColor(data.stringIdx), 4f);
        cube.GetComponent<Renderer>().material = noteMat;

        GameObject textObj = null;
        if (!isOpen)
        {
            textObj = new GameObject("NoteText");
            textObj.transform.SetParent(cube.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0f, -0.6f);

            TextMeshPro tm = textObj.AddComponent<TextMeshPro>();
            tm.text = data.fret.ToString();
            tm.fontSize = isGrouped ? 7 : 8;
            tm.alignment = TextAlignmentOptions.Center;
            tm.color = Color.white;
        }

        if (isGrouped)
        {
            if (isOpen)
            {
                float leftX = GetHandWindowStartX(GetGroupHandFret(group));
                float rightX = GetHandWindowEndX(GetGroupHandFret(group));
                cube.transform.localScale = new Vector3(Mathf.Max(0.5f, rightX - leftX), owner.chordOpenLineHeight, owner.chordOpenLineDepth);
            }
            else
            {
                cube.transform.localScale = new Vector3(owner.chordFrettedNoteWidth, owner.chordFrettedNoteHeight, owner.chordFrettedNoteDepth);
            }
        }
        else
        {
            if (isOpen)
                cube.transform.localScale = new Vector3(owner.singleOpenWidth, owner.singleOpenHeight, owner.singleOpenDepth);
            else
                cube.transform.localScale = new Vector3(3.5f, 0.9f, 0.6f);
        }

        GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tail.name = "Tail_" + data.id;
        tail.transform.SetParent(root.transform, false);
        tail.GetComponent<Renderer>().material = owner.CreateSharedGlowMaterial(owner.GetStringColor(data.stringIdx) * 0.5f, 1f);

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "Marker_" + data.id;
        marker.transform.SetParent(root.transform, false);
        marker.transform.position = new Vector3(xPos, yPos, owner.StrikeLineZ);
        marker.transform.localScale = new Vector3(0.5f, 0.5f, 0.2f);
        marker.GetComponent<Renderer>().material = owner.CreateSharedGlowMaterial(owner.GetStringColor(data.stringIdx), 6f);

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
        view.tail.transform.localScale = new Vector3(0.1f, 0.1f, tailLength);
        view.tail.SetActive(tailLength > 0.01f);

        view.noteRoot.transform.localScale = view.baseScale;

        Color finalColor = view.baseColor;
        float emission = 4f;

        if (state.IsHit)
        {
            finalColor = owner.highwayHitColor;
            emission = 5.5f;
            float pulse = Mathf.Clamp01((songTime - state.resolvedAt) / Mathf.Max(0.01f, owner.highwayResolvedHoldTime));
            view.noteRoot.transform.localScale = view.baseScale * Mathf.Lerp(1.08f, 1f, pulse);
        }
        else if (state.IsMissed)
        {
            finalColor = owner.highwayMissColor;
            emission = 2f;
        }
        else if (state.isJudgeable)
        {
            emission = 4f * owner.judgeableDarkenMultiplier;
            finalColor = Color.Lerp(view.baseColor, Color.white, 0.15f);
        }

        view.noteMaterial.color = finalColor;
        view.noteMaterial.EnableKeyword("_EMISSION");
        view.noteMaterial.SetColor("_EmissionColor", finalColor * Mathf.Pow(2f, emission));

        if (view.label != null)
            view.label.color = state.IsMissed ? owner.highwayMissColor : Color.white;

        if (view.marker != null)
        {
            Renderer markerRenderer = view.marker.GetComponent<Renderer>();
            Color markerColor = state.IsHit ? owner.highwayHitColor : (state.IsMissed ? owner.highwayMissColor : view.baseColor);
            markerRenderer.material.SetColor("_EmissionColor", markerColor * (state.IsHit ? 15f : 6f));
        }
    }

    private void UpdateChordFrames(GuitarGameplaySnapshot snapshot)
    {
        HashSet<int> activeChordIds = new HashSet<int>();

        foreach (var pair in chordGroups)
        {
            List<NoteData> group = pair.Value;
            if (group == null || group.Count < 2)
                continue;

            float anchorTime = group[0].time;
            float z = owner.StrikeLineZ + ((anchorTime - snapshot.songTime) * owner.noteSpeed);
            bool anyRecent = group.Any(n => TryGetState(snapshot.noteStates, n.id, out GameplayNoteState state) && (!state.IsResolved || snapshot.songTime - state.resolvedAt <= owner.highwayResolvedHoldTime));
            bool visible = z <= owner.SpawnZ && z >= owner.StrikeLineZ - (owner.noteSpeed * (owner.hitWindowLate + owner.judgmentGrace) + 1f);

            if (!visible && !anyRecent)
                continue;

            activeChordIds.Add(pair.Key);

            if (!chordFrames.TryGetValue(pair.Key, out GameObject frame) || frame == null)
            {
                int handFret = GetGroupHandFret(group);
                float leftX = GetHandWindowStartX(handFret);
                float rightX = GetHandWindowEndX(handFret);
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
        for (int s = 0; s < 6; s++)
        {
            for (int f = 0; f <= owner.TotalFrets; f++)
                fretLightMats[s, f].SetColor("_EmissionColor", Color.black);
        }

        if (pitchesToLight == null)
            return;

        foreach (int pitch in pitchesToLight)
        {
            for (int s = 0; s < 6; s++)
            {
                for (int f = 0; f <= owner.TotalFrets; f++)
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
        float renderSongTime = Mathf.Max(0f, snapshot.songTime);
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
        return GetNoteX(handFret - 1) - (owner.FretSpacing * owner.chordSidePaddingFrets);
    }

    private float GetHandWindowEndX(int handFret)
    {
        return GetNoteX(handFret + 3) + (owner.FretSpacing * owner.chordSidePaddingFrets);
    }

    private float GetGroupAnchorX(List<NoteData> group)
    {
        int handFret = GetGroupHandFret(group);
        return (GetHandWindowStartX(handFret) + GetHandWindowEndX(handFret)) * 0.5f;
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
            maxX = GetHandWindowEndX(handFret);
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