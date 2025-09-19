using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.FX;
using TcgEngine.Client;
using UnityEditor;
using Unity.Netcode;

namespace TcgEngine
{
    /// <summary>
    /// Static functions to spawn FX prefabs
    /// </summary>

    public class FXTool : MonoBehaviour
    {
        public static GameObject DoFX(GameObject fx_prefab, Vector3 pos, float duration = 5f)
        {
            if (fx_prefab != null)
            {
                GameObject fx = Instantiate(fx_prefab, pos, GetFXRotation());
                Destroy(fx, duration);
                return fx;
            }
            return null;
        }

        public static GameObject DoSnapFX(GameObject fx_prefab, Transform snap_target)
        {
            return DoSnapFX(fx_prefab, snap_target, Vector3.zero);
        }

        public static GameObject DoSnapFX(GameObject fx_prefab, Transform snap_target, Vector3 offset)
        {
            if (fx_prefab != null && snap_target != null)
            {
                GameObject fx = Instantiate(fx_prefab, snap_target.transform.position + snap_target.transform.up * 2f, GetFXRotation());
                SnapFX snap = fx.AddComponent<SnapFX>();
                snap.target = snap_target;
                snap.offset = offset;
                Destroy(fx, 5f);
                return fx;
            }
            return null;
        }

        private static Quaternion GetFXRotation()
        {
            GameBoard board = GameBoard.Get();
            Vector3 facing = board != null ? board.transform.forward : Vector3.forward;
            return Quaternion.LookRotation(facing, Vector3.up);
        }

        public static Quaternion GetFXRotation(GameObject fx, GameObject caster = null, GameObject target = null)
        {
            FXSetting setting = fx.GetComponentInChildren<FXSetting>();
            Quaternion? rot = null;
            if (setting == null)
                return GetFXRotation();

            switch (setting.rotation_option)
            {
                case FXRotationOption.NoRotation:
                    rot = GetFXRotation();
                    break;

                case FXRotationOption.CasterToTarget:
                    if (caster != null && target != null)
                    {
                        Vector3 dir = (target.transform.position - caster.transform.position).normalized;
                        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                        rot = Quaternion.Euler(0f, 0f, angle);
                    }
                    break;

                case FXRotationOption.TargetToCaster:
                    if (caster != null && target != null)
                    {
                        Vector3 dir = (caster.transform.position - target.transform.position).normalized;
                        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                        rot = Quaternion.Euler(0f, 0f, angle);
                    }
                    break;

                default:
                    break;
            }

            if (rot == null)
                return GetFXRotation();

            return rot.Value;
        }
    }
}
