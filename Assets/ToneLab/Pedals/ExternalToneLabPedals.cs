using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;

public enum ToneLabExternalPedalKind
{
    Lv2,
    Nam
}

[Serializable]
public sealed class ToneLabExternalPedalSettings
{
    public string descriptor_id = string.Empty;
    public string processor_kind = string.Empty;
    public string plugin_uri = string.Empty;
    public string display_name = string.Empty;
    public string bundle_path = string.Empty;
    public string model_path = string.Empty;
    public List<ToneLabExternalParameterValue> parameters = new List<ToneLabExternalParameterValue>();
}

[Serializable]
public sealed class ToneLabExternalParameterValue
{
    public string parameter_id = string.Empty;
    public float value;
}

public sealed class ToneLabExternalParameterSpec
{
    public string ParameterId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int PortIndex { get; set; } = -1;
    public float DefaultValue { get; set; }
    public float MinimumValue { get; set; }
    public float MaximumValue { get; set; } = 1f;
    public bool Visible { get; set; } = true;
}

public sealed class ToneLabExternalPedalDescriptor : IToneLabPedalDescriptor
{
    private readonly ToneLabExternalPedalKind kind;
    private readonly string sourcePath;
    private readonly string lv2BinaryPath;
    private readonly int[] audioInputPorts;
    private readonly int[] audioOutputPorts;
    private readonly IReadOnlyList<ToneLabExternalParameterSpec> parameterSpecs;
    private readonly IReadOnlyList<ToneLabPedalParameterDefinition> parameters;
    private readonly ToneLabPedalAppearance appearance;

    public ToneLabExternalPedalDescriptor(
        ToneLabExternalPedalKind kind,
        string descriptorId,
        string displayName,
        string shortName,
        string description,
        string sourcePath,
        IReadOnlyList<ToneLabExternalParameterSpec> parameterSpecs,
        string lv2BinaryPath = "",
        int[] audioInputPorts = null,
        int[] audioOutputPorts = null)
    {
        this.kind = kind;
        DescriptorId = descriptorId ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? (kind == ToneLabExternalPedalKind.Nam ? "NAM Amp" : "LV2 Effect") : displayName.Trim();
        ShortName = string.IsNullOrWhiteSpace(shortName) ? (kind == ToneLabExternalPedalKind.Nam ? "NAM" : "LV2") : shortName.Trim();
        Description = string.IsNullOrWhiteSpace(description)
            ? (kind == ToneLabExternalPedalKind.Nam ? "External Neural Amp Modeler capture." : "External LV2 effect loaded from the Tone Lab plugin folder.")
            : description.Trim();
        this.sourcePath = sourcePath ?? string.Empty;
        this.lv2BinaryPath = lv2BinaryPath ?? string.Empty;
        this.audioInputPorts = audioInputPorts ?? Array.Empty<int>();
        this.audioOutputPorts = audioOutputPorts ?? Array.Empty<int>();
        this.parameterSpecs = parameterSpecs ?? Array.Empty<ToneLabExternalParameterSpec>();
        parameters = CreateParameterDefinitions(this.parameterSpecs);
        appearance = kind == ToneLabExternalPedalKind.Nam ? CreateNamAppearance() : CreateLv2Appearance(DisplayName);
    }

    public string DescriptorId { get; }
    public UnityToneLabRuntime.ToneLabPedalType PedalType => kind == ToneLabExternalPedalKind.Nam
        ? UnityToneLabRuntime.ToneLabPedalType.NamModel
        : UnityToneLabRuntime.ToneLabPedalType.Lv2Plugin;
    public string DisplayName { get; }
    public string ShortName { get; }
    public string Description { get; }
    public ToneLabPedalAppearance Appearance => appearance;
    public IReadOnlyList<ToneLabPedalParameterDefinition> Parameters => parameters;
    public ToneLabExternalPedalKind Kind => kind;
    public string SourcePath => sourcePath;
    public string Lv2BinaryPath => lv2BinaryPath;
    public IReadOnlyList<int> AudioInputPorts => audioInputPorts;
    public IReadOnlyList<int> AudioOutputPorts => audioOutputPorts;
    public IReadOnlyList<ToneLabExternalParameterSpec> ParameterSpecs => parameterSpecs;

    public object CreateDefaultSettingsObject()
    {
        ToneLabExternalPedalSettings settings = new ToneLabExternalPedalSettings
        {
            descriptor_id = DescriptorId,
            processor_kind = kind == ToneLabExternalPedalKind.Nam ? "nam" : "lv2",
            display_name = DisplayName
        };

        if (kind == ToneLabExternalPedalKind.Nam)
            settings.model_path = sourcePath;
        else
        {
            settings.plugin_uri = DescriptorId.StartsWith("lv2:", StringComparison.Ordinal) ? DescriptorId.Substring(4) : DescriptorId;
            settings.bundle_path = sourcePath;
        }

        for (int i = 0; i < parameterSpecs.Count; i++)
        {
            ToneLabExternalParameterSpec spec = parameterSpecs[i];
            settings.parameters.Add(new ToneLabExternalParameterValue
            {
                parameter_id = spec.ParameterId,
                value = Mathf.Clamp(spec.DefaultValue, spec.MinimumValue, spec.MaximumValue)
            });
        }

        return settings;
    }

    public object DeserializeSettingsObject(string settingsJson)
    {
        ToneLabExternalPedalSettings settings = null;
        if (!string.IsNullOrWhiteSpace(settingsJson))
        {
            try
            {
                settings = JsonUtility.FromJson<ToneLabExternalPedalSettings>(settingsJson);
            }
            catch
            {
                settings = null;
            }
        }

        settings ??= (ToneLabExternalPedalSettings)CreateDefaultSettingsObject();
        settings.descriptor_id = string.IsNullOrWhiteSpace(settings.descriptor_id) ? DescriptorId : settings.descriptor_id;
        settings.processor_kind = string.IsNullOrWhiteSpace(settings.processor_kind)
            ? (kind == ToneLabExternalPedalKind.Nam ? "nam" : "lv2")
            : settings.processor_kind;
        settings.display_name = string.IsNullOrWhiteSpace(settings.display_name) ? DisplayName : settings.display_name;
        if (kind == ToneLabExternalPedalKind.Nam)
            settings.model_path = string.IsNullOrWhiteSpace(settings.model_path) ? sourcePath : settings.model_path;
        else
        {
            settings.plugin_uri = string.IsNullOrWhiteSpace(settings.plugin_uri)
                ? (DescriptorId.StartsWith("lv2:", StringComparison.Ordinal) ? DescriptorId.Substring(4) : DescriptorId)
                : settings.plugin_uri;
            settings.bundle_path = string.IsNullOrWhiteSpace(settings.bundle_path) ? sourcePath : settings.bundle_path;
        }

        EnsureParameterValues(settings);
        return settings;
    }

    public string SerializeSettingsObject(object settingsObject)
    {
        ToneLabExternalPedalSettings settings = settingsObject as ToneLabExternalPedalSettings
            ?? (ToneLabExternalPedalSettings)CreateDefaultSettingsObject();
        settings.descriptor_id = DescriptorId;
        settings.processor_kind = kind == ToneLabExternalPedalKind.Nam ? "nam" : "lv2";
        settings.display_name = DisplayName;
        if (kind == ToneLabExternalPedalKind.Nam)
        {
            settings.model_path = sourcePath;
            settings.plugin_uri = string.Empty;
            settings.bundle_path = string.Empty;
        }
        else
        {
            settings.plugin_uri = DescriptorId.StartsWith("lv2:", StringComparison.Ordinal) ? DescriptorId.Substring(4) : DescriptorId;
            settings.bundle_path = sourcePath;
            settings.model_path = string.Empty;
        }

        EnsureParameterValues(settings);
        return JsonUtility.ToJson(settings);
    }

    public IToneLabPedalProcessor CreateProcessor()
    {
        return new ToneLabExternalPedalProcessor(this);
    }

    private void EnsureParameterValues(ToneLabExternalPedalSettings settings)
    {
        settings.parameters ??= new List<ToneLabExternalParameterValue>();
        for (int i = 0; i < parameterSpecs.Count; i++)
        {
            ToneLabExternalParameterSpec spec = parameterSpecs[i];
            ToneLabExternalParameterValue value = FindParameterValue(settings, spec.ParameterId);
            if (value == null)
            {
                settings.parameters.Add(new ToneLabExternalParameterValue
                {
                    parameter_id = spec.ParameterId,
                    value = Mathf.Clamp(spec.DefaultValue, spec.MinimumValue, spec.MaximumValue)
                });
            }
            else
            {
                value.value = Mathf.Clamp(value.value, spec.MinimumValue, spec.MaximumValue);
            }
        }
    }

    private static IReadOnlyList<ToneLabPedalParameterDefinition> CreateParameterDefinitions(IReadOnlyList<ToneLabExternalParameterSpec> specs)
    {
        List<ToneLabPedalParameterDefinition> definitions = new List<ToneLabPedalParameterDefinition>(specs.Count);
        for (int i = 0; i < specs.Count; i++)
        {
            ToneLabExternalParameterSpec spec = specs[i];
            if (spec == null || !spec.Visible)
                continue;

            string parameterId = spec.ParameterId;
            definitions.Add(new ToneLabPedalParameterDefinition(
                parameterId,
                spec.DisplayName,
                spec.MinimumValue,
                spec.MaximumValue,
                value => FormatParameterValue(value, spec),
                settingsObject => GetParameterValue(settingsObject as ToneLabExternalPedalSettings, parameterId, spec.DefaultValue),
                (settingsObject, value) => SetParameterValue(settingsObject as ToneLabExternalPedalSettings, parameterId, value)));
        }

        return definitions;
    }

    private static string FormatParameterValue(float value, ToneLabExternalParameterSpec spec)
    {
        if (Mathf.Approximately(spec.MinimumValue, 0f) && Mathf.Approximately(spec.MaximumValue, 1f))
            return $"{value * 100f:F0}%";

        if (Mathf.Abs(spec.MaximumValue - spec.MinimumValue) <= 12f)
            return value.ToString("F2", CultureInfo.InvariantCulture);

        return value.ToString("F1", CultureInfo.InvariantCulture);
    }

    private static float GetParameterValue(ToneLabExternalPedalSettings settings, string parameterId, float fallback)
    {
        ToneLabExternalParameterValue value = FindParameterValue(settings, parameterId);
        return value != null ? value.value : fallback;
    }

    private static void SetParameterValue(ToneLabExternalPedalSettings settings, string parameterId, float value)
    {
        if (settings == null || string.IsNullOrWhiteSpace(parameterId))
            return;

        settings.parameters ??= new List<ToneLabExternalParameterValue>();
        ToneLabExternalParameterValue parameterValue = FindParameterValue(settings, parameterId);
        if (parameterValue == null)
        {
            parameterValue = new ToneLabExternalParameterValue { parameter_id = parameterId };
            settings.parameters.Add(parameterValue);
        }

        parameterValue.value = value;
    }

    private static ToneLabExternalParameterValue FindParameterValue(ToneLabExternalPedalSettings settings, string parameterId)
    {
        if (settings?.parameters == null || string.IsNullOrWhiteSpace(parameterId))
            return null;

        for (int i = 0; i < settings.parameters.Count; i++)
        {
            ToneLabExternalParameterValue value = settings.parameters[i];
            if (value != null && string.Equals(value.parameter_id, parameterId, StringComparison.Ordinal))
                return value;
        }

        return null;
    }

    private static ToneLabPedalAppearance CreateLv2Appearance(string displayName)
    {
        Color accent = PickExternalAccent(displayName);
        return new ToneLabPedalAppearance
        {
            BodyColor = new Color(0.08f, 0.10f, 0.14f, 1f),
            FaceColor = new Color(0.15f, 0.18f, 0.24f, 1f),
            LabelStripColor = new Color(0.02f, 0.03f, 0.05f, 0.98f),
            TextColor = new Color(0.96f, 0.98f, 1f, 1f),
            SecondaryTextColor = new Color(accent.r, accent.g, accent.b, 0.96f),
            EdgeColor = new Color(0.03f, 0.05f, 0.08f, 1f),
            TopEdgeColor = new Color(accent.r, accent.g, accent.b, 1f),
            AccentColor = accent,
            ShadowColor = new Color(accent.r, accent.g, accent.b, 0.18f),
            KnobColor = new Color(0.88f, 0.90f, 0.94f, 1f),
            KnobIndicatorColor = accent,
            LedOnColor = accent,
            FootswitchColor = new Color(0.10f, 0.12f, 0.16f, 1f),
            KnobCount = 0,
            SliderCount = 4,
            DecorationStyle = ToneLabPedalDecorationStyle.SparkBars,
            FooterEnabledText = "LV2",
            FooterBypassedText = "BYPASS"
        };
    }

    private static ToneLabPedalAppearance CreateNamAppearance()
    {
        return new ToneLabPedalAppearance
        {
            BodyColor = new Color(0.07f, 0.06f, 0.05f, 1f),
            FaceColor = new Color(0.18f, 0.15f, 0.11f, 1f),
            LabelStripColor = new Color(0.02f, 0.02f, 0.02f, 0.98f),
            TextColor = new Color(1.00f, 0.95f, 0.82f, 1f),
            SecondaryTextColor = new Color(0.95f, 0.69f, 0.26f, 0.98f),
            EdgeColor = new Color(0.02f, 0.02f, 0.02f, 1f),
            TopEdgeColor = new Color(0.98f, 0.66f, 0.20f, 1f),
            AccentColor = new Color(0.98f, 0.66f, 0.20f, 1f),
            ShadowColor = new Color(0.98f, 0.45f, 0.12f, 0.16f),
            KnobColor = new Color(0.94f, 0.79f, 0.50f, 1f),
            KnobIndicatorColor = new Color(0.08f, 0.06f, 0.04f, 1f),
            LedOnColor = new Color(1.00f, 0.72f, 0.24f, 1f),
            FootswitchColor = new Color(0.73f, 0.45f, 0.16f, 1f),
            KnobCount = 3,
            SliderCount = 1,
            DecorationStyle = ToneLabPedalDecorationStyle.CenterStripe,
            FooterEnabledText = "NAM",
            FooterBypassedText = "BYPASS"
        };
    }

    private static Color PickExternalAccent(string name)
    {
        uint hash = 2166136261u;
        string value = name ?? string.Empty;
        for (int i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= 16777619u;
        }

        switch (hash % 5u)
        {
            case 0: return new Color(0.16f, 0.76f, 0.98f, 1f);
            case 1: return new Color(0.98f, 0.34f, 0.62f, 1f);
            case 2: return new Color(0.32f, 0.95f, 0.58f, 1f);
            case 3: return new Color(0.99f, 0.72f, 0.20f, 1f);
            default: return new Color(0.69f, 0.50f, 1.00f, 1f);
        }
    }
}

public sealed class ToneLabExternalPedalProcessor : IToneLabPedalProcessor
{
    private readonly ToneLabExternalPedalDescriptor descriptor;
    private ToneLabExternalPedalSettings settings = new ToneLabExternalPedalSettings();
    private IntPtr nativeHandle = IntPtr.Zero;
    private ToneLabManagedLv2Instance managedLv2Instance;
    private int preparedSampleRate;
    private int preparedChannels;

    public ToneLabExternalPedalProcessor(ToneLabExternalPedalDescriptor descriptor)
    {
        this.descriptor = descriptor;
    }

    ~ToneLabExternalPedalProcessor()
    {
        ReleaseNativeHandle();
    }

    public void Prepare(int sampleRate, int channels)
    {
        preparedSampleRate = Mathf.Max(1, sampleRate);
        preparedChannels = Mathf.Max(1, channels);
        RecreateNativeHandle();
    }

    public void Reset()
    {
        managedLv2Instance?.Reset();
        if (nativeHandle != IntPtr.Zero)
            ToneLabNativeHost.Reset(nativeHandle);
    }

    public void ApplySettings(object settingsObject)
    {
        settings = settingsObject as ToneLabExternalPedalSettings ?? (ToneLabExternalPedalSettings)descriptor.CreateDefaultSettingsObject();
        ApplyParametersToNative();
    }

    public void Process(float[] data, int channels, int sampleRate)
    {
        if (data == null || channels <= 0)
            return;

        if (managedLv2Instance != null)
        {
            managedLv2Instance.Process(data, channels);
            return;
        }

        if (nativeHandle == IntPtr.Zero)
            return;

        int frames = data.Length / channels;
        if (frames <= 0)
            return;

        if (!ToneLabNativeHost.ProcessInterleaved(nativeHandle, data, frames, channels))
            ReleaseNativeHandle();
    }

    private void RecreateNativeHandle()
    {
        ReleaseNativeHandle();
        if (preparedSampleRate <= 0 || preparedChannels <= 0 || settings == null)
            return;

        if (ToneLabNativeHost.IsAvailable)
        {
            nativeHandle = descriptor.Kind == ToneLabExternalPedalKind.Nam
                ? ToneLabNativeHost.CreateNam(settings.model_path, preparedSampleRate, preparedChannels, 2048)
                : ToneLabNativeHost.CreateLv2(settings.plugin_uri, ExternalContentPaths.PersistentToneLabLv2Directory, preparedSampleRate, preparedChannels, 2048);
            if (nativeHandle != IntPtr.Zero)
            {
                ApplyParametersToNative();
                return;
            }
        }

        if (descriptor.Kind == ToneLabExternalPedalKind.Lv2)
        {
            managedLv2Instance = ToneLabManagedLv2Instance.TryCreate(descriptor, settings, preparedSampleRate, preparedChannels);
            if (managedLv2Instance != null)
                ApplyParametersToNative();
        }
    }

    private void ApplyParametersToNative()
    {
        if (managedLv2Instance != null && settings?.parameters != null)
        {
            for (int i = 0; i < settings.parameters.Count; i++)
            {
                ToneLabExternalParameterValue parameter = settings.parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.parameter_id))
                    continue;

                managedLv2Instance.SetParameter(parameter.parameter_id, parameter.value);
            }
        }

        if (nativeHandle == IntPtr.Zero || settings?.parameters == null)
            return;

        for (int i = 0; i < settings.parameters.Count; i++)
        {
            ToneLabExternalParameterValue parameter = settings.parameters[i];
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.parameter_id))
                continue;

            ToneLabNativeHost.SetParameter(nativeHandle, parameter.parameter_id, parameter.value);
        }
    }

    private void ReleaseNativeHandle()
    {
        if (managedLv2Instance != null)
        {
            managedLv2Instance.Dispose();
            managedLv2Instance = null;
        }

        if (nativeHandle == IntPtr.Zero)
            return;

        ToneLabNativeHost.Destroy(nativeHandle);
        nativeHandle = IntPtr.Zero;
    }
}

public static class ToneLabExternalPedalCatalog
{
    private const string Lv2DescriptorPrefix = "lv2:";
    private const string NamDescriptorPrefix = "nam:";
    private const string NamMetadataFileName = "metadata.json";
    private static readonly Regex PluginSubjectRegex = new Regex(@"<(?<uri>[^>]+)>\s+a\s+(?<types>[^.]*?lv2:Plugin[^.]*?)\s*;", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex DescriptionRegex = new Regex(@"(?:rdfs:comment|doap:description|dc:description|dcterms:description)\s+(?:""""""(?<long>.*?)""""""|""(?<short>[^""]*)"")", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex ControlPortRegex = new Regex(@"\[\s*(?=[^\]]*lv2:ControlPort)(?<body>.*?lv2:maximum\s+[-+0-9.eE]+[^]]*)\]", RegexOptions.Compiled | RegexOptions.Singleline);

    [Serializable]
    private sealed class ToneLabNamMetadataCatalog
    {
        public List<ToneLabNamMetadataEntry> profiles = new List<ToneLabNamMetadataEntry>();
    }

    [Serializable]
    private sealed class ToneLabNamMetadataEntry
    {
        public string path = string.Empty;
        public string display_name = string.Empty;
        public string short_name = string.Empty;
        public string description = string.Empty;
        public string creator = string.Empty;
        public string license = string.Empty;
        public string source_url = string.Empty;
    }

    public static IToneLabPedalDescriptor[] ScanDescriptors()
    {
        List<IToneLabPedalDescriptor> descriptors = new List<IToneLabPedalDescriptor>();
        try
        {
            ExternalContentPaths.EnsureUnityRootsCaptured();
            ScanLv2Descriptors(descriptors);
            ScanNamDescriptors(descriptors);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ToneLabExternalPedalCatalog] External pedal scan failed: {ex.Message}");
        }

        return descriptors
            .OrderBy(descriptor => descriptor.PedalType == UnityToneLabRuntime.ToneLabPedalType.NamModel ? 1 : 0)
            .ThenBy(descriptor => descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string BuildContentSignature()
    {
        try
        {
            ExternalContentPaths.EnsureUnityRootsCaptured();
            List<string> entries = new List<string>();
            AppendSignatureEntries(entries, ExternalContentPaths.PersistentToneLabLv2Directory);
            AppendSignatureEntries(entries, ExternalContentPaths.PersistentToneLabNamDirectory);
            entries.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join("|", entries);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ToneLabExternalPedalCatalog] Failed to build external pedal signature: {ex.Message}");
            return DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
        }
    }

    public static IToneLabPedalDescriptor CreateMissingDescriptor(UnityToneLabRuntime.ToneLabPedalSlot slot)
    {
        string descriptorId = slot?.descriptor_id ?? string.Empty;
        ToneLabExternalPedalKind kind = descriptorId.StartsWith(NamDescriptorPrefix, StringComparison.Ordinal)
            ? ToneLabExternalPedalKind.Nam
            : ToneLabExternalPedalKind.Lv2;
        string name = kind == ToneLabExternalPedalKind.Nam ? "Missing NAM" : "Missing LV2";
        return new ToneLabExternalPedalDescriptor(
            kind,
            descriptorId,
            name,
            kind == ToneLabExternalPedalKind.Nam ? "NAM" : "LV2",
            "This external pedal is saved in the preset but its file is not currently installed.",
            string.Empty,
            Array.Empty<ToneLabExternalParameterSpec>());
    }

    public static string BuildLv2DescriptorId(string pluginUri)
    {
        return string.IsNullOrWhiteSpace(pluginUri) ? string.Empty : Lv2DescriptorPrefix + pluginUri.Trim();
    }

    public static string BuildNamDescriptorId(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            return string.Empty;

        string relativePath = GetRelativePath(ExternalContentPaths.PersistentToneLabNamDirectory, modelPath);
        string descriptorPath = string.IsNullOrWhiteSpace(relativePath) ? modelPath : relativePath;
        return BuildNamDescriptorIdFromRelativePath(descriptorPath);
    }

    public static string BuildNamDescriptorIdFromRelativePath(string relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath) ? string.Empty : NamDescriptorPrefix + NormalizePath(relativePath);
    }

    public static string ResolveNamDescriptorId(string descriptorId)
    {
        if (string.IsNullOrWhiteSpace(descriptorId) || !descriptorId.StartsWith(NamDescriptorPrefix, StringComparison.Ordinal))
            return descriptorId ?? string.Empty;

        string descriptorPath = NormalizePath(descriptorId.Substring(NamDescriptorPrefix.Length));
        if (string.IsNullOrWhiteSpace(descriptorPath))
            return descriptorId;

        string relativePath = TryExtractNamRelativePath(descriptorPath);
        return string.IsNullOrWhiteSpace(relativePath) ? descriptorId : BuildNamDescriptorIdFromRelativePath(relativePath);
    }

    private static void ScanLv2Descriptors(List<IToneLabPedalDescriptor> descriptors)
    {
        string lv2Root = ExternalContentPaths.PersistentToneLabLv2Directory;
        if (!Directory.Exists(lv2Root))
            return;

        HashSet<string> descriptorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string bundlePath in Directory.GetDirectories(lv2Root, "*.lv2", SearchOption.AllDirectories))
        {
            string manifestPath = Path.Combine(bundlePath, "manifest.ttl");
            if (!File.Exists(manifestPath))
                continue;

            string ttl = ReadBundleTtl(bundlePath);
            foreach (Match match in PluginSubjectRegex.Matches(ttl))
            {
                string pluginUri = match.Groups["uri"].Value.Trim();
                if (string.IsNullOrWhiteSpace(pluginUri))
                    continue;

                string descriptorId = BuildLv2DescriptorId(pluginUri);
                if (descriptorIds.Contains(descriptorId))
                    continue;

                string pluginBlock = ExtractPluginSubjectBlocks(ttl, pluginUri);
                string displayName = FindName(pluginBlock);
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = PrettyNameFromBundle(bundlePath);

                IReadOnlyList<ToneLabExternalParameterSpec> parameterSpecs = ParseControlPorts(pluginBlock);
                int[] audioInputPorts = ParseAudioPorts(pluginBlock, inputPorts: true);
                int[] audioOutputPorts = ParseAudioPorts(pluginBlock, inputPorts: false);
                if (audioInputPorts.Length == 0 || audioOutputPorts.Length == 0)
                    continue;

                string binaryPath = ResolveLv2BinaryPath(bundlePath, ttl);
                if (string.IsNullOrWhiteSpace(binaryPath))
                    continue;
                if (!descriptorIds.Add(descriptorId))
                    continue;

                string shortName = BuildShortName(displayName, "LV2");
                descriptors.Add(new ToneLabExternalPedalDescriptor(
                    ToneLabExternalPedalKind.Lv2,
                    descriptorId,
                    displayName,
                    shortName,
                    BuildLv2Description(pluginBlock),
                    bundlePath,
                    parameterSpecs,
                    binaryPath,
                    audioInputPorts,
                    audioOutputPorts));
            }
        }
    }

    private static void AppendSignatureEntries(List<string> entries, string rootPath)
    {
        if (entries == null || string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            return;

        entries.Add($"root:{NormalizePath(rootPath)}");
        foreach (string filePath in Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            try
            {
                FileInfo fileInfo = new FileInfo(filePath);
                string relativePath = GetRelativePath(rootPath, filePath);
                entries.Add($"{NormalizePath(relativePath)}:{fileInfo.Length}:{fileInfo.LastWriteTimeUtc.Ticks}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ToneLabExternalPedalCatalog] Failed to inspect external pedal file '{filePath}': {ex.Message}");
            }
        }
    }

    private static void ScanNamDescriptors(List<IToneLabPedalDescriptor> descriptors)
    {
        string namRoot = ExternalContentPaths.PersistentToneLabNamDirectory;
        if (!Directory.Exists(namRoot))
            return;

        Dictionary<string, ToneLabNamMetadataEntry> metadata = LoadNamMetadata(namRoot);
        foreach (string modelPath in Directory.GetFiles(namRoot, "*.nam", SearchOption.AllDirectories))
        {
            string relativePath = GetRelativePath(namRoot, modelPath);
            ToneLabNamMetadataEntry metadataEntry = FindNamMetadata(metadata, relativePath, modelPath);
            string displayName = !string.IsNullOrWhiteSpace(metadataEntry?.display_name)
                ? metadataEntry.display_name
                : Path.GetFileNameWithoutExtension(modelPath);
            string shortName = !string.IsNullOrWhiteSpace(metadataEntry?.short_name) ? metadataEntry.short_name : "NAM";
            string description = !string.IsNullOrWhiteSpace(metadataEntry?.description)
                ? metadataEntry.description
                : "Neural Amp Modeler capture loaded from the Tone Lab NAM folder.";
            descriptors.Add(new ToneLabExternalPedalDescriptor(
                ToneLabExternalPedalKind.Nam,
                BuildNamDescriptorIdFromRelativePath(relativePath),
                displayName,
                shortName,
                description,
                modelPath,
                CreateNamParameterSpecs()));
        }
    }

    private static Dictionary<string, ToneLabNamMetadataEntry> LoadNamMetadata(string namRoot)
    {
        Dictionary<string, ToneLabNamMetadataEntry> result = new Dictionary<string, ToneLabNamMetadataEntry>(StringComparer.OrdinalIgnoreCase);
        string metadataPath = Path.Combine(namRoot, NamMetadataFileName);
        if (!File.Exists(metadataPath))
            return result;

        try
        {
            ToneLabNamMetadataCatalog catalog = JsonUtility.FromJson<ToneLabNamMetadataCatalog>(File.ReadAllText(metadataPath));
            if (catalog?.profiles == null)
                return result;

            for (int i = 0; i < catalog.profiles.Count; i++)
            {
                ToneLabNamMetadataEntry entry = catalog.profiles[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.path))
                    continue;

                string normalizedPath = NormalizePath(entry.path);
                result[normalizedPath] = entry;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ToneLabExternalPedalCatalog] Failed to read NAM metadata '{metadataPath}': {ex.Message}");
        }

        return result;
    }

    private static ToneLabNamMetadataEntry FindNamMetadata(Dictionary<string, ToneLabNamMetadataEntry> metadata, string relativePath, string modelPath)
    {
        if (metadata == null || metadata.Count == 0)
            return null;

        string normalizedRelativePath = NormalizePath(relativePath);
        if (!string.IsNullOrWhiteSpace(normalizedRelativePath) && metadata.TryGetValue(normalizedRelativePath, out ToneLabNamMetadataEntry entry))
            return entry;

        return null;
    }

    private static string GetRelativePath(string rootPath, string filePath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(filePath))
            return Path.GetFileName(filePath) ?? string.Empty;

        string fullRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullPath = Path.GetFullPath(filePath);
        string rootWithSeparator = fullRoot + Path.DirectorySeparatorChar;
        if (fullPath.StartsWith(rootWithSeparator, StringTheoryPlatform.PathComparison))
            return fullPath.Substring(rootWithSeparator.Length);

        return Path.GetFileName(filePath) ?? string.Empty;
    }

    private static string TryExtractNamRelativePath(string descriptorPath)
    {
        string normalizedDescriptorPath = NormalizePath(descriptorPath);
        if (string.IsNullOrWhiteSpace(normalizedDescriptorPath))
            return string.Empty;

        string namRoot = NormalizePath(ExternalContentPaths.PersistentToneLabNamDirectory);
        if (!string.IsNullOrWhiteSpace(namRoot))
        {
            string rootWithSeparator = namRoot.TrimEnd('/') + "/";
            if (normalizedDescriptorPath.StartsWith(rootWithSeparator, StringTheoryPlatform.PathComparison))
                return normalizedDescriptorPath.Substring(rootWithSeparator.Length);
        }

        string marker = "/" + ExternalContentPaths.ToneLabFolderName + "/" + ExternalContentPaths.ToneLabNamFolderName + "/";
        int markerIndex = normalizedDescriptorPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
            return normalizedDescriptorPath.Substring(markerIndex + marker.Length);

        return Path.IsPathRooted(normalizedDescriptorPath) ? string.Empty : normalizedDescriptorPath;
    }

    private static string ReadBundleTtl(string bundlePath)
    {
        string[] ttlFiles = Directory.GetFiles(bundlePath, "*.ttl", SearchOption.TopDirectoryOnly);
        List<string> chunks = new List<string>(ttlFiles.Length);
        for (int i = 0; i < ttlFiles.Length; i++)
        {
            try
            {
                chunks.Add(File.ReadAllText(ttlFiles[i]));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ToneLabExternalPedalCatalog] Failed to read LV2 metadata '{ttlFiles[i]}': {ex.Message}");
            }
        }

        return string.Join("\n", chunks);
    }

    private static string ExtractPluginSubjectBlocks(string ttl, string subjectUri)
    {
        if (string.IsNullOrWhiteSpace(ttl) || string.IsNullOrWhiteSpace(subjectUri))
            return string.Empty;

        List<string> blocks = new List<string>();
        foreach (Match match in PluginSubjectRegex.Matches(ttl))
        {
            string matchedUri = match.Groups["uri"].Value.Trim();
            if (!string.Equals(matchedUri, subjectUri, StringComparison.Ordinal))
                continue;

            int start = match.Index;
            int nextSubject = ttl.IndexOf("\n<", start + match.Length, StringComparison.Ordinal);
            if (nextSubject < 0)
                nextSubject = ttl.Length;

            blocks.Add(ttl.Substring(start, nextSubject - start));
        }

        return string.Join("\n", blocks);
    }

    private static IReadOnlyList<ToneLabExternalParameterSpec> ParseControlPorts(string pluginBlock)
    {
        List<ToneLabExternalParameterSpec> specs = new List<ToneLabExternalParameterSpec>();
        if (string.IsNullOrWhiteSpace(pluginBlock))
            return specs;

        foreach (Match match in ControlPortRegex.Matches(pluginBlock))
        {
            string body = match.Groups["body"].Value;
            if (body.IndexOf("lv2:InputPort", StringComparison.Ordinal) < 0)
                continue;

            string symbol = ReadQuotedValue(body, "lv2:symbol");
            if (string.IsNullOrWhiteSpace(symbol))
                continue;

            int portIndex = Mathf.RoundToInt(ReadFloatValue(body, "lv2:index", -1f));
            string name = ReadQuotedValue(body, "lv2:name");
            float min = ReadFloatValue(body, "lv2:minimum", 0f);
            float max = ReadFloatValue(body, "lv2:maximum", 1f);
            float def = ReadFloatValue(body, "lv2:default", min);
            if (max <= min)
                max = min + 1f;

            specs.Add(new ToneLabExternalParameterSpec
            {
                ParameterId = symbol,
                DisplayName = string.IsNullOrWhiteSpace(name) ? PrettyParameterName(symbol) : name,
                PortIndex = portIndex,
                MinimumValue = min,
                MaximumValue = max,
                DefaultValue = Mathf.Clamp(def, min, max),
                Visible = !string.Equals(symbol, "BYPASS", StringComparison.OrdinalIgnoreCase)
            });
        }

        return specs;
    }

    private static int[] ParseAudioPorts(string pluginBlock, bool inputPorts)
    {
        List<int> ports = new List<int>();
        if (string.IsNullOrWhiteSpace(pluginBlock))
            return ports.ToArray();

        foreach (Match match in Regex.Matches(pluginBlock, @"\[\s*(?<body>.*?lv2:name\s+""[^""]+""[^]]*)\]", RegexOptions.Singleline))
        {
            string body = match.Groups["body"].Value;
            if (body.IndexOf("lv2:AudioPort", StringComparison.Ordinal) < 0)
                continue;
            if (inputPorts && body.IndexOf("lv2:InputPort", StringComparison.Ordinal) < 0)
                continue;
            if (!inputPorts && body.IndexOf("lv2:OutputPort", StringComparison.Ordinal) < 0)
                continue;

            int portIndex = Mathf.RoundToInt(ReadFloatValue(body, "lv2:index", -1f));
            if (portIndex >= 0)
                ports.Add(portIndex);
        }

        return ports.ToArray();
    }

    private static string ResolveLv2BinaryPath(string bundlePath, string ttl)
    {
        Match match = Regex.Match(ttl ?? string.Empty, @"lv2:binary\s+<(?<path>[^>]+)>", RegexOptions.Singleline);
        if (match.Success)
        {
            string binaryName = match.Groups["path"].Value.Trim();
            string candidate = Path.Combine(bundlePath, binaryName);
            if (File.Exists(candidate))
                return candidate;
        }

        foreach (string extension in GetNativePluginBinaryPatterns())
        {
            string[] binaries = Directory.GetFiles(bundlePath, extension, SearchOption.TopDirectoryOnly);
            for (int i = 0; i < binaries.Length; i++)
            {
                string fileName = Path.GetFileName(binaries[i]);
                if (!IsLv2UiBinary(fileName))
                    return binaries[i];
            }
        }

        return string.Empty;
    }

    private static string[] GetNativePluginBinaryPatterns()
    {
        if (StringTheoryPlatform.IsMacOS)
            return new[] { "*.dylib", "*.so" };
        if (StringTheoryPlatform.IsLinux)
            return new[] { "*.so" };
        return new[] { "*.dll" };
    }

    private static bool IsLv2UiBinary(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        string name = fileName.Replace('\\', '/');
        return name.EndsWith("_ui.dll", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("_ui.dylib", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("_ui.so", StringComparison.OrdinalIgnoreCase) ||
               name.IndexOf("ui.", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static IReadOnlyList<ToneLabExternalParameterSpec> CreateNamParameterSpecs()
    {
        return new[]
        {
            new ToneLabExternalParameterSpec { ParameterId = "input_trim_db", DisplayName = "Input Trim", MinimumValue = -24f, MaximumValue = 24f, DefaultValue = 0f },
            new ToneLabExternalParameterSpec { ParameterId = "output_trim_db", DisplayName = "Output Trim", MinimumValue = -24f, MaximumValue = 24f, DefaultValue = 0f },
            new ToneLabExternalParameterSpec { ParameterId = "mix", DisplayName = "Mix", MinimumValue = 0f, MaximumValue = 1f, DefaultValue = 1f }
        };
    }

    private static string FindName(string block)
    {
        if (string.IsNullOrWhiteSpace(block))
            return string.Empty;

        string name = ReadQuotedValue(block, "doap:name");
        if (!string.IsNullOrWhiteSpace(name))
            return CleanDisplayName(name);

        name = ReadQuotedValue(block, "rdfs:label");
        if (!string.IsNullOrWhiteSpace(name))
            return CleanDisplayName(name);

        int portSectionIndex = block.IndexOf("lv2:port", StringComparison.Ordinal);
        string pluginHeader = portSectionIndex >= 0 ? block.Substring(0, portSectionIndex) : block;
        name = ReadQuotedValue(pluginHeader, "lv2:name");
        return string.IsNullOrWhiteSpace(name) ? string.Empty : CleanDisplayName(name);
    }

    private static string BuildShortName(string displayName, string fallback)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return fallback;

        string cleaned = displayName.Replace("Gx", string.Empty).Replace("_", string.Empty).Trim();
        if (cleaned.Length <= 5)
            return cleaned.ToUpperInvariant();

        return cleaned.Substring(0, Math.Min(5, cleaned.Length)).ToUpperInvariant();
    }

    private static string BuildLv2Description(string pluginBlock)
    {
        string metadataDescription = FindDescription(pluginBlock);
        if (!string.IsNullOrWhiteSpace(metadataDescription))
            return metadataDescription;

        if (pluginBlock != null && pluginBlock.IndexOf("DistortionPlugin", StringComparison.OrdinalIgnoreCase) >= 0)
            return "External LV2 drive pedal from the bundled guitar effect library.";
        if (pluginBlock != null && pluginBlock.IndexOf("AmplifierPlugin", StringComparison.OrdinalIgnoreCase) >= 0)
            return "External LV2 amp or preamp effect from the bundled guitar effect library.";
        if (pluginBlock != null && pluginBlock.IndexOf("EnvelopePlugin", StringComparison.OrdinalIgnoreCase) >= 0)
            return "External LV2 envelope effect from the bundled guitar effect library.";
        if (pluginBlock != null && pluginBlock.IndexOf("GatePlugin", StringComparison.OrdinalIgnoreCase) >= 0)
            return "External LV2 dynamics effect from the bundled guitar effect library.";

        return "External LV2 effect loaded from the Tone Lab plugin folder.";
    }

    private static string FindDescription(string block)
    {
        Match match = DescriptionRegex.Match(block ?? string.Empty);
        if (!match.Success)
            return string.Empty;

        string value = match.Groups["long"].Success ? match.Groups["long"].Value : match.Groups["short"].Value;
        value = CleanDescription(value);
        if (string.IsNullOrWhiteSpace(value) || value == "..." || value == ".")
            return string.Empty;

        return value;
    }

    private static string CleanDescription(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string cleaned = Regex.Replace(value.Trim(), @"\s+", " ");
        return cleaned.Trim(' ', '\t', '\r', '\n', '"');
    }

    private static string PrettyNameFromBundle(string bundlePath)
    {
        string name = Path.GetFileNameWithoutExtension(bundlePath) ?? "LV2 Effect";
        return CleanDisplayName(name);
    }

    private static string CleanDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        string cleaned = name.Trim().Trim('"').Replace("_", " ");
        if (cleaned.StartsWith("Gx ", StringComparison.OrdinalIgnoreCase))
            cleaned = "Gx" + cleaned.Substring(2);
        return cleaned.Trim();
    }

    private static string PrettyParameterName(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return "Parameter";

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(symbol.Replace("_", " ").ToLowerInvariant());
    }

    private static string ReadQuotedValue(string text, string predicate)
    {
        Match match = Regex.Match(text ?? string.Empty, Regex.Escape(predicate) + "\\s+\"(?<value>[^\"]+)\"", RegexOptions.Singleline);
        return match.Success ? match.Groups["value"].Value : string.Empty;
    }

    private static float ReadFloatValue(string text, string predicate, float fallback)
    {
        Match match = Regex.Match(text ?? string.Empty, Regex.Escape(predicate) + "\\s+(?<value>[-+0-9.eE]+)", RegexOptions.Singleline);
        if (!match.Success)
            return fallback;

        return float.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? value
            : fallback;
    }

    private static string NormalizePath(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/').Trim();
    }
}

public sealed class ToneLabManagedLv2Instance : IDisposable
{
    private const int DefaultBlockFrames = 2048;

    private readonly IntPtr libraryHandle;
    private readonly IntPtr instanceHandle;
    private readonly Lv2DescriptorNative descriptor;
    private readonly ConnectPortDelegate connectPort;
    private readonly RunDelegate run;
    private readonly ActivateDelegate activate;
    private readonly DeactivateDelegate deactivate;
    private readonly CleanupDelegate cleanup;
    private readonly Dictionary<string, int> controlPortIndices = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, float[]> controlValues = new Dictionary<string, float[]>(StringComparer.Ordinal);
    private readonly List<GCHandle> pinnedHandles = new List<GCHandle>();
    private readonly int[] inputPorts;
    private readonly int[] outputPorts;
    private float[][] inputBuffers = Array.Empty<float[]>();
    private float[][] outputBuffers = Array.Empty<float[]>();
    private int bufferFrames;
    private bool disposed;

    private ToneLabManagedLv2Instance(
        IntPtr libraryHandle,
        IntPtr instanceHandle,
        Lv2DescriptorNative descriptor,
        int[] inputPorts,
        int[] outputPorts,
        IReadOnlyList<ToneLabExternalParameterSpec> parameterSpecs)
    {
        this.libraryHandle = libraryHandle;
        this.instanceHandle = instanceHandle;
        this.descriptor = descriptor;
        this.inputPorts = inputPorts ?? Array.Empty<int>();
        this.outputPorts = outputPorts ?? Array.Empty<int>();
        connectPort = Marshal.GetDelegateForFunctionPointer<ConnectPortDelegate>(descriptor.connect_port);
        run = Marshal.GetDelegateForFunctionPointer<RunDelegate>(descriptor.run);
        activate = descriptor.activate != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer<ActivateDelegate>(descriptor.activate) : null;
        deactivate = descriptor.deactivate != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer<DeactivateDelegate>(descriptor.deactivate) : null;
        cleanup = descriptor.cleanup != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer<CleanupDelegate>(descriptor.cleanup) : null;

        CreateControlPorts(parameterSpecs);
        EnsureAudioBuffers(DefaultBlockFrames);
        activate?.Invoke(instanceHandle);
    }

    ~ToneLabManagedLv2Instance()
    {
        Dispose();
    }

    public static ToneLabManagedLv2Instance TryCreate(
        ToneLabExternalPedalDescriptor pedalDescriptor,
        ToneLabExternalPedalSettings settings,
        int sampleRate,
        int channels)
    {
        if (!StringTheoryPlatform.IsWindows)
            return null;

        if (pedalDescriptor == null || settings == null)
            return null;
        if (string.IsNullOrWhiteSpace(pedalDescriptor.Lv2BinaryPath) || !File.Exists(pedalDescriptor.Lv2BinaryPath))
            return null;
        if (pedalDescriptor.AudioInputPorts.Count == 0 || pedalDescriptor.AudioOutputPorts.Count == 0)
            return null;

        IntPtr library = IntPtr.Zero;
        bool transferLibraryOwnership = false;
        try
        {
            library = LoadLibraryExW(pedalDescriptor.Lv2BinaryPath, IntPtr.Zero, LoadWithAlteredSearchPath);
            if (library == IntPtr.Zero)
                library = LoadLibraryW(pedalDescriptor.Lv2BinaryPath);
            if (library == IntPtr.Zero)
                return null;

            IntPtr descriptorFunctionPointer = GetProcAddress(library, "lv2_descriptor");
            if (descriptorFunctionPointer == IntPtr.Zero)
                return null;

            Lv2DescriptorFunction descriptorFunction = Marshal.GetDelegateForFunctionPointer<Lv2DescriptorFunction>(descriptorFunctionPointer);
            IntPtr descriptorPointer = FindDescriptorPointer(descriptorFunction, settings.plugin_uri);
            if (descriptorPointer == IntPtr.Zero)
                return null;

            Lv2DescriptorNative descriptor = Marshal.PtrToStructure<Lv2DescriptorNative>(descriptorPointer);
            if (descriptor.instantiate == IntPtr.Zero || descriptor.connect_port == IntPtr.Zero || descriptor.run == IntPtr.Zero)
                return null;

            InstantiateDelegate instantiate = Marshal.GetDelegateForFunctionPointer<InstantiateDelegate>(descriptor.instantiate);
            string bundlePath = pedalDescriptor.SourcePath;
            if (!bundlePath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                bundlePath += Path.DirectorySeparatorChar;
            IntPtr instance = instantiate(descriptorPointer, sampleRate, bundlePath, IntPtr.Zero);
            if (instance == IntPtr.Zero)
                return null;

            ToneLabManagedLv2Instance managedInstance = new ToneLabManagedLv2Instance(
                library,
                instance,
                descriptor,
                pedalDescriptor.AudioInputPorts.ToArray(),
                pedalDescriptor.AudioOutputPorts.ToArray(),
                pedalDescriptor.ParameterSpecs);
            transferLibraryOwnership = true;
            return managedInstance;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ToneLabManagedLv2Instance] Failed to create LV2 instance '{pedalDescriptor.DisplayName}': {ex.Message}");
            return null;
        }
        finally
        {
            if (!transferLibraryOwnership && library != IntPtr.Zero)
                FreeLibrary(library);
        }
    }

    public void SetParameter(string parameterId, float value)
    {
        if (disposed || string.IsNullOrWhiteSpace(parameterId))
            return;

        if (controlValues.TryGetValue(parameterId, out float[] controlValue))
            controlValue[0] = value;
    }

    public void Reset()
    {
        if (disposed)
            return;

        try
        {
            deactivate?.Invoke(instanceHandle);
            activate?.Invoke(instanceHandle);
        }
        catch
        {
            disposed = true;
        }
    }

    public void Process(float[] data, int channels)
    {
        if (disposed || data == null || channels <= 0)
            return;

        int frames = data.Length / channels;
        if (frames <= 0)
            return;

        EnsureAudioBuffers(frames);
        FillInputPorts(data, frames, channels);
        ClearOutputPorts(frames);

        try
        {
            run(instanceHandle, (uint)frames);
        }
        catch
        {
            disposed = true;
            return;
        }

        ReadOutputPorts(data, frames, channels);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        try
        {
            deactivate?.Invoke(instanceHandle);
        }
        catch
        {
            // Ignore plugin shutdown errors.
        }

        try
        {
            cleanup?.Invoke(instanceHandle);
        }
        catch
        {
            // Ignore plugin shutdown errors.
        }

        ReleasePins();
        if (libraryHandle != IntPtr.Zero)
            FreeLibrary(libraryHandle);
        GC.SuppressFinalize(this);
    }

    private void CreateControlPorts(IReadOnlyList<ToneLabExternalParameterSpec> parameterSpecs)
    {
        if (parameterSpecs == null)
            return;

        for (int i = 0; i < parameterSpecs.Count; i++)
        {
            ToneLabExternalParameterSpec spec = parameterSpecs[i];
            if (spec == null || spec.PortIndex < 0 || string.IsNullOrWhiteSpace(spec.ParameterId))
                continue;

            float[] value = { spec.DefaultValue };
            controlPortIndices[spec.ParameterId] = spec.PortIndex;
            controlValues[spec.ParameterId] = value;
            GCHandle pin = GCHandle.Alloc(value, GCHandleType.Pinned);
            pinnedHandles.Add(pin);
            connectPort(instanceHandle, (uint)spec.PortIndex, pin.AddrOfPinnedObject());
        }
    }

    private void EnsureAudioBuffers(int frames)
    {
        int safeFrames = Mathf.Max(1, frames);
        if (safeFrames <= bufferFrames && inputBuffers.Length == inputPorts.Length && outputBuffers.Length == outputPorts.Length)
            return;

        ReleaseAudioPinsOnly();
        bufferFrames = Mathf.Max(safeFrames, DefaultBlockFrames);
        inputBuffers = CreatePinnedAudioBuffers(inputPorts, bufferFrames);
        outputBuffers = CreatePinnedAudioBuffers(outputPorts, bufferFrames);
    }

    private float[][] CreatePinnedAudioBuffers(int[] ports, int frames)
    {
        float[][] buffers = new float[ports.Length][];
        for (int i = 0; i < ports.Length; i++)
        {
            buffers[i] = new float[frames];
            GCHandle pin = GCHandle.Alloc(buffers[i], GCHandleType.Pinned);
            pinnedHandles.Add(pin);
            connectPort(instanceHandle, (uint)ports[i], pin.AddrOfPinnedObject());
        }

        return buffers;
    }

    private void ReleaseAudioPinsOnly()
    {
        for (int i = pinnedHandles.Count - 1; i >= 0; i--)
        {
            GCHandle handle = pinnedHandles[i];
            object target = handle.IsAllocated ? handle.Target : null;
            if (target is float[] buffer && IsAudioBuffer(buffer))
            {
                handle.Free();
                pinnedHandles.RemoveAt(i);
            }
        }
    }

    private bool IsAudioBuffer(float[] buffer)
    {
        if (inputBuffers != null)
        {
            for (int i = 0; i < inputBuffers.Length; i++)
                if (ReferenceEquals(inputBuffers[i], buffer))
                    return true;
        }

        if (outputBuffers != null)
        {
            for (int i = 0; i < outputBuffers.Length; i++)
                if (ReferenceEquals(outputBuffers[i], buffer))
                    return true;
        }

        return false;
    }

    private void ReleasePins()
    {
        for (int i = 0; i < pinnedHandles.Count; i++)
        {
            if (pinnedHandles[i].IsAllocated)
                pinnedHandles[i].Free();
        }

        pinnedHandles.Clear();
    }

    private void FillInputPorts(float[] data, int frames, int channels)
    {
        if (inputBuffers.Length == 1)
        {
            float[] mono = inputBuffers[0];
            for (int frame = 0; frame < frames; frame++)
            {
                float sum = 0f;
                int baseIndex = frame * channels;
                for (int channel = 0; channel < channels; channel++)
                    sum += data[baseIndex + channel];
                mono[frame] = sum / channels;
            }

            return;
        }

        for (int input = 0; input < inputBuffers.Length; input++)
        {
            float[] buffer = inputBuffers[input];
            int sourceChannel = Mathf.Min(input, channels - 1);
            for (int frame = 0; frame < frames; frame++)
                buffer[frame] = data[(frame * channels) + sourceChannel];
        }
    }

    private void ClearOutputPorts(int frames)
    {
        for (int output = 0; output < outputBuffers.Length; output++)
        {
            float[] buffer = outputBuffers[output];
            if (buffer == null)
                continue;

            Array.Clear(buffer, 0, Mathf.Min(frames, buffer.Length));
        }
    }

    private void ReadOutputPorts(float[] data, int frames, int channels)
    {
        if (outputBuffers.Length == 1)
        {
            float[] mono = outputBuffers[0];
            for (int frame = 0; frame < frames; frame++)
            {
                float value = mono[frame];
                int baseIndex = frame * channels;
                for (int channel = 0; channel < channels; channel++)
                    data[baseIndex + channel] = value;
            }

            return;
        }

        for (int frame = 0; frame < frames; frame++)
        {
            int baseIndex = frame * channels;
            for (int channel = 0; channel < channels; channel++)
            {
                int output = Mathf.Min(channel, outputBuffers.Length - 1);
                data[baseIndex + channel] = outputBuffers[output][frame];
            }
        }
    }

    private static IntPtr FindDescriptorPointer(Lv2DescriptorFunction descriptorFunction, string pluginUri)
    {
        for (uint index = 0; index < 512; index++)
        {
            IntPtr descriptorPointer = descriptorFunction(index);
            if (descriptorPointer == IntPtr.Zero)
                return IntPtr.Zero;

            Lv2DescriptorNative descriptor = Marshal.PtrToStructure<Lv2DescriptorNative>(descriptorPointer);
            string uri = Marshal.PtrToStringAnsi(descriptor.uri) ?? string.Empty;
            if (string.Equals(uri, pluginUri, StringComparison.Ordinal))
                return descriptorPointer;
        }

        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Lv2DescriptorNative
    {
        public IntPtr uri;
        public IntPtr instantiate;
        public IntPtr connect_port;
        public IntPtr activate;
        public IntPtr run;
        public IntPtr deactivate;
        public IntPtr cleanup;
        public IntPtr extension_data;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr Lv2DescriptorFunction(uint index);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate IntPtr InstantiateDelegate(IntPtr descriptor, double sampleRate, string bundlePath, IntPtr features);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ConnectPortDelegate(IntPtr instance, uint port, IntPtr dataLocation);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ActivateDelegate(IntPtr instance);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void RunDelegate(IntPtr instance, uint sampleCount);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DeactivateDelegate(IntPtr instance);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void CleanupDelegate(IntPtr instance);

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryW(string lpFileName);

    private const uint LoadWithAlteredSearchPath = 0x00000008;

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryExW(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("kernel32", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);
}

public static class ToneLabNativeHost
{
    private const string DllName = "StringTheoryToneHost";
    private static bool availabilityChecked;
    private static bool available;
    private static int apiVersion;

    public static bool IsAvailable
    {
        get
        {
            if (availabilityChecked)
                return available;

            availabilityChecked = true;
            try
            {
                apiVersion = st_get_api_version();
                available = apiVersion >= 1;
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is BadImageFormatException)
            {
                apiVersion = 0;
                available = false;
            }

            return available;
        }
    }

    public static IntPtr CreateLv2(string pluginUri, string searchPath, int sampleRate, int channels, int maxBlockFrames)
    {
        if (!IsAvailable || apiVersion < 3 || string.IsNullOrWhiteSpace(pluginUri))
            return IntPtr.Zero;

        try
        {
            return st_create_lv2_instance(pluginUri, searchPath ?? string.Empty, sampleRate, channels, maxBlockFrames);
        }
        catch
        {
            available = false;
            return IntPtr.Zero;
        }
    }

    public static IntPtr CreateNam(string modelPath, int sampleRate, int channels, int maxBlockFrames)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(modelPath))
            return IntPtr.Zero;

        try
        {
            return st_create_nam_instance(modelPath, sampleRate, channels, maxBlockFrames);
        }
        catch
        {
            available = false;
            return IntPtr.Zero;
        }
    }

    public static void Destroy(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !IsAvailable)
            return;

        try
        {
            st_destroy_instance(handle);
        }
        catch
        {
            available = false;
        }
    }

    public static void Reset(IntPtr handle)
    {
        if (handle == IntPtr.Zero || !IsAvailable)
            return;

        try
        {
            st_reset_instance(handle);
        }
        catch
        {
            available = false;
        }
    }

    public static void SetParameter(IntPtr handle, string parameterId, float value)
    {
        if (handle == IntPtr.Zero || !IsAvailable || string.IsNullOrWhiteSpace(parameterId))
            return;

        try
        {
            st_set_parameter(handle, parameterId, value);
        }
        catch
        {
            available = false;
        }
    }

    public static bool ProcessInterleaved(IntPtr handle, float[] data, int frames, int channels)
    {
        if (handle == IntPtr.Zero || data == null || frames <= 0 || channels <= 0 || !IsAvailable)
            return false;

        try
        {
            return st_process_interleaved(handle, data, frames, channels) != 0;
        }
        catch
        {
            available = false;
            return false;
        }
    }

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int st_get_api_version();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr st_create_lv2_instance([MarshalAs(UnmanagedType.LPUTF8Str)] string pluginUri, [MarshalAs(UnmanagedType.LPUTF8Str)] string searchPath, int sampleRate, int channels, int maxBlockFrames);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr st_create_nam_instance([MarshalAs(UnmanagedType.LPUTF8Str)] string modelPath, int sampleRate, int channels, int maxBlockFrames);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void st_destroy_instance(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void st_reset_instance(IntPtr handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void st_set_parameter(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string parameterId, float value);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int st_process_interleaved(IntPtr handle, [In, Out] float[] data, int frames, int channels);
}
