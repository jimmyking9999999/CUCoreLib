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
            if (status == null || !StatusMetadata.TryCreate(status.GetType(), out StatusMetadata metadata) || !metadata.SaveEnabled)
            {
                return false;
            }

            try
            {
                JObject values = JObject.FromObject(status);
                payload = new JObject
                {
                    ["type"] = metadata.Key,
                    ["data"] = values ?? new JObject()
                };
                return true;
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib Statuses: Failed to capture status '" + status.GetType().FullName + "'.\n" + ex);
                return false;
            }
        }

        internal static void RestoreBodyStatuses(Body body, JArray payloads)
        {
            if (body == null || payloads == null)
            {
                return;
            }

            BodyStatusCollection collection = Get(body);
            foreach (JToken token in payloads)
            {
                RestoreStatusToken(collection, token, isBody: true);
            }
        }

        internal static void RestoreLimbStatuses(Limb limb, JArray payloads)
        {
            if (limb == null || payloads == null)
            {
                return;
            }

            LimbStatusCollection collection = Get(limb);
            foreach (JToken token in payloads)
            {
                RestoreStatusToken(collection, token, isBody: false);
            }
        }

        internal static JObject CaptureNetworkSnapshot()
        {
            JObject root = new JObject();
            Body body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
            if (body == null)
            {
                return root;
            }

            return CaptureBodyNetworkSnapshot(body);
        }

        internal static JObject CaptureBodyNetworkSnapshot(Body body)
        {
            JObject root = new JObject();
            if (body == null)
            {
                return root;
            }

            root["body"] = CaptureBodyStatusArray(body);
            root["limbs"] = CaptureLimbStatusArray(body);
            return root;
        }

        internal static JArray CaptureBodyStatusArray(Body body)
        {
            JArray bodyStatuses = new JArray();
            if (body == null)
            {
                return bodyStatuses;
            }

            foreach (KeyValuePair<Type, BodyStatus> entry in EnumerateBodyStatuses(body))
            {
                if (entry.Value == null || !TryCapture(entry.Value, out JObject payload))
                {
                    continue;
                }

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
            JArray limbStatuses = new JArray();
            Limb[] limbs = body != null ? body.limbs : null;
            if (limbs != null)
            {
                for (int limbIndex = 0; limbIndex < limbs.Length; limbIndex++)
                {
                    Limb limb = limbs[limbIndex];
                    if (limb == null)
                    {
                        continue;
                    }

                    foreach (KeyValuePair<Type, LimbStatus> entry in EnumerateLimbStatuses(limb))
                    {
                        if (entry.Value == null || !TryCapture(entry.Value, out JObject payload))
                        {
                            continue;
                        }

                        limbStatuses.Add(new JObject
                        {
                            ["slot"] = limbIndex,
                            ["type"] = entry.Key.AssemblyQualifiedName ?? entry.Key.FullName,
                            ["payload"] = payload
                        });
                    }
                }
            }

            return limbStatuses;
        }

        internal static void ApplyNetworkSnapshot(JObject snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            Body body = PlayerCamera.main != null ? PlayerCamera.main.body : null;
            if (body == null)
            {
                return;
            }

            ApplyBodySnapshot(body, snapshot["body"] as JArray);
            ApplyLimbSnapshot(body, snapshot["limbs"] as JArray);
        }

        private static void RestoreStatusToken(IStatusCollection collection, JToken token, bool isBody)
        {
            JObject obj = token as JObject;
            if (obj == null)
            {
                return;
            }

            string key = obj.Value<string>("type");
            JObject data = obj["data"] as JObject;
            if (string.IsNullOrWhiteSpace(key) || data == null)
            {
                return;
            }

            if (!StatusMetadata.TryResolve(key, out StatusMetadata metadata))
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib Statuses: Skipping unknown status type '" + key + "' during restore.");
                return;
            }

            if (isBody && !typeof(BodyStatus).IsAssignableFrom(metadata.StatusType))
            {
                return;
            }

            if (!isBody && !typeof(LimbStatus).IsAssignableFrom(metadata.StatusType))
            {
                return;
            }

            try
            {
                StatusBase restored = (StatusBase)data.ToObject(metadata.StatusType);
                if (restored == null)
                {
                    return;
                }

                collection.Set(metadata.StatusType, restored);
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib Statuses: Failed to restore status '" + key + "'.\n" + ex);
            }
        }

        private static void ApplyBodySnapshot(Body body, JArray payloads)
        {
            if (body == null || payloads == null)
            {
                return;
            }

            BodyStatusCollection collection = Get(body);
            foreach (JToken token in payloads)
            {
                ApplySnapshotToken(collection, token, isBody: true);
            }
        }

        private static void ApplyLimbSnapshot(Body body, JArray payloads)
        {
            if (body == null || payloads == null || body.limbs == null)
            {
                return;
            }

            foreach (JToken token in payloads)
            {
                JObject obj = token as JObject;
                if (obj == null)
                {
                    continue;
                }

                int limbIndex = obj.Value<int?>("slot") ?? -1;
                if (limbIndex < 0 || limbIndex >= body.limbs.Length)
                {
                    continue;
                }

                Limb limb = body.limbs[limbIndex];
                if (limb == null)
                {
                    continue;
                }

                ApplySnapshotToken(Get(limb), obj["payload"], isBody: false);
            }
        }

        private static void ApplySnapshotToken(IStatusCollection collection, JToken token, bool isBody)
        {
            JObject obj = token as JObject;
            if (collection == null || obj == null)
            {
                return;
            }

            string typeName = obj.Value<string>("type");
            JObject payload = obj["payload"] as JObject ?? obj["data"] as JObject;
            if (string.IsNullOrWhiteSpace(typeName) || payload == null)
            {
                return;
            }

            Type statusType = Type.GetType(typeName, false);
            if (statusType == null || !StatusMetadata.TryCreate(statusType, out StatusMetadata metadata))
            {
                return;
            }

            if (isBody && !typeof(BodyStatus).IsAssignableFrom(metadata.StatusType))
            {
                return;
            }

            if (!isBody && !typeof(LimbStatus).IsAssignableFrom(metadata.StatusType))
            {
                return;
            }

            try
            {
                StatusBase restored = (StatusBase)payload.ToObject(metadata.StatusType);
                if (restored != null)
                {
                    collection.Set(metadata.StatusType, restored);
                }
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib Statuses: Failed to apply network snapshot for status '" + typeName + "'.\n" + ex);
            }
        }

        internal sealed class BodyStatusCollection : IStatusCollection
        {
            private readonly Dictionary<Type, BodyStatus> _entries = new Dictionary<Type, BodyStatus>();

            internal IEnumerable<KeyValuePair<Type, BodyStatus>> Entries => _entries;

            public TStatus Get<TStatus>() where TStatus : BodyStatus, new()
            {
                Type type = typeof(TStatus);
                if (_entries.TryGetValue(type, out BodyStatus value))
                {
                    return (TStatus)value;
                }

                TStatus created = new TStatus();
                _entries[type] = created;
                StatusMetadata.TryCreate(type, out _);
                return created;
            }

            public void Set(Type type, StatusBase status)
            {
                if (type == null || status == null)
                {
                    return;
                }

                _entries[type] = (BodyStatus)status;
            }
        }

        internal sealed class LimbStatusCollection : IStatusCollection
        {
            private readonly Dictionary<Type, LimbStatus> _entries = new Dictionary<Type, LimbStatus>();

            internal IEnumerable<KeyValuePair<Type, LimbStatus>> Entries => _entries;

            public TStatus Get<TStatus>() where TStatus : LimbStatus, new()
            {
                Type type = typeof(TStatus);
                if (_entries.TryGetValue(type, out LimbStatus value))
                {
                    return (TStatus)value;
                }

                TStatus created = new TStatus();
                _entries[type] = created;
                StatusMetadata.TryCreate(type, out _);
                return created;
            }

            public void Set(Type type, StatusBase status)
            {
                if (type == null || status == null)
                {
                    return;
                }

                _entries[type] = (LimbStatus)status;
            }
        }

        internal interface IStatusCollection
        {
            void Set(Type type, StatusBase status);
        }

        internal sealed class StatusMetadata
        {
            private static readonly Dictionary<Type, StatusMetadata> ByType = new Dictionary<Type, StatusMetadata>();
            private static readonly Dictionary<string, StatusMetadata> ByKey = new Dictionary<string, StatusMetadata>(StringComparer.Ordinal);

            internal Type StatusType;
            internal string Key;
            internal bool SaveEnabled;

            internal static bool TryCreate(Type type, out StatusMetadata metadata)
            {
                metadata = null;
                if (type == null || !typeof(StatusBase).IsAssignableFrom(type))
                {
                    return false;
                }

                if (ByType.TryGetValue(type, out metadata))
                {
                    return true;
                }

                StatusOptionsAttribute options = type.GetCustomAttribute<StatusOptionsAttribute>();
                string key = options?.Key;
                if (string.IsNullOrWhiteSpace(key))
                {
                    key = type.FullName;
                }

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
