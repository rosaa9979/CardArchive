using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Unity.Netcode;
using System.Threading.Tasks;

namespace TcgEngine.Client
{
    /// <summary>
    /// Main script for the client-side of the game, should be in game scene only
    /// Will connect to server, then connect to the game on that server (with uid) and then will send game settings
    /// During the game, will send all actions performed by the player and receive game refreshes
    /// </summary>

    public partial class GameClient : MonoBehaviour
    {
        //--- These settings are set in the menu scene and when the game start will be sent to server

        public static GameSettings game_settings = GameSettings.Default;
        public static PlayerSettings player_settings = PlayerSettings.Default;
        public static PlayerSettings ai_settings = PlayerSettings.DefaultAI;
        public static string observe_user = null; //Which user should it observe, null if not an obs

        //-----

        public UnityAction onConnectServer;
        public UnityAction onConnectGame;
        public UnityAction<int> onPlayerReady;

        public UnityAction onGameStart;
        public UnityAction<int> onGameEnd;              //winner player_id
        public UnityAction<int> onNewTurn;              //current player_id
        public UnityAction<int> onAttackPhase;
        public UnityAction<int> onMulligan;

        public UnityAction<Card, Slot> onCardPlayed;
        public UnityAction<Card, int> onCardDissolved; //Card, owner player_id
        public UnityAction<Card, Slot> onCardMoved;
        public UnityAction<Slot> onCardSummoned;
        public UnityAction<Card> onCardTransformed;
        public UnityAction<Card> onCardDiscarded;
        public UnityAction<int> onCardDraw;
        public UnityAction<int, int> onExhaustDamage; //player_id, damage
        public UnityAction<int> onValueRolled;
        public UnityAction<Card, EffectStatType> onCardStatChange;

        public UnityAction<AbilityData, Card> onAbilityStart;
        public UnityAction<AbilityData, Card, Card> onAbilityTargetCard;      //Ability, Caster, Target
        public UnityAction<AbilityData, Card, Player> onAbilityTargetPlayer;
        public UnityAction<AbilityData, Card, Slot> onAbilityTargetSlot;
        public UnityAction<AbilityData, Card> onAbilityEnd;
        public UnityAction<Card, Card> onSecretTrigger;    //Secret, Triggerer
        public UnityAction<Card, Card> onSecretResolve;    //Secret, Triggerer

        public UnityAction<Card, Card> onAttackStart;   //Attacker, Defender
        public UnityAction<Card, Card> onAttackHit;
        public UnityAction<Card, Card, int> onCardDamaged; //Attacker (null if none), Target, applied damage
        public UnityAction<Card, Player, int> onPlayerDamaged; //Attacker, Target player, applied damage
        public UnityAction<Card, Card> onAttackEvade;
        public UnityAction<Card, Card> onAttackEnd;     //Attacker, Defender
        public UnityAction<Card, Player> onAttackPlayerStart;
        public UnityAction<Card, Player> onAttackPlayerHit;
        public UnityAction<Card, Player> onAttackPlayerEnd;

        public UnityAction<int, string> onChatMsg;  //player_id, msg
        public UnityAction< string> onServerMsg;  //msg
        public UnityAction onRefreshAll;

        private int player_id = 0; //Player playing on this device;
        private Game game_data => presentationState.Displayed;

        private bool observe_mode = false;
        private int observe_player_id = 0;
        private float timer = 0f;


        private Dictionary<ushort, RefreshEvent> registered_commands = new Dictionary<ushort, RefreshEvent>();

        private static GameClient instance;

        protected virtual void Awake()
        {
            instance = this;
            PresentationLoopClock.ResetSession();
            presentationState.Changed += OnPresentationStateChanged;
            Application.targetFrameRate = 120;
        }

        protected virtual void Start()
        {
            RegisterRefresh(GameAction.Presentation, OnPresentation);
            RegisterRefresh(GameAction.PresentationGate, OnPresentationGate);
            RegisterRefresh(GameAction.ActionResult, OnActionResult);
            RegisterRefresh(GameAction.Connected, OnConnectedToGame);
            RegisterRefresh(GameAction.PlayerReady, OnPlayerReady);
            RegisterRefresh(GameAction.GameStart, OnGameStart);
            RegisterRefresh(GameAction.GameEnd, OnGameEnd);
            RegisterRefresh(GameAction.NewTurn, OnNewTurn);
            RegisterRefresh(GameAction.AttackPhase, OnAttackPhase);
            RegisterRefresh(GameAction.Mulligan, OnMulligan);
            RegisterRefresh(GameAction.CardPlayed, OnCardPlayed);
            RegisterRefresh(GameAction.CardMoved, OnCardMoved);
            RegisterRefresh(GameAction.CardSummoned, OnCardSummoned);
            RegisterRefresh(GameAction.CardTransformed, OnCardTransformed);
            RegisterRefresh(GameAction.CardDiscarded, OnCardDiscarded);
            RegisterRefresh(GameAction.CardDissolved, OnCardDissolved);
            RegisterRefresh(GameAction.CardDrawn, OnCardDraw);
            RegisterRefresh(GameAction.ExhaustDamage, OnExhaustDamage);
            RegisterRefresh(GameAction.ValueRolled, OnValueRolled);
            RegisterRefresh(GameAction.CardStatChange, OnCardStatChange);

            RegisterRefresh(GameAction.AttackStart, OnAttackStart);
            RegisterRefresh(GameAction.AttackHit, OnAttackHit);
            RegisterRefresh(GameAction.CardDamaged, OnCardDamaged);
            RegisterRefresh(GameAction.PlayerDamaged, OnPlayerDamaged);
            RegisterRefresh(GameAction.AttackEvade, OnAttackEvade);
            RegisterRefresh(GameAction.AttackEnd, OnAttackEnd);
            RegisterRefresh(GameAction.AttackPlayerStart, OnAttackPlayerStart);
            RegisterRefresh(GameAction.AttackPlayerHit, OnAttackPlayerHit);
            RegisterRefresh(GameAction.AttackPlayerEnd, OnAttackPlayerEnd);

            RegisterRefresh(GameAction.AbilityTrigger, OnAbilityTrigger);
            RegisterRefresh(GameAction.AbilityTargetCard, OnAbilityTargetCard);
            RegisterRefresh(GameAction.AbilityTargetPlayer, OnAbilityTargetPlayer);
            RegisterRefresh(GameAction.AbilityTargetSlot, OnAbilityTargetSlot);
            RegisterRefresh(GameAction.AbilityEnd, OnAbilityAfter);

            RegisterRefresh(GameAction.SecretTriggered, OnSecretTrigger);
            RegisterRefresh(GameAction.SecretResolved, OnSecretResolve);

            RegisterRefresh(GameAction.ChatMessage, OnChat);
            RegisterRefresh(GameAction.ServerMessage, OnServerMsg);

            if (TcgEngine.Replay.ReplaySession.Active)
            {
                player_id = TcgEngine.Replay.ReplaySession.PlayerId;
                observe_player_id = player_id;
                observe_mode = true;
                ResetPresentation(TcgEngine.Replay.ReplaySession.Record.initialState);
                gameObject.AddComponent<TcgEngine.Replay.ReplayDirector>();
                return;
            }
            TcgNetwork.Get().onConnect += OnConnectedServer;
            TcgNetwork.Get().Messaging.ListenMsg("refresh", OnReceiveRefresh);

            ConnectToAPI();
            ConnectToServer();
        }

        protected virtual void OnDestroy()
        {
            CancelPresentationStep();
            presentationState.Changed -= OnPresentationStateChanged;
            PresentationClock.Reset();
            PresentationLoopClock.ResetSession();
            TcgNetwork.Get().onConnect -= OnConnectedServer;
            TcgNetwork.Get().Messaging.UnListenMsg("refresh");
        }

        protected virtual void Update()
        {
            if (TcgEngine.Replay.ReplaySession.Active) return;
            TryAcknowledgePresentation();
            bool is_starting = game_data == null || game_data.state == GameState.Connecting;
            bool is_client = !game_settings.IsHost();
            bool is_connecting = TcgNetwork.Get().IsConnecting();
            bool is_connected = TcgNetwork.Get().IsConnected();

            //Exit game scene if cannot connect after a while
            if (is_starting && is_client)
            {
                timer += Time.deltaTime;
                if (timer > 10f)
                {
                    SceneNav.GoTo("Menu");
                }
            }

            //Reconnect to server
            if (!is_starting && !is_connecting && is_client && !is_connected)
            {
                timer += Time.deltaTime;
                if (timer > 5f)
                {
                    timer = 0f;
                    ConnectToServer();
                }
            }
        }

        //--------------------

        public virtual void ConnectToAPI()
        {
            //Should already be logged in from the menu
            //If not connected, start in test mode (this means game scene was launched directly from Unity)
            if (!Authenticator.Get().IsSignedIn())
            {
                Authenticator.Get().LoginTest("Player");

                if (!player_settings.HasDeck())
                {
                    player_settings.deck = new UserDeckData(GameplayData.Get().test_deck);
                }

                if (!ai_settings.HasDeck())
                {
                    ai_settings.deck = new UserDeckData(GameplayData.Get().test_deck_ai);
                    ai_settings.ai_level = GameplayData.Get().ai_level;
                }
            }

            //Set avatar, cardback based on your api data
            UserData udata = Authenticator.Get().UserData;
            if (udata != null)
            {
                player_settings.avatar = udata.GetAvatar();
                player_settings.cardback = udata.GetCardback();
            }
        }

        public virtual async void ConnectToServer()
        {
            await Task.Yield(); //Wait for initialization to finish

            if (TcgNetwork.Get().IsActive())
                return; // Already connected

#if UNITY_EDITOR
            if (TcgEngine.EffectTestPanel.Active)
            {
                TcgNetwork.Get().StartHostOffline();
                return;
            }
#endif

            if (game_settings.IsHost() && NetworkData.Get().solo_type == SoloType.Offline)
            {
                TcgNetwork.Get().StartHostOffline();    //WebGL dont support hosting a game, must join a dedicated server, in solo it starts a offline mode that doesn't use netcode at all
            }
            else if (game_settings.IsHost())
            {
                TcgNetwork.Get().StartHost(NetworkData.Get().port);       //Host a game, either solo or for P2P, still using netcode in solo to have consistant behavior when testing solo vs multi
            }
            else
            {
                TcgNetwork.Get().StartClient(game_settings.GetUrl(), NetworkData.Get().port);       //Join server
            }
        }

        public virtual async void ConnectToGame(string uid)
        {
            await Task.Yield(); //Wait for initialization to finish

            if (!TcgNetwork.Get().IsActive())
                return; //Not connected to server

            Debug.Log("Connect to Game: " + uid);

            MsgPlayerConnect nplayer = new MsgPlayerConnect();
            nplayer.user_id = Authenticator.Get().UserID;
            nplayer.username = Authenticator.Get().Username;
            nplayer.game_uid = uid;
            nplayer.nb_players = game_settings.nb_players;
            nplayer.observer = game_settings.game_type == GameType.Observer;

            Messaging.SendObject("connect", ServerID, nplayer, NetworkDelivery.Reliable);
        }

        public virtual void SendGameSettings()
        {
            if (game_settings.IsOffline())
            {
                //Solo mode, send both your settings and AI settings
                SendGameplaySettings(game_settings);
                SendPlayerSettingsAI(ai_settings);
                SendPlayerSettings(player_settings);
            }
            else
            {
                //Online mode, only send your own settings
                SendGameplaySettings(game_settings);
                SendPlayerSettings(player_settings);
            }
        }

        public virtual void Disconnect()
        {
            if (TcgEngine.Replay.ReplaySession.Active) return;
            TcgNetwork.Get().Disconnect();
        }

        private void RegisterRefresh(ushort tag, UnityAction<SerializedData> callback)
        {
            RefreshEvent cmdevt = new RefreshEvent();
            cmdevt.tag = tag;
            cmdevt.callback = callback;
            registered_commands.Add(tag, cmdevt);
        }

        public void OnReceiveRefresh(ulong client_id, FastBufferReader reader)
        {
            reader.ReadValueSafe(out ushort type);
            if (type != GameAction.Presentation && type != GameAction.PresentationGate
                && type != GameAction.ActionResult && type != GameAction.Connected
                && type != GameAction.PlayerReady && type != GameAction.ChatMessage
                && type != GameAction.ServerMessage) return;
            bool found = registered_commands.TryGetValue(type, out RefreshEvent command);
            if (found)
            {
                command.callback.Invoke(new SerializedData(reader));
            }
        }

        //--------------------------

        public void SendPlayerSettings(PlayerSettings psettings)
        {
            SendAction(GameAction.PlayerSettings, psettings, NetworkDelivery.ReliableFragmentedSequenced);
        }

        public void SendPlayerSettingsAI(PlayerSettings psettings)
        {
            SendAction(GameAction.PlayerSettingsAI, psettings, NetworkDelivery.ReliableFragmentedSequenced);
        }

        public void SendGameplaySettings(GameSettings settings)
        {
            SendAction(GameAction.GameSettings, settings, NetworkDelivery.ReliableFragmentedSequenced);
        }

        public void PlayCard(Card card, Slot slot)
        {
            if (card == null || !IsInputOpen || cardPlays.IsPending(card.uid)) return;
            Card latest = GetLatestGameData().GetCard(card.uid);
            if (!GetLatestGameData().CanPlayCard(latest, slot)) return;
            MsgPlayRequest mdata = new MsgPlayRequest();
            mdata.card_uid = card.uid;
            mdata.slot = slot;
            Card visibleTarget = game_data.GetSlotCard(slot);
            if (card.CardData.IsRequireTarget() && visibleTarget != null)
            {
                if (!CanUseBoardCard(visibleTarget.uid)) return;
                mdata.target_uid = visibleTarget.uid;
            }
            mdata.request_id = cardPlays.Begin(card.uid);
            SendAction(GameAction.PlayCard, mdata);
        }

        public void AttackTarget(Card card, Card target)
        {
            //Manual attacks are disabled: attacks only happen automatically during the attack phase.
            //Kept for reference; the send path below is intentionally blocked at the start.
            return;
            //MsgAttack mdata = new MsgAttack();
            //mdata.attacker_uid = card.uid;
            //mdata.target_uid = target.uid;
            //SendAction(GameAction.Attack, mdata);
        }

        public void AttackPlayer(Card card, Player target)
        {
            //Manual attacks are disabled: attacks only happen automatically during the attack phase.
            //Kept for reference; the send path below is intentionally blocked at the start.
            return;
            //MsgAttackPlayer mdata = new MsgAttackPlayer();
            //mdata.attacker_uid = card.uid;
            //mdata.target_id = target.player_id;
            //SendAction(GameAction.AttackPlayer, mdata);
        }

        public void Move(Card card, Slot slot)
        {
            MsgPlayCard mdata = new MsgPlayCard();
            mdata.card_uid = card.uid;
            mdata.slot = slot;
            SendAction(GameAction.Move, mdata);
        }

        public void CastAbility(Card card, AbilityData ability)
        {
            MsgCastAbility mdata = new MsgCastAbility();
            mdata.caster_uid = card.uid;
            mdata.ability_id = ability.id;
            mdata.target_uid = "";
            SendAction(GameAction.CastAbility, mdata);
        }

        public void SelectCard(Card card)
        {
            if (card == null) return;
            if (game_data.IsOnBoard(card) && !CanUseBoardCard(card.uid)) return;
            if (game_data.selector_version != GetLatestGameData().selector_version) return;
            MsgCard mdata = new MsgCard();
            mdata.card_uid = card.uid;
            SendAction(GameAction.SelectCard, mdata);
        }

        public void SelectPlayer(Player player)
        {
            if (game_data.selector_version != GetLatestGameData().selector_version) return;
            MsgPlayer mdata = new MsgPlayer();
            mdata.player_id = player.player_id;
            SendAction(GameAction.SelectPlayer, mdata);
        }

        public void SelectSlot(Slot slot, string targetUid = "")
        {
            if (!string.IsNullOrEmpty(targetUid) && !CanUseBoardCard(targetUid)) return;
            if (game_data.selector_version != GetLatestGameData().selector_version) return;
            SendAction(GameAction.SelectSlot, new MsgSelectSlotRequest { slot = slot, target_uid = targetUid });
        }

        public void SelectChoice(int c)
        {
            if (game_data.selector_version != GetLatestGameData().selector_version) return;
            MsgInt choice = new MsgInt();
            choice.value = c;
            SendAction(GameAction.SelectChoice, choice);
        }

        public void Mulligan(string[] cards)
        {
            MsgMulligan mdata = new MsgMulligan();
            mdata.cards = cards;
            SendAction(GameAction.SelectMulligan, mdata);
        }

        public void CancelSelection()
        {
            if (game_data.selector_version != GetLatestGameData().selector_version) return;
            SendAction(GameAction.CancelSelect);
        }

        public void SendChatMsg(string msg)
        {
            MsgChat chat = new MsgChat();
            chat.msg = msg;
            chat.player_id = player_id;
            SendAction(GameAction.ChatMessage, chat);
        }

        public void EndTurn()
        {
            SendAction(GameAction.EndTurn);
        }

        public void Resign()
        {
            SendAction(GameAction.Resign);
        }

        public void SetObserverMode(int player_id)
        {
            if (TcgEngine.Replay.ReplaySession.Active) return;
            observe_mode = true;
            observe_player_id = player_id;
        }

        public void SetObserverMode(string username)
        {
            if (TcgEngine.Replay.ReplaySession.Active) return;
            observe_player_id = 0; //Default value of observe_user not found

            Game data = GetGameData();
            foreach (Player player in data.players)
            {
                if (player.username == username)
                {
                    observe_player_id = player.player_id;
                }
            }
        }

        public void SendAction<T>(ushort type, T data, NetworkDelivery delivery = NetworkDelivery.Reliable) where T : INetworkSerializable
        {
            if (TcgEngine.Replay.ReplaySession.Active || !CanSendAction(type)) return;
            FastBufferWriter writer = new FastBufferWriter(128, Unity.Collections.Allocator.Temp, TcgNetwork.MsgSizeMax);
            writer.WriteValueSafe(type);
            writer.WriteValueSafe(inputEpoch);
            writer.WriteValueSafe(GetLatestGameData()?.selector_version ?? 0L);
            writer.WriteNetworkSerializable(data);
            Messaging.Send("action", ServerID, writer, NetworkDelivery.ReliableFragmentedSequenced);
            writer.Dispose();
        }

        public void SendAction(ushort type, int data)
        {
            if (TcgEngine.Replay.ReplaySession.Active || !CanSendAction(type)) return;
            FastBufferWriter writer = new FastBufferWriter(128, Unity.Collections.Allocator.Temp, TcgNetwork.MsgSizeMax);
            writer.WriteValueSafe(type);
            writer.WriteValueSafe(inputEpoch);
            writer.WriteValueSafe(GetLatestGameData()?.selector_version ?? 0L);
            writer.WriteValueSafe(data);
            Messaging.Send("action", ServerID, writer, NetworkDelivery.ReliableFragmentedSequenced);
            writer.Dispose();
        }

        public void SendAction(ushort type)
        {
            if (TcgEngine.Replay.ReplaySession.Active || !CanSendAction(type)) return;
            FastBufferWriter writer = new FastBufferWriter(128, Unity.Collections.Allocator.Temp, TcgNetwork.MsgSizeMax);
            writer.WriteValueSafe(type);
            writer.WriteValueSafe(inputEpoch);
            writer.WriteValueSafe(GetLatestGameData()?.selector_version ?? 0L);
            Messaging.Send("action", ServerID, writer, NetworkDelivery.ReliableFragmentedSequenced);
            writer.Dispose();
        }

        //--- Receive Refresh ----------------------

        protected virtual void OnConnectedServer()
        {
            ConnectToGame(game_settings.game_uid);
            onConnectServer?.Invoke();
        }

        protected virtual void OnConnectedToGame(SerializedData sdata)
        {
            MsgAfterConnected msg = sdata.Get<MsgAfterConnected>();
            player_id = msg.player_id;
            connectNotificationPending = true;
            if (!presentationReady) ResetPresentation(TcgEngine.Replay.ReplayStateCodec.Capture(msg.game_data));
            observe_mode = player_id < 0; //Will usually return -1 if its an observer

            acknowledgedBarrier = -1;
            cardPlays.Clear();
            directCardPlays.Clear();
            RequestPresentationSync();
            SendGameSettings();
        }

        protected virtual void OnPlayerReady(SerializedData sdata)
        {
            MsgInt msg = sdata.Get<MsgInt>();
            int pid = msg.value;

            if (onPlayerReady != null)
                onPlayerReady.Invoke(pid);
        }

        private void OnGameStart(SerializedData sdata)
        {
            directCardPlays.Clear();
            // Preparation UI reads only the published first-player/phase snapshot.
            AfterPresentationCommit(() => onGameStart?.Invoke());
        }

        private void OnGameEnd(SerializedData sdata)
        {
            MsgPlayer msg = sdata.Get<MsgPlayer>();
            AfterPresentationCommit(() => onGameEnd?.Invoke(msg.player_id));
        }

        private void OnNewTurn(SerializedData sdata)
        {
            MsgPlayer msg = sdata.Get<MsgPlayer>();
            onNewTurn?.Invoke(msg.player_id);
        }

        private void OnAttackPhase(SerializedData sdata)
        {
            MsgPlayer msg = sdata.Get<MsgPlayer>();
            onAttackPhase?.Invoke(msg.player_id);
        }

        private void OnMulligan(SerializedData sdata)
        {
            MsgPlayer msg = sdata.Get<MsgPlayer>();
            int player_id = msg.player_id;
            // Swap/handoff UI must inspect the confirmed replacement hand and ready flags.
            AfterPresentationCommit(() => onMulligan?.Invoke(player_id));
        }

        private void OnCardPlayed(SerializedData sdata)
        {
            MsgCardPlayed msg = sdata.Get<MsgCardPlayed>();
            Card card = EventData.GetCard(msg.card_uid);
            if (card != null)
            {
                if (msg.automatic) directCardPlays.Remove(card.uid);
                else directCardPlays[card.uid] = card.play_order;
            }
            onCardPlayed?.Invoke(card, msg.slot);
        }

        private void OnCardSummoned(SerializedData sdata)
        {
            MsgPlayCard msg = sdata.Get<MsgPlayCard>();
            onCardSummoned?.Invoke(msg.slot);
        }

        private void OnCardMoved(SerializedData sdata)
        {
            MsgPlayCard msg = sdata.Get<MsgPlayCard>();
            Card card = EventData.GetCard(msg.card_uid);
            onCardMoved?.Invoke(card, msg.slot);
        }

        private void OnCardTransformed(SerializedData sdata)
        {
            MsgCard msg = sdata.Get<MsgCard>();
            Card card = EventData.GetCard(msg.card_uid);
            onCardTransformed?.Invoke(card);
        }

        private void OnCardDiscarded(SerializedData sdata)
        {
            MsgCard msg = sdata.Get<MsgCard>();
            Card card = EventData.GetCard(msg.card_uid);
            onCardDiscarded?.Invoke(card);
        }

        private void OnCardDissolved(SerializedData sdata)
        {
            MsgDissolveCard msg = sdata.Get<MsgDissolveCard>();
            Card card = EventData.GetCard(msg.card_uid);
            onCardDissolved?.Invoke(card, msg.player_id);
        }

        private void OnCardDraw(SerializedData sdata)
        {
            MsgInt msg = sdata.Get<MsgInt>();
            AfterPresentationCommit(() => onCardDraw?.Invoke(msg.value));
        }

        private void OnExhaustDamage(SerializedData sdata)
        {
            MsgPlayerValue msg = sdata.Get<MsgPlayerValue>();
            onExhaustDamage?.Invoke(msg.player_id, msg.value);
        }

        private void OnValueRolled(SerializedData sdata)
        {
            MsgInt msg = sdata.Get<MsgInt>();
            onValueRolled?.Invoke(msg.value);
        }

        private void OnCardStatChange(SerializedData sdata)
        {
            MsgStat msg = sdata.Get<MsgStat>();
            Card card = EventData.GetCard(msg.card_uid);
            AfterPresentationCommit(() => onCardStatChange?.Invoke(card, msg.type));
        }

        private void OnAttackStart(SerializedData sdata)
        {
            MsgAttack msg = sdata.Get<MsgAttack>();
            Card attacker = EventData.GetCard(msg.attacker_uid);
            Card target = EventData.GetCard(msg.target_uid);
            onAttackStart?.Invoke(attacker, target);
        }

        private void OnAttackHit(SerializedData sdata)
        {
            MsgAttack msg = sdata.Get<MsgAttack>();
            Card attacker = EventData.GetCard(msg.attacker_uid);
            Card target = EventData.GetCard(msg.target_uid);
            onAttackHit?.Invoke(attacker, target);
        }

        private void OnCardDamaged(SerializedData sdata)
        {
            MsgAttack msg = sdata.Get<MsgAttack>();
            Card attacker = EventData.GetCard(msg.attacker_uid);
            Card target = EventData.GetCard(msg.target_uid);
            if (target != null) AfterPresentationCommit(() => onCardDamaged?.Invoke(attacker, target, msg.damage));
        }

        private void OnPlayerDamaged(SerializedData sdata)
        {
            MsgAttackPlayer msg = sdata.Get<MsgAttackPlayer>();
            Card attacker = EventData.GetCard(msg.attacker_uid);
            Player target = EventData.GetPlayer(msg.target_id);
            if (target != null) AfterPresentationCommit(() => onPlayerDamaged?.Invoke(attacker, target, msg.damage));
        }

        private void OnAttackEvade(SerializedData sdata)
        {
            MsgAttack msg = sdata.Get<MsgAttack>();
            Card attacker = EventData.GetCard(msg.attacker_uid);
            Card target = EventData.GetCard(msg.target_uid);
            onAttackEvade?.Invoke(attacker, target);
        }

        private void OnAttackEnd(SerializedData sdata)
        {
            MsgAttack msg = sdata.Get<MsgAttack>();
            Card attacker = EventData.GetCard(msg.attacker_uid);
            Card target = EventData.GetCard(msg.target_uid);
            onAttackEnd?.Invoke(attacker, target);
        }

        private void OnAttackPlayerStart(SerializedData sdata)
        {
            MsgAttackPlayer msg = sdata.Get<MsgAttackPlayer>();
            Card attacker = EventData.GetCard(msg.attacker_uid);
            Player target = EventData.GetPlayer(msg.target_id);
            onAttackPlayerStart?.Invoke(attacker, target);
        }

        private void OnAttackPlayerHit(SerializedData sdata)
        {
            MsgAttackPlayer msg = sdata.Get<MsgAttackPlayer>();
            Card attacker = EventData.GetCard(msg.attacker_uid);
            Player target = EventData.GetPlayer(msg.target_id);
            onAttackPlayerHit?.Invoke(attacker, target);
        }

        private void OnAttackPlayerEnd(SerializedData sdata)
        {
            MsgAttackPlayer msg = sdata.Get<MsgAttackPlayer>();
            Card attacker = EventData.GetCard(msg.attacker_uid);
            Player target = EventData.GetPlayer(msg.target_id);
            onAttackPlayerEnd?.Invoke(attacker, target);
        }

        private void OnAbilityTrigger(SerializedData sdata)
        {
            MsgCastAbility msg = sdata.Get<MsgCastAbility>();
            AbilityData ability = AbilityData.Get(msg.ability_id);
            Card caster = EventData.GetCard(msg.caster_uid);
            onAbilityStart?.Invoke(ability, caster);
        }

        private void OnAbilityTargetCard(SerializedData sdata)
        {
            MsgCastAbility msg = sdata.Get<MsgCastAbility>();
            AbilityData ability = AbilityData.Get(msg.ability_id);
            Card caster = EventData.GetCard(msg.caster_uid);
            Card target = EventData.GetCard(msg.target_uid);
            PresentAbilityTarget(ability, caster, target != null ? CardPosition(target) : Vector3.zero);
            AfterPresentationCommit(() => onAbilityTargetCard?.Invoke(ability, caster, target));
        }

        private void OnAbilityTargetPlayer(SerializedData sdata)
        {
            MsgCastAbilityPlayer msg = sdata.Get<MsgCastAbilityPlayer>();
            AbilityData ability = AbilityData.Get(msg.ability_id);
            Card caster = EventData.GetCard(msg.caster_uid);
            Player target = EventData.GetPlayer(msg.target_id);
            BoardSlotPlayer zone = target != null ? BoardSlotPlayer.Get(target.player_id != GetPlayerID()) : null;
            if (zone != null) PresentAbilityTarget(ability, caster, zone.transform.position);
            AfterPresentationCommit(() => onAbilityTargetPlayer?.Invoke(ability, caster, target));
        }

        private void OnAbilityTargetSlot(SerializedData sdata)
        {
            MsgCastAbilitySlot msg = sdata.Get<MsgCastAbilitySlot>();
            AbilityData ability = AbilityData.Get(msg.ability_id);
            Card caster = EventData.GetCard(msg.caster_uid);
            PresentAbilityTarget(ability, caster, SlotPosition(msg.slot));
            AfterPresentationCommit(() => onAbilityTargetSlot?.Invoke(ability, caster, msg.slot));
        }

        private void OnAbilityAfter(SerializedData sdata)
        {
            MsgCastAbility msg = sdata.Get<MsgCastAbility>();
            AbilityData ability = AbilityData.Get(msg.ability_id);
            Card caster = EventData.GetCard(msg.caster_uid);
            onAbilityEnd?.Invoke(ability, caster);
        }

        private void OnSecretTrigger(SerializedData sdata)
        {
            MsgSecret msg = sdata.Get<MsgSecret>();
            Card secret = EventData.GetCard(msg.secret_uid);
            Card triggerer = EventData.GetCard(msg.triggerer_uid);
            onSecretTrigger?.Invoke(secret, triggerer);
        }

        private void OnSecretResolve(SerializedData sdata)
        {
            MsgSecret msg = sdata.Get<MsgSecret>();
            Card secret = EventData.GetCard(msg.secret_uid);
            Card triggerer = EventData.GetCard(msg.triggerer_uid);
            onSecretResolve?.Invoke(secret, triggerer);
        }

        private void OnChat(SerializedData sdata)
        {
            MsgChat msg = sdata.Get<MsgChat>();
            onChatMsg?.Invoke(msg.player_id, msg.msg);
        }

        private void OnServerMsg(SerializedData sdata)
        {
            string msg = sdata.GetString();
            onServerMsg?.Invoke(msg);
        }

        //--------------------------

        public virtual bool IsReady()
        {
            return game_data != null && (TcgEngine.Replay.ReplaySession.Active || TcgNetwork.Get().IsConnected());
        }

        public Player GetPlayer()
        {
            Game gdata = GetGameData();
            return gdata.GetPlayer(GetPlayerID());
        }

        public Player GetOpponentPlayer()
        {
            Game gdata = GetGameData();
            return gdata.GetPlayer(GetOpponentPlayerID());
        }

        public int GetPlayerID()
        {
            if (observe_mode)
                return observe_player_id;
            return player_id;
        }

        public int GetOpponentPlayerID()
        {
            return GetPlayerID() == 0 ? 1 : 0;
        }

        public virtual bool IsYourTurn()
        {
            Player player = game_data?.GetPlayer(GetPlayerID());
            // Turn/selection ownership is independent of connection and presentation gates.
            return game_data != null && (game_data.phase == GamePhase.Main || game_data.selector != SelectorType.None)
                && game_data.IsPlayerTurn(player);
        }

        public bool IsObserveMode()
        {
            return observe_mode;
        }

        public Game GetGameData()
        {
            return game_data;
        }

        public bool HasEnded()
        {
            return game_data != null && game_data.HasEnded();
        }

        private void OnApplicationQuit()
        {
            Resign(); //Auto Resign before closing the app. NOTE: doesn't seem to work since the msg dont have time to be sent before it closes
        }

        public bool IsHost { get { return TcgNetwork.Get().IsHost; } }
        public ulong ServerID { get { return TcgNetwork.Get().ServerID; } }
        public NetworkMessaging Messaging { get { return TcgNetwork.Get().Messaging; } }

        public static GameClient Get()
        {
            return instance;
        }

    }

    public class RefreshEvent
    {
        public ushort tag;
        public UnityAction<SerializedData> callback;
    }
}
