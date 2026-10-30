using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;
using System.Runtime.ExceptionServices;
using TcgEngine.Client;

namespace TcgEngine
{
    // Applies to spatial criteria only. Non-spatial criteria already determine their recipient type.
    public enum AbilityEffectTarget
    {
        Slot = 0,
        Unit = 1,
        Player = 2,
        UnitOrPlayer = 3,
    }

    public enum AbilityPlaybackMode
    {
        Simultaneous = 0,
        Sequential = 1,
    }

    /// <summary>
    /// Defines all ability data
    /// </summary>

    [CreateAssetMenu(fileName = "ability", menuName = "TcgEngine/AbilityData", order = 5)]
    public class AbilityData : ScriptableObject
    {
        public string id;

        [Header("Trigger")]
        public AbilityTrigger trigger;             //WHEN does the ability trigger?
        public ConditionData[] conditions_trigger; //Condition checked on the card triggering the ability (usually the caster)

        [Header("Repeat")]
        public RepeatConditionData condition_repeat; //Condition checked on the card triggering the ability (usually the caster).
                                                     //Iterations are paced Hearthstone-style: the condition is evaluated after the
                                                     //Death Phase of the previous iteration (deaths + death triggers resolved).

        [Header("Target")]
        public AbilityTarget criteria_target;               //WHO is targeted?
        [Tooltip("Spatial criteria: apply to the position itself, its unit, a teacher position, or a unit/teacher. Other criteria keep their native recipient type.")]
        public AbilityEffectTarget effect_target;          //Recipient projected from a selected/derived position
        public ConditionData[] conditions_criteria_target;  //Condition checked on the target to know if its a valid taget
        public ConditionWideAreaRange condition_wide_range;
        public ConditionData[] condition_target;
        public FilterData[] filters_target;  //Condition checked on the target to know if its a valid taget
        public SortData sort_target;         //Reorder the surviving targets (after filters), before effects are applied

        [Header("Effect")]
        public EffectData[] effects;              //WHAT this does?
        public StatusData[] status;               //Status added by this ability  
        public int value;                         //Value passed to the effect (deal X damage)
        public int duration;                      //Duration passed to the effect (usually for status, 0=permanent)

        [Header("Data Containers")]
        public DataContainer[] data_containers = System.Array.Empty<DataContainer>();

        [Header("Cancel")]
        public bool can_cancel = false;

        [Header("Chain/Choices")]
        public AbilityData[] chain_abilities;    //Abilities that will be triggered after this one

        [Header("Activated Ability")]
        public int mana_cost;                   //Mana cost for  activated abilities
        public bool exhaust;                    //Action cost for activated abilities

        [Header("FX")]
        [Tooltip("Simultaneous: all targets share one Impact. Sequential: each target's Impact reveals its own result, in target sort order.")]
        public AbilityPlaybackMode playback_mode = AbilityPlaybackMode.Simultaneous;
        public bool show_card_fx;               //Show caster card zoom on board when this ability triggers
        public GameObject board_fx;
        public GameObject caster_fx;
        [Tooltip("Looping decoration attached to an ongoing ability caster while on board and not silenced.")]
        public GameObject ongoing_fx;
        public GameObject target_fx;
        public AudioClip cast_audio;
        public AudioClip target_audio;
        public bool charge_target;

        [Header("Text")]
        public string title;
        [TextArea(5, 7)]
        public string desc;
        [TextArea(5, 7)]
        public string selector_desc;
        public KeywordData[] keywords = System.Array.Empty<KeywordData>(); //Keywords explained in the card preview

        public static List<AbilityData> ability_list = new List<AbilityData>();                             //Faster access in loops
        public static Dictionary<string, AbilityData> ability_dict = new Dictionary<string, AbilityData>(); //Faster access in Get(id)

        public static void Load(string folder = "")
        {
            if (ability_list.Count == 0)
            {
                ability_list.AddRange(Resources.LoadAll<AbilityData>(folder));

                foreach (AbilityData ability in ability_list)
                    ability_dict.Add(ability.id, ability);
            }
        }

        public string GetTitle()
        {
            return title;
        }

        public string GetDesc()
        {
            return desc;
        }

        public string GetDesc(CardData card)
        {
            string dsc = desc;
            dsc = dsc.Replace("<name>", card.title);
            dsc = dsc.Replace("<value>", value.ToString());
            dsc = dsc.Replace("<duration>", duration.ToString());
            return dsc;
        }

        public string GetDesc(ClubData club)
        {
            string dsc = desc;
            dsc = dsc.Replace("<name>", club.title);
            return dsc;
        }

        //Generic condition for the ability to trigger
        public bool AreTriggerConditionsMet(Game data, Card caster, AbilityEventContext context = null)
        {
            return AreTriggerConditionsMet(data, caster, caster, context: context); //Triggerer is the caster
        }

        //Some abilities are caused by another card (PlayOther), otherwise most of the time the triggerer is the caster, check condition on triggerer
        public bool AreTriggerConditionsMet(Game data, Card caster, Card trigger_card, AbilityEventContext context = null)
        {
            foreach (ConditionData cond in conditions_trigger)
            {
                if (cond != null)
                {
                    if (!ConditionEvaluation.Matches(cond, data, this, caster, trigger_card, ConditionStage.Trigger, context))
                        return false;
                }
            }
            return true;
        }

        //Some abilities are caused by an action on a player (OnFight when attacking the player), check condition on that player
        public bool AreTriggerConditionsMet(Game data, Card caster, Player trigger_player, AbilityEventContext context = null)
        {
            foreach (ConditionData cond in conditions_trigger)
            {
                if (cond != null)
                {
                    if (!ConditionEvaluation.Matches(cond, data, this, caster, trigger_player, ConditionStage.Trigger, context))
                        return false;
                }
            }
            return true;
        }
  
        public bool AreRepeatConditionsMet(Game data, int max_repeat_times, int repeat_times, AbilityEventContext context = null)
        {
            if (condition_repeat != null && !condition_repeat.IsRepeatConditionMet(data, this, max_repeat_times, repeat_times, context: context))
                return false;

            return true;
        }

        public bool AreOngoingRepeatConditionsMet(Game data, int max_repeat_times, int repeat_times, AbilityEventContext context = null)
        {
            // No repeat condition set => behave like a single execution (run up to max, default 1).
            // Returning true unconditionally here caused abilities with a null condition_repeat to
            // re-queue themselves forever in AfterAbilityResolved (infinite loop, e.g. chain_clear_stored_slot).
            if (condition_repeat == null)
                return repeat_times < max_repeat_times;

            return condition_repeat.IsOngoingRepeatConditionMet(data, this, max_repeat_times, repeat_times, context: context);
        }

        public int GetMaxRepeatTimes(Game data, Card caster, AbilityEventContext context = null)
        {
            if (condition_repeat != null)
                return condition_repeat.GetMaxRepeatTimes(data, this, caster, context: context);

            return 1;
        }

        

        //Check if the card target is valid
        public bool AreCriteriaTargetConditionsMet(Game data, Card caster, Card target_card, AbilityEventContext context = null)
        {
            foreach (ConditionData cond in conditions_criteria_target)
            {
                if (cond != null && !ConditionEvaluation.Matches(cond, data, this, caster, target_card, ConditionStage.Criteria, context))
                    return false;
            }
            return true;
        }

        //Check if the player target is valid
        public bool AreCriteriaTargetConditionsMet(Game data, Card caster, Player target_player, AbilityEventContext context = null)
        {
            foreach (ConditionData cond in conditions_criteria_target)
            {
                if (cond != null && !ConditionEvaluation.Matches(cond, data, this, caster, target_player, ConditionStage.Criteria, context))
                    return false;
            }
            return true;
        }

        //Check if the slot target is valid
        public bool AreCriteriaTargetConditionsMet(Game data, Card caster, Slot target_slot, AbilityEventContext context = null)
        {
            foreach (ConditionData cond in conditions_criteria_target)
            {
                if (cond != null && !ConditionEvaluation.Matches(cond, data, this, caster, target_slot, ConditionStage.Criteria, context))
                    return false;
            }
            return true;
        }

        //Check if the card data target is valid
        public bool AreCriteriaTargetConditionsMet(Game data, Card caster, CardData target_card, AbilityEventContext context = null)
        {
            foreach (ConditionData cond in conditions_criteria_target)
            {
                if (cond != null && !ConditionEvaluation.Matches(cond, data, this, caster, target_card, ConditionStage.Criteria, context))
                    return false;
            }
            return true;
        }

        public bool AreCriteriaTargetConditionsMet(Game data, Card caster, Slot selected, Slot target_slot, AbilityEventContext context = null)
        {
            foreach (ConditionData cond in conditions_criteria_target)
            {
                if (cond != null && !ConditionEvaluation.Matches(cond, data, this, caster, target_slot, ConditionStage.Criteria, context, selected))
                    return false;
            }
            return true;
        }

        //Check if the card target is valid
        public bool AreTargetConditionsMet(Game data, Card caster, Card target_card, AbilityEventContext context = null)
        {
            foreach (ConditionData cond in condition_target)
            {
                if (cond != null && !ConditionEvaluation.Matches(cond, data, this, caster, target_card, ConditionStage.Target, context))
                    return false;
            }
            return true;
        }

        //Check if the player target is valid
        public bool AreTargetConditionsMet(Game data, Card caster, Player target_player, AbilityEventContext context = null)
        {
            foreach (ConditionData cond in condition_target)
            {
                if (cond != null && !ConditionEvaluation.Matches(cond, data, this, caster, target_player, ConditionStage.Target, context))
                    return false;
            }
            return true;
        }

        //Check if the slot target is valid
        public bool AreTargetConditionsMet(Game data, Card caster, Slot target_slot, AbilityEventContext context = null)
        {
            foreach (ConditionData cond in condition_target)
            {
                if (cond != null && !ConditionEvaluation.Matches(cond, data, this, caster, target_slot, ConditionStage.Target, context))
                    return false;
            }
            return true;
        }

        //Check if the card data target is valid
        public bool AreTargetConditionsMet(Game data, Card caster, CardData target_card, AbilityEventContext context = null)
        {
            foreach (ConditionData cond in condition_target)
            {
                if (cond != null && !ConditionEvaluation.Matches(cond, data, this, caster, target_card, ConditionStage.Target, context))
                    return false;
            }
            return true;
        }

        public bool AreTargetConditionsMet(Game data, Card caster, Slot selected, Slot target_slot, AbilityEventContext context = null)
        {
            foreach (ConditionData cond in condition_target)
            {
                if (cond != null && !ConditionEvaluation.Matches(cond, data, this, caster, target_slot, ConditionStage.Target, context, selected))
                    return false;
            }
            return true;
        }

        //Check if the card target is valid
        public bool AreWideRangeConditionsMet(Game data, Card caster, Card target_card, AbilityEventContext context = null)
        {
            if (condition_wide_range != null && !ConditionEvaluation.Matches(condition_wide_range, data, this, caster, target_card, ConditionStage.Range, context))
                return false;

            return true;
        }

        //Check if the player target is valid
        public bool AreWideRangeConditionsMet(Game data, Card caster, Player target_player, AbilityEventContext context = null)
        {
            if (condition_wide_range != null && !ConditionEvaluation.Matches(condition_wide_range, data, this, caster, target_player, ConditionStage.Range, context))
                return false;
                    
            return true;
        }

        //Check if the slot target is valid
        public bool AreWideRangeConditionsMet(Game data, Card caster, Slot target_slot, AbilityEventContext context = null)
        {
            if (condition_wide_range != null && !ConditionEvaluation.Matches(condition_wide_range, data, this, caster, target_slot, ConditionStage.Range, context))
                return false;
                    
            return true;
        }

        //Check if the card data target is valid
        public bool AreWideRangeConditionsMet(Game data, Card caster, CardData target_card, AbilityEventContext context = null)
        {
            if (condition_wide_range != null && !ConditionEvaluation.Matches(condition_wide_range, data, this, caster, target_card, ConditionStage.Range, context))
                return false;
                    
            return true;
        }

        public bool AreWideRangeConditionsMet(Game data, Card caster, Slot selected, Slot target_slot, AbilityEventContext context = null)
        {
            if (condition_wide_range != null && !ConditionEvaluation.Matches(condition_wide_range, data, this, caster, target_slot, ConditionStage.Range, context, selected))
                return false;
                    
            return true;
        }

        //CanTarget is similar to AreTargetConditionsMet but only applies to targets on the board, with extra board-only conditions
        public bool UsesSpatialTargets()
        {
            return criteria_target == AbilityTarget.SelfSlot || criteria_target == AbilityTarget.AllSlots
                || criteria_target == AbilityTarget.PlayTarget || criteria_target == AbilityTarget.DeathSlot
                || criteria_target == AbilityTarget.AttachedSlot || IsTargetSelector();
        }

        public object GetSpatialRecipient(Game data, Slot position)
        {
            if (position.IsPlayerSlot())
                return effect_target == AbilityEffectTarget.Player || effect_target == AbilityEffectTarget.UnitOrPlayer
                    ? data.GetPlayer(position.p) : null;
            if (!position.IsValid()) return null;
            if (effect_target == AbilityEffectTarget.Slot) return position;
            if (effect_target == AbilityEffectTarget.Unit || effect_target == AbilityEffectTarget.UnitOrPlayer)
            {
                Card card = data.GetSlotCard(position);
                return card != null && card.CardData.IsBoardCard() ? card : null;
            }
            return null;
        }

        // Preview performs no filters or random draws; its conditions see the same projected type.
        public bool AreSpatialTargetConditionsMet(Game data, Card caster, Slot position, AbilityEventContext context = null)
        {
            object recipient = GetSpatialRecipient(data, position);
            if (recipient is Card card) return AreTargetConditionsMet(data, caster, card, context);
            if (recipient is Player player) return AreTargetConditionsMet(data, caster, player, context);
            return recipient is Slot slot && AreTargetConditionsMet(data, caster, slot, context);
        }

        private bool SpatialCriteria(Game data, Card caster, Slot position, AbilityEventContext context, bool selection)
        {
            if (position.IsPlayerSlot())
                return GetSpatialRecipient(data, position) is Player player
                    && AreCriteriaTargetConditionsMet(data, caster, player, context);
            if (!position.IsValid() || effect_target == AbilityEffectTarget.Player) return false;
            Card card = data.GetSlotCard(position);
            if (selection && effect_target != AbilityEffectTarget.Slot && card != null
                && (!data.CanInputTarget(card) || card.HasStatus(StatusType.Stealth)
                    || card.HasStatus(StatusType.SpellImmunity))) return false;
            return AreCriteriaTargetConditionsMet(data, caster, position, context);
        }

        public bool CanTarget(Game data, Card caster, Card target, AbilityEventContext context = null, bool evaluate = false)
        {
            if (caster == null || target == null) return false;
            if (UsesSpatialTargets()) return data.IsOnBoard(target) && CanTarget(data, caster, target.slot, context, evaluate);
            if (!evaluate) context = SelectionQuery(data, caster, context);
            if (IsTargetSelector() && !data.CanInputTarget(target)) return false;
            return !target.HasStatus(StatusType.Stealth) && !target.HasStatus(StatusType.SpellImmunity)
                && AreCriteriaTargetConditionsMet(data, caster, target, context);
        }

        public bool CanTarget(Game data, Card caster, Player target, AbilityEventContext context = null, bool evaluate = false)
        {
            if (caster == null || target == null) return false;
            if (UsesSpatialTargets()) return CanTarget(data, caster, new Slot(0, 0, target.player_id), context, evaluate);
            if (!evaluate) context = SelectionQuery(data, caster, context);
            return AreCriteriaTargetConditionsMet(data, caster, target, context);
        }

        public bool CanTarget(Game data, Card caster, Slot target, AbilityEventContext context = null, bool evaluate = false)
        {
            if (caster == null) return false;
            if (!evaluate) context = SelectionQuery(data, caster, context);
            return UsesSpatialTargets() ? SpatialCriteria(data, caster, target, context, true)
                : target.IsValid() && AreCriteriaTargetConditionsMet(data, caster, target, context);
        }

        public List<Slot> GetSelectableSlots(Game data, Card caster, AbilityEventContext context = null)
        {
            var result = new List<Slot>();
            foreach (Slot slot in Slot.GetAll())
                if (CanTarget(data, caster, slot, context)) result.Add(slot);
            foreach (Player player in data.players)
            {
                var slot = new Slot(0, 0, player.player_id);
                if (CanTarget(data, caster, slot, context)) result.Add(slot);
            }
            return result;
        }

        // Move selection: one (from, to) pair. UI, server CanTarget and AI share this check.
        // The departure unit follows the unit selection limits; the destination always follows Game.CanMoveCard.
        public bool CanTarget(Game data, Card caster, Slot from, Slot to, AbilityEventContext context = null, bool evaluate = false)
        {
            if (caster == null || !IsMoveSelector() || !from.IsValid() || !to.IsValid()) return false;
            Card unit = data.GetSlotCard(from);
            if (unit == null || !data.CanInputTarget(unit) || unit.HasStatus(StatusType.Stealth)
                || unit.HasStatus(StatusType.SpellImmunity) || !data.CanMoveCard(unit, to, true)) return false;
            if (!evaluate) context = SelectionQuery(data, caster, context);
            return AreCriteriaTargetConditionsMet(data, caster, from, to, context);
        }

        // Enumerates each board unit once as departure and each board slot once as destination.
        public List<(Slot from, Slot to)> GetSelectableMoves(Game data, Card caster, AbilityEventContext context = null)
        {
            return EnumerateSelectableMoves(data, caster, context).ToList();
        }

        private IEnumerable<(Slot from, Slot to)> EnumerateSelectableMoves(Game data, Card caster, AbilityEventContext context)
        {
            if (!IsMoveSelector()) yield break;
            List<Slot> slots = Slot.GetAll();
            foreach (Player player in data.players)
                foreach (Card unit in player.cards_board.ToList())
                    foreach (Slot to in slots)
                        if (CanTarget(data, caster, unit.slot, to, context)) yield return (unit.slot, to);
        }

        // A selector only accepts its authoritative cached candidates; validation never rerolls filters.
        public bool IsSelectionCandidate(Game data, Card caster, Card target, AbilityEventContext context)
        {
            return target != null && data.selector_card_uids != null && System.Array.IndexOf(data.selector_card_uids, target.uid) >= 0
                && AreCriteriaTargetConditionsMet(data, caster, target, context)
                && AreTargetConditionsMet(data, caster, target, context);
        }

        private AbilityEventContext SelectionQuery(Game data, Card caster, AbilityEventContext context)
        {
            return AbilityExecutionContext.Query(context ?? (IsPlaySelectTarget() && caster != null
                ? AbilityExecutionContext.PreparePlay(data, caster) : null));
        }

        //EffectStepDebug.LogEffect는 [Conditional("UNITY_EDITOR")]라 빌드에서는 호출 자체가 사라지고,
        //에디터에서도 치트가 꺼져 있으면 static bool 하나만 읽고 즉시 반환한다.
        // Keep intermediate replay snapshots, but publish only after GameLogic has
        // attached the target animation event and finished this target's statuses.
        public void DoEffects(GameLogic logic, Card caster, AbilityEventContext context = null)
        {
            foreach(EffectData effect in effects)
            {
                EffectStepDebug.LogEffect(this, effect, caster);
                effect?.DoEffect(logic, this, caster, context: context);
                logic.RecordReplayState();
            }
        }

        public void DoEffects(GameLogic logic, Card caster, Card target, AbilityEventContext context = null)
        {
            foreach (EffectData effect in effects)
            {
                EffectStepDebug.LogEffect(this, effect, caster, target);
                effect?.DoEffect(logic, this, caster, target, context: context);
                logic.RecordReplayState();
            }
            foreach(StatusData stat in status)
                target.AddStatus(stat, value, duration);
        }

        public void DoEffects(GameLogic logic, Card caster, List<Card> target, AbilityEventContext context = null)
        {
            foreach (EffectData effect in effects)
            {
                EffectStepDebug.LogEffect(this, effect, caster, target);
                effect?.DoEffect(logic, this, caster, target, context: context);
                logic.RecordReplayState();
            }
            foreach (Card targ in target)
            {
                foreach(StatusData stat in status)
                    targ.AddStatus(stat, value, duration);
            }
        }

        public void DoEffects(GameLogic logic, Card caster, Player target, AbilityEventContext context = null)
        {
            foreach (EffectData effect in effects)
            {
                EffectStepDebug.LogEffect(this, effect, caster, target);
                effect?.DoEffect(logic, this, caster, target, context: context);
                logic.RecordReplayState();
            }
            foreach (StatusData stat in status)
                target.AddStatus(stat, value, duration);
        }

        public void DoEffects(GameLogic logic, Card caster, Slot target, AbilityEventContext context = null)
        {
            Game game_data = logic.GetGameData();
            Card slot_card = game_data.GetSlotCard(target);
            foreach (EffectData effect in effects)
            {
                EffectStepDebug.LogEffect(this, effect, caster, target);
                effect?.DoEffect(logic, this, caster, target, context: context);
                logic.RecordReplayState();
            }
            if (slot_card != null)
            {
                foreach (StatusData stat in status)
                    slot_card.AddStatus(stat, value, duration);
            }

        }

        //Move selection: statuses go to the unit that was on the departure slot
        public void DoEffects(GameLogic logic, Card caster, Slot selected, Slot target, AbilityEventContext context = null)
        {
            Card unit = logic.GetGameData().GetSlotCard(selected);
            foreach (EffectData effect in effects)
            {
                EffectStepDebug.LogEffect(this, effect, caster, target);
                effect?.DoEffect(logic, this, caster, selected, target, context: context);
                logic.RecordReplayState();
            }
            if (unit != null)
            {
                foreach (StatusData stat in status)
                    unit.AddStatus(stat, value, duration);
            }
        }

        public void DoEffects(GameLogic logic, Card caster, CardData target, AbilityEventContext context = null)
        {
            foreach (EffectData effect in effects)
            {
                EffectStepDebug.LogEffect(this, effect, caster, target);
                effect?.DoEffect(logic, this, caster, target, context: context);
                logic.RecordReplayState();
            }
        }

        public bool HasEffectOrStatus()
        {
            return (effects != null && System.Array.Exists(effects, effect => effect != null))
                || (status != null && System.Array.Exists(status, entry => entry != null));
        }

        public void DoOngoingEffects(GameLogic logic, Card caster, Card target)
        {
            foreach (EffectData effect in effects)
            {
                effect?.DoOngoingEffect(logic, this, caster, target);
            }
            foreach (StatusData stat in status)
                target.AddOngoingStatus(stat, value);
        }

        public void DoOngoingEffects(GameLogic logic, Card caster, Player target)
        {
            foreach (EffectData effect in effects)
                effect?.DoOngoingEffect(logic, this, caster, target);
            foreach (StatusData stat in status)
                target.AddOngoingStatus(stat, value);
        }

        public bool HasEffect<T>() where T : EffectData
        {
            foreach (EffectData eff in effects)
            {
                if (eff != null && eff is T)
                    return true;
            }
            return false;
        }

        public bool HasStatus(StatusType type)
        {
            foreach (StatusData sta in status)
            {
                if (sta != null && sta.effect == type)
                    return true;
            }
            return false;
        }

        private void AddValidCards(Game data, Card caster, List<Card> source, List<Card> targets, AbilityEventContext context = null)
        {
            foreach (Card card in source)
            {
                if (AreCriteriaTargetConditionsMet(data, caster, card, context: context))
                    targets.Add(card);
            }
        }

        //Return cards targets,  memory_array is used for optimization and avoid allocating new memory
        public List<Card> GetCardTargets(Game data, Card caster, ListSwap<Card> memory_array = null, AbilityEventContext context = null)
        {
            if (memory_array == null)
                memory_array = new ListSwap<Card>(); //Slow operation

            List<Card> candidate_targets = memory_array.Get();

            if (criteria_target == AbilityTarget.Self)
            {
                if (AreCriteriaTargetConditionsMet(data, caster, caster, context: context))
                    candidate_targets.Add(caster);
            }

            if (criteria_target == AbilityTarget.AllCardsHand)
            {
                foreach (Player player in data.players)
                {
                    foreach (Card card in player.cards_hand)
                    {
                        if (AreCriteriaTargetConditionsMet(data, caster, card, context: context))
                            candidate_targets.Add(card);
                    }
                }
            }

            if (criteria_target == AbilityTarget.AllCardsBoard)
            {
                foreach (Player player in data.players)
                {
                    foreach (Card card in player.cards_board)
                    {
                        if (AreCriteriaTargetConditionsMet(data, caster, card, context: context))
                            candidate_targets.Add(card);
                    }
                }
            }

            if (criteria_target == AbilityTarget.AllCardsAllPiles || criteria_target == AbilityTarget.CardSelector)
            {
                foreach (Player player in data.players)
                {
                    AddValidCards(data, caster, player.cards_club, candidate_targets, context: context);
                    AddValidCards(data, caster, player.cards_deck, candidate_targets, context: context);
                    AddValidCards(data, caster, player.cards_discard, candidate_targets, context: context);
                    AddValidCards(data, caster, player.cards_hand, candidate_targets, context: context);
                    AddValidCards(data, caster, player.cards_secret, candidate_targets, context: context);
                    AddValidCards(data, caster, player.cards_board, candidate_targets, context: context);
                    AddValidCards(data, caster, player.cards_equip, candidate_targets, context: context);
                    AddValidCards(data, caster, player.cards_attach, candidate_targets, context: context);
                    AddValidCards(data, caster, player.cards_temp, candidate_targets, context: context);
                    AddValidCards(data, caster, player.player_ability, candidate_targets, context: context);
                }
            }

            if (criteria_target == AbilityTarget.AbilityTriggerer)
            {
                Card target = data.GetCard((context as AbilityExecutionContext)?.trigger_uid ?? data.ability_triggerer);
                if (target != null && AreCriteriaTargetConditionsMet(data, caster, target, context: context))
                    candidate_targets.Add(target);
            }

            if (criteria_target == AbilityTarget.EquippedCard)
            {
                if (caster.CardData.IsEquipment())
                {
                    //Get bearer of the equipment
                    Player player = data.GetPlayer(caster.player_id);
                    Card target = player.GetBearerCard(caster);
                    if (target != null && AreCriteriaTargetConditionsMet(data, caster, target, context: context))
                        candidate_targets.Add(target);
                }
                else if(caster.equipped_uid != null)
                {
                    //Get equipped card
                    Card target = data.GetCard(caster.equipped_uid);
                    if (target != null && AreCriteriaTargetConditionsMet(data, caster, target, context: context))
                        candidate_targets.Add(target);
                }
            }

            List<Card> final_candidate_target = new List<Card>();
            List<Slot> all_slots = Slot.GetAll();
            

            foreach (Card candidate in candidate_targets)
            {
                if (data.IsOnBoard(candidate))
                {
                    foreach (Slot s in all_slots)
                    {
                        if (AreWideRangeConditionsMet(data, caster, candidate.slot, s, context: context) && data.GetSlotCard(s) != null)
                        {
                            if (!final_candidate_target.Contains(data.GetSlotCard(s)))
                                final_candidate_target.Add(data.GetSlotCard(s));
                        }
                    }

                }
                else
                {
                    if (!final_candidate_target.Contains(candidate))
                        final_candidate_target.Add(candidate);
                }
            }

            List<Card> targets = new List<Card>();

            if (final_candidate_target.Count > 0)
            {
                foreach (Card c in final_candidate_target)
                {
                    if (AreTargetConditionsMet(data, caster, c, context: context))
                        targets.Add(c);
                }

            }

            //Filter targets
            if (filters_target != null && targets.Count > 0)
            {
                foreach (FilterData filter in filters_target)
                {
                    if (filter != null)
                        targets = filter.FilterTargets(data, this, caster, targets, memory_array.GetOther(targets));
                }
            }

            //Sort targets
            if (sort_target != null && targets.Count > 1)
                targets = sort_target.SortTargets(data, this, caster, targets);

            return targets;
        }

        //Return player targets,  memory_array is used for optimization and avoid allocating new memory
        public List<Player> GetPlayerTargets(Game data, Card caster, ListSwap<Player> memory_array = null, AbilityEventContext context = null)
        {
            if (memory_array == null)
                memory_array = new ListSwap<Player>(); //Slow operation

            List<Player> targets = memory_array.Get();

            if (criteria_target == AbilityTarget.PlayerSelf)
            {
                Player player = data.GetPlayer(caster.player_id);
                targets.Add(player);
            }
            else if (criteria_target == AbilityTarget.PlayerOpponent)
            {
                for (int tp = 0; tp < data.players.Length; tp++)
                {
                    if (tp != caster.player_id)
                    {
                        Player oplayer = data.players[tp];
                        targets.Add(oplayer);
                    }
                }
            }
            else if (criteria_target == AbilityTarget.AllPlayers)
            {
                targets.AddRange(data.players);
            }

            if (criteria_target == AbilityTarget.ParentSlotPlayers && context is AbilityExecutionContext execution)
            {
                foreach (Slot slot in execution.parent_slots)
                {
                    if (!slot.IsValid() || !AreCriteriaTargetConditionsMet(data, caster, slot, context)) continue;
                    Player player = data.GetPlayer(slot.GetP());
                    if (player != null && !targets.Contains(player) && AreTargetConditionsMet(data, caster, player, context))
                        targets.Add(player);
                }
            }

            //Filter targets
            if (filters_target != null && targets.Count > 0)
            {
                foreach (FilterData filter in filters_target)
                {
                    if (filter != null)
                        targets = filter.FilterTargets(data, this, caster, targets, memory_array.GetOther(targets));
                }
            }

            //Sort targets
            if (sort_target != null && targets.Count > 1)
                targets = sort_target.SortTargets(data, this, caster, targets);

            return targets;
        }

        // Compatibility accessor for consumers explicitly requesting final Slot recipients.
        public List<Slot> GetSlotTargets(Game data, Card caster, bool is_play_target = false,
            AbilityEventContext context = null)
        {
            return GetSpatialTargets(data, caster, context, is_play_target: is_play_target).OfType<Slot>().ToList();
        }

        // Selection, play position and automatic spatial criteria share expansion and projection.
        public List<object> GetSpatialTargets(Game data, Card caster, AbilityEventContext context = null,
            Slot? selected = null, bool is_play_target = false)
        {
            var criteria = new List<Slot>();
            if (!UsesSpatialTargets()) return new List<object>();
            if (selected.HasValue)
            {
                if (CanTarget(data, caster, selected.Value, context, true)) criteria.Add(selected.Value);
            }
            else if (criteria_target == AbilityTarget.PlayTarget)
            {
                if (is_play_target && CanTarget(data, caster, caster.slot, context, true)) criteria.Add(caster.slot);
            }
            else if (IsPlaySelectTarget())
            {
                if (CanTarget(data, caster, caster.play_target_slot, context, true)) criteria.Add(caster.play_target_slot);
            }
            else if (criteria_target == AbilityTarget.AllSlots)
            {
                foreach (Slot slot in Slot.GetAll())
                    if (SpatialCriteria(data, caster, slot, context, false)) criteria.Add(slot);
            }
            else
            {
                Slot slot = criteria_target == AbilityTarget.DeathSlot
                    ? (AbilityExecutionContext.Event(context) as DeathEventContext)?.slot ?? Slot.None
                    : criteria_target == AbilityTarget.SelfSlot || criteria_target == AbilityTarget.AttachedSlot ? caster.slot : Slot.None;
                if (SpatialCriteria(data, caster, slot, context, false)) criteria.Add(slot);
            }

            var positions = new List<Slot>();
            var seen = new HashSet<Slot>();
            foreach (Slot center in criteria)
            {
                if (center.IsPlayerSlot())
                {
                    if (seen.Add(center)) positions.Add(center);
                    continue;
                }
                foreach (Slot slot in Slot.GetAll())
                    if (AreWideRangeConditionsMet(data, caster, center, slot, context) && seen.Add(slot)) positions.Add(slot);
            }

            // Freeze recipients before any effect runs. Empty unit positions contribute no Card.
            var recipients = positions.Select(slot => GetSpatialRecipient(data, slot)).Where(t => t != null).Distinct().ToList();
            if (recipients.Count == 0) return recipients;
            if (recipients[0] is Card)
                return RefineSpatialTargets(recipients.Cast<Card>().ToList(),
                    card => AreTargetConditionsMet(data, caster, card, context),
                    (filter, list) => filter.FilterTargets(data, this, caster, list, new List<Card>()),
                    list => sort_target.SortTargets(data, this, caster, list)).Cast<object>().ToList();
            if (recipients[0] is Player)
                return RefineSpatialTargets(recipients.Cast<Player>().ToList(),
                    player => AreTargetConditionsMet(data, caster, player, context),
                    (filter, list) => filter.FilterTargets(data, this, caster, list, new List<Player>()),
                    list => sort_target.SortTargets(data, this, caster, list)).Cast<object>().ToList();
            return RefineSpatialTargets(recipients.Cast<Slot>().ToList(),
                slot => AreTargetConditionsMet(data, caster, slot, context),
                (filter, list) => filter.FilterTargets(data, this, caster, list, new List<Slot>()),
                list => sort_target.SortTargets(data, this, caster, list)).Cast<object>().ToList();
        }

        private List<T> RefineSpatialTargets<T>(List<T> targets, Func<T, bool> qualifies,
            Func<FilterData, List<T>, List<T>> filter, Func<List<T>, List<T>> sort)
        {
            targets = targets.Where(qualifies).ToList();
            if (filters_target != null)
                foreach (FilterData item in filters_target)
                    if (item != null && targets.Count > 0) targets = filter(item, targets);
            if (sort_target != null && targets.Count > 1) targets = sort(targets);
            return targets;
        }

        public List<CardData> GetCardDataTargets(Game data, Card caster, ListSwap<CardData> memory_array = null, AbilityEventContext context = null)
        {
            if (memory_array == null)
                memory_array = new ListSwap<CardData>(); //Slow operation

            List<CardData> targets = memory_array.Get();

            if (criteria_target == AbilityTarget.AllCardData)
            {
                foreach (CardData card in CardData.GetAll())
                {
                    if (AreCriteriaTargetConditionsMet(data, caster, card, context: context))
                        targets.Add(card);
                }
            }

            //Filter targets
            if (filters_target != null && targets.Count > 0)
            {
                foreach (FilterData filter in filters_target)
                {
                    if (filter != null)
                        targets = filter.FilterTargets(data, this, caster, targets, memory_array.GetOther(targets));
                }
            }

            //Sort targets
            if (sort_target != null && targets.Count > 1)
                targets = sort_target.SortTargets(data, this, caster, targets);

            return targets;
        }

        // Check if there is any valid target, if not, AI wont try to cast activated ability
        public bool HasValidSelectTarget(Game game_data, Card caster, AbilityEventContext context = null)
        {
            context = SelectionQuery(game_data, caster, context);
            if (IsTargetSelector())
                return GetSelectableSlots(game_data, caster, context).Count > 0;

            if (IsMoveSelector())
                return EnumerateSelectableMoves(game_data, caster, context).Any();

            if (criteria_target == AbilityTarget.CardSelector)
            {
                if (HasValidCardTarget(game_data, caster, context: context))
                    return true;
                return false;
            }

            if (criteria_target == AbilityTarget.ChoiceSelector)
            {
                foreach (AbilityData choice in chain_abilities)
                {
                    if (choice != null && choice.AreTriggerConditionsMet(game_data, caster, context: AbilityExecutionContext.Query(
                        new AbilityExecutionContext(game_data, caster, null, context))))
                        return true;
                }
                return false;
            }

            return true; //Not selecting, valid
        }

        public bool HasValidCardTarget(Game game_data, Card caster, AbilityEventContext context = null)
        {
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                bool v1 = HasValidCardTarget(game_data, caster, player.cards_club, context: context);
                bool v2 = HasValidCardTarget(game_data, caster, player.cards_deck, context: context);
                bool v3 = HasValidCardTarget(game_data, caster, player.cards_discard, context: context);
                bool v4 = HasValidCardTarget(game_data, caster, player.cards_hand, context: context);
                bool v5 = HasValidCardTarget(game_data, caster, player.cards_board, context: context);
                bool v6 = HasValidCardTarget(game_data, caster, player.cards_equip, context: context);
                bool v7 = HasValidCardTarget(game_data, caster, player.cards_attach, context: context);
                bool v8 = HasValidCardTarget(game_data, caster, player.cards_secret, context: context);
                bool v9 = HasValidCardTarget(game_data, caster, player.cards_temp, context: context);
                bool v10 = HasValidCardTarget(game_data, caster, player.player_ability, context: context);
                if (v1 || v2 || v3 || v4 || v5 || v6 || v7 || v8 || v9 || v10)
                    return true;
            }
            return false;
        }

        public bool HasValidCardTarget(Game game_data, Card caster, List<Card> list, AbilityEventContext context = null)
        {
            for (int c = 0; c < list.Count; c++)
            {
                Card card = list[c];
                if (AreCriteriaTargetConditionsMet(game_data, caster, card, context: context))
                    return true;
            }
            return false;
        }

        // Selection candidates were finalized when the selector opened; never reroll its filters.
        public List<Card> GetSelectionCandidates(Game data, Card caster)
        {
            var result = new List<Card>();
            if (data.selector_card_uids == null) return result;
            var query = AbilityExecutionContext.Query(data.selector_context);
            foreach (string uid in data.selector_card_uids)
            {
                Card card = data.GetCard(uid);
                if (IsSelectionCandidate(data, caster, card, query)) result.Add(card);
            }
            return result;
        }

        public bool IsTargetSelector()
        {
            return criteria_target == AbilityTarget.SelectTarget || criteria_target == AbilityTarget.SelectSlot;
        }

        public bool IsMoveSelector()
        {
            return criteria_target == AbilityTarget.SelectMove;
        }

        public bool IsSelector()
        {
            return IsTargetSelector() || IsMoveSelector() || criteria_target == AbilityTarget.CardSelector || criteria_target == AbilityTarget.ChoiceSelector;
        }

        //Target selected while the card is being played (GameLogic.SelectPlayTarget), stored on the caster until this OnPlay resolves
        public bool IsPlaySelectTarget()
        {
            return trigger == AbilityTrigger.OnPlay && (IsTargetSelector() || IsMoveSelector());
        }

        public static AbilityData Get(string id)
        {
            if (id == null)
                return null;
            bool success = ability_dict.TryGetValue(id, out AbilityData ability);
            if (success)
                return ability;
            return null;
        }

        public static List<AbilityData> GetAll()
        {
            return ability_list;
        }
    }


    public enum AbilityTrigger
    {
        None = 0,

        OnGameStart = 1, //Once, before the mulligan

        Ongoing = 2,  //Always active (does not work with all effects)
        Activate = 5, //Action

        OnPlay = 10,  //When playeds
        OnPlayOther = 11,  //When another card played

        OnUse = 13,
        OnUseOther = 14,

        StartOfTurn = 20, //Every turn
        EndOfTurn = 22, //Every turn

        OnBeforeAttack = 30, //When attacking, before damage
        OnBeforeAttackOther = 31,
        OnAfterAttack = 32, //When attacking, after damage if still alive
        OnAfterAttackOther = 33,
        OnBeforeDefend = 34, //When being attacked, before damage
        OnBeforeDefendOther = 35,
        OnAfterDefend = 36, //When being attacked, after damage if still alive
        OnAfterDefendOther = 37,
        OnAfterDamage = 38,
        OnKill = 39,        //When killing another card during an attack

        OnDeath = 40, //When dying
        OnDeathOther = 42, //When another dying

        OnDraw = 45,

        OnMove = 47, //When this card moves to another slot (player move and forced relocation such as move effects or knockback)

        OnHeal = 50,
        OnHealOther = 51,

        OnAddClubOther = 55, //When a club is added to a card; the joined card is the triggerer
    }

    public enum AbilityTarget
    {
        None = 0,
        Self = 1,
        SelfSlot = 2,

        PlayerSelf = 4,
        PlayerOpponent = 5,
        AllPlayers = 7,
        ParentSlotPlayers = 8,  //Map filtered parent result slots to their territory players

        AllCardsBoard = 10,
        AllCardsHand = 11,
        AllCardsAllPiles = 12,
        Club = 13,              //The caster owner's club card(s) in cards_club (ongoing path only)
        AllSlots = 15,
        AllCardData = 17,       //For card Create effects only

        PlayTarget = 20,        //The target selected at the same time the spell was played (spell only)    
        AbilityTriggerer = 25,   //The card that triggered the trap
        DeathSlot = 26,         //The victim's slot captured by the current death event
        EquippedCard = 27,       //If equipment, the bearer, if citizen, the item equipped
        AttachedSlot = 28,

        SelectTarget = 30,        //Serialized alias for position selection
        SelectSlot = 31,          //Select a board slot; clicking its occupant selects the slot
        SelectMove = 32,          //Drag a board unit to a destination slot; conditions receive (from, to) through ConditionCanMove

        CardSelector = 40,          //Card selector menu
        ChoiceSelector = 50,        //Choice selector menu
    }

}
