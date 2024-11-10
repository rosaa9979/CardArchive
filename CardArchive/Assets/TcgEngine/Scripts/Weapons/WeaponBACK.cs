using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "weapon", menuName = "TcgEngine/Weapon/BACK", order = 10)]
    public class WeaponBACK : WeaponData
    {
        public string BACK_id = "BACK";
        public WeaponType BACK_type = WeaponType.BACK;
        public int BACK_range = 3;
        private Color32 weapon_BACK_color = new Color32(0, 136, 254, 255);

        public override string GetWeaponID()
        {
            return BACK_id;
        }

        public override WeaponType GetWeaponType()
        {
            return BACK_type;
        }

        public override int GetDefaultRange()
        {
            return BACK_range;
        }

        public override Color32 GetWeaponColor()
        {
            return weapon_BACK_color;
        }

        public override List<Card> SearchTarget(GameLogic logic, Card attacker)
        {
            List<Card> target = new List<Card>();
            Dictionary<int, List<Card>> targets = logic.GetAllEnemyTarget(attacker);
            List<Card> target_list = targets.Values.SelectMany(cardList => cardList).ToList();

            if (attacker.HasStatus(StatusType.MassShooting))
            {
                foreach(Card targ in target_list)
                {
                    if (targ == attacker || attacker.player_id == targ.player_id)
                        continue;

                    float ran = UnityEngine.Random.Range(0.0f, 1.0f);
                    if (ran < 1)
                        target.Add(targ);
                }
            }
            
            else if (target_list.Count > 0)
            {
                bool contain_taunt = target_list.Any(card => card.HasStatus(StatusType.Protection));
                bool contain_place = target_list.Any(card => card.CardData.IsPlace());
                
                //List<Card> candidate_target = logic.GetGameData().CanAttackTarget(attacker, target_list);
                //Debug.Log(candidate_target.Count);
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

                // 1. 가장 작은 HP 값 찾기
                int minHP = candidate_target.Min(card => card.GetHP());

                // 2. 가장 작은 HP 값을 가진 카드들 찾기
                var lowestHPCards = candidate_target.Where(card => card.GetHP() == minHP).ToList();

                // 3. 랜덤으로 하나 선택
                target.Add(lowestHPCards[UnityEngine.Random.Range(0, lowestHPCards.Count)]);
            }

            return target;
        }

        public override void AttackTarget(GameLogic logic, Card attacker, List<Card> targets)
        {
            foreach (Card targ in targets)
                logic.AttackTarget(attacker, targ);
        }

        public override void AttackTarget(GameLogic logic, Card attacker, Player target)
        {
            Game game = logic.GetGameData();
            Player oplayer = game.GetOpponentPlayer(attacker.player_id);

            logic.AttackPlayer(attacker, oplayer);
        }
    }
}