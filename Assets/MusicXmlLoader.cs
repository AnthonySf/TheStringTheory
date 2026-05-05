using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

public static class MusicXmlLoader
{
    public sealed class MusicXmlPartSummary
    {
        public int Index;
        public string PartId;
        public string Name;
        public string GroupId;
        public string GroupDisplayName;
        public string DifficultyLabel;
        public int DifficultyUiIndex = -1;
        public bool HasDifficultyVariants;
        public int NoteCount;
        public int TabCount;
        public int Score;
        public int[] StringTuningPitches;
        public string TuningDisplayName;

        public MusicXmlPartSummary Clone()
        {
            return new MusicXmlPartSummary
            {
                Index = Index,
                PartId = PartId,
                Name = Name,
                GroupId = GroupId,
                GroupDisplayName = GroupDisplayName,
                DifficultyLabel = DifficultyLabel,
                DifficultyUiIndex = DifficultyUiIndex,
                HasDifficultyVariants = HasDifficultyVariants,
                NoteCount = NoteCount,
                TabCount = TabCount,
                Score = Score,
                StringTuningPitches = StringTuningPitches != null ? (int[])StringTuningPitches.Clone() : null,
                TuningDisplayName = TuningDisplayName
            };
        }
    }

    private static readonly int[] stringBasePitches = { 40, 45, 50, 55, 59, 64 }; // E2 A2 D3 G3 B3 E4
    private static readonly string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private class TempoEvent
    {
        public double quarterPos;
        public double bpm;

        public TempoEvent(double quarterPos, double bpm)
        {
            this.quarterPos = quarterPos;
            this.bpm = bpm;
        }
    }

    private sealed class ParsedNote
    {
        public int sourceIndex;
        public double quarterPos;
        public double durationQuarter;
        public int midi;
        public int stringIdx;
        public int fret;
        public string note;
        public int staff;
        public bool fromTab;
        public bool tieStart;
        public bool tieStop;
        public bool slideStart;
        public bool hammerStart;
        public bool pullStart;
        public bool vibrato;
        public float bendStep;
        public double bendVisualStartQuarter;
        public double bendVisualDurationQuarter;
        public bool bendPreBend;
        public bool bendRelease;
        public bool isMuted;
        public List<ParsedTechniqueSegment> techniqueSegments = new List<ParsedTechniqueSegment>();
    }

    private sealed class ParsedTechniqueSegment
    {
        public NoteTechniqueSegmentType type;
        public double startQuarter;
        public double endQuarter;
        public int startFret;
        public int endFret;
        public float startBend;
        public float endBend;
    }

    public static List<NoteData> LoadMusicXmlSong(string filePath, int targetPartIndex = -1)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("MusicXML file not found: " + filePath);
            return null;
        }

        try
        {
            XDocument doc = XDocument.Load(filePath);
            XElement root = doc.Root;
            if (root == null)
            {
                Debug.LogError("Invalid MusicXML: no root node.");
                return null;
            }

            Dictionary<string, string> partNames = ReadPartNames(root);
            List<XElement> parts = root.Elements().Where(e => e.Name.LocalName == "part").ToList();

            if (parts.Count == 0)
            {
                Debug.LogError("No <part> elements found in MusicXML.");
                return null;
            }

            List<MusicXmlPartSummary> summaries = BuildPartSummaries(parts, partNames);

            int chosenPartIndex = (targetPartIndex >= 0 && targetPartIndex < parts.Count)
                ? targetPartIndex
                : ChooseBestPart(summaries);

            XElement chosenPart = parts[chosenPartIndex];
            string chosenPartId = Attr(chosenPart, "id");
            string chosenPartName = partNames.ContainsKey(chosenPartId) ? partNames[chosenPartId] : $"Part {chosenPartIndex}";

            Debug.Log($"MusicXML selected part: {chosenPartIndex} ('{chosenPartName}')");

            List<double> canonicalMeasureStarts = BuildCanonicalMeasureStarts(parts);
            List<TempoEvent> tempoMap = BuildGlobalTempoMap(parts, canonicalMeasureStarts);
            List<ParsedNote> parsed = ParsePart(chosenPart, canonicalMeasureStarts);

            if (parsed.Count == 0)
            {
                Debug.LogWarning("MusicXML part parsed but no usable notes were found.");
                return new List<NoteData>();
            }

            int preferredStaff = ChoosePreferredStaff(parsed);
            bool hasAnyTab = parsed.Any(n => n.fromTab);

            IEnumerable<ParsedNote> filtered = parsed;

            if (hasAnyTab)
            {
                filtered = filtered.Where(n => n.fromTab && n.staff == preferredStaff);
                Debug.Log($"MusicXML: using TAB staff only -> staff {preferredStaff}");
            }
            else
            {
                filtered = filtered.Where(n => n.staff == preferredStaff);
                Debug.Log($"MusicXML: no TAB detected, using preferred staff {preferredStaff}");
            }

            List<ParsedNote> normalized = NormalizeParsedNotes(filtered.OrderBy(n => n.quarterPos).ThenBy(n => n.sourceIndex).ToList());
            List<NoteData> result = BuildGameplayNotes(normalized, tempoMap);

            int debugCount = Math.Min(60, result.Count);
            for (int i = 0; i < debugCount; i++)
            {
                float delta = i == 0 ? result[i].time : result[i].time - result[i - 1].time;
                Debug.Log(
                        $"[XML IMPORT] idx={i} t={result[i].time:F3}s \u0394={delta:F3}s string={result[i].stringIdx} fret={result[i].fret} note={result[i].note} tech={result[i].technique} pluck={result[i].requiresPluck}");
            }

            Debug.Log($"Loaded {result.Count} notes from MusicXML part '{chosenPartName}'");
            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to parse MusicXML: " + ex);
            return null;
        }
    }

    public static List<MusicXmlPartSummary> GetPartSummaries(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("MusicXML file not found: " + filePath);
            return new List<MusicXmlPartSummary>();
        }

        try
        {
            XDocument doc = XDocument.Load(filePath);
            XElement root = doc.Root;
            if (root == null)
                return new List<MusicXmlPartSummary>();

            Dictionary<string, string> partNames = ReadPartNames(root);
            List<XElement> parts = root.Elements().Where(e => e.Name.LocalName == "part").ToList();
            return BuildPartSummaries(parts, partNames);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to read MusicXML part summaries: " + ex.Message);
            return new List<MusicXmlPartSummary>();
        }
    }

    private static Dictionary<string, string> ReadPartNames(XElement root)
    {
        var result = new Dictionary<string, string>();

        XElement partList = root.Elements().FirstOrDefault(e => e.Name.LocalName == "part-list");
        if (partList == null)
            return result;

        foreach (XElement scorePart in partList.Elements().Where(e => e.Name.LocalName == "score-part"))
        {
            string id = Attr(scorePart, "id");
            string name = ChildValue(scorePart, "part-name");
            if (!string.IsNullOrEmpty(id))
                result[id] = string.IsNullOrEmpty(name) ? id : name;
        }

        return result;
    }

    private static List<MusicXmlPartSummary> BuildPartSummaries(List<XElement> parts, Dictionary<string, string> partNames)
    {
        var summaries = new List<MusicXmlPartSummary>();

        for (int i = 0; i < parts.Count; i++)
        {
            XElement part = parts[i];
            string id = Attr(part, "id");
            string name = partNames.ContainsKey(id) ? partNames[id] : $"Part {i}";
            string lower = name.ToLowerInvariant();

            int score = 0;
            int noteCount = 0;
            int tabCount = 0;

            foreach (XElement note in part.Descendants().Where(e => e.Name.LocalName == "note"))
            {
                if (note.Elements().Any(e => e.Name.LocalName == "rest"))
                    continue;

                noteCount++;

                XElement technical = note.Descendants().FirstOrDefault(e => e.Name.LocalName == "technical");
                if (technical != null &&
                    technical.Elements().Any(e => e.Name.LocalName == "string") &&
                    technical.Elements().Any(e => e.Name.LocalName == "fret"))
                {
                    tabCount++;
                }
            }

            score += noteCount;
            score += tabCount * 20;
            if (lower.Contains("guitar")) score += 500;
            if (lower.Contains("rythm")) score += 120;
            if (lower.Contains("rhythm")) score += 120;
            if (lower.Contains("lead")) score += 100;
            if (lower.Contains("tab")) score += 150;
            if (lower.Contains("bass")) score -= 250;
            if (lower.Contains("drum")) score -= 500;
            if (lower.Contains("voice")) score -= 200;
            if (lower.Contains("vocal")) score -= 200;
            if (lower.Contains("piano")) score -= 100;

            int[] tuningPitches = ParsePartTuningPitches(part);
            summaries.Add(new MusicXmlPartSummary
            {
                Index = i,
                PartId = id,
                Name = name,
                NoteCount = noteCount,
                TabCount = tabCount,
                Score = score,
                StringTuningPitches = tuningPitches,
                TuningDisplayName = StringTuningUtils.FormatTuningDisplayName(tuningPitches)
            });
        }

        foreach (MusicXmlPartSummary summary in summaries)
            Debug.Log($"MusicXML part {summary.Index}: '{summary.Name}' noteCount={summary.NoteCount} tabCount={summary.TabCount} score={summary.Score}");

        return summaries;
    }

    private static int ChooseBestPart(List<MusicXmlPartSummary> summaries)
    {
        if (summaries == null || summaries.Count == 0)
            return 0;

        int bestIndex = summaries[0].Index;
        int bestScore = int.MinValue;

        foreach (MusicXmlPartSummary summary in summaries)
        {
            if (summary.Score > bestScore)
            {
                bestScore = summary.Score;
                bestIndex = summary.Index;
            }
        }

        return bestIndex;
    }

    private static List<ParsedNote> ParsePart(XElement part, List<double> canonicalMeasureStarts)
    {
        var notes = new List<ParsedNote>();

        double divisions = 1.0;
        int chromaticTranspose = 0;
        int sourceIndex = 0;
        int measureIndex = 0;

        foreach (XElement measure in part.Elements().Where(e => e.Name.LocalName == "measure"))
        {
            double currentMeasureStartQuarter = measureIndex < canonicalMeasureStarts.Count
                ? canonicalMeasureStarts[measureIndex]
                : (canonicalMeasureStarts.Count > 0 ? canonicalMeasureStarts[canonicalMeasureStarts.Count - 1] : 0.0);

            var voiceCursorOffsets = new Dictionary<string, double>();
            var lastNoteStartByVoice = new Dictionary<string, double>();
            string activeVoiceKey = "1:1";

            foreach (XElement child in measure.Elements())
            {
                string local = child.Name.LocalName;

                if (local == "attributes")
                {
                    XElement divNode = child.Elements().FirstOrDefault(e => e.Name.LocalName == "divisions");
                    if (divNode != null)
                    {
                        double parsedDiv;
                        if (double.TryParse(divNode.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedDiv) && parsedDiv > 0)
                            divisions = parsedDiv;
                    }

                    XElement transposeNode = child.Elements().FirstOrDefault(e => e.Name.LocalName == "transpose");
                    if (transposeNode != null)
                        chromaticTranspose = ParseInt(ChildValue(transposeNode, "chromatic"), chromaticTranspose);
                }
                else if (local == "backup")
                {
                    double durQuarter = DurationNodeToQuarter(child, divisions);
                    double currentOffset = voiceCursorOffsets.ContainsKey(activeVoiceKey) ? voiceCursorOffsets[activeVoiceKey] : 0.0;
                    voiceCursorOffsets[activeVoiceKey] = Math.Max(0.0, currentOffset - durQuarter);
                }
                else if (local == "forward")
                {
                    double durQuarter = DurationNodeToQuarter(child, divisions);
                    double currentOffset = voiceCursorOffsets.ContainsKey(activeVoiceKey) ? voiceCursorOffsets[activeVoiceKey] : 0.0;
                    voiceCursorOffsets[activeVoiceKey] = currentOffset + durQuarter;
                }
                else if (local == "note")
                {
                    bool isRest = child.Elements().Any(e => e.Name.LocalName == "rest");
                    bool isChordTone = child.Elements().Any(e => e.Name.LocalName == "chord");
                    bool isGrace = child.Elements().Any(e => e.Name.LocalName == "grace");

                    int staff = ParseInt(ChildValue(child, "staff"), 1);
                    int voice = ParseInt(ChildValue(child, "voice"), 1);
                    string voiceKey = voice.ToString(CultureInfo.InvariantCulture) + ":" + staff.ToString(CultureInfo.InvariantCulture);

                    double currentOffset;
                    if (!voiceCursorOffsets.TryGetValue(voiceKey, out currentOffset))
                    {
                        currentOffset = voiceCursorOffsets.ContainsKey(activeVoiceKey) ? voiceCursorOffsets[activeVoiceKey] : 0.0;
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

                    double durQuarter = isGrace ? 0.0 : DurationNodeToQuarter(child, divisions);

                    if (!isRest)
                    {
                        int stringIdx;
                        int fret;
                        int midi;
                        string name;

                        ParseTechniqueInfo(child,
                            out bool tieStart, out bool tieStop,
                            out bool slideStart, out bool hammerStart, out bool pullStart,
                            out bool vibrato, out float bendStep, out bool bendPreBend, out bool bendRelease);
                        bool isMuted = IsStraightMutedNote(child);

                        if (TryReadTabNote(child, out stringIdx, out fret, out midi, out name))
                        {
                            List<ParsedTechniqueSegment> techniqueSegments = BuildInitialTechniqueSegments(
                                noteStartQuarter,
                                durQuarter,
                                fret,
                                vibrato,
                                bendStep,
                                bendPreBend,
                                bendRelease,
                                BuildBendTechniqueSegments(child, noteStartQuarter, durQuarter, fret));
                            notes.Add(new ParsedNote
                            {
                                sourceIndex = sourceIndex++,
                                quarterPos = noteStartQuarter,
                                durationQuarter = durQuarter,
                                stringIdx = stringIdx,
                                fret = fret,
                                midi = midi,
                                note = name,
                                staff = staff,
                                fromTab = true,
                                tieStart = tieStart,
                                tieStop = tieStop,
                                slideStart = slideStart,
                                hammerStart = hammerStart,
                                pullStart = pullStart,
                                vibrato = vibrato,
                                bendStep = bendStep,
                                bendVisualStartQuarter = bendStep > 0f || bendPreBend || bendRelease ? noteStartQuarter : -1.0,
                                bendVisualDurationQuarter = bendStep > 0f || bendPreBend || bendRelease ? durQuarter : 0.0,
                                bendPreBend = bendPreBend,
                                bendRelease = bendRelease,
                                isMuted = isMuted,
                                techniqueSegments = techniqueSegments
                            });
                        }
                        else if (TryReadPitchedNote(child, chromaticTranspose, out midi, out name))
                        {
                            var mapped = MapMidiToGuitar(midi);
                            if (mapped.HasValue)
                            {
                                List<ParsedTechniqueSegment> techniqueSegments = BuildInitialTechniqueSegments(
                                    noteStartQuarter,
                                    durQuarter,
                                    mapped.Value.Value,
                                    vibrato,
                                    bendStep,
                                    bendPreBend,
                                    bendRelease,
                                    BuildBendTechniqueSegments(child, noteStartQuarter, durQuarter, mapped.Value.Value));
                                notes.Add(new ParsedNote
                                {
                                    sourceIndex = sourceIndex++,
                                    quarterPos = noteStartQuarter,
                                    durationQuarter = durQuarter,
                                    stringIdx = mapped.Value.Key,
                                    fret = mapped.Value.Value,
                                    midi = midi,
                                    note = name,
                                    staff = staff,
                                    fromTab = false,
                                    tieStart = tieStart,
                                    tieStop = tieStop,
                                    slideStart = slideStart,
                                    hammerStart = hammerStart,
                                    pullStart = pullStart,
                                    vibrato = vibrato,
                                    bendStep = bendStep,
                                    bendVisualStartQuarter = bendStep > 0f || bendPreBend || bendRelease ? noteStartQuarter : -1.0,
                                    bendVisualDurationQuarter = bendStep > 0f || bendPreBend || bendRelease ? durQuarter : 0.0,
                                    bendPreBend = bendPreBend,
                                    bendRelease = bendRelease,
                                    isMuted = isMuted,
                                    techniqueSegments = techniqueSegments
                                });
                            }
                        }
                    }

                    if (!isChordTone)
                        voiceCursorOffsets[voiceKey] = currentOffset + durQuarter;

                    activeVoiceKey = voiceKey;
                }
            }
            measureIndex++;
        }

        return notes;
    }

    private static List<double> BuildCanonicalMeasureStarts(List<XElement> parts)
    {
        var perPartDurations = new List<List<double>>();
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
                double timeSigQuarter = 0.0;

                foreach (XElement child in measure.Elements())
                {
                    string local = child.Name.LocalName;

                    if (local == "attributes")
                    {
                        XElement divNode = child.Elements().FirstOrDefault(e => e.Name.LocalName == "divisions");
                        if (divNode != null)
                        {
                            double parsedDiv;
                            if (double.TryParse(divNode.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedDiv) && parsedDiv > 0)
                                divisions = parsedDiv;
                        }

                        XElement timeNode = child.Elements().FirstOrDefault(e => e.Name.LocalName == "time");
                        if (timeNode != null)
                        {
                            int beats = ParseInt(ChildValue(timeNode, "beats"), 0);
                            int beatType = ParseInt(ChildValue(timeNode, "beat-type"), 0);
                            if (beats > 0 && beatType > 0)
                                timeSigQuarter = beats * (4.0 / beatType);
                        }
                    }
                    else if (local == "backup")
                    {
                        cursorQuarter -= DurationNodeToQuarter(child, divisions);
                        if (cursorQuarter < 0.0)
                            cursorQuarter = 0.0;
                    }
                    else if (local == "forward")
                    {
                        cursorQuarter += DurationNodeToQuarter(child, divisions);
                        if (cursorQuarter > measureMaxQuarter)
                            measureMaxQuarter = cursorQuarter;
                    }
                    else if (local == "note")
                    {
                        bool isChordTone = child.Elements().Any(e => e.Name.LocalName == "chord");
                        bool isGrace = child.Elements().Any(e => e.Name.LocalName == "grace");
                        if (!isChordTone)
                        {
                            cursorQuarter += isGrace ? 0.0 : DurationNodeToQuarter(child, divisions);
                            if (cursorQuarter > measureMaxQuarter)
                                measureMaxQuarter = cursorQuarter;
                        }
                    }
                }

                double durationQuarter = Math.Max(measureMaxQuarter, timeSigQuarter);
                if (durationQuarter <= 0.0)
                    durationQuarter = 4.0;

                durations.Add(durationQuarter);
            }

            perPartDurations.Add(durations);
            if (durations.Count > maxMeasureCount)
                maxMeasureCount = durations.Count;
        }

        List<double> measureDurations = new List<double>(Math.Max(1, maxMeasureCount));
        for (int m = 0; m < Math.Max(1, maxMeasureCount); m++)
        {
            double best = 0.0;
            for (int p = 0; p < perPartDurations.Count; p++)
            {
                List<double> durations = perPartDurations[p];
                if (m < durations.Count && durations[m] > best)
                    best = durations[m];
            }

            if (best <= 0.0)
                best = 4.0;

            measureDurations.Add(best);
        }

        List<double> measureStarts = new List<double>(measureDurations.Count + 1) { 0.0 };
        for (int m = 0; m < measureDurations.Count; m++)
            measureStarts.Add(measureStarts[m] + measureDurations[m]);

        return measureStarts;
    }

    private static List<TempoEvent> BuildGlobalTempoMap(List<XElement> parts, List<double> canonicalMeasureStarts)
    {
        var tempoCandidates = new List<TempoEvent> { new TempoEvent(0.0, 120.0) };

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

                var voiceCursorOffsets = new Dictionary<string, double>();
                string activeVoiceKey = "1:1";

                foreach (XElement child in measure.Elements())
                {
                    string local = child.Name.LocalName;

                    if (local == "attributes")
                    {
                        XElement divNode = child.Elements().FirstOrDefault(e => e.Name.LocalName == "divisions");
                        if (divNode != null)
                        {
                            double parsedDiv;
                            if (double.TryParse(divNode.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedDiv) && parsedDiv > 0)
                                divisions = parsedDiv;
                        }
                    }
                    else if (local == "direction")
                    {
                        double? tempo = TryReadTempoFromDirection(child);
                        if (tempo.HasValue && tempo.Value > 0.0)
                        {
                            double offsetQuarter = 0.0;
                            TryReadDirectionOffsetQuarter(child, divisions, out offsetQuarter);

                            double baseOffset = voiceCursorOffsets.ContainsKey(activeVoiceKey) ? voiceCursorOffsets[activeVoiceKey] : 0.0;
                            double tempoQuarter = currentMeasureStartQuarter + baseOffset + offsetQuarter;
                            if (tempoQuarter < currentMeasureStartQuarter)
                                tempoQuarter = currentMeasureStartQuarter;

                            tempoCandidates.Add(new TempoEvent(tempoQuarter, tempo.Value));
                        }
                    }
                    else if (local == "backup")
                    {
                        double durQuarter = DurationNodeToQuarter(child, divisions);
                        double currentOffset = voiceCursorOffsets.ContainsKey(activeVoiceKey) ? voiceCursorOffsets[activeVoiceKey] : 0.0;
                        voiceCursorOffsets[activeVoiceKey] = Math.Max(0.0, currentOffset - durQuarter);
                    }
                    else if (local == "forward")
                    {
                        double durQuarter = DurationNodeToQuarter(child, divisions);
                        double currentOffset = voiceCursorOffsets.ContainsKey(activeVoiceKey) ? voiceCursorOffsets[activeVoiceKey] : 0.0;
                        voiceCursorOffsets[activeVoiceKey] = currentOffset + durQuarter;
                    }
                    else if (local == "note")
                    {
                        bool isChordTone = child.Elements().Any(e => e.Name.LocalName == "chord");
                        bool isGrace = child.Elements().Any(e => e.Name.LocalName == "grace");
                        int staff = ParseInt(ChildValue(child, "staff"), 1);
                        int voice = ParseInt(ChildValue(child, "voice"), 1);
                        string voiceKey = voice.ToString(CultureInfo.InvariantCulture) + ":" + staff.ToString(CultureInfo.InvariantCulture);

                        double currentOffset;
                        if (!voiceCursorOffsets.TryGetValue(voiceKey, out currentOffset))
                        {
                            currentOffset = voiceCursorOffsets.ContainsKey(activeVoiceKey) ? voiceCursorOffsets[activeVoiceKey] : 0.0;
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

        List<TempoEvent> tempoMap = tempoCandidates
            .OrderBy(t => t.quarterPos)
            .GroupBy(t => t.quarterPos)
            .Select(g => g.Last())
            .ToList();

        foreach (TempoEvent t in tempoMap)
            Debug.Log($"MusicXML Tempo (global): quarter={t.quarterPos:F3} -> {t.bpm:F2} BPM");

        return tempoMap;
    }

    private static List<ParsedNote> NormalizeParsedNotes(List<ParsedNote> notes)
    {
        var deduped = new List<ParsedNote>();

        foreach (ParsedNote n in notes.OrderBy(x => x.quarterPos).ThenBy(x => x.sourceIndex))
        {
            ParsedNote existing = deduped.FirstOrDefault(x => Math.Abs(x.quarterPos - n.quarterPos) < 1e-5 && x.stringIdx == n.stringIdx && x.fret == n.fret);
            if (existing != null)
            {
                existing.durationQuarter = Math.Max(existing.durationQuarter, n.durationQuarter);
                existing.tieStart |= n.tieStart;
                existing.tieStop |= n.tieStop;
                existing.slideStart |= n.slideStart;
                existing.hammerStart |= n.hammerStart;
                existing.pullStart |= n.pullStart;
                existing.vibrato |= n.vibrato;
                existing.bendStep = Mathf.Max(existing.bendStep, n.bendStep);
                if (existing.bendVisualStartQuarter < 0.0 && n.bendVisualStartQuarter >= 0.0)
                    existing.bendVisualStartQuarter = n.bendVisualStartQuarter;
                existing.bendVisualDurationQuarter = Math.Max(existing.bendVisualDurationQuarter, n.bendVisualDurationQuarter);
                existing.bendPreBend |= n.bendPreBend;
                existing.bendRelease |= n.bendRelease;
                existing.isMuted |= n.isMuted;
                AppendTechniqueSegments(existing.techniqueSegments, n.techniqueSegments);
                existing.fromTab |= n.fromTab;
                continue;
            }

            deduped.Add(n);
        }

        var normalized = new List<ParsedNote>();

        for (int i = 0; i < deduped.Count; i++)
        {
            ParsedNote current = deduped[i];
            if (current.tieStop)
            {
                ParsedNote previous = normalized.LastOrDefault(n => n.stringIdx == current.stringIdx && n.fret == current.fret && n.quarterPos <= current.quarterPos);
                if (previous != null)
                {
                    float carriedBend = GetEndingBendValue(previous.techniqueSegments);
                    previous.durationQuarter += current.durationQuarter;
                    previous.tieStart = previous.tieStart || current.tieStart;
                    previous.vibrato = previous.vibrato || current.vibrato;
                    float previousBendStep = previous.bendStep;
                    bool previousPreBend = previous.bendPreBend;
                    previous.bendStep = Mathf.Max(previous.bendStep, current.bendStep);
                    bool startsNewVisibleBendSegment =
                        current.bendVisualStartQuarter >= 0.0 &&
                        (current.bendRelease ||
                         current.bendPreBend != previousPreBend ||
                         Math.Abs(current.bendStep - previousBendStep) > 0.01f);

                    if (startsNewVisibleBendSegment)
                    {
                        previous.bendVisualStartQuarter = current.bendVisualStartQuarter;
                        previous.bendVisualDurationQuarter = current.bendVisualDurationQuarter;
                    }
                    else if (current.bendVisualDurationQuarter > 0.0)
                    {
                        double bendSegmentEndQuarter = current.quarterPos + current.bendVisualDurationQuarter;
                        double visualStartQuarter = previous.bendVisualStartQuarter >= 0.0 ? previous.bendVisualStartQuarter : previous.quarterPos;
                        previous.bendVisualDurationQuarter = Math.Max(previous.bendVisualDurationQuarter, bendSegmentEndQuarter - visualStartQuarter);
                    }
                    previous.bendPreBend = previous.bendPreBend || current.bendPreBend;
                    previous.bendRelease = previous.bendRelease || current.bendRelease;

                    if (current.techniqueSegments != null && current.techniqueSegments.Count > 0)
                    {
                        AlignReleaseSegmentStartBend(current.techniqueSegments, carriedBend);
                        AppendTechniqueSegments(previous.techniqueSegments, current.techniqueSegments);
                    }
                    else if (carriedBend > 0.01f && current.durationQuarter > 0.0)
                    {
                        AddParsedTechniqueSegment(previous.techniqueSegments, new ParsedTechniqueSegment
                        {
                            type = current.vibrato ? NoteTechniqueSegmentType.Vibrato : NoteTechniqueSegmentType.Sustain,
                            startQuarter = current.quarterPos,
                            endQuarter = current.quarterPos + current.durationQuarter,
                            startFret = current.fret,
                            endFret = current.fret,
                            startBend = carriedBend,
                            endBend = carriedBend
                        });
                    }
                    continue;
                }
            }

            normalized.Add(current);
        }

        return normalized;
    }

    private static int[] ParsePartTuningPitches(XElement part)
    {
        if (part == null)
            return null;

        XElement firstStringedAttributes = part
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "attributes" &&
                element.Elements().Any(child => child.Name.LocalName == "staff-details" &&
                                                child.Elements().Any(grandChild => grandChild.Name.LocalName == "staff-tuning")));
        if (firstStringedAttributes == null)
            return null;

        XElement staffDetails = firstStringedAttributes.Elements().FirstOrDefault(child => child.Name.LocalName == "staff-details");
        if (staffDetails == null)
            return null;

        List<(int line, int midi)> tunings = new List<(int line, int midi)>();
        foreach (XElement tuningElement in staffDetails.Elements().Where(child => child.Name.LocalName == "staff-tuning"))
        {
            int line = ParseInt(Attr(tuningElement, "line"), 0);
            string step = ChildValue(tuningElement, "tuning-step");
            int octave = ParseInt(ChildValue(tuningElement, "tuning-octave"), int.MinValue);
            if (string.IsNullOrWhiteSpace(step) || octave == int.MinValue)
                continue;

            int alter = ParseInt(ChildValue(tuningElement, "tuning-alter"), 0);
            int pitchClass = StringTuningUtils.TryGetPitchClass(step.Trim(), out int resolvedPitchClass)
                ? resolvedPitchClass
                : -1;
            if (pitchClass < 0)
                continue;

            tunings.Add((line, ((octave + 1) * 12) + pitchClass + alter));
        }

        if (tunings.Count == 0)
            return null;

        return tunings
            .OrderBy(tuning => tuning.line)
            .Select(tuning => tuning.midi)
            .ToArray();
    }

    private static List<ParsedTechniqueSegment> BuildInitialTechniqueSegments(
        double noteStartQuarter,
        double durationQuarter,
        int fret,
        bool vibrato,
        float bendStep,
        bool bendPreBend,
        bool bendRelease,
        List<ParsedTechniqueSegment> explicitBendSegments)
    {
        var segments = new List<ParsedTechniqueSegment>();
        if (durationQuarter <= 0.0)
            return segments;

        double noteEndQuarter = noteStartQuarter + durationQuarter;

        if (explicitBendSegments != null && explicitBendSegments.Count > 0)
        {
            AppendTechniqueSegments(segments, explicitBendSegments);
            ApplyVibratoTailIfNeeded(segments, noteStartQuarter, noteEndQuarter, fret, vibrato);
            return segments;
        }

        if (bendRelease)
        {
            float targetBend = bendStep > 0.01f ? bendStep : 1f;

            if (bendPreBend)
            {
                AddParsedTechniqueSegment(segments, new ParsedTechniqueSegment
                {
                    type = NoteTechniqueSegmentType.Bend,
                    startQuarter = noteStartQuarter,
                    endQuarter = noteEndQuarter,
                    startFret = fret,
                    endFret = fret,
                    startBend = targetBend,
                    endBend = 0f
                });
            }
            else
            {
                double bendPeakQuarter = noteStartQuarter + (durationQuarter * 0.45);
                AddParsedTechniqueSegment(segments, new ParsedTechniqueSegment
                {
                    type = NoteTechniqueSegmentType.Bend,
                    startQuarter = noteStartQuarter,
                    endQuarter = bendPeakQuarter,
                    startFret = fret,
                    endFret = fret,
                    startBend = 0f,
                    endBend = targetBend
                });
                AddParsedTechniqueSegment(segments, new ParsedTechniqueSegment
                {
                    type = NoteTechniqueSegmentType.Bend,
                    startQuarter = bendPeakQuarter,
                    endQuarter = noteEndQuarter,
                    startFret = fret,
                    endFret = fret,
                    startBend = targetBend,
                    endBend = 0f
                });
            }
            return segments;
        }

        if (bendPreBend || bendStep > 0.01f)
        {
            float targetBend = bendStep > 0.01f ? bendStep : 1f;
            AddParsedTechniqueSegment(segments, new ParsedTechniqueSegment
            {
                type = bendPreBend ? NoteTechniqueSegmentType.Sustain : NoteTechniqueSegmentType.Bend,
                startQuarter = noteStartQuarter,
                endQuarter = noteEndQuarter,
                startFret = fret,
                endFret = fret,
                startBend = bendPreBend ? targetBend : 0f,
                endBend = targetBend
            });
        }
        else if (vibrato)
        {
            AddParsedTechniqueSegment(segments, new ParsedTechniqueSegment
            {
                type = NoteTechniqueSegmentType.Vibrato,
                startQuarter = noteStartQuarter,
                endQuarter = noteEndQuarter,
                startFret = fret,
                endFret = fret,
                startBend = 0f,
                endBend = 0f
            });
        }

        ApplyVibratoTailIfNeeded(segments, noteStartQuarter, noteEndQuarter, fret, vibrato);
        return segments;
    }

    private static void ApplyVibratoTailIfNeeded(
        List<ParsedTechniqueSegment> segments,
        double noteStartQuarter,
        double noteEndQuarter,
        int fret,
        bool vibrato)
    {
        if (!vibrato || segments == null || segments.Count == 0)
            return;

        ParsedTechniqueSegment last = segments[segments.Count - 1];
        if (last == null)
            return;

        if (last.type == NoteTechniqueSegmentType.Vibrato)
            return;

        float endingBend = GetEndingBendValue(segments);

        if (last.type == NoteTechniqueSegmentType.Sustain)
        {
            last.type = NoteTechniqueSegmentType.Vibrato;
            last.startBend = endingBend;
            last.endBend = endingBend;
            return;
        }

        double noteDurationQuarter = Math.Max(0.0, noteEndQuarter - noteStartQuarter);
        double lastDurationQuarter = Math.Max(0.0, last.endQuarter - last.startQuarter);
        if (noteDurationQuarter <= 0.0001 || lastDurationQuarter <= 0.0001)
            return;

        double desiredTailQuarter = Math.Max(noteDurationQuarter * 0.24, Math.Min(noteDurationQuarter * 0.42, lastDurationQuarter * 0.45));
        double vibratoStartQuarter = Math.Max(last.startQuarter, noteEndQuarter - desiredTailQuarter);

        if (vibratoStartQuarter > last.startQuarter + 0.0001)
            last.endQuarter = vibratoStartQuarter;

        AddParsedTechniqueSegment(segments, new ParsedTechniqueSegment
        {
            type = NoteTechniqueSegmentType.Vibrato,
            startQuarter = Math.Max(last.startQuarter, vibratoStartQuarter),
            endQuarter = noteEndQuarter,
            startFret = fret,
            endFret = fret,
            startBend = endingBend,
            endBend = endingBend
        });
    }

    private static List<ParsedTechniqueSegment> BuildBendTechniqueSegments(
        XElement noteNode,
        double noteStartQuarter,
        double durationQuarter,
        int fret)
    {
        var segments = new List<ParsedTechniqueSegment>();
        if (noteNode == null || durationQuarter <= 0.0)
            return segments;

        List<XElement> bendNodes = noteNode
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "bend", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (bendNodes.Count == 0)
            return segments;

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
            if (i == 0 && isPreBend)
                firstIsPreBend = true;

            currentOffset = targetOffset;
        }

        if (bendTargets.Count == 0)
            return segments;

        List<(float normalizedTime, float semitoneOffset)> points = new List<(float normalizedTime, float semitoneOffset)>();
        float startOffset = firstIsPreBend ? bendTargets[0] : 0f;
        points.Add((0f, startOffset));

        if (bendTargets.Count == 1)
        {
            float soleTarget = bendTargets[0];
            if (!Mathf.Approximately(startOffset, soleTarget))
                points.Add((firstIsPreBend ? 0.18f : 0.24f, soleTarget));
            points.Add((1f, soleTarget));
        }
        else
        {
            int firstAnimatedIndex = firstIsPreBend ? 1 : 0;
            int animatedCount = bendTargets.Count - firstAnimatedIndex;
            bool releaseLikeTail = bendTargets.Count > 1 &&
                                   (bendIsRelease[bendIsRelease.Count - 1] ||
                                    Mathf.Abs(bendTargets[bendTargets.Count - 1] - startOffset) <= 0.05f);
            float segmentStartTime = firstIsPreBend ? 0.16f : 0.24f;
            float segmentEndTime = releaseLikeTail ? 0.78f : 1f;
            for (int animatedIndex = 0; animatedIndex < animatedCount; animatedIndex++)
            {
                int targetIndex = firstAnimatedIndex + animatedIndex;
                float normalizedTime;
                if (animatedCount == 1)
                {
                    normalizedTime = segmentEndTime;
                }
                else
                {
                    float t = (animatedIndex + 1f) / animatedCount;
                    normalizedTime = Mathf.Lerp(segmentStartTime, segmentEndTime, t);
                }

                points.Add((Mathf.Clamp01(normalizedTime), bendTargets[targetIndex]));
            }

            if (!Mathf.Approximately(points[points.Count - 1].normalizedTime, 1f))
                points.Add((1f, points[points.Count - 1].semitoneOffset));
        }

        for (int pointIndex = 1; pointIndex < points.Count; pointIndex++)
        {
            (float normalizedTime, float semitoneOffset) start = points[pointIndex - 1];
            (float normalizedTime, float semitoneOffset) end = points[pointIndex];
            double segmentStartQuarter = noteStartQuarter + (durationQuarter * start.normalizedTime);
            double segmentEndQuarter = noteStartQuarter + (durationQuarter * end.normalizedTime);
            if (segmentEndQuarter <= segmentStartQuarter + 1e-6)
                continue;

            NoteTechniqueSegmentType segmentType = Mathf.Abs(end.semitoneOffset - start.semitoneOffset) <= 0.01f
                ? NoteTechniqueSegmentType.Sustain
                : NoteTechniqueSegmentType.Bend;

            AddParsedTechniqueSegment(segments, new ParsedTechniqueSegment
            {
                type = segmentType,
                startQuarter = segmentStartQuarter,
                endQuarter = segmentEndQuarter,
                startFret = fret,
                endFret = fret,
                startBend = start.semitoneOffset,
                endBend = end.semitoneOffset
            });
        }

        return segments;
    }

    private static void AddParsedTechniqueSegment(List<ParsedTechniqueSegment> segments, ParsedTechniqueSegment segment)
    {
        if (segments == null || segment == null || segment.endQuarter <= segment.startQuarter + 1e-6)
            return;

        ParsedTechniqueSegment last = segments.Count > 0 ? segments[segments.Count - 1] : null;
        if (last != null &&
            last.type == segment.type &&
            last.startFret == segment.startFret &&
            last.endFret == segment.endFret &&
            Math.Abs(last.startBend - segment.startBend) < 0.01f &&
            Math.Abs(last.endBend - segment.endBend) < 0.01f &&
            (Math.Abs(last.endQuarter - segment.startQuarter) < 1e-5 ||
             (Math.Abs(last.startQuarter - segment.startQuarter) < 1e-5 &&
              Math.Abs(last.endQuarter - segment.endQuarter) < 1e-5)))
        {
            last.startQuarter = Math.Min(last.startQuarter, segment.startQuarter);
            last.endQuarter = Math.Max(last.endQuarter, segment.endQuarter);
            return;
        }

        segments.Add(segment);
    }

    private static void AppendTechniqueSegments(List<ParsedTechniqueSegment> target, List<ParsedTechniqueSegment> source)
    {
        if (target == null || source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            ParsedTechniqueSegment segment = source[i];
            AddParsedTechniqueSegment(target, new ParsedTechniqueSegment
            {
                type = segment.type,
                startQuarter = segment.startQuarter,
                endQuarter = segment.endQuarter,
                startFret = segment.startFret,
                endFret = segment.endFret,
                startBend = segment.startBend,
                endBend = segment.endBend
            });
        }
    }

    private static float GetEndingBendValue(List<ParsedTechniqueSegment> segments)
    {
        if (segments == null)
            return 0f;

        for (int i = segments.Count - 1; i >= 0; i--)
        {
            ParsedTechniqueSegment segment = segments[i];
            if (segment == null)
                continue;

            if (segment.type == NoteTechniqueSegmentType.Bend ||
                segment.type == NoteTechniqueSegmentType.Sustain ||
                segment.type == NoteTechniqueSegmentType.Vibrato)
            {
                return segment.endBend;
            }
        }

        return 0f;
    }

    private static void AlignReleaseSegmentStartBend(List<ParsedTechniqueSegment> segments, float carriedBend)
    {
        if (segments == null || segments.Count == 0 || carriedBend <= 0.01f)
            return;

        ParsedTechniqueSegment first = segments[0];
        if (first != null &&
            first.type == NoteTechniqueSegmentType.Bend &&
            first.endBend < first.startBend)
        {
            first.startBend = Mathf.Max(carriedBend, first.startBend);
        }
    }

    private static List<NoteTechniqueSegmentData> ConvertTechniqueSegmentsToGameplay(
        List<ParsedTechniqueSegment> parsedSegments,
        double noteStartQuarter,
        List<TempoEvent> tempoMap)
    {
        if (parsedSegments == null || parsedSegments.Count == 0)
            return null;

        double noteStartSeconds = QuarterToSeconds(noteStartQuarter, tempoMap);
        var result = new List<NoteTechniqueSegmentData>(parsedSegments.Count);

        foreach (ParsedTechniqueSegment segment in parsedSegments.OrderBy(s => s.startQuarter))
        {
            float startOffset = (float)Math.Max(0.0, QuarterToSeconds(segment.startQuarter, tempoMap) - noteStartSeconds);
            float endOffset = (float)Math.Max(startOffset, QuarterToSeconds(segment.endQuarter, tempoMap) - noteStartSeconds);
            if (endOffset <= startOffset + 0.0001f)
                continue;

            result.Add(new NoteTechniqueSegmentData(
                segment.type,
                startOffset,
                endOffset,
                segment.startFret,
                segment.endFret,
                segment.startBend,
                segment.endBend));
        }

        return result.Count > 0 ? result : null;
    }

    private static bool HasBendTechniqueSegments(List<NoteTechniqueSegmentData> techniqueSegments)
    {
        return techniqueSegments != null &&
               techniqueSegments.Any(segment =>
                   segment.type == NoteTechniqueSegmentType.Bend ||
                   ((segment.type == NoteTechniqueSegmentType.Sustain || segment.type == NoteTechniqueSegmentType.Vibrato) &&
                    (Mathf.Abs(segment.startBend) > 0.01f || Mathf.Abs(segment.endBend) > 0.01f)));
    }

    private static float GetMaximumTechniqueSegmentBend(List<NoteTechniqueSegmentData> techniqueSegments)
    {
        if (techniqueSegments == null || techniqueSegments.Count == 0)
            return 0f;

        float maxBend = 0f;
        for (int i = 0; i < techniqueSegments.Count; i++)
        {
            NoteTechniqueSegmentData segment = techniqueSegments[i];
            maxBend = Mathf.Max(maxBend, Mathf.Abs(segment.startBend), Mathf.Abs(segment.endBend));
        }

        return maxBend;
    }

    private static bool StartsWithBentTechniqueSegment(List<NoteTechniqueSegmentData> techniqueSegments)
    {
        if (techniqueSegments == null || techniqueSegments.Count == 0)
            return false;

        NoteTechniqueSegmentData first = techniqueSegments
            .OrderBy(segment => segment.startOffset)
            .First();
        return Mathf.Abs(first.startBend) > 0.01f;
    }

    private static bool HasReleaseTechniqueSegment(List<NoteTechniqueSegmentData> techniqueSegments)
    {
        if (techniqueSegments == null || techniqueSegments.Count == 0)
            return false;

        for (int i = 0; i < techniqueSegments.Count; i++)
        {
            NoteTechniqueSegmentData segment = techniqueSegments[i];
            if (segment.endBend < segment.startBend - 0.01f)
                return true;
        }

        return false;
    }

    private static float GetTechniqueSegmentVisualStart(List<NoteTechniqueSegmentData> techniqueSegments)
    {
        if (techniqueSegments == null || techniqueSegments.Count == 0)
            return -1f;

        float start = float.PositiveInfinity;
        for (int i = 0; i < techniqueSegments.Count; i++)
        {
            NoteTechniqueSegmentData segment = techniqueSegments[i];
            if (segment.type != NoteTechniqueSegmentType.Bend &&
                segment.type != NoteTechniqueSegmentType.Sustain &&
                segment.type != NoteTechniqueSegmentType.Vibrato)
                continue;

            start = Mathf.Min(start, segment.startOffset);
        }

        return float.IsPositiveInfinity(start) ? -1f : start;
    }

    private static float GetTechniqueSegmentVisualDuration(List<NoteTechniqueSegmentData> techniqueSegments)
    {
        if (techniqueSegments == null || techniqueSegments.Count == 0)
            return 0f;

        float start = float.PositiveInfinity;
        float end = 0f;
        for (int i = 0; i < techniqueSegments.Count; i++)
        {
            NoteTechniqueSegmentData segment = techniqueSegments[i];
            if (segment.type != NoteTechniqueSegmentType.Bend &&
                segment.type != NoteTechniqueSegmentType.Sustain &&
                segment.type != NoteTechniqueSegmentType.Vibrato)
                continue;

            start = Mathf.Min(start, segment.startOffset);
            end = Mathf.Max(end, segment.endOffset);
        }

        if (float.IsPositiveInfinity(start))
            return 0f;

        return Mathf.Max(0f, end - start);
    }

    private static List<NoteData> BuildGameplayNotes(List<ParsedNote> parsed, List<TempoEvent> tempoMap)
    {
        var result = new List<NoteData>(parsed.Count);
        var sourceIndexToResultIndex = new Dictionary<int, int>();
        var chordIds = BuildChordIds(parsed);

        for (int i = 0; i < parsed.Count; i++)
        {
            ParsedNote n = parsed[i];
            float noteStartSeconds = (float)QuarterToSeconds(n.quarterPos, tempoMap);
            float noteDurationSeconds = (float)Math.Max(0.0, QuarterToSeconds(n.quarterPos + n.durationQuarter, tempoMap) - QuarterToSeconds(n.quarterPos, tempoMap));
            List<NoteTechniqueSegmentData> techniqueSegments = ConvertTechniqueSegmentsToGameplay(n.techniqueSegments, n.quarterPos, tempoMap);
            bool hasBendSegments = HasBendTechniqueSegments(techniqueSegments);
            float summarizedBendStep = hasBendSegments ? GetMaximumTechniqueSegmentBend(techniqueSegments) : n.bendStep;
            bool summarizedPreBend = hasBendSegments ? StartsWithBentTechniqueSegment(techniqueSegments) : n.bendPreBend;
            bool summarizedRelease = hasBendSegments ? HasReleaseTechniqueSegment(techniqueSegments) : n.bendRelease;
            float summarizedBendVisualStart = hasBendSegments
                ? noteStartSeconds + GetTechniqueSegmentVisualStart(techniqueSegments)
                : (n.bendVisualStartQuarter >= 0.0 ? (float)QuarterToSeconds(n.bendVisualStartQuarter, tempoMap) : -1f);
            float summarizedBendVisualDuration = hasBendSegments
                ? GetTechniqueSegmentVisualDuration(techniqueSegments)
                : (float)Math.Max(0.0, QuarterToSeconds(n.quarterPos + n.bendVisualDurationQuarter, tempoMap) - QuarterToSeconds(n.quarterPos, tempoMap));
            var noteData = new NoteData(
                i,
                noteStartSeconds,
                noteDurationSeconds,
                n.stringIdx,
                n.fret,
                n.note,
                chordIds[i],
                hasBendSegments || n.bendStep > 0f || n.bendPreBend || n.bendRelease ? NoteTechnique.Bend : (n.vibrato ? NoteTechnique.Vibrato : NoteTechnique.None),
                -1,
                summarizedBendStep,
                false,
                true,
                -1,
                summarizedPreBend,
                summarizedRelease,
                summarizedBendVisualStart,
                summarizedBendVisualDuration,
                techniqueSegments,
                n.isMuted);

            result.Add(noteData);
            sourceIndexToResultIndex[n.sourceIndex] = i;
        }

        for (int i = 0; i < parsed.Count; i++)
        {
            ParsedNote current = parsed[i];
            int nextIndex = FindNextLinkedNoteIndex(parsed, i);
            if (nextIndex < 0)
                continue;

            NoteTechnique technique = NoteTechnique.None;
            if (current.slideStart) technique = NoteTechnique.Slide;
            else if (current.hammerStart) technique = NoteTechnique.HammerOn;
            else if (current.pullStart) technique = NoteTechnique.PullOff;
            else continue;

            NoteData start = result[i];
            NoteData dest = result[nextIndex];
            start.technique = technique;
            start.slideTargetFret = dest.fret;
            start.duration = Mathf.Max(start.duration, Mathf.Max(0.05f, dest.time - start.time));
            if (technique == NoteTechnique.Slide)
            {
                if (start.techniqueSegments == null)
                    start.techniqueSegments = new List<NoteTechniqueSegmentData>();

                float slideEndOffset = Mathf.Max(0.05f, dest.time - start.time);
                start.techniqueSegments.Add(new NoteTechniqueSegmentData(
                    NoteTechniqueSegmentType.Slide,
                    0f,
                    slideEndOffset,
                    start.fret,
                    dest.fret,
                    0f,
                    0f));

                if (dest.techniqueSegments != null && dest.techniqueSegments.Count > 0)
                {
                    for (int segmentIndex = 0; segmentIndex < dest.techniqueSegments.Count; segmentIndex++)
                    {
                        NoteTechniqueSegmentData segment = dest.techniqueSegments[segmentIndex];
                        start.techniqueSegments.Add(new NoteTechniqueSegmentData(
                            segment.type,
                            slideEndOffset + segment.startOffset,
                            slideEndOffset + segment.endOffset,
                            segment.startFret,
                            segment.endFret,
                            segment.startBend,
                            segment.endBend));
                    }
                }
                else if (dest.duration > GuitarTechniqueVisualThresholds.SustainSeconds)
                {
                    start.techniqueSegments.Add(new NoteTechniqueSegmentData(
                        NoteTechniqueSegmentType.Sustain,
                        slideEndOffset,
                        slideEndOffset + dest.duration,
                        dest.fret,
                        dest.fret,
                        0f,
                        0f));
                }
            }
            result[i] = start;

            dest.isLegato = true;
            dest.requiresPluck = false;
            dest.linkedFromNoteId = start.id;
            result[nextIndex] = dest;
        }

        return result;
    }

    private static int[] BuildChordIds(List<ParsedNote> parsed)
    {
        int[] chordIds = new int[parsed.Count];
        int currentChordId = 0;

        for (int i = 0; i < parsed.Count; i++)
        {
            if (i == 0)
            {
                chordIds[i] = currentChordId;
                continue;
            }

            if (Math.Abs(parsed[i].quarterPos - parsed[i - 1].quarterPos) > 1e-5)
                currentChordId++;

            chordIds[i] = currentChordId;
        }

        return chordIds;
    }

    private static int FindNextLinkedNoteIndex(List<ParsedNote> parsed, int sourceIndex)
    {
        ParsedNote source = parsed[sourceIndex];
        for (int i = sourceIndex + 1; i < parsed.Count; i++)
        {
            ParsedNote next = parsed[i];
            if (next.stringIdx != source.stringIdx)
                continue;
            if (next.quarterPos + 1e-6 < source.quarterPos)
                continue;
            if (Math.Abs(next.quarterPos - source.quarterPos) < 1e-6)
                continue;
            return i;
        }
        return -1;
    }

    private static void ParseTechniqueInfo(
        XElement noteNode,
        out bool tieStart,
        out bool tieStop,
        out bool slideStart,
        out bool hammerStart,
        out bool pullStart,
        out bool vibrato,
        out float bendStep,
        out bool bendPreBend,
        out bool bendRelease)
    {
        tieStart = noteNode.Elements().Any(e => e.Name.LocalName == "tie" && Attr(e, "type") == "start");
        tieStop = noteNode.Elements().Any(e => e.Name.LocalName == "tie" && Attr(e, "type") == "stop");
        slideStart = false;
        hammerStart = false;
        pullStart = false;
        vibrato = false;
        bendStep = 0f;
        bendPreBend = false;
        bendRelease = false;

        XElement notations = noteNode.Elements().FirstOrDefault(e => e.Name.LocalName == "notations");
        if (notations == null)
            return;

        slideStart |= notations.Descendants().Any(e => e.Name.LocalName == "slide" && Attr(e, "type") == "start");
        hammerStart |= notations.Descendants().Any(e => e.Name.LocalName == "hammer-on" && Attr(e, "type") == "start");
        pullStart |= notations.Descendants().Any(e => e.Name.LocalName == "pull-off" && Attr(e, "type") == "start");
        vibrato |= notations.Descendants().Any(e => e.Name.LocalName == "wavy-line" || e.Name.LocalName == "vibrato");
        tieStart |= notations.Descendants().Any(e => e.Name.LocalName == "tied" && Attr(e, "type") == "start");
        tieStop |= notations.Descendants().Any(e => e.Name.LocalName == "tied" && Attr(e, "type") == "stop");

        XElement technical = notations.Descendants().FirstOrDefault(e => e.Name.LocalName == "technical");
        if (technical != null)
        {
            slideStart |= technical.Elements().Any(e => e.Name.LocalName == "slide" && Attr(e, "type") == "start");
            hammerStart |= technical.Elements().Any(e => e.Name.LocalName == "hammer-on" && Attr(e, "type") == "start");
            pullStart |= technical.Elements().Any(e => e.Name.LocalName == "pull-off" && Attr(e, "type") == "start");
            vibrato |= technical.Elements().Any(e => e.Name.LocalName == "vibrato");

            IEnumerable<XElement> bendNodes = technical.Elements().Where(e => e.Name.LocalName == "bend");
            bool foundAnyBend = false;
            float maxBendAlter = 0f;

            foreach (XElement bendNode in bendNodes)
            {
                foundAnyBend = true;
                bendPreBend |= bendNode.Elements().Any(e => e.Name.LocalName == "pre-bend");
                bendRelease |= bendNode.Elements().Any(e => e.Name.LocalName == "release");
                float bendAlter;
                if (float.TryParse(ChildValue(bendNode, "bend-alter"), NumberStyles.Any, CultureInfo.InvariantCulture, out bendAlter))
                    maxBendAlter = Mathf.Max(maxBendAlter, bendAlter);
                else
                    maxBendAlter = Mathf.Max(maxBendAlter, 1f);
            }

            if (foundAnyBend)
                bendStep = maxBendAlter;
        }
    }

    private static bool IsStraightMutedNote(XElement noteNode)
    {
        if (noteNode == null)
            return false;

        XElement notehead = noteNode.Elements().FirstOrDefault(e => e.Name.LocalName == "notehead");
        if (notehead != null && string.Equals(notehead.Value?.Trim(), "x", StringComparison.OrdinalIgnoreCase))
            return true;

        XElement play = noteNode.Elements().FirstOrDefault(e => e.Name.LocalName == "play");
        if (play == null)
            return false;

        return play.Elements().Any(e =>
            e.Name.LocalName == "mute" &&
            string.Equals((e.Value ?? string.Empty).Trim(), "straight", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryReadDirectionOffsetQuarter(XElement directionNode, double divisions, out double offsetQuarter)
    {
        offsetQuarter = 0.0;
        XElement offsetNode = directionNode.Elements().FirstOrDefault(e => e.Name.LocalName == "offset");
        if (offsetNode == null)
            return false;

        if (divisions <= 0.0)
            divisions = 1.0;

        double offsetDivisions;
        if (!double.TryParse(offsetNode.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out offsetDivisions))
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
            double tempo;
            if (double.TryParse(tempoAttr, NumberStyles.Any, CultureInfo.InvariantCulture, out tempo) && tempo > 0.0)
                return tempo;
        }

        XElement directionType = directionNode.Elements().FirstOrDefault(e => e.Name.LocalName == "direction-type");
        if (directionType != null)
        {
            XElement metronome = directionType.Elements().FirstOrDefault(e => e.Name.LocalName == "metronome");
            if (metronome != null)
            {
                string perMinuteText = ChildValue(metronome, "per-minute");
                double tempo;
                if (double.TryParse(perMinuteText, NumberStyles.Any, CultureInfo.InvariantCulture, out tempo) && tempo > 0.0)
                    return tempo;
            }
        }

        return null;
    }

    private static int ChoosePreferredStaff(List<ParsedNote> notes)
    {
        var grouped = notes
            .GroupBy(n => n.staff)
            .Select(g => new
            {
                Staff = g.Key,
                Count = g.Count(),
                TabCount = g.Count(x => x.fromTab)
            })
            .OrderByDescending(x => x.TabCount)
            .ThenByDescending(x => x.Count)
            .ToList();

        foreach (var g in grouped)
            Debug.Log($"MusicXML staff {g.Staff}: count={g.Count} tabCount={g.TabCount}");

        if (grouped.Count == 0)
            return 1;

        return grouped[0].Staff;
    }

    private static bool TryReadTabNote(XElement noteNode, out int stringIdx, out int fret, out int midi, out string name)
    {
        stringIdx = -1;
        fret = -1;
        midi = -1;
        name = null;

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

        stringIdx = 6 - musicXmlString;
        fret = parsedFret;
        midi = stringBasePitches[stringIdx] + fret;
        name = GetNoteName(midi);
        return true;
    }

    private static bool TryReadPitchedNote(XElement noteNode, int chromaticTranspose, out int midi, out string name)
    {
        midi = -1;
        name = null;

        XElement pitchNode = noteNode.Elements().FirstOrDefault(e => e.Name.LocalName == "pitch");
        if (pitchNode == null)
            return false;

        string step = ChildValue(pitchNode, "step");
        int alter = ParseInt(ChildValue(pitchNode, "alter"), 0);
        int octave = ParseInt(ChildValue(pitchNode, "octave"), -100);
        if (string.IsNullOrEmpty(step) || octave < -10)
            return false;

        int pitchClass = StepToPitchClass(step, alter);
        midi = (octave + 1) * 12 + pitchClass + chromaticTranspose;
        name = GetNoteName(midi);
        return true;
    }

    private static KeyValuePair<int, int>? MapMidiToGuitar(int midi)
    {
        KeyValuePair<int, int>? best = null;
        int bestFret = int.MaxValue;

        for (int s = 0; s < stringBasePitches.Length; s++)
        {
            int fret = midi - stringBasePitches[s];
            if (fret >= 0 && fret <= 24 && fret < bestFret)
            {
                bestFret = fret;
                best = new KeyValuePair<int, int>(s, fret);
            }
        }

        return best;
    }

    private static double QuarterToSeconds(double targetQuarter, List<TempoEvent> tempoMap)
    {
        double totalSeconds = 0.0;
        double previousQuarter = 0.0;
        double currentBpm = 120.0;

        for (int i = 0; i < tempoMap.Count; i++)
        {
            TempoEvent t = tempoMap[i];
            if (t.quarterPos > targetQuarter)
                break;

            double deltaQuarter = t.quarterPos - previousQuarter;
            totalSeconds += deltaQuarter * (60.0 / currentBpm);
            previousQuarter = t.quarterPos;
            currentBpm = t.bpm;
        }

        double remainingQuarter = targetQuarter - previousQuarter;
        totalSeconds += remainingQuarter * (60.0 / currentBpm);
        return totalSeconds;
    }

    private static double DurationNodeToQuarter(XElement node, double divisions)
    {
        XElement durNode = node.Elements().FirstOrDefault(e => e.Name.LocalName == "duration");
        if (durNode == null)
            return 0.0;

        double durationDiv;
        if (!double.TryParse(durNode.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out durationDiv))
            return 0.0;

        if (divisions <= 0.0)
            divisions = 1.0;

        return durationDiv / divisions;
    }

    private static int StepToPitchClass(string step, int alter)
    {
        int basePitch = 0;
        switch (step)
        {
            case "C": basePitch = 0; break;
            case "D": basePitch = 2; break;
            case "E": basePitch = 4; break;
            case "F": basePitch = 5; break;
            case "G": basePitch = 7; break;
            case "A": basePitch = 9; break;
            case "B": basePitch = 11; break;
        }
        return ((basePitch + alter) % 12 + 12) % 12;
    }

    private static string GetNoteName(int midi)
    {
        return noteNames[((midi % 12) + 12) % 12];
    }

    private static string Attr(XElement e, string attrName)
    {
        XAttribute a = e.Attribute(attrName);
        return a != null ? a.Value : "";
    }

    private static string ChildValue(XElement e, string childName)
    {
        XElement child = e.Elements().FirstOrDefault(x => x.Name.LocalName == childName);
        return child != null ? child.Value : "";
    }

    private static int ParseInt(string s, int fallback)
    {
        int value;
        if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            return value;
        return fallback;
    }
}
