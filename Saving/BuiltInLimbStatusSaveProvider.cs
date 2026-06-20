using Newtonsoft.Json.Linq;
using CUCoreLib.Registries;

namespace CUCoreLib.Saving
{
    internal sealed class BuiltInLimbStatusSaveProvider : ILimbSaveProvider
    {
        public int GetVersion()
        {
            return 1;
        }

        public JToken Capture(Limb limb, int limbIndex)
        {
            if (limb == null)
            {
                return null;
            }

            JArray statuses = new JArray();
            foreach (var entry in StatusRegistry.EnumerateLimbStatuses(limb))
            {
                if (StatusRegistry.TryCapture(entry.Value, out JObject payload))
                {
                    statuses.Add(payload);
                }
            }

            return statuses.Count == 0 ? null : statuses;
        }

        public void Restore(Limb limb, int limbIndex, JToken payload, int version, SaveRestoreContext context)
        {
            StatusRegistry.RestoreLimbStatuses(limb, payload as JArray);
        }
    }
}
