using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.FX;
using DG.Tweening;
using UnityEngine.Rendering;

namespace TcgEngine.UI
{
    /// <summary>
    /// Text that is displayed at the bottom of the screen when things cant be done
    /// </summary>

    public class WarningText : MonoBehaviour
    {
        public AudioClip warning_audio;
        public Text text;

        private CanvasGroup canvas_group;
        private Sequence sequence;

        private static WarningText instance;

        void Awake()
        {
            instance = this;
            canvas_group = GetComponent<CanvasGroup>();
            canvas_group.alpha = 0f;
        }

        void Update()
        {

        }

        public void Show(string txt)
        {
            sequence?.Kill(complete: false);
            text.text = txt;
            canvas_group.alpha = 0f;
            transform.localScale = Vector3.one * 0.9f;
            //animator.SetTrigger("play");

            sequence = DOTween.Sequence();
            sequence.Append(canvas_group.DOFade(1f, 0.25f).SetEase(Ease.OutExpo));
            sequence.Join(transform.DOScale(1.01f, 0.25f).SetEase(Ease.OutBack, 2.0f));
            sequence.Append(transform.DOScale(1f, 0.1f));
            sequence.AppendInterval(1.0f);
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
            canvas_group.alpha = 0f;
        }

        public static void ShowText(string txt)
        {
            WarningText w = WarningText.Get();
            w.Show(txt);
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
            return instance;
        }
    }
}
