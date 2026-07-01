using System.IO;
using System.IO.Compression;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace CUCoreLib.Util;

public static class CompressionUtils
{
    public static byte[] CompressGZip(byte[] data)
    {
        if (data == null) return null;

        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    public static byte[] DecompressGZip(byte[] compressedData)
    {
        if (compressedData == null) return null;

        using var input = new MemoryStream(compressedData);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    public static byte[] CompressDeflate(byte[] data)
    {
        if (data == null) return null;

        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal))
        {
            deflate.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    public static byte[] DecompressDeflate(byte[] compressedData)
    {
        if (compressedData == null) return null;

        using var input = new MemoryStream(compressedData);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return output.ToArray();
    }
}