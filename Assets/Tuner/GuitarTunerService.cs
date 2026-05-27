using System;
using UnityEngine;

public sealed class GuitarTunerService
{
    private const int AnalysisWindowSize = 4096;
    private const float AnalysisIntervalSeconds = 1f / 30f;
    private const float SignalHoldSeconds = 0.20f;
    private const float InTuneCentsThreshold = 4f;
    private const float ConfirmTuneHoldSeconds = 0.55f;
    private const float RetuneClearCentsThreshold = 12f;
    private const int MinimumUsefulSamples = 2048;

    private static readonly GuitarTunerTarget[] StandardGuitarTargets =
    {
        CreateTarget("e2", "String 6", "E2", 40, 0),
        CreateTarget("a2", "String 5", "A2", 45, 1),
        CreateTarget("d3", "String 4", "D3", 50, 2),
        CreateTarget("g3", "String 3", "G3", 55, 3),
        CreateTarget("b3", "String 2", "B3", 59, 4),
        CreateTarget("e4", "String 1", "E4", 64, 5)
    };

    private readonly object audioLock = new object();
    private float[] ringBuffer = Array.Empty<float>();
    private int ringWriteIndex;
    private int ringCount;
    private int currentSampleRate = 48000;
    private string currentInputChannelMode = SharedAudioInputChannelModes.Input1;
    private float latestInputLevel;

    private readonly float[] analysisBuffer = new float[AnalysisWindowSize];
    private readonly float[] scoreScratch = new float[AnalysisWindowSize + 2];
    private float analysisTimer;
    private float secondsSinceSignal = float.PositiveInfinity;

    private GuitarTunerMode mode = GuitarTunerMode.Automatic;
    private int selectedTargetIndex;
    private int activeTargetIndex;
    private readonly bool[] tunedTargets = new bool[StandardGuitarTargets.Length];
    private int confirmingTargetIndex = -1;
    private float confirmingSeconds;
    private bool hasSmoothedDetection;
    private float smoothedFrequencyHz;
    private float smoothedMidi;
    private float smoothedCents;
    private float smoothedConfidence;
    private GuitarTunerPitchDetection latestDetection;
    private GuitarTunerSnapshot latestSnapshot;

    public GuitarTunerService()
    {
        activeTargetIndex = selectedTargetIndex;
        latestSnapshot = BuildSnapshot(hasSignal: false);
    }

    public GuitarTunerMode Mode => mode;
    public int SelectedTargetIndex => selectedTargetIndex;

    public void Reset()
    {
        lock (audioLock)
        {
            ringWriteIndex = 0;
            ringCount = 0;
            latestInputLevel = 0f;
        }

        analysisTimer = 0f;
        secondsSinceSignal = float.PositiveInfinity;
        latestDetection = default;
        activeTargetIndex = mode == GuitarTunerMode.Manual ? selectedTargetIndex : activeTargetIndex;
        Array.Clear(tunedTargets, 0, tunedTargets.Length);
        confirmingTargetIndex = -1;
        confirmingSeconds = 0f;
        ResetSmoothing();
        latestSnapshot = BuildSnapshot(hasSignal: false, inputLevelOverride: 0f);
    }

    public void SubmitAudioBlock(float[] input, int inputChannels, int frameCount, int sampleRate, string inputChannelMode)
    {
        if (input == null || frameCount <= 0 || sampleRate < 8000)
            return;

        int safeChannels = Mathf.Clamp(inputChannels, 1, 64);
        if ((long)input.Length < (long)frameCount * safeChannels)
            return;

        string normalizedMode = SharedAudioInputChannelModes.Normalize(inputChannelMode);
        float blockPeak = 0f;
        double blockEnergy = 0d;

        lock (audioLock)
        {
            EnsureRingBufferCapacity(sampleRate);
            currentSampleRate = sampleRate;
            currentInputChannelMode = normalizedMode;

            for (int frame = 0; frame < frameCount; frame++)
            {
                float sample = ReadSelectedInputSample(input, safeChannels, frame, normalizedMode);
                ringBuffer[ringWriteIndex] = sample;
                ringWriteIndex = (ringWriteIndex + 1) % ringBuffer.Length;
                if (ringCount < ringBuffer.Length)
                    ringCount++;

                float abs = Mathf.Abs(sample);
                if (abs > blockPeak)
                    blockPeak = abs;
                blockEnergy += sample * sample;
            }

            float blockRms = Mathf.Sqrt((float)(blockEnergy / Mathf.Max(1, frameCount)));
            latestInputLevel = Mathf.Clamp01(Mathf.Max(blockPeak * 0.72f, blockRms * 10f));
        }
    }

    public void Update(float deltaSeconds)
    {
        analysisTimer -= Mathf.Max(0f, deltaSeconds);
        if (analysisTimer > 0f)
        {
            if (secondsSinceSignal < float.PositiveInfinity)
                secondsSinceSignal += Mathf.Max(0f, deltaSeconds);
            return;
        }

        analysisTimer = AnalysisIntervalSeconds;
        AnalyzeLatestAudio();
    }

    public void SetMode(GuitarTunerMode newMode)
    {
        if (mode == newMode)
            return;

        mode = newMode;
        ResetSmoothing();
        int detectedTargetIndex = ResolveDetectedTargetIndex(latestDetection);
        activeTargetIndex = ResolveActiveTargetIndex(detectedTargetIndex, latestDetection);
        latestSnapshot = BuildSnapshot(secondsSinceSignal <= SignalHoldSeconds);
    }

    public void ToggleMode()
    {
        SetMode(mode == GuitarTunerMode.Automatic ? GuitarTunerMode.Manual : GuitarTunerMode.Automatic);
    }

    public void SetManualTargetIndex(int index)
    {
        selectedTargetIndex = Mathf.Clamp(index, 0, StandardGuitarTargets.Length - 1);
        activeTargetIndex = selectedTargetIndex;
        if (mode != GuitarTunerMode.Manual)
            mode = GuitarTunerMode.Manual;

        ResetSmoothing();
        latestSnapshot = BuildSnapshot(secondsSinceSignal <= SignalHoldSeconds);
    }

    public void MoveManualTarget(int delta)
    {
        if (delta == 0)
            return;

        int count = StandardGuitarTargets.Length;
        int next = (selectedTargetIndex + delta) % count;
        if (next < 0)
            next += count;
        SetManualTargetIndex(next);
    }

    public GuitarTunerSnapshot GetSnapshot()
    {
        return latestSnapshot ?? BuildSnapshot(hasSignal: false);
    }

    private void AnalyzeLatestAudio()
    {
        int sampleRate;
        float inputLevel;
        bool hasEnoughSamples;

        lock (audioLock)
        {
            sampleRate = currentSampleRate;
            inputLevel = latestInputLevel;
            hasEnoughSamples = ringCount >= MinimumUsefulSamples;
            if (hasEnoughSamples)
                CopyLatestSamplesLocked(analysisBuffer, AnalysisWindowSize);
        }

        if (!hasEnoughSamples)
        {
            secondsSinceSignal = float.PositiveInfinity;
            latestSnapshot = BuildSnapshot(hasSignal: false, inputLevelOverride: inputLevel);
            return;
        }

        bool detected = GuitarTunerPitchDetector.TryDetectPitch(
            analysisBuffer,
            AnalysisWindowSize,
            sampleRate,
            scoreScratch,
            out GuitarTunerPitchDetection detection);

        detection.inputLevel = Mathf.Clamp01(Mathf.Max(detection.inputLevel, inputLevel));

        if (!detected)
        {
            secondsSinceSignal = Mathf.Min(secondsSinceSignal + AnalysisIntervalSeconds, 999f);
            if (secondsSinceSignal > SignalHoldSeconds)
                hasSmoothedDetection = false;

            latestSnapshot = BuildSnapshot(secondsSinceSignal <= SignalHoldSeconds, inputLevelOverride: inputLevel);
            return;
        }

        secondsSinceSignal = 0f;
        latestDetection = detection;
        int detectedTargetIndex = ResolveDetectedTargetIndex(detection);
        float centsFromDetectedTarget = CentsFromTarget(detection.frequencyHz, detectedTargetIndex);
        if (ShouldIgnoreCompletedAutomaticTarget(detectedTargetIndex, centsFromDetectedTarget))
        {
            activeTargetIndex = FindNextUntunedTargetAfter(detectedTargetIndex);
            ResetSmoothing();
            latestSnapshot = BuildSnapshot(hasSignal: false, inputLevelOverride: detection.inputLevel);
            return;
        }

        activeTargetIndex = ResolveActiveTargetIndex(detectedTargetIndex, detection);
        float centsFromTarget = CentsFromTarget(detection.frequencyHz, activeTargetIndex);

        if (!hasSmoothedDetection || Mathf.Abs(centsFromTarget - smoothedCents) > 35f)
        {
            smoothedFrequencyHz = detection.frequencyHz;
            smoothedMidi = detection.midi;
            smoothedCents = centsFromTarget;
            smoothedConfidence = detection.confidence;
            hasSmoothedDetection = true;
        }
        else
        {
            const float blend = 0.42f;
            smoothedFrequencyHz = Mathf.Lerp(smoothedFrequencyHz, detection.frequencyHz, blend);
            smoothedMidi = Mathf.Lerp(smoothedMidi, detection.midi, blend);
            smoothedCents = Mathf.Lerp(smoothedCents, centsFromTarget, blend);
            smoothedConfidence = Mathf.Lerp(smoothedConfidence, detection.confidence, blend);
        }

        if (UpdateTunedProgress(activeTargetIndex, smoothedCents, smoothedConfidence))
        {
            ResetSmoothing();
            latestSnapshot = BuildSnapshot(hasSignal: false, inputLevelOverride: detection.inputLevel);
            return;
        }

        latestSnapshot = BuildSnapshot(hasSignal: true, inputLevelOverride: detection.inputLevel);
    }

    private GuitarTunerSnapshot BuildSnapshot(bool hasSignal, float inputLevelOverride = -1f)
    {
        GuitarTunerTarget target = StandardGuitarTargets[Mathf.Clamp(activeTargetIndex, 0, StandardGuitarTargets.Length - 1)];
        float cents = hasSignal && hasSmoothedDetection ? Mathf.Clamp(smoothedCents, -99.9f, 99.9f) : 0f;
        bool inTune = hasSignal && Mathf.Abs(cents) <= InTuneCentsThreshold;
        float inputLevel = inputLevelOverride >= 0f ? inputLevelOverride : latestInputLevel;
        bool allTuned = AllTargetsTuned();

        return new GuitarTunerSnapshot
        {
            mode = mode,
            targets = CloneTargets(),
            tunedTargets = CloneTunedTargets(),
            selectedTargetIndex = mode == GuitarTunerMode.Manual ? selectedTargetIndex : activeTargetIndex,
            targetLabel = target.label,
            targetNoteName = target.noteName,
            targetFrequencyHz = target.frequencyHz,
            allTargetsTuned = allTuned,
            hasSignal = hasSignal,
            isInTune = inTune,
            detectedFrequencyHz = hasSignal && hasSmoothedDetection ? smoothedFrequencyHz : 0f,
            detectedMidi = hasSignal && hasSmoothedDetection ? smoothedMidi : 0f,
            detectedNearestMidi = hasSignal && hasSmoothedDetection ? Mathf.RoundToInt(smoothedMidi) : -1,
            detectedNoteName = hasSignal && hasSmoothedDetection ? GuitarTunerPitchDetector.FormatNoteName(Mathf.RoundToInt(smoothedMidi)) : "--",
            cents = cents,
            confidence = hasSignal && hasSmoothedDetection ? Mathf.Clamp01(smoothedConfidence) : 0f,
            inputLevel = Mathf.Clamp01(inputLevel),
            statusText = BuildStatusText(hasSignal, inTune, cents, allTuned),
            inputRouteLabel = currentInputChannelMode
        };
    }

    private string BuildStatusText(bool hasSignal, bool inTune, float cents, bool allTuned)
    {
        if (allTuned && !hasSignal)
            return "All strings tuned";
        if (!hasSignal)
            return latestInputLevel > 0.02f ? "Listening..." : "Play a string";
        if (inTune)
            return "In tune";
        return cents < 0f ? "Tune up" : "Tune down";
    }

    private int ResolveDetectedTargetIndex(GuitarTunerPitchDetection detection)
    {
        if (!detection.detected || detection.frequencyHz <= 0f)
            return Mathf.Clamp(activeTargetIndex, 0, StandardGuitarTargets.Length - 1);

        int bestIndex = 0;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < StandardGuitarTargets.Length; i++)
        {
            float distance = Mathf.Abs(1200f * Mathf.Log(detection.frequencyHz / StandardGuitarTargets[i].frequencyHz, 2f));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private int ResolveActiveTargetIndex(int detectedTargetIndex, GuitarTunerPitchDetection detection)
    {
        if (mode == GuitarTunerMode.Manual)
            return Mathf.Clamp(selectedTargetIndex, 0, StandardGuitarTargets.Length - 1);

        int safeDetectedTargetIndex = Mathf.Clamp(detectedTargetIndex, 0, StandardGuitarTargets.Length - 1);
        if (AllTargetsTuned())
            return safeDetectedTargetIndex;

        float centsFromDetected = detection.detected && detection.frequencyHz > 0f
            ? CentsFromTarget(detection.frequencyHz, safeDetectedTargetIndex)
            : 0f;

        if (tunedTargets[safeDetectedTargetIndex] && Mathf.Abs(centsFromDetected) <= RetuneClearCentsThreshold)
            return FindNextUntunedTargetAfter(safeDetectedTargetIndex);

        return safeDetectedTargetIndex;
    }

    private bool ShouldIgnoreCompletedAutomaticTarget(int detectedTargetIndex, float centsFromDetectedTarget)
    {
        if (mode != GuitarTunerMode.Automatic || AllTargetsTuned())
            return false;

        int safeTargetIndex = Mathf.Clamp(detectedTargetIndex, 0, tunedTargets.Length - 1);
        return tunedTargets[safeTargetIndex] && Mathf.Abs(centsFromDetectedTarget) <= RetuneClearCentsThreshold;
    }

    private bool UpdateTunedProgress(int targetIndex, float cents, float confidence)
    {
        int safeTargetIndex = Mathf.Clamp(targetIndex, 0, tunedTargets.Length - 1);
        bool inTune = Mathf.Abs(cents) <= InTuneCentsThreshold && confidence >= 0.60f;
        if (!inTune)
        {
            confirmingTargetIndex = -1;
            confirmingSeconds = 0f;
            if (Mathf.Abs(cents) > RetuneClearCentsThreshold)
                tunedTargets[safeTargetIndex] = false;
            return false;
        }

        if (confirmingTargetIndex != safeTargetIndex)
        {
            confirmingTargetIndex = safeTargetIndex;
            confirmingSeconds = 0f;
        }

        confirmingSeconds += AnalysisIntervalSeconds;
        if (confirmingSeconds < ConfirmTuneHoldSeconds)
            return false;

        tunedTargets[safeTargetIndex] = true;
        if (mode == GuitarTunerMode.Automatic && !AllTargetsTuned())
        {
            activeTargetIndex = FindNextUntunedTargetAfter(safeTargetIndex);
            return true;
        }

        return false;
    }

    private static float CentsFromTarget(float frequencyHz, int targetIndex)
    {
        int safeTargetIndex = Mathf.Clamp(targetIndex, 0, StandardGuitarTargets.Length - 1);
        return 1200f * Mathf.Log(frequencyHz / Mathf.Max(1e-6f, StandardGuitarTargets[safeTargetIndex].frequencyHz), 2f);
    }

    private int FindNextUntunedTargetAfter(int targetIndex)
    {
        int count = tunedTargets.Length;
        for (int offset = 1; offset <= count; offset++)
        {
            int candidate = (targetIndex + offset) % count;
            if (!tunedTargets[candidate])
                return candidate;
        }

        return Mathf.Clamp(targetIndex, 0, count - 1);
    }

    private bool AllTargetsTuned()
    {
        for (int i = 0; i < tunedTargets.Length; i++)
        {
            if (!tunedTargets[i])
                return false;
        }

        return tunedTargets.Length > 0;
    }

    private void EnsureRingBufferCapacity(int sampleRate)
    {
        int requiredLength = Mathf.Clamp(sampleRate * 2, AnalysisWindowSize * 2, 384000);
        if (ringBuffer != null && ringBuffer.Length == requiredLength)
            return;

        ringBuffer = new float[requiredLength];
        ringWriteIndex = 0;
        ringCount = 0;
    }

    private void CopyLatestSamplesLocked(float[] destination, int sampleCount)
    {
        int available = Mathf.Min(ringCount, sampleCount);
        int missing = sampleCount - available;
        for (int i = 0; i < missing; i++)
            destination[i] = 0f;

        int start = ringWriteIndex - available;
        while (start < 0)
            start += ringBuffer.Length;

        for (int i = 0; i < available; i++)
        {
            int sourceIndex = start + i;
            if (sourceIndex >= ringBuffer.Length)
                sourceIndex -= ringBuffer.Length;
            destination[missing + i] = ringBuffer[sourceIndex];
        }
    }

    private void ResetSmoothing()
    {
        hasSmoothedDetection = false;
        smoothedFrequencyHz = 0f;
        smoothedMidi = 0f;
        smoothedCents = 0f;
        smoothedConfidence = 0f;
    }

    private static float ReadSelectedInputSample(float[] input, int inputChannels, int frame, string inputChannelMode)
    {
        int baseIndex = frame * inputChannels;
        switch (SharedAudioInputChannelModes.Normalize(inputChannelMode))
        {
            case SharedAudioInputChannelModes.Input2:
                return input[baseIndex + Mathf.Min(1, inputChannels - 1)];
            case SharedAudioInputChannelModes.MonoMix:
                if (inputChannels <= 1)
                    return input[baseIndex];
                return (input[baseIndex] + input[baseIndex + 1]) * 0.5f;
            case SharedAudioInputChannelModes.Input1:
            default:
                return input[baseIndex];
        }
    }

    private static GuitarTunerTarget[] CloneTargets()
    {
        GuitarTunerTarget[] result = new GuitarTunerTarget[StandardGuitarTargets.Length];
        for (int i = 0; i < StandardGuitarTargets.Length; i++)
            result[i] = StandardGuitarTargets[i].Clone();
        return result;
    }

    private bool[] CloneTunedTargets()
    {
        return (bool[])tunedTargets.Clone();
    }

    private static GuitarTunerTarget CreateTarget(string id, string label, string noteName, int midi, int stringIndex)
    {
        return new GuitarTunerTarget
        {
            id = id,
            label = label,
            noteName = noteName,
            midi = midi,
            frequencyHz = GuitarTunerPitchDetector.MidiToFrequency(midi),
            stringIndex = stringIndex
        };
    }
}
