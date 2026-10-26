using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;

namespace TcgEngine.UI
{
    /// <summary>
    /// Main script for the main menu scene
    /// </summary>

    public class MainMenu : MonoBehaviour
    {
        public AudioClip music;
        public AudioClip ambience;

        [Header("Player UI")]
        public Text username_txt;
        public Text credits_txt;
        public AvatarUI avatar;

        [Header("UI")]
        public Text version_text;
        //public DeckSelector deck_selector;
        public DeckSelectorPanel deck_selector_panel;
        //public DeckDisplay deck_preview;

        private bool starting = false;

        private static MainMenu instance;

        private const string load_key = "deck_audit";

        void Awake()
        {
            instance = this;

            //Set default settings
            Application.targetFrameRate = 120;
            GameClient.game_settings = GameSettings.Default;
        }

        void OnDestroy()
        {
            LoadPanel.Hide(load_key); //The global panel outlives this scene
        }

        private void Start()
        {
            BlackPanel.Get().Show(true);
            AudioTool.Get().PlayMusic("music", music);
            AudioTool.Get().PlaySFX("ambience", ambience, 0.5f, true, true);

            username_txt.text = "";
            credits_txt.text = "";
            version_text.text = "Version " + Application.version;
            deck_selector_panel.onChange += OnChangeDeck;

            if (Authenticator.Get().IsConnected())
                AfterLogin();
            else
                RefreshLogin();
        }

        void Update()
        {
            UserData udata = Authenticator.Get().UserData;
            if (udata != null)
            {
                credits_txt.text = GameUI.FormatNumber(udata.coins);
            }

            bool matchmaking = GameClientMatchmaker.Get().IsMatchmaking();
            if (MatchmakingPanel.Get().IsVisible() != matchmaking)
                MatchmakingPanel.Get().SetVisible(matchmaking);
        }

        private async void RefreshLogin()
        {
            bool success = await Authenticator.Get().RefreshLogin();
            // The request can finish after leaving this menu or stopping Play Mode.
            if (!Application.isPlaying || this == null || !isActiveAndEnabled || starting) return;
            if (success)
                AfterLogin();
            else
                SceneNav.GoTo("LoginMenu");
        }

        private void AfterLogin()
        {
            BlackPanel.Get().Hide();

            //Events
            GameClientMatchmaker matchmaker = GameClientMatchmaker.Get();
            matchmaker.onMatchmaking += OnMatchmakingDone;
            matchmaker.onMatchList += OnReceiveObserver;

            //Deck
            GameClient.player_settings.deck.tid = PlayerPrefs.GetString("tcg_deck_" + Authenticator.Get().Username, "");

            //UserData
            RefreshUserData();

            //Friend list
            //FriendPanel.Get().Show();
        }

        //Input stays blocked by the global LoadPanel until the user decks are loaded and audited
        public async void RefreshUserData()
        {
            LoadPanel.Show(load_key, "덱 확인 중...");
            try
            {
                UserData user = await Authenticator.Get().LoadUserData();
                if (!Application.isPlaying || this == null || !isActiveAndEnabled || starting) return;
                if (user != null)
                {
                    username_txt.text = user.username;
                    credits_txt.text = GameUI.FormatNumber(user.coins);

                    AvatarData avatar = AvatarData.Get(user.avatar);
                    this.avatar.SetAvatar(avatar);

                    //Decks
                    try
                    {
                        await AuditUserDecks(user);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError("Deck audit failed: " + e);
                    }
                    if (!Application.isPlaying || this == null || !isActiveAndEnabled || starting) return;
                    RefreshDeckList();
                }
            }
            finally
            {
                LoadPanel.Hide(load_key); //Every exit releases the block, even a failed load
            }
        }

        //Self check every saved deck against the current card data before the menu takes input
        private async Task AuditUserDecks(UserData user)
        {
            List<UserDeckData> repaired = user.RepairDecks(out int invalid_count);

            if (repaired.Count > 0)
            {
                if (Authenticator.Get().IsApi())
                {
                    foreach (UserDeckData deck in repaired)
                    {
                        string url = ApiClient.ServerURL + "/users/deck/" + deck.tid;
                        await ApiClient.Get().SendPostRequest(url, ApiTool.ToJson(deck));
                    }
                }
                await Authenticator.Get().SaveUserData();
            }

            if (invalid_count > 0)
                WarningText.ShowText("편성할 수 없는 덱이 " + invalid_count + "개 있습니다. 덱을 다시 편성해 주세요", 2.5f);
        }

        public void RefreshDeckList()
        {
            deck_selector_panel.RefreshDeckList();
        }

        private void OnChangeDeck(string tid)
        {
            GameClient.player_settings.deck = deck_selector_panel.GetDeck();
            PlayerPrefs.SetString("tcg_deck_" + Authenticator.Get().Username, tid);
        }

        private void OnMatchmakingDone(MatchmakingResult result)
        {
            if (result == null)
                return;

            if (result.success)
            {
                Debug.Log("Matchmaking found: " + result.success + " " + result.server_url + "/" + result.game_uid);
                StartGame(GameType.Multiplayer, result.game_uid, result.server_url);
            }
            else
            {
                MatchmakingPanel.Get().SetCount(result.players);
            }
        }

        private void OnReceiveObserver(MatchList list)
        {
            MatchListItem target = null;
            foreach (MatchListItem item in list.items)
            {
                if (item.username == GameClient.observe_user)
                    target = item;
            }

            if (target != null)
            {
                StartGame(GameType.Observer, target.game_uid, target.game_url);
            }
        }

        public void StartGame(GameType type, GameMode mode)
        {
            string uid = GameTool.GenerateRandomID();
            GameClient.game_settings.game_type = type;
            GameClient.game_settings.game_mode = mode;
            StartGame(uid); 
        }

        public void StartGame(GameType type, string game_uid, string server_url = "")
        {
            GameClient.game_settings.game_type = type;
            StartGame(game_uid, server_url);
        }

        public void StartGame(string game_uid, string server_url = "")
        {
            if (!starting)
            {
                starting = true;
                GameClient.game_settings.server_url = server_url; //Empty server_url will use the default one in NetworkData
                GameClient.game_settings.game_uid = game_uid;
                GameClientMatchmaker.Get().Disconnect();
                FadeToScene(GameClient.game_settings.GetScene());
            }
        }

        public void StartObserve(string user)
        {
            GameClient.observe_user = user;
            GameClientMatchmaker.Get().StopMatchmaking();
            GameClientMatchmaker.Get().RefreshMatchList(user);
        }

        public void StartChallenge(string user)
        {
            string self = Authenticator.Get().Username;
            if (self == user)
                return; //Cant challenge self

            string key;
            if (self.CompareTo(user) > 0)
                key = self + "-" + user;
            else
                key = user + "-" + self;

            StartMathmaking(GameMode.Casual, key);
        }

        public void StartMathmaking(GameMode mode, string group)
        {
            //The game server refuses new matches shortly before a maintenance
            LiveOpsManager maintenance = LiveOpsManager.Get();
            if (maintenance != null && maintenance.IsMatchmakingBlocked())
            {
                WarningText.ShowText("서버 점검 예정으로 매칭을 시작할 수 없습니다");
                return;
            }

            UserDeckData deck = deck_selector_panel.GetDeck();
            if (DeckSelectorPanel.CheckDeck(deck))
            {
                GameClient.game_settings.game_type = GameType.Multiplayer;
                GameClient.game_settings.game_mode = mode;
                GameClient.player_settings.deck = deck;
                GameClient.game_settings.scene = GameplayData.Get().GetRandomArena();
                GameClientMatchmaker.Get().StartMatchmaking(group, GameClient.game_settings.nb_players);
            }
        }

        public void OnClickSolo()
        {
            //if (!GameClient.player_settings.deck.IsValid())
            //    return;

            if (!Authenticator.Get().IsConnected())
            {
                FadeToScene("LoginMenu");
                return;
            }

            GameClient.game_settings.game_type = GameType.Solo;
            GameClient.game_settings.game_mode = GameMode.Casual;

            DeckSelectorPanel panel = DeckSelectorPanel.Get();
            panel.onConfirm = (deck_id) =>
            {
                GameClient.player_settings.deck.tid = deck_id;
                GameClient.ai_settings.deck.tid = GameplayData.Get().GetRandomAIDeck();
                GameClient.ai_settings.ai_level = GameplayData.Get().ai_level;
                GameClient.game_settings.scene = GameplayData.Get().GetRandomArena();
                StartGame(GameType.Solo, GameMode.Casual);
            };
            panel.Show();
        }

        public void OnClickPvP()
        {
            //if (!GameClient.player_settings.deck.IsValid())
            //    return;

            if (!Authenticator.Get().IsConnected())
            {
                FadeToScene("LoginMenu");
                return;
            }

            //StartMathmaking(GameMode.Ranked, "");

            GameClient.game_settings.game_type = GameType.Multiplayer;
            GameClient.game_settings.game_mode = GameMode.Ranked;

            DeckSelectorPanel panel = DeckSelectorPanel.Get();
            panel.onConfirm = (deck_id) =>
            {
                GameClient.player_settings.deck.tid = deck_id;
                StartMathmaking(GameMode.Ranked, "");
            };
            panel.Show();
        }

        public void OnClickAdventure()
        {
            //if (!GameClient.player_settings.deck.IsValid())
            //    return;

            AdventurePanel.Get().Show();
        }

        public void OnClickPlayCode()
        {
            //if (!GameClient.player_settings.deck.IsValid())
            //    return;

            GameClient.game_settings.game_type = GameType.Multiplayer;
            GameClient.game_settings.game_mode = GameMode.Casual;
            JoinCodePanel.Get().Show();
        }
        
        public void OnClickCancelMatch()
        {
            GameClientMatchmaker.Get().StopMatchmaking();
        }

        public void OnClickSettings()
        {
            SettingsPanel.Get().Show();
        }

        public void FadeToScene(string scene)
        {
            StartCoroutine(FadeToRun(scene));
        }

        private IEnumerator FadeToRun(string scene)
        {
            BlackPanel.Get().Show();
            AudioTool.Get().FadeOutMusic("music");
            yield return new WaitForSeconds(1f);
            SceneNav.GoTo(scene);
        }

        public void OnClickLogout()
        {
            TcgNetwork.Get().Disconnect();
            Authenticator.Get().Logout();
            FadeToScene("LoginMenu");
        }

        public void OnClickQuit()
        {
            Application.Quit();
        }

        public static MainMenu Get()
        {
            return instance;
        }
    }
}
