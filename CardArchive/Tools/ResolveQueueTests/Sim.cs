// GameLogic의 ProcessDeathStep / ProcessPendingRepeats / Trigger*AbilityType 를 미러링한 하네스.
// 수정된 실제 ResolveQueue.cs 를 그대로 컴파일해서 돌린다.
using System;
using System.Collections.Generic;
using System.Linq;

namespace TcgEngine
{
    public partial class Sim
    {
        public Game game = new Game();
        public ResolveQueue rq;
        public List<string> log = new List<string>();
        public bool show_depth;   //디버그: 로그에 중첩 깊이를 붙인다

        class PendingRepeat
        {
            public AbilityData ability; public Card caster;
            public int max_repeat; public int next_repeat;
        }
        List<PendingRepeat> pending = new List<PendingRepeat>();

        public Sim()
        {
            game.players.Add(new Player { player_id = 0 });
            game.players.Add(new Player { player_id = 1 });
            rq = new ResolveQueue(game, true);
            rq.SetDeathStep(ProcessDeathStep, HasPendingDeaths, () => death_step_suspended);
        }

        // ---------------- 보드 ----------------
        int play_counter = 0;
        public Card AddCard(int pid, string uid, int hp, AbilityData on_death = null, AbilityData on_death_other = null)
        {
            Card c = new Card { uid = uid, player_id = pid, hp = hp, play_order = ++play_counter,
                                on_death = on_death, on_death_other = on_death_other };
            game.players[pid].cards_board.Add(c);
            return c;
        }
        public Card AddPlayerAbilityCard(int pid, string uid, int hp, AbilityData on_death = null)
        {
            Card c = new Card { uid = uid, player_id = pid, hp = hp, play_order = ++play_counter, on_death = on_death };
            game.players[pid].player_ability.Add(c);
            return c;
        }

        public bool IsDying(Card c) { return !c.invincible && (c.dying || c.GetHP() <= 0); }
        public bool IsInPlay(Card c)
        {
            foreach (Player p in game.players)
                if (p.cards_board.Contains(c) || p.player_ability.Contains(c)) return true;
            return false;
        }

        // ---------------- 죽음 페이즈 (GameLogic.ProcessDeathStep 미러) ----------------
        public bool HasPendingDeaths()
        {
            if (pending.Count > 0) return true;
            foreach (Player p in game.players)
            {
                foreach (Card c in p.cards_board) if (IsDying(c)) return true;
                foreach (Card c in p.player_ability) if (IsDying(c)) return true;
            }
            return false;
        }

        public bool ProcessDeathStep()
        {
            //볼리 진행 중에는 죽음을 보류한다 (GameLogic.ProcessDeathStep 미러)
            if (death_step_suspended) return false;

            List<Card> dying = new List<Card>();
            foreach (Player p in game.players)
            {
                foreach (Card c in p.cards_board) if (IsDying(c)) dying.Add(c);
                foreach (Card c in p.player_ability) if (IsDying(c)) dying.Add(c);
            }

            if (dying.Count == 0) return ProcessPendingRepeats();

            dying.Sort((a, b) => a.play_order.CompareTo(b.play_order));
            log.Add("[wave:" + string.Join("+", dying.Select(c => c.uid)) + "]");

            rq.BeginImmediatePhase();
            foreach (Card c in dying)   // 동시 제거
            {
                game.players[c.player_id].cards_board.Remove(c);
                game.players[c.player_id].player_ability.Remove(c);
            }
            //OnKill 확정 (GameLogic.cs:2003-2014) — 죽은 순서대로, OnDeath보다 먼저
            foreach (Card c in dying)
                if (c.death_source != null && c.death_source.on_kill != null)
                    TriggerCardAbilityType(c.death_source, c.death_source.on_kill);
            foreach (Card c in dying)
            {
                if (c.on_death != null) TriggerCardAbilityType(c, c.on_death);
                List<Card> survivors = new List<Card>();
                foreach (Player p in game.players) survivors.AddRange(p.cards_board);
                survivors.Sort((a, b) => a.play_order.CompareTo(b.play_order));
                foreach (Card s in survivors)
                    if (s.on_death_other != null)
                        TriggerCardAbilityType(s, new AbilityData { id = s.uid + ":목격(" + c.uid + ")" });
            }
            rq.EndPhase();
            return true;
        }

        bool ProcessPendingRepeats()
        {
            if (pending.Count == 0) return false;
            bool any = false;
            rq.BeginImmediatePhase();
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingRepeat p = pending[i];
                if (!IsInPlay(p.caster)) continue;                                  // 요그 규칙
                if (p.ability.repeat_condition != null && !p.ability.repeat_condition(this)) continue;
                rq.AddAbility(p.ability, p.caster, p.caster, p.max_repeat, p.next_repeat, ResolveCardAbility);
                any = true;
            }
            rq.EndPhase();
            pending.Clear();
            return any;
        }

        // ---------------- 트리거 적재 (GameLogic 미러) ----------------

        /// GameLogic.TriggerCardAbilityType — 카드 한 장의 같은 트리거 어빌리티들을 한 Phase로
        public void TriggerCardAbilityType(Card caster, params AbilityData[] abilities)
        {
            rq.BeginPhase();
            foreach (AbilityData a in abilities) TriggerCardAbility(a, caster);
            rq.EndPhase();
        }

        /// GameLogic.TriggerOtherCardsAbilityType / TriggerPlayerCardsAbilityType —
        /// 여러 카드가 한 이벤트에 반응 (바깥 Phase + 카드별 Phase)
        public void RaiseEvent(params (Card caster, AbilityData ab)[] responders)
        {
            rq.BeginPhase();
            foreach (var r in responders) TriggerCardAbilityType(r.caster, r.ab);
            rq.EndPhase();
        }

        public void TriggerCardAbility(AbilityData ab, Card caster, bool is_chain = false)
        {
            rq.AddAbility(ab, caster, caster, Math.Max(ab.max_repeat, 1), 0, ResolveCardAbility, is_chain);
        }

        void ResolveCardAbility(AbilityData ab, Card caster, Card triggerer, int max_repeat, int current_repeat)
        {
            string label = max_repeat > 1 || ab.repeat_condition != null ? ab.id + (current_repeat + 1) : ab.id;
            if (show_depth) label = "d" + rq.CurrentDepth() + " " + label;
            log.Add(label);
            ab.effect?.Invoke(this, caster, current_repeat);
            if (ab.repeat_condition != null || current_repeat + 1 < max_repeat)
                pending.Add(new PendingRepeat { ability = ab, caster = caster,
                                                max_repeat = max_repeat, next_repeat = current_repeat + 1 });
        }

        // ---------------- 효과 헬퍼 ----------------
        public void DamageAllEnemies(int pid, int dmg)
        { foreach (Card c in game.players[1 - pid].cards_board) c.hp -= dmg; }
        /// GameLogic.DamageCard 미러: 피해 적용 + 킬 귀속 + OnAfterDamage (대상마다 별개 이벤트)
        public void DamageCard(Card attacker, Card target, int dmg, AbilityData on_after_damage = null)
        {
            if (target == null) return;
            target.hp -= dmg;
            if (target.hp <= 0 && target.death_source == null) target.death_source = attacker;
            if (dmg > 0 && on_after_damage != null) TriggerCardAbilityType(attacker, on_after_damage);
        }
        public void DamageAll(int dmg)
        { foreach (Player p in game.players) foreach (Card c in p.cards_board) c.hp -= dmg; }
        public void Heal(Card c, int amt) { if (c != null) c.hp += amt; }
        public void MarkDying(Card c) { if (c != null) c.dying = true; }
        public int EnemyCount(int pid) { return game.players[1 - pid].cards_board.Count; }
        public Card Find(string uid)
        {
            foreach (Player p in game.players)
            {
                foreach (Card c in p.cards_board) if (c.uid == uid) return c;
                foreach (Card c in p.player_ability) if (c.uid == uid) return c;
            }
            return null;
        }
        public string Log() { return string.Join(" → ", log); }
        /// 마지막 로그 줄을 교체한다 (깊이 접두사 유지)
        public void Relabel(string text)
        { log[log.Count - 1] = (show_depth ? "d" + rq.CurrentDepth() + " " : "") + text; }
    }
}
