using System;
using System.Collections.Generic;
using UnityEngine;

namespace CUCoreLib.Data
{
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
        public string ID;
        public string Name;
        public string Description;

        public Sprite Sprite;
        public string SpriteAnimationId;
        public int SortingOrder = 5;
        public bool UseGlowPlantMaterial;
        public Vector3 Scale = Vector3.one;
        public Vector2? ColliderSize;
        public Vector2? ColliderOffset;
        public bool ColliderIsTrigger;
        public int? Layer;
        public bool AddRigidbody2D;
        public RigidbodyType2D RigidbodyBodyType = RigidbodyType2D.Static;
        public float RigidbodyGravityScale;

        public float Health = 250f;
        public bool RequireGround = true;
        public bool Metallic;
        public bool CantHit;
        public bool Animal;
        public bool IgnoreBodyOptimize;
        public float DropChanceMultiplier = 1f;

        public ItemDrop[] ItemsDropOnDestroy;
        public ItemDrop[] AlwaysDrop;
        public string[] ItemCategoriesToAdd;
        public int GuaranteedDropAmount;

        public BuildingPlacementType Placement = BuildingPlacementType.Floor;
        public BuildingGenerationStyle GenerationStyle = BuildingGenerationStyle.None;
        public float SpawnMinPerChunk;
        public float SpawnMaxPerChunk;
        public float SurfaceOffset = 0.5f;
        public bool RandomFlip = true;
        public bool SpawnInGround;
        public WorldGeneration.PlaceCheckDelegate PlaceCheck;

        public Action<GameObject> ConfigurePrefab;
        public Action<GameObject> ConfigureInstance;
        public Type[] Components;
        public List<string> SpawnComponents = new List<string>();

        public string HitSoundReferenceId;
        public AudioClip HitSound;
        public ushort BlockFootstepSoundId;
        public string RenderReferenceId = "stoneplant";
        public bool CopyGlowPlantLayer;
        public float HeatRadius;
        public float HeatPerSecond;
        public float MaxHeatBodyTemperature;
    }
}
