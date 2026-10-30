using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TcgEngine.Client;
using TcgEngine;

namespace TcgEngine.UI
{
    /// <summary>
    /// Cancel / confirm buttons shown while using the SelectMove ability target.
    /// The pair itself is dragged on the board (PlayerControls) and only sent on confirm.
    /// </summary>

    public class SelectMoveUI : SelectorPanel
    {
        public Button cancel_button;
        public Button confirm_button;

        private static SelectMoveUI _instance;

        protected override void Awake()
        {
            _instance = this;
            base.Awake();
        }

        protected override void Update()
        {
            base.Update();

            Game game = GameClient.Get().GetGameData();
            if (game != null && game.selector == SelectorType.None)
                Hide();

            //Cancel follows the same rule as a card play target (AbilityData.can_cancel)
            if (cancel_button != null)
                cancel_button.gameObject.SetActive(game != null && game.CanCancelSelector());
            if (confirm_button != null)
                confirm_button.interactable = PlayerControls.Get() != null && PlayerControls.Get().HasMoveSelection();
        }

        public override void Show(AbilityData ability, Card caster)
        {
            Show();
        }

        public void OnClickCancel()
        {
            if (!GameClient.Get().GetLatestGameData().CanCancelSelector())
                return;

            PlayerControls.Get()?.ClearMoveSelection();
            GameClient.Get().CancelSelection();
        }

        public void OnClickConfirm()
        {
            PlayerControls.Get()?.ConfirmMoveSelection();
        }

        public override bool ShouldShow()
        {
            Game data = GameClient.Get().GetGameData();
            int player_id = GameClient.Get().GetPlayerID();
            return data.selector == SelectorType.SelectMove && data.selector_player_id == player_id;
        }

        //Board drags must not start under the panel buttons
        public static bool IsPointerOver()
        {
            if (_instance == null || !_instance.IsVisible() || EventSystem.current == null)
                return false;

            PointerEventData pointer = new PointerEventData(EventSystem.current);
            pointer.position = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, results);
            foreach (RaycastResult result in results)
            {
                if (result.gameObject != null && result.gameObject.transform.IsChildOf(_instance.transform))
                    return true;
            }
            return false;
        }

        public static SelectMoveUI Get()
        {
            return _instance;
        }
    }
}
