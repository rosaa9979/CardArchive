using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "weapon", menuName = "TcgEngine/Weapon/MT", order = 10)]
    public class WeaponMT : WeaponData
    {
        public string MT_id = "MT";
        public WeaponType MT_type = WeaponType.MT;
        public int MT_range = 3;

        public override string GetWeaponID()
        {
            return MT_id;
        }

        public override WeaponType GetWeaponType()
        {
            return MT_type;
        }

        public override int GetDefaultRange()
        {
            return MT_range;
        }


        public override List<Card> SearchTarget(GameLogic logic, Card attacker)
        {
            List<Card> target = new List<Card>();
            List<Card> targets = logic.GetAllTarget(attacker);

            if (targets.Count > 0)
            {
                int ran = UnityEngine.Random.Range(0, targets.Count);
                target.Add(targets[ran]);
            }

            return target;
        }

        public override void AttackTarget(GameLogic logic, Card attacker, List<Card> targets)
        {
            Game game_data = logic.GetGameData();

            foreach (Card targ in targets)
            {
                logic.AttackTarget(attacker, targ);

                List<Slot> neighbor_slots = targ.slot.GetNeighborSlot();

                foreach (Slot neighbor in neighbor_slots)
                {
                    if (game_data.CanAttackTarget(attacker, game_data.GetSlotCard(neighbor)))
                        logic.DamageCard(attacker, neighbor, attacker.GetAttack());
                }

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