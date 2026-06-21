using CUCoreLib.Registries;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CUCoreLib.Saving
{
    internal sealed class BuiltInBuildingEntitySaveProvider : IWorldSaveProvider
    {
        public int GetVersion()
        {
            return 1;
        }

        public JToken Capture(WorldSaveContext context)
        {
            var buildings = new JArray();
            foreach (var runtime in BuildingEntityRegistry.GetActiveRuntimes())
            {
                if (runtime == null || string.IsNullOrWhiteSpace(runtime.DefinitionId)) continue;
                if (!runtime.TryGetComponent(out BuildingEntity building)) continue;

                buildings.Add(new JObject
                {
                    ["id"] = runtime.DefinitionId,
                    ["position"] = Vector2Token(runtime.transform.position),
                    ["rotation"] = runtime.transform.eulerAngles.z,
                    ["scale"] = Vector3Token(runtime.transform.localScale),
                    ["health"] = building.health,
                    ["blockPlacedOn"] = Vector2IntToken(building.blockPlacedOn)
                });
            }

            return buildings;
        }

        public void Restore(WorldSaveContext context, JToken payload, int version, SaveRestoreContext contextForRestore)
        {
            var buildings = payload as JArray;
            if (buildings == null) return;

            contextForRestore.Defer(() =>
            {
                foreach (var runtime in BuildingEntityRegistry.GetActiveRuntimes())
                    if (runtime != null)
                        Object.Destroy(runtime.gameObject);

                foreach (var token in buildings)
                {
                    var id = (string)token["id"];
                    if (!BuildingEntityRegistry.TryGetDefinition(id, out _))
                    {
                        CUCoreLibPlugin.Log?.LogWarning("CUCoreLib Save: Skipped unknown custom building '" + id +
                                                        "'.");
                        continue;
                    }

                    var position = ReadVector2(token["position"]);
                    var rotation = (float?)token["rotation"] ?? 0f;
                    var instance = BuildingEntityRegistry.Spawn(id, position, Quaternion.Euler(0f, 0f, rotation));
                    if (instance == null) continue;

                    instance.transform.localScale = ReadVector3(token["scale"], instance.transform.localScale);
                    if (instance.TryGetComponent(out BuildingEntity building))
                    {
                        building.health = (float?)token["health"] ?? building.health;
                        BuildingEntityRegistry.RestoreSeating(instance, context.World,
                            ReadVector2Int(token["blockPlacedOn"]));
                    }
                }
            });
        }

        private static JObject Vector2Token(Vector2 value)
        {
            return new JObject
            {
                ["x"] = value.x,
                ["y"] = value.y
            };
        }

        private static JObject Vector2IntToken(Vector2Int value)
        {
            return new JObject
            {
                ["x"] = value.x,
                ["y"] = value.y
            };
        }

        private static JObject Vector3Token(Vector3 value)
        {
            return new JObject
            {
                ["x"] = value.x,
                ["y"] = value.y,
                ["z"] = value.z
            };
        }

        private static Vector2 ReadVector2(JToken token)
        {
            return new Vector2((float?)token?["x"] ?? 0f, (float?)token?["y"] ?? 0f);
        }

        private static Vector2Int ReadVector2Int(JToken token)
        {
            return new Vector2Int((int?)token?["x"] ?? 0, (int?)token?["y"] ?? 0);
        }

        private static Vector3 ReadVector3(JToken token, Vector3 fallback)
        {
            if (token == null) return fallback;

            return new Vector3(
                (float?)token["x"] ?? fallback.x,
                (float?)token["y"] ?? fallback.y,
                (float?)token["z"] ?? fallback.z
            );
        }
    }
}