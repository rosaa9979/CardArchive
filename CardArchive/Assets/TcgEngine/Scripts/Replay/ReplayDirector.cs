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
        EventSystem[] replayEventSystems;
        IEnumerator Start()
        {
            client = GameClient.Get();
            presenter = gameObject.AddComponent<ReplayInteractionPresenter>();
            // Existing scene presentation stays intact, but no real input or debug views.
            replayEventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.InstanceID);
            foreach (var input in replayEventSystems) input.enabled = false;
            foreach (var control in FindObjectsByType<PlayerControls>(FindObjectsSortMode.InstanceID)) control.enabled = false;
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.InstanceID))
                if (behaviour.GetType().Name == "AIDebugPanel" || behaviour.GetType().Name == "EffectStepPanel")
                    behaviour.gameObject.SetActive(false);
            state = ReplayStateCodec.Copy(ReplaySession.Record.initialState);
            client.ApplyReplayState(ReplayStateCodec.Restore(state));
            yield return null; // Let scene subscribers initialize before dispatching events.
            yield return new WaitForSeconds(0.5f);
            var entries = ReplaySession.Record.events;
            abilityDepth = 0;
            attackInFlight = false;
            for (cursor = 0; cursor < entries.Count; cursor++)
            {
                while (paused) yield return null;
                var entry = entries[cursor];
                if (entry.kind == "interaction")
                {
                    abilityDepth = 0;
                    attackInFlight = false;
                    // Live matches get this gap from queue timers and player input; the recording only keeps order.
                    if (entry.interaction.kind != "mulligan" && entry.interaction.kind != "cancel")
                        yield return new WaitForSeconds(ActionLead);
                    yield return presenter.Present(entry.interaction);
                }
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
                    if (tag == GameAction.AbilityTrigger) abilityDepth++;
                    else if (tag == GameAction.AbilityEnd) abilityDepth = Mathf.Max(0, abilityDepth - 1);
                    else if (tag == GameAction.NewTurn || tag == GameAction.AttackPhase || tag == GameAction.AttackStart || tag == GameAction.AttackPlayerStart)
                        abilityDepth = 0; // never let a missing AbilityEnd swallow later pauses
                    // Live, an attack's damage events and its hit arrive in the same frame (CardDamaged before AttackHit);
                    // keep them together so damage numbers line up with the hit FX as in game.
                    if (tag == GameAction.AttackStart || tag == GameAction.AttackPlayerStart) attackInFlight = true;
                    else if (tag == GameAction.AttackHit || tag == GameAction.AttackEvade || tag == GameAction.AttackPlayerHit
                        || tag == GameAction.AttackEnd || tag == GameAction.AttackPlayerEnd || tag == GameAction.NewTurn || tag == GameAction.AttackPhase)
                        attackInFlight = false;
                    // One ability's targets and area damage land together: no pauses between AbilityTrigger and AbilityEnd.
                    bool together = (abilityDepth > 0 && tag != GameAction.AbilityTrigger)
                        || (attackInFlight && tag != GameAction.AttackStart && tag != GameAction.AttackPlayerStart);
                    float delay = together ? 0 : EventDelay(tag);
                    if (delay > 0) yield return new WaitForSeconds(delay);
                }
                // Allow the board to instantiate/despawn cards between state changes (inside an ability or an attack they apply together).
                if (abilityDepth == 0 && !attackInFlight) yield return null;
            }
            Failed = ReplayStateCodec.Hash(state) != ReplaySession.Record.finalHash;
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
        const float ActionLead = 0.35f; // before a recorded play, move, selection or end turn
        const float DrawDelay = 0.6f;   // lets the drawn card reach the hand
        const float EffectGap = 0.15f;  // presentation events without their own pause
        const float AttackGap = 0.8f;   // after each attack, before the next attacker
        int abilityDepth;               // inside AbilityTrigger..AbilityEnd
        bool attackInFlight;            // after AttackStart, until the hit/evade/end
        static float EventDelay(ushort tag)
        {
            if (tag == GameAction.Mulligan) return GameConfig.Timing.mulligan_to_turn;
            if (tag == GameAction.NewTurn || tag == GameAction.AttackPhase) return GameConfig.Timing.turn_start;
            if (tag == GameAction.AbilityTrigger) return GameConfig.Timing.ability;
            if (tag == GameAction.AttackStart || tag == GameAction.AttackPlayerStart) return 0.35f;
            if (tag == GameAction.AttackEnd || tag == GameAction.AttackPlayerEnd) return AttackGap;
            if (tag == GameAction.CardPlayed || tag == GameAction.CardMoved) return GameConfig.Timing.play_card;
            if (tag == GameAction.ValueRolled) return 0.7f;
            if (tag == GameAction.CardDrawn) return DrawDelay;
            return EffectGap;
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
