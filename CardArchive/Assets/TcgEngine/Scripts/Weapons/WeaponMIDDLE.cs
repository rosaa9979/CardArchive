using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "weapon", menuName = "TcgEngine/Weapon/MIDDLE", order = 10)]
    public class WeaponMIDDLE : WeaponData
    {
        public string MIDDLE_id = "MIDDLE";
        public WeaponType MIDDLE_type = WeaponType.MIDDLE;
        public int MIDDLE_range = 2;
        private Color32 weapon_MIDDLE_color = new Color32(255, 181, 78, 255);

        public override string GetWeaponID()
        {
            return MIDDLE_id;
        }

        public override WeaponType GetWeaponType()
        {
            return MIDDLE_type;
        }

        public override int GetDefaultRange()
        {
            return MIDDLE_range;
        }

        public override Color32 GetWeaponColor()
        {
            return weapon_MIDDLE_color;
        }



        public override List<Card> SearchTarget(GameLogic logic, Card attacker)
        {
            List<Card> target = new List<Card>();
            Dictionary<int, List<Card>> targets = logic.GetAllTarget(attacker);
            List<Card> target_list = targets.Values.SelectMany(cardList => cardList).ToList();

            if (attacker.HasStatus(StatusType.MassShooting))
            {
                foreach(Card targ in target_list)
                {
                    if (targ == attacker || attacker.player_id == targ.player_id)
                        continue;

                    float ran = UnityEngine.Random.Range(0.0f, 1.0f);
                    Debug.Log(ran);
                    if (ran < 1)
                        target.Add(targ);
                }
            }
            
            else if (target_list.Count > 0)
            {
                bool contain_taunt = target_list.Any(card => card.HasStatus(StatusType.Protection));
                
                List<Card> candidate_target = logic.GetGameData().CanAttackTarget(attacker, target_list);
                if (candidate_target.Count > 0)
                {
                    int ran = UnityEngine.Random.Range(0, candidate_target.Count);
                    target.Add(candidate_target[ran]);
                }
            }

            return target;
        }

        public override void AttackTarget(GameLogic logic, Card attacker, List<Card> targets)
        {
            foreach (Card targ in targets)
                logic.AttackTarget(attacker, targ);
        }

        public override void AttackTarget(GameLogic logic, Card attacker, Player target)
        {
            Game game = logic.GetGameData();
            Player oplayer = game.GetOpponentPlayer(attacker.player_id);

            logic.AttackPlayer(attacker, oplayer);
        }
    }
}