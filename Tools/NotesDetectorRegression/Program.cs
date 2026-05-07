using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class Program
{
    private static readonly int[] StandardOpenMidis = { 40, 45, 50, 55, 59, 64 };

    private static int Main()
    {
        string repoRoot = FindRepoRoot();
        string toolRoot = Path.Combine(repoRoot, "Tools", "NotesDetectorRegression");
        string fixturePath = Path.Combine(toolRoot, "fixture_library.json");
        string nativeDllPath = ResolveNativeDllPath(repoRoot);
        Console.WriteLine($"Native DLL: {nativeDllPath}");

        if (!File.Exists(fixturePath))
            throw new FileNotFoundException("Missing fixture library.", fixturePath);
        if (!File.Exists(nativeDllPath))
            throw new FileNotFoundException("Missing native detector DLL.", nativeDllPath);

        FixtureLibrary library = JsonSerializer.Deserialize<FixtureLibrary>(
            File.ReadAllText(fixturePath),
            JsonOptions()) ?? throw new InvalidOperationException("Failed to parse fixture library.");

        string sessionId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string sessionRoot = Path.Combine(toolRoot, "Generated", sessionId);
        string audioRoot = Path.Combine(sessionRoot, "audio");
        string resultRoot = Path.Combine(sessionRoot, "results");
        Directory.CreateDirectory(audioRoot);
        Directory.CreateDirectory(resultRoot);

        using NativeRegressionBridge native = new(nativeDllPath);

        List<FixtureSummary> summaries = new();
        foreach (FixtureCase testCase in library.Cases)
        {
            float[] audio = RenderFixture(testCase, library.SampleRate);
            string wavPath = Path.Combine(audioRoot, $"{testCase.Id}.wav");
            WriteMonoPcm16Wave(wavPath, library.SampleRate, audio);

            string expectedCsv = BuildExpectedCsv(testCase.ExpectedNotes);
            string settingsJson = BuildSettingsJson(testCase);
            string detectorJson = native.Evaluate(audio, expectedCsv, settingsJson);
            string resultPath = Path.Combine(resultRoot, $"{testCase.Id}.json");
            File.WriteAllText(resultPath, detectorJson);

            using JsonDocument doc = JsonDocument.Parse(detectorJson);
            JsonElement root = doc.RootElement;
            bool detectorAccepted = root.TryGetProperty("accepted", out JsonElement acceptedElement) && acceptedElement.GetBoolean();
            string mode = root.TryGetProperty("mode", out JsonElement modeElement) ? modeElement.GetString() ?? "unknown" : "unknown";
            int hitCount = root.TryGetProperty("hitCount", out JsonElement hitCountElement) ? hitCountElement.GetInt32() : -1;
            int requiredHits = root.TryGetProperty("requiredHits", out JsonElement requiredHitsElement) ? requiredHitsElement.GetInt32() : -1;
            int detectedMidi = root.TryGetProperty("detectedMidi", out JsonElement detectedMidiElement) ? detectedMidiElement.GetInt32() : -1;
            string fastAcceptedSource = root.TryGetProperty("fastAcceptedSource", out JsonElement fastAcceptedSourceElement) ? fastAcceptedSourceElement.GetString() ?? string.Empty : string.Empty;
            bool continuousAccepted = root.TryGetProperty("continuousAccepted", out JsonElement continuousAcceptedElement) && continuousAcceptedElement.ValueKind == JsonValueKind.True;

            summaries.Add(new FixtureSummary
            {
                Id = testCase.Id,
                Label = testCase.Label,
                Mode = mode,
                ExpectedAccept = testCase.ExpectedAccept,
                DetectorAccepted = detectorAccepted,
                HitCount = hitCount,
                RequiredHits = requiredHits,
                DetectedMidi = detectedMidi,
                FastAcceptedSource = fastAcceptedSource,
                ContinuousAccepted = continuousAccepted,
                AudioPath = MakeRelative(repoRoot, wavPath),
                ResultPath = MakeRelative(repoRoot, resultPath)
            });
        }

        string summaryPath = Path.Combine(sessionRoot, "summary.json");
        File.WriteAllText(summaryPath, JsonSerializer.Serialize(new { sessionId, summaries }, JsonOptionsIndented()));

        bool allPassed = true;
        Console.WriteLine($"Session: {sessionId}");
        foreach (FixtureSummary summary in summaries)
        {
            bool casePassed = summary.ExpectedAccept == summary.DetectorAccepted;
            allPassed &= casePassed;
            Console.WriteLine(
                $"{(casePassed ? "PASS" : "FAIL")}  {summary.Id,-30} mode={summary.Mode,-6} expected={(summary.ExpectedAccept ? 1 : 0)} actual={(summary.DetectorAccepted ? 1 : 0)} hit={summary.HitCount}/{summary.RequiredHits} midi={summary.DetectedMidi} fast={summary.FastAcceptedSource} cont={(summary.ContinuousAccepted ? 1 : 0)}");
        }

        Console.WriteLine($"Summary: {MakeRelative(repoRoot, summaryPath)}");
        return allPassed ? 0 : 1;
    }

    private static string BuildSettingsJson(FixtureCase testCase)
    {
        if (testCase.ExpectedNotes.Count >= 2)
        {
            return JsonSerializer.Serialize(new { chordLeniency = testCase.ChordLeniency ?? 1.0f }, JsonOptions());
        }

        return "{}";
    }

    private static string BuildExpectedCsv(List<FixtureNote> notes)
    {
        return string.Join(",",
            notes.Select(note =>
            {
                int openMidi = StandardOpenMidis[note.StringIndex];
                int midi = openMidi + note.Fret;
                return $"{midi}~{note.StringIndex}~{note.Fret}~{openMidi}~0";
            }));
    }

    private static float[] RenderFixture(FixtureCase testCase, int sampleRate)
    {
        float totalSeconds = MathF.Max(0.25f, testCase.LeadInSeconds + testCase.DurationSeconds + testCase.TailSeconds);
        int totalSamples = (int)Math.Ceiling(totalSeconds * sampleRate);
        float[] samples = new float[totalSamples];

        List<FixtureNote> playedNotes = BuildPlayedNotes(testCase);
        for (int i = 0; i < playedNotes.Count; ++i)
        {
            FixtureNote note = playedNotes[i];
            int midi = StandardOpenMidis[note.StringIndex] + note.Fret;
            float startSeconds = testCase.LeadInSeconds + note.StartOffsetSeconds;
            AddPluckedTone(samples, sampleRate, midi, startSeconds, testCase.DurationSeconds, note.Amplitude, i + (midi * 17));
        }

        Normalize(samples, 0.82f);
        return samples;
    }

    private static List<FixtureNote> BuildPlayedNotes(FixtureCase testCase)
    {
        List<FixtureNote> result = testCase.PlayedNotes.Count > 0
            ? testCase.PlayedNotes.Select(note => note.Clone()).ToList()
            : testCase.ExpectedNotes.Select(note => note.Clone()).ToList();

        if (testCase.StrumSpacingSeconds <= 0f)
        {
            foreach (FixtureNote note in result)
            {
                if (note.StartOffsetSeconds < 0f)
                    note.StartOffsetSeconds = 0f;
            }

            return result;
        }

        result.Sort((left, right) => left.StringIndex.CompareTo(right.StringIndex));
        for (int i = 0; i < result.Count; ++i)
        {
            if (result[i].StartOffsetSeconds < 0f)
                result[i].StartOffsetSeconds = i * testCase.StrumSpacingSeconds;
        }

        return result;
    }

    private static void AddPluckedTone(float[] destination, int sampleRate, int midi, float startSeconds, float durationSeconds, float amplitude, int seed)
    {
        float frequency = MidiToFrequencyHz(midi);
        if (!float.IsFinite(frequency) || frequency <= 0f)
            return;

        int startSample = (int)Math.Round(startSeconds * sampleRate);
        int sampleCount = Math.Min(destination.Length - startSample, (int)Math.Ceiling((durationSeconds + 0.20f) * sampleRate));
        if (startSample < 0 || sampleCount <= 0)
            return;

        DeterministicNoise noise = new(seed);
        for (int i = 0; i < sampleCount; ++i)
        {
            float time = i / (float)sampleRate;
            float attack = MathF.Min(1f, time / 0.0045f);
            float envelope = attack * MathF.Exp(-time * 2.25f);
            float fundamental = MathF.Sin(2f * MathF.PI * frequency * time);
            float second = 0.52f * MathF.Sin(2f * MathF.PI * frequency * 2f * time + 0.12f);
            float third = 0.28f * MathF.Sin(2f * MathF.PI * frequency * 3f * time + 0.27f);
            float fourth = 0.16f * MathF.Sin(2f * MathF.PI * frequency * 4f * time + 0.41f);
            float pickNoise = time < 0.022f ? 0.045f * (1f - (time / 0.022f)) * noise.NextSignedUnit() : 0f;
            destination[startSample + i] += amplitude * envelope * (fundamental + second + third + fourth + pickNoise);
        }
    }

    private static void Normalize(float[] samples, float targetPeak)
    {
        float peak = 0f;
        for (int i = 0; i < samples.Length; ++i)
            peak = MathF.Max(peak, MathF.Abs(samples[i]));

        if (peak <= 1e-6f)
            return;

        float scale = targetPeak / peak;
        for (int i = 0; i < samples.Length; ++i)
            samples[i] *= scale;
    }

    private static float MidiToFrequencyHz(int midi)
    {
        return 440f * MathF.Pow(2f, (midi - 69) / 12f);
    }

    private static void WriteMonoPcm16Wave(string path, int sampleRate, float[] samples)
    {
        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: false);

        int byteRate = sampleRate * 2;
        int dataLength = samples.Length * 2;
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);

        foreach (float sample in samples)
        {
            float clamped = Math.Clamp(sample, -1f, 1f);
            short pcm = (short)Math.Round(clamped * short.MaxValue);
            writer.Write(pcm);
        }
    }

    private static string FindRepoRoot()
    {
        string[] rootsToTry =
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (string root in rootsToTry)
        {
            DirectoryInfo? current = new(root);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "NativeNotesDetectorBridge")) &&
                    File.Exists(Path.Combine(current.FullName, ".gitignore")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    private static string ResolveNativeDllPath(string repoRoot)
    {
        string? explicitPath = Environment.GetEnvironmentVariable("NOTES_DETECTOR_NATIVE_DLL");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        string[] candidates =
        {
            Path.Combine(repoRoot, "NativeNotesDetectorBridge", "build", "AltRelease", "NativeNotesDetectorBridgeNative_v6_regression.dll"),
            Path.Combine(repoRoot, "Assets", "Plugins", "x86_64", "NativeNotesDetectorBridgeNative_v6.dll")
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        return candidates[^1];
    }

    private static string MakeRelative(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }

    private static JsonSerializerOptions JsonOptionsIndented()
    {
        JsonSerializerOptions options = JsonOptions();
        options.WriteIndented = true;
        return options;
    }

    private sealed class NativeRegressionBridge : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int EvaluateDelegate(
            [In] float[] samples,
            int sampleCount,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string expectedNoteSpecsUtf8,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string settingsJsonUtf8,
            StringBuilder destination,
            int capacity);

        private readonly nint libraryHandle;
        private readonly EvaluateDelegate evaluate;

        public NativeRegressionBridge(string nativeDllPath)
        {
            libraryHandle = NativeLibrary.Load(nativeDllPath);
            nint export = NativeLibrary.GetExport(libraryHandle, "NativeDetector_DebugEvaluatePcmFloat");
            evaluate = Marshal.GetDelegateForFunctionPointer<EvaluateDelegate>(export);
        }

        public string Evaluate(float[] samples, string expectedCsv, string settingsJson)
        {
            StringBuilder buffer = new(64 * 1024);
            if (evaluate(samples, samples.Length, expectedCsv, settingsJson, buffer, buffer.Capacity) == 0)
                throw new InvalidOperationException("Native detector fixture evaluation failed.");
            return buffer.ToString();
        }

        public void Dispose()
        {
            NativeLibrary.Free(libraryHandle);
        }
    }

    private sealed class DeterministicNoise
    {
        private uint state;

        public DeterministicNoise(int seed)
        {
            state = unchecked((uint)(seed == 0 ? 1 : seed));
        }

        public float NextSignedUnit()
        {
            state = unchecked((state * 1664525u) + 1013904223u);
            uint bits = (state >> 8) & 0x00FFFFFFu;
            float normalized = bits / (float)0x00FFFFFFu;
            return (normalized * 2f) - 1f;
        }
    }

    private sealed class FixtureLibrary
    {
        [JsonPropertyName("sampleRate")]
        public int SampleRate { get; set; } = 22050;

        [JsonPropertyName("cases")]
        public List<FixtureCase> Cases { get; set; } = new();
    }

    private sealed class FixtureCase
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("expectedAccept")]
        public bool ExpectedAccept { get; set; }

        [JsonPropertyName("leadInSeconds")]
        public float LeadInSeconds { get; set; } = 0.10f;

        [JsonPropertyName("durationSeconds")]
        public float DurationSeconds { get; set; } = 1.20f;

        [JsonPropertyName("tailSeconds")]
        public float TailSeconds { get; set; } = 0.35f;

        [JsonPropertyName("strumSpacingSeconds")]
        public float StrumSpacingSeconds { get; set; }

        [JsonPropertyName("chordLeniency")]
        public float? ChordLeniency { get; set; }

        [JsonPropertyName("expectedNotes")]
        public List<FixtureNote> ExpectedNotes { get; set; } = new();

        [JsonPropertyName("playedNotes")]
        public List<FixtureNote> PlayedNotes { get; set; } = new();
    }

    private sealed class FixtureNote
    {
        [JsonPropertyName("stringIndex")]
        public int StringIndex { get; set; }

        [JsonPropertyName("fret")]
        public int Fret { get; set; }

        [JsonPropertyName("startOffsetSeconds")]
        public float StartOffsetSeconds { get; set; } = -1f;

        [JsonPropertyName("amplitude")]
        public float Amplitude { get; set; } = 1f;

        public FixtureNote Clone()
        {
            return new FixtureNote
            {
                StringIndex = StringIndex,
                Fret = Fret,
                StartOffsetSeconds = StartOffsetSeconds,
                Amplitude = Amplitude
            };
        }
    }

    private sealed class FixtureSummary
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Mode { get; set; } = string.Empty;
        public bool ExpectedAccept { get; set; }
        public bool DetectorAccepted { get; set; }
        public int HitCount { get; set; }
        public int RequiredHits { get; set; }
        public int DetectedMidi { get; set; }
        public string FastAcceptedSource { get; set; } = string.Empty;
        public bool ContinuousAccepted { get; set; }
        public string AudioPath { get; set; } = string.Empty;
        public string ResultPath { get; set; } = string.Empty;
    }
}
