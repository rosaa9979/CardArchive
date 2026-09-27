using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Client;
using TMPro;

namespace TcgEngine.FX
{
    /// <summary>
    /// The crosshair target that appears when targeting with a spell
    /// </summary>

    [DefaultExecutionOrder(100)] // Consume TargetingManager's state after its Update.
    public class AimTargetFX : MonoBehaviour
    {
        public GameObject target_fx;
        public GameObject text_fx;

        [Header("Target snap")]
        [Min(0.01f)] public float snap_duration = 0.14f;
        [Min(1f)] public float snap_start_scale = 1.16f;
        [Min(0.1f)] public float tile_frame_scale = 1.03f;
        [Tooltip("Fraction of the sprite occupied by the visible frame, excluding transparent padding.")]
        public Vector2 frame_fraction = new Vector2(0.77f, 0.86f);

        private BSlot snapped_slot;
        private SpriteRenderer target_renderer;
        private SpriteRenderer snapped_tile_renderer;
        private int default_sorting_layer;
        private int default_sorting_order;
        private Vector3 default_scale;
        private Quaternion default_rotation;
        private Vector3 settled_scale;
        private float snap_elapsed;
        private bool visuals_initialized;

        //Pushed each frame by TargetingManager: the usable require-target card being dragged, or null.
        private HandCard play_targeting_card;

        void Awake()
        {
            //Register with the manager so it drives this FX (static setter -> Awake-order independent).
            TargetingManager.SetAimFX(this);
        }

        public void SetPlayTargetingCard(HandCard card)
        {
            play_targeting_card = card;
        }

        void Start()
        {

        }

        void Update()
        {
            if (TcgEngine.Replay.ReplaySession.Active || !GameClient.Get().IsReady())
            {
                UpdateTargetVisual(null, false, Vector3.zero, 0f);
                text_fx.SetActive(false);
                return;
            }

            Game game_data = GameClient.Get().GetGameData();
            Vector3 dest = GameBoard.Get().RaycastMouseBoard();
            BSlot bslot = BSlot.GetNearest(dest);

            bool visible = false;
            bool text_visible = false;

            if (game_data.selector == SelectorType.SelectTarget && game_data.IsPlayerSelectorTurn(GameClient.Get().GetPlayer()))
            {
                AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

                if (!string.IsNullOrWhiteSpace(ability.selector_desc))
                {
                    text_visible = true;
                    TextMeshPro tmpro_text = text_fx.GetComponentInChildren<TextMeshPro>();
                    tmpro_text.text = ability.selector_desc;
                }

                if (bslot != null)
                {
                    Card caster = game_data.GetCard(game_data.selector_caster_uid);
                    Card target = game_data.GetSlotCard(bslot.GetSlot());
                    Player player = bslot.GetPlayer();

                    if (ability.criteria_target == AbilityTarget.SelectTarget && ability.CanTarget(game_data, caster, bslot.GetSlot(), context: game_data.selector_context))
                    {
                        visible = true;
                    }
                }
            }

            //Targeting state is computed by TargetingManager and pushed into play_targeting_card,
            //so the crosshair/text show/hide together with the aim line and only for a usable card.
            HandCard hcard = play_targeting_card;
            if (hcard != null)
            {
                Card caster = hcard.GetCard();
                AbilityData ability = caster.GetAbility(AbilityTarget.PlayTarget);

                if (!string.IsNullOrWhiteSpace(ability.selector_desc))
                {
                    text_visible = true;
                    TextMeshPro tmpro_text = text_fx.GetComponentInChildren<TextMeshPro>();
                    tmpro_text.text = ability.selector_desc;
                }

                if (bslot != null)
                {
                    Card target = game_data.GetSlotCard(bslot.GetSlot());
                    Player player = bslot.GetPlayer();

                    if (ability.CanTarget(game_data, caster, bslot.GetSlot()))
                    {
                        visible = true;
                    }
                    if (ability.CanTarget(game_data, caster, target))
                        visible = true;
                    if (ability.CanTarget(game_data, caster, player))
                        visible = true;
                }
            }
            

            if (text_fx.activeSelf != text_visible)
                text_fx.SetActive(text_visible);
            
            if (visible || text_visible)
                transform.position = dest;

            UpdateTargetVisual(bslot, visible, dest, Time.unscaledDeltaTime);
        }

        // Only the marker snaps; the description remains attached to the cursor.
        private void UpdateTargetVisual(BSlot slot, bool visible, Vector3 cursor, float deltaTime)
        {
            if (!visuals_initialized)
            {
                target_renderer = target_fx.GetComponent<SpriteRenderer>();
                if (target_renderer != null)
                {
                    default_sorting_layer = target_renderer.sortingLayerID;
                    default_sorting_order = target_renderer.sortingOrder;
                }
                default_scale = target_fx.transform.localScale;
                default_rotation = target_fx.transform.localRotation;
                visuals_initialized = true;
            }

            if (!visible || slot == null)
            {
                snapped_slot = null;
                snapped_tile_renderer = null;
                snap_elapsed = 0f;
                target_fx.SetActive(false);
                target_fx.transform.localScale = default_scale;
                target_fx.transform.localRotation = default_rotation;
                return;
            }

            bool acquired = snapped_slot != slot || !target_fx.activeSelf;
            if (acquired)
            {
                snapped_slot = slot;
                snap_elapsed = 0f;
                settled_scale = default_scale;
                SpriteRenderer tile = slot.GetComponent<SpriteRenderer>();
                snapped_tile_renderer = slot is BoardSlot ? tile : null;
                if (slot is BoardSlot && tile != null && tile.sprite != null && target_renderer != null && target_renderer.sprite != null)
                {
                    Vector3 tileSize = tile.sprite.bounds.size;
                    Vector3 frameSize = target_renderer.sprite.bounds.size;
                    Vector3 tileScale = tile.transform.lossyScale;
                    Vector3 parentScale = target_fx.transform.parent.lossyScale;
                    settled_scale.x = tileSize.x * Mathf.Abs(tileScale.x) * tile_frame_scale
                        / Mathf.Max(0.0001f, frameSize.x * frame_fraction.x * Mathf.Abs(parentScale.x));
                    settled_scale.y = tileSize.y * Mathf.Abs(tileScale.y) * tile_frame_scale
                        / Mathf.Max(0.0001f, frameSize.y * frame_fraction.y * Mathf.Abs(parentScale.y));
                }
            }
            else
                snap_elapsed += Mathf.Max(0f, deltaTime);

            target_fx.SetActive(true);
            // Keep the frame on the tile, beneath card art. Range highlighting can
            // move tiles and cards to another sorting layer while hovering.
            if (target_renderer != null && snapped_tile_renderer != null)
            {
                target_renderer.sortingLayerID = snapped_tile_renderer.sortingLayerID;
                target_renderer.sortingOrder = snapped_tile_renderer.sortingOrder + 1;
            }
            else if (target_renderer != null)
            {
                target_renderer.sortingLayerID = default_sorting_layer;
                target_renderer.sortingOrder = default_sorting_order;
            }
            // Player/group zones aren't hex tiles; preserve their cursor-based placement.
            target_fx.transform.position = slot is BoardSlot ? slot.transform.position : cursor;
            target_fx.transform.localRotation = default_rotation;
            if (slot is BoardSlot)
                target_fx.transform.rotation = slot.transform.rotation;

            float t = Mathf.Clamp01(snap_elapsed / Mathf.Max(0.01f, snap_duration));
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            target_fx.transform.localScale = settled_scale * Mathf.Lerp(snap_start_scale, 1f, ease);
        }

        public bool TryGetSnapPosition(out Vector3 position)
        {
            position = target_fx.transform.position;
            return isActiveAndEnabled && target_fx.activeSelf && snapped_slot is BoardSlot;
        }

        void OnDisable()
        {
            if (target_fx != null)
                UpdateTargetVisual(null, false, Vector3.zero, 0f);
            if (text_fx != null)
                text_fx.SetActive(false);
        }
    }
}
