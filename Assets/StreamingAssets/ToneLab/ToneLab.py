import json
import os

ROOT = os.path.dirname(os.path.abspath(__file__))
TONE_JSON = os.path.join(ROOT, "tone.json")

if __name__ == "__main__":
    if os.path.exists(TONE_JSON):
        with open(TONE_JSON, "r", encoding="utf-8") as f:
            data = json.load(f)
        print("Tone Lab loaded tone.json:", data)
    else:
        print("tone.json missing at", TONE_JSON)
