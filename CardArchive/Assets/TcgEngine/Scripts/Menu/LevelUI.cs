using DG.Tweening;
using TcgEngine.Client;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace TcgEngine.UI
{
    /// <summary>
    /// Reusable menu entry. Displays supplied content and invokes the callback
    /// assigned by its owner. IGameTypeView is an optional display adapter. Wrapper child is the animation target so LayoutGroup
    /// keeps controlling the root's slot position.
    /// </summary>
    public class LevelUI : MonoBehaviour
    {
        [Header("Animation target")]
        public RectTransform wrapper;
        public CanvasGroup wrapper_canvas;

        [Header("Display")]
        public Text title;
        public Text subtitle;
        public Image icon;
        public DeckDisplay deck;
        public GameObject completed;

        private IGameTypeView data;
        private bool launching;
        private bool lock_after_click;
        private UnityAction click_callback;
        private Tweener move_tween;
        private Tweener fade_tween;

        void Awake()
        {
            Button btn = GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OnClick);
        }

        void OnDisable()
        {
            launching = false;
            KillTweens();
        }

        void OnDestroy()
        {
            click_callback = null;
            KillTweens();
        }

        // The owner decides what a click does; the entry never launches a game itself.
        public void SetData(IGameTypeView d, UnityAction onClick, bool lockAfterClick = false)
        {
            data = d;
            SetCallback(onClick, lockAfterClick);
            Refresh();
        }

        // For story chapters or other entries that do not implement IGameTypeView.
        public void SetContent(string titleText, string description, Sprite image,
            UnityAction onClick, bool lockAfterClick = false)
        {
            data = null;
            SetCallback(onClick, lockAfterClick);
            if (title != null) title.text = titleText ?? string.Empty;
            if (subtitle != null) subtitle.text = description ?? string.Empty;
            if (icon != null)
            {
                icon.sprite = image;
                icon.enabled = image != null;
            }
            if (deck != null) deck.gameObject.SetActive(false);
            if (completed != null) completed.SetActive(false);
            gameObject.SetActive(true);
        }

        private void SetCallback(UnityAction onClick, bool lockAfterClick)
        {
            click_callback = onClick;
            lock_after_click = lockAfterClick;
            launching = false;
        }

        public void Refresh()
        {
            if (data == null)
                return;

            if (title != null)
                title.text = data.GetTitle();

            if (subtitle != null)
                subtitle.text = data is TutorialData tutorial ? tutorial.description : string.Empty;

            if (icon != null)
            {
                Sprite s = data.GetIcon();
                icon.sprite = s;
                icon.enabled = s != null;
            }

            if (deck != null)
            {
                DeckData d = data.GetDisplayDeck();
                bool show_deck = !(data is TutorialData) && d != null;
                deck.gameObject.SetActive(show_deck);
                if (show_deck && d != null)
                    deck.SetDeck(d);
            }

            if (completed != null)
            {
                UserData udata = Authenticator.Get() != null ? Authenticator.Get().GetUserData() : null;
                completed.SetActive(udata != null && udata.HasReward(data.GetId()));
            }

            gameObject.SetActive(true);
        }

        //Sets wrapper to the pre-intro state (offset position + alpha 0).
        public void ResetIntroState(float offset_x)
        {
            KillTweens();
            if (wrapper != null)
            {
                Vector3 pos = wrapper.localPosition;
                pos.x = offset_x;
                wrapper.localPosition = pos;
            }
            if (wrapper_canvas != null)
            {
                wrapper_canvas.alpha = 0f;
                wrapper_canvas.interactable = false;
                wrapper_canvas.blocksRaycasts = false;
            }
        }

        //Snaps wrapper to final state without animation (for off-viewport entries).
        public void SetFinalState()
        {
            KillTweens();
            if (wrapper != null)
            {
                Vector3 pos = wrapper.localPosition;
                pos.x = 0f;
                wrapper.localPosition = pos;
            }
            if (wrapper_canvas != null)
            {
                wrapper_canvas.alpha = 1f;
                wrapper_canvas.interactable = true;
                wrapper_canvas.blocksRaycasts = true;
            }
        }

        public void PlayIntro(float duration, float delay, Ease ease)
        {
            KillTweens();
            if (wrapper != null)
                move_tween = wrapper.DOLocalMoveX(0f, duration).SetEase(ease).SetDelay(delay).SetUpdate(true);
            if (wrapper_canvas != null)
                fade_tween = wrapper_canvas.DOFade(1f, duration).SetDelay(delay).SetUpdate(true)
                    .OnComplete(() =>
                    {
                        wrapper_canvas.interactable = true;
                        wrapper_canvas.blocksRaycasts = true;
                    });
        }

        private void KillTweens()
        {
            if (move_tween != null && move_tween.IsActive())
                move_tween.Kill();
            if (fade_tween != null && fade_tween.IsActive())
                fade_tween.Kill();
            move_tween = null;
            fade_tween = null;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void OnClick()
        {
            Button button = GetComponent<Button>();
            if (!isActiveAndEnabled || click_callback == null || launching
                || (button != null && !button.IsInteractable())
                || (wrapper_canvas != null && !wrapper_canvas.interactable))
                return;

            launching = lock_after_click;
            try
            {
                click_callback.Invoke();
            }
            catch
            {
                launching = false;
                throw;
            }
        }
    }
}
