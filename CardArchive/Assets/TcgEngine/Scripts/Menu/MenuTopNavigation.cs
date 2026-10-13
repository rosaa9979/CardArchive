using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    // The top-right action reflects the current menu layer, not the scene itself.
    public class MenuTopNavigation : MonoBehaviour
    {
        public Button power_button;
        public Button home_button;
        public AdventurePanel adventure_panel;
        public DeckSelectorPanel solo_panel;
        public TabButton home_tab;

        private void OnEnable()
        {
            if (adventure_panel != null)
            {
                adventure_panel.onShow += Refresh;
                adventure_panel.onHide += Refresh;
            }
            if (solo_panel != null)
            {
                solo_panel.onShow += Refresh;
                solo_panel.onHide += Refresh;
            }
            Refresh();
        }

        private void Start() => Refresh();

        private void OnDisable()
        {
            if (adventure_panel != null)
            {
                adventure_panel.onShow -= Refresh;
                adventure_panel.onHide -= Refresh;
            }
            if (solo_panel != null)
            {
                solo_panel.onShow -= Refresh;
                solo_panel.onHide -= Refresh;
            }
        }

        private bool IsSoloSelectionOpen()
        {
            return solo_panel != null && solo_panel.IsVisible()
                && TcgEngine.Client.GameClient.game_settings.game_type == GameType.Solo;
        }

        public void Refresh()
        {
            bool showHome = (adventure_panel != null && adventure_panel.IsVisible())
                || IsSoloSelectionOpen();
            if (power_button != null) power_button.gameObject.SetActive(!showHome);
            if (home_button != null) home_button.gameObject.SetActive(showHome);
        }

        public void OnClickHome()
        {
            if (adventure_panel != null && adventure_panel.IsVisible()) adventure_panel.Hide();
            if (IsSoloSelectionOpen()) solo_panel.Hide();
            if (home_tab != null) home_tab.Activate();
            Refresh();
        }
    }
}