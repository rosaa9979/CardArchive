using System;
using System.IO;
using TcgEngine;
using TcgEngine.Client;
using TcgEngine.Replay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch/editor smoke test uses the real scene, subscriptions, UI and stored server packets.
[InitializeOnLoad]
public static class ReplayPlaybackValidation
{
    const string Flag = "CardArchive.ReplaySmoke";
    static double started;
    static int errors;
    static int pass;
    static bool leaving;
    static ReplayDirector previous;
    static ReplayPlaybackValidation()
    {
        EditorApplication.playModeStateChanged += Changed;
        EditorApplication.update += Tick;
    }
    public static void Run()
    {
        Directory.CreateDirectory("Library/ReplayValidation");
        if (!File.Exists("Library/ReplayValidation/replay-smoke.json")) ReplayValidation.Run();
        SessionState.SetBool(Flag, true);
        EditorSceneManager.OpenScene(ReplaySession.ToolPath);
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            started = EditorApplication.timeSinceStartup;
            errors = 0;
            pass = 0;
            leaving = false;
            Application.logMessageReceived += Log;
            try { Open(0); }
            catch (Exception e) { Finish(false, e.Message); }
        }
    }
    static void Open(int player)
    {
        var upload = JsonUtility.FromJson<ReplayUpload>(File.ReadAllText("Library/ReplayValidation/replay-smoke.json"));
        var record = ReplayStorage.Decode(upload.payload);
        ReplaySession.Open(record, record.players[player]);
    }
    static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error) errors++;
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || leaving || started == 0) return;
        if (EditorApplication.timeSinceStartup - started > 240) { Finish(false, "Playback timeout"); return; }
        var director = UnityEngine.Object.FindFirstObjectByType<ReplayDirector>();
        if (director == null || director == previous) return;
        director.SetSpeed(4);
        if (TcgNetwork.Get() != null && TcgNetwork.Get().IsActive()) { Finish(false, "Replay started networking"); return; }
        if (GameClient.Get().GetPlayerID() != pass) { Finish(false, "Wrong viewpoint"); return; }
        if (errors > 0) { Finish(false, "Scene errors=" + errors); return; }
        if (director.Failed) { Finish(false, "Replay director failed"); return; }
        var presenter = director.GetComponent<ReplayInteractionPresenter>();
        if (presenter != null && presenter.HasHeldVisual)
        {
            var visual = presenter.HeldVisual;
            var hand = visual.GetComponent<HandCard>();
            var back = visual.GetComponent<HandCardBack>();
            var board = visual.GetComponent<BoardCard>();
            if (hand == null && back == null && board == null)
            { Finish(false, "Drag is not using a real scene card"); return; }
            if (hand != null && (hand.enabled || !hand.IsDrag() || visual.parent != HandCardArea.Get().card_area))
            { Finish(false, "Real hand drag lost its input lock or canvas parent"); return; }
            foreach (var aim in UnityEngine.Object.FindObjectsByType<TcgEngine.FX.AimTargetFX>(FindObjectsSortMode.InstanceID))
                if (aim.target_fx.activeSelf || aim.text_fx.activeSelf)
                { Finish(false, "Replay targeting cursor is visible"); return; }
        }
        if (director.Completed)
        {
            if (pass == 0) { pass = 1; previous = director; Open(1); }
            else Finish(errors == 0, "Both viewpoints, real scene playback; errors=" + errors);
        }
    }
    static void Finish(bool success, string message)
    {
        leaving = true;
        SessionState.SetBool(Flag, false);
        Application.logMessageReceived -= Log;
        File.WriteAllText("Library/ReplayValidation/replay-playback-validation.txt", (success ? "PASS: " : "FAIL: ") + message);
        if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}
