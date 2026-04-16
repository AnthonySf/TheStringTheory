import contextlib
import io
import logging
import os
import queue
import tempfile
import threading
import time
from collections import Counter, deque

import aubio
import numpy as np
import sounddevice as sd
from scipy.io import wavfile

logging.getLogger().setLevel(logging.ERROR)

from basic_pitch.inference import predict

SAMPLE_RATE = 22050
HOP_S = 512
CAPTURE_SAMPLES = int(SAMPLE_RATE * 0.3)
DEBOUNCE_SEC = 0.05
EVENT_BROADCAST_SEC = 0.140

CONTINUOUS_RMS_GATE = 0.007
CONTINUOUS_CONFIDENCE_GATE = 0.65
CONTINUOUS_MEDIAN_WINDOW = 5
CONTINUOUS_STABLE_FRAMES = 1
CONTINUOUS_HOLD_SEC = 0.10
CONTINUOUS_MIN_MIDI = 36
CONTINUOUS_MAX_MIDI = 88

UNITY_SYNC_ALPHA = 0.20
DEFAULT_HINT_LOOKBACK_SEC = 0.070
DEFAULT_HINT_LOOKAHEAD_SEC = 0.220
HINT_RETENTION_SEC = 2.0
EXPECT_EXACT_BONUS = 1.50
EXPECT_NEAR_BONUS_1 = 0.80
EXPECT_NEAR_BONUS_2 = 0.35
EXPECT_MAX_DISTANCE_AI = 2
EXPECT_MAX_DISTANCE_CONTINUOUS = 2
EXPECT_STRICT_CONFIDENCE = 0.80
CHORD_SCORE_KEEP_RATIO = 0.72
CHORD_EXPECTED_SCORE_KEEP_RATIO = 0.62
MAX_EVENT_NOTES = 6

NOTE_NAMES = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"]
NOTE_TO_SEMITONE = {name: i for i, name in enumerate(NOTE_NAMES)}

audio_queue = queue.Queue()
ai_task_queue = queue.Queue()
ai_result_queue = queue.Queue()

runtime_lock = threading.Lock()
frames_processed_lock = threading.Lock()
frames_processed = 0

latest_packet = "A|--|0|0.0000|"
latest_status = "Detector idle."
latest_input_name = "Default input device"
last_error = ""
running = False
stop_event = threading.Event()
detector_thread = None
ai_thread_started = False
session_generation = 0
selected_input_device_index = None
last_input_level = 0.0


def midi_to_note_name(midi_num):
    return f"{NOTE_NAMES[midi_num % 12]}{(midi_num // 12) - 1}"


def note_name_to_midi(note_name):
    if not note_name:
        return None
    s = note_name.strip().upper().replace("♯", "#")
    if len(s) < 2:
        return None

    if len(s) >= 3 and s[1] == "#":
        pitch = s[:2]
        octave_str = s[2:]
    else:
        pitch = s[:1]
        octave_str = s[1:]

    if pitch not in NOTE_TO_SEMITONE:
        return None

    try:
        octave = int(octave_str)
    except ValueError:
        return None

    return NOTE_TO_SEMITONE[pitch] + (octave + 1) * 12


def semitone_distance(note_a, note_b):
    midi_a = note_name_to_midi(note_a)
    midi_b = note_name_to_midi(note_b)
    if midi_a is None or midi_b is None:
        return None
    return abs(midi_a - midi_b)


def _set_status(text):
    global latest_status
    with runtime_lock:
        latest_status = text or ""


def _set_last_error(text):
    global last_error
    with runtime_lock:
        last_error = text or ""


def _set_packet(packet_text):
    global latest_packet
    with runtime_lock:
        latest_packet = packet_text or "A|--|0|0.0000|"

class UnityHintState:
    def __init__(self):
        self.lock = threading.Lock()
        self.offset = None
        self.expected_windows = deque()

    def reset(self):
        with self.lock:
            self.offset = None
            self.expected_windows.clear()

    def set_sync(self, unity_song_time, python_audio_time):
        new_offset = python_audio_time - unity_song_time
        with self.lock:
            if self.offset is None:
                self.offset = new_offset
            else:
                self.offset = (1.0 - UNITY_SYNC_ALPHA) * self.offset + UNITY_SYNC_ALPHA * new_offset

    def add_hint_window(self, start_time, end_time, notes):
        notes = {n.strip().upper() for n in notes if n and n.strip()}
        if not notes:
            return
        if end_time < start_time:
            start_time, end_time = end_time, start_time

        with self.lock:
            self.expected_windows.append(
                {
                    "start": float(start_time),
                    "end": float(end_time),
                    "notes": notes,
                    "created_at": time.time(),
                }
            )
            self._prune_locked()

    def _prune_locked(self):
        now = time.time()
        while self.expected_windows and (now - self.expected_windows[0]["created_at"]) > HINT_RETENTION_SEC:
            self.expected_windows.popleft()

    def get_expected_notes_for_unity_time(self, unity_song_time):
        with self.lock:
            self._prune_locked()
            matches = set()
            for entry in self.expected_windows:
                if entry["start"] <= unity_song_time <= entry["end"]:
                    matches.update(entry["notes"])
            return matches

    def python_time_to_unity_time(self, python_audio_time):
        with self.lock:
            if self.offset is None:
                return None
            return python_audio_time - self.offset


unity_hint_state = UnityHintState()


def get_python_audio_time():
    with frames_processed_lock:
        return frames_processed / SAMPLE_RATE


def get_input_device_name(device_index):
    if device_index is None:
        try:
            default_input = sd.default.device[0]
            if default_input is not None and default_input >= 0:
                return sd.query_devices(default_input)["name"]
        except Exception:
            pass
        return "Default input device"

    try:
        return sd.query_devices(device_index)["name"]
    except Exception:
        return f"Device {device_index}"


def list_input_devices_json():
    try:
        result = []
        devices = sd.query_devices()
        for i, device in enumerate(devices):
            if device.get("max_input_channels", 0) > 0:
                result.append(
                    {
                        "index": int(i),
                        "name": str(device.get("name", f"Device {i}")),
                        "channels": int(device.get("max_input_channels", 0)),
                    }
                )
        import json

        return json.dumps(result)
    except Exception:
        return "[]"


def parse_unity_message(message):
    message = (message or "").strip()
    if not message:
        return

    parts = message.split("|")
    cmd = parts[0].strip().upper()
    python_now = get_python_audio_time()

    try:
        if cmd in ("SYNC", "TIME"):
            unity_song_time = float(parts[-1])
            unity_hint_state.set_sync(unity_song_time, python_now)
            return

        if cmd in ("EXPECT", "HINT"):
            current_song_time = float(parts[1])
            unity_hint_state.set_sync(current_song_time, python_now)

            if len(parts) == 3:
                notes = [n.strip() for n in parts[2].split(",") if n.strip()]
                unity_hint_state.add_hint_window(
                    current_song_time - DEFAULT_HINT_LOOKBACK_SEC,
                    current_song_time + DEFAULT_HINT_LOOKAHEAD_SEC,
                    notes,
                )
            else:
                for entry in parts[2:]:
                    entry = entry.strip()
                    if not entry:
                        continue
                    try:
                        start_s, end_s, notes_csv = entry.split(":", 2)
                        notes = [n.strip() for n in notes_csv.split(",") if n.strip()]
                        unity_hint_state.add_hint_window(float(start_s), float(end_s), notes)
                    except ValueError:
                        notes = [n.strip() for n in entry.split(",") if n.strip()]
                        unity_hint_state.add_hint_window(
                            current_song_time - DEFAULT_HINT_LOOKBACK_SEC,
                            current_song_time + DEFAULT_HINT_LOOKAHEAD_SEC,
                            notes,
                        )
    except Exception as exc:
        _set_last_error(f"Hint parse error: {exc}")


def get_expected_notes_for_python_time(python_audio_time):
    unity_time = unity_hint_state.python_time_to_unity_time(python_audio_time)
    if unity_time is None:
        return set(), None
    return unity_hint_state.get_expected_notes_for_unity_time(unity_time), unity_time


def score_ai_candidates(note_events, expected_notes):
    if not note_events:
        return []

    max_amp = max(float(event[3]) for event in note_events) if note_events else 1.0
    candidates = []

    for event in note_events:
        note_name = midi_to_note_name(int(event[2]))
        amp = float(event[3])
        rel_amp = amp / max_amp if max_amp > 0 else 0.0
        best_dist = None
        if expected_notes:
            distances = [semitone_distance(note_name, exp) for exp in expected_notes]
            distances = [d for d in distances if d is not None]
            if distances:
                best_dist = min(distances)

        score = rel_amp
        if best_dist is not None:
            if best_dist == 0:
                score += EXPECT_EXACT_BONUS
            elif best_dist == 1:
                score += EXPECT_NEAR_BONUS_1
            elif best_dist <= EXPECT_MAX_DISTANCE_AI:
                score += EXPECT_NEAR_BONUS_2

        candidates.append(
            {
                "note": note_name,
                "amp": amp,
                "rel_amp": rel_amp,
                "score": score,
                "distance": best_dist,
            }
        )

    dedup = {}
    for candidate in candidates:
        previous = dedup.get(candidate["note"])
        if previous is None or candidate["score"] > previous["score"]:
            dedup[candidate["note"]] = candidate

    ranked = sorted(dedup.values(), key=lambda item: (-item["score"], -item["rel_amp"], item["note"]))
    if not ranked:
        return []

    best_score = ranked[0]["score"]
    expected_exact_present = any(candidate["distance"] == 0 for candidate in ranked)
    keep_ratio = CHORD_EXPECTED_SCORE_KEEP_RATIO if expected_exact_present else CHORD_SCORE_KEEP_RATIO

    selected = []
    for candidate in ranked:
        if len(selected) >= MAX_EVENT_NOTES:
            break
        if candidate["score"] >= best_score * keep_ratio:
            if expected_notes:
                if candidate["distance"] is not None and candidate["distance"] > EXPECT_MAX_DISTANCE_AI and candidate["rel_amp"] < 0.95:
                    continue
            selected.append(candidate["note"])

    if selected:
        return selected

    fallback = []
    for candidate in ranked:
        if candidate["rel_amp"] > 0.4:
            fallback.append(candidate["note"])
    return fallback[:MAX_EVENT_NOTES]


def ai_worker():
    while True:
        task = ai_task_queue.get()
        if task is None:
            return

        audio_data, pluck_id, onset_time, generation = task
        temp_wav = os.path.join(tempfile.gettempdir(), f"embedded_pluck_{generation}_{pluck_id}.wav")

        try:
            wavfile.write(temp_wav, SAMPLE_RATE, audio_data)
            with contextlib.redirect_stdout(io.StringIO()):
                _, _, note_events = predict(temp_wav)

            expected_notes, unity_time = get_expected_notes_for_python_time(onset_time)
            ai_notes = score_ai_candidates(note_events or [], expected_notes)

            ai_result_queue.put(
                {
                    "generation": generation,
                    "id": pluck_id,
                    "notes": ai_notes,
                    "onset_time": onset_time,
                    "expected_notes": sorted(expected_notes),
                    "unity_time": unity_time,
                }
            )
        except Exception as exc:
            _set_last_error(f"AI error: {exc}")
            ai_result_queue.put(
                {
                    "generation": generation,
                    "id": pluck_id,
                    "notes": [],
                    "onset_time": onset_time,
                    "expected_notes": [],
                    "unity_time": None,
                }
            )
        finally:
            if os.path.exists(temp_wav):
                try:
                    os.remove(temp_wav)
                except OSError:
                    pass


def ensure_ai_worker():
    global ai_thread_started
    if ai_thread_started:
        return
    threading.Thread(target=ai_worker, daemon=True).start()
    ai_thread_started = True


def flush_audio_queue():
    while True:
        try:
            audio_queue.get_nowait()
        except queue.Empty:
            return


def make_onset_detector():
    detector = aubio.onset("default", 1024, HOP_S, SAMPLE_RATE)
    detector.set_threshold(0.2)
    return detector


def make_pitch_detector():
    detector = aubio.pitch("yinfast", 2048, HOP_S, SAMPLE_RATE)
    detector.set_unit("midi")
    detector.set_tolerance(0.82)
    return detector


def update_continuous_notes(chunk_float32, current_time, pitch_detector, state):
    rms = float(np.sqrt(np.mean(np.square(chunk_float32), dtype=np.float64)))
    state["last_rms"] = rms

    if not np.isfinite(rms) or rms < CONTINUOUS_RMS_GATE:
        state["continuous_pitch_history"].clear()
        state["stable_note"] = None
        state["stable_count"] = 0
        if current_time - state["last_continuous_note_time"] > CONTINUOUS_HOLD_SEC:
            state["last_continuous_note"] = None
            return set()
        return {state["last_continuous_note"]} if state["last_continuous_note"] else set()

    midi_estimate = float(pitch_detector(chunk_float32)[0])
    confidence = float(pitch_detector.get_confidence())

    candidate_note = None
    if (
        np.isfinite(midi_estimate)
        and CONTINUOUS_MIN_MIDI <= midi_estimate <= CONTINUOUS_MAX_MIDI
        and confidence >= CONTINUOUS_CONFIDENCE_GATE
    ):
        midi_quantized = int(round(midi_estimate))
        candidate_note = midi_to_note_name(midi_quantized)

    expected_notes, _unity_time = get_expected_notes_for_python_time(current_time)
    if candidate_note is not None and expected_notes:
        distances = [semitone_distance(candidate_note, exp) for exp in expected_notes]
        distances = [distance for distance in distances if distance is not None]
        best_dist = min(distances) if distances else None
        if best_dist is not None and best_dist > EXPECT_MAX_DISTANCE_CONTINUOUS and confidence < EXPECT_STRICT_CONFIDENCE:
            candidate_note = None

    if candidate_note is not None:
        state["continuous_pitch_history"].append(candidate_note)
        if len(state["continuous_pitch_history"]) > 0:
            most_common_note, _ = Counter(state["continuous_pitch_history"]).most_common(1)[0]
        else:
            most_common_note = candidate_note

        if most_common_note == state["stable_note"]:
            state["stable_count"] += 1
        else:
            state["stable_note"] = most_common_note
            state["stable_count"] = 1

        if state["stable_count"] >= CONTINUOUS_STABLE_FRAMES:
            state["last_continuous_note"] = state["stable_note"]
            state["last_continuous_note_time"] = current_time
            return {state["last_continuous_note"]}

        if state["last_continuous_note"] and (current_time - state["last_continuous_note_time"]) <= CONTINUOUS_HOLD_SEC:
            return {state["last_continuous_note"]}
        return set()

    if (current_time - state["last_continuous_note_time"]) <= CONTINUOUS_HOLD_SEC and state["last_continuous_note"]:
        return {state["last_continuous_note"]}

    state["continuous_pitch_history"].clear()
    state["stable_note"] = None
    state["stable_count"] = 0
    state["last_continuous_note"] = None
    return set()


def _make_detector_state():
    return {
        "active_captures": [],
        "pluck_counter": 0,
        "last_onset_time": 0.0,
        "current_active_notes": set(),
        "continuous_pitch_history": deque(maxlen=CONTINUOUS_MEDIAN_WINDOW),
        "last_continuous_note": None,
        "last_continuous_note_time": -999.0,
        "stable_note": None,
        "stable_count": 0,
        "broadcast_event_id": 0,
        "broadcast_event_notes": set(),
        "broadcast_event_onset_time": 0.0,
        "broadcast_event_until": 0.0,
        "last_rms": 0.0,
    }


def audio_callback(indata, frames, time_info, status):
    if status:
        _set_status(f"Audio warning: {status}")
    try:
        audio_queue.put_nowait(indata[:, 0].copy())
    except Exception:
        pass


def run_stream(selected_input_device, generation):
    global frames_processed, latest_input_name, running, last_input_level

    onset_detector = make_onset_detector()
    pitch_detector = make_pitch_detector()
    state = _make_detector_state()

    flush_audio_queue()
    unity_hint_state.reset()
    with frames_processed_lock:
        frames_processed = 0

    latest_input_name = get_input_device_name(selected_input_device)
    _set_status(f"Listening on {latest_input_name}")
    _set_last_error("")
    _set_packet("A|--|0|0.0000|")

    stream_kwargs = {
        "samplerate": SAMPLE_RATE,
        "channels": 1,
        "blocksize": HOP_S,
        "callback": audio_callback,
        "latency": "low",
    }
    if selected_input_device is not None:
        stream_kwargs["device"] = selected_input_device

    try:
        with sd.InputStream(**stream_kwargs):
            while not stop_event.is_set():
                try:
                    chunk = audio_queue.get(timeout=0.05)
                except queue.Empty:
                    continue

                chunk_float32 = chunk.astype(np.float32)
                with frames_processed_lock:
                    current_time = frames_processed / SAMPLE_RATE
                    frames_processed += len(chunk)

                for capture in state["active_captures"]:
                    capture["audio"].append(chunk)

                state["current_active_notes"] = update_continuous_notes(chunk_float32, current_time, pitch_detector, state)
                last_input_level = float(np.clip(state["last_rms"] * 18.0, 0.0, 1.0))

                is_onset = onset_detector(chunk_float32)
                if is_onset and (current_time - state["last_onset_time"]) > DEBOUNCE_SEC:
                    state["last_onset_time"] = current_time
                    state["pluck_counter"] += 1
                    state["active_captures"].append(
                        {
                            "id": state["pluck_counter"],
                            "audio": [chunk],
                            "onset_time": current_time,
                        }
                    )

                for capture in list(state["active_captures"]):
                    if sum(len(block) for block in capture["audio"]) >= CAPTURE_SAMPLES:
                        full_audio = np.concatenate(capture["audio"])
                        ai_task_queue.put((full_audio, capture["id"], capture["onset_time"], generation))
                        state["active_captures"].remove(capture)

                while not ai_result_queue.empty():
                    result = ai_result_queue.get()
                    if result.get("generation") != generation:
                        continue

                    state["broadcast_event_id"] = result["id"]
                    state["broadcast_event_notes"] = set(result["notes"])
                    state["broadcast_event_onset_time"] = result["onset_time"]
                    state["broadcast_event_until"] = current_time + EVENT_BROADCAST_SEC
                    if state["broadcast_event_notes"]:
                        _set_status(f"Event {result['id']} -> {','.join(sorted(state['broadcast_event_notes']))}")

                event_id_to_send = 0
                event_age_to_send = 0.0
                event_notes_to_send = set()
                if (
                    current_time <= state["broadcast_event_until"]
                    and state["broadcast_event_id"] > 0
                    and state["broadcast_event_notes"]
                ):
                    event_id_to_send = state["broadcast_event_id"]
                    event_age_to_send = max(0.0, current_time - state["broadcast_event_onset_time"])
                    event_notes_to_send = state["broadcast_event_notes"]

                active_csv = ",".join(sorted(state["current_active_notes"])) if state["current_active_notes"] else "--"
                event_csv = ",".join(sorted(event_notes_to_send)) if event_notes_to_send else ""
                packet = f"A|{active_csv}|{event_id_to_send}|{event_age_to_send:.4f}|{event_csv}|{last_input_level:.4f}"
                _set_packet(packet)
    except Exception as exc:
        _set_last_error(f"Detector stream error: {exc}")
        _set_status(f"Detector error: {exc}")
    finally:
        with runtime_lock:
            running = False
        _set_packet("A|--|0|0.0000|")


def initialize_runtime():
    ensure_ai_worker()
    _set_status("Detector runtime ready.")
    _set_last_error("")


def start_detector(input_device_index=-1):
    global detector_thread, running, session_generation, selected_input_device_index
    stop_detector()
    ensure_ai_worker()

    selected_input_device_index = None if input_device_index is None or int(input_device_index) < 0 else int(input_device_index)
    stop_event.clear()
    session_generation += 1
    generation = session_generation

    with runtime_lock:
        running = True

    detector_thread = threading.Thread(
        target=run_stream,
        args=(selected_input_device_index, generation),
        name="EmbeddedDetectorWorker",
        daemon=True,
    )
    detector_thread.start()
    return True


def stop_detector():
    global detector_thread, running
    stop_event.set()
    thread = detector_thread
    detector_thread = None
    if thread is not None and thread.is_alive():
        thread.join(timeout=1.5)
    with runtime_lock:
        running = False
    _set_status("Detector stopped.")
    _set_packet("A|--|0|0.0000|")
    return True


def set_hint_payload(payload):
    parse_unity_message(payload)
    return True


def poll_packet():
    with runtime_lock:
        return latest_packet


def get_status():
    with runtime_lock:
        if not running and last_error:
            return f"{latest_status} | {last_error}"
        return latest_status


def get_last_error():
    with runtime_lock:
        return last_error


def is_running():
    with runtime_lock:
        return bool(running)


def get_input_name():
    with runtime_lock:
        return latest_input_name
