using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TcgEngine
{
    /// <summary>
    /// Resolve abilties and actions one by one, with an optional delay in between each
    /// </summary>

    public class ResolveQueue
    {
        private Pool<AbilityQueueElement> ability_elem_pool = new Pool<AbilityQueueElement>();
        private Pool<SecretQueueElement> secret_elem_pool = new Pool<SecretQueueElement>();
        private Pool<AttackQueueElement> attack_elem_pool = new Pool<AttackQueueElement>();
        private Pool<CallbackQueueElement> callback_elem_pool = new Pool<CallbackQueueElement>();
        private Pool<AbilityPhase> phase_pool = new Pool<AbilityPhase>();

        //ability_queue holds the initial simultaneous trigger batch of a top-level action.
        //Abilities triggered WHILE an element is resolving do not go here: they go into the
        //phase of the resolving element (see phase_stack) so they resolve depth-first,
        //before anything that was already waiting. See docs/resolve-queue-hearthstone-redesign.md
        private Queue<AbilityQueueElement> ability_queue = new Queue<AbilityQueueElement>();
        private Queue<SecretQueueElement> secret_queue = new Queue<SecretQueueElement>();
        private Queue<AttackQueueElement> attack_queue = new Queue<AttackQueueElement>();
        private Queue<CallbackQueueElement> callback_queue = new Queue<CallbackQueueElement>();

        //Depth-first phases. Every resolving element (ability/secret/attack/callback) opens
        //a phase; abilities it triggers are added there. Phases resolve top-first, and within
        //a phase chain abilities resolve before effect-triggered abilities.
        //phase_stack: last item = top. insert_stack: where AddAbility currently routes to
        //(a stack because a selector completion can nest inside another resolution).
        private List<AbilityPhase> phase_stack = new List<AbilityPhase>();
        private Stack<AbilityPhase> insert_stack = new Stack<AbilityPhase>();

        //Per-queue "gap before the next element" lives in GameConfig.Timing (single source of truth).
        //The AI/skip_delay path never applies delays (see ResolveAll/SetDelay).

        //Death Phase hooks (Phase 2), set by GameLogic. death_step runs at every outermost
        //boundary — whenever an element and its whole depth-first subtree have finished
        //(phase stack empty), before the next waiting element (Hearthstone rule). It removes
        //dying cards, queues their death triggers, and at the stable point re-enqueues
        //deferred repeat iterations; returns true if it did any of that. has_deaths gates the
        //call and keeps the resolve loop alive (CanResolve) while a death step or deferred
        //repeat is pending. See docs/resolve-queue-hearthstone-redesign.md (Phase 2)
        private Func<bool> death_step;
        private Func<bool> has_deaths;

        private Game game_data;
        private bool is_resolving = false;
        private float resolve_delay = 0f;
        private bool skip_delay = false;

        public ResolveQueue(Game data, bool skip)
        {
            game_data = data;
            skip_delay = skip;
        }

        public void SetData(Game data)
        {
            game_data = data;
        }

        public void SetDeathStep(Func<bool> death_step, Func<bool> has_deaths)
        {
            this.death_step = death_step;
            this.has_deaths = has_deaths;
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

        public virtual void AddAbility(AbilityData ability, Card caster, Card triggerer, int max_repeat, int current_repeat, Action<AbilityData, Card, Card, int, int> callback, bool is_chain = false)
        {
            if (ability != null && caster != null)
            {
                AbilityQueueElement elem = ability_elem_pool.Create();
                elem.caster = caster;
                elem.triggerer = triggerer;
                elem.max_repeat = max_repeat;
                elem.current_repeat = current_repeat;
                elem.ability = ability;
                elem.callback = callback;

                if (insert_stack.Count > 0)
                {
                    //Triggered while an element is resolving: goes into that element's phase
                    //(depth-first). Chains resolve before effect-triggered abilities.
                    AbilityPhase phase = insert_stack.Peek();
                    if (is_chain)
                        phase.chains.Enqueue(elem);
                    else
                        phase.triggers.Enqueue(elem);
                }
                else
                {
                    //Top-level simultaneous batch (e.g. OnPlay + OnPlayOther after a card is played)
                    ability_queue.Enqueue(elem);
                }
            }
        }

        //Open a phase: abilities added until EndPhase resolve before everything already queued.
        //Called automatically around each resolving element; GameLogic also calls it manually
        //around selector completions (which apply effects outside of Resolve()).
        public virtual void BeginPhase()
        {
            AbilityPhase phase = phase_pool.Create();
            phase.active = true;
            phase_stack.Add(phase);
            insert_stack.Push(phase);
        }

        public virtual void EndPhase()
        {
            if (insert_stack.Count == 0)
                return;
            AbilityPhase phase = insert_stack.Pop();
            phase.active = false;
            if (phase.Count == 0)
            {
                phase_stack.Remove(phase);
                phase_pool.Dispose(phase);
            }
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

        public virtual void Resolve()
        {
            //Death Phase (Hearthstone rule): runs whenever an outermost element and its whole
            //depth-first subtree have finished (phase stack empty) — between waiting elements,
            //never mid-subtree. Gated on has_deaths so idle boundaries skip the full step.
            PruneDrainedPhases();
            if (phase_stack.Count == 0 && death_step != null && has_deaths != null && has_deaths() && death_step())
                return; //Deaths processed and/or deferred repeat iterations queued; resolve those next

            //Each resolving element opens its own phase so anything it triggers resolves
            //depth-first (before elements that were already waiting).
            AbilityQueueElement aelem = DequeueAbilityElement();
            if (aelem != null)
            {
                //Resolve Ability
                ability_elem_pool.Dispose(aelem);
                BeginPhase();
                aelem.callback?.Invoke(aelem.ability, aelem.caster, aelem.triggerer, aelem.max_repeat, aelem.current_repeat);
                EndPhase();
            }
            else if (secret_queue.Count > 0)
            {
                //Resolve Secret
                SecretQueueElement elem = secret_queue.Dequeue();
                secret_elem_pool.Dispose(elem);
                BeginPhase();
                elem.callback?.Invoke(elem.secret_trigger, elem.secret, elem.triggerer);
                EndPhase();
            }
            else if (attack_queue.Count > 0)
            {
                //Resolve Attack
                AttackQueueElement elem = attack_queue.Dequeue();
                attack_elem_pool.Dispose(elem);
                BeginPhase();
                if (elem.ptarget != null)
                    elem.pcallback?.Invoke(elem.attacker, elem.ptarget, elem.skip_cost);
                else if (elem.target != null)
                    elem.callback?.Invoke(elem.attacker, elem.target, elem.skip_cost);
                else if (elem.atarget.IsValid())
                    elem.acallback?.Invoke(elem.attacker, elem.atarget, elem.skip_cost);
                else
                    elem.scallback?.Invoke(elem.attacker, elem.skip_cost);
                EndPhase();
            }
            else if (callback_queue.Count > 0)
            {
                CallbackQueueElement elem = callback_queue.Dequeue();
                callback_elem_pool.Dispose(elem);
                BeginPhase();
                if (elem.target != null)
                    elem.acallback?.Invoke(elem.attacker, elem.target);
                else
                    elem.callback.Invoke();
                EndPhase();
            }
        }

        //Next ability to resolve: topmost non-empty phase first (chains before triggers),
        //then the base queue. Cleans up drained inactive phases from the top.
        protected virtual AbilityQueueElement DequeueAbilityElement()
        {
            PruneDrainedPhases();

            for (int i = phase_stack.Count - 1; i >= 0; i--)
            {
                AbilityPhase phase = phase_stack[i];
                if (phase.chains.Count > 0)
                    return phase.chains.Dequeue();
                if (phase.triggers.Count > 0)
                    return phase.triggers.Dequeue();
            }

            if (ability_queue.Count > 0)
                return ability_queue.Dequeue();
            return null;
        }

        protected void PruneDrainedPhases()
        {
            while (phase_stack.Count > 0)
            {
                AbilityPhase top = phase_stack[phase_stack.Count - 1];
                if (top.Count > 0 || top.active)
                    break;
                phase_stack.RemoveAt(phase_stack.Count - 1);
                phase_pool.Dispose(top);
            }
        }

        protected int CountAbilityElements()
        {
            int count = ability_queue.Count;
            for (int i = 0; i < phase_stack.Count; i++)
                count += phase_stack[i].Count;
            return count;
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

            is_resolving = true;

            if (skip_delay)
            {
                while (CanResolve())
                    Resolve();
            }
            else if (CanResolve())
            {
                Resolve();
                bool hasMore = CountAbilityElements() > 0 || secret_queue.Count > 0 || attack_queue.Count > 0 || callback_queue.Count > 0 || HasPendingDeaths();
                if (hasMore)
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

        //Default gap before the NEXT element resolves, picked by the queue it will come from.
        //Must mirror Resolve()'s priority order (death phase at outermost boundaries ->
        //ability -> secret -> attack -> callback).
        protected virtual float GetNextQueueDelay()
        {
            if (skip_delay)
                return 0f;
            if (CountAbilityElements() > 0)
                return GameConfig.Timing.ability;
            if (HasPendingDeaths())
                return GameConfig.Timing.ability; //Death Phase is next; paced like an ability
            if (secret_queue.Count > 0)
                return GameConfig.Timing.secret;
            if (attack_queue.Count > 0)
                return GameConfig.Timing.attack;
            if (callback_queue.Count > 0)
                return GameConfig.Timing.callback;
            return 0f;
        }

        public virtual bool CanResolve()
        {
            if (resolve_delay > 0f)
                return false;   //Is waiting delay
            if (game_data.state == GameState.GameEnded)
                return false; //Cant execute anymore when game is ended
            if (game_data.selector != SelectorType.None)
                return false; //Waiting for player input, in the middle of resolve loop
            return attack_queue.Count > 0 || CountAbilityElements() > 0 || secret_queue.Count > 0 || callback_queue.Count > 0 || HasPendingDeaths();
        }

        public virtual bool IsResolving()
        {
            return is_resolving || resolve_delay > 0f;
        }

        public virtual void Clear()
        {
            attack_elem_pool.DisposeAll();
            ability_elem_pool.DisposeAll();
            secret_elem_pool.DisposeAll();
            callback_elem_pool.DisposeAll();
            phase_pool.DisposeAll();
            attack_queue.Clear();
            ability_queue.Clear();
            secret_queue.Clear();
            callback_queue.Clear();
            for (int i = 0; i < phase_stack.Count; i++)
            {
                phase_stack[i].chains.Clear();
                phase_stack[i].triggers.Clear();
            }
            phase_stack.Clear();
            insert_stack.Clear();
        }

        public Queue<AttackQueueElement> GetAttackQueue()
        {
            return attack_queue;
        }

        public Queue<AbilityQueueElement> GetAbilityQueue()
        {
            return ability_queue;
        }

        public Queue<SecretQueueElement> GetSecretQueue()
        {
            return secret_queue;
        }

        public Queue<CallbackQueueElement> GetCallbackQueue()
        {
            return callback_queue;
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

    //One depth-first resolution phase: everything triggered by a single resolving element.
    //Chains resolve before effect-triggered abilities. 'active' = the owning element is
    //still resolving (the phase may still receive elements and must not be pruned).
    public class AbilityPhase
    {
        public Queue<AbilityQueueElement> chains = new Queue<AbilityQueueElement>();
        public Queue<AbilityQueueElement> triggers = new Queue<AbilityQueueElement>();
        public bool active;

        public int Count { get { return chains.Count + triggers.Count; } }
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
