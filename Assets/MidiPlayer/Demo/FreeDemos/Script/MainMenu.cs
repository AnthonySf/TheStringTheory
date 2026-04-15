using MidiPlayerTK;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemoMPTK
{
    public class MainMenu : MonoBehaviour
    {
        static private Texture buttonIconHome;
        static private Texture buttonIconMPTK;
        static private Texture buttonIconQuit;
        static private Texture buttonIconHelp;

        public void Awake()
        {
        }

        public void GotoWeb(string uri)
        {
            Application.OpenURL(uri);
        }

        public void Quit()
        {
            if (!Application.isEditor)
            {
                MidiPlayerGlobal.MPTK_Stop();
                Application.Quit();
                //for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                //{
                //    Debug.Log(SceneUtility.GetScenePathByBuildIndex(i));

                //    if (SceneUtility.GetScenePathByBuildIndex(i).Contains("ScenesDemonstration"))
                //    {
                //        SceneManager.LoadScene(i, LoadSceneMode.Single);
                //        return;
                //    }
                //}
            }
#if UNITY_EDITOR
            else
                UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        static public void Display(string title, CustomStyle myStyle, float width, string helpUrl = null)
        {
            if (buttonIconHome == null) buttonIconHome = Resources.Load<Texture2D>("Textures/home");
            if (buttonIconQuit == null) buttonIconQuit = Resources.Load<Texture2D>("Textures/quit");
            if (buttonIconMPTK == null) buttonIconMPTK = Resources.Load<Texture2D>("Logo_MPTK");
            if (buttonIconHelp == null) buttonIconHelp = Resources.Load<Texture2D>("Textures/help-icon");

            GUILayout.BeginHorizontal(myStyle.BacgDemosMedium, GUILayout.Width(width));

            if (Application.isMobilePlatform)
                // Often, corner are rounded ...
                GUILayout.Space(20);

            if (GUILayout.Button(new GUIContent(buttonIconHome, "Go to main menu"), GUILayout.Width(60), GUILayout.Height(60)))
                for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
                    if (SceneUtility.GetScenePathByBuildIndex(i).Contains("ScenesDemonstration"))
                    {
                        MidiPlayerGlobal.MPTK_Stop();
                        SceneManager.LoadScene(i, LoadSceneMode.Single);
                        return;
                    }

            if (helpUrl != null)
            {
                if (GUILayout.Button(new GUIContent(buttonIconHelp, "Help"), GUILayout.Width(60), GUILayout.Height(60)))
                    Application.OpenURL(helpUrl);
            }
            GUILayout.BeginVertical(myStyle.BacgDemosMedium/*, GUILayout.Width(width)*/);
            GUILayout.Label(title, myStyle.TitleLabel1Centered, GUILayout.Height(30));
            if (GUILayout.Button(
                "MPTK demos are built using IMGUI. However for a more accurate playing, design your project with others Unity UI API. Click for comparing.",
                myStyle.TitleLabel3Centered, /*GUILayout.Width(width-60-60-60-60),*/ GUILayout.Height(25)))
                Application.OpenURL("https://docs.unity3d.com/2023.3/Documentation/Manual/UI-system-compare.html");
            GUILayout.EndVertical();

            if (GUILayout.Button(new GUIContent(buttonIconMPTK, "Go to web site"), GUILayout.Width(60), GUILayout.Height(60)))
                Application.OpenURL("https://paxstellar.fr/");

            if (GUILayout.Button(new GUIContent(buttonIconQuit, "Exit"), GUILayout.Width(60), GUILayout.Height(60)))
                MidiPlayerGlobal.MPTK_Quit();

            if (Application.isMobilePlatform)
                // Often, corner are rounded ...
                GUILayout.Space(20);
            GUILayout.EndHorizontal();
        }

        public void GoToMainMenu()
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                Debug.Log("Try loading ScenesDemonstration, index:" + SceneUtility.GetScenePathByBuildIndex(i));

                if (SceneUtility.GetScenePathByBuildIndex(i).Contains("ScenesDemonstration"))
                {
                    //Debug.Log("   load " + i + " " + SceneUtility.GetScenePathByBuildIndex(i));

                    SceneManager.LoadScene(i, LoadSceneMode.Single);
                    return;
                }
            }
            //int index = SceneUtility.GetBuildIndexByScenePath(sceneMainMenu);
            //Debug.Log(sceneMainMenu + " " + index);
            //if (index < 0)
            //{
            //    Debug.LogWarning("To avoid interacting with your project, MPTK doesn't add MPTK scenes in the Build Settings.");
            //    Debug.LogWarning("Add these scenes with \"File/Build Settings\" if you want full demonstrator functionality.");
            //}
            //else
            //    SceneManager.LoadScene(index, LoadSceneMode.Single);
        }
    }
}
