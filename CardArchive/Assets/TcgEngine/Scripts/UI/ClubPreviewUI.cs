using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine;
using DG.Tweening;


namespace TcgEngine.UI
{
    /// <summary>
    /// In the game scene, the ClubPreviewUI is what shows the Club in big with extra info when hovering a club icon
    /// </summary>

    public class ClubPreviewUI : MonoBehaviour
    {
        [Header("Display Setting")]
        public CanvasGroup canvas_group;
        public CardUI card_ui;
        public float display_duration;
        public float screen_width_ratio = 0.2f;

        public float side_offset;

        private static ClubPreviewUI _instance;


        private void Awake()
        {
            _instance = this;
        }

        private void Start()
        {
            canvas_group.alpha = 0.0f;
        }

        public void Show(Card club)
        {
            if (club == null)
                return;
                
            Hide();

            side_offset = Screen.width * screen_width_ratio;
            canvas_group.alpha = 1.0f;

            bool is_opponent = club.player_id != GameClient.Get().GetPlayerID();

            ClubPreviewItem item = ClubPreviewItem.Get(is_opponent);

            if (item != null)
                item.Show(club);
        }

        public void Hide()
        {
            foreach (ClubPreviewItem item in ClubPreviewItem.Get())
            {
                item.Hide();
            }

            canvas_group.alpha = 0.0f;
        }

        

        public static ClubPreviewUI Get()
        {
            return _instance;
        }
    }
}
