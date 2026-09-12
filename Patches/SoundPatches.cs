using CUCoreLib.Registries;
using HarmonyLib;
using UnityEngine;

namespace CUCoreLib.Patches
{
    internal static class SoundPatches
    {
        [HarmonyPatch(
            typeof(Sound), 
            nameof(Sound.Play),
            typeof(string),
            typeof(Vector2), 
            typeof(bool),
            typeof(bool),
            typeof(Transform), 
            typeof(float),
            typeof(float),
            typeof(bool), 
            typeof(bool))]
        [HarmonyPrefix]
        private static bool PlayTileHitSound(
            string clip,
            Vector2 pos,
            bool twoDimensional,
            bool pitchShift,
            Transform follow,
            float volume, 
            float pitch,
            bool noReverb,
            bool ignoreMixer,
            ref AudioSource __result)
        {
            return !TileRegistry.TryPlayHitSoundToken(
                clip,
                pos, 
                twoDimensional,
                pitchShift,
                follow, 
                volume,
                pitch,
                noReverb,
                ignoreMixer, 
                out __result);
            // Logger.LogInfo($"No custom hit sound found for token '{clip}'.");
        }
    }
}