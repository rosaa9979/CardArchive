using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    /// <summary>A textured solid coin projected into a Canvas, without an extra camera or shader.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class CoinTossGraphic : UnityEngine.UI.MaskableGraphic
    {
        public Texture front;
        public Texture back;
        public bool rim;
        public Quaternion rotation = Quaternion.identity;
        public float diameter = 310f;
        private const int Segments = 128;
        private const float HalfDepth = 0.065f;
        private static readonly Color Blue = new Color32(24, 143, 255, 255);
        private static readonly Color Pink = new Color32(240, 113, 171, 255);

        private bool FrontVisible => (rotation * Vector3.forward).z > 0f;
        public override Texture mainTexture => rim ? Texture2D.whiteTexture : (FrontVisible ? front : back);

        public void SetPose(Quaternion pose, float size)
        {
            rotation = pose;
            diameter = size;
            SetVerticesDirty();
            SetMaterialDirty();
        }

        private Vector3 Project(Vector3 point)
        {
            Vector3 rotated = rotation * point;
            float perspective = 6f / (6f - rotated.z);
            return new Vector3(rotated.x, rotated.y, 0f) * (diameter * 0.5f * perspective);
        }

        private void Vertex(VertexHelper vh, Vector3 point, Vector2 uv, Color tint)
        {
            vh.AddVert(Project(point), tint * color, uv);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (rim)
            {
                // Two axial bands reproduce the blue/pink side reference. Only the near half
                // of the convex rim is emitted, so Canvas draw order needs no depth buffer.
                for (int band = 0; band < 2; band++)
                {
                    float z0 = band == 0 ? -HalfDepth : 0f;
                    float z1 = band == 0 ? 0f : HalfDepth;
                    Color tint = band == 0 ? Pink : Blue;
                    for (int i = 0; i < Segments; i++)
                    {
                        float a = i * Mathf.PI * 2f / Segments;
                        float b = (i + 1) * Mathf.PI * 2f / Segments;
                        Vector3 normal = rotation * new Vector3(Mathf.Cos((a + b) * 0.5f), Mathf.Sin((a + b) * 0.5f), 0f);
                        if (normal.z * 6f <= 1f) continue;
                        Color shade = tint * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(normal.y * 0.3f + normal.z * 0.7f));
                        shade.a = 1f;
                        int v = vh.currentVertCount;
                        Vertex(vh, new Vector3(Mathf.Cos(a), Mathf.Sin(a), z0), Vector2.zero, shade);
                        Vertex(vh, new Vector3(Mathf.Cos(b), Mathf.Sin(b), z0), Vector2.zero, shade);
                        Vertex(vh, new Vector3(Mathf.Cos(b), Mathf.Sin(b), z1), Vector2.zero, shade);
                        Vertex(vh, new Vector3(Mathf.Cos(a), Mathf.Sin(a), z1), Vector2.zero, shade);
                        vh.AddTriangle(v, v + 1, v + 2);
                        vh.AddTriangle(v, v + 2, v + 3);
                    }
                }
                return;
            }

            if (mainTexture == null) return;
            bool isFront = FrontVisible;
            float depth = isFront ? HalfDepth : -HalfDepth;
            // Original 390x430 references: circular artwork centered at (194,219), radius 189.
            // Sampling just the disc retains the whale's white interior and excludes the margin.
            Vector2 center = new Vector2(194f / 390f, 211f / 430f);
            Vector2 radius = new Vector2(188f / 390f, 188f / 430f);
            Vertex(vh, new Vector3(0f, 0f, depth), center, Color.white);
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                Vector2 p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 uv = center + Vector2.Scale(new Vector2(isFront ? p.x : -p.x, p.y), radius);
                Vertex(vh, new Vector3(p.x, p.y, depth), uv, Color.white);
                if (i > 0) vh.AddTriangle(0, i, i + 1);
            }
        }
    }
}
