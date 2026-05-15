using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public static class TabBendCurveBuilder
{
    private const float MinimumRaisedLiftInLines = 0.95f;
    private const float LiftPerSemitoneInLines = 0.72f;
    private const float MinimumThickness = 0.032f;
    private const float MinimumSegmentLength = 0.035f;
    private const int BendCurveSamples = 18;
    private const int VibratoCurveSamples = 18;
    private const float VibratoAmplitudeInLines = 0.18f;
    private const float VibratoCyclesPerSecond = 5f;

    public static GameObject Build(
        Transform parent,
        GuitarBridgeServer owner,
        NoteData note,
        TabSectionData section,
        float noteCenterX,
        float noteCenterY,
        float leftEdge,
        float usableWidth,
        float visibleNoteRadius,
        float lineSpacing,
        List<Renderer> extraRenderers)
    {
        List<NoteTechniqueSegmentData> bendSegments = GetRenderableSegments(note);
        if (bendSegments.Count == 0 || section == null)
            return null;

        GameObject root = new GameObject($"BendCurve_{note.id}");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        Color color = owner.GetStringColor(note.stringIdx);
        float thickness = Mathf.Max(MinimumThickness, lineSpacing * 0.078f);
        float depth = Mathf.Max(0.02f, owner.tabSustainDepth * 0.75f);
        Vector3? previousEndpoint = null;

        for (int i = 0; i < bendSegments.Count; i++)
        {
            NoteTechniqueSegmentData segment = bendSegments[i];
            float segmentStartTime = note.time + segment.startOffset;
            float segmentEndTime = note.time + segment.endOffset;
            if (segmentEndTime <= section.startTime || segmentStartTime >= section.endTime)
                continue;

            float clippedStartTime = Mathf.Max(section.startTime, segmentStartTime);
            float clippedEndTime = Mathf.Min(section.endTime, segmentEndTime);
            if (clippedEndTime <= clippedStartTime + 0.0001f)
                continue;

            float originalDuration = Mathf.Max(0.0001f, segmentEndTime - segmentStartTime);
            float clipStartT = Mathf.Clamp01((clippedStartTime - segmentStartTime) / originalDuration);
            float clipEndT = Mathf.Clamp01((clippedEndTime - segmentStartTime) / originalDuration);
            if (clipEndT <= clipStartT + 0.0001f)
                continue;

            float startBend = Mathf.Lerp(segment.startBend, segment.endBend, clipStartT);
            float endBend = Mathf.Lerp(segment.startBend, segment.endBend, clipEndT);
            if (Mathf.Abs(startBend) <= 0.05f)
                startBend = 0f;
            if (Mathf.Abs(endBend) <= 0.05f)
                endBend = 0f;
            float startX = i == 0 && Mathf.Abs(clippedStartTime - note.time) <= 0.0001f
                ? Mathf.Max(noteCenterX + (visibleNoteRadius * 0.62f), TimeToX(clippedStartTime, section.startTime, section.endTime, leftEdge, usableWidth))
                : TimeToX(clippedStartTime, section.startTime, section.endTime, leftEdge, usableWidth);
            float endX = TimeToX(clippedEndTime, section.startTime, section.endTime, leftEdge, usableWidth);

            float startY = EvaluateBendY(noteCenterY, lineSpacing, startBend);
            float endY = EvaluateBendY(noteCenterY, lineSpacing, endBend);

            List<Vector3> points = segment.type == NoteTechniqueSegmentType.Vibrato
                ? BuildVibratoPoints(startX, endX, startY, endY, owner.tabZDepth - 0.085f, clippedEndTime - clippedStartTime, lineSpacing)
                : BuildCurvePoints(segment.type, startX, endX, startY, endY, owner.tabZDepth - 0.085f);

            if (previousEndpoint.HasValue && points.Count > 0)
                points[0] = previousEndpoint.Value;

            AddPolyline(root.transform, owner, points, color, thickness, depth, extraRenderers);

            if (points.Count > 0)
                previousEndpoint = points[points.Count - 1];
        }

        if (root.transform.childCount == 0)
        {
            Object.Destroy(root);
            return null;
        }

        return root;
    }

    private static List<NoteTechniqueSegmentData> GetRenderableSegments(NoteData note)
    {
        List<NoteTechniqueSegmentData> segments = new List<NoteTechniqueSegmentData>();
        if (note.techniqueSegments != null && note.techniqueSegments.Count > 0)
        {
            List<NoteTechniqueSegmentData> orderedSegments = note.techniqueSegments
                .OrderBy(segment => segment.startOffset)
                .ToList();

            for (int i = 0; i < orderedSegments.Count; i++)
            {
                NoteTechniqueSegmentData segment = orderedSegments[i];
                if (IsRenderableBendSegment(segment) || IsZeroBendSustainAdjacentToBend(orderedSegments, i))
                    segments.Add(segment);
            }
        }

        if (segments.Count > 0)
            return segments;

        if (note.technique != NoteTechnique.Bend && note.bendStep <= 0.01f && !note.bendPreBend && !note.bendRelease)
            return segments;

        float duration = Mathf.Max(note.duration, note.bendVisualDuration, 0.12f);
        float bend = Mathf.Max(0.5f, note.bendStep);

        if (note.bendRelease)
        {
            if (!note.bendPreBend)
            {
                float midpoint = duration * 0.5f;
                segments.Add(new NoteTechniqueSegmentData(NoteTechniqueSegmentType.Bend, 0f, midpoint, note.fret, note.fret, 0f, bend));
                segments.Add(new NoteTechniqueSegmentData(NoteTechniqueSegmentType.Bend, midpoint, duration, note.fret, note.fret, bend, 0f));
                return segments;
            }

            segments.Add(new NoteTechniqueSegmentData(NoteTechniqueSegmentType.Bend, 0f, duration, note.fret, note.fret, bend, 0f));
            return segments;
        }

        if (note.bendPreBend)
        {
            segments.Add(new NoteTechniqueSegmentData(NoteTechniqueSegmentType.Sustain, 0f, duration, note.fret, note.fret, bend, bend));
            return segments;
        }

        segments.Add(new NoteTechniqueSegmentData(NoteTechniqueSegmentType.Bend, 0f, duration, note.fret, note.fret, 0f, bend));
        return segments;
    }

    private static bool IsRenderableBendSegment(NoteTechniqueSegmentData segment)
    {
        if (segment.type == NoteTechniqueSegmentType.Bend || segment.type == NoteTechniqueSegmentType.Vibrato)
            return true;

        if (segment.type != NoteTechniqueSegmentType.Sustain)
            return false;

        return Mathf.Abs(segment.startBend) > 0.01f || Mathf.Abs(segment.endBend) > 0.01f;
    }

    private static bool IsZeroBendSustainAdjacentToBend(List<NoteTechniqueSegmentData> orderedSegments, int index)
    {
        NoteTechniqueSegmentData segment = orderedSegments[index];
        if (segment.type != NoteTechniqueSegmentType.Sustain)
            return false;

        if (Mathf.Abs(segment.startBend) > 0.01f || Mathf.Abs(segment.endBend) > 0.01f)
            return false;

        return IsBendLikeNeighbor(orderedSegments, index - 1) || IsBendLikeNeighbor(orderedSegments, index + 1);
    }

    private static bool IsBendLikeNeighbor(List<NoteTechniqueSegmentData> orderedSegments, int index)
    {
        if (index < 0 || index >= orderedSegments.Count)
            return false;

        NoteTechniqueSegmentData neighbor = orderedSegments[index];
        if (neighbor.type == NoteTechniqueSegmentType.Bend || neighbor.type == NoteTechniqueSegmentType.Vibrato)
            return true;

        return neighbor.type == NoteTechniqueSegmentType.Sustain &&
               (Mathf.Abs(neighbor.startBend) > 0.01f || Mathf.Abs(neighbor.endBend) > 0.01f);
    }

    private static float EvaluateBendY(float noteCenterY, float lineSpacing, float bendSemitones)
    {
        float bend = Mathf.Max(0f, bendSemitones);
        if (bend <= 0.01f)
            return noteCenterY;

        return noteCenterY + (lineSpacing * (MinimumRaisedLiftInLines + (bend * LiftPerSemitoneInLines)));
    }

    private static float TimeToX(float time, float sectionStartTime, float sectionEndTime, float leftEdge, float usableWidth)
    {
        float normalized = Mathf.InverseLerp(sectionStartTime, Mathf.Max(sectionStartTime + 0.01f, sectionEndTime), time);
        return leftEdge + (normalized * usableWidth);
    }

    private static List<Vector3> BuildCurvePoints(NoteTechniqueSegmentType type, float startX, float endX, float startY, float endY, float z)
    {
        List<Vector3> points = new List<Vector3>(BendCurveSamples);
        if (Mathf.Abs(endX - startX) <= 0.001f)
        {
            points.Add(new Vector3(startX, startY, z));
            points.Add(new Vector3(endX + 0.001f, endY, z));
            return points;
        }

        if (type == NoteTechniqueSegmentType.Sustain || Mathf.Abs(endY - startY) <= 0.01f)
        {
            points.Add(new Vector3(startX, startY, z));
            points.Add(new Vector3(endX, endY, z));
            return points;
        }

        float xSpan = endX - startX;
        Vector3 p0 = new Vector3(startX, startY, z);
        Vector3 p3 = new Vector3(endX, endY, z);
        Vector3 p1;
        Vector3 p2;

        if (endY > startY)
        {
            float midRise = Mathf.Max(Mathf.Abs(endY - startY) * 0.48f, 0.05f);
            p1 = new Vector3(startX + (xSpan * 0.42f), startY, z);
            p2 = new Vector3(endX, endY - midRise, z);
        }
        else
        {
            float midDrop = Mathf.Max(Mathf.Abs(endY - startY) * 0.48f, 0.05f);
            p1 = new Vector3(startX, startY - midDrop, z);
            p2 = new Vector3(startX + (xSpan * 0.58f), endY, z);
        }

        for (int i = 0; i < BendCurveSamples; i++)
        {
            float t = i / (float)(BendCurveSamples - 1);
            points.Add(EvaluateCubicBezier(p0, p1, p2, p3, t));
        }

        return points;
    }

    private static List<Vector3> BuildVibratoPoints(float startX, float endX, float startY, float endY, float z, float durationSeconds, float lineSpacing)
    {
        List<Vector3> points = new List<Vector3>(VibratoCurveSamples);
        float cycles = Mathf.Clamp(durationSeconds * VibratoCyclesPerSecond, 1.5f, 4.5f);
        float amplitude = lineSpacing * VibratoAmplitudeInLines;
        for (int i = 0; i < VibratoCurveSamples; i++)
        {
            float t = i / (float)(VibratoCurveSamples - 1);
            float x = Mathf.Lerp(startX, endX, t);
            float y = Mathf.Lerp(startY, endY, t) + (Mathf.Sin(t * Mathf.PI * 2f * cycles) * amplitude);
            points.Add(new Vector3(x, y, z));
        }

        return points;
    }

    private static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return (u * u * u * p0) +
               (3f * u * u * t * p1) +
               (3f * u * t * t * p2) +
               (t * t * t * p3);
    }

    private static void AddPolyline(Transform parent, GuitarBridgeServer owner, List<Vector3> points, Color color, float thickness, float depth, List<Renderer> extraRenderers)
    {
        for (int i = 1; i < points.Count; i++)
            CreateLineSegment(parent, owner, points[i - 1], points[i], color, thickness, depth, extraRenderers);
    }

    private static void CreateLineSegment(Transform parent, GuitarBridgeServer owner, Vector3 start, Vector3 end, Color color, float thickness, float depth, List<Renderer> extraRenderers)
    {
        Vector3 delta = end - start;
        float length = delta.magnitude;
        if (length <= MinimumSegmentLength)
            return;

        GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
        segment.transform.SetParent(parent, false);
        segment.name = "TabBendSegment";
        segment.transform.position = (start + end) * 0.5f;
        segment.transform.rotation = Quaternion.FromToRotation(Vector3.right, delta.normalized);
        segment.transform.localScale = new Vector3(length, thickness, depth);

        Object.Destroy(segment.GetComponent<Collider>());
        Renderer renderer = segment.GetComponent<Renderer>();
        renderer.material = owner.CreateSharedTabsGlowMaterial(color, 1.15f);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        extraRenderers?.Add(renderer);
    }
}
