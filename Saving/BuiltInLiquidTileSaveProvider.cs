using CUCoreLib.Registries;
using Newtonsoft.Json.Linq;

namespace CUCoreLib.Saving
{
    internal sealed class BuiltInLiquidTileSaveProvider : IWorldSaveProvider
    {
        public int GetVersion()
        {
            return 2;
        }

        public JToken Capture(WorldSaveContext context)
        {
            return new JObject
            {
                ["layer"] = context.World?.biomeDepth ?? -1,
                ["mapping"] = LiquidTileRegistry.CaptureMappingSnapshot(),
                ["world"] = LiquidTileRegistry.CaptureWorldStateSnapshot()
            };
        }

        public void Restore(WorldSaveContext context, JToken payload, int version,
            SaveRestoreContext contextForRestore)
        {
            if (!(payload is JObject obj)) return;

            var layer = obj.Value<int?>("layer");

            contextForRestore.Defer(() =>
            {
                // Flooded cells belong to the layer that captured them, unlike the id-to-byte mapping.
                // Payloads that carry no layer restore as they did before the guard existed.
                if (layer.HasValue && layer != WorldGeneration.world?.biomeDepth)
                    obj["world"] = null;

                LiquidTileRegistry.ApplyNetworkSnapshot(obj);
            });
        }
    }
}
