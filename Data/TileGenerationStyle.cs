using System;

namespace CUCoreLib.Data
{
    [Flags]
    public enum TileGenerationStyle : byte
    {
        None = 0,
        Vein = 1 << 0,
        HeavyVeins = 1 << 1,
        Singular = 1 << 2,
        Stripe = 1 << 3,
        Inner = 1 << 4,
        Outskirt = 1 << 5
    }
}