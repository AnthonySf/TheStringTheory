//#define MPTK_PRO

namespace MidiPlayerTK
{
    public class Constant
    {
        public const string forumSite = "https://forum.unity.com/threads/midi-player-tool-kit-good-news-for-your-rhythm-game.526741/";
        public const string paxSite = "https://www.paxstellar.com";
        public const string demoSite = "https://paxstellar.fr/maestro-demos/";
        public const string apiSite = "https://mptkapi.paxstellar.com/annotated.html";
        public const string blogSite = "https://paxstellar.fr/midi-player-tool-kit-for-unity-v2/";
        public const string UnitySite = "https://assetstore.unity.com/packages/tools/audio/midi-tool-kit-pro-115331";
        public const string DiscordSite = "https://discord.gg/NhjXPTdeWk";
        public const string CustomGptSite = "https://discord.gg/33sAXkYqZ7";

#if MPTK_PRO
        public const string version = "2.19.0 Pro";
#else
        public const string version = "2.19.0 Free";
#endif
        public const string releaseDate = "Mar, 28 2026";


#if MPTK_UNLOCK_VOLUME
        public const float MAX_VOLUME = 10f;
#else
        public const float MAX_VOLUME = 1f;
#endif

#if MPTK_UNLOCK_SPEED
        public const float MIN_SPEED = 0.0001f;
        public const float MAX_SPEED = 100f;
#else
        public const float MIN_SPEED = 0.1f;
        public const float MAX_SPEED = 10f;
#endif

#if MPTK_MAESTRO_MENU_TOOLS 
        public const string MENU_MAESTRO = "Tools/Maestro";
#else
        public const string MENU_MAESTRO = "Maestro";
#endif
    }
}
