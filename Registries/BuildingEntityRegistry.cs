using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CUCoreLib.ContentReload;
using CUCoreLib.Data;
using CUCoreLib.Helpers;
using CUCoreLib.Networking;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace CUCoreLib.Registries;

public static class BuildingEntityRegistry
{
    private const string DefaultHitSoundReferenceId = "glowplant";

    private static readonly Dictionary<string, CustomBuildingEntityDefinition> RegisteredDefinitions =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, GameObject> PrefabCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> DefinitionOwners = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<CustomBuildingRuntime> ActiveRuntimes = [];

    private static readonly ItemDrop[] EmptyDrops = [];
    private static readonly string[] EmptyCategories = [];
    private static readonly int GroundMask = LayerMask.GetMask("Ground");
    private static readonly int GroundLayer = LayerMask.NameToLayer("Ground");
    private static string ActiveOwnerId;

    public static IReadOnlyDictionary<string, CustomBuildingEntityDefinition> RegisteredDefinitionsView
        => new ReadOnlyDictionary<string, CustomBuildingEntityDefinition>(RegisteredDefinitions);

    public static event Action<string, CustomBuildingEntityDefinition, bool> Registered;

    public static void Register(string id, CustomBuildingEntityDefinition definition)
    {
        ContentReloadSession.AssertAllowed(
            ContentReloadSurface.Buildings,
            "BuildingEntityRegistry.Register()",
            "Only basic/scriptless building definitions are supported during strict content reload.");

        if (string.IsNullOrWhiteSpace(id))
        {
            CUCoreLibPlugin.Log?.LogWarning("Ignored custom building registration with no ID.");
            return;
        }

        id = id.Trim();
        definition ??= new CustomBuildingEntityDefinition();

        definition.ID = id;
        var replacingExisting = RegisteredDefinitions.ContainsKey(id);
        RegisteredDefinitions[id] = definition;
        var ownerId = !string.IsNullOrWhiteSpace(ActiveOwnerId)
            ? ActiveOwnerId
            : ContentReloadSession.ResolveAmbientOwnerId();
        if (!string.IsNullOrWhiteSpace(ownerId)) DefinitionOwners[id] = ownerId;
        PrefabCache.Remove(id);
        Registered?.Invoke(id, definition, replacingExisting);

        if (!string.IsNullOrEmpty(definition.Name)) LocaleRegistry.Register("building", id, definition.Name);

        if (!string.IsNullOrEmpty(definition.Description))
            LocaleRegistry.Register("building", id + "dsc", definition.Description);

        MultiplayerSyncRegistry.QueueHostSnapshotBroadcast();
    }

    public static IDisposable BeginOwnerRegistration(string ownerId)
    {
        return new OwnerScope(ownerId);
    }

    public static bool TryGetDefinition(string id, out CustomBuildingEntityDefinition definition)
    {
        definition = null;
        return !string.IsNullOrWhiteSpace(id) &&
               RegisteredDefinitions.TryGetValue(SpawnIdHelpers.NormalizeSpawnId(id), out definition);
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
        if (runtime != null) ActiveRuntimes.Add(runtime);
    }

    internal static void UnregisterRuntime(CustomBuildingRuntime runtime)
    {
        if (runtime != null) ActiveRuntimes.Remove(runtime);
    }

    internal static CustomBuildingRuntime[] GetActiveRuntimes()
    {
        return ActiveRuntimes.Where(runtime => runtime != null && runtime.isActiveAndEnabled).ToArray();
    }

    internal static Dictionary<string, CustomBuildingEntityDefinition> CaptureOwnerEntries(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return new Dictionary<string, CustomBuildingEntityDefinition>(StringComparer.OrdinalIgnoreCase);

        var normalizedOwnerId = ownerId.Trim();
        return DefinitionOwners
            .Where(entry => string.Equals(entry.Value, normalizedOwnerId, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Key)
            .Where(id => RegisteredDefinitions.TryGetValue(id, out _))
            .ToDictionary(id => id, id => RegisteredDefinitions[id], StringComparer.OrdinalIgnoreCase);
    }

    internal static void RestoreOwnerEntries(string ownerId,
        IDictionary<string, CustomBuildingEntityDefinition> entries)
    {
        if (entries == null || entries.Count == 0) return;

        foreach (var entry in entries) Register(entry.Key, entry.Value);
    }

    internal static void ClearOwnerEntries(string ownerId, ContentReloadResult result)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) return;

        var normalizedOwnerId = ownerId.Trim();
        var ids = DefinitionOwners
            .Where(entry => string.Equals(entry.Value, normalizedOwnerId, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Key)
            .ToArray();

        foreach (var id in ids)
        {
            RegisteredDefinitions.Remove(id);
            DefinitionOwners.Remove(id);
            PrefabCache.Remove(id);
        }

        if (ids.Length > 0)
            result?.AddInfo("Cleared " + ids.Length + " building registrations owned by '" + normalizedOwnerId +
                            "'.");
    }

    internal static void RefreshLiveInstances(IEnumerable<string> definitionIds = null)
    {
        var filteredIds = definitionIds != null
            ? new HashSet<string>(definitionIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()),
                StringComparer.OrdinalIgnoreCase)
            : null;

        foreach (var runtime in GetActiveRuntimes())
        {
            if (runtime == null || string.IsNullOrWhiteSpace(runtime.DefinitionId)) continue;

            if (filteredIds != null && !filteredIds.Contains(runtime.DefinitionId)) continue;

            RefreshLiveInstance(runtime.gameObject, runtime.DefinitionId);
        }
    }

    internal static JObject CaptureNetworkSnapshot()
    {
        var root = new JObject();
        foreach (var entry in RegisteredDefinitions)
        {
            var definition = entry.Value;
            if (definition == null) continue;

            var building = new JObject
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
                ["hitSound"] =
                    NetworkSnapshotSerialization.WriteStringOrEmpty(definition.HitSound != null
                        ? definition.HitSound.name
                        : null),
                ["blockFootstepSoundId"] = definition.BlockFootstepSoundId,
                ["renderReferenceId"] = definition.RenderReferenceId ?? string.Empty,
                ["copyGlowPlantLayer"] = definition.CopyGlowPlantLayer,
                ["heatRadius"] = definition.HeatRadius,
                ["heatPerSecond"] = definition.HeatPerSecond,
                ["maxHeatBodyTemperature"] = definition.MaxHeatBodyTemperature,
                ["spawnComponents"] = definition.SpawnComponents != null
                    ? JArray.FromObject(definition.SpawnComponents)
                    : new JArray(),
                ["components"] = NetworkSnapshotSerialization.WriteTypeNames(definition.Components)
            };

            if (definition.ItemsDropOnDestroy != null)
                building["itemsDropOnDestroy"] = JArray.FromObject(definition.ItemsDropOnDestroy);

            if (definition.AlwaysDrop != null) building["alwaysDrop"] = JArray.FromObject(definition.AlwaysDrop);

            if (definition.ItemCategoriesToAdd != null)
                building["itemCategoriesToAdd"] = JArray.FromObject(definition.ItemCategoriesToAdd);

            root[entry.Key] = building;
        }

        return root;
    }

    internal static void ApplyNetworkSnapshot(JObject snapshot)
    {
        if (snapshot == null) return;

        foreach (var property in snapshot.Properties())
        {
            var id = property.Name;
            var obj = property.Value as JObject;
            if (string.IsNullOrWhiteSpace(id) || obj == null) continue;

            var definition = new CustomBuildingEntityDefinition
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
                    ? new Vector2?(new Vector2(obj.Value<float?>("colliderSizeX") ?? 0f,
                        obj.Value<float?>("colliderSizeY") ?? 0f))
                    : null,
                ColliderOffset = obj["colliderOffsetX"] != null || obj["colliderOffsetY"] != null
                    ? new Vector2?(new Vector2(obj.Value<float?>("colliderOffsetX") ?? 0f,
                        obj.Value<float?>("colliderOffsetY") ?? 0f))
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
                SpawnComponents =
                    (obj["spawnComponents"] as JArray)?.ToObject<List<string>>() ?? new List<string>(),
                Components = NetworkSnapshotSerialization.ReadTypeNames(obj["components"])
            };

            if (obj["itemsDropOnDestroy"] is JArray drops)
                definition.ItemsDropOnDestroy = drops.ToObject<ItemDrop[]>();
            if (obj["alwaysDrop"] is JArray alwaysDrop) definition.AlwaysDrop = alwaysDrop.ToObject<ItemDrop[]>();
            if (obj["itemCategoriesToAdd"] is JArray categories)
                definition.ItemCategoriesToAdd = categories.ToObject<string[]>();

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

        id = SpawnIdHelpers.NormalizeSpawnId(id);
        var prefab = GetOrCreatePrefab(id);
        if (prefab == null)
        {
            CUCoreLibPlugin.Log?.LogWarning("Could not spawn custom building '" + id.Trim() +
                                            "' because it is not registered.");
            return null;
        }

        var instance = Object.Instantiate(prefab, position, rotation);
        instance.SetActive(true);
        return instance;
    }

    public static GameObject PlaceOnSurface(string id, Vector2 origin, Vector2 direction,
        WorldGeneration world = null)
    {
        if (direction == Vector2.zero) direction = Vector2.down;

        if (!TryGetDefinition(id, out var definition)) return null;
        world = world ?? WorldGeneration.world;

        var hit = Physics2D.Raycast(origin, direction.normalized, WorldGeneration.CHUNKSIZE, GroundMask);
        if (!hit) return null;

        var spawnPos = hit.point - direction.normalized * definition.SurfaceOffset;
        var instance = Spawn(id, spawnPos, Quaternion.identity);
        if (instance == null) return null;

        ApplyPlacement(instance, definition, direction);
        SetBlockSeating(instance, world, hit.point + direction.normalized * 0.5f);
        return instance;
    }

    public static void DistributeInWorld(string id, WorldGeneration world)
    {
        if (world == null || !TryGetDefinition(id, out var definition)) return;
        if (definition.GenerationStyle == BuildingGenerationStyle.None) return;

        var prefab = GetOrCreatePrefab(id);
        if (prefab == null) return;

        var count = Mathf.RoundToInt(world.chunkWidth * world.chunkHeight *
                                     Random.Range(definition.SpawnMinPerChunk, definition.SpawnMaxPerChunk));
        for (var i = 0; i < count; i++)
            if (definition.GenerationStyle == BuildingGenerationStyle.DropPod)
                DistributeDropPod(world, id, definition);
            else
                DistributeStandard(world, id, definition);
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

    public static ItemDrop AddDrop(ICollection<ItemDrop> drops, string id, float chance = 1f,
        float conditionMin = 1f, float conditionMax = 1f)
    {
        var drop = AddDrop(id, chance, conditionMin, conditionMax);
        drops?.Add(drop);
        return drop;
    }

    internal static GameObject GetOrCreatePrefab(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        id = id.Trim();

        if (PrefabCache.TryGetValue(id, out var cached) && cached != null) return cached;

        if (!RegisteredDefinitions.TryGetValue(id, out var definition))
        {
            CUCoreLibPlugin.Log?.LogWarning("No custom building definition was registered for '" + id + "'.");
            return null;
        }

        var prefab = CreatePrefab(id, definition);
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

        var isNearPlayer = PlayerCamera.main != null &&
                           PlayerCamera.main.body != null &&
                           Vector2.Distance(source.transform.position, PlayerCamera.main.body.transform.position) <
                           8f;

        SpawnDropArray(source, definition.ItemsDropOnDestroy, definition.DropChanceMultiplier, isNearPlayer, true);
        SpawnCategoryDrops(source, definition, isNearPlayer);
        SpawnDropArray(source, definition.AlwaysDrop, definition.DropChanceMultiplier, isNearPlayer, false);
    }

    internal static void RestoreSeating(GameObject instance, WorldGeneration world, Vector2Int blockPlacedOn)
    {
        if (instance == null || world == null) return;
        if (!instance.TryGetComponent(out BuildingEntity building)) return;

        building.blockPlacedOn = blockPlacedOn;
        AttachSeatingListener(building, world);
    }

    internal static void RefreshLiveInstance(GameObject instance, string id)
    {
        if (instance == null || string.IsNullOrWhiteSpace(id)) return;

        if (!TryGetDefinition(id, out var definition) || definition == null) return;

        instance.name = id.Trim();

        var renderer = instance.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sprite = definition.Sprite;
            renderer.sortingOrder = definition.SortingOrder;
            if (!string.IsNullOrWhiteSpace(definition.SpriteAnimationId))
                AssetLoader.TryApplyAnimation(renderer, definition.SpriteAnimationId);
        }

        instance.transform.localScale = definition.Scale == Vector3.zero ? Vector3.one : definition.Scale;
        instance.layer = definition.Layer ?? GetReferenceLayer(GetRenderReference(definition), GroundLayer);

        var collider = instance.GetComponent<BoxCollider2D>();
        if (collider != null)
        {
            if (definition.ColliderSize.HasValue)
                collider.size = definition.ColliderSize.Value;
            else if (definition.Sprite != null) collider.size = definition.Sprite.bounds.size;

            if (definition.ColliderOffset.HasValue)
                collider.offset = definition.ColliderOffset.Value;
            else if (definition.Sprite != null) collider.offset = definition.Sprite.bounds.center;

            collider.isTrigger = definition.ColliderIsTrigger;
        }

        var building = instance.GetComponent<BuildingEntity>();
        if (building == null) return;
        ApplyBuildingFields(building, definition);
        if (!string.IsNullOrEmpty(definition.Name))
            building.fullName = LocaleRegistry.Get("building", id, definition.Name);

        if (!string.IsNullOrEmpty(definition.Description))
            building.description = LocaleRegistry.Get("building", id + "dsc", definition.Description);
    }

    private static GameObject CreatePrefab(string id, CustomBuildingEntityDefinition definition)
    {
        var go = new GameObject(id);
        go.SetActive(false);
        Object.DontDestroyOnLoad(go);

        go.transform.localScale = definition.Scale == Vector3.zero ? Vector3.one : definition.Scale;
        var renderReference = GetRenderReference(definition);
        go.layer = definition.Layer ?? GetReferenceLayer(renderReference, GroundLayer);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = definition.Sprite;
        renderer.sortingOrder = definition.SortingOrder;
        if (!string.IsNullOrWhiteSpace(definition.SpriteAnimationId))
            AssetLoader.TryApplyAnimation(renderer, definition.SpriteAnimationId);
        var referenceRenderer = renderReference != null ? renderReference.GetComponent<SpriteRenderer>() : null;
        if (referenceRenderer != null) renderer.sharedMaterial = referenceRenderer.sharedMaterial;

        if (definition.UseGlowPlantMaterial)
        {
            var glowRef = Resources.Load<GameObject>("glowplant");
            var glowRenderer = glowRef != null ? glowRef.GetComponent<SpriteRenderer>() : null;
            if (glowRenderer != null) renderer.sharedMaterial = glowRenderer.sharedMaterial;
        }

        var collider = go.AddComponent<BoxCollider2D>();
        if (definition.ColliderSize.HasValue)
            collider.size = definition.ColliderSize.Value;
        else if (definition.Sprite != null) collider.size = definition.Sprite.bounds.size;

        if (definition.ColliderOffset.HasValue)
            collider.offset = definition.ColliderOffset.Value;
        else if (definition.Sprite != null) collider.offset = definition.Sprite.bounds.center;

        collider.isTrigger = definition.ColliderIsTrigger;

        if (definition.AddRigidbody2D)
        {
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = definition.RigidbodyBodyType;
            rb.gravityScale = definition.RigidbodyGravityScale;
        }

        var building = go.AddComponent<BuildingEntity>();
        ApplyBuildingFields(building, definition);

        var runtime = go.AddComponent<CustomBuildingRuntime>();
        runtime.DefinitionId = id;

        if (definition.Components != null)
            foreach (var componentType in definition.Components)
            {
                if (componentType == null || !typeof(Component).IsAssignableFrom(componentType)) continue;
                if (go.GetComponent(componentType) == null) go.AddComponent(componentType);
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
        building.hitSound = definition.HitSound
                            ?? ResolveHitSound(definition.HitSoundReferenceId)
                            ?? ResolveHitSound(DefaultHitSoundReferenceId);
        building.blockFootstepSoundId = definition.BlockFootstepSoundId;
        building.skipDescriptionSet = false;
    }

    private static AudioClip ResolveHitSound(string referenceId)
    {
        if (string.IsNullOrWhiteSpace(referenceId)) return null;

        var normalized = referenceId.Trim();
        normalized = normalized.ToLower() switch
        {
            "metal" => "turret",
            "rubber" => "glowplant",
            "plant" => "glowplant",
            "rustle" => "geotree",
            "crystal" => "BloodCrystal",
            "flesh" => "shadecrawler",
            "pop" => "pop",
            "ice" or "glass" => "icestalagmite",
            "stone" or "rock" => "stoneplant",
            "chain" => "barbedwirefence",
            _ => normalized
        };
        // TODO add more, add to documentation, and allow mods to specify 
        // - custom sound references
        // - exact buildingentity names as references
        // - tiles, for their sounds too

        // This should only be shorthand (!)

        var reference = Resources.Load<GameObject>(normalized);
        if (reference != null && reference.TryGetComponent(out BuildingEntity building)) return building.hitSound;

        CUCoreLibPlugin.Log?.LogWarning("Could not resolve building hit sound reference '" + referenceId.Trim() +
                                        "'.");
        return null;
    }

    private static GameObject GetRenderReference(CustomBuildingEntityDefinition definition)
    {
        var referenceId = string.IsNullOrWhiteSpace(definition.RenderReferenceId)
            ? "stoneplant"
            : definition.RenderReferenceId.Trim();

        var reference = Resources.Load<GameObject>(referenceId);
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
        var reference = Resources.Load<GameObject>(resourceId);
        return reference != null ? reference.layer : fallback;
    }

    private static void DistributeStandard(WorldGeneration world, string id,
        CustomBuildingEntityDefinition definition)
    {
        var randomPos = new Vector2(
            Random.Range(-(float)world.halfWidth, world.halfWidth),
            Random.Range(-(float)world.halfHeight, world.halfHeight)
        );

        if (Physics2D.OverlapPoint(randomPos, GroundMask) && !definition.SpawnInGround) return;

        var direction = DirectionForPlacement(definition);
        var hit = Physics2D.Raycast(randomPos, direction, WorldGeneration.CHUNKSIZE, GroundMask);
        if (!hit) return;
        if (!(Mathf.Abs(hit.point.x) < world.halfWidth - 1f) ||
            !(Mathf.Abs(hit.point.y) < world.halfHeight - 1f)) return;
        if (definition.PlaceCheck != null &&
            !definition.PlaceCheck(world.WorldToBlockPos(hit.point - Vector2.up * 0.5f))) return;

        var spawnPos = hit.point - direction * definition.SurfaceOffset;
        var instance = Spawn(id, spawnPos, Quaternion.identity);
        if (instance == null) return;

        ApplyPlacement(instance, definition, direction);
        SetBlockSeating(instance, world, hit.point + direction * 0.5f);
    }

    private static void DistributeDropPod(WorldGeneration world, string id,
        CustomBuildingEntityDefinition definition)
    {
        var randomPos = new Vector2(
            Random.Range(-(float)world.halfWidth + 50f, world.halfWidth - 50f),
            Random.Range(-(float)world.halfHeight + 50f, world.halfHeight - 50f)
        );

        var hit = Physics2D.Raycast(randomPos, Vector2.down, 400f, GroundMask);
        var finalPos = hit ? hit.point : randomPos;
        if (Mathf.Abs(finalPos.x) > world.halfWidth - 40f || Mathf.Abs(finalPos.y) > world.halfHeight - 40f) return;

        var instance = Spawn(id, finalPos, Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
        if (instance == null) return;

        world.GenerateBlockCircle(finalPos, 32, 3, 0.7f, 0f, true);
        world.GenerateBlockCircle(finalPos, 30, 6, 0.04f, 0.04f, true);
        world.GenerateBlockCircle(finalPos, 4, 0, 1f, 0.9f, true);
        SetBlockSeating(instance, world, finalPos + Vector2.down * 0.5f);
    }

    private static Vector2 DirectionForPlacement(CustomBuildingEntityDefinition definition)
    {
        switch (definition.Placement)
        {
            case BuildingPlacementType.Ceiling:
                return Vector2.up;
            case BuildingPlacementType.Wall:
                return Random.value > 0.5f ? Vector2.right : Vector2.left;
            case BuildingPlacementType.Floor:
            default:
                return Vector2.down;
        }
    }

    private static void ApplyPlacement(GameObject instance, CustomBuildingEntityDefinition definition,
        Vector2 direction)
    {
        if (instance == null) return;

        if (definition.Placement == BuildingPlacementType.Wall)
        {
            var x = direction.x >= 0f ? -1f : 1f;
            instance.transform.localScale = new Vector3(Mathf.Abs(instance.transform.localScale.x) * x,
                instance.transform.localScale.y, instance.transform.localScale.z);
        }
        else if (definition.RandomFlip && Random.value > 0.5f)
        {
            instance.transform.localScale = new Vector3(-instance.transform.localScale.x,
                instance.transform.localScale.y, instance.transform.localScale.z);
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

        var chunkX = building.blockPlacedOn.x / WorldGeneration.CHUNKSIZE;
        var chunkY = building.blockPlacedOn.y / WorldGeneration.CHUNKSIZE;
        if (chunkX < 0 || chunkY < 0 || chunkX >= world.ChunkUpdated.GetLength(0) ||
            chunkY >= world.ChunkUpdated.GetLength(1)) return;

        var chunkUpdated = world.ChunkUpdated[chunkX, chunkY];

        chunkUpdated?.AddListener(building.CheckSeating);
    }

    private static void SpawnDropArray(BuildingEntity source, ItemDrop[] drops, float multiplier, bool isNearPlayer,
        bool rollChance)
    {
        if (drops == null) return;

        foreach (var drop in drops)
        {
            if (drop == null || string.IsNullOrWhiteSpace(drop.id)) continue;
            if (rollChance && Random.Range(0f, 1f) >= drop.chance * multiplier) continue;

            SpawnAndSetupDrop(source, drop.id, drop.conditionMin, drop.conditionMax, isNearPlayer);
        }
    }

    private static void SpawnCategoryDrops(BuildingEntity source, CustomBuildingEntityDefinition definition,
        bool isNearPlayer)
    {
        if (definition.GuaranteedDropAmount <= 0 || definition.ItemCategoriesToAdd == null ||
            definition.ItemCategoriesToAdd.Length == 0) return;
        if (ItemLootPool.pool == null) return;

        for (var i = 0; i < definition.GuaranteedDropAmount; i++)
        {
            var category = definition.ItemCategoriesToAdd[Random.Range(0, definition.ItemCategoriesToAdd.Length)];
            if (string.IsNullOrWhiteSpace(category)) continue;
            if (!ItemLootPool.pool.TryGetValue(category, out var poolItems) || poolItems == null ||
                poolItems.Count == 0) continue;

            var dropId = poolItems[Random.Range(0, poolItems.Count)];
            SpawnAndSetupDrop(source, dropId, 1f, 1f, isNearPlayer);
        }
    }

    private static void SpawnAndSetupDrop(BuildingEntity source, string itemId, float conditionMin,
        float conditionMax, bool isNearPlayer)
    {
        var obj = CustomInstantiate.InstantiateReturn(itemId, source.transform.position,
            Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
        if (obj == null)
        {
            CUCoreLibPlugin.Log?.LogWarning("Custom building '" + source.id + "' failed to spawn drop '" + itemId +
                                            "'.");
            return;
        }

        if (obj.TryGetComponent(out Rigidbody2D rb))
            rb.velocity = new Vector2(Random.Range(-7f, 7f), Random.Range(-7f, 7f));

        if (obj.TryGetComponent(out Item item)) item.SetCondition(Random.Range(conditionMin, conditionMax));

        if (isNearPlayer && obj.GetComponent<Rigidbody2D>() != null && obj.GetComponent<SpriteRenderer>() != null)
            obj.AddComponent<FreshItemDrop>();
    }

    private sealed class OwnerScope : IDisposable
    {
        private readonly string previousOwnerId;

        public OwnerScope(string ownerId)
        {
            previousOwnerId = ActiveOwnerId;
            ActiveOwnerId = string.IsNullOrWhiteSpace(ownerId) ? null : ownerId.Trim();
        }

        public void Dispose()
        {
            ActiveOwnerId = previousOwnerId;
        }
    }
}