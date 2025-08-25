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
        private DeckInfoPanel instance;

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
    }
}