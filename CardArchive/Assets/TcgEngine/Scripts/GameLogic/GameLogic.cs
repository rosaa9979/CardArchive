using System;
using System.Collections.Generic;
using System.Linq;
using TcgEngine.Client;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Profiling;

namespace TcgEngine.Gameplay
{
    /// <summary>
    /// Execute and resolves game rules and logic
    /// </summary>

    public class GameLogic
    {
        public UnityAction onGameStart;
        public UnityAction<Player> onGameEnd;          //Winner

        public UnityAction onTurnStart;
        public UnityAction onTurnPlay;
        public UnityAction onTurnEnd;

        public UnityAction<Card, Slot> onCardPlayed;      
        public UnityAction<Card, Slot> onCardSummoned;
        public UnityAction<Card, Slot> onCardMoved;
        public UnityAction<Card> onCardTransformed;
        public UnityAction<Card> onCardDiscarded;
        public UnityAction<Card, int> onCardDissolved;
        public UnityAction<int> onCardDrawn;
        public UnityAction<int> onRollValue;

        public UnityAction<AbilityData, Card> onAbilityStart;        
        public UnityAction<AbilityData, Card, Card> onAbilityTargetCard;  //Ability, Caster, Target
        public UnityAction<AbilityData, Card, Player> onAbilityTargetPlayer;
        public UnityAction<AbilityData, Card, Slot> onAbilityTargetSlot;
        public UnityAction<AbilityData, Card> onAbilityEnd;

        public UnityAction<Card, Card> onAttackStart;  //Attacker, Defender
        public UnityAction<Card, Card> onAttackEnd;     //Attacker, Defender
        public UnityAction<Card, Card> onAttackHit;
        public UnityAction<Card, Player> onAttackPlayerStart;
        public UnityAction<Card, Player> onAttackPlayerEnd;
        public UnityAction<Card, Player> onAttackPlayerHit;

        public UnityAction<Card, Card> onSecretTrigger;    //Secret, Triggerer
        public UnityAction<Card, Card> onSecretResolve;    //Secret, Triggerer

        public UnityAction onRefresh;

        private Game game_data;

        private ResolveQueue resolve_queue;
        private bool is_ai_predict = false;
        
        private System.Random random = new System.Random();

        private ListSwap<Card> card_array = new ListSwap<Card>();
        private ListSwap<Player> player_array = new ListSwap<Player>();
        private ListSwap<Slot> slot_array = new ListSwap<Slot>();
        private ListSwap<CardData> card_data_array = new ListSwap<CardData>();
        private List<Card> cards_to_clear = new List<Card>();

        private Queue<Card> attack_list = new Queue<Card>();
        private Slot additional_slot = new Slot(0, 0, -1);

        public GameLogic(bool is_ai)
        {
            //is_instant ignores all gameplay delays and process everything immediately, needed for AI prediction
            resolve_queue = new ResolveQueue(null, is_ai);
            is_ai_predict = is_ai;
        }

        public GameLogic(Game game)
        {
            game_data = game;
            resolve_queue = new ResolveQueue(game, false);
        }

        public virtual void SetData(Game game)
        {
            game_data = game;
            resolve_queue.SetData(game);
        }

        public virtual void Update(float delta)
        {
            resolve_queue.Update(delta);
        }

        //----- Turn Phases ----------

        public virtual void StartGame()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            //Choose first player
            game_data.state = GameState.Play;
            game_data.first_player = random.NextDouble() < 0.5 ? 0 : 1;
            game_data.current_player = game_data.first_player;
            game_data.turn_count = 1;

            bool should_mulligan = GameplayData.Get().mulligan;

            //Adventure settings
            LevelData level = game_data.settings.GetLevel();
            if (level != null)
            {
                if (level != null && level.first_player == LevelFirst.Player)
                    game_data.first_player = 0;
                if (level != null && level.first_player == LevelFirst.AI)
                    game_data.first_player = 1;
                game_data.current_player = game_data.first_player;
                should_mulligan = level.mulligan;
            }

            //Init each players
            foreach (Player player in game_data.players)
            {
                //Puzzle level deck
                DeckPuzzleData pdeck = DeckPuzzleData.Get(player.deck);

                //Hp / mana
                player.hp_max = pdeck != null ? pdeck.start_hp : GameplayData.Get().hp_start;
                player.hp = player.hp_max;
                player.mana_max = pdeck != null ? pdeck.start_mana : GameplayData.Get().mana_start;
                player.mana = player.mana_max;

                //Draw starting cards
                int dcards = pdeck != null ? pdeck.start_cards : GameplayData.Get().cards_start;
                if (!Tutorial.Get().IsTuto())
                    dcards = player.player_id == game_data.first_player ? dcards - 1 : dcards;
                DrawCard(player, dcards);

                
                //Add coin second player
                bool is_random = level == null || level.first_player == LevelFirst.Random;
                if (is_random && player.player_id != game_data.first_player && GameplayData.Get().second_bonus != null)
                {
                    Card card = Card.Create(GameplayData.Get().second_bonus, VariantData.GetDefault(), player);
                    player.cards_hand.Add(card);
                }
            }

            //Start state
            RefreshData();
            onGameStart?.Invoke();

            if (should_mulligan)
                GoToMulligan();
            else
                StartTurn();

        }
		
        public virtual void StartTurn()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            ClearTurnData();
            game_data.phase = GamePhase.StartTurn;
            onTurnStart?.Invoke();
            RefreshData();

            Player player = game_data.GetActivePlayer();

            //Cards draw
            if (game_data.turn_count > 1 || player.player_id != game_data.first_player)
            {
                DrawCard(player, GameplayData.Get().cards_per_turn);
            }

            //Mana 
            player.mana_max += GameplayData.Get().mana_per_turn;
            player.mana_max = Mathf.Min(player.mana_max, GameplayData.Get().mana_max);
            player.mana = player.mana_max;

            //Turn timer and history
            game_data.turn_timer = GameplayData.Get().turn_duration;
            player.history_list.Clear();

            //Player poison
            if (player.HasStatus(StatusType.Poisoned))
                player.hp -= player.GetStatusValue(StatusType.Poisoned);

            if (player.hero != null)
                player.hero.Refresh();

            //Refresh Cards and Status Effects
            for (int i = player.cards_board.Count - 1; i >= 0; i--)
            {
                Card card = player.cards_board[i];

                if(!card.HasStatus(StatusType.Sleep))
                    card.Refresh();

                if (card.HasStatus(StatusType.Poisoned))
                    DamageCard_Event(card, card.GetStatusValue(StatusType.Poisoned));
            }



            //StartTurn Abilities
            foreach (Player p in game_data.players)
                TriggerPlayerCardsAbilityType(p, AbilityTrigger.StartOfTurn);

            TriggerPlayerSecrets(player, AbilityTrigger.StartOfTurn);

            resolve_queue.ResolveAll(0.2f);

            UpdateOngoing();

            resolve_queue.AddCallback(StartMainPhase);
            resolve_queue.ResolveAll(0.2f);
        }

        public virtual void StartNextTurn()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            game_data.current_player = (game_data.current_player + 1) % game_data.settings.nb_players;

            if (game_data.current_player == game_data.first_player)
                game_data.turn_count++;

            RefreshData();
            CheckForWinner();
            StartTurn();
        }

        public virtual void StartMainPhase()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            game_data.phase = GamePhase.Main;
            onTurnPlay?.Invoke();
            RefreshData();
        }
        
        public virtual void StartAttackPhase()
        {
            if (game_data.state == GameState.GameEnded)
                return;
            if (game_data.phase != GamePhase.Main)
                return;

            game_data.selector = SelectorType.None;
            game_data.phase = GamePhase.Attack;
            
            resolve_queue.AddCallback(AttackCheck);
            resolve_queue.ResolveAll(0.2f);
        }

        public virtual void AttackCheck()
        {
            if (game_data.state == GameState.GameEnded)
                return;
            if (game_data.phase != GamePhase.Attack)
                return;

            Player player = game_data.GetActivePlayer();
            bool can_attack = false;

            attack_list.Clear(); 

            List<Slot> attack_order = Slot.GetAttackOrder(player.player_id);

            // List B의 Slot을 key로, List A의 Monster를 value로 하는 Dictionary 생성
            var citizensBySlot = player.cards_board.ToDictionary(citizen => citizen.slot, citizen => citizen);

            // List B의 순서대로 Monster를 추출
            var orderedCitizens = attack_order.Select(slot =>
            {
                if (citizensBySlot.TryGetValue(slot, out var citizen))
                {
                    return citizen;
                }
                else
                {
                    return null; // 또는 다른 기본값
                }
            }).ToList();

            // orderedMonsters를 새로운 List에 복사
            List<Card> reorderedCitizens = orderedCitizens.ToList();

            // reorderedMonsters가 재배열된 결과

            foreach (Card attacker in reorderedCitizens)
            { 
                if (attacker != null && attacker.CanAttack())
                {
                    can_attack = true;
                    //resolve_queue.AddAttack(attacker, AttackSearch);
                    //resolve_queue.ResolveAll(0.2f);
                    AttackSearch(attacker);
                    break;
                }
            }

            if (!can_attack)
            {
                resolve_queue.AddCallback(EndTurn);
                resolve_queue.ResolveAll(0.2f);
            }

        }

        public virtual void AttackSearch(Card attacker, bool skip_cost = false)
        {
            Player player = game_data.GetPlayer(attacker.player_id);
            Player oplayer = game_data.GetOpponentPlayer(player.player_id);

            Dictionary<int, List<Slot>> range_slot = attacker.slot.GetRangeSlot(attacker.GetRange());
            List<Card> candidate_target = new List<Card>();

            candidate_target = attacker.weapon.SearchTarget(this, attacker);

            if (attacker.HasClub(ClubData.Get("Trinity_Vigilante_Crew")))
            {
                List<Slot> rslot = range_slot.Values.SelectMany(list => list).ToList();
                List<Slot> pslot = Slot.GetInsideSlot(oplayer.player_id);

                if (rslot.Any(element => pslot.Contains(element)))
                {
                    attacker.weapon.AttackTarget(this, attacker, oplayer);
                }

                else if (candidate_target.Count != 0)
                    attacker.weapon.AttackTarget(this, attacker, candidate_target);

                else
                    ExhaustBattle(attacker);
            }

            else
            {
                if (candidate_target.Count != 0)
                    attacker.weapon.AttackTarget(this, attacker, candidate_target);

                else
                {
                    List<Slot> rslot = range_slot.Values.SelectMany(list => list).ToList();
                    List<Slot> pslot = Slot.GetInsideSlot(oplayer.player_id);

                    if (rslot.Any(element => pslot.Contains(element)))
                    {
                        attacker.weapon.AttackTarget(this, attacker, oplayer);
                    }

                    else
                    {
                        ExhaustBattle(attacker);
                    }
                }
            }

            //ExhaustBattle(attacker);

            resolve_queue.AddCallback(AttackCheck);
            resolve_queue.ResolveAll(0.5f);
        }

        public virtual Dictionary<int, List<Card>> GetAllTarget(Card attacker)
        {
            Dictionary<int, List<Slot>> range_slot = attacker.slot.GetRangeSlot(attacker.GetRange());
            Dictionary<int, List<Card>> targets = new Dictionary<int, List<Card>>();

            // rangeSlots 딕셔너리를 순회
            foreach (var dis in range_slot.Keys)
            {
                // 해당 거리에서 유닛이 있는 슬롯들만 필터링하고 유닛(Card)을 리스트로 변환
                var cards = range_slot[dis]
                    .Select(slot => game_data.GetSlotCard(slot))   // 슬롯에서 카드 가져오기
                    .Where(card => card != null)                   // null이 아닌 카드만 필터링
                    .ToList();                                     // 결과를 List<Card>로 변환

                // 유닛(Card) 리스트가 비어 있지 않다면 Dictionary에 추가
                if (cards.Any())  // cards 리스트가 비어 있지 않으면
                    targets[dis] = cards;
            } 

            return targets;
        }

        public virtual Dictionary<int, List<Card>> GetAllEnemyTarget(Card attacker)
        {
            Dictionary<int, List<Slot>> range_slot = attacker.slot.GetRangeSlot(attacker.GetRange());
            Dictionary<int, List<Card>> targets = new Dictionary<int, List<Card>>();

            // rangeSlots 딕셔너리를 순회
            foreach (var dis in range_slot.Keys)
            {
                // 해당 거리에서 유닛이 있는 슬롯들만 필터링하고 유닛(Card)을 리스트로 변환
                var cards = range_slot[dis]
                    .Select(slot => game_data.GetSlotCard(slot))   // 슬롯에서 카드 가져오기
                    .Where(card => card != null && card.player_id != attacker.player_id)                   // null이 아닌 카드만 필터링
                    .ToList();                                     // 결과를 List<Card>로 변환

                // 유닛(Card) 리스트가 비어 있지 않다면 Dictionary에 추가
                if (cards.Any())  // cards 리스트가 비어 있지 않으면
                    targets[dis] = cards;
            } 

            return targets;
        }

        public virtual void EndTurn()
        {
            if (game_data.state == GameState.GameEnded)
                return;
            if (game_data.phase != GamePhase.Attack)
                return;

            game_data.selector = SelectorType.None;
            game_data.phase = GamePhase.EndTurn;

            //Reduce status effects with duration
            foreach (Player aplayer in game_data.players)
            {
                foreach (Card card in aplayer.cards_board)
                    card.ReduceStatusDurations();
                foreach (Card card in aplayer.cards_equip)
                    card.ReduceStatusDurations();
            }

            //End of turn abilities
            Player player = game_data.GetActivePlayer();
            TriggerPlayerCardsAbilityType(player, AbilityTrigger.EndOfTurn);

            onTurnEnd?.Invoke();
            RefreshData();

            resolve_queue.AddCallback(StartNextTurn);
            resolve_queue.ResolveAll(0.2f);
        }

        //End game with winner
        public virtual void EndGame(int winner)
        {
            if (game_data.state != GameState.GameEnded)
            {
                game_data.state = GameState.GameEnded;
                game_data.phase = GamePhase.None;
                game_data.selector = SelectorType.None;
                game_data.current_player = winner; //Winner player
                resolve_queue.Clear();
                Player player = game_data.GetPlayer(winner);
                onGameEnd?.Invoke(player);
                RefreshData();
            }
        }

        //Progress to the next step/phase 
        public virtual void NextStep()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            CancelSelection();

            //Add to resolve queue in case its still resolving
            resolve_queue.AddCallback(StartAttackPhase);
            resolve_queue.ResolveAll();
        }

        //Check if a player is winning the game, if so end the game
        //Change or edit this function for a new win condition
        protected virtual void CheckForWinner()
        {
            int count_alive = 0;
            Player alive = null;
            foreach (Player player in game_data.players)
            {
                if (!player.IsDead())
                {
                    alive = player;
                    count_alive++;
                }
            }

            if (count_alive == 0)
            {
                EndGame(-1); //Everyone is dead, Draw
            }
            else if (count_alive == 1)
            {
                EndGame(alive.player_id); //Player win
            }
        }

        protected virtual void ClearTurnData()
        {
            game_data.selector = SelectorType.None;
            resolve_queue.Clear();
            card_array.Clear();
            player_array.Clear();
            slot_array.Clear();
            card_data_array.Clear();
            game_data.last_played = null;
            game_data.last_destroyed = null;
            game_data.last_destroyed_slot = Slot.None;
            game_data.last_target = null;
            game_data.last_targeted_slot = Slot.None;
            game_data.last_attack = null;
            game_data.last_attack_slot = Slot.None;
            game_data.last_attacked = null;
            game_data.last_attacked_slot = Slot.None;
            game_data.last_player_attacked = false;
            game_data.last_summoned = null;
            game_data.last_summoned_slot = Slot.None;
            game_data.ability_triggerer = null;
            game_data.ability_played.Clear();
            game_data.cards_attacked.Clear();      
        }

        //--- Setup ------

        //Set deck using a Deck in Resources
        public virtual void SetPlayerDeck(Player player, DeckData deck)
        {
            player.cards_all.Clear();
            player.cards_deck.Clear();
            player.deck = deck.id;
            player.hero = null;

            VariantData variant = VariantData.GetDefault();
            if (deck.hero != null)
            {
                player.hero = Card.Create(deck.hero, variant, player);
            }

            if (deck.clubs.Length > 0)
            {
                foreach (CardData club in deck.clubs)
                    player.cards_club.Add(Card.Create(club, variant, player));
            }

            foreach (CardData card in deck.cards)
            {
                if (card != null)
                {
                    Card acard = Card.Create(card, variant, player);
                    player.cards_deck.Add(acard);
                }
            }

            DeckPuzzleData puzzle = deck as DeckPuzzleData;

            //Board cards
            if (puzzle != null)
            {
                foreach (DeckCardSlot card in puzzle.board_cards)
                {
                    Card acard = Card.Create(card.card, variant, player);
                    acard.slot = new Slot(card.slot, Slot.GetP(player.player_id));
                    player.cards_board.Add(acard);
                }
            }

            //Shuffle deck
            if(puzzle == null || !puzzle.dont_shuffle_deck)
                ShuffleDeck(player.cards_deck);
        }

        //Set deck using custom deck in save file or database
        public virtual void SetPlayerDeck(Player player, UserDeckData deck)
        {
            player.cards_all.Clear();
            player.cards_deck.Clear();
            player.deck = deck.tid;
            player.hero = null;

            if (deck.hero != null)
            {
                CardData hdata = CardData.Get(deck.hero.tid);
                VariantData hvariant = VariantData.Get(deck.hero.variant);
                if(hdata != null && hvariant != null)
                    player.hero = Card.Create(hdata, hvariant, player);
            }

            foreach (UserCardData club in deck.clubs)
            {
                CardData iclub = CardData.Get(club.tid);
                VariantData variant = VariantData.GetDefault();

                if (iclub != null && variant != null)
                    player.cards_club.Add(Card.Create(iclub, variant, player));
            }


            foreach (UserCardData card in deck.cards)
            {
                CardData icard = CardData.Get(card.tid);
                VariantData variant = VariantData.Get(card.variant);
                if (icard != null && variant != null)
                {
                    for (int i = 0; i < card.quantity; i++)
                    {
                        Card acard = Card.Create(icard, variant, player);
                        player.cards_deck.Add(acard);
                    }
                }
            }

            //Shuffle deck
            ShuffleDeck(player.cards_deck);
        }

        //---- Gameplay Actions --------------

        public virtual void PlayCard(Card card, Slot slot, bool skip_cost = false)
        {
            if (game_data.CanPlayCard(card, slot, skip_cost))
            {
                Player player = game_data.GetPlayer(card.player_id);

                //Cost
                if (!skip_cost)
                    player.PayMana(card);

                //Play card
                player.RemoveCardFromAllGroups(card);

                //Add to board
                CardData icard = card.CardData;
                if (icard.IsBoardCard())
                {
                    player.cards_board.Add(card);
                    card.slot = slot;
                    card.exhausted = true; //Cant attack first turn
                    game_data.last_summoned = card.uid;
                    game_data.last_summoned_slot = slot;
                }
                else if (icard.IsEquipment())
                {
                    Card bearer = game_data.GetSlotCard(slot);
                    EquipCard(bearer, card);
                    card.exhausted = true;
                }
                else if (icard.IsPlayerAbility())
                {
                    player.player_ability.Add(card);
                }
                else if (icard.IsAttachment())
                {
                    AttachCard(slot, card);
                    card.exhausted = true;
                }
                else if (icard.IsSecret())
                {
                    player.cards_secret.Add(card);
                }
                else
                {
                    player.cards_discard.Add(card);
                    card.slot = slot; //Save slot in case spell has PlayTarget
                }

                //History
                if (!is_ai_predict && !icard.IsSecret())
                    player.AddHistory(GameAction.PlayCard, card);

                //Update ongoing effects
                game_data.last_played = card.uid;
                UpdateOngoing();

                if (!skip_cost)
                {
                    //Trigger abilities
                    TriggerSecrets(AbilityTrigger.OnPlayOther, card); //After playing card

                    TriggerCardAbilityType(AbilityTrigger.OnPlay, card);
                    TriggerOtherCardsAbilityType(AbilityTrigger.OnPlayOther, card);
                }

                TriggerSecrets(AbilityTrigger.OnUseOther, card); //After summon card
                TriggerCardAbilityType(AbilityTrigger.OnUse, card);
                TriggerOtherCardsAbilityType(AbilityTrigger.OnUseOther, card);

                RefreshData();

                onCardPlayed?.Invoke(card, slot);
                resolve_queue.ResolveAll(0.3f);
            }
        }

        public virtual void MoveCard(Card card, Slot slot, bool skip_cost = false)
        {
            if (game_data.CanMoveCard(card, slot, skip_cost))
            {
                card.slot = slot;

                //Moving doesn't really have any effect in demo so can be done indefinitely
                //if(!skip_cost)
                //card.exhausted = true;
                //card.RemoveStatus(StatusEffect.Stealth);
                //player.AddHistory(GameAction.Move, card);

                //Also move the equipment
                Card equip = game_data.GetEquipCard(card.equipped_uid);
                if (equip != null)
                    equip.slot = slot;

                UpdateOngoing();
                RefreshData();

                onCardMoved?.Invoke(card, slot);
                resolve_queue.ResolveAll(0.2f);
            }
        }

        public virtual void CastAbility(Card card, AbilityData iability)
        {
            if (game_data.CanCastAbility(card, iability))
            {
                Player player = game_data.GetPlayer(card.player_id);
                if (!is_ai_predict && iability.criteria_target != AbilityTarget.SelectTarget)
                    player.AddHistory(GameAction.CastAbility, card, iability);
                card.RemoveStatus(StatusType.Stealth);
                TriggerCardAbility(iability, card);
                resolve_queue.ResolveAll();
            }
        }

        public virtual void AttackTarget(Card attacker, Card target, bool skip_cost = false)
        {
            Player player = game_data.GetPlayer(attacker.player_id);

            //if (!game_data.CanAttackTarget(attacker, target))
            //    return;

            if(!is_ai_predict)
                player.AddHistory(GameAction.Attack, attacker, target);
            
            game_data.last_attack = attacker.uid;
            game_data.last_attack_slot = attacker.slot;
            game_data.last_attacked = target.uid;
            game_data.last_attacked_slot = target.slot;
            game_data.last_player_attacked = false;

            //Trigger before attack abilities
            TriggerCardAbilityType(AbilityTrigger.OnBeforeAttack, attacker, target);
            TriggerCardAbilityType(AbilityTrigger.OnBeforeDefend, target, attacker);
            //TriggerSecrets(AbilityTrigger.OnBeforeAttack, attacker);
            //TriggerSecrets(AbilityTrigger.OnBeforeDefend, target);
            TriggerOtherCardsAbilityType(AbilityTrigger.OnBeforeAttackOther, attacker);
            TriggerOtherCardsAbilityType(AbilityTrigger.OnBeforeDefendOther, target);

            //Resolve attack
            resolve_queue.AddAttack(attacker, target, ResolveAttack, skip_cost);
            resolve_queue.ResolveAll(0.2f);
        }

        protected virtual void ResolveAttack(Card attacker, Card target, bool skip_cost)
        {
            if (!game_data.IsOnBoard(attacker) || !game_data.IsOnBoard(target))
                return;

            if (!game_data.CanAttackTarget(attacker, target, skip_cost))
                return;

            //if (!attacker.slot.GetNeighborSlot(attacker.GetRange()).Contains(target.slot))
            //    return;
            

            attacker.RemoveStatus(StatusType.Stealth);

            if (target.HasStatus(StatusType.Evasion))
            {
                float ran = UnityEngine.Random.Range(0.0f, 1.0f);
                if (ran < 0.5)
                    return;
            }

            onAttackStart?.Invoke(attacker, target);

            //if (attacker.GetWeaponType() == WeaponType.FRONT)
            //    onAttackStart?.Invoke(target, attacker);
            //attacker.RemoveStatus(StatusType.Stealth);
            UpdateOngoing();

            resolve_queue.AddAttack(attacker, target, ResolveAttackHit, skip_cost);
            resolve_queue.ResolveAll(0.2f);
        }

        protected virtual void ResolveAttackHit(Card attacker, Card target, bool skip_cost)
        {
            //Count attack damage
            int datt1 = attacker.GetAttack();
            int datt2 = target.GetAttack();

            Player player = game_data.GetPlayer(attacker.player_id);
            //Slot target_slot = target.slot;

            //Damage Cards (공격 스탭 변경점, 롤백 시 DamageCard1을 DamageCard로 변경))
            DamageCard(attacker, target, datt1);

            //Counter Damage
            if(attacker.GetWeaponType() == WeaponType.FRONT && !attacker.HasStatus(StatusType.Intimidate))
                DamageCard(target, attacker, datt2, false, true);

            //Save attack and exhaust
            if (!skip_cost)
                ExhaustBattle(attacker);

            //Recalculate bonus
            UpdateOngoing();

            //Abilities
            bool att_board = game_data.IsOnBoard(attacker);
            bool def_board = game_data.IsOnBoard(target);
            if (att_board)
                TriggerCardAbilityType(AbilityTrigger.OnAfterAttack, attacker, target);
            if (def_board)
                TriggerCardAbilityType(AbilityTrigger.OnAfterDefend, target, attacker);
            if (att_board)
                TriggerSecrets(AbilityTrigger.OnAfterAttack, attacker);
            if (def_board)
                TriggerSecrets(AbilityTrigger.OnAfterDefend, target);

            TriggerOtherCardsAbilityType(AbilityTrigger.OnAfterAttackOther, attacker);
            TriggerOtherCardsAbilityType(AbilityTrigger.OnAfterDefendOther, target);

            Debug.Log("end attack");
            resolve_queue.ResolveAll(0.2f);

            onAttackHit?.Invoke(attacker, target);
            onAttackEnd?.Invoke(attacker, target);
            RefreshData();
            CheckForWinner();
        }

        public virtual void AttackPlayer(Card attacker, Player target, bool skip_cost = false)
        {
            if (attacker == null || target == null)
                return;

            //if (!game_data.CanAttackTarget(attacker, target, skip_cost))
            //    return;
            
            Player player = game_data.GetPlayer(attacker.player_id);
            
            game_data.last_player_attacked = true;

            if(!is_ai_predict)
                player.AddHistory(GameAction.AttackPlayer, attacker, target);

            //Resolve abilities
            TriggerSecrets(AbilityTrigger.OnBeforeAttack, attacker);
            TriggerCardAbilityType(AbilityTrigger.OnBeforeAttack, attacker, target);
            TriggerOtherCardsAbilityType(AbilityTrigger.OnBeforeAttackOther, attacker);

            //Resolve attack
            resolve_queue.AddAttack(attacker, target, ResolveAttackPlayer, skip_cost);
            resolve_queue.ResolveAll(0.2f);
        }

        protected virtual void ResolveAttackPlayer(Card attacker, Player target, bool skip_cost)
        {
            if (!game_data.IsOnBoard(attacker))
                return;

            if (!game_data.CanAttackTarget(attacker, target, skip_cost))
                return;

            onAttackPlayerStart?.Invoke(attacker, target);

            attacker.RemoveStatus(StatusType.Stealth);
            UpdateOngoing();

            resolve_queue.AddAttack(attacker, target, ResolveAttackPlayerHit, skip_cost);
            resolve_queue.ResolveAll(0.2f);
        }

        protected virtual void ResolveAttackPlayerHit(Card attacker, Player target, bool skip_cost)
        {
            DamagePlayer(attacker, target, attacker.GetAttack());

            //Save attack and exhaust
            if (!skip_cost)
                ExhaustBattle(attacker);

            //Recalculate bonus
            UpdateOngoing();

            if (game_data.IsOnBoard(attacker))
                TriggerCardAbilityType(AbilityTrigger.OnAfterAttack, attacker, target);

            TriggerSecrets(AbilityTrigger.OnAfterAttack, attacker);
            TriggerOtherCardsAbilityType(AbilityTrigger.OnAfterAttackOther, attacker);

            onAttackPlayerHit?.Invoke(attacker, target);
            onAttackPlayerEnd?.Invoke(attacker, target);
            RefreshData();
            CheckForWinner();

            resolve_queue.ResolveAll(0.2f);
        }

        //Exhaust after battle
        public virtual void ExhaustBattle(Card attacker)
        {
            bool attacked_before = game_data.cards_attacked.Contains(attacker.uid);
            game_data.cards_attacked.Add(attacker.uid);
            bool attack_again = attacker.HasStatus(StatusType.Fury) && !attacked_before;
            attacker.exhausted = !attack_again;
        }

        //Redirect attack to a new target
        public virtual void RedirectAttack(Card attacker, Card new_target)
        {
            foreach (AttackQueueElement att in resolve_queue.GetAttackQueue())
            {
                if (att.attacker.uid == attacker.uid)
                {
                    att.target = new_target;
                    att.ptarget = null;
                    att.callback = ResolveAttack;
                    att.pcallback = null;
                    att.scallback = null;
                }
            }
        }

        public virtual void RedirectAttack(Card attacker, Player new_target)
        {
            foreach (AttackQueueElement att in resolve_queue.GetAttackQueue())
            {
                if (att.attacker.uid == attacker.uid)
                {
                    att.ptarget = new_target;
                    att.target = null;
                    att.pcallback = ResolveAttackPlayer;
                    att.callback = null;
                    att.scallback = null;
                }
            }
        }

        public virtual void ShuffleDeck(List<Card> cards)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                Card temp = cards[i];
                int randomIndex = random.Next(i, cards.Count);
                cards[i] = cards[randomIndex];
                cards[randomIndex] = temp;
            }
        }

        public virtual void DrawCard(Player player, int nb = 1)
        {
            for (int i = 0; i < nb; i++)
            {
                if (player.cards_deck.Count > 0)
                {
                    Card card = player.cards_deck[0];
                    player.cards_deck.RemoveAt(0);

                    if (player.cards_hand.Count < GameplayData.Get().cards_max)
                    {
                        player.cards_hand.Add(card);
                        TriggerPlayerCardsAbilityType(player, AbilityTrigger.OnDraw);
                    }
                    else
                    {
                        DrawDiscardCard(player); // 손패 가득 → 묘지로 보냄

                        onCardDissolved?.Invoke(card, player.player_id);
                    }
                }
                else
                {
                    DamagePlayer_Exhaust(player); // 덱 없음 → 데미지
                }
            }

            onCardDrawn?.Invoke(nb);
        }

        //Put a card from deck into discard
        public virtual void DrawDiscardCard(Player player, int nb = 1)
        {
            for (int i = 0; i < nb; i++)
            {
                if (player.cards_deck.Count > 0)
                {
                    Card card = player.cards_deck[0];
                    player.cards_deck.RemoveAt(0);
                    player.cards_discard.Add(card);
                }
            }
        }

        //Summon copy of an exiting card
        public virtual Card SummonCopy(Player player, Card copy, Slot slot)
        {
            CardData icard = copy.CardData;
            return SummonCard(player, icard, copy.VariantData, slot);
        }

        //Summon copy of an exiting card into hand
        public virtual Card SummonCopyHand(Player player, Card copy)
        {
            CardData icard = copy.CardData;
            return SummonCardHand(player, icard, copy.VariantData);
        }

        public virtual Card SummonCard(Player player, CardData card, VariantData variant, Slot slot)
        {
            Card acard = SummonCardHand(player, card, variant);
            PlayCard(acard, slot, true);

            if (player.cards_hand.Contains(acard))
                player.RemoveCardFromAllGroups(acard);

            return acard;
        }

        /*
        //Create a new card and send it to the board with no mana
        public virtual Card UseCard(Player player, CardData icard, VariantData variant, Slot slot)
        {
            //if (!slot.IsValid())
            //    return null;

            //if (game_data.GetSlotCard(slot) != null)
            //    return null;

            Card card = SummonCardHand(player, icard, variant);

            if (game_data.CanPlayCard(card, slot, true))
            {
                //Play card
                player.RemoveCardFromAllGroups(card);

                //Add to board
                //CardData icard = card.CardData;
                if (icard.IsBoardCard())
                {
                    player.cards_board.Add(card);
                    card.slot = slot;
                    card.exhausted = true; //Cant attack first turn
                    game_data.last_summoned = card.uid;
                    game_data.last_summoned_slot = slot;
                }

                else if (icard.IsEquipment())
                {
                    Card bearer = game_data.GetSlotCard(slot);
                    EquipCard(bearer, card);
                    card.exhausted = true;
                }

                else if (icard.IsAttachment())
                {
                    AttachCard(slot, card);
                    card.exhausted = true;
                }

                else if (icard.IsSecret())
                {
                    player.cards_secret.Add(card);
                }

                else if (icard.IsPlayerAbility())
                {
                    player.player_ability.Add(card);
                }

                else
                {
                    player.cards_discard.Add(card);
                    card.slot = slot; //Save slot in case spell has PlayTarget
                }

                //History
                if (!is_ai_predict && !icard.IsSecret())
                    player.AddHistory(GameAction.PlayCard, card);

                //Update ongoing effects
                //game_data.last_played = card.uid;
                UpdateOngoing();

                //Trigger abilities
                TriggerSecrets(AbilityTrigger.OnUseOther, card); //After playing card
                TriggerCardAbilityType(AbilityTrigger.OnUse, card);
                TriggerOtherCardsAbilityType(AbilityTrigger.OnUseOther, card);

                RefreshData();

                onCardPlayed?.Invoke(card, slot);
                resolve_queue.ResolveAll(0.3f);
            }

            onCardSummoned?.Invoke(card, slot);

            return card;
        }
        */

        //Create a new card and send it to your hand
        public virtual Card SummonCardHand(Player player, CardData card, VariantData variant)
        {
            Card acard = Card.Create(card, variant, player);
            player.cards_hand.Add(acard);
            game_data.last_summoned = acard.uid;
            game_data.last_summoned_slot = acard.slot;
            return acard;
        }

        //Transform card into another one
        public virtual Card TransformCard(Card card, CardData transform_to)
        {
            card.SetCard(transform_to, card.VariantData);

            onCardTransformed?.Invoke(card);

            return card;
        }

        public virtual void EquipCard(Card card, Card equipment)
        {
            if (card != null && equipment != null && card.player_id == equipment.player_id)
            {
                if (!card.CardData.IsEquipment() && equipment.CardData.IsEquipment())
                {
                    UnequipAll(card); //Unequip previous cards, only 1 equip at a time

                    Player player = game_data.GetPlayer(card.player_id);
                    player.RemoveCardFromAllGroups(equipment);
                    player.cards_equip.Add(equipment);
                    card.equipped_uid = equipment.uid;
                    equipment.slot = card.slot;
                }
            }
        }

        public virtual void UnequipAll(Card card)
        {
            if (card != null && card.equipped_uid != null)
            {
                Player player = game_data.GetPlayer(card.player_id);
                Card equip = player.GetEquipCard(card.equipped_uid);
                if (equip != null)
                {
                    card.equipped_uid = null;
                    DiscardCard(equip);
                }
            }
        }

        public virtual void AttachCard(Slot slot, Card attachment)
        {
            if (slot != null && attachment != null)
            {
                if (attachment.CardData.IsAttachment())
                {
                    DetachAll(slot); //Detach previous cards, only 1 attach at a time
                    Player player = game_data.GetPlayer(attachment.player_id);
                    player.RemoveCardFromAllGroups(attachment);
                    player.cards_attach.Add(attachment);
                    attachment.slot = slot;
                }
            }
        }

        public virtual void DetachAll(Slot slot)
        {
            Card attached_card = game_data.GetAttachCard(slot);

            if (slot != null && slot.IsValid() && attached_card != null)
            {
                attached_card.slot = Slot.None;
                DiscardCard(attached_card);
            }

        }

        //Change owner of a card
        public virtual void ChangeOwner(Card card, Player owner)
        {
            if (card.player_id != owner.player_id)
            {
                Player powner = game_data.GetPlayer(card.player_id);
                powner.RemoveCardFromAllGroups(card);
                powner.cards_all.Remove(card.uid);
                owner.cards_all[card.uid] = card;
                card.player_id = owner.player_id;
            }
        }

        //Damage a player
        public virtual void DamagePlayer(Card attacker, Player target, int value)
        {
            //Damage player
            target.hp -= value;
            target.hp = Mathf.Clamp(target.hp, 0, target.hp_max);

            //Lifesteal
            Player aplayer = game_data.GetPlayer(attacker.player_id);
            if (attacker.HasStatus(StatusType.LifeSteal))
                aplayer.hp += value;
        }
        
        public virtual void DamagePlayer_Event(Card attacker, Player target, int value)
        {
            //Damage player
            target.hp -= value;
            target.hp = Mathf.Clamp(target.hp, 0, target.hp_max);

            //Lifesteal
            Player aplayer = game_data.GetPlayer(attacker.player_id);
            if (attacker.HasStatus(StatusType.LifeSteal))
                aplayer.hp += value;
        }

        public virtual void DamagePlayer_Exhaust(Player target)
        {
            target.exhaust_damage += 1;

            //Damage player
            target.hp -= target.exhaust_damage;
            target.hp = Mathf.Clamp(target.hp, 0, target.hp_max);
        }

        //Heal a card
        public virtual void HealCard(Card target, int value)
        {
            if (target == null)
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return;

            if (target.card_id == "Akashi_Junko")
                value = 0;

            int before = target.damage;

            target.damage -= value;
            target.damage = Mathf.Max(target.damage, 0);

            Player p = game_data.GetPlayer(target.player_id);

            p.total_heal += (before - target.damage);

            TriggerCardAbilityType(AbilityTrigger.OnHeal, target);
            TriggerOtherCardsAbilityType(AbilityTrigger.OnHealOther, target);
        }

        public virtual void HealPlayer(Player target, int value)
        {
            if (target == null)
                return;

            int before = target.hp;
            target.hp += value;
            target.hp = Mathf.Clamp(target.hp, 0, target.hp_max);
            int after = target.hp;

            target.total_heal += (after - before);
        }

        //Generic damage that doesnt come from another card
        public virtual void DamageCard(Card target, int value)
        {
            if(target == null)
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return; //Invincible

            if (target.HasStatus(StatusType.SpellImmunity))
                return; //Spell immunity

            target.damage += value;

            if (target.GetHP() <= 0)
                DiscardCard(target);
        }

        //Damage a card with attacker/caster
        public virtual void DamageCard(Card attacker, Card target, int value, bool spell_damage = false, bool counter_attack = false)
        {
            if (attacker == null || target == null)
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return; //Invincible

            if (target.HasStatus(StatusType.SpellImmunity) && !attacker.CardData.IsCitizen())
                return; //Spell immunity

            //Shell
            bool doublelife = target.HasStatus(StatusType.Shell);
            if (doublelife && value > 0)
            {
                target.RemoveStatus(StatusType.Shell);
                return;
            }

            //Armor
            if (!spell_damage && target.HasStatus(StatusType.Armor))
                value = Mathf.Max(value - target.GetStatusValue(StatusType.Armor), 0);

            //Damage
            int damage_max = Mathf.Min(value, target.GetHP());
            int extra = value - target.GetHP();
            target.damage += value;

            if (value > 0)
                TriggerCardAbilityType(AbilityTrigger.OnAfterDamage, attacker, target);

            //Trample
            Player tplayer = game_data.GetPlayer(target.player_id);
            if (!spell_damage && extra > 0 && attacker.player_id == game_data.current_player && attacker.HasStatus(StatusType.Trample))
                tplayer.hp -= extra;

            //Lifesteal
            Player player = game_data.GetPlayer(attacker.player_id);
            if (!spell_damage && attacker.HasStatus(StatusType.LifeSteal))
                player.hp += damage_max;

            //Remove sleep on damage
            target.RemoveStatus(StatusType.Sleep);

            //Deathtouch
            if (value > 0 && attacker.HasStatus(StatusType.Deathtouch) && target.CardData.IsCitizen())
                KillCard(attacker, target, counter_attack);

            //Kill card if no hp
            if (target.GetHP() <= 0)
                KillCard(attacker, target, counter_attack);
        }

        //Damage a slot with attacker/caster
        public virtual void DamageCard(Card attacker, Slot target, int value, bool spell_damage = false)
        {
            Player oplayer = game_data.GetOpponentPlayer(attacker.player_id);
            Card card_target = game_data.GetSlotCard(target);
            Card attach_target = game_data.GetAttachCard(target);

            foreach (var slot in Slot.GetInsideSlot(oplayer.player_id)) 
            {
                if (slot == target)
                    DamagePlayer(attacker, oplayer, value);
            }  

            if (card_target != null)
                DamageCard(attacker, card_target, value, spell_damage);
            
            if (attach_target != null)
                TriggerCardAbilityType(AbilityTrigger.OnAfterDamage, attacker, attach_target);
        }

        // 어빌리티로 트리거 되지 않은 데미지
        public virtual void DamageCard_Event(Card target, int value)
        {
            if (target == null)
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return; //Invincible

            if (target.HasStatus(StatusType.SpellImmunity))
                return; //Spell immunity

            target.damage += value;

            if (target.GetHP() <= 0)
                DiscardCard(target);
        }

        //Damage a card with attacker/caster
        // 유닛에게 데미지
        public virtual void DamageCard_Event(Card attacker, Card target, int value, bool spell_damage = false)
        {
            if (attacker == null || target == null)
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return; //Invincible

            if (target.HasStatus(StatusType.SpellImmunity) && !attacker.CardData.IsCitizen())
                return; //Spell immunity

            if (target.card_id == "Shishido_Izumi")
            {
                HealCard(target, value);
                return;
            }

            //Shell
            bool doublelife = target.HasStatus(StatusType.Shell);
            if (doublelife && value > 0)
            {
                target.RemoveStatus(StatusType.Shell);
                return;
            }

            //Armor
            if (!spell_damage && target.HasStatus(StatusType.Armor))
                value = Mathf.Max(value - target.GetStatusValue(StatusType.Armor), 0);

            //Damage
            int damage_max = Mathf.Min(value, target.GetHP());
            int extra = value - target.GetHP();
            target.damage += value;

            if (value > 0)
                TriggerCardAbilityType(AbilityTrigger.OnAfterDamage, attacker, target);


            //Trample
            Player tplayer = game_data.GetPlayer(target.player_id);
            if (!spell_damage && extra > 0 && attacker.player_id == game_data.current_player && attacker.HasStatus(StatusType.Trample))
                tplayer.hp -= extra;

            //Lifesteal
            Player player = game_data.GetPlayer(attacker.player_id);
            if (!spell_damage && attacker.HasStatus(StatusType.LifeSteal))
                player.hp += damage_max;

            //Remove sleep on damage
            target.RemoveStatus(StatusType.Sleep);

            //Deathtouch
            if (value > 0 && attacker.HasStatus(StatusType.Deathtouch) && target.CardData.IsCitizen())
                KillCard(attacker, target);

            //Kill card if no hp
            if (target.GetHP() <= 0)
                KillCard(attacker, target);
        }

        //Damage a slot with attacker/caster
        // 슬롯에게 데미지
        public virtual void DamageCard_Event(Card attacker, Slot target, int value, bool spell_damage = false)
        {
            Player oplayer = game_data.GetOpponentPlayer(attacker.player_id);
            Card card_target = game_data.GetSlotCard(target);
            Card attach_target = game_data.GetAttachCard(target);

            foreach (var slot in Slot.GetInsideSlot())
            {
                if (slot == target)
                {
                    Player player = game_data.GetPlayer(target.GetP());
                    if (player != null)
                        DamagePlayer_Event(attacker, player, value);
                }

            }

            if (card_target != null)
                DamageCard_Event(attacker, card_target, value, spell_damage);

            if (attach_target != null)
                TriggerCardAbilityType(AbilityTrigger.OnAfterDamage, attacker, attach_target);
        }

        //A card that kills another card
        public virtual void KillCard(Card attacker, Card target, bool counter_attack = false)
        {
            if (attacker == null || target == null)
                return;

            if (!game_data.IsOnBoard(target) && !game_data.IsEquipped(target))
                return; //Already killed

            if (target.HasStatus(StatusType.Invincibility))
                return; //Cant be killed

            Player pattacker = game_data.GetPlayer(attacker.player_id);
            if (attacker.player_id != target.player_id)
                pattacker.kill_count++;

            DiscardCard(target);

            if (!counter_attack)
                TriggerCardAbilityType(AbilityTrigger.OnKill, attacker, target);
        }
        //Send card into discard
        public virtual void DiscardCard(Card card)
        {
            if (card == null)
                return;

            if (game_data.IsInDiscard(card))
                return; //Already discarded

            CardData icard = card.CardData;
            Player player = game_data.GetPlayer(card.player_id);
            bool was_on_board = game_data.IsOnBoard(card) || game_data.IsEquipped(card);

            //Unequip card
            UnequipAll(card);

            //Detach card
            DetachAll(card.slot);

            //Remove card from board and add to discard
            player.RemoveCardFromAllGroups(card);

            if (!card.CardData.IsPlayerAbility())
            {
                player.cards_discard.Add(card);
                game_data.last_destroyed = card.uid;
                game_data.last_destroyed_slot = card.slot;
            }


            //Remove from bearer
            Card bearer = player.GetBearerCard(card);
            if (bearer != null)
                bearer.equipped_uid = null;

            if (was_on_board)
            {
                //Trigger on death abilities
                TriggerCardAbilityType(AbilityTrigger.OnDeath, card);
                TriggerOtherCardsAbilityType(AbilityTrigger.OnDeathOther, card);
                TriggerSecrets(AbilityTrigger.OnDeathOther, card);
            }

            cards_to_clear.Add(card); //Will be Clear() in the next UpdateOngoing, so that simultaneous damage effects work
            onCardDiscarded?.Invoke(card);
        }

        public int RollRandomValue(int dice)
        {
            return RollRandomValue(1, dice + 1);
        }

        public virtual int RollRandomValue(int min, int max)
        {
            game_data.rolled_value = random.Next(min, max);
            onRollValue?.Invoke(game_data.rolled_value);
            resolve_queue.SetDelay(1f);
            return game_data.rolled_value;
        }

        //--- Abilities --

        public virtual void TriggerCardAbilityType(AbilityTrigger type, Card caster, Card triggerer = null)
        {
            foreach (AbilityData iability in caster.GetAbilities())
            {
                if (iability && iability.trigger == type)
                {
                    TriggerCardAbility(iability, caster, triggerer);
                }
            }

            Card equipped = game_data.GetEquipCard(caster.equipped_uid);
            if(equipped != null)
                TriggerCardAbilityType(type, equipped, triggerer);
        }

        public virtual void TriggerCardAbilityType(AbilityTrigger type, Card caster, Player triggerer)
        {
            foreach (AbilityData iability in caster.GetAbilities())
            {
                if (iability && iability.trigger == type)
                {
                    TriggerCardAbility(iability, caster, triggerer);
                }
            }

            Card equipped = game_data.GetEquipCard(caster.equipped_uid);
            if (equipped != null)
                TriggerCardAbilityType(type, equipped, triggerer);
        }
        
        public virtual void TriggerOtherCardsAbilityType(AbilityTrigger type, Card triggerer)
        {
            foreach (Player oplayer in game_data.players)
            {
                if(oplayer.hero != null)
                    TriggerCardAbilityType(type, oplayer.hero, triggerer);

                foreach (Card card in oplayer.cards_board)
                    TriggerCardAbilityType(type, card, triggerer);
                foreach (Card club in oplayer.cards_club)
                    TriggerCardAbilityType(type, club, triggerer);
            }
        }

        public virtual void TriggerPlayerCardsAbilityType(Player player, AbilityTrigger type)
        {
            if (player.hero != null)
                TriggerCardAbilityType(type, player.hero, player.hero);

            foreach (Card card in player.cards_board)
                TriggerCardAbilityType(type, card, card);
            
            foreach (Card card in player.cards_club)
                TriggerCardAbilityType(type, card, card);

            foreach (Card card in player.cards_attach)
                TriggerCardAbilityType(type, card, card);

            foreach (Card card in player.player_ability)
                TriggerCardAbilityType(type, card, card);

        }

        public virtual void TriggerCardAbility(AbilityData iability, Card caster, Card triggerer = null)
        {
            Card trigger_card = triggerer != null ? triggerer : caster; //Triggerer is the caster if not set

            if (!caster.HasStatus(StatusType.Silenced) && iability.AreTriggerConditionsMet(game_data, caster, trigger_card))
            {
                int current_repeat = 0;
                int max_repeat = iability.GetMaxRepeatTimes(game_data, caster);

                if (iability.AreOngoingRepeatConditionsMet(game_data, max_repeat, current_repeat))
                    RepeatTriggerCardAbility(iability, caster, trigger_card, max_repeat, current_repeat);
            }
        }

        public virtual void RepeatTriggerCardAbility(AbilityData iability, Card caster, Card triggerer = null, int max_repeat = 0, int current_repeat = 0)
        {
            Card trigger_card = triggerer != null ? triggerer : caster; //Triggerer is the caster if not set

            if (!caster.HasStatus(StatusType.Silenced) && iability.AreTriggerConditionsMet(game_data, caster, trigger_card))
            {
                resolve_queue.AddAbility(iability, caster, trigger_card, max_repeat, current_repeat, ResolveCardAbility);
            }
        }

        public virtual void TriggerCardAbility(AbilityData iability, Card caster, Player triggerer)
        {
            if (!caster.HasStatus(StatusType.Silenced) && iability.AreTriggerConditionsMet(game_data, caster, triggerer))
            {
                int current_repeat = 0;
                int max_repeat = iability.GetMaxRepeatTimes(game_data, caster);

                RepeatTriggerCardAbility(iability, caster, caster, max_repeat, current_repeat);
            }
        }

        public virtual void RepeatTriggerCardAbility(AbilityData iability, Card caster, Player triggerer, int max_repeat = 0, int current_repeat = 0)
        {
            if (!caster.HasStatus(StatusType.Silenced) && iability.AreTriggerConditionsMet(game_data, caster, triggerer))
            {
                resolve_queue.AddAbility(iability, caster, caster, max_repeat, current_repeat, ResolveCardAbility);
            }
        }

        //Resolve a card ability, may stop to ask for target
        protected virtual void ResolveCardAbility(AbilityData iability, Card caster, Card triggerer, int max_repeat, int current_repeat)
        {
            if (!caster.CanDoAbilities())
                return; //Silenced card cant cast

            if (iability.trigger == AbilityTrigger.OnDeathOther && (caster.CardData.IsBoardCard() && !game_data.IsOnBoard(caster)))
                return;

            Debug.Log("Trigger Ability " + iability.id + " : " + caster.card_id + " "+System.DateTime.Now.Ticks);

            onAbilityStart?.Invoke(iability, caster);
            game_data.ability_triggerer = triggerer.uid;

            bool is_selector = ResolveCardAbilitySelector(iability, caster, triggerer, max_repeat, current_repeat);
            if (is_selector)
                return; //Wait for player to select

            ResolveCardAbilityPlayTarget(iability, caster);
            ResolveCardAbilityPlayers(iability, caster);
            ResolveCardAbilityCards(iability, caster);
            ResolveCardAbilitySlots(iability, caster);
            ResolveCardAbilityCardData(iability, caster);
            ResolveCardAbilityNoTarget(iability, caster);
            AfterAbilityResolved(iability, caster, triggerer, max_repeat, current_repeat);
        }

        protected virtual bool ResolveCardAbilitySelector(AbilityData iability, Card caster, Card triggerer, int max_repeat, int current_repeat)
        {
            if (!iability.HasValidSelectTarget(game_data, caster))
                return false;

            if (iability.criteria_target == AbilityTarget.SelectTarget || iability.criteria_target == AbilityTarget.SelectCard || iability.criteria_target == AbilityTarget.SelectSlot)
            {
                Debug.Log("?");
                //Wait for target
                GoToSelectTarget(iability, caster, triggerer, max_repeat, current_repeat);
                return true;
            }
            else if (iability.criteria_target == AbilityTarget.CardSelector)
            {
                GoToSelectorCard(iability, caster, triggerer, max_repeat, current_repeat);
                return true;
            }
            else if (iability.criteria_target == AbilityTarget.ChoiceSelector)
            {
                GoToSelectorChoice(iability, caster, triggerer, max_repeat, current_repeat);
                return true;
            }
            return false;
        }

        protected virtual void ResolveCardAbilityPlayTarget(AbilityData iability, Card caster)
        {
            if (iability.criteria_target == AbilityTarget.PlayTarget)
            {
                Slot slot = caster.slot;
                Card slot_card = game_data.GetSlotCard(slot);
                if (slot.IsPlayerSlot())
                {
                    Player tplayer = game_data.GetPlayer(slot.p);
                    if (iability.CanTarget(game_data, caster, tplayer))
                        ResolveEffectTarget(iability, caster, tplayer);
                }
                else if (slot_card != null)
                {
                    if (iability.CanTarget(game_data, caster, slot_card))
                        ResolveEffectTarget(iability, caster, slot_card);
                }
                else
                {
                    if (iability.CanTarget(game_data, caster, slot))
                        ResolveEffectTarget(iability, caster, slot);
                }
            }
        }

        protected virtual void ResolveCardAbilityPlayers(AbilityData iability, Card caster)
        {
            //Get Player Targets based on conditions
            List<Player> targets = iability.GetPlayerTargets(game_data, caster, player_array);

            //Resolve effects
            foreach (Player target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilityCards(AbilityData iability, Card caster)
        {
            //Get Cards Targets based on conditions
            List<Card> targets = iability.GetCardTargets(game_data, caster, card_array);

            //ResolveEffectTarget(iability, caster, targets);
            
            //Resolve effects
            foreach (Card target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
            
        }

        protected virtual void ResolveCardAbilitySlots(AbilityData iability, Card caster)
        {
            //Get Slot Targets based on conditions
            List<Slot> targets = iability.GetSlotTargets(game_data, caster, slot_array);

            //Resolve effects
            foreach (Slot target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilityCardData(AbilityData iability, Card caster)
        {
            //Get Cards Targets based on conditions
            List<CardData> targets = iability.GetCardDataTargets(game_data, caster, card_data_array);

            //Resolve effects
            foreach (CardData target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilityNoTarget(AbilityData iability, Card caster)
        {
            if (iability.criteria_target == AbilityTarget.None)
                iability.DoEffects(this, caster);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, Player target)
        {
            iability.DoEffects(this, caster, target);

            onAbilityTargetPlayer?.Invoke(iability, caster, target);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, Card target)
        {
            iability.DoEffects(this, caster, target);

            onAbilityTargetCard?.Invoke(iability, caster, target);

            game_data.last_target = target.uid;
            game_data.last_targeted_slot = target.slot;
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, List<Card> target)
        {
            iability.DoEffects(this, caster, target);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, Slot target)
        {
            iability.DoEffects(this, caster, target);

            onAbilityTargetSlot?.Invoke(iability, caster, target);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, CardData target)
        {
            iability.DoEffects(this, caster, target);
        }

        protected virtual void AfterAbilityResolved(AbilityData iability, Card caster, Card trigger_card, int max_repeat, int current_repeat)
        {
            Player player = game_data.GetPlayer(caster.player_id);

            //Add to played
            game_data.ability_played.Add(iability.id);

            //Pay cost
            if (iability.trigger == AbilityTrigger.Activate || iability.trigger == AbilityTrigger.None)
            {
                player.mana -= iability.mana_cost;
                caster.exhausted = caster.exhausted || iability.exhaust;
            }

            //Recalculate and clear
            UpdateOngoing();
            CheckForWinner();

            //Chain ability
            if (iability.criteria_target != AbilityTarget.ChoiceSelector && game_data.state != GameState.GameEnded)
            {
                foreach (AbilityData chain_ability in iability.chain_abilities)
                {
                    if (chain_ability != null)
                    {
                        TriggerCardAbility(chain_ability, caster);
                        //TriggerCardAbility(iability, caster);
                    }
                }
            }

            onAbilityEnd?.Invoke(iability, caster);
            resolve_queue.ResolveAll(0.5f);

            if (iability.AreOngoingRepeatConditionsMet(game_data, max_repeat, current_repeat+1))
                RepeatTriggerCardAbility(iability, caster, trigger_card, max_repeat, current_repeat+1);


            RefreshData();
        }

        //This function is called often to update status/stats affected by ongoing abilities
        //It basically first reset the bonus to 0 (CleanOngoing) and then recalculate it to make sure it it still present
        //Only cards in hand and on board are updated in this way
        public virtual void UpdateOngoing()
        {
            Profiler.BeginSample("Update Ongoing");
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                player.ClearOngoing();

                for (int c = 0; c < player.cards_board.Count; c++)
                    player.cards_board[c].ClearOngoing();

                for (int c = 0; c < player.cards_equip.Count; c++)
                    player.cards_equip[c].ClearOngoing();

                for (int c = 0; c < player.cards_attach.Count; c++)
                    player.cards_attach[c].ClearOngoing();

                for (int c = 0; c < player.cards_hand.Count; c++)
                    player.cards_hand[c].ClearOngoing();
                for (int c = 0; c < player.cards_club.Count; c++)
                    player.cards_club[c].ClearOngoing();
            }

            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                UpdateOngoingAbilities(player, player.hero);  //Remove this line if hero is on the board

                for (int c = 0; c < player.cards_board.Count; c++)
                {
                    Card card = player.cards_board[c];
                    List<CardClub> card_clubs = card.GetAllClubs();

                    foreach (CardClub card_club in card_clubs)
                    {
                        // player_clubs에서 모든 CardClub을 가져옴
                        List<CardClub> playerClubList = player.cards_club.SelectMany(club => club.GetAllClubs()).ToList();
                        List<CardClub> revealedClubList = player.clubs_revealed.SelectMany(club => club.GetAllClubs()).ToList();
                        
                        // 조건: player_clubs에 card_club이 포함되어 있고, 이미 공개되지 않은 경우
                        if (playerClubList.Any(club => club.id == card_club.id) && !revealedClubList.Any(club => club.id == card_club.id))
                        {
                            /*
                            // 공개되지 않은 경우에 추가
                            Card matchingCard = player.cards_club.FirstOrDefault(card => card.GetAllClubs().Contains(card_club));
                            if (matchingCard != null)
                            player.clubs_revealed.Add(matchingCard);
                            */

                            Card matchingCard = player.cards_club.FirstOrDefault(card => card.GetAllClubs().Any(club => club.id == card_club.id));

                            if (matchingCard != null)
                                player.clubs_revealed.Add(matchingCard);
                        }
                    }

                    UpdateOngoingAbilities(player, card);
                }

                for (int c = 0; c < player.cards_equip.Count; c++)
                {
                    Card card = player.cards_equip[c];
                    UpdateOngoingAbilities(player, card);
                }

                for (int c = 0; c < player.cards_attach.Count; c++)
                {
                    Card card = player.cards_attach[c];
                    UpdateOngoingAbilities(player, card);
                }

                for (int c = 0; c < player.cards_club.Count; c++)
                {
                    Card card = player.cards_club[c];
                    UpdateOngoingAbilities(player, card);
                }

                for (int c = 0; c < player.player_ability.Count; c++)
                {
                    Card card = player.player_ability[c];
                    UpdateOngoingAbilities(player, card);
                }
            }

            //Stats bonus
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                for(int c=0; c<player.cards_board.Count; c++)
                {
                    Card card = player.cards_board[c];

                    //Taunt effect
                    /*
                    if (card.HasStatus(StatusType.Protection) && !card.HasStatus(StatusType.Stealth))
                    {
                        player.AddOngoingStatus(StatusType.Protected, 0);

                        for (int tc = 0; tc < player.cards_board.Count; tc++)
                        {
                            Card tcard = player.cards_board[tc];
                            if (!tcard.HasStatus(StatusType.Protection) && !tcard.HasStatus(StatusType.Protected))
                            {
                                tcard.AddOngoingStatus(StatusType.Protected, 0);
                            }
                        }
                    }
                    */

                    //Status bonus
                    foreach (CardStatus status in card.status)
                        AddOngoingStatusBonus(card, status);
                    foreach (CardStatus status in card.ongoing_status)
                        AddOngoingStatusBonus(card, status);
                }

                for (int c = 0; c < player.cards_hand.Count; c++)
                {
                    Card card = player.cards_hand[c];
                    //Status bonus
                    foreach (CardStatus status in card.status)
                        AddOngoingStatusBonus(card, status);
                    foreach (CardStatus status in card.ongoing_status)
                        AddOngoingStatusBonus(card, status);
                }

                for (int c = 0; c < player.player_ability.Count; c++) //MP3
                {
                    Card card = player.player_ability[c];
                    //Status bonus
                    foreach (CardStatus status in card.status)
                        AddOngoingStatusBonus(card, status);
                    foreach (CardStatus status in card.ongoing_status)
                        AddOngoingStatusBonus(card, status);
                }
            }

            //Kill stuff with 0 hp
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                for (int i = player.cards_attach.Count - 1; i >= 0; i--)
                {
                    Card card = player.cards_attach[i];
                    if (card.GetHP() <= 0)
                        DiscardCard(card);
                }

                for (int i = player.cards_board.Count - 1; i >= 0; i--)
                {
                    Card card = player.cards_board[i];
                    if (card.GetHP() <= 0)
                        DiscardCard(card);
                }
                for (int i = player.cards_equip.Count - 1; i >= 0; i--)
                {
                    Card card = player.cards_equip[i];
                    if (card.GetHP() <= 0)
                        DiscardCard(card);
                    Card bearer = player.GetBearerCard(card);
                    if(bearer == null)
                        DiscardCard(card);
                }
                for (int i = player.player_ability.Count - 1; i >= 0; i--)
                {
                    Card card = player.player_ability[i];
                    if (card.GetHP() <= 0)
                    {
                        DiscardCard(card);
                    }
                }
            }

            //Clear cards
            for (int c = 0; c < cards_to_clear.Count; c++)
                cards_to_clear[c].Clear();

            cards_to_clear.Clear();

            Profiler.EndSample();
        }

        protected virtual void UpdateOngoingAbilities(Player player, Card card)
        {
            if (card == null || !card.CanDoAbilities())
                return;

            List<AbilityData> cabilities = card.GetAbilities();
            for (int a = 0; a < cabilities.Count; a++)
            {
                AbilityData ability = cabilities[a];
                if (ability != null && ability.trigger == AbilityTrigger.Ongoing && ability.AreTriggerConditionsMet(game_data,  card))
                {
                    if (ability.criteria_target == AbilityTarget.Self)
                    {
                        if (ability.AreCriteriaTargetConditionsMet(game_data, card, card))
                        {
                            ability.DoOngoingEffects(this, card, card);
                        }
                    }

                    if (ability.criteria_target == AbilityTarget.PlayerSelf)
                    {
                        if (ability.AreCriteriaTargetConditionsMet(game_data, card, player))
                        {
                            ability.DoOngoingEffects(this, card, player);
                        }
                    }

                    if (ability.criteria_target == AbilityTarget.AllPlayers || ability.criteria_target == AbilityTarget.PlayerOpponent)
                    {
                        for (int tp = 0; tp < game_data.players.Length; tp++)
                        {
                            if (ability.criteria_target == AbilityTarget.AllPlayers || tp != player.player_id)
                            {
                                Player oplayer = game_data.players[tp];
                                if (ability.AreCriteriaTargetConditionsMet(game_data, card, oplayer))
                                {
                                    ability.DoOngoingEffects(this, card, oplayer);
                                }
                            }
                        }
                    }

                    if (ability.criteria_target == AbilityTarget.EquippedCard)
                    {
                        if (card.CardData.IsEquipment())
                        {
                            //Get bearer of the equipment
                            Card target = player.GetBearerCard(card);
                            if (target != null && ability.AreCriteriaTargetConditionsMet(game_data, card, target))
                            {
                                ability.DoOngoingEffects(this, card, target);
                            }
                        }
                        else if (card.equipped_uid != null)
                        {
                            //Get equipped card
                            Card target = game_data.GetCard(card.equipped_uid);
                            if (target != null && ability.AreCriteriaTargetConditionsMet(game_data, card, target))
                            {
                                ability.DoOngoingEffects(this, card, target);
                            }
                        }
                    }

                    if (ability.criteria_target == AbilityTarget.AttachedSlot)
                    {
                        if (card.CardData.IsAttachment())
                        {
                            Slot attached_slot = player.GetAttachedSlot(card);
                            Card target = player.GetSlotCard(attached_slot);

                            if (attached_slot.IsValid() && target != null)
                            {
                                ability.DoOngoingEffects(this, card, target);
                            }
                        }
                    }

                    if (ability.criteria_target == AbilityTarget.AllCardsAllPiles || ability.criteria_target == AbilityTarget.AllCardsHand || ability.criteria_target == AbilityTarget.AllCardsBoard)
                    {
                        for (int tp = 0; tp < game_data.players.Length; tp++)
                        {
                            //Looping on all cards is very slow, since there are no ongoing effects that works out of board/hand we loop on those only
                            Player tplayer = game_data.players[tp];

                            //Hand Cards
                            if (ability.criteria_target == AbilityTarget.AllCardsAllPiles || ability.criteria_target == AbilityTarget.AllCardsHand)
                            {
                                for (int tc = 0; tc < tplayer.cards_hand.Count; tc++)
                                {
                                    Card tcard = tplayer.cards_hand[tc];
                                    if (ability.AreCriteriaTargetConditionsMet(game_data, card, tcard))
                                    {
                                        ability.DoOngoingEffects(this, card, tcard);
                                    }
                                }
                            }

                            //Board Cards
                            if (ability.criteria_target == AbilityTarget.AllCardsAllPiles || ability.criteria_target == AbilityTarget.AllCardsBoard)
                            {
                                for (int tc = 0; tc < tplayer.cards_board.Count; tc++)
                                {
                                    Card tcard = tplayer.cards_board[tc];
                                    if (ability.AreCriteriaTargetConditionsMet(game_data, card, tcard))
                                    {
                                        List<Slot> all_slot = Slot.GetAll();
                                        List<Card> targets = new List<Card>();

                                        foreach(Slot s in all_slot)
                                        {
                                            if (ability.AreWideRangeConditionsMet(game_data, card, tcard.slot, s) && game_data.GetSlotCard(s) != null)
                                            {
                                                targets.Add(game_data.GetSlotCard(s));
                                            }
                                        }

                                        foreach(Card t in targets)
                                        {
                                            if (ability.AreTargetConditionsMet(game_data, card, t))
                                                ability.DoOngoingEffects(this, card, t);
                                        }
                                    }
                                }
                            }

                            //Equip Cards
                            if (ability.criteria_target == AbilityTarget.AllCardsAllPiles)
                            {
                                for (int tc = 0; tc < tplayer.cards_equip.Count; tc++)
                                {
                                    Card tcard = tplayer.cards_equip[tc];
                                    if (ability.AreCriteriaTargetConditionsMet(game_data, card, tcard))
                                    {
                                        ability.DoOngoingEffects(this, card, tcard);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        protected virtual void AddOngoingStatusBonus(Card card, CardStatus status)
        {
            if (status.type == StatusType.AddAttack)
                card.attack_ongoing += status.value;
            if (status.type == StatusType.AddHP)
                card.hp_ongoing += status.value;
            if (status.type == StatusType.AddManaCost)
                card.mana_ongoing += status.value;
        }

        //---- Secrets ------------

        public virtual bool TriggerPlayerSecrets(Player player, AbilityTrigger secret_trigger)
        {
            for (int i = player.cards_secret.Count - 1; i >= 0; i--)
            {
                Card card = player.cards_secret[i];
                CardData icard = card.CardData;
                if (icard.type == CardType.Secret && !card.exhausted)
                {
                    if (card.AreAbilityConditionsMet(secret_trigger, game_data, card, card))
                    {
                        resolve_queue.AddSecret(secret_trigger, card, card, ResolveSecret);
                        resolve_queue.SetDelay(0.5f);
                        card.exhausted = true;

                        if (onSecretTrigger != null)
                            onSecretTrigger.Invoke(card, card);

                        return true; //Trigger only 1 secret per trigger
                    }
                }
            }
            return false;
        }

        public virtual bool TriggerSecrets(AbilityTrigger secret_trigger, Card trigger_card)
        {
            if (trigger_card != null && trigger_card.HasStatus(StatusType.SpellImmunity))
                return false; //Spell Immunity, triggerer is the one that trigger the trap, target is the one attacked, so usually the player who played the trap, so we dont check the target

            for(int p=0; p < game_data.players.Length; p++ )
            {
                if (p != game_data.current_player)
                {
                    Player other_player = game_data.players[p];
                    for (int i = other_player.cards_secret.Count - 1; i >= 0; i--)
                    {
                        Card card = other_player.cards_secret[i];
                        CardData icard = card.CardData;
                        if (icard.type == CardType.Secret && !card.exhausted)
                        {
                            Card trigger = trigger_card != null ? trigger_card : card;
                            if (card.AreAbilityConditionsMet(secret_trigger, game_data, card, trigger))
                            {
                                resolve_queue.AddSecret(secret_trigger, card, trigger, ResolveSecret);
                                resolve_queue.SetDelay(0.5f);
                                card.exhausted = true;

                                if (onSecretTrigger != null)
                                    onSecretTrigger.Invoke(card, trigger);

                                return true; //Trigger only 1 secret per trigger
                            }
                        }
                    }
                }
            }
            return false;
        }

        protected virtual void ResolveSecret(AbilityTrigger secret_trigger, Card secret_card, Card trigger)
        {
            CardData icard = secret_card.CardData;
            Player player = game_data.GetPlayer(secret_card.player_id);
            if (icard.type == CardType.Secret)
            {
                Player tplayer = game_data.GetPlayer(trigger.player_id);
                if(!is_ai_predict)
                    tplayer.AddHistory(GameAction.SecretTriggered, secret_card, trigger);

                TriggerCardAbilityType(secret_trigger, secret_card, trigger);
                DiscardCard(secret_card);

                if (onSecretResolve != null)
                    onSecretResolve.Invoke(secret_card, trigger);
            }
        }

        //---- Resolve Selector -----

        public virtual void SelectCard(Card target)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            Card triggerer = game_data.GetCard(game_data.selector_triggerer_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || target == null || ability == null)
                return;

            if (game_data.selector == SelectorType.SelectTarget)
            {
                if (!ability.CanTarget(game_data, caster, target))
                    return; //Can't target that target

                Player player = game_data.GetPlayer(caster.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.CastAbility, caster, ability, target);

                game_data.selector = SelectorType.None;
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster, triggerer, game_data.selector_max_repeat, game_data.selector_current_repeat);
                resolve_queue.ResolveAll();
            }

            if (game_data.selector == SelectorType.SelectorCard)
            {
                if (!ability.IsCardSelectionValid(game_data, caster, target, card_array))
                    return; //Supports conditions and filters

                game_data.selector = SelectorType.None;
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster, triggerer, game_data.selector_max_repeat, game_data.selector_current_repeat);
                resolve_queue.ResolveAll();
            }
        }

        public virtual void SelectPlayer(Player target)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            Card triggerer = game_data.GetCard(game_data.selector_triggerer_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || target == null || ability == null)
                return;

            if (game_data.selector == SelectorType.SelectTarget)
            {
                if (!ability.CanTarget(game_data, caster, target))
                    return; //Can't target that target

                Player player = game_data.GetPlayer(caster.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.CastAbility, caster, ability, target);

                game_data.selector = SelectorType.None;
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster, triggerer, game_data.selector_max_repeat, game_data.selector_current_repeat);
                resolve_queue.ResolveAll();
            }
        }

        public virtual void SelectSlot(Slot target)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            Card triggerer = game_data.GetCard(game_data.selector_triggerer_uid);
            Card target_card = game_data.GetSlotCard(target);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || ability == null || !target.IsValid())
                return;
            
            //if (ability.target == AbilityTarget.SelectCard)
            //    return;

            if (game_data.selector == SelectorType.SelectTarget)
            {
                if (ability.criteria_target == AbilityTarget.SelectCard)
                    SelectCard(target_card);
                
                else
                {
                    if (!ability.CanTarget(game_data, caster, target))
                        return; //Conditions not met

                    Player player = game_data.GetPlayer(caster.player_id);
                    if (!is_ai_predict)
                        player.AddHistory(GameAction.CastAbility, caster, ability, target);

                    game_data.selector = SelectorType.None;

                    List<Slot> targets = new List<Slot>();

                    if (ability.criteria_target == AbilityTarget.SelectTarget || ability.criteria_target == AbilityTarget.SelectSlot)
                        targets = Slot.GetAll();
                    
                    foreach (Slot targ in targets)
                    {
                        if (!ability.AreWideRangeConditionsMet(game_data, caster, target, targ))
                            continue;

                        ResolveEffectTarget(ability, caster, targ);
                    }

                    AfterAbilityResolved(ability, caster, triggerer, game_data.selector_max_repeat, game_data.selector_current_repeat);
                    resolve_queue.ResolveAll();
                }
            }
        }

        public virtual void SelectChoice(int choice)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            Card triggerer = game_data.GetCard(game_data.selector_triggerer_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || ability == null || choice < 0)
                return;

            if (game_data.selector == SelectorType.SelectorChoice && ability.criteria_target == AbilityTarget.ChoiceSelector)
            {
                if (choice >= 0 && choice < ability.chain_abilities.Length)
                {
                    AbilityData achoice = ability.chain_abilities[choice];
                    if (achoice != null && game_data.CanSelectAbility(caster, achoice))
                    {
                        game_data.selector = SelectorType.None;
                        AfterAbilityResolved(ability, caster, triggerer, game_data.selector_max_repeat, game_data.selector_current_repeat);
                        ResolveCardAbility(achoice, caster, caster, achoice.GetMaxRepeatTimes(game_data, caster), 0);
                        resolve_queue.ResolveAll();
                    }
                }
            }
        }

        public virtual void CancelSelection()
        {
            if (game_data.selector != SelectorType.None)
            {
                //End selection
                game_data.selector = SelectorType.None;
                RefreshData();
            }
        }

        public virtual void Mulligan(Player player, string[] cards)
        {
            if (game_data.phase == GamePhase.Mulligan && !player.ready)
            {
                int count = 0;
                List<Card> remove_list = new List<Card>();
                foreach (Card card in player.cards_hand)
                {
                    if (cards.Contains(card.uid))
                    {
                        remove_list.Add(card);
                        count++;
                    }
                }

                DrawCard(player, count);

                foreach (Card card in remove_list)
                {
                    player.RemoveCardFromAllGroups(card);
                    //player.cards_discard.Add(card);
                    player.cards_deck.Add(card);
                }

                ShuffleDeck(player.cards_deck);

                player.ready = true;
                //DrawCard(player, count);
                RefreshData();

                if (game_data.AreAllPlayersReady())
                {
                    StartTurn();
                }
            }
        }

        //-----Trigger Selector-----

        protected virtual void GoToSelectTarget(AbilityData iability, Card caster, Card triggerer, int max_repeat, int current_repeat)
        {
            game_data.selector = SelectorType.SelectTarget;
            game_data.selector_player_id = caster.player_id;
            game_data.selector_ability_id = iability.id;
            game_data.selector_caster_uid = caster.uid;
            game_data.selector_triggerer_uid = triggerer.uid;
            game_data.selector_max_repeat = max_repeat;
            game_data.selector_current_repeat = current_repeat;
            RefreshData();
        }

        protected virtual void GoToSelectorCard(AbilityData iability, Card caster, Card triggerer, int max_repeat, int current_repeat)
        {
            game_data.selector = SelectorType.SelectorCard;
            game_data.selector_player_id = caster.player_id;
            game_data.selector_ability_id = iability.id;
            game_data.selector_caster_uid = caster.uid;
            game_data.selector_triggerer_uid = triggerer.uid;
            game_data.selector_max_repeat = max_repeat;
            game_data.selector_current_repeat = current_repeat;
            RefreshData();
        }

        protected virtual void GoToSelectorChoice(AbilityData iability, Card caster, Card triggerer, int max_repeat, int current_repeat)
        {
            game_data.selector = SelectorType.SelectorChoice;
            game_data.selector_player_id = caster.player_id;
            game_data.selector_ability_id = iability.id;
            game_data.selector_caster_uid = caster.uid;
            game_data.selector_triggerer_uid = triggerer.uid;
            game_data.selector_max_repeat = max_repeat;
            game_data.selector_current_repeat = current_repeat;
            RefreshData();
        }

        protected virtual void GoToMulligan()
        {
            game_data.phase = GamePhase.Mulligan;
            game_data.turn_timer = GameplayData.Get().turn_duration;
            foreach (Player player in game_data.players)
                player.ready = false;

            RefreshData();
        }

        //-------------

        public virtual void RefreshData()
        {
            onRefresh?.Invoke();
        }

        public virtual void ClearResolve()
        {
            resolve_queue.Clear();
        }

        public virtual bool IsResolving()
        {
            return resolve_queue.IsResolving();
        }

        public virtual bool IsGameStarted()
        {
            return game_data.HasStarted();
        }

        public virtual bool IsGameEnded()
        {
            return game_data.HasEnded();
        }

        public virtual Game GetGameData()
        {
            return game_data;
        }

        public System.Random GetRandom()
        {
            return random;
        }

        public Game GameData { get { return game_data; } }
        public ResolveQueue ResolveQueue { get { return resolve_queue; } }
    }
}