using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Defines all traits and stats data
    /// </summary>

    [CreateAssetMenu(fileName = "AcademyData", menuName = "TcgEngine/AcademyData", order = 1)]
    public class AcademyData : ScriptableObject
    {
        public string id;
        public string title;
        //public Sprite icon;

        public Sprite acadmey_icon;

        public static List<AcademyData> academy_list = new List<AcademyData>();

        public string GetTitle()
        {
            return title;
        }

        public static void Load(string folder = "")
        {
            if (academy_list.Count == 0)
                academy_list.AddRange(Resources.LoadAll<AcademyData>(folder));
        }

        public static AcademyData Get(string id)
        {
            foreach (AcademyData academy in GetAll())
            {
                if (academy.id == id)
                    return academy;
            }
            return null;
        }

        public static List<AcademyData> GetAll()
        {
            return academy_list;
        }
    }

    [System.Serializable]
    public struct AcademyStat
    {
        public AcademyData club;
        public int value;
    }
}