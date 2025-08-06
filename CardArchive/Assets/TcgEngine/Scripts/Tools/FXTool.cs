using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Spine.Unity;
using TcgEngine.FX;
using TcgEngine.Client;

namespace TcgEngine
{
    /// <summary>
    /// Static functions to spawn FX prefabs
    /// </summary>

    public class FXTool : MonoBehaviour
    {
        private const string CHARACTER_SKELETON_DATA_PATH = "Spine/Explosion/explosion_SkeletonData";

        public static GameObject DoSpineFX(Vector3 pos, Quaternion rotation)
        {
            SkeletonDataAsset skeletonDataAsset= Resources.Load<SkeletonDataAsset>(CHARACTER_SKELETON_DATA_PATH);
            SkeletonAnimation spawnedSkeleton = SkeletonAnimation.NewSkeletonAnimationGameObject(skeletonDataAsset);
            if (spawnedSkeleton != null)
            {
                spawnedSkeleton.transform.position = pos;
                spawnedSkeleton.transform.rotation = rotation;
                spawnedSkeleton.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

                Renderer spawnedSkeletonRenderer = spawnedSkeleton.GetComponent<Renderer>();
                if (spawnedSkeletonRenderer != null)
                    spawnedSkeletonRenderer.sortingOrder = 999;
                spawnedSkeleton.Initialize(true);
                spawnedSkeleton.AnimationState.SetAnimation(0, "animation", false);

                return spawnedSkeleton.gameObject;
            }

            return null;
        }
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
    }
}
