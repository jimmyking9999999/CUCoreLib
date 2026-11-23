using CUCoreLib.Data;
using CUCoreLib.Patches;
using CUCoreLib.Registries;
using CUCoreLib.Helpers;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CUCoreLib.Saving
{
    internal sealed class BuiltInItemRuntimeSaveProvider : IItemSaveProvider
    {
        public int GetVersion()
        {
            return 2;
        }

        public JToken Capture(Item item, string itemKey)
        {
            if (item == null || !ItemRegistry.TryGetCustomInfo(item, out _)) return null;

            var result = new JObject();
            var customData = ItemRegistry.CaptureRuntimeCustomData(item);
            if (customData != null) result["customData"] = customData;

            if (ItemRegistryPatches.TryGetRuntimeIconOverride(item, out var iconOverride) && iconOverride != null)
                result["iconOverride"] = NetworkSnapshotSerialization.WriteSprite(iconOverride);

            if (ItemRegistryPatches.TryGetRuntimeLightOverride(item, out var lightOverride))
                result["lightOverride"] = WriteLight(lightOverride);

            var lightItem = item.GetComponent<LightItem>();
            if (lightItem != null) result["lightEnabled"] = lightItem.shouldEnable;

            return result.HasValues ? result : null;
        }

        public void Restore(Item item, string itemKey, JToken payload, int version, SaveRestoreContext context)
        {
            if (item == null || !(payload is JObject obj) || !ItemRegistry.TryGetCustomInfo(item, out var def)) return;

            if (obj["customData"] is JObject customData)
                ItemRegistry.RestoreRuntimeCustomData(item, customData);

            if (obj["iconOverride"] is JObject iconOverride)
                ItemRegistryPatches.SetRuntimeIconOverride(item, NetworkSnapshotSerialization.ReadSprite(iconOverride));
            else
                ItemRegistryPatches.SetRuntimeIconOverride(item, null);

            if (obj["lightOverride"] is JObject lightOverride)
                ItemRegistryPatches.SetRuntimeLightOverride(item, def, ReadLight(lightOverride));
            else if (ItemRegistryPatches.TryGetRuntimeLightOverride(item, out _))
                ItemRegistryPatches.SetRuntimeLightOverride(item, def, null);

            var enabledToken = obj["lightEnabled"];
            if (enabledToken == null || enabledToken.Type == JTokenType.Null) return;

            var enabled = enabledToken.Value<bool>();
            var lightItem = item.GetComponent<LightItem>();
            if (lightItem == null)
            {
                context.Defer(delegate
                {
                    ItemRegistryPatches.ApplyCustomItemRuntime(item);
                    ApplyLightState(item, enabled);
                });
                return;
            }

            ApplyLightState(item, enabled);
        }

        private static void ApplyLightState(Item item, bool enabled)
        {
            if (item == null) return;

            var lightItem = item.GetComponent<LightItem>();
            if (lightItem == null) return;

            lightItem.shouldEnable = enabled;
            if (lightItem.light == null) lightItem.light = item.GetComponentInChildren<Light2D>();

            if (lightItem.light != null) lightItem.light.enabled = enabled && !lightItem.inContainer;
        }

        private static JObject WriteLight(LightProperties light)
        {
            return new JObject
            {
                ["intensity"] = light.Intensity,
                ["color"] = NetworkSnapshotSerialization.WriteColor(light.Color),
                ["falloffIntensity"] = light.FalloffIntensity,
                ["volumeIntensity"] = light.VolumeIntensity,
                ["pointLightOuterRadius"] = light.PointLightOuterRadius,
                ["pointLightInnerRadius"] = light.PointLightInnerRadius,
                ["pointLightOuterAngle"] = light.PointLightOuterAngle,
                ["pointLightInnerAngle"] = light.PointLightInnerAngle,
                ["lightType"] = (int)light.LightType,
                ["rotation"] = light.Rotation,
                ["offsetX"] = light.Offset.x,
                ["offsetY"] = light.Offset.y,
                ["enabled"] = light.Enabled,
                ["addLightItem"] = light.AddLightItem
            };
        }

        private static LightProperties ReadLight(JObject obj)
        {
            var defaults = new LightProperties();
            return new LightProperties
            {
                Intensity = obj.Value<float?>("intensity") ?? defaults.Intensity,
                Color = NetworkSnapshotSerialization.ReadColor(obj["color"], defaults.Color),
                FalloffIntensity = obj.Value<float?>("falloffIntensity") ?? defaults.FalloffIntensity,
                VolumeIntensity = obj.Value<float?>("volumeIntensity"),
                PointLightOuterRadius = obj.Value<float?>("pointLightOuterRadius") ?? defaults.PointLightOuterRadius,
                PointLightInnerRadius = obj.Value<float?>("pointLightInnerRadius") ?? defaults.PointLightInnerRadius,
                PointLightOuterAngle = obj.Value<float?>("pointLightOuterAngle") ?? defaults.PointLightOuterAngle,
                PointLightInnerAngle = obj.Value<float?>("pointLightInnerAngle") ?? defaults.PointLightInnerAngle,
                LightType = (CustomLightType)(obj.Value<int?>("lightType") ?? (int)defaults.LightType),
                Rotation = obj.Value<float?>("rotation") ?? defaults.Rotation,
                Offset = new Vector2(obj.Value<float?>("offsetX") ?? 0f, obj.Value<float?>("offsetY") ?? 0f),
                Enabled = obj.Value<bool?>("enabled") ?? defaults.Enabled,
                AddLightItem = obj.Value<bool?>("addLightItem") ?? defaults.AddLightItem
            };
        }
    }
}
