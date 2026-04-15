//#define MPTK_PRO
#define DEBUG_START_MIDIx
using UnityEngine;

namespace MidiPlayerTK
{
    // required to instanciate in edit mode: Awake() and Start() are executed when this class is instanciated
    [ExecuteAlways] 
    /// @ingroup runtime_debug_tools
    public partial class MidiFileEditorPlayer : MidiFilePlayer
    {
        new void Awake()
        {
            MPTK_CorePlayer = true;
            base.Awake();
        }

    }
}

