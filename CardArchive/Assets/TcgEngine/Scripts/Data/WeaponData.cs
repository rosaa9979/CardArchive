using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Defines all traits and stats data
    /// </summary>

    [CreateAssetMenu(fileName = "WeaponData", menuName = "TcgEngine/WeaponData", order = 1)]
    public class WeaponData : ScriptableObject
    {
        public string id;
        public string title;

        public static List<WeaponData> weapon_list = new List<WeaponData>();

        public string GetTitle()
        {
            return title;
        }

        public static void Load(string folder = "")
        {
            if (weapon_list.Count == 0)
                weapon_list.AddRange(Resources.LoadAll<WeaponData>(folder));
        }

        public static WeaponData Get(string id)
        {
            foreach (WeaponData weapon in GetAll())
            {
                if (weapon.id == id)
                    return weapon;
            }
            return null;
        }

        public static List<WeaponData> GetAll()
        {
            return weapon_list;
        }
    }

    /*
    [System.Serializable]
    public struct TraitStat
    {
        public TraitData trait;
        public int value;
    }
    */
}