using UnityEditor;
using UnityEditor.UI;
using TcgEngine.UI;

namespace TcgEngine.EditorTool
{
    /// <summary>
    /// Inspector for CardGrid. GridLayoutGroup's editor applies to child classes and only
    /// draws the base fields, hiding CardGrid's own fields. This re-exposes them.
    /// </summary>

    [CustomEditor(typeof(CardGrid))]
    [CanEditMultipleObjects]
    public class CardGridEditor : GridLayoutGroupEditor
    {
        private SerializedProperty auto_cell_scale;
        private SerializedProperty design_cell_size;

        protected override void OnEnable()
        {
            base.OnEnable();
            auto_cell_scale = serializedObject.FindProperty("auto_cell_scale");
            design_cell_size = serializedObject.FindProperty("design_cell_size");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            serializedObject.Update();
            EditorGUILayout.PropertyField(auto_cell_scale);
            EditorGUILayout.PropertyField(design_cell_size);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
