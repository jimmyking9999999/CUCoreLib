using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BepInEx.Bootstrap;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CUCoreLib.Networking
{
    internal enum KrokMpSaveScope
    {
        NotActive,
        Client,
        Shared,
        Player,
        Unsupported
    }

    public static class MultiplayerBridge
    {
        private const string PluginGuid = "CasualtiesMP";
        private const string MpTypeName = "Together.Multiplayer";
        private const string NetTypeName = "Together.Net";
        private const string NetTypeEnumName = "Together.Net+NetType";
        private const string ServerMainTypeName = "Together.ServerMain";
        private const string ClientMainTypeName = "Together.ClientMain";
        private const string LiteNetTransportTypeName = "Together.TransportLiteNetLib";
        private const string MainMenuTypeName = "Together.UIMainMenu";
        private const string SavesystemPatchTypeName = "Together.SavesystemPatch";
        private const string ExtensionsTypeName = "Together.MyLiteNetLibExtensions";
        private const string MessageField = "msg";
        private const string ChannelField = "channel";
        private const string KindField = "kind";
        private const string RequestIdField = "requestId";
        private const string SenderField = "sender";
        private const string PayloadField = "payload";

        // C:Tv5 identifies messages by a single byte,so we need to use the custom-message channel instead of claiming raw message ids, 
        // thus the name is hashed to a uint for CustomMessage
        private const string RequestChannel = "cucorelib.bridge.request";
        private const string ResponseChannel = "cucorelib.bridge.response";
        private const string RelayChannel = "cucorelib.mp.broadcast";

        private static readonly Dictionary<string, Func<JToken, JToken>> ServerHandlers =
            new Dictionary<string, Func<JToken, JToken>>(StringComparer.Ordinal);

        private static readonly Dictionary<string, Func<uint, JToken, JToken>> ServerHandlersWithSender =
            new Dictionary<string, Func<uint, JToken, JToken>>(StringComparer.Ordinal);

        private static readonly Dictionary<string, Action<JToken>> ClientHandlers =
            new Dictionary<string, Action<JToken>>(StringComparer.Ordinal);

        private static readonly Dictionary<string, Action<JToken>> PendingResponses =
            new Dictionary<string, Action<JToken>>(StringComparer.Ordinal);

        private static bool _initialized;
        private static bool _retryScheduled;

        private static Assembly _krokAssembly;
        private static Type _mpType;
        private static Type _netType;
        private static readonly Dictionary<string, PropertyInfo> NetBoolProperties =
            new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
        private static Type _netModeType;
        private static Type _serverMainType;
        private static Type _clientMainType;
        private static Type _liteNetTransportType;
        private static Type _mainMenuType;
        private static Type _deliveryMethodType;
        private static Type _readerType;
        private static Type _writerType;
        private static MethodInfo _createNamedWriterMethod;
        private static MethodInfo _clientSendMethod;
        private static MethodInfo _serverSendToMethod;
        private static MethodInfo _serverSendToClientsMethod;
        private static MethodInfo _registerCustomServerReceiverMethod;
        private static MethodInfo _registerCustomClientReceiverMethod;
        private static MemberInfo _playerIdMember;
        private static bool _playerIdMemberWarned;
        private static bool _receiversInstalled;
        private static MethodInfo _writerPutStringMethod;
        private static MethodInfo _readerGetStringMethod;
        private static MethodInfo _writerPutUShortMethod;
        private static MethodInfo _writerPutUIntMethod;
        private static MethodInfo _writerPutBytesWithLengthMethod;
        private static MethodInfo _readerGetUShortMethod;
        private static MethodInfo _readerGetUIntMethod;
        private static MethodInfo _readerGetBytesSegmentMethod;
        private static MethodInfo _readerGetBytesWithLengthMethod;
        private static PropertyInfo _readerAvailableBytesProperty;
        private static MethodInfo _liteNetConnectMethod;
        private static MethodInfo _serverAnnounceGameStartMethod;
        private static object _reliableOrdered;
        private static object _reliableUnordered;
        private static MethodInfo _serverSendToClientsInvoker;
        private static MethodInfo _serverSendToClientsInvokerSource;
        private static int _dynamicMethodCounter;

        private static Harmony _harmony;
        private static bool _transportHookInstalled;

        public static bool IsAvailable { get; private set; }

        public static bool ReceiversInstalled => _receiversInstalled;

        internal static Assembly KrokMpAssembly =>
            _krokAssembly ?? (_krokAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly =>
                    string.Equals(assembly.GetName().Name, PluginGuid, StringComparison.OrdinalIgnoreCase)));

        public static bool IsRunning => GetNetBool("IsRunning");
        public static bool IsClient => GetNetBool("IsClient");
        public static bool IsServer => GetNetBool("IsServer");
        public static bool IsHost => GetNetBool("IsHost");
        public static bool IsConnected => GetNetBool("IsConnected");

        internal static KrokMpSaveScope GetKrokMpSaveScope(out string directory)
        {
            directory = null;
            if (!IsKrokMpExpected()) return KrokMpSaveScope.NotActive;

            try
            {
                if (_krokAssembly == null)
                    _krokAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
                        assembly.GetType(MpTypeName, false) != null);

                var netType = _krokAssembly?.GetType(NetTypeName, false);
                var running = netType?.GetProperty("IsRunning", BindingFlags.Public | BindingFlags.Static);
                if (running?.PropertyType != typeof(bool))
                    return KrokMpSaveScope.Unsupported;
                if (!GetStaticBool(running))
                    return KrokMpSaveScope.NotActive;

                var isClient = netType.GetProperty("IsClient", BindingFlags.Public | BindingFlags.Static);
                if (isClient?.PropertyType == typeof(bool) && GetStaticBool(isClient))
                    return KrokMpSaveScope.Client;

                var savesType = _krokAssembly.GetType(SavesystemPatchTypeName, false);
                var replacement = savesType?.GetField("savedatapathreplacement",
                    BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
                var root = savesType?.GetProperty("mpsavefolder", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null, null) as string;
                if (string.IsNullOrWhiteSpace(replacement) || string.IsNullOrWhiteSpace(root))
                    return KrokMpSaveScope.Unsupported;

                var normalizedReplacement = Path.GetFullPath(replacement)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var normalizedRoot = Path.GetFullPath(root)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                directory = normalizedReplacement;

                if (string.Equals(normalizedReplacement, normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    return KrokMpSaveScope.Shared;

                var rootPrefix = normalizedRoot + Path.DirectorySeparatorChar;
                return normalizedReplacement.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                    ? KrokMpSaveScope.Player
                    : KrokMpSaveScope.Unsupported;
            }
            catch (Exception /*ex*/)
            {
                // CUCoreLibPlugin.Log?.LogWarning("CUCoreLib could not resolve the KrokMP save scope.\n" + ex);
                return KrokMpSaveScope.Unsupported;
            }
        }

        public static bool TryConfigureLocalIdentity(string username, string address)
        {
            if (!TryResolveRuntime()) return false;
            if (_mainMenuType == null) return false;

            try
            {
                SetStaticString(_mainMenuType, "USERINPUT_NAME", username);
                SetStaticString(_mainMenuType, "USERINPUT_IPPORT", address);
                return true;
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib failed to configure KrokMP local identity.\n" + ex);
                return false;
            }
        }

        public static bool TryStartLocalQuickTestHost(string address)
        {
            return TryStartLocalConnection(address, "Host");
        }

        public static bool TryStartLocalQuickTestClient(string address)
        {
            return TryStartLocalConnection(address, "Client");
        }

        public static bool TryAnnounceGameStart()
        {
            if (!TryResolveRuntime() || _serverAnnounceGameStartMethod == null) return false;

            try
            {
                _serverAnnounceGameStartMethod.Invoke(null, null);
                return true;
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib failed to announce KrokMP game start.\n" + ex);
                return false;
            }
        }

        public static void Initialize(Harmony harmony = null)
        {
            if (_initialized) return;

            _initialized = true;
            _harmony = harmony;
            RegisterServerHandler(RelayChannel, HandleRelay);

            if (TryResolveRuntime())
            {
                InstallNamedReceivers();
                InstallKrokMpTransportHook();
                IsAvailable = true;
                return;
            }

            ScheduleRetry();
        }

        public static void RegisterServerHandler(string channel, Func<JToken, JToken> handler)
        {
            if (!string.IsNullOrWhiteSpace(channel) && handler != null) ServerHandlers[channel.Trim()] = handler;
        }

        internal static void RegisterServerHandler(string channel, Func<uint, JToken, JToken> handler)
        {
            if (!string.IsNullOrWhiteSpace(channel) && handler != null)
                ServerHandlersWithSender[channel.Trim()] = handler;
        }

        public static void RegisterClientHandler(string channel, Action<JToken> handler)
        {
            if (!string.IsNullOrWhiteSpace(channel) && handler != null) ClientHandlers[channel.Trim()] = handler;
        }

        public static bool SendToServer(string channel, object payload = null, bool reliable = true)
        {
            return SendMessage(RequestChannel, channel, "event", payload, reliable, null, 0u, null);
        }

        public static bool RequestServer(string channel, object payload, Action<JToken> onResponse,
            bool reliable = true)
        {
            var requestId = Guid.NewGuid().ToString("N");
            if (onResponse != null) PendingResponses[requestId] = onResponse;

            var sent = SendMessage(RequestChannel, channel, "request", payload, reliable, requestId, 0u, null);
            if (!sent) PendingResponses.Remove(requestId);
            return sent;
        }

        public static bool SendToClient(uint clientId, string channel, object payload = null, bool reliable = true)
        {
            return SendMessage(ResponseChannel, channel, "event", payload, reliable, null, clientId, null);
        }

        public static bool Broadcast(string channel, object payload = null, bool includeHost = false,
            bool reliable = true)
        {
            if (!IsAvailable || !IsServer) return false;

            var targets = includeHost ? GetMemberList("AllClientIds") : GetMemberList("AllClientIdsExceptHost");
            return SendMessage(ResponseChannel, channel, "event", payload, reliable, null, 0u, targets);
        }

        public static bool BroadcastEverywhere(string channel, object payload = null, bool reliable = true)
        {
            return BroadcastRelayed(channel, null, payload, reliable);
        }

        public static bool BroadcastToPeer(string channel, uint clientId, object payload = null, bool reliable = true)
        {
            return BroadcastRelayed(channel, clientId, payload, reliable);
        }

        private static bool BroadcastRelayed(string channel, uint? target, object payload, bool reliable)
        {
            if (string.IsNullOrWhiteSpace(channel)) return false;

            var name = channel.Trim();
            var token = NormalizePayload(payload);

            if (!IsAvailable || !IsRunning)
            {
                // No session: this instance is the whole lobby, so the local handler is the only delivery.
                InvokeClientHandler(name, token);
                return target == null;
            }

            if (IsServer) return Deliver(name, target, token, reliable);

            // Clients cannot reach each other, so the host does the routing. Always reliable: losing
            // this hop would lose the send for everyone it covers.
            var envelope = new JObject
            {
                ["channel"] = name,
                ["reliable"] = reliable,
                ["payload"] = token
            };
            if (target.HasValue) envelope["target"] = target.Value;

            return SendToServer(RelayChannel, envelope);
        }

        private static bool Deliver(string channel, uint? target, JToken payload, bool reliable)
        {
            if (target == null)
            {
                Broadcast(channel, payload, includeHost: false, reliable: reliable);
                InvokeClientHandler(channel, payload);
                return true;
            }

            if (ContainsClientId("AllClientIdsExceptHost", target.Value))
                return SendToClient(target.Value, channel, payload, reliable);

            if (!ContainsClientId("AllClientIds", target.Value)) return false;

            InvokeClientHandler(channel, payload);
            return true;
        }

        private static JToken HandleRelay(JToken envelope)
        {
            var channel = envelope?.Value<string>("channel");
            if (string.IsNullOrWhiteSpace(channel)) return null;

            Deliver(channel.Trim(), envelope.Value<uint?>("target"), envelope["payload"],
                envelope.Value<bool?>("reliable") ?? true);
            return null;
        }

        private static bool ContainsClientId(string memberName, uint clientId)
        {
            if (!(GetMemberList(memberName) is IEnumerable list)) return false;

            foreach (var entry in list)
            {
                if (ConvertClientIdToUInt(entry) == clientId) return true;
            }

            return false;
        }

        internal static JToken NormalizePayload(object payload)
        {
            if (payload == null) return null;

            return payload is JToken token ? token : JToken.FromObject(payload);
        }

        internal static void HandleServerMessageObject(object senderPlayer, object reader)
        {
            HandleEnvelope(ConvertPlayerToClientId(senderPlayer), reader, true);
        }

        internal static void HandleClientMessageObject(object reader)
        {
            HandleEnvelope(0u, reader, false);
        }

        // The named server receiver hands over the sender's player object instead of a client id
        internal static uint ConvertPlayerToClientId(object player)
        {
            if (player == null) return 0u;

            try
            {
                if (_playerIdMember == null)
                    _playerIdMember = MultiplayerApi.FindKrokMpMember(player.GetType(), "clientId");

                object clientId;
                switch (_playerIdMember)
                {
                    case FieldInfo field:
                        clientId = field.GetValue(player);
                        break;
                    case PropertyInfo property when property.CanRead:
                        clientId = property.GetValue(player, null);
                        break;
                    default:
                        if (!_playerIdMemberWarned)
                        {
                            _playerIdMemberWarned = true;
                            CUCoreLibPlugin.Log?.LogWarning(
                                "CUCoreLib could not resolve the multiplayer sender id; server replies will be dropped.");
                        }

                        return 0u;
                }

                return ConvertClientIdToUInt(clientId);
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib could not read the multiplayer sender id.\n" + ex);
                return 0u;
            }
        }

        private static void HandleEnvelope(uint senderClientId, object reader, bool serverSide)
        {
            if (!TryReadEnvelope(reader, out var envelope)) return;

            var channel = envelope.Value<string>(ChannelField);
            if (string.IsNullOrWhiteSpace(channel)) return;

            var kind = envelope.Value<string>(KindField) ?? "event";
            var payload = envelope[PayloadField];
            var requestId = envelope.Value<string>(RequestIdField);

            if (string.Equals(kind, "response", StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(requestId) ||
                    !PendingResponses.TryGetValue(requestId, out var callback)) return;
                PendingResponses.Remove(requestId);
                callback(payload);

                return;
            }

            if (serverSide)
            {
                try
                {
                    JToken response;
                    if (ServerHandlersWithSender.TryGetValue(channel, out var senderHandler))
                        response = senderHandler(senderClientId, payload);
                    else if (ServerHandlers.TryGetValue(channel, out var handler))
                        response = handler(payload);
                    else
                        return;

                    if (response != null && !string.IsNullOrWhiteSpace(requestId))
                        SendEnvelopeToClient(senderClientId, channel, "response", response, requestId, true);
                }
                catch (Exception ex)
                {
                    CUCoreLibPlugin.Log?.LogWarning("CUCoreLib multiplayer server handler failed for '" + channel +
                                                    "'.\n" + ex);
                }
            }
            else
            {
                InvokeClientHandler(channel, payload);
            }
        }

        private static void InvokeClientHandler(string channel, JToken payload)
        {
            if (!ClientHandlers.TryGetValue(channel, out var handler)) return;
            try
            {
                handler(payload);
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib multiplayer client handler failed for '" + channel +
                                                "'.\n" + ex);
            }
        }

        private static bool SendMessage(string netChannel, string channel, string kind, object payload, bool reliable,
            string requestId, uint clientId, object targets)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(channel)) return false;

            var envelope = new JObject
            {
                [ChannelField] = channel.Trim(),
                [KindField] = kind,
                [RequestIdField] = requestId ?? string.Empty,
                [SenderField] = 0u,
                [PayloadField] = NormalizePayload(payload)
            };

            return SendEnvelope(netChannel, envelope, reliable, clientId, targets);
        }

        private static bool SendEnvelopeToClient(uint clientId, string channel, string kind, JToken payload,
            string requestId, bool reliable)
        {
            var envelope = new JObject
            {
                [ChannelField] = channel,
                [KindField] = kind,
                [RequestIdField] = requestId ?? string.Empty,
                [SenderField] = 0u,
                [PayloadField] = payload
            };

            return SendEnvelope(ResponseChannel, envelope, reliable, clientId, null);
        }

        private static bool SendEnvelope(string netChannel, JObject envelope, bool reliable, uint clientId,
            object targets)
        {
            if (!TryBuildWriter(netChannel, envelope, out var writer)) return false;

            var delivery = reliable ? _reliableOrdered : _reliableUnordered;
            try
            {
                if (targets != null)
                {
                    var invoker = GetSendToClientsInvoker();
                    if (invoker == null) return false;

                    invoker.Invoke(null, new[] { delivery, writer, targets });
                    return true;
                }

                if (clientId != 0u || IsHost)
                {
                    _serverSendToMethod.Invoke(null,
                        new[]
                        {
                            delivery, writer,
                            ConvertClientId(clientId, _serverSendToMethod.GetParameters()[2].ParameterType)
                        });
                    return true;
                }

                if (!IsClient || !IsConnected) return false;
                _clientSendMethod.Invoke(null, new[] { delivery, writer });
                return true;
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib multiplayer bridge failed to send a message.\n" + ex);
                return false;
            }
        }

        private static bool TryBuildWriter(string netChannel, JObject envelope, out object writer)
        {
            writer = null;
            if (_createNamedWriterMethod == null) return false;

            try
            {
                writer = _createNamedWriterMethod.Invoke(null, new object[] { netChannel });
                if (writer == null) return false;

                var json = JsonConvert.SerializeObject(envelope, Formatting.None);
                var encoded = Convert.ToBase64String(CUCoreUtils.CompressGZip(Encoding.UTF8.GetBytes(json)));

                return MultiplayerPayloadFrame.Write(writer, encoded, _writerPutStringMethod,
                    _writerPutUShortMethod, _writerPutUIntMethod, _writerPutBytesWithLengthMethod);
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib multiplayer bridge failed to build a message.\n" + ex);
            }

            writer = null;
            return false;
        }

        private static bool TryReadEnvelope(object reader, out JObject envelope)
        {
            envelope = null;
            if (reader == null) return false;

            try
            {
                var encoded = ReadString(reader);
                if (string.IsNullOrWhiteSpace(encoded)) return false;

                var compressed = Convert.FromBase64String(encoded);
                var decompressed = CUCoreUtils.DecompressGZip(compressed);
                if (decompressed == null) return false;

                var json = Encoding.UTF8.GetString(decompressed);
                envelope = JObject.Parse(json);
                return true;
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib multiplayer bridge failed to read a message.\n" + ex);
                return false;
            }
        }

        private static string ReadString(object reader)
        {
            if (reader == null) return null;

            if (_readerGetUShortMethod != null && _readerGetUIntMethod != null &&
                _readerGetBytesSegmentMethod != null && _readerGetBytesWithLengthMethod != null)
                return MultiplayerPayloadFrame.Read(reader, _readerGetStringMethod, _readerGetUShortMethod,
                    _readerGetUIntMethod, _readerGetBytesSegmentMethod, _readerGetBytesWithLengthMethod,
                    _readerAvailableBytesProperty);

            if (_readerGetStringMethod != null)
            {
                var args = new[] { reader, null, true };
                _readerGetStringMethod.Invoke(null, args);
                return args[1] as string;
            }

            var getString = reader.GetType().GetMethod("GetString",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (getString == null) return null;
            var value = getString.Invoke(reader, null);
            return value as string;

        }

        private static void InstallNamedReceivers()
        {
            if (_receiversInstalled) return;
            if (_registerCustomServerReceiverMethod == null || _registerCustomClientReceiverMethod == null) return;

            var serverDelegate = CreateReceiverDelegate(_registerCustomServerReceiverMethod,
                typeof(MultiplayerBridge).GetMethod(nameof(HandleServerMessageObject),
                    BindingFlags.NonPublic | BindingFlags.Static));
            var clientDelegate = CreateReceiverDelegate(_registerCustomClientReceiverMethod,
                typeof(MultiplayerBridge).GetMethod(nameof(HandleClientMessageObject),
                    BindingFlags.NonPublic | BindingFlags.Static));
            if (serverDelegate == null || clientDelegate == null) return;

            var serverInstalled = TryRegisterNamedReceiver(_registerCustomServerReceiverMethod, RequestChannel,
                serverDelegate);
            var clientInstalled = TryRegisterNamedReceiver(_registerCustomClientReceiverMethod, ResponseChannel,
                clientDelegate);
            _receiversInstalled = serverInstalled && clientInstalled;
        }

        private static bool TryRegisterNamedReceiver(MethodInfo registerMethod, string name, Delegate receiver)
        {
            try
            {
                registerMethod.Invoke(null, new object[] { name, receiver });
                return true;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null &&
                                                       ex.InnerException.GetType().Name == "DuplicateNameException")
            {
                // An earlier attempt registered this name already, we're fine
                return true;
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib could not register the multiplayer receiver '" + name +
                                                "'.\n" + ex);
                return false;
            }
        }

        private static void InstallKrokMpTransportHook()
        {
            if (_transportHookInstalled || _harmony == null || _netType == null || _netModeType == null) return;

            try
            {
               // Prevent net.shutdownreset
                var transportCreated = AccessTools.Method(_netType, "TransportCreated",
                    new[] { _netModeType, typeof(bool) });
                if (transportCreated == null) return;

                _harmony.Patch(transportCreated,
                    postfix: new HarmonyMethod(typeof(MultiplayerBridge).GetMethod(
                        nameof(HandleKrokMpTransportCreated), BindingFlags.NonPublic | BindingFlags.Static)));
                _transportHookInstalled = true;
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib could not hook KrokMP transport creation, consider reloading the session \n" +
                                                ex);
                return;
            }

            try
            {
                if (IsRunning) HandleKrokMpTransportCreated();
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib failed to restore multiplayer state for an active KrokMP session.\n" +
                                                ex);
            }
        }

        private static void HandleKrokMpTransportCreated()
        {
            if (!IsAvailable) return;
            if (IsClient && !IsServer)
            {
                DropPoolRegistry.ResetGeneration();
                MultiplayerSyncRegistry.MarkNewTransportSession();
              // I hate doing it this way, but keep on requesting the initial snapshot until we get it
              // guh.
                MultiplayerSyncRegistry.RequestInitialSnapshotForNewSession();
            }
        }

        private static Delegate CreateReceiverDelegate(MethodInfo registerMethod, MethodInfo helperMethod)
        {
            if (registerMethod == null || helperMethod == null) return null;

            var registerParams = registerMethod.GetParameters();
            if (registerParams.Length < 2) return null;

            var delegateType = registerParams[1].ParameterType;
            var invokeMethod = delegateType.GetMethod("Invoke");
            if (invokeMethod == null) return null;

            var invokeParams = invokeMethod.GetParameters();
            if (invokeParams.Length != helperMethod.GetParameters().Length) return null;

            var method = new DynamicMethod(
                "CUCoreLib_MP_Receiver_" + helperMethod.Name,
                typeof(void),
                invokeParams.Select(parameter => parameter.ParameterType).ToArray(),
                typeof(MultiplayerBridge).Module,
                true);

            var il = method.GetILGenerator();
            foreach (var parameter in invokeParams)
            {
                var parameterType = parameter.ParameterType;
                il.Emit(OpCodes.Ldarg, parameter.Position);

                if (parameterType.IsByRef)
                {
                    il.Emit(OpCodes.Ldind_Ref);
                    parameterType = parameterType.GetElementType();
                }

                if (parameterType != null && parameterType.IsValueType) il.Emit(OpCodes.Box, parameterType);
            }

            il.Emit(OpCodes.Call, helperMethod);
            il.Emit(OpCodes.Ret);
            return method.CreateDelegate(delegateType);
        }

        private static void ScheduleRetry()
        {
            if (_retryScheduled || !IsKrokMpExpected()) return;

            _retryScheduled = true;
            CUCoreUtils.CallWhen(TryResolveRuntime, BootstrapIfPossible, 1f);
        }

        private static void BootstrapIfPossible()
        {
            if (!TryResolveRuntime()) return;
            InstallNamedReceivers();
            InstallKrokMpTransportHook();
            IsAvailable = true;
        }

        private static bool TryResolveRuntime()
        {
            if (_krokAssembly == null)
                _krokAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
                    string.Equals(assembly.GetName().Name, PluginGuid, StringComparison.OrdinalIgnoreCase));

            if (!IsKrokMpExpected()) return false;

            if (_krokAssembly == null)
                _krokAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => assembly.GetType(MpTypeName, false) != null);

            if (_krokAssembly == null) return false;

            _mpType = _krokAssembly.GetType(MpTypeName, false);
            _netType = _krokAssembly.GetType(NetTypeName, false);
            NetBoolProperties.Clear();
            _netModeType = _krokAssembly.GetType(NetTypeEnumName, false);
            _serverMainType = _krokAssembly.GetType(ServerMainTypeName, false);
            _clientMainType = _krokAssembly.GetType(ClientMainTypeName, false);
            _liteNetTransportType = _krokAssembly.GetType(LiteNetTransportTypeName, false);
            _mainMenuType = _krokAssembly.GetType(MainMenuTypeName, false);
            if (_mpType == null || _netType == null || _netModeType == null || _serverMainType == null ||
                _clientMainType == null || _liteNetTransportType == null || _mainMenuType == null) return false;

            var liteNetLibAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, "LiteNetLib", StringComparison.OrdinalIgnoreCase));
            if (liteNetLibAssembly == null) return false;

            _readerType = liteNetLibAssembly.GetType("LiteNetLib.Utils.NetDataReader", false);
            _writerType = liteNetLibAssembly.GetType("LiteNetLib.Utils.NetDataWriter", false);
            if (_readerType == null || _writerType == null) return false;

            _deliveryMethodType = ResolveDeliveryMethodType();
            if (_deliveryMethodType == null) return false;

            _createNamedWriterMethod = ResolveMethod(_mpType, new[] { "CreateNamedWriter" },
                new[] { typeof(string) });
            _clientSendMethod = ResolveMethod(_netType, new[] { "Client_Send" },
                new[] { _deliveryMethodType, _writerType });
            _serverSendToMethod = ResolveMethod(_netType, new[] { "Server_SendTo" },
                new[] { _deliveryMethodType, _writerType, typeof(uint) });
            _serverSendToClientsMethod = ResolveSendToClientsMethod(_netType, _deliveryMethodType, _writerType);
            _registerCustomServerReceiverMethod = ResolveMethod(_mpType,
                new[] { "RegisterCustomServerReceiver" }, new[] { typeof(string), null });
            _registerCustomClientReceiverMethod = ResolveMethod(_mpType,
                new[] { "RegisterCustomClientReceiver" }, new[] { typeof(string), null });
            _writerPutStringMethod = ResolveStringPutMethod();
            _readerGetStringMethod = ResolveStringGetMethod();
            _writerPutUShortMethod = _writerType.GetMethod("Put", new[] { typeof(ushort) });
            _writerPutUIntMethod = _writerType.GetMethod("Put", new[] { typeof(uint) });
            _writerPutBytesWithLengthMethod = _writerType.GetMethod("PutBytesWithLength", new[] { typeof(byte[]) });
            _readerGetUShortMethod = _readerType.GetMethod("GetUShort", Type.EmptyTypes);
            _readerGetUIntMethod = _readerType.GetMethod("GetUInt", Type.EmptyTypes);
            _readerGetBytesSegmentMethod = _readerType.GetMethod("GetBytesSegment", new[] { typeof(int) });
            _readerGetBytesWithLengthMethod = _readerType.GetMethod("GetBytesWithLength", Type.EmptyTypes);
            _readerAvailableBytesProperty = _readerType.GetProperty("AvailableBytes");
            _liteNetConnectMethod = ResolveMethod(_liteNetTransportType, new[] { "OnWantToConnect" },
                new[] { typeof(string), _netModeType });
            _serverAnnounceGameStartMethod = ResolveMethod(_serverMainType, new[] { "Server_Announce_GAME_START" },
                Type.EmptyTypes);

            if (_createNamedWriterMethod == null || _clientSendMethod == null || _serverSendToMethod == null ||
                _serverSendToClientsMethod == null || _registerCustomServerReceiverMethod == null ||
                _registerCustomClientReceiverMethod == null || _liteNetConnectMethod == null ||
                _serverAnnounceGameStartMethod == null) return false;

            _reliableOrdered = Enum.Parse(_deliveryMethodType, "ReliableOrdered");
            _reliableUnordered = Enum.Parse(_deliveryMethodType, "ReliableUnordered");
            return true;
        }

        private static bool IsKrokMpExpected()
        {
            if (_krokAssembly != null) return true;

            return Chainloader.PluginInfos.ContainsKey(PluginGuid) || AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetType(MpTypeName, false) != null);
        }

        private static MethodInfo ResolveMethod(Type type, string[] methodNames, Type[] parameterTypes)
        {
            if (type == null) return null;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (methodNames == null ||
                    !methodNames.Any(name => string.Equals(method.Name, name, StringComparison.Ordinal))) continue;

                if (parameterTypes == null) return method;

                var parameters = method.GetParameters();
                if (parameters.Length != parameterTypes.Length) continue;

                var matches = !parameters.Where((t, i) => !ParameterMatches(parameterTypes[i], t.ParameterType)).Any();

                if (matches) return method;
            }

            return null;
        }

        private static MethodInfo ResolveSendToClientsMethod(Type netType, Type deliveryMethodType,
            Type writerType)
        {
            // KrokMP ships several Server_SendToClients overloads:
            //   (in DeliveryMethod, in NetDataWriter, in knetid)
            //   (in DeliveryMethod, in NetDataWriter, in IReadOnlyList<ScavPlayer>)
            //   (in DeliveryMethod, in NetDataWriter, in IEnumerable<knetid>)
            
            var candidates = netType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(candidate => string.Equals(candidate.Name, "Server_SendToClients", StringComparison.Ordinal))
                .Select(candidate => new { Method = candidate, Parameters = candidate.GetParameters() })
                .Where(candidate => candidate.Parameters.Length == 3 &&
                                    ParameterMatches(deliveryMethodType, candidate.Parameters[0].ParameterType) &&
                                    ParameterMatches(writerType, candidate.Parameters[1].ParameterType))
                .ToArray();

            MethodInfo looseFallback = null;
            foreach (var candidate in candidates)
            {
                var targetsType = UnwrapByRef(candidate.Parameters[2].ParameterType);
                if (targetsType == null || !targetsType.IsGenericType) continue;
                if (targetsType.GetGenericTypeDefinition() != typeof(IEnumerable<>)) continue;

                var elementType = targetsType.GetGenericArguments()[0];
                if (IsClientIdType(elementType)) return candidate.Method;
                if (looseFallback == null && elementType.IsValueType) looseFallback = candidate.Method;
            }

          
            return looseFallback;
        }

        private static MethodInfo GetSendToClientsInvoker()
        {
            if (_serverSendToClientsInvoker != null &&
                ReferenceEquals(_serverSendToClientsInvokerSource, _serverSendToClientsMethod))
                return _serverSendToClientsInvoker;

            _serverSendToClientsInvoker = BuildSendToClientsInvoker(_serverSendToClientsMethod);
            _serverSendToClientsInvokerSource = _serverSendToClientsMethod;
            return _serverSendToClientsInvoker;
        }

        private static MethodInfo BuildSendToClientsInvoker(MethodInfo method)
        {
            if (method == null) return null;

            var parameters = method.GetParameters();
            if (parameters.Length != 3) return method;

            if (!parameters.Any(parameter => parameter.ParameterType.IsByRef)) return method;

            var deliveryType = UnwrapByRef(parameters[0].ParameterType);
            var writerType = UnwrapByRef(parameters[1].ParameterType);
            var targetsType = UnwrapByRef(parameters[2].ParameterType);
           
            if (deliveryType == null || writerType == null || targetsType == null || targetsType.IsValueType)
                return method;

        
            var invoker = new DynamicMethod(
                "CUCoreLib_MP_SendToClients_Invoker_" + _dynamicMethodCounter++,
                typeof(void),
                new[] { deliveryType, writerType, typeof(object) },
                typeof(MultiplayerBridge).Module,
                true);

            var il = invoker.GetILGenerator();
            var deliveryLocal = il.DeclareLocal(deliveryType);
            var writerLocal = il.DeclareLocal(writerType);
            var targetsLocal = il.DeclareLocal(targetsType);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Stloc, deliveryLocal);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stloc, writerLocal);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Castclass, targetsType);
            il.Emit(OpCodes.Stloc, targetsLocal);

            il.Emit(OpCodes.Ldloca, deliveryLocal);
            il.Emit(OpCodes.Ldloca, writerLocal);
            il.Emit(OpCodes.Ldloca, targetsLocal);
            il.Emit(OpCodes.Call, method);
            il.Emit(OpCodes.Ret);
            return invoker;
        }

        private static Type UnwrapByRef(Type type)
        {
            return type != null && type.IsByRef ? type.GetElementType() : type;
        }

        private static Type ResolveDeliveryMethodType()
        {
            var method = ResolveMethod(_netType, new[] { "Client_Send" }, null);
            if (method == null) return null;

            var parameters = method.GetParameters();
            return parameters.Length > 0
                ? (parameters[0].ParameterType.IsByRef
                    ? parameters[0].ParameterType.GetElementType()
                    : parameters[0].ParameterType)
                : null;
        }

        private static MethodInfo ResolveStringPutMethod()
        {
            var extensions = _krokAssembly.GetType(ExtensionsTypeName, false);
            if (extensions == null) return null;    // Use null propagation

            return extensions.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    var parameters = method.GetParameters();
                    return method.Name == "Put" &&
                           parameters.Length == 3 &&
                           parameters[0].ParameterType == _writerType &&
                           parameters[1].ParameterType == typeof(string) &&
                           parameters[2].ParameterType == typeof(bool);
                });
        }

        private static MethodInfo ResolveStringGetMethod()
        {
            var extensions = _krokAssembly.GetType(ExtensionsTypeName, false);
            if (extensions == null) return null;

            return extensions.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    var parameters = method.GetParameters();
                    return method.Name == "Get" &&
                           parameters.Length == 3 &&
                           parameters[0].ParameterType == _readerType &&
                           parameters[1].IsOut &&
                           parameters[1].ParameterType == typeof(string).MakeByRefType() &&
                           parameters[2].ParameterType == typeof(bool);
                });
        }

        private static object GetMemberList(string memberName)
        {
            if (_serverMainType == null) return null;

            var property = _serverMainType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static);
            return property != null 
                ? property.GetValue(null, null)
                : null;
        }

        private static bool GetNetBool(string memberName)
        {
            if (_netType == null) return false;

            if (!NetBoolProperties.TryGetValue(memberName, out var property))
            {
                property = _netType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static);
                if (property == null || property.PropertyType != typeof(bool)) return false;
                NetBoolProperties[memberName] = property;
            }

            return GetStaticBool(property);
        }

        private static bool GetStaticBool(PropertyInfo property)
        {
            var value = property?.GetValue(null, null);
            return value is bool flag && flag;
        }

        private static bool ParameterMatches(Type expectedType, Type actualType)
        {
            if (expectedType == null) return true;

            if (actualType == expectedType) return true;

            var normalizedActual = actualType.IsByRef ? actualType.GetElementType() : actualType;
            var normalizedExpected = expectedType.IsByRef ? expectedType.GetElementType() : expectedType;
            if (normalizedActual == null || normalizedExpected == null) return false;

            if (normalizedActual == normalizedExpected) return true;

            if (normalizedExpected == typeof(IEnumerable)) return typeof(IEnumerable).IsAssignableFrom(normalizedActual);

            if (normalizedExpected.IsAssignableFrom(normalizedActual)) return true;

            return IsUnsignedIntegerLike(normalizedExpected) && IsClientIdType(normalizedActual);
        }

        internal static object ConvertClientId(uint clientId, Type targetType)
        {
            var normalizedType = targetType.IsByRef ? targetType.GetElementType() : targetType;
            if (normalizedType == null || normalizedType == typeof(uint)) return clientId;

            if (normalizedType.IsEnum) return Enum.ToObject(normalizedType, clientId);
            if (IsUnsignedIntegerLike(normalizedType)) return Convert.ChangeType(clientId, normalizedType);

            var idField = normalizedType.GetField("id", BindingFlags.Public | BindingFlags.NonPublic |
                                                        BindingFlags.Instance);
            if (idField != null && IsUnsignedIntegerLike(idField.FieldType))
            {
                var value = Activator.CreateInstance(normalizedType);
                idField.SetValue(value, Convert.ChangeType(clientId, idField.FieldType));
                return value;
            }

            return Convert.ChangeType(clientId, normalizedType);
        }

        internal static bool IsClientIdType(Type type)
        {
            var normalizedType = type.IsByRef ? type.GetElementType() : type;
            if (normalizedType == null) return false;
            if (IsUnsignedIntegerLike(normalizedType)) return true;

            var idField = normalizedType.GetField("id", BindingFlags.Public | BindingFlags.NonPublic |
                                                        BindingFlags.Instance);
            return idField != null && IsUnsignedIntegerLike(idField.FieldType);
        }

        private static uint ConvertClientIdToUInt(object clientId)
        {
            if (clientId == null) return 0u;
            if (IsUnsignedIntegerLike(clientId.GetType())) return Convert.ToUInt32(clientId);

            var idField = clientId.GetType().GetField("id", BindingFlags.Public | BindingFlags.NonPublic |
                                                        BindingFlags.Instance);
            return idField != null && IsUnsignedIntegerLike(idField.FieldType)
                ? Convert.ToUInt32(idField.GetValue(clientId))
                : 0u;
        }

        private static bool IsUnsignedIntegerLike(Type type)
        {
            return type == typeof(byte) || type == typeof(ushort) || type == typeof(uint) || type == typeof(ulong);
        }

        private static bool TryStartLocalConnection(string address, string modeName)
        {
            if (!TryResolveRuntime() || _liteNetConnectMethod == null || _netModeType == null) return false;

            try
            {
                var targetAddress = string.IsNullOrWhiteSpace(address) ? "localhost:7790" : address.Trim();
                var mode = Enum.Parse(_netModeType, modeName);
                var result = _liteNetConnectMethod.Invoke(null, new object[] { targetAddress, mode });
                return result is bool connected && connected;
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib failed to start KrokMP localhost quick test mode.\n" + ex);
                return false;
            }
        }

        private static void SetStaticString(Type type, string memberName, string value)
        {
            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null && field.FieldType == typeof(string))
            {
                field.SetValue(null, value ?? string.Empty);
                return;
            }

            var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (property != null && property.CanWrite && property.PropertyType == typeof(string))
            {
                property.SetValue(null, value ?? string.Empty, null);
                return;
            }

            throw new MissingMemberException(type.FullName, memberName);
        }
    }
}
