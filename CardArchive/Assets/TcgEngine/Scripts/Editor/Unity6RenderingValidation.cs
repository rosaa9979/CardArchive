using System;
using System.Collections.Generic;
using System.IO;
using EPOOutline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Render real pixels through each project's quality pipeline, including EPO's Render Graph pass.
public static class Unity6RenderingValidation
{
    public static void Run()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Run this validation in a separate batch-mode editor.");

        const string output = "Logs/Unity6Warnings/rendering";
        Directory.CreateDirectory(output);
        int originalQuality = QualitySettings.GetQualityLevel();
        var messages = new List<string>();
        var results = new List<string>();
        Application.LogCallback capture = (message, stack, type) =>
        {
            if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                messages.Add(type + ": " + message);
        };
        var originalScene = EditorSceneManager.GetSceneManagerSetup();
        Material material = null;
        RenderTexture target = null;
        Texture2D pixels = null;
        Application.logMessageReceived += capture;
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Outline validation camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -4);
            camera.orthographic = true;
            camera.orthographicSize = 1.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var outliner = camera.gameObject.AddComponent<Outliner>();
            outliner.DilateIterations = 3;
            outliner.BlurIterations = 0;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", Color.white);
            var renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            var outline = cube.AddComponent<Outlinable>();
            outline.AddTarget(new OutlineTarget(renderer));
            outline.OutlineParameters.Color = Color.green;

            target = new RenderTexture(256, 256, 24);
            target.Create();
            pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            for (int quality = 0; quality < QualitySettings.names.Length; quality++)
            {
                QualitySettings.SetQualityLevel(quality, true);
                // Warm up visibility tracking and the selected pipeline before reading the image.
                for (int frame = 0; frame < 3; frame++)
                    RenderPipeline.SubmitRenderRequest(camera, request);

                var previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                    pixels.Apply();
                }
                finally { RenderTexture.active = previous; }

                int green = 0, white = 0;
                foreach (var color in pixels.GetPixels32())
                {
                    if (color.g > 100 && color.r < color.g / 2 && color.b < color.g / 2) green++;
                    if (color.r > 180 && color.g > 180 && color.b > 180) white++;
                }
                string name = QualitySettings.names[quality];
                File.WriteAllBytes(output + "/" + name + ".png", pixels.EncodeToPNG());
                results.Add(name + ": outline pixels=" + green + ", object pixels=" + white);
                if (green < 50 || white < 500)
                    throw new InvalidOperationException("Missing outline/object: " + results[results.Count - 1]);
            }
            if (messages.Count > 0)
                throw new InvalidOperationException(string.Join("\n", messages));
        }
        catch (Exception exception)
        {
            File.WriteAllText(output + "/result.txt", "FAIL: " + exception + "\n" + string.Join("\n", results) + "\n" + string.Join("\n", messages));
            throw;
        }
        finally
        {
            Application.logMessageReceived -= capture;
            QualitySettings.SetQualityLevel(originalQuality, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            if (Array.Exists(originalScene, scene => scene.isLoaded && scene.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(originalScene);
        }
        File.WriteAllLines(output + "/result.txt", new[] { "PASS: all quality levels render object and outline without warnings/errors." });
        File.AppendAllLines(output + "/result.txt", results);
        Debug.Log("Unity 6 rendering validation PASS");
    }
}
