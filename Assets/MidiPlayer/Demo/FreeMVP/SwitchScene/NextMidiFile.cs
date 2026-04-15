using MidiPlayerTK;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemoMVPSwitchScene
{

    public class NextMidiFile : MonoBehaviour
    {
        private void Awake()
        {
        }

        // Start is called before the first frame update
        void Start()
        {
        }

        public void NextMidi()
        {
            if (LoadMidiFilePlayer.Instance.midiFilePlayer != null)
            {
                // Access to the MidiFilePlayer from the static class and its singleton instance
                LoadMidiFilePlayer.Instance.midiFilePlayer.MPTK_Next();
                Debug.Log($"Next MIDI file: {LoadMidiFilePlayer.Instance.midiFilePlayer.MPTK_MidiName}");
            }
            else
                Debug.LogWarning("No MidiFilePlayer found");

        }

        // Update is called once per frame
        void Update()
        {
        }
    }
}
