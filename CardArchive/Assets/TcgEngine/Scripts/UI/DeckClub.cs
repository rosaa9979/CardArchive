using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    public class DeckClub : MonoBehaviour
    {
        public Sprite default_icon;

        [SerializeField]
        private Image club_icon;

        private CardData cached_club;

        public void Start()
        {
            Clear();
        }

        public void SetDeckClub(CardData club)
        {
            if (club != null)
            {
                SetCachedClub(club);
                SetCachedClubIcon(club.GetBoardArt(VariantData.GetDefault()));
            }

            else
            {
                SetCachedClub(null);
                SetCachedClubIcon(default_icon);
            }

        }

        public void Clear()
        {
            SetCachedClub(null);
            SetCachedClubIcon(default_icon);
        }

        public void OnClick()
        {
            if (cached_club == null)
                return;
            
            CardZoomPanel.Get().ShowCard(cached_club, VariantData.GetDefault());
        }

        public CardData GetCachedClub()
        {
            return cached_club;
        }

        public void SetCachedClub(CardData club)
        {
            cached_club = club;
        }

        public Image GetClubIcon()
        {
            return club_icon;
        }

        public void SetCachedClubIcon(Sprite icon)
        {
            club_icon.sprite = icon;
        }
    }

}
