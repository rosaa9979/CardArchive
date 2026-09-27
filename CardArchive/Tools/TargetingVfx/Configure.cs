using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using TcgEngine.Client;
using TcgEngine.FX;

// Run through Unity Pipeline run_script; kept outside Assets to avoid editor assembly changes.
public static class ConfigureTargetingVfx
{
    public static string Main()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run while the Editor is stopped.");

        const string source = "Assets/TcgEngine/Sprites/FX/target_hex_red_v3.png";
        const string output = "Assets/TcgEngine/Sprites/FX/target_hex_red_snap.png";
        const string prefabPath = "Assets/TcgEngine/Prefabs/FX/AimTarget.prefab";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(output) == null && !AssetDatabase.CopyAsset(source, output))
            throw new InvalidOperationException("Unable to copy target sprite.");
        var importer = (TextureImporter)AssetImporter.GetAtPath(output);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spritePivot = new Vector2(0.5f, 0.5f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(output);
        if (sprite == null) throw new InvalidOperationException("Single sprite import failed.");
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var fx = root.GetComponent<AimTargetFX>();
            var renderer = fx.target_fx.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = -8;
            fx.snap_duration = 0.14f;
            fx.snap_start_scale = 1.16f;
            fx.tile_frame_scale = 1.03f;
            fx.frame_fraction = new Vector2(0.77f, 0.86f);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return Validate();
    }

    public static string ApplySorting()
    {
        const string path = "Assets/TcgEngine/Prefabs/FX/AimTarget.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var renderer = root.GetComponent<AimTargetFX>().target_fx.GetComponent<SpriteRenderer>();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = -8;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return Validate();
    }

    public static string Validate()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/TcgEngine/Prefabs/FX/AimTarget.prefab");
        var tileRoot = PrefabUtility.LoadPrefabContents("Assets/TcgEngine/Prefabs/Gameplay/BoardSlot.prefab");
        GameObject second = null;
        try
        {
            var fx = root.GetComponent<AimTargetFX>();
            var marker = fx.target_fx.GetComponent<SpriteRenderer>();
            Check(marker.sprite.name == "target_hex_red_snap", "Sprite reference");
            Check(marker.color == Color.white, "Preserve original red and cream colors");
            var slot = tileRoot.GetComponent<BoardSlot>();
            tileRoot.transform.position = new Vector3(2, 3, 0);
            var method = typeof(AimTargetFX).GetMethod("UpdateTargetVisual", BindingFlags.NonPublic | BindingFlags.Instance);
            Action<BSlot, bool, Vector3, float> show = (s, v, p, dt) => method.Invoke(fx, new object[] { s, v, p, dt });
            show(null, false, Vector3.zero, 0);
            show(slot, true, new Vector3(2.3f, 3.2f, 0), 0);
            Vector3 expanded = fx.target_fx.transform.localScale;
            Check(fx.target_fx.transform.position == slot.transform.position, "Snap to tile center");
            show(slot, true, new Vector3(2.4f, 3.1f, 0), fx.snap_duration);
            Vector3 settled = fx.target_fx.transform.localScale;
            Check((expanded - settled * fx.snap_start_scale).sqrMagnitude < 0.000001f, "Snap contraction");
            show(slot, true, Vector3.zero, 0.02f);
            Check(fx.target_fx.transform.localScale == settled, "No retrigger inside same tile");
            Vector3 snapped;
            Check(fx.TryGetSnapPosition(out snapped) && snapped == slot.transform.position, "Line endpoint");
            var tileRenderer = slot.GetComponent<SpriteRenderer>();
            Check(marker.sortingLayerID == tileRenderer.sortingLayerID && marker.sortingOrder == tileRenderer.sortingOrder + 2, "Frame above hatching, below card");
            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TcgEngine/Prefabs/Gameplay/BoardCard.prefab");
            Check(marker.sortingOrder < cardPrefab.GetComponent<BoardCard>().card_sprite.sortingOrder, "Card art occludes frame");
            tileRenderer.sortingLayerName = "UI";
            show(slot, true, Vector3.zero, 0.02f);
            Check(marker.sortingLayerID == tileRenderer.sortingLayerID, "Follow highlighted tile layer");
            tileRenderer.sortingLayerName = "Default";
            show(slot, true, Vector3.zero, 0.02f);
            Check(marker.sortingLayerID == tileRenderer.sortingLayerID, "Restore normal tile layer");
            float width = marker.sprite.bounds.size.x * marker.transform.lossyScale.x * fx.frame_fraction.x;
            Check(Mathf.Abs(width - tileRenderer.sprite.bounds.size.x * slot.transform.lossyScale.x * fx.tile_frame_scale) < 0.001f, "Tile frame sizing");

            second = UnityEngine.Object.Instantiate(tileRoot);
            second.transform.position = new Vector3(4, 3, 0);
            show(second.GetComponent<BoardSlot>(), true, Vector3.zero, 0.02f);
            Check((fx.target_fx.transform.localScale - expanded).sqrMagnitude < 0.000001f, "Restart on tile change");
            Check(fx.target_fx.transform.position == second.transform.position, "Follow new tile");
            show(null, false, Vector3.zero, 0);
            Check(!fx.target_fx.activeSelf && !fx.TryGetSnapPosition(out snapped), "Clear invalid target");
            show(slot, true, Vector3.zero, 0);
            Check((fx.target_fx.transform.localScale - expanded).sqrMagnitude < 0.000001f, "Restart after reacquisition");
            root.SetActive(false);
            // Prefab contents aren't running MonoBehaviour callbacks in edit mode.
            typeof(AimTargetFX).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(fx, null);
            Check(!fx.target_fx.activeSelf && !fx.text_fx.activeSelf, "Disable clears presentation");
            return "PASS: sprite, tint, tile centering, contraction, stable hover, line endpoint, sizing, tile switching, invalid target, reacquisition, disable cleanup.";
        }
        finally
        {
            if (second != null) UnityEngine.Object.DestroyImmediate(second);
            PrefabUtility.UnloadPrefabContents(tileRoot);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Targeting VFX validation failed: " + name);
    }
}
