using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    public class Mana : MonoBehaviour
    {
        Image mana_image;
        private Button button;
        private bool isOn;

        [SerializeField] private ManaFilter manaFilter;
        [SerializeField] private int value;
        [SerializeField] private Sprite unselectedSprite;    // 선택 안 된 상태        
        [SerializeField] private Sprite selectedSprite;  // 선택된 상태


        void Awake()
        {
            mana_image = GetComponentInChildren<Image>();
            button = GetComponentInChildren<Button>();
        }

        void Start()
        {
            isOn = true;
        }

        public void OnClickedMana()
        {
            if (manaFilter != null)
                manaFilter.OnClickedMana(value);
        }

        public void UpdateManaVisual()
        {
            if (mana_image != null)
                mana_image.sprite = isOn ? selectedSprite : unselectedSprite;
        }

        public void SetActive(bool isActive)
        {
            isOn = isActive;
            UpdateManaVisual();
        }

        public int GetValue()
        {
            return value;
        }
    }
}
