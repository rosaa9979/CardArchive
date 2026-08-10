using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine;
using System.Diagnostics.SymbolStore;

namespace TcgEngine.UI
{
    /// <summary>
    /// In the game scene, the CardPreviewUI is what shows the card in big with extra info when hovering a card
    /// </summary>

    public class CardPreviewUI : MonoBehaviour
    {
        public UIPanel ui_panel;
        public CardUI card_ui;

        public RectTransform[] side_rows;
        public StatusLine[] status_lines;

        private float preview_timer = 0f;
        private float offset;
        private Vector2[] start_pos;

        [Header("Default Setting")]
        public ConditionWideAreaRange default_wide_area_range;

        private void Start()
        {
            start_pos = new Vector2[side_rows.Length];
            for (int i = 0; i < side_rows.Length; i++)
            {
                start_pos[i] = side_rows[i].anchoredPosition;
            }

            offset = start_pos[1].x - start_pos[0].x;
        }

        void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            foreach (StatusLine line in status_lines)
                line.Hide();

            PlayerControls controls = PlayerControls.Get();
            Game game_data = GameClient.Get().GetGameData();
            BoardCard bcard = BoardCard.GetFocus();
            HeroUI hero_ui = HeroUI.GetFocus();
            Card histcard = TurnHistoryLine.GetHoverCard();

            float delay = GameConfig.Gesture.preview_delay;

            Card pcard = bcard?.GetFocusCard();
            if (pcard == null)
                pcard = histcard;
            if (pcard == null)
                pcard = hero_ui?.GetCard();

            //A held press must be able to show the preview (hold-to-preview);
            //only an actual hand card drag blocks it
            bool should_show_preview = !HandCardArea.Get().IsDragging() && !GameUI.IsUIOpened() && pcard != null;

            //While a board press is held (scrubbing), keep the timer so moving over
            //empty space and back onto a card re-shows the preview instantly
            if (should_show_preview)
                preview_timer += Time.deltaTime;
            else if (!controls.IsPressActive())
                preview_timer = 0f;

            bool show_preview = should_show_preview && preview_timer >= delay;
            ui_panel.SetVisible(show_preview);

            if (show_preview)
            {
                Vector2[] final_pos = new Vector2[side_rows.Length];

                final_pos = start_pos;

                for (int i = 0; i < side_rows.Length; i++)
                {
                    side_rows[i].anchoredPosition = final_pos[i];
                }

                card_ui.SetCard(pcard);

                //Abilities
                int index = 0;
                foreach (AbilityData ability in pcard.GetAbilities())
                {
                    if (index < status_lines.Length)
                    {
                        if (ability.condition_wide_range != default_wide_area_range)
                        //Dont display default ability (GetAbilitiesDesc does that already)
                        //if (!pcard.CardData.HasAbility(ability) && !string.IsNullOrWhiteSpace(ability.desc))
                        {
                            status_lines[index].SetLine(pcard.CardData, ability.condition_wide_range.thumnail);
                            index++;
                        }
                    }
                }

                foreach (AbilityData ability in pcard.GetAbilities())
                {
                    if (index < status_lines.Length)
                    {
                        if (!string.IsNullOrWhiteSpace(ability.desc))
                        //Dont display default ability (GetAbilitiesDesc does that already)
                        //if (!pcard.CardData.HasAbility(ability) && !string.IsNullOrWhiteSpace(ability.desc))
                        {
                            status_lines[index].SetLine(pcard.CardData, ability);
                            index++;
                        }
                    }
                }

                //Status
                foreach (CardStatus status in pcard.GetAllStatus())
                {
                    if (index < status_lines.Length)
                    {
                        StatusData istatus = StatusData.Get(status.type);
                        if (istatus != null && !string.IsNullOrWhiteSpace(istatus.desc))
                        {
                            int ival = Mathf.Max(status.value, Mathf.CeilToInt(status.duration / 2f));
                            status_lines[index].SetLine(istatus, ival);
                            index++;
                        }
                    }
                }
            }

        }
    }

    public enum CardPrevirwType
    {
        None,
        BoardCard,
        HandCard
    }
}
