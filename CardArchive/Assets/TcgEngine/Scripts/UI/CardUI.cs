using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TcgEngine.Client;
using System;

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
        public Image description_image;
        public Image type_background;
        public Image attack_background;
        public Image attack_icon;
        public Image hp_background;
        public Image hp_icon;
        public Image cost_icon;
        public Image member_icon;
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

        [Header("Mat")]
        public Material default_mat;
        private static readonly int DissolveAmountProperty = Shader.PropertyToID("_DissolveAmount");



        void Awake()
        {

        }

        public void SetCard(Card card)
        {
            if (card == null)
                return;

            SetCard(card.CardData, card.VariantData);

            if (cost != null)
                cost.text = card.GetMana().ToString();
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
                float totalWidth = textWidth + (2 * padding);

                imageRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, totalWidth);
            }

            //Card (runtime) face text: prefer text_format, fall back to text when empty.
            //Resolved with the live card's current stats/status; runs every frame via BoardCard.Update.
            if (card_text != null)
            {
                CardData cd = card.CardData;
                string d = FormatText(cd, card);
                if (!string.IsNullOrEmpty(d))
                    card_text.text = d;
            }

            foreach (TraitUI stat in stats)
                stat.SetCard(card);
        }

        //Resolve the card face text: text_format takes priority, text is the fallback.
        //text_format substitutes {0},{1},... with resolved text_values.
        //card may be null (static/CardData context) — values then resolve from base/definition.
        private string FormatText(CardData card_data, Card card)
        {
            if (string.IsNullOrEmpty(card_data.text_format))
                return card_data.text;

            string template = card_data.text_format;
            if (card_data.text_values == null || card_data.text_values.Length == 0)
                return template;

            object[] args = new object[card_data.text_values.Length];
            for (int i = 0; i < card_data.text_values.Length; i++)
                args[i] = card_data.text_values[i] != null ? card_data.text_values[i].Resolve(card, card_data) : "";

            try
            {
                return string.Format(template, args);
            }
            catch (System.FormatException)
            {
                return template;
            }
        }

        public void SetCard(CardData card, VariantData variant)
        {
            if (card == null)
                return;

            this.card = card;
            this.variant = variant;

            //SetMaterial(default_mat);

            if (frame_image != null)
            {
                Color32 frame_color = new Color32();

                frame_color = GetFrameColor(card);

                frame_image.color = frame_color;
            }

            if (type != null)
                type.text = card.GetTypeId().ToString();
            if (card_image != null)
                card_image.sprite = card.GetFullArt(variant);
            if (card_title != null)
                card_title.text = card.GetTitle().ToUpper();
            //CardData (static/collection) face text: same text_format/text priority as the runtime path,
            //resolved with no runtime card so values fall back to the card definition.
            if (card_text != null)
                card_text.text = FormatText(card, null);

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
                cost_icon.enabled = card.type != CardType.Club;
            if (member_icon != null)
                member_icon.enabled = card.type == CardType.Club;
            //if (cost != null)
            //    cost.enabled = card.type != CardType.Club;
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
            if (range != null)
                range.text = card.GetRange().ToString();
            if (clubs != null)
                clubs.text = string.Join(" / ", card.clubs.Select(club => club.title));
            if (trait != null)
                trait.text = string.Join(" / ", card.traits.Select(tra => tra.title));

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
                //academy 아이콘이 유효할 때만 표시. 참조(academy / icon / 부모)가 비어 있으면
                //미리보기 전체가 예외로 죽지 않도록 투명 처리하고 건너뛴다.
                Sprite academy_icon = (card.clubs.Length > 0 && card.clubs[0].academy != null)
                    ? card.clubs[0].academy.acadmey_icon
                    : null;

                if (academy_icon != null)
                {
                    academy_logo.sprite = academy_icon;
                    academy_logo.color = new Color(academy_logo.color.r, academy_logo.color.g, academy_logo.color.b, 0.2f);

                    // 컴포넌트 가져오기
                    AspectRatioFitter aspectFitter = academy_logo.GetComponent<AspectRatioFitter>();
                    RectTransform rectTransform = academy_logo.GetComponent<RectTransform>();

                    // 부모의 부모 크기 가져오기
                    RectTransform grandParentRect = academy_logo.transform.parent != null
                        ? academy_logo.transform.parent.parent as RectTransform
                        : null;

                    if (rectTransform != null && grandParentRect != null)
                    {
                        float grandParentWidth = grandParentRect.rect.width;

                        // 중앙 정렬을 위한 앵커 설정
                        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                        rectTransform.pivot = new Vector2(0.5f, 0.5f);
                        rectTransform.anchoredPosition = Vector2.zero; // 중앙에 위치

                        // 스프라이트 비율 계산
                        float spriteAspectRatio = (float)academy_icon.rect.width / academy_icon.rect.height;

                        // 너비를 부모의 30%로 설정
                        float desiredWidth = grandParentWidth * 0.3f;
                        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, desiredWidth);

                        // AspectRatioFitter를 너비 기준 모드로 전환
                        if (aspectFitter != null)
                        {
                            aspectFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
                            aspectFitter.aspectRatio = spriteAspectRatio;
                        }
                    }
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
                title_background.material = new Material(mat);
            if (type_background != null)
                type_background.material = new Material(mat);
            if (description_image != null)
                description_image.material = new Material(mat);
            if (range_background != null)
                range_background.material = new Material(mat);
            if (club_background != null)
                club_background.material = new Material(mat);
            if (trait_background != null)
                trait_background.material = new Material(mat);
            if (card_image != null)
                card_image.material = new Material(mat);
            if (frame_image != null)
                frame_image.material = new Material(mat);
            if (attack_icon != null)
                attack_icon.material = new Material(mat);
            if (hp_icon != null)
                hp_icon.material = new Material(mat);
            if (range_icon != null)
                range_icon.material = new Material(mat);
            if (cost_icon != null)
                cost_icon.material = new Material(mat);
            if (academy_logo != null)
                academy_logo.material = new Material(mat);

            if (type != null)
                type.material = new Material(mat);
            if (attack != null)
                attack.material = new Material(mat);
            if (hp != null)
                hp.material = new Material(mat);
            if (cost != null)
                cost.material = new Material(mat);
            if (range != null)
                range.material = new Material(mat);
            if (clubs != null)
                clubs.material = new Material(mat);
            if (trait != null)
                trait.material = new Material(mat);
            if (card_title != null)
                card_title.material = new Material(mat);
            if (card_text != null)
                card_text.material = new Material(mat);
        }

        public void Dissolve(float alpha)
        {
            if (title_background.material != null && title_background.material.HasProperty(DissolveAmountProperty))
                title_background.material.SetFloat(DissolveAmountProperty, alpha);
            if (type_background.material != null && type_background.material.HasProperty(DissolveAmountProperty))
                type_background.material.SetFloat(DissolveAmountProperty, alpha);
            if (description_image.material != null && description_image.material.HasProperty(DissolveAmountProperty))
                description_image.material.SetFloat(DissolveAmountProperty, alpha);
            if (range_background.material != null && range_background.material.HasProperty(DissolveAmountProperty))
                range_background.material.SetFloat(DissolveAmountProperty, alpha);
            if (club_background.material != null && club_background.material.HasProperty(DissolveAmountProperty))
                club_background.material.SetFloat(DissolveAmountProperty, alpha);
            if (trait_background.material != null && trait_background.material.HasProperty(DissolveAmountProperty))
                trait_background.material.SetFloat(DissolveAmountProperty, alpha);
            if (card_image.material != null && card_image.material.HasProperty(DissolveAmountProperty))
                card_image.material.SetFloat(DissolveAmountProperty, alpha);
            if (frame_image.material != null && frame_image.material.HasProperty(DissolveAmountProperty))
                frame_image.material.SetFloat(DissolveAmountProperty, alpha);
            if (attack_icon.material != null && attack_icon.material.HasProperty(DissolveAmountProperty))
                attack_icon.material.SetFloat(DissolveAmountProperty, alpha);
            if (hp_icon.material != null && hp_icon.material.HasProperty(DissolveAmountProperty))
                hp_icon.material.SetFloat(DissolveAmountProperty, alpha);
            if (range_icon.material != null && range_icon.material.HasProperty(DissolveAmountProperty))
                range_icon.material.SetFloat(DissolveAmountProperty, alpha);
            if (cost_icon.material != null && cost_icon.material.HasProperty(DissolveAmountProperty))
                cost_icon.material.SetFloat(DissolveAmountProperty, alpha);
            if (academy_logo.material != null && academy_logo.material.HasProperty(DissolveAmountProperty))
                academy_logo.material.SetFloat(DissolveAmountProperty, alpha);

            if (type.material != null && type.material.HasProperty(DissolveAmountProperty))
                type.material.SetFloat(DissolveAmountProperty, alpha);
            if (attack.material != null && attack.material.HasProperty(DissolveAmountProperty))
                attack.material.SetFloat(DissolveAmountProperty, alpha);
            if (hp.material != null && hp.material.HasProperty(DissolveAmountProperty))
                hp.material.SetFloat(DissolveAmountProperty, alpha);
            if (cost.material != null && cost.material.HasProperty(DissolveAmountProperty))
                cost.material.SetFloat(DissolveAmountProperty, alpha);
            if (range.material != null && range.material.HasProperty(DissolveAmountProperty))
                range.material.SetFloat(DissolveAmountProperty, alpha);
            if (clubs.material != null && clubs.material.HasProperty(DissolveAmountProperty))
                clubs.material.SetFloat(DissolveAmountProperty, alpha);
            if (trait.material != null && trait.material.HasProperty(DissolveAmountProperty))
                trait.material.SetFloat(DissolveAmountProperty, alpha);
            if (card_title.material != null && card_title.material.HasProperty(DissolveAmountProperty))
                card_title.material.SetFloat(DissolveAmountProperty, alpha);
            if (card_text.material != null && card_text.material.HasProperty(DissolveAmountProperty))
                card_text.material.SetFloat(DissolveAmountProperty, alpha);
                
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
            if (description_image != null)
                description_image.color = new Color(description_image.color.r, description_image.color.g, description_image.color.b, opacity);
            if (range_background != null)
                range_background.color = new Color(range_background.color.r, range_background.color.g, range_background.color.b, opacity);
            if (club_background != null)
                club_background.color = new Color(club_background.color.r, club_background.color.g, club_background.color.b, opacity);
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

        private Color GetFrameColor(CardData card)
        {
            switch (card.type)
            {
                case CardType.Hero:
                    return new Color32(155, 89, 182, 255);

                case CardType.Club:
                    return new Color32(46, 204, 113, 255);

                case CardType.Student:
                    return new Color32(26, 188, 156, 255);

                case CardType.NonStudent:
                    return new Color32(243, 156, 18, 255);

                case CardType.Place:
                    return new Color32(63, 81, 181, 255);

                case CardType.Spell:
                    return new Color32(231, 76, 60, 255);

                default:
                    return new Color(255, 255, 255, 255);
            }
        }
    }
}
