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
        public UnityAction onAttackPhase;
        public UnityAction onTurnPlay;
        public UnityAction onTurnEnd;
        public UnityAction<int> onMulligan;

        public UnityAction<Card, Slot> onCardPlayed;      
        public UnityAction<Card, Slot> onCardSummoned;
        public UnityAction<Card, Slot> onCardMoved;
        public UnityAction<Card> onCardTransformed;
        public UnityAction<Card> onCardDiscarded;
        public UnityAction<Card, int> onCardDissolved;
        public UnityAction<int> onCardDrawn;
        public UnityAction<Player> onExhaustDamage;
        public UnityAction<int> onRollValue;
        public UnityAction<Card, EffectStatType> onCardStatChange;

        public UnityAction<AbilityData, Card> onAbilityStart;        
        public UnityAction<AbilityData, Card, Card> onAbilityTargetCard;  //Ability, Caster, Target
        public UnityAction<AbilityData, Card, Player> onAbilityTargetPlayer;
        public UnityAction<AbilityData, Card, Slot> onAbilityTargetSlot;
        public UnityAction<AbilityData, Card> onAbilityEnd;

        public UnityAction<Card, Card> onAttackStart;  //Attacker, Defender
        public UnityAction<Card, Card> onAttackEnd;     //Attacker, Defender
        public UnityAction<Card, Card> onAttackHit;
        public UnityAction<Card, Card> onAttackEvade;
        public UnityAction<Card, Card, int> onCardDamaged; //Attacker (null if none), Target, final applied damage (after armor)
        public UnityAction<Card, Player, int> onPlayerDamaged; //Attacker, Target player, applied damage
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

        //Volley death batching: while true, ProcessDeathStep does nothing, so cards killed by an attack
        //stay on the board (mortally wounded) until the attack is fully done and are then removed
        //together in a single death wave.
        //INVARIANT: true exactly while a shot is in flight. Owned exclusively by AttackTargets, which
        //clears it on entry and re-arms it only when it queues another shot — so it is a pure function
        //of the volley loop's state and needs no per-exit cleanup.
        //AttackSearch and ClearTurnData clear it too, as recovery for a chain abandoned between shots:
        //ResolveAttack/AttackTarget can early-return (target bounced off the board, phase changed) and
        //never re-enter AttackTargets, which would otherwise strand the flag on.
        //See the AttackTargets and ProcessDeathStep comments for the full rationale.
        private bool death_step_suspended = false;

        public GameLogic(bool is_ai)
        {
            //is_instant ignores all gameplay delays and process everything immediately, needed for AI prediction
            resolve_queue = new ResolveQueue(null, is_ai);
            resolve_queue.SetDeathStep(ProcessDeathStep, HasPendingDeaths, IsDeathStepSuspended);
            is_ai_predict = is_ai;
        }

        public GameLogic(Game game)
        {
            game_data = game;
            resolve_queue = new ResolveQueue(game, false);
            resolve_queue.SetDeathStep(ProcessDeathStep, HasPendingDeaths, IsDeathStepSuspended);
        }

        public virtual void SetData(Game game)
        {
            game_data = game;
            game.SetRandom(random); //Share logic's thread-safe RNG with the data layer (conditions, etc.)
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

            //Collect match-level setup providers (Adventure level, Total Assault, ...)
            List<IGameSetupProvider> match_providers = game_data.settings.GetMatchProviders();

            //Init boss state if Total Assault match
            TotalAssaultData assault_data = game_data.settings.GetTotalAssaultData();
            if (assault_data != null)
            {
                Player boss = null;
                foreach (Player p in game_data.players)
                {
                    if (p.is_ai) { boss = p; break; }
                }
                if (boss != null)
                {
                    game_data.boss_state = new BossState
                    {
                        player_id = boss.player_id,
                        skill_gauge = assault_data.skill_gauge_start,
                        skill_gauge_max = assault_data.skill_gauge_max,
                        atg_gauge = assault_data.atg_gauge_start,
                        atg_gauge_max = assault_data.atg_gauge_max,
                        groggy_gauge = assault_data.groggy_gauge_start,
                        groggy_gauge_max = assault_data.groggy_gauge_max,
                        skip_next_turn = false,
                    };
                }
            }

            //First player override
            LevelFirst? first_override = FirstNonNull(match_providers, p => p.GetFirstPlayer());
            if (first_override.HasValue)
            {
                if (first_override.Value == LevelFirst.Player)
                    game_data.first_player = 0;
                else if (first_override.Value == LevelFirst.AI)
                    game_data.first_player = 1;
                game_data.current_player = game_data.first_player;
            }

            //Mulligan override
            bool should_mulligan = FirstNonNull(match_providers, p => p.GetMulligan()) ?? GameplayData.Get().mulligan;

            //Starting hand size per player — the actual draw happens after OnGameStart effects resolve
            Dictionary<Player, int> start_hand_counts = new Dictionary<Player, int>();

            //Init each players
            foreach (Player player in game_data.players)
            {
                //Per-player providers (PlayerSetupData) + match-level providers, in priority order
                List<IGameSetupProvider> providers = new List<IGameSetupProvider>();
                PlayerSetupData pdeck = PlayerSetupData.Get(player.deck);
                if (pdeck != null) providers.Add(pdeck);
                providers.AddRange(match_providers);

                //Hp / mana
                player.hp_max = FirstNonNull(providers, prov => prov.GetStartHp(player)) ?? GameplayData.Get().hp_start;
                player.hp = player.hp_max;
                player.mana_max = FirstNonNull(providers, prov => prov.GetStartMana(player)) ?? GameplayData.Get().mana_start;
                player.mana = player.mana_max;

                //Starting hand size — drawn later, after OnGameStart effects resolve
                int dcards = FirstNonNull(providers, prov => prov.GetStartHand(player)) ?? GameplayData.Get().cards_start;
                if (!first_override.HasValue)
                    dcards = player.player_id == game_data.first_player ? dcards : dcards + 1;
                start_hand_counts[player] = dcards;

                //Extra clubs (synergy) — additive across providers
                VariantData variant = VariantData.GetDefault();
                foreach (IGameSetupProvider prov in providers)
                {
                    IEnumerable<CardData> extras = prov.GetExtraClubs(player);
                    if (extras == null) continue;
                    foreach (CardData c in extras)
                    {
                        if (c != null)
                            player.cards_club.Add(Card.Create(c, variant, player));
                    }
                }

                //Per-turn behavior flags — first non-null provider wins; defaults to true
                player.draws_per_turn      = FirstNonNull(providers, prov => prov.GetDrawsPerTurn(player))     ?? true;
                player.mana_grows_per_turn = FirstNonNull(providers, prov => prov.GetManaGrowsPerTurn(player)) ?? true;
            }

            //Assign play order to cards that start the game already in play (hero, clubs,
            //puzzle board cards, player abilities). Order of play drives simultaneous trigger ordering.
            foreach (Player player in game_data.players)
            {
                AssignPlayOrder(player.hero);
                foreach (Card card in player.cards_club)
                    AssignPlayOrder(card);
                foreach (Card card in player.cards_board)
                    AssignPlayOrder(card);
                foreach (Card card in player.player_ability)
                    AssignPlayOrder(card);
            }

            //Start state
            RefreshData();
            onGameStart?.Invoke();

            //OnGameStart abilities fire BEFORE the draw and mulligan now (hand and board are still empty here).
            //(club / hero / player_ability cards). The starting hand is drawn once these resolve.
            game_data.phase = GamePhase.GameStart;
            RefreshData();
            //[EVENT PHASE] 양쪽 플레이어의 게임시작 트리거는 하나의 이벤트다.
            resolve_queue.BeginPhase();
            foreach (Player player in game_data.players)
                TriggerPlayerCardsAbilityType(player, AbilityTrigger.OnGameStart);
            resolve_queue.EndPhase();

            //Flow: OnGameStart effects -> draw starting hand -> mulligan -> first turn.
            //ability_queue drains before callbacks, so effects fully resolve before the draw runs.
            resolve_queue.AddCallback(() => DrawStartingHands(start_hand_counts, first_override));
            if (should_mulligan)
                resolve_queue.AddCallback(GoToMulligan);
            else
                resolve_queue.AddCallback(StartFirstTurn);
            resolve_queue.ResolveAll(GameConfig.Timing.game_start);
        }

        //Draws each player's starting hand (after OnGameStart effects), then adds the second player's coin
        protected virtual void DrawStartingHands(Dictionary<Player, int> start_hand_counts, LevelFirst? first_override)
        {
            if (game_data.state == GameState.GameEnded)
                return;

            bool is_random_first = !first_override.HasValue || first_override.Value == LevelFirst.Random;

            foreach (Player player in game_data.players)
            {
                if (start_hand_counts.TryGetValue(player, out int dcards))
                    DrawCard(player, dcards);

                //Add coin to the second player — fires when first player was chosen randomly
                if (is_random_first && player.player_id != game_data.first_player && GameplayData.Get().second_bonus != null)
                {
                    Card card = Card.Create(GameplayData.Get().second_bonus, VariantData.GetDefault(), player);
                    player.cards_hand.Add(card);
                }
            }

            RefreshData();
        }

        //Returns the first non-null value produced by selector across providers, or null if none.
        private static T? FirstNonNull<T>(List<IGameSetupProvider> providers, System.Func<IGameSetupProvider, T?> selector) where T : struct
        {
            foreach (IGameSetupProvider p in providers)
            {
                T? v = selector(p);
                if (v.HasValue)
                    return v;
            }
            return null;
        }

        //Begins the first turn. OnGameStart abilities already fired before the mulligan (see StartGame).
        public virtual void StartFirstTurn()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            //Leave the Mulligan phase first so the client closes the mulligan panel
            //(MulliganSelector shows while phase == Mulligan). GameStart enables no input and
            //does not fire the new-turn banner (that comes from onTurnStart in StartTurn).
            game_data.phase = GamePhase.GameStart;
            RefreshData();

            //Delay lets the mulligan panel close before the first turn begins.
            resolve_queue.AddCallback(StartTurn);
            resolve_queue.ResolveAll(GameConfig.Timing.first_turn);
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

            //Mana update & refill (before start-of-turn effects; draw happens later, after effects)
            if (player.mana_grows_per_turn)
            {
                player.mana_max += GameplayData.Get().mana_per_turn;
                player.mana_max = Mathf.Min(player.mana_max, GameplayData.Get().mana_max);
            }
            player.mana = player.mana_max;

            //Turn timer and history
            game_data.turn_timer = GameplayData.Get().turn_duration;
            player.history_list.Clear();

            UpdateOngoing();
            RefreshData();
            resolve_queue.AddCallback(BeforeMainPahse);
            resolve_queue.ResolveAll(GameConfig.Timing.turn_start);
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

        public virtual void BeforeMainPahse()
        {
            Player player = game_data.GetActivePlayer();
            //Player poison
            if (player.HasStatus(StatusType.Poisoned))
                player.hp -= player.GetStatusValue(StatusType.Poisoned);

            if (player.hero != null)
                player.hero.Refresh();

            //Refresh Cards and Status Effects
            for (int i = player.cards_board.Count - 1; i >= 0; i--)
            {
                Card card = player.cards_board[i];

                if (!card.HasStatus(StatusType.Sleep))
                    card.Refresh();

                if (card.HasStatus(StatusType.Poisoned))
                    DamageCard_Event(card, card.GetStatusValue(StatusType.Poisoned));
            }

            //StartTurn Abilities
            //[EVENT PHASE] 양쪽 플레이어의 턴시작 트리거는 **하나의 이벤트**다 → 한 Phase로 묶는다.
            //위의 독약 피해는 별개 이벤트이고, 이 메서드는 Phase로 감싸이지 않는 턴 진행 콜백이라
            //(ResolveQueue.Resolve의 callback 분기 주석 참조) 독약 → 사망 처리 → 턴시작 순으로
            //자연히 갈린다.
            resolve_queue.BeginPhase();
            foreach (Player p in game_data.players)
                TriggerPlayerCardsAbilityType(p, AbilityTrigger.StartOfTurn);
            resolve_queue.EndPhase();

            TriggerPlayerSecrets(player, AbilityTrigger.StartOfTurn);

            UpdateOngoing();

            //Draw happens after start-of-turn abilities resolve (ability_queue drains before callbacks)
            resolve_queue.AddCallback(DrawForTurn);
            resolve_queue.AddCallback(StartMainPhase);
            resolve_queue.ResolveAll(GameConfig.Timing.pre_main_phase);
        }

        //Turn draw, runs after start-of-turn effects have resolved
        protected virtual void DrawForTurn()
        {
            if (game_data.state == GameState.GameEnded)
                return;

            Player player = game_data.GetActivePlayer();
            if (player.draws_per_turn)
                DrawCard(player, GameplayData.Get().cards_per_turn);

            RefreshData();
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
            game_data.attack_index = 0; //Start a fresh single-pass over the attack order
            onAttackPhase?.Invoke();
            RefreshData();

            resolve_queue.AddCallback(AttackCheck);
            resolve_queue.ResolveAll(GameConfig.Timing.attack_phase_start);
        }

        public virtual void AttackCheck()
        {
            if (game_data.state == GameState.GameEnded)
                return;
            if (game_data.phase != GamePhase.Attack)
                return;

            Player player = game_data.GetActivePlayer();

            attack_list.Clear();

            List<Slot> attack_order = Slot.GetAttackOrder(player.player_id);

            // 단일 패스: attack_index 커서를 따라 한 방향으로만 진행한다.
            // 이미 지나간(커서보다 앞선) 타일에 유닛이 소환/부활해도 다시 보지 않으므로
            // exhausted가 풀려 있어도 이번 전투에는 공격하지 않는다.
            // 반대로 아직 지나지 않은 타일은 커서가 도달할 때 평가되므로 공격 가능하다.
            while (game_data.attack_index < attack_order.Count)
            {
                Slot slot = attack_order[game_data.attack_index];
                Card attacker = game_data.GetSlotCard(slot);

                if (attacker != null && attacker.player_id == player.player_id && attacker.CanAttack())
                {
                    // 한 명 발사 후 콜백으로 AttackCheck에 복귀한다.
                    // Fury 등으로 exhausted가 풀린 동안은 커서를 전진시키지 않아 같은 타일에서 재공격한다.
                    AttackSearch(attacker);
                    return;
                }

                game_data.attack_index++; //이 타일은 처리 완료(=지나감)
            }

            // 한 바퀴 종료
            resolve_queue.AddCallback(EndTurn);
            resolve_queue.ResolveAll(GameConfig.Timing.attack_phase_end);
        }

        public virtual void AttackSearch(Card attacker, bool skip_cost = false)
        {
            Player player = game_data.GetPlayer(attacker.player_id);
            Player oplayer = game_data.GetOpponentPlayer(player.player_id);

            Dictionary<int, List<Slot>> range_slot = attacker.slot.GetRangeSlot(attacker.GetRange());

            List<Card> candidate_target = attacker.weapon.SearchTarget(this, attacker);
            game_data.attack_list = candidate_target;
            game_data.attack_complete_list.Clear();
            game_data.attack_evade_list.Clear();
            //Every volley starts with the Death Phase live. Recovery, not bookkeeping: if the previous
            //volley's chain was abandoned mid-flight (a micro-step early-returned and never came back
            //through AttackTargets) its hold is still on, and this is where it gets released.
            death_step_suspended = false;

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
            resolve_queue.ResolveAll(GameConfig.Timing.between_attackers);
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

            //End of turn abilities — fire for every player's cards (like StartOfTurn).
            //Owner-only EndOfTurn effects must gate themselves with a Turn condition.
            //[EVENT PHASE] 양쪽 플레이어의 턴종료 트리거는 하나의 이벤트다.
            resolve_queue.BeginPhase();
            foreach (Player p in game_data.players)
                TriggerPlayerCardsAbilityType(p, AbilityTrigger.EndOfTurn);
            resolve_queue.EndPhase();

            onTurnEnd?.Invoke();
            RefreshData();

            resolve_queue.AddCallback(StartNextTurn);
            resolve_queue.ResolveAll(GameConfig.Timing.turn_end);
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
            pending_repeats.Clear();
            death_step_suspended = false; //Queue was just wiped; never carry a volley's death hold across
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

            PlayerSetupData puzzle = deck as PlayerSetupData;

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
                if (hdata != null && hvariant != null)
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
        public virtual void SelectPlayTarget(Card card, Slot slot, bool skip_cost = false)
        {
            Player player = game_data.GetPlayer(card.player_id);
            if (game_data.CanPlayCard(card, slot, skip_cost))
            {
                game_data.selector_caster_slot = slot;
                if (card.HasAbility(AbilityTrigger.OnPlay, AbilityTarget.SelectTarget))
                {
                    AbilityData iability = card.GetAbility(AbilityTrigger.OnPlay);

                    if (iability != null)
                    {
                        if (iability.HasValidSelectTarget(game_data, card))
                        {
                            game_data.selector_hand_index = player.cards_hand.IndexOf(card);
                            player.RemoveCardFromAllGroups(card);
                            player.cards_board_temp.Add(card);
                            card.slot = slot;

                            game_data.last_summoned_temp = card.uid;
                            game_data.last_summoned_temp_slot = slot;

                            game_data.last_selected = "";
                            game_data.last_selected_slot = new Slot(0, 0, -1);

                            GoToSelectTarget(iability, card, card, 1, 1);
                            return;
                        }
                    }
                }

                PlayCard(card, slot, skip_cost);
            }
        }
        
        public virtual void PlayCard(Card card, Slot slot, bool skip_cost = false)
        {
            if (game_data.CanPlayCard(card, slot, skip_cost))
            {
                Player player = game_data.GetPlayer(card.player_id);

                //Entering play: assign order of play (drives simultaneous trigger ordering)
                AssignPlayOrder(card);

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
                resolve_queue.ResolveAll(GameConfig.Timing.play_card);
            }
        }

        //Order of play: called whenever a card enters the field (board/hero/club/equip/attach/player ability).
        //Leaving and re-entering the field assigns a new value (Hearthstone rule). Drives the
        //activation order of simultaneous trigger batches (TriggerOtherCardsAbilityType and co).
        public virtual void AssignPlayOrder(Card card)
        {
            if (card != null)
                card.play_order = ++game_data.play_order_counter;
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

                //Trigger move abilities (only on player-initiated moves, not forced relocation such as knockback)
                if (!skip_cost)
                    TriggerCardAbilityType(AbilityTrigger.OnMove, card);

                resolve_queue.ResolveAll(GameConfig.Timing.move_card);
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

        //---- Volley (multi-target attack) ----
        //Fires ONE shot per invocation and re-enters itself through ResolveDeath, walking
        //game_data.attack_list (built once per volley in AttackSearch) until nothing is left to shoot.
        //A single-target weapon just runs this loop once; MassShooting fills attack_list with every
        //enemy in range (WeaponData.SearchTarget) and so runs it N times.
        //
        //Micro-step chain of one shot:
        //  AttackTargets -> AttackTarget (OnBeforeAttack/Defend) -> ResolveAttack (evade roll, FX)
        //                -> ResolveAttackHit (damage, OnAfterAttack/Defend) -> ResolveDeath -> AttackTargets
        //
        //DEATH BATCHING — the whole volley is one logical phase (Hearthstone: the Death Creation Step
        //runs after the outermost phase ends, never inside it). Each target still gets its own
        //before -> damage -> after sequence, but every card the volley kills is removed together in a
        //single death wave once the last shot is done. Previously each micro-step was an outermost
        //element of the resolve queue, so ProcessDeathStep ran between every shot and target #1 was
        //already gone before target #2 was even picked.
        //
        //There is NO single-target vs multi-target branch here, deliberately: N=1 is just N. The rule is
        //simply "deaths are held while a shot is in flight", which for one target means its death lands
        //after the attack fully finishes instead of interrupting it. ExhaustBattle is pure attacker-side
        //bookkeeping (no triggers, doesn't read the board), so nothing observes that reordering.
        //death_step_suspended is what holds the deaths back; see ProcessDeathStep.
        public virtual void AttackTargets(Card attacker, bool skip_cost = false)
        {
            //Release the hold on every re-entry, then re-arm it below only if another shot goes out.
            //Written this way so the flag is a pure function of "is a shot in flight" and NO exit path —
            //including the early returns below — can leave the Death Phase disabled behind it.
            death_step_suspended = false;

            //Attacks only happen automatically during the attack phase. Block any out-of-phase normal attack.
            //Ability-forced attacks (EffectAttack) pass skip_cost=true and are allowed at any time.
            if (game_data.phase != GamePhase.Attack && !skip_cost)
                return;

            Player player = game_data.GetPlayer(attacker.player_id);

            //if (!game_data.CanAttackTarget(attacker, target))
            //    return;

            //if(!is_ai_predict)
            //    player.AddHistory(GameAction.Attack, attacker, target);

            //Resolve attack
            List<Card> targets = game_data.attack_list;

            foreach (Card target in targets)
            {
                if (game_data.attack_complete_list.Contains(target) || game_data.attack_evade_list.Contains(target))
                    continue; //Already hit, or already missed, earlier in this volley

                //Overkill guard (Hearthstone rule: a mortally wounded character is not selected by later
                //hits of a sequential multi-hit effect). Because deaths are suspended for the duration of
                //the volley, a target killed by an earlier shot — or by a trigger, trample, deathtouch...
                //— is still physically on the board. Without this check it would soak a shot and run
                //OnBeforeDefend / OnAfterDefend, and counter-attack, while already dead.
                //Deliberately only skipped, never added to attack_complete_list: a heal landing mid-volley
                //un-kills it (dying cards are saveable right up to the death step) and a later pass will
                //then shoot it after all.
                if (IsDying(target))
                    continue;

                //A shot is going out: hold the deaths until we come back through here.
                death_step_suspended = true;

                resolve_queue.AddAttack(attacker, target, AttackTarget, skip_cost);
                resolve_queue.ResolveAll(GameConfig.Timing.attack_step);
                return;
            }

            //Nothing left to shoot, and the hold was already released at the top. Safe to fall through
            //without queueing anything: the AttackCheck callback that AttackSearch queued is still
            //waiting, so the resolve loop is alive and its next Resolve() hits the death gate first
            //(ResolveQueue.Resolve) and runs the one ProcessDeathStep that removes everything this
            //volley killed, simultaneously — still ahead of AttackCheck, so the next attacker and any
            //Fury re-attack always search a board with the dead already cleared away.
            ExhaustBattle(attacker);
        }

        public virtual void AttackTarget(Card attacker, Card target, bool skip_cost = false)
        {
            //Attacks only happen automatically during the attack phase. Block any out-of-phase normal attack.
            //Ability-forced attacks (EffectAttack) pass skip_cost=true and are allowed at any time.
            if (game_data.phase != GamePhase.Attack && !skip_cost)
                return;

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
            //어빌리티 효과가 유발한 공격(EffectAttack)만 현재 Phase에 담아 depth-first를 유지한다.
            //플레이어가 건 공격과 볼리 마이크로스텝은 그 자체가 Phase라 base attack 큐로 간다.
            //insert_stack이 아니라 명시적 플래그로 판별하는 이유: 공격 마이크로스텝도 이제
            //스코프를 열기 때문에(ResolveQueue.Resolve의 attack 분기) insert_stack만 보면
            //볼리의 다음 발이 현재 공격의 Phase 안으로 잘못 들어간다.
            if (attack_triggered_by_effect)
                resolve_queue.AddTriggeredAttack(attacker, target, ResolveAttack, skip_cost);
            else
                resolve_queue.AddAttack(attacker, target, ResolveAttack, skip_cost);
            resolve_queue.ResolveAll(GameConfig.Timing.attack_step);
        }

        protected virtual void ResolveAttack(Card attacker, Card target, bool skip_cost)
        {
            if (!game_data.IsOnBoard(attacker) || !game_data.IsOnBoard(target))
                return;

            //if (!game_data.CanAttackTarget(attacker, target, skip_cost))
            //    return;

            //if (!attacker.slot.GetNeighborSlot(attacker.GetRange()).Contains(target.slot))
            //    return;


            attacker.RemoveStatus(StatusType.Stealth);
            
            if (attacker.HasStatus(StatusType.MassShooting) || target.HasStatus(StatusType.Evasion))
            {
                double ran = random.NextDouble();
                if (ran < 0.5)
                {
                    if (!game_data.attack_evade_list.Contains(target))
                        game_data.attack_evade_list.Add(target);
                }
            }

            onAttackStart?.Invoke(attacker, target);

            //if (attacker.GetWeaponType() == WeaponType.FRONT)
            //    onAttackStart?.Invoke(target, attacker);
            //attacker.RemoveStatus(StatusType.Stealth);
            UpdateOngoing();
            RefreshData(); //Sync recalculated stats so buffs show before the hit lands

            resolve_queue.AddAttack(attacker, target, ResolveAttackHit, skip_cost);
            resolve_queue.ResolveAll(GameConfig.Timing.attack_step);
        }

        protected virtual void ResolveAttackHit(Card attacker, Card target, bool skip_cost)
        {
            //Count attack damage
            if (!game_data.attack_evade_list.Contains(target))
            {
                Player player = game_data.GetPlayer(attacker.player_id);

                int datt1 = attacker.GetAttack();
                int datt2 = target.GetAttack();

                DamageCard(attacker, target, datt1);

                if (attacker.GetWeaponType() == WeaponType.FRONT && !attacker.HasStatus(StatusType.Intimidate))
                    DamageCard(target, attacker, datt2, false, true);

                //Save attack and exhaust
                //if (!skip_cost)
                //    ExhaustBattle(attacker);

                //Recalculate bonus
                UpdateOngoing(true);


                //if (att_board)
                TriggerCardAbilityType(AbilityTrigger.OnAfterAttack, attacker, target);
                //if (def_board)
                TriggerCardAbilityType(AbilityTrigger.OnAfterDefend, target, attacker);
                //if (att_board)
                TriggerSecrets(AbilityTrigger.OnAfterAttack, attacker);
                //if (def_board)
                TriggerSecrets(AbilityTrigger.OnAfterDefend, target);

                TriggerOtherCardsAbilityType(AbilityTrigger.OnAfterAttackOther, attacker);
                TriggerOtherCardsAbilityType(AbilityTrigger.OnAfterDefendOther, target);

                game_data.attack_complete_list.Add(target);
            }

            resolve_queue.AddAttack(attacker, target, ResolveDeath, skip_cost);
            resolve_queue.ResolveAll(GameConfig.Timing.attack_hit);

            if (!game_data.attack_evade_list.Contains(target))
                onAttackHit?.Invoke(attacker, target);
            else
                onAttackEvade?.Invoke(attacker, target);

            onAttackEnd?.Invoke(attacker, target);

            //Sync damage right when it is applied (the player-attack path already does this);
            //without it the client only sees the new hp at ResolveDeath, one queue-delay later
            RefreshData();
            //CheckForWinner();
        }
        
        protected virtual void ResolveDeath(Card attacker, Card target, bool skip_cost)
        {
            //Phase 2: lethal combat damage no longer kills here. Kill attribution was recorded in
            //DamageCard when hp dropped to 0, and the Death Creation Step (ProcessDeathStep) does the
            //actual removal. Despite the name this step only chains back into the volley loop: the deaths
            //it used to line up are now deferred to the end of the whole attack (death_step_suspended,
            //see AttackTargets), so `target` is still on the board here even when this shot killed it.
            resolve_queue.AddAttack(attacker, AttackTargets, skip_cost);
            resolve_queue.ResolveAll(GameConfig.Timing.attack_step);

            RefreshData();
        }

        public virtual void AttackPlayer(Card attacker, Player target, bool skip_cost = false)
        {
            if (attacker == null || target == null)
                return;

            //Attacks only happen automatically during the attack phase. Block any out-of-phase normal attack.
            //Ability-forced attacks (EffectAttack) pass skip_cost=true and are allowed at any time.
            if (game_data.phase != GamePhase.Attack && !skip_cost)
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
            resolve_queue.ResolveAll(GameConfig.Timing.attack_player_step);
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
            resolve_queue.ResolveAll(GameConfig.Timing.attack_player_step);
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

            resolve_queue.ResolveAll(GameConfig.Timing.attack_player_step);
        }

        //Exhaust after battle
        public virtual void ExhaustBattle(Card attacker)
        {
            bool attacked_before = game_data.cards_attacked.Contains(attacker.uid);
            game_data.cards_attacked.Add(attacker.uid);
            bool attack_again = attacker.HasStatus(StatusType.Fury) && !attacked_before;
            attacker.exhausted = !attack_again;
        }

        //어빌리티 효과(EffectAttack)가 유발한 공격임을 표시. AttackTarget이 공격 요소를
        //현재 Phase에 담을지(유발) base 큐에 담을지(플레이어 공격/볼리) 가르는 데 쓴다.
        private bool attack_triggered_by_effect = false;

        /// <summary>어빌리티 효과가 유발한 공격. EffectAttack 전용 진입점.</summary>
        public virtual void AttackTargetFromEffect(Card attacker, Card target, bool skip_cost = true)
        {
            attack_triggered_by_effect = true;
            try { AttackTarget(attacker, target, skip_cost); }
            finally { attack_triggered_by_effect = false; }
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
                        player.cards_discard.Add(card); // 손패 가득 → 뽑은 카드를 묘지로 보냄

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

        //Add a club to a card and notify other cards (OnAddClubOther) that it joined; no-op if already a member
        public virtual void AddClub(Card card, string club_id)
        {
            if (card == null || string.IsNullOrEmpty(club_id) || card.HasClub(club_id))
                return;

            card.AddClub(club_id);
            UpdateOngoing();
            TriggerOtherCardsAbilityType(AbilityTrigger.OnAddClubOther, card);
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
                    AssignPlayOrder(equipment); //Field entry can bypass PlayCard (effect-driven equip)
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
                    AssignPlayOrder(attachment); //Field entry can bypass PlayCard (EffectAttach)
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
            onPlayerDamaged?.Invoke(attacker, target, value);

            //Lifesteal
            Player aplayer = game_data.GetPlayer(attacker.player_id);
            if (attacker.HasStatus(StatusType.LifeSteal))
                aplayer.hp += value;
        }
        
        public virtual void DamagePlayer_Event(Card attacker, Player target, int value)
        {
            if (attacker == null || target == null)
                return;

            //Damage player
            target.hp -= value;
            target.hp = Mathf.Clamp(target.hp, 0, target.hp_max);
            onPlayerDamaged?.Invoke(attacker, target, value);

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

            onExhaustDamage?.Invoke(target);
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

            //Healed back above 0 before the death step: it survives, clear stale kill attribution
            //(a card marked dying can't be saved and keeps its attribution)
            if (!target.dying && target.GetHP() > 0)
            {
                target.death_source_uid = null;
                target.death_source_counter = false;
            }

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
            onCardDamaged?.Invoke(null, target, value);

            //Board cards at 0 hp die at the Death Creation Step; other zones are removed immediately
            if (target.GetHP() <= 0 && !game_data.IsOnBoard(target))
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
            onCardDamaged?.Invoke(attacker, target, value);

            //Kill attribution for the Death Creation Step (deaths are deferred; see ProcessDeathStep)
            if (value > 0 && target.GetHP() <= 0)
                SetDeathSource(attacker, target, counter_attack);

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
            onCardDamaged?.Invoke(null, target, value);

            //Board cards at 0 hp die at the Death Creation Step; other zones are removed immediately
            if (target.GetHP() <= 0 && !game_data.IsOnBoard(target))
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
            onCardDamaged?.Invoke(attacker, target, value);

            //Kill attribution for the Death Creation Step (deaths are deferred; see ProcessDeathStep)
            if (value > 0 && target.GetHP() <= 0)
                SetDeathSource(attacker, target, false);

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

            //Deathtouch: marks the target dying (removed at the next Death Creation Step)
            if (value > 0 && attacker.HasStatus(StatusType.Deathtouch) && target.CardData.IsCitizen())
                KillCard(attacker, target);

            //0 hp: no immediate kill — the Death Creation Step collects GetHP()<=0 at step time,
            //so a heal triggered before the step can still save the card (Hearthstone rule)
        }

        //Damage a slot with attacker/caster
        // 슬롯에게 데미지
        public virtual void DamageCard_Event(Card attacker, Slot target, int value, EffectDamageType damage_type = EffectDamageType.Card, bool spell_damage = false)
        {
            Player oplayer = game_data.GetOpponentPlayer(attacker.player_id);
            Card card_target = game_data.GetSlotCard(target);
            Card attach_target = game_data.GetAttachCard(target);

            if (damage_type == EffectDamageType.Slot)
            {
                foreach (var slot in Slot.GetInsideSlot())
                {
                    if (slot == target)
                    {
                        Player player = game_data.GetPlayer(target.GetP());
                        if (player != null)
                            DamagePlayer_Event(attacker, player, value);
                    }

                }
            }


            if (card_target != null)
                DamageCard_Event(attacker, card_target, value, spell_damage);

            if (attach_target != null)
                TriggerCardAbilityType(AbilityTrigger.OnAfterDamage, attacker, attach_target);
        }

        //A card that kills another card. Board cards are only marked dying and are removed at the
        //next Death Creation Step (ProcessDeathStep), which finalizes kill_count/OnKill; equipment
        //doesn't go through the death phase and is removed immediately (previous behavior).
        public virtual void KillCard(Card attacker, Card target, bool counter_attack = false)
        {
            if (attacker == null || target == null)
                return;

            if (game_data.IsOnBoard(target))
            {
                MarkDying(attacker, target, counter_attack);
            }
            else if (game_data.IsEquipped(target))
            {
                if (target.HasStatus(StatusType.Invincibility))
                    return; //Cant be killed

                Player pattacker = game_data.GetPlayer(attacker.player_id);
                if (attacker.player_id != target.player_id)
                    pattacker.kill_count++;

                DiscardCard(target);

                if (!counter_attack)
                    TriggerCardAbilityType(AbilityTrigger.OnKill, attacker, target);
            }
        }

        //Phase 2: mark a board card as mortally wounded instead of removing it immediately.
        //It keeps its slot and keeps reacting to triggers until the next Death Creation Step;
        //healing cannot save a card marked dying (unlike plain 0-hp damage deaths).
        public virtual void MarkDying(Card attacker, Card target, bool counter_attack = false)
        {
            if (target == null || !game_data.IsOnBoard(target))
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return; //Cant be killed

            target.dying = true;
            SetDeathSource(attacker, target, counter_attack);
        }

        //Record which card gets kill credit (kill_count / OnKill) when the target dies at the
        //death step. First lethal source wins; cleared if the card is healed back above 0.
        protected virtual void SetDeathSource(Card attacker, Card target, bool counter_attack)
        {
            if (attacker == null || target == null)
                return;

            if (target.death_source_uid != null)
                return; //Already attributed

            target.death_source_uid = attacker.uid;
            target.death_source_counter = counter_attack;
        }

        //Send card into discard. Immediate removal path (hand/deck discard, equipment, secrets,
        //direct pile moves). Board combat/destroy deaths do NOT come through here anymore: they
        //are marked dying and removed by the Death Creation Step, which fires the death triggers.
        public virtual void DiscardCard(Card card)
        {
            if (card == null)
                return;

            if (game_data.IsInDiscard(card))
                return; //Already discarded

            bool was_on_board = game_data.IsOnBoard(card) || game_data.IsEquipped(card);

            RemoveFromPlay(card);

            if (was_on_board)
            {
                //Trigger on death abilities (immediate path; death-step deaths fire these in ProcessDeathStep)
                TriggerCardAbilityType(AbilityTrigger.OnDeath, card);
                TriggerOtherCardsAbilityType(AbilityTrigger.OnDeathOther, card);
                TriggerSecrets(AbilityTrigger.OnDeathOther, card);
            }

            onCardDiscarded?.Invoke(card);
        }

        //Remove a card from play into the discard pile WITHOUT firing death triggers.
        //Shared by DiscardCard (immediate removal) and the Death Creation Step (simultaneous removal).
        protected virtual void RemoveFromPlay(Card card)
        {
            Player player = game_data.GetPlayer(card.player_id);

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

            cards_to_clear.Add(card); //Will be Clear() in the next UpdateOngoing, so that simultaneous damage effects work
        }

        //---- Death Phase / Death Creation Step (Phase 2) ----
        //Deaths are deferred: lethal damage / destroy effects only mark cards as mortally wounded
        //(Card.dying, or GetHP()<=0 re-checked at step time). ResolveQueue invokes this step at
        //every outermost boundary — whenever an element and its whole depth-first subtree have
        //finished (phase stack empty), before the next waiting element (Hearthstone rule). It also
        //runs between attack micro-steps and before callbacks.
        //The one exception is an attack in flight, which suspends the step so all of that attack's kills
        //are batched into one wave at the end (death_step_suspended, see AttackTargets).
        //See docs/resolve-queue-hearthstone-redesign.md (Phase 2)

        private List<Card> dying_batch = new List<Card>();

        //Repeat iteration deferred by AfterAbilityResolved: its repeat condition is evaluated
        //at the Death Phase stable point, after the deaths and death triggers caused by the
        //previous iteration fully resolved (Hearthstone/Defile pacing).
        protected class PendingRepeat
        {
            public AbilityData ability;
            public Card caster;
            public Card triggerer;
            public int max_repeat;
            public int next_repeat;
        }

        private List<PendingRepeat> pending_repeats = new List<PendingRepeat>();

        //Mortally wounded at this instant (invincible cards can never die; predicate must match
        //ProcessDeathStep's collection exactly or the resolve loop could spin forever)
        protected virtual bool IsDying(Card card)
        {
            if (card.HasStatus(StatusType.Invincibility))
                return false; //Cant be killed
            return card.dying || card.GetHP() <= 0;
        }

        //True while an attack in flight is holding its deaths back (see AttackTargets). Exposed to
        //ResolveQueue for PACING only — it must not affect whether the resolve loop keeps running,
        //which is why HasPendingDeaths below stays truthful about the 0-hp cards on the board.
        protected virtual bool IsDeathStepSuspended()
        {
            return death_step_suspended;
        }

        //Quick check used by ResolveQueue.CanResolve to keep the resolve loop alive while a death
        //step is still pending (e.g. the last resolved element left a 0-hp card behind)
        protected virtual bool HasPendingDeaths()
        {
            if (game_data == null || game_data.state == GameState.GameEnded)
                return false;

            if (pending_repeats.Count > 0)
                return true; //Deferred repeat iteration awaiting evaluation at the stable point

            foreach (Player player in game_data.players)
            {
                foreach (Card card in player.cards_board)
                {
                    if (IsDying(card))
                        return true;
                }
                //player_ability도 일반 효과와 동일하게 죽음 페이즈를 탄다 (2026-08 결정).
                //장비/부착 카드는 여전히 UpdateOngoing의 즉시 제거 경로다.
                foreach (Card card in player.player_ability)
                {
                    if (IsDying(card))
                        return true;
                }
            }
            return false;
        }

        //Runs one death wave. Returns true if anything died (the resolve loop then resolves the
        //queued death triggers and re-runs the step until the board is stable — deathrattle chains).
        //All dying cards are removed BEFORE any trigger fires, so cards that die in the same wave
        //don't receive each other's OnDeathOther (Hearthstone simultaneous-death rule).
        protected virtual bool ProcessDeathStep()
        {
            if (game_data == null || game_data.state == GameState.GameEnded)
                return false;

            //A shot is in flight: hold every death until the attack finishes, so its kills are removed in
            //one wave instead of one-by-one between shots (see AttackTargets).
            //Suppressed HERE rather than in HasPendingDeaths on purpose — HasPendingDeaths also feeds
            //ResolveQueue.CanResolve, and lying to it would let the resolve loop go idle while 0-hp cards
            //are still on the board. Returning false just makes the gate fall through to the next queued
            //element, and the volley always has one queued (its next micro-step, or AttackCheck).
            if (death_step_suspended)
                return false;

            UpdateOngoing(); //Refresh auras first so hp reflects lost/gained ongoing bonuses

            //Collect dying cards, ordered by order of play (first played dies/triggers first)
            dying_batch.Clear();
            foreach (Player player in game_data.players)
            {
                foreach (Card card in player.cards_board)
                {
                    if (IsDying(card))
                        dying_batch.Add(card);
                }
                //player_ability는 일반 효과와 동일 취급 (2026-08 결정) — 즉시 제거가 아니라
                //죽음 페이즈에서 동시 제거되고 OnDeath/OnDeathOther를 정상적으로 발동한다.
                foreach (Card card in player.player_ability)
                {
                    if (IsDying(card))
                        dying_batch.Add(card);
                }
            }

            if (dying_batch.Count == 0)
            {
                //Board stable: deaths and death triggers of the previous wave are fully resolved.
                //This is the Defile timing — deferred repeat iterations are judged here.
                if (ProcessPendingRepeats())
                    return true; //Next iteration(s) queued; the resolve loop picks them up

                CheckForWinner(); //Integrated win check
                return false;
            }

            dying_batch.Sort((a, b) => a.play_order.CompareTo(b.play_order));

            //Death triggers open their own phase. BeginImmediatePhase = "현재 Sequence의 다음
            //Phase" — 대기 중인 다른 최상위 Phase(예: 같은 카드 플레이의 OnPlayOther)보다 **먼저**
            //온다. 하스스톤도 Death Creation Step 직후 Death Phase가 바로 이어진다.
            resolve_queue.BeginImmediatePhase();

            //Remove all at once, without triggers (simultaneous deaths)
            foreach (Card card in dying_batch)
            {
                RemoveFromPlay(card);
                onCardDiscarded?.Invoke(card);
            }

            //Finalize kill attribution (kill_count / OnKill), in death order
            foreach (Card card in dying_batch)
            {
                Card killer = card.death_source_uid != null ? game_data.GetCard(card.death_source_uid) : null;
                if (killer != null)
                {
                    if (killer.player_id != card.player_id)
                        game_data.GetPlayer(killer.player_id).kill_count++;

                    if (!card.death_source_counter)
                        TriggerCardAbilityType(AbilityTrigger.OnKill, killer, card);
                }
            }

            //OnDeath of the dead + OnDeathOther of survivors + secrets, in death order
            foreach (Card card in dying_batch)
            {
                TriggerCardAbilityType(AbilityTrigger.OnDeath, card);
                TriggerOtherCardsAbilityType(AbilityTrigger.OnDeathOther, card);
                TriggerSecrets(AbilityTrigger.OnDeathOther, card);
            }

            resolve_queue.EndPhase();

            //Recompute auras now that the dead are gone. Losing a dead card's HP bonus no longer
            //kills (Hearthstone rule: damage is forgiven so current HP is kept, see
            //ForgiveDamageOnHPMaxLoss) — but cards whose max HP itself drops to 0 without the
            //aura still die in the next wave, so the stability loop remains (stability loop)
            UpdateOngoing();

            RefreshData();
            return true;
        }

        //반복 회차를 죽음 페이즈의 **안정 시점**(이번 회차의 죽음 + 죽메 연쇄 완결)에 판정한다.
        //= 하스스톤 모독(Defile) 페이싱. 회차 조건이 "정리가 끝난 보드"를 보므로 "이번 회차로
        //죽었는가" 류 조건을 쓸 수 있고, 죽메가 소환한 토큰이 다음 회차에 참여한다.
        //
        //회차는 BeginImmediatePhase로 "현재 Sequence의 다음 Phase"가 되어, 대기 중인 다른 최상위
        //Phase보다 먼저 온다. 다만 **같은 이벤트 묶음 안의 형제**보다는 뒤다 — 묶음이 다 비어야
        //죽음 페이즈가 돌기 때문(Rule 3)이고, 물리적으로 앞설 수 없다. 개편 전 문서에 있던
        //"반복분은 대기 배치보다 앞" 규칙은 이 범위로 축소되었다.
        //
        //Returns true if any next iteration was queued.
        protected virtual bool ProcessPendingRepeats()
        {
            if (pending_repeats.Count == 0)
                return false;

            bool any = false;
            resolve_queue.BeginImmediatePhase();
            //Reverse order: entries added later come from deeper elements of the finished
            //subtree; depth-first wants their repeat chains to complete before an outer one's.
            for (int i = pending_repeats.Count - 1; i >= 0; i--)
            {
                PendingRepeat pending = pending_repeats[i];

                //[요그사론 규칙] 하스스톤 Rule 6: "Subsequent Phases of a Sequence will not run if
                //a subject is required but is no longer in play." 시전자가 회차 도중 필드를 떠났으면
                //(자기 광역에 자멸하는 경우 등) 남은 회차는 발동하지 않는다.
                if (pending.caster.CardData.IsBoardCard() && !game_data.IsOnBoard(pending.caster))
                    continue;

                if (pending.ability.AreOngoingRepeatConditionsMet(game_data, pending.max_repeat, pending.next_repeat))
                {
                    RepeatTriggerCardAbility(pending.ability, pending.caster, pending.triggerer, pending.max_repeat, pending.next_repeat);
                    any = true;
                }
            }
            resolve_queue.EndPhase();
            pending_repeats.Clear();
            return any;
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

        //[EVENT PHASE] 하나의 이벤트에 반응하는 트리거들은 반드시 한 Phase 안에 묶여야 한다.
        //하스스톤 Rule 3: 묶음 안에서는 죽음 처리가 절대 끼어들지 않는다 (칼 곡예사 2장 규칙 —
        //"All Knife Juggler effects are handled before any of their deaths are detected").
        //묶지 않으면 각 트리거가 자기 최상위 Phase가 되어 사이사이 사망 웨이브가 돈다.
        public virtual void TriggerCardAbilityType(AbilityTrigger type, Card caster, Card triggerer = null)
        {
            resolve_queue.BeginPhase();
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
            resolve_queue.EndPhase();
        }

        //[EVENT PHASE] Card-triggerer 버전과 동일 (위 주석 참조)
        public virtual void TriggerCardAbilityType(AbilityTrigger type, Card caster, Player triggerer)
        {
            resolve_queue.BeginPhase();
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
            resolve_queue.EndPhase();
        }

        //Reused buffer for ordering simultaneous trigger batches. Safe to share: TriggerCardAbilityType
        //only enqueues abilities, it never resolves during the iteration below.
        private List<Card> trigger_batch = new List<Card>();

        public virtual void TriggerOtherCardsAbilityType(AbilityTrigger type, Card triggerer)
        {
            //Order of play: cards that entered the field first trigger first (not board slot order)
            trigger_batch.Clear();
            foreach (Player oplayer in game_data.players)
            {
                if (oplayer.hero != null)
                    trigger_batch.Add(oplayer.hero);

                foreach (Card card in oplayer.cards_board)
                    trigger_batch.Add(card);
                foreach (Card club in oplayer.cards_club)
                    trigger_batch.Add(club);
            }
            trigger_batch.Sort((a, b) => a.play_order.CompareTo(b.play_order));

            //[EVENT PHASE] 이 배치 전체가 하나의 이벤트다 — 카드 사이에 사망 처리가 끼면 안 된다.
            resolve_queue.BeginPhase();
            foreach (Card card in trigger_batch)
                TriggerCardAbilityType(type, card, triggerer);
            resolve_queue.EndPhase();
        }

        public virtual void TriggerPlayerCardsAbilityType(Player player, AbilityTrigger type)
        {
            //Order of play: cards that entered the field first trigger first (not board slot order)
            trigger_batch.Clear();
            if (player.hero != null)
                trigger_batch.Add(player.hero);

            foreach (Card card in player.cards_club)
                trigger_batch.Add(card);

            foreach (Card card in player.cards_board)
                trigger_batch.Add(card);

            foreach (Card card in player.cards_attach)
                trigger_batch.Add(card);

            foreach (Card card in player.player_ability)
                trigger_batch.Add(card);

            trigger_batch.Sort((a, b) => a.play_order.CompareTo(b.play_order));

            //[EVENT PHASE] 위와 동일 — 턴시작/턴종료/드로우 배치가 한 묶음으로 유지된다.
            resolve_queue.BeginPhase();
            foreach (Card card in trigger_batch)
                TriggerCardAbilityType(type, card, card);
            resolve_queue.EndPhase();
        }

        public virtual void TriggerCardAbility(AbilityData iability, Card caster, Card triggerer = null, bool is_chain = false)
        {
            Card trigger_card = triggerer != null ? triggerer : caster; //Triggerer is the caster if not set

            // [DOUBLE-CHECK TRIGGER CONDITIONS] Hearthstone rule: trigger conditions are evaluated
            // HERE at enqueue time (against the state of the triggering event) AND again at resolve
            // time in ResolveCardAbility (against the latest state). The ability fires only if BOTH
            // pass. Consequence: a condition that was false at the event but becomes true before
            // resolve (e.g. a club host-counter cycled by an earlier ability in the same batch)
            // does NOT fire — the ability is never enqueued.
            // Repeat iterations skip both checks (repeat condition only, see ProcessPendingRepeats).
            if (!caster.CanDoAbilities())
                return; //Silenced card cant trigger
            if (!iability.AreTriggerConditionsMet(game_data, caster, trigger_card))
                return;

            int current_repeat = 0;
            int max_repeat = iability.GetMaxRepeatTimes(game_data, caster);

            if (iability.AreOngoingRepeatConditionsMet(game_data, max_repeat, current_repeat))
                RepeatTriggerCardAbility(iability, caster, trigger_card, max_repeat, current_repeat, is_chain);
        }

        public virtual void RepeatTriggerCardAbility(AbilityData iability, Card caster, Card triggerer = null, int max_repeat = 0, int current_repeat = 0, bool is_chain = false)
        {
            Card trigger_card = triggerer != null ? triggerer : caster; //Triggerer is the caster if not set

            //Raw enqueue. First iterations arrive here through TriggerCardAbility (enqueue-time
            //trigger-condition check already passed); repeat iterations arrive through
            //ProcessPendingRepeats (repeat condition only, trigger conditions never re-checked).
            //Silence and trigger conditions are re-verified at resolve time (ResolveCardAbility).
            resolve_queue.AddAbility(iability, caster, trigger_card, max_repeat, current_repeat, ResolveCardAbility, is_chain);
        }

        public virtual void TriggerCardAbility(AbilityData iability, Card caster, Player triggerer)
        {
            // [DOUBLE-CHECK TRIGGER CONDITIONS] Enqueue-time check against the Player triggerer.
            // This is the only point where player-triggerer conditions can be evaluated: the Player
            // is not carried into the queue (the caster is passed as the trigger card), so the
            // resolve-time re-check in ResolveCardAbility uses the caster as the trigger target.
            if (!caster.CanDoAbilities())
                return; //Silenced card cant trigger
            if (!iability.AreTriggerConditionsMet(game_data, caster, triggerer))
                return;

            int current_repeat = 0;
            int max_repeat = iability.GetMaxRepeatTimes(game_data, caster);

            RepeatTriggerCardAbility(iability, caster, caster, max_repeat, current_repeat);
        }

        public virtual void RepeatTriggerCardAbility(AbilityData iability, Card caster, Player triggerer, int max_repeat = 0, int current_repeat = 0)
        {
            //Raw enqueue (see the Card-triggerer overload above). Enqueue-time checks happen in
            //TriggerCardAbility; resolve-time re-check happens in ResolveCardAbility.
            resolve_queue.AddAbility(iability, caster, caster, max_repeat, current_repeat, ResolveCardAbility);
        }

        //Resolve a card ability, may stop to ask for target
        protected virtual void ResolveCardAbility(AbilityData iability, Card caster, Card triggerer, int max_repeat, int current_repeat)
        {
            if (!caster.CanDoAbilities())
                return; //Silenced card cant cast

            // [DOUBLE-CHECK TRIGGER CONDITIONS] Resolve-time re-check (Hearthstone rule): the
            // conditions already passed at enqueue time (TriggerCardAbility), and are verified
            // again here against the latest state — an ability whose condition was invalidated by
            // an earlier-resolving ability in the same batch is cancelled.
            // Repeat iterations (current_repeat > 0) skip this check on purpose: once an ability
            // fired, its repeats are governed only by the repeat condition (evaluated at the death
            // phase stable point, see ProcessPendingRepeats), even if the first iteration's effects
            // made the trigger condition false. See docs/resolve-queue-hearthstone-redesign.md
            if (current_repeat == 0 && !iability.AreTriggerConditionsMet(game_data, caster, triggerer))
                return;

            if (iability.trigger == AbilityTrigger.OnDeathOther && caster.CardData.IsBoardCard() && !game_data.IsOnBoard(caster))
                return;

            Debug.Log("Trigger Ability " + iability.id + " : " + caster.card_id);

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
            game_data.last_selected = "";
            game_data.last_selected_slot = new Slot(0, 0, -1);

            if (!iability.HasValidSelectTarget(game_data, caster))
                return false;

            if (iability.trigger != AbilityTrigger.OnPlay && iability.criteria_target == AbilityTarget.SelectTarget)
            {
                //Wait for target
                GoToSelectTarget(iability, caster, triggerer, max_repeat, current_repeat);
                return true;
            }

            if (iability.criteria_target == AbilityTarget.CardSelector)
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
                
                if (slot.IsPlayerSlot())
                {
                    Player tplayer = game_data.GetPlayer(slot.p);
                    if (iability.CanTarget(game_data, caster, tplayer))
                        ResolveEffectTarget(iability, caster, tplayer);
                }

                else
                {
                    if (iability.CanTarget(game_data, caster, slot))
                    {
                        List<Slot> target_slots = iability.GetSlotTargets(game_data, caster, true);

                        foreach (Slot target_slot in target_slots)
                            ResolveEffectTarget(iability, caster, target_slot);
                    }
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
            List<Slot> targets = iability.GetSlotTargets(game_data, caster, false, slot_array);

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

            game_data.last_targeted_slot = target;
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

            //Chain ability (is_chain: resolves before abilities triggered by this ability's effects)
            if (iability.criteria_target != AbilityTarget.ChoiceSelector && game_data.state != GameState.GameEnded)
            {
                foreach (AbilityData chain_ability in iability.chain_abilities)
                {
                    if (chain_ability != null)
                    {
                        TriggerCardAbility(chain_ability, caster, null, true);
                        //TriggerCardAbility(iability, caster);
                    }
                }
            }

            onAbilityEnd?.Invoke(iability, caster);
            resolve_queue.ResolveAll(GameConfig.Timing.ability_resolve);

            //Repeat (Hearthstone/Defile pacing): the next iteration is deferred to the Death
            //Phase stable point after this iteration's consequences (deaths + death triggers
            //included) fully resolve. ProcessPendingRepeats then judges it by the repeat
            //condition only (trigger condition is never re-evaluated) and re-enqueues it in a
            //new phase, so iterations still resolve before the waiting batch.
            if (iability.condition_repeat != null || current_repeat + 1 < max_repeat)
            {
                pending_repeats.Add(new PendingRepeat
                {
                    ability = iability,
                    caster = caster,
                    triggerer = trigger_card,
                    max_repeat = max_repeat,
                    next_repeat = current_repeat + 1,
                });
            }


            RefreshData();
        }

        //This function is called often to update status/stats affected by ongoing abilities
        //It basically first reset the bonus to 0 (CleanOngoing) and then recalculate it to make sure it it still present
        //Only cards in hand and on board are updated in this way
        public virtual void UpdateOngoing(bool except_death = false)
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

            //Hearthstone rule: a card whose max HP went down in this recalc (aura source gone,
            //temp buff expired) keeps its current HP, only capped at the new max — losing a HP
            //bonus never kills. Runs before the 0-hp cleanup below so equip/attach cards that
            //merely lost an aura are not discarded. Covers exactly the groups cleared above.
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                for (int c = 0; c < player.cards_board.Count; c++)
                    player.cards_board[c].ForgiveDamageOnHPMaxLoss();
                for (int c = 0; c < player.cards_equip.Count; c++)
                    player.cards_equip[c].ForgiveDamageOnHPMaxLoss();
                for (int c = 0; c < player.cards_attach.Count; c++)
                    player.cards_attach[c].ForgiveDamageOnHPMaxLoss();
                for (int c = 0; c < player.cards_hand.Count; c++)
                    player.cards_hand[c].ForgiveDamageOnHPMaxLoss();
                for (int c = 0; c < player.cards_club.Count; c++)
                    player.cards_club[c].ForgiveDamageOnHPMaxLoss();
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

                //Phase 2: board cards at 0 hp are NOT discarded here anymore. They stay on board
                //(mortally wounded) until the Death Creation Step removes them (ProcessDeathStep),
                //so simultaneous deaths and death-trigger ordering are handled in one place.
                //(except_death is kept for signature compatibility; it no longer changes behavior)

                for (int i = player.cards_equip.Count - 1; i >= 0; i--)
                {
                    Card card = player.cards_equip[i];
                    if (card.GetHP() <= 0)
                        DiscardCard(card);
                    Card bearer = player.GetBearerCard(card);
                    if(bearer == null)
                        DiscardCard(card);
                }
                //player_ability는 2026-08 결정으로 **일반 효과와 동일 취급**한다: 여기서 즉시
                //제거하지 않고 보드 카드처럼 빈사 상태로 남았다가 Death Creation Step에서
                //동시 제거되고 OnDeath/OnDeathOther를 정상 발동한다 (ProcessDeathStep 수집 참조).
                //장비/부착 카드는 미사용 타입이라 기존 즉시 제거 경로를 유지한다.
            }

            //Clear cards
            for (int c = 0; c < cards_to_clear.Count; c++)
                cards_to_clear[c].Clear();

            cards_to_clear.Clear();

            DiffAndFireStatEvents();

            Profiler.EndSample();
        }

        // Fires onCardStatChange only when the displayed stat (GetAttack/GetHP/GetRange) actually changed.
        // Covers damage, one-shot buffs, ongoing recompute — silent on no-op recompute.
        protected virtual void DiffAndFireStatEvents()
        {
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player pl = game_data.players[p];
                CheckCardStatDelta(pl.hero);
                for (int c = 0; c < pl.cards_board.Count; c++)   CheckCardStatDelta(pl.cards_board[c]);
                for (int c = 0; c < pl.cards_equip.Count; c++)   CheckCardStatDelta(pl.cards_equip[c]);
                for (int c = 0; c < pl.cards_attach.Count; c++)  CheckCardStatDelta(pl.cards_attach[c]);
                for (int c = 0; c < pl.cards_hand.Count; c++)    CheckCardStatDelta(pl.cards_hand[c]);
                for (int c = 0; c < pl.cards_club.Count; c++)    CheckCardStatDelta(pl.cards_club[c]);
                for (int c = 0; c < pl.player_ability.Count; c++) CheckCardStatDelta(pl.player_ability[c]);
            }
        }

        protected virtual void CheckCardStatDelta(Card c)
        {
            if (c == null) return;

            int a = c.GetAttack();
            int h = c.GetHP();
            int r = c.GetRange();

            if (!c.stat_tracking_initialized)
            {
                c.prev_attack = a;
                c.prev_hp = h;
                c.prev_range = r;
                c.stat_tracking_initialized = true;
                return;
            }

            if (a != c.prev_attack) { c.prev_attack = a; onCardStatChange?.Invoke(c, EffectStatType.Attack); }
            if (h != c.prev_hp)     { c.prev_hp = h;     onCardStatChange?.Invoke(c, EffectStatType.HP); }
            if (r != c.prev_range)  { c.prev_range = r;  onCardStatChange?.Invoke(c, EffectStatType.Range); }
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

                    if (ability.criteria_target == AbilityTarget.Club)
                    {
                        //Buff the caster owner's club card(s); ongoing so it is wiped/recomputed each
                        //cycle and naturally drops when the granter (card) is silenced.
                        foreach (Card club in player.cards_club)
                        {
                            if (ability.AreCriteriaTargetConditionsMet(game_data, card, club))
                                ability.DoOngoingEffects(this, card, club);
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
                game_data.selector_target_card_uid = target.uid;

                if (ability.trigger == AbilityTrigger.OnPlay)
                    PlayCard(caster, game_data.selector_caster_slot);
                else
                {
                    //Phase: effects applied outside Resolve(), so open the depth-first scope manually
                    resolve_queue.BeginPhase();
                    ResolveEffectTarget(ability, caster, target);
                    AfterAbilityResolved(ability, caster, triggerer, game_data.selector_max_repeat, game_data.selector_current_repeat);
                    resolve_queue.EndPhase();
                    resolve_queue.ResolveAll();
                }
            }

            if (game_data.selector == SelectorType.SelectorCard)
            {
                if (!ability.IsCardSelectionValid(game_data, caster, target, card_array))
                    return; //Supports conditions and filters

                game_data.selector = SelectorType.None;
                game_data.selector_target_card_uid = target.uid;

                resolve_queue.BeginPhase();
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster, triggerer, game_data.selector_max_repeat, game_data.selector_current_repeat);
                resolve_queue.EndPhase();
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
                game_data.selector_target_player = target;

                if (ability.trigger == AbilityTrigger.OnPlay)
                    PlayCard(caster, game_data.selector_caster_slot);
                else
                {
                    resolve_queue.BeginPhase();
                    ResolveEffectTarget(ability, caster, target);
                    AfterAbilityResolved(ability, caster, triggerer, game_data.selector_max_repeat, game_data.selector_current_repeat);
                    resolve_queue.EndPhase();
                    resolve_queue.ResolveAll();
                }
            }
        }

        public virtual void SelectSlot(Slot target)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            Card triggerer = game_data.GetCard(game_data.selector_triggerer_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || ability == null || !target.IsValid())
                return;
            
            //if (ability.target == AbilityTarget.SelectCard)
            //    return;

            if (game_data.selector == SelectorType.SelectTarget)
            {
                if (!ability.CanTarget(game_data, caster, target))
                    return; //Conditions not met


                Player player = game_data.GetPlayer(caster.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.CastAbility, caster, ability, target);

                game_data.selector = SelectorType.None;
                game_data.selector_target_slot = target;

                if (ability.trigger == AbilityTrigger.OnPlay)
                    PlayCard(caster, game_data.selector_caster_slot);
                else
                {
                    List<Slot> targets = Slot.GetAll();

                    resolve_queue.BeginPhase();
                    foreach (Slot targ in targets)
                    {
                        if (!ability.AreWideRangeConditionsMet(game_data, caster, target, targ))
                            continue;

                        if (ability.AreTargetConditionsMet(game_data, caster, targ))
                        ResolveEffectTarget(ability, caster, targ);
                    }

                    AfterAbilityResolved(ability, caster, triggerer, game_data.selector_max_repeat, game_data.selector_current_repeat);
                    resolve_queue.EndPhase();
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
                        resolve_queue.BeginPhase();
                        AfterAbilityResolved(ability, caster, triggerer, game_data.selector_max_repeat, game_data.selector_current_repeat);
                        ResolveCardAbility(achoice, caster, caster, achoice.GetMaxRepeatTimes(game_data, caster), 0);
                        resolve_queue.EndPhase();
                        resolve_queue.ResolveAll();
                    }
                }
            }
        }

        public virtual void CancelSelection()
        {
            if (game_data.selector != SelectorType.None)
            {
                AbilityData iability = AbilityData.Get(game_data.selector_ability_id);
                if (iability != null && iability.trigger == AbilityTrigger.OnPlay)
                    CancelPlayCard();

                //취소 = 소환도 어빌리티도 없던 일로 한다. 중단돼 있던 Phase 스코프를 버리지 않으면
                //다음에 호출되는 아무 BeginPhase()가 그 스코프를 물려받아, 무관한 이벤트의 트리거가
                //취소된 어빌리티의 자식으로 들어간다 (최상위 Phase가 못 되어 사이의 사망 처리가 밀린다).
                resolve_queue.DiscardSuspendedScope();

                //End selection
                game_data.selector = SelectorType.None;
                RefreshData();

                //selector가 풀렸으니 남아 있던 시퀀스를 이어서 굴린다. (취소 전에는 CanResolve가
                //selector 때문에 false라 큐가 멈춰 있었다.)
                resolve_queue.ResolveAll();
            }
        }

        public void CancelPlayCard()
        {
            Card card = game_data.GetCard(game_data.selector_caster_uid);
            if (card != null)
            {
                Player player = game_data.GetPlayer(card.player_id);

                player.RemoveCardFromAllGroups(card);
                player.AddCard(player.cards_hand, card, game_data.selector_hand_index);
                //card.Clear();
            }
        }

        public virtual void Mulligan(Player player, string[] cards)
        {
            if (game_data.phase == GamePhase.Mulligan && !player.ready)
            {
                //Replace each mulliganed card with a freshly drawn one AT THE SAME hand index, so the new
                //card occupies the slot of the card it replaced (the hand keeps its order/positions).
                for (int i = 0; i < player.cards_hand.Count; i++)
                {
                    Card card = player.cards_hand[i];
                    if (cards.Contains(card.uid) && player.cards_deck.Count > 0)
                    {
                        Card new_card = player.cards_deck[0];
                        player.cards_deck.RemoveAt(0);
                        player.cards_hand[i] = new_card;   //new card takes the removed card's slot
                        player.cards_deck.Add(card);       //mulliganed card returns to the deck (shuffled below)
                    }
                }

                ShuffleDeck(player.cards_deck);

                player.ready = true;
                RefreshData();

                onMulligan?.Invoke(player.player_id);
                if (game_data.AreAllPlayersReady())
                {
                    //Buffer covers the client-side mulligan->hand handoff animation
                    //before the mulligan panel closes and the first turn begins.
                    resolve_queue.AddCallback(StartFirstTurn);
                    resolve_queue.ResolveAll(GameConfig.Timing.mulligan_to_turn);
                    //StartTurn();
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
            //pending_repeats lives on this GameLogic, not on Game data: the AI reuses one GameLogic
            //across many cloned games, so entries stranded by an interrupted resolve (selector/game end)
            //would fire on the next clone with cards from another game. Real matches never call this mid-game.
            pending_repeats.Clear();
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