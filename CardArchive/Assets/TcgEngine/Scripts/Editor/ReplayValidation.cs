using System;
using System.IO;
using System.Linq;
using TcgEngine;
using TcgEngine.Replay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Reflection;
using TcgEngine.AI;
using TcgEngine.Gameplay;
using TcgEngine.Server;

public static class ReplayValidation
{
    [MenuItem("Tools/Card Archive/Open Replay Tool")]
    public static void OpenTool()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ReplaySession.ToolPath);
    }

    [MenuItem("Tools/Card Archive/Validate Replay Codec")]
    public static void Run()
    {
        Directory.CreateDirectory("Library/ReplayValidation");
        try
        {
            var game = new Game("test-replay", 2);
            game.turn_timer = 12.345678f;
            game.players[0].username = "A";
            game.players[1].username = "B";
            var a = new Card("test-card", "a1", 0) { hp = 5, attack = 3 };
            var b = new Card("other", "b1", 1) { hp = 7 };
            game.players[0].cards_all.Add(a.uid, a);
            game.players[0].cards_deck.Add(a);
            game.players[1].cards_all.Add(b.uid, b);
            game.players[1].cards_hand.Add(b);
            game.boss_state = new BossState { player_id = 1, skill_gauge = 4 };
            var recorder = new ReplayRecorder();
            recorder.Begin(game);
            Check(!recorder.Failed, "initial capture");
            string original = ReplayStateCodec.Hash(recorder.Record.initialState);
            var restored = ReplayStateCodec.Restore(recorder.Record.initialState);
            Check(ReplayStateCodec.Hash(ReplayStateCodec.Capture(restored)) == original, "full state round trip");
            Check(ReferenceEquals(restored.players[0].cards_all["a1"], restored.players[0].cards_deck[0]), "card identity across zones");
            recorder.Interaction(game, new ReplayInteraction { kind = "mulligan", player = 0, cards = new[] { "a1" } });
            game.players[0].cards_deck.Clear();
            game.players[0].cards_hand.Add(a);
            game.players[0].ready = true;
            recorder.Capture(game);
            var random = new ReplayRandom(recorder.Random);
            a.damage = random.Next(1, 5);
            b.attack = random.Next(1, 8);
            recorder.Capture(game);
            game.players[0].cards_hand.Clear();
            game.players[0].cards_discard.Add(a);
            game.boss_state.skill_gauge = 9;
            game.state = GameState.GameEnded;
            var record = recorder.Finish(game);
            Check(record != null && record.events.Any(e => e.kind == "random"), "random results recorded");
            Check(original == ReplayStateCodec.Hash(record.initialState), "initial state immutable");
            var decoded = ReplayStorage.Decode(ReplayStorage.Encode(record));
            for (int pass = 0; pass < 2; pass++)
            {
                var state = ReplayStateCodec.Copy(decoded.initialState);
                foreach (var entry in decoded.events)
                    if (entry.kind == "state") state = ReplayStateCodec.Apply(state, entry.changes);
                Check(ReplayStateCodec.Hash(state) == decoded.finalHash, "state replay/restart " + pass);
                var result = ReplayStateCodec.Restore(state);
                Check(result.players[0].cards_discard.Single().damage == a.damage, "exact random damage");
                Check(result.boss_state.skill_gauge == 9, "boss state preserved");
            }
            string matchStats = RunActualMatch();
            File.WriteAllText("Library/ReplayValidation/replay-validation.txt", "PASS: state codec, references, delta, mulligan, randomness, gzip, restart, final hash\n" + matchStats);
            Debug.Log("Replay validation PASS");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText("Library/ReplayValidation/replay-validation.txt", "FAIL: " + e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }
    static string RunActualMatch()
    {
        var go = new GameObject("Replay validation data");
        try
        {
            var loader = go.AddComponent<DataLoader>();
            loader.data = Resources.LoadAll<GameplayData>("").First();
            loader.assets = Resources.LoadAll<AssetData>("").First();
            typeof(DataLoader).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, loader);
            loader.LoadData();
            var server = new GameServer("replay-smoke", 2, false);
            var logic = (GameLogic)typeof(GameServer).GetField("gameplay", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(server);
            var recorder = (ReplayRecorder)typeof(GameServer).GetField("replay", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(server);
            var game = server.GetGameData();
            for (int i = 0; i < 2; i++)
            {
                var player = game.players[i];
                player.username = "ReplayTest" + i;
                player.is_ai = true;
                player.ready = true;
                logic.SetPlayerDeck(player, i == 0 ? loader.data.test_deck : loader.data.test_deck_ai);
            }
            logic.StartGame();
            Settle(logic);
            if (game.phase == GamePhase.Mulligan)
            {
                foreach (var player in game.players)
                    logic.Mulligan(player, player.cards_hand.Take(1).Select(c => c.uid).ToArray());
                Settle(logic);
            }
            var ai = new[] { new AIPlayerRandomSync(logic, 0, 1), new AIPlayerRandomSync(logic, 1, 1) };
            for (int i = 0; i < 12 && !game.HasEnded(); i++)
            {
                ai[game.current_player].PlaySync();
                Settle(logic);
            }
            if (!game.HasEnded()) logic.EndGame(-1);
            var record = recorder.Finish(game);
            Check(record != null && !recorder.Failed, "actual server recording");
            var state = ReplayStateCodec.Copy(record.initialState);
            foreach (var entry in record.events)
                if (entry.kind == "state") state = ReplayStateCodec.Apply(state, entry.changes);
            Check(ReplayStateCodec.Hash(state) == record.finalHash, "actual match delta replay");
            Check(ReplayStateCodec.Hash(ReplayStateCodec.Capture(ReplayStateCodec.Restore(state))) == record.finalHash, "actual match restored state");
            string payload = ReplayStorage.Encode(record);
            var request = new ReplayUpload { matchId = record.matchId, players = record.players, payload = payload };
            File.WriteAllText("Library/ReplayValidation/replay-smoke.json", JsonUtility.ToJson(request));
            Check(record.events.Any(e => e.kind == "interaction" && e.interaction.kind == "play"), "actual play interactions");
            return "Actual match: " + game.turn_count + " turns, " + record.events.Count + " entries, " + Convert.FromBase64String(payload).Length + " compressed bytes";
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    static void Settle(GameLogic logic)
    {
        int limit = 1000;
        while (logic.IsResolving() && limit-- > 0) logic.Update(1f);
        Check(limit > 0, "effect queue settled");
    }
    static void Check(bool value, string name) { if (!value) throw new Exception("Replay validation failed: " + name); }
}
