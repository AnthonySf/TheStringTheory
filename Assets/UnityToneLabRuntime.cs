using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using Unity.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class UnityToneLabRuntime : MonoBehaviour
{
    private const int PreferredSampleRate = 48000;
    private const int PreferredDspBufferSize = 128;
    private const int UltraLowDspBufferSize = 64;
    private const int SafeDspBufferSize = 256;
    private const int DriverManagedPortAudioMaxBlockFrames = 8192;
    private const float SettingsSaveDelaySeconds = 0.18f;
    private const int MicrophoneClipLengthSeconds = 1;
    private const float MicrophoneStartupTimeoutSeconds = 2f;
    private const int MinimumStartupLeadSamples = 512;
    private const int MaxDelayMilliseconds = 2500;
    private const int MaxChorusMilliseconds = 64;
    private const float MinRigGainDb = -36f;
    private const float MaxRigGainDb = 36f;
    private const float MinGlobalInputTrimDb = -36f;
    private const float MaxGlobalInputTrimDb = 12f;
    private const float DefaultGlobalInputTrimDb = 0f;
    private const float MinGlobalOutputGainDb = -12f;
    private const float MaxGlobalOutputGainDb = 12f;
    private const float DefaultGlobalOutputGainDb = 0f;
    private const float DefaultRigInputGainDb = 14f;
    private const float DefaultRigOutputGainDb = 10f;
    private const float MaxIntermediateAudioMagnitude = 16f;
    private const float LiveAudioDiagnosticsIntervalSeconds = 1f;
    public const float MaxMonitorVolumePercent = 200f;

    [Serializable]
    public sealed class ToneLabSettings
    {
        public string input_device_name = string.Empty;
        public string output_device_name = string.Empty;
        public int monitoring_buffer_size = PreferredDspBufferSize;
        public string selected_preset_id = string.Empty;
        public List<ToneLabPreset> presets = new List<ToneLabPreset>();
        public List<ToneLabPedalSlot> pedal_chain = new List<ToneLabPedalSlot>();
        public float global_input_trim_db = DefaultGlobalInputTrimDb;
        public float global_output_gain_db = DefaultGlobalOutputGainDb;
        public float input_gain_db = DefaultRigInputGainDb;
        public float output_gain_db = DefaultRigOutputGainDb;
        public bool dist_enabled;
        public bool chorus_enabled;
        public bool phaser_enabled;
        public bool delay_enabled;
        public bool reverb_enabled;
        public bool comp_enabled;
        public float dist_drive_db = 18f;
        public float chorus_rate_hz = 0.8f;
        public float chorus_depth = 0.35f;
        public float chorus_mix = 0.25f;
        public float phaser_rate_hz = 0.5f;
        public float phaser_depth = 0.5f;
        public float phaser_mix = 0.25f;
        public float phaser_center_hz = 1200f;
        public float phaser_feedback = 0.1f;
        public float delay_seconds = 0.28f;
        public float delay_feedback = 0.30f;
        public float delay_mix = 0.20f;
        public float reverb_room_size = 0.45f;
        public float reverb_damping = 0.35f;
        public float reverb_wet = 0.22f;
        public float reverb_dry = 0.95f;
        public float reverb_width = 1f;
        public float reverb_freeze;
        public float comp_threshold_db = -18f;
        public float comp_ratio = 3f;
        public float comp_attack_ms = 10f;
        public float comp_release_ms = 120f;
    }

    [Serializable]
    public sealed class ToneLabPreset
    {
        public string preset_id = string.Empty;
        public string preset_name = string.Empty;
        public float input_gain_db = DefaultRigInputGainDb;
        public float output_gain_db = DefaultRigOutputGainDb;
        public List<ToneLabPedalSlot> pedal_chain = new List<ToneLabPedalSlot>();
    }

    public enum ToneLabPedalType
    {
        NoiseGate,
        Amp,
        CabSim,
        StudioEq,
        Distortion,
        Chorus,
        Phaser,
        Delay,
        Reverb,
        Compressor,
        Lv2Plugin,
        NamModel
    }

    [Serializable]
    public sealed class ToneLabPedalSlot
    {
        public string pedal_instance_id = string.Empty;
        public ToneLabPedalType pedal_type;
        public string descriptor_id = string.Empty;
        public bool enabled = true;
        public string settings_json = string.Empty;
    }

    private sealed class CompiledPedalSlot
    {
        public ToneLabPedalSlot slot;
        public IToneLabPedalDescriptor descriptor;
        public IToneLabPedalProcessor processor;
    }

    private ToneLabPreset playbackPresetOverride;
    private bool playbackPresetOverrideActive;
    private string playbackPresetOverrideId = string.Empty;

    private sealed class SoftClipDistortionEffect
    {
        public void Process(float[] data, float driveDb)
        {
            float drive = DbToLinear(driveDb);
            float scale = 2f / Mathf.PI;
            for (int i = 0; i < data.Length; i++)
                data[i] = scale * Mathf.Atan(data[i] * drive);
        }
    }

    private sealed class ChorusEffect
    {
        private float[][] delayBuffers;
        private int[] writeIndices;
        private int delayBufferLength;
        private float phase;
        private int sampleRate;
        private int channelCount;

        public void Reset(int newSampleRate, int newChannelCount)
        {
            sampleRate = Mathf.Max(1, newSampleRate);
            channelCount = Mathf.Max(1, newChannelCount);
            delayBufferLength = Mathf.Max(32, Mathf.CeilToInt(sampleRate * (MaxChorusMilliseconds / 1000f)) + 4);
            delayBuffers = new float[channelCount][];
            writeIndices = new int[channelCount];
            for (int channel = 0; channel < channelCount; channel++)
                delayBuffers[channel] = new float[delayBufferLength];
            phase = 0f;
        }

        public void Process(float[] data, int channels, float rateHz, float depth, float mix)
        {
            if (data == null || data.Length == 0 || delayBuffers == null || writeIndices == null || sampleRate <= 0 || channelCount != channels || channels <= 0 || delayBufferLength <= 1)
                return;

            int safeChannelCount = Mathf.Min(channels, Mathf.Min(delayBuffers.Length, writeIndices.Length));
            if (safeChannelCount <= 0)
                return;

            float clampedDepth = Mathf.Clamp01(depth);
            float clampedMix = Mathf.Clamp01(mix);
            float baseDelaySamples = Mathf.Max(2f, sampleRate * 0.012f);
            float modDelaySamples = sampleRate * 0.008f * clampedDepth;
            float phaseStep = (2f * Mathf.PI * Mathf.Clamp(rateHz, 0.05f, 6f)) / sampleRate;

            for (int frame = 0; frame < data.Length; frame += channels)
            {
                float framePhase = phase;
                int frameChannelCount = Mathf.Min(safeChannelCount, data.Length - frame);
                for (int channel = 0; channel < frameChannelCount; channel++)
                {
                    float[] channelBuffer = delayBuffers[channel];
                    if (channelBuffer == null || channelBuffer.Length < delayBufferLength)
                        continue;

                    float input = data[frame + channel];
                    float channelPhase = framePhase + (channel * 0.65f);
                    float modulation = 0.5f + (0.5f * Mathf.Sin(channelPhase));
                    float delaySamples = baseDelaySamples + (modDelaySamples * modulation);
                    int writeIndex = writeIndices[channel];
                    if ((uint)writeIndex >= (uint)delayBufferLength)
                    {
                        writeIndex %= delayBufferLength;
                        if (writeIndex < 0)
                            writeIndex += delayBufferLength;
                        writeIndices[channel] = writeIndex;
                    }

                    float readIndex = writeIndex - delaySamples;
                    while (readIndex < 0f)
                        readIndex += delayBufferLength;
                    while (readIndex >= delayBufferLength)
                        readIndex -= delayBufferLength;

                    int readIndexA = Mathf.Clamp((int)readIndex, 0, delayBufferLength - 1);
                    int readIndexB = (readIndexA + 1) % delayBufferLength;
                    float lerp = readIndex - readIndexA;
                    float delayed = Mathf.Lerp(channelBuffer[readIndexA], channelBuffer[readIndexB], lerp);
                    channelBuffer[writeIndex] = input;
                    data[frame + channel] = (input * (1f - clampedMix)) + (delayed * clampedMix);
                    writeIndices[channel] = (writeIndex + 1) % delayBufferLength;
                }

                phase += phaseStep;
                if (phase > 2f * Mathf.PI)
                    phase -= 2f * Mathf.PI;
            }
        }
    }

    private sealed class DelayEffect
    {
        private float[][] delayBuffers;
        private int[] writeIndices;
        private int delayBufferLength;
        private int sampleRate;
        private int channelCount;

        public void Reset(int newSampleRate, int newChannelCount)
        {
            sampleRate = Mathf.Max(1, newSampleRate);
            channelCount = Mathf.Max(1, newChannelCount);
            delayBufferLength = Mathf.Max(64, Mathf.CeilToInt(sampleRate * (MaxDelayMilliseconds / 1000f)) + 4);
            delayBuffers = new float[channelCount][];
            writeIndices = new int[channelCount];
            for (int channel = 0; channel < channelCount; channel++)
                delayBuffers[channel] = new float[delayBufferLength];
        }

        public void Process(float[] data, int channels, float delaySeconds, float feedback, float mix)
        {
            if (delayBuffers == null || sampleRate <= 0 || channelCount != channels)
                return;

            int delaySamples = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(delaySeconds, 0.01f, MaxDelayMilliseconds / 1000f) * sampleRate), 1, delayBufferLength - 2);
            float clampedFeedback = Mathf.Clamp(feedback, 0f, 0.95f);
            float clampedMix = Mathf.Clamp01(mix);

            for (int frame = 0; frame < data.Length; frame += channels)
            {
                for (int channel = 0; channel < channels; channel++)
                {
                    float input = data[frame + channel];
                    int readIndex = writeIndices[channel] - delaySamples;
                    if (readIndex < 0)
                        readIndex += delayBufferLength;

                    float delayed = delayBuffers[channel][readIndex];
                    delayBuffers[channel][writeIndices[channel]] = input + (delayed * clampedFeedback);
                    data[frame + channel] = (input * (1f - clampedMix)) + (delayed * clampedMix);
                    writeIndices[channel] = (writeIndices[channel] + 1) % delayBufferLength;
                }
            }
        }
    }

    private sealed class PhaserEffect
    {
        private const int StageCount = 4;

        private float[,] stageInputHistory;
        private float[,] stageOutputHistory;
        private float[] feedbackHistory;
        private float phase;
        private int sampleRate;
        private int channelCount;

        public void Reset(int newSampleRate, int newChannelCount)
        {
            sampleRate = Mathf.Max(1, newSampleRate);
            channelCount = Mathf.Max(1, newChannelCount);
            stageInputHistory = new float[channelCount, StageCount];
            stageOutputHistory = new float[channelCount, StageCount];
            feedbackHistory = new float[channelCount];
            phase = 0f;
        }

        public void Process(float[] data, int channels, float rateHz, float depth, float mix, float centerHz, float feedback)
        {
            if (stageInputHistory == null || sampleRate <= 0 || channelCount != channels)
                return;

            float clampedDepth = Mathf.Clamp01(depth);
            float clampedMix = Mathf.Clamp01(mix);
            float clampedFeedback = Mathf.Clamp(feedback, -0.92f, 0.92f);
            float clampedCenterHz = Mathf.Clamp(centerHz, 120f, 4200f);
            float phaseStep = (2f * Mathf.PI * Mathf.Clamp(rateHz, 0.05f, 5f)) / sampleRate;

            for (int frame = 0; frame < data.Length; frame += channels)
            {
                float lfo = 0.5f + (0.5f * Mathf.Sin(phase));
                float frequency = Mathf.Lerp(clampedCenterHz * (1f - (0.75f * clampedDepth)), clampedCenterHz * (1f + (1.25f * clampedDepth)), lfo);
                float tangent = Mathf.Tan(Mathf.PI * Mathf.Clamp(frequency, 20f, sampleRate * 0.45f) / sampleRate);
                float coefficient = (1f - tangent) / Mathf.Max(0.0001f, 1f + tangent);

                for (int channel = 0; channel < channels; channel++)
                {
                    float input = data[frame + channel];
                    float stageValue = input + (feedbackHistory[channel] * clampedFeedback);
                    for (int stage = 0; stage < StageCount; stage++)
                    {
                        float output = (-coefficient * stageValue) + stageInputHistory[channel, stage] + (coefficient * stageOutputHistory[channel, stage]);
                        stageInputHistory[channel, stage] = stageValue;
                        stageOutputHistory[channel, stage] = output;
                        stageValue = output;
                    }

                    feedbackHistory[channel] = stageValue;
                    data[frame + channel] = (input * (1f - clampedMix)) + (stageValue * clampedMix);
                }

                phase += phaseStep;
                if (phase > 2f * Mathf.PI)
                    phase -= 2f * Mathf.PI;
            }
        }
    }

    private sealed class CompressorEffect
    {
        private float envelope;

        public void Reset()
        {
            envelope = 0f;
        }

        public void Process(float[] data, int channels, int sampleRate, float thresholdDb, float ratio, float attackMs, float releaseMs)
        {
            float threshold = Mathf.Max(0.0001f, DbToLinear(thresholdDb));
            float clampedRatio = Mathf.Max(1f, ratio);
            float attackCoeff = Mathf.Exp(-1f / Mathf.Max(0.0001f, attackMs * 0.001f * sampleRate));
            float releaseCoeff = Mathf.Exp(-1f / Mathf.Max(0.0001f, releaseMs * 0.001f * sampleRate));

            for (int frame = 0; frame < data.Length; frame += channels)
            {
                float detector = 0f;
                for (int channel = 0; channel < channels; channel++)
                    detector = Mathf.Max(detector, Mathf.Abs(data[frame + channel]));

                if (detector > envelope)
                    envelope = (attackCoeff * envelope) + ((1f - attackCoeff) * detector);
                else
                    envelope = (releaseCoeff * envelope) + ((1f - releaseCoeff) * detector);

                float gain = 1f;
                if (envelope > threshold)
                {
                    float over = envelope / threshold;
                    gain = Mathf.Pow(over, -(clampedRatio - 1f) / clampedRatio);
                }

                for (int channel = 0; channel < channels; channel++)
                    data[frame + channel] *= gain;
            }
        }
    }

    private sealed class ReverbEffect
    {
        private sealed class CombFilter
        {
            private readonly float[] buffer;
            private int index;
            private float feedback;
            private float damp1;
            private float damp2;
            private float filterStore;

            public CombFilter(int length)
            {
                buffer = new float[Mathf.Max(1, length)];
            }

            public void Reset()
            {
                Array.Clear(buffer, 0, buffer.Length);
                index = 0;
                filterStore = 0f;
            }

            public void SetFeedback(float value)
            {
                feedback = value;
            }

            public void SetDamping(float value)
            {
                damp1 = value;
                damp2 = 1f - value;
            }

            public float Process(float input)
            {
                float output = buffer[index];
                filterStore = (output * damp2) + (filterStore * damp1);
                buffer[index] = input + (filterStore * feedback);
                index++;
                if (index >= buffer.Length)
                    index = 0;
                return output;
            }
        }

        private sealed class AllPassFilter
        {
            private readonly float[] buffer;
            private int index;
            private readonly float feedback;

            public AllPassFilter(int length, float feedback)
            {
                buffer = new float[Mathf.Max(1, length)];
                this.feedback = feedback;
            }

            public void Reset()
            {
                Array.Clear(buffer, 0, buffer.Length);
                index = 0;
            }

            public float Process(float input)
            {
                float buffered = buffer[index];
                float output = -input + buffered;
                buffer[index] = input + (buffered * feedback);
                index++;
                if (index >= buffer.Length)
                    index = 0;
                return output;
            }
        }

        private CombFilter[] combLeft;
        private CombFilter[] combRight;
        private AllPassFilter[] allPassLeft;
        private AllPassFilter[] allPassRight;
        private int sampleRate;

        public void Reset(int newSampleRate)
        {
            sampleRate = Mathf.Max(1, newSampleRate);
            int[] combTunings = ScaleTunings(new[] { 1116, 1188, 1277, 1356 }, sampleRate);
            int[] allPassTunings = ScaleTunings(new[] { 556, 441 }, sampleRate);

            combLeft = new CombFilter[combTunings.Length];
            combRight = new CombFilter[combTunings.Length];
            for (int i = 0; i < combTunings.Length; i++)
            {
                combLeft[i] = new CombFilter(combTunings[i]);
                combRight[i] = new CombFilter(combTunings[i] + Mathf.RoundToInt(23f * sampleRate / 44100f));
            }

            allPassLeft = new AllPassFilter[allPassTunings.Length];
            allPassRight = new AllPassFilter[allPassTunings.Length];
            for (int i = 0; i < allPassTunings.Length; i++)
            {
                allPassLeft[i] = new AllPassFilter(allPassTunings[i], 0.5f);
                allPassRight[i] = new AllPassFilter(allPassTunings[i] + Mathf.RoundToInt(23f * sampleRate / 44100f), 0.5f);
            }

            ResetState();
        }

        public void ResetState()
        {
            if (combLeft != null)
            {
                for (int i = 0; i < combLeft.Length; i++)
                {
                    combLeft[i].Reset();
                    combRight[i].Reset();
                }
            }

            if (allPassLeft != null)
            {
                for (int i = 0; i < allPassLeft.Length; i++)
                {
                    allPassLeft[i].Reset();
                    allPassRight[i].Reset();
                }
            }
        }

        public void Process(float[] data, int channels, float roomSize, float damping, float wet, float dry, float width, float freeze)
        {
            if (combLeft == null || sampleRate <= 0)
                return;

            float room = Mathf.Lerp(0.45f, 0.93f, Mathf.Clamp01(roomSize));
            float damp = Mathf.Clamp01(damping);
            bool freezeMode = freeze >= 0.5f;
            float wetLevel = Mathf.Clamp01(wet);
            float dryLevel = Mathf.Clamp01(dry);
            float stereoWidth = Mathf.Clamp01(width);
            float wet1 = wetLevel * ((stereoWidth * 0.5f) + 0.5f);
            float wet2 = wetLevel * ((1f - stereoWidth) * 0.5f);
            float feedback = freezeMode ? 0.995f : room;
            float inputGain = freezeMode ? 0f : 0.23f;

            for (int i = 0; i < combLeft.Length; i++)
            {
                combLeft[i].SetFeedback(feedback);
                combRight[i].SetFeedback(feedback);
                combLeft[i].SetDamping(damp);
                combRight[i].SetDamping(damp);
            }

            for (int frame = 0; frame < data.Length; frame += channels)
            {
                float inL = data[frame];
                float inR = channels > 1 ? data[frame + 1] : inL;
                float input = ((inL + inR) * 0.5f) * inputGain;

                float outL = 0f;
                float outR = 0f;
                for (int i = 0; i < combLeft.Length; i++)
                {
                    outL += combLeft[i].Process(input);
                    outR += combRight[i].Process(input);
                }

                for (int i = 0; i < allPassLeft.Length; i++)
                {
                    outL = allPassLeft[i].Process(outL);
                    outR = allPassRight[i].Process(outR);
                }

                data[frame] = (inL * dryLevel) + (outL * wet1) + (outR * wet2);
                if (channels > 1)
                    data[frame + 1] = (inR * dryLevel) + (outR * wet1) + (outL * wet2);
            }
        }

        private static int[] ScaleTunings(int[] baseTunings, int sampleRate)
        {
            int[] scaled = new int[baseTunings.Length];
            float scale = sampleRate / 44100f;
            for (int i = 0; i < baseTunings.Length; i++)
                scaled[i] = Mathf.Max(1, Mathf.RoundToInt(baseTunings[i] * scale));
            return scaled;
        }
    }

    private AudioSource monitorSource;
    private ToneLabSettings settings;
    private string[] inputDevices = Array.Empty<string>();
    private string[] outputDevices = Array.Empty<string>();
    private float[] portAudioProcessBuffer = Array.Empty<float>();
    private readonly Dictionary<int, float[]> portAudioProcessBuffersBySampleCount = new Dictionary<int, float[]>();
    private float[] unityOutputMixBuffer = Array.Empty<float>();
    private string statusMessage = "Stopped";
    private bool settingsLoaded;
    private bool settingsDirty;
    private float nextSettingsSaveTime = -1f;
    private bool monitoring;
    private bool awaitingMicrophoneStart;
    private string pendingDeviceName = string.Empty;
    private AudioClip pendingMicrophoneClip;
    private AudioClip monitorDriverClip;
    private float microphoneStartupDeadline;
    private AudioConfiguration? cachedPreToneLabAudioConfiguration;
    private int activeSampleRate = PreferredSampleRate;
    private int activeDspBufferSize = PreferredDspBufferSize;
    private int desiredMonitorLeadSamples = MinimumStartupLeadSamples;
    private int microphoneClipFrameCount;
    private int microphoneClipChannelCount = 1;
    private int microphoneClipReadFramePosition;
    private float[] microphoneSnapshotBuffer = Array.Empty<float>();
    private float[] microphoneInputRingBuffer = Array.Empty<float>();
    private float[] microphoneRawInputCallbackBuffer = Array.Empty<float>();
    private int microphoneInputRingWriteIndex;
    private int microphoneInputRingReadIndex;
    private int microphoneInputRingCount;
    private readonly object microphoneBufferLock = new object();
    private string inputRouteLabel = "Automatic Input";
    private string outputRouteLabel = "System Default Output";
    private string activeHostApiName = string.Empty;
    private ToneLabPortAudio.DuplexStream portAudioStream;
    private ToneLabPortAudio.SplitStream portAudioSplitStream;
    private ToneLabPortAudio.DeviceDescriptor[] portAudioAllDevices = Array.Empty<ToneLabPortAudio.DeviceDescriptor>();
    private ToneLabPortAudio.DeviceDescriptor[] portAudioInputDevices = Array.Empty<ToneLabPortAudio.DeviceDescriptor>();
    private ToneLabPortAudio.DeviceDescriptor[] portAudioOutputDevices = Array.Empty<ToneLabPortAudio.DeviceDescriptor>();
    private bool usingPortAudioBackend;
    private volatile bool sharedInputRouteActive;
    private volatile bool sharedInputSubmitDisabled;
    private volatile bool sharedInputSubmitFailurePending;
    private bool sharedInputRestoreDeferredAfterRouteFailure;
    private SharedInputRouteInfo activeSharedInputRoute;
    private float monitorVolumePercent = 100f;
    private AdvancedRoutingOptions advancedRoutingOptions = new AdvancedRoutingOptions();
    private string lastRoutingDiagnostics = string.Empty;
    private string lastRoutingAttemptSummary = string.Empty;
    private bool unityOutputCaptureActive;
    private int unityOutputCaptureChannels = 2;
    private int unityOutputCaptureSampleRate = PreferredSampleRate;
    private float[] unityOutputCaptureRingBuffer = Array.Empty<float>();
    private int unityOutputCaptureWriteIndex;
    private int unityOutputCaptureReadIndex;
    private int unityOutputCaptureCount;
    private int unityOutputCaptureUnderrunCount;
    private int unityOutputCaptureOverflowCount;
    private int unityOutputCapturePeakQueuedSamples;
    private float unityOutputLimiterGain = 1f;
    private readonly object unityOutputCaptureLock = new object();
    private float latestRawInputPeak;
    private float latestRawInputRms;
    private float latestRawInputDc;
    private float latestRawInputUnclampedPeak;
    private float latestRawInputClipPercent;
    private int latestRawInputNonFiniteSamples;
    private float latestProcessedPeak;
    private float latestProcessedRms;
    private float latestProcessedDc;
    private float latestProcessedUnclampedPeak;
    private float latestProcessedClipPercent;
    private int latestProcessedNonFiniteSamples;
    private int latestAudioDiagnosticsSampleRate;
    private int latestAudioDiagnosticsInputChannels;
    private int latestAudioDiagnosticsOutputChannels;
    private int latestAudioDiagnosticsFrameCount;
    private string latestAudioDiagnosticsInputChannelMode = SharedAudioInputChannelModes.Input1;
    private int liveAudioDiagnosticsBurstLogsRemaining;
    private float nextLiveAudioDiagnosticsLogTime = -1f;
    private long portAudioProcessBlockCount;
    private long portAudioProcessFrameCount;
    private long sharedDetectorSubmitCount;
    private long sharedDetectorRejectedCount;
    private volatile bool unityRecorderCaptureActive;
    private int unityRecorderCaptureChannels = 2;
    private int unityRecorderCaptureSampleRate = PreferredSampleRate;
    private float[] unityRecorderCaptureRingBuffer = Array.Empty<float>();
    private int unityRecorderCaptureWriteIndex;
    private int unityRecorderCaptureReadIndex;
    private int unityRecorderCaptureCount;
    private int unityRecorderCaptureUnderrunCount;
    private int unityRecorderCaptureOverflowCount;
    private int unityRecorderCapturePeakQueuedSamples;
    private readonly object unityRecorderCaptureLock = new object();
    private GuitarBridgeServer unifiedSongSourceOwner;
    private readonly List<UnityToneLabAudioSourceTap> unifiedSongSourceTaps = new List<UnityToneLabAudioSourceTap>();
    private UnityToneLabAudioSourceTap[] unifiedSongSourceTapSnapshot = Array.Empty<UnityToneLabAudioSourceTap>();
    private float nextUnifiedSongSourceRefreshTime = -1f;

    private CompiledPedalSlot[] compiledPedalChain = Array.Empty<CompiledPedalSlot>();
    private int preparedCompiledPedalSampleRate = -1;
    private int preparedCompiledPedalChannelCount = -1;
    private static readonly string[] LatencyPresetLabels = { "Ultra Low (64)", "Low (128)", "Safe (256)" };
    private static readonly ToneLabPedalType[] DefaultPedalOrder =
    {
        ToneLabPedalType.NoiseGate,
        ToneLabPedalType.Compressor,
        ToneLabPedalType.Distortion,
        ToneLabPedalType.Amp,
        ToneLabPedalType.CabSim,
        ToneLabPedalType.StudioEq,
        ToneLabPedalType.Chorus,
        ToneLabPedalType.Phaser,
        ToneLabPedalType.Delay,
        ToneLabPedalType.Reverb
    };

    public ToneLabSettings CurrentSettings
    {
        get
        {
            EnsureSettingsLoaded();
            return settings;
        }
    }

    public string[] InputDevices => inputDevices;
    public string[] OutputDevices => outputDevices;
    public string[] MonitoringLatencyOptions => LatencyPresetLabels;
    public string CurrentMonitoringLatencyOption => GetLatencyPresetLabel(CurrentSettings.monitoring_buffer_size);
    public ToneLabPreset[] CurrentPresets
    {
        get
        {
            EnsureSettingsLoaded();
            EnsurePresetLibrary(settings);
            return settings.presets
                .Select(ClonePreset)
                .ToArray();
        }
    }
    public string CurrentPresetId
    {
        get
        {
            EnsureSettingsLoaded();
            EnsurePresetLibrary(settings);
            return settings.selected_preset_id ?? string.Empty;
        }
    }
    public string FindPresetIdByName(string presetName)
    {
        if (string.IsNullOrWhiteSpace(presetName))
            return string.Empty;

        EnsureSettingsLoaded();
        EnsurePresetLibrary(settings);
        if (settings?.presets == null)
            return string.Empty;

        for (int i = 0; i < settings.presets.Count; i++)
        {
            ToneLabPreset preset = settings.presets[i];
            if (preset != null &&
                string.Equals(preset.preset_name ?? string.Empty, presetName, StringComparison.OrdinalIgnoreCase))
            {
                return preset.preset_id?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }
    public ToneLabPedalSlot[] CurrentPedalChain
    {
        get
        {
            EnsureSettingsLoaded();
            EnsurePedalChain(settings);
            return settings.pedal_chain
                .Select(ClonePedalSlot)
                .ToArray();
        }
    }
    public string ActiveMonitoringLatencyOption => GetLatencyPresetLabel((monitoring || awaitingMicrophoneStart) ? activeDspBufferSize : CurrentSettings.monitoring_buffer_size);
    public string ActiveAudioBackendLabel => usingPortAudioBackend ? "PortAudio" : (monitoring || awaitingMicrophoneStart ? "Unity Audio" : "Idle");
    public string ActiveHostApiLabel => string.IsNullOrWhiteSpace(activeHostApiName) ? (usingPortAudioBackend ? "PortAudio" : (monitoring || awaitingMicrophoneStart ? "Unity Audio" : "-")) : activeHostApiName;
    public bool IsMonitoring => monitoring;
    public bool IsAwaitingStartup => awaitingMicrophoneStart;
    public string StatusMessage => statusMessage;
    public int ActiveSampleRate => activeSampleRate;
    public int ActiveDspBufferSize => activeDspBufferSize;
    public string InputRouteLabel => inputRouteLabel;
    public string OutputRouteLabel => outputRouteLabel;
    public float MonitorVolumePercent => monitorVolumePercent;
    public string LastRoutingDiagnostics => lastRoutingDiagnostics;
    public string LastRoutingAttemptSummary => lastRoutingAttemptSummary;

    public string BuildAudioDiagnosticSnapshot()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("ToneLabRuntime");
        builder.AppendLine($"  monitoring: {monitoring}");
        builder.AppendLine($"  awaitingMicrophoneStart: {awaitingMicrophoneStart}");
        builder.AppendLine($"  usingPortAudioBackend: {usingPortAudioBackend}");
        builder.AppendLine($"  activeBackend: {ActiveAudioBackendLabel}");
        builder.AppendLine($"  activeHostApi: {ActiveHostApiLabel}");
        builder.AppendLine($"  activeInput: {(string.IsNullOrWhiteSpace(inputRouteLabel) ? "-" : inputRouteLabel)}");
        builder.AppendLine($"  activeOutput: {(string.IsNullOrWhiteSpace(outputRouteLabel) ? "-" : outputRouteLabel)}");
        builder.AppendLine($"  activeSampleRate: {activeSampleRate}");
        builder.AppendLine($"  activeBuffer: {FormatActiveBufferLabel(activeDspBufferSize)}");
        builder.AppendLine($"  status: {(string.IsNullOrWhiteSpace(statusMessage) ? "-" : statusMessage)}");
        builder.AppendLine($"  stream: {GetPortAudioDiagnosticSummary()}");
        builder.AppendLine($"  rawPeak: {latestRawInputPeak:0.0000}");
        builder.AppendLine($"  rawRms: {latestRawInputRms:0.0000}");
        builder.AppendLine($"  rawDc: {latestRawInputDc:0.0000}");
        builder.AppendLine($"  rawUnclampedPeak: {latestRawInputUnclampedPeak:0.0000}");
        builder.AppendLine($"  rawClipPercent: {latestRawInputClipPercent:0.###}");
        builder.AppendLine($"  rawNonFiniteSamples: {latestRawInputNonFiniteSamples}");
        builder.AppendLine($"  processedPeak: {latestProcessedPeak:0.0000}");
        builder.AppendLine($"  processedRms: {latestProcessedRms:0.0000}");
        builder.AppendLine($"  processedDc: {latestProcessedDc:0.0000}");
        builder.AppendLine($"  processedUnclampedPeak: {latestProcessedUnclampedPeak:0.0000}");
        builder.AppendLine($"  processedClipPercent: {latestProcessedClipPercent:0.###}");
        builder.AppendLine($"  processedNonFiniteSamples: {latestProcessedNonFiniteSamples}");
        builder.AppendLine($"  rawInputClippingLikely: {IsRawInputClippingLikely()}");
        builder.AppendLine($"  processedOutputClippingLikely: {IsProcessedOutputClippingLikely()}");
        builder.AppendLine($"  latestInputChannels: {latestAudioDiagnosticsInputChannels}");
        builder.AppendLine($"  latestOutputChannels: {latestAudioDiagnosticsOutputChannels}");
        builder.AppendLine($"  latestInputChannelMode: {latestAudioDiagnosticsInputChannelMode}");
        builder.AppendLine($"  processBlocks: {Volatile.Read(ref portAudioProcessBlockCount)}");
        builder.AppendLine($"  processFrames: {Volatile.Read(ref portAudioProcessFrameCount)}");
        builder.AppendLine($"  sharedDetectorActive: {sharedInputRouteActive}");
        builder.AppendLine($"  sharedDetectorSubmits: {Volatile.Read(ref sharedDetectorSubmitCount)}");
        builder.AppendLine($"  sharedDetectorRejects: {Volatile.Read(ref sharedDetectorRejectedCount)}");
        builder.AppendLine($"  currentPreset: {GetCurrentDiagnosticPresetName()}");
        builder.AppendLine($"  currentChain: {GetCurrentDiagnosticChainSummary()}");
        builder.AppendLine("EffectiveToneLabPreset");
        ToneLabPreset effectivePreset = GetCurrentDiagnosticPresetSnapshot();
        builder.AppendLine(effectivePreset != null ? JsonUtility.ToJson(effectivePreset, true) : "(null)");
        builder.AppendLine("EffectivePedalResolution");
        builder.AppendLine(BuildCurrentDiagnosticPedalResolutionSummary());
        builder.AppendLine($"  externalPedalRefreshUtc: {ToneLabPedalRegistry.LastExternalRefreshUtc:O}");
        builder.AppendLine($"  externalPedalRefreshSummary: {ToneLabPedalRegistry.LastExternalRefreshSummary}");
        builder.AppendLine($"  registeredPedalDescriptorCount: {ToneLabPedalRegistry.AllDescriptors.Count}");
        builder.AppendLine($"  playbackOverrideActive: {playbackPresetOverrideActive}");
        builder.AppendLine($"  monitorVolumePercent: {monitorVolumePercent:0.##}");

        if (settings != null)
        {
            builder.AppendLine("ToneLabSettings");
            builder.AppendLine($"  inputDevice: {(string.IsNullOrWhiteSpace(settings.input_device_name) ? "Automatic" : settings.input_device_name)}");
            builder.AppendLine($"  outputDevice: {(string.IsNullOrWhiteSpace(settings.output_device_name) ? "Automatic" : settings.output_device_name)}");
            builder.AppendLine($"  monitoringBufferSize: {FormatActiveBufferLabel(settings.monitoring_buffer_size)}");
            builder.AppendLine($"  selectedPresetId: {settings.selected_preset_id}");
            builder.AppendLine($"  inputGainDb: {settings.input_gain_db:0.###}");
            builder.AppendLine($"  outputGainDb: {settings.output_gain_db:0.###}");
            builder.AppendLine($"  globalInputTrimDb: {settings.global_input_trim_db:0.###}");
            builder.AppendLine($"  globalOutputGainDb: {settings.global_output_gain_db:0.###}");
            builder.AppendLine($"  presetCount: {(settings.presets != null ? settings.presets.Count : 0)}");
        }

        if (advancedRoutingOptions != null)
        {
            builder.AppendLine("AdvancedAudio");
            builder.AppendLine($"  betaEnabled: {advancedRoutingOptions.betaEnabled}");
            builder.AppendLine($"  inputChannelMode: {SharedAudioInputChannelModes.Normalize(advancedRoutingOptions.inputChannelMode)}");
            builder.AppendLine($"  backendMode: {SharedAudioBackendModes.Normalize(advancedRoutingOptions.backendMode)}");
            builder.AppendLine($"  allowFallback: {advancedRoutingOptions.allowFallback}");
            builder.AppendLine($"  preferredInput: {(string.IsNullOrWhiteSpace(advancedRoutingOptions.preferredInputDeviceName) ? "Automatic" : advancedRoutingOptions.preferredInputDeviceName)}");
            builder.AppendLine($"  preferredOutput: {(string.IsNullOrWhiteSpace(advancedRoutingOptions.preferredOutputDeviceName) ? "Automatic" : advancedRoutingOptions.preferredOutputDeviceName)}");
            builder.AppendLine($"  sampleRate: {SharedAudioSampleRateOptions.ToLabel(advancedRoutingOptions.sampleRate)}");
            builder.AppendLine($"  bufferSize: {FormatActiveBufferLabel(advancedRoutingOptions.bufferSize)}");
            builder.AppendLine($"  splitInputOutput: {advancedRoutingOptions.splitInputOutputEnabled}");
            builder.AppendLine($"  unifiedOutput: {advancedRoutingOptions.unifiedOutputEnabled}");
            builder.AppendLine($"  unityRecorderCapture: {advancedRoutingOptions.unityRecorderCaptureEnabled}");
        }

        builder.AppendLine("DeviceCatalog");
        builder.AppendLine(BuildDeviceCatalogLog(includeAllPortAudioInputs: true, includeAllPortAudioOutputs: true));

        if (!string.IsNullOrWhiteSpace(lastRoutingDiagnostics))
        {
            builder.AppendLine("LastRoutingDiagnostics");
            builder.AppendLine(lastRoutingDiagnostics);
        }

        if (!string.IsNullOrWhiteSpace(lastRoutingAttemptSummary))
        {
            builder.AppendLine("LastRoutingAttempts");
            builder.AppendLine(lastRoutingAttemptSummary);
        }

        SharedInputRouteInfo route = GetActiveSharedInputRouteInfo();
        if (route != null)
        {
            builder.AppendLine("SharedInputRoute");
            builder.AppendLine($"  inputDeviceIndex: {route.InputDeviceIndex}");
            builder.AppendLine($"  inputDevice: {route.InputDeviceDisplayName}");
            builder.AppendLine($"  hostApi: {route.HostApiName}");
            builder.AppendLine($"  sampleRate: {route.SampleRate}");
            builder.AppendLine($"  inputChannels: {route.InputChannelCount}");
            builder.AppendLine($"  maxBlockFrames: {route.MaxBlockFrames}");
            builder.AppendLine($"  inputChannelMode: {route.InputChannelMode}");
        }

        return builder.ToString().TrimEnd();
    }
    public string LastExternalPedalRefreshSummary => ToneLabPedalRegistry.LastExternalRefreshSummary;
    public bool IsAdvancedRoutingBetaEnabled => advancedRoutingOptions != null && advancedRoutingOptions.betaEnabled;
    public bool IsSharedInputRouteActive => sharedInputRouteActive && usingPortAudioBackend;
    public Func<SharedInputRouteInfo, bool> SharedInputRouteStarting { get; set; }
    public Action SharedInputRouteStopped { get; set; }
    public Func<float[], int, int, int, string, bool> SharedInputBlockReceived { get; set; }
    public Action<float[], int, int, int, string> RawInputBlockReceived { get; set; }
    public SharedInputRouteInfo GetActiveSharedInputRouteInfo()
    {
        return IsSharedInputRouteActive ? activeSharedInputRoute?.Clone() : null;
    }
    public static string[] SharedMonitoringLatencyOptions => (string[])LatencyPresetLabels.Clone();
    public static string GetSharedMonitoringLatencyLabel(int bufferSize) => GetLatencyPresetLabel(bufferSize);
    public static int ParseSharedMonitoringLatencyBufferSize(string label)
    {
        if (string.Equals(label, "Driver", StringComparison.Ordinal))
            return PreferredDspBufferSize;
        if (string.Equals(label, LatencyPresetLabels[0], StringComparison.Ordinal))
            return UltraLowDspBufferSize;
        if (string.Equals(label, LatencyPresetLabels[2], StringComparison.Ordinal))
            return SafeDspBufferSize;
        return PreferredDspBufferSize;
    }

    public sealed class AdvancedRoutingOptions
    {
        public bool betaEnabled;
        public string inputChannelMode = SharedAudioInputChannelModes.Input1;
        public string backendMode = SharedAudioBackendModes.Auto;
        public bool allowFallback = true;
        public string preferredInputDeviceName = string.Empty;
        public string preferredOutputDeviceName = string.Empty;
        public int sampleRate;
        public int bufferSize = PreferredDspBufferSize;
        public bool splitInputOutputEnabled;
        public bool unifiedOutputEnabled;
        public bool unityRecorderCaptureEnabled;
    }

    public sealed class SharedInputRouteInfo
    {
        public int InputDeviceIndex;
        public string InputDeviceDisplayName = string.Empty;
        public string HostApiName = string.Empty;
        public int SampleRate;
        public int InputChannelCount;
        public int MaxBlockFrames;
        public string InputChannelMode = SharedAudioInputChannelModes.Input1;

        public SharedInputRouteInfo Clone()
        {
            return new SharedInputRouteInfo
            {
                InputDeviceIndex = InputDeviceIndex,
                InputDeviceDisplayName = InputDeviceDisplayName,
                HostApiName = HostApiName,
                SampleRate = SampleRate,
                InputChannelCount = InputChannelCount,
                MaxBlockFrames = MaxBlockFrames,
                InputChannelMode = InputChannelMode
            };
        }
    }

    private sealed class PortAudioRoutePlan
    {
        public ToneLabPortAudio.DeviceDescriptor InputDevice;
        public ToneLabPortAudio.DeviceDescriptor OutputDevice;
        public int SampleRate;
        public int BufferSize;
        public int Rank;
    }

    private void Awake()
    {
        monitorSource = GetComponent<AudioSource>();
        monitorSource.playOnAwake = false;
        monitorSource.loop = true;
        monitorSource.spatialBlend = 0f;
        monitorSource.dopplerLevel = 0f;
        monitorSource.reverbZoneMix = 0f;
        monitorSource.volume = 1f;
        monitorSource.pitch = 1f;
        monitorSource.bypassEffects = false;
        monitorSource.bypassListenerEffects = false;
        monitorSource.bypassReverbZones = true;
        monitorSource.ignoreListenerPause = true;
        monitorSource.clip = CreateMonitorDriverClip(PreferredSampleRate);
        portAudioStream = new ToneLabPortAudio.DuplexStream(ProcessPortAudioBlock);
        portAudioSplitStream = new ToneLabPortAudio.SplitStream(ProcessPortAudioBlock);
        EnsureSettingsLoaded();
        RefreshInputDevices();
    }

    public void SetMonitorVolumePercent(float percent)
    {
        monitorVolumePercent = Mathf.Clamp(percent, 0f, MaxMonitorVolumePercent);
    }

    public void SetAdvancedRoutingOptions(AdvancedRoutingOptions options)
    {
        advancedRoutingOptions = options ?? new AdvancedRoutingOptions();
        advancedRoutingOptions.inputChannelMode = SharedAudioInputChannelModes.Normalize(advancedRoutingOptions.inputChannelMode);
        advancedRoutingOptions.backendMode = SharedAudioBackendModes.Normalize(advancedRoutingOptions.backendMode);
        advancedRoutingOptions.bufferSize = ResolveMonitoringBufferSize(advancedRoutingOptions.bufferSize);
        advancedRoutingOptions.sampleRate = SharedAudioSampleRateOptions.Normalize(advancedRoutingOptions.sampleRate);
        advancedRoutingOptions.preferredInputDeviceName = NormalizeStoredDeviceLabel(advancedRoutingOptions.preferredInputDeviceName);
        advancedRoutingOptions.preferredOutputDeviceName = NormalizeStoredDeviceLabel(advancedRoutingOptions.preferredOutputDeviceName);
    }

    public IReadOnlyList<string> GetAdvancedInputDeviceChoices(string backendMode)
    {
        RefreshInputDevices();
        List<string> choices = new List<string> { "Automatic" };
        AppendAdvancedDeviceChoices(choices, portAudioAllDevices, input: true, backendMode);
        return choices;
    }

    public IReadOnlyList<string> GetAdvancedOutputDeviceChoices(string backendMode)
    {
        RefreshInputDevices();
        List<string> choices = new List<string> { "Automatic" };
        AppendAdvancedDeviceChoices(choices, portAudioAllDevices, input: false, backendMode);
        return choices;
    }

    private static string NormalizeStoredDeviceLabel(string value)
    {
        return SharedAudioSettingsUtility.NormalizeStoredDeviceName(value);
    }

    private static bool IsAutomaticDeviceChoice(string value)
    {
        return string.IsNullOrWhiteSpace(value) ||
               string.Equals(value.Trim(), "Automatic", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetNormalizedHostApiLabel(string hostApiName)
    {
        return SharedAudioBackendModes.NormalizeHostApiLabel(hostApiName);
    }

    private static int GetAdvancedHostPriority(string hostApiName)
    {
        return SharedAudioBackendModes.GetHostPriority(hostApiName);
    }

    private static bool MatchesBackendMode(string hostApiName, string backendMode)
    {
        string normalizedBackendMode = SharedAudioBackendModes.Normalize(backendMode);
        if (string.Equals(normalizedBackendMode, SharedAudioBackendModes.Auto, StringComparison.Ordinal))
            return true;

        return string.Equals(GetNormalizedHostApiLabel(hostApiName), normalizedBackendMode, StringComparison.Ordinal);
    }

    private static string BuildAdvancedDeviceChoiceLabel(ToneLabPortAudio.DeviceDescriptor descriptor)
    {
        if (descriptor == null)
            return string.Empty;

        string deviceName = string.IsNullOrWhiteSpace(descriptor.Name)
            ? NormalizePortAudioDeviceName(descriptor.DisplayName)
            : descriptor.Name.Trim();
        string hostLabel = GetNormalizedHostApiLabel(descriptor.HostApiName);
        return $"{deviceName} [{hostLabel}] (#{descriptor.Index})";
    }

    private static string BuildAdvancedDeviceMatchKey(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return string.Empty;

        string trimmed = NormalizeStoredDeviceLabel(label);
        int suffixIndex = trimmed.LastIndexOf(" (#", StringComparison.Ordinal);
        if (suffixIndex >= 0 && trimmed.EndsWith(")", StringComparison.Ordinal))
            trimmed = trimmed.Substring(0, suffixIndex);
        return trimmed.Trim().ToLowerInvariant();
    }

    private static string BuildAdvancedDeviceMatchKey(ToneLabPortAudio.DeviceDescriptor descriptor)
    {
        if (descriptor == null)
            return string.Empty;

        string deviceName = string.IsNullOrWhiteSpace(descriptor.Name)
            ? NormalizePortAudioDeviceName(descriptor.DisplayName)
            : descriptor.Name.Trim();
        return $"{deviceName} [{GetNormalizedHostApiLabel(descriptor.HostApiName)}]".Trim().ToLowerInvariant();
    }

    private static bool SameHostApi(ToneLabPortAudio.DeviceDescriptor left, ToneLabPortAudio.DeviceDescriptor right)
    {
        if (left == null || right == null)
            return false;

        return string.Equals(
            GetNormalizedHostApiLabel(left.HostApiName),
            GetNormalizedHostApiLabel(right.HostApiName),
            StringComparison.Ordinal);
    }

    private static int ResolveRequestedChannelCount(ToneLabPortAudio.DeviceDescriptor descriptor, bool input)
    {
        if (descriptor == null)
            return 1;

        int maxChannels = input ? descriptor.MaxInputChannels : descriptor.MaxOutputChannels;
        return Mathf.Clamp(maxChannels, 1, 2);
    }

    private GuitarBridgeServer ResolveUnifiedSongSourceOwner()
    {
        if (unifiedSongSourceOwner == null)
            unifiedSongSourceOwner = GetComponentInParent<GuitarBridgeServer>();
        return unifiedSongSourceOwner;
    }

    private void RefreshUnifiedSongSourceTaps(bool forceBufferReset)
    {
        if (!advancedRoutingOptions.betaEnabled || !advancedRoutingOptions.unifiedOutputEnabled)
        {
            StopUnifiedSongSourceTaps();
            return;
        }

        GuitarBridgeServer owner = ResolveUnifiedSongSourceOwner();
        List<AudioSource> desiredSources = owner != null ? owner.GetToneLabUnifiedPlaybackSources() : null;
        desiredSources ??= new List<AudioSource>();

        for (int i = unifiedSongSourceTaps.Count - 1; i >= 0; i--)
        {
            UnityToneLabAudioSourceTap tap = unifiedSongSourceTaps[i];
            AudioSource tappedSource = tap != null ? tap.Source : null;
            bool keep = tappedSource != null && desiredSources.Contains(tappedSource);
            if (keep)
                continue;

            if (tap != null)
                tap.SetCaptureState(false, false);
            unifiedSongSourceTaps.RemoveAt(i);
        }

        for (int i = 0; i < desiredSources.Count; i++)
        {
            AudioSource source = desiredSources[i];
            if (source == null)
                continue;

            UnityToneLabAudioSourceTap tap = unifiedSongSourceTaps.FirstOrDefault(candidate => candidate != null && candidate.Source == source);
            bool isNewTap = tap == null;
            if (tap == null)
            {
                tap = source.GetComponent<UnityToneLabAudioSourceTap>();
                if (tap == null)
                    tap = source.gameObject.AddComponent<UnityToneLabAudioSourceTap>();
                unifiedSongSourceTaps.Add(tap);
            }

            tap.SetCaptureState(true, suppress: true);
            tap.SetOutputGain(source.mute ? 0f : source.volume);
            if (forceBufferReset || isNewTap)
                tap.ResetCapturedAudio();
        }

        unifiedSongSourceTapSnapshot = unifiedSongSourceTaps
            .Where(tap => tap != null && tap.Source != null)
            .ToArray();
    }

    private void StopUnifiedSongSourceTaps()
    {
        for (int i = 0; i < unifiedSongSourceTaps.Count; i++)
        {
            UnityToneLabAudioSourceTap tap = unifiedSongSourceTaps[i];
            if (tap != null)
                tap.SetCaptureState(false, false);
        }

        unifiedSongSourceTaps.Clear();
        unifiedSongSourceTapSnapshot = Array.Empty<UnityToneLabAudioSourceTap>();
        nextUnifiedSongSourceRefreshTime = -1f;
    }

    private bool TryStartUnifiedSongSourceTaps(out string notice)
    {
        RefreshUnifiedSongSourceTaps(forceBufferReset: true);
        nextUnifiedSongSourceRefreshTime = Time.unscaledTime + 0.25f;
        if (unifiedSongSourceTapSnapshot.Length > 0)
        {
            notice = $"Unified source taps active ({unifiedSongSourceTapSnapshot.Length} source{(unifiedSongSourceTapSnapshot.Length == 1 ? string.Empty : "s")}).";
            return true;
        }

        notice = "Unified source taps armed; waiting for song audio sources.";
        return true;
    }

    private void AppendAdvancedDeviceChoices(List<string> choices, ToneLabPortAudio.DeviceDescriptor[] devices, bool input, string backendMode)
    {
        if (choices == null || devices == null || devices.Length == 0)
            return;

        IEnumerable<ToneLabPortAudio.DeviceDescriptor> filtered = devices
            .Where(device => device != null)
            .Where(device => input ? device.MaxInputChannels > 0 : device.MaxOutputChannels > 0)
            .Where(device => MatchesBackendMode(device.HostApiName, backendMode))
            .OrderBy(device => GetAdvancedHostPriority(device.HostApiName))
            .ThenBy(device => GetNormalizedHostApiLabel(device.HostApiName), StringComparer.OrdinalIgnoreCase)
            .ThenBy(device => device.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(device => device.Index);

        foreach (ToneLabPortAudio.DeviceDescriptor descriptor in filtered)
        {
            string label = BuildAdvancedDeviceChoiceLabel(descriptor);
            bool exists = false;
            for (int i = 0; i < choices.Count; i++)
            {
                if (string.Equals(choices[i], label, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
                choices.Add(label);
        }
    }

    private ToneLabPortAudio.DeviceDescriptor ResolveAdvancedDeviceSelection(string selectedLabel, IReadOnlyList<ToneLabPortAudio.DeviceDescriptor> devices, bool input)
    {
        if (IsAutomaticDeviceChoice(selectedLabel) || devices == null || devices.Count == 0)
            return null;

        for (int i = 0; i < devices.Count; i++)
        {
            ToneLabPortAudio.DeviceDescriptor descriptor = devices[i];
            if (descriptor == null)
                continue;

            string exactLabel = BuildAdvancedDeviceChoiceLabel(descriptor);
            if (string.Equals(exactLabel, selectedLabel, StringComparison.OrdinalIgnoreCase))
                return descriptor;
        }

        string selectionKey = BuildAdvancedDeviceMatchKey(selectedLabel);
        for (int i = 0; i < devices.Count; i++)
        {
            ToneLabPortAudio.DeviceDescriptor descriptor = devices[i];
            if (descriptor == null)
                continue;

            if (string.Equals(BuildAdvancedDeviceMatchKey(descriptor), selectionKey, StringComparison.Ordinal))
                return descriptor;
        }

        ToneLabPortAudio.DeviceDescriptor[] candidateArray = devices.ToArray();
        ToneLabPortAudio.DeviceDescriptor legacyMatch = ResolvePortAudioDevice(selectedLabel, candidateArray);
        if (legacyMatch != null && (input ? legacyMatch.MaxInputChannels > 0 : legacyMatch.MaxOutputChannels > 0))
            return legacyMatch;

        return null;
    }

    private static List<int> BuildSampleRateCandidates(int requestedSampleRate, ToneLabPortAudio.DeviceDescriptor inputDevice, ToneLabPortAudio.DeviceDescriptor outputDevice, bool allowFallback, int forcedSampleRate)
    {
        List<int> rates = new List<int>();

        void AddRate(int value)
        {
            if (value <= 0 || rates.Contains(value))
                return;
            rates.Add(value);
        }

        if (forcedSampleRate > 0)
        {
            AddRate(forcedSampleRate);
            return rates;
        }

        AddRate(requestedSampleRate);
        AddRate(ResolveSampleRate(inputDevice, outputDevice));
        if (allowFallback)
        {
            AddRate(48000);
            AddRate(44100);
        }

        if (rates.Count == 0)
            AddRate(PreferredSampleRate);

        return rates;
    }

    private static List<int> BuildBufferSizeCandidates(int requestedBufferSize, bool allowFallback, ToneLabPortAudio.DeviceDescriptor inputDevice, ToneLabPortAudio.DeviceDescriptor outputDevice)
    {
        List<int> buffers = new List<int>();
        bool asioRoute =
            string.Equals(GetNormalizedHostApiLabel(inputDevice?.HostApiName), SharedAudioBackendModes.Asio, StringComparison.Ordinal) ||
            string.Equals(GetNormalizedHostApiLabel(outputDevice?.HostApiName), SharedAudioBackendModes.Asio, StringComparison.Ordinal);

        void AddBuffer(int value)
        {
            int resolved = ResolveMonitoringBufferSize(value);
            if (!buffers.Contains(resolved))
                buffers.Add(resolved);
        }

        void AddRawBuffer(int value)
        {
            if (value < 0 || buffers.Contains(value))
                return;

            buffers.Add(value);
        }

        AddBuffer(requestedBufferSize);
        if (asioRoute)
            AddRawBuffer(0);
        if (allowFallback)
        {
            AddBuffer(PreferredDspBufferSize);
            AddBuffer(SafeDspBufferSize);
            AddBuffer(UltraLowDspBufferSize);
        }

        if (buffers.Count == 0)
            AddBuffer(PreferredDspBufferSize);

        return buffers;
    }

    private List<PortAudioRoutePlan> BuildAdvancedRoutePlans()
    {
        string backendMode = SharedAudioBackendModes.Normalize(advancedRoutingOptions.backendMode);
        bool allowFallback = advancedRoutingOptions.allowFallback;
        int forcedUnifiedSampleRate = (advancedRoutingOptions.unifiedOutputEnabled || advancedRoutingOptions.unityRecorderCaptureEnabled) ? GetUnityOutputSampleRate() : 0;

        List<ToneLabPortAudio.DeviceDescriptor> inputCandidates = portAudioAllDevices
            .Where(device => device != null && device.MaxInputChannels > 0 && MatchesBackendMode(device.HostApiName, backendMode))
            .OrderBy(device => GetAdvancedHostPriority(device.HostApiName))
            .ThenBy(device => device.Index)
            .ToList();
        List<ToneLabPortAudio.DeviceDescriptor> outputCandidates = portAudioAllDevices
            .Where(device => device != null && device.MaxOutputChannels > 0 && MatchesBackendMode(device.HostApiName, backendMode))
            .OrderBy(device => GetAdvancedHostPriority(device.HostApiName))
            .ThenBy(device => device.Index)
            .ToList();

        ToneLabPortAudio.DeviceDescriptor selectedInput = ResolveAdvancedDeviceSelection(advancedRoutingOptions.preferredInputDeviceName, inputCandidates, input: true)
            ?? ResolvePortAudioDevice(settings.input_device_name, inputCandidates.ToArray());
        ToneLabPortAudio.DeviceDescriptor selectedOutput = ResolveAdvancedDeviceSelection(advancedRoutingOptions.preferredOutputDeviceName, outputCandidates, input: false)
            ?? ResolvePortAudioDevice(settings.output_device_name, outputCandidates.ToArray());

        bool selectedInputExplicit = !IsAutomaticDeviceChoice(advancedRoutingOptions.preferredInputDeviceName) && selectedInput != null;
        bool selectedOutputExplicit = !IsAutomaticDeviceChoice(advancedRoutingOptions.preferredOutputDeviceName) && selectedOutput != null;
        List<(ToneLabPortAudio.DeviceDescriptor input, ToneLabPortAudio.DeviceDescriptor output, int rank)> rankedPairs = new List<(ToneLabPortAudio.DeviceDescriptor, ToneLabPortAudio.DeviceDescriptor, int)>();
        for (int i = 0; i < inputCandidates.Count; i++)
        {
            ToneLabPortAudio.DeviceDescriptor inputDevice = inputCandidates[i];
            for (int j = 0; j < outputCandidates.Count; j++)
            {
                ToneLabPortAudio.DeviceDescriptor outputDevice = outputCandidates[j];
                if (!SameHostApi(inputDevice, outputDevice))
                    continue;

                int rank = 0;
                if (selectedInput != null && inputDevice.Index != selectedInput.Index)
                    rank += 10;
                if (selectedOutput != null && outputDevice.Index != selectedOutput.Index)
                    rank += 10;
                rank += GetAdvancedHostPriority(inputDevice.HostApiName) * 100;
                rankedPairs.Add((inputDevice, outputDevice, rank));
            }
        }

        if (selectedInputExplicit)
            rankedPairs = rankedPairs.Where(pair => pair.input.Index == selectedInput.Index).ToList();
        if (selectedOutputExplicit)
            rankedPairs = rankedPairs.Where(pair => pair.output.Index == selectedOutput.Index).ToList();

        if (rankedPairs.Count == 0)
            return new List<PortAudioRoutePlan>();

        rankedPairs = rankedPairs
            .OrderBy(pair => pair.rank)
            .ThenBy(pair => pair.input.Index)
            .ThenBy(pair => pair.output.Index)
            .ToList();

        if (!allowFallback)
            rankedPairs = rankedPairs.Take(1).ToList();

        List<PortAudioRoutePlan> plans = new List<PortAudioRoutePlan>();
        for (int i = 0; i < rankedPairs.Count; i++)
        {
            (ToneLabPortAudio.DeviceDescriptor inputDevice, ToneLabPortAudio.DeviceDescriptor outputDevice, int rank) = rankedPairs[i];
            List<int> sampleRates = BuildSampleRateCandidates(advancedRoutingOptions.sampleRate, inputDevice, outputDevice, allowFallback, forcedUnifiedSampleRate);
            List<int> bufferSizes = BuildBufferSizeCandidates(advancedRoutingOptions.bufferSize, allowFallback, inputDevice, outputDevice);
            for (int sampleIndex = 0; sampleIndex < sampleRates.Count; sampleIndex++)
            {
                for (int bufferIndex = 0; bufferIndex < bufferSizes.Count; bufferIndex++)
                {
                    plans.Add(new PortAudioRoutePlan
                    {
                        InputDevice = inputDevice,
                        OutputDevice = outputDevice,
                        SampleRate = sampleRates[sampleIndex],
                        BufferSize = bufferSizes[bufferIndex],
                        Rank = rank + (sampleIndex * 10) + bufferIndex
                    });
                }
            }
        }

        return plans
            .OrderBy(plan => plan.Rank)
            .ThenBy(plan => plan.InputDevice.Index)
            .ThenBy(plan => plan.OutputDevice.Index)
            .ToList();
    }

    private List<PortAudioRoutePlan> BuildAdvancedSplitRoutePlans()
    {
        string backendMode = SharedAudioBackendModes.Normalize(advancedRoutingOptions.backendMode);
        bool allowFallback = advancedRoutingOptions.allowFallback;
        int forcedUnifiedSampleRate = (advancedRoutingOptions.unifiedOutputEnabled || advancedRoutingOptions.unityRecorderCaptureEnabled) ? GetUnityOutputSampleRate() : 0;

        List<ToneLabPortAudio.DeviceDescriptor> inputCandidates = portAudioAllDevices
            .Where(device => device != null && device.MaxInputChannels > 0 && MatchesBackendMode(device.HostApiName, backendMode))
            .OrderBy(device => GetAdvancedHostPriority(device.HostApiName))
            .ThenBy(device => device.Index)
            .ToList();
        List<ToneLabPortAudio.DeviceDescriptor> outputCandidates = portAudioAllDevices
            .Where(device => device != null && device.MaxOutputChannels > 0 && MatchesBackendMode(device.HostApiName, backendMode))
            .OrderBy(device => GetAdvancedHostPriority(device.HostApiName))
            .ThenBy(device => device.Index)
            .ToList();

        ToneLabPortAudio.DeviceDescriptor selectedInput = ResolveAdvancedDeviceSelection(advancedRoutingOptions.preferredInputDeviceName, inputCandidates, input: true)
            ?? ResolvePortAudioDevice(settings.input_device_name, inputCandidates.ToArray());
        ToneLabPortAudio.DeviceDescriptor selectedOutput = ResolveAdvancedDeviceSelection(advancedRoutingOptions.preferredOutputDeviceName, outputCandidates, input: false)
            ?? ResolvePortAudioDevice(settings.output_device_name, outputCandidates.ToArray());

        bool selectedInputExplicit = !IsAutomaticDeviceChoice(advancedRoutingOptions.preferredInputDeviceName) && selectedInput != null;
        bool selectedOutputExplicit = !IsAutomaticDeviceChoice(advancedRoutingOptions.preferredOutputDeviceName) && selectedOutput != null;
        List<(ToneLabPortAudio.DeviceDescriptor input, ToneLabPortAudio.DeviceDescriptor output, int rank)> rankedPairs = new List<(ToneLabPortAudio.DeviceDescriptor, ToneLabPortAudio.DeviceDescriptor, int)>();
        for (int i = 0; i < inputCandidates.Count; i++)
        {
            ToneLabPortAudio.DeviceDescriptor inputDevice = inputCandidates[i];
            for (int j = 0; j < outputCandidates.Count; j++)
            {
                ToneLabPortAudio.DeviceDescriptor outputDevice = outputCandidates[j];
                if (inputDevice.Index == outputDevice.Index)
                    continue;

                int rank = 0;
                if (selectedInput != null && inputDevice.Index != selectedInput.Index)
                    rank += 10;
                if (selectedOutput != null && outputDevice.Index != selectedOutput.Index)
                    rank += 10;
                rank += GetAdvancedHostPriority(inputDevice.HostApiName) * 100;
                rank += GetAdvancedHostPriority(outputDevice.HostApiName) * 100;
                rankedPairs.Add((inputDevice, outputDevice, rank));
            }
        }

        if (selectedInputExplicit)
            rankedPairs = rankedPairs.Where(pair => pair.input.Index == selectedInput.Index).ToList();
        if (selectedOutputExplicit)
            rankedPairs = rankedPairs.Where(pair => pair.output.Index == selectedOutput.Index).ToList();

        if (rankedPairs.Count == 0)
            return new List<PortAudioRoutePlan>();

        rankedPairs = rankedPairs
            .OrderBy(pair => pair.rank)
            .ThenBy(pair => pair.input.Index)
            .ThenBy(pair => pair.output.Index)
            .ToList();

        if (!allowFallback)
            rankedPairs = rankedPairs.Take(1).ToList();

        List<PortAudioRoutePlan> plans = new List<PortAudioRoutePlan>();
        for (int i = 0; i < rankedPairs.Count; i++)
        {
            (ToneLabPortAudio.DeviceDescriptor inputDevice, ToneLabPortAudio.DeviceDescriptor outputDevice, int rank) = rankedPairs[i];
            List<int> sampleRates = BuildSampleRateCandidates(advancedRoutingOptions.sampleRate, inputDevice, outputDevice, allowFallback, forcedUnifiedSampleRate);
            List<int> bufferSizes = BuildBufferSizeCandidates(advancedRoutingOptions.bufferSize, allowFallback, inputDevice, outputDevice);
            for (int sampleIndex = 0; sampleIndex < sampleRates.Count; sampleIndex++)
            {
                for (int bufferIndex = 0; bufferIndex < bufferSizes.Count; bufferIndex++)
                {
                    plans.Add(new PortAudioRoutePlan
                    {
                        InputDevice = inputDevice,
                        OutputDevice = outputDevice,
                        SampleRate = sampleRates[sampleIndex],
                        BufferSize = bufferSizes[bufferIndex],
                        Rank = rank + (sampleIndex * 10) + bufferIndex
                    });
                }
            }
        }

        return plans
            .OrderBy(plan => plan.Rank)
            .ThenBy(plan => plan.InputDevice.Index)
            .ThenBy(plan => plan.OutputDevice.Index)
            .ToList();
    }

    private List<PortAudioRoutePlan> BuildLegacyRoutePlans()
    {
        List<ToneLabPortAudio.DeviceDescriptor> inputCandidates = portAudioInputDevices?
            .Where(device => device != null && device.MaxInputChannels > 0)
            .ToList() ?? new List<ToneLabPortAudio.DeviceDescriptor>();
        List<ToneLabPortAudio.DeviceDescriptor> outputCandidates = portAudioOutputDevices?
            .Where(device => device != null && device.MaxOutputChannels > 0)
            .ToList() ?? new List<ToneLabPortAudio.DeviceDescriptor>();

        if (inputCandidates.Count == 0 || outputCandidates.Count == 0)
            return new List<PortAudioRoutePlan>();

        ToneLabPortAudio.DeviceDescriptor selectedInput = ResolvePortAudioDevice(settings?.input_device_name, inputCandidates.ToArray());
        ToneLabPortAudio.DeviceDescriptor selectedOutput = ResolvePortAudioDevice(settings?.output_device_name, outputCandidates.ToArray());

        List<(ToneLabPortAudio.DeviceDescriptor input, ToneLabPortAudio.DeviceDescriptor output, int rank)> rankedPairs =
            new List<(ToneLabPortAudio.DeviceDescriptor, ToneLabPortAudio.DeviceDescriptor, int)>();
        for (int inputIndex = 0; inputIndex < inputCandidates.Count; inputIndex++)
        {
            ToneLabPortAudio.DeviceDescriptor inputDevice = inputCandidates[inputIndex];
            for (int outputIndex = 0; outputIndex < outputCandidates.Count; outputIndex++)
            {
                ToneLabPortAudio.DeviceDescriptor outputDevice = outputCandidates[outputIndex];
                if (!SameHostApi(inputDevice, outputDevice))
                    continue;

                int rank = GetAdvancedHostPriority(inputDevice.HostApiName) * 100;
                if (selectedInput != null && inputDevice.Index != selectedInput.Index)
                    rank += 10;
                if (selectedOutput != null && outputDevice.Index != selectedOutput.Index)
                    rank += 10;
                rank += inputIndex + outputIndex;
                rankedPairs.Add((inputDevice, outputDevice, rank));
            }
        }

        List<PortAudioRoutePlan> plans = new List<PortAudioRoutePlan>();
        foreach ((ToneLabPortAudio.DeviceDescriptor inputDevice, ToneLabPortAudio.DeviceDescriptor outputDevice, int rank) in rankedPairs.OrderBy(pair => pair.rank))
        {
            List<int> sampleRates = BuildSampleRateCandidates(0, inputDevice, outputDevice, allowFallback: true, forcedSampleRate: 0);
            List<int> bufferSizes = BuildBufferSizeCandidates(settings != null ? settings.monitoring_buffer_size : PreferredDspBufferSize, allowFallback: true, inputDevice, outputDevice);
            for (int sampleIndex = 0; sampleIndex < sampleRates.Count; sampleIndex++)
            {
                for (int bufferIndex = 0; bufferIndex < bufferSizes.Count; bufferIndex++)
                {
                    plans.Add(new PortAudioRoutePlan
                    {
                        InputDevice = inputDevice,
                        OutputDevice = outputDevice,
                        SampleRate = sampleRates[sampleIndex],
                        BufferSize = bufferSizes[bufferIndex],
                        Rank = rank + (sampleIndex * 10) + bufferIndex
                    });
                }
            }
        }

        return plans
            .OrderBy(plan => plan.Rank)
            .ThenBy(plan => plan.InputDevice.Index)
            .ThenBy(plan => plan.OutputDevice.Index)
            .ToList();
    }

    private static string FormatRoutePlanPreview(IReadOnlyList<PortAudioRoutePlan> plans, int maxPlans = 8)
    {
        if (plans == null || plans.Count == 0)
            return "(none)";

        int count = Mathf.Min(Mathf.Max(1, maxPlans), plans.Count);
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            PortAudioRoutePlan plan = plans[i];
            if (plan == null)
                continue;

            if (builder.Length > 0)
                builder.Append(" || ");
            builder.Append("#");
            builder.Append(i + 1);
            builder.Append(": ");
            builder.Append(BuildAdvancedDeviceChoiceLabel(plan.InputDevice));
            builder.Append(" -> ");
            builder.Append(BuildAdvancedDeviceChoiceLabel(plan.OutputDevice));
            builder.Append(" @ ");
            builder.Append(plan.SampleRate);
            builder.Append(" Hz / ");
            builder.Append(FormatActiveBufferLabel(plan.BufferSize));
            builder.Append(" rank=");
            builder.Append(plan.Rank);
        }

        if (plans.Count > count)
        {
            builder.Append(" || +");
            builder.Append(plans.Count - count);
            builder.Append(" more");
        }

        return builder.ToString();
    }

    private void Update()
    {
        if (settingsDirty && Time.unscaledTime >= nextSettingsSaveTime)
            FlushSettingsToDisk();

        if (usingPortAudioBackend)
        {
            if (sharedInputSubmitFailurePending)
                HandleSharedInputSubmitFailureOnMainThread();

            if (advancedRoutingOptions.betaEnabled && advancedRoutingOptions.unifiedOutputEnabled)
            {
                if (Time.unscaledTime >= nextUnifiedSongSourceRefreshTime)
                {
                    RefreshUnifiedSongSourceTaps(forceBufferReset: false);
                    nextUnifiedSongSourceRefreshTime = Time.unscaledTime + 0.25f;
                }

                for (int i = 0; i < unifiedSongSourceTaps.Count; i++)
                {
                    UnityToneLabAudioSourceTap tap = unifiedSongSourceTaps[i];
                    AudioSource source = tap != null ? tap.Source : null;
                    if (tap == null || source == null)
                        continue;

                    tap.SetOutputGain(source.mute ? 0f : source.volume);
                }
            }

            if (unityOutputCaptureActive)
                PumpUnityOutputCapture();
            LogLiveAudioDiagnosticsIfDue();
            return;
        }

        if (monitoring && pendingMicrophoneClip != null && !string.IsNullOrWhiteSpace(pendingDeviceName))
        {
            int liveMicPosition = Microphone.GetPosition(pendingDeviceName);
            if (liveMicPosition >= 0)
                PumpRecordedMicrophoneSamples(liveMicPosition);
        }

        if (!awaitingMicrophoneStart)
        {
            LogLiveAudioDiagnosticsIfDue();
            return;
        }

        if (string.IsNullOrWhiteSpace(pendingDeviceName))
        {
            awaitingMicrophoneStart = false;
            statusMessage = "No microphone input selected.";
            return;
        }

        int micPosition = Microphone.GetPosition(pendingDeviceName);
        if (micPosition > 0 && pendingMicrophoneClip != null)
        {
            int requiredLeadSamples = ComputeRequiredLeadSamples(pendingMicrophoneClip.frequency);
            if (micPosition >= requiredLeadSamples)
            {
                PrepareMicrophoneCaptureBuffers(pendingMicrophoneClip, requiredLeadSamples);
                PumpRecordedMicrophoneSamples(micPosition);
                BeginMicrophonePlayback(requiredLeadSamples);
                return;
            }

            float bufferedMs = 1000f * micPosition / Mathf.Max(1f, pendingMicrophoneClip.frequency);
            float targetMs = 1000f * requiredLeadSamples / Mathf.Max(1f, pendingMicrophoneClip.frequency);
            statusMessage = $"Priming live monitoring on {pendingDeviceName}... {bufferedMs:F1}/{targetMs:F1} ms buffered";
        }

        if (Time.unscaledTime >= microphoneStartupDeadline)
        {
            awaitingMicrophoneStart = false;
            if (!string.IsNullOrWhiteSpace(pendingDeviceName))
                Microphone.End(pendingDeviceName);
            pendingMicrophoneClip = null;
            statusMessage = "Unable to start microphone stream.";
        }
    }

    private void OnDisable()
    {
        StopMonitoringInternal(restoreAudioConfiguration: true, notifySharedInputStopped: false);
        FlushSettingsToDisk();
    }

    private void OnDestroy()
    {
        StopMonitoringInternal(restoreAudioConfiguration: true, notifySharedInputStopped: false);
        FlushSettingsToDisk();
        portAudioStream?.Dispose();
        portAudioSplitStream?.Dispose();
        ToneLabPortAudio.Shutdown();
    }

    public void OpenForSession()
    {
        EnsureSettingsLoaded();
        RestoreSelectedPresetWorkingRig();
        RefreshInputDevices(forcePortAudioRescan: false);
        if (!monitoring && !awaitingMicrophoneStart)
            TryStartMonitoring();
    }

    public void StartBackgroundMonitoring()
    {
        EnsureSettingsLoaded();
        RestoreSelectedPresetWorkingRig();
        RefreshInputDevices(forcePortAudioRescan: false);
        if (!monitoring && !awaitingMicrophoneStart)
            TryStartMonitoring();
    }

    public void CloseForSession()
    {
        StopMonitoring();
        FlushSettingsToDisk();
    }

    public void RestoreSelectedPresetWorkingRig()
    {
        EnsureSettingsLoaded();
        playbackPresetOverrideActive = false;
        playbackPresetOverride = null;
        playbackPresetOverrideId = string.Empty;
        RestoreWorkingRigFromSelectedPreset();
        RebuildCompiledPedalChain();
    }

    public void RefreshInputDevices(bool forcePortAudioRescan = false)
    {
        EnsureSettingsLoaded();
        bool restartMonitoringAfterRescan = forcePortAudioRescan && (monitoring || awaitingMicrophoneStart || usingPortAudioBackend);
        if (forcePortAudioRescan)
        {
            if (restartMonitoringAfterRescan)
                StopMonitoringInternal(restoreAudioConfiguration: true, notifySharedInputStopped: false);

            ToneLabPortAudio.Shutdown();
        }

        if (ToneLabPortAudio.TryEnsureInitialized(out string portAudioError))
        {
            ToneLabPortAudio.DeviceDescriptor[] allDevices = ToneLabPortAudio.EnumerateDevices().ToArray();
            portAudioAllDevices = allDevices;
            portAudioInputDevices = ToneLabPortAudio.GetPreferredInputDevices(allDevices).ToArray();
            portAudioOutputDevices = ToneLabPortAudio.GetPreferredOutputDevices(allDevices).ToArray();
            inputDevices = portAudioInputDevices.Select(device => device.DisplayName).ToArray();
            outputDevices = portAudioOutputDevices.Select(device => device.DisplayName).ToArray();
            if (forcePortAudioRescan)
                LogAudioRouteEvent("PortAudio device rescan", BuildDeviceCatalogLog(includeAllPortAudioInputs: true));

            if (inputDevices.Length == 0)
            {
                settings.input_device_name = string.Empty;
                settings.output_device_name = string.Empty;
                inputRouteLabel = "No input devices";
                outputRouteLabel = "No output devices";
                statusMessage = "No PortAudio input devices found.";
                MarkSettingsDirty();
                return;
            }

            string resolvedInput = ResolveSavedDeviceName(settings.input_device_name, portAudioInputDevices);
            if (string.IsNullOrWhiteSpace(resolvedInput))
                resolvedInput = inputDevices[0];
            if (!string.Equals(settings.input_device_name, resolvedInput, StringComparison.Ordinal))
            {
                settings.input_device_name = resolvedInput;
                MarkSettingsDirty();
            }
            inputRouteLabel = string.IsNullOrWhiteSpace(resolvedInput) ? "Automatic Input" : resolvedInput;

            string resolvedOutput = ResolveSavedDeviceName(settings.output_device_name, portAudioOutputDevices);
            if (string.IsNullOrWhiteSpace(resolvedOutput))
                resolvedOutput = ToneLabPortAudio.GetDefaultOutputDisplayName(portAudioOutputDevices);
            if (string.IsNullOrWhiteSpace(resolvedOutput) && portAudioOutputDevices.Length > 0)
                resolvedOutput = portAudioOutputDevices[0].DisplayName;
            if (!string.Equals(settings.output_device_name, resolvedOutput, StringComparison.Ordinal))
            {
                settings.output_device_name = resolvedOutput;
                MarkSettingsDirty();
            }

            outputRouteLabel = string.IsNullOrWhiteSpace(resolvedOutput) ? "System Default Output" : resolvedOutput;
            if (restartMonitoringAfterRescan)
                TryStartMonitoring();
            return;
        }

        portAudioInputDevices = Array.Empty<ToneLabPortAudio.DeviceDescriptor>();
        portAudioOutputDevices = Array.Empty<ToneLabPortAudio.DeviceDescriptor>();
        portAudioAllDevices = Array.Empty<ToneLabPortAudio.DeviceDescriptor>();
        inputDevices = Microphone.devices ?? Array.Empty<string>();
        outputDevices = new[] { "System Default" };
        if (inputDevices.Length == 0)
        {
            settings.input_device_name = string.Empty;
            settings.output_device_name = "System Default";
            inputRouteLabel = "No input devices";
            outputRouteLabel = "System Default Output";
            statusMessage = string.IsNullOrWhiteSpace(portAudioError) ? "No microphone inputs found." : portAudioError;
            MarkSettingsDirty();
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.input_device_name) || Array.IndexOf(inputDevices, settings.input_device_name) < 0)
        {
            settings.input_device_name = inputDevices[0];
            MarkSettingsDirty();
        }

        if (string.IsNullOrWhiteSpace(settings.output_device_name))
        {
            settings.output_device_name = "System Default";
            MarkSettingsDirty();
        }

        inputRouteLabel = string.IsNullOrWhiteSpace(settings.input_device_name) ? "Automatic Input" : settings.input_device_name;
        outputRouteLabel = "System Default Output";
        if (restartMonitoringAfterRescan)
            TryStartMonitoring();
    }

    public void RefreshExternalPedalLibrary(bool force = false)
    {
        ExternalContentBootstrap.EnsureToneLabRuntimeContentReady();
        bool changed = ToneLabPedalRegistry.RefreshExternalDescriptors(force);
        if (settingsLoaded && (changed || force))
            RebuildCompiledPedalChain();
    }

    public void UpdateSettings(Action<ToneLabSettings> mutate, bool restartMonitoring, bool rebuildPedalChain = true)
    {
        EnsureSettingsLoaded();
        mutate?.Invoke(settings);
        ClampSettings(settings);
        if (rebuildPedalChain)
            RebuildCompiledPedalChain();
        MarkSettingsDirty();

        if (restartMonitoring && (monitoring || awaitingMicrophoneStart))
            RestartMonitoring();
    }

    public void SetPedalEnabled(string pedalInstanceId, bool enabled)
    {
        UpdateSettings(toneSettings =>
        {
            ToneLabPedalSlot slot = FindPedalSlot(toneSettings, pedalInstanceId);
            if (slot != null)
                slot.enabled = enabled;
            SyncLegacySettingsFromChain(toneSettings);
        }, restartMonitoring: false);
    }

    public string AddPedalToChain(ToneLabPedalType pedalType)
    {
        return AddPedalToChain(pedalType, int.MaxValue);
    }

    public string AddPedalToChain(ToneLabPedalType pedalType, int insertionIndex)
    {
        string createdInstanceId = string.Empty;
        UpdateSettings(toneSettings =>
        {
            EnsurePedalChain(toneSettings);

            IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(pedalType);
            ToneLabPedalSlot slot = new ToneLabPedalSlot
            {
                pedal_instance_id = CreatePedalInstanceId(),
                pedal_type = pedalType,
                descriptor_id = descriptor.DescriptorId,
                enabled = true,
                settings_json = descriptor.SerializeSettingsObject(descriptor.CreateDefaultSettingsObject())
            };
            int clampedInsertionIndex = Mathf.Clamp(insertionIndex, 0, toneSettings.pedal_chain.Count);
            toneSettings.pedal_chain.Insert(clampedInsertionIndex, slot);
            createdInstanceId = slot.pedal_instance_id;
            SyncLegacySettingsFromChain(toneSettings);
        }, restartMonitoring: false);

        return createdInstanceId;
    }

    public string AddPedalToChain(string descriptorId)
    {
        return AddPedalToChain(descriptorId, int.MaxValue);
    }

    public string AddPedalToChain(string descriptorId, int insertionIndex)
    {
        if (string.IsNullOrWhiteSpace(descriptorId))
            return string.Empty;

        string createdInstanceId = string.Empty;
        UpdateSettings(toneSettings =>
        {
            EnsurePedalChain(toneSettings);

            IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(descriptorId);
            ToneLabPedalSlot slot = new ToneLabPedalSlot
            {
                pedal_instance_id = CreatePedalInstanceId(),
                pedal_type = descriptor.PedalType,
                descriptor_id = descriptor.DescriptorId,
                enabled = true,
                settings_json = descriptor.SerializeSettingsObject(descriptor.CreateDefaultSettingsObject())
            };
            int clampedInsertionIndex = Mathf.Clamp(insertionIndex, 0, toneSettings.pedal_chain.Count);
            toneSettings.pedal_chain.Insert(clampedInsertionIndex, slot);
            createdInstanceId = slot.pedal_instance_id;
            SyncLegacySettingsFromChain(toneSettings);
        }, restartMonitoring: false);

        return createdInstanceId;
    }

    public void RemovePedalFromChain(string pedalInstanceId)
    {
        UpdateSettings(toneSettings =>
        {
            EnsurePedalChain(toneSettings);
            toneSettings.pedal_chain.RemoveAll(slot => slot != null && string.Equals(slot.pedal_instance_id, pedalInstanceId, StringComparison.Ordinal));
            SyncLegacySettingsFromChain(toneSettings);
        }, restartMonitoring: false);
    }

    public void SetPedalChainOrder(IReadOnlyList<string> orderedPedalInstanceIds)
    {
        UpdateSettings(toneSettings => ApplyPedalOrder(toneSettings, orderedPedalInstanceIds), restartMonitoring: false);
    }

    public void UpdatePedalParameter(string pedalInstanceId, string parameterId, float value)
    {
        if (string.IsNullOrWhiteSpace(pedalInstanceId) || string.IsNullOrWhiteSpace(parameterId))
            return;

        EnsureSettingsLoaded();
        ToneLabPedalSlot existingSlot = FindPedalSlot(settings, pedalInstanceId);
        if (existingSlot != null &&
            (existingSlot.pedal_type == ToneLabPedalType.Lv2Plugin || existingSlot.pedal_type == ToneLabPedalType.NamModel))
        {
            IToneLabPedalDescriptor externalDescriptor = ToneLabPedalRegistry.GetDescriptor(existingSlot);
            ToneLabPedalParameterDefinition externalParameter = externalDescriptor.Parameters.FirstOrDefault(candidate =>
                string.Equals(candidate.ParameterId, parameterId, StringComparison.Ordinal));
            if (externalParameter == null)
                return;

            object externalSettingsObject = externalDescriptor.DeserializeSettingsObject(existingSlot.settings_json);
            externalParameter.SetValue(externalSettingsObject, value);
            existingSlot.settings_json = externalDescriptor.SerializeSettingsObject(externalSettingsObject);
            ApplyCompiledPedalSettings(existingSlot, externalDescriptor, externalSettingsObject);
            MarkSettingsDirty();
            return;
        }

        UpdateSettings(toneSettings =>
        {
            EnsurePedalChain(toneSettings);
            ToneLabPedalSlot slot = FindPedalSlot(toneSettings, pedalInstanceId);
            if (slot == null)
                return;

            IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(slot);
            ToneLabPedalParameterDefinition parameterDefinition = descriptor.Parameters.FirstOrDefault(candidate =>
                string.Equals(candidate.ParameterId, parameterId, StringComparison.Ordinal));
            if (parameterDefinition == null)
                return;

            object settingsObject = descriptor.DeserializeSettingsObject(slot.settings_json);
            parameterDefinition.SetValue(settingsObject, value);
            slot.settings_json = descriptor.SerializeSettingsObject(settingsObject);
            SyncLegacySettingsFromChain(toneSettings);
        }, restartMonitoring: false);
    }

    public void SelectPreset(string presetId)
    {
        UpdateSettings(toneSettings =>
        {
            EnsurePresetLibrary(toneSettings);
            ToneLabPreset preset = FindPreset(toneSettings, presetId) ?? GetDefaultPreset(toneSettings);
            if (preset == null)
                return;

            ApplyPresetToSettings(toneSettings, preset);
        }, restartMonitoring: false);
    }

    public bool SetPlaybackPresetOverride(string presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
            return false;

        EnsureSettingsLoaded();
        if (settings == null)
            return false;

        EnsurePresetLibrary(settings);
        ToneLabPreset preset = FindPreset(settings, presetId);
        if (preset == null)
            return false;

        if (playbackPresetOverrideActive &&
            string.Equals(playbackPresetOverrideId, preset.preset_id, StringComparison.Ordinal) &&
            playbackPresetOverride != null)
        {
            return true;
        }

        playbackPresetOverride = ClonePreset(preset);
        playbackPresetOverrideId = playbackPresetOverride?.preset_id ?? string.Empty;
        playbackPresetOverrideActive = true;
        RebuildCompiledPedalChain();
        return true;
    }

    public bool SetPlaybackPresetOverride(ToneLabPreset preset)
    {
        if (preset == null || preset.pedal_chain == null || preset.pedal_chain.Count == 0)
            return false;

        string presetId = preset.preset_id ?? string.Empty;
        if (playbackPresetOverrideActive &&
            string.Equals(playbackPresetOverrideId, presetId, StringComparison.Ordinal) &&
            playbackPresetOverride != null)
        {
            return true;
        }

        playbackPresetOverride = ClonePreset(preset);
        playbackPresetOverrideId = playbackPresetOverride?.preset_id ?? string.Empty;
        playbackPresetOverrideActive = true;
        RebuildCompiledPedalChain();
        return true;
    }

    public void ClearPlaybackPresetOverride()
    {
        if (!playbackPresetOverrideActive && playbackPresetOverride == null && string.IsNullOrWhiteSpace(playbackPresetOverrideId))
            return;

        playbackPresetOverrideActive = false;
        playbackPresetOverride = null;
        playbackPresetOverrideId = string.Empty;
        RebuildCompiledPedalChain();
    }

    public string CreatePresetFromCurrent(string presetName)
    {
        string createdPresetId = string.Empty;
        UpdateSettings(toneSettings =>
        {
            EnsurePresetLibrary(toneSettings);
            string resolvedName = MakeUniquePresetName(toneSettings, presetName);
            ToneLabPreset preset = CaptureCurrentPreset(toneSettings, resolvedName, CreatePresetId());
            toneSettings.presets.Add(preset);
            toneSettings.selected_preset_id = preset.preset_id;
            createdPresetId = preset.preset_id;
        }, restartMonitoring: false);
        SavePresetLibraryToDisk(settings?.presets);

        return createdPresetId;
    }

    public string SaveCurrentAsNewPreset(string presetName)
    {
        return CreatePresetFromCurrent(presetName);
    }

    public string CreatePresetCopy(string sourcePresetId, string presetName)
    {
        if (string.IsNullOrWhiteSpace(sourcePresetId))
            return string.Empty;

        string createdPresetId = string.Empty;
        bool created = false;
        UpdateSettings(toneSettings =>
        {
            EnsurePresetLibrary(toneSettings);
            ToneLabPreset sourcePreset = FindPreset(toneSettings, sourcePresetId);
            if (sourcePreset == null)
                return;

            ToneLabPreset preset = ClonePreset(sourcePreset);
            preset.preset_id = CreatePresetId();
            preset.preset_name = MakeUniquePresetName(toneSettings, presetName);
            toneSettings.presets.Add(preset);
            createdPresetId = preset.preset_id;
            created = true;
        }, restartMonitoring: false, rebuildPedalChain: false);

        if (created)
            SavePresetLibraryToDisk(settings?.presets);

        return createdPresetId;
    }

    public string CreatePresetCopy(ToneLabPreset sourcePreset, string presetName)
    {
        if (sourcePreset == null || sourcePreset.pedal_chain == null || sourcePreset.pedal_chain.Count == 0)
            return string.Empty;

        string createdPresetId = string.Empty;
        bool created = false;
        UpdateSettings(toneSettings =>
        {
            EnsurePresetLibrary(toneSettings);
            ToneLabPreset preset = ClonePreset(sourcePreset);
            preset.preset_id = CreatePresetId();
            preset.preset_name = MakeUniquePresetName(toneSettings, presetName);
            toneSettings.presets.Add(preset);
            createdPresetId = preset.preset_id;
            created = true;
        }, restartMonitoring: false, rebuildPedalChain: false);

        if (created)
            SavePresetLibraryToDisk(settings?.presets);

        return createdPresetId;
    }

    public void SaveCurrentToPreset(string presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
            return;

        UpdateSettings(toneSettings =>
        {
            EnsurePresetLibrary(toneSettings);
            ToneLabPreset preset = FindPreset(toneSettings, presetId);
            if (preset == null)
                return;

            preset.input_gain_db = toneSettings.input_gain_db;
            preset.output_gain_db = toneSettings.output_gain_db;
            preset.pedal_chain = ClonePedalChain(toneSettings.pedal_chain);
            toneSettings.selected_preset_id = preset.preset_id;
        }, restartMonitoring: false);
        SavePresetLibraryToDisk(settings?.presets);
    }

    public bool DeletePreset(string presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
            return false;

        bool deleted = false;
        UpdateSettings(toneSettings =>
        {
            EnsurePresetLibrary(toneSettings);
            if (toneSettings.presets == null)
                return;

            deleted = toneSettings.presets.RemoveAll(preset =>
                preset != null && string.Equals(preset.preset_id, presetId, StringComparison.Ordinal)) > 0;
            if (!deleted)
                return;

            ToneLabPreset selectedPreset = FindPreset(toneSettings, toneSettings.selected_preset_id);
            if (selectedPreset == null)
            {
                ToneLabPreset fallbackPreset = GetDefaultPreset(toneSettings);
                if (fallbackPreset != null)
                    ApplyPresetToSettings(toneSettings, fallbackPreset);
                else
                    toneSettings.selected_preset_id = string.Empty;
            }
        }, restartMonitoring: false);

        if (deleted)
            SavePresetLibraryToDisk(settings?.presets);

        return deleted;
    }

    public void ResetAllToFactoryDefaults()
    {
        UpdateSettings(toneSettings =>
        {
            if (toneSettings == null)
                return;

            string inputDeviceName = toneSettings.input_device_name ?? string.Empty;
            string outputDeviceName = toneSettings.output_device_name ?? string.Empty;
            int monitoringBufferSize = ResolveMonitoringBufferSize(toneSettings.monitoring_buffer_size);

            toneSettings.input_device_name = inputDeviceName;
            toneSettings.output_device_name = outputDeviceName;
            toneSettings.monitoring_buffer_size = monitoringBufferSize;
            toneSettings.global_input_trim_db = DefaultGlobalInputTrimDb;
            toneSettings.global_output_gain_db = DefaultGlobalOutputGainDb;
            toneSettings.presets = CreateDefaultPresets();

            ToneLabPreset defaultPreset = GetDefaultPreset(toneSettings);
            if (defaultPreset != null)
            {
                ApplyPresetToSettings(toneSettings, defaultPreset);
            }
            else
            {
                toneSettings.selected_preset_id = string.Empty;
                toneSettings.pedal_chain = new List<ToneLabPedalSlot>();
                toneSettings.global_input_trim_db = DefaultGlobalInputTrimDb;
                toneSettings.global_output_gain_db = DefaultGlobalOutputGainDb;
                toneSettings.input_gain_db = DefaultRigInputGainDb;
                toneSettings.output_gain_db = DefaultRigOutputGainDb;
                EnsurePedalChain(toneSettings);
            }

            SyncLegacySettingsFromChain(toneSettings);
        }, restartMonitoring: false);

        SavePresetLibraryToDisk(settings?.presets);
    }

    public bool TryStartMonitoring()
    {
        EnsureSettingsLoaded();
        RefreshInputDevices();
        lastRoutingDiagnostics = string.Empty;
        lastRoutingAttemptSummary = string.Empty;
        LogAudioRouteEvent("Start requested", BuildDeviceCatalogLog(includeAllPortAudioInputs: true, includeAllPortAudioOutputs: true));

        if (advancedRoutingOptions != null && advancedRoutingOptions.betaEnabled)
            return TryStartMonitoringAdvancedPath();

        return TryStartMonitoringLegacyPath();
    }

    public void RestartMonitoring()
    {
        if (monitoring || awaitingMicrophoneStart)
            TryStartMonitoring();
    }

    public void StopMonitoring()
    {
        StopMonitoringInternal(restoreAudioConfiguration: true);
    }

    private void StopMonitoringInternal(bool restoreAudioConfiguration, bool notifySharedInputStopped = true)
    {
        if (monitoring || awaitingMicrophoneStart || usingPortAudioBackend)
            LogAudioRouteEvent("Stopping monitoring", $"restoreAudioConfiguration={restoreAudioConfiguration}\n{GetPortAudioDiagnosticSummary()}");

        usingPortAudioBackend = false;
        StopUnityOutputCapture();
        StopUnityRecorderCapture();
        if (portAudioStream != null && portAudioStream.IsRunning)
            portAudioStream.Stop();
        if (portAudioSplitStream != null && portAudioSplitStream.IsRunning)
            portAudioSplitStream.Stop();
        StopSharedInputRoute(notifySharedInputStopped);
        StopUnifiedSongSourceTaps();

        if (monitorSource != null && monitorSource.isPlaying)
            monitorSource.Stop();

        if (!string.IsNullOrWhiteSpace(pendingDeviceName))
        {
            try
            {
                if (Microphone.IsRecording(pendingDeviceName))
                    Microphone.End(pendingDeviceName);
            }
            catch
            {
                // Ignore device shutdown errors.
            }
        }

        monitoring = false;
        awaitingMicrophoneStart = false;
        pendingMicrophoneClip = null;
        pendingDeviceName = string.Empty;
        activeHostApiName = string.Empty;
        preparedCompiledPedalSampleRate = -1;
        preparedCompiledPedalChannelCount = -1;

        if (restoreAudioConfiguration)
            RestorePreviousAudioConfiguration();

        microphoneClipFrameCount = 0;
        microphoneClipChannelCount = 1;
        microphoneClipReadFramePosition = 0;
        microphoneSnapshotBuffer = Array.Empty<float>();
        microphoneInputRingBuffer = Array.Empty<float>();
        microphoneRawInputCallbackBuffer = Array.Empty<float>();
        microphoneInputRingWriteIndex = 0;
        microphoneInputRingReadIndex = 0;
        microphoneInputRingCount = 0;
        portAudioProcessBuffer = Array.Empty<float>();
        portAudioProcessBuffersBySampleCount.Clear();

        inputRouteLabel = string.IsNullOrWhiteSpace(settings?.input_device_name) ? "Automatic Input" : settings.input_device_name;
        statusMessage = "Stopped";
    }

    private bool TryStartMonitoringLegacyPath()
    {
        if (portAudioInputDevices.Length > 0)
        {
            List<PortAudioRoutePlan> plans = BuildLegacyRoutePlans();
            if (plans.Count == 0)
            {
                statusMessage = "No PortAudio duplex route available.";
                LogAudioRouteEvent("Legacy PortAudio unavailable", BuildDeviceCatalogLog(), warning: true);
            }
            else
            {
                StopMonitoringInternal(restoreAudioConfiguration: false, notifySharedInputStopped: false);
                StringBuilder attempts = new StringBuilder();
                attempts.AppendLine($"Candidate preview: {FormatRoutePlanPreview(plans)}");
                for (int i = 0; i < plans.Count; i++)
                {
                    PortAudioRoutePlan plan = plans[i];
                    attempts.AppendLine(
                        $"Attempt {i + 1}: {BuildAdvancedDeviceChoiceLabel(plan.InputDevice)} -> {BuildAdvancedDeviceChoiceLabel(plan.OutputDevice)} @ {plan.SampleRate} Hz / {plan.BufferSize}");

                    if (TryStartPortAudioRoute(
                        plan.InputDevice,
                        plan.OutputDevice,
                        plan.SampleRate,
                        plan.BufferSize,
                        enableUnityOutputCapture: false,
                        enableUnityRecorderCapture: advancedRoutingOptions != null && advancedRoutingOptions.unityRecorderCaptureEnabled,
                        out string error,
                        out string captureNotice))
                    {
                        if (!string.IsNullOrWhiteSpace(captureNotice))
                            attempts.AppendLine($"  Note: {captureNotice}");
                        attempts.AppendLine("  Result: success");
                        lastRoutingAttemptSummary = attempts.ToString().TrimEnd();
                        LogAudioRouteEvent("Legacy PortAudio monitoring live", lastRoutingAttemptSummary);
                        return true;
                    }

                    attempts.AppendLine($"  Result: {error}");
                    Debug.LogWarning($"[UnityToneLabRuntime] PortAudio route attempt failed: {error}");
                }

                lastRoutingAttemptSummary = attempts.ToString().TrimEnd();
                LogAudioRouteEvent("Legacy PortAudio attempts failed", lastRoutingAttemptSummary, warning: true);
                RestoreDeferredSharedInputRouteAfterFailedPortAudioStart();
            }
        }

        RestoreDeferredSharedInputRouteAfterFailedPortAudioStart();
        inputDevices = Microphone.devices ?? Array.Empty<string>();
        outputDevices = new[] { "System Default" };
        LogAudioRouteEvent("Falling back to Unity microphone monitoring", BuildDeviceCatalogLog(), warning: true);
        return TryStartUnityMicrophoneMonitoring();
    }

    private bool TryStartMonitoringAdvancedPath()
    {
        StringBuilder diagnostics = new StringBuilder();
        StringBuilder attempts = new StringBuilder();
        string backendMode = SharedAudioBackendModes.Normalize(advancedRoutingOptions.backendMode);
        bool allowFallback = advancedRoutingOptions.allowFallback;

        diagnostics.AppendLine($"Beta enabled: {advancedRoutingOptions.betaEnabled}");
        diagnostics.AppendLine($"Input channel mode: {SharedAudioInputChannelModes.Normalize(advancedRoutingOptions.inputChannelMode)}");
        diagnostics.AppendLine($"Backend mode: {backendMode}");
        diagnostics.AppendLine($"Allow fallback: {allowFallback}");
        diagnostics.AppendLine($"Split input/output: {advancedRoutingOptions.splitInputOutputEnabled}");
        diagnostics.AppendLine($"Unified output beta: {advancedRoutingOptions.unifiedOutputEnabled}");
        diagnostics.AppendLine($"Unity Recorder guitar capture: {advancedRoutingOptions.unityRecorderCaptureEnabled}");
        if (advancedRoutingOptions.unifiedOutputEnabled || advancedRoutingOptions.unityRecorderCaptureEnabled)
            diagnostics.AppendLine($"Unity audio sample rate lock: {GetUnityOutputSampleRate()} Hz");
        diagnostics.AppendLine($"Requested input: {(string.IsNullOrWhiteSpace(advancedRoutingOptions.preferredInputDeviceName) ? "Automatic" : advancedRoutingOptions.preferredInputDeviceName)}");
        diagnostics.AppendLine($"Requested output: {(string.IsNullOrWhiteSpace(advancedRoutingOptions.preferredOutputDeviceName) ? "Automatic" : advancedRoutingOptions.preferredOutputDeviceName)}");
        diagnostics.AppendLine($"Requested sample rate: {SharedAudioSampleRateOptions.ToLabel(advancedRoutingOptions.sampleRate)}");
        diagnostics.AppendLine($"Requested buffer: {ResolveMonitoringBufferSize(advancedRoutingOptions.bufferSize)}");

        List<PortAudioRoutePlan> plans = advancedRoutingOptions.splitInputOutputEnabled
            ? BuildAdvancedSplitRoutePlans()
            : BuildAdvancedRoutePlans();
        diagnostics.AppendLine($"PortAudio devices: {portAudioAllDevices.Length}");
        diagnostics.AppendLine($"Candidate route plans: {plans.Count}");
        diagnostics.AppendLine($"Candidate preview: {FormatRoutePlanPreview(plans)}");

        if (plans.Count > 0)
        {
            StopMonitoringInternal(restoreAudioConfiguration: false, notifySharedInputStopped: false);
            for (int i = 0; i < plans.Count; i++)
            {
                PortAudioRoutePlan plan = plans[i];
                attempts.AppendLine(
                    $"Attempt {i + 1}: {BuildAdvancedDeviceChoiceLabel(plan.InputDevice)} -> {BuildAdvancedDeviceChoiceLabel(plan.OutputDevice)} @ {plan.SampleRate} Hz / {plan.BufferSize}");

                string error;
                string captureNotice;
                bool started = advancedRoutingOptions.splitInputOutputEnabled
                    ? TryStartPortAudioSplitRoute(
                        plan.InputDevice,
                        plan.OutputDevice,
                        plan.SampleRate,
                        plan.BufferSize,
                        advancedRoutingOptions.unifiedOutputEnabled,
                        advancedRoutingOptions.unityRecorderCaptureEnabled,
                        out error,
                        out captureNotice)
                    : TryStartPortAudioRoute(
                        plan.InputDevice,
                        plan.OutputDevice,
                        plan.SampleRate,
                        plan.BufferSize,
                        advancedRoutingOptions.unifiedOutputEnabled,
                        advancedRoutingOptions.unityRecorderCaptureEnabled,
                        out error,
                        out captureNotice);

                if (started)
                {
                    if (!string.IsNullOrWhiteSpace(captureNotice))
                        attempts.AppendLine($"  Note: {captureNotice}");
                    attempts.AppendLine("  Result: success");
                    lastRoutingDiagnostics = diagnostics.ToString().TrimEnd();
                    lastRoutingAttemptSummary = attempts.ToString().TrimEnd();
                    LogAudioRouteEvent("Advanced PortAudio monitoring live", $"{lastRoutingDiagnostics}\n{lastRoutingAttemptSummary}");
                    return true;
                }

                attempts.AppendLine($"  Result: {error}");
                Debug.LogWarning($"[UnityToneLabRuntime] Advanced PortAudio attempt failed: {error}");
            }
        }

        if (allowFallback)
        {
            attempts.AppendLine("Fallback: trying legacy monitoring path.");
            bool fallbackStarted = TryStartMonitoringLegacyPath();
            if (fallbackStarted)
            {
                attempts.AppendLine("  Result: legacy path success");
                statusMessage = $"{statusMessage}  (advanced fallback)";
                lastRoutingDiagnostics = diagnostics.ToString().TrimEnd();
                lastRoutingAttemptSummary = attempts.ToString().TrimEnd();
                LogAudioRouteEvent("Advanced audio fell back to legacy monitoring", $"{lastRoutingDiagnostics}\n{lastRoutingAttemptSummary}", warning: true);
                return true;
            }

            attempts.AppendLine($"  Result: legacy path failed ({statusMessage})");
        }

        if (plans.Count == 0)
        {
            statusMessage = backendMode == SharedAudioBackendModes.Auto
                ? $"Advanced audio beta found no valid PortAudio {(advancedRoutingOptions.splitInputOutputEnabled ? "split" : "duplex")} route."
                : $"Advanced audio beta found no valid {backendMode} {(advancedRoutingOptions.splitInputOutputEnabled ? "split" : "duplex")} route.";
        }

        lastRoutingDiagnostics = diagnostics.ToString().TrimEnd();
        lastRoutingAttemptSummary = attempts.ToString().TrimEnd();
        LogAudioRouteEvent("Advanced audio failed to start", $"{lastRoutingDiagnostics}\n{lastRoutingAttemptSummary}", warning: true);
        RestoreDeferredSharedInputRouteAfterFailedPortAudioStart();
        return false;
    }

    private bool TryStartSharedInputRoute(SharedInputRouteInfo route)
    {
        sharedInputRouteActive = false;
        sharedInputSubmitDisabled = false;
        sharedInputSubmitFailurePending = false;
        activeSharedInputRoute = route?.Clone();
        Func<SharedInputRouteInfo, bool> callback = SharedInputRouteStarting;
        if (callback == null || route == null)
            return false;

        try
        {
            bool started = callback(route.Clone());
            if (started)
                sharedInputRestoreDeferredAfterRouteFailure = false;
            sharedInputRouteActive = started;
            return started;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnityToneLabRuntime] Shared detector input start failed: {ex.Message}");
            return false;
        }
    }

    private void StopSharedInputRoute(bool notifyStopped = true)
    {
        bool wasActive = sharedInputRouteActive;
        sharedInputRouteActive = false;
        sharedInputSubmitDisabled = true;
        sharedInputSubmitFailurePending = false;
        activeSharedInputRoute = null;
        if (!wasActive || !notifyStopped)
            return;

        sharedInputRestoreDeferredAfterRouteFailure = false;
        try
        {
            SharedInputRouteStopped?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnityToneLabRuntime] Shared detector input stop handler failed: {ex.Message}");
        }
    }

    private void HandleSharedInputSubmitFailureOnMainThread()
    {
        if (!sharedInputSubmitFailurePending)
            return;

        sharedInputSubmitFailurePending = false;
        if (!sharedInputRouteActive)
            return;

        Debug.LogWarning(
            "[UnityToneLabRuntime] Shared detector input stopped accepting Tone Lab audio; restoring independent detector input. " +
            $"submits={Volatile.Read(ref sharedDetectorSubmitCount)}, rejects={Volatile.Read(ref sharedDetectorRejectedCount)}, activeRoute={FormatSharedInputRoute(activeSharedInputRoute)}, {GetPortAudioDiagnosticSummary()}");
        StopSharedInputRoute();
    }

    private static string FormatSharedInputRoute(SharedInputRouteInfo route)
    {
        if (route == null)
            return "(none)";

        return $"deviceIndex={route.InputDeviceIndex}, device={route.InputDeviceDisplayName}, host={route.HostApiName}, sampleRate={route.SampleRate}, channels={route.InputChannelCount}, maxBlockFrames={route.MaxBlockFrames}, inputMode={route.InputChannelMode}";
    }

    private void DeferSharedInputRestoreAfterFailedPortAudioStart()
    {
        if (SharedInputRouteStopped != null)
            sharedInputRestoreDeferredAfterRouteFailure = true;
    }

    private void RestoreDeferredSharedInputRouteAfterFailedPortAudioStart()
    {
        if (!sharedInputRestoreDeferredAfterRouteFailure)
            return;

        sharedInputRestoreDeferredAfterRouteFailure = false;
        if (sharedInputRouteActive)
        {
            StopSharedInputRoute();
            return;
        }

        try
        {
            SharedInputRouteStopped?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnityToneLabRuntime] Deferred shared detector input restore failed: {ex.Message}");
        }
    }

    private static int ResolvePortAudioCallbackFrameCapacity(uint framesPerBuffer)
    {
        if (framesPerBuffer > 0 && framesPerBuffer <= int.MaxValue)
            return Mathf.Max((int)framesPerBuffer, SafeDspBufferSize);

        return DriverManagedPortAudioMaxBlockFrames;
    }

    private bool TryStartPortAudioRoute(
        ToneLabPortAudio.DeviceDescriptor inputDevice,
        ToneLabPortAudio.DeviceDescriptor outputDevice,
        int sampleRate,
        int monitoringBufferSize,
        bool enableUnityOutputCapture,
        bool enableUnityRecorderCapture,
        out string error,
        out string captureNotice)
    {
        error = string.Empty;
        captureNotice = string.Empty;
        if (inputDevice == null || outputDevice == null)
        {
            error = "No PortAudio duplex route available.";
            statusMessage = error;
            return false;
        }

        if (enableUnityOutputCapture || enableUnityRecorderCapture)
        {
            int unityOutputSampleRate = GetUnityOutputSampleRate();
            if (sampleRate != unityOutputSampleRate)
                sampleRate = unityOutputSampleRate;
        }

        int inputChannels = ResolveRequestedChannelCount(inputDevice, input: true);
        int outputChannels = ResolveRequestedChannelCount(outputDevice, input: false);
        bool driverManagedBuffer = monitoringBufferSize <= 0;
        uint framesPerBuffer = driverManagedBuffer ? 0u : (uint)ResolveMonitoringBufferSize(monitoringBufferSize);
        int callbackFrameCapacity = ResolvePortAudioCallbackFrameCapacity(framesPerBuffer);
        string routeDescription =
            $"{BuildAdvancedDeviceChoiceLabel(inputDevice)} [{inputChannels}ch] -> " +
            $"{BuildAdvancedDeviceChoiceLabel(outputDevice)} [{outputChannels}ch]";

        SharedInputRouteInfo sharedRoute = new SharedInputRouteInfo
        {
            InputDeviceIndex = inputDevice.Index,
            InputDeviceDisplayName = BuildAdvancedDeviceChoiceLabel(inputDevice),
            HostApiName = inputDevice.HostApiName,
            SampleRate = sampleRate,
            InputChannelCount = inputChannels,
            MaxBlockFrames = callbackFrameCapacity,
            InputChannelMode = SharedAudioInputChannelModes.Normalize(advancedRoutingOptions?.inputChannelMode)
        };

        bool sharedInputStarted = TryStartSharedInputRoute(sharedRoute);
        activeSampleRate = sampleRate;
        activeDspBufferSize = driverManagedBuffer ? 0 : ResolveMonitoringBufferSize(monitoringBufferSize);
        if (!driverManagedBuffer)
            GetPortAudioProcessBuffer(callbackFrameCapacity * (outputChannels > 1 ? 2 : 1));
        EnsureUnityOutputMixBufferCapacity(callbackFrameCapacity * Mathf.Max(inputChannels, outputChannels));

        string portAudioStartError = string.Empty;
        bool started = portAudioStream != null && portAudioStream.Start(
            inputDevice.Index,
            outputDevice.Index,
            inputChannels,
            outputChannels,
            sampleRate,
            framesPerBuffer,
            Math.Max(0.0, inputDevice.DefaultLowInputLatency),
            Math.Max(0.0, outputDevice.DefaultLowOutputLatency),
            routeDescription,
            out portAudioStartError);

        if (!started)
        {
            if (sharedInputStarted)
                DeferSharedInputRestoreAfterFailedPortAudioStart();
            error = portAudioStartError;
            statusMessage = portAudioStartError;
            return false;
        }

        monitoring = true;
        usingPortAudioBackend = true;
        awaitingMicrophoneStart = false;
        sharedInputRouteActive = sharedInputStarted;
        activeSharedInputRoute = sharedRoute;
        activeHostApiName = outputDevice.HostApiName;
        pendingDeviceName = inputDevice.DisplayName;
        inputRouteLabel = BuildAdvancedDeviceChoiceLabel(inputDevice);
        outputRouteLabel = BuildAdvancedDeviceChoiceLabel(outputDevice);
        preparedCompiledPedalSampleRate = -1;
        preparedCompiledPedalChannelCount = -1;
        SetUnityRecorderCaptureActive(enableUnityRecorderCapture, outputChannels > 1 ? 2 : 1, sampleRate);
        ResetLiveAudioDiagnosticsBurst();

        if (enableUnityOutputCapture)
        {
            bool usingDirectSourceTaps = TryStartUnifiedSongSourceTaps(out captureNotice);
            if (!usingDirectSourceTaps)
            {
                if (!TryStartUnityOutputCapture(out string captureError))
                {
                    if (!advancedRoutingOptions.allowFallback)
                    {
                        error = captureError;
                        statusMessage = captureError;
                        if (sharedInputStarted)
                            DeferSharedInputRestoreAfterFailedPortAudioStart();
                        StopMonitoringInternal(restoreAudioConfiguration: false, notifySharedInputStopped: false);
                        return false;
                    }

                    captureNotice = captureError;
                }
                else
                {
                    captureNotice = "Unified output capture active.";
                }
            }
        }

        statusMessage = $"Live  {inputRouteLabel}  \u2022  {sampleRate} Hz  \u2022  Buffer {FormatActiveBufferLabel(activeDspBufferSize)}  \u2022  PortAudio  {GetNormalizedHostApiLabel(outputDevice.HostApiName)}";
        if (!string.IsNullOrWhiteSpace(captureNotice))
            statusMessage = $"{statusMessage}  \u2022  {captureNotice}";
        if (enableUnityRecorderCapture)
            statusMessage = $"{statusMessage}  \u2022  Unity Recorder guitar capture";
        LogAudioRouteEvent(
            "PortAudio route opened",
            $"route={routeDescription}, sampleRate={sampleRate}, framesPerBuffer={framesPerBuffer}, inputLatency={inputDevice.DefaultLowInputLatency:0.####}, outputLatency={outputDevice.DefaultLowOutputLatency:0.####}, unityOutputCapture={enableUnityOutputCapture}, recorderCapture={enableUnityRecorderCapture}, sharedInputStarted={sharedInputStarted}\n{GetPortAudioDiagnosticSummary()}");
        sharedInputRestoreDeferredAfterRouteFailure = false;
        return true;
    }

    private bool TryStartPortAudioSplitRoute(
        ToneLabPortAudio.DeviceDescriptor inputDevice,
        ToneLabPortAudio.DeviceDescriptor outputDevice,
        int sampleRate,
        int monitoringBufferSize,
        bool enableUnityOutputCapture,
        bool enableUnityRecorderCapture,
        out string error,
        out string captureNotice)
    {
        error = string.Empty;
        captureNotice = string.Empty;
        if (inputDevice == null || outputDevice == null)
        {
            error = "No PortAudio split route available.";
            statusMessage = error;
            return false;
        }

        if (enableUnityOutputCapture || enableUnityRecorderCapture)
        {
            int unityOutputSampleRate = GetUnityOutputSampleRate();
            if (sampleRate != unityOutputSampleRate)
                sampleRate = unityOutputSampleRate;
        }

        int inputChannels = ResolveRequestedChannelCount(inputDevice, input: true);
        int outputChannels = ResolveRequestedChannelCount(outputDevice, input: false);
        bool driverManagedBuffer = monitoringBufferSize <= 0;
        uint framesPerBuffer = driverManagedBuffer ? 0u : (uint)ResolveMonitoringBufferSize(monitoringBufferSize);
        int callbackFrameCapacity = ResolvePortAudioCallbackFrameCapacity(framesPerBuffer);
        string routeDescription =
            $"{BuildAdvancedDeviceChoiceLabel(inputDevice)} [{inputChannels}ch] -> " +
            $"{BuildAdvancedDeviceChoiceLabel(outputDevice)} [{outputChannels}ch] (split)";

        SharedInputRouteInfo sharedRoute = new SharedInputRouteInfo
        {
            InputDeviceIndex = inputDevice.Index,
            InputDeviceDisplayName = BuildAdvancedDeviceChoiceLabel(inputDevice),
            HostApiName = inputDevice.HostApiName,
            SampleRate = sampleRate,
            InputChannelCount = inputChannels,
            MaxBlockFrames = callbackFrameCapacity,
            InputChannelMode = SharedAudioInputChannelModes.Normalize(advancedRoutingOptions?.inputChannelMode)
        };

        bool sharedInputStarted = TryStartSharedInputRoute(sharedRoute);
        activeSampleRate = sampleRate;
        activeDspBufferSize = driverManagedBuffer ? 0 : ResolveMonitoringBufferSize(monitoringBufferSize);
        if (!driverManagedBuffer)
            GetPortAudioProcessBuffer(callbackFrameCapacity * (outputChannels > 1 ? 2 : 1));
        EnsureUnityOutputMixBufferCapacity(callbackFrameCapacity * Mathf.Max(inputChannels, outputChannels));

        string portAudioStartError = string.Empty;
        bool started = portAudioSplitStream != null && portAudioSplitStream.Start(
            inputDevice.Index,
            outputDevice.Index,
            inputChannels,
            outputChannels,
            sampleRate,
            framesPerBuffer,
            Math.Max(0.0, inputDevice.DefaultLowInputLatency),
            Math.Max(0.0, outputDevice.DefaultLowOutputLatency),
            routeDescription,
            out portAudioStartError);

        if (!started)
        {
            if (sharedInputStarted)
                DeferSharedInputRestoreAfterFailedPortAudioStart();
            error = portAudioStartError;
            statusMessage = portAudioStartError;
            return false;
        }

        monitoring = true;
        usingPortAudioBackend = true;
        awaitingMicrophoneStart = false;
        sharedInputRouteActive = sharedInputStarted;
        activeSharedInputRoute = sharedRoute;
        activeHostApiName = $"{GetNormalizedHostApiLabel(inputDevice.HostApiName)} -> {GetNormalizedHostApiLabel(outputDevice.HostApiName)}";
        pendingDeviceName = inputDevice.DisplayName;
        inputRouteLabel = BuildAdvancedDeviceChoiceLabel(inputDevice);
        outputRouteLabel = BuildAdvancedDeviceChoiceLabel(outputDevice);
        preparedCompiledPedalSampleRate = -1;
        preparedCompiledPedalChannelCount = -1;
        SetUnityRecorderCaptureActive(enableUnityRecorderCapture, outputChannels > 1 ? 2 : 1, sampleRate);
        ResetLiveAudioDiagnosticsBurst();

        if (enableUnityOutputCapture)
        {
            bool usingDirectSourceTaps = TryStartUnifiedSongSourceTaps(out captureNotice);
            if (!usingDirectSourceTaps)
            {
                if (!TryStartUnityOutputCapture(out string captureError))
                {
                    if (!advancedRoutingOptions.allowFallback)
                    {
                        error = captureError;
                        statusMessage = captureError;
                        if (sharedInputStarted)
                            DeferSharedInputRestoreAfterFailedPortAudioStart();
                        StopMonitoringInternal(restoreAudioConfiguration: false, notifySharedInputStopped: false);
                        return false;
                    }

                    captureNotice = captureError;
                }
                else
                {
                    captureNotice = "Unified output capture active.";
                }
            }
        }

        statusMessage = $"Live  {inputRouteLabel}  \u2022  {sampleRate} Hz  \u2022  Buffer {FormatActiveBufferLabel(activeDspBufferSize)}  \u2022  PortAudio split  {activeHostApiName}";
        if (!string.IsNullOrWhiteSpace(captureNotice))
            statusMessage = $"{statusMessage}  \u2022  {captureNotice}";
        if (enableUnityRecorderCapture)
            statusMessage = $"{statusMessage}  \u2022  Unity Recorder guitar capture";
        LogAudioRouteEvent(
            "PortAudio split route opened",
            $"route={routeDescription}, sampleRate={sampleRate}, framesPerBuffer={framesPerBuffer}, inputLatency={inputDevice.DefaultLowInputLatency:0.####}, outputLatency={outputDevice.DefaultLowOutputLatency:0.####}, unityOutputCapture={enableUnityOutputCapture}, recorderCapture={enableUnityRecorderCapture}, sharedInputStarted={sharedInputStarted}\n{GetPortAudioDiagnosticSummary()}");
        sharedInputRestoreDeferredAfterRouteFailure = false;
        return true;
    }

    private bool TryStartUnityMicrophoneMonitoring()
    {
        if (inputDevices.Length == 0)
        {
            statusMessage = "No microphone inputs found.";
            LogAudioRouteEvent("Unity microphone monitoring unavailable", BuildDeviceCatalogLog(), warning: true);
            return false;
        }

        ApplyLowLatencyAudioConfiguration();
        StopMonitoringInternal(restoreAudioConfiguration: false);

        pendingDeviceName = ResolveUnityMicrophoneDeviceName(settings.input_device_name, inputDevices);
        if (string.IsNullOrWhiteSpace(pendingDeviceName))
            pendingDeviceName = inputDevices[0];
        inputRouteLabel = pendingDeviceName;

        try
        {
            pendingMicrophoneClip = Microphone.Start(pendingDeviceName, true, MicrophoneClipLengthSeconds, PreferredSampleRate);
            awaitingMicrophoneStart = pendingMicrophoneClip != null;
            activeHostApiName = awaitingMicrophoneStart ? "Unity Audio" : string.Empty;
            microphoneStartupDeadline = Time.unscaledTime + MicrophoneStartupTimeoutSeconds;
            statusMessage = awaitingMicrophoneStart
                ? $"Starting live monitoring on {pendingDeviceName}..."
                : "Failed to allocate microphone clip.";
            LogAudioRouteEvent(
                awaitingMicrophoneStart ? "Unity microphone startup requested" : "Unity microphone allocation failed",
                BuildDeviceCatalogLog(),
                warning: !awaitingMicrophoneStart);
            return awaitingMicrophoneStart;
        }
        catch (Exception ex)
        {
            awaitingMicrophoneStart = false;
            pendingMicrophoneClip = null;
            statusMessage = $"Microphone start failed: {ex.Message}";
            Debug.LogWarning($"[UnityToneLabRuntime] Failed to start microphone '{pendingDeviceName}': {ex.Message}");
            LogAudioRouteEvent("Unity microphone start failed", ex.ToString(), warning: true);
            return false;
        }
    }

    private static string ResolveUnityMicrophoneDeviceName(string savedName, IReadOnlyList<string> devices)
    {
        if (devices == null || devices.Count == 0 || string.IsNullOrWhiteSpace(savedName))
            return string.Empty;

        for (int i = 0; i < devices.Count; i++)
        {
            string device = devices[i];
            if (string.Equals(device, savedName, StringComparison.OrdinalIgnoreCase))
                return device;
        }

        string normalizedSaved = SharedAudioSettingsUtility.NormalizeDeviceKey(savedName);
        for (int i = 0; i < devices.Count; i++)
        {
            string device = devices[i];
            if (string.Equals(SharedAudioSettingsUtility.NormalizeDeviceKey(device), normalizedSaved, StringComparison.Ordinal))
                return device;
        }

        return string.Empty;
    }

    private void BeginMicrophonePlayback(int requiredLeadSamples)
    {
        awaitingMicrophoneStart = false;
        monitoring = true;
        activeSampleRate = pendingMicrophoneClip != null ? pendingMicrophoneClip.frequency : PreferredSampleRate;
        activeDspBufferSize = AudioSettings.GetConfiguration().dspBufferSize;
        activeHostApiName = "Unity Audio";
        monitorSource.clip = CreateMonitorDriverClip(activeSampleRate);
        monitorSource.loop = true;

        PrepareCompiledPedalChainIfNeeded(activeSampleRate, Mathf.Max(1, AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2));
        monitorSource.timeSamples = 0;
        monitorSource.Play();
        ResetLiveAudioDiagnosticsBurst();

        int bufferLength;
        int numBuffers;
        AudioSettings.GetDSPBufferSize(out bufferLength, out numBuffers);
        float latencyMs = 1000f * bufferLength * Mathf.Max(1, numBuffers) / Mathf.Max(1f, activeSampleRate);
        float startupLeadMs = 1000f * requiredLeadSamples / Mathf.Max(1f, activeSampleRate);
        statusMessage = $"Live  {pendingDeviceName}  \u2022  {activeSampleRate} Hz  \u2022  Buffer {bufferLength} x {numBuffers}  \u2022  Startup {startupLeadMs:F1} ms  \u2022  ~{latencyMs:F1} ms";
        LogAudioRouteEvent(
            "Unity microphone monitoring live",
            $"requiredLeadSamples={requiredLeadSamples}, startupLeadMs={startupLeadMs:F1}, dspBufferLength={bufferLength}, dspBufferCount={numBuffers}, estimatedDspLatencyMs={latencyMs:F1}");
    }

    private void PrepareMicrophoneCaptureBuffers(AudioClip microphoneClip, int requiredLeadSamples)
    {
        if (microphoneClip == null)
            return;

        microphoneClipFrameCount = Mathf.Max(1, microphoneClip.samples);
        microphoneClipChannelCount = Mathf.Max(1, microphoneClip.channels);
        int snapshotLength = microphoneClipFrameCount * microphoneClipChannelCount;
        if (microphoneSnapshotBuffer == null || microphoneSnapshotBuffer.Length != snapshotLength)
            microphoneSnapshotBuffer = new float[snapshotLength];

        int ringLength = Mathf.Max(microphoneClipFrameCount * 2, requiredLeadSamples * 8);
        if (microphoneInputRingBuffer == null || microphoneInputRingBuffer.Length != ringLength)
            microphoneInputRingBuffer = new float[ringLength];

        desiredMonitorLeadSamples = Mathf.Max(MinimumStartupLeadSamples, requiredLeadSamples);
        microphoneClipReadFramePosition = 0;
        microphoneInputRingWriteIndex = 0;
        microphoneInputRingReadIndex = 0;
        microphoneInputRingCount = 0;
    }

    private void PumpRecordedMicrophoneSamples(int micPosition)
    {
        if (pendingMicrophoneClip == null || microphoneClipFrameCount <= 0 || microphoneSnapshotBuffer == null || microphoneSnapshotBuffer.Length == 0)
            return;

        if (micPosition < 0)
            return;

        pendingMicrophoneClip.GetData(microphoneSnapshotBuffer, 0);
        int framesToCopy = micPosition - microphoneClipReadFramePosition;
        if (framesToCopy < 0)
            framesToCopy += microphoneClipFrameCount;

        if (framesToCopy <= 0)
            return;

        if (microphoneRawInputCallbackBuffer == null || microphoneRawInputCallbackBuffer.Length < framesToCopy)
            microphoneRawInputCallbackBuffer = new float[Mathf.NextPowerOfTwo(Mathf.Max(1, framesToCopy))];

        lock (microphoneBufferLock)
        {
            for (int i = 0; i < framesToCopy; i++)
            {
                int frameIndex = microphoneClipReadFramePosition + i;
                if (frameIndex >= microphoneClipFrameCount)
                    frameIndex -= microphoneClipFrameCount;

                int sampleIndex = frameIndex * microphoneClipChannelCount;
                float monoSample = microphoneSnapshotBuffer[sampleIndex];
                microphoneRawInputCallbackBuffer[i] = monoSample;
                microphoneInputRingBuffer[microphoneInputRingWriteIndex] = monoSample;
                microphoneInputRingWriteIndex = (microphoneInputRingWriteIndex + 1) % microphoneInputRingBuffer.Length;

                if (microphoneInputRingCount < microphoneInputRingBuffer.Length)
                {
                    microphoneInputRingCount++;
                }
                else
                {
                    microphoneInputRingReadIndex = (microphoneInputRingReadIndex + 1) % microphoneInputRingBuffer.Length;
                }
            }

            int maxBufferedSamples = Mathf.Max(desiredMonitorLeadSamples * 2, MinimumStartupLeadSamples * 2);
            if (microphoneInputRingCount > maxBufferedSamples)
            {
                int dropCount = microphoneInputRingCount - desiredMonitorLeadSamples;
                microphoneInputRingReadIndex = (microphoneInputRingReadIndex + dropCount) % microphoneInputRingBuffer.Length;
                microphoneInputRingCount = Mathf.Max(0, microphoneInputRingCount - dropCount);
            }
        }

        CaptureRawInputMetrics(
            microphoneRawInputCallbackBuffer,
            1,
            framesToCopy,
            pendingMicrophoneClip != null && pendingMicrophoneClip.frequency > 0 ? pendingMicrophoneClip.frequency : activeSampleRate,
            SharedAudioInputChannelModes.Input1);
        NotifyRawInputBlockReceived(
            microphoneRawInputCallbackBuffer,
            1,
            framesToCopy,
            pendingMicrophoneClip != null && pendingMicrophoneClip.frequency > 0 ? pendingMicrophoneClip.frequency : activeSampleRate,
            SharedAudioInputChannelModes.Input1);

        microphoneClipReadFramePosition = micPosition;
    }

    private void NotifyRawInputBlockReceived(float[] input, int inputChannels, int frameCount, int sampleRate, string inputChannelMode)
    {
        Action<float[], int, int, int, string> rawInputBlockReceived = RawInputBlockReceived;
        if (rawInputBlockReceived == null || input == null || frameCount <= 0)
            return;

        try
        {
            rawInputBlockReceived(
                input,
                Mathf.Max(1, inputChannels),
                frameCount,
                Mathf.Clamp(sampleRate, 8000, 192000),
                SharedAudioInputChannelModes.Normalize(inputChannelMode));
        }
        catch
        {
            // Raw input observers must never interrupt audio capture or monitoring.
        }
    }

    private void ApplyLowLatencyAudioConfiguration()
    {
        if (!cachedPreToneLabAudioConfiguration.HasValue)
            cachedPreToneLabAudioConfiguration = AudioSettings.GetConfiguration();

        AudioConfiguration config = AudioSettings.GetConfiguration();
        int monitoringBufferSize = ResolveMonitoringBufferSize(CurrentSettings.monitoring_buffer_size);
        bool dirty = false;
        if (config.sampleRate != PreferredSampleRate)
        {
            config.sampleRate = PreferredSampleRate;
            dirty = true;
        }

        if (config.dspBufferSize != monitoringBufferSize)
        {
            config.dspBufferSize = monitoringBufferSize;
            dirty = true;
        }

        if (dirty)
            AudioSettings.Reset(config);
    }

    private void RestorePreviousAudioConfiguration()
    {
        if (!cachedPreToneLabAudioConfiguration.HasValue)
            return;

        AudioSettings.Reset(cachedPreToneLabAudioConfiguration.Value);
        cachedPreToneLabAudioConfiguration = null;
    }

    private void EnsureSettingsLoaded()
    {
        if (settingsLoaded)
            return;

        ExternalContentBootstrap.EnsureToneLabRuntimeContentReady();
        ToneLabPedalRegistry.RefreshExternalDescriptors();
        settings = LoadSettingsFromDisk();
        List<ToneLabPreset> presetsFromDisk = LoadPresetLibraryFromDisk();
        if (presetsFromDisk.Count > 0)
            settings.presets = presetsFromDisk;
        ClampSettings(settings);
        bool selectionChanged = RestoreWorkingRigFromSelectedPreset(settings);
        SavePresetLibraryToDisk(settings.presets);
        settingsLoaded = true;
        if (selectionChanged)
            settingsDirty = true;
        RebuildCompiledPedalChain();
    }

    private void RestoreWorkingRigFromSelectedPreset()
    {
        if (!settingsLoaded || settings == null)
            return;

        RestoreWorkingRigFromSelectedPreset(settings);
        RebuildCompiledPedalChain();
    }

    private static ToneLabSettings LoadSettingsFromDisk()
    {
        string path = ExternalContentPaths.PersistentToneLabConfigPath;
        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                ToneLabSettings loaded = JsonUtility.FromJson<ToneLabSettings>(json);
                if (loaded != null)
                    return loaded;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnityToneLabRuntime] Failed to load tone settings from '{path}': {ex.Message}");
        }

        return new ToneLabSettings();
    }

    private static List<ToneLabPreset> LoadPresetLibraryFromDisk()
    {
        List<ToneLabPreset> presets = new List<ToneLabPreset>();
        string directory = ExternalContentPaths.PersistentToneLabPresetDirectory;
        try
        {
            Directory.CreateDirectory(directory);
            string[] presetFiles = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < presetFiles.Length; i++)
            {
                string presetPath = presetFiles[i];
                try
                {
                    string json = File.ReadAllText(presetPath);
                    ToneLabPreset preset = JsonUtility.FromJson<ToneLabPreset>(json);
                    if (preset != null)
                        presets.Add(preset);
                }
                catch (Exception presetEx)
                {
                    Debug.LogWarning($"[UnityToneLabRuntime] Failed to load preset '{presetPath}': {presetEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnityToneLabRuntime] Failed to enumerate preset folder '{directory}': {ex.Message}");
        }

        return presets;
    }

    private void FlushSettingsToDisk()
    {
        if (!settingsLoaded || !settingsDirty)
            return;

        try
        {
            string directory = Path.GetDirectoryName(ExternalContentPaths.PersistentToneLabConfigPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(ExternalContentPaths.PersistentToneLabConfigPath, JsonUtility.ToJson(CreateSettingsStorageSnapshot(settings), true));
            settingsDirty = false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnityToneLabRuntime] Failed to save tone settings: {ex.Message}");
        }
    }

    private static void SavePresetLibraryToDisk(IReadOnlyList<ToneLabPreset> presets)
    {
        string directory = ExternalContentPaths.PersistentToneLabPresetDirectory;
        try
        {
            Directory.CreateDirectory(directory);
            string[] existingFiles = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            HashSet<string> expectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (presets != null)
            {
                for (int i = 0; i < presets.Count; i++)
                {
                    ToneLabPreset preset = presets[i];
                    if (preset == null || string.IsNullOrWhiteSpace(preset.preset_id))
                        continue;

                    string presetPath = GetPresetFilePath(preset);
                    expectedFiles.Add(presetPath);
                    File.WriteAllText(presetPath, JsonUtility.ToJson(preset, true));
                }
            }

            for (int i = 0; i < existingFiles.Length; i++)
            {
                string existingFile = existingFiles[i];
                if (!expectedFiles.Contains(existingFile))
                    File.Delete(existingFile);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnityToneLabRuntime] Failed to save preset library: {ex.Message}");
        }
    }

    private void MarkSettingsDirty()
    {
        settingsDirty = true;
        nextSettingsSaveTime = Time.unscaledTime + SettingsSaveDelaySeconds;
    }

    private static string ResolveSavedDeviceName(string savedName, ToneLabPortAudio.DeviceDescriptor[] devices)
    {
        if (devices == null || devices.Length == 0)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(savedName))
        {
            ToneLabPortAudio.DeviceDescriptor exact = Array.Find(devices, candidate =>
                string.Equals(candidate.DisplayName, savedName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact.DisplayName;

            string normalizedSavedName = NormalizePortAudioDeviceName(savedName);
            ToneLabPortAudio.DeviceDescriptor normalized = Array.Find(devices, candidate =>
                string.Equals(NormalizePortAudioDeviceName(candidate.DisplayName), normalizedSavedName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.Name, normalizedSavedName, StringComparison.OrdinalIgnoreCase));
            if (normalized != null)
                return normalized.DisplayName;
        }

        return string.Empty;
    }

    private static ToneLabPortAudio.DeviceDescriptor ResolvePortAudioDevice(string savedName, ToneLabPortAudio.DeviceDescriptor[] devices)
    {
        string resolvedName = ResolveSavedDeviceName(savedName, devices);
        if (string.IsNullOrWhiteSpace(resolvedName))
            return null;

        return Array.Find(devices, candidate => string.Equals(candidate.DisplayName, resolvedName, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePortAudioDeviceName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return string.Empty;

        string trimmed = rawName.Trim();
        int colonIndex = trimmed.IndexOf(':');
        if (colonIndex > 0)
        {
            string prefix = trimmed.Substring(0, colonIndex).Trim();
            bool isNumericPrefix = true;
            for (int i = 0; i < prefix.Length; i++)
            {
                if (!char.IsDigit(prefix[i]))
                {
                    isNumericPrefix = false;
                    break;
                }
            }

            if (isNumericPrefix)
                return trimmed.Substring(colonIndex + 1).Trim();
        }

        return trimmed;
    }

    private static int ResolveSampleRate(ToneLabPortAudio.DeviceDescriptor inputDevice, ToneLabPortAudio.DeviceDescriptor outputDevice)
    {
        double inputRate = inputDevice != null && inputDevice.DefaultSampleRate > 0.0 ? inputDevice.DefaultSampleRate : PreferredSampleRate;
        double outputRate = outputDevice != null && outputDevice.DefaultSampleRate > 0.0 ? outputDevice.DefaultSampleRate : PreferredSampleRate;
        int resolved = Mathf.RoundToInt((float)Math.Min(inputRate, outputRate));
        if (resolved <= 0)
            resolved = PreferredSampleRate;
        if (resolved > PreferredSampleRate)
            resolved = PreferredSampleRate;
        return resolved;
    }

    private static int ResolveUnityOutputCaptureChannelCount()
    {
        switch (AudioSettings.speakerMode)
        {
            case AudioSpeakerMode.Mono:
                return 1;
            case AudioSpeakerMode.Stereo:
            case AudioSpeakerMode.Prologic:
                return 2;
            case AudioSpeakerMode.Quad:
                return 4;
            case AudioSpeakerMode.Surround:
                return 5;
            case AudioSpeakerMode.Mode5point1:
                return 6;
            case AudioSpeakerMode.Mode7point1:
                return 8;
            default:
                return 2;
        }
    }

    private static int GetUnityOutputSampleRate()
    {
        int sampleRate = AudioSettings.outputSampleRate;
        return sampleRate > 0 ? sampleRate : PreferredSampleRate;
    }

    private bool TryStartUnityOutputCapture(out string error)
    {
        error = string.Empty;
        if (unityOutputCaptureActive)
            return true;

        try
        {
            unityOutputCaptureChannels = ResolveUnityOutputCaptureChannelCount();
            unityOutputCaptureSampleRate = GetUnityOutputSampleRate();
            bool started = AudioRenderer.Start();
            if (!started)
            {
                error = "Unity output capture could not start.";
                return false;
            }

            unityOutputCaptureActive = true;
            unityOutputCaptureRingBuffer = Array.Empty<float>();
            unityOutputCaptureWriteIndex = 0;
            unityOutputCaptureReadIndex = 0;
            unityOutputCaptureCount = 0;
            unityOutputCaptureUnderrunCount = 0;
            unityOutputCaptureOverflowCount = 0;
            unityOutputCapturePeakQueuedSamples = 0;
            unityOutputLimiterGain = 1f;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Unity output capture failed: {ex.Message}";
            return false;
        }
    }

    private void StopUnityOutputCapture()
    {
        if (!unityOutputCaptureActive)
            return;

        try
        {
            AudioRenderer.Stop();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnityToneLabRuntime] Failed to stop Unity output capture: {ex.Message}");
        }

        unityOutputCaptureActive = false;
        unityOutputCaptureRingBuffer = Array.Empty<float>();
        unityOutputCaptureWriteIndex = 0;
        unityOutputCaptureReadIndex = 0;
        unityOutputCaptureCount = 0;
        unityOutputCaptureUnderrunCount = 0;
        unityOutputCaptureOverflowCount = 0;
        unityOutputCapturePeakQueuedSamples = 0;
        unityOutputLimiterGain = 1f;
    }

    private void SetUnityRecorderCaptureActive(bool active, int channels, int sampleRate)
    {
        if (!active)
        {
            StopUnityRecorderCapture();
            return;
        }

        unityRecorderCaptureChannels = Mathf.Clamp(channels, 1, 2);
        unityRecorderCaptureSampleRate = sampleRate > 0 ? sampleRate : GetUnityOutputSampleRate();
        lock (unityRecorderCaptureLock)
        {
            unityRecorderCaptureRingBuffer = Array.Empty<float>();
            unityRecorderCaptureWriteIndex = 0;
            unityRecorderCaptureReadIndex = 0;
            unityRecorderCaptureCount = 0;
            unityRecorderCaptureUnderrunCount = 0;
            unityRecorderCaptureOverflowCount = 0;
            unityRecorderCapturePeakQueuedSamples = 0;
            unityRecorderCaptureActive = true;
        }

        if (monitorSource != null)
        {
            monitorSource.clip = CreateMonitorDriverClip(unityRecorderCaptureSampleRate);
            monitorSource.loop = true;
            monitorSource.timeSamples = 0;
            if (!monitorSource.isPlaying)
                monitorSource.Play();
        }
    }

    private void StopUnityRecorderCapture()
    {
        lock (unityRecorderCaptureLock)
        {
            unityRecorderCaptureActive = false;
            unityRecorderCaptureRingBuffer = Array.Empty<float>();
            unityRecorderCaptureWriteIndex = 0;
            unityRecorderCaptureReadIndex = 0;
            unityRecorderCaptureCount = 0;
            unityRecorderCaptureUnderrunCount = 0;
            unityRecorderCaptureOverflowCount = 0;
            unityRecorderCapturePeakQueuedSamples = 0;
        }
    }

    private void PumpUnityOutputCapture()
    {
        if (!unityOutputCaptureActive)
            return;

        int sampleCount = 0;
        NativeArray<float> captureBuffer = default;
        try
        {
            sampleCount = AudioRenderer.GetSampleCountForCaptureFrame();
            if (sampleCount <= 0)
                return;

            captureBuffer = new NativeArray<float>(sampleCount, Allocator.Temp);
            if (!AudioRenderer.Render(captureBuffer))
                return;

            AppendUnityOutputCapturedSamples(captureBuffer);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnityToneLabRuntime] Unity output capture render failed: {ex.Message}");
            StopUnityOutputCapture();
        }
        finally
        {
            if (captureBuffer.IsCreated)
                captureBuffer.Dispose();
        }
    }

    private void AppendUnityOutputCapturedSamples(NativeArray<float> samples)
    {
        if (!samples.IsCreated || samples.Length <= 0)
            return;

        lock (unityOutputCaptureLock)
        {
            int requiredCapacity = Mathf.Max(samples.Length * 4, unityOutputCaptureCount + samples.Length + 2048);
            if (unityOutputCaptureRingBuffer == null || unityOutputCaptureRingBuffer.Length < requiredCapacity)
            {
                float[] replacement = new float[requiredCapacity];
                for (int i = 0; i < unityOutputCaptureCount; i++)
                {
                    int sourceIndex = (unityOutputCaptureReadIndex + i) % Mathf.Max(1, unityOutputCaptureRingBuffer.Length);
                    replacement[i] = unityOutputCaptureRingBuffer.Length > 0 ? unityOutputCaptureRingBuffer[sourceIndex] : 0f;
                }

                unityOutputCaptureRingBuffer = replacement;
                unityOutputCaptureReadIndex = 0;
                unityOutputCaptureWriteIndex = unityOutputCaptureCount;
            }

            for (int i = 0; i < samples.Length; i++)
            {
                unityOutputCaptureRingBuffer[unityOutputCaptureWriteIndex] = samples[i];
                unityOutputCaptureWriteIndex = (unityOutputCaptureWriteIndex + 1) % unityOutputCaptureRingBuffer.Length;
                if (unityOutputCaptureCount < unityOutputCaptureRingBuffer.Length)
                {
                    unityOutputCaptureCount++;
                }
                else
                {
                    unityOutputCaptureOverflowCount++;
                    unityOutputCaptureReadIndex = (unityOutputCaptureReadIndex + 1) % unityOutputCaptureRingBuffer.Length;
                }
            }

            if (unityOutputCaptureCount > unityOutputCapturePeakQueuedSamples)
                unityOutputCapturePeakQueuedSamples = unityOutputCaptureCount;
        }
    }

    private int ConsumeUnityOutputCapturedSamples(float[] destination, int requestedSamples)
    {
        if (destination == null || requestedSamples <= 0)
            return 0;

        int copied = 0;
        lock (unityOutputCaptureLock)
        {
            int samplesToCopy = Mathf.Min(requestedSamples, unityOutputCaptureCount);
            for (int i = 0; i < samplesToCopy; i++)
            {
                destination[i] = unityOutputCaptureRingBuffer[unityOutputCaptureReadIndex];
                unityOutputCaptureReadIndex = (unityOutputCaptureReadIndex + 1) % unityOutputCaptureRingBuffer.Length;
            }

            unityOutputCaptureCount -= samplesToCopy;
            copied = samplesToCopy;
            if (samplesToCopy < requestedSamples)
                unityOutputCaptureUnderrunCount += requestedSamples - samplesToCopy;
        }

        if (copied < requestedSamples)
            Array.Clear(destination, copied, requestedSamples - copied);

        return copied;
    }

    private void AppendUnityRecorderCaptureSamples(float[] samples, int sampleCount)
    {
        if (!unityRecorderCaptureActive || samples == null || sampleCount <= 0)
            return;

        int safeSampleCount = Mathf.Min(sampleCount, samples.Length);
        if (safeSampleCount <= 0)
            return;

        lock (unityRecorderCaptureLock)
        {
            if (!unityRecorderCaptureActive)
                return;

            int minimumCapacity = Mathf.Max(safeSampleCount * 4, unityRecorderCaptureSampleRate * Mathf.Max(1, unityRecorderCaptureChannels));
            int requiredCapacity = Mathf.Max(2048, minimumCapacity);
            if (unityRecorderCaptureRingBuffer == null || unityRecorderCaptureRingBuffer.Length < requiredCapacity)
            {
                float[] replacement = new float[requiredCapacity];
                for (int i = 0; i < unityRecorderCaptureCount; i++)
                {
                    int sourceIndex = unityRecorderCaptureRingBuffer != null && unityRecorderCaptureRingBuffer.Length > 0
                        ? (unityRecorderCaptureReadIndex + i) % unityRecorderCaptureRingBuffer.Length
                        : 0;
                    replacement[i] = unityRecorderCaptureRingBuffer != null && unityRecorderCaptureRingBuffer.Length > 0 ? unityRecorderCaptureRingBuffer[sourceIndex] : 0f;
                }

                unityRecorderCaptureRingBuffer = replacement;
                unityRecorderCaptureReadIndex = 0;
                unityRecorderCaptureWriteIndex = unityRecorderCaptureCount;
            }

            for (int i = 0; i < safeSampleCount; i++)
            {
                unityRecorderCaptureRingBuffer[unityRecorderCaptureWriteIndex] = samples[i];
                unityRecorderCaptureWriteIndex = (unityRecorderCaptureWriteIndex + 1) % unityRecorderCaptureRingBuffer.Length;
                if (unityRecorderCaptureCount < unityRecorderCaptureRingBuffer.Length)
                {
                    unityRecorderCaptureCount++;
                }
                else
                {
                    unityRecorderCaptureOverflowCount++;
                    unityRecorderCaptureReadIndex = (unityRecorderCaptureReadIndex + 1) % unityRecorderCaptureRingBuffer.Length;
                }
            }

            if (unityRecorderCaptureCount > unityRecorderCapturePeakQueuedSamples)
                unityRecorderCapturePeakQueuedSamples = unityRecorderCaptureCount;
        }
    }

    private void FillOutputBufferFromUnityRecorderCapture(float[] data, int channels)
    {
        if (data == null || data.Length == 0)
            return;

        Array.Clear(data, 0, data.Length);
        if (!unityRecorderCaptureActive)
            return;

        int destinationChannels = Mathf.Max(1, channels);
        int sourceChannels = Mathf.Max(1, unityRecorderCaptureChannels);
        int frameCount = data.Length / destinationChannels;

        lock (unityRecorderCaptureLock)
        {
            if (!unityRecorderCaptureActive || unityRecorderCaptureRingBuffer == null || unityRecorderCaptureRingBuffer.Length == 0)
                return;

            for (int frame = 0; frame < frameCount; frame++)
            {
                float left = 0f;
                float right = 0f;
                if (unityRecorderCaptureCount >= sourceChannels)
                {
                    left = unityRecorderCaptureRingBuffer[unityRecorderCaptureReadIndex];
                    unityRecorderCaptureReadIndex = (unityRecorderCaptureReadIndex + 1) % unityRecorderCaptureRingBuffer.Length;
                    unityRecorderCaptureCount--;

                    if (sourceChannels > 1)
                    {
                        right = unityRecorderCaptureRingBuffer[unityRecorderCaptureReadIndex];
                        unityRecorderCaptureReadIndex = (unityRecorderCaptureReadIndex + 1) % unityRecorderCaptureRingBuffer.Length;
                        unityRecorderCaptureCount--;
                    }
                    else
                    {
                        right = left;
                    }
                }
                else
                {
                    unityRecorderCaptureUnderrunCount += sourceChannels;
                }

                int destinationIndex = frame * destinationChannels;
                if (destinationChannels == 1)
                {
                    data[destinationIndex] = (left + right) * 0.5f;
                    continue;
                }

                data[destinationIndex] = left;
                if (destinationIndex + 1 < data.Length)
                    data[destinationIndex + 1] = right;
                for (int channel = 2; channel < destinationChannels; channel++)
                {
                    int index = destinationIndex + channel;
                    if (index >= data.Length)
                        break;
                    data[index] = 0f;
                }
            }
        }
    }

    private static void DownmixUnityCapturedFrameToStereo(float[] source, int frameIndex, int captureChannels, out float left, out float right)
    {
        left = 0f;
        right = 0f;
        if (source == null || captureChannels <= 0)
            return;

        int frameStart = frameIndex * captureChannels;
        if (frameStart < 0 || frameStart >= source.Length)
            return;

        switch (captureChannels)
        {
            case 1:
            {
                float mono = source[frameStart];
                left = mono;
                right = mono;
                return;
            }
            case 2:
            {
                left = source[frameStart];
                right = frameStart + 1 < source.Length ? source[frameStart + 1] : left;
                return;
            }
            case 4:
            {
                float frontLeft = source[frameStart];
                float frontRight = frameStart + 1 < source.Length ? source[frameStart + 1] : frontLeft;
                float rearLeft = frameStart + 2 < source.Length ? source[frameStart + 2] : 0f;
                float rearRight = frameStart + 3 < source.Length ? source[frameStart + 3] : 0f;
                left = frontLeft + (rearLeft * 0.5f);
                right = frontRight + (rearRight * 0.5f);
                return;
            }
            case 5:
            {
                float frontLeft = source[frameStart];
                float frontRight = frameStart + 1 < source.Length ? source[frameStart + 1] : frontLeft;
                float center = frameStart + 2 < source.Length ? source[frameStart + 2] : 0f;
                float rearLeft = frameStart + 3 < source.Length ? source[frameStart + 3] : 0f;
                float rearRight = frameStart + 4 < source.Length ? source[frameStart + 4] : 0f;
                left = frontLeft + (center * 0.7071f) + (rearLeft * 0.5f);
                right = frontRight + (center * 0.7071f) + (rearRight * 0.5f);
                return;
            }
            case 6:
            {
                float frontLeft = source[frameStart];
                float frontRight = frameStart + 1 < source.Length ? source[frameStart + 1] : frontLeft;
                float center = frameStart + 2 < source.Length ? source[frameStart + 2] : 0f;
                float rearLeft = frameStart + 3 < source.Length ? source[frameStart + 3] : 0f;
                float rearRight = frameStart + 4 < source.Length ? source[frameStart + 4] : 0f;
                float lfe = frameStart + 5 < source.Length ? source[frameStart + 5] : 0f;
                left = frontLeft + (center * 0.7071f) + (rearLeft * 0.5f) + (lfe * 0.2f);
                right = frontRight + (center * 0.7071f) + (rearRight * 0.5f) + (lfe * 0.2f);
                return;
            }
            case 8:
            {
                float frontLeft = source[frameStart];
                float frontRight = frameStart + 1 < source.Length ? source[frameStart + 1] : frontLeft;
                float center = frameStart + 2 < source.Length ? source[frameStart + 2] : 0f;
                float rearLeft = frameStart + 3 < source.Length ? source[frameStart + 3] : 0f;
                float rearRight = frameStart + 4 < source.Length ? source[frameStart + 4] : 0f;
                float sideLeft = frameStart + 5 < source.Length ? source[frameStart + 5] : 0f;
                float sideRight = frameStart + 6 < source.Length ? source[frameStart + 6] : 0f;
                float lfe = frameStart + 7 < source.Length ? source[frameStart + 7] : 0f;
                left = frontLeft + (center * 0.7071f) + (rearLeft * 0.35f) + (sideLeft * 0.35f) + (lfe * 0.2f);
                right = frontRight + (center * 0.7071f) + (rearRight * 0.35f) + (sideRight * 0.35f) + (lfe * 0.2f);
                return;
            }
            default:
            {
                float leftSum = 0f;
                float rightSum = 0f;
                int leftCount = 0;
                int rightCount = 0;
                for (int channel = 0; channel < captureChannels; channel++)
                {
                    int sampleIndex = frameStart + channel;
                    if (sampleIndex >= source.Length)
                        break;

                    if ((channel & 1) == 0)
                    {
                        leftSum += source[sampleIndex];
                        leftCount++;
                    }
                    else
                    {
                        rightSum += source[sampleIndex];
                        rightCount++;
                    }
                }

                left = leftCount > 0 ? leftSum / leftCount : 0f;
                right = rightCount > 0 ? rightSum / rightCount : left;
                return;
            }
        }
    }

    private void MixUnifiedSongSourceTapAudio(float[] destination, int destinationChannels, int frameCount)
    {
        UnityToneLabAudioSourceTap[] taps = unifiedSongSourceTapSnapshot;
        if (taps == null || taps.Length == 0 || destination == null || destination.Length == 0 || frameCount <= 0)
            return;

        for (int tapIndex = 0; tapIndex < taps.Length; tapIndex++)
        {
            UnityToneLabAudioSourceTap tap = taps[tapIndex];
            if (tap == null)
                continue;

            int tapChannels = Mathf.Max(1, tap.ChannelCount);
            int requestedSamples = frameCount * tapChannels;
            EnsureUnityOutputMixBufferCapacity(requestedSamples);
            tap.Consume(unityOutputMixBuffer, requestedSamples);

            if (tapChannels == destinationChannels)
            {
                int sampleCount = Mathf.Min(destination.Length, requestedSamples);
                for (int i = 0; i < sampleCount; i++)
                    destination[i] += unityOutputMixBuffer[i];
                continue;
            }

            int frameLimit = Mathf.Min(frameCount, requestedSamples / tapChannels);
            if (destinationChannels == 1)
            {
                for (int frame = 0; frame < frameLimit; frame++)
                {
                    DownmixUnityCapturedFrameToStereo(unityOutputMixBuffer, frame, tapChannels, out float left, out float right);
                    destination[frame] += (left + right) * 0.5f;
                }

                continue;
            }

            if (destinationChannels >= 2)
            {
                for (int frame = 0; frame < frameLimit; frame++)
                {
                    DownmixUnityCapturedFrameToStereo(unityOutputMixBuffer, frame, tapChannels, out float left, out float right);
                    int destinationIndex = frame * destinationChannels;
                    if (destinationIndex >= destination.Length)
                        break;

                    destination[destinationIndex] += left;
                    if (destinationIndex + 1 < destination.Length)
                        destination[destinationIndex + 1] += right;
                }
            }
        }
    }

    private void MixUnityOutputCapturedAudio(float[] destination, int destinationChannels, int frameCount)
    {
        if (!unityOutputCaptureActive || destination == null || destination.Length == 0 || frameCount <= 0)
            return;

        int captureChannels = Mathf.Max(1, unityOutputCaptureChannels);
        int requestedSamples = frameCount * captureChannels;
        EnsureUnityOutputMixBufferCapacity(requestedSamples);
        ConsumeUnityOutputCapturedSamples(unityOutputMixBuffer, requestedSamples);

        if (captureChannels == destinationChannels)
        {
            int sampleCount = Mathf.Min(destination.Length, requestedSamples);
            for (int i = 0; i < sampleCount; i++)
                destination[i] += unityOutputMixBuffer[i];
            return;
        }

        int frameLimit = Mathf.Min(frameCount, requestedSamples / captureChannels);
        if (destinationChannels == 1)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                DownmixUnityCapturedFrameToStereo(unityOutputMixBuffer, frame, captureChannels, out float left, out float right);
                destination[frame] += (left + right) * 0.5f;
            }
            return;
        }

        if (destinationChannels >= 2)
        {
            for (int frame = 0; frame < frameLimit; frame++)
            {
                DownmixUnityCapturedFrameToStereo(unityOutputMixBuffer, frame, captureChannels, out float left, out float right);
                int destinationIndex = frame * destinationChannels;
                if (destinationIndex >= destination.Length)
                    break;

                destination[destinationIndex] += left;
                if (destinationIndex + 1 < destination.Length)
                    destination[destinationIndex + 1] += right;
            }
        }
    }

    private void ProcessPortAudioBlock(float[] input, int inputChannels, int outputChannels, int frameCount, float[] output)
    {
        Interlocked.Increment(ref portAudioProcessBlockCount);
        Interlocked.Add(ref portAudioProcessFrameCount, Mathf.Max(0, frameCount));
        if (output != null)
            Array.Clear(output, 0, output.Length);

        if (input == null || output == null)
            return;

        int sampleRate = activeSampleRate > 0 ? activeSampleRate : PreferredSampleRate;
        string inputChannelMode = activeSharedInputRoute?.InputChannelMode ?? advancedRoutingOptions?.inputChannelMode ?? SharedAudioInputChannelModes.Input1;
        int safeOutputChannels = Mathf.Max(1, outputChannels);
        int processingChannels = safeOutputChannels > 1 ? 2 : 1;
        int processingSampleCount = frameCount * processingChannels;
        float[] processBuffer = GetPortAudioProcessBuffer(processingSampleCount);
        FillProcessBufferFromPortAudioInput(
            input,
            inputChannels,
            processingChannels,
            frameCount,
            processBuffer,
            processingSampleCount,
            inputChannelMode);

        CaptureRawInputMetrics(input, inputChannels, frameCount, sampleRate, inputChannelMode);
        NotifyRawInputBlockReceived(input, inputChannels, frameCount, sampleRate, inputChannelMode);

        Func<float[], int, int, int, string, bool> sharedInputBlockReceived = SharedInputBlockReceived;
        if (sharedInputRouteActive && !sharedInputSubmitDisabled && sharedInputBlockReceived != null)
        {
            try
            {
                if (!sharedInputBlockReceived(input, inputChannels, frameCount, sampleRate, inputChannelMode))
                {
                    Interlocked.Increment(ref sharedDetectorRejectedCount);
                    sharedInputSubmitDisabled = true;
                    sharedInputSubmitFailurePending = true;
                }
                else
                {
                    Interlocked.Increment(ref sharedDetectorSubmitCount);
                }
            }
            catch
            {
                Interlocked.Increment(ref sharedDetectorRejectedCount);
                sharedInputSubmitDisabled = true;
                sharedInputSubmitFailurePending = true;
            }
        }

        ProcessToneBuffer(processBuffer, processingChannels, sampleRate);
        ApplyMonitorVolumeToBuffer(processBuffer);
        MixUnifiedSongSourceTapAudio(processBuffer, processingChannels, frameCount);
        MixUnityOutputCapturedAudio(processBuffer, processingChannels, frameCount);
        ApplyUnifiedOutputLimiter(processBuffer, sampleRate, frameCount);
        CaptureProcessedAudioMetrics(processBuffer, processingChannels, frameCount, sampleRate, safeOutputChannels);
        AppendUnityRecorderCaptureSamples(processBuffer, processingSampleCount);
        FillPortAudioOutputBufferFromProcessedAudio(processBuffer, processingChannels, frameCount, output, safeOutputChannels);
    }

    private void ResetLiveAudioDiagnosticsBurst()
    {
        liveAudioDiagnosticsBurstLogsRemaining = 6;
        nextLiveAudioDiagnosticsLogTime = -1f;
        latestRawInputPeak = 0f;
        latestRawInputRms = 0f;
        latestRawInputDc = 0f;
        latestRawInputUnclampedPeak = 0f;
        latestRawInputClipPercent = 0f;
        latestRawInputNonFiniteSamples = 0;
        latestProcessedPeak = 0f;
        latestProcessedRms = 0f;
        latestProcessedDc = 0f;
        latestProcessedUnclampedPeak = 0f;
        latestProcessedClipPercent = 0f;
        latestProcessedNonFiniteSamples = 0;
        Volatile.Write(ref portAudioProcessBlockCount, 0L);
        Volatile.Write(ref portAudioProcessFrameCount, 0L);
        Volatile.Write(ref sharedDetectorSubmitCount, 0L);
        Volatile.Write(ref sharedDetectorRejectedCount, 0L);
    }

    private void CaptureRawInputMetrics(float[] input, int inputChannels, int frameCount, int sampleRate, string inputChannelMode)
    {
        ComputeSelectedInputMetrics(
            input,
            inputChannels,
            frameCount,
            inputChannelMode,
            out latestRawInputPeak,
            out latestRawInputRms,
            out latestRawInputDc,
            out latestRawInputUnclampedPeak,
            out latestRawInputClipPercent,
            out latestRawInputNonFiniteSamples);
        latestAudioDiagnosticsSampleRate = sampleRate;
        latestAudioDiagnosticsInputChannels = Mathf.Max(1, inputChannels);
        latestAudioDiagnosticsFrameCount = Mathf.Max(0, frameCount);
        latestAudioDiagnosticsInputChannelMode = SharedAudioInputChannelModes.Normalize(inputChannelMode);
    }

    private void CaptureProcessedAudioMetrics(float[] data, int channels, int frameCount, int sampleRate, int outputChannels)
    {
        ComputeInterleavedMetrics(
            data,
            channels,
            frameCount,
            out latestProcessedPeak,
            out latestProcessedRms,
            out latestProcessedDc,
            out latestProcessedUnclampedPeak,
            out latestProcessedClipPercent,
            out latestProcessedNonFiniteSamples);
        latestAudioDiagnosticsSampleRate = sampleRate;
        latestAudioDiagnosticsOutputChannels = Mathf.Max(1, outputChannels);
        latestAudioDiagnosticsFrameCount = Mathf.Max(0, frameCount);
    }

    private void LogLiveAudioDiagnosticsIfDue()
    {
        if (!monitoring && !awaitingMicrophoneStart)
            return;
        if (Time.unscaledTime < nextLiveAudioDiagnosticsLogTime)
            return;

        nextLiveAudioDiagnosticsLogTime = Time.unscaledTime + LiveAudioDiagnosticsIntervalSeconds;
        bool rawInputLooksBad =
            IsRawInputClippingLikely() ||
            latestRawInputRms >= 0.35f ||
            Mathf.Abs(latestRawInputDc) >= 0.08f;
        bool processedLooksBad =
            IsProcessedOutputClippingLikely() ||
            latestProcessedRms >= 0.35f ||
            Mathf.Abs(latestProcessedDc) >= 0.08f;
        bool quietInput = latestRawInputPeak < 0.05f && latestRawInputRms < 0.01f;
        bool suspicious = rawInputLooksBad || (processedLooksBad && quietInput);

        if (liveAudioDiagnosticsBurstLogsRemaining <= 0 && !suspicious)
            return;

        if (liveAudioDiagnosticsBurstLogsRemaining > 0)
            liveAudioDiagnosticsBurstLogsRemaining--;

        string presetName = GetCurrentDiagnosticPresetName();
        string chainSummary = GetCurrentDiagnosticChainSummary();
        string streamSummary = GetPortAudioDiagnosticSummary();
        Debug.Log(
            $"[ToneLabAudio] Live levels | rawPeak={latestRawInputPeak:0.0000}, rawRms={latestRawInputRms:0.0000}, rawDc={latestRawInputDc:0.0000}, rawUnclampedPeak={latestRawInputUnclampedPeak:0.0000}, rawClip={latestRawInputClipPercent:0.###}%, rawBadSamples={latestRawInputNonFiniteSamples} | " +
            $"processedPeak={latestProcessedPeak:0.0000}, processedRms={latestProcessedRms:0.0000}, processedDc={latestProcessedDc:0.0000}, processedUnclampedPeak={latestProcessedUnclampedPeak:0.0000}, processedClip={latestProcessedClipPercent:0.###}%, processedBadSamples={latestProcessedNonFiniteSamples} | " +
            $"sampleRate={latestAudioDiagnosticsSampleRate}, frames={latestAudioDiagnosticsFrameCount}, inputChannels={latestAudioDiagnosticsInputChannels}, outputChannels={latestAudioDiagnosticsOutputChannels}, inputMode={latestAudioDiagnosticsInputChannelMode} | " +
            $"processBlocks={Volatile.Read(ref portAudioProcessBlockCount)}, processFrames={Volatile.Read(ref portAudioProcessFrameCount)}, sharedDetectorActive={sharedInputRouteActive}, sharedDetectorSubmits={Volatile.Read(ref sharedDetectorSubmitCount)}, sharedDetectorRejects={Volatile.Read(ref sharedDetectorRejectedCount)} | " +
            $"preset={presetName}, monitor={monitorVolumePercent:0.#}%, globalIn={(settings?.global_input_trim_db ?? 0f):0.#} dB, globalOut={(settings?.global_output_gain_db ?? 0f):0.#} dB, chain={chainSummary} | {streamSummary}");
    }

    private bool IsRawInputClippingLikely()
    {
        return latestRawInputUnclampedPeak >= 0.999f ||
               latestRawInputClipPercent > 0f ||
               latestRawInputNonFiniteSamples > 0;
    }

    private bool IsProcessedOutputClippingLikely()
    {
        return latestProcessedUnclampedPeak >= 0.999f ||
               latestProcessedClipPercent > 0f ||
               latestProcessedNonFiniteSamples > 0;
    }

    private string GetPortAudioDiagnosticSummary()
    {
        if (!usingPortAudioBackend)
            return "stream=not-portaudio";

        if (portAudioSplitStream != null && portAudioSplitStream.IsRunning)
            return portAudioSplitStream.GetDiagnosticSummary();
        if (portAudioStream != null && portAudioStream.IsRunning)
            return portAudioStream.GetDiagnosticSummary();

        bool split = advancedRoutingOptions != null && advancedRoutingOptions.splitInputOutputEnabled;
        if (split && portAudioSplitStream != null)
            return portAudioSplitStream.GetDiagnosticSummary();
        return portAudioStream != null ? portAudioStream.GetDiagnosticSummary() : "portAudioStream=missing";
    }

    private string GetCurrentDiagnosticPresetName()
    {
        if (playbackPresetOverrideActive && playbackPresetOverride != null)
            return string.IsNullOrWhiteSpace(playbackPresetOverride.preset_name) ? "Playback override" : playbackPresetOverride.preset_name;

        ToneLabPreset preset = FindPreset(settings, settings?.selected_preset_id);
        if (preset != null && !string.IsNullOrWhiteSpace(preset.preset_name))
            return preset.preset_name;

        return string.IsNullOrWhiteSpace(settings?.selected_preset_id) ? "None" : settings.selected_preset_id;
    }

    private string GetCurrentDiagnosticChainSummary()
    {
        ToneLabPedalSlot[] chain = playbackPresetOverrideActive && playbackPresetOverride?.pedal_chain != null
            ? playbackPresetOverride.pedal_chain.ToArray()
            : settings?.pedal_chain?.ToArray();
        if (chain == null || chain.Length == 0)
            return "empty";

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < chain.Length; i++)
        {
            ToneLabPedalSlot slot = chain[i];
            if (slot == null || !slot.enabled)
                continue;

            if (builder.Length > 0)
                builder.Append(">");
            builder.Append(string.IsNullOrWhiteSpace(slot.descriptor_id) ? slot.pedal_type.ToString() : slot.descriptor_id);
        }

        return builder.Length == 0 ? "all disabled" : builder.ToString();
    }

    private ToneLabPreset GetCurrentDiagnosticPresetSnapshot()
    {
        if (playbackPresetOverrideActive && playbackPresetOverride != null)
            return ClonePreset(playbackPresetOverride);

        if (settings == null)
            return null;

        ToneLabPreset selectedPreset = FindPreset(settings, settings.selected_preset_id);
        return new ToneLabPreset
        {
            preset_id = settings.selected_preset_id ?? string.Empty,
            preset_name = !string.IsNullOrWhiteSpace(selectedPreset?.preset_name)
                ? selectedPreset.preset_name
                : "Working rig",
            input_gain_db = settings.input_gain_db,
            output_gain_db = settings.output_gain_db,
            pedal_chain = ClonePedalChain(settings.pedal_chain)
        };
    }

    private string BuildCurrentDiagnosticPedalResolutionSummary()
    {
        List<ToneLabPedalSlot> chain = playbackPresetOverrideActive && playbackPresetOverride?.pedal_chain != null
            ? playbackPresetOverride.pedal_chain
            : settings?.pedal_chain;
        if (chain == null || chain.Count == 0)
            return "  (empty)";

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < chain.Count; i++)
        {
            ToneLabPedalSlot slot = chain[i];
            if (slot == null)
            {
                builder.AppendLine($"  [{i}] null");
                continue;
            }

            builder.Append($"  [{i}] enabled={slot.enabled}, type={slot.pedal_type}, descriptorId={(string.IsNullOrWhiteSpace(slot.descriptor_id) ? "-" : slot.descriptor_id)}");
            try
            {
                IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(slot);
                builder.Append($", resolved={descriptor.DescriptorId}, displayName={descriptor.DisplayName}, resolvedType={descriptor.PedalType}, parameters={(descriptor.Parameters != null ? descriptor.Parameters.Count : 0)}");
            }
            catch (Exception ex)
            {
                builder.Append($", resolutionError={ex.GetType().Name}: {ex.Message}");
            }
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static void ComputeSelectedInputMetrics(
        float[] input,
        int inputChannels,
        int frameCount,
        string inputChannelMode,
        out float peak,
        out float rms,
        out float dc,
        out float unclampedPeak,
        out float clipPercent,
        out int nonFiniteSamples)
    {
        peak = 0f;
        rms = 0f;
        dc = 0f;
        unclampedPeak = 0f;
        clipPercent = 0f;
        nonFiniteSamples = 0;
        if (input == null || input.Length == 0 || frameCount <= 0)
            return;

        int safeInputChannels = Mathf.Max(1, inputChannels);
        string normalizedChannelMode = SharedAudioInputChannelModes.Normalize(inputChannelMode);
        bool monoMix = string.Equals(normalizedChannelMode, SharedAudioInputChannelModes.MonoMix, StringComparison.Ordinal);
        int sourceChannel = string.Equals(normalizedChannelMode, SharedAudioInputChannelModes.Input2, StringComparison.Ordinal) && safeInputChannels > 1
            ? 1
            : 0;
        double sum = 0d;
        double energy = 0d;
        int count = 0;
        int clippedSamples = 0;

        for (int frame = 0; frame < frameCount; frame++)
        {
            int inputFrameStart = frame * safeInputChannels;
            if (inputFrameStart >= input.Length)
                break;

            float monoFallback = input[inputFrameStart];
            float sample = monoMix
                ? MixPortAudioInputFrameToMono(input, inputFrameStart, safeInputChannels)
                : ReadPortAudioInputChannel(input, inputFrameStart, sourceChannel, monoFallback);
            if (float.IsNaN(sample) || float.IsInfinity(sample))
            {
                nonFiniteSamples++;
                sample = 0f;
            }

            float unclampedAbsolute = Mathf.Abs(sample);
            if (unclampedAbsolute > unclampedPeak)
                unclampedPeak = unclampedAbsolute;
            if (unclampedAbsolute >= 0.999f)
                clippedSamples++;

            sample = Mathf.Clamp(sample, -1f, 1f);
            float absolute = Mathf.Abs(sample);
            if (absolute > peak)
                peak = absolute;
            sum += sample;
            energy += sample * sample;
            count++;
        }

        if (count <= 0)
            return;

        rms = Mathf.Sqrt((float)(energy / count));
        dc = (float)(sum / count);
        clipPercent = clippedSamples * 100f / count;
    }

    private static void ComputeInterleavedMetrics(
        float[] data,
        int channels,
        int frameCount,
        out float peak,
        out float rms,
        out float dc,
        out float unclampedPeak,
        out float clipPercent,
        out int nonFiniteSamples)
    {
        peak = 0f;
        rms = 0f;
        dc = 0f;
        unclampedPeak = 0f;
        clipPercent = 0f;
        nonFiniteSamples = 0;
        if (data == null || data.Length == 0 || frameCount <= 0)
            return;

        int sampleCount = Mathf.Min(data.Length, frameCount * Mathf.Max(1, channels));
        double sum = 0d;
        double energy = 0d;
        int clippedSamples = 0;
        for (int i = 0; i < sampleCount; i++)
        {
            float sample = data[i];
            if (float.IsNaN(sample) || float.IsInfinity(sample))
            {
                nonFiniteSamples++;
                sample = 0f;
            }

            float unclampedAbsolute = Mathf.Abs(sample);
            if (unclampedAbsolute > unclampedPeak)
                unclampedPeak = unclampedAbsolute;
            if (unclampedAbsolute >= 0.999f)
                clippedSamples++;

            sample = Mathf.Clamp(sample, -1f, 1f);
            float absolute = Mathf.Abs(sample);
            if (absolute > peak)
                peak = absolute;
            sum += sample;
            energy += sample * sample;
        }

        if (sampleCount <= 0)
            return;

        rms = Mathf.Sqrt((float)(energy / sampleCount));
        dc = (float)(sum / sampleCount);
        clipPercent = clippedSamples * 100f / sampleCount;
    }

    private void ApplyUnifiedOutputLimiter(float[] data, int sampleRate, int frameCount)
    {
        if ((unifiedSongSourceTapSnapshot == null || unifiedSongSourceTapSnapshot.Length == 0) &&
            !unityOutputCaptureActive)
        {
            unityOutputLimiterGain = 1f;
            return;
        }

        if (data == null || data.Length == 0)
            return;

        float peak = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float absolute = Mathf.Abs(data[i]);
            if (absolute > peak)
                peak = absolute;
        }

        float targetGain = peak > 0.98f ? 0.98f / peak : 1f;
        float blockDurationSeconds = sampleRate > 0 && frameCount > 0
            ? frameCount / (float)sampleRate
            : 0.01f;
        float attackBlend = 1f - Mathf.Exp(-blockDurationSeconds / 0.003f);
        float releaseBlend = 1f - Mathf.Exp(-blockDurationSeconds / 0.080f);
        float blend = targetGain < unityOutputLimiterGain ? attackBlend : releaseBlend;
        unityOutputLimiterGain = Mathf.Lerp(unityOutputLimiterGain, targetGain, Mathf.Clamp01(blend));

        if (unityOutputLimiterGain >= 0.9995f && targetGain >= 0.9995f)
        {
            unityOutputLimiterGain = 1f;
            return;
        }

        for (int i = 0; i < data.Length; i++)
            data[i] = Mathf.Clamp(data[i] * unityOutputLimiterGain, -1f, 1f);
    }

    private static void ClampSettings(ToneLabSettings toneSettings)
    {
        if (toneSettings == null)
            return;

        toneSettings.monitoring_buffer_size = ResolveMonitoringBufferSize(toneSettings.monitoring_buffer_size);
        EnsurePresetLibrary(toneSettings);
        EnsurePedalChain(toneSettings);
        toneSettings.global_input_trim_db = Mathf.Clamp(toneSettings.global_input_trim_db, MinGlobalInputTrimDb, MaxGlobalInputTrimDb);
        toneSettings.global_output_gain_db = Mathf.Clamp(toneSettings.global_output_gain_db, MinGlobalOutputGainDb, MaxGlobalOutputGainDb);
        toneSettings.input_gain_db = Mathf.Clamp(toneSettings.input_gain_db, MinRigGainDb, MaxRigGainDb);
        toneSettings.output_gain_db = Mathf.Clamp(toneSettings.output_gain_db, MinRigGainDb, MaxRigGainDb);
        toneSettings.dist_drive_db = Mathf.Clamp(toneSettings.dist_drive_db, 0f, 36f);
        toneSettings.chorus_rate_hz = Mathf.Clamp(toneSettings.chorus_rate_hz, 0.1f, 4f);
        toneSettings.chorus_depth = Mathf.Clamp01(toneSettings.chorus_depth);
        toneSettings.chorus_mix = Mathf.Clamp01(toneSettings.chorus_mix);
        toneSettings.phaser_rate_hz = Mathf.Clamp(toneSettings.phaser_rate_hz, 0.1f, 3f);
        toneSettings.phaser_depth = Mathf.Clamp01(toneSettings.phaser_depth);
        toneSettings.phaser_mix = Mathf.Clamp01(toneSettings.phaser_mix);
        toneSettings.phaser_center_hz = Mathf.Clamp(toneSettings.phaser_center_hz, 120f, 4200f);
        toneSettings.phaser_feedback = Mathf.Clamp(toneSettings.phaser_feedback, -0.9f, 0.9f);
        toneSettings.delay_seconds = Mathf.Clamp(toneSettings.delay_seconds, 0.02f, MaxDelayMilliseconds / 1000f);
        toneSettings.delay_feedback = Mathf.Clamp(toneSettings.delay_feedback, 0f, 0.95f);
        toneSettings.delay_mix = Mathf.Clamp01(toneSettings.delay_mix);
        toneSettings.reverb_room_size = Mathf.Clamp01(toneSettings.reverb_room_size);
        toneSettings.reverb_damping = Mathf.Clamp01(toneSettings.reverb_damping);
        toneSettings.reverb_wet = Mathf.Clamp01(toneSettings.reverb_wet);
        toneSettings.reverb_dry = Mathf.Clamp01(toneSettings.reverb_dry);
        toneSettings.reverb_width = Mathf.Clamp01(toneSettings.reverb_width);
        toneSettings.reverb_freeze = Mathf.Clamp01(toneSettings.reverb_freeze);
        toneSettings.comp_threshold_db = Mathf.Clamp(toneSettings.comp_threshold_db, -60f, 0f);
        toneSettings.comp_ratio = Mathf.Clamp(toneSettings.comp_ratio, 1f, 8f);
        toneSettings.comp_attack_ms = Mathf.Clamp(toneSettings.comp_attack_ms, 1f, 120f);
        toneSettings.comp_release_ms = Mathf.Clamp(toneSettings.comp_release_ms, 20f, 600f);
    }

    private void RebuildCompiledPedalChain()
    {
        if (!settingsLoaded || settings == null)
        {
            compiledPedalChain = Array.Empty<CompiledPedalSlot>();
            return;
        }

        List<ToneLabPedalSlot> sourceChain;
        if (playbackPresetOverrideActive && playbackPresetOverride != null)
        {
            sourceChain = playbackPresetOverride.pedal_chain ?? new List<ToneLabPedalSlot>();
        }
        else
        {
            EnsurePedalChain(settings);
            sourceChain = settings.pedal_chain ?? new List<ToneLabPedalSlot>();
        }

        List<CompiledPedalSlot> rebuiltChain = new List<CompiledPedalSlot>(sourceChain.Count);
        for (int i = 0; i < sourceChain.Count; i++)
        {
            ToneLabPedalSlot slot = sourceChain[i];
            if (slot == null)
                continue;

            IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(slot);
            object settingsObject = descriptor.DeserializeSettingsObject(slot.settings_json);
            IToneLabPedalProcessor processor = descriptor.CreateProcessor();
            processor.ApplySettings(settingsObject);
            if (preparedCompiledPedalSampleRate > 0 && preparedCompiledPedalChannelCount > 0)
            {
                processor.Prepare(preparedCompiledPedalSampleRate, preparedCompiledPedalChannelCount);
                processor.Reset();
            }

            rebuiltChain.Add(new CompiledPedalSlot
            {
                slot = ClonePedalSlot(slot),
                descriptor = descriptor,
                processor = processor
            });
        }

        compiledPedalChain = rebuiltChain.ToArray();
    }

    private void PrepareCompiledPedalChainIfNeeded(int sampleRate, int channels)
    {
        int safeSampleRate = Mathf.Max(1, sampleRate);
        int safeChannels = Mathf.Max(1, channels);
        if (safeSampleRate == preparedCompiledPedalSampleRate && safeChannels == preparedCompiledPedalChannelCount)
            return;

        CompiledPedalSlot[] chain = compiledPedalChain;
        for (int i = 0; i < chain.Length; i++)
        {
            chain[i].processor?.Prepare(safeSampleRate, safeChannels);
            chain[i].processor?.Reset();
        }

        preparedCompiledPedalSampleRate = safeSampleRate;
        preparedCompiledPedalChannelCount = safeChannels;
    }

    private void ApplyCompiledPedalSettings(ToneLabPedalSlot slot, IToneLabPedalDescriptor descriptor, object settingsObject)
    {
        if (slot == null || descriptor == null)
            return;

        CompiledPedalSlot[] chain = compiledPedalChain;
        for (int i = 0; i < chain.Length; i++)
        {
            CompiledPedalSlot compiledSlot = chain[i];
            if (compiledSlot?.slot == null ||
                !string.Equals(compiledSlot.slot.pedal_instance_id, slot.pedal_instance_id, StringComparison.Ordinal))
            {
                continue;
            }

            compiledSlot.slot = ClonePedalSlot(slot);
            compiledSlot.descriptor = descriptor;
            compiledSlot.processor?.ApplySettings(settingsObject);
            return;
        }
    }

    private static ToneLabPreset ClonePreset(ToneLabPreset preset)
    {
        if (preset == null)
            return null;

        return new ToneLabPreset
        {
            preset_id = preset.preset_id ?? string.Empty,
            preset_name = preset.preset_name ?? string.Empty,
            input_gain_db = preset.input_gain_db,
            output_gain_db = preset.output_gain_db,
            pedal_chain = ClonePedalChain(preset.pedal_chain)
        };
    }

    private static List<ToneLabPedalSlot> ClonePedalChain(IReadOnlyList<ToneLabPedalSlot> pedalChain)
    {
        List<ToneLabPedalSlot> cloned = new List<ToneLabPedalSlot>(pedalChain?.Count ?? 0);
        if (pedalChain == null)
            return cloned;

        for (int i = 0; i < pedalChain.Count; i++)
        {
            ToneLabPedalSlot slot = ClonePedalSlot(pedalChain[i]);
            if (slot != null)
                cloned.Add(slot);
        }

        return cloned;
    }

    private static ToneLabPedalSlot ClonePedalSlot(ToneLabPedalSlot slot)
    {
        if (slot == null)
            return null;

        return new ToneLabPedalSlot
        {
            pedal_instance_id = slot.pedal_instance_id ?? string.Empty,
            pedal_type = slot.pedal_type,
            descriptor_id = slot.descriptor_id ?? string.Empty,
            enabled = slot.enabled,
            settings_json = slot.settings_json ?? string.Empty
        };
    }

    private static void EnsurePresetLibrary(ToneLabSettings toneSettings)
    {
        if (toneSettings == null)
            return;

        List<ToneLabPreset> presets = toneSettings.presets ?? new List<ToneLabPreset>();
        if (presets.Count == 0)
        {
            toneSettings.presets = CreateDefaultPresets();
        }
        else
        {
            List<ToneLabPreset> normalizedPresets = new List<ToneLabPreset>(presets.Count);
            HashSet<string> presetIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> presetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < presets.Count; i++)
            {
                ToneLabPreset normalized = NormalizePreset(presets[i], presetIds, presetNames);
                if (normalized != null)
                    normalizedPresets.Add(normalized);
            }

            toneSettings.presets = normalizedPresets;
            if (toneSettings.presets.Count == 0)
                toneSettings.presets = CreateDefaultPresets();
        }

        UpgradeLegacyFactoryPresets(toneSettings);
        AddMissingFactoryPresets(toneSettings);

        ToneLabPreset selectedPreset = FindPreset(toneSettings, toneSettings.selected_preset_id);
        if (selectedPreset == null)
        {
            ToneLabPreset defaultPreset = GetDefaultPreset(toneSettings);
            if (defaultPreset != null)
                ApplyPresetToSettings(toneSettings, defaultPreset);
        }
    }

    private static void EnsurePedalChain(ToneLabSettings toneSettings)
    {
        if (toneSettings == null)
            return;

        List<ToneLabPedalSlot> existing = toneSettings.pedal_chain ?? new List<ToneLabPedalSlot>();
        List<ToneLabPedalSlot> normalized = new List<ToneLabPedalSlot>(existing.Count);

        for (int i = 0; i < existing.Count; i++)
        {
            ToneLabPedalSlot slot = existing[i];
            if (slot == null)
                continue;

            normalized.Add(NormalizePedalSlot(toneSettings, slot));
        }

        if (normalized.Count == 0)
        {
            for (int i = 0; i < DefaultPedalOrder.Length; i++)
            {
                ToneLabPedalType pedalType = DefaultPedalOrder[i];
                if (!GetLegacyPedalEnabled(toneSettings, pedalType))
                    continue;

                normalized.Add(CreatePedalSlotFromLegacy(toneSettings, pedalType));
            }
        }

        toneSettings.pedal_chain = normalized;
        SyncLegacySettingsFromChain(toneSettings);
    }

    private static ToneLabPreset NormalizePreset(ToneLabPreset preset, HashSet<string> presetIds, HashSet<string> presetNames)
    {
        if (preset == null)
            return null;

        ToneLabPreset normalized = ClonePreset(preset);
        normalized.preset_id = string.IsNullOrWhiteSpace(normalized.preset_id) || !presetIds.Add(normalized.preset_id)
            ? CreatePresetId()
            : normalized.preset_id;
        normalized.preset_name = MakeUniquePresetName(NormalizePresetName(normalized.preset_name), presetNames);
        normalized.input_gain_db = Mathf.Clamp(normalized.input_gain_db, MinRigGainDb, MaxRigGainDb);
        normalized.output_gain_db = Mathf.Clamp(normalized.output_gain_db, MinRigGainDb, MaxRigGainDb);

        List<ToneLabPedalSlot> sourceChain = normalized.pedal_chain ?? new List<ToneLabPedalSlot>();
        List<ToneLabPedalSlot> normalizedChain = new List<ToneLabPedalSlot>(sourceChain.Count);
        for (int i = 0; i < sourceChain.Count; i++)
        {
            ToneLabPedalSlot slot = NormalizePresetPedalSlot(sourceChain[i]);
            if (slot != null)
                normalizedChain.Add(slot);
        }

        normalized.pedal_chain = normalizedChain;
        return normalized;
    }

    private static string NormalizePresetName(string presetName)
    {
        string name = string.IsNullOrWhiteSpace(presetName) ? "New Preset" : presetName.Trim();
        if (name.StartsWith("RS ", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(3).TrimStart();

        return string.IsNullOrWhiteSpace(name) ? "New Preset" : name;
    }

    private static ToneLabPedalSlot NormalizePedalSlot(ToneLabSettings toneSettings, ToneLabPedalSlot slot)
    {
        ToneLabPedalSlot normalized = ClonePedalSlot(slot);
        if (normalized == null)
            return null;

        IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(normalized);
        if (string.IsNullOrWhiteSpace(normalized.pedal_instance_id))
            normalized.pedal_instance_id = CreatePedalInstanceId();
        if (string.IsNullOrWhiteSpace(normalized.descriptor_id))
            normalized.descriptor_id = descriptor.DescriptorId;
        else if (normalized.pedal_type == ToneLabPedalType.NamModel || normalized.pedal_type == ToneLabPedalType.Lv2Plugin)
            normalized.descriptor_id = descriptor.DescriptorId;

        if (string.IsNullOrWhiteSpace(normalized.settings_json))
        {
            object legacySettings = CreateLegacySettingsObject(toneSettings, normalized.pedal_type);
            normalized.settings_json = descriptor.SerializeSettingsObject(legacySettings ?? descriptor.CreateDefaultSettingsObject());
        }

        normalized.settings_json = ClampPedalSettingsJson(descriptor, normalized.settings_json);

        return normalized;
    }

    private static ToneLabPedalSlot NormalizePresetPedalSlot(ToneLabPedalSlot slot)
    {
        ToneLabPedalSlot normalized = ClonePedalSlot(slot);
        if (normalized == null)
            return null;

        IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(normalized);
        if (string.IsNullOrWhiteSpace(normalized.pedal_instance_id))
            normalized.pedal_instance_id = CreatePedalInstanceId();
        if (string.IsNullOrWhiteSpace(normalized.descriptor_id))
            normalized.descriptor_id = descriptor.DescriptorId;
        else if (normalized.pedal_type == ToneLabPedalType.NamModel || normalized.pedal_type == ToneLabPedalType.Lv2Plugin)
            normalized.descriptor_id = descriptor.DescriptorId;
        if (string.IsNullOrWhiteSpace(normalized.settings_json))
            normalized.settings_json = descriptor.SerializeSettingsObject(descriptor.CreateDefaultSettingsObject());

        normalized.settings_json = ClampPedalSettingsJson(descriptor, normalized.settings_json);
        return normalized;
    }

    private static ToneLabPedalSlot CreatePedalSlotFromLegacy(ToneLabSettings toneSettings, ToneLabPedalType pedalType)
    {
        IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(pedalType);
        object legacySettings = CreateLegacySettingsObject(toneSettings, pedalType);
        return new ToneLabPedalSlot
        {
            pedal_instance_id = CreatePedalInstanceId(),
            pedal_type = pedalType,
            descriptor_id = descriptor.DescriptorId,
            enabled = GetLegacyPedalEnabled(toneSettings, pedalType),
            settings_json = descriptor.SerializeSettingsObject(legacySettings ?? descriptor.CreateDefaultSettingsObject())
        };
    }

    private static object CreateLegacySettingsObject(ToneLabSettings toneSettings, ToneLabPedalType pedalType)
    {
        switch (pedalType)
        {
            case ToneLabPedalType.Distortion:
                return new DistortionPedalSettings
                {
                    drive_db = toneSettings.dist_drive_db
                };
            case ToneLabPedalType.Chorus:
                return new ChorusPedalSettings
                {
                    rate_hz = toneSettings.chorus_rate_hz,
                    depth = toneSettings.chorus_depth,
                    mix = toneSettings.chorus_mix
                };
            case ToneLabPedalType.Phaser:
                return new PhaserPedalSettings
                {
                    rate_hz = toneSettings.phaser_rate_hz,
                    depth = toneSettings.phaser_depth,
                    mix = toneSettings.phaser_mix,
                    center_hz = toneSettings.phaser_center_hz,
                    feedback = toneSettings.phaser_feedback
                };
            case ToneLabPedalType.Delay:
                return new DelayPedalSettings
                {
                    delay_seconds = toneSettings.delay_seconds,
                    feedback = toneSettings.delay_feedback,
                    mix = toneSettings.delay_mix
                };
            case ToneLabPedalType.Reverb:
                return new ReverbPedalSettings
                {
                    room_size = toneSettings.reverb_room_size,
                    damping = toneSettings.reverb_damping,
                    wet = toneSettings.reverb_wet,
                    dry = toneSettings.reverb_dry,
                    width = toneSettings.reverb_width,
                    freeze = toneSettings.reverb_freeze
                };
            case ToneLabPedalType.Compressor:
                return new CompressorPedalSettings
                {
                    threshold_db = toneSettings.comp_threshold_db,
                    ratio = toneSettings.comp_ratio,
                    attack_ms = toneSettings.comp_attack_ms,
                    release_ms = toneSettings.comp_release_ms
                };
            default:
                return null;
        }
    }

    private static void ApplyLegacySettingsFromSlot(ToneLabSettings toneSettings, ToneLabPedalSlot slot)
    {
        if (toneSettings == null || slot == null)
            return;

        IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(slot);
        object settingsObject = descriptor.DeserializeSettingsObject(slot.settings_json);
        switch (slot.pedal_type)
        {
            case ToneLabPedalType.Distortion:
                DistortionPedalSettings distortion = settingsObject as DistortionPedalSettings;
                if (distortion != null)
                    toneSettings.dist_drive_db = distortion.drive_db;
                break;
            case ToneLabPedalType.Chorus:
                ChorusPedalSettings chorus = settingsObject as ChorusPedalSettings;
                if (chorus != null)
                {
                    toneSettings.chorus_rate_hz = chorus.rate_hz;
                    toneSettings.chorus_depth = chorus.depth;
                    toneSettings.chorus_mix = chorus.mix;
                }
                break;
            case ToneLabPedalType.Phaser:
                PhaserPedalSettings phaser = settingsObject as PhaserPedalSettings;
                if (phaser != null)
                {
                    toneSettings.phaser_rate_hz = phaser.rate_hz;
                    toneSettings.phaser_depth = phaser.depth;
                    toneSettings.phaser_mix = phaser.mix;
                    toneSettings.phaser_center_hz = phaser.center_hz;
                    toneSettings.phaser_feedback = phaser.feedback;
                }
                break;
            case ToneLabPedalType.Delay:
                DelayPedalSettings delay = settingsObject as DelayPedalSettings;
                if (delay != null)
                {
                    toneSettings.delay_seconds = delay.delay_seconds;
                    toneSettings.delay_feedback = delay.feedback;
                    toneSettings.delay_mix = delay.mix;
                }
                break;
            case ToneLabPedalType.Reverb:
                ReverbPedalSettings reverb = settingsObject as ReverbPedalSettings;
                if (reverb != null)
                {
                    toneSettings.reverb_room_size = reverb.room_size;
                    toneSettings.reverb_damping = reverb.damping;
                    toneSettings.reverb_wet = reverb.wet;
                    toneSettings.reverb_dry = reverb.dry;
                    toneSettings.reverb_width = reverb.width;
                    toneSettings.reverb_freeze = reverb.freeze;
                }
                break;
            case ToneLabPedalType.Compressor:
                CompressorPedalSettings compressor = settingsObject as CompressorPedalSettings;
                if (compressor != null)
                {
                    toneSettings.comp_threshold_db = compressor.threshold_db;
                    toneSettings.comp_ratio = compressor.ratio;
                    toneSettings.comp_attack_ms = compressor.attack_ms;
                    toneSettings.comp_release_ms = compressor.release_ms;
                }
                break;
        }
    }

    private static void ApplyPedalOrder(ToneLabSettings toneSettings, IReadOnlyList<string> orderedPedalInstanceIds)
    {
        EnsurePedalChain(toneSettings);
        if (orderedPedalInstanceIds == null || orderedPedalInstanceIds.Count == 0)
            return;

        List<ToneLabPedalSlot> reordered = new List<ToneLabPedalSlot>(toneSettings.pedal_chain.Count);
        HashSet<string> added = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < orderedPedalInstanceIds.Count; i++)
        {
            ToneLabPedalSlot slot = FindPedalSlot(toneSettings, orderedPedalInstanceIds[i]);
            if (slot == null || !added.Add(slot.pedal_instance_id))
                continue;

            reordered.Add(ClonePedalSlot(slot));
        }

        for (int i = 0; i < toneSettings.pedal_chain.Count; i++)
        {
            ToneLabPedalSlot slot = toneSettings.pedal_chain[i];
            if (slot == null || !added.Add(slot.pedal_instance_id))
                continue;

            reordered.Add(ClonePedalSlot(slot));
        }

        toneSettings.pedal_chain = reordered;
        SyncLegacySettingsFromChain(toneSettings);
    }

    private static ToneLabPedalSlot FindPedalSlot(ToneLabSettings toneSettings, string pedalInstanceId)
    {
        if (toneSettings?.pedal_chain == null || string.IsNullOrWhiteSpace(pedalInstanceId))
            return null;

        for (int i = 0; i < toneSettings.pedal_chain.Count; i++)
        {
            ToneLabPedalSlot slot = toneSettings.pedal_chain[i];
            if (slot != null && string.Equals(slot.pedal_instance_id, pedalInstanceId, StringComparison.Ordinal))
                return slot;
        }

        return null;
    }

    private static void SyncLegacySettingsFromChain(ToneLabSettings toneSettings)
    {
        if (toneSettings == null)
            return;

        toneSettings.dist_enabled = false;
        toneSettings.chorus_enabled = false;
        toneSettings.phaser_enabled = false;
        toneSettings.delay_enabled = false;
        toneSettings.reverb_enabled = false;
        toneSettings.comp_enabled = false;

        if (toneSettings.pedal_chain == null)
            return;

        HashSet<ToneLabPedalType> parameterSynced = new HashSet<ToneLabPedalType>();
        for (int i = 0; i < toneSettings.pedal_chain.Count; i++)
        {
            ToneLabPedalSlot slot = toneSettings.pedal_chain[i];
            if (slot == null)
                continue;

            if (slot.enabled)
                SetLegacyPedalEnabled(toneSettings, slot.pedal_type, true);
            if (parameterSynced.Add(slot.pedal_type))
                ApplyLegacySettingsFromSlot(toneSettings, slot);
        }
    }

    private static string ClampPedalSettingsJson(IToneLabPedalDescriptor descriptor, string settingsJson)
    {
        if (descriptor == null)
            return string.Empty;

        object settingsObject = descriptor.DeserializeSettingsObject(settingsJson);
        IReadOnlyList<ToneLabPedalParameterDefinition> parameters = descriptor.Parameters;
        if (parameters != null)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                ToneLabPedalParameterDefinition parameter = parameters[i];
                parameter.SetValue(settingsObject, parameter.GetValue(settingsObject));
            }
        }

        return descriptor.SerializeSettingsObject(settingsObject);
    }

    private static ToneLabPreset FindPreset(ToneLabSettings toneSettings, string presetId)
    {
        if (toneSettings?.presets == null || string.IsNullOrWhiteSpace(presetId))
            return null;

        for (int i = 0; i < toneSettings.presets.Count; i++)
        {
            ToneLabPreset preset = toneSettings.presets[i];
            if (preset != null && string.Equals(preset.preset_id, presetId, StringComparison.Ordinal))
                return preset;
        }

        return null;
    }

    private static ToneLabPreset GetDefaultPreset(ToneLabSettings toneSettings)
    {
        if (toneSettings?.presets == null || toneSettings.presets.Count == 0)
            return null;

        for (int i = 0; i < toneSettings.presets.Count; i++)
        {
            ToneLabPreset preset = toneSettings.presets[i];
            if (preset != null && string.Equals(preset.preset_name, "Clean", StringComparison.OrdinalIgnoreCase))
                return preset;
        }

        for (int i = 0; i < toneSettings.presets.Count; i++)
        {
            ToneLabPreset preset = toneSettings.presets[i];
            if (preset != null)
                return preset;
        }

        return null;
    }

    private static void AddMissingFactoryPresets(ToneLabSettings toneSettings)
    {
        if (toneSettings?.presets == null)
            return;

        HashSet<string> existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < toneSettings.presets.Count; i++)
        {
            ToneLabPreset existing = toneSettings.presets[i];
            if (existing != null && !string.IsNullOrWhiteSpace(existing.preset_name))
                existingNames.Add(existing.preset_name.Trim());
        }

        List<ToneLabPreset> factoryDefaults = CreateDefaultPresets();
        for (int i = 0; i < factoryDefaults.Count; i++)
        {
            ToneLabPreset factoryPreset = factoryDefaults[i];
            if (factoryPreset == null || string.IsNullOrWhiteSpace(factoryPreset.preset_name))
                continue;

            if (existingNames.Add(factoryPreset.preset_name.Trim()))
                toneSettings.presets.Add(factoryPreset);
        }
    }

    private static void UpgradeLegacyFactoryPresets(ToneLabSettings toneSettings)
    {
        if (toneSettings?.presets == null || toneSettings.presets.Count == 0)
            return;

        List<ToneLabPreset> factoryDefaults = CreateDefaultPresets();
        Dictionary<string, ToneLabPreset> defaultsByName = new Dictionary<string, ToneLabPreset>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < factoryDefaults.Count; i++)
        {
            ToneLabPreset preset = factoryDefaults[i];
            if (preset != null && !string.IsNullOrWhiteSpace(preset.preset_name))
                defaultsByName[preset.preset_name.Trim()] = preset;
        }

        for (int i = 0; i < toneSettings.presets.Count; i++)
        {
            ToneLabPreset existing = toneSettings.presets[i];
            if (existing == null || string.IsNullOrWhiteSpace(existing.preset_name))
                continue;

            if (!defaultsByName.TryGetValue(existing.preset_name.Trim(), out ToneLabPreset factoryPreset))
                continue;

            if (!NeedsFactoryPresetUpgrade(existing))
                continue;

            ToneLabPreset upgraded = ClonePreset(factoryPreset);
            upgraded.preset_id = existing.preset_id;
            upgraded.preset_name = existing.preset_name;
            toneSettings.presets[i] = upgraded;
        }
    }

    private static bool NeedsFactoryPresetUpgrade(ToneLabPreset preset)
    {
        if (preset?.pedal_chain == null || preset.pedal_chain.Count == 0)
            return true;

        if (PresetContainsPedalType(preset, ToneLabPedalType.NamModel))
            return false;

        bool hasAmp = false;
        bool hasCabSim = false;
        bool hasStudioEq = false;
        for (int i = 0; i < preset.pedal_chain.Count; i++)
        {
            ToneLabPedalSlot slot = preset.pedal_chain[i];
            if (slot == null)
                continue;

            switch (slot.pedal_type)
            {
                case ToneLabPedalType.Amp:
                    hasAmp = true;
                    break;
                case ToneLabPedalType.CabSim:
                    hasCabSim = true;
                    break;
                case ToneLabPedalType.StudioEq:
                    hasStudioEq = true;
                    break;
            }
        }

        if (!(hasAmp && hasCabSim && hasStudioEq))
            return true;

        return LooksLikeOutdatedFactoryPreset(preset);
    }

    private static bool PresetContainsPedalType(ToneLabPreset preset, ToneLabPedalType pedalType)
    {
        if (preset?.pedal_chain == null)
            return false;

        for (int i = 0; i < preset.pedal_chain.Count; i++)
        {
            ToneLabPedalSlot slot = preset.pedal_chain[i];
            if (slot != null && slot.pedal_type == pedalType)
                return true;
        }

        return false;
    }

    private static bool LooksLikeOutdatedFactoryPreset(ToneLabPreset preset)
    {
        if (preset == null || string.IsNullOrWhiteSpace(preset.preset_name))
            return false;

        string presetName = preset.preset_name.Trim();
        switch (presetName.ToUpperInvariant())
        {
            case "CLEAN":
            {
                NoiseGatePedalSettings gate = TryGetPedalSettings<NoiseGatePedalSettings>(preset, ToneLabPedalType.NoiseGate);
                AmpPedalSettings amp = TryGetPedalSettings<AmpPedalSettings>(preset, ToneLabPedalType.Amp);
                ChorusPedalSettings chorus = TryGetPedalSettings<ChorusPedalSettings>(preset, ToneLabPedalType.Chorus);
                if (gate != null && gate.threshold_db < -60f)
                    return true;

                return MatchesApproximateFactoryValues(preset, 8f, 7.5f)
                    && amp != null
                    && chorus != null
                    && Approximately(amp.gain_db, 10.5f, 0.35f)
                    && Approximately(chorus.mix, 0.12f, 0.03f);
            }
            case "BLUES":
            {
                DistortionPedalSettings distortion = TryGetPedalSettings<DistortionPedalSettings>(preset, ToneLabPedalType.Distortion);
                AmpPedalSettings amp = TryGetPedalSettings<AmpPedalSettings>(preset, ToneLabPedalType.Amp);
                return MatchesApproximateFactoryValues(preset, 9f, 8f)
                    && distortion != null
                    && amp != null
                    && Approximately(distortion.drive_db, 8.5f, 0.35f)
                    && Approximately(amp.gain_db, 18f, 0.5f);
            }
            case "JAZZ":
            {
                AmpPedalSettings amp = TryGetPedalSettings<AmpPedalSettings>(preset, ToneLabPedalType.Amp);
                StudioEqPedalSettings eq = TryGetPedalSettings<StudioEqPedalSettings>(preset, ToneLabPedalType.StudioEq);
                return MatchesApproximateFactoryValues(preset, 8f, 7f)
                    && amp != null
                    && eq != null
                    && Approximately(amp.gain_db, 8f, 0.35f)
                    && Approximately(eq.high_shelf_db, -3.0f, 0.25f);
            }
            case "EDGY":
            {
                DistortionPedalSettings distortion = TryGetPedalSettings<DistortionPedalSettings>(preset, ToneLabPedalType.Distortion);
                AmpPedalSettings amp = TryGetPedalSettings<AmpPedalSettings>(preset, ToneLabPedalType.Amp);
                PhaserPedalSettings phaser = TryGetPedalSettings<PhaserPedalSettings>(preset, ToneLabPedalType.Phaser);
                return MatchesApproximateFactoryValues(preset, 9f, 6.5f)
                    && distortion != null
                    && amp != null
                    && phaser != null
                    && Approximately(distortion.drive_db, 13f, 0.35f)
                    && Approximately(amp.gain_db, 24f, 0.5f)
                    && Approximately(phaser.mix, 0.10f, 0.03f);
            }
            case "METAL":
            {
                DistortionPedalSettings distortion = TryGetPedalSettings<DistortionPedalSettings>(preset, ToneLabPedalType.Distortion);
                AmpPedalSettings amp = TryGetPedalSettings<AmpPedalSettings>(preset, ToneLabPedalType.Amp);
                StudioEqPedalSettings eq = TryGetPedalSettings<StudioEqPedalSettings>(preset, ToneLabPedalType.StudioEq);
                return MatchesApproximateFactoryValues(preset, 10f, 5f)
                    && distortion != null
                    && amp != null
                    && eq != null
                    && Approximately(distortion.drive_db, 16.5f, 0.35f)
                    && Approximately(amp.gain_db, 37f, 0.5f)
                    && Approximately(eq.mid_db, -4.2f, 0.3f);
            }
        }

        return false;
    }

    private static bool MatchesApproximateFactoryValues(ToneLabPreset preset, float inputGainDb, float outputGainDb)
    {
        return preset != null
            && Approximately(preset.input_gain_db, inputGainDb, 0.25f)
            && Approximately(preset.output_gain_db, outputGainDb, 0.25f);
    }

    private static bool Approximately(float a, float b, float tolerance)
    {
        return Mathf.Abs(a - b) <= Mathf.Abs(tolerance);
    }

    private static TSettings TryGetPedalSettings<TSettings>(ToneLabPreset preset, ToneLabPedalType pedalType) where TSettings : class
    {
        if (preset?.pedal_chain == null)
            return null;

        for (int i = 0; i < preset.pedal_chain.Count; i++)
        {
            ToneLabPedalSlot slot = preset.pedal_chain[i];
            if (slot == null || slot.pedal_type != pedalType)
                continue;

            IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(slot);
            return descriptor.DeserializeSettingsObject(slot.settings_json) as TSettings;
        }

        return null;
    }

    private static bool RestoreWorkingRigFromSelectedPreset(ToneLabSettings toneSettings)
    {
        if (toneSettings == null)
            return false;

        EnsurePresetLibrary(toneSettings);
        ToneLabPreset preset = FindPreset(toneSettings, toneSettings.selected_preset_id) ?? GetDefaultPreset(toneSettings);
        if (preset == null)
        {
            EnsurePedalChain(toneSettings);
            return false;
        }

        bool selectionChanged = !string.Equals(toneSettings.selected_preset_id, preset.preset_id, StringComparison.Ordinal);
        ApplyPresetToSettings(toneSettings, preset);
        return selectionChanged;
    }

    private static void ApplyPresetToSettings(ToneLabSettings toneSettings, ToneLabPreset preset)
    {
        if (toneSettings == null || preset == null)
            return;

        toneSettings.selected_preset_id = preset.preset_id ?? string.Empty;
        toneSettings.input_gain_db = Mathf.Clamp(preset.input_gain_db, MinRigGainDb, MaxRigGainDb);
        toneSettings.output_gain_db = Mathf.Clamp(preset.output_gain_db, MinRigGainDb, MaxRigGainDb);
        toneSettings.pedal_chain = ClonePedalChain(preset.pedal_chain);
        EnsurePedalChain(toneSettings);
    }

    private static ToneLabPreset CaptureCurrentPreset(ToneLabSettings toneSettings, string presetName, string presetId)
    {
        EnsurePedalChain(toneSettings);
        return new ToneLabPreset
        {
            preset_id = string.IsNullOrWhiteSpace(presetId) ? CreatePresetId() : presetId,
            preset_name = string.IsNullOrWhiteSpace(presetName) ? "New Preset" : presetName.Trim(),
            input_gain_db = Mathf.Clamp(toneSettings.input_gain_db, MinRigGainDb, MaxRigGainDb),
            output_gain_db = Mathf.Clamp(toneSettings.output_gain_db, MinRigGainDb, MaxRigGainDb),
            pedal_chain = ClonePedalChain(toneSettings.pedal_chain)
        };
    }

    private static List<ToneLabPreset> CreateDefaultPresets()
    {
        return new List<ToneLabPreset>
        {
            CreateCleanPreset(),
            CreateBluesPreset(),
            CreateJazzPreset(),
            CreateEdgyPreset(),
            CreateMetalPreset(),
            CreateLv2SessionCleanPreset(),
            CreateLv2TexasEdgePreset(),
            CreateLv2BritishCrunchPreset(),
            CreateLv2FuzzLeadPreset(),
            CreateLv2SwellWahPreset(),
            CreateLv2BoutiqueBoardPreset(),
            CreateLv2ModernDriveBoardPreset(),
            CreateLv2PsychedelicFuzzPreset(),
            CreateNamTwoRockCleanPreset(),
            CreateNamPlexiCrunchPreset(),
            CreateNamPowerballTightPreset(),
            CreateRsCleanPopPreset(),
            CreateRsIndieJanglePreset(),
            CreateRsFunkSnapPreset(),
            CreateRsCountryTwangPreset(),
            CreateRsBluesBreakupPreset(),
            CreateRsClassicCrunchPreset(),
            CreateRsPunkRhythmPreset(),
            CreateRsAltLeadPreset(),
            CreateRsMetalTightPreset(),
            CreateRsDropModernPreset(),
            CreateRsAmbientCleanPreset(),
            CreateRsShoegazeWallPreset(),
            CreateRsSurfPlatePreset(),
            CreateRsOctaveFuzzPreset(),
            CreateRsBassGrindPreset()
        };
    }

    private static ToneLabPreset CreateCleanPreset()
    {
        return BuildPreset(
            "Clean",
            inputGainDb: 12f,
            outputGainDb: 11f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -56f,
                attack_ms = 2f,
                hold_ms = 28f,
                release_ms = 110f,
                range_db = -80f
            }),
            CreatePresetSlot(ToneLabPedalType.Compressor, true, new CompressorPedalSettings
            {
                threshold_db = -28f,
                ratio = 2.4f,
                attack_ms = 16f,
                release_ms = 150f
            }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 9f,
                tone = 0.64f,
                presence = 0.48f,
                master_db = 1.8f,
                sag = 0.08f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.38f,
                presence = 0.40f,
                air = 0.66f,
                mix = 1.0f
            }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 68f,
                low_shelf_db = -0.3f,
                mid_db = -0.8f,
                high_shelf_db = 0.9f,
                high_cut_hz = 8400f
            }),
            CreatePresetSlot(ToneLabPedalType.Chorus, true, new ChorusPedalSettings
            {
                rate_hz = 0.45f,
                depth = 0.26f,
                mix = 0.18f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.12f,
                feedback = 0.14f,
                mix = 0.12f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.28f,
                damping = 0.52f,
                wet = 0.13f,
                dry = 1.00f,
                width = 0.96f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateBluesPreset()
    {
        return BuildPreset(
            "Blues",
            inputGainDb: 13.5f,
            outputGainDb: 12f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -64f,
                attack_ms = 1.2f,
                hold_ms = 22f,
                release_ms = 115f,
                range_db = -72f
            }),
            CreatePresetSlot(ToneLabPedalType.Compressor, true, new CompressorPedalSettings
            {
                threshold_db = -25f,
                ratio = 2.6f,
                attack_ms = 16f,
                release_ms = 155f
            }),
            CreatePresetSlot(ToneLabPedalType.Distortion, true, new DistortionPedalSettings
            {
                drive_db = 11.5f
            }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 22f,
                tone = 0.52f,
                presence = 0.48f,
                master_db = 1.1f,
                sag = 0.16f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.56f,
                presence = 0.46f,
                air = 0.40f,
                mix = 1f
            }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 78f,
                low_shelf_db = 1.1f,
                mid_db = 1.6f,
                high_shelf_db = -0.6f,
                high_cut_hz = 7100f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.15f,
                feedback = 0.18f,
                mix = 0.11f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.20f,
                damping = 0.46f,
                wet = 0.10f,
                dry = 1.00f,
                width = 0.90f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateJazzPreset()
    {
        return BuildPreset(
            "Jazz",
            inputGainDb: 11.5f,
            outputGainDb: 10.5f,
            CreatePresetSlot(ToneLabPedalType.Compressor, true, new CompressorPedalSettings
            {
                threshold_db = -21f,
                ratio = 2.8f,
                attack_ms = 24f,
                release_ms = 210f
            }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 6.5f,
                tone = 0.34f,
                presence = 0.22f,
                master_db = 1.4f,
                sag = 0.07f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.46f,
                presence = 0.24f,
                air = 0.32f,
                mix = 1f
            }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 70f,
                low_shelf_db = 2.2f,
                mid_db = -1.2f,
                high_shelf_db = -3.6f,
                high_cut_hz = 5000f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.09f,
                feedback = 0.08f,
                mix = 0.04f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.24f,
                damping = 0.62f,
                wet = 0.10f,
                dry = 1.00f,
                width = 0.84f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateEdgyPreset()
    {
        return BuildPreset(
            "Edgy",
            inputGainDb: 14f,
            outputGainDb: 11f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -56f,
                attack_ms = 1.0f,
                hold_ms = 24f,
                release_ms = 105f,
                range_db = -78f
            }),
            CreatePresetSlot(ToneLabPedalType.Compressor, true, new CompressorPedalSettings
            {
                threshold_db = -25f,
                ratio = 2.5f,
                attack_ms = 10f,
                release_ms = 110f
            }),
            CreatePresetSlot(ToneLabPedalType.Distortion, true, new DistortionPedalSettings
            {
                drive_db = 16.5f
            }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 28.5f,
                tone = 0.60f,
                presence = 0.64f,
                master_db = 0.7f,
                sag = 0.22f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.62f,
                presence = 0.54f,
                air = 0.42f,
                mix = 1f
            }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 86f,
                low_shelf_db = 0.2f,
                mid_db = 1.1f,
                high_shelf_db = 1.1f,
                high_cut_hz = 7400f
            }),
            CreatePresetSlot(ToneLabPedalType.Phaser, true, new PhaserPedalSettings
            {
                rate_hz = 0.34f,
                depth = 0.26f,
                mix = 0.14f,
                center_hz = 980f,
                feedback = 0.06f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.24f,
                feedback = 0.22f,
                mix = 0.12f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.16f,
                damping = 0.46f,
                wet = 0.08f,
                dry = 1.00f,
                width = 0.88f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateMetalPreset()
    {
        return BuildPreset(
            "Metal",
            inputGainDb: 15.5f,
            outputGainDb: 10f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -42f,
                attack_ms = 0.8f,
                hold_ms = 30f,
                release_ms = 72f,
                range_db = -80f
            }),
            CreatePresetSlot(ToneLabPedalType.Compressor, true, new CompressorPedalSettings
            {
                threshold_db = -24f,
                ratio = 2.4f,
                attack_ms = 4f,
                release_ms = 58f
            }),
            CreatePresetSlot(ToneLabPedalType.Distortion, true, new DistortionPedalSettings
            {
                drive_db = 21f
            }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 39.5f,
                tone = 0.38f,
                presence = 0.78f,
                master_db = 0.2f,
                sag = 0.10f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.86f,
                presence = 0.68f,
                air = 0.18f,
                mix = 1f
            }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 86f,
                low_shelf_db = -0.8f,
                mid_db = -5.8f,
                high_shelf_db = 2.1f,
                high_cut_hz = 6100f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.30f,
                feedback = 0.12f,
                mix = 0.06f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.08f,
                damping = 0.68f,
                wet = 0.04f,
                dry = 1.00f,
                width = 0.76f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateLv2SessionCleanPreset()
    {
        return BuildPreset(
            "LV2 Session Clean",
            inputGainDb: 12f,
            outputGainDb: 9.5f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -67f,
                attack_ms = 1.2f,
                hold_ms = 18f,
                release_ms = 90f,
                range_db = -74f
            }),
            CreatePresetSlot(ToneLabPedalType.Compressor, true, new CompressorPedalSettings
            {
                threshold_db = -27f,
                ratio = 2.2f,
                attack_ms = 18f,
                release_ms = 165f
            }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_MicroAmp_#_MicroAmp_", "GxMicroAmp", true,
                new[] { "GAIN" },
                new[] { 0.22f }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 7.8f,
                tone = 0.62f,
                presence = 0.44f,
                master_db = 1.4f,
                sag = 0.06f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.34f,
                presence = 0.38f,
                air = 0.62f,
                mix = 1f
            }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 72f,
                low_shelf_db = -0.6f,
                mid_db = -1.0f,
                high_shelf_db = 1.2f,
                high_cut_hz = 8200f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.22f,
                damping = 0.56f,
                wet = 0.08f,
                dry = 1f,
                width = 0.92f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateLv2TexasEdgePreset()
    {
        return BuildPreset(
            "LV2 Texas Edge",
            inputGainDb: 13.5f,
            outputGainDb: 8.8f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -61f,
                attack_ms = 1.0f,
                hold_ms = 20f,
                release_ms = 98f,
                range_db = -76f
            }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_timray_#_timray_", "GxTimRay", true,
                new[] { "BASS", "GAIN", "TREBLE", "TRIM", "VOLUME" },
                new[] { 0.55f, 0.42f, 0.58f, 0.46f, 0.56f }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 18f,
                tone = 0.54f,
                presence = 0.46f,
                master_db = 0.6f,
                sag = 0.15f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.48f,
                presence = 0.42f,
                air = 0.38f,
                mix = 1f
            }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 82f,
                low_shelf_db = 0.4f,
                mid_db = 1.8f,
                high_shelf_db = -0.2f,
                high_cut_hz = 6900f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.16f,
                feedback = 0.14f,
                mix = 0.08f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.18f,
                damping = 0.50f,
                wet = 0.08f,
                dry = 1f,
                width = 0.88f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateLv2BritishCrunchPreset()
    {
        return BuildPreset(
            "LV2 British Crunch",
            inputGainDb: 14f,
            outputGainDb: 8.2f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -55f,
                attack_ms = 0.9f,
                hold_ms = 22f,
                release_ms = 86f,
                range_db = -78f
            }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_guvnor_#_guvnor_", "GxGuvnor", true,
                new[] { "BASS", "GAIN", "LEVEL", "MID", "TREBLE" },
                new[] { 0.48f, 0.62f, 0.54f, 0.58f, 0.56f }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 24f,
                tone = 0.56f,
                presence = 0.54f,
                master_db = -0.2f,
                sag = 0.18f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.58f,
                presence = 0.50f,
                air = 0.30f,
                mix = 1f
            }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_ultracab_#_ultracab_", "GxUltraCab", true,
                new[] { "GAIN", "MIDS", "PUNCH", "RESONANCE", "SIZE", "TOP" },
                new[] { 0.46f, 0.58f, 0.54f, 0.48f, 0.52f, 0.44f }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 88f,
                low_shelf_db = -0.4f,
                mid_db = 1.2f,
                high_shelf_db = 0.7f,
                high_cut_hz = 6400f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.12f,
                damping = 0.58f,
                wet = 0.05f,
                dry = 1f,
                width = 0.78f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateLv2FuzzLeadPreset()
    {
        return BuildPreset(
            "LV2 Fuzz Lead",
            inputGainDb: 13f,
            outputGainDb: 7.6f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -52f,
                attack_ms = 0.8f,
                hold_ms = 26f,
                release_ms = 82f,
                range_db = -80f
            }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_SunFace_#_SunFace_", "GxSunFace", true,
                new[] { "DRIVE", "INPUT", "VOLUME" },
                new[] { 0.70f, 0.58f, 0.46f }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 20f,
                tone = 0.44f,
                presence = 0.50f,
                master_db = -0.3f,
                sag = 0.12f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.50f,
                presence = 0.46f,
                air = 0.25f,
                mix = 1f
            }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 104f,
                low_shelf_db = -1.6f,
                mid_db = 2.4f,
                high_shelf_db = -0.8f,
                high_cut_hz = 5600f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.28f,
                feedback = 0.20f,
                mix = 0.10f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.18f,
                damping = 0.62f,
                wet = 0.07f,
                dry = 1f,
                width = 0.82f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateLv2SwellWahPreset()
    {
        return BuildPreset(
            "LV2 Swell Wah",
            inputGainDb: 12.5f,
            outputGainDb: 8.9f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -63f,
                attack_ms = 1.0f,
                hold_ms = 18f,
                release_ms = 105f,
                range_db = -74f
            }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_slowgear_#_slowgear_", "GxSlowGear", true,
                new[] { "DOWNTIME", "TRESHOLD", "UPTIME" },
                new[] { 12f, 1.8f, 180f }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_quack_#_quack_", "GxQuack", true,
                new[] { "DEPTH", "DRIVE", "GAIN", "MODE", "PEAK", "RANGE", "TONE" },
                new[] { 0.62f, 0f, -3f, 2f, 8f, 1f, 1f }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 11f,
                tone = 0.58f,
                presence = 0.42f,
                master_db = 0.8f,
                sag = 0.08f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.40f,
                presence = 0.38f,
                air = 0.48f,
                mix = 1f
            }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 80f,
                low_shelf_db = -0.8f,
                mid_db = 0.6f,
                high_shelf_db = 0.4f,
                high_cut_hz = 7200f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.22f,
                feedback = 0.16f,
                mix = 0.10f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.30f,
                damping = 0.55f,
                wet = 0.12f,
                dry = 1f,
                width = 0.94f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateLv2BoutiqueBoardPreset()
    {
        return BuildPreset(
            "LV2 Boutique Board",
            inputGainDb: 12.5f,
            outputGainDb: 8.4f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -64f,
                attack_ms = 1.2f,
                hold_ms = 18f,
                release_ms = 100f,
                range_db = -74f
            }),
            CreatePresetSlot(ToneLabPedalType.Compressor, true, new CompressorPedalSettings
            {
                threshold_db = -27f,
                ratio = 2.2f,
                attack_ms = 18f,
                release_ms = 165f
            }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_MicroAmp_#_MicroAmp_", "GxMicroAmp", true,
                new[] { "GAIN" },
                new[] { 0.18f }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_eternity_#_eternity_", "GxEternity", true,
                new[] { "DRIVE", "GLASS", "LEVEL" },
                new[] { 0.28f, 0.54f, 0.48f }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 11.5f,
                tone = 0.58f,
                presence = 0.42f,
                master_db = 0.8f,
                sag = 0.10f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.42f,
                presence = 0.38f,
                air = 0.52f,
                mix = 1f
            }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_ultracab_#_ultracab_", "GxUltraCab", true,
                new[] { "GAIN", "MIDS", "PUNCH", "RESONANCE", "SIZE", "TOP" },
                new[] { 0.40f, 0.50f, 0.46f, 0.38f, 0.56f, 0.42f }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 78f,
                low_shelf_db = -0.4f,
                mid_db = 0.7f,
                high_shelf_db = 0.5f,
                high_cut_hz = 7600f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.18f,
                feedback = 0.16f,
                mix = 0.08f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.22f,
                damping = 0.56f,
                wet = 0.09f,
                dry = 1f,
                width = 0.90f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateLv2ModernDriveBoardPreset()
    {
        return BuildPreset(
            "LV2 Modern Drive Board",
            inputGainDb: 13.8f,
            outputGainDb: 7.8f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -50f,
                attack_ms = 0.8f,
                hold_ms = 26f,
                release_ms = 80f,
                range_db = -80f
            }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_sd1sim_#_sd1sim_", "GxSD1", true,
                new[] { "DRIVE", "LEVEL", "TONE" },
                new[] { 0.18f, 0.62f, 0.58f }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_guvnor_#_guvnor_", "GxGuvnor", true,
                new[] { "BASS", "GAIN", "LEVEL", "MID", "TREBLE" },
                new[] { 0.48f, 0.54f, 0.52f, 0.58f, 0.54f }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 28f,
                tone = 0.50f,
                presence = 0.60f,
                master_db = -0.5f,
                sag = 0.16f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.62f,
                presence = 0.50f,
                air = 0.26f,
                mix = 1f
            }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_ultracab_#_ultracab_", "GxUltraCab", true,
                new[] { "GAIN", "MIDS", "PUNCH", "RESONANCE", "SIZE", "TOP" },
                new[] { 0.42f, 0.58f, 0.58f, 0.50f, 0.48f, 0.36f }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 92f,
                low_shelf_db = -1.2f,
                mid_db = 0.8f,
                high_shelf_db = 1.2f,
                high_cut_hz = 6200f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.24f,
                feedback = 0.12f,
                mix = 0.06f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.10f,
                damping = 0.64f,
                wet = 0.045f,
                dry = 1f,
                width = 0.76f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateLv2PsychedelicFuzzPreset()
    {
        return BuildPreset(
            "LV2 Psychedelic Fuzz",
            inputGainDb: 13f,
            outputGainDb: 7.4f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -54f,
                attack_ms = 0.9f,
                hold_ms = 28f,
                release_ms = 90f,
                range_db = -80f
            }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_AxisFace_#_AxisFace_", "GxAxisFace", true,
                new[] { "ATTACK", "SMOOTH", "VOLUME" },
                new[] { 0.70f, 0.42f, 0.48f }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_quack_#_quack_", "GxQuack", true,
                new[] { "DEPTH", "DRIVE", "GAIN", "MODE", "PEAK", "RANGE", "TONE" },
                new[] { 0.54f, 0.08f, -3f, 2f, 6f, 0.78f, 0.9f }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 19f,
                tone = 0.48f,
                presence = 0.46f,
                master_db = -0.4f,
                sag = 0.14f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.48f,
                presence = 0.42f,
                air = 0.28f,
                mix = 1f
            }),
            CreatePresetSlot(ToneLabPedalType.Phaser, true, new PhaserPedalSettings
            {
                rate_hz = 0.26f,
                depth = 0.36f,
                mix = 0.18f,
                center_hz = 900f,
                feedback = 0.08f
            }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 110f,
                low_shelf_db = -2.0f,
                mid_db = 2.2f,
                high_shelf_db = -0.6f,
                high_cut_hz = 5600f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.31f,
                feedback = 0.22f,
                mix = 0.11f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.24f,
                damping = 0.54f,
                wet = 0.09f,
                dry = 1f,
                width = 0.88f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateNamTwoRockCleanPreset()
    {
        return BuildPreset(
            "NAM Two Rock Clean",
            inputGainDb: 12.2f,
            outputGainDb: 8.8f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -64f,
                attack_ms = 1.2f,
                hold_ms = 18f,
                release_ms = 105f,
                range_db = -74f
            }),
            CreatePresetSlot(ToneLabPedalType.Compressor, true, new CompressorPedalSettings
            {
                threshold_db = -27f,
                ratio = 2.1f,
                attack_ms = 18f,
                release_ms = 170f
            }),
            CreateNamPresetSlot(
                "Two Rock Studio Signature + OX Box 2x12 Two Rock Cab V2/All 5s Traditional.nam",
                "Two Rock All 5s",
                true,
                inputTrimDb: 0.0f,
                outputTrimDb: -1.0f,
                mix: 1f),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 82f,
                low_shelf_db = -0.6f,
                mid_db = -0.8f,
                high_shelf_db = 1.4f,
                high_cut_hz = 8200f
            }),
            CreatePresetSlot(ToneLabPedalType.Chorus, true, new ChorusPedalSettings
            {
                rate_hz = 0.36f,
                depth = 0.18f,
                mix = 0.10f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.22f,
                feedback = 0.14f,
                mix = 0.07f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.26f,
                damping = 0.56f,
                wet = 0.10f,
                dry = 1f,
                width = 0.92f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateNamPlexiCrunchPreset()
    {
        return BuildPreset(
            "NAM Plexi Crunch",
            inputGainDb: 13.2f,
            outputGainDb: 7.6f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -55f,
                attack_ms = 0.9f,
                hold_ms = 22f,
                release_ms = 90f,
                range_db = -78f
            }),
            CreateNamPresetSlot(
                "pLEXI-LORE/model (1).nam",
                "pLEXI-LORE",
                true,
                inputTrimDb: -1.5f,
                outputTrimDb: -3.0f,
                mix: 1f),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 92f,
                low_shelf_db = -0.8f,
                mid_db = 1.2f,
                high_shelf_db = 0.4f,
                high_cut_hz = 6400f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.18f,
                feedback = 0.12f,
                mix = 0.06f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.16f,
                damping = 0.60f,
                wet = 0.06f,
                dry = 1f,
                width = 0.78f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateNamPowerballTightPreset()
    {
        return BuildPreset(
            "NAM Powerball Tight",
            inputGainDb: 14.6f,
            outputGainDb: 6.6f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -42f,
                attack_ms = 0.4f,
                hold_ms = 24f,
                release_ms = 58f,
                range_db = -82f
            }),
            CreatePresetSlot(ToneLabPedalType.Compressor, false, new CompressorPedalSettings
            {
                threshold_db = -18f,
                ratio = 1.6f,
                attack_ms = 8f,
                release_ms = 90f
            }),
            CreateNamPresetSlot(
                "Engl Powerball II/Engle_Powerball_II.nam",
                "ENGL Powerball II",
                true,
                inputTrimDb: -3.0f,
                outputTrimDb: -4.5f,
                mix: 1f),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 86f,
                low_shelf_db = -1.8f,
                mid_db = -3.6f,
                high_shelf_db = 1.2f,
                high_cut_hz = 5600f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.08f,
                damping = 0.72f,
                wet = 0.035f,
                dry = 1f,
                width = 0.70f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateRsCleanPopPreset()
    {
        return BuildPreset(
            "Clean Pop",
            inputGainDb: 12.2f,
            outputGainDb: 8.8f,
            CreateLv2ZamGateX2Slot(-58f, -50f, 8f, 120f),
            CreateLv2ZamCompX2Slot(-24f, 2.4f, 2.5f, 18f, 160f, 2f),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_MicroAmp_#_MicroAmp_", "GxMicroAmp", true,
                new[] { "GAIN" },
                new[] { 0.16f }),
            CreateCleanAmpSlot(8.4f, 0.62f, 0.42f, 1.1f, 0.06f),
            CreateOpenCabSlot(0.34f, 0.36f, 0.60f),
            CreateLv2Dpf3BandEqSlot(-1.2f, -0.8f, 1.8f, -0.8f, 420f, 3100f),
            CreatePresetSlot(ToneLabPedalType.Chorus, true, new ChorusPedalSettings
            {
                rate_hz = 0.42f,
                depth = 0.20f,
                mix = 0.12f
            }),
            CreateLv2DragonflyRoomSlot(88f, 5f, 9f, 10f, 0.32f, 90f, 9000f, 90f));
    }

    private static ToneLabPreset CreateRsIndieJanglePreset()
    {
        return BuildPreset(
            "Indie Jangle",
            inputGainDb: 12.6f,
            outputGainDb: 8.4f,
            CreateLv2ZamGateX2Slot(-60f, -48f, 6f, 110f),
            CreateLv2ZamCompX2Slot(-26f, 3.2f, 3.0f, 12f, 130f, 3f),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_MicroAmp_#_MicroAmp_", "GxMicroAmp", true,
                new[] { "GAIN" },
                new[] { 0.20f }),
            CreateCleanAmpSlot(10.5f, 0.68f, 0.48f, 0.9f, 0.05f),
            CreateOpenCabSlot(0.30f, 0.42f, 0.68f),
            CreateLv2Dpf3BandEqSlot(-1.8f, -1.0f, 3.0f, -1.2f, 520f, 3600f),
            CreateLv2ZamDelaySlot(115f, 0.08f, 0.09f, 7200f, -5f),
            CreateLv2DragonflyPlateSlot(84f, 8f, 1f, 0.55f, 26f, 8800f, 100f));
    }

    private static ToneLabPreset CreateRsFunkSnapPreset()
    {
        return BuildPreset(
            "Funk Snap",
            inputGainDb: 12.8f,
            outputGainDb: 8.2f,
            CreateLv2ZamGateX2Slot(-56f, -50f, 2.5f, 90f),
            CreateLv2ZamCompX2Slot(-30f, 4.2f, 4.0f, 5f, 95f, 4f),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_quack_#_quack_", "GxQuack", true,
                new[] { "DEPTH", "DRIVE", "GAIN", "MODE", "PEAK", "RANGE", "TONE" },
                new[] { 0.58f, 0.0f, -4f, 2f, 7f, 0.82f, 0.92f }),
            CreateCleanAmpSlot(7.2f, 0.58f, 0.38f, 1.0f, 0.04f),
            CreateOpenCabSlot(0.30f, 0.34f, 0.55f),
            CreateLv2Dpf3BandEqSlot(-2.0f, 1.4f, 1.0f, -1.2f, 380f, 2600f),
            CreateLv2DragonflyRoomSlot(92f, 4f, 5f, 8f, 0.22f, 65f, 7600f, 100f));
    }

    private static ToneLabPreset CreateRsCountryTwangPreset()
    {
        return BuildPreset(
            "Country Twang",
            inputGainDb: 12.5f,
            outputGainDb: 8.6f,
            CreateLv2ZamGateX2Slot(-60f, -48f, 3f, 105f),
            CreateLv2ZamCompX2Slot(-27f, 3.6f, 3.2f, 7f, 120f, 3f),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_timray_#_timray_", "GxTimRay", true,
                new[] { "BASS", "GAIN", "TREBLE", "TRIM", "VOLUME" },
                new[] { 0.42f, 0.24f, 0.68f, 0.38f, 0.58f }),
            CreateCleanAmpSlot(11f, 0.64f, 0.48f, 0.8f, 0.06f),
            CreateOpenCabSlot(0.36f, 0.46f, 0.58f),
            CreateLv2Dpf3BandEqSlot(-1.2f, 0.4f, 2.6f, -1.0f, 430f, 3000f),
            CreateLv2ZamDelaySlot(88f, 0.05f, 0.10f, 6600f, -5f),
            CreateLv2DragonflyRoomSlot(88f, 5f, 8f, 9f, 0.30f, 78f, 8200f, 90f));
    }

    private static ToneLabPreset CreateRsBluesBreakupPreset()
    {
        return BuildPreset(
            "Blues Breakup",
            inputGainDb: 13.2f,
            outputGainDb: 8.0f,
            CreateLv2ZamGateX2Slot(-58f, -46f, 4f, 120f),
            CreateLv2ZamCompX2Slot(-23f, 2.0f, 1.5f, 20f, 170f, 2f),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_timray_#_timray_", "GxTimRay", true,
                new[] { "BASS", "GAIN", "TREBLE", "TRIM", "VOLUME" },
                new[] { 0.54f, 0.44f, 0.52f, 0.44f, 0.56f }),
            CreateLv2ZamTubeSlot(1.8f, 5.4f, 5.8f, 5.0f, 0f, -2f, 0f),
            CreateCleanAmpSlot(18f, 0.52f, 0.44f, 0.3f, 0.14f),
            CreateOpenCabSlot(0.50f, 0.42f, 0.36f),
            CreateLv2ZamEq2Slot(0.8f, 180f, 1.6f, 900f, 1.3f, 1.0f, 2800f, 1.3f, -1.0f, 6500f, -1f, 0f),
            CreateLv2DragonflyPlateSlot(86f, 10f, 0f, 0.70f, 22f, 6800f, 95f));
    }

    private static ToneLabPreset CreateRsClassicCrunchPreset()
    {
        return BuildPreset(
            "Classic Crunch",
            inputGainDb: 13.8f,
            outputGainDb: 7.6f,
            CreateLv2ZamGateX2Slot(-52f, -50f, 1.5f, 85f),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_guvnor_#_guvnor_", "GxGuvnor", true,
                new[] { "BASS", "GAIN", "LEVEL", "MID", "TREBLE" },
                new[] { 0.48f, 0.56f, 0.50f, 0.58f, 0.52f }),
            CreateLv2ZamTubeSlot(2.8f, 5.0f, 5.8f, 5.2f, 0f, -3f, 0f),
            CreateCrunchAmpSlot(25f, 0.54f, 0.54f, -0.3f, 0.16f),
            CreateClosedCabSlot(0.58f, 0.48f, 0.30f),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_ultracab_#_ultracab_", "GxUltraCab", true,
                new[] { "GAIN", "MIDS", "PUNCH", "RESONANCE", "SIZE", "TOP" },
                new[] { 0.42f, 0.56f, 0.52f, 0.46f, 0.52f, 0.38f }),
            CreateLv2Dpf3BandEqSlot(-1.0f, 0.8f, 0.6f, -1.2f, 460f, 3200f),
            CreateLv2DragonflyPlateSlot(90f, 6f, 1f, 0.46f, 16f, 6200f, 110f));
    }

    private static ToneLabPreset CreateRsPunkRhythmPreset()
    {
        return BuildPreset(
            "Punk Rhythm",
            inputGainDb: 14f,
            outputGainDb: 7.4f,
            CreateLv2ZamGateX2Slot(-50f, -50f, 0.9f, 72f),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_sd1sim_#_sd1sim_", "GxSD1", true,
                new[] { "DRIVE", "LEVEL", "TONE" },
                new[] { 0.32f, 0.64f, 0.56f }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_guvnor_#_guvnor_", "GxGuvnor", true,
                new[] { "BASS", "GAIN", "LEVEL", "MID", "TREBLE" },
                new[] { 0.44f, 0.62f, 0.52f, 0.54f, 0.56f }),
            CreateCrunchAmpSlot(28f, 0.52f, 0.60f, -0.6f, 0.14f),
            CreateClosedCabSlot(0.54f, 0.50f, 0.24f),
            CreateLv2Dpf3BandEqSlot(-1.8f, 1.2f, 0.8f, -1.6f, 380f, 2800f),
            CreateLv2DragonflyRoomSlot(96f, 2f, 3f, 8f, 0.18f, 55f, 6000f, 120f));
    }

    private static ToneLabPreset CreateRsAltLeadPreset()
    {
        return BuildPreset(
            "Alt Lead",
            inputGainDb: 13.6f,
            outputGainDb: 7.5f,
            CreateLv2ZamGateX2Slot(-54f, -50f, 1.2f, 90f),
            CreateLv2ZamCompX2Slot(-22f, 2.4f, 2.0f, 14f, 140f, 2f),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_eternity_#_eternity_", "GxEternity", true,
                new[] { "DRIVE", "GLASS", "LEVEL" },
                new[] { 0.48f, 0.56f, 0.52f }),
            CreateCrunchAmpSlot(24f, 0.50f, 0.56f, -0.4f, 0.14f),
            CreateClosedCabSlot(0.50f, 0.46f, 0.30f),
            CreateLv2ZamEq2Slot(-0.8f, 140f, 1.4f, 850f, 2.2f, 1.4f, 3200f, 1.2f, -1.6f, 6400f, -1.5f, 0f),
            CreateLv2ZamDelaySlot(360f, 0.24f, 0.18f, 5200f, -7f),
            CreateLv2DragonflyHallSlot(86f, 7f, 12f, 18f, 1.0f, 18f, 6200f, 90f));
    }

    private static ToneLabPreset CreateRsMetalTightPreset()
    {
        return BuildPreset(
            "Metal Tight",
            inputGainDb: 15f,
            outputGainDb: 6.8f,
            CreateLv2ZamGateX2Slot(-42f, -50f, 0.4f, 55f),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_sd1sim_#_sd1sim_", "GxSD1", true,
                new[] { "DRIVE", "LEVEL", "TONE" },
                new[] { 0.08f, 0.72f, 0.62f }),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_guvnor_#_guvnor_", "GxGuvnor", true,
                new[] { "BASS", "GAIN", "LEVEL", "MID", "TREBLE" },
                new[] { 0.38f, 0.62f, 0.50f, 0.56f, 0.56f }),
            CreateHighGainAmpSlot(37f, 0.38f, 0.78f, -0.8f, 0.08f),
            CreateClosedCabSlot(0.78f, 0.62f, 0.18f),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 92f,
                low_shelf_db = -1.2f,
                mid_db = -4.6f,
                high_shelf_db = 1.5f,
                high_cut_hz = 6100f
            }),
            CreateLv2Dpf3BandEqSlot(-2.0f, -1.5f, 1.0f, -2.5f, 360f, 2800f),
            CreateLv2DragonflyRoomSlot(98f, 1f, 2f, 8f, 0.14f, 40f, 5200f, 120f));
    }

    private static ToneLabPreset CreateRsDropModernPreset()
    {
        return BuildPreset(
            "Drop Modern",
            inputGainDb: 15.5f,
            outputGainDb: 6.4f,
            CreateLv2ZamGateX2Slot(-39f, -50f, 0.3f, 45f),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_sd1sim_#_sd1sim_", "GxSD1", true,
                new[] { "DRIVE", "LEVEL", "TONE" },
                new[] { 0.05f, 0.76f, 0.66f }),
            CreateLv2ZamTubeSlot(2.2f, 3.8f, 5.0f, 6.4f, 0f, -5f, 0f),
            CreateHighGainAmpSlot(40f, 0.34f, 0.84f, -1.0f, 0.07f),
            CreateClosedCabSlot(0.90f, 0.66f, 0.15f),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 78f,
                low_shelf_db = -2.6f,
                mid_db = -5.2f,
                high_shelf_db = 1.8f,
                high_cut_hz = 5600f
            }),
            CreateLv2Dpf3BandEqSlot(-3.0f, -1.8f, 0.8f, -3.0f, 320f, 2400f));
    }

    private static ToneLabPreset CreateRsAmbientCleanPreset()
    {
        return BuildPreset(
            "Ambient Clean",
            inputGainDb: 12f,
            outputGainDb: 7.8f,
            CreateLv2ZamGateX2Slot(-62f, -46f, 10f, 150f),
            CreateLv2ZamCompX2Slot(-28f, 2.8f, 3.0f, 22f, 220f, 3f),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_MicroAmp_#_MicroAmp_", "GxMicroAmp", true,
                new[] { "GAIN" },
                new[] { 0.12f }),
            CreateCleanAmpSlot(6.8f, 0.60f, 0.36f, 1.0f, 0.05f),
            CreateOpenCabSlot(0.32f, 0.34f, 0.62f),
            CreateLv2Dpf3BandEqSlot(-1.6f, -1.0f, 1.2f, -1.8f, 420f, 3000f),
            CreateLv2ZamDelaySlot(520f, 0.38f, 0.22f, 5200f, -8f),
            CreateLv2DragonflyHallSlot(78f, 10f, 24f, 34f, 2.8f, 28f, 6800f, 95f));
    }

    private static ToneLabPreset CreateRsShoegazeWallPreset()
    {
        return BuildPreset(
            "Shoegaze Wall",
            inputGainDb: 13f,
            outputGainDb: 6.8f,
            CreateLv2ZamGateX2Slot(-58f, -42f, 8f, 180f),
            CreateLv2ZamCompX2Slot(-30f, 3.0f, 4.0f, 18f, 230f, 4f),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_AxisFace_#_AxisFace_", "GxAxisFace", true,
                new[] { "ATTACK", "SMOOTH", "VOLUME" },
                new[] { 0.48f, 0.55f, 0.42f }),
            CreateCrunchAmpSlot(18f, 0.46f, 0.42f, -0.6f, 0.18f),
            CreateClosedCabSlot(0.48f, 0.38f, 0.22f),
            CreatePresetSlot(ToneLabPedalType.Phaser, true, new PhaserPedalSettings
            {
                rate_hz = 0.18f,
                depth = 0.40f,
                mix = 0.16f,
                center_hz = 820f,
                feedback = 0.08f
            }),
            CreateLv2ZamDelaySlot(420f, 0.46f, 0.24f, 4200f, -9f),
            CreateLv2DragonflyHallSlot(72f, 14f, 32f, 46f, 4.2f, 32f, 5600f, 100f));
    }

    private static ToneLabPreset CreateRsSurfPlatePreset()
    {
        return BuildPreset(
            "Surf Plate",
            inputGainDb: 12.7f,
            outputGainDb: 8.2f,
            CreateLv2ZamGateX2Slot(-61f, -48f, 4f, 120f),
            CreateLv2ZamCompX2Slot(-25f, 3.0f, 2.6f, 9f, 135f, 3f),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_MicroAmp_#_MicroAmp_", "GxMicroAmp", true,
                new[] { "GAIN" },
                new[] { 0.14f }),
            CreateCleanAmpSlot(9.2f, 0.68f, 0.52f, 0.8f, 0.04f),
            CreateOpenCabSlot(0.34f, 0.46f, 0.64f),
            CreateLv2Dpf3BandEqSlot(-1.0f, -0.4f, 3.2f, -1.4f, 430f, 3500f),
            CreateLv2PresetSlot("http://distrho.sf.net/plugins/PingPongPan", "Ping Pong Pan", true,
                new[] { "freq", "width" },
                new[] { 50f, 20f }),
            CreateLv2ZamDelaySlot(110f, 0.10f, 0.11f, 7600f, -5f),
            CreateLv2DragonflyPlateSlot(72f, 30f, 2f, 1.4f, 12f, 9300f, 120f));
    }

    private static ToneLabPreset CreateRsOctaveFuzzPreset()
    {
        return BuildPreset(
            "Octave Fuzz",
            inputGainDb: 13.4f,
            outputGainDb: 6.9f,
            CreateLv2ZamGateX2Slot(-51f, -50f, 0.8f, 80f),
            CreateLv2PresetSlot("http://guitarix.sourceforge.net/plugins/gx_SunFace_#_SunFace_", "GxSunFace", true,
                new[] { "DRIVE", "INPUT", "VOLUME" },
                new[] { 0.66f, 0.58f, 0.42f }),
            CreateLv2PresetSlot("http://distrho.sf.net/plugins/MaPitchshift", "MaPitchshift", true,
                new[] { "blur", "window", "ratio", "xfade" },
                new[] { 0.02f, 80f, 2.0f, 0.42f }),
            CreateCrunchAmpSlot(19f, 0.44f, 0.48f, -0.5f, 0.12f),
            CreateClosedCabSlot(0.48f, 0.42f, 0.24f),
            CreateLv2Dpf3BandEqSlot(-2.4f, 2.0f, -1.2f, -2.5f, 480f, 2500f),
            CreateLv2DragonflyPlateSlot(86f, 8f, 0f, 0.65f, 20f, 5600f, 90f));
    }

    private static ToneLabPreset CreateRsBassGrindPreset()
    {
        return BuildPreset(
            "Bass Grind",
            inputGainDb: 12.8f,
            outputGainDb: 8.0f,
            CreateLv2ZamGateX2Slot(-56f, -46f, 4f, 120f),
            CreateLv2ZamCompX2Slot(-32f, 4.5f, 5.5f, 14f, 180f, 4f),
            CreateLv2ZamTubeSlot(1.2f, 7.2f, 5.2f, 3.8f, 0f, -4f, 0f),
            CreateCrunchAmpSlot(13f, 0.36f, 0.30f, 0.2f, 0.12f),
            CreateClosedCabSlot(0.92f, 0.26f, 0.12f),
            CreateLv2Dpf3BandEqSlot(3.0f, 0.8f, -3.4f, -1.8f, 280f, 1800f),
            CreateLv2DragonflyRoomSlot(96f, 1f, 2f, 10f, 0.20f, 45f, 4200f, 80f));
    }

    private static ToneLabPreset BuildPreset(string presetName, float inputGainDb, float outputGainDb, params ToneLabPedalSlot[] pedalSlots)
    {
        return new ToneLabPreset
        {
            preset_id = CreatePresetId(),
            preset_name = presetName,
            input_gain_db = Mathf.Clamp(inputGainDb, MinRigGainDb, MaxRigGainDb),
            output_gain_db = Mathf.Clamp(outputGainDb, MinRigGainDb, MaxRigGainDb),
            pedal_chain = ClonePedalChain(pedalSlots)
        };
    }

    private static ToneLabPedalSlot CreatePresetSlot(ToneLabPedalType pedalType, bool enabled, object settingsObject)
    {
        IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(pedalType);
        return new ToneLabPedalSlot
        {
            pedal_instance_id = CreatePedalInstanceId(),
            pedal_type = pedalType,
            descriptor_id = descriptor.DescriptorId,
            enabled = enabled,
            settings_json = descriptor.SerializeSettingsObject(settingsObject ?? descriptor.CreateDefaultSettingsObject())
        };
    }

    private static ToneLabPedalSlot CreateLv2PresetSlot(string pluginUri, string displayName, bool enabled, string[] parameterIds, float[] parameterValues)
    {
        string descriptorId = ToneLabExternalPedalCatalog.BuildLv2DescriptorId(pluginUri);
        ToneLabExternalPedalSettings settings = new ToneLabExternalPedalSettings
        {
            descriptor_id = descriptorId,
            processor_kind = "lv2",
            plugin_uri = pluginUri ?? string.Empty,
            display_name = displayName ?? "LV2 Effect",
            parameters = new List<ToneLabExternalParameterValue>()
        };

        int parameterCount = Mathf.Min(parameterIds?.Length ?? 0, parameterValues?.Length ?? 0);
        for (int i = 0; i < parameterCount; i++)
        {
            if (string.IsNullOrWhiteSpace(parameterIds[i]))
                continue;

            settings.parameters.Add(new ToneLabExternalParameterValue
            {
                parameter_id = parameterIds[i],
                value = parameterValues[i]
            });
        }

        return new ToneLabPedalSlot
        {
            pedal_instance_id = CreatePedalInstanceId(),
            pedal_type = ToneLabPedalType.Lv2Plugin,
            descriptor_id = descriptorId,
            enabled = enabled,
            settings_json = JsonUtility.ToJson(settings)
        };
    }

    private static ToneLabPedalSlot CreateNamPresetSlot(string modelRelativePath, string displayName, bool enabled, float inputTrimDb, float outputTrimDb, float mix)
    {
        string normalizedRelativePath = (modelRelativePath ?? string.Empty).Replace('\\', '/').Trim('/');
        string descriptorId = ToneLabExternalPedalCatalog.BuildNamDescriptorIdFromRelativePath(normalizedRelativePath);
        string modelPath = Path.Combine(ExternalContentPaths.PersistentToneLabNamDirectory, normalizedRelativePath);
        ToneLabExternalPedalSettings settings = new ToneLabExternalPedalSettings
        {
            descriptor_id = descriptorId,
            processor_kind = "nam",
            display_name = string.IsNullOrWhiteSpace(displayName) ? "NAM Amp" : displayName.Trim(),
            model_path = modelPath,
            parameters = new List<ToneLabExternalParameterValue>
            {
                new ToneLabExternalParameterValue { parameter_id = "input_trim_db", value = Mathf.Clamp(inputTrimDb, -24f, 24f) },
                new ToneLabExternalParameterValue { parameter_id = "output_trim_db", value = Mathf.Clamp(outputTrimDb, -24f, 24f) },
                new ToneLabExternalParameterValue { parameter_id = "mix", value = Mathf.Clamp01(mix) }
            }
        };

        return new ToneLabPedalSlot
        {
            pedal_instance_id = CreatePedalInstanceId(),
            pedal_type = ToneLabPedalType.NamModel,
            descriptor_id = descriptorId,
            enabled = enabled,
            settings_json = JsonUtility.ToJson(settings)
        };
    }

    private static ToneLabPedalSlot CreateLv2ZamGateX2Slot(float thresholdDb, float closeDb, float attackMs, float releaseMs)
    {
        return CreateLv2PresetSlot("urn:zamaudio:ZamGateX2", "ZamGateX2", true,
            new[] { "att", "rel", "thr", "mak", "sidechain", "close", "mode" },
            new[] { attackMs, releaseMs, thresholdDb, 0f, 0f, closeDb, 0f });
    }

    private static ToneLabPedalSlot CreateLv2ZamCompX2Slot(float thresholdDb, float ratio, float makeupDb, float attackMs, float releaseMs, float kneeDb)
    {
        return CreateLv2PresetSlot("urn:zamaudio:ZamCompX2", "ZamCompX2", true,
            new[] { "att", "rel", "kn", "rat", "thr", "mak", "slew", "stereodet", "sidechain" },
            new[] { attackMs, releaseMs, kneeDb, ratio, thresholdDb, makeupDb, 1f, 1f, 0f });
    }

    private static ToneLabPedalSlot CreateLv2ZamTubeSlot(float drive, float bass, float mids, float treble, float toneStack, float inputGainDb, float insane)
    {
        return CreateLv2PresetSlot("urn:zamaudio:ZamTube", "ZamTube", true,
            new[] { "tubedrive", "bass", "mids", "treb", "tonestack", "gain", "insane" },
            new[] { drive, bass, mids, treble, toneStack, inputGainDb, insane });
    }

    private static ToneLabPedalSlot CreateLv2ZamDelaySlot(float timeMs, float feedback, float dryWet, float lowPassHz, float outputGainDb)
    {
        return CreateLv2PresetSlot("urn:zamaudio:ZamDelay", "ZamDelay", true,
            new[] { "inv", "time", "sync", "lpf", "div", "gain", "drywet", "feedb" },
            new[] { 0f, timeMs, 0f, lowPassHz, 3f, outputGainDb, dryWet, feedback });
    }

    private static ToneLabPedalSlot CreateLv2ZamEq2Slot(
        float lowBoostDb,
        float lowFreqHz,
        float mid1BoostDb,
        float mid1FreqHz,
        float mid1Bandwidth,
        float mid2BoostDb,
        float mid2FreqHz,
        float mid2Bandwidth,
        float highBoostDb,
        float highFreqHz,
        float outputGainDb,
        float inputGainDb)
    {
        return CreateLv2PresetSlot("urn:zamaudio:ZamEQ2", "ZamEQ2", true,
            new[] { "boost1", "bw1", "f1", "boost2", "bw2", "f2", "boostl", "fl", "boosth", "fh", "outputgain", "inputgain" },
            new[] { mid1BoostDb, mid1Bandwidth, mid1FreqHz, mid2BoostDb, mid2Bandwidth, mid2FreqHz, lowBoostDb, lowFreqHz, highBoostDb, highFreqHz, outputGainDb, inputGainDb });
    }

    private static ToneLabPedalSlot CreateLv2Dpf3BandEqSlot(float lowDb, float midDb, float highDb, float masterDb, float lowMidHz, float midHighHz)
    {
        return CreateLv2PresetSlot("http://distrho.sf.net/plugins/3BandEQ", "3 Band EQ", true,
            new[] { "low", "mid", "high", "master", "low_mid", "mid_high" },
            new[] { lowDb, midDb, highDb, masterDb, lowMidHz, midHighHz });
    }

    private static ToneLabPedalSlot CreateLv2DragonflyRoomSlot(float dryLevel, float earlyLevel, float lateLevel, float size, float decay, float diffuse, float highCutHz, float lowCutHz)
    {
        return CreateLv2PresetSlot("urn:dragonfly:room", "Dragonfly Room", true,
            new[] { "dry_level", "early_level", "early_send", "late_level", "size", "width", "predelay", "decay", "diffuse", "spin", "wander", "in_high_cut", "early_damp", "late_damp", "low_boost", "boost_freq", "in_low_cut" },
            new[] { dryLevel, earlyLevel, earlyLevel, lateLevel, size, 100f, 6f, decay, diffuse, 0.6f, 20f, highCutHz, highCutHz, highCutHz, 40f, 520f, lowCutHz });
    }

    private static ToneLabPedalSlot CreateLv2DragonflyPlateSlot(float dryLevel, float wetLevel, float algorithm, float decay, float predelayMs, float highCutHz, float width)
    {
        return CreateLv2PresetSlot("urn:dragonfly:plate", "Dragonfly Plate", true,
            new[] { "dry_level", "early_level", "algorithm", "width", "predelay", "decay", "low_cut", "high_cut", "early_damp" },
            new[] { dryLevel, wetLevel, algorithm, width, predelayMs, decay, 120f, highCutHz, highCutHz });
    }

    private static ToneLabPedalSlot CreateLv2DragonflyHallSlot(float dryLevel, float earlyLevel, float lateLevel, float size, float decay, float predelayMs, float highCutHz, float lowCutHz)
    {
        return CreateLv2PresetSlot("https://github.com/michaelwillis/dragonfly-reverb", "Dragonfly Hall", true,
            new[] { "dry_level", "early_level", "late_level", "size", "width", "delay", "diffuse", "low_cut", "low_xo", "low_mult", "high_cut", "high_xo", "high_mult", "spin", "wander", "decay", "early_send", "modulation" },
            new[] { dryLevel, earlyLevel, lateLevel, size, 100f, predelayMs, 90f, lowCutHz, 500f, 1.1f, highCutHz, 5200f, 0.55f, 2.6f, 12f, decay, earlyLevel, 14f });
    }

    private static ToneLabPedalSlot CreateCleanAmpSlot(float gainDb, float tone, float presence, float masterDb, float sag)
    {
        return CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
        {
            gain_db = gainDb,
            tone = tone,
            presence = presence,
            master_db = masterDb,
            sag = sag
        });
    }

    private static ToneLabPedalSlot CreateCrunchAmpSlot(float gainDb, float tone, float presence, float masterDb, float sag)
    {
        return CreateCleanAmpSlot(gainDb, tone, presence, masterDb, sag);
    }

    private static ToneLabPedalSlot CreateHighGainAmpSlot(float gainDb, float tone, float presence, float masterDb, float sag)
    {
        return CreateCleanAmpSlot(gainDb, tone, presence, masterDb, sag);
    }

    private static ToneLabPedalSlot CreateOpenCabSlot(float thump, float presence, float air)
    {
        return CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
        {
            thump = thump,
            presence = presence,
            air = air,
            mix = 1f
        });
    }

    private static ToneLabPedalSlot CreateClosedCabSlot(float thump, float presence, float air)
    {
        return CreateOpenCabSlot(thump, presence, air);
    }

    private static string MakeUniquePresetName(ToneLabSettings toneSettings, string requestedName)
    {
        HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (toneSettings?.presets != null)
        {
            for (int i = 0; i < toneSettings.presets.Count; i++)
            {
                ToneLabPreset preset = toneSettings.presets[i];
                if (preset != null && !string.IsNullOrWhiteSpace(preset.preset_name))
                    usedNames.Add(preset.preset_name.Trim());
            }
        }

        return MakeUniquePresetName(requestedName, usedNames);
    }

    private static string MakeUniquePresetName(string requestedName, HashSet<string> usedNames)
    {
        string trimmed = string.IsNullOrWhiteSpace(requestedName) ? "New Preset" : requestedName.Trim();
        if (usedNames == null)
            return trimmed;
        if (usedNames.Add(trimmed))
            return trimmed;

        int suffix = 2;
        while (true)
        {
            string candidate = $"{trimmed} {suffix}";
            if (usedNames.Add(candidate))
                return candidate;
            suffix++;
        }
    }

    private static bool GetLegacyPedalEnabled(ToneLabSettings toneSettings, ToneLabPedalType pedalType)
    {
        if (toneSettings == null)
            return false;

        switch (pedalType)
        {
            case ToneLabPedalType.Distortion:
                return toneSettings.dist_enabled;
            case ToneLabPedalType.Chorus:
                return toneSettings.chorus_enabled;
            case ToneLabPedalType.Phaser:
                return toneSettings.phaser_enabled;
            case ToneLabPedalType.Delay:
                return toneSettings.delay_enabled;
            case ToneLabPedalType.Reverb:
                return toneSettings.reverb_enabled;
            case ToneLabPedalType.Compressor:
                return toneSettings.comp_enabled;
            default:
                return false;
        }
    }

    private static void SetLegacyPedalEnabled(ToneLabSettings toneSettings, ToneLabPedalType pedalType, bool enabled)
    {
        if (toneSettings == null)
            return;

        switch (pedalType)
        {
            case ToneLabPedalType.Distortion:
                toneSettings.dist_enabled = enabled;
                break;
            case ToneLabPedalType.Chorus:
                toneSettings.chorus_enabled = enabled;
                break;
            case ToneLabPedalType.Phaser:
                toneSettings.phaser_enabled = enabled;
                break;
            case ToneLabPedalType.Delay:
                toneSettings.delay_enabled = enabled;
                break;
            case ToneLabPedalType.Reverb:
                toneSettings.reverb_enabled = enabled;
                break;
            case ToneLabPedalType.Compressor:
                toneSettings.comp_enabled = enabled;
                break;
        }
    }

    private static string CreatePedalInstanceId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static string CreatePresetId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static string GetPresetFilePath(ToneLabPreset preset)
    {
        string safeName = SanitizePresetFileName(preset?.preset_name);
        string presetId = string.IsNullOrWhiteSpace(preset?.preset_id) ? CreatePresetId() : preset.preset_id;
        return Path.Combine(ExternalContentPaths.PersistentToneLabPresetDirectory, $"{safeName}--{presetId}.json");
    }

    private static string SanitizePresetFileName(string presetName)
    {
        string trimmed = string.IsNullOrWhiteSpace(presetName) ? "Preset" : presetName.Trim();
        char[] invalidChars = Path.GetInvalidFileNameChars();
        char[] buffer = trimmed.ToCharArray();
        for (int i = 0; i < buffer.Length; i++)
        {
            if (Array.IndexOf(invalidChars, buffer[i]) >= 0)
                buffer[i] = '_';
        }

        string sanitized = new string(buffer).Replace(' ', '_');
        return string.IsNullOrWhiteSpace(sanitized) ? "Preset" : sanitized;
    }

    private static ToneLabSettings CreateSettingsStorageSnapshot(ToneLabSettings source)
    {
        if (source == null)
            return new ToneLabSettings();

        EnsurePresetLibrary(source);
        ToneLabPreset persistedPreset = FindPreset(source, source.selected_preset_id) ?? GetDefaultPreset(source);
        ToneLabSettings snapshot = new ToneLabSettings
        {
            input_device_name = source.input_device_name ?? string.Empty,
            output_device_name = source.output_device_name ?? string.Empty,
            monitoring_buffer_size = source.monitoring_buffer_size,
            selected_preset_id = persistedPreset?.preset_id ?? source.selected_preset_id ?? string.Empty,
            presets = new List<ToneLabPreset>(),
            pedal_chain = ClonePedalChain(persistedPreset?.pedal_chain ?? source.pedal_chain),
            global_input_trim_db = Mathf.Clamp(source.global_input_trim_db, MinGlobalInputTrimDb, MaxGlobalInputTrimDb),
            global_output_gain_db = Mathf.Clamp(source.global_output_gain_db, MinGlobalOutputGainDb, MaxGlobalOutputGainDb),
            input_gain_db = persistedPreset != null ? persistedPreset.input_gain_db : source.input_gain_db,
            output_gain_db = persistedPreset != null ? persistedPreset.output_gain_db : source.output_gain_db
        };

        SyncLegacySettingsFromChain(snapshot);
        return snapshot;
    }

    private static int ComputeRequiredLeadSamples(int sampleRate)
    {
        int bufferLength;
        int numBuffers;
        AudioSettings.GetDSPBufferSize(out bufferLength, out numBuffers);

        int dspLeadSamples = bufferLength * Mathf.Max(1, numBuffers);
        int floorLeadSamples = Mathf.Max(MinimumStartupLeadSamples, Mathf.RoundToInt(sampleRate * 0.012f));
        return Mathf.Max(floorLeadSamples, dspLeadSamples);
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (usingPortAudioBackend)
        {
            if (unityRecorderCaptureActive)
                FillOutputBufferFromUnityRecorderCapture(data, channels);
            else
                Array.Clear(data, 0, data.Length);
            return;
        }

        if (!monitoring || settings == null)
        {
            Array.Clear(data, 0, data.Length);
            return;
        }

        int sampleRate = activeSampleRate > 0 ? activeSampleRate : PreferredSampleRate;
        FillOutputBufferFromMicrophoneRing(data, channels);
        ProcessToneBuffer(data, channels, sampleRate);
        ApplyMonitorVolumeToBuffer(data);
        CaptureProcessedAudioMetrics(data, channels, data.Length / Mathf.Max(1, channels), sampleRate, channels);
    }

    private void ProcessToneBuffer(float[] data, int channels, int sampleRate)
    {
        if (data == null || settings == null)
            return;

        PrepareCompiledPedalChainIfNeeded(sampleRate, channels);

        ToneLabPreset overridePreset = playbackPresetOverrideActive ? playbackPresetOverride : null;
        float inputGainDb = Mathf.Clamp(
            (overridePreset != null ? overridePreset.input_gain_db : settings.input_gain_db) + settings.global_input_trim_db,
            MinRigGainDb,
            MaxRigGainDb);
        float outputGainDb = overridePreset != null ? overridePreset.output_gain_db : settings.output_gain_db;

        float inputGain = ToneLabPedalUtility.DbToLinear(inputGainDb);
        if (!Mathf.Approximately(inputGain, 1f))
        {
            for (int i = 0; i < data.Length; i++)
                data[i] *= inputGain;
        }

        CompiledPedalSlot[] chain = compiledPedalChain;
        for (int i = 0; i < chain.Length; i++)
        {
            CompiledPedalSlot compiledSlot = chain[i];
            if (compiledSlot?.slot == null || !compiledSlot.slot.enabled)
                continue;

            compiledSlot.processor?.Process(data, channels, sampleRate);
            ScrubIntermediateAudioBlock(data);
        }

        float outputGain = ToneLabPedalUtility.DbToLinear(outputGainDb);
        if (!Mathf.Approximately(outputGain, 1f))
        {
            for (int i = 0; i < data.Length; i++)
                data[i] *= outputGain;
        }

        float globalOutputGain = ToneLabPedalUtility.DbToLinear(settings.global_output_gain_db);
        if (!Mathf.Approximately(globalOutputGain, 1f))
        {
            for (int i = 0; i < data.Length; i++)
                data[i] *= globalOutputGain;
        }

        for (int i = 0; i < data.Length; i++)
            data[i] = SanitizeAudioSample(data[i]);
    }

    private void ApplyMonitorVolumeToBuffer(float[] data)
    {
        if (data == null || data.Length == 0)
            return;

        float monitorGain = Mathf.Clamp(monitorVolumePercent / 100f, 0f, MaxMonitorVolumePercent / 100f);
        if (Mathf.Approximately(monitorGain, 1f))
            return;

        for (int i = 0; i < data.Length; i++)
            data[i] *= monitorGain;
    }

    private float[] GetPortAudioProcessBuffer(int sampleCount)
    {
        int safeSampleCount = Mathf.Max(0, sampleCount);
        if (safeSampleCount == 0)
        {
            portAudioProcessBuffer = Array.Empty<float>();
            return portAudioProcessBuffer;
        }

        if (!portAudioProcessBuffersBySampleCount.TryGetValue(safeSampleCount, out float[] buffer) || buffer == null)
        {
            buffer = new float[safeSampleCount];
            portAudioProcessBuffersBySampleCount[safeSampleCount] = buffer;
        }

        portAudioProcessBuffer = buffer;
        return buffer;
    }

    private void EnsureUnityOutputMixBufferCapacity(int sampleCount)
    {
        if (unityOutputMixBuffer == null || unityOutputMixBuffer.Length < sampleCount)
            unityOutputMixBuffer = new float[sampleCount];
    }

    private static void FillProcessBufferFromPortAudioInput(float[] input, int inputChannels, int outputChannels, int frameCount, float[] destination, int sampleCount)
    {
        FillProcessBufferFromPortAudioInput(input, inputChannels, outputChannels, frameCount, destination, sampleCount, SharedAudioInputChannelModes.Input1);
    }

    private static void FillProcessBufferFromPortAudioInput(float[] input, int inputChannels, int outputChannels, int frameCount, float[] destination, int sampleCount, string inputChannelMode)
    {
        if (destination == null)
            return;

        sampleCount = Mathf.Clamp(sampleCount, 0, destination.Length);
        Array.Clear(destination, 0, sampleCount);
        if (input == null || input.Length == 0)
            return;

        int safeInputChannels = Mathf.Max(1, inputChannels);
        string normalizedChannelMode = SharedAudioInputChannelModes.Normalize(inputChannelMode);
        bool monoMix = string.Equals(normalizedChannelMode, SharedAudioInputChannelModes.MonoMix, StringComparison.Ordinal);
        int sourceChannel = string.Equals(normalizedChannelMode, SharedAudioInputChannelModes.Input2, StringComparison.Ordinal) && safeInputChannels > 1
            ? 1
            : 0;
        for (int frame = 0; frame < frameCount; frame++)
        {
            int destinationFrameStart = frame * outputChannels;
            if (destinationFrameStart >= sampleCount)
                break;

            int inputFrameStart = frame * safeInputChannels;
            float monoFallback = inputFrameStart < input.Length ? input[inputFrameStart] : 0f;
            float sample = monoMix
                ? MixPortAudioInputFrameToMono(input, inputFrameStart, safeInputChannels)
                : ReadPortAudioInputChannel(input, inputFrameStart, sourceChannel, monoFallback);
            sample = SanitizeAudioSample(sample);

            for (int channel = 0; channel < outputChannels; channel++)
            {
                int destinationIndex = destinationFrameStart + channel;
                if (destinationIndex >= sampleCount)
                    break;

                destination[destinationIndex] = sample;
            }
        }
    }

    private static float ReadPortAudioInputChannel(float[] input, int frameStart, int sourceChannel, float fallback)
    {
        if (input == null || input.Length == 0)
            return 0f;

        int sourceIndex = frameStart + Mathf.Max(0, sourceChannel);
        return sourceIndex < input.Length ? input[sourceIndex] : fallback;
    }

    private static float MixPortAudioInputFrameToMono(float[] input, int frameStart, int inputChannels)
    {
        if (input == null || input.Length == 0)
            return 0f;

        int mixChannels = Mathf.Min(Mathf.Max(1, inputChannels), 2);
        float sum = 0f;
        int count = 0;
        for (int channel = 0; channel < mixChannels; channel++)
        {
            int index = frameStart + channel;
            if (index >= input.Length)
                break;

            sum += input[index];
            count++;
        }

        return count > 0 ? sum / count : 0f;
    }

    private static void FillPortAudioOutputBufferFromProcessedAudio(float[] processed, int processedChannels, int frameCount, float[] output, int outputChannels)
    {
        if (output == null)
            return;

        Array.Clear(output, 0, output.Length);
        if (processed == null || processed.Length == 0)
            return;

        int safeProcessedChannels = Mathf.Max(1, processedChannels);
        int safeOutputChannels = Mathf.Max(1, outputChannels);

        for (int frame = 0; frame < frameCount; frame++)
        {
            int processedFrameStart = frame * safeProcessedChannels;
            if (processedFrameStart >= processed.Length)
                break;

            int outputFrameStart = frame * safeOutputChannels;
            if (outputFrameStart >= output.Length)
                break;

            float monoFallback = processed[processedFrameStart];
            for (int channel = 0; channel < safeOutputChannels; channel++)
            {
                int outputIndex = outputFrameStart + channel;
                if (outputIndex >= output.Length)
                    break;

                float sample;
                if (safeProcessedChannels == 1)
                {
                    sample = monoFallback;
                }
                else
                {
                    int processedChannel = channel % safeProcessedChannels;
                    int processedIndex = processedFrameStart + processedChannel;
                    sample = processedIndex < processed.Length ? processed[processedIndex] : monoFallback;
                }

                output[outputIndex] = SanitizeAudioSample(sample);
            }
        }
    }

    private static float SanitizeAudioSample(float sample)
    {
        if (float.IsNaN(sample) || float.IsInfinity(sample))
            return 0f;

        return Mathf.Clamp(sample, -1f, 1f);
    }

    private static void ScrubIntermediateAudioBlock(float[] data)
    {
        if (data == null)
            return;

        for (int i = 0; i < data.Length; i++)
        {
            float sample = data[i];
            if (float.IsNaN(sample) || float.IsInfinity(sample))
            {
                data[i] = 0f;
                continue;
            }

            if (sample > MaxIntermediateAudioMagnitude)
                data[i] = MaxIntermediateAudioMagnitude;
            else if (sample < -MaxIntermediateAudioMagnitude)
                data[i] = -MaxIntermediateAudioMagnitude;
        }
    }

    private void FillOutputBufferFromMicrophoneRing(float[] data, int channels)
    {
        lock (microphoneBufferLock)
        {
            for (int frame = 0; frame < data.Length; frame += channels)
            {
                float sample = 0f;
                if (microphoneInputRingCount > 0)
                {
                    sample = microphoneInputRingBuffer[microphoneInputRingReadIndex];
                    microphoneInputRingReadIndex = (microphoneInputRingReadIndex + 1) % microphoneInputRingBuffer.Length;
                    microphoneInputRingCount--;
                }

                for (int channel = 0; channel < channels; channel++)
                    data[frame + channel] = sample;
            }
        }
    }

    private string BuildDeviceCatalogLog(bool includeAllPortAudioInputs = false, bool includeAllPortAudioOutputs = false)
    {
        StringBuilder builder = new StringBuilder();
        int dspBufferLength;
        int dspBufferCount;
        AudioSettings.GetDSPBufferSize(out dspBufferLength, out dspBufferCount);
        builder.Append("unityAudioSampleRate=");
        builder.Append(AudioSettings.outputSampleRate);
        builder.Append(", unityDspBuffer=");
        builder.Append(dspBufferLength);
        builder.Append("x");
        builder.Append(dspBufferCount);
        builder.Append("\n");
        builder.Append("inputChoices=");
        builder.Append(FormatDeviceList(inputDevices));
        builder.Append("\noutputChoices=");
        builder.Append(FormatDeviceList(outputDevices));
        builder.Append("\nportAudioDeviceCount=");
        builder.Append(portAudioAllDevices != null ? portAudioAllDevices.Length : 0);
        builder.Append(", preferredInputs=");
        builder.Append(portAudioInputDevices != null ? portAudioInputDevices.Length : 0);
        builder.Append(", preferredOutputs=");
        builder.Append(portAudioOutputDevices != null ? portAudioOutputDevices.Length : 0);
        builder.Append("\npreferredPortAudioInputs=");
        builder.Append(FormatPortAudioDeviceList(portAudioInputDevices, input: true));
        builder.Append("\npreferredPortAudioOutputs=");
        builder.Append(FormatPortAudioDeviceList(portAudioOutputDevices, input: false));
        if (includeAllPortAudioInputs)
        {
            builder.Append("\nallPortAudioInputs=");
            builder.Append(FormatPortAudioDeviceList(portAudioAllDevices, input: true));
        }
        if (includeAllPortAudioOutputs)
        {
            builder.Append("\nallPortAudioOutputs=");
            builder.Append(FormatPortAudioDeviceList(portAudioAllDevices, input: false));
        }

        return builder.ToString();
    }

    private static string FormatDeviceList(IReadOnlyList<string> devices)
    {
        if (devices == null || devices.Count == 0)
            return "(none)";

        return string.Join("; ", devices.Where(device => !string.IsNullOrWhiteSpace(device)).Select(device => device.Trim()));
    }

    private static string FormatPortAudioDeviceList(IReadOnlyList<ToneLabPortAudio.DeviceDescriptor> devices, bool input)
    {
        if (devices == null || devices.Count == 0)
            return "(none)";

        IEnumerable<ToneLabPortAudio.DeviceDescriptor> filtered = devices.Where(device =>
            device != null && (input ? device.MaxInputChannels > 0 : device.MaxOutputChannels > 0));
        string[] labels = filtered
            .Select(device =>
            {
                int channels = input ? device.MaxInputChannels : device.MaxOutputChannels;
                double latency = input ? device.DefaultLowInputLatency : device.DefaultLowOutputLatency;
                return $"{device.Index}: {device.Name} [{GetNormalizedHostApiLabel(device.HostApiName)}] ({channels}ch, defaultSr={device.DefaultSampleRate:0.#}, lowLatency={latency:0.####})";
            })
            .ToArray();
        return labels.Length == 0 ? "(none)" : string.Join("; ", labels);
    }

    private void LogAudioRouteEvent(string eventName, string details = null, bool warning = false)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("[ToneLabAudio] ");
        builder.Append(string.IsNullOrWhiteSpace(eventName) ? "Event" : eventName.Trim());
        builder.Append(" | backend=");
        builder.Append(ActiveAudioBackendLabel);
        builder.Append(" | hostApi=");
        builder.Append(ActiveHostApiLabel);
        builder.Append(" | input=");
        builder.Append(string.IsNullOrWhiteSpace(inputRouteLabel) ? "-" : inputRouteLabel);
        builder.Append(" | output=");
        builder.Append(string.IsNullOrWhiteSpace(outputRouteLabel) ? "-" : outputRouteLabel);
        builder.Append(" | activeSampleRate=");
        builder.Append(activeSampleRate);
        builder.Append(" | activeBuffer=");
        builder.Append(FormatActiveBufferLabel(activeDspBufferSize));
        builder.Append(" | monitoring=");
        builder.Append(monitoring);
        builder.Append(" | awaitingStartup=");
        builder.Append(awaitingMicrophoneStart);
        builder.Append(" | status=");
        builder.Append(string.IsNullOrWhiteSpace(statusMessage) ? "-" : statusMessage);

        if (settings != null)
        {
            builder.Append(" | settingsInput=");
            builder.Append(string.IsNullOrWhiteSpace(settings.input_device_name) ? "Automatic" : settings.input_device_name);
            builder.Append(" | settingsOutput=");
            builder.Append(string.IsNullOrWhiteSpace(settings.output_device_name) ? "Automatic" : settings.output_device_name);
            builder.Append(" | settingsBuffer=");
            builder.Append(FormatActiveBufferLabel(settings.monitoring_buffer_size));
        }

        if (advancedRoutingOptions != null)
        {
            builder.Append(" | advancedBeta=");
            builder.Append(advancedRoutingOptions.betaEnabled);
            builder.Append(" | inputChannelMode=");
            builder.Append(SharedAudioInputChannelModes.Normalize(advancedRoutingOptions.inputChannelMode));
            builder.Append(" | advancedBackend=");
            builder.Append(SharedAudioBackendModes.Normalize(advancedRoutingOptions.backendMode));
            builder.Append(" | allowFallback=");
            builder.Append(advancedRoutingOptions.allowFallback);
            builder.Append(" | advancedInput=");
            builder.Append(string.IsNullOrWhiteSpace(advancedRoutingOptions.preferredInputDeviceName) ? "Automatic" : advancedRoutingOptions.preferredInputDeviceName);
            builder.Append(" | advancedOutput=");
            builder.Append(string.IsNullOrWhiteSpace(advancedRoutingOptions.preferredOutputDeviceName) ? "Automatic" : advancedRoutingOptions.preferredOutputDeviceName);
            builder.Append(" | advancedSampleRate=");
            builder.Append(SharedAudioSampleRateOptions.ToLabel(advancedRoutingOptions.sampleRate));
            builder.Append(" | advancedBuffer=");
            builder.Append(FormatActiveBufferLabel(advancedRoutingOptions.bufferSize));
            builder.Append(" | splitInputOutput=");
            builder.Append(advancedRoutingOptions.splitInputOutputEnabled);
            builder.Append(" | unifiedOutput=");
            builder.Append(advancedRoutingOptions.unifiedOutputEnabled);
            builder.Append(" | recorderCapture=");
            builder.Append(advancedRoutingOptions.unityRecorderCaptureEnabled);
        }

        if (!string.IsNullOrWhiteSpace(details))
        {
            builder.Append('\n');
            builder.Append(details.Trim());
        }

        string message = builder.ToString();
        if (warning)
            Debug.LogWarning(message);
        else
            Debug.Log(message);
    }

    private AudioClip CreateMonitorDriverClip(int sampleRate)
    {
        int clampedSampleRate = Mathf.Max(1, sampleRate);
        if (monitorDriverClip != null && monitorDriverClip.frequency == clampedSampleRate)
            return monitorDriverClip;

        if (monitorDriverClip != null)
            Destroy(monitorDriverClip);

        monitorDriverClip = AudioClip.Create("UnityToneLabMonitorDriver", clampedSampleRate, 1, clampedSampleRate, false);
        return monitorDriverClip;
    }

    private static float DbToLinear(float decibels)
    {
        return Mathf.Pow(10f, decibels / 20f);
    }

    private static int ResolveMonitoringBufferSize(int requestedBufferSize)
    {
        switch (requestedBufferSize)
        {
            case UltraLowDspBufferSize:
                return UltraLowDspBufferSize;
            case SafeDspBufferSize:
                return SafeDspBufferSize;
            default:
                return PreferredDspBufferSize;
        }
    }

    private static string GetLatencyPresetLabel(int bufferSize)
    {
        if (bufferSize <= 0)
            return "Driver";

        switch (ResolveMonitoringBufferSize(bufferSize))
        {
            case UltraLowDspBufferSize:
                return LatencyPresetLabels[0];
            case SafeDspBufferSize:
                return LatencyPresetLabels[2];
            default:
                return LatencyPresetLabels[1];
        }
    }

    private static string FormatActiveBufferLabel(int bufferSize)
    {
        return bufferSize <= 0 ? "Driver" : bufferSize.ToString();
    }
}
