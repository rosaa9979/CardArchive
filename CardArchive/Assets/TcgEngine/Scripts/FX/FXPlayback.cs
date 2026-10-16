using System;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Client;

namespace TcgEngine.FX
{
    // Clips call Impact(resultIndex), optionally Continue(), then Complete().
    // Bind confirmed results BEFORE the Animator's first update. Clips carry no damage values.
    public sealed class FXPlayback : MonoBehaviour
    {
        private static bool shuttingDown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetShutdown() { shuttingDown = false; }

        private void OnApplicationQuit() { shuttingDown = true; }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorShutdown()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= OnEditorPlayState;
            UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayState;
        }

        private static void OnEditorPlayState(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) shuttingDown = true;
            else if (state == UnityEditor.PlayModeStateChange.ExitingEditMode) shuttingDown = false;
        }
#endif

        private Action[] impacts;
        private bool[] fired;
        private Action complete;
        private Action continuation;
        private Action ongoingStart;
        private bool continueRequested;
        private bool progressionReleased;
        private bool finished;
        private float deadline;
        private Animator playbackAnimator;
        private bool armed;
        [HideInInspector] public float cueProgress; // Binding for the generic particle cue timeline.
        private GameObject root;
        private AnimatorOverrideController ownedController;
        private readonly List<AnimationClip> ownedClips = new List<AnimationClip>();
        public bool HasPendingImpact => impacts != null && !finished && Array.Exists(fired, value => !value);
        public bool IsPlaying => impacts != null && !finished;

        // Existing FX keep their Animator and appearance. Authored Impact/Complete events
        // win; legacy one-shot clips get a first-visible-frame Impact and an end marker on
        // PRIVATE runtime copies. Asset clips/controllers are never modified at runtime.
        public static FXPlayback Prepare(GameObject fx, bool? appliesStateOverride = null)
        {
            var setting = fx.GetComponentInChildren<FXSetting>(true);
            bool appliesState = appliesStateOverride ?? (setting == null || setting.presentation_impact);
            Animator animator = setting != null && setting.animator != null
                ? setting.animator : fx.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                // Particle-only spawn/death FX use the same animation-event contract.
                // The neutral timeline does not animate visuals; artists can replace it
                // with an authored controller to choose a later Impact frame.
                var timeline = Resources.Load<RuntimeAnimatorController>("Presentation/StateCue");
                if (timeline == null) return null;
                float duration = FXTool.GetDuration(fx);
                animator = fx.AddComponent<Animator>();
                animator.runtimeAnimatorController = timeline;
                animator.speed = 1f / Mathf.Max(0.01f, duration);
            }
            if (animator.runtimeAnimatorController == null) return null;
            var source = animator.runtimeAnimatorController;
            var clips = source.animationClips;
            if (clips.Length == 0) return null;
            foreach (var clip in clips) if (clip.isLooping) return null; // Persistent/idle FX do not gate a state.
            var playback = animator.GetComponent<FXPlayback>() ?? animator.gameObject.AddComponent<FXPlayback>();
            playback.playbackAnimator = animator;
            animator.enabled = false; // Registration must close before any clip can signal Impact.
            playback.root = fx;
            playback.ownedController = new AnimatorOverrideController(source);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            // Constructing an override controller from another one unwraps its base
            // controller. Retain the authored replacement clips and their event times.
            if (source is AnimatorOverrideController authoredOverrides)
                authoredOverrides.GetOverrides(overrides);
            else
                playback.ownedController.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
            {
                AnimationClip original = overrides[i].Value != null ? overrides[i].Value : overrides[i].Key;
                var events = new List<AnimationEvent>();
                bool impact = false, complete = false;
                foreach (var marker in original.events)
                {
                    // Destruction belongs to Complete now, so it cannot preempt publication.
                    if (marker.functionName == "OnAnimationFinished") continue;
                    impact |= marker.functionName == "Impact" && marker.intParameter == 0;
                    complete |= marker.functionName == "Complete";
                    events.Add(marker);
                }
                if (appliesState && !impact) events.Add(new AnimationEvent { time = 0f, functionName = "Impact", intParameter = 0 });
                if (!complete) events.Add(new AnimationEvent { time = original.length, functionName = "Complete" });
                events.Sort((a, b) => {
                    int order = a.time.CompareTo(b.time);
                    if (order != 0) return order;
                    int Rank(string name) => name == "Complete" ? 3 : name == "Continue" ? 2 : name == "StartOngoing" ? 1 : 0;
                    return Rank(a.functionName).CompareTo(Rank(b.functionName));
                });
                AnimationClip copy = Instantiate(original);
                copy.events = events.ToArray();
                playback.ownedClips.Add(copy);
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, copy);
            }
            playback.ownedController.ApplyOverrides(overrides);
            animator.runtimeAnimatorController = playback.ownedController;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.fireEvents = true;
            return playback;
        }

        public void Bind(Action<int> onImpact, Action onComplete, Action onContinue = null)
        {
            Bind(new Action[] { () => onImpact(0) }, onComplete, onContinue);
        }

        // Multiple hits/targets bind one confirmed result per index in authored event order.
        public void Bind(IReadOnlyList<Action> results, Action onComplete, Action onContinue = null)
        {
            if (impacts != null || finished) throw new InvalidOperationException("FX playback can only be bound once");
            impacts = new Action[results.Count];
            for (int i = 0; i < results.Count; i++) impacts[i] = results[i];
            fired = new bool[results.Count];
            complete = onComplete;
            continuation = onContinue;
            PresentationClock.Track(() => this == null || finished || progressionReleased);
        }

        // Registered before Arm. This marker only opens a view gate; it never applies a result.
        public void BindOngoingStart(Action onStart)
        {
            if (armed || finished || ongoingStart != null) throw new InvalidOperationException("Bind ongoing before arming FX");
            ongoingStart = onStart;
        }

        public void StartOngoing(AnimationEvent marker)
        {
            if (marker == null || !marker.isFiredByAnimator || !armed || finished) return;
            ReleaseOngoingStart();
        }

        private void ReleaseOngoingStart()
        {
            var callback = ongoingStart;
            ongoingStart = null;
            if (!shuttingDown) callback?.Invoke();
        }

        public void Arm()
        {
            if (finished || armed || impacts == null) return;
            armed = true;
            deadline = Time.time + 30f;
            playbackAnimator.enabled = true;
        }

        // Only a real Animator event may release a result. Direct calls cannot forge it.
        public void Impact(AnimationEvent marker)
        {
            if (marker == null || !marker.isFiredByAnimator || !armed || finished || impacts == null) return;
            // Visual-only instances may share the exact same clip (and Impact events).
            // Their completion still participates in the effect boundary.
            if (impacts.Length == 0) return;
            int index = marker.intParameter;
            if (index < 0 || index >= impacts.Length)
            {
                Debug.LogError("FX animation referenced an unbound result index: " + index, this);
                return;
            }
            if (fired[index]) return;
            fired[index] = true;
            impacts[index]?.Invoke();
            TryContinue();
        }

        // An optional authored Animator event. It never stops/destroys the visual,
        // never fabricates Impact, and is deferred until this FX delivered all results.
        public void Continue(AnimationEvent marker)
        {
            if (marker == null || !marker.isFiredByAnimator || !armed || finished || impacts == null) return;
            continueRequested = true;
            TryContinue();
        }

        private void TryContinue()
        {
            if (!continueRequested || progressionReleased || HasPendingImpact || continuation == null) return;
            progressionReleased = true;
            var callback = continuation;
            continuation = null;
            callback();
        }

        public void Complete(AnimationEvent marker)
        {
            if (marker == null || !marker.isFiredByAnimator || !armed || finished) return;
            if (HasPendingImpact)
                Debug.LogWarning("FX completed without Impact; Presentation will synchronize at the effect boundary", this);
            // Without an explicit StartOngoing marker, ongoing begins when spawn completes.
            ReleaseOngoingStart();
            finished = true;
            complete?.Invoke();
            complete = null;
            continuation = null;
            Destroy(root != null ? root : gameObject);
        }

        public void Cancel()
        {
            // A reconnect/scene reset supplies a new baseline; old results must not run.
            finished = true;
            ReleaseOngoingStart(); // Cleanup only; canceled generations cannot open a newer spawn.
            complete = null;
            continuation = null;
            Destroy(root != null ? root : gameObject);
        }

        private void ReportMissingImpacts()
        {
            if (impacts == null) return;
            for (int i = 0; i < impacts.Length; i++)
                if (!fired[i])
                {
                    Debug.LogError("FX ended before Impact(" + i + "); presentation remains blocked and numbers are unchanged", this);
                }
        }

        private void Update()
        {
            // A broken asset fails closed. Neither timeout nor destruction can invent Impact.
            if (armed && !finished && Time.time >= deadline)
            {
                Debug.LogError("FX animation did not send Complete within 30 seconds", this);
                ReportMissingImpacts();
                Cancel();
            }
        }

        private void OnDestroy()
        {
            if (!finished)
            {
                // Scene teardown order is unspecified: FX may die before GameClient cancels them.
                // A real mid-game destruction still reports missing results and never invents Impact.
                if (!shuttingDown) ReportMissingImpacts();
                finished = true;
                ReleaseOngoingStart();
                complete = null;
                continuation = null;
            }
            foreach (var clip in ownedClips) if (clip != null) Destroy(clip);
            if (ownedController != null) Destroy(ownedController);
        }
    }
}
