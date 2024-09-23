using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    [CreateAssetMenu(fileName = "weapon", menuName = "TcgEngine/Weapon/FT", order = 10)]
    public class WeaponFT : WeaponData
    {
        public string FT_id = "FT";
        public WeaponType FT_type = WeaponType.FT;
        public int FT_range = 1;

        public override string GetWeaponID()
        {
            return FT_id;
        }

        public override WeaponType GetWeaponType()
        {
            return FT_type;
        }

        public override int GetDefaultRange()
        {
            return FT_range;
        }


        public override List<Card> SearchTarget(GameLogic logic, Card attacker)
        {
            List<Card> target = new List<Card>();
            List<Card> targets = logic.GetNearestTarget(attacker);

            if (targets.Count > 0)
            {
                int ran = UnityEngine.Random.Range(0, targets.Count);
                target.Add(targets[ran]);
            }

            return target;
        }

        public override void AttackTarget(GameLogic logic, Card attacker, List<Card> targets)
        {
            Game game_data = logic.GetGameData();
            Player oplayer = game_data.GetOpponentPlayer(attacker.player_id);

            foreach (Card targ in targets)
            {
                logic.AttackTarget(attacker, targ);


                // 연결된 유닛 공격
                HashSet<Slot> visited = new HashSet<Slot>();
                Queue<(Slot slot, int distance)> queue = new Queue<(Slot slot, int distance)>();

                List<Slot> neighbor_slot = new List<Slot>();

                queue.Enqueue((targ.slot, 0));
                visited.Add(targ.slot);
                //neighbor_slot.Add(new Slot(x, y, p));

                while (queue.Count > 0)
                {
                    // 현재 슬롯과 거리 정보를 큐에서 꺼냄
                    var (currentSlot, currentDistance) = queue.Dequeue();

                    // 현재 슬롯의 모든 이웃 슬롯 탐색
                    foreach (var neighbor in currentSlot.GetNeighborSlot())
                    {
                        // 이웃 슬롯이 방문하지 않았다면
                        if (!visited.Contains(neighbor))
                        {
                            Card candidate = game_data.GetSlotCard(neighbor);
                            if (candidate != null && candidate.player_id == oplayer.player_id)
                            {
                                visited.Add(neighbor);
                                neighbor_slot.Add(candidate.slot);
                                queue.Enqueue((neighbor, currentDistance + 1));
                            }
                        }
                    }
                }

                foreach (Slot s in neighbor_slot)
                    logic.DamageCard(attacker, s, attacker.GetAttack());
            }
        }

        public override void AttackTarget(GameLogic logic, Card attacker, Player target)
        {
            Game game = logic.GetGameData();
            Player oplayer = game.GetOpponentPlayer(attacker.player_id);

            logic.AttackPlayer(attacker, oplayer);
        }
    }
}