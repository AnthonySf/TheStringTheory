using System;

public static class DrumLaneMapper
{
    public const int LaneCount = 8;
    public const int HiHatLane = 0;
    public const int CrashLane = 1;
    public const int SnareLane = 2;
    public const int HighTomLane = 3;
    public const int KickLane = 4;
    public const int MidTomLane = 5;
    public const int FloorTomLane = 6;
    public const int RideLane = 7;

    private static readonly string[] LaneLabels =
    {
        "Hat",
        "Crash",
        "Snare",
        "T1",
        "Kick",
        "T2",
        "Floor",
        "Ride"
    };

    public static string GetLaneLabel(int lane)
    {
        return lane >= 0 && lane < LaneLabels.Length ? LaneLabels[lane] : lane.ToString();
    }

    public static int MapGeneralMidiToLane(int midi)
    {
        switch (midi)
        {
            case 35:
            case 36:
                return KickLane;
            case 37:
            case 38:
            case 39:
            case 40:
                return SnareLane;
            case 42:
            case 44:
            case 46:
                return HiHatLane;
            case 48:
            case 50:
                return HighTomLane;
            case 45:
            case 47:
                return MidTomLane;
            case 41:
            case 43:
                return FloorTomLane;
            case 49:
            case 52:
            case 55:
            case 57:
                return CrashLane;
            case 51:
            case 53:
            case 59:
                return RideLane;
            default:
                return RideLane;
        }
    }

    public static bool TryResolveLaneFromLabel(string label, out int lane)
    {
        lane = -1;
        if (string.IsNullOrWhiteSpace(label))
            return false;

        string normalized = label.Trim().ToLowerInvariant();
        if (normalized.Contains("kick") || normalized.Contains("bass drum"))
        {
            lane = KickLane;
            return true;
        }

        if (normalized.Contains("snare") || normalized.Contains("rim") || normalized.Contains("clap"))
        {
            lane = SnareLane;
            return true;
        }

        if (normalized.Contains("hat") || normalized.Contains("hihat") || normalized.Contains("hi-hat"))
        {
            lane = HiHatLane;
            return true;
        }

        if (normalized.Contains("ride") || normalized.Contains("bell"))
        {
            lane = RideLane;
            return true;
        }

        if (normalized.Contains("crash") ||
            normalized.Contains("splash") ||
            normalized.Contains("china") ||
            normalized.Contains("cymbal"))
        {
            lane = CrashLane;
            return true;
        }

        if (normalized.Contains("tom"))
        {
            if (normalized.Contains("floor") ||
                normalized.Contains("low") ||
                normalized.Contains("tom 3") ||
                normalized.Contains("tom3"))
            {
                lane = FloorTomLane;
                return true;
            }

            if (normalized.Contains("mid") ||
                normalized.Contains("middle") ||
                normalized.Contains("tom 2") ||
                normalized.Contains("tom2"))
            {
                lane = MidTomLane;
                return true;
            }

            if (normalized.Contains("high") ||
                normalized.Contains("rack") ||
                normalized.Contains("small") ||
                normalized.Contains("tom 1") ||
                normalized.Contains("tom1"))
            {
                lane = HighTomLane;
                return true;
            }

            lane = MidTomLane;
            return true;
        }

        if (normalized.Contains("tambourine") ||
            normalized.Contains("cowbell") ||
            normalized.Contains("clave") ||
            normalized.Contains("woodblock") ||
            normalized.Contains("percussion"))
        {
            lane = RideLane;
            return true;
        }

        return false;
    }

    public static string GetGeneralMidiDrumName(int midi)
    {
        switch (midi)
        {
            case 35:
            case 36:
                return "Kick";
            case 37:
                return "Side Stick";
            case 38:
            case 39:
            case 40:
                return "Snare";
            case 41:
            case 43:
                return "Floor Tom";
            case 42:
            case 44:
            case 46:
                return "Hi-Hat";
            case 45:
            case 47:
                return "Mid Tom";
            case 48:
            case 50:
                return "High Tom";
            case 49:
            case 52:
            case 55:
            case 57:
                return "Crash Cymbal";
            case 51:
            case 53:
            case 59:
                return "Ride Cymbal";
            default:
                return string.Empty;
        }
    }
}
