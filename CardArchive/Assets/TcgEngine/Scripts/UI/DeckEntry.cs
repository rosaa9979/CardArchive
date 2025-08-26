using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;


namespace TcgEngine.UI
{
    /// <summary>
    /// 덱의 카드들을 종류별로 분류하여 통계를 볼 수 있도록 하는 클래스
    /// </summary>
    public class DeckEntry : MonoBehaviour
    {
        public Text student_text;
        public Text non_student_text;
        public Text place_text;
        public Text event_text;

        public void Refresh(List<UserCardData> deck_info)
        {
            int student_count = 0;
            int non_student_count = 0;
            int place_count = 0;
            int event_count = 0;

            foreach (UserCardData udata in deck_info)
            {
                CardData ucard = CardData.Get(udata.tid);

                if (ucard != null)
                {
                    switch (ucard.type)
                    {
                        case CardType.Student:
                            student_count += udata.quantity;
                            break;

                        case CardType.NonStudent:
                            non_student_count += udata.quantity;
                            break;
                        case CardType.Place:
                            place_count += udata.quantity;
                            break;
                        case CardType.Spell:
                            event_count += udata.quantity;
                            break;
                        default:
                            break;
                    }
                }
            }

            if (student_text != null)
                student_text.text = student_count.ToString();
            if (non_student_text != null)    
                non_student_text.text = non_student_count.ToString();
            if (place_text != null)
                place_text.text = place_count.ToString();
            if (event_text != null)
                event_text.text = event_count.ToString();
        }
    }
}