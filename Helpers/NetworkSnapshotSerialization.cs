using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CUCoreLib.Helpers
{
    internal static class NetworkSnapshotSerialization
    {
        private static readonly Dictionary<SpritePayloadKey, string> SpritePayloadCache =
            new Dictionary<SpritePayloadKey, string>();

        private static int _spriteDedupeDepth;

        // Wraps a snapshot capture so sprites sliced from one shared texture (tile
        // variants, animation frames) encode their sheet region once and reuse the
        // base64 payload for every entry. Scopes may nest; the cache lives exactly
        // as long as the outermost scope so no Texture2D references are held between
        // captures.
        internal static IDisposable BeginSpriteDedupeScope()
        {
            if (_spriteDedupeDepth++ == 0) SpritePayloadCache.Clear();
            return new SpriteDedupeScope();
        }

        internal static JObject WriteSprite(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return null;

            var key = SpritePayloadKey.From(sprite);
            if (_spriteDedupeDepth > 0 && SpritePayloadCache.TryGetValue(key, out var cachedData))
                return BuildSpritePayload(sprite, cachedData);

            var png = WriteTextureRegion(sprite);
            if (png == null || png.Length == 0) return null;

            var data = Convert.ToBase64String(png);
            if (_spriteDedupeDepth > 0) SpritePayloadCache[key] = data;

            return BuildSpritePayload(sprite, data);
        }

        private static JObject BuildSpritePayload(Sprite sprite, string data)
        {
            // Sprite pivots are normalized against the sprite rect, so store them
            // normalized too; readers rebuild the sprite from the (possibly cropped)
            // PNG with the same normalized pivot.
            var rectSize = sprite.rect.size;
            var pivot = rectSize.x > 0f && rectSize.y > 0f
                ? new Vector2(sprite.pivot.x / rectSize.x, sprite.pivot.y / rectSize.y)
                : new Vector2(0.5f, 0.5f);

            return new JObject
            {
                ["name"] = sprite.name,
                ["ppu"] = sprite.pixelsPerUnit,
                ["px"] = pivot.x,
                ["py"] = pivot.y,
                ["data"] = data
            };
        }

        internal static Sprite ReadSprite(JToken token)
        {
            if (!(token is JObject obj)) return null;

            var encoded = obj.Value<string>("data");
            if (string.IsNullOrWhiteSpace(encoded)) return null;

            if (!TryDecodeBase64(encoded, out var data) || data == null || data.Length == 0) return null;

            var ppu = obj.Value<float?>("ppu") ?? AssetLoader.PPU_WORLD;
            var pivot = new Vector2(obj.Value<float?>("px") ?? 0.5f, obj.Value<float?>("py") ?? 0.5f);
            var sprite = AssetLoader.LoadSpriteFromBytes(data, ppu, pivot);
            if (sprite == null) return sprite;
            var name = obj.Value<string>("name");
            if (!string.IsNullOrWhiteSpace(name)) sprite.name = name;

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
            if (!(token is JObject obj)) return fallback;

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

            stacks.AddRange(from obj
                    in array.OfType<JObject>()
                let liquidId = obj.Value<string>("liquidId")
                where !string.IsNullOrWhiteSpace(liquidId)
                select new LiquidStack(liquidId, obj.Value<float?>("amount") ?? 0f));

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

            qualities.AddRange(from obj
                    in array.OfType<JObject>()
                let id = obj.Value<string>("id")
                where !string.IsNullOrWhiteSpace(id)
                select new CraftingQuality(id, obj.Value<float?>("amount") ?? 1f));

            return qualities;
        }

        internal static JObject WriteSpriteDictionary(IReadOnlyDictionary<string, Sprite> sprites)
        {
            if (sprites == null || sprites.Count == 0) return null;

            var obj = new JObject();
            foreach (var entry in sprites)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null) continue;

                var sprite = WriteSprite(entry.Value);
                if (sprite != null) obj[entry.Key] = sprite;
            }

            return obj;
        }

        internal static Dictionary<string, Sprite> ReadSpriteDictionary(JToken token)
        {
            var obj = token as JObject;
            var sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            if (obj == null) return sprites;

            foreach (var property in obj.Properties())
            {
                if (string.IsNullOrWhiteSpace(property.Name)) continue;

                var sprite = ReadSprite(property.Value);
                if (sprite == null) continue;

                sprites[property.Name] = sprite;
            }

            return sprites;
        }

        internal static JObject WriteVector2Dictionary(IReadOnlyDictionary<string, Vector2> offsets)
        {
            if (offsets == null || offsets.Count == 0) return null;

            var obj = new JObject();
            foreach (var entry in offsets)
            {
                if (string.IsNullOrWhiteSpace(entry.Key)) continue;

                obj[entry.Key] = new JObject
                {
                    ["x"] = entry.Value.x,
                    ["y"] = entry.Value.y
                };
            }

            return obj;
        }

        internal static Dictionary<string, Vector2> ReadVector2Dictionary(JToken token)
        {
            var obj = token as JObject;
            var offsets = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
            if (obj == null) return offsets;

            foreach (var property in obj.Properties())
            {
                if (string.IsNullOrWhiteSpace(property.Name) || !(property.Value is JObject vector)) continue;

                offsets[property.Name] = new Vector2(
                    vector.Value<float?>("x") ?? 0f,
                    vector.Value<float?>("y") ?? 0f);
            }

            return offsets;
        }

        internal static JArray WriteTypeNames(IEnumerable<Type> types)
        {
            if (types == null) return null;

            var array = new JArray();
            foreach (var type in types)
            {
                if (type == null) continue;

                // maybe null
                // 111
                array.Add(type.AssemblyQualifiedName ?? type.FullName);
            }

            return array;
        }

        internal static Type[] ReadTypeNames(JToken token)
        {
            if (!(token is JArray array)) return null;

            return (from entry in array
                    select entry?
                        .Value<string>()
                    into typeName
                    where !string
                        .IsNullOrWhiteSpace(typeName)
                    select Type
                        .GetType(typeName, false)
                    into resolved
                    where resolved != null
                    select resolved)
                .ToArray();
        }

        internal static string WriteStringOrEmpty(string value)
        {
            return value ?? string.Empty;
        }

        private static byte[] WriteTextureRegion(Sprite sprite)
        {
            var texture = sprite.texture;
            if (texture == null) return null;

            // Sprites are frequently sliced from shared sheets, so encode only the
            // sprite's own rect; encoding sprite.texture would re-embed the whole
            // sheet once per slice. A full-texture or unusable rect falls back to
            // the legacy whole-texture path.
            var rect = sprite.rect;
            var x = Mathf.Clamp(Mathf.FloorToInt(rect.x), 0, texture.width);
            var y = Mathf.Clamp(Mathf.FloorToInt(rect.y), 0, texture.height);
            var width = Mathf.Clamp(Mathf.RoundToInt(rect.width), 0, texture.width - x);
            var height = Mathf.Clamp(Mathf.RoundToInt(rect.height), 0, texture.height - y);
            if (width <= 0 || height <= 0 || (x == 0 && y == 0 && width == texture.width && height == texture.height))
                return WriteTexture(texture);

            try
            {
                if (texture.isReadable)
                {
                    var cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    try
                    {
                        cropped.SetPixels(texture.GetPixels(x, y, width, height));
                        cropped.Apply();
                        return cropped.EncodeToPNG();
                    }
                    finally
                    {
                        Object.DestroyImmediate(cropped);
                    }
                }
            }
            catch
            {
                // fall through to the render-texture path
            }

            RenderTexture renderTexture = null;
            var previous = RenderTexture.active;
            try
            {
                renderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                    RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
                Graphics.Blit(texture, renderTexture);
                RenderTexture.active = renderTexture;

                var readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    readable.ReadPixels(new Rect(x, y, width, height), 0, 0);
                    readable.Apply();
                    return readable.EncodeToPNG();
                }
                finally
                {
                    Object.DestroyImmediate(readable);
                }
            }
            catch
            {
                // Last resort: the un-cropped texture still round-trips, just larger.
                return WriteTexture(texture);
            }
            finally
            {
                RenderTexture.active = previous;
                if (renderTexture != null) RenderTexture.ReleaseTemporary(renderTexture);
            }
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
                // ignored
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

        private sealed class SpriteDedupeScope : IDisposable
        {
            public void Dispose()
            {
                if (_spriteDedupeDepth > 0 && --_spriteDedupeDepth == 0) SpritePayloadCache.Clear();
            }
        }

        // Identifies the pixel payload of a sprite: same texture, rect, pixels-per-
        // unit and pivot produce an identical wire payload, so one entry can serve
        // every sprite sharing them. Sprite names are deliberately excluded (they
        // are cosmetic and the first writer wins).
        private readonly struct SpritePayloadKey : IEquatable<SpritePayloadKey>
        {
            private SpritePayloadKey(int textureId, float x, float y, float width, float height, float ppu,
                float pivotX, float pivotY)
            {
                TextureId = textureId;
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Ppu = ppu;
                PivotX = pivotX;
                PivotY = pivotY;
            }

            private int TextureId { get; }
            private float X { get; }
            private float Y { get; }
            private float Width { get; }
            private float Height { get; }
            private float Ppu { get; }
            private float PivotX { get; }
            private float PivotY { get; }

            public static SpritePayloadKey From(Sprite sprite)
            {
                return new SpritePayloadKey(sprite.texture.GetInstanceID(), sprite.rect.x, sprite.rect.y,
                    sprite.rect.width, sprite.rect.height, sprite.pixelsPerUnit,
                    sprite.pivot.x, sprite.pivot.y);
            }

            public bool Equals(SpritePayloadKey other)
            {
                return TextureId == other.TextureId && X == other.X && Y == other.Y &&
                       Width == other.Width && Height == other.Height && Ppu == other.Ppu &&
                       PivotX == other.PivotX && PivotY == other.PivotY;
            }

            public override bool Equals(object obj)
            {
                return obj is SpritePayloadKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = TextureId;
                    hash = (hash * 397) ^ X.GetHashCode();
                    hash = (hash * 397) ^ Y.GetHashCode();
                    hash = (hash * 397) ^ Width.GetHashCode();
                    hash = (hash * 397) ^ Height.GetHashCode();
                    hash = (hash * 397) ^ Ppu.GetHashCode();
                    hash = (hash * 397) ^ PivotX.GetHashCode();
                    hash = (hash * 397) ^ PivotY.GetHashCode();
                    return hash;
                }
            }
        }
    }
}