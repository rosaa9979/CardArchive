using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TcgEngine
{
    /// <summary>
    /// Resolve abilties and actions one by one, with an optional delay in between each
    ///
    /// ── 구조 요약 (2026-08 하스스톤 Sequence/Phase 정합 개편) ──────────────────────
    /// 하스스톤 Advanced Rulebook의 모델을 그대로 따른다:
    ///   * Event 하나  = Queue 하나 = Phase 하나. Phase 안의 항목들은 개별로 순차 resolve되지만
    ///     그 사이에 죽음 처리는 절대 끼어들지 않는다 (Rule 1c).
    ///   * Death Creation Step은 "**최상위(outermost) Phase**가 끝날 때만" 돈다 (Rule 3).
    ///   * 한 Player Action은 여러 Phase로 이루어진 Sequence일 수 있고, Phase와 Phase
    ///     사이에는 죽음 처리가 돈다 (Rule 6).
    ///
    /// 그래서 Phase는 **두 축**을 구분해야 한다:
    ///   * 부모 ↔ 자식 (연쇄):  자식이 부모의 남은 항목보다 **먼저**  → depth-first
    ///   * 형제 ↔ 형제 (같은 레벨): **먼저 만든 것이 먼저**            → FIFO
    /// 개편 전에는 평평한 phase_stack을 top부터 스캔해서 두 축을 하나의 LIFO로 처리했고,
    /// 그 결과 형제 Phase의 순서가 뒤집혔다 (전투의 함성이 타 카드 반응보다 늦게 터지는 등).
    /// 지금은 phase_queue(최상위 형제, FIFO) + AbilityPhase.children(자식, FIFO)로 분리한다.
    ///
    /// 자세한 배경/검증은 docs/resolve-queue-hearthstone-redesign.md 참조.
    /// ─────────────────────────────────────────────────────────────────────────────
    /// </summary>

    public class ResolveQueue
    {
        private Pool<AbilityQueueElement> ability_elem_pool = new Pool<AbilityQueueElement>();
        private Pool<SecretQueueElement> secret_elem_pool = new Pool<SecretQueueElement>();
        private Pool<AttackQueueElement> attack_elem_pool = new Pool<AttackQueueElement>();
        private Pool<CallbackQueueElement> callback_elem_pool = new Pool<CallbackQueueElement>();
        private Pool<AbilityPhase> phase_pool = new Pool<AbilityPhase>();

        //대기 중인 최상위 Phase들. 이벤트 하나당 하나가 만들어지고 **FIFO**로 소진된다.
        //(개편 전의 base ability_queue를 대체한다. 낱개 효과가 아니라 Phase 단위로 줄을 선다.)
        //List를 쓰는 이유: 죽음 트리거/반복 회차는 "현재 Sequence의 다음 Phase"라서 맨 앞에
        //끼워넣어야 한다 (BeginImmediatePhase 참조).
        private List<AbilityPhase> phase_queue = new List<AbilityPhase>();

        //지금 처리 중인 최상위 Phase. 이게 완전히 소진되는 순간이 곧 Death Creation Step 경계다.
        //null이면 "다음 최상위 Phase를 꺼낼 차례" = 죽음 경계.
        private AbilityPhase current_top;

        //AddAbility가 지금 어디로 적재해야 하는지. BeginPhase가 push, EndPhase가 pop.
        private Stack<AbilityPhase> insert_stack = new Stack<AbilityPhase>();

        //selector로 중단된 효과의 스코프. 플레이어 입력을 기다리는 동안 살려두었다가,
        //재개 시 BeginPhase()가 이걸 그대로 다시 연다. 이렇게 해야 재개된 효과의 연쇄가
        //"원래 있던 자리"(중단 시점의 부모 Phase 아래)에 들어가 depth-first가 유지된다.
        //(개편 전에는 재개 시 새 Phase가 stack top에 쌓여 우연히 맞았지만, 최상위가 FIFO가 된
        // 지금은 명시적으로 복원하지 않으면 대기 중인 형제들 뒤로 밀린다.)
        private AbilityPhase suspended_scope;

        //base 큐들. secret/attack/callback은 개편 대상이 아니다.
        //  - attack: 공격 마이크로스텝(ResolveAttack→Hit→Death→AttackTargets)은 하나의 공격
        //            Sequence를 쪼갠 것이므로 최상위가 맞다. 어빌리티가 **유발한** 공격만
        //            AddTriggeredAttack으로 현재 Phase에 담는다.
        //  - callback: 턴 진행 단계(DrawForTurn/StartMainPhase/AttackCheck/EndTurn)라
        //            모든 이벤트 Phase가 끝난 뒤에 와야 한다.
        //우선순위는 개편 전과 동일: Phase(ability) → secret → attack → callback.
        private Queue<SecretQueueElement> secret_queue = new Queue<SecretQueueElement>();
        private Queue<AttackQueueElement> attack_queue = new Queue<AttackQueueElement>();
        private Queue<CallbackQueueElement> callback_queue = new Queue<CallbackQueueElement>();

        //Per-queue "gap before the next element" lives in GameConfig.Timing (single source of truth).
        //The AI/skip_delay path never applies delays (see ResolveAll/SetDelay).

        //Death Phase hooks (Phase 2), set by GameLogic. death_step runs at every **최상위 Phase**
        //boundary — whenever current_top drains — before the next waiting Phase (Hearthstone Rule 3/4a).
        //It removes dying cards, queues their death triggers, and at the stable point re-enqueues
        //deferred repeat iterations; returns true if it did any of that. has_deaths gates the
        //call and keeps the resolve loop alive (CanResolve) while a death step or deferred
        //repeat is pending.
        //Note: has_deaths staying true while death_step returns false is a legitimate state —
        //GameLogic suspends the step while an attack is in flight so all of that attack's kills resolve
        //as one wave (GameLogic.AttackTargets). The gate then simply falls through to the next queued
        //element, which is why the suspension must never be expressed via has_deaths.
        private Func<bool> death_step;
        private Func<bool> has_deaths;
        //True while GameLogic is holding the death step back for an in-flight attack. Used for PACING
        //ONLY (GetNextQueueDelay): dying cards are still pending, so has_deaths must keep reporting them
        //and keep the loop alive, but the Death Phase is not what resolves next and the gap before
        //the next element must not be stretched to the ability tempo.
        private Func<bool> death_suspended;

        private Game game_data;
        private bool is_resolving = false;
        private float resolve_delay = 0f;
        private bool skip_delay = false;

        //Cap for the skip_delay (AI simulation) resolve loop; see ResolveAll
        private const int skip_resolve_max = 10000;

        //Cap for the Phase tree walks; see ReportPhaseTreeCorrupt
        private const int max_phase_depth = 256;
        private bool phase_tree_corrupt = false;

        public ResolveQueue(Game data, bool skip)
        {
            game_data = data;
            skip_delay = skip;
#if UNITY_EDITOR
            //EDITOR-ONLY 치트(EffectStepDebug) 훅. 실게임 큐만 등록한다 — AI 예측용 큐(skip_delay=true)를
            //등록하면 워커 스레드의 minimax가 스텝 대기에 걸려버린다.
            if (!skip)
                EffectStepDebug.Register(this);
#endif
        }

        public void SetData(Game data)
        {
            game_data = data;
        }

        public void SetDeathStep(Func<bool> death_step, Func<bool> has_deaths, Func<bool> death_suspended)
        {
            this.death_step = death_step;
            this.has_deaths = has_deaths;
            this.death_suspended = death_suspended;
        }

        public virtual void Update(float delta)
        {
            if (resolve_delay > 0f)
            {
                resolve_delay -= delta;
                if (resolve_delay <= 0f)
                    ResolveAll();
            }
        }

        // ======================= 적재 (Enqueue) =======================

        public virtual void AddAbility(AbilityData ability, Card caster, Card triggerer, int max_repeat, int current_repeat, Action<AbilityData, Card, Card, int, int> callback, bool is_chain = false)
        {
            if (ability == null || caster == null)
                return;

            AbilityQueueElement elem = ability_elem_pool.Create();
            elem.caster = caster;
            elem.triggerer = triggerer;
            elem.max_repeat = max_repeat;
            elem.current_repeat = current_repeat;
            elem.ability = ability;
            elem.callback = callback;

            //열려 있는 Phase가 없으면 이 효과 하나만 담는 최상위 Phase를 만들어 큐 끝에 붙인다.
            //(GameLogic의 Trigger*AbilityType들이 이벤트 단위로 감싸므로 보통은 여기 오지 않는다.
            // CastAbility처럼 단발로 등록하는 경로를 위한 폴백.)
            AbilityPhase phase = insert_stack.Count > 0 ? insert_stack.Peek() : NewTopPhase(false);

            //chain은 같은 Phase 안에서 일반 유발 트리거보다 먼저 소진된다 (요구사항 1).
            if (is_chain)
                phase.chains.Enqueue(elem);
            else
                phase.triggers.Enqueue(elem);
        }

        /// <summary>
        /// 어빌리티가 **유발한** 공격. 현재 Phase에 담아 depth-first를 유지한다.
        /// (구조적 공격 마이크로스텝은 AddAttack을 그대로 쓴다 — 그쪽은 최상위가 맞다.)
        /// </summary>
        public virtual void AddTriggeredAttack(Card attacker, Card target, Action<Card, Card, bool> callback, bool skip_cost = false)
        {
            if (attacker == null || target == null)
                return;
            if (insert_stack.Count == 0)
            {
                AddAttack(attacker, target, callback, skip_cost); //처리 중이 아니면 평소대로
                return;
            }

            AttackQueueElement elem = attack_elem_pool.Create();
            elem.attacker = attacker;
            elem.target = target;
            elem.ptarget = null;
            elem.atarget = new Slot(0, 0, -1);
            elem.skip_cost = skip_cost;
            elem.callback = callback;
            insert_stack.Peek().attacks.Enqueue(elem);
        }

        /// <summary>
        /// 이벤트 하나가 만드는 Phase를 연다. 여는 위치는 "지금 효과를 처리하는 중인가"로 갈린다:
        ///   * 처리 중  → 그 효과 스코프의 **자식**으로 (생성 순 FIFO, 부모의 남은 항목보다 먼저)
        ///   * 아님     → **최상위** Phase로 phase_queue 끝에 (형제 FIFO)
        /// EndPhase는 "더 이상 이 묶음에 적재하지 않겠다"는 뜻이지 Phase를 없애는 게 아니다.
        /// 내용물이 있으면 살아남아 순서대로 소진된다.
        /// </summary>
        public virtual void BeginPhase()
        {
            //selector로 중단됐던 스코프가 있으면 그걸 그대로 다시 연다 (재개 복원).
            if (suspended_scope != null)
            {
                AbilityPhase resumed = suspended_scope;
                suspended_scope = null;
                if (resumed.alive)
                {
                    resumed.active = true;
                    insert_stack.Push(resumed);
                    return;
                }
                //방어선: 스코프가 이미 회수된 상태였다 (DisposePhase가 끊어주므로 정상 흐름에서는
                //도달하지 않는다). 되살리면 안 되므로 새로 연다. 재개되는 효과는 "현재 Sequence의
                //연속"이니 대기 중인 형제들 뒤가 아니라 앞에 놓는다.
                Debug.LogError("ResolveQueue: suspended scope was already recycled — opening a fresh phase instead");
                AbilityPhase fallback = insert_stack.Count > 0
                    ? NewChildPhase(insert_stack.Peek(), true)
                    : NewTopPhase(true);
                fallback.active = true;
                insert_stack.Push(fallback);
                return;
            }

            AbilityPhase phase = insert_stack.Count > 0
                ? NewChildPhase(insert_stack.Peek())
                : NewTopPhase(false);
            phase.active = true;
            insert_stack.Push(phase);
        }

        /// <summary>
        /// 죽음 트리거 / 반복 회차처럼 "현재 Sequence를 이어가는" Phase. 대기 중인 다른 최상위
        /// Phase보다 **앞**에 끼워넣는다 (하스스톤: Death Phase는 Sequence의 다음 Phase보다 먼저).
        /// </summary>
        public virtual void BeginImmediatePhase()
        {
            AbilityPhase phase = insert_stack.Count > 0
                ? NewChildPhase(insert_stack.Peek())
                : NewTopPhase(true);
            phase.active = true;
            insert_stack.Push(phase);
        }

        public virtual void EndPhase()
        {
            if (insert_stack.Count == 0)
                return;
            AbilityPhase phase = insert_stack.Pop();
            phase.active = false;
            //비어 있으면 즉시 회수. 내용물이 있으면 남겨두고 순서대로 소진한다.
            if (!phase.HasAnything)
                DetachPhase(phase);
        }

        /// <summary>
        /// selector로 효과가 중단됐음을 알린다. 그 효과의 스코프를 살려두었다가 재개 시 복원한다.
        /// GameLogic.Resolve 경로에서만 호출된다.
        /// </summary>
        protected void SuspendScope(AbilityPhase scope)
        {
            suspended_scope = scope;
        }

        /// <summary>
        /// 선택이 **취소**됐음을 알린다 (GameLogic.CancelSelection). 취소는 그 어빌리티 자체를
        /// 없던 일로 하는 것이므로 중단돼 있던 스코프를 버린다.
        /// 버리지 않으면 다음에 호출되는 아무 BeginPhase()가 그 죽은 스코프를 물려받아,
        /// 무관한 이벤트의 트리거들이 취소된 어빌리티의 자식으로 들어간다 (= 최상위 Phase가 되지
        /// 못해 그 사이의 사망 처리가 밀린다).
        /// 중단은 ResolveCardAbilitySelector가 효과 적용 **전에** 걸리므로 스코프는 보통 비어 있다.
        /// 혹시 적재된 게 있으면 그건 이미 발생한 효과라 트리에 남겨 정상적으로 소진시킨다.
        /// </summary>
        public virtual void DiscardSuspendedScope()
        {
            AbilityPhase scope = suspended_scope;
            suspended_scope = null;
            if (scope != null && scope.alive && !scope.HasAnything)
                DetachPhase(scope);
        }

        AbilityPhase NewTopPhase(bool immediate)
        {
            AbilityPhase phase = phase_pool.Create();
            phase.Reset(null);
            phase.alive = true;
            if (immediate)
                phase_queue.Insert(0, phase);
            else
                phase_queue.Add(phase);
            return phase;
        }

        //front=true는 "지금 처리하는 효과의 스코프"용. 그 효과가 **이미 유발해둔** 형제 자식들보다
        //먼저 소진되어야 depth-first가 성립한다 (chain을 먼저 꺼낸 뒤 그 chain의 연쇄가 앞서 등록된
        //유발 이벤트를 추월해야 하는 경우). 일반 이벤트 Phase는 false = 생성 순 FIFO.
        AbilityPhase NewChildPhase(AbilityPhase parent, bool front = false)
        {
            AbilityPhase phase = phase_pool.Create();
            if (phase == parent)
            {
                //풀이 "지금 열려 있는 Phase"를 다시 발급했다 = 어딘가에서 이중 반납된 상태.
                //그대로 두면 parent.children에 자기 자신이 들어가 트리 순회가 무한 재귀한다.
                Debug.LogError("ResolveQueue: pool re-issued a phase that is still in use (double dispose) — recovering with an unpooled phase");
                phase = new AbilityPhase();
            }
            phase.Reset(parent);
            phase.alive = true;
            if (front)
                parent.children.Insert(0, phase);
            else
                parent.children.Add(phase);
            return phase;
        }

        void DetachPhase(AbilityPhase phase)
        {
            if (phase.parent != null)
                phase.parent.children.Remove(phase);
            else
                phase_queue.Remove(phase);
            DisposePhase(phase);
        }

        //Phase를 풀로 돌려보내는 **유일한** 창구. 여기서 이 Phase를 가리키던 필드들을 전부 끊는다.
        //(끊지 않으면 반납된 Phase가 나중에 되살아나 트리 두 곳에 동시에 존재하게 되고,
        // 결국 자기 자신을 자식으로 갖는 Phase가 만들어져 트리 순회가 스택을 태운다.)
        void DisposePhase(AbilityPhase phase)
        {
            if (phase == null || !phase.alive)
                return; //이미 반납됨 — 유령 참조를 통한 이중 반납
            phase.alive = false;
            phase.active = false;
            if (current_top == phase)
                current_top = null;
            if (suspended_scope == phase)
                suspended_scope = null;
            phase_pool.Dispose(phase);
        }

        public virtual void AddAttack(Card attacker, Card target, Action<Card, Card, bool> callback, bool skip_cost = false)
        {
            if (attacker != null && target != null)
            {
                AttackQueueElement elem = attack_elem_pool.Create();
                elem.attacker = attacker;
                elem.target = target;
                elem.ptarget = null;
                elem.atarget = new Slot(0, 0, -1);
                elem.skip_cost = skip_cost;
                elem.callback = callback;
                attack_queue.Enqueue(elem);
            }
        }

        public virtual void AddAttack(Card attacker, Player target, Action<Card, Player, bool> callback, bool skip_cost = false)
        {
            if (attacker != null && target != null)
            {
                AttackQueueElement elem = attack_elem_pool.Create();
                elem.attacker = attacker;
                elem.target = null;
                elem.ptarget = target;
                elem.atarget = new Slot(0, 0, -1);
                elem.skip_cost = skip_cost;
                elem.pcallback = callback;
                attack_queue.Enqueue(elem);
            }
        }

        public virtual void AddAttack(Card attacker, Action<Card, bool> callback, bool skip_cost = false)
        {
            if (attacker != null)
            {
                AttackQueueElement elem = attack_elem_pool.Create();
                elem.attacker = attacker;
                elem.target = null;
                elem.ptarget = null;
                elem.atarget = new Slot(0, 0, -1);
                elem.skip_cost = skip_cost;
                elem.scallback = callback;
                attack_queue.Enqueue(elem);
            }
        }

        public virtual void AddAttack(Card attacker, Slot target, Action<Card, Slot, bool> callback, bool skip_cost = false)
        {
            if (attacker != null && target != null)
            {
                AttackQueueElement elem = attack_elem_pool.Create();
                elem.attacker = attacker;
                elem.target = null;
                elem.ptarget = null;
                elem.atarget = target;
                elem.skip_cost = skip_cost;
                elem.acallback = callback;
                attack_queue.Enqueue(elem);
            }
        }

        public virtual void AddSecret(AbilityTrigger secret_trigger, Card secret, Card trigger, Action<AbilityTrigger, Card, Card> callback)
        {
            if (secret != null && trigger != null)
            {
                SecretQueueElement elem = secret_elem_pool.Create();
                elem.secret_trigger = secret_trigger;
                elem.secret = secret;
                elem.triggerer = trigger;
                elem.callback = callback;
                secret_queue.Enqueue(elem);
            }
        }

        public virtual void AddCallback(Action callback)
        {
            if (callback != null)
            {
                CallbackQueueElement elem = callback_elem_pool.Create();
                elem.callback = callback;
                elem.acallback = null;
                elem.attacker = null;
                elem.target = null;
                callback_queue.Enqueue(elem);
            }
        }

        public virtual void AddCallback(Card attacker, Card target, Action<Card, Card> callback)
        {
            if (callback != null)
            {
                CallbackQueueElement elem = callback_elem_pool.Create();
                elem.callback = null;
                elem.acallback = callback;
                elem.attacker = attacker;
                elem.target = target;
                callback_queue.Enqueue(elem);
            }
        }

        // ======================= 소진 (Resolve) =======================

        public virtual void Resolve()
        {
            //① 현재 최상위 Phase가 소진됐으면 놓아준다. 이 순간이 곧 하스스톤의 outermost Phase
            //   종료 = Death Creation Step 경계다.
            if (current_top != null && !current_top.active && FindNextPhase(current_top) == null)
            {
                AbilityPhase done = current_top;
                current_top = null;
                DetachPhase(done);
            }
            //빈 채로 남은 선두 Phase도 정리한다 (트리거 조건에 전부 걸려 아무것도 적재되지
            //않은 이벤트 등). 정리해두지 않으면 ③에서 빈 Phase를 붙잡아 죽음 경계를 놓친다.
            while (current_top == null && phase_queue.Count > 0)
            {
                AbilityPhase head = phase_queue[0];
                if (head.active || FindNextPhase(head) != null)
                    break;
                phase_queue.RemoveAt(0);
                DisposePhase(head);
            }

            //② Death Creation Step (Rule 3/4a): 최상위 Phase 경계에서만 돈다.
            //   묶음 안에 형제가 남아 있으면 current_top != null 이므로 여기 오지 않는다.
            if (current_top == null && death_step != null && has_deaths != null && has_deaths() && death_step())
                return; //죽음 처리 / 지연 반복이 새 Phase를 적재했다. 그걸 먼저 소진한다.

            //③ 처리할 최상위 Phase 확보
            if (current_top == null && phase_queue.Count > 0)
                current_top = phase_queue[0];

            //④ 다음 항목: 자식 Phase(생성 순) → 현재 Phase의 chain → trigger → 유발된 공격
            AbilityPhase src = current_top != null ? FindNextPhase(current_top) : null;
            if (src != null)
            {
                if (src.chains.Count > 0)
                    ResolveAbilityElement(src.chains.Dequeue(), src);
                else if (src.triggers.Count > 0)
                    ResolveAbilityElement(src.triggers.Dequeue(), src);
                else
                    ResolveTriggeredAttack(src.attacks.Dequeue(), src);
                return;
            }

            //⑤ 최상위 Phase가 없을 때만 base 큐. 우선순위는 개편 전과 동일.
            //   secret / attack 마이크로스텝 / callback은 **효과가 아니라 Sequence의 골격**이므로
            //   Phase로 감싸지 않는다. 이들이 띄우는 이벤트는 각자 최상위 Phase가 되어 FIFO로
            //   줄을 서고, 그 사이사이에 죽음 처리가 정상적으로 돈다 (하스스톤 Rule 6).
            //   개편 전에는 전부 감쌌는데, 그러면 한 콜백 안의 서로 다른 이벤트가 중첩 형제가 되어
            //   순서가 뒤집혔다 (독약 피해가 턴시작 배치보다 뒤로 밀리는 문제).
            if (secret_queue.Count > 0)
            {
                SecretQueueElement elem = secret_queue.Dequeue();
                secret_elem_pool.Dispose(elem);
                elem.callback?.Invoke(elem.secret_trigger, elem.secret, elem.triggerer);
            }
            else if (attack_queue.Count > 0)
            {
                //공격 마이크로스텝은 **Phase 자체**다 (하스스톤의 'Preparation' / 'Attack' Phase).
                //턴 진행 콜백 같은 Sequence 골격이 아니다. 그래서 스코프를 열고 그것을 곧바로
                //current_top으로 잡는다 — 이렇게 해야 이 스텝이 띄운 트리거(OnAfterDamage,
                //OnAfterAttack, OnAfterDefend…)가 **죽음 처리보다 먼저** 발동한다.
                //(Rule 4a: Death Creation Step은 Phase가 끝난 뒤. 그냥 최상위 Phase로 큐에 넣으면
                // ②의 죽음 게이트가 먼저 열려서 공격에 죽은 카드가 트리거보다 앞서 제거된다.)
                AttackQueueElement elem = attack_queue.Dequeue();
                attack_elem_pool.Dispose(elem);

                AbilityPhase scope = NewTopPhase(true);
                scope.active = true;
                insert_stack.Push(scope);
                InvokeAttack(elem);
                insert_stack.Pop();
                scope.active = false;

                if (scope.HasAnything)
                    current_top = scope;
                else
                    DetachPhase(scope);
            }
            else if (callback_queue.Count > 0)
            {
                CallbackQueueElement elem = callback_queue.Dequeue();
                callback_elem_pool.Dispose(elem);
                if (elem.target != null)
                    elem.acallback?.Invoke(elem.attacker, elem.target);
                else
                    elem.callback.Invoke();
            }
        }

        //어빌리티 하나를 처리한다. 처리 중 유발되는 것들을 담을 스코프를 owner의 자식으로 연다.
        void ResolveAbilityElement(AbilityQueueElement elem, AbilityPhase owner)
        {
            ability_elem_pool.Dispose(elem);

            AbilityPhase scope = NewChildPhase(owner, true);
            scope.active = true;
            insert_stack.Push(scope);

            elem.callback?.Invoke(elem.ability, elem.caster, elem.triggerer, elem.max_repeat, elem.current_repeat);

            insert_stack.Pop();
            scope.active = false;

            //selector로 중단됐으면 스코프를 살려둔다 (재개 시 BeginPhase가 복원).
            if (game_data != null && game_data.selector != SelectorType.None)
            {
                SuspendScope(scope);
                return;
            }
            if (!scope.HasAnything)
                DetachPhase(scope);
        }

        void ResolveTriggeredAttack(AttackQueueElement elem, AbilityPhase owner)
        {
            attack_elem_pool.Dispose(elem);
            AbilityPhase scope = NewChildPhase(owner, true);
            scope.active = true;
            insert_stack.Push(scope);
            InvokeAttack(elem);
            insert_stack.Pop();
            scope.active = false;
            if (!scope.HasAnything)
                DetachPhase(scope);
        }

        void InvokeAttack(AttackQueueElement elem)
        {
            if (elem.ptarget != null)
                elem.pcallback?.Invoke(elem.attacker, elem.ptarget, elem.skip_cost);
            else if (elem.target != null)
                elem.callback?.Invoke(elem.attacker, elem.target, elem.skip_cost);
            else if (elem.atarget.IsValid())
                elem.acallback?.Invoke(elem.attacker, elem.atarget, elem.skip_cost);
            else
                elem.scallback?.Invoke(elem.attacker, elem.skip_cost);
        }

        /// <summary>
        /// 항목이 남아 있는 가장 앞선 Phase를 찾는다.
        ///   자식 먼저(생성 순 FIFO) → 자기 항목.
        /// 자식이 완전히 소진되면 그 자리에서 회수한다. 없으면 null (= 이 Phase는 소진됨).
        /// </summary>
        protected AbilityPhase FindNextPhase(AbilityPhase p)
        {
            return FindNextPhase(p, 0);
        }

        AbilityPhase FindNextPhase(AbilityPhase p, int depth)
        {
            if (depth > max_phase_depth)
            {
                ReportPhaseTreeCorrupt("FindNextPhase");
                return null;
            }

            //chain은 "그 어빌리티의 후속 텍스트"라 **자신이 유발한 이벤트보다도 먼저** 온다
            //(문서 요구사항 1: "해당 어빌리티의 chain이 유발 효과보다 먼저").
            //자식보다 앞서 검사하지 않으면 유발 이벤트가 chain을 추월한다.
            if (p.chains.Count > 0)
                return p;

            while (p.children.Count > 0)
            {
                AbilityPhase c = p.children[0];
                AbilityPhase r = FindNextPhase(c, depth + 1);
                if (r != null)
                    return r;
                if (c.active)
                    return null;    //아직 채워지는 중 — 더 진행하지 않는다
                p.children.RemoveAt(0);
                DisposePhase(c);
            }
            return (p.triggers.Count > 0 || p.attacks.Count > 0) ? p : null;
        }

        //"어빌리티 항목이 하나라도 남아 있는가". 호출부는 전부 개수가 아니라 존재 여부만 쓰므로
        //(CanResolve / ResolveAll / GetNextQueueDelay) 첫 항목에서 즉시 빠져나온다 —
        //AI(skip_delay) 루프가 매 반복마다 트리 전체를 세던 비용이 사라진다.
        protected bool HasAnyElement()
        {
            for (int i = 0; i < phase_queue.Count; i++)
            {
                if (PhaseHasElement(phase_queue[i], 0))
                    return true;
            }
            return false;
        }

        bool PhaseHasElement(AbilityPhase p, int depth)
        {
            if (depth > max_phase_depth)
            {
                ReportPhaseTreeCorrupt("PhaseHasElement");
                return false;
            }
            if (p.HasItems)
                return true;
            for (int i = 0; i < p.children.Count; i++)
            {
                if (PhaseHasElement(p.children[i], depth + 1))
                    return true;
            }
            return false;
        }

        //Last-resort guard. Phase 트리는 자식이 소진되는 대로 잘려나가므로 실제 깊이는 십 단위를
        //넘지 않는다. 상한에 닿았다는 건 링크가 망가졌다는 뜻이고, 그대로 두면 재귀가
        //StackOverflowException(캐치 불가 = 에디터/빌드 즉사)을 낸다. 여기서는 표시만 하고
        //ResolveAll이 다음 진입에서 Clear()로 회수한다 (skip_resolve_max 가드와 동일한 정책).
        void ReportPhaseTreeCorrupt(string where)
        {
            if (phase_tree_corrupt)
                return;
            phase_tree_corrupt = true;
            Debug.LogError("ResolveQueue: phase tree exceeded depth " + max_phase_depth + " in " + where + " (corrupted parent/child link) — the queue will be cleared");
        }

        public virtual void ResolveAll(float delay)
        {
            SetDelay(delay);
            ResolveAll();  //Resolve now if no delay
        }

        public virtual void ResolveAll()
        {
            if (is_resolving)
                return;

            //깨진 Phase 링크가 감지됐으면 여기서 회수한다 (트리 순회 도중에 Clear()를 부를 수는 없다).
            if (phase_tree_corrupt)
            {
                Clear();
                return;
            }

            is_resolving = true;

            if (skip_delay)
            {
                //Last-resort guard (AI-only path): a resolve bug (e.g. a repeat re-queueing itself
                //without changing state) must abort with a log instead of spinning this worker
                //thread forever. A legitimate full-turn simulation stays well under the cap.
                int iterations = 0;
                while (CanResolve())
                {
                    Resolve();
                    if (++iterations >= skip_resolve_max)
                    {
                        Debug.LogError("ResolveQueue: aborted after " + skip_resolve_max + " iterations (resolve loop stuck, likely repeat/death-step cycle)");
                        Clear();
                        break;
                    }
                }
            }
            else if (CanResolve())
            {
                //EDITOR-ONLY 치트 훅. 빌드에서는 [Conditional] 덕분에 이 두 줄이 통째로 사라지고,
                //에디터에서도 치트가 꺼져 있으면 bool 하나 읽고 즉시 반환한다.
                EffectStepDebug.BeginStep(this);
                Resolve();
                EffectStepDebug.EndStep(this);

                if (HasPendingWork())
                    SetDelay(GetNextQueueDelay());
            }

            is_resolving = false;
        }

        public virtual void SetDelay(float delay)
        {
            if (!skip_delay)
            {
                resolve_delay = Mathf.Max(resolve_delay, delay);
            }
        }

        protected bool HasPendingDeaths()
        {
            return has_deaths != null && has_deaths();
        }

        //Will the Death Phase actually run at the next boundary? False while an in-flight attack holds
        //it back, even though HasPendingDeaths() is true. Only pacing cares about the distinction.
        protected bool IsDeathStepDue()
        {
            return HasPendingDeaths() && (death_suspended == null || !death_suspended());
        }

        //Default gap before the NEXT element resolves, picked by the queue it will come from.
        //Must mirror Resolve()'s priority order (Phase -> death phase at top-level boundaries ->
        //secret -> attack -> callback).
        protected virtual float GetNextQueueDelay()
        {
            if (skip_delay)
                return 0f;
            if (HasAnyElement())
                return GameConfig.Timing.ability;
            if (IsDeathStepDue())
                return GameConfig.Timing.ability; //Death Phase is next; paced like an ability
            //Deliberately IsDeathStepDue and not HasPendingDeaths: while an attack is in flight its
            //deaths are held back, so the next element is that attack's own next micro-step and it must
            //keep the tight attack tempo. Using HasPendingDeaths here would stretch every step after the
            //attack's first kill to Timing.ability (1s vs attack_step 0.05s).
            if (secret_queue.Count > 0)
                return GameConfig.Timing.secret;
            if (attack_queue.Count > 0)
                return GameConfig.Timing.attack;
            if (callback_queue.Count > 0)
                return GameConfig.Timing.callback;
            return 0f;
        }

        //"처리할 것이 남아 있는가". CanResolve의 마지막 절이자 ResolveAll의 hasMore 판정.
        //(에디터 치트의 일시정지 판정도 이걸 공유한다.)
        protected bool HasPendingWork()
        {
            return attack_queue.Count > 0 || HasAnyElement() || secret_queue.Count > 0 || callback_queue.Count > 0 || HasPendingDeaths();
        }

        public virtual bool CanResolve()
        {
            if (resolve_delay > 0f)
                return false;   //Is waiting delay
            if (game_data.state == GameState.GameEnded)
                return false; //Cant execute anymore when game is ended
            if (game_data.selector != SelectorType.None)
                return false; //Waiting for player input, in the middle of resolve loop
#if UNITY_EDITOR
            if (IsStepPaused())
                return false; //EDITOR-ONLY 치트: 스텝 버튼을 누를 때까지 다음 항목을 처리하지 않는다
#endif
            return HasPendingWork();
        }

        public virtual bool IsResolving()
        {
#if UNITY_EDITOR
            //치트로 멈춰 있는 동안에는 "처리 중"으로 보고한다. 이 한 줄로 GameServer의 턴 타이머,
            //대기 액션 처리, 플레이어 입력, AIPlayer의 행동이 전부 함께 멈춘다 — 멈춘 사이에 다른
            //행동이 끼어들어 큐 순서가 오염되는 것을 막기 위해서다.
            if (IsStepHolding())
                return true;
#endif
            return is_resolving || resolve_delay > 0f;
        }

#if UNITY_EDITOR
        // ======================= EDITOR-ONLY 치트 (EffectStepDebug) =======================
        // 전부 #if UNITY_EDITOR 안에 있고, 치트가 꺼져 있으면 IsPausing이 static bool 하나만 읽고
        // false를 돌려주므로 정상 동작 경로에는 실질적인 개입이 없다.

        //이 큐가 스텝 대기 상태인가. AI 예측 큐(skip_delay=true)는 절대 걸리지 않는다.
        private bool IsStepPaused()
        {
            return !skip_delay && EffectStepDebug.IsPausing(this);
        }

        //IsResolving을 true로 붙잡아야 하는 상태인가.
        //selector/mulligan 대기와 게임 종료는 **제외**해야 한다: 그 상태에서 IsResolving을 true로
        //만들면 GameServer가 플레이어의 선택/멀리건 입력까지 거부해 교착에 빠진다.
        private bool IsStepHolding()
        {
            if (!IsStepPaused())
                return false;
            if (game_data == null || game_data.state == GameState.GameEnded)
                return false;
            if (game_data.selector != SelectorType.None || game_data.phase == GamePhase.Mulligan)
                return false;
            return HasPendingWork();
        }

        //대기 중인 delay를 걷어내고 큐를 한 번 굴린다. 스텝 버튼과 치트 해제가 쓴다.
        //(스텝 대기 중인 큐는 resolve_delay가 0인 채로 잠들어 있어서 Update가 깨우지 못한다.)
        public void EditorResolveKick()
        {
            resolve_delay = 0f;
            ResolveAll();
        }

        public bool EditorHasPendingWork()
        {
            return HasPendingWork();
        }

        public bool EditorIsWaitingSelector()
        {
            return game_data != null && game_data.selector != SelectorType.None;
        }

        /// <summary>
        /// 지금 대기 중인 항목들을 **처리될 순서(추정)**로 문자열화한다. 읽기 전용 — 큐를 건드리지 않는다.
        /// Resolve()의 우선순위를 그대로 흉내낸다: 최상위 Phase(FIFO) → (첫 Phase가 끝나는 지점의)
        /// 사망 처리 → secret → 공격 마이크로스텝 → 턴 진행 콜백.
        /// 항목 하나가 처리되면서 새 이벤트가 튀어나오므로 어디까지나 "현재 스냅샷"이다.
        /// </summary>
        public void EditorCollectPending(EffectStepDebug.PendingList output)
        {
            if (output == null)
                return;

            for (int i = 0; i < phase_queue.Count; i++)
            {
                if (output.IsFull)
                    return;
                EditorCollectPhase(phase_queue[i], output, 0);

                //첫 최상위 Phase가 소진되는 지점이 Death Creation Step 경계다.
                if (i == 0 && IsDeathStepDue())
                    output.Add("사망", "Death Creation Step", null, 0);
            }

            if (phase_queue.Count == 0 && IsDeathStepDue())
                output.Add("사망", "Death Creation Step", null, 0);

            foreach (SecretQueueElement e in secret_queue)
            {
                if (output.IsFull) return;
                output.Add("비밀", EffectStepDebug.DescribeCard(e.secret), EffectStepDebug.DescribeCard(e.triggerer), 0);
            }
            foreach (AttackQueueElement e in attack_queue)
            {
                if (output.IsFull) return;
                output.Add("공격", EditorDescribeAttack(e), null,0);
            }
            if (callback_queue.Count > 0)
                output.Add("진행", "턴 진행 콜백 x" + callback_queue.Count, null, 0);
        }

        void EditorCollectPhase(AbilityPhase p, EffectStepDebug.PendingList output, int depth)
        {
            if (p == null || depth > max_phase_depth || output.IsFull)
                return;

            //FindNextPhase와 같은 순서: chain → 자식(생성 순) → 자기 trigger → 유발 공격
            foreach (AbilityQueueElement e in p.chains)
            {
                if (output.IsFull) return;
                EditorAddAbility(output, "연쇄", e, depth);
            }
            for (int i = 0; i < p.children.Count; i++)
                EditorCollectPhase(p.children[i], output, depth + 1);
            foreach (AbilityQueueElement e in p.triggers)
            {
                if (output.IsFull) return;
                EditorAddAbility(output, "유발", e, depth);
            }
            foreach (AttackQueueElement e in p.attacks)
            {
                if (output.IsFull) return;
                output.Add("유발공격", EditorDescribeAttack(e), null,depth);
            }
        }

        void EditorAddAbility(EffectStepDebug.PendingList output, string tag, AbilityQueueElement e, int depth)
        {
            string title = EffectStepDebug.DescribeAbility(e.ability);
            if (e.max_repeat > 1)
                title += " (" + (e.current_repeat + 1) + "/" + e.max_repeat + "회)";
            output.Add(tag, title, EffectStepDebug.DescribeCard(e.caster), depth);
        }

        string EditorDescribeAttack(AttackQueueElement e)
        {
            string atk = EffectStepDebug.DescribeCard(e.attacker);
            if (e.ptarget != null)
                return atk + " → 플레이어 " + e.ptarget.player_id;
            if (e.target != null)
                return atk + " → " + EffectStepDebug.DescribeCard(e.target);
            if (e.atarget.IsValid())
                return atk + " → " + EffectStepDebug.DescribeSlot(e.atarget);
            return atk;
        }
#endif

        public virtual void Clear()
        {
            attack_elem_pool.DisposeAll();
            ability_elem_pool.DisposeAll();
            secret_elem_pool.DisposeAll();
            callback_elem_pool.DisposeAll();
            //살아 있는 Phase를 트리로 순회하지 않고 풀의 in-use 집합으로 훑는다. 트리에서 떨어져
            //나온 고아 Phase까지 확실히 정리되고, 링크가 망가진 상태에서도 재귀로 터지지 않는다.
            foreach (AbilityPhase p in phase_pool.GetAllActive())
            {
                p.Reset(null);
                p.alive = false;
            }
            phase_pool.DisposeAll();
            attack_queue.Clear();
            secret_queue.Clear();
            callback_queue.Clear();
            phase_queue.Clear();
            insert_stack.Clear();
            current_top = null;
            suspended_scope = null;
            phase_tree_corrupt = false;
        }

        public Queue<AttackQueueElement> GetAttackQueue()
        {
            return attack_queue;
        }

        public Queue<SecretQueueElement> GetSecretQueue()
        {
            return secret_queue;
        }

        public Queue<CallbackQueueElement> GetCallbackQueue()
        {
            return callback_queue;
        }

        //디버그/검증용: 현재 최상위 Phase가 소진되었는가 (= 죽음 경계인가)
        public bool IsTopPhaseDrained()
        {
            return current_top == null || FindNextPhase(current_top) == null;
        }

        //디버그/검증용: Queue에 들어 있는 **최상위 Phase 개수**.
        //효과 처리 중에 뜬 이벤트는 여기 늘지 않는다 (현재 스코프의 자식으로 들어가므로).
        public int TopPhaseCount()
        {
            return phase_queue.Count;
        }

        //디버그/검증용: 지금 적재 대상 Phase의 **트리 깊이** (최상위 = 1).
        //효과가 resolve되는 동안에는 그 효과의 스코프까지 센 값이다.
        public int CurrentDepth()
        {
            if (insert_stack.Count == 0)
                return 0;
            int d = 0;
            for (AbilityPhase p = insert_stack.Peek(); p != null && d <= max_phase_depth; p = p.parent)
                d++;
            return d;
        }
    }

    public class AbilityQueueElement
    {
        public AbilityData ability;
        public Card caster;
        public Card triggerer;
        public int max_repeat;
        public int current_repeat;
        public Action<AbilityData, Card, Card, int, int> callback;
    }

    /// <summary>
    /// 하나의 이벤트가 만든 트리거 묶음, 또는 하나의 효과가 여는 처리 스코프.
    ///   chains / triggers : 자기 항목. chain이 먼저 소진된다 (요구사항 1).
    ///   attacks           : 이 스코프 안에서 유발된 공격 (AddTriggeredAttack).
    ///   children          : 이 안에서 열린 하위 Phase. **생성 순(FIFO)**으로, 자기 항목보다 먼저.
    ///   active            : 아직 적재 중(소유 요소가 resolve 중)이라 소진 판정을 하면 안 됨.
    /// </summary>
    public class AbilityPhase
    {
        public Queue<AbilityQueueElement> chains = new Queue<AbilityQueueElement>();
        public Queue<AbilityQueueElement> triggers = new Queue<AbilityQueueElement>();
        public Queue<AttackQueueElement> attacks = new Queue<AttackQueueElement>();
        public List<AbilityPhase> children = new List<AbilityPhase>();
        public AbilityPhase parent;
        public bool active;
        //False once the phase has been handed back to the pool. Any reference still pointing at a
        //dead phase is a dangling one and must not be re-opened or disposed again — a phase that is
        //free while still referenced can be re-issued by the pool to a caller that already holds it,
        //which produces a self-referencing children link and an infinite tree walk.
        public bool alive;

        public int ItemCount { get { return chains.Count + triggers.Count + attacks.Count; } }
        public bool HasItems { get { return ItemCount > 0; } }
        public bool HasAnything { get { return HasItems || children.Count > 0; } }

        public void Reset(AbilityPhase parent)
        {
            chains.Clear();
            triggers.Clear();
            attacks.Clear();
            children.Clear();
            this.parent = parent;
            active = false;
        }
    }

    public class AttackQueueElement
    {
        public Card attacker;
        public Card target;
        public Player ptarget;
        public Slot atarget;
        public bool skip_cost;
        public Action<Card, Card, bool> callback;
        public Action<Card, Player, bool> pcallback;
        public Action<Card, bool> scallback;
        public Action<Card, Slot, bool> acallback;
    }

    public class SecretQueueElement
    {
        public AbilityTrigger secret_trigger;
        public Card secret;
        public Card triggerer;
        public Action<AbilityTrigger, Card, Card> callback;
    }

    public class CallbackQueueElement
    {
        public Card attacker;
        public Card target;
        public Action callback;
        public Action<Card, Card> acallback;
    }
}
