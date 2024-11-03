using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "weapon", menuName = "TcgEngine/Weapon/FRONT", order = 10)]
    public class WeaponFRONT : WeaponData
    {
        public string FRONT_id = "FRONT";
        public WeaponType FRONT_type = WeaponType.FRONT;
        public int FRONT_range = 1;
        private Color32 weapon_FRONT_color = new Color32(255, 125, 125, 255);

        public override string GetWeaponID()
        {
            return FRONT_id;
        }

        public override WeaponType GetWeaponType()
        {
            return FRONT_type;
        }

        public override int GetDefaultRange()
        {
            return FRONT_range;
        }

        public override Color32 GetWeaponColor()
        {
            return weapon_FRONT_color;
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