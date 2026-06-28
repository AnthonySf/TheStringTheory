using System;
using System.Collections.Generic;
using System.Linq;

namespace SynchTheory
{
    internal static class SynchTheoryScoreFeatureExtractor
    {
        public static SynchTheoryFeatureSequence Extract(
            SynchTheoryScoreMap score,
            double startBeat,
            double endBeat,
            double scoreStartTimeSeconds,
            double scoreEndTimeSeconds,
            SynchTheoryOptions options)
        {
            double frameRate = Math.Max(10.0, options?.frameRate ?? 25.0);
            scoreStartTimeSeconds = Math.Max(0.0, scoreStartTimeSeconds);
            scoreEndTimeSeconds = Math.Max(scoreStartTimeSeconds + 0.05, scoreEndTimeSeconds);
            int frameCount = Math.Max(2, (int)Math.Ceiling((scoreEndTimeSeconds - scoreStartTimeSeconds) * frameRate));

            SynchTheoryFeatureSequence sequence = new SynchTheoryFeatureSequence
            {
                startTimeSeconds = scoreStartTimeSeconds,
                frameRate = frameRate,
                onset = new float[frameCount],
                energy = new float[frameCount],
                beat = new float[frameCount]
            };

            AddBeatPulses(sequence, score, startBeat, endBeat, scoreStartTimeSeconds);
            AddScoreEventPulses(sequence, score, startBeat, endBeat, scoreStartTimeSeconds);
            SynchTheoryAudioFeatureExtractor.NormalizeRobust(sequence.onset);
            SynchTheoryAudioFeatureExtractor.NormalizeRobust(sequence.energy);
            SynchTheoryAudioFeatureExtractor.NormalizeRobust(sequence.beat);
            return sequence;
        }

        private static void AddBeatPulses(
            SynchTheoryFeatureSequence sequence,
            SynchTheoryScoreMap score,
            double startBeat,
            double endBeat,
            double startTimeSeconds)
        {
            if (score?.beats == null)
                return;

            List<SynchTheoryBeat> beats = score.beats
                .Where(beat => beat != null &&
                               beat.beatPosition >= startBeat - 0.0001 &&
                               beat.beatPosition <= endBeat + 0.0001)
                .OrderBy(beat => beat.beatPosition)
                .ToList();

            for (int i = 0; i < beats.Count; i++)
            {
                SynchTheoryBeat beat = beats[i];
                double time = Math.Max(0.0, beat.chartTimeSeconds - startTimeSeconds);
                float weight = beat.isDownbeat ? 0.78f : 0.38f;
                AddPulse(sequence.beat, sequence.frameRate, time, weight, beat.isDownbeat ? 2 : 1);
                AddPulse(sequence.onset, sequence.frameRate, time, beat.isDownbeat ? 0.16f : 0.07f, 1);
            }
        }

        private static void AddScoreEventPulses(
            SynchTheoryFeatureSequence sequence,
            SynchTheoryScoreMap score,
            double startBeat,
            double endBeat,
            double startTimeSeconds)
        {
            if (score?.events == null)
                return;

            List<SynchTheoryScoreEvent> events = score.events
                .Where(evt => evt != null &&
                              evt.beatPosition >= startBeat - 0.0001 &&
                              evt.beatPosition <= endBeat + 0.0001)
                .OrderBy(evt => evt.chartTimeSeconds)
                .ToList();

            for (int i = 0; i < events.Count; i++)
            {
                SynchTheoryScoreEvent evt = events[i];
                double time = Math.Max(0.0, evt.chartTimeSeconds - startTimeSeconds);
                float weight = (float)Math.Max(0.05, Math.Min(2.0, evt.weight));
                AddPulse(sequence.onset, sequence.frameRate, time, weight, evt.isPercussive ? 1 : 2);
                AddPulse(sequence.energy, sequence.frameRate, time, weight * 0.65f, Math.Max(2, (int)Math.Ceiling(Math.Max(0.04, evt.durationSeconds) * sequence.frameRate)));
            }
        }

        private static void AddPulse(float[] target, double frameRate, double timeSeconds, float amount, int radiusFrames)
        {
            if (target == null || target.Length == 0 || frameRate <= 0.0001)
                return;

            int center = (int)Math.Round(timeSeconds * frameRate);
            int radius = Math.Max(0, radiusFrames);
            for (int offset = -radius; offset <= radius; offset++)
            {
                int index = center + offset;
                if (index < 0 || index >= target.Length)
                    continue;

                float falloff = radius <= 0 ? 1f : 1f - Math.Min(1f, Math.Abs(offset) / (float)(radius + 1));
                target[index] += amount * falloff;
            }
        }
    }
}
