using System;
using System.Collections.Generic;
using System.Linq;
using AlphaTab.Model;
using UnityEngine;

internal sealed class AlphaTabGpTimelineTempoChange
{
    public double beatPosition;
    public double timeSeconds;
    public double bpm;
}

internal sealed class AlphaTabGpTimelineTimeSignatureChange
{
    public double beatPosition;
    public int numerator;
    public int denominator;
}

internal sealed class AlphaTabGpTimelineSectionMarker
{
    public string name;
    public float timeSeconds;
    public int index;
}

internal sealed class AlphaTabGpTimelineData
{
    public double defaultTempoBpm = 120.0;
    public float durationSeconds;
    public readonly List<AlphaTabGpTimelineTempoChange> tempoChanges = new List<AlphaTabGpTimelineTempoChange>();
    public readonly List<AlphaTabGpTimelineTimeSignatureChange> timeSignatures = new List<AlphaTabGpTimelineTimeSignatureChange>();
    public readonly List<AlphaTabGpTimelineSectionMarker> sections = new List<AlphaTabGpTimelineSectionMarker>();
}

internal static class AlphaTabGpTimelineLoader
{
    public static bool TryLoadTimeline(string filePath, out AlphaTabGpTimelineData timeline)
    {
        timeline = null;
        AlphaTabGpScoreData data = AlphaTabGpScoreCache.GetOrLoad(filePath);
        if (data?.score == null)
            return false;

        timeline = new AlphaTabGpTimelineData
        {
            defaultTempoBpm = ResolveDefaultTempo(data),
            durationSeconds = ResolveDurationSeconds(data)
        };

        AddTempoChanges(timeline, data);
        AddTimeSignatures(timeline, data);
        AddSections(timeline, data);
        return true;
    }

    private static double ResolveDefaultTempo(AlphaTabGpScoreData data)
    {
        AlphaTabGpTempoPoint firstTempo = data?.tempoPoints?
            .OrderBy(point => point.tick)
            .FirstOrDefault(point => point != null && point.bpm > 0.0);
        if (firstTempo != null)
            return Math.Max(1.0, firstTempo.bpm);

        return Math.Max(1.0, data?.score?.Tempo ?? 120.0);
    }

    private static float ResolveDurationSeconds(AlphaTabGpScoreData data)
    {
        long endTick = data?.matchedNotes != null && data.matchedNotes.Count > 0
            ? data.matchedNotes.Max(note => Math.Max(note.startTick, note.endTick))
            : 0L;
        return (float)Math.Max(0.0, AlphaTabGpLoader.TickToSeconds(endTick, data?.tempoPoints, data?.midiDivision ?? 1.0));
    }

    private static void AddTempoChanges(AlphaTabGpTimelineData timeline, AlphaTabGpScoreData data)
    {
        IEnumerable<AlphaTabGpTempoPoint> source = data?.tempoPoints ?? Enumerable.Empty<AlphaTabGpTempoPoint>();
        foreach (AlphaTabGpTempoPoint point in source
                     .Where(point => point != null)
                     .GroupBy(point => Math.Max(0L, point.tick))
                     .Select(group => group.First())
                     .OrderBy(point => point.tick))
        {
            long tick = Math.Max(0L, point.tick);
            timeline.tempoChanges.Add(new AlphaTabGpTimelineTempoChange
            {
                beatPosition = TickToBeatPosition(tick, data),
                timeSeconds = Math.Max(0.0, AlphaTabGpLoader.TickToSeconds(tick, data.tempoPoints, data.midiDivision)),
                bpm = Math.Max(1.0, point.bpm)
            });
        }

        if (timeline.tempoChanges.Count == 0 || timeline.tempoChanges[0].beatPosition > 0.0001)
        {
            timeline.tempoChanges.Insert(0, new AlphaTabGpTimelineTempoChange
            {
                beatPosition = 0.0,
                timeSeconds = 0.0,
                bpm = timeline.defaultTempoBpm
            });
        }
    }

    private static void AddTimeSignatures(AlphaTabGpTimelineData timeline, AlphaTabGpScoreData data)
    {
        int lastNumerator = -1;
        int lastDenominator = -1;
        if (data?.score?.MasterBars != null)
        {
            foreach (MasterBar bar in data.score.MasterBars
                         .Where(bar => bar != null)
                         .OrderBy(bar => RoundTick(bar.Start)))
            {
                int numerator = Math.Max(1, Mathf.RoundToInt((float)bar.TimeSignatureNumerator));
                int denominator = Math.Max(1, Mathf.RoundToInt((float)bar.TimeSignatureDenominator));
                if (numerator == lastNumerator && denominator == lastDenominator)
                    continue;

                timeline.timeSignatures.Add(new AlphaTabGpTimelineTimeSignatureChange
                {
                    beatPosition = TickToBeatPosition(RoundTick(bar.Start), data),
                    numerator = numerator,
                    denominator = denominator
                });
                lastNumerator = numerator;
                lastDenominator = denominator;
            }
        }

        if (timeline.timeSignatures.Count == 0 || timeline.timeSignatures[0].beatPosition > 0.0001)
        {
            timeline.timeSignatures.Insert(0, new AlphaTabGpTimelineTimeSignatureChange
            {
                beatPosition = 0.0,
                numerator = 4,
                denominator = 4
            });
        }
    }

    private static void AddSections(AlphaTabGpTimelineData timeline, AlphaTabGpScoreData data)
    {
        if (data?.score?.MasterBars == null)
            return;

        int index = 0;
        foreach (MasterBar bar in data.score.MasterBars
                     .Where(bar => bar != null)
                     .OrderBy(bar => RoundTick(bar.Start)))
        {
            if (!bar.IsSectionStart && bar.Section == null)
                continue;

            string name = FirstNonEmpty(bar.Section?.Marker, bar.Section?.Text);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            long tick = RoundTick(bar.Start);
            timeline.sections.Add(new AlphaTabGpTimelineSectionMarker
            {
                name = name,
                timeSeconds = (float)Math.Max(0.0, AlphaTabGpLoader.TickToSeconds(tick, data.tempoPoints, data.midiDivision)),
                index = index++
            });
        }
    }

    private static long RoundTick(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return 0L;
        return Math.Max(0L, Convert.ToInt64(Math.Round(value, MidpointRounding.AwayFromZero)));
    }

    private static double TickToBeatPosition(long tick, AlphaTabGpScoreData data)
    {
        return Math.Max(0.0, tick / Math.Max(1.0, data?.midiDivision ?? 1.0));
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
}
