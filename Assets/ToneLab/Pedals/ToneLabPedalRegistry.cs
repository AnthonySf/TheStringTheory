using System;
using System.Collections.Generic;
using System.Linq;

public static class ToneLabPedalRegistry
{
    private static readonly IToneLabPedalDescriptor[] builtInDescriptors =
    {
        new NoiseGatePedalDescriptor(),
        new AmpPedalDescriptor(),
        new CabSimPedalDescriptor(),
        new StudioEqPedalDescriptor(),
        new DistortionPedalDescriptor(),
        new ChorusPedalDescriptor(),
        new PhaserPedalDescriptor(),
        new DelayPedalDescriptor(),
        new ReverbPedalDescriptor(),
        new CompressorPedalDescriptor()
    };

    private static readonly Dictionary<UnityToneLabRuntime.ToneLabPedalType, IToneLabPedalDescriptor> descriptorsByType =
        builtInDescriptors.ToDictionary(descriptor => descriptor.PedalType);
    private static readonly object syncRoot = new object();
    private static IToneLabPedalDescriptor[] externalDescriptors = Array.Empty<IToneLabPedalDescriptor>();
    private static Dictionary<string, IToneLabPedalDescriptor> descriptorsById = BuildDescriptorIdMap(builtInDescriptors);
    private static DateTime lastExternalRefreshUtc = DateTime.MinValue;
    private static string lastExternalRefreshSummary = "External pedals have not been scanned yet.";
    private static string lastExternalContentSignature = string.Empty;

    public static IReadOnlyList<IToneLabPedalDescriptor> AllDescriptors
    {
        get
        {
            lock (syncRoot)
                return builtInDescriptors.Concat(externalDescriptors).ToArray();
        }
    }

    public static string LastExternalRefreshSummary
    {
        get
        {
            lock (syncRoot)
                return lastExternalRefreshSummary;
        }
    }

    public static DateTime LastExternalRefreshUtc
    {
        get
        {
            lock (syncRoot)
                return lastExternalRefreshUtc;
        }
    }

    public static bool RefreshExternalDescriptors(bool force = false)
    {
        string contentSignature = ToneLabExternalPedalCatalog.BuildContentSignature();
        lock (syncRoot)
        {
            if (!force &&
                lastExternalRefreshUtc != DateTime.MinValue &&
                string.Equals(contentSignature, lastExternalContentSignature, StringComparison.Ordinal))
            {
                return false;
            }
        }

        IToneLabPedalDescriptor[] scannedDescriptors = ToneLabExternalPedalCatalog.ScanDescriptors();
        lock (syncRoot)
        {
            externalDescriptors = scannedDescriptors ?? Array.Empty<IToneLabPedalDescriptor>();
            descriptorsById = BuildDescriptorIdMap(builtInDescriptors.Concat(externalDescriptors));
            lastExternalRefreshUtc = DateTime.UtcNow;
            lastExternalContentSignature = contentSignature;
            lastExternalRefreshSummary = BuildRefreshSummary(externalDescriptors);
        }

        return true;
    }

    public static IToneLabPedalDescriptor GetDescriptor(UnityToneLabRuntime.ToneLabPedalType pedalType)
    {
        if (descriptorsByType.TryGetValue(pedalType, out IToneLabPedalDescriptor descriptor))
            return descriptor;

        throw new ArgumentOutOfRangeException(nameof(pedalType), pedalType, "Unknown Tone Lab pedal type.");
    }

    public static IToneLabPedalDescriptor GetDescriptor(string descriptorId)
    {
        string resolvedDescriptorId = ToneLabExternalPedalCatalog.ResolveNamDescriptorId(descriptorId);
        if (!string.IsNullOrWhiteSpace(resolvedDescriptorId))
        {
            lock (syncRoot)
            {
                if (descriptorsById.TryGetValue(resolvedDescriptorId, out IToneLabPedalDescriptor descriptor))
                    return descriptor;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(descriptorId), descriptorId, "Unknown Tone Lab pedal descriptor.");
    }

    public static IToneLabPedalDescriptor GetDescriptor(UnityToneLabRuntime.ToneLabPedalSlot slot)
    {
        if (slot == null)
            throw new ArgumentNullException(nameof(slot));

        if (!string.IsNullOrWhiteSpace(slot.descriptor_id))
        {
            string resolvedDescriptorId = ToneLabExternalPedalCatalog.ResolveNamDescriptorId(slot.descriptor_id);
            lock (syncRoot)
            {
                if (descriptorsById.TryGetValue(resolvedDescriptorId, out IToneLabPedalDescriptor descriptor))
                    return descriptor;
            }

            if (slot.pedal_type == UnityToneLabRuntime.ToneLabPedalType.Lv2Plugin ||
                slot.pedal_type == UnityToneLabRuntime.ToneLabPedalType.NamModel)
            {
                return ToneLabExternalPedalCatalog.CreateMissingDescriptor(slot);
            }
        }

        if (slot.pedal_type == UnityToneLabRuntime.ToneLabPedalType.Lv2Plugin ||
            slot.pedal_type == UnityToneLabRuntime.ToneLabPedalType.NamModel)
        {
            return ToneLabExternalPedalCatalog.CreateMissingDescriptor(slot);
        }

        return GetDescriptor(slot.pedal_type);
    }

    private static string BuildRefreshSummary(IReadOnlyList<IToneLabPedalDescriptor> descriptors)
    {
        int lv2Count = 0;
        int namCount = 0;
        if (descriptors != null)
        {
            for (int i = 0; i < descriptors.Count; i++)
            {
                IToneLabPedalDescriptor descriptor = descriptors[i];
                if (descriptor == null)
                    continue;

                if (descriptor.PedalType == UnityToneLabRuntime.ToneLabPedalType.Lv2Plugin)
                    lv2Count++;
                else if (descriptor.PedalType == UnityToneLabRuntime.ToneLabPedalType.NamModel)
                    namCount++;
            }
        }

        return $"External pedals scanned: {lv2Count} LV2, {namCount} NAM.";
    }

    private static Dictionary<string, IToneLabPedalDescriptor> BuildDescriptorIdMap(IEnumerable<IToneLabPedalDescriptor> descriptors)
    {
        Dictionary<string, IToneLabPedalDescriptor> map = new Dictionary<string, IToneLabPedalDescriptor>(StringComparer.Ordinal);
        foreach (IToneLabPedalDescriptor descriptor in descriptors)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.DescriptorId))
                continue;

            map[descriptor.DescriptorId] = descriptor;
        }

        return map;
    }
}
