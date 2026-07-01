using System;
using System.Collections.Generic;
using UnityEngine;

namespace CUCoreLib.Data;

public enum BuildingPlacementType
{
    Floor,
    Ceiling,
    Wall
}

public enum BuildingGenerationStyle
{
    None,
    Standard,
    DropPod
}

public sealed class CustomBuildingEntityDefinition
{
    public bool AddRigidbody2D;
    public ItemDrop[] AlwaysDrop;
    public bool Animal;
    public ushort BlockFootstepSoundId;
    public bool CantHit;
    public bool ColliderIsTrigger;
    public Vector2? ColliderOffset;
    public Vector2? ColliderSize;
    public Type[] Components;
    public Action<GameObject> ConfigureInstance;

    public Action<GameObject> ConfigurePrefab;
    public bool CopyGlowPlantLayer;
    public string Description;
    public float DropChanceMultiplier = 1f;
    public BuildingGenerationStyle GenerationStyle = BuildingGenerationStyle.None;
    public int GuaranteedDropAmount;

    public float Health = 250f;
    public float HeatPerSecond;
    public float HeatRadius;
    public AudioClip HitSound;

    public string HitSoundReferenceId;
    public string ID;
    public bool IgnoreBodyOptimize;
    public string[] ItemCategoriesToAdd;

    public ItemDrop[] ItemsDropOnDestroy;
    public int? Layer;
    public float MaxHeatBodyTemperature;
    public bool Metallic;
    public string Name;
    public WorldGeneration.PlaceCheckDelegate PlaceCheck;

    public BuildingPlacementType Placement = BuildingPlacementType.Floor;
    public bool RandomFlip = true;
    public string RenderReferenceId = "stoneplant";
    public bool RequireGround = true;
    public RigidbodyType2D RigidbodyBodyType = RigidbodyType2D.Static;
    public float RigidbodyGravityScale;
    public Vector3 Scale = Vector3.one;
    public int SortingOrder = 5;
    public List<string> SpawnComponents = [];
    public bool SpawnInGround;
    public float SpawnMaxPerChunk;
    public float SpawnMinPerChunk;

    public Sprite Sprite;
    public string SpriteAnimationId;
    public float SurfaceOffset = 0.5f;
    public bool UseGlowPlantMaterial;
}