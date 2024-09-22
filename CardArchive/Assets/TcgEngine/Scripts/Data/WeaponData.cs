using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Defines all traits and stats data
    /// </summary>
    /// 

    [System.Serializable]
    public class WeaponData : ScriptableObject
    {
        [System.NonSerialized] public string id = "Default";
        [System.NonSerialized] public WeaponType type = WeaponType.None;
        [System.NonSerialized] public int range = 0;
        public static List<WeaponData> weapon_list = new List<WeaponData>();

        public static void Load(string folder = "")
        {
            if (weapon_list.Count == 0)
                weapon_list.AddRange(Resources.LoadAll<WeaponData>(folder));
        }

        public static List<WeaponData> GetAll()
        {
            return weapon_list;
        }

        public static WeaponData Get(string id)
        {
            foreach (WeaponData weapon in GetAll())
            {
                if (weapon.GetWeaponID() == id)
                    return weapon;
            }
            return null;
        }

        public virtual string GetWeaponID()
        {
            return id;
        }

        public virtual WeaponType GetWeaponType()
        {
            return type;
        }

        public virtual int GetDefaultRange()
        {
            return range;
        }

        public virtual List<Card> SearchTarget(GameLogic logic, Card attacker)
        {
            return new List<Card>();
        }

        public virtual void AttackTarget(GameLogic logic, Card attacker, List<Card> targets)
        {
            return;
        }
    }
}