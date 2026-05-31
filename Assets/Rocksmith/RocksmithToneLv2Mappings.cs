using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

internal enum RocksmithToneLv2SlotRole
{
    Dynamics,
    Gain,
    Amp,
    Cab,
    Eq,
    Modulation,
    Ambience
}

internal readonly struct RocksmithToneLv2SlotMapping
{
    public RocksmithToneLv2SlotMapping(RocksmithToneLv2SlotRole role, UnityToneLabRuntime.ToneLabPedalSlot slot)
    {
        Role = role;
        Slot = slot;
    }

    public RocksmithToneLv2SlotRole Role { get; }
    public UnityToneLabRuntime.ToneLabPedalSlot Slot { get; }
}

internal static class RocksmithToneLv2Mappings
{
    private const string GxPrefix = "http://guitarix.sourceforge.net/plugins/";
    private const string GxMicroAmp = GxPrefix + "gx_MicroAmp_#_MicroAmp_";
    private const string GxSd1 = GxPrefix + "gx_sd1sim_#_sd1sim_";
    private const string GxGuvnor = GxPrefix + "gx_guvnor_#_guvnor_";
    private const string GxTimRay = GxPrefix + "gx_timray_#_timray_";
    private const string GxSunFace = GxPrefix + "gx_SunFace_#_SunFace_";
    private const string GxAxisFace = GxPrefix + "gx_AxisFace_#_AxisFace_";
    private const string GxQuack = GxPrefix + "gx_quack_#_quack_";
    private const string GxSlowGear = GxPrefix + "gx_slowgear_#_slowgear_";
    private const string GxPlexi = GxPrefix + "gx_plexi_#_plexi_";
    private const string GxSuperSonic = GxPrefix + "gx_supersonic_#_supersonic_";
    private const string GxBlueAmp = GxPrefix + "gx_blueamp_#_blueamp_";
    private const string GxVmk2D = GxPrefix + "gx_vmk2d_#_vmk2d_";
    private const string GxAmpegSvt = GxPrefix + "gx_ampegsvt_#_ampegsvt_";
    private const string GxUltraCab = GxPrefix + "gx_ultracab_#_ultracab_";
    private const string ZamGateX2 = "urn:zamaudio:ZamGateX2";
    private const string ZamCompX2 = "urn:zamaudio:ZamCompX2";
    private const string ZamDelay = "urn:zamaudio:ZamDelay";
    private const string ZamEq2 = "urn:zamaudio:ZamEQ2";
    private const string ZamGeq31 = "urn:zamaudio:ZamGEQ31";
    private const string ZamTube = "urn:zamaudio:ZamTube";
    private const string DpfPitchShift = "http://distrho.sf.net/plugins/MaPitchshift";
    private const string DpfThreeBandEq = "http://distrho.sf.net/plugins/3BandEQ";
    private const string DragonflyRoom = "urn:dragonfly:room";
    private const string DragonflyPlate = "urn:dragonfly:plate";
    private const string DragonflyHall = "https://github.com/michaelwillis/dragonfly-reverb";

    private static readonly string[] RequiredPluginUris =
    {
        GxMicroAmp,
        GxSd1,
        GxGuvnor,
        GxTimRay,
        GxSunFace,
        GxAxisFace,
        GxQuack,
        GxSlowGear,
        GxPlexi,
        GxSuperSonic,
        GxBlueAmp,
        GxVmk2D,
        GxAmpegSvt,
        GxUltraCab,
        ZamGateX2,
        ZamCompX2,
        ZamDelay,
        ZamEq2,
        ZamGeq31,
        ZamTube,
        DpfPitchShift,
        DpfThreeBandEq,
        DragonflyRoom,
        DragonflyPlate,
        DragonflyHall
    };

    private static readonly float[] ZamGeq31Frequencies =
    {
        32f, 40f, 50f, 63f, 79f, 100f, 126f, 158f, 200f, 251f,
        316f, 398f, 501f, 631f, 794f, 999f, 1257f, 1584f, 1997f,
        2514f, 3165f, 3986f, 5017f, 6318f, 7963f, 10032f, 12662f,
        16081f, 20801f
    };

    public static bool AreRequiredPluginsAvailable(Func<string, bool> pluginAvailable)
    {
        if (pluginAvailable == null)
            return true;

        for (int i = 0; i < RequiredPluginUris.Length; i++)
        {
            string pluginUri = RequiredPluginUris[i];
            if (string.IsNullOrWhiteSpace(pluginUri))
                continue;

            try
            {
                if (!pluginAvailable(pluginUri))
                    return false;
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryCreateMappedSlot(
        string slotKey,
        string type,
        string name,
        string category,
        IReadOnlyDictionary<string, float> knobs,
        bool isBassRoute,
        bool highGain,
        float driveIntent,
        out RocksmithToneLv2SlotMapping mapping)
    {
        mapping = default;
        string text = NormalizeText($"{slotKey} {type} {name} {category}");
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (ContainsAny(text, "gate", "noise suppress", "noise reduction", "hush"))
            return Return(RocksmithToneLv2SlotRole.Dynamics, CreateNoiseGateSlot(knobs, driveIntent, highGain), out mapping);

        if (ContainsAny(text, "compress", "sustain", "limiter", "mbcomp", "studio compressor"))
            return Return(RocksmithToneLv2SlotRole.Dynamics, CreateCompressorSlot(knobs, driveIntent), out mapping);

        if (ContainsAny(text, "cab", "cabinet", "speaker"))
        {
            UnityToneLabRuntime.ToneLabPedalSlot cabSlot = isBassRoute
                ? null
                : CreateUltraCabSlot(knobs, highGain, driveIntent);
            return cabSlot != null && Return(RocksmithToneLv2SlotRole.Cab, cabSlot, out mapping);
        }

        if (LooksLikeAmpGear(slotKey, type, name, category, text))
            return Return(RocksmithToneLv2SlotRole.Amp, CreateAmpSlot(text, knobs, isBassRoute, highGain, driveIntent), out mapping);

        if (ContainsAny(text, "eq", "equalizer", "filter", "graphic"))
            return Return(RocksmithToneLv2SlotRole.Eq, CreateEqSlot(text, knobs), out mapping);

        if (ContainsAny(text, "delay", "echo"))
            return Return(RocksmithToneLv2SlotRole.Ambience, CreateDelaySlot(knobs), out mapping);

        if (ContainsAny(text, "reverb", "room", "hall", "plate", "spring", "verb"))
            return Return(RocksmithToneLv2SlotRole.Ambience, CreateReverbSlot(text, knobs, highGain), out mapping);

        if (ContainsAny(text, "wah", "envelope", "auto wah", "quack"))
            return Return(RocksmithToneLv2SlotRole.Modulation, CreateQuackSlot(knobs), out mapping);

        if (ContainsAny(text, "slowgear", "slow gear", "swell"))
            return Return(RocksmithToneLv2SlotRole.Modulation, CreateSlowGearSlot(knobs), out mapping);

        if (ContainsAny(text, "octave", "pitch", "whammy", "harmon"))
            return Return(RocksmithToneLv2SlotRole.Gain, CreatePitchShiftSlot(knobs), out mapping);

        if (ContainsAny(text, "drive", "dist", "fuzz", "muff", "screamer", "overdrive", "boost", "clean boost", "microamp", "micro amp", "preamp", "pre amp", "rat", "ds1", "sd1", "tube", "edenwtdi"))
            return Return(RocksmithToneLv2SlotRole.Gain, CreateDriveSlot(text, knobs, isBassRoute), out mapping);

        if (ContainsAny(text, "acoustic emulator"))
            return Return(RocksmithToneLv2SlotRole.Eq, CreateAcousticShapeSlot(knobs), out mapping);

        return false;
    }

    public static UnityToneLabRuntime.ToneLabPedalSlot CreateDefaultNoiseGateSlot(float driveIntent, bool highGain)
    {
        float drive = Mathf.Clamp01(driveIntent);
        float threshold = Mathf.Lerp(-62f, -40f, drive);
        return CreateZamGateX2Slot(
            thresholdDb: threshold,
            closeDb: Mathf.Clamp(threshold - 8f, -80f, 0f),
            attackMs: highGain ? 0.8f : 3f,
            releaseMs: highGain ? 70f : 125f);
    }

    public static UnityToneLabRuntime.ToneLabPedalSlot CreateDefaultAmpSlot(
        bool isBassRoute,
        bool highGain,
        float driveIntent,
        float bassIntent,
        float midIntent,
        float trebleIntent,
        float presenceIntent)
    {
        Dictionary<string, float> syntheticKnobs = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            ["gain"] = Mathf.Lerp(18f, highGain ? 88f : 58f, Smooth01(driveIntent)),
            ["bass"] = Mathf.Clamp01(bassIntent) * 100f,
            ["mid"] = Mathf.Clamp01(midIntent) * 100f,
            ["treble"] = Mathf.Clamp01(trebleIntent) * 100f,
            ["presence"] = Mathf.Clamp01(presenceIntent) * 100f,
            ["volume"] = Mathf.Lerp(42f, 58f, 1f - Mathf.Clamp01(driveIntent))
        };

        string text = isBassRoute ? "bass amp" : highGain ? "high gain amp" : "clean amp";
        return CreateAmpSlot(text, syntheticKnobs, isBassRoute, highGain, driveIntent);
    }

    public static UnityToneLabRuntime.ToneLabPedalSlot CreateDefaultCabSlot(
        bool isBassRoute,
        bool highGain,
        float driveIntent,
        float bassIntent,
        float midIntent,
        float trebleIntent,
        float presenceIntent)
    {
        if (isBassRoute)
            return null;

        Dictionary<string, float> syntheticKnobs = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            ["gain"] = Mathf.Lerp(34f, 72f, Mathf.Clamp01(driveIntent)),
            ["mids"] = Mathf.Clamp01(midIntent) * 100f,
            ["punch"] = Mathf.Clamp01(bassIntent) * 100f,
            ["resonance"] = Mathf.Lerp(42f, 62f, Mathf.Clamp01(bassIntent)),
            ["size"] = highGain ? 58f : 46f,
            ["top"] = Mathf.Clamp01((presenceIntent * 0.7f) + (trebleIntent * 0.3f)) * 100f
        };
        return CreateUltraCabSlot(syntheticKnobs, highGain, driveIntent);
    }

    public static UnityToneLabRuntime.ToneLabPedalSlot CreateDefaultEqSlot(
        bool isBassRoute,
        bool highGain,
        float bassIntent,
        float midIntent,
        float trebleIntent)
    {
        float lowDb = Mathf.Lerp(-4.5f, 4.5f, Mathf.Clamp01(bassIntent));
        float midDb = Mathf.Lerp(-5.5f, 4.0f, Mathf.Clamp01(midIntent));
        float highDb = Mathf.Lerp(-4.0f, 4.5f, Mathf.Clamp01(trebleIntent));
        if (highGain)
        {
            lowDb -= 1.2f;
            midDb -= 1.6f;
            highDb += 0.6f;
        }

        if (isBassRoute)
        {
            return CreateZamEq2Slot(
                lowBoostDb: Mathf.Clamp(lowDb + 1.8f, -12f, 12f),
                lowFreqHz: 85f,
                mid1BoostDb: Mathf.Clamp(midDb, -12f, 12f),
                mid1FreqHz: 520f,
                mid1Bandwidth: 1.4f,
                mid2BoostDb: Mathf.Clamp(midDb * 0.5f, -12f, 12f),
                mid2FreqHz: 1600f,
                mid2Bandwidth: 1.2f,
                highBoostDb: Mathf.Clamp(highDb - 1f, -12f, 12f),
                highFreqHz: 4200f,
                outputGainDb: 0f,
                inputGainDb: 0f);
        }

        return CreateDpfThreeBandEqSlot(
            lowDb: Mathf.Clamp(lowDb, -12f, 12f),
            midDb: Mathf.Clamp(midDb, -12f, 12f),
            highDb: Mathf.Clamp(highDb, -12f, 12f),
            masterDb: highGain ? -1.5f : -0.8f,
            lowMidHz: highGain ? 360f : 430f,
            midHighHz: highGain ? 2800f : 3200f);
    }

    public static UnityToneLabRuntime.ToneLabPedalSlot CreateDefaultReverbSlot(bool highGain)
    {
        return highGain
            ? CreateDragonflyRoomSlot(99f, 0.5f, 1.2f, 8f, 0.12f, 35f, 5200f, 120f)
            : CreateDragonflyRoomSlot(96f, 1.5f, 3.5f, 10f, 0.22f, 62f, 8200f, 90f);
    }

    private static bool Return(RocksmithToneLv2SlotRole role, UnityToneLabRuntime.ToneLabPedalSlot slot, out RocksmithToneLv2SlotMapping mapping)
    {
        mapping = slot == null ? default : new RocksmithToneLv2SlotMapping(role, slot);
        return slot != null;
    }

    private static bool LooksLikeAmpGear(string slotKey, string type, string name, string category, string normalizedText)
    {
        string slot = NormalizeText(slotKey);
        string gearType = NormalizeText(type);
        string gearName = NormalizeText(name);
        string gearCategory = NormalizeText(category);

        if (HasToken(gearType, "amp") || HasToken(gearType, "amps"))
            return true;

        if (StartsWithAmpToken(slot) || StartsWithAmpToken(gearName))
            return true;

        bool pedalOrRackSlot = ContainsAny($"{slot} {gearType} {gearCategory}", "pedal", "pedals", "stomp", "rack", "pre", "post");
        if (pedalOrRackSlot)
            return false;

        return HasToken(gearCategory, "amp") ||
               HasToken(gearCategory, "amps") ||
               HasToken(normalizedText, "amp") ||
               HasToken(normalizedText, "amps");
    }

    private static bool StartsWithAmpToken(string normalizedText)
    {
        return string.Equals(normalizedText, "amp", StringComparison.Ordinal) ||
               string.Equals(normalizedText, "amps", StringComparison.Ordinal) ||
               normalizedText.StartsWith("amp ", StringComparison.Ordinal) ||
               normalizedText.StartsWith("amps ", StringComparison.Ordinal) ||
               normalizedText.StartsWith("bass amp ", StringComparison.Ordinal) ||
               normalizedText.StartsWith("bass amps ", StringComparison.Ordinal);
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateNoiseGateSlot(IReadOnlyDictionary<string, float> knobs, float driveIntent, bool highGain)
    {
        float threshold = GetDbControl(knobs, Mathf.Lerp(-58f, -42f, Mathf.Clamp01(driveIntent)), -80f, -20f, "threshold", "thresh", "gate");
        return CreateZamGateX2Slot(
            thresholdDb: threshold,
            closeDb: Mathf.Clamp(threshold - 8f, -80f, 0f),
            attackMs: GetMilliseconds(knobs, highGain ? 0.8f : 3f, 0.1f, 120f, "attack"),
            releaseMs: GetMilliseconds(knobs, highGain ? 70f : 125f, 20f, 600f, "release", "decay"));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateCompressorSlot(IReadOnlyDictionary<string, float> knobs, float driveIntent)
    {
        float compression = GetNormalizedControl(knobs, Mathf.Lerp(0.34f, 0.58f, Mathf.Clamp01(driveIntent)), "compress", "comp", "sustain", "amount");
        return CreateZamCompX2Slot(
            thresholdDb: GetDbControl(knobs, Mathf.Lerp(-30f, -20f, compression), -60f, 0f, "threshold", "thresh"),
            ratio: GetRatio(knobs, Mathf.Lerp(2.0f, 5.5f, compression), "ratio"),
            makeupDb: Mathf.Lerp(1.5f, 5.0f, compression),
            attackMs: GetMilliseconds(knobs, 12f, 1f, 120f, "attack"),
            releaseMs: GetMilliseconds(knobs, 150f, 20f, 600f, "release"),
            kneeDb: Mathf.Lerp(1.5f, 4.0f, compression));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateDriveSlot(string text, IReadOnlyDictionary<string, float> knobs, bool isBassRoute)
    {
        float drive = GetNormalizedControl(knobs, isBassRoute ? 0.26f : 0.42f, "drive", "gain", "dist", "distortion", "pregain");
        float level = GetNormalizedControl(knobs, 0.56f, "level", "volume", "output", "master");
        float tone = GetNormalizedControl(knobs, 0.55f, "tone", "filter", "color", "treble");
        float bass = GetToneStackControl(knobs, 0.5f, "bass", "low");
        float mid = GetToneStackControl(knobs, 0.52f, "mid", "middle", "mids");
        float treble = GetToneStackControl(knobs, tone, "treble", "high");

        if (ContainsAny(text, "screamer", "tube screamer", "sd1", "ts9", "overdrive"))
        {
            return CreateLv2Slot(GxSd1, "GxSD1",
                ("DRIVE", Mathf.Clamp01(drive)),
                ("LEVEL", Mathf.Lerp(-16f, 2f, Mathf.Clamp01(level))),
                ("TONE", Mathf.Lerp(180f, 880f, Mathf.Clamp01(tone))));
        }

        if (ContainsAny(text, "boost", "microamp", "micro amp", "clean boost"))
            return CreateLv2Slot(GxMicroAmp, "GxMicroAmp", ("GAIN", Mathf.Clamp01(Mathf.Max(level, drive))));

        if (ContainsAny(text, "fuzzwashe", "fuzz", "muff", "face"))
        {
            string uri = ContainsAny(text, "axis", "octave") ? GxAxisFace : GxSunFace;
            string displayName = uri == GxAxisFace ? "GxAxisFace" : "GxSunFace";
            return uri == GxAxisFace
                ? CreateLv2Slot(uri, displayName,
                    ("ATTACK", Mathf.Clamp01(drive)),
                    ("SMOOTH", Mathf.Clamp01(1f - (tone * 0.35f))),
                    ("VOLUME", Mathf.Clamp01(level)))
                : CreateLv2Slot(uri, displayName,
                    ("DRIVE", Mathf.Clamp01(drive)),
                    ("INPUT", Mathf.Clamp01(Mathf.Lerp(0.45f, 0.80f, drive))),
                    ("VOLUME", Mathf.Clamp01(level)));
        }

        if (ContainsAny(text, "tube", "edenwtdi") || isBassRoute)
        {
            return CreateLv2Slot(ZamTube, "ZamTube",
                ("tubedrive", Mathf.Lerp(0.6f, 3.6f, Mathf.Clamp01(drive))),
                ("bass", Mathf.Lerp(2.0f, 8.0f, Mathf.Clamp01(bass))),
                ("mids", Mathf.Lerp(2.0f, 8.0f, Mathf.Clamp01(mid))),
                ("treb", Mathf.Lerp(2.0f, 8.0f, Mathf.Clamp01(treble))),
                ("tonestack", 0f),
                ("gain", Mathf.Lerp(-5f, 1f, Mathf.Clamp01(level))),
                ("insane", 0f));
        }

        if (ContainsAny(text, "tim", "transparent", "blues"))
        {
            return CreateLv2Slot(GxTimRay, "GxTimRay",
                ("BASS", Mathf.Clamp01(bass)),
                ("GAIN", Mathf.Clamp01(drive)),
                ("TREBLE", Mathf.Clamp01(treble)),
                ("TRIM", Mathf.Clamp01(Mathf.Lerp(0.38f, 0.62f, level))),
                ("VOLUME", Mathf.Clamp01(level)));
        }

        return CreateLv2Slot(GxGuvnor, "GxGuvnor",
            ("BASS", Mathf.Clamp01(bass)),
            ("GAIN", Mathf.Clamp01(drive)),
            ("LEVEL", Mathf.Clamp01(level)),
            ("MID", Mathf.Clamp01(mid)),
            ("TREBLE", Mathf.Clamp01(treble)));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateAmpSlot(string text, IReadOnlyDictionary<string, float> knobs, bool isBassRoute, bool highGain, float driveIntent)
    {
        float gain = Mathf.Max(GetNormalizedControl(knobs, driveIntent, "gain", "drive", "pregain", "pre gain", "pre"), Mathf.Clamp01(driveIntent));
        float bass = GetToneStackControl(knobs, isBassRoute ? 0.68f : 0.50f, "bass", "low");
        float mid = GetToneStackControl(knobs, highGain ? 0.42f : 0.54f, "mid", "middle", "mids");
        float treble = GetToneStackControl(knobs, highGain ? 0.56f : 0.52f, "treble", "high");
        float presence = GetToneStackControl(knobs, highGain ? 0.65f : 0.48f, "presence", "pres", "bright");
        float volume = GetNormalizedControl(knobs, Mathf.Lerp(0.58f, 0.38f, gain), "volume", "master", "level");

        if (isBassRoute || ContainsAny(text, "bass amp", "bt975", "eden", "ampeg"))
        {
            return CreateLv2Slot(GxAmpegSvt, "GxAmpegSVT",
                ("BASS", Mathf.Clamp01(bass)),
                ("MIDDLE", Mathf.Clamp01(mid)),
                ("TREBLE", Mathf.Clamp01(treble)),
                ("VOLUME", Mathf.Clamp01(Mathf.Lerp(0.24f, 0.78f, volume))),
                ("LOWSWITCH", bass >= 0.62f ? 2f : 1f),
                ("MIDSWITCH", mid >= 0.58f ? 2f : 1f),
                ("HIGHSWITCH", treble >= 0.56f ? 1f : 0f),
                ("CABSWITCH", 1f));
        }

        if (highGain || gain >= 0.74f || ContainsAny(text, "dsl", "mesa", "rect", "5150", "engl", "orange", "ad50", "metal"))
        {
            return CreateLv2Slot(GxSuperSonic, "GxSuperSonic",
                ("GAIN", Mathf.Clamp01(gain)),
                ("BASS", Mathf.Clamp01(bass)),
                ("TREBLE", Mathf.Clamp01((treble * 0.72f) + (presence * 0.28f))),
                ("VOLUME", Mathf.Clamp01(Mathf.Lerp(0.25f, 0.58f, volume))));
        }

        if (ContainsAny(text, "plexi", "marshall", "gb", "brit", "jcm", "tw40"))
        {
            return CreateLv2Slot(GxPlexi, "GxPlexi",
                ("BASS", Mathf.Clamp01(bass)),
                ("MASTER", Mathf.Clamp01(Mathf.Lerp(0.28f, 0.72f, gain))),
                ("MID", Mathf.Clamp01(mid)),
                ("PRESENSE", Mathf.Clamp01(presence)),
                ("TREBLE", Mathf.Clamp01(treble)),
                ("VOLUME", Mathf.Clamp01(Mathf.Lerp(0.18f, 0.56f, volume))));
        }

        if (ContainsAny(text, "tweed", "tw22", "twin", "vibe"))
        {
            return CreateLv2Slot(GxVmk2D, "GxVMK2D",
                ("BASS", Mathf.Clamp01(bass)),
                ("DEPTH", 0.5f),
                ("MRBSELECT", 0f),
                ("MRB", 0f),
                ("REVERBLEVEL", 0.08f),
                ("REVERB", 0f),
                ("SPEED", 0.4f),
                ("TREBLE", Mathf.Clamp01(treble)),
                ("VIBE", 0f),
                ("VOLUME", Mathf.Clamp01(Mathf.Lerp(0.28f, 0.62f, volume))));
        }

        return CreateLv2Slot(GxBlueAmp, "GxBlueAmp",
            ("MASTER", Mathf.Clamp01(Mathf.Lerp(0.30f, 0.70f, gain))),
            ("TONE", Mathf.Clamp01((treble * 0.58f) + (presence * 0.28f) + 0.07f)),
            ("VOLUME", Mathf.Clamp01(Mathf.Lerp(0.32f, 0.66f, volume))));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateUltraCabSlot(IReadOnlyDictionary<string, float> knobs, bool highGain, float driveIntent)
    {
        float mids = GetToneStackControl(knobs, highGain ? 0.48f : 0.54f, "mids", "mid", "middle");
        float punch = GetToneStackControl(knobs, highGain ? 0.58f : 0.46f, "punch", "bass", "low", "thump");
        float top = GetToneStackControl(knobs, highGain ? 0.36f : 0.50f, "top", "treble", "presence", "air");
        return CreateLv2Slot(GxUltraCab, "GxUltraCab",
            ("GAIN", GetNormalizedControl(knobs, Mathf.Lerp(0.34f, 0.62f, driveIntent), "gain", "level", "volume")),
            ("MIDS", Mathf.Clamp01(mids)),
            ("PUNCH", Mathf.Clamp01(punch)),
            ("RESONANCE", GetNormalizedControl(knobs, highGain ? 0.44f : 0.52f, "resonance", "res")),
            ("SIZE", GetNormalizedControl(knobs, highGain ? 0.52f : 0.46f, "size")),
            ("TOP", Mathf.Clamp01(top)));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateEqSlot(string text, IReadOnlyDictionary<string, float> knobs)
    {
        if (ContainsAny(text, "graphic") &&
            TryCreateGraphicEqSlot(knobs, out UnityToneLabRuntime.ToneLabPedalSlot graphicEq))
        {
            return graphicEq;
        }

        float lowDb = GetEqDb(knobs, 0f, "bass", "low");
        float lowFreq = GetFrequency(knobs, 160f, "bassfreq", "lowfreq", "bass freq", "low freq");
        float mid1Db = GetEqDb(knobs, 0f, "lomid", "lowmid", "mid");
        float mid1Freq = GetFrequency(knobs, 750f, "lomidfreq", "lowmidfreq", "midfreq");
        float mid2Db = GetEqDb(knobs, 0f, "himid", "highmid");
        float mid2Freq = GetFrequency(knobs, 2200f, "himidfreq", "highmidfreq");
        float highDb = GetEqDb(knobs, 0f, "treble", "high");
        float highFreq = GetFrequency(knobs, 5600f, "treblefreq", "highfreq", "treb");

        if (ContainsAny(text, "graphic"))
        {
            return CreateDpfThreeBandEqSlot(
                lowDb: Mathf.Clamp(lowDb, -12f, 12f),
                midDb: Mathf.Clamp((mid1Db + mid2Db) * 0.5f, -12f, 12f),
                highDb: Mathf.Clamp(highDb, -12f, 12f),
                masterDb: 0f,
                lowMidHz: Mathf.Clamp(mid1Freq, 120f, 1200f),
                midHighHz: Mathf.Clamp(mid2Freq, 1400f, 6400f));
        }

        return CreateZamEq2Slot(
            lowBoostDb: lowDb,
            lowFreqHz: lowFreq,
            mid1BoostDb: mid1Db,
            mid1FreqHz: mid1Freq,
            mid1Bandwidth: 1.2f,
            mid2BoostDb: mid2Db,
            mid2FreqHz: mid2Freq,
            mid2Bandwidth: 1.2f,
            highBoostDb: highDb,
            highFreqHz: highFreq,
            outputGainDb: 0f,
            inputGainDb: 0f);
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateAcousticShapeSlot(IReadOnlyDictionary<string, float> knobs)
    {
        float body = GetNormalizedControl(knobs, 0.48f, "body");
        float mid = GetToneStackControl(knobs, 0.56f, "mid");
        float tone = GetToneStackControl(knobs, 0.65f, "tone", "treble");
        return CreateZamEq2Slot(
            lowBoostDb: Mathf.Lerp(-1.5f, 3.5f, body),
            lowFreqHz: 180f,
            mid1BoostDb: Mathf.Lerp(-3.5f, 2.5f, mid),
            mid1FreqHz: GetFrequency(knobs, 775f, "midshift", "mid freq"),
            mid1Bandwidth: 1.0f,
            mid2BoostDb: Mathf.Lerp(-2.0f, 1.0f, body),
            mid2FreqHz: 2400f,
            mid2Bandwidth: 1.4f,
            highBoostDb: Mathf.Lerp(-2.0f, 3.0f, tone),
            highFreqHz: 6500f,
            outputGainDb: -1f,
            inputGainDb: 0f);
    }

    private static bool TryCreateGraphicEqSlot(IReadOnlyDictionary<string, float> knobs, out UnityToneLabRuntime.ToneLabPedalSlot slot)
    {
        slot = null;
        if (knobs == null || knobs.Count == 0)
            return false;

        Dictionary<int, float> bands = new Dictionary<int, float>();
        foreach (KeyValuePair<string, float> pair in knobs)
        {
            if (!TryExtractFrequencyFromKnobName(pair.Key, out float frequencyHz))
                continue;

            int nearestBand = FindNearestZamGeqBand(frequencyHz);
            bands[nearestBand] = Mathf.Clamp(ConvertEqValueToDb(pair.Value), -12f, 12f);
        }

        if (bands.Count < 3)
            return false;

        List<(string id, float value)> parameters = new List<(string id, float value)> { ("master", 0f) };
        foreach (KeyValuePair<int, float> band in bands)
            parameters.Add(($"band{band.Key + 1}", band.Value));

        slot = CreateLv2Slot(ZamGeq31, "ZamGEQ31", parameters);
        return true;
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateDelaySlot(IReadOnlyDictionary<string, float> knobs)
    {
        float timeMs = GetDelayMilliseconds(knobs, 280f, "time", "delay");
        float feedback = GetNormalizedControl(knobs, 0.30f, "feedback", "regen", "repeat");
        float mix = GetNormalizedControl(knobs, 0.20f, "mix", "wet", "drywet", "blend");
        float tone = GetToneStackControl(knobs, 0.65f, "tone", "filter", "treble");
        return CreateZamDelaySlot(
            timeMs: timeMs,
            feedback: Mathf.Clamp(feedback, 0f, 0.92f),
            dryWet: Mathf.Clamp(mix, 0.02f, 0.62f),
            lowPassHz: Mathf.Lerp(2800f, 9000f, Mathf.Clamp01(tone)),
            outputGainDb: Mathf.Lerp(-8f, -3f, Mathf.Clamp01(mix)));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateReverbSlot(string text, IReadOnlyDictionary<string, float> knobs, bool highGain)
    {
        float mix = GetNormalizedControl(knobs, highGain ? 0.12f : 0.24f, "mix", "wet", "level");
        float depth = GetNormalizedControl(knobs, 0.36f, "depth", "size", "room");
        float decay = Mathf.Lerp(0.18f, highGain ? 0.65f : 2.4f, GetNormalizedControl(knobs, depth, "time", "decay"));
        float tone = GetToneStackControl(knobs, highGain ? 0.42f : 0.62f, "tone", "treble", "damp");
        float dry = Mathf.Lerp(96f, 82f, Mathf.Clamp01(mix));
        float wet = Mathf.Lerp(4f, 36f, Mathf.Clamp01(mix));

        if (ContainsAny(text, "plate", "spring"))
            return CreateDragonflyPlateSlot(dry, wet, ContainsAny(text, "spring") ? 2f : 1f, decay, Mathf.Lerp(8f, 40f, depth), Mathf.Lerp(4200f, 12000f, tone), 100f);

        if (ContainsAny(text, "hall", "shimmer", "ambient"))
            return CreateDragonflyHallSlot(dry, wet * 0.45f, wet, Mathf.Lerp(16f, 44f, depth), decay, Mathf.Lerp(8f, 32f, depth), Mathf.Lerp(4200f, 12000f, tone), 100f);

        return CreateDragonflyRoomSlot(dry, wet * 0.45f, wet, Mathf.Lerp(8f, 24f, depth), decay, Mathf.Lerp(45f, 95f, depth), Mathf.Lerp(4200f, 12000f, tone), 90f);
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateQuackSlot(IReadOnlyDictionary<string, float> knobs)
    {
        return CreateLv2Slot(GxQuack, "GxQuack",
            ("DEPTH", GetNormalizedControl(knobs, 0.65f, "depth", "range", "sensitivity")),
            ("DRIVE", GetNormalizedControl(knobs, 0.08f, "drive", "gain")),
            ("GAIN", Mathf.Lerp(-8f, 2f, GetNormalizedControl(knobs, 0.45f, "level", "volume"))),
            ("MODE", 2f),
            ("PEAK", Mathf.Lerp(2f, 12f, GetNormalizedControl(knobs, 0.55f, "peak", "q", "resonance"))),
            ("RANGE", GetNormalizedControl(knobs, 0.72f, "range")),
            ("TONE", Mathf.Lerp(0.4f, 1.6f, GetToneStackControl(knobs, 0.68f, "tone", "treble"))));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateSlowGearSlot(IReadOnlyDictionary<string, float> knobs)
    {
        return CreateLv2Slot(GxSlowGear, "GxSlowGear",
            ("DOWNTIME", GetMilliseconds(knobs, 60f, 0f, 1000f, "downtime", "release")),
            ("TRESHOLD", Mathf.Lerp(0.4f, 5.5f, GetNormalizedControl(knobs, 0.35f, "threshold", "sensitivity"))),
            ("UPTIME", GetMilliseconds(knobs, 140f, 0f, 1000f, "uptime", "attack", "swell")));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreatePitchShiftSlot(IReadOnlyDictionary<string, float> knobs)
    {
        float ratio = ContainsKnobName(knobs, "down") ? 0.5f : 2.0f;
        if (TryGetAnyKnob(knobs, out float rawRatio, "ratio", "shift", "pitch"))
        {
            if (Mathf.Abs(rawRatio) <= 1.0001f)
                ratio = Mathf.Lerp(0.5f, 2.0f, Mathf.Clamp01(rawRatio));
            else if (rawRatio > 0f && rawRatio <= 4f)
                ratio = rawRatio;
        }

        return CreateLv2Slot(DpfPitchShift, "MaPitchshift",
            ("blur", 0.02f),
            ("window", 80f),
            ("ratio", Mathf.Clamp(ratio, 0.25f, 4f)),
            ("xfade", 0.42f));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateZamGateX2Slot(float thresholdDb, float closeDb, float attackMs, float releaseMs)
    {
        return CreateLv2Slot(ZamGateX2, "ZamGateX2",
            ("att", Mathf.Clamp(attackMs, 0.1f, 500f)),
            ("rel", Mathf.Clamp(releaseMs, 0.1f, 500f)),
            ("thr", Mathf.Clamp(thresholdDb, -60f, 0f)),
            ("mak", 0f),
            ("sidechain", 0f),
            ("close", Mathf.Clamp(closeDb, -50f, 0f)),
            ("mode", 0f));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateZamCompX2Slot(float thresholdDb, float ratio, float makeupDb, float attackMs, float releaseMs, float kneeDb)
    {
        return CreateLv2Slot(ZamCompX2, "ZamCompX2",
            ("att", Mathf.Clamp(attackMs, 0.1f, 100f)),
            ("rel", Mathf.Clamp(releaseMs, 1f, 500f)),
            ("kn", Mathf.Clamp(kneeDb, 0f, 8f)),
            ("rat", Mathf.Clamp(ratio, 1f, 20f)),
            ("thr", Mathf.Clamp(thresholdDb, -80f, 0f)),
            ("mak", Mathf.Clamp(makeupDb, 0f, 30f)),
            ("slew", 1f),
            ("stereodet", 1f),
            ("sidechain", 0f));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateZamDelaySlot(float timeMs, float feedback, float dryWet, float lowPassHz, float outputGainDb)
    {
        return CreateLv2Slot(ZamDelay, "ZamDelay",
            ("inv", 0f),
            ("time", Mathf.Clamp(timeMs, 1f, 8000f)),
            ("sync", 0f),
            ("lpf", Mathf.Clamp(lowPassHz, 20f, 20000f)),
            ("div", 3f),
            ("gain", Mathf.Clamp(outputGainDb, -60f, 0f)),
            ("drywet", Mathf.Clamp01(dryWet)),
            ("feedb", Mathf.Clamp(feedback, 0f, 0.99f)));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateZamEq2Slot(
        float lowBoostDb,
        float lowFreqHz,
        float mid1BoostDb,
        float mid1FreqHz,
        float mid1Bandwidth,
        float mid2BoostDb,
        float mid2FreqHz,
        float mid2Bandwidth,
        float highBoostDb,
        float highFreqHz,
        float outputGainDb,
        float inputGainDb)
    {
        return CreateLv2Slot(ZamEq2, "ZamEQ2",
            ("boost1", Mathf.Clamp(mid1BoostDb, -20f, 20f)),
            ("bw1", Mathf.Clamp(mid1Bandwidth, 0.7f, 2.5f)),
            ("f1", Mathf.Clamp(mid1FreqHz, 200f, 2500f)),
            ("boost2", Mathf.Clamp(mid2BoostDb, -20f, 20f)),
            ("bw2", Mathf.Clamp(mid2Bandwidth, 0.7f, 2.5f)),
            ("f2", Mathf.Clamp(mid2FreqHz, 600f, 7000f)),
            ("boostl", Mathf.Clamp(lowBoostDb, -20f, 20f)),
            ("fl", Mathf.Clamp(lowFreqHz, 40f, 600f)),
            ("boosth", Mathf.Clamp(highBoostDb, -20f, 20f)),
            ("fh", Mathf.Clamp(highFreqHz, 1500f, 22000f)),
            ("outputgain", Mathf.Clamp(outputGainDb, -10f, 10f)),
            ("inputgain", Mathf.Clamp(inputGainDb, -10f, 10f)));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateDpfThreeBandEqSlot(float lowDb, float midDb, float highDb, float masterDb, float lowMidHz, float midHighHz)
    {
        return CreateLv2Slot(DpfThreeBandEq, "3 Band EQ",
            ("low", Mathf.Clamp(lowDb, -24f, 24f)),
            ("mid", Mathf.Clamp(midDb, -24f, 24f)),
            ("high", Mathf.Clamp(highDb, -24f, 24f)),
            ("master", Mathf.Clamp(masterDb, -24f, 24f)),
            ("low_mid", Mathf.Clamp(lowMidHz, 0f, 1000f)),
            ("mid_high", Mathf.Clamp(midHighHz, 1000f, 20000f)));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateDragonflyRoomSlot(float dryLevel, float earlyLevel, float lateLevel, float size, float decay, float diffuse, float highCutHz, float lowCutHz)
    {
        return CreateLv2Slot(DragonflyRoom, "Dragonfly Room",
            ("dry_level", Mathf.Clamp(dryLevel, 0f, 100f)),
            ("early_level", Mathf.Clamp(earlyLevel, 0f, 100f)),
            ("early_send", Mathf.Clamp(earlyLevel, 0f, 100f)),
            ("late_level", Mathf.Clamp(lateLevel, 0f, 100f)),
            ("size", Mathf.Clamp(size, 8f, 32f)),
            ("width", 100f),
            ("predelay", 6f),
            ("decay", Mathf.Clamp(decay, 0.1f, 10f)),
            ("diffuse", Mathf.Clamp(diffuse, 0f, 100f)),
            ("spin", 0.6f),
            ("wander", 20f),
            ("in_high_cut", Mathf.Clamp(highCutHz, 1000f, 16000f)),
            ("early_damp", Mathf.Clamp(highCutHz, 1000f, 16000f)),
            ("late_damp", Mathf.Clamp(highCutHz, 1000f, 16000f)),
            ("low_boost", 40f),
            ("boost_freq", 520f),
            ("in_low_cut", Mathf.Clamp(lowCutHz, 0f, 200f)));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateDragonflyPlateSlot(float dryLevel, float wetLevel, float algorithm, float decay, float predelayMs, float highCutHz, float width)
    {
        return CreateLv2Slot(DragonflyPlate, "Dragonfly Plate",
            ("dry_level", Mathf.Clamp(dryLevel, 0f, 100f)),
            ("early_level", Mathf.Clamp(wetLevel, 0f, 100f)),
            ("algorithm", Mathf.Clamp(algorithm, 0f, 2f)),
            ("width", Mathf.Clamp(width, 50f, 150f)),
            ("predelay", Mathf.Clamp(predelayMs, 0f, 100f)),
            ("decay", Mathf.Clamp(decay, 0.1f, 10f)),
            ("low_cut", 120f),
            ("high_cut", Mathf.Clamp(highCutHz, 1000f, 16000f)),
            ("early_damp", Mathf.Clamp(highCutHz, 1000f, 16000f)));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateDragonflyHallSlot(float dryLevel, float earlyLevel, float lateLevel, float size, float decay, float predelayMs, float highCutHz, float lowCutHz)
    {
        return CreateLv2Slot(DragonflyHall, "Dragonfly Hall",
            ("dry_level", Mathf.Clamp(dryLevel, 0f, 100f)),
            ("early_level", Mathf.Clamp(earlyLevel, 0f, 100f)),
            ("late_level", Mathf.Clamp(lateLevel, 0f, 100f)),
            ("size", Mathf.Clamp(size, 10f, 60f)),
            ("width", 100f),
            ("delay", Mathf.Clamp(predelayMs, 0f, 100f)),
            ("diffuse", 90f),
            ("low_cut", Mathf.Clamp(lowCutHz, 0f, 200f)),
            ("low_xo", 500f),
            ("low_mult", 1.1f),
            ("high_cut", Mathf.Clamp(highCutHz, 1000f, 16000f)),
            ("high_xo", 5200f),
            ("high_mult", 0.55f),
            ("spin", 2.6f),
            ("wander", 12f),
            ("decay", Mathf.Clamp(decay, 0.1f, 10f)),
            ("early_send", Mathf.Clamp(earlyLevel, 0f, 100f)),
            ("modulation", 14f));
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateLv2Slot(string pluginUri, string displayName, params (string id, float value)[] parameters)
    {
        return CreateLv2Slot(pluginUri, displayName, (IReadOnlyList<(string id, float value)>)parameters);
    }

    private static UnityToneLabRuntime.ToneLabPedalSlot CreateLv2Slot(string pluginUri, string displayName, IReadOnlyList<(string id, float value)> parameters)
    {
        string descriptorId = ToneLabExternalPedalCatalog.BuildLv2DescriptorId(pluginUri);
        ToneLabExternalPedalSettings settings = new ToneLabExternalPedalSettings
        {
            descriptor_id = descriptorId,
            processor_kind = "lv2",
            plugin_uri = pluginUri ?? string.Empty,
            display_name = displayName ?? "LV2 Effect",
            parameters = new List<ToneLabExternalParameterValue>()
        };

        if (parameters != null)
        {
            for (int i = 0; i < parameters.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(parameters[i].id) || !float.IsFinite(parameters[i].value))
                    continue;

                settings.parameters.Add(new ToneLabExternalParameterValue
                {
                    parameter_id = parameters[i].id,
                    value = parameters[i].value
                });
            }
        }

        return new UnityToneLabRuntime.ToneLabPedalSlot
        {
            pedal_instance_id = Guid.NewGuid().ToString("N"),
            pedal_type = UnityToneLabRuntime.ToneLabPedalType.Lv2Plugin,
            descriptor_id = descriptorId,
            enabled = true,
            settings_json = JsonUtility.ToJson(settings)
        };
    }

    private static float GetNormalizedControl(IReadOnlyDictionary<string, float> knobs, float fallback, params string[] aliases)
    {
        return TryGetAnyKnob(knobs, out float value, aliases) ? NormalizeGenericKnob(value) : Mathf.Clamp01(fallback);
    }

    private static float GetToneStackControl(IReadOnlyDictionary<string, float> knobs, float fallback, params string[] aliases)
    {
        if (!TryGetAnyKnob(knobs, out float value, aliases))
            return Mathf.Clamp01(fallback);

        if (HasSignedToneStack(knobs) && Mathf.Abs(value) <= 12.0001f)
            return Mathf.InverseLerp(-10f, 10f, Mathf.Clamp(value, -10f, 10f));

        return NormalizeGenericKnob(value);
    }

    private static float GetEqDb(IReadOnlyDictionary<string, float> knobs, float fallbackDb, params string[] aliases)
    {
        return TryGetAnyKnob(knobs, out float value, aliases)
            ? Mathf.Clamp(ConvertEqValueToDb(value), -12f, 12f)
            : fallbackDb;
    }

    private static float GetDbControl(IReadOnlyDictionary<string, float> knobs, float fallbackDb, float minDb, float maxDb, params string[] aliases)
    {
        if (!TryGetAnyKnob(knobs, out float value, aliases))
            return Mathf.Clamp(fallbackDb, minDb, maxDb);

        if (value >= minDb && value <= maxDb)
            return Mathf.Clamp(value, minDb, maxDb);

        return Mathf.Lerp(minDb, maxDb, NormalizeGenericKnob(value));
    }

    private static float GetRatio(IReadOnlyDictionary<string, float> knobs, float fallback, params string[] aliases)
    {
        if (!TryGetAnyKnob(knobs, out float value, aliases))
            return fallback;

        if (value >= 1f && value <= 20f)
            return value;

        return Mathf.Lerp(1f, 8f, NormalizeGenericKnob(value));
    }

    private static float GetMilliseconds(IReadOnlyDictionary<string, float> knobs, float fallback, float minMs, float maxMs, params string[] aliases)
    {
        if (!TryGetAnyKnob(knobs, out float value, aliases))
            return fallback;

        if (value >= minMs && value <= maxMs)
            return value;

        return Mathf.Lerp(minMs, maxMs, NormalizeGenericKnob(value));
    }

    private static float GetDelayMilliseconds(IReadOnlyDictionary<string, float> knobs, float fallbackMs, params string[] aliases)
    {
        if (!TryGetAnyKnob(knobs, out float value, aliases))
            return fallbackMs;

        if (value > 20f)
            return Mathf.Clamp(value, 20f, 2000f);

        return Mathf.Lerp(60f, 1100f, NormalizeGenericKnob(value));
    }

    private static float GetFrequency(IReadOnlyDictionary<string, float> knobs, float fallbackHz, params string[] aliases)
    {
        if (!TryGetAnyKnob(knobs, out float value, aliases))
            return fallbackHz;

        if (value >= 20f)
            return Mathf.Clamp(value, 20f, 20000f);

        // Several Rocksmith rack EQ fields store kHz-like frequency values.
        if (value > 0f && value <= 20f)
            return Mathf.Clamp(value * 1000f, 20f, 20000f);

        return fallbackHz;
    }

    private static bool TryGetAnyKnob(IReadOnlyDictionary<string, float> knobs, out float value, params string[] aliases)
    {
        value = 0f;
        if (knobs == null || aliases == null)
            return false;

        int bestScore = 0;
        int bestAliasIndex = int.MaxValue;
        foreach (KeyValuePair<string, float> pair in knobs)
        {
            if (!float.IsFinite(pair.Value))
                continue;

            string key = NormalizeText(pair.Key);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            string[] keyTokens = key.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string keyCompact = CompactNormalizedText(key);
            for (int i = 0; i < aliases.Length; i++)
            {
                int score = ScoreKnobAlias(key, keyTokens, keyCompact, aliases[i]);
                if (score <= 0)
                    continue;

                if (score > bestScore || (score == bestScore && i < bestAliasIndex))
                {
                    bestScore = score;
                    bestAliasIndex = i;
                    value = pair.Value;
                }
            }
        }

        return bestScore > 0;
    }

    private static int ScoreKnobAlias(string normalizedKey, string[] keyTokens, string keyCompact, string rawAlias)
    {
        string alias = NormalizeText(rawAlias);
        if (string.IsNullOrWhiteSpace(normalizedKey) || string.IsNullOrWhiteSpace(alias))
            return 0;

        string aliasCompact = CompactNormalizedText(alias);
        if (string.IsNullOrWhiteSpace(aliasCompact))
            return 0;

        if (string.Equals(normalizedKey, alias, StringComparison.Ordinal))
            return 1000;

        if (string.Equals(keyCompact, aliasCompact, StringComparison.Ordinal))
            return 980;

        string lastToken = keyTokens != null && keyTokens.Length > 0
            ? keyTokens[keyTokens.Length - 1]
            : normalizedKey;
        if (string.Equals(lastToken, alias, StringComparison.Ordinal) ||
            string.Equals(lastToken, aliasCompact, StringComparison.Ordinal))
        {
            return 960;
        }

        if (normalizedKey.EndsWith(" " + alias, StringComparison.Ordinal))
            return 930;

        if (keyTokens != null)
        {
            for (int i = 0; i < keyTokens.Length; i++)
            {
                if (string.Equals(keyTokens[i], alias, StringComparison.Ordinal) ||
                    string.Equals(keyTokens[i], aliasCompact, StringComparison.Ordinal))
                {
                    return 900;
                }
            }
        }

        if ((keyCompact.EndsWith("freq", StringComparison.Ordinal) ||
             keyCompact.EndsWith("frequency", StringComparison.Ordinal)) &&
            !aliasCompact.EndsWith("freq", StringComparison.Ordinal) &&
            !aliasCompact.EndsWith("frequency", StringComparison.Ordinal) &&
            !aliasCompact.EndsWith("hz", StringComparison.Ordinal))
        {
            return 0;
        }

        // Compound aliases such as "bassfreq" or "pre gain" intentionally match
        // compact Rocksmith keys like "Rack_StudioEQ_BassFreq" and "Amp_PreGain".
        // Short generic aliases do not use substring matching because they can
        // incorrectly bind "bass" to "BassFreq" or "mid" to "HiMidFreq".
        if (aliasCompact.Length >= 5 && keyCompact.EndsWith(aliasCompact, StringComparison.Ordinal))
            return 820;

        if (aliasCompact.Length >= 6 && keyCompact.Contains(aliasCompact, StringComparison.Ordinal))
            return 640;

        return 0;
    }

    private static string CompactNormalizedText(string normalizedText)
    {
        if (string.IsNullOrWhiteSpace(normalizedText))
            return string.Empty;

        return normalizedText.Replace(" ", string.Empty);
    }

    private static bool ContainsKnobName(IReadOnlyDictionary<string, float> knobs, string needle)
    {
        string normalizedNeedle = NormalizeText(needle);
        if (knobs == null || string.IsNullOrWhiteSpace(normalizedNeedle))
            return false;

        foreach (string key in knobs.Keys)
        {
            if (NormalizeText(key).Contains(normalizedNeedle, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool HasSignedToneStack(IReadOnlyDictionary<string, float> knobs)
    {
        if (knobs == null)
            return false;

        foreach (KeyValuePair<string, float> pair in knobs)
        {
            string key = NormalizeText(pair.Key);
            if (pair.Value < 0f &&
                ContainsAny(key, "bass", "mid", "middle", "treble", "pres", "presence", "high", "low"))
            {
                return true;
            }
        }

        return false;
    }

    private static float NormalizeGenericKnob(float value)
    {
        if (!float.IsFinite(value))
            return 0.5f;

        float absolute = Mathf.Abs(value);
        if (absolute <= 1.0001f)
            return Mathf.Clamp01(value);
        if (value < 0f)
            return Mathf.InverseLerp(-10f, 10f, Mathf.Clamp(value, -10f, 10f));
        if (absolute <= 10.0001f)
            return Mathf.Clamp01(value / 10f);
        if (absolute <= 100.0001f)
            return Mathf.Clamp01(value / 100f);
        return Mathf.Clamp01(value / 1000f);
    }

    private static float ConvertEqValueToDb(float value)
    {
        if (!float.IsFinite(value))
            return 0f;

        if (value >= -24f && value <= 24f)
            return value;

        return Mathf.Lerp(-12f, 12f, NormalizeGenericKnob(value));
    }

    private static bool TryExtractFrequencyFromKnobName(string knobName, out float frequencyHz)
    {
        frequencyHz = 0f;
        if (string.IsNullOrWhiteSpace(knobName))
            return false;

        int end = knobName.Length - 1;
        while (end >= 0 && !char.IsDigit(knobName[end]))
            end--;
        if (end < 0)
            return false;

        bool kiloSuffix = false;
        for (int i = end + 1; i < knobName.Length; i++)
        {
            char suffix = char.ToLowerInvariant(knobName[i]);
            if (suffix == 'k')
            {
                kiloSuffix = true;
                break;
            }
        }

        int start = end;
        while (start >= 0 && (char.IsDigit(knobName[start]) || knobName[start] == '.'))
            start--;
        string numberText = knobName.Substring(start + 1, end - start);
        if (!float.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out frequencyHz))
            return false;

        if (kiloSuffix || (frequencyHz > 0f && frequencyHz < 20f))
            frequencyHz *= 1000f;

        return frequencyHz >= 20f && frequencyHz <= 22050f;
    }

    private static int FindNearestZamGeqBand(float frequencyHz)
    {
        int nearest = 0;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < ZamGeq31Frequencies.Length; i++)
        {
            float distance = Mathf.Abs(Mathf.Log(Mathf.Max(1f, frequencyHz) / ZamGeq31Frequencies[i]));
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = i;
        }

        return nearest;
    }

    private static float Smooth01(float value)
    {
        float clamped = Mathf.Clamp01(value);
        return clamped * clamped * (3f - (2f * clamped));
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

    private static bool HasToken(string normalizedText, string token)
    {
        string normalizedToken = NormalizeText(token);
        if (string.IsNullOrWhiteSpace(normalizedText) || string.IsNullOrWhiteSpace(normalizedToken))
            return false;

        string[] tokens = normalizedText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            if (string.Equals(tokens[i], normalizedToken, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
