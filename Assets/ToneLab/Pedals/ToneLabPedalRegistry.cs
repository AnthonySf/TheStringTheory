using System;
using System.Collections.Generic;
using System.Linq;

public static class ToneLabPedalRegistry
{
    private static readonly IToneLabPedalDescriptor[] allDescriptors =
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
        allDescriptors.ToDictionary(descriptor => descriptor.PedalType);

    public static IReadOnlyList<IToneLabPedalDescriptor> AllDescriptors => allDescriptors;

    public static IToneLabPedalDescriptor GetDescriptor(UnityToneLabRuntime.ToneLabPedalType pedalType)
    {
        if (descriptorsByType.TryGetValue(pedalType, out IToneLabPedalDescriptor descriptor))
            return descriptor;

        throw new ArgumentOutOfRangeException(nameof(pedalType), pedalType, "Unknown Tone Lab pedal type.");
    }
}
