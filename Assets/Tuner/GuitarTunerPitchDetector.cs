using System;
using UnityEngine;

public static class GuitarTunerPitchDetector
{
    public const float StandardConcertAHz = 440f;
    public const float DefaultMinFrequencyHz = 70f;
    public const float DefaultMaxFrequencyHz = 500f;

    private const float MinimumInputRms = 0.0035f;
    private const float MinimumConfidence = 0.58f;

    public static bool TryDetectPitch(
        float[] samples,
        int sampleCount,
        int sampleRate,
        float[] scoreScratch,
        out GuitarTunerPitchDetection detection,
        float minFrequencyHz = DefaultMinFrequencyHz,
        float maxFrequencyHz = DefaultMaxFrequencyHz)
    {
        detection = default;

        if (samples == null || sampleCount < 512 || sampleRate < 8000 || scoreScratch == null)
            return false;

        float minFrequency = Mathf.Clamp(minFrequencyHz, 20f, 2000f);
        float maxFrequency = Mathf.Clamp(maxFrequencyHz, minFrequency + 1f, 4000f);
        int minTau = Mathf.Max(2, Mathf.FloorToInt(sampleRate / maxFrequency));
        int maxTau = Mathf.Min(sampleCount - 2, Mathf.CeilToInt(sampleRate / minFrequency));
        if (maxTau <= minTau + 2 || scoreScratch.Length <= maxTau + 1)
            return false;

        double sum = 0d;
        for (int i = 0; i < sampleCount; i++)
            sum += samples[i];

        float mean = (float)(sum / sampleCount);
        double energy = 0d;
        for (int i = 0; i < sampleCount; i++)
        {
            float centered = samples[i] - mean;
            energy += centered * centered;
        }

        float rms = Mathf.Sqrt((float)(energy / sampleCount));
        detection.inputLevel = Mathf.Clamp01(rms * 16f);
        if (rms < MinimumInputRms)
            return false;

        for (int tau = 0; tau < minTau; tau++)
            scoreScratch[tau] = 0f;

        float bestScore = 0f;
        int bestTau = minTau;
        for (int tau = minTau; tau <= maxTau; tau++)
        {
            int limit = sampleCount - tau;
            double correlation = 0d;
            double energyA = 0d;
            double energyB = 0d;

            for (int i = 0; i < limit; i++)
            {
                float a = samples[i] - mean;
                float b = samples[i + tau] - mean;
                correlation += a * b;
                energyA += a * a;
                energyB += b * b;
            }

            double denominator = energyA + energyB;
            float score = denominator > 1e-12d
                ? Mathf.Clamp((float)((2d * correlation) / denominator), -1f, 1f)
                : 0f;

            scoreScratch[tau] = score;
            if (score > bestScore)
            {
                bestScore = score;
                bestTau = tau;
            }
        }

        if (bestScore < MinimumConfidence)
            return false;

        float strongPeakThreshold = Mathf.Max(MinimumConfidence, bestScore * 0.86f);
        int selectedTau = bestTau;
        for (int tau = minTau + 1; tau < maxTau; tau++)
        {
            float score = scoreScratch[tau];
            if (score >= strongPeakThreshold &&
                score >= scoreScratch[tau - 1] &&
                score >= scoreScratch[tau + 1])
            {
                selectedTau = tau;
                break;
            }
        }

        float refinedTau = RefinePeakTau(scoreScratch, selectedTau, minTau, maxTau);
        if (refinedTau <= 0.0001f)
            return false;

        float frequency = sampleRate / refinedTau;
        if (frequency < minFrequency * 0.92f || frequency > maxFrequency * 1.08f)
            return false;

        float midi = FrequencyToMidi(frequency);
        int nearestMidi = Mathf.RoundToInt(midi);

        detection.detected = true;
        detection.frequencyHz = frequency;
        detection.midi = midi;
        detection.nearestMidi = nearestMidi;
        detection.centsFromNearest = (midi - nearestMidi) * 100f;
        detection.confidence = Mathf.Clamp01(scoreScratch[selectedTau]);
        detection.inputLevel = Mathf.Clamp01(Mathf.Max(detection.inputLevel, rms * 16f));
        return true;
    }

    public static float FrequencyToMidi(float frequencyHz)
    {
        if (frequencyHz <= 0f)
            return 0f;

        return 69f + (12f * Mathf.Log(frequencyHz / StandardConcertAHz, 2f));
    }

    public static float MidiToFrequency(float midi)
    {
        return StandardConcertAHz * Mathf.Pow(2f, (midi - 69f) / 12f);
    }

    public static string FormatNoteName(int midi)
    {
        if (midi < 0)
            return "--";

        string[] names =
        {
            "C", "C#", "D", "D#", "E", "F",
            "F#", "G", "G#", "A", "A#", "B"
        };

        int pitchClass = Mod(midi, 12);
        int octave = (midi / 12) - 1;
        return $"{names[pitchClass]}{octave}";
    }

    private static float RefinePeakTau(float[] scores, int tau, int minTau, int maxTau)
    {
        if (scores == null || tau <= minTau || tau >= maxTau)
            return tau;

        float left = scores[tau - 1];
        float center = scores[tau];
        float right = scores[tau + 1];
        float denominator = left - (2f * center) + right;
        if (Mathf.Abs(denominator) < 1e-6f)
            return tau;

        float offset = 0.5f * (left - right) / denominator;
        return tau + Mathf.Clamp(offset, -0.5f, 0.5f);
    }

    private static int Mod(int value, int divisor)
    {
        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }
}
