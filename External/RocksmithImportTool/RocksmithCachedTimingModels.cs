using System.Collections.Generic;

internal sealed class CachedArrangementTimingData
{
    public float averageTempoBpm = 120f;
    public int capo;
    public List<CachedEbeatData> ebeats = new List<CachedEbeatData>();
    public List<CachedSectionData> sections = new List<CachedSectionData>();
}

internal sealed class CachedEbeatData
{
    public float timeSeconds;
    public short measure = -1;
}

internal sealed class CachedSectionData
{
    public string name = string.Empty;
    public short number;
    public float timeSeconds;
}
