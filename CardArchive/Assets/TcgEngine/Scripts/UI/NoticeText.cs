using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace TcgEngine.UI
{
    /// <summary>
    /// Global announcement text (server maintenance, admin messages...) displayed on top of every scene,
    /// separate from WarningText and rendered above it (same sorting layer, higher order).
    /// ShowText queues a text displayed for a while, texts are shown one after the other.
    /// HoldText keeps one visible (countdowns) until ReleaseText, queued texts wait until it is released.
    /// Created from Resources on first use and kept between scenes
    /// </summary>

    public class NoticeText : MonoBehaviour
    {
        public AudioClip notice_audio;
        public Text text;

        private CanvasGroup canvas_group;
        private Sequence sequence;
        private bool holding = false;
        private bool showing = false;
        private readonly Queue<KeyValuePair<string, float>> queue = new Queue<KeyValuePair<string, float>>();

        private const string prefab_path = "UI/NoticeText";
        private const float default_duration = 3f;

        private static NoticeText instance;

        void Awake()
        {
            canvas_group = GetComponent<CanvasGroup>();
            canvas_group.alpha = 0f;

            //Get() only creates the notice when none exists, an extra one means it was placed in a scene
            if (instance != null && instance != this)
            {
                Debug.LogWarning("NoticeText already exists, remove the one placed in " + gameObject.scene.name);
                return;
            }

            instance = this;
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                sequence?.Kill(complete: false);
                instance = null;
            }
        }

        public void Show(string txt, float duration = default_duration)
        {
            queue.Enqueue(new KeyValuePair<string, float>(txt, duration));
            ShowNext();
        }

        //Stays visible until Release, later calls only update the text. Interrupts a text being shown.
        public void Hold(string txt)
        {
            if (holding)
            {
                text.text = txt;
                return;
            }

            holding = true;
            showing = false;
            Appear(txt);
        }

        public void Release()
        {
            if (!holding)
                return;

            sequence?.Kill(complete: false); //Still holding: the kill keeps the text visible for the fade
            holding = false;
            sequence = DOTween.Sequence();
            sequence.Append(canvas_group.DOFade(0f, 0.25f));
            sequence.SetUpdate(true);
            sequence.OnComplete(() => {
                sequence = null;
                ShowNext();
            });
        }

        //Hides the current text and drops the queued ones
        public void Clear()
        {
            queue.Clear();
            holding = false;
            showing = false;
            sequence?.Kill(complete: false);
            sequence = null;
            canvas_group.alpha = 0f;
        }

        private void ShowNext()
        {
            if (holding || showing || sequence != null || queue.Count == 0)
                return;

            KeyValuePair<string, float> next = queue.Dequeue();
            showing = true;
            Appear(next.Key);
            sequence.AppendInterval(next.Value);
            sequence.OnComplete(() => {
                canvas_group.alpha = 0.0f;
                showing = false;
                sequence = null;
                ShowNext();
            });
        }

        private void Appear(string txt)
        {
            sequence?.Kill(complete: false);
            text.text = txt;
            canvas_group.alpha = 0f;
            transform.localScale = Vector3.one * 0.9f;

            sequence = DOTween.Sequence();
            sequence.Append(canvas_group.DOFade(1f, 0.25f).SetEase(Ease.OutExpo));
            sequence.Join(transform.DOScale(1.01f, 0.25f).SetEase(Ease.OutBack, 2.0f));
            sequence.Append(transform.DOScale(1f, 0.1f));
            sequence.SetUpdate(true); //Keep playing when the game is paused (timeScale = 0)

            if (notice_audio != null)
                AudioTool.Get().PlaySFX("notice", notice_audio, 0.7f, false);
        }

        public static void ShowText(string txt, float duration = default_duration)
        {
            if (string.IsNullOrEmpty(txt))
                return;

            NoticeText n = NoticeText.Get();
            n?.Show(txt, duration);
        }

        public static void HoldText(string txt)
        {
            if (string.IsNullOrEmpty(txt))
                return;

            NoticeText n = NoticeText.Get();
            n?.Hold(txt);
        }

        //Never creates the notice just to release or clear it
        public static void ReleaseText()
        {
            instance?.Release();
        }

        public static void ClearText()
        {
            instance?.Clear();
        }

        public static NoticeText Get()
        {
            if (instance == null && !Application.isBatchMode && Application.isPlaying)
            {
                GameObject prefab = Resources.Load<GameObject>(prefab_path);
                if (prefab == null)
                {
                    Debug.LogError("NoticeText prefab not found in Resources: " + prefab_path);
                    return null;
                }

                GameObject obj = Instantiate(prefab);
                obj.name = prefab.name;
                DontDestroyOnLoad(obj);
            }
            return instance;
        }
    }
}
