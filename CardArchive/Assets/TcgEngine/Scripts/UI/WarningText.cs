using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.FX;

namespace TcgEngine.UI
{
    /// <summary>
    /// Text that is displayed at the bottom of the screen when things cant be done
    /// </summary>

    public class WarningText : MonoBehaviour, IAnimationEndHandler
    {
        public AudioClip warning_audio;
        public Text text;

        private CanvasGroup canvas_group;
        private Animator animator;

        private static WarningText instance;

        void Awake()
        {
            instance = this;
            canvas_group = GetComponent<CanvasGroup>();
            animator = GetComponent<Animator>();
            canvas_group.alpha = 0f;
        }

        void Update()
        {

        }

        public void Show(string txt)
        {
            text.text = txt;
            canvas_group.alpha = 1f;
            animator.SetTrigger("play");
            AudioTool.Get().PlaySFX("warning", warning_audio, 0.7f, false);
        }

        public void Hide()
        {
            canvas_group.alpha = 0f;
        }

        public void OnAnimationEnd(int fullPathHash, int layerIndex)
        {
            Hide();
        }

        public static void ShowText(string txt)
        {
            WarningText w = WarningText.Get();
            w.Show(txt);
        }

        public static void ShowNotYourTurn()
        {
            ShowText("Not your turn");
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
