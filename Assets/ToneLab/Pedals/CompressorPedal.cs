using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class CompressorPedalSettings
{
    public float threshold_db = -18f;
    public float ratio = 3f;
    public float attack_ms = 10f;
    public float release_ms = 120f;
}

public sealed class CompressorPedalProcessor : IToneLabPedalProcessor
{
    private CompressorPedalSettings settings = new CompressorPedalSettings();
    private float envelope;

    public void Prepare(int sampleRate, int channels)
    {
    }

    public void Reset()
    {
        envelope = 0f;
    }

    public void ApplySettings(object settingsObject)
    {
        settings = settingsObject as CompressorPedalSettings ?? new CompressorPedalSettings();
        settings.threshold_db = Mathf.Clamp(settings.threshold_db, -60f, 0f);
        settings.ratio = Mathf.Clamp(settings.ratio, 1f, 8f);
        settings.attack_ms = Mathf.Clamp(settings.attack_ms, 1f, 120f);
        settings.release_ms = Mathf.Clamp(settings.release_ms, 20f, 600f);
    }

    public void Process(float[] data, int channels, int sampleRate)
    {
        if (data == null)
            return;

        float threshold = Mathf.Max(0.0001f, ToneLabPedalUtility.DbToLinear(settings.threshold_db));
        float attackCoeff = Mathf.Exp(-1f / Mathf.Max(0.0001f, settings.attack_ms * 0.001f * sampleRate));
        float releaseCoeff = Mathf.Exp(-1f / Mathf.Max(0.0001f, settings.release_ms * 0.001f * sampleRate));

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
                gain = Mathf.Pow(over, -(settings.ratio - 1f) / settings.ratio);
            }

            for (int channel = 0; channel < channels; channel++)
                data[frame + channel] *= gain;
        }
    }
}

public sealed class CompressorPedalDescriptor : ToneLabPedalDescriptorBase<CompressorPedalSettings, CompressorPedalProcessor>
{
    private static readonly IReadOnlyList<ToneLabPedalParameterDefinition> parameterDefinitions =
        new[]
        {
            new ToneLabPedalParameterDefinition("threshold_db", "Threshold", -60f, 0f, value => $"{value:F0} dB", settingsObject => ((CompressorPedalSettings)settingsObject).threshold_db, (settingsObject, value) => ((CompressorPedalSettings)settingsObject).threshold_db = value),
            new ToneLabPedalParameterDefinition("ratio", "Ratio", 1f, 8f, value => $"{value:F1}:1", settingsObject => ((CompressorPedalSettings)settingsObject).ratio, (settingsObject, value) => ((CompressorPedalSettings)settingsObject).ratio = value),
            new ToneLabPedalParameterDefinition("attack_ms", "Attack", 1f, 120f, value => $"{value:F0} ms", settingsObject => ((CompressorPedalSettings)settingsObject).attack_ms, (settingsObject, value) => ((CompressorPedalSettings)settingsObject).attack_ms = value),
            new ToneLabPedalParameterDefinition("release_ms", "Release", 20f, 600f, value => $"{value:F0} ms", settingsObject => ((CompressorPedalSettings)settingsObject).release_ms, (settingsObject, value) => ((CompressorPedalSettings)settingsObject).release_ms = value)
        };

    public override UnityToneLabRuntime.ToneLabPedalType PedalType => UnityToneLabRuntime.ToneLabPedalType.Compressor;
    public override string DisplayName => "Compressor";
    public override string ShortName => "CP";
    public override string Description => "Linked dynamics control to stabilize attack and output level.";
    public override IReadOnlyList<ToneLabPedalParameterDefinition> Parameters => parameterDefinitions;

    protected override ToneLabPedalAppearance CreateAppearance()
    {
        return new ToneLabPedalAppearance
        {
            BodyColor = new Color(0.34f, 0.52f, 0.69f, 1f),
            FaceColor = new Color(0.71f, 0.84f, 0.93f, 1f),
            LabelStripColor = new Color(0.09f, 0.17f, 0.25f, 0.98f),
            TextColor = new Color(0.95f, 0.98f, 1.00f, 1f),
            SecondaryTextColor = new Color(0.16f, 0.27f, 0.37f, 0.95f),
            EdgeColor = new Color(0.16f, 0.28f, 0.39f, 1f),
            TopEdgeColor = new Color(0.86f, 0.94f, 0.99f, 1f),
            AccentColor = new Color(0.49f, 0.76f, 0.93f, 1f),
            LedOnColor = new Color(0.67f, 0.90f, 0.98f, 1f),
            KnobCount = 0,
            SliderCount = 4,
            DecorationStyle = ToneLabPedalDecorationStyle.MeterDots
        };
    }
}
