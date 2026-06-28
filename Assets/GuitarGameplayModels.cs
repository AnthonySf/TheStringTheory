using System;
using System.Collections.Generic;
using UnityEngine;

public enum GuitarRenderMode
{
    Highway3D,
    Tabs,
    TabsAlphaTab,
    Highway3DAndTabs
}

public static class GuitarRenderModeExtensions
{
    public static bool UsesHighway3D(this GuitarRenderMode mode)
    {
        return mode == GuitarRenderMode.Highway3D || mode == GuitarRenderMode.Highway3DAndTabs;
    }

    public static bool UsesClassicTabs(this GuitarRenderMode mode)
    {
        return mode == GuitarRenderMode.Tabs;
    }

    public static bool UsesAlphaTab(this GuitarRenderMode mode)
    {
        return mode == GuitarRenderMode.TabsAlphaTab || mode == GuitarRenderMode.Highway3DAndTabs;
    }

    public static bool UsesTabHudLayout(this GuitarRenderMode mode)
    {
        return mode == GuitarRenderMode.Tabs || mode == GuitarRenderMode.TabsAlphaTab;
    }
}

public enum GuitarGameplayMode
{
    Guitar,
    Arcade
}

public enum SongLibraryType
{
    Guitar,
    Arcade
}

public enum HighwayCharacterDisplayMode
{
    Always,
    Never,
    HeroModeOnly
}

public enum HighwayCharacterChoice
{
    Hero = 0,
    ElizeColor2 = 1
}

public enum HighwayCameraEngine
{
    Legacy = 0,
    SmartLookahead = 1
}

public enum GameplayNoteResult
{
    Pending,
    Hit,
    Missed
}

public enum ArcadeDifficulty
{
    Easy,
    Medium,
    Hard,
    Expert
}

public enum ArcadeInstrument
{
    Guitar,
    Bass,
    Rhythm,
    CoopGuitar,
    Keys,
    Drums
}

public enum ArcadeNoteType
{
    Strum,
    Hopo,
    Tap
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
public sealed class ArpeggioGuideData
{
    public int id;
    public float startTime;
    public float endTime;
    public string chordName;
    public int[] stringFrets;
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
    public string chordName;

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
        chordName = null;
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
                    bool muted = false, string chordDisplayName = null)
    {
        id = noteId;
        time = t;
        duration = d;
        stringIdx = s;
        fret = f;
        note = n;
        chordId = assignedChordId;
        chordName = chordDisplayName;
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
public struct ArcadeNoteData
{
    public int id;
    public float time;
    public float duration;
    public float sustainBeats;
    public int lane;
    public bool isOpen;
    public bool isHopo;
    public bool isTap;
    public ArcadeNoteType noteType;
    public int chordId;

    public ArcadeNoteData(int noteId, float startTime, float noteDuration, float noteSustainBeats, int laneIndex, bool openNote, bool hopoNote, bool tapNote, int assignedChordId)
    {
        id = noteId;
        time = startTime;
        duration = noteDuration;
        sustainBeats = noteSustainBeats;
        lane = laneIndex;
        isOpen = openNote;
        isHopo = hopoNote && !tapNote;
        isTap = tapNote;
        noteType = tapNote ? ArcadeNoteType.Tap : hopoNote ? ArcadeNoteType.Hopo : ArcadeNoteType.Strum;
        chordId = assignedChordId;
    }

    public ArcadeNoteData(int noteId, float startTime, float noteDuration, float noteSustainBeats, int laneIndex, bool openNote, bool tapNote, int assignedChordId)
        : this(noteId, startTime, noteDuration, noteSustainBeats, laneIndex, openNote, false, tapNote, assignedChordId)
    {
    }
}

[Serializable]
public sealed class ArcadeNoteState
{
    public ArcadeNoteData data;
    public GameplayNoteResult result = GameplayNoteResult.Pending;
    public float resolvedAt = -1f;
    public bool isJudgeable;

    public bool IsResolved => result != GameplayNoteResult.Pending;
    public bool IsHit => result == GameplayNoteResult.Hit;
    public bool IsMissed => result == GameplayNoteResult.Missed;

    public ArcadeNoteState(ArcadeNoteData note)
    {
        data = note;
    }
}

public sealed class ArcadeArrangementSummary
{
    public string ArrangementId;
    public string DisplayName;
    public ArcadeInstrument Instrument;
    public List<ArcadeDifficulty> Difficulties = new List<ArcadeDifficulty>();

    public ArcadeArrangementSummary Clone()
    {
        return new ArcadeArrangementSummary
        {
            ArrangementId = ArrangementId,
            DisplayName = DisplayName,
            Instrument = Instrument,
            Difficulties = Difficulties != null ? new List<ArcadeDifficulty>(Difficulties) : new List<ArcadeDifficulty>()
        };
    }
}

public sealed class ArcadeChartData
{
    public string SourcePath;
    public int LaneCount = 5;
    public List<ArcadeArrangementSummary> Arrangements = new List<ArcadeArrangementSummary>();
    public List<ArcadeNoteData> Notes = new List<ArcadeNoteData>();
    public List<ArcadePracticeSectionData> PracticeSections = new List<ArcadePracticeSectionData>();
    public float DurationSeconds;
}

[Serializable]
public sealed class ArcadePracticeSectionData
{
    public int index;
    public string name;
    public float startTime;
    public float endTime;
}

[Serializable]
public sealed class TabSectionData
{
    public int index;
    public float startTime;
    public float endTime;
    public List<int> noteIds = new List<int>();
}

[Serializable]
public sealed class SongTimelineSectionData
{
    public int index;
    public string name;
    public float startTime;
    public float endTime;
}

public enum MultiplayerRhythmInputDeviceKind
{
    Keyboard,
    Controller
}

[Serializable]
public sealed class MultiplayerRhythmPlayerSnapshot
{
    public int playerIndex;
    public string playerLabel;
    public string deviceLabel;
    public List<ArcadeNoteState> arcadeNoteStates;
    public bool[] heldLanes;
    public int scoreValue;
    public float scorePercent;
    public int comboCount;
    public int multiplier;
    public int hitCount;
    public int missCount;
    public int maxCombo;
    public bool winner;
}

public sealed class GuitarGameplaySnapshot
{
    public GuitarGameplayMode gameplayMode;
    public bool multiplayerRhythmMode;
    public bool showMultiplayerRhythmSetup;
    public List<string> multiplayerRhythmAvailableInputDevices;
    public int selectedMultiplayerRhythmSetupIndex;
    public int selectedMultiplayerRhythmPlayerOneDeviceIndex;
    public int selectedMultiplayerRhythmPlayerTwoDeviceIndex;
    public string multiplayerRhythmPlayerOneSetupLabel;
    public string multiplayerRhythmPlayerTwoSetupLabel;
    public int multiplayerRhythmSetupCapturePlayerIndex;
    public bool multiplayerRhythmSetupCanContinue;
    public string multiplayerRhythmSetupStatusText;
    public List<MultiplayerRhythmPlayerSnapshot> multiplayerRhythmPlayers;
    public int multiplayerRhythmWinningPlayerIndex;
    public bool multiplayerRhythmDraw;
    public SongLibraryType songLibraryType;
    public int selectedSongLibraryTypeIndex;
    public float songTime;
    public float songDurationSeconds;
    public bool isPaused;
    public bool noteByNoteModeEnabled;
    public bool noteByNoteWaitingForMatch;
    public bool heroModeEnabled;
    public int heroModeHeartCount;
    public int currentHeroHeartsRemaining;
    public bool showHighwayCharacter;
    public bool forceStandardTuning;
    public bool loopEnabled;
    public float loopStartTime;
    public float loopEndTime;
    public bool loopStartConfigured;
    public bool loopEndConfigured;
    public int selectedLoopMarker;
    public bool showLoopSettings;
    public bool showLoopBookmarksPanel;
    public bool loopPreviewPlaying;
    public bool showLoopPausePopup;
    public int selectedLoopPausePopupIndex;
    public float loopPauseDurationSeconds;
    public float loopRestartPauseRemainingSeconds;
    public bool showArrangementDifficultyPopup;
    public bool arrangementDifficultyModeAvailable;
    public List<string> arrangementDifficultyOptionLabels;
    public int selectedArrangementDifficultyOptionIndex;
    public List<string> loopBookmarkNames;
    public List<string> loopBookmarkDetails;
    public int selectedLoopBookmarkIndex;
    public bool selectedLoopBookmarkModified;
    public bool loopBookmarkRenameActive;
    public string loopBookmarkRenameDraft;
    public string loopBookmarkTrackLabel;
    public List<string> rhythmPracticeSectionNames;
    public List<string> rhythmPracticeSectionDetails;
    public int selectedRhythmPracticeSectionIndex;
    public int rhythmPracticeLoopStartSectionIndex;
    public int rhythmPracticeLoopEndSectionIndex;
    public float playbackSpeedPercent;
    public bool scoreSaveInvalidated;
    public float currentSectionProgress;
    public int currentSectionIndex;
    public int nextSectionIndex;
    public float sectionDuration;
    public int currentSessionScoreHits;
    public int currentSessionScoreMisses;
    public float currentSessionScorePercent;
    public int currentSessionScoreValue;
    public int currentSessionScoreCombo;
    public int currentSessionScoreMultiplier;
    public int currentSessionArcadeScore;
    public int currentSessionArcadeCombo;
    public int currentSessionArcadeMultiplier;
    public List<GameplayNoteState> noteStates;
    public List<ArpeggioGuideData> arpeggioGuides;
    public List<ArcadeNoteState> arcadeNoteStates;
    public int arcadeLaneCount;
    public string selectedArcadeArrangementId;
    public string selectedArcadeArrangementDisplayName;
    public string selectedArcadeDifficultyLabel;
    public List<string> arcadeDifficultyLabels;
    public List<bool> arcadeDifficultyAvailable;
    public int selectedArcadeDifficultyIndex;
    public bool showLibraryDifficultySelector;
    public List<string> libraryDifficultyLabels;
    public List<bool> libraryDifficultyAvailable;
    public int selectedLibraryDifficultyIndex;
    public List<TabSectionData> sections;
    public List<SongTimelineSectionData> songTimelineSections;
    public HashSet<int> latestDetectedPitches;
    public bool showSongSettings;
    public bool showGameplayAudioPopup;
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
    public bool showMiniGames;
    public bool showChartEditor;
    public int selectedMiniGameIndex;
    public MiniGameScreenSnapshot miniGameSnapshot;
    public bool showStartMenu;
    public bool showCharacterSelection;
    public bool characterSelectionOpenedFromStartup;
    public int selectedCharacterSelectionIndex;
    public int selectedHighwayCharacterIndex;
    public bool showLibraryLoadingOverlay;
    public string libraryLoadingStatusText;
    public float libraryLoadingProgressPercent;
    public bool libraryLoadingShowsProgress;
    public int selectedStartMenuStepIndex;
    public int selectedStartMenuModeIndex;
    public int selectedStartMenuArcadeSetupIndex;
    public int selectedStartMenuArcadeInputIndex;
    public bool startMenuArcadeGamepadMode;
    public bool showSongSelection;
    public bool songSelectionSongConfirmed;
    public bool showTrackSelection;
    public bool showToneLab;
    public bool showTuner;
    public bool showNotesDetectorTestMenu;
    public bool notesDetectorGameplayTestActive;
    public bool showNotesDetectorTestSelectionPopup;
    public bool showGameModes;
    public bool showHeroModeSettings;
    public int selectedGameModesIndex;
    public int selectedHeroModeSettingsIndex;
    public int selectedNotesDetectorTestIndex;
    public int selectedNotesDetectorCatalogIndex;
    public int selectedPauseActionIndex;
    public int selectedSongSettingsIndex;
    public string songLibraryListTitle;
    public string songLibraryListStatusText;
    public string songLibrarySearchQuery;
    public int songLibraryBrowseModeIndex;
    public List<string> availableSongNames;
    public List<string> availableSongSubtitles;
    public List<string> availableSongAlbums;
    public List<string> availableSongArtworkPaths;
    public List<string> availableSongDurationLabels;
    public List<string> availableSongDifficultyLabels;
    public List<bool> availableSongFavorited;
    public List<float> availableSongScores;
    public List<string> availableSongScoreTexts;
    public int selectedSongIndex;
    public string selectedLibrarySongSubtitle;
    public string selectedLibrarySongAlbum;
    public string selectedLibrarySongArtworkPath;
    public string selectedLibrarySongDifficultyLabel;
    public string selectedLibrarySongAudioLabel;
    public string selectedLibrarySongTuningLabel;
    public int selectedLibrarySongTrackCount;
    public string selectedLibraryHeroScoreText;
    public int selectedLibrarySongHeroBestHeartsRemaining;
    public int selectedLibrarySongHeroBestHeartsTotal;
    public bool selectedLibrarySongHasMp3;
    public bool selectedLibrarySongHasMidi;
    public bool selectedLibrarySongIsCurrent;
    public List<string> availableTrackNames;
    public List<string> availableTrackMetaTexts;
    public List<float> availableTrackScores;
    public List<string> availableTrackScoreTexts;
    public int selectedTrackIndex;
    public string currentSongDisplayName;
    public int songListScrollOffset;
    public float audioOffsetMs;
    public float tabSpeedOffsetPercent;
    public float songStartDelaySeconds;
    public float songVolumePercent;
    public float guitarVolumePercent;
    public int selectedGameplayAudioPopupIndex;
    public string songPlaybackAudioModeLabel;
    public bool songPlaybackUsesGeneratedMode;
    public bool stemMixSourceAvailable;
    public bool stemMixAvailable;
    public bool stemMixPlaybackEnabled;
    public bool stemGenerationRunning;
    public bool stemGeneratorReady;
    public bool stemGeneratorInstallAvailable;
    public bool stemGeneratorInstalling;
    public float stemGeneratorInstallProgressPercent;
    public string stemMixStatusText;
    public List<string> stemMixIds;
    public List<string> stemMixDisplayNames;
    public List<float> stemMixVolumePercents;
    public bool stemSmartDuckingEnabled;
    public bool generatedAudioTrackSelectionAvailable;
    public string generatedAudioTrackSelectionSummary;
    public bool showGeneratedAudioTrackSelectionPopup;
    public List<string> generatedAudioTrackNames;
    public List<bool> generatedAudioTrackEnabled;
    public int selectedGeneratedAudioTrackIndex;
    public bool showSongSettingsTrackSelectionPopup;
    public List<string> songSettingsTrackOptionNames;
    public int selectedSongSettingsTrackOptionIndex;
    public string selectedTrackDisplayName;
    public string selectedTrackTuningLabel;
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
    public bool songEndedAsGameOver;
    public int selectedSongEndActionIndex;
    public float currentTrackBestScorePercent;
    public int currentTrackBestScoreValue;
    public int currentTrackBestArcadeScore;
    public int currentTrackBestArcadeHeroScore;
    public int currentSongBestArcadeScore;
    public float currentTrackHeroBestScorePercent;
    public int currentTrackHeroBestScoreValue;
    public int currentTrackHeroBestHeartsRemaining;
    public int currentTrackHeroBestHeartsTotal;
    public string notesDetectorBackendLabel;
    public string notesDetectorStatusText;
    public string notesDetectorDetailText;
    public int notesDetectorCaptureSampleRate;
    public int notesDetectorInternalSampleRate;
    public bool notesDetectorResamplerToggleVisible;
    public string notesDetectorResamplerModeLabel;
    public List<string> notesDetectorAvailableInputDevices;
    public int selectedNotesDetectorInputDeviceIndex;
    public List<string> notesDetectorPresetLabels;
    public int selectedNotesDetectorPresetIndex;
    public string notesDetectorSelectedPresetLabel;
    public List<NativeDetectorSettingSnapshot> notesDetectorSettings;
    public string notesDetectorFastNotesText;
    public string notesDetectorAiNotesText;
    public string notesDetectorLatestAcceptanceSourceText;
    public List<string> notesDetectorAvailableTestCategories;
    public List<string> notesDetectorAvailableTestNames;
    public List<string> notesDetectorAvailableTestDescriptions;
    public bool showNotesDetectorRoutinePopup;
    public string notesDetectorRoutineInstructionText;
    public string notesDetectorRoutineTargetText;
    public string notesDetectorRoutineStatusText;
    public string notesDetectorRoutineProgressText;
    public List<string> notesDetectorRoutineTabRows;
    public List<int> notesDetectorRoutineTabRowStates;
    public bool notesDetectorRoutineStatusOk;
    public bool notesDetectorRoutineCompleted;
    public bool showStartupTuningReminder;
    public bool showGlobalSettings;
    public bool showBackgroundMoodSetter;
    public bool showDiagnosticsConsentPopup;
    public int selectedDiagnosticsConsentIndex;
    public bool showBugReportScreen;
    public int selectedBugReportActionIndex;
    public string bugReportDescription;
    public string bugReportStatus;
    public bool bugReportSending;
    public bool showBugReportSentPopup;
    public int selectedBugReportSentActionIndex;
    public string bugReportSentMessage;
    public bool diagnosticsUploadEnabled;
    public bool diagnosticsUploadEndpointConfigured;
    public string songsFolderMenuValueLabel;
    public string effectsFolderMenuValueLabel;
    public int selectedGlobalSettingsTopIndex;
    public int selectedGlobalSettingsItemIndex;
    public int selectedBackgroundMoodSettingIndex;
    public string activeGlobalSettingsCategory;
    public string backgroundMoodSetterStatusText;
    public bool globalSettingsTransparentBackground;
    public bool showGlobalSettingsSelectionPopup;
    public string globalSettingsSelectionPopupEyebrow;
    public string globalSettingsSelectionPopupTitle;
    public string globalSettingsSelectionPopupSummary;
    public List<string> globalSettingsSelectionPopupOptionNames;
    public List<string> globalSettingsSelectionPopupOptionStates;
    public List<string> globalSettingsSelectionPopupOptionActions;
    public int selectedGlobalSettingsSelectionPopupIndex;
    public bool showGameplayHudPreviewInMenus;
    public bool showGameplayTimeline;
    public List<RuntimeSettingSectionSnapshot> runtimeSettingsSections;
    public List<RuntimeSettingSectionSnapshot> backgroundMoodSetterSections;
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
