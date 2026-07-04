using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;

public static class ChartEditorTimingService
{
    private static readonly ProfilerMarker RebuildBeatMapMarker = new ProfilerMarker("Timing.RebuildBeatMap");
    private static readonly ProfilerMarker ApplyContentMarker = new ProfilerMarker("Timing.ApplyBeatMapToContent");
    private static readonly ProfilerMarker AttachContentMarker = new ProfilerMarker("Timing.AttachContentToBeatMap");
    private static readonly ProfilerMarker SignatureMarker = new ProfilerMarker("Timing.ComputeSignature");
    private static readonly ProfilerMarker SyncLegacyMarker = new ProfilerMarker("Timing.SyncLegacySyncPoints");
    private static readonly ProfilerMarker TempoProbeMarker = new ProfilerMarker("Timing.MoveTrailingBeatAsTempoProbe");
    private static readonly ProfilerMarker PreviewMarker = new ProfilerMarker("Timing.PreviewBeatMapChange");
    private static readonly ProfilerMarker RebuildSanitizeMarker = new ProfilerMarker("Timing.Rebuild.Sanitize");
    private static readonly ProfilerMarker RebuildControlsMarker = new ProfilerMarker("Timing.Rebuild.CollectControls");
    private static readonly ProfilerMarker RebuildRegionsMarker = new ProfilerMarker("Timing.Rebuild.TempoRegions");
    private static readonly ProfilerMarker RebuildRequiredMarker = new ProfilerMarker("Timing.Rebuild.RequiredBeats");
    private static readonly ProfilerMarker RebuildGenerateMarker = new ProfilerMarker("Timing.Rebuild.GenerateMarkers");
    private static readonly ProfilerMarker RebuildSortMarker = new ProfilerMarker("Timing.Rebuild.FinalSort");

    private const double DefaultTempoBpm = 120.0;
    private const double MinTempoBpm = 20.0;
    private const double MaxTempoBpm = 360.0;
    private const double MinAnchorGapSeconds = 0.02;
    private const double BeatEpsilon = 0.0001;

    public static void EnsureDefaultSyncPoints(ChartEditorProject project)
    {
        EnsureBeatMap(project, attachContentToBeatMap: true);
    }

    public static void EnsureBeatMap(ChartEditorProject project, bool attachContentToBeatMap)
    {
        if (project == null)
            return;

        project.EnsureDefaultsThrottled();
        project.beatMap.defaultTempoBpm = ClampTempo(project.beatMap.defaultTempoBpm <= 0.001 ? DefaultTempoBpm : project.beatMap.defaultTempoBpm);

        if (!project.beatMap.timeSignatures.Any(change => change != null))
        {
            project.beatMap.timeSignatures.Add(new ChartEditorTimeSignatureChange
            {
                beatPosition = 0.0,
                numerator = 4,
                denominator = 4
            });
        }

        bool hasAnchors = project.beatMap.beatMarkers.Any(marker => marker != null && marker.isAnchor);
        bool hasLegacySyncPoints = project.syncPoints?.Any(point => point != null) == true;
        if (!hasAnchors && hasLegacySyncPoints)
            SeedAnchors(project);

        // Rebuilding the beat map is expensive (it regenerates every beat
        // marker with tempo integration). Skip it when none of its inputs
        // changed since the last rebuild; mutating service calls invoke
        // RebuildBeatMap directly, which refreshes the cached signature.
        BeatMapCacheState cacheState = beatMapCache.GetOrCreateValue(project);
        bool hasMarkers = project.beatMap.beatMarkers.Count > 0;
        bool skipRebuild = hasMarkers && cacheState.validatedFrame == Time.frameCount;
        if (!skipRebuild)
        {
            ulong signature = ComputeBeatMapInputSignature(project);
            skipRebuild = hasMarkers && signature == cacheState.signature;
            cacheState.signature = signature;
            cacheState.validatedFrame = Time.frameCount;
        }

        if (!skipRebuild)
            RebuildBeatMap(project);
        if (attachContentToBeatMap)
            AttachContentToBeatMap(project);
        // Always refresh the legacy mirror: anchor label/locked edits are not
        // signature inputs, so a skipped rebuild must not let saves serialize
        // stale syncPoint data. This is O(anchors) — cheap.
        SyncLegacySyncPointsFromBeatMap(project);
    }

    private sealed class BeatMapCacheState
    {
        public int validatedFrame = -1;
        public ulong signature;
        public List<ChartEditorTempoRegion> sortedRegions;
        public double duration;
        public int durationFrame = -1;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ChartEditorProject, BeatMapCacheState> beatMapCache =
        new System.Runtime.CompilerServices.ConditionalWeakTable<ChartEditorProject, BeatMapCacheState>();

    /// <summary>
    /// Call after mutating beat markers directly (outside this service's own
    /// mutation APIs) so the next EnsureBeatMap re-validates instead of
    /// trusting the same-frame cache and remapping against stale regions.
    /// </summary>
    public static void InvalidateBeatMapCache(ChartEditorProject project)
    {
        if (project == null)
            return;

        BeatMapCacheState cacheState = beatMapCache.GetOrCreateValue(project);
        cacheState.validatedFrame = -1;
        cacheState.sortedRegions = null;
    }

    /// <summary>
    /// Per-frame cached ChartEditorProject.DurationSeconds — the raw property
    /// scans every note of every track per access, which is far too slow for
    /// code that runs per drag frame. ComputeBeatMapInputSignature refreshes
    /// this cache for free during its own content scan.
    /// </summary>
    public static double GetCachedDurationSeconds(ChartEditorProject project)
    {
        if (project == null)
            return 0.0;

        // A short staleness window is fine: every consumer clamps with slack
        // (+4s cutoffs, +128-beat caps), and the signature scan refreshes the
        // value on any real rebuild. Recomputing per frame would re-scan all
        // notes once per drag frame for no behavioral difference.
        BeatMapCacheState cacheState = beatMapCache.GetOrCreateValue(project);
        if (!Application.isPlaying || cacheState.durationFrame < 0 || Time.frameCount - cacheState.durationFrame > 15)
        {
            cacheState.duration = project.DurationSeconds;
            cacheState.durationFrame = Time.frameCount;
        }

        return cacheState.duration;
    }

    private static ulong ComputeBeatMapInputSignature(ChartEditorProject project)
    {
        using ProfilerMarker.AutoScope scope = SignatureMarker.Auto();
        const ulong fnvPrime = 1099511628211UL;
        ulong hash = 14695981039346656037UL;

        void Mix(ulong value)
        {
            hash ^= value;
            hash *= fnvPrime;
        }

        void MixDouble(double value)
        {
            Mix((ulong)BitConverter.DoubleToInt64Bits(value));
        }

        ChartEditorBeatMap beatMap = project.beatMap;
        MixDouble(beatMap.defaultTempoBpm);
        // Duration inputs are mixed piecewise below (audio length + max content
        // time) instead of via DurationSeconds, whose getter re-scans every
        // note — the content scan further down already visits them all once.
        double audioDuration = Math.Max(0.0, project.audio?.durationSeconds ?? 0.0);
        MixDouble(audioDuration);
        Mix((ulong)(beatMap.beatMarkers?.Count ?? 0));

        if (beatMap.timeSignatures != null)
        {
            for (int i = 0; i < beatMap.timeSignatures.Count; i++)
            {
                ChartEditorTimeSignatureChange change = beatMap.timeSignatures[i];
                if (change == null)
                    continue;

                MixDouble(change.beatPosition);
                Mix((ulong)change.numerator);
                Mix((ulong)change.denominator);
            }
        }

        if (beatMap.beatMarkers != null)
        {
            for (int i = 0; i < beatMap.beatMarkers.Count; i++)
            {
                ChartEditorBeatMarker marker = beatMap.beatMarkers[i];
                if (marker == null || (!marker.isAnchor && !marker.generatedBySynchTheory))
                    continue;

                MixDouble(marker.beatPosition);
                MixDouble(marker.audioTimeSeconds);
                // bpm feeds BuildTempoRegions (trailing/fallback region), so a
                // bpm-only change must invalidate the beat map too.
                MixDouble(marker.bpm);
                Mix(marker.isAnchor ? 3UL : 5UL);
            }
        }

        double maxContentBeat = 0.0;
        double maxContentTime = 0.0;
        if (project.tracks != null)
        {
            for (int trackIndex = 0; trackIndex < project.tracks.Count; trackIndex++)
            {
                List<ChartEditorNote> notes = project.tracks[trackIndex]?.notes;
                if (notes == null)
                    continue;

                for (int noteIndex = 0; noteIndex < notes.Count; noteIndex++)
                {
                    ChartEditorNote note = notes[noteIndex];
                    if (note == null)
                        continue;

                    double endBeat = note.beatPosition + Math.Max(0.0, note.durationBeats);
                    if (endBeat > maxContentBeat)
                        maxContentBeat = endBeat;
                    double endTime = note.timeSeconds + Math.Max(0.0, note.durationSeconds);
                    if (endTime > maxContentTime)
                        maxContentTime = endTime;
                }
            }
        }

        if (project.sections != null)
        {
            for (int i = 0; i < project.sections.Count; i++)
            {
                ChartEditorSection section = project.sections[i];
                if (section == null)
                    continue;
                if (section.endBeatPosition > maxContentBeat)
                    maxContentBeat = section.endBeatPosition;
                if (section.endTimeSeconds > maxContentTime)
                    maxContentTime = section.endTimeSeconds;
            }
        }

        // maxContentTime is deliberately NOT mixed: remaps move note times
        // every preview frame, and the rebuild only consumes duration through
        // slack-padded caps — mixing it would force a second rebuild per drag
        // frame for no behavioral difference. Beat extent and audio length
        // (mixed above) cover every input the rebuild actually depends on.
        MixDouble(maxContentBeat);

        // This scan just visited every note — refresh the duration cache for
        // free so hot paths don't have to re-scan via DurationSeconds.
        BeatMapCacheState durationCache = beatMapCache.GetOrCreateValue(project);
        durationCache.duration = Math.Max(0.1, Math.Max(audioDuration, maxContentTime));
        durationCache.durationFrame = Time.frameCount;

        return hash;
    }

    public static void ApplySyncPoints(ChartEditorProject project)
    {
        ApplyBeatMapToContent(project);
    }

    public static void ApplyBeatMapToContent(ChartEditorProject project, bool visibleTracksOnly = false, ISet<string> previewTrackIds = null)
    {
        if (project == null)
            return;

        using ProfilerMarker.AutoScope scope = ApplyContentMarker.Auto();
        EnsureBeatMap(project, attachContentToBeatMap: false);

        // The remap runs 2-3 timing lookups per note; hoist the cached region
        // list once instead of re-fetching it from the ConditionalWeakTable on
        // every lookup.
        List<ChartEditorTempoRegion> tempoRegions = GetTempoRegionsCached(project);
        double defaultBpm = project.beatMap?.defaultTempoBpm ?? DefaultTempoBpm;
        double AudioForBeat(double beat) => GetAudioTimeForBeatInternal(tempoRegions, Math.Max(0.0, beat), defaultBpm);
        double BeatForAudio(double audio) => GetBeatPositionForAudioTimeInternal(tempoRegions, Math.Max(0.0, audio), defaultBpm);

        var applyClock = System.Diagnostics.Stopwatch.StartNew();
        int remappedNotes = 0;
        int touchedTracks = 0;

        // Heal any beat values corrupted by older builds while remapping:
        // nothing can legitimately live past the end of the audio.
        double beatCap = BeatForAudio(GetCachedDurationSeconds(project)) + 128.0;

        if (project.tracks != null)
        {
            for (int trackIndex = 0; trackIndex < project.tracks.Count; trackIndex++)
            {
                ChartEditorTrack track = project.tracks[trackIndex];
                List<ChartEditorNote> notes = track?.notes;
                if (notes == null)
                    continue;

                // Live drag previews only need the tracks actually drawn on
                // screen (previewTrackIds) or at least the user-visible ones;
                // the full apply runs when the gesture is committed.
                if (previewTrackIds != null)
                {
                    if (string.IsNullOrEmpty(track.id) || !previewTrackIds.Contains(track.id))
                        continue;
                }
                else if (visibleTracksOnly && !track.visible)
                    continue;

                touchedTracks++;
                for (int noteIndex = 0; noteIndex < notes.Count; noteIndex++)
                {
                    ChartEditorNote note = notes[noteIndex];
                    if (note == null || !note.usesBeatMapTiming)
                        continue;

                    remappedNotes++;
                    if (note.beatPosition > beatCap)
                        note.beatPosition = beatCap;

                    double start = AudioForBeat(note.beatPosition);
                    double durationBeats = Math.Min(note.durationBeats, Math.Max(0.0, beatCap - note.beatPosition));
                    if (durationBeats <= BeatEpsilon && note.durationSeconds > BeatEpsilon)
                        durationBeats = Math.Max(0.0, BeatForAudio(note.timeSeconds + note.durationSeconds) - note.beatPosition);

                    double end = AudioForBeat(note.beatPosition + Math.Max(0.0, durationBeats));
                    double oldDuration = Math.Max(0.0, note.durationSeconds);
                    note.chartTimeSeconds = Math.Max(0.0, start);
                    note.timeSeconds = Math.Max(0.0, start);
                    note.durationBeats = Math.Max(0.0, durationBeats);
                    note.durationSeconds = Math.Max(0.0, end - start);

                    RescaleTechniqueSegments(note, oldDuration);
                }

                // Beat-map remaps are monotonic, so an already-sorted list stays
                // sorted — skip the per-frame sort unless order actually broke.
                bool sorted = true;
                for (int noteIndex = 1; noteIndex < notes.Count; noteIndex++)
                {
                    if ((notes[noteIndex]?.timeSeconds ?? 0.0) < (notes[noteIndex - 1]?.timeSeconds ?? 0.0))
                    {
                        sorted = false;
                        break;
                    }
                }

                if (!sorted)
                    notes.Sort((a, b) => (a?.timeSeconds ?? 0.0).CompareTo(b?.timeSeconds ?? 0.0));

                // Tone switches and arpeggio guides must follow beat edits like
                // notes do, or saved packages fire tones at pre-edit times.
                // Items from older saves (no beat tracking yet) get attached on
                // first contact instead of remapped.
                List<ChartEditorToneChange> toneChanges = track.tones?.changes;
                if (toneChanges != null)
                {
                    for (int toneIndex = 0; toneIndex < toneChanges.Count; toneIndex++)
                    {
                        ChartEditorToneChange change = toneChanges[toneIndex];
                        if (change == null)
                            continue;

                        if (!change.usesBeatMapTiming)
                        {
                            change.beatPosition = BeatForAudio(change.timeSeconds);
                            change.usesBeatMapTiming = true;
                            continue;
                        }

                        if (change.beatPosition > beatCap)
                            change.beatPosition = beatCap;
                        change.timeSeconds = (float)Math.Max(0.0, AudioForBeat(change.beatPosition));
                    }
                }

                List<ChartEditorArpeggioGuide> guides = track.arpeggioGuides;
                if (guides != null)
                {
                    for (int guideIndex = 0; guideIndex < guides.Count; guideIndex++)
                    {
                        ChartEditorArpeggioGuide guide = guides[guideIndex];
                        if (guide == null)
                            continue;

                        if (!guide.usesBeatMapTiming)
                        {
                            guide.startBeat = BeatForAudio(guide.startTime);
                            guide.endBeat = Math.Max(guide.startBeat, BeatForAudio(guide.endTime));
                            guide.usesBeatMapTiming = true;
                            continue;
                        }

                        if (guide.startBeat > beatCap)
                            guide.startBeat = beatCap;
                        if (guide.endBeat > beatCap)
                            guide.endBeat = beatCap;
                        guide.startTime = (float)Math.Max(0.0, AudioForBeat(guide.startBeat));
                        guide.endTime = (float)Math.Max(guide.startTime, AudioForBeat(Math.Max(guide.startBeat, guide.endBeat)));
                    }
                }
            }
        }

        if (project.sections != null)
        {
            for (int sectionIndex = 0; sectionIndex < project.sections.Count; sectionIndex++)
            {
                ChartEditorSection section = project.sections[sectionIndex];
                if (section == null || !section.usesBeatMapTiming)
                    continue;

                if (section.startBeatPosition > beatCap)
                    section.startBeatPosition = beatCap;
                if (section.endBeatPosition > beatCap)
                    section.endBeatPosition = beatCap;

                double start = AudioForBeat(section.startBeatPosition);
                double end = AudioForBeat(Math.Max(section.startBeatPosition + BeatEpsilon, section.endBeatPosition));
                section.chartStartTimeSeconds = Math.Max(0.0, start);
                section.chartEndTimeSeconds = Math.Max(section.chartStartTimeSeconds + 0.05, end);
                section.startTimeSeconds = section.chartStartTimeSeconds;
                section.endTimeSeconds = section.chartEndTimeSeconds;
            }

            NormalizeSections(project);
        }

        SyncLegacySyncPointsFromBeatMap(project);
        project.dirty = true;

        // Diagnostic: silent when remaps are healthy. Names the workload so a
        // slow preview frame is attributable without another profiler round.
        long applyMs = applyClock.ElapsedMilliseconds;
        if (applyMs >= 50)
            Debug.LogWarning($"[ChartEditor] Slow ApplyBeatMapToContent {applyMs}ms | tracks={touchedTracks} notes={remappedNotes} previewIds={(previewTrackIds != null ? previewTrackIds.Count.ToString() : "null")} visibleOnly={visibleTracksOnly}");
    }

    public static void AttachContentToBeatMap(ChartEditorProject project, bool visibleTracksOnly = false, ISet<string> previewTrackIds = null)
    {
        if (project == null)
            return;

        using ProfilerMarker.AutoScope scope = AttachContentMarker.Auto();
        EnsureBeatMap(project, attachContentToBeatMap: false);

        List<ChartEditorTempoRegion> tempoRegions = GetTempoRegionsCached(project);
        double defaultBpm = project.beatMap?.defaultTempoBpm ?? DefaultTempoBpm;

        if (project.tracks != null)
        {
            foreach (ChartEditorTrack track in project.tracks)
            {
                if (track?.notes == null)
                    continue;

                // Live drag previews only need the tracks actually drawn on
                // screen; the full attach runs when the gesture is committed.
                if (previewTrackIds != null)
                {
                    if (string.IsNullOrEmpty(track.id) || !previewTrackIds.Contains(track.id))
                        continue;
                }
                else if (visibleTracksOnly && !track.visible)
                    continue;

                foreach (ChartEditorNote note in track.notes)
                    UpdateNoteBeatTiming(tempoRegions, defaultBpm, note);

                List<ChartEditorToneChange> toneChanges = track.tones?.changes;
                if (toneChanges != null)
                {
                    foreach (ChartEditorToneChange change in toneChanges)
                    {
                        if (change == null)
                            continue;

                        change.beatPosition = Math.Max(0.0, GetBeatPositionForAudioTimeInternal(tempoRegions, Math.Max(0.0, change.timeSeconds), defaultBpm));
                        change.usesBeatMapTiming = true;
                    }
                }

                List<ChartEditorArpeggioGuide> guides = track.arpeggioGuides;
                if (guides != null)
                {
                    foreach (ChartEditorArpeggioGuide guide in guides)
                    {
                        if (guide == null)
                            continue;

                        guide.startBeat = Math.Max(0.0, GetBeatPositionForAudioTimeInternal(tempoRegions, Math.Max(0.0, guide.startTime), defaultBpm));
                        guide.endBeat = Math.Max(guide.startBeat, GetBeatPositionForAudioTimeInternal(tempoRegions, Math.Max(0.0, guide.endTime), defaultBpm));
                        guide.usesBeatMapTiming = true;
                    }
                }
            }
        }

        if (project.sections != null)
        {
            foreach (ChartEditorSection section in project.sections)
                UpdateSectionBeatTiming(project, section);
        }
    }

    public static void UpdateNoteBeatTiming(ChartEditorProject project, ChartEditorNote note)
    {
        if (project == null || note == null)
            return;

        note.usesBeatMapTiming = true;
        note.beatPosition = Math.Max(0.0, GetBeatPositionForAudioTime(project, note.timeSeconds));
        double endBeat = Math.Max(note.beatPosition, GetBeatPositionForAudioTime(project, note.timeSeconds + Math.Max(0.0, note.durationSeconds)));
        note.durationBeats = Math.Max(0.0, endBeat - note.beatPosition);
        note.chartTimeSeconds = note.timeSeconds;
    }

    // Region-hoisted variant for bulk attach loops: identical math to the
    // public overload without re-fetching the cached region list per note.
    private static void UpdateNoteBeatTiming(List<ChartEditorTempoRegion> regions, double defaultBpm, ChartEditorNote note)
    {
        if (note == null)
            return;

        note.usesBeatMapTiming = true;
        note.beatPosition = Math.Max(0.0, GetBeatPositionForAudioTimeInternal(regions, Math.Max(0.0, note.timeSeconds), defaultBpm));
        double endBeat = Math.Max(note.beatPosition, GetBeatPositionForAudioTimeInternal(regions, Math.Max(0.0, note.timeSeconds + Math.Max(0.0, note.durationSeconds)), defaultBpm));
        note.durationBeats = Math.Max(0.0, endBeat - note.beatPosition);
        note.chartTimeSeconds = note.timeSeconds;
    }

    public static void UpdateSectionBeatTiming(ChartEditorProject project, ChartEditorSection section)
    {
        if (project == null || section == null)
            return;

        section.usesBeatMapTiming = true;
        section.startBeatPosition = Math.Max(0.0, GetBeatPositionForAudioTime(project, section.startTimeSeconds));
        section.endBeatPosition = Math.Max(section.startBeatPosition + BeatEpsilon, GetBeatPositionForAudioTime(project, section.endTimeSeconds));
        section.chartStartTimeSeconds = section.startTimeSeconds;
        section.chartEndTimeSeconds = section.endTimeSeconds;
    }

    public static ChartEditorBeatMarker AddAnchorAtAudioTime(ChartEditorProject project, double audioTimeSeconds, bool moveContentWithBeatMap = true)
    {
        if (project == null)
            return null;

        EnsureBeatMap(project, attachContentToBeatMap: true);
        audioTimeSeconds = Math.Max(0.0, Math.Min(GetCachedDurationSeconds(project), audioTimeSeconds));
        double beatPosition = Math.Max(0.0, Math.Round(GetBeatPositionForAudioTime(project, audioTimeSeconds)));

        ChartEditorBeatMarker marker = FindBeatMarker(project, beatPosition);
        if (marker != null && marker.isAnchor)
            return marker;

        if (marker == null)
        {
            marker = new ChartEditorBeatMarker
            {
                id = Guid.NewGuid().ToString("N"),
                beatPosition = beatPosition
            };
            project.beatMap.beatMarkers.Add(marker);
        }

        marker.isAnchor = true;
        marker.generatedBySynchTheory = false;
        marker.synchTheorySource = string.Empty;
        marker.audioTimeSeconds = audioTimeSeconds;
        marker.label = string.IsNullOrWhiteSpace(marker.label) ? FormatAnchorLabel(project, marker) : marker.label;
        ClearGeneratedSynchTheoryTimingForManualAnchorEdit(project, marker.beatPosition);
        RebuildBeatMap(project);
        FinishBeatMapEdit(project, moveContentWithBeatMap);
        return FindBeatMarker(project, beatPosition, anchorsOnly: true);
    }

    public static ChartEditorBeatMarker AddAnchorAtBeat(ChartEditorProject project, double beatPosition, double audioTimeSeconds, bool moveContentWithBeatMap = true, ISet<string> previewTrackIds = null)
    {
        if (project == null)
            return null;

        // Drag-start previews skip the full O(all-notes) attach; the commit on
        // pointer-up runs the full pass.
        EnsureBeatMap(project, attachContentToBeatMap: previewTrackIds == null);
        beatPosition = Math.Max(0.0, beatPosition);
        audioTimeSeconds = Math.Max(0.0, Math.Min(GetCachedDurationSeconds(project), audioTimeSeconds));

        ChartEditorBeatMarker marker = FindBeatMarker(project, beatPosition);
        if (marker == null)
        {
            marker = new ChartEditorBeatMarker
            {
                id = Guid.NewGuid().ToString("N"),
                beatPosition = beatPosition
            };
            project.beatMap.beatMarkers.Add(marker);
        }

        marker.isAnchor = true;
        marker.generatedBySynchTheory = false;
        marker.synchTheorySource = string.Empty;
        marker.audioTimeSeconds = audioTimeSeconds;
        marker.label = string.IsNullOrWhiteSpace(marker.label) ? FormatAnchorLabel(project, marker) : marker.label;
        ClearGeneratedSynchTheoryTimingForManualAnchorEdit(project, marker.beatPosition);
        RebuildBeatMap(project);
        FinishBeatMapEdit(project, moveContentWithBeatMap, previewTrackIds);
        return FindBeatMarker(project, beatPosition, anchorsOnly: true);
    }

    public static ChartEditorBeatMarker MoveBeatMarkerAsAnchor(ChartEditorProject project, ChartEditorBeatMarker marker, double audioTimeSeconds)
    {
        if (project == null || marker == null)
            return null;

        ChartEditorBeatMarker anchor = AddAnchorAtBeat(project, marker.beatPosition, marker.audioTimeSeconds);
        if (anchor == null || anchor.locked)
            return anchor;

        MoveAnchor(project, anchor, audioTimeSeconds);
        return FindAnchorById(project, anchor.id) ?? FindBeatMarker(project, anchor.beatPosition, anchorsOnly: true);
    }

    public static void MoveAnchor(ChartEditorProject project, ChartEditorBeatMarker anchor, double audioTimeSeconds, bool moveContentWithBeatMap = true)
    {
        if (project == null || anchor == null)
            return;

        // When content moves with the beat map, beats are the source of truth.
        // Attaching from times here would re-derive every track's beat
        // positions from (possibly preview-stale) timeSeconds against the new
        // map — permanently corrupting tracks the drag preview skipped. The
        // full ApplyBeatMapToContent below heals all tracks from their beats.
        EnsureBeatMap(project, attachContentToBeatMap: !moveContentWithBeatMap);
        ChartEditorBeatMarker liveAnchor = FindAnchorById(project, anchor.id) ?? FindBeatMarker(project, anchor.beatPosition, anchorsOnly: true);
        if (liveAnchor == null)
            return;

        List<ChartEditorBeatMarker> anchors = GetAnchors(project);
        int index = anchors.FindIndex(candidate => string.Equals(candidate.id, liveAnchor.id, StringComparison.OrdinalIgnoreCase));
        double min = 0.0;
        double max = GetCachedDurationSeconds(project);
        if (index > 0)
            min = anchors[index - 1].audioTimeSeconds + MinAnchorGapSeconds;
        if (index >= 0 && index + 1 < anchors.Count)
            max = anchors[index + 1].audioTimeSeconds - MinAnchorGapSeconds;

        liveAnchor.audioTimeSeconds = Math.Max(min, Math.Min(max, audioTimeSeconds));
        liveAnchor.isAnchor = true;
        liveAnchor.generatedBySynchTheory = false;
        liveAnchor.synchTheorySource = string.Empty;
        ClearGeneratedSynchTheoryTimingForManualAnchorEdit(project, liveAnchor.beatPosition);
        RebuildBeatMap(project);
        if (moveContentWithBeatMap)
            ApplyBeatMapToContent(project);
        else
        {
            AttachContentToBeatMap(project);
            SyncLegacySyncPointsFromBeatMap(project);
            project.dirty = true;
        }
    }

    public static void PreviewBeatMapChange(ChartEditorProject project, bool moveContentWithBeatMap = false, bool visibleTracksOnly = false, ISet<string> previewTrackIds = null)
    {
        if (project == null)
            return;

        using ProfilerMarker.AutoScope scope = PreviewMarker.Auto();

        // The caller just mutated beat-map inputs. Rebuild explicitly — the
        // EnsureBeatMap inside ApplyBeatMapToContent may have already validated
        // this frame (same-frame cache) and would skip the rebuild, remapping
        // content against stale tempo regions.
        RebuildBeatMap(project);
        if (moveContentWithBeatMap)
        {
            ApplyBeatMapToContent(project, visibleTracksOnly, previewTrackIds);
        }
        else
        {
            AttachContentToBeatMap(project, visibleTracksOnly, previewTrackIds);
            SyncLegacySyncPointsFromBeatMap(project);
            project.dirty = true;
        }
    }

    public static void RemoveAnchor(ChartEditorProject project, ChartEditorBeatMarker anchor)
    {
        if (project == null || anchor == null)
            return;

        EnsureBeatMap(project, attachContentToBeatMap: false);
        ChartEditorBeatMarker liveAnchor = FindAnchorById(project, anchor.id) ?? FindBeatMarker(project, anchor.beatPosition, anchorsOnly: true);
        if (liveAnchor == null)
            return;

        liveAnchor.isAnchor = false;
        liveAnchor.locked = false;
        liveAnchor.label = string.Empty;
        RebuildBeatMap(project);
        SyncLegacySyncPointsFromBeatMap(project);
        ApplyBeatMapToContent(project);
    }

    public static double GetAudioTimeForBeat(ChartEditorProject project, double beatPosition)
    {
        if (project?.beatMap == null)
            return Math.Max(0.0, beatPosition * 60.0 / DefaultTempoBpm);

        // Uses the cached sorted region list plus binary search: this runs
        // once per note during beat-map remaps, so allocating a sorted copy
        // of the regions per call froze the editor on synced charts.
        return GetAudioTimeForBeatInternal(
            GetTempoRegionsCached(project),
            Math.Max(0.0, beatPosition),
            project.beatMap.defaultTempoBpm);
    }

    public static double GetBeatPositionForAudioTime(ChartEditorProject project, double audioTimeSeconds)
    {
        if (project?.beatMap == null)
            return Math.Max(0.0, audioTimeSeconds * DefaultTempoBpm / 60.0);

        return GetBeatPositionForAudioTimeInternal(
            GetTempoRegionsCached(project),
            Math.Max(0.0, audioTimeSeconds),
            project.beatMap.defaultTempoBpm);
    }

    private static List<ChartEditorTempoRegion> GetTempoRegionsCached(ChartEditorProject project)
    {
        BeatMapCacheState state = beatMapCache.GetOrCreateValue(project);
        return state.sortedRegions ??= GetTempoRegions(project);
    }

    public static double GetTempoAtBeat(ChartEditorProject project, double beatPosition)
    {
        List<ChartEditorTempoRegion> regions = GetTempoRegionsCached(project);
        if (regions.Count == 0)
            return ClampTempo(project?.beatMap?.defaultTempoBpm ?? DefaultTempoBpm);

        for (int i = 0; i < regions.Count; i++)
        {
            ChartEditorTempoRegion region = regions[i];
            if (beatPosition <= region.endBeat || i == regions.Count - 1)
                return ClampTempo(region.bpm);
        }

        return ClampTempo(regions[regions.Count - 1].bpm);
    }

    public static void MoveEntireProject(ChartEditorProject project, double deltaSeconds)
    {
        if (project == null || Math.Abs(deltaSeconds) < 0.000001)
            return;

        ShiftProjectContent(project, _ => true, deltaSeconds);
    }

    public static void MoveEverythingAfter(ChartEditorProject project, double cursorSeconds, double deltaSeconds)
    {
        if (project == null || Math.Abs(deltaSeconds) < 0.000001)
            return;

        ShiftProjectContent(project, time => time >= cursorSeconds, deltaSeconds);
    }

    public static void StretchRegion(ChartEditorProject project, double startSeconds, double endSeconds, double scale)
    {
        if (project == null || endSeconds <= startSeconds + 0.0001 || Math.Abs(scale - 1.0) < 0.000001)
            return;

        scale = Math.Max(0.05, scale);

        double Transform(double time)
        {
            if (time < startSeconds || time > endSeconds)
                return time;
            return startSeconds + ((time - startSeconds) * scale);
        }

        if (project.tracks != null)
        {
            foreach (ChartEditorTrack track in project.tracks)
            {
                if (track == null)
                    continue;

                if (track.notes != null)
                {
                    foreach (ChartEditorNote note in track.notes)
                    {
                        if (note == null || note.timeSeconds < startSeconds || note.timeSeconds > endSeconds)
                            continue;

                        double oldEnd = note.timeSeconds + Math.Max(0.0, note.durationSeconds);
                        double oldDurationSeconds = Math.Max(0.0, note.durationSeconds);
                        note.timeSeconds = Math.Max(0.0, Transform(note.timeSeconds));
                        note.chartTimeSeconds = Math.Max(0.0, Transform(note.chartTimeSeconds));
                        double newEnd = Transform(oldEnd);
                        note.durationSeconds = Math.Max(0.0, newEnd - note.timeSeconds);
                        RescaleTechniqueSegments(note, oldDurationSeconds);
                        UpdateNoteBeatTiming(project, note);
                    }

                    track.notes.Sort((a, b) => (a?.timeSeconds ?? 0.0).CompareTo(b?.timeSeconds ?? 0.0));
                }

                List<ChartEditorToneChange> toneChanges = track.tones?.changes;
                if (toneChanges != null)
                {
                    foreach (ChartEditorToneChange change in toneChanges)
                    {
                        if (change == null || change.timeSeconds < startSeconds || change.timeSeconds > endSeconds)
                            continue;

                        change.timeSeconds = (float)Math.Max(0.0, Transform(change.timeSeconds));
                        change.beatPosition = GetBeatPositionForAudioTime(project, change.timeSeconds);
                        change.usesBeatMapTiming = true;
                    }
                }

                if (track.arpeggioGuides != null)
                {
                    foreach (ChartEditorArpeggioGuide guide in track.arpeggioGuides)
                    {
                        if (guide == null || guide.startTime < startSeconds || guide.startTime > endSeconds)
                            continue;

                        guide.startTime = (float)Math.Max(0.0, Transform(guide.startTime));
                        guide.endTime = (float)Math.Max(guide.startTime, Transform(guide.endTime));
                        guide.startBeat = GetBeatPositionForAudioTime(project, guide.startTime);
                        guide.endBeat = Math.Max(guide.startBeat, GetBeatPositionForAudioTime(project, guide.endTime));
                        guide.usesBeatMapTiming = true;
                    }
                }
            }
        }

        if (project.sections != null)
        {
            foreach (ChartEditorSection section in project.sections)
            {
                if (section == null || section.startTimeSeconds < startSeconds || section.startTimeSeconds > endSeconds)
                    continue;

                section.startTimeSeconds = Math.Max(0.0, Transform(section.startTimeSeconds));
                section.endTimeSeconds = Math.Max(section.startTimeSeconds + 0.05, Transform(section.endTimeSeconds));
                section.chartStartTimeSeconds = Math.Max(0.0, Transform(section.chartStartTimeSeconds));
                section.chartEndTimeSeconds = Math.Max(section.chartStartTimeSeconds + 0.05, Transform(section.chartEndTimeSeconds));
                section.userEdited = true;
                UpdateSectionBeatTiming(project, section);
            }

            NormalizeSections(project);
        }

        RebuildBeatMap(project);
        SyncLegacySyncPointsFromBeatMap(project);
        project.dirty = true;
    }

    public static void SetDefaultTempo(ChartEditorProject project, double bpm)
    {
        if (project == null)
            return;

        project.EnsureDefaults();
        project.beatMap.defaultTempoBpm = ClampTempo(bpm);
        ChartEditorBeatMarker origin = FindBeatMarker(project, 0.0);
        if (origin != null)
            origin.bpm = project.beatMap.defaultTempoBpm;
        RebuildBeatMap(project);
        ApplyBeatMapToContent(project);
    }

    public static bool SetTempoRegionBpmAtBeat(ChartEditorProject project, double beatPosition, double bpm)
    {
        if (project == null)
            return false;

        EnsureBeatMap(project, attachContentToBeatMap: true);
        bpm = ClampTempo(bpm);
        List<ChartEditorTempoRegion> regions = GetTempoRegions(project);
        if (regions.Count == 0)
        {
            SetDefaultTempo(project, bpm);
            return true;
        }

        ChartEditorTempoRegion region = regions
            .FirstOrDefault(candidate => candidate != null &&
                                         beatPosition >= candidate.startBeat - BeatEpsilon &&
                                         beatPosition <= candidate.endBeat + BeatEpsilon) ??
                                         regions[regions.Count - 1];
        if (region == null)
            return false;

        ChartEditorBeatMarker startMarker = FindBeatMarker(project, region.startBeat);
        ChartEditorBeatMarker startAnchor = FindBeatMarker(project, region.startBeat, anchorsOnly: true);
        ChartEditorBeatMarker endAnchor = FindBeatMarker(project, region.endBeat, anchorsOnly: true);
        if (startAnchor == null)
        {
            if (startMarker != null)
                startMarker.bpm = bpm;
            SetDefaultTempo(project, bpm);
            return true;
        }

        if (endAnchor == null || string.Equals(startAnchor.id, endAnchor.id, StringComparison.OrdinalIgnoreCase))
        {
            startAnchor.bpm = bpm;
            if (Math.Abs(startAnchor.beatPosition) <= BeatEpsilon)
                project.beatMap.defaultTempoBpm = bpm;
            RebuildBeatMap(project);
            ApplyBeatMapToContent(project);
            return true;
        }

        if (endAnchor.locked)
            return false;

        double beatSpan = Math.Max(BeatEpsilon, endAnchor.beatPosition - startAnchor.beatPosition);
        double requestedEndAudio = startAnchor.audioTimeSeconds + beatSpan * 60.0 / bpm;
        MoveAnchor(project, endAnchor, requestedEndAudio);
        return true;
    }

    public static bool CanUseTrailingTempoProbe(ChartEditorProject project, double beatPosition, bool markerIsAnchor)
    {
        if (project == null || markerIsAnchor || beatPosition <= BeatEpsilon)
            return false;

        EnsureBeatMap(project, attachContentToBeatMap: false);
        return !GetAnchors(project).Any(anchor => anchor != null && anchor.beatPosition > beatPosition + BeatEpsilon);
    }

    public static bool MoveTrailingBeatAsTempoProbe(ChartEditorProject project, double beatPosition, double audioTimeSeconds, bool moveContentWithBeatMap, bool visibleTracksOnly = false, ISet<string> previewTrackIds = null)
    {
        if (project == null || beatPosition <= BeatEpsilon)
            return false;

        using ProfilerMarker.AutoScope scope = TempoProbeMarker.Auto();

        EnsureBeatMap(project, attachContentToBeatMap: false);
        if (GetAnchors(project).Any(anchor => anchor != null && anchor.beatPosition > beatPosition + BeatEpsilon))
            return false;

        ChartEditorBeatMarker start = project.beatMap.beatMarkers
            .Where(marker => marker != null &&
                             marker.beatPosition < beatPosition - BeatEpsilon &&
                             (marker.isAnchor || Math.Abs(marker.beatPosition) <= BeatEpsilon))
            .OrderByDescending(marker => marker.beatPosition)
            .FirstOrDefault();
        if (start == null)
            return false;

        audioTimeSeconds = Math.Max(start.audioTimeSeconds + MinAnchorGapSeconds, Math.Min(GetCachedDurationSeconds(project), audioTimeSeconds));
        double beatSpan = Math.Max(BeatEpsilon, beatPosition - start.beatPosition);
        double timeSpan = Math.Max(MinAnchorGapSeconds, audioTimeSeconds - start.audioTimeSeconds);
        double bpm = ClampTempo(beatSpan * 60.0 / timeSpan);

        start.bpm = bpm;
        if (Math.Abs(start.beatPosition) <= BeatEpsilon)
            project.beatMap.defaultTempoBpm = bpm;
        ClearGeneratedSynchTheoryTimingAfterBeat(project, start.beatPosition);

        // The bpm/marker mutations above invalidate the beat map. Rebuild
        // explicitly — ApplyBeatMapToContent's EnsureBeatMap already validated
        // this frame (line above) and would otherwise skip the rebuild and
        // remap content against stale tempo regions.
        RebuildBeatMap(project);
        if (moveContentWithBeatMap)
            ApplyBeatMapToContent(project, visibleTracksOnly, previewTrackIds);
        else
        {
            AttachContentToBeatMap(project, visibleTracksOnly, previewTrackIds);
            SyncLegacySyncPointsFromBeatMap(project);
            project.dirty = true;
        }

        return true;
    }

    public static void SetTimeSignatureAtBeat(ChartEditorProject project, double beatPosition, int numerator, int denominator)
    {
        if (project == null)
            return;

        EnsureBeatMap(project, attachContentToBeatMap: false);
        beatPosition = Math.Max(0.0, Math.Round(beatPosition, 4));
        numerator = Math.Max(1, numerator);
        denominator = Mathf.Clamp(denominator <= 0 ? 4 : denominator, 1, 64);

        project.beatMap.timeSignatures.RemoveAll(change => change == null || Math.Abs(change.beatPosition - beatPosition) <= BeatEpsilon);
        project.beatMap.timeSignatures.Add(new ChartEditorTimeSignatureChange
        {
            beatPosition = beatPosition,
            numerator = numerator,
            denominator = denominator
        });
        project.beatMap.timeSignatures = project.beatMap.timeSignatures
            .Where(change => change != null)
            .OrderBy(change => change.beatPosition)
            .ToList();

        RebuildBeatMap(project);
        SyncLegacySyncPointsFromBeatMap(project);
        project.dirty = true;
    }

    public static ChartEditorBeatMarker GetNearestBeatMarker(ChartEditorProject project, double audioTimeSeconds)
    {
        EnsureBeatMap(project, attachContentToBeatMap: false);
        return project?.beatMap?.beatMarkers?
            .Where(marker => marker != null)
            .OrderBy(marker => Math.Abs(marker.audioTimeSeconds - audioTimeSeconds))
            .FirstOrDefault();
    }

    public static ChartEditorTimeSignatureChange GetTimeSignatureAtBeat(ChartEditorProject project, double beatPosition)
    {
        EnsureBeatMap(project, attachContentToBeatMap: false);
        return ResolveTimeSignature(project?.beatMap, beatPosition);
    }

    public static void NormalizeSections(ChartEditorProject project)
    {
        if (project?.sections == null)
            return;

        project.sections = project.sections
            .Where(section => section != null)
            .OrderBy(section => section.startTimeSeconds)
            .ToList();

        double duration = Math.Max(0.1, GetCachedDurationSeconds(project));
        for (int i = 0; i < project.sections.Count; i++)
        {
            ChartEditorSection section = project.sections[i];
            double nextStart = i + 1 < project.sections.Count
                ? project.sections[i + 1].startTimeSeconds
                : Math.Max(duration, section.endTimeSeconds);
            section.endTimeSeconds = Math.Max(section.startTimeSeconds + 0.05, Math.Max(section.endTimeSeconds, nextStart));
            section.chartEndTimeSeconds = Math.Max(section.chartStartTimeSeconds + 0.05, section.chartEndTimeSeconds);
        }
    }

    public static double MapChartTimeToAudioTime(double chartTimeSeconds, List<ChartEditorSyncPoint> points)
    {
        if (points == null || points.Count == 0)
            return Math.Max(0.0, chartTimeSeconds);

        if (points.Count == 1)
            return Math.Max(0.0, chartTimeSeconds + (points[0].audioTimeSeconds - points[0].chartTimeSeconds));

        chartTimeSeconds = Math.Max(0.0, chartTimeSeconds);
        List<ChartEditorSyncPoint> ordered = points
            .Where(point => point != null)
            .OrderBy(point => point.chartTimeSeconds)
            .ToList();
        if (ordered.Count == 0)
            return chartTimeSeconds;
        if (chartTimeSeconds <= ordered[0].chartTimeSeconds)
            return Math.Max(0.0, chartTimeSeconds + (ordered[0].audioTimeSeconds - ordered[0].chartTimeSeconds));

        for (int i = 0; i < ordered.Count - 1; i++)
        {
            ChartEditorSyncPoint a = ordered[i];
            ChartEditorSyncPoint b = ordered[i + 1];
            if (chartTimeSeconds > b.chartTimeSeconds)
                continue;

            double range = Math.Max(0.000001, b.chartTimeSeconds - a.chartTimeSeconds);
            double t = Mathf.Clamp01((float)((chartTimeSeconds - a.chartTimeSeconds) / range));
            return Mathf.Lerp((float)a.audioTimeSeconds, (float)b.audioTimeSeconds, (float)t);
        }

        ChartEditorSyncPoint last = ordered[ordered.Count - 1];
        return Math.Max(0.0, chartTimeSeconds + (last.audioTimeSeconds - last.chartTimeSeconds));
    }

    public static List<ChartEditorBeatMarker> GetBeatMarkers(ChartEditorProject project)
    {
        EnsureBeatMap(project, attachContentToBeatMap: false);
        return project?.beatMap?.beatMarkers?
            .Where(marker => marker != null)
            .OrderBy(marker => marker.beatPosition)
            .ToList() ?? new List<ChartEditorBeatMarker>();
    }

    public static List<ChartEditorBeatMarker> GetAnchors(ChartEditorProject project)
    {
        return project?.beatMap?.beatMarkers?
            .Where(marker => marker != null && marker.isAnchor)
            .OrderBy(marker => marker.beatPosition)
            .ToList() ?? new List<ChartEditorBeatMarker>();
    }

    public static List<ChartEditorTempoRegion> GetTempoRegions(ChartEditorProject project)
    {
        return project?.beatMap?.tempoRegions?
            .Where(region => region != null)
            .OrderBy(region => region.startBeat)
            .ToList() ?? new List<ChartEditorTempoRegion>();
    }

    public static void SyncLegacySyncPointsFromBeatMap(ChartEditorProject project)
    {
        if (project?.beatMap == null)
            return;

        using ProfilerMarker.AutoScope scope = SyncLegacyMarker.Auto();

        List<ChartEditorBeatMarker> anchors = GetAnchors(project);
        project.syncPoints = anchors.Select((anchor, index) => new ChartEditorSyncPoint
        {
            id = anchor.id,
            name = string.IsNullOrWhiteSpace(anchor.label) ? (index == 0 ? "Start" : $"Anchor {index + 1}") : anchor.label,
            chartTimeSeconds = GetAudioTimeForBeat(project, anchor.beatPosition),
            audioTimeSeconds = anchor.audioTimeSeconds,
            linkedSectionId = anchor.linkedSectionId,
            locked = anchor.locked
        }).ToList();
    }

    private static void SeedAnchors(ChartEditorProject project)
    {
        project.beatMap.beatMarkers.Clear();
        double defaultBpm = ClampTempo(project.beatMap.defaultTempoBpm);
        double beatSeconds = 60.0 / defaultBpm;

        List<ChartEditorSyncPoint> legacyPoints = project.syncPoints?
            .Where(point => point != null)
            .OrderBy(point => point.chartTimeSeconds)
            .ToList() ?? new List<ChartEditorSyncPoint>();

        if (legacyPoints.Count > 0)
        {
            for (int i = 0; i < legacyPoints.Count; i++)
            {
                ChartEditorSyncPoint point = legacyPoints[i];
                project.beatMap.beatMarkers.Add(new ChartEditorBeatMarker
                {
                    id = string.IsNullOrWhiteSpace(point.id) ? Guid.NewGuid().ToString("N") : point.id,
                    beatPosition = Math.Max(0.0, point.chartTimeSeconds / beatSeconds),
                    audioTimeSeconds = Math.Max(0.0, point.audioTimeSeconds),
                    isAnchor = true,
                    locked = point.locked,
                    label = string.IsNullOrWhiteSpace(point.name) ? (i == 0 ? "Start" : $"Anchor {i + 1}") : point.name,
                    linkedSectionId = point.linkedSectionId
                });
            }
            return;
        }

        project.beatMap.beatMarkers.Add(new ChartEditorBeatMarker
        {
            id = StableBeatMapId("beat", 0.0),
            beatPosition = 0.0,
            audioTimeSeconds = 0.0,
            isAnchor = false,
            label = string.Empty,
            bpm = defaultBpm
        });
    }

    private static void RebuildBeatMap(ChartEditorProject project)
    {
        if (project?.beatMap == null)
            return;

        using ProfilerMarker.AutoScope scope = RebuildBeatMapMarker.Auto();

        ChartEditorBeatMap beatMap = project.beatMap;
        beatMap.EnsureDefaults();

        var rebuildClock = System.Diagnostics.Stopwatch.StartNew();
        int markersIn = beatMap.beatMarkers.Count;

        RebuildSanitizeMarker.Begin();
        // An absolute sanity cap that does NOT depend on tempo regions (which
        // a corrupt marker would poison): no beat can exist beyond what the
        // song duration holds at the maximum supported tempo. Corrupt timing
        // controls with a valid audio time would otherwise survive forever
        // and force every rebuild to generate millions of markers.
        // DurationSeconds scans every note of every track per access — read the
        // per-frame cached value once and reuse it below.
        double songDuration = GetCachedDurationSeconds(project);
        double maxSaneBeat = Math.Max(4.0, songDuration * MaxTempoBpm / 60.0) + 128.0;
        int removedCorrupt = beatMap.beatMarkers.RemoveAll(marker =>
            marker != null &&
            (double.IsNaN(marker.beatPosition) ||
             double.IsInfinity(marker.beatPosition) ||
             marker.beatPosition > maxSaneBeat ||
             double.IsNaN(marker.audioTimeSeconds) ||
             double.IsInfinity(marker.audioTimeSeconds)));
        if (removedCorrupt > 0)
            Debug.LogWarning($"[ChartEditor] Removed {removedCorrupt} corrupt beat markers (beat position beyond {maxSaneBeat:0} or non-finite).");
        RebuildSanitizeMarker.End();
        long msSanitize = rebuildClock.ElapsedMilliseconds;

        RebuildControlsMarker.Begin();
        List<ChartEditorBeatMarker> anchors = beatMap.beatMarkers
            .Where(marker => marker != null && marker.isAnchor)
            .OrderBy(marker => marker.beatPosition)
            .ToList();

        ChartEditorBeatMarker origin = beatMap.beatMarkers
            .Where(marker => marker != null && Math.Abs(marker.beatPosition) <= BeatEpsilon)
            .OrderByDescending(marker => marker.isAnchor)
            .ThenByDescending(marker => marker.generatedBySynchTheory)
            .FirstOrDefault();

        if (origin == null)
        {
            double originAudio = 0.0;
            if (anchors.Count > 0)
                originAudio = Math.Max(0.0, anchors[0].audioTimeSeconds - anchors[0].beatPosition * 60.0 / ClampTempo(beatMap.defaultTempoBpm));

            origin = new ChartEditorBeatMarker
            {
                id = StableBeatMapId("beat", 0.0),
                beatPosition = 0.0,
                audioTimeSeconds = originAudio,
                isAnchor = false,
                label = string.Empty,
                bpm = ClampTempo(beatMap.defaultTempoBpm)
            };
        }

        List<ChartEditorBeatMarker> timingControls = beatMap.beatMarkers
            .Where(marker => marker != null &&
                             (marker.isAnchor || marker.generatedBySynchTheory) &&
                             Math.Abs(marker.beatPosition) > BeatEpsilon)
            .OrderBy(marker => marker.beatPosition)
            .ToList();
        timingControls.Insert(0, origin);
        RebuildControlsMarker.End();
        long msControls = rebuildClock.ElapsedMilliseconds;

        RebuildRegionsMarker.Begin();
        beatMap.tempoRegions = BuildTempoRegions(project, timingControls, songDuration);
        RebuildRegionsMarker.End();
        long msRegions = rebuildClock.ElapsedMilliseconds;

        RebuildRequiredMarker.Begin();
        double maxBeat = ResolveRequiredBeatCount(project, timingControls, beatMap.tempoRegions, songDuration);
        maxBeat = Math.Min(maxBeat, maxSaneBeat);
        int markerCount = Mathf.Max(1, Mathf.CeilToInt((float)maxBeat) + 2);
        if (markerCount > 100000)
            Debug.LogWarning($"[ChartEditor] Beat map rebuild is generating {markerCount} markers (maxBeat {maxBeat:0}). Check for corrupt content.");
        RebuildRequiredMarker.End();
        long msRequired = rebuildClock.ElapsedMilliseconds;

        RebuildGenerateMarker.Begin();
        List<ChartEditorTimeSignatureChange> signatureChanges = BuildSortedTimeSignatureChanges(beatMap);
        Dictionary<string, ChartEditorBeatMarker> anchorByRoundedBeat = timingControls
            .GroupBy(anchor => BeatKey(anchor.beatPosition), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        List<ChartEditorBeatMarker> rebuilt = new List<ChartEditorBeatMarker>(markerCount);
        HashSet<ChartEditorBeatMarker> consumedControls = new HashSet<ChartEditorBeatMarker>();
        for (int i = 0; i < markerCount; i++)
        {
            double beat = i;
            string key = BeatKey(beat);
            ChartEditorBeatMarker marker;
            if (anchorByRoundedBeat.TryGetValue(key, out ChartEditorBeatMarker anchor))
            {
                marker = anchor;
                consumedControls.Add(anchor);
            }
            else
            {
                marker = new ChartEditorBeatMarker { id = StableBeatMapId("beat", beat) };
            }

            marker.index = i;
            marker.beatPosition = beat;
            if (!marker.isAnchor && !marker.generatedBySynchTheory)
                marker.audioTimeSeconds = GetAudioTimeForBeatInternal(beatMap.tempoRegions, beat, beatMap.defaultTempoBpm);
            ApplyBarInfo(signatureChanges, marker);
            rebuilt.Add(marker);
        }

        foreach (ChartEditorBeatMarker anchor in timingControls)
        {
            // Membership-based, not epsilon-based: an anchor whose rounded key
            // missed the integer lookup (BeatKey rounds to 4 decimals while
            // BeatEpsilon is 1e-4) would otherwise be silently deleted.
            if (consumedControls.Contains(anchor))
                continue;

            anchor.index = Mathf.RoundToInt((float)anchor.beatPosition);
            anchor.audioTimeSeconds = Math.Max(0.0, anchor.audioTimeSeconds);
            ApplyBarInfo(signatureChanges, anchor);
            rebuilt.Add(anchor);
        }
        RebuildGenerateMarker.End();
        long msGenerate = rebuildClock.ElapsedMilliseconds;

        RebuildSortMarker.Begin();
        double durationCutoff = songDuration + 4.0;
        beatMap.beatMarkers = rebuilt
            .Where(marker => marker != null && marker.audioTimeSeconds <= durationCutoff)
            .OrderBy(marker => marker.beatPosition)
            .ThenBy(marker => marker.audioTimeSeconds)
            .ToList();
        RebuildSortMarker.End();
        long msSort = rebuildClock.ElapsedMilliseconds;

        BeatMapCacheState cacheState = beatMapCache.GetOrCreateValue(project);
        cacheState.signature = ComputeBeatMapInputSignature(project);
        cacheState.validatedFrame = Time.frameCount;
        cacheState.sortedRegions = null;

        // Diagnostic: silent when rebuilds are healthy, loud when a phase
        // regresses. Rebuilds run once per frame during drags, so an
        // unconditional log would itself cost editor time.
        long msTotal = rebuildClock.ElapsedMilliseconds;
        if (msTotal >= 50)
            Debug.LogWarning(
                $"[ChartEditor] Slow RebuildBeatMap {msTotal}ms | sanitize {msSanitize} | controls {msControls - msSanitize} | regions {msRegions - msControls} | required {msRequired - msRegions} | generate {msGenerate - msRequired} | sort {msSort - msGenerate} | signature {msTotal - msSort} | markersIn={markersIn} removedCorrupt={removedCorrupt} timingControls={timingControls.Count} timeSigs={beatMap.timeSignatures?.Count ?? 0} tempoRegions={beatMap.tempoRegions?.Count ?? 0} markerCount={markerCount} markersOut={beatMap.beatMarkers.Count}");
    }

    public static int ClearGeneratedSynchTheoryTiming(ChartEditorProject project, double? startBeat = null, double? endBeat = null)
    {
        if (project?.beatMap?.beatMarkers == null)
            return 0;

        double minBeat = startBeat ?? double.NegativeInfinity;
        double maxBeat = endBeat ?? double.PositiveInfinity;
        int removed = project.beatMap.beatMarkers.RemoveAll(marker =>
            marker != null &&
            marker.generatedBySynchTheory &&
            !marker.isAnchor &&
            marker.beatPosition >= minBeat - BeatEpsilon &&
            marker.beatPosition <= maxBeat + BeatEpsilon);

        if (removed > 0)
        {
            RebuildBeatMap(project);
            ApplyBeatMapToContent(project);
        }

        return removed;
    }

    private static int ClearGeneratedSynchTheoryTimingForManualAnchorEdit(ChartEditorProject project, double beatPosition)
    {
        if (project?.beatMap?.beatMarkers == null)
            return 0;

        beatPosition = Math.Max(0.0, beatPosition);
        List<ChartEditorBeatMarker> surroundingAnchors = project.beatMap.beatMarkers
            .Where(marker => marker != null &&
                             marker.isAnchor &&
                             Math.Abs(marker.beatPosition - beatPosition) > BeatEpsilon)
            .OrderBy(marker => marker.beatPosition)
            .ToList();

        ChartEditorBeatMarker previousAnchor = surroundingAnchors
            .Where(marker => marker.beatPosition < beatPosition - BeatEpsilon)
            .LastOrDefault();
        ChartEditorBeatMarker nextAnchor = surroundingAnchors
            .FirstOrDefault(marker => marker.beatPosition > beatPosition + BeatEpsilon);

        double lowerBeat = previousAnchor != null ? previousAnchor.beatPosition : 0.0;
        double upperBeat = nextAnchor != null ? nextAnchor.beatPosition : double.PositiveInfinity;

        // Locked generated markers are imported measured grids (theory
        // packages) — incidental anchor edits must not delete a song's
        // measured timing; deliberate range syncs use the public clear.
        return project.beatMap.beatMarkers.RemoveAll(marker =>
            marker != null &&
            marker.generatedBySynchTheory &&
            !marker.isAnchor &&
            !marker.locked &&
            marker.beatPosition > lowerBeat + BeatEpsilon &&
            marker.beatPosition < upperBeat - BeatEpsilon);
    }

    private static int ClearGeneratedSynchTheoryTimingAfterBeat(ChartEditorProject project, double beatPosition)
    {
        if (project?.beatMap?.beatMarkers == null)
            return 0;

        double lowerBeat = Math.Max(0.0, beatPosition);
        return project.beatMap.beatMarkers.RemoveAll(marker =>
            marker != null &&
            marker.generatedBySynchTheory &&
            !marker.isAnchor &&
            !marker.locked &&
            marker.beatPosition > lowerBeat + BeatEpsilon);
    }

    private static void FinishBeatMapEdit(ChartEditorProject project, bool moveContentWithBeatMap, ISet<string> previewTrackIds = null)
    {
        if (moveContentWithBeatMap)
        {
            ApplyBeatMapToContent(project, visibleTracksOnly: false, previewTrackIds);
        }
        else
        {
            AttachContentToBeatMap(project, visibleTracksOnly: false, previewTrackIds);
            SyncLegacySyncPointsFromBeatMap(project);
            project.dirty = true;
        }
    }

    private static List<ChartEditorTempoRegion> BuildTempoRegions(ChartEditorProject project, List<ChartEditorBeatMarker> anchors, double songDuration)
    {
        List<ChartEditorTempoRegion> regions = new List<ChartEditorTempoRegion>();
        double fallbackBpm = ClampTempo(project.beatMap.defaultTempoBpm);
        for (int i = 0; i < anchors.Count; i++)
        {
            ChartEditorBeatMarker start = anchors[i];
            ChartEditorBeatMarker end = i + 1 < anchors.Count ? anchors[i + 1] : null;
            double startBeat = Math.Max(0.0, start.beatPosition);
            double startAudio = Math.Max(0.0, start.audioTimeSeconds);
            double endBeat;
            double endAudio;
            double bpm;
            double explicitBpm = start.bpm > 0.001 ? ClampTempo(start.bpm) : 0.0;

            if (end != null)
            {
                // Clamp degenerate or non-monotonic spans to the next anchor
                // instead of falling through to the trailing extrapolation —
                // that produced a region reaching the song end in the MIDDLE
                // of the list, overlapping every later anchor and mismapping
                // beats/times by whole seconds.
                endBeat = Math.Max(startBeat + BeatEpsilon, end.beatPosition);
                endAudio = Math.Max(startAudio + MinAnchorGapSeconds, end.audioTimeSeconds);
                bpm = ClampTempo((endBeat - startBeat) * 60.0 / Math.Max(MinAnchorGapSeconds, endAudio - startAudio));
            }
            else
            {
                bpm = explicitBpm > 0.0
                    ? explicitBpm
                    : i > 0 && regions.Count > 0
                        ? ClampTempo(regions[regions.Count - 1].bpm)
                        : fallbackBpm;
                endAudio = Math.Max(songDuration, startAudio + 60.0 / bpm);
                endBeat = startBeat + Math.Max(1.0, (endAudio - startAudio) * bpm / 60.0);
            }

            regions.Add(new ChartEditorTempoRegion
            {
                id = StableBeatMapId("tempo", startBeat, endBeat),
                startBeat = startBeat,
                endBeat = Math.Max(startBeat + BeatEpsilon, endBeat),
                startAudioTimeSeconds = startAudio,
                endAudioTimeSeconds = Math.Max(startAudio + MinAnchorGapSeconds, endAudio),
                bpm = bpm
            });
        }

        return regions;
    }

    private static double ResolveRequiredBeatCount(ChartEditorProject project, List<ChartEditorBeatMarker> anchors, List<ChartEditorTempoRegion> regions, double songDuration)
    {
        double durationBeat = Math.Max(4.0, GetBeatPositionForAudioTimeInternal(regions, songDuration, project.beatMap.defaultTempoBpm));
        double maxBeat = durationBeat;
        foreach (ChartEditorBeatMarker anchor in anchors)
            maxBeat = Math.Max(maxBeat, anchor.beatPosition);

        if (project.tracks != null)
        {
            foreach (ChartEditorTrack track in project.tracks)
            {
                if (track?.notes == null)
                    continue;

                foreach (ChartEditorNote note in track.notes)
                {
                    if (note == null)
                        continue;

                    maxBeat = Math.Max(maxBeat, note.beatPosition + Math.Max(0.0, note.durationBeats));
                }
            }
        }

        if (project.sections != null)
        {
            foreach (ChartEditorSection section in project.sections)
            {
                if (section == null)
                    continue;

                maxBeat = Math.Max(maxBeat, section.endBeatPosition);
            }
        }

        // A single note, section, or marker carrying a corrupt beat value
        // (written by an older build) must never inflate the beat map. Use an
        // absolute cap derived only from the audio duration and the maximum
        // supported tempo — region-derived values could themselves be
        // poisoned by a corrupt timing control.
        double hardCap = Math.Max(4.0, songDuration * MaxTempoBpm / 60.0) + 128.0;
        return Math.Min(maxBeat, Math.Min(durationBeat + 128.0, hardCap));
    }

    // Technique segments store second-based offsets relative to the note
    // start — any pass that changes a note's duration must rescale them or
    // slides and bends desync from the resized sustain.
    private static void RescaleTechniqueSegments(ChartEditorNote note, double oldDurationSeconds)
    {
        if (note?.techniqueSegments == null ||
            note.techniqueSegments.Count == 0 ||
            oldDurationSeconds <= BeatEpsilon ||
            Math.Abs(note.durationSeconds - oldDurationSeconds) <= BeatEpsilon)
        {
            return;
        }

        float segmentScale = (float)(note.durationSeconds / oldDurationSeconds);
        for (int segmentIndex = 0; segmentIndex < note.techniqueSegments.Count; segmentIndex++)
        {
            ChartEditorTechniqueSegment segment = note.techniqueSegments[segmentIndex];
            if (segment == null)
                continue;

            segment.startOffset = Mathf.Max(0f, segment.startOffset * segmentScale);
            segment.endOffset = Mathf.Max(segment.startOffset, segment.endOffset * segmentScale);
        }
    }

    private static List<ChartEditorTimeSignatureChange> BuildSortedTimeSignatureChanges(ChartEditorBeatMap beatMap)
    {
        List<ChartEditorTimeSignatureChange> changes = beatMap?.timeSignatures?
            .Where(change => change != null &&
                             !double.IsNaN(change.beatPosition) &&
                             !double.IsInfinity(change.beatPosition))
            .OrderBy(change => change.beatPosition)
            .ToList() ?? new List<ChartEditorTimeSignatureChange>();
        if (changes.Count == 0 || changes[0].beatPosition > BeatEpsilon)
            changes.Insert(0, new ChartEditorTimeSignatureChange { beatPosition = 0.0, numerator = 4, denominator = 4 });
        return changes;
    }

    private static void ApplyBarInfo(List<ChartEditorTimeSignatureChange> changes, ChartEditorBeatMarker marker)
    {
        if (marker == null || changes == null || changes.Count == 0)
            return;

        int barNumber = 1;
        ChartEditorTimeSignatureChange active = changes[0];
        double segmentStartBeat = Math.Max(0.0, active.beatPosition);

        for (int i = 1; i < changes.Count && changes[i].beatPosition <= marker.beatPosition + BeatEpsilon; i++)
        {
            double measureLength = GetMeasureLengthInQuarterBeats(active);
            // Ceiling, not floor: a signature change landing mid-bar starts a
            // new bar, so the trailing partial bar still counts — flooring gave
            // two different beats the same "bar 1, beat 1" downbeat label.
            if (measureLength > BeatEpsilon)
                barNumber += Mathf.Max(0, Mathf.CeilToInt((float)((changes[i].beatPosition - segmentStartBeat) / measureLength - 0.000001)));

            active = changes[i];
            segmentStartBeat = Math.Max(0.0, active.beatPosition);
        }

        double activeMeasureLength = GetMeasureLengthInQuarterBeats(active);
        double beatUnit = GetBeatUnitInQuarterBeats(active);
        double offset = Math.Max(0.0, marker.beatPosition - segmentStartBeat);
        int barsIntoSegment = activeMeasureLength > BeatEpsilon ? Mathf.Max(0, Mathf.FloorToInt((float)(offset / activeMeasureLength))) : 0;
        double beatRemainder = activeMeasureLength > BeatEpsilon ? offset - barsIntoSegment * activeMeasureLength : 0.0;

        marker.barNumber = Math.Max(1, barNumber + barsIntoSegment);
        marker.beatInBar = Mathf.Clamp(Mathf.FloorToInt((float)(beatRemainder / Math.Max(BeatEpsilon, beatUnit))) + 1, 1, Math.Max(1, active.numerator));
        marker.isDownbeat = beatRemainder <= BeatEpsilon || activeMeasureLength - beatRemainder <= BeatEpsilon;
    }

    private static ChartEditorTimeSignatureChange ResolveTimeSignature(ChartEditorBeatMap beatMap, double beatPosition)
    {
        ChartEditorTimeSignatureChange resolved = beatMap?.timeSignatures?
            .Where(change => change != null)
            .OrderBy(change => change.beatPosition)
            .LastOrDefault(change => change.beatPosition <= beatPosition + BeatEpsilon);
        if (resolved != null)
            return resolved;

        return new ChartEditorTimeSignatureChange { beatPosition = 0.0, numerator = 4, denominator = 4 };
    }

    private static double GetMeasureLengthInQuarterBeats(ChartEditorTimeSignatureChange signature)
    {
        signature ??= new ChartEditorTimeSignatureChange { numerator = 4, denominator = 4 };
        return Math.Max(BeatEpsilon, Math.Max(1, signature.numerator) * GetBeatUnitInQuarterBeats(signature));
    }

    private static double GetBeatUnitInQuarterBeats(ChartEditorTimeSignatureChange signature)
    {
        signature ??= new ChartEditorTimeSignatureChange { numerator = 4, denominator = 4 };
        return 4.0 / Math.Max(1, signature.denominator);
    }

    private static ChartEditorBeatMarker FindBeatMarker(ChartEditorProject project, double beatPosition, bool anchorsOnly = false)
    {
        string key = BeatKey(beatPosition);
        return project?.beatMap?.beatMarkers?
            .FirstOrDefault(marker => marker != null &&
                                      (!anchorsOnly || marker.isAnchor) &&
                                      string.Equals(BeatKey(marker.beatPosition), key, StringComparison.OrdinalIgnoreCase));
    }

    private static ChartEditorBeatMarker FindAnchorById(ChartEditorProject project, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return project?.beatMap?.beatMarkers?
            .FirstOrDefault(marker => marker != null &&
                                      marker.isAnchor &&
                                      string.Equals(marker.id, id, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountAnchors(ChartEditorProject project)
    {
        return project?.beatMap?.beatMarkers?.Count(marker => marker != null && marker.isAnchor) ?? 0;
    }

    private static string FormatAnchorLabel(ChartEditorProject project, ChartEditorBeatMarker marker)
    {
        int count = Math.Max(1, CountAnchors(project));
        if (marker != null && Math.Abs(marker.beatPosition) <= BeatEpsilon)
            return "Start";

        return $"Anchor {count}";
    }

    private static void ShiftProjectContent(ChartEditorProject project, Func<double, bool> predicate, double deltaSeconds)
    {
        if (project.tracks != null)
        {
            foreach (ChartEditorTrack track in project.tracks)
            {
                if (track == null)
                    continue;

                if (track.notes != null)
                {
                    foreach (ChartEditorNote note in track.notes)
                    {
                        if (note == null || !predicate(note.timeSeconds))
                            continue;

                        note.timeSeconds = Math.Max(0.0, note.timeSeconds + deltaSeconds);
                        note.chartTimeSeconds = Math.Max(0.0, note.chartTimeSeconds + deltaSeconds);
                        UpdateNoteBeatTiming(project, note);
                    }

                    track.notes.Sort((a, b) => (a?.timeSeconds ?? 0.0).CompareTo(b?.timeSeconds ?? 0.0));
                }

                List<ChartEditorToneChange> toneChanges = track.tones?.changes;
                if (toneChanges != null)
                {
                    foreach (ChartEditorToneChange change in toneChanges)
                    {
                        if (change == null || !predicate(change.timeSeconds))
                            continue;

                        change.timeSeconds = (float)Math.Max(0.0, change.timeSeconds + deltaSeconds);
                        change.beatPosition = GetBeatPositionForAudioTime(project, change.timeSeconds);
                        change.usesBeatMapTiming = true;
                    }
                }

                if (track.arpeggioGuides != null)
                {
                    foreach (ChartEditorArpeggioGuide guide in track.arpeggioGuides)
                    {
                        if (guide == null || !predicate(guide.startTime))
                            continue;

                        guide.startTime = (float)Math.Max(0.0, guide.startTime + deltaSeconds);
                        guide.endTime = (float)Math.Max(guide.startTime, guide.endTime + deltaSeconds);
                        guide.startBeat = GetBeatPositionForAudioTime(project, guide.startTime);
                        guide.endBeat = Math.Max(guide.startBeat, GetBeatPositionForAudioTime(project, guide.endTime));
                        guide.usesBeatMapTiming = true;
                    }
                }
            }
        }

        if (project.sections != null)
        {
            foreach (ChartEditorSection section in project.sections)
            {
                if (section == null || !predicate(section.startTimeSeconds))
                    continue;

                section.startTimeSeconds = Math.Max(0.0, section.startTimeSeconds + deltaSeconds);
                section.endTimeSeconds = Math.Max(section.startTimeSeconds + 0.05, section.endTimeSeconds + deltaSeconds);
                section.chartStartTimeSeconds = Math.Max(0.0, section.chartStartTimeSeconds + deltaSeconds);
                section.chartEndTimeSeconds = Math.Max(section.chartStartTimeSeconds + 0.05, section.chartEndTimeSeconds + deltaSeconds);
                section.userEdited = true;
                UpdateSectionBeatTiming(project, section);
            }

            NormalizeSections(project);
        }

        if (project.beatMap?.beatMarkers != null)
        {
            foreach (ChartEditorBeatMarker anchor in project.beatMap.beatMarkers.Where(marker => marker != null && marker.isAnchor))
            {
                if (anchor.locked || !predicate(anchor.audioTimeSeconds))
                    continue;

                anchor.audioTimeSeconds = Math.Max(0.0, anchor.audioTimeSeconds + deltaSeconds);
            }
        }

        project.cursorTimeSeconds = Math.Max(0.0, project.cursorTimeSeconds + deltaSeconds);
        RebuildBeatMap(project);
        SyncLegacySyncPointsFromBeatMap(project);
        project.dirty = true;
    }

    private static double GetAudioTimeForBeatInternal(List<ChartEditorTempoRegion> regions, double beatPosition, double defaultBpm)
    {
        if (regions == null || regions.Count == 0)
            return Math.Max(0.0, beatPosition * 60.0 / ClampTempo(defaultBpm));

        beatPosition = Math.Max(0.0, beatPosition);
        ChartEditorTempoRegion first = regions[0];
        if (beatPosition <= first.startBeat)
            return Math.Max(0.0, first.startAudioTimeSeconds + ((beatPosition - first.startBeat) * 60.0 / ClampTempo(first.bpm)));

        // Regions are sorted by beat; binary-search the first region whose
        // endBeat covers the position. Note remaps call this once per note,
        // so a linear scan over SynchTheory's per-beat regions was O(n^2).
        int low = 0;
        int high = regions.Count - 1;
        while (low < high)
        {
            int mid = (low + high) >> 1;
            if (beatPosition > regions[mid].endBeat)
                low = mid + 1;
            else
                high = mid;
        }

        ChartEditorTempoRegion region = regions[low];
        double beatSpan = Math.Max(BeatEpsilon, region.endBeat - region.startBeat);
        double t = Math.Max(0.0, Math.Min(1.0, (beatPosition - region.startBeat) / beatSpan));
        return Mathf.Lerp((float)region.startAudioTimeSeconds, (float)region.endAudioTimeSeconds, (float)t);
    }

    private static double GetBeatPositionForAudioTimeInternal(List<ChartEditorTempoRegion> regions, double audioTimeSeconds, double defaultBpm)
    {
        if (regions == null || regions.Count == 0)
            return Math.Max(0.0, audioTimeSeconds * ClampTempo(defaultBpm) / 60.0);

        audioTimeSeconds = Math.Max(0.0, audioTimeSeconds);
        ChartEditorTempoRegion first = regions[0];
        if (audioTimeSeconds <= first.startAudioTimeSeconds)
            return Math.Max(0.0, first.startBeat + ((audioTimeSeconds - first.startAudioTimeSeconds) * ClampTempo(first.bpm) / 60.0));

        int low = 0;
        int high = regions.Count - 1;
        while (low < high)
        {
            int mid = (low + high) >> 1;
            if (audioTimeSeconds > regions[mid].endAudioTimeSeconds)
                low = mid + 1;
            else
                high = mid;
        }

        ChartEditorTempoRegion region = regions[low];
        double timeSpan = Math.Max(MinAnchorGapSeconds, region.endAudioTimeSeconds - region.startAudioTimeSeconds);
        double t = Math.Max(0.0, Math.Min(1.0, (audioTimeSeconds - region.startAudioTimeSeconds) / timeSpan));
        return Mathf.Lerp((float)region.startBeat, (float)region.endBeat, (float)t);
    }

    private static double ClampTempo(double bpm)
    {
        return Math.Max(MinTempoBpm, Math.Min(MaxTempoBpm, bpm));
    }

    private static string BeatKey(double beatPosition)
    {
        return Math.Round(beatPosition, 4).ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string StableBeatMapId(string prefix, params double[] beats)
    {
        string[] keys = beats?
            .Select(beat => BeatKey(beat).Replace(".", "_"))
            .ToArray() ?? Array.Empty<string>();
        return prefix + "_" + string.Join("_", keys);
    }
}
