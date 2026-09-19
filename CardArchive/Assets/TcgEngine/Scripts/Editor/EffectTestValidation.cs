using System;
using System.IO;
using System.Linq;
using TcgEngine;
using TcgEngine.Gameplay;
using UnityEditor;
using UnityEngine;

public static class EffectTestValidation
{
    [MenuItem("Tools/Card Archive/Validate Effect Test Insertion")]
    public static void Run()
    {
        CardData.Load();
        VariantData.Load();
        AbilityData.Load();
        WeaponData.Load();
        var definition = CardData.GetAll().First(c => c.IsBoardCard()
            && c.abilities.Any(a => a != null && a.trigger == AbilityTrigger.OnPlay));
        var game = new Game("effect-insertion-test", 2) { state = GameState.Play, phase = GamePhase.Main };
        var logic = new GameLogic(game);
        int triggered = 0, refreshed = 0;
        logic.onAbilityStart += (a, c) => triggered++;
        logic.onCardPlayed += (c, s) => triggered++;
        logic.onCardSummoned += (c, s) => triggered++;
        logic.onCardDrawn += n => triggered++;
        logic.onRefresh += () => refreshed++;
        var slots = Slot.GetAll();
        for (int owner = 0; owner < 2; owner++)
        {
            var player = game.GetPlayer(owner);
            int mana = player.mana;
            var hand = logic.DebugInsertCard(definition, owner, false, Slot.None);
            var field = logic.DebugInsertCard(definition, owner, true, slots[owner]);
            Check(player.cards_hand.Contains(hand) && player.cards_board.Contains(field), "owner and zone");
            Check(hand.player_id == owner && field.player_id == owner, "card ownership");
            Check(ReferenceEquals(player.cards_all[hand.uid], hand) && ReferenceEquals(player.cards_all[field.uid], field), "canonical references");
            Check(field.play_order > 0 && !field.exhausted && player.mana == mana, "play order without costs");
        }
        Check(triggered == 0 && game.ability_played.Count == 0 && !logic.IsResolving(), "no play, summon, draw or ability triggers");
        Check(refreshed == 4, "state refresh for each insertion");
        int count = game.GetPlayer(0).cards_all.Count;
        try { logic.DebugInsertCard(definition, 0, true, slots[0]); throw new Exception("Occupied slot accepted"); }
        catch (ArgumentException) { }
        Check(game.GetPlayer(0).cards_all.Count == count, "rejected insertion leaves no orphan card");
        game.phase = GamePhase.Attack;
        try { logic.DebugInsertCard(definition, 0, false, Slot.None); throw new Exception("Attack phase edit accepted"); }
        catch (InvalidOperationException) { }
        Directory.CreateDirectory("Library/EffectTestValidation");
        File.WriteAllText("Library/EffectTestValidation/result.txt", "PASS: both owners/zones, references, no triggers/costs, occupied slot and phase guards");
        Debug.Log("Effect test insertion validation passed.");
    }
    static void Check(bool value, string label)
    {
        if (!value) throw new Exception("Effect insertion failed: " + label);
    }
}
