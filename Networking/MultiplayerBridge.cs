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
        private const string PluginGuid = "KrokoshaCasualtiesMP";
        private const string MpTypeName = "KrokoshaCasualtiesMP.KrokoshaScavMultiplayer";
        private const string NetTypeName = "KrokoshaCasualtiesMP.Net";
        private const string NetTypeEnumName = "KrokoshaCasualtiesMP.Net+NetType";
        private const string ServerMainTypeName = "KrokoshaCasualtiesMP.ServerMain";
        private const string ClientMainTypeName = "KrokoshaCasualtiesMP.ClientMain";
        private const string LiteNetTransportTypeName = "KrokoshaCasualtiesMP.TransportLiteNetLib";
        private const string MessageField = "msg";
        private const string ChannelField = "channel";
        private const string KindField = "kind";
        private const string RequestIdField = "requestId";
        private const string SenderField = "sender";
        private const string PayloadField = "payload";
        private const ushort RequestMessageId = 56420;
        private const ushort ResponseMessageId = 56421;

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
        private static Type _netModeType;
        private static Type _serverMainType;
        private static Type _clientMainType;
        private static Type _liteNetTransportType;
        private static Type _deliveryMethodType;
        private static Type _readerType;
        private static Type _writerType;
        private static MethodInfo _createWriterMethod;
        private static MethodInfo _clientSendMethod;
        private static MethodInfo _serverSendToMethod;
        private static MethodInfo _serverSendToClientsMethod;
        private static MethodInfo _registerServerReceiverMethod;
        private static MethodInfo _registerClientReceiverMethod;
        private static MethodInfo _writerPutStringMethod;
        private static MethodInfo _readerGetStringMethod;
        private static MethodInfo _liteNetConnectMethod;
        private static MethodInfo _serverAnnounceGameStartMethod;
        private static object _reliableOrdered;
        private static object _reliableUnordered;

        // KrokMP's Net.Server_SendToClients overloads take `in` (byref) parameters.
        // Mono's reflection invoke requires every byref argument in the argument
        // array to be the EXACT parameter type (it must be able to write the value
        // back through the pointer), while CUCoreLib passes the AllClientIds /
        // AllClientIdsExceptHost collections whose runtime type is List<knetid>.
        // That never matches the byref IEnumerable<knetid> parameter exactly, so a
        // plain _serverSendToClientsMethod.Invoke(...) throws ArgumentException and
        // every server-side Broadcast silently fails. _serverSendToClientsInvoker is
        // a DynamicMethod with plain (non-byref) parameters that forwards the call
        // with proper managed pointers; because all of its parameters are value
        // parameters, MethodInfo.Invoke only performs a normal assignability check.
        // _serverSendToClientsInvokerSource tracks which MethodInfo the invoker was
        // built for, because TryResolveRuntime re-resolves the field on its retry
        // schedule and the invoker must be rebuilt whenever the source changes.
        private static MethodInfo _serverSendToClientsInvoker;
        private static MethodInfo _serverSendToClientsInvokerSource;
        private static int _dynamicMethodCounter;

        // KrokMP's Net.ShutdownReset() clears the SERVER_MESSAGE_HANDLERS /
        // CLIENT_MESSAGE_HANDLERS tables where CUCoreLib registers its 56420/56421
        // receivers, and KrokMP does not re-run any third-party registration when
        // the next transport is created. Net.TransportCreated is therefore hooked
        // with Harmony (the same proven approach KrokMpCucorelibBridgeFix used) so
        // CUCoreLib can re-install its receivers right when every new session
        // starts; otherwise the bridge silently stops receiving every message from
        // the second session onward.
        private static Harmony _harmony;
        private static bool _transportHookInstalled;

        public static bool IsAvailable { get; private set; }

        public static bool IsRunning => GetNetBool("running");
        public static bool IsClient => GetNetBool("is_client");
        public static bool IsServer => GetNetBool("is_server");
        public static bool IsHost => GetNetBool("is_host");
        public static bool IsConnected => GetNetBool("is_connected");

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
                var running = netType?.GetProperty("running", BindingFlags.Public | BindingFlags.Static);
                if (running?.PropertyType != typeof(bool))
                    return KrokMpSaveScope.Unsupported;
                if (!GetStaticBool(running))
                    return KrokMpSaveScope.NotActive;

                var isClient = netType.GetProperty("is_client", BindingFlags.Public | BindingFlags.Static);
                if (isClient?.PropertyType == typeof(bool) && GetStaticBool(isClient))
                    return KrokMpSaveScope.Client;

                var savesType = _krokAssembly.GetType("KrokoshaCasualtiesMP.SavesystemPatch", false);
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
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib could not resolve the KrokMP save scope.\n" + ex);
                return KrokMpSaveScope.Unsupported;
            }
        }

        public static bool TryConfigureLocalIdentity(string username, string address)
        {
            if (!TryResolveRuntime()) return false;
            if (_mpType == null) return false;

            try
            {
                SetStaticStringProperty(_mpType, "INPUT_USERNAME", username);
                SetStaticStringProperty(_mpType, "INPUT_IPPORT", address);
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
            if (TryResolveRuntime())
            {
                InstallReceivers();
                InstallKrokMpTransportHook();
                IsAvailable = true;
                CUCoreLibPlugin.Log?.LogInfo("CUCoreLib multiplayer bridge is ready.");
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
            return SendMessage(RequestMessageId, channel, "event", payload, reliable, null, 0u, null);
        }

        public static bool RequestServer(string channel, object payload, Action<JToken> onResponse,
            bool reliable = true)
        {
            var requestId = Guid.NewGuid().ToString("N");
            if (onResponse != null) PendingResponses[requestId] = onResponse;

            var sent = SendMessage(RequestMessageId, channel, "request", payload, reliable, requestId, 0u, null);
            if (!sent) PendingResponses.Remove(requestId);
            return sent;
        }

        public static bool SendToClient(uint clientId, string channel, object payload = null, bool reliable = true)
        {
            return SendMessage(ResponseMessageId, channel, "event", payload, reliable, null, clientId, null);
        }

        public static bool Broadcast(string channel, object payload = null, bool includeHost = false,
            bool reliable = true)
        {
            if (!IsAvailable || !IsServer) return false;

            var targets = includeHost ? GetMemberList("AllClientIds") : GetMemberList("AllClientIdsExceptHost");
            return SendMessage(ResponseMessageId, channel, "event", payload, reliable, null, 0u, targets);
        }

        internal static JToken NormalizePayload(object payload)
        {
            if (payload == null) return null;

            return payload is JToken token ? token : JToken.FromObject(payload);
        }

        internal static void HandleServerMessageObject(object senderClientId, object reader)
        {
            HandleEnvelope(ConvertClientIdToUInt(senderClientId), reader, true);
        }

        internal static void HandleClientMessageObject(object senderClientId, object reader)
        {
            HandleEnvelope(ConvertClientIdToUInt(senderClientId), reader, false);
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
        }

        private static bool SendMessage(ushort messageId, string channel, string kind, object payload, bool reliable,
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

            return SendEnvelope(messageId, envelope, reliable, clientId, targets);
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

            return SendEnvelope(ResponseMessageId, envelope, reliable, clientId, null);
        }

        private static bool SendEnvelope(ushort messageId, JObject envelope, bool reliable, uint clientId,
            object targets)
        {
            if (!TryBuildWriter(messageId, envelope, out var writer)) return false;

            var delivery = reliable ? _reliableOrdered : _reliableUnordered;
            try
            {
                if (targets != null)
                {
                    // Never invoke _serverSendToClientsMethod directly: KrokMP
                    // declares it with `in` parameters, and Mono's reflection
                    // invoke requires exact types for byref arguments, which the
                    // List<knetid> targets collection can never satisfy. The
                    // invoker wraps the call with plain value parameters.
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

        private static bool TryBuildWriter(ushort messageId, JObject envelope, out object writer)
        {
            writer = null;
            if (_createWriterMethod == null) return false;

            try
            {
                writer = _createWriterMethod.Invoke(null, new object[] { messageId });
                if (writer == null) return false;

                var json = JsonConvert.SerializeObject(envelope, Formatting.None);
                var encoded = Convert.ToBase64String(CUCoreUtils.CompressGZip(Encoding.UTF8.GetBytes(json)));

                if (_writerPutStringMethod != null)
                {
                    _writerPutStringMethod.Invoke(null, new[] { writer, encoded, true });
                    return true;
                }

                var putString = writer.GetType().GetMethod("Put", new[] { typeof(string) });
                if (putString != null)
                {
                    putString.Invoke(writer, new object[] { encoded });
                    return true;
                }
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

        private static void InstallReceivers()
        {
            var registerServer = _registerServerReceiverMethod;
            var registerClient = _registerClientReceiverMethod;
            if (registerServer != null)
            {
                var serverDelegate = CreateReceiverDelegate(registerServer,
                    typeof(MultiplayerBridge).GetMethod(nameof(HandleServerMessageObject),
                        BindingFlags.NonPublic | BindingFlags.Static));
                if (serverDelegate != null)
                    TryInstallReceiver(registerServer, "SERVER_MESSAGE_HANDLERS", RequestMessageId, serverDelegate);
            }

            if (registerClient == null) return;
            var clientDelegate = CreateReceiverDelegate(registerClient,
                typeof(MultiplayerBridge).GetMethod(nameof(HandleClientMessageObject),
                    BindingFlags.NonPublic | BindingFlags.Static));
            if (clientDelegate != null)
                TryInstallReceiver(registerClient, "CLIENT_MESSAGE_HANDLERS", ResponseMessageId, clientDelegate);
        }

        private static bool TryInstallReceiver(MethodInfo registerMethod, string handlerFieldName, ushort messageId,
            Delegate receiver)
        {
            if (registerMethod == null || receiver == null) return false;

            if (IsReceiverRegistered(registerMethod.DeclaringType, handlerFieldName, messageId)) return true;

            try
            {
                registerMethod.Invoke(null, new object[] { messageId, receiver });
                return true;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException &&
                                                        IsReceiverRegistered(registerMethod.DeclaringType,
                                                            handlerFieldName, messageId))
            {
                return true;
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib could not register KrokMP receiver " + messageId + ".\n" +
                                                ex);
                return false;
            }
        }

        private static bool IsReceiverRegistered(Type netType, string handlerFieldName, ushort messageId)
        {
            if (netType == null || string.IsNullOrWhiteSpace(handlerFieldName)) return false;

            var field = netType.GetField(handlerFieldName, BindingFlags.NonPublic | BindingFlags.Static);
            if (!(field?.GetValue(null) is IDictionary handlers)) return false;

            return handlers.Contains(messageId);
        }

        private static void InstallKrokMpTransportHook()
        {
            if (_transportHookInstalled || _harmony == null || _netType == null || _netModeType == null) return;

            try
            {
                // Same proven approach as KrokMpCucorelibBridgeFix: hook KrokMP's
                // Net.TransportCreated with a Harmony postfix so the receivers are
                // re-registered exactly when every new session's transport starts,
                // immediately after KrokMP's ShutdownReset has wiped them.
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
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib could not hook KrokMP transport creation; " +
                                                "multiplayer receivers will not be re-installed after a session restart.\n" +
                                                ex);
                return;
            }

            // If a transport was already created before this hook could be
            // installed (for example while the bridge was still waiting for the
            // KrokMP assembly to load), run the same recovery path once now so the
            // receivers are present for the session that is already active.
            try
            {
                if (IsRunning) HandleKrokMpTransportCreated();
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib failed to restore receivers for an active KrokMP session.\n" +
                                                ex);
            }
        }

        private static void HandleKrokMpTransportCreated()
        {
            if (!IsAvailable) return;

            // Re-register the 56420/56421 receivers that KrokMP's ShutdownReset()
            // wiped from the handler tables. InstallReceivers is idempotent: it
            // checks whether each message id is already registered before
            // registering it, so running it on every transport creation is safe.
            InstallReceivers();

            // A client may have already consumed its one-shot initial snapshot
            // guards in a previous session; re-arm them and pull a fresh snapshot
            // for the newly created transport after a short delay (the connection
            // handshake usually completes within that window).
            if (IsClient)
                CUCoreUtils.DelayCall(3f, MultiplayerSyncRegistry.RequestInitialSnapshotForNewSession);
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
            if (invokeParams.Length < 2) return null;

            var readerRefType = invokeParams[1].ParameterType;
            var readerType = readerRefType.IsByRef ? readerRefType.GetElementType() : readerRefType;
            if (readerType == null) return null;

            var method = new DynamicMethod(
                "CUCoreLib_MP_Receiver_" + helperMethod.Name,
                typeof(void),
                new[] { invokeParams[0].ParameterType, readerRefType },
                typeof(MultiplayerBridge).Module,
                true);

            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            var senderType = invokeParams[0].ParameterType;
            if (senderType.IsValueType)
                il.Emit(OpCodes.Box, senderType);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldind_Ref);
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
            InstallReceivers();
            InstallKrokMpTransportHook();
            IsAvailable = true;
            CUCoreLibPlugin.Log?.LogInfo("CUCoreLib multiplayer bridge is ready.");
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
            _netModeType = _krokAssembly.GetType(NetTypeEnumName, false);
            _serverMainType = _krokAssembly.GetType(ServerMainTypeName, false);
            _clientMainType = _krokAssembly.GetType(ClientMainTypeName, false);
            _liteNetTransportType = _krokAssembly.GetType(LiteNetTransportTypeName, false);
            if (_mpType == null || _netType == null || _netModeType == null || _serverMainType == null ||
                _clientMainType == null || _liteNetTransportType == null) return false;

            var liteNetLibAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, "LiteNetLib", StringComparison.OrdinalIgnoreCase));
            if (liteNetLibAssembly == null) return false;

            _readerType = liteNetLibAssembly.GetType("LiteNetLib.Utils.NetDataReader", false);
            _writerType = liteNetLibAssembly.GetType("LiteNetLib.Utils.NetDataWriter", false);
            if (_readerType == null || _writerType == null) return false;

            _deliveryMethodType = ResolveDeliveryMethodType();
            if (_deliveryMethodType == null) return false;

            _createWriterMethod = ResolveMethod(_netType, new[] { "CreateWriter" }, new[] { typeof(ushort) });
            _clientSendMethod = ResolveMethod(_netType, new[] { "Client_Send" },
                new[] { _deliveryMethodType, _writerType });
            _serverSendToMethod = ResolveMethod(_netType, new[] { "Server_SendTo" },
                new[] { _deliveryMethodType, _writerType, typeof(uint) });
            _serverSendToClientsMethod = ResolveSendToClientsMethod(_netType, _deliveryMethodType, _writerType);
            _registerServerReceiverMethod = ResolveMethod(_netType,
                new[] { "RegisterServerReceiver", "RegisterServerReciever" }, new[] { typeof(ushort), null });
            _registerClientReceiverMethod = ResolveMethod(_netType,
                new[] { "RegisterClientReceiver", "RegisterClientReciever" }, new[] { typeof(ushort), null });
            _writerPutStringMethod = ResolveStringPutMethod();
            _readerGetStringMethod = ResolveStringGetMethod();
            _liteNetConnectMethod = ResolveMethod(_liteNetTransportType, new[] { "OnWantToConnect" },
                new[] { typeof(string), _netModeType });
            _serverAnnounceGameStartMethod = ResolveMethod(_serverMainType, new[] { "Server_Announce_GAME_START" },
                Type.EmptyTypes);

            if (_createWriterMethod == null || _clientSendMethod == null || _serverSendToMethod == null ||
                _serverSendToClientsMethod == null || _registerServerReceiverMethod == null ||
                _registerClientReceiverMethod == null || _liteNetConnectMethod == null ||
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
            //   (in DeliveryMethod, in NetDataWriter, in IReadOnlyList<NetPlayer>)
            //   (in DeliveryMethod, in NetDataWriter, in IEnumerable<knetid>)
            // A loose typeof(IEnumerable) filter matches every one of them, and the
            // enumeration order of GetMethods is not contractual, so the generic
            // ResolveMethod call could pick the IReadOnlyList<NetPlayer> overload
            // even though Broadcast passes a List<knetid>. Pick the overload whose
            // target collection is an IEnumerable<T> of a client-id-like element
            // type (knetid is a struct carrying a public "id" field) - that is the
            // overload CUCoreLib actually needs, and it makes the runtime cast
            // inside the DynamicMethod invoker succeed for the real target lists.
            var candidates = netType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(candidate => string.Equals(candidate.Name, "Server_SendToClients", StringComparison.Ordinal))
                .Select(candidate => new { Method = candidate, Parameters = candidate.GetParameters() })
                .Where(candidate => candidate.Parameters.Length == 3 &&
                                    ParameterMatches(deliveryMethodType, candidate.Parameters[0].ParameterType) &&
                                    ParameterMatches(writerType, candidate.Parameters[1].ParameterType))
                .ToArray();

            foreach (var candidate in candidates)
            {
                var targetsType = UnwrapByRef(candidate.Parameters[2].ParameterType);
                if (targetsType == null || !targetsType.IsGenericType) continue;
                if (targetsType.GetGenericTypeDefinition() != typeof(IEnumerable<>)) continue;

                var elementType = targetsType.GetGenericArguments()[0];
                if (IsClientIdType(elementType)) return candidate.Method;
            }

            foreach (var candidate in candidates)
            {
                var targetsType = UnwrapByRef(candidate.Parameters[2].ParameterType);
                if (targetsType == null || !targetsType.IsGenericType) continue;
                if (targetsType.GetGenericTypeDefinition() != typeof(IEnumerable<>)) continue;

                var elementType = targetsType.GetGenericArguments()[0];
                if (elementType.IsValueType) return candidate.Method;
            }

            // Fall back to the legacy loose resolution so a future KrokMP
            // signature change degrades to the previous behaviour instead of
            // failing the whole bridge resolution outright.
            return ResolveMethod(netType, new[] { "Server_SendToClients" },
                new[] { deliveryMethodType, writerType, typeof(IEnumerable) });
        }

        private static MethodInfo GetSendToClientsInvoker()
        {
            // TryResolveRuntime re-runs on the retry schedule and re-resolves the
            // field every time, so the invoker must be rebuilt whenever the
            // underlying MethodInfo reference changes.
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

            // If the resolved overload is already declared with plain value
            // parameters, MethodInfo.Invoke performs a standard assignability
            // check on each argument and no wrapper is needed.
            if (!parameters.Any(parameter => parameter.ParameterType.IsByRef)) return method;

            var deliveryType = UnwrapByRef(parameters[0].ParameterType);
            var writerType = UnwrapByRef(parameters[1].ParameterType);
            var targetsType = UnwrapByRef(parameters[2].ParameterType);
            // The wrapper narrows the boxed targets object with a castclass, which
            // is only legal for reference types. If a future KrokMP signature ever
            // used a value type here, fall back to the raw method (the old
            // behaviour) rather than emitting invalid IL.
            if (deliveryType == null || writerType == null || targetsType == null || targetsType.IsValueType)
                return method;

            // The wrapper forwards to the original `in` signature. C# `in`
            // parameters are emitted as byref parameters carrying a
            // modreq(IsReadOnlyAttribute); Mono's JIT ignores custom modifiers
            // when verifying call sites, so pushing the addresses of locals is
            // sufficient and the wrapper verifies fine.
            //
            // IL:
            //   ldarg.0                -> stloc.0 (delivery)
            //   ldarg.1                -> stloc.1 (writer)
            //   ldarg.2 (object)       -> castclass targetsType -> stloc.2
            //   ldloca.0, ldloca.1, ldloca.2
            //   call Server_SendToClients
            //   ret
            //
            // The third wrapper parameter is deliberately declared as `object`
            // (instead of the unknown IEnumerable<knetid> type) so that
            // MethodInfo.Invoke accepts the List<knetid> argument via its normal
            // assignability check; the castclass in the IL then narrows it to the
            // exact interface type the original method expects.
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
            var extensions = _krokAssembly.GetType("KrokoshaCasualtiesMP.MyLiteNetLibExtensions", false);
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
            var extensions = _krokAssembly.GetType("KrokoshaCasualtiesMP.MyLiteNetLibExtensions", false);
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

            var property = _netType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static);
            if (property == null || property.PropertyType != typeof(bool)) return false;

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

        private static void SetStaticStringProperty(Type type, string propertyName, string value)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (property == null || !property.CanWrite || property.PropertyType != typeof(string))
                throw new MissingMemberException(type.FullName, propertyName);

            property.SetValue(null, value ?? string.Empty, null);
        }
    }
}
