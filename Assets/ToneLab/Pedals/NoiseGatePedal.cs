using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class NoiseGatePedalSettings
{
    public float threshold_db = -56f;
    public float attack_ms = 2f;
    public float hold_ms = 28f;
    public float release_ms = 110f;
    public float range_db = -80f;
}

public sealed class NoiseGatePedalProcessor : IToneLabPedalProcessor
{
    private NoiseGatePedalSettings settings = new NoiseGatePedalSettings();
    private float envelope;
    private float gateGain = 1f;
    private int holdSamplesRemaining;
    private int preparedSampleRate;
    private int preparedChannels;

    public void Prepare(int sampleRate, int channels)
    {
        preparedSampleRate = Mathf.Max(1, sampleRate);
        preparedChannels = Mathf.Max(1, channels);
        Reset();
    }

    public void Reset()
    {
        envelope = 0f;
        gateGain = GetFloorGain();
        holdSamplesRemaining = 0;
    }

    public void ApplySettings(object settingsObject)
    {
        settings = settingsObject as NoiseGatePedalSettings ?? new NoiseGatePedalSettings();
        settings.threshold_db = Mathf.Clamp(settings.threshold_db, -80f, -20f);
        settings.attack_ms = Mathf.Clamp(settings.attack_ms, 0.5f, 25f);
        settings.hold_ms = Mathf.Clamp(settings.hold_ms, 0f, 220f);
        settings.release_ms = Mathf.Clamp(settings.release_ms, 20f, 600f);
        settings.range_db = Mathf.Clamp(settings.range_db, -80f, 0f);
        gateGain = Mathf.Clamp(gateGain, GetFloorGain(), 1f);
    }

    public void Process(float[] data, int channels, int sampleRate)
    {
        if (data == null || preparedSampleRate <= 0 || preparedChannels != channels)
            return;

        float threshold = ToneLabPedalUtility.DbToLinear(settings.threshold_db);
        float floor = ToneLabPedalUtility.DbToLinear(settings.range_db);
        float attackCoeff = Mathf.Exp(-1f / Mathf.Max(0.0001f, settings.attack_ms * 0.001f * preparedSampleRate));
        float releaseCoeff = Mathf.Exp(-1f / Mathf.Max(0.0001f, settings.release_ms * 0.001f * preparedSampleRate));
        int holdSamples = Mathf.RoundToInt(settings.hold_ms * 0.001f * preparedSampleRate);

        for (int frame = 0; frame < data.Length; frame += channels)
        {
            float detector = 0f;
            for (int channel = 0; channel < channels; channel++)
                detector = Mathf.Max(detector, Mathf.Abs(data[frame + channel]));

            if (detector > envelope)
                envelope = Mathf.Lerp(detector, envelope, attackCoeff);
            else
                envelope = Mathf.Lerp(detector, envelope, releaseCoeff);

            float targetGain = floor;
            if (envelope >= threshold)
            {
                holdSamplesRemaining = holdSamples;
                targetGain = 1f;
            }
            else if (holdSamplesRemaining > 0)
            {
                holdSamplesRemaining--;
                targetGain = 1f;
            }

            float gainCoeff = targetGain > gateGain ? attackCoeff : releaseCoeff;
            gateGain = Mathf.Lerp(targetGain, gateGain, gainCoeff);

            for (int channel = 0; channel < channels; channel++)
                data[frame + channel] *= gateGain;
        }
    }

    private float GetFloorGain()
    {
        return ToneLabPedalUtility.DbToLinear(settings != null ? settings.range_db : -80f);
    }
}

public sealed class NoiseGatePedalDescriptor : ToneLabPedalDescriptorBase<NoiseGatePedalSettings, NoiseGatePedalProcessor>
{
    private static readonly IReadOnlyList<ToneLabPedalParameterDefinition> parameterDefinitions =
        new[]
        {
            new ToneLabPedalParameterDefinition("threshold_db", "Threshold", -80f, -20f, value => $"{value:F0} dB", settingsObject => ((NoiseGatePedalSettings)settingsObject).threshold_db, (settingsObject, value) => ((NoiseGatePedalSettings)settingsObject).threshold_db = value),
            new ToneLabPedalParameterDefinition("attack_ms", "Attack", 0.5f, 25f, value => $"{value:F1} ms", settingsObject => ((NoiseGatePedalSettings)settingsObject).attack_ms, (settingsObject, value) => ((NoiseGatePedalSettings)settingsObject).attack_ms = value),
            new ToneLabPedalParameterDefinition("hold_ms", "Hold", 0f, 220f, value => $"{value:F0} ms", settingsObject => ((NoiseGatePedalSettings)settingsObject).hold_ms, (settingsObject, value) => ((NoiseGatePedalSettings)settingsObject).hold_ms = value),
            new ToneLabPedalParameterDefinition("release_ms", "Release", 20f, 600f, value => $"{value:F0} ms", settingsObject => ((NoiseGatePedalSettings)settingsObject).release_ms, (settingsObject, value) => ((NoiseGatePedalSettings)settingsObject).release_ms = value),
            new ToneLabPedalParameterDefinition("range_db", "Range", -80f, 0f, value => $"{value:F0} dB", settingsObject => ((NoiseGatePedalSettings)settingsObject).range_db, (settingsObject, value) => ((NoiseGatePedalSettings)settingsObject).range_db = value)
        };

    public override UnityToneLabRuntime.ToneLabPedalType PedalType => UnityToneLabRuntime.ToneLabPedalType.NoiseGate;
    public override string DisplayName => "Noise Gate";
    public override string ShortName => "NG";
    public override string Description => "Fast front-end cleanup that clamps hiss and adapter noise between phrases.";
    public override IReadOnlyList<ToneLabPedalParameterDefinition> Parameters => parameterDefinitions;

    protected override ToneLabPedalAppearance CreateAppearance()
    {
        return new ToneLabPedalAppearance
        {
            BodyColor = new Color(0.33f, 0.36f, 0.40f, 1f),
            FaceColor = new Color(0.72f, 0.75f, 0.79f, 1f),
            LabelStripColor = new Color(0.10f, 0.11f, 0.13f, 0.98f),
            TextColor = new Color(0.97f, 0.98f, 0.99f, 1f),
            SecondaryTextColor = new Color(0.19f, 0.21f, 0.24f, 0.95f),
            EdgeColor = new Color(0.18f, 0.20f, 0.23f, 1f),
            TopEdgeColor = new Color(0.91f, 0.93f, 0.95f, 1f),
            AccentColor = new Color(0.82f, 0.87f, 0.92f, 1f),
            LedOnColor = new Color(0.66f, 0.97f, 0.75f, 1f),
            KnobCount = 4,
            SliderCount = 1,
            DecorationStyle = ToneLabPedalDecorationStyle.MeterDots
        };
    }
}
