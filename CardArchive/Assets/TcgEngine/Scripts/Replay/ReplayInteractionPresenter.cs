using System.Collections;
using System.Linq;
using TcgEngine.Client;
using TcgEngine.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.Replay
{
    public class ReplayInteractionPresenter : MonoBehaviour
    {
        // Borrow the real scene visual; never clone card renderers into a second canvas.
        Transform held;
        Behaviour heldMotion;
        bool motionEnabled;
        HandCard heldHand;
        string heldUid;
        int heldPlayer, handCount;
        Slot heldSlot;
        Vector3 originalPosition, originalScale;
        Quaternion originalRotation;
        int originalSibling;
        bool heldMove;
        public bool HasHeldVisual => held != null;
        public Transform HeldVisual => held;

        public void SyncState()
        {
            if (held == null) return;
            var game = GameClient.Get().GetGameData();
            var card = game.GetCard(heldUid);
            bool consumed = heldMove ? card == null || card.slot == heldSlot
                : heldHand != null ? game.GetPlayer(heldPlayer).GetHandCard(heldUid) == null
                : game.GetPlayer(heldPlayer).cards_hand.Count < handCount;
            if (consumed) Release(false);
        }
        void Release(bool restore)
        {
            if (held != null)
            {
                if (heldHand != null) heldHand.SetDrag(false);
                if (restore)
                {
                    held.localPosition = originalPosition;
                    held.localScale = originalScale;
                    held.localRotation = originalRotation;
                    held.SetSiblingIndex(originalSibling);
                }
                else if (!heldMove) held.gameObject.SetActive(false);
                if (heldMotion != null) heldMotion.enabled = motionEnabled;
            }
            held = null;
            heldHand = null;
            heldMotion = null;
        }
        void OnDestroy() { Release(true); }

        void Borrow(Transform source, Behaviour motion, HandCard hand, ReplayInteraction action)
        {
            Release(true);
            held = source;
            heldMotion = motion;
            motionEnabled = motion != null && motion.enabled;
            if (motion != null) motion.enabled = false;
            heldHand = hand;
            heldUid = action.card;
            heldPlayer = action.player;
            heldSlot = action.slot;
            heldMove = action.kind == "move";
            handCount = GameClient.Get().GetGameData().GetPlayer(action.player).cards_hand.Count;
            originalPosition = source.localPosition;
            originalRotation = source.localRotation;
            originalScale = source.localScale;
            originalSibling = source.GetSiblingIndex();
            if (hand != null)
            {
                hand.SetFocus(false);
                hand.SetDrag(true);
                hand.GetComponent<CanvasGroup>().alpha = 1;
                hand.hand_canvas_group.alpha = 1;
                hand.board_canvas_group.alpha = 0;
                source.SetAsLastSibling();
            }
        }
        static void MoveToScreen(Transform visual, Vector2 point)
        {
            var parent = visual.parent as RectTransform;
            var canvas = visual.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null
                : canvas != null && canvas.worldCamera != null ? canvas.worldCamera : GameCamera.GetCamera();
            if (parent != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, point, camera, out var world))
                visual.position = world;
        }
        public IEnumerator Present(ReplayInteraction action)
        {
            if (action.kind == "cancel") { Release(true); yield break; }
            var client = GameClient.Get();
            bool own = action.player == client.GetPlayerID();
            // Opponent private selections/mulligan candidates are never presented.
            if (!own && action.kind != "play" && action.kind != "move") yield break;
            if (action.kind == "mulligan")
            {
                foreach (var card in FindObjectsByType<CardMulligan>(FindObjectsSortMode.InstanceID))
                {
                    bool selected = action.cards != null && action.cards.Contains(card.GetCard().uid);
                    card.SetSelected(selected);
                    if (selected) yield return new WaitForSeconds(0.15f);
                }
                yield return new WaitForSeconds(0.25f);
                yield break;
            }
            Transform source = null, target = null;
            Behaviour motion = null;
            HandCard hand = null;
            if (own && !string.IsNullOrEmpty(action.card))
            {
                hand = HandCard.Get(action.card);
                var board = BoardCard.Get(action.card);
                source = hand != null ? hand.transform : board != null ? board.transform : null;
                motion = hand != null ? (Behaviour)hand : board;
            }
            else if (!own && action.kind == "move")
            {
                var board = BoardCard.Get(action.card);
                if (board != null) { source = board.transform; motion = board; }
            }
            else if (!own && action.kind == "play")
            {
                // Opponent hands expose count, not identity. Use the same last card that
                // OpponentHand removes when the recorded hand count decreases.
                var back = OpponentHand.Get() != null ? OpponentHand.Get().GetLastVisualCard() : null;
                if (back != null) { source = back.transform; motion = back; }
            }
            if (action.slot.IsValid())
            {
                var slot = BSlot.Get(action.slot);
                if (slot != null) target = slot.transform;
            }
            if (action.kind == "card")
            {
                var card = FindObjectsByType<CardSelectorCard>(FindObjectsSortMode.InstanceID).FirstOrDefault(c => c.GetCard().uid == action.target);
                var board = BoardCard.Get(action.target);
                var targetHand = HandCard.Get(action.target);
                target = card != null ? card.transform : board != null ? board.transform : targetHand != null ? targetHand.transform : null;
            }
            if (action.kind == "choice")
            {
                var panel = ChoiceSelector.Get();
                if (panel != null && action.choice >= 0 && action.choice < panel.choices.Length)
                    target = panel.choices[action.choice].transform;
            }
            if (action.kind == "player")
            {
                var slot = BoardSlotPlayer.Get(action.choice != client.GetPlayerID());
                if (slot != null) target = slot.transform;
            }
            if (action.kind == "endTurn" && GameUI.Get() != null)
                target = GameUI.Get().end_turn_button.transform;
            Vector2 from = source != null ? ScreenPosition(source) : new Vector2(Screen.width * 0.5f, Screen.height * (own ? 0.15f : 0.85f));
            Vector2 to = target != null ? ScreenPosition(target) : from;
            if (action.slot.IsValid() && BSlot.Get(action.slot) != null)
                to = GameCamera.GetCamera().WorldToScreenPoint(BSlot.Get(action.slot).GetPosition(action.slot));

            bool drag = action.kind == "play" || action.kind == "move";
            if (drag && source != null)
            {
                Borrow(source, motion, hand, action);
                Vector3 worldFrom = source.position;
                Vector3 worldTo = action.slot.IsValid() && BSlot.Get(action.slot) != null
                    ? BSlot.Get(action.slot).GetPosition(action.slot) : worldFrom;
                float duration = Mathf.Clamp(Vector2.Distance(from, to) / 1000f, 0.25f, 0.7f);
                var card = client.GetGameData().GetCard(action.card);
                for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
                {
                    if (held == null) break;
                    float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                    if (action.kind == "move") held.position = Vector3.Lerp(worldFrom, worldTo, t);
                    else MoveToScreen(held, Vector2.Lerp(from, to, t));
                    held.localRotation = Quaternion.Slerp(originalRotation, Quaternion.identity, t);
                    if (hand != null)
                    {
                        held.localScale = originalScale * Mathf.Lerp(1, 0.75f, t);
                        // Reuse the HandCard prefab's existing hand/board faces and canvas.
                        bool boardFace = t > 0.5f && action.slot.IsValid() && card != null && card.CardData.IsBoardCard();
                        hand.hand_canvas_group.alpha = boardFace ? 0 : 1;
                        hand.board_canvas_group.alpha = boardFace ? 1 : 0;
                    }
                    yield return null;
                }
                if (held != null)
                {
                    if (action.kind == "move") held.position = worldTo;
                    else MoveToScreen(held, to);
                    held.localRotation = Quaternion.identity;
                }
                // Keep the real visual here until the next authoritative state consumes it.
                yield return new WaitForSeconds(0.12f);
            }
            if (action.kind == "card" && CardSelector.Get() != null && CardSelector.Get().IsVisible())
            {
                var animation = CardSelector.Get().PresentReplayChoice(action.target);
                while (!animation.IsCompleted) yield return null;
                if (animation.IsFaulted) Debug.LogException(animation.Exception);
            }
            else if (!drag && target != null)
            {
                Vector3 original = target.localScale;
                for (float elapsed = 0; elapsed < 0.2f; elapsed += Time.deltaTime)
                {
                    if (target == null) break;
                    target.localScale = original * (1 - 0.08f * Mathf.Sin(elapsed / 0.2f * Mathf.PI));
                    yield return null;
                }
                if (target != null) target.localScale = original;
            }
        }
        static Vector2 ScreenPosition(Transform transform)
        {
            var canvas = transform.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null
                : canvas != null && canvas.worldCamera != null ? canvas.worldCamera : GameCamera.GetCamera();
            return RectTransformUtility.WorldToScreenPoint(camera, transform.position);
        }
    }
}
