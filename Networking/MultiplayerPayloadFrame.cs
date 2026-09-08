using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace CUCoreLib.Networking
{
    internal static class MultiplayerPayloadFrame
    {
        private const int MaxLength = 16 * 1024 * 1024;
        private const ushort Sentinel = ushort.MaxValue;
        private const int SegmentLength = 60000;

        internal static bool Write(object writer, string encoded, MethodInfo putString, MethodInfo putUShort,
            MethodInfo putUInt, MethodInfo putBytes)
        {
            if (encoded.Length <= SegmentLength)
            {
                if (putString != null)
                {
                    putString.Invoke(null, new[] { writer, encoded, true });
                    return true;
                }
                var put = writer.GetType().GetMethod("Put", new[] { typeof(string) });
                if (put == null) return false;
                put.Invoke(writer, new object[] { encoded });
                return true;
            }
            if (encoded.Length > MaxLength || putUShort == null || putUInt == null || putBytes == null)
                throw new InvalidDataException("CUCoreLib payload exceeds the 16 MiB frame limit.");
            var bytes = Encoding.ASCII.GetBytes(encoded);
            var count = (ushort)((bytes.Length + SegmentLength - 1) / SegmentLength);
            putUShort.Invoke(writer, new object[] { Sentinel });
            putUInt.Invoke(writer, new object[] { (uint)bytes.Length });
            putUShort.Invoke(writer, new object[] { count });
            for (var offset = 0; offset < bytes.Length; offset += SegmentLength)
            {
                var length = Math.Min(SegmentLength, bytes.Length - offset);
                var segment = new byte[length];
                Buffer.BlockCopy(bytes, offset, segment, 0, length);
                putBytes.Invoke(writer, new object[] { segment });
            }
            return true;
        }

        internal static string Read(object reader, MethodInfo getString, MethodInfo getUShort, MethodInfo getUInt,
            MethodInfo getSegment, MethodInfo getBytes, PropertyInfo availableBytes)
        {
            if (getUShort == null || getUInt == null || getSegment == null || getBytes == null)
            {
                if (getString == null) return reader.GetType().GetMethod("GetString")?.Invoke(reader, null) as string;
                var args = new[] { reader, null, true };
                getString.Invoke(null, args);
                return args[1] as string;
            }
            var length = (ushort)getUShort.Invoke(reader, null);
            if (length != Sentinel)
            {
                var segment = (ArraySegment<byte>)getSegment.Invoke(reader, new object[] { (int)length });
                return Encoding.ASCII.GetString(segment.Array, segment.Offset, segment.Count);
            }
            var total = (uint)getUInt.Invoke(reader, null);
            var count = (ushort)getUShort.Invoke(reader, null);
            if (total <= SegmentLength || total > MaxLength || count != (total + SegmentLength - 1) / SegmentLength ||
                availableBytes == null || (long)total + count * 2 > (int)availableBytes.GetValue(reader, null))
                throw new InvalidDataException("Invalid segmented payload header or truncated frame.");
            var bytes = new byte[(int)total];
            var offset = 0;
            for (var i = 0; i < count; i++)
            {
                var segment = (byte[])getBytes.Invoke(reader, null);
                var lengthExpected = Math.Min(SegmentLength, bytes.Length - offset);
                if (segment == null || segment.Length != lengthExpected) throw new InvalidDataException("Invalid segmented payload.");
                Buffer.BlockCopy(segment, 0, bytes, offset, segment.Length);
                offset += segment.Length;
            }
            return Encoding.ASCII.GetString(bytes);
        }
    }
}
