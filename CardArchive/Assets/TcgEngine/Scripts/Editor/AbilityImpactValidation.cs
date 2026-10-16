using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TcgEngine.Client;
using TcgEngine.FX;
using TcgEngine.Replay;
using UnityEditor;
using UnityEngine;

namespace TcgEngine.Editor
{
    // Uses real Animator events and the real client reservation/binding path.
    public static class AbilityImpactValidation
    {
        [MenuItem("TcgEngine/FX/Validate Ability Impacts (Play Mode)")]
        public static void Validate() => Debug.Log("Ability Impact checks passed: " + Run());

        public static int Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
            int checks = 0;
            Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception(label); checks++; };
            var cleanup = new List<UnityEngine.Object>();
            var root = new GameObject("Ability Impact validation");
            cleanup.Add(root);
            var client = root.AddComponent<InputValidationClient>();
            var state = (PresentationState)Field("presentationState").GetValue(client);
            var game = new Game("ability-impact", 2);
            game.players[0].hp = game.players[1].hp = 20;
            state.Stage(ReplayStateCodec.Capture(game));
            var baseline = state.RequireAnimationEvent(); state.Seal(); baseline();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TcgEngine/Prefabs/FX/ExplosionFX.prefab");
            int publications = 0;
            state.Changed += () => publications++;
            try
            {
                game.players[0].hp = game.players[1].hp = 17;
                Stage(client, state, game);
                var owner = client.ReservePresentationAnimation(prefab, abilityTarget: true);
                var visual = client.ReservePresentationAnimation(prefab, abilityTarget: true);
                var a = Create(client, prefab, owner, cleanup);
                var b = Create(client, prefab, visual, cleanup);
                Seal(client, state, a, b);
                b.animator.Update(0.1f); // Intentionally deliver the non-owner's genuine Impact first.
                check(state.Pending && state.Displayed.players[0].hp == 20 && publications == 0,
                    "Another instance's earlier Impact cannot publish the ability");
                a.animator.Update(0.1f);
                check(state.Pending && publications == 0, "Owner's numbers wait for its authored Impact frame");
                b.animator.Update(0.3f);
                check(state.Pending, "Visual-only Impact is harmless, with no unbound-index error");
                a.animator.Update(0.3f);
                check(!state.Pending && state.Displayed.players[0].hp == 17 && state.Displayed.players[1].hp == 17
                    && publications == 1, "One real owner Impact publishes all simultaneous damage");
                a.animator.Update(0.2f); b.animator.Update(0.2f);
                check(publications == 1, "Repeated Impact(0) does not publish twice");
                a.animator.Update(0.5f);
                check(!state.CanFinishEffect, "Owner completion still waits for the visual-only instance");
                b.animator.Update(0.5f);
                check(state.CanFinishEffect, "All completed instances allow the next effect");
                state.SynchronizeEffectBoundary();

                // Each sequential target gets a new generation and its own owner.
                publications = 0;
                game.players[0].hp = 14;
                Stage(client, state, game);
                var first = Create(client, prefab, client.ReservePresentationAnimation(prefab, true), cleanup);
                Seal(client, state, first);
                first.animator.Update(0.4f);
                check(state.Displayed.players[0].hp == 14 && state.Displayed.players[1].hp == 17 && publications == 1,
                    "First sequential Impact changes only the first target");
                first.animator.Update(0.7f);
                state.SynchronizeEffectBoundary();
                game.players[1].hp = 14;
                Stage(client, state, game);
                var second = Create(client, prefab, client.ReservePresentationAnimation(prefab, true), cleanup);
                Seal(client, state, second);
                second.animator.Update(0.1f);
                check(state.Pending && state.Displayed.players[1].hp == 17, "Next target remains unchanged before its Impact");
                second.animator.Update(0.3f);
                check(!state.Pending && state.Displayed.players[0].hp == 14 && state.Displayed.players[1].hp == 14,
                    "Next sequential Impact publishes the next cumulative snapshot");
                second.animator.Update(0.7f);
            }
            finally
            {
                state.Cancel();
                foreach (var obj in cleanup) if (obj != null) UnityEngine.Object.Destroy(obj);
            }
            return checks;
        }

        private static FieldInfo Field(string name) => typeof(GameClient).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static void Stage(GameClient client, PresentationState state, Game game)
        {
            state.Stage(ReplayStateCodec.Capture(game));
            Field("abilityImpactReserved").SetValue(client, false);
            Field("collectingAnimations").SetValue(client, true);
        }
        private static void Seal(GameClient client, PresentationState state, params (FXPlayback playback, Animator animator)[] effects)
        {
            state.Seal();
            Field("collectingAnimations").SetValue(client, false);
            foreach (var effect in effects)
            {
                effect.playback.Arm();
                effect.animator.Rebind();
                effect.animator.Update(0f);
            }
        }
        private static (FXPlayback playback, Animator animator) Create(GameClient client, GameObject prefab,
            GameClient.AnimationReservation reservation, List<UnityEngine.Object> cleanup)
        {
            var fx = UnityEngine.Object.Instantiate(prefab, new Vector3(1000, 1000, 0), Quaternion.identity);
            cleanup.Add(fx);
            var animator = fx.GetComponentInChildren<Animator>();
            var controller = new AnimatorOverrideController(animator.runtimeAnimatorController);
            cleanup.Add(controller);
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); controller.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++)
            {
                var clip = UnityEngine.Object.Instantiate(pairs[i].Value != null ? pairs[i].Value : pairs[i].Key);
                cleanup.Add(clip);
                clip.events = new[] {
                    new AnimationEvent { time = 0.25f, functionName = "Impact", intParameter = 0 },
                    new AnimationEvent { time = 0.5f, functionName = "Impact", intParameter = 0 },
                    new AnimationEvent { time = 0.9f, functionName = "Complete" }
                };
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, clip);
            }
            controller.ApplyOverrides(pairs); animator.runtimeAnimatorController = controller;
            client.BindPresentationAnimation(fx, reservation);
            return (fx.GetComponentInChildren<FXPlayback>(), animator);
        }
    }
}
