using System;
using System.Collections.Generic;

namespace SynchTheory
{
    public enum SynchTheoryRunScope
    {
        FullSong,
        BeatRange
    }

    public sealed class SynchTheoryOptions
    {
        public SynchTheoryRunScope scope = SynchTheoryRunScope.FullSong;
        public double startBeat;
        public double endBeat;
        public double frameRate = 25.0;
        public double maxWarpWindowSeconds = 18.0;
        public double boundarySearchSeconds = 8.0;
        public double onsetWeight = 0.62;
        public double energyWeight = 0.18;
        public double beatWeight = 0.20;
        public double localOnsetSnapSeconds = 0.075;
        public double minimumTempoBpm = 20.0;
        public double maximumTempoBpm = 360.0;
        public bool keepManualAnchors = true;
        public bool moveContentWithBeatMap = true;
        public bool smoothGeneratedTempo = true;
        public int tempoSmoothingPasses = 2;

        public static SynchTheoryOptions Default()
        {
            return new SynchTheoryOptions();
        }
    }

    public sealed class SynchTheoryAudioData
    {
        public float[] monoSamples = Array.Empty<float>();
        public int sampleRate;

        public double DurationSeconds
        {
            get
            {
                if (sampleRate <= 0 || monoSamples == null)
                    return 0.0;
                return monoSamples.Length / (double)sampleRate;
            }
        }

        public bool IsValid => sampleRate > 0 && monoSamples != null && monoSamples.Length > 0;
    }

    public sealed class SynchTheoryScoreMap
    {
        public double durationSeconds;
        public double defaultTempoBpm = 120.0;
        public List<SynchTheoryBeat> beats = new List<SynchTheoryBeat>();
        public List<SynchTheoryScoreEvent> events = new List<SynchTheoryScoreEvent>();
        public List<SynchTheoryAnchor> anchors = new List<SynchTheoryAnchor>();
    }

    public sealed class SynchTheoryBeat
    {
        public int index;
        public double beatPosition;
        public double chartTimeSeconds;
        public double audioTimeSeconds;
        public bool isDownbeat;
        public bool isAnchor;
        public bool isGenerated;
        public double confidence;
    }

    public sealed class SynchTheoryAnchor
    {
        public string id;
        public double beatPosition;
        public double audioTimeSeconds;
        public bool locked;
        public string label;
    }

    public sealed class SynchTheoryScoreEvent
    {
        public string id;
        public double beatPosition;
        public double chartTimeSeconds;
        public double durationSeconds;
        public double weight = 1.0;
        public int pitchClass = -1;
        public bool isDownbeat;
        public bool isPercussive;
    }

    public sealed class SynchTheoryAlignmentResult
    {
        public bool success;
        public string message;
        public double confidence;
        public double startBeat;
        public double endBeat;
        public List<SynchTheoryBeat> generatedBeats = new List<SynchTheoryBeat>();
        public List<SynchTheoryRegionResult> regions = new List<SynchTheoryRegionResult>();
        public List<string> warnings = new List<string>();
    }

    public sealed class SynchTheoryRegionResult
    {
        public double startBeat;
        public double endBeat;
        public double startAudioTimeSeconds;
        public double endAudioTimeSeconds;
        public double confidence;
        public int scoreFrameCount;
        public int audioFrameCount;
        public int alignedFrameCount;
        public string message;
    }

    internal sealed class SynchTheoryFeatureSequence
    {
        public double startTimeSeconds;
        public double frameRate;
        public float[] onset = Array.Empty<float>();
        public float[] energy = Array.Empty<float>();
        public float[] beat = Array.Empty<float>();

        public int Count => onset?.Length ?? 0;

        public double TimeAtFrame(int frame)
        {
            if (frameRate <= 0.0001)
                return startTimeSeconds;
            return startTimeSeconds + frame / frameRate;
        }
    }
}
