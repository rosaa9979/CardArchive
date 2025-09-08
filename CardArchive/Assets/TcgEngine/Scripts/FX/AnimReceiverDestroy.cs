using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.FX
{
    public class AnimReceiverDestroy : MonoBehaviour, IAnimationEndHandler
    {
        public void OnAnimationEnd(int hash, int layer)
        {
            Destroy(gameObject);
        }

        public void OnAnimationFinished() // SendMessage용
        {
            Destroy(gameObject);
        }
    }
}