using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using CUCoreLib.Data;
using Newtonsoft.Json.Linq;

namespace CUCoreLib.Registries
{
    public static class StatusRegistry
    {
        private static readonly ConditionalWeakTable<Body, BodyStatusCollection> BodyStatuses =
            new ConditionalWeakTable<Body, BodyStatusCollection>();

        private static readonly ConditionalWeakTable<Limb, LimbStatusCollection> LimbStatuses =
            new ConditionalWeakTable<Limb, LimbStatusCollection>();

        internal static IEnumerable<KeyValuePair<Type, BodyStatus>> EnumerateBodyStatuses(Body body)
        {
            return body == null ? Array.Empty<KeyValuePair<Type, BodyStatus>>() : Get(body).Entries;
        }

        internal static IEnumerable<KeyValuePair<Type, LimbStatus>> EnumerateLimbStatuses(Limb limb)
        {
            return limb == null ? Array.Empty<KeyValuePair<Type, LimbStatus>>() : Get(limb).Entries;
        }

        internal static BodyStatusCollection Get(Body body)
        {
            return body == null ? null : BodyStatuses.GetValue(body, _ => new BodyStatusCollection());
        }

        internal static LimbStatusCollection Get(Limb limb)
        {
            return limb == null ? null : LimbStatuses.GetValue(limb, _ => new LimbStatusCollection());
        }

        internal static bool TryCapture(StatusBase status, out JObject payload)
        {
            payload = null;
            if (status == null || !StatusMetadata.TryCreate(status.GetType(), out var metadata) ||
                !metadata.SaveEnabled) return false;

            try
            {
                var values = JObject.FromObject(status);
                payload = new JObject
                {
                    ["type"] = metadata.Key,
                    ["data"] = values ?? new JObject()
                };
                return true;
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib Statuses: Failed to capture status '" +
                                                status.GetType().FullName + "'.\n" + ex);
                return false;
            }
        }

        internal static void RestoreBodyStatuses(Body body, JArray payloads)
        {
            if (body == null || payloads == null) return;

            var collection = Get(body);
            foreach (var token in payloads) RestoreStatusToken(collection, token, true);
        }

        internal static void RestoreLimbStatuses(Limb limb, JArray payloads)
        {
            if (limb == null || payloads == null) return;

            var collection = Get(limb);
            foreach (var token in payloads) RestoreStatusToken(collection, token, false);
        }

        internal static JObject CaptureNetworkSnapshot()
        {
            var root = new JObject();
            var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
            if (body == null) return root;

            return CaptureBodyNetworkSnapshot(body);
        }

        internal static JObject CaptureBodyNetworkSnapshot(Body body)
        {
            var root = new JObject();
            if (body == null) return root;

            root["body"] = CaptureBodyStatusArray(body);
            root["limbs"] = CaptureLimbStatusArray(body);
            return root;
        }

        internal static JArray CaptureBodyStatusArray(Body body)
        {
            var bodyStatuses = new JArray();
            if (body == null) return bodyStatuses;

            foreach (var entry in EnumerateBodyStatuses(body))
            {
                if (entry.Value == null || !TryCapture(entry.Value, out var payload)) continue;

                bodyStatuses.Add(new JObject
                {
                    ["slot"] = "body",
                    ["type"] = entry.Key.AssemblyQualifiedName ?? entry.Key.FullName,
                    ["payload"] = payload
                });
            }

            return bodyStatuses;
        }

        internal static JArray CaptureLimbStatusArray(Body body)
        {
            var limbStatuses = new JArray();
            var limbs = body != null ? body.limbs : null;
            if (limbs != null)
                for (var limbIndex = 0; limbIndex < limbs.Length; limbIndex++)
                {
                    var limb = limbs[limbIndex];
                    if (limb == null) continue;

                    foreach (var entry in EnumerateLimbStatuses(limb))
                    {
                        if (entry.Value == null || !TryCapture(entry.Value, out var payload)) continue;

                        limbStatuses.Add(new JObject
                        {
                            ["slot"] = limbIndex,
                            ["type"] = entry.Key.AssemblyQualifiedName ?? entry.Key.FullName,
                            ["payload"] = payload
                        });
                    }
                }

            return limbStatuses;
        }

        internal static void ApplyNetworkSnapshot(JObject snapshot)
        {
            if (snapshot == null) return;

            var body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
            if (body == null) return;

            ApplyBodySnapshot(body, snapshot["body"] as JArray);
            ApplyLimbSnapshot(body, snapshot["limbs"] as JArray);
        }

        private static void RestoreStatusToken(IStatusCollection collection, JToken token, bool isBody)
        {
            var obj = token as JObject;
            if (obj == null) return;

            var key = obj.Value<string>("type");
            var data = obj["data"] as JObject;
            if (string.IsNullOrWhiteSpace(key) || data == null) return;

            if (!StatusMetadata.TryResolve(key, out var metadata))
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib Statuses: Skipping unknown status type '" + key +
                                                "' during restore.");
                return;
            }

            if (isBody && !typeof(BodyStatus).IsAssignableFrom(metadata.StatusType)) return;

            if (!isBody && !typeof(LimbStatus).IsAssignableFrom(metadata.StatusType)) return;

            try
            {
                var restored = (StatusBase)data.ToObject(metadata.StatusType);
                if (restored == null) return;

                collection.Set(metadata.StatusType, restored);
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib Statuses: Failed to restore status '" + key + "'.\n" + ex);
            }
        }

        private static void ApplyBodySnapshot(Body body, JArray payloads)
        {
            if (body == null || payloads == null) return;

            var collection = Get(body);
            foreach (var token in payloads) ApplySnapshotToken(collection, token, true);
        }

        private static void ApplyLimbSnapshot(Body body, JArray payloads)
        {
            if (body == null || payloads == null || body.limbs == null) return;

            foreach (var token in payloads)
            {
                var obj = token as JObject;
                if (obj == null) continue;

                var limbIndex = obj.Value<int?>("slot") ?? -1;
                if (limbIndex < 0 || limbIndex >= body.limbs.Length) continue;

                var limb = body.limbs[limbIndex];
                if (limb == null) continue;

                ApplySnapshotToken(Get(limb), obj["payload"], false);
            }
        }

        private static void ApplySnapshotToken(IStatusCollection collection, JToken token, bool isBody)
        {
            var obj = token as JObject;
            if (collection == null || obj == null) return;

            var typeName = obj.Value<string>("type");
            var payload = obj["payload"] as JObject ?? obj["data"] as JObject;
            if (string.IsNullOrWhiteSpace(typeName) || payload == null) return;

            var statusType = Type.GetType(typeName, false);
            if (statusType == null || !StatusMetadata.TryCreate(statusType, out var metadata)) return;

            if (isBody && !typeof(BodyStatus).IsAssignableFrom(metadata.StatusType)) return;

            if (!isBody && !typeof(LimbStatus).IsAssignableFrom(metadata.StatusType)) return;

            try
            {
                var restored = (StatusBase)payload.ToObject(metadata.StatusType);
                if (restored != null) collection.Set(metadata.StatusType, restored);
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib Statuses: Failed to apply network snapshot for status '" +
                                                typeName + "'.\n" + ex);
            }
        }

        internal sealed class BodyStatusCollection : IStatusCollection
        {
            private readonly Dictionary<Type, BodyStatus> _entries = new Dictionary<Type, BodyStatus>();

            internal IEnumerable<KeyValuePair<Type, BodyStatus>> Entries => _entries;

            public void Set(Type type, StatusBase status)
            {
                if (type == null || status == null) return;

                _entries[type] = (BodyStatus)status;
            }

            public TStatus Get<TStatus>() where TStatus : BodyStatus, new()
            {
                var type = typeof(TStatus);
                if (_entries.TryGetValue(type, out var value)) return (TStatus)value;

                var created = new TStatus();
                _entries[type] = created;
                StatusMetadata.TryCreate(type, out _);
                return created;
            }
        }

        internal sealed class LimbStatusCollection : IStatusCollection
        {
            private readonly Dictionary<Type, LimbStatus> _entries = new Dictionary<Type, LimbStatus>();

            internal IEnumerable<KeyValuePair<Type, LimbStatus>> Entries => _entries;

            public void Set(Type type, StatusBase status)
            {
                if (type == null || status == null) return;

                _entries[type] = (LimbStatus)status;
            }

            public TStatus Get<TStatus>() where TStatus : LimbStatus, new()
            {
                var type = typeof(TStatus);
                if (_entries.TryGetValue(type, out var value)) return (TStatus)value;

                var created = new TStatus();
                _entries[type] = created;
                StatusMetadata.TryCreate(type, out _);
                return created;
            }
        }

        internal interface IStatusCollection
        {
            void Set(Type type, StatusBase status);
        }

        internal sealed class StatusMetadata
        {
            private static readonly Dictionary<Type, StatusMetadata> ByType = new Dictionary<Type, StatusMetadata>();

            private static readonly Dictionary<string, StatusMetadata> ByKey =
                new Dictionary<string, StatusMetadata>(StringComparer.Ordinal);

            internal string Key;
            internal bool SaveEnabled;

            internal Type StatusType;

            internal static bool TryCreate(Type type, out StatusMetadata metadata)
            {
                metadata = null;
                if (type == null || !typeof(StatusBase).IsAssignableFrom(type)) return false;

                if (ByType.TryGetValue(type, out metadata)) return true;

                var options = type.GetCustomAttribute<StatusOptionsAttribute>();
                var key = options?.Key;
                if (string.IsNullOrWhiteSpace(key)) key = type.FullName;

                metadata = new StatusMetadata
                {
                    StatusType = type,
                    Key = key,
                    SaveEnabled = options == null || options.SaveEnabled
                };

                ByType[type] = metadata;
                ByKey[key] = metadata;
                return true;
            }

            internal static bool TryResolve(string key, out StatusMetadata metadata)
            {
                return ByKey.TryGetValue(key, out metadata);
            }
        }
    }
}