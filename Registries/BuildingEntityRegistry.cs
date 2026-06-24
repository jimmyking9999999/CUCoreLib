using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CUCoreLib.ContentReload;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace CUCoreLib.Registries
{
    public static class BuildingEntityRegistry
    {
        private static readonly Dictionary<string, CustomBuildingEntityDefinition> RegisteredDefinitions =
            new Dictionary<string, CustomBuildingEntityDefinition>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, GameObject> PrefabCache =
            new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<CustomBuildingRuntime> ActiveRuntimes =
            new HashSet<CustomBuildingRuntime>();

        private static readonly ItemDrop[] EmptyDrops = new ItemDrop[0];
        private static readonly string[] EmptyCategories = new string[0];
        private static readonly int GroundMask = LayerMask.GetMask("Ground");
        private static readonly int GroundLayer = LayerMask.NameToLayer("Ground");

        public static event Action<string, CustomBuildingEntityDefinition, bool> Registered;

        public static IReadOnlyDictionary<string, CustomBuildingEntityDefinition> RegisteredDefinitionsView
            => new ReadOnlyDictionary<string, CustomBuildingEntityDefinition>(RegisteredDefinitions);

        public static void Register(string id, CustomBuildingEntityDefinition definition)
        {
            ContentReloadSession.AssertNotActive("BuildingEntityRegistry.Register()", "Buildings are excluded from strict content reload.");

            if (string.IsNullOrWhiteSpace(id))
            {
                CUCoreLibPlugin.Log?.LogWarning("Ignored custom building registration with no ID.");
                return;
            }

            id = id.Trim();
            if (definition == null)
            {
                definition = new CustomBuildingEntityDefinition();
            }

            definition.ID = id;
            bool replacingExisting = RegisteredDefinitions.ContainsKey(id);
            RegisteredDefinitions[id] = definition;
            PrefabCache.Remove(id);
            Registered?.Invoke(id, definition, replacingExisting);

            if (!string.IsNullOrEmpty(definition.Name))
            {
                LocaleRegistry.Register("building", id, definition.Name);
            }

            if (!string.IsNullOrEmpty(definition.Description))
            {
                LocaleRegistry.Register("building", id + "dsc", definition.Description);
            }

            CUCoreLib.Networking.MultiplayerSyncRegistry.QueueHostSnapshotBroadcast();
        }

        public static bool TryGetDefinition(string id, out CustomBuildingEntityDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(id)) return false;

            return RegisteredDefinitions.TryGetValue(CUCoreLib.Helpers.SpawnIdHelpers.NormalizeSpawnId(id), out definition);
        }

        public static bool TryGetRegisteredDefinition(string id, out CustomBuildingEntityDefinition definition)
        {
            return TryGetDefinition(id, out definition);
        }

        public static bool TryGetPrefab(string id, out GameObject prefab)
        {
            prefab = GetOrCreatePrefab(id);
            return prefab != null;
        }

        public static IEnumerable<string> GetRegisteredIds()
        {
            return RegisteredDefinitions.Keys.ToArray();
        }

        public static IEnumerable<KeyValuePair<string, CustomBuildingEntityDefinition>> GetRegisteredDefinitions()
        {
            return RegisteredDefinitions.ToArray();
        }

        internal static void RegisterRuntime(CustomBuildingRuntime runtime)
        {
            if (runtime != null)
            {
                ActiveRuntimes.Add(runtime);
            }
        }

        internal static void UnregisterRuntime(CustomBuildingRuntime runtime)
        {
            if (runtime != null)
            {
                ActiveRuntimes.Remove(runtime);
            }
        }

        internal static CustomBuildingRuntime[] GetActiveRuntimes()
        {
            return ActiveRuntimes.Where(runtime => runtime != null && runtime.isActiveAndEnabled).ToArray();
        }

        internal static JObject CaptureNetworkSnapshot()
        {
            JObject root = new JObject();
            foreach (KeyValuePair<string, CustomBuildingEntityDefinition> entry in RegisteredDefinitions)
            {
                CustomBuildingEntityDefinition definition = entry.Value;
                if (definition == null)
                {
                    continue;
                }

                JObject building = new JObject
                {
                    ["name"] = definition.Name ?? string.Empty,
                    ["description"] = definition.Description ?? string.Empty,
                    ["sprite"] = NetworkSnapshotSerialization.WriteSprite(definition.Sprite),
                    ["sortingOrder"] = definition.SortingOrder,
                    ["useGlowPlantMaterial"] = definition.UseGlowPlantMaterial,
                    ["scaleX"] = definition.Scale.x,
                    ["scaleY"] = definition.Scale.y,
                    ["scaleZ"] = definition.Scale.z,
                    ["colliderSizeX"] = definition.ColliderSize?.x,
                    ["colliderSizeY"] = definition.ColliderSize?.y,
                    ["colliderOffsetX"] = definition.ColliderOffset?.x,
                    ["colliderOffsetY"] = definition.ColliderOffset?.y,
                    ["colliderIsTrigger"] = definition.ColliderIsTrigger,
                    ["layer"] = definition.Layer,
                    ["addRigidbody2D"] = definition.AddRigidbody2D,
                    ["rigidbodyBodyType"] = (int)definition.RigidbodyBodyType,
                    ["rigidbodyGravityScale"] = definition.RigidbodyGravityScale,
                    ["health"] = definition.Health,
                    ["requireGround"] = definition.RequireGround,
                    ["metallic"] = definition.Metallic,
                    ["cantHit"] = definition.CantHit,
                    ["animal"] = definition.Animal,
                    ["ignoreBodyOptimize"] = definition.IgnoreBodyOptimize,
                    ["dropChanceMultiplier"] = definition.DropChanceMultiplier,
                    ["placement"] = (int)definition.Placement,
                    ["generationStyle"] = (int)definition.GenerationStyle,
                    ["spawnMinPerChunk"] = definition.SpawnMinPerChunk,
                    ["spawnMaxPerChunk"] = definition.SpawnMaxPerChunk,
                    ["surfaceOffset"] = definition.SurfaceOffset,
                    ["randomFlip"] = definition.RandomFlip,
                    ["spawnInGround"] = definition.SpawnInGround,
                    ["hitSoundReferenceId"] = definition.HitSoundReferenceId ?? string.Empty,
                    ["hitSound"] = NetworkSnapshotSerialization.WriteStringOrEmpty(definition.HitSound != null ? definition.HitSound.name : null),
                    ["blockFootstepSoundId"] = definition.BlockFootstepSoundId,
                    ["renderReferenceId"] = definition.RenderReferenceId ?? string.Empty,
                    ["copyGlowPlantLayer"] = definition.CopyGlowPlantLayer,
                    ["heatRadius"] = definition.HeatRadius,
                    ["heatPerSecond"] = definition.HeatPerSecond,
                    ["maxHeatBodyTemperature"] = definition.MaxHeatBodyTemperature,
                    ["spawnComponents"] = definition.SpawnComponents != null ? JArray.FromObject(definition.SpawnComponents) : new JArray(),
                    ["components"] = NetworkSnapshotSerialization.WriteTypeNames(definition.Components)
                };

                if (definition.ItemsDropOnDestroy != null)
                {
                    building["itemsDropOnDestroy"] = JArray.FromObject(definition.ItemsDropOnDestroy);
                }

                if (definition.AlwaysDrop != null)
                {
                    building["alwaysDrop"] = JArray.FromObject(definition.AlwaysDrop);
                }

                if (definition.ItemCategoriesToAdd != null)
                {
                    building["itemCategoriesToAdd"] = JArray.FromObject(definition.ItemCategoriesToAdd);
                }

                root[entry.Key] = building;
            }

            return root;
        }

        internal static void ApplyNetworkSnapshot(JObject snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            foreach (JProperty property in snapshot.Properties())
            {
                string id = property.Name;
                JObject obj = property.Value as JObject;
                if (string.IsNullOrWhiteSpace(id) || obj == null)
                {
                    continue;
                }

                CustomBuildingEntityDefinition definition = new CustomBuildingEntityDefinition
                {
                    Name = obj.Value<string>("name"),
                    Description = obj.Value<string>("description"),
                    Sprite = NetworkSnapshotSerialization.ReadSprite(obj["sprite"]),
                    SortingOrder = obj.Value<int?>("sortingOrder") ?? 5,
                    UseGlowPlantMaterial = obj.Value<bool?>("useGlowPlantMaterial") ?? false,
                    Scale = new Vector3(
                        obj.Value<float?>("scaleX") ?? 1f,
                        obj.Value<float?>("scaleY") ?? 1f,
                        obj.Value<float?>("scaleZ") ?? 1f),
                    ColliderSize = obj["colliderSizeX"] != null || obj["colliderSizeY"] != null
                        ? new Vector2?(new Vector2(obj.Value<float?>("colliderSizeX") ?? 0f, obj.Value<float?>("colliderSizeY") ?? 0f))
                        : null,
                    ColliderOffset = obj["colliderOffsetX"] != null || obj["colliderOffsetY"] != null
                        ? new Vector2?(new Vector2(obj.Value<float?>("colliderOffsetX") ?? 0f, obj.Value<float?>("colliderOffsetY") ?? 0f))
                        : null,
                    ColliderIsTrigger = obj.Value<bool?>("colliderIsTrigger") ?? false,
                    Layer = obj.Value<int?>("layer"),
                    AddRigidbody2D = obj.Value<bool?>("addRigidbody2D") ?? false,
                    RigidbodyBodyType = (RigidbodyType2D)(obj.Value<int?>("rigidbodyBodyType") ?? 0),
                    RigidbodyGravityScale = obj.Value<float?>("rigidbodyGravityScale") ?? 0f,
                    Health = obj.Value<float?>("health") ?? 250f,
                    RequireGround = obj.Value<bool?>("requireGround") ?? true,
                    Metallic = obj.Value<bool?>("metallic") ?? false,
                    CantHit = obj.Value<bool?>("cantHit") ?? false,
                    Animal = obj.Value<bool?>("animal") ?? false,
                    IgnoreBodyOptimize = obj.Value<bool?>("ignoreBodyOptimize") ?? false,
                    DropChanceMultiplier = obj.Value<float?>("dropChanceMultiplier") ?? 1f,
                    Placement = (BuildingPlacementType)(obj.Value<int?>("placement") ?? 0),
                    GenerationStyle = (BuildingGenerationStyle)(obj.Value<int?>("generationStyle") ?? 0),
                    SpawnMinPerChunk = obj.Value<float?>("spawnMinPerChunk") ?? 0f,
                    SpawnMaxPerChunk = obj.Value<float?>("spawnMaxPerChunk") ?? 0f,
                    SurfaceOffset = obj.Value<float?>("surfaceOffset") ?? 0.5f,
                    RandomFlip = obj.Value<bool?>("randomFlip") ?? true,
                    SpawnInGround = obj.Value<bool?>("spawnInGround") ?? false,
                    HitSoundReferenceId = obj.Value<string>("hitSoundReferenceId"),
                    BlockFootstepSoundId = obj.Value<ushort?>("blockFootstepSoundId") ?? 0,
                    RenderReferenceId = obj.Value<string>("renderReferenceId"),
                    CopyGlowPlantLayer = obj.Value<bool?>("copyGlowPlantLayer") ?? false,
                    HeatRadius = obj.Value<float?>("heatRadius") ?? 0f,
                    HeatPerSecond = obj.Value<float?>("heatPerSecond") ?? 0f,
                    MaxHeatBodyTemperature = obj.Value<float?>("maxHeatBodyTemperature") ?? 0f,
                    SpawnComponents = (obj["spawnComponents"] as JArray)?.ToObject<List<string>>() ?? new List<string>(),
                    Components = NetworkSnapshotSerialization.ReadTypeNames(obj["components"])
                };

                JArray drops = obj["itemsDropOnDestroy"] as JArray;
                if (drops != null)
                {
                    definition.ItemsDropOnDestroy = drops.ToObject<ItemDrop[]>();
                }

                JArray alwaysDrop = obj["alwaysDrop"] as JArray;
                if (alwaysDrop != null)
                {
                    definition.AlwaysDrop = alwaysDrop.ToObject<ItemDrop[]>();
                }

                JArray categories = obj["itemCategoriesToAdd"] as JArray;
                if (categories != null)
                {
                    definition.ItemCategoriesToAdd = categories.ToObject<string[]>();
                }

                Register(id, definition);
            }
        }

        public static bool Contains(string id)
        {
            return IsRegistered(id);
        }

        public static GameObject Spawn(string id, Vector3 position)
        {
            return Spawn(id, position, Quaternion.identity);
        }

        public static GameObject Spawn(string id, Vector3 position, Quaternion rotation)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                CUCoreLibPlugin.Log?.LogWarning("No custom building ID provided.");
                return null;
            }

            id = CUCoreLib.Helpers.SpawnIdHelpers.NormalizeSpawnId(id);
            GameObject prefab = GetOrCreatePrefab(id);
            if (prefab == null)
            {
                CUCoreLibPlugin.Log?.LogWarning("Could not spawn custom building '" + id.Trim() + "' because it is not registered.");
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
            instance.SetActive(true);
            return instance;
        }

        public static GameObject PlaceOnSurface(string id, Vector2 origin, Vector2 direction, WorldGeneration world = null)
        {
            if (direction == Vector2.zero)
            {
                direction = Vector2.down;
            }

            if (!TryGetDefinition(id, out var definition)) return null;
            world = world ?? WorldGeneration.world;

            RaycastHit2D hit = Physics2D.Raycast(origin, direction.normalized, WorldGeneration.CHUNKSIZE, GroundMask);
            if (!hit) return null;

            Vector2 spawnPos = hit.point - direction.normalized * definition.SurfaceOffset;
            GameObject instance = Spawn(id, spawnPos, Quaternion.identity);
            if (instance == null) return null;

            ApplyPlacement(instance, definition, direction);
            SetBlockSeating(instance, world, hit.point + direction.normalized * 0.5f);
            return instance;
        }

        public static void DistributeInWorld(string id, WorldGeneration world)
        {
            if (world == null || !TryGetDefinition(id, out var definition)) return;
            if (definition.GenerationStyle == BuildingGenerationStyle.None) return;

            GameObject prefab = GetOrCreatePrefab(id);
            if (prefab == null) return;

            int count = Mathf.RoundToInt((float)(world.chunkWidth * world.chunkHeight) * UnityEngine.Random.Range(definition.SpawnMinPerChunk, definition.SpawnMaxPerChunk));
            for (int i = 0; i < count; i++)
            {
                if (definition.GenerationStyle == BuildingGenerationStyle.DropPod)
                {
                    DistributeDropPod(world, id, definition);
                }
                else
                {
                    DistributeStandard(world, id, definition);
                }
            }
        }

        public static ItemDrop AddDrop(string id, float chance = 1f, float conditionMin = 1f, float conditionMax = 1f)
        {
            return new ItemDrop
            {
                id = id,
                chance = chance,
                conditionMin = conditionMin,
                conditionMax = conditionMax
            };
        }

        public static ItemDrop AddDrop(ICollection<ItemDrop> drops, string id, float chance = 1f, float conditionMin = 1f, float conditionMax = 1f)
        {
            ItemDrop drop = AddDrop(id, chance, conditionMin, conditionMax);
            drops?.Add(drop);
            return drop;
        }

        internal static GameObject GetOrCreatePrefab(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            id = id.Trim();

            if (PrefabCache.TryGetValue(id, out GameObject cached) && cached != null)
            {
                return cached;
            }

            if (!RegisteredDefinitions.TryGetValue(id, out var definition))
            {
                CUCoreLibPlugin.Log?.LogWarning("No custom building definition was registered for '" + id + "'.");
                return null;
            }

            GameObject prefab = CreatePrefab(id, definition);
            if (prefab == null) return null;

            PrefabCache[id] = prefab;
            return prefab;
        }

        public static bool IsRegistered(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && RegisteredDefinitions.ContainsKey(id.Trim());
        }

        internal static void ApplyInstanceConfiguration(GameObject instance, string id)
        {
            if (instance == null || string.IsNullOrWhiteSpace(id)) return;
            if (!RegisteredDefinitions.TryGetValue(id.Trim(), out var definition)) return;

            definition.ConfigureInstance?.Invoke(instance);
        }

        internal static void SpawnDrops(BuildingEntity source, string id)
        {
            if (source == null || !TryGetDefinition(id, out var definition)) return;

            bool isNearPlayer = PlayerCamera.main != null &&
                PlayerCamera.main.body != null &&
                Vector2.Distance(source.transform.position, PlayerCamera.main.body.transform.position) < 8f;

            SpawnDropArray(source, definition.ItemsDropOnDestroy, definition.DropChanceMultiplier, isNearPlayer, rollChance: true);
            SpawnCategoryDrops(source, definition, isNearPlayer);
            SpawnDropArray(source, definition.AlwaysDrop, definition.DropChanceMultiplier, isNearPlayer, rollChance: false);
        }

        internal static void RestoreSeating(GameObject instance, WorldGeneration world, Vector2Int blockPlacedOn)
        {
            if (instance == null || world == null) return;
            if (!instance.TryGetComponent(out BuildingEntity building)) return;

            building.blockPlacedOn = blockPlacedOn;
            AttachSeatingListener(building, world);
        }

        private static GameObject CreatePrefab(string id, CustomBuildingEntityDefinition definition)
        {
            GameObject go = new GameObject(id);
            go.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(go);

            go.transform.localScale = definition.Scale == Vector3.zero ? Vector3.one : definition.Scale;
            GameObject renderReference = GetRenderReference(definition);
            go.layer = definition.Layer ?? GetReferenceLayer(renderReference, GroundLayer);

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = definition.Sprite;
            renderer.sortingOrder = definition.SortingOrder;
            if (!string.IsNullOrWhiteSpace(definition.SpriteAnimationId))
            {
                AssetLoader.TryApplyAnimation(renderer, definition.SpriteAnimationId);
            }
            SpriteRenderer referenceRenderer = renderReference != null ? renderReference.GetComponent<SpriteRenderer>() : null;
            if (referenceRenderer != null)
            {
                renderer.sharedMaterial = referenceRenderer.sharedMaterial;
            }

            if (definition.UseGlowPlantMaterial)
            {
                GameObject glowRef = Resources.Load<GameObject>("glowplant");
                SpriteRenderer glowRenderer = glowRef != null ? glowRef.GetComponent<SpriteRenderer>() : null;
                if (glowRenderer != null)
                {
                    renderer.sharedMaterial = glowRenderer.sharedMaterial;
                }
            }

            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            if (definition.ColliderSize.HasValue)
            {
                collider.size = definition.ColliderSize.Value;
            }
            else if (definition.Sprite != null)
            {
                collider.size = definition.Sprite.bounds.size;
            }

            if (definition.ColliderOffset.HasValue)
            {
                collider.offset = definition.ColliderOffset.Value;
            }
            else if (definition.Sprite != null)
            {
                collider.offset = definition.Sprite.bounds.center;
            }

            collider.isTrigger = definition.ColliderIsTrigger;

            if (definition.AddRigidbody2D)
            {
                Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = definition.RigidbodyBodyType;
                rb.gravityScale = definition.RigidbodyGravityScale;
            }

            BuildingEntity building = go.AddComponent<BuildingEntity>();
            ApplyBuildingFields(building, definition);

            CustomBuildingRuntime runtime = go.AddComponent<CustomBuildingRuntime>();
            runtime.DefinitionId = id;

            if (definition.Components != null)
            {
                foreach (Type componentType in definition.Components)
                {
                    if (componentType == null || !typeof(Component).IsAssignableFrom(componentType)) continue;
                    if (go.GetComponent(componentType) == null)
                    {
                        go.AddComponent(componentType);
                    }
                }
            }

            definition.ConfigurePrefab?.Invoke(go);
            return go;
        }

        private static void ApplyBuildingFields(BuildingEntity building, CustomBuildingEntityDefinition definition)
        {
            building.id = definition.ID;
            building.health = definition.Health;
            building.requireGround = definition.RequireGround;
            building.metallic = definition.Metallic;
            building.cantHit = definition.CantHit;
            building.animal = definition.Animal;
            building.ignoreBodyOptimize = definition.IgnoreBodyOptimize;
            building.dropChanceMultiplier = definition.DropChanceMultiplier;
            building.guaranteedDropAmount = 0;
            building.itemsDropOnDestroy = EmptyDrops;
            building.alwaysDrop = EmptyDrops;
            building.itemCategoriesToAdd = EmptyCategories;
            building.hitSound = definition.HitSound ?? ResolveHitSound(definition.HitSoundReferenceId);
            building.blockFootstepSoundId = definition.BlockFootstepSoundId;
            building.skipDescriptionSet = false;
        }

        private static AudioClip ResolveHitSound(string referenceId)
        {
            if (string.IsNullOrWhiteSpace(referenceId)) return null;

            string normalized = referenceId.Trim();
            switch (normalized.ToLower())
            {
                case "metal":
                    normalized = "turret";
                break;
                case "rubber":
                    normalized = "glowplant";
                break;
                case "plant":
                    normalized = "glowplant";
                break;
                case "rustle":
                    normalized = "geotree";
                break;  
                case "crystal":
                    normalized = "BloodCrystal";
                break;
                case "flesh":
                    normalized = "shadecrawler";
                break;
                case "pop":
                    normalized = "pop";
                break;
                case "ice":
                case "glass":
                    normalized = "icestalagmite";
                break;
                case "stone":     
                case "rock":               
                    normalized = "stoneplant";
                break;
                case "chain":
                    normalized = "barbedwirefence";
                break;

            }
            // TODO add more, add to documentation, and allow mods to specify 
            // - custom sound references
            // - exact buildingentity names as references
            // - tiles, for their sounds too

            // This should only be shorthand (!)

            GameObject reference = Resources.Load<GameObject>(normalized);
            if (reference != null && reference.TryGetComponent(out BuildingEntity building))
            {
                return building.hitSound;
            }

            CUCoreLibPlugin.Log?.LogWarning("Could not resolve building hit sound reference '" + referenceId.Trim() + "'.");
            return null;
        }

        private static GameObject GetRenderReference(CustomBuildingEntityDefinition definition)
        {
            string referenceId = string.IsNullOrWhiteSpace(definition.RenderReferenceId)
                ? "stoneplant"
                : definition.RenderReferenceId.Trim();

            GameObject reference = Resources.Load<GameObject>(referenceId);
            if (reference != null) return reference;

            CUCoreLibPlugin.Log?.LogWarning("Could not load building render reference '" + referenceId + "'.");
            return Resources.Load<GameObject>("stoneplant");
        }

        private static int GetReferenceLayer(GameObject reference, int fallback)
        {
            return reference != null ? reference.layer : fallback;
        }

        private static int GetReferenceLayer(string resourceId, int fallback)
        {
            GameObject reference = Resources.Load<GameObject>(resourceId);
            return reference != null ? reference.layer : fallback;
        }

        private static void DistributeStandard(WorldGeneration world, string id, CustomBuildingEntityDefinition definition)
        {
            Vector2 randomPos = new Vector2(
                UnityEngine.Random.Range(-(float)world.halfWidth, (float)world.halfWidth),
                UnityEngine.Random.Range(-(float)world.halfHeight, (float)world.halfHeight)
            );

            if (Physics2D.OverlapPoint(randomPos, GroundMask) && !definition.SpawnInGround) return;

            Vector2 direction = DirectionForPlacement(definition);
            RaycastHit2D hit = Physics2D.Raycast(randomPos, direction, WorldGeneration.CHUNKSIZE, GroundMask);
            if (!hit) return;
            if (!(Mathf.Abs(hit.point.x) < world.halfWidth - 1f) || !(Mathf.Abs(hit.point.y) < world.halfHeight - 1f)) return;
            if (definition.PlaceCheck != null && !definition.PlaceCheck(world.WorldToBlockPos(hit.point - Vector2.up * 0.5f))) return;

            Vector2 spawnPos = hit.point - direction * definition.SurfaceOffset;
            GameObject instance = Spawn(id, spawnPos, Quaternion.identity);
            if (instance == null) return;

            ApplyPlacement(instance, definition, direction);
            SetBlockSeating(instance, world, hit.point + direction * 0.5f);
        }

        private static void DistributeDropPod(WorldGeneration world, string id, CustomBuildingEntityDefinition definition)
        {
            Vector2 randomPos = new Vector2(
                UnityEngine.Random.Range(-(float)world.halfWidth + 50f, world.halfWidth - 50f),
                UnityEngine.Random.Range(-(float)world.halfHeight + 50f, world.halfHeight - 50f)
            );

            RaycastHit2D hit = Physics2D.Raycast(randomPos, Vector2.down, 400f, GroundMask);
            Vector2 finalPos = hit ? hit.point : randomPos;
            if (Mathf.Abs(finalPos.x) > world.halfWidth - 40f || Mathf.Abs(finalPos.y) > world.halfHeight - 40f) return;

            GameObject instance = Spawn(id, finalPos, Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)));
            if (instance == null) return;

            world.GenerateBlockCircle(finalPos, 32, 3, 0.7f, 0f, autoUpdateChunk: true);
            world.GenerateBlockCircle(finalPos, 30, 6, 0.04f, 0.04f, autoUpdateChunk: true);
            world.GenerateBlockCircle(finalPos, 4, 0, 1f, 0.9f, autoUpdateChunk: true);
            SetBlockSeating(instance, world, finalPos + Vector2.down * 0.5f);
        }

        private static Vector2 DirectionForPlacement(CustomBuildingEntityDefinition definition)
        {
            if (definition.Placement == BuildingPlacementType.Ceiling) return Vector2.up;
            if (definition.Placement == BuildingPlacementType.Wall)
            {
                return UnityEngine.Random.value > 0.5f ? Vector2.right : Vector2.left;
            }

            return Vector2.down;
        }

        private static void ApplyPlacement(GameObject instance, CustomBuildingEntityDefinition definition, Vector2 direction)
        {
            if (instance == null) return;

            if (definition.Placement == BuildingPlacementType.Wall)
            {
                float x = direction.x >= 0f ? -1f : 1f;
                instance.transform.localScale = new Vector3(Mathf.Abs(instance.transform.localScale.x) * x, instance.transform.localScale.y, instance.transform.localScale.z);
            }
            else if (definition.RandomFlip && UnityEngine.Random.value > 0.5f)
            {
                instance.transform.localScale = new Vector3(-instance.transform.localScale.x, instance.transform.localScale.y, instance.transform.localScale.z);
            }
        }

        private static void SetBlockSeating(GameObject instance, WorldGeneration world, Vector2 blockReference)
        {
            if (instance == null || world == null) return;
            if (!instance.TryGetComponent(out BuildingEntity building)) return;

            building.blockPlacedOn = world.WorldToBlockPos(blockReference);
            AttachSeatingListener(building, world);
        }

        private static void AttachSeatingListener(BuildingEntity building, WorldGeneration world)
        {
            if (building == null || world == null || !building.requireGround || world.ChunkUpdated == null) return;

            int chunkX = building.blockPlacedOn.x / WorldGeneration.CHUNKSIZE;
            int chunkY = building.blockPlacedOn.y / WorldGeneration.CHUNKSIZE;
            if (chunkX < 0 || chunkY < 0 || chunkX >= world.ChunkUpdated.GetLength(0) || chunkY >= world.ChunkUpdated.GetLength(1)) return;

            UnityEvent chunkUpdated = world.ChunkUpdated[chunkX, chunkY];
            if (chunkUpdated == null) return;

            chunkUpdated.AddListener(building.CheckSeating);
        }

        private static void SpawnDropArray(BuildingEntity source, ItemDrop[] drops, float multiplier, bool isNearPlayer, bool rollChance)
        {
            if (drops == null) return;

            foreach (ItemDrop drop in drops)
            {
                if (drop == null || string.IsNullOrWhiteSpace(drop.id)) continue;
                if (rollChance && UnityEngine.Random.Range(0f, 1f) >= drop.chance * multiplier) continue;

                SpawnAndSetupDrop(source, drop.id, drop.conditionMin, drop.conditionMax, isNearPlayer);
            }
        }

        private static void SpawnCategoryDrops(BuildingEntity source, CustomBuildingEntityDefinition definition, bool isNearPlayer)
        {
            if (definition.GuaranteedDropAmount <= 0 || definition.ItemCategoriesToAdd == null || definition.ItemCategoriesToAdd.Length == 0) return;
            if (ItemLootPool.pool == null) return;

            for (int i = 0; i < definition.GuaranteedDropAmount; i++)
            {
                string category = definition.ItemCategoriesToAdd[UnityEngine.Random.Range(0, definition.ItemCategoriesToAdd.Length)];
                if (string.IsNullOrWhiteSpace(category)) continue;
                if (!ItemLootPool.pool.TryGetValue(category, out List<string> poolItems) || poolItems == null || poolItems.Count == 0) continue;

                string dropId = poolItems[UnityEngine.Random.Range(0, poolItems.Count)];
                SpawnAndSetupDrop(source, dropId, 1f, 1f, isNearPlayer);
            }
        }

        private static void SpawnAndSetupDrop(BuildingEntity source, string itemId, float conditionMin, float conditionMax, bool isNearPlayer)
        {
            GameObject obj = CustomInstantiate.InstantiateReturn(itemId, source.transform.position, Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)));
            if (obj == null)
            {
                CUCoreLibPlugin.Log?.LogWarning("Custom building '" + source.id + "' failed to spawn drop '" + itemId + "'.");
                return;
            }

            if (obj.TryGetComponent(out Rigidbody2D rb))
            {
                rb.velocity = new Vector2(UnityEngine.Random.Range(-7f, 7f), UnityEngine.Random.Range(-7f, 7f));
            }

            if (obj.TryGetComponent(out Item item))
            {
                item.SetCondition(UnityEngine.Random.Range(conditionMin, conditionMax));
            }

            if (isNearPlayer && obj.GetComponent<Rigidbody2D>() != null && obj.GetComponent<SpriteRenderer>() != null)
            {
                obj.AddComponent<FreshItemDrop>();
            }
        }

    }
}
