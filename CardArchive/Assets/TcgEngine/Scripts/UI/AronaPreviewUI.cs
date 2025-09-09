using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.UI
{
    public class AronaPreviewUI : MonoBehaviour
    {
        public UIPanel ui_panel;
        public CardUI card_ui;

        public float hover_delay = 0.7f;

        private float preview_timer = 0.0f;

        public void Update()
        {
            Debug.Log("hELLO");
            IconButton icon = IconButton.GetFocus("arona");

            CardData icon_data = icon != null ? CardData.Get(icon.value) : null;

            float delay = icon_data != null ? hover_delay : 0.0f;

            bool should_show_preview = icon_data != null;

            if (should_show_preview)
                preview_timer += Time.deltaTime;
            else
                preview_timer = 0.0f;

            bool should_show = should_show_preview && preview_timer >= delay;
            ui_panel.SetVisible(should_show);

            if (should_show)
                card_ui.SetCard(icon_data, VariantData.GetDefault());
        }
    }
}
