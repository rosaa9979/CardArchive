using System;
using System.Collections;
using System.Collections.Generic;
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