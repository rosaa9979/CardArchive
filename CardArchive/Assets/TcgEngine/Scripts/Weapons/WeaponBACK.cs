using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "weapon", menuName = "TcgEngine/Weapon/BACK", order = 10)]
    public class WeaponBACK : WeaponData
    {
        public string BACK_id = "BACK";
        public WeaponType BACK_type = WeaponType.BACK;
        public int BACK_range = 3;
        private Color32 weapon_BACK_color = new Color32(0, 136, 254, 255);

        public override string GetWeaponID()
        {
            return BACK_id;
        }

        public override WeaponType GetWeaponType()
        {
            return BACK_type;
        }

        public override int GetDefaultRange()
        {
            return BACK_range;
        }

        public override Color32 GetWeaponColor()
        {
            return weapon_BACK_color;
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