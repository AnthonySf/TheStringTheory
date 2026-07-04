using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class GpTheoryParityIntegrationTests
{
    private static readonly HashSet<string> GpExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".gp",
        ".gp3",
        ".gp4",
        ".gp5",
        ".gp8",
        ".gpx"
    };

    [Test]
    public void LocalGpFixtures_EditorTheoryConversion_MatchesDirectRuntimeData()
    {
        string fixtureRoot = Path.Combine(GetProjectRoot(), "LocalTestFixtures", "GuitarPro");
        if (!Directory.Exists(fixtureRoot))
            Assert.Ignore($"No local GP fixture directory found: {fixtureRoot}");

        List<string> fixturePaths = Directory.GetFiles(fixtureRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => GpExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (fixturePaths.Count == 0)
            Assert.Ignore($"No local GP fixtures found under: {fixtureRoot}");

        List<string> failures = new List<string>();
        foreach (string fixturePath in fixturePaths)
        {
            try
            {
                List<string> differences = CompareFixture(fixturePath);
                if (differences.Count > 0)
                {
                    failures.Add(Path.GetFileName(fixturePath));
                    failures.AddRange(differences.Take(60).Select(line => "  " + line));
                }
            }
            catch (Exception ex)
            {
                failures.Add(Path.GetFileName(fixturePath));
                failures.Add("  threw: " + ex);
            }
        }

        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures.Take(400)));
    }

    private static List<string> CompareFixture(string gpPath)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "StringTheoryGpParity_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            string audioPath = Path.Combine(tempRoot, "placeholder.ogg");
            File.WriteAllBytes(audioPath, new byte[] { 0x4f, 0x67, 0x67, 0x53 });

            RuntimeSnapshot direct = RuntimeSnapshot.FromGp(gpPath);

            Assert.IsTrue(
                ChartEditorImportService.ImportChartAndAudio(gpPath, audioPath, out ChartEditorImportResult importResult, out string importError),
                importError);
            Assert.IsNotNull(importResult?.project, "Editor import did not return a project.");
            Assert.IsTrue(
                TheoryChartEditorExporter.ExportProject(importResult.project, tempRoot, out string packagePath, out string exportError),
                exportError);
            Assert.IsTrue(File.Exists(packagePath), "Editor export did not create a .theory package.");

            RuntimeSnapshot theory = RuntimeSnapshot.FromTheory(packagePath);
            return RuntimeSnapshotComparer.Compare(direct, theory);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private sealed class RuntimeSnapshot
    {
        public List<string> GeneratedPlayback = new List<string>();
        public Dictionary<string, ArrangementSnapshot> ArrangementsBySourcePartId = new Dictionary<string, ArrangementSnapshot>(StringComparer.OrdinalIgnoreCase);

        public static RuntimeSnapshot FromGp(string gpPath)
        {
            RuntimeSnapshot snapshot = new RuntimeSnapshot();
            List<MusicXmlLoader.MusicXmlPartSummary> summaries = SongNotationFacade.GetPartSummaries(gpPath, SongNotationSourceKind.Gp5)
                .Where(summary => summary != null)
                .OrderBy(summary => ArrangementKey(summary), StringComparer.OrdinalIgnoreCase)
                .ToList();
            GeneratedPlaybackArrangement generated = SongNotationFacade.LoadGeneratedArrangement(gpPath, SongNotationSourceKind.Gp5);
            snapshot.GeneratedPlayback = BuildGeneratedArrangementDigest(generated);

            for (int i = 0; i < summaries.Count; i++)
            {
                MusicXmlLoader.MusicXmlPartSummary summary = summaries[i];
                string key = ArrangementKey(summary);
                snapshot.ArrangementsBySourcePartId[key] = new ArrangementSnapshot
                {
                    Summary = BuildSummaryDigest(summary),
                    Notes = BuildNoteDigest(SongNotationFacade.LoadSong(gpPath, SongNotationSourceKind.Gp5, summary.Index)),
                    Arpeggios = BuildArpeggioDigest(SongNotationFacade.LoadArpeggioGuides(gpPath, SongNotationSourceKind.Gp5, summary.Index)),
                    StoredGeneratedChannels = BuildGeneratedChannelDigest(FilterGeneratedChannels(generated, summary)),
                    StoredGeneratedNotes = BuildGeneratedNoteDigest(FilterGeneratedNotes(generated, summary))
                };
            }

            return snapshot;
        }

        public static RuntimeSnapshot FromTheory(string packagePath)
        {
            RuntimeSnapshot snapshot = new RuntimeSnapshot();
            Assert.IsTrue(TheorySongLoader.TryLoadManifest(packagePath, out TheorySongManifest manifest), $".theory manifest could not be loaded: {packagePath}");

            List<MusicXmlLoader.MusicXmlPartSummary> summaries = SongNotationFacade.GetPartSummaries(packagePath, SongNotationSourceKind.TheoryPackage)
                .Where(summary => summary != null)
                .OrderBy(summary => ArrangementKey(summary), StringComparer.OrdinalIgnoreCase)
                .ToList();
            snapshot.GeneratedPlayback = BuildGeneratedArrangementDigest(SongNotationFacade.LoadGeneratedArrangement(packagePath, SongNotationSourceKind.TheoryPackage));

            for (int i = 0; i < summaries.Count; i++)
            {
                MusicXmlLoader.MusicXmlPartSummary summary = summaries[i];
                Assert.IsTrue(
                    TheorySongLoader.TryLoadArrangementByPartId(packagePath, summary.PartId, out _, out TheoryArrangementData arrangement),
                    $"Could not read .theory arrangement for part '{summary.PartId}'.");
                string key = FirstNonEmpty(arrangement?.generatedPart?.partId, summary.PartId, summary.GroupId, summary.Name);
                snapshot.ArrangementsBySourcePartId[key] = new ArrangementSnapshot
                {
                    Summary = BuildSummaryDigest(summary),
                    Timing = BuildTheoryTimingDigest(arrangement?.timing),
                    Notes = BuildNoteDigest(SongNotationFacade.LoadSong(packagePath, SongNotationSourceKind.TheoryPackage, summary.Index)),
                    Arpeggios = BuildArpeggioDigest(SongNotationFacade.LoadArpeggioGuides(packagePath, SongNotationSourceKind.TheoryPackage, summary.Index)),
                    StoredGeneratedChannels = BuildTheoryGeneratedChannelDigest(arrangement?.generatedChannels),
                    StoredGeneratedNotes = BuildTheoryGeneratedNoteDigest(arrangement?.generatedNotes)
                };
            }

            return snapshot;
        }
    }

    private sealed class ArrangementSnapshot
    {
        public List<string> Summary = new List<string>();
        public List<string> Timing = new List<string>();
        public List<string> Notes = new List<string>();
        public List<string> Arpeggios = new List<string>();
        public List<string> StoredGeneratedChannels = new List<string>();
        public List<string> StoredGeneratedNotes = new List<string>();
    }

    private static class RuntimeSnapshotComparer
    {
        public static List<string> Compare(RuntimeSnapshot direct, RuntimeSnapshot theory)
        {
            List<string> differences = new List<string>();
            AddListDifferences(differences, "actual generated playback", direct.GeneratedPlayback, theory.GeneratedPlayback);

            foreach (string missing in direct.ArrangementsBySourcePartId.Keys.Except(theory.ArrangementsBySourcePartId.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
                differences.Add($"missing .theory arrangement for source part: {missing}");
            foreach (string extra in theory.ArrangementsBySourcePartId.Keys.Except(direct.ArrangementsBySourcePartId.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
                differences.Add($"extra .theory arrangement for source part: {extra}");

            foreach (string key in direct.ArrangementsBySourcePartId.Keys.Intersect(theory.ArrangementsBySourcePartId.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
            {
                ArrangementSnapshot left = direct.ArrangementsBySourcePartId[key];
                ArrangementSnapshot right = theory.ArrangementsBySourcePartId[key];
                AddListDifferences(differences, $"{key} summary", left.Summary, right.Summary);
                AddListDifferences(differences, $"{key} notes", left.Notes, right.Notes);
                AddListDifferences(differences, $"{key} arpeggios", left.Arpeggios, right.Arpeggios);
                AddListDifferences(differences, $"{key} stored generated channels", left.StoredGeneratedChannels, right.StoredGeneratedChannels);
                AddListDifferences(differences, $"{key} stored generated notes", left.StoredGeneratedNotes, right.StoredGeneratedNotes);
                Assert.IsNotNull(right.Timing, $"{key} .theory timing digest should be populated.");
            }

            return differences;
        }

        private static void AddListDifferences(List<string> differences, string label, List<string> direct, List<string> theory)
        {
            direct ??= new List<string>();
            theory ??= new List<string>();
            if (direct.SequenceEqual(theory, StringComparer.Ordinal))
                return;

            differences.Add($"{label}: direct count={direct.Count}, theory count={theory.Count}");
            int shared = Math.Min(direct.Count, theory.Count);
            int reported = 0;
            for (int i = 0; i < shared && reported < 10; i++)
            {
                if (string.Equals(direct[i], theory[i], StringComparison.Ordinal))
                    continue;

                differences.Add($"{label}[{i}]");
                differences.Add($"    direct: {direct[i]}");
                differences.Add($"    theory: {theory[i]}");
                reported++;
            }

            if (reported == 0 && direct.Count != theory.Count)
            {
                if (direct.Count > shared)
                    differences.Add($"{label} first extra direct[{shared}]: {direct[shared]}");
                if (theory.Count > shared)
                    differences.Add($"{label} first extra theory[{shared}]: {theory[shared]}");
            }
        }
    }

    private static List<string> BuildSummaryDigest(MusicXmlLoader.MusicXmlPartSummary summary)
    {
        return new List<string>
        {
            string.Join("|",
                "summary",
                summary?.Name ?? string.Empty,
                summary?.InstrumentType ?? string.Empty,
                summary?.Route ?? string.Empty,
                FirstNonEmpty(summary?.GroupDisplayName, summary?.Name),
                NormalizeDifficultyLabel(summary?.DifficultyLabel, summary?.DifficultyUiIndex ?? -1),
                NormalizeDifficultyUiIndex(summary?.DifficultyUiIndex ?? -1, summary?.DifficultyLabel).ToString(CultureInfo.InvariantCulture),
                summary?.HasDifficultyVariants.ToString() ?? string.Empty,
                summary?.NoteCount.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                summary?.TabCount.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                summary?.Score.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                IntArrayDigest(summary?.StringTuningPitches),
                summary?.TuningDisplayName ?? string.Empty)
        };
    }

    private static List<string> BuildTheoryTimingDigest(TheoryTimingData timing)
    {
        List<string> digest = new List<string>
        {
            $"timing|tempo|{FormatFloat(timing?.averageTempoBpm ?? 0f)}|capo|{timing?.capo ?? 0}"
        };
        digest.AddRange((timing?.beats ?? new List<TheoryBeatData>())
            .OrderBy(beat => beat?.timeSeconds ?? 0f)
            .Select((beat, index) => string.Join("|",
                "beat",
                index.ToString(CultureInfo.InvariantCulture),
                FormatFloat(beat?.timeSeconds ?? 0f),
                (beat?.measure ?? 0).ToString(CultureInfo.InvariantCulture))));
        digest.AddRange((timing?.sections ?? new List<TheorySectionData>())
            .OrderBy(section => section?.timeSeconds ?? 0f)
            .Select((section, index) => string.Join("|",
                "section",
                index.ToString(CultureInfo.InvariantCulture),
                section?.name ?? string.Empty,
                (section?.number ?? 0).ToString(CultureInfo.InvariantCulture),
                FormatFloat(section?.timeSeconds ?? 0f))));
        return digest;
    }

    private static List<string> BuildNoteDigest(IEnumerable<NoteData> notes)
    {
        return (notes ?? Enumerable.Empty<NoteData>())
            .OrderBy(note => note.time)
            .ThenBy(note => note.stringIdx)
            .ThenBy(note => note.fret)
            .ThenBy(note => note.id)
            .Select((note, index) => string.Join("|",
                "note",
                index.ToString(CultureInfo.InvariantCulture),
                note.id.ToString(CultureInfo.InvariantCulture),
                FormatFloat(note.time),
                FormatFloat(note.duration),
                note.stringIdx.ToString(CultureInfo.InvariantCulture),
                note.fret.ToString(CultureInfo.InvariantCulture),
                note.note ?? string.Empty,
                note.chordId.ToString(CultureInfo.InvariantCulture),
                note.chordName ?? string.Empty,
                note.technique.ToString(),
                note.slideTargetFret.ToString(CultureInfo.InvariantCulture),
                FormatFloat(note.bendStep),
                note.isLegato.ToString(),
                note.requiresPluck.ToString(),
                note.linkedFromNoteId.ToString(CultureInfo.InvariantCulture),
                note.bendPreBend.ToString(),
                note.bendRelease.ToString(),
                FormatFloat(note.bendVisualStartTime),
                FormatFloat(note.bendVisualDuration),
                note.isMuted.ToString(),
                note.isPalmMute.ToString(),
                SegmentDigest(note.techniqueSegments)))
            .ToList();
    }

    private static List<string> BuildArpeggioDigest(IEnumerable<ArpeggioGuideData> guides)
    {
        return (guides ?? Enumerable.Empty<ArpeggioGuideData>())
            .OrderBy(guide => guide.startTime)
            .ThenBy(guide => guide.id)
            .Select(guide => string.Join("|",
                "arp",
                guide.id.ToString(CultureInfo.InvariantCulture),
                FormatFloat(guide.startTime),
                FormatFloat(guide.endTime),
                guide.chordName ?? string.Empty,
                IntArrayDigest(guide.stringFrets)))
            .ToList();
    }

    private static List<string> BuildGeneratedArrangementDigest(GeneratedPlaybackArrangement arrangement)
    {
        List<string> digest = new List<string>();
        if (arrangement == null)
            return digest;

        arrangement = GeneratedPlaybackArrangementFilter.CreateFiltered(arrangement, null, useAllParts: true);
        if (arrangement == null)
            return digest;

        float duration = (arrangement.notes ?? new List<GeneratedPlaybackNoteEvent>()).Count > 0
            ? arrangement.notes.Max(note => note.EndTimeSeconds)
            : Mathf.Max(0f, arrangement.durationSeconds);
        digest.Add($"generated|duration|{FormatFloat(duration)}");
        digest.AddRange((arrangement.parts ?? new List<GeneratedPlaybackPartInfo>())
            .OrderBy(part => part.partId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(part => string.Join("|",
                "generated-part",
                part.partId ?? string.Empty,
                part.displayName ?? string.Empty,
                part.instrumentName ?? string.Empty,
                part.sourceMidiChannel.ToString(CultureInfo.InvariantCulture),
                part.sourceMidiProgram.ToString(CultureInfo.InvariantCulture),
                part.preferredBank.ToString(CultureInfo.InvariantCulture),
                part.isDrum.ToString(),
                part.isGuitarFamily.ToString(),
                part.isExplicitHarmonicPart.ToString())));
        digest.AddRange(BuildGeneratedChannelDigest(arrangement.channelAssignments));
        digest.AddRange(BuildGeneratedNoteDigest(arrangement.notes));
        return digest;
    }

    private static List<string> BuildGeneratedChannelDigest(IEnumerable<GeneratedPlaybackChannelAssignment> channels)
    {
        return (channels ?? Enumerable.Empty<GeneratedPlaybackChannelAssignment>())
            .OrderBy(channel => channel.channel)
            .ThenBy(channel => channel.sourcePartId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(channel => string.Join("|",
                "generated-channel",
                channel.channel.ToString(CultureInfo.InvariantCulture),
                channel.bank.ToString(CultureInfo.InvariantCulture),
                channel.preset.ToString(CultureInfo.InvariantCulture),
                channel.isDrum.ToString(),
                channel.label ?? string.Empty,
                channel.sourcePartId ?? string.Empty,
                channel.sourcePartName ?? string.Empty,
                channel.pitchBendRangeSemitones.ToString(CultureInfo.InvariantCulture)))
            .ToList();
    }

    private static List<string> BuildTheoryGeneratedChannelDigest(IEnumerable<TheoryGeneratedChannelAssignment> channels)
    {
        return (channels ?? Enumerable.Empty<TheoryGeneratedChannelAssignment>())
            .OrderBy(channel => channel.channel)
            .ThenBy(channel => channel.sourcePartId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(channel => string.Join("|",
                "generated-channel",
                channel.channel.ToString(CultureInfo.InvariantCulture),
                channel.bank.ToString(CultureInfo.InvariantCulture),
                channel.preset.ToString(CultureInfo.InvariantCulture),
                channel.isDrum.ToString(),
                channel.label ?? string.Empty,
                channel.sourcePartId ?? string.Empty,
                channel.sourcePartName ?? string.Empty,
                channel.pitchBendRangeSemitones.ToString(CultureInfo.InvariantCulture)))
            .ToList();
    }

    private static List<string> BuildGeneratedNoteDigest(IEnumerable<GeneratedPlaybackNoteEvent> notes)
    {
        return (notes ?? Enumerable.Empty<GeneratedPlaybackNoteEvent>())
            .OrderBy(note => note.startTimeSeconds)
            .ThenBy(note => note.channel)
            .ThenBy(note => note.midiNote)
            .ThenBy(note => note.partId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select((note, index) => BuildGeneratedNoteDigest(
                index,
                note.startTimeSeconds,
                note.durationSeconds,
                note.pitchPreRollSeconds,
                note.midiNote,
                note.velocity,
                note.channel,
                note.partId,
                note.partName,
                (int)note.techniqueVariant,
                (int)note.legatoTransitionKind,
                note.attackVelocityScale,
                note.vibratoDepthSemitones,
                note.vibratoRateHz,
                note.vibratoDelayNormalized,
                note.vibratoFadeNormalized,
                note.pitchBendRangeSemitones,
                note.pitchCurve))
            .ToList();
    }

    private static List<string> BuildTheoryGeneratedNoteDigest(IEnumerable<TheoryGeneratedNoteEvent> notes)
    {
        return (notes ?? Enumerable.Empty<TheoryGeneratedNoteEvent>())
            .OrderBy(note => note.startTimeSeconds)
            .ThenBy(note => note.channel)
            .ThenBy(note => note.midiNote)
            .ThenBy(note => note.partId ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select((note, index) => BuildGeneratedNoteDigest(
                index,
                note.startTimeSeconds,
                note.durationSeconds,
                note.pitchPreRollSeconds,
                note.midiNote,
                note.velocity,
                note.channel,
                note.partId,
                note.partName,
                note.techniqueVariant,
                note.legatoTransitionKind,
                note.attackVelocityScale,
                note.vibratoDepthSemitones,
                note.vibratoRateHz,
                note.vibratoDelayNormalized,
                note.vibratoFadeNormalized,
                note.pitchBendRangeSemitones,
                note.pitchCurve?.Select(point => point == null
                    ? null
                    : new GeneratedPlaybackPitchPoint
                    {
                        normalizedTime = point.normalizedTime,
                        semitoneOffset = point.semitoneOffset
                    }).ToList()))
            .ToList();
    }

    private static string BuildGeneratedNoteDigest(
        int index,
        float startTimeSeconds,
        float durationSeconds,
        float pitchPreRollSeconds,
        int midiNote,
        int velocity,
        int channel,
        string partId,
        string partName,
        int techniqueVariant,
        int legatoTransitionKind,
        float attackVelocityScale,
        float vibratoDepthSemitones,
        float vibratoRateHz,
        float vibratoDelayNormalized,
        float vibratoFadeNormalized,
        int pitchBendRangeSemitones,
        List<GeneratedPlaybackPitchPoint> pitchCurve)
    {
        return string.Join("|",
            "generated-note",
            index.ToString(CultureInfo.InvariantCulture),
            FormatFloat(startTimeSeconds),
            FormatFloat(durationSeconds),
            FormatFloat(pitchPreRollSeconds),
            midiNote.ToString(CultureInfo.InvariantCulture),
            velocity.ToString(CultureInfo.InvariantCulture),
            channel.ToString(CultureInfo.InvariantCulture),
            partId ?? string.Empty,
            partName ?? string.Empty,
            techniqueVariant.ToString(CultureInfo.InvariantCulture),
            legatoTransitionKind.ToString(CultureInfo.InvariantCulture),
            FormatFloat(attackVelocityScale),
            FormatFloat(vibratoDepthSemitones),
            FormatFloat(vibratoRateHz),
            FormatFloat(vibratoDelayNormalized),
            FormatFloat(vibratoFadeNormalized),
            pitchBendRangeSemitones.ToString(CultureInfo.InvariantCulture),
            PitchCurveDigest(pitchCurve));
    }

    private static List<GeneratedPlaybackChannelAssignment> FilterGeneratedChannels(GeneratedPlaybackArrangement arrangement, MusicXmlLoader.MusicXmlPartSummary summary)
    {
        HashSet<string> ids = BuildCandidateIds(summary);
        return (arrangement?.channelAssignments ?? new List<GeneratedPlaybackChannelAssignment>())
            .Where(channel => channel != null &&
                              MatchesChannelIdentity(channel, ids))
            .ToList();
    }

    private static List<GeneratedPlaybackNoteEvent> FilterGeneratedNotes(GeneratedPlaybackArrangement arrangement, MusicXmlLoader.MusicXmlPartSummary summary)
    {
        HashSet<string> ids = BuildCandidateIds(summary);
        HashSet<int> channels = new HashSet<int>(FilterGeneratedChannels(arrangement, summary).Select(channel => channel.channel));
        return (arrangement?.notes ?? new List<GeneratedPlaybackNoteEvent>())
            .Where(note => MatchesGeneratedNoteIdentity(note, ids, channels))
            .ToList();
    }

    private static bool MatchesGeneratedNoteIdentity(
        GeneratedPlaybackNoteEvent note,
        HashSet<string> ids,
        HashSet<int> channels)
    {
        if (note == null)
            return false;

        if (ids != null && ids.Contains(note.partId ?? string.Empty))
            return true;

        return (ids == null || ids.Count == 0 || string.IsNullOrWhiteSpace(note.partId)) &&
               channels != null &&
               channels.Contains(note.channel);
    }

    private static bool MatchesChannelIdentity(GeneratedPlaybackChannelAssignment channel, HashSet<string> ids)
    {
        if (channel == null || ids == null || ids.Count == 0)
            return false;

        bool hasSourceIdentity = !string.IsNullOrWhiteSpace(channel.sourcePartId) ||
                                 !string.IsNullOrWhiteSpace(channel.sourcePartName);
        return ids.Contains(channel.sourcePartId ?? string.Empty) ||
               ids.Contains(channel.sourcePartName ?? string.Empty) ||
               (!hasSourceIdentity && ids.Contains(channel.label ?? string.Empty));
    }

    private static HashSet<string> BuildCandidateIds(MusicXmlLoader.MusicXmlPartSummary summary)
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddId(ids, summary?.PartId);
        AddId(ids, summary?.GroupId);
        AddId(ids, summary?.Name);
        AddId(ids, summary?.GroupDisplayName);
        return ids;
    }

    private static void AddId(HashSet<string> ids, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            ids.Add(value.Trim());
    }

    private static string SegmentDigest(List<NoteTechniqueSegmentData> segments)
    {
        if (segments == null || segments.Count == 0)
            return string.Empty;

        return string.Join(";", segments.Select(segment => string.Join(",",
            segment.type.ToString(),
            FormatFloat(segment.startOffset),
            FormatFloat(segment.endOffset),
            segment.startFret.ToString(CultureInfo.InvariantCulture),
            segment.endFret.ToString(CultureInfo.InvariantCulture),
            FormatFloat(segment.startBend),
            FormatFloat(segment.endBend))));
    }

    private static string PitchCurveDigest(List<GeneratedPlaybackPitchPoint> points)
    {
        if (points == null || points.Count == 0)
            return string.Empty;

        return string.Join(";", points
            .Where(point => point != null)
            .Select(point => $"{FormatFloat(point.normalizedTime)},{FormatFloat(point.semitoneOffset)}"));
    }

    private static string ArrangementKey(MusicXmlLoader.MusicXmlPartSummary summary)
    {
        return FirstNonEmpty(summary?.PartId, summary?.GroupId, summary?.Name, summary?.Index.ToString(CultureInfo.InvariantCulture));
    }

    private static string IntArrayDigest(IEnumerable<int> values)
    {
        return values == null ? string.Empty : string.Join(",", values);
    }

    private static string FormatFloat(float value)
    {
        return Math.Round(value, 5, MidpointRounding.AwayFromZero).ToString("0.#####", CultureInfo.InvariantCulture);
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return string.Empty;

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return string.Empty;
    }

    private static string NormalizeDifficultyLabel(string label, int uiIndex)
    {
        if (!string.IsNullOrWhiteSpace(label))
            return label.Trim();
        return uiIndex <= 0 ? "Full" : uiIndex.ToString(CultureInfo.InvariantCulture);
    }

    private static int NormalizeDifficultyUiIndex(int uiIndex, string label)
    {
        if (uiIndex >= 0)
            return uiIndex;
        if (string.Equals(label?.Trim(), "Full", StringComparison.OrdinalIgnoreCase))
            return 0;
        return int.TryParse(label, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? Math.Max(0, parsed)
            : 0;
    }

    private static string GetProjectRoot()
    {
        return Directory.GetParent(Application.dataPath)?.FullName ?? Environment.CurrentDirectory;
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // Temporary test output can be cleaned up by the OS if a file handle lingers.
        }
    }
}

public static class GpTheoryParityCommandLine
{
    public static void Run()
    {
        string resultPath = GetArgumentValue("-gpTheoryParityResult");
        if (string.IsNullOrWhiteSpace(resultPath))
            resultPath = Path.Combine(Path.GetTempPath(), "gp_theory_parity_commandline.txt");

        try
        {
            new GpTheoryParityIntegrationTests().LocalGpFixtures_EditorTheoryConversion_MatchesDirectRuntimeData();
            File.WriteAllText(resultPath, "PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText(resultPath, "FAIL" + Environment.NewLine + ex);
            EditorApplication.Exit(1);
        }
    }

    private static string GetArgumentValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return string.Empty;
    }
}
