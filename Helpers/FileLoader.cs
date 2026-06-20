using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
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
            return AssetLoader.LoadEmbeddedText(filename, Assembly.GetExecutingAssembly());
        }


        public static Sprite LoadSpriteFromFile(string filename)
        {
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
            var pluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            // pluginPath maybe are null?
            var imagePath = Path.Combine(pluginPath, "Images", filename);

            if (!File.Exists(imagePath))
            {
                imagePath = Path.Combine(pluginPath, filename);
                if (!File.Exists(imagePath))
                {
                    imagePath = Path.Combine(pluginPath, "Images", "Images", filename);
                    if (!File.Exists(imagePath))
                    {
                        // Debug.LogError($"You didn't download the image: {filename}");
                        Debug.LogError($"Image file not found: {filename}");
                        return null;
                    }
                }
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
            return AssetLoader.LoadEmbeddedAudio(fileName, Assembly.GetExecutingAssembly());
        }

        public static Sprite LoadEmbeddedSprite(string filename)
        {
            return LoadEmbeddedSprite(filename, 100, FilterMode.Point, 1, 1);
        }

        public static Sprite LoadEmbeddedSprite(string filename, float ppu, FilterMode filterMode)
        {
            return LoadEmbeddedSprite(filename, ppu, filterMode, 1, 1);
        }

        public static Sprite LoadEmbeddedSprite(string filename, float ppu, FilterMode filterMode, int widthMultiplier,
            int heightMultiplier)
        {
            var asm = Assembly.GetExecutingAssembly();
            var newFilename = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(filename));
            if (newFilename == null)
            {
                Logger.LogError(
                    $"Image by the name of {filename} does not exist. Check capitalization and file extension");
                return null;
            }

            // to read file data from manifest resource
            var stream = asm.GetManifestResourceStream(newFilename);
            // maybe System.NullReferenceException
            var fileData = new byte[stream.Length];
            stream.Read(fileData, 0, fileData.Length);

            var texture = new Texture2D(2, 2, TextureFormat.RGBAHalf, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = filterMode,
                name = newFilename
            };
            texture.LoadImage(fileData);
            texture.Apply();
            if (widthMultiplier > 1 || heightMultiplier > 1)
                texture = ModifyTextures.ResizeTexture(texture, widthMultiplier, heightMultiplier);

            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                ppu
            );
            sprite.name = newFilename;

            return sprite;
        }
    }
}