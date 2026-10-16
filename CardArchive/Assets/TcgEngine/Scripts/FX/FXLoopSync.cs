using TcgEngine.Client;
using UnityEngine;

namespace TcgEngine.FX
{
    // Only persistent, event-free decorations opt into shared presentation time.
    public sealed class FXLoopSync : MonoBehaviour
    {
        private Animator target;
        private float period;
        private float originalSpeed;
        private bool originalFireEvents;

        public static void Bind(GameObject fx)
        {
            var setting = fx != null ? fx.GetComponent<FXSetting>() : null;
            if (setting == null || !setting.synchronize_loop) return;
            var animator = setting.animator != null ? setting.animator : fx.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null || animator.layerCount != 1
                || setting.loop_period <= 0f || fx.GetComponentInChildren<FXPlayback>(true) != null)
            {
                Debug.LogWarning("Loop synchronization needs a single-layer decorative Animator and a positive period.", fx);
                return;
            }
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip == null || !clip.isLooping || clip.events.Length != 0)
                {
                    Debug.LogWarning("Loop synchronization only supports looping clips without animation events.", fx);
                    return;
                }
            var sync = fx.GetComponent<FXLoopSync>();
            if (sync != null) return;
            sync = fx.AddComponent<FXLoopSync>();
            sync.target = animator;
            sync.period = setting.loop_period;
            sync.originalSpeed = animator.speed;
            sync.originalFireEvents = animator.fireEvents;
            sync.TakeControl();
        }

        private void TakeControl()
        {
            if (target == null) return;
            target.speed = 0f;
            target.fireEvents = false;
            Sample();
        }

        private void OnEnable() { TakeControl(); }
        private void OnDisable()
        {
            if (target == null) return;
            target.speed = originalSpeed;
            target.fireEvents = originalFireEvents;
        }

        // Owner selection runs in Update; sample the selected state afterwards.
        private void LateUpdate() { Sample(); }
        private void Sample()
        {
            if (target == null || !target.isActiveAndEnabled || target.runtimeAnimatorController == null) return;
            target.Update(0f);
            var state = target.GetCurrentAnimatorStateInfo(0);
            if (!state.loop) return;
            target.Play(state.fullPathHash, 0, PresentationLoopClock.Phase(period));
            target.Update(0f);
        }
    }
}
