using UnityEditor;
using UnityEditor.SceneManagement;
using TcgEngine;

public static class EffectTestSceneMenu
{
    [MenuItem("Tools/Card Archive/Open Effect Test Scene")]
    public static void Open()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(EffectTestPanel.ScenePath);
    }
}
