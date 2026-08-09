using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CUCoreLib.Data
{
    /// <summary>
    /// Defines the data used by <c>TileRegistry.Register</c> to register a custom terrain tile.
    /// </summary>
    public sealed class CustomTileDefinition
    {
        /// <summary>
        /// Don't change this.
        /// </summary>
        public Tile.ColliderType ColliderType = Tile.ColliderType.Grid;

        /// <summary>
        /// Tint applied to <see cref="Sprite"/> when the tile is rendered. Not recommended to change.
        /// </summary>
        public Color Color = Color.white;

        /// <summary>
        /// Registration-time metadata for mod-defined tile behavior. Yay, custom behaviour!
        /// </summary>
        public Dictionary<string, object> CustomData = new Dictionary<string, object>();

        /// <summary>
        /// !! WILL NOT SHOW IN-GAME !!. Only added as it is a field in vanilla.
        /// </summary>
        public string Description;

        /// <summary>
        /// Optional item drops spawned when the tile breaks.
        /// </summary>
        public ItemDrop[] Drops;

        /// <summary>
        /// Preset world-generation shapes used when <see cref="SpawnAmount"/> is greater than zero.
        /// Use with TileGenerationStyle.(Vein, HeavyVeins, Singular, Stripe, Inner, and/or Outskirt)
        /// </summary>
        public TileGenerationStyle GenerationStyle = TileGenerationStyle.Vein;

        /// <summary>
        /// Damage required to break the block.
        /// </summary>
        public float Health = 100f;

        /// <summary>
        /// Vanilla hit sound tile reference used when the block is damaged.
        /// </summary>
        public string HitSound = "rock";

        /// <summary>
        /// Direct hit-sound override. When set, this takes priority over <see cref="HitSound"/>.
        /// </summary>
        public AudioClip HitSoundClip;

        /// <summary>
        /// Effective stable tile ID used for locale keys and lookup. Set automatically when using
        /// <c>TileRegistry.Register(string, ...)</c>; that overload returns this value after collision handling.
        /// </summary>
        public string ID;

        /// <summary>
        /// Enables the vanilla metallic damage behavior for the tile. Only for plasma cutter interactions.
        /// </summary>
        public bool Metallic;

        /// <summary>
        /// Display name on hover.
        /// </summary>
        public string Name;

        /// <summary>
        /// Disables the game's visual tile variation for this tile. (Overlap uniqueness on all four sides)
        /// </summary>
        public bool NoVariation;

        /// <summary>
        /// Rest quality while sleeping on the tile.
        /// </summary>
        public Body.SleepQuality SleepQuality = Body.SleepQuality.Bad;

        /// <summary>
        /// Enables the vanilla ice behavior for the tile.
        /// </summary>
        public bool Slippery;

        /// <summary>
        /// Copper-relative world-generation multiplier. Set to zero to disable automatic spawning. 2f = twice as much as copper, 0.5f = half as much as copper.
        /// </summary>
        public float SpawnAmount;

        /// <summary>
        /// Bitmask of allowed world layers for automatic spawning. Defaults to all layers.
        /// </summary>
        public int SpawnLayers = -1;

        /// <summary>
        /// Required sprite used for the registered tile.
        /// </summary>
        public Sprite Sprite;

        /// <summary>
        /// Vanilla sound id used for footsteps on the tile.
        /// </summary>
        public string StepSound = "Rock";

        /// <summary>
        /// Optional explicit Unity object name for the generated tile asset. Defaults to <see cref="ID"/>.
        /// Not necessary nor recommended to edit.
        /// </summary>
        public string TileName;

        /// <summary>
        /// Vanilla toxirock (radiation) behaviour value applied to the block.
        /// </summary>
        public float Toxicity;
    }
}
