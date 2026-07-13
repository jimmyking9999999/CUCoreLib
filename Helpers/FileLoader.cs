using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using CUCoreLib.ContentReload;
using UnityEngine;

namespace CUCoreLib.Helpers
{
    internal static class FileLoader
    {
        private static ManualLogSource Logger;

        public static void Initialize(ManualLogSource logger)
        {
            Logger = logger;
        }

        public static string LoadEmbeddedText(string filename)
        {
            return AssetLoader.LoadEmbeddedText(filename, ResolveSourceAssembly());
        }


        public static Sprite LoadSpriteFromFile(string filename)
        {
            // Introduced optional parameters for method 'Sprite LoadSpriteFromFile(string, float, FilterMode, int, int)'
            return LoadSpriteFromFile(filename, 100, FilterMode.Point, 1, 1);
        }

        public static Sprite LoadSpriteFromFile(string filename, float ppu, FilterMode filterMode)
        {
            return LoadSpriteFromFile(filename, ppu, filterMode, 1, 1);
        }

        // Direct file loads
        public static Sprite LoadSpriteFromFile(string filename, float ppu, FilterMode filterMode, int widthMultiplier,
            int heightMultiplier)
        {
            var pluginPath = ResolvePluginDirectory();
            var imagePath = TryResolveSpriteFilePath(pluginPath, filename, out var attemptedPaths);
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                Logger?.LogWarning(BuildMissingSpriteMessage(filename, attemptedPaths));
                return null;
            }

            var fileData = File.ReadAllBytes(imagePath);

            var originalTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            if (!originalTexture.LoadImage(fileData)) return null;
            Texture2D finalTexture;

            if (widthMultiplier > 1 || heightMultiplier > 1)
                finalTexture = ModifyTextures.ResizeTexture(originalTexture, widthMultiplier, heightMultiplier);
            else
                finalTexture = originalTexture;

            finalTexture.filterMode = filterMode;
            finalTexture.wrapMode = TextureWrapMode.Clamp;

            return Sprite.Create(
                finalTexture,
                new Rect(0, 0, finalTexture.width, finalTexture.height),
                new Vector2(0.5f, 0.5f),
                ppu
            );
        }


        // Embedded Resource file loads 
        public static AudioClip LoadEmbeddedAudio(string fileName)
        {
            return AssetLoader.LoadEmbeddedAudio(fileName, ResolveSourceAssembly());
        }

        public static Sprite LoadEmbeddedSprite(string filename)
        {
            // Introduced optional parameters for method 'Sprite LoadEmbeddedSprite(string, float, FilterMode, int, int)' 
            return LoadEmbeddedSprite(filename, 100, FilterMode.Point, 1, 1);
        }

        public static Sprite LoadEmbeddedSprite(string filename, float ppu, FilterMode filterMode)
        {
            return LoadEmbeddedSprite(filename, ppu, filterMode, 1, 1);
        }

        public static Sprite LoadEmbeddedSprite(string filename, float ppu, FilterMode filterMode, int widthMultiplier,
            int heightMultiplier)
        {
            var asm = ResolveSourceAssembly();
            if (asm == null) return null;

            if (filterMode == FilterMode.Point && widthMultiplier <= 1 && heightMultiplier <= 1)
                return AssetLoader.LoadEmbeddedSprite(filename, ppu, asm);

            var sprite = AssetLoader.CreateSpriteFromEmbeddedTexture(filename, asm, ppu, filterMode, widthMultiplier,
                heightMultiplier);
            if (sprite != null) return sprite;

            Logger?.LogWarning(
                BuildMissingEmbeddedSpriteMessage(filename, asm));
            return null;
        }

        private static Assembly ResolveSourceAssembly()
        {
            return ContentReloadSession.GetSourceAssemblyOverride() ?? Assembly.GetCallingAssembly();
        }

        private static string ResolvePluginDirectory()
        {
            var overrideDirectory = ContentReloadSession.GetPluginDirectoryOverride();
            return !string.IsNullOrWhiteSpace(overrideDirectory)
                ? overrideDirectory
                : Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        private static string TryResolveSpriteFilePath(string pluginPath, string filename, out string[] attemptedPaths)
        {
            attemptedPaths = new[]
            {
                Path.Combine(pluginPath, "Images", filename),
                Path.Combine(pluginPath, filename),
                Path.Combine(pluginPath, "Images", "Images", filename)
            };

            return attemptedPaths.FirstOrDefault(File.Exists);
        }

        private static string BuildMissingSpriteMessage(string filename, string[] attemptedPaths)
        {
            var builder = new StringBuilder();
            builder.Append("Image file not found: '").Append(filename)
                .Append("'. Checked paths: ");

            if (attemptedPaths == null || attemptedPaths.Length == 0)
                builder.Append("(none)");
            else
                builder.Append(string.Join(", ", attemptedPaths.Select(path => "'" + path + "'")));

            return builder.ToString();
        }

        private static string BuildMissingEmbeddedSpriteMessage(string filename, Assembly assembly)
        {
            var assemblyName = assembly?.GetName().Name ?? "<unknown>";
            var normalizedName = string.IsNullOrWhiteSpace(filename)
                ? string.Empty
                : filename.Trim().Replace('/', '.').Replace('\\', '.');

            return "Embedded image not found: '" + filename + "' in assembly '" + assemblyName +
                   "'. Checked embedded resource name '" + normalizedName +
                   "' and assembly resource suffix matches. Check capitalization, file extension, and build action.";
        }
    }
}