import os
import sys
import traceback
from pathlib import Path

APP_DIR = Path(__file__).resolve().parent
LOG_FILE = APP_DIR / "tonelab_log.txt"
SETTINGS_FILE = APP_DIR / "tone.json"

def log(msg: str):
    try:
        print(msg, flush=True)
    except Exception:
        pass
    try:
        with open(LOG_FILE, "a", encoding="utf-8") as f:
            f.write(msg + "\n")
    except Exception:
        pass

log("=== ToneLab launch ===")
log(f"Python exe: {sys.executable}")
log(f"Script path: {__file__}")
log(f"CWD: {os.getcwd()}")

try:
    import json
    import threading
    import numpy as np
    import sounddevice as sd
    import customtkinter as ctk

    from pedalboard import (
        Pedalboard,
        Distortion,
        Chorus,
        Phaser,
        Delay,
        Reverb,
        Compressor,
        Gain,
    )

    log("All imports succeeded")

except Exception:
    log("Import failure:")
    log(traceback.format_exc())
    raise
    

# ----------------------------
# Settings
# ----------------------------

def default_settings():
    return {
        "input_device_name": "",
        "output_device_name": "",
        "input_gain_db": 0.0,
        "output_gain_db": 0.0,
        "dist_enabled": True,
        "chorus_enabled": False,
        "phaser_enabled": False,
        "delay_enabled": False,
        "reverb_enabled": True,
        "comp_enabled": False,
        "dist_drive_db": 18.0,
        "chorus_rate_hz": 0.8,
        "chorus_depth": 0.35,
        "chorus_mix": 0.25,
        "phaser_rate_hz": 0.5,
        "phaser_depth": 0.5,
        "phaser_mix": 0.25,
        "phaser_center_hz": 1200.0,
        "phaser_feedback": 0.1,
        "delay_seconds": 0.28,
        "delay_feedback": 0.30,
        "delay_mix": 0.20,
        "reverb_room_size": 0.45,
        "reverb_damping": 0.35,
        "reverb_wet": 0.22,
        "reverb_dry": 0.95,
        "reverb_width": 1.0,
        "reverb_freeze": 0.0,
        "comp_threshold_db": -18.0,
        "comp_ratio": 3.0,
        "comp_attack_ms": 10.0,
        "comp_release_ms": 120.0,
    }


def load_settings():
    if not os.path.exists(SETTINGS_FILE):
        data = default_settings()
        save_settings(data)
        return data

    try:
        with open(SETTINGS_FILE, "r", encoding="utf-8") as f:
            loaded = json.load(f)

        data = default_settings()
        data.update(loaded)
        return data
    except Exception:
        traceback.print_exc()
        data = default_settings()
        save_settings(data)
        return data


def save_settings(data):
    try:
        with open(SETTINGS_FILE, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2)
    except Exception:
        traceback.print_exc()


# ----------------------------
# Audio Engine
# ----------------------------

class AudioEngine:
    def __init__(self, settings):
        self.sample_rate = None
        self.block_size = 128
        self.stream = None
        self.lock = threading.Lock()

        self.input_gain_db = settings["input_gain_db"]
        self.output_gain_db = settings["output_gain_db"]

        self.dist_enabled = settings["dist_enabled"]
        self.chorus_enabled = settings["chorus_enabled"]
        self.phaser_enabled = settings["phaser_enabled"]
        self.delay_enabled = settings["delay_enabled"]
        self.reverb_enabled = settings["reverb_enabled"]
        self.comp_enabled = settings["comp_enabled"]

        self.dist_drive_db = settings["dist_drive_db"]

        self.chorus_rate_hz = settings["chorus_rate_hz"]
        self.chorus_depth = settings["chorus_depth"]
        self.chorus_mix = settings["chorus_mix"]

        self.phaser_rate_hz = settings["phaser_rate_hz"]
        self.phaser_depth = settings["phaser_depth"]
        self.phaser_mix = settings["phaser_mix"]
        self.phaser_center_hz = settings["phaser_center_hz"]
        self.phaser_feedback = settings["phaser_feedback"]

        self.delay_seconds = settings["delay_seconds"]
        self.delay_feedback = settings["delay_feedback"]
        self.delay_mix = settings["delay_mix"]

        self.reverb_room_size = settings["reverb_room_size"]
        self.reverb_damping = settings["reverb_damping"]
        self.reverb_wet = settings["reverb_wet"]
        self.reverb_dry = settings["reverb_dry"]
        self.reverb_width = settings["reverb_width"]
        self.reverb_freeze = settings["reverb_freeze"]

        self.comp_threshold_db = settings["comp_threshold_db"]
        self.comp_ratio = settings["comp_ratio"]
        self.comp_attack_ms = settings["comp_attack_ms"]
        self.comp_release_ms = settings["comp_release_ms"]

        self.rebuild_board()

    def rebuild_board(self):
        chain = [Gain(gain_db=self.input_gain_db)]

        if self.dist_enabled:
            chain.append(Distortion(drive_db=self.dist_drive_db))

        if self.chorus_enabled:
            chain.append(
                Chorus(
                    rate_hz=self.chorus_rate_hz,
                    depth=self.chorus_depth,
                    mix=self.chorus_mix,
                )
            )

        if self.phaser_enabled:
            chain.append(
                Phaser(
                    rate_hz=self.phaser_rate_hz,
                    depth=self.phaser_depth,
                    centre_frequency_hz=self.phaser_center_hz,
                    feedback=self.phaser_feedback,
                    mix=self.phaser_mix,
                )
            )

        if self.delay_enabled:
            chain.append(
                Delay(
                    delay_seconds=self.delay_seconds,
                    feedback=self.delay_feedback,
                    mix=self.delay_mix,
                )
            )

        if self.reverb_enabled:
            chain.append(
                Reverb(
                    room_size=self.reverb_room_size,
                    damping=self.reverb_damping,
                    wet_level=self.reverb_wet,
                    dry_level=self.reverb_dry,
                    width=self.reverb_width,
                    freeze_mode=self.reverb_freeze,
                )
            )

        if self.comp_enabled:
            chain.append(
                Compressor(
                    threshold_db=self.comp_threshold_db,
                    ratio=self.comp_ratio,
                    attack_ms=self.comp_attack_ms,
                    release_ms=self.comp_release_ms,
                )
            )

        chain.append(Gain(gain_db=self.output_gain_db))
        self.board = Pedalboard(chain)

    def update(self):
        with self.lock:
            self.rebuild_board()

    def start(self, input_device=None, output_device=None):
        if self.stream is not None:
            return

        in_info = sd.query_devices(input_device)
        out_info = sd.query_devices(output_device)

        print("Input device:", in_info["name"])
        print("Output device:", out_info["name"])

        self.sample_rate = int(min(in_info["default_samplerate"], out_info["default_samplerate"]))
        self.block_size = 128

        print(
            f"Starting stream: sr={self.sample_rate}, block={self.block_size}, "
            f"in={input_device}, out={output_device}"
        )

        self.stream = sd.Stream(
            samplerate=self.sample_rate,
            blocksize=self.block_size,
            channels=1,
            dtype="float32",
            callback=self.audio_callback,
            device=(input_device, output_device),
            latency="low",
        )
        self.stream.start()
        print("Audio stream started")

    def stop(self):
        if self.stream is not None:
            try:
                self.stream.stop()
                self.stream.close()
            finally:
                self.stream = None

    def audio_callback(self, indata, outdata, frames, time_info, status):
        if status:
            print(status, flush=True)

        try:
            mono = np.copy(indata[:, 0])
            with self.lock:
                processed = self.board(mono, self.sample_rate, reset=False)

            processed = np.asarray(processed, dtype=np.float32).reshape(-1)

            if len(processed) < frames:
                outdata[:, 0] = 0
                outdata[:len(processed), 0] = processed
            else:
                outdata[:, 0] = processed[:frames]

        except Exception:
            traceback.print_exc()
            outdata.fill(0)


# ----------------------------
# UI Helpers
# ----------------------------

class SliderControl(ctk.CTkFrame):
    def __init__(self, master, title, from_, to, initial, callback, scale=1.0, suffix=""):
        super().__init__(master, fg_color="transparent")
        self.callback = callback
        self.scale = scale
        self.suffix = suffix

        self.title = ctk.CTkLabel(self, text=title, font=ctk.CTkFont(size=13, weight="bold"))
        self.title.pack(pady=(0, 4))

        self.value_label = ctk.CTkLabel(self, text="")
        self.value_label.pack(pady=(0, 4))

        steps = int(round(to - from_)) if abs(to - from_) >= 1 else 100
        self.slider = ctk.CTkSlider(self, from_=from_, to=to, number_of_steps=steps)
        self.slider.pack(fill="x", padx=4)

        self.slider.configure(command=self._on_change)
        self.set_value(initial)

    def _on_change(self, raw):
        value = float(raw) / self.scale
        self.value_label.configure(text=f"{value:.2f}{self.suffix}")
        self.callback(value)

    def set_value(self, actual_value):
        raw = actual_value * self.scale
        self.slider.set(raw)
        self.value_label.configure(text=f"{actual_value:.2f}{self.suffix}")


class EffectCard(ctk.CTkFrame):
    def __init__(self, master, title, color):
        super().__init__(master, corner_radius=16, fg_color=color)
        self.grid_columnconfigure((0, 1, 2), weight=1)

        header = ctk.CTkFrame(self, fg_color="transparent")
        header.pack(fill="x", padx=12, pady=(12, 6))

        self.title = ctk.CTkLabel(
            header, text=title, font=ctk.CTkFont(size=18, weight="bold")
        )
        self.title.pack(side="left")

        self.enabled = ctk.CTkSwitch(header, text="On")
        self.enabled.pack(side="right")

        self.controls = ctk.CTkFrame(self, fg_color="transparent")
        self.controls.pack(fill="both", expand=True, padx=12, pady=(0, 12))

    def add_control(self, control, row, col):
        control.grid(in_=self.controls, row=row, column=col, padx=8, pady=8, sticky="nsew")
        self.controls.grid_columnconfigure(col, weight=1)


# ----------------------------
# Main App
# ----------------------------

class GuitarRigApp(ctk.CTk):
    def __init__(self):
        super().__init__()

        ctk.set_appearance_mode("dark")
        ctk.set_default_color_theme("dark-blue")

        self.settings_data = load_settings()
        self.engine = AudioEngine(self.settings_data)

        self.title("Tone Lab")
        self.geometry("1350x820")
        self.minsize(1100, 700)

        self.input_devices = []
        self.output_devices = []

        self.build_ui()
        self.load_devices()

    def build_ui(self):
        self.grid_columnconfigure(0, weight=1)
        self.grid_rowconfigure(2, weight=1)

        top = ctk.CTkFrame(self, corner_radius=18, fg_color="#111827")
        top.grid(row=0, column=0, sticky="ew", padx=16, pady=(16, 10))
        top.grid_columnconfigure(1, weight=1)

        title_wrap = ctk.CTkFrame(top, fg_color="transparent")
        title_wrap.grid(row=0, column=0, sticky="w", padx=16, pady=16)

        ctk.CTkLabel(
            title_wrap,
            text="Tone Lab",
            font=ctk.CTkFont(size=30, weight="bold"),
        ).pack(anchor="w")

        ctk.CTkLabel(
            title_wrap,
            text="Live guitar rig with built-in Pedalboard effects",
            text_color="#9ca3af",
            font=ctk.CTkFont(size=14),
        ).pack(anchor="w", pady=(2, 0))

        device_wrap = ctk.CTkFrame(top, fg_color="transparent")
        device_wrap.grid(row=0, column=1, sticky="e", padx=16, pady=16)

        ctk.CTkLabel(device_wrap, text="Input").grid(row=0, column=0, padx=6, pady=4)
        self.input_menu = ctk.CTkOptionMenu(
            device_wrap,
            values=["Loading..."],
            width=260,
            command=self.on_input_changed,
        )
        self.input_menu.grid(row=0, column=1, padx=6, pady=4)

        ctk.CTkLabel(device_wrap, text="Output").grid(row=0, column=2, padx=6, pady=4)
        self.output_menu = ctk.CTkOptionMenu(
            device_wrap,
            values=["Loading..."],
            width=260,
            command=self.on_output_changed,
        )
        self.output_menu.grid(row=0, column=3, padx=6, pady=4)

        self.refresh_btn = ctk.CTkButton(device_wrap, text="Refresh Devices", command=self.load_devices)
        self.refresh_btn.grid(row=0, column=4, padx=6, pady=4)

        self.start_btn = ctk.CTkButton(device_wrap, text="Start Audio", command=self.start_audio)
        self.start_btn.grid(row=0, column=5, padx=6, pady=4)

        self.stop_btn = ctk.CTkButton(device_wrap, text="Stop Audio", command=self.stop_audio)
        self.stop_btn.grid(row=0, column=6, padx=6, pady=4)

        global_frame = ctk.CTkFrame(self, corner_radius=18, fg_color="#111827")
        global_frame.grid(row=1, column=0, sticky="ew", padx=16, pady=(0, 10))
        global_frame.grid_columnconfigure((0, 1), weight=1)

        ctk.CTkLabel(
            global_frame,
            text="Master",
            font=ctk.CTkFont(size=18, weight="bold"),
        ).pack(anchor="w", padx=16, pady=(12, 0))

        global_controls = ctk.CTkFrame(global_frame, fg_color="transparent")
        global_controls.pack(fill="x", padx=12, pady=12)

        self.input_gain_slider = SliderControl(
            global_controls,
            "Input",
            -24,
            24,
            self.engine.input_gain_db,
            self.set_input_gain,
            scale=1.0,
            suffix=" dB",
        )
        self.input_gain_slider.pack(side="left", fill="x", expand=True, padx=8)

        self.output_gain_slider = SliderControl(
            global_controls,
            "Output",
            -24,
            24,
            self.engine.output_gain_db,
            self.set_output_gain,
            scale=1.0,
            suffix=" dB",
        )
        self.output_gain_slider.pack(side="left", fill="x", expand=True, padx=8)

        effects_wrap = ctk.CTkFrame(self, fg_color="transparent")
        effects_wrap.grid(row=2, column=0, sticky="nsew", padx=16, pady=(0, 16))
        effects_wrap.grid_columnconfigure((0, 1, 2), weight=1)
        effects_wrap.grid_rowconfigure((0, 1), weight=1)

        dist = EffectCard(effects_wrap, "Distortion", "#3b1f2b")
        dist.grid(row=0, column=0, sticky="nsew", padx=8, pady=8)
        if self.engine.dist_enabled:
            dist.enabled.select()
        else:
            dist.enabled.deselect()
        dist.enabled.configure(command=lambda: self.toggle_effect("dist", dist.enabled.get()))
        self.dist_drive_slider = SliderControl(
            dist.controls, "Drive", 0, 36, self.engine.dist_drive_db, self.set_dist_drive, suffix=" dB"
        )
        dist.add_control(self.dist_drive_slider, 0, 0)

        chorus = EffectCard(effects_wrap, "Chorus", "#1f3145")
        chorus.grid(row=0, column=1, sticky="nsew", padx=8, pady=8)
        if self.engine.chorus_enabled:
            chorus.enabled.select()
        else:
            chorus.enabled.deselect()
        chorus.enabled.configure(command=lambda: self.toggle_effect("chorus", chorus.enabled.get()))
        self.chorus_rate_slider = SliderControl(
            chorus.controls, "Rate", 1, 400, self.engine.chorus_rate_hz, self.set_chorus_rate, scale=100.0, suffix=" Hz"
        )
        self.chorus_depth_slider = SliderControl(
            chorus.controls, "Depth", 0, 100, self.engine.chorus_depth, self.set_chorus_depth, scale=100.0
        )
        self.chorus_mix_slider = SliderControl(
            chorus.controls, "Mix", 0, 100, self.engine.chorus_mix, self.set_chorus_mix, scale=100.0
        )
        chorus.add_control(self.chorus_rate_slider, 0, 0)
        chorus.add_control(self.chorus_depth_slider, 0, 1)
        chorus.add_control(self.chorus_mix_slider, 0, 2)

        phaser = EffectCard(effects_wrap, "Phaser", "#2a2345")
        phaser.grid(row=0, column=2, sticky="nsew", padx=8, pady=8)
        if self.engine.phaser_enabled:
            phaser.enabled.select()
        else:
            phaser.enabled.deselect()
        phaser.enabled.configure(command=lambda: self.toggle_effect("phaser", phaser.enabled.get()))
        self.phaser_rate_slider = SliderControl(
            phaser.controls, "Rate", 1, 300, self.engine.phaser_rate_hz, self.set_phaser_rate, scale=100.0, suffix=" Hz"
        )
        self.phaser_depth_slider = SliderControl(
            phaser.controls, "Depth", 0, 100, self.engine.phaser_depth, self.set_phaser_depth, scale=100.0
        )
        self.phaser_mix_slider = SliderControl(
            phaser.controls, "Mix", 0, 100, self.engine.phaser_mix, self.set_phaser_mix, scale=100.0
        )
        phaser.add_control(self.phaser_rate_slider, 0, 0)
        phaser.add_control(self.phaser_depth_slider, 0, 1)
        phaser.add_control(self.phaser_mix_slider, 0, 2)

        delay = EffectCard(effects_wrap, "Delay", "#3b2b1f")
        delay.grid(row=1, column=0, sticky="nsew", padx=8, pady=8)
        if self.engine.delay_enabled:
            delay.enabled.select()
        else:
            delay.enabled.deselect()
        delay.enabled.configure(command=lambda: self.toggle_effect("delay", delay.enabled.get()))
        self.delay_time_slider = SliderControl(
            delay.controls, "Time", 1, 120, self.engine.delay_seconds, self.set_delay_time, scale=100.0, suffix=" s"
        )
        self.delay_feedback_slider = SliderControl(
            delay.controls, "FB", 0, 95, self.engine.delay_feedback, self.set_delay_feedback, scale=100.0
        )
        self.delay_mix_slider = SliderControl(
            delay.controls, "Mix", 0, 100, self.engine.delay_mix, self.set_delay_mix, scale=100.0
        )
        delay.add_control(self.delay_time_slider, 0, 0)
        delay.add_control(self.delay_feedback_slider, 0, 1)
        delay.add_control(self.delay_mix_slider, 0, 2)

        reverb = EffectCard(effects_wrap, "Reverb", "#1f3a2a")
        reverb.grid(row=1, column=1, sticky="nsew", padx=8, pady=8)
        if self.engine.reverb_enabled:
            reverb.enabled.select()
        else:
            reverb.enabled.deselect()
        reverb.enabled.configure(command=lambda: self.toggle_effect("reverb", reverb.enabled.get()))
        self.reverb_room_slider = SliderControl(
            reverb.controls, "Room", 0, 100, self.engine.reverb_room_size, self.set_reverb_room, scale=100.0
        )
        self.reverb_damping_slider = SliderControl(
            reverb.controls, "Damp", 0, 100, self.engine.reverb_damping, self.set_reverb_damping, scale=100.0
        )
        self.reverb_wet_slider = SliderControl(
            reverb.controls, "Wet", 0, 100, self.engine.reverb_wet, self.set_reverb_wet, scale=100.0
        )
        reverb.add_control(self.reverb_room_slider, 0, 0)
        reverb.add_control(self.reverb_damping_slider, 0, 1)
        reverb.add_control(self.reverb_wet_slider, 0, 2)

        comp = EffectCard(effects_wrap, "Compressor", "#2f2f2f")
        comp.grid(row=1, column=2, sticky="nsew", padx=8, pady=8)
        if self.engine.comp_enabled:
            comp.enabled.select()
        else:
            comp.enabled.deselect()
        comp.enabled.configure(command=lambda: self.toggle_effect("comp", comp.enabled.get()))
        self.comp_threshold_slider = SliderControl(
            comp.controls, "Thresh", -40, 0, self.engine.comp_threshold_db, self.set_comp_threshold, suffix=" dB"
        )
        self.comp_ratio_slider = SliderControl(
            comp.controls, "Ratio", 10, 100, self.engine.comp_ratio, self.set_comp_ratio, scale=10.0
        )
        comp.add_control(self.comp_threshold_slider, 0, 0)
        comp.add_control(self.comp_ratio_slider, 0, 1)

        self.status_label = ctk.CTkLabel(
            self,
            text="Ready",
            text_color="#9ca3af",
            font=ctk.CTkFont(size=13),
        )
        self.status_label.grid(row=3, column=0, sticky="w", padx=20, pady=(0, 12))

    def current_settings(self):
        return {
            "input_device_name": self.input_menu.get() if hasattr(self, "input_menu") else "",
            "output_device_name": self.output_menu.get() if hasattr(self, "output_menu") else "",
            "input_gain_db": self.engine.input_gain_db,
            "output_gain_db": self.engine.output_gain_db,
            "dist_enabled": self.engine.dist_enabled,
            "chorus_enabled": self.engine.chorus_enabled,
            "phaser_enabled": self.engine.phaser_enabled,
            "delay_enabled": self.engine.delay_enabled,
            "reverb_enabled": self.engine.reverb_enabled,
            "comp_enabled": self.engine.comp_enabled,
            "dist_drive_db": self.engine.dist_drive_db,
            "chorus_rate_hz": self.engine.chorus_rate_hz,
            "chorus_depth": self.engine.chorus_depth,
            "chorus_mix": self.engine.chorus_mix,
            "phaser_rate_hz": self.engine.phaser_rate_hz,
            "phaser_depth": self.engine.phaser_depth,
            "phaser_mix": self.engine.phaser_mix,
            "phaser_center_hz": self.engine.phaser_center_hz,
            "phaser_feedback": self.engine.phaser_feedback,
            "delay_seconds": self.engine.delay_seconds,
            "delay_feedback": self.engine.delay_feedback,
            "delay_mix": self.engine.delay_mix,
            "reverb_room_size": self.engine.reverb_room_size,
            "reverb_damping": self.engine.reverb_damping,
            "reverb_wet": self.engine.reverb_wet,
            "reverb_dry": self.engine.reverb_dry,
            "reverb_width": self.engine.reverb_width,
            "reverb_freeze": self.engine.reverb_freeze,
            "comp_threshold_db": self.engine.comp_threshold_db,
            "comp_ratio": self.engine.comp_ratio,
            "comp_attack_ms": self.engine.comp_attack_ms,
            "comp_release_ms": self.engine.comp_release_ms,
        }

    def persist_settings(self):
        save_settings(self.current_settings())

    def load_devices(self):
        try:
            devices = sd.query_devices()
            self.input_devices = []
            self.output_devices = []

            input_names = []
            output_names = []

            for i, d in enumerate(devices):
                name = f"{i}: {d['name']}"
                hostapi_name = sd.query_hostapis(d["hostapi"])["name"]

                if d["max_input_channels"] > 0 and hostapi_name == "Windows WASAPI":
                    self.input_devices.append((name, i))
                    input_names.append(name)

                if d["max_output_channels"] > 0 and hostapi_name == "Windows WASAPI":
                    self.output_devices.append((name, i))
                    output_names.append(name)

            if not input_names:
                for i, d in enumerate(devices):
                    name = f"{i}: {d['name']}"
                    if d["max_input_channels"] > 0:
                        self.input_devices.append((name, i))
                        input_names.append(name)

            if not output_names:
                for i, d in enumerate(devices):
                    name = f"{i}: {d['name']}"
                    if d["max_output_channels"] > 0:
                        self.output_devices.append((name, i))
                        output_names.append(name)

            if not input_names:
                input_names = ["No input devices"]
            if not output_names:
                output_names = ["No output devices"]

            self.input_menu.configure(values=input_names)
            self.output_menu.configure(values=output_names)

            saved_input = self.settings_data.get("input_device_name", "")
            saved_output = self.settings_data.get("output_device_name", "")

            if saved_input in input_names:
                self.input_menu.set(saved_input)
            else:
                self.input_menu.set(input_names[0])

            if saved_output in output_names:
                self.output_menu.set(saved_output)
            else:
                self.output_menu.set(output_names[0])

            self.persist_settings()
            self.status_label.configure(text="Audio devices loaded")
        except Exception as e:
            self.status_label.configure(text=f"Device load error: {e}")
            traceback.print_exc()

    def get_selected_device_index(self, menu_value, device_list):
        for name, idx in device_list:
            if name == menu_value:
                return idx
        return None

    def start_audio(self):
        try:
            input_idx = self.get_selected_device_index(self.input_menu.get(), self.input_devices)
            output_idx = self.get_selected_device_index(self.output_menu.get(), self.output_devices)

            print("Selected input:", input_idx, self.input_menu.get())
            print("Selected output:", output_idx, self.output_menu.get())

            self.engine.start(input_idx, output_idx)
            self.persist_settings()
            self.status_label.configure(text="Audio started")
        except Exception as e:
            self.status_label.configure(text=f"Start error: {e}")
            traceback.print_exc()

    def stop_audio(self):
        try:
            self.engine.stop()
            self.persist_settings()
            self.status_label.configure(text="Audio stopped")
        except Exception as e:
            self.status_label.configure(text=f"Stop error: {e}")
            traceback.print_exc()

    def on_input_changed(self, _value):
        self.persist_settings()

    def on_output_changed(self, _value):
        self.persist_settings()

    def toggle_effect(self, effect_name, value):
        enabled = bool(value)

        if effect_name == "dist":
            self.engine.dist_enabled = enabled
        elif effect_name == "chorus":
            self.engine.chorus_enabled = enabled
        elif effect_name == "phaser":
            self.engine.phaser_enabled = enabled
        elif effect_name == "delay":
            self.engine.delay_enabled = enabled
        elif effect_name == "reverb":
            self.engine.reverb_enabled = enabled
        elif effect_name == "comp":
            self.engine.comp_enabled = enabled

        self.engine.update()
        self.persist_settings()

    def set_input_gain(self, value):
        self.engine.input_gain_db = value
        self.engine.update()
        self.persist_settings()

    def set_output_gain(self, value):
        self.engine.output_gain_db = value
        self.engine.update()
        self.persist_settings()

    def set_dist_drive(self, value):
        self.engine.dist_drive_db = value
        self.engine.update()
        self.persist_settings()

    def set_chorus_rate(self, value):
        self.engine.chorus_rate_hz = value
        self.engine.update()
        self.persist_settings()

    def set_chorus_depth(self, value):
        self.engine.chorus_depth = value
        self.engine.update()
        self.persist_settings()

    def set_chorus_mix(self, value):
        self.engine.chorus_mix = value
        self.engine.update()
        self.persist_settings()

    def set_phaser_rate(self, value):
        self.engine.phaser_rate_hz = value
        self.engine.update()
        self.persist_settings()

    def set_phaser_depth(self, value):
        self.engine.phaser_depth = value
        self.engine.update()
        self.persist_settings()

    def set_phaser_mix(self, value):
        self.engine.phaser_mix = value
        self.engine.update()
        self.persist_settings()

    def set_delay_time(self, value):
        self.engine.delay_seconds = value
        self.engine.update()
        self.persist_settings()

    def set_delay_feedback(self, value):
        self.engine.delay_feedback = value
        self.engine.update()
        self.persist_settings()

    def set_delay_mix(self, value):
        self.engine.delay_mix = value
        self.engine.update()
        self.persist_settings()

    def set_reverb_room(self, value):
        self.engine.reverb_room_size = value
        self.engine.update()
        self.persist_settings()

    def set_reverb_damping(self, value):
        self.engine.reverb_damping = value
        self.engine.update()
        self.persist_settings()

    def set_reverb_wet(self, value):
        self.engine.reverb_wet = value
        self.engine.update()
        self.persist_settings()

    def set_comp_threshold(self, value):
        self.engine.comp_threshold_db = value
        self.engine.update()
        self.persist_settings()

    def set_comp_ratio(self, value):
        self.engine.comp_ratio = value
        self.engine.update()
        self.persist_settings()

    def on_close(self):
        self.engine.stop()
        self.persist_settings()
        self.destroy()


if __name__ == "__main__":
    try:
        log("Creating app...")
        app = GuitarRigApp()
        app.protocol("WM_DELETE_WINDOW", app.on_close)
        log("Entering mainloop")
        app.mainloop()
        log("Mainloop ended normally")
    except Exception:
        log("Fatal startup/runtime error:")
        log(traceback.format_exc())
        raise