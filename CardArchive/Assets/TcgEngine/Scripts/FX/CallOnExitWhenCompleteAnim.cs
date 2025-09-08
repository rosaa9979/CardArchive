using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

namespace TcgEngine.FX
{
    public interface IAnimationEndHandler
    {
        void OnAnimationEnd(int fullPathHash, int layerIndex);
    }

    public class CallOnExitWhenCompleteAnim : StateMachineBehaviour
    {
        [Tooltip("이 값 이상 재생되면 '완주'로 간주")]
        [Range(0.0f, 1.5f)] public float completionThreshold = 0.99f;

        [Tooltip("루프 상태에서도 한 바퀴 끝날 때 콜백을 보낼지")]
        public bool fireOnLoop = false;

        [Tooltip("중간에 끊겨도(인터럽트) 콜백을 보낼지")]
        public bool alsoWhenInterrupted = false;

        private bool _completed;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            _completed = false; // 매 진입 시 초기화
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // 루프 상태이고, 루프 끝날 때마다 콜백 보내고 싶지 않다면 스킵
            if (stateInfo.loop && !fireOnLoop) return;

            // normalizedTime: 0~1 한 번 재생, 그 이후 1.0 이상
            if (!_completed && stateInfo.normalizedTime >= completionThreshold)
            {
                Debug.Log("Hello");
                _completed = true; // 한 번만 True로 전환
            }
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // 완주했거나, 혹은 인터럽트에도 호출 옵션이 켜져 있으면 콜백
            if (_completed || alsoWhenInterrupted)
            {
                // 우선 인터페이스로 타입 안전하게 전달
                if (animator.TryGetComponent<IAnimationEndHandler>(out var handler))
                {
                    handler.OnAnimationEnd(stateInfo.fullPathHash, layerIndex);
                }
                else
                {
                    // 최소 호환: 동일 오브젝트에 "OnAnimationFinished" 메서드가 있으면 호출
                    animator.gameObject.SendMessage("OnAnimationFinished", SendMessageOptions.DontRequireReceiver);
                }
            }
        }
    }

}