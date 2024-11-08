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

        private bool focus = false;
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
            trigger.triggers.Add(entry);
            trigger.triggers.Add(exit);
        }

        // Update is called once per frame
        void Update()
        {
            if (!GameClient.Get().IsReady())
                return;
        }

        private void OnEnterMouse()
        {
            Debug.Log("in");
            focus = true;
        }

        private void OnExitMouse()
        {
            Debug.Log("out");
            focus = false;
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
    }
}
