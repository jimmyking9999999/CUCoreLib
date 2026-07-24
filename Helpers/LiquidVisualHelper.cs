using System;
using System.IO;
using System.Reflection;
using CUCoreLib.ContentReload;
using UnityEngine;

namespace CUCoreLib.Helpers
{
    public static class LiquidVisualHelper
    {
        private static readonly string[] FallbackLiquidShaderNames =
        {
            "Legacy Shaders/Particles/Alpha Blended",
            "Particles/Standard Unlit",
            "Sprites/Default",
            "Unlit/Transparent"
        };

        public static Texture2D LoadEmbeddedTexture(string resourcePath, Assembly sourceAssembly = null,
            FilterMode filterMode = FilterMode.Point, TextureWrapMode wrapMode = TextureWrapMode.Clamp)
        {
            var resolvedAssembly = ResolveSourceAssembly(sourceAssembly);
            using (var stream = AssetLoader.LoadEmbeddedStream(resourcePath, resolvedAssembly, "texture"))
            {
                return stream == null
                    ? null
                    : CreateTextureFromBytes(ReadAllBytes(stream), Path.GetFileNameWithoutExtension(resourcePath),
                        filterMode, wrapMode);
            }
        }

        public static Texture2D LoadTextureFromFile(string filePath, FilterMode filterMode = FilterMode.Point,
            TextureWrapMode wrapMode = TextureWrapMode.Clamp)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                CUCoreLibPlugin.Log?.LogWarning("Liquid texture file not found: '" + filePath + "'.");
                return null;
            }

            return CreateTextureFromBytes(File.ReadAllBytes(filePath), Path.GetFileNameWithoutExtension(filePath),
                filterMode, wrapMode);
        }

        public static Material CreateLiquidMaterial(Texture2D texture, Material baseMaterial = null,
            string shaderName = null)
        {
            var material = baseMaterial != null
                ? new Material(baseMaterial)
                : CreateMaterialFromShader(shaderName) ?? CreateMaterialFromFallbackShaders();

            if (material == null)
            {
                CUCoreLibPlugin.Log?.LogWarning(
                    "Could not create a liquid visual material because no valid shader was available.");
                return null;
            }

            if (texture != null)
            {
                if (material.HasProperty("_MainTex")) material.mainTexture = texture;
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            return material;
        }

        public static Material CreateLiquidMaterialFromEmbeddedTexture(string resourcePath, Material baseMaterial = null,
            string shaderName = null, Assembly sourceAssembly = null, FilterMode filterMode = FilterMode.Point,
            TextureWrapMode wrapMode = TextureWrapMode.Clamp)
        {
            var texture = LoadEmbeddedTexture(resourcePath, sourceAssembly, filterMode, wrapMode);
            return CreateLiquidMaterial(texture, baseMaterial, shaderName);
        }

        public static Material CreateLiquidMaterialFromFile(string filePath, Material baseMaterial = null,
            string shaderName = null, FilterMode filterMode = FilterMode.Point,
            TextureWrapMode wrapMode = TextureWrapMode.Clamp)
        {
            var texture = LoadTextureFromFile(filePath, filterMode, wrapMode);
            return CreateLiquidMaterial(texture, baseMaterial, shaderName);
        }

        private static Assembly ResolveSourceAssembly(Assembly sourceAssembly)
        {
            return sourceAssembly ?? ContentReloadSession.GetSourceAssemblyOverride() ?? Assembly.GetCallingAssembly();
        }

        private static Material CreateMaterialFromShader(string shaderName)
        {
            if (string.IsNullOrWhiteSpace(shaderName)) return null;

            var shader = Shader.Find(shaderName);
            if (shader != null) return new Material(shader);

            CUCoreLibPlugin.Log?.LogWarning("Liquid visual shader not found: '" + shaderName + "'.");
            return null;
        }

        private static Material CreateMaterialFromFallbackShaders()
        {
            foreach (var shaderName in FallbackLiquidShaderNames)
            {
                var shader = Shader.Find(shaderName);
                if (shader != null) return new Material(shader);
            }

            return null;
        }

        private static Texture2D CreateTextureFromBytes(byte[] data, string textureName, FilterMode filterMode,
            TextureWrapMode wrapMode)
        {
            if (data == null || data.Length == 0) return null;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = filterMode,
                wrapMode = wrapMode,
                name = string.IsNullOrWhiteSpace(textureName) ? "CUCoreLibLiquidTexture" : textureName
            };

            if (texture.LoadImage(data)) return texture;

            UnityEngine.Object.Destroy(texture);
            return null;
        }

        private static byte[] ReadAllBytes(Stream stream)
        {
            if (stream == null) return null;

            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }
    }
}
