using System;

namespace CUCoreLib.Data
{
    /// <summary>
    /// Optional fixed loot sources for custom items. Leave null to use vanilla category fallback only.
    /// </summary>
    [Flags]
    public enum DropPool : ushort
    {
        None = 0,
        Corpse = 1 << 0,
        MedicalCrate = 1 << 1,
        FoodCrate = 1 << 2,
        ContainerCrate = 1 << 3,
        Trader1 = 1 << 4,
        Trader2 = 1 << 5,
        Trader3 = 1 << 6,
        AllTraders = Trader1 | Trader2 | Trader3,
        DropCapsule = 1 << 7,
        CapsuleContainer = 1 << 8
    }
}
