using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TcgEngine.UI
{
    /// <summary>
    /// 현재 제작하고 있는 덱의 마나 커브 / 타입별 매수 / 영웅 능력 설정할 수 있는 Panel
    /// </summary>

    public class DeckInfoPanel : UIPanel
    {
        [SerializeField] private GameObject[] mana_bars;
        private Dictionary<int, int> mana_distribution;
        private List<UserCardData> deck_info;
        private static DeckInfoPanel instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void Start()
        {
            base.Start();
            mana_distribution = new Dictionary<int, int>();
            deck_info = CollectionPanel.Get().GetDeckCards();
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
            RefreshManaCurve();
        }

        public void RefreshManaCurve()
        {
            mana_distribution.Clear();

            deck_info.Clear();
            deck_info = CollectionPanel.Get().GetDeckCards();

            foreach (UserCardData utid in deck_info)
            {
                CardData ucard = CardData.Get(utid.tid);
                if (mana_distribution.ContainsKey(ucard.mana))
                    mana_distribution[ucard.mana] = utid.quantity;
                else
                    mana_distribution[ucard.mana] += utid.quantity;
            }

            DrawManaCurve();
        }

        public void DrawManaCurve()
        {

        }

        public static DeckInfoPanel Get()
        {
            return instance;
        }
    }
}