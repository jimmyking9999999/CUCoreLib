using CUCoreLib.Registries;
using Newtonsoft.Json.Linq;

namespace CUCoreLib.Saving
{
    internal sealed class BuiltInLiquidTileSaveProvider : IWorldSaveProvider
    {
        public int GetVersion()
        {
            return 1;
        }

        public JToken Capture(WorldSaveContext context)
        {
            return new JObject
            {
                ["mapping"] = LiquidTileRegistry.CaptureMappingSnapshot(),
                ["world"] = LiquidTileRegistry.CaptureWorldStateSnapshot()
            };
        }

        public void Restore(WorldSaveContext context, JToken payload, int version, SaveRestoreContext contextForRestore)
        {
            if (!(payload is JObject obj)) return;

            contextForRestore.Defer(() =>
            {
                LiquidTileRegistry.ApplyNetworkSnapshot(obj);
            });
        }
    }
}
