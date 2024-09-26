using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "weapon", menuName = "TcgEngine/Weapon/MG", order = 10)]
    public class WeaponMG : WeaponData
    {
        public string MG_id = "MG";
        public WeaponType MG_type = WeaponType.MG;
        public int MG_range = 2;

        public override string GetWeaponID()
        {
            return MG_id;
        }

        public override WeaponType GetWeaponType()
        {
            return MG_type;
        }

        public override int GetDefaultRange()
        {
            return MG_range;
        }


        public override List<Card> SearchTarget(GameLogic logic, Card attacker)
        {
            List<Card> target = new List<Card>();
            List<Card> targets = logic.GetAllTarget(attacker);

            return targets;
        }

        public override void AttackTarget(GameLogic logic, Card attacker, List<Card> targets)
        {
            List<Card> final_result = new List<Card>();
            System.Random random = new System.Random();

            foreach (Card targ in targets)
            {
                //float ran = UnityEngine.Random.Range(0.0f, 1.0f);
                double ran = random.NextDouble();

                if (ran < 0.5)
                    final_result.Add(targ);
            }

            foreach (Card targ in final_result)
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