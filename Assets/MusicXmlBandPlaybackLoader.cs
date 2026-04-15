using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

public static class MusicXmlBandPlaybackLoader
{
    private const float PreBendPitchPreRollSeconds = 0.075f;
    private const float BendRouteReleaseTailSeconds = 0.025f;

    private sealed class TempoEvent
    {
        public double quarterPos;
        public double bpm;

        public TempoEvent(double quarterPos, double bpm)
        {
            this.quarterPos = quarterPos;
            this.bpm = bpm;
        }
    }

    private sealed class PartMetadata
    {
        public string id;
        public string name;
        public string instrumentName;
        public int midiChannel = -1;
        public int midiProgram = -1;
        public int preferredBank = -1;
        public bool isDrum;
        public bool isGuitarFamily;
        public bool isExplicitHarmonicPart;
        public bool usesPercussionClef;
        public bool usesMidiUnpitched;
    }

    private sealed class ParsedPlaybackNote
    {
        public string partId;
        public string partName;
        public int staff;
        public int voice;
        public int stringNumber = -1;
        public int fret = -1;
        public bool fromTab;
        public double quarterPos;
        public double durationQuarter;
        public int midi;
        public int velocity;
        public bool tieStart;
        public bool tieStop;
        public bool slideStart;
        public bool slideStop;
        public bool hammerOnStart;
        public bool hammerOnStop;
        public bool pullOffStart;
        public bool pullOffStop;
        public bool vibrato;
        public GeneratedLegatoTransitionKind legatoTransitionKind;
        public float attackVelocityScale = 1f;
        public float vibratoDepthSemitones;
        public float vibratoRateHz;
        public float vibratoDelayNormalized;
        public float vibratoFadeNormalized;
        public float pitchPreRollSeconds;
        public GeneratedTechniqueVariant techniqueVariant;
        public int pitchBendRangeSemitones;
        public List<GeneratedPlaybackPitchPoint> pitchCurve = new List<GeneratedPlaybackPitchPoint>();

        public double EndQuarterPos => quarterPos + durationQuarter;
    }

    private readonly struct RouteKey : IEquatable<RouteKey>
    {
        public readonly string partId;
        public readonly bool isDrum;
        public readonly int bank;
        public readonly int preset;
        public readonly int pitchBendRangeSemitones;

        public RouteKey(string partId, bool isDrum, int bank, int preset, int pitchBendRangeSemitones)
        {
            this.partId = partId ?? string.Empty;
            this.isDrum = isDrum;
            this.bank = bank;
            this.preset = preset;
            this.pitchBendRangeSemitones = pitchBendRangeSemitones;
        }

        public bool Equals(RouteKey other)
        {
            return string.Equals(partId, other.partId, StringComparison.OrdinalIgnoreCase) &&
                   isDrum == other.isDrum &&
                   bank == other.bank &&
                   preset == other.preset &&
                   pitchBendRangeSemitones == other.pitchBendRangeSemitones;
        }

        public override bool Equals(object obj)
        {
            return obj is RouteKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(partId ?? string.Empty);
                hash = (hash * 397) ^ (isDrum ? 1 : 0);
                hash = (hash * 397) ^ bank;
                hash = (hash * 397) ^ preset;
                hash = (hash * 397) ^ pitchBendRangeSemitones;
                return hash;
            }
        }
    }

    private sealed class RouteDescriptor
    {
        public int channel;
        public int bank;
        public int preset;
        public bool isDrum;
        public string label;
        public string sourcePartId;
        public string sourcePartName;
        public int pitchBendRangeSemitones;
    }

    private sealed class RouteLaneState
    {
        public RouteDescriptor descriptor;
        public float occupiedUntilSeconds;
    }

    public static GeneratedPlaybackArrangement LoadArrangement(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            Debug.LogWarning($"[GeneratedSong] MusicXML not found for generated playback: {filePath}");
            return null;
        }

        try
        {
            XDocument document = XDocument.Load(filePath);
            XElement root = document.Root;
            if (root == null)
            {
                Debug.LogWarning($"[GeneratedSong] Invalid MusicXML root in: {filePath}");
                return null;
            }

            Dictionary<string, PartMetadata> partMetadataById = ReadPartMetadata(root);
            List<XElement> parts = root.Elements().Where(e => e.Name.LocalName == "part").ToList();
            if (parts.Count == 0)
            {
                Debug.LogWarning($"[GeneratedSong] No <part> elements found in: {filePath}");
                return null;
            }

            List<double> canonicalMeasureStarts = BuildCanonicalMeasureStarts(parts);
            List<TempoEvent> tempoMap = BuildGlobalTempoMap(parts, canonicalMeasureStarts);
            int harmonicPreset = ResolveHarmonicPreset(partMetadataById.Values);

            List<GeneratedPlaybackPartInfo> partInfos = new List<GeneratedPlaybackPartInfo>(parts.Count);
            List<RouteDescriptor> routes = new List<RouteDescriptor>();
            List<GeneratedPlaybackNoteEvent> noteEvents = new List<GeneratedPlaybackNoteEvent>(4096);
            float durationSeconds = 0f;

            Dictionary<RouteKey, RouteDescriptor> sharedRoutes = new Dictionary<RouteKey, RouteDescriptor>();
            Dictionary<RouteKey, List<RouteLaneState>> pitchRouteLanes = new Dictionary<RouteKey, List<RouteLaneState>>();
            HashSet<int> usedChannels = new HashSet<int> { 9 };

            for (int i = 0; i < parts.Count; i++)
            {
                XElement partElement = parts[i];
                string partId = Attr(partElement, "id");
                if (!partMetadataById.TryGetValue(partId, out PartMetadata metadata))
                {
                    metadata = new PartMetadata
                    {
                        id = partId,
                        name = string.IsNullOrWhiteSpace(partId) ? $"Part {i + 1}" : partId
                    };
                }

                List<ParsedPlaybackNote> parsedNotes = ParsePart(partElement, metadata, canonicalMeasureStarts);
                if (parsedNotes.Count == 0)
                {
                    partInfos.Add(ToPartInfo(metadata));
                    continue;
                }

                IEnumerable<ParsedPlaybackNote> filteredNotes = FilterPartStaves(parsedNotes);
                partInfos.Add(ToPartInfo(metadata));

                foreach (ParsedPlaybackNote note in filteredNotes.OrderBy(n => n.quarterPos))
                {
                    int preset = ResolvePresetForNote(metadata, note.techniqueVariant, harmonicPreset);
                    RouteKey routeKey = new RouteKey(metadata.id, metadata.isDrum, metadata.preferredBank, preset, note.pitchBendRangeSemitones);
                    float startTimeSeconds = (float)QuarterToSeconds(note.quarterPos, tempoMap);
                    float endTimeSeconds = (float)QuarterToSeconds(note.quarterPos + note.durationQuarter, tempoMap);
                    float noteDurationSeconds = Mathf.Max(0.03f, endTimeSeconds - startTimeSeconds);
                    RouteDescriptor route = note.pitchBendRangeSemitones > 0
                        ? ResolvePitchRouteDescriptor(
                            routeKey,
                            metadata,
                            note.techniqueVariant,
                            preset,
                            startTimeSeconds,
                            noteDurationSeconds,
                            note.pitchPreRollSeconds,
                            usedChannels,
                            routes,
                            pitchRouteLanes)
                        : ResolveSharedRouteDescriptor(
                            routeKey,
                            metadata,
                            note.techniqueVariant,
                            preset,
                            usedChannels,
                            routes,
                            sharedRoutes);

                    if (route == null)
                    {
                        Debug.LogWarning($"[GeneratedSong] Ran out of MIDI channels while building generated playback for '{filePath}'.");
                        continue;
                    }

                    int eventMidiNote = note.midi;
                    float eventPitchPreRollSeconds = note.pitchPreRollSeconds;
                    List<GeneratedPlaybackPitchPoint> eventPitchCurve = note.pitchCurve != null
                        ? note.pitchCurve.Select(point => new GeneratedPlaybackPitchPoint
                        {
                            normalizedTime = point.normalizedTime,
                            semitoneOffset = point.semitoneOffset
                        }).ToList()
                        : new List<GeneratedPlaybackPitchPoint>();

                    if (TryNormalizePreBendAttackPitch(ref eventMidiNote, ref eventPitchPreRollSeconds, eventPitchCurve))
                    {
                        // Pre-bent attacks sound much clearer on GM synths if the attack starts on the
                        // actually heard pitch, then the release/re-bend motion happens relative to it.
                    }

                    StretchEarlyMultiStepPitchCurve(eventPitchCurve, noteDurationSeconds);

                    noteEvents.Add(new GeneratedPlaybackNoteEvent
                    {
                        startTimeSeconds = startTimeSeconds,
                        durationSeconds = noteDurationSeconds,
                        pitchPreRollSeconds = eventPitchPreRollSeconds,
                        midiNote = eventMidiNote,
                        velocity = Mathf.Clamp(note.velocity, 1, 127),
                        channel = route.channel,
                        partId = metadata.id,
                        partName = metadata.name,
                        techniqueVariant = note.techniqueVariant,
                        legatoTransitionKind = note.legatoTransitionKind,
                        attackVelocityScale = note.attackVelocityScale,
                        vibratoDepthSemitones = note.vibratoDepthSemitones,
                        vibratoRateHz = note.vibratoRateHz,
                        vibratoDelayNormalized = note.vibratoDelayNormalized,
                        vibratoFadeNormalized = note.vibratoFadeNormalized,
                        pitchBendRangeSemitones = note.pitchBendRangeSemitones,
                        pitchCurve = eventPitchCurve
                    });

                    durationSeconds = Mathf.Max(durationSeconds, startTimeSeconds + noteDurationSeconds);
                }
            }

            noteEvents = noteEvents
                .OrderBy(n => n.startTimeSeconds)
                .ThenBy(n => n.channel)
                .ThenBy(n => n.midiNote)
                .ToList();

            GeneratedPlaybackArrangement arrangement = new GeneratedPlaybackArrangement
            {
                sourcePath = filePath,
                durationSeconds = durationSeconds,
                parts = partInfos,
                channelAssignments = routes.Select(route => new GeneratedPlaybackChannelAssignment
                {
                    channel = route.channel,
                    bank = route.bank,
                    preset = route.preset,
                    isDrum = route.isDrum,
                    label = route.label,
                    sourcePartId = route.sourcePartId,
                    sourcePartName = route.sourcePartName,
                    pitchBendRangeSemitones = route.pitchBendRangeSemitones
                }).OrderBy(route => route.channel).ToList(),
                notes = noteEvents
            };

            Debug.Log($"[GeneratedSong] Built generated arrangement from '{Path.GetFileName(filePath)}' with {arrangement.notes.Count} note events across {arrangement.channelAssignments.Count} channels.");
            return arrangement;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GeneratedSong] Failed to build generated arrangement from '{filePath}': {ex}");
            return null;
        }
    }

    private static GeneratedPlaybackPartInfo ToPartInfo(PartMetadata metadata)
    {
        return new GeneratedPlaybackPartInfo
        {
            partId = metadata.id,
            displayName = metadata.name,
            instrumentName = metadata.instrumentName,
            sourceMidiChannel = metadata.midiChannel,
            sourceMidiProgram = metadata.midiProgram,
            preferredBank = metadata.preferredBank,
            isDrum = metadata.isDrum,
            isGuitarFamily = metadata.isGuitarFamily,
            isExplicitHarmonicPart = metadata.isExplicitHarmonicPart
        };
    }

    private static IEnumerable<ParsedPlaybackNote> FilterPartStaves(List<ParsedPlaybackNote> notes)
    {
        bool hasTab = notes.Any(note => note.fromTab);
        if (!hasTab)
            return notes;

        int preferredStaff = notes
            .GroupBy(note => note.staff)
            .Select(group => new
            {
                staff = group.Key,
                noteCount = group.Count(),
                tabCount = group.Count(note => note.fromTab)
            })
            .OrderByDescending(group => group.tabCount)
            .ThenByDescending(group => group.noteCount)
            .First().staff;

        return notes.Where(note => note.staff == preferredStaff);
    }

    private static Dictionary<string, PartMetadata> ReadPartMetadata(XElement root)
    {
        Dictionary<string, PartMetadata> result = new Dictionary<string, PartMetadata>(StringComparer.OrdinalIgnoreCase);
        XElement partList = root.Elements().FirstOrDefault(e => e.Name.LocalName == "part-list");
        if (partList == null)
            return result;

        foreach (XElement scorePart in partList.Elements().Where(e => e.Name.LocalName == "score-part"))
        {
            string id = Attr(scorePart, "id");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            string name = ChildValue(scorePart, "part-name");
            XElement scoreInstrument = scorePart.Elements().FirstOrDefault(e => e.Name.LocalName == "score-instrument");
            string instrumentName = ChildValue(scoreInstrument, "instrument-name");
            XElement midiInstrument = scorePart.Elements().FirstOrDefault(e => e.Name.LocalName == "midi-instrument");
            int midiChannel = ParseInt(ChildValue(midiInstrument, "midi-channel"), -1);
            int midiProgram = ParseInt(ChildValue(midiInstrument, "midi-program"), -1);
            int midiBank = ParseInt(ChildValue(midiInstrument, "midi-bank"), -1);
            bool usesMidiUnpitched = scorePart.Descendants().Any(e => e.Name.LocalName == "midi-unpitched");

            string searchableText = $"{name} {instrumentName}".ToLowerInvariant();
            result[id] = new PartMetadata
            {
                id = id,
                name = string.IsNullOrWhiteSpace(name) ? id : name.Trim(),
                instrumentName = string.IsNullOrWhiteSpace(instrumentName) ? string.Empty : instrumentName.Trim(),
                midiChannel = midiChannel,
                midiProgram = midiProgram,
                preferredBank = midiBank > 0 ? midiBank : -1,
                isDrum = midiChannel == 10 || usesMidiUnpitched || searchableText.Contains("drum") || searchableText.Contains("percussion"),
                isGuitarFamily = searchableText.Contains("guitar"),
                isExplicitHarmonicPart = searchableText.Contains("harm"),
                usesMidiUnpitched = usesMidiUnpitched
            };
        }

        return result;
    }

    private static List<ParsedPlaybackNote> ParsePart(XElement part, PartMetadata metadata, List<double> canonicalMeasureStarts)
    {
        List<ParsedPlaybackNote> notes = new List<ParsedPlaybackNote>();
        double divisions = 1.0;
        int chromaticTranspose = 0;
        int measureIndex = 0;
        bool usesPercussionClef = metadata.isDrum || metadata.usesPercussionClef;

        foreach (XElement measure in part.Elements().Where(e => e.Name.LocalName == "measure"))
        {
            double currentMeasureStartQuarter = measureIndex < canonicalMeasureStarts.Count
                ? canonicalMeasureStarts[measureIndex]
                : (canonicalMeasureStarts.Count > 0 ? canonicalMeasureStarts[canonicalMeasureStarts.Count - 1] : 0.0);

            Dictionary<string, double> voiceCursorOffsets = new Dictionary<string, double>();
            Dictionary<string, double> lastNoteStartByVoice = new Dictionary<string, double>();
            string activeVoiceKey = "1:1";

            foreach (XElement child in measure.Elements())
            {
                string local = child.Name.LocalName;

                if (local == "attributes")
                {
                    XElement divNode = child.Elements().FirstOrDefault(e => e.Name.LocalName == "divisions");
                    if (divNode != null && double.TryParse(divNode.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedDiv) && parsedDiv > 0.0)
                        divisions = parsedDiv;

                    XElement transposeNode = child.Elements().FirstOrDefault(e => e.Name.LocalName == "transpose");
                    if (transposeNode != null)
                        chromaticTranspose = ParseInt(ChildValue(transposeNode, "chromatic"), chromaticTranspose);

                    if (child.Descendants().Any(e =>
                            e.Name.LocalName == "sign" &&
                            string.Equals((e.Value ?? string.Empty).Trim(), "percussion", StringComparison.OrdinalIgnoreCase)))
                    {
                        usesPercussionClef = true;
                        metadata.usesPercussionClef = true;
                        metadata.isDrum = true;
                    }
                }
                else if (local == "backup")
                {
                    double currentOffset = voiceCursorOffsets.TryGetValue(activeVoiceKey, out double storedOffset) ? storedOffset : 0.0;
                    voiceCursorOffsets[activeVoiceKey] = Math.Max(0.0, currentOffset - DurationNodeToQuarter(child, divisions));
                }
                else if (local == "forward")
                {
                    double currentOffset = voiceCursorOffsets.TryGetValue(activeVoiceKey, out double storedOffset) ? storedOffset : 0.0;
                    voiceCursorOffsets[activeVoiceKey] = currentOffset + DurationNodeToQuarter(child, divisions);
                }
                else if (local == "note")
                {
                    bool isRest = child.Elements().Any(e => e.Name.LocalName == "rest");
                    bool isChordTone = child.Elements().Any(e => e.Name.LocalName == "chord");
                    bool isGrace = child.Elements().Any(e => e.Name.LocalName == "grace");
                    int staff = ParseInt(ChildValue(child, "staff"), 1);
                    int voice = ParseInt(ChildValue(child, "voice"), 1);
                    string voiceKey = $"{voice}:{staff}";

                    if (!voiceCursorOffsets.TryGetValue(voiceKey, out double currentOffset))
                    {
                        currentOffset = voiceCursorOffsets.TryGetValue(activeVoiceKey, out double activeOffset) ? activeOffset : 0.0;
                        voiceCursorOffsets[voiceKey] = currentOffset;
                    }

                    double noteStartQuarter;
                    if (isChordTone)
                    {
                        if (!lastNoteStartByVoice.TryGetValue(voiceKey, out noteStartQuarter))
                            noteStartQuarter = currentMeasureStartQuarter + currentOffset;
                    }
                    else
                    {
                        noteStartQuarter = currentMeasureStartQuarter + currentOffset;
                        lastNoteStartByVoice[voiceKey] = noteStartQuarter;
                    }

                    double durationQuarter = isGrace ? 0.0 : DurationNodeToQuarter(child, divisions);
                    if (!isRest && TryReadNotePitch(
                            child,
                            chromaticTranspose,
                            usesPercussionClef || metadata.usesMidiUnpitched,
                            out int midi,
                            out bool fromTab,
                            out int stringNumber,
                            out int fret))
                    {
                        ParsedPlaybackNote parsedNote = new ParsedPlaybackNote
                        {
                            partId = metadata.id,
                            partName = metadata.name,
                            staff = staff,
                            voice = voice,
                            stringNumber = stringNumber,
                            fret = fret,
                            fromTab = fromTab,
                            quarterPos = noteStartQuarter,
                            durationQuarter = durationQuarter,
                            midi = midi,
                            velocity = ParseVelocity(child)
                        };

                        ParseTechniqueInfo(child, metadata, parsedNote);
                        ParsePitchCurve(child, out List<GeneratedPlaybackPitchPoint> pitchCurve, out int pitchBendRangeSemitones, out float pitchPreRollSeconds);
                        parsedNote.pitchBendRangeSemitones = pitchBendRangeSemitones;
                        parsedNote.pitchCurve = pitchCurve;
                        parsedNote.pitchPreRollSeconds = pitchPreRollSeconds;
                        notes.Add(parsedNote);
                    }

                    if (!isChordTone)
                        voiceCursorOffsets[voiceKey] = currentOffset + durationQuarter;

                    activeVoiceKey = voiceKey;
                }
            }

            measureIndex++;
        }

        List<ParsedPlaybackNote> mergedNotes = MergeTiedNotes(notes);
        ApplyExpressivePlaybackTechniques(mergedNotes, metadata);
        return mergedNotes;
    }

    private static List<ParsedPlaybackNote> MergeTiedNotes(List<ParsedPlaybackNote> notes)
    {
        List<ParsedPlaybackNote> normalized = new List<ParsedPlaybackNote>();

        foreach (ParsedPlaybackNote current in notes.OrderBy(note => note.quarterPos).ThenBy(note => note.midi))
        {
            if (current.tieStop)
            {
                ParsedPlaybackNote previous = normalized.LastOrDefault(note =>
                    note.partId == current.partId &&
                    note.staff == current.staff &&
                    note.voice == current.voice &&
                    note.midi == current.midi &&
                    note.quarterPos <= current.quarterPos + 0.00001);
                if (previous != null)
                {
                    double previousDurationQuarter = previous.durationQuarter;
                    previous.durationQuarter += current.durationQuarter;
                    previous.pitchCurve = MergeTiedPitchCurves(previous.pitchCurve, previousDurationQuarter, current.pitchCurve, current.durationQuarter);
                    previous.tieStart |= current.tieStart;
                    if ((int)current.techniqueVariant > (int)previous.techniqueVariant)
                        previous.techniqueVariant = current.techniqueVariant;
                    previous.pitchBendRangeSemitones = Mathf.Max(
                        previous.pitchBendRangeSemitones,
                        current.pitchBendRangeSemitones,
                        CalculatePitchCurveRange(previous.pitchCurve));
                    previous.velocity = Mathf.Max(previous.velocity, current.velocity);
                    previous.vibrato |= current.vibrato;
                    previous.vibratoDepthSemitones = Mathf.Max(previous.vibratoDepthSemitones, current.vibratoDepthSemitones);
                    previous.vibratoRateHz = Mathf.Max(previous.vibratoRateHz, current.vibratoRateHz);
                    previous.vibratoDelayNormalized = Mathf.Min(previous.vibratoDelayNormalized <= 0f ? 1f : previous.vibratoDelayNormalized,
                        current.vibratoDelayNormalized <= 0f ? 1f : current.vibratoDelayNormalized);
                    previous.vibratoFadeNormalized = Mathf.Max(previous.vibratoFadeNormalized, current.vibratoFadeNormalized);
                    continue;
                }
            }

            normalized.Add(current);
        }

        return normalized;
    }

    private static List<GeneratedPlaybackPitchPoint> MergeTiedPitchCurves(
        List<GeneratedPlaybackPitchPoint> previousCurve,
        double previousDurationQuarter,
        List<GeneratedPlaybackPitchPoint> currentCurve,
        double currentDurationQuarter)
    {
        float previousDuration = Mathf.Max(0.0001f, (float)previousDurationQuarter);
        float currentDuration = Mathf.Max(0.0001f, (float)currentDurationQuarter);
        float totalDuration = previousDuration + currentDuration;

        List<GeneratedPlaybackPitchPoint> normalizedPrevious = NormalizeSegmentCurve(previousCurve);
        float carryInOffset = normalizedPrevious[normalizedPrevious.Count - 1].semitoneOffset;
        List<GeneratedPlaybackPitchPoint> normalizedCurrent = NormalizeContinuationCurve(currentCurve, carryInOffset);

        List<GeneratedPlaybackPitchPoint> merged = new List<GeneratedPlaybackPitchPoint>(
            normalizedPrevious.Count + normalizedCurrent.Count + 2);

        AppendScaledCurve(merged, normalizedPrevious, 0f, previousDuration / totalDuration);
        AppendScaledCurve(merged, normalizedCurrent, previousDuration / totalDuration, currentDuration / totalDuration);

        if (merged.Count == 0)
        {
            merged.Add(new GeneratedPlaybackPitchPoint { normalizedTime = 0f, semitoneOffset = 0f });
            merged.Add(new GeneratedPlaybackPitchPoint { normalizedTime = 1f, semitoneOffset = 0f });
        }
        else if (merged[merged.Count - 1].normalizedTime < 0.9995f)
        {
            merged.Add(new GeneratedPlaybackPitchPoint
            {
                normalizedTime = 1f,
                semitoneOffset = merged[merged.Count - 1].semitoneOffset
            });
        }

        return merged;
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
            normalized[normalized.Count - 1].normalizedTime = 1f;
        }

        return normalized;
    }

    private static List<GeneratedPlaybackPitchPoint> NormalizeContinuationCurve(List<GeneratedPlaybackPitchPoint> curve, float carryInOffset)
    {
        List<GeneratedPlaybackPitchPoint> normalized = ClonePitchCurve(curve);
        if (normalized.Count == 0)
        {
            normalized.Add(new GeneratedPlaybackPitchPoint { normalizedTime = 0f, semitoneOffset = carryInOffset });
            normalized.Add(new GeneratedPlaybackPitchPoint { normalizedTime = 1f, semitoneOffset = carryInOffset });
            return normalized;
        }

        if (normalized[0].normalizedTime > 0.0005f)
        {
            normalized.Insert(0, new GeneratedPlaybackPitchPoint
            {
                normalizedTime = 0f,
                semitoneOffset = carryInOffset
            });
        }
        else
        {
            normalized[0].normalizedTime = 0f;
            if (Mathf.Approximately(normalized[0].semitoneOffset, 0f) && !Mathf.Approximately(carryInOffset, 0f))
                normalized[0].semitoneOffset = carryInOffset;
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
            normalized[normalized.Count - 1].normalizedTime = 1f;
        }

        return normalized;
    }

    private static List<GeneratedPlaybackPitchPoint> ClonePitchCurve(List<GeneratedPlaybackPitchPoint> curve)
    {
        List<GeneratedPlaybackPitchPoint> cloned = new List<GeneratedPlaybackPitchPoint>();
        if (curve == null)
            return cloned;

        for (int i = 0; i < curve.Count; i++)
        {
            GeneratedPlaybackPitchPoint point = curve[i];
            if (point == null)
                continue;

            cloned.Add(new GeneratedPlaybackPitchPoint
            {
                normalizedTime = Mathf.Clamp01(point.normalizedTime),
                semitoneOffset = point.semitoneOffset
            });
        }

        cloned.Sort((left, right) => left.normalizedTime.CompareTo(right.normalizedTime));
        return cloned;
    }

    private static void AppendScaledCurve(List<GeneratedPlaybackPitchPoint> target, List<GeneratedPlaybackPitchPoint> source, float segmentOffset, float segmentScale)
    {
        for (int i = 0; i < source.Count; i++)
        {
            GeneratedPlaybackPitchPoint point = source[i];
            float scaledTime = segmentOffset + (point.normalizedTime * segmentScale);
            AddOrReplacePitchPoint(target, scaledTime, point.semitoneOffset);
        }
    }

    private static void AddOrReplacePitchPoint(List<GeneratedPlaybackPitchPoint> target, float normalizedTime, float semitoneOffset)
    {
        float clampedTime = Mathf.Clamp01(normalizedTime);
        if (target.Count > 0 && Mathf.Abs(target[target.Count - 1].normalizedTime - clampedTime) <= 0.0005f)
        {
            target[target.Count - 1].normalizedTime = clampedTime;
            target[target.Count - 1].semitoneOffset = semitoneOffset;
            return;
        }

        target.Add(new GeneratedPlaybackPitchPoint
        {
            normalizedTime = clampedTime,
            semitoneOffset = semitoneOffset
        });
    }

    private static int CalculatePitchCurveRange(List<GeneratedPlaybackPitchPoint> curve)
    {
        if (curve == null || curve.Count == 0)
            return 0;

        float maxOffset = 0f;
        for (int i = 0; i < curve.Count; i++)
            maxOffset = Mathf.Max(maxOffset, Mathf.Abs(curve[i].semitoneOffset));

        return Mathf.CeilToInt(maxOffset);
    }

    private static void ApplyExpressivePlaybackTechniques(List<ParsedPlaybackNote> notes, PartMetadata metadata)
    {
        if (notes == null || notes.Count == 0)
            return;

        foreach (IGrouping<string, ParsedPlaybackNote> group in notes
                     .GroupBy(note => $"{note.partId}|{note.staff}|{note.voice}|{note.stringNumber}"))
        {
            List<ParsedPlaybackNote> ordered = group.OrderBy(note => note.quarterPos).ToList();
            for (int i = 1; i < ordered.Count; i++)
            {
                ParsedPlaybackNote previous = ordered[i - 1];
                ParsedPlaybackNote current = ordered[i];
                if (!CanLinkExpressiveNotes(previous, current))
                    continue;

                if (previous.slideStart && current.slideStop)
                {
                    ApplyLegatoTransition(current, previous, GeneratedLegatoTransitionKind.Slide);
                }
                else if (previous.hammerOnStart && current.hammerOnStop)
                {
                    ApplyLegatoTransition(current, previous, GeneratedLegatoTransitionKind.HammerOn);
                }
                else if (previous.pullOffStart && current.pullOffStop)
                {
                    ApplyLegatoTransition(current, previous, GeneratedLegatoTransitionKind.PullOff);
                }
            }
        }

        if (!metadata.isDrum)
        {
            for (int i = 0; i < notes.Count; i++)
            {
                ParsedPlaybackNote note = notes[i];
                if (!note.vibrato)
                    continue;

                note.vibratoDepthSemitones = Mathf.Max(note.vibratoDepthSemitones, metadata.isGuitarFamily ? 0.28f : 0.18f);
                note.vibratoRateHz = Mathf.Max(note.vibratoRateHz, metadata.isGuitarFamily ? 5.6f : 5.0f);
                note.vibratoDelayNormalized = note.vibratoDelayNormalized > 0f ? note.vibratoDelayNormalized : 0.18f;
                note.vibratoFadeNormalized = note.vibratoFadeNormalized > 0f ? note.vibratoFadeNormalized : 0.14f;
                note.pitchBendRangeSemitones = Mathf.Max(note.pitchBendRangeSemitones, Mathf.CeilToInt(note.vibratoDepthSemitones));
            }
        }
    }

    private static bool CanLinkExpressiveNotes(ParsedPlaybackNote previous, ParsedPlaybackNote current)
    {
        if (previous == null || current == null)
            return false;

        if (previous.midi == current.midi)
            return false;

        if (previous.partId != current.partId || previous.staff != current.staff || previous.voice != current.voice)
            return false;

        if (previous.stringNumber > 0 && current.stringNumber > 0 && previous.stringNumber != current.stringNumber)
            return false;

        double gapQuarter = current.quarterPos - previous.EndQuarterPos;
        return gapQuarter >= -0.02 && gapQuarter <= 0.35;
    }

    private static void ApplyLegatoTransition(ParsedPlaybackNote target, ParsedPlaybackNote previous, GeneratedLegatoTransitionKind kind)
    {
        target.legatoTransitionKind = kind;
        float sourceRelativeOffset = previous.midi - target.midi;
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

    private static float GetPitchCurveStartOffset(List<GeneratedPlaybackPitchPoint> pitchCurve)
    {
        if (pitchCurve == null || pitchCurve.Count == 0)
            return 0f;

        return NormalizeSegmentCurve(pitchCurve)[0].semitoneOffset;
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

    private static void ParseTechniqueInfo(XElement noteNode, PartMetadata metadata, ParsedPlaybackNote parsedNote)
    {
        parsedNote.tieStart = noteNode.Elements().Any(e => e.Name.LocalName == "tie" && string.Equals(Attr(e, "type"), "start", StringComparison.OrdinalIgnoreCase));
        parsedNote.tieStop = noteNode.Elements().Any(e => e.Name.LocalName == "tie" && string.Equals(Attr(e, "type"), "stop", StringComparison.OrdinalIgnoreCase));

        bool isPalmMute = IsMuteType(noteNode, "palm");
        bool isStraightMute = IsStraightMutedNote(noteNode);
        bool isHarmonic = HasHarmonicNotation(noteNode) || metadata.isExplicitHarmonicPart;

        XElement notations = noteNode.Elements().FirstOrDefault(e => e.Name.LocalName == "notations");
        if (notations != null)
        {
            parsedNote.tieStart |= notations.Descendants().Any(e => e.Name.LocalName == "tied" && string.Equals(Attr(e, "type"), "start", StringComparison.OrdinalIgnoreCase));
            parsedNote.tieStop |= notations.Descendants().Any(e => e.Name.LocalName == "tied" && string.Equals(Attr(e, "type"), "stop", StringComparison.OrdinalIgnoreCase));
            parsedNote.slideStart = notations.Descendants().Any(e =>
                (e.Name.LocalName == "slide" || e.Name.LocalName == "glissando") &&
                string.Equals(Attr(e, "type"), "start", StringComparison.OrdinalIgnoreCase));
            parsedNote.slideStop = notations.Descendants().Any(e =>
                (e.Name.LocalName == "slide" || e.Name.LocalName == "glissando") &&
                string.Equals(Attr(e, "type"), "stop", StringComparison.OrdinalIgnoreCase));
        }

        XElement technical = noteNode.Descendants().FirstOrDefault(e => e.Name.LocalName == "technical");
        if (technical != null)
        {
            parsedNote.hammerOnStart = technical.Elements().Any(e => e.Name.LocalName == "hammer-on" && string.Equals(Attr(e, "type"), "start", StringComparison.OrdinalIgnoreCase));
            parsedNote.hammerOnStop = technical.Elements().Any(e => e.Name.LocalName == "hammer-on" && string.Equals(Attr(e, "type"), "stop", StringComparison.OrdinalIgnoreCase));
            parsedNote.pullOffStart = technical.Elements().Any(e => e.Name.LocalName == "pull-off" && string.Equals(Attr(e, "type"), "start", StringComparison.OrdinalIgnoreCase));
            parsedNote.pullOffStop = technical.Elements().Any(e => e.Name.LocalName == "pull-off" && string.Equals(Attr(e, "type"), "stop", StringComparison.OrdinalIgnoreCase));
            parsedNote.vibrato = technical.Descendants().Any(e =>
                e.Name.LocalName == "other-technical" &&
                (e.Value ?? string.Empty).IndexOf("vibr", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        parsedNote.vibrato |= noteNode.Descendants().Any(e =>
            e.Name.LocalName == "wavy-line" ||
            (e.Name.LocalName == "other-ornament" && (e.Value ?? string.Empty).IndexOf("vibr", StringComparison.OrdinalIgnoreCase) >= 0));

        if (isHarmonic)
            parsedNote.techniqueVariant = GeneratedTechniqueVariant.Harmonic;
        else if (isPalmMute)
            parsedNote.techniqueVariant = GeneratedTechniqueVariant.PalmMute;
        else if (isStraightMute)
            parsedNote.techniqueVariant = GeneratedTechniqueVariant.StraightMute;
        else
            parsedNote.techniqueVariant = GeneratedTechniqueVariant.Normal;
    }

    private static void ParsePitchCurve(
        XElement noteNode,
        out List<GeneratedPlaybackPitchPoint> pitchCurve,
        out int pitchBendRangeSemitones,
        out float pitchPreRollSeconds)
    {
        pitchCurve = new List<GeneratedPlaybackPitchPoint>();
        pitchBendRangeSemitones = 0;
        pitchPreRollSeconds = 0f;

        List<XElement> bendNodes = noteNode
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "bend", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (bendNodes.Count == 0)
            return;

        List<float> bendTargets = new List<float>(bendNodes.Count);
        List<bool> bendIsRelease = new List<bool>(bendNodes.Count);
        bool firstIsPreBend = false;
        float currentOffset = 0f;
        for (int i = 0; i < bendNodes.Count; i++)
        {
            XElement bendNode = bendNodes[i];
            string alterText = ChildValue(bendNode, "bend-alter");
            if (!float.TryParse(alterText, NumberStyles.Any, CultureInfo.InvariantCulture, out float bendAlter))
                bendAlter = 0f;

            bool isRelease = bendNode.Elements().Any(element => string.Equals(element.Name.LocalName, "release", StringComparison.OrdinalIgnoreCase));
            bool isPreBend = bendNode.Elements().Any(element => string.Equals(element.Name.LocalName, "pre-bend", StringComparison.OrdinalIgnoreCase));
            float targetOffset;
            if (isPreBend)
            {
                targetOffset = Mathf.Abs(bendAlter);
            }
            else if (isRelease && bendAlter < 0f)
            {
                targetOffset = currentOffset + bendAlter;
            }
            else
            {
                targetOffset = bendAlter;
            }

            bendTargets.Add(targetOffset);
            bendIsRelease.Add(isRelease);
            pitchBendRangeSemitones = Mathf.Max(pitchBendRangeSemitones, Mathf.CeilToInt(Mathf.Abs(targetOffset)));

            if (i == 0 && isPreBend)
                firstIsPreBend = true;

            currentOffset = targetOffset;
        }

        if (pitchBendRangeSemitones <= 0)
            return;

        float startOffset = firstIsPreBend && bendTargets.Count > 0 ? bendTargets[0] : 0f;
        AddOrReplacePitchPoint(pitchCurve, 0f, startOffset);
        if (Mathf.Abs(startOffset) > 0.01f)
            pitchPreRollSeconds = PreBendPitchPreRollSeconds;

        int firstAnimatedIndex = firstIsPreBend ? 1 : 0;
        int animatedCount = bendTargets.Count - firstAnimatedIndex;
        if (animatedCount <= 0)
        {
            AddOrReplacePitchPoint(pitchCurve, 1f, startOffset);
            return;
        }

        bool releaseLikeTail = bendTargets.Count > 1 &&
                               (bendIsRelease[bendIsRelease.Count - 1] ||
                                Mathf.Abs(bendTargets[bendTargets.Count - 1] - startOffset) <= 0.05f);

        float holdTime = firstIsPreBend && Mathf.Abs(startOffset) > 0.01f
            ? (animatedCount == 1 ? 0.12f : 0.14f)
            : 0f;
        if (holdTime > 0.001f)
            AddOrReplacePitchPoint(pitchCurve, holdTime, startOffset);

        if (TryBuildDistinctPreBendReboundCurve(
                pitchCurve,
                firstIsPreBend,
                animatedCount,
                startOffset,
                bendTargets,
                firstAnimatedIndex))
        {
            if (!Mathf.Approximately(pitchCurve[pitchCurve.Count - 1].normalizedTime, 1f))
            {
                GeneratedPlaybackPitchPoint lastPoint = pitchCurve[pitchCurve.Count - 1];
                pitchCurve.Add(new GeneratedPlaybackPitchPoint
                {
                    normalizedTime = 1f,
                    semitoneOffset = lastPoint.semitoneOffset
                });
            }

            return;
        }

        float gestureEndTime = ResolvePitchGestureEndTime(firstIsPreBend, animatedCount, releaseLikeTail);
        gestureEndTime = Mathf.Max(gestureEndTime, holdTime + 0.06f);

        float[] transitionWeights = new float[animatedCount];
        float totalWeight = 0f;
        float previousOffset = startOffset;
        for (int animatedIndex = 0; animatedIndex < animatedCount; animatedIndex++)
        {
            int targetIndex = firstAnimatedIndex + animatedIndex;
            float targetOffset = bendTargets[targetIndex];
            float weight = ResolvePitchTransitionWeight(
                previousOffset,
                targetOffset,
                bendIsRelease[targetIndex],
                firstIsPreBend,
                animatedCount);
            transitionWeights[animatedIndex] = weight;
            totalWeight += weight;
            previousOffset = targetOffset;
        }

        float cumulativeWeight = 0f;
        for (int animatedIndex = 0; animatedIndex < animatedCount; animatedIndex++)
        {
            int targetIndex = firstAnimatedIndex + animatedIndex;
            cumulativeWeight += transitionWeights[animatedIndex];
            float t = totalWeight > 0.0001f
                ? cumulativeWeight / totalWeight
                : (animatedIndex + 1f) / animatedCount;
            float normalizedTime = Mathf.Lerp(holdTime, gestureEndTime, Mathf.Clamp01(t));
            AddOrReplacePitchPoint(pitchCurve, normalizedTime, bendTargets[targetIndex]);
        }

        if (!Mathf.Approximately(pitchCurve[pitchCurve.Count - 1].normalizedTime, 1f))
        {
            GeneratedPlaybackPitchPoint lastPoint = pitchCurve[pitchCurve.Count - 1];
            pitchCurve.Add(new GeneratedPlaybackPitchPoint
            {
                normalizedTime = 1f,
                semitoneOffset = lastPoint.semitoneOffset
            });
        }
    }

    private static float ResolvePitchGestureEndTime(bool firstIsPreBend, int animatedCount, bool releaseLikeTail)
    {
        if (firstIsPreBend)
        {
            if (animatedCount <= 1)
                return 0.28f;
            if (animatedCount == 2)
                return 0.56f;
            return Mathf.Clamp(0.62f + ((animatedCount - 2) * 0.08f), 0.62f, 0.84f);
        }

        if (animatedCount <= 1)
            return 0.24f;
        if (releaseLikeTail)
            return animatedCount == 2 ? 0.82f : 0.88f;
        return Mathf.Clamp(0.64f + ((animatedCount - 2) * 0.08f), 0.64f, 0.90f);
    }

    private static float ResolvePitchTransitionWeight(
        float previousOffset,
        float targetOffset,
        bool isRelease,
        bool firstIsPreBend,
        int animatedCount)
    {
        float delta = Mathf.Max(0.2f, Mathf.Abs(targetOffset - previousOffset));
        float weight = 0.72f + (Mathf.Min(delta, 2.5f) * 0.28f);
        if (isRelease)
            weight *= 0.94f;
        if (firstIsPreBend && animatedCount == 1)
            weight *= 0.9f;
        return weight;
    }

    private static bool TryBuildDistinctPreBendReboundCurve(
        List<GeneratedPlaybackPitchPoint> pitchCurve,
        bool firstIsPreBend,
        int animatedCount,
        float startOffset,
        List<float> bendTargets,
        int firstAnimatedIndex)
    {
        if (!firstIsPreBend || animatedCount != 2 || Mathf.Abs(startOffset) <= 0.01f)
            return false;

        float releaseTarget = bendTargets[firstAnimatedIndex];
        float reboundTarget = bendTargets[firstAnimatedIndex + 1];
        float releaseDelta = releaseTarget - startOffset;
        float reboundDelta = reboundTarget - releaseTarget;

        bool isReleaseThenRise =
            Mathf.Abs(releaseTarget) <= Mathf.Max(0.1f, Mathf.Abs(startOffset) * 0.35f) &&
            Mathf.Abs(reboundTarget) >= Mathf.Max(0.5f, Mathf.Abs(startOffset) * 0.7f) &&
            Mathf.Abs(releaseDelta) > 0.25f &&
            Mathf.Abs(reboundDelta) > 0.25f &&
            Mathf.Sign(releaseDelta) != Mathf.Sign(reboundDelta);

        if (!isReleaseThenRise)
            return false;

        float attackHold = Mathf.Clamp(0.11f + (Mathf.Min(Mathf.Abs(startOffset), 4f) * 0.01f), 0.11f, 0.15f);
        float releaseEnd = Mathf.Clamp(attackHold + 0.17f, 0.30f, 0.38f);
        float valleyHoldEnd = Mathf.Clamp(releaseEnd + 0.06f, releaseEnd + 0.05f, 0.46f);
        float reboundEnd = Mathf.Clamp(valleyHoldEnd + 0.18f, valleyHoldEnd + 0.14f, 0.66f);

        AddOrReplacePitchPoint(pitchCurve, 0f, startOffset);
        AddOrReplacePitchPoint(pitchCurve, attackHold, startOffset);
        AddOrReplacePitchPoint(pitchCurve, releaseEnd, releaseTarget);
        AddOrReplacePitchPoint(pitchCurve, valleyHoldEnd, releaseTarget);
        AddOrReplacePitchPoint(pitchCurve, reboundEnd, reboundTarget);
        return true;
    }

    private static bool TryNormalizePreBendAttackPitch(
        ref int midiNote,
        ref float pitchPreRollSeconds,
        List<GeneratedPlaybackPitchPoint> pitchCurve)
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

    private static void StretchEarlyMultiStepPitchCurve(List<GeneratedPlaybackPitchPoint> pitchCurve, float noteDurationSeconds)
    {
        if (pitchCurve == null || pitchCurve.Count < 4 || noteDurationSeconds <= 0.001f)
            return;

        float finalOffset = pitchCurve[pitchCurve.Count - 1].semitoneOffset;
        float minOffset = pitchCurve.Min(point => point.semitoneOffset);
        float maxOffset = pitchCurve.Max(point => point.semitoneOffset);
        bool hasDirectionChange = false;
        float previousDelta = 0f;
        for (int i = 1; i < pitchCurve.Count; i++)
        {
            float delta = pitchCurve[i].semitoneOffset - pitchCurve[i - 1].semitoneOffset;
            if (Mathf.Abs(delta) <= 0.01f)
                continue;

            if (Mathf.Abs(previousDelta) > 0.01f && Mathf.Sign(delta) != Mathf.Sign(previousDelta))
            {
                hasDirectionChange = true;
                break;
            }

            previousDelta = delta;
        }

        if (!hasDirectionChange || Mathf.Abs(maxOffset - minOffset) < 0.5f)
            return;

        int expressiveEndIndex = -1;
        for (int i = pitchCurve.Count - 2; i >= 1; i--)
        {
            if (Mathf.Abs(pitchCurve[i].semitoneOffset - finalOffset) > 0.02f)
            {
                expressiveEndIndex = i;
                break;
            }
        }

        if (expressiveEndIndex < 1)
            return;

        int trailingReturnIndex = Mathf.Min(expressiveEndIndex + 1, pitchCurve.Count - 1);
        float expressiveEndNormalized = pitchCurve[trailingReturnIndex].normalizedTime;
        if (expressiveEndNormalized <= 0.0001f || expressiveEndNormalized >= 0.95f)
            return;

        float expressiveSeconds = expressiveEndNormalized * noteDurationSeconds;
        float targetSeconds = Mathf.Min(Mathf.Max(expressiveSeconds, 0.34f), noteDurationSeconds * 0.68f);
        if (targetSeconds <= expressiveSeconds + 0.01f)
            return;

        float targetNormalized = Mathf.Clamp(targetSeconds / noteDurationSeconds, expressiveEndNormalized, 0.82f);
        float scale = targetNormalized / expressiveEndNormalized;
        for (int i = 1; i <= trailingReturnIndex; i++)
            pitchCurve[i].normalizedTime = Mathf.Clamp01(pitchCurve[i].normalizedTime * scale);

        for (int i = trailingReturnIndex + 1; i < pitchCurve.Count; i++)
        {
            if (pitchCurve[i].normalizedTime < pitchCurve[i - 1].normalizedTime)
                pitchCurve[i].normalizedTime = pitchCurve[i - 1].normalizedTime;
        }
    }

    private static RouteDescriptor ResolveSharedRouteDescriptor(
        RouteKey routeKey,
        PartMetadata metadata,
        GeneratedTechniqueVariant techniqueVariant,
        int preset,
        HashSet<int> usedChannels,
        List<RouteDescriptor> routes,
        Dictionary<RouteKey, RouteDescriptor> sharedRoutes)
    {
        if (sharedRoutes.TryGetValue(routeKey, out RouteDescriptor route))
            return route;

        int channel = AllocateChannel(metadata.isDrum, usedChannels);
        if (channel < 0)
            return null;

        route = CreateRouteDescriptor(channel, metadata, techniqueVariant, preset, routeKey.pitchBendRangeSemitones, laneIndex: 0);
        sharedRoutes[routeKey] = route;
        routes.Add(route);
        return route;
    }

    private static RouteDescriptor ResolvePitchRouteDescriptor(
        RouteKey routeKey,
        PartMetadata metadata,
        GeneratedTechniqueVariant techniqueVariant,
        int preset,
        float startTimeSeconds,
        float noteDurationSeconds,
        float pitchPreRollSeconds,
        HashSet<int> usedChannels,
        List<RouteDescriptor> routes,
        Dictionary<RouteKey, List<RouteLaneState>> pitchRouteLanes)
    {
        if (!pitchRouteLanes.TryGetValue(routeKey, out List<RouteLaneState> lanes))
        {
            lanes = new List<RouteLaneState>();
            pitchRouteLanes[routeKey] = lanes;
        }

        float occupancyStart = startTimeSeconds - Mathf.Max(0f, pitchPreRollSeconds);
        float occupancyEnd = startTimeSeconds + noteDurationSeconds + BendRouteReleaseTailSeconds;

        for (int i = 0; i < lanes.Count; i++)
        {
            RouteLaneState lane = lanes[i];
            if (lane.occupiedUntilSeconds <= occupancyStart + 0.0005f)
            {
                lane.occupiedUntilSeconds = occupancyEnd;
                return lane.descriptor;
            }
        }

        int channel = AllocateChannel(metadata.isDrum, usedChannels);
        if (channel >= 0)
        {
            RouteDescriptor descriptor = CreateRouteDescriptor(channel, metadata, techniqueVariant, preset, routeKey.pitchBendRangeSemitones, lanes.Count);
            routes.Add(descriptor);
            lanes.Add(new RouteLaneState
            {
                descriptor = descriptor,
                occupiedUntilSeconds = occupancyEnd
            });
            return descriptor;
        }

        if (lanes.Count == 0)
            return null;

        RouteLaneState fallbackLane = lanes
            .OrderBy(lane => lane.occupiedUntilSeconds)
            .First();
        fallbackLane.occupiedUntilSeconds = occupancyEnd;
        return fallbackLane.descriptor;
    }

    private static RouteDescriptor CreateRouteDescriptor(int channel, PartMetadata metadata, GeneratedTechniqueVariant techniqueVariant, int preset, int pitchBendRangeSemitones, int laneIndex)
    {
        string label = BuildRouteLabel(metadata, techniqueVariant, preset, pitchBendRangeSemitones);
        if (laneIndex > 0)
            label = $"{label} {laneIndex + 1}";

        return new RouteDescriptor
        {
            channel = channel,
            bank = metadata.preferredBank,
            preset = preset,
            isDrum = metadata.isDrum,
            label = label,
            sourcePartId = metadata.id,
            sourcePartName = metadata.name,
            pitchBendRangeSemitones = pitchBendRangeSemitones
        };
    }

    private static bool TryReadNotePitch(
        XElement noteNode,
        int chromaticTranspose,
        bool preferPercussion,
        out int midi,
        out bool fromTab,
        out int stringNumber,
        out int fret)
    {
        midi = -1;
        fromTab = false;
        stringNumber = -1;
        fret = -1;

        if (preferPercussion && TryReadUnpitchedNote(noteNode, out int unpitchedMidi))
        {
            midi = unpitchedMidi;
            return true;
        }

        if (TryReadTabNote(noteNode, out int tabMidi, out stringNumber, out fret))
        {
            midi = tabMidi;
            fromTab = true;
            return true;
        }

        if (TryReadPitchedNote(noteNode, preferPercussion ? 0 : chromaticTranspose, out int pitchedMidi))
        {
            midi = pitchedMidi;
            return true;
        }

        if (!preferPercussion && TryReadUnpitchedNote(noteNode, out int fallbackUnpitchedMidi))
        {
            midi = fallbackUnpitchedMidi;
            return true;
        }

        return false;
    }

    private static bool TryReadUnpitchedNote(XElement noteNode, out int midi)
    {
        midi = -1;
        XElement unpitchedNode = noteNode.Elements().FirstOrDefault(e => e.Name.LocalName == "unpitched");
        if (unpitchedNode == null)
            return false;

        string step = ChildValue(unpitchedNode, "display-step");
        int octave = ParseInt(ChildValue(unpitchedNode, "display-octave"), int.MinValue);
        if (string.IsNullOrWhiteSpace(step) || octave == int.MinValue)
            return false;

        int pitchClass = StepToPitchClass(step, 0);
        midi = (octave + 1) * 12 + pitchClass;
        return true;
    }

    private static bool TryReadPitchedNote(XElement noteNode, int chromaticTranspose, out int midi)
    {
        midi = -1;
        XElement pitchNode = noteNode.Elements().FirstOrDefault(e => e.Name.LocalName == "pitch");
        if (pitchNode == null)
            return false;

        string step = ChildValue(pitchNode, "step");
        int alter = ParseInt(ChildValue(pitchNode, "alter"), 0);
        int octave = ParseInt(ChildValue(pitchNode, "octave"), int.MinValue);
        if (string.IsNullOrWhiteSpace(step) || octave == int.MinValue)
            return false;

        int pitchClass = StepToPitchClass(step, alter);
        midi = (octave + 1) * 12 + pitchClass + chromaticTranspose;
        return true;
    }

    private static bool TryReadTabNote(XElement noteNode, out int midi, out int stringNumber, out int fret)
    {
        midi = -1;
        stringNumber = -1;
        fret = -1;
        XElement technical = noteNode.Descendants().FirstOrDefault(e => e.Name.LocalName == "technical");
        if (technical == null)
            return false;

        XElement stringNode = technical.Elements().FirstOrDefault(e => e.Name.LocalName == "string");
        XElement fretNode = technical.Elements().FirstOrDefault(e => e.Name.LocalName == "fret");
        if (stringNode == null || fretNode == null)
            return false;

        int musicXmlString = ParseInt(stringNode.Value, -1);
        int parsedFret = ParseInt(fretNode.Value, -1);
        if (musicXmlString < 1 || musicXmlString > 6 || parsedFret < 0)
            return false;

        int[] stringBasePitches = { 40, 45, 50, 55, 59, 64 };
        int stringIndex = 6 - musicXmlString;
        stringNumber = musicXmlString;
        fret = parsedFret;
        midi = stringBasePitches[stringIndex] + parsedFret;
        return true;
    }

    private static int ParseVelocity(XElement noteNode)
    {
        string dynamicsAttr = Attr(noteNode, "dynamics");
        if (float.TryParse(dynamicsAttr, NumberStyles.Any, CultureInfo.InvariantCulture, out float dynamics))
            return Mathf.Clamp(Mathf.RoundToInt(dynamics), 1, 127);

        XElement velocityNode = noteNode.Descendants().FirstOrDefault(e => e.Name.LocalName == "velocity");
        if (velocityNode != null && int.TryParse(velocityNode.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int velocity))
            return Mathf.Clamp(velocity, 1, 127);

        return 96;
    }

    private static bool HasHarmonicNotation(XElement noteNode)
    {
        return noteNode.Descendants().Any(element => string.Equals(element.Name.LocalName, "harmonic", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMuteType(XElement noteNode, string expectedValue)
    {
        XElement playNode = noteNode.Elements().FirstOrDefault(e => e.Name.LocalName == "play");
        if (playNode == null)
            return false;

        return playNode.Elements().Any(element =>
            element.Name.LocalName == "mute" &&
            string.Equals((element.Value ?? string.Empty).Trim(), expectedValue, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsStraightMutedNote(XElement noteNode)
    {
        XElement notehead = noteNode.Elements().FirstOrDefault(e => e.Name.LocalName == "notehead");
        if (notehead != null && string.Equals((notehead.Value ?? string.Empty).Trim(), "x", StringComparison.OrdinalIgnoreCase))
            return true;

        return IsMuteType(noteNode, "straight");
    }

    private static int ResolveHarmonicPreset(IEnumerable<PartMetadata> parts)
    {
        PartMetadata harmonicPart = parts.FirstOrDefault(part => part.isExplicitHarmonicPart && part.midiProgram > 0);
        if (harmonicPart != null)
            return Mathf.Clamp(harmonicPart.midiProgram - 1, 0, 127);

        return 53;
    }

    private static int ResolvePresetForNote(PartMetadata metadata, GeneratedTechniqueVariant techniqueVariant, int harmonicPreset)
    {
        int sourcePreset = Mathf.Clamp(metadata.midiProgram > 0 ? metadata.midiProgram - 1 : 0, 0, 127);
        if (metadata.isDrum)
            return sourcePreset;

        switch (techniqueVariant)
        {
            case GeneratedTechniqueVariant.PalmMute:
            case GeneratedTechniqueVariant.StraightMute:
                return 28;
            case GeneratedTechniqueVariant.Harmonic:
                return harmonicPreset;
            default:
                return sourcePreset;
        }
    }

    private static string BuildRouteLabel(PartMetadata metadata, GeneratedTechniqueVariant techniqueVariant, int preset, int pitchBendRangeSemitones)
    {
        if (metadata.isDrum)
            return "Drums";

        if (pitchBendRangeSemitones > 0)
            return $"{metadata.name} Bend";
        if (techniqueVariant == GeneratedTechniqueVariant.Harmonic)
            return $"{metadata.name} Harmonic";
        if (techniqueVariant == GeneratedTechniqueVariant.PalmMute)
            return $"{metadata.name} Palm Mute";
        if (techniqueVariant == GeneratedTechniqueVariant.StraightMute)
            return $"{metadata.name} Mute";

        return $"{metadata.name} ({preset})";
    }

    private static int AllocateChannel(bool isDrum, HashSet<int> usedChannels)
    {
        if (isDrum)
        {
            usedChannels.Add(9);
            return 9;
        }

        for (int channel = 0; channel < 16; channel++)
        {
            if (channel == 9 || usedChannels.Contains(channel))
                continue;

            usedChannels.Add(channel);
            return channel;
        }

        return -1;
    }

    private static List<double> BuildCanonicalMeasureStarts(List<XElement> parts)
    {
        List<List<double>> perPartDurations = new List<List<double>>();
        int maxMeasureCount = 0;

        for (int partIndex = 0; partIndex < parts.Count; partIndex++)
        {
            XElement part = parts[partIndex];
            List<double> durations = new List<double>();
            double divisions = 1.0;

            foreach (XElement measure in part.Elements().Where(e => e.Name.LocalName == "measure"))
            {
                double cursorQuarter = 0.0;
                double measureMaxQuarter = 0.0;
                double timeSignatureQuarter = 0.0;

                foreach (XElement child in measure.Elements())
                {
                    string local = child.Name.LocalName;
                    if (local == "attributes")
                    {
                        XElement divNode = child.Elements().FirstOrDefault(e => e.Name.LocalName == "divisions");
                        if (divNode != null && double.TryParse(divNode.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedDiv) && parsedDiv > 0.0)
                            divisions = parsedDiv;

                        XElement timeNode = child.Elements().FirstOrDefault(e => e.Name.LocalName == "time");
                        if (timeNode != null)
                        {
                            int beats = ParseInt(ChildValue(timeNode, "beats"), 0);
                            int beatType = ParseInt(ChildValue(timeNode, "beat-type"), 0);
                            if (beats > 0 && beatType > 0)
                                timeSignatureQuarter = beats * (4.0 / beatType);
                        }
                    }
                    else if (local == "backup")
                    {
                        cursorQuarter = Math.Max(0.0, cursorQuarter - DurationNodeToQuarter(child, divisions));
                    }
                    else if (local == "forward")
                    {
                        cursorQuarter += DurationNodeToQuarter(child, divisions);
                        measureMaxQuarter = Math.Max(measureMaxQuarter, cursorQuarter);
                    }
                    else if (local == "note")
                    {
                        bool isChordTone = child.Elements().Any(e => e.Name.LocalName == "chord");
                        bool isGrace = child.Elements().Any(e => e.Name.LocalName == "grace");
                        if (!isChordTone)
                        {
                            cursorQuarter += isGrace ? 0.0 : DurationNodeToQuarter(child, divisions);
                            measureMaxQuarter = Math.Max(measureMaxQuarter, cursorQuarter);
                        }
                    }
                }

                double durationQuarter = Math.Max(measureMaxQuarter, timeSignatureQuarter);
                if (durationQuarter <= 0.0)
                    durationQuarter = 4.0;

                durations.Add(durationQuarter);
            }

            perPartDurations.Add(durations);
            maxMeasureCount = Math.Max(maxMeasureCount, durations.Count);
        }

        List<double> measureDurations = new List<double>(Math.Max(1, maxMeasureCount));
        for (int measureIndex = 0; measureIndex < Math.Max(1, maxMeasureCount); measureIndex++)
        {
            double bestDuration = 0.0;
            for (int partIndex = 0; partIndex < perPartDurations.Count; partIndex++)
            {
                List<double> durations = perPartDurations[partIndex];
                if (measureIndex < durations.Count && durations[measureIndex] > bestDuration)
                    bestDuration = durations[measureIndex];
            }

            if (bestDuration <= 0.0)
                bestDuration = 4.0;

            measureDurations.Add(bestDuration);
        }

        List<double> starts = new List<double>(measureDurations.Count + 1) { 0.0 };
        for (int i = 0; i < measureDurations.Count; i++)
            starts.Add(starts[i] + measureDurations[i]);

        return starts;
    }

    private static List<TempoEvent> BuildGlobalTempoMap(List<XElement> parts, List<double> canonicalMeasureStarts)
    {
        List<TempoEvent> tempoCandidates = new List<TempoEvent> { new TempoEvent(0.0, 120.0) };

        for (int partIndex = 0; partIndex < parts.Count; partIndex++)
        {
            XElement part = parts[partIndex];
            double divisions = 1.0;
            int measureIndex = 0;

            foreach (XElement measure in part.Elements().Where(e => e.Name.LocalName == "measure"))
            {
                double currentMeasureStartQuarter = measureIndex < canonicalMeasureStarts.Count
                    ? canonicalMeasureStarts[measureIndex]
                    : (canonicalMeasureStarts.Count > 0 ? canonicalMeasureStarts[canonicalMeasureStarts.Count - 1] : 0.0);

                Dictionary<string, double> voiceCursorOffsets = new Dictionary<string, double>();
                string activeVoiceKey = "1:1";

                foreach (XElement child in measure.Elements())
                {
                    string local = child.Name.LocalName;
                    if (local == "attributes")
                    {
                        XElement divNode = child.Elements().FirstOrDefault(e => e.Name.LocalName == "divisions");
                        if (divNode != null && double.TryParse(divNode.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedDiv) && parsedDiv > 0.0)
                            divisions = parsedDiv;
                    }
                    else if (local == "direction")
                    {
                        double? tempo = TryReadTempoFromDirection(child);
                        if (tempo.HasValue && tempo.Value > 0.0)
                        {
                            TryReadDirectionOffsetQuarter(child, divisions, out double offsetQuarter);
                            double baseOffset = voiceCursorOffsets.TryGetValue(activeVoiceKey, out double activeOffset) ? activeOffset : 0.0;
                            double tempoQuarter = Math.Max(currentMeasureStartQuarter, currentMeasureStartQuarter + baseOffset + offsetQuarter);
                            tempoCandidates.Add(new TempoEvent(tempoQuarter, tempo.Value));
                        }
                    }
                    else if (local == "backup")
                    {
                        double currentOffset = voiceCursorOffsets.TryGetValue(activeVoiceKey, out double storedOffset) ? storedOffset : 0.0;
                        voiceCursorOffsets[activeVoiceKey] = Math.Max(0.0, currentOffset - DurationNodeToQuarter(child, divisions));
                    }
                    else if (local == "forward")
                    {
                        double currentOffset = voiceCursorOffsets.TryGetValue(activeVoiceKey, out double storedOffset) ? storedOffset : 0.0;
                        voiceCursorOffsets[activeVoiceKey] = currentOffset + DurationNodeToQuarter(child, divisions);
                    }
                    else if (local == "note")
                    {
                        bool isChordTone = child.Elements().Any(e => e.Name.LocalName == "chord");
                        bool isGrace = child.Elements().Any(e => e.Name.LocalName == "grace");
                        int staff = ParseInt(ChildValue(child, "staff"), 1);
                        int voice = ParseInt(ChildValue(child, "voice"), 1);
                        string voiceKey = $"{voice}:{staff}";

                        if (!voiceCursorOffsets.TryGetValue(voiceKey, out double currentOffset))
                        {
                            currentOffset = voiceCursorOffsets.TryGetValue(activeVoiceKey, out double activeOffset) ? activeOffset : 0.0;
                            voiceCursorOffsets[voiceKey] = currentOffset;
                        }

                        if (!isChordTone)
                            voiceCursorOffsets[voiceKey] = currentOffset + (isGrace ? 0.0 : DurationNodeToQuarter(child, divisions));

                        activeVoiceKey = voiceKey;
                    }
                }

                measureIndex++;
            }
        }

        return tempoCandidates
            .OrderBy(candidate => candidate.quarterPos)
            .GroupBy(candidate => candidate.quarterPos)
            .Select(group => group.Last())
            .ToList();
    }

    private static double QuarterToSeconds(double targetQuarter, List<TempoEvent> tempoMap)
    {
        double totalSeconds = 0.0;
        double previousQuarter = 0.0;
        double currentBpm = 120.0;

        for (int i = 0; i < tempoMap.Count; i++)
        {
            TempoEvent tempoEvent = tempoMap[i];
            if (tempoEvent.quarterPos > targetQuarter)
                break;

            double deltaQuarter = tempoEvent.quarterPos - previousQuarter;
            totalSeconds += deltaQuarter * (60.0 / currentBpm);
            previousQuarter = tempoEvent.quarterPos;
            currentBpm = tempoEvent.bpm;
        }

        totalSeconds += (targetQuarter - previousQuarter) * (60.0 / currentBpm);
        return totalSeconds;
    }

    private static bool TryReadDirectionOffsetQuarter(XElement directionNode, double divisions, out double offsetQuarter)
    {
        offsetQuarter = 0.0;
        XElement offsetNode = directionNode.Elements().FirstOrDefault(e => e.Name.LocalName == "offset");
        if (offsetNode == null)
            return false;

        if (divisions <= 0.0)
            divisions = 1.0;

        if (!double.TryParse(offsetNode.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double offsetDivisions))
            return false;

        offsetQuarter = offsetDivisions / divisions;
        return true;
    }

    private static double? TryReadTempoFromDirection(XElement directionNode)
    {
        XElement soundNode = directionNode.Elements().FirstOrDefault(e => e.Name.LocalName == "sound");
        if (soundNode != null)
        {
            string tempoAttr = Attr(soundNode, "tempo");
            if (double.TryParse(tempoAttr, NumberStyles.Any, CultureInfo.InvariantCulture, out double tempo) && tempo > 0.0)
                return tempo;
        }

        XElement directionType = directionNode.Elements().FirstOrDefault(e => e.Name.LocalName == "direction-type");
        XElement metronome = directionType?.Elements().FirstOrDefault(e => e.Name.LocalName == "metronome");
        if (metronome != null)
        {
            string perMinuteText = ChildValue(metronome, "per-minute");
            if (double.TryParse(perMinuteText, NumberStyles.Any, CultureInfo.InvariantCulture, out double tempo) && tempo > 0.0)
                return tempo;
        }

        return null;
    }

    private static double DurationNodeToQuarter(XElement node, double divisions)
    {
        XElement durationNode = node.Elements().FirstOrDefault(e => e.Name.LocalName == "duration");
        if (durationNode == null)
            return 0.0;

        if (!double.TryParse(durationNode.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double durationDivisions))
            return 0.0;

        if (divisions <= 0.0)
            divisions = 1.0;

        return durationDivisions / divisions;
    }

    private static int StepToPitchClass(string step, int alter)
    {
        int basePitch = step switch
        {
            "C" => 0,
            "D" => 2,
            "E" => 4,
            "F" => 5,
            "G" => 7,
            "A" => 9,
            "B" => 11,
            _ => 0
        };

        return ((basePitch + alter) % 12 + 12) % 12;
    }

    private static string Attr(XElement element, string attributeName)
    {
        return element?.Attribute(attributeName)?.Value ?? string.Empty;
    }

    private static string ChildValue(XElement element, string childName)
    {
        return element?.Elements().FirstOrDefault(child => child.Name.LocalName == childName)?.Value ?? string.Empty;
    }

    private static int ParseInt(string text, int fallback)
    {
        return int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out int value) ? value : fallback;
    }
}
