namespace CUCoreLib.Registries.Infrastructure
{
    internal static class SpawnLayerMask
    {
        internal const int All = -1;

        internal static int FromLayerNumber(int layerNumber)
        {
            return layerNumber > 0 && layerNumber <= 31 ? 1 << (layerNumber - 1) : 0;
        }

        internal static int Combine(int[] layerNumbers)
        {
            if (layerNumbers == null || layerNumbers.Length == 0) return 0;
            var mask = 0;
            for (var i = 0; i < layerNumbers.Length; i++) mask |= FromLayerNumber(layerNumbers[i]);
            return mask;
        }

        internal static int Excluding(int[] excludedLayerNumbers)
        {
            var mask = All;
            if (excludedLayerNumbers == null || excludedLayerNumbers.Length == 0) return mask;
            for (var i = 0; i < excludedLayerNumbers.Length; i++)
            {
                var excludedMask = FromLayerNumber(excludedLayerNumbers[i]);
                if (excludedMask != 0) mask &= ~excludedMask;
            }
            return mask;
        }
    }
}
