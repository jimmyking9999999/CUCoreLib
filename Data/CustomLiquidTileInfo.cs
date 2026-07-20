using System;
using UnityEngine;

namespace CUCoreLib.Data
{
    public enum LiquidTileVisualMode
    {
        ExistingLiquidPlusTint = 0,
        Material = 1,
        Sprite = 2,
        HighResImageGenerated = 3
    }

    public sealed class LiquidTileTouchContext
    {
        public Vector2Int BlockPosition;
        public Vector2 WorldPosition;
        public byte WorldByte;
        public float DeltaTime;
        public bool Entered;
        public bool Exited;
        public bool InWater;
    }

    public class CustomLiquidTileInfo
    {
        public string LiquidId;
        public float Buoyancy = 0.6f;
        public float Drag = 0.915f;
        public bool PushBodies = true;
        public float WetnessPerSecond = 20f;
        public float TemperaturePerSecond;
        public float SicknessPerSecond;
        public float DirtynessPerSecond;
        public float DisinfectPerSecond;
        public float SlipPerSecond;
        public float RagdollBarDrainPerSecond;
        public LiquidTileVisualMode VisualMode = LiquidTileVisualMode.ExistingLiquidPlusTint;
        public byte ExistingVisualLiquidByte = 1;
        public Color Tint = Color.white;
        public Material VisualMaterial;
        public Sprite VisualSprite;
        public Texture2D HighResImage;
        public float SpawnAmount;
        public int SpawnLayers = -1;
        public int MaxFloodFill = 128;
        public bool ConsumeOnDrink = true;
        public bool ConsumeOnFill = true;
        public string FillLiquidId;
        public LiquidType.OnDrink OnDrinkOverride;
        public Action<Body, LiquidTileTouchContext> OnTouch;
        public Action<Body, LiquidTileTouchContext> OnEnter;
        public Action<Body, LiquidTileTouchContext> OnExit;
    }
}
