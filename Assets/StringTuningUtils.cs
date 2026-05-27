using System;
using System.Collections.Generic;
using System.Linq;

public static class StringTuningUtils
{
    public static readonly int[] StandardGuitarTuning = { 40, 45, 50, 55, 59, 64 };
    public static readonly int[] StandardBassTuning = { 28, 33, 38, 43 };

    private static readonly CommonTuningPreset[] CommonGuitarTunings =
    {
        new CommonTuningPreset("E Standard", new[] { 40, 45, 50, 55, 59, 64 }),
        new CommonTuningPreset("Eb Standard", new[] { 39, 44, 49, 54, 58, 63 }),
        new CommonTuningPreset("D Standard", new[] { 38, 43, 48, 53, 57, 62 }),
        new CommonTuningPreset("C# Standard", new[] { 37, 42, 47, 52, 56, 61 }),
        new CommonTuningPreset("C Standard", new[] { 36, 41, 46, 51, 55, 60 }),
        new CommonTuningPreset("Drop D", new[] { 38, 45, 50, 55, 59, 64 }),
        new CommonTuningPreset("Drop Db", new[] { 37, 44, 49, 54, 58, 63 }),
        new CommonTuningPreset("Drop C", new[] { 36, 43, 48, 53, 57, 62 }),
        new CommonTuningPreset("Drop B", new[] { 35, 42, 47, 52, 56, 61 }),
        new CommonTuningPreset("DADGAD", new[] { 38, 45, 50, 55, 57, 62 }),
        new CommonTuningPreset("Open G", new[] { 38, 43, 50, 55, 59, 62 }),
        new CommonTuningPreset("Open D", new[] { 38, 45, 50, 54, 57, 62 })
    };

    private static readonly CommonTuningPreset[] CommonBassTunings =
    {
        new CommonTuningPreset("E Standard Bass", new[] { 28, 33, 38, 43 }),
        new CommonTuningPreset("Eb Standard Bass", new[] { 27, 32, 37, 42 }),
        new CommonTuningPreset("D Standard Bass", new[] { 26, 31, 36, 41 }),
        new CommonTuningPreset("C# Standard Bass", new[] { 25, 30, 35, 40 }),
        new CommonTuningPreset("C Standard Bass", new[] { 24, 29, 34, 39 }),
        new CommonTuningPreset("Drop D Bass", new[] { 26, 33, 38, 43 }),
        new CommonTuningPreset("Drop Db Bass", new[] { 25, 32, 37, 42 }),
        new CommonTuningPreset("Drop C Bass", new[] { 24, 31, 36, 41 }),
        new CommonTuningPreset("Drop B Bass", new[] { 23, 30, 35, 40 })
    };

    private static readonly string[] FlatPitchNames =
    {
        "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"
    };

    private static readonly Dictionary<string, int> PitchClassByStep = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = 0,
        ["D"] = 2,
        ["E"] = 4,
        ["F"] = 5,
        ["G"] = 7,
        ["A"] = 9,
        ["B"] = 11
    };

    public static bool TryGetPitchClass(string step, out int pitchClass)
    {
        if (string.IsNullOrWhiteSpace(step))
        {
            pitchClass = -1;
            return false;
        }

        return PitchClassByStep.TryGetValue(step.Trim(), out pitchClass);
    }

    public static int[] CloneOrDefault(int[] tuningPitches, bool preferBass = false)
    {
        if (tuningPitches == null || tuningPitches.Length == 0)
            return preferBass
                ? (int[])StandardBassTuning.Clone()
                : (int[])StandardGuitarTuning.Clone();

        return (int[])tuningPitches.Clone();
    }

    public static List<string> GetCommonTuningPresetLabels(bool bass)
    {
        CommonTuningPreset[] presets = bass ? CommonBassTunings : CommonGuitarTunings;
        return presets.Select(preset => preset.Label).ToList();
    }

    public static string GetDefaultCommonTuningPresetLabel(bool bass)
    {
        CommonTuningPreset[] presets = bass ? CommonBassTunings : CommonGuitarTunings;
        return presets.Length > 0 ? presets[0].Label : FormatTuningDisplayName(CloneOrDefault(null, bass));
    }

    public static bool TryGetCommonTuningPresetPitches(bool bass, string label, out int[] pitches)
    {
        CommonTuningPreset[] presets = bass ? CommonBassTunings : CommonGuitarTunings;
        for (int i = 0; i < presets.Length; i++)
        {
            if (!string.Equals(presets[i].Label, label, StringComparison.OrdinalIgnoreCase))
                continue;

            pitches = (int[])presets[i].Pitches.Clone();
            return true;
        }

        pitches = null;
        return false;
    }

    public static string FormatTuningDisplayName(int[] tuningPitches)
    {
        int[] resolved = CloneOrDefault(tuningPitches, preferBass: tuningPitches != null && tuningPitches.Length > 0 && tuningPitches.Length <= 4);
        string commonLabel = FindCommonTuningPresetLabel(resolved);
        if (!string.IsNullOrWhiteSpace(commonLabel))
            return commonLabel;

        if (resolved.Length == 6)
        {
            if (Matches(resolved, new[] { 40, 45, 50, 55, 59, 64 })) return "E Standard";
            if (Matches(resolved, new[] { 39, 44, 49, 54, 58, 63 })) return "Eb Standard";
            if (Matches(resolved, new[] { 38, 43, 48, 53, 57, 62 })) return "D Standard";
            if (Matches(resolved, new[] { 38, 45, 50, 55, 59, 64 })) return "Drop D";
            if (Matches(resolved, new[] { 37, 44, 49, 54, 58, 63 })) return "Drop Db";
            if (Matches(resolved, new[] { 36, 43, 48, 53, 57, 62 })) return "Drop C";
        }
        else if (resolved.Length == 4)
        {
            if (Matches(resolved, new[] { 28, 33, 38, 43 })) return "E Standard Bass";
            if (Matches(resolved, new[] { 27, 32, 37, 42 })) return "Eb Standard Bass";
            if (Matches(resolved, new[] { 26, 31, 36, 41 })) return "D Standard Bass";
            if (Matches(resolved, new[] { 26, 33, 38, 43 })) return "Drop D Bass";
            if (Matches(resolved, new[] { 25, 32, 37, 42 })) return "Drop Db Bass";
            if (Matches(resolved, new[] { 24, 31, 36, 41 })) return "Drop C Bass";
        }

        string joined = string.Join(" ", resolved.Select(FormatMidiNoteNoOctave));
        return $"Custom ({joined})";
    }

    public static string FormatTuningRichText(string label, string value, string hexColor)
    {
        if (string.IsNullOrWhiteSpace(value))
            value = FormatTuningDisplayName(null);

        return $"{label} <color={hexColor}>{value}</color>";
    }

    private static string FindCommonTuningPresetLabel(int[] tuningPitches)
    {
        if (tuningPitches == null || tuningPitches.Length == 0)
            return string.Empty;

        CommonTuningPreset[] presets = tuningPitches.Length <= 4 ? CommonBassTunings : CommonGuitarTunings;
        for (int i = 0; i < presets.Length; i++)
        {
            if (Matches(tuningPitches, presets[i].Pitches))
                return presets[i].Label;
        }

        return string.Empty;
    }

    private static string FormatMidiNoteNoOctave(int midi)
    {
        int pitchClass = Mod(midi, 12);
        return FlatPitchNames[pitchClass];
    }

    private static int Mod(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private static bool Matches(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        if (left == null || right == null || left.Count != right.Count)
            return false;

        for (int i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }

    private sealed class CommonTuningPreset
    {
        public readonly string Label;
        public readonly int[] Pitches;

        public CommonTuningPreset(string label, int[] pitches)
        {
            Label = label ?? string.Empty;
            Pitches = pitches ?? Array.Empty<int>();
        }
    }
}
