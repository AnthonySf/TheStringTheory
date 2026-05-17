using System.Collections.Generic;

internal sealed class CachedArrangementTimingData
{
    public float averageTempoBpm = 120f;
    public int capo;
    public List<CachedEbeatData> ebeats = new List<CachedEbeatData>();
}

internal sealed class CachedEbeatData
{
    public float timeSeconds;
    public short measure = -1;
}
