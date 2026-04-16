using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ChorusPedalSettings
{
    public float rate_hz = 0.8f;
    public float depth = 0.35f;
    public float mix = 0.25f;
}

public sealed class ChorusPedalProcessor : IToneLabPedalProcessor
{
    private const int MaxChorusMilliseconds = 64;

    private ChorusPedalSettings settings = new ChorusPedalSettings();
    private float[][] delayBuffers;
    private int[] writeIndices;
    private int delayBufferLength;
    private float phase;
    private int preparedSampleRate;
    private int preparedChannels;

    public void Prepare(int sampleRate, int channels)
    {
        preparedSampleRate = Mathf.Max(1, sampleRate);
        preparedChannels = Mathf.Max(1, channels);
        delayBufferLength = Mathf.Max(32, Mathf.CeilToInt(preparedSampleRate * (MaxChorusMilliseconds / 1000f)) + 4);
        delayBuffers = new float[preparedChannels][];
        writeIndices = new int[preparedChannels];
        for (int channel = 0; channel < preparedChannels; channel++)
            delayBuffers[channel] = new float[delayBufferLength];
        phase = 0f;
    }

    public void Reset()
    {
        if (delayBuffers != null)
        {
            for (int channel = 0; channel < delayBuffers.Length; channel++)
            {
                if (delayBuffers[channel] != null)
                    Array.Clear(delayBuffers[channel], 0, delayBuffers[channel].Length);
            }
        }

        if (writeIndices != null)
            Array.Clear(writeIndices, 0, writeIndices.Length);

        phase = 0f;
    }

    public void ApplySettings(object settingsObject)
    {
        settings = settingsObject as ChorusPedalSettings ?? new ChorusPedalSettings();
        settings.rate_hz = Mathf.Clamp(settings.rate_hz, 0.1f, 4f);
        settings.depth = Mathf.Clamp01(settings.depth);
        settings.mix = Mathf.Clamp01(settings.mix);
    }

    public void Process(float[] data, int channels, int sampleRate)
    {
        if (data == null || data.Length == 0 || delayBuffers == null || writeIndices == null || preparedSampleRate <= 0 || preparedChannels != channels || channels <= 0 || delayBufferLength <= 1)
            return;

        int safeChannelCount = Mathf.Min(channels, Mathf.Min(delayBuffers.Length, writeIndices.Length));
        if (safeChannelCount <= 0)
            return;

        float baseDelaySamples = Mathf.Max(2f, preparedSampleRate * 0.012f);
        float modDelaySamples = preparedSampleRate * 0.008f * settings.depth;
        float phaseStep = (2f * Mathf.PI * Mathf.Clamp(settings.rate_hz, 0.05f, 6f)) / preparedSampleRate;

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
                data[frame + channel] = (input * (1f - settings.mix)) + (delayed * settings.mix);
                writeIndices[channel] = (writeIndex + 1) % delayBufferLength;
            }

            phase += phaseStep;
            if (phase > 2f * Mathf.PI)
                phase -= 2f * Mathf.PI;
        }
    }
}

public sealed class ChorusPedalDescriptor : ToneLabPedalDescriptorBase<ChorusPedalSettings, ChorusPedalProcessor>
{
    private static readonly IReadOnlyList<ToneLabPedalParameterDefinition> parameterDefinitions =
        new[]
        {
            new ToneLabPedalParameterDefinition("rate_hz", "Rate", 0.1f, 4f, value => $"{value:F2} Hz", settingsObject => ((ChorusPedalSettings)settingsObject).rate_hz, (settingsObject, value) => ((ChorusPedalSettings)settingsObject).rate_hz = value),
            new ToneLabPedalParameterDefinition("depth", "Depth", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((ChorusPedalSettings)settingsObject).depth, (settingsObject, value) => ((ChorusPedalSettings)settingsObject).depth = value),
            new ToneLabPedalParameterDefinition("mix", "Mix", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((ChorusPedalSettings)settingsObject).mix, (settingsObject, value) => ((ChorusPedalSettings)settingsObject).mix = value)
        };

    public override UnityToneLabRuntime.ToneLabPedalType PedalType => UnityToneLabRuntime.ToneLabPedalType.Chorus;
    public override string DisplayName => "Chorus";
    public override string ShortName => "CH";
    public override string Description => "Short modulated delay for width and movement after the gain stage.";
    public override IReadOnlyList<ToneLabPedalParameterDefinition> Parameters => parameterDefinitions;

    protected override ToneLabPedalAppearance CreateAppearance()
    {
        return new ToneLabPedalAppearance
        {
            BodyColor = new Color(0.13f, 0.50f, 0.56f, 1f),
            FaceColor = new Color(0.38f, 0.74f, 0.78f, 1f),
            LabelStripColor = new Color(0.05f, 0.20f, 0.24f, 0.98f),
            TextColor = new Color(0.94f, 1.00f, 0.98f, 1f),
            SecondaryTextColor = new Color(0.06f, 0.29f, 0.27f, 0.95f),
            EdgeColor = new Color(0.05f, 0.31f, 0.34f, 1f),
            TopEdgeColor = new Color(0.70f, 0.95f, 0.94f, 1f),
            AccentColor = new Color(0.44f, 0.90f, 0.84f, 1f),
            LedOnColor = new Color(0.30f, 0.96f, 0.84f, 1f),
            KnobCount = 3,
            SliderCount = 0,
            DecorationStyle = ToneLabPedalDecorationStyle.WaveBands
        };
    }
}
