using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CustomInputFieldPlaceholder : MonoBehaviour
{
    public InputField inputField;
    public Text placeholder;

    void Start()
    {
        // EventTrigger 추가
        EventTrigger eventTrigger = inputField.gameObject.GetComponent<EventTrigger>();
        if (eventTrigger == null)
        {
            eventTrigger = inputField.gameObject.AddComponent<EventTrigger>();
        }

        // PointerClick 이벤트 추가
        EventTrigger.Entry pointerClick = new EventTrigger.Entry();
        pointerClick.eventID = EventTriggerType.PointerClick;
        pointerClick.callback.AddListener(OnInputFieldClick);
        eventTrigger.triggers.Add(pointerClick);

        // Select 이벤트 추가
        EventTrigger.Entry select = new EventTrigger.Entry();
        select.eventID = EventTriggerType.Select;
        select.callback.AddListener(OnInputFieldSelect);
        eventTrigger.triggers.Add(select);

        // Deselect 이벤트 추가
        EventTrigger.Entry deselect = new EventTrigger.Entry();
        deselect.eventID = EventTriggerType.Deselect;
        deselect.callback.AddListener(OnInputFieldDeselect);
        eventTrigger.triggers.Add(deselect);
    }

    void OnInputFieldClick(BaseEventData eventData)
    {
        placeholder.enabled = false;
    }

    void OnInputFieldSelect(BaseEventData eventData)
    {
        placeholder.enabled = false;
    }

    void OnInputFieldDeselect(BaseEventData eventData)
    {
        if (string.IsNullOrEmpty(inputField.text))
        {
            placeholder.enabled = true;
        }
    }
}