using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;
using System.Runtime.ExceptionServices;
using UnityEditor.ShaderGraph.Drawing;

namespace TcgEngine
{
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
        public RepeatConditionData condition_repeat; //Condition checked on the card triggering the ability (usually the caster)

        [Header("Target")]
        public AbilityTarget criteria_target;               //WHO is targeted?
        public ConditionData[] conditions_criteria_target;  //Condition checked on the target to know if its a valid taget
        public ConditionData condition_wide_range;
        public ConditionData[] condition_target;
        public FilterData[] filters_target;  //Condition checked on the target to know if its a valid taget

        [Header("Effect")]
        public EffectData[] effects;              //WHAT this does?
        public StatusData[] status;               //Status added by this ability  
        public int value;                         //Value passed to the effect (deal X damage)
        public int duration;                      //Duration passed to the effect (usually for status, 0=permanent)

        [Header("Chain/Choices")]
        public AbilityData[] chain_abilities;    //Abilities that will be triggered after this one

        [Header("Activated Ability")]
        public int mana_cost;                   //Mana cost for  activated abilities
        public bool exhaust;                    //Action cost for activated abilities

        [Header("FX")]
        public GameObject board_fx;
        public GameObject caster_fx;
        public GameObject target_fx;
        public AudioClip cast_audio;
        public AudioClip target_audio;
        public bool charge_target;

        [Header("Text")]
        public string title;
        [TextArea(5, 7)]
        public string desc;

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
        public bool AreTriggerConditionsMet(Game data, Card caster)
        {
            return AreTriggerConditionsMet(data, caster, caster); //Triggerer is the caster
        }

        //Some abilities are caused by another card (PlayOther), otherwise most of the time the triggerer is the caster, check condition on triggerer
        public bool AreTriggerConditionsMet(Game data, Card caster, Card trigger_card)
        {
            foreach (ConditionData cond in conditions_trigger)
            {
                if (cond != null)
                {
                    if (!cond.IsTriggerConditionMet(data, this, caster))
                        return false;
                    if (!cond.IsTargetConditionMet(data, this, caster, trigger_card))
                        return false;
                }
            }
            return true;
        }

        //Some abilities are caused by an action on a player (OnFight when attacking the player), check condition on that player
        public bool AreTriggerConditionsMet(Game data, Card caster, Player trigger_player)
        {
            foreach (ConditionData cond in conditions_trigger)
            {
                if (cond != null)
                {
                    if (!cond.IsTriggerConditionMet(data, this, caster))
                        return false;
                    if (!cond.IsTargetConditionMet(data, this, caster, trigger_player))
                        return false;
                }
            }
            return true;
        }
  
        public bool AreRepeatConditionsMet(Game data, int max_repeat_times, int repeat_times)
        {
            if (condition_repeat != null && !condition_repeat.IsRepeatConditionMet(data, this, max_repeat_times, repeat_times))
                return false;

            return true;
        }

        public bool AreOngoingRepeatConditionsMet(Game data, int max_repeat_times, int repeat_times)
        {
            if (condition_repeat != null && !condition_repeat.IsOngoingRepeatConditionMet(data, this, max_repeat_times, repeat_times))
                return false;

            return true;
        }

        public int GetMaxRepeatTimes(Game data, Card caster)
        {
            if (condition_repeat != null)
                return condition_repeat.GetMaxRepeatTimes(data, this, caster);

            return 1;
        }

        

        //Check if the card target is valid
        public bool AreCriteriaTargetConditionsMet(Game data, Card caster, Card target_card)
        {
            foreach (ConditionData cond in conditions_criteria_target)
            {
                if (cond != null && !cond.IsTargetConditionMet(data, this, caster, target_card))
                    return false;
            }
            return true;
        }

        //Check if the player target is valid
        public bool AreCriteriaTargetConditionsMet(Game data, Card caster, Player target_player)
        {
            foreach (ConditionData cond in conditions_criteria_target)
            {
                if (cond != null && !cond.IsTargetConditionMet(data, this, caster, target_player))
                    return false;
            }
            return true;
        }

        //Check if the slot target is valid
        public bool AreCriteriaTargetConditionsMet(Game data, Card caster, Slot target_slot)
        {
            foreach (ConditionData cond in conditions_criteria_target)
            {
                if (cond != null && !cond.IsTargetConditionMet(data, this, caster, target_slot))
                    return false;
            }
            return true;
        }

        //Check if the card data target is valid
        public bool AreCriteriaTargetConditionsMet(Game data, Card caster, CardData target_card)
        {
            foreach (ConditionData cond in conditions_criteria_target)
            {
                if (cond != null && !cond.IsTargetConditionMet(data, this, caster, target_card))
                    return false;
            }
            return true;
        }

        public bool AreCriteriaTargetConditionsMet(Game data, Card caster, Slot selected, Slot target_slot)
        {
            foreach (ConditionData cond in conditions_criteria_target)
            {
                if (cond != null && !cond.IsTargetConditionMet(data, this, caster, selected, target_slot))
                    return false;
            }
            return true;
        }

        //Check if the card target is valid
        public bool AreTargetConditionsMet(Game data, Card caster, Card target_card)
        {
            foreach (ConditionData cond in condition_target)
            {
                if (cond != null && !cond.IsTargetConditionMet(data, this, caster, target_card))
                    return false;
            }
            return true;
        }

        //Check if the player target is valid
        public bool AreTargetConditionsMet(Game data, Card caster, Player target_player)
        {
            foreach (ConditionData cond in condition_target)
            {
                if (cond != null && !cond.IsTargetConditionMet(data, this, caster, target_player))
                    return false;
            }
            return true;
        }

        //Check if the slot target is valid
        public bool AreTargetConditionsMet(Game data, Card caster, Slot target_slot)
        {
            foreach (ConditionData cond in condition_target)
            {
                if (cond != null && !cond.IsTargetConditionMet(data, this, caster, target_slot))
                    return false;
            }
            return true;
        }

        //Check if the card data target is valid
        public bool AreTargetConditionsMet(Game data, Card caster, CardData target_card)
        {
            foreach (ConditionData cond in condition_target)
            {
                if (cond != null && !cond.IsTargetConditionMet(data, this, caster, target_card))
                    return false;
            }
            return true;
        }

        public bool AreTargetConditionsMet(Game data, Card caster, Slot selected, Slot target_slot)
        {
            foreach (ConditionData cond in condition_target)
            {
                if (cond != null && !cond.IsTargetConditionMet(data, this, caster, selected, target_slot))
                    return false;
            }
            return true;
        }

        //Check if the card target is valid
        public bool AreWideRangeConditionsMet(Game data, Card caster, Card target_card)
        {
            if (condition_wide_range != null && !condition_wide_range.IsTargetConditionMet(data, this, caster, target_card))
                return false;

            return true;
        }

        //Check if the player target is valid
        public bool AreWideRangeConditionsMet(Game data, Card caster, Player target_player)
        {
            if (condition_wide_range != null && !condition_wide_range.IsTargetConditionMet(data, this, caster, target_player))
                return false;
                    
            return true;
        }

        //Check if the slot target is valid
        public bool AreWideRangeConditionsMet(Game data, Card caster, Slot target_slot)
        {
            if (condition_wide_range != null && !condition_wide_range.IsTargetConditionMet(data, this, caster, target_slot))
                return false;
                    
            return true;
        }

        //Check if the card data target is valid
        public bool AreWideRangeConditionsMet(Game data, Card caster, CardData target_card)
        {
            if (condition_wide_range != null && !condition_wide_range.IsTargetConditionMet(data, this, caster, target_card))
                return false;
                    
            return true;
        }

        public bool AreWideRangeConditionsMet(Game data, Card caster, Slot selected, Slot target_slot)
        {
            if (condition_wide_range != null && !condition_wide_range.IsTargetConditionMet(data, this, caster, selected, target_slot))
                return false;
                    
            return true;
        }

        //CanTarget is similar to AreTargetConditionsMet but only applies to targets on the board, with extra board-only conditions
        public bool CanTarget(Game data, Card caster, Card target)
        {
            if (target.HasStatus(StatusType.Stealth))
                return false; //Hidden

            if (target.HasStatus(StatusType.SpellImmunity))
                return false; //Spell immunity

            bool condition_match = AreCriteriaTargetConditionsMet(data, caster, target);
            return condition_match;
        }

        //Can target check additional restrictions and is usually for SelectTarget or PlayTarget abilities
        public bool CanTarget(Game data, Card caster, Player target)
        {
            bool condition_match = AreCriteriaTargetConditionsMet(data, caster, target);
            return condition_match;
        }

        public bool CanTarget(Game data, Card caster, Slot target)
        {
            return AreCriteriaTargetConditionsMet(data, caster, target); //No additional conditions for slots
        }

        //Check if destination array has the target after being filtered, used to support filters in CardSelector
        public bool IsCardSelectionValid(Game data, Card caster, Card target, ListSwap<Card> card_array = null)
        {
            List<Card> targets = GetCardTargets(data, caster, card_array);
            return targets.Contains(target); //Card is still in array after filtering
        }

        public void DoEffects(GameLogic logic, Card caster)
        {
            foreach(EffectData effect in effects)
                effect?.DoEffect(logic, this, caster);
        }

        public void DoEffects(GameLogic logic, Card caster, Card target)
        {
            foreach (EffectData effect in effects)
                effect?.DoEffect(logic, this, caster, target);
            foreach(StatusData stat in status)
                target.AddStatus(stat, value, duration);
        }

        public void DoEffects(GameLogic logic, Card caster, List<Card> target)
        {
            foreach (EffectData effect in effects)
                effect?.DoEffect(logic, this, caster, target);
            foreach (Card targ in target)
            {
                foreach(StatusData stat in status)
                    targ.AddStatus(stat, value, duration);
            }
        }

        public void DoEffects(GameLogic logic, Card caster, Player target)
        {
            foreach (EffectData effect in effects)
                effect?.DoEffect(logic, this, caster, target);
            foreach (StatusData stat in status)
                target.AddStatus(stat, value, duration);
        }

        public void DoEffects(GameLogic logic, Card caster, Slot target)
        {
            foreach (EffectData effect in effects)
                effect?.DoEffect(logic, this, caster, target);
        }

        public void DoEffects(GameLogic logic, Card caster, CardData target)
        {
            foreach (EffectData effect in effects)
                effect?.DoEffect(logic, this, caster, target);
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

        private void AddValidCards(Game data, Card caster, List<Card> source, List<Card> targets)
        {
            foreach (Card card in source)
            {
                if (AreCriteriaTargetConditionsMet(data, caster, card))
                    targets.Add(card);
            }
        }

        //Return cards targets,  memory_array is used for optimization and avoid allocating new memory
        public List<Card> GetCardTargets(Game data, Card caster, ListSwap<Card> memory_array = null)
        {
            if (memory_array == null)
                memory_array = new ListSwap<Card>(); //Slow operation

            List<Card> candidate_targets = memory_array.Get();

            if (criteria_target == AbilityTarget.Self)
            {
                if (AreCriteriaTargetConditionsMet(data, caster, caster))
                    candidate_targets.Add(caster);
            }

            if (criteria_target == AbilityTarget.AllCardsBoard || criteria_target == AbilityTarget.SelectTarget || criteria_target == AbilityTarget.SelectCard)
            {
                foreach (Player player in data.players)
                {
                    foreach (Card card in player.cards_board)
                    {
                        if (AreCriteriaTargetConditionsMet(data, caster, card))
                            candidate_targets.Add(card);
                    }
                }
            }

            if (criteria_target == AbilityTarget.AllCardsHand)
            {
                foreach (Player player in data.players)
                {
                    foreach (Card card in player.cards_hand)
                    {
                        if (AreCriteriaTargetConditionsMet(data, caster, card))
                            candidate_targets.Add(card);
                    }
                }
            }

            if (criteria_target == AbilityTarget.AllCardsAllPiles || criteria_target == AbilityTarget.CardSelector)
            {
                foreach (Player player in data.players)
                {
                    AddValidCards(data, caster, player.cards_club, candidate_targets);
                    AddValidCards(data, caster, player.cards_deck, candidate_targets);
                    AddValidCards(data, caster, player.cards_discard, candidate_targets);
                    AddValidCards(data, caster, player.cards_hand, candidate_targets);
                    AddValidCards(data, caster, player.cards_secret, candidate_targets);
                    AddValidCards(data, caster, player.cards_board, candidate_targets);
                    AddValidCards(data, caster, player.cards_equip, candidate_targets);
                    AddValidCards(data, caster, player.cards_attach, candidate_targets);
                    AddValidCards(data, caster, player.cards_temp, candidate_targets);
                    AddValidCards(data, caster, player.player_ability, candidate_targets);
                }
            }

            if (criteria_target == AbilityTarget.LastPlayed)
            {
                Card target = data.GetCard(data.last_played);
                if (target != null && AreCriteriaTargetConditionsMet(data, caster, target))
                    candidate_targets.Add(target);
            }

            if (criteria_target == AbilityTarget.LastDestroyed)
            {
                Card target = data.GetCard(data.last_destroyed);
                if (target != null && AreCriteriaTargetConditionsMet(data, caster, target))
                    candidate_targets.Add(target);
            }

            if (criteria_target == AbilityTarget.LastTargeted)
            {
                Card target = data.GetCard(data.last_target);
                if (target != null && AreCriteriaTargetConditionsMet(data, caster, target))
                    candidate_targets.Add(target);
            }

            if (criteria_target == AbilityTarget.LastAttack)
            {
                Card target = data.GetCard(data.last_attack);
                if (target != null && AreCriteriaTargetConditionsMet(data, caster, target))
                    candidate_targets.Add(target);
            }

            if (criteria_target == AbilityTarget.LastAttacked)
            {
                Card target = data.GetCard(data.last_attacked);
                if (target != null && AreCriteriaTargetConditionsMet(data, caster, target))
                    candidate_targets.Add(target);
            }

            if (criteria_target == AbilityTarget.LastSummoned)
            {
                Card target = data.GetCard(data.last_summoned);
                if (target != null && AreCriteriaTargetConditionsMet(data, caster, target))
                    candidate_targets.Add(target);
            }

            if (criteria_target == AbilityTarget.AbilityTriggerer)
            {
                Card target = data.GetCard(data.ability_triggerer);
                if (target != null && AreCriteriaTargetConditionsMet(data, caster, target))
                    candidate_targets.Add(target);
            }

            if (criteria_target == AbilityTarget.EquippedCard)
            {
                if (caster.CardData.IsEquipment())
                {
                    //Get bearer of the equipment
                    Player player = data.GetPlayer(caster.player_id);
                    Card target = player.GetBearerCard(caster);
                    if (target != null && AreCriteriaTargetConditionsMet(data, caster, target))
                        candidate_targets.Add(target);
                }
                else if(caster.equipped_uid != null)
                {
                    //Get equipped card
                    Card target = data.GetCard(caster.equipped_uid);
                    if (target != null && AreCriteriaTargetConditionsMet(data, caster, target))
                        candidate_targets.Add(target);
                }
            }

            List<Card> targets = new List<Card>();
            List<Slot> all_slots = Slot.GetAll();
            

            foreach (Card candidate in candidate_targets)
            {
                if (data.IsOnBoard(candidate))
                {
                    foreach (Slot s in all_slots)
                    {
                        if (AreWideRangeConditionsMet(data, caster, candidate.slot, s) && data.GetSlotCard(s) != null)
                        {
                            targets.Add(data.GetSlotCard(s));
                        }
                    }

                }
                else
                {
                    targets.Add(candidate);
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

            return targets;
        }

        //Return player targets,  memory_array is used for optimization and avoid allocating new memory
        public List<Player> GetPlayerTargets(Game data, Card caster, ListSwap<Player> memory_array = null)
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

            //Filter targets
            if (filters_target != null && targets.Count > 0)
            {
                foreach (FilterData filter in filters_target)
                {
                    if (filter != null)
                        targets = filter.FilterTargets(data, this, caster, targets, memory_array.GetOther(targets));
                }
            }

            return targets;
        }

        //Return slot targets,  memory_array is used for optimization and avoid allocating new memory
        public List<Slot> GetSlotTargets(Game data, Card caster, ListSwap<Slot> memory_array = null)
        {
            if (memory_array == null)
                memory_array = new ListSwap<Slot>(); //Slow operation

            List<Slot> candiidate_targets = memory_array.Get();

            if (criteria_target == AbilityTarget.AllSlots)
            {
                List<Slot> slots = Slot.GetAll();
                foreach (Slot slot in slots)
                {
                    if (AreCriteriaTargetConditionsMet(data, caster, slot))
                        candiidate_targets.Add(slot);
                }
            }

            if (criteria_target == AbilityTarget.WideAreaSlot)
            {
                List<Slot> slots = Slot.GetAll();
                foreach (Slot slot in slots)
                {
                    if (AreCriteriaTargetConditionsMet(data, caster, slot))
                        candiidate_targets.Add(slot);
                }
            }

            if (criteria_target == AbilityTarget.AttachedSlot)
            {
                Slot slot = caster.slot;

                if (AreCriteriaTargetConditionsMet(data, caster, slot))
                    candiidate_targets.Add(slot);
            }

            if (criteria_target == AbilityTarget.LastAttackSlot)
            {
                Slot slot = data.last_attack_slot;

                if (AreCriteriaTargetConditionsMet(data, caster, slot))
                    candiidate_targets.Add(slot);
            }

            if (criteria_target == AbilityTarget.LastAttackedSlot)
            {
                Slot slot = data.last_attacked_slot;

                if (AreCriteriaTargetConditionsMet(data, caster, slot))
                    candiidate_targets.Add(slot);
            }

            if (criteria_target == AbilityTarget.LastSummonedSlot)
            {
                Slot slot = data.last_summoned_slot;

                if (!AreCriteriaTargetConditionsMet(data, caster, slot))
                    return candiidate_targets;
            }

            if (criteria_target == AbilityTarget.LastDestroyedSlot)
            {
                Slot slot = data.last_destroyed_slot;

                if (AreCriteriaTargetConditionsMet(data, caster, slot))
                    candiidate_targets.Add(slot);
            }

            if (criteria_target == AbilityTarget.LastTargetedSlot)
            {
                Slot slot = data.last_targeted_slot;

                if (AreCriteriaTargetConditionsMet(data, caster, slot))
                    candiidate_targets.Add(slot);
            }

            List<Slot> targets = new List<Slot>();
            List<Slot> all_slots = Slot.GetAll();

            //WideAreaRange targets
            if (candiidate_targets.Count > 0)
            {
                foreach (Slot s in candiidate_targets)
                {
                    foreach (Slot a in all_slots)
                    {
                        if (AreWideRangeConditionsMet(data, caster, s, a))
                        {
                            targets.Add(a);
                        }
                    }
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


            //return targets;
            return targets;
        }

        public List<CardData> GetCardDataTargets(Game data, Card caster, ListSwap<CardData> memory_array = null)
        {
            if (memory_array == null)
                memory_array = new ListSwap<CardData>(); //Slow operation

            List<CardData> targets = memory_array.Get();

            if (criteria_target == AbilityTarget.AllCardData)
            {
                foreach (CardData card in CardData.GetAll())
                {
                    if (AreCriteriaTargetConditionsMet(data, caster, card))
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

            return targets;
        }

        // Check if there is any valid target, if not, AI wont try to cast activated ability
        public bool HasValidSelectTarget(Game game_data, Card caster)
        {
            if (criteria_target == AbilityTarget.SelectTarget)
            {
                if (HasValidBoardCardTarget(game_data, caster))
                    return true;
                if (HasValidPlayerTarget(game_data, caster))
                    return true;
                if (HasValidSlotTarget(game_data, caster))
                    return true;
                return false;
            }

            if (criteria_target == AbilityTarget.SelectCard)
            {
                if (HasValidBoardCardTarget(game_data, caster))
                    return true;
                return false;
            }

            if (criteria_target == AbilityTarget.SelectSlot)
            {
                if (HasValidSlotTarget(game_data, caster))
                    return true;
                return false;
            }

            if (criteria_target == AbilityTarget.CardSelector)
            {
                if (HasValidCardTarget(game_data, caster))
                    return true;
                return false;
            }

            if (criteria_target == AbilityTarget.ChoiceSelector)
            {
                foreach (AbilityData choice in chain_abilities)
                {
                    if(choice.AreTriggerConditionsMet(game_data, caster))
                        return true;
                }
                return false;
            }

            return true; //Not selecting, valid
        }

        public bool HasValidBoardCardTarget(Game game_data, Card caster)
        {
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                for (int c = 0; c < player.cards_board.Count; c++)
                {
                    Card card = player.cards_board[c];
                    if (CanTarget(game_data, caster, card))
                        return true;
                }
            }
            return false;
        }

        public bool HasValidCardTarget(Game game_data, Card caster)
        {
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                bool v1 = HasValidCardTarget(game_data, caster, player.cards_club);
                bool v2 = HasValidCardTarget(game_data, caster, player.cards_deck);
                bool v3 = HasValidCardTarget(game_data, caster, player.cards_discard);
                bool v4 = HasValidCardTarget(game_data, caster, player.cards_hand);
                bool v5 = HasValidCardTarget(game_data, caster, player.cards_board);
                bool v6 = HasValidCardTarget(game_data, caster, player.cards_equip);
                bool v7 = HasValidCardTarget(game_data, caster, player.cards_attach);
                bool v8 = HasValidCardTarget(game_data, caster, player.cards_secret);
                bool v9 = HasValidCardTarget(game_data, caster, player.cards_temp);
                bool v10 = HasValidCardTarget(game_data, caster, player.player_ability);
                if (v1 || v2 || v3 || v4 || v5 || v6 || v7 || v8 || v9 || v10)
                    return true;
            }
            return false;
        }

        public bool HasValidCardTarget(Game game_data, Card caster, List<Card> list)
        {
            for (int c = 0; c < list.Count; c++)
            {
                Card card = list[c];
                if (AreCriteriaTargetConditionsMet(game_data, caster, card))
                    return true;
            }
            return false;
        }

        public bool HasValidPlayerTarget(Game game_data, Card caster)
        {
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                if (CanTarget(game_data, caster, player))
                    return true;
            }
            return false;
        }

        public bool HasValidSlotTarget(Game game_data, Card caster)
        {
            foreach (Slot slot in Slot.GetAll())
            {
                if (CanTarget(game_data, caster, slot))
                    return true;
            }
            return false;
        }

        public bool IsSelector()
        {
            return criteria_target == AbilityTarget.SelectTarget || criteria_target == AbilityTarget.SelectCard || criteria_target == AbilityTarget.SelectSlot || criteria_target == AbilityTarget.CardSelector || criteria_target == AbilityTarget.ChoiceSelector;
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

        OnHeal = 50,
        OnHealOther = 51,
    }

    public enum AbilityTarget
    {
        None = 0,
        Self = 1,

        PlayerSelf = 4,
        PlayerOpponent = 5,
        AllPlayers = 7,

        AllCardsBoard = 10,
        AllCardsHand = 11,
        AllCardsAllPiles = 12,
        AllSlots = 15,
        AllCardData = 17,       //For card Create effects only


        PlayTarget = 20,        //The target selected at the same time the spell was played (spell only)      
        AbilityTriggerer = 25,   //The card that triggered the trap
        EquippedCard = 27,       //If equipment, the bearer, if citizen, the item equipped
        AttachedSlot = 28,

        SelectTarget = 30,        //Select a card, player or slot on board
        SelectCard = 31,
        SelectSlot = 32,
        WideAreaCard = 33,
        WideAreaSlot = 34,
        
        CardSelector = 40,          //Card selector menu
        ChoiceSelector = 50,        //Choice selector menu

        LastPlayed = 70,            //Last card that was played
        LastAttack = 71,
        LastAttackSlot = 72, 
        LastAttacked = 73,
        LastAttackedSlot = 74,
        LastTargeted = 75,          //Last card that was targeted with an ability
        LastTargetedSlot = 76,
        LastDestroyed = 77,            //Last card that was killed
        LastDestroyedSlot = 78,
        LastSummoned = 79,            //Last card that was summoned or created
        LastSummonedSlot = 80,

    }

}
