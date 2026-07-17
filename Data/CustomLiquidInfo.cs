using System.Collections.Generic;
using UnityEngine;

namespace CUCoreLib.Data
{
    /// <summary>
    /// Defines a custom liquid registered through <c>LiquidRegistry.Register</c>.
    /// </summary>
    public class CustomLiquidInfo
    {
        /// <summary>
        /// Tint used for the liquid in containers.
        /// </summary>
        public Color color = Color.white;

        /// <summary>
        /// Description locale text for the liquid.
        /// </summary>
        public string description;

        /// <summary>
        /// Whether the liquid can be used on skin when dragged onto woundview.
        /// </summary>
        public bool healthUsable;

        /// <summary>
        /// Whether the liquid can be injected when dragged onto woundview.
        /// </summary>
        public bool injectable;

        /// <summary>
        /// Sickness added by injection of the liquid. 
        /// </summary>
        public float injectionSickness = 1f;

        /// <summary>
        /// Reuses locale text from an item registration instead of registering separate liquid locale entries. Don't set this to true if the liquid is not associated with an item.
        /// </summary>
        public bool localeFromItem;

        /// <summary>
        /// Display name for the liquid.
        /// </summary>
        public string name;

        /// <summary>
        /// Callback invoked when the liquid is drunk.
        /// </summary>
        public LiquidType.OnDrink onDrink;

        /// <summary>
        /// Callback invoked when the liquid is used through a health action (applied to skin).
        /// </summary>
        public LiquidType.OnHealthUse onHealthUse;

        /// <summary>
        /// Callback invoked when the liquid is applied directly to a limb.
        /// Falls back to <see cref="onHealthUse"/> when unset.
        /// </summary>
        public LiquidType.OnHealthUse onApplyToLimb;

        /// <summary>
        /// Callback invoked when the liquid is injected into a limb.
        /// Falls back to <see cref="onHealthUse"/> when unset.
        /// </summary>
        public LiquidType.OnHealthUse onInject;

        /// <summary>
        /// Crafting-quality tags associated with the liquid.
        /// </summary>
        public List<CraftingQuality> qualities = new List<CraftingQuality>();

        /// <summary>
        /// Value per liter of this liquid.
        /// </summary>
        public float valuePerLiter;
    }
}
