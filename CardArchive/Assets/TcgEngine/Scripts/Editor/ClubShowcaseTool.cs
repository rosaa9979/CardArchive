#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using TcgEngine;
using TcgEngine.Gameplay;
using TcgEngine.Replay;
using TcgEngine.Server;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

// Website demonstration authoring only. All outcomes run through the real GameLogic queue;
// prepared board states and recordings stay in .ai-work, never in production card assets.
[InitializeOnLoad]
public static class ClubShowcaseTool
{
    public static readonly string Folder = Path.GetFullPath(".ai-work/club-media/generated");
    static readonly string[] Clubs = { "Make_Up_Work_Club", "Justice_Task_Force", "Trinity_Vigilante_Crew",
        "Gourmet_Research_Society", "Hot_Springs_Department", "Engineering_Department", "Arius_Squad" };
    const string CaptureFlag = "CardArchive.ClubShowcaseCapture";
    static RecorderController controller;
    static RecorderControllerSettings controllerSettings;
    static MovieRecorderSettings movie;
    static int index, startFrame, completedFrame;
    static bool opened;
    static double deadline;

    static ClubShowcaseTool()
    {
        EditorApplication.playModeStateChanged += Changed;
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Card Archive/Website/Generate Club Replays")]
    public static void Generate()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Generate outside Play Mode.");
        Directory.CreateDirectory(Folder);
        var instanceField = typeof(DataLoader).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        var previousLoader = instanceField.GetValue(null);
        var root = new GameObject("Club showcase data") { hideFlags = HideFlags.HideAndDontSave };
        var loader = root.AddComponent<DataLoader>();
        loader.data = Resources.LoadAll<GameplayData>("").First();
        loader.assets = Resources.LoadAll<AssetData>("").First();
        instanceField.SetValue(null, loader);
        try
        {
            loader.LoadData();
            var eligible = CardData.GetAll().Where(c => c.type == CardType.Student && c.deckbuilding)
                .SelectMany(c => c.clubs).Where(c => c != null).Select(c => c.id).Distinct().ToArray();
            Check(eligible.OrderBy(x => x).SequenceEqual(Clubs.OrderBy(x => x)), "Update showcase scenarios for the current playable clubs.");
            var failures = new List<string>();
            foreach (string club in Clubs)
            {
                try { GenerateOne(club, loader); }
                catch (Exception e) { failures.Add(club + ": " + e.Message); }
            }
            Check(failures.Count == 0, string.Join("\n", failures));
        }
        finally { instanceField.SetValue(null, previousLoader); UnityEngine.Object.DestroyImmediate(root); }
    }

    static Slot S(int x, int y) => Slot.GetAll().First(s => s.x == x && s.y == y);
    static T Private<T>(object obj, string field) => (T)obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);
    static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }

    static void Settle(GameServer server, GameLogic logic)
    {
        var release = typeof(GameServer).GetMethod("UpdatePresentationBarrier", BindingFlags.NonPublic | BindingFlags.Instance);
        var barrier = Private<PresentationBarrier>(server, "barrier");
        int left = 1000;
        while (left-- > 0)
        {
            release.Invoke(server, null);
            if (!barrier.Active && !logic.IsResolving()) break;
            logic.Update(1f);
        }
        Check(left > 0, "Scenario queue did not settle.");
    }

    static void GenerateOne(string id, DataLoader loader)
    {
        File.Delete(Path.Combine(Folder, id + ".json"));
        File.Delete(Path.Combine(Folder, id + "-validation.txt"));
        File.Delete(Path.Combine(Folder, id + "-playback.txt"));
        var server = new GameServer("website-" + id, 2, false);
        var logic = Private<GameLogic>(server, "gameplay");
        var recorder = Private<ReplayRecorder>(server, "replay");
        var game = server.GetGameData();
        game.state = GameState.Play; game.phase = GamePhase.Main; game.current_player = 0; game.turn_count = 4;
        game.SetRandom(new GameRandom(17));
        foreach (var player in game.players)
        {
            player.username = player.player_id == 0 ? "Club showcase" : "Practice";
            player.is_ai = true; player.ready = true;
            player.hp = player.hp_max = 20; player.mana = player.mana_max = 10;
            if (loader.data.test_deck.hero != null)
                player.hero = Card.Create(loader.data.test_deck.hero, VariantData.GetDefault(), player);
            // A player with no cards in hand/board/deck loses immediately under the real rules.
            for (int i = 0; i < 3; i++)
                player.cards_deck.Add(Card.Create(CardData.Get("Sweeper"), VariantData.GetDefault(), player));
        }
        var owner = game.players[0];
        owner.cards_club.Add(Card.Create(CardData.Get(id), VariantData.GetDefault(), owner));
        Func<string, int, int, int, Card> add = (card, player, x, y) => logic.DebugInsertCard(CardData.Get(card), player, true, S(x, y));
        Action action;
        Func<bool> verify;
        string description;
        if (id == "Make_Up_Work_Club")
        {
            var target = add("Shirasu_Azusa", 0, 3, 2);
            var played = logic.DebugInsertCard(CardData.Get("Sweeper"), 0, false, Slot.None);
            int before = target.GetAttack();
            action = () => logic.SelectPlayTarget(played, S(2, 3));
            verify = () => target.GetAttack() == before + 1;
            description = "Play a card; an existing Make-Up Work Club student gains 1 attack.";
        }
        else if (id == "Hot_Springs_Department")
        {
            var attacker = add("Shimokura_Megu", 0, 3, 3);
            var place = add("The_Way_To_School", 1, 3, 4);
            action = () => logic.AttackTarget(attacker, place, true);
            verify = () => !game.players[1].cards_board.Contains(place)
                && game.players.SelectMany(p => p.cards_board).Any(c => c.card_id == "Hot_Spring");
            description = "A Hot Springs student attacks a Place: destroy it and summon a Hot Spring.";
        }
        else if (id == "Trinity_Vigilante_Crew")
        {
            var attacker = add("Morizuki_Suzumi", 0, 3, 3);
            var blocker = add("Sweeper", 1, 4, 3);
            action = () => { game.phase = GamePhase.Attack; logic.AttackSearch(attacker, true); };
            verify = () => game.players[1].hp < 20 && blocker.damage == 0;
            description = "Prioritize the opposing teacher even with an enemy unit in range.";
        }
        else if (id == "Gourmet_Research_Society")
        {
            var definition = CardData.GetAll().Where(c => c.type == CardType.Student && c.deckbuilding
                && c.clubs.Any(club => club.id == id)).OrderByDescending(c => c.hp).First();
            var target = add(definition.id, 0, 3, 2);
            target.damage = target.GetHPMax() - 1;
            var enemy = add("Modded_Sweeper", 1, 3, 4);
            action = () => logic.HealCard(target, 1);
            verify = () => enemy.damage > 0 || !game.players[1].cards_board.Contains(enemy);
            description = "Heal a wounded Gourmet student without fully restoring HP; trigger board-wide damage.";
        }
        else
        {
            Card victim;
            if (id == "Engineering_Department") { add("Shiraishi_Utaha", 0, 4, 2); victim = add("Sweeper", 0, 3, 3); }
            else if (id == "Justice_Task_Force") { add("Shizuyama_Mashiro", 0, 4, 2); victim = add("Sweeper", 0, 3, 3); }
            else { victim = add("Tsuchinaga_Hiyori", 0, 3, 3); victim.damage = victim.GetHPMax() - 1; }
            var spell = logic.DebugInsertCard(CardData.Get("C4"), 1, false, Slot.None);
            game.current_player = 1;
            action = () => {
                Slot target = victim.slot;
                Check(logic.SelectPlayTarget(spell, target), "C4 play rejected");
                if (game.selector != SelectorType.None) logic.SelectSlot(target);
            };
            if (id == "Engineering_Department") verify = () => owner.hp < 20 && game.players[1].hp < 20;
            else if (id == "Justice_Task_Force") verify = () => owner.cards_board.Any(c => c.card_id == "Justice_Task_Force_Memeber");
            else verify = () => owner.cards_deck.Contains(victim) && !owner.cards_discard.Contains(victim);
            description = id == "Arius_Squad" ? "A defeated Arius student returns to the deck."
                : id == "Engineering_Department" ? "A friendly machine dies; both teachers take damage."
                : "A friendly unit dies; summon Justice Task Force member tokens.";
        }
        logic.UpdateOngoing(); Settle(server, logic);
        recorder.Begin(game);
        var fired = new List<string>();
        logic.onAbilityStart += (ability, caster) => fired.Add(caster.card_id + ":" + ability.id);
        action(); Settle(server, logic);
        File.WriteAllText(Path.Combine(Folder, id + "-state.txt"), "state=" + game.state + " phase=" + game.phase + " resolving=" + logic.IsResolving() + " selector=" + game.selector + "\n"
            + string.Join("\n", game.players.Select(p => "player " + p.player_id + " hp=" + p.hp + " dead=" + p.IsDead())) + "\n"
            + string.Join("\n", game.players.SelectMany(p => p.cards_board).Select(c => c.card_id + " HP=" + c.GetHP() + " owner=" + c.player_id))
            + "\nAbilities: " + string.Join(",", fired));
        Check(verify(), id + ": expected club outcome missing");
        var record = recorder.Finish(game);
        Check(record != null && !recorder.Failed, id + ": recording failed");
        var restored = ReplayStateCodec.Copy(record.initialState);
        foreach (var entry in record.events.Where(e => e.kind == "state")) restored = ReplayStateCodec.Apply(restored, entry.changes);
        Check(ReplayStateCodec.Hash(restored) == record.finalHash, id + ": replay final hash mismatch");
        File.WriteAllText(Path.Combine(Folder, id + ".json"), JsonUtility.ToJson(new ReplayUpload {
            matchId = record.matchId, players = record.players, payload = ReplayStorage.Encode(record) }));
        File.WriteAllText(Path.Combine(Folder, id + "-validation.txt"), "PASS: actual GameLogic outcome and replay final hash\n" + description + "\nEntries=" + record.events.Count);
    }

    [MenuItem("Tools/Card Archive/Website/Capture Club Replays")]
    public static void Capture()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Start outside Play Mode.");
        Check(Clubs.All(id => File.Exists(Path.Combine(Folder, id + ".json"))), "Generate the replays first.");
        File.Delete(Path.Combine(Folder, "capture-error.txt"));
        foreach (string id in Clubs) File.Delete(Path.Combine(Folder, id + "-playback.txt"));
        SessionState.SetString("ClubShowcase.PreviousStartScene", AssetDatabase.GetAssetPath(EditorSceneManagerStartScene()));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ReplaySession.ToolPath);
        SessionState.SetBool(CaptureFlag, true);
        EditorApplication.EnterPlaymode();
    }
    static SceneAsset EditorSceneManagerStartScene() => EditorSceneManager.playModeStartScene;

    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(CaptureFlag, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) { index = 0; opened = false; deadline = EditorApplication.timeSinceStartup + 120; }
        if (state == PlayModeStateChange.ExitingPlayMode) StopRecorder();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(CaptureFlag, false);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("ClubShowcase.PreviousStartScene", ""));
        }
    }

    static void Tick()
    {
        if (!SessionState.GetBool(CaptureFlag, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Club recording timed out.");
            if (!opened)
            {
                var upload = JsonUtility.FromJson<ReplayUpload>(File.ReadAllText(Path.Combine(Folder, Clubs[index] + ".json")));
                ReplaySession.Open(ReplayStorage.Decode(upload.payload), upload.players[0]);
                opened = true; startFrame = 0; completedFrame = 0;
                return;
            }
            var director = UnityEngine.Object.FindAnyObjectByType<ReplayDirector>();
            if (director == null) return;
            if (startFrame == 0)
            {
                director.SetPaused(true);
                controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
                movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                movie.name = "Club showcase"; movie.Enabled = true;
                movie.CaptureAudio = false;
                movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1280, OutputHeight = 720 };
                movie.OutputFile = Path.Combine(Folder, Clubs[index]);
                controllerSettings.AddRecorderSettings(movie);
                controllerSettings.SetRecordModeToManual(); controllerSettings.FrameRate = 30;
                controller = new RecorderController(controllerSettings);
                controller.PrepareRecording(); controller.StartRecording();
                startFrame = Time.frameCount;
            }
            if (Time.frameCount - startFrame >= 30) director.SetPaused(false);
            if (director.Failed) throw new InvalidOperationException(Clubs[index] + ": playback failed");
            if (!director.Completed) return;
            if (completedFrame == 0) completedFrame = Time.frameCount;
            if (Time.frameCount - completedFrame < 45) return;
            StopRecorder();
            File.WriteAllText(Path.Combine(Folder, Clubs[index] + "-playback.txt"), "PASS: ReplayDirector completed with verified final state");
            index++;
            if (index == Clubs.Length) { EditorApplication.ExitPlaymode(); return; }
            opened = false; deadline = EditorApplication.timeSinceStartup + 120;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(Folder, "capture-error.txt"), error.ToString());
            Debug.LogException(error); StopRecorder(); EditorApplication.ExitPlaymode();
        }
    }
    static void StopRecorder()
    {
        if (controller != null) controller.StopRecording();
        controller = null;
        if (movie != null) UnityEngine.Object.DestroyImmediate(movie);
        if (controllerSettings != null) UnityEngine.Object.DestroyImmediate(controllerSettings);
    }
}
#endif
