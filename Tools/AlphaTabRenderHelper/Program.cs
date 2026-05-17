using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlphaTab;
using AlphaTab.Core.EcmaScript;
using AlphaTab.Importer;
using AlphaTab.Midi;
using AlphaTab.Model;
using AlphaTab.Rendering;
using AlphaTab.Rendering.Utils;
using AlphaTabColor = AlphaTab.Model.Color;

namespace AlphaTabRenderHelper;

internal static class Program
{
    private const bool UseGameplayAlphaTabVoiceStyling = true;
    private const bool UseGameplayAlphaTabTiedBendCleanup = true;
    private const int RenderManifestVersion = 12;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        IncludeFields = true,
        WriteIndented = true
    };

    private static int Main(string[] args)
    {
        if (!TryParseArguments(args, out string requestPath, out string responsePath, out string error))
        {
            Console.Error.WriteLine(error);
            return 2;
        }

        AlphaTabRenderResponse response;
        try
        {
            AlphaTabRenderRequest request = LoadJson<AlphaTabRenderRequest>(requestPath);
            response = Render(request);
        }
        catch (Exception ex)
        {
            response = new AlphaTabRenderResponse
            {
                success = false,
                error = ex.ToString()
            };
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(responsePath) ?? ".");
            File.WriteAllText(responsePath, JsonSerializer.Serialize(response, JsonOptions));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 3;
        }

        return response.success ? 0 : 1;
    }

    private static AlphaTabRenderResponse Render(AlphaTabRenderRequest request)
    {
        ValidateRequest(request);

        Directory.CreateDirectory(request.outputDirectory);
        string manifestPath = Path.Combine(request.outputDirectory, "alphatab_manifest.json");
        long notationLastWriteTicks = File.GetLastWriteTimeUtc(request.notationPath).Ticks;

        if (TryLoadExistingManifest(manifestPath, request, notationLastWriteTicks, out AlphaTabRenderManifest existing))
        {
            return new AlphaTabRenderResponse
            {
                success = true,
                error = string.Empty,
                manifestPath = manifestPath,
                trackLabel = existing.trackLabel,
                trackIndex = existing.trackIndex,
                notationPath = existing.notationPath
            };
        }

        Settings settings = CreateSettings(request);
        Score score = ScoreLoader.LoadScoreFromBytes(new Uint8Array(File.ReadAllBytes(request.notationPath)), settings);
        int trackIndex = Math.Clamp(request.trackIndex, 0, Math.Max(0, score.Tracks.Count - 1));
        Track selectedTrack = score.Tracks[(int)trackIndex];
        List<TempoPoint> tempoPoints = BuildTempoPoints(score, settings, out double midiDivision);
        List<BeatTiming> beatTimings = BuildBeatTimings(selectedTrack, tempoPoints, midiDivision);
        ApplyTimingOverridesIfAvailable(request.notationPath, beatTimings);

        ScoreRenderer renderer = new ScoreRenderer(settings)
        {
            Width = request.renderWidth
        };

        List<RenderedPartial> partials = new List<RenderedPartial>();
        Exception? renderError = null;
        using ManualResetEventSlim done = new ManualResetEventSlim(false);

        renderer.PartialLayoutFinished.On(args => renderer.RenderResult(args.Id));
        renderer.PartialRenderFinished.On(args =>
        {
            partials.Add(new RenderedPartial
            {
                index = partials.Count,
                firstMasterBarIndex = (int)args.FirstMasterBarIndex,
                lastMasterBarIndex = (int)args.LastMasterBarIndex,
                absoluteX = (float)args.X,
                absoluteY = (float)args.Y,
                width = (float)args.Width,
                height = (float)args.Height,
                renderResult = args.RenderResult
            });
        });
        renderer.RenderFinished.On(_ => done.Set());
        renderer.Error.On(ex =>
        {
            renderError = ex;
            done.Set();
        });

        renderer.RenderScore(score, new List<double> { selectedTrack.Index });

        if (!done.Wait(TimeSpan.FromSeconds(60)))
            throw new TimeoutException("AlphaTab render timed out after 60 seconds.");

        if (renderError != null)
            throw new InvalidOperationException("AlphaTab render failed.", renderError);

        if (renderer.BoundsLookup == null)
            throw new InvalidOperationException("AlphaTab render completed without bounds lookup data.");

        List<RenderedPartial> musicalPartials = partials
            .Where(partial => partial.firstMasterBarIndex >= 0 && partial.lastMasterBarIndex >= partial.firstMasterBarIndex)
            .OrderBy(partial => partial.firstMasterBarIndex)
            .ToList();

        AlphaTabRenderManifest manifest = new AlphaTabRenderManifest
        {
            version = RenderManifestVersion,
            notationPath = request.notationPath,
            notationLastWriteTicks = notationLastWriteTicks,
            trackIndex = trackIndex,
            trackLabel = ResolveTrackLabel(selectedTrack, trackIndex),
            themeId = request.themeId ?? "black_on_white",
            renderWidth = request.renderWidth,
            scale = request.scale,
            barsPerRow = request.barsPerRow,
            barsPerSection = request.barsPerSection,
            totalWidth = (float)renderer.Width,
            totalHeight = musicalPartials.Count > 0 ? musicalPartials.Max(partial => partial.absoluteY + partial.height) : 0f,
            sections = new List<AlphaTabRenderSectionManifest>()
        };

        for (int i = 0; i < musicalPartials.Count; i++)
        {
            RenderedPartial partial = musicalPartials[i];
            string pngPath = Path.Combine(request.outputDirectory, $"section_{i:D3}.png");
            File.WriteAllBytes(pngPath, ExtractPngBytes(partial.renderResult));

            AlphaTabRenderSectionManifest section = new AlphaTabRenderSectionManifest
            {
                index = i,
                firstMasterBarIndex = partial.firstMasterBarIndex,
                lastMasterBarIndex = partial.lastMasterBarIndex,
                imagePath = pngPath,
                width = partial.width,
                height = partial.height,
                startTime = 0f,
                endTime = 0f,
                beats = new List<AlphaTabRenderBeatMarker>()
            };

            List<BeatTiming> beatsInSection = beatTimings
                .Where(beat => beat.masterBarIndex >= partial.firstMasterBarIndex && beat.masterBarIndex <= partial.lastMasterBarIndex)
                .Where(ShouldIncludeBeatInSection)
                .ToList();
            bool allRestSection = beatsInSection.Count > 0 && beatsInSection.All(beat => beat.beat.IsRest);
            Dictionary<int, BarTimingRange> barTimingRanges = BuildBarTimingRanges(beatsInSection);

            if (beatsInSection.Count > 0)
            {
                section.startTime = beatsInSection.Min(beat => beat.startSeconds);
                section.endTime = beatsInSection.Max(beat => beat.endSeconds);
            }

            for (int beatIndex = 0; beatIndex < beatsInSection.Count; beatIndex++)
            {
                BeatTiming beatTiming = beatsInSection[beatIndex];
                BeatBounds? beatBounds = renderer.BoundsLookup.FindBeat(beatTiming.beat);
                if (beatBounds == null || beatBounds.BarBounds == null || beatBounds.BarBounds.VisualBounds == null)
                    continue;

                Bounds beatVisual = beatBounds.VisualBounds;
                Bounds barVisual = beatBounds.BarBounds.VisualBounds;
                float noteX01 = partial.width > 0.0001f
                    ? Clamp01((float)((beatBounds.OnNotesX - partial.absoluteX) / partial.width))
                    : 0f;
                float indicatorY01 = partial.height > 0.0001f
                    ? Clamp01((float)((barVisual.Y - partial.absoluteY) / partial.height))
                    : 0f;
                float indicatorHeight01 = partial.height > 0.0001f
                    ? Clamp01((float)(barVisual.H / partial.height))
                    : 0f;
                float visualWidth01 = partial.width > 0.0001f
                    ? Clamp01((float)(beatVisual.W / partial.width))
                    : 0f;
                (_, float timeEndX01) = ResolveTimeBasedIndicatorSpan(
                    beatTiming,
                    beatBounds,
                    partial,
                    barTimingRanges);
                float indicatorX01 = noteX01;
                float indicatorEndX01 = timeEndX01;

                if (allRestSection)
                {
                    float startFraction = beatIndex / (float)Math.Max(1, beatsInSection.Count);
                    float endFraction = (beatIndex + 1) / (float)Math.Max(1, beatsInSection.Count);
                    indicatorX01 = LerpClamped(0.08f, 0.92f, startFraction);
                    indicatorEndX01 = LerpClamped(0.08f, 0.92f, endFraction);
                    visualWidth01 = Math.Max(visualWidth01, indicatorEndX01 - indicatorX01);
                }

                section.beats.Add(new AlphaTabRenderBeatMarker
                {
                    beatId = (long)beatTiming.beat.Id,
                    masterBarIndex = beatTiming.masterBarIndex,
                    sourceEventId = beatTiming.sourceEventId,
                    voiceIndex = beatTiming.voiceIndex,
                    startTime = beatTiming.startSeconds,
                    endTime = beatTiming.endSeconds,
                    indicatorX01 = indicatorX01,
                    indicatorEndX01 = indicatorEndX01,
                    indicatorY01 = indicatorY01,
                    indicatorHeight01 = Math.Max(0.06f, indicatorHeight01),
                    visualWidth01 = visualWidth01,
                    isRest = beatTiming.beat.IsRest,
                    continuesFromPrevious = beatTiming.continuesFromPrevious,
                    continuesToNext = beatTiming.continuesToNext
                });
            }

            ResolveSequentialIndicatorAnchors(section.beats);
            manifest.sections.Add(section);
        }

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
        return new AlphaTabRenderResponse
        {
            success = true,
            error = string.Empty,
            manifestPath = manifestPath,
            notationPath = request.notationPath,
            trackIndex = trackIndex,
            trackLabel = manifest.trackLabel
        };
    }

    private static Settings CreateSettings(AlphaTabRenderRequest request)
    {
        Settings settings = new Settings();
        settings.Core.Engine = "skia";
        settings.Core.UseWorkers = false;
        settings.Core.IncludeNoteBounds = true;
        settings.Display.LayoutMode = LayoutMode.Page;
        settings.Display.Scale = Math.Max(0.25, request.scale);
        settings.Display.StaveProfile = StaveProfile.Tab;
        settings.Display.BarsPerRow = Math.Max(1, request.barsPerRow);
        settings.Display.BarCountPerPartial = Math.Max(1, request.barsPerSection);
        settings.Display.JustifyLastSystem = true;
        ApplyMinimalDisplayPadding(settings);
        ApplyThemeResources(settings, request.themeId);
        if (UseGameplayAlphaTabVoiceStyling)
            settings.Display.Resources.SecondaryGlyphColor = settings.Display.Resources.MainGlyphColor;
        if (UseGameplayAlphaTabTiedBendCleanup)
        {
            settings.Notation.ExtendBendArrowsOnTiedNotes = false;
            settings.Notation.Elements.Set(NotationElement.ParenthesisOnTiedBends, false);
            settings.Notation.Elements.Set(NotationElement.TabNotesOnTiedBends, false);
        }
        return settings;
    }

    private static void ApplyMinimalDisplayPadding(Settings settings)
    {
        settings.Display.Padding = new List<double> { 0d, 0d };
        settings.Display.FirstStaffPaddingLeft = 0d;
        settings.Display.StaffPaddingLeft = 0d;
        settings.Display.SystemLabelPaddingLeft = 0d;
        settings.Display.SystemLabelPaddingRight = 0d;
        settings.Display.FirstSystemPaddingTop = 0d;
        settings.Display.SystemPaddingTop = 0d;
        settings.Display.SystemPaddingBottom = 0d;
        settings.Display.LastSystemPaddingBottom = 0d;
        settings.Display.FirstNotationStaffPaddingTop = 0d;
        settings.Display.NotationStaffPaddingTop = 0d;
        settings.Display.NotationStaffPaddingBottom = 0d;
        settings.Display.LastNotationStaffPaddingBottom = 0d;
        settings.Display.EffectStaffPaddingTop = 0d;
        settings.Display.EffectStaffPaddingBottom = 0d;
        settings.Display.EffectBandPaddingBottom = 0d;
        settings.Display.TrackStaffPaddingBetween = 0d;
        settings.Display.LyricLinesPaddingBetween = 0d;
        settings.Display.AccoladeBarPaddingRight = 0d;
    }

    private static void ApplyThemeResources(Settings settings, string themeId)
    {
        bool darkTheme = string.Equals(themeId, "white_on_dark_blue", StringComparison.OrdinalIgnoreCase);
        AlphaTabColor mainGlyphColor = darkTheme ? CreateColor(255, 255, 255) : CreateColor(15, 15, 15);
        AlphaTabColor secondaryGlyphColor = darkTheme ? CreateColor(255, 255, 255) : CreateColor(15, 15, 15);
        AlphaTabColor lineColor = darkTheme ? CreateColor(244, 246, 252) : CreateColor(18, 18, 18);

        settings.Display.Resources.MainGlyphColor = mainGlyphColor;
        settings.Display.Resources.SecondaryGlyphColor = secondaryGlyphColor;
        settings.Display.Resources.StaffLineColor = lineColor;
        settings.Display.Resources.BarSeparatorColor = lineColor;
        settings.Display.Resources.BarNumberColor = lineColor;
        settings.Display.Resources.ScoreInfoColor = lineColor;
    }

    private static AlphaTabColor CreateColor(double r, double g, double b, double a = 255d)
    {
        return new AlphaTabColor(r, g, b, a);
    }

    private static List<TempoPoint> BuildTempoPoints(Score score, Settings settings, out double midiDivision)
    {
        MidiFile midiFile = new MidiFile();
        AlphaSynthMidiFileHandler midiHandler = new AlphaSynthMidiFileHandler(midiFile, true);
        MidiFileGenerator generator = new MidiFileGenerator(score, settings, midiHandler);
        generator.Generate();

        midiDivision = Math.Max(1.0, midiFile.Division);
        List<TempoPoint> tempoPoints = midiFile.Events
            .OfType<TempoChangeEvent>()
            .Select(tempo => new TempoPoint
            {
                tick = (long)Math.Max(0, tempo.Tick),
                bpm = Math.Max(1.0, tempo.BeatsPerMinute)
            })
            .OrderBy(point => point.tick)
            .ToList();

        if (tempoPoints.Count == 0 || tempoPoints[0].tick != 0)
        {
            tempoPoints.Insert(0, new TempoPoint
            {
                tick = 0,
                bpm = Math.Max(1.0, score.Tempo)
            });
        }

        return tempoPoints;
    }

    private static List<BeatTiming> BuildBeatTimings(Track track, List<TempoPoint> tempoPoints, double midiDivision)
    {
        HashSet<double> seenIds = new HashSet<double>();
        List<BeatTiming> beats = new List<BeatTiming>();

        foreach (Staff staff in track.Staves)
        {
            foreach (Bar bar in staff.Bars)
            {
                for (int voiceIndex = 0; voiceIndex < bar.Voices.Count; voiceIndex++)
                {
                    Voice voice = bar.Voices[voiceIndex];
                    foreach (Beat beat in voice.Beats)
                    {
                        if (!seenIds.Add(beat.Id))
                            continue;

                        long startTick = (long)Math.Round(beat.AbsolutePlaybackStart);
                        long endTick = (long)Math.Round(beat.AbsolutePlaybackStart + Math.Max(1.0, beat.PlaybackDuration));
                        float startSeconds = (float)TickToSeconds(startTick, tempoPoints, midiDivision);
                        float endSeconds = (float)TickToSeconds(endTick, tempoPoints, midiDivision);
                        if (endSeconds <= startSeconds)
                            endSeconds = startSeconds + 0.05f;

                        beats.Add(new BeatTiming
                        {
                            beat = beat,
                            voiceIndex = voiceIndex,
                            masterBarIndex = (int)bar.MasterBar.Index,
                            startSeconds = startSeconds,
                            endSeconds = endSeconds
                        });
                    }
                }
            }
        }

        return beats
            .OrderBy(beat => beat.startSeconds)
            .ThenBy(beat => beat.masterBarIndex)
            .ThenBy(beat => beat.voiceIndex)
            .ToList();
    }

    private static void ApplyTimingOverridesIfAvailable(string notationPath, List<BeatTiming> beatTimings)
    {
        if (beatTimings == null || beatTimings.Count == 0 || string.IsNullOrWhiteSpace(notationPath))
            return;

        string sidecarPath = $"{notationPath}.timing.json";
        if (!File.Exists(sidecarPath))
            return;

        RocksmithAlphaTabTimingSidecar? sidecar;
        try
        {
            sidecar = LoadJson<RocksmithAlphaTabTimingSidecar>(sidecarPath);
        }
        catch
        {
            return;
        }

        if (sidecar?.beats == null || sidecar.beats.Count != beatTimings.Count)
        {
            Console.Error.WriteLine($"[AlphaTabHelper] Timing override count mismatch for '{notationPath}'. sidecarBeats={sidecar?.beats?.Count ?? 0}, renderedBeats={beatTimings.Count}. Falling back to descriptor matching.");
        }

        int appliedOverrides = ApplyTimingOverridesByDescriptor(sidecar.beats, beatTimings);

        Console.Error.WriteLine($"[AlphaTabHelper] Applied {appliedOverrides} timing overrides from '{sidecarPath}'.");
    }

    private static int ApplyTimingOverridesByDescriptor(List<RocksmithAlphaTabTimingBeatEntry> entries, List<BeatTiming> beatTimings)
    {
        if (entries == null || beatTimings == null)
            return 0;

        Dictionary<(int bar, int voice), List<RocksmithAlphaTabTimingBeatEntry>> sidecarGroups = entries
            .GroupBy(entry => (entry.masterBarIndex, entry.voiceIndex))
            .ToDictionary(group => group.Key, group => group.OrderBy(entry => entry.startTime).ToList());

        Dictionary<(int bar, int voice), List<BeatTiming>> beatGroups = beatTimings
            .GroupBy(beat => (beat.masterBarIndex, beat.voiceIndex))
            .ToDictionary(group => group.Key, group => group.ToList());

        HashSet<BeatTiming> assigned = new HashSet<BeatTiming>();
        int applied = 0;
        foreach (KeyValuePair<(int bar, int voice), List<RocksmithAlphaTabTimingBeatEntry>> pair in sidecarGroups)
        {
            List<RocksmithAlphaTabTimingBeatEntry> groupEntries = pair.Value;
            if (!beatGroups.TryGetValue(pair.Key, out List<BeatTiming>? groupBeats))
                groupBeats = new List<BeatTiming>();

            int cursor = 0;
            for (int entryIndex = 0; entryIndex < groupEntries.Count; entryIndex++)
            {
                RocksmithAlphaTabTimingBeatEntry entry = groupEntries[entryIndex];
                int matchIndex = FindMatchingBeatIndex(groupBeats, entry, cursor, assigned);
                if (matchIndex < 0)
                    continue;

                BeatTiming beat = groupBeats[matchIndex];
                ApplyTimingOverride(entry, beat);
                assigned.Add(beat);
                cursor = matchIndex + 1;
                applied++;
            }
        }

        return applied;
    }

    private static int FindMatchingBeatIndex(List<BeatTiming> beats, RocksmithAlphaTabTimingBeatEntry entry, int startIndex, HashSet<BeatTiming> assigned)
    {
        if (beats == null || entry == null)
            return -1;

        for (int i = Math.Max(0, startIndex); i < beats.Count; i++)
        {
            BeatTiming beat = beats[i];
            if (assigned.Contains(beat))
                continue;
            if (BeatMatchesEntry(beat, entry))
                return i;
        }

        for (int i = 0; i < Math.Max(0, startIndex); i++)
        {
            BeatTiming beat = beats[i];
            if (assigned.Contains(beat))
                continue;
            if (BeatMatchesEntry(beat, entry))
                return i;
        }

        return -1;
    }

    private static bool BeatMatchesEntry(BeatTiming beat, RocksmithAlphaTabTimingBeatEntry entry)
    {
        if (beat == null || entry == null)
            return false;

        if ((beat.beat?.IsRest ?? true) != entry.isRest)
            return false;
        if (!string.Equals(BuildLoadedBeatNoteKey(beat), entry.noteKey ?? string.Empty, StringComparison.Ordinal))
            return false;
        if (ResolveLoadedBeatContinuesFromPrevious(beat) != entry.continuesFromPrevious)
            return false;
        if (ResolveLoadedBeatContinuesToNext(beat) != entry.continuesToNext)
            return false;

        return true;
    }

    private static void ApplyTimingOverride(RocksmithAlphaTabTimingBeatEntry entry, BeatTiming beat)
    {
        beat.startSeconds = entry.startTime;
        beat.endSeconds = Math.Max(entry.endTime, entry.startTime + 0.001f);
        beat.sourceEventId = entry.sourceEventId;
        beat.voiceIndex = entry.voiceIndex;
        beat.continuesFromPrevious = entry.continuesFromPrevious;
        beat.continuesToNext = entry.continuesToNext;
    }

    private static string BuildLoadedBeatNoteKey(BeatTiming beat)
    {
        if (beat?.beat == null || beat.beat.IsRest || beat.beat.Notes == null || beat.beat.Notes.Count == 0)
            return "rest";

        List<string> parts = beat.beat.Notes
            .Where(note => note != null)
            .OrderBy(note => note.String)
            .ThenBy(note => note.Fret)
            .Select(note => $"{note.String}:{Math.Max(0, note.Fret)}:{(note.IsDead ? 1 : 0)}")
            .ToList();

        return parts.Count == 0 ? "rest" : string.Join("|", parts);
    }

    private static bool ResolveLoadedBeatContinuesFromPrevious(BeatTiming beat)
    {
        if (beat?.beat == null || beat.beat.Notes == null)
            return false;

        return beat.beat.Notes.Any(note => note != null && (note.IsTieDestination || note.TieOrigin != null));
    }

    private static bool ResolveLoadedBeatContinuesToNext(BeatTiming beat)
    {
        if (beat?.beat == null || beat.beat.Notes == null)
            return false;

        return beat.beat.Notes.Any(note => note != null && note.TieDestination != null);
    }

    private static double TickToSeconds(long targetTick, List<TempoPoint> tempoPoints, double division)
    {
        if (tempoPoints.Count == 0)
            return 0.0;

        double seconds = 0.0;
        TempoPoint current = tempoPoints[0];

        for (int i = 1; i < tempoPoints.Count; i++)
        {
            TempoPoint next = tempoPoints[i];
            if (targetTick <= next.tick)
                break;

            seconds += TicksToSeconds(next.tick - current.tick, current.bpm, division);
            current = next;
        }

        seconds += TicksToSeconds(targetTick - current.tick, current.bpm, division);
        return seconds;
    }

    private static double TicksToSeconds(long deltaTicks, double bpm, double division)
    {
        if (deltaTicks <= 0 || bpm <= 0.0 || division <= 0.0)
            return 0.0;

        double beats = deltaTicks / division;
        return (60.0 / bpm) * beats;
    }

    private static byte[] ExtractPngBytes(object? renderResult)
    {
        if (renderResult == null)
            throw new InvalidOperationException("AlphaTab render result was null.");

        object? image = renderResult.GetType().GetProperty("Image")?.GetValue(renderResult) ?? renderResult;
        byte[]? png = image.GetType().GetMethod("ToPng", Type.EmptyTypes)?.Invoke(image, null) as byte[];
        if (png == null || png.Length == 0)
            throw new InvalidOperationException($"Unable to encode render result '{image.GetType().FullName}' to PNG.");
        return png;
    }

    private static Dictionary<int, BarTimingRange> BuildBarTimingRanges(List<BeatTiming> beatsInSection)
    {
        Dictionary<int, BarTimingRange> ranges = new Dictionary<int, BarTimingRange>();
        for (int i = 0; i < beatsInSection.Count; i++)
        {
            BeatTiming beat = beatsInSection[i];
            if (!ranges.TryGetValue(beat.masterBarIndex, out BarTimingRange range))
            {
                range = new BarTimingRange();
                ranges.Add(beat.masterBarIndex, range);
            }

            if (beat.startSeconds < range.startSeconds)
                range.startSeconds = beat.startSeconds;
            if (beat.endSeconds > range.endSeconds)
                range.endSeconds = beat.endSeconds;
        }

        return ranges;
    }

    private static bool ShouldIncludeBeatInSection(BeatTiming beat)
    {
        if (beat == null)
            return false;

        return !(beat.voiceIndex > 0 && beat.beat != null && beat.beat.IsRest && beat.sourceEventId < 0);
    }

    private static bool TryLoadExistingManifest(string manifestPath, AlphaTabRenderRequest request, long notationLastWriteTicks, out AlphaTabRenderManifest manifest)
    {
        manifest = null!;
        if (!File.Exists(manifestPath))
            return false;

        try
        {
            manifest = LoadJson<AlphaTabRenderManifest>(manifestPath);
            if (manifest == null ||
                manifest.version != RenderManifestVersion ||
                !string.Equals(Path.GetFullPath(manifest.notationPath ?? string.Empty), Path.GetFullPath(request.notationPath), StringComparison.OrdinalIgnoreCase) ||
                manifest.notationLastWriteTicks != notationLastWriteTicks ||
                manifest.trackIndex != request.trackIndex ||
                !string.Equals(manifest.themeId ?? string.Empty, request.themeId ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                manifest.renderWidth != request.renderWidth ||
                Math.Abs(manifest.scale - request.scale) > 0.0001f ||
                manifest.barsPerRow != request.barsPerRow ||
                manifest.barsPerSection != request.barsPerSection ||
                manifest.sections == null ||
                manifest.sections.Count == 0 ||
                manifest.sections.Any(section => string.IsNullOrWhiteSpace(section.imagePath) || !File.Exists(section.imagePath)))
            {
                manifest = null!;
                return false;
            }

            return true;
        }
        catch
        {
            manifest = null!;
            return false;
        }
    }

    private static (float startX01, float endX01) ResolveTimeBasedIndicatorSpan(
        BeatTiming beatTiming,
        BeatBounds beatBounds,
        RenderedPartial partial,
        Dictionary<int, BarTimingRange> barTimingRanges)
    {
        if (beatTiming == null || beatBounds?.BarBounds?.VisualBounds == null || partial.width <= 0.0001f)
            return (0f, 0.02f);

        if (!barTimingRanges.TryGetValue(beatTiming.masterBarIndex, out BarTimingRange range))
            range = new BarTimingRange { startSeconds = beatTiming.startSeconds, endSeconds = beatTiming.endSeconds };

        Bounds barVisual = beatBounds.BarBounds.VisualBounds;
        float barStartX01 = Clamp01((float)((barVisual.X - partial.absoluteX) / partial.width));
        float barEndX01 = Clamp01((float)(((barVisual.X + barVisual.W) - partial.absoluteX) / partial.width));
        float barSpan01 = Math.Max(0.02f, barEndX01 - barStartX01);
        float inset01 = Math.Min(0.012f, Math.Max(0.003f, barSpan01 * 0.04f));
        float usableStartX01 = Clamp01(barStartX01 + inset01);
        float usableEndX01 = Clamp01(Math.Max(usableStartX01 + 0.02f, barEndX01 - inset01));
        float barDuration = Math.Max(0.001f, range.endSeconds - range.startSeconds);
        float startFraction = Clamp01((beatTiming.startSeconds - range.startSeconds) / barDuration);
        float endFraction = Clamp01((beatTiming.endSeconds - range.startSeconds) / barDuration);
        if (endFraction <= startFraction + 0.0005f)
            endFraction = Math.Min(1f, startFraction + 0.05f);

        float startX01 = LerpClamped(usableStartX01, usableEndX01, startFraction);
        float endX01 = LerpClamped(usableStartX01, usableEndX01, endFraction);
        if (endX01 <= startX01 + 0.0005f)
            endX01 = Math.Min(1f, startX01 + 0.02f);
        return (startX01, endX01);
    }

    private static void ResolveSequentialIndicatorAnchors(List<AlphaTabRenderBeatMarker> beats)
    {
        if (beats == null || beats.Count == 0)
            return;

        float previousStartX01 = 0.08f;
        for (int i = 0; i < beats.Count; i++)
        {
            AlphaTabRenderBeatMarker beat = beats[i];
            if (beat == null)
                continue;

            if (beat.indicatorX01 < previousStartX01)
                beat.indicatorX01 = previousStartX01;

            beat.indicatorX01 = Clamp01(beat.indicatorX01);
            previousStartX01 = beat.indicatorX01;
        }

        for (int i = 0; i < beats.Count; i++)
        {
            AlphaTabRenderBeatMarker beat = beats[i];
            if (beat == null)
                continue;

            float minWidth = Math.Max(beat.visualWidth01, 0.02f);
            float fallbackEndX01 = beat.indicatorEndX01;
            float desiredEndX01 = i < beats.Count - 1
                ? beats[i + 1].indicatorX01
                : fallbackEndX01;

            if (desiredEndX01 < beat.indicatorX01 + minWidth)
                desiredEndX01 = beat.indicatorX01 + minWidth;

            beat.indicatorEndX01 = Clamp01(Math.Max(desiredEndX01, beat.indicatorX01 + 0.001f));
        }
    }

    private static T LoadJson<T>(string path)
    {
        string json = File.ReadAllText(path);
        T? value = JsonSerializer.Deserialize<T>(json, JsonOptions);
        if (value == null)
            throw new InvalidOperationException($"Failed to deserialize '{path}' as {typeof(T).Name}.");
        return value;
    }

    private static void ValidateRequest(AlphaTabRenderRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.notationPath) || !File.Exists(request.notationPath))
            throw new FileNotFoundException("AlphaTab source notation file was not found.", request?.notationPath);
        if (string.IsNullOrWhiteSpace(request.outputDirectory))
            throw new InvalidOperationException("AlphaTab output directory was not provided.");
        if (request.renderWidth < 320)
            throw new InvalidOperationException("AlphaTab render width must be at least 320 pixels.");
        if (request.scale <= 0f)
            throw new InvalidOperationException("AlphaTab render scale must be greater than zero.");
    }

    private static string ResolveTrackLabel(Track track, int trackIndex)
    {
        string name = track?.Name ?? string.Empty;
        return string.IsNullOrWhiteSpace(name) ? $"Track {trackIndex + 1}" : name.Trim();
    }

    private static bool TryParseArguments(string[] args, out string requestPath, out string responsePath, out string error)
    {
        requestPath = string.Empty;
        responsePath = string.Empty;
        error = string.Empty;

        if (args == null || args.Length < 5 || !string.Equals(args[0], "render", StringComparison.OrdinalIgnoreCase))
        {
            error = "Usage: AlphaTabRenderHelper render --request <request.json> --response <response.json>";
            return false;
        }

        for (int i = 1; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--request", StringComparison.OrdinalIgnoreCase))
                requestPath = args[i + 1];
            else if (string.Equals(args[i], "--response", StringComparison.OrdinalIgnoreCase))
                responsePath = args[i + 1];
        }

        if (string.IsNullOrWhiteSpace(requestPath) || string.IsNullOrWhiteSpace(responsePath))
        {
            error = "Both --request and --response arguments are required.";
            return false;
        }

        return true;
    }

    private static float Clamp01(float value)
    {
        if (value < 0f) return 0f;
        if (value > 1f) return 1f;
        return value;
    }

    private static float LerpClamped(float min, float max, float t)
    {
        if (t < 0f) t = 0f;
        if (t > 1f) t = 1f;
        return min + ((max - min) * t);
    }

    private sealed class TempoPoint
    {
        public long tick;
        public double bpm;
    }

    private sealed class BeatTiming
    {
        public Beat beat = null!;
        public int masterBarIndex;
        public int sourceEventId = -1;
        public int voiceIndex;
        public float startSeconds;
        public float endSeconds;
        public bool continuesFromPrevious;
        public bool continuesToNext;
    }

    private sealed class BarTimingRange
    {
        public float startSeconds = float.MaxValue;
        public float endSeconds = float.MinValue;
    }

    private sealed class RenderedPartial
    {
        public int index;
        public int firstMasterBarIndex;
        public int lastMasterBarIndex;
        public float absoluteX;
        public float absoluteY;
        public float width;
        public float height;
        public object? renderResult;
    }
}

[Serializable]
public sealed class AlphaTabRenderRequest
{
    public string notationPath = string.Empty;
    public int trackIndex;
    public string themeId = "white_on_dark_blue";
    public int renderWidth = 1600;
    public float scale = 1f;
    public int barsPerRow = 2;
    public int barsPerSection = 2;
    public string outputDirectory = string.Empty;
}

[Serializable]
public sealed class AlphaTabRenderResponse
{
    public bool success;
    public string error = string.Empty;
    public string manifestPath = string.Empty;
    public string notationPath = string.Empty;
    public int trackIndex;
    public string trackLabel = string.Empty;
}

[Serializable]
public sealed class AlphaTabRenderManifest
{
    public int version = 12;
    public string notationPath = string.Empty;
    public long notationLastWriteTicks;
    public int trackIndex;
    public string trackLabel = string.Empty;
    public string themeId = "white_on_dark_blue";
    public int renderWidth;
    public float scale;
    public int barsPerRow;
    public int barsPerSection;
    public float totalWidth;
    public float totalHeight;
    public List<AlphaTabRenderSectionManifest> sections = new List<AlphaTabRenderSectionManifest>();
}

[Serializable]
public sealed class AlphaTabRenderSectionManifest
{
    public int index;
    public int firstMasterBarIndex;
    public int lastMasterBarIndex;
    public string imagePath = string.Empty;
    public float width;
    public float height;
    public float startTime;
    public float endTime;
    public List<AlphaTabRenderBeatMarker> beats = new List<AlphaTabRenderBeatMarker>();
}

[Serializable]
public sealed class AlphaTabRenderBeatMarker
{
    public long beatId;
    public int masterBarIndex;
    public int sourceEventId = -1;
    public int voiceIndex;
    public float startTime;
    public float endTime;
    public float indicatorX01;
    public float indicatorEndX01;
    public float indicatorY01;
    public float indicatorHeight01;
    public float visualWidth01;
    public bool isRest;
    public bool continuesFromPrevious;
    public bool continuesToNext;
}

[Serializable]
public sealed class RocksmithAlphaTabTimingSidecar
{
    public int version = 2;
    public string notationPath = string.Empty;
    public List<RocksmithAlphaTabTimingBeatEntry> beats = new List<RocksmithAlphaTabTimingBeatEntry>();
}

[Serializable]
public sealed class RocksmithAlphaTabTimingBeatEntry
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
