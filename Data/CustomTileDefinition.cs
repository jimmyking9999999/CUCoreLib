using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CUCoreLib.Data;

public sealed class CustomTileDefinition
{
    public Tile.ColliderType ColliderType = Tile.ColliderType.Grid;
    public Color Color = Color.white;
    public Dictionary<string, object> CustomData = new();
    public string Description;
    public ItemDrop[] Drops;
    public TileGenerationStyle GenerationStyle = TileGenerationStyle.Vein;

    public float Health = 100f;
    public string HitSound = "rock";
    public AudioClip HitSoundClip;
    public string ID;
    public bool Metallic;
    public string Name;
    public bool NoVariation;
    public Body.SleepQuality SleepQuality = Body.SleepQuality.Bad;
    public bool Slippery;
    public float SpawnAmount;
    public int SpawnLayers = -1;

    public Sprite Sprite;
    public string StepSound = "Rock";
    public string TileName;
    public float Toxicity;
}