using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using TcgEngine.Client;

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
        //모든 FX Animator가 공용으로 사용하는 스테이트 명 규약
        public const string state_self = "Self";
        public const string state_enemy = "Enemy";

        [Header("Rotation")]
        public FXRotationOption rotation_option;

        [Header("Animation")]
        [Tooltip("Whether this FX's Impact publishes the linked display state. Disable for visuals whose result is carried by another FX.")]
        public bool presentation_impact = true;
        [Tooltip("Delay before creating a scheduled presentation FX. Its state waits for the eventual Impact.")]
        [Min(0f)] public float presentation_delay;
        [Tooltip("Presentation duration in seconds; zero measures animation clips and particles.")]
        [Min(0f)] public float presentation_duration;
        public Animator animator;   //자신/상대 스테이트 분기가 필요한 FX만 할당

        [Header("Persistent Loop Synchronization")]
        [Tooltip("Persistent decorative FX only: follow the session presentation clock instead of spawn time. Requires event-free looping clips on a single Animator layer.")]
        public bool synchronize_loop;
        [Tooltip("Seconds per full loop at normal replay speed, including the intended state speed.")]
        [Min(0.01f)] public float loop_period = 2.6666667f;

        //이벤트 주인이 자신인지 판단하여 공용 스테이트 명 반환 (관전 모드는 관전 대상 기준)
        public static string GetOwnerState(int owner_player_id)
        {
            return FXOwnerState.GetState(owner_player_id);
        }

        //판정 + 재생까지 하는 편의 API
        public void PlayOwnerState(int owner_player_id)
        {
            FXTool.SetOwner(gameObject, owner_player_id);
        }
    }
}
