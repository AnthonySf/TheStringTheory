using System;
using UnityEngine;

public enum GuitarTunerMode
{
    Automatic,
    Manual
}

public enum GuitarTunerInstrument
{
    Guitar,
    Bass
}

[Serializable]
public sealed class GuitarTunerTarget
{
    public string id = string.Empty;
    public string label = string.Empty;
    public string noteName = string.Empty;
    public int midi;
    public float frequencyHz;
    public int stringIndex;

    public GuitarTunerTarget Clone()
    {
        return new GuitarTunerTarget
        {
            id = id ?? string.Empty,
            label = label ?? string.Empty,
            noteName = noteName ?? string.Empty,
            midi = midi,
            frequencyHz = frequencyHz,
            stringIndex = stringIndex
        };
    }
}

public struct GuitarTunerPitchDetection
{
    public bool detected;
    public float frequencyHz;
    public float midi;
    public int nearestMidi;
    public float centsFromNearest;
    public float confidence;
    public float inputLevel;
}

[Serializable]
public sealed class GuitarTunerSnapshot
{
    public GuitarTunerMode mode;
    public GuitarTunerTarget[] targets = Array.Empty<GuitarTunerTarget>();
    public bool[] tunedTargets = Array.Empty<bool>();
    public int selectedTargetIndex;
    public string targetLabel = string.Empty;
    public string targetNoteName = string.Empty;
    public float targetFrequencyHz;
    public bool allTargetsTuned;
    public bool hasSignal;
    public bool isInTune;
    public float detectedFrequencyHz;
    public float detectedMidi;
    public int detectedNearestMidi;
    public string detectedNoteName = string.Empty;
    public float cents;
    public float confidence;
    public float inputLevel;
    public string statusText = "Waiting for input";
    public string inputRouteLabel = string.Empty;

    public GuitarTunerSnapshot Clone()
    {
        GuitarTunerTarget[] clonedTargets = targets == null
            ? Array.Empty<GuitarTunerTarget>()
            : Array.ConvertAll(targets, target => target?.Clone() ?? new GuitarTunerTarget());
        bool[] clonedTunedTargets = tunedTargets == null
            ? Array.Empty<bool>()
            : (bool[])tunedTargets.Clone();

        return new GuitarTunerSnapshot
        {
            mode = mode,
            targets = clonedTargets,
            tunedTargets = clonedTunedTargets,
            selectedTargetIndex = selectedTargetIndex,
            targetLabel = targetLabel ?? string.Empty,
            targetNoteName = targetNoteName ?? string.Empty,
            targetFrequencyHz = targetFrequencyHz,
            allTargetsTuned = allTargetsTuned,
            hasSignal = hasSignal,
            isInTune = isInTune,
            detectedFrequencyHz = detectedFrequencyHz,
            detectedMidi = detectedMidi,
            detectedNearestMidi = detectedNearestMidi,
            detectedNoteName = detectedNoteName ?? string.Empty,
            cents = cents,
            confidence = confidence,
            inputLevel = inputLevel,
            statusText = statusText ?? string.Empty,
            inputRouteLabel = inputRouteLabel ?? string.Empty
        };
    }
}
