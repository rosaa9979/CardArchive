using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace TcgEngine.UI
{
    /// <summary>
    /// A toggle button that will disable other buttons in same group when clicked
    /// </summary>

    public class IconButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string group;
        public string value;

        public Image active_img;
        public Sprite selected_ui;
        public Sprite unselected_ui;
        public bool on_if_all_off;

        public UnityAction<IconButton> onClick;

        private bool active = false;
        private bool focus = false;
        private Button button;
        private static List<IconButton> toggle_list = new List<IconButton>();

        void Awake()
        {
            toggle_list.Add(this);
            button = GetComponent<Button>();
            button.onClick.AddListener(OnClick);

            if (!on_if_all_off && active_img != null)
            {
                active_img.sprite = unselected_ui;
                active_img.SetNativeSize();
            }
        }

        private void OnDestroy()
        {
            toggle_list.Remove(this);
        }

        void Start()
        {

        }

        private void Update()
        {
            if (on_if_all_off)
            {
                if (active_img != null && IsAllOff(group))
                {
                    active_img.sprite = selected_ui;
                    active_img.SetNativeSize();
                }
            }
        }

        void OnClick()
        {
            bool was_active = active;

            DeactivateAll(group);

            if (!was_active)
                Activate();

            if (onClick != null)
                onClick.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (GameTool.IsMobile())
                return;

            focus = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            focus = false;
        }

        public void SetValue(string new_value)
        {
            value = new_value;
        }

        public void SetActive(bool act)
        {
            if (act) Activate();
            else Deactivate();
        }

        public void Activate()
        {
            active = true;
            if (active_img != null)
            {
                active_img.sprite = selected_ui;
                active_img.SetNativeSize();
            }
        }

        public void Deactivate()
        {
            active = false;
            if (active_img != null)
            {
                active_img.sprite = unselected_ui;
                active_img.SetNativeSize();
            }
        }

        public bool IsFocus()
        {
            return focus;
        }

        public bool IsActive()
        {
            return active;
        }

        public static string GetCurrentValue(string group)
        {
            List<IconButton> toggles = GetAll(group);

            foreach (IconButton toggle in toggles)
            {
                if (toggle.IsActive())
                    return toggle.value;
            }

            foreach (IconButton toggle in toggles)
            {
                if (toggle.on_if_all_off)
                    return toggle.value;
            }

            return "";
        }

        public static IconButton GetFocus(string group)
        {
            List<IconButton> toggles = GetAll(group);

            foreach (IconButton toggle in toggles)
            {
                if (toggle.IsFocus())
                {
                    return toggle;
                }
            }

            return null;
        }

        public static bool IsAllOff(string group)
        {
            bool all_off = true;
            foreach (IconButton toggle in toggle_list)
            {
                if (toggle.group == group && toggle.IsActive())
                    all_off = false;
            }
            return all_off;
        }

        public static void DeactivateAll(string group)
        {
            foreach (IconButton toggle in toggle_list)
            {
                if (toggle.group == group)
                    toggle.Deactivate();
            }
        }

        public static List<IconButton> GetAll(string group)
        {
            List<IconButton> toggles = new List<IconButton>();
            foreach (IconButton toggle in toggle_list)
            {
                if (toggle.group == group)
                    toggles.Add(toggle);
            }
            return toggles;
        }
    }
}