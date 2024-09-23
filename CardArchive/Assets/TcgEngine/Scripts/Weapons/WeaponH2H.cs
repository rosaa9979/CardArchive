using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "weapon", menuName = "TcgEngine/Weapon/H2H", order = 10)]
    public class WeaponH2H : WeaponData
    {
        public string H2H_id = "H2H";
        public WeaponType H2H_type = WeaponType.H2H;
        public int H2H_range = 1;

        public override string GetWeaponID()
        {
            return H2H_id;
        }

        public override WeaponType GetWeaponType()
        {
            return H2H_type;
        }

        public override int GetDefaultRange()
        {
            return H2H_range;
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