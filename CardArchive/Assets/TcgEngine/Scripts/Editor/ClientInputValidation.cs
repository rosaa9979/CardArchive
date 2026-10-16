using System;
using System.Reflection;
using TcgEngine.Client;
using TcgEngine.Replay;
using UnityEditor;
using UnityEngine;

namespace TcgEngine.Editor
{
    public static class ClientInputValidation
    {
        [MenuItem("TcgEngine/FX/Validate Client Input Gates")]
        public static void Validate() => Debug.Log("Client input checks passed: " + Run());

        public static int Run()
        {
            int checks = 0;
            Action<bool, string> check = (ok, name) => {
                if (!ok) throw new Exception(name);
                checks++;
            };
            var root = new GameObject("Client input validation") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var client = root.AddComponent<InputValidationClient>();
                var presentation = (PresentationState)Field("presentationState").GetValue(client);
                var game = new Game("input-validation", 2) {
                    state = GameState.Play, phase = GamePhase.Main, current_player = 0, turn_timer = 60f
                };
                game.players[0].hp = 20;
                Publish(presentation, game);
                Field("presentationReady").SetValue(client, true);
                Field("inputClosed").SetValue(client, false);
                check(client.IsInputOpen && client.IsYourTurn(), "Own main phase accepts input");

                game.turn_timer = 59f;
                Action timerImpact = Stage(presentation, game);
                check(presentation.Pending && client.IsInputOpen && client.IsYourTurn(), "Timer awaiting Impact must not close input or change turn ownership");
                check(client.GetGameData().turn_timer == 60f, "Timer display still waits for Impact");
                timerImpact();
                check(client.GetGameData().turn_timer == 59f, "Timer publishes on Impact");

                game.players[0].hp = 17;
                Action damageImpact = Stage(presentation, game);
                check(client.IsInputOpen && client.GetGameData().players[0].hp == 20, "Pending damage leaves input independent and displayed health unchanged");
                damageImpact();
                check(client.GetGameData().players[0].hp == 17, "Health still publishes on Impact");

                Field("inputClosed").SetValue(client, true);
                check(!client.IsInputOpen && client.IsYourTurn(), "Server/sync gate closes input without changing turn ownership");
                Field("inputClosed").SetValue(client, false);
                Field("presentationReady").SetValue(client, false);
                check(!client.IsInputOpen && client.IsYourTurn(), "Initial sync still blocks input");
                Field("presentationReady").SetValue(client, true);
                client.connected = false;
                check(!client.IsInputOpen && client.IsYourTurn(), "Disconnect still blocks input");
                client.connected = true;
                Field("observe_mode").SetValue(client, true);
                check(!client.IsInputOpen, "Observers cannot act");
                Field("observe_mode").SetValue(client, false);
                game.current_player = 1;
                Publish(presentation, game);
                check(client.IsInputOpen && !client.IsYourTurn(), "Opponent turn is independent of an open input gate");
                game.selector = SelectorType.SelectTarget;
                game.selector_player_id = 0;
                Publish(presentation, game);
                check(client.IsInputOpen && client.IsYourTurn(), "Local target selection retains its action ownership");
                return checks;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static FieldInfo Field(string name) => typeof(GameClient).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        private static Action Stage(PresentationState presentation, Game game)
        {
            presentation.Stage(ReplayStateCodec.Capture(game));
            Action impact = presentation.RequireAnimationEvent();
            presentation.Seal();
            return impact;
        }
        private static void Publish(PresentationState presentation, Game game) => Stage(presentation, game)();
    }

    // Exercise real client predicates without connecting or changing the scene's singleton.
    internal sealed class InputValidationClient : GameClient
    {
        public bool connected = true;
        public override bool IsReady() => connected && GetGameData() != null;
        protected override void Awake() { }
        protected override void Start() { }
        protected override void Update() { }
        protected override void OnDestroy() { }
    }
}
