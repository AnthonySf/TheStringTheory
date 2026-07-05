using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using AlphaTab;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Exporter;
using AlphaTab.Importer;
using AlphaTab.Model;
using UnityEngine;

public static class AlphaTabRenderClient
{
    public static Task<AlphaTabRenderManifestData> RenderAsync(AlphaTabRenderRequestData request)
    {
        PreparedRenderRequest prepared = PrepareRenderRequest(request);
        return Task.Run(() => RenderBlocking(prepared));
    }

    private static PreparedRenderRequest PrepareRenderRequest(AlphaTabRenderRequestData request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        AlphaTabResolvedSourceData resolvedSource = AlphaTabSourceResolver.Resolve(request.notationPath, request.trackIndex);
        string helperPath = ExternalContentPaths.StreamingAlphaTabRenderHelperExePath;
        if (!File.Exists(helperPath))
            throw new FileNotFoundException("AlphaTab render helper executable was not found.", helperPath);
        StringTheoryPlatform.TryEnsureExecutable(helperPath);
        long helperLastWriteTicks = File.GetLastWriteTimeUtc(helperPath).Ticks;

        AlphaTabRenderRequestData normalizedRequest = new AlphaTabRenderRequestData
        {
            notationPath = resolvedSource.resolvedNotationPath,
            trackIndex = resolvedSource.resolvedTrackIndex,
            themeId = string.IsNullOrWhiteSpace(request.themeId) ? "white_on_dark_blue" : request.themeId,
            renderWidth = request.renderWidth,
            scale = request.scale,
            barsPerRow = request.barsPerRow,
            barsPerSection = request.barsPerSection,
            outputDirectory = request.outputDirectory
        };

        string requestJson = JsonUtility.ToJson(normalizedRequest, true);
        string requestHash = ComputeRequestHash(requestJson, resolvedSource, helperLastWriteTicks);
        string requestDirectory = Path.Combine(request.outputDirectory, requestHash);
        Directory.CreateDirectory(requestDirectory);

        normalizedRequest.outputDirectory = requestDirectory;
        requestJson = JsonUtility.ToJson(normalizedRequest, true);

        return new PreparedRenderRequest
        {
            helperPath = helperPath,
            helperLastWriteTicks = helperLastWriteTicks,
            resolvedSource = resolvedSource,
            requestDirectory = requestDirectory,
            requestJson = requestJson,
            requestPath = Path.Combine(requestDirectory, "request.json"),
            responsePath = Path.Combine(requestDirectory, "response.json")
        };
    }

    private static AlphaTabRenderManifestData RenderBlocking(PreparedRenderRequest prepared)
    {
        Directory.CreateDirectory(prepared.requestDirectory);
        File.WriteAllText(prepared.requestPath, prepared.requestJson, Encoding.UTF8);
        UnityEngine.Debug.Log($"[AlphaTab] Launching helper. request='{prepared.requestPath}', response='{prepared.responsePath}', source='{prepared.resolvedSource.resolvedNotationPath}'");

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = prepared.helperPath,
            Arguments = $"render --request \"{prepared.requestPath}\" --response \"{prepared.responsePath}\"",
            WorkingDirectory = Path.GetDirectoryName(prepared.helperPath) ?? System.Environment.CurrentDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to launch AlphaTab helper: {prepared.helperPath}");
        if (!process.WaitForExit(120000))
        {
            TryKillProcess(process);
            throw new TimeoutException("AlphaTab render helper timed out after 120 seconds.");
        }

        string stderr = process.StandardError.ReadToEnd();
        if (!string.IsNullOrWhiteSpace(stderr))
            UnityEngine.Debug.LogWarning($"[AlphaTab] Helper stderr: {stderr}");
        if (process.ExitCode != 0 && !File.Exists(prepared.responsePath))
            throw new InvalidOperationException($"AlphaTab render helper exited with code {process.ExitCode}: {stderr}");

        AlphaTabRenderResponseData response = LoadJson<AlphaTabRenderResponseData>(prepared.responsePath);
        if (!response.success)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.error) ? "AlphaTab render helper failed." : response.error);
        if (string.IsNullOrWhiteSpace(response.manifestPath) || !File.Exists(response.manifestPath))
            throw new FileNotFoundException("AlphaTab render helper did not produce a manifest.", response.manifestPath);

        AlphaTabRenderManifestData manifest = LoadJson<AlphaTabRenderManifestData>(response.manifestPath);
        manifest.notationPath = prepared.resolvedSource.logicalNotationPath;
        manifest.trackIndex = prepared.resolvedSource.logicalTrackIndex;
        UnityEngine.Debug.Log($"[AlphaTab] Helper completed. manifest='{response.manifestPath}', sections={manifest.sections?.Count ?? 0}");
        return manifest;
    }

    private static T LoadJson<T>(string path)
    {
        string json = File.ReadAllText(path, Encoding.UTF8);
        T value = JsonUtility.FromJson<T>(json);
        if (value == null)
            throw new InvalidOperationException($"Failed to deserialize '{path}' as {typeof(T).Name}.");
        return value;
    }

    private static string ComputeRequestHash(string requestJson, AlphaTabResolvedSourceData resolvedSource, long helperLastWriteTicks)
    {
        long ticks = File.Exists(resolvedSource.resolvedNotationPath) ? File.GetLastWriteTimeUtc(resolvedSource.resolvedNotationPath).Ticks : 0L;
        string input = string.Join("|", new[]
        {
            ticks.ToString(CultureInfo.InvariantCulture),
            helperLastWriteTicks.ToString(CultureInfo.InvariantCulture),
            resolvedSource.logicalNotationPath ?? string.Empty,
            resolvedSource.logicalTrackIndex.ToString(CultureInfo.InvariantCulture),
            requestJson
        });
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes;
        using (SHA256 sha256 = SHA256.Create())
            hashBytes = sha256.ComputeHash(inputBytes);
        StringBuilder builder = new StringBuilder(hashBytes.Length * 2);
        for (int i = 0; i < hashBytes.Length; i++)
            builder.Append(hashBytes[i].ToString("x2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill();
        }
        catch
        {
        }
    }

    private sealed class PreparedRenderRequest
    {
        public string helperPath = string.Empty;
        public long helperLastWriteTicks;
        public AlphaTabResolvedSourceData resolvedSource;
        public string requestDirectory = string.Empty;
        public string requestJson = string.Empty;
        public string requestPath = string.Empty;
        public string responsePath = string.Empty;
    }
}

public sealed class AlphaTabResolvedSourceData
{
    public string logicalNotationPath = string.Empty;
    public int logicalTrackIndex;
    public string resolvedNotationPath = string.Empty;
    public int resolvedTrackIndex;
}

public static class AlphaTabSourceResolver
{
    public static AlphaTabResolvedSourceData Resolve(string notationPath, int trackIndex)
    {
        if (string.IsNullOrWhiteSpace(notationPath))
            throw new ArgumentException("Notation path was empty.", nameof(notationPath));

        string normalizedPath = Path.GetFullPath(notationPath);
        AlphaTabResolvedSourceData resolved = new AlphaTabResolvedSourceData
        {
            logicalNotationPath = normalizedPath,
            logicalTrackIndex = Math.Max(0, trackIndex),
            resolvedNotationPath = normalizedPath,
            resolvedTrackIndex = Math.Max(0, trackIndex)
        };

        if (TheoryPackageFormat.IsPackagePath(normalizedPath))
        {
            resolved.resolvedNotationPath = TheoryAlphaTabGpSourceBuilder.GetOrCreate(normalizedPath, resolved.logicalTrackIndex);
            resolved.resolvedTrackIndex = 0;
        }

        return resolved;
    }
}

public static class TheoryAlphaTabGpSourceBuilder
{
    private const string CacheFolderName = "TheorySources";
    private const string ExportVersion = "alphatex_v1";

    public static string GetOrCreate(string packagePath, int arrangementIndex)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("Theory package path was empty.", nameof(packagePath));

        string normalizedPackagePath = Path.GetFullPath(packagePath);
        if (!TheoryPackageIO.TryReadManifest(normalizedPackagePath, out TheorySongManifest manifest, out string manifestError) || manifest == null)
            throw new InvalidOperationException($"Failed to load theory package '{normalizedPackagePath}': {manifestError}");

        int resolvedIndex = ResolveArrangementIndex(manifest, arrangementIndex);
        if (resolvedIndex < 0 || resolvedIndex >= manifest.arrangements.Count)
            throw new InvalidOperationException($"Failed to resolve theory arrangement {arrangementIndex} from '{normalizedPackagePath}'.");

        TheoryArrangementSummary theorySummary = manifest.arrangements[resolvedIndex];
        if (!TheoryPackageIO.TryReadArrangement(normalizedPackagePath, theorySummary, out TheoryArrangementData theoryArrangement, out string arrangementError) ||
            theoryArrangement == null)
        {
            throw new InvalidOperationException($"Failed to load theory arrangement {resolvedIndex} from '{normalizedPackagePath}': {arrangementError}");
        }

        PsarcCachedSongManifest convertedManifest = ToCachedManifest(manifest, normalizedPackagePath);
        PsarcCachedArrangementSummary convertedSummary = ToCachedSummary(theorySummary, resolvedIndex);
        PsarcCachedArrangementPart convertedPart = ToCachedPart(theoryArrangement, convertedSummary, manifest.durationSeconds);

        string cacheDirectory = Path.Combine(
            ExternalContentPaths.PersistentAlphaTabRenderCacheDirectory,
            CacheFolderName,
            ComputeCacheKey(normalizedPackagePath, theorySummary?.arrangementId, theorySummary?.entry));
        Directory.CreateDirectory(cacheDirectory);

        string fileBaseName = TheoryPackageFormat.SanitizeEntryFileName(theorySummary?.arrangementId, $"arrangement_{resolvedIndex.ToString(CultureInfo.InvariantCulture)}");
        string outputGpPath = Path.Combine(cacheDirectory, $"{fileBaseName}.alphatex");
        long sourceTicks = File.GetLastWriteTimeUtc(normalizedPackagePath).Ticks;
        long outputTicks = File.Exists(outputGpPath) ? File.GetLastWriteTimeUtc(outputGpPath).Ticks : 0L;
        if (outputTicks >= sourceTicks && File.Exists(outputGpPath))
            return outputGpPath;

        PsarcAlphaTabGpWriter.Write(outputGpPath, convertedManifest, convertedSummary, convertedPart);
        return outputGpPath;
    }

    private static int ResolveArrangementIndex(TheorySongManifest manifest, int requestedIndex)
    {
        if (manifest?.arrangements == null || manifest.arrangements.Count == 0)
            return -1;

        if (requestedIndex >= 0 && requestedIndex < manifest.arrangements.Count)
            return requestedIndex;

        if (!string.IsNullOrWhiteSpace(manifest.defaultArrangementId))
        {
            for (int i = 0; i < manifest.arrangements.Count; i++)
            {
                if (string.Equals(manifest.arrangements[i]?.arrangementId ?? string.Empty, manifest.defaultArrangementId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        return 0;
    }

    private static PsarcCachedSongManifest ToCachedManifest(TheorySongManifest source, string packagePath)
    {
        FileInfo info = new FileInfo(packagePath);
        return new PsarcCachedSongManifest
        {
            schemaVersion = PsarcCachedSongFormat.SchemaVersion,
            sourcePsarcPath = string.Empty,
            sourcePsarcLastWriteUtcTicks = info.LastWriteTimeUtc.Ticks,
            importedAtUtcTicks = ResolveManifestTimestamp(source),
            displayName = source.title ?? string.Empty,
            artist = source.artist ?? string.Empty,
            album = source.album ?? string.Empty,
            subtitle = source.subtitle ?? string.Empty,
            durationSeconds = Mathf.Max(0f, source.durationSeconds),
            difficultyRating = Mathf.Clamp(source.difficultyRating, 0, 5),
            arrangements = new List<PsarcCachedArrangementSummary>()
        };
    }

    private static long ResolveManifestTimestamp(TheorySongManifest source)
    {
        if (source == null)
            return 0L;
        return source.modifiedAtUtcTicks > 0 ? source.modifiedAtUtcTicks : source.createdAtUtcTicks;
    }

    private static PsarcCachedArrangementSummary ToCachedSummary(TheoryArrangementSummary source, int index)
    {
        string partId = string.IsNullOrWhiteSpace(source?.arrangementId)
            ? $"arrangement_{index.ToString(CultureInfo.InvariantCulture)}"
            : source.arrangementId;
        return new PsarcCachedArrangementSummary
        {
            partId = partId,
            displayName = source?.displayName ?? partId,
            route = source?.route ?? string.Empty,
            arrangementGroupId = string.IsNullOrWhiteSpace(source?.groupId) ? partId : source.groupId,
            arrangementDisplayName = string.IsNullOrWhiteSpace(source?.groupDisplayName) ? source?.displayName ?? partId : source.groupDisplayName,
            difficultyLabel = source?.difficultyLabel ?? string.Empty,
            difficultyUiIndex = source?.difficultyUiIndex ?? -1,
            hasDifficultyVariants = source?.hasDifficultyVariants ?? false,
            partFilePath = source?.entry ?? string.Empty,
            noteCount = Mathf.Max(0, source?.noteCount ?? 0),
            tabCount = Mathf.Max(0, source?.tabCount ?? 0),
            score = Mathf.Max(0, source?.score ?? 0),
            difficultyRating = Mathf.Clamp(source?.difficultyRating ?? 0, 0, 5),
            tuningPitches = source?.tuningPitches != null ? (int[])source.tuningPitches.Clone() : null,
            tuningDisplayName = source?.tuningDisplayName ?? string.Empty
        };
    }

    private static PsarcCachedArrangementPart ToCachedPart(
        TheoryArrangementData source,
        PsarcCachedArrangementSummary summary,
        float manifestDuration)
    {
        PsarcCachedArrangementPart part = new PsarcCachedArrangementPart
        {
            schemaVersion = PsarcCachedSongFormat.SchemaVersion,
            partId = source.arrangementId ?? summary.partId,
            displayName = source.displayName ?? summary.displayName,
            route = source.route ?? summary.route,
            arrangementGroupId = string.IsNullOrWhiteSpace(source.groupId) ? summary.arrangementGroupId : source.groupId,
            arrangementDisplayName = string.IsNullOrWhiteSpace(source.groupDisplayName) ? summary.arrangementDisplayName : source.groupDisplayName,
            difficultyLabel = source.difficultyLabel ?? summary.difficultyLabel,
            difficultyUiIndex = source.difficultyUiIndex,
            hasDifficultyVariants = source.hasDifficultyVariants,
            durationSeconds = Mathf.Max(0f, source.durationSeconds, manifestDuration),
            difficultyRating = Mathf.Clamp(source.difficultyRating, 0, 5),
            tuningPitches = source.tuningPitches != null ? (int[])source.tuningPitches.Clone() : null,
            tuningDisplayName = source.tuningDisplayName ?? string.Empty,
            timing = ToCachedTiming(source.timing),
            generatedPart = ToCachedGeneratedPart(source.generatedPart, source),
            notes = ToCachedNotes(source.notes),
            arpeggioGuides = ToCachedArpeggioGuides(source.arpeggioGuides),
            generatedNotes = new List<PsarcCachedGeneratedNoteEvent>()
        };

        return part;
    }

    private static PsarcCachedArrangementTimingData ToCachedTiming(TheoryTimingData source)
    {
        return new PsarcCachedArrangementTimingData
        {
            averageTempoBpm = Mathf.Max(1f, source?.averageTempoBpm ?? 120f),
            capo = Mathf.Max(0, source?.capo ?? 0),
            ebeats = source?.beats?
                .Where(beat => beat != null && beat.timeSeconds >= 0f)
                .OrderBy(beat => beat.timeSeconds)
                .Select(beat => new PsarcCachedEbeatData
                {
                    timeSeconds = Mathf.Max(0f, beat.timeSeconds),
                    measure = beat.measure
                })
                .ToList() ?? new List<PsarcCachedEbeatData>(),
            sections = source?.sections?
                .Where(section => section != null && section.timeSeconds >= 0f)
                .OrderBy(section => section.timeSeconds)
                .Select(section => new PsarcCachedSectionData
                {
                    name = section.name ?? string.Empty,
                    number = section.number,
                    timeSeconds = Mathf.Max(0f, section.timeSeconds)
                })
                .ToList() ?? new List<PsarcCachedSectionData>()
        };
    }

    private static PsarcCachedGeneratedPartInfo ToCachedGeneratedPart(TheoryGeneratedPartInfo source, TheoryArrangementData arrangement)
    {
        if (source == null)
            return null;

        return new PsarcCachedGeneratedPartInfo
        {
            partId = source.partId ?? arrangement?.arrangementId ?? string.Empty,
            displayName = source.displayName ?? arrangement?.displayName ?? string.Empty,
            instrumentName = source.instrumentName ?? arrangement?.route ?? string.Empty,
            sourceMidiChannel = source.sourceMidiChannel,
            sourceMidiProgram = source.sourceMidiProgram,
            preferredBank = source.preferredBank,
            isDrum = source.isDrum,
            isGuitarFamily = source.isGuitarFamily,
            isExplicitHarmonicPart = source.isExplicitHarmonicPart
        };
    }

    private static List<PsarcCachedNoteData> ToCachedNotes(List<TheoryNoteData> source)
    {
        return source?
            .Where(note => note != null)
            .OrderBy(note => note.time)
            .ThenBy(note => note.stringIndex)
            .Select(ToCachedNote)
            .ToList() ?? new List<PsarcCachedNoteData>();
    }

    private static PsarcCachedNoteData ToCachedNote(TheoryNoteData source)
    {
        return new PsarcCachedNoteData
        {
            id = source.id,
            time = Mathf.Max(0f, source.time),
            duration = Mathf.Max(0f, source.duration),
            stringIdx = Mathf.Max(0, source.stringIndex),
            fret = Mathf.Max(0, source.fret),
            note = source.noteName ?? string.Empty,
            chordId = source.chordId,
            chordName = source.chordName ?? string.Empty,
            technique = Mathf.Clamp(source.primaryTechnique, (int)NoteTechnique.None, (int)NoteTechnique.Vibrato),
            slideTargetFret = source.slideTargetFret,
            bendStep = source.bendStep,
            bendVisualStartTime = source.bendVisualStartTime,
            bendVisualDuration = source.bendVisualDuration,
            bendPreBend = source.bendPreBend,
            bendRelease = source.bendRelease,
            isMuted = source.muted,
            isPalmMute = source.palmMute,
            isFretHandMute = source.fretHandMute,
            isHarmonic = source.harmonic,
            isAccent = source.accent,
            isTap = source.tap,
            isTremolo = source.tremolo,
            isPinchHarmonic = source.pinchHarmonic,
            isHammerOn = source.hammerOn,
            isPullOff = source.pullOff,
            isHopo = source.hopo || source.hammerOn || source.pullOff,
            hasVibrato = source.vibrato,
            vibratoStrength = source.vibratoStrength,
            maxBend = Mathf.Max(source.maxBend, source.bendStep),
            isLegato = source.legato || source.hammerOn || source.pullOff,
            requiresPluck = source.requiresPluck,
            linkedFromNoteId = source.linkedFromNoteId,
            bendPoints = source.bendPoints?
                .Where(point => point != null)
                .Select(point => new PsarcCachedBendPointData
                {
                    timeSeconds = point.timeSeconds,
                    step = point.step
                })
                .ToList() ?? new List<PsarcCachedBendPointData>(),
            techniqueSegments = source.techniqueSegments?
                .Where(segment => segment != null)
                .Select(segment => new PsarcCachedTechniqueSegmentData
                {
                    type = segment.type,
                    startOffset = segment.startOffset,
                    endOffset = segment.endOffset,
                    startFret = segment.startFret,
                    endFret = segment.endFret,
                    startBend = segment.startBend,
                    endBend = segment.endBend
                })
                .ToList() ?? new List<PsarcCachedTechniqueSegmentData>()
        };
    }

    private static List<PsarcCachedArpeggioGuideData> ToCachedArpeggioGuides(List<TheoryArpeggioGuideData> source)
    {
        return source?
            .Where(guide => guide != null)
            .Select(guide => new PsarcCachedArpeggioGuideData
            {
                id = guide.id,
                startTime = Mathf.Max(0f, guide.startTime),
                endTime = Mathf.Max(guide.startTime, guide.endTime),
                chordName = guide.chordName ?? string.Empty,
                stringFrets = guide.stringFrets != null ? (int[])guide.stringFrets.Clone() : null
            })
            .ToList() ?? new List<PsarcCachedArpeggioGuideData>();
    }

    private static string ComputeCacheKey(string packagePath, string arrangementId, string arrangementEntry)
    {
        FileInfo info = new FileInfo(packagePath);
        string input = string.Join("|", new[]
        {
            ExportVersion,
            info.FullName,
            info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture),
            info.Length.ToString(CultureInfo.InvariantCulture),
            arrangementId ?? string.Empty,
            arrangementEntry ?? string.Empty
        });

        using SHA256 sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        StringBuilder builder = new StringBuilder(hashBytes.Length * 2);
        for (int i = 0; i < hashBytes.Length; i++)
            builder.Append(hashBytes[i].ToString("x2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}


public static class PsarcMusicXmlWriter
{
    private const int DefaultSlotsPerBeat = 8;
    private static readonly int[] SupportedSlotsPerBeat = { 8, 16, 32 };

    private static readonly string[] SharpPitchNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public static void Write(
        string outputPath,
        PsarcCachedSongManifest manifest,
        PsarcCachedArrangementSummary summary,
        PsarcCachedArrangementPart part)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path was empty.", nameof(outputPath));
        if (summary == null)
            throw new ArgumentNullException(nameof(summary));
        if (part == null)
            throw new ArgumentNullException(nameof(part));

        PsarcAlphaTabTimingSidecar timingSidecar;
        XDocument document = BuildDocument(manifest, summary, part, out timingSidecar);
        string directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        document.Save(outputPath);
        timingSidecar.notationPath = outputPath;
        File.WriteAllText(GetTimingSidecarPath(outputPath), JsonUtility.ToJson(timingSidecar, true));
    }

    private static XDocument BuildDocument(
        PsarcCachedSongManifest manifest,
        PsarcCachedArrangementSummary summary,
        PsarcCachedArrangementPart part,
        out PsarcAlphaTabTimingSidecar timingSidecar)
    {
        timingSidecar = new PsarcAlphaTabTimingSidecar();
        int[] tuningPitches = ResolveTuningPitches(part, summary);
        string trackName = !string.IsNullOrWhiteSpace(summary.displayName)
            ? summary.displayName
            : (!string.IsNullOrWhiteSpace(part.displayName) ? part.displayName : "Track");
        Dictionary<int, float> effectiveEndTimes = BuildEffectiveEndTimes(part);
        List<MeasureInfo> measures = BuildMeasures(part, effectiveEndTimes);
        List<EventInfo> events = BuildEvents(part.notes, effectiveEndTimes);
        NoteRenderContext noteRenderContext = BuildNoteRenderContext(part?.notes);
        List<List<EventSliceInfo>> measureSlices = BuildEventSlices(events, measures);

        XElement scorePart = new XElement("score-part",
            new XAttribute("id", "P1"),
            new XElement("part-name", trackName),
            new XElement("part-abbreviation", summary.route ?? string.Empty));

        XElement partElement = new XElement("part", new XAttribute("id", "P1"));
        float previousTempo = -1f;
        for (int measureIndex = 0; measureIndex < measures.Count; measureIndex++)
        {
            MeasureInfo measure = measures[measureIndex];
            XElement measureElement = new XElement("measure", new XAttribute("number", measureIndex + 1));
            bool writeAttributes = measureIndex == 0;

            if (writeAttributes)
            {
                measureElement.Add(BuildAttributesElement(measure, tuningPitches, part?.timing?.capo ?? 0));
            }
            else if (measureIndex > 0)
            {
                MeasureInfo previousMeasure = measures[measureIndex - 1];
                XElement changedAttributes = BuildChangedAttributesElement(previousMeasure, measure);
                if (changedAttributes != null)
                    measureElement.Add(changedAttributes);
            }

            if (measure.tempoBpm > 0.01f && (measureIndex == 0 || Math.Abs(previousTempo - measure.tempoBpm) > 0.25f))
            {
                measureElement.Add(BuildTempoElement(measure.tempoBpm));
                previousTempo = measure.tempoBpm;
            }

            Dictionary<int, List<EventSliceInfo>> slicesByVoice = measureSlices[measureIndex]
                .GroupBy(slice => Math.Max(0, slice.voiceIndex))
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.OrderBy(slice => slice.startTime).ToList());

            int resolvedSlotsPerBeat = ResolveSlotsPerBeatForMeasure(slicesByVoice.Values, measure);
            measure.SetSlotsPerBeat(resolvedSlotsPerBeat);
            int totalSlots = measure.TotalSlots;
            List<PsarcAlphaTabTimingBeatEntry> measureTimingEntries = new List<PsarcAlphaTabTimingBeatEntry>();

            if (slicesByVoice.Count == 0)
            {
                AppendRestSequence(measureElement, totalSlots, measure.DivisionsPerQuarter, measureTimingEntries, measure.startTime, measure.endTime, 1);
            }
            else
            {
                bool wroteVoice = false;
                foreach (KeyValuePair<int, List<EventSliceInfo>> voiceEntry in slicesByVoice)
                {
                    int voiceIndex = voiceEntry.Key;
                    List<EventSliceInfo> voiceSlices = voiceEntry.Value;
                    if (wroteVoice)
                        measureElement.Add(BuildBackupElement(totalSlots));

                    List<QuantizedEvent> quantizedEvents = QuantizeEventsForMeasure(voiceSlices, measure, resolvedSlotsPerBeat);
                    int cursorSlot = 0;
                    float cursorTime = measure.startTime;
                    int voiceNumber = voiceIndex + 1;

                    if (quantizedEvents.Count == 0)
                    {
                        AppendRestSequence(measureElement, totalSlots, measure.DivisionsPerQuarter, measureTimingEntries, measure.startTime, measure.endTime, voiceNumber);
                    }
                    else
                    {
                        for (int i = 0; i < quantizedEvents.Count; i++)
                        {
                            QuantizedEvent current = quantizedEvents[i];
                            if (current.startSlot > cursorSlot)
                            {
                                float restEndTime = Mathf.Clamp(current.sourceStartTime, cursorTime, measure.endTime);
                                AppendRestSequence(measureElement, current.startSlot - cursorSlot, measure.DivisionsPerQuarter, measureTimingEntries, cursorTime, restEndTime, voiceNumber);
                                cursorTime = restEndTime;
                            }

                            AppendEventSequence(measureElement, current, tuningPitches, measure.DivisionsPerQuarter, measureTimingEntries, current.sourceStartTime, current.sourceEndTime, voiceNumber, noteRenderContext);
                            cursorSlot = Math.Max(cursorSlot, current.endSlot);
                            cursorTime = Mathf.Max(cursorTime, current.sourceEndTime);
                        }

                        if (cursorSlot < totalSlots)
                            AppendRestSequence(measureElement, totalSlots - cursorSlot, measure.DivisionsPerQuarter, measureTimingEntries, cursorTime, measure.endTime, voiceNumber);
                    }

                    wroteVoice = true;
                }
            }

            measureTimingEntries.Sort((left, right) =>
            {
                int cmp = left.startTime.CompareTo(right.startTime);
                if (cmp != 0)
                    return cmp;
                return left.voiceIndex.CompareTo(right.voiceIndex);
            });
            timingSidecar.beats.AddRange(measureTimingEntries);

            partElement.Add(measureElement);
        }

        XElement root = new XElement("score-partwise",
            new XAttribute("version", "3.1"),
            new XElement("work", new XElement("work-title", manifest?.displayName ?? trackName)),
            new XElement("identification",
                new XElement("creator", new XAttribute("type", "composer"), manifest?.artist ?? string.Empty),
                new XElement("encoding",
                    new XElement("software", "StringTheory PSARC AlphaTab Export"))),
            new XElement("part-list", scorePart),
            partElement);

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
    }

    private static XElement BuildAttributesElement(MeasureInfo measure, int[] tuningPitches, int capo)
    {
        XElement staffDetails = new XElement("staff-details",
            new XElement("staff-lines", tuningPitches.Length.ToString(CultureInfo.InvariantCulture)));

        if (capo > 0)
            staffDetails.Add(new XElement("capo", capo.ToString(CultureInfo.InvariantCulture)));

        for (int musicXmlString = 1; musicXmlString <= tuningPitches.Length; musicXmlString++)
        {
            int runtimeStringIndex = tuningPitches.Length - musicXmlString;
            PitchData pitch = ToPitchData(tuningPitches[runtimeStringIndex]);
            XElement tuningElement = new XElement("staff-tuning",
                new XAttribute("line", musicXmlString.ToString(CultureInfo.InvariantCulture)),
                new XElement("tuning-step", pitch.step));
            if (pitch.alter != 0)
                tuningElement.Add(new XElement("tuning-alter", pitch.alter.ToString(CultureInfo.InvariantCulture)));
            tuningElement.Add(new XElement("tuning-octave", pitch.octave.ToString(CultureInfo.InvariantCulture)));
            staffDetails.Add(tuningElement);
        }

        return new XElement("attributes",
            new XElement("divisions", measure.DivisionsPerQuarter.ToString(CultureInfo.InvariantCulture)),
            new XElement("key", new XElement("fifths", "0")),
            new XElement("time",
                new XElement("beats", measure.beatCount.ToString(CultureInfo.InvariantCulture)),
                new XElement("beat-type", "4")),
            new XElement("clef",
                new XAttribute("number", "1"),
                new XElement("sign", "TAB"),
                new XElement("line", "5")),
            staffDetails);
    }

    private static XElement BuildChangedAttributesElement(MeasureInfo previousMeasure, MeasureInfo currentMeasure)
    {
        XElement attributes = new XElement("attributes");
        bool changed = false;

        if (previousMeasure == null || previousMeasure.DivisionsPerQuarter != currentMeasure.DivisionsPerQuarter)
        {
            attributes.Add(new XElement("divisions", currentMeasure.DivisionsPerQuarter.ToString(CultureInfo.InvariantCulture)));
            changed = true;
        }

        if (previousMeasure == null || previousMeasure.beatCount != currentMeasure.beatCount)
        {
            attributes.Add(new XElement("time",
                new XElement("beats", currentMeasure.beatCount.ToString(CultureInfo.InvariantCulture)),
                new XElement("beat-type", "4")));
            changed = true;
        }

        return changed ? attributes : null;
    }

    private static XElement BuildTempoElement(float tempoBpm)
    {
        int displayTempo = Math.Max(1, (int)Math.Round(tempoBpm));
        return new XElement("direction",
            new XElement("direction-type",
                new XElement("metronome",
                    new XElement("beat-unit", "quarter"),
                    new XElement("per-minute", displayTempo.ToString(CultureInfo.InvariantCulture)))),
            new XElement("sound", new XAttribute("tempo", tempoBpm.ToString("0.###", CultureInfo.InvariantCulture))));
    }

    private static List<MeasureInfo> BuildMeasures(PsarcCachedArrangementPart part, Dictionary<int, float> effectiveEndTimes)
    {
        List<PsarcCachedEbeatData> ebeats = (part?.timing?.ebeats ?? new List<PsarcCachedEbeatData>())
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
            List<PsarcCachedEbeatData> beats = ebeats.Skip(startIndex).Take(Math.Max(1, nextIndex - startIndex)).ToList();
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

    private static float ResolveFallbackBeatSeconds(PsarcCachedArrangementPart part)
    {
        float averageTempo = part?.timing?.averageTempoBpm ?? 120f;
        if (averageTempo <= 0.01f)
            averageTempo = 120f;
        return 60f / averageTempo;
    }

    private static float ResolveFinalTime(PsarcCachedArrangementPart part, List<PsarcCachedEbeatData> ebeats, float fallbackBeatSeconds, Dictionary<int, float> effectiveEndTimes)
    {
        float noteEnd = 0f;
        if (part?.notes != null && part.notes.Count > 0)
        {
            for (int i = 0; i < part.notes.Count; i++)
            {
                PsarcCachedNoteData note = part.notes[i];
                noteEnd = Math.Max(noteEnd, ResolveNoteEndTime(note, effectiveEndTimes));
            }
        }

        float lastBeat = ebeats.Count > 0 ? ebeats[ebeats.Count - 1].timeSeconds : 0f;
        return Math.Max(Math.Max(part?.durationSeconds ?? 0f, noteEnd), lastBeat + Math.Max(0.25f, fallbackBeatSeconds));
    }

    private static float EstimateFinalMeasureEnd(List<PsarcCachedEbeatData> beats, float finalTime, float fallbackBeatSeconds)
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

    private static float EstimateTempoBpm(List<PsarcCachedEbeatData> beats, float endTime, float fallbackTempoBpm)
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

    private static List<EventInfo> BuildEvents(List<PsarcCachedNoteData> notes, Dictionary<int, float> effectiveEndTimes)
    {
        List<EventInfo> events = new List<EventInfo>();
        if (notes == null || notes.Count == 0)
            return events;

        List<PsarcCachedNoteData> sorted = notes
            .Where(note => note != null)
            .OrderBy(note => note.time)
            .ThenBy(note => note.chordId)
            .ThenBy(note => note.stringIdx)
            .ToList();

        EventInfo current = null;
        int nextSourceEventId = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            PsarcCachedNoteData note = sorted[i];
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

    private static NoteRenderContext BuildNoteRenderContext(List<PsarcCachedNoteData> notes)
    {
        NoteRenderContext context = new NoteRenderContext();
        if (notes == null)
            return context;

        for (int i = 0; i < notes.Count; i++)
        {
            PsarcCachedNoteData note = notes[i];
            if (note == null)
                continue;

            context.notesById[note.id] = note;
            if (note.linkedFromNoteId >= 0 && !context.legatoDestinationByOriginId.ContainsKey(note.linkedFromNoteId))
                context.legatoDestinationByOriginId[note.linkedFromNoteId] = note;
        }

        return context;
    }

    private static Dictionary<int, float> BuildEffectiveEndTimes(PsarcCachedArrangementPart part)
    {
        Dictionary<int, float> effectiveEndTimes = new Dictionary<int, float>();
        List<PsarcCachedNoteData> notes = part?.notes;
        if (notes == null || notes.Count == 0)
            return effectiveEndTimes;

        List<PsarcCachedNoteData> sorted = notes
            .Where(note => note != null)
            .OrderBy(note => note.time)
            .ThenBy(note => note.stringIdx)
            .ThenBy(note => note.id)
            .ToList();

        Dictionary<int, PsarcCachedNoteData> linkedChildrenByParentId = new Dictionary<int, PsarcCachedNoteData>();
        Dictionary<int, PsarcCachedNoteData> nextNoteOnStringById = new Dictionary<int, PsarcCachedNoteData>();
        Dictionary<int, PsarcCachedNoteData> nextGlobalNoteById = new Dictionary<int, PsarcCachedNoteData>();
        PsarcCachedNoteData[] nextNoteOnString = new PsarcCachedNoteData[8];
        PsarcCachedNoteData nextGlobalNote = null;

        for (int i = sorted.Count - 1; i >= 0; i--)
        {
            PsarcCachedNoteData note = sorted[i];
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
            PsarcCachedNoteData note = sorted[i];
            if (note == null)
                continue;

            float explicitDuration = Mathf.Max(note.duration, 0f);
            float visualDuration = Mathf.Max(note.bendVisualDuration, 0f);
            float segmentDuration = 0f;
            if (note.techniqueSegments != null)
            {
                for (int segmentIndex = 0; segmentIndex < note.techniqueSegments.Count; segmentIndex++)
                {
                    PsarcCachedTechniqueSegmentData segment = note.techniqueSegments[segmentIndex];
                    if (segment == null)
                        continue;
                    segmentDuration = Mathf.Max(segmentDuration, Mathf.Max(0f, segment.endOffset));
                }
            }

            float endTime = note.time + Mathf.Max(explicitDuration, Mathf.Max(visualDuration, segmentDuration));
            if (endTime <= note.time + 0.0005f)
            {
                if (linkedChildrenByParentId.TryGetValue(note.id, out PsarcCachedNoteData linkedChild) &&
                    linkedChild != null &&
                    linkedChild.time > note.time + 0.0005f)
                {
                    endTime = linkedChild.time;
                }
                else if ((note.isLegato || !note.requiresPluck || note.linkedFromNoteId >= 0) &&
                         nextNoteOnStringById.TryGetValue(note.id, out PsarcCachedNoteData nextOnString) &&
                         nextOnString != null &&
                         nextOnString.time > note.time + 0.0005f)
                {
                    endTime = nextOnString.time;
                }
                else if (nextGlobalNoteById.TryGetValue(note.id, out PsarcCachedNoteData nextOnset) &&
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

    private static bool TryResolveNextBeatTime(List<PsarcCachedEbeatData> ebeats, float noteTime, out float nextBeatTime)
    {
        nextBeatTime = 0f;
        if (ebeats == null || ebeats.Count == 0)
            return false;

        for (int i = 0; i < ebeats.Count; i++)
        {
            PsarcCachedEbeatData ebeat = ebeats[i];
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

    private static float ResolveNoteEndTime(PsarcCachedNoteData note, Dictionary<int, float> effectiveEndTimes)
    {
        if (note == null)
            return 0f;

        if (effectiveEndTimes != null && effectiveEndTimes.TryGetValue(note.id, out float endTime))
            return endTime;

        return note.time + Mathf.Max(0.05f, note.duration);
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

    private static bool TryQuantizeEventsForMeasure(
        List<EventSliceInfo> events,
        MeasureInfo measure,
        int slotsPerBeat,
        out List<QuantizedEvent> result)
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

            QuantizedEvent quantized = new QuantizedEvent
            {
                startSlot = startSlot,
                endSlot = endSlot,
                sourceEventId = source.sourceEventId,
                sourceStartTime = source.startTime,
                sourceEndTime = clippedEndTime,
                tieFromPrevious = source.tieFromPrevious,
                tieToNext = source.tieToNext,
                notes = source.notes
            };
            result.Add(quantized);
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

    private static void AppendRestSequence(
        XElement measureElement,
        int slotCount,
        int slotsPerBeat,
        List<PsarcAlphaTabTimingBeatEntry> timingEntries,
        float startTime,
        float endTime,
        int voiceNumber)
    {
        List<DurationToken> tokens = DecomposeDuration(slotCount, slotsPerBeat);
        float cursorTime = startTime;
        int totalSlots = Math.Max(1, slotCount);
        int consumedSlots = 0;
        for (int i = 0; i < tokens.Count; i++)
        {
            DurationToken token = tokens[i];
            measureElement.Add(BuildRestElement(token, voiceNumber));
            consumedSlots += token.slots;
            float nextTime = i == tokens.Count - 1
                ? endTime
                : Mathf.Lerp(startTime, endTime, consumedSlots / (float)totalSlots);
            nextTime = Math.Max(cursorTime + 0.001f, nextTime);
            timingEntries?.Add(new PsarcAlphaTabTimingBeatEntry
            {
                startTime = cursorTime,
                endTime = nextTime,
                isRest = true,
                sourceEventId = -1,
                continuesFromPrevious = false,
                continuesToNext = false,
                voiceIndex = Math.Max(0, voiceNumber - 1)
            });
            cursorTime = nextTime;
        }
    }

    private static void AppendEventSequence(
        XElement measureElement,
        QuantizedEvent quantized,
        int[] tuningPitches,
        int slotsPerBeat,
        List<PsarcAlphaTabTimingBeatEntry> timingEntries,
        float startTime,
        float endTime,
        int voiceNumber,
        NoteRenderContext noteRenderContext)
    {
        List<DurationToken> tokens = DecomposeDuration(quantized.DurationSlots, slotsPerBeat);
        float cursorTime = startTime;
        int totalSlots = Math.Max(1, quantized.DurationSlots);
        int consumedSlots = 0;
        for (int tokenIndex = 0; tokenIndex < tokens.Count; tokenIndex++)
        {
            DurationToken token = tokens[tokenIndex];
            bool tieStart = tokenIndex < tokens.Count - 1 || (tokenIndex == tokens.Count - 1 && quantized.tieToNext);
            bool tieStop = tokenIndex > 0 || (tokenIndex == 0 && quantized.tieFromPrevious);
            bool continuesFromPrevious = tokenIndex > 0 || quantized.tieFromPrevious;
            bool continuesToNext = tokenIndex < tokens.Count - 1 || quantized.tieToNext;

            for (int noteIndex = 0; noteIndex < quantized.notes.Count; noteIndex++)
            {
                PsarcCachedNoteData note = quantized.notes[noteIndex];
                measureElement.Add(BuildNoteElement(note, token, tuningPitches, noteIndex > 0, tieStart, tieStop, voiceNumber, noteRenderContext));
            }

            consumedSlots += token.slots;
            float nextTime = tokenIndex == tokens.Count - 1
                ? endTime
                : Mathf.Lerp(startTime, endTime, consumedSlots / (float)totalSlots);
            nextTime = Math.Max(cursorTime + 0.001f, nextTime);
            timingEntries?.Add(new PsarcAlphaTabTimingBeatEntry
            {
                startTime = cursorTime,
                endTime = nextTime,
                isRest = false,
                sourceEventId = quantized.sourceEventId,
                continuesFromPrevious = continuesFromPrevious,
                continuesToNext = continuesToNext,
                voiceIndex = Math.Max(0, voiceNumber - 1)
            });
            cursorTime = nextTime;
        }
    }

    private static string GetTimingSidecarPath(string notationPath)
    {
        return $"{notationPath}.timing.json";
    }

    private static XElement BuildRestElement(DurationToken token, int voiceNumber)
    {
        XElement note = new XElement("note",
            new XAttribute("print-object", "no"),
            new XElement("rest"),
            new XElement("duration", token.slots.ToString(CultureInfo.InvariantCulture)),
            new XElement("voice", voiceNumber.ToString(CultureInfo.InvariantCulture)),
            new XElement("type", token.type));
        for (int i = 0; i < token.dotCount; i++)
            note.Add(new XElement("dot"));
        return note;
    }

    private static XElement BuildBackupElement(int durationSlots)
    {
        return new XElement("backup",
            new XElement("duration", Math.Max(1, durationSlots).ToString(CultureInfo.InvariantCulture)));
    }

    private static XElement BuildNoteElement(
        PsarcCachedNoteData note,
        DurationToken token,
        int[] tuningPitches,
        bool isChordTone,
        bool tieStart,
        bool tieStop,
        int voiceNumber,
        NoteRenderContext noteRenderContext)
    {
        int stringCount = tuningPitches != null && tuningPitches.Length > 0 ? tuningPitches.Length : 6;
        int clampedStringIndex = Math.Clamp(note.stringIdx, 0, Math.Max(0, stringCount - 1));
        int musicXmlString = stringCount - clampedStringIndex;
        int midi = ResolveMidiFromNote(note, tuningPitches);
        PitchData pitch = ToPitchData(midi);

        XElement noteElement = new XElement("note");
        if (isChordTone)
            noteElement.Add(new XElement("chord"));

        XElement pitchElement = new XElement("pitch",
            new XElement("step", pitch.step));
        if (pitch.alter != 0)
            pitchElement.Add(new XElement("alter", pitch.alter.ToString(CultureInfo.InvariantCulture)));
        pitchElement.Add(new XElement("octave", pitch.octave.ToString(CultureInfo.InvariantCulture)));
        noteElement.Add(pitchElement);

        noteElement.Add(
            new XElement("duration", token.slots.ToString(CultureInfo.InvariantCulture)),
            new XElement("voice", voiceNumber.ToString(CultureInfo.InvariantCulture)),
            new XElement("type", token.type));
        for (int i = 0; i < token.dotCount; i++)
            noteElement.Add(new XElement("dot"));

        if (tieStop)
            noteElement.Add(new XElement("tie", new XAttribute("type", "stop")));
        if (tieStart)
            noteElement.Add(new XElement("tie", new XAttribute("type", "start")));

        XElement technical = new XElement("technical",
            new XElement("string", musicXmlString.ToString(CultureInfo.InvariantCulture)),
            new XElement("fret", Math.Max(0, note.fret).ToString(CultureInfo.InvariantCulture)));

        if (note.bendStep > 0.01f)
        {
            technical.Add(BuildBendElement(note.bendStep, note.bendPreBend, release: false));
            if (note.bendRelease)
                technical.Add(BuildBendElement(note.bendStep, preBend: false, release: true));
        }

        AppendTechniqueElements(note, technical);

        XElement notations = new XElement("notations", technical);
        if (tieStop)
            notations.Add(new XElement("tied", new XAttribute("type", "stop")));
        if (tieStart)
            notations.Add(new XElement("tied", new XAttribute("type", "start")));

        AppendLinkedTechniqueNotations(note, notations, noteRenderContext);
        AppendExpressiveNotations(note, notations);
        noteElement.Add(notations);

        if (note.isMuted)
            noteElement.Add(new XElement("notehead", "x"));
        else if (note.isPinchHarmonic || IsHarmonic(note))
            noteElement.Add(new XElement("notehead", "diamond"));

        return noteElement;
    }

    private static XElement BuildBendElement(float bendStep, bool preBend, bool release)
    {
        XElement bend = new XElement("bend",
            new XElement("bend-alter", bendStep.ToString("0.###", CultureInfo.InvariantCulture)));
        if (preBend)
            bend.Add(new XElement("pre-bend"));
        if (release)
            bend.Add(new XElement("release"));
        return bend;
    }

    private static void AppendTechniqueElements(PsarcCachedNoteData note, XElement technical)
    {
        if (technical == null || note == null)
            return;

        if (IsHarmonic(note))
        {
            XElement harmonic = new XElement("harmonic");
            harmonic.Add(new XElement(note.isPinchHarmonic ? "artificial" : "natural"));
            technical.Add(harmonic);
        }

        if (note.isTap)
            technical.Add(new XElement("tap"));
    }

    private static void AppendLinkedTechniqueNotations(PsarcCachedNoteData note, XElement notations, NoteRenderContext context)
    {
        if (note == null || notations == null || context == null)
            return;

        XElement technical = notations.Element("technical");
        if (technical == null)
            return;

        if (context.legatoDestinationByOriginId.TryGetValue(note.id, out PsarcCachedNoteData destination))
            AddLegatoNotation(technical, notations, ResolveLegatoTechnique(destination, context), isStart: true);

        if (note.isLegato && note.linkedFromNoteId >= 0)
            AddLegatoNotation(technical, notations, ResolveLegatoTechnique(note, context), isStart: false);
    }

    private static void AddLegatoNotation(XElement technical, XElement notations, NoteTechnique technique, bool isStart)
    {
        if (technique == NoteTechnique.None)
            return;

        string type = isStart ? "start" : "stop";
        switch (technique)
        {
            case NoteTechnique.HammerOn:
                technical.Add(new XElement("hammer-on", new XAttribute("type", type), "H"));
                notations.Add(new XElement("slur", new XAttribute("type", type)));
                break;
            case NoteTechnique.PullOff:
                technical.Add(new XElement("pull-off", new XAttribute("type", type), "P"));
                notations.Add(new XElement("slur", new XAttribute("type", type)));
                break;
            case NoteTechnique.Slide:
                notations.Add(new XElement("slide", new XAttribute("type", type)));
                break;
        }
    }

    private static void AppendExpressiveNotations(PsarcCachedNoteData note, XElement notations)
    {
        if (note == null || notations == null)
            return;

        if (note.isAccent)
        {
            XElement articulations = EnsureChild(notations, "articulations");
            articulations.Add(new XElement("accent"));
        }

        if (IsVibrato(note))
        {
            XElement ornaments = EnsureChild(notations, "ornaments");
            ornaments.Add(new XElement("wavy-line", new XAttribute("type", "start")));
        }

        if (note.isTremolo)
        {
            XElement ornaments = EnsureChild(notations, "ornaments");
            ornaments.Add(new XElement("tremolo", new XAttribute("type", "single"), "3"));
        }
    }

    private static XElement EnsureChild(XElement parent, string elementName)
    {
        XElement child = parent.Element(elementName);
        if (child == null)
        {
            child = new XElement(elementName);
            parent.Add(child);
        }

        return child;
    }

    private static NoteTechnique ResolveLegatoTechnique(PsarcCachedNoteData note, NoteRenderContext context)
    {
        if (note == null)
            return NoteTechnique.None;

        if (note.slideTargetFret >= 0)
            return NoteTechnique.Slide;

        if (note.technique >= (int)NoteTechnique.None && note.technique <= (int)NoteTechnique.Vibrato)
        {
            NoteTechnique resolved = (NoteTechnique)note.technique;
            if (resolved == NoteTechnique.HammerOn || resolved == NoteTechnique.PullOff || resolved == NoteTechnique.Slide)
                return resolved;
        }

        if (note.isLegato && note.linkedFromNoteId >= 0)
        {
            if (context != null &&
                context.notesById.TryGetValue(note.linkedFromNoteId, out PsarcCachedNoteData previous) &&
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

    private static bool IsVibrato(PsarcCachedNoteData note)
    {
        if (note == null)
            return false;

        if (note.technique == (int)NoteTechnique.Vibrato)
            return true;

        if (note.hasVibrato)
            return true;

        if (note.techniqueSegments == null)
            return false;

        for (int i = 0; i < note.techniqueSegments.Count; i++)
        {
            if (note.techniqueSegments[i] != null && note.techniqueSegments[i].type == (int)NoteTechniqueSegmentType.Vibrato)
                return true;
        }

        return false;
    }

    private static bool IsHarmonic(PsarcCachedNoteData note)
    {
        return note != null && (note.isHarmonic || note.isPinchHarmonic);
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
            new DurationToken(slotsPerBeat * 4, "whole", 0),
            new DurationToken(slotsPerBeat * 3, "half", 1),
            new DurationToken(slotsPerBeat * 2, "half", 0),
            new DurationToken(slotsPerBeat + (slotsPerBeat / 2), "quarter", 1),
            new DurationToken(slotsPerBeat, "quarter", 0)
        };

        if (slotsPerBeat >= 2)
        {
            tokens.Add(new DurationToken((slotsPerBeat / 2) + (slotsPerBeat / 4), "eighth", 1));
            tokens.Add(new DurationToken(slotsPerBeat / 2, "eighth", 0));
        }

        if (slotsPerBeat >= 4)
        {
            tokens.Add(new DurationToken((slotsPerBeat / 4) + (slotsPerBeat / 8), "16th", 1));
            tokens.Add(new DurationToken(slotsPerBeat / 4, "16th", 0));
        }

        if (slotsPerBeat >= 8)
        {
            int thirtySecondSlots = slotsPerBeat / 8;
            if (thirtySecondSlots > 0)
            {
                if (slotsPerBeat >= 16)
                    tokens.Add(new DurationToken(thirtySecondSlots + (slotsPerBeat / 16), "32nd", 1));
                tokens.Add(new DurationToken(thirtySecondSlots, "32nd", 0));
            }
        }

        if (slotsPerBeat >= 16)
            tokens.Add(new DurationToken(slotsPerBeat / 16, "64th", 0));

        if (slotsPerBeat >= 32)
            tokens.Add(new DurationToken(slotsPerBeat / 32, "128th", 0));

        return tokens
            .Where(token => token.slots > 0)
            .Distinct()
            .OrderByDescending(token => token.slots)
            .ToArray();
    }

    private static int[] ResolveTuningPitches(PsarcCachedArrangementPart part, PsarcCachedArrangementSummary summary)
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

    private static int ResolveMidiFromNote(PsarcCachedNoteData note, int[] tuningPitches)
    {
        int[] resolvedTuning = tuningPitches != null && tuningPitches.Length > 0
            ? tuningPitches
            : StringTuningUtils.StandardGuitarTuning;
        int stringIndex = Math.Clamp(note.stringIdx, 0, resolvedTuning.Length - 1);
        return resolvedTuning[stringIndex] + Math.Max(0, note.fret);
    }

    private static PitchData ToPitchData(int midiNote)
    {
        int normalized = ((midiNote % 12) + 12) % 12;
        string pitchName = SharpPitchNames[normalized];
        int octave = (midiNote / 12) - 1;
        if (pitchName.Length > 1)
            return new PitchData(pitchName[0].ToString(), 1, octave);

        return new PitchData(pitchName, 0, octave);
    }

    private readonly struct PitchData
    {
        public readonly string step;
        public readonly int alter;
        public readonly int octave;

        public PitchData(string step, int alter, int octave)
        {
            this.step = step;
            this.alter = alter;
            this.octave = octave;
        }
    }

    private readonly struct DurationToken
    {
        public readonly int slots;
        public readonly string type;
        public readonly int dotCount;

        public DurationToken(int slots, string type, int dotCount)
        {
            this.slots = slots;
            this.type = type;
            this.dotCount = dotCount;
        }
    }

    private sealed class EventInfo
    {
        public readonly int sourceEventId;
        public readonly int chordId;
        public readonly float startTime;
        public float endTime;
        public int voiceIndex;
        public readonly List<PsarcCachedNoteData> notes = new List<PsarcCachedNoteData>();

        public EventInfo(PsarcCachedNoteData note, int sourceEventId, Dictionary<int, float> effectiveEndTimes)
        {
            this.sourceEventId = sourceEventId;
            chordId = note.chordId;
            startTime = note.time;
            endTime = ResolveNoteEndTime(note, effectiveEndTimes);
        }

        public bool CanAccept(PsarcCachedNoteData note)
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
        public List<PsarcCachedNoteData> notes = new List<PsarcCachedNoteData>();

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
        public List<PsarcCachedNoteData> notes = new List<PsarcCachedNoteData>();
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
        public readonly Dictionary<int, PsarcCachedNoteData> notesById = new Dictionary<int, PsarcCachedNoteData>();
        public readonly Dictionary<int, PsarcCachedNoteData> legatoDestinationByOriginId = new Dictionary<int, PsarcCachedNoteData>();
    }
}

[Serializable]
public sealed class PsarcAlphaTabTimingSidecar
{
    public int version = 2;
    public string notationPath = string.Empty;
    public List<PsarcAlphaTabTimingBeatEntry> beats = new List<PsarcAlphaTabTimingBeatEntry>();
}

[Serializable]
public sealed class PsarcAlphaTabTimingBeatEntry
{
    public float startTime;
    public float endTime;
    public bool isRest;
    public int masterBarIndex = -1;
    public int sourceEventId = -1;
    public bool continuesFromPrevious;
    public bool continuesToNext;
    public int voiceIndex;
    public string noteKey = string.Empty;
}

internal static class PsarcAlphaTabGpWriter
{
    private const int DefaultSlotsPerBeat = 8;
    private static readonly int[] SupportedSlotsPerBeat = { 8, 16, 32 };
    private static readonly string[] SharpPitchNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    private const bool UseGameplayAlphaTabLegatoDisplaySimplification = true;
    private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

    public static void Write(
        string outputGpPath,
        PsarcCachedSongManifest manifest,
        PsarcCachedArrangementSummary summary,
        PsarcCachedArrangementPart part)
    {
        if (string.IsNullOrWhiteSpace(outputGpPath))
            throw new ArgumentException("Output AlphaTex path was empty.", nameof(outputGpPath));

        string directory = Path.GetDirectoryName(outputGpPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        PsarcAlphaTabTimingSidecar timingSidecar;
        string alphaTex = BuildAlphaTex(manifest, summary, part, out timingSidecar);
        File.WriteAllText(outputGpPath, alphaTex, Utf8WithoutBom);

        string timingSidecarPath = $"{outputGpPath}.timing.json";
        timingSidecar.notationPath = outputGpPath;
        File.WriteAllText(timingSidecarPath, SerializeTimingSidecar(timingSidecar), Utf8WithoutBom);
    }

    private static string BuildAlphaTex(
        PsarcCachedSongManifest manifest,
        PsarcCachedArrangementSummary summary,
        PsarcCachedArrangementPart part,
        out PsarcAlphaTabTimingSidecar timingSidecar)
    {
        timingSidecar = new PsarcAlphaTabTimingSidecar();

        int[] tuningPitches = ResolveTuningPitches(part, summary);
        string trackName = !string.IsNullOrWhiteSpace(summary.displayName)
            ? summary.displayName
            : (!string.IsNullOrWhiteSpace(part.displayName) ? part.displayName : "Track");

        Dictionary<int, float> effectiveEndTimes = BuildEffectiveEndTimes(part);
        List<MeasureInfo> measures = BuildMeasures(part, effectiveEndTimes);
        List<EventInfo> events = BuildEvents(part.notes, effectiveEndTimes);
        AssignVoices(events);
        NoteRenderContext noteRenderContext = BuildNoteRenderContext(part?.notes);
        List<List<EventSliceInfo>> measureSlices = BuildEventSlices(events, measures);

        int voiceCount = Math.Max(1, events.Count > 0 ? events.Max(evt => evt.voiceIndex) + 1 : 1);
        List<List<string>> voiceMeasures = new List<List<string>>(voiceCount);
        for (int voiceIndex = 0; voiceIndex < voiceCount; voiceIndex++)
            voiceMeasures.Add(new List<string>(measures.Count));

        for (int measureIndex = 0; measureIndex < measures.Count; measureIndex++)
        {
            MeasureInfo measure = measures[measureIndex];
            Dictionary<int, List<EventSliceInfo>> slicesByVoice = measureSlices[measureIndex]
                .GroupBy(slice => Math.Max(0, slice.voiceIndex))
                .OrderBy(group => group.Key)
                .ToDictionary(group => group.Key, group => group.OrderBy(slice => slice.startTime).ToList());

            int resolvedSlotsPerBeat = ResolveSlotsPerBeatForMeasure(slicesByVoice.Values, measure);
            measure.SetSlotsPerBeat(resolvedSlotsPerBeat);

            for (int voiceIndex = 0; voiceIndex < voiceCount; voiceIndex++)
            {
                List<PsarcAlphaTabTimingBeatEntry> measureTimingEntries = new List<PsarcAlphaTabTimingBeatEntry>();
                string measureText = BuildMeasureAlphaTex(
                    measure,
                    measureIndex,
                    measureIndex == 0 && voiceIndex == 0 ? BuildInitialMeasureMetadata(summary, part, measure) : BuildChangedMeasureMetadata(measureIndex, measures, voiceIndex),
                    slicesByVoice.TryGetValue(voiceIndex, out List<EventSliceInfo> voiceSlices) ? voiceSlices : null,
                    tuningPitches,
                    voiceIndex,
                    noteRenderContext,
                    measureTimingEntries);

                voiceMeasures[voiceIndex].Add(measureText);
                timingSidecar.beats.AddRange(measureTimingEntries);
            }
        }

        timingSidecar.beats = timingSidecar.beats
            .OrderBy(entry => entry.startTime)
            .ThenBy(entry => entry.voiceIndex)
            .ToList();

        StringBuilder builder = new StringBuilder(8192);
        bool hasScoreMetadata = false;
        if (!string.IsNullOrWhiteSpace(manifest?.displayName))
        {
            builder.Append("\\title ").Append(Quote(EscapeAlphaTexString(manifest.displayName))).Append('\n');
            hasScoreMetadata = true;
        }
        if (hasScoreMetadata)
            builder.Append('.').Append('\n');
        builder.Append("\\track ").Append(Quote(EscapeAlphaTexString(trackName))).Append('\n');
        builder.Append("\\staff {tabs}\n");
        builder.Append("\\tuning ");
        for (int i = tuningPitches.Length - 1; i >= 0; i--)
        {
            if (i < tuningPitches.Length - 1)
                builder.Append(' ');
            builder.Append(ToAlphaTexPitch(tuningPitches[i]));
        }
        builder.Append('\n');
        if ((part?.timing?.capo ?? 0) > 0)
            builder.Append("\\capo ").Append((part?.timing?.capo ?? 0).ToString(CultureInfo.InvariantCulture)).Append('\n');
        for (int voiceIndex = 0; voiceIndex < voiceCount; voiceIndex++)
        {
            builder.Append("\\voice\n");
            builder.Append(string.Join(" | ", voiceMeasures[voiceIndex]));
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static string BuildInitialMeasureMetadata(
        PsarcCachedArrangementSummary summary,
        PsarcCachedArrangementPart part,
        MeasureInfo measure)
    {
        StringBuilder builder = new StringBuilder();
        AppendMeasureMetadata(builder, null, measure);
        return builder.ToString().Trim();
    }

    private static string BuildChangedMeasureMetadata(int measureIndex, List<MeasureInfo> measures, int voiceIndex)
    {
        if (voiceIndex != 0 || measureIndex <= 0 || measures == null || measureIndex >= measures.Count)
            return string.Empty;

        StringBuilder builder = new StringBuilder();
        AppendMeasureMetadata(builder, measures[measureIndex - 1], measures[measureIndex]);
        return builder.ToString().Trim();
    }

    private static void AppendMeasureMetadata(StringBuilder builder, MeasureInfo previous, MeasureInfo current)
    {
        if (current == null)
            return;

        if (previous == null || previous.beatCount != current.beatCount)
        {
            if (builder.Length > 0) builder.Append(' ');
            if (current.beatCount == 4)
                builder.Append("\\ts common");
            else
                builder.Append("\\ts ").Append(current.beatCount.ToString(CultureInfo.InvariantCulture)).Append(" 4");
        }

        // AlphaTab sheet rendering in this path uses the Psarc timing sidecar for all
        // playback/cursor timing. Emitting bar tempo metadata here is not required for layout
        // and causes parser incompatibilities across AlphaTex versions, so we leave it out.
    }

    private static string BuildMeasureAlphaTex(
        MeasureInfo measure,
        int masterBarIndex,
        string metadataPrefix,
        List<EventSliceInfo> voiceSlices,
        int[] tuningPitches,
        int voiceIndex,
        NoteRenderContext noteRenderContext,
        List<PsarcAlphaTabTimingBeatEntry> timingEntries)
    {
        int totalSlots = measure.TotalSlots;
        List<string> beats = new List<string>();

        if (voiceSlices == null || voiceSlices.Count == 0)
        {
            AppendRestSequence(beats, totalSlots, measure.DivisionsPerQuarter, timingEntries, measure.startTime, measure.endTime, masterBarIndex, voiceIndex);
        }
        else
        {
            List<QuantizedEvent> quantizedEvents = QuantizeEventsForMeasure(voiceSlices, measure, measure.DivisionsPerQuarter);
            int cursorSlot = 0;
            float cursorTime = measure.startTime;

            for (int i = 0; i < quantizedEvents.Count; i++)
            {
                QuantizedEvent current = quantizedEvents[i];
                if (current.startSlot > cursorSlot)
                {
                    float restEndTime = Mathf.Clamp(current.sourceStartTime, cursorTime, measure.endTime);
                    AppendRestSequence(beats, current.startSlot - cursorSlot, measure.DivisionsPerQuarter, timingEntries, cursorTime, restEndTime, masterBarIndex, voiceIndex);
                    cursorTime = restEndTime;
                }

                AppendEventSequence(beats, current, tuningPitches, measure.DivisionsPerQuarter, timingEntries, current.sourceStartTime, current.sourceEndTime, masterBarIndex, voiceIndex, noteRenderContext);
                cursorSlot = Math.Max(cursorSlot, current.endSlot);
                cursorTime = Mathf.Max(cursorTime, current.sourceEndTime);
            }

            if (cursorSlot < totalSlots)
                AppendRestSequence(beats, totalSlots - cursorSlot, measure.DivisionsPerQuarter, timingEntries, cursorTime, measure.endTime, masterBarIndex, voiceIndex);
        }

        string beatsText = beats.Count > 0 ? string.Join(" ", beats) : $"r.{ResolveDurationValue(measure.DivisionsPerQuarter * 4)}";
        if (string.IsNullOrWhiteSpace(metadataPrefix))
            return beatsText;
        return $"{metadataPrefix} {beatsText}".Trim();
    }

    private static void AppendRestSequence(
        List<string> beats,
        int slotCount,
        int slotsPerBeat,
        List<PsarcAlphaTabTimingBeatEntry> timingEntries,
        float startTime,
        float endTime,
        int masterBarIndex,
        int voiceIndex)
    {
        List<DurationToken> tokens = DecomposeDuration(slotCount, slotsPerBeat);
        float cursorTime = startTime;
        int totalSlots = Math.Max(1, slotCount);
        int consumedSlots = 0;
        for (int i = 0; i < tokens.Count; i++)
        {
            DurationToken token = tokens[i];
            beats.Add($"r.{token.value.ToString(CultureInfo.InvariantCulture)}");
            consumedSlots += token.slots;
            float nextTime = i == tokens.Count - 1
                ? endTime
                : Mathf.Lerp(startTime, endTime, consumedSlots / (float)totalSlots);
            nextTime = Math.Max(cursorTime + 0.001f, nextTime);
            timingEntries?.Add(new PsarcAlphaTabTimingBeatEntry
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
        List<string> beats,
        QuantizedEvent quantized,
        int[] tuningPitches,
        int slotsPerBeat,
        List<PsarcAlphaTabTimingBeatEntry> timingEntries,
        float startTime,
        float endTime,
        int masterBarIndex,
        int voiceIndex,
        NoteRenderContext noteRenderContext)
    {
        List<DurationToken> tokens = DecomposeDuration(quantized.DurationSlots, slotsPerBeat);
        if (UseGameplayAlphaTabLegatoDisplaySimplification &&
            tokens.Count > 1 &&
            ShouldFlattenLegatoOriginDisplay(quantized, noteRenderContext))
        {
            tokens = new List<DurationToken> { tokens[0] };
        }
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

            beats.Add(BuildEventBeatText(quantized, token, tuningPitches, noteRenderContext, isAttackToken, cursorTime, nextTime));

            timingEntries?.Add(new PsarcAlphaTabTimingBeatEntry
            {
                startTime = cursorTime,
                endTime = nextTime,
                isRest = false,
                masterBarIndex = masterBarIndex,
                sourceEventId = quantized.sourceEventId,
                continuesFromPrevious = continuesFromPrevious,
                continuesToNext = continuesToNext,
                voiceIndex = voiceIndex,
                noteKey = BuildTimingNoteKey(quantized.notes, tuningPitches != null && tuningPitches.Length > 0 ? tuningPitches.Length : 6)
            });
            cursorTime = nextTime;
        }
    }

    private static string BuildEventBeatText(
        QuantizedEvent quantized,
        DurationToken token,
        int[] tuningPitches,
        NoteRenderContext noteRenderContext,
        bool isAttackToken,
        float tokenStartTime,
        float tokenEndTime)
    {
        int stringCount = tuningPitches != null && tuningPitches.Length > 0 ? tuningPitches.Length : 6;
        List<string> noteTokens = new List<string>(quantized.notes.Count);
        for (int i = 0; i < quantized.notes.Count; i++)
        {
            PsarcCachedNoteData note = quantized.notes[i];
            noteTokens.Add(BuildNoteText(note, stringCount, noteRenderContext, isAttackToken, tokenStartTime, tokenEndTime));
        }

        string content = noteTokens.Count == 1
            ? noteTokens[0]
            : $"({string.Join(" ", noteTokens)})";

        string beatText = $"{content}.{token.value.ToString(CultureInfo.InvariantCulture)}";
        string beatEffects = BuildBeatEffects(quantized, tokenStartTime, tokenEndTime, token.value);
        if (!string.IsNullOrWhiteSpace(beatEffects))
            beatText += $" {{{beatEffects}}}";
        return beatText;
    }

    private static string BuildNoteText(
        PsarcCachedNoteData note,
        int stringCount,
        NoteRenderContext noteRenderContext,
        bool isAttackToken,
        float tokenStartTime,
        float tokenEndTime)
    {
        bool isTieContinuation = !isAttackToken;
        int alphaTexString = Math.Max(1, stringCount - Mathf.Clamp(note.stringIdx, 0, Math.Max(0, stringCount - 1)));
        string noteValue = isTieContinuation
            ? "-"
            : (note.isFretHandMute ? "x" : Math.Max(0, note.fret).ToString(CultureInfo.InvariantCulture));

        List<string> effects = new List<string>();
        if (isAttackToken)
            AppendAttackEffects(effects, note, noteRenderContext);
        AppendSustainEffects(effects, note, tokenStartTime, tokenEndTime);

        string text = $"{noteValue}.{alphaTexString.ToString(CultureInfo.InvariantCulture)}";
        if (effects.Count > 0)
            text += $"{{{string.Join(" ", effects)}}}";
        return text;
    }

    private static void AppendAttackEffects(List<string> effects, PsarcCachedNoteData note, NoteRenderContext noteRenderContext)
    {
        if (note == null || effects == null)
            return;

        if (TryResolveOriginLegatoEffect(note, noteRenderContext, out string legatoEffect))
            effects.Add(legatoEffect);

        if (note.isPalmMute)
            effects.Add("pm");
        if (note.isAccent)
            effects.Add("ac");
        if (note.isTap)
            effects.Add("lht");
        if (note.isPinchHarmonic)
            effects.Add("ph");
        else if (note.isHarmonic)
            effects.Add("nh");
    }

    private static void AppendSustainEffects(List<string> effects, PsarcCachedNoteData note, float tokenStartTime, float tokenEndTime)
    {
        if (note == null || effects == null)
            return;

        string bendEffect = BuildBendEffect(note, tokenStartTime, tokenEndTime);
        if (!string.IsNullOrWhiteSpace(bendEffect))
            effects.Add(bendEffect);

        if (HasVibratoDuringWindow(note, tokenStartTime, tokenEndTime))
            effects.Add("v");
    }

    private static string BuildBeatEffects(QuantizedEvent quantized, float tokenStartTime, float tokenEndTime, int tokenDurationValue)
    {
        if (quantized?.notes == null || quantized.notes.Count == 0)
            return string.Empty;

        if (!quantized.notes.Any(note => note != null && note.isTremolo))
            return string.Empty;

        int tremoloSpeed = ResolveTremoloSpeed(tokenDurationValue);
        return $"tp {tremoloSpeed.ToString(CultureInfo.InvariantCulture)}";
    }

    private static int ResolveTremoloSpeed(int tokenDurationValue)
    {
        if (tokenDurationValue >= 32)
            return 32;
        if (tokenDurationValue >= 16)
            return 16;
        return 8;
    }

    private static string BuildTimingNoteKey(IEnumerable<PsarcCachedNoteData> notes, int stringCount)
    {
        if (notes == null)
            return "rest";

        List<string> parts = notes
            .Where(note => note != null)
            .OrderBy(note => Math.Max(1, stringCount - Mathf.Clamp(note.stringIdx, 0, Math.Max(0, stringCount - 1))))
            .ThenBy(note => note.fret)
            .Select(note =>
            {
                int alphaTexString = Math.Max(1, stringCount - Mathf.Clamp(note.stringIdx, 0, Math.Max(0, stringCount - 1)));
                return $"{alphaTexString}:{Math.Max(0, note.fret)}:{(note.isFretHandMute ? 1 : 0)}";
            })
            .ToList();

        return parts.Count == 0 ? "rest" : string.Join("|", parts);
    }

    private static string SerializeTimingSidecar(PsarcAlphaTabTimingSidecar sidecar)
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.Append("{\n");
        builder.Append("  \"version\": ").Append(sidecar?.version ?? 0).Append(",\n");
        builder.Append("  \"notationPath\": ").Append(QuoteJson(sidecar?.notationPath ?? string.Empty)).Append(",\n");
        builder.Append("  \"beats\": [\n");

        List<PsarcAlphaTabTimingBeatEntry> beats = sidecar?.beats ?? new List<PsarcAlphaTabTimingBeatEntry>();
        for (int i = 0; i < beats.Count; i++)
        {
            PsarcAlphaTabTimingBeatEntry beat = beats[i] ?? new PsarcAlphaTabTimingBeatEntry();
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

    private static bool TryResolveOriginLegatoEffect(PsarcCachedNoteData origin, NoteRenderContext context, out string effect)
    {
        effect = string.Empty;
        if (origin == null || context == null || !context.legatoDestinationByOriginId.TryGetValue(origin.id, out PsarcCachedNoteData destination) || destination == null)
            return false;

        if (destination.slideTargetFret >= 0 || (NoteTechnique)Mathf.Clamp(destination.technique, 0, (int)NoteTechnique.Vibrato) == NoteTechnique.Slide)
        {
            effect = destination.requiresPluck ? "ss" : "sl";
            return true;
        }

        if (destination.isHammerOn)
        {
            effect = "h";
            return true;
        }

        if (destination.isPullOff)
        {
            effect = "h";
            return true;
        }

        if (destination.isHopo)
        {
            effect = "h";
            return true;
        }

        if (destination.isLegato && destination.linkedFromNoteId >= 0)
        {
            if (destination.fret != origin.fret)
            {
                effect = "h";
                return true;
            }
        }

        return false;
    }

    private static bool ShouldFlattenLegatoOriginDisplay(QuantizedEvent quantized, NoteRenderContext context)
    {
        if (quantized?.notes == null || context == null)
            return false;

        for (int i = 0; i < quantized.notes.Count; i++)
        {
            PsarcCachedNoteData origin = quantized.notes[i];
            if (origin == null)
                continue;
            if (!context.legatoDestinationByOriginId.TryGetValue(origin.id, out PsarcCachedNoteData destination) || destination == null)
                continue;
            if (destination.time <= origin.time + 0.0005f)
                continue;
            if (Math.Abs(destination.time - quantized.sourceEndTime) > 0.02f)
                continue;
            return true;
        }

        return false;
    }

    private static bool HasVibratoDuringWindow(PsarcCachedNoteData note, float tokenStartTime, float tokenEndTime)
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
            PsarcCachedTechniqueSegmentData segment = note.techniqueSegments[i];
            if (segment == null || segment.type != (int)NoteTechniqueSegmentType.Vibrato)
                continue;

            if (segment.endOffset > relativeStart + 0.0005f && segment.startOffset < relativeEnd - 0.0005f)
                return true;
        }

        return false;
    }

    private static string BuildBendEffect(PsarcCachedNoteData note, float tokenStartTime, float tokenEndTime)
    {
        if (note == null)
            return string.Empty;

        List<PsarcCachedBendPointData> bendPoints = note.bendPoints?
            .Where(point => point != null)
            .OrderBy(point => point.timeSeconds)
            .ToList();

        float windowStart = Math.Max(0f, tokenStartTime - note.time);
        float windowEnd = Math.Max(windowStart + 0.001f, tokenEndTime - note.time);

        if (bendPoints == null || bendPoints.Count == 0)
            return BuildFallbackBendEffect(note);

        float startStep = SampleBendStep(bendPoints, windowStart, note);
        float endStep = SampleBendStep(bendPoints, windowEnd, note);
        List<(int offset, float value)> entries = new List<(int offset, float value)>
        {
            (0, ToAlphaTabBendValue(startStep))
        };

        for (int i = 0; i < bendPoints.Count; i++)
        {
            PsarcCachedBendPointData point = bendPoints[i];
            if (point.timeSeconds <= windowStart + 0.0005f || point.timeSeconds >= windowEnd - 0.0005f)
                continue;

            int offset = Mathf.Clamp(Mathf.RoundToInt(((point.timeSeconds - windowStart) / Math.Max(0.001f, windowEnd - windowStart)) * 60f), 0, 60);
            entries.Add((offset, ToAlphaTabBendValue(point.step)));
        }

        entries.Add((60, ToAlphaTabBendValue(endStep)));
        entries = entries
            .OrderBy(entry => entry.offset)
            .GroupBy(entry => entry.offset)
            .Select(group => group.Last())
            .ToList();

        bool anyNonZero = entries.Any(entry => Math.Abs(entry.value) > 0.01f);
        if (!anyNonZero)
            return string.Empty;

        StringBuilder builder = new StringBuilder("be (");
        for (int i = 0; i < entries.Count; i++)
        {
            if (i > 0)
                builder.Append(' ');
            builder.Append(entries[i].offset.ToString(CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(entries[i].value.ToString("0.###", CultureInfo.InvariantCulture));
        }
        builder.Append(')');
        return builder.ToString();
    }

    private static string BuildFallbackBendEffect(PsarcCachedNoteData note)
    {
        if (note == null)
            return string.Empty;

        float bend = Math.Max(note.bendStep, Math.Max(0f, note.maxBend));
        if (bend <= 0.01f)
            return string.Empty;

        float value = ToAlphaTabBendValue(bend);
        if (note.bendPreBend && note.bendRelease)
            return $"be (0 {value.ToString("0.###", CultureInfo.InvariantCulture)} 60 0)";
        if (note.bendPreBend)
            return $"be (0 {value.ToString("0.###", CultureInfo.InvariantCulture)} 60 {value.ToString("0.###", CultureInfo.InvariantCulture)})";
        if (note.bendRelease)
            return $"be (0 0 30 {value.ToString("0.###", CultureInfo.InvariantCulture)} 60 0)";
        return $"be (0 0 60 {value.ToString("0.###", CultureInfo.InvariantCulture)})";
    }

    private static float SampleBendStep(List<PsarcCachedBendPointData> points, float timeSeconds, PsarcCachedNoteData note)
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
            PsarcCachedBendPointData previous = points[i - 1];
            PsarcCachedBendPointData current = points[i];
            if (timeSeconds <= current.timeSeconds + 0.0005f)
            {
                float span = Math.Max(0.0001f, current.timeSeconds - previous.timeSeconds);
                float t = Mathf.Clamp01((timeSeconds - previous.timeSeconds) / span);
                return Mathf.Lerp(previous.step, current.step, t);
            }
        }

        return points[points.Count - 1].step;
    }

    private static float ResolveInitialBendStep(List<PsarcCachedBendPointData> points, PsarcCachedNoteData note)
    {
        if (points == null || points.Count == 0)
            return Mathf.Max(note?.bendStep ?? 0f, 0f);

        if (points[0].timeSeconds <= 0.0005f)
            return points[0].step;

        if (note != null && note.bendPreBend)
            return Math.Max(points[0].step, Math.Max(note.bendStep, Math.Max(0f, note.maxBend)));

        return 0f;
    }

    private static float ToAlphaTabBendValue(float semitoneStep)
    {
        return Mathf.Round(semitoneStep * 2f);
    }

    private static Dictionary<int, float> BuildEffectiveEndTimes(PsarcCachedArrangementPart part)
    {
        Dictionary<int, float> effectiveEndTimes = new Dictionary<int, float>();
        List<PsarcCachedNoteData> notes = part?.notes;
        if (notes == null || notes.Count == 0)
            return effectiveEndTimes;

        List<PsarcCachedNoteData> sorted = notes
            .Where(note => note != null)
            .OrderBy(note => note.time)
            .ThenBy(note => note.stringIdx)
            .ThenBy(note => note.id)
            .ToList();

        Dictionary<int, PsarcCachedNoteData> linkedChildrenByParentId = new Dictionary<int, PsarcCachedNoteData>();
        Dictionary<int, PsarcCachedNoteData> nextNoteOnStringById = new Dictionary<int, PsarcCachedNoteData>();
        Dictionary<int, PsarcCachedNoteData> nextGlobalNoteById = new Dictionary<int, PsarcCachedNoteData>();
        PsarcCachedNoteData[] nextNoteOnString = new PsarcCachedNoteData[8];
        PsarcCachedNoteData nextGlobalNote = null;

        for (int i = sorted.Count - 1; i >= 0; i--)
        {
            PsarcCachedNoteData note = sorted[i];
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
            PsarcCachedNoteData note = sorted[i];
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
                    PsarcCachedTechniqueSegmentData segment = note.techniqueSegments[segmentIndex];
                    if (segment == null)
                        continue;
                    segmentDuration = Mathf.Max(segmentDuration, Mathf.Max(0f, segment.endOffset));
                }
            }

            float endTime = note.time + Mathf.Max(explicitDuration, Mathf.Max(rawBendDuration, Mathf.Max(visualDuration, segmentDuration)));
            if (endTime <= note.time + 0.0005f)
            {
                if (linkedChildrenByParentId.TryGetValue(note.id, out PsarcCachedNoteData linkedChild) &&
                    linkedChild != null &&
                    linkedChild.time > note.time + 0.0005f)
                {
                    endTime = linkedChild.time;
                }
                else if ((note.isLegato || !note.requiresPluck || note.linkedFromNoteId >= 0) &&
                         nextNoteOnStringById.TryGetValue(note.id, out PsarcCachedNoteData nextOnString) &&
                         nextOnString != null &&
                         nextOnString.time > note.time + 0.0005f)
                {
                    endTime = nextOnString.time;
                }
                else if (nextGlobalNoteById.TryGetValue(note.id, out PsarcCachedNoteData nextOnset) &&
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

    private static bool TryResolveNextBeatTime(List<PsarcCachedEbeatData> ebeats, float noteTime, out float nextBeatTime)
    {
        nextBeatTime = 0f;
        if (ebeats == null || ebeats.Count == 0)
            return false;

        for (int i = 0; i < ebeats.Count; i++)
        {
            PsarcCachedEbeatData ebeat = ebeats[i];
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

    private static float ResolveNoteEndTime(PsarcCachedNoteData note, Dictionary<int, float> effectiveEndTimes)
    {
        if (note == null)
            return 0f;

        if (effectiveEndTimes != null && effectiveEndTimes.TryGetValue(note.id, out float endTime))
            return endTime;

        return note.time + Mathf.Max(0.05f, note.duration);
    }

    private static List<MeasureInfo> BuildMeasures(PsarcCachedArrangementPart part, Dictionary<int, float> effectiveEndTimes)
    {
        List<PsarcCachedEbeatData> ebeats = (part?.timing?.ebeats ?? new List<PsarcCachedEbeatData>())
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
            List<PsarcCachedEbeatData> beats = ebeats.Skip(startIndex).Take(Math.Max(1, nextIndex - startIndex)).ToList();
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

    private static float ResolveFallbackBeatSeconds(PsarcCachedArrangementPart part)
    {
        float averageTempo = part?.timing?.averageTempoBpm ?? 120f;
        if (averageTempo <= 0.01f)
            averageTempo = 120f;
        return 60f / averageTempo;
    }

    private static float ResolveFinalTime(PsarcCachedArrangementPart part, List<PsarcCachedEbeatData> ebeats, float fallbackBeatSeconds, Dictionary<int, float> effectiveEndTimes)
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

    private static float EstimateFinalMeasureEnd(List<PsarcCachedEbeatData> beats, float finalTime, float fallbackBeatSeconds)
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

    private static float EstimateTempoBpm(List<PsarcCachedEbeatData> beats, float endTime, float fallbackTempoBpm)
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

    private static List<EventInfo> BuildEvents(List<PsarcCachedNoteData> notes, Dictionary<int, float> effectiveEndTimes)
    {
        List<EventInfo> events = new List<EventInfo>();
        if (notes == null || notes.Count == 0)
            return events;

        List<PsarcCachedNoteData> sorted = notes
            .Where(note => note != null)
            .OrderBy(note => note.time)
            .ThenBy(note => note.chordId)
            .ThenBy(note => note.stringIdx)
            .ToList();

        EventInfo current = null;
        int nextSourceEventId = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            PsarcCachedNoteData note = sorted[i];
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
        List<DurationToken> availableTokens = BuildDurationTokens(slotsPerBeat);
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

    private static List<DurationToken> BuildDurationTokens(int slotsPerBeat)
    {
        List<DurationToken> tokens = new List<DurationToken>
        {
            new DurationToken(slotsPerBeat * 4, 1, 0),
            new DurationToken(slotsPerBeat * 2, 2, 0),
            new DurationToken(slotsPerBeat, 4, 0)
        };

        if (slotsPerBeat >= 2)
            tokens.Add(new DurationToken(slotsPerBeat / 2, 8, 0));
        if (slotsPerBeat >= 4)
            tokens.Add(new DurationToken(slotsPerBeat / 4, 16, 0));
        if (slotsPerBeat >= 8)
        {
            int thirtySecondSlots = slotsPerBeat / 8;
            if (thirtySecondSlots > 0)
            {
                tokens.Add(new DurationToken(thirtySecondSlots, 32, 0));
            }
        }
        if (slotsPerBeat >= 16)
            tokens.Add(new DurationToken(slotsPerBeat / 16, 64, 0));
        if (slotsPerBeat >= 32)
            tokens.Add(new DurationToken(slotsPerBeat / 32, 128, 0));

        return tokens
            .Where(token => token.slots > 0)
            .OrderByDescending(token => token.slots)
            .ToList();
    }

    private static int ResolveDurationValue(int slotCount)
    {
        if (slotCount <= 0)
            return 4;
        return slotCount;
    }

    private static NoteRenderContext BuildNoteRenderContext(List<PsarcCachedNoteData> notes)
    {
        NoteRenderContext context = new NoteRenderContext();
        if (notes == null)
            return context;

        for (int i = 0; i < notes.Count; i++)
        {
            PsarcCachedNoteData note = notes[i];
            if (note == null)
                continue;

            context.notesById[note.id] = note;
            if (note.linkedFromNoteId >= 0 && !context.legatoDestinationByOriginId.ContainsKey(note.linkedFromNoteId))
                context.legatoDestinationByOriginId[note.linkedFromNoteId] = note;
        }

        return context;
    }

    private static int[] ResolveTuningPitches(PsarcCachedArrangementPart part, PsarcCachedArrangementSummary summary)
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

    private static string ToAlphaTexPitch(int midiNote)
    {
        int normalized = ((midiNote % 12) + 12) % 12;
        string pitchName = SharpPitchNames[normalized];
        int octave = (midiNote / 12) - 1;
        return $"{pitchName}{octave.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string EscapeAlphaTexString(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }

    private sealed class EventInfo
    {
        public readonly int sourceEventId;
        public readonly int chordId;
        public readonly float startTime;
        public float endTime;
        public int voiceIndex;
        public readonly List<PsarcCachedNoteData> notes = new List<PsarcCachedNoteData>();

        public EventInfo(PsarcCachedNoteData note, int sourceEventId, Dictionary<int, float> effectiveEndTimes)
        {
            this.sourceEventId = sourceEventId;
            chordId = note.chordId;
            startTime = note.time;
            endTime = ResolveNoteEndTime(note, effectiveEndTimes);
        }

        public bool CanAccept(PsarcCachedNoteData note)
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
        public List<PsarcCachedNoteData> notes = new List<PsarcCachedNoteData>();
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
        public List<PsarcCachedNoteData> notes = new List<PsarcCachedNoteData>();
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
        public readonly Dictionary<int, PsarcCachedNoteData> notesById = new Dictionary<int, PsarcCachedNoteData>();
        public readonly Dictionary<int, PsarcCachedNoteData> legatoDestinationByOriginId = new Dictionary<int, PsarcCachedNoteData>();
    }

    private readonly struct DurationToken
    {
        public readonly int slots;
        public readonly int value;
        public readonly int dotCount;

        public DurationToken(int slots, int value, int dotCount)
        {
            this.slots = slots;
            this.value = value;
            this.dotCount = dotCount;
        }
    }
}
