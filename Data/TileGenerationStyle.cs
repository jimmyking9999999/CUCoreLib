using System;

namespace CUCoreLib.Data
{
    /// <summary>
    ///     Preset flags that control how a custom tile is distributed during automatic ore-style world generation.
    /// </summary>
    [Flags]
    public enum TileGenerationStyle : byte
    {
        /// <summary>
        ///     Disables automatic generation styles.
        /// </summary>
        None = 0,

        /// <summary>
        ///     Uses the standard copper-like vein walker.
        /// </summary>
        Vein = 1 << 0,

        /// <summary>
        ///     Uses a denser, chunkier vein preset.
        /// </summary>
        HeavyVeins = 1 << 1,

        /// <summary>
        ///     Spawns isolated single-tile deposits.
        /// </summary>
        Singular = 1 << 2,

        /// <summary>
        ///     Spawns long stripe-like deposits.
        /// </summary>
        Stripe = 1 << 3,

        /// <summary>
        ///     Biases spawning toward the inner area of a biome layer.
        /// </summary>
        Inner = 1 << 4,

        /// <summary>
        ///     Biases spawning toward the outer edge of a biome layer.
        /// </summary>
        Outskirt = 1 << 5
    }
}