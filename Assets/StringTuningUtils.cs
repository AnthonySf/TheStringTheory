using System;
using System.Collections.Generic;
using System.Linq;

public static class StringTuningUtils
{
    public static readonly int[] StandardGuitarTuning = { 40, 45, 50, 55, 59, 64 };
    public static readonly int[] StandardBassTuning = { 28, 33, 38, 43 };

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

    public static string FormatTuningDisplayName(int[] tuningPitches)
    {
        int[] resolved = CloneOrDefault(tuningPitches, preferBass: tuningPitches != null && tuningPitches.Length > 0 && tuningPitches.Length <= 4);
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
}
