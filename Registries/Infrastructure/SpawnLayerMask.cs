using System.Linq;

namespace CUCoreLib.Registries.Infrastructure
{
    internal static class SpawnLayerMask
    {
        internal const int All = -1;

        internal static int FromLayerNumber(int layerNumber)
        {
            return layerNumber > 0 && layerNumber <= 31
                ? 1 << (layerNumber - 1)
                : 0;
        }

        internal static int Combine(int[] layerNumbers)
        {
            if (layerNumbers == null || layerNumbers.Length == 0) return 0;
            return layerNumbers.Aggregate(0,
                (current, t) => current | FromLayerNumber(t));
        }

        internal static int Excluding(int[] excludedLayerNumbers)
        {
            if (excludedLayerNumbers == null || excludedLayerNumbers.Length == 0)
                return All;

            return excludedLayerNumbers.Select(FromLayerNumber)
                .Where(excludedMask => excludedMask != 0).Aggregate(All,
                    (current, excludedMask) => current & ~excludedMask);
        }
    }
}
