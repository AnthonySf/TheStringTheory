using System.Collections.Generic;

internal sealed class Gp5Song
{
    public string filePath;
    public string version;
    public string title;
    public string subtitle;
    public string artist;
    public string album;
    public string words;
    public string music;
    public string copyright;
    public string tabbedBy;
    public string instructions;
    public string tempoName;
    public int initialTempo = 120;
    public List<Gp5TempoChange> tempoChanges = new List<Gp5TempoChange>();
    public List<Gp5MeasureHeader> measureHeaders = new List<Gp5MeasureHeader>();
    public List<Gp5Track> tracks = new List<Gp5Track>();
}

internal sealed class Gp5TempoChange
{
    public double quarterPos;
    public double bpm;
}

internal sealed class Gp5MeasureHeader
{
    public int number;
    public int numerator = 4;
    public int denominator = 4;
    public bool isRepeatOpen;
    public int repeatClose = -1;
    public int repeatAlternative;
    public bool hasDoubleBar;
    public string markerName;
    public double startQuarter;
    public double lengthQuarter;
}

internal sealed class Gp5MidiChannel
{
    public int index;
    public int effectChannelIndex = -1;
    public int instrument;
    public int volume = 95;
    public int balance = 64;
    public int chorus;
    public int reverb;
    public int phaser;
    public int tremolo;

    public bool IsPercussion => index % 16 == 9;
}

internal sealed class Gp5Track
{
    public int index;
    public string partId;
    public string name;
    public bool isPercussionTrack;
    public bool isVisible = true;
    public bool isSolo;
    public bool isMuted;
    public bool useRse;
    public int port;
    public int fretCount;
    public int capo;
    public int midiBank;
    public int sourceMidiChannel = -1;
    public int sourceMidiProgram = -1;
    public int[] stringsHighToLow = new int[0];
    public readonly List<Gp5Beat> beats = new List<Gp5Beat>();
}

internal sealed class Gp5Beat
{
    public int measureIndex;
    public int voiceIndex;
    public double startQuarter;
    public double durationQuarter;
    public bool isRest;
    public bool isEmpty;
    public bool beatWideVibrato;
    public bool noteVibrato;
    public int tempoChangeBpm = -1;
    public string tempoName;
    public readonly List<Gp5Note> notes = new List<Gp5Note>();
}

internal sealed class Gp5Note
{
    public int stringNumber;
    public int stringIdx;
    public int fret;
    public int midi;
    public int velocity = 95;
    public bool isTie;
    public bool isDead;
    public bool isGhost;
    public bool isAccentuated;
    public bool isHeavyAccentuated;
    public bool isHammer;
    public bool letRing;
    public bool isPalmMute;
    public bool isStaccato;
    public bool isVibrato;
    public bool isHarmonic;
    public bool hasSlide;
    public int slideFlags;
    public double durationPercent = 1.0;
    public Gp5BendEffect bend;
}

internal sealed class Gp5BendEffect
{
    public int type;
    public int value;
    public readonly List<Gp5BendPoint> points = new List<Gp5BendPoint>();
}

internal sealed class Gp5BendPoint
{
    public int position;
    public float value;
    public bool vibrato;
}
