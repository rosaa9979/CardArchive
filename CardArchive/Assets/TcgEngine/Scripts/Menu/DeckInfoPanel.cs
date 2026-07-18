using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Collections.LowLevel.Unsafe;

namespace TcgEngine.UI
{
    /// <summary>
    /// 현재 제작하고 있는 덱의 마나 커브 / 타입별 매수 / 영웅 능력 설정할 수 있는 Panel
    /// </summary>

    public class DeckInfoPanel : UIPanel
    {
        [Header("Arona Ability")]
        [SerializeField] AronaSelection arona_selection;
        [SerializeField] UIPanel ui_panel;
        [SerializeField] CardUI card_ui;
        private float preview_timer = 0.0f;

        [Header("Mana Curve")]
        [SerializeField] ManaCurve mana_curve;

        [Header("Deck Entry")]
        [SerializeField] DeckEntry deck_entry;

        private bool is_show = false;


        private static DeckInfoPanel instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void Start()
        {
            base.Start();
        }

        protected override void Update()
        {
            base.Update();

            IconButton icon = IconButton.GetFocus("arona");
            CardData icon_data = icon != null ? CardData.Get(icon.value) : null;

            float delay = GameConfig.Gesture.preview_delay;

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

        public override void Show(bool instance = false)
        {
            base.Show(instance);
            is_show = true;
            RefreshAll();
        }

        public override void Hide(bool instant = false)
        {
            base.Hide(instant);
            is_show = false;
        }

        public bool IsShow()
        {
            return is_show;
        }

        public void RefreshAll()
        {
            List<UserCardData> deck_info = CollectionPanel.Get().GetDeckCards();
            UserCardData hero_info = CollectionPanel.Get().GetDeckHero();
            RefreshManaCurve(deck_info);
            RefreshDeckEntry(deck_info);
            RefreshAronaList(hero_info);
        }

        public void RefreshManaCurve(List<UserCardData> deck_info)
        {
            if (mana_curve != null)
            {
                mana_curve.Refresh(deck_info);
            }
        }

        public void RefreshDeckEntry(List<UserCardData> deck_info)
        {
            if (deck_entry != null)
                deck_entry.Refresh(deck_info);
        }

        public void RefreshAronaList(UserCardData hero_info)
        {
            if (arona_selection != null)
                arona_selection.Refresh(hero_info);
        }

        public static DeckInfoPanel Get()
        {
            return instance;
        }
    }
}