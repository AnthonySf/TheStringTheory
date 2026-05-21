using System;
using System.Collections.Generic;
using UnityEngine;

public interface IToneLabPedalProcessor
{
    void Prepare(int sampleRate, int channels);
    void Reset();
    void ApplySettings(object settingsObject);
    void Process(float[] data, int channels, int sampleRate);
}

public interface IToneLabPedalDescriptor
{
    string DescriptorId { get; }
    UnityToneLabRuntime.ToneLabPedalType PedalType { get; }
    string DisplayName { get; }
    string ShortName { get; }
    string Description { get; }
    ToneLabPedalAppearance Appearance { get; }
    IReadOnlyList<ToneLabPedalParameterDefinition> Parameters { get; }
    object CreateDefaultSettingsObject();
    object DeserializeSettingsObject(string settingsJson);
    string SerializeSettingsObject(object settingsObject);
    IToneLabPedalProcessor CreateProcessor();
}

public enum ToneLabPedalDecorationStyle
{
    None,
    Grille,
    WaveBands,
    SweepBars,
    CenterStripe,
    SparkBars,
    MeterDots
}

public sealed class ToneLabPedalAppearance
{
    public Color BodyColor { get; set; } = new Color(0.70f, 0.49f, 0.18f, 1f);
    public Color FaceColor { get; set; } = new Color(0.80f, 0.64f, 0.34f, 1f);
    public Color LabelStripColor { get; set; } = new Color(0.08f, 0.09f, 0.11f, 0.98f);
    public Color TextColor { get; set; } = new Color(0.98f, 0.96f, 0.90f, 1f);
    public Color SecondaryTextColor { get; set; } = new Color(0.22f, 0.15f, 0.05f, 0.90f);
    public Color EdgeColor { get; set; } = new Color(0.29f, 0.20f, 0.09f, 1f);
    public Color TopEdgeColor { get; set; } = new Color(1f, 0.87f, 0.53f, 1f);
    public Color AccentColor { get; set; } = new Color(0.93f, 0.76f, 0.45f, 1f);
    public Color ShadowColor { get; set; } = new Color(0.01f, 0.01f, 0.02f, 0.26f);
    public Color KnobColor { get; set; } = new Color(0.18f, 0.18f, 0.20f, 1f);
    public Color KnobIndicatorColor { get; set; } = new Color(0.97f, 0.88f, 0.68f, 1f);
    public Color LedOnColor { get; set; } = new Color(0.40f, 1.00f, 0.56f, 1f);
    public Color LedOffColor { get; set; } = new Color(0.16f, 0.19f, 0.23f, 1f);
    public Color FootswitchColor { get; set; } = new Color(0.69f, 0.45f, 0.16f, 1f);
    public Color FootswitchTextColor { get; set; } = new Color(0.99f, 0.94f, 0.86f, 1f);
    public int KnobCount { get; set; } = 3;
    public int SliderCount { get; set; } = 0;
    public ToneLabPedalDecorationStyle DecorationStyle { get; set; } = ToneLabPedalDecorationStyle.Grille;
    public string FooterEnabledText { get; set; } = "ENGAGED";
    public string FooterBypassedText { get; set; } = "BYPASS";

    public static ToneLabPedalAppearance CreateDefault()
    {
        return new ToneLabPedalAppearance();
    }
}

public sealed class ToneLabPedalParameterDefinition
{
    private readonly Func<object, float> getter;
    private readonly Action<object, float> setter;

    public ToneLabPedalParameterDefinition(
        string parameterId,
        string displayName,
        float minimumValue,
        float maximumValue,
        Func<float, string> formatter,
        Func<object, float> getter,
        Action<object, float> setter)
    {
        ParameterId = parameterId;
        DisplayName = displayName;
        MinimumValue = minimumValue;
        MaximumValue = maximumValue;
        Formatter = formatter ?? (value => value.ToString("F2"));
        this.getter = getter ?? (_ => 0f);
        this.setter = setter ?? ((_, __) => { });
    }

    public string ParameterId { get; }
    public string DisplayName { get; }
    public float MinimumValue { get; }
    public float MaximumValue { get; }
    public Func<float, string> Formatter { get; }

    public float GetValue(object settingsObject)
    {
        return Mathf.Clamp(getter(settingsObject), MinimumValue, MaximumValue);
    }

    public void SetValue(object settingsObject, float value)
    {
        setter(settingsObject, Mathf.Clamp(value, MinimumValue, MaximumValue));
    }
}

public abstract class ToneLabPedalDescriptorBase<TSettings, TProcessor> : IToneLabPedalDescriptor
    where TSettings : class, new()
    where TProcessor : IToneLabPedalProcessor, new()
{
    public virtual string DescriptorId => PedalType.ToString();
    public abstract UnityToneLabRuntime.ToneLabPedalType PedalType { get; }
    public abstract string DisplayName { get; }
    public abstract string ShortName { get; }
    public abstract string Description { get; }
    public virtual ToneLabPedalAppearance Appearance => CreateAppearance();
    public abstract IReadOnlyList<ToneLabPedalParameterDefinition> Parameters { get; }

    public object CreateDefaultSettingsObject()
    {
        return CreateDefaultSettings();
    }

    public object DeserializeSettingsObject(string settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
            return CreateDefaultSettings();

        try
        {
            TSettings settings = JsonUtility.FromJson<TSettings>(settingsJson);
            return settings ?? CreateDefaultSettings();
        }
        catch
        {
            return CreateDefaultSettings();
        }
    }

    public string SerializeSettingsObject(object settingsObject)
    {
        TSettings typedSettings = settingsObject as TSettings ?? CreateDefaultSettings();
        return JsonUtility.ToJson(typedSettings);
    }

    public IToneLabPedalProcessor CreateProcessor()
    {
        return new TProcessor();
    }

    protected virtual TSettings CreateDefaultSettings()
    {
        return new TSettings();
    }

    protected virtual ToneLabPedalAppearance CreateAppearance()
    {
        return ToneLabPedalAppearance.CreateDefault();
    }
}

public static class ToneLabPedalUtility
{
    public static float DbToLinear(float decibels)
    {
        return Mathf.Pow(10f, decibels / 20f);
    }

    public static float LinearToDb(float linear)
    {
        return 20f * Mathf.Log10(Mathf.Max(0.000001f, linear));
    }

    public static float SoftClipAtan(float sample, float drive)
    {
        return (2f / Mathf.PI) * Mathf.Atan(sample * drive);
    }

    public static float ClampFrequency(float frequency, float sampleRate)
    {
        return Mathf.Clamp(frequency, 10f, Mathf.Max(20f, sampleRate * 0.475f));
    }

    public static float ClampQ(float q)
    {
        return Mathf.Clamp(q, 0.1f, 18f);
    }

    public static float NormalizeKernel(float[] kernel)
    {
        if (kernel == null || kernel.Length == 0)
            return 1f;

        float sum = 0f;
        for (int i = 0; i < kernel.Length; i++)
            sum += Mathf.Abs(kernel[i]);

        float normalization = sum > 0.0001f ? 1f / sum : 1f;
        for (int i = 0; i < kernel.Length; i++)
            kernel[i] *= normalization;
        return normalization;
    }
}

public sealed class ToneLabBiquadFilter
{
    private float b0;
    private float b1;
    private float b2;
    private float a1;
    private float a2;
    private float z1;
    private float z2;

    public void Reset()
    {
        z1 = 0f;
        z2 = 0f;
    }

    public float Process(float input)
    {
        float output = (input * b0) + z1;
        z1 = (input * b1) + z2 - (a1 * output);
        z2 = (input * b2) - (a2 * output);
        return output;
    }

    public void SetLowPass(float sampleRate, float frequency, float q)
    {
        ConfigureLowPass(sampleRate, frequency, q, out b0, out b1, out b2, out a1, out a2);
    }

    public void SetHighPass(float sampleRate, float frequency, float q)
    {
        ConfigureHighPass(sampleRate, frequency, q, out b0, out b1, out b2, out a1, out a2);
    }

    public void SetPeaking(float sampleRate, float frequency, float q, float gainDb)
    {
        ConfigurePeaking(sampleRate, frequency, q, gainDb, out b0, out b1, out b2, out a1, out a2);
    }

    public void SetLowShelf(float sampleRate, float frequency, float q, float gainDb)
    {
        ConfigureLowShelf(sampleRate, frequency, q, gainDb, out b0, out b1, out b2, out a1, out a2);
    }

    public void SetHighShelf(float sampleRate, float frequency, float q, float gainDb)
    {
        ConfigureHighShelf(sampleRate, frequency, q, gainDb, out b0, out b1, out b2, out a1, out a2);
    }

    private static void ConfigureLowPass(float sampleRate, float frequency, float q, out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float omega = 2f * Mathf.PI * ToneLabPedalUtility.ClampFrequency(frequency, sampleRate) / Mathf.Max(1f, sampleRate);
        float sin = Mathf.Sin(omega);
        float cos = Mathf.Cos(omega);
        float alpha = sin / (2f * ToneLabPedalUtility.ClampQ(q));
        float rawB0 = (1f - cos) * 0.5f;
        float rawB1 = 1f - cos;
        float rawB2 = (1f - cos) * 0.5f;
        float rawA0 = 1f + alpha;
        float rawA1 = -2f * cos;
        float rawA2 = 1f - alpha;
        Normalize(rawB0, rawB1, rawB2, rawA0, rawA1, rawA2, out b0, out b1, out b2, out a1, out a2);
    }

    private static void ConfigureHighPass(float sampleRate, float frequency, float q, out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float omega = 2f * Mathf.PI * ToneLabPedalUtility.ClampFrequency(frequency, sampleRate) / Mathf.Max(1f, sampleRate);
        float sin = Mathf.Sin(omega);
        float cos = Mathf.Cos(omega);
        float alpha = sin / (2f * ToneLabPedalUtility.ClampQ(q));
        float rawB0 = (1f + cos) * 0.5f;
        float rawB1 = -(1f + cos);
        float rawB2 = (1f + cos) * 0.5f;
        float rawA0 = 1f + alpha;
        float rawA1 = -2f * cos;
        float rawA2 = 1f - alpha;
        Normalize(rawB0, rawB1, rawB2, rawA0, rawA1, rawA2, out b0, out b1, out b2, out a1, out a2);
    }

    private static void ConfigurePeaking(float sampleRate, float frequency, float q, float gainDb, out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float omega = 2f * Mathf.PI * ToneLabPedalUtility.ClampFrequency(frequency, sampleRate) / Mathf.Max(1f, sampleRate);
        float sin = Mathf.Sin(omega);
        float cos = Mathf.Cos(omega);
        float alpha = sin / (2f * ToneLabPedalUtility.ClampQ(q));
        float gain = Mathf.Pow(10f, gainDb / 40f);
        float rawB0 = 1f + (alpha * gain);
        float rawB1 = -2f * cos;
        float rawB2 = 1f - (alpha * gain);
        float rawA0 = 1f + (alpha / gain);
        float rawA1 = -2f * cos;
        float rawA2 = 1f - (alpha / gain);
        Normalize(rawB0, rawB1, rawB2, rawA0, rawA1, rawA2, out b0, out b1, out b2, out a1, out a2);
    }

    private static void ConfigureLowShelf(float sampleRate, float frequency, float q, float gainDb, out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float omega = 2f * Mathf.PI * ToneLabPedalUtility.ClampFrequency(frequency, sampleRate) / Mathf.Max(1f, sampleRate);
        float sin = Mathf.Sin(omega);
        float cos = Mathf.Cos(omega);
        float gain = Mathf.Pow(10f, gainDb / 40f);
        float rootGain = Mathf.Sqrt(gain);
        float alpha = sin / (2f * ToneLabPedalUtility.ClampQ(q));
        float twoRootGainAlpha = 2f * rootGain * alpha;

        float rawB0 = gain * ((gain + 1f) - ((gain - 1f) * cos) + twoRootGainAlpha);
        float rawB1 = 2f * gain * ((gain - 1f) - ((gain + 1f) * cos));
        float rawB2 = gain * ((gain + 1f) - ((gain - 1f) * cos) - twoRootGainAlpha);
        float rawA0 = (gain + 1f) + ((gain - 1f) * cos) + twoRootGainAlpha;
        float rawA1 = -2f * ((gain - 1f) + ((gain + 1f) * cos));
        float rawA2 = (gain + 1f) + ((gain - 1f) * cos) - twoRootGainAlpha;
        Normalize(rawB0, rawB1, rawB2, rawA0, rawA1, rawA2, out b0, out b1, out b2, out a1, out a2);
    }

    private static void ConfigureHighShelf(float sampleRate, float frequency, float q, float gainDb, out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float omega = 2f * Mathf.PI * ToneLabPedalUtility.ClampFrequency(frequency, sampleRate) / Mathf.Max(1f, sampleRate);
        float sin = Mathf.Sin(omega);
        float cos = Mathf.Cos(omega);
        float gain = Mathf.Pow(10f, gainDb / 40f);
        float rootGain = Mathf.Sqrt(gain);
        float alpha = sin / (2f * ToneLabPedalUtility.ClampQ(q));
        float twoRootGainAlpha = 2f * rootGain * alpha;

        float rawB0 = gain * ((gain + 1f) + ((gain - 1f) * cos) + twoRootGainAlpha);
        float rawB1 = -2f * gain * ((gain - 1f) + ((gain + 1f) * cos));
        float rawB2 = gain * ((gain + 1f) + ((gain - 1f) * cos) - twoRootGainAlpha);
        float rawA0 = (gain + 1f) - ((gain - 1f) * cos) + twoRootGainAlpha;
        float rawA1 = 2f * ((gain - 1f) - ((gain + 1f) * cos));
        float rawA2 = (gain + 1f) - ((gain - 1f) * cos) - twoRootGainAlpha;
        Normalize(rawB0, rawB1, rawB2, rawA0, rawA1, rawA2, out b0, out b1, out b2, out a1, out a2);
    }

    private static void Normalize(float rawB0, float rawB1, float rawB2, float rawA0, float rawA1, float rawA2, out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float invA0 = 1f / Mathf.Max(0.000001f, rawA0);
        b0 = rawB0 * invA0;
        b1 = rawB1 * invA0;
        b2 = rawB2 * invA0;
        a1 = rawA1 * invA0;
        a2 = rawA2 * invA0;
    }
}

public sealed class ToneLabFirConvolver
{
    private float[] kernel = Array.Empty<float>();
    private float[] buffer = Array.Empty<float>();
    private int writeIndex;

    public void SetKernel(float[] impulseResponse)
    {
        kernel = impulseResponse != null && impulseResponse.Length > 0
            ? (float[])impulseResponse.Clone()
            : Array.Empty<float>();

        buffer = kernel.Length > 0 ? new float[kernel.Length] : Array.Empty<float>();
        writeIndex = 0;
    }

    public void Reset()
    {
        if (buffer.Length > 0)
            Array.Clear(buffer, 0, buffer.Length);
        writeIndex = 0;
    }

    public float Process(float input)
    {
        if (kernel.Length == 0)
            return input;

        buffer[writeIndex] = input;
        float output = 0f;
        int readIndex = writeIndex;
        for (int i = 0; i < kernel.Length; i++)
        {
            output += kernel[i] * buffer[readIndex];
            readIndex--;
            if (readIndex < 0)
                readIndex = buffer.Length - 1;
        }

        writeIndex++;
        if (writeIndex >= buffer.Length)
            writeIndex = 0;
        return output;
    }
}
