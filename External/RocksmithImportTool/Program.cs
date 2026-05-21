using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ImageMagick;
using Microsoft.FSharp.Core;
using Rocksmith2014.Audio;
using Rocksmith2014.DLCProject;
using Rocksmith2014.XML;
using Rocksmith2014.XML.Processing;

internal static class Program
{
    private const int SchemaVersion = 16;
    private const string ManifestFileName = "song.rs2song.json";
    private const string ContentDirectoryName = "psarc_content";
    private const float RocksmithVibratoCyclesPerSecond = 5f;
    private const float RocksmithBendDrivenVibratoMinimumHoldSeconds = 0.12f;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        IncludeFields = true,
        WriteIndented = true
    };

    private static readonly int[] StandardGuitarTuning = { 40, 45, 50, 55, 59, 64 };
    private static readonly int[] StandardBassTuning = { 28, 33, 38, 43 };
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 3 || !string.Equals(args[0], "import", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Usage: RocksmithImportTool import <psarcPath> <targetDirectory>");
            return 2;
        }

        string psarcPath = Path.GetFullPath(args[1]);
        string targetDirectory = Path.GetFullPath(args[2]);

        try
        {
            await ImportAsync(psarcPath, targetDirectory);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static async Task ImportAsync(string psarcPath, string targetDirectory)
    {
        if (!File.Exists(psarcPath))
            throw new FileNotFoundException("PSARC file not found.", psarcPath);

        Directory.CreateDirectory(targetDirectory);
        string contentDirectory = Path.Combine(targetDirectory, ContentDirectoryName);
        ResetContentDirectory(contentDirectory);

        Console.WriteLine($"[RocksmithImportTool] Importing {psarcPath}");
        FSharpFunc<Unit, Unit> progress = new ConsoleProgressFunc();

        await Rocksmith2014.DLCProject.PsarcImporter.import(
            progress,
            psarcPath,
            contentDirectory);

        ConvertExtractedAudio(contentDirectory);

        List<string> arrangementXmlPaths = Directory.GetFiles(contentDirectory, "arr_*_RS2.xml", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                string fileName = Path.GetFileName(path);
                return !fileName.Contains("showlights", StringComparison.OrdinalIgnoreCase) &&
                       !fileName.Contains("vocals", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (arrangementXmlPaths.Count == 0)
            throw new InvalidOperationException("No instrumental Rocksmith arrangements were extracted.");

        List<string> manifestJsonPaths = Directory.GetFiles(contentDirectory, "*.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<CachedArrangementSummary> arrangementSummaries = new List<CachedArrangementSummary>();
        int maxDifficultyRating = 0;
        float maxDurationSeconds = 0f;
        InstrumentalArrangement firstArrangement = null;

        for (int i = 0; i < arrangementXmlPaths.Count; i++)
        {
            string xmlPath = arrangementXmlPaths[i];
            InstrumentalArrangement arrangement = InstrumentalArrangement.Load(xmlPath);
            firstArrangement ??= arrangement;
            ImproveArrangementForImport(arrangement);
            ArrangementContext context = ArrangementContext.From(arrangement, xmlPath, i);
            context.Tones = ExtractArrangementTones(manifestJsonPaths, xmlPath, context.Route);

            List<ArrangementVariantBuildResult> variants = BuildVariants(context);
            for (int variantIndex = 0; variantIndex < variants.Count; variantIndex++)
            {
                ArrangementVariantBuildResult variant = variants[variantIndex];
                string partFilePath = Path.Combine(contentDirectory, $"{SanitizeFileName(variant.Context.PartId)}.rs2part.json");
                File.WriteAllText(partFilePath, JsonSerializer.Serialize(variant.Part, JsonOptions));

                arrangementSummaries.Add(new CachedArrangementSummary
                {
                    partId = variant.Context.PartId,
                    displayName = variant.Context.DisplayName,
                    route = context.Route,
                    arrangementGroupId = context.PartId,
                    arrangementDisplayName = context.DisplayName,
                    difficultyLabel = variant.Context.DifficultyLabel,
                    difficultyUiIndex = variant.Context.DifficultyUiIndex,
                    hasDifficultyVariants = variant.Context.HasDifficultyVariants,
                    partFilePath = BuildStoredPath(targetDirectory, partFilePath),
                    noteCount = variant.Part.notes.Count,
                    tabCount = variant.Part.notes.Count,
                    score = ScoreArrangement(context.Route, variant.Part.notes.Count),
                    difficultyRating = variant.Part.difficultyRating,
                    tuningPitches = context.TuningPitches,
                    tuningDisplayName = context.TuningDisplayName
                });

                maxDifficultyRating = Math.Max(maxDifficultyRating, variant.Part.difficultyRating);
                maxDurationSeconds = Math.Max(maxDurationSeconds, variant.Part.durationSeconds);
            }
        }

        string? mainAudioPath = SelectPrimaryAudioPath(contentDirectory);
        string? previewAudioPath = SelectPreviewAudioPath(contentDirectory);
        string? artworkPath = ExtractArtworkPath(contentDirectory);
        string manifestPath = Path.Combine(targetDirectory, ManifestFileName);

        string title = firstArrangement?.MetaData?.Title ?? Path.GetFileNameWithoutExtension(psarcPath);
        string artist = firstArrangement?.MetaData?.ArtistName ?? string.Empty;
        string album = firstArrangement?.MetaData?.AlbumName ?? string.Empty;

        CachedSongManifest manifest = new CachedSongManifest
        {
            schemaVersion = SchemaVersion,
            sourcePsarcPath = psarcPath,
            sourcePsarcLastWriteUtcTicks = File.GetLastWriteTimeUtc(psarcPath).Ticks,
            importedAtUtcTicks = DateTime.UtcNow.Ticks,
            displayName = title,
            artist = artist,
            album = album,
            subtitle = string.IsNullOrWhiteSpace(artist) ? string.Empty : artist,
            artworkPath = BuildStoredPath(targetDirectory, artworkPath),
            audioPath = BuildStoredPath(targetDirectory, mainAudioPath),
            previewAudioPath = BuildStoredPath(targetDirectory, previewAudioPath),
            durationSeconds = maxDurationSeconds,
            difficultyRating = maxDifficultyRating,
            arrangements = arrangementSummaries
        };

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
        CleanupIntermediateFiles(contentDirectory);
    }

    private sealed class ConsoleProgressFunc : FSharpFunc<Unit, Unit>
    {
        public override Unit Invoke(Unit arg)
        {
            Console.WriteLine("[RocksmithImportTool] import stage advanced");
            return default;
        }
    }

    private static void ConvertExtractedAudio(string contentDirectory)
    {
        foreach (string wemPath in Directory.GetFiles(contentDirectory, "*.wem", SearchOption.TopDirectoryOnly))
        {
            if (File.Exists(Path.ChangeExtension(wemPath, ".ogg")))
                continue;

            Rocksmith2014.Audio.Conversion.wemToOgg(wemPath);
        }
    }

    private static CachedArrangementToneData ExtractArrangementTones(IReadOnlyList<string> manifestJsonPaths, string xmlPath, string arrangementName)
    {
        ManifestToneData manifestToneData = FindManifestToneData(manifestJsonPaths, arrangementName);
        CachedArrangementToneData tones = ParseXmlToneData(xmlPath, manifestToneData.IdNameMap);
        if (string.IsNullOrWhiteSpace(tones.baseToneName) &&
            manifestToneData.IdNameMap.TryGetValue(0, out string fallbackBaseTone) &&
            !string.IsNullOrWhiteSpace(fallbackBaseTone))
        {
            tones.baseToneName = fallbackBaseTone.Trim();
        }

        tones.definitions = manifestToneData.Definitions;
        return NormalizeToneData(tones);
    }

    private static ManifestToneData FindManifestToneData(IReadOnlyList<string> manifestJsonPaths, string arrangementName)
    {
        ManifestToneData result = new ManifestToneData();
        if (manifestJsonPaths == null || string.IsNullOrWhiteSpace(arrangementName))
            return result;

        string targetName = arrangementName.Trim();
        for (int fileIndex = 0; fileIndex < manifestJsonPaths.Count; fileIndex++)
        {
            string jsonPath = manifestJsonPaths[fileIndex];
            if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
                continue;

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(jsonPath));
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("Entries", out JsonElement entries) ||
                    entries.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (JsonProperty entryProperty in entries.EnumerateObject())
                {
                    JsonElement entry = entryProperty.Value;
                    if (entry.ValueKind != JsonValueKind.Object ||
                        !entry.TryGetProperty("Attributes", out JsonElement attributes) ||
                        attributes.ValueKind != JsonValueKind.Object ||
                        !TryGetJsonString(attributes, "ArrangementName", out string entryArrangementName) ||
                        !string.Equals(entryArrangementName.Trim(), targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    result.Definitions = ExtractToneDefinitions(attributes);
                    result.IdNameMap = ExtractToneIdNameMap(attributes);
                    return result;
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                Console.WriteLine($"[RocksmithImportTool] Skipping unparseable manifest JSON '{Path.GetFileName(jsonPath)}': {ex.Message}");
            }
        }

        return result;
    }

    private static List<CachedToneDefinitionData> ExtractToneDefinitions(JsonElement attributes)
    {
        List<CachedToneDefinitionData> definitions = new List<CachedToneDefinitionData>();
        if (!attributes.TryGetProperty("Tones", out JsonElement tonesElement) ||
            tonesElement.ValueKind != JsonValueKind.Array)
        {
            return definitions;
        }

        HashSet<string> seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement toneElement in tonesElement.EnumerateArray())
        {
            if (toneElement.ValueKind != JsonValueKind.Object)
                continue;

            TryGetJsonString(toneElement, "Key", out string key);
            if (!string.IsNullOrWhiteSpace(key) && !seenKeys.Add(key))
                continue;

            TryGetJsonString(toneElement, "Name", out string name);
            definitions.Add(new CachedToneDefinitionData
            {
                name = name ?? string.Empty,
                key = key ?? string.Empty,
                rawJson = toneElement.GetRawText()
            });
        }

        return definitions;
    }

    private static Dictionary<int, string> ExtractToneIdNameMap(JsonElement attributes)
    {
        Dictionary<int, string> idNameMap = new Dictionary<int, string>();
        string[] keys = { "Tone_A", "Tone_B", "Tone_C", "Tone_D" };
        for (int i = 0; i < keys.Length; i++)
        {
            if (TryGetJsonString(attributes, keys[i], out string toneName) && !string.IsNullOrWhiteSpace(toneName))
                idNameMap[i] = toneName.Trim();
        }

        return idNameMap;
    }

    private static CachedArrangementToneData ParseXmlToneData(string xmlPath, IReadOnlyDictionary<int, string> idNameMap)
    {
        CachedArrangementToneData result = new CachedArrangementToneData();
        if (string.IsNullOrWhiteSpace(xmlPath) || !File.Exists(xmlPath))
            return result;

        try
        {
            XDocument document = XDocument.Load(xmlPath);
            XElement root = document.Root;
            if (root == null || !string.Equals(root.Name.LocalName, "song", StringComparison.OrdinalIgnoreCase))
                return result;

            XElement toneBaseElement = root.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, "tonebase", StringComparison.OrdinalIgnoreCase));
            if (toneBaseElement != null && !string.IsNullOrWhiteSpace(toneBaseElement.Value))
                result.baseToneName = toneBaseElement.Value.Trim();

            XElement tonesElement = root.Elements().FirstOrDefault(element => string.Equals(element.Name.LocalName, "tones", StringComparison.OrdinalIgnoreCase));
            if (tonesElement == null)
                return result;

            foreach (XElement toneElement in tonesElement.Elements().Where(element => string.Equals(element.Name.LocalName, "tone", StringComparison.OrdinalIgnoreCase)))
            {
                string timeText = toneElement.Attribute("time")?.Value;
                if (!float.TryParse(timeText, NumberStyles.Float, CultureInfo.InvariantCulture, out float timeSeconds) ||
                    float.IsNaN(timeSeconds) ||
                    float.IsInfinity(timeSeconds))
                {
                    continue;
                }

                string idText = toneElement.Attribute("id")?.Value;
                int toneId = -1;
                if (!string.IsNullOrWhiteSpace(idText))
                    int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out toneId);

                string toneName = toneElement.Attribute("name")?.Value ?? string.Empty;
                if ((string.IsNullOrWhiteSpace(toneName) || string.Equals(toneName, "N/A", StringComparison.OrdinalIgnoreCase)) &&
                    toneId >= 0 &&
                    idNameMap != null &&
                    idNameMap.TryGetValue(toneId, out string mappedName))
                {
                    toneName = mappedName;
                }

                if (string.IsNullOrWhiteSpace(toneName))
                    continue;

                result.changes.Add(new CachedToneChangeData
                {
                    timeSeconds = MathF.Round(Math.Max(0f, timeSeconds), 3),
                    toneName = toneName.Trim(),
                    toneId = toneId
                });
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException)
        {
            Console.WriteLine($"[RocksmithImportTool] Failed to parse arrangement tones from '{Path.GetFileName(xmlPath)}': {ex.Message}");
        }

        return result;
    }

    private static CachedArrangementToneData NormalizeToneData(CachedArrangementToneData source)
    {
        CachedArrangementToneData normalized = new CachedArrangementToneData
        {
            baseToneName = source?.baseToneName?.Trim() ?? string.Empty,
            changes = source?.changes != null
                ? source.changes
                    .Where(change => change != null && !string.IsNullOrWhiteSpace(change.toneName))
                    .OrderBy(change => change.timeSeconds)
                    .Select(change => new CachedToneChangeData
                    {
                        timeSeconds = MathF.Round(Math.Max(0f, change.timeSeconds), 3),
                        toneName = change.toneName.Trim(),
                        toneId = change.toneId
                    })
                    .ToList()
                : new List<CachedToneChangeData>(),
            definitions = source?.definitions != null
                ? source.definitions
                    .Where(definition => definition != null && (!string.IsNullOrWhiteSpace(definition.name) || !string.IsNullOrWhiteSpace(definition.key) || !string.IsNullOrWhiteSpace(definition.rawJson)))
                    .Select(definition => new CachedToneDefinitionData
                    {
                        name = definition.name?.Trim() ?? string.Empty,
                        key = definition.key?.Trim() ?? string.Empty,
                        rawJson = definition.rawJson ?? string.Empty
                    })
                    .ToList()
                : new List<CachedToneDefinitionData>()
        };

        return normalized;
    }

    private static CachedArrangementToneData CloneToneData(CachedArrangementToneData source)
    {
        return NormalizeToneData(source ?? new CachedArrangementToneData());
    }

    private static bool TryGetJsonString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static CachedArrangementPart BuildPart(ArrangementVariantContext context)
    {
        CachedArrangementPart part = new CachedArrangementPart
        {
            schemaVersion = SchemaVersion,
            partId = context.PartId,
            displayName = context.DisplayName,
            route = context.Arrangement.Route,
            arrangementGroupId = context.Arrangement.PartId,
            arrangementDisplayName = context.Arrangement.DisplayName,
            difficultyLabel = context.DifficultyLabel,
            difficultyUiIndex = context.DifficultyUiIndex,
            hasDifficultyVariants = context.HasDifficultyVariants,
            durationSeconds = Math.Max(context.Arrangement.Arrangement.MetaData.SongLength / 1000f, GetLevelDurationSeconds(context.SourceLevel)),
            difficultyRating = CalculateDifficultyRating(context.SourceLevel, context.Arrangement.Arrangement.MetaData.SongLength / 1000f),
            tuningPitches = context.Arrangement.TuningPitches,
            tuningDisplayName = context.Arrangement.TuningDisplayName,
            timing = RocksmithImportTimingExporter.Build(context.Arrangement.Arrangement),
            tones = CloneToneData(context.Arrangement.Tones),
            generatedPart = new CachedGeneratedPartInfo
            {
                partId = context.PartId,
                displayName = context.DisplayName,
                instrumentName = context.Arrangement.Route,
                sourceMidiChannel = -1,
                sourceMidiProgram = string.Equals(context.Arrangement.Route, "Bass", StringComparison.OrdinalIgnoreCase) ? 33 : 29,
                preferredBank = -1,
                isDrum = false,
                isGuitarFamily = true,
                isExplicitHarmonicPart = false
            }
        };

        List<SourceEvent> events = BuildSourceEvents(context.SourceLevel, context.Arrangement.Arrangement.ChordTemplates, context.Arrangement.Arrangement.MetaData.Tuning);
        part.arpeggioGuides = BuildArpeggioGuides(context.SourceLevel, context.Arrangement.Arrangement.ChordTemplates);
        int noteId = 0;
        int chordId = 0;
        Dictionary<string, CachedNoteData> previousByStringRoute = new Dictionary<string, CachedNoteData>(StringComparer.OrdinalIgnoreCase);

        for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
        {
            SourceEvent sourceEvent = events[eventIndex];
            int assignedChordId = chordId++;

            foreach (SourceNote source in sourceEvent.Notes.OrderBy(note => note.String))
            {
                CachedNoteData note = BuildGameplayNote(source, context, noteId++, assignedChordId);
                ApplyLegatoLink(note, source, previousByStringRoute);
                part.notes.Add(note);
                part.generatedNotes.Add(BuildGeneratedNote(source, context, note));
                previousByStringRoute[note.stringIdx.ToString(CultureInfo.InvariantCulture)] = note;
            }
        }

        part.durationSeconds = Math.Max(part.durationSeconds, part.notes.Count > 0
            ? part.notes.Max(note => note.time + Math.Max(0.05f, note.duration))
            : 0f);

        return part;
    }

    private static List<CachedArpeggioGuideData> BuildArpeggioGuides(Level level, List<ChordTemplate> chordTemplates)
    {
        List<CachedArpeggioGuideData> guides = new List<CachedArpeggioGuideData>();
        if (level?.HandShapes == null || chordTemplates == null)
            return guides;

        for (int i = 0; i < level.HandShapes.Count; i++)
        {
            HandShape handShape = level.HandShapes[i];
            if (handShape == null)
                continue;

            if (handShape.ChordId < 0 || handShape.ChordId >= chordTemplates.Count)
                continue;

            ChordTemplate template = chordTemplates[handShape.ChordId];
            if (template == null || !template.IsArpeggio)
                continue;

            int[] stringFrets = new int[template.Frets.Length];
            int activeStrings = 0;
            for (int stringIndex = 0; stringIndex < template.Frets.Length; stringIndex++)
            {
                int fret = template.Frets[stringIndex];
                stringFrets[stringIndex] = fret;
                if (fret >= 0)
                    activeStrings++;
            }

            if (activeStrings < 2)
                continue;

            float startTime = handShape.StartTime / 1000f;
            float endTime = Math.Max(handShape.EndTime, handShape.StartTime) / 1000f;
            if (endTime <= startTime + 0.01f)
                endTime = startTime + 0.01f;

            guides.Add(new CachedArpeggioGuideData
            {
                id = guides.Count,
                startTime = startTime,
                endTime = endTime,
                chordName = ResolveChordDisplayName(template),
                stringFrets = stringFrets
            });
        }

        return guides;
    }

    private static List<ArrangementVariantBuildResult> BuildVariants(ArrangementContext context)
    {
        List<ArrangementVariantSource> sources = BuildVariantSources(context.Arrangement);
        if (sources.Count == 0)
        {
            ArrangementVariantContext fallbackContext = new ArrangementVariantContext
            {
                Arrangement = context,
                SourceLevel = ChooseExpertLevel(context.Arrangement),
                PartId = context.PartId,
                DisplayName = context.DisplayName,
                DifficultyLabel = "Full",
                DifficultyUiIndex = 0,
                HasDifficultyVariants = false
            };

            return new List<ArrangementVariantBuildResult>
            {
                new ArrangementVariantBuildResult
                {
                    Context = fallbackContext,
                    Part = BuildPart(fallbackContext)
                }
            };
        }

        bool hasDifficultyVariants = sources.Count > 1;
        ArrangementVariantBuildResult expertResult = null;
        List<ArrangementVariantBuildResult> nonExpertResults = new List<ArrangementVariantBuildResult>(Math.Max(0, sources.Count - 1));
        for (int i = 0; i < sources.Count; i++)
        {
            ArrangementVariantSource source = sources[i];
            ArrangementVariantContext variantContext = new ArrangementVariantContext
            {
                Arrangement = context,
                SourceLevel = source.Level,
                PartId = context.PartId,
                DisplayName = context.DisplayName,
                DifficultyLabel = string.Empty,
                DifficultyUiIndex = -1,
                HasDifficultyVariants = hasDifficultyVariants
            };

            CachedArrangementPart part = BuildPart(variantContext);
            ArrangementVariantBuildResult result = new ArrangementVariantBuildResult
            {
                Context = variantContext,
                Part = part
            };

            if (source.IsExpertSource)
                expertResult = result;
            else
                nonExpertResults.Add(result);
        }

        nonExpertResults = nonExpertResults
            .OrderByDescending(result => result?.Part?.notes?.Count ?? 0)
            .ThenByDescending(result => result?.Part?.generatedNotes?.Count ?? 0)
            .ThenByDescending(result => result?.Context?.SourceLevel?.Difficulty ?? int.MinValue)
            .ToList();

        List<ArrangementVariantBuildResult> results = new List<ArrangementVariantBuildResult>(sources.Count);
        if (expertResult != null)
            results.Add(expertResult);
        results.AddRange(nonExpertResults);

        for (int i = 0; i < results.Count; i++)
        {
            ArrangementVariantBuildResult result = results[i];
            ArrangementVariantContext variantContext = result.Context;
            int uiIndex = i;
            string difficultyLabel = GetDifficultyLabelForOrderedIndex(uiIndex, results.Count);
            string partId = uiIndex == 0
                ? context.PartId
                : $"{context.PartId}::level-{variantContext.SourceLevel.Difficulty:D3}-{uiIndex:D3}";
            string displayName = hasDifficultyVariants ? $"{context.DisplayName} - {difficultyLabel}" : context.DisplayName;

            variantContext.PartId = partId;
            variantContext.DisplayName = displayName;
            variantContext.DifficultyLabel = difficultyLabel;
            variantContext.DifficultyUiIndex = uiIndex;
            ApplyVariantMetadata(result.Part, variantContext);
        }

        return results;
    }

    private static void ApplyVariantMetadata(CachedArrangementPart part, ArrangementVariantContext context)
    {
        if (part == null || context == null)
            return;

        part.partId = context.PartId;
        part.displayName = context.DisplayName;
        part.route = context.Arrangement.Route;
        part.arrangementGroupId = context.Arrangement.PartId;
        part.arrangementDisplayName = context.Arrangement.DisplayName;
        part.difficultyLabel = context.DifficultyLabel;
        part.difficultyUiIndex = context.DifficultyUiIndex;
        part.hasDifficultyVariants = context.HasDifficultyVariants;

        if (part.generatedPart != null)
        {
            part.generatedPart.partId = context.PartId;
            part.generatedPart.displayName = context.DisplayName;
            part.generatedPart.instrumentName = context.Arrangement.Route;
        }

        if (part.generatedNotes == null)
            return;

        for (int i = 0; i < part.generatedNotes.Count; i++)
        {
            part.generatedNotes[i].partId = context.PartId;
            part.generatedNotes[i].partName = context.DisplayName;
        }
    }

    private static List<ArrangementVariantSource> BuildVariantSources(InstrumentalArrangement arrangement)
    {
        List<ArrangementVariantSource> results = new List<ArrangementVariantSource>();
        Level expertLevel = ChooseExpertLevel(arrangement);
        if (HasPlayableContent(expertLevel))
            results.Add(new ArrangementVariantSource { Level = expertLevel, IsExpertSource = true });

        List<Level> playableLevels = (arrangement.Levels ?? new List<Level>())
            .Where(HasPlayableContent)
            .OrderByDescending(level => level.Difficulty)
            .ToList();

        for (int i = 0; i < playableLevels.Count; i++)
            results.Add(new ArrangementVariantSource { Level = playableLevels[i] });

        return results;
    }

    private static string GetDifficultyLabelForOrderedIndex(int orderedIndex, int totalVariantCount)
    {
        if (orderedIndex <= 0 || totalVariantCount <= 1)
            return "Full";

        int numericLevel = Math.Max(1, totalVariantCount - orderedIndex);
        return numericLevel.ToString(CultureInfo.InvariantCulture);
    }

    private static string BuildPartSignature(CachedArrangementPart part)
    {
        HashCode hash = new HashCode();
        hash.Add(part?.notes?.Count ?? 0);
        if (part?.notes != null)
        {
            for (int i = 0; i < part.notes.Count; i++)
            {
                CachedNoteData note = part.notes[i];
                hash.Add((int)Math.Round(note.time * 1000f));
                hash.Add((int)Math.Round(note.duration * 1000f));
                hash.Add(note.stringIdx);
                hash.Add(note.fret);
                hash.Add(note.chordId);
                hash.Add(note.technique);
                hash.Add(note.slideTargetFret);
                hash.Add((int)Math.Round(note.bendStep * 100f));
            }
        }

        return hash.ToHashCode().ToString(CultureInfo.InvariantCulture);
    }

    private static Level ChooseExpertLevel(InstrumentalArrangement arrangement)
    {
        if (HasPlayableContent(arrangement.TranscriptionTrack))
            return arrangement.TranscriptionTrack!;

        if ((arrangement.Levels?.Count ?? 0) > 1)
        {
            try
            {
                Level generated = arrangement.GenerateTranscriptionTrack().GetAwaiter().GetResult();
                if (HasPlayableContent(generated))
                    return generated;
            }
            catch
            {
                // Fall through to best-effort level selection below.
            }
        }

        Level? bestLevel = arrangement.Levels?
            .Where(HasPlayableContent)
            .OrderByDescending(level => (level.Notes?.Count ?? 0) + (level.Chords?.Count ?? 0))
            .ThenByDescending(level => level.Difficulty)
            .FirstOrDefault();
        if (bestLevel != null)
            return bestLevel;

        return arrangement.TranscriptionTrack
               ?? arrangement.Levels?.OrderByDescending(level => level.Difficulty).FirstOrDefault()
               ?? new Level(0);
    }

    private static bool HasPlayableContent(Level? level)
    {
        return level != null &&
               ((level.Notes?.Count ?? 0) > 0 || (level.Chords?.Count ?? 0) > 0);
    }

    private static List<SourceEvent> BuildSourceEvents(Level level, List<ChordTemplate> chordTemplates, Tuning tuning)
    {
        List<SourceEvent> events = new List<SourceEvent>();

        if (level?.Notes != null)
        {
            for (int i = 0; i < level.Notes.Count; i++)
            {
                Note note = level.Notes[i];
                if (note == null || note.IsIgnore || !IsValidStringIndex(note.String))
                    continue;

                events.Add(new SourceEvent
                {
                    TimeMs = note.Time,
                    Notes = new List<SourceNote>
                    {
                        SourceNote.FromNote(note, false, false, false, string.Empty)
                    }
                });
            }
        }

        if (level?.Chords != null)
        {
            for (int i = 0; i < level.Chords.Count; i++)
            {
                Chord chord = level.Chords[i];
                if (chord == null || chord.IsIgnore)
                    continue;

                List<SourceNote> sourceNotes = new List<SourceNote>();
                if (chord.HasChordNotes)
                {
                    string chordDisplayName = ResolveChordDisplayName(chord, chordTemplates);
                    foreach (Note chordNote in chord.ChordNotes!)
                    {
                        if (chordNote == null || chordNote.IsIgnore || !IsValidStringIndex(chordNote.String))
                            continue;

                        sourceNotes.Add(SourceNote.FromNote(chordNote, chord.IsPalmMute, chord.IsFretHandMute, chord.IsHopo, chordDisplayName));
                    }
                }
                else if (chord.ChordId >= 0 && chord.ChordId < chordTemplates.Count)
                {
                    ChordTemplate template = chordTemplates[chord.ChordId];
                    string chordDisplayName = ResolveChordDisplayName(template);
                    for (int stringIndex = 0; stringIndex < template.Frets.Length; stringIndex++)
                    {
                        sbyte fret = template.Frets[stringIndex];
                        if (fret < 0)
                            continue;

                        sourceNotes.Add(SourceNote.FromTemplate(chord.Time, stringIndex, fret, chord.IsPalmMute, chord.IsFretHandMute, chord.IsHopo, chord.IsLinkNext, chordDisplayName));
                    }
                }

                if (sourceNotes.Count == 0)
                    continue;

                events.Add(new SourceEvent
                {
                    TimeMs = chord.Time,
                    Notes = sourceNotes
                });
            }
        }

        events.Sort((left, right) =>
        {
            int cmp = left.TimeMs.CompareTo(right.TimeMs);
            if (cmp != 0)
                return cmp;
            return right.Notes.Count.CompareTo(left.Notes.Count);
        });

        return events;
    }

    private static CachedNoteData BuildGameplayNote(SourceNote source, ArrangementVariantContext context, int noteId, int chordId)
    {
        float startSeconds = source.TimeMs / 1000f;
        float durationSeconds = Math.Max(0f, source.SustainMs / 1000f);
        float bendStep = source.BendValues.Count > 0
            ? source.BendValues.Max(value => Math.Abs(value.Step))
            : Math.Max(0f, source.MaxBend);
        List<CachedTechniqueSegmentData> segments = BuildTechniqueSegments(source, durationSeconds);
        bool hasBend = segments.Any(segment => segment.type == 1);
        float bendVisualStartTime = hasBend ? startSeconds : -1f;
        float bendVisualDuration = hasBend ? durationSeconds : 0f;

        return new CachedNoteData
        {
            id = noteId,
            time = startSeconds,
            duration = durationSeconds,
            stringIdx = source.String,
            fret = source.Fret,
            note = GetNoteName(ComputeMidiNote(context.Arrangement.TuningPitches, source.String, source.Fret)),
            chordId = chordId,
            chordName = source.ChordName,
            technique = DetermineGameplayTechnique(source),
            slideTargetFret = source.SlideTargetFret,
            bendStep = bendStep,
            bendVisualStartTime = bendVisualStartTime,
            bendVisualDuration = bendVisualDuration,
            bendPreBend = StartsWithPreBend(source.BendValues),
            bendRelease = HasBendRelease(source.BendValues),
            isMuted = source.IsPalmMute || source.IsFretHandMute,
            isPalmMute = source.IsPalmMute,
            isFretHandMute = source.IsFretHandMute,
            isHarmonic = source.IsHarmonic,
            isAccent = source.IsAccent,
            isTap = source.IsTap,
            isTremolo = source.IsTremolo,
            isPinchHarmonic = source.IsPinchHarmonic,
            isHammerOn = source.IsHammerOn,
            isPullOff = source.IsPullOff,
            isHopo = source.IsHopo,
            hasVibrato = source.HasVibrato,
            vibratoStrength = source.VibratoStrength,
            maxBend = source.MaxBend,
            isLegato = false,
            requiresPluck = true,
            linkedFromNoteId = -1,
            bendPoints = source.BendValues
                .Select(point => new CachedBendPointData
                {
                    timeSeconds = Math.Max(0f, point.TimeMs / 1000f),
                    step = point.Step
                })
                .ToList(),
            techniqueSegments = segments
        };
    }

    private static void ApplyLegatoLink(CachedNoteData note, SourceNote source, Dictionary<string, CachedNoteData> previousByStringRoute)
    {
        string key = source.String.ToString(CultureInfo.InvariantCulture);
        if (!previousByStringRoute.TryGetValue(key, out CachedNoteData? previous))
            return;

        if (!(source.SlideTargetFret >= 0 || source.IsHammerOn || source.IsPullOff || source.IsHopo))
            return;

        note.isLegato = true;
        note.requiresPluck = false;
        note.linkedFromNoteId = previous.id;

        if (note.technique == 0)
        {
            if (source.SlideTargetFret >= 0)
            {
                note.technique = 3;
            }
            else if (source.IsHammerOn)
            {
                note.technique = 1;
            }
            else if (source.IsPullOff)
            {
                note.technique = 2;
            }
            else if (source.IsHopo)
            {
                if (note.fret > previous.fret)
                    note.technique = 1;
                else if (note.fret < previous.fret)
                    note.technique = 2;
            }
        }
    }

    private static CachedGeneratedNoteEvent BuildGeneratedNote(SourceNote source, ArrangementVariantContext context, CachedNoteData note)
    {
        List<CachedGeneratedPitchPoint> pitchCurve = BuildPitchCurve(source, note.duration);
        int pitchBendRange = 0;
        for (int i = 0; i < pitchCurve.Count; i++)
            pitchBendRange = Math.Max(pitchBendRange, (int)Math.Ceiling(Math.Abs(pitchCurve[i].semitoneOffset)));

        bool usesBendDrivenVibrato = UsesBendDrivenVibrato(source);
        float genericVibratoDepth = !usesBendDrivenVibrato && source.HasVibrato
            ? ResolveRocksmithVibratoDepthSemitones(source.VibratoStrength, bendDriven: false)
            : 0f;

        return new CachedGeneratedNoteEvent
        {
            startTimeSeconds = note.time,
            durationSeconds = Math.Max(0.05f, note.duration),
            pitchPreRollSeconds = StartsWithPreBend(source.BendValues) ? 0f : 0f,
            midiNote = ComputeMidiNote(context.Arrangement.TuningPitches, source.String, source.Fret),
            velocity = source.IsPalmMute || source.IsFretHandMute ? 86 : 112,
            channel = 0,
            partId = context.PartId,
            partName = context.DisplayName,
            techniqueVariant = DetermineGeneratedTechniqueVariant(source),
            legatoTransitionKind = DetermineGeneratedLegatoTransitionKind(source),
            attackVelocityScale = source.IsPalmMute || source.IsFretHandMute ? 0.82f : 1f,
            vibratoDepthSemitones = genericVibratoDepth,
            vibratoRateHz = genericVibratoDepth > 0.01f ? RocksmithVibratoCyclesPerSecond : 0f,
            vibratoDelayNormalized = genericVibratoDepth > 0.01f ? 0.05f : 0f,
            vibratoFadeNormalized = genericVibratoDepth > 0.01f ? 0.35f : 0f,
            pitchBendRangeSemitones = pitchBendRange,
            pitchCurve = pitchCurve
        };
    }

    private static List<CachedTechniqueSegmentData> BuildTechniqueSegments(SourceNote source, float durationSeconds)
    {
        List<CachedTechniqueSegmentData> segments = new List<CachedTechniqueSegmentData>();
        if (durationSeconds > 0.35f)
        {
            segments.Add(new CachedTechniqueSegmentData
            {
                type = 2,
                startOffset = 0f,
                endOffset = durationSeconds,
                startFret = source.Fret,
                endFret = source.Fret,
                startBend = 0f,
                endBend = 0f
            });
        }

        if (source.SlideTargetFret >= 0)
        {
            segments.Add(new CachedTechniqueSegmentData
            {
                type = 0,
                startOffset = 0f,
                endOffset = Math.Max(0.05f, durationSeconds > 0f ? durationSeconds : 0.15f),
                startFret = source.Fret,
                endFret = source.SlideTargetFret,
                startBend = 0f,
                endBend = 0f
            });
        }

        if (source.BendValues.Count > 0)
        {
            BendPoint firstPoint = source.BendValues[0];
            float firstPointTime = Math.Clamp(firstPoint.TimeMs / 1000f, 0f, Math.Max(durationSeconds, 0.001f));
            bool startsWithPreBend = StartsWithPreBend(source.BendValues);
            if (firstPointTime > 0.0001f && Math.Abs(firstPoint.Step) > 0.01f)
            {
                segments.Add(new CachedTechniqueSegmentData
                {
                    type = 1,
                    startOffset = 0f,
                    endOffset = firstPointTime,
                    startFret = source.Fret,
                    endFret = source.Fret,
                    startBend = startsWithPreBend ? firstPoint.Step : 0f,
                    endBend = firstPoint.Step
                });
            }

            for (int i = 1; i < source.BendValues.Count; i++)
            {
                BendPoint previous = source.BendValues[i - 1];
                BendPoint current = source.BendValues[i];
                float startOffset = Math.Clamp(previous.TimeMs / 1000f, 0f, Math.Max(durationSeconds, 0.001f));
                float endOffset = Math.Clamp(current.TimeMs / 1000f, 0f, Math.Max(durationSeconds, 0.001f));
                if (endOffset <= startOffset + 0.0001f)
                    continue;

                bool useBendDrivenVibrato =
                    source.HasVibrato &&
                    IsFlatBendHold(previous, current) &&
                    (endOffset - startOffset) >= RocksmithBendDrivenVibratoMinimumHoldSeconds;
                segments.Add(new CachedTechniqueSegmentData
                {
                    type = useBendDrivenVibrato ? 3 : 1,
                    startOffset = startOffset,
                    endOffset = endOffset,
                    startFret = source.Fret,
                    endFret = source.Fret,
                    startBend = previous.Step,
                    endBend = current.Step
                });
            }

            if (source.HasVibrato &&
                source.BendValues.Count == 1 &&
                Math.Abs(source.BendValues[0].Step) > 0.01f &&
                Math.Max(0f, durationSeconds) >= RocksmithBendDrivenVibratoMinimumHoldSeconds)
            {
                segments.Add(new CachedTechniqueSegmentData
                {
                    type = 3,
                    startOffset = 0f,
                    endOffset = Math.Max(0.1f, durationSeconds),
                    startFret = source.Fret,
                    endFret = source.Fret,
                    startBend = source.BendValues[0].Step,
                    endBend = source.BendValues[0].Step
                });
            }
        }

        if (source.HasVibrato && !UsesBendDrivenVibrato(source))
        {
            segments.Add(new CachedTechniqueSegmentData
            {
                type = 3,
                startOffset = 0f,
                endOffset = Math.Max(0.1f, durationSeconds),
                startFret = source.Fret,
                endFret = source.Fret,
                startBend = 0f,
                endBend = 0f
            });
        }

        return segments;
    }

    private static List<CachedGeneratedPitchPoint> BuildPitchCurve(SourceNote source, float durationSeconds)
    {
        List<CachedGeneratedPitchPoint> curve = new List<CachedGeneratedPitchPoint>
        {
            new CachedGeneratedPitchPoint { normalizedTime = 0f, semitoneOffset = 0f }
        };

        if (source.BendValues.Count > 0 && durationSeconds > 0.0001f)
        {
            if (StartsWithPreBend(source.BendValues) && source.BendValues[0].TimeMs > 0)
            {
                curve[0].semitoneOffset = source.BendValues[0].Step;
            }

            for (int i = 0; i < source.BendValues.Count; i++)
            {
                BendPoint point = source.BendValues[i];
                AddOrReplacePitchPoint(
                    curve,
                    Math.Clamp(point.TimeMs / 1000f / Math.Max(durationSeconds, 0.0001f), 0f, 1f),
                    point.Step);

                if (source.HasVibrato && i + 1 < source.BendValues.Count)
                {
                    BendPoint nextPoint = source.BendValues[i + 1];
                    AppendBendDrivenVibratoPoints(curve, source, point, nextPoint, durationSeconds);
                }
            }

            if (UsesBendDrivenVibrato(source) &&
                source.BendValues.Count == 1 &&
                Math.Abs(source.BendValues[0].Step) > 0.01f)
            {
                AppendBendDrivenVibratoHold(
                    curve,
                    source,
                    0f,
                    1f,
                    source.BendValues[0].Step,
                    durationSeconds);
            }
        }

        if (curve[^1].normalizedTime < 1f)
        {
            curve.Add(new CachedGeneratedPitchPoint
            {
                normalizedTime = 1f,
                semitoneOffset = curve[^1].semitoneOffset
            });
        }

        return curve;
    }

    private static void AppendBendDrivenVibratoPoints(
        List<CachedGeneratedPitchPoint> curve,
        SourceNote source,
        BendPoint start,
        BendPoint end,
        float durationSeconds)
    {
        if (!source.HasVibrato || !UsesBendDrivenVibrato(source))
            return;

        if (!IsFlatBendHold(start, end))
            return;

        float spanSeconds = Math.Max(0f, (end.TimeMs - start.TimeMs) / 1000f);
        if (spanSeconds < RocksmithBendDrivenVibratoMinimumHoldSeconds || durationSeconds <= 0.0001f)
            return;
        float startNormalized = Math.Clamp(start.TimeMs / 1000f / durationSeconds, 0f, 1f);
        float endNormalized = Math.Clamp(end.TimeMs / 1000f / durationSeconds, 0f, 1f);
        AppendBendDrivenVibratoHold(curve, source, startNormalized, endNormalized, start.Step, durationSeconds);
    }

    private static void AddOrReplacePitchPoint(List<CachedGeneratedPitchPoint> curve, float normalizedTime, float semitoneOffset)
    {
        if (curve.Count > 0 && Math.Abs(curve[curve.Count - 1].normalizedTime - normalizedTime) <= 0.0005f)
        {
            curve[curve.Count - 1].normalizedTime = normalizedTime;
            curve[curve.Count - 1].semitoneOffset = semitoneOffset;
            return;
        }

        curve.Add(new CachedGeneratedPitchPoint
        {
            normalizedTime = normalizedTime,
            semitoneOffset = semitoneOffset
        });
    }

    private static bool UsesBendDrivenVibrato(SourceNote source)
    {
        return source != null &&
               source.HasVibrato &&
               source.BendValues != null &&
               source.BendValues.Count > 0 &&
               (source.BendValues.Count > 1 || Math.Abs(source.BendValues[0].Step) > 0.01f);
    }

    private static void AppendBendDrivenVibratoHold(
        List<CachedGeneratedPitchPoint> curve,
        SourceNote source,
        float startNormalized,
        float endNormalized,
        float baseline,
        float durationSeconds)
    {
        float spanNormalized = endNormalized - startNormalized;
        if (spanNormalized <= 0.0001f || durationSeconds <= 0.0001f)
            return;

        float amplitude = ResolveRocksmithVibratoDepthSemitones(source.VibratoStrength, bendDriven: true);
        if (amplitude <= 0.01f)
            return;

        float spanSeconds = spanNormalized * durationSeconds;
        int halfWaves = Math.Clamp(Math.Max(4, (int)Math.Round(spanSeconds * RocksmithVibratoCyclesPerSecond * 2f)), 4, 16);
        for (int index = 1; index < halfWaves; index++)
        {
            float normalizedT = index / (float)halfWaves;
            float pointTime = startNormalized + (spanNormalized * normalizedT);
            float offset = baseline + (MathF.Sin(normalizedT * MathF.PI * 2f) * amplitude);
            AddOrReplacePitchPoint(curve, pointTime, offset);
        }

        AddOrReplacePitchPoint(curve, endNormalized, baseline);
    }

    private static bool IsFlatBendHold(BendPoint start, BendPoint end)
    {
        return Math.Abs(start.Step - end.Step) <= 0.01f && end.TimeMs > start.TimeMs;
    }

    private static float ResolveRocksmithVibratoDepthSemitones(int rawStrength, bool bendDriven)
    {
        if (rawStrength >= 110)
            return bendDriven ? 0.18f : 0.26f;
        if (rawStrength >= 70)
            return bendDriven ? 0.15f : 0.22f;
        if (rawStrength > 0)
            return bendDriven ? 0.12f : 0.18f;
        return bendDriven ? 0.15f : 0.22f;
    }

    private static int DetermineGameplayTechnique(SourceNote source)
    {
        if (source.BendValues.Count > 0 || source.MaxBend > 0.01f)
            return 4;
        if (source.SlideTargetFret >= 0)
            return 3;
        if (source.IsHammerOn)
            return 1;
        if (source.IsPullOff)
            return 2;
        if (source.HasVibrato)
            return 5;
        return 0;
    }

    private static int DetermineGeneratedTechniqueVariant(SourceNote source)
    {
        if (source.IsPalmMute)
            return 1;
        if (source.IsFretHandMute)
            return 2;
        if (source.IsHarmonic)
            return 3;
        return 0;
    }

    private static int DetermineGeneratedLegatoTransitionKind(SourceNote source)
    {
        if (source.SlideTargetFret >= 0)
            return 1;
        if (source.IsHammerOn)
            return 2;
        if (source.IsPullOff)
            return 3;
        return 0;
    }

    private static int ComputeMidiNote(int[] tuningPitches, int stringIndex, int fret)
    {
        int basePitch = tuningPitches != null && stringIndex >= 0 && stringIndex < tuningPitches.Length
            ? tuningPitches[stringIndex]
            : StandardGuitarTuning[Math.Clamp(stringIndex, 0, StandardGuitarTuning.Length - 1)];
        return basePitch + fret;
    }

    private static string GetNoteName(int midiNote)
    {
        return NoteNames[Math.Abs(midiNote) % 12];
    }

    private static string ResolveChordDisplayName(Chord chord, List<ChordTemplate> chordTemplates)
    {
        if (chord == null || chordTemplates == null)
            return string.Empty;

        if (chord.ChordId < 0 || chord.ChordId >= chordTemplates.Count)
            return string.Empty;

        return ResolveChordDisplayName(chordTemplates[chord.ChordId]);
    }

    private static string ResolveChordDisplayName(ChordTemplate template)
    {
        if (template == null)
            return string.Empty;

        string candidate = string.IsNullOrWhiteSpace(template.DisplayName)
            ? template.Name
            : template.DisplayName;

        if (string.IsNullOrWhiteSpace(candidate))
            return string.Empty;

        return candidate
            .Replace("min", "m", StringComparison.OrdinalIgnoreCase)
            .Replace("CONV", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-nop", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-arp", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static bool IsValidStringIndex(int stringIndex)
    {
        return stringIndex >= 0 && stringIndex < 6;
    }

    private static bool StartsWithPreBend(List<BendPoint> bendValues)
    {
        return bendValues.Count > 0 && bendValues[0].TimeMs <= 1 && Math.Abs(bendValues[0].Step) > 0.01f;
    }

    private static bool HasBendRelease(List<BendPoint> bendValues)
    {
        for (int i = 1; i < bendValues.Count; i++)
        {
            if (bendValues[i].Step < bendValues[i - 1].Step - 0.01f)
                return true;
        }

        return false;
    }

    private static int CalculateDifficultyRating(Level level, float durationSeconds)
    {
        int noteCount = (level?.Notes?.Count ?? 0) + (level?.Chords?.Count ?? 0);
        float safeDuration = Math.Max(1f, durationSeconds);
        float density = noteCount / safeDuration;
        if (density < 0.5f) return 1;
        if (density < 1.25f) return 2;
        if (density < 2.25f) return 3;
        if (density < 3.5f) return 4;
        return 5;
    }

    private static int ScoreArrangement(string route, int noteCount)
    {
        int score = noteCount * 2;
        if (route.Contains("Lead", StringComparison.OrdinalIgnoreCase))
            score += 160;
        else if (route.Contains("Rhythm", StringComparison.OrdinalIgnoreCase))
            score += 120;
        else if (route.Contains("Bass", StringComparison.OrdinalIgnoreCase))
            score += 60;
        else if (route.Contains("Combo", StringComparison.OrdinalIgnoreCase))
            score += 90;
        return score;
    }

    private static float GetLevelDurationSeconds(Level level)
    {
        float maxSeconds = 0f;
        if (level?.Notes != null)
        {
            for (int i = 0; i < level.Notes.Count; i++)
            {
                Note note = level.Notes[i];
                maxSeconds = Math.Max(maxSeconds, (note.Time + Math.Max(note.Sustain, 50)) / 1000f);
            }
        }

        if (level?.Chords != null)
        {
            for (int i = 0; i < level.Chords.Count; i++)
            {
                Chord chord = level.Chords[i];
                maxSeconds = Math.Max(maxSeconds, chord.Time / 1000f);
                if (chord.ChordNotes != null)
                {
                    for (int j = 0; j < chord.ChordNotes.Count; j++)
                    {
                        Note note = chord.ChordNotes[j];
                        maxSeconds = Math.Max(maxSeconds, (chord.Time + Math.Max(note.Sustain, 50)) / 1000f);
                    }
                }
            }
        }

        return maxSeconds;
    }

    private static string? SelectPrimaryAudioPath(string contentDirectory)
    {
        return Directory.GetFiles(contentDirectory, "*.ogg", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains("preview", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string? SelectPreviewAudioPath(string contentDirectory)
    {
        return Directory.GetFiles(contentDirectory, "*.ogg", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileNameWithoutExtension(path).Contains("preview", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string? ExtractArtworkPath(string contentDirectory)
    {
        string ddsPath = Path.Combine(contentDirectory, "cover.dds");
        if (!File.Exists(ddsPath))
            return null;

        string pngPath = Path.Combine(contentDirectory, "cover.png");
        try
        {
            using MagickImage image = new MagickImage(ddsPath);
            image.Write(pngPath, MagickFormat.Png);
            return pngPath;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[RocksmithImportTool] Failed to convert cover art '{ddsPath}': {ex.Message}");
            return null;
        }
    }

    private static void CleanupIntermediateFiles(string contentDirectory)
    {
        string[] cleanupPatterns =
        {
            "*.wem",
            "*.xml",
            "*.rs2dlc",
            "*.dds"
        };

        foreach (string pattern in cleanupPatterns)
        {
            foreach (string path in Directory.GetFiles(contentDirectory, pattern, SearchOption.TopDirectoryOnly))
            {
                File.Delete(path);
            }
        }
    }

    private static void ResetContentDirectory(string contentDirectory)
    {
        if (Directory.Exists(contentDirectory))
            Directory.Delete(contentDirectory, true);

        Directory.CreateDirectory(contentDirectory);
    }

    private static string SanitizeFileName(string value)
    {
        StringBuilder builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        string sanitized = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "arrangement" : sanitized;
    }

    private sealed class ArrangementContext
    {
        public required InstrumentalArrangement Arrangement;
        public required string Route;
        public required string DisplayName;
        public required string PartId;
        public required int[] TuningPitches;
        public required string TuningDisplayName;
        public CachedArrangementToneData Tones = new CachedArrangementToneData();

        public static ArrangementContext From(InstrumentalArrangement arrangement, string xmlPath, int index)
        {
            string route = string.IsNullOrWhiteSpace(arrangement.MetaData.Arrangement)
                ? InferRouteFromFileName(xmlPath)
                : arrangement.MetaData.Arrangement!;
            short partNumber = arrangement.MetaData.Part <= 0 ? (short)1 : arrangement.MetaData.Part;
            string displayName = partNumber > 1 ? $"{route} {partNumber}" : route;
            int[] tuningPitches = BuildTuningPitches(route, arrangement.MetaData.Tuning);
            return new ArrangementContext
            {
                Arrangement = arrangement,
                Route = route,
                DisplayName = displayName,
                PartId = $"{route.ToLowerInvariant()}::{partNumber}",
                TuningPitches = tuningPitches,
                TuningDisplayName = FormatTuningDisplayName(tuningPitches),
                Tones = new CachedArrangementToneData()
            };
        }

        private static string InferRouteFromFileName(string xmlPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(xmlPath) ?? string.Empty;
            if (fileName.Contains("bass", StringComparison.OrdinalIgnoreCase))
                return "Bass";
            if (fileName.Contains("rhythm", StringComparison.OrdinalIgnoreCase))
                return "Rhythm";
            if (fileName.Contains("combo", StringComparison.OrdinalIgnoreCase))
                return "Combo";
            return "Lead";
        }
    }

    private sealed class ArrangementVariantSource
    {
        public required Level Level;
        public bool IsExpertSource;
    }

    private sealed class ArrangementVariantContext
    {
        public required ArrangementContext Arrangement;
        public required Level SourceLevel;
        public required string PartId;
        public required string DisplayName;
        public required string DifficultyLabel;
        public int DifficultyUiIndex;
        public bool HasDifficultyVariants;
    }

    private sealed class ArrangementVariantBuildResult
    {
        public required ArrangementVariantContext Context;
        public required CachedArrangementPart Part;
    }

    private sealed class SourceEvent
    {
        public int TimeMs;
        public List<SourceNote> Notes = new List<SourceNote>();
    }

    private sealed class SourceNote
    {
        public int TimeMs;
        public int SustainMs;
        public int String;
        public int Fret;
        public string ChordName = string.Empty;
        public int SlideTargetFret = -1;
        public bool IsPalmMute;
        public bool IsFretHandMute;
        public bool IsAccent;
        public bool IsTap;
        public bool IsTremolo;
        public bool IsHammerOn;
        public bool IsPullOff;
        public bool IsHopo;
        public bool IsHarmonic;
        public bool IsPinchHarmonic;
        public bool HasVibrato;
        public int VibratoStrength;
        public float MaxBend;
        public List<BendPoint> BendValues = new List<BendPoint>();

        public static SourceNote FromNote(Note note, bool chordPalmMute, bool chordFretHandMute, bool chordHopo, string chordName)
        {
            return new SourceNote
            {
                TimeMs = note.Time,
                SustainMs = note.Sustain,
                String = note.String,
                Fret = note.Fret,
                ChordName = chordName ?? string.Empty,
                SlideTargetFret = note.SlideTo >= 0 ? note.SlideTo : note.SlideUnpitchTo >= 0 ? note.SlideUnpitchTo : -1,
                IsPalmMute = note.IsPalmMute || chordPalmMute,
                IsFretHandMute = note.IsFretHandMute || chordFretHandMute,
                IsAccent = note.IsAccent,
                IsTap = note.IsTap,
                IsTremolo = note.IsTremolo,
                IsHammerOn = note.IsHammerOn || chordHopo,
                IsPullOff = note.IsPullOff,
                IsHopo = note.IsHopo || chordHopo,
                IsHarmonic = note.IsHarmonic || note.IsPinchHarmonic,
                IsPinchHarmonic = note.IsPinchHarmonic,
                HasVibrato = note.Vibrato > 0,
                VibratoStrength = note.Vibrato,
                MaxBend = note.MaxBend,
                BendValues = BuildBendPoints(note.BendValues, note.Time)
            };
        }

        public static SourceNote FromTemplate(int timeMs, int stringIndex, int fret, bool palmMute, bool fretHandMute, bool hopo, bool linkNext, string chordName)
        {
            return new SourceNote
            {
                TimeMs = timeMs,
                SustainMs = 0,
                String = stringIndex,
                Fret = fret,
                ChordName = chordName ?? string.Empty,
                SlideTargetFret = -1,
                IsPalmMute = palmMute,
                IsFretHandMute = fretHandMute,
                IsAccent = false,
                IsTap = false,
                IsTremolo = false,
                IsHammerOn = hopo && linkNext,
                IsPullOff = false,
                IsHopo = hopo,
                IsHarmonic = false,
                IsPinchHarmonic = false,
                HasVibrato = false,
                VibratoStrength = 0,
                MaxBend = 0f,
                BendValues = new List<BendPoint>()
            };
        }

        private static List<BendPoint> BuildBendPoints(List<BendValue>? bendValues, int noteTimeMs)
        {
            List<BendPoint> result = new List<BendPoint>();
            if (bendValues == null)
                return result;

            for (int i = 0; i < bendValues.Count; i++)
            {
                int relativeTimeMs = Math.Max(0, bendValues[i].Time - noteTimeMs);
                result.Add(new BendPoint { TimeMs = relativeTimeMs, Step = bendValues[i].Step });
            }

            return result;
        }
    }

    private static void ImproveArrangementForImport(InstrumentalArrangement arrangement)
    {
        if (arrangement == null)
            return;

        BasicFixes.fixLinkNexts(arrangement);
        BasicFixes.removeOverlappingBendValues(arrangement);
    }

    private sealed class BendPoint
    {
        public int TimeMs;
        public float Step;
    }

    private sealed class CachedSongManifest
    {
        public int schemaVersion;
        public string sourcePsarcPath = string.Empty;
        public long sourcePsarcLastWriteUtcTicks;
        public long importedAtUtcTicks;
        public string displayName = string.Empty;
        public string artist = string.Empty;
        public string album = string.Empty;
        public string subtitle = string.Empty;
        public string artworkPath = string.Empty;
        public string audioPath = string.Empty;
        public string previewAudioPath = string.Empty;
        public float durationSeconds;
        public int difficultyRating;
        public List<CachedArrangementSummary> arrangements = new List<CachedArrangementSummary>();
    }

    private sealed class CachedArrangementSummary
    {
        public string partId = string.Empty;
        public string displayName = string.Empty;
        public string route = string.Empty;
        public string arrangementGroupId = string.Empty;
        public string arrangementDisplayName = string.Empty;
        public string difficultyLabel = string.Empty;
        public int difficultyUiIndex = -1;
        public bool hasDifficultyVariants;
        public string partFilePath = string.Empty;
        public int noteCount;
        public int tabCount;
        public int score;
        public int difficultyRating;
        public int[]? tuningPitches;
        public string tuningDisplayName = string.Empty;
    }

    private sealed class CachedArrangementPart
    {
        public int schemaVersion;
        public string partId = string.Empty;
        public string displayName = string.Empty;
        public string route = string.Empty;
        public string arrangementGroupId = string.Empty;
        public string arrangementDisplayName = string.Empty;
        public string difficultyLabel = string.Empty;
        public int difficultyUiIndex = -1;
        public bool hasDifficultyVariants;
        public float durationSeconds;
        public int difficultyRating;
        public int[]? tuningPitches;
        public string tuningDisplayName = string.Empty;
        public CachedArrangementTimingData timing = new CachedArrangementTimingData();
        public CachedArrangementToneData tones = new CachedArrangementToneData();
        public CachedGeneratedPartInfo generatedPart = new CachedGeneratedPartInfo();
        public List<CachedNoteData> notes = new List<CachedNoteData>();
        public List<CachedArpeggioGuideData> arpeggioGuides = new List<CachedArpeggioGuideData>();
        public List<CachedGeneratedNoteEvent> generatedNotes = new List<CachedGeneratedNoteEvent>();
    }

    private sealed class CachedArrangementToneData
    {
        public string baseToneName = string.Empty;
        public List<CachedToneChangeData> changes = new List<CachedToneChangeData>();
        public List<CachedToneDefinitionData> definitions = new List<CachedToneDefinitionData>();
    }

    private sealed class CachedToneChangeData
    {
        public float timeSeconds;
        public string toneName = string.Empty;
        public int toneId = -1;
    }

    private sealed class CachedToneDefinitionData
    {
        public string name = string.Empty;
        public string key = string.Empty;
        public string rawJson = string.Empty;
    }

    private sealed class ManifestToneData
    {
        public List<CachedToneDefinitionData> Definitions = new List<CachedToneDefinitionData>();
        public Dictionary<int, string> IdNameMap = new Dictionary<int, string>();
    }

    private sealed class CachedGeneratedPartInfo
    {
        public string partId = string.Empty;
        public string displayName = string.Empty;
        public string instrumentName = string.Empty;
        public int sourceMidiChannel = -1;
        public int sourceMidiProgram = 29;
        public int preferredBank = -1;
        public bool isDrum;
        public bool isGuitarFamily = true;
        public bool isExplicitHarmonicPart;
    }

    private sealed class CachedNoteData
    {
        public int id;
        public float time;
        public float duration;
        public int stringIdx;
        public int fret;
        public string note = string.Empty;
        public int chordId;
        public string chordName = string.Empty;
        public int technique;
        public int slideTargetFret = -1;
        public float bendStep;
        public float bendVisualStartTime = -1f;
        public float bendVisualDuration;
        public bool bendPreBend;
        public bool bendRelease;
        public bool isMuted;
        public bool isPalmMute;
        public bool isFretHandMute;
        public bool isHarmonic;
        public bool isAccent;
        public bool isTap;
        public bool isTremolo;
        public bool isPinchHarmonic;
        public bool isHammerOn;
        public bool isPullOff;
        public bool isHopo;
        public bool hasVibrato;
        public int vibratoStrength;
        public float maxBend;
        public bool isLegato;
        public bool requiresPluck = true;
        public int linkedFromNoteId = -1;
        public List<CachedBendPointData> bendPoints = new List<CachedBendPointData>();
        public List<CachedTechniqueSegmentData> techniqueSegments = new List<CachedTechniqueSegmentData>();
    }

    private sealed class CachedBendPointData
    {
        public float timeSeconds;
        public float step;
    }

    private sealed class CachedTechniqueSegmentData
    {
        public int type;
        public float startOffset;
        public float endOffset;
        public int startFret;
        public int endFret;
        public float startBend;
        public float endBend;
    }

    private sealed class CachedArpeggioGuideData
    {
        public int id;
        public float startTime;
        public float endTime;
        public string chordName = string.Empty;
        public int[] stringFrets = new int[0];
    }

    private sealed class CachedGeneratedNoteEvent
    {
        public float startTimeSeconds;
        public float durationSeconds;
        public float pitchPreRollSeconds;
        public int midiNote;
        public int velocity;
        public int channel;
        public string partId = string.Empty;
        public string partName = string.Empty;
        public int techniqueVariant;
        public int legatoTransitionKind;
        public float attackVelocityScale = 1f;
        public float vibratoDepthSemitones;
        public float vibratoRateHz;
        public float vibratoDelayNormalized;
        public float vibratoFadeNormalized;
        public int pitchBendRangeSemitones;
        public List<CachedGeneratedPitchPoint> pitchCurve = new List<CachedGeneratedPitchPoint>();
    }

    private sealed class CachedGeneratedPitchPoint
    {
        public float normalizedTime;
        public float semitoneOffset;
    }

    private static int[] BuildTuningPitches(string route, Tuning tuning)
    {
        bool isBassRoute = !string.IsNullOrWhiteSpace(route) && route.Contains("Bass", StringComparison.OrdinalIgnoreCase);
        int[] baseTuning = isBassRoute ? StandardBassTuning : StandardGuitarTuning;
        int stringCount = Math.Min(baseTuning.Length, tuning?.Strings?.Length ?? 0);
        if (stringCount <= 0)
            return (int[])baseTuning.Clone();

        int[] pitches = new int[stringCount];
        for (int i = 0; i < pitches.Length; i++)
            pitches[i] = baseTuning[i] + tuning.Strings[i];
        return pitches;
    }

    private static string FormatTuningDisplayName(int[] tuningPitches)
    {
        if (Matches(tuningPitches, new[] { 40, 45, 50, 55, 59, 64 })) return "E Standard";
        if (Matches(tuningPitches, new[] { 39, 44, 49, 54, 58, 63 })) return "Eb Standard";
        if (Matches(tuningPitches, new[] { 38, 43, 48, 53, 57, 62 })) return "D Standard";
        if (Matches(tuningPitches, new[] { 38, 45, 50, 55, 59, 64 })) return "Drop D";
        if (Matches(tuningPitches, new[] { 37, 44, 49, 54, 58, 63 })) return "Drop Db";
        if (Matches(tuningPitches, new[] { 36, 43, 48, 53, 57, 62 })) return "Drop C";
        if (Matches(tuningPitches, new[] { 28, 33, 38, 43 })) return "E Standard Bass";
        if (Matches(tuningPitches, new[] { 27, 32, 37, 42 })) return "Eb Standard Bass";
        if (Matches(tuningPitches, new[] { 26, 31, 36, 41 })) return "D Standard Bass";
        if (Matches(tuningPitches, new[] { 26, 33, 38, 43 })) return "Drop D Bass";
        if (Matches(tuningPitches, new[] { 25, 32, 37, 42 })) return "Drop Db Bass";
        if (Matches(tuningPitches, new[] { 24, 31, 36, 41 })) return "Drop C Bass";
        return $"Custom ({string.Join(" ", tuningPitches.Select(GetNoteName))})";
    }

    private static string BuildStoredPath(string targetDirectory, string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return string.Empty;

        return Path.GetRelativePath(targetDirectory, absolutePath);
    }

    private static bool Matches(int[] left, int[] right)
    {
        if (left.Length != right.Length)
            return false;

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }
}
