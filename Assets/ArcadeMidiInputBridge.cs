using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public sealed class ArcadeMidiInputBridge : IDisposable
{
    public readonly struct MidiInputEvent
    {
        public readonly int note;
        public readonly int velocity;
        public readonly bool noteOn;

        public MidiInputEvent(int note, int velocity, bool noteOn)
        {
            this.note = note;
            this.velocity = velocity;
            this.noteOn = noteOn;
        }
    }

    private readonly object syncRoot = new object();
    private readonly HashSet<int> heldNotes = new HashSet<int>();
    private readonly List<MidiInputEvent> pendingEvents = new List<MidiInputEvent>();
    private bool running;
    private string lastError = string.Empty;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const uint CallbackFunction = 0x00030000;
    private const uint MimData = 0x3C3;

    private delegate void MidiInCallback(IntPtr handle, uint message, IntPtr instance, IntPtr param1, IntPtr param2);

    private IntPtr midiHandle = IntPtr.Zero;
    private MidiInCallback callback;

    [DllImport("winmm.dll")]
    private static extern uint midiInGetNumDevs();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MidiInCaps
    {
        public ushort manufacturerId;
        public ushort productId;
        public uint driverVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string productName;

        public uint support;
    }

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    private static extern uint midiInGetDevCaps(UIntPtr deviceId, out MidiInCaps caps, uint capsSize);

    [DllImport("winmm.dll")]
    private static extern uint midiInOpen(out IntPtr handle, uint deviceId, MidiInCallback callback, IntPtr instance, uint flags);

    [DllImport("winmm.dll")]
    private static extern uint midiInStart(IntPtr handle);

    [DllImport("winmm.dll")]
    private static extern uint midiInStop(IntPtr handle);

    [DllImport("winmm.dll")]
    private static extern uint midiInReset(IntPtr handle);

    [DllImport("winmm.dll")]
    private static extern uint midiInClose(IntPtr handle);
#endif

    public bool IsRunning => running;
    public string LastError => lastError;

    public static List<string> GetInputDeviceNames()
    {
        List<string> names = new List<string>();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        uint deviceCount = midiInGetNumDevs();
        uint capsSize = (uint)Marshal.SizeOf(typeof(MidiInCaps));
        for (uint i = 0; i < deviceCount; i++)
        {
            if (midiInGetDevCaps((UIntPtr)i, out MidiInCaps caps, capsSize) == 0 && !string.IsNullOrWhiteSpace(caps.productName))
                names.Add(caps.productName.Trim());
            else
                names.Add($"MIDI Device {i}");
        }
#endif

        return names;
    }

    public bool Start(int deviceIndex)
    {
        if (running)
            return true;

        lastError = string.Empty;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        uint deviceCount = midiInGetNumDevs();
        if (deviceCount == 0)
        {
            lastError = "No Windows MIDI input devices were found.";
            return false;
        }

        uint resolvedDeviceIndex = (uint)Math.Max(0, Math.Min(deviceIndex, (int)deviceCount - 1));
        callback = HandleMidiInput;

        uint openResult = midiInOpen(out midiHandle, resolvedDeviceIndex, callback, IntPtr.Zero, CallbackFunction);
        if (openResult != 0 || midiHandle == IntPtr.Zero)
        {
            midiHandle = IntPtr.Zero;
            lastError = $"Windows MIDI input open failed: {openResult}";
            return false;
        }

        uint startResult = midiInStart(midiHandle);
        if (startResult != 0)
        {
            Stop();
            lastError = $"Windows MIDI input start failed: {startResult}";
            return false;
        }

        running = true;
        return true;
#else
        lastError = "Direct MIDI input is only available on Windows builds.";
        return false;
#endif
    }

    public void Stop()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (midiHandle != IntPtr.Zero)
        {
            midiInStop(midiHandle);
            midiInReset(midiHandle);
            midiInClose(midiHandle);
            midiHandle = IntPtr.Zero;
        }
#endif

        lock (syncRoot)
        {
            heldNotes.Clear();
            pendingEvents.Clear();
        }

        running = false;
    }

    public List<MidiInputEvent> ConsumeEvents()
    {
        lock (syncRoot)
        {
            List<MidiInputEvent> events = new List<MidiInputEvent>(pendingEvents);
            pendingEvents.Clear();
            return events;
        }
    }

    public HashSet<int> GetHeldNotesSnapshot()
    {
        lock (syncRoot)
        {
            return new HashSet<int>(heldNotes);
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private void HandleMidiInput(IntPtr handle, uint message, IntPtr instance, IntPtr param1, IntPtr param2)
    {
        if (message != MimData)
            return;

        int packedMessage = param1.ToInt32();
        int status = packedMessage & 0xFF;
        int command = status & 0xF0;
        int note = (packedMessage >> 8) & 0xFF;
        int velocity = (packedMessage >> 16) & 0xFF;

        bool noteOn = command == 0x90 && velocity > 0;
        bool noteOff = command == 0x80 || (command == 0x90 && velocity <= 0);
        if (!noteOn && !noteOff)
            return;

        lock (syncRoot)
        {
            if (noteOn)
            {
                heldNotes.Add(note);
                pendingEvents.Add(new MidiInputEvent(note, velocity, true));
            }
            else
            {
                heldNotes.Remove(note);
                pendingEvents.Add(new MidiInputEvent(note, 0, false));
            }
        }
    }
#endif
}
