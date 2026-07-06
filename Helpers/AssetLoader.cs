using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CUCoreLib.ContentReload;
using CUCoreLib.Data;
using NAudio.Wave;
using UnityEngine;
using UnityEngine.UI;

namespace CUCoreLib.Helpers
{
    public static class AssetLoader
    {
        public const float PPU_WORLD = 8f;
        public const float PPU_UI = 100f;
        private const float EmbeddedSpritePreloadFrameBudgetSeconds = 0.004f;

        private static ManualLogSource Logger;

        internal static Dictionary<string, Sprite> SpriteCache =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Sprite> SpriteVariantCache =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        internal static Dictionary<string, AudioClip> AudioClipCache =
            new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

        internal static Dictionary<string, RegisteredSpriteAnimation> SpriteAnimationCache =
            new Dictionary<string, RegisteredSpriteAnimation>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string[]> ResourceNameCache =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Texture2D> EmbeddedTextureCache =
            new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Sprite> EmbeddedSpriteVariantCache =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, HashSet<string>> AssemblyTextureKeys =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, HashSet<string>> AssemblySpriteVariantKeys =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> LoggedMissingResources =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> SupportedAudioExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".wav",
                ".mp1",
                ".mp2",
                ".mp3",
                ".cue",
                ".aif",
                ".aiff"
            };

        private static readonly HashSet<string> SupportedImageExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png",
                ".jpg",
                ".jpeg"
            };

        private static readonly Dictionary<string, RegisteredAssetBundle> RegisteredBundles =
            new Dictionary<string, RegisteredAssetBundle>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, UnityEngine.Object> BundleAssetCache =
            new Dictionary<string, UnityEngine.Object>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, AnimationCurve> BundleAnimationCurveCache =
            new Dictionary<string, AnimationCurve>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, HashSet<string>> BundleAssetCacheKeysByBundleId =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, HashSet<string>> BundleCurveCacheKeysByBundleId =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, HashSet<string>> AssemblyBundleIds =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, HashSet<string>> ModGuidBundleIds =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Queue<EmbeddedSpritePreloadEntry> EmbeddedSpritePreloadQueue =
            new Queue<EmbeddedSpritePreloadEntry>();

        private static readonly HashSet<string> QueuedEmbeddedTextureKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static Coroutine embeddedSpritePreloadCoroutine;
        private static bool embeddedSpritePreloadDiscovered;
        private static bool embeddedSpritePreloadCompleted;

        public static void Initialize(ManualLogSource logger)
        {
            if (Logger != null) return;

            Logger = logger;
        }

        public static void CacheSprite(string id, Sprite sprite)
        {
            if (string.IsNullOrWhiteSpace(id) || sprite == null) return;
            var normalizedId = NormalizeCacheKey(id);
            if (string.IsNullOrEmpty(normalizedId)) return;

            SpriteCache[normalizedId] = sprite;
            ClearCachedSpriteVariants(normalizedId);
        }

        public static Sprite GetCachedSprite(string id)
        {
            var normalizedId = NormalizeCacheKey(id);
            if (string.IsNullOrEmpty(normalizedId)) return null;

            return SpriteCache.TryGetValue(normalizedId, out var sprite) ? sprite : null;
        }

        public static Sprite GetCachedSprite(string id, float pixelsPerUnit)
        {
            var sprite = GetCachedSprite(id);
            if (sprite == null) return null;

            if (pixelsPerUnit <= 0f || Mathf.Approximately(sprite.pixelsPerUnit, pixelsPerUnit)) return sprite;

            var normalizedId = NormalizeCacheKey(id);
            if (string.IsNullOrEmpty(normalizedId))
                return CreateSpriteVariant(sprite, pixelsPerUnit);

            var variantCacheKey = BuildSpriteVariantCacheKey(normalizedId, pixelsPerUnit);
            if (SpriteVariantCache.TryGetValue(variantCacheKey, out var cachedVariant) && cachedVariant != null)
                return cachedVariant;

            var spriteVariant = CreateSpriteVariant(sprite, pixelsPerUnit);
            if (spriteVariant == null) return null;

            SpriteVariantCache[variantCacheKey] = spriteVariant;
            return spriteVariant;
        }

        public static void CacheAudioClip(string id, AudioClip clip)
        {
            if (string.IsNullOrWhiteSpace(id) || clip == null) return;
            var normalizedId = NormalizeCacheKey(id);
            if (string.IsNullOrEmpty(normalizedId)) return;

            AudioClipCache[normalizedId] = clip;
        }

        public static AudioClip GetCachedAudioClip(string id)
        {
            var normalizedId = NormalizeCacheKey(id);
            if (string.IsNullOrEmpty(normalizedId)) return null;

            return AudioClipCache.TryGetValue(normalizedId, out var clip) ? clip : null;
        }

        public static void CacheSpriteAnimation(string id, RegisteredSpriteAnimation animation)
        {
            if (string.IsNullOrWhiteSpace(id) || animation == null || animation.Frames == null ||
                animation.Frames.Length == 0) return;

            var normalizedId = NormalizeCacheKey(id);
            if (string.IsNullOrEmpty(normalizedId)) return;

            animation.Id = normalizedId;
            SpriteAnimationCache[normalizedId] = animation;
            CacheSprite(normalizedId, animation.Frames[0]);
        }

        public static RegisteredSpriteAnimation GetCachedSpriteAnimation(string id)
        {
            var normalizedId = NormalizeCacheKey(id);
            if (string.IsNullOrEmpty(normalizedId)) return null;

            return SpriteAnimationCache.TryGetValue(normalizedId, out var animation) ? animation : null;
        }

        public static Sprite LoadEmbeddedSprite(string resourcePath, float pixelsPerUnit = PPU_WORLD,
            Assembly sourceAssembly = null)
        {
            if (sourceAssembly == null)
                sourceAssembly = ContentReloadSession.GetSourceAssemblyOverride() ?? Assembly.GetCallingAssembly();

            return LoadSpriteInternal(resourcePath, pixelsPerUnit, sourceAssembly);
        }

        public static Sprite LoadUISprite(string resourcePath, Assembly sourceAssembly = null)
        {
            if (sourceAssembly == null)
                sourceAssembly = ContentReloadSession.GetSourceAssemblyOverride() ?? Assembly.GetCallingAssembly();

            return LoadSpriteInternal(resourcePath, PPU_UI, sourceAssembly);
        }

        internal static Texture2D LoadEmbeddedTexture(string resourcePath, Assembly sourceAssembly = null)
        {
            if (sourceAssembly == null)
                sourceAssembly = ContentReloadSession.GetSourceAssemblyOverride() ?? Assembly.GetCallingAssembly();

            if (sourceAssembly == null) return null;

            var resolvedResourceName = FindEmbeddedResourceName(resourcePath, sourceAssembly);
            if (string.IsNullOrEmpty(resolvedResourceName))
            {
                LogMissingEmbeddedResource(resourcePath, sourceAssembly, "sprite");
                return null;
            }

            return LoadEmbeddedTextureByResolvedResourceName(sourceAssembly, resolvedResourceName);
        }

        internal static Sprite CreateSpriteFromEmbeddedTexture(string resourcePath, Assembly sourceAssembly, float ppu,
            FilterMode filterMode, int widthMultiplier, int heightMultiplier)
        {
            var texture = LoadEmbeddedTexture(resourcePath, sourceAssembly);
            if (texture == null) return null;

            var spriteName = FindEmbeddedResourceName(resourcePath, sourceAssembly);
            return CreateSpriteFromTexture(texture, ppu, filterMode, widthMultiplier, heightMultiplier, spriteName,
                false);
        }

        internal static void BeginMainMenuEmbeddedSpritePreload()
        {
            if (embeddedSpritePreloadCompleted || embeddedSpritePreloadCoroutine != null) return;

            if (!embeddedSpritePreloadDiscovered)
            {
                DiscoverEmbeddedSpritePreloadQueue();
                embeddedSpritePreloadDiscovered = true;

                if (EmbeddedSpritePreloadQueue.Count == 0)
                {
                    embeddedSpritePreloadCompleted = true;
                    return;
                }
            }

            if (EmbeddedSpritePreloadQueue.Count == 0)
            {
                embeddedSpritePreloadCompleted = true;
                return;
            }

            embeddedSpritePreloadCoroutine = CUCoreUtils.StartCoroutine(PreloadEmbeddedSpritesInMainMenu());
        }

        internal static void InvalidateEmbeddedCachesForModGuid(string modGuid)
        {
            if (string.IsNullOrWhiteSpace(modGuid)) return;

            if (Chainloader.PluginInfos.TryGetValue(modGuid, out var pluginInfo))
            {
                var assembly = pluginInfo?.Instance != null ? pluginInfo.Instance.GetType().Assembly : null;
                InvalidateEmbeddedCachesForAssembly(assembly);
            }

            ResetEmbeddedSpritePreloadState();
        }

        internal static void InvalidateBundlesForModGuid(string modGuid, bool unregister = false)
        {
            if (string.IsNullOrWhiteSpace(modGuid)) return;

            if (!ModGuidBundleIds.TryGetValue(modGuid, out var bundleIds) || bundleIds == null || bundleIds.Count == 0)
                return;

            foreach (var bundleId in bundleIds.ToArray())
            {
                if (unregister)
                    UnregisterBundle(bundleId);
                else
                    InvalidateBundle(bundleId);
            }

            if (unregister) ModGuidBundleIds.Remove(modGuid);
        }

        internal static void InvalidateEmbeddedCachesForAssembly(Assembly assembly)
        {
            if (assembly == null) return;

            var assemblyKey = GetAssemblyCacheKey(assembly);
            if (string.IsNullOrWhiteSpace(assemblyKey)) return;

            if (AssemblySpriteVariantKeys.TryGetValue(assemblyKey, out var spriteVariantKeys))
            {
                foreach (var spriteKey in spriteVariantKeys.ToArray())
                {
                    if (!EmbeddedSpriteVariantCache.TryGetValue(spriteKey, out var sprite) || sprite == null) continue;

                    EmbeddedSpriteVariantCache.Remove(spriteKey);
                    UnityEngine.Object.Destroy(sprite);
                }

                AssemblySpriteVariantKeys.Remove(assemblyKey);
            }

            if (AssemblyTextureKeys.TryGetValue(assemblyKey, out var textureKeys))
            {
                foreach (var textureKey in textureKeys.ToArray())
                {
                    if (!EmbeddedTextureCache.TryGetValue(textureKey, out var texture) || texture == null) continue;

                    EmbeddedTextureCache.Remove(textureKey);
                    UnityEngine.Object.Destroy(texture);
                }

                AssemblyTextureKeys.Remove(assemblyKey);
            }

            ResourceNameCache.Remove(assemblyKey);
            LoggedMissingResources.RemoveWhere(key => key.IndexOf(assemblyKey, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static void InvalidateBundlesForAssembly(Assembly assembly, bool unregister = false)
        {
            var assemblyKey = GetAssemblyCacheKey(assembly);
            if (string.IsNullOrWhiteSpace(assemblyKey)) return;

            if (!AssemblyBundleIds.TryGetValue(assemblyKey, out var bundleIds) || bundleIds == null || bundleIds.Count == 0)
                return;

            foreach (var bundleId in bundleIds.ToArray())
            {
                if (unregister)
                    UnregisterBundle(bundleId);
                else
                    InvalidateBundle(bundleId);
            }

            if (unregister) AssemblyBundleIds.Remove(assemblyKey);
        }

        public static bool RegisterBundle(string bundleId, AssetBundle bundle, Assembly sourceAssembly = null)
        {
            var normalizedBundleId = NormalizeCacheKey(bundleId);
            if (string.IsNullOrEmpty(normalizedBundleId) || bundle == null) return false;

            UnregisterBundle(normalizedBundleId);

            if (sourceAssembly == null)
                sourceAssembly = ContentReloadSession.GetSourceAssemblyOverride() ?? Assembly.GetCallingAssembly();

            RegisterBundleDefinition(new RegisteredAssetBundle
            {
                BundleId = normalizedBundleId,
                RegistrationKind = AssetBundleRegistrationKind.RuntimeInstance,
                LoadedBundle = bundle,
                SourceAssembly = sourceAssembly,
                OwnerAssemblyKey = GetAssemblyCacheKey(sourceAssembly),
                OwnerModGuid = ResolveOwnerModGuid(sourceAssembly),
                Descriptor = bundle.name
            });

            return true;
        }

        public static bool RegisterBundleFromFile(string bundleId, string filePath)
        {
            var normalizedBundleId = NormalizeCacheKey(bundleId);
            if (string.IsNullOrEmpty(normalizedBundleId) || string.IsNullOrWhiteSpace(filePath)) return false;

            var fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath))
            {
                LogMissingFileResource(fullPath, "asset bundle");
                return false;
            }

            UnregisterBundle(normalizedBundleId);
            RegisterBundleDefinition(new RegisteredAssetBundle
            {
                BundleId = normalizedBundleId,
                RegistrationKind = AssetBundleRegistrationKind.LooseFile,
                FilePath = fullPath,
                OwnerModGuid = ContentReloadSession.ResolveAmbientOwnerId(),
                Descriptor = fullPath
            });
            return true;
        }

        public static bool RegisterBundleFromPluginFolder(BaseUnityPlugin plugin, string bundleId, string relativePath)
        {
            var fullPath = GetPluginFolderPath(plugin, relativePath);
            return !string.IsNullOrEmpty(fullPath) && RegisterBundleFromFile(bundleId, fullPath);
        }

        public static bool RegisterEmbeddedBundle(string bundleId, string resourcePath, Assembly sourceAssembly = null)
        {
            var normalizedBundleId = NormalizeCacheKey(bundleId);
            if (string.IsNullOrEmpty(normalizedBundleId) || string.IsNullOrWhiteSpace(resourcePath)) return false;

            if (sourceAssembly == null)
                sourceAssembly = ContentReloadSession.GetSourceAssemblyOverride() ?? Assembly.GetCallingAssembly();

            if (sourceAssembly == null) return false;

            var resolvedResourceName = FindEmbeddedResourceName(resourcePath, sourceAssembly);
            if (string.IsNullOrEmpty(resolvedResourceName))
            {
                LogMissingEmbeddedResource(resourcePath, sourceAssembly, "asset bundle");
                return false;
            }

            UnregisterBundle(normalizedBundleId);
            RegisterBundleDefinition(new RegisteredAssetBundle
            {
                BundleId = normalizedBundleId,
                RegistrationKind = AssetBundleRegistrationKind.EmbeddedResource,
                ResourcePath = resolvedResourceName,
                SourceAssembly = sourceAssembly,
                OwnerAssemblyKey = GetAssemblyCacheKey(sourceAssembly),
                OwnerModGuid = ResolveOwnerModGuid(sourceAssembly),
                Descriptor = resolvedResourceName
            });
            return true;
        }

        public static bool InvalidateBundle(string bundleId)
        {
            var normalizedBundleId = NormalizeCacheKey(bundleId);
            if (string.IsNullOrEmpty(normalizedBundleId)) return false;

            var removedAnything = false;
            if (RegisteredBundles.TryGetValue(normalizedBundleId, out var registration) && registration != null)
            {
                removedAnything = true;

                if (registration.LoadedBundle != null)
                {
                    registration.LoadedBundle.Unload(false);
                    registration.LoadedBundle = null;
                }
            }

            removedAnything |= ClearBundleAssetCaches(normalizedBundleId);
            LoggedMissingResources.RemoveWhere(key => key.IndexOf("bundle-asset:" + normalizedBundleId + ":",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                   key.IndexOf("bundle-curve:" + normalizedBundleId + ":",
                                                       StringComparison.OrdinalIgnoreCase) >= 0);
            return removedAnything;
        }

        public static bool UnregisterBundle(string bundleId)
        {
            var normalizedBundleId = NormalizeCacheKey(bundleId);
            if (string.IsNullOrEmpty(normalizedBundleId)) return false;

            var removedAnything = InvalidateBundle(normalizedBundleId);
            if (RegisteredBundles.TryGetValue(normalizedBundleId, out var registration) && registration != null)
            {
                RegisteredBundles.Remove(normalizedBundleId);
                RemoveBundleOwnership(registration);
                removedAnything = true;
            }

            return removedAnything;
        }

        public static void InvalidateAllBundles()
        {
            foreach (var bundleId in RegisteredBundles.Keys.ToArray()) InvalidateBundle(bundleId);
        }

        public static bool TryLoadBundleAsset<T>(string bundleId, string assetName, out T asset)
            where T : UnityEngine.Object
        {
            asset = null;

            var normalizedBundleId = NormalizeCacheKey(bundleId);
            var normalizedAssetName = NormalizeCacheKey(assetName);
            if (string.IsNullOrEmpty(normalizedBundleId) || string.IsNullOrEmpty(normalizedAssetName)) return false;

            var cacheKey = BuildBundleAssetCacheKey(normalizedBundleId, typeof(T), normalizedAssetName);
            if (BundleAssetCache.TryGetValue(cacheKey, out var cachedAsset))
            {
                asset = cachedAsset as T;
                if (asset != null) return true;

                BundleAssetCache.Remove(cacheKey);
            }

            if (!TryGetLoadedBundle(normalizedBundleId, out var bundle)) return false;

            asset = bundle.LoadAsset<T>(normalizedAssetName);
            if (asset == null)
            {
                LogMissingBundleAsset(normalizedBundleId, normalizedAssetName, typeof(T));
                return false;
            }

            BundleAssetCache[cacheKey] = asset;
            AddBundleCacheKey(BundleAssetCacheKeysByBundleId, normalizedBundleId, cacheKey);
            return true;
        }

        public static bool TryLoadBundleGameObject(string bundleId, string assetName, out GameObject prefab)
        {
            return TryLoadBundleAsset(bundleId, assetName, out prefab);
        }

        public static bool TryLoadBundleAnimationCurve(string bundleId, string assetName, out AnimationCurve curve)
        {
            curve = null;

            var normalizedBundleId = NormalizeCacheKey(bundleId);
            var normalizedAssetName = NormalizeCacheKey(assetName);
            if (string.IsNullOrEmpty(normalizedBundleId) || string.IsNullOrEmpty(normalizedAssetName)) return false;

            var cacheKey = BuildBundleCurveCacheKey(normalizedBundleId, normalizedAssetName);
            if (BundleAnimationCurveCache.TryGetValue(cacheKey, out var cachedCurve) && cachedCurve != null)
            {
                curve = cachedCurve;
                return true;
            }

            if (!TryLoadBundleAsset(normalizedBundleId, normalizedAssetName, out AnimationCurveAsset curveAsset) ||
                curveAsset == null || curveAsset.Curve == null)
            {
                LogMissingBundleCurve(normalizedBundleId, normalizedAssetName);
                return false;
            }

            curve = CloneAnimationCurve(curveAsset.Curve);
            if (curve == null) return false;

            BundleAnimationCurveCache[cacheKey] = curve;
            AddBundleCacheKey(BundleCurveCacheKeysByBundleId, normalizedBundleId, cacheKey);
            return true;
        }

        public static RegisteredSpriteAnimation RegisterFrameAnimation(string id, IEnumerable<Sprite> frames,
            float framesPerSecond = 12f, bool loop = true)
        {
            if (string.IsNullOrWhiteSpace(id) || frames == null) return null;

            var frameArray = frames.Where(sprite => sprite != null).ToArray();
            if (frameArray.Length == 0) return null;

            var animation = new RegisteredSpriteAnimation
            {
                Id = NormalizeCacheKey(id),
                Frames = frameArray,
                FramesPerSecond = Mathf.Max(0f, framesPerSecond),
                Loop = loop
            };

            CacheSpriteAnimation(animation.Id, animation);
            return animation;
        }

        public static RegisteredSpriteAnimation LoadFrameAnimationFromFiles(string id, IEnumerable<string> framePaths,
            float pixelsPerUnit = PPU_WORLD, float framesPerSecond = 12f, bool loop = true)
        {
            if (framePaths == null) return null;

            var frames = framePaths
                .Select(framePath => LoadSpriteFromFile(framePath, pixelsPerUnit))
                .Where(sprite => sprite != null)
                .ToList();

            return RegisterFrameAnimation(id, frames, framesPerSecond, loop);
        }

        public static RegisteredSpriteAnimation LoadFrameAnimationFromFolder(string id, string folderPath,
            float pixelsPerUnit = PPU_WORLD, float framesPerSecond = 12f, bool loop = true, string prefix = null)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                LogMissingFileResource(folderPath, "animation folder");
                return null;
            }

            var normalizedPrefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix.Trim();
            var framePaths = Directory.GetFiles(folderPath)
                .Where(path => SupportedImageExtensions.Contains(Path.GetExtension(path)))
                .Where(path => string.IsNullOrEmpty(normalizedPrefix) || Path.GetFileNameWithoutExtension(path)
                    .StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => ExtractTrailingFrameNumber(Path.GetFileNameWithoutExtension(path)))
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return LoadFrameAnimationFromFiles(id, framePaths, pixelsPerUnit, framesPerSecond, loop);
        }

        public static RegisteredSpriteAnimation LoadFrameAnimationFromPluginFolder(string id, BaseUnityPlugin plugin,
            string relativeFolderPath, float pixelsPerUnit = PPU_WORLD, float framesPerSecond = 12f, bool loop = true,
            string prefix = null)
        {
            var fullPath = GetPluginFolderPath(plugin, relativeFolderPath);
            return string.IsNullOrEmpty(fullPath)
                ? null
                : LoadFrameAnimationFromFolder(id, fullPath, pixelsPerUnit, framesPerSecond, loop, prefix);
        }

        public static object LoadAnimationAsVideoClip(string pathOrResource, Assembly sourceAssembly = null)
        {
            var extension = Path.GetExtension(pathOrResource ?? string.Empty);
            if (!extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase)) return null;

            Logger?.LogWarning(
                "Runtime .gif/.mp4 import into Unity VideoClip assets is not supported in this game build. Use RegisterFrameAnimation instead!");
            Logger?.LogWarning("Sorry about that.");

            return null;
        }

        public static AudioClip LoadEmbeddedAudio(string resourcePath, Assembly sourceAssembly = null)
        {
            if (sourceAssembly == null)
                sourceAssembly = ContentReloadSession.GetSourceAssemblyOverride() ?? Assembly.GetCallingAssembly();

            if (sourceAssembly == null) return null;

            var clipCacheKey = $"{sourceAssembly.FullName}:{NormalizeResourcePath(resourcePath)}";
            if (AudioClipCache.TryGetValue(clipCacheKey, out var cachedClip)) return cachedClip;

            var resourceName = FindEmbeddedResourceName(resourcePath, sourceAssembly);
            if (string.IsNullOrEmpty(resourceName))
            {
                LogMissingEmbeddedResource(resourcePath, sourceAssembly, "audio");
                return null;
            }

            using (var stream = sourceAssembly.GetManifestResourceStream(resourceName))
            {
                var clip = LoadAudioFromStream(stream, Path.GetFileName(resourceName) ?? resourceName);
                if (clip == null) return null;

                clip.name = Path.GetFileNameWithoutExtension(resourceName) ?? resourceName;
                AudioClipCache[clipCacheKey] = clip;
                return clip;
            }
        }

        public static AudioClip LoadAudioFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                LogMissingFileResource(filePath, "audio");
                return null;
            }

            var fullPath = Path.GetFullPath(filePath);
            if (AudioClipCache.TryGetValue(fullPath, out var cachedClip)) return cachedClip;

            using (var stream = File.OpenRead(fullPath))
            {
                var clip = LoadAudioFromStream(stream, Path.GetFileName(fullPath));
                if (clip == null) return null;

                clip.name = Path.GetFileNameWithoutExtension(fullPath) ?? fullPath;
                AudioClipCache[fullPath] = clip;
                return clip;
            }
        }

        public static AudioClip LoadAudioFromPluginFolder(BaseUnityPlugin plugin, string relativePath)
        {
            var fullPath = GetPluginFolderPath(plugin, relativePath);
            return string.IsNullOrEmpty(fullPath)
                ? null
                : LoadAudioFromFile(fullPath);
        }

        public static string LoadEmbeddedText(string resourcePath, Assembly sourceAssembly = null)
        {
            using (var stream = LoadEmbeddedStream(resourcePath, sourceAssembly, "text"))
            {
                if (stream == null) return null;

                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        internal static Stream LoadEmbeddedStream(string resourcePath, Assembly sourceAssembly = null,
            string resourceType = "resource stream")
        {
            if (sourceAssembly == null)
                sourceAssembly = ContentReloadSession.GetSourceAssemblyOverride() ?? Assembly.GetCallingAssembly();

            if (sourceAssembly == null) return null;

            var foundResource = FindEmbeddedResourceName(resourcePath, sourceAssembly);
            if (string.IsNullOrEmpty(foundResource))
            {
                LogMissingEmbeddedResource(resourcePath, sourceAssembly, resourceType);
                return null;
            }

            var stream = sourceAssembly.GetManifestResourceStream(foundResource);
            if (stream != null) return stream;

            LogMissingEmbeddedResource(foundResource, sourceAssembly, resourceType);
            return null;
        }

        public static Sprite LoadSpriteFromFile(string filePath, float pixelsPerUnit = PPU_WORLD)
        {
            if (File.Exists(filePath)) return CreateSpriteFromBytes(File.ReadAllBytes(filePath), pixelsPerUnit);

            LogMissingFileResource(filePath, "sprite");
            return null;
        }

        public static Sprite LoadSpriteFromBytes(byte[] data, float pixelsPerUnit = PPU_WORLD)
        {
            if (data == null || data.Length == 0) return null;

            return CreateSpriteFromBytes(data, pixelsPerUnit);
        }

        public static Sprite LoadSpriteFromPluginFolder(BaseUnityPlugin plugin, string relativePath,
            float pixelsPerUnit = PPU_WORLD)
        {
            var fullPath = GetPluginFolderPath(plugin, relativePath);
            return string.IsNullOrEmpty(fullPath)
                ? null
                : LoadSpriteFromFile(fullPath, pixelsPerUnit);
        }

        public static bool TryApplyAnimation(SpriteRenderer renderer, string animationId)
        {
            if (renderer == null || string.IsNullOrWhiteSpace(animationId)) return false;

            var animation = GetCachedSpriteAnimation(animationId);
            if (animation == null) return false;

            var player = renderer.GetComponent<AnimatedSpriteRenderer>();
            if (player == null) player = renderer.gameObject.AddComponent<AnimatedSpriteRenderer>();

            player.SetAnimation(animationId, animation);
            return true;
        }

        public static bool TryApplyAnimation(Image image, string animationId)
        {
            if (image == null || string.IsNullOrWhiteSpace(animationId)) return false;

            var animation = GetCachedSpriteAnimation(animationId);
            if (animation == null) return false;

            var player = image.GetComponent<AnimatedImage>();
            if (player == null) player = image.gameObject.AddComponent<AnimatedImage>();

            player.SetAnimation(animationId, animation);
            return true;
        }

        private static Sprite LoadSpriteInternal(string resourcePath, float ppu, Assembly sourceAssembly)
        {
            if (sourceAssembly == null) return null;

            var resolvedResourceName = FindEmbeddedResourceName(resourcePath, sourceAssembly);
            if (string.IsNullOrEmpty(resolvedResourceName))
            {
                LogMissingEmbeddedResource(resourcePath, sourceAssembly, "sprite");
                return null;
            }

            return LoadEmbeddedSpriteVariant(sourceAssembly, resolvedResourceName, ppu);
        }

        private static Sprite LoadEmbeddedSpriteVariant(Assembly sourceAssembly, string resolvedResourceName, float ppu)
        {
            var texture = LoadEmbeddedTextureByResolvedResourceName(sourceAssembly, resolvedResourceName);
            if (texture == null) return null;

            var assemblyKey = GetAssemblyCacheKey(sourceAssembly);
            var textureCacheKey = BuildEmbeddedTextureCacheKey(assemblyKey, resolvedResourceName);
            var spriteVariantKey = BuildEmbeddedSpriteVariantKey(textureCacheKey, ppu);
            if (EmbeddedSpriteVariantCache.TryGetValue(spriteVariantKey, out var cachedSprite) && cachedSprite != null)
                return cachedSprite;

            var sprite = CreateSpriteFromTexture(texture, ppu, FilterMode.Point, 1, 1, resolvedResourceName, true);
            if (sprite == null) return null;

            EmbeddedSpriteVariantCache[spriteVariantKey] = sprite;
            AddAssemblyKey(AssemblySpriteVariantKeys, assemblyKey, spriteVariantKey);
            return sprite;
        }

        private static Texture2D LoadEmbeddedTextureByResolvedResourceName(Assembly sourceAssembly,
            string resolvedResourceName)
        {
            var assemblyKey = GetAssemblyCacheKey(sourceAssembly);
            var textureCacheKey = BuildEmbeddedTextureCacheKey(assemblyKey, resolvedResourceName);
            if (EmbeddedTextureCache.TryGetValue(textureCacheKey, out var cachedTexture) && cachedTexture != null)
                return cachedTexture;

            using (var stream = sourceAssembly.GetManifestResourceStream(resolvedResourceName))
            {
                if (stream == null)
                {
                    LogMissingEmbeddedResource(resolvedResourceName, sourceAssembly, "sprite");
                    return null;
                }

                var fileData = ReadAllBytes(stream);
                var texture = CreateTextureFromBytes(fileData, resolvedResourceName, FilterMode.Point);
                if (texture == null) return null;

                EmbeddedTextureCache[textureCacheKey] = texture;
                AddAssemblyKey(AssemblyTextureKeys, assemblyKey, textureCacheKey);
                return texture;
            }
        }

        private static Texture2D CreateTextureFromBytes(byte[] data, string textureName, FilterMode filterMode)
        {
            if (data == null || data.Length == 0) return null;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = filterMode,
                wrapMode = TextureWrapMode.Clamp,
                name = textureName
            };

            return texture.LoadImage(data) ? texture : null;
        }

        private static Sprite CreateSpriteFromBytes(byte[] data, float ppu)
        {
            var texture = CreateTextureFromBytes(data, string.Empty, FilterMode.Point);
            if (texture == null) return null;

            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                ppu);
        }

        private static Sprite CreateSpriteVariant(Sprite sourceSprite, float pixelsPerUnit)
        {
            if (sourceSprite == null) return null;

            var spriteVariant = Sprite.Create(sourceSprite.texture, sourceSprite.rect,
                sourceSprite.pivot / sourceSprite.rect.size, pixelsPerUnit, 0, SpriteMeshType.FullRect,
                sourceSprite.border);
            spriteVariant.name = sourceSprite.name;
            return spriteVariant;
        }

        private static string BuildSpriteVariantCacheKey(string normalizedId, float pixelsPerUnit)
        {
            return normalizedId + "|" + pixelsPerUnit.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void ClearCachedSpriteVariants(string normalizedId)
        {
            if (string.IsNullOrEmpty(normalizedId) || SpriteVariantCache.Count == 0) return;

            var prefix = normalizedId + "|";
            foreach (var entry in SpriteVariantCache
                         .Where(pair => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                         .ToArray())
            {
                SpriteVariantCache.Remove(entry.Key);
                if (entry.Value != null) UnityEngine.Object.Destroy(entry.Value);
            }
        }

        private static Sprite CreateSpriteFromTexture(Texture2D sourceTexture, float ppu, FilterMode filterMode,
            int widthMultiplier, int heightMultiplier, string spriteName, bool useSourceTextureDirectly)
        {
            if (sourceTexture == null) return null;

            var finalTexture = sourceTexture;
            var needsUniqueTexture = !useSourceTextureDirectly || filterMode != FilterMode.Point ||
                                     widthMultiplier > 1 || heightMultiplier > 1;

            if (needsUniqueTexture)
            {
                finalTexture = DuplicateTexture(sourceTexture, spriteName);
                if (finalTexture == null) return null;
            }

            if (widthMultiplier > 1 || heightMultiplier > 1)
            {
                finalTexture = ModifyTextures.ResizeTexture(finalTexture, widthMultiplier, heightMultiplier);
            }

            finalTexture.filterMode = filterMode;
            finalTexture.wrapMode = TextureWrapMode.Clamp;
            if (!string.IsNullOrWhiteSpace(spriteName)) finalTexture.name = spriteName;

            var sprite = Sprite.Create(finalTexture, new Rect(0, 0, finalTexture.width, finalTexture.height),
                new Vector2(0.5f, 0.5f), ppu);
            if (!string.IsNullOrWhiteSpace(spriteName)) sprite.name = spriteName;

            return sprite;
        }

        private static Texture2D DuplicateTexture(Texture2D sourceTexture, string textureName)
        {
            try
            {
                var duplicate = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false)
                {
                    filterMode = sourceTexture.filterMode,
                    wrapMode = sourceTexture.wrapMode,
                    name = textureName
                };

                duplicate.SetPixels32(sourceTexture.GetPixels32());
                duplicate.Apply();
                return duplicate;
            }
            catch (Exception ex)
            {
                Logger?.LogWarning("Could not duplicate texture '" + (sourceTexture.name ?? textureName) +
                                   "' for a custom sprite variant.\n" + ex);
                return null;
            }
        }

        private static IEnumerator PreloadEmbeddedSpritesInMainMenu()
        {
            while (EmbeddedSpritePreloadQueue.Count > 0)
            {
                if (!CUCoreUtils.IsMainMenuReady())
                {
                    embeddedSpritePreloadCoroutine = null;
                    yield break;
                }

                var frameStart = Time.realtimeSinceStartup;
                do
                {
                    if (EmbeddedSpritePreloadQueue.Count == 0) break;

                    var entry = EmbeddedSpritePreloadQueue.Dequeue();
                    QueuedEmbeddedTextureKeys.Remove(entry.TextureCacheKey);

                    if (entry.Assembly == null) continue;
                    if (EmbeddedTextureCache.ContainsKey(entry.TextureCacheKey)) continue;

                    _ = LoadEmbeddedTextureByResolvedResourceName(entry.Assembly, entry.ResourceName);
                } while (EmbeddedSpritePreloadQueue.Count > 0 &&
                         Time.realtimeSinceStartup - frameStart < EmbeddedSpritePreloadFrameBudgetSeconds);

                yield return null;
            }

            embeddedSpritePreloadCompleted = true;
            embeddedSpritePreloadCoroutine = null;
        }

        private static void DiscoverEmbeddedSpritePreloadQueue()
        {
            EmbeddedSpritePreloadQueue.Clear();
            QueuedEmbeddedTextureKeys.Clear();

            foreach (var pluginInfo in Chainloader.PluginInfos.Values
                         .OrderBy(info => info.Metadata?.GUID ?? string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                var assembly = pluginInfo?.Instance != null ? pluginInfo.Instance.GetType().Assembly : null;
                if (assembly == null) continue;

                var assemblyKey = GetAssemblyCacheKey(assembly);
                foreach (var resourceName in GetManifestResourceNames(assembly)
                             .Where(IsSupportedEmbeddedImageResource)
                             .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                {
                    var textureCacheKey = BuildEmbeddedTextureCacheKey(assemblyKey, resourceName);
                    if (EmbeddedTextureCache.ContainsKey(textureCacheKey)) continue;
                    if (!QueuedEmbeddedTextureKeys.Add(textureCacheKey)) continue;

                    EmbeddedSpritePreloadQueue.Enqueue(new EmbeddedSpritePreloadEntry(assembly, resourceName,
                        textureCacheKey));
                }
            }
        }

        private static void ResetEmbeddedSpritePreloadState()
        {
            EmbeddedSpritePreloadQueue.Clear();
            QueuedEmbeddedTextureKeys.Clear();
            embeddedSpritePreloadCoroutine = null;
            embeddedSpritePreloadCompleted = false;
            embeddedSpritePreloadDiscovered = false;
        }

        private static bool IsSupportedEmbeddedImageResource(string resourceName)
        {
            if (string.IsNullOrWhiteSpace(resourceName)) return false;

            return SupportedImageExtensions.Contains(Path.GetExtension(resourceName));
        }

        private static int ExtractTrailingFrameNumber(string fileNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension)) return int.MaxValue;

            var end = fileNameWithoutExtension.Length - 1;
            while (end >= 0 && char.IsDigit(fileNameWithoutExtension[end])) end--;

            var numericSuffix = fileNameWithoutExtension.Substring(end + 1);
            return int.TryParse(numericSuffix, out var frameNumber) ? frameNumber : int.MaxValue;
        }

        private static string FindEmbeddedResourceName(string resourcePath, Assembly sourceAssembly)
        {
            if (sourceAssembly == null) return null;

            var searchPattern = NormalizeResourcePath(resourcePath);
            if (string.IsNullOrEmpty(searchPattern)) return null;

            var searchPatternWithoutExtension = NormalizeResourceStem(resourcePath);
            var resourceNames = GetManifestResourceNames(sourceAssembly);
            if (resourceNames.Length == 0) return null;

            var exactMatch =
                resourceNames.FirstOrDefault(r => string.Equals(r, resourcePath, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(exactMatch)) return exactMatch;

            var normalizedMatch = resourceNames.FirstOrDefault(r =>
                string.Equals(NormalizeResourcePath(r), searchPattern, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(normalizedMatch)) return normalizedMatch;

            var suffixMatch =
                resourceNames.FirstOrDefault(r => r.EndsWith(searchPattern, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(suffixMatch)) return suffixMatch;

            var filenamePattern = NormalizeResourcePath(Path.GetFileName(resourcePath));
            if (!string.IsNullOrEmpty(filenamePattern))
            {
                var filenameMatch = resourceNames.FirstOrDefault(r =>
                    r.EndsWith(filenamePattern, StringComparison.OrdinalIgnoreCase) ||
                    NormalizeResourcePath(r).EndsWith(filenamePattern, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(filenameMatch)) return filenameMatch;
            }

            if (string.IsNullOrEmpty(searchPatternWithoutExtension)) return null;

            var stemMatch = resourceNames.FirstOrDefault(r =>
            {
                var normalizedStem = NormalizeResourceStem(r);
                return string.Equals(normalizedStem, searchPatternWithoutExtension, StringComparison.OrdinalIgnoreCase)
                       || normalizedStem.EndsWith("." + searchPatternWithoutExtension,
                           StringComparison.OrdinalIgnoreCase);
            });

            return string.IsNullOrEmpty(stemMatch) ? null : stemMatch;
        }

        private static string[] GetManifestResourceNames(Assembly sourceAssembly)
        {
            if (sourceAssembly == null) return Array.Empty<string>();

            var cacheKey = GetAssemblyCacheKey(sourceAssembly);
            if (string.IsNullOrWhiteSpace(cacheKey)) return sourceAssembly.GetManifestResourceNames();

            if (ResourceNameCache.TryGetValue(cacheKey, out var cachedNames)) return cachedNames;

            var names = sourceAssembly.GetManifestResourceNames();
            ResourceNameCache[cacheKey] = names;
            return names;
        }

        private static string GetPluginFolderPath(BaseUnityPlugin plugin, string relativePath)
        {
            if (plugin == null || string.IsNullOrWhiteSpace(relativePath)) return null;

            var pluginFolder = ContentReloadSession.GetPluginDirectoryOverride() ??
                               Path.GetDirectoryName(plugin.Info.Location);
            return string.IsNullOrEmpty(pluginFolder)
                ? null
                : Path.Combine(pluginFolder, relativePath);
        }

        private static void RegisterBundleDefinition(RegisteredAssetBundle registration)
        {
            if (registration == null || string.IsNullOrEmpty(registration.BundleId)) return;

            RegisteredBundles[registration.BundleId] = registration;
            AddBundleOwner(registration.OwnerAssemblyKey, registration.BundleId, AssemblyBundleIds);
            AddBundleOwner(registration.OwnerModGuid, registration.BundleId, ModGuidBundleIds);
        }

        private static bool TryGetLoadedBundle(string bundleId, out AssetBundle bundle)
        {
            bundle = null;

            if (!RegisteredBundles.TryGetValue(bundleId, out var registration) || registration == null)
            {
                Logger?.LogWarning($"No registered asset bundle with id '{bundleId}' was found.");
                return false;
            }

            if (registration.LoadedBundle != null)
            {
                bundle = registration.LoadedBundle;
                return true;
            }

            switch (registration.RegistrationKind)
            {
                case AssetBundleRegistrationKind.RuntimeInstance:
                    bundle = registration.LoadedBundle;
                    break;

                case AssetBundleRegistrationKind.LooseFile:
                    bundle = AssetBundle.LoadFromFile(registration.FilePath);
                    if (bundle == null) LogMissingFileResource(registration.FilePath, "asset bundle");
                    break;

                case AssetBundleRegistrationKind.EmbeddedResource:
                    using (var stream = LoadEmbeddedStream(registration.ResourcePath, registration.SourceAssembly,
                               "asset bundle"))
                    {
                        if (stream == null) return false;
                        var bytes = ReadAllBytes(stream);
                        bundle = bytes.Length > 0 ? AssetBundle.LoadFromMemory(bytes) : null;
                    }

                    if (bundle == null)
                        Logger?.LogWarning(
                            $"Could not load embedded asset bundle '{registration.Descriptor}' for bundle id '{bundleId}'.");
                    break;
            }

            if (bundle == null) return false;

            registration.LoadedBundle = bundle;
            return true;
        }

        private static bool ClearBundleAssetCaches(string bundleId)
        {
            var clearedAny = false;

            if (BundleAssetCacheKeysByBundleId.TryGetValue(bundleId, out var assetCacheKeys) && assetCacheKeys != null)
            {
                foreach (var cacheKey in assetCacheKeys.ToArray())
                {
                    clearedAny |= BundleAssetCache.Remove(cacheKey);
                }

                BundleAssetCacheKeysByBundleId.Remove(bundleId);
            }

            if (BundleCurveCacheKeysByBundleId.TryGetValue(bundleId, out var curveCacheKeys) && curveCacheKeys != null)
            {
                foreach (var cacheKey in curveCacheKeys.ToArray())
                {
                    clearedAny |= BundleAnimationCurveCache.Remove(cacheKey);
                }

                BundleCurveCacheKeysByBundleId.Remove(bundleId);
            }

            return clearedAny;
        }

        private static void RemoveBundleOwnership(RegisteredAssetBundle registration)
        {
            RemoveBundleOwner(registration?.OwnerAssemblyKey, registration?.BundleId, AssemblyBundleIds);
            RemoveBundleOwner(registration?.OwnerModGuid, registration?.BundleId, ModGuidBundleIds);
        }

        private static void AddBundleOwner(string ownerKey, string bundleId, Dictionary<string, HashSet<string>> index)
        {
            if (string.IsNullOrWhiteSpace(ownerKey) || string.IsNullOrWhiteSpace(bundleId)) return;

            if (!index.TryGetValue(ownerKey, out var bundleIds) || bundleIds == null)
            {
                bundleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                index[ownerKey] = bundleIds;
            }

            bundleIds.Add(bundleId);
        }

        private static void RemoveBundleOwner(string ownerKey, string bundleId, Dictionary<string, HashSet<string>> index)
        {
            if (string.IsNullOrWhiteSpace(ownerKey) || string.IsNullOrWhiteSpace(bundleId)) return;
            if (!index.TryGetValue(ownerKey, out var bundleIds) || bundleIds == null) return;

            bundleIds.Remove(bundleId);
            if (bundleIds.Count == 0) index.Remove(ownerKey);
        }

        private static void AddBundleCacheKey(Dictionary<string, HashSet<string>> index, string bundleId, string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(bundleId) || string.IsNullOrWhiteSpace(cacheKey)) return;

            if (!index.TryGetValue(bundleId, out var cacheKeys) || cacheKeys == null)
            {
                cacheKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                index[bundleId] = cacheKeys;
            }

            cacheKeys.Add(cacheKey);
        }

        private static string NormalizeResourcePath(string resourcePath)
        {
            return string.IsNullOrWhiteSpace(resourcePath)
                ? string.Empty
                : resourcePath.Trim().Replace('/', '.').Replace('\\', '.');
        }

        private static string NormalizeResourceStem(string resourcePath)
        {
            var normalizedPath = NormalizeResourcePath(resourcePath);
            if (string.IsNullOrEmpty(normalizedPath)) return string.Empty;

            var extension = Path.GetExtension(resourcePath ?? string.Empty);
            if (string.IsNullOrEmpty(extension)) return normalizedPath;

            return normalizedPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                ? normalizedPath.Substring(0, normalizedPath.Length - extension.Length)
                : normalizedPath;
        }

        private static string NormalizeCacheKey(string key)
        {
            return string.IsNullOrWhiteSpace(key)
                ? string.Empty
                : key.Trim();
        }

        private static string GetAssemblyCacheKey(Assembly sourceAssembly)
        {
            if (sourceAssembly == null) return string.Empty;

            return sourceAssembly.FullName ?? sourceAssembly.GetName().Name ?? string.Empty;
        }

        private static string ResolveOwnerModGuid(Assembly sourceAssembly)
        {
            var modGuid = ContentReloadSession.ResolveAmbientOwnerId();
            if (!string.IsNullOrWhiteSpace(modGuid)) return modGuid;
            if (sourceAssembly == null) return null;

            foreach (var pluginInfo in Chainloader.PluginInfos.Values)
            {
                var pluginAssembly = pluginInfo?.Instance != null ? pluginInfo.Instance.GetType().Assembly : null;
                if (pluginAssembly == null) continue;
                if (!ReferenceEquals(pluginAssembly, sourceAssembly)) continue;

                return pluginInfo.Metadata?.GUID;
            }

            return null;
        }

        private static string BuildEmbeddedTextureCacheKey(string assemblyKey, string resourceName)
        {
            return assemblyKey + "|" + NormalizeResourcePath(resourceName);
        }

        private static string BuildEmbeddedSpriteVariantKey(string textureCacheKey, float ppu)
        {
            return textureCacheKey + "|" + ppu.ToString("R");
        }

        private static string BuildBundleAssetCacheKey(string bundleId, Type assetType, string assetName)
        {
            return bundleId + "|" + (assetType?.FullName ?? typeof(UnityEngine.Object).FullName) + "|" + assetName;
        }

        private static string BuildBundleCurveCacheKey(string bundleId, string assetName)
        {
            return bundleId + "|curve|" + assetName;
        }

        private static void AddAssemblyKey(Dictionary<string, HashSet<string>> index, string assemblyKey, string key)
        {
            if (string.IsNullOrWhiteSpace(assemblyKey) || string.IsNullOrWhiteSpace(key)) return;

            if (!index.TryGetValue(assemblyKey, out var keys) || keys == null)
            {
                keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                index[assemblyKey] = keys;
            }

            keys.Add(key);
        }

        private static byte[] ReadAllBytes(Stream stream)
        {
            if (stream == null) return Array.Empty<byte>();

            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        private static AnimationCurve CloneAnimationCurve(AnimationCurve source)
        {
            if (source == null) return null;

            var clone = new AnimationCurve(source.keys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode
            };

            return clone;
        }

        private static void LogMissingEmbeddedResource(string resourcePath, Assembly sourceAssembly,
            string resourceType)
        {
            if (sourceAssembly == null || string.IsNullOrWhiteSpace(resourcePath)) return;

            var normalizedPath = NormalizeResourcePath(resourcePath);
            var assemblyKey = GetAssemblyCacheKey(sourceAssembly);
            var key = "embedded:" + resourceType + ":" + assemblyKey + ":" + normalizedPath;
            if (!LoggedMissingResources.Add(key)) return;

            Logger?.LogWarning(
                $"Could not load embedded {resourceType} '{resourcePath}' from assembly '{sourceAssembly.GetName().Name}'. " +
                $"CUCoreLib looked for normalized resource name '{normalizedPath}' and assembly resource suffix matches.");
        }

        private static void LogMissingFileResource(string filePath, string resourceType)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            var fullPath = Path.GetFullPath(filePath);
            var key = "file:" + resourceType + ":" + fullPath;
            if (!LoggedMissingResources.Add(key)) return;

            Logger?.LogWarning($"Could not load {resourceType} file '{fullPath}'.");
        }

        private static void LogMissingBundleAsset(string bundleId, string assetName, Type assetType)
        {
            if (string.IsNullOrWhiteSpace(bundleId) || string.IsNullOrWhiteSpace(assetName)) return;

            var key = "bundle-asset:" + bundleId + ":" + (assetType?.FullName ?? "object") + ":" + assetName;
            if (!LoggedMissingResources.Add(key)) return;

            Logger?.LogWarning(
                $"Could not load asset '{assetName}' of type '{assetType?.Name ?? "Object"}' from asset bundle '{bundleId}'.");
        }

        private static void LogMissingBundleCurve(string bundleId, string assetName)
        {
            if (string.IsNullOrWhiteSpace(bundleId) || string.IsNullOrWhiteSpace(assetName)) return;

            var key = "bundle-curve:" + bundleId + ":" + assetName;
            if (!LoggedMissingResources.Add(key)) return;

            Logger?.LogWarning(
                $"Could not resolve AnimationCurve asset '{assetName}' from asset bundle '{bundleId}'. " +
                "Wrap the curve in a CUCoreLib AnimationCurveAsset when authoring the bundle.");
        }

        private static AudioClip LoadAudioFromStream(Stream stream, string resourceName)
        {
            if (stream == null || string.IsNullOrWhiteSpace(resourceName)) return null;

            var extension = Path.GetExtension(resourceName);
            if (string.IsNullOrWhiteSpace(extension) || !SupportedAudioExtensions.Contains(extension))
            {
                Logger?.LogError(
                    $"Could not load audio file {resourceName}: Unknown or unsupported file extension {extension}");
                return null;
            }

            ISampleProvider provider;
            switch (extension.ToLowerInvariant())
            {
                case ".wav":
                    provider = new WaveFileReader(stream).ToSampleProvider();
                    break;

                case ".mp1":
                case ".mp2":
                case ".mp3":
                    provider = new Mp3FileReader(stream).ToSampleProvider();
                    break;

                case ".cue":
                    provider = new CueWaveFileReader(stream).ToSampleProvider();
                    break;

                case ".aif":
                case ".aiff":
                    provider = new AiffFileReader(stream).ToSampleProvider();
                    break;

                default:
                    Logger?.LogError(
                        $"Could not load audio file {resourceName}: Unknown or unsupported file extension {extension}");
                    return null;
            }

            var samples = new List<float>();
            var buffer = new float[provider.WaveFormat.SampleRate * provider.WaveFormat.Channels];
            int read;
            while ((read = provider.Read(buffer, 0, buffer.Length)) > 0) samples.AddRange(buffer.Take(read));

            var waveFormat = provider.WaveFormat;
            var sampleRate = waveFormat.SampleRate;
            var channels = waveFormat.Channels;
            var samplesPerChannel = samples.Count / channels;

            var clip = AudioClip.Create(resourceName, samplesPerChannel, channels, sampleRate, false);
            clip.SetData(samples.ToArray(), 0);
            return clip;
        }

        private sealed class EmbeddedSpritePreloadEntry
        {
            public EmbeddedSpritePreloadEntry(Assembly assembly, string resourceName, string textureCacheKey)
            {
                Assembly = assembly;
                ResourceName = resourceName;
                TextureCacheKey = textureCacheKey;
            }

            public Assembly Assembly { get; }
            public string ResourceName { get; }
            public string TextureCacheKey { get; }
        }

        private enum AssetBundleRegistrationKind
        {
            LooseFile,
            EmbeddedResource,
            RuntimeInstance
        }

        private sealed class RegisteredAssetBundle
        {
            public string BundleId;
            public string Descriptor;
            public string FilePath;
            public AssetBundle LoadedBundle;
            public string OwnerAssemblyKey;
            public string OwnerModGuid;
            public AssetBundleRegistrationKind RegistrationKind;
            public string ResourcePath;
            public Assembly SourceAssembly;
        }
    }
}
