//
// MPTK Readme 
// https://assetstore.unity.com/packages/tools/audio/midi-tool-kit-free-107994
//
using System;
using UnityEngine;
namespace MidiPlayerTK
{
    public class Readme : ScriptableObject
    {
        public Texture2D icon;
        public string title;
        public Section[] sections;
        public bool loadedLayout;

        [Serializable]
        public class Section
        {
            public string heading, text, linkText, url;
        }
    }
}
