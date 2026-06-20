using System.Collections.Generic;
using UnityEngine;

namespace CUCoreLib.Data
{
    public class CustomLiquidInfo
    {
        public string name;
        public string description;
        public Color color = Color.white;
        public float valuePerLiter;
        public LiquidType.OnDrink onDrink;
        public LiquidType.OnHealthUse onHealthUse;
        public bool healthUsable;
        public bool injectable;
        public float injectionSickness = 1f;
        public bool localeFromItem;
        public List<CraftingQuality> qualities = new List<CraftingQuality>();
    }
}
