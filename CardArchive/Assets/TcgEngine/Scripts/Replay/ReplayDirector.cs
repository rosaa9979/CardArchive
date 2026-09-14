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
        ReplayNode state;
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
        IEnumerator Start()
        {
            client = GameClient.Get();
            presenter = gameObject.AddComponent<ReplayInteractionPresenter>();
            // Existing scene presentation stays intact, but no real input or debug views.
            foreach (var input in FindObjectsOfType<EventSystem>()) input.enabled = false;
            foreach (var control in FindObjectsOfType<PlayerControls>()) control.enabled = false;
            foreach (var behaviour in FindObjectsOfType<MonoBehaviour>())
                if (behaviour.GetType().Name == "AIDebugPanel" || behaviour.GetType().Name == "EffectStepPanel")
                    behaviour.gameObject.SetActive(false);
            state = ReplayStateCodec.Copy(ReplaySession.Record.initialState);
            client.ApplyReplayState(ReplayStateCodec.Restore(state));
            yield return null; // Let scene subscribers initialize before dispatching events.
            yield return new WaitForSeconds(0.5f);
            var entries = ReplaySession.Record.events;
            for (cursor = 0; cursor < entries.Count; cursor++)
            {
                while (paused) yield return null;
                var entry = entries[cursor];
                if (entry.kind == "interaction")
                    yield return presenter.Present(entry.interaction);
                else if (entry.kind == "state")
                {
                    bool selectorBefore = client.GetGameData().selector != SelectorType.None;
                    var phaseBefore = client.GetGameData().phase;
                    bool applied = TryApply(entry);
                    if (!applied) yield break;
                    var data = client.GetGameData();
                    // Ensure panels have appeared before synthetic selection/confirmation.
                    if ((!selectorBefore && data.selector != SelectorType.None) || (phaseBefore != GamePhase.Mulligan && data.phase == GamePhase.Mulligan))
                        yield return new WaitForSeconds(1.5f);
                }
                else if (entry.kind == "event")
                {
                    ushort tag = 0;
                    try
                    {
                        byte[] packet = Convert.FromBase64String(entry.packet);
                        using (var reader = new FastBufferReader(packet, Allocator.Temp)) reader.ReadValueSafe(out tag);
                        client.ApplyReplayEvent(packet);
                    }
                    catch (Exception e) { status = "Replay event failed: " + e.Message; paused = true; Failed = true; Debug.LogException(e); }
                    if (paused) yield break;
                    float delay = EventDelay(tag);
                    if (delay > 0) yield return new WaitForSeconds(delay);
                }
                // Allow the board to instantiate/despawn cards between state changes.
                yield return null;
            }
            Failed = ReplayStateCodec.Hash(state) != ReplaySession.Record.finalHash;
            Completed = !Failed;
            status = Completed ? "Completed — state verified" : "ERROR: final state mismatch";
        }
        bool TryApply(ReplayEntry entry)
        {
            try
            {
                state = ReplayStateCodec.Apply(state, entry.changes);
                client.ApplyReplayState(ReplayStateCodec.Restore(state));
                presenter.SyncState();
                return true;
            }
            catch (Exception e) { status = "Replay state failed: " + e.Message; Failed = true; Debug.LogException(e); return false; }
        }
        static float EventDelay(ushort tag)
        {
            if (tag == GameAction.Mulligan) return GameConfig.Timing.mulligan_to_turn;
            if (tag == GameAction.NewTurn || tag == GameAction.AttackPhase) return GameConfig.Timing.turn_start;
            if (tag == GameAction.AbilityTrigger) return GameConfig.Timing.ability;
            if (tag == GameAction.AttackStart || tag == GameAction.AttackPlayerStart) return 0.35f;
            if (tag == GameAction.AttackEnd || tag == GameAction.AttackPlayerEnd) return 0.3f;
            if (tag == GameAction.CardPlayed || tag == GameAction.CardMoved) return GameConfig.Timing.play_card;
            if (tag == GameAction.ValueRolled) return 0.7f;
            return 0;
        }
        void OnGUI()
        {
            if (!ReplaySession.Active) return;
            if (GUI.Button(new Rect(10, 10, 140, 28), showControls ? "Hide replay controls" : "Replay controls"))
                showControls = !showControls;
            if (!showControls) return;
            GUILayout.BeginArea(new Rect(10, 44, Mathf.Min(600, Screen.width - 20), 85), GUI.skin.box);
            GUILayout.Label("Replay — " + ReplaySession.Record.players[ReplaySession.PlayerId] + " | " + cursor + "/" + ReplaySession.Record.events.Count + " " + status);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(paused ? "Resume" : "Pause")) paused = !paused;
            foreach (float value in new[] { 0.5f, 1f, 2f, 4f })
                if (GUILayout.Button(value + "x")) speed = value;
            if (GUILayout.Button("Restart")) ReplaySession.Open(ReplaySession.Record, ReplaySession.Record.players[ReplaySession.PlayerId]);
            if (GUILayout.Button("Tool")) ReplaySession.Exit();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
        void Update() { Time.timeScale = paused ? 0 : speed; }
        void OnDestroy() { Time.timeScale = 1; }
    }
}
