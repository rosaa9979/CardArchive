using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// How the AI heuristic should value this club's synergy.
    /// Each club is evaluated independently (never relative to other clubs).
    /// </summary>
    public enum ClubSynergyType
    {
        OnOff      = 0,  //Active once count >= threshold; fixed value, no bonus for stacking further
        Count      = 1,  //Scales with how many of this club are on board (more = better)
        Individual = 2,  //No synergy score (effect is per-card, board composition irrelevant)
    }

    /// <summary>
    /// Defines all traits and stats data
    /// </summary>

    [CreateAssetMenu(fileName = "ClubData", menuName = "TcgEngine/ClubData", order = 1)]
    public class ClubData : ScriptableObject
    {
        public string id;
        public string title;
        public AcademyData academy;
        public Sprite icon;

        [Header("AI Synergy")]
        public ClubSynergyType synergy_type = ClubSynergyType.Individual; //How the AI scores this club
        public int synergy_value = 10;       //Heuristic points (you define per club)
        public int synergy_threshold = 1;    //OnOff only: active when board count >= this

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