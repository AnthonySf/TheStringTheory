using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(AudioSource))]
public class GuitarEngine : MonoBehaviour
{
    [Header("UI Output")]
    public TextMeshProUGUI uiText;

    [Header("PitchPlease Config")]
    public int fftSize = 8192; // Unity max for high resolution
    public int stabilityFrames = 4;
    public float thresholdMultiplier = 3.0f;
    public int numHarmonics = 6;

    private AudioSource audioSource;
    private string selectedDevice;
    private float[] spectrum;
    private List<string> history = new List<string>();
    
    private readonly string[] NOTE_NAMES = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    private float noiseFloor = 0;

    // Chord Templates (Ported from JS)
    private struct ChordTemplate {
        public string name;
        public int[] intervals;
        public ChordTemplate(string n, int[] i) { name = n; intervals = i; }
    }

    private List<ChordTemplate> chordLibrary = new List<ChordTemplate> {
        new ChordTemplate("", new[] {4, 7}),         // Major
        new ChordTemplate("m", new[] {3, 7}),        // Minor
        new ChordTemplate("7", new[] {4, 7, 10}),
        new ChordTemplate("maj7", new[] {4, 7, 11}),
        new ChordTemplate("m7", new[] {3, 7, 10}),
        new ChordTemplate("sus4", new[] {5, 7}),
        new ChordTemplate("sus2", new[] {2, 7}),
        new ChordTemplate("5", new[] {7}),           // Power Chord
        new ChordTemplate("add9", new[] {2, 4, 7}),
        new ChordTemplate("dim", new[] {3, 6})
    };

    void Start() {
        spectrum = new float[fftSize];
        audioSource = GetComponent<AudioSource>();
        
        foreach (var device in Microphone.devices) {
            if (device.Contains("Rocksmith")) selectedDevice = device;
        }
        if (string.IsNullOrEmpty(selectedDevice)) selectedDevice = Microphone.devices[0];

        audioSource.clip = Microphone.Start(selectedDevice, true, 1, 48000);
        audioSource.loop = true;
        
        // IMPORTANT: Keep volume just above zero so Unity's analyzer stays awake
        audioSource.volume = 0.01f; 
        audioSource.mute = false;

        while (!(Microphone.GetPosition(selectedDevice) > 0)) { }
        audioSource.Play();
    }

    void Update() {
        if (string.IsNullOrEmpty(selectedDevice)) return;

        // 1. Get High-Res Spectrum
        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        // 2. Dynamic Noise Floor Logic
        float maxE = spectrum.Max();
        float currentSum = 0; int currentCnt = 0;
        for (int i = 0; i < spectrum.Length; i += 10) {
            if (spectrum[i] < maxE * 0.3f) { currentSum += spectrum[i]; currentCnt++; }
        }
        noiseFloor = (noiseFloor * 0.95f) + ((currentCnt > 0 ? currentSum / currentCnt : 0) * 0.05f);
        float threshold = Mathf.Max(0.0001f, noiseFloor * thresholdMultiplier);

        // 3. Peak Finding & Quadratic Interpolation (Sub-bin accuracy)
        List<float> peakMidis = new List<float>();
        List<float> peakEnergies = new List<float>();

        for (int i = 1; i < spectrum.Length - 1; i++) {
            float cur = spectrum[i], prev = spectrum[i-1], next = spectrum[i+1];
            if (cur > threshold && cur > prev && cur > next) {
                // Parabolic Interpolation math from PitchPlease
                float d = 2 * (prev - 2 * cur + next);
                float off = (Mathf.Abs(d) > 0.000001f) ? (prev - next) / d : 0;
                
                float freq = (i + off) * 48000f / fftSize;
                if (freq < 40) continue; // Ignore subsonic noise

                // Convert to MIDI (JS Formula: 12 * log2(hz / 8.175))
                float midi = 12 * Mathf.Log(freq / 8.17579f, 2);
                peakMidis.Add(midi);
                peakEnergies.Add(cur);
            }
        }

        // 4. Harmonic Sieve (The Fundamental Filter)
        List<int> fundamentals = new List<int>();
        for (int i = 0; i < peakMidis.Count; i++) {
            float m = peakMidis[i];
            if (m < 28 || m > 90) continue; // Electric guitar range E1 to F#6

            int hCount = 1;
            float score = peakEnergies[i];

            for (int n = 2; n <= numHarmonics; n++) {
                float expected = m + 12 * Mathf.Log(n, 2);
                for (int j = 0; j < peakMidis.Count; j++) {
                    if (Mathf.Abs(peakMidis[j] - expected) < 0.7f) {
                        score += peakEnergies[j] / n;
                        hCount++;
                        break;
                    }
                }
            }

            // Stricter check: fundamental must have harmonics or be very loud
            if (hCount >= 2 || (score / maxE) > 0.8f) {
                int pc = Mathf.RoundToInt(m) % 12;
                if (!fundamentals.Contains(pc)) fundamentals.Add(pc);
            }
        }

        // 5. Stability History (Removes flickering)
        fundamentals.Sort();
        string currentSig = string.Join(",", fundamentals);
        history.Insert(0, currentSig);
        if (history.Count > stabilityFrames) history.RemoveAt(stabilityFrames);

        bool isStable = history.Count >= stabilityFrames && history.All(h => h == history[0]);

        // 6. Output to UI
        if (isStable && fundamentals.Count > 0) {
            string chord = MatchChord(fundamentals);
            string notes = string.Join(", ", fundamentals.Select(n => NOTE_NAMES[n]));
            uiText.color = Color.green;
            uiText.text = $"<size=120%>{chord}</size>\nNotes: {notes}";
        } else if (maxE < 0.0001f) {
            uiText.color = Color.gray;
            uiText.text = "Listening...";
        }
    }

    string MatchChord(List<int> pcs) {
        if (pcs.Count == 1) return NOTE_NAMES[pcs[0]];

        foreach (int root in pcs) {
            // Find intervals relative to this root
            List<int> intervals = pcs.Select(p => (p - root + 12) % 12)
                                     .Where(v => v > 0).Distinct().OrderBy(v => v).ToList();

            foreach (var template in chordLibrary) {
                // Check if the played notes satisfy the template (can have extra notes)
                if (template.intervals.All(i => intervals.Contains(i))) {
                    return NOTE_NAMES[root] + template.name;
                }
            }
        }
        return string.Join("-", pcs.Select(p => NOTE_NAMES[p]));
    }
}