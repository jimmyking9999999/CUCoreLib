using System;
using System.Collections.Generic;
using CUCoreLib.Registries;
using UnityEngine;

namespace CUCoreLib.Data
{
    /// <summary>
    /// Describes which surface type a custom building entity attaches to.
    /// </summary>
    public enum BuildingPlacementType
    {
        /// <summary>
        /// Places the entity on the floor.
        /// </summary>
        Floor,

        /// <summary>
        /// Places the entity on the ceiling.
        /// </summary>
        Ceiling,

        /// <summary>
        /// Places the entity on walls.
        /// </summary>
        Wall
    }

    /// <summary>
    /// Automatic world distribution of custom building entities. Default None.
    /// </summary>
    public enum BuildingGenerationStyle
    {
        /// <summary>
        /// Disables automatic world generation.
        /// </summary>
        None,

        /// <summary>
        /// Uses the standard building-entity distribution path. (Raycast, like in-game)
        /// </summary>
        Standard,

        /// <summary>
        /// Uses drop-pod-style placement. (Removes ground, random location)
        /// </summary>
        DropPod
    }

    /// <summary>
    /// Enum for common Unity layers used by custom building entities. Default 'Ground' (6).
    /// </summary>
    public enum BuildingLayer
    {
        /// <summary>
        /// Unity Default layer.
        /// </summary>
        Default = 0,
        /// <summary>
        /// Unity TransparentFX layer.
        /// </summary>
        TransparentFix = 1,
        /// <summary>
        /// Unity Ignore Raycast layer.
        /// </summary>
        IgnoreRaycast = 2,
        /// <summary>
        /// Unity layer 3.
        /// </summary>
        Layer3 = 3,
        /// <summary>
        /// Unity Water layer.
        /// </summary>
        Water = 4,
        /// <summary>
        /// Unity UI layer.
        /// </summary>
        UI = 5,
        /// <summary>
        /// Used for 90% of cases
        /// </summary>
        Ground = 6
    }

    /// <summary>
    /// Defines a custom building entity registered through <c>BuildingEntityRegistry.Register</c>.
    /// </summary>
    public sealed class CustomBuildingEntityDefinition
    {
        /// <summary>
        /// Adds a falling <c>Rigidbody2D</c> and vanilla <c>DamagingCrate</c> behavior to spawned instances.
        /// Defaults the instance to the vanilla crate Ground layer unless <see cref="Layer"/> or <see cref="LayerEnum"/> is set.
        /// </summary>
        public bool AddRigidbody2D;

        /// <summary>
        /// Makes an added <c>Rigidbody2D</c> damage player limbs on sufficiently fast impacts, using vanilla stalactite behavior.
        /// </summary>
        public bool DamagePlayerOnImpact;

        /// <summary>
        /// Drops always spawned when the entity is destroyed, regardless of chance-based drop logic. Added to match game internal behaviour, but not really needed. Just set a drop chance to 1f.
        /// </summary>
        public ItemDrop[] AlwaysDrop;

        /// <summary>
        /// Marks the entity as an animal for vanilla behavior checks. (Only for enemies, and has the attack cooldown check)
        /// </summary>
        public bool Animal;

        /// <summary>
        /// Vanilla block footstep sound id used when walking on the entity.
        /// </summary>
        public ushort BlockFootstepSoundId;

        /// <summary>
        /// Prevents the entity from being hit by vanilla damage interactions. Meant for Decal.
        /// </summary>
        public bool CantHit;

        /// <summary>
        /// Makes the generated collider a trigger instead of a solid collider.
        /// </summary>
        public bool ColliderIsTrigger;

        /// <summary>
        /// Optional offset for the generated collider.
        /// </summary>
        public Vector2? ColliderOffset;

        /// <summary>
        /// Optional size for the generated collider.
        /// </summary>
        public Vector2? ColliderSize;

        /// <summary>
        /// Additional component types attached to the spawned prefab. Yay custom behavior! (Use <see cref="SpawnComponents"/> for string-based component registration)
        /// </summary>
        public Type[] Components;

        /// <summary>
        /// Callback that can further edit the spawned instance after it is created.
        /// </summary>
        public Action<GameObject> ConfigureInstance;

        /// <summary>
        /// Callback that can modify the generated prefab before it is cached for future spawns.
        /// </summary>
        public Action<GameObject> ConfigurePrefab;

        /// <summary>
        /// Really should be renamed 'SetPlantLayer'. Places above blocks, but below the player and enemies.
        /// </summary>
        public bool CopyGlowPlantLayer;

        /// <summary>
        /// Description text for the building entity. Will change if a locale key is present in another language file.
        /// </summary>
        public string Description;

        /// <summary>
        /// Multiplier applied to chance-based drop rolls. Not really needed, but okay for custom extra drop chance tools?
        /// </summary>
        public float DropChanceMultiplier = 1f;

        /// <summary>
        /// Automatic world-generation preset for this entity.
        /// </summary>
        public BuildingGenerationStyle GenerationStyle = BuildingGenerationStyle.Standard;

        /// <summary>
        /// Number of guaranteed drop rolls attempted from the chance-based drop table.
        /// </summary>
        public int GuaranteedDropAmount;

        /// <summary>
        /// Health of the spawned entity.
        /// </summary>
        public float Health = 250f;

        /// <summary>
        /// Heat emitted per second by the entity.
        /// </summary>
        public float HeatPerSecond;

        /// <summary>
        /// Radius of the heat effect emitted by the entity.
        /// </summary>
        public float HeatRadius;

        /// <summary>
        /// Direct audio clip played when the entity is hit. Overrides HitSoundReferenceId if set. 
        /// </summary>
        public AudioClip HitSound;

        /// <summary>
        /// Reference building id used to copy a vanilla hit sound when <see cref="HitSound"/> is not set. Default 'rustle' (glowplant)
        /// </summary>
        public string HitSoundReferenceId;

        /// <summary>
        /// Stable id. Not needed typically, as BuildingEntityRegistry.Register already asks for and generates the ID.
        /// </summary>
        public string ID;

        /// <summary>
        /// Prevents body optimization from stopping the entity. (E.g. going too far away)
        /// </summary>
        public bool IgnoreBodyOptimize;

        /// <summary>
        /// Additional item categories that should include this building entity.
        /// </summary>
        public string[] ItemCategoriesToAdd;

        /// <summary>
        /// Chance-based drops spawned when the entity is destroyed. 
        /// Use with BuildingEntityRegistry.AddDrop("id", float maxcondition, float mincondition, float chance) 
        /// </summary>
        public ItemDrop[] ItemsDropOnDestroy;

        /// <summary>
        /// Explicit Unity layer index for the generated object..?
        /// </summary>
        public int? Layer;

        /// <summary>
        /// Enum wrapper around common Unity layer indices.
        /// </summary>
        public BuildingLayer? LayerEnum;

        /// <summary>
        /// Maximum body temperature reached from this entity's heat output.
        /// </summary>
        public float MaxHeatBodyTemperature;

        /// <summary>
        /// Enables vanilla metallic damage behavior. Mainly for plasma cutters. (Does not adjust sound effects)
        /// </summary>
        public bool Metallic;

        /// <summary>
        /// Display name registered for the entity.
        /// </summary>
        public string Name;

        /// <summary>
        /// Very optional custom placement validation callback.
        /// </summary>
        public WorldGeneration.PlaceCheckDelegate PlaceCheck;

        /// <summary>
        ///  Type the entity attaches to when placed. Default floor.
        /// </summary>
        public BuildingPlacementType Placement = BuildingPlacementType.Floor;

        /// <summary>
        /// Allows random horizontal sprite flipping on spawn. (Scale X *= -1). Default true.
        /// </summary>
        public bool RandomFlip = true;

        /// <summary>
        /// Reference building id used to copy a vanilla renderer/prefab baseline. Not recommended to edit.
        /// </summary>
        public string RenderReferenceId = "stoneplant";

        /// <summary>
        /// Requires valid ground support beneath the placement point. (Destroys when ground below is broken). Default true.
        /// </summary>
        public bool RequireGround = true;

        /// <summary>
        /// Body type assigned to an added <c>Rigidbody2D</c>. Defaults to Dynamic.
        /// </summary>
        public RigidbodyType2D RigidbodyBodyType = RigidbodyType2D.Dynamic;

        /// <summary>
        /// Gravity scale assigned to an added <c>Rigidbody2D</c>. Defaults to 1.
        /// </summary>
        public float RigidbodyGravityScale = 1f;

        /// <summary>
        /// Local scale applied to the generated object.
        /// </summary>
        public Vector3 Scale = Vector3.one;

        /// <summary>
        /// Sorting order used by the generated renderer. Higher = closer to camera
        /// </summary>
        public int SortingOrder = 5;

        /// <summary>
        /// Component type names resolved and attached at spawn time.
        /// </summary>
        public List<string> SpawnComponents = new List<string>();

        /// <summary>
        /// Allows the entity to spawn embedded in ground tiles.
        /// </summary>
        public bool SpawnInGround;

        /// <summary>
        /// Bitmask of which world layers the entity can spawn in.
        /// </summary>
        public int SpawnLayers = BuildingEntityRegistry.AllSpawnLayersMask;

        /// <summary>
        /// Maximum automatic spawns attempted per chunk.
        /// </summary>
        public float SpawnMaxPerChunk;

        /// <summary>
        /// Minimum automatic spawns attempted per chunk.
        /// </summary>
        public float SpawnMinPerChunk;

        /// <summary>
        /// Sprite used by the generated renderer.
        /// </summary>
        public Sprite Sprite;

        /// <summary>
        /// Optional registered sprite animation id applied to the renderer.
        /// </summary>
        public string SpriteAnimationId;

        /// <summary>
        /// Offset from the placement surface to the rendered object. For ground, goes up. For ceiling, goes down. For walls, goes out from the wall.
        /// </summary>
        public float SurfaceOffset = 0.5f;

        /// <summary>
        /// Uses the glow-plant material on the generated renderer. Not recommended to change.
        /// </summary>
        public bool UseGlowPlantMaterial;
    }
}
