using System;
using UnityEngine;

public class ReadmeLite : ScriptableObject
{
    public Texture2D icon;
    public string title;
    public SectionLite[] sections;
    public bool loadedLayout;

    [Serializable]
    public class SectionLite
    {
        public string heading, text, linkText, url;
    }
}
