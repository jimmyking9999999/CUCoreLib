using System.Reflection;
using CUCoreLib.Helpers;
using UnityEngine;

namespace CUCoreLib.Util;

public static class AssetUtils
{
    public static Sprite LoadEmbeddedSprite(string resourcePath, float pixelsPerUnit = AssetLoader.PPU_UI,
        Assembly sourceAssembly = null)
    {
        return AssetLoader.LoadEmbeddedSprite(resourcePath, pixelsPerUnit, sourceAssembly);
    }
}