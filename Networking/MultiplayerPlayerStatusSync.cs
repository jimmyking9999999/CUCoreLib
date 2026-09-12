using System.Collections;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Networking
{
    internal static class MultiplayerPlayerStatusSync
    {
        private const float SyncSeconds = 1f;
        private static bool _scheduled;

        internal static void RegisterServerHandler()
        {
            MultiplayerBridge.RegisterServerHandler(MultiplayerSyncRegistry.PlayerStatusSnapshotChannel,
                (senderClientId, _) => MultiplayerApi.TryGetBodyFromClientId(senderClientId, out var body)
                    ? StatusRegistry.CaptureBodyNetworkSnapshot(body)
                    : new JObject());
        }

        internal static void Schedule()
        {
            if (_scheduled) return;
            _scheduled = true;
            CUCoreUtils.CallWhen(
                () => MultiplayerBridge.IsAvailable
                      && MultiplayerBridge.IsRunning
                      && MultiplayerBridge.IsClient 
                      && MultiplayerBridge.IsConnected
                      && CUCoreUtils.IsInWorld(),
                () => CUCoreUtils.StartCoroutine(Run()), 1f);
        }

        private static IEnumerator Run()
        {
            while (MultiplayerBridge.IsAvailable 
                   && MultiplayerBridge.IsRunning 
                   && MultiplayerBridge.IsClient 
                   && MultiplayerBridge.IsConnected
                   && CUCoreUtils.IsInWorld())
            {
                MultiplayerBridge.RequestServer(MultiplayerSyncRegistry.PlayerStatusSnapshotChannel, null, payload =>
                {
                    if (payload is JObject snapshot) StatusRegistry.ApplyNetworkSnapshot(snapshot);
                });
                yield return new WaitForSeconds(SyncSeconds);
            }

            _scheduled = false;
            Schedule();
        }
    }
}