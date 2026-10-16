using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.FX;
using TcgEngine.Client;
using UnityEditor;
using Unity.Netcode;

namespace TcgEngine
{
    //FX 정렬 대역: 씬의 고정 요소(슬롯 -10~1, 보드 카드 -8~19, 손패 20, 마우스라인 25, 패널 100) 사이 빈 대역에 FX를 끼워넣는다
    //SpriteRenderer/ParticleSystemRenderer/Canvas(Screen Space - Camera) 모두 같은 sortingOrder 체계를 공유하므로 월드/UI FX에 공통 적용 가능
    public enum FXLayer
    {
        Default = 0,    //프리팹에 설정된 order 그대로 사용
        UnderCard = 10, //보드 슬롯 위, 보드 카드 아래
        OverCard = 20,  //보드 카드 위, 손패 아래
        OverHand = 30,  //손패 위, 마우스 라인 아래
    }

    /// <summary>
    /// Static functions to spawn FX prefabs
    /// </summary>

    public class FXTool : MonoBehaviour
    {
        // 2 = target impact / field entry / field exit; 1 = board or caster cue.
        // Normal DoFX remains a decorative cue (priority 0).
        // Reserve NOW, create LATER. Never wrap DoFX in a timer for a state-bearing FX.
        public static void ScheduleStateFX(GameObject prefab, Vector3 position, float delay = -1f,
            int priority = 2, System.Action<GameObject> onCreated = null, bool abilityTarget = false, int? ownerPlayerId = null,
            System.Action<FXPlayback> onPlaybackReady = null)
        {
            if (prefab == null) { onPlaybackReady?.Invoke(null); return; }
            var client = GameClient.Get();
            GameClient.AnimationReservation reservation;
            using (client?.PresentationCue(priority)) reservation = client?.ReservePresentationAnimation(prefab, abilityTarget);
            var setting = prefab.GetComponentInChildren<FXSetting>(true);
            float wait = delay >= 0f ? delay : setting != null ? setting.presentation_delay : 0f;
            System.Action create = () => {
                if (reservation != null && (client == null || !client.IsCurrentReservation(reservation))) return;
                GameObject fx = Instantiate(prefab, position, GetFXRotation());
                ManageLifetime(fx, GetDuration(prefab), reservation, ownerPlayerId, onPlaybackReady);
                onCreated?.Invoke(fx);
            };
            if (wait > 0f) TimeTool.WaitFor(wait, create); else create();
        }
        private static Dictionary<string, GameObject> unique_fx = new Dictionary<string, GameObject>();

        //Only one FX per key can be alive at a time, spawning a new one destroys the previous one
        public static GameObject DoUniqueFX(string key, GameObject fx_prefab, Vector3 pos, float duration = -1f, int? ownerPlayerId = null)
        {
            if (fx_prefab == null)
                return null;

            if (unique_fx.TryGetValue(key, out GameObject existing) && existing != null)
            {
                // Replacing a visual must not destroy an outstanding state cue. Let it
                // deliver its own Impact/Complete instead of manufacturing a result.
                var playback = existing.GetComponentInChildren<FXPlayback>(true);
                if (playback == null || !playback.IsPlaying) Destroy(existing);
            }

            GameObject fx = DoFX(fx_prefab, pos, duration, ownerPlayerId);
            unique_fx[key] = fx;
            return fx;
        }

        public static GameObject DoFX(GameObject fx_prefab, Vector3 pos, float duration = -1f, int? ownerPlayerId = null)
        {
            if (fx_prefab != null)
            {
                duration = duration < 0f ? GetDuration(fx_prefab) : duration;
                var reservation = GameClient.Get()?.ReservePresentationAnimation(fx_prefab);
                GameObject fx = Instantiate(fx_prefab, pos, GetFXRotation());
                ManageLifetime(fx, duration, reservation, ownerPlayerId);
                return fx;
            }
            return null;
        }

        public static GameObject DoFX(GameObject fx_prefab, Vector3 pos, FXLayer layer, float duration = -1f, int? ownerPlayerId = null)
        {
            GameObject fx = DoFX(fx_prefab, pos, duration, ownerPlayerId);
            SetFXLayer(fx, layer);
            return fx;
        }

        //Spawn a screen UI FX under a canvas, worldPositionStays=false so the prefab's RectTransform (anchors/offsets) is preserved
        public static GameObject DoUIFX(GameObject fx_prefab, Transform canvas_parent, float duration = -1f, int? ownerPlayerId = null)
        {
            if (fx_prefab != null && canvas_parent != null)
            {
                duration = duration < 0f ? GetDuration(fx_prefab) : duration;
                var reservation = GameClient.Get()?.ReservePresentationAnimation(fx_prefab);
                GameObject fx = Instantiate(fx_prefab, canvas_parent, false);

                //연출용 UI가 게임 입력을 가로채지 않게 클릭 통과 처리
                CanvasGroup group = fx.GetComponent<CanvasGroup>();
                if (group == null)
                    group = fx.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                ManageLifetime(fx, duration, reservation, ownerPlayerId);
                return fx;
            }
            return null;
        }

        public static GameObject DoUIFX(GameObject fx_prefab, Transform canvas_parent, FXLayer layer, float duration = -1f, int? ownerPlayerId = null)
        {
            GameObject fx = DoUIFX(fx_prefab, canvas_parent, duration, ownerPlayerId);
            if (fx != null && layer != FXLayer.Default && fx.GetComponent<Canvas>() == null)
                fx.AddComponent<Canvas>(); //부모 캔버스 order 대신 FX 대역으로 그리려면 자체 캔버스가 필요
            SetFXLayer(fx, layer);
            return fx;
        }

        //FX의 모든 렌더러/캔버스를 지정 sortingLayer/orderInLayer로 세팅한다, 같은 값이 되는 내부 요소 간 앞뒤는 z거리로 정해진다
        public static void SetFXLayer(GameObject fx, string sorting_layer, int order_in_layer)
        {
            if (fx == null)
                return;

            foreach (Renderer render in fx.GetComponentsInChildren<Renderer>(true))
            {
                render.sortingLayerName = sorting_layer;
                render.sortingOrder = order_in_layer;
            }

            foreach (Canvas canvas in fx.GetComponentsInChildren<Canvas>(true))
            {
                canvas.overrideSorting = true;
                canvas.sortingLayerName = sorting_layer;
                canvas.sortingOrder = order_in_layer;
            }
        }

        public static void SetFXLayer(GameObject fx, FXLayer layer)
        {
            if (fx == null || layer == FXLayer.Default)
                return;

            GetFXLayerBand(layer, out string sorting_layer, out int order_in_layer);
            SetFXLayer(fx, sorting_layer, order_in_layer);
        }

        //소팅 레이어(Default < UI < TopUI)가 order보다 우선이므로 대역은 (레이어, order) 쌍으로 정의
        //보드 요소(슬롯 -10~1, 카드 -8~18)는 Default 레이어, 손패는 UI/20
        private static void GetFXLayerBand(FXLayer layer, out string sorting_layer, out int order_in_layer)
        {
            switch (layer)
            {
                case FXLayer.UnderCard: sorting_layer = "Default"; order_in_layer = -9; break; //슬롯(-10)과 카드(-8) 사이
                case FXLayer.OverCard: sorting_layer = "Default"; order_in_layer = 19; break;  //카드 상단(18) 위, UI 레이어(손패)는 항상 이 위
                case FXLayer.OverHand: sorting_layer = "UI"; order_in_layer = 21; break;       //손패(UI/20) 위
                default: sorting_layer = "Default"; order_in_layer = 0; break;
            }
        }

        public static GameObject DoSnapFX(GameObject fx_prefab, Transform snap_target, int? ownerPlayerId = null)
        {
            return DoSnapFX(fx_prefab, snap_target, Vector3.zero, ownerPlayerId);
        }

        public static GameObject DoSnapFX(GameObject fx_prefab, Transform snap_target, Vector3 offset, int? ownerPlayerId = null)
        {
            if (fx_prefab != null && snap_target != null)
            {
                var reservation = GameClient.Get()?.ReservePresentationAnimation(fx_prefab);
                GameObject fx = Instantiate(fx_prefab, snap_target.transform.position + snap_target.transform.up * 2f, GetFXRotation());
                SnapFX snap = fx.AddComponent<SnapFX>();
                snap.target = snap_target;
                snap.offset = offset;
                float duration = GetDuration(fx_prefab);
                ManageLifetime(fx, duration, reservation, ownerPlayerId);
                return fx;
            }
            return null;
        }

        // The owner rule is shared by world, UI, attached, status and ongoing FX.
        public static void SetOwner(GameObject fx, int? ownerPlayerId) => FXOwnerState.Bind(fx, ownerPlayerId);

        // Persistent decorations never reserve a presentation boundary or get a finite timer.
        public static GameObject DoPersistentFX(GameObject prefab, Transform parent, int ownerPlayerId, bool preserveWorldSize = false)
        {
            if (prefab == null || parent == null) return null;
            GameObject fx;
            if (preserveWorldSize)
            {
                fx = Instantiate(prefab, parent.position, GetFXRotation(prefab));
                fx.transform.SetParent(parent, true);
            }
            else
            {
                fx = Instantiate(prefab, parent, false);
                fx.transform.localPosition = Vector3.zero;
            }
            SetOwner(fx, ownerPlayerId);
            FXLoopSync.Bind(fx);
            return fx;
        }

        private static void ManageLifetime(GameObject fx, float duration, GameClient.AnimationReservation reservation, int? ownerPlayerId,
            System.Action<FXPlayback> onPlaybackReady = null)
        {
            bool bound = GameClient.Get() != null && GameClient.Get().BindPresentationAnimation(fx, reservation, onPlaybackReady);
            // Prepare may replace/disable the Animator. Select only after preparation and never evaluate it here.
            SetOwner(fx, ownerPlayerId);
            if (bound) return;
            if (onPlaybackReady != null)
            {
                // Baseline/direct spawns still use real animation events without a state reservation.
                var playback = FXPlayback.Prepare(fx, false);
                if (playback != null)
                {
                    playback.Bind(System.Array.Empty<System.Action>(), null);
                    onPlaybackReady(playback);
                    SetOwner(fx, ownerPlayerId);
                    playback.Arm();
                    return;
                }
                onPlaybackReady(null);
            }
            PresentationClock.Hold(duration);
            Destroy(fx, duration);
        }

        public static float GetDuration(GameObject prefab)
        {
            if (prefab == null) return 0f;
            FXSetting setting = prefab.GetComponentInChildren<FXSetting>(true);
            if (setting != null && setting.presentation_duration > 0f) return setting.presentation_duration;
            float duration = 0f;
            foreach (Animator animator in prefab.GetComponentsInChildren<Animator>(true))
                if (animator.runtimeAnimatorController != null)
                    foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                        duration = Mathf.Max(duration, clip.length / Mathf.Max(0.01f, Mathf.Abs(animator.speed)));
            foreach (ParticleSystem particles in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particles.main;
                if (!main.loop)
                    duration = Mathf.Max(duration, (main.startDelay.constantMax + main.duration + main.startLifetime.constantMax)
                        / Mathf.Max(0.01f, main.simulationSpeed));
            }
            return duration > 0f ? duration : 5f;
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
