using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    /// <summary>
    /// In the CardPreviewUI, a list of current status appear
    /// This is one of those
    /// </summary>

    public class StatusLine : MonoBehaviour
    {
        public Text title;
        public Text desc;
        public Image thumnail;

        private float timer = 0f;

        private static readonly List<KeywordData> keyword_buffer = new List<KeywordData>();

        private void Start()
        {
            gameObject.SetActive(false);
        }

        void Update()
        {
            timer += Time.deltaTime;
        }

        //Range thumbnails first, then the keywords the card's abilities show, then keywords of statuses the card has
        public static void SetLines(StatusLine[] lines, Card card, ConditionWideAreaRange default_wide_area_range)
        {
            int index = 0;
            List<AbilityData> abilities = card.GetAbilities();
            foreach (AbilityData ability in abilities)
            {
                if (index < lines.Length && ability.condition_wide_range != null && ability.condition_wide_range != default_wide_area_range)
                {
                    lines[index].SetLine(card.CardData, ability.condition_wide_range.thumnail);
                    index++;
                }
            }

            keyword_buffer.Clear();
            foreach (AbilityData ability in abilities)
            {
                if (ability.keywords == null)
                    continue;
                foreach (KeywordData keyword in ability.keywords)
                    AddKeyword(keyword);
            }

            foreach (CardStatus status in card.GetAllStatus())
            {
                StatusData istatus = StatusData.Get(status.type);
                if (istatus != null)
                    AddKeyword(istatus.keyword);
            }

            foreach (KeywordData keyword in keyword_buffer)
            {
                if (index < lines.Length)
                {
                    lines[index].SetLine(keyword);
                    index++;
                }
            }
        }

        private static void AddKeyword(KeywordData keyword)
        {
            if (keyword != null && !keyword_buffer.Contains(keyword))
                keyword_buffer.Add(keyword);
        }

        public void SetLine(CardData icard, Sprite new_thumnail)
        {
            if (new_thumnail != null)
            {
                title.text = "범위";
                desc.gameObject.SetActive(false);
                thumnail.gameObject.SetActive(true);
                thumnail.sprite = new_thumnail;
                gameObject.SetActive(true);
                timer = 0f;
            }
        }

        public void SetLine(KeywordData keyword)
        {
            title.text = keyword.GetTitle();
            desc.gameObject.SetActive(true);
            thumnail.gameObject.SetActive(false);
            desc.text = keyword.GetDesc();
            gameObject.SetActive(true);
            timer = 0f;
        }

        public void Hide()
        {
            if (timer > 0.05f)
                gameObject.SetActive(false);
        }
    }
}
