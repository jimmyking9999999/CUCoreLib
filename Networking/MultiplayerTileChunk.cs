using System.IO;
using System.IO.Compression;

namespace CUCoreLib.Networking
{
    internal static class MultiplayerTileChunk
    {
        internal const int Size = 32;

        internal static void WriteTileId(Stream stream, ushort tileId)
        {
            stream.WriteByte((byte)tileId);
            stream.WriteByte((byte)(tileId >> 8));
        }

        internal static void Apply(byte[] compressed, ushort[,] blocks, int x, int y)
        {
            if (blocks == null || x < 0 || y < 0 ||
                x > blocks.GetLength(0) - Size || y > blocks.GetLength(1) - Size)
                throw new InvalidDataException("Tile chunk is outside the world.");
            if (compressed == null || compressed.Length > 4096)
                throw new InvalidDataException("Invalid compressed tile chunk size.");

            // Bound decompression before touching world state. Older hosts send one byte per tile.
            var raw = new byte[Size * Size * 2];
            var length = 0;
            using (var input = new MemoryStream(compressed, false))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            {
                int read;
                while (length < raw.Length && (read = deflate.Read(raw, length, raw.Length - length)) > 0)
                    length += read;
                if (deflate.ReadByte() != -1 || (length != Size * Size && length != raw.Length))
                    throw new InvalidDataException("Expected 1024 byte or 2048 byte tile chunk.");
            }

            var wide = length == raw.Length;
            var offset = 0;
            for (var row = 0; row < Size; row++)
            for (var column = 0; column < Size; column++)
            {
                var tileId = (ushort)raw[offset++];
                if (wide) tileId |= (ushort)(raw[offset++] << 8);
                blocks[x + column, y + row] = tileId;
            }
        }
    }
}