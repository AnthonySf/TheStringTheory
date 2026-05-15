using System;
using System.Collections.Generic;
using System.Linq;
using AlphaTab.Model;
using UnityEngine;

internal static class AlphaTabGpLoader
{
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public static string TryReadTitle(string filePath)
    {
        AlphaTabGpScoreData data = AlphaTabGpScoreCache.GetOrLoad(filePath);
        return data?.score?.Title;
    }

    public static string TryReadArtist(string filePath)
    {
        AlphaTabGpScoreData data = AlphaTabGpScoreCache.GetOrLoad(filePath);
        return data?.score?.Artist;
    }

    public static List<MusicXmlLoader.MusicXmlPartSummary> GetPartSummaries(string filePath)
    {
        AlphaTabGpScoreData data = AlphaTabGpScoreCache.GetOrLoad(filePath);
        if (data == null)
            return new List<MusicXmlLoader.MusicXmlPartSummary>();

        List<MusicXmlLoader.MusicXmlPartSummary> summaries = new List<MusicXmlLoader.MusicXmlPartSummary>(data.tracks.Count);
        for (int i = 0; i < data.tracks.Count; i++)
        {
            AlphaTabGpTrackContext track = data.tracks[i];
            List<AlphaTabGpMatchedNote> notes = data.matchedNotesByTrack.TryGetValue(track.index, out List<AlphaTabGpMatchedNote> matched)
                ? matched
                : new List<AlphaTabGpMatchedNote>();

            int noteCount = notes.Count;
            int tabCount = notes.Count(note => note.isStringed);
            int score = noteCount + (tabCount * 20) + ScoreTrackName(track.name, track.isPercussion);
            summaries.Add(new MusicXmlLoader.MusicXmlPartSummary
            {
                Index = track.index,
                PartId = track.partId,
                Name = track.name,
                NoteCount = noteCount,
                TabCount = tabCount,
                Score = score,
                StringTuningPitches = track.stringTuningPitches != null ? (int[])track.stringTuningPitches.Clone() : null,
                TuningDisplayName = string.IsNullOrWhiteSpace(track.tuningDisplayName)
                    ? StringTuningUtils.FormatTuningDisplayName(track.stringTuningPitches)
                    : track.tuningDisplayName
            });
        }

        return summaries;
    }

    public static List<NoteData> LoadSong(string filePath, int targetPartIndex = -1)
    {
        AlphaTabGpScoreData data = AlphaTabGpScoreCache.GetOrLoad(filePath);
        if (data == null || data.tracks.Count == 0)
            return new List<NoteData>();

        List<MusicXmlLoader.MusicXmlPartSummary> summaries = GetPartSummaries(filePath);
        int chosenTrackIndex = (targetPartIndex >= 0 && targetPartIndex < data.tracks.Count)
            ? targetPartIndex
            : ChooseBestTrack(summaries);

        List<AlphaTabGpMatchedNote> selected = data.matchedNotesByTrack.TryGetValue(chosenTrackIndex, out List<AlphaTabGpMatchedNote> matched)
            ? matched.OrderBy(note => note.startTick).ThenBy(note => note.stringIdx).ToList()
            : new List<AlphaTabGpMatchedNote>();
        if (selected.Count == 0)
            return new List<NoteData>();

        Dictionary<Note, AlphaTabGpMatchedNote> matchedBySource = selected
            .Where(note => note.sourceNote != null)
            .GroupBy(note => note.sourceNote)
            .ToDictionary(group => group.Key, group => group.First());

        Dictionary<Note, int> noteIdBySource = new Dictionary<Note, int>();
        for (int i = 0; i < selected.Count; i++)
        {
            if (selected[i].sourceNote != null)
                noteIdBySource[selected[i].sourceNote] = i;
        }

        List<NoteData> result = new List<NoteData>(selected.Count);
        int currentChordId = -1;
        long previousChordTick = long.MinValue;

        for (int i = 0; i < selected.Count; i++)
        {
            AlphaTabGpMatchedNote note = selected[i];
            if (note.startTick != previousChordTick)
            {
                currentChordId++;
                previousChordTick = note.startTick;
            }

            float startSeconds = (float)TickToSeconds(note.startTick, data.tempoPoints, data.midiDivision);
            float endSeconds = (float)TickToSeconds(note.endTick, data.tempoPoints, data.midiDivision);
            float durationSeconds = Mathf.Max(0f, endSeconds - startSeconds);

            Note source = note.sourceNote;
            NoteTechnique technique = NoteTechnique.None;
            int slideTargetFret = -1;
            float bendStep = 0f;
            bool isLegato = false;
            bool requiresPluck = true;
            int linkedFromNoteId = -1;
            bool bendPreBend = false;
            bool bendRelease = false;
            float bendVisualStartTime = -1f;
            float bendVisualDuration = 0f;
            bool isMuted = source != null && source.IsDead;
            List<NoteTechniqueSegmentData> segments = BuildTechniqueSegments(note, matchedBySource, durationSeconds, data.tempoPoints, data.midiDivision);

            if (segments != null && segments.Count > 0)
            {
                NoteTechniqueSegmentData firstBend = segments.FirstOrDefault(segment => segment.type == NoteTechniqueSegmentType.Bend);
                if (!firstBend.Equals(default(NoteTechniqueSegmentData)) || segments.Any(segment => segment.type == NoteTechniqueSegmentType.Bend))
                {
                    technique = NoteTechnique.Bend;
                    float maxBend = 0f;
                    float minBend = 0f;
                    float bendStart = float.MaxValue;
                    float bendEnd = 0f;
                    bool hasRelease = false;
                    bool hasPreBend = false;

                    for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
                    {
                        NoteTechniqueSegmentData segment = segments[segmentIndex];
                        if (segment.type != NoteTechniqueSegmentType.Bend)
                            continue;

                        bendStart = Mathf.Min(bendStart, segment.startOffset);
                        bendEnd = Mathf.Max(bendEnd, segment.endOffset);
                        maxBend = Mathf.Max(maxBend, segment.startBend, segment.endBend);
                        minBend = Mathf.Min(minBend, segment.startBend, segment.endBend);
                        hasRelease |= segment.endBend < segment.startBend - 0.01f;
                        hasPreBend |= segment.startOffset <= 0.0005f && Mathf.Abs(segment.startBend) > 0.01f;
                    }

                    bendStep = Mathf.Max(Mathf.Abs(maxBend), Mathf.Abs(minBend));
                    bendPreBend = hasPreBend;
                    bendRelease = hasRelease;
                    bendVisualStartTime = bendStart < float.MaxValue ? bendStart : -1f;
                    bendVisualDuration = bendEnd > bendStart ? bendEnd - bendStart : 0f;
                }
            }

            if (source != null && source.IsHammerPullDestination && source.HammerPullOrigin != null)
            {
                AlphaTabGpMatchedNote origin = FindRelatedNote(source.HammerPullOrigin, matchedBySource);
                if (origin != null)
                {
                    linkedFromNoteId = noteIdBySource.TryGetValue(origin.sourceNote, out int originId) ? originId : -1;
                    isLegato = true;
                    requiresPluck = false;
                }
            }

            if (source != null && source.SlideTarget != null)
            {
                AlphaTabGpMatchedNote slideTarget = FindRelatedNote(source.SlideTarget, matchedBySource);
                if (slideTarget != null)
                {
                    technique = NoteTechnique.Slide;
                    slideTargetFret = slideTarget.fret;
                }
            }

            if (technique == NoteTechnique.None && segments != null && segments.Any(segment => segment.type == NoteTechniqueSegmentType.Vibrato))
                technique = NoteTechnique.Vibrato;

            result.Add(new NoteData(
                i,
                startSeconds,
                durationSeconds,
                note.stringIdx,
                note.fret,
                note.NoteName(),
                currentChordId,
                technique,
                slideTargetFret,
                bendStep,
                isLegato,
                requiresPluck,
                linkedFromNoteId,
                bendPreBend,
                bendRelease,
                bendVisualStartTime,
                bendVisualDuration,
                segments,
                isMuted));
        }

        ApplyLegatoVisualLinks(result);
        return result;
    }

    internal static double TickToSeconds(long targetTick, List<AlphaTabGpTempoPoint> tempoPoints, double division)
    {
        if (tempoPoints == null || tempoPoints.Count == 0 || division <= 0.0001)
            return 0.0;

        double totalSeconds = 0.0;
        long previousTick = 0;
        double currentBpm = Math.Max(1.0, tempoPoints[0].bpm);

        for (int i = 0; i < tempoPoints.Count; i++)
        {
            AlphaTabGpTempoPoint point = tempoPoints[i];
            if (point.tick <= previousTick)
            {
                currentBpm = Math.Max(1.0, point.bpm);
                continue;
            }

            if (point.tick > targetTick)
                break;

            totalSeconds += ((point.tick - previousTick) / division) * (60.0 / currentBpm);
            previousTick = point.tick;
            currentBpm = Math.Max(1.0, point.bpm);
        }

        if (targetTick > previousTick)
            totalSeconds += ((targetTick - previousTick) / division) * (60.0 / currentBpm);

        return totalSeconds;
    }

    internal static List<GeneratedPlaybackPitchPoint> BuildPitchCurve(Note note)
    {
        List<GeneratedPlaybackPitchPoint> curve = new List<GeneratedPlaybackPitchPoint>();
        if (note == null || !note.HasBend || note.BendPoints == null || note.BendPoints.Count == 0)
        {
            curve.Add(new GeneratedPlaybackPitchPoint { normalizedTime = 0f, semitoneOffset = 0f });
            curve.Add(new GeneratedPlaybackPitchPoint { normalizedTime = 1f, semitoneOffset = 0f });
            return curve;
        }

        float initialOffset = (float)note.InitialBendValue / 4f;
        AddOrReplacePitchPoint(curve, 0f, initialOffset);

        for (int i = 0; i < note.BendPoints.Count; i++)
        {
            BendPoint point = note.BendPoints[i];
            AddOrReplacePitchPoint(curve, (float)point.Offset / 60f, (float)point.Value / 4f);
        }

        if (curve[curve.Count - 1].normalizedTime < 0.9995f)
        {
            curve.Add(new GeneratedPlaybackPitchPoint
            {
                normalizedTime = 1f,
                semitoneOffset = curve[curve.Count - 1].semitoneOffset
            });
        }

        return NormalizeSegmentCurve(curve);
    }

    internal static float ResolveVibratoDelayNormalized(Note source, List<GeneratedPlaybackPitchPoint> pitchCurve)
    {
        if (source == null)
            return 0f;

        float durationSeconds = 1f;
        List<NoteTechniqueSegmentData> synthesizedSegments = BuildTechniqueSegmentsForTiming(source, pitchCurve, durationSeconds);
        float startSeconds = ResolveVibratoStartSeconds(durationSeconds, synthesizedSegments);
        return Mathf.Clamp01(startSeconds / durationSeconds);
    }

    internal static int CalculatePitchCurveRange(List<GeneratedPlaybackPitchPoint> curve)
    {
        if (curve == null || curve.Count == 0)
            return 0;

        float maxOffset = 0f;
        for (int i = 0; i < curve.Count; i++)
            maxOffset = Mathf.Max(maxOffset, Mathf.Abs(curve[i].semitoneOffset));

        return Mathf.CeilToInt(maxOffset);
    }

    internal static bool TryNormalizePreBendAttackPitch(ref int midiNote, ref float pitchPreRollSeconds, List<GeneratedPlaybackPitchPoint> pitchCurve)
    {
        if (pitchCurve == null || pitchCurve.Count < 2)
            return false;

        float startOffset = pitchCurve[0].semitoneOffset;
        if (Mathf.Abs(startOffset) < 0.5f)
            return false;

        int roundedShift = Mathf.RoundToInt(startOffset);
        if (Mathf.Abs(startOffset - roundedShift) > 0.12f || roundedShift == 0)
            return false;

        midiNote = Mathf.Clamp(midiNote + roundedShift, 0, 127);
        for (int i = 0; i < pitchCurve.Count; i++)
            pitchCurve[i].semitoneOffset -= roundedShift;

        pitchPreRollSeconds = 0f;
        return true;
    }

    internal static void ApplyLegatoTransition(GeneratedPlaybackNoteEvent target, AlphaTabGpMatchedNote previous, GeneratedLegatoTransitionKind kind)
    {
        target.legatoTransitionKind = kind;
        float sourceRelativeOffset = previous.midiNote - target.midiNote;
        float absoluteInterval = Mathf.Abs(sourceRelativeOffset);
        bool shouldApplyPitchGlide = kind == GeneratedLegatoTransitionKind.Slide ||
                                     (absoluteInterval <= 2.5f && (kind == GeneratedLegatoTransitionKind.HammerOn || kind == GeneratedLegatoTransitionKind.PullOff));

        switch (kind)
        {
            case GeneratedLegatoTransitionKind.Slide:
                target.attackVelocityScale *= 0.90f;
                break;
            case GeneratedLegatoTransitionKind.HammerOn:
                target.attackVelocityScale *= 0.82f;
                break;
            case GeneratedLegatoTransitionKind.PullOff:
                target.attackVelocityScale *= 0.78f;
                break;
        }

        if (!shouldApplyPitchGlide)
            return;

        float transitionEndNormalized = kind == GeneratedLegatoTransitionKind.Slide ? 0.32f : 0.035f;
        float targetStartOffset = GetPitchCurveStartOffset(target.pitchCurve);
        target.pitchCurve = PrependIntroTransition(target.pitchCurve, sourceRelativeOffset, targetStartOffset, transitionEndNormalized);
        target.pitchBendRangeSemitones = Mathf.Max(target.pitchBendRangeSemitones, CalculatePitchCurveRange(target.pitchCurve));
    }

    private static NoteTechniqueSegmentData? FindFirstBendSegment(List<NoteTechniqueSegmentData> segments)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i].type == NoteTechniqueSegmentType.Bend)
                return segments[i];
        }

        return null;
    }

    private static AlphaTabGpMatchedNote FindRelatedNote(Note related, Dictionary<Note, AlphaTabGpMatchedNote> matchedBySource)
    {
        if (related == null)
            return null;

        matchedBySource.TryGetValue(related, out AlphaTabGpMatchedNote matched);
        return matched;
    }

    private static void ApplyLegatoVisualLinks(List<NoteData> notes)
    {
        if (notes == null || notes.Count == 0)
            return;

        Dictionary<int, int> noteIndexById = new Dictionary<int, int>(notes.Count);
        for (int i = 0; i < notes.Count; i++)
            noteIndexById[notes[i].id] = i;

        for (int i = 0; i < notes.Count; i++)
        {
            NoteData destination = notes[i];
            if (!destination.isLegato || destination.linkedFromNoteId < 0)
                continue;

            if (!noteIndexById.TryGetValue(destination.linkedFromNoteId, out int originIndex))
                continue;

            NoteData origin = notes[originIndex];
            if (origin.stringIdx != destination.stringIdx)
                continue;

            NoteTechnique legatoTechnique = destination.fret >= origin.fret
                ? NoteTechnique.HammerOn
                : NoteTechnique.PullOff;

            if (origin.technique == NoteTechnique.None)
                origin.technique = legatoTechnique;

            origin.slideTargetFret = destination.fret;
            origin.duration = Mathf.Max(origin.duration, Mathf.Max(0.05f, destination.time - origin.time));
            notes[originIndex] = origin;
        }
    }

    private static List<NoteTechniqueSegmentData> BuildTechniqueSegments(
        AlphaTabGpMatchedNote note,
        Dictionary<Note, AlphaTabGpMatchedNote> matchedBySource,
        float durationSeconds,
        List<AlphaTabGpTempoPoint> tempoPoints,
        double midiDivision)
    {
        List<NoteTechniqueSegmentData> segments = new List<NoteTechniqueSegmentData>();
        Note source = note.sourceNote;
        float safeDurationSeconds = Mathf.Max(0.0001f, durationSeconds);

        if (source != null && source.HasBend && source.BendPoints != null && source.BendPoints.Count > 0)
        {
            List<GeneratedPlaybackPitchPoint> pitchCurve = BuildPitchCurve(source);
            for (int i = 1; i < pitchCurve.Count; i++)
            {
                GeneratedPlaybackPitchPoint previous = pitchCurve[i - 1];
                GeneratedPlaybackPitchPoint current = pitchCurve[i];
                segments.Add(new NoteTechniqueSegmentData(
                    NoteTechniqueSegmentType.Bend,
                    previous.normalizedTime * safeDurationSeconds,
                    current.normalizedTime * safeDurationSeconds,
                    note.fret,
                    note.fret,
                    previous.semitoneOffset,
                    current.semitoneOffset));
            }
        }

        if (source != null && source.SlideTarget != null)
        {
            AlphaTabGpMatchedNote slideTarget = FindRelatedNote(source.SlideTarget, matchedBySource);
            if (slideTarget != null)
            {
                float slideEndSeconds = Mathf.Max(
                    0.03f,
                    (float)(TickToSeconds(slideTarget.startTick, tempoPoints, midiDivision) - TickToSeconds(note.startTick, tempoPoints, midiDivision)));
                segments.Add(new NoteTechniqueSegmentData(
                    NoteTechniqueSegmentType.Slide,
                    0f,
                    Mathf.Min(slideEndSeconds, safeDurationSeconds),
                    note.fret,
                    slideTarget.fret,
                    0f,
                    0f));
            }
        }

        bool hasVibrato = source != null &&
                          (source.Vibrato != VibratoType.None || source.Beat.Vibrato != VibratoType.None);
        if (hasVibrato)
        {
            float vibratoStart = ResolveVibratoStartSeconds(safeDurationSeconds, segments);
            ResolveTechniqueStateAtTime(segments, Mathf.Clamp(vibratoStart, 0f, safeDurationSeconds), note.fret, out int vibratoFret, out float vibratoBend);

            segments.Add(new NoteTechniqueSegmentData(
                NoteTechniqueSegmentType.Vibrato,
                Mathf.Clamp(vibratoStart, 0f, safeDurationSeconds),
                safeDurationSeconds,
                vibratoFret,
                vibratoFret,
                vibratoBend,
                vibratoBend));
        }

        return segments.Count > 0 ? segments : null;
    }

    private static List<NoteTechniqueSegmentData> BuildTechniqueSegmentsForTiming(
        Note source,
        List<GeneratedPlaybackPitchPoint> pitchCurve,
        float durationSeconds)
    {
        List<NoteTechniqueSegmentData> segments = new List<NoteTechniqueSegmentData>();
        float safeDurationSeconds = Mathf.Max(0.0001f, durationSeconds);

        if (source != null && source.HasBend && pitchCurve != null && pitchCurve.Count > 1)
        {
            for (int i = 1; i < pitchCurve.Count; i++)
            {
                GeneratedPlaybackPitchPoint previous = pitchCurve[i - 1];
                GeneratedPlaybackPitchPoint current = pitchCurve[i];
                segments.Add(new NoteTechniqueSegmentData(
                    NoteTechniqueSegmentType.Bend,
                    previous.normalizedTime * safeDurationSeconds,
                    current.normalizedTime * safeDurationSeconds,
                    Mathf.Max(0, Mathf.RoundToInt((float)source.Fret)),
                    Mathf.Max(0, Mathf.RoundToInt((float)source.Fret)),
                    previous.semitoneOffset,
                    current.semitoneOffset));
            }
        }

        return segments;
    }

    private static float ResolveVibratoStartSeconds(float durationSeconds, List<NoteTechniqueSegmentData> segments)
    {
        float safeDurationSeconds = Mathf.Max(0.0001f, durationSeconds);
        if (segments == null || segments.Count == 0)
            return 0f;

        float latestMotionEnd = 0f;
        for (int i = 0; i < segments.Count; i++)
        {
            NoteTechniqueSegmentData segment = segments[i];
            if (!SegmentRepresentsMotion(segment))
                continue;

            latestMotionEnd = Mathf.Max(latestMotionEnd, segment.endOffset);
        }

        if (latestMotionEnd <= 0.0005f)
            return 0f;

        return Mathf.Clamp(latestMotionEnd, 0f, safeDurationSeconds);
    }

    private static bool SegmentRepresentsMotion(NoteTechniqueSegmentData segment)
    {
        switch (segment.type)
        {
            case NoteTechniqueSegmentType.Bend:
                return Mathf.Abs(segment.endBend - segment.startBend) > 0.01f;
            case NoteTechniqueSegmentType.Slide:
                return segment.endFret != segment.startFret;
            default:
                return false;
        }
    }

    private static void ResolveTechniqueStateAtTime(
        List<NoteTechniqueSegmentData> segments,
        float timeSeconds,
        int defaultFret,
        out int fret,
        out float bend)
    {
        fret = defaultFret;
        bend = 0f;

        if (segments == null || segments.Count == 0)
            return;

        NoteTechniqueSegmentData? activeSegment = null;
        NoteTechniqueSegmentData? latestCompletedSegment = null;
        float latestCompletedEnd = float.MinValue;

        for (int i = 0; i < segments.Count; i++)
        {
            NoteTechniqueSegmentData segment = segments[i];
            if (segment.startOffset - 0.0001f <= timeSeconds && segment.endOffset + 0.0001f >= timeSeconds)
            {
                activeSegment = segment;
                break;
            }

            if (segment.endOffset <= timeSeconds + 0.0001f && segment.endOffset > latestCompletedEnd)
            {
                latestCompletedSegment = segment;
                latestCompletedEnd = segment.endOffset;
            }
        }

        if (activeSegment.HasValue)
        {
            NoteTechniqueSegmentData segment = activeSegment.Value;
            float duration = Mathf.Max(0.0001f, segment.endOffset - segment.startOffset);
            float t = Mathf.Clamp01((timeSeconds - segment.startOffset) / duration);
            fret = Mathf.RoundToInt(Mathf.Lerp(segment.startFret, segment.endFret, t));
            bend = Mathf.Lerp(segment.startBend, segment.endBend, t);
            return;
        }

        if (latestCompletedSegment.HasValue)
        {
            NoteTechniqueSegmentData segment = latestCompletedSegment.Value;
            fret = segment.endFret;
            bend = segment.endBend;
        }
    }

    private static List<GeneratedPlaybackPitchPoint> NormalizeSegmentCurve(List<GeneratedPlaybackPitchPoint> curve)
    {
        List<GeneratedPlaybackPitchPoint> normalized = ClonePitchCurve(curve);
        if (normalized.Count == 0)
        {
            normalized.Add(new GeneratedPlaybackPitchPoint { normalizedTime = 0f, semitoneOffset = 0f });
            normalized.Add(new GeneratedPlaybackPitchPoint { normalizedTime = 1f, semitoneOffset = 0f });
            return normalized;
        }

        if (normalized[0].normalizedTime > 0.0005f)
        {
            normalized.Insert(0, new GeneratedPlaybackPitchPoint
            {
                normalizedTime = 0f,
                semitoneOffset = normalized[0].semitoneOffset
            });
        }
        else
        {
            normalized[0].normalizedTime = 0f;
        }

        if (normalized[normalized.Count - 1].normalizedTime < 0.9995f)
        {
            normalized.Add(new GeneratedPlaybackPitchPoint
            {
                normalizedTime = 1f,
                semitoneOffset = normalized[normalized.Count - 1].semitoneOffset
            });
        }
        else
        {
            GeneratedPlaybackPitchPoint last = normalized[normalized.Count - 1];
            last.normalizedTime = 1f;
            normalized[normalized.Count - 1] = last;
        }

        List<GeneratedPlaybackPitchPoint> deduped = new List<GeneratedPlaybackPitchPoint>(normalized.Count);
        for (int i = 0; i < normalized.Count; i++)
            AddOrReplacePitchPoint(deduped, normalized[i].normalizedTime, normalized[i].semitoneOffset);

        return deduped;
    }

    private static List<GeneratedPlaybackPitchPoint> ClonePitchCurve(List<GeneratedPlaybackPitchPoint> curve)
    {
        List<GeneratedPlaybackPitchPoint> clone = new List<GeneratedPlaybackPitchPoint>();
        if (curve == null)
            return clone;

        for (int i = 0; i < curve.Count; i++)
        {
            clone.Add(new GeneratedPlaybackPitchPoint
            {
                normalizedTime = curve[i].normalizedTime,
                semitoneOffset = curve[i].semitoneOffset
            });
        }

        return clone;
    }

    private static List<GeneratedPlaybackPitchPoint> PrependIntroTransition(
        List<GeneratedPlaybackPitchPoint> originalCurve,
        float introStartOffset,
        float introTargetOffset,
        float transitionEndNormalized)
    {
        List<GeneratedPlaybackPitchPoint> baseCurve = NormalizeSegmentCurve(originalCurve);
        float clampedTransitionEnd = Mathf.Clamp(transitionEndNormalized, 0.02f, 0.85f);
        List<GeneratedPlaybackPitchPoint> result = new List<GeneratedPlaybackPitchPoint>(baseCurve.Count + 2)
        {
            new GeneratedPlaybackPitchPoint
            {
                normalizedTime = 0f,
                semitoneOffset = introStartOffset
            },
            new GeneratedPlaybackPitchPoint
            {
                normalizedTime = clampedTransitionEnd,
                semitoneOffset = introTargetOffset
            }
        };

        for (int i = 0; i < baseCurve.Count; i++)
        {
            GeneratedPlaybackPitchPoint point = baseCurve[i];
            float shiftedTime = clampedTransitionEnd + (point.normalizedTime * (1f - clampedTransitionEnd));
            AddOrReplacePitchPoint(result, shiftedTime, point.semitoneOffset);
        }

        if (result[result.Count - 1].normalizedTime < 0.9995f)
        {
            result.Add(new GeneratedPlaybackPitchPoint
            {
                normalizedTime = 1f,
                semitoneOffset = result[result.Count - 1].semitoneOffset
            });
        }

        return result;
    }

    private static float GetPitchCurveStartOffset(List<GeneratedPlaybackPitchPoint> pitchCurve)
    {
        if (pitchCurve == null || pitchCurve.Count == 0)
            return 0f;

        return NormalizeSegmentCurve(pitchCurve)[0].semitoneOffset;
    }

    private static void AddOrReplacePitchPoint(List<GeneratedPlaybackPitchPoint> target, float normalizedTime, float semitoneOffset)
    {
        float clampedTime = Mathf.Clamp01(normalizedTime);
        if (target.Count > 0 && Mathf.Abs(target[target.Count - 1].normalizedTime - clampedTime) <= 0.0005f)
        {
            GeneratedPlaybackPitchPoint point = target[target.Count - 1];
            point.normalizedTime = clampedTime;
            point.semitoneOffset = semitoneOffset;
            target[target.Count - 1] = point;
            return;
        }

        target.Add(new GeneratedPlaybackPitchPoint
        {
            normalizedTime = clampedTime,
            semitoneOffset = semitoneOffset
        });
    }

    private static int ChooseBestTrack(List<MusicXmlLoader.MusicXmlPartSummary> summaries)
    {
        int bestIndex = 0;
        int bestScore = int.MinValue;
        for (int i = 0; i < summaries.Count; i++)
        {
            if (summaries[i].Score > bestScore)
            {
                bestScore = summaries[i].Score;
                bestIndex = summaries[i].Index;
            }
        }

        return bestIndex;
    }

    private static int ScoreTrackName(string name, bool isPercussion)
    {
        string lower = (name ?? string.Empty).ToLowerInvariant();
        int score = 0;

        if (lower.Contains("guitar"))
            score += 160;
        if (lower.Contains("lead"))
            score += 120;
        if (lower.Contains("solo"))
            score += 100;
        if (lower.Contains("slash"))
            score += 80;
        if (lower.Contains("rhythm"))
            score += 40;
        if (lower.Contains("bass"))
            score += 20;
        if (lower.Contains("vocal") || lower.Contains("voice") || lower.Contains("drum") || lower.Contains("piano"))
            score -= 80;
        if (isPercussion)
            score -= 120;

        return score;
    }

    private static string GetNoteName(int midiNote)
    {
        return NoteNames[Mathf.Abs(midiNote) % 12];
    }

    private static string NoteName(this AlphaTabGpMatchedNote note)
    {
        return GetNoteName(note.midiNote);
    }
}
