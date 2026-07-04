using System;

namespace SynchTheory
{
    internal static class SynchTheoryAudioFeatureExtractor
    {
        public static SynchTheoryFeatureSequence Extract(SynchTheoryAudioData audio, double startTimeSeconds, double endTimeSeconds, SynchTheoryOptions options)
        {
            if (audio == null || !audio.IsValid)
                return Empty(startTimeSeconds, options);

            double requestedFrameRate = Math.Max(10.0, options?.frameRate ?? 25.0);
            startTimeSeconds = Math.Max(0.0, startTimeSeconds);
            endTimeSeconds = Math.Max(startTimeSeconds + 0.05, Math.Min(audio.DurationSeconds, endTimeSeconds));

            int startSample = Clamp((int)Math.Round(startTimeSeconds * audio.sampleRate), 0, audio.monoSamples.Length - 1);
            int endSample = Clamp((int)Math.Round(endTimeSeconds * audio.sampleRate), startSample + 1, audio.monoSamples.Length);
            int hop = Math.Max(64, (int)Math.Round(audio.sampleRate / requestedFrameRate));
            double frameRate = audio.sampleRate / (double)hop;
            int window = Math.Max(256, Math.Min(4096, hop * 3));
            int frameCount = Math.Max(2, (int)Math.Ceiling((endSample - startSample) / (double)hop));

            float[] energy = new float[frameCount];
            float[] highMotion = new float[frameCount];
            float previousSample = 0f;

            for (int frame = 0; frame < frameCount; frame++)
            {
                int center = startSample + frame * hop;
                int from = Clamp(center - window / 2, 0, audio.monoSamples.Length - 1);
                int to = Clamp(center + window / 2, from + 1, audio.monoSamples.Length);
                double sumSquares = 0.0;
                double sumMotion = 0.0;
                int count = 0;

                for (int i = from; i < to; i++)
                {
                    float sample = audio.monoSamples[i];
                    sumSquares += sample * sample;
                    sumMotion += Math.Abs(sample - previousSample);
                    previousSample = sample;
                    count++;
                }

                if (count > 0)
                {
                    energy[frame] = (float)Math.Sqrt(sumSquares / count);
                    highMotion[frame] = (float)(sumMotion / count);
                }
            }

            float[] onset = new float[frameCount];
            float previousEnergy = energy.Length > 0 ? energy[0] : 0f;
            float previousMotion = highMotion.Length > 0 ? highMotion[0] : 0f;
            for (int i = 1; i < frameCount; i++)
            {
                float energyFlux = Math.Max(0f, energy[i] - previousEnergy);
                float motionFlux = Math.Max(0f, highMotion[i] - previousMotion);
                onset[i] = energyFlux * 0.65f + motionFlux * 0.35f;
                previousEnergy = energy[i] * 0.85f + previousEnergy * 0.15f;
                previousMotion = highMotion[i] * 0.85f + previousMotion * 0.15f;
            }

            NormalizeRobust(energy);
            NormalizeRobust(onset);
            EnhancePeaks(onset);

            return new SynchTheoryFeatureSequence
            {
                startTimeSeconds = startTimeSeconds,
                frameRate = frameRate,
                onset = onset,
                energy = energy,
                beat = new float[frameCount]
            };
        }

        public static int FindStrongestOnsetFrame(SynchTheoryFeatureSequence audio, int centerFrame, int radiusFrames)
        {
            if (audio == null || audio.Count == 0)
                return centerFrame;

            int start = Clamp(centerFrame - Math.Max(0, radiusFrames), 0, audio.Count - 1);
            int end = Clamp(centerFrame + Math.Max(0, radiusFrames), start, audio.Count - 1);
            int best = Clamp(centerFrame, start, end);
            float bestScore = -1f;
            for (int i = start; i <= end; i++)
            {
                float score = (audio.onset?[i] ?? 0f) * 0.82f + (audio.energy?[i] ?? 0f) * 0.18f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best;
        }

        public static int FindEarliestCredibleOnsetFrame(SynchTheoryFeatureSequence audio, int startFrame, int endFrame, float threshold = 0.34f)
        {
            if (audio == null || audio.Count == 0)
                return Math.Max(0, startFrame);

            startFrame = Clamp(startFrame, 0, audio.Count - 1);
            endFrame = Clamp(endFrame, startFrame, audio.Count - 1);
            float bestPeak = 0f;
            for (int i = startFrame; i <= endFrame; i++)
            {
                float onset = audio.onset != null && i < audio.onset.Length ? audio.onset[i] : 0f;
                if (onset > bestPeak)
                    bestPeak = onset;
            }

            float gate = Math.Max(0.18f, Math.Min(0.72f, Math.Max(threshold, bestPeak * 0.42f)));
            int bestFallback = startFrame;
            float bestFallbackScore = -1f;
            for (int i = startFrame; i <= endFrame; i++)
            {
                float onset = audio.onset != null && i < audio.onset.Length ? audio.onset[i] : 0f;
                float energy = audio.energy != null && i < audio.energy.Length ? audio.energy[i] : 0f;
                float score = onset * 0.86f + energy * 0.14f;
                if (score > bestFallbackScore)
                {
                    bestFallbackScore = score;
                    bestFallback = i;
                }

                if (onset < gate && score < gate * 0.92f)
                    continue;

                float previous = i > 0 && audio.onset != null ? audio.onset[i - 1] : 0f;
                float next = i + 1 < audio.Count && audio.onset != null ? audio.onset[i + 1] : 0f;
                if (onset + 0.001f >= previous && onset + 0.001f >= next)
                    return i;
            }

            return bestFallback;
        }

        private static SynchTheoryFeatureSequence Empty(double startTimeSeconds, SynchTheoryOptions options)
        {
            return new SynchTheoryFeatureSequence
            {
                startTimeSeconds = Math.Max(0.0, startTimeSeconds),
                frameRate = Math.Max(10.0, options?.frameRate ?? 25.0),
                onset = Array.Empty<float>(),
                energy = Array.Empty<float>(),
                beat = Array.Empty<float>()
            };
        }

        private static void EnhancePeaks(float[] values)
        {
            if (values == null || values.Length < 3)
                return;

            float[] copy = new float[values.Length];
            Array.Copy(values, copy, values.Length);
            for (int i = 1; i < values.Length - 1; i++)
            {
                float local = copy[i];
                float neighbor = Math.Max(copy[i - 1], copy[i + 1]);
                values[i] = Math.Max(0f, local - neighbor * 0.35f);
            }

            NormalizeRobust(values);
        }

        internal static void NormalizeRobust(float[] values)
        {
            if (values == null || values.Length == 0)
                return;

            float[] sorted = new float[values.Length];
            Array.Copy(values, sorted, values.Length);
            Array.Sort(sorted);
            float percentile = sorted[Math.Max(0, Math.Min(sorted.Length - 1, (int)(sorted.Length * 0.95)))];
            if (percentile <= 0.000001f)
            {
                float max = 0f;
                for (int i = 0; i < values.Length; i++)
                    max = Math.Max(max, values[i]);
                percentile = max;
            }

            if (percentile <= 0.000001f)
                return;

            for (int i = 0; i < values.Length; i++)
            {
                float normalized = values[i] / percentile;
                values[i] = normalized <= 0f ? 0f : normalized >= 1f ? 1f : normalized;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
