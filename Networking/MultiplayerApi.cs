using System;
using System.Linq;
using System.Reflection;
using CUCoreLib.ContentReload;
using CUCoreLib.Registries;
using Newtonsoft.Json.Linq;

namespace CUCoreLib.Networking;

public static class MultiplayerApi
{
    private const string CustomPlayerDataChannel = "cucorelib.playerdata.get";
    private const string CustomPlayerLimbDataChannel = "cucorelib.playerdata.limbs.get";
    private const string NetPlayerTypeName = "KrokoshaCasualtiesMP.NetPlayer";

    private static Type _netPlayerType;
    private static MethodInfo _tryGetNetPlayerAndBodyFromClientIdMethod;
    private static bool _playerDataHandlersRegistered;

    public static bool IsAvailable => MultiplayerBridge.IsAvailable;
    public static bool IsRunning => MultiplayerBridge.IsRunning;
    public static bool IsClient => MultiplayerBridge.IsClient;
    public static bool IsServer => MultiplayerBridge.IsServer;
    public static bool IsHost => MultiplayerBridge.IsHost;

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

    public static void RegisterSyncModule(string key, Func<JObject> capture, Action<JObject> apply = null)
    {
        ContentReloadSession.AssertNotActive("MultiplayerApi.RegisterSyncModule()",
            "Multiplayer registration is excluded from strict content reload.");
        MultiplayerSyncRegistry.RegisterModule(key, capture, apply);
    }

    public static JObject CaptureSnapshot()
    {
        return MultiplayerSyncRegistry.CaptureSnapshot();
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

    private static bool TryGetBodyFromClientId(uint clientId, out Body body)
    {
        body = null;
        if (!TryResolveNetPlayerReflection()) return false;

        var args = new object[] { clientId, null, null };
        var found = _tryGetNetPlayerAndBodyFromClientIdMethod.Invoke(null, args) is bool and true;
        if (!found) return false;

        body = args[2] as Body;
        return body != null;
    }

    private static bool TryResolveNetPlayerReflection()
    {
        if (_tryGetNetPlayerAndBodyFromClientIdMethod != null) return true;

        _netPlayerType = ResolveLoadedType(NetPlayerTypeName);
        if (_netPlayerType == null) return false;

        _tryGetNetPlayerAndBodyFromClientIdMethod = _netPlayerType
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(method =>
            {
                if (!string.Equals(method.Name, "TryGetNetPlayerAndBodyFromClientId", StringComparison.Ordinal))
                    return false;

                var parameters = method.GetParameters();
                return parameters.Length == 3 &&
                       IsClientIdType(parameters[0].ParameterType) &&
                       parameters[1].IsOut &&
                       parameters[2].IsOut &&
                       parameters[2].ParameterType == typeof(Body).MakeByRefType();
            });

        return _tryGetNetPlayerAndBodyFromClientIdMethod != null;
    }

    private static bool IsClientIdType(Type type)
    {
        return type == typeof(byte) ||
               type == typeof(ushort) ||
               type == typeof(uint) ||
               type == typeof(ulong);
    }

    private static Type ResolveLoadedType(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return null;

        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName, false))
            .FirstOrDefault(type => type != null);
    }
}