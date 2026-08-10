// GameLogic의 전투 흐름 미러.
//   AttackCheck → AttackSearch → AttackTargets(볼리 루프)
//     → AttackTarget(전 트리거) → ResolveAttack → ResolveAttackHit(데미지 교환 + 후 트리거)
//     → ResolveDeath → AttackTargets 복귀
// 죽음은 볼리가 끝날 때까지 death_step_suspended로 보류된다 (GameLogic.cs:955-1021).
using System;
using System.Collections.Generic;

namespace TcgEngine
{
    public partial class Sim
    {
        public bool death_step_suspended;

        List<Card> attack_list = new List<Card>();
        List<Card> attack_complete = new List<Card>();
        int attack_index;

        /// 전투 시작. 필드 유닛을 순서대로 한 번씩 공격시킨다.
        public void StartAttackPhase()
        {
            attack_index = 0;
            rq.AddCallback(AttackCheck);
        }

        // GameLogic.AttackCheck (GameLogic.cs:427-463) — 커서로 단일 패스
        void AttackCheck()
        {
            List<Card> order = game.players[0].cards_board;
            while (attack_index < order.Count)
            {
                Card attacker = order[attack_index];
                if (!attacker.exhausted && IsInPlay(attacker))
                {
                    AttackSearch(attacker);
                    return;
                }
                attack_index++;
            }
            log.Add("— 전투 종료 —");
        }

        // GameLogic.AttackSearch (GameLogic.cs:465-524)
        void AttackSearch(Card attacker)
        {
            attack_list = new List<Card>(game.players[1].cards_board);
            attack_complete.Clear();
            death_step_suspended = false;
            log.Add($"▶ {attacker.uid} 공격 개시");

            if (attack_list.Count == 0)
            {
                attacker.exhausted = true;
                log.Add($"◀ {attacker.uid} 대상 없음");
            }
            else AttackTargets(attacker);

            rq.AddCallback(AttackCheck);   //다음 유닛으로 복귀
        }

        // GameLogic.AttackTargets (GameLogic.cs:967-1021) — 볼리 루프
        void AttackTargets(Card attacker, bool skip = false)
        {
            death_step_suspended = false;   //재진입마다 해제

            foreach (Card t in attack_list)
            {
                if (attack_complete.Contains(t)) continue;
                if (!IsInPlay(t)) continue;
                if (IsDying(t)) continue;   //overkill guard: 이미 빈사면 건너뜀

                death_step_suspended = true;   //한 발 나가는 동안 죽음 보류
                rq.AddAttack(attacker, t, AttackTarget);
                return;
            }

            attacker.exhausted = true;   //ExhaustBattle
            log.Add($"◀ {attacker.uid} 공격 종료");
        }

        // GameLogic.AttackTarget (GameLogic.cs:1023-1066) — 공격 전 트리거
        void AttackTarget(Card attacker, Card target, bool skip)
        {
            if (attacker.on_before_attack != null) TriggerCardAbilityType(attacker, attacker.on_before_attack);
            if (target.on_before_defend != null) TriggerCardAbilityType(target, target.on_before_defend);
            RaiseOther(o => o.on_before_attack_other);
            rq.AddAttack(attacker, target, ResolveAttack);
        }

        // GameLogic.ResolveAttack (GameLogic.cs:1068-1102)
        void ResolveAttack(Card attacker, Card target, bool skip)
        {
            if (!IsInPlay(attacker) || !IsInPlay(target))
            {
                log.Add($"  ✕ {attacker.uid} → {target.uid} 무산 (보드 이탈)");
                rq.AddAttack(attacker, (a, s) => AttackTargets(a, s));
                return;
            }
            rq.AddAttack(attacker, target, ResolveAttackHit);
        }

        // GameLogic.ResolveAttackHit (GameLogic.cs:1104-1156) — 데미지 교환 + 공격 후 트리거
        void ResolveAttackHit(Card attacker, Card target, bool skip)
        {
            log.Add($"  ⚔ {attacker.uid}({attacker.attack}) → {target.uid}(체{target.hp})");
            DamageCard(attacker, target, attacker.attack);

            if (attacker.is_front)   //FRONT 무기 = 반격 (데미지 교환)
            {
                log.Add($"  ⚔ 반격 {target.uid}({target.attack}) → {attacker.uid}(체{attacker.hp})");
                DamageCard(target, attacker, target.attack);
            }

            if (attacker.on_after_attack != null) TriggerCardAbilityType(attacker, attacker.on_after_attack);
            if (target.on_after_defend != null) TriggerCardAbilityType(target, target.on_after_defend);
            RaiseOther(o => o.on_after_attack_other);

            attack_complete.Add(target);
            rq.AddAttack(attacker, target, ResolveDeath);
        }

        // GameLogic.ResolveDeath (GameLogic.cs:1158-1169) — 볼리 루프로 복귀
        void ResolveDeath(Card attacker, Card target, bool skip)
        {
            rq.AddAttack(attacker, (a, s) => AttackTargets(a, s));
        }

        // OnBeforeAttackOther / OnAfterAttackOther 배치
        void RaiseOther(Func<Card, AbilityData> pick)
        {
            var list = new List<(Card, AbilityData)>();
            foreach (Player p in game.players)
                foreach (Card c in p.cards_board)
                {
                    AbilityData ab = pick(c);
                    if (ab != null) list.Add((c, ab));
                }
            if (list.Count > 0) RaiseEvent(list.ToArray());
        }
    }
}
