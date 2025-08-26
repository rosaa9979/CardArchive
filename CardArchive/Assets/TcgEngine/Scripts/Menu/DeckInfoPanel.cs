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
        [Header("Mana Curve")]
        [SerializeField] ManaCurve mana_curve;

        [Header("Deck Entry")]
        [SerializeField] DeckEntry deck_entry;


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
        }

        public override void Show(bool instance = false)
        {
            base.Show(instance);
            RefreshAll();
        }

        public void RefreshAll()
        { 
            List<UserCardData> deck_info = CollectionPanel.Get().GetDeckCards();
            RefreshManaCurve(deck_info);
            RefreshDeckEntry(deck_info);
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

        public static DeckInfoPanel Get()
        {
            return instance;
        }
    }
}