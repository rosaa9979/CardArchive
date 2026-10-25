using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.FX;
using DG.Tweening;

namespace TcgEngine.UI
{
    /// <summary>
    /// Global notice text displayed on top of every scene when things cant be done
    /// Created from Resources on first use and kept between scenes
    /// </summary>

    public class WarningText : MonoBehaviour
    {
        public AudioClip warning_audio;
        public Text text;

        private CanvasGroup canvas_group;
        private Sequence sequence;

        private const string prefab_path = "UI/WarningText";
        private const float default_duration = 1f;

        private static WarningText instance;

        void Awake()
        {
            canvas_group = GetComponent<CanvasGroup>();
            canvas_group.alpha = 0f;

            //Get() only creates the notice when none exists, an extra one means it was placed in a scene
            if (instance != null && instance != this)
            {
                Debug.LogWarning("WarningText already exists, remove the one placed in " + gameObject.scene.name);
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
            sequence?.Kill(complete: false);
            text.text = txt;
            canvas_group.alpha = 0f;
            transform.localScale = Vector3.one * 0.9f;

            sequence = DOTween.Sequence();
            sequence.Append(canvas_group.DOFade(1f, 0.25f).SetEase(Ease.OutExpo));
            sequence.Join(transform.DOScale(1.01f, 0.25f).SetEase(Ease.OutBack, 2.0f));
            sequence.Append(transform.DOScale(1f, 0.1f));
            sequence.AppendInterval(duration);
            sequence.SetUpdate(true); //Keep playing when the game is paused (timeScale = 0)
            sequence.OnComplete(() => {
                canvas_group.alpha = 0.0f;
                sequence = null;
            });
            sequence.OnKill(() => {
                canvas_group.alpha = 0.0f;
                sequence = null;
            });

            AudioTool.Get().PlaySFX("warning", warning_audio, 0.7f, false);
        }

        public void Hide()
        {
            sequence?.Kill(complete: false);
            canvas_group.alpha = 0f;
        }

        public static void ShowText(string txt, float duration = default_duration)
        {
            if (string.IsNullOrEmpty(txt))
                return;

            WarningText w = WarningText.Get();
            w?.Show(txt, duration);
        }

        public static void ShowNotYourTurn()
        {
            ShowText("자신의 차례가 아닙니다");
        }

        public static void ShowExhausted()
        {
            ShowText("No more action");
        }

        public static void ShowNoMana()
        {
            ShowText("청휘석이 부족합니다");
        }

        public static void ShowSpellImmune()
        {
            ShowText("Spell Immunity");
        }

        public static void ShowInvalidTarget()
        {
            ShowText("잘못된 대상입니다");
        }

        public static WarningText Get()
        {
            if (instance == null && !Application.isBatchMode && Application.isPlaying)
            {
                GameObject prefab = Resources.Load<GameObject>(prefab_path);
                if (prefab == null)
                {
                    Debug.LogError("WarningText prefab not found in Resources: " + prefab_path);
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
