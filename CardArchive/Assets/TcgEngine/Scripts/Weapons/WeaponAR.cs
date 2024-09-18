using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    [System.Serializable]
    public class WeaponAR : WeaponData
    {
        public WeaponAR()
        {
            type = WeaponType.AR;
            range = 3;
        }

        public override WeaponType GetWeaponType()
        {
            return type;
        }

        public override int GetDefaultRange()
        {
            return range;
        }


        public override List<Card> SearchTarget(GameLogic logic, Card attacker)
        {
            List<Card> target = new List<Card>();
            List<Card> targets = logic.GetAllTarget(attacker);

            if (targets.Count > 0)
            {
                int ran = 0;
                target.Add(targets[ran]);
            }

            return target;
        }

        public override void AttackTarget(GameLogic logic, Card attacker, List<Card> targets)
        {
            foreach (Card target in targets)
            {
                logic.AttackTarget(attacker, target);
            }
        }

        public override void AttackTarget(GameLogic logic, Card attacker, Player target)
        {
            Game game = logic.GetGameData();
            Player oplayer = game.GetOpponentPlayer(attacker.player_id);

            logic.AttackPlayer(attacker, oplayer);
        }
    }
}