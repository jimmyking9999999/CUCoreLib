using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

            MethodInfo sender = null;
            MethodInfo receiver = null;
            var transpiler = AccessTools.Method(typeof(KrokMpWorldChunkPatches), nameof(SendTranspiler));
            var prefix = AccessTools.Method(typeof(KrokMpWorldChunkPatches), nameof(ReceivePrefix));
            var reliablePrefix = AccessTools.Method(typeof(KrokMpWorldChunkPatches), nameof(SendPrefix));
            try
            {
                sender = AccessTools.GetDeclaredMethods(chunkSync).Single(method =>
                    method.Name == "Server_Sendchunk" && method.GetParameters().Length == 3 &&
                    method.GetParameters()[1].ParameterType.FullName == "KrokoshaCasualtiesMP.NetPlayer" &&
                    method.GetParameters()[2].ParameterType == typeof(bool));
                receiver = AccessTools.Method(chunkSync, "ClientReceiver_WorldTilemapChunk");
                var parameters = receiver?.GetParameters();
                if (parameters == null || parameters.Length != 2 || !parameters[1].ParameterType.IsByRef)
                    throw new MissingMethodException("KrokMP tile chunk receiver signature changed.");
                var reader = parameters[1].ParameterType.GetElementType();
                _getByte = AccessTools.Method(reader, "GetByte", Type.EmptyTypes);
                _getBytes = AccessTools.Method(reader, "GetBytesWithLength", Type.EmptyTypes);
                if (_getByte?.ReturnType != typeof(byte) || _getBytes?.ReturnType != typeof(byte[]) ||
                    WorldBlocks == null || InstantiatingWorld == null)
                    throw new MissingMemberException("KrokMP tile chunk reader or world fields unavailable.");

                harmony.Patch(receiver, prefix: new HarmonyMethod(prefix));
                harmony.Patch(sender, prefix: new HarmonyMethod(reliablePrefix), transpiler: new HarmonyMethod(transpiler));
                _installed = true;
            }
            catch (Exception exception)
            {
                // Never leave only half of the wire format patched.
                if (sender != null)
                {
                    harmony.Unpatch(sender, transpiler);
                    harmony.Unpatch(sender, reliablePrefix);
                }
                if (receiver != null) harmony.Unpatch(receiver, prefix);
            }
        }

        private static void SendPrefix(ref bool __2)
        {
            // A 16-bit chunk may exceed the MTU; ReliableUnordered supports fragmentation.
            __2 = true;
        }

        internal static IEnumerable<CodeInstruction> SendTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var writeByte = AccessTools.Method(typeof(Stream), nameof(Stream.WriteByte), new[] { typeof(byte) });
            var matches = Enumerable.Range(1, code.Count - 1)
                .Where(index => code[index - 1].opcode == OpCodes.Conv_U1 && code[index].Calls(writeByte))
                .ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("Expected exactly one byte-truncating KrokMP tile write.");

            var match = matches[0];
            code[match - 1].opcode = OpCodes.Nop;
            code[match].opcode = OpCodes.Call;
            code[match].operand = AccessTools.Method(typeof(MultiplayerTileChunk), nameof(MultiplayerTileChunk.WriteTileId));
            return code;
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
