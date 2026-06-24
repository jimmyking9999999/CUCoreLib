using CUCoreLib.Registries;
using Newtonsoft.Json.Linq;

namespace CUCoreLib.Saving
{
    internal sealed class BuiltInBodyStatusSaveProvider : IBodySaveProvider
    {
        public int GetVersion()
        {
            return 1;
        }

        public JToken Capture(Body body)
        {
            if (body == null) return null;

            var statuses = new JArray();
            foreach (var entry in StatusRegistry.EnumerateBodyStatuses(body))
                if (StatusRegistry.TryCapture(entry.Value, out var payload))
                    statuses.Add(payload);

            return statuses.Count == 0 ? null : statuses;
        }

        public void Restore(Body body, JToken payload, int version, SaveRestoreContext context)
        {
            StatusRegistry.RestoreBodyStatuses(body, payload as JArray);
        }
    }
}