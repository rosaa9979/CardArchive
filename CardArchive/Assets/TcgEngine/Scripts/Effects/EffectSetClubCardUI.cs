using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    //동아리 카드의 필드 동아리 부원 수를 세팅하기 위한 코드
    //효과용이 아닌 동아리 인원 수 표시용

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/SetClubCardUI", order = 10)]
    public class EffectSetClubCardUI : EffectData
    {
        public override void DoOngoingEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            Game game = logic.GetGameData();
            target.mana_ongoing += GetCount(game, caster);
        }
        
        private int GetCount(Game data, Card caster)
        {
            Player p = data.GetPlayer(caster.player_id);
            ClubData club = caster.GetAllClubs()[0].ClubData;

            if (club == null)
                return 0;
                
            int val = 0;

            foreach (Card card in p.cards_board)
            {
                if (card.HasClub(club))
                    val++;
            }

            return val;
        }
    }
}