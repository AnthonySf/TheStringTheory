using System;
using System.Collections.Generic;
using System.Linq;
using MidiPlayerTK;
using UnityEngine;

public sealed class GeneratedSongPlayer : IDisposable
{
    private const float ScheduleLookaheadRealSeconds = 0.12f;
    private const float HardSeekToleranceSeconds = 0.10f;
    private const int PitchWheelCenterValue = 8192;

    private readonly List<MPTKEvent> scheduleBuffer = new List<MPTKEvent>(128);
    private readonly Dictionary<int, int> lastPitchWheelValueByChannel = new Dictionary<int, int>();
    private readonly HashSet<string> loggedBendScheduleKeys = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> loggedBendActivationKeys = new HashSet<string>(StringComparer.Ordinal);
    private GameObject synthRoot;
    private MidiStreamPlayer midiStreamPlayer;
    private GeneratedPlaybackArrangement arrangement;
    private int nextNoteIndex;
    private bool wasPlaying;
    private float lastSyncedSongTime = float.NegativeInfinity;
    private float lastPlaybackSpeedScale = 1f;
    private bool routeConfigurationDirty = true;
    private bool subscribedToPresetLoaded;
    private bool loggedArrangementSummary;
    private bool loggedSynthNotReady;
    private bool loggedMissingArrangement;
    private bool loggedPlaybackWindowInfo;
    private bool loggedQueuedNotes;
    private bool loggedRouteConfiguration;

    public bool HasArrangement => arrangement != null && arrangement.IsValid;
    public float ArrangementDurationSeconds => arrangement != null ? arrangement.durationSeconds : 0f;
    public bool IsReady => midiStreamPlayer != null && midiStreamPlayer.MPTK_SoundFont.IsReady;

    public void EnsureInitialized(Transform parent)
    {
        if (midiStreamPlayer != null)
            return;

        if (MidiPlayerGlobal.Instance == null)
        {
            GameObject globalRoot = new GameObject("MidiPlayerGlobal");
            if (parent != null)
                globalRoot.transform.SetParent(parent, false);
            globalRoot.AddComponent<MidiPlayerGlobal>();
        }

        synthRoot = new GameObject("GeneratedSongPlayer");
        if (parent != null)
            synthRoot.transform.SetParent(parent, false);

        midiStreamPlayer = synthRoot.AddComponent<MidiStreamPlayer>();
        midiStreamPlayer.MPTK_CorePlayer = true;
        midiStreamPlayer.MPTK_DirectSendToPlayer = true;
        midiStreamPlayer.MPTK_EnablePresetDrum = true;
        midiStreamPlayer.MPTK_ApplyRealTimeModulator = true;
        midiStreamPlayer.MPTK_LogEvents = false;
        if (midiStreamPlayer.CoreAudioSource != null)
            midiStreamPlayer.CoreAudioSource.enabled = true;
        midiStreamPlayer.MPTK_StartMidiStream();
        midiStreamPlayer.MPTK_StartSynth();
        if (midiStreamPlayer.CoreAudioSource != null && !midiStreamPlayer.CoreAudioSource.isPlaying)
        {
            midiStreamPlayer.CoreAudioSource.enabled = true;
            midiStreamPlayer.CoreAudioSource.Play();
            Debug.LogWarning("[GeneratedSong] Forced Maestro CoreAudioSource to Play() for generated backing playback.");
        }

        SubscribeToPresetLoaded();
    }

    public void LoadArrangement(Transform parent, GeneratedPlaybackArrangement newArrangement)
    {
        EnsureInitialized(parent);
        arrangement = newArrangement;
        routeConfigurationDirty = true;
        loggedArrangementSummary = false;
        loggedPlaybackWindowInfo = false;
        loggedQueuedNotes = false;
        loggedMissingArrangement = false;
        ResetState(clearSound: true);
        LogArrangementSummaryIfNeeded();
        if (IsReady)
            ApplyRouteConfiguration();
    }

    public void ClearArrangement()
    {
        arrangement = null;
        loggedMissingArrangement = false;
        ResetState(clearSound: true);
    }

    public void SetMasterVolumePercent(float songVolumePercent)
    {
        if (midiStreamPlayer == null)
            return;

        midiStreamPlayer.MPTK_Volume = Mathf.Clamp01(songVolumePercent / 100f);
    }

    public void SyncTransport(float songTimeSeconds, float playbackSpeedScale, bool shouldPlay, bool forceSeek)
    {
        if (!HasArrangement)
        {
            if (!loggedMissingArrangement)
            {
                Debug.LogWarning("[GeneratedSong] Generated playback is selected, but no valid arrangement is loaded.");
                loggedMissingArrangement = true;
            }
            return;
        }

        if (midiStreamPlayer == null)
            return;

        if (!IsReady)
        {
            if (!loggedSynthNotReady)
            {
                Debug.LogWarning($"[GeneratedSong] Maestro synth is not ready yet. SoundFontReady={MidiPlayerGlobal.MPTK_SoundFontIsReady}");
                loggedSynthNotReady = true;
            }
            return;
        }

        loggedSynthNotReady = false;

        playbackSpeedScale = Mathf.Max(0.01f, playbackSpeedScale);
        if (routeConfigurationDirty)
            ApplyRouteConfiguration();

        bool outOfPlayableRange = songTimeSeconds <= 0f || songTimeSeconds >= arrangement.durationSeconds;
        if (!shouldPlay || outOfPlayableRange)
        {
            if (wasPlaying || forceSeek)
            {
                ResetPitchWheelState();
                ClearAllSound();
            }

            wasPlaying = false;
            lastSyncedSongTime = songTimeSeconds;
            lastPlaybackSpeedScale = playbackSpeedScale;
            return;
        }

        LogPlaybackWindowIfNeeded(songTimeSeconds);

        bool hardResync = forceSeek ||
                          !wasPlaying ||
                          Mathf.Abs(playbackSpeedScale - lastPlaybackSpeedScale) > 0.0001f ||
                          songTimeSeconds < lastSyncedSongTime - 0.01f ||
                          Mathf.Abs(songTimeSeconds - lastSyncedSongTime) > HardSeekToleranceSeconds;

        if (hardResync)
            ResetScheduleAt(songTimeSeconds, playbackSpeedScale);

        UpdateRealtimePitchBends(songTimeSeconds);
        QueueUpcomingNotes(songTimeSeconds, playbackSpeedScale);

        wasPlaying = true;
        lastSyncedSongTime = songTimeSeconds;
        lastPlaybackSpeedScale = playbackSpeedScale;
    }

    public void StopImmediately()
    {
        ResetState(clearSound: true);
    }

    public void Dispose()
    {
        UnsubscribeFromPresetLoaded();
        ResetState(clearSound: true);

        if (synthRoot != null)
            UnityEngine.Object.Destroy(synthRoot);

        synthRoot = null;
        midiStreamPlayer = null;
    }

    private void ResetState(bool clearSound)
    {
        nextNoteIndex = 0;
        wasPlaying = false;
        lastSyncedSongTime = float.NegativeInfinity;
        lastPlaybackSpeedScale = 1f;
        loggedBendScheduleKeys.Clear();
        loggedBendActivationKeys.Clear();
        ResetPitchWheelState();
        if (clearSound)
            ClearAllSound();
    }

    private void ResetScheduleAt(float songTimeSeconds, float playbackSpeedScale)
    {
        ClearAllSound();
        nextNoteIndex = FindFirstNoteIndexAtOrAfter(songTimeSeconds);
        ScheduleActiveSustains(songTimeSeconds, playbackSpeedScale);
    }

    private void QueueUpcomingNotes(float songTimeSeconds, float playbackSpeedScale)
    {
        if (arrangement == null || arrangement.notes == null || arrangement.notes.Count == 0)
            return;

        float songLookahead = ScheduleLookaheadRealSeconds * playbackSpeedScale;
        float scheduleLimit = songTimeSeconds + songLookahead;
        scheduleBuffer.Clear();

        while (nextNoteIndex >= 0 && nextNoteIndex < arrangement.notes.Count)
        {
            GeneratedPlaybackNoteEvent note = arrangement.notes[nextNoteIndex];
            if (note.startTimeSeconds > scheduleLimit)
                break;

            float earliestScheduleSongTime = note.startTimeSeconds - Mathf.Max(0f, note.pitchPreRollSeconds);
            if (songTimeSeconds + 0.0005f < earliestScheduleSongTime)
                break;

            float delaySeconds = Mathf.Max(0f, (note.startTimeSeconds - songTimeSeconds) / playbackSpeedScale);
            float durationSeconds = Mathf.Max(0.02f, note.durationSeconds / playbackSpeedScale);
            LogQueuedBendNoteIfNeeded(note, songTimeSeconds, playbackSpeedScale, delaySeconds, durationSeconds);
            AddScheduledNote(note, delaySeconds, durationSeconds);

            nextNoteIndex++;
        }

        if (scheduleBuffer.Count > 0)
        {
            if (!loggedQueuedNotes)
            {
                MPTKEvent firstEvent = scheduleBuffer[0];
                Debug.LogWarning($"[GeneratedSong] Queued {scheduleBuffer.Count} Maestro events at songTime={songTimeSeconds:F3}s speed={playbackSpeedScale:F2}. FirstEvent delay={firstEvent.Delay}ms duration={firstEvent.Duration}ms channel={firstEvent.Channel} note={firstEvent.Value}.");
                loggedQueuedNotes = true;
            }
            midiStreamPlayer.MPTK_PlayEvent(scheduleBuffer);
        }
    }

    private void ScheduleActiveSustains(float songTimeSeconds, float playbackSpeedScale)
    {
        if (arrangement == null || arrangement.notes == null || arrangement.notes.Count == 0)
            return;

        scheduleBuffer.Clear();
        for (int i = 0; i < arrangement.notes.Count; i++)
        {
            GeneratedPlaybackNoteEvent note = arrangement.notes[i];
            if (note.startTimeSeconds >= songTimeSeconds || note.EndTimeSeconds <= songTimeSeconds + 0.01f)
                continue;

            float remainingSeconds = Mathf.Max(0.02f, (note.EndTimeSeconds - songTimeSeconds) / playbackSpeedScale);
            AddScheduledNote(note, 0f, remainingSeconds);
        }

        if (scheduleBuffer.Count > 0)
            midiStreamPlayer.MPTK_PlayEvent(scheduleBuffer);
    }

    private int FindFirstNoteIndexAtOrAfter(float songTimeSeconds)
    {
        if (arrangement == null || arrangement.notes == null || arrangement.notes.Count == 0)
            return 0;

        int low = 0;
        int high = arrangement.notes.Count;
        while (low < high)
        {
            int mid = low + ((high - low) / 2);
            if (arrangement.notes[mid].startTimeSeconds < songTimeSeconds)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }

    private void ApplyRouteConfiguration()
    {
        if (midiStreamPlayer == null || !IsReady || arrangement == null)
            return;

        scheduleBuffer.Clear();
        if (arrangement.channelAssignments != null)
        {
            for (int i = 0; i < arrangement.channelAssignments.Count; i++)
            {
                GeneratedPlaybackChannelAssignment route = arrangement.channelAssignments[i];
                int bank = ResolveRouteBank(route);
                int preset = Mathf.Clamp(route.preset, 0, 127);
                midiStreamPlayer.MPTK_Channels[route.channel].ForcedBank = bank;
                midiStreamPlayer.MPTK_Channels[route.channel].BankNum = bank;
                midiStreamPlayer.MPTK_Channels[route.channel].ForcedPreset = preset;
                midiStreamPlayer.MPTK_Channels[route.channel].PresetNum = preset;

                if (!route.isDrum && route.pitchBendRangeSemitones > 0)
                {
                    scheduleBuffer.Add(BuildControlChangeEvent(route.channel, MPTKController.RPN_MSB, 0));
                    scheduleBuffer.Add(BuildControlChangeEvent(route.channel, MPTKController.RPN_LSB, (int)midi_rpn_event.RPN_PITCH_BEND_RANGE));
                    scheduleBuffer.Add(BuildControlChangeEvent(route.channel, MPTKController.DATA_ENTRY_MSB, Mathf.Clamp(route.pitchBendRangeSemitones, 0, 24)));
                    scheduleBuffer.Add(BuildControlChangeEvent(route.channel, MPTKController.DATA_ENTRY_LSB, 0));
                    scheduleBuffer.Add(BuildControlChangeEvent(route.channel, MPTKController.RPN_MSB, 127));
                    scheduleBuffer.Add(BuildControlChangeEvent(route.channel, MPTKController.RPN_LSB, 127));
                    scheduleBuffer.Add(new MPTKEvent
                    {
                        Command = MPTKCommand.PitchWheelChange,
                        Value = PitchWheelCenterValue,
                        Channel = route.channel
                    });
                }
            }
        }

        if (scheduleBuffer.Count > 0)
            midiStreamPlayer.MPTK_PlayEvent(scheduleBuffer);

        if (!loggedRouteConfiguration && arrangement.channelAssignments != null && arrangement.channelAssignments.Count > 0)
        {
            string summary = string.Join(", ", arrangement.channelAssignments.Select(route =>
                $"ch{route.channel}:{route.label} bank={ResolveRouteBank(route)} preset={route.preset}" +
                (route.pitchBendRangeSemitones > 0 ? $" bend={route.pitchBendRangeSemitones}" : string.Empty)));
            Debug.LogWarning($"[GeneratedSong] Applied Maestro route configuration: {summary}");
            loggedRouteConfiguration = true;
        }

        routeConfigurationDirty = false;
    }

    private int ResolveRouteBank(GeneratedPlaybackChannelAssignment route)
    {
        if (route.bank >= 0)
            return route.bank;

        if (MidiPlayerGlobal.ImSFCurrent == null)
            return 0;

        return route.isDrum
            ? MidiPlayerGlobal.ImSFCurrent.DrumKitBankNumber
            : MidiPlayerGlobal.ImSFCurrent.DefaultBankNumber;
    }

    private void ClearAllSound()
    {
        if (midiStreamPlayer != null)
            midiStreamPlayer.MPTK_ClearAllSound(false);
    }

    private void SubscribeToPresetLoaded()
    {
        if (subscribedToPresetLoaded || MidiPlayerGlobal.OnEventPresetLoaded == null)
            return;

        MidiPlayerGlobal.OnEventPresetLoaded.AddListener(HandlePresetLoaded);
        subscribedToPresetLoaded = true;
    }

    private void UnsubscribeFromPresetLoaded()
    {
        if (!subscribedToPresetLoaded || MidiPlayerGlobal.OnEventPresetLoaded == null)
            return;

        MidiPlayerGlobal.OnEventPresetLoaded.RemoveListener(HandlePresetLoaded);
        subscribedToPresetLoaded = false;
    }

    private void HandlePresetLoaded()
    {
        Debug.LogWarning("[GeneratedSong] Maestro SoundFont/preset load completed.");
        routeConfigurationDirty = true;
        if (IsReady)
            ApplyRouteConfiguration();
    }

    private void LogArrangementSummaryIfNeeded()
    {
        if (loggedArrangementSummary || arrangement == null)
            return;

        if (arrangement.notes == null || arrangement.notes.Count == 0)
        {
            Debug.LogWarning($"[GeneratedSong] Arrangement ready from '{arrangement.sourcePath}', but no generated tracks are currently selected.");
            loggedArrangementSummary = true;
            return;
        }

        GeneratedPlaybackNoteEvent first = arrangement.notes[0];
        GeneratedPlaybackNoteEvent last = arrangement.notes[arrangement.notes.Count - 1];
        Debug.LogWarning($"[GeneratedSong] Arrangement ready from '{arrangement.sourcePath}'. Notes={arrangement.notes.Count}, Channels={arrangement.channelAssignments?.Count ?? 0}, FirstNote={first.startTimeSeconds:F3}s, LastNote={last.startTimeSeconds:F3}s, Duration={arrangement.durationSeconds:F3}s.");
        loggedArrangementSummary = true;
    }

    private void LogPlaybackWindowIfNeeded(float songTimeSeconds)
    {
        if (loggedPlaybackWindowInfo || arrangement == null || arrangement.notes == null || arrangement.notes.Count == 0)
            return;

        GeneratedPlaybackNoteEvent first = arrangement.notes[0];
        if (songTimeSeconds < first.startTimeSeconds)
        {
            Debug.LogWarning($"[GeneratedSong] Playback active but first generated note is still ahead. SongTime={songTimeSeconds:F3}s, FirstNote={first.startTimeSeconds:F3}s.");
        }
        else
        {
            Debug.LogWarning($"[GeneratedSong] Playback reached generated note window. SongTime={songTimeSeconds:F3}s, FirstNote={first.startTimeSeconds:F3}s.");
        }

        loggedPlaybackWindowInfo = true;
    }

    private void AddScheduledNote(GeneratedPlaybackNoteEvent note, float delaySeconds, float durationSeconds)
    {
        long noteDelayMs = Math.Max(0L, Mathf.RoundToInt(delaySeconds * 1000f));
        long durationMs = Math.Max(1L, Mathf.RoundToInt(durationSeconds * 1000f));
        int velocity = Mathf.Clamp(Mathf.RoundToInt(note.velocity * Mathf.Clamp(note.attackVelocityScale, 0.1f, 1.5f)), 1, 127);

        scheduleBuffer.Add(new MPTKEvent
        {
            Command = MPTKCommand.NoteOn,
            Channel = note.channel,
            Value = note.midiNote,
            Velocity = velocity,
            Delay = noteDelayMs,
            Duration = durationMs
        });
    }

    private static MPTKEvent BuildControlChangeEvent(int channel, MPTKController controller, int value)
    {
        return new MPTKEvent
        {
            Command = MPTKCommand.ControlChange,
            Controller = controller,
            Value = value,
            Channel = channel
        };
    }

    private static int ConvertSemitoneOffsetToPitchWheel(float semitoneOffset, int rangeSemitones)
    {
        if (rangeSemitones <= 0)
            return PitchWheelCenterValue;

        float normalized = Mathf.Clamp(semitoneOffset / rangeSemitones, -1f, 1f);
        return Mathf.Clamp(Mathf.RoundToInt(PitchWheelCenterValue + (normalized * 8191f)), 0, 16383);
    }

    private static float EvaluatePitchCurveSemitones(GeneratedPlaybackNoteEvent note, float normalizedTime)
    {
        if (note.pitchCurve == null || note.pitchCurve.Count == 0)
            return 0f;

        if (note.pitchCurve.Count == 1)
            return note.pitchCurve[0].semitoneOffset;

        float clampedTime = Mathf.Clamp01(normalizedTime);
        GeneratedPlaybackPitchPoint previous = note.pitchCurve[0];
        for (int i = 1; i < note.pitchCurve.Count; i++)
        {
            GeneratedPlaybackPitchPoint current = note.pitchCurve[i];
            if (current.normalizedTime <= previous.normalizedTime)
            {
                previous = current;
                continue;
            }

            if (clampedTime <= current.normalizedTime)
            {
                float t = Mathf.InverseLerp(previous.normalizedTime, current.normalizedTime, clampedTime);
                return Mathf.Lerp(previous.semitoneOffset, current.semitoneOffset, t);
            }

            previous = current;
        }

        return note.pitchCurve[note.pitchCurve.Count - 1].semitoneOffset;
    }

    private static float EvaluateRealtimePitchSemitones(GeneratedPlaybackNoteEvent note, float songTimeSeconds)
    {
        float normalizedTime = Mathf.Clamp01((songTimeSeconds - note.startTimeSeconds) / Mathf.Max(0.001f, note.durationSeconds));
        float semitoneOffset = EvaluatePitchCurveSemitones(note, normalizedTime);
        if (!note.HasVibrato)
            return semitoneOffset;

        if (normalizedTime < note.vibratoDelayNormalized)
            return semitoneOffset;

        float vibratoPhaseTime = Mathf.Max(0f, songTimeSeconds - note.startTimeSeconds - (note.durationSeconds * note.vibratoDelayNormalized));
        float fadeNormalized = note.vibratoFadeNormalized > 0.001f
            ? Mathf.Clamp01((normalizedTime - note.vibratoDelayNormalized) / note.vibratoFadeNormalized)
            : 1f;
        float vibrato = Mathf.Sin(vibratoPhaseTime * note.vibratoRateHz * Mathf.PI * 2f) * note.vibratoDepthSemitones * fadeNormalized;
        return semitoneOffset + vibrato;
    }

    private void UpdateRealtimePitchBends(float songTimeSeconds)
    {
        if (midiStreamPlayer == null || arrangement?.channelAssignments == null)
            return;

        HashSet<int> activeBendChannels = new HashSet<int>();
        for (int i = 0; i < arrangement.channelAssignments.Count; i++)
        {
            GeneratedPlaybackChannelAssignment route = arrangement.channelAssignments[i];
            if (route == null || route.pitchBendRangeSemitones <= 0)
                continue;

            int pitchWheelValue = ResolveCurrentPitchWheelForChannel(route.channel, route.pitchBendRangeSemitones, songTimeSeconds);
            SendPitchWheelIfChanged(route.channel, pitchWheelValue);
            activeBendChannels.Add(route.channel);
        }

        if (lastPitchWheelValueByChannel.Count == 0)
            return;

        List<int> staleChannels = lastPitchWheelValueByChannel.Keys.Where(channel => !activeBendChannels.Contains(channel)).ToList();
        for (int i = 0; i < staleChannels.Count; i++)
            SendPitchWheelIfChanged(staleChannels[i], PitchWheelCenterValue, removeIfCenter: true);
    }

    private int ResolveCurrentPitchWheelForChannel(int channel, int rangeSemitones, float songTimeSeconds)
    {
        if (arrangement?.notes == null)
            return PitchWheelCenterValue;

        for (int i = arrangement.notes.Count - 1; i >= 0; i--)
        {
            GeneratedPlaybackNoteEvent note = arrangement.notes[i];
            if (note.channel != channel)
                continue;

            if (!note.HasPitchCurve && !note.HasVibrato)
                continue;

            float pitchActivationStart = note.startTimeSeconds - Mathf.Max(0f, note.pitchPreRollSeconds);
            if (songTimeSeconds < pitchActivationStart || songTimeSeconds > note.EndTimeSeconds)
                continue;

            LogActiveBendNoteIfNeeded(note, songTimeSeconds, rangeSemitones);

            if (songTimeSeconds < note.startTimeSeconds)
                return ConvertSemitoneOffsetToPitchWheel(note.PitchStartSemitoneOffset, rangeSemitones);

            float semitoneOffset = EvaluateRealtimePitchSemitones(note, songTimeSeconds);
            return ConvertSemitoneOffsetToPitchWheel(semitoneOffset, rangeSemitones);
        }

        return PitchWheelCenterValue;
    }

    private void ResetPitchWheelState()
    {
        if (midiStreamPlayer == null)
        {
            lastPitchWheelValueByChannel.Clear();
            return;
        }

        if (arrangement?.channelAssignments != null)
        {
            for (int i = 0; i < arrangement.channelAssignments.Count; i++)
            {
                GeneratedPlaybackChannelAssignment route = arrangement.channelAssignments[i];
                if (route != null && route.pitchBendRangeSemitones > 0)
                    SendPitchWheelIfChanged(route.channel, PitchWheelCenterValue, removeIfCenter: true, forceSend: true);
            }
        }

        lastPitchWheelValueByChannel.Clear();
    }

    private void SendPitchWheelIfChanged(int channel, int value, bool removeIfCenter = false, bool forceSend = false)
    {
        if (!forceSend && lastPitchWheelValueByChannel.TryGetValue(channel, out int currentValue) && currentValue == value)
            return;

        midiStreamPlayer.MPTK_PlayDirectEvent(new MPTKEvent
        {
            Command = MPTKCommand.PitchWheelChange,
            Channel = channel,
            Value = value
        });

        if (removeIfCenter && value == PitchWheelCenterValue)
            lastPitchWheelValueByChannel.Remove(channel);
        else
            lastPitchWheelValueByChannel[channel] = value;
    }

    private void LogQueuedBendNoteIfNeeded(
        GeneratedPlaybackNoteEvent note,
        float songTimeSeconds,
        float playbackSpeedScale,
        float delaySeconds,
        float durationSeconds)
    {
        if (!note.HasPitchCurve)
            return;

        string key = BuildBendDebugKey(note);
        if (!loggedBendScheduleKeys.Add(key))
            return;

        Debug.LogWarning(
            $"[GeneratedSong][BendQueue] {key} songTime={songTimeSeconds:F3}s speed={playbackSpeedScale:F2} " +
            $"delay={delaySeconds * 1000f:F0}ms dur={durationSeconds * 1000f:F0}ms preroll={note.pitchPreRollSeconds * 1000f:F0}ms " +
            $"curve={FormatPitchCurve(note)}");
    }

    private void LogActiveBendNoteIfNeeded(GeneratedPlaybackNoteEvent note, float songTimeSeconds, int rangeSemitones)
    {
        if (!note.HasPitchCurve)
            return;

        string key = BuildBendDebugKey(note);
        if (!loggedBendActivationKeys.Add(key))
            return;

        float semitoneOffset = songTimeSeconds < note.startTimeSeconds
            ? note.PitchStartSemitoneOffset
            : EvaluateRealtimePitchSemitones(note, songTimeSeconds);
        int pitchWheelValue = ConvertSemitoneOffsetToPitchWheel(semitoneOffset, rangeSemitones);

        Debug.LogWarning(
            $"[GeneratedSong][BendActive] {key} activeAt={songTimeSeconds:F3}s startOffset={note.PitchStartSemitoneOffset:F2}st " +
            $"currentOffset={semitoneOffset:F2}st wheel={pitchWheelValue} range={rangeSemitones} " +
            $"curve={FormatPitchCurve(note)}");
    }

    private static string BuildBendDebugKey(GeneratedPlaybackNoteEvent note)
    {
        return $"part='{note.partName}' ch={note.channel} midi={note.midiNote} start={note.startTimeSeconds:F3}s dur={note.durationSeconds:F3}s";
    }

    private static string FormatPitchCurve(GeneratedPlaybackNoteEvent note)
    {
        if (note.pitchCurve == null || note.pitchCurve.Count == 0)
            return "[]";

        return "[" + string.Join(", ", note.pitchCurve.Select(point => $"({point.normalizedTime:F3},{point.semitoneOffset:F2})")) + "]";
    }
}
