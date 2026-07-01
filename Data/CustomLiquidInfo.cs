using System.Collections.Generic;
using UnityEngine;

namespace CUCoreLib.Data;

public class CustomLiquidInfo
{
    public Color color = Color.white;
    public string description;
    public bool healthUsable;
    public bool injectable;
    public float injectionSickness = 1f;
    public bool localeFromItem;
    public string name;
    public LiquidType.OnDrink onDrink;
    public LiquidType.OnHealthUse onHealthUse;
    public List<CraftingQuality> qualities = [];
    public float valuePerLiter;
}