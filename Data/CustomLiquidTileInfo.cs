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
        public float DeltaTime;
        public bool Entered;
        public bool Exited;
        public bool InWater;
        public byte WorldByte;
        public Vector2 WorldPosition;
    }

    public class CustomLiquidTileInfo
    {
        public float Buoyancy = 0.6f;
        public bool ConsumeOnDrink = true;
        public bool ConsumeOnFill = true;
        public float DirtynessPerSecond;
        public float DisinfectPerSecond;
        public float Drag = 0.915f;
        public byte ExistingVisualLiquidByte = 1;
        public string FillLiquidId;
        public Texture2D HighResImage;
        public string LiquidId;
        public int MaxFloodFill = 128;
        public LiquidType.OnDrink OnDrinkOverride;
        public Action<Body, LiquidTileTouchContext> OnEnter;
        public Action<Body, LiquidTileTouchContext> OnExit;
        public Action<Body, LiquidTileTouchContext> OnTouch;
        public bool PushBodies = true;
        public float RagdollBarDrainPerSecond;
        public float SicknessPerSecond;
        public float SlipPerSecond;
        public float SpawnAmount;
        public int SpawnLayers = -1;
        public float TemperaturePerSecond;
        public Color Tint = Color.white;
        public Material VisualMaterial;
        public LiquidTileVisualMode VisualMode = LiquidTileVisualMode.ExistingLiquidPlusTint;
        public Sprite VisualSprite;
        public float WetnessPerSecond = 20f;
    }
}