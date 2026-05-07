using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class TutorialMask : MaskableGraphic, ICanvasRaycastFilter
    {
        [SerializeField] private RectTransform _target; // 구멍을 뚫을 대상

        private Vector3[] _targetWorldCorners = new Vector3[4];
        private Vector3 _targetMin;
        private Vector3 _targetMax;

        private Rect _lastSelfRect;
        private Matrix4x4 _lastTargetMatrix;

        private bool IsTargetNull => _target == null;

        // 외부에서 대상 변경 가능
        public void SetTarget(RectTransform target)
        {
            _target = target;
            ForceRefresh();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ForceRefresh();
        }

        void LateUpdate()
        {
            if (IsTargetNull) return;

            bool selfRectChanged = (_lastSelfRect != rectTransform.rect);
            bool targetMatrixChanged = (_lastTargetMatrix != _target.localToWorldMatrix);

            if (selfRectChanged || targetMatrixChanged)
                ForceRefresh();
        }

        private void ForceRefresh()
        {
            _lastSelfRect = rectTransform.rect;

            if (IsTargetNull) return;

            _lastTargetMatrix = _target.localToWorldMatrix;
            _target.GetWorldCorners(_targetWorldCorners);

            // 대상의 월드 좌표 → 자신의 로컬 좌표로 변환
            Matrix4x4 selfWorldToLocal = rectTransform.worldToLocalMatrix;
            Vector3 vMin = new(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 vMax = new(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < 4; i++)
            {
                Vector3 localPoint = selfWorldToLocal.MultiplyPoint3x4(_targetWorldCorners[i]);
                vMin = Vector3.Min(vMin, localPoint);
                vMax = Vector3.Max(vMax, localPoint);
            }

            _targetMin = vMin;
            _targetMax = vMax;

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            // 타겟 없으면 그냥 전체를 덮는 사각형
            if (IsTargetNull)
            {
                base.OnPopulateMesh(vh);
                return;
            }

            // Outer Rect (자신의 RectTransform 전체)
            float outerLx = rectTransform.rect.xMin;
            float outerBy = rectTransform.rect.yMin;
            float outerRx = rectTransform.rect.xMax;
            float outerTy = rectTransform.rect.yMax;

            vh.AddVert(new Vector3(outerLx, outerTy), color, Vector2.zero); // 0 좌상
            vh.AddVert(new Vector3(outerRx, outerTy), color, Vector2.zero); // 1 우상
            vh.AddVert(new Vector3(outerRx, outerBy), color, Vector2.zero); // 2 우하
            vh.AddVert(new Vector3(outerLx, outerBy), color, Vector2.zero); // 3 좌하

            // Inner Rect (구멍 영역)
            float innerLx = _targetMin.x;
            float innerBy = _targetMin.y;
            float innerRx = _targetMax.x;
            float innerTy = _targetMax.y;

            vh.AddVert(new Vector3(innerLx, innerTy), color, Vector2.zero); // 4 좌상
            vh.AddVert(new Vector3(innerRx, innerTy), color, Vector2.zero); // 5 우상
            vh.AddVert(new Vector3(innerRx, innerBy), color, Vector2.zero); // 6 우하
            vh.AddVert(new Vector3(innerLx, innerBy), color, Vector2.zero); // 7 좌하

            // 도넛 삼각형 8개
            vh.AddTriangle(0, 1, 5); vh.AddTriangle(5, 4, 0); // 위
            vh.AddTriangle(1, 2, 6); vh.AddTriangle(6, 5, 1); // 오른쪽
            vh.AddTriangle(2, 3, 7); vh.AddTriangle(7, 6, 2); // 아래
            vh.AddTriangle(3, 0, 4); vh.AddTriangle(4, 7, 3); // 왼쪽
        }

        // 구멍 부분은 클릭 통과, 마스크 부분은 클릭 차단
        public bool IsRaycastLocationValid(Vector2 screenPos, Camera eventCamera)
        {
            if (!isActiveAndEnabled) return true;
            if (IsTargetNull) return true;

            return !RectTransformUtility.RectangleContainsScreenPoint(_target, screenPos, eventCamera);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            ForceRefresh();
        }
#endif
    }
}