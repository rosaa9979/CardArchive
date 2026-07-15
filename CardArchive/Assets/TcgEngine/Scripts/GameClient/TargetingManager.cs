using UnityEngine;
using TcgEngine.FX;

namespace TcgEngine.Client
{
    /// <summary>
    /// Central authority for play-card "require target" targeting UI.
    /// Each frame it computes a single targeting state — a require-target card is being dragged AND
    /// it is currently usable (playable), independent of cursor Y — and pushes it to the targeting FX
    /// so the crosshair + selector text (AimTargetFX) and the aim line (MouseLineFX) stay in sync.
    ///
    /// The FX register themselves in their Awake via the static setters below, so registration is
    /// independent of Awake order (the manager instance reads them when its own Update runs).
    /// Place this component on the same prefab/scene object as the targeting FX.
    /// </summary>
    public class TargetingManager : MonoBehaviour
    {
        private static TargetingManager instance;

        //FX self-register here in their Awake. Static so it works no matter which Awake runs first.
        private static AimTargetFX aim_fx;
        private static MouseLineFX line_fx;

        void Awake()
        {
            instance = this;
        }

        public static void SetAimFX(AimTargetFX fx) { aim_fx = fx; }
        public static void SetLineFX(MouseLineFX fx) { line_fx = fx; }

        void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            HandCard card = ComputeTargetingCard();
            if (aim_fx != null)
                aim_fx.SetPlayTargetingCard(card);
            if (line_fx != null)
                line_fx.SetPlayTargetingCard(card);
        }

        //The dragged hand card currently driving play-card targeting UI, or null.
        private HandCard ComputeTargetingCard()
        {
            HandCard drag = HandCard.GetDrag();
            if (drag == null)
                return null;

            Card card = drag.GetCard();
            if (card == null || !card.CardData.IsRequireTarget())
                return null;

            //"Usable" = same rule HandCard uses for its can-play outline (mana, in-hand, a valid target
            //exists, ...), independent of cursor Y. An unusable card shows no targeting UI.
            Game gdata = GameClient.Get().GetGameData();
            bool usable = GameClient.Get().IsYourTurn()
                && gdata.phase == GamePhase.Main
                && gdata.CanPlay(card);

            return usable ? drag : null;
        }

        public static TargetingManager Get()
        {
            return instance;
        }
    }
}
