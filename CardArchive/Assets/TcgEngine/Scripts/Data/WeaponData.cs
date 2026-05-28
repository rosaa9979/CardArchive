using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Defines a weapon (range + targeting/attack rules). One asset per weapon kind
    /// (FRONT / MIDDLE / BACK / NONE), distinguished by the `type` field.
    /// </summary>

    [CreateAssetMenu(fileName = "weapon", menuName = "TcgEngine/Weapon", order = 10)]
    public class WeaponData : ScriptableObject
    {
        public string id = "Default";
        public WeaponType type = WeaponType.NONE;
        public int range = 0;
        public Color32 weapon_color = new Color32(255, 255, 255, 255);

        [Header("FX")]
        public GameObject attack_fx;
        public GameObject hit_fx;

        [Header("Audio")]
        public AudioClip attack_audio;

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

        public virtual string GetWeaponID() { return id; }
        public virtual WeaponType GetWeaponType() { return type; }
        public virtual int GetDefaultRange() { return range; }
        public virtual Color32 GetWeaponColor() { return weapon_color; }

        public virtual List<Card> SearchTarget(GameLogic logic, Card attacker)
        {
            List<Card> target = new List<Card>();
            if (type == WeaponType.NONE)
                return target;

            Dictionary<int, List<Card>> targets = logic.GetAllEnemyTarget(attacker);
            List<Card> target_list = targets.Values.SelectMany(cardList => cardList).ToList();

            if (attacker.HasStatus(StatusType.MassShooting))
            {
                foreach (Card targ in target_list)
                {
                    if (targ == attacker || attacker.player_id == targ.player_id)
                        continue;
                    target.Add(targ);
                }
                return target;
            }

            if (target_list.Count > 0)
            {
                bool contain_taunt = target_list.Any(card => card.HasStatus(StatusType.Protection));
                bool contain_place = target_list.Any(card => card.CardData.IsPlace());

                List<Card> candidate_target = new List<Card>();

                foreach (Card targ in target_list)
                {
                    if (logic.GetGameData().CanAttackTarget(attacker, targ))
                    {
                        if (contain_place)
                        {
                            if (targ.CardData.IsPlace())
                                candidate_target.Add(targ);
                        }
                        else if (contain_taunt)
                        {
                            if (targ.HasStatus(StatusType.Protection))
                                candidate_target.Add(targ);
                        }
                        else
                        {
                            candidate_target.Add(targ);
                        }
                    }
                }

                if (candidate_target.Count > 0)
                {
                    int ran = UnityEngine.Random.Range(0, candidate_target.Count);
                    target.Add(candidate_target[ran]);
                }
            }
            return target;
        }

        public virtual void AttackTarget(GameLogic logic, Card attacker, List<Card> targets)
        {
            if (type == WeaponType.NONE)
                return;
            logic.AttackTargets(attacker);
        }

        public virtual void AttackTarget(GameLogic logic, Card attacker, Player target)
        {
            if (type == WeaponType.NONE)
                return;
            Game game = logic.GetGameData();
            Player oplayer = game.GetOpponentPlayer(attacker.player_id);
            logic.AttackPlayer(attacker, oplayer);
        }
    }
}
