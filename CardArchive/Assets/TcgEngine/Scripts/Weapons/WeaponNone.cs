using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "weapon", menuName = "TcgEngine/Weapon/NONE", order = 10)]
    public class WeaponNONE : WeaponData
    {
        public string NONE_id = "NONE";
        public WeaponType NONE_type = WeaponType.NONE;
        public int NONE_range = 0;

        public override string GetWeaponID()
        {
            return NONE_id;
        }

        public override WeaponType GetWeaponType()
        {
            return NONE_type;
        }

        public override int GetDefaultRange()
        {
            return NONE_range;
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