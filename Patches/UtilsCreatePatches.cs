using CUCoreLib.Helpers;
using CUCoreLib.Registries;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace CUCoreLib.Patches
{
    [HarmonyPatch]
    internal static class UtilsCreatePatches
    {
        private static bool Prepare()
        {
            return TargetMethod() != null;
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method("Utils:Create", new[] { typeof(string), typeof(Vector2), typeof(float) });
        }

        [HarmonyPrefix]
        private static bool CreateItemFallback(string id, Vector2 pos, float rot, ref GameObject __result)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return true;
            }

            if (Resources.Load<GameObject>(id) != null)
            {
                return true;
            }

            if (ItemRegistry.RegisteredItems.ContainsKey(id) || BuildingEntityRegistry.IsRegistered(id))
            {
                __result = CustomInstantiate.InstantiateReturn(id, pos, Quaternion.Euler(0f, 0f, rot));
                return false;
            }

            return true;
        }
    }
}
