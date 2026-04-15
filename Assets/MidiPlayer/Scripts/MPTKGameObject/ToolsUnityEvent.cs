using System.Collections.Generic;
using UnityEngine.Events;

namespace MidiPlayerTK
{

    [System.Serializable]
    /// @ingroup unity_event_bridges
    public class EventMidiClass : UnityEvent<MPTKEvent>
    {
    }

    [System.Serializable]
    /// @ingroup unity_event_bridges
    public class EventNotesMidiClass : UnityEvent<List<MPTKEvent>>
    {
    }

    [System.Serializable]
    /// @ingroup unity_event_bridges
    public class EventSynthClass : UnityEvent<string>
    {
    }

    [System.Serializable]
    /// @ingroup unity_event_bridges
    public class EventStartMidiClass : UnityEvent<string>
    {
    }

    [System.Serializable]
    /// @ingroup unity_event_bridges
    public class EventEndMidiClass : UnityEvent<string, EventEndMidiEnum>
    {
    }

    [System.Serializable]
    /// @ingroup unity_event_bridges
    static public class ToolsUnityEvent
    {

        static public bool HasPersistantEvent(this EventMidiClass evt)
        {
            if (evt != null && evt.GetPersistentEventCount() > 0 && !string.IsNullOrEmpty(evt.GetPersistentMethodName(0)))
                return true;
            else
                return false;
        }

        static public bool HasPersistantEvent(this UnityEvent evt)
        {
            if (evt != null && evt.GetPersistentEventCount() > 0 && !string.IsNullOrEmpty(evt.GetPersistentMethodName(0)))
                return true;
            else
                return false;
        }
        static public bool HasPersistantEvent(this EventNotesMidiClass evt)
        {
            if (evt != null && evt.GetPersistentEventCount() > 0 && !string.IsNullOrEmpty(evt.GetPersistentMethodName(0)))
                return true;
            else
                return false;
        }

        static public bool HasPersistantEvent(this EventStartMidiClass evt)
        {
            if (evt != null && evt.GetPersistentEventCount() > 0 && !string.IsNullOrEmpty(evt.GetPersistentMethodName(0)))
                return true;
            else
                return false;
        }

        static public bool HasPersistantEvent(this EventEndMidiClass evt)
        {
            if (evt != null && evt.GetPersistentEventCount() > 0 && !string.IsNullOrEmpty(evt.GetPersistentMethodName(0)))
                return true;
            else
                return false;
        }

        static public bool HasPersistantEvent(this EventSynthClass evt)
        {
            if (evt != null && evt.GetPersistentEventCount() > 0 && !string.IsNullOrEmpty(evt.GetPersistentMethodName(0)))
                return true;
            else
                return false;
        }

    }
}
