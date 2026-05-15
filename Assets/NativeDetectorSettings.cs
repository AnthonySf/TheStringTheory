using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[Serializable]
public sealed class NativeDetectorSettingsData
{
    public float chordLeniency = 0.66f;
    public float continuousRmsGate = 0.007f;
    public float continuousConfidenceGate = 0.65f;
    public float continuousHoldSeconds = 0.10f;
    public int highStringMinMidi = 64;
    public float highStringRmsMultiplier = 0.50f;
    public float highStringConfidenceMultiplier = 0.70f;
    public int highStringBenefitMatchMaxDistance;
    public float onsetExpectLookaheadSeconds = 0.120f;
    public float standardOnsetThreshold = 0.20f;
    public float highStringOnsetThreshold = 0.12f;
    public float expectExactBonus = 1.50f;
    public float expectNearBonus1 = 0.80f;
    public float expectNearBonus2 = 0.35f;
    public int expectMaxDistanceAi = 2;
    public int expectMaxDistanceContinuous = 2;
    public float expectStrictConfidence = 0.80f;
    public float chordScoreKeepRatio = 0.72f;
    public float chordExpectedScoreKeepRatio = 0.62f;
}

[Serializable]
public sealed class NativeDetectorPresetStore
{
    public string selectedPresetId = NativeDetectorSettingCatalog.DefaultPresetId;
    public NativeDetectorSettingsData customSettings = NativeDetectorSettingCatalog.CreateLevel2();
}

[Serializable]
public sealed class NativeDetectorSettingSnapshot
{
    public string key;
    public string label;
    public string description;
    public float value;
    public float min;
    public float max;
    public bool wholeNumbers;
    public string valueText;
}

public sealed class NativeDetectorPresetDescriptor
{
    public NativeDetectorPresetDescriptor(string id, string label, bool builtIn)
    {
        Id = id;
        Label = label;
        IsBuiltIn = builtIn;
    }

    public string Id { get; }
    public string Label { get; }
    public bool IsBuiltIn { get; }
}

public sealed class NativeDetectorSettingDefinition
{
    public NativeDetectorSettingDefinition(
        string key,
        string label,
        string description,
        float min,
        float max,
        bool wholeNumbers,
        int decimals,
        bool isAdvanced = false)
    {
        Key = key;
        Label = label;
        Description = description;
        Min = min;
        Max = max;
        WholeNumbers = wholeNumbers;
        Decimals = decimals;
        IsAdvanced = isAdvanced;
    }

    public string Key { get; }
    public string Label { get; }
    public string Description { get; }
    public float Min { get; }
    public float Max { get; }
    public bool WholeNumbers { get; }
    public int Decimals { get; }
    public bool IsAdvanced { get; }
}

public static class NativeDetectorSettingCatalog
{
    public const string DefaultPresetId = "level2";
    public const string CustomPresetId = "custom";

    private static readonly List<NativeDetectorPresetDescriptor> presetDescriptors = new List<NativeDetectorPresetDescriptor>
    {
        new NativeDetectorPresetDescriptor("level1", "Level 1 / Default", true),
        new NativeDetectorPresetDescriptor("level2", "Level 2 / Tight", true),
        new NativeDetectorPresetDescriptor("level3", "Level 3 / Strict", true),
        new NativeDetectorPresetDescriptor(CustomPresetId, "Custom", false)
    };

    private static readonly List<NativeDetectorSettingDefinition> definitions = new List<NativeDetectorSettingDefinition>
    {
        new NativeDetectorSettingDefinition("chordLeniency", "Chord Leniency", "Fraction of expected chord tones that must ring before a multi-note chord is accepted. Dyads still require both notes.", 0.34f, 1.00f, false, 2),
        new NativeDetectorSettingDefinition("continuousRmsGate", "Continuous RMS Gate", "Minimum signal energy needed for continuous fast-path note tracking.", 0.001f, 0.05f, false, 3),
        new NativeDetectorSettingDefinition("continuousConfidenceGate", "Continuous Confidence Gate", "Minimum aubio pitch confidence required before a continuous note is accepted.", 0.00f, 0.99f, false, 2),
        new NativeDetectorSettingDefinition("continuousHoldSeconds", "Continuous Hold", "How long a detected fast-path note stays alive before clearing.", 0.01f, 0.50f, false, 2),
        new NativeDetectorSettingDefinition("highStringMinMidi", "High-String Split", "Expected MIDI note where the detector begins using relaxed high-string behavior.", 36f, 96f, true, 0),
        new NativeDetectorSettingDefinition("highStringRmsMultiplier", "High-String RMS Multiplier", "Extra sensitivity applied to high strings for the RMS gate.", 0.10f, 1.50f, false, 2),
        new NativeDetectorSettingDefinition("highStringConfidenceMultiplier", "High-String Confidence Multiplier", "Confidence relaxation applied only in the high register.", 0.10f, 1.00f, false, 2),
        new NativeDetectorSettingDefinition("highStringBenefitMatchMaxDistance", "High-String Match Distance", "Semitone distance allowed for relaxed fast-path acceptance on high strings.", 0f, 2f, true, 0, isAdvanced: true),
        new NativeDetectorSettingDefinition("onsetExpectLookaheadSeconds", "Onset Lookahead", "How far ahead expected notes are consulted when adapting onset sensitivity.", 0.00f, 0.40f, false, 3, isAdvanced: true),
        new NativeDetectorSettingDefinition("standardOnsetThreshold", "Standard Onset Threshold", "Aubio HFC onset threshold for normal strings.", 0.01f, 1.00f, false, 2),
        new NativeDetectorSettingDefinition("highStringOnsetThreshold", "High-String Onset Threshold", "Aubio HFC onset threshold used when high-string context is detected.", 0.01f, 1.00f, false, 2),
        new NativeDetectorSettingDefinition("expectExactBonus", "Exact Match Bonus", "Score bonus applied to AI note candidates that match the expected note exactly.", 0.00f, 5.00f, false, 2, isAdvanced: true),
        new NativeDetectorSettingDefinition("expectNearBonus1", "Near Match Bonus 1", "Score bonus for AI note candidates one semitone away from the expectation.", 0.00f, 3.00f, false, 2, isAdvanced: true),
        new NativeDetectorSettingDefinition("expectNearBonus2", "Near Match Bonus 2", "Score bonus for AI note candidates within the max AI hint distance.", 0.00f, 3.00f, false, 2, isAdvanced: true),
        new NativeDetectorSettingDefinition("expectMaxDistanceAi", "Max AI Hint Distance", "Maximum semitone distance where expected notes still help AI scoring.", 0f, 4f, true, 0, isAdvanced: true),
        new NativeDetectorSettingDefinition("expectMaxDistanceContinuous", "Max Fast Hint Distance", "Maximum semitone distance where expected notes still help fast-path tracking.", 0f, 4f, true, 0, isAdvanced: true),
        new NativeDetectorSettingDefinition("expectStrictConfidence", "Strict Confidence", "Fast-path confidence required before notes outside the expected distance are still accepted.", 0.00f, 0.99f, false, 2, isAdvanced: true),
        new NativeDetectorSettingDefinition("chordScoreKeepRatio", "Chord Keep Ratio", "Minimum relative AI chord score kept when no exact expected note is present.", 0.00f, 0.99f, false, 2, isAdvanced: true),
        new NativeDetectorSettingDefinition("chordExpectedScoreKeepRatio", "Expected Chord Keep Ratio", "Minimum relative AI chord score kept when an exact expected note is present.", 0.00f, 0.99f, false, 2, isAdvanced: true)
    };

    public static IReadOnlyList<NativeDetectorPresetDescriptor> PresetDescriptors => presetDescriptors;
    public static IReadOnlyList<NativeDetectorSettingDefinition> Definitions => definitions;

    public static NativeDetectorSettingsData CreateLevel1()
    {
        return new NativeDetectorSettingsData();
    }

    public static NativeDetectorSettingsData CreateLevel2()
    {
        NativeDetectorSettingsData settings = CreateLevel1();
        settings.chordLeniency = Clamp(settings.chordLeniency + 0.06f, 0f, 1f);
        settings.continuousConfidenceGate = Clamp(settings.continuousConfidenceGate + 0.08f, 0f, 0.99f);
        settings.highStringConfidenceMultiplier = Clamp(settings.highStringConfidenceMultiplier + 0.12f, 0f, 1f);
        settings.standardOnsetThreshold += 0.03f;
        settings.highStringOnsetThreshold += 0.02f;
        settings.expectExactBonus += 0.50f;
        settings.expectNearBonus1 = Mathf.Max(0f, settings.expectNearBonus1 - 0.25f);
        settings.expectNearBonus2 = Mathf.Max(0f, settings.expectNearBonus2 - 0.15f);
        settings.expectMaxDistanceAi = Mathf.Max(1, settings.expectMaxDistanceAi - 1);
        settings.expectMaxDistanceContinuous = Mathf.Max(1, settings.expectMaxDistanceContinuous - 1);
        settings.expectStrictConfidence = Clamp(settings.expectStrictConfidence + 0.08f, 0f, 0.99f);
        settings.chordScoreKeepRatio = Clamp(settings.chordScoreKeepRatio + 0.08f, 0f, 0.99f);
        settings.chordExpectedScoreKeepRatio = Clamp(settings.chordExpectedScoreKeepRatio + 0.08f, 0f, 0.99f);
        return settings;
    }

    public static NativeDetectorSettingsData CreateLevel3()
    {
        NativeDetectorSettingsData settings = CreateLevel1();
        settings.chordLeniency = Clamp(settings.chordLeniency + 0.14f, 0f, 1f);
        settings.continuousConfidenceGate = Clamp(settings.continuousConfidenceGate + 0.15f, 0f, 0.995f);
        settings.highStringConfidenceMultiplier = Clamp(settings.highStringConfidenceMultiplier + 0.20f, 0f, 1f);
        settings.standardOnsetThreshold += 0.06f;
        settings.highStringOnsetThreshold += 0.04f;
        settings.expectExactBonus += 0.90f;
        settings.expectNearBonus1 = Mathf.Max(0f, settings.expectNearBonus1 - 0.40f);
        settings.expectNearBonus2 = Mathf.Max(0f, settings.expectNearBonus2 - 0.25f);
        settings.expectMaxDistanceAi = 1;
        settings.expectMaxDistanceContinuous = 1;
        settings.expectStrictConfidence = Clamp(settings.expectStrictConfidence + 0.12f, 0f, 0.995f);
        settings.chordScoreKeepRatio = Clamp(settings.chordScoreKeepRatio + 0.13f, 0f, 0.995f);
        settings.chordExpectedScoreKeepRatio = Clamp(settings.chordExpectedScoreKeepRatio + 0.13f, 0f, 0.995f);
        return settings;
    }

    public static NativeDetectorSettingsData CreatePresetById(string presetId)
    {
        switch ((presetId ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "level1":
                return CreateLevel1();
            case "level3":
                return CreateLevel3();
            case "level2":
            default:
                return CreateLevel2();
        }
    }

    public static NativeDetectorSettingsData Clone(NativeDetectorSettingsData source)
    {
        if (source == null)
            return CreateLevel2();

        return new NativeDetectorSettingsData
        {
            chordLeniency = source.chordLeniency,
            continuousRmsGate = source.continuousRmsGate,
            continuousConfidenceGate = source.continuousConfidenceGate,
            continuousHoldSeconds = source.continuousHoldSeconds,
            highStringMinMidi = source.highStringMinMidi,
            highStringRmsMultiplier = source.highStringRmsMultiplier,
            highStringConfidenceMultiplier = source.highStringConfidenceMultiplier,
            highStringBenefitMatchMaxDistance = source.highStringBenefitMatchMaxDistance,
            onsetExpectLookaheadSeconds = source.onsetExpectLookaheadSeconds,
            standardOnsetThreshold = source.standardOnsetThreshold,
            highStringOnsetThreshold = source.highStringOnsetThreshold,
            expectExactBonus = source.expectExactBonus,
            expectNearBonus1 = source.expectNearBonus1,
            expectNearBonus2 = source.expectNearBonus2,
            expectMaxDistanceAi = source.expectMaxDistanceAi,
            expectMaxDistanceContinuous = source.expectMaxDistanceContinuous,
            expectStrictConfidence = source.expectStrictConfidence,
            chordScoreKeepRatio = source.chordScoreKeepRatio,
            chordExpectedScoreKeepRatio = source.chordExpectedScoreKeepRatio
        };
    }

    public static string GetPresetLabel(string presetId)
    {
        foreach (NativeDetectorPresetDescriptor descriptor in presetDescriptors)
        {
            if (string.Equals(descriptor.Id, presetId, StringComparison.OrdinalIgnoreCase))
                return descriptor.Label;
        }

        return presetDescriptors[1].Label;
    }

    public static int GetPresetIndex(string presetId)
    {
        for (int i = 0; i < presetDescriptors.Count; i++)
        {
            if (string.Equals(presetDescriptors[i].Id, presetId, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 1;
    }

    public static string GetPresetIdAtIndex(int index)
    {
        if (index < 0 || index >= presetDescriptors.Count)
            return DefaultPresetId;

        return presetDescriptors[index].Id;
    }

    public static List<string> BuildPresetLabels()
    {
        List<string> labels = new List<string>(presetDescriptors.Count);
        for (int i = 0; i < presetDescriptors.Count; i++)
            labels.Add(presetDescriptors[i].Label);
        return labels;
    }

    public static NativeDetectorSettingsData Sanitize(NativeDetectorSettingsData source)
    {
        NativeDetectorSettingsData settings = Clone(source);
        settings.chordLeniency = Clamp(settings.chordLeniency, 0.34f, 1.00f);
        settings.continuousRmsGate = Clamp(settings.continuousRmsGate, 0.001f, 0.05f);
        settings.continuousConfidenceGate = Clamp(settings.continuousConfidenceGate, 0f, 0.99f);
        settings.continuousHoldSeconds = Clamp(settings.continuousHoldSeconds, 0.01f, 0.50f);
        settings.highStringMinMidi = Mathf.Clamp(settings.highStringMinMidi, 36, 96);
        settings.highStringRmsMultiplier = Clamp(settings.highStringRmsMultiplier, 0.10f, 1.50f);
        settings.highStringConfidenceMultiplier = Clamp(settings.highStringConfidenceMultiplier, 0.10f, 1.00f);
        settings.highStringBenefitMatchMaxDistance = Mathf.Clamp(settings.highStringBenefitMatchMaxDistance, 0, 2);
        settings.onsetExpectLookaheadSeconds = Clamp(settings.onsetExpectLookaheadSeconds, 0.0f, 0.40f);
        settings.standardOnsetThreshold = Clamp(settings.standardOnsetThreshold, 0.01f, 1.00f);
        settings.highStringOnsetThreshold = Clamp(settings.highStringOnsetThreshold, 0.01f, 1.00f);
        settings.expectExactBonus = Clamp(settings.expectExactBonus, 0.0f, 5.0f);
        settings.expectNearBonus1 = Clamp(settings.expectNearBonus1, 0.0f, 3.0f);
        settings.expectNearBonus2 = Clamp(settings.expectNearBonus2, 0.0f, 3.0f);
        settings.expectMaxDistanceAi = Mathf.Clamp(settings.expectMaxDistanceAi, 0, 4);
        settings.expectMaxDistanceContinuous = Mathf.Clamp(settings.expectMaxDistanceContinuous, 0, 4);
        settings.expectStrictConfidence = Clamp(settings.expectStrictConfidence, 0.0f, 0.99f);
        settings.chordScoreKeepRatio = Clamp(settings.chordScoreKeepRatio, 0.0f, 0.99f);
        settings.chordExpectedScoreKeepRatio = Clamp(settings.chordExpectedScoreKeepRatio, 0.0f, 0.99f);
        return settings;
    }

    public static float GetSettingValue(NativeDetectorSettingsData settings, string key)
    {
        if (settings == null)
            settings = CreateLevel2();

        switch (key)
        {
            case "chordLeniency": return settings.chordLeniency;
            case "continuousRmsGate": return settings.continuousRmsGate;
            case "continuousConfidenceGate": return settings.continuousConfidenceGate;
            case "continuousHoldSeconds": return settings.continuousHoldSeconds;
            case "highStringMinMidi": return settings.highStringMinMidi;
            case "highStringRmsMultiplier": return settings.highStringRmsMultiplier;
            case "highStringConfidenceMultiplier": return settings.highStringConfidenceMultiplier;
            case "highStringBenefitMatchMaxDistance": return settings.highStringBenefitMatchMaxDistance;
            case "onsetExpectLookaheadSeconds": return settings.onsetExpectLookaheadSeconds;
            case "standardOnsetThreshold": return settings.standardOnsetThreshold;
            case "highStringOnsetThreshold": return settings.highStringOnsetThreshold;
            case "expectExactBonus": return settings.expectExactBonus;
            case "expectNearBonus1": return settings.expectNearBonus1;
            case "expectNearBonus2": return settings.expectNearBonus2;
            case "expectMaxDistanceAi": return settings.expectMaxDistanceAi;
            case "expectMaxDistanceContinuous": return settings.expectMaxDistanceContinuous;
            case "expectStrictConfidence": return settings.expectStrictConfidence;
            case "chordScoreKeepRatio": return settings.chordScoreKeepRatio;
            case "chordExpectedScoreKeepRatio": return settings.chordExpectedScoreKeepRatio;
            default: return 0f;
        }
    }

    public static void ApplySettingValue(NativeDetectorSettingsData settings, string key, float value)
    {
        if (settings == null)
            return;

        switch (key)
        {
            case "chordLeniency":
                settings.chordLeniency = Clamp(value, 0.34f, 1.00f);
                break;
            case "continuousRmsGate":
                settings.continuousRmsGate = Clamp(value, 0.001f, 0.05f);
                break;
            case "continuousConfidenceGate":
                settings.continuousConfidenceGate = Clamp(value, 0f, 0.99f);
                break;
            case "continuousHoldSeconds":
                settings.continuousHoldSeconds = Clamp(value, 0.01f, 0.50f);
                break;
            case "highStringMinMidi":
                settings.highStringMinMidi = Mathf.Clamp(Mathf.RoundToInt(value), 36, 96);
                break;
            case "highStringRmsMultiplier":
                settings.highStringRmsMultiplier = Clamp(value, 0.10f, 1.50f);
                break;
            case "highStringConfidenceMultiplier":
                settings.highStringConfidenceMultiplier = Clamp(value, 0.10f, 1.00f);
                break;
            case "highStringBenefitMatchMaxDistance":
                settings.highStringBenefitMatchMaxDistance = Mathf.Clamp(Mathf.RoundToInt(value), 0, 2);
                break;
            case "onsetExpectLookaheadSeconds":
                settings.onsetExpectLookaheadSeconds = Clamp(value, 0.0f, 0.40f);
                break;
            case "standardOnsetThreshold":
                settings.standardOnsetThreshold = Clamp(value, 0.01f, 1.00f);
                break;
            case "highStringOnsetThreshold":
                settings.highStringOnsetThreshold = Clamp(value, 0.01f, 1.00f);
                break;
            case "expectExactBonus":
                settings.expectExactBonus = Clamp(value, 0.0f, 5.0f);
                break;
            case "expectNearBonus1":
                settings.expectNearBonus1 = Clamp(value, 0.0f, 3.0f);
                break;
            case "expectNearBonus2":
                settings.expectNearBonus2 = Clamp(value, 0.0f, 3.0f);
                break;
            case "expectMaxDistanceAi":
                settings.expectMaxDistanceAi = Mathf.Clamp(Mathf.RoundToInt(value), 0, 4);
                break;
            case "expectMaxDistanceContinuous":
                settings.expectMaxDistanceContinuous = Mathf.Clamp(Mathf.RoundToInt(value), 0, 4);
                break;
            case "expectStrictConfidence":
                settings.expectStrictConfidence = Clamp(value, 0.0f, 0.99f);
                break;
            case "chordScoreKeepRatio":
                settings.chordScoreKeepRatio = Clamp(value, 0.0f, 0.99f);
                break;
            case "chordExpectedScoreKeepRatio":
                settings.chordExpectedScoreKeepRatio = Clamp(value, 0.0f, 0.99f);
                break;
        }
    }

    public static List<NativeDetectorSettingSnapshot> BuildSettingSnapshots(NativeDetectorSettingsData settings)
    {
        NativeDetectorSettingsData sanitized = Sanitize(settings);
        List<NativeDetectorSettingSnapshot> snapshots = new List<NativeDetectorSettingSnapshot>(definitions.Count);
        for (int i = 0; i < definitions.Count; i++)
        {
            NativeDetectorSettingDefinition definition = definitions[i];
            float value = GetSettingValue(sanitized, definition.Key);
            snapshots.Add(new NativeDetectorSettingSnapshot
            {
                key = definition.Key,
                label = definition.Label,
                description = definition.Description,
                value = value,
                min = definition.Min,
                max = definition.Max,
                wholeNumbers = definition.WholeNumbers,
                valueText = FormatValue(definition, value)
            });
        }

        return snapshots;
    }

    public static string FormatValue(NativeDetectorSettingDefinition definition, float value)
    {
        if (definition == null)
            return value.ToString(CultureInfo.InvariantCulture);

        if (definition.WholeNumbers)
            return Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);

        return value.ToString($"F{definition.Decimals}", CultureInfo.InvariantCulture);
    }

    private static float Clamp(float value, float min, float max)
    {
        return Mathf.Clamp(value, min, max);
    }
}
