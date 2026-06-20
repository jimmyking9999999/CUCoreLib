using Newtonsoft.Json.Linq;
using UnityEngine.Rendering.Universal;
using CUCoreLib.Patches;
using CUCoreLib.Registries;

namespace CUCoreLib.Saving
{
    internal sealed class BuiltInItemRuntimeSaveProvider : IItemSaveProvider
    {
        public int GetVersion()
        {
            return 1;
        }

        public JToken Capture(Item item, string itemKey)
        {
            if (item == null || !ItemRegistry.TryGetCustomInfo(item, out _))
            {
                return null;
            }

            LightItem lightItem = item.GetComponent<LightItem>();
            if (lightItem == null)
            {
                return null;
            }

            return new JObject
            {
                ["lightEnabled"] = lightItem.shouldEnable
            };
        }

        public void Restore(Item item, string itemKey, JToken payload, int version, SaveRestoreContext context)
        {
            JObject obj = payload as JObject;
            if (item == null || obj == null || !ItemRegistry.TryGetCustomInfo(item, out _))
            {
                return;
            }

            JToken enabledToken = obj["lightEnabled"];
            if (enabledToken == null || enabledToken.Type == JTokenType.Null)
            {
                return;
            }

            bool enabled = enabledToken.Value<bool>();
            LightItem lightItem = item.GetComponent<LightItem>();
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
            if (item == null)
            {
                return;
            }

            LightItem lightItem = item.GetComponent<LightItem>();
            if (lightItem == null)
            {
                return;
            }

            lightItem.shouldEnable = enabled;
            if (lightItem.light == null)
            {
                lightItem.light = item.GetComponentInChildren<Light2D>();
            }

            if (lightItem.light != null)
            {
                lightItem.light.enabled = enabled && !lightItem.inContainer;
            }
        }
    }
}
