using System;
using System.Reflection;
using CUCoreLib.Networking;
using HarmonyLib;
using UnityEngine;

namespace CUCoreLib.Patches
{
    internal static class KrokMpWorldChunkPatches
    {
        private static bool _installed;
        private static MethodInfo _getByte;
        private static MethodInfo _getBytes;
        private static readonly FieldInfo WorldBlocks = AccessTools.Field(typeof(WorldGeneration), "worldBlocks");
        private static readonly FieldInfo InstantiatingWorld = AccessTools.Field(typeof(WorldGeneration), "instantiatingWorld");

        internal static bool IsInstalled => _installed;

        internal static void Install(Harmony harmony, Type chunkSync)
        {
            if (_installed || chunkSync == null) return;

            MethodInfo receiver = null;
            var prefix = AccessTools.Method(typeof(KrokMpWorldChunkPatches), nameof(ReceivePrefix));
            try
            {
                receiver = AccessTools.Method(chunkSync, "ClientReceiver_WorldTilemapChunk");
                var parameters = receiver?.GetParameters();
                if (parameters == null || parameters.Length != 2)
                    throw new MissingMethodException("KrokMP tile chunk receiver signature changed.");
                // v4 declared the reader `ref`, v5 declares it by value
                // Both are okay in ccl
                var readerParameter = parameters[1].ParameterType;
                var reader = readerParameter.IsByRef ? readerParameter.GetElementType() : readerParameter;
                _getByte = AccessTools.Method(reader, "GetByte", Type.EmptyTypes);
                _getBytes = AccessTools.Method(reader, "GetBytesWithLength", Type.EmptyTypes);
                if (_getByte?.ReturnType != typeof(byte) || _getBytes?.ReturnType != typeof(byte[]) ||
                    WorldBlocks == null || InstantiatingWorld == null)
                    throw new MissingMemberException("KrokMP tile chunk reader or world fields unavailable.");

                harmony.Patch(receiver, prefix: new HarmonyMethod(prefix));
                _installed = true;
            }
            catch (Exception exception)
            {
                if (receiver != null) harmony.Unpatch(receiver, prefix);
                CUCoreLibPlugin.Log?.LogWarning("That's not good: " + exception);
            }
        }

        private static bool ReceivePrefix(object[] __args)
        {
            var world = WorldGeneration.world;
            if (world == null || (bool)InstantiatingWorld.GetValue(world)) return false;
            var blocks = WorldBlocks.GetValue(world) as ushort[,];
            if (blocks == null) return false;

            var reader = __args[1];
            var x = (byte)_getByte.Invoke(reader, null) * MultiplayerTileChunk.Size;
            var y = (byte)_getByte.Invoke(reader, null) * MultiplayerTileChunk.Size;
            MultiplayerTileChunk.Apply((byte[])_getBytes.Invoke(reader, null), blocks, x, y);
            world.UpdateChunkClosest(new Vector2Int(x, y));
            return false;
        }
    }
}
