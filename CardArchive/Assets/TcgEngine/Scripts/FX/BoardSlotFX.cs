using System.Collections;
using System.Collections.Generic;
using TcgEngine.Client;
using TcgEngine.UI;
using UnityEngine;

namespace TcgEngine.FX
{
    /// <summary>
    /// 타일에 적용되는 모든 이펙트
    /// </summary>
    /// 
    /// 
    public class BoardSlotFX : MonoBehaviour
    {
        private BoardSlot bslot;
        private Animator bslot_animator;
        private bool range_selected;
        private bool use_range_hatching;
        [Header("Range hatching motion")]
        [Min(0.01f)] public float range_enter_duration = 0.18f;
        [Min(0.01f)] public float range_exit_duration = 0.15f;
        private BSlot range_anchor;
        private string range_caster;
        private string range_ability;
        private bool restart_range;
        private enum RangePhase { Hidden, Entering, Holding, Exiting }
        private RangePhase range_phase;
        private float range_started;
        private float range_reveal;
        private float exit_reveal;
        private MaterialPropertyBlock range_properties;
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int DashSizeId = Shader.PropertyToID("_DashSize");

        void Awake()
        {
            bslot = GetComponent<BoardSlot>();
            bslot_animator = GetComponent<Animator>();
            var overlay = bslot.overlay_renderer;
            use_range_hatching = overlay != null && overlay.sharedMaterial != null
                && overlay.sharedMaterial.shader.name == "TcgEngine/RangeHatching";
            if (use_range_hatching)
                overlay.enabled = false;
        }

        void Start()
        {
            GameClient client = GameClient.Get();

            client.onAbilityStart += OnAbilityStart;
            client.onAbilityTargetSlot += OnAbilityEffect;
            client.onAbilityEnd += OnAbilityAfter;
        }

        private void OnDestroy()
        {
            GameClient client = GameClient.Get();

            client.onAbilityStart -= OnAbilityStart;
            client.onAbilityTargetSlot -= OnAbilityEffect;
            client.onAbilityEnd -= OnAbilityAfter;
        }

        void Update()
        {

        }

        void LateUpdate()
        {
            if (!use_range_hatching)
                return;

            UpdateRangeMotion(Time.unscaledTime);
        }

        private void UpdateRangeMotion(float now)
        {
            if (TcgEngine.Replay.ReplaySession.Active)
            {
                range_phase = RangePhase.Hidden;
                range_selected = false;
                restart_range = false;
                bslot.overlay_renderer.enabled = false;
                return;
            }

            // Resolve after the frame's reset/select calls. A changed anchor restarts
            // every affected tile, including tiles shared by the old and new ranges.
            if (range_selected && (restart_range || range_phase == RangePhase.Hidden || range_phase == RangePhase.Exiting))
            {
                range_phase = RangePhase.Entering;
                range_started = now;
            }
            else if (!range_selected && (range_phase == RangePhase.Entering || range_phase == RangePhase.Holding))
            {
                range_phase = RangePhase.Exiting;
                range_started = now;
                exit_reveal = range_reveal;
            }
            restart_range = false;

            float elapsed = Mathf.Max(0f, now - range_started);
            if (range_phase == RangePhase.Entering)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, range_enter_duration));
                range_reveal = 1f - Mathf.Pow(1f - t, 3f);
                if (t >= 1f)
                    range_phase = RangePhase.Holding;
            }
            if (range_phase == RangePhase.Holding)
            {
                range_reveal = 1f;
            }
            if (range_phase == RangePhase.Exiting)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, range_exit_duration));
                range_reveal = exit_reveal * (1f - t * t);
                if (t >= 1f)
                    range_phase = RangePhase.Hidden;
            }

            bslot.overlay_renderer.enabled = range_phase != RangePhase.Hidden;
            if (range_properties == null)
                range_properties = new MaterialPropertyBlock();
            bslot.overlay_renderer.GetPropertyBlock(range_properties);
            range_properties.SetFloat(RevealId, range_reveal);
            range_properties.SetFloat(DashSizeId, 1f);
            bslot.overlay_renderer.SetPropertyBlock(range_properties);
        }

        void OnDisable()
        {
            range_selected = false;
            range_phase = RangePhase.Hidden;
            restart_range = false;
            if (use_range_hatching && bslot != null && bslot.overlay_renderer != null)
                bslot.overlay_renderer.enabled = false;
        }

        private void OnAbilityStart(AbilityData iability, Card caster)
        {
            if (iability != null && caster != null)
            {

            }
        }

        private void OnAbilityAfter(AbilityData iability, Card caster)
        {
            if (iability != null && caster != null)
            {

            }
        }

        private void OnAbilityEffect(AbilityData iability, Card caster, Slot target)
        {
            if (iability != null && caster != null && target != null)
            {
                if (target == bslot.GetSlot())
                {
                    FXTool.DoSnapFX(iability.target_fx, bslot.transform);
                    AudioTool.Get().PlaySFX("ability_effect", iability.target_audio);
                }
                /*
                if (caster.uid == bcard.GetCardUID())
                {
                    if (iability.charge_target && caster.CardData.IsBoardCard())
                    {
                        BoardCard btarget = BoardCard.Get(target.uid);
                        ChargeInto(btarget);
                    }
                }
                */
            }
        }


        public void SetAnimParameter(bool is_selected)
        {
            range_selected = is_selected;
            bslot_animator.SetBool("is_selected", is_selected);
        }

        public void SetRangeTarget(BSlot anchor, string caster, string ability)
        {
            restart_range |= range_anchor != anchor || range_caster != caster || range_ability != ability;
            range_anchor = anchor;
            range_caster = caster;
            range_ability = ability;
            SetAnimParameter(true);
        }

        public void SetSortingLayer(string layer_id)
        {
            SpriteRenderer[] spriteRenderers = bslot.GetComponentsInChildren<SpriteRenderer>();

            foreach (SpriteRenderer sr in spriteRenderers)
            {
                sr.sortingLayerName = layer_id;
            }
        }

        public void ResetIndicator()
        {
            SetAnimParameter(false);
            SetSortingLayer("Default");
        }
    }
}
