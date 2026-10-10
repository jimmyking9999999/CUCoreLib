using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CUCoreLib.Helpers;
using UnityEngine;

namespace CUCoreLib.Networking
{
    //   A
    //   (server not ready) client sends message 205 once for player placement, but host silently returns when the 
    //   server is not yet in-world, losing the req.
    //
    //   B
    //   (client body not replicated) once the server does answer, it sends a single reminder
    //   pack, which wins over the replication, gets dropped, and the client is stuck forever >.>
    
    internal static class SpawnHandshakeRecovery
    {
        private const float IntervalSeconds = 0.5f;
        private const byte SpawnRequestMessageId = 205;

        private const string MpTypeName = "Together.Multiplayer";
        private const string ClientMainTypeName = "Together.ClientMain";
        private const string ServerMainTypeName = "Together.ServerMain";
        private const string ScavPlayerTypeName = "Together.ScavPlayer";
        private const string ReminderFlagFieldName = "_last_reminderpack_while_generating_received";
        private const string AllPlayersExceptHostPropertyName = "AllPlayersExceptHost";
        private const string ServerPlayerStateFieldName = "ServerPlayerState";
        private const string IsLocalPropertyName = "IsLocal";
        private const string RemindMethodName = "Server_RemindPlayersCurrentState";
        private const string DidGiveSpawnFieldName = "did_give_spawn_location";
        private const string IsLoadedInFieldName = "is_loaded_in";

        private static bool _scheduled;

        private static bool _clientRefsResolved;
        private static bool _clientRefsFailed;
        private static Action<byte, bool, bool> _sendSpawnRequest;
        private static FieldInfo _reminderFlagField;

        private static bool _hostRefsResolved;
        private static bool _hostRefsFailed;
        private static PropertyInfo _allPlayersExceptHostProperty;
        private static PropertyInfo _isLocalProperty;
        private static FieldInfo _serverPlayerStateField;
        private static MethodInfo _remindMethod;
        private static FieldInfo _didGiveSpawnField;
        private static FieldInfo _isLoadedInField;

        internal static void Schedule()
        {
            if (_scheduled) return;

            _scheduled = true;
            CUCoreUtils.CallWhen(
                () => MultiplayerBridge.IsAvailable && MultiplayerBridge.IsRunning && MultiplayerBridge.IsConnected,
                () => CUCoreUtils.StartCoroutine(Run()), 1f);
        }

        private static IEnumerator Run()
        {
            var wait = new WaitForSecondsRealtime(IntervalSeconds);
            while (MultiplayerBridge.IsAvailable && MultiplayerBridge.IsRunning && MultiplayerBridge.IsConnected)
            {
                if (MultiplayerBridge.IsServer)
                    RemindUnloadedClients();
                else if (MultiplayerBridge.IsClient)
                    ResendSpawnRequest();

                yield return wait;
            }

            _scheduled = false;
            Schedule();
        }

        private static bool ClientInGeneratedWorld(out WorldGeneration world)
        {
            world = WorldGeneration.world;
            return world != null && !world.generatingWorld;
        }


        private static void ResendSpawnRequest()
        {
            if (!EnsureClientRefs()) return;
            if (!ClientInGeneratedWorld(out _)) return;   // client has not finished its own worldgen yet
            if (ReminderPackReceived()) return;           // server already answered, we're good

            try
            {
                _sendSpawnRequest(SpawnRequestMessageId, WorldGeneration.unchipped, true);
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib spawn-handshake recovery failed to resend message 205.\n" + ex);
            }
        }

        private static bool ReminderPackReceived()
        {
            try
            {
                return _reminderFlagField != null && (bool)_reminderFlagField.GetValue(null);
            }
            catch
            {
                // prevent being resent forever
                return true;
            }
        }

        private static bool EnsureClientRefs()
        {
            if (_clientRefsResolved) return true;
            if (_clientRefsFailed) return false;

            var assembly = FindKrokAssembly();
            if (assembly == null) return false;   // Keep on trying...

            var mpType = assembly.GetType(MpTypeName, false);
            var clientMainType = assembly.GetType(ClientMainTypeName, false);
            if (mpType == null || clientMainType == null)
            {
                _clientRefsFailed = true;
                return false;
            }

            _reminderFlagField = clientMainType.GetField(ReminderFlagFieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            var sendMethod = mpType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(method =>
                {
                    if (method.Name != "Client_SendSimpleMessageToServer") return false;
                    var parameters = method.GetParameters();
                    return parameters.Length == 3 &&
                           parameters[0].ParameterType == typeof(byte).MakeByRefType() &&
                           parameters[1].ParameterType == typeof(bool) &&
                           parameters[2].ParameterType == typeof(bool);
                });

            if (_reminderFlagField == null || sendMethod == null)
            {
                _clientRefsFailed = true;
                return false;
            }

            _sendSpawnRequest = BuildSendInvoker(sendMethod);
            if (_sendSpawnRequest == null)
            {
                _clientRefsFailed = true;
                return false;
            }

            _clientRefsResolved = true;
            return true;
        }

        private static Action<byte, bool, bool> BuildSendInvoker(MethodInfo method)
        {
            try
            {
                var invoker = new DynamicMethod(
                    "CUCoreLib_MP_SpawnHandshakeRecovery_Send",
                    typeof(void),
                    new[] { typeof(byte), typeof(bool), typeof(bool) },
                    typeof(SpawnHandshakeRecovery).Module,
                    true);

                var il = invoker.GetILGenerator();
                var nameLocal = il.DeclareLocal(typeof(byte));
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Stloc, nameLocal);
                il.Emit(OpCodes.Ldloca, nameLocal);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Call, method);
                il.Emit(OpCodes.Ret);

                return (Action<byte, bool, bool>)invoker.CreateDelegate(typeof(Action<byte, bool, bool>));
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib spawn-handshake recovery could not build its sender.\n" + ex);
                return null;
            }
        }


        private static void RemindUnloadedClients()
        {
            if (!EnsureHostRefs()) return;
            if (!ClientInGeneratedWorld(out _)) return;   // host not in-world yet

            object players;
            try
            {
                players = _allPlayersExceptHostProperty.GetValue(null, null);
            }
            catch (Exception ex)
            {
                CUCoreLibPlugin.Log?.LogWarning("CUCoreLib spawn-handshake recovery could not read the player list.\n" + ex);
                return;
            }

            if (!(players is IEnumerable enumerable)) return;

            foreach (var player in enumerable)
            {
                if (player == null) continue;

                try
                {
                    if ((bool)_isLocalProperty.GetValue(player, null)) continue;

                    var state = _serverPlayerStateField.GetValue(player);
                    if (state == null) continue;

                    if (!(bool)_didGiveSpawnField.GetValue(state)) continue;
                    if ((bool)_isLoadedInField.GetValue(state)) continue;

                    _remindMethod.Invoke(player, new object[] { false, true });
                }
                catch (Exception ex)
                {
                    CUCoreLibPlugin.Log?.LogWarning("CUCoreLib spawn-handshake recovery failed to remind a client.\n" + ex);
                }
            }
        }

        private static bool EnsureHostRefs()
        {
            if (_hostRefsResolved) return true;
            if (_hostRefsFailed) return false;

            var assembly = FindKrokAssembly();
            if (assembly == null) return false;   // Together not loaded yet

            var serverMainType = assembly.GetType(ServerMainTypeName, false);
            var scavPlayerType = assembly.GetType(ScavPlayerTypeName, false);
            if (serverMainType == null || scavPlayerType == null)
            {
                _hostRefsFailed = true;
                return false;
            }

            _allPlayersExceptHostProperty = serverMainType.GetProperty(AllPlayersExceptHostPropertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            _isLocalProperty = scavPlayerType.GetProperty(IsLocalPropertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _serverPlayerStateField = scavPlayerType.GetField(ServerPlayerStateFieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _remindMethod = scavPlayerType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(method =>
                {
                    if (method.Name != RemindMethodName) return false;
                    var parameters = method.GetParameters();
                    return parameters.Length == 2 &&
                           parameters[0].ParameterType == typeof(bool) &&
                           parameters[1].ParameterType == typeof(bool);
                });

            var stateType = _serverPlayerStateField?.FieldType;
            _didGiveSpawnField = stateType?.GetField(DidGiveSpawnFieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _isLoadedInField = stateType?.GetField(IsLoadedInFieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (_allPlayersExceptHostProperty == null || _isLocalProperty == null ||
                _serverPlayerStateField == null || _remindMethod == null ||
                _didGiveSpawnField == null || _isLoadedInField == null)
            {
                _hostRefsFailed = true;
                return false;
            }

            _hostRefsResolved = true;
            return true;
        }

        private static Assembly FindKrokAssembly()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(candidate => candidate.GetType(MpTypeName, false) != null);
        }
    }
}
