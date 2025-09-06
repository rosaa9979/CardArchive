using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

namespace TcgEngine.FX
{
    public class DestroyAnimFX : StateMachineBehaviour
    {
        [Header("Destroy Settings")]
        [SerializeField] private bool destroyOnExit = true;
        [SerializeField] private float exitDelay = 0f;
        
        // 애니메이션 상태가 끝날 때 호출
        override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (destroyOnExit)
            {
                if (exitDelay > 0)
                {
                    Destroy(animator.gameObject, exitDelay);
                }
                else
                {
                    Destroy(animator.gameObject);
                }
            }
        }
    }

}