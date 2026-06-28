using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SynchTheory;

public sealed class SynchTheoryEngineTests
{
    [Test]
    public void FullSongAlignment_RecoversShiftedSyntheticBeatMap()
    {
        const int sampleRate = 2000;
        const double shift = 0.35;
        const double interval = 0.92;
        const int beatCount = 9;
        SynchTheoryScoreMap score = new SynchTheoryScoreMap
        {
            durationSeconds = 8.0,
            defaultTempoBpm = 60.0,
            beats = new List<SynchTheoryBeat>(),
            events = new List<SynchTheoryScoreEvent>()
        };

        for (int beat = 0; beat < beatCount; beat++)
        {
            score.beats.Add(new SynchTheoryBeat
            {
                index = beat,
                beatPosition = beat,
                chartTimeSeconds = beat,
                audioTimeSeconds = beat,
                isDownbeat = beat % 4 == 0
            });
            score.events.Add(new SynchTheoryScoreEvent
            {
                id = $"note_{beat}",
                beatPosition = beat,
                chartTimeSeconds = beat,
                weight = beat % 4 == 0 ? 1.4 : 1.0
            });
        }

        double duration = shift + interval * (beatCount - 1) + 1.0;
        float[] samples = new float[(int)Math.Ceiling(duration * sampleRate)];
        for (int beat = 0; beat < beatCount; beat++)
            AddClick(samples, sampleRate, shift + beat * interval, beat % 4 == 0 ? 1.0f : 0.78f);

        SynchTheoryAudioData audio = new SynchTheoryAudioData
        {
            monoSamples = samples,
            sampleRate = sampleRate
        };

        SynchTheoryOptions options = SynchTheoryOptions.Default();
        options.frameRate = 50.0;
        options.maxWarpWindowSeconds = 2.0;
        options.localOnsetSnapSeconds = 0.04;

        SynchTheoryAlignmentResult result = SynchTheoryEngine.Align(score, audio, options);

        Assert.IsTrue(result.success, result.message);
        Assert.Greater(result.confidence, 0.40);
        for (int beat = 0; beat < beatCount; beat++)
        {
            SynchTheoryBeat generated = result.generatedBeats.FirstOrDefault(item => Math.Abs(item.beatPosition - beat) < 0.0001);
            Assert.NotNull(generated, $"Missing generated beat {beat}.");
            Assert.AreEqual(shift + beat * interval, generated.audioTimeSeconds, 0.12, $"Beat {beat} was not aligned to the synthetic audio click.");
        }
    }

    [Test]
    public void FullSongAlignment_UsesFirstCredibleOpeningHitInsteadOfLaterLouderHit()
    {
        const int sampleRate = 2000;
        const double shift = 0.42;
        const double interval = 0.90;
        const int beatCount = 8;
        SynchTheoryScoreMap score = new SynchTheoryScoreMap
        {
            durationSeconds = 8.0,
            defaultTempoBpm = 60.0,
            beats = new List<SynchTheoryBeat>(),
            events = new List<SynchTheoryScoreEvent>()
        };

        for (int beat = 0; beat < beatCount; beat++)
        {
            score.beats.Add(new SynchTheoryBeat
            {
                index = beat,
                beatPosition = beat,
                chartTimeSeconds = beat,
                audioTimeSeconds = beat,
                isDownbeat = beat == 0 || beat % 4 == 0
            });
            score.events.Add(new SynchTheoryScoreEvent
            {
                id = $"riff_{beat}",
                beatPosition = beat,
                chartTimeSeconds = beat,
                weight = 1.0
            });
        }

        float[] samples = new float[(int)Math.Ceiling(12.0 * sampleRate)];
        AddClick(samples, sampleRate, shift, 0.46f);
        for (int beat = 1; beat < beatCount; beat++)
            AddClick(samples, sampleRate, shift + beat * interval, 0.55f);
        AddClick(samples, sampleRate, 6.5, 1.0f);

        SynchTheoryAlignmentResult result = SynchTheoryEngine.Align(score, new SynchTheoryAudioData
        {
            monoSamples = samples,
            sampleRate = sampleRate
        }, new SynchTheoryOptions
        {
            frameRate = 50.0,
            maxWarpWindowSeconds = 2.0,
            boundarySearchSeconds = 8.0,
            localOnsetSnapSeconds = 0.04
        });

        Assert.IsTrue(result.success, result.message);
        SynchTheoryBeat first = result.generatedBeats.FirstOrDefault(item => Math.Abs(item.beatPosition) < 0.0001);
        Assert.NotNull(first);
        Assert.AreEqual(shift, first.audioTimeSeconds, 0.14, "The opening beat should align to the first credible riff hit, not a later louder hit.");
        Assert.Less(first.audioTimeSeconds, 1.0);
    }

    private static void AddClick(float[] samples, int sampleRate, double timeSeconds, float gain)
    {
        int center = Math.Max(0, Math.Min(samples.Length - 1, (int)Math.Round(timeSeconds * sampleRate)));
        for (int offset = -8; offset <= 8; offset++)
        {
            int index = center + offset;
            if (index < 0 || index >= samples.Length)
                continue;

            float falloff = 1f - Math.Min(1f, Math.Abs(offset) / 9f);
            samples[index] += gain * falloff;
        }
    }
}
