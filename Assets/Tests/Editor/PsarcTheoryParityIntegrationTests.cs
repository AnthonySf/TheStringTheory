using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

public sealed class PsarcTheoryParityIntegrationTests
{
    private const int MinimumFixtureCount = 10;
    private const int ImporterTimeoutMilliseconds = 60 * 60 * 1000;
    private const string FixtureRootEnvironmentVariable = "STRING_THEORY_PSARC_PARITY_ROOT";
    private const string OutputRootEnvironmentVariable = "STRING_THEORY_PSARC_PARITY_OUTPUT_ROOT";
    private const string DefaultFixtureRelativePath = "LocalTestFixtures/PsarcTheoryParity";

    public static void RunFromCommandLine()
    {
        string resultPath = GetCommandLineValue("-psarcParityResultPath") ??
                            Path.Combine(ProjectRoot, "Temp", "PsarcTheoryParity", "commandline-result.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath) ?? ProjectRoot);

        try
        {
            new PsarcTheoryParityIntegrationTests().RealPsarcTheoryConversion_MatchesLegacyRuntimeCacheGameData();
            File.WriteAllText(resultPath, "PASS" + Environment.NewLine);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            File.WriteAllText(resultPath, "FAIL" + Environment.NewLine + ex);
            Debug.LogError(ex);
            EditorApplication.Exit(1);
        }
    }

    [Test]
    [Explicit("Requires private local PSARC fixtures. Set STRING_THEORY_PSARC_PARITY_ROOT or use LocalTestFixtures/PsarcTheoryParity.")]
    public void RealPsarcTheoryConversion_MatchesLegacyRuntimeCacheGameData()
    {
        string fixtureRoot = ResolveFixtureRoot();
        if (!Directory.Exists(fixtureRoot))
            Assert.Inconclusive($"PSARC parity fixture folder was not found: {fixtureRoot}");

        List<string> psarcPaths = Directory.GetFiles(fixtureRoot, "*.psarc", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (psarcPaths.Count == 0)
            Assert.Inconclusive($"No PSARC fixtures were found in: {fixtureRoot}");

        Assert.GreaterOrEqual(
            psarcPaths.Count,
            MinimumFixtureCount,
            $"Expected at least {MinimumFixtureCount} PSARC fixtures in {fixtureRoot}.");

        string importerPath = ResolveImporterExecutablePath();
        Assert.IsTrue(File.Exists(importerPath), $"Psarc importer executable was not found: {importerPath}");

        string outputRoot = ResolveOutputRoot();
        Directory.CreateDirectory(outputRoot);

        List<string> failures = new List<string>();
        for (int i = 0; i < psarcPaths.Count; i++)
        {
            string psarcPath = psarcPaths[i];
            string caseName = $"{(i + 1).ToString("00", CultureInfo.InvariantCulture)}_{SanitizePathSegment(Path.GetFileNameWithoutExtension(psarcPath))}";
            string caseRoot = Path.Combine(outputRoot, caseName);
            ResetDirectory(caseRoot);

            try
            {
                string theoryPath = Path.Combine(caseRoot, "converted.theory");
                string cacheDirectory = Path.Combine(caseRoot, "legacy-cache");
                string importerOutput = RunImporter(importerPath, psarcPath, theoryPath, cacheDirectory);
                string manifestPath = Path.Combine(cacheDirectory, PsarcCachedSongFormat.ManifestFileName);

                Assert.IsTrue(File.Exists(theoryPath), $"Importer did not create .theory output for {Path.GetFileName(psarcPath)}.\n{importerOutput}");
                Assert.IsTrue(File.Exists(manifestPath), $"Importer did not leave the legacy cache manifest for {Path.GetFileName(psarcPath)}.\n{importerOutput}");

                RuntimeSongSnapshot legacy = RuntimeSongSnapshot.FromLegacyCache(manifestPath);
                RuntimeSongSnapshot theory = RuntimeSongSnapshot.FromTheoryPackage(theoryPath);
                List<string> differences = RuntimeSongSnapshotComparer.Compare(legacy, theory);
                if (differences.Count > 0)
                {
                    string legacySnapshotPath = Path.Combine(caseRoot, "legacy-runtime-snapshot.txt");
                    string theorySnapshotPath = Path.Combine(caseRoot, "theory-runtime-snapshot.txt");
                    File.WriteAllText(legacySnapshotPath, legacy.ToSnapshotText());
                    File.WriteAllText(theorySnapshotPath, theory.ToSnapshotText());

                    failures.Add(
                        $"{Path.GetFileName(psarcPath)} failed parity with {differences.Count} difference(s):\n" +
                        string.Join("\n", differences.Take(40)) +
                        $"\nFull snapshots:\n  legacy: {legacySnapshotPath}\n  theory: {theorySnapshotPath}");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(psarcPath)} threw during parity test:\n{ex}");
            }
        }

        if (failures.Count > 0)
            Assert.Fail(string.Join("\n\n", failures));
    }

    private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    private static string ResolveFixtureRoot()
    {
        string fromEnvironment = Environment.GetEnvironmentVariable(FixtureRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return Path.GetFullPath(fromEnvironment);

        return Path.GetFullPath(Path.Combine(ProjectRoot, DefaultFixtureRelativePath));
    }

    private static string ResolveOutputRoot()
    {
        string fromCommandLine = GetCommandLineValue("-psarcParityOutputRoot");
        if (!string.IsNullOrWhiteSpace(fromCommandLine))
            return Path.GetFullPath(fromCommandLine);

        string fromEnvironment = Environment.GetEnvironmentVariable(OutputRootEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return Path.GetFullPath(fromEnvironment);

        return Path.GetFullPath(Path.Combine(ProjectRoot, "Temp", "PsarcTheoryParity"));
    }

    private static string GetCommandLineValue(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    private static string ResolveImporterExecutablePath()
    {
        string streamingImporter = Path.Combine(
            Application.dataPath,
            "StreamingAssets",
            "Importers",
            "rocksmith-psarc",
            "RocksmithImport",
            "RocksmithImportTool.exe");
        if (File.Exists(streamingImporter))
            return streamingImporter;

        return Path.Combine(
            ProjectRoot,
            "External",
            "RocksmithImportTool",
            "bin",
            "Debug",
            "net9.0",
            "win-x64",
            "RocksmithImportTool.exe");
    }

    private static string RunImporter(string importerPath, string psarcPath, string theoryPath, string cacheDirectory)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(theoryPath) ?? string.Empty);
        Directory.CreateDirectory(cacheDirectory);

        StringBuilder output = new StringBuilder();
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = importerPath,
            Arguments = string.Join(" ", new[]
            {
                "import-theory",
                "--source", Quote(psarcPath),
                "--output", Quote(theoryPath),
                "--work", Quote(cacheDirectory)
            }),
            WorkingDirectory = Path.GetDirectoryName(importerPath) ?? ProjectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using Process process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                output.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(ImporterTimeoutMilliseconds))
        {
            TryKill(process);
            Assert.Fail($"Psarc importer timed out after {ImporterTimeoutMilliseconds / 60000} minute(s) for {psarcPath}.\n{output}");
        }

        process.WaitForExit();
        string processOutput = output.ToString();
        Assert.AreEqual(0, process.ExitCode, $"Psarc importer failed for {psarcPath}.\n{processOutput}");
        return processOutput;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (process != null && !process.HasExited)
                process.Kill();
        }
        catch
        {
        }
    }

    private static void ResetDirectory(string directory)
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
        Directory.CreateDirectory(directory);
    }

    private static string Quote(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }

    private static string SanitizePathSegment(string value)
    {
        string sanitized = value ?? string.Empty;
        foreach (char invalid in Path.GetInvalidFileNameChars())
            sanitized = sanitized.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(sanitized) ? "psarc" : sanitized;
    }

    private sealed class RuntimeSongSnapshot
    {
        public string Title;
        public string Artist;
        public float DurationSeconds;
        public List<string> SummaryOrder = new List<string>();
        public List<RuntimeArrangementSnapshot> Arrangements = new List<RuntimeArrangementSnapshot>();
        public List<string> GeneratedPlayback = new List<string>();

        public static RuntimeSongSnapshot FromLegacyCache(string manifestPath)
        {
            Assert.IsTrue(LegacyPsarcCacheRuntimeLoader.TryLoadManifest(manifestPath, out PsarcCachedSongManifest manifest), $"Legacy cache manifest could not be loaded: {manifestPath}");

            RuntimeSongSnapshot snapshot = new RuntimeSongSnapshot
            {
                Title = manifest.displayName ?? string.Empty,
                Artist = manifest.artist ?? string.Empty,
                DurationSeconds = manifest.durationSeconds
            };

            List<MusicXmlLoader.MusicXmlPartSummary> summaries = LegacyPsarcCacheRuntimeLoader.GetPartSummaries(manifestPath);
            snapshot.SummaryOrder = summaries.Select(BuildSummaryDigest).ToList();
            for (int i = 0; i < summaries.Count; i++)
            {
                MusicXmlLoader.MusicXmlPartSummary summary = summaries[i];
                List<NoteData> notes = LegacyPsarcCacheRuntimeLoader.LoadSong(manifestPath, summary.Index);
                List<ArpeggioGuideData> arpeggios = LegacyPsarcCacheRuntimeLoader.LoadArpeggioGuides(manifestPath, summary.Index);
                Assert.IsTrue(
                    LegacyPsarcCacheRuntimeLoader.TryLoadArrangementPart(manifestPath, summary.Index, out _, out PsarcCachedArrangementPart part),
                    $"Legacy cache arrangement could not be loaded: {summary.PartId}");

                snapshot.Arrangements.Add(RuntimeArrangementSnapshot.FromLegacy(summary, notes, arpeggios, part));
            }

            snapshot.GeneratedPlayback = BuildGeneratedArrangementDigest(LegacyPsarcCacheRuntimeLoader.LoadGeneratedArrangement(manifestPath));
            return snapshot;
        }

        public static RuntimeSongSnapshot FromTheoryPackage(string packagePath)
        {
            Assert.IsTrue(TheorySongLoader.TryLoadManifest(packagePath, out TheorySongManifest manifest), $".theory manifest could not be loaded: {packagePath}");

            RuntimeSongSnapshot snapshot = new RuntimeSongSnapshot
            {
                Title = manifest.title ?? string.Empty,
                Artist = manifest.artist ?? string.Empty,
                DurationSeconds = manifest.durationSeconds
            };

            List<MusicXmlLoader.MusicXmlPartSummary> summaries = SongNotationFacade.GetPartSummaries(packagePath, SongNotationSourceKind.TheoryPackage);
            snapshot.SummaryOrder = summaries.Select(BuildSummaryDigest).ToList();
            for (int i = 0; i < summaries.Count; i++)
            {
                MusicXmlLoader.MusicXmlPartSummary summary = summaries[i];
                List<NoteData> notes = SongNotationFacade.LoadSong(packagePath, SongNotationSourceKind.TheoryPackage, summary.Index);
                List<ArpeggioGuideData> arpeggios = SongNotationFacade.LoadArpeggioGuides(packagePath, SongNotationSourceKind.TheoryPackage, summary.Index);
                Assert.IsTrue(
                    TheorySongLoader.TryLoadArrangementByPartId(packagePath, summary.PartId, out _, out TheoryArrangementData arrangement),
                    $".theory arrangement could not be loaded: {summary.PartId}");

                snapshot.Arrangements.Add(RuntimeArrangementSnapshot.FromTheory(summary, notes, arpeggios, arrangement, packagePath));
            }

            snapshot.GeneratedPlayback = BuildGeneratedArrangementDigest(SongNotationFacade.LoadGeneratedArrangement(packagePath, SongNotationSourceKind.TheoryPackage));
            return snapshot;
        }

        public string ToSnapshotText()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"title|{Title}");
            builder.AppendLine($"artist|{Artist}");
            builder.AppendLine($"duration|{FormatFloat(DurationSeconds)}");
            builder.AppendLine("[summary-order]");
            foreach (string item in SummaryOrder)
                builder.AppendLine(item);
            builder.AppendLine("[arrangements]");
            foreach (RuntimeArrangementSnapshot arrangement in Arrangements.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                arrangement.AppendTo(builder);
            builder.AppendLine("[generated-playback]");
            foreach (string item in GeneratedPlayback)
                builder.AppendLine(item);
            return builder.ToString();
        }
    }

    private sealed class RuntimeArrangementSnapshot
    {
        public string Key;
        public List<string> Summary = new List<string>();
        public List<string> Timing = new List<string>();
        public List<string> Tones = new List<string>();
        public List<string> Notes = new List<string>();
        public List<string> Arpeggios = new List<string>();
        public List<string> StoredGeneratedNotes = new List<string>();

        public static RuntimeArrangementSnapshot FromLegacy(
            MusicXmlLoader.MusicXmlPartSummary summary,
            List<NoteData> notes,
            List<ArpeggioGuideData> arpeggios,
            PsarcCachedArrangementPart part)
        {
            return new RuntimeArrangementSnapshot
            {
                Key = BuildArrangementKey(summary),
                Summary = new List<string> { BuildSummaryDigest(summary) },
                Timing = BuildCachedTimingDigest(part?.timing),
                Tones = BuildCachedToneDigest(part?.tones),
                Notes = BuildNoteDigest(notes),
                Arpeggios = BuildArpeggioDigest(arpeggios),
                StoredGeneratedNotes = BuildCachedGeneratedNoteDigest(part?.generatedNotes)
            };
        }

        public static RuntimeArrangementSnapshot FromTheory(
            MusicXmlLoader.MusicXmlPartSummary summary,
            List<NoteData> notes,
            List<ArpeggioGuideData> arpeggios,
            TheoryArrangementData arrangement,
            string packagePath)
        {
            return new RuntimeArrangementSnapshot
            {
                Key = BuildArrangementKey(summary),
                Summary = new List<string> { BuildSummaryDigest(summary) },
                Timing = BuildTheoryTimingDigest(arrangement?.timing),
                Tones = BuildTheoryToneDigest(arrangement?.tones, packagePath),
                Notes = BuildNoteDigest(notes),
                Arpeggios = BuildArpeggioDigest(arpeggios),
                StoredGeneratedNotes = BuildTheoryGeneratedNoteDigest(arrangement?.generatedNotes)
            };
        }

        public void AppendTo(StringBuilder builder)
        {
            builder.AppendLine($"arrangement|{Key}");
            AppendSection(builder, "summary", Summary);
            AppendSection(builder, "timing", Timing);
            AppendSection(builder, "tones", Tones);
            AppendSection(builder, "notes", Notes);
            AppendSection(builder, "arpeggios", Arpeggios);
            AppendSection(builder, "stored-generated-notes", StoredGeneratedNotes);
        }

        private static void AppendSection(StringBuilder builder, string name, List<string> lines)
        {
            builder.AppendLine($"[{name}]");
            foreach (string line in lines ?? new List<string>())
                builder.AppendLine(line);
        }
    }

    private static class RuntimeSongSnapshotComparer
    {
        public static List<string> Compare(RuntimeSongSnapshot legacy, RuntimeSongSnapshot theory)
        {
            List<string> differences = new List<string>();
            AddValueDifference(differences, "title", legacy.Title, theory.Title);
            AddValueDifference(differences, "artist", legacy.Artist, theory.Artist);
            AddValueDifference(differences, "duration", FormatFloat(legacy.DurationSeconds), FormatFloat(theory.DurationSeconds));
            AddListDifferences(differences, "summary order", legacy.SummaryOrder, theory.SummaryOrder);
            AddListDifferences(differences, "actual generated playback", legacy.GeneratedPlayback, theory.GeneratedPlayback);

            Dictionary<string, RuntimeArrangementSnapshot> legacyByKey = legacy.Arrangements.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, RuntimeArrangementSnapshot> theoryByKey = theory.Arrangements.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
            foreach (string missing in legacyByKey.Keys.Except(theoryByKey.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
                differences.Add($"missing theory arrangement: {missing}");
            foreach (string extra in theoryByKey.Keys.Except(legacyByKey.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
                differences.Add($"extra theory arrangement: {extra}");

            foreach (string key in legacyByKey.Keys.Intersect(theoryByKey.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
            {
                RuntimeArrangementSnapshot left = legacyByKey[key];
                RuntimeArrangementSnapshot right = theoryByKey[key];
                AddListDifferences(differences, $"{key} summary", left.Summary, right.Summary);
                AddListDifferences(differences, $"{key} timing", left.Timing, right.Timing);
                AddListDifferences(differences, $"{key} tones", left.Tones, right.Tones);
                AddListDifferences(differences, $"{key} notes", left.Notes, right.Notes);
                AddListDifferences(differences, $"{key} arpeggios", left.Arpeggios, right.Arpeggios);
                AddListDifferences(differences, $"{key} stored generated notes", left.StoredGeneratedNotes, right.StoredGeneratedNotes);
            }

            return differences;
        }

        private static void AddValueDifference(List<string> differences, string label, string legacy, string theory)
        {
            if (!string.Equals(legacy ?? string.Empty, theory ?? string.Empty, StringComparison.Ordinal))
                differences.Add($"{label}: legacy='{legacy}' theory='{theory}'");
        }

        private static void AddListDifferences(List<string> differences, string label, List<string> legacy, List<string> theory)
        {
            legacy ??= new List<string>();
            theory ??= new List<string>();
            if (legacy.SequenceEqual(theory, StringComparer.Ordinal))
                return;

            differences.Add($"{label}: legacy count={legacy.Count}, theory count={theory.Count}");
            int shared = Math.Min(legacy.Count, theory.Count);
            int reported = 0;
            for (int i = 0; i < shared && reported < 10; i++)
            {
                if (string.Equals(legacy[i], theory[i], StringComparison.Ordinal))
                    continue;

                differences.Add($"{label}[{i}]\n  legacy: {legacy[i]}\n  theory: {theory[i]}");
                reported++;
            }

            if (reported == 0 && legacy.Count != theory.Count)
            {
                if (legacy.Count > shared)
                    differences.Add($"{label} first extra legacy[{shared}]: {legacy[shared]}");
                if (theory.Count > shared)
                    differences.Add($"{label} first extra theory[{shared}]: {theory[shared]}");
            }
        }
    }

    private static string BuildArrangementKey(MusicXmlLoader.MusicXmlPartSummary summary)
    {
        if (!string.IsNullOrWhiteSpace(summary?.PartId))
            return summary.PartId.Trim();

        return string.Join("|",
            summary?.GroupId ?? string.Empty,
            summary?.Route ?? string.Empty,
            summary?.DifficultyUiIndex.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            summary?.DifficultyLabel ?? string.Empty);
    }

    private static string BuildSummaryDigest(MusicXmlLoader.MusicXmlPartSummary summary)
    {
        return string.Join("|",
            "summary",
            summary?.Index.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            summary?.PartId ?? string.Empty,
            summary?.Name ?? string.Empty,
            summary?.InstrumentType ?? string.Empty,
            summary?.Route ?? string.Empty,
            summary?.GroupId ?? string.Empty,
            summary?.GroupDisplayName ?? string.Empty,
            summary?.DifficultyLabel ?? string.Empty,
            summary?.DifficultyUiIndex.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            summary?.HasDifficultyVariants.ToString() ?? string.Empty,
            summary?.NoteCount.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            summary?.TabCount.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            summary?.Score.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            IntArrayDigest(summary?.StringTuningPitches),
            summary?.TuningDisplayName ?? string.Empty);
    }

    private static List<string> BuildNoteDigest(List<NoteData> notes)
    {
        List<string> digest = new List<string>();
        if (notes == null)
            return digest;

        for (int i = 0; i < notes.Count; i++)
        {
            NoteData note = notes[i];
            digest.Add(string.Join("|",
                "note",
                i.ToString(CultureInfo.InvariantCulture),
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
                SegmentDigest(note.techniqueSegments)));
        }

        return digest;
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

    private static List<string> BuildArpeggioDigest(List<ArpeggioGuideData> guides)
    {
        return guides?
            .Select(guide => string.Join("|",
                "arp",
                guide.id.ToString(CultureInfo.InvariantCulture),
                FormatFloat(guide.startTime),
                FormatFloat(guide.endTime),
                guide.chordName ?? string.Empty,
                IntArrayDigest(guide.stringFrets)))
            .ToList() ?? new List<string>();
    }

    private static List<string> BuildCachedTimingDigest(PsarcCachedArrangementTimingData timing)
    {
        List<string> digest = new List<string>
        {
            $"timing|tempo|{FormatFloat(timing?.averageTempoBpm ?? 0f)}|capo|{timing?.capo ?? 0}"
        };
        digest.AddRange((timing?.ebeats ?? new List<PsarcCachedEbeatData>())
            .Select((beat, index) => string.Join("|",
                "beat",
                index.ToString(CultureInfo.InvariantCulture),
                FormatFloat(beat.timeSeconds),
                beat.measure.ToString(CultureInfo.InvariantCulture))));
        digest.AddRange((timing?.sections ?? new List<PsarcCachedSectionData>())
            .Select((section, index) => string.Join("|",
                "section",
                index.ToString(CultureInfo.InvariantCulture),
                section.name ?? string.Empty,
                section.number.ToString(CultureInfo.InvariantCulture),
                FormatFloat(section.timeSeconds))));
        return digest;
    }

    private static List<string> BuildTheoryTimingDigest(TheoryTimingData timing)
    {
        List<string> digest = new List<string>
        {
            $"timing|tempo|{FormatFloat(timing?.averageTempoBpm ?? 0f)}|capo|{timing?.capo ?? 0}"
        };
        digest.AddRange((timing?.beats ?? new List<TheoryBeatData>())
            .Select((beat, index) => string.Join("|",
                "beat",
                index.ToString(CultureInfo.InvariantCulture),
                FormatFloat(beat.timeSeconds),
                beat.measure.ToString(CultureInfo.InvariantCulture))));
        digest.AddRange((timing?.sections ?? new List<TheorySectionData>())
            .Select((section, index) => string.Join("|",
                "section",
                index.ToString(CultureInfo.InvariantCulture),
                section.name ?? string.Empty,
                section.number.ToString(CultureInfo.InvariantCulture),
                FormatFloat(section.timeSeconds))));
        return digest;
    }

    private static List<string> BuildCachedToneDigest(PsarcCachedArrangementToneData tones)
    {
        List<string> digest = new List<string> { $"tone|base|{tones?.baseToneName ?? string.Empty}" };
        digest.AddRange((tones?.changes ?? new List<PsarcCachedToneChangeData>())
            .Select((change, index) => string.Join("|",
                "tone-change",
                index.ToString(CultureInfo.InvariantCulture),
                FormatFloat(change.timeSeconds),
                change.toneName ?? string.Empty,
                change.toneId.ToString(CultureInfo.InvariantCulture))));
        digest.AddRange((tones?.definitions ?? new List<PsarcCachedToneDefinitionData>())
            .OrderBy(definition => definition?.name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(definition => definition?.key ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(definition => string.Join("|",
                "tone-definition",
                definition?.name ?? string.Empty,
                definition?.key ?? string.Empty,
                definition?.preferredPresetName ?? string.Empty,
                definition?.fallbackSearchText ?? string.Empty,
                HashString(definition?.rawJson ?? string.Empty))));
        return digest;
    }

    private static List<string> BuildTheoryToneDigest(TheoryToneData tones, string packagePath)
    {
        List<string> digest = new List<string> { $"tone|base|{tones?.baseToneName ?? string.Empty}" };
        digest.AddRange((tones?.changes ?? new List<TheoryToneChangeData>())
            .Select((change, index) => string.Join("|",
                "tone-change",
                index.ToString(CultureInfo.InvariantCulture),
                FormatFloat(change.timeSeconds),
                change.toneName ?? string.Empty,
                change.toneId.ToString(CultureInfo.InvariantCulture))));
        digest.AddRange((tones?.definitions ?? new List<TheoryToneDefinitionData>())
            .OrderBy(definition => definition?.name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(definition => definition?.key ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(definition => string.Join("|",
                "tone-definition",
                definition?.name ?? string.Empty,
                definition?.key ?? string.Empty,
                definition?.preferredPresetName ?? definition?.preset?.presetName ?? string.Empty,
                definition?.fallbackSearchText ?? definition?.fallback?.searchText ?? string.Empty,
                HashString(ReadTheoryToneJson(packagePath, definition?.rawToneEntry)))));
        return digest;
    }

    private static string ReadTheoryToneJson(string packagePath, string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
            return string.Empty;

        return TheoryPackageIO.TryReadTextEntry(packagePath, entry, out string text, out _)
            ? text ?? string.Empty
            : string.Empty;
    }

    private static List<string> BuildCachedGeneratedNoteDigest(List<PsarcCachedGeneratedNoteEvent> notes)
    {
        return notes?
            .Select((note, index) => BuildGeneratedNoteDigest(
                index,
                note?.startTimeSeconds ?? 0f,
                note?.durationSeconds ?? 0f,
                note?.pitchPreRollSeconds ?? 0f,
                note?.midiNote ?? 0,
                note?.velocity ?? 0,
                note?.channel ?? 0,
                note?.partId,
                note?.partName,
                note?.techniqueVariant ?? 0,
                note?.legatoTransitionKind ?? 0,
                note?.attackVelocityScale ?? 0f,
                note?.vibratoDepthSemitones ?? 0f,
                note?.vibratoRateHz ?? 0f,
                note?.vibratoDelayNormalized ?? 0f,
                note?.vibratoFadeNormalized ?? 0f,
                note?.pitchBendRangeSemitones ?? 0,
                note?.pitchCurve?.Select(point => new GeneratedPlaybackPitchPoint
                {
                    normalizedTime = point.normalizedTime,
                    semitoneOffset = point.semitoneOffset
                }).ToList()))
            .ToList() ?? new List<string>();
    }

    private static List<string> BuildTheoryGeneratedNoteDigest(List<TheoryGeneratedNoteEvent> notes)
    {
        return notes?
            .Select((note, index) => BuildGeneratedNoteDigest(
                index,
                note?.startTimeSeconds ?? 0f,
                note?.durationSeconds ?? 0f,
                note?.pitchPreRollSeconds ?? 0f,
                note?.midiNote ?? 0,
                note?.velocity ?? 0,
                note?.channel ?? 0,
                note?.partId,
                note?.partName,
                note?.techniqueVariant ?? 0,
                note?.legatoTransitionKind ?? 0,
                note?.attackVelocityScale ?? 0f,
                note?.vibratoDepthSemitones ?? 0f,
                note?.vibratoRateHz ?? 0f,
                note?.vibratoDelayNormalized ?? 0f,
                note?.vibratoFadeNormalized ?? 0f,
                note?.pitchBendRangeSemitones ?? 0,
                note?.pitchCurve?.Select(point => new GeneratedPlaybackPitchPoint
                {
                    normalizedTime = point.normalizedTime,
                    semitoneOffset = point.semitoneOffset
                }).ToList()))
            .ToList() ?? new List<string>();
    }

    private static List<string> BuildGeneratedArrangementDigest(GeneratedPlaybackArrangement arrangement)
    {
        List<string> digest = new List<string>();
        if (arrangement == null)
            return digest;

        digest.Add($"generated|duration|{FormatFloat(arrangement.durationSeconds)}");
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
        digest.AddRange((arrangement.channelAssignments ?? new List<GeneratedPlaybackChannelAssignment>())
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
                channel.pitchBendRangeSemitones.ToString(CultureInfo.InvariantCulture))));
        digest.AddRange((arrangement.notes ?? new List<GeneratedPlaybackNoteEvent>())
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
                note.pitchCurve)));
        return digest;
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

    private static string PitchCurveDigest(List<GeneratedPlaybackPitchPoint> points)
    {
        if (points == null || points.Count == 0)
            return string.Empty;

        return string.Join(";", points.Select(point => string.Join(",",
            FormatFloat(point.normalizedTime),
            FormatFloat(point.semitoneOffset))));
    }

    private static string IntArrayDigest(int[] values)
    {
        return values == null ? string.Empty : string.Join(",", values.Select(value => value.ToString(CultureInfo.InvariantCulture)));
    }

    private static string FormatFloat(float value)
    {
        return Mathf.Abs(value) < 0.00005f
            ? "0"
            : value.ToString("0.#####", CultureInfo.InvariantCulture);
    }

    private static string HashString(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }

    private static class LegacyPsarcCacheRuntimeLoader
    {
        private const int MinimumSupportedSchemaVersion = 10;

        public static bool TryLoadManifest(string manifestPath, out PsarcCachedSongManifest manifest)
        {
            manifest = null;
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                return false;

            try
            {
                PsarcCachedSongManifest loaded = JsonUtility.FromJson<PsarcCachedSongManifest>(File.ReadAllText(manifestPath));
                if (loaded == null || !IsSupportedSchemaVersion(loaded.schemaVersion))
                    return false;

                NormalizeManifest(manifestPath, loaded);
                manifest = loaded;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PsarcParity] Failed to parse legacy cache manifest '{manifestPath}': {ex.Message}");
                return false;
            }
        }

        public static List<MusicXmlLoader.MusicXmlPartSummary> GetPartSummaries(string manifestPath)
        {
            if (!TryLoadManifest(manifestPath, out PsarcCachedSongManifest manifest))
                return new List<MusicXmlLoader.MusicXmlPartSummary>();

            List<MusicXmlLoader.MusicXmlPartSummary> summaries = new List<MusicXmlLoader.MusicXmlPartSummary>();
            for (int i = 0; i < (manifest.arrangements?.Count ?? 0); i++)
            {
                PsarcCachedArrangementSummary arrangement = manifest.arrangements[i];
                if (arrangement == null || string.IsNullOrWhiteSpace(arrangement.partFilePath))
                    continue;

                summaries.Add(new MusicXmlLoader.MusicXmlPartSummary
                {
                    Index = i,
                    PartId = arrangement.partId ?? string.Empty,
                    Name = string.IsNullOrWhiteSpace(arrangement.displayName) ? arrangement.route ?? $"Arrangement {i + 1}" : arrangement.displayName,
                    InstrumentType = InstrumentTypeForRoute(arrangement.route),
                    Route = arrangement.route ?? string.Empty,
                    GroupId = arrangement.arrangementGroupId ?? arrangement.partId ?? string.Empty,
                    GroupDisplayName = string.IsNullOrWhiteSpace(arrangement.arrangementDisplayName)
                        ? (string.IsNullOrWhiteSpace(arrangement.route) ? arrangement.displayName : arrangement.route)
                        : arrangement.arrangementDisplayName,
                    DifficultyLabel = arrangement.difficultyLabel ?? string.Empty,
                    DifficultyUiIndex = arrangement.difficultyUiIndex,
                    HasDifficultyVariants = arrangement.hasDifficultyVariants,
                    NoteCount = Mathf.Max(0, arrangement.noteCount),
                    TabCount = Mathf.Max(0, arrangement.tabCount),
                    Score = arrangement.score,
                    StringTuningPitches = arrangement.tuningPitches != null ? (int[])arrangement.tuningPitches.Clone() : null,
                    TuningDisplayName = string.IsNullOrWhiteSpace(arrangement.tuningDisplayName)
                        ? StringTuningUtils.FormatTuningDisplayName(arrangement.tuningPitches)
                        : arrangement.tuningDisplayName
                });
            }

            return summaries;
        }

        public static List<NoteData> LoadSong(string manifestPath, int targetPartIndex = -1)
        {
            if (!TryLoadArrangementPart(manifestPath, targetPartIndex, out _, out PsarcCachedArrangementPart part))
                return new List<NoteData>();

            List<NoteData> notes = new List<NoteData>(part.notes?.Count ?? 0);
            if (part.notes == null)
                return notes;

            for (int i = 0; i < part.notes.Count; i++)
            {
                PsarcCachedNoteData source = part.notes[i];
                notes.Add(new NoteData(
                    source.id,
                    source.time,
                    source.duration,
                    source.stringIdx,
                    source.fret,
                    source.note,
                    source.chordId,
                    ResolveRuntimePrimaryTechnique(source),
                    source.slideTargetFret,
                    source.bendStep,
                    source.isLegato,
                    source.requiresPluck,
                    source.linkedFromNoteId,
                    source.bendPreBend,
                    source.bendRelease,
                    source.bendVisualStartTime,
                    source.bendVisualDuration,
                    PsarcTechniqueSegmentNormalizer.BuildNormalizedTechniqueSegments(source),
                    source.isMuted,
                    source.chordName));
            }

            NormalizeLegatoTransitions(notes);
            return notes;
        }

        public static List<ArpeggioGuideData> LoadArpeggioGuides(string manifestPath, int targetPartIndex = -1)
        {
            if (!TryLoadArrangementPart(manifestPath, targetPartIndex, out _, out PsarcCachedArrangementPart part) ||
                part.arpeggioGuides == null)
            {
                return new List<ArpeggioGuideData>();
            }

            List<ArpeggioGuideData> guides = new List<ArpeggioGuideData>(part.arpeggioGuides.Count);
            for (int i = 0; i < part.arpeggioGuides.Count; i++)
            {
                PsarcCachedArpeggioGuideData source = part.arpeggioGuides[i];
                if (source == null || source.stringFrets == null || source.stringFrets.Length == 0)
                    continue;

                guides.Add(new ArpeggioGuideData
                {
                    id = source.id,
                    startTime = source.startTime,
                    endTime = source.endTime,
                    chordName = source.chordName,
                    stringFrets = (int[])source.stringFrets.Clone()
                });
            }

            return guides;
        }

        public static bool TryLoadArrangementPart(
            string manifestPath,
            int targetPartIndex,
            out PsarcCachedArrangementSummary summary,
            out PsarcCachedArrangementPart part)
        {
            summary = null;
            part = null;
            if (!TryLoadManifest(manifestPath, out PsarcCachedSongManifest manifest))
                return false;

            int index = ResolveArrangementIndex(manifest, targetPartIndex);
            if (index < 0 || index >= manifest.arrangements.Count)
                return false;

            summary = manifest.arrangements[index];
            return TryLoadPart(summary?.partFilePath, out part);
        }

        public static GeneratedPlaybackArrangement LoadGeneratedArrangement(string manifestPath)
        {
            if (!TryLoadManifest(manifestPath, out PsarcCachedSongManifest manifest))
                return null;

            GeneratedPlaybackArrangement arrangement = new GeneratedPlaybackArrangement
            {
                sourcePath = manifestPath,
                durationSeconds = Mathf.Max(0f, manifest.durationSeconds)
            };

            List<PsarcCachedArrangementSummary> summaries = SelectGeneratedArrangementSummaries(manifest.arrangements);
            int nextChannel = 0;
            for (int i = 0; i < summaries.Count; i++)
            {
                PsarcCachedArrangementSummary summary = summaries[i];
                if (!TryLoadPart(summary?.partFilePath, out PsarcCachedArrangementPart part))
                    continue;

                if (part.generatedPart != null)
                {
                    arrangement.parts.Add(new GeneratedPlaybackPartInfo
                    {
                        partId = part.generatedPart.partId ?? part.partId ?? $"rs_part_{i}",
                        displayName = part.generatedPart.displayName ?? part.displayName,
                        instrumentName = part.generatedPart.instrumentName ?? part.route,
                        sourceMidiChannel = part.generatedPart.sourceMidiChannel,
                        sourceMidiProgram = part.generatedPart.sourceMidiProgram,
                        preferredBank = part.generatedPart.preferredBank,
                        isDrum = part.generatedPart.isDrum,
                        isGuitarFamily = part.generatedPart.isGuitarFamily,
                        isExplicitHarmonicPart = part.generatedPart.isExplicitHarmonicPart
                    });
                }

                arrangement.channelAssignments.Add(new GeneratedPlaybackChannelAssignment
                {
                    channel = Mathf.Clamp(nextChannel, 0, 15),
                    bank = -1,
                    preset = part.generatedPart != null ? part.generatedPart.sourceMidiProgram : 29,
                    isDrum = part.generatedPart != null && part.generatedPart.isDrum,
                    label = part.displayName,
                    sourcePartId = part.partId,
                    sourcePartName = part.displayName,
                    pitchBendRangeSemitones = GetMaxPitchBendRange(part.generatedNotes)
                });

                if (part.generatedNotes != null)
                {
                    for (int noteIndex = 0; noteIndex < part.generatedNotes.Count; noteIndex++)
                    {
                        PsarcCachedGeneratedNoteEvent source = part.generatedNotes[noteIndex];
                        if (source == null)
                            continue;

                        GeneratedPlaybackNoteEvent note = new GeneratedPlaybackNoteEvent
                        {
                            startTimeSeconds = source.startTimeSeconds,
                            durationSeconds = source.durationSeconds,
                            pitchPreRollSeconds = source.pitchPreRollSeconds,
                            midiNote = source.midiNote,
                            velocity = source.velocity,
                            channel = Mathf.Clamp(nextChannel, 0, 15),
                            partId = source.partId ?? part.partId,
                            partName = source.partName ?? part.displayName,
                            techniqueVariant = (GeneratedTechniqueVariant)Mathf.Clamp(source.techniqueVariant, 0, (int)GeneratedTechniqueVariant.Harmonic),
                            legatoTransitionKind = (GeneratedLegatoTransitionKind)Mathf.Clamp(source.legatoTransitionKind, 0, (int)GeneratedLegatoTransitionKind.PullOff),
                            attackVelocityScale = source.attackVelocityScale,
                            vibratoDepthSemitones = source.vibratoDepthSemitones,
                            vibratoRateHz = source.vibratoRateHz,
                            vibratoDelayNormalized = source.vibratoDelayNormalized,
                            vibratoFadeNormalized = source.vibratoFadeNormalized,
                            pitchBendRangeSemitones = source.pitchBendRangeSemitones
                        };

                        if (source.pitchCurve != null)
                        {
                            for (int pointIndex = 0; pointIndex < source.pitchCurve.Count; pointIndex++)
                            {
                                PsarcCachedGeneratedPitchPoint point = source.pitchCurve[pointIndex];
                                if (point == null)
                                    continue;

                                note.pitchCurve.Add(new GeneratedPlaybackPitchPoint
                                {
                                    normalizedTime = point.normalizedTime,
                                    semitoneOffset = point.semitoneOffset
                                });
                            }
                        }

                        arrangement.notes.Add(note);
                    }
                }

                nextChannel++;
                if (nextChannel == 9)
                    nextChannel++;
            }

            if (arrangement.durationSeconds <= 0.01f && arrangement.notes.Count > 0)
                arrangement.durationSeconds = arrangement.notes.Max(note => note.EndTimeSeconds);

            return arrangement;
        }

        private static bool TryLoadPart(string partFilePath, out PsarcCachedArrangementPart part)
        {
            part = null;
            if (string.IsNullOrWhiteSpace(partFilePath) || !File.Exists(partFilePath))
                return false;

            try
            {
                PsarcCachedArrangementPart loaded = JsonUtility.FromJson<PsarcCachedArrangementPart>(File.ReadAllText(partFilePath));
                if (loaded == null || !IsSupportedSchemaVersion(loaded.schemaVersion))
                    return false;

                NormalizePart(loaded);
                part = loaded;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PsarcParity] Failed to parse legacy cache part '{partFilePath}': {ex.Message}");
                return false;
            }
        }

        private static void NormalizeManifest(string manifestPath, PsarcCachedSongManifest manifest)
        {
            manifest.arrangements ??= new List<PsarcCachedArrangementSummary>();
            string manifestDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;

            manifest.audioPath = ResolveStoredPath(manifestDirectory, manifest.audioPath);
            manifest.previewAudioPath = ResolveStoredPath(manifestDirectory, manifest.previewAudioPath);
            manifest.artworkPath = ResolveStoredPath(manifestDirectory, manifest.artworkPath);
            manifest.durationSeconds = Mathf.Max(0f, manifest.durationSeconds);
            manifest.difficultyRating = Mathf.Clamp(manifest.difficultyRating, 0, 5);

            for (int i = 0; i < manifest.arrangements.Count; i++)
            {
                PsarcCachedArrangementSummary arrangement = manifest.arrangements[i];
                if (arrangement == null)
                    continue;

                arrangement.partFilePath = ResolveStoredPath(manifestDirectory, arrangement.partFilePath);
                arrangement.arrangementGroupId = string.IsNullOrWhiteSpace(arrangement.arrangementGroupId)
                    ? arrangement.partId ?? string.Empty
                    : arrangement.arrangementGroupId;
                arrangement.arrangementDisplayName = string.IsNullOrWhiteSpace(arrangement.arrangementDisplayName)
                    ? (!string.IsNullOrWhiteSpace(arrangement.route) ? arrangement.route : arrangement.displayName)
                    : arrangement.arrangementDisplayName;
                arrangement.difficultyLabel = NormalizeDifficultyLabel(arrangement.difficultyLabel, arrangement.difficultyUiIndex);
                arrangement.difficultyUiIndex = NormalizeDifficultyUiIndex(arrangement.difficultyUiIndex, arrangement.difficultyLabel);
                arrangement.tuningDisplayName = string.IsNullOrWhiteSpace(arrangement.tuningDisplayName)
                    ? StringTuningUtils.FormatTuningDisplayName(arrangement.tuningPitches)
                    : arrangement.tuningDisplayName;
                arrangement.difficultyRating = Mathf.Clamp(arrangement.difficultyRating, 0, 5);
            }

            manifest.arrangements = manifest.arrangements
                .OrderByDescending(arrangement => GetDefaultRoutePriority(arrangement?.route))
                .ThenBy(arrangement => arrangement?.arrangementDisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(arrangement => arrangement?.difficultyUiIndex ?? int.MaxValue)
                .ToList();
        }

        private static void NormalizePart(PsarcCachedArrangementPart part)
        {
            part.timing ??= new PsarcCachedArrangementTimingData();
            part.tones ??= new PsarcCachedArrangementToneData();
            part.generatedPart ??= new PsarcCachedGeneratedPartInfo();
            part.notes ??= new List<PsarcCachedNoteData>();
            part.arpeggioGuides ??= new List<PsarcCachedArpeggioGuideData>();
            part.generatedNotes ??= new List<PsarcCachedGeneratedNoteEvent>();
            part.durationSeconds = Mathf.Max(0f, part.durationSeconds);
            part.difficultyRating = Mathf.Clamp(part.difficultyRating, 0, 5);
            part.difficultyLabel = NormalizeDifficultyLabel(part.difficultyLabel, part.difficultyUiIndex);
            part.difficultyUiIndex = NormalizeDifficultyUiIndex(part.difficultyUiIndex, part.difficultyLabel);
            part.tuningDisplayName = string.IsNullOrWhiteSpace(part.tuningDisplayName)
                ? StringTuningUtils.FormatTuningDisplayName(part.tuningPitches)
                : part.tuningDisplayName;
            part.timing.ebeats ??= new List<PsarcCachedEbeatData>();
            part.timing.sections ??= new List<PsarcCachedSectionData>();
            part.tones.changes ??= new List<PsarcCachedToneChangeData>();
            part.tones.definitions ??= new List<PsarcCachedToneDefinitionData>();
        }

        private static int ResolveArrangementIndex(PsarcCachedSongManifest manifest, int targetPartIndex)
        {
            if (manifest?.arrangements == null || manifest.arrangements.Count == 0)
                return -1;
            if (targetPartIndex >= 0 && targetPartIndex < manifest.arrangements.Count)
                return targetPartIndex;

            int bestIndex = 0;
            int bestScore = int.MinValue;
            for (int i = 0; i < manifest.arrangements.Count; i++)
            {
                PsarcCachedArrangementSummary arrangement = manifest.arrangements[i];
                int score = arrangement != null
                    ? GetDefaultRoutePriority(arrangement.route) + arrangement.score
                    : int.MinValue;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static List<PsarcCachedArrangementSummary> SelectGeneratedArrangementSummaries(List<PsarcCachedArrangementSummary> arrangements)
        {
            if (arrangements == null || arrangements.Count == 0)
                return new List<PsarcCachedArrangementSummary>();

            return arrangements
                .GroupBy(arrangement => string.IsNullOrWhiteSpace(arrangement?.arrangementGroupId) ? arrangement?.partId ?? string.Empty : arrangement.arrangementGroupId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderBy(arrangement => arrangement?.difficultyUiIndex ?? int.MaxValue)
                    .ThenByDescending(arrangement => arrangement?.score ?? 0)
                    .FirstOrDefault())
                .Where(arrangement => arrangement != null)
                .ToList();
        }

        private static NoteTechnique ResolveRuntimePrimaryTechnique(PsarcCachedNoteData source)
        {
            if (source == null)
                return NoteTechnique.None;
            if (source.isHammerOn)
                return NoteTechnique.HammerOn;
            if (source.isPullOff)
                return NoteTechnique.PullOff;
            return (NoteTechnique)Mathf.Clamp(source.technique, (int)NoteTechnique.None, (int)NoteTechnique.Vibrato);
        }

        private static void NormalizeLegatoTransitions(List<NoteData> notes)
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

                float transitionDuration = Mathf.Max(0.05f, destination.time - origin.time);
                if (destination.technique == NoteTechnique.Slide)
                {
                    if (origin.fret != destination.fret)
                    {
                        NoteTechnique inferredLegato = destination.fret >= origin.fret ? NoteTechnique.HammerOn : NoteTechnique.PullOff;
                        if (origin.technique == NoteTechnique.None)
                            origin.technique = inferredLegato;
                        if (origin.slideTargetFret < 0)
                            origin.slideTargetFret = destination.fret;
                        origin.duration = Mathf.Max(origin.duration, transitionDuration);
                        notes[originIndex] = origin;
                    }
                    else
                    {
                        destination.isLegato = false;
                        destination.requiresPluck = true;
                        destination.linkedFromNoteId = -1;
                        notes[i] = destination;
                    }

                    continue;
                }

                NoteTechnique inferredTechnique = destination.technique;
                if (inferredTechnique == NoteTechnique.None && origin.fret != destination.fret)
                    inferredTechnique = destination.fret >= origin.fret ? NoteTechnique.HammerOn : NoteTechnique.PullOff;

                if (inferredTechnique == NoteTechnique.HammerOn || inferredTechnique == NoteTechnique.PullOff)
                {
                    if (origin.technique == NoteTechnique.None)
                        origin.technique = inferredTechnique;
                    if (origin.slideTargetFret < 0)
                        origin.slideTargetFret = destination.fret;
                    origin.duration = Mathf.Max(origin.duration, transitionDuration);
                    destination.technique = NoteTechnique.None;
                    notes[originIndex] = origin;
                    notes[i] = destination;
                }
            }
        }

        private static bool IsSupportedSchemaVersion(int schemaVersion)
        {
            return schemaVersion >= MinimumSupportedSchemaVersion &&
                   schemaVersion <= PsarcCachedSongFormat.SchemaVersion;
        }

        private static string ResolveStoredPath(string baseDirectory, string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
                return storedPath;
            if (Path.IsPathRooted(storedPath))
                return storedPath;

            string resolved = Path.GetFullPath(Path.Combine(baseDirectory ?? string.Empty, storedPath));
            if (File.Exists(resolved) || Directory.Exists(resolved))
                return resolved;

            string remapped = TryRemapLegacyStoredPath(baseDirectory, storedPath);
            return string.IsNullOrWhiteSpace(remapped) ? resolved : remapped;
        }

        private static string TryRemapLegacyStoredPath(string baseDirectory, string storedPath)
        {
            string normalized = storedPath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            string legacyPrefix = PsarcCachedSongFormat.LegacyContentDirectoryName + Path.DirectorySeparatorChar;
            string currentPrefix = PsarcCachedSongFormat.ContentDirectoryName + Path.DirectorySeparatorChar;

            if (normalized.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string remapped = PsarcCachedSongFormat.ContentDirectoryName + normalized.Substring(PsarcCachedSongFormat.LegacyContentDirectoryName.Length);
                return Path.GetFullPath(Path.Combine(baseDirectory ?? string.Empty, remapped));
            }

            if (normalized.StartsWith(currentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string remapped = PsarcCachedSongFormat.LegacyContentDirectoryName + normalized.Substring(PsarcCachedSongFormat.ContentDirectoryName.Length);
                return Path.GetFullPath(Path.Combine(baseDirectory ?? string.Empty, remapped));
            }

            return null;
        }

        private static string NormalizeDifficultyLabel(string difficultyLabel, int difficultyUiIndex)
        {
            if (!string.IsNullOrWhiteSpace(difficultyLabel))
                return difficultyLabel.Trim();
            if (difficultyUiIndex == 0)
                return "Full";
            if (difficultyUiIndex > 0)
                return difficultyUiIndex.ToString(CultureInfo.InvariantCulture);
            return string.Empty;
        }

        private static int NormalizeDifficultyUiIndex(int difficultyUiIndex, string difficultyLabel)
        {
            if (difficultyUiIndex >= 0)
                return difficultyUiIndex;
            if (string.IsNullOrWhiteSpace(difficultyLabel))
                return -1;

            string normalized = difficultyLabel.Trim();
            if (string.Equals(normalized, "Full", StringComparison.OrdinalIgnoreCase))
                return 0;
            return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? Math.Max(0, parsed)
                : -1;
        }

        private static int GetDefaultRoutePriority(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
                return 0;
            if (route.IndexOf("lead", StringComparison.OrdinalIgnoreCase) >= 0)
                return 4000;
            if (route.IndexOf("combo", StringComparison.OrdinalIgnoreCase) >= 0)
                return 3000;
            if (route.IndexOf("rhythm", StringComparison.OrdinalIgnoreCase) >= 0)
                return 2000;
            if (route.IndexOf("bass", StringComparison.OrdinalIgnoreCase) >= 0)
                return 1000;
            return 0;
        }

        private static string InstrumentTypeForRoute(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
                return string.Empty;
            if (route.IndexOf("bass", StringComparison.OrdinalIgnoreCase) >= 0)
                return "bass";
            if (route.IndexOf("drum", StringComparison.OrdinalIgnoreCase) >= 0)
                return "drums";
            if (route.IndexOf("vocal", StringComparison.OrdinalIgnoreCase) >= 0)
                return "vocals";
            if (route.IndexOf("piano", StringComparison.OrdinalIgnoreCase) >= 0 ||
                route.IndexOf("keys", StringComparison.OrdinalIgnoreCase) >= 0 ||
                route.IndexOf("keyboard", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "piano";
            }

            return "guitar";
        }

        private static int GetMaxPitchBendRange(List<PsarcCachedGeneratedNoteEvent> notes)
        {
            if (notes == null || notes.Count == 0)
                return 0;

            int range = 0;
            for (int i = 0; i < notes.Count; i++)
                range = Mathf.Max(range, notes[i]?.pitchBendRangeSemitones ?? 0);
            return range;
        }
    }
}
                                                                                                                                                                                                                                                                                            