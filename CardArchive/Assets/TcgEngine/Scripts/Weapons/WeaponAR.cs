using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "weapon", menuName = "TcgEngine/Weapon/AR", order = 10)]
    public class WeaponAR : WeaponData
    {
        public string AR_id = "AR";
        public WeaponType AR_type = WeaponType.AR;
        public int AR_range = 3;

        public override string GetWeaponID()
        {
            return AR_id;
        }

        public override WeaponType GetWeaponType()
        {
            return AR_type;
        }

        public override int GetDefaultRange()
        {
            return AR_range;
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
            int randInt = UnityEngine.Random.Range(0, targets.Count);
            logic.AttackTarget(attacker, targets[randInt]);
        }


        /*
        public override void AttackTarget(GameLogic logic, Card attacker, Player target)
        {
            Game game = logic.GetGameData();
            Player oplayer = game.GetOpponentPlayer(attacker.player_id);

            logic.AttackPlayer(attacker, oplayer);
        }
        */
    }
}