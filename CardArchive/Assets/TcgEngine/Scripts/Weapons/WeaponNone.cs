using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "weapon", menuName = "TcgEngine/Weapon/None", order = 10)]
    public class WeaponNone : WeaponData
    {
        public string None_id = "None";
        public WeaponType None_type = WeaponType.None;
        public int None_range = 0;

        public override string GetWeaponID()
        {
            return None_id;
        }

        public override WeaponType GetWeaponType()
        {
            return None_type;
        }

        public override int GetDefaultRange()
        {
            return None_range;
        }

        public override List<Card> SearchTarget(GameLogic logic, Card attacker)
        {
            return new List<Card>();
        }

        public override void AttackTarget(GameLogic logic, Card attacker, List<Card> targets)
        {
            return;
        }
    }
}