using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using CUCoreLib.ContentReload;
using CUCoreLib.Registries;
using Newtonsoft.Json.Linq;

namespace CUCoreLib.Networking
{
    public static class MultiplayerApi
    {
        private const string CustomPlayerDataChannel = "cucorelib.playerdata.get";
        private const string CustomPlayerLimbDataChannel = "cucorelib.playerdata.limbs.get";
        private const string NetPlayerTypeName = "Together.ScavPlayer";
        private const string KrokMpPluginGuid = "CasualtiesMP";

        // Yay! It was used :)
        private static readonly string[][] KrokMpMemberAliases =
        {
            new[] { "ScavPlayer", "steam_id", "SteamId" },
            new[] { "ScavPlayer", "plrcolor", "playerColor" },
            new[] { "ScavPlayer", "additional_profile_tag_icons", "KnownUserTagIcons" },
            new[] { "ScavPlayer", "LOCAL_PLAYER", "LocalPlayer" },
            new[] { "ScavPlayer", "clientId", "playerId" },
            new[] { "ScavPlayer", "is_local", "IsLocal" },
            new[] { "ScavPlayer", "TryGetNetPlayerAndBodyFromClientId", "TryGetPlayerAndBodyFromClientId" },
            new[] { "NetBody", "is_player", "IsPlayer" },
            new[] { "NetBody", "is_local", "IsLocal" },
            new[] { "Net", "running", "IsRunning" },
            new[] { "Net", "is_client", "IsClient" },
            new[] { "Net", "is_server", "IsServer" },
            new[] { "Net", "is_host", "IsHost" },
            new[] { "Net", "is_connected", "IsConnected" },
            new[] { "Net", "type", "Type" },
            new[] { "Net", "TRANSPORT", "Transport" },
            new[] { "NetObjectRegistry", "ObjectCanBeIgnoredForNetwork", "ObjectShouldNotBeSynced" },
            new[] { "NetObjectRegistry", "Server_EnsureItemNetworkRegistered", "Server_EnsureItemIsNetworkRegistered" },
            new[] { "Net", "MY_SERVER_INFO", "MyServerInfo" },
            new[] { "SyncInfo", "LoadObjectResource", "InstantiateResource" }
        };

        private static readonly string[][] KrokMpTypeAliases =
        {
            new[] { "KrokoshaCasualtiesMP.NetPlayer", "Together.ScavPlayer" },
            new[] { "KrokoshaCasualtiesMP.KrokoshaScavMultiplayer", "Together.Multiplayer" },
            new[] { "KrokoshaCasualtiesMP.NewCoolerObjectPacketWriteReadSystem", "Together.SyncInfo" },
            new[]
            {
                "KrokoshaCasualtiesMP.Krokosha_BuildingEntity_Rope_TrackerComponent",
                "Together.TrackerClimbableEntity"
            },
            new[]
            {
                "KrokoshaCasualtiesMP.KrokoshaCoopModAssets", "KrokoshaCasualtiesMP.CoopModAssets",
                "Together.CoopModAssets"
            },
            new[] { "KrokoshaCasualtiesMP.FontUntils", "Together.FontUtils" },
            new[] { "KrokoshaCasualtiesMP.HingeJointState", "Together.ClientMain+SavedHingeJointState" },
            new[] { "KrokoshaCasualtiesMP.StatePrinter", "Together.StatePrinter" }
        };

        private static readonly string[] KrokMpNamespaces =
            { "Together", "CasualtiesMultiplayerAPI", "CasualtiesTogetherUtils" };

        private static Version _krokMpVersion;
        private static Type _netPlayerType;
        private static MethodInfo _tryGetNetPlayerAndBodyFromClientIdMethod;
        private static MethodInfo _getPlayerFromBodyMethod;
        private static MemberInfo _localPlayerMember;
        private static MemberInfo _clientIdToPlayerDictMember;
        private static MemberInfo _playerNameMember;
        private static MemberInfo _playerBodyMember;
        private static FieldInfo _netPlayerBodyField;
        private static bool _playerDataHandlersRegistered;

        public static bool IsAvailable => MultiplayerBridge.IsAvailable;
        public static bool IsRunning => MultiplayerBridge.IsRunning;
        public static bool IsClient => MultiplayerBridge.IsClient;
        public static bool IsServer => MultiplayerBridge.IsServer;
        public static bool IsHost => MultiplayerBridge.IsHost;

        public static bool BridgeReceiversInstalled => MultiplayerBridge.ReceiversInstalled;

        public static Version KrokMpVersion
        {
            get
            {
                if (_krokMpVersion != null) return _krokMpVersion;
                if (Chainloader.PluginInfos.TryGetValue(KrokMpPluginGuid, out var info) && info?.Metadata != null)
                    _krokMpVersion = info.Metadata.Version;
                return _krokMpVersion;
            }
        }

        public static Assembly KrokMpAssembly => MultiplayerBridge.KrokMpAssembly;

        public static Type ResolveKrokMpType(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            foreach (var candidate in TypeCandidates(name.Trim()))
            {
                var type = ResolveLoadedType(candidate);
                if (type != null) return type;
            }

            return null;
        }
        public static MemberInfo FindKrokMpMember(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name)) return null;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.Static;

            foreach (var candidate in MemberCandidates(type.Name, name.Trim()))
            {
                var field = type.GetField(candidate, flags);
                if (field != null) return field;

                var property = type.GetProperty(candidate, flags);
                if (property != null) return property;
            }

            return null;
        }

        public static MemberInfo FindKrokMpMember(string typeName, string name)
        {
            return FindKrokMpMember(ResolveKrokMpType(typeName), name);
        }

        public static MethodInfo FindKrokMpMethod(Type type, string name, Type[] parameterTypes = null)
        {
            if (type == null || string.IsNullOrWhiteSpace(name)) return null;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.Static;

            foreach (var candidate in MemberCandidates(type.Name, name.Trim()))
            foreach (var method in type.GetMethods(flags))
            {
                if (string.Equals(method.Name, candidate, StringComparison.Ordinal) &&
                    ParametersMatch(method, parameterTypes))
                    return method;
            }

            return null;
        }

        public static MethodInfo FindKrokMpMethod(string typeName, string name, Type[] parameterTypes = null)
        {
            return FindKrokMpMethod(ResolveKrokMpType(typeName), name, parameterTypes);
        }

        private static bool ParametersMatch(MethodInfo method, Type[] parameterTypes)
        {
            if (parameterTypes == null) return true;

            var parameters = method.GetParameters();
            if (parameters.Length != parameterTypes.Length) return false;

            for (var index = 0; index < parameters.Length; index++)
            {
                var expected = UnwrapByRef(parameterTypes[index]);
                var actual = UnwrapByRef(parameters[index].ParameterType);
                if (expected == null || actual == null) return false;
                if (expected != actual && !expected.IsAssignableFrom(actual)) return false;
            }

            return true;
        }

        private static Type UnwrapByRef(Type type)
        {
            return type != null && type.IsByRef ? type.GetElementType() : type;
        }

        public static void RegisterServerHandler(string channel, Func<JToken, JToken> handler)
        {
            ContentReloadSession.AssertNotActive("MultiplayerApi.RegisterServerHandler()",
                "Multiplayer registration is excluded from strict content reload.");
            MultiplayerBridge.RegisterServerHandler(channel, handler);
        }

        public static void RegisterClientHandler(string channel, Action<JToken> handler)
        {
            ContentReloadSession.AssertNotActive("MultiplayerApi.RegisterClientHandler()",
                "Multiplayer registration is excluded from strict content reload.");
            MultiplayerBridge.RegisterClientHandler(channel, handler);
        }

        public static void RegisterHandler(string channel, Action<JToken> handler)
        {
            if (handler == null) return;

            RegisterClientHandler(channel, handler);
            RegisterServerHandler(channel, payload =>
            {
                handler(payload);
                return null;
            });
        }

        /// <summary>
        /// Sends to the selected client when called on the server, otherwise sends to the server.
        /// The target client ID is ignored on clients. Does not relay between clients or broadcast.
        /// </summary>
        public static bool SendToPeer(uint targetClientId, string channel, object payload = null, bool reliable = true)
        {
            return IsServer
                ? SendToClient(targetClientId, channel, payload, reliable)
                : SendToServer(channel, payload, reliable);
        }

        public static bool SendToServer(string channel, object payload = null, bool reliable = true)
        {
            return MultiplayerBridge.SendToServer(channel, payload, reliable);
        }

        public static bool RequestServer(string channel, object payload, Action<JToken> onResponse,
            bool reliable = true)
        {
            return MultiplayerBridge.RequestServer(channel, payload, onResponse, reliable);
        }

        public static bool SendToClient(uint clientId, string channel, object payload = null, bool reliable = true)
        {
            return MultiplayerBridge.SendToClient(clientId, channel, payload, reliable);
        }

        public static bool Broadcast(string channel, object payload = null, bool includeHost = false,
            bool reliable = true)
        {
            return MultiplayerBridge.Broadcast(channel, payload, includeHost, reliable);
        }

        /// <summary>
        /// Sends to every instance in the lobby, including this one: the host broadcasts and applies
        /// locally, a client asks the host to relay, and with no session the local handler just runs.
        /// Pair it with RegisterHandler on the same channel.
        /// </summary>
        public static bool BroadcastEverywhere(string channel, object payload = null, bool reliable = true)
        {
            return MultiplayerBridge.BroadcastEverywhere(channel, payload, reliable);
        }

        /// <summary>
        /// Sends to one instance whichever role you hold: host to any client, client to the host, and
        /// client to another client through the host. Unknown ids return false.
        /// </summary>
        public static bool BroadcastToPeer(string channel, uint clientId, object payload = null, bool reliable = true)
        {
            return MultiplayerBridge.BroadcastToPeer(channel, clientId, payload, reliable);
        }

        public static void RegisterSyncModule(string key, Func<JObject> capture, Action<JObject> apply = null)
        {
            ContentReloadSession.AssertNotActive("MultiplayerApi.RegisterSyncModule()",
                "Multiplayer registration is excluded from strict content reload.");
            MultiplayerSyncRegistry.RegisterModule(key, capture, apply);
        }

        public static JObject CaptureSnapshot(string targetLanguage = null)
        {
            return MultiplayerSyncRegistry.CaptureSnapshot(targetLanguage);
        }

        public static void ApplySnapshot(JObject snapshot)
        {
            MultiplayerSyncRegistry.ApplySnapshot(snapshot);
        }

        public static void ScheduleInitialSnapshot()
        {
            MultiplayerSyncRegistry.ScheduleInitialSnapshot();
        }

        public static void RequestInitialSnapshot()
        {
            MultiplayerSyncRegistry.RequestInitialSnapshot();
        }

        public static bool BroadcastSnapshot(bool includeHost = false)
        {
            return MultiplayerSyncRegistry.BroadcastSnapshot(includeHost);
        }

        // Loadbar progress, for slow connections
        public static void ReportLoadingProgress(string label, int current, int total)
        {
            MultiplayerLoadingProgress.Report(label, current, total);
        }

        public static void RegisterBuiltIns()
        {
            ContentReloadSession.AssertNotActive("MultiplayerApi.RegisterBuiltIns()",
                "Multiplayer registration is excluded from strict content reload.");
            MultiplayerSyncRegistry.RegisterBuiltIns();
            RegisterCustomPlayerDataHandlers();
        }

        public static JObject GetCustomPlayerData(uint clientId)
        {
            if (!TryGetBodyFromClientId(clientId, out var body)) return new JObject();

            return new JObject
            {
                ["clientId"] = clientId,
                ["body"] = StatusRegistry.CaptureBodyStatusArray(body)
            };
        }

        public static JObject GetCustomPlayerLimbData(uint clientId)
        {
            if (!TryGetBodyFromClientId(clientId, out var body)) return new JObject();

            return new JObject
            {
                ["clientId"] = clientId,
                ["limbs"] = StatusRegistry.CaptureLimbStatusArray(body)
            };
        }

        public static bool RequestCustomPlayerData(uint clientId, Action<JObject> onResponse, bool reliable = true)
        {
            RegisterCustomPlayerDataHandlers();
            return RequestServer(
                CustomPlayerDataChannel,
                new JObject { ["clientId"] = clientId },
                token => onResponse?.Invoke(token as JObject),
                reliable);
        }

        public static bool RequestCustomPlayerLimbData(uint clientId, Action<JObject> onResponse, bool reliable = true)
        {
            RegisterCustomPlayerDataHandlers();
            return RequestServer(
                CustomPlayerLimbDataChannel,
                new JObject { ["clientId"] = clientId },
                token => onResponse?.Invoke(token as JObject),
                reliable);
        }

        public static void RegisterCustomPlayerDataHandlers()
        {
            ContentReloadSession.AssertNotActive("MultiplayerApi.RegisterCustomPlayerDataHandlers()",
                "Multiplayer registration is excluded from strict content reload.");

            if (_playerDataHandlersRegistered) return;

            _playerDataHandlersRegistered = true;
            RegisterServerHandler(CustomPlayerDataChannel, payload =>
            {
                var clientId = payload?.Value<uint?>("clientId") ?? 0u;
                return GetCustomPlayerData(clientId);
            });

            RegisterServerHandler(CustomPlayerLimbDataChannel, payload =>
            {
                var clientId = payload?.Value<uint?>("clientId") ?? 0u;
                return GetCustomPlayerLimbData(clientId);
            });
        }

        public static bool TryGetLocalBody(out Body body)
        {
            body = null;

            if (IsRunning && TryResolveLocalPlayerBody(out var localBody))
            {
                body = localBody;
                return true;
            }

            if (PlayerCamera.main != null) body = PlayerCamera.main.body;
            return body != null;
        }

        private static bool TryResolveLocalPlayerBody(out Body body)
        {
            body = null;

            if (_localPlayerMember == null)
            {
                _localPlayerMember = FindKrokMpMember("NetPlayer", "LOCAL_PLAYER");
                if (_localPlayerMember == null) return false;
            }

            var localPlayer = ReadMember(_localPlayerMember, null);
            if (localPlayer == null) return false;

            if (_netPlayerBodyField == null)
                _netPlayerBodyField = localPlayer.GetType().GetField("body");

            body = _netPlayerBodyField?.GetValue(localPlayer) as Body;
            return body != null;
        }

        private static object ReadMember(MemberInfo member, object instance)
        {
            switch (member)
            {
                case FieldInfo field:
                    return field.GetValue(instance);
                case PropertyInfo property:
                    return property.CanRead ? property.GetValue(instance) : null;
                default:
                    return null;
            }
        }

        internal static bool TryGetBodyFromClientId(uint clientId, out Body body)
        {
            body = null;
            if (!TryResolveNetPlayerReflection()) return false;

            var clientIdType = _tryGetNetPlayerAndBodyFromClientIdMethod.GetParameters()[0].ParameterType;
            var args = new object[] { MultiplayerBridge.ConvertClientId(clientId, clientIdType), null, null };
            var found = _tryGetNetPlayerAndBodyFromClientIdMethod.Invoke(null, args) is bool flag && flag;
            if (!found) return false;

            body = args[2] as Body;
            return body != null;
        }

        internal static bool TryGetClientIdFromBody(Body body, out uint clientId)
        {
            clientId = 0u;
            if (body == null) return false;

            if (_getPlayerFromBodyMethod == null)
                _getPlayerFromBodyMethod = FindKrokMpMethod("NetPlayer", "GetPlayerFromBody", new[] { typeof(Body) });
            if (_getPlayerFromBodyMethod == null) return false;

            var player = _getPlayerFromBodyMethod.Invoke(null, new object[] { body });
            if (player == null) return false;

            clientId = MultiplayerBridge.ConvertPlayerToClientId(player);
            return true;
        }

        internal static List<KeyValuePair<string, Body>> EnumeratePlayers()
        {
            var result = new List<KeyValuePair<string, Body>>();

            var playerType = ResolveLoadedType(NetPlayerTypeName);
            if (playerType == null) return result;

            if (_clientIdToPlayerDictMember == null)
                _clientIdToPlayerDictMember = FindKrokMpMember(playerType, "ClientIdToPlayerDict");

            var dict = ReadMember(_clientIdToPlayerDictMember, null) as System.Collections.IDictionary;
            if (dict == null) return result;

            foreach (var value in dict.Values)
            {
                if (value == null) continue;

                var type = value.GetType();
                if (_playerNameMember == null) _playerNameMember = FindKrokMpMember(type, "playerName");
                if (_playerBodyMember == null) _playerBodyMember = FindKrokMpMember(type, "body");

                var body = ReadMember(_playerBodyMember, value) as Body;
                if (body == null) continue;

                result.Add(new KeyValuePair<string, Body>(ReadMember(_playerNameMember, value) as string, body));
            }

            return result;
        }

        private static bool TryResolveNetPlayerReflection()
        {
            if (_tryGetNetPlayerAndBodyFromClientIdMethod != null) return true;

            _netPlayerType = ResolveLoadedType(NetPlayerTypeName);
            if (_netPlayerType == null) return false;

            var names = MemberCandidates(_netPlayerType.Name, "TryGetNetPlayerAndBodyFromClientId").ToArray();
            _tryGetNetPlayerAndBodyFromClientIdMethod = _netPlayerType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    if (!names.Contains(method.Name)) return false;

                    var parameters = method.GetParameters();
                    return parameters.Length == 3 &&
                           MultiplayerBridge.IsClientIdType(parameters[0].ParameterType) &&
                           parameters[1].IsOut &&
                           parameters[2].IsOut &&
                           parameters[2].ParameterType == typeof(Body).MakeByRefType();
                });

            return _tryGetNetPlayerAndBodyFromClientIdMethod != null;
        }

        private static Type ResolveLoadedType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;

            var scoped = MultiplayerBridge.KrokMpAssembly?.GetType(fullName, false);
            if (scoped != null) return scoped;

            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static IEnumerable<string> TypeCandidates(string name)
        {
            yield return name;

            // Do people still use simple namespaces?
            var simple = SimpleName(name);
            foreach (var @namespace in KrokMpNamespaces)
                yield return @namespace + "." + simple;

            foreach (var group in KrokMpTypeAliases)
            {
                if (!group.Any(full => string.Equals(full, name, StringComparison.Ordinal) ||
                                       string.Equals(SimpleName(full), simple, StringComparison.Ordinal)))
                    continue;

                foreach (var full in group) yield return full;
            }
        }

        private static IEnumerable<string> MemberCandidates(string typeName, string name)
        {
            yield return name;

            foreach (var group in KrokMpMemberAliases)
            {
                if (!string.Equals(group[0], typeName, StringComparison.Ordinal)) continue;
                if (!group.Skip(1).Any(alias => string.Equals(alias, name, StringComparison.Ordinal))) continue;

                foreach (var alias in group.Skip(1))
                    if (!string.Equals(alias, name, StringComparison.Ordinal)) yield return alias;
            }
        }

        private static string SimpleName(string fullName)
        {
            var index = fullName.LastIndexOf('.');
            return index < 0 ? fullName : fullName.Substring(index + 1);
        }
    }
}
