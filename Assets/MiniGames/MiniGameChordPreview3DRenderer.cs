using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public sealed class MiniGameChordPreview3DRenderer
{
    private const string RootName = "MiniGameChordPreview3D";
    private const float ChordSpacing = 3.62f;
    private const float DiagramWidth = 2.70f;
    private const float DiagramHeight = 3.16f;
    private const int VisibleFretCount = 4;
    private const float StringColumnCount = 5f;
    private const float FingerCircleDiameter = 0.48f;
    private const float BarreHeight = 0.44f;
    private const float NoteDepth = 0.12f;
    private const float FrameThickness = 0.045f;
    private const float CameraDistance = 12.4f;
    private const float CameraYOffset = 4.34f;

    private readonly GuitarBridgeServer owner;
    private GameObject root;
    private string lastSignature = string.Empty;
    private bool staleRootScanDone;

    public MiniGameChordPreview3DRenderer(GuitarBridgeServer owner)
    {
        this.owner = owner;
    }

    public void Update(FightClubMiniGameSnapshot snapshot, bool visible)
    {
        bool shouldShow = visible && snapshot != null && snapshot.active && snapshot.chords != null && snapshot.chords.Count > 0;
        if (!shouldShow)
        {
            Hide();
            return;
        }

        EnsureRoot();
        SetVisible(true);
        PositionRoot();

        string signature = BuildSignature(snapshot);
        if (!string.Equals(signature, lastSignature, StringComparison.Ordinal))
        {
            Rebuild(snapshot);
            lastSignature = signature;
        }

        float pulse = 1f + (Mathf.Sin(Time.unscaledTime * 4.2f) * 0.012f);
        root.transform.localScale = new Vector3(pulse, pulse, 1f);
    }

    public void Hide()
    {
        if (root == null)
        {
            DestroyExistingRootOnce();
            return;
        }

        ClearChildren();
        lastSignature = string.Empty;
        SetVisible(false);
    }

    private void EnsureRoot()
    {
        if (root != null)
            return;

        DestroyExistingRoot();
        staleRootScanDone = true;

        root = new GameObject(RootName);
        root.hideFlags = HideFlags.DontSave;
        if (owner != null)
            root.transform.SetParent(owner.transform, false);
        root.SetActive(false);
    }

    private void SetVisible(bool visible)
    {
        if (root == null)
            return;

        if (root.activeSelf != visible)
            root.SetActive(visible);
    }

    private void DestroyExistingRootOnce()
    {
        if (staleRootScanDone)
            return;

        DestroyExistingRoot();
        staleRootScanDone = true;
    }

    private static void DestroyExistingRoot()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
            Object.Destroy(existing);
    }

    private void PositionRoot()
    {
        Camera camera = Camera.main;
        if (camera == null || root == null)
            return;

        Transform cameraTransform = camera.transform;
        root.transform.position = cameraTransform.position + (cameraTransform.forward * CameraDistance) + (cameraTransform.up * CameraYOffset);
        root.transform.rotation = cameraTransform.rotation;
    }

    private void Rebuild(FightClubMiniGameSnapshot snapshot)
    {
        ClearChildren();

        List<FightClubChordSnapshot> chords = snapshot.chords;
        int count = Mathf.Min(FightClubRunSettings.MaxChordsPerRound, chords.Count);
        float spacing = count >= FightClubRunSettings.MaxChordsPerRound ? 3.16f : ChordSpacing;
        for (int i = 0; i < count; i++)
        {
            FightClubChordSnapshot chord = chords[i];
            if (chord == null)
                continue;

            GameObject chordRoot = new GameObject("FightClubChord_" + i);
            chordRoot.transform.SetParent(root.transform, false);
            chordRoot.transform.localPosition = new Vector3((i - ((count - 1) * 0.5f)) * spacing, 0f, 0f);
            chordRoot.transform.localRotation = Quaternion.identity;
            float scale = count >= FightClubRunSettings.MaxChordsPerRound
                ? chord.active ? 0.98f : 0.86f
                : chord.active ? 1.08f : 0.96f;
            chordRoot.transform.localScale = new Vector3(scale, scale, scale);

            CreateChordPrompt(chordRoot.transform, chord, i);
        }
    }

    private void CreateChordPrompt(Transform parent, FightClubChordSnapshot chord, int chordIndex)
    {
        int[] frets = chord.fretsLowToHigh ?? Array.Empty<int>();
        int[] fingers = chord.fingersLowToHigh ?? Array.Empty<int>();
        int baseFret = GetBaseFret(frets, chord.barres);

        Color frameColor = chord.status == 1
            ? new Color(0.42f, 0.96f, 0.68f, 0.96f)
            : chord.status == 2
                ? new Color(1f, 0.30f, 0.24f, 0.96f)
                : chord.active
                    ? new Color(0.55f, 0.94f, 1f, 0.98f)
                    : new Color(0.72f, 0.85f, 1f, 0.72f);

        float left = -DiagramWidth * 0.5f;
        float right = DiagramWidth * 0.5f;
        float bottom = -DiagramHeight * 0.5f;
        float top = DiagramHeight * 0.5f;
        float fretSpacing = DiagramHeight / VisibleFretCount;

        CreateChordGrid(parent, left, right, bottom, top, baseFret, frameColor, chord.active);
        CreateBarres(parent, chord.barres, baseFret, left, right, top, fretSpacing, chord.active);

        for (int stringIndex = 0; stringIndex < frets.Length && stringIndex < 6; stringIndex++)
        {
            int fret = frets[stringIndex];
            float x = GetStringDiagramX(stringIndex, left, right);
            int finger = stringIndex < fingers.Length ? fingers[stringIndex] : 0;

            if (fret < 0)
            {
                CreateStringMarker(parent, "x", stringIndex, x, top + 0.36f, frameColor);
                continue;
            }

            if (fret == 0)
            {
                CreateStringMarker(parent, "0", stringIndex, x, top + 0.36f, frameColor);
                continue;
            }

            if (IsCoveredByBarre(chord.barres, fret, stringIndex, finger))
                continue;

            int displayFret = Mathf.Clamp(fret - baseFret, 0, VisibleFretCount - 1);
            float y = top - ((displayFret + 0.5f) * fretSpacing);
            CreateFingerCircle(parent, stringIndex, finger, x, y, FingerCircleDiameter, chord.active);
        }

        CreateChordFrame(parent, left, right, bottom, top, frameColor, chord.active);
        CreateChordNameLabel(parent, chord.name, left, bottom - 0.42f, frameColor);
        CreateOrdinalLabel(parent, chord.status == 1 ? "O" : chord.status == 2 ? "X" : (chordIndex + 1).ToString(), right, bottom - 0.42f, frameColor);
    }

    private static int GetBaseFret(int[] frets, List<FightClubBarreSnapshot> barres)
    {
        int minFret = int.MaxValue;
        int maxFret = 0;
        bool hasOpenString = false;
        for (int i = 0; i < frets.Length; i++)
        {
            int fret = frets[i];
            if (fret == 0)
                hasOpenString = true;
            if (fret <= 0)
                continue;

            minFret = Mathf.Min(minFret, fret);
            maxFret = Mathf.Max(maxFret, fret);
        }

        if (minFret == int.MaxValue)
            return 1;

        if (barres != null)
        {
            for (int i = 0; i < barres.Count; i++)
            {
                FightClubBarreSnapshot barre = barres[i];
                if (barre != null && barre.fret > 1)
                    return barre.fret;
            }
        }

        if (!hasOpenString && minFret > 1)
            return minFret;

        return maxFret > VisibleFretCount ? Mathf.Max(1, minFret) : 1;
    }

    private void CreateChordGrid(Transform parent, float left, float right, float bottom, float top, int baseFret, Color frameColor, bool active)
    {
        Material gridMat = CreatePreviewTransparentMaterial(new Color(0.88f, 0.96f, 1f, active ? 0.62f : 0.42f), active ? 0.18f : 0.08f);
        ConfigureOverlayMaterial(gridMat, 140, true);

        float width = right - left;
        float height = top - bottom;
        float fretSpacing = height / VisibleFretCount;

        for (int i = 0; i < 6; i++)
        {
            float x = GetStringDiagramX(i, left, right);
            Color stringColor = owner != null ? owner.GetStringColor(i) : Color.cyan;
            Material stringMat = CreatePreviewTransparentMaterial(new Color(stringColor.r, stringColor.g, stringColor.b, active ? 0.82f : 0.56f), active ? 0.35f : 0.15f);
            ConfigureOverlayMaterial(stringMat, 145, true);
            CreateFramePiece(parent, new Vector3(x, 0f, 0.04f), new Vector3(0.026f, height, 0.055f), stringMat, "MiniGameChordString");
        }

        for (int fretLine = 0; fretLine <= VisibleFretCount; fretLine++)
        {
            float y = top - (fretLine * fretSpacing);
            bool isNut = fretLine == 0 && baseFret == 1;
            float thickness = isNut ? 0.16f : 0.035f;
            Material lineMat = isNut
                ? CreatePreviewGlowMaterial(new Color(0.95f, 0.98f, 1f, active ? 0.98f : 0.82f), active ? 0.55f : 0.22f)
                : gridMat;
            ConfigureOverlayMaterial(lineMat, isNut ? 172 : 140, true);
            CreateFramePiece(parent, new Vector3(0f, y, 0.035f), new Vector3(width, thickness, 0.055f), lineMat, isNut ? "MiniGameChordNut" : "MiniGameChordFretLine");
        }

        if (baseFret > 1)
            CreateBaseFretLabel(parent, baseFret, left - 0.24f, top - (fretSpacing * 0.50f), frameColor);
    }

    private static float GetStringDiagramX(int stringIndex, float left, float right)
    {
        return left + ((stringIndex / StringColumnCount) * (right - left));
    }

    private void CreateBarres(Transform parent, List<FightClubBarreSnapshot> barres, int baseFret, float left, float right, float top, float fretSpacing, bool active)
    {
        if (barres == null)
            return;

        for (int i = 0; i < barres.Count; i++)
        {
            FightClubBarreSnapshot barre = barres[i];
            if (barre == null || barre.fret <= 0)
                continue;

            int displayFret = Mathf.Clamp(barre.fret - baseFret, 0, VisibleFretCount - 1);
            float y = top - ((displayFret + 0.5f) * fretSpacing);
            int startString = Mathf.Clamp(Mathf.Min(barre.startString, barre.endString), 0, 5);
            int endString = Mathf.Clamp(Mathf.Max(barre.startString, barre.endString), 0, 5);
            float startX = GetStringDiagramX(startString, left, right);
            float endX = GetStringDiagramX(endString, left, right);
            CreateBarreMarker(parent, startString, endString, barre.finger, startX, endX, y, active);
        }
    }

    private static bool IsCoveredByBarre(List<FightClubBarreSnapshot> barres, int fret, int stringIndex, int finger)
    {
        if (barres == null)
            return false;

        for (int i = 0; i < barres.Count; i++)
        {
            FightClubBarreSnapshot barre = barres[i];
            if (barre == null || barre.fret != fret)
                continue;

            int start = Mathf.Min(barre.startString, barre.endString);
            int end = Mathf.Max(barre.startString, barre.endString);
            if (stringIndex >= start && stringIndex <= end && (finger <= 0 || finger == barre.finger))
                return true;
        }

        return false;
    }

    private void CreateBarreMarker(Transform parent, int startString, int endString, int finger, float startX, float endX, float y, bool active)
    {
        Color color = new Color(0.98f, 0.98f, 1f, active ? 0.96f : 0.82f);
        Material material = CreatePreviewGlowMaterial(color, active ? 1.12f : 0.72f);
        ConfigureOverlayMaterial(material, 188, true);

        float width = Mathf.Abs(endX - startX);
        float centerX = (startX + endX) * 0.5f;
        CreateFramePiece(parent, new Vector3(centerX, y, 0f), new Vector3(Mathf.Max(BarreHeight * 0.25f, width), BarreHeight, NoteDepth), material, "MiniGameBarreBody");
        CreateCirclePiece(parent, new Vector3(startX, y, -0.002f), BarreHeight, NoteDepth, material, "MiniGameBarreCapStart");
        CreateCirclePiece(parent, new Vector3(endX, y, -0.002f), BarreHeight, NoteDepth, material, "MiniGameBarreCapEnd");
        CreateFingerLabel(parent, finger, centerX, y, new Color(0.02f, 0.04f, 0.08f, 1f), 3.75f);
    }

    private void CreateFingerCircle(Transform parent, int stringIndex, int finger, float x, float y, float diameter, bool active)
    {
        Color stringColor = owner != null ? owner.GetStringColor(stringIndex) : Color.cyan;
        float intensity = active ? 1.15f : 0.76f;
        Material noteMat = CreatePreviewGlowMaterial(stringColor, intensity);
        ConfigureOverlayMaterial(noteMat, 180, true);

        CreateCirclePiece(parent, new Vector3(x, y, 0f), diameter, NoteDepth, noteMat, "MiniGameFinger_" + stringIndex + "_" + finger);
        CreateFingerLabel(parent, finger, x, y, GetReadableTextColor(stringColor), 3.65f);
    }

    private void CreateNoteOutline(Transform parent, Vector3 center, float width, float height, Color color)
    {
        Material outlineMat = CreatePreviewTransparentMaterial(new Color(color.r, color.g, color.b, 0.70f), 0.18f);
        ConfigureOverlayMaterial(outlineMat, 190, true);

        CreateFramePiece(parent, center + new Vector3(0f, height * 0.5f, 0f), new Vector3(width, 0.034f, NoteDepth * 0.55f), outlineMat, "MiniGameNoteOutlineTop");
        CreateFramePiece(parent, center + new Vector3(0f, -height * 0.5f, 0f), new Vector3(width, 0.034f, NoteDepth * 0.55f), outlineMat, "MiniGameNoteOutlineBottom");
        CreateFramePiece(parent, center + new Vector3(-width * 0.5f, 0f, 0f), new Vector3(0.034f, height, NoteDepth * 0.55f), outlineMat, "MiniGameNoteOutlineLeft");
        CreateFramePiece(parent, center + new Vector3(width * 0.5f, 0f, 0f), new Vector3(0.034f, height, NoteDepth * 0.55f), outlineMat, "MiniGameNoteOutlineRight");
    }

    private void CreateChordFrame(Transform parent, float left, float right, float bottom, float top, Color color, bool active)
    {
        Material frameMat = CreatePreviewGlowMaterial(color, active ? 1.28f : 0.82f);
        ConfigureOverlayMaterial(frameMat, 170, true);

        float width = Mathf.Max(0.5f, right - left);
        float height = Mathf.Max(0.5f, top - bottom);
        Vector3 center = new Vector3((left + right) * 0.5f, (bottom + top) * 0.5f, 0.025f);
        CreateFramePiece(parent, center + new Vector3(0f, height * 0.5f, 0f), new Vector3(width, FrameThickness, NoteDepth), frameMat, "MiniGameChordFrameTop");
        CreateFramePiece(parent, center + new Vector3(0f, -height * 0.5f, 0f), new Vector3(width, FrameThickness, NoteDepth), frameMat, "MiniGameChordFrameBottom");
        CreateFramePiece(parent, center + new Vector3(-width * 0.5f, 0f, 0f), new Vector3(FrameThickness, height, NoteDepth), frameMat, "MiniGameChordFrameLeft");
        CreateFramePiece(parent, center + new Vector3(width * 0.5f, 0f, 0f), new Vector3(FrameThickness, height, NoteDepth), frameMat, "MiniGameChordFrameRight");
    }

    private void CreateFramePiece(Transform parent, Vector3 localPosition, Vector3 scale, Material material, string name)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = name;
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localRotation = Quaternion.identity;
        piece.transform.localScale = scale;
        Renderer renderer = piece.GetComponent<Renderer>();
        renderer.material = material;
        ConfigureRenderer(renderer);
        Object.Destroy(piece.GetComponent<Collider>());
    }

    private GameObject CreateCirclePiece(Transform parent, Vector3 localPosition, float diameter, float depth, Material material, string name)
    {
        GameObject circle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        circle.name = name;
        circle.transform.SetParent(parent, false);
        circle.transform.localPosition = localPosition;
        circle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        circle.transform.localScale = new Vector3(diameter, depth * 0.50f, diameter);
        Renderer renderer = circle.GetComponent<Renderer>();
        renderer.material = material;
        ConfigureRenderer(renderer);
        Object.Destroy(circle.GetComponent<Collider>());
        return circle;
    }

    private void CreateFingerLabel(Transform parent, int finger, float x, float y, Color color, float fontSize)
    {
        string text = finger > 0 ? finger.ToString() : string.Empty;
        TextMeshPro label = CreateText(parent, text, fontSize, color, TextAlignmentOptions.Center);
        label.transform.localPosition = new Vector3(x, y - 0.02f, -0.075f);
        label.rectTransform.sizeDelta = new Vector2(2.7f, 2.0f);
    }

    private void CreateChordNameLabel(Transform parent, string text, float x, float y, Color color)
    {
        TextMeshPro label = CreateText(parent, string.IsNullOrWhiteSpace(text) ? "--" : text.Trim(), 3.8f, Color.white, TextAlignmentOptions.Left);
        label.transform.localPosition = new Vector3(x, y, -0.06f);
        label.rectTransform.pivot = new Vector2(0f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(7.8f, 2.0f);
        ConfigureTextGlow(label, color);
    }

    private void CreateOrdinalLabel(Transform parent, string text, float x, float y, Color color)
    {
        TextMeshPro label = CreateText(parent, text, 3.85f, color, TextAlignmentOptions.Right);
        label.transform.localPosition = new Vector3(x, y, -0.06f);
        label.rectTransform.pivot = new Vector2(1f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(2.3f, 2.0f);
        ConfigureTextGlow(label, color);
    }

    private void CreateStringMarker(Transform parent, string text, int stringIndex, float x, float y, Color color)
    {
        Color stringColor = owner != null ? owner.GetStringColor(stringIndex) : color;
        TextMeshPro label = CreateText(parent, text, 3.65f, new Color(stringColor.r, stringColor.g, stringColor.b, 0.92f), TextAlignmentOptions.Center);
        label.transform.localPosition = new Vector3(x, y, -0.06f);
        label.rectTransform.sizeDelta = new Vector2(2.6f, 2.0f);
        ConfigureTextGlow(label, stringColor);
    }

    private void CreateBaseFretLabel(Transform parent, int baseFret, float x, float y, Color color)
    {
        TextMeshPro label = CreateText(parent, baseFret.ToString() + "fr", 2.65f, new Color(0.90f, 0.96f, 1f, 0.90f), TextAlignmentOptions.Right);
        label.transform.localPosition = new Vector3(x, y, -0.06f);
        label.rectTransform.pivot = new Vector2(1f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(1.55f, 1.55f);
        ConfigureTextGlow(label, color);
    }

    private TextMeshPro CreateText(Transform parent, string text, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject textObj = new GameObject("MiniGameText");
        textObj.transform.SetParent(parent, false);
        textObj.transform.localRotation = Quaternion.identity;
        textObj.transform.localScale = Vector3.one;

        TextMeshPro tm = textObj.AddComponent<TextMeshPro>();
        tm.text = text;
        tm.fontSize = fontSize;
        tm.fontStyle = FontStyles.Bold;
        tm.alignment = alignment;
        tm.overflowMode = TextOverflowModes.Overflow;
        tm.textWrappingMode = TextWrappingModes.NoWrap;
        tm.characterSpacing = 0f;
        tm.lineSpacing = 0f;
        tm.color = color;
        tm.sortingOrder = 280;
        if (tm.fontSharedMaterial != null)
            tm.fontMaterial = new Material(tm.fontSharedMaterial);
        ConfigureTextGlow(tm, color);
        return tm;
    }

    private static void ConfigureTextGlow(TextMeshPro label, Color color)
    {
        if (label == null || label.fontMaterial == null)
            return;

        Material fontMat = label.fontMaterial;
        if (fontMat.HasProperty("_FaceColor"))
            fontMat.SetColor("_FaceColor", label.color);
        bool darkFace = GetRelativeLuminance(label.color) < 0.38f;
        Color outlineColor = darkFace
            ? new Color(1f, 1f, 1f, 0.82f)
            : new Color(0.01f, 0.03f, 0.06f, 0.95f);
        if (fontMat.HasProperty("_OutlineWidth"))
            fontMat.SetFloat("_OutlineWidth", darkFace ? 0.08f : 0.12f);
        if (fontMat.HasProperty("_OutlineColor"))
            fontMat.SetColor("_OutlineColor", outlineColor);
        if (fontMat.HasProperty("_GlowColor"))
        {
            fontMat.SetFloat("_GlowPower", 0.66f);
            fontMat.SetFloat("_GlowInner", 0.03f);
            fontMat.SetFloat("_GlowOuter", 0.25f);
            Color glowColor = darkFace
                ? new Color(outlineColor.r, outlineColor.g, outlineColor.b, 0.55f)
                : new Color(color.r, color.g, color.b, 0.88f);
            fontMat.SetColor("_GlowColor", glowColor);
        }

        fontMat.renderQueue = (int)RenderQueue.Transparent + 185;
        if (fontMat.HasProperty("_ZWrite"))
            fontMat.SetFloat("_ZWrite", 0f);
        if (fontMat.HasProperty("_CullMode"))
            fontMat.SetFloat("_CullMode", 0f);
        if (fontMat.HasProperty("_ZTestMode"))
            fontMat.SetFloat("_ZTestMode", (float)CompareFunction.Always);
        else if (fontMat.HasProperty("_ZTest"))
            fontMat.SetFloat("_ZTest", (float)CompareFunction.Always);
    }

    private static void ConfigureOverlayMaterial(Material material, int queueOffset, bool alwaysOnTop)
    {
        if (material == null)
            return;

        material.renderQueue = (int)RenderQueue.Transparent + queueOffset;
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (alwaysOnTop)
        {
            if (material.HasProperty("_ZTestMode"))
                material.SetFloat("_ZTestMode", (float)CompareFunction.Always);
            else if (material.HasProperty("_ZTest"))
                material.SetFloat("_ZTest", (float)CompareFunction.Always);
        }
    }

    private Material CreatePreviewGlowMaterial(Color color, float intensity)
    {
        Material material = owner != null ? owner.CreateSharedGlowMaterial(color, intensity) : null;
        if (IsBrokenMaterial(material))
            material = CreateFallbackMaterial(color);
        ApplyMaterialColor(material, color);
        return material;
    }

    private Material CreatePreviewTransparentMaterial(Color color, float intensity)
    {
        Material material = owner != null ? owner.CreateSharedTransparentMaterial(color, intensity) : null;
        if (IsBrokenMaterial(material))
            material = CreateFallbackMaterial(color);
        ApplyMaterialColor(material, color);
        return material;
    }

    private static bool IsBrokenMaterial(Material material)
    {
        return material == null ||
               material.shader == null ||
               string.Equals(material.shader.name, "Hidden/InternalErrorShader", StringComparison.Ordinal);
    }

    private static Material CreateFallbackMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        ApplyMaterialColor(material, color);
        return material;
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", color);
        material.color = color;
    }

    private static Color GetReadableTextColor(Color background)
    {
        float luminance = GetRelativeLuminance(background);
        float contrastWithDark = (luminance + 0.05f) / 0.05f;
        float contrastWithLight = 1.05f / (luminance + 0.05f);
        return contrastWithDark >= contrastWithLight
            ? new Color(0.015f, 0.025f, 0.045f, 1f)
            : new Color(0.98f, 0.99f, 1f, 1f);
    }

    private static float GetRelativeLuminance(Color color)
    {
        float r = ToLinearColorChannel(color.r);
        float g = ToLinearColorChannel(color.g);
        float b = ToLinearColorChannel(color.b);
        return (0.2126f * r) + (0.7152f * g) + (0.0722f * b);
    }

    private static float ToLinearColorChannel(float value)
    {
        value = Mathf.Clamp01(value);
        return value <= 0.04045f
            ? value / 12.92f
            : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
    }

    private static void ConfigureRenderer(Renderer renderer)
    {
        if (renderer == null)
            return;

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private void ClearChildren()
    {
        if (root == null)
            return;

        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Object.Destroy(root.transform.GetChild(i).gameObject);
    }

    private static string BuildSignature(FightClubMiniGameSnapshot snapshot)
    {
        if (snapshot?.chords == null || snapshot.chords.Count == 0)
            return "empty";

        var parts = new List<string>
        {
            snapshot.activeChordIndex.ToString(),
            snapshot.phaseLabel ?? string.Empty
        };
        for (int i = 0; i < snapshot.chords.Count; i++)
        {
            FightClubChordSnapshot chord = snapshot.chords[i];
            string frets = chord?.fretsLowToHigh == null ? string.Empty : string.Join(",", chord.fretsLowToHigh);
            string fingers = chord?.fingersLowToHigh == null ? string.Empty : string.Join(",", chord.fingersLowToHigh);
            string barres = BuildBarreSignature(chord?.barres);
            parts.Add($"{chord?.name}:{frets}:{fingers}:{barres}:{chord?.status}:{chord?.active}");
        }

        return string.Join("|", parts);
    }

    private static string BuildBarreSignature(List<FightClubBarreSnapshot> barres)
    {
        if (barres == null || barres.Count == 0)
            return string.Empty;

        var parts = new string[barres.Count];
        for (int i = 0; i < barres.Count; i++)
        {
            FightClubBarreSnapshot barre = barres[i];
            parts[i] = barre == null
                ? string.Empty
                : $"{barre.fret},{barre.startString},{barre.endString},{barre.finger}";
        }

        return string.Join(";", parts);
    }
}
