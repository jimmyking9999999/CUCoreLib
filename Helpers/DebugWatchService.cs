using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CUCoreLib.Helpers
{
    internal static class DebugWatchService
    {
        private static readonly Dictionary<string, WatchDescriptor> DescriptorByName =
            new Dictionary<string, WatchDescriptor>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<WatchDescriptor> AllDescriptors = new List<WatchDescriptor>();
        private static readonly List<WatchDescriptor> ActiveWatches = new List<WatchDescriptor>();

        private static readonly List<string> RootAutofill = new List<string>
        {
            "add",
            "remove",
            "list",
            "clear",
            "show",
            "hide"
        };

        private static GameObject _overlayRoot;
        private static GUIStyle _overlayStyle;
        private static bool _initialized;
        private static bool _overlayVisible = true;

        internal static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            RebuildDescriptorCache();

            _overlayRoot = new GameObject("CUCoreLib.DebugWatchOverlay");
            Object.DontDestroyOnLoad(_overlayRoot);
            _overlayRoot.hideFlags = HideFlags.HideAndDontSave;
            _overlayRoot.AddComponent<DebugWatchOverlayBehaviour>();
        }

        internal static bool AddWatch(string query, out string message)
        {
            if (!TryResolveDescriptor(query, out var descriptor, out message))
                return false;

            if (ActiveWatches.Contains(descriptor))
            {
                message = $"Already watching {descriptor.DisplayName}.";
                return true;
            }

            ActiveWatches.Add(descriptor);
            message = $"Watching {descriptor.DisplayName}.";
            return true;
        }

        internal static bool RemoveWatch(string query, out string message)
        {
            if (!TryResolveDescriptor(query, out var descriptor, out message))
                return false;

            if (!ActiveWatches.Remove(descriptor))
            {
                message = $"{descriptor.DisplayName} is not currently watched.";
                return true;
            }

            message = $"Stopped watching {descriptor.DisplayName}.";
            return true;
        }

        internal static void ClearWatches()
        {
            ActiveWatches.Clear();
        }

        internal static IReadOnlyList<string> GetActiveWatchLines()
        {
            return ActiveWatches
                .Select(descriptor => $"{descriptor.DisplayName}: {FormatValue(descriptor)}")
                .ToList();
        }

        internal static IReadOnlyList<string> GetAvailableWatchNames()
        {
            return AllDescriptors.Select(descriptor => descriptor.DisplayName).ToList();
        }

        internal static IReadOnlyList<string> GetRootAutofill()
        {
            return RootAutofill;
        }

        internal static bool HasActiveWatches()
        {
            return ActiveWatches.Count > 0;
        }

        internal static void SetOverlayVisible(bool visible)
        {
            _overlayVisible = visible;
        }

        internal static bool IsOverlayVisible()
        {
            return _overlayVisible;
        }

        private static void RebuildDescriptorCache()
        {
            DescriptorByName.Clear();
            AllDescriptors.Clear();

            foreach (var assembly in GetCandidateAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(type => type != null).ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (type == null) continue;

                    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                         BindingFlags.Static | BindingFlags.FlattenHierarchy))
                    {
                        if (!IsSupportedField(field)) continue;

                        var descriptor = new WatchDescriptor(type, field);
                        AllDescriptors.Add(descriptor);

                        if (!DescriptorByName.ContainsKey(descriptor.DisplayName))
                            DescriptorByName[descriptor.DisplayName] = descriptor;
                    }
                }
            }

            AllDescriptors.Sort((left, right) =>
                string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<Assembly> GetCandidateAssemblies()
        {
            foreach (var plugin in Chainloader.PluginInfos.Values)
            {
                var assembly = plugin?.Instance?.GetType().Assembly;
                if (assembly == null) continue;
                yield return assembly;
            }
        }

        private static bool IsSupportedField(FieldInfo field)
        {
            if (field == null) return false;
            if (!field.IsStatic) return false;
            return IsSupportedValueType(field.FieldType);
        }

        private static bool IsSupportedValueType(Type type)
        {
            if (type == null) return false;

            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying.IsEnum) return true;
            if (underlying == typeof(string)) return true;
            if (underlying == typeof(bool)) return true;
            if (underlying == typeof(byte)) return true;
            if (underlying == typeof(sbyte)) return true;
            if (underlying == typeof(short)) return true;
            if (underlying == typeof(ushort)) return true;
            if (underlying == typeof(int)) return true;
            if (underlying == typeof(uint)) return true;
            if (underlying == typeof(long)) return true;
            if (underlying == typeof(ulong)) return true;
            if (underlying == typeof(float)) return true;
            if (underlying == typeof(double)) return true;
            if (underlying == typeof(decimal)) return true;
            if (underlying == typeof(Vector2)) return true;
            if (underlying == typeof(Vector3)) return true;
            return false;
        }

        private static bool TryResolveDescriptor(string query, out WatchDescriptor descriptor, out string message)
        {
            descriptor = null;
            if (string.IsNullOrWhiteSpace(query))
            {
                message = "Usage: debugwatch add [Type.member]";
                return false;
            }

            var trimmedQuery = query.Trim();
            if (DescriptorByName.TryGetValue(trimmedQuery, out descriptor))
            {
                message = null;
                return true;
            }

            var matches = AllDescriptors
                .Where(candidate =>
                    candidate.DisplayName.EndsWith(trimmedQuery, StringComparison.OrdinalIgnoreCase) ||
                    candidate.ShortName.Equals(trimmedQuery, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 1)
            {
                descriptor = matches[0];
                message = null;
                return true;
            }

            if (matches.Count > 1)
            {
                message = $"Ambiguous debug watch '{trimmedQuery}'. Use a more specific type path.";
                return false;
            }

            message = $"Could not find supported static field '{trimmedQuery}'.";
            return false;
        }

        private static string FormatValue(WatchDescriptor descriptor)
        {
            try
            {
                var value = descriptor.Field.GetValue(null);
                return FormatValueObject(value, descriptor.Field.FieldType);
            }
            catch (Exception ex)
            {
                return $"<error: {ex.GetType().Name}>";
            }
        }

        private static string FormatValueObject(object value, Type declaredType)
        {
            if (value == null) return "null";

            var underlying = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
            if (underlying == typeof(string)) return (string)value;
            if (underlying == typeof(bool)) return (bool)value ? "true" : "false";
            if (underlying.IsEnum) return value.ToString();

            if (underlying == typeof(float))
                return ((float)value).ToString("0.###", CultureInfo.InvariantCulture);
            if (underlying == typeof(double))
                return ((double)value).ToString("0.###", CultureInfo.InvariantCulture);
            if (underlying == typeof(decimal))
                return ((decimal)value).ToString("0.###", CultureInfo.InvariantCulture);
            if (underlying == typeof(Vector2))
            {
                var vector = (Vector2)value;
                return $"({vector.x:0.###}, {vector.y:0.###})";
            }

            if (underlying == typeof(Vector3))
            {
                var vector = (Vector3)value;
                return $"({vector.x:0.###}, {vector.y:0.###}, {vector.z:0.###})";
            }

            if (value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            return value.ToString();
        }

        private static bool ShouldRenderOverlay()
        {
            if (!_overlayVisible) return false;
            if (ActiveWatches.Count == 0) return false;
            if (WorldGeneration.world == null) return false;
            return PlayerCamera.main != null;
        }

        private static GUIStyle GetOverlayStyle()
        {
            if (_overlayStyle != null) return _overlayStyle;

            _overlayStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white },
                alignment = TextAnchor.UpperRight,
                fontSize = 16,
                wordWrap = false,
                richText = false
            };

            return _overlayStyle;
        }

        private sealed class WatchDescriptor
        {
            internal WatchDescriptor(Type declaringType, FieldInfo field)
            {
                DeclaringType = declaringType;
                Field = field;
                DisplayName = $"{GetTypeDisplayName(declaringType)}.{field.Name}";
                ShortName = $"{declaringType.Name}.{field.Name}";
            }

            internal Type DeclaringType { get; }
            internal FieldInfo Field { get; }
            internal string DisplayName { get; }
            internal string ShortName { get; }

            private static string GetTypeDisplayName(Type type)
            {
                return (type.FullName ?? type.Name).Replace('+', '.');
            }
        }

        private sealed class DebugWatchOverlayBehaviour : MonoBehaviour
        {
            private void OnGUI()
            {
                if (!ShouldRenderOverlay()) return;

                var lines = GetActiveWatchLines();
                if (lines.Count == 0) return;

                const float width = 500f;
                var height = 24f * lines.Count + 12f;
                var rect = new Rect(Screen.width - width - 16f, 12f, width, height);
                GUI.Label(rect, string.Join("\n", lines), GetOverlayStyle());
            }
        }
    }
}