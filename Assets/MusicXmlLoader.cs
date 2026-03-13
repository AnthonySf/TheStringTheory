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
        public int NoteCount;
        public int TabCount;
        public int Score;
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
                    $"[XML IMPORT] idx={i} t={result[i].time:F3}s Δ={delta:F3}s string={result[i].stringIdx} fret={result[i].fret} note={result[i].note} tech={result[i].technique} pluck={result[i].requiresPluck}");
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

            summaries.Add(new MusicXmlPartSummary
            {
                Index = i,
                PartId = id,
                Name = name,
                NoteCount = noteCount,
                TabCount = tabCount,
                Score = score
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
                            out bool vibrato, out float bendStep);

                        if (TryReadTabNote(child, out stringIdx, out fret, out midi, out name))
                        {
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
                                bendStep = bendStep
                            });
                        }
                        else if (TryReadPitchedNote(child, chromaticTranspose, out midi, out name))
                        {
                            var mapped = MapMidiToGuitar(midi);
                            if (mapped.HasValue)
                            {
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
                                    bendStep = bendStep
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
                    previous.durationQuarter += current.durationQuarter;
                    previous.tieStart = previous.tieStart || current.tieStart;
                    previous.vibrato = previous.vibrato || current.vibrato;
                    previous.bendStep = Mathf.Max(previous.bendStep, current.bendStep);
                    continue;
                }
            }

            normalized.Add(current);
        }

        return normalized;
    }

    private static List<NoteData> BuildGameplayNotes(List<ParsedNote> parsed, List<TempoEvent> tempoMap)
    {
        var result = new List<NoteData>(parsed.Count);
        var sourceIndexToResultIndex = new Dictionary<int, int>();
        var chordIds = BuildChordIds(parsed);

        for (int i = 0; i < parsed.Count; i++)
        {
            ParsedNote n = parsed[i];
            var noteData = new NoteData(
                i,
                (float)QuarterToSeconds(n.quarterPos, tempoMap),
                (float)Math.Max(0.0, QuarterToSeconds(n.quarterPos + n.durationQuarter, tempoMap) - QuarterToSeconds(n.quarterPos, tempoMap)),
                n.stringIdx,
                n.fret,
                n.note,
                chordIds[i],
                n.bendStep > 0f ? NoteTechnique.Bend : (n.vibrato ? NoteTechnique.Vibrato : NoteTechnique.None),
                -1,
                n.bendStep,
                false,
                true,
                -1);

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
        out float bendStep)
    {
        tieStart = noteNode.Elements().Any(e => e.Name.LocalName == "tie" && Attr(e, "type") == "start");
        tieStop = noteNode.Elements().Any(e => e.Name.LocalName == "tie" && Attr(e, "type") == "stop");
        slideStart = false;
        hammerStart = false;
        pullStart = false;
        vibrato = false;
        bendStep = 0f;

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

            XElement bendNode = technical.Elements().FirstOrDefault(e => e.Name.LocalName == "bend");
            if (bendNode != null)
            {
                float bendAlter;
                if (float.TryParse(ChildValue(bendNode, "bend-alter"), NumberStyles.Any, CultureInfo.InvariantCulture, out bendAlter))
                    bendStep = bendAlter;
                else
                    bendStep = 1f;
            }
        }
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
