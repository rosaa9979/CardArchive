using TcgEngine.Client;
using TcgEngine.UI;
using UnityEngine;

namespace TcgEngine.FX
{
    /// <summary>A continuous sliced sprite shaft with a fixed-size arrowhead.</summary>
    [DefaultExecutionOrder(200)]
    public class MouseLineFX : MonoBehaviour
    {
        public SpriteRenderer shaft_outline;
        public SpriteRenderer shaft_fill;
        public SpriteRenderer arrow_outline;
        public SpriteRenderer arrow_fill;
        [Min(0.01f)] public float line_width = 0.14f;
        [Min(0f)] public float outline_width = 0.025f;
        [Min(0.01f)] public float arrow_length = 0.38f;
        private HandCard play_targeting_card;

        void Awake() { TargetingManager.SetLineFX(this); Hide(); }
        public void SetPlayTargetingCard(HandCard card) { play_targeting_card = card; }

        void LateUpdate()
        {
            GameClient client = GameClient.Get();
            if (TcgEngine.Replay.ReplaySession.Active || client == null || !client.IsReady() || client.IsObserveMode())
            { Hide(); return; }
            Game data = client.GetGameData();
            bool visible = false;
            Vector3 source = Vector3.zero;
            if (play_targeting_card != null)
            { source = play_targeting_card.transform.position; visible = true; }
            if (data.selector == SelectorType.SelectTarget)
            {
                if (!data.IsPlayerSelectorTurn(client.GetPlayer())) { Hide(); return; }
                BoardCard caster = BoardCard.Get(data.selector_caster_uid);
                HeroUI hero = HeroUI.Get(false);
                if (caster != null) { source = caster.transform.position; visible = true; }
                else if (hero != null && hero.GetCard() != null && hero.GetCard().uid == data.selector_caster_uid)
                { source = hero.transform.position; visible = true; }
            }
            if (!visible) { Hide(); return; }
            // The tile marker still snaps, but the arrow always follows the cursor.
            SetEndpoints(source, GameBoard.Get().RaycastMouseBoard());
        }

        // XY is the board plane; sorting keeps the line above board art.
        public void SetEndpoints(Vector3 source, Vector3 destination)
        {
            Vector2 delta = destination - source;
            float distance = delta.magnitude;
            if (distance < 0.04f || shaft_outline == null || shaft_fill == null || arrow_outline == null || arrow_fill == null)
            { Hide(); return; }
            Vector3 direction = new Vector3(delta.x / distance, delta.y / distance, 0f);
            source.z = destination.z;
            Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            // Collapse only for endpoints closer than the head itself.
            float head = Mathf.Min(arrow_length, distance);
            float length = Mathf.Max(0f, distance - head * 0.4f);
            SetShaft(shaft_outline, source, direction, rotation, length, line_width + outline_width * 2f);
            SetShaft(shaft_fill, source + direction * outline_width, direction, rotation, Mathf.Max(0f, length - outline_width), line_width);
            SetHead(arrow_outline, destination, rotation, head);
            SetHead(arrow_fill, destination - direction * outline_width, rotation, Mathf.Max(0.001f, head - outline_width * 2f));
            TargetingManager.PositionLineLabel(source, destination);
        }
        private static void SetShaft(SpriteRenderer renderer, Vector3 start, Vector3 direction, Quaternion rotation, float length, float width)
        {
            renderer.enabled = length > 0.01f;
            renderer.transform.SetPositionAndRotation(start + direction * length * 0.5f, rotation);
            renderer.size = new Vector2(length, width);
        }
        private static void SetHead(SpriteRenderer renderer, Vector3 tip, Quaternion rotation, float length)
        {
            renderer.enabled = true;
            renderer.transform.SetPositionAndRotation(tip, rotation * Quaternion.Euler(0f, 0f, -90f));
            renderer.transform.localScale = Vector3.one * (length / renderer.sprite.bounds.size.y);
        }
        private void Hide()
        {
            TargetingManager.HideLineLabel();
            if (shaft_outline != null) shaft_outline.enabled = false;
            if (shaft_fill != null) shaft_fill.enabled = false;
            if (arrow_outline != null) arrow_outline.enabled = false;
            if (arrow_fill != null) arrow_fill.enabled = false;
        }
        void OnDisable() { Hide(); }
    }
}
