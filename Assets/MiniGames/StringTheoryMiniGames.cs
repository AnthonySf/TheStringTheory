using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class MiniGameMenuEntrySnapshot
{
    public string id = string.Empty;
    public string title = string.Empty;
    public string subtitle = string.Empty;
    public int highScore;
    public bool selected;
}

[Serializable]
public sealed class MiniGameScreenSnapshot
{
    public List<MiniGameMenuEntrySnapshot> entries = new List<MiniGameMenuEntrySnapshot>();
    public int selectedIndex;
    public int selectedPauseActionIndex;
    public FightClubSetupSnapshot fightClubSetup = new FightClubSetupSnapshot();
    public FightClubRunSettingsSnapshot fightClubRunSettings = new FightClubRunSettingsSnapshot();
    public bool fightClubActive;
    public FightClubMiniGameSnapshot fightClub = new FightClubMiniGameSnapshot();
}

[Serializable]
public sealed class FightClubSetupSnapshot
{
    public bool visible;
    public bool randomMode = true;
    public int selectedLevelIndex;
    public int sourceMode;
    public bool canStart = true;
    public string statusLabel = string.Empty;
    public List<FightClubLevelSnapshot> levels = new List<FightClubLevelSnapshot>();
    public List<FightClubChordGroupSnapshot> groups = new List<FightClubChordGroupSnapshot>();
    public List<FightClubChordOptionSnapshot> availableChords = new List<FightClubChordOptionSnapshot>();
    public List<FightClubChordOptionSnapshot> playableChords = new List<FightClubChordOptionSnapshot>();
    public List<FightClubSongChordSourceSnapshot> songs = new List<FightClubSongChordSourceSnapshot>();
}

[Serializable]
public sealed class FightClubLevelSnapshot
{
    public int index;
    public string id = string.Empty;
    public string title = string.Empty;
    public string name = string.Empty;
    public string subtitle = string.Empty;
    public int chordCount;
    public int unlockScore;
    public int highScore;
    public bool unlocked;
    public bool selected;
}

[Serializable]
public sealed class FightClubRunSettingsSnapshot
{
    public bool visible;
    public bool activeRun;
    public bool canStart = true;
    public int chordLeniencyIndex = 1;
    public string chordLeniencyLabel = "Normal";
    public string chordLeniencyDescription = "All chord notes are required with the standard beat window.";
    public float beatIntervalSeconds = 1.12f;
    public int tempoBpm = 54;
    public float countdownSeconds = 3f;
    public int maxFailedRounds = 3;
    public int metronomeSoundIndex = (int)StringTheoryMetronomeSound.Drums;
    public string metronomeSoundLabel = "Drums";
    public int chordPreviewInstrumentIndex = (int)StringTheoryChordPreviewInstrument.ElectricGuitar;
    public string chordPreviewInstrumentLabel = "Electric";
}

[Serializable]
public sealed class FightClubChordGroupSnapshot
{
    public string id = string.Empty;
    public string name = string.Empty;
    public string subtitle = string.Empty;
    public int chordCount;
    public bool selected;
}

[Serializable]
public sealed class FightClubChordOptionSnapshot
{
    public string id = string.Empty;
    public string name = string.Empty;
    public string sourceLabel = string.Empty;
    public int difficulty;
    public bool selected;
}

[Serializable]
public sealed class FightClubSongChordSourceSnapshot
{
    public string songKey = string.Empty;
    public string displayName = string.Empty;
    public string artist = string.Empty;
    public string artworkPath = string.Empty;
    public int matchedChordCount;
    public bool selected;
}

[Serializable]
public sealed class FightClubMiniGameSnapshot
{
    public bool active;
    public bool ended;
    public bool endedByLoss;
    public bool highScoreEnabled;
    public string title = "Fight Club";
    public string phaseLabel = "Ready";
    public string statusLabel = "Select Start";
    public string countdownLabel = string.Empty;
    public int round;
    public int score;
    public int highScore;
    public int streak;
    public int bestStreak;
    public int misses;
    public int failedRounds;
    public int maxFailedRounds = 3;
    public float beatProgress01;
    public float beatIntervalSeconds = 1.12f;
    public int metronomeSoundIndex = (int)StringTheoryMetronomeSound.Drums;
    public int chordPreviewInstrumentIndex = (int)StringTheoryChordPreviewInstrument.ElectricGuitar;
    public int activeChordIndex = -1;
    public bool opponentPreviewActive;
    public int opponentActiveChordIndex = -1;
    public int opponentChordSoundSerial;
    public int opponentChordSoundIndex = -1;
    public List<FightClubChordSnapshot> chords = new List<FightClubChordSnapshot>();
    public List<FightClubChordResultSnapshot> chordResults = new List<FightClubChordResultSnapshot>();
}

[Serializable]
public sealed class FightClubChordSnapshot
{
    public string name = string.Empty;
    public int[] fretsLowToHigh = Array.Empty<int>();
    public int[] fingersLowToHigh = Array.Empty<int>();
    public int[] expectedMidis = Array.Empty<int>();
    public List<FightClubBarreSnapshot> barres = new List<FightClubBarreSnapshot>();
    public int status;
    public bool active;
}

[Serializable]
public sealed class FightClubChordResultSnapshot
{
    public string id = string.Empty;
    public string name = string.Empty;
    public int hits;
    public int misses;
}

[Serializable]
public sealed class FightClubBarreSnapshot
{
    public int fret;
    public int startString;
    public int endString;
    public int finger;
}

public readonly struct MiniGameExpectedNote
{
    public readonly int midi;
    public readonly int stringIndex;
    public readonly int fret;
    public readonly int openMidi;
    public readonly int noteId;
    public readonly int chordId;
    public readonly float noteTime;

    public MiniGameExpectedNote(int midi, int stringIndex, int fret, int openMidi, int noteId, int chordId, float noteTime)
    {
        this.midi = midi;
        this.stringIndex = stringIndex;
        this.fret = fret;
        this.openMidi = openMidi;
        this.noteId = noteId;
        this.chordId = chordId;
        this.noteTime = noteTime;
    }
}

public readonly struct MiniGameDetectorHintWindow
{
    public readonly float startTime;
    public readonly float endTime;
    public readonly int[] pitches;
    public readonly MiniGameExpectedNote[] expectedNotes;

    public MiniGameDetectorHintWindow(float startTime, float endTime, int[] pitches, MiniGameExpectedNote[] expectedNotes)
    {
        this.startTime = startTime;
        this.endTime = endTime;
        this.pitches = pitches ?? Array.Empty<int>();
        this.expectedNotes = expectedNotes ?? Array.Empty<MiniGameExpectedNote>();
    }
}

public sealed class FightClubRunSettings
{
    public const int StrictLeniency = 0;
    public const int NormalLeniency = 1;
    public const int ForgivingLeniency = 2;
    public const int MinTempoBpm = 40;
    public const int MaxTempoBpm = 180;

    public int chordLeniencyIndex = NormalLeniency;
    public float beatIntervalSeconds = 1.12f;
    public float countdownSeconds = 3f;
    public int maxFailedRounds = 3;
    public int metronomeSoundIndex = (int)StringTheoryMetronomeSound.Drums;
    public int chordPreviewInstrumentIndex = (int)StringTheoryChordPreviewInstrument.ElectricGuitar;

    public static FightClubRunSettings CreateDefault()
    {
        return new FightClubRunSettings();
    }

    public FightClubRunSettings Clone()
    {
        return new FightClubRunSettings
        {
            chordLeniencyIndex = Mathf.Clamp(chordLeniencyIndex, StrictLeniency, ForgivingLeniency),
            beatIntervalSeconds = Mathf.Clamp(beatIntervalSeconds, 0.66f, 2.5f),
            countdownSeconds = Mathf.Clamp(Mathf.Round(countdownSeconds), 1f, 8f),
            maxFailedRounds = Mathf.Clamp(maxFailedRounds, 1, 10),
            metronomeSoundIndex = (int)StringTheoryMetronome.NormalizeSoundIndex(metronomeSoundIndex),
            chordPreviewInstrumentIndex = (int)StringTheoryChordAudioPlayer.NormalizeInstrumentIndex(chordPreviewInstrumentIndex)
        };
    }

    public int GetTempoBpm()
    {
        float interval = Mathf.Clamp(beatIntervalSeconds, 0.66f, 2.5f);
        return Mathf.Clamp(Mathf.RoundToInt(60f / interval), MinTempoBpm, MaxTempoBpm);
    }

    public void SetTempoBpm(int tempoBpm)
    {
        int clamped = Mathf.Clamp(tempoBpm, MinTempoBpm, MaxTempoBpm);
        beatIntervalSeconds = Mathf.Round((60f / clamped) * 1000f) / 1000f;
    }

    public StringTheoryMetronomeSound GetMetronomeSound()
    {
        return StringTheoryMetronome.NormalizeSoundIndex(metronomeSoundIndex);
    }

    public StringTheoryChordPreviewInstrument GetChordPreviewInstrument()
    {
        return StringTheoryChordAudioPlayer.NormalizeInstrumentIndex(chordPreviewInstrumentIndex);
    }

    public string GetLeniencyLabel()
    {
        switch (Mathf.Clamp(chordLeniencyIndex, StrictLeniency, ForgivingLeniency))
        {
            case StrictLeniency:
                return "Strict";
            case ForgivingLeniency:
                return "Forgiving";
            default:
                return "Normal";
        }
    }

    public string GetLeniencyDescription()
    {
        switch (Mathf.Clamp(chordLeniencyIndex, StrictLeniency, ForgivingLeniency))
        {
            case StrictLeniency:
                return "Tighter timing. Every chord note must be detected.";
            case ForgivingLeniency:
                return "Wider timing, and large chords can pass with one missing note.";
            default:
                return "Standard timing. Every chord note must be detected.";
        }
    }

    public float GetEarlyWindowSeconds()
    {
        switch (Mathf.Clamp(chordLeniencyIndex, StrictLeniency, ForgivingLeniency))
        {
            case StrictLeniency:
                return 0.18f;
            case ForgivingLeniency:
                return 0.30f;
            default:
                return 0.22f;
        }
    }

    public float GetLateWindowSeconds()
    {
        switch (Mathf.Clamp(chordLeniencyIndex, StrictLeniency, ForgivingLeniency))
        {
            case StrictLeniency:
                return 0.22f;
            case ForgivingLeniency:
                return 0.38f;
            default:
                return 0.28f;
        }
    }

    public int GetAllowedMissingNotes(int expectedNoteCount)
    {
        if (Mathf.Clamp(chordLeniencyIndex, StrictLeniency, ForgivingLeniency) != ForgivingLeniency)
            return 0;

        return expectedNoteCount >= 4 ? 1 : 0;
    }
}

public sealed class MiniGameManager
{
    private const string SaveFileName = "minigames_save.json";

    private readonly FightClubMiniGame fightClub = new FightClubMiniGame();
    private MiniGameSaveData saveData = new MiniGameSaveData();
    private string savePath = string.Empty;
    private bool fightClubSetupVisible;
    private bool fightClubRunSettingsVisible;
    private bool fightClubRandomMode = true;
    private int fightClubSetupSourceMode;
    private int fightClubSelectedLevelIndex;
    private readonly HashSet<string> fightClubSelectedGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> fightClubCheckedChordIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> fightClubPlayableChordIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> fightClubSelectedSongKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<string>> fightClubSongChordCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, int>> fightClubSongTransitionCache = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
    private List<SongLibraryEntry> fightClubSongSourceCache;

    public bool IsFightClubActive => fightClub.IsActive;
    public bool IsFightClubEnded => fightClub.IsEnded;
    public bool IsFightClubSetupVisible => fightClubSetupVisible;
    public bool IsFightClubRunSettingsVisible => fightClubRunSettingsVisible;
    public bool IsAnyGameActive => fightClub.IsActive;
    public bool DetectorHintDirty => fightClub.DetectorHintDirty;
    public float DetectorTimelineTime => fightClub.DetectorTimelineTime;

    public void Initialize(string persistentRoot)
    {
        string root = string.IsNullOrWhiteSpace(persistentRoot) ? Application.persistentDataPath : persistentRoot;
        savePath = Path.Combine(root, SaveFileName);
        Load();
        saveData.fightClub.highestUnlockedLevelIndex = Mathf.Clamp(saveData.fightClub.highestUnlockedLevelIndex, 0, Mathf.Max(0, FightClubChordCatalog.Levels.Length - 1));
        fightClubSelectedLevelIndex = Mathf.Clamp(saveData.fightClub.selectedLevelIndex, 0, Mathf.Max(0, FightClubChordCatalog.Levels.Length - 1));
        if (fightClubSelectedLevelIndex > saveData.fightClub.highestUnlockedLevelIndex)
            fightClubSelectedLevelIndex = saveData.fightClub.highestUnlockedLevelIndex;
        EnsureFightClubSaveShape();
        fightClub.SetHighScore(GetFightClubLevelHighScore(fightClubSelectedLevelIndex));
    }

    public MiniGameScreenSnapshot BuildSnapshot(int selectedIndex, int selectedPauseActionIndex)
    {
        var snapshot = new MiniGameScreenSnapshot
        {
            selectedIndex = selectedIndex,
            selectedPauseActionIndex = selectedPauseActionIndex,
            fightClubSetup = BuildFightClubSetupSnapshot(),
            fightClubRunSettings = BuildFightClubRunSettingsSnapshot(),
            fightClubActive = fightClub.IsActive,
            fightClub = fightClub.BuildSnapshot()
        };

        snapshot.entries.Add(new MiniGameMenuEntrySnapshot
        {
            id = "fight-club",
            title = "Fight Club",
            subtitle = "Play three chords on the beat. Each round gets tighter.",
            highScore = 0,
            selected = selectedIndex == 0
        });

        return snapshot;
    }

    public void StartFightClub()
    {
        fightClub.SetHighScore(0);
        fightClub.Start(FightClubChordCatalog.Chords, -1, null, GetFightClubRunSettings());
        fightClubSetupVisible = false;
        fightClubRunSettingsVisible = false;
    }

    public void OpenFightClubSetup()
    {
        fightClubSetupVisible = true;
        fightClubRunSettingsVisible = false;
        if (fightClubPlayableChordIds.Count == 0)
        {
            fightClubRandomMode = true;
            fightClubSetupSourceMode = 0;
        }
    }

    public void CloseFightClubSetup()
    {
        fightClubSetupVisible = false;
    }

    public void OpenFightClubRunSettings()
    {
        if (!fightClub.IsActive && !CanStartConfiguredFightClub())
            return;

        fightClubRunSettingsVisible = true;
        fightClubSetupVisible = false;
    }

    public void CloseFightClubRunSettings()
    {
        fightClubRunSettingsVisible = false;
        if (!fightClub.IsActive)
            fightClubSetupVisible = true;
    }

    public void CycleFightClubChordLeniency(int delta)
    {
        EnsureFightClubSaveShape();
        saveData.fightClub.settings.chordLeniencyIndex = Mathf.Clamp(
            saveData.fightClub.settings.chordLeniencyIndex + delta,
            FightClubRunSettings.StrictLeniency,
            FightClubRunSettings.ForgivingLeniency);
        Save();
        ApplyFightClubSettingsToActiveRun();
    }

    public void AdjustFightClubBeatInterval(float deltaSeconds)
    {
        EnsureFightClubSaveShape();
        saveData.fightClub.settings.beatIntervalSeconds = Mathf.Round(Mathf.Clamp(saveData.fightClub.settings.beatIntervalSeconds + deltaSeconds, 0.66f, 2.5f) * 100f) / 100f;
        Save();
        ApplyFightClubSettingsToActiveRun();
    }

    public void AdjustFightClubTempoBpm(int deltaBpm)
    {
        EnsureFightClubSaveShape();
        FightClubRunSettings settings = GetFightClubRunSettings();
        settings.SetTempoBpm(settings.GetTempoBpm() + deltaBpm);
        saveData.fightClub.settings.beatIntervalSeconds = settings.beatIntervalSeconds;
        Save();
        ApplyFightClubSettingsToActiveRun();
    }

    public void CycleFightClubMetronomeSound(int delta)
    {
        EnsureFightClubSaveShape();
        int count = Enum.GetValues(typeof(StringTheoryMetronomeSound)).Length;
        int current = (int)StringTheoryMetronome.NormalizeSoundIndex(saveData.fightClub.settings.metronomeSoundIndex);
        saveData.fightClub.settings.metronomeSoundIndex = Mod(current + delta, count);
        Save();
        ApplyFightClubSettingsToActiveRun();
    }

    public void CycleFightClubChordPreviewInstrument(int delta)
    {
        EnsureFightClubSaveShape();
        int count = Enum.GetValues(typeof(StringTheoryChordPreviewInstrument)).Length;
        int current = (int)StringTheoryChordAudioPlayer.NormalizeInstrumentIndex(saveData.fightClub.settings.chordPreviewInstrumentIndex);
        saveData.fightClub.settings.chordPreviewInstrumentIndex = Mod(current + delta, count);
        Save();
        ApplyFightClubSettingsToActiveRun();
    }

    public void AdjustFightClubCountdown(float deltaSeconds)
    {
        EnsureFightClubSaveShape();
        saveData.fightClub.settings.countdownSeconds = Mathf.Clamp(Mathf.Round(saveData.fightClub.settings.countdownSeconds + deltaSeconds), 1f, 8f);
        Save();
        ApplyFightClubSettingsToActiveRun();
    }

    public void AdjustFightClubMaxFailedRounds(int delta)
    {
        EnsureFightClubSaveShape();
        saveData.fightClub.settings.maxFailedRounds = Mathf.Clamp(saveData.fightClub.settings.maxFailedRounds + delta, 1, 10);
        Save();
        ApplyFightClubSettingsToActiveRun();
    }

    private static int Mod(int value, int divisor)
    {
        if (divisor <= 0)
            return 0;

        int result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    public void SetFightClubSetupRandomMode(bool enabled)
    {
        fightClubRandomMode = enabled;
        if (!enabled && fightClubPlayableChordIds.Count == 0 && fightClubCheckedChordIds.Count == 0)
        {
            FightClubChordGroupDefinition firstGroup = FightClubChordCatalog.Groups.FirstOrDefault();
            if (firstGroup != null)
            {
                fightClubSelectedGroupIds.Add(firstGroup.Id);
                for (int i = 0; i < firstGroup.Chords.Length; i++)
                    fightClubCheckedChordIds.Add(firstGroup.Chords[i].Id);
            }
        }
    }

    public void SelectFightClubSetupLevel(int index)
    {
        int maxIndex = Mathf.Max(0, FightClubChordCatalog.Levels.Length - 1);
        int clamped = Mathf.Clamp(index, 0, maxIndex);
        if (clamped > saveData.fightClub.highestUnlockedLevelIndex)
            return;

        fightClubSelectedLevelIndex = clamped;
        saveData.fightClub.selectedLevelIndex = fightClubSelectedLevelIndex;
        fightClubRandomMode = true;
        Save();
    }

    public void SetFightClubSetupSourceMode(int mode)
    {
        fightClubSetupSourceMode = Mathf.Clamp(mode, 0, 1);
    }

    public void ToggleFightClubSetupGroup(string groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
            return;

        FightClubChordGroupDefinition group = FightClubChordCatalog.Groups
            .FirstOrDefault(candidate => string.Equals(candidate.Id, groupId, StringComparison.OrdinalIgnoreCase));
        if (group == null)
            return;

        if (fightClubSelectedGroupIds.Contains(group.Id))
        {
            fightClubSelectedGroupIds.Remove(group.Id);
            for (int i = 0; i < group.Chords.Length; i++)
                fightClubCheckedChordIds.Remove(group.Chords[i].Id);
        }
        else
        {
            fightClubSelectedGroupIds.Add(group.Id);
            for (int i = 0; i < group.Chords.Length; i++)
                fightClubCheckedChordIds.Add(group.Chords[i].Id);
        }

        fightClubSetupSourceMode = 0;
        fightClubRandomMode = false;
    }

    public void SelectAllFightClubSetupGroups()
    {
        fightClubSelectedGroupIds.Clear();
        fightClubCheckedChordIds.Clear();
        for (int i = 0; i < FightClubChordCatalog.Groups.Length; i++)
        {
            FightClubChordGroupDefinition group = FightClubChordCatalog.Groups[i];
            fightClubSelectedGroupIds.Add(group.Id);
            for (int chordIndex = 0; chordIndex < group.Chords.Length; chordIndex++)
                fightClubCheckedChordIds.Add(group.Chords[chordIndex].Id);
        }

        fightClubSetupSourceMode = 0;
        fightClubRandomMode = false;
    }

    public void ToggleFightClubSetupChord(string chordId)
    {
        FightClubChordDefinition chord = FightClubChordCatalog.FindById(chordId);
        if (chord == null)
            return;

        if (!fightClubCheckedChordIds.Add(chord.Id))
            fightClubCheckedChordIds.Remove(chord.Id);

        fightClubRandomMode = false;
    }

    public void AddCheckedFightClubChordsToPlayable()
    {
        bool added = false;
        foreach (FightClubChordDefinition chord in GetFightClubAvailableSetupChords())
        {
            if (chord != null && fightClubCheckedChordIds.Contains(chord.Id))
                added |= fightClubPlayableChordIds.Add(chord.Id);
        }

        if (added)
            fightClubRandomMode = false;
    }

    public void RemoveFightClubPlayableChord(string chordId)
    {
        if (!string.IsNullOrWhiteSpace(chordId))
            fightClubPlayableChordIds.Remove(chordId.Trim());

        if (fightClubPlayableChordIds.Count == 0)
            fightClubRandomMode = true;
    }

    public void ClearFightClubPlayableChords()
    {
        fightClubPlayableChordIds.Clear();
        fightClubRandomMode = true;
    }

    public void ToggleFightClubSetupSong(string songKey)
    {
        if (string.IsNullOrWhiteSpace(songKey))
            return;

        EnsureFightClubSongSourceCache();
        SongLibraryEntry entry = fightClubSongSourceCache?.FirstOrDefault(song =>
            string.Equals(BuildFightClubSongKey(song), songKey, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            return;

        bool selected = fightClubSelectedSongKeys.Contains(songKey);
        if (selected)
        {
            fightClubSelectedSongKeys.Remove(songKey);
        }
        else
        {
            fightClubSelectedSongKeys.Add(songKey);
            foreach (string chordId in GetFightClubSongChordIds(entry))
                fightClubCheckedChordIds.Add(chordId);
        }

        fightClubSetupSourceMode = 1;
        fightClubRandomMode = false;
    }

    public void StartConfiguredFightClub()
    {
        int activeLevelIndex = -1;
        FightClubChordDefinition[] pool;
        if (fightClubRandomMode)
        {
            FightClubLevelDefinition level = GetSelectedFightClubLevel();
            pool = FightClubChordCatalog.ResolveIds(level?.ChordIds);
            activeLevelIndex = fightClubSelectedLevelIndex;
        }
        else
        {
            pool = FightClubChordCatalog.ResolveIds(fightClubPlayableChordIds);
        }

        if (pool.Length == 0)
            return;

        fightClub.SetHighScore(activeLevelIndex >= 0 ? GetFightClubLevelHighScore(activeLevelIndex) : 0);
        fightClub.Start(pool, activeLevelIndex, fightClubRandomMode ? null : BuildFightClubSelectedSongTransitionProfile(pool), GetFightClubRunSettings());
        fightClubSetupVisible = false;
        fightClubRunSettingsVisible = false;
    }

    public void StopFightClub()
    {
        if (!fightClub.IsActive)
            return;

        CaptureFightClubScore();
        fightClub.Stop();
        Save();
    }

    public void EndFightClub()
    {
        if (!fightClub.IsActive)
            return;

        CaptureFightClubScore();
        fightClub.End();
        Save();
    }

    public void RestartFightClub()
    {
        CaptureFightClubScore();
        int activeLevelIndex = fightClub.ActiveLevelIndex;
        fightClub.SetHighScore(activeLevelIndex >= 0 ? GetFightClubLevelHighScore(activeLevelIndex) : 0);
        fightClub.Start(null, activeLevelIndex, null, GetFightClubRunSettings());
        Save();
    }

    public void Update(float deltaTime, HashSet<int> detectedPitches)
    {
        if (!fightClub.IsActive)
            return;

        int previousHighScore = fightClub.HighScore;
        bool wasEnded = fightClub.IsEnded;
        fightClub.Update(deltaTime, detectedPitches);
        if (fightClub.HighScore > previousHighScore || IsFightClubUnlockReady() || (!wasEnded && fightClub.IsEnded))
        {
            CaptureFightClubScore();
            Save();
        }
    }

    public MiniGameDetectorHintWindow[] BuildDetectorHintWindows()
    {
        return fightClub.BuildDetectorHintWindows();
    }

    public void ClearDetectorHintDirty()
    {
        fightClub.ClearDetectorHintDirty();
    }

    private void CaptureFightClubScore()
    {
        int activeLevelIndex = fightClub.ActiveLevelIndex;
        if (activeLevelIndex >= 0)
        {
            FightClubLevelScoreSaveData levelScore = GetFightClubLevelScoreData(activeLevelIndex, create: true);
            if (levelScore != null)
            {
                levelScore.highScore = Mathf.Max(levelScore.highScore, fightClub.Score);
                levelScore.bestStreak = Mathf.Max(levelScore.bestStreak, fightClub.BestStreak);
            }
        }

        saveData.fightClub.lastScore = fightClub.Score;
        TryUnlockNextFightClubLevel();
    }

    private FightClubSetupSnapshot BuildFightClubSetupSnapshot()
    {
        var snapshot = new FightClubSetupSnapshot
        {
            visible = fightClubSetupVisible,
            randomMode = fightClubRandomMode,
            selectedLevelIndex = fightClubSelectedLevelIndex,
            sourceMode = fightClubSetupSourceMode
        };

        for (int i = 0; i < FightClubChordCatalog.Levels.Length; i++)
        {
            FightClubLevelDefinition level = FightClubChordCatalog.Levels[i];
            snapshot.levels.Add(new FightClubLevelSnapshot
            {
                index = i,
                id = level.Id,
                title = level.Title,
                name = level.Name,
                subtitle = level.Subtitle,
                chordCount = level.ChordIds?.Length ?? 0,
                unlockScore = i > 0 ? FightClubChordCatalog.Levels[i - 1].UnlockScore : 0,
                highScore = GetFightClubLevelHighScore(i),
                unlocked = i <= saveData.fightClub.highestUnlockedLevelIndex,
                selected = i == fightClubSelectedLevelIndex
            });
        }

        for (int i = 0; i < FightClubChordCatalog.Groups.Length; i++)
        {
            FightClubChordGroupDefinition group = FightClubChordCatalog.Groups[i];
            snapshot.groups.Add(new FightClubChordGroupSnapshot
            {
                id = group.Id,
                name = group.Name,
                subtitle = group.Subtitle,
                chordCount = group.Chords.Length,
                selected = fightClubSelectedGroupIds.Contains(group.Id)
            });
        }

        foreach (FightClubChordDefinition chord in GetFightClubAvailableSetupChords())
            snapshot.availableChords.Add(BuildChordOptionSnapshot(chord, GetChordSourceLabel(chord), fightClubCheckedChordIds.Contains(chord.Id)));

        foreach (FightClubChordDefinition chord in FightClubChordCatalog.ResolveIds(fightClubPlayableChordIds)
                     .OrderBy(chord => chord.Difficulty)
                     .ThenBy(chord => chord.Name, StringComparer.OrdinalIgnoreCase))
        {
            snapshot.playableChords.Add(BuildChordOptionSnapshot(chord, "Game list", true));
        }

        EnsureFightClubSongSourceCache();
        if (fightClubSongSourceCache != null)
        {
            foreach (SongLibraryEntry song in fightClubSongSourceCache)
            {
                if (song == null)
                    continue;

                string songKey = BuildFightClubSongKey(song);
                int matchedCount = 0;
                if (fightClubSelectedSongKeys.Contains(songKey))
                    matchedCount = GetFightClubSongChordIds(song).Count;

                snapshot.songs.Add(new FightClubSongChordSourceSnapshot
                {
                    songKey = songKey,
                    displayName = string.IsNullOrWhiteSpace(song.DisplayName) ? Path.GetFileName(song.SongDirectory) : song.DisplayName,
                    artist = song.Artist ?? string.Empty,
                    artworkPath = song.ArtworkPath ?? string.Empty,
                    matchedChordCount = matchedCount,
                    selected = fightClubSelectedSongKeys.Contains(songKey)
                });
            }
        }

        snapshot.canStart = fightClubRandomMode || snapshot.playableChords.Count > 0;
        snapshot.statusLabel = fightClubRandomMode
            ? GetSelectedLevelStatusLabel()
            : snapshot.playableChords.Count > 0
                ? $"{snapshot.playableChords.Count.ToString(CultureInfo.InvariantCulture)} chords ready."
                : "Add at least one chord to start.";
        return snapshot;
    }

    private FightClubRunSettingsSnapshot BuildFightClubRunSettingsSnapshot()
    {
        FightClubRunSettings settings = GetFightClubRunSettings();
        return new FightClubRunSettingsSnapshot
        {
            visible = fightClubRunSettingsVisible,
            activeRun = fightClub.IsActive,
            canStart = fightClub.IsActive || CanStartConfiguredFightClub(),
            chordLeniencyIndex = settings.chordLeniencyIndex,
            chordLeniencyLabel = settings.GetLeniencyLabel(),
            chordLeniencyDescription = settings.GetLeniencyDescription(),
            beatIntervalSeconds = settings.beatIntervalSeconds,
            tempoBpm = settings.GetTempoBpm(),
            countdownSeconds = settings.countdownSeconds,
            maxFailedRounds = settings.maxFailedRounds,
            metronomeSoundIndex = settings.metronomeSoundIndex,
            metronomeSoundLabel = StringTheoryMetronome.GetSoundLabel(settings.GetMetronomeSound()),
            chordPreviewInstrumentIndex = settings.chordPreviewInstrumentIndex,
            chordPreviewInstrumentLabel = StringTheoryChordAudioPlayer.GetInstrumentLabel(settings.GetChordPreviewInstrument())
        };
    }

    private bool CanStartConfiguredFightClub()
    {
        return fightClubRandomMode || fightClubPlayableChordIds.Count > 0;
    }

    private FightClubLevelDefinition GetSelectedFightClubLevel()
    {
        if (FightClubChordCatalog.Levels == null || FightClubChordCatalog.Levels.Length == 0)
            return null;

        fightClubSelectedLevelIndex = Mathf.Clamp(fightClubSelectedLevelIndex, 0, FightClubChordCatalog.Levels.Length - 1);
        if (fightClubSelectedLevelIndex > saveData.fightClub.highestUnlockedLevelIndex)
            fightClubSelectedLevelIndex = saveData.fightClub.highestUnlockedLevelIndex;

        return FightClubChordCatalog.Levels[fightClubSelectedLevelIndex];
    }

    private string GetSelectedLevelStatusLabel()
    {
        FightClubLevelDefinition level = GetSelectedFightClubLevel();
        if (level == null)
            return "Select a level to start.";

        return $"{level.Title}: {level.Name} selected.";
    }

    private void TryUnlockNextFightClubLevel()
    {
        int activeLevelIndex = fightClub.ActiveLevelIndex;
        if (activeLevelIndex < 0 || FightClubChordCatalog.Levels == null || FightClubChordCatalog.Levels.Length == 0)
            return;

        if (activeLevelIndex != saveData.fightClub.highestUnlockedLevelIndex)
            return;

        int nextIndex = activeLevelIndex + 1;
        if (nextIndex >= FightClubChordCatalog.Levels.Length)
            return;

        FightClubLevelDefinition currentLevel = FightClubChordCatalog.Levels[activeLevelIndex];
        if (fightClub.Score < Mathf.Max(1, currentLevel.UnlockScore))
            return;

        saveData.fightClub.highestUnlockedLevelIndex = nextIndex;
        if (saveData.fightClub.selectedLevelIndex > nextIndex)
            saveData.fightClub.selectedLevelIndex = nextIndex;
    }

    private bool IsFightClubUnlockReady()
    {
        int activeLevelIndex = fightClub.ActiveLevelIndex;
        if (activeLevelIndex < 0 || FightClubChordCatalog.Levels == null || FightClubChordCatalog.Levels.Length == 0)
            return false;

        if (activeLevelIndex != saveData.fightClub.highestUnlockedLevelIndex)
            return false;

        int nextIndex = activeLevelIndex + 1;
        if (nextIndex >= FightClubChordCatalog.Levels.Length)
            return false;

        FightClubLevelDefinition currentLevel = FightClubChordCatalog.Levels[activeLevelIndex];
        return fightClub.Score >= Mathf.Max(1, currentLevel.UnlockScore);
    }

    private FightClubChordOptionSnapshot BuildChordOptionSnapshot(FightClubChordDefinition chord, string sourceLabel, bool selected)
    {
        return new FightClubChordOptionSnapshot
        {
            id = chord?.Id ?? string.Empty,
            name = chord?.Name ?? string.Empty,
            sourceLabel = sourceLabel ?? string.Empty,
            difficulty = chord?.Difficulty ?? 1,
            selected = selected
        };
    }

    private IEnumerable<FightClubChordDefinition> GetFightClubAvailableSetupChords()
    {
        if (fightClubSetupSourceMode == 1)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            EnsureFightClubSongSourceCache();
            if (fightClubSongSourceCache != null)
            {
                foreach (SongLibraryEntry song in fightClubSongSourceCache)
                {
                    string songKey = BuildFightClubSongKey(song);
                    if (!fightClubSelectedSongKeys.Contains(songKey))
                        continue;

                    foreach (string chordId in GetFightClubSongChordIds(song))
                        ids.Add(chordId);
                }
            }

            return FightClubChordCatalog.ResolveIds(ids)
                .OrderBy(chord => chord.Difficulty)
                .ThenBy(chord => chord.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (fightClubSelectedGroupIds.Count == 0)
        {
            return FightClubChordCatalog.Chords
                .OrderBy(chord => chord.Difficulty)
                .ThenBy(chord => chord.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return FightClubChordCatalog.Groups
            .Where(group => fightClubSelectedGroupIds.Contains(group.Id))
            .SelectMany(group => group.Chords)
            .GroupBy(chord => chord.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(chord => chord.Difficulty)
            .ThenBy(chord => chord.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string GetChordSourceLabel(FightClubChordDefinition chord)
    {
        if (chord == null)
            return string.Empty;

        if (fightClubSetupSourceMode == 1)
            return "Song chord";

        FightClubChordGroupDefinition group = FightClubChordCatalog.Groups.FirstOrDefault(candidate =>
            candidate.Chords.Any(groupChord => string.Equals(groupChord.Id, chord.Id, StringComparison.OrdinalIgnoreCase)));
        return group?.Name ?? "Catalog";
    }

    private void EnsureFightClubSongSourceCache()
    {
        if (fightClubSongSourceCache != null)
            return;

        try
        {
            fightClubSongSourceCache = SongLibraryService.GetAvailableSongs()
                .Where(song => song != null &&
                               song.LibraryType == SongLibraryType.Guitar &&
                               !string.IsNullOrWhiteSpace(song.PrimaryNotationPath) &&
                               song.PrimaryNotationKind != SongNotationSourceKind.None)
                .OrderBy(song => song.DisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(song => song.Artist ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MiniGames] Failed to load Fight Club song chord sources: {ex.Message}");
            fightClubSongSourceCache = new List<SongLibraryEntry>();
        }
    }

    private Dictionary<string, int> BuildFightClubSelectedSongTransitionProfile(FightClubChordDefinition[] pool)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (fightClubSelectedSongKeys.Count == 0 || pool == null || pool.Length == 0)
            return result;

        var allowedChordIds = new HashSet<string>(pool.Select(chord => chord.Id), StringComparer.OrdinalIgnoreCase);
        EnsureFightClubSongSourceCache();
        if (fightClubSongSourceCache == null)
            return result;

        foreach (SongLibraryEntry song in fightClubSongSourceCache)
        {
            string songKey = BuildFightClubSongKey(song);
            if (!fightClubSelectedSongKeys.Contains(songKey))
                continue;

            GetFightClubSongChordIds(song);
            if (!fightClubSongTransitionCache.TryGetValue(songKey, out Dictionary<string, int> transitions))
                continue;

            foreach (KeyValuePair<string, int> pair in transitions)
            {
                SplitTransitionKey(pair.Key, out string fromId, out string toId);
                if (!allowedChordIds.Contains(fromId) || !allowedChordIds.Contains(toId))
                    continue;

                if (result.TryGetValue(pair.Key, out int existing))
                    result[pair.Key] = existing + pair.Value;
                else
                    result[pair.Key] = pair.Value;
            }
        }

        return result;
    }

    private HashSet<string> GetFightClubSongChordIds(SongLibraryEntry song)
    {
        var empty = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (song == null)
            return empty;

        string key = BuildFightClubSongKey(song);
        if (string.IsNullOrWhiteSpace(key))
            return empty;

        if (fightClubSongChordCache.TryGetValue(key, out HashSet<string> cached))
            return cached;

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var transitions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            List<MusicXmlLoader.MusicXmlPartSummary> summaries = SongNotationFacade.GetPartSummaries(song.PrimaryNotationPath, song.PrimaryNotationKind);
            if (summaries != null && summaries.Count > 0)
            {
                foreach (MusicXmlLoader.MusicXmlPartSummary summary in summaries)
                    AddSongChordMatches(result, transitions, SongNotationFacade.LoadSong(song.PrimaryNotationPath, song.PrimaryNotationKind, summary.Index));
            }
            else
            {
                AddSongChordMatches(result, transitions, SongNotationFacade.LoadSong(song.PrimaryNotationPath, song.PrimaryNotationKind));
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MiniGames] Failed to inspect song chords for '{song.DisplayName}': {ex.Message}");
        }

        fightClubSongChordCache[key] = result;
        fightClubSongTransitionCache[key] = transitions;
        return result;
    }

    private static void AddSongChordMatches(HashSet<string> target, Dictionary<string, int> transitions, List<NoteData> notes)
    {
        if (target == null || notes == null)
            return;

        string previousChordId = null;
        for (int i = 0; i < notes.Count; i++)
        {
            string chordName = notes[i].chordName;
            if (string.IsNullOrWhiteSpace(chordName))
                continue;

            FightClubChordDefinition chord = FightClubChordCatalog.FindByName(chordName);
            if (chord == null)
                continue;

            target.Add(chord.Id);
            if (!string.IsNullOrEmpty(previousChordId) &&
                !string.Equals(previousChordId, chord.Id, StringComparison.OrdinalIgnoreCase) &&
                transitions != null)
            {
                string transitionKey = BuildTransitionKey(previousChordId, chord.Id);
                if (transitions.TryGetValue(transitionKey, out int count))
                    transitions[transitionKey] = count + 1;
                else
                    transitions[transitionKey] = 1;
            }

            previousChordId = chord.Id;
        }
    }

    private static string BuildTransitionKey(string fromChordId, string toChordId)
    {
        return $"{fromChordId ?? string.Empty}>{toChordId ?? string.Empty}";
    }

    private static void SplitTransitionKey(string transitionKey, out string fromChordId, out string toChordId)
    {
        fromChordId = string.Empty;
        toChordId = string.Empty;
        if (string.IsNullOrWhiteSpace(transitionKey))
            return;

        int separator = transitionKey.IndexOf('>');
        if (separator < 0)
        {
            fromChordId = transitionKey.Trim();
            return;
        }

        fromChordId = transitionKey.Substring(0, separator).Trim();
        toChordId = transitionKey.Substring(separator + 1).Trim();
    }

    private static string BuildFightClubSongKey(SongLibraryEntry song)
    {
        if (song == null)
            return string.Empty;

        string value = !string.IsNullOrWhiteSpace(song.SongDirectory) ? song.SongDirectory : song.PrimaryNotationPath;
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace('\\', '/').Trim();
    }

    private FightClubRunSettings GetFightClubRunSettings()
    {
        EnsureFightClubSaveShape();
        return new FightClubRunSettings
        {
            chordLeniencyIndex = saveData.fightClub.settings.chordLeniencyIndex,
            beatIntervalSeconds = saveData.fightClub.settings.beatIntervalSeconds,
            countdownSeconds = saveData.fightClub.settings.countdownSeconds,
            maxFailedRounds = saveData.fightClub.settings.maxFailedRounds,
            metronomeSoundIndex = saveData.fightClub.settings.metronomeSoundIndex,
            chordPreviewInstrumentIndex = saveData.fightClub.settings.chordPreviewInstrumentIndex
        }.Clone();
    }

    private void ApplyFightClubSettingsToActiveRun()
    {
        if (fightClub.IsActive && !fightClub.IsEnded)
            fightClub.ApplySettings(GetFightClubRunSettings());
    }

    private int GetFightClubLevelHighScore(int levelIndex)
    {
        FightClubLevelScoreSaveData data = GetFightClubLevelScoreData(levelIndex, create: false);
        return Mathf.Max(0, data?.highScore ?? 0);
    }

    private FightClubLevelScoreSaveData GetFightClubLevelScoreData(int levelIndex, bool create)
    {
        if (levelIndex < 0 || FightClubChordCatalog.Levels == null || levelIndex >= FightClubChordCatalog.Levels.Length)
            return null;

        EnsureFightClubSaveShape();
        string levelId = FightClubChordCatalog.Levels[levelIndex]?.Id ?? string.Empty;
        FightClubLevelScoreSaveData existing = saveData.fightClub.levelScores
            .FirstOrDefault(score => score != null && string.Equals(score.levelId, levelId, StringComparison.OrdinalIgnoreCase));
        if (existing != null || !create)
            return existing;

        existing = new FightClubLevelScoreSaveData { levelId = levelId };
        saveData.fightClub.levelScores.Add(existing);
        return existing;
    }

    private void EnsureFightClubSaveShape()
    {
        if (saveData == null)
            saveData = new MiniGameSaveData();
        if (saveData.fightClub == null)
            saveData.fightClub = new FightClubSaveData();
        if (saveData.fightClub.levelScores == null)
            saveData.fightClub.levelScores = new List<FightClubLevelScoreSaveData>();
        if (saveData.fightClub.settings == null)
            saveData.fightClub.settings = new FightClubRunSettingsSaveData();

        saveData.fightClub.settings.chordLeniencyIndex = Mathf.Clamp(saveData.fightClub.settings.chordLeniencyIndex, FightClubRunSettings.StrictLeniency, FightClubRunSettings.ForgivingLeniency);
        saveData.fightClub.settings.beatIntervalSeconds = Mathf.Clamp(saveData.fightClub.settings.beatIntervalSeconds <= 0f ? 1.12f : saveData.fightClub.settings.beatIntervalSeconds, 0.66f, 2.5f);
        saveData.fightClub.settings.countdownSeconds = Mathf.Clamp(Mathf.Round(saveData.fightClub.settings.countdownSeconds <= 0f ? 3f : saveData.fightClub.settings.countdownSeconds), 1f, 8f);
        saveData.fightClub.settings.maxFailedRounds = Mathf.Clamp(saveData.fightClub.settings.maxFailedRounds <= 0 ? 3 : saveData.fightClub.settings.maxFailedRounds, 1, 10);
        saveData.fightClub.settings.metronomeSoundIndex = (int)StringTheoryMetronome.NormalizeSoundIndex(saveData.fightClub.settings.metronomeSoundIndex);
        saveData.fightClub.settings.chordPreviewInstrumentIndex = (int)StringTheoryChordAudioPlayer.NormalizeInstrumentIndex(saveData.fightClub.settings.chordPreviewInstrumentIndex);

        if (saveData.fightClub.highScore > 0 && saveData.fightClub.levelScores.Count == 0 && FightClubChordCatalog.Levels != null && FightClubChordCatalog.Levels.Length > 0)
        {
            saveData.fightClub.levelScores.Add(new FightClubLevelScoreSaveData
            {
                levelId = FightClubChordCatalog.Levels[0].Id,
                highScore = saveData.fightClub.highScore,
                bestStreak = Mathf.Max(0, saveData.fightClub.bestStreak)
            });
        }
    }

    private void Load()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(savePath) && File.Exists(savePath))
            {
                string json = File.ReadAllText(savePath);
                MiniGameSaveData loaded = JsonUtility.FromJson<MiniGameSaveData>(json);
                if (loaded != null)
                    saveData = loaded;
            }

            EnsureFightClubSaveShape();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MiniGames] Failed to load minigame save data: {ex.Message}");
            saveData = new MiniGameSaveData();
            EnsureFightClubSaveShape();
        }
    }

    private void Save()
    {
        if (string.IsNullOrWhiteSpace(savePath))
            return;

        try
        {
            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(savePath, JsonUtility.ToJson(saveData, true));
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MiniGames] Failed to save minigame data: {ex.Message}");
        }
    }

    [Serializable]
    private sealed class MiniGameSaveData
    {
        public FightClubSaveData fightClub = new FightClubSaveData();
    }

    [Serializable]
    private sealed class FightClubSaveData
    {
        public int highScore = 0;
        public int bestStreak = 0;
        public int lastScore;
        public int highestUnlockedLevelIndex;
        public int selectedLevelIndex;
        public List<FightClubLevelScoreSaveData> levelScores = new List<FightClubLevelScoreSaveData>();
        public FightClubRunSettingsSaveData settings = new FightClubRunSettingsSaveData();
    }

    [Serializable]
    private sealed class FightClubLevelScoreSaveData
    {
        public string levelId = string.Empty;
        public int highScore;
        public int bestStreak;
    }

    [Serializable]
    private sealed class FightClubRunSettingsSaveData
    {
        public int chordLeniencyIndex = FightClubRunSettings.NormalLeniency;
        public float beatIntervalSeconds = 1.12f;
        public float countdownSeconds = 3f;
        public int maxFailedRounds = 3;
        public int metronomeSoundIndex = (int)StringTheoryMetronomeSound.Drums;
        public int chordPreviewInstrumentIndex = (int)StringTheoryChordPreviewInstrument.ElectricGuitar;
    }
}

public sealed class FightClubMiniGame
{
    private enum Phase
    {
        Idle,
        OpponentIntro,
        OpponentCountdown,
        OpponentPreview,
        PlayerIntro,
        PlayerCountdown,
        Playing,
        RoundComplete,
        Ended
    }

    private enum ChordFlavor
    {
        Major,
        Minor,
        Dominant,
        MajorSeventh,
        MinorSeventh,
        Suspended,
        Power,
        Other
    }

    private const int ChordsPerRound = 3;
    private const int ExactRoundSearchLimit = 34;
    private const int SampledRoundSearchCount = 900;
    private const float OpponentPreviewHitLeadSeconds = 0.18f;
    private const float MinimumBeatIntervalSeconds = 0.66f;
    private const int MiniGameNoteIdBase = 8000000;
    private static readonly int[] StandardTuning = { 40, 45, 50, 55, 59, 64 };
    private static readonly FightClubChordDefinition[] ChordLibrary = FightClubChordCatalog.Chords;
    private FightClubChordDefinition[] activeChordPool = ChordLibrary;
    private Dictionary<string, int> activeTransitionWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private FightClubRunSettings activeSettings = FightClubRunSettings.CreateDefault();

    private readonly FightClubChordRun[] currentRound = new FightClubChordRun[ChordsPerRound];
    private readonly string[] previousRoundChordIds = new string[ChordsPerRound];
    private readonly Dictionary<string, FightClubChordResult> chordResults = new Dictionary<string, FightClubChordResult>(StringComparer.OrdinalIgnoreCase);
    private readonly System.Random random = new System.Random();
    private readonly HashSet<int> activeWindowPitches = new HashSet<int>();
    private Phase phase = Phase.Idle;
    private float phaseTime;
    private float roundTime;
    private int round = 1;
    private int activeChordIndex = -1;
    private int failedRounds;
    private bool endedByLoss;
    private bool lastRoundWasPerfect;
    private string statusLabel = "Ready";
    private bool opponentPreviewEnabled = true;
    private bool detectorHintDirty;
    private int opponentChordSoundSerial;
    private int opponentChordSoundIndex = -1;
    private int lastOpponentSoundIndex = -1;

    public bool IsActive => phase != Phase.Idle;
    public bool IsEnded => phase == Phase.Ended;
    public bool DetectorHintDirty => detectorHintDirty;
    public float DetectorTimelineTime => Mathf.Max(0f, roundTime);
    public int Score { get; private set; }
    public int HighScore { get; private set; }
    public int Streak { get; private set; }
    public int BestStreak { get; private set; }
    public int Misses { get; private set; }
    public int ActiveLevelIndex { get; private set; } = -1;

    public void SetHighScore(int highScore)
    {
        HighScore = Mathf.Max(0, highScore);
    }

    internal void Start(
        IEnumerable<FightClubChordDefinition> chordPool = null,
        int activeLevelIndex = -1,
        IReadOnlyDictionary<string, int> transitionWeights = null,
        FightClubRunSettings runSettings = null)
    {
        activeSettings = (runSettings ?? FightClubRunSettings.CreateDefault()).Clone();
        if (chordPool != null)
            activeChordPool = NormalizeChordPool(chordPool);
        else if (activeChordPool == null || activeChordPool.Length == 0)
            activeChordPool = ChordLibrary;
        if (transitionWeights != null)
            activeTransitionWeights = transitionWeights
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        else if (chordPool != null)
            activeTransitionWeights.Clear();
        ActiveLevelIndex = activeLevelIndex;
        Score = 0;
        Streak = 0;
        BestStreak = 0;
        Misses = 0;
        failedRounds = 0;
        endedByLoss = false;
        chordResults.Clear();
        round = 1;
        opponentChordSoundSerial = 0;
        opponentChordSoundIndex = -1;
        lastOpponentSoundIndex = -1;
        Array.Clear(previousRoundChordIds, 0, previousRoundChordIds.Length);
        statusLabel = "Get ready";
        BeginRoundIntro();
    }

    public void ApplySettings(FightClubRunSettings runSettings)
    {
        activeSettings = (runSettings ?? FightClubRunSettings.CreateDefault()).Clone();
        detectorHintDirty = true;
    }

    public void Stop()
    {
        phase = Phase.Idle;
        ActiveLevelIndex = -1;
        phaseTime = 0f;
        roundTime = 0f;
        activeChordIndex = -1;
        opponentChordSoundIndex = -1;
        lastOpponentSoundIndex = -1;
        Array.Clear(previousRoundChordIds, 0, previousRoundChordIds.Length);
        activeWindowPitches.Clear();
        detectorHintDirty = true;
    }

    internal void End(bool loss = false)
    {
        if (phase == Phase.Idle)
            return;

        phase = Phase.Ended;
        phaseTime = 0f;
        activeChordIndex = -1;
        activeWindowPitches.Clear();
        endedByLoss = loss;
        statusLabel = loss ? "Game over" : "Run ended";
        detectorHintDirty = true;
    }

    public void Update(float deltaTime, HashSet<int> detectedPitches)
    {
        if (phase == Phase.Idle)
            return;

        float dt = Mathf.Clamp(deltaTime, 0f, 0.1f);
        phaseTime += dt;

        switch (phase)
        {
            case Phase.OpponentIntro:
                if (phaseTime >= GetStartBannerDuration())
                {
                    if (opponentPreviewEnabled)
                        BeginOpponentCountdown();
                    else
                        BeginPlayerIntro();
                }
                break;
            case Phase.OpponentCountdown:
                if (phaseTime >= GetPrePlayCountdownDuration())
                    BeginOpponentPreview();
                break;
            case Phase.OpponentPreview:
                UpdateOpponentPreviewSoundCue();
                if (phaseTime >= GetOpponentPreviewDuration())
                    BeginPlayerIntro();
                break;
            case Phase.PlayerIntro:
                if (phaseTime >= GetStartBannerDuration())
                    BeginPlayerCountdown();
                break;
            case Phase.PlayerCountdown:
                if (phaseTime >= GetPrePlayCountdownDuration())
                    BeginRound();
                break;
            case Phase.Playing:
                roundTime += dt;
                UpdateRound(detectedPitches);
                break;
            case Phase.RoundComplete:
                if (phaseTime >= GetRoundCompleteHoldDuration())
                    BeginRoundIntro();
                break;
            case Phase.Ended:
                break;
        }
    }

    public FightClubMiniGameSnapshot BuildSnapshot()
    {
        var snapshot = new FightClubMiniGameSnapshot
        {
            active = IsActive,
            phaseLabel = GetPhaseLabel(),
            statusLabel = statusLabel,
            countdownLabel = GetCountdownLabel(),
            round = round,
            score = Score,
            highScore = HighScore,
            streak = Streak,
            bestStreak = BestStreak,
            misses = Misses,
            failedRounds = failedRounds,
            maxFailedRounds = activeSettings.maxFailedRounds,
            highScoreEnabled = ActiveLevelIndex >= 0,
            ended = phase == Phase.Ended,
            endedByLoss = endedByLoss,
            activeChordIndex = activeChordIndex,
            opponentPreviewActive = phase == Phase.OpponentPreview && opponentPreviewEnabled,
            opponentActiveChordIndex = phase == Phase.OpponentPreview && opponentPreviewEnabled ? GetOpponentPreviewChordIndex() : -1,
            opponentChordSoundSerial = opponentChordSoundSerial,
            opponentChordSoundIndex = opponentChordSoundIndex,
            beatIntervalSeconds = GetBeatInterval(),
            metronomeSoundIndex = activeSettings.metronomeSoundIndex,
            chordPreviewInstrumentIndex = activeSettings.chordPreviewInstrumentIndex,
            beatProgress01 = GetBeatProgress01()
        };

        for (int i = 0; i < currentRound.Length; i++)
        {
            FightClubChordRun run = currentRound[i];
            if (run == null)
                continue;

            int visualStatus = run.Status;
            bool visualActive = i == activeChordIndex && phase == Phase.Playing;
            if (phase == Phase.OpponentPreview && opponentPreviewEnabled)
            {
                int previewIndex = GetOpponentPreviewChordIndex();
                float previewSlotProgress = GetOpponentPreviewSlotProgress01();
                if (i < previewIndex || (i == previewIndex && previewSlotProgress >= 1f - Mathf.Clamp01(OpponentPreviewHitLeadSeconds / GetOpponentPreviewChordInterval())))
                    visualStatus = 1;
                visualActive = i == previewIndex;
            }

            snapshot.chords.Add(new FightClubChordSnapshot
            {
                name = run.Definition.Name,
                fretsLowToHigh = (int[])run.Definition.FretsLowToHigh.Clone(),
                fingersLowToHigh = (int[])run.Definition.FingersLowToHigh.Clone(),
                expectedMidis = run.ExpectedMidis,
                barres = run.Definition.GetBarres(),
                status = visualStatus,
                active = visualActive
            });
        }

        foreach (FightClubChordResult result in chordResults.Values
                     .OrderByDescending(result => result.Hits + result.Misses)
                     .ThenBy(result => result.Name, StringComparer.OrdinalIgnoreCase))
        {
            snapshot.chordResults.Add(new FightClubChordResultSnapshot
            {
                id = result.Id,
                name = result.Name,
                hits = result.Hits,
                misses = result.Misses
            });
        }

        return snapshot;
    }

    public MiniGameDetectorHintWindow[] BuildDetectorHintWindows()
    {
        if (phase != Phase.Playing)
            return Array.Empty<MiniGameDetectorHintWindow>();

        var windows = new List<MiniGameDetectorHintWindow>(ChordsPerRound);
        float beatInterval = GetBeatInterval();
        float rangeStart = roundTime - 0.35f;
        float rangeEnd = roundTime + (beatInterval * 2f);

        for (int i = 0; i < currentRound.Length; i++)
        {
            FightClubChordRun run = currentRound[i];
            if (run == null || run.Status != 0)
                continue;

            float target = i * beatInterval;
            float start = target - activeSettings.GetEarlyWindowSeconds();
            float end = target + activeSettings.GetLateWindowSeconds();
            if (end < rangeStart || start > rangeEnd)
                continue;

            windows.Add(new MiniGameDetectorHintWindow(
                Mathf.Max(start, rangeStart),
                Mathf.Min(end, rangeEnd),
                run.ExpectedMidis,
                run.BuildExpectedNotes(target)));
        }

        return windows.ToArray();
    }

    public void ClearDetectorHintDirty()
    {
        detectorHintDirty = false;
    }

    private void BeginRoundIntro()
    {
        phase = opponentPreviewEnabled ? Phase.OpponentIntro : Phase.PlayerIntro;
        phaseTime = 0f;
        roundTime = 0f;
        activeChordIndex = -1;
        opponentChordSoundIndex = -1;
        lastOpponentSoundIndex = -1;
        activeWindowPitches.Clear();
        GenerateRound();
        statusLabel = $"Round {round.ToString(CultureInfo.InvariantCulture)}";
        detectorHintDirty = true;
    }

    private void BeginOpponentCountdown()
    {
        phase = Phase.OpponentCountdown;
        phaseTime = 0f;
        activeChordIndex = -1;
        statusLabel = "Watch";
        detectorHintDirty = true;
    }

    private void BeginOpponentPreview()
    {
        phase = Phase.OpponentPreview;
        phaseTime = 0f;
        activeChordIndex = -1;
        opponentChordSoundIndex = -1;
        lastOpponentSoundIndex = -1;
        statusLabel = "Player 2";
        UpdateOpponentPreviewSoundCue();
        detectorHintDirty = true;
    }

    private void BeginPlayerIntro()
    {
        phase = Phase.PlayerIntro;
        phaseTime = 0f;
        activeChordIndex = -1;
        statusLabel = "Your turn";
        detectorHintDirty = true;
    }

    private void BeginPlayerCountdown()
    {
        phase = Phase.PlayerCountdown;
        phaseTime = 0f;
        activeChordIndex = -1;
        statusLabel = "Get ready";
        detectorHintDirty = true;
    }

    private float GetPrePlayCountdownDuration()
    {
        return Mathf.Clamp(Mathf.Round(activeSettings.countdownSeconds), 1f, 8f) * GetBeatInterval();
    }

    private float GetOpponentPreviewDuration()
    {
        return ChordsPerRound * GetOpponentPreviewChordInterval();
    }

    private float GetOpponentPreviewChordInterval()
    {
        return GetBeatInterval();
    }

    private float GetStartBannerDuration()
    {
        return GetBeatInterval();
    }

    private float GetRoundCompleteHoldDuration()
    {
        return GetBeatInterval();
    }

    private void UpdateOpponentPreviewSoundCue()
    {
        if (phase != Phase.OpponentPreview || !opponentPreviewEnabled)
            return;

        int previewIndex = GetOpponentPreviewChordIndex();
        if (previewIndex < 0 || previewIndex == lastOpponentSoundIndex)
            return;

        lastOpponentSoundIndex = previewIndex;
        opponentChordSoundIndex = previewIndex;
        opponentChordSoundSerial++;
    }

    private int GetOpponentPreviewChordIndex()
    {
        if (phase != Phase.OpponentPreview || !opponentPreviewEnabled)
            return -1;

        float interval = GetOpponentPreviewChordInterval();
        float previewDuration = GetOpponentPreviewDuration();
        if (phaseTime < 0f || phaseTime >= previewDuration)
            return -1;

        return Mathf.Clamp(Mathf.FloorToInt(phaseTime / interval), 0, ChordsPerRound - 1);
    }

    private float GetOpponentPreviewSlotProgress01()
    {
        if (phase != Phase.OpponentPreview || !opponentPreviewEnabled)
            return 0f;

        float interval = GetOpponentPreviewChordInterval();
        float previewDuration = GetOpponentPreviewDuration();
        if (phaseTime < 0f || phaseTime >= previewDuration)
            return 0f;

        return Mathf.Clamp01((phaseTime % interval) / interval);
    }

    private void BeginRound()
    {
        phase = Phase.Playing;
        phaseTime = 0f;
        roundTime = 0f;
        activeChordIndex = -1;
        activeWindowPitches.Clear();
        statusLabel = "On beat";
        detectorHintDirty = true;
    }

    private void FinishRound()
    {
        if (!lastRoundWasPerfect)
        {
            failedRounds++;
            if (failedRounds >= activeSettings.maxFailedRounds)
            {
                End(loss: true);
                return;
            }
        }

        phase = Phase.RoundComplete;
        phaseTime = 0f;
        activeChordIndex = -1;
        activeWindowPitches.Clear();
        statusLabel = lastRoundWasPerfect
            ? "Perfect round"
            : $"{Mathf.Max(0, activeSettings.maxFailedRounds - failedRounds).ToString(CultureInfo.InvariantCulture)} failed rounds left";
        round++;
        detectorHintDirty = true;
    }

    private void GenerateRound()
    {
        int maxDifficulty = Mathf.Clamp(1 + ((round - 1) / 2), 1, 5);
        FightClubChordDefinition[] pool = activeChordPool != null && activeChordPool.Length > 0 ? activeChordPool : ChordLibrary;
        List<FightClubChordDefinition> candidates = BuildRoundCandidates(pool, maxDifficulty);
        FightClubChordDefinition[] selectedRound = SelectRoundSequence(candidates, maxDifficulty);

        for (int i = 0; i < currentRound.Length; i++)
        {
            FightClubChordDefinition selected = selectedRound[Mathf.Min(i, selectedRound.Length - 1)];
            currentRound[i] = new FightClubChordRun(selected, i, round);
            previousRoundChordIds[i] = selected.Id;
        }

        lastRoundWasPerfect = true;
    }

    private static List<FightClubChordDefinition> BuildRoundCandidates(FightClubChordDefinition[] pool, int maxDifficulty)
    {
        List<FightClubChordDefinition> orderedPool = (pool ?? ChordLibrary)
            .Where(chord => chord != null)
            .GroupBy(chord => chord.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(chord => chord.Difficulty)
            .ThenBy(chord => GetAverageFret(chord))
            .ThenBy(chord => chord.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (orderedPool.Count == 0)
            orderedPool = ChordLibrary.ToList();

        var candidates = orderedPool
            .Where(chord => chord.Difficulty <= maxDifficulty)
            .ToList();

        if (candidates.Count < ChordsPerRound)
        {
            foreach (FightClubChordDefinition chord in orderedPool)
            {
                if (candidates.Any(candidate => string.Equals(candidate.Id, chord.Id, StringComparison.OrdinalIgnoreCase)))
                    continue;

                candidates.Add(chord);
                if (candidates.Count >= ChordsPerRound)
                    break;
            }
        }

        return candidates.Count > 0 ? candidates : ChordLibrary.Take(ChordsPerRound).ToList();
    }

    private FightClubChordDefinition[] SelectRoundSequence(List<FightClubChordDefinition> candidates, int maxDifficulty)
    {
        if (candidates == null || candidates.Count == 0)
            return ChordLibrary.Take(ChordsPerRound).ToArray();

        if (candidates.Count == 1)
            return Enumerable.Repeat(candidates[0], ChordsPerRound).ToArray();

        FightClubChordDefinition bestA = null;
        FightClubChordDefinition bestB = null;
        FightClubChordDefinition bestC = null;
        float bestScore = float.NegativeInfinity;

        if (candidates.Count <= ExactRoundSearchLimit)
        {
            for (int ai = 0; ai < candidates.Count; ai++)
            {
                for (int bi = 0; bi < candidates.Count; bi++)
                {
                    for (int ci = 0; ci < candidates.Count; ci++)
                    {
                        ConsiderRoundSequence(
                            candidates[ai],
                            candidates[bi],
                            candidates[ci],
                            candidates.Count,
                            maxDifficulty,
                            ref bestA,
                            ref bestB,
                            ref bestC,
                            ref bestScore);
                    }
                }
            }
        }
        else
        {
            for (int attempt = 0; attempt < SampledRoundSearchCount; attempt++)
            {
                ConsiderRoundSequence(
                    candidates[random.Next(candidates.Count)],
                    candidates[random.Next(candidates.Count)],
                    candidates[random.Next(candidates.Count)],
                    candidates.Count,
                    maxDifficulty,
                    ref bestA,
                    ref bestB,
                    ref bestC,
                    ref bestScore);
            }
        }

        if (bestA == null || bestB == null || bestC == null)
            return BuildFallbackRoundSequence(candidates);

        return new[] { bestA, bestB, bestC };
    }

    private void ConsiderRoundSequence(
        FightClubChordDefinition a,
        FightClubChordDefinition b,
        FightClubChordDefinition c,
        int candidateCount,
        int maxDifficulty,
        ref FightClubChordDefinition bestA,
        ref FightClubChordDefinition bestB,
        ref FightClubChordDefinition bestC,
        ref float bestScore)
    {
        if (ShouldSkipRoundSequence(a, b, c, candidateCount))
            return;

        float score = ScoreRoundSequence(a, b, c, maxDifficulty) + ((float)random.NextDouble() * 0.08f);
        if (score <= bestScore)
            return;

        bestA = a;
        bestB = b;
        bestC = c;
        bestScore = score;
    }

    private static bool ShouldSkipRoundSequence(
        FightClubChordDefinition a,
        FightClubChordDefinition b,
        FightClubChordDefinition c,
        int candidateCount)
    {
        if (a == null || b == null || c == null)
            return true;

        if (candidateCount > 1 &&
            (string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(b.Id, c.Id, StringComparison.OrdinalIgnoreCase)))
            return true;

        if (candidateCount >= ChordsPerRound &&
            (string.Equals(a.Id, c.Id, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(a.Id, b.Id, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(b.Id, c.Id, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    private static FightClubChordDefinition[] BuildFallbackRoundSequence(List<FightClubChordDefinition> candidates)
    {
        var result = new FightClubChordDefinition[ChordsPerRound];
        for (int i = 0; i < result.Length; i++)
            result[i] = candidates[i % candidates.Count];

        return result;
    }

    private float ScoreRoundSequence(
        FightClubChordDefinition a,
        FightClubChordDefinition b,
        FightClubChordDefinition c,
        int maxDifficulty)
    {
        float score = 0f;
        score += ScoreSharedKey(a, b, c) * 2.4f;
        score += ScoreChordTransition(a, b);
        score += ScoreChordTransition(b, c);
        score += ScorePreferredTransition(a, b);
        score += ScorePreferredTransition(b, c);

        int minDifficulty = Mathf.Min(a.Difficulty, Mathf.Min(b.Difficulty, c.Difficulty));
        int maxRoundDifficulty = Mathf.Max(a.Difficulty, Mathf.Max(b.Difficulty, c.Difficulty));
        int difficultySpread = maxRoundDifficulty - minDifficulty;
        score -= difficultySpread * 1.1f;
        score -= Mathf.Max(0, maxRoundDifficulty - maxDifficulty) * 0.7f;

        if (string.Equals(previousRoundChordIds[0], a.Id, StringComparison.OrdinalIgnoreCase))
            score -= 0.65f;
        if (string.Equals(previousRoundChordIds[1], b.Id, StringComparison.OrdinalIgnoreCase))
            score -= 0.65f;
        if (string.Equals(previousRoundChordIds[2], c.Id, StringComparison.OrdinalIgnoreCase))
            score -= 0.65f;
        if (string.Equals(previousRoundChordIds[0], a.Id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(previousRoundChordIds[1], b.Id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(previousRoundChordIds[2], c.Id, StringComparison.OrdinalIgnoreCase))
            score -= 4.5f;

        return score;
    }

    private float ScorePreferredTransition(FightClubChordDefinition from, FightClubChordDefinition to)
    {
        if (from == null || to == null || activeTransitionWeights == null || activeTransitionWeights.Count == 0)
            return 0f;

        string directKey = BuildTransitionKey(from.Id, to.Id);
        if (activeTransitionWeights.TryGetValue(directKey, out int directCount))
            return Mathf.Min(2.5f, 0.65f + ((float)Math.Log(directCount + 1) * 0.55f));

        string reverseKey = BuildTransitionKey(to.Id, from.Id);
        if (activeTransitionWeights.TryGetValue(reverseKey, out int reverseCount))
            return Mathf.Min(1.0f, 0.25f + ((float)Math.Log(reverseCount + 1) * 0.25f));

        return 0f;
    }

    private static float ScoreChordTransition(FightClubChordDefinition from, FightClubChordDefinition to)
    {
        if (from == null || to == null)
            return -10f;
        if (string.Equals(from.Id, to.Id, StringComparison.OrdinalIgnoreCase))
            return -8f;

        float score = 0f;
        float fretDelta = Mathf.Abs(GetAverageFret(from) - GetAverageFret(to));
        score -= fretDelta * 0.32f;

        int maxFretDelta = Mathf.Abs(GetMaxFret(from) - GetMaxFret(to));
        score -= Mathf.Max(0, maxFretDelta - 4) * 0.18f;

        bool fromOpen = IsOpenPositionChord(from);
        bool toOpen = IsOpenPositionChord(to);
        if (fromOpen && toOpen)
            score += 0.85f;
        else if (fromOpen != toOpen)
            score -= Mathf.Max(0f, fretDelta - 2f) * 0.24f;

        int fromRoot = GetRootPitchClass(from);
        int toRoot = GetRootPitchClass(to);
        if (fromRoot >= 0 && toRoot >= 0)
        {
            int directed = PitchClassDistance(toRoot, fromRoot);
            int shortest = Mathf.Min(directed, 12 - directed);
            if (directed == 5 || directed == 7)
                score += 1.35f;
            else if (directed == 2 || directed == 10)
                score += 0.7f;
            else if (directed == 0)
                score += 0.45f;
            else if (shortest == 6)
                score -= 0.85f;
        }

        if (GetChordFlavor(from) == GetChordFlavor(to))
            score += 0.35f;

        return score;
    }

    private static float ScoreSharedKey(
        FightClubChordDefinition a,
        FightClubChordDefinition b,
        FightClubChordDefinition c)
    {
        float bestScore = 0f;
        for (int key = 0; key < 12; key++)
        {
            float score = 0f;
            int degreeA;
            int degreeB;
            int degreeC;
            score += ScoreChordInMajorKey(a, key, out degreeA);
            score += ScoreChordInMajorKey(b, key, out degreeB);
            score += ScoreChordInMajorKey(c, key, out degreeC);

            int fitCount = 0;
            if (degreeA >= 0)
                fitCount++;
            if (degreeB >= 0)
                fitCount++;
            if (degreeC >= 0)
                fitCount++;

            if (fitCount == ChordsPerRound)
                score += 2.5f;
            if (IsCommonMajorDegreeTransition(degreeA, degreeB))
                score += 0.8f;
            if (IsCommonMajorDegreeTransition(degreeB, degreeC))
                score += 0.8f;
            if (degreeA == 0 || degreeB == 0 || degreeC == 0)
                score += 0.35f;
            if (degreeA == 7 || degreeB == 7 || degreeC == 7)
                score += 0.35f;

            bestScore = Mathf.Max(bestScore, score);
        }

        return bestScore;
    }

    private static float ScoreChordInMajorKey(FightClubChordDefinition chord, int keyPitchClass, out int degree)
    {
        int root = GetRootPitchClass(chord);
        if (root < 0)
        {
            degree = -1;
            return 0f;
        }

        degree = PitchClassDistance(root, keyPitchClass);
        ChordFlavor flavor = GetChordFlavor(chord);
        switch (flavor)
        {
            case ChordFlavor.Major:
                if (degree == 0 || degree == 5 || degree == 7)
                    return 2.2f;
                if (degree == 2 || degree == 10)
                    return 0.8f;
                break;
            case ChordFlavor.Minor:
                if (degree == 2 || degree == 4 || degree == 9)
                    return 2.1f;
                break;
            case ChordFlavor.Dominant:
                if (degree == 7)
                    return 2.4f;
                if (degree == 0 || degree == 5)
                    return 1.0f;
                break;
            case ChordFlavor.MajorSeventh:
                if (degree == 0 || degree == 5)
                    return 2.2f;
                break;
            case ChordFlavor.MinorSeventh:
                if (degree == 2 || degree == 4 || degree == 9)
                    return 2.2f;
                break;
            case ChordFlavor.Suspended:
                if (degree == 0 || degree == 5 || degree == 7)
                    return 2.0f;
                break;
            case ChordFlavor.Power:
                if (degree == 0 || degree == 2 || degree == 5 || degree == 7 || degree == 10)
                    return 1.65f;
                break;
            default:
                if (degree == 0 || degree == 2 || degree == 4 || degree == 5 || degree == 7 || degree == 9 || degree == 11)
                    return 0.9f;
                break;
        }

        degree = -1;
        return 0f;
    }

    private static bool IsCommonMajorDegreeTransition(int fromDegree, int toDegree)
    {
        if (fromDegree < 0 || toDegree < 0 || fromDegree == toDegree)
            return false;

        return
            (fromDegree == 0 && (toDegree == 5 || toDegree == 7 || toDegree == 9 || toDegree == 2)) ||
            (fromDegree == 5 && (toDegree == 0 || toDegree == 7 || toDegree == 9)) ||
            (fromDegree == 7 && (toDegree == 0 || toDegree == 5 || toDegree == 9)) ||
            (fromDegree == 9 && (toDegree == 5 || toDegree == 7 || toDegree == 0)) ||
            (fromDegree == 2 && (toDegree == 7 || toDegree == 0));
    }

    private static int GetRootPitchClass(FightClubChordDefinition chord)
    {
        if (chord == null || string.IsNullOrWhiteSpace(chord.Name))
            return -1;

        string name = chord.Name.Trim();
        int root;
        switch (char.ToUpperInvariant(name[0]))
        {
            case 'C':
                root = 0;
                break;
            case 'D':
                root = 2;
                break;
            case 'E':
                root = 4;
                break;
            case 'F':
                root = 5;
                break;
            case 'G':
                root = 7;
                break;
            case 'A':
                root = 9;
                break;
            case 'B':
                root = 11;
                break;
            default:
                return -1;
        }

        if (name.Length > 1)
        {
            char accidental = name[1];
            if (accidental == '#' || accidental == '♯')
                root++;
            else if (accidental == 'b' || accidental == '♭')
                root--;
        }

        return Mod12(root);
    }

    private static ChordFlavor GetChordFlavor(FightClubChordDefinition chord)
    {
        string suffix = GetChordSuffix(chord).ToUpperInvariant();
        if (suffix.Contains("DIM") || suffix.Contains("AUG"))
            return ChordFlavor.Other;
        if (suffix.Contains("SUS"))
            return ChordFlavor.Suspended;
        if (suffix.Contains("MAJ7"))
            return ChordFlavor.MajorSeventh;
        if (suffix.StartsWith("M7", StringComparison.Ordinal) || suffix.StartsWith("MIN7", StringComparison.Ordinal))
            return ChordFlavor.MinorSeventh;
        if (suffix.StartsWith("M", StringComparison.Ordinal) && !suffix.StartsWith("MAJ", StringComparison.Ordinal))
            return ChordFlavor.Minor;
        if (suffix.Contains("5") && !suffix.Contains("ADD"))
            return ChordFlavor.Power;
        if (suffix.Contains("7") || (suffix.Contains("9") && !suffix.Contains("ADD")))
            return ChordFlavor.Dominant;

        return ChordFlavor.Major;
    }

    private static string GetChordSuffix(FightClubChordDefinition chord)
    {
        if (chord == null || string.IsNullOrWhiteSpace(chord.Name))
            return string.Empty;

        string name = chord.Name.Trim();
        int suffixIndex = 1;
        if (name.Length > 1 && (name[1] == '#' || name[1] == '♯' || name[1] == 'b' || name[1] == '♭'))
            suffixIndex = 2;

        return suffixIndex < name.Length ? name.Substring(suffixIndex).Replace(" ", string.Empty) : string.Empty;
    }

    private static int PitchClassDistance(int pitchClass, int fromPitchClass)
    {
        return Mod12(pitchClass - fromPitchClass);
    }

    private static int Mod12(int value)
    {
        int result = value % 12;
        return result < 0 ? result + 12 : result;
    }

    private static float GetAverageFret(FightClubChordDefinition chord)
    {
        if (chord == null || chord.FretsLowToHigh == null)
            return 0f;

        int count = 0;
        float sum = 0f;
        foreach (int fret in chord.FretsLowToHigh)
        {
            if (fret < 0)
                continue;

            sum += fret;
            count++;
        }

        return count > 0 ? sum / count : 0f;
    }

    private static int GetMaxFret(FightClubChordDefinition chord)
    {
        if (chord == null || chord.FretsLowToHigh == null)
            return 0;

        int max = 0;
        foreach (int fret in chord.FretsLowToHigh)
        {
            if (fret > max)
                max = fret;
        }

        return max;
    }

    private static bool IsOpenPositionChord(FightClubChordDefinition chord)
    {
        if (chord == null || chord.FretsLowToHigh == null)
            return false;

        bool hasOpen = false;
        int maxFret = 0;
        foreach (int fret in chord.FretsLowToHigh)
        {
            if (fret == 0)
                hasOpen = true;
            if (fret > maxFret)
                maxFret = fret;
        }

        return hasOpen && maxFret <= 4;
    }

    private static string BuildTransitionKey(string fromChordId, string toChordId)
    {
        return $"{fromChordId ?? string.Empty}>{toChordId ?? string.Empty}";
    }

    private static FightClubChordDefinition[] NormalizeChordPool(IEnumerable<FightClubChordDefinition> chordPool)
    {
        if (chordPool == null)
            return ChordLibrary;

        FightClubChordDefinition[] normalized = chordPool
            .Where(chord => chord != null)
            .GroupBy(chord => chord.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(chord => chord.Difficulty)
            .ThenBy(chord => chord.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length > 0 ? normalized : ChordLibrary;
    }

    private void UpdateRound(HashSet<int> detectedPitches)
    {
        float beatInterval = GetBeatInterval();
        bool anyPending = false;

        for (int i = 0; i < currentRound.Length; i++)
        {
            FightClubChordRun run = currentRound[i];
            if (run == null || run.Status != 0)
                continue;

            anyPending = true;
            float targetTime = i * beatInterval;
            float windowStart = targetTime - activeSettings.GetEarlyWindowSeconds();
            float windowEnd = targetTime + activeSettings.GetLateWindowSeconds();

            if (roundTime >= windowStart && roundTime <= windowEnd)
            {
                if (activeChordIndex != i)
                {
                    activeChordIndex = i;
                    activeWindowPitches.Clear();
                    detectorHintDirty = true;
                }

                if (detectedPitches != null && detectedPitches.Count > 0)
                    activeWindowPitches.UnionWith(detectedPitches);

                if (IsChordMatched(run))
                {
                    MarkChordHit(run);
                    activeChordIndex = -1;
                    activeWindowPitches.Clear();
                    detectorHintDirty = true;
                }
            }
            else if (roundTime > windowEnd)
            {
                MarkChordMiss(run);
                activeChordIndex = -1;
                activeWindowPitches.Clear();
                detectorHintDirty = true;
            }
        }

        if (!anyPending && roundTime >= ((ChordsPerRound - 1) * beatInterval) + activeSettings.GetLateWindowSeconds() + 0.25f)
            FinishRound();
    }

    private bool IsChordMatched(FightClubChordRun run)
    {
        if (run == null || run.ExpectedMidis == null || run.ExpectedMidis.Length == 0)
            return false;

        int matched = 0;
        for (int i = 0; i < run.ExpectedMidis.Length; i++)
        {
            if (activeWindowPitches.Contains(run.ExpectedMidis[i]))
                matched++;
        }

        int required = Mathf.Max(1, run.ExpectedMidis.Length - activeSettings.GetAllowedMissingNotes(run.ExpectedMidis.Length));
        return matched >= required;
    }

    private void MarkChordHit(FightClubChordRun run)
    {
        run.Status = 1;
        AddChordResult(run.Definition, hit: true);
        Streak++;
        BestStreak = Mathf.Max(BestStreak, Streak);
        int roundBonus = Mathf.Max(1, round);
        int streakBonus = 1 + Mathf.Min(4, Streak / 5);
        Score += 100 * roundBonus * streakBonus;
        HighScore = Mathf.Max(HighScore, Score);
        statusLabel = "Hit";
    }

    private void MarkChordMiss(FightClubChordRun run)
    {
        run.Status = 2;
        AddChordResult(run.Definition, hit: false);
        Streak = 0;
        Misses++;
        lastRoundWasPerfect = false;
        statusLabel = "Miss";
    }

    private void AddChordResult(FightClubChordDefinition chord, bool hit)
    {
        if (chord == null)
            return;

        if (!chordResults.TryGetValue(chord.Id, out FightClubChordResult result))
        {
            result = new FightClubChordResult(chord.Id, chord.Name);
            chordResults[chord.Id] = result;
        }

        if (hit)
            result.Hits++;
        else
            result.Misses++;
    }

    private float GetBeatInterval()
    {
        return Mathf.Max(MinimumBeatIntervalSeconds, activeSettings.beatIntervalSeconds);
    }

    private string GetPhaseLabel()
    {
        switch (phase)
        {
            case Phase.OpponentIntro:
            case Phase.OpponentCountdown:
            case Phase.OpponentPreview:
            case Phase.PlayerIntro:
            case Phase.PlayerCountdown:
                return "Countdown";
            case Phase.Playing:
                return "Play";
            case Phase.RoundComplete:
                return "Round Clear";
            case Phase.Ended:
                return endedByLoss ? "Game Over" : "Finished";
            default:
                return "Ready";
        }
    }

    private string GetCountdownLabel()
    {
        switch (phase)
        {
            case Phase.OpponentIntro:
                return "PLAYER 2 STARTS";
            case Phase.PlayerIntro:
                return "PLAYER 1 STARTS";
            case Phase.OpponentCountdown:
            case Phase.PlayerCountdown:
                float remaining = Mathf.Max(0f, GetPrePlayCountdownDuration() - phaseTime);
                float beatInterval = Mathf.Max(0.001f, GetBeatInterval());
                int maxCount = Mathf.Clamp(Mathf.RoundToInt(activeSettings.countdownSeconds), 1, 8);
                int count = Mathf.Clamp(Mathf.CeilToInt(remaining / beatInterval), 1, maxCount);
                return count.ToString(CultureInfo.InvariantCulture);
            default:
                return string.Empty;
        }
    }

    private float GetCountdownProgress01()
    {
        switch (phase)
        {
            case Phase.OpponentIntro:
            case Phase.PlayerIntro:
                return Mathf.Clamp01(phaseTime / GetStartBannerDuration());
            case Phase.OpponentCountdown:
            case Phase.PlayerCountdown:
                return Mathf.Clamp01((phaseTime % Mathf.Max(0.001f, GetBeatInterval())) / Mathf.Max(0.001f, GetBeatInterval()));
            case Phase.OpponentPreview:
                return GetOpponentPreviewSlotProgress01();
            default:
                return 0f;
        }
    }

    private float GetBeatProgress01()
    {
        if (phase != Phase.Playing)
            return GetCountdownProgress01();

        float beatInterval = GetBeatInterval();
        if (beatInterval <= 0f)
            return 0f;

        return Mathf.Clamp01((roundTime % beatInterval) / beatInterval);
    }

    private sealed class FightClubChordRun
    {
        public readonly FightClubChordDefinition Definition;
        public readonly int[] ExpectedMidis;
        private readonly int chordIndex;
        private readonly int roundIndex;
        public int Status;

        public FightClubChordRun(FightClubChordDefinition definition, int chordIndex, int roundIndex)
        {
            Definition = definition;
            this.chordIndex = chordIndex;
            this.roundIndex = Mathf.Max(1, roundIndex);
            ExpectedMidis = definition.GetExpectedMidis(StandardTuning);
        }

        public MiniGameExpectedNote[] BuildExpectedNotes(float noteTime)
        {
            int roundNoteIdBase = MiniGameNoteIdBase + ((roundIndex - 1) * 128);
            return Definition.GetExpectedNotes(chordIndex, noteTime, StandardTuning, roundNoteIdBase);
        }
    }

    private sealed class FightClubChordResult
    {
        public readonly string Id;
        public readonly string Name;
        public int Hits;
        public int Misses;

        public FightClubChordResult(string id, string name)
        {
            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
        }
    }
}
