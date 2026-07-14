using CUCoreLib.Patches;
using CUCoreLib.Registries;
using CUCoreLib.Helpers;
using Newtonsoft.Json.Linq;
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

            var lightItem = item.GetComponent<LightItem>();
            if (lightItem != null) result["lightEnabled"] = lightItem.shouldEnable;

            return result.HasValues ? result : null;
        }

        public void Restore(Item item, string itemKey, JToken payload, int version, SaveRestoreContext context)
        {
            if (item == null || !(payload is JObject obj) || !ItemRegistry.TryGetCustomInfo(item, out _)) return;

            if (obj["customData"] is JObject customData)
                ItemRegistry.RestoreRuntimeCustomData(item, customData);

            if (obj["iconOverride"] is JObject iconOverride)
                ItemRegistryPatches.SetRuntimeIconOverride(item, NetworkSnapshotSerialization.ReadSprite(iconOverride));
            else
                ItemRegistryPatches.SetRuntimeIconOverride(item, null);

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
    }
}
