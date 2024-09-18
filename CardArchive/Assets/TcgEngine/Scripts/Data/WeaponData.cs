using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Defines all traits and stats data
    /// </summary>
    [System.Serializable]
    public class WeaponData : ScriptableObject
    {
        [SerializeField]
        public WeaponType type;
        public int range= 0;

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

        }

        public virtual void AttackTarget(GameLogic logic, Card attacker, Player target)
        {

        }
    }
    
    [System.Serializable]
    public enum WeaponType
    {
        None = 0,

        SG = 1,
        SMG = 2,
        HG = 3,
        H2H = 4,

        AR = 11,
        MG = 12,
        SR = 13,

        GL = 21,
        RL = 22,
        MT = 23,

        RG = 31,
        FT = 32,
    }
}