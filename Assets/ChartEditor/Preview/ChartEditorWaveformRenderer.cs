using System;
using System.Collections;
using UnityEngine;

public sealed class ChartEditorWaveformData
{
    public float durationSeconds;
    public int sampleRate;
    public int channels;
    public float[] positivePeaks;
    public float[] negativePeaks;
    public float[] rms;

    public int BinCount => positivePeaks?.Length ?? 0;
    public bool IsValid => BinCount > 0 && negativePeaks != null && negativePeaks.Length == BinCount;
}

public static class ChartEditorWaveformRenderer
{
    private const int MinimumBins = 8192;
    private const int MaximumBins = 262144;
    private const int MinimumTextureWidth = 64;
    private const int MaximumTextureWidth = 32768;
    private const int MinimumTextureHeight = 160;
    private const int MaximumTextureHeight = 512;
    private const int MinimumOverviewTextureWidth = 4096;
    private const float OverviewPixelsPerSecond = 128f;

    public static ChartEditorWaveformData BuildData(AudioClip clip)
    {
        if (clip == null || clip.samples <= 0 || clip.channels <= 0 || clip.frequency <= 0)
            return null;

        int totalFrames = Mathf.Max(1, clip.samples);
        int channels = Mathf.Max(1, clip.channels);
        int binCount = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(1f, clip.length) * 720f), MinimumBins, MaximumBins);
        float[] positive = new float[binCount];
        float[] negative = new float[binCount];
        double[] squareSums = new double[binCount];
        int[] counts = new int[binCount];
        int chunkFrames = Mathf.Max(1024, Mathf.Min(totalFrames, clip.frequency * 4));

        for (int offsetFrame = 0; offsetFrame < totalFrames; offsetFrame += chunkFrames)
        {
            int frames = Mathf.Min(chunkFrames, totalFrames - offsetFrame);
            float[] samples = new float[frames * channels];
            if (!clip.GetData(samples, offsetFrame))
                return null;

            for (int frame = 0; frame < frames; frame++)
            {
                float selected = 0f;
                int baseIndex = frame * channels;
                for (int channel = 0; channel < channels; channel++)
                {
                    float sample = samples[baseIndex + channel];
                    if (Mathf.Abs(sample) > Mathf.Abs(selected))
                        selected = sample;
                }

                int bin = Mathf.Clamp((int)(((long)(offsetFrame + frame) * binCount) / totalFrames), 0, binCount - 1);
                if (selected > positive[bin])
                    positive[bin] = selected;
                if (selected < negative[bin])
                    negative[bin] = selected;

                squareSums[bin] += selected * selected;
                counts[bin]++;
            }
        }

        float[] rms = new float[binCount];
        for (int i = 0; i < binCount; i++)
        {
            if (counts[i] > 0)
                rms[i] = Mathf.Sqrt((float)(squareSums[i] / counts[i]));
        }

        Normalize(positive, negative, rms);
        SmoothRms(rms);
        return new ChartEditorWaveformData
        {
            durationSeconds = Mathf.Max(0.1f, clip.length),
            sampleRate = clip.frequency,
            channels = channels,
            positivePeaks = positive,
            negativePeaks = negative,
            rms = rms
        };
    }

    public static IEnumerator BuildDataAsync(AudioClip clip, Action<ChartEditorWaveformData> onComplete)
    {
        if (clip == null || clip.samples <= 0 || clip.channels <= 0 || clip.frequency <= 0)
        {
            onComplete?.Invoke(null);
            yield break;
        }

        int totalFrames = Mathf.Max(1, clip.samples);
        int channels = Mathf.Max(1, clip.channels);
        int binCount = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(1f, clip.length) * 720f), MinimumBins, MaximumBins);
        float[] positive = new float[binCount];
        float[] negative = new float[binCount];
        double[] squareSums = new double[binCount];
        int[] counts = new int[binCount];
        int chunkFrames = Mathf.Max(2048, Mathf.Min(totalFrames, clip.frequency));

        for (int offsetFrame = 0; offsetFrame < totalFrames; offsetFrame += chunkFrames)
        {
            int frames = Mathf.Min(chunkFrames, totalFrames - offsetFrame);
            float[] samples = new float[frames * channels];
            if (!clip.GetData(samples, offsetFrame))
            {
                onComplete?.Invoke(null);
                yield break;
            }

            AccumulateSamples(samples, frames, channels, offsetFrame, totalFrames, binCount, positive, negative, squareSums, counts);
            yield return null;
        }

        float[] rms = new float[binCount];
        for (int i = 0; i < binCount; i++)
        {
            if (counts[i] > 0)
                rms[i] = Mathf.Sqrt((float)(squareSums[i] / counts[i]));
        }

        yield return null;
        Normalize(positive, negative, rms);
        yield return null;
        SmoothRms(rms);
        onComplete?.Invoke(new ChartEditorWaveformData
        {
            durationSeconds = Mathf.Max(0.1f, clip.length),
            sampleRate = clip.frequency,
            channels = channels,
            positivePeaks = positive,
            negativePeaks = negative,
            rms = rms
        });
    }

    public static int ResolveTextureWidth(float requestedWidth)
    {
        return Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(1f, requestedWidth)), MinimumTextureWidth, ResolveMaximumTextureWidth());
    }

    public static int ResolveMaximumTextureWidth()
    {
        return Mathf.Clamp(SystemInfo.maxTextureSize > 0 ? SystemInfo.maxTextureSize : MaximumTextureWidth, MinimumTextureWidth, MaximumTextureWidth);
    }

    public static int ResolveTextureHeight(float waveformHeight)
    {
        return Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(1f, waveformHeight)), MinimumTextureHeight, MaximumTextureHeight);
    }

    public static int ResolveOverviewTextureWidth(ChartEditorWaveformData data)
    {
        float duration = Mathf.Max(1f, data?.durationSeconds ?? 1f);
        return Mathf.Clamp(Mathf.CeilToInt(duration * OverviewPixelsPerSecond), MinimumOverviewTextureWidth, MaximumTextureWidth);
    }

    public static Texture2D RenderTexture(ChartEditorWaveformData data, double startSeconds, double endSeconds, float requestedWidth, float waveformHeight)
    {
        if (data == null || !data.IsValid)
            return null;

        int width = ResolveTextureWidth(requestedWidth);
        int height = ResolveTextureHeight(waveformHeight);
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "ChartEditorModernWaveform",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[width * height];
        FillBackground(pixels, width, height);
        DrawWaveform(pixels, width, height, data, startSeconds, endSeconds);
        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        return texture;
    }

    public static IEnumerator RenderTextureAsync(
        ChartEditorWaveformData data,
        double startSeconds,
        double endSeconds,
        float requestedWidth,
        float waveformHeight,
        Action<Texture2D> onComplete,
        Func<bool> shouldCancel = null)
    {
        if (data == null || !data.IsValid || ShouldCancel(shouldCancel))
        {
            onComplete?.Invoke(null);
            yield break;
        }

        int width = ResolveTextureWidth(requestedWidth);
        int height = ResolveTextureHeight(waveformHeight);
        Color32[] pixels = new Color32[width * height];

        const int backgroundRowsPerFrame = 24;
        for (int y = 0; y < height; y += backgroundRowsPerFrame)
        {
            if (ShouldCancel(shouldCancel))
            {
                onComplete?.Invoke(null);
                yield break;
            }

            FillBackgroundRows(pixels, width, height, y, Mathf.Min(height, y + backgroundRowsPerFrame));
            yield return null;
        }

        if (ShouldCancel(shouldCancel))
        {
            onComplete?.Invoke(null);
            yield break;
        }

        DrawBackgroundCenterLine(pixels, width, height);
        yield return null;

        float center = (height - 1) * 0.5f;
        float maxHalf = height * 0.43f;
        startSeconds = Math.Max(0.0, Math.Min(data.durationSeconds, startSeconds));
        endSeconds = Math.Max(startSeconds + 0.001, Math.Min(data.durationSeconds, endSeconds));
        double secondsPerPixel = (endSeconds - startSeconds) / Math.Max(1, width);
        float previousBody = 0f;
        float previousPeak = 0f;
        const int columnsPerFrame = 320;

        for (int xStart = 0; xStart < width; xStart += columnsPerFrame)
        {
            if (ShouldCancel(shouldCancel))
            {
                onComplete?.Invoke(null);
                yield break;
            }

            int xEnd = Math.Min(width, xStart + columnsPerFrame);
            for (int x = xStart; x < xEnd; x++)
            {
                double sampleStart = startSeconds + x * secondsPerPixel;
                double sampleEnd = sampleStart + secondsPerPixel;
                SampleRange(data, sampleStart, sampleEnd, out float positive, out float negative, out float rms);
                float peak = Mathf.Max(Mathf.Abs(positive), Mathf.Abs(negative));
                float bodyHeight = Mathf.Lerp(0.75f, maxHalf * 0.72f, Mathf.Pow(Mathf.Clamp01(rms), 0.72f));
                float peakHeight = Mathf.Lerp(bodyHeight + 0.75f, maxHalf, Mathf.Pow(Mathf.Clamp01(peak), 0.70f));

                bodyHeight = previousBody * 0.20f + bodyHeight * 0.80f;
                peakHeight = previousPeak * 0.18f + peakHeight * 0.82f;
                previousBody = bodyHeight;
                previousPeak = Mathf.Max(bodyHeight, peakHeight);

                DrawEnvelopeColumn(pixels, width, height, x, center, bodyHeight, Mathf.Max(bodyHeight, peakHeight));
            }

            yield return null;
        }

        if (ShouldCancel(shouldCancel))
        {
            onComplete?.Invoke(null);
            yield break;
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "ChartEditorModernWaveform",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        onComplete?.Invoke(texture);
    }

    private static bool ShouldCancel(Func<bool> shouldCancel)
    {
        return shouldCancel != null && shouldCancel();
    }

    private static void Normalize(float[] positive, float[] negative, float[] rms)
    {
        int count = positive?.Length ?? 0;
        if (count == 0 || negative == null || rms == null)
            return;

        float[] peakMagnitudes = new float[count];
        float[] rmsMagnitudes = new float[count];
        for (int i = 0; i < count; i++)
        {
            peakMagnitudes[i] = Mathf.Max(Mathf.Abs(positive[i]), Mathf.Abs(negative[i]));
            rmsMagnitudes[i] = Mathf.Abs(rms[i]);
        }

        float peakScale = Mathf.Max(0.0001f, Percentile(peakMagnitudes, 0.985f));
        float rmsScale = Mathf.Max(0.0001f, Percentile(rmsMagnitudes, 0.965f));
        for (int i = 0; i < count; i++)
        {
            positive[i] = Mathf.Clamp(positive[i] / peakScale, 0f, 1f);
            negative[i] = Mathf.Clamp(negative[i] / peakScale, -1f, 0f);
            rms[i] = Mathf.Clamp01(rms[i] / rmsScale);
        }
    }

    private static void AccumulateSamples(
        float[] samples,
        int frames,
        int channels,
        int offsetFrame,
        int totalFrames,
        int binCount,
        float[] positive,
        float[] negative,
        double[] squareSums,
        int[] counts)
    {
        for (int frame = 0; frame < frames; frame++)
        {
            float selected = 0f;
            int baseIndex = frame * channels;
            for (int channel = 0; channel < channels; channel++)
            {
                float sample = samples[baseIndex + channel];
                if (Mathf.Abs(sample) > Mathf.Abs(selected))
                    selected = sample;
            }

            int bin = Mathf.Clamp((int)(((long)(offsetFrame + frame) * binCount) / totalFrames), 0, binCount - 1);
            if (selected > positive[bin])
                positive[bin] = selected;
            if (selected < negative[bin])
                negative[bin] = selected;

            squareSums[bin] += selected * selected;
            counts[bin]++;
        }
    }

    private static float Percentile(float[] values, float percentile)
    {
        if (values == null || values.Length == 0)
            return 0f;

        float[] copy = new float[values.Length];
        Array.Copy(values, copy, values.Length);
        Array.Sort(copy);
        int index = Mathf.Clamp(Mathf.RoundToInt((copy.Length - 1) * Mathf.Clamp01(percentile)), 0, copy.Length - 1);
        return copy[index];
    }

    private static void SmoothRms(float[] rms)
    {
        if (rms == null || rms.Length < 3)
            return;

        float previous = rms[0];
        for (int i = 1; i < rms.Length - 1; i++)
        {
            float current = rms[i];
            rms[i] = previous * 0.22f + current * 0.56f + rms[i + 1] * 0.22f;
            previous = current;
        }
    }

    private static void FillBackground(Color32[] pixels, int width, int height)
    {
        FillBackgroundRows(pixels, width, height, 0, height);
        DrawBackgroundCenterLine(pixels, width, height);
    }

    private static void FillBackgroundRows(Color32[] pixels, int width, int height, int yStart, int yEnd)
    {
        Color top = new Color(0.014f, 0.018f, 0.026f, 1f);
        Color middle = new Color(0.010f, 0.013f, 0.020f, 1f);
        Color bottom = new Color(0.007f, 0.010f, 0.016f, 1f);
        for (int y = Mathf.Max(0, yStart); y < Mathf.Min(height, yEnd); y++)
        {
            float t = y / (float)Mathf.Max(1, height - 1);
            Color color = t < 0.5f
                ? Color.Lerp(top, middle, t * 2f)
                : Color.Lerp(middle, bottom, (t - 0.5f) * 2f);
            Color32 packed = ToColor32(color);
            int row = y * width;
            for (int x = 0; x < width; x++)
                pixels[row + x] = packed;
        }
    }

    private static void DrawBackgroundCenterLine(Color32[] pixels, int width, int height)
    {
        Color center = new Color(0.18f, 0.24f, 0.34f, 0.22f);
        int centerY = Mathf.RoundToInt((height - 1) * 0.5f);
        for (int x = 0; x < width; x++)
        {
            BlendPixel(pixels, width, height, x, centerY, center);
            if (centerY + 1 < height)
                BlendPixel(pixels, width, height, x, centerY + 1, new Color(center.r, center.g, center.b, 0.10f));
        }
    }

    private static void DrawWaveform(Color32[] pixels, int width, int height, ChartEditorWaveformData data, double startSeconds, double endSeconds)
    {
        float center = (height - 1) * 0.5f;
        float maxHalf = height * 0.43f;
        startSeconds = Math.Max(0.0, Math.Min(data.durationSeconds, startSeconds));
        endSeconds = Math.Max(startSeconds + 0.001, Math.Min(data.durationSeconds, endSeconds));
        double secondsPerPixel = (endSeconds - startSeconds) / Math.Max(1, width);
        float previousBody = 0f;
        float previousPeak = 0f;

        for (int x = 0; x < width; x++)
        {
            double sampleStart = startSeconds + x * secondsPerPixel;
            double sampleEnd = sampleStart + secondsPerPixel;
            SampleRange(data, sampleStart, sampleEnd, out float positive, out float negative, out float rms);
            float peak = Mathf.Max(Mathf.Abs(positive), Mathf.Abs(negative));
            float bodyHeight = Mathf.Lerp(0.75f, maxHalf * 0.72f, Mathf.Pow(Mathf.Clamp01(rms), 0.72f));
            float peakHeight = Mathf.Lerp(bodyHeight + 0.75f, maxHalf, Mathf.Pow(Mathf.Clamp01(peak), 0.70f));

            bodyHeight = previousBody * 0.20f + bodyHeight * 0.80f;
            peakHeight = previousPeak * 0.18f + peakHeight * 0.82f;
            previousBody = bodyHeight;
            previousPeak = Mathf.Max(bodyHeight, peakHeight);

            DrawEnvelopeColumn(pixels, width, height, x, center, bodyHeight, Mathf.Max(bodyHeight, peakHeight));
        }
    }

    public static void SampleRange(ChartEditorWaveformData data, double startSeconds, double endSeconds, out float positive, out float negative, out float rms)
    {
        double duration = Math.Max(0.001, data.durationSeconds);
        int first = Mathf.Clamp(Mathf.FloorToInt((float)(startSeconds / duration * data.BinCount)), 0, data.BinCount - 1);
        int last = Mathf.Clamp(Mathf.CeilToInt((float)(endSeconds / duration * data.BinCount)), first, data.BinCount - 1);
        positive = 0f;
        negative = 0f;
        float rmsSum = 0f;
        int count = 0;

        for (int i = first; i <= last; i++)
        {
            positive = Mathf.Max(positive, data.positivePeaks[i]);
            negative = Mathf.Min(negative, data.negativePeaks[i]);
            if (data.rms != null && i < data.rms.Length)
                rmsSum += data.rms[i];
            count++;
        }

        rms = count > 0 ? rmsSum / count : Mathf.Max(positive, Mathf.Abs(negative));
    }

    private static void DrawEnvelopeColumn(Color32[] pixels, int width, int height, int x, float center, float bodyHeight, float peakHeight)
    {
        int yMin = Mathf.Clamp(Mathf.FloorToInt(center - peakHeight - 1f), 0, height - 1);
        int yMax = Mathf.Clamp(Mathf.CeilToInt(center + peakHeight + 1f), 0, height - 1);
        Color body = new Color(0.42f, 0.24f, 0.70f, 0.82f);
        Color core = new Color(0.66f, 0.40f, 0.91f, 0.88f);
        Color peakColor = new Color(0.20f, 0.38f, 0.58f, 0.18f);

        for (int y = yMin; y <= yMax; y++)
        {
            float distance = Mathf.Abs(y - center);
            if (distance > bodyHeight)
            {
                float peakAa = distance <= peakHeight ? Mathf.Clamp01((peakHeight - distance) / Mathf.Max(1f, peakHeight - bodyHeight)) : 0f;
                if (peakAa > 0f)
                    BlendPixel(pixels, width, height, x, y, new Color(peakColor.r, peakColor.g, peakColor.b, peakColor.a * peakAa));
                continue;
            }

            float vertical = 1f - Mathf.Clamp01(distance / Mathf.Max(1f, bodyHeight));
            Color color = body;
            float coreBlend = 1f - Mathf.Clamp01(distance / Mathf.Max(1f, bodyHeight * 0.55f));
            if (coreBlend > 0f)
                color = Color.Lerp(color, core, coreBlend * 0.38f);

            float edge = Mathf.Clamp01(bodyHeight - distance);
            if (edge < 1f)
                color.a *= Mathf.Lerp(0.55f, 1f, edge);
            BlendPixel(pixels, width, height, x, y, color);
        }
    }

    private static void BlendPixel(Color32[] pixels, int width, int height, int x, int y, Color source)
    {
        if (x < 0 || x >= width || y < 0 || y >= height || source.a <= 0f)
            return;

        int index = y * width + x;
        Color destination = ToColor(pixels[index]);
        float a = Mathf.Clamp01(source.a);
        Color blended = new Color(
            destination.r * (1f - a) + source.r * a,
            destination.g * (1f - a) + source.g * a,
            destination.b * (1f - a) + source.b * a,
            1f);
        pixels[index] = ToColor32(blended);
    }

    private static Color ToColor(Color32 color)
    {
        const float scale = 1f / 255f;
        return new Color(color.r * scale, color.g * scale, color.b * scale, color.a * scale);
    }

    private static Color32 ToColor32(Color color)
    {
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255));
    }
}
