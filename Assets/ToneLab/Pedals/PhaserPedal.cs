using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class PhaserPedalSettings
{
    public float rate_hz = 0.5f;
    public float depth = 0.5f;
    public float mix = 0.25f;
    public float center_hz = 1200f;
    public float feedback = 0.1f;
}

public sealed class PhaserPedalProcessor : IToneLabPedalProcessor
{
    private const int StageCount = 4;

    private PhaserPedalSettings settings = new PhaserPedalSettings();
    private float[,] stageInputHistory;
    private float[,] stageOutputHistory;
    private float[] feedbackHistory;
    private float phase;
    private int preparedSampleRate;
    private int preparedChannels;

    public void Prepare(int sampleRate, int channels)
    {
        preparedSampleRate = Mathf.Max(1, sampleRate);
        preparedChannels = Mathf.Max(1, channels);
        stageInputHistory = new float[preparedChannels, StageCount];
        stageOutputHistory = new float[preparedChannels, StageCount];
        feedbackHistory = new float[preparedChannels];
        phase = 0f;
    }

    public void Reset()
    {
        if (stageInputHistory != null)
            Array.Clear(stageInputHistory, 0, stageInputHistory.Length);
        if (stageOutputHistory != null)
            Array.Clear(stageOutputHistory, 0, stageOutputHistory.Length);
        if (feedbackHistory != null)
            Array.Clear(feedbackHistory, 0, feedbackHistory.Length);
        phase = 0f;
    }

    public void ApplySettings(object settingsObject)
    {
        settings = settingsObject as PhaserPedalSettings ?? new PhaserPedalSettings();
        settings.rate_hz = Mathf.Clamp(settings.rate_hz, 0.1f, 3f);
        settings.depth = Mathf.Clamp01(settings.depth);
        settings.mix = Mathf.Clamp01(settings.mix);
        settings.center_hz = Mathf.Clamp(settings.center_hz, 120f, 4200f);
        settings.feedback = Mathf.Clamp(settings.feedback, -0.9f, 0.9f);
    }

    public void Process(float[] data, int channels, int sampleRate)
    {
        if (data == null || stageInputHistory == null || preparedSampleRate <= 0 || preparedChannels != channels)
            return;

        float clampedFeedback = Mathf.Clamp(settings.feedback, -0.92f, 0.92f);
        float phaseStep = (2f * Mathf.PI * Mathf.Clamp(settings.rate_hz, 0.05f, 5f)) / preparedSampleRate;

        for (int frame = 0; frame < data.Length; frame += channels)
        {
            float lfo = 0.5f + (0.5f * Mathf.Sin(phase));
            float frequency = Mathf.Lerp(settings.center_hz * (1f - (0.75f * settings.depth)), settings.center_hz * (1f + (1.25f * settings.depth)), lfo);
            float tangent = Mathf.Tan(Mathf.PI * Mathf.Clamp(frequency, 20f, preparedSampleRate * 0.45f) / preparedSampleRate);
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
                data[frame + channel] = (input * (1f - settings.mix)) + (stageValue * settings.mix);
            }

            phase += phaseStep;
            if (phase > 2f * Mathf.PI)
                phase -= 2f * Mathf.PI;
        }
    }
}

public sealed class PhaserPedalDescriptor : ToneLabPedalDescriptorBase<PhaserPedalSettings, PhaserPedalProcessor>
{
    private static readonly IReadOnlyList<ToneLabPedalParameterDefinition> parameterDefinitions =
        new[]
        {
            new ToneLabPedalParameterDefinition("rate_hz", "Rate", 0.1f, 3f, value => $"{value:F2} Hz", settingsObject => ((PhaserPedalSettings)settingsObject).rate_hz, (settingsObject, value) => ((PhaserPedalSettings)settingsObject).rate_hz = value),
            new ToneLabPedalParameterDefinition("depth", "Depth", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((PhaserPedalSettings)settingsObject).depth, (settingsObject, value) => ((PhaserPedalSettings)settingsObject).depth = value),
            new ToneLabPedalParameterDefinition("mix", "Mix", 0f, 1f, value => $"{value * 100f:F0}%", settingsObject => ((PhaserPedalSettings)settingsObject).mix, (settingsObject, value) => ((PhaserPedalSettings)settingsObject).mix = value),
            new ToneLabPedalParameterDefinition("center_hz", "Center", 120f, 4200f, value => $"{value:F0} Hz", settingsObject => ((PhaserPedalSettings)settingsObject).center_hz, (settingsObject, value) => ((PhaserPedalSettings)settingsObject).center_hz = value),
            new ToneLabPedalParameterDefinition("feedback", "Feedback", -0.9f, 0.9f, value => value.ToString("F2"), settingsObject => ((PhaserPedalSettings)settingsObject).feedback, (settingsObject, value) => ((PhaserPedalSettings)settingsObject).feedback = value)
        };

    public override UnityToneLabRuntime.ToneLabPedalType PedalType => UnityToneLabRuntime.ToneLabPedalType.Phaser;
    public override string DisplayName => "Phaser";
    public override string ShortName => "PH";
    public override string Description => "Animated all-pass sweep that shifts the harmonic focus of the note.";
    public override IReadOnlyList<ToneLabPedalParameterDefinition> Parameters => parameterDefinitions;

    protected override ToneLabPedalAppearance CreateAppearance()
    {
        return new ToneLabPedalAppearance
        {
            BodyColor = new Color(0.39f, 0.29f, 0.57f, 1f),
            FaceColor = new Color(0.66f, 0.58f, 0.82f, 1f),
            LabelStripColor = new Color(0.15f, 0.11f, 0.24f, 0.98f),
            TextColor = new Color(0.98f, 0.95f, 1.00f, 1f),
            SecondaryTextColor = new Color(0.23f, 0.18f, 0.36f, 0.95f),
            EdgeColor = new Color(0.24f, 0.15f, 0.39f, 1f),
            TopEdgeColor = new Color(0.84f, 0.78f, 0.98f, 1f),
            AccentColor = new Color(0.77f, 0.62f, 0.96f, 1f),
            LedOnColor = new Color(0.86f, 0.70f, 0.98f, 1f),
            KnobCount = 4,
            SliderCount = 0,
            DecorationStyle = ToneLabPedalDecorationStyle.SweepBars
        };
    }
}
