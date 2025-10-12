using System.Collections;
using System.Collections.Generic;
using TcgEngine.UI;
using UnityEngine;

namespace TcgEngine.Client
{

    public class Tutorial : MonoBehaviour
    {

        private bool is_tuto = false;
        private TutoStepGroup current_group;
        private TutoStep current_step;
        private bool locked = false;

        private static Tutorial instance;

        void Awake()
        {
            instance = this;
        }

        void Start()
        {
            if (GameClient.game_settings.game_type == GameType.Adventure)
            {
                LevelData level = GameClient.game_settings.GetLevel();
                if (level.tuto_prefab != null)
                {
                    is_tuto = true;

                    GameObject tuto_obj = Instantiate(level.tuto_prefab);
                    tuto_obj.GetComponent<Canvas>().worldCamera = GameCamera.GetCamera();
                    tuto_obj.GetComponent<Canvas>().sortingLayerName = "TopUI";
                    tuto_obj.GetComponent<Canvas>().sortingOrder = -1;

                    GameClient.Get().onGameStart += OnGameStart;
                    GameClient.Get().onNewTurn += OnNewTurn;
                    GameClient.Get().onCardPlayed += OnCardPlayed;
                    GameClient.Get().onAttackStart += OnAttack;
                    GameClient.Get().onAttackPlayerStart += OnAttackPlayer;
                    GameClient.Get().onAbilityStart += OnCastAbility;
                    GameClient.Get().onAbilityTargetCard += OnTargetCard;
                    GameClient.Get().onAbilityTargetSlot += OnTargetSlot;
                    GameClient.Get().onAbilityTargetPlayer += OnTargetPlayer;

                    MulliganSelector.Get().onMulliganSelect += OnMulliganSelect;
                    MulliganSelector.Get().onMulliganConfirm += OnMulliganConfirm;
                }

                HideAll();
            }
        }

        private void OnGameStart()
        {
            Game data = GameClient.Get().GetGameData();
            LevelData level = GameClient.game_settings.GetLevel();

            if (data == null)
                return;

            if (!level.mulligan)
                return;

            EndGroup();

            TutoStepGroup group = TutoStepGroup.Get(TutoStartTrigger.StartTurn, data.turn_count);
            ShowGroup(group);
        }

        private void OnNewTurn(int player_id)
        {
            Game data = GameClient.Get().GetGameData();

            if (data == null)
                return;

            EndGroup();

            /*
            if (player_id != GameClient.Get().GetPlayerID())
            {
                return;
            }
            */

            TutoStepGroup group = TutoStepGroup.Get(TutoStartTrigger.StartTurn, data.turn_count);
            Debug.Log(group.gameObject.name);
            ShowGroup(group);
        }

        private void OnCardPlayed(Card card, Slot slot)
        {
            Game data = GameClient.Get().GetGameData();

            if (card.player_id == GameClient.Get().GetPlayerID())
            {
                if (current_step != null && current_step.trigger_target == card.CardData)
                TriggerEndStep(TutoEndTrigger.PlayCard);
                TriggerStartGroup(TutoStartTrigger.PlayCard, card);
            }
        }

        private void OnAttack(Card card, Card target)
        {
            Game data = GameClient.Get().GetGameData();
            if (card.player_id == GameClient.Get().GetPlayerID())
            {
                TriggerEndStep(TutoEndTrigger.Attack, 2f);
                TriggerStartGroup(TutoStartTrigger.Attack, card);
                TriggerStartGroup(TutoStartTrigger.Attack, target);
            }
        }

        private void OnAttackPlayer(Card card, Player target)
        {
            Game data = GameClient.Get().GetGameData();
            if (card.player_id == GameClient.Get().GetPlayerID())
            {

                TriggerEndStep(TutoEndTrigger.AttackPlayer, 2f);
                TriggerStartGroup(TutoStartTrigger.Attack, card);
            }
        }

        private void OnCastAbility(AbilityData ability, Card card)
        {
            Game data = GameClient.Get().GetGameData();
            if (card.player_id == GameClient.Get().GetPlayerID())
            {
                TriggerEndStep(TutoEndTrigger.CastAbility);
                TriggerStartGroup(TutoStartTrigger.CastAbility, card);
            }
        }

        private void OnTargetCard(AbilityData ability, Card card, Card target)
        {
            Game data = GameClient.Get().GetGameData();
            if (card.player_id == GameClient.Get().GetPlayerID())
            {
                TriggerEndStep(TutoEndTrigger.SelectTarget);
            }
        }

        private void OnTargetSlot(AbilityData ability, Card card, Slot target)
        {
            Game data = GameClient.Get().GetGameData();
            Slot target_slot = new Slot(current_step.target_slot);
            if (card.player_id == GameClient.Get().GetPlayerID() && target_slot == target)
            {
                TriggerEndStep(TutoEndTrigger.SelectTarget);
            }
        }

        private void OnTargetPlayer(AbilityData ability, Card card, Player target)
        {
            Game data = GameClient.Get().GetGameData();
            if (card.player_id == GameClient.Get().GetPlayerID())
            {
                TriggerEndStep(TutoEndTrigger.SelectTarget);
            }
        }

        private void OnMulliganSelect(Card card)
        {
            Game data = GameClient.Get().GetGameData();
            if (card.player_id == GameClient.Get().GetPlayerID())
            {
                TriggerEndStep(TutoEndTrigger.MulliganSelect);
            }
        }

        private void OnMulliganConfirm()
        {
            Game data = GameClient.Get().GetGameData();
            
            TriggerEndStep(TutoEndTrigger.MulliganConfirm);
        }

        public void TriggerEndStep(TutoEndTrigger trigger, float time = 1f)
        {
            if (current_step != null && current_step.end_trigger == trigger)
            {
                Hide();
                TutoStepGroup group = current_group;
                locked = true;
                TimeTool.WaitFor(time, () =>
                {
                    locked = false;
                    if (group == current_group)
                    {
                        ShowNext();
                    }
                });
            }
        }

        public void TriggerStartGroup(TutoStartTrigger trigger, Card card)
        {
            if (current_group == null || !current_group.forced)
            {
                if (current_step == null || !current_step.forced)
                {
                    CardData target = card != null ? card.CardData : null;
                    ShowGroup(trigger, target);
                }
            }
        }

        public void ShowGroup(TutoStartTrigger trigger, CardData target)
        {
            Game data = GameClient.Get().GetGameData();
            TutoStepGroup group = TutoStepGroup.Get(trigger, target, data.turn_count);
            ShowGroup(group);
        }

        public void ShowGroup(TutoStepGroup group)
        {
            if (group != null)
            {
                current_group = group;
                group.SetTriggered();
                TutoStep step = TutoStep.Get(group, 0);
                Show(step);
            }
        }

        public void ShowNext()
        {
            if (current_group != null)
            {
                int index = GetNextIndex();
                TutoStep step = TutoStep.Get(current_group, index);
                if (step != null)
                    Show(step);
                else
                    EndGroup();
            }
        }

        public void Show(TutoStep step)
        {
            HideAll();
            current_step = step;
            if (step != null)
                step.Show();
        }

        public void EndGroup()
        {
            HideAll();
            current_group = null;
            current_step = null;
        }

        public void Hide(TutoStep step)
        {
            if (step != null)
                step.Hide();
        }

        public void Hide()
        {
            Hide(current_step);
        }

        public bool CanDo(TutoEndTrigger trigger)
        {
            return CanDo(trigger, null);
        }

        public bool CanDo(TutoEndTrigger trigger, Slot target)
        {
            Game data = GameClient.Get().GetGameData();

            if (!is_tuto)
                return true; //Not a tutorial

            if (locked)
                return false;

            if (current_step != null && current_step.forced)
            {
                if (trigger == TutoEndTrigger.CastAbility && current_step.end_trigger == TutoEndTrigger.SelectTarget)
                    return true; //Dont get locked into select target if ability was canceled

                if (current_step.end_trigger != trigger)
                    return false; //Wrong trigger

                Slot target_slot = new Slot(current_step.target_slot);
                if (target_slot.IsValid() && target_slot != target)
                    return false; //Wrong target
            }

            return true;
        }

        public bool CanDo(TutoEndTrigger trigger, Card target)
        {
            if (!is_tuto)
                return true; //Not a tutorial

            if (locked)
                return false;

            if (current_step != null && current_step.forced)
            {
                if (trigger == TutoEndTrigger.CastAbility && current_step.end_trigger == TutoEndTrigger.SelectTarget)
                    return true; //Dont get locked into select target if ability was canceled

                if (current_step.end_trigger != trigger)
                    return false; //Wrong trigger

                CardData target_data = target != null ? target.CardData : null;
                if (current_step.trigger_target != null && current_step.trigger_target != target_data)
                    return false; //Wrong target
            }

            return true;
        }

        public bool CanDo(TutoEndTrigger trigger, Card target_card, Slot target_slot)
        {
            if (!is_tuto)
                return true; //Not a tutorial

            if (locked)
                return false;

            if (current_step != null && current_step.forced)
            {
                if (trigger == TutoEndTrigger.CastAbility && current_step.end_trigger == TutoEndTrigger.SelectTarget)
                    return true; //Dont get locked into select target if ability was canceled

                if (current_step.end_trigger != trigger)
                    return false; //Wrong trigger

                CardData target_data = target_card != null ? target_card.CardData : null;
                Slot forced_slot = new Slot(current_step.target_slot);

                if (current_step.trigger_target != null && current_step.trigger_target != target_data)
                    return false;

                if (forced_slot.IsValid() && forced_slot != target_slot)
                    return false; //Wrong target

            }

            return true;
        }

        public int GetNextIndex()
        {
            if (current_step != null)
                return current_step.GetStepIndex() + 1;
            return 0;
        }

        public TutoStepGroup GetCurrentGroup()
        {
            return current_group;
        }

        public bool IsTuto()
        {
            return is_tuto;
        }

        public TutoEndTrigger GetEndTrigger()
        {
            if (current_step != null)
                return current_step.end_trigger;
            return TutoEndTrigger.Click;
        }

        public void HideAll()
        {
            TutoStep.HideAll();
        }

        public static Tutorial Get()
        {
            return instance;
        }
    }

    public enum TutoStartTrigger
    {
        StartTurn = 0,
        PlayCard = 10,
        Attack = 20,
        CastAbility = 30,
    }

    public enum TutoEndTrigger
    {
        Click = 0,
        EndTurn = 5,
        PlayCard = 10,
        Move = 15,
        Attack = 20,
        AttackPlayer = 25,
        CastAbility = 30,
        SelectTarget = 35,
        CancelAbility = 40,
        MulliganSelect = 45,
        MulliganConfirm = 50,
    }
}
