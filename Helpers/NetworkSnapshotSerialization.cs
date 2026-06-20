using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Helpers
{
    internal static class NetworkSnapshotSerialization
    {
        internal static JObject WriteSprite(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return null;

            var png = WriteTexture(sprite.texture);
            if (png == null || png.Length == 0) return null;

            return new JObject
            {
                ["name"] = sprite.name ?? string.Empty,
                ["ppu"] = sprite.pixelsPerUnit,
                ["data"] = Convert.ToBase64String(png)
            };
        }

        internal static Sprite ReadSprite(JToken token)
        {
            var obj = token as JObject;
            if (obj == null) return null;

            var encoded = obj.Value<string>("data");
            if (string.IsNullOrWhiteSpace(encoded)) return null;

            if (!TryDecodeBase64(encoded, out var data) || data == null || data.Length == 0) return null;

            var ppu = obj.Value<float?>("ppu") ?? AssetLoader.PPU_WORLD;
            var sprite = AssetLoader.LoadSpriteFromBytes(data, ppu);
            if (sprite != null)
            {
                var name = obj.Value<string>("name");
                if (!string.IsNullOrWhiteSpace(name)) sprite.name = name;
            }

            return sprite;
        }

        internal static JObject WriteColor(Color color)
        {
            return new JObject
            {
                ["r"] = color.r,
                ["g"] = color.g,
                ["b"] = color.b,
                ["a"] = color.a
            };
        }

        internal static Color ReadColor(JToken token, Color fallback)
        {
            var obj = token as JObject;
            if (obj == null) return fallback;

            return new Color(
                obj.Value<float?>("r") ?? fallback.r,
                obj.Value<float?>("g") ?? fallback.g,
                obj.Value<float?>("b") ?? fallback.b,
                obj.Value<float?>("a") ?? fallback.a);
        }

        internal static JArray WriteLiquidStacks(IEnumerable<LiquidStack> stacks)
        {
            if (stacks == null) return null;

            var array = new JArray();
            foreach (var stack in stacks)
            {
                if (stack == null || string.IsNullOrWhiteSpace(stack.liquidId)) continue;

                array.Add(new JObject
                {
                    ["liquidId"] = stack.liquidId,
                    ["amount"] = stack.amount
                });
            }

            return array;
        }

        internal static List<LiquidStack> ReadLiquidStacks(JToken token)
        {
            var array = token as JArray;
            var stacks = new List<LiquidStack>();
            if (array == null) return stacks;

            foreach (var entry in array)
            {
                var obj = entry as JObject;
                if (obj == null) continue;

                var liquidId = obj.Value<string>("liquidId");
                if (string.IsNullOrWhiteSpace(liquidId)) continue;

                stacks.Add(new LiquidStack(liquidId, obj.Value<float?>("amount") ?? 0f));
            }

            return stacks;
        }

        internal static JArray WriteCraftingQualities(IEnumerable<CraftingQuality> qualities)
        {
            if (qualities == null) return null;

            var array = new JArray();
            foreach (var quality in qualities)
            {
                if (quality == null || string.IsNullOrWhiteSpace(quality.id)) continue;

                array.Add(new JObject
                {
                    ["id"] = quality.id,
                    ["amount"] = quality.amount
                });
            }

            return array;
        }

        internal static List<CraftingQuality> ReadCraftingQualities(JToken token)
        {
            var array = token as JArray;
            var qualities = new List<CraftingQuality>();
            if (array == null) return qualities;

            foreach (var entry in array)
            {
                var obj = entry as JObject;
                if (obj == null) continue;

                var id = obj.Value<string>("id");
                if (string.IsNullOrWhiteSpace(id)) continue;

                qualities.Add(new CraftingQuality(id, obj.Value<float?>("amount") ?? 1f));
            }

            return qualities;
        }

        internal static JArray WriteTypeNames(IEnumerable<Type> types)
        {
            if (types == null) return null;

            var array = new JArray();
            foreach (var type in types)
            {
                if (type == null) continue;

                array.Add(type.AssemblyQualifiedName ?? type.FullName);
            }

            return array;
        }

        internal static Type[] ReadTypeNames(JToken token)
        {
            var array = token as JArray;
            if (array == null) return null;

            var types = new List<Type>();
            foreach (var entry in array)
            {
                var typeName = entry?.Value<string>();
                if (string.IsNullOrWhiteSpace(typeName)) continue;

                var resolved = Type.GetType(typeName, false);
                if (resolved != null) types.Add(resolved);
            }

            return types.ToArray();
        }

        internal static string WriteStringOrEmpty(string value)
        {
            return value ?? string.Empty;
        }

        private static byte[] WriteTexture(Texture2D texture)
        {
            if (texture == null) return null;

            try
            {
                if (texture.isReadable) return texture.EncodeToPNG();
            }
            catch
            {
            }

            RenderTexture renderTexture = null;
            var previous = RenderTexture.active;
            try
            {
                renderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                    RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
                Graphics.Blit(texture, renderTexture);
                RenderTexture.active = renderTexture;

                var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readable.Apply();
                return readable.EncodeToPNG();
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (renderTexture != null) RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static bool TryDecodeBase64(string encoded, out byte[] data)
        {
            try
            {
                data = Convert.FromBase64String(encoded);
                return true;
            }
            catch
            {
                data = null;
                return false;
            }
        }
    }
}