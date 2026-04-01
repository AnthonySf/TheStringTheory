using System;
using System.Collections.Generic;
using UnityEngine;

public enum GuitarRenderMode
{
    Highway3D,
    Tabs
}

public enum GameplayNoteResult
{
    Pending,
    Hit,
    Missed
}

public enum NoteTechnique
{
    None,
    HammerOn,
    PullOff,
    Slide,
    Bend,
    Vibrato
}

public enum NoteTechniqueSegmentType
{
    Slide,
    Bend,
    Sustain,
    Vibrato
}

public static class GuitarTechniqueVisualThresholds
{
    public const float SustainSeconds = 0.35f;
}

[Serializable]
public struct NoteTechniqueSegmentData
{
    public NoteTechniqueSegmentType type;
    public float startOffset;
    public float endOffset;
    public int startFret;
    public int endFret;
    public float startBend;
    public float endBend;

    public NoteTechniqueSegmentData(
        NoteTechniqueSegmentType segmentType,
        float startTimeOffset,
        float endTimeOffset,
        int fromFret,
        int toFret,
        float fromBend,
        float toBend)
    {
        type = segmentType;
        startOffset = startTimeOffset;
        endOffset = endTimeOffset;
        startFret = fromFret;
        endFret = toFret;
        startBend = fromBend;
        endBend = toBend;
    }
}

[Serializable]
public struct NoteData
{
    public int id;
    public float time;
    public float duration;
    public int stringIdx;
    public int fret;
    public string note;
    public int chordId;

    public NoteTechnique technique;
    public int slideTargetFret;
    public float bendStep;
    public float bendVisualStartTime;
    public float bendVisualDuration;
    public bool bendPreBend;
    public bool bendRelease;
    public bool isMuted;
    public bool isLegato;
    public bool requiresPluck;
    public int linkedFromNoteId;
    public List<NoteTechniqueSegmentData> techniqueSegments;

    // Simple constructor for backward compatibility
    public NoteData(float t, int s, int f, string n)
    {
        id = -1;
        time = t;
        duration = 0f;
        stringIdx = s;
        fret = f;
        note = n;
        chordId = -1;
        technique = NoteTechnique.None;
        slideTargetFret = -1;
        bendStep = 0;
        bendVisualStartTime = -1f;
        bendVisualDuration = 0;
        bendPreBend = false;
        bendRelease = false;
        isMuted = false;
        isLegato = false;
        requiresPluck = true;
        linkedFromNoteId = -1;
        techniqueSegments = null;
    }

    // Full constructor for the XML Loader
    public NoteData(int noteId, float t, float d, int s, int f, string n, int assignedChordId,
                    NoteTechnique tech = NoteTechnique.None, int slideTo = -1, float bend = 0, bool legato = false,
                    bool pluckRequired = true, int linkedFrom = -1, bool preBend = false, bool release = false,
                    float visualBendStartTime = -1f, float visualBendDuration = 0f, List<NoteTechniqueSegmentData> segments = null,
                    bool muted = false)
    {
        id = noteId;
        time = t;
        duration = d;
        stringIdx = s;
        fret = f;
        note = n;
        chordId = assignedChordId;
        technique = tech;
        slideTargetFret = slideTo;
        bendStep = bend;
        bendVisualStartTime = visualBendStartTime;
        bendVisualDuration = visualBendDuration;
        bendPreBend = preBend;
        bendRelease = release;
        isMuted = muted;
        isLegato = legato;
        requiresPluck = pluckRequired;
        linkedFromNoteId = linkedFrom;
        techniqueSegments = segments;
    }
}

[Serializable]
public sealed class GameplayNoteState
{
    public NoteData data;
    public GameplayNoteResult result = GameplayNoteResult.Pending;
    public float resolvedAt = -1f;
    public bool isJudgeable;

    public bool IsResolved => result != GameplayNoteResult.Pending;
    public bool IsHit => result == GameplayNoteResult.Hit;
    public bool IsMissed => result == GameplayNoteResult.Missed;

    public GameplayNoteState(NoteData note)
    {
        data = note;
    }
}

[Serializable]
public sealed class TabSectionData
{
    public int index;
    public float startTime;
    public float endTime;
    public List<int> noteIds = new List<int>();
}

public sealed class GuitarGameplaySnapshot
{
    public float songTime;
    public bool isPaused;
    public bool noteByNoteModeEnabled;
    public bool noteByNoteWaitingForMatch;
    public bool loopEnabled;
    public float loopStartTime;
    public float loopEndTime;
    public int selectedLoopMarker;
    public bool showLoopSettings;
    public bool loopPreviewPlaying;
    public bool showLoopPausePopup;
    public int selectedLoopPausePopupIndex;
    public float loopPauseDurationSeconds;
    public float loopRestartPauseRemainingSeconds;
    public float playbackSpeedPercent;
    public bool scoreSaveInvalidated;
    public float currentSectionProgress;
    public int currentSectionIndex;
    public int nextSectionIndex;
    public float sectionDuration;
    public int currentSessionScoreHits;
    public int currentSessionScoreMisses;
    public float currentSessionScorePercent;
    public List<GameplayNoteState> noteStates;
    public List<TabSectionData> sections;
    public HashSet<int> latestDetectedPitches;
    public bool showSongSettings;
    public bool showOffsetHelper;
    public bool offsetHelperAdjusting;
    public bool offsetHelperPreviewPlaying;
    public float offsetHelperAnchorTime;
    public float offsetHelperPreviewStartTime;
    public float offsetHelperPreviewEndTime;
    public string offsetHelperAnchorLabel;
    public bool showMainMenu;
    public bool mainMenuFlowActive;
    public int selectedMainMenuIndex;
    public bool showSongSelection;
    public bool songSelectionSongConfirmed;
    public bool showTrackSelection;
    public int selectedPauseActionIndex;
    public int selectedSongSettingsIndex;
    public List<string> availableSongNames;
    public List<string> availableSongSubtitles;
    public List<float> availableSongScores;
    public int selectedSongIndex;
    public List<string> availableTrackNames;
    public List<float> availableTrackScores;
    public int selectedTrackIndex;
    public string currentSongDisplayName;
    public int songListScrollOffset;
    public float audioOffsetMs;
    public float tabSpeedOffsetPercent;
    public float songStartDelaySeconds;
    public float songVolumePercent;
    public string selectedTrackDisplayName;
    public string trackSelectionHint;
    public string offsetScopeLabel;
    public string offsetScopeHint;
    public bool hasBackingTrack;
    public bool isBackingTrackPlaying;
    public float backingTrackTime;
    public bool noteDetectorConnected;
    public float inputLevelNormalized;
    public float songDuration;
    public float songProgressNormalized;
    public bool songEnded;
    public int selectedSongEndActionIndex;
    public float currentTrackBestScorePercent;
    public bool showStartupTuningReminder;
    public bool showGlobalSettings;
    public int selectedGlobalSettingsTopIndex;
    public int selectedGlobalSettingsItemIndex;
    public string activeGlobalSettingsCategory;
    public List<RuntimeSettingSectionSnapshot> runtimeSettingsSections;
}

public sealed class RuntimeSettingSectionSnapshot
{
    public string title;
    public List<RuntimeSettingSnapshot> settings;
}

public sealed class RuntimeSettingSnapshot
{
    public string id;
    public string label;
    public string tooltip;
    public string valueType;
    public string value;
    public float min;
    public float max;
    public float step;
    public List<string> enumOptions;
}
