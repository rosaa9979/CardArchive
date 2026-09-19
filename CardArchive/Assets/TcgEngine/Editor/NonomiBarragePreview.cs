using UnityEditor;
using UnityEngine;
using TcgEngine.FX;

namespace TcgEngine.EditorTools
{
    /// <summary>A separate preview camera keeps the user's open scene untouched.</summary>
    public sealed class NonomiBarragePreview : EditorWindow
    {
        private PreviewRenderUtility preview;
        private NonomiBarrageFX effect;
        private float time = 0.2f;
        private bool animate = true;
        private double previous_time;

        [MenuItem("Tools/Card Archive/FX/Nonomi Barrage Preview")]
        public static void Open()
        {
            var window = GetWindow<NonomiBarragePreview>("Nonomi Barrage");
            window.minSize = new Vector2(640f, 410f);
        }

        private void OnEnable()
        {
            previous_time = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            if (preview != null) preview.Cleanup();
            preview = null;
            effect = null;
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (animate && effect != null)
            {
                time += (float)(now - previous_time);
                if (time > effect.Duration + 0.65f) time = 0f;
                Repaint();
            }
            previous_time = now;
        }

        private void OnGUI()
        {
            if (preview == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TcgEngine/Prefabs/FX/Nonomi/NonomiBarrageFX.prefab");
                if (prefab == null) { EditorGUILayout.HelpBox("Nonomi prefab is not available.", MessageType.Info); return; }
                preview = new PreviewRenderUtility();
                var instance = Instantiate(prefab);
                preview.AddSingleGO(instance);
                effect = instance.GetComponent<NonomiBarrageFX>();
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = 5.4f;
                preview.camera.nearClipPlane = 0.1f;
                preview.camera.farClipPlane = 30f;
                preview.camera.transform.position = new Vector3(0, 0, -10);
                preview.camera.transform.rotation = Quaternion.identity;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(0.022f, 0.035f, 0.065f);
            }
            EditorGUILayout.LabelField("노노미, 총을 쏴", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Bottom-centre fan barrage · 147 rounds · presentation only");
            animate = EditorGUILayout.Toggle("Loop preview", animate);
            EditorGUI.BeginChangeCheck();
            float sample = EditorGUILayout.Slider("Time", Mathf.Min(time, effect.Duration), 0f, effect.Duration);
            if (EditorGUI.EndChangeCheck()) { time = sample; animate = false; }
            Rect area = GUILayoutUtility.GetRect(100f, 10000f, 240f, 10000f, GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint)
            {
                preview.camera.aspect = area.width / area.height;
                effect.SamplePreview(time, preview.camera);
                preview.BeginPreview(area, GUIStyle.none);
                preview.Render(true);
                preview.EndAndDrawPreview(area);
            }
        }
    }
}
