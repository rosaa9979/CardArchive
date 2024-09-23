using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "weapon", menuName = "TcgEngine/Weapon/SR", order = 10)]
    public class WeaponSR : WeaponData
    {
        public string SR_id = "SR";
        public WeaponType SR_type = WeaponType.SR;
        public int SR_range = 4;

        public override string GetWeaponID()
        {
            return SR_id;
        }

        public override WeaponType GetWeaponType()
        {
            return SR_type;
        }

        public override int GetDefaultRange()
        {
            return SR_range;
        }


        public override List<Card> SearchTarget(GameLogic logic, Card attacker)
        {
            List<Card> target = new List<Card>();
            List<Card> targets = logic.GetAllTarget(attacker);

            if (targets.Count != 0)
            {
                int minHP = targets.Min(card => card.GetHP());
                List<Card> lowestHPCards = targets.Where(card => card.GetHP() == minHP).ToList();
                int randomIdx = UnityEngine.Random.Range(0, lowestHPCards.Count);
                target.Add(lowestHPCards[randomIdx]);
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