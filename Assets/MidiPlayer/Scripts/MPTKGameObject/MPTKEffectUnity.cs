using System;
using UnityEngine;

namespace MidiPlayerTK
{
    /// <summary>
    /// Unlike SoundFont effects, Unity effects are applied to the whole player.\n
    /// Unity effect parameters are richer and are based on Unity audio algorithms.\n
    /// https://docs.unity3d.com/Manual/class-AudioEffectMixer.html\n
    /// Only the most important effects are integrated in Maestro: Reverb and Chorus. Additional Unity effects can be added if needed. 
    /// @version Maestro Pro 
    /// @note
    ///     - Unity effect integration modules are available only in Maestro MPTK Pro. 
    ///     - By default, these effects are disabled in Maestro. 
    ///     - To enable them, adjust settings in the prefab inspector: Synth Parameters / Unity Effect.
    ///     - Each setting is also available by script.
    /// @ingroup unity_effects
    /// @code
    /// // Find an MPTK prefab (also works for MidiStreamPlayer, MidiExternalPlayer, and all classes that inherit from MidiSynth).
    /// MidiFilePlayer fp = FindFirstObjectByType<MidiFilePlayer>();
    /// fp.MPTK_EffectUnity.EnableReverb = true;
    /// fp.MPTK_EffectUnity.ReverbDelay = 0.09f;
    /// @endcode
    /// </summary>
    public partial class MPTKEffectUnity : ScriptableObject
    {

    }
}


