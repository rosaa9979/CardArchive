using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    [System.Serializable]
    public class WeaponNone : WeaponData
    {
        public WeaponNone()
        {
            type = WeaponType.None;
            range = 0;
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
            return new List<Card>();
        }

        public override void AttackTarget(GameLogic logic, Card attacker, List<Card> targets)
        {
            return;
        }

        public override void AttackTarget(GameLogic logic, Card attacker, Player target)
        {
            return;
        }
    }
}