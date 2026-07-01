using UnityEngine;

namespace CUCoreLib.Util;

public static class WorldUtils
{
    public static void FillBlocks(int sx, int sy, int ex, int ey, ushort block)
    {
        var w = WorldGeneration.world;
        if (w == null) return;
        var csx = Mathf.Clamp(sx, 0, (int)w.width - 2);
        var csy = Mathf.Clamp(sy, 0, (int)w.height - 2);
        var cex = Mathf.Clamp(ex, 0, (int)w.width - 2);
        var cey = Mathf.Clamp(ey, 0, (int)w.height - 2);
        for (var x = csx; x <= cex; x++)
        for (var y = csy; y <= cey; y++)
            w.SetBlockNoUpdate(new Vector2Int(x, y), block);
        for (var cx = csx / WorldGeneration.CHUNKSIZE; cx <= cex / WorldGeneration.CHUNKSIZE; cx++)
        for (var cy = csy / WorldGeneration.CHUNKSIZE; cy <= cey / WorldGeneration.CHUNKSIZE; cy++)
            w.UpdateChunk(new Vector2Int(cx, cy));
    }
}
