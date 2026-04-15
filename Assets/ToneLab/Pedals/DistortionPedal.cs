using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DistortionPedalSettings
{
    public float drive_db = 18f;
}

public sealed class DistortionPedalProcessor : IToneLabPedalProcessor
{
    private DistortionPedalSettings settings = new DistortionPedalSettings();
    private float[] previousInput = Array.Empty<float>();
    private int preparedChannels;

    public void Prepare(int sampleRate, int channels)
    {
        preparedChannels = Mathf.Max(1, channels);
        previousInput = new float[preparedChannels];
    }

    public void Reset()
    {
        if (previousInput.Length > 0)
            Array.Clear(previousInput, 0, previousInput.Length);
    }

    public void ApplySettings(object settingsObject)
    {
        settings = settingsObject as DistortionPedalSettings ?? new DistortionPedalSettings();
        settings.drive_db = Mathf.Clamp(settings.drive_db, 0f, 36f);
    }

    public void Process(float[] data, int channels, int sampleRate)
    {
        if (data == null || preparedChannels != channels)
            return;

        float drive = ToneLabPedalUtility.DbToLinear(settings.drive_db);
        for (int frame = 0; frame < data.Length; frame += channels)
        {
            for (int channel = 0; channel < channels; channel++)
            {
                int index = frame + channel;
                float current = data[index];
                float mid = 0.5f * (previousInput[channel] + current);
                float shapedMid = ToneLabPedalUtility.SoftClipAtan(mid, drive);
                float shapedCurrent = ToneLabPedalUtility.SoftClipAtan(current, drive);
                data[index] = 0.5f * (shapedMid + shapedCurrent);
                previousInput[channel] = current;
            }
        }
    }
}

public sealed class DistortionPedalDescriptor : ToneLabPedalDescriptorBase<DistortionPedalSettings, DistortionPedalProcessor>
{
    private static readonly IReadOnlyList<ToneLabPedalParameterDefinition> parameterDefinitions =
        new[]
        {
            new ToneLabPedalParameterDefinition(
                "drive_db",
                "Drive",
                0f,
                36f,
                value => $"{value:F1} dB",
                settingsObject => ((DistortionPedalSettings)settingsObject).drive_db,
                (settingsObject, value) => ((DistortionPedalSettings)settingsObject).drive_db = value)
        };

    public override UnityToneLabRuntime.ToneLabPedalType PedalType => UnityToneLabRuntime.ToneLabPedalType.Distortion;
    public override string DisplayName => "Distortion";
    public override string ShortName => "DS-1";
    public override string Description => "Soft clipping at the front of the chain for edge, crunch, and sustain.";
    public override IReadOnlyList<ToneLabPedalParameterDefinition> Parameters => parameterDefinitions;

    protected override ToneLabPedalAppearance CreateAppearance()
    {
        return new ToneLabPedalAppearance
        {
            BodyColor = new Color(0.83f, 0.43f, 0.16f, 1f),
            FaceColor = new Color(0.91f, 0.67f, 0.34f, 1f),
            LabelStripColor = new Color(0.16f, 0.09f, 0.07f, 0.98f),
            TextColor = new Color(1.00f, 0.95f, 0.90f, 1f),
            SecondaryTextColor = new Color(0.30f, 0.16f, 0.09f, 0.95f),
            EdgeColor = new Color(0.42f, 0.19f, 0.08f, 1f),
            TopEdgeColor = new Color(1.00f, 0.83f, 0.58f, 1f),
            AccentColor = new Color(0.96f, 0.48f, 0.23f, 1f),
            KnobCount = 3,
            SliderCount = 0,
            DecorationStyle = ToneLabPedalDecorationStyle.Grille
        };
    }
}
