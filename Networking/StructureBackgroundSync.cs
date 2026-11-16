using System.Collections;
using System.Collections.Generic;
using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Networking
{
    internal static class StructureBackgroundSync
    {
        private const string Channel = "cucorelib.structures.backgrounds";
        // We do a funny thing where we turn all of it into a singular gameobject and natively broadcast it over :)

        private static readonly List<(string Id, Vector2 Position)> HostPlacements =
            new List<(string, Vector2)>();

        private static readonly HashSet<(string Id, Vector2 Position)> AppliedPlacements =
            new HashSet<(string, Vector2)>();

        private static readonly Queue<(int Epoch, string Id, Vector2 Position)> PendingPlacements =
            new Queue<(int, string, Vector2)>();

        private static bool _registered;
        private static int _hostEpoch;
        private static int _clientEpoch;
        private static bool _worldReady;
        private static bool _awaitingRecord;
        private static bool _recordRequestInFlight;
        private static int _requestGeneration;

        internal static void Register()
        {
            if (_registered) return;
            _registered = true;

            MultiplayerBridge.RegisterServerHandler(Channel, HandleServerRequest);
            MultiplayerBridge.RegisterClientHandler(Channel, HandleClientMessage);
            CUCoreUtils.StartCoroutine(ClientWatchRoutine());
        }

        internal static void OnHostWorldGeneration()
        {
            if (!MultiplayerBridge.IsRunning || !MultiplayerBridge.IsServer) return;

            HostPlacements.Clear();
            _hostEpoch++;
        }

        internal static void NotifyHostPlacement(string id, Vector2 position)
        {
            if (!MultiplayerBridge.IsRunning || !MultiplayerBridge.IsServer) return;

            HostPlacements.Add((id, position));

            var world = WorldGeneration.world;
            if (world != null && world.generatingWorld) return;

            MultiplayerBridge.Broadcast(Channel, new JObject
            {
                ["epoch"] = _hostEpoch,
                ["id"] = id,
                ["x"] = position.x,
                ["y"] = position.y
            });
        }

        private static JToken HandleServerRequest(JToken payload)
        {
            var placements = new JArray();
            foreach (var placement in HostPlacements)
            {
                placements.Add(new JObject
                {
                    ["id"] = placement.Id,
                    ["x"] = placement.Position.x,
                    ["y"] = placement.Position.y
                });
            }

            return new JObject
            {
                ["epoch"] = _hostEpoch,
                ["placements"] = placements
            };
        }

        private static void HandleClientMessage(JToken payload)
        {
            if (!(payload is JObject obj)) return;

            var id = obj.Value<string>("id");
            if (string.IsNullOrWhiteSpace(id)) return;

            var epoch = obj.Value<int?>("epoch") ?? 0;
            var position = new Vector2(obj.Value<float?>("x") ?? 0f, obj.Value<float?>("y") ?? 0f);

            if (!_worldReady || _awaitingRecord || epoch != _clientEpoch)
            {
                PendingPlacements.Enqueue((epoch, id, position));
                return;
            }

            ApplyPlacement(id, position);
        }

        private static IEnumerator ClientWatchRoutine()
        {
            var wait = new WaitForSecondsRealtime(0.5f);
            while (true)
            {
                if (MultiplayerBridge.IsAvailable && MultiplayerBridge.IsRunning && MultiplayerBridge.IsClient)
                {
                    var ready = CUCoreUtils.IsWorldGenerationReady();
                    if (ready && !_worldReady)
                    {
                        AppliedPlacements.Clear();
                        PendingPlacements.Clear();
                        _awaitingRecord = true;
                        _requestGeneration++;
                    }

                    _worldReady = ready;

                    if (_worldReady && _awaitingRecord && !_recordRequestInFlight)
                    {
                        var generation = _requestGeneration;
                        _recordRequestInFlight = MultiplayerBridge.RequestServer(Channel,
                            new JObject { ["request"] = true },
                            response =>
                            {
                                _recordRequestInFlight = false;
                                if (generation != _requestGeneration) return;
                                if (response is JObject record && record["placements"] is JArray placements)
                                    ApplyRecord(record.Value<int?>("epoch") ?? 0, placements);
                            });
                    }
                }
                else
                {
                    _worldReady = false;
                    _awaitingRecord = false;
                    _recordRequestInFlight = false;
                }

                yield return wait;
            }
        }

        private static void ApplyRecord(int epoch, JArray placements)
        {
            _clientEpoch = epoch;
            _awaitingRecord = false;
            AppliedPlacements.Clear();

            foreach (var token in placements)
            {
                if (!(token is JObject obj)) continue;
                var id = obj.Value<string>("id");
                if (string.IsNullOrWhiteSpace(id)) continue;

                ApplyPlacement(id, new Vector2(obj.Value<float?>("x") ?? 0f, obj.Value<float?>("y") ?? 0f));
            }

            while (PendingPlacements.Count > 0)
            {
                var pending = PendingPlacements.Dequeue();
                if (pending.Epoch == _clientEpoch) ApplyPlacement(pending.Id, pending.Position);
            }
        }

        private static void ApplyPlacement(string id, Vector2 position)
        {
            if (!AppliedPlacements.Add((id, position))) return;
            if (!StructureRegistry.TrySpawnBackgrounds(id, position))
                AppliedPlacements.Remove((id, position));
        }
    }
}