using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using BepInEx.Bootstrap;
using CUCoreLib.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CUCoreLib.Networking
{
    public static class MultiplayerBridge
    {
        private const string PluginGuid = "KrokoshaCasualtiesMP";
        private const string MpTypeName = "KrokoshaCasualtiesMP.KrokoshaScavMultiplayer";
        private const string NetTypeName = "KrokoshaCasualtiesMP.Net";
        private const string ServerMainTypeName = "KrokoshaCasualtiesMP.ServerMain";
        private const string ClientMainTypeName = "KrokoshaCasualtiesMP.ClientMain";
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

        private static readonly Dictionary<string, Action<JToken>> ClientHandlers =
            new Dictionary<string, Action<JToken>>(StringComparer.Ordinal);

        private static readonly Dictionary<string, Action<JToken>> PendingResponses =
            new Dictionary<string, Action<JToken>>(StringComparer.Ordinal);

        private static bool _initialized;
        private static bool _retryScheduled;

        private static Assembly _krokAssembly;
        private static Type _mpType;
        private static Type _netType;
        private static Type _serverMainType;
        private static Type _clientMainType;
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
        private static object _reliableOrdered;
        private static object _reliableUnordered;

        public static bool IsAvailable { get; private set; }

        public static bool IsRunning => GetNetBool("running");
        public static bool IsClient => GetNetBool("is_client");
        public static bool IsServer => GetNetBool("is_server");
        public static bool IsHost => GetNetBool("is_host");

        public static void Initialize()
        {
            if (_initialized) return;

            _initialized = true;
            if (TryResolveRuntime())
            {
                InstallReceivers();
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

            return SendMessage(RequestMessageId, channel, "request", payload, reliable, requestId, 0u, null);
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

        internal static void HandleServerMessageObject(uint senderClientId, object reader)
        {
            HandleEnvelope(senderClientId, reader, true);
        }

        internal static void HandleClientMessageObject(uint senderClientId, object reader)
        {
            HandleEnvelope(senderClientId, reader, false);
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
                if (!string.IsNullOrWhiteSpace(requestId) && PendingResponses.TryGetValue(requestId, out var callback))
                {
                    PendingResponses.Remove(requestId);
                    callback(payload);
                }

                return;
            }

            if (serverSide)
            {
                if (!ServerHandlers.TryGetValue(channel, out var handler)) return;

                try
                {
                    var response = handler(payload);
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
                if (ClientHandlers.TryGetValue(channel, out var handler))
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
            string requestId, uint clientId, IReadOnlyList<uint> targets)
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
            IReadOnlyList<uint> targets)
        {
            if (!TryBuildWriter(messageId, envelope, out var writer)) return false;

            var delivery = reliable ? _reliableOrdered : _reliableUnordered;
            try
            {
                if (targets != null)
                {
                    _serverSendToClientsMethod.Invoke(null, new[] { delivery, writer, targets });
                    return true;
                }

                if (clientId != 0u || IsHost)
                {
                    _serverSendToMethod.Invoke(null, new[] { delivery, writer, clientId });
                    return true;
                }

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
            if (getString != null)
            {
                var value = getString.Invoke(reader, null);
                return value as string;
            }

            return null;
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
                    registerServer.Invoke(null, new object[] { RequestMessageId, serverDelegate });
            }

            if (registerClient != null)
            {
                var clientDelegate = CreateReceiverDelegate(registerClient,
                    typeof(MultiplayerBridge).GetMethod(nameof(HandleClientMessageObject),
                        BindingFlags.NonPublic | BindingFlags.Static));
                if (clientDelegate != null)
                    registerClient.Invoke(null, new object[] { ResponseMessageId, clientDelegate });
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
            if (invokeParams.Length < 2) return null;

            var readerRefType = invokeParams[1].ParameterType;
            var readerType = readerRefType.IsByRef ? readerRefType.GetElementType() : readerRefType;
            if (readerType == null) return null;

            var method = new DynamicMethod(
                "CUCoreLib_MP_Receiver_" + helperMethod.Name,
                typeof(void),
                new[] { typeof(uint), readerRefType },
                typeof(MultiplayerBridge).Module,
                true);

            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
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
            if (TryResolveRuntime())
            {
                InstallReceivers();
                IsAvailable = true;
                CUCoreLibPlugin.Log?.LogInfo("CUCoreLib multiplayer bridge is ready.");
            }
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
            _serverMainType = _krokAssembly.GetType(ServerMainTypeName, false);
            _clientMainType = _krokAssembly.GetType(ClientMainTypeName, false);
            if (_mpType == null || _netType == null || _serverMainType == null || _clientMainType == null) return false;

            var liteNetLibAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, "LiteNetLib", StringComparison.OrdinalIgnoreCase));
            if (liteNetLibAssembly == null) return false;

            _readerType = liteNetLibAssembly.GetType("LiteNetLib.Utils.NetDataReader", false);
            _writerType = liteNetLibAssembly.GetType("LiteNetLib.Utils.NetDataWriter", false);
            if (_readerType == null || _writerType == null) return false;

            _deliveryMethodType = ResolveDeliveryMethodType();
            if (_deliveryMethodType == null) return false;

            _createWriterMethod = ResolveMethod(_netType, "CreateWriter", new[] { typeof(ushort) });
            _clientSendMethod = ResolveMethod(_netType, "Client_Send", new[] { _deliveryMethodType, _writerType });
            _serverSendToMethod = ResolveMethod(_netType, "Server_SendTo",
                new[] { _deliveryMethodType, _writerType, typeof(uint) });
            _serverSendToClientsMethod = ResolveMethod(_netType, "Server_SendToClients",
                new[] { _deliveryMethodType, _writerType, typeof(IReadOnlyList<uint>) });
            _registerServerReceiverMethod =
                ResolveMethod(_netType, "RegisterServerReciever", new[] { typeof(ushort), null });
            _registerClientReceiverMethod =
                ResolveMethod(_netType, "RegisterClientReciever", new[] { typeof(ushort), null });
            _writerPutStringMethod = ResolveStringPutMethod();
            _readerGetStringMethod = ResolveStringGetMethod();

            if (_createWriterMethod == null || _clientSendMethod == null || _serverSendToMethod == null ||
                _serverSendToClientsMethod == null || _registerServerReceiverMethod == null ||
                _registerClientReceiverMethod == null) return false;

            _reliableOrdered = Enum.Parse(_deliveryMethodType, "ReliableOrdered");
            _reliableUnordered = Enum.Parse(_deliveryMethodType, "ReliableUnordered");
            return true;
        }

        private static bool IsKrokMpExpected()
        {
            if (_krokAssembly != null) return true;

            if (Chainloader.PluginInfos.ContainsKey(PluginGuid)) return true;

            return AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetType(MpTypeName, false) != null);
        }

        private static MethodInfo ResolveMethod(Type type, string methodName, Type[] parameterTypes)
        {
            if (type == null) return null;

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal)) continue;

                if (parameterTypes == null) return method;

                var parameters = method.GetParameters();
                if (parameters.Length != parameterTypes.Length) continue;

                var matches = true;
                for (var i = 0; i < parameters.Length; i++)
                    if (parameterTypes[i] != null && parameters[i].ParameterType != parameterTypes[i])
                    {
                        matches = false;
                        break;
                    }

                if (matches) return method;
            }

            return null;
        }

        private static Type ResolveDeliveryMethodType()
        {
            var method = ResolveMethod(_netType, "Client_Send", null);
            if (method == null) return null;

            var parameters = method.GetParameters();
            return parameters.Length > 0 ? parameters[0].ParameterType : null;
        }

        private static MethodInfo ResolveStringPutMethod()
        {
            var extensions = _krokAssembly.GetType("KrokoshaCasualtiesMP.MyLiteNetLibExtensions", false);
            if (extensions == null) return null;

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

        private static IReadOnlyList<uint> GetMemberList(string memberName)
        {
            if (_serverMainType == null) return Array.Empty<uint>();

            var property = _serverMainType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static);
            if (property != null)
            {
                var value = property.GetValue(null, null);
                if (value is IReadOnlyList<uint> list) return list;
            }

            return Array.Empty<uint>();
        }

        private static bool GetNetBool(string memberName)
        {
            if (_netType == null) return false;

            var property = _netType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static);
            if (property == null || property.PropertyType != typeof(bool)) return false;

            var value = property.GetValue(null, null);
            return value is bool flag && flag;
        }
    }
}