using System;
using System.Collections.Generic;
using CUCoreLib.Data;
using CUCoreLib.Patches;
using CUCoreLib.Registries;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CUCoreLib.Helpers
{
    public static class CustomInstantiate
    {
        private const byte ColliderAlphaThreshold = 8;

        private static readonly Dictionary<string, GameObject> _templateCache =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        // Shared buffer avoids per-shape allocations (physics)
        private static readonly List<Vector2> SharedPhysicsShapeBuffer = new List<Vector2>();

        internal static void ClearTemplateCache(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            id = SpawnIdHelpers.NormalizeSpawnId(id);
            _templateCache.Remove(id);
        }

        public static GameObject InstantiateReturn(string id, Vector3 position, Quaternion rotation,
            float? condition = null)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            id = SpawnIdHelpers.NormalizeSpawnId(id);

            var prefab = ResolvePrefab(id);
            return prefab == null 
                ? null 
                : PrepareInstantiatedObject(Object.Instantiate(prefab, position, rotation), condition);
        }

        public static GameObject ResolvePrefab(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            id = SpawnIdHelpers.NormalizeSpawnId(id);

            ResourceCache.TryInitialize();

            if (BuildingEntityRegistry.IsRegistered(id)) return BuildingEntityRegistry.GetOrCreatePrefab(id);

            if (ResourceCache.AllPrefabs.TryGetValue(id, out var cached)) return cached;

            var vanilla = Resources.Load<GameObject>(id);
            return vanilla != null
                ? vanilla
                : GetOrCreateTemplate(id);
        }

        /// <summary>
        /// Resolves a saved or externally-restored item resource.  Vanilla resources are returned unchanged;
        /// CUCoreLib runtime item templates are returned when the ID is registered by a dependent mod.
        /// </summary>
        public static Object ResolveSavedResource(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            var vanilla = Resources.Load(id);
            return vanilla != null ? vanilla : GetOrCreateTemplate(id);
        }

        internal static GameObject PrepareInstantiatedObject(GameObject obj, float? condition = null)
        {
            if (obj == null) return null;

            obj.SetActive(true);
            var item = obj.GetComponent<Item>();
            if (item != null)
            {
                // Matches vanilla's throw path so custom sprite colliders do not tunnel through terrain.
                if (ItemRegistry.TryGetCustomInfo(item, out _) && item.rb != null)
                    item.rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                ItemRegistryPatches.MarkPendingBatteryInitialization(obj);
            }

            if (!condition.HasValue) return obj;
            if (item) item.condition = condition.Value;

            return obj;
        }

        public static GameObject GetOrCreateTemplate(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            id = SpawnIdHelpers.NormalizeSpawnId(id);

            if (_templateCache.TryGetValue(id, out var cachedTemplate)) return cachedTemplate;

            if (!ItemRegistry.RegisteredItems.TryGetValue(id, out var itemInfo)) return null;

            var template = CreateTemplate(id, itemInfo);
            if (template == null) return null;

            _templateCache[id] = template;
            return template;
        }

        private static GameObject CreateTemplate(string id, CustomItemInfo info)
        {
            var baseId = ChooseTemplateId(info);

            var basePrefab = Resources.Load<GameObject>(baseId);

            var obj = Object.Instantiate(basePrefab);
            obj.SetActive(false);
            obj.name = id;
            Object.DontDestroyOnLoad(obj);

            switch (baseId)
            {
                // Flashlights are special battery items, so they need a bit more handling
                case "flashlight" when info.Battery == null && info.Light == null:
                case "flashlight" when info.Battery != null && info.Light == null:
                {
                    var light = obj.GetComponentInChildren<Light2D>();
                    if (light) DestroyWithoutImmediate(light.gameObject);
                    break;
                }
                default:
                {
                    if (info.Light != null)
                    {
                        EnsureLightItemHasLight(obj, info.Light);
                    }

                    break;
                }
            }

            var item = obj.GetComponent<Item>();
            if (item)
            {
                item.id = id;
                // Container capacity must live on the template itself: saved-game loading calls
                // Container.LoadItem in the same frame as instantiation, before Item.Start runs.
                ItemRegistryPatches.ApplyContainerProperties(item, info);
            }

            if (item != null && info.wearable && obj.GetComponent<Wearable>() == null)
            {
                obj.AddComponent<Wearable>();
            }

            if (item != null && info.Gun != null)
                ItemRegistryPatches.ApplyGunProperties(item, info);

            var waterContainer = obj.GetComponent<WaterContainerItem>();
            if (waterContainer != null) ItemRegistryPatches.ApplyLiquidMask(waterContainer, info);

            var sr = obj.GetComponent<SpriteRenderer>();
            var icon = ItemRegistry.GetIcon(info);
            if (sr && icon != null)
            {
                sr.sprite = icon;
                ApplySpriteCollision(obj, icon);
            }

            if (item == null) return obj;
            if (info.Battery != null)
            {
                var batteryItem = obj.GetComponent<BatteryItem>();
                var createdBattery = batteryItem == null;   // not use
                if (batteryItem == null) batteryItem = obj.AddComponent<BatteryItem>();

                ItemRegistryPatches.ApplyBatteryProperties(item, batteryItem, info, true);
            }

            ItemRegistryPatches.ApplyCustomScale(item, info);

            return obj;
        }

        internal static void ApplySpriteCollision(GameObject obj, Sprite sprite)
        {
            if (obj == null || sprite == null) return;

            if (!TryGetTrimmedColliderData(sprite, out var trimmedCollider))
            {
                trimmedCollider = CreateFullSpriteColliderData(sprite);
            }

            // Preserve current collider settings
            var existingCollider = obj.GetComponent<Collider2D>();
            
            // For some reason the first time items are spawned existingCollider gives us the incorrect collider data
            // sloppiest fix ever but without this items just dont have collision on first spawn
            if (TryApplyPolygonCollider(obj, sprite, null, trimmedCollider)) return;
            ApplyBoxCollider(obj, null, trimmedCollider);
        }

        private static bool TryApplyPolygonCollider(GameObject obj, Sprite sprite, Collider2D existingCollider,
            TrimmedColliderData trimmedCollider)
        {
            var shapeCount = sprite.GetPhysicsShapeCount();
            if (shapeCount <= 0) return false;

            var polygon = obj.GetComponent<PolygonCollider2D>();
            if (polygon == null) polygon = obj.AddComponent<PolygonCollider2D>();

            CopyColliderSettings(existingCollider, polygon);

            polygon.pathCount = shapeCount;
            for (var i = 0; i < shapeCount; i++)
            {
                SharedPhysicsShapeBuffer.Clear();
                sprite.GetPhysicsShape(i, SharedPhysicsShapeBuffer);
                for (var j = 0; j < SharedPhysicsShapeBuffer.Count; j++)
                {
                    SharedPhysicsShapeBuffer[j] = ClampPointToTrimmedBounds(SharedPhysicsShapeBuffer[j], trimmedCollider);
                }

                polygon.SetPath(i, SharedPhysicsShapeBuffer);
            }

            polygon.offset = Vector2.zero;
            RemoveOtherColliders(obj, polygon);
            return true;
        }

        private static void ApplyBoxCollider(GameObject obj, Collider2D existingCollider, TrimmedColliderData trimmedCollider)
        {
            var box = obj.GetComponent<BoxCollider2D>();
            if (box == null) box = obj.AddComponent<BoxCollider2D>();

            CopyColliderSettings(existingCollider, box);
            box.size = trimmedCollider.Size;
            box.offset = trimmedCollider.Center;
            RemoveOtherColliders(obj, box);
        }

        private static bool TryGetTrimmedColliderData(Sprite sprite, out TrimmedColliderData trimmedCollider)
        {
            trimmedCollider = default;
            if (sprite == null || sprite.texture == null) return false;

            var spriteRect = sprite.rect;
            var texture = sprite.texture;
            var startX = Mathf.RoundToInt(spriteRect.x);
            var startY = Mathf.RoundToInt(spriteRect.y);
            var width = Mathf.RoundToInt(spriteRect.width);
            var height = Mathf.RoundToInt(spriteRect.height);
            if (width <= 0 || height <= 0) return false;

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch
            {
                return false;
            }

            if (pixels == null || pixels.Length == 0) return false;

            var minX = width;
            var minY = height;
            var maxX = -1;
            var maxY = -1;

            for (var localY = 0; localY < height; localY++)
            {
                var textureY = startY + localY;
                if (textureY < 0 || textureY >= texture.height) continue;

                var rowIndex = textureY * texture.width;
                for (var localX = 0; localX < width; localX++)
                {
                    var textureX = startX + localX;
                    if (textureX < 0 || textureX >= texture.width) continue;

                    if (pixels[rowIndex + textureX].a < ColliderAlphaThreshold) continue;

                    if (localX < minX) minX = localX;
                    if (localY < minY) minY = localY;
                    if (localX > maxX) maxX = localX;
                    if (localY > maxY) maxY = localY;
                }
            }

            if (maxX < minX || maxY < minY) return false;

            trimmedCollider = CreateTrimmedColliderData(sprite, minX, minY, maxX, maxY);
            return true;
        }

        private static TrimmedColliderData CreateTrimmedColliderData(Sprite sprite, int minPixelX, int minPixelY,
            int maxPixelX, int maxPixelY)
        {
            var pixelsPerUnit = sprite.pixelsPerUnit > 0f ? sprite.pixelsPerUnit : 1f;
            var pivotInPixels = sprite.pivot;

            var left = (minPixelX - pivotInPixels.x) / pixelsPerUnit;
            var right = (maxPixelX + 1 - pivotInPixels.x) / pixelsPerUnit;
            var bottom = (minPixelY - pivotInPixels.y) / pixelsPerUnit;
            var top = (maxPixelY + 1 - pivotInPixels.y) / pixelsPerUnit;

            return new TrimmedColliderData(
                new Vector2(right - left, top - bottom),
                new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f),
                left,
                right,
                bottom,
                top);
        }

        private static TrimmedColliderData CreateFullSpriteColliderData(Sprite sprite)
        {
            var bounds = sprite.bounds;
            var extents = bounds.extents;
            return new TrimmedColliderData(
                bounds.size,
                bounds.center,
                bounds.center.x - extents.x,
                bounds.center.x + extents.x,
                bounds.center.y - extents.y,
                bounds.center.y + extents.y);
        }

        private static Vector2 ClampPointToTrimmedBounds(Vector2 point, TrimmedColliderData trimmedCollider)
        {
            return new Vector2(
                Mathf.Clamp(point.x, trimmedCollider.MinX, trimmedCollider.MaxX),
                Mathf.Clamp(point.y, trimmedCollider.MinY, trimmedCollider.MaxY));
        }

        private static void CopyColliderSettings(Collider2D source, Collider2D target)
        {
            if (target == null) return;

            if (source == null || source == target) return;
            target.isTrigger = source.isTrigger;
            target.sharedMaterial = source.sharedMaterial;
            target.usedByEffector = source.usedByEffector;
            target.usedByComposite = source.usedByComposite;
            target.enabled = source.enabled;
        }

        private static void RemoveOtherColliders(GameObject obj, Collider2D keep)
        {
            if (obj == null || keep == null) return;

            var colliders = obj.GetComponents<Collider2D>();
            foreach (var collider in colliders)
                if (collider != null && collider != keep)
                    DestroyWithoutImmediate(collider);
        }

        private static void DestroyWithoutImmediate(Object target)
        {
            if (target == null) return;

            // Disabled objects/components do not affect templates instantiated before Unity completes
            // the deferred destroy, and Object.Destroy is permitted from physics callbacks.
            if (target is GameObject gameObject)
                gameObject.SetActive(false);
            else if (target is Behaviour behaviour)
                behaviour.enabled = false;

            Object.Destroy(target);
        }

        private static void EnsureLightItemHasLight(GameObject obj, LightProperties properties)
        {
            if (properties == null) return;

            LightItem lightItem = null;
            if (properties.AddLightItem)
            {
                lightItem = obj.GetComponent<LightItem>();
                if (lightItem == null) lightItem = obj.AddComponent<LightItem>();
            }

            var light = obj.GetComponentInChildren<Light2D>();
            if (light == null)
            {
                var lightObject = new GameObject("CustomLight", typeof(Light2D));
                lightObject.transform.SetParent(obj.transform);
                lightObject.transform.localPosition = properties.Offset;
                lightObject.transform.localRotation = Quaternion.identity;
                lightObject.transform.localScale = Vector3.one;
                light = lightObject.GetComponent<Light2D>();
            }

            ItemRegistryPatches.ApplyLightProperties(light, properties);

            if (lightItem == null) return;
            lightItem.light = light;
            lightItem.shouldEnable = true;
        }

        private static string ChooseTemplateId(CustomItemInfo info)
        {
            // Shhh...
            if (info == null) return "bandage";
            if (info.Gun != null) return "pistol";
            if (info.Container != null) return "smallpack";
            if (info.Battery != null) return "flashlight";
            if (info.Light != null) return "bandage";
            if (info.capacity > 0f || (info.defaultContents != null && info.defaultContents.Count > 0))
                return "waterbottle";
            if (info.category == "water" || info.category == "liquid") return "waterbottle";

            return "bandage";
        }

        private static Light2D.LightType ToLight2DType(CustomLightType type)
        {
            return (Light2D.LightType)(int)type;
        }

        private readonly struct TrimmedColliderData
        {
            public readonly Vector2 Size;
            public readonly Vector2 Center;
            public readonly float MinX;
            public readonly float MaxX;
            public readonly float MinY;
            public readonly float MaxY;

            public TrimmedColliderData(Vector2 size, Vector2 center, float minX, float maxX, float minY, float maxY)
            {
                Size = size;
                Center = center;
                MinX = minX;
                MaxX = maxX;
                MinY = minY;
                MaxY = maxY;
            }
        }
    }
}
