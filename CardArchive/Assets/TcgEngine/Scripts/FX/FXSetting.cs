using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace TcgEngine.FX
{
    /// <summary>
    /// FX에 대한 설정 값
    /// </summary>

    public enum FXRotationOption
    {
        NoRotation = 0,
        CasterToTarget = 10,
        TargetToCaster = 20
    }

    public class FXSetting : MonoBehaviour
    {
        [Header("Rotation")]
        public FXRotationOption rotation_option;

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}
