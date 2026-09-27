using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using TcgEngine;
using TcgEngine.Client;
using TcgEngine.FX;

public static class ValidateHibikiRange
{
    public static string Main()
    {
        if (!EditorApplication.isPlaying)
            throw new InvalidOperationException("Run in the Game scene in Play mode.");
        var slots = BoardSlot.GetAll().ToArray();
        if (slots.Length == 0) throw new InvalidOperationException("No live board tiles.");
        var presenter = new BSlotIndicatorTypeSelector();
        var lateUpdate = typeof(BoardSlotFX).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        Action refresh = () => { foreach (var s in slots) lateUpdate.Invoke(s.GetBoardSlotFX(), null); };
        var results = new System.Collections.Generic.List<string>();
        foreach (var id in new[] { "Nekozuka_Hibiki", "Nekozuka_Hibiki_Tutorial" })
        {
            var game = new Game("hibiki-range-validation", 2);
            var caster = Card.Create(CardData.Get(id), VariantData.GetDefault(), game.players[0], "validation-hibiki");
            var ability = AbilityData.Get("OP_damage1_X");
            Check(caster.GetAbility(AbilityTarget.PlayTarget) == null, "Regression requires non-PlayTarget caster");
            game.selector = SelectorType.SelectTarget;
            game.selector_caster_uid = caster.uid;
            game.selector_ability_id = ability.id;
            Func<BoardSlot, BoardSlot[]> affected = selected => slots.Where(s =>
                ability.AreWideRangeConditionsMet(game, caster, selected.GetSlot(), s.GetSlot())
                && ability.AreTargetConditionsMet(game, caster, s.GetSlot())).ToArray();
            var center = slots.OrderByDescending(s => affected(s).Length).First();
            var expected = affected(center);
            Check(expected.Length == 5, "Hibiki center must affect five tiles");
            presenter.Execute(game, center);
            refresh();
            var visible = slots.Where(s => s.overlay_renderer.enabled).ToArray();
            Check(visible.Length == expected.Length && expected.All(visible.Contains), "Five actual live overlays enabled");
            Check(visible.All(s => s.overlay_renderer.sharedMaterial.shader.name == "TcgEngine/RangeHatching"), "Actual hatch materials");
            var edge = slots.OrderBy(s => affected(s).Length).First();
            presenter.Execute(game, edge);
            refresh();
            visible = slots.Where(s => s.overlay_renderer.enabled).ToArray();
            Check(visible.Length == affected(edge).Length && affected(edge).All(visible.Contains), "Retarget clears previous footprint");
            presenter.Execute(game, null);
            refresh();
            Check(slots.All(s => !s.overlay_renderer.enabled), "Leaving board clears range");
            game.selector_ability_id = "missing-validation-ability";
            presenter.Execute(game, center);
            refresh();
            Check(slots.All(s => !s.overlay_renderer.enabled), "Missing selector safely clears range");
            results.Add(id + ": five-tile center, edge retarget, clear, invalid selector PASS");
        }
        return string.Join("; ", results);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
