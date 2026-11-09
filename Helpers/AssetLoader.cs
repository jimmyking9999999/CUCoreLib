using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using CUCoreLib.ContentReload;
using CUCoreLib.Data;
using NAudio.Wave;
using NAudio.Vorbis;
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

        private static readonly Dictionary<string, string> SpriteAnimationOwnerAssemblyKeys =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, HashSet<string>> AssemblySpriteAnimationIds =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string[]> ResourceNameCache =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> ResolvedResourceNameCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private const string MissingResourceName = "\0";

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
                ".ogg",
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

        private static readonly Dictionary<string, HashSet<string>> BundleAssetCacheKeysByBundleId =
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

            RemoveSpriteAnimationOwnership(normalizedId);
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
            var resolutionPrefix = assemblyKey + "|";
            foreach (var cacheKey in ResolvedResourceNameCache.Keys
                         .Where(key => key.StartsWith(resolutionPrefix, StringComparison.OrdinalIgnoreCase)).ToArray())
                ResolvedResourceNameCache.Remove(cacheKey);
            LoggedMissingResources.RemoveWhere(key => key.IndexOf(assemblyKey, StringComparison.OrdinalIgnoreCase) >= 0);

            InvalidateSpriteAnimationsForAssembly(assemblyKey);
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

        /// <summary>
        /// Loads an ordered list of embedded image resources as a registered frame animation. Frame order is exactly
        /// the order supplied by <paramref name="frameResourcePaths"/>.
        /// </summary>
        public static RegisteredSpriteAnimation LoadFrameAnimationFromEmbeddedResources(string id,
            IEnumerable<string> frameResourcePaths, float pixelsPerUnit = PPU_WORLD, float framesPerSecond = 12f,
            bool loop = true, Assembly sourceAssembly = null)
        {
            if (frameResourcePaths == null) return null;

            if (sourceAssembly == null)
                sourceAssembly = ContentReloadSession.GetSourceAssemblyOverride() ?? Assembly.GetCallingAssembly();

            if (sourceAssembly == null) return null;

            var frames = frameResourcePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => LoadSpriteInternal(path, pixelsPerUnit, sourceAssembly))
                .Where(sprite => sprite != null)
                .ToArray();
            var animation = RegisterFrameAnimation(id, frames, framesPerSecond, loop);
            if (animation != null) TrackSpriteAnimationOwnership(animation.Id, sourceAssembly);

            return animation;
        }

        /// <summary>
        /// Reads an embedded Aseprite .ase/.aseprite file and registers its frames as a looping animation.
        /// The file's timeline — its tags, its per-frame durations, its layers — drives the animation,
        /// so a looping icon is authored in Aseprite rather than as a PNG strip a script assembles.
        /// </summary>
        /// <param name="id">The animation id to register under (used by IconAnimationId, SpriteAnimationId, etc.).</param>
        /// <param name="resourcePath">Embedded resource path (e.g. "images.Items.foo.ase").</param>
        /// <param name="tag">A named Aseprite tag to play, or null for every frame in the file.</param>
        /// <param name="pixelsPerUnit">Sprite pixels-per-unit (default 8 for world sprites).</param>
        /// <param name="sourceAssembly">Assembly containing the embedded resource (null = calling assembly).</param>
        /// <returns>The registered animation, or null when the file cannot be read.</returns>
        public static RegisteredSpriteAnimation LoadAsepriteAnimation(string id, string resourcePath,
            string tag = null, float pixelsPerUnit = PPU_WORLD, Assembly sourceAssembly = null)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(resourcePath)) return null;

            if (sourceAssembly == null)
                sourceAssembly = ContentReloadSession.GetSourceAssemblyOverride() ?? Assembly.GetCallingAssembly();

            if (sourceAssembly == null) return null;

            if (!TryReadEmbeddedResource(resourcePath, sourceAssembly, out var data))
            {
                Logger?.LogError($"LoadAsepriteAnimation: '{resourcePath}' is not embedded in the assembly.");
                return null;
            }

            if (!TryDecodeAseprite(data, resourcePath, out var width, out var height, out var layers,
                out var frames, out var tags)) return null;

            var order = ResolveAsepriteRange(tags, tag, frames.Count, resourcePath);
            if (order == null || order.Count == 0) return null;

            var sprites = new Sprite[order.Count];
            for (var i = 0; i < order.Count; i++)
            {
                var texture = BakeAsepriteFrame(id + "_" + i, width, height, layers, frames[order[i]]);
                if (texture == null)
                {
                    Logger?.LogError($"LoadAsepriteAnimation: frame {order[i]} of '{resourcePath}' is empty.");
                    return null;
                }

                sprites[i] = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f),
                    pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprites[i].name = id + "_" + i;
            }

            var fps = ComputeAsepriteFps(order, frames, resourcePath);
            var animation = RegisterFrameAnimation(id, sprites, fps, true);
            if (animation != null)
            {
                TrackSpriteAnimationOwnership(animation.Id, sourceAssembly);
                ReportAsepriteAnimation(animation, id, order.Count, width, height, fps);
            }

            return animation;
        }

        /// <summary>
        /// Reads an embedded Aseprite .ase/.aseprite file and returns a single frame as a Sprite.
        /// Useful for static images authored in Aseprite.
        /// </summary>
        /// <param name="resourcePath">Embedded resource path (e.g. "images.Items.foo.ase").</param>
        /// <param name="tag">A named Aseprite tag to select a frame from, or null for frame 0.</param>
        /// <param name="frameIndex">Zero-based frame index within the tag (or file if no tag). Defaults to 0.</param>
        /// <param name="pixelsPerUnit">Sprite pixels-per-unit (default 8 for world sprites).</param>
        /// <param name="sourceAssembly">Assembly containing the embedded resource (null = calling assembly).</param>
        /// <returns>The sprite, or null when the file cannot be read.</returns>
        public static Sprite LoadAsepriteSprite(string resourcePath, string tag = null, int frameIndex = 0,
            float pixelsPerUnit = PPU_WORLD, Assembly sourceAssembly = null)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return null;

            if (sourceAssembly == null)
                sourceAssembly = ContentReloadSession.GetSourceAssemblyOverride() ?? Assembly.GetCallingAssembly();

            if (sourceAssembly == null) return null;

            if (!TryReadEmbeddedResource(resourcePath, sourceAssembly, out var data))
            {
                Logger?.LogError($"LoadAsepriteSprite: '{resourcePath}' is not embedded in the assembly.");
                return null;
            }

            if (!TryDecodeAseprite(data, resourcePath, out var width, out var height, out var layers,
                out var frames, out var tags)) return null;

            var order = ResolveAsepriteRange(tags, tag, frames.Count, resourcePath);
            if (order == null || order.Count == 0) return null;

            if (frameIndex < 0 || frameIndex >= order.Count)
            {
                Logger?.LogError($"LoadAsepriteSprite: frame index {frameIndex} is out of range for " +
                                 $"'{resourcePath}' (has {order.Count} frame(s) in tag '{tag ?? "all"}').");
                return null;
            }

            var texture = BakeAsepriteFrame(resourcePath + "_frame" + frameIndex, width, height, layers,
                frames[order[frameIndex]]);
            if (texture == null)
            {
                Logger?.LogError($"LoadAsepriteSprite: frame {frameIndex} of '{resourcePath}' is empty.");
                return null;
            }

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f),
                pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = resourcePath + "_frame" + frameIndex;
            return sprite;
        }

        /// <summary>
        /// Reads an embedded Aseprite .ase/.aseprite file and returns all frames as a Sprite array (spritesheet).
        /// </summary>
        /// <param name="resourcePath">Embedded resource path (e.g. "images.Items.foo.ase").</param>
        /// <param name="tag">A named Aseprite tag to play, or null for every frame in the file.</param>
        /// <param name="pixelsPerUnit">Sprite pixels-per-unit (default 8 for world sprites).</param>
        /// <param name="sourceAssembly">Assembly containing the embedded resource (null = calling assembly).</param>
        /// <returns>Array of sprites, or null when the file cannot be read.</returns>
        public static Sprite[] LoadAsepriteSpritesheet(string resourcePath, string tag = null,
            float pixelsPerUnit = PPU_WORLD, Assembly sourceAssembly = null)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return null;

            if (sourceAssembly == null)
                sourceAssembly = ContentReloadSession.GetSourceAssemblyOverride() ?? Assembly.GetCallingAssembly();

            if (sourceAssembly == null) return null;

            if (!TryReadEmbeddedResource(resourcePath, sourceAssembly, out var data))
            {
                Logger?.LogError($"LoadAsepriteSpritesheet: '{resourcePath}' is not embedded in the assembly.");
                return null;
            }

            if (!TryDecodeAseprite(data, resourcePath, out var width, out var height, out var layers,
                out var frames, out var tags)) return null;

            var order = ResolveAsepriteRange(tags, tag, frames.Count, resourcePath);
            if (order == null || order.Count == 0) return null;

            var sprites = new Sprite[order.Count];
            for (var i = 0; i < order.Count; i++)
            {
                var texture = BakeAsepriteFrame(resourcePath + "_" + i, width, height, layers, frames[order[i]]);
                if (texture == null)
                {
                    Logger?.LogError($"LoadAsepriteSpritesheet: frame {order[i]} of '{resourcePath}' is empty.");
                    return null;
                }

                sprites[i] = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f),
                    pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprites[i].name = resourcePath + "_" + i;
            }

            return sprites;
        }

        /// <summary>
        /// Loads an Aseprite spritesheet with default pixels-per-unit.
        /// </summary>
        public static Sprite[] LoadAsepriteSpritesheet(string resourcePath, Assembly sourceAssembly)
            => LoadAsepriteSpritesheet(resourcePath, null, PPU_WORLD, sourceAssembly);

        /// <summary>
        /// Loads a single Aseprite sprite with default pixels-per-unit.
        /// </summary>
        public static Sprite LoadAsepriteSprite(string resourcePath, Assembly sourceAssembly)
            => LoadAsepriteSprite(resourcePath, null, 0, PPU_WORLD, sourceAssembly);

        /// <summary>
        /// Loads an Aseprite animation with default pixels-per-unit.
        /// </summary>
        public static RegisteredSpriteAnimation LoadAsepriteAnimation(string id, string resourcePath,
            Assembly sourceAssembly)
            => LoadAsepriteAnimation(id, resourcePath, null, PPU_WORLD, sourceAssembly);

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
            if (sourceAssembly == null)
            {
                Assembly assembly = ContentReloadSession.GetSourceAssemblyOverride();
                if ((object)assembly == null)
                    assembly = Assembly.GetCallingAssembly();
                sourceAssembly = assembly;
            }

            using (Stream stream = AssetLoader.LoadEmbeddedStream(resourcePath, sourceAssembly, "text"))
            {
                if (stream == null)
                    return null;
                using (StreamReader streamReader = new StreamReader(stream))
                    return streamReader.ReadToEnd();
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

        public static Sprite LoadSpriteFromBytes(byte[] data, float pixelsPerUnit, Vector2 pivot)
        {
            if (data == null || data.Length == 0) return null;

            return CreateSpriteFromBytes(data, pixelsPerUnit, pivot);
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
            
            if (player.AnimationId == animationId && player.Animation == animation) return true;

            player.SetAnimation(animationId, animation);
            return true;
        }

        private static Sprite LoadSpriteInternal(string resourcePath, float ppu, Assembly sourceAssembly)
        {
            if (sourceAssembly == null) return null;

            // Route .ase/.aseprite files to the Aseprite loader
            var extension = Path.GetExtension(resourcePath ?? string.Empty);
            if (extension.Equals(".ase", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".aseprite", StringComparison.OrdinalIgnoreCase))
            {
                return LoadAsepriteSprite(resourcePath, null, 0, ppu, sourceAssembly);
            }

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

        private static Sprite CreateSpriteFromBytes(byte[] data, float ppu, Vector2? pivot = null)
        {
            var texture = CreateTextureFromBytes(data, string.Empty, FilterMode.Point);
            if (texture == null) return null;

            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                pivot ?? new Vector2(0.5f, 0.5f), ppu);
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

        private static void TrackSpriteAnimationOwnership(string animationId, Assembly sourceAssembly)
        {
            var assemblyKey = GetAssemblyCacheKey(sourceAssembly);
            if (string.IsNullOrWhiteSpace(animationId) || string.IsNullOrWhiteSpace(assemblyKey)) return;

            RemoveSpriteAnimationOwnership(animationId);
            SpriteAnimationOwnerAssemblyKeys[animationId] = assemblyKey;

            if (!AssemblySpriteAnimationIds.TryGetValue(assemblyKey, out var animationIds) || animationIds == null)
            {
                animationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AssemblySpriteAnimationIds[assemblyKey] = animationIds;
            }

            animationIds.Add(animationId);
        }

        private static void InvalidateSpriteAnimationsForAssembly(string assemblyKey)
        {
            if (string.IsNullOrWhiteSpace(assemblyKey) ||
                !AssemblySpriteAnimationIds.TryGetValue(assemblyKey, out var animationIds) || animationIds == null)
                return;

            foreach (var animationId in animationIds.ToArray())
            {
                if (!SpriteAnimationOwnerAssemblyKeys.TryGetValue(animationId, out var ownerAssemblyKey) ||
                    !string.Equals(ownerAssemblyKey, assemblyKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (SpriteAnimationCache.TryGetValue(animationId, out var animation) && animation?.Frames?.Length > 0 &&
                    SpriteCache.TryGetValue(animationId, out var cachedSprite) &&
                    ReferenceEquals(cachedSprite, animation.Frames[0]))
                {
                    SpriteCache.Remove(animationId);
                    ClearCachedSpriteVariants(animationId);
                }

                SpriteAnimationCache.Remove(animationId);
                SpriteAnimationOwnerAssemblyKeys.Remove(animationId);
            }

            AssemblySpriteAnimationIds.Remove(assemblyKey);
        }

        private static void RemoveSpriteAnimationOwnership(string animationId)
        {
            if (string.IsNullOrWhiteSpace(animationId) ||
                !SpriteAnimationOwnerAssemblyKeys.TryGetValue(animationId, out var assemblyKey))
                return;

            SpriteAnimationOwnerAssemblyKeys.Remove(animationId);
            if (!AssemblySpriteAnimationIds.TryGetValue(assemblyKey, out var animationIds) || animationIds == null) return;

            animationIds.Remove(animationId);
            if (animationIds.Count == 0) AssemblySpriteAnimationIds.Remove(assemblyKey);
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

            var assemblyKey = GetAssemblyCacheKey(sourceAssembly);
            var resolutionCacheKey = string.IsNullOrWhiteSpace(assemblyKey)
                ? null
                : assemblyKey + "|" + resourcePath.Trim();
            if (resolutionCacheKey != null &&
                ResolvedResourceNameCache.TryGetValue(resolutionCacheKey, out var cachedResourceName))
                return cachedResourceName == MissingResourceName ? null : cachedResourceName;

            var searchPatternWithoutExtension = NormalizeResourceStem(resourcePath);
            var resourceNames = GetManifestResourceNames(sourceAssembly);
            if (resourceNames.Length == 0) return CacheResolvedResourceName(resolutionCacheKey, null);

            var exactMatch =
                resourceNames.FirstOrDefault(r => string.Equals(r, resourcePath, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(exactMatch)) return CacheResolvedResourceName(resolutionCacheKey, exactMatch);

            var normalizedMatch = resourceNames.FirstOrDefault(r =>
                string.Equals(NormalizeResourcePath(r), searchPattern, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(normalizedMatch))
                return CacheResolvedResourceName(resolutionCacheKey, normalizedMatch);

            var suffixMatch =
                resourceNames.FirstOrDefault(r => r.EndsWith(searchPattern, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(suffixMatch)) return CacheResolvedResourceName(resolutionCacheKey, suffixMatch);

            var filenamePattern = NormalizeResourcePath(Path.GetFileName(resourcePath));
            if (!string.IsNullOrEmpty(filenamePattern))
            {
                var filenameMatch = resourceNames.FirstOrDefault(r =>
                    r.EndsWith(filenamePattern, StringComparison.OrdinalIgnoreCase) ||
                    NormalizeResourcePath(r).EndsWith(filenamePattern, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(filenameMatch))
                    return CacheResolvedResourceName(resolutionCacheKey, filenameMatch);
            }

            if (string.IsNullOrEmpty(searchPatternWithoutExtension))
                return CacheResolvedResourceName(resolutionCacheKey, null);

            var stemMatch = resourceNames.FirstOrDefault(r =>
            {
                var normalizedStem = NormalizeResourceStem(r);
                return string.Equals(normalizedStem, searchPatternWithoutExtension, StringComparison.OrdinalIgnoreCase)
                       || normalizedStem.EndsWith("." + searchPatternWithoutExtension,
                           StringComparison.OrdinalIgnoreCase);
            });

            return CacheResolvedResourceName(resolutionCacheKey, stemMatch);
        }

        private static string CacheResolvedResourceName(string cacheKey, string resourceName)
        {
            if (cacheKey != null)
                ResolvedResourceNameCache[cacheKey] = string.IsNullOrEmpty(resourceName)
                    ? MissingResourceName
                    : resourceName;

            return string.IsNullOrEmpty(resourceName) ? null : resourceName;
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

                case ".ogg":
                    provider = new VorbisWaveReader(stream).ToSampleProvider();
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

        #region Aseprite Parser

        private const int AsepriteHeaderSize = 128;
        private const ushort AsepriteFileMagic = 0xA5E0;
        private const ushort AsepriteFrameMagic = 0xF1FA;
        private const ushort AsepriteChunkPalette = 0x0004;
        private const ushort AsepriteChunkLayer = 0x2004;
        private const ushort AsepriteChunkCel = 0x2005;
        private const ushort AsepriteChunkTag = 0x2018;

        private sealed class AsepriteLayer
        {
            internal bool Visible;
            internal byte Opacity;
        }

        private sealed class AsepriteCel
        {
            internal int Layer;
            internal int X;
            internal int Y;
            internal byte Opacity;
            internal int LinkFrame = -1;
            internal int Width;
            internal int Height;
            internal Color32[] Pixels;
        }

        private sealed class AsepriteFrame
        {
            internal int DurationMs;
            internal readonly List<AsepriteCel> Cels = new List<AsepriteCel>();
        }

        private sealed class AsepriteTag
        {
            internal string Name;
            internal int From;
            internal int To;
        }

        private static bool TryReadEmbeddedResource(string resourcePath, Assembly sourceAssembly, out byte[] data)
        {
            data = null;
            if (sourceAssembly == null || string.IsNullOrWhiteSpace(resourcePath)) return false;

            var resolvedName = FindEmbeddedResourceName(resourcePath, sourceAssembly);
            if (string.IsNullOrEmpty(resolvedName)) return false;

            using (var stream = sourceAssembly.GetManifestResourceStream(resolvedName))
            {
                if (stream == null) return false;
                using (var buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    data = buffer.ToArray();
                }
            }

            return data != null && data.Length > AsepriteHeaderSize;
        }

        private static bool TryDecodeAseprite(byte[] data, string resource, out int width, out int height,
            out List<AsepriteLayer> layers, out List<AsepriteFrame> frames, out List<AsepriteTag> tags)
        {
            width = height = 0;
            layers = new List<AsepriteLayer>();
            frames = new List<AsepriteFrame>();
            tags = new List<AsepriteTag>();

            var reader = new BinaryReader(new MemoryStream(data));
            try
            {
                reader.ReadUInt32();
                if (reader.ReadUInt16() != AsepriteFileMagic)
                {
                    Logger?.LogError(data.Length > 3 && data[0] == 0x50 && data[1] == 0x4B
                        ? $"LoadAsepriteAnimation: '{resource}' is a compressed Aseprite file. Save it with " +
                          "File > Save As with 'Compress' off."
                        : $"LoadAsepriteAnimation: '{resource}' is not an Aseprite file.");
                    return false;
                }

                var frameCount = reader.ReadUInt16();
                width = reader.ReadUInt16();
                height = reader.ReadUInt16();
                var depth = reader.ReadUInt16();
                if (depth != 32 || width <= 0 || height <= 0 || frameCount == 0)
                {
                    Logger?.LogError($"LoadAsepriteAnimation: '{resource}' is {depth}-bit. Only RGB (32-bit) " +
                                     "files carry per-pixel colour and alpha; use Sprite > Convert to > RGB Color.");
                    return false;
                }

                var cursor = (long)AsepriteHeaderSize;
                for (var f = 0; f < frameCount; f++)
                {
                    reader.BaseStream.Position = cursor;
                    var blockSize = reader.ReadUInt32();
                    if (reader.ReadUInt16() != AsepriteFrameMagic)
                    {
                        Logger?.LogError($"LoadAsepriteAnimation: frame {f} of '{resource}' has no frame header.");
                        return false;
                    }

                    var chunkCount = reader.ReadUInt16();
                    var frame = new AsepriteFrame { DurationMs = reader.ReadUInt16() };
                    reader.ReadUInt16();
                    var newCount = reader.ReadUInt32();
                    if (chunkCount == 0xFFFF) chunkCount = (ushort)newCount;
                    frames.Add(frame);

                    var end = cursor + blockSize;
                    cursor += 16;
                    for (var c = 0; c < chunkCount; c++)
                    {
                        var chunkSize = reader.ReadUInt32();
                        var type = reader.ReadUInt16();
                        if (chunkSize < 6 || reader.BaseStream.Position + chunkSize - 6 > end)
                        {
                            // I trust aseprite to not run into this
                            return false;
                        }

                        var body = reader.ReadBytes((int)chunkSize - 6);
                        switch (type)
                        {
                            case AsepriteChunkLayer:
                                ReadAsepriteLayer(body, layers);
                                break;
                            case AsepriteChunkCel:
                                ReadAsepriteCel(body, f, resource, frame);
                                break;
                            case AsepriteChunkTag:
                                ReadAsepriteTags(body, tags);
                                break;
                            case AsepriteChunkPalette:
                                break;
                            default:
                                break;
                        }

                        cursor += chunkSize;
                    }

                    if (cursor != end)
                    {
                        //Logger?.LogError($"LoadAsepriteAnimation: frame {f} of '{resource}' holds " +
                        //                 $"{chunkCount} chunk(s) that do not fill its {blockSize} bytes.");
                        return false;
                    }
                }

                if (cursor != data.Length)
                {
                    //Logger?.LogError($"LoadAsepriteAnimation: '{resource}' has {data.Length - cursor} byte(s) " +
                    //                 "after its last frame.");
                    return false;
                }

                ResolveAsepriteLinks(frames, resource);
                return true;
            }
            catch (Exception e)
            {
                Logger?.LogError($"LoadAsepriteAnimation: '{resource}' could not be read: {e.Message}");
                return false;
            }
            finally
            {
                reader.Dispose();
            }
        }

        private static void ReadAsepriteLayer(byte[] body, List<AsepriteLayer> layers)
        {
            layers.Add(new AsepriteLayer
            {
                Visible = (body[0] & 1) != 0,
                Opacity = body.Length > 12 ? body[12] : (byte)255
            });
        }

        private static void ReadAsepriteTags(byte[] body, List<AsepriteTag> tags)
        {
            var count = body[0] | body[1] << 8;
            var offset = 10;
            for (var t = 0; t < count && offset + 19 <= body.Length; t++)
            {
                var length = body[offset + 17] | body[offset + 18] << 8;
                if (offset + 19 + length > body.Length) break;
                tags.Add(new AsepriteTag
                {
                    From = body[offset] | body[offset + 1] << 8,
                    To = body[offset + 2] | body[offset + 3] << 8,
                    Name = Encoding.UTF8.GetString(body, offset + 19, length)
                });
                offset += 19 + length;
            }
        }

        private static void ReadAsepriteCel(byte[] body, int frameIndex, string resource, AsepriteFrame frame)
        {
            if (body.Length < 9) return;
            var cel = new AsepriteCel
            {
                Layer = body[0] | body[1] << 8,
                X = (short)(body[2] | body[3] << 8),
                Y = (short)(body[4] | body[5] << 8),
                Opacity = body[6]
            };
            frame.Cels.Add(cel);

            var type = body[7] | body[8] << 8;
            if (type == 1)
            {
                if (body.Length >= 18) cel.LinkFrame = body[16] | body[17] << 8;
                return;
            }

            if (type != 0 && type != 2)
            {
                Logger?.LogError($"LoadAsepriteAnimation: cel {frameIndex} of '{resource}' is tilemap-" +
                                 "compressed, which this loader cannot read! Sorry!");
                return;
            }

            if (body.Length < 20) return;
            cel.Width = body[16] | body[17] << 8;
            cel.Height = body[18] | body[19] << 8;
            if (cel.Width <= 0 || cel.Height <= 0 || cel.Width > 4096 || cel.Height > 4096) return;

            var stream = type == 2 ? InflateAseprite(body, 22, body.Length - 22, resource, frameIndex)
                : SliceAseprite(body, 20, body.Length - 20);
            cel.Pixels = ReadAsepritePixels(stream, cel.Width, cel.Height);
            if (cel.Pixels == null)
            {
                Logger?.LogError($"Please change your aseprite file '{resource}' to use an RGBA color mode! Thanks.");
            }
        }

        private static Color32[] ReadAsepritePixels(byte[] stream, int width, int height)
        {
            if (stream == null) return null;
            var cells = width * height;
            if (stream.Length == cells * 4) return ToAsepritePixels(stream, cells);

            var pixels = new byte[cells * 4];
            var written = 0;
            var offset = 0;
            for (var row = 0; row < height; row++)
            {
                var filled = 0;
                while (filled < width)
                {
                    if (offset + 2 > stream.Length) return null;
                    var run = stream[offset] | stream[offset + 1] << 8;
                    offset += 2;
                    if (run > width - filled || offset + run * 4 > stream.Length) return null;
                    Array.Copy(stream, offset, pixels, written, run * 4);
                    offset += run * 4;
                    written += run * 4;
                    filled += run;
                }
            }

            return offset == stream.Length ? ToAsepritePixels(pixels, cells) : null;
        }

        private static void ResolveAsepriteLinks(List<AsepriteFrame> frames, string resource)
        {
            foreach (var frame in frames)
            for (var i = 0; i < frame.Cels.Count; i++)
            {
                var cel = frame.Cels[i];
                if (cel.Pixels != null || cel.LinkFrame < 0) continue;
                for (var source = cel.LinkFrame; source >= 0 && source < frames.Count;)
                {
                    var match = frames[source].Cels.FirstOrDefault(c => c.Layer == cel.Layer);
                    if (match == null) break;
                    if (match.Pixels == null)
                    {
                        source = match.LinkFrame;
                        continue;
                    }

                    cel.Width = match.Width;
                    cel.Height = match.Height;
                    cel.Pixels = match.Pixels;
                    break;
                }

                if (cel.Pixels == null)
                {
                    Logger?.LogError($"LoadAsepriteAnimation: a cel of '{resource}' links to frame " +
                                     $"{cel.LinkFrame}, which holds no pixels for that layer.");
                }
            }
        }

        private static Color32[] ToAsepritePixels(byte[] stream, int count)
        {
            var pixels = new Color32[count];
            for (var i = 0; i < count; i++)
                pixels[i] = new Color32(stream[i * 4], stream[i * 4 + 1], stream[i * 4 + 2], stream[i * 4 + 3]);
            return pixels;
        }

        private static byte[] SliceAseprite(byte[] body, int from, int length)
        {
            if (length <= 0 || from < 0 || from + length > body.Length) return null;
            var slice = new byte[length];
            Array.Copy(body, from, slice, 0, length);
            return slice;
        }

        private static byte[] InflateAseprite(byte[] body, int from, int length, string resource, int frameIndex)
        {
            var slice = SliceAseprite(body, from, length);
            if (slice == null)
            {
                // Logger?.LogError($"LoadAsepriteAnimation: cel {frameIndex} of '{resource}' is not long " +
                //                  "enough to hold a zlib stream.");
                return null;
            }

            try
            {
                using (var compressed = new MemoryStream(slice, false))
                using (var deflate = new System.IO.Compression.DeflateStream(compressed,
                    System.IO.Compression.CompressionMode.Decompress))
                using (var decoded = new MemoryStream())
                {
                    deflate.CopyTo(decoded);
                    return decoded.ToArray();
                }
            }
            catch (Exception e)
            {
                Logger?.LogError($"LoadAsepriteAnimation: cel {frameIndex} of '{resource}' did not " +
                                 $"inflate: {e.Message}");
                return null;
            }
        }

        private static Texture2D BakeAsepriteFrame(string name, int width, int height,
            IList<AsepriteLayer> layers, AsepriteFrame frame)
        {
            var top = new Color32[width * height];
            foreach (var cel in frame.Cels)
            {
                if (cel.Layer < 0 || cel.Layer >= layers.Count) continue;
                var layer = layers[cel.Layer];
                if (!layer.Visible || cel.Pixels == null) continue;
                var stacking = (cel.Opacity / 255f) * (layer.Opacity / 255f);
                if (stacking <= 0f) continue;

                for (var y = 0; y < cel.Height; y++)
                {
                    var row = cel.Y + y;
                    if (row < 0 || row >= height) continue;
                    for (var x = 0; x < cel.Width; x++)
                    {
                        var column = cel.X + x;
                        if (column < 0 || column >= width) continue;
                        BlendAsepriteOver(ref top[row * width + column], cel.Pixels[y * cel.Width + x], stacking);
                    }
                }
            }

            var any = false;
            for (var i = 0; i < top.Length; i++)
                if (top[i].a != 0)
                {
                    any = true;
                    break;
                }

            if (!any) return null;

            var pixels = new Color32[width * height];
            for (var row = 0; row < height; row++)
                Array.Copy(top, (height - 1 - row) * width, pixels, row * width, width);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                name = name + " (aseprite)"
            };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        private static void BlendAsepriteOver(ref Color32 destination, Color32 source, float stacking)
        {
            var sourceAlpha = (source.a / 255f) * stacking;
            if (sourceAlpha <= 0f) return;
            var destinationAlpha = destination.a / 255f;
            var outgoing = 1f - sourceAlpha;
            var result = sourceAlpha + destinationAlpha * outgoing;
            if (result <= 0f)
            {
                destination = new Color32(0, 0, 0, 0);
                return;
            }

            destination = new Color32(
                (byte)Mathf.Clamp((source.r * sourceAlpha + destination.r * destinationAlpha * outgoing) / result,
                    0f, 255f),
                (byte)Mathf.Clamp((source.g * sourceAlpha + destination.g * destinationAlpha * outgoing) / result,
                    0f, 255f),
                (byte)Mathf.Clamp((source.b * sourceAlpha + destination.b * destinationAlpha * outgoing) / result,
                    0f, 255f),
                (byte)Mathf.Clamp(result * 255f, 0f, 255f));
        }

        private static List<int> ResolveAsepriteRange(IList<AsepriteTag> tags, string tag, int frameCount,
            string resource)
        {
            if (string.IsNullOrWhiteSpace(tag)) return Enumerable.Range(0, frameCount).ToList();

            var match = tags.FirstOrDefault(t => string.Equals(t.Name, tag, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                Logger?.LogError($"LoadAsepriteAnimation: '{resource}' has no tag '{tag}'.");
                return null;
            }

            if (match.From < 0 || match.To < match.From || match.To >= frameCount)
            {
                Logger?.LogError($"LoadAsepriteAnimation: tag '{tag}' of '{resource}' covers frames " +
                                 $"{match.From}-{match.To} of {frameCount}.");
                return null;
            }

            return Enumerable.Range(match.From, match.To - match.From + 1).ToList();
        }

        private static float ComputeAsepriteFps(IList<int> order, IList<AsepriteFrame> frames, string resource)
        {
            var total = 0;
            foreach (var index in order) total += Math.Max(1, frames[index].DurationMs);
            if (order.Select(index => frames[index].DurationMs).Distinct().Count() > 1)
            {
                // 
            }

            return 1000f / (total / (float)order.Count);
        }

        private static void ReportAsepriteAnimation(RegisteredSpriteAnimation animation, string id,
            int frameCount, int width, int height, float framesPerSecond)
        {
            var held = GetCachedSpriteAnimation(id);
            var playable = held != null && held.Frames != null && held.Frames.Length > 0 &&
                           held.Frames[0] != null;
            Logger?.LogInfo($"LoadAsepriteAnimation: '{id}' baked {frameCount} frame(s) of {width}x{height} at " +
                            $"{framesPerSecond:0.#} fps, cache reads back " +
                            $"{(playable ? $"{held.Frames.Length} frame(s)" : "NOTHING")}.");
            if (animation == null || !playable)
            {
                // Logger?.LogError($"LoadAsepriteAnimation: '{id}' did not reach the animation cache, " +
                //                  "so nothing will play it.");
            }
        }

        #endregion
    }
}
