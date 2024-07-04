using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Defines all traits and stats data
    /// </summary>

    [CreateAssetMenu(fileName = "ClubData", menuName = "TcgEngine/ClubData", order = 1)]
    public class ClubData : ScriptableObject
    {
        public string id;
        public string title;
        //public Sprite icon;

        public static List<ClubData> club_list = new List<ClubData>();

        public string GetTitle()
        {
            return title;
        }

        public static void Load(string folder = "")
        {
            if (club_list.Count == 0)
                club_list.AddRange(Resources.LoadAll<ClubData>(folder));
        }

        public static ClubData Get(string id)
        {
            foreach (ClubData club in GetAll())
            {
                if (club.id == id)
                    return club;
            }
            return null;
        }

        public static List<ClubData> GetAll()
        {
            return club_list;
        }
    }

    [System.Serializable]
    public struct ClubStat
    {
        public ClubData club;
        public int value;
    }
}