using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public static class ChartEditorToneScopeService
{
    public static bool NormalizeProjectToneGroups(ChartEditorProject project)
    {
        if (project?.tracks == null || project.tracks.Count == 0)
            return false;

        bool changed = false;
        foreach (IGrouping<string, ChartEditorTrack> group in project.tracks
                     .Where(track => track != null)
                     .GroupBy(GetArrangementGroupKey, StringComparer.OrdinalIgnoreCase))
        {
            List<ChartEditorTrack> tracks = group.Where(track => track != null).ToList();
            if (tracks.Count <= 1)
                continue;

            ChartEditorTrack source = SelectCanonicalToneTrack(tracks);
            if (source?.tones == null)
                continue;

            source.tones.EnsureDefaults();
            NormalizeToneData(source.tones);
            for (int i = 0; i < tracks.Count; i++)
            {
                ChartEditorTrack target = tracks[i];
                if (target == null || ReferenceEquals(target, source))
                    continue;

                if (!AreToneDataEqual(target.tones, source.tones))
                {
                    target.tones = CloneToneData(source.tones);
                    changed = true;
                }
            }
        }

        return changed;
    }

    public static void PropagateToneDataFromTrack(ChartEditorProject project, ChartEditorTrack source)
    {
        if (project?.tracks == null || source == null)
            return;

        source.tones ??= new ChartEditorToneData();
        source.tones.EnsureDefaults();
        NormalizeToneData(source.tones);
        string groupKey = GetArrangementGroupKey(source);
        for (int i = 0; i < project.tracks.Count; i++)
        {
            ChartEditorTrack target = project.tracks[i];
            if (target == null ||
                ReferenceEquals(target, source) ||
                !string.Equals(GetArrangementGroupKey(target), groupKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            target.tones = CloneToneData(source.tones);
        }
    }

    public static ChartEditorToneData CloneToneData(ChartEditorToneData source)
    {
        ChartEditorToneData clone = new ChartEditorToneData
        {
            baseToneName = source?.baseToneName ?? string.Empty,
            changes = new List<ChartEditorToneChange>(),
            definitions = new List<ChartEditorToneDefinition>()
        };

        if (source?.changes != null)
        {
            for (int i = 0; i < source.changes.Count; i++)
            {
                ChartEditorToneChange change = source.changes[i];
                if (change == null)
                    continue;

                clone.changes.Add(new ChartEditorToneChange
                {
                    timeSeconds = change.timeSeconds,
                    toneName = change.toneName ?? string.Empty,
                    toneId = change.toneId,
                    // Beat tracking must survive propagation or the variants'
                    // tone switches stop following beat-map edits.
                    beatPosition = change.beatPosition,
                    usesBeatMapTiming = change.usesBeatMapTiming
                });
            }
        }

        if (source?.definitions != null)
        {
            for (int i = 0; i < source.definitions.Count; i++)
            {
                ChartEditorToneDefinition definition = source.definitions[i];
                if (definition == null)
                    continue;

                clone.definitions.Add(new ChartEditorToneDefinition
                {
                    name = definition.name ?? string.Empty,
                    key = definition.key ?? string.Empty,
                    preset = CloneTonePreset(definition.preset),
                    fallback = CloneToneFallback(definition.fallback)
                });
            }
        }

        clone.EnsureDefaults();
        NormalizeToneData(clone);
        return clone;
    }

    private static ChartEditorTrack SelectCanonicalToneTrack(IReadOnlyList<ChartEditorTrack> tracks)
    {
        return tracks
            .Where(track => track != null)
            .OrderBy(track => GetToneDataScore(track) > 0 ? 0 : 1)
            .ThenBy(ResolveCanonicalDifficultyRank)
            .ThenByDescending(GetToneDataScore)
            .ThenBy(track => track.id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static int ResolveCanonicalDifficultyRank(ChartEditorTrack track)
    {
        if (track == null)
            return int.MaxValue;

        if (track.difficultyUiIndex == 0 ||
            string.Equals(track.difficultyLabel?.Trim(), "Full", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(track.difficultyLabel?.Trim(), "Max", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return 1;
    }

    private static int GetToneDataScore(ChartEditorTrack track)
    {
        ChartEditorToneData tones = track?.tones;
        if (tones == null)
            return 0;

        int score = 0;
        score += tones.changes?.Count(change => change != null) * 10 ?? 0;
        score += tones.definitions?.Count(definition => definition != null) * 20 ?? 0;
        if (tones.definitions != null)
        {
            score += tones.definitions
                .Where(definition => definition?.preset?.pedalChain != null)
                .Sum(definition => definition.preset.pedalChain.Count);
        }

        if (!string.IsNullOrWhiteSpace(tones.baseToneName))
            score += 5;
        return score;
    }

    private static string GetArrangementGroupKey(ChartEditorTrack track)
    {
        string key = string.IsNullOrWhiteSpace(track?.arrangementGroupId)
            ? track?.id
            : track.arrangementGroupId;
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }

    private static void NormalizeToneData(ChartEditorToneData tones)
    {
        if (tones == null)
            return;

        tones.EnsureDefaults();
        tones.changes = tones.changes
            .Where(change => change != null)
            .OrderBy(change => change.timeSeconds)
            .ThenBy(change => change.toneName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ChartEditorTonePresetData CloneTonePreset(ChartEditorTonePresetData source)
    {
        ChartEditorTonePresetData clone = new ChartEditorTonePresetData
        {
            presetId = source?.presetId ?? string.Empty,
            presetName = source?.presetName ?? string.Empty,
            inputGainDb = source?.inputGainDb ?? 0f,
            outputGainDb = source?.outputGainDb ?? 0f,
            pedalChain = new List<ChartEditorTonePedalSlotData>()
        };

        if (source?.pedalChain != null)
        {
            for (int i = 0; i < source.pedalChain.Count; i++)
            {
                ChartEditorTonePedalSlotData slot = source.pedalChain[i];
                if (slot == null)
                    continue;

                clone.pedalChain.Add(new ChartEditorTonePedalSlotData
                {
                    instanceId = slot.instanceId ?? string.Empty,
                    pedalType = slot.pedalType ?? string.Empty,
                    descriptorId = slot.descriptorId ?? string.Empty,
                    enabled = slot.enabled,
                    settingsJson = slot.settingsJson ?? string.Empty
                });
            }
        }

        clone.EnsureDefaults();
        return clone;
    }

    private static ChartEditorToneFallbackData CloneToneFallback(ChartEditorToneFallbackData source)
    {
        return new ChartEditorToneFallbackData
        {
            preferredPresetName = source?.preferredPresetName ?? string.Empty,
            searchText = source?.searchText ?? string.Empty
        };
    }

    private static bool AreToneDataEqual(ChartEditorToneData left, ChartEditorToneData right)
    {
        return string.Equals(BuildToneSignature(left), BuildToneSignature(right), StringComparison.Ordinal);
    }

    private static string BuildToneSignature(ChartEditorToneData tones)
    {
        if (tones == null)
            return string.Empty;

        NormalizeToneData(tones);
        List<string> parts = new List<string> { tones.baseToneName ?? string.Empty };
        if (tones.changes != null)
        {
            parts.AddRange(tones.changes.Select(change => string.Join("|",
                "change",
                change.timeSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                change.toneName ?? string.Empty,
                change.toneId.ToString(CultureInfo.InvariantCulture))));
        }

        if (tones.definitions != null)
        {
            parts.AddRange(tones.definitions.Select(definition => string.Join("|",
                "definition",
                definition?.name ?? string.Empty,
                definition?.key ?? string.Empty,
                JsonUtility.ToJson(definition?.preset, false) ?? string.Empty,
                JsonUtility.ToJson(definition?.fallback, false) ?? string.Empty)));
        }

        return string.Join("\n", parts);
    }
}
