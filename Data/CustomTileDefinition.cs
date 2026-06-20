using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CUCoreLib.Data
{
    public sealed class CustomTileDefinition
    {
        public string ID;
        public string Name;
        public string Description;

        public Sprite Sprite;
        public string TileName;
        public Color Color = Color.white;
        public Tile.ColliderType ColliderType = Tile.ColliderType.Grid;

        public float Health = 100f;
        public string HitSound = "rock";
        public AudioClip HitSoundClip;
        public string StepSound = "Rock";
        public Body.SleepQuality SleepQuality = Body.SleepQuality.Bad;
        public bool NoVariation;
        public bool Metallic;
        public float Toxicity;
        public bool Slippery;
        public float SpawnAmount;
        public int SpawnLayers = -1;
        public TileGenerationStyle GenerationStyle = TileGenerationStyle.Vein;
        public ItemDrop[] Drops;
        public Dictionary<string, object> CustomData = new Dictionary<string, object>();
    }
}
