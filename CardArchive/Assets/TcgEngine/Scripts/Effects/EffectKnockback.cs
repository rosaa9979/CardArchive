using System;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// 공격 후, 피격자를 "공격자 -> 피격자" 방향으로 ability.value 칸만큼 밀어낸다.
    /// 사용법: AbilityData
    ///   - trigger        = OnAfterAttack
    ///   - criteria_target = AbilityTriggerer  (= 피격자 카드)
    ///   - value          = 밀어내는 칸 수
    ///
    /// 규칙:
    ///   - 공격자와 피격자가 육각 직선 축(가로 / y축 / 대각선) 위에 정렬됐을 때만 민다.
    ///     사거리 2+ 의 사선 명중처럼 축에서 벗어난 경우엔 밀지 않는다(기획 결정).
    ///   - 경로 중간에 다른 카드/보드 밖이 가로막으면 그 직전 칸까지만 이동한다.
    ///   - 바로 뒤 한 칸이 막혀 있으면(또는 보드 밖이면) 이동하지 않는다.
    ///
    /// 좌표계: x = 가로(오른쪽 +), y = 좌상단 방향.
    ///   가로축 dy==0 / y축 dx==0 / 대각선축 dx==dy 일 때만 (sign(dx), sign(dy)) 가 단위 방향이 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/Knockback", order = 10)]
    public class EffectKnockback : EffectData
    {
        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            if (caster == null || target == null)
                return;

            Game data = logic.GetGameData();

            int steps = Mathf.Max(0, ability.value); //이동 칸 수
            if (steps <= 0)
                return;

            int vx = target.slot.x - caster.slot.x;
            int vy = target.slot.y - caster.slot.y;

            //육각 직선 축에 정렬된 경우만 넉백 (그 외 사선 명중은 밀지 않음)
            bool onAxis = (vy == 0 || vx == 0 || vx == vy) && (vx != 0 || vy != 0);
            if (!onAxis)
                return;

            (int dx, int dy) dir = (Math.Sign(vx), Math.Sign(vy));

            //피격자 위치에서 방향대로 한 칸씩 진행, 막히기 직전까지의 마지막 빈 칸을 구한다
            Slot cur = target.slot;
            Slot dest = Slot.None;
            for (int i = 0; i < steps; i++)
            {
                Slot next = cur.GetSlot(dir);
                if (!next.IsValid())
                    break;                            //보드 밖 -> 정지
                if (data.GetSlotCard(next) != null)
                    break;                            //다른 카드가 가로막음 -> 그 직전까지만
                dest = next;
                cur = next;
            }

            //바로 뒤가 막혀 있으면 dest 가 None -> 이동하지 않음
            if (dest.IsValid() && data.CanMoveCard(target, dest, true))
                logic.MoveCard(target, dest, true);   //skip_cost = true: 강제 이동
        }
    }
}
