using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TcgEngine
{
    //Script to manage transitions between scenes
    public class SceneNav
    {
        public static void RestartLevel()
        {
            GoTo(SceneManager.GetActiveScene().name);
        }

        public static void GoTo(string scene)
        {
            // Runtime callbacks must not navigate the editor after Play Mode has ended.
            if (!Application.isPlaying) return;
            SceneManager.LoadScene(scene);
        }

        public static string GetCurrentScene()
        {
            return SceneManager.GetActiveScene().name;
        }

        public static bool DoSceneExist(string scene)
        {
            return Application.CanStreamedLevelBeLoaded(scene);
        }
    }
}
