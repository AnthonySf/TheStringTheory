using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class RocksmithTechniqueSegmentNormalizer
{
    public static List<NoteTechniqueSegmentData> BuildNormalizedTechniqueSegments(RocksmithCachedNoteData source)
    {
        List<NoteTechniqueSegmentData> segments = null;
        if (source?.techniqueSegments != null && source.techniqueSegments.Count > 0)
        {
            segments = new List<NoteTechniqueSegmentData>(source.techniqueSegments.Count);
            for (int segmentIndex = 0; segmentIndex < source.techniqueSegments.Count; segmentIndex++)
            {
                RocksmithCachedTechniqueSegmentData segment = source.techniqueSegments[segmentIndex];
                if (segment == null)
                    continue;

                segments.Add(new NoteTechniqueSegmentData(
                    (NoteTechniqueSegmentType)Mathf.Clamp(segment.type, 0, (int)NoteTechniqueSegmentType.Vibrato),
                    segment.startOffset,
                    segment.endOffset,
                    segment.startFret,
                    segment.endFret,
                    segment.startBend,
                    segment.endBend));
            }
        }

        segments = NormalizeStaticBendSegments(segments);
        return NormalizeRocksmithTechniqueSegments(source, segments);
    }

    private static List<NoteTechniqueSegmentData> NormalizeRocksmithTechniqueSegments(
        RocksmithCachedNoteData source,
        List<NoteTechniqueSegmentData> segments)
    {
        segments = EnsureFlagTechniqueSegments(source, segments);
        bool wantsBendVisual =
            source != null &&
            ((NoteTechnique)Mathf.Clamp(source.technique, 0, (int)NoteTechnique.Vibrato) == NoteTechnique.Bend ||
             source.bendStep > 0.01f ||
             source.bendPreBend ||
             source.bendRelease);
        if (!wantsBendVisual)
            return RemoveFlatSustainUnderExpressiveSegments(segments);

        if (source.bendPoints != null && source.bendPoints.Count > 0)
            return RemoveFlatSustainUnderExpressiveSegments(BuildBendTechniqueSegmentsFromPoints(source, segments));

        if (HasRenderableBendTechniqueSegments(segments))
            return RemoveFlatSustainUnderExpressiveSegments(segments);

        return RemoveFlatSustainUnderExpressiveSegments(BuildFallbackBendTechniqueSegments(source, segments));
    }

    private static List<NoteTechniqueSegmentData> EnsureFlagTechniqueSegments(
        RocksmithCachedNoteData source,
        List<NoteTechniqueSegmentData> segments)
    {
        if (source == null || !source.hasVibrato || HasTechniqueSegment(segments, NoteTechniqueSegmentType.Vibrato))
            return segments;

        List<NoteTechniqueSegmentData> result = segments != null
            ? new List<NoteTechniqueSegmentData>(segments)
            : new List<NoteTechniqueSegmentData>();
        float duration = Mathf.Max(0.05f, source.duration);
        int fret = Mathf.Max(0, source.fret);
        result.Add(new NoteTechniqueSegmentData(
            NoteTechniqueSegmentType.Vibrato,
            0f,
            duration,
            fret,
            fret,
            0f,
            0f));
        return result;
    }

    private static bool HasTechniqueSegment(List<NoteTechniqueSegmentData> segments, NoteTechniqueSegmentType type)
    {
        if (segments == null)
            return false;

        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i].type == type)
                return true;
        }

        return false;
    }

    private static bool HasRenderableBendTechniqueSegments(List<NoteTechniqueSegmentData> segments)
    {
        if (segments == null || segments.Count == 0)
            return false;

        for (int i = 0; i < segments.Count; i++)
        {
            NoteTechniqueSegmentData segment = segments[i];
            if (segment.endOffset <= segment.startOffset + 0.0001f)
                continue;

            if (segment.type == NoteTechniqueSegmentType.Bend)
            {
                if (Mathf.Abs(segment.endBend - segment.startBend) > 0.01f)
                    return true;
            }
            else if ((segment.type == NoteTechniqueSegmentType.Sustain || segment.type == NoteTechniqueSegmentType.Vibrato) &&
                     (Mathf.Abs(segment.startBend) > 0.01f || Mathf.Abs(segment.endBend) > 0.01f))
            {
                return true;
            }
        }

        return false;
    }

    private static List<NoteTechniqueSegmentData> NormalizeStaticBendSegments(List<NoteTechniqueSegmentData> segments)
    {
        if (segments == null || segments.Count == 0)
            return segments;

        List<NoteTechniqueSegmentData> result = null;
        for (int i = 0; i < segments.Count; i++)
        {
            NoteTechniqueSegmentData segment = segments[i];
            bool isStaticBentBend =
                segment.type == NoteTechniqueSegmentType.Bend &&
                Mathf.Abs(segment.endBend - segment.startBend) <= 0.01f &&
                (Mathf.Abs(segment.startBend) > 0.01f ||
                 Mathf.Abs(segment.endBend) > 0.01f);
            if (!isStaticBentBend)
            {
                result?.Add(segment);
                continue;
            }

            result ??= segments.Take(i).ToList();
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Sustain,
                segment.startOffset,
                segment.endOffset,
                segment.startFret,
                segment.endFret,
                segment.startBend,
                segment.endBend));
        }

        return result ?? segments;
    }

    private static List<NoteTechniqueSegmentData> BuildFallbackBendTechniqueSegments(
        RocksmithCachedNoteData source,
        List<NoteTechniqueSegmentData> existingSegments)
    {
        List<NoteTechniqueSegmentData> result = new List<NoteTechniqueSegmentData>();
        if (existingSegments != null)
        {
            for (int i = 0; i < existingSegments.Count; i++)
            {
                NoteTechniqueSegmentData segment = existingSegments[i];
                if ((segment.type == NoteTechniqueSegmentType.Slide ||
                     segment.type == NoteTechniqueSegmentType.Vibrato) &&
                    segment.endOffset > segment.startOffset + 0.0001f)
                {
                    result.Add(segment);
                }
            }
        }

        float duration = Mathf.Max(source.duration, source.bendVisualDuration, 0.12f);
        float bend = Mathf.Max(0.5f, source.bendStep);
        int fret = source.fret;

        if (source.bendRelease)
        {
            if (source.bendPreBend)
            {
                result.Add(new NoteTechniqueSegmentData(
                    NoteTechniqueSegmentType.Bend,
                    0f,
                    duration,
                    fret,
                    fret,
                    bend,
                    0f));
                return result;
            }

            float bendUpEnd = Mathf.Clamp(duration * 0.45f, 0.08f, Mathf.Max(0.08f, duration - 0.08f));
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Bend,
                0f,
                bendUpEnd,
                fret,
                fret,
                0f,
                bend));
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Bend,
                bendUpEnd,
                duration,
                fret,
                fret,
                bend,
                0f));
            return result;
        }

        if (source.bendPreBend)
        {
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Sustain,
                0f,
                duration,
                fret,
                fret,
                bend,
                bend));
            return result;
        }

        float riseEnd = Mathf.Clamp(duration * 0.24f, 0.08f, duration);
        if (duration > riseEnd + 0.04f)
        {
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Bend,
                0f,
                riseEnd,
                fret,
                fret,
                0f,
                bend));
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Sustain,
                riseEnd,
                duration,
                fret,
                fret,
                bend,
                bend));
        }
        else
        {
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Bend,
                0f,
                duration,
                fret,
                fret,
                0f,
                bend));
        }

        return result;
    }

    private static List<NoteTechniqueSegmentData> BuildBendTechniqueSegmentsFromPoints(
        RocksmithCachedNoteData source,
        List<NoteTechniqueSegmentData> existingSegments)
    {
        List<RocksmithCachedBendPointData> points = source.bendPoints
            .Where(point => point != null)
            .OrderBy(point => point.timeSeconds)
            .ToList();
        List<NoteTechniqueSegmentData> result = new List<NoteTechniqueSegmentData>();
        float duration = Mathf.Max(source.duration, source.bendVisualDuration, 0.12f);
        if (existingSegments != null)
        {
            for (int i = 0; i < existingSegments.Count; i++)
            {
                NoteTechniqueSegmentData segment = existingSegments[i];
                if (segment.endOffset <= segment.startOffset + 0.0001f)
                    continue;

                if (segment.type == NoteTechniqueSegmentType.Bend)
                    continue;

                if ((segment.type == NoteTechniqueSegmentType.Sustain || segment.type == NoteTechniqueSegmentType.Vibrato) &&
                    (Mathf.Abs(segment.startBend) > 0.01f || Mathf.Abs(segment.endBend) > 0.01f))
                {
                    if (segment.type == NoteTechniqueSegmentType.Vibrato)
                    {
                        float startOffset = Mathf.Clamp(segment.startOffset, 0f, duration);
                        float endOffset = Mathf.Clamp(segment.endOffset, 0f, duration);
                        float targetBend = Mathf.Max(Mathf.Abs(segment.startBend), Mathf.Abs(segment.endBend));
                        startOffset = Mathf.Max(startOffset, FindFirstBendPointOffsetAtOrAbove(points, targetBend, duration));
                        if (endOffset > startOffset + 0.0001f)
                        {
                            result.Add(new NoteTechniqueSegmentData(
                                NoteTechniqueSegmentType.Vibrato,
                                startOffset,
                                endOffset,
                                segment.startFret,
                                segment.endFret,
                                segment.startBend,
                                segment.endBend));
                        }
                    }

                    continue;
                }

                result.Add(segment);
            }
        }

        if (points.Count == 0)
            return BuildFallbackBendTechniqueSegments(source, result);

        int fret = source.fret;
        bool startsWithPreBend = source.bendPreBend ||
                                 (points[0].timeSeconds <= 0.001f && Mathf.Abs(points[0].step) > 0.01f);

        RocksmithCachedBendPointData firstPoint = points[0];
        float firstPointTime = Mathf.Clamp(firstPoint.timeSeconds, 0f, duration);
        if (firstPointTime > 0.0001f && Mathf.Abs(firstPoint.step) > 0.01f)
        {
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Bend,
                0f,
                firstPointTime,
                fret,
                fret,
                startsWithPreBend ? firstPoint.step : 0f,
                firstPoint.step));
        }

        for (int i = 1; i < points.Count; i++)
        {
            RocksmithCachedBendPointData previous = points[i - 1];
            RocksmithCachedBendPointData current = points[i];
            float startOffset = Mathf.Clamp(previous.timeSeconds, 0f, duration);
            float endOffset = Mathf.Clamp(current.timeSeconds, 0f, duration);
            if (endOffset <= startOffset + 0.0001f)
                continue;

            NoteTechniqueSegmentType segmentType =
                Mathf.Abs(current.step - previous.step) <= 0.01f
                    ? NoteTechniqueSegmentType.Sustain
                    : NoteTechniqueSegmentType.Bend;

            result.Add(new NoteTechniqueSegmentData(
                segmentType,
                startOffset,
                endOffset,
                fret,
                fret,
                previous.step,
                current.step));
        }

        RocksmithCachedBendPointData lastPoint = points[points.Count - 1];
        float lastPointTime = Mathf.Clamp(lastPoint.timeSeconds, 0f, duration);
        if (duration > lastPointTime + 0.0001f && Mathf.Abs(lastPoint.step) > 0.01f)
        {
            result.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Sustain,
                lastPointTime,
                duration,
                fret,
                fret,
                lastPoint.step,
                lastPoint.step));
        }

        return result;
    }

    private static float FindFirstBendPointOffsetAtOrAbove(
        IReadOnlyList<RocksmithCachedBendPointData> points,
        float targetBend,
        float duration)
    {
        if (points == null || points.Count == 0 || targetBend <= 0.01f)
            return 0f;

        for (int i = 0; i < points.Count; i++)
        {
            RocksmithCachedBendPointData point = points[i];
            if (point == null)
                continue;

            if (Mathf.Abs(point.step) >= targetBend - 0.01f)
                return Mathf.Clamp(point.timeSeconds, 0f, duration);
        }

        return 0f;
    }

    private static List<NoteTechniqueSegmentData> RemoveFlatSustainUnderExpressiveSegments(List<NoteTechniqueSegmentData> segments)
    {
        if (segments == null || segments.Count == 0)
            return segments;

        List<TechniqueSpan> expressiveSpans = null;
        List<TechniqueSpan> nonSustainExpressiveSpans = null;
        for (int i = 0; i < segments.Count; i++)
        {
            NoteTechniqueSegmentData segment = segments[i];
            if (segment.endOffset <= segment.startOffset + 0.0001f)
                continue;

            bool isExpressive = false;
            if (segment.type == NoteTechniqueSegmentType.Vibrato)
            {
                isExpressive = true;
            }
            else if (segment.type == NoteTechniqueSegmentType.Slide)
            {
                isExpressive = segment.startFret != segment.endFret;
            }
            else if (segment.type == NoteTechniqueSegmentType.Bend)
            {
                isExpressive =
                    Mathf.Abs(segment.endBend - segment.startBend) > 0.01f ||
                    Mathf.Abs(segment.startBend) > 0.01f ||
                    Mathf.Abs(segment.endBend) > 0.01f;
            }
            else if (segment.type == NoteTechniqueSegmentType.Sustain)
            {
                isExpressive =
                    Mathf.Abs(segment.startBend) > 0.01f ||
                    Mathf.Abs(segment.endBend) > 0.01f;
            }

            if (isExpressive)
            {
                expressiveSpans ??= new List<TechniqueSpan>();
                expressiveSpans.Add(new TechniqueSpan(segment.startOffset, segment.endOffset));
                if (segment.type != NoteTechniqueSegmentType.Sustain)
                {
                    nonSustainExpressiveSpans ??= new List<TechniqueSpan>();
                    nonSustainExpressiveSpans.Add(new TechniqueSpan(segment.startOffset, segment.endOffset));
                }
            }
        }

        if (expressiveSpans == null || expressiveSpans.Count == 0)
            return segments;

        List<TechniqueSpan> mergedExpressiveSpans = MergeTechniqueSpans(expressiveSpans);
        List<TechniqueSpan> mergedNonSustainExpressiveSpans = MergeTechniqueSpans(nonSustainExpressiveSpans);

        List<NoteTechniqueSegmentData> filtered = new List<NoteTechniqueSegmentData>(segments.Count + mergedExpressiveSpans.Count);
        for (int i = 0; i < segments.Count; i++)
        {
            NoteTechniqueSegmentData segment = segments[i];
            bool isFlatSustain =
                segment.type == NoteTechniqueSegmentType.Sustain &&
                Mathf.Abs(segment.startBend) <= 0.01f &&
                Mathf.Abs(segment.endBend) <= 0.01f &&
                segment.endOffset > segment.startOffset + 0.0001f;
            bool isConstantBentSustain =
                segment.type == NoteTechniqueSegmentType.Sustain &&
                Mathf.Abs(segment.startBend - segment.endBend) <= 0.01f &&
                (Mathf.Abs(segment.startBend) > 0.01f || Mathf.Abs(segment.endBend) > 0.01f) &&
                segment.endOffset > segment.startOffset + 0.0001f;

            if (!isFlatSustain && !isConstantBentSustain)
            {
                filtered.Add(segment);
                continue;
            }

            List<TechniqueSpan> spans = isFlatSustain
                ? mergedExpressiveSpans
                : mergedNonSustainExpressiveSpans;
            if (spans == null || spans.Count == 0)
            {
                filtered.Add(segment);
                continue;
            }

            float cursor = segment.startOffset;
            bool emittedSplit = false;
            for (int spanIndex = 0; spanIndex < spans.Count; spanIndex++)
            {
                float spanStart = spans[spanIndex].start;
                float spanEnd = spans[spanIndex].end;
                if (spanEnd <= cursor + 0.0001f)
                    continue;
                if (spanStart >= segment.endOffset - 0.0001f)
                    break;

                if (spanStart > cursor + 0.0001f)
                {
                    filtered.Add(new NoteTechniqueSegmentData(
                        NoteTechniqueSegmentType.Sustain,
                        cursor,
                        Mathf.Min(spanStart, segment.endOffset),
                        segment.startFret,
                        segment.endFret,
                        segment.startBend,
                        segment.endBend));
                    emittedSplit = true;
                }

                cursor = Mathf.Max(cursor, spanEnd);
                if (cursor >= segment.endOffset - 0.0001f)
                {
                    emittedSplit = true;
                    break;
                }
            }

            if (cursor < segment.endOffset - 0.0001f)
            {
                filtered.Add(new NoteTechniqueSegmentData(
                    NoteTechniqueSegmentType.Sustain,
                    cursor,
                    segment.endOffset,
                    segment.startFret,
                    segment.endFret,
                    segment.startBend,
                    segment.endBend));
                emittedSplit = true;
            }

            if (!emittedSplit)
            {
                filtered.Add(segment);
            }
        }

        return filtered
            .OrderBy(segment => segment.startOffset)
            .ThenBy(segment => segment.endOffset)
            .ToList();
    }

    private struct TechniqueSpan
    {
        public float start;
        public float end;

        public TechniqueSpan(float start, float end)
        {
            this.start = start;
            this.end = end;
        }
    }

    private static List<TechniqueSpan> MergeTechniqueSpans(List<TechniqueSpan> spans)
    {
        if (spans == null || spans.Count == 0)
            return null;

        spans.Sort((left, right) => left.start.CompareTo(right.start));
        List<TechniqueSpan> merged = new List<TechniqueSpan>(spans.Count);
        for (int i = 0; i < spans.Count; i++)
        {
            TechniqueSpan span = spans[i];
            if (merged.Count == 0)
            {
                merged.Add(span);
                continue;
            }

            TechniqueSpan current = merged[merged.Count - 1];
            if (span.start <= current.end + 0.0001f)
                merged[merged.Count - 1] = new TechniqueSpan(current.start, Mathf.Max(current.end, span.end));
            else
                merged.Add(span);
        }

        return merged;
    }

}
