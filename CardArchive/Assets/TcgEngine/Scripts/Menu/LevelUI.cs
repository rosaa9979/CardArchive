using DG.Tweening;
using TcgEngine.Client;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    /// <summary>
    /// Single AdventurePanel entry. Renders any IGameTypeView (LevelData /
    /// TutorialData / TotalAssaultData / ...) and launches the corresponding
    /// match on click. Wrapper child is the animation target so LayoutGroup
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
        private Tweener move_tween;
        private Tweener fade_tween;

        void Awake()
        {
            Button btn = GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(OnClick);
        }

        void OnDestroy()
        {
            KillTweens();
        }

        public void SetData(IGameTypeView d)
        {
            data = d;
            Refresh();
        }

        public void Refresh()
        {
            if (data == null)
                return;

            if (title != null)
                title.text = data.GetTitle();

            if (icon != null)
            {
                Sprite s = data.GetIcon();
                icon.sprite = s;
                icon.enabled = s != null;
            }

            if (deck != null)
            {
                DeckData d = data.GetDisplayDeck();
                if (d != null)
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
                wrapper_canvas.alpha = 0f;
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
                wrapper_canvas.alpha = 1f;
        }

        public void PlayIntro(float duration, float delay, Ease ease)
        {
            KillTweens();
            if (wrapper != null)
                move_tween = wrapper.DOLocalMoveX(0f, duration).SetEase(ease).SetDelay(delay);
            if (wrapper_canvas != null)
                fade_tween = wrapper_canvas.DOFade(1f, duration).SetDelay(delay);
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
            if (data == null)
                return;

            data.Launch();
        }
    }
}
