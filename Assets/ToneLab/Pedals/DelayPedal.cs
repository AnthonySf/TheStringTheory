using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DelayPedalSettings
{
    public float delay_seconds = 0.28f;
    public float feedback = 0.30f;
    public float mix = 0.20f;
}

public sealed class DelayPedalProcessor : IToneLabPedalProcessor
{
    private const int MaxDelayMilliseconds = 2500;

    private DelayPedalSettings settings = new DelayPedalSettings();
    private float[][] delayBuffers;
    private int[] writeIndices;
    private int delayBufferLength;
    private int preparedSampleRate;
    private int preparedChannels;

    public void Prepare(int sampleRate, int channels)
    {
        preparedSampleRate = Mathf.Max(1, sampleRate);
        preparedChannels = Mathf.Max(1, channels);
        delayBufferLength = Mathf.Max(64, Mathf.CeilToInt(preparedSampleRate * (MaxDelayMilliseconds / 1000f)) + 4);
        delayBuffers = new float[preparedChannels][];
        writeIndices = new int[preparedChannels];
        for (int channel = 0; channel < preparedChannels; channel++)
            delayBuffers[channel] = new float[delayBufferLength];
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
    }

    public void ApplySettings(object settingsObject)
    {
        settings = settingsObject as DelayPedalSettings ?? new DelayPedalSettings();
        settings.delay_seconds = Mathf.Clamp(settings.delay_seconds, 0.02f, MaxDelayMilliseconds / 1000f);
        settings.feedback = Mathf.Clamp(settings.feedback, 0f, 0.95f);
        settings.mix = Mathf.Clamp01(settings.mix);
    }

    public void Process(float[] data, int channels, int sampleRate)
    {
        if (data == null || delayBuffers == null || preparedSampleRate <= 0 || preparedChannels != channels)
            return;

        int delaySamples = Mathf.Clamp(Mathf.RoundToInt(settings.delay_seconds * preparedSampleRate), 1, delayBufferLength - 2);

        for (int frame = 0; frame < data.Length; frame += channels)
        {
            for (int channel = 0; channel < channels; channel++)
            {
                float input = data[frame + channel];
                int readIndex = writeIndices[channel] - delaySamples;
                if (readIndex < 0)
                    readIndex += delayBufferLength;

                float delayed = delayBuffers[channel][readIndex];
                delayBuffers[channel][writeIndices[channel]] = input + (delayed * settings.feedback);
                data[frame + channel] = (input * (1f - settings.mix)) + (delayed * settings.mix);
                writeIndices[channel] = (writeIndices[channel] + 1) % delayBufferLength;
            }
        }
    }
}

public sealed class DelayPedalDescriptor : ToneLabPedalDescriptorBase<DelayPedalSettings, DelayPedalProcessor>
{
    private static readonly IReadOnlyList<ToneLabPedalParameterDefinition> parameterDefinitions =
        new[]
        {
            new ToneLabPedalParameterDefinition("delay_seconds", "Time", 0.02f, 1.5f, value => $"{value:F2} s", settingsObject => ((DelayPedalSettings)settingsObject).delay_seconds, (settingsObject, value) => ((DelayPedalSettings)settingsObject).delay_seconds = value),
            new ToneLabPedalParameterDefinition("feedback", "Feedback", 0f, 0.95f, value => $"{value * 100f:F0}%", settingsObject => ((DelayPedalSettings)settingsObject).feedback, (settingsObject, value) => ((DelayPedalSettings)settingsObject).feedback = value),
            new ToneLabPedalParameterDefinition("mix", "Mix", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((DelayPedalSettings)settingsObject).mix, (settingsObject, value) => ((DelayPedalSettings)settingsObject).mix = value)
        };

    public override UnityToneLabRuntime.ToneLabPedalType PedalType => UnityToneLabRuntime.ToneLabPedalType.Delay;
    public override string DisplayName => "Delay";
    public override string ShortName => "DL";
    public override string Description => "Feedback echo pedal for rhythmic repeats and trailing ambience.";
    public override IReadOnlyList<ToneLabPedalParameterDefinition> Parameters => parameterDefinitions;

    protected override ToneLabPedalAppearance CreateAppearance()
    {
        return new ToneLabPedalAppearance
        {
            BodyColor = new Color(0.56f, 0.74f, 0.76f, 1f),
            FaceColor = new Color(0.78f, 0.92f, 0.93f, 1f),
            LabelStripColor = new Color(0.10f, 0.30f, 0.32f, 0.98f),
            TextColor = new Color(0.95f, 1.00f, 0.99f, 1f),
            SecondaryTextColor = new Color(0.13f, 0.33f, 0.34f, 0.95f),
            EdgeColor = new Color(0.17f, 0.41f, 0.42f, 1f),
            TopEdgeColor = new Color(0.92f, 0.98f, 0.98f, 1f),
            AccentColor = new Color(0.29f, 0.64f, 0.67f, 1f),
            LedOnColor = new Color(0.46f, 0.95f, 0.92f, 1f),
            KnobCount = 2,
            SliderCount = 1,
            DecorationStyle = ToneLabPedalDecorationStyle.CenterStripe
        };
    }
}
