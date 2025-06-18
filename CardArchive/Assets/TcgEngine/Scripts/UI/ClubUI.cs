using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using UnityEngine.EventSystems;

namespace TcgEngine.UI
{
    

    public class ClubUI : MonoBehaviour
    {
        public bool opponent;
        public GameObject club_area;
        public Image club_image;
        public Sprite default_icon;
        public int index;

        private List<Card> reference_clubs;
        private Card club;
        private bool focus = false;

        private static List<ClubUI> ui_list = new List<ClubUI>();

        private void Awake()
        {
            ui_list.Add(this);
        }

        private void OnDestroy()
        {
            ui_list.Remove(this);
        }

        // Start is called before the first frame update
        void Start()
        {
            EventTrigger trigger = club_area.GetComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((eventData) => { OnEnterMouse(); });

            EventTrigger.Entry exit = new EventTrigger.Entry();
            exit.eventID = EventTriggerType.PointerExit;
            exit.callback.AddListener((eventData) => { OnExitMouse(); });

            EventTrigger.Entry mouse_down = new EventTrigger.Entry();
            mouse_down.eventID = EventTriggerType.PointerDown;
            mouse_down.callback.AddListener((eventData) => { OnMouseDown(); });

            trigger.triggers.Add(entry);
            trigger.triggers.Add(exit);
            trigger.triggers.Add(mouse_down);
        }

        // Update is called once per frame
        void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            reference_clubs = opponent ? GameClient.Get().GetOpponentPlayer().clubs_revealed : GetPlayer().cards_club;

            if (index + 1 <= reference_clubs.Count)
                club = reference_clubs[index];
            
            club_image.sprite = club?.CardData.GetBoardArt(club.VariantData) ?? default_icon;
        }

        private void OnEnterMouse()
        {
            focus = true;
        }

        private void OnExitMouse()
        {
            focus = false;
        }

        private void OnMouseDown()
        {
            if (GameTool.IsMobile())
                focus = true;
        }

        public bool IsFocus()
        {
            return focus;
        }

        public int GetPlayerID()
        {
            return opponent ? GameClient.Get().GetOpponentPlayerID() : GameClient.Get().GetPlayerID();
        }

        public Player GetPlayer()
        {
            Game gdata = GameClient.Get().GetGameData();
            return gdata.GetPlayer(GetPlayerID());
        }

        public Card GetCard()
        {
            return club;
        }

        public static ClubUI GetFocus()
        {
            foreach (ClubUI ui in ui_list)
            {
                if (ui.IsFocus())
                    return ui;
            }
            return null;
        }
    }
}
