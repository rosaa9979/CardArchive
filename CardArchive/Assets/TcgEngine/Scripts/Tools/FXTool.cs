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
        private static Dictionary<string, GameObject> unique_fx = new Dictionary<string, GameObject>();

        //Only one FX per key can be alive at a time, spawning a new one destroys the previous one
        public static GameObject DoUniqueFX(string key, GameObject fx_prefab, Vector3 pos, float duration = 5f)
        {
            if (fx_prefab == null)
                return null;

            if (unique_fx.TryGetValue(key, out GameObject existing) && existing != null)
                Destroy(existing);

            GameObject fx = DoFX(fx_prefab, pos, duration);
            unique_fx[key] = fx;
            return fx;
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

        public static GameObject DoFX(GameObject fx_prefab, Vector3 pos, FXLayer layer, float duration = 5f)
        {
            GameObject fx = DoFX(fx_prefab, pos, duration);
            SetFXLayer(fx, layer);
            return fx;
        }

        //Spawn a screen UI FX under a canvas, worldPositionStays=false so the prefab's RectTransform (anchors/offsets) is preserved
        public static GameObject DoUIFX(GameObject fx_prefab, Transform canvas_parent, float duration = 5f)
        {
            if (fx_prefab != null && canvas_parent != null)
            {
                GameObject fx = Instantiate(fx_prefab, canvas_parent, false);

                //연출용 UI가 게임 입력을 가로채지 않게 클릭 통과 처리
                CanvasGroup group = fx.GetComponent<CanvasGroup>();
                if (group == null)
                    group = fx.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;

                Destroy(fx, duration);
                return fx;
            }
            return null;
        }

        public static GameObject DoUIFX(GameObject fx_prefab, Transform canvas_parent, FXLayer layer, float duration = 5f)
        {
            GameObject fx = DoUIFX(fx_prefab, canvas_parent, duration);
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
