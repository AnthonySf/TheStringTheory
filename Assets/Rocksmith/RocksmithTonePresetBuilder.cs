using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class RocksmithTonePresetBuilder
{
    public const string GeneratedPresetIdPrefix = "rsgen_";

    private const float DefaultKnobValue = 0.5f;
    private const float RocksmithReferenceToneVolumeDb = -20f;
    private const float MinRocksmithToneVolumeDb = -45f;
    private const float MaxRocksmithToneVolumeDb = 45f;
    private const float MinGeneratedOutputGainDb = -12f;
    private const float MaxGeneratedOutputGainDb = 18f;

    public static bool IsGeneratedPresetId(string presetId)
    {
        return !string.IsNullOrWhiteSpace(presetId) &&
               presetId.StartsWith(GeneratedPresetIdPrefix, StringComparison.Ordinal);
    }

    public static bool TryBuildPreset(
        string toneName,
        string arrangementRoute,
        string rawToneJson,
        out UnityToneLabRuntime.ToneLabPreset preset)
    {
        preset = null;
        if (string.IsNullOrWhiteSpace(rawToneJson) ||
            !RocksmithToneJsonParser.TryParse(rawToneJson, out object parsed) ||
            parsed is not Dictionary<string, object> root)
        {
            return false;
        }

        if (!TryGetObject(root, "GearList", out Dictionary<string, object> gearList))
            return false;

        List<RocksmithGear> gears = ExtractGears(gearList);
        if (gears.Count == 0)
            return false;

        float? rootToneVolumeDb = TryGetRootToneVolumeDb(root);
        ToneBuildContext context = BuildContext(toneName, arrangementRoute, gears, rootToneVolumeDb);
        List<UnityToneLabRuntime.ToneLabPedalSlot> chain = BuildPedalChain(context, gears);
        if (chain.Count == 0)
            return false;

        preset = new UnityToneLabRuntime.ToneLabPreset
        {
            preset_id = BuildGeneratedPresetId(toneName, arrangementRoute, rawToneJson),
            preset_name = BuildGeneratedPresetName(toneName),
            input_gain_db = context.InputGainDb,
            output_gain_db = context.OutputGainDb,
            pedal_chain = chain
        };
        return true;
    }

    private static List<RocksmithGear> ExtractGears(Dictionary<string, object> gearList)
    {
        List<RocksmithGear> gears = new List<RocksmithGear>();
        if (gearList == null)
            return gears;

        foreach (KeyValuePair<string, object> pair in gearList.OrderBy(pair => GetGearSlotOrder(pair.Key)))
        {
            if (pair.Value is Dictionary<string, object> gearObject)
            {
                AddGearIfUsable(gears, pair.Key, gearObject);
                continue;
            }

            if (pair.Value is List<object> gearArray)
            {
                for (int i = 0; i < gearArray.Count; i++)
                {
                    if (gearArray[i] is Dictionary<string, object> arrayGear)
                        AddGearIfUsable(gears, $"{pair.Key}{i}", arrayGear);
                }
            }
        }

        return gears;
    }

    private static void AddGearIfUsable(List<RocksmithGear> gears, string slotKey, Dictionary<string, object> gearObject)
    {
        if (gears == null || gearObject == null)
            return;

        string type = GetString(gearObject, "Type");
        string name = FirstNonEmpty(
            GetString(gearObject, "Name"),
            GetString(gearObject, "DisplayName"),
            GetString(gearObject, "Key"),
            GetString(gearObject, "PedalKey"),
            GetString(gearObject, "ModelName"));
        string category = FirstNonEmpty(
            GetString(gearObject, "Category"),
            GetString(gearObject, "GearType"),
            GetString(gearObject, "Slot"));

        if (LooksEmptyGear(type) && LooksEmptyGear(name) && LooksEmptyGear(category))
            return;

        bool enabled = !GetBool(gearObject, "Bypassed", false) &&
                       !GetBool(gearObject, "Bypass", false) &&
                       GetBool(gearObject, "Enabled", true);
        if (!enabled)
            return;

        gears.Add(new RocksmithGear
        {
            SlotKey = slotKey ?? string.Empty,
            Type = type ?? string.Empty,
            Name = name ?? string.Empty,
            Category = category ?? string.Empty,
            Knobs = ExtractKnobs(gearObject)
        });
    }

    private static Dictionary<string, float> ExtractKnobs(Dictionary<string, object> gearObject)
    {
        Dictionary<string, float> knobs = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        if (gearObject == null)
            return knobs;

        foreach (string containerName in new[] { "KnobValues", "Knobs", "Parameters", "Values" })
        {
            if (!TryGetValue(gearObject, containerName, out object container))
                continue;

            if (container is Dictionary<string, object> knobObject)
            {
                foreach (KeyValuePair<string, object> pair in knobObject)
                {
                    if (TryGetFloat(pair.Value, out float value))
                        knobs[pair.Key] = value;
                }
            }
            else if (container is List<object> knobArray)
            {
                for (int i = 0; i < knobArray.Count; i++)
                {
                    if (knobArray[i] is not Dictionary<string, object> knobEntry)
                        continue;

                    string key = FirstNonEmpty(
                        GetString(knobEntry, "Key"),
                        GetString(knobEntry, "Name"),
                        GetString(knobEntry, "Id"),
                        GetString(knobEntry, "Parameter"));
                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    if (TryGetValue(knobEntry, "Value", out object valueObject) &&
                        TryGetFloat(valueObject, out float value))
                    {
                        knobs[key] = value;
                    }
                }
            }
        }

        foreach (KeyValuePair<string, object> pair in gearObject)
        {
            if (IsStructuralGearField(pair.Key))
                continue;

            if (TryGetFloat(pair.Value, out float value))
                knobs[pair.Key] = value;
        }

        return knobs;
    }

    private static ToneBuildContext BuildContext(
        string toneName,
        string arrangementRoute,
        IReadOnlyList<RocksmithGear> gears,
        float? rootToneVolumeDb)
    {
        string arrangementText = NormalizeText($"{toneName} {arrangementRoute}");
        string gearText = BuildGearSearchText(gears);
        string searchText = NormalizeText($"{arrangementText} {gearText}");
        bool bass = ContainsToken(arrangementText, "bass");
        bool highGain = ContainsAny(searchText, "metal", "rect", "mesa", "5150", "engl", "powerball", "high gain", "djent", "heavy");

        float driveIntent = highGain ? 0.75f : 0.25f;
        float bassIntent = bass ? 0.72f : 0.50f;
        float midIntent = highGain ? 0.38f : 0.52f;
        float trebleIntent = highGain ? 0.56f : 0.52f;
        float presenceIntent = highGain ? 0.66f : 0.48f;
        bool explicitGate = false;

        if (gears != null)
        {
            foreach (RocksmithGear gear in gears)
            {
                if (gear == null)
                    continue;

                GearFamily family = ClassifyGear(gear);
                CanonicalControls controls = CanonicalControls.FromKnobs(gear.Knobs);
                driveIntent = Mathf.Max(driveIntent, controls.Get("drive", 0f));
                bassIntent = MergeIntent(bassIntent, controls.TryGet("bass", out float bassValue) ? bassValue : float.NaN);
                midIntent = MergeIntent(midIntent, controls.TryGet("mid", out float midValue) ? midValue : float.NaN);
                trebleIntent = MergeIntent(trebleIntent, controls.TryGet("treble", out float trebleValue) ? trebleValue : float.NaN);
                presenceIntent = MergeIntent(presenceIntent, controls.TryGet("presence", out float presenceValue) ? presenceValue : float.NaN);
                explicitGate |= family == GearFamily.NoiseGate;
            }
        }

        float inputGain = Mathf.Lerp(12.2f, 15.0f, driveIntent);
        float outputGain = Mathf.Lerp(8.6f, 6.5f, driveIntent);
        if (bass)
        {
            inputGain = Mathf.Min(inputGain, 13.0f);
            outputGain = Mathf.Max(outputGain, 7.4f);
        }

        outputGain = ApplyRocksmithToneVolume(outputGain, rootToneVolumeDb);

        return new ToneBuildContext
        {
            SearchText = searchText,
            IsBass = bass,
            HighGain = highGain || driveIntent >= 0.68f,
            DriveIntent = Mathf.Clamp01(driveIntent),
            BassIntent = Mathf.Clamp01(bassIntent),
            MidIntent = Mathf.Clamp01(midIntent),
            TrebleIntent = Mathf.Clamp01(trebleIntent),
            PresenceIntent = Mathf.Clamp01(presenceIntent),
            NeedsGate = explicitGate || highGain || driveIntent >= 0.62f,
            InputGainDb = inputGain,
            OutputGainDb = outputGain
        };
    }

    private static float? TryGetRootToneVolumeDb(Dictionary<string, object> root)
    {
        if (root == null ||
            !TryGetValue(root, "Volume", out object rawVolume) ||
            !TryGetFloat(rawVolume, out float volumeDb) ||
            float.IsNaN(volumeDb) ||
            float.IsInfinity(volumeDb))
        {
            return null;
        }

        return Mathf.Clamp(volumeDb, MinRocksmithToneVolumeDb, MaxRocksmithToneVolumeDb);
    }

    private static float ApplyRocksmithToneVolume(float outputGainDb, float? rocksmithToneVolumeDb)
    {
        if (!rocksmithToneVolumeDb.HasValue)
            return outputGainDb;

        // Rocksmith tone volume is inverted in Toolkit/exported tones: 0 is soft, -20 is normal, -30 is loud.
        float correctionDb = RocksmithReferenceToneVolumeDb - rocksmithToneVolumeDb.Value;
        return Mathf.Clamp(outputGainDb + correctionDb, MinGeneratedOutputGainDb, MaxGeneratedOutputGainDb);
    }

    private static string BuildGearSearchText(IReadOnlyList<RocksmithGear> gears)
    {
        if (gears == null || gears.Count == 0)
            return string.Empty;

        StringBuilder builder = new StringBuilder(gears.Count * 48);
        for (int i = 0; i < gears.Count; i++)
        {
            RocksmithGear gear = gears[i];
            if (gear == null)
                continue;

            builder.Append(' ');
            builder.Append(gear.SlotKey);
            builder.Append(' ');
            builder.Append(gear.Type);
            builder.Append(' ');
            builder.Append(gear.Name);
            builder.Append(' ');
            builder.Append(gear.Category);
        }

        return builder.ToString();
    }

    private static List<UnityToneLabRuntime.ToneLabPedalSlot> BuildPedalChain(ToneBuildContext context, IReadOnlyList<RocksmithGear> gears)
    {
        List<UnityToneLabRuntime.ToneLabPedalSlot> dynamics = new List<UnityToneLabRuntime.ToneLabPedalSlot>();
        List<UnityToneLabRuntime.ToneLabPedalSlot> gain = new List<UnityToneLabRuntime.ToneLabPedalSlot>();
        List<UnityToneLabRuntime.ToneLabPedalSlot> modulation = new List<UnityToneLabRuntime.ToneLabPedalSlot>();
        List<UnityToneLabRuntime.ToneLabPedalSlot> ambience = new List<UnityToneLabRuntime.ToneLabPedalSlot>();
        bool sawKnownGear = false;
        bool hasCompressor = false;
        bool hasDelay = false;
        bool hasReverb = false;
        int drivePedalCount = 0;

        foreach (RocksmithGear gear in gears)
        {
            GearFamily family = ClassifyGear(gear);
            CanonicalControls controls = CanonicalControls.FromKnobs(gear.Knobs);
            switch (family)
            {
                case GearFamily.NoiseGate:
                    context.NeedsGate = true;
                    sawKnownGear = true;
                    break;
                case GearFamily.Compressor:
                    if (!hasCompressor)
                    {
                        dynamics.Add(CreateSlot(UnityToneLabRuntime.ToneLabPedalType.Compressor, new CompressorPedalSettings
                        {
                            threshold_db = controls.GetDb("threshold", Mathf.Lerp(-30f, -20f, context.DriveIntent), -60f, 0f),
                            ratio = controls.GetRatio("ratio", Mathf.Lerp(2.2f, 4.4f, context.DriveIntent)),
                            attack_ms = controls.GetMilliseconds("attack", 10f, 1f, 120f),
                            release_ms = controls.GetMilliseconds("release", 150f, 20f, 600f)
                        }));
                        hasCompressor = true;
                    }
                    sawKnownGear = true;
                    break;
                case GearFamily.Drive:
                    if (drivePedalCount < 2)
                    {
                        float drive = controls.Get("drive", context.DriveIntent);
                        gain.Add(CreateSlot(UnityToneLabRuntime.ToneLabPedalType.Distortion, new DistortionPedalSettings
                        {
                            drive_db = Mathf.Lerp(6f, 33f, ApplyDriveCurve(drive))
                        }));
                        drivePedalCount++;
                    }
                    sawKnownGear = true;
                    break;
                case GearFamily.Chorus:
                    modulation.Add(CreateSlot(UnityToneLabRuntime.ToneLabPedalType.Chorus, new ChorusPedalSettings
                    {
                        rate_hz = controls.GetRateHz("rate", 0.75f),
                        depth = controls.Get("depth", 0.34f),
                        mix = controls.Get("mix", 0.24f)
                    }));
                    sawKnownGear = true;
                    break;
                case GearFamily.Phaser:
                    modulation.Add(CreateSlot(UnityToneLabRuntime.ToneLabPedalType.Phaser, new PhaserPedalSettings
                    {
                        rate_hz = controls.GetRateHz("rate", 0.55f),
                        depth = controls.Get("depth", 0.50f),
                        mix = controls.Get("mix", 0.22f),
                        center_hz = Mathf.Lerp(380f, 2100f, controls.Get("tone", 0.50f)),
                        feedback = Mathf.Lerp(0.02f, 0.34f, controls.Get("feedback", 0.25f))
                    }));
                    sawKnownGear = true;
                    break;
                case GearFamily.Delay:
                    if (!hasDelay)
                    {
                        ambience.Add(CreateSlot(UnityToneLabRuntime.ToneLabPedalType.Delay, new DelayPedalSettings
                        {
                            delay_seconds = controls.GetSeconds("time", 0.28f),
                            feedback = controls.Get("feedback", 0.30f),
                            mix = controls.Get("mix", 0.20f)
                        }));
                        hasDelay = true;
                    }
                    sawKnownGear = true;
                    break;
                case GearFamily.Reverb:
                    if (!hasReverb)
                    {
                        ambience.Add(CreateSlot(UnityToneLabRuntime.ToneLabPedalType.Reverb, new ReverbPedalSettings
                        {
                            room_size = controls.Get("size", controls.Get("room", 0.46f)),
                            damping = 1f - controls.Get("tone", 0.62f),
                            wet = Mathf.Lerp(0.08f, 0.36f, controls.Get("mix", 0.30f)),
                            dry = 0.96f,
                            width = 1f,
                            freeze = 0f
                        }));
                        hasReverb = true;
                    }
                    sawKnownGear = true;
                    break;
                case GearFamily.Amp:
                case GearFamily.Cab:
                case GearFamily.Eq:
                    sawKnownGear = true;
                    break;
            }
        }

        if (!sawKnownGear)
            return new List<UnityToneLabRuntime.ToneLabPedalSlot>();

        List<UnityToneLabRuntime.ToneLabPedalSlot> chain = new List<UnityToneLabRuntime.ToneLabPedalSlot>();
        if (context.NeedsGate)
        {
            chain.Add(CreateSlot(UnityToneLabRuntime.ToneLabPedalType.NoiseGate, new NoiseGatePedalSettings
            {
                threshold_db = Mathf.Lerp(-62f, -40f, context.DriveIntent),
                attack_ms = context.HighGain ? 0.8f : 3f,
                hold_ms = context.HighGain ? 20f : 35f,
                release_ms = context.HighGain ? 70f : 125f,
                range_db = -80f
            }));
        }

        chain.AddRange(dynamics);
        chain.AddRange(gain);
        chain.Add(CreateSlot(UnityToneLabRuntime.ToneLabPedalType.Amp, BuildAmpSettings(context)));
        chain.Add(CreateSlot(UnityToneLabRuntime.ToneLabPedalType.CabSim, BuildCabSettings(context)));
        chain.Add(CreateSlot(UnityToneLabRuntime.ToneLabPedalType.StudioEq, BuildEqSettings(context)));
        chain.AddRange(modulation);
        chain.AddRange(ambience);
        if (!hasReverb)
        {
            chain.Add(CreateSlot(UnityToneLabRuntime.ToneLabPedalType.Reverb, new ReverbPedalSettings
            {
                room_size = context.HighGain ? 0.18f : 0.32f,
                damping = context.HighGain ? 0.56f : 0.42f,
                wet = context.HighGain ? 0.08f : 0.15f,
                dry = 0.96f,
                width = 0.95f,
                freeze = 0f
            }));
        }

        return chain;
    }

    private static AmpPedalSettings BuildAmpSettings(ToneBuildContext context)
    {
        float gainDb = Mathf.Lerp(context.IsBass ? 9f : 7f, context.HighGain ? 39f : 27f, ApplyDriveCurve(context.DriveIntent));
        return new AmpPedalSettings
        {
            gain_db = gainDb,
            tone = Mathf.Clamp01((context.TrebleIntent * 0.54f) + (context.MidIntent * 0.22f) + 0.12f),
            presence = Mathf.Clamp01((context.PresenceIntent * 0.70f) + (context.TrebleIntent * 0.20f)),
            master_db = Mathf.Lerp(1.1f, -1.1f, context.DriveIntent),
            sag = context.HighGain ? 0.08f : Mathf.Lerp(0.05f, 0.20f, context.DriveIntent)
        };
    }

    private static CabSimPedalSettings BuildCabSettings(ToneBuildContext context)
    {
        return new CabSimPedalSettings
        {
            thump = context.IsBass ? Mathf.Max(0.78f, context.BassIntent) : Mathf.Clamp01(context.BassIntent),
            presence = Mathf.Clamp01((context.PresenceIntent * 0.65f) + (context.MidIntent * 0.20f)),
            air = context.HighGain ? Mathf.Lerp(0.18f, 0.38f, context.TrebleIntent) : Mathf.Lerp(0.38f, 0.72f, context.TrebleIntent),
            mix = 1f
        };
    }

    private static StudioEqPedalSettings BuildEqSettings(ToneBuildContext context)
    {
        float lowShelf = Mathf.Lerp(-4.5f, 4.5f, context.BassIntent);
        float mid = Mathf.Lerp(-5.5f, 4.0f, context.MidIntent);
        float highShelf = Mathf.Lerp(-4.0f, 4.5f, context.TrebleIntent);
        if (context.HighGain)
        {
            lowShelf -= 1.2f;
            mid -= 1.6f;
            highShelf += 0.6f;
        }

        return new StudioEqPedalSettings
        {
            low_cut_hz = context.IsBass ? 45f : Mathf.Lerp(65f, 95f, context.DriveIntent),
            low_shelf_db = Mathf.Clamp(lowShelf, -12f, 12f),
            mid_db = Mathf.Clamp(mid, -12f, 12f),
            high_shelf_db = Mathf.Clamp(highShelf, -12f, 12f),
            high_cut_hz = context.IsBass
                ? Mathf.Lerp(3600f, 5200f, context.TrebleIntent)
                : (context.HighGain ? Mathf.Lerp(4800f, 6500f, context.TrebleIntent) : Mathf.Lerp(6200f, 9800f, context.TrebleIntent))
        };
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateSlot(UnityToneLabRuntime.ToneLabPedalType pedalType, object settingsObject)
    {
        IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(pedalType);
        return new UnityToneLabRuntime.ToneLabPedalSlot
        {
            pedal_instance_id = Guid.NewGuid().ToString("N"),
            pedal_type = pedalType,
            descriptor_id = descriptor.DescriptorId,
            enabled = true,
            settings_json = descriptor.SerializeSettingsObject(settingsObject ?? descriptor.CreateDefaultSettingsObject())
        };
    }

    private static GearFamily ClassifyGear(RocksmithGear gear)
    {
        string text = NormalizeText($"{gear?.SlotKey} {gear?.Type} {gear?.Name} {gear?.Category}");
        if (string.IsNullOrWhiteSpace(text))
            return GearFamily.Unknown;
        if (ContainsAny(text, "gate", "noise suppress", "noise reduction", "hush"))
            return GearFamily.NoiseGate;
        if (ContainsAny(text, "compress", "sustain", "limiter"))
            return GearFamily.Compressor;
        if (ContainsAny(text, "amp") || text.StartsWith("amp ", StringComparison.Ordinal) || text.StartsWith("amp_", StringComparison.Ordinal))
            return GearFamily.Amp;
        if (ContainsAny(text, "cab", "cabinet", "speaker"))
            return GearFamily.Cab;
        if (ContainsAny(text, "eq", "equalizer", "filter"))
            return GearFamily.Eq;
        if (ContainsAny(text, "delay", "echo"))
            return GearFamily.Delay;
        if (ContainsAny(text, "reverb", "room", "hall", "plate", "spring"))
            return GearFamily.Reverb;
        if (ContainsAny(text, "chorus", "flanger", "rotary", "doubler"))
            return GearFamily.Chorus;
        if (ContainsAny(text, "phaser", "phase", "vibe", "tremolo", "wah", "envelope"))
            return GearFamily.Phaser;
        if (ContainsAny(text, "drive", "dist", "fuzz", "muff", "screamer", "overdrive", "boost", "rat", "ds1", "sd1", "octave", "pitch", "whammy"))
            return GearFamily.Drive;
        return GearFamily.Unknown;
    }

    private static int GetGearSlotOrder(string key)
    {
        string text = NormalizeText(key);
        int number = ExtractFirstInteger(text);
        int offset = number >= 0 ? number : 0;
        if (text.Contains("gate", StringComparison.Ordinal))
            return 0 + offset;
        if (text.Contains("pre", StringComparison.Ordinal) || text.Contains("pedal", StringComparison.Ordinal) || text.Contains("stomp", StringComparison.Ordinal))
            return 20 + offset;
        if (text.Contains("amp", StringComparison.Ordinal))
            return 100;
        if (text.Contains("cab", StringComparison.Ordinal))
            return 110;
        if (text.Contains("rack", StringComparison.Ordinal) || text.Contains("post", StringComparison.Ordinal))
            return 180 + offset;
        return 80 + offset;
    }

    private static int ExtractFirstInteger(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return -1;

        int value = 0;
        bool found = false;
        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsDigit(text[i]))
            {
                if (found)
                    return value;
                continue;
            }

            found = true;
            value = (value * 10) + (text[i] - '0');
        }

        return found ? value : -1;
    }

    private static float ApplyDriveCurve(float normalized)
    {
        float clamped = Mathf.Clamp01(normalized);
        return clamped * clamped * (3f - (2f * clamped));
    }

    private static float MergeIntent(float current, float candidate)
    {
        return float.IsNaN(candidate)
            ? current
            : Mathf.Clamp01((current * 0.42f) + (candidate * 0.58f));
    }

    private static string BuildGeneratedPresetId(string toneName, string arrangementRoute, string rawToneJson)
    {
        string key = $"{toneName ?? string.Empty}\n{arrangementRoute ?? string.Empty}\n{rawToneJson ?? string.Empty}";
        byte[] hashBytes;
        using (SHA1 sha1 = SHA1.Create())
            hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(key));
        StringBuilder builder = new StringBuilder(GeneratedPresetIdPrefix, GeneratedPresetIdPrefix.Length + 16);
        for (int i = 0; i < 8 && i < hashBytes.Length; i++)
            builder.Append(hashBytes[i].ToString("x2", CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static string BuildGeneratedPresetName(string toneName)
    {
        return string.IsNullOrWhiteSpace(toneName) ? "Rocksmith Tone" : toneName.Trim();
    }

    private static bool LooksEmptyGear(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        string normalized = NormalizeText(value);
        return normalized == "none" ||
               normalized == "null" ||
               normalized == "empty" ||
               normalized == "bypass" ||
               normalized == "n/a";
    }

    private static bool IsStructuralGearField(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return true;

        string normalized = NormalizeText(key);
        return normalized is "type" or "name" or "displayname" or "key" or "pedalkey" or "modelname" or "category" or "geartype" or "slot" or "knobvalues" or "knobs" or "parameters" or "values" or "enabled" or "bypassed" or "bypass" or "skin";
    }

    private static bool TryGetObject(Dictionary<string, object> source, string key, out Dictionary<string, object> value)
    {
        value = null;
        return TryGetValue(source, key, out object raw) &&
               (value = raw as Dictionary<string, object>) != null;
    }

    private static bool TryGetValue(Dictionary<string, object> source, string key, out object value)
    {
        value = null;
        if (source == null || string.IsNullOrWhiteSpace(key))
            return false;

        return source.TryGetValue(key, out value);
    }

    private static string GetString(Dictionary<string, object> source, string key)
    {
        if (!TryGetValue(source, key, out object value) || value == null)
            return string.Empty;

        return value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static bool GetBool(Dictionary<string, object> source, string key, bool fallback)
    {
        if (!TryGetValue(source, key, out object value) || value == null)
            return fallback;

        if (value is bool boolValue)
            return boolValue;

        string text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return bool.TryParse(text, out bool parsed) ? parsed : fallback;
    }

    private static bool TryGetFloat(object value, out float result)
    {
        result = 0f;
        switch (value)
        {
            case float floatValue:
                result = floatValue;
                return float.IsFinite(result);
            case double doubleValue:
                result = (float)doubleValue;
                return float.IsFinite(result);
            case int intValue:
                result = intValue;
                return true;
            case long longValue:
                result = longValue;
                return float.IsFinite(result);
            case string text:
                return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
                       float.IsFinite(result);
            default:
                return false;
        }
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
            return string.Empty;

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
                return values[i].Trim();
        }

        return string.Empty;
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        StringBuilder builder = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = char.ToLowerInvariant(text[i]);
            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return string.Join(" ", builder.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(haystack) || needles == null)
            return false;

        for (int i = 0; i < needles.Length; i++)
        {
            string needle = NormalizeText(needles[i]);
            if (!string.IsNullOrWhiteSpace(needle) && haystack.Contains(needle, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool ContainsToken(string normalizedHaystack, string token)
    {
        if (string.IsNullOrWhiteSpace(normalizedHaystack) || string.IsNullOrWhiteSpace(token))
            return false;

        string normalizedToken = NormalizeText(token);
        if (string.IsNullOrWhiteSpace(normalizedToken))
            return false;

        string paddedHaystack = $" {normalizedHaystack} ";
        return paddedHaystack.Contains($" {normalizedToken} ", StringComparison.Ordinal);
    }

    private enum GearFamily
    {
        Unknown,
        NoiseGate,
        Compressor,
        Drive,
        Amp,
        Cab,
        Eq,
        Chorus,
        Phaser,
        Delay,
        Reverb
    }

    private sealed class RocksmithGear
    {
        public string SlotKey;
        public string Type;
        public string Name;
        public string Category;
        public Dictionary<string, float> Knobs = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ToneBuildContext
    {
        public string SearchText;
        public bool IsBass;
        public bool HighGain;
        public bool NeedsGate;
        public float DriveIntent;
        public float BassIntent;
        public float MidIntent;
        public float TrebleIntent;
        public float PresenceIntent;
        public float InputGainDb;
        public float OutputGainDb;
    }

    private sealed class CanonicalControls
    {
        private readonly Dictionary<string, float> values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        public static CanonicalControls FromKnobs(Dictionary<string, float> knobs)
        {
            CanonicalControls controls = new CanonicalControls();
            if (knobs == null)
                return controls;

            foreach (KeyValuePair<string, float> pair in knobs)
            {
                string control = ResolveCanonicalControl(pair.Key);
                if (string.IsNullOrWhiteSpace(control))
                    continue;

                float normalized = NormalizeKnobValue(pair.Value, control);
                if (controls.values.TryGetValue(control, out float existing))
                    controls.values[control] = Mathf.Clamp01((existing + normalized) * 0.5f);
                else
                    controls.values[control] = normalized;
            }

            return controls;
        }

        public bool TryGet(string key, out float value)
        {
            return values.TryGetValue(key, out value);
        }

        public float Get(string key, float fallback)
        {
            return values.TryGetValue(key, out float value) ? value : fallback;
        }

        public float GetDb(string key, float fallbackDb, float minDb, float maxDb)
        {
            return values.TryGetValue(key, out float value)
                ? Mathf.Lerp(minDb, maxDb, Mathf.Clamp01(value))
                : fallbackDb;
        }

        public float GetRatio(string key, float fallback)
        {
            return values.TryGetValue(key, out float value)
                ? Mathf.Lerp(1f, 8f, Mathf.Clamp01(value))
                : fallback;
        }

        public float GetMilliseconds(string key, float fallback, float minMs, float maxMs)
        {
            return values.TryGetValue(key, out float value)
                ? Mathf.Lerp(minMs, maxMs, Mathf.Clamp01(value))
                : fallback;
        }

        public float GetSeconds(string key, float fallback)
        {
            return values.TryGetValue(key, out float value)
                ? Mathf.Lerp(0.06f, 1.10f, Mathf.Clamp01(value))
                : fallback;
        }

        public float GetRateHz(string key, float fallback)
        {
            return values.TryGetValue(key, out float value)
                ? Mathf.Lerp(0.12f, 3.2f, Mathf.Clamp01(value))
                : fallback;
        }

        private static string ResolveCanonicalControl(string rawName)
        {
            string name = NormalizeText(rawName);
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            if (ContainsAny(name, "drive", "gain", "dist", "distortion", "input", "pregain"))
                return "drive";
            if (ContainsAny(name, "volume", "level", "output", "master", "makeup", "trim"))
                return "level";
            if (ContainsAny(name, "tone", "color", "filter"))
                return "tone";
            if (ContainsAny(name, "bass", "low"))
                return "bass";
            if (ContainsAny(name, "middle", "mid", "mids"))
                return "mid";
            if (ContainsAny(name, "treble", "high"))
                return "treble";
            if (ContainsAny(name, "presence", "bright", "top", "edge", "air"))
                return "presence";
            if (ContainsAny(name, "mix", "wet", "drywet", "blend"))
                return "mix";
            if (ContainsAny(name, "rate", "speed", "freq", "frequency"))
                return "rate";
            if (ContainsAny(name, "depth", "intensity", "width", "range"))
                return "depth";
            if (ContainsAny(name, "feedback", "regen", "repeat"))
                return "feedback";
            if (ContainsAny(name, "time", "delay"))
                return "time";
            if (ContainsAny(name, "threshold", "thresh"))
                return "threshold";
            if (ContainsAny(name, "ratio"))
                return "ratio";
            if (ContainsAny(name, "attack"))
                return "attack";
            if (ContainsAny(name, "release", "decay"))
                return "release";
            if (ContainsAny(name, "room", "size"))
                return "size";
            return string.Empty;
        }

        private static float NormalizeKnobValue(float value, string control)
        {
            if (!float.IsFinite(value))
                return DefaultKnobValue;

            if (IsPercentageControl(control))
                return NormalizePercentageKnob(value);

            if (string.Equals(control, "time", StringComparison.OrdinalIgnoreCase))
            {
                if (value > 10f)
                    return Mathf.InverseLerp(60f, 1100f, Mathf.Clamp(value, 60f, 1100f));
                return NormalizeGenericKnob(value);
            }

            if (string.Equals(control, "threshold", StringComparison.OrdinalIgnoreCase) && value < 0f)
                return Mathf.InverseLerp(-80f, -20f, Mathf.Clamp(value, -80f, -20f));

            if (string.Equals(control, "ratio", StringComparison.OrdinalIgnoreCase) && value > 1f)
                return Mathf.InverseLerp(1f, 8f, Mathf.Clamp(value, 1f, 8f));

            return NormalizeGenericKnob(value);
        }

        private static bool IsPercentageControl(string control)
        {
            return string.Equals(control, "mix", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(control, "feedback", StringComparison.OrdinalIgnoreCase);
        }

        private static float NormalizePercentageKnob(float value)
        {
            float absolute = Mathf.Abs(value);
            if (absolute <= 1.0001f)
                return Mathf.Clamp01(value);
            return Mathf.Clamp01(value / 100f);
        }

        private static float NormalizeGenericKnob(float value)
        {
            float absolute = Mathf.Abs(value);
            if (absolute <= 1.0001f)
                return Mathf.Clamp01(value);
            if (absolute <= 10.0001f)
                return Mathf.Clamp01(value / 10f);
            if (absolute <= 100.0001f)
                return Mathf.Clamp01(value / 100f);
            return Mathf.Clamp01(value / 1000f);
        }
    }

    private sealed class RocksmithToneJsonParser
    {
        private readonly string json;
        private int index;

        private RocksmithToneJsonParser(string json)
        {
            this.json = json ?? string.Empty;
        }

        public static bool TryParse(string json, out object value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                RocksmithToneJsonParser parser = new RocksmithToneJsonParser(json);
                value = parser.ParseValue(0);
                parser.SkipWhitespace();
                return parser.index == parser.json.Length;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        private object ParseValue(int depth)
        {
            if (depth > 96)
                throw new FormatException("JSON nesting too deep.");

            SkipWhitespace();
            if (index >= json.Length)
                throw new FormatException("Unexpected end of JSON.");

            char c = json[index];
            if (c == '{')
                return ParseObject(depth + 1);
            if (c == '[')
                return ParseArray(depth + 1);
            if (c == '"')
                return ParseString();
            if (c == 't')
                return ParseLiteral("true", true);
            if (c == 'f')
                return ParseLiteral("false", false);
            if (c == 'n')
                return ParseLiteral("null", null);
            return ParseNumber();
        }

        private Dictionary<string, object> ParseObject(int depth)
        {
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            index++;
            SkipWhitespace();
            if (TryConsume('}'))
                return result;

            while (true)
            {
                SkipWhitespace();
                string key = ParseString();
                SkipWhitespace();
                Expect(':');
                result[key] = ParseValue(depth);
                SkipWhitespace();
                if (TryConsume('}'))
                    return result;
                Expect(',');
            }
        }

        private List<object> ParseArray(int depth)
        {
            List<object> result = new List<object>();
            index++;
            SkipWhitespace();
            if (TryConsume(']'))
                return result;

            while (true)
            {
                result.Add(ParseValue(depth));
                SkipWhitespace();
                if (TryConsume(']'))
                    return result;
                Expect(',');
            }
        }

        private string ParseString()
        {
            Expect('"');
            StringBuilder builder = new StringBuilder();
            while (index < json.Length)
            {
                char c = json[index++];
                if (c == '"')
                    return builder.ToString();
                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                if (index >= json.Length)
                    throw new FormatException("Invalid JSON escape.");

                char escaped = json[index++];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        builder.Append(escaped);
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        builder.Append(ParseUnicodeEscape());
                        break;
                    default:
                        throw new FormatException("Invalid JSON escape.");
                }
            }

            throw new FormatException("Unterminated JSON string.");
        }

        private char ParseUnicodeEscape()
        {
            if (index + 4 > json.Length)
                throw new FormatException("Invalid unicode escape.");

            string hex = json.Substring(index, 4);
            index += 4;
            return (char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private object ParseNumber()
        {
            int start = index;
            if (index < json.Length && json[index] == '-')
                index++;

            while (index < json.Length && char.IsDigit(json[index]))
                index++;

            if (index < json.Length && json[index] == '.')
            {
                index++;
                while (index < json.Length && char.IsDigit(json[index]))
                    index++;
            }

            if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
            {
                index++;
                if (index < json.Length && (json[index] == '+' || json[index] == '-'))
                    index++;
                while (index < json.Length && char.IsDigit(json[index]))
                    index++;
            }

            string numberText = json.Substring(start, index - start);
            if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                throw new FormatException("Invalid JSON number.");

            return value;
        }

        private object ParseLiteral(string literal, object value)
        {
            if (index + literal.Length > json.Length ||
                !string.Equals(json.Substring(index, literal.Length), literal, StringComparison.Ordinal))
            {
                throw new FormatException("Invalid JSON literal.");
            }

            index += literal.Length;
            return value;
        }

        private void SkipWhitespace()
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
                index++;
        }

        private bool TryConsume(char c)
        {
            if (index >= json.Length || json[index] != c)
                return false;

            index++;
            return true;
        }

        private void Expect(char c)
        {
            if (index >= json.Length || json[index] != c)
                throw new FormatException($"Expected '{c}'.");

            index++;
        }
    }
}
