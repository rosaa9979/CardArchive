using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using TcgEngine.FX;

namespace TcgEngine.Editor
{
    // Explicit authoring command: never rewrite an artist's event times on Editor startup.
    public static class FXPresentationAssets
    {
        private const string TimelineFolder = "Assets/TcgEngine/Animations/Presentation";

        public static string[] FindLinkedPrefabs()
        {
            var paths = new HashSet<string>();
            Collect("AbilityData", paths, "board_fx", "caster_fx", "target_fx");
            Collect("CardData", paths, "spawn_fx", "death_fx");
            Collect("WeaponData", paths, "hit_fx");
            Collect("AssetData", paths, "card_spawn_fx", "card_destroy_fx", "card_attack_hit_fx");
            foreach (string name in new[] { "HealFX", "BuffFX" })
            {
                string path = "Assets/TcgEngine/Prefabs/FX/" + name + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) paths.Add(path);
            }
            return paths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }

        private static void Collect(string type, HashSet<string> paths, params string[] fields)
        {
            // Entire Assets tree, including abilities outside Resources and custom FX folders.
            foreach (string guid in AssetDatabase.FindAssets("t:" + type, new[] { "Assets" }))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                var serialized = new SerializedObject(asset);
                foreach (string field in fields)
                {
                    var reference = serialized.FindProperty(field);
                    if (reference == null) continue;
                    if (reference.objectReferenceValue == null)
                    {
                        if (!reference.objectReferenceEntityIdValue.Equals(default(EntityId)))
                            throw new BuildFailedException(AssetDatabase.GetAssetPath(asset) + ": missing " + field);
                        continue;
                    }
                    paths.Add(AssetDatabase.GetAssetPath(reference.objectReferenceValue));
                }
            }
        }

        [MenuItem("TcgEngine/FX/Configure Linked Presentation Events")]
        public static void ConfigureLinked()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Configure presentation assets outside Play Mode.");
            foreach (string path in FindLinkedPrefabs()) ConfigurePrefab(path);
            AssetDatabase.SaveAssets();
            Validate();
        }

        public static void ConfigurePrefab(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var setting = root.GetComponentInChildren<FXSetting>(true);
                var animator = setting != null && setting.animator != null
                    ? setting.animator : root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    // Keep particle visuals unchanged; each prefab owns an editable event timeline.
                    float duration = FXTool.GetDuration(root);
                    EnsureFolder(TimelineFolder);
                    string stem = TimelineFolder + "/" + root.name + "_" + AssetDatabase.AssetPathToGUID(path).Substring(0, 8);
                    string clipPath = stem + ".anim";
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (clip == null)
                    {
                        clip = new AnimationClip { name = root.name + "Presentation" };
                        clip.SetCurve("", typeof(FXPlayback), "cueProgress", AnimationCurve.Linear(0f, 0f, Mathf.Max(0.01f, duration), 1f));
                        AssetDatabase.CreateAsset(clip, clipPath);
                    }
                    var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(stem + ".controller");
                    if (controller == null)
                    {
                        controller = AnimatorController.CreateAnimatorControllerAtPath(stem + ".controller");
                        controller.AddMotion(clip);
                    }
                    animator = root.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                }
                if (animator.runtimeAnimatorController == null || animator.runtimeAnimatorController.animationClips.Length == 0)
                    throw new BuildFailedException(path + ": Animator has no clips.");
                if (animator.GetComponent<FXPlayback>() == null) animator.gameObject.AddComponent<FXPlayback>();
                animator.enabled = true;
                animator.fireEvents = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (setting != null) setting.animator = animator;
                foreach (var receiver in root.GetComponentsInChildren<AnimReceiverDestroy>(true))
                    UnityEngine.Object.DestroyImmediate(receiver);
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips.Distinct())
                    ConfigureClip(clip, setting == null || setting.presentation_impact);
                if (animator.runtimeAnimatorController is AnimatorController authoredController)
                {
                    foreach (var layer in authoredController.layers) RemoveLegacyExits(layer.stateMachine);
                    EditorUtility.SetDirty(authoredController);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void RemoveLegacyExits(AnimatorStateMachine machine)
        {
            // Complete owns one-shot lifetime. Legacy exit transitions can stop sampling
            // before the last frame (BuffFX exited at 75%), losing the completion event.
            foreach (var child in machine.states)
                foreach (var transition in child.state.transitions.Where(t => t.isExit).ToArray())
                {
                    child.state.RemoveTransition(transition);
                    UnityEngine.Object.DestroyImmediate(transition, true);
                }
            foreach (var child in machine.stateMachines) RemoveLegacyExits(child.stateMachine);
        }

        private static bool HasLegacyExit(AnimatorStateMachine machine)
        {
            return machine.states.Any(child => child.state.transitions.Any(t => t.isExit))
                || machine.stateMachines.Any(child => HasLegacyExit(child.stateMachine));
        }

        private static void ConfigureClip(AnimationClip clip, bool appliesState)
        {
            if (clip.isLooping) throw new BuildFailedException(AssetDatabase.GetAssetPath(clip) + ": a state cue cannot loop.");
            var events = AnimationUtility.GetAnimationEvents(clip).ToList();
            // Retain the former visual lifetime, and preserve any already authored Impact/Complete.
            float end = events.Where(e => e.functionName == "OnAnimationFinished")
                .Select(e => e.time).DefaultIfEmpty(clip.length).Min();
            events.RemoveAll(e => e.functionName == "OnAnimationFinished");
            if (!appliesState) events.RemoveAll(e => e.functionName == "Impact");
            if (appliesState && !events.Any(e => e.functionName == "Impact" && e.intParameter == 0))
                events.Add(new AnimationEvent { time = 0f, functionName = "Impact", intParameter = 0 });
            if (!events.Any(e => e.functionName == "Complete"))
                events.Add(new AnimationEvent { time = Mathf.Max(end, events.Where(e => e.functionName == "Impact").Select(e => e.time).DefaultIfEmpty(0f).Max()), functionName = "Complete" });
            AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time)
                .ThenBy(e => e.functionName == "Complete" ? 2 : e.functionName == "Continue" ? 1 : 0).ToArray());
            EditorUtility.SetDirty(clip);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        [MenuItem("TcgEngine/FX/Validate Animation Events")]
        public static void Validate()
        {
            string[] paths = FindLinkedPrefabs();
            foreach (string path in paths) ValidatePrefab(path);
            Debug.Log("Validated presentation animation events on " + paths.Length + " linked FX prefabs.");
        }

        public static void ValidatePrefab(string path)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) throw new BuildFailedException(path + ": missing FX prefab.");
            var setting = root.GetComponentInChildren<FXSetting>(true);
            bool appliesState = setting == null || setting.presentation_impact;
            var animator = setting != null && setting.animator != null ? setting.animator : root.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null || !animator.enabled || !animator.fireEvents
                || animator.GetComponent<FXPlayback>() == null)
                throw new BuildFailedException(path + ": requires an enabled Animator and FXPlayback on the same object.");
            if (root.GetComponentInChildren<AnimReceiverDestroy>(true) != null)
                throw new BuildFailedException(path + ": legacy destruction can preempt Complete.");
            if (animator.runtimeAnimatorController is AnimatorController controller
                && controller.layers.Any(layer => HasLegacyExit(layer.stateMachine)))
                throw new BuildFailedException(path + ": use Complete for lifetime; an Exit transition can skip its event.");
            var clips = animator.runtimeAnimatorController.animationClips;
            if (clips.Length == 0) throw new BuildFailedException(path + ": no animation clips.");
            foreach (AnimationClip clip in clips)
            {
                var events = AnimationUtility.GetAnimationEvents(clip);
                var impacts = events.Where(e => e.functionName == "Impact").ToArray();
                var finishes = events.Where(e => e.functionName == "Complete").ToArray();
                var continuations = events.Where(e => e.functionName == "Continue").ToArray();
                if (clip.isLooping || (appliesState ? !impacts.Any(e => e.intParameter == 0) : impacts.Length != 0) || finishes.Length != 1
                    || impacts.Any(e => e.time > finishes[0].time || e.time < 0f)
                    || finishes[0].time > clip.length || events.Any(e => e.functionName == "OnAnimationFinished"))
                    throw new BuildFailedException(path + "/" + clip.name + ": require a non-looping clip, one Complete, and Impact only for state-bearing FX.");
                if (continuations.Length > 1 || continuations.Any(e => e.time < 0f || e.time > finishes[0].time))
                    throw new BuildFailedException(path + "/" + clip.name + ": optional Continue must occur once, before or at Complete.");
            }
        }
    }

    public sealed class FXPlaybackBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) { FXPresentationAssets.Validate(); }
    }
}
