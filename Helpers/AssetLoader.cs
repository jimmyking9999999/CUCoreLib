using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
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
        private static ManualLogSource Logger;
        internal static Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();
        internal static Dictionary<string, AudioClip> AudioClipCache = new Dictionary<string, AudioClip>();

        internal static Dictionary<string, RegisteredSpriteAnimation> SpriteAnimationCache =
            new Dictionary<string, RegisteredSpriteAnimation>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string[]> ResourceNameCache =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        // Prevent repeated error spam for the same missing asset lookup
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

            return Sprite.Create(sprite.texture, sprite.rect, sprite.pivot / sprite.rect.size, pixelsPerUnit, 0,
                SpriteMeshType.FullRect, sprite.border);
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
            if (string.IsNullOrWhiteSpace(id) || animation?.Frames == null ||
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
            if (sourceAssembly == null) sourceAssembly = Assembly.GetCallingAssembly();

            return LoadSpriteInternal(resourcePath, pixelsPerUnit, sourceAssembly);
        }

        public static Sprite LoadUISprite(string resourcePath, Assembly sourceAssembly = null)
        {
            if (sourceAssembly == null) sourceAssembly = Assembly.GetCallingAssembly();

            return LoadSpriteInternal(resourcePath, PPU_UI, sourceAssembly);
        }

        private static Sprite LoadSpriteInternal(string resourcePath, float ppu, Assembly sourceAssembly)
        {
            if (sourceAssembly == null) return null;

            var stream = OpenEmbeddedResourceStream(resourcePath, sourceAssembly);

            if (stream == null)
            {
                LogMissingEmbeddedResource(resourcePath, sourceAssembly, "sprite");
                return null;
            }

            using (stream)
            {
                var fileData = new byte[stream.Length];
                stream.Read(fileData, 0, fileData.Length); // This is fine imho
                return CreateSpriteFromBytes(fileData, ppu);
            }
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
            if (!extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase)) 
                return null;
            Logger?.LogWarning(
                "Runtime .gif/.mp4 import into Unity VideoClip assets is not supported in this game build. Use RegisterFrameAnimation instead!");
            Logger?.LogWarning("Sorry about that.");

            return null;
        }

        public static AudioClip LoadEmbeddedAudio(string resourcePath, Assembly sourceAssembly = null)
        {
            if (sourceAssembly == null) sourceAssembly = Assembly.GetCallingAssembly();

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

                clip.name = Path.GetFileNameWithoutExtension(fullPath);
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
            if (sourceAssembly == null) sourceAssembly = Assembly.GetCallingAssembly();

            var foundResource = FindEmbeddedResourceName(resourcePath, sourceAssembly);

            if (string.IsNullOrEmpty(foundResource))
            {
                LogMissingEmbeddedResource(resourcePath, sourceAssembly, "text");
                return null;
            }

            using (var stream = sourceAssembly.GetManifestResourceStream(foundResource))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static Sprite CreateSpriteFromBytes(byte[] data, float ppu)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            if (texture.LoadImage(data))
                return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                    ppu);
            return null;
        }

        private static string GetPluginFolderPath(BaseUnityPlugin plugin, string relativePath)
        {
            if (plugin == null || string.IsNullOrWhiteSpace(relativePath)) return null;

            var pluginFolder = Path.GetDirectoryName(plugin.Info.Location);
            return string.IsNullOrEmpty(pluginFolder)
                ? null
                : Path.Combine(pluginFolder, relativePath);
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

        private static int ExtractTrailingFrameNumber(string fileNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension)) return int.MaxValue;

            var end = fileNameWithoutExtension.Length - 1;
            while (end >= 0 && char.IsDigit(fileNameWithoutExtension[end])) end--;

            var numericSuffix = fileNameWithoutExtension.Substring(end + 1);
            // Non-numbered names sort last when frame files are ordered
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

            // strict -> permissive order
            // probably shouldn't add fuzzy matching
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

            if (!string.IsNullOrEmpty(searchPatternWithoutExtension))
            {
                var stemMatch = resourceNames.FirstOrDefault(r =>
                {
                    var normalizedStem = NormalizeResourceStem(r);
                    return string.Equals(normalizedStem, searchPatternWithoutExtension,
                               StringComparison.OrdinalIgnoreCase)
                           || normalizedStem.EndsWith("." + searchPatternWithoutExtension,
                               StringComparison.OrdinalIgnoreCase);
                });

                if (!string.IsNullOrEmpty(stemMatch)) return stemMatch;
            }

            return null;
        }

        private static Stream OpenEmbeddedResourceStream(string resourcePath, Assembly sourceAssembly)
        {
            var resourceName = FindEmbeddedResourceName(resourcePath, sourceAssembly);
            return string.IsNullOrEmpty(resourceName) ? null : sourceAssembly.GetManifestResourceStream(resourceName);
        }

        private static string[] GetManifestResourceNames(Assembly sourceAssembly)
        {
            if (sourceAssembly == null) return Array.Empty<string>();

            // not null
            var cacheKey = sourceAssembly.FullName ?? sourceAssembly.GetName().Name;
            if (string.IsNullOrWhiteSpace(cacheKey)) return sourceAssembly.GetManifestResourceNames();

            if (ResourceNameCache.TryGetValue(cacheKey, out var cachedNames)) return cachedNames;

            var names = sourceAssembly.GetManifestResourceNames();
            ResourceNameCache[cacheKey] = names;
            return names;
        }

        private static void LogMissingEmbeddedResource(string resourcePath, Assembly sourceAssembly,
            string resourceType)
        {
            if (sourceAssembly == null || string.IsNullOrWhiteSpace(resourcePath)) return;

            var normalizedPath = NormalizeResourcePath(resourcePath);
            var key = "embedded:" + resourceType + ":" + sourceAssembly.FullName + ":" + normalizedPath;
            if (!LoggedMissingResources.Add(key)) return;

            Logger?.LogError(
                $"Could not load embedded {resourceType} '{resourcePath}' (normalized '{normalizedPath}') from assembly '{sourceAssembly.GetName().Name}'.");
        }

        private static void LogMissingFileResource(string filePath, string resourceType)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            var fullPath = Path.GetFullPath(filePath);
            var key = "file:" + resourceType + ":" + fullPath;
            if (!LoggedMissingResources.Add(key)) return;

            Logger?.LogError($"Could not load {resourceType} file '{fullPath}'.");
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
    }
}