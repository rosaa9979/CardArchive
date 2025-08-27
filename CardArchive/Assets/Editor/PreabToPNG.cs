using UnityEngine;
using UnityEditor;
using System.IO;

public class PrefabToPNG
{
    [MenuItem("Tools/Export Prefab To PNG")]
    static void ExportPrefabToPNG()
    {
        // 현재 선택된 오브젝트 가져오기
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("프리팹이나 오브젝트를 선택하세요!");
            return;
        }

        // 전용 카메라 생성
        GameObject camObj = new GameObject("TempCamera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.backgroundColor = Color.clear;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.orthographic = true;
        cam.orthographicSize = 5;

        // RenderTexture 준비
        int width = 512;
        int height = 512;
        RenderTexture rt = new RenderTexture(width, height, 24);
        cam.targetTexture = rt;

        // 카메라 위치 오브젝트 중심으로 설정
        Bounds bounds = GetRendererBounds(selected);
        cam.transform.position = bounds.center + new Vector3(0, 0, -10);
        cam.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y);

        // 렌더링
        cam.Render();

        // Texture2D로 읽기
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        // PNG 저장
        string path = EditorUtility.SaveFilePanel("Save PNG", Application.dataPath, selected.name + ".png", "png");
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("저장 완료: " + path);
        }

        // 정리
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(camObj);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }

    // 오브젝트의 전체 Bounds 계산
    static Bounds GetRendererBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(obj.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);

        return bounds;
    }
}