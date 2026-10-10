using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Networking
{
    internal static class MultiplayerLoadingProgress
    {
        internal const string Channel = "cucorelib.sync.loadingprogress";
        private const float MinSendIntervalSeconds = 0.15f;
        private const string DefaultLabel = "Loading";

        private static bool _registered;
        private static float _lastSendTime = float.NegativeInfinity;
        private static string _lastLabel;
        private static int _lastCurrent = int.MinValue;
        private static int _lastTotal = int.MinValue;

        internal static void Register()
        {
            if (_registered) return;
            _registered = true;
            MultiplayerBridge.RegisterClientHandler(Channel, Apply);
        }

        internal static void Report(string label, int current, int total)
        {
            if (!MultiplayerBridge.IsAvailable || !MultiplayerBridge.IsServer) return;

            if (string.IsNullOrWhiteSpace(label)) label = DefaultLabel;
            if (label == _lastLabel && current == _lastCurrent && total == _lastTotal) return;

            var isFinal = total > 0 && current >= total;
            var now = Time.realtimeSinceStartup;
            if (!isFinal && now - _lastSendTime < MinSendIntervalSeconds) return;

            _lastSendTime = now;
            _lastLabel = label;
            _lastCurrent = current;
            _lastTotal = total;

            MultiplayerBridge.Broadcast(Channel, new JObject
            {
                ["label"] = label,
                ["current"] = current,
                ["total"] = total
            }, includeHost: false, reliable: false);
        }

        private static void Apply(JToken payload)
        {
            var world = WorldGeneration.world;
            if (world == null || !(payload is JObject obj)) return;

            var label = obj.Value<string>("label");
            if (string.IsNullOrWhiteSpace(label)) label = DefaultLabel;
            var current = obj.Value<int?>("current") ?? 0;
            var total = obj.Value<int?>("total") ?? 0;

            world.SetLoadingTextNoLocale(total > 0
                ? label + " (" + current + "/" + total + ")"
                : label);
        }
    }
}
