using System;
using System.Collections.Generic;
using CUCoreLib.Data;
using CUCoreLib.Patches;
using CUCoreLib.Registries;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CUCoreLib.Helpers;

public static class CustomInstantiate
{
    private static readonly Dictionary<string, GameObject> _templateCache = new(StringComparer.OrdinalIgnoreCase);

    // Shared buffer avoids per-shape allocations (physics)
    private static readonly List<Vector2> SharedPhysicsShapeBuffer = new();

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

    private static GameObject PrepareInstantiatedObject(GameObject obj, float? condition)
    {
        if (obj == null) return null;

        obj.SetActive(true);
        if (obj.GetComponent<Item>() != null) ItemRegistryPatches.MarkPendingBatteryInitialization(obj);

        if (!condition.HasValue) return obj;
        var itemComp = obj.GetComponent<Item>();
        if (itemComp) itemComp.condition = condition.Value;

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
                if (light) Object.DestroyImmediate(light.gameObject);
                break;
            }
            default:
            {
                if (info.Light != null) EnsureLightItemHasLight(obj, info.Light);

                break;
            }
        }

        var item = obj.GetComponent<Item>();
        if (item) item.id = id;

        var waterContainer = obj.GetComponent<WaterContainerItem>();
        if (waterContainer != null) waterContainer.fillSprite = info.LiquidMask;

        var sr = obj.GetComponent<SpriteRenderer>();
        if (sr && info.Icon != null)
        {
            sr.sprite = info.Icon;
            ApplySpriteCollision(obj, info.Icon);
        }

        if (item == null) return obj;
        if (info.Battery != null)
        {
            var batteryItem = obj.GetComponent<BatteryItem>();
            var createdBattery = batteryItem == null; // not use
            if (batteryItem == null) batteryItem = obj.AddComponent<BatteryItem>();

            ItemRegistryPatches.ApplyBatteryProperties(item, batteryItem, info, true, true);
        }

        ItemRegistryPatches.ApplyCustomScale(item, info);

        return obj;
    }

    internal static void ApplySpriteCollision(GameObject obj, Sprite sprite)
    {
        if (obj == null || sprite == null) return;

        // Preserve current collider settings
        var existingCollider = obj.GetComponent<Collider2D>();
        if (TryApplyPolygonCollider(obj, sprite, existingCollider)) return;

        ApplyBoxCollider(obj, sprite, existingCollider);
    }

    private static bool TryApplyPolygonCollider(GameObject obj, Sprite sprite, Collider2D existingCollider)
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
            polygon.SetPath(i, SharedPhysicsShapeBuffer);
        }

        polygon.offset = Vector2.zero;
        RemoveOtherColliders(obj, polygon);
        return true;
    }

    private static void ApplyBoxCollider(GameObject obj, Sprite sprite, Collider2D existingCollider)
    {
        var box = obj.GetComponent<BoxCollider2D>();
        if (box == null) box = obj.AddComponent<BoxCollider2D>();

        CopyColliderSettings(existingCollider, box);
        box.size = sprite.bounds.size;
        box.offset = sprite.bounds.center;
        RemoveOtherColliders(obj, box);
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
                Object.DestroyImmediate(collider);
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

        light.transform.localPosition = properties.Offset;
        light.lightType = ToLight2DType(properties.LightType);
        light.intensity = properties.Intensity;
        light.color = properties.Color;
        light.pointLightOuterRadius = properties.PointLightOuterRadius;
        light.pointLightInnerRadius = properties.PointLightInnerRadius;

        if (lightItem == null) return;
        lightItem.light = light;
        lightItem.shouldEnable = true;
    }

    private static string ChooseTemplateId(CustomItemInfo info)
    {
        // Shhh...
        if (info == null) return "bandage";
        if (info.Container != null) return "smallpack";
        if (info.Battery != null || info.Light != null) return "flashlight";
        if (info.capacity > 0f || (info.defaultContents != null && info.defaultContents.Count > 0))
            return "waterbottle";
        if (info.category == "water" || info.category == "liquid") return "waterbottle";

        return "bandage";
    }

    private static Light2D.LightType ToLight2DType(CustomLightType type)
    {
        return (Light2D.LightType)(int)type;
    }
}