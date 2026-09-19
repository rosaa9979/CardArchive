using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TcgEngine.Replay
{
    public static class ReplaySession
    {
        public const string ToolPath = "Assets/TcgEngine/Resources/Scenes/Tool/ReplayTool.unity";
        public const string GamePath = "Assets/TcgEngine/Resources/Scenes/Tool/ReplayGame.unity";
        public static ReplayRecord Record { get; private set; }
        public static int PlayerId { get; private set; }
        public static int Generation { get; private set; }
        public static bool Active => Record != null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Record = null; PlayerId = 0; Generation++; }
        public static void Open(ReplayRecord record, string username)
        {
            int id = Array.FindIndex(record.players, p => string.Equals(p, username, StringComparison.OrdinalIgnoreCase));
            if (id < 0) throw new ArgumentException("Selected user is not a participant");
            if (record.buildVersion != Application.version) throw new InvalidOperationException("Replay build version differs from this client");
            TcgEngine.Client.GameClient.game_settings = ReplayStateCodec.Restore(record.initialState).settings;
            Record = record;
            PlayerId = id;
            Generation++;
            Time.timeScale = 1;
            if (TcgNetwork.Get() != null && TcgNetwork.Get().IsActive()) TcgNetwork.Get().Disconnect();
            Load(GamePath);
        }
        public static void Exit()
        {
            Clear();
            Load(ToolPath);
        }
        public static void Clear()
        {
            Reset();
            Time.timeScale = 1;
        }
        static void Load(string path)
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(System.IO.Path.GetFileNameWithoutExtension(path));
#endif
        }
    }
}
