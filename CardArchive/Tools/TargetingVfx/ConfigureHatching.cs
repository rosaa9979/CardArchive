using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using TcgEngine.Client;
using TcgEngine.FX;

public static class ConfigureRangeHatching
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run while Editor is stopped.");
        var shader = Shader.Find("TcgEngine/RangeHatching");
        if (shader == null || ShaderUtil.ShaderHasError(shader))
            throw new InvalidOperationException("Hatching shader failed to compile.");
        const string materialPath = "Assets/TcgEngine/Materials/FX/RangeHatching.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        const string slotPath = "Assets/TcgEngine/Prefabs/Gameplay/BoardSlot.prefab";
        var root = PrefabUtility.LoadPrefabContents(slotPath);
        try
        {
            var tile = root.GetComponent<BoardSlot>();
            var overlay = tile.overlay_renderer;
            material.SetVector("_ShapeSize", overlay.sprite.bounds.size);
            material.SetColor("_InkColor", new Color(0.98f, 0.98f, 0.96f, 1));
            material.SetColor("_OutlineColor", new Color(0.10f, 0.16f, 0.25f, 1));
            material.SetFloat("_FlowSpeed", 0.018f);
            EditorUtility.SetDirty(material);
            overlay.sharedMaterial = material;
            overlay.enabled = false;
            overlay.sortingOrder = -9;
            overlay.color = Color.white;
            overlay.transform.localPosition = Vector3.zero;
            overlay.transform.localScale = new Vector3(0.78f, 0.78f, 1);
            PrefabUtility.SaveAsPrefabAsset(root, slotPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        const string aimPath = "Assets/TcgEngine/Prefabs/FX/AimTarget.prefab";
        root = PrefabUtility.LoadPrefabContents(aimPath);
        try
        {
            root.GetComponent<AimTargetFX>().target_fx.GetComponent<SpriteRenderer>().sortingOrder = -8;
            PrefabUtility.SaveAsPrefabAsset(root, aimPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssetIfDirty(material);
        return Validate();
    }

    public static string Validate()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/TcgEngine/Prefabs/Gameplay/BoardSlot.prefab");
        try
        {
            var slot = root.GetComponent<BoardSlot>();
            var fx = root.GetComponent<BoardSlotFX>();
            var overlay = slot.overlay_renderer;
            Action<string> invoke = name => typeof(BoardSlotFX).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fx, null);
            invoke("Awake");
            Check(!overlay.enabled, "Initially hidden");
            fx.SetAnimParameter(true);
            invoke("LateUpdate");
            Check(overlay.enabled, "Selected visible");
            fx.ResetIndicator();
            fx.SetAnimParameter(true);
            invoke("LateUpdate");
            Check(overlay.enabled, "Reset then select remains visible");
            fx.ResetIndicator();
            invoke("LateUpdate");
            Check(!overlay.enabled, "Deselected hidden");
            fx.SetAnimParameter(true);
            invoke("LateUpdate");
            invoke("OnDisable");
            Check(!overlay.enabled, "Disable clears overlay");
            var aim = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TcgEngine/Prefabs/FX/AimTarget.prefab").GetComponent<AimTargetFX>();
            var card = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TcgEngine/Prefabs/Gameplay/BoardCard.prefab").GetComponent<BoardCard>();
            Check(slot.GetComponent<SpriteRenderer>().sortingOrder < overlay.sortingOrder, "Above tile");
            Check(overlay.sortingOrder < aim.target_fx.GetComponent<SpriteRenderer>().sortingOrder, "Below target");
            Check(overlay.sortingOrder < card.card_sprite.sortingOrder, "Below card");
            Check(!ShaderUtil.ShaderHasError(overlay.sharedMaterial.shader), "Shader compilation");
            return "PASS: hidden idle, selected visible, reset/select continuity, deselection, disable, tile < hatch < target < card, shader compile.";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static string RenderPreview()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        RenderTexture rt = null;
        Texture2D image = null;
        var previous = RenderTexture.active;
        var previewMaterials = new System.Collections.Generic.List<Material>();
        try
        {
            var cameraObject = new GameObject("Preview camera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = 1.6f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.14f, 0.17f, 0.23f, 1);
            rt = new RenderTexture(1200, 480, 24);
            camera.targetTexture = rt;
            var slot = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TcgEngine/Prefabs/Gameplay/BoardSlot.prefab").GetComponent<BoardSlot>();
            var aim = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TcgEngine/Prefabs/FX/AimTarget.prefab").GetComponent<AimTargetFX>();
            string[] names = { "red_tile_inside", "neutral_tile", "blue_tile_inside" };
            for (int i = 0; i < names.Length; i++)
            {
                var baseObject = new GameObject(names[i]);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(baseObject, scene);
                baseObject.transform.position = new Vector3((i - 1) * 2.45f, 0, 0);
                baseObject.transform.localScale = Vector3.one * 0.18f;
                var tile = baseObject.AddComponent<SpriteRenderer>();
                tile.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TcgEngine/Sprites/UI/" + names[i] + ".png");
                var tileMaterial = new Material(Shader.Find("Sprites/Default"));
                tileMaterial.mainTexture = tile.sprite.texture;
                tile.sharedMaterial = tileMaterial;
                previewMaterials.Add(tileMaterial);
                tile.sortingOrder = -10;
                var overlayObject = new GameObject("Hatching");
                overlayObject.transform.SetParent(baseObject.transform, false);
                overlayObject.transform.localScale = new Vector3(0.78f, 0.78f, 1);
                var overlay = overlayObject.AddComponent<SpriteRenderer>();
                overlay.sprite = slot.overlay_renderer.sprite;
                overlay.sharedMaterial = slot.overlay_renderer.sharedMaterial;
                overlay.color = new Color(1, 0, 0, 0.15f); // Deliberately prove old tint/alpha don't bleed through.
                overlay.sortingOrder = -9;
                if (i == 1)
                {
                    var markerObject = new GameObject("Selected target");
                    markerObject.transform.SetParent(baseObject.transform, false);
                    var marker = markerObject.AddComponent<SpriteRenderer>();
                    marker.sprite = aim.target_fx.GetComponent<SpriteRenderer>().sprite;
                    var markerMaterial = new Material(Shader.Find("Sprites/Default"));
                    markerMaterial.mainTexture = marker.sprite.texture;
                    marker.sharedMaterial = markerMaterial;
                    previewMaterials.Add(markerMaterial);
                    marker.sortingOrder = -8;
                    markerObject.transform.localScale = new Vector3(
                        tile.sprite.bounds.size.x * aim.tile_frame_scale / (marker.sprite.bounds.size.x * aim.frame_fraction.x),
                        tile.sprite.bounds.size.y * aim.tile_frame_scale / (marker.sprite.bounds.size.y * aim.frame_fraction.y), 1);
                }
            }
            camera.Render();
            RenderTexture.active = rt;
            image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            image.Apply();
            string path = Path.GetFullPath("Tools/TargetingVfx/range-hatching-preview.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            return path;
        }
        finally
        {
            RenderTexture.active = previous;
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            foreach (var material in previewMaterials) UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
