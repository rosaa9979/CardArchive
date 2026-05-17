using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace TcgEngine.Client
{
    //Grants rewards for adventure solo mode

    public class RewardManager : MonoBehaviour
    {
        private bool reward_gained = false;

        private static RewardManager instance;

        void Awake()
        {
            instance = this;
        }

        private void Start()
        {
            GameClient.Get().onGameEnd += OnGameEnd;
        }

        void OnGameEnd(int winner)
        {
            int player_id = GameClient.Get().GetPlayerID();
            if (winner != player_id)
                return;

            if (GameClient.game_settings.game_type == GameType.Adventure)
            {
                UserData udata = Authenticator.Get().UserData;
                LevelData level = LevelData.Get(GameClient.game_settings.level);
                if (level != null && !udata.HasReward(level.id) && !reward_gained)
                {
                    if (Authenticator.Get().IsTest())
                        GainRewardTest(level.id, level.reward_coins, level.reward_xp, level.reward_cards, level.reward_packs);
                    if (Authenticator.Get().IsApi())
                        GainRewardAPIAsync(level.id);
                }
            }
            else if (GameClient.game_settings.game_type == GameType.Tutorial)
            {
                UserData udata = Authenticator.Get().UserData;
                TutorialData tutorial = TutorialData.Get(GameClient.game_settings.level);
                if (tutorial != null && !udata.HasReward(tutorial.id) && !reward_gained)
                {
                    if (Authenticator.Get().IsTest())
                        GainRewardTest(tutorial.id, tutorial.reward_coins, tutorial.reward_xp, tutorial.reward_cards, tutorial.reward_packs);
                    if (Authenticator.Get().IsApi())
                        GainRewardAPIAsync(tutorial.id);
                }
            }
            else if (GameClient.game_settings.game_type == GameType.TotalAssault)
            {
                UserData udata = Authenticator.Get().UserData;
                TotalAssaultData data = TotalAssaultData.Get(GameClient.game_settings.level);
                if (data != null && !udata.HasReward(data.id) && !reward_gained)
                {
                    if (Authenticator.Get().IsTest())
                        GainRewardTest(data.id, data.reward_coins, data.reward_xp, data.reward_cards, data.reward_packs);
                    if (Authenticator.Get().IsApi())
                        GainRewardAPIAsync(data.id);
                }
            }
        }

        private async void GainRewardTest(string reward_id, int coins, int xp, CardData[] cards, PackData[] packs)
        {
            VariantData variant = VariantData.GetDefault();
            UserData udata = Authenticator.Get().UserData;
            udata.coins += coins;
            udata.xp += xp;
            udata.AddReward(reward_id);

            if (cards != null)
            {
                foreach (CardData card in cards)
                {
                    udata.AddCard(card.id, variant.id, 1);
                }
            }

            if (packs != null)
            {
                foreach (PackData pack in packs)
                {
                    udata.AddPack(pack.id, 1);
                }
            }

            reward_gained = true;
            await Authenticator.Get().SaveUserData();
        }

        private async void GainRewardAPIAsync(string reward_id)
        {
            bool success = await GainRewardAPI(reward_id);
            reward_gained = success;
        }

        public async Task<bool> GainRewardAPI(string reward_id)
        {
            RewardGainRequest req = new RewardGainRequest();
            req.reward = reward_id;

            string url = ApiClient.ServerURL + "/users/rewards/gain/" + ApiClient.Get().UserID;
            string json = ApiTool.ToJson(req);
            WebResponse res = await ApiClient.Get().SendPostRequest(url, json);
            Debug.Log("Gain Reward: " + reward_id + " " + res.success);
            return res.success;
        }

        public bool IsRewardGained()
        {
            return reward_gained;
        }

        public static RewardManager Get()
        {
            return instance;
        }
    }
}
