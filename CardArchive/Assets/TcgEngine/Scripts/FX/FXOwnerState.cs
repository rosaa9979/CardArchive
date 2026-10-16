using System.Collections.Generic;
using TcgEngine.Client;
using UnityEngine;

namespace TcgEngine.FX
{
    // Shared by every FX category. Binding never evaluates an Animator or fires events.
    public sealed class FXOwnerState : MonoBehaviour
    {
        private int owner;
        private Animator[] animators;
        private readonly Dictionary<Animator, RuntimeAnimatorController> controllers = new Dictionary<Animator, RuntimeAnimatorController>();
        private string appliedState;

        public static string GetState(int ownerPlayerId)
        {
            var client = GameClient.Get();
            return client != null && client.GetPlayerID() == ownerPlayerId ? FXSetting.state_self : FXSetting.state_enemy;
        }

        public static void Bind(GameObject fx, int? ownerPlayerId)
        {
            if (fx == null || !ownerPlayerId.HasValue) return;
            var binding = fx.GetComponent<FXOwnerState>();
            if (binding == null)
            {
                var candidates = new List<Animator>(fx.GetComponentsInChildren<Animator>(true));
                foreach (var setting in fx.GetComponentsInChildren<FXSetting>(true))
                    if (setting.animator != null && !candidates.Contains(setting.animator)) candidates.Add(setting.animator);
                if (!candidates.Exists(a => HasState(a, FXSetting.state_self) || HasState(a, FXSetting.state_enemy))) return;
                binding = fx.AddComponent<FXOwnerState>();
                binding.animators = candidates.ToArray();
            }
            binding.owner = ownerPlayerId.Value;
            binding.Refresh();
        }

        private static int StateHash(Animator animator, string state) => Animator.StringToHash(animator.GetLayerName(0) + "." + state);
        private static bool HasState(Animator animator, string state) => animator != null && animator.runtimeAnimatorController != null
            && animator.layerCount > 0 && animator.HasState(0, StateHash(animator, state));

        private void Update() { Refresh(); }

        private void Refresh()
        {
            if (animators == null) return;
            string state = GetState(owner);
            foreach (var animator in animators)
            {
                if (!HasState(animator, state)) continue; // Legacy single-state FX keep their authored default.
                bool known = controllers.TryGetValue(animator, out var controller);
                if (appliedState == state && known && controller == animator.runtimeAnimatorController) continue;
                float phase = known ? animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f : 0f;
                animator.Play(StateHash(animator, state), 0, phase);
                controllers[animator] = animator.runtimeAnimatorController;
            }
            appliedState = state;
        }
    }
}
