using System.Collections.Generic;
using Rocksmith2014.XML;

internal static class RocksmithImportTimingExporter
{
    public static CachedArrangementTimingData Build(InstrumentalArrangement arrangement)
    {
        CachedArrangementTimingData timing = new CachedArrangementTimingData();
        if (arrangement == null)
            return timing;

        timing.averageTempoBpm = arrangement.MetaData != null && arrangement.MetaData.AverageTempo > 0f
            ? arrangement.MetaData.AverageTempo
            : 120f;
        timing.capo = arrangement.MetaData?.Capo ?? 0;
        timing.ebeats = new List<CachedEbeatData>(arrangement.Ebeats?.Count ?? 0);
        timing.sections = new List<CachedSectionData>(arrangement.Sections?.Count ?? 0);

        if (arrangement.Ebeats != null)
        {
            for (int i = 0; i < arrangement.Ebeats.Count; i++)
            {
                Ebeat ebeat = arrangement.Ebeats[i];
                timing.ebeats.Add(new CachedEbeatData
                {
                    timeSeconds = ebeat.Time / 1000f,
                    measure = ebeat.Measure
                });
            }
        }

        if (arrangement.Sections != null)
        {
            for (int i = 0; i < arrangement.Sections.Count; i++)
            {
                Section section = arrangement.Sections[i];
                if (section == null)
                    continue;

                timing.sections.Add(new CachedSectionData
                {
                    name = section.Name ?? string.Empty,
                    number = section.Number,
                    timeSeconds = section.Time / 1000f
                });
            }

            timing.sections.Sort((left, right) =>
            {
                int timeCompare = left.timeSeconds.CompareTo(right.timeSeconds);
                return timeCompare != 0 ? timeCompare : left.number.CompareTo(right.number);
            });
        }

        return timing;
    }
}
