using System;
using System.Collections;
using TcgEngine.Client;
using TcgEngine.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Netcode;
using Unity.Collections;

namespace TcgEngine.Replay
{
    public class ReplayDirector : MonoBehaviour
    {
        ReplayState state;
        int cursor;
        float speed = 1;
        bool paused;
        bool showControls = false;
        string status = "";
        public bool Completed { get; private set; }
        public bool Failed { get; private set; }
        public int Cursor => cursor;
        public void SetSpeed(float value) { speed = Mathf.Clamp(value, 0.5f, 4f); }
        public void SetPaused(bool value) { paused = value; }
        GameClient client;
        ReplayInteractionPresenter presenter;
        EventSystem[] replayEventSystems;
        IEnumerator Start()
        {
            client = GameClient.Get();
            presenter = gameObject.AddComponent<ReplayInteractionPresenter>();
            // Existing scene presentation stays intact, but no real input or debug views.
            replayEventSystems = FindObjectsByType<EventSystem>();
            foreach (var input in replayEventSystems) input.enabled = false;
            foreach (var control in FindObjectsByType<PlayerControls>()) control.enabled = false;
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>())
                if (behaviour.GetType().Name == "AIDebugPanel" || behaviour.GetType().Name == "EffectStepPanel")
                    behaviour.gameObject.SetActive(false);
            state = ReplayStateCodec.Copy(ReplaySession.Record.initialState);
            while (!client.IsReady() || client.IsPresentingState) yield return null;
            yield return null; // Let scene subscribers initialize before dispatching events.
            yield return new WaitForSeconds(0.5f);
            var entries = ReplaySession.Record.events;
            bool explicitBoundaries = entries.Exists(e => e.kind == "presentation");
            for (cursor = 0; cursor < entries.Count; cursor++)
            {
                while (paused) yield return null;
                var entry = entries[cursor];
                if (entry.kind == "interaction")
                {
                    // Live matches get this gap from queue timers and player input; the recording only keeps order.
                    if (entry.interaction.kind != "mulligan" && entry.interaction.kind != "cancel")
                        yield return new WaitForSeconds(ActionLead);
                    yield return presenter.Present(entry.interaction);
                }
                else if (entry.kind == "state" || entry.kind == "event")
                {
                    bool selectorBefore = client.GetGameData().selector != SelectorType.None;
                    var phaseBefore = client.GetGameData().phase;
                    var packets = new System.Collections.Generic.List<string>();
                    try
                    {
                        if (entry.kind == "state") state = ReplayStateCodec.Apply(state, entry.changes);
                        else packets.Add(entry.packet);
                        // New recordings preserve the exact live presentation boundary.
                        // Older recordings retain their original state/event grouping.
                        while (cursor + 1 < entries.Count)
                        {
                            var following = entries[cursor + 1];
                            if (following.kind == "interaction" || (!explicitBoundaries && following.kind == "state")) break;
                            cursor++;
                            if (following.kind == "presentation") break;
                            if (following.kind == "state") state = ReplayStateCodec.Apply(state, following.changes);
                            if (following.kind == "event") packets.Add(following.packet);
                        }
                    }
                    catch (Exception e)
                    {
                        status = "Replay state failed: " + e.Message;
                        Failed = true;
                        Debug.LogException(e);
                    }
                    if (Failed) yield break;
                    yield return client.PresentReplayState(state, packets);
                    presenter.SyncState();
                    var data = client.GetGameData();
                    if ((!selectorBefore && data.selector != SelectorType.None)
                        || (phaseBefore != GamePhase.Mulligan && data.phase == GamePhase.Mulligan))
                        yield return new WaitForSeconds(1.5f);
                }
                yield return null;
            }
            Failed = ReplayStateCodec.Hash(state) != ReplaySession.Record.finalHash
                || ReplayStateCodec.Hash(ReplayStateCodec.Capture(client.GetGameData())) != ReplaySession.Record.finalHash;
            Completed = !Failed;
            status = Completed ? "Completed — state verified" : "ERROR: final state mismatch";
            if (Completed && client.HasEnded())
            {
                paused = false;
                speed = 1;
                Time.timeScale = 1;
                GameBoard.Get().EndGame();
                // Restore UI clicks only once the shared result panel is ready.
                while (!EndGamePanel.Get().IsVisible()) yield return null;
                foreach (var input in replayEventSystems)
                    if (input != null) input.enabled = true;
            }
        }
        const float ActionLead = 0.35f; // before a recorded play, move, selection or end turn
        void OnGUI()
        {
            if (!ReplaySession.Active) return;
            if (GUI.Button(new Rect(10, 10, 140, 28), showControls ? "Hide replay controls" : "Replay controls"))
                showControls = !showControls;
            if (!showControls) return;
            GUILayout.BeginArea(new Rect(10, 44, Mathf.Min(600, Screen.width - 20), 85), GUI.skin.box);
            GUILayout.Label("Replay — " + ReplaySession.Record.players[ReplaySession.PlayerId] + " | " + cursor + "/" + ReplaySession.Record.events.Count + " " + status);
            GUILayout.BeginHorizontal();
            GUI.enabled = !Completed;
            if (GUILayout.Button(paused ? "Resume" : "Pause")) paused = !paused;
            foreach (float value in new[] { 0.5f, 1f, 2f, 4f })
                if (GUILayout.Button(value + "x")) speed = value;
            GUI.enabled = true;
            if (GUILayout.Button("Restart")) ReplaySession.Open(ReplaySession.Record, ReplaySession.Record.players[ReplaySession.PlayerId]);
            if (GUILayout.Button("Tool")) ReplaySession.Exit();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
        void Update() { Time.timeScale = Completed ? 1 : (paused ? 0 : speed); }
        void OnDestroy() { Time.timeScale = 1; }
    }
}
