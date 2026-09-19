using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using TcgEngine;
using TcgEngine.Client;
using TcgEngine.FX;
using TcgEngine.Gameplay;

public static class ValidateNonomiBarrage
{
    static List<string> checks;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        checks.Add(name);
    }

    public static async Task<object> Run()
    {
        checks = new List<string>();
        Check(Application.isPlaying, "Play Mode active");
        var client = GameClient.Get();
        var cardData = CardData.Get("Nonomi_Fire");
        var tutorial = CardData.Get("Nonomi_Fire_Tutorial");
        var ability = cardData.abilities.Single();
        Check(ability.board_fx != null, "Ability board FX is assigned");
        Check(tutorial.abilities.Single() == ability, "Normal and tutorial cards share the effect");
        Check(ability.value == 1, "Damage value remains one");
        var cam = GameCamera.GetCamera();
        var random = UnityEngine.Random.state;
        var fx = UnityEngine.Object.Instantiate(ability.board_fx).GetComponent<NonomiBarrageFX>();
        float originalAspect = cam.aspect;
        try
        {
            foreach (float aspect in new[] { 4f/3f, 16f/9f, 21f/9f })
            {
                cam.aspect = aspect;
                fx.SamplePreview(.24f, cam);
                var origin = cam.WorldToViewportPoint(fx.transform.position);
                Check(Mathf.Abs(origin.x-.5f)<.001f && Mathf.Abs(origin.y-.035f)<.001f, "Bottom centre at aspect " + aspect.ToString("0.00"));
                int count = fx.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount);
                Check(count>50 && count<=200, "Bounded visible barrage at aspect " + aspect.ToString("0.00"));
            }
            fx.SamplePreview(.95f, cam);
            Check(fx.GetComponentsInChildren<ParticleSystem>().Sum(p=>p.particleCount)==0, "No particles remain after the burst");
            Check(UnityEngine.Random.state.Equals(random), "Effect does not change gameplay Unity random state");
        }
        finally { cam.aspect = originalAspect; UnityEngine.Object.Destroy(fx.gameObject); }

        // Exercise the real target selection and damage effect with isolated game state.
        var game = new Game("nonomi-verification", 2);
        foreach (var player in game.players) { player.hp = 30; player.hp_max = 30; }
        var caster = new Card(cardData.id, "verify-caster", 0);
        game.players[0].cards_all[caster.uid] = caster;
        game.players[0].cards_discard.Add(caster);
        string unitId = CardData.card_list.First(c => c.IsCitizen() && c.id != "Shishido_Izumi").id;
        var units = new List<Card>();
        int index = 0;
        foreach (Slot slot in Slot.GetAll())
        {
            int owner = index % 3 == 0 ? 0 : 1;
            var card = new Card(unitId, "verify-unit-" + index, owner) { hp = 5, slot = slot };
            game.players[owner].cards_all[card.uid] = card;
            game.players[owner].cards_board.Add(card);
            units.Add(card);
            index++;
        }
        Check(units.Count > 6, "Test covers the complete multi-row field");
        var hand = new Card(unitId, "verify-hand", 1) { hp = 5 };
        game.players[1].cards_all[hand.uid] = hand;
        game.players[1].cards_hand.Add(hand);
        var targets = ability.GetCardTargets(game, caster);
        Check(targets.Count == units.Count(c=>c.player_id==1) && targets.Distinct().Count()==targets.Count,
            "Every enemy board unit is targeted once");
        var logic = new GameLogic(game);
        foreach(var target in targets) ability.DoEffects(logic, caster, target);
        Check(units.Where(c=>c.player_id==1).All(c=>c.GetHP()==4), "All enemy board units lose exactly one HP");
        Check(units.Where(c=>c.player_id==0).All(c=>c.GetHP()==5), "Friendly board units take no damage");
        Check(hand.GetHP()==5 && game.players.All(p=>p.hp==30), "Hands and players take no damage");
        game.players[1].cards_board.Clear();
        Check(ability.GetCardTargets(game, caster).Count==0, "Empty enemy field is valid");

        await Task.Yield();
        int before = UnityEngine.Object.FindObjectsByType<NonomiBarrageFX>(FindObjectsSortMode.None).Length;
        for(int i=0;i<3;i++) client.onAbilityStart?.Invoke(ability, caster);
        int after = UnityEngine.Object.FindObjectsByType<NonomiBarrageFX>(FindObjectsSortMode.None).Length;
        Check(after==before+3, "Actual GameBoardFX event spawns one barrage per activation");
        float end = Time.time + 1.2f;
        double deadline = EditorApplication.timeSinceStartup + 8;
        while(Time.time<end && EditorApplication.timeSinceStartup<deadline) await Task.Yield();
        Check(Time.time>=end, "Live frames advanced during validation");
        Check(UnityEngine.Object.FindObjectsByType<NonomiBarrageFX>(FindObjectsSortMode.None).Length==0,
            "Three repeated activations clean up their GameObjects");
        return new { passed = checks.Count, checks };
    }
}
