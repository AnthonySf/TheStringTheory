using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AlphaTab;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Exporter;
using AlphaTab.Model;
using UnityEngine;

internal static class RocksmithAlphaTabScoreConverter
{
    private const bool UseGameplayAlphaTabSimplification = true;
    private const bool UseGameplayHiddenBendContinuations = false;
    private const int DefaultSlotsPerBeat = 8;
    private static readonly int[] SupportedSlotsPerBeat = { 8, 16, 32 };

    public static void WriteGp7(
        string outputGpPath,
        RocksmithCachedSongManifest manifest,
        RocksmithCachedArrangementSummary summary,
        RocksmithCachedArrangementPart part)
    {
        if (string.IsNullOrWhiteSpace(outputGpPath))
            throw new ArgumentException("Output GP path was empty.", nameof(outputGpPath));
        if (summary == null)
            throw new ArgumentNullException(nameof(summary));
        if (part == null)
            throw new ArgumentNullException(nameof(part));

        string directory = Path.GetDirectoryName(outputGpPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        RocksmithAlphaTabTimingSidecar timingSidecar;
        Score score = BuildScore(manifest, summary, part, outputGpPath, out timingSidecar);

        Settings settings = new Settings();
        score.Finish(settings);

        Gp7Exporter exporter = new Gp7Exporter();
        var data = exporter.Export(score, settings);
        byte[] bytes = new byte[(int)data.Length];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)data[(uint)i];

        File.WriteAllBytes(outputGpPath, bytes);

        string timingSidecarPath = $"{outputGpPath}.timing.json";
        timingSidecar.notationPath = outputGpPath;
        File.WriteAllText(timingSidecarPath, SerializeTimingSidecar(timingSidecar));
    }

    private static Score BuildScore(
        RocksmithCachedSongManifest manifest,
        RocksmithCachedArrangementSummary summary,
        RocksmithCachedArrangementPart part,
        string outputGpPath,
        out RocksmithAlphaTabTimingSidecar timingSidecar)
    {
        timingSidecar = new RocksmithAlphaTabTimingSidecar();

        int[] tuningPitches = ResolveTuningPitches(part, summary);
        string trackName = !string.IsNullOrWhiteSpace(summary.displayName)
            ? summary.displayName
            : (!string.IsNullOrWhiteSpace(part.displayName) ? part.displayName : "Track");

        Dictionary<int, float> effectiveEndTimes = BuildEffectiveEndTimes(part);
        List<MeasureInfo> measures = BuildMeasures(part, effectiveEndTimes);
        List<EventInfo> events = BuildEvents(part.notes, effectiveEndTimes);
        AssignVoices(events);
        NoteRenderContext noteRenderContext = BuildNoteRenderContext(part?.notes, effectiveEndTimes);
        List<List<EventSliceInfo>> measureSlices = BuildEventSlices(events, measures);
        if (UseGameplayAlphaTabSimplification)
            measureSlices = SimplifyGameplayMeasureSlices(measureSlices);

        int voiceCount = Math.Max(1, events.Count > 0 ? events.Max(evt => evt.voiceIndex) + 1 : 1);

        Score score = new Score
        {
            Title = manifest?.displayName ?? string.Empty,
            SubTitle = manifest?.subtitle ?? string.Empty,
            Artist = manifest?.artist ?? string.Empty,
            Album = manifest?.album ?? string.Empty,
            Tempo = measures.Count > 0 ? Mathf.Max(1f, measures[0].tempoBpm) : Mathf.Max(1f, part?.timing?.averageTempoBpm ?? 120f),
            Tab = string.Empty
        };

        Track track = BuildTrack(trackName, part, tuningPitches);
        Staff staff = BuildStaff(part, tuningPitches);
        score.AddTrack(track);
        track.AddStaff(staff);

        BuildState buildState = new BuildState();

        for (int measureIndex = 0; measureIndex < measures.Count; measureIndex++)
        {
            MeasureInfo measure = measures[measureIndex];
            Dictionary<int, List<EventSliceInfo>> slicesByVoice = measureSlices[measureIndex]
                .GroupBy(slice => Math.Max(0, slice.voiceIndex))
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.OrderBy(slice => slice.startTime).ToList());

            int resolvedSlotsPerBeat = ResolveSlotsPerBeatForMeasure(slicesByVoice.Values, measure);
            measure.SetSlotsPerBeat(resolvedSlotsPerBeat);

            MasterBar masterBar = BuildMasterBar(measureIndex, measures, measure);
            score.AddMasterBar(masterBar);

            Bar bar = BuildBar();
            staff.AddBar(bar);

            for (int voiceIndex = 0; voiceIndex < voiceCount; voiceIndex++)
            {
                Voice voice = new Voice();
                bar.AddVoice(voice);

                List<RocksmithAlphaTabTimingBeatEntry> measureTimingEntries = new List<RocksmithAlphaTabTimingBeatEntry>();
                List<BeatBuildInfo> beatInfos = BuildMeasureBeatInfos(
                    measureIndex,
                    measure,
                    slicesByVoice.TryGetValue(voiceIndex, out List<EventSliceInfo> voiceSlices) ? voiceSlices : null,
                    voiceIndex,
                    noteRenderContext,
                    measureTimingEntries);

                for (int beatIndex = 0; beatIndex < beatInfos.Count; beatIndex++)
                {
                    Beat beat = CreateBeat(beatInfos[beatIndex], buildState);
                    voice.AddBeat(beat);
                }

                timingSidecar.beats.AddRange(measureTimingEntries);
            }
        }

        ApplyLegatoLinks(buildState, noteRenderContext);
        timingSidecar.beats = timingSidecar.beats
            .OrderBy(entry => entry.startTime)
            .ThenBy(entry => entry.voiceIndex)
            .ToList();

        return score;
    }

    private static Track BuildTrack(string trackName, RocksmithCachedArrangementPart part, int[] tuningPitches)
    {
        PlaybackInformation playback = new PlaybackInformation
        {
            Program = Math.Clamp(part?.generatedPart?.sourceMidiProgram ?? 29, 0, 127),
            PrimaryChannel = ResolveMidiChannel(part?.generatedPart?.sourceMidiChannel, fallback: 0),
            SecondaryChannel = ResolveMidiChannel(part?.generatedPart?.sourceMidiChannel, fallback: 1),
            Volume = 15,
            Balance = 8
        };

        Track track = new Track
        {
            Name = trackName,
            ShortName = BuildTrackShortName(trackName),
            PlaybackInfo = playback,
            DefaultSystemsLayout = 0
        };

        return track;
    }

    private static Staff BuildStaff(RocksmithCachedArrangementPart part, int[] tuningPitches)
    {
        string tuningName = !string.IsNullOrWhiteSpace(part?.tuningDisplayName)
            ? part.tuningDisplayName
            : StringTuningUtils.FormatTuningDisplayName(tuningPitches);

        Tuning tuning = new Tuning(tuningName, tuningPitches.Select(value => (double)value).ToList(), false)
        {
            Name = tuningName
        };

        Staff staff = new Staff
        {
            ShowStandardNotation = false,
            ShowTablature = true,
            ShowSlash = false,
            ShowNumbered = false,
            StandardNotationLineCount = 5,
            Capo = Math.Max(0, part?.timing?.capo ?? 0),
            StringTuning = tuning
        };

        return staff;
    }

    private static MasterBar BuildMasterBar(int measureIndex, List<MeasureInfo> measures, MeasureInfo measure)
    {
        MasterBar masterBar = new MasterBar
        {
            TimeSignatureNumerator = Math.Max(1, measure.beatCount),
            TimeSignatureDenominator = 4,
            TimeSignatureCommon = measure.beatCount == 4,
            Start = measure.startTime
        };

        if (measureIndex == 0 || Math.Abs(measures[measureIndex - 1].tempoBpm - measure.tempoBpm) > 0.25f)
        {
            masterBar.TempoAutomations.Add(new Automation
            {
                Type = AutomationType.Tempo,
                Value = Math.Max(1f, measure.tempoBpm),
                RatioPosition = 0,
                IsLinear = false
            });
        }

        return masterBar;
    }

    private static Bar BuildBar()
    {
        return new Bar
        {
            Clef = Clef.Neutral
        };
    }

    private static Beat CreateBeat(BeatBuildInfo info, BuildState buildState)
    {
        Beat beat = new Beat
        {
            Duration = info.durationToken.duration,
            Dots = info.durationToken.dotCount,
            TupletNumerator = 1,
            TupletDenominator = 1,
            IsPalmMute = info.isPalmMute,
            IsLetRing = info.isLetRing,
            Vibrato = info.beatVibrato,
            TremoloSpeed = info.tremoloSpeed
        };

        if (info.isRest)
            return beat;

        for (int i = 0; i < info.notes.Count; i++)
        {
            NoteBuildInfo source = info.notes[i];
            Note note = new Note
            {
                String = source.stringNumber,
                Fret = source.fret,
                IsVisible = source.isVisible,
                IsDead = source.isDead,
                IsPalmMute = source.isPalmMute,
                IsLeftHandTapped = source.isTap,
                IsLetRing = source.isLetRing,
                Vibrato = source.vibrato,
                Accentuated = source.accentuation,
                HarmonicType = source.harmonicType,
                HarmonicValue = source.harmonicValue,
                BendType = source.bendType
            };

            if (source.bendPoints != null)
            {
                for (int bendIndex = 0; bendIndex < source.bendPoints.Count; bendIndex++)
                    note.AddBendPoint(source.bendPoints[bendIndex]);
            }

            beat.AddNote(note);

            CreatedNoteOccurrence occurrence = new CreatedNoteOccurrence
            {
                sourceNote = source.sourceNote,
                renderedNote = note,
                sourceEventId = info.sourceEventId,
                isAttackToken = source.isAttackToken,
                continuesFromPrevious = source.continuesFromPrevious,
                continuesToNext = source.continuesToNext
            };
            buildState.noteOccurrences.Add(occurrence);

            if (source.isAttackToken && source.sourceNote != null && !buildState.attackNoteBySourceId.ContainsKey(source.sourceNote.id))
                buildState.attackNoteBySourceId[source.sourceNote.id] = occurrence;

            if (source.sourceNote != null)
            {
                if (source.continuesFromPrevious &&
                    buildState.activeTieBySourceId.TryGetValue(source.sourceNote.id, out CreatedNoteOccurrence previousOccurrence) &&
                    previousOccurrence != null &&
                    previousOccurrence.renderedNote != null)
                {
                    previousOccurrence.renderedNote.TieDestination = note;
                    note.TieOrigin = previousOccurrence.renderedNote;
                    note.IsTieDestination = true;
                }

                if (source.continuesToNext)
                    buildState.activeTieBySourceId[source.sourceNote.id] = occurrence;
                else
                    buildState.activeTieBySourceId.Remove(source.sourceNote.id);
            }
        }

        return beat;
    }

    private static void ApplyLegatoLinks(BuildState buildState, NoteRenderContext context)
    {
        if (buildState == null || context == null)
            return;

        foreach (KeyValuePair<int, CreatedNoteOccurrence> pair in buildState.attackNoteBySourceId)
        {
            if (!context.notesById.TryGetValue(pair.Key, out RocksmithCachedNoteData destinationSource) ||
                destinationSource == null ||
                destinationSource.linkedFromNoteId < 0)
            {
                continue;
            }

            if (!buildState.attackNoteBySourceId.TryGetValue(destinationSource.linkedFromNoteId, out CreatedNoteOccurrence originOccurrence) ||
                originOccurrence?.renderedNote == null ||
                pair.Value?.renderedNote == null)
            {
                continue;
            }

            NoteTechnique technique = ResolveLegatoTechnique(destinationSource, context);
            switch (technique)
            {
                case NoteTechnique.HammerOn:
                case NoteTechnique.PullOff:
                    originOccurrence.renderedNote.IsHammerPullOrigin = true;
                    originOccurrence.renderedNote.HammerPullDestination = pair.Value.renderedNote;
                    pair.Value.renderedNote.HammerPullOrigin = originOccurrence.renderedNote;
                    break;
                case NoteTechnique.Slide:
                    originOccurrence.renderedNote.SlideTarget = pair.Value.renderedNote;
                    pair.Value.renderedNote.SlideOrigin = originOccurrence.renderedNote;
                    originOccurrence.renderedNote.SlideOutType = destinationSource.requiresPluck ? SlideOutType.Shift : SlideOutType.Legato;
                    break;
            }
        }
    }

    private static List<BeatBuildInfo> BuildMeasureBeatInfos(
        int masterBarIndex,
        MeasureInfo measure,
        List<EventSliceInfo> voiceSlices,
        int voiceIndex,
        NoteRenderContext noteRenderContext,
        List<RocksmithAlphaTabTimingBeatEntry> timingEntries)
    {
        int totalSlots = measure.TotalSlots;
        List<BeatBuildInfo> beats = new List<BeatBuildInfo>();

        if (voiceSlices == null || voiceSlices.Count == 0)
        {
            AppendRestSequence(beats, totalSlots, measure.DivisionsPerQuarter, timingEntries, measure.startTime, measure.endTime, voiceIndex, masterBarIndex);
            return beats;
        }

        List<QuantizedEvent> quantizedEvents = QuantizeEventsForMeasure(voiceSlices, measure, measure.DivisionsPerQuarter);
        int cursorSlot = 0;
        float cursorTime = measure.startTime;

        for (int i = 0; i < quantizedEvents.Count; i++)
        {
            QuantizedEvent current = quantizedEvents[i];
            if (current.startSlot > cursorSlot)
            {
                float restEndTime = Mathf.Clamp(current.sourceStartTime, cursorTime, measure.endTime);
                AppendRestSequence(beats, current.startSlot - cursorSlot, measure.DivisionsPerQuarter, timingEntries, cursorTime, restEndTime, voiceIndex, masterBarIndex);
                cursorTime = restEndTime;
            }

            AppendEventSequence(beats, current, measure.DivisionsPerQuarter, timingEntries, current.sourceStartTime, current.sourceEndTime, voiceIndex, masterBarIndex, noteRenderContext);
            cursorSlot = Math.Max(cursorSlot, current.endSlot);
            cursorTime = Mathf.Max(cursorTime, current.sourceEndTime);
        }

        if (cursorSlot < totalSlots)
            AppendRestSequence(beats, totalSlots - cursorSlot, measure.DivisionsPerQuarter, timingEntries, cursorTime, measure.endTime, voiceIndex, masterBarIndex);

        return beats;
    }

    private static void AppendRestSequence(
        List<BeatBuildInfo> beats,
        int slotCount,
        int slotsPerBeat,
        List<RocksmithAlphaTabTimingBeatEntry> timingEntries,
        float startTime,
        float endTime,
        int voiceIndex,
        int masterBarIndex)
    {
        List<DurationToken> tokens = DecomposeDuration(slotCount, slotsPerBeat);
        float cursorTime = startTime;
        int totalSlots = Math.Max(1, slotCount);
        int consumedSlots = 0;
        for (int i = 0; i < tokens.Count; i++)
        {
            DurationToken token = tokens[i];
            consumedSlots += token.slots;
            float nextTime = i == tokens.Count - 1
                ? endTime
                : Mathf.Lerp(startTime, endTime, consumedSlots / (float)totalSlots);
            nextTime = Math.Max(cursorTime + 0.001f, nextTime);

            beats.Add(new BeatBuildInfo
            {
                isRest = true,
                durationToken = token
            });

            timingEntries?.Add(new RocksmithAlphaTabTimingBeatEntry
            {
                startTime = cursorTime,
                endTime = nextTime,
                isRest = true,
                masterBarIndex = masterBarIndex,
                sourceEventId = -1,
                continuesFromPrevious = false,
                continuesToNext = false,
                voiceIndex = voiceIndex,
                noteKey = "rest"
            });
            cursorTime = nextTime;
        }
    }

    private static void AppendEventSequence(
        List<BeatBuildInfo> beats,
        QuantizedEvent quantized,
        int slotsPerBeat,
        List<RocksmithAlphaTabTimingBeatEntry> timingEntries,
        float startTime,
        float endTime,
        int voiceIndex,
        int masterBarIndex,
        NoteRenderContext noteRenderContext)
    {
        List<DurationToken> tokens = DecomposeDuration(quantized.DurationSlots, slotsPerBeat);
        float cursorTime = startTime;
        int totalSlots = Math.Max(1, quantized.DurationSlots);
        int consumedSlots = 0;

        for (int tokenIndex = 0; tokenIndex < tokens.Count; tokenIndex++)
        {
            DurationToken token = tokens[tokenIndex];
            bool continuesFromPrevious = tokenIndex > 0 || quantized.tieFromPrevious;
            bool continuesToNext = tokenIndex < tokens.Count - 1 || quantized.tieToNext;
            bool isAttackToken = tokenIndex == 0 && !quantized.tieFromPrevious;

            consumedSlots += token.slots;
            float nextTime = tokenIndex == tokens.Count - 1
                ? endTime
                : Mathf.Lerp(startTime, endTime, consumedSlots / (float)totalSlots);
            nextTime = Math.Max(cursorTime + 0.001f, nextTime);

            BeatBuildInfo beatInfo = BuildEventBeatInfo(quantized, token, isAttackToken, continuesFromPrevious, continuesToNext, cursorTime, nextTime, noteRenderContext);
            beats.Add(beatInfo);

            timingEntries?.Add(new RocksmithAlphaTabTimingBeatEntry
            {
                startTime = cursorTime,
                endTime = nextTime,
                isRest = false,
                masterBarIndex = masterBarIndex,
                sourceEventId = quantized.sourceEventId,
                continuesFromPrevious = continuesFromPrevious,
                continuesToNext = continuesToNext,
                voiceIndex = voiceIndex,
                noteKey = BuildTimingNoteKey(quantized.notes)
            });
            cursorTime = nextTime;
        }
    }

    private static BeatBuildInfo BuildEventBeatInfo(
        QuantizedEvent quantized,
        DurationToken token,
        bool isAttackToken,
        bool continuesFromPrevious,
        bool continuesToNext,
        float tokenStartTime,
        float tokenEndTime,
        NoteRenderContext noteRenderContext)
    {
        BeatBuildInfo info = new BeatBuildInfo
        {
            isRest = false,
            durationToken = token,
            sourceEventId = quantized.sourceEventId
        };

        bool anyPalmMute = false;
        bool anyLetRing = false;
        bool anyVibrato = false;
        bool anyTremolo = false;

        for (int i = 0; i < quantized.notes.Count; i++)
        {
            RocksmithCachedNoteData note = quantized.notes[i];
            NoteBuildInfo noteInfo = BuildNoteInfo(note, tokenStartTime, tokenEndTime, isAttackToken, continuesFromPrevious, continuesToNext, noteRenderContext);
            info.notes.Add(noteInfo);

            anyPalmMute |= noteInfo.isPalmMute;
            anyLetRing |= noteInfo.isLetRing;
            anyVibrato |= noteInfo.vibrato != VibratoType.None;
            anyTremolo |= note != null && note.isTremolo;
        }

        info.isPalmMute = anyPalmMute;
        info.isLetRing = anyLetRing;
        info.beatVibrato = anyVibrato ? VibratoType.Slight : VibratoType.None;
        if (anyTremolo)
            info.tremoloSpeed = ResolveTremoloSpeed(token.duration);

        return info;
    }

    private static NoteBuildInfo BuildNoteInfo(
        RocksmithCachedNoteData note,
        float tokenStartTime,
        float tokenEndTime,
        bool isAttackToken,
        bool continuesFromPrevious,
        bool continuesToNext,
        NoteRenderContext noteRenderContext)
    {
        HarmonicType harmonicType = HarmonicType.None;
        double harmonicValue = 0d;
        if (note != null)
        {
            if (note.isPinchHarmonic)
                harmonicType = HarmonicType.Pinch;
            else if (note.isHarmonic)
                harmonicType = HarmonicType.Natural;
        }

        bool hasBendSemantics = HasBendSemantics(note);
        bool hideContinuationVisual = UseGameplayHiddenBendContinuations &&
                                      UseGameplayAlphaTabSimplification &&
                                      hasBendSemantics &&
                                      continuesFromPrevious;
        float bendWindowStart = tokenStartTime;
        float bendWindowEnd = tokenEndTime;
        if (UseGameplayHiddenBendContinuations &&
            UseGameplayAlphaTabSimplification &&
            hasBendSemantics &&
            isAttackToken &&
            note != null &&
            noteRenderContext != null)
        {
            bendWindowStart = note.time;
            bendWindowEnd = ResolveNoteEndTime(note, noteRenderContext.effectiveEndTimes);
        }

        BendSlice bendSlice = hideContinuationVisual
            ? default
            : BuildBendSlice(note, bendWindowStart, bendWindowEnd);
        VibratoType vibrato = ResolveVibratoType(note, tokenStartTime, tokenEndTime);
        bool isDead = note != null && (note.isFretHandMute || note.isMuted);
        bool isLetRing = note != null &&
                         !isDead &&
                         !note.isPalmMute &&
                         !note.isTremolo &&
                         !note.isLegato &&
                         (tokenEndTime - tokenStartTime) >= GuitarTechniqueVisualThresholds.SustainSeconds;

        return new NoteBuildInfo
        {
            sourceNote = note,
            stringNumber = Math.Max(1, note != null ? note.stringIdx + 1 : 1),
            fret = Math.Max(0, note?.fret ?? 0),
            isDead = isDead,
            isPalmMute = note != null && note.isPalmMute,
            isTap = note != null && note.isTap,
            isLetRing = isLetRing,
            vibrato = vibrato,
            accentuation = note != null && note.isAccent ? AccentuationType.Normal : AccentuationType.None,
            harmonicType = harmonicType,
            harmonicValue = harmonicValue,
            bendType = bendSlice.type,
            bendPoints = bendSlice.points,
            isAttackToken = isAttackToken,
            continuesFromPrevious = continuesFromPrevious,
            continuesToNext = continuesToNext,
            isVisible = !hideContinuationVisual
        };
    }

    private static bool HasBendSemantics(RocksmithCachedNoteData note)
    {
        return note != null &&
               (((note.bendPoints?.Count) ?? 0) > 0 ||
                note.bendPreBend ||
                note.bendRelease ||
                note.bendStep > 0.01f ||
                note.maxBend > 0.01f);
    }

    private static BendSlice BuildBendSlice(RocksmithCachedNoteData note, float tokenStartTime, float tokenEndTime)
    {
        BendSlice slice = default;
        if (note == null)
            return slice;

        List<RocksmithCachedBendPointData> sourcePoints = note.bendPoints?
            .Where(point => point != null)
            .OrderBy(point => point.timeSeconds)
            .ToList();

        float windowStart = Math.Max(0f, tokenStartTime - note.time);
        float windowEnd = Math.Max(windowStart + 0.001f, tokenEndTime - note.time);

        List<BendPoint> points = new List<BendPoint>();
        if (sourcePoints != null && sourcePoints.Count > 0)
        {
            float startStep = SampleBendStep(sourcePoints, windowStart, note);
            float endStep = SampleBendStep(sourcePoints, windowEnd, note);
            points.Add(new BendPoint(0, ToAlphaTabBendValue(startStep)));

            for (int i = 0; i < sourcePoints.Count; i++)
            {
                RocksmithCachedBendPointData point = sourcePoints[i];
                if (point.timeSeconds <= windowStart + 0.0005f || point.timeSeconds >= windowEnd - 0.0005f)
                    continue;

                double offset = Math.Clamp(Math.Round(((point.timeSeconds - windowStart) / Math.Max(0.001f, windowEnd - windowStart)) * 60d), 0d, 60d);
                points.Add(new BendPoint(offset, ToAlphaTabBendValue(point.step)));
            }

            points.Add(new BendPoint(60, ToAlphaTabBendValue(endStep)));
        }
        else
        {
            float bend = Math.Max(note.bendStep, Math.Max(0f, note.maxBend));
            if (bend > 0.01f)
            {
                double bendValue = ToAlphaTabBendValue(bend);
                if (note.bendPreBend && note.bendRelease)
                {
                    points.Add(new BendPoint(0, bendValue));
                    points.Add(new BendPoint(60, 0));
                }
                else if (note.bendPreBend)
                {
                    points.Add(new BendPoint(0, bendValue));
                    points.Add(new BendPoint(60, bendValue));
                }
                else if (note.bendRelease)
                {
                    points.Add(new BendPoint(0, 0));
                    points.Add(new BendPoint(30, bendValue));
                    points.Add(new BendPoint(60, 0));
                }
                else
                {
                    points.Add(new BendPoint(0, 0));
                    points.Add(new BendPoint(60, bendValue));
                }
            }
        }

        points = points
            .Where(point => point != null)
            .GroupBy(point => Math.Round(point.Offset))
            .Select(group => group.Last())
            .OrderBy(point => point.Offset)
            .ToList();

        bool anyNonZero = points.Any(point => Math.Abs(point.Value) > 0.01);
        if (!anyNonZero)
            return slice;

        slice.points = points;
        slice.type = ClassifyBendType(points);
        return slice;
    }

    private static BendType ClassifyBendType(List<BendPoint> points)
    {
        if (points == null || points.Count == 0)
            return BendType.None;

        double start = points[0].Value;
        double end = points[points.Count - 1].Value;
        bool hasUp = false;
        bool hasDown = false;
        for (int i = 1; i < points.Count; i++)
        {
            double delta = points[i].Value - points[i - 1].Value;
            if (delta > 0.05)
                hasUp = true;
            if (delta < -0.05)
                hasDown = true;
        }

        bool startsBent = Math.Abs(start) > 0.05;
        if (startsBent)
        {
            if (hasUp && hasDown)
                return BendType.Custom;
            if (hasUp)
                return BendType.PrebendBend;
            if (hasDown)
                return BendType.PrebendRelease;
            return BendType.Hold;
        }

        if (hasUp && hasDown)
            return BendType.BendRelease;
        if (hasUp)
            return BendType.Bend;
        if (hasDown)
            return BendType.Release;
        return BendType.Hold;
    }

    private static VibratoType ResolveVibratoType(RocksmithCachedNoteData note, float tokenStartTime, float tokenEndTime)
    {
        if (!HasVibratoDuringWindow(note, tokenStartTime, tokenEndTime))
            return VibratoType.None;

        return note != null && note.vibratoStrength > 1
            ? VibratoType.Wide
            : VibratoType.Slight;
    }

    private static Duration? ResolveTremoloSpeed(Duration duration)
    {
        switch (duration)
        {
            case Duration.ThirtySecond:
            case Duration.SixtyFourth:
            case Duration.OneHundredTwentyEighth:
            case Duration.TwoHundredFiftySixth:
                return Duration.ThirtySecond;
            case Duration.Sixteenth:
                return Duration.Sixteenth;
            default:
                return Duration.Eighth;
        }
    }

    private static bool HasVibratoDuringWindow(RocksmithCachedNoteData note, float tokenStartTime, float tokenEndTime)
    {
        if (note == null)
            return false;

        if (note.hasVibrato && tokenEndTime > note.time + 0.0005f)
            return true;

        if (note.techniqueSegments == null)
            return false;

        float relativeStart = Math.Max(0f, tokenStartTime - note.time);
        float relativeEnd = Math.Max(relativeStart, tokenEndTime - note.time);
        for (int i = 0; i < note.techniqueSegments.Count; i++)
        {
            RocksmithCachedTechniqueSegmentData segment = note.techniqueSegments[i];
            if (segment == null || segment.type != (int)NoteTechniqueSegmentType.Vibrato)
                continue;

            if (segment.endOffset > relativeStart + 0.0005f && segment.startOffset < relativeEnd - 0.0005f)
                return true;
        }

        return false;
    }

    private static Dictionary<int, float> BuildEffectiveEndTimes(RocksmithCachedArrangementPart part)
    {
        Dictionary<int, float> effectiveEndTimes = new Dictionary<int, float>();
        List<RocksmithCachedNoteData> notes = part?.notes;
        if (notes == null || notes.Count == 0)
            return effectiveEndTimes;

        List<RocksmithCachedNoteData> sorted = notes
            .Where(note => note != null)
            .OrderBy(note => note.time)
            .ThenBy(note => note.stringIdx)
            .ThenBy(note => note.id)
            .ToList();

        Dictionary<int, RocksmithCachedNoteData> linkedChildrenByParentId = new Dictionary<int, RocksmithCachedNoteData>();
        Dictionary<int, RocksmithCachedNoteData> nextNoteOnStringById = new Dictionary<int, RocksmithCachedNoteData>();
        Dictionary<int, RocksmithCachedNoteData> nextGlobalNoteById = new Dictionary<int, RocksmithCachedNoteData>();
        RocksmithCachedNoteData[] nextNoteOnString = new RocksmithCachedNoteData[8];
        RocksmithCachedNoteData nextGlobalNote = null;

        for (int i = sorted.Count - 1; i >= 0; i--)
        {
            RocksmithCachedNoteData note = sorted[i];
            if (note == null)
                continue;

            if (note.linkedFromNoteId >= 0 && !linkedChildrenByParentId.ContainsKey(note.linkedFromNoteId))
                linkedChildrenByParentId[note.linkedFromNoteId] = note;

            int clampedStringIndex = Mathf.Clamp(note.stringIdx, 0, nextNoteOnString.Length - 1);
            if (nextNoteOnString[clampedStringIndex] != null)
                nextNoteOnStringById[note.id] = nextNoteOnString[clampedStringIndex];
            nextNoteOnString[clampedStringIndex] = note;

            if (nextGlobalNote != null)
                nextGlobalNoteById[note.id] = nextGlobalNote;
            nextGlobalNote = note;
        }

        for (int i = 0; i < sorted.Count; i++)
        {
            RocksmithCachedNoteData note = sorted[i];
            if (note == null)
                continue;

            float explicitDuration = Mathf.Max(note.duration, 0f);
            float visualDuration = Mathf.Max(note.bendVisualDuration, 0f);
            float rawBendDuration = note.bendPoints != null && note.bendPoints.Count > 0
                ? Mathf.Max(0f, note.bendPoints.Max(point => point?.timeSeconds ?? 0f))
                : 0f;
            float segmentDuration = 0f;
            if (note.techniqueSegments != null)
            {
                for (int segmentIndex = 0; segmentIndex < note.techniqueSegments.Count; segmentIndex++)
                {
                    RocksmithCachedTechniqueSegmentData segment = note.techniqueSegments[segmentIndex];
                    if (segment == null)
                        continue;
                    segmentDuration = Mathf.Max(segmentDuration, Mathf.Max(0f, segment.endOffset));
                }
            }

            float endTime = note.time + Mathf.Max(explicitDuration, Mathf.Max(rawBendDuration, Mathf.Max(visualDuration, segmentDuration)));
            if (endTime <= note.time + 0.0005f)
            {
                if (linkedChildrenByParentId.TryGetValue(note.id, out RocksmithCachedNoteData linkedChild) &&
                    linkedChild != null &&
                    linkedChild.time > note.time + 0.0005f)
                {
                    endTime = linkedChild.time;
                }
                else if ((note.isLegato || !note.requiresPluck || note.linkedFromNoteId >= 0) &&
                         nextNoteOnStringById.TryGetValue(note.id, out RocksmithCachedNoteData nextOnString) &&
                         nextOnString != null &&
                         nextOnString.time > note.time + 0.0005f)
                {
                    endTime = nextOnString.time;
                }
                else if (nextGlobalNoteById.TryGetValue(note.id, out RocksmithCachedNoteData nextOnset) &&
                         nextOnset != null &&
                         nextOnset.time > note.time + 0.0005f)
                {
                    endTime = nextOnset.time;
                }
                else if (TryResolveNextBeatTime(part?.timing?.ebeats, note.time, out float nextBeatTime))
                {
                    endTime = nextBeatTime;
                }
                else if (part != null && part.durationSeconds > note.time + 0.0005f)
                {
                    endTime = part.durationSeconds;
                }
                else
                {
                    endTime = note.time + ResolveFallbackBeatSeconds(part);
                }
            }

            effectiveEndTimes[note.id] = endTime;
        }

        return effectiveEndTimes;
    }

    private static bool TryResolveNextBeatTime(List<RocksmithCachedEbeatData> ebeats, float noteTime, out float nextBeatTime)
    {
        nextBeatTime = 0f;
        if (ebeats == null || ebeats.Count == 0)
            return false;

        for (int i = 0; i < ebeats.Count; i++)
        {
            RocksmithCachedEbeatData ebeat = ebeats[i];
            if (ebeat == null)
                continue;

            if (ebeat.timeSeconds > noteTime + 0.0005f)
            {
                nextBeatTime = ebeat.timeSeconds;
                return true;
            }
        }

        return false;
    }

    private static float ResolveNoteEndTime(RocksmithCachedNoteData note, Dictionary<int, float> effectiveEndTimes)
    {
        if (note == null)
            return 0f;

        if (effectiveEndTimes != null && effectiveEndTimes.TryGetValue(note.id, out float endTime))
            return endTime;

        return note.time + Mathf.Max(0.05f, note.duration);
    }

    private static List<MeasureInfo> BuildMeasures(RocksmithCachedArrangementPart part, Dictionary<int, float> effectiveEndTimes)
    {
        List<RocksmithCachedEbeatData> ebeats = (part?.timing?.ebeats ?? new List<RocksmithCachedEbeatData>())
            .Where(ebeat => ebeat != null)
            .OrderBy(ebeat => ebeat.timeSeconds)
            .ToList();
        float fallbackBeatSeconds = ResolveFallbackBeatSeconds(part);
        float finalTime = ResolveFinalTime(part, ebeats, fallbackBeatSeconds, effectiveEndTimes);

        if (ebeats.Count == 0)
            return BuildFallbackMeasures(finalTime, fallbackBeatSeconds);

        List<int> measureStartIndices = new List<int>();
        for (int i = 0; i < ebeats.Count; i++)
        {
            if (ebeats[i].measure >= 0)
                measureStartIndices.Add(i);
        }

        if (measureStartIndices.Count == 0)
            measureStartIndices.Add(0);
        else if (measureStartIndices[0] > 0)
            measureStartIndices.Insert(0, 0);

        List<MeasureInfo> measures = new List<MeasureInfo>(measureStartIndices.Count);
        for (int i = 0; i < measureStartIndices.Count; i++)
        {
            int startIndex = measureStartIndices[i];
            int nextIndex = i + 1 < measureStartIndices.Count ? measureStartIndices[i + 1] : ebeats.Count;
            List<RocksmithCachedEbeatData> beats = ebeats.Skip(startIndex).Take(Math.Max(1, nextIndex - startIndex)).ToList();
            float startTime = i == 0 && startIndex > 0 ? 0f : beats[0].timeSeconds;
            float endTime = i + 1 < measureStartIndices.Count
                ? ebeats[measureStartIndices[i + 1]].timeSeconds
                : EstimateFinalMeasureEnd(beats, finalTime, fallbackBeatSeconds);

            if (endTime <= startTime + 0.001f)
                endTime = startTime + fallbackBeatSeconds * Math.Max(1, beats.Count);

            float tempoBpm = EstimateTempoBpm(beats, endTime, part?.timing?.averageTempoBpm ?? 120f);
            measures.Add(new MeasureInfo(startTime, endTime, beats.Select(beat => beat.timeSeconds).ToList(), tempoBpm));
        }

        return measures;
    }

    private static List<MeasureInfo> BuildFallbackMeasures(float finalTime, float beatSeconds)
    {
        int totalMeasures = Math.Max(1, (int)Math.Ceiling(finalTime / Math.Max(0.01f, beatSeconds * 4f)));
        List<MeasureInfo> measures = new List<MeasureInfo>(totalMeasures);
        for (int measureIndex = 0; measureIndex < totalMeasures; measureIndex++)
        {
            float start = measureIndex * beatSeconds * 4f;
            float end = Math.Min(finalTime, start + beatSeconds * 4f);
            if (measureIndex == totalMeasures - 1 && end <= start + 0.001f)
                end = start + beatSeconds * 4f;
            measures.Add(new MeasureInfo(
                start,
                end,
                new List<float> { start, start + beatSeconds, start + (beatSeconds * 2f), start + (beatSeconds * 3f) },
                beatSeconds > 0.001f ? 60f / beatSeconds : 120f));
        }
        return measures;
    }

    private static float ResolveFallbackBeatSeconds(RocksmithCachedArrangementPart part)
    {
        float averageTempo = part?.timing?.averageTempoBpm ?? 120f;
        if (averageTempo <= 0.01f)
            averageTempo = 120f;
        return 60f / averageTempo;
    }

    private static float ResolveFinalTime(RocksmithCachedArrangementPart part, List<RocksmithCachedEbeatData> ebeats, float fallbackBeatSeconds, Dictionary<int, float> effectiveEndTimes)
    {
        float noteEnd = 0f;
        if (part?.notes != null && part.notes.Count > 0)
        {
            for (int i = 0; i < part.notes.Count; i++)
                noteEnd = Math.Max(noteEnd, ResolveNoteEndTime(part.notes[i], effectiveEndTimes));
        }

        float lastBeat = ebeats.Count > 0 ? ebeats[ebeats.Count - 1].timeSeconds : 0f;
        return Math.Max(Math.Max(part?.durationSeconds ?? 0f, noteEnd), lastBeat + Math.Max(0.25f, fallbackBeatSeconds));
    }

    private static float EstimateFinalMeasureEnd(List<RocksmithCachedEbeatData> beats, float finalTime, float fallbackBeatSeconds)
    {
        float startTime = beats.Count > 0 ? beats[0].timeSeconds : 0f;
        float averageBeat = fallbackBeatSeconds;
        if (beats.Count > 1)
        {
            float total = 0f;
            for (int i = 1; i < beats.Count; i++)
                total += Math.Max(0.001f, beats[i].timeSeconds - beats[i - 1].timeSeconds);
            averageBeat = total / Math.Max(1, beats.Count - 1);
        }

        return Math.Max(finalTime, startTime + (averageBeat * Math.Max(1, beats.Count)));
    }

    private static float EstimateTempoBpm(List<RocksmithCachedEbeatData> beats, float endTime, float fallbackTempoBpm)
    {
        float totalDuration = 0f;
        int segmentCount = 0;
        for (int i = 1; i < beats.Count; i++)
        {
            totalDuration += Math.Max(0.001f, beats[i].timeSeconds - beats[i - 1].timeSeconds);
            segmentCount++;
        }

        if (segmentCount == 0 && beats.Count > 0)
        {
            totalDuration = Math.Max(0.001f, endTime - beats[0].timeSeconds);
            segmentCount = 1;
        }

        if (segmentCount == 0)
            return fallbackTempoBpm > 0.01f ? fallbackTempoBpm : 120f;

        float averageBeatSeconds = totalDuration / segmentCount;
        return averageBeatSeconds > 0.001f ? 60f / averageBeatSeconds : 120f;
    }

    private static List<EventInfo> BuildEvents(List<RocksmithCachedNoteData> notes, Dictionary<int, float> effectiveEndTimes)
    {
        List<EventInfo> events = new List<EventInfo>();
        if (notes == null || notes.Count == 0)
            return events;

        List<RocksmithCachedNoteData> sorted = notes
            .Where(note => note != null)
            .OrderBy(note => note.time)
            .ThenBy(note => note.chordId)
            .ThenBy(note => note.stringIdx)
            .ToList();

        EventInfo current = null;
        int nextSourceEventId = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            RocksmithCachedNoteData note = sorted[i];
            if (current == null || !current.CanAccept(note))
            {
                current = new EventInfo(note, nextSourceEventId++, effectiveEndTimes);
                events.Add(current);
            }

            current.notes.Add(note);
            current.endTime = Math.Max(current.endTime, ResolveNoteEndTime(note, effectiveEndTimes));
        }

        return events;
    }

    private static void AssignVoices(List<EventInfo> events)
    {
        if (events == null || events.Count == 0)
            return;

        List<float> activeVoiceEndTimes = new List<float>();
        for (int i = 0; i < events.Count; i++)
        {
            EventInfo current = events[i];
            if (current == null)
                continue;

            int voiceIndex = 0;
            for (; voiceIndex < activeVoiceEndTimes.Count; voiceIndex++)
            {
                if (current.startTime >= activeVoiceEndTimes[voiceIndex] - 0.0005f)
                    break;
            }

            if (voiceIndex >= activeVoiceEndTimes.Count)
                activeVoiceEndTimes.Add(current.endTime);
            else
                activeVoiceEndTimes[voiceIndex] = Math.Max(activeVoiceEndTimes[voiceIndex], current.endTime);

            current.voiceIndex = voiceIndex;
        }
    }

    private static List<List<EventSliceInfo>> BuildEventSlices(List<EventInfo> events, List<MeasureInfo> measures)
    {
        List<List<EventSliceInfo>> slicesByMeasure = new List<List<EventSliceInfo>>(measures.Count);
        for (int i = 0; i < measures.Count; i++)
            slicesByMeasure.Add(new List<EventSliceInfo>());

        if (events == null || measures == null || measures.Count == 0)
            return slicesByMeasure;

        for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
        {
            EventInfo source = events[eventIndex];
            if (source == null)
                continue;

            bool emittedAny = false;
            for (int measureIndex = 0; measureIndex < measures.Count; measureIndex++)
            {
                MeasureInfo measure = measures[measureIndex];
                if (source.endTime <= measure.startTime + 0.0005f)
                    continue;
                if (source.startTime >= measure.endTime - 0.0005f)
                    continue;

                float sliceStart = Math.Max(source.startTime, measure.startTime);
                float sliceEnd = Math.Min(source.endTime, measure.endTime);
                if (sliceEnd <= sliceStart + 0.0005f)
                    continue;

                bool tieFromPrevious = emittedAny || sliceStart > source.startTime + 0.0005f;
                bool tieToNext = source.endTime > measure.endTime + 0.0005f;
                slicesByMeasure[measureIndex].Add(new EventSliceInfo
                {
                    sourceEventId = source.sourceEventId,
                    voiceIndex = source.voiceIndex,
                    startTime = sliceStart,
                    endTime = sliceEnd,
                    tieFromPrevious = tieFromPrevious,
                    tieToNext = tieToNext,
                    notes = source.notes.OrderBy(note => note.stringIdx).ToList()
                });
                emittedAny = true;
            }
        }

        for (int measureIndex = 0; measureIndex < slicesByMeasure.Count; measureIndex++)
            slicesByMeasure[measureIndex] = slicesByMeasure[measureIndex].OrderBy(slice => slice.startTime).ToList();

        return slicesByMeasure;
    }

    private static List<List<EventSliceInfo>> SimplifyGameplayMeasureSlices(List<List<EventSliceInfo>> slicesByMeasure)
    {
        if (slicesByMeasure == null || slicesByMeasure.Count == 0)
            return slicesByMeasure ?? new List<List<EventSliceInfo>>();

        List<List<EventSliceInfo>> simplified = new List<List<EventSliceInfo>>(slicesByMeasure.Count);
        for (int measureIndex = 0; measureIndex < slicesByMeasure.Count; measureIndex++)
        {
            List<EventSliceInfo> measureSlices = slicesByMeasure[measureIndex] ?? new List<EventSliceInfo>();
            List<EventSliceInfo> splitSlices = SplitSlicesAtAttackBoundaries(measureSlices);
            List<EventSliceInfo> filteredSlices = FilterGameplaySupportContinuations(splitSlices);
            ReassignSliceVoices(filteredSlices);
            simplified.Add(filteredSlices
                .OrderBy(slice => slice.startTime)
                .ThenBy(slice => slice.voiceIndex)
                .ToList());
        }

        return simplified;
    }

    private static List<EventSliceInfo> SplitSlicesAtAttackBoundaries(List<EventSliceInfo> measureSlices)
    {
        if (measureSlices == null || measureSlices.Count == 0)
            return new List<EventSliceInfo>();

        List<float> attackBoundaries = measureSlices
            .Where(slice => slice != null && !slice.tieFromPrevious)
            .Select(slice => slice.startTime)
            .Distinct()
            .OrderBy(time => time)
            .ToList();

        List<EventSliceInfo> result = new List<EventSliceInfo>();
        for (int i = 0; i < measureSlices.Count; i++)
        {
            EventSliceInfo source = measureSlices[i];
            if (source == null)
                continue;

            List<float> splitTimes = attackBoundaries
                .Where(time => time > source.startTime + 0.0005f && time < source.endTime - 0.0005f)
                .OrderBy(time => time)
                .ToList();

            if (splitTimes.Count == 0)
            {
                result.Add(CloneSlice(source));
                continue;
            }

            float segmentStart = source.startTime;
            bool segmentTieFromPrevious = source.tieFromPrevious;
            for (int splitIndex = 0; splitIndex <= splitTimes.Count; splitIndex++)
            {
                float segmentEnd = splitIndex < splitTimes.Count ? splitTimes[splitIndex] : source.endTime;
                if (segmentEnd <= segmentStart + 0.0005f)
                    continue;

                bool segmentTieToNext = splitIndex < splitTimes.Count || source.tieToNext;
                result.Add(new EventSliceInfo
                {
                    sourceEventId = source.sourceEventId,
                    voiceIndex = source.voiceIndex,
                    startTime = segmentStart,
                    endTime = segmentEnd,
                    tieFromPrevious = segmentTieFromPrevious,
                    tieToNext = segmentTieToNext,
                    notes = source.notes.OrderBy(note => note.stringIdx).ToList()
                });

                segmentStart = segmentEnd;
                segmentTieFromPrevious = true;
            }
        }

        return result
            .OrderBy(slice => slice.startTime)
            .ThenBy(slice => slice.voiceIndex)
            .ToList();
    }

    private static List<EventSliceInfo> FilterGameplaySupportContinuations(List<EventSliceInfo> measureSlices)
    {
        if (measureSlices == null || measureSlices.Count == 0)
            return new List<EventSliceInfo>();

        List<EventSliceInfo> result = new List<EventSliceInfo>();
        for (int i = 0; i < measureSlices.Count; i++)
        {
            EventSliceInfo slice = measureSlices[i];
            if (slice == null)
                continue;

            if (ShouldSuppressGameplaySupportSlice(slice, measureSlices))
                continue;

            result.Add(slice);
        }

        return result;
    }

    private static bool ShouldSuppressGameplaySupportSlice(EventSliceInfo slice, List<EventSliceInfo> measureSlices)
    {
        if (slice == null || !slice.tieFromPrevious)
            return false;

        if (SliceRequiresExplicitTechniqueVisibility(slice))
            return false;

        for (int i = 0; i < measureSlices.Count; i++)
        {
            EventSliceInfo other = measureSlices[i];
            if (other == null || ReferenceEquals(other, slice) || other.sourceEventId == slice.sourceEventId)
                continue;

            if (other.startTime > slice.endTime - 0.0005f || other.endTime < slice.startTime + 0.0005f)
                continue;

            if (!other.tieFromPrevious || SliceRequiresExplicitTechniqueVisibility(other))
                return true;
        }

        return false;
    }

    private static bool SliceRequiresExplicitTechniqueVisibility(EventSliceInfo slice)
    {
        if (slice == null || slice.notes == null || slice.notes.Count == 0)
            return false;

        for (int i = 0; i < slice.notes.Count; i++)
        {
            RocksmithCachedNoteData note = slice.notes[i];
            if (note == null)
                continue;

            if (note.hasVibrato ||
                note.bendStep > 0.01f ||
                (note.bendPoints != null && note.bendPoints.Count > 0) ||
                note.bendPreBend ||
                note.bendRelease ||
                note.isTap ||
                note.isHarmonic ||
                note.isPinchHarmonic ||
                note.isPalmMute ||
                note.isAccent ||
                note.isTremolo)
            {
                return true;
            }

            NoteTechnique technique = (NoteTechnique)Math.Clamp(note.technique, (int)NoteTechnique.None, (int)NoteTechnique.Vibrato);
            switch (technique)
            {
                case NoteTechnique.HammerOn:
                case NoteTechnique.PullOff:
                case NoteTechnique.Slide:
                case NoteTechnique.Vibrato:
                    return true;
            }
        }

        return false;
    }

    private static void ReassignSliceVoices(List<EventSliceInfo> slices)
    {
        if (slices == null || slices.Count == 0)
            return;

        List<float> activeVoiceEndTimes = new List<float>();
        List<EventSliceInfo> ordered = slices
            .OrderBy(slice => slice.startTime)
            .ThenBy(slice => slice.endTime)
            .ToList();

        for (int i = 0; i < ordered.Count; i++)
        {
            EventSliceInfo current = ordered[i];
            if (current == null)
                continue;

            int voiceIndex = 0;
            for (; voiceIndex < activeVoiceEndTimes.Count; voiceIndex++)
            {
                if (current.startTime >= activeVoiceEndTimes[voiceIndex] - 0.0005f)
                    break;
            }

            if (voiceIndex >= activeVoiceEndTimes.Count)
                activeVoiceEndTimes.Add(current.endTime);
            else
                activeVoiceEndTimes[voiceIndex] = Math.Max(activeVoiceEndTimes[voiceIndex], current.endTime);

            current.voiceIndex = voiceIndex;
        }
    }

    private static EventSliceInfo CloneSlice(EventSliceInfo source)
    {
        return new EventSliceInfo
        {
            sourceEventId = source.sourceEventId,
            voiceIndex = source.voiceIndex,
            startTime = source.startTime,
            endTime = source.endTime,
            tieFromPrevious = source.tieFromPrevious,
            tieToNext = source.tieToNext,
            notes = source.notes.OrderBy(note => note.stringIdx).ToList()
        };
    }

    private static int ResolveSlotsPerBeatForMeasure(IEnumerable<List<EventSliceInfo>> voices, MeasureInfo measure)
    {
        for (int i = 0; i < SupportedSlotsPerBeat.Length; i++)
        {
            int slotsPerBeat = SupportedSlotsPerBeat[i];
            bool allVoicesSupported = true;
            foreach (List<EventSliceInfo> voice in voices)
            {
                if (!TryQuantizeEventsForMeasure(voice, measure, slotsPerBeat, out _))
                {
                    allVoicesSupported = false;
                    break;
                }
            }

            if (allVoicesSupported)
                return slotsPerBeat;
        }

        return SupportedSlotsPerBeat[SupportedSlotsPerBeat.Length - 1];
    }

    private static List<QuantizedEvent> QuantizeEventsForMeasure(List<EventSliceInfo> events, MeasureInfo measure, int slotsPerBeat)
    {
        if (TryQuantizeEventsForMeasure(events, measure, slotsPerBeat, out List<QuantizedEvent> quantizedEvents))
            return quantizedEvents;

        return QuantizeEventsForMeasureFallback(events, measure);
    }

    private static bool TryQuantizeEventsForMeasure(List<EventSliceInfo> events, MeasureInfo measure, int slotsPerBeat, out List<QuantizedEvent> result)
    {
        result = new List<QuantizedEvent>();
        float[] slotTimes = measure.BuildSlotTimes(slotsPerBeat);
        int totalSlots = measure.GetTotalSlots(slotsPerBeat);
        int cursorSlot = 0;

        for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
        {
            EventSliceInfo source = events[eventIndex];
            if (source.startTime >= measure.endTime - 0.0005f)
                break;
            if (source.startTime < measure.startTime - 0.0005f)
                continue;
            if (cursorSlot >= totalSlots)
                return false;

            int startSlot = FindNearestSlot(slotTimes, source.startTime);
            startSlot = Math.Clamp(startSlot, cursorSlot, totalSlots - 1);
            float clippedEndTime = Math.Min(measure.endTime, Math.Max(source.startTime + 0.02f, source.endTime));
            int endSlot = FindCeilSlot(slotTimes, clippedEndTime);
            if (endSlot <= startSlot)
                endSlot = Math.Min(totalSlots, startSlot + 1);
            if (endSlot <= startSlot)
                return false;

            result.Add(new QuantizedEvent
            {
                startSlot = startSlot,
                endSlot = endSlot,
                sourceEventId = source.sourceEventId,
                sourceStartTime = source.startTime,
                sourceEndTime = clippedEndTime,
                tieFromPrevious = source.tieFromPrevious,
                tieToNext = source.tieToNext,
                notes = source.notes
            });
            cursorSlot = endSlot;
        }

        return true;
    }

    private static List<QuantizedEvent> QuantizeEventsForMeasureFallback(List<EventSliceInfo> events, MeasureInfo measure)
    {
        measure.SetSlotsPerBeat(SupportedSlotsPerBeat[SupportedSlotsPerBeat.Length - 1]);
        List<QuantizedEvent> result = new List<QuantizedEvent>();
        float[] slotTimes = measure.BuildSlotTimes();
        int totalSlots = measure.TotalSlots;
        int cursorSlot = 0;

        for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
        {
            EventSliceInfo source = events[eventIndex];
            if (source.startTime >= measure.endTime - 0.0005f)
                break;
            if (source.startTime < measure.startTime - 0.0005f)
                continue;
            if (cursorSlot >= totalSlots)
                break;

            int startSlot = FindNearestSlot(slotTimes, source.startTime);
            startSlot = Math.Clamp(startSlot, cursorSlot, totalSlots - 1);
            float clippedEndTime = Math.Min(measure.endTime, Math.Max(source.startTime + 0.02f, source.endTime));
            int endSlot = Math.Min(totalSlots, Math.Max(startSlot + 1, FindCeilSlot(slotTimes, clippedEndTime)));

            result.Add(new QuantizedEvent
            {
                startSlot = startSlot,
                endSlot = endSlot,
                sourceEventId = source.sourceEventId,
                sourceStartTime = source.startTime,
                sourceEndTime = clippedEndTime,
                tieFromPrevious = source.tieFromPrevious,
                tieToNext = source.tieToNext,
                notes = source.notes
            });
            cursorSlot = endSlot;
        }

        return result;
    }

    private static int FindNearestSlot(float[] slotTimes, float time)
    {
        int bestIndex = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < slotTimes.Length - 1; i++)
        {
            float distance = Math.Abs(slotTimes[i] - time);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    private static int FindCeilSlot(float[] slotTimes, float time)
    {
        for (int i = 1; i < slotTimes.Length; i++)
        {
            if (slotTimes[i] >= time - 0.0005f)
                return i;
        }
        return slotTimes.Length - 1;
    }

    private static List<DurationToken> DecomposeDuration(int slotCount, int slotsPerBeat)
    {
        DurationToken[] availableTokens = BuildDurationTokens(slotsPerBeat);
        List<DurationToken> tokens = new List<DurationToken>();
        int remaining = Math.Max(1, slotCount);
        while (remaining > 0)
        {
            DurationToken token = availableTokens.First(candidate => candidate.slots <= remaining);
            tokens.Add(token);
            remaining -= token.slots;
        }

        return tokens;
    }

    private static DurationToken[] BuildDurationTokens(int slotsPerBeat)
    {
        List<DurationToken> tokens = new List<DurationToken>
        {
            new DurationToken(slotsPerBeat * 4, Duration.Whole, 0),
            new DurationToken(slotsPerBeat * 3, Duration.Half, 1),
            new DurationToken(slotsPerBeat * 2, Duration.Half, 0),
            new DurationToken(slotsPerBeat + (slotsPerBeat / 2), Duration.Quarter, 1),
            new DurationToken(slotsPerBeat, Duration.Quarter, 0)
        };

        if (slotsPerBeat >= 2)
        {
            tokens.Add(new DurationToken((slotsPerBeat / 2) + (slotsPerBeat / 4), Duration.Eighth, 1));
            tokens.Add(new DurationToken(slotsPerBeat / 2, Duration.Eighth, 0));
        }

        if (slotsPerBeat >= 4)
        {
            tokens.Add(new DurationToken((slotsPerBeat / 4) + (slotsPerBeat / 8), Duration.Sixteenth, 1));
            tokens.Add(new DurationToken(slotsPerBeat / 4, Duration.Sixteenth, 0));
        }

        if (slotsPerBeat >= 8)
        {
            int thirtySecondSlots = slotsPerBeat / 8;
            if (thirtySecondSlots > 0)
            {
                if (slotsPerBeat >= 16)
                    tokens.Add(new DurationToken(thirtySecondSlots + (slotsPerBeat / 16), Duration.ThirtySecond, 1));
                tokens.Add(new DurationToken(thirtySecondSlots, Duration.ThirtySecond, 0));
            }
        }

        if (slotsPerBeat >= 16)
            tokens.Add(new DurationToken(slotsPerBeat / 16, Duration.SixtyFourth, 0));

        if (slotsPerBeat >= 32)
            tokens.Add(new DurationToken(slotsPerBeat / 32, Duration.OneHundredTwentyEighth, 0));

        return tokens
            .Where(token => token.slots > 0)
            .Distinct()
            .OrderByDescending(token => token.slots)
            .ToArray();
    }

    private static NoteRenderContext BuildNoteRenderContext(List<RocksmithCachedNoteData> notes)
    {
        NoteRenderContext context = new NoteRenderContext();
        if (notes == null)
            return context;

        for (int i = 0; i < notes.Count; i++)
        {
            RocksmithCachedNoteData note = notes[i];
            if (note == null)
                continue;

            context.notesById[note.id] = note;
            if (note.linkedFromNoteId >= 0 && !context.legatoDestinationByOriginId.ContainsKey(note.linkedFromNoteId))
                context.legatoDestinationByOriginId[note.linkedFromNoteId] = note;
        }

        return context;
    }

    private static NoteRenderContext BuildNoteRenderContext(List<RocksmithCachedNoteData> notes, Dictionary<int, float> effectiveEndTimes)
    {
        NoteRenderContext context = BuildNoteRenderContext(notes);
        if (effectiveEndTimes != null)
        {
            foreach (KeyValuePair<int, float> pair in effectiveEndTimes)
                context.effectiveEndTimes[pair.Key] = pair.Value;
        }

        return context;
    }

    private static NoteTechnique ResolveLegatoTechnique(RocksmithCachedNoteData note, NoteRenderContext context)
    {
        if (note == null)
            return NoteTechnique.None;

        if (note.slideTargetFret >= 0)
            return NoteTechnique.Slide;

        if (note.isHammerOn)
            return NoteTechnique.HammerOn;
        if (note.isPullOff)
            return NoteTechnique.PullOff;

        if (note.technique >= (int)NoteTechnique.None && note.technique <= (int)NoteTechnique.Vibrato)
        {
            NoteTechnique resolved = (NoteTechnique)note.technique;
            if (resolved == NoteTechnique.HammerOn || resolved == NoteTechnique.PullOff || resolved == NoteTechnique.Slide)
                return resolved;
        }

        if (note.isLegato && note.linkedFromNoteId >= 0)
        {
            if (context != null &&
                context.notesById.TryGetValue(note.linkedFromNoteId, out RocksmithCachedNoteData previous) &&
                previous != null)
            {
                if (note.fret > previous.fret)
                    return NoteTechnique.HammerOn;
                if (note.fret < previous.fret)
                    return NoteTechnique.PullOff;
            }
        }

        return NoteTechnique.None;
    }

    private static int[] ResolveTuningPitches(RocksmithCachedArrangementPart part, RocksmithCachedArrangementSummary summary)
    {
        if (part?.tuningPitches != null && part.tuningPitches.Length > 0)
            return (int[])part.tuningPitches.Clone();
        if (summary?.tuningPitches != null && summary.tuningPitches.Length > 0)
            return (int[])summary.tuningPitches.Clone();

        bool preferBass =
            (!string.IsNullOrWhiteSpace(summary?.route) && summary.route.IndexOf("bass", StringComparison.OrdinalIgnoreCase) >= 0) ||
            (!string.IsNullOrWhiteSpace(part?.route) && part.route.IndexOf("bass", StringComparison.OrdinalIgnoreCase) >= 0);
        return StringTuningUtils.CloneOrDefault(null, preferBass);
    }

    private static int ResolveMidiChannel(int? sourceChannel, int fallback)
    {
        int channel = sourceChannel ?? fallback;
        if (channel < 0 || channel > 15)
            channel = fallback;
        return channel;
    }

    private static string BuildTrackShortName(string trackName)
    {
        if (string.IsNullOrWhiteSpace(trackName))
            return "Trk";

        string trimmed = trackName.Trim();
        return trimmed.Length <= 12 ? trimmed : trimmed.Substring(0, 12);
    }

    private static double ToAlphaTabBendValue(float semitoneStep)
    {
        return semitoneStep * 4d;
    }

    private static string SerializeTimingSidecar(RocksmithAlphaTabTimingSidecar sidecar)
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.Append("{\n");
        builder.Append("  \"version\": ").Append(sidecar?.version ?? 0).Append(",\n");
        builder.Append("  \"notationPath\": ").Append(QuoteJson(sidecar?.notationPath ?? string.Empty)).Append(",\n");
        builder.Append("  \"beats\": [\n");

        List<RocksmithAlphaTabTimingBeatEntry> beats = sidecar?.beats ?? new List<RocksmithAlphaTabTimingBeatEntry>();
        for (int i = 0; i < beats.Count; i++)
        {
            RocksmithAlphaTabTimingBeatEntry beat = beats[i] ?? new RocksmithAlphaTabTimingBeatEntry();
            builder.Append("    {\n");
            builder.Append("      \"startTime\": ").Append(FormatJsonFloat(beat.startTime)).Append(",\n");
            builder.Append("      \"endTime\": ").Append(FormatJsonFloat(beat.endTime)).Append(",\n");
            builder.Append("      \"isRest\": ").Append(beat.isRest ? "true" : "false").Append(",\n");
            builder.Append("      \"masterBarIndex\": ").Append(beat.masterBarIndex.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("      \"sourceEventId\": ").Append(beat.sourceEventId.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("      \"continuesFromPrevious\": ").Append(beat.continuesFromPrevious ? "true" : "false").Append(",\n");
            builder.Append("      \"continuesToNext\": ").Append(beat.continuesToNext ? "true" : "false").Append(",\n");
            builder.Append("      \"voiceIndex\": ").Append(beat.voiceIndex.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            builder.Append("      \"noteKey\": ").Append(QuoteJson(beat.noteKey ?? string.Empty)).Append('\n');
            builder.Append("    }");
            if (i < beats.Count - 1)
                builder.Append(',');
            builder.Append('\n');
        }

        builder.Append("  ]\n");
        builder.Append('}');
        return builder.ToString();
    }

    private static string QuoteJson(string value)
    {
        return $"\"{EscapeJsonString(value)}\"";
    }

    private static string EscapeJsonString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        StringBuilder builder = new StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    private static string FormatJsonFloat(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string BuildTimingNoteKey(IEnumerable<RocksmithCachedNoteData> notes)
    {
        if (notes == null)
            return "rest";

        List<string> parts = notes
            .Where(note => note != null)
            .OrderBy(note => note.stringIdx)
            .ThenBy(note => note.fret)
            .Select(note => $"{Math.Max(1, note.stringIdx + 1)}:{Math.Max(0, note.fret)}:{(note.isFretHandMute || note.isMuted ? 1 : 0)}")
            .ToList();

        return parts.Count == 0 ? "rest" : string.Join("|", parts);
    }

    private static float SampleBendStep(List<RocksmithCachedBendPointData> points, float timeSeconds, RocksmithCachedNoteData note)
    {
        if (points == null || points.Count == 0)
            return Mathf.Max(note?.bendStep ?? 0f, 0f);

        float initialStep = ResolveInitialBendStep(points, note);
        if (timeSeconds <= 0.0005f)
            return initialStep;

        if (timeSeconds < points[0].timeSeconds - 0.0005f)
            return initialStep;

        if (timeSeconds <= points[0].timeSeconds + 0.0005f)
        {
            float span = Math.Max(0.0001f, points[0].timeSeconds);
            float t = Mathf.Clamp01(timeSeconds / span);
            return Mathf.Lerp(initialStep, points[0].step, t);
        }

        for (int i = 1; i < points.Count; i++)
        {
            RocksmithCachedBendPointData previous = points[i - 1];
            RocksmithCachedBendPointData current = points[i];
            if (timeSeconds <= current.timeSeconds + 0.0005f)
            {
                float span = Math.Max(0.0001f, current.timeSeconds - previous.timeSeconds);
                float t = Mathf.Clamp01((timeSeconds - previous.timeSeconds) / span);
                return Mathf.Lerp(previous.step, current.step, t);
            }
        }

        return points[points.Count - 1].step;
    }

    private static float ResolveInitialBendStep(List<RocksmithCachedBendPointData> points, RocksmithCachedNoteData note)
    {
        if (points == null || points.Count == 0)
            return Mathf.Max(note?.bendStep ?? 0f, 0f);

        if (points[0].timeSeconds <= 0.0005f)
            return points[0].step;

        if (note != null && (note.bendPreBend || note.bendRelease))
            return Math.Max(points[0].step, Math.Max(note.bendStep, Math.Max(0f, note.maxBend)));

        return 0f;
    }

    private sealed class BuildState
    {
        public readonly Dictionary<int, CreatedNoteOccurrence> attackNoteBySourceId = new Dictionary<int, CreatedNoteOccurrence>();
        public readonly Dictionary<int, CreatedNoteOccurrence> activeTieBySourceId = new Dictionary<int, CreatedNoteOccurrence>();
        public readonly List<CreatedNoteOccurrence> noteOccurrences = new List<CreatedNoteOccurrence>();
    }

    private sealed class CreatedNoteOccurrence
    {
        public RocksmithCachedNoteData sourceNote;
        public Note renderedNote;
        public int sourceEventId = -1;
        public bool isAttackToken;
        public bool continuesFromPrevious;
        public bool continuesToNext;
    }

    private struct BendSlice
    {
        public BendType type;
        public List<BendPoint> points;
    }

    private sealed class BeatBuildInfo
    {
        public bool isRest;
        public int sourceEventId = -1;
        public DurationToken durationToken;
        public bool isPalmMute;
        public bool isLetRing;
        public VibratoType beatVibrato = VibratoType.None;
        public Duration? tremoloSpeed;
        public readonly List<NoteBuildInfo> notes = new List<NoteBuildInfo>();
    }

    private sealed class NoteBuildInfo
    {
        public RocksmithCachedNoteData sourceNote;
        public int stringNumber;
        public int fret;
        public bool isVisible = true;
        public bool isDead;
        public bool isPalmMute;
        public bool isTap;
        public bool isLetRing;
        public VibratoType vibrato;
        public AccentuationType accentuation;
        public HarmonicType harmonicType;
        public double harmonicValue;
        public BendType bendType = BendType.None;
        public List<BendPoint> bendPoints;
        public bool isAttackToken;
        public bool continuesFromPrevious;
        public bool continuesToNext;
    }

    private sealed class EventInfo
    {
        public readonly int sourceEventId;
        public readonly int chordId;
        public readonly float startTime;
        public float endTime;
        public int voiceIndex;
        public readonly List<RocksmithCachedNoteData> notes = new List<RocksmithCachedNoteData>();

        public EventInfo(RocksmithCachedNoteData note, int sourceEventId, Dictionary<int, float> effectiveEndTimes)
        {
            this.sourceEventId = sourceEventId;
            chordId = note.chordId;
            startTime = note.time;
            endTime = ResolveNoteEndTime(note, effectiveEndTimes);
        }

        public bool CanAccept(RocksmithCachedNoteData note)
        {
            return note != null &&
                   note.chordId == chordId &&
                   Math.Abs(note.time - startTime) <= 0.01f;
        }
    }

    private sealed class QuantizedEvent
    {
        public int startSlot;
        public int endSlot;
        public int sourceEventId = -1;
        public float sourceStartTime;
        public float sourceEndTime;
        public bool tieFromPrevious;
        public bool tieToNext;
        public List<RocksmithCachedNoteData> notes = new List<RocksmithCachedNoteData>();
        public int DurationSlots => Math.Max(1, endSlot - startSlot);
    }

    private sealed class EventSliceInfo
    {
        public int sourceEventId = -1;
        public int voiceIndex;
        public float startTime;
        public float endTime;
        public bool tieFromPrevious;
        public bool tieToNext;
        public List<RocksmithCachedNoteData> notes = new List<RocksmithCachedNoteData>();
    }

    private sealed class MeasureInfo
    {
        public readonly float startTime;
        public readonly float endTime;
        public readonly List<float> beatStarts;
        public readonly int beatCount;
        public readonly float tempoBpm;
        private int slotsPerBeat;

        public MeasureInfo(float startTime, float endTime, List<float> beatStarts, float tempoBpm)
        {
            this.startTime = startTime;
            this.endTime = endTime;
            this.beatStarts = beatStarts ?? new List<float>();
            beatCount = Math.Max(1, this.beatStarts.Count);
            this.tempoBpm = tempoBpm > 0.01f ? tempoBpm : 120f;
            slotsPerBeat = DefaultSlotsPerBeat;
        }

        public int DivisionsPerQuarter => slotsPerBeat;
        public int TotalSlots => GetTotalSlots(slotsPerBeat);

        public void SetSlotsPerBeat(int value)
        {
            slotsPerBeat = SupportedSlotsPerBeat.Contains(value) ? value : DefaultSlotsPerBeat;
        }

        public int GetTotalSlots(int resolvedSlotsPerBeat)
        {
            return Math.Max(1, beatCount * Math.Max(1, resolvedSlotsPerBeat));
        }

        public float[] BuildSlotTimes()
        {
            return BuildSlotTimes(slotsPerBeat);
        }

        public float[] BuildSlotTimes(int resolvedSlotsPerBeat)
        {
            int safeSlotsPerBeat = Math.Max(1, resolvedSlotsPerBeat);
            float[] result = new float[GetTotalSlots(safeSlotsPerBeat) + 1];
            for (int beatIndex = 0; beatIndex < beatCount; beatIndex++)
            {
                float beatStart = beatIndex < beatStarts.Count ? beatStarts[beatIndex] : startTime;
                float beatEnd = beatIndex + 1 < beatStarts.Count ? beatStarts[beatIndex + 1] : endTime;
                if (beatEnd <= beatStart + 0.0001f)
                    beatEnd = beatStart + ((endTime - startTime) / Math.Max(1, beatCount));

                for (int slotIndex = 0; slotIndex < safeSlotsPerBeat; slotIndex++)
                {
                    int absoluteIndex = (beatIndex * safeSlotsPerBeat) + slotIndex;
                    float t = slotIndex / (float)safeSlotsPerBeat;
                    result[absoluteIndex] = beatStart + ((beatEnd - beatStart) * t);
                }
            }

            result[result.Length - 1] = endTime;
            return result;
        }
    }

    private sealed class NoteRenderContext
    {
        public readonly Dictionary<int, RocksmithCachedNoteData> notesById = new Dictionary<int, RocksmithCachedNoteData>();
        public readonly Dictionary<int, RocksmithCachedNoteData> legatoDestinationByOriginId = new Dictionary<int, RocksmithCachedNoteData>();
        public readonly Dictionary<int, float> effectiveEndTimes = new Dictionary<int, float>();
    }

    private readonly struct DurationToken
    {
        public readonly int slots;
        public readonly Duration duration;
        public readonly int dotCount;

        public DurationToken(int slots, Duration duration, int dotCount)
        {
            this.slots = slots;
            this.duration = duration;
            this.dotCount = dotCount;
        }
    }
}
