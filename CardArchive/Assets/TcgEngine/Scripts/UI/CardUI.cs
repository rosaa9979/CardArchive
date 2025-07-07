using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TcgEngine.Client;

namespace TcgEngine.UI
{
    /// <summary>
    /// Scripts to display all stats of a card, 
    /// is used by other script that display cards like BoardCard, and HandCard, CollectionCard..
    /// </summary>

    public class CardUI : MonoBehaviour, IPointerClickHandler
    {
        public Image title_background;
        public Image card_image;
        public Image frame_image;
        public Image type_background;
        //public Image team_icon;
        public Image attack_background;
        public Image attack_icon;
        public Image hp_background;
        public Image hp_icon;
        public Image cost_icon;
        public Image range_background;
        public Image range_icon;
        public Image trait_background;
        public Image club_background;
        public Image academy_logo;
        public Text type;
        public Text attack;
        public Text hp;
        public Text cost;
        public Text range;
        public Text clubs;
        public Text trait;
        public RectTransform club_rect;
        public RectTransform trait_rect;
        private float padding = 20f;

        public Text card_title;
        public Text card_text;

        public TraitUI[] stats;

        public UnityAction<CardUI> onClick;
        public UnityAction<CardUI> onClickRight;

        private CardData card;
        private VariantData variant;

        public Text card_name;
        public Color32 ally_name =  new Color32(14, 165, 233, 255);
        public Color32 enemy_name = new Color32(220, 38, 38, 255);


        void Awake()
        {

        }

        public void SetCard(Card card)
        {
            if (card == null)
                return;

            SetCard(card.CardData, card.VariantData);

            if (card.CardData.IsClub())
            {
                //Debug.Log("Hello");
            }

            else
            {
                if (cost != null)
                    cost.text = card.GetMana().ToString();
            }

            if (attack != null)
                attack.text = card.GetAttack().ToString();
            if (hp != null)
                hp.text = card.GetHP().ToString();
            if (range_background != null)
                range_background.color = card.weapon.GetWeaponColor();
            //if (weapon_type != null)
            //    weapon_type.text = card.weapon.GetWeaponID().ToString();
            if (range != null)
                range.text = card.GetRange().ToString();
            if (trait_background != null)
                trait_background.enabled = card.GetAllTraits().Count > 0;

            if (trait != null)
                trait.enabled = card.GetAllTraits().Count > 0;
            if (trait != null)
                trait.text = string.Join(" / ", card.GetAllTraits().Select(tra => tra.TraitData.GetTitle()));

            if (clubs != null)
                clubs.enabled = card.GetAllClubs().Count > 0;
            if (clubs != null)
                clubs.text = string.Join(" / ", card.GetAllClubs().Select(club => club.ClubData.GetTitle()));

            if (club_background != null)
                club_background.enabled = card.GetAllClubs().Count > 0;

            if (clubs != null && club_background != null)
            {
                RectTransform imageRectTransform = club_background.GetComponent<RectTransform>();
                float textWidth = clubs.preferredWidth;

                // 2. 최종 Image의 너비 계산 (Text 너비 + 좌우 패딩)
                float totalWidth = textWidth + (2 * padding);

                // 3. Image의 RectTransform 너비 조정
                imageRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalWidth);
            }

            if (card.CardData.IsClub())
            {
                int count = 0;
                Player player = GameClient.Get().GetGameData().GetPlayer(card.player_id);

                foreach (Card c in player.cards_board)
                {
                    if (c.HasClub(card.clubs[0].ClubData))
                        count += 1;
                }

                cost.text = count.ToString();
            }

            foreach (TraitUI stat in stats)
                stat.SetCard(card);
                
            if (card_name != null)
                card_name.text = card.CardData.GetTitle();
        }

        public void SetCard(CardData card, VariantData variant)
        {
            if (card == null)
                return;

            this.card = card;
            this.variant = variant;

            if (type != null)
                type.text = card.GetTypeId().ToString();
            if (card_image != null)
                card_image.sprite = card.GetFullArt(variant);
            //if (frame_image != null)
            //    frame_image.sprite = variant.frame;
            if (card_title != null)
                card_title.text = card.GetTitle().ToUpper();
            if (card_text != null)
                card_text.text = card.GetText();

            if (attack_background != null)
                attack_background.enabled = card.IsCitizen();
            if (attack_icon != null)
                attack_icon.enabled = card.IsCitizen();
            if (attack != null)
                attack.enabled = card.IsCitizen();
            if (hp_background != null)
                hp_background.enabled = card.IsBoardCard() || card.IsEquipment();
            if (hp_icon != null)
                hp_icon.enabled = card.IsBoardCard() || card.IsEquipment();
            if (hp != null)
                hp.enabled = card.IsBoardCard() || card.IsEquipment();
            if (cost_icon != null)
                cost_icon.enabled = card.type != CardType.Hero;
            if (cost != null)
                cost.enabled = card.type != CardType.Hero;
            //if (weapon_icon != null)
            //    weapon_icon.enabled = card.IsCitizen();
            //if (weapon_type != null)
            //    weapon_type.enabled = card.IsCitizen();
            if (range_background != null)
                range_background.enabled = card.IsCitizen();
            if (range_background != null)
                range_background.color = card.weapon.GetWeaponColor();
            if (range_icon != null)
                range_icon.enabled = card.IsCitizen();
            if (range != null)
                range.enabled = card.IsCitizen();
            if (club_background != null)
                club_background.enabled = card.IsStudent();
            if (clubs != null)
                clubs.enabled = card.IsStudent();
            if (trait_background != null)
                trait_background.enabled = card.traits.Length > 0;
            if (trait != null)
                trait.enabled = card.traits.Length > 0;

            if (cost != null)
                cost.text = card.mana.ToString();
            if (attack != null)
                attack.text = card.attack.ToString();
            if (hp != null)
                hp.text = card.hp.ToString();
            //if (weapon_type != null)
            //    weapon_type.text = card.weapon.GetWeaponID().ToString();
            if (range != null)
                range.text = card.GetRange().ToString();
            if (clubs != null)
                clubs.text = string.Join(" / ", card.clubs.Select(club => club.title));
            if (trait != null)
                trait.text = string.Join(" / ", card.traits.Select(tra => tra.title));
            //if (team_icon != null)
            //{
            //    team_icon.sprite = card.team.icon;
            //    team_icon.enabled = team_icon.sprite != null;
            //}

            if (clubs != null && club_background != null)
            {
                RectTransform imageRectTransform = club_background.GetComponent<RectTransform>();
                float textWidth = clubs.preferredWidth;

                // 2. 최종 Image의 너비 계산 (Text 너비 + 좌우 패딩)
                float totalWidth = textWidth + (2 * padding);

                // 3. Image의 RectTransform 너비 조정
                imageRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalWidth);
            }
            
            if (academy_logo != null)
            {
                if (card.clubs.Length > 0)
                {
                    academy_logo.sprite = card.clubs[0].academy.acadmey_icon;
                    academy_logo.color = new Color(academy_logo.color.r, academy_logo.color.g, academy_logo.color.b, 0.2f);

                    // 컴포넌트 가져오기
                    AspectRatioFitter aspectFitter = academy_logo.GetComponent<AspectRatioFitter>();
                    RectTransform rectTransform = academy_logo.GetComponent<RectTransform>();

                    // 부모의 부모 크기 가져오기
                    RectTransform grandParentRect = academy_logo.transform.parent.parent as RectTransform;
                    float grandParentWidth = grandParentRect.rect.width;
                    float grandParentHeight = grandParentRect.rect.height;

                    // 중앙 정렬을 위한 앵커 설정
                    rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    rectTransform.anchoredPosition = Vector2.zero; // 중앙에 위치

                    // 스프라이트 비율 계산
                    float spriteAspectRatio = (float)academy_logo.sprite.rect.width / academy_logo.sprite.rect.height;

                    // 너비 40% 기준으로 높이 계산
                    float desiredWidth = grandParentWidth * 0.4f;
                    float calculatedHeight = desiredWidth / spriteAspectRatio;

                    // 높이가 부모의 부모의 30%를 넘는지 체크
                    float maxHeight = grandParentHeight * 0.2f;
                    
                    // 높이 기준으로 설정 (높이 30% 고정, 너비 자동 조절)
                    aspectFitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                    aspectFitter.aspectRatio = spriteAspectRatio;
                    rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, maxHeight);
                    /*
                    if (calculatedHeight <= maxHeight)
                    {
                        // 너비 기준으로 설정
                        aspectFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
                        aspectFitter.aspectRatio = spriteAspectRatio;
                        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, desiredWidth);
                    }
                    

                    else
                    {
                        // 높이 기준으로 설정 (높이 30% 고정, 너비 자동 조절)
                        aspectFitter.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
                        aspectFitter.aspectRatio = spriteAspectRatio;
                        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, maxHeight);
                    }
                    */
                }

                else
                {
                    academy_logo.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
                }
            }

            foreach (TraitUI stat in stats)
                    stat.SetCard(card);

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

        }

        public void SetMaterial(Material mat)
        {
            if (title_background != null)
                title_background.material = mat;
            if (type_background != null)
                type_background.material = mat;
            if (range_background != null)
                range_background.material = mat;
            if (trait_background != null)
                trait_background.material = mat;
            if (card_image != null)
                card_image.material = mat;
            if (frame_image != null)
                frame_image.material = mat;
            //if (team_icon != null)
            //    team_icon.material = mat;
            if (attack_icon != null)
                attack_icon.material = mat;
            if (hp_icon != null)
                hp_icon.material = mat;
            if (cost_icon != null)
                cost_icon.material = mat;
        }

        public void SetOpacity(float opacity)
        {
            if (title_background != null)
                title_background.color = new Color(title_background.color.r, title_background.color.g, title_background.color.b, opacity);
            if (type_background != null)
                type_background.color = new Color(type_background.color.r, type_background.color.g, type_background.color.b, opacity);
            if (card_image != null)
                card_image.color = new Color(card_image.color.r, card_image.color.g, card_image.color.b, opacity);
            if (frame_image != null)
                frame_image.color = new Color(frame_image.color.r, frame_image.color.g, frame_image.color.b, opacity);
            if (range_background != null)
                range_background.color = new Color(range_background.color.r, range_background.color.g, range_background.color.b, opacity);
            if (club_background != null)
                club_background.color = new Color(club_background.color.r, club_background.color.g, club_background.color.b, opacity);
            //if (art_bg != null)
            //    art_bg.color = new Color(art_bg.color.r, art_bg.color.g, art_bg.color.b, opacity);
            //if (art_frame != null)
            //    art_frame.color = new Color(art_frame.color.r, art_frame.color.g, art_frame.color.b, opacity);
            if (trait_background != null)
                trait_background.color = new Color(trait_background.color.r, trait_background.color.g, trait_background.color.b, opacity);
            if (attack_icon != null)
                attack_icon.color = new Color(attack_icon.color.r, attack_icon.color.g, attack_icon.color.b, opacity);
            if (hp_icon != null)
                hp_icon.color = new Color(hp_icon.color.r, hp_icon.color.g, hp_icon.color.b, opacity);
            if (range_icon != null)
                range_icon.color = new Color(range_icon.color.r, range_icon.color.g, range_icon.color.b, opacity);
            if (cost_icon != null)
                cost_icon.color = new Color(cost_icon.color.r, cost_icon.color.g, cost_icon.color.b, opacity);
            if (type != null)
                type.color = new Color(type.color.r, type.color.g, type.color.b, opacity);
            if (attack != null)
                attack.color = new Color(attack.color.r, attack.color.g, attack.color.b, opacity);
            if (hp != null)
                hp.color = new Color(hp.color.r, hp.color.g, hp.color.b, opacity);
            if (range != null)
                range.color = new Color(range.color.r, range.color.g, range.color.b, opacity);
            if (cost != null)
                cost.color = new Color(cost.color.r, cost.color.g, cost.color.b, opacity);
            if (trait != null)
                trait.color = new Color(trait.color.r, trait.color.g, trait.color.b, opacity);
            if (clubs != null)
                clubs.color = new Color(clubs.color.r, clubs.color.g, clubs.color.b, opacity);
            if (card_title != null)
                card_title.color = new Color(card_title.color.r, card_title.color.g, card_title.color.b, opacity);
            if (card_text != null)
                card_text.color = new Color(card_text.color.r, card_text.color.g, card_text.color.b, opacity);
            if (card_name != null)
                card_name.color = new Color(card_name.color.r, card_name.color.g, card_name.color.b, opacity);
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (onClick != null)
                    onClick.Invoke(this);
            }

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (onClickRight != null)
                    onClickRight.Invoke(this);
            }
        }

        public CardData GetCard()
        {
            return card;
        }

        public VariantData GetVariant()
        {
            return variant;
        }
    }
}
