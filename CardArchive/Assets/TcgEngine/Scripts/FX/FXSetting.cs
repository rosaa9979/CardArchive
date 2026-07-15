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
        public Animator animator;   //자신/상대 스테이트 분기가 필요한 FX만 할당

        //이벤트 주인이 자신인지 판단하여 공용 스테이트 명 반환 (관전 모드는 관전 대상 기준)
        public static string GetOwnerState(int owner_player_id)
        {
            GameClient client = GameClient.Get();
            bool is_self = client != null && client.GetPlayerID() == owner_player_id;
            return is_self ? state_self : state_enemy;
        }

        //판정 + 재생까지 하는 편의 API
        public void PlayOwnerState(int owner_player_id)
        {
            if (animator != null)
                animator.Play(GetOwnerState(owner_player_id));
        }
    }
}
