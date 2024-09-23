using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "weapon", menuName = "TcgEngine/Weapon/SG", order = 10)]
    public class WeaponSG : WeaponData
    {
        public string SG_id = "SG";
        public WeaponType SG_type = WeaponType.SG;
        public int SG_range = 1;

        public override string GetWeaponID()
        {
            return SG_id;
        }

        public override WeaponType GetWeaponType()
        {
            return SG_type;
        }

        public override int GetDefaultRange()
        {
            return SG_range;
        }


        public override List<Card> SearchTarget(GameLogic logic, Card attacker)
        {
            List<Card> target = new List<Card>();
            List<Card> targets = logic.GetNearestTarget(attacker);

            if (targets.Count > 0)
            {
                int ran = UnityEngine.Random.Range(0, targets.Count);
                target.Add(targets[ran]);
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