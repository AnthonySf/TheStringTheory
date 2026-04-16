using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class UnityToneLabRuntime : MonoBehaviour
{
    private const int PreferredSampleRate = 48000;
    private const int PreferredDspBufferSize = 128;
    private const int UltraLowDspBufferSize = 64;
    private const int SafeDspBufferSize = 256;
    private const float SettingsSaveDelaySeconds = 0.18f;
    private const int MicrophoneClipLengthSeconds = 1;
    private const float MicrophoneStartupTimeoutSeconds = 2f;
    private const int MinimumStartupLeadSamples = 512;
    private const int MaxDelayMilliseconds = 2500;
    private const int MaxChorusMilliseconds = 64;

    [Serializable]
    public sealed class ToneLabSettings
    {
        public string input_device_name = string.Empty;
        public string output_device_name = string.Empty;
        public int monitoring_buffer_size = PreferredDspBufferSize;
        public string selected_preset_id = string.Empty;
        public List<ToneLabPreset> presets = new List<ToneLabPreset>();
        public List<ToneLabPedalSlot> pedal_chain = new List<ToneLabPedalSlot>();
        public float input_gain_db = 8f;
        public float output_gain_db = 8f;
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
        public float input_gain_db = 8f;
        public float output_gain_db = 8f;
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
        Compressor
    }

    [Serializable]
    public sealed class ToneLabPedalSlot
    {
        public string pedal_instance_id = string.Empty;
        public ToneLabPedalType pedal_type;
        public bool enabled = true;
        public string settings_json = string.Empty;
    }

    private sealed class CompiledPedalSlot
    {
        public ToneLabPedalSlot slot;
        public IToneLabPedalDescriptor descriptor;
        public IToneLabPedalProcessor processor;
    }

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
    private int microphoneInputRingWriteIndex;
    private int microphoneInputRingReadIndex;
    private int microphoneInputRingCount;
    private readonly object microphoneBufferLock = new object();
    private string outputRouteLabel = "System Default Output";
    private string activeHostApiName = string.Empty;
    private ToneLabPortAudio.DuplexStream portAudioStream;
    private ToneLabPortAudio.DeviceDescriptor[] portAudioInputDevices = Array.Empty<ToneLabPortAudio.DeviceDescriptor>();
    private ToneLabPortAudio.DeviceDescriptor[] portAudioOutputDevices = Array.Empty<ToneLabPortAudio.DeviceDescriptor>();
    private bool usingPortAudioBackend;

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
    public string OutputRouteLabel => outputRouteLabel;

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
        EnsureSettingsLoaded();
        RefreshInputDevices();
    }

    private void Update()
    {
        if (settingsDirty && Time.unscaledTime >= nextSettingsSaveTime)
            FlushSettingsToDisk();

        if (usingPortAudioBackend)
            return;

        if (monitoring && pendingMicrophoneClip != null && !string.IsNullOrWhiteSpace(pendingDeviceName))
        {
            int liveMicPosition = Microphone.GetPosition(pendingDeviceName);
            if (liveMicPosition >= 0)
                PumpRecordedMicrophoneSamples(liveMicPosition);
        }

        if (!awaitingMicrophoneStart)
            return;

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
        StopMonitoring();
        FlushSettingsToDisk();
    }

    private void OnDestroy()
    {
        StopMonitoring();
        FlushSettingsToDisk();
        portAudioStream?.Dispose();
        ToneLabPortAudio.Shutdown();
    }

    public void OpenForSession()
    {
        EnsureSettingsLoaded();
        RestoreWorkingRigFromSelectedPreset();
        RefreshInputDevices();
        if (!monitoring && !awaitingMicrophoneStart)
            TryStartMonitoring();
    }

    public void StartBackgroundMonitoring()
    {
        EnsureSettingsLoaded();
        RestoreWorkingRigFromSelectedPreset();
        RefreshInputDevices();
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
        RestoreWorkingRigFromSelectedPreset();
        RebuildCompiledPedalChain();
    }

    public void RefreshInputDevices()
    {
        EnsureSettingsLoaded();
        if (ToneLabPortAudio.TryEnsureInitialized(out string portAudioError))
        {
            ToneLabPortAudio.DeviceDescriptor[] allDevices = ToneLabPortAudio.EnumerateDevices().ToArray();
            portAudioInputDevices = ToneLabPortAudio.GetPreferredInputDevices(allDevices).ToArray();
            portAudioOutputDevices = ToneLabPortAudio.GetPreferredOutputDevices(allDevices).ToArray();
            inputDevices = portAudioInputDevices.Select(device => device.DisplayName).ToArray();
            outputDevices = portAudioOutputDevices.Select(device => device.DisplayName).ToArray();

            if (inputDevices.Length == 0)
            {
                settings.input_device_name = string.Empty;
                settings.output_device_name = string.Empty;
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
            return;
        }

        portAudioInputDevices = Array.Empty<ToneLabPortAudio.DeviceDescriptor>();
        portAudioOutputDevices = Array.Empty<ToneLabPortAudio.DeviceDescriptor>();
        inputDevices = Microphone.devices ?? Array.Empty<string>();
        outputDevices = new[] { "System Default" };
        if (inputDevices.Length == 0)
        {
            settings.input_device_name = string.Empty;
            settings.output_device_name = "System Default";
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

        outputRouteLabel = "System Default Output";
    }

    public void UpdateSettings(Action<ToneLabSettings> mutate, bool restartMonitoring)
    {
        EnsureSettingsLoaded();
        mutate?.Invoke(settings);
        ClampSettings(settings);
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
        string createdInstanceId = string.Empty;
        UpdateSettings(toneSettings =>
        {
            EnsurePedalChain(toneSettings);

            IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(pedalType);
            ToneLabPedalSlot slot = new ToneLabPedalSlot
            {
                pedal_instance_id = CreatePedalInstanceId(),
                pedal_type = pedalType,
                enabled = true,
                settings_json = descriptor.SerializeSettingsObject(descriptor.CreateDefaultSettingsObject())
            };
            toneSettings.pedal_chain.Add(slot);
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

        UpdateSettings(toneSettings =>
        {
            EnsurePedalChain(toneSettings);
            ToneLabPedalSlot slot = FindPedalSlot(toneSettings, pedalInstanceId);
            if (slot == null)
                return;

            IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(slot.pedal_type);
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

    public bool TryStartMonitoring()
    {
        EnsureSettingsLoaded();
        RefreshInputDevices();

        if (portAudioInputDevices.Length > 0)
        {
            StopMonitoringInternal(restoreAudioConfiguration: false);

            ToneLabPortAudio.DeviceDescriptor inputDevice = ResolvePortAudioDevice(settings.input_device_name, portAudioInputDevices) ?? portAudioInputDevices[0];
            ToneLabPortAudio.DeviceDescriptor outputDevice = ResolvePortAudioDevice(settings.output_device_name, portAudioOutputDevices)
                ?? (portAudioOutputDevices.Length > 0 ? portAudioOutputDevices[0] : null);

            if (inputDevice == null || outputDevice == null)
            {
                statusMessage = "No PortAudio duplex route available.";
                return false;
            }

            int sampleRate = ResolveSampleRate(inputDevice, outputDevice);
            int outputChannels = Mathf.Clamp(outputDevice.MaxOutputChannels, 1, 2);
            int monitoringBufferSize = ResolveMonitoringBufferSize(settings.monitoring_buffer_size);
            uint framesPerBuffer = (uint)monitoringBufferSize;

            string portAudioStartError = string.Empty;
            bool started = portAudioStream != null && portAudioStream.Start(
                inputDevice.Index,
                outputDevice.Index,
                1,
                outputChannels,
                sampleRate,
                framesPerBuffer,
                Math.Max(0.0, inputDevice.DefaultLowInputLatency),
                Math.Max(0.0, outputDevice.DefaultLowOutputLatency),
                out portAudioStartError);

            if (!started)
            {
                statusMessage = portAudioStartError;
                Debug.LogWarning($"[UnityToneLabRuntime] PortAudio start failed: {portAudioStartError}");
                return false;
            }

            monitoring = true;
            usingPortAudioBackend = true;
            awaitingMicrophoneStart = false;
            activeSampleRate = sampleRate;
            activeDspBufferSize = monitoringBufferSize;
            activeHostApiName = outputDevice.HostApiName;
            pendingDeviceName = inputDevice.DisplayName;
            outputRouteLabel = outputDevice.DisplayName;
            preparedCompiledPedalSampleRate = -1;
            preparedCompiledPedalChannelCount = -1;
            statusMessage = $"Live  {inputDevice.DisplayName}  \u2022  {sampleRate} Hz  \u2022  Buffer {monitoringBufferSize}  \u2022  PortAudio  {outputDevice.HostApiName}";
            return true;
        }

        if (inputDevices.Length == 0)
        {
            statusMessage = "No microphone inputs found.";
            return false;
        }

        ApplyLowLatencyAudioConfiguration();
        StopMonitoringInternal(restoreAudioConfiguration: false);

        pendingDeviceName = settings.input_device_name;
        if (string.IsNullOrWhiteSpace(pendingDeviceName))
            pendingDeviceName = inputDevices[0];

        try
        {
            pendingMicrophoneClip = Microphone.Start(pendingDeviceName, true, MicrophoneClipLengthSeconds, PreferredSampleRate);
            awaitingMicrophoneStart = pendingMicrophoneClip != null;
            activeHostApiName = awaitingMicrophoneStart ? "Unity Audio" : string.Empty;
            microphoneStartupDeadline = Time.unscaledTime + MicrophoneStartupTimeoutSeconds;
            statusMessage = awaitingMicrophoneStart
                ? $"Starting live monitoring on {pendingDeviceName}..."
                : "Failed to allocate microphone clip.";
            return awaitingMicrophoneStart;
        }
        catch (Exception ex)
        {
            awaitingMicrophoneStart = false;
            pendingMicrophoneClip = null;
            statusMessage = $"Microphone start failed: {ex.Message}";
            Debug.LogWarning($"[UnityToneLabRuntime] Failed to start microphone '{pendingDeviceName}': {ex.Message}");
            return false;
        }
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

    private void StopMonitoringInternal(bool restoreAudioConfiguration)
    {
        usingPortAudioBackend = false;
        if (portAudioStream != null && portAudioStream.IsRunning)
            portAudioStream.Stop();

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
        microphoneInputRingWriteIndex = 0;
        microphoneInputRingReadIndex = 0;
        microphoneInputRingCount = 0;

        statusMessage = "Stopped";
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

        int bufferLength;
        int numBuffers;
        AudioSettings.GetDSPBufferSize(out bufferLength, out numBuffers);
        float latencyMs = 1000f * bufferLength * Mathf.Max(1, numBuffers) / Mathf.Max(1f, activeSampleRate);
        float startupLeadMs = 1000f * requiredLeadSamples / Mathf.Max(1f, activeSampleRate);
        statusMessage = $"Live  {pendingDeviceName}  \u2022  {activeSampleRate} Hz  \u2022  Buffer {bufferLength} x {numBuffers}  \u2022  Startup {startupLeadMs:F1} ms  \u2022  ~{latencyMs:F1} ms";
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

        lock (microphoneBufferLock)
        {
            for (int i = 0; i < framesToCopy; i++)
            {
                int frameIndex = microphoneClipReadFramePosition + i;
                if (frameIndex >= microphoneClipFrameCount)
                    frameIndex -= microphoneClipFrameCount;

                int sampleIndex = frameIndex * microphoneClipChannelCount;
                float monoSample = microphoneSnapshotBuffer[sampleIndex];
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

        microphoneClipReadFramePosition = micPosition;
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

        ExternalContentBootstrap.EnsureRuntimeContentReady();
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

    private void ProcessPortAudioBlock(float[] input, int inputChannels, int outputChannels, int frameCount, float[] output)
    {
        if (input == null || output == null)
            return;

        int safeOutputChannels = Mathf.Max(1, outputChannels);
        int processingChannels = safeOutputChannels > 1 ? 2 : 1;
        int processingSampleCount = frameCount * processingChannels;
        EnsurePortAudioProcessBufferCapacity(processingSampleCount);
        FillProcessBufferFromPortAudioInput(input, inputChannels, processingChannels, frameCount, portAudioProcessBuffer, processingSampleCount);

        int sampleRate = activeSampleRate > 0 ? activeSampleRate : PreferredSampleRate;
        ProcessToneBuffer(portAudioProcessBuffer, processingChannels, sampleRate);
        FillPortAudioOutputBufferFromProcessedAudio(portAudioProcessBuffer, processingChannels, frameCount, output, safeOutputChannels);
    }

    private static void ClampSettings(ToneLabSettings toneSettings)
    {
        if (toneSettings == null)
            return;

        toneSettings.monitoring_buffer_size = ResolveMonitoringBufferSize(toneSettings.monitoring_buffer_size);
        EnsurePresetLibrary(toneSettings);
        EnsurePedalChain(toneSettings);
        toneSettings.input_gain_db = Mathf.Clamp(toneSettings.input_gain_db, -24f, 24f);
        toneSettings.output_gain_db = Mathf.Clamp(toneSettings.output_gain_db, -24f, 24f);
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

        EnsurePedalChain(settings);
        List<CompiledPedalSlot> rebuiltChain = new List<CompiledPedalSlot>(settings.pedal_chain.Count);
        for (int i = 0; i < settings.pedal_chain.Count; i++)
        {
            ToneLabPedalSlot slot = settings.pedal_chain[i];
            if (slot == null)
                continue;

            IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(slot.pedal_type);
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
        normalized.preset_name = MakeUniquePresetName(normalized.preset_name, presetNames);
        normalized.input_gain_db = Mathf.Clamp(normalized.input_gain_db, -24f, 24f);
        normalized.output_gain_db = Mathf.Clamp(normalized.output_gain_db, -24f, 24f);

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

    private static ToneLabPedalSlot NormalizePedalSlot(ToneLabSettings toneSettings, ToneLabPedalSlot slot)
    {
        ToneLabPedalSlot normalized = ClonePedalSlot(slot);
        if (normalized == null)
            return null;

        IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(normalized.pedal_type);
        if (string.IsNullOrWhiteSpace(normalized.pedal_instance_id))
            normalized.pedal_instance_id = CreatePedalInstanceId();

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

        IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(normalized.pedal_type);
        if (string.IsNullOrWhiteSpace(normalized.pedal_instance_id))
            normalized.pedal_instance_id = CreatePedalInstanceId();
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

        IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(slot.pedal_type);
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
            if (preset != null && string.Equals(preset.preset_name, "Blues", StringComparison.OrdinalIgnoreCase))
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

    private static bool LooksLikeOutdatedFactoryPreset(ToneLabPreset preset)
    {
        if (preset == null || string.IsNullOrWhiteSpace(preset.preset_name))
            return false;

        if (string.Equals(preset.preset_name, "Metal", StringComparison.OrdinalIgnoreCase))
        {
            DistortionPedalSettings distortion = TryGetPedalSettings<DistortionPedalSettings>(preset, ToneLabPedalType.Distortion);
            AmpPedalSettings amp = TryGetPedalSettings<AmpPedalSettings>(preset, ToneLabPedalType.Amp);
            StudioEqPedalSettings eq = TryGetPedalSettings<StudioEqPedalSettings>(preset, ToneLabPedalType.StudioEq);
            return distortion != null &&
                   amp != null &&
                   eq != null &&
                   Mathf.Abs(distortion.drive_db - 8.5f) < 0.25f &&
                   Mathf.Abs(amp.gain_db - 32f) < 0.5f &&
                   Mathf.Abs(eq.mid_db - (-2.5f)) < 0.25f;
        }

        return false;
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

            IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(slot.pedal_type);
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
        toneSettings.input_gain_db = Mathf.Clamp(preset.input_gain_db, -24f, 24f);
        toneSettings.output_gain_db = Mathf.Clamp(preset.output_gain_db, -24f, 24f);
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
            input_gain_db = Mathf.Clamp(toneSettings.input_gain_db, -24f, 24f),
            output_gain_db = Mathf.Clamp(toneSettings.output_gain_db, -24f, 24f),
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
            CreateMetalPreset()
        };
    }

    private static ToneLabPreset CreateCleanPreset()
    {
        return BuildPreset(
            "Clean",
            inputGainDb: 8f,
            outputGainDb: 7.5f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -66f,
                attack_ms = 1.4f,
                hold_ms = 18f,
                release_ms = 95f,
                range_db = -76f
            }),
            CreatePresetSlot(ToneLabPedalType.Compressor, true, new CompressorPedalSettings
            {
                threshold_db = -26f,
                ratio = 2.2f,
                attack_ms = 18f,
                release_ms = 160f
            }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 10.5f,
                tone = 0.58f,
                presence = 0.40f,
                master_db = -1.0f,
                sag = 0.10f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.40f,
                presence = 0.35f,
                air = 0.60f,
                mix = 1.0f
            }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 72f,
                low_shelf_db = -0.5f,
                mid_db = -1.5f,
                high_shelf_db = -0.8f,
                high_cut_hz = 7600f
            }),
            CreatePresetSlot(ToneLabPedalType.Chorus, true, new ChorusPedalSettings
            {
                rate_hz = 0.42f,
                depth = 0.18f,
                mix = 0.12f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.11f,
                feedback = 0.10f,
                mix = 0.08f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.22f,
                damping = 0.55f,
                wet = 0.09f,
                dry = 1.00f,
                width = 0.90f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateBluesPreset()
    {
        return BuildPreset(
            "Blues",
            inputGainDb: 9f,
            outputGainDb: 8f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -62f,
                attack_ms = 1.2f,
                hold_ms = 22f,
                release_ms = 105f,
                range_db = -72f
            }),
            CreatePresetSlot(ToneLabPedalType.Compressor, true, new CompressorPedalSettings
            {
                threshold_db = -24f,
                ratio = 2.6f,
                attack_ms = 14f,
                release_ms = 140f
            }),
            CreatePresetSlot(ToneLabPedalType.Distortion, true, new DistortionPedalSettings
            {
                drive_db = 8.5f
            }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 18f,
                tone = 0.48f,
                presence = 0.43f,
                master_db = -1.6f,
                sag = 0.18f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.58f,
                presence = 0.40f,
                air = 0.44f,
                mix = 1f
            }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 78f,
                low_shelf_db = 1.5f,
                mid_db = 0.8f,
                high_shelf_db = -1.4f,
                high_cut_hz = 6700f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.14f,
                feedback = 0.15f,
                mix = 0.10f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.18f,
                damping = 0.48f,
                wet = 0.08f,
                dry = 1.00f,
                width = 0.88f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateJazzPreset()
    {
        return BuildPreset(
            "Jazz",
            inputGainDb: 8f,
            outputGainDb: 7f,
            CreatePresetSlot(ToneLabPedalType.Compressor, true, new CompressorPedalSettings
            {
                threshold_db = -20f,
                ratio = 2.4f,
                attack_ms = 22f,
                release_ms = 180f
            }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 8f,
                tone = 0.38f,
                presence = 0.22f,
                master_db = -1.2f,
                sag = 0.08f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.46f,
                presence = 0.26f,
                air = 0.38f,
                mix = 1f
            }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 74f,
                low_shelf_db = 1.8f,
                mid_db = -1.2f,
                high_shelf_db = -3.0f,
                high_cut_hz = 5400f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.08f,
                feedback = 0.08f,
                mix = 0.04f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.18f,
                damping = 0.58f,
                wet = 0.08f,
                dry = 1.00f,
                width = 0.86f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateEdgyPreset()
    {
        return BuildPreset(
            "Edgy",
            inputGainDb: 9f,
            outputGainDb: 6.5f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -58f,
                attack_ms = 1.0f,
                hold_ms = 24f,
                release_ms = 95f,
                range_db = -78f
            }),
            CreatePresetSlot(ToneLabPedalType.Compressor, true, new CompressorPedalSettings
            {
                threshold_db = -24f,
                ratio = 2.8f,
                attack_ms = 12f,
                release_ms = 120f
            }),
            CreatePresetSlot(ToneLabPedalType.Distortion, true, new DistortionPedalSettings
            {
                drive_db = 13f
            }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 24f,
                tone = 0.57f,
                presence = 0.58f,
                master_db = -1.5f,
                sag = 0.22f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.60f,
                presence = 0.50f,
                air = 0.45f,
                mix = 1f
            }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 84f,
                low_shelf_db = 0.5f,
                mid_db = 1.8f,
                high_shelf_db = 0.4f,
                high_cut_hz = 6900f
            }),
            CreatePresetSlot(ToneLabPedalType.Phaser, true, new PhaserPedalSettings
            {
                rate_hz = 0.30f,
                depth = 0.20f,
                mix = 0.10f,
                center_hz = 900f,
                feedback = 0.03f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.22f,
                feedback = 0.18f,
                mix = 0.10f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.14f,
                damping = 0.50f,
                wet = 0.07f,
                dry = 1.00f,
                width = 0.84f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset CreateMetalPreset()
    {
        return BuildPreset(
            "Metal",
            inputGainDb: 10f,
            outputGainDb: 5.0f,
            CreatePresetSlot(ToneLabPedalType.NoiseGate, true, new NoiseGatePedalSettings
            {
                threshold_db = -48f,
                attack_ms = 0.8f,
                hold_ms = 34f,
                release_ms = 90f,
                range_db = -80f
            }),
            CreatePresetSlot(ToneLabPedalType.Compressor, true, new CompressorPedalSettings
            {
                threshold_db = -22f,
                ratio = 2.8f,
                attack_ms = 5f,
                release_ms = 60f
            }),
            CreatePresetSlot(ToneLabPedalType.Distortion, true, new DistortionPedalSettings
            {
                drive_db = 16.5f
            }),
            CreatePresetSlot(ToneLabPedalType.Amp, true, new AmpPedalSettings
            {
                gain_db = 37f,
                tone = 0.42f,
                presence = 0.72f,
                master_db = -2.8f,
                sag = 0.12f
            }),
            CreatePresetSlot(ToneLabPedalType.CabSim, true, new CabSimPedalSettings
            {
                thump = 0.82f,
                presence = 0.64f,
                air = 0.22f,
                mix = 1f
            }),
            CreatePresetSlot(ToneLabPedalType.StudioEq, true, new StudioEqPedalSettings
            {
                low_cut_hz = 82f,
                low_shelf_db = -0.3f,
                mid_db = -4.2f,
                high_shelf_db = 1.3f,
                high_cut_hz = 5600f
            }),
            CreatePresetSlot(ToneLabPedalType.Delay, true, new DelayPedalSettings
            {
                delay_seconds = 0.28f,
                feedback = 0.10f,
                mix = 0.05f
            }),
            CreatePresetSlot(ToneLabPedalType.Reverb, true, new ReverbPedalSettings
            {
                room_size = 0.08f,
                damping = 0.66f,
                wet = 0.03f,
                dry = 1.00f,
                width = 0.76f,
                freeze = 0f
            }));
    }

    private static ToneLabPreset BuildPreset(string presetName, float inputGainDb, float outputGainDb, params ToneLabPedalSlot[] pedalSlots)
    {
        return new ToneLabPreset
        {
            preset_id = CreatePresetId(),
            preset_name = presetName,
            input_gain_db = Mathf.Clamp(inputGainDb, -24f, 24f),
            output_gain_db = Mathf.Clamp(outputGainDb, -24f, 24f),
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
            enabled = enabled,
            settings_json = descriptor.SerializeSettingsObject(settingsObject ?? descriptor.CreateDefaultSettingsObject())
        };
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

        ToneLabPreset persistedPreset = FindPreset(source, source.selected_preset_id) ?? GetDefaultPreset(source);
        ToneLabSettings snapshot = new ToneLabSettings
        {
            input_device_name = source.input_device_name ?? string.Empty,
            output_device_name = source.output_device_name ?? string.Empty,
            monitoring_buffer_size = source.monitoring_buffer_size,
            selected_preset_id = persistedPreset?.preset_id ?? source.selected_preset_id ?? string.Empty,
            presets = new List<ToneLabPreset>(),
            pedal_chain = ClonePedalChain(persistedPreset?.pedal_chain ?? source.pedal_chain),
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
    }

    private void ProcessToneBuffer(float[] data, int channels, int sampleRate)
    {
        if (data == null || settings == null)
            return;

        PrepareCompiledPedalChainIfNeeded(sampleRate, channels);

        float inputGain = ToneLabPedalUtility.DbToLinear(settings.input_gain_db);
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
        }

        float outputGain = ToneLabPedalUtility.DbToLinear(settings.output_gain_db);
        if (!Mathf.Approximately(outputGain, 1f))
        {
            for (int i = 0; i < data.Length; i++)
                data[i] *= outputGain;
        }

        for (int i = 0; i < data.Length; i++)
            data[i] = Mathf.Clamp(data[i], -1f, 1f);
    }

    private void EnsurePortAudioProcessBufferCapacity(int sampleCount)
    {
        if (portAudioProcessBuffer == null || portAudioProcessBuffer.Length != sampleCount)
            portAudioProcessBuffer = new float[sampleCount];
    }

    private static void FillProcessBufferFromPortAudioInput(float[] input, int inputChannels, int outputChannels, int frameCount, float[] destination, int sampleCount)
    {
        Array.Clear(destination, 0, sampleCount);
        if (input == null || input.Length == 0)
            return;

        int safeInputChannels = Mathf.Max(1, inputChannels);
        for (int frame = 0; frame < frameCount; frame++)
        {
            int destinationFrameStart = frame * outputChannels;
            if (destinationFrameStart >= sampleCount)
                break;

            int inputFrameStart = frame * safeInputChannels;
            float monoFallback = inputFrameStart < input.Length ? input[inputFrameStart] : 0f;

            for (int channel = 0; channel < outputChannels; channel++)
            {
                int destinationIndex = destinationFrameStart + channel;
                if (destinationIndex >= sampleCount)
                    break;

                int sourceIndex = inputFrameStart + Mathf.Min(channel, safeInputChannels - 1);
                float sample = sourceIndex < input.Length ? input[sourceIndex] : monoFallback;
                destination[destinationIndex] = sample;
            }
        }
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

                output[outputIndex] = sample;
            }
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
}
