using TcgEngine.Client;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine;
using TcgEngine.UI;

namespace TcgEngine.FX
{
    /// <summary>
    /// Line FX that appear when dragin a board card to attack
    /// </summary>

    public class MouseLineFX : MonoBehaviour
    {
        [Header("Dot")]
        public GameObject dot_template;
        public float dot_spacing = 0.2f;

        private List<GameObject> dot_list = new List<GameObject>();
        private List<Vector3> points = new List<Vector3>();

        //Pushed each frame by TargetingManager: the usable require-target card being dragged, or null.
        private HandCard play_targeting_card;

        void Awake()
        {
            //Register with the manager so it drives this FX (static setter -> Awake-order independent).
            TargetingManager.SetLineFX(this);
        }

        public void SetPlayTargetingCard(HandCard card)
        {
            play_targeting_card = card;
        }

        void Start()
        {
            dot_template.SetActive(false);
        }

        void Update()
        {
            
            if (!GameClient.Get().IsReady())
                return;

            RefreshLine();
            RefreshRender();
            
        }

        private void RefreshLine()
        {
            points.Clear();

            Game gdata = GameClient.Get().GetGameData();
            PlayerControls controls = PlayerControls.Get();
            BoardCard bcard = controls.GetSelected();

            bool visible = false;
            Vector3 source = Vector3.zero;
            /*
            if (bcard != null)
            {
                source = bcard.transform.position;
                visible = true;
            }
            */

            //Targeting state is computed by TargetingManager and pushed into play_targeting_card,
            //so the aim line shows/hides together with the crosshair/text and only for a usable card.
            HandCard drag = play_targeting_card;
            if (drag != null)
            {
                source = drag.transform.position;
                visible = true;
            }

            if (gdata.selector == SelectorType.SelectTarget && gdata.IsPlayerSelectorTurn(GameClient.Get().GetPlayer()))
            {
                BoardCard caster = BoardCard.Get(gdata.selector_caster_uid);
                HeroUI player_hero = HeroUI.Get(false);
                if (caster != null)
                {
                    source = caster.transform.position;
                    visible = true;
                }

                else if (player_hero.GetCard().uid == gdata.selector_caster_uid)
                {
                    source = player_hero.transform.position;
                    visible = true;
                }
            }

            if (visible)
            {
                Vector3 dest = GameBoard.Get().RaycastMouseBoard();
                Vector3 dir = (dest - source).normalized;
                float dist = (dest - source).magnitude;

                float value = 0f;
                while (value < dist)
                {
                    Vector3 pos = source + dir * value;
                    points.Add(pos);

                    value += dot_spacing;
                }

                AbilityData iability = AbilityData.Get(gdata.selector_ability_id);

                if (iability != null && string.IsNullOrWhiteSpace(iability.selector_desc))
                {
                }
            }
        }

        private void RefreshRender()
        {
            while (dot_list.Count < points.Count)
            {
                AddDot();
            }

            int index = 0;
            foreach (GameObject dot in dot_list)
            {
                bool active = false;
                if (index < points.Count)
                {
                    Vector3 pos = points[index];
                    dot.transform.position = pos;
                    active = true;
                }

                if (dot.activeSelf != active)
                    dot.SetActive(active);

                index++;
            }
        }

        public void AddDot()
        {
            GameObject dot = Instantiate(dot_template, transform);
            dot.SetActive(true);
            dot_list.Add(dot);
        }
    }
}
