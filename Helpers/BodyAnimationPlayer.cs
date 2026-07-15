using System;
using System.Collections.Generic;
using System.Linq;
using CUCoreLib.Data;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CUCoreLib.Helpers
{
    public static class BodyAnimationPlayer
    {
        private sealed class PlaybackState
        {
            public Body Body;
            public PlayableGraph Graph;
            public AnimationClipPlayable BodyPlayable;
            public AnimationClipPlayable ArmsPlayable;
            public bool Loop;
            public float Speed;
            public float Duration;
            public float StartedAt;
        }

        private static readonly Dictionary<int, PlaybackState> PlaybackByBodyId =
            new Dictionary<int, PlaybackState>();

        private static BodyAnimationPlayerRuntime runtime;

        public static bool PlayBundled(Body body, string bundleId, string animationId)
        {
            if (!TryResolveBundledEntry(bundleId, animationId, out var entry, out var bodyClip, out var armsClip))
            {
                return false;
            }

            return Play(body, entry, bodyClip, armsClip, entry.Loop, entry.Speed);
        }

        public static bool PlayBundled(Body body, string bundleId, string animationId, bool loop, float speed = 1f)
        {
            if (!TryResolveBundledEntry(bundleId, animationId, out var entry, out var bodyClip, out var armsClip))
            {
                return false;
            }

            return Play(body, entry, bodyClip, armsClip, loop, speed);
        }

        public static void Stop(Body body)
        {
            ResetToVanilla(body);
        }

        public static void ResetToVanilla(Body body)
        {
            if (body == null)
            {
                return;
            }

            int bodyId = body.GetInstanceID();
            if (!PlaybackByBodyId.TryGetValue(bodyId, out var state) || state == null)
            {
                return;
            }

            DestroyPlayback(state);
            PlaybackByBodyId.Remove(bodyId);

            if (body.bodyAnimator != null)
            {
                body.bodyAnimator.Rebind();
                body.bodyAnimator.Update(0f);
            }

            if (body.armsAnimator != null)
            {
                body.armsAnimator.Rebind();
                body.armsAnimator.Update(0f);
            }
        }

        internal static void Update()
        {
            if (PlaybackByBodyId.Count == 0)
            {
                return;
            }

            var completedBodies = new List<int>();
            foreach (var pair in PlaybackByBodyId)
            {
                var state = pair.Value;
                if (state == null || state.Body == null || state.Body.bodyAnimator == null || state.Body.armsAnimator == null)
                {
                    completedBodies.Add(pair.Key);
                    continue;
                }

                if (state.Duration <= 0.001f || state.Speed <= 0.0001f)
                {
                    continue;
                }

                float elapsed = Time.time - state.StartedAt;
                if (state.Loop)
                {
                    if (elapsed >= state.Duration)
                    {
                        state.BodyPlayable.SetTime(0d);
                        state.ArmsPlayable.SetTime(0d);
                        state.StartedAt = Time.time;
                    }
                }
                else if (elapsed >= state.Duration)
                {
                    completedBodies.Add(pair.Key);
                }
            }

            foreach (int bodyId in completedBodies)
            {
                if (PlaybackByBodyId.TryGetValue(bodyId, out var state) && state != null)
                {
                    ResetToVanilla(state.Body);
                }
            }
        }

        private static bool TryResolveBundledEntry(string bundleId, string animationId, out BodyAnimationPackEntry entry,
            out AnimationClip bodyClip, out AnimationClip armsClip)
        {
            entry = null;
            bodyClip = null;
            armsClip = null;

            if (string.IsNullOrWhiteSpace(bundleId) || string.IsNullOrWhiteSpace(animationId))
            {
                LogWarning("Could not play custom body animation because bundleId or animationId was empty.");
                return false;
            }

            string assetName = NormalizePackAssetName(bundleId);
            if (!AssetLoader.TryLoadBundleAsset(bundleId, assetName, out TextAsset manifestAsset) || manifestAsset == null ||
                string.IsNullOrWhiteSpace(manifestAsset.text))
            {
                LogWarning("Could not load bundled body animation manifest '" + assetName + "' from bundle '" + bundleId + "'.");
                return false;
            }

            BodyAnimationPackManifest manifest;
            try
            {
                manifest = JsonConvert.DeserializeObject<BodyAnimationPackManifest>(manifestAsset.text);
            }
            catch (Exception ex)
            {
                LogWarning("Could not parse bundled body animation manifest '" + assetName + "': " + ex.Message);
                return false;
            }

            if (manifest == null || manifest.Animations == null || manifest.Animations.Length == 0)
            {
                LogWarning("Bundled body animation manifest '" + assetName + "' did not contain any animation entries.");
                return false;
            }

            entry = manifest.Animations.FirstOrDefault(candidate =>
                candidate != null &&
                string.Equals(candidate.AnimationId ?? string.Empty, animationId.Trim(), StringComparison.OrdinalIgnoreCase));

            if (entry == null)
            {
                LogWarning("Bundled body animation '" + animationId + "' was not found in bundle '" + bundleId + "'.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(entry.BodyClipAssetName) || string.IsNullOrWhiteSpace(entry.ArmsClipAssetName))
            {
                LogWarning("Bundled body animation '" + animationId + "' is missing clip asset names.");
                return false;
            }

            if (!AssetLoader.TryLoadBundleAsset(bundleId, entry.BodyClipAssetName.Trim(), out bodyClip) || bodyClip == null)
            {
                LogWarning("Could not load body clip '" + entry.BodyClipAssetName + "' for custom animation '" + animationId + "'.");
                return false;
            }

            if (!AssetLoader.TryLoadBundleAsset(bundleId, entry.ArmsClipAssetName.Trim(), out armsClip) || armsClip == null)
            {
                LogWarning("Could not load arms clip '" + entry.ArmsClipAssetName + "' for custom animation '" + animationId + "'.");
                return false;
            }

            return true;
        }

        private static bool Play(Body body, BodyAnimationPackEntry entry, AnimationClip bodyClip, AnimationClip armsClip, bool loop,
            float speed)
        {
            if (body == null || body.bodyAnimator == null || body.armsAnimator == null || bodyClip == null || armsClip == null)
            {
                LogWarning("Could not play custom body animation because the body seam was incomplete.");
                return false;
            }

            EnsureRuntime();
            ResetToVanilla(body);

            float normalizedSpeed = Mathf.Max(0.01f, speed <= 0f ? entry?.Speed ?? 1f : speed);
            var graph = PlayableGraph.Create("CUCoreLib.CustomBodyAnimation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            var bodyPlayable = AnimationClipPlayable.Create(graph, bodyClip);
            bodyPlayable.SetDuration(bodyClip.length);
            bodyPlayable.SetTime(0d);
            bodyPlayable.SetSpeed(normalizedSpeed);

            var armsPlayable = AnimationClipPlayable.Create(graph, armsClip);
            armsPlayable.SetDuration(armsClip.length);
            armsPlayable.SetTime(0d);
            armsPlayable.SetSpeed(normalizedSpeed);

            var bodyOutput = AnimationPlayableOutput.Create(graph, "Body", body.bodyAnimator);
            bodyOutput.SetSourcePlayable(bodyPlayable);

            var armsOutput = AnimationPlayableOutput.Create(graph, "Arms", body.armsAnimator);
            armsOutput.SetSourcePlayable(armsPlayable);

            graph.Play();

            PlaybackByBodyId[body.GetInstanceID()] = new PlaybackState
            {
                Body = body,
                Graph = graph,
                BodyPlayable = bodyPlayable,
                ArmsPlayable = armsPlayable,
                Loop = loop,
                Speed = normalizedSpeed,
                Duration = Mathf.Max(bodyClip.length, armsClip.length) / normalizedSpeed,
                StartedAt = Time.time
            };

            return true;
        }

        private static void EnsureRuntime()
        {
            if (runtime != null)
            {
                return;
            }

            var runtimeObject = new GameObject("CUCoreLib BodyAnimationPlayer Runtime");
            UnityEngine.Object.DontDestroyOnLoad(runtimeObject);
            runtimeObject.hideFlags = HideFlags.HideAndDontSave;
            runtime = runtimeObject.AddComponent<BodyAnimationPlayerRuntime>();
        }

        private static void DestroyPlayback(PlaybackState state)
        {
            if (state == null)
            {
                return;
            }

            if (state.Graph.IsValid())
            {
                state.Graph.Destroy();
            }
        }

        private static string NormalizePackAssetName(string bundleId)
        {
            string trimmed = (bundleId ?? string.Empty).Trim();
            int lastDot = trimmed.LastIndexOf('.');
            string stem = lastDot >= 0 ? trimmed.Substring(0, lastDot) : trimmed;
            return stem + "AnimationPack";
        }

        private static void LogWarning(string message)
        {
            if (CUCoreLibPlugin.Log != null)
            {
                CUCoreLibPlugin.Log.LogWarning(message);
            }
        }

        private sealed class BodyAnimationPlayerRuntime : MonoBehaviour
        {
            private void Update()
            {
                BodyAnimationPlayer.Update();
            }
        }
    }
}
